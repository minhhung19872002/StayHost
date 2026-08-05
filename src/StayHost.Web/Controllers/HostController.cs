using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

/// <summary>Everything a host needs to run their listings: inventory, calendar, orders, money.</summary>
[ApiController]
[Route("api/host")]
public class HostController(StayHostDbContext db, AuthService auth, NotificationService notifications)
    : ControllerBase
{
    private async Task<(User? User, HostProfile? Profile)> ResolveAsync(CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return (null, null);
        var profile = await db.Hosts.FirstOrDefaultAsync(h => h.UserId == user.Id, ct);
        return (user, profile);
    }

    /* ---------------------------------------------------------- dashboard */

    [HttpGet("dashboard")]
    public async Task<ActionResult<HostDashboardDto>> Dashboard(CancellationToken ct)
    {
        var (user, profile) = await ResolveAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        if (profile is null)
            return Ok(new HostDashboardDto(0, 0, 0, 0, 0, 0, 0, 0, [], [], []));

        var listings = await db.Listings
            .Where(l => l.HostId == profile.Id)
            .Include(l => l.Images)
            .Include(l => l.Amenities).ThenInclude(la => la.Amenity)
            .AsSplitQuery()
            .ToListAsync(ct);

        var listingIds = listings.Select(l => l.Id).ToList();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var bookings = await db.Bookings
            .Where(b => listingIds.Contains(b.ListingId))
            .Include(b => b.Listing)
            .Include(b => b.Payment)
            .Include(b => b.GuestUser)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);

        var live = bookings.Where(b => b.Status != BookingStatus.Cancelled).ToList();
        var past = live.Where(b => b.CheckOut <= today).ToList();
        var upcoming = live.Where(b => b.CheckOut > today).ToList();

        decimal PayoutOf(Booking b) => b.Payment?.HostPayout ?? b.Subtotal + b.CleaningFee;

        var byMonth = live
            .GroupBy(b => new { b.CheckIn.Year, b.CheckIn.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new MonthlyEarningDto(
                $"{g.Key.Month:00}/{g.Key.Year}",
                g.Sum(PayoutOf),
                g.Sum(b => b.Nights)))
            .ToList();

        var reviewed = listings.Where(l => l.ReviewCount > 0).ToList();

        var unread = await db.Messages.CountAsync(m =>
            m.SenderUserId != user.Id && m.ReadAt == null && m.Thread!.HostUserId == user.Id, ct);

        var dtoListings = listings
            .Select(l => ToHostListing(l,
                upcoming.Count(b => b.ListingId == l.Id),
                past.Where(b => b.ListingId == l.Id).Sum(PayoutOf)))
            .ToList();

        return Ok(new HostDashboardDto(
            listings.Count,
            listings.Count(l => l.IsPublished),
            upcoming.Count,
            past.Sum(PayoutOf),
            upcoming.Sum(PayoutOf),
            reviewed.Count == 0 ? 0 : Math.Round(reviewed.Average(l => l.Rating), 2),
            listings.Sum(l => l.ReviewCount),
            unread,
            dtoListings,
            bookings.Select(ToHostBooking).ToList(),
            byMonth));
    }

    /* ------------------------------------------------------------ listings */

    [HttpPost("listings")]
    public async Task<ActionResult<HostListingDto>> Create([FromBody] SaveListingRequest req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var error = Validate(req);
        if (error is not null) return BadRequest(new { message = error });

        var profile = await auth.EnsureHostProfileAsync(user, ct);

        var listing = new Listing { HostId = profile.Id, Slug = await UniqueSlugAsync(req.Title, ct) };
        await ApplyAsync(listing, req, ct);

        db.Listings.Add(listing);
        await db.SaveChangesAsync(ct);

        return Created($"/api/host/listings/{listing.Id}", ToHostListing(listing, 0, 0));
    }

    [HttpPut("listings/{id:int}")]
    public async Task<ActionResult<HostListingDto>> Update(int id, [FromBody] SaveListingRequest req, CancellationToken ct)
    {
        var (user, profile) = await ResolveAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });
        if (profile is null) return Forbid();

        var listing = await db.Listings
            .Include(l => l.Images)
            .Include(l => l.Amenities)
            .FirstOrDefaultAsync(l => l.Id == id, ct);

        if (listing is null) return NotFound();
        if (listing.HostId != profile.Id) return Forbid();

        var error = Validate(req);
        if (error is not null) return BadRequest(new { message = error });

        await ApplyAsync(listing, req, ct);
        await db.SaveChangesAsync(ct);

        return Ok(ToHostListing(listing, 0, 0));
    }

    [HttpDelete("listings/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var (user, profile) = await ResolveAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });
        if (profile is null) return Forbid();

        var listing = await db.Listings.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (listing is null) return NoContent();
        if (listing.HostId != profile.Id) return Forbid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var hasLiveStay = await db.Bookings.AnyAsync(b =>
            b.ListingId == id && b.Status != BookingStatus.Cancelled && b.CheckOut > today, ct);

        if (hasLiveStay)
            return Conflict(new { message = "Chỗ nghỉ còn lượt đặt sắp tới. Hãy gỡ đăng thay vì xoá." });

        db.Listings.Remove(listing);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /* ------------------------------------------------------------ calendar */

    [HttpGet("listings/{id:int}/calendar")]
    public async Task<ActionResult<object>> Calendar(int id, CancellationToken ct)
    {
        var (user, profile) = await ResolveAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var listing = await db.Listings.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (listing is null) return NotFound();
        if (profile is null || listing.HostId != profile.Id) return Forbid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var blocks = await db.CalendarBlocks
            .Where(b => b.ListingId == id && b.To >= today)
            .OrderBy(b => b.From)
            .Select(b => new CalendarBlockDto(b.Id, b.From, b.To, b.Note))
            .ToListAsync(ct);

        var booked = await db.Bookings
            .Where(b => b.ListingId == id && b.Status != BookingStatus.Cancelled && b.CheckOut >= today)
            .Select(b => new { b.Reference, b.CheckIn, b.CheckOut, b.Guests })
            .ToListAsync(ct);

        var rules = await db.PriceRules
            .Where(r => r.ListingId == id && r.To >= today)
            .OrderBy(r => r.From)
            .Select(r => new PriceRuleDto(r.Id, r.Name, r.From, r.To, r.NightlyRate))
            .ToListAsync(ct);

        return Ok(new { blocks, bookings = booked, priceRules = rules, basePrice = listing.PricePerNight });
    }

    [HttpPost("blocks")]
    public async Task<ActionResult<CalendarBlockDto>> AddBlock([FromBody] CreateBlockRequest req, CancellationToken ct)
    {
        var (user, profile) = await ResolveAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var listing = await db.Listings.FirstOrDefaultAsync(l => l.Id == req.ListingId, ct);
        if (listing is null) return NotFound();
        if (profile is null || listing.HostId != profile.Id) return Forbid();
        if (req.To < req.From) return BadRequest(new { message = "Ngày kết thúc phải sau ngày bắt đầu." });

        var clash = await db.Bookings.AnyAsync(b =>
            b.ListingId == req.ListingId && b.Status != BookingStatus.Cancelled &&
            b.CheckIn <= req.To && req.From < b.CheckOut, ct);
        if (clash) return Conflict(new { message = "Khoảng ngày này đã có khách đặt." });

        var block = new CalendarBlock { ListingId = req.ListingId, From = req.From, To = req.To, Note = req.Note };
        db.CalendarBlocks.Add(block);
        await db.SaveChangesAsync(ct);

        return Ok(new CalendarBlockDto(block.Id, block.From, block.To, block.Note));
    }

    [HttpDelete("blocks/{id:int}")]
    public async Task<IActionResult> RemoveBlock(int id, CancellationToken ct)
    {
        var (user, profile) = await ResolveAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var block = await db.CalendarBlocks.Include(b => b.Listing).FirstOrDefaultAsync(b => b.Id == id, ct);
        if (block is null) return NoContent();
        if (profile is null || block.Listing!.HostId != profile.Id) return Forbid();

        db.CalendarBlocks.Remove(block);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /* --------------------------------------------------------- price rules */

    [HttpPost("price-rules")]
    public async Task<ActionResult<PriceRuleDto>> AddPriceRule([FromBody] CreatePriceRuleRequest req, CancellationToken ct)
    {
        var (user, profile) = await ResolveAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var listing = await db.Listings.FirstOrDefaultAsync(l => l.Id == req.ListingId, ct);
        if (listing is null) return NotFound();
        if (profile is null || listing.HostId != profile.Id) return Forbid();

        if (req.To < req.From) return BadRequest(new { message = "Ngày kết thúc phải sau ngày bắt đầu." });
        if (req.NightlyRate < 50_000) return BadRequest(new { message = "Giá mỗi đêm tối thiểu 50.000₫." });

        var overlaps = await db.PriceRules.AnyAsync(r =>
            r.ListingId == req.ListingId && r.From <= req.To && req.From <= r.To, ct);
        if (overlaps) return Conflict(new { message = "Khoảng ngày này đã có quy tắc giá khác." });

        var rule = new PriceRule
        {
            ListingId = req.ListingId,
            Name = (req.Name ?? "Mùa cao điểm").Trim(),
            From = req.From,
            To = req.To,
            NightlyRate = req.NightlyRate
        };
        db.PriceRules.Add(rule);
        await db.SaveChangesAsync(ct);

        return Ok(new PriceRuleDto(rule.Id, rule.Name, rule.From, rule.To, rule.NightlyRate));
    }

    [HttpDelete("price-rules/{id:int}")]
    public async Task<IActionResult> RemovePriceRule(int id, CancellationToken ct)
    {
        var (user, profile) = await ResolveAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var rule = await db.PriceRules.Include(r => r.Listing).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null) return NoContent();
        if (profile is null || rule.Listing!.HostId != profile.Id) return Forbid();

        db.PriceRules.Remove(rule);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /* ------------------------------------------------------- guest reviews */

    [HttpPost("bookings/{id:int}/review-guest")]
    public async Task<IActionResult> ReviewGuest(int id, [FromBody] ReviewGuestRequest req, CancellationToken ct)
    {
        var (user, profile) = await ResolveAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var booking = await db.Bookings.Include(b => b.Listing).FirstOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null) return NotFound();
        if (profile is null || booking.Listing!.HostId != profile.Id) return Forbid();
        if (booking.GuestUserId is not int guestId)
            return BadRequest(new { message = "Lượt đặt này không gắn với tài khoản khách." });
        if (booking.CheckOut > DateOnly.FromDateTime(DateTime.UtcNow))
            return BadRequest(new { message = "Chỉ đánh giá được sau khi khách trả phòng." });
        if (await db.GuestReviews.AnyAsync(r => r.BookingId == id, ct))
            return Conflict(new { message = "Bạn đã đánh giá khách này rồi." });

        var text = (req.Text ?? "").Trim();
        if (text.Length < 10) return BadRequest(new { message = "Nội dung đánh giá cần tối thiểu 10 ký tự." });

        db.GuestReviews.Add(new GuestReview
        {
            BookingId = id,
            HostUserId = user.Id,
            GuestUserId = guestId,
            Rating = Math.Clamp(req.Rating, 1, 5),
            Text = text,
            WouldHostAgain = req.WouldHostAgain
        });

        var guest = await db.Users.FirstOrDefaultAsync(u => u.Id == guestId, ct);
        await notifications.QueueWithEmailAsync(guest, NotificationKind.ReviewReceived,
            "Chủ nhà đã đánh giá bạn",
            $"{user.FullName} vừa để lại đánh giá cho chuyến đi {booking.Reference}.",
            $"/trips/{booking.Id}", ct);

        await db.SaveChangesAsync(ct);
        await RecalculateSuperhostAsync(profile.Id, ct);
        return NoContent();
    }

    /// <summary>
    /// Superhost is earned, not set by hand: 4.8+ average, at least five completed
    /// stays, and no host-side cancellations.
    /// </summary>
    private async Task RecalculateSuperhostAsync(int hostId, CancellationToken ct)
    {
        var host = await db.Hosts.FirstOrDefaultAsync(h => h.Id == hostId, ct);
        if (host is null) return;

        var listings = await db.Listings.Where(l => l.HostId == hostId)
            .Select(l => new { l.Id, l.Rating, l.ReviewCount }).ToListAsync(ct);
        var listingIds = listings.Select(l => l.Id).ToList();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var completed = await db.Bookings.CountAsync(b =>
            listingIds.Contains(b.ListingId) && b.Status != BookingStatus.Cancelled && b.CheckOut < today, ct);

        var hostCancellations = await db.Bookings.CountAsync(b =>
            listingIds.Contains(b.ListingId) && b.Status == BookingStatus.Cancelled &&
            b.CancellationReason != null && b.CancellationReason.Contains("Chủ nhà"), ct);

        var rated = listings.Where(l => l.ReviewCount > 0).ToList();
        var average = rated.Count == 0 ? 0 : rated.Average(l => l.Rating);

        host.IsSuperhost = average >= 4.8 && completed >= 5 && hostCancellations == 0;

        foreach (var id in listingIds)
        {
            var listing = await db.Listings.FirstAsync(l => l.Id == id, ct);
            listing.IsSuperhost = host.IsSuperhost;
        }

        await db.SaveChangesAsync(ct);
    }

    /* ------------------------------------------------------------ bookings */

    [HttpPost("bookings/{id:int}/{action}")]
    public async Task<IActionResult> Respond(int id, string action, [FromBody] RespondBody? body, CancellationToken ct)
    {
        var (user, profile) = await ResolveAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var booking = await db.Bookings
            .Include(b => b.Listing).Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

        if (booking is null) return NotFound();
        if (profile is null || booking.Listing!.HostId != profile.Id) return Forbid();

        switch (action.ToLowerInvariant())
        {
            case "confirm":
                booking.Status = BookingStatus.Confirmed;
                if (booking.Payment is not null)
                {
                    booking.Payment.Status = PaymentStatus.Captured;
                    booking.Payment.CapturedAt = DateTime.UtcNow;
                    booking.Payment.PayoutDueOn = booking.CheckIn.AddDays(1);
                }
                break;

            case "decline":
                booking.Status = BookingStatus.Cancelled;
                booking.CancellationReason = body?.Reason ?? "Chủ nhà từ chối";
                if (booking.Payment is not null) booking.Payment.Status = PaymentStatus.Refunded;
                break;

            default:
                return BadRequest(new { message = "Hành động không hợp lệ." });
        }

        booking.RespondedAt = DateTime.UtcNow;

        var guest = booking.GuestUserId is int guestId
            ? await db.Users.FirstOrDefaultAsync(u => u.Id == guestId, ct)
            : null;

        var confirmed = booking.Status == BookingStatus.Confirmed;
        await notifications.QueueWithEmailAsync(guest,
            confirmed ? NotificationKind.BookingConfirmed : NotificationKind.BookingDeclined,
            confirmed ? "Chủ nhà đã xác nhận chuyến đi" : "Chủ nhà đã từ chối yêu cầu",
            confirmed
                ? $"Mã {booking.Reference} · {booking.Listing!.Title}. Hẹn gặp bạn ngày {booking.CheckIn:dd/MM}."
                : $"Mã {booking.Reference} đã bị từ chối. Toàn bộ số tiền sẽ được hoàn lại.",
            $"/trips/{booking.Id}", ct);

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    public record RespondBody(string? Reason);

    /* ------------------------------------------------------------- helpers */

    private static string? Validate(SaveListingRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Title) || r.Title.Trim().Length < 8)
            return "Tiêu đề cần tối thiểu 8 ký tự.";
        if (string.IsNullOrWhiteSpace(r.City)) return "Vui lòng nhập thành phố.";
        if (string.IsNullOrWhiteSpace(r.Description) || r.Description.Trim().Length < 40)
            return "Mô tả cần tối thiểu 40 ký tự.";
        if (r.PricePerNight < 50_000) return "Giá mỗi đêm tối thiểu 50.000₫.";
        if (r.MaxGuests is < 1 or > 30) return "Số khách tối đa phải từ 1 đến 30.";
        if (r.Bedrooms < 0 || r.Beds < 1) return "Cần ít nhất 1 giường.";
        if (r.Images.Count == 0) return "Cần ít nhất 1 ảnh.";
        if (r.MinNights is < 1 or > 90) return "Số đêm tối thiểu phải từ 1 đến 90.";
        return null;
    }

    private async Task ApplyAsync(Listing listing, SaveListingRequest r, CancellationToken ct)
    {
        listing.Title = r.Title.Trim();
        listing.City = r.City.Trim();
        var category = CatalogService.Categories.FirstOrDefault(c => c.Key == r.TypeKey && c.Key != "all");
        listing.Type = category.Key is null ? PlaceType.House : category.Type;
        listing.RoomType = r.RoomTypeKey switch
        {
            "private" => RoomType.PrivateRoom,
            "shared" => RoomType.SharedRoom,
            _ => RoomType.EntirePlace
        };
        listing.Bedrooms = r.Bedrooms;
        listing.Beds = r.Beds;
        listing.Bathrooms = r.Bathrooms;
        listing.MaxGuests = r.MaxGuests;
        listing.PricePerNight = r.PricePerNight;
        listing.CleaningFee = r.CleaningFee;
        listing.MinNights = r.MinNights;
        listing.InstantBook = r.InstantBook;
        listing.IsPublished = r.IsPublished;
        listing.CancellationTier = Enum.TryParse<CancellationTier>(r.CancellationTier, true, out var tier)
            ? tier
            : CancellationTier.Moderate;
        listing.Description = r.Description.Trim();
        listing.SpaceHighlight = string.IsNullOrWhiteSpace(r.Highlight) ? null : r.Highlight.Trim();
        listing.UpdatedAt = DateTime.UtcNow;

        var coords = CityCoordinates(listing.City);
        listing.Latitude = r.Latitude ?? (listing.Latitude != 0 ? listing.Latitude : coords.Lat);
        listing.Longitude = r.Longitude ?? (listing.Longitude != 0 ? listing.Longitude : coords.Lng);

        listing.Images.Clear();
        var order = 0;
        foreach (var url in r.Images.Where(u => !string.IsNullOrWhiteSpace(u)).Take(20))
            listing.Images.Add(new ListingImage { Url = url.Trim(), SortOrder = order++, Caption = $"Ảnh {order}" });

        var wanted = await db.Amenities
            .Where(a => r.AmenityKeys.Contains(a.Key))
            .Select(a => a.Id)
            .ToListAsync(ct);

        listing.Amenities.Clear();
        foreach (var amenityId in wanted)
            listing.Amenities.Add(new ListingAmenity { AmenityId = amenityId });
    }

    private async Task<string> UniqueSlugAsync(string title, CancellationToken ct)
    {
        var baseSlug = Slugify(title);
        var slug = baseSlug;
        var n = 1;
        while (await db.Listings.AnyAsync(l => l.Slug == slug, ct))
            slug = $"{baseSlug}-{++n}";
        return slug;
    }

    private static string Slugify(string title)
    {
        var normalized = title.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c == 'đ' || c == 'Đ' ? 'd' : c));
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        var slug = sb.ToString().Trim('-');
        return string.IsNullOrEmpty(slug) ? "cho-nghi" : slug;
    }

    /// <summary>Rough centroid so a new listing lands on the map before the host drags a pin.</summary>
    private static (double Lat, double Lng) CityCoordinates(string city) => city switch
    {
        var c when c.Contains("Đà Nẵng") => (16.0544, 108.2022),
        var c when c.Contains("Đà Lạt") => (11.9404, 108.4583),
        var c when c.Contains("Nha Trang") => (12.2388, 109.1967),
        var c when c.Contains("Hội An") => (15.8801, 108.3380),
        var c when c.Contains("Phú Quốc") => (10.2270, 103.9670),
        var c when c.Contains("Hà Nội") => (21.0278, 105.8342),
        var c when c.Contains("Hồ Chí Minh") => (10.7769, 106.7009),
        var c when c.Contains("Huế") => (16.4637, 107.5909),
        var c when c.Contains("Vũng Tàu") => (10.3460, 107.0843),
        var c when c.Contains("Sa Pa") => (22.3364, 103.8438),
        _ => (16.0, 107.5)
    };

    private static HostListingDto ToHostListing(Listing l, int upcoming, decimal earnings) => new(
        l.Id, l.Slug, l.Title, l.City,
        CatalogService.CategoryKey(l.Type),
        l.RoomType switch
        {
            RoomType.PrivateRoom => "private",
            RoomType.SharedRoom => "shared",
            _ => "entire"
        },
        l.Bedrooms, l.Beds, l.Bathrooms, l.MaxGuests,
        l.PricePerNight, l.CleaningFee, l.MinNights, l.InstantBook, l.IsPublished,
        l.CancellationTier.ToString(),
        Math.Round(l.Rating, 2), l.ReviewCount,
        l.Description, l.SpaceHighlight, l.Latitude, l.Longitude,
        l.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).ToList(),
        l.Amenities.Where(a => a.Amenity is not null).Select(a => a.Amenity!.Key).ToList(),
        upcoming, earnings);

    private static HostBookingDto ToHostBooking(Booking b) => new(
        b.Id, b.Reference, b.ListingId, b.Listing?.Title ?? "",
        b.GuestUser?.FullName ?? b.GuestName ?? "Khách",
        b.GuestEmail ?? b.GuestUser?.Email,
        b.GuestNote,
        b.CheckIn, b.CheckOut, b.Nights, b.Guests, b.Total,
        b.Payment?.HostPayout ?? b.Subtotal + b.CleaningFee,
        b.Status.ToString(),
        b.Payment?.Status.ToString() ?? "Pending",
        b.CreatedAt);
}
