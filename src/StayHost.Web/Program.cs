using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Infrastructure;
using StayHost.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// docs/03 §1 fixes the fee rates; the admin console (QT-06) needs to change them
// without a deploy, so they are bound from configuration with the spec as default.
var pricing = builder.Configuration.GetSection("Pricing").Get<PricingSettings>();
if (pricing is not null) PricingSettings.Current = pricing;

// docs/01 TC-07 — how long promotional balance lasts. docs/07 §16 settled the
// numbers on 11/08/2026 (twelve months for what the platform gave away, no expiry
// on a gift card) and appsettings.json carries them; an unset value still means
// that kind never lapses.
var credits = builder.Configuration.GetSection("Credits").Get<CreditSettings>();
if (credits is not null) CreditSettings.Current = credits;

// docs/01 AT-01 — pre-publish review of new listings. Off by default (a host
// publishes and the place is live at once), which is how the platform shipped;
// the queue and search gate are built and wait only on this switch.
var moderation = builder.Configuration.GetSection("Moderation").Get<ModerationSettings>();
if (moderation is not null) ModerationSettings.Current = moderation;

// docs/01 TĐ-03, TN-06 — machine translation. Both compose files run a
// LibreTranslate container and set Translation__Provider, so a deployment has this
// on without buying anything; a bare `dotnet run` has no provider named and the
// "Dịch" button never shows, like the social-login buttons. A paid provider is
// still an option — its key arrives as Translation__ApiKey, never appsettings.json.
var translation = builder.Configuration.GetSection("Translation").Get<TranslationSettings>() ?? new();
TranslationSettings.Current = translation;
var translationKey = builder.Configuration["Translation:ApiKey"];
builder.Services.AddHttpClient("translation");
if (translation.IsConfigured)
{
    if (string.Equals(translation.Provider, "google", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(translationKey))
        builder.Services.AddScoped<ITranslator>(sp => new GoogleTranslator(
            sp.GetRequiredService<IHttpClientFactory>(), translationKey!,
            sp.GetRequiredService<ILogger<GoogleTranslator>>()));
    else if (string.Equals(translation.Provider, "libretranslate", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(translation.Url))
        builder.Services.AddScoped<ITranslator>(sp => new LibreTranslator(
            sp.GetRequiredService<IHttpClientFactory>(), translation.Url!, translationKey,
            sp.GetRequiredService<ILogger<LibreTranslator>>()));
    else
        builder.Services.AddScoped<ITranslator, StubTranslator>();
}
// Factory so ITranslator can be absent (feature off) without DI failing to build.
builder.Services.AddScoped(sp => new TranslationService(
    sp.GetRequiredService<StayHostDbContext>(), sp.GetService<ITranslator>()));

// docs/01 TK-02 — the provider client ids live in configuration so one build can
// run against a test project on a laptop and the real one on the server. Nothing
// here is a secret except the Facebook app secret, which comes from the
// environment file rather than appsettings.json.
// docs/07 §2.3 — the company account a VietQR credits. Off until an account
// number exists, so the method never shows up leading nowhere.
var bankTransfer = builder.Configuration.GetSection("BankTransfer").Get<BankTransferSettings>() ?? new();
builder.Services.AddSingleton(bankTransfer);

var externalLogin = builder.Configuration.GetSection("ExternalLogin").Get<ExternalLoginSettings>() ?? new();
builder.Services.AddSingleton(externalLogin);
builder.Services.AddHttpClient("external-login");
builder.Services.AddScoped<ExternalTokenVerifier>();

// Outgoing mail. Everything queues rows in EmailMessages; EmailWorker drains
// them through whichever sender is registered here. With no Email:Host the
// queue simply holds the mail — nothing pretends to send. The SMTP password
// arrives as Email__Password from the environment, never appsettings.json.
var email = builder.Configuration.GetSection("Email").Get<EmailSettings>() ?? new();
builder.Services.AddSingleton(email);
if (email.IsConfigured) builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
else builder.Services.AddScoped<IEmailSender, UnconfiguredEmailSender>();
builder.Services.AddScoped<EmailDispatcher>();
builder.Services.AddHostedService<EmailWorker>();

var connectionString =
    builder.Configuration.GetConnectionString("Postgres")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? "Host=localhost;Port=5432;Database=stayhost;Username=stayhost;Password=stayhost";

builder.Services.AddDbContext<StayHostDbContext>(o => o
    .UseNpgsql(connectionString, npg => npg.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null)));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<BadgeService>();
builder.Services.AddScoped<PayoutService>();
builder.Services.AddScoped<PaymentCompletion>();
builder.Services.AddScoped<CardAuthSweeper>();
builder.Services.AddScoped<BankTransferService>();
builder.Services.AddScoped<SavedSearchSweeper>();
builder.Services.AddScoped<AdminGate>();
builder.Services.AddScoped<ThreadMessenger>();
builder.Services.AddScoped<AdminAudit>();
builder.Services.AddScoped<PaymentGateway>();
builder.Services.AddScoped<BalanceCollector>();
builder.Services.AddScoped<RiskWatch>();
builder.Services.AddScoped<SplitBillService>();
builder.Services.AddScoped<ExperienceService>();
builder.Services.AddScoped<ServiceMarketService>();
builder.Services.AddScoped<WalletService>();
builder.Services.AddScoped<CouponService>();
builder.Services.AddScoped<ShieldService>();
builder.Services.AddScoped<HostAccess>();
builder.Services.AddScoped<CalendarSyncService>();
builder.Services.AddHttpClient("ical");
builder.Services.AddHostedService<CalendarSyncWorker>();
builder.Services.AddHostedService<BookingLifecycleWorker>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<IdentityService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddControllers();
builder.Services.AddResponseCompression(o => o.EnableForHttps = true);
builder.Services.AddHealthChecks().AddDbContextCheck<StayHostDbContext>();

// docs/08 §3 — the admin two-factor gate. Configurable only so a server with no
// mail configured is not locked out of its own console; see AdminActions.
AdminActions.RequireTwoFactor = builder.Configuration.GetValue("Admin:RequireTwoFactor", true);

var app = builder.Build();

// The web container starts alongside Postgres; retry until the database answers, then migrate + seed.
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StayHostDbContext>();
    var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    for (var attempt = 1; attempt <= 15; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            await DbSeeder.SeedAsync(db);

            // docs/08 §3 — "Bắt buộc bảo mật 2 lớp. Không bật thì không đăng nhập
            // được, không có ngoại lệ." An admin row saved before that rule
            // existed has the flag off, and the rule then locks the console
            // against everyone: turning two-factor on requires signing in, and
            // signing in requires two-factor. Nobody can climb out of that from
            // the UI, so the invalid state is repaired here instead.
            //
            // Admin:RequireTwoFactor=false stands the whole gate down for a
            // deployment that cannot send the code yet. The flag on the rows
            // follows the setting, because a live TwoFactorEnabled would still
            // send a code nobody can read.
            if (AdminActions.RequireTwoFactor)
            {
                var repaired = await db.Users
                    .Where(u => u.Role == UserRole.Admin && !u.TwoFactorEnabled)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.TwoFactorEnabled, true));

                if (repaired > 0)
                    log.LogWarning("Đã bật bảo mật 2 lớp cho {Count} tài khoản quản trị (docs/08 §3).", repaired);
            }
            else
            {
                var stoodDown = await db.Users
                    .Where(u => u.Role == UserRole.Admin && u.TwoFactorEnabled)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.TwoFactorEnabled, false));

                log.LogWarning(
                    "BẢO MẬT 2 LỚP CHO QUẢN TRỊ ĐANG TẮT (Admin:RequireTwoFactor=false) — {Count} tài khoản. " +
                    "Mật khẩu là thứ duy nhất chặn người lạ vào trang quản trị. " +
                    "Bật lại ngay khi gửi được email.", stoodDown);
            }

            // The seeded console account is admin@stayhost.vn, a domain nobody
            // owns — so the six-digit code of §3 is posted to an address that
            // cannot be read. Setting Admin:Email (ADMIN_EMAIL in the deploy env
            // file) moves the account to a real inbox, which is also the address
            // it is then signed in with. Left unset, nothing changes.
            var adminEmail = (builder.Configuration["Admin:Email"] ?? "").Trim().ToLowerInvariant();
            if (adminEmail.Length > 0)
            {
                var console = await db.Users
                    .Where(u => u.Role == UserRole.Admin)
                    .OrderBy(u => u.Id)
                    .FirstOrDefaultAsync();

                if (console is null)
                    log.LogWarning("Admin:Email được đặt nhưng chưa có tài khoản quản trị nào.");
                else if (console.Email == adminEmail)
                    log.LogInformation("Tài khoản quản trị đã dùng {Email}.", adminEmail);
                else if (await db.Users.AnyAsync(u => u.Email == adminEmail && u.Id != console.Id))
                    log.LogWarning("Không đổi được email quản trị: {Email} đã thuộc tài khoản khác.", adminEmail);
                else
                {
                    var was = console.Email;
                    console.Email = adminEmail;
                    // The address is where the sign-in code goes, so control of
                    // it is proved by the next sign-in rather than assumed here.
                    console.EmailConfirmed = false;
                    await db.SaveChangesAsync();

                    log.LogWarning("Đã chuyển tài khoản quản trị từ {Was} sang {Now}.", was, adminEmail);
                }
            }

            // docs/01 AT-07 — help articles seed on their own, so adding one
            // later does not need the whole catalogue rebuilt.
            await HelpSeeder.SeedAsync(db);
            await ExperienceSeeder.SeedAsync(db);
            await ServiceSeeder.SeedAsync(db);
            // Last of the three: it books the sessions and jobs the other two
            // created, so both have to exist before it runs.
            await ReviewSeeder.SeedAsync(db);
            await HotelSeeder.SeedAsync(db);
            await FeatureFlagSeeder.SeedAsync(db);
            log.LogInformation("Database ready.");
            break;
        }
        catch (Exception ex) when (attempt < 15)
        {
            log.LogWarning("Database not ready ({Attempt}/15): {Message}", attempt, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseResponseCompression();

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path.Value ?? "";
        ctx.Context.Response.Headers.CacheControl =
            app.Environment.IsDevelopment() || path.EndsWith(".html")
                ? "no-cache"
                : "public,max-age=3600";
    }
});

// Every request gets a wishlist identity, including the first HTML hit.
app.Use(async (ctx, next) =>
{
    ctx.SessionId();
    await next();
});

// docs/08 §7.6 — the nine things a support session inside somebody's account
// must never do, refused before any controller sees the request.
app.UseMiddleware<StayHost.Web.Infrastructure.ImpersonationGuard>();

app.MapControllers();
app.MapHealthChecks("/health");
app.Map("/error", () => Results.Problem("Đã có lỗi xảy ra."));

// Client-side routes (/rooms/..., /wishlists, /host, /trips) fall back to the SPA shell.
app.MapFallbackToFile("index.html");

app.Run();
