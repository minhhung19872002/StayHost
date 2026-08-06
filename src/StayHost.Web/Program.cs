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
builder.Services.AddScoped<ThreadMessenger>();
builder.Services.AddScoped<AdminAudit>();
builder.Services.AddScoped<PaymentGateway>();
builder.Services.AddScoped<BalanceCollector>();
builder.Services.AddScoped<RiskWatch>();
builder.Services.AddScoped<SplitBillService>();
builder.Services.AddScoped<ExperienceService>();
builder.Services.AddScoped<HostAccess>();
builder.Services.AddScoped<CalendarSyncService>();
builder.Services.AddHttpClient("ical");
builder.Services.AddHostedService<CalendarSyncWorker>();
builder.Services.AddHostedService<BookingLifecycleWorker>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddControllers();
builder.Services.AddResponseCompression(o => o.EnableForHttps = true);
builder.Services.AddHealthChecks().AddDbContextCheck<StayHostDbContext>();

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
            // docs/01 AT-07 — help articles seed on their own, so adding one
            // later does not need the whole catalogue rebuilt.
            await HelpSeeder.SeedAsync(db);
            await ExperienceSeeder.SeedAsync(db);
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

app.MapControllers();
app.MapHealthChecks("/health");
app.Map("/error", () => Results.Problem("Đã có lỗi xảy ra."));

// Client-side routes (/rooms/..., /wishlists, /host, /trips) fall back to the SPA shell.
app.MapFallbackToFile("index.html");

app.Run();
