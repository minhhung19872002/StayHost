using Microsoft.EntityFrameworkCore;
using StayHost.Domain;

namespace StayHost.Infrastructure;

public class StayHostDbContext(DbContextOptions<StayHostDbContext> options) : DbContext(options)
{
    public DbSet<HostProfile> Hosts => Set<HostProfile>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<ListingImage> ListingImages => Set<ListingImage>();
    public DbSet<Amenity> Amenities => Set<Amenity>();
    public DbSet<ListingAmenity> ListingAmenities => Set<ListingAmenity>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Wishlist> Wishlists => Set<Wishlist>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<UserToken> UserTokens => Set<UserToken>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<MessageThread> MessageThreads => Set<MessageThread>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<CalendarBlock> CalendarBlocks => Set<CalendarBlock>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ListingReport> ListingReports => Set<ListingReport>();
    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();
    public DbSet<PriceRule> PriceRules => Set<PriceRule>();
    public DbSet<GuestReview> GuestReviews => Set<GuestReview>();
    public DbSet<TaxRule> TaxRules => Set<TaxRule>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<BookingEvent> BookingEvents => Set<BookingEvent>();
    public DbSet<ResolutionCase> ResolutionCases => Set<ResolutionCase>();
    public DbSet<ResolutionEvent> ResolutionEvents => Set<ResolutionEvent>();
    public DbSet<AdminAuditEntry> AdminAudit => Set<AdminAuditEntry>();
    public DbSet<QuickReply> QuickReplies => Set<QuickReply>();
    public DbSet<CoHost> CoHosts => Set<CoHost>();
    public DbSet<CalendarFeed> CalendarFeeds => Set<CalendarFeed>();
    public DbSet<HelpArticle> HelpArticles => Set<HelpArticle>();
    public DbSet<RiskFlag> RiskFlags => Set<RiskFlag>();
    public DbSet<BillSplit> BillSplits => Set<BillSplit>();
    public DbSet<BillShare> BillShares => Set<BillShare>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<HostProfile>(e =>
        {
            e.ToTable("hosts");
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Initials).HasMaxLength(4);
            e.Property(x => x.PayoutBankName).HasMaxLength(120);
            e.Property(x => x.PayoutAccountName).HasMaxLength(120);
            e.Property(x => x.PayoutAccountLast4).HasMaxLength(4);
            e.HasOne(x => x.User).WithOne(u => u.HostProfile)
                .HasForeignKey<HostProfile>(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Initials).HasMaxLength(4);
            e.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
            e.Property(x => x.PasswordSalt).HasMaxLength(100).IsRequired();
            e.Ignore(x => x.HostProfileId);
        });

        b.Entity<AuthSession>(e =>
        {
            e.ToTable("auth_sessions");
            e.HasIndex(x => x.Token).IsUnique();
            e.Property(x => x.Token).HasMaxLength(88).IsRequired();
            e.Property(x => x.UserAgent).HasMaxLength(300);
            e.HasOne(x => x.User).WithMany(u => u.Sessions)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Ignore(x => x.IsActive);
        });

        b.Entity<UserToken>(e =>
        {
            e.ToTable("user_tokens");
            e.HasIndex(x => x.Token).IsUnique();
            e.Property(x => x.Token).HasMaxLength(88).IsRequired();
            e.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Payment>(e =>
        {
            e.ToTable("payments");
            e.HasIndex(x => x.Reference).IsUnique();
            e.Property(x => x.Reference).HasMaxLength(24).IsRequired();
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Method).HasMaxLength(20);
            e.Property(x => x.CardLast4).HasMaxLength(4);
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.PlatformFee).HasPrecision(12, 2);
            e.Property(x => x.HostPayout).HasPrecision(12, 2);
            e.HasOne(x => x.Booking).WithOne(bk => bk.Payment)
                .HasForeignKey<Payment>(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<MessageThread>(e =>
        {
            e.ToTable("message_threads");
            e.HasIndex(x => new { x.ListingId, x.GuestUserId }).IsUnique();
            e.HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.GuestUser).WithMany().HasForeignKey(x => x.GuestUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.HostUser).WithMany().HasForeignKey(x => x.HostUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Message>(e =>
        {
            e.ToTable("messages");
            e.Property(x => x.Body).HasMaxLength(4000).IsRequired();
            e.HasOne(x => x.Thread).WithMany(t => t.Messages)
                .HasForeignKey(x => x.ThreadId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Attachments).HasMaxLength(2000);
            e.HasOne(x => x.SenderUser).WithMany()
                .HasForeignKey(x => x.SenderUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<QuickReply>(e =>
        {
            e.ToTable("quick_replies");
            e.HasIndex(x => new { x.HostUserId, x.SortOrder });
            e.Property(x => x.Title).HasMaxLength(80).IsRequired();
            e.Property(x => x.Body).HasMaxLength(2000).IsRequired();
            e.HasOne(x => x.HostUser).WithMany()
                .HasForeignKey(x => x.HostUserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CoHost>(e =>
        {
            e.ToTable("co_hosts");
            e.HasIndex(x => new { x.OwnerUserId, x.Email });
            e.HasIndex(x => x.InviteToken).IsUnique();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.InviteToken).HasMaxLength(64).IsRequired();
            e.HasOne(x => x.OwnerUser).WithMany()
                .HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CoHostUser).WithMany()
                .HasForeignKey(x => x.CoHostUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Listing).WithMany()
                .HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CalendarFeed>(e =>
        {
            e.ToTable("calendar_feeds");
            e.HasIndex(x => x.ListingId);
            e.Property(x => x.Label).HasMaxLength(80).IsRequired();
            e.Property(x => x.Url).HasMaxLength(600).IsRequired();
            e.Property(x => x.LastError).HasMaxLength(400);
            e.HasOne(x => x.Listing).WithMany(l => l.CalendarFeeds)
                .HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<BillSplit>(e =>
        {
            e.ToTable("bill_splits");
            e.HasIndex(x => x.BookingId).IsUnique();
            e.HasOne(x => x.Booking).WithMany()
                .HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.OrganiserUser).WithMany()
                .HasForeignKey(x => x.OrganiserUserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<BillShare>(e =>
        {
            e.ToTable("bill_shares");
            e.HasIndex(x => x.Token).IsUnique();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.Token).HasMaxLength(64).IsRequired();
            e.Property(x => x.CardLast4).HasMaxLength(4);
            e.HasOne(x => x.Split).WithMany(s => s.Shares)
                .HasForeignKey(x => x.SplitId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<HelpArticle>(e =>
        {
            e.ToTable("help_articles");
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Slug).HasMaxLength(120).IsRequired();
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Category).HasMaxLength(80).IsRequired();
            e.Property(x => x.Summary).HasMaxLength(400);
            e.Property(x => x.SearchText).HasMaxLength(6000);
        });

        b.Entity<RiskFlag>(e =>
        {
            e.ToTable("risk_flags");
            e.HasIndex(x => new { x.UserId, x.Kind, x.Status });
            e.Property(x => x.Summary).HasMaxLength(200).IsRequired();
            e.Property(x => x.Detail).HasMaxLength(400);
            e.Property(x => x.Resolution).HasMaxLength(400);
            e.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Booking).WithMany()
                .HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<PriceRule>(e =>
        {
            e.ToTable("price_rules");
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.Property(x => x.NightlyRate).HasPrecision(12, 2);
            e.HasIndex(x => new { x.ListingId, x.From });
            e.HasOne(x => x.Listing).WithMany(l => l.PriceRules)
                .HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<GuestReview>(e =>
        {
            e.ToTable("guest_reviews");
            e.HasIndex(x => x.BookingId).IsUnique();
            e.Property(x => x.Text).HasMaxLength(2000);
            e.HasOne(x => x.Booking).WithMany()
                .HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.HostUser).WithMany()
                .HasForeignKey(x => x.HostUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.GuestUser).WithMany()
                .HasForeignKey(x => x.GuestUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Notification>(e =>
        {
            e.ToTable("notifications");
            e.HasIndex(x => new { x.UserId, x.ReadAt });
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Body).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Link).HasMaxLength(300);
            e.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ListingReport>(e =>
        {
            e.ToTable("listing_reports");
            e.HasIndex(x => x.Status);
            e.Property(x => x.Reason).HasMaxLength(120).IsRequired();
            e.Property(x => x.Detail).HasMaxLength(2000);
            e.Property(x => x.Resolution).HasMaxLength(500);
            e.Property(x => x.SessionId).HasMaxLength(64);
            e.HasOne(x => x.Listing).WithMany()
                .HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ReporterUser).WithMany()
                .HasForeignKey(x => x.ReporterUserId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<EmailMessage>(e =>
        {
            e.ToTable("email_messages");
            e.Property(x => x.ToEmail).HasMaxLength(200).IsRequired();
            e.Property(x => x.ToName).HasMaxLength(150);
            e.Property(x => x.Subject).HasMaxLength(250).IsRequired();
            e.Property(x => x.Body).HasMaxLength(4000).IsRequired();
            e.Property(x => x.Error).HasMaxLength(500);
        });

        b.Entity<CalendarBlock>(e =>
        {
            e.ToTable("calendar_blocks");
            e.Property(x => x.Note).HasMaxLength(200);
            e.Property(x => x.ExternalUid).HasMaxLength(300);
            e.HasIndex(x => new { x.FeedId, x.ExternalUid });
            e.HasOne(x => x.Listing).WithMany(l => l.Blocks)
                .HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Feed).WithMany()
                .HasForeignKey(x => x.FeedId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Amenity>(e =>
        {
            e.ToTable("amenities");
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(40).IsRequired();
            e.Property(x => x.Label).HasMaxLength(80).IsRequired();
        });

        b.Entity<Listing>(e =>
        {
            e.ToTable("listings");
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.City);
            e.HasIndex(x => x.PricePerNight);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.City).HasMaxLength(100).IsRequired();
            e.Property(x => x.PricePerNight).HasPrecision(12, 2);
            e.Property(x => x.CleaningFee).HasPrecision(12, 2);
            e.Property(x => x.WeekendSurchargeRate).HasPrecision(5, 4);
            e.Property(x => x.ExtraGuestFee).HasPrecision(12, 2);
            e.Property(x => x.PetFee).HasPrecision(12, 2);
            e.Property(x => x.TimeZoneId).HasMaxLength(60);
            // Diacritic-free haystack for docs/03 §6; indexed for the LIKE the search runs.
            e.Property(x => x.SearchText).HasMaxLength(400);
            e.HasIndex(x => x.SearchText);
            e.Property(x => x.BedLayoutJson).HasColumnType("jsonb");
            e.Property(x => x.LicenseNumber).HasMaxLength(80);
            e.Property(x => x.SecurityCameraNote).HasMaxLength(500);
            e.HasOne(x => x.Host).WithMany(h => h.Listings)
                .HasForeignKey(x => x.HostId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ListingImage>(e =>
        {
            e.ToTable("listing_images");
            e.Property(x => x.Url).HasMaxLength(500).IsRequired();
            e.HasOne(x => x.Listing).WithMany(l => l.Images)
                .HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ListingAmenity>(e =>
        {
            e.ToTable("listing_amenities");
            e.HasKey(x => new { x.ListingId, x.AmenityId });
            e.HasOne(x => x.Listing).WithMany(l => l.Amenities)
                .HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Amenity).WithMany(a => a.Listings)
                .HasForeignKey(x => x.AmenityId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Review>(e =>
        {
            e.ToTable("reviews");
            e.Property(x => x.AuthorName).HasMaxLength(120).IsRequired();
            e.Property(x => x.Text).HasMaxLength(2000);
            e.Property(x => x.HostReply).HasMaxLength(2000);
            e.Property(x => x.PrivateNote).HasMaxLength(2000);
            // Only published reviews are ever queried for display.
            e.HasIndex(x => new { x.ListingId, x.PublishedAt });
            e.HasOne(x => x.Listing).WithMany(l => l.Reviews)
                .HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.AuthorUser).WithMany()
                .HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Booking).WithMany()
                .HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Wishlist>(e =>
        {
            e.ToTable("wishlists");
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.Property(x => x.SessionId).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.SessionId);
            e.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Favorite>(e =>
        {
            e.ToTable("favorites");
            e.Property(x => x.Note).HasMaxLength(300);
            e.HasOne(x => x.Wishlist).WithMany(w => w.Items)
                .HasForeignKey(x => x.WishlistId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.SessionId, x.ListingId }).IsUnique();
            e.HasIndex(x => x.UserId);
            e.Property(x => x.SessionId).HasMaxLength(64).IsRequired();
            e.HasOne(x => x.Listing).WithMany()
                .HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Booking>(e =>
        {
            e.ToTable("bookings");
            e.HasIndex(x => x.Reference).IsUnique();
            e.Property(x => x.Reference).HasMaxLength(20).IsRequired();
            e.Property(x => x.SessionId).HasMaxLength(64).IsRequired();
            foreach (var money in new[]
                     {
                         nameof(Booking.RoomBeforeDiscount), nameof(Booking.RoomDiscount),
                         nameof(Booking.ExtraGuestFee), nameof(Booking.PetFee), nameof(Booking.CleaningFee),
                         nameof(Booking.Subtotal), nameof(Booking.ServiceFee), nameof(Booking.Tax),
                         nameof(Booking.Promotion), nameof(Booking.Total),
                         nameof(Booking.HostServiceFee), nameof(Booking.HostPayout),
                         nameof(Booking.RefundedAmount), nameof(Booking.GoodwillCredit)
                     })
            {
                e.Property(money).HasPrecision(12, 2);
            }

            e.Property(x => x.PriceLinesJson).HasColumnType("jsonb");
            e.Property(x => x.GuestNote).HasMaxLength(1000);
            e.Property(x => x.CancellationReason).HasMaxLength(300);
            e.HasIndex(x => x.GuestUserId);
            e.HasOne(x => x.Listing).WithMany()
                .HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.GuestUser).WithMany()
                .HasForeignKey(x => x.GuestUserId).OnDelete(DeleteBehavior.SetNull);

            // docs/03 §2 calls double-booking "yêu cầu bắt buộc, không phải tối
            // ưu hoá", so the guarantee lives in the database, not in a check
            // that two concurrent requests could both pass. The migration turns
            // this into a GiST exclusion constraint over listing + date range.
            e.HasIndex(x => new { x.ListingId, x.CheckIn, x.CheckOut })
                .HasDatabaseName("ix_bookings_listing_range");
        });

        b.Entity<BookingEvent>(e =>
        {
            e.ToTable("booking_events");
            e.HasIndex(x => new { x.BookingId, x.CreatedAt });
            e.Property(x => x.Actor).HasMaxLength(40).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(300);
            e.HasOne(x => x.Booking).WithMany(bk => bk.Events)
                .HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ResolutionCase>(e =>
        {
            e.ToTable("resolution_cases");
            e.HasIndex(x => x.Reference).IsUnique();
            e.HasIndex(x => x.Status);
            e.Property(x => x.Reference).HasMaxLength(20).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            e.Property(x => x.EvidenceUrls).HasMaxLength(2000);
            e.Property(x => x.Response).HasMaxLength(4000);
            e.Property(x => x.Decision).HasMaxLength(2000);
            e.Property(x => x.AmountClaimed).HasPrecision(12, 2);
            e.Property(x => x.AmountAwarded).HasPrecision(12, 2);
            e.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.OpenedByUser).WithMany().HasForeignKey(x => x.OpenedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.DecidedByUser).WithMany().HasForeignKey(x => x.DecidedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<ResolutionEvent>(e =>
        {
            e.ToTable("resolution_events");
            e.HasIndex(x => new { x.CaseId, x.CreatedAt });
            e.Property(x => x.Actor).HasMaxLength(40).IsRequired();
            e.Property(x => x.Note).HasMaxLength(1000);
            e.HasOne(x => x.Case).WithMany(c => c.Events).HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AdminAuditEntry>(e =>
        {
            e.ToTable("admin_audit");
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.Target);
            e.Property(x => x.Action).HasMaxLength(60).IsRequired();
            e.Property(x => x.Target).HasMaxLength(60).IsRequired();
            e.Property(x => x.Before).HasMaxLength(2000);
            e.Property(x => x.After).HasMaxLength(2000);
            e.Property(x => x.Note).HasMaxLength(1000);
            e.HasOne(x => x.ActorUser).WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<TaxRule>(e =>
        {
            e.ToTable("tax_rules");
            e.HasIndex(x => new { x.Country, x.City });
            e.Property(x => x.Country).HasMaxLength(100).IsRequired();
            e.Property(x => x.City).HasMaxLength(100);
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Value).HasPrecision(12, 4);
        });

        b.Entity<LedgerEntry>(e =>
        {
            e.ToTable("ledger_entries");
            e.HasIndex(x => x.TransactionId);
            e.HasIndex(x => new { x.Account, x.CreatedAt });
            e.Property(x => x.TransactionKind).HasMaxLength(40).IsRequired();
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Memo).HasMaxLength(200);
            e.Property(x => x.Amount).HasPrecision(14, 2);
            e.Ignore(x => x.Signed);
            e.HasOne(x => x.Booking).WithMany(bk => bk.LedgerEntries)
                .HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.SetNull);
        });
    }

    /// <summary>
    /// docs/00 §6.1 and §6.2: ledger rows are append-only. Anything that tries to
    /// update or delete one is a bug, so it fails loudly rather than silently
    /// rewriting history.
    /// </summary>
    public override int SaveChanges()
    {
        GuardLedger();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        GuardLedger();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void GuardLedger()
    {
        var tampered = ChangeTracker.Entries<LedgerEntry>()
            .Any(e => e.State is EntityState.Modified or EntityState.Deleted);

        if (tampered)
        {
            throw new InvalidOperationException(
                "Sổ ghi tiền là bất biến: chỉ được thêm bút toán mới, không sửa hay xoá bút toán cũ.");
        }

        var rewrittenHistory =
            ChangeTracker.Entries<BookingEvent>().Any(e => e.State is EntityState.Modified or EntityState.Deleted)
            || ChangeTracker.Entries<ResolutionEvent>().Any(e => e.State is EntityState.Modified or EntityState.Deleted)
            || ChangeTracker.Entries<AdminAuditEntry>().Any(e => e.State is EntityState.Modified or EntityState.Deleted);

        if (rewrittenHistory)
        {
            throw new InvalidOperationException(
                "Lịch sử đơn, hồ sơ và nhật ký quản trị chỉ được thêm, không sửa hay xoá (docs/00 §6.2).");
        }
    }
}
