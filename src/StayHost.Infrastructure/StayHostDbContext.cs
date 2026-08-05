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
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<MessageThread> MessageThreads => Set<MessageThread>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<CalendarBlock> CalendarBlocks => Set<CalendarBlock>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<HostProfile>(e =>
        {
            e.ToTable("hosts");
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Initials).HasMaxLength(4);
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
            e.HasOne(x => x.SenderUser).WithMany()
                .HasForeignKey(x => x.SenderUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<CalendarBlock>(e =>
        {
            e.ToTable("calendar_blocks");
            e.Property(x => x.Note).HasMaxLength(200);
            e.HasOne(x => x.Listing).WithMany(l => l.Blocks)
                .HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
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
            e.Property(x => x.ServiceFeeRate).HasPrecision(5, 4);
            e.Property(x => x.WeekendSurchargeRate).HasPrecision(5, 4);
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
            e.HasOne(x => x.Listing).WithMany(l => l.Reviews)
                .HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.AuthorUser).WithMany()
                .HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Booking).WithMany()
                .HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Favorite>(e =>
        {
            e.ToTable("favorites");
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
            e.Property(x => x.Subtotal).HasPrecision(12, 2);
            e.Property(x => x.CleaningFee).HasPrecision(12, 2);
            e.Property(x => x.ServiceFee).HasPrecision(12, 2);
            e.Property(x => x.Tax).HasPrecision(12, 2);
            e.Property(x => x.Total).HasPrecision(12, 2);
            e.Property(x => x.RefundedAmount).HasPrecision(12, 2);
            e.Property(x => x.GuestNote).HasMaxLength(1000);
            e.Property(x => x.CancellationReason).HasMaxLength(300);
            e.HasIndex(x => x.GuestUserId);
            e.HasOne(x => x.Listing).WithMany()
                .HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.GuestUser).WithMany()
                .HasForeignKey(x => x.GuestUserId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
