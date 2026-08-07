using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;
using StayHost.Web.Infrastructure;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

/// <summary>Everything a host needs to run their listings: inventory, calendar, orders, money.</summary>
[ApiController]
[Route("api/host")]
public class HostController(
    StayHostDbContext db, AuthService auth, NotificationService notifications,
    BookingService rules, ReviewService reviews)
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

        var live = bookings.Where(b => BookingLifecycle.BlocksDates.Contains(b.Status)).ToList();
        var past = live.Where(b => b.CheckOut <= today).ToList();
        var upcoming = live.Where(b => b.CheckOut > today).ToList();

        // Subtotal already includes the cleaning fee (docs/03 section 1 step 6),
        // so the fallback is subtotal minus the host service fee, not a sum of the two.
        decimal PayoutOf(Booking b) => b.Payment?.HostPayout ?? b.HostPayout;

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
        if (profile is null) return this.Denied();

        var listing = await db.Listings
            .Include(l => l.Images)
            .Include(l => l.Amenities)
            .FirstOrDefaultAsync(l => l.Id == id, ct);

        if (listing is null) return NotFound();
        if (listing.HostId != profile.Id) return this.Denied();

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
        if (profile is null) return this.Denied();

        var listing = await db.Listings.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (listing is null) return NoContent();
        if (listing.HostId != profile.Id) return this.Denied();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var hasLiveStay = await db.Bookings.AnyAsync(b =>
            b.ListingId == id && BookingLifecycle.BlocksDates.Contains(b.Status) && b.CheckOut > today, ct);

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
        if (profile is null || listing.HostId != profile.Id) return this.Denied();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var blocks = await db.CalendarBlocks
            .Where(b => b.ListingId == id && b.To >= today)
            .OrderBy(b => b.From)
            .Select(b => new CalendarBlockDto(b.Id, b.From, b.To, b.Note))
            .ToListAsync(ct);

        var booked = await db.Bookings
            .Where(b => b.ListingId == id && BookingLifecycle.BlocksDates.Contains(b.Status) && b.CheckOut >= today)
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
        if (profile is null || listing.HostId != profile.Id) return this.Denied();
        if (req.To < req.From) return BadRequest(new { message = "Ngày kết thúc phải sau ngày bắt đầu." });

        var clash = await db.Bookings.AnyAsync(b =>
            b.ListingId == req.ListingId && BookingLifecycle.BlocksDates.Contains(b.Status) &&
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
        if (profile is null || block.Listing!.HostId != profile.Id) return this.Denied();

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
        if (profile is null || listing.HostId != profile.Id) return this.Denied();

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
        if (profile is null || rule.Listing!.HostId != profile.Id) return this.Denied();

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
        if (profile is null || booking.Listing!.HostId != profile.Id) return this.Denied();
        if (booking.GuestUserId is not int guestId)
            return BadRequest(new { message = "Lượt đặt này không gắn với tài khoản khách." });
        // docs/03 §7 — only a completed stay, inside the 14-day window.
        if (booking.Status != BookingStatus.Completed)
            return BadRequest(new { message = "Chỉ đánh giá được sau khi khách trả phòng." });
        if (DateTime.UtcNow > ReviewService.Deadline(booking))
            return BadRequest(new { message = "Đã quá 14 ngày kể từ ngày trả phòng." });
        if (await db.GuestReviews.AnyAsync(r => r.BookingId == id, ct))
            return Conflict(new { message = "Bạn đã đánh giá khách này rồi." });

        var text = (req.Text ?? "").Trim();
        if (text.Length < 10) return BadRequest(new { message = "Nội dung đánh giá cần tối thiểu 10 ký tự." });

        // docs/01 ĐG-09 — the same content rules as a guest's review.
        var guard = ContentGuard.CheckReview(text);
        if (!guard.Ok) return BadRequest(new { message = guard.Message });

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
            $"{user.FullName} vừa để lại đánh giá cho chuyến đi {booking.Reference}. " +
            "Đánh giá của hai bên sẽ hiện khi cả hai đã gửi.",
            $"/trips/{booking.Id}", ct);

        await db.SaveChangesAsync(ct);

        // Blind both ways: publishing only happens once the guest has written
        // one too, or the window closes.
        var published = await reviews.TryPublishAsync(booking.Id, ct);
        if (published) await db.SaveChangesAsync(ct);

        return Ok(new
        {
            published,
            message = published
                ? "Đánh giá của hai bên đã được công khai."
                : "Đã gửi. Đánh giá sẽ hiện khi khách cũng gửi, hoặc sau 14 ngày."
        });
    }

    /* ------------------------------------------------------------ bookings */

    // "action" is a reserved route token in attribute routing — ASP.NET Core
    // replaces it with the method name, so /confirm never matched. The parameter
    // has to be called something else.
    [HttpPost("bookings/{id:int}/{decision}")]
    public async Task<IActionResult> Respond(int id, string decision, [FromBody] RespondBody? body, CancellationToken ct)
    {
        var (user, profile) = await ResolveAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var booking = await db.Bookings
            .Include(b => b.Listing).Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

        if (booking is null) return NotFound();
        if (profile is null || booking.Listing!.HostId != profile.Id) return this.Denied();

        var verb = decision.ToLowerInvariant();
        if (verb is not ("confirm" or "decline"))
            return BadRequest(new { message = "Hành động không hợp lệ." });

        if (booking.Status != BookingStatus.PendingHostApproval)
        {
            return BadRequest(new
            {
                message = $"Đơn đang ở trạng thái \"{BookingLifecycle.Label(booking.Status)}\" nên không trả lời được nữa."
            });
        }

        if (verb == "confirm")
        {
            // docs/03 §2: a request never held the dates, so someone else may have
            // taken them while the host was thinking. Re-run the nine checks.
            var check = await rules.CheckAsync(
                booking.Listing!, booking.CheckIn, booking.CheckOut,
                new PartySize(booking.Adults, booking.Children, booking.Infants, booking.Pets), ct,
                ignoreBookingId: booking.Id);

            if (!check.Ok) return Conflict(new { message = check.Message, reason = check.Reason.ToString() });

            // The guest pays as the host accepts, so the booking passes through
            // "chờ thanh toán" rather than jumping straight to confirmed.
            db.BookingEvents.Add(BookingLifecycle.Transition(
                booking, BookingStatus.PendingPayment, $"host:{user.Id}", "Chủ nhà chấp nhận yêu cầu."));
            db.BookingEvents.Add(BookingLifecycle.Transition(
                booking, BookingStatus.Confirmed, "system", "Thanh toán thành công."));

            if (booking.Payment is not null)
            {
                booking.Payment.Status = PaymentStatus.Captured;
                booking.Payment.CapturedAt = DateTime.UtcNow;
                booking.Payment.PayoutDueOn = booking.CheckIn.AddDays(1);
            }

            // docs/03 §5: a request-to-book is only charged once the host accepts,
            // so this is the moment the money enters the books.
            if (!await db.LedgerEntries.AnyAsync(e => e.BookingId == booking.Id, ct))
                db.LedgerEntries.AddRange(Ledger.CaptureBooking(booking, BookedPrice(booking), DateTime.UtcNow));
        }
        else
        {
            // Declining a request that was never charged simply drops it; there
            // is nothing to reverse and nothing to refund.
            db.BookingEvents.Add(BookingLifecycle.Transition(
                booking, BookingStatus.Declined, $"host:{user.Id}", body?.Reason ?? "Chủ nhà từ chối"));
            booking.CancellationReason = body?.Reason ?? "Chủ nhà từ chối";
            booking.CancelledBy = CancelledBy.Host;
            if (booking.Payment is not null) booking.Payment.Status = PaymentStatus.Refunded;
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

    /// <summary>
    /// docs/01 TĐ-12 and docs/03 §7: the host answers a review publicly, once,
    /// within 30 days of it appearing.
    /// </summary>
    [HttpPost("reviews/{id:int}/reply")]
    public async Task<IActionResult> ReplyToReview(int id, [FromBody] ReplyToReviewRequest req, CancellationToken ct)
    {
        var (user, profile) = await ResolveAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });
        if (profile is null) return this.Denied();

        var review = await db.Reviews.Include(r => r.Listing)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (review is null) return NotFound();
        if (review.Listing!.HostId != profile.Id) return this.Denied();

        if (review.HostReply is not null)
            return Conflict(new { message = "Bạn chỉ được trả lời một lần cho mỗi đánh giá." });

        if (review.CreatedAt.AddDays(30) < DateTime.UtcNow)
            return BadRequest(new { message = "Đã quá 30 ngày kể từ khi đánh giá được công khai." });

        var text = (req.Text ?? "").Trim();
        if (text.Length < 10) return BadRequest(new { message = "Phản hồi cần tối thiểu 10 ký tự." });

        review.HostReply = text;
        review.HostRepliedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

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

        // docs/01 CN-07 — five photos is the bar for going public. A draft may
        // have fewer, so the check only applies when publishing.
        if (r.IsPublished && r.Images.Count(u => !string.IsNullOrWhiteSpace(u)) < 5)
            return "Cần tối thiểu 5 ảnh trước khi hiển thị công khai.";

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
        // Title and city just changed, so the diacritic-free search column has to follow.
        listing.RefreshSearchText();

        // docs/01 CN-01 — where the wizard got to, so an interrupted host can resume.
        listing.WizardStep = Math.Clamp(r.WizardStep, 0, 12);
        listing.IsComplete = r.IsComplete;

        // docs/01 CN-05 — beds per room.
        if (r.BedLayout is { Count: > 0 })
            listing.BedLayoutJson = System.Text.Json.JsonSerializer.Serialize(r.BedLayout, LayoutJson);

        // docs/01 CN-12 — the declarations, stored as given.
        if (r.Legal is { } legal)
        {
            listing.LicenseNumber = string.IsNullOrWhiteSpace(legal.LicenseNumber) ? null : legal.LicenseNumber.Trim();
            listing.HasSecurityCameras = legal.HasSecurityCameras;
            listing.SecurityCameraNote = string.IsNullOrWhiteSpace(legal.SecurityCameraNote)
                ? null
                : legal.SecurityCameraNote.Trim();
            listing.HasWeaponsOnProperty = legal.HasWeaponsOnProperty;
            listing.HasDangerousAnimals = legal.HasDangerousAnimals;
        }

        if (r.Pricing is { } p)
        {
            // Percentages are clamped here as well as in the UI: the platform cap of
            // docs/03 §1 is meaningless if a host can store 500% in the first place.
            listing.WeeklyDiscountPercent = Math.Clamp(p.WeeklyDiscountPercent, 0, 60);
            listing.MonthlyDiscountPercent = Math.Clamp(p.MonthlyDiscountPercent, 0, 60);
            listing.EarlyBirdDays = Math.Clamp(p.EarlyBirdDays, 0, 365);
            listing.EarlyBirdPercent = Math.Clamp(p.EarlyBirdPercent, 0, 60);
            listing.LastMinuteDays = Math.Clamp(p.LastMinuteDays, 0, 60);
            listing.LastMinutePercent = Math.Clamp(p.LastMinutePercent, 0, 60);
            listing.WeekendSurchargeRate = Math.Clamp(p.WeekendSurchargeRate, 0m, 2m);
            listing.FreeGuestThreshold = Math.Clamp(p.FreeGuestThreshold, 1, Math.Max(1, r.MaxGuests));
            listing.ExtraGuestFee = Math.Max(0m, p.ExtraGuestFee);
            listing.PetsAllowed = p.PetsAllowed;
            listing.MaxPets = Math.Clamp(p.MaxPets, 0, 10);
            listing.PetFee = Math.Max(0m, p.PetFee);
            listing.PetFeePerNight = p.PetFeePerNight;
        }

        // docs/01 CĐ-03 — the arrival guide. Left untouched when the client did
        // not send one, so a save from an older screen cannot wipe it.
        if (r.CheckIn is { } guide)
        {
            listing.CheckInFrom = CheckInGuide.ParseTime(guide.CheckInFrom, listing.CheckInFrom);
            listing.CheckInTo = CheckInGuide.ParseTime(guide.CheckInTo, listing.CheckInTo);
            listing.CheckOutBefore = CheckInGuide.ParseTime(guide.CheckOutBefore, listing.CheckOutBefore);
            listing.CheckInMethod = Enum.TryParse<CheckInMethod>(guide.Method, true, out var method)
                ? method
                : CheckInMethod.Host;
            listing.AddressLine = Profiles.Tidy(guide.AddressLine, CheckInGuide.LineMax * 2);
            listing.WifiName = Profiles.Tidy(guide.WifiName, CheckInGuide.LineMax);
            listing.WifiPassword = Profiles.Tidy(guide.WifiPassword, CheckInGuide.LineMax);
            listing.DoorCode = Profiles.Tidy(guide.DoorCode, 40);
            listing.HostPhone = Profiles.Tidy(guide.HostPhone, 30);
            listing.Directions = Profiles.TidyLines(guide.Directions, CheckInGuide.NoteMax);
            listing.ApplianceNotes = Profiles.TidyLines(guide.ApplianceNotes, CheckInGuide.NoteMax);
        }

        // docs/03 §8 — the badge belongs to the host; the copy on the listing is
        // what the search filter reads. A newly published place used to carry no
        // badge at all until the next quarterly review, so a Superhost's newest
        // listing was the one missing from the "Siêu chủ nhà" filter.
        listing.IsSuperhost = await db.Hosts
            .Where(h => h.Id == listing.HostId).Select(h => h.IsSuperhost).FirstOrDefaultAsync(ct);

        var coords = CityCoordinates(listing.City);
        listing.Latitude = r.Latitude ?? (listing.Latitude != 0 ? listing.Latitude : coords.Lat);
        listing.Longitude = r.Longitude ?? (listing.Longitude != 0 ? listing.Longitude : coords.Lng);

        // docs/01 CN-07 — the order the host dragged them into, and their labels.
        listing.Images.Clear();
        var urls = r.Images.Where(u => !string.IsNullOrWhiteSpace(u)).Take(20).ToList();
        for (var i = 0; i < urls.Count; i++)
        {
            var caption = r.ImageCaptions is { } captions && i < captions.Count && !string.IsNullOrWhiteSpace(captions[i])
                ? captions[i].Trim()
                : i == 0 ? "Ảnh bìa" : $"Ảnh {i + 1}";

            listing.Images.Add(new ListingImage { Url = urls[i].Trim(), SortOrder = i, Caption = caption });
        }

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
        upcoming, earnings,
        new PricingRulesDto(
            l.WeeklyDiscountPercent, l.MonthlyDiscountPercent,
            l.EarlyBirdDays, l.EarlyBirdPercent,
            l.LastMinuteDays, l.LastMinutePercent,
            l.WeekendSurchargeRate,
            l.FreeGuestThreshold, l.ExtraGuestFee,
            l.PetsAllowed, l.MaxPets, l.PetFee, l.PetFeePerNight),
        ReadLayout(l),
        l.Images.OrderBy(i => i.SortOrder).Select(i => i.Caption).ToList(),
        new LegalDeclarationDto(
            l.LicenseNumber, l.HasSecurityCameras, l.SecurityCameraNote,
            l.HasWeaponsOnProperty, l.HasDangerousAnimals),
        l.WizardStep,
        l.IsComplete,
        // docs/01 CĐ-03 — the host sees their own guide in full, door code included.
        new CheckInSetupDto(
            l.CheckInFrom.ToString("HH\\:mm"),
            l.CheckInTo.ToString("HH\\:mm"),
            l.CheckOutBefore.ToString("HH\\:mm"),
            l.CheckInMethod.ToString(),
            l.AddressLine, l.Directions, l.WifiName, l.WifiPassword,
            l.ApplianceNotes, l.DoorCode, l.HostPhone));

    private static readonly System.Text.Json.JsonSerializerOptions LayoutJson =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    /// <summary>The stored layout, or an empty list when the host has not set one.</summary>
    private static List<BedroomDto> ReadLayout(Listing l)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<BedroomDto>>(l.BedLayoutJson, LayoutJson) ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Replays the amounts frozen on the booking as a breakdown, so the ledger
    /// posts the numbers the guest actually agreed to rather than re-pricing.
    /// </summary>
    private static Pricing.Breakdown BookedPrice(Booking b) => new()
    {
        Nights = b.Nights,
        NightlyRate = b.Nights > 0 ? b.RoomBeforeDiscount / b.Nights : b.RoomBeforeDiscount,
        RoomBeforeDiscount = b.RoomBeforeDiscount,
        RoomDiscount = b.RoomDiscount,
        DiscountPercent = b.DiscountPercent,
        DiscountParts = [],
        ExtraGuestFee = b.ExtraGuestFee,
        PetFee = b.PetFee,
        CleaningFee = b.CleaningFee,
        Subtotal = b.Subtotal,
        GuestServiceFee = b.ServiceFee,
        Tax = b.Tax,
        TaxLines = [],
        Promotion = b.Promotion,
        Total = b.Total,
        HostServiceFee = b.HostServiceFee,
        HostPayout = b.HostPayout,
        Lines = [],
        Nightly = []
    };

    private static HostBookingDto ToHostBooking(Booking b) => new(
        b.Id, b.Reference, b.ListingId, b.Listing?.Title ?? "",
        b.GuestUser?.FullName ?? b.GuestName ?? "Khách",
        b.GuestEmail ?? b.GuestUser?.Email,
        b.GuestNote,
        b.CheckIn, b.CheckOut, b.Nights, b.Guests, b.Total,
        b.Payment?.HostPayout ?? b.HostPayout,
        b.Status.ToString(),
        BookingLifecycle.Label(b.Status),
        BookingLifecycle.BadgeClass(b.Status),
        b.Payment?.Status.ToString() ?? "Pending",
        b.RequestExpiresAt,
        b.CreatedAt);
}
