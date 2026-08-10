using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;
using StayHost.Web.Infrastructure;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

/// <summary>
/// docs/01 CĐ-10, CĐ-11 — merging bookings into one trip with a shared day-by-day
/// itinerary that the owner and invited companions build together.
/// </summary>
[ApiController]
[Route("api/trip-plans")]
public class TripPlansController(StayHostDbContext db, AuthService auth) : ControllerBase
{
    /// <summary>docs/01 CĐ-10 — trips I own or was invited to.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TripPlanSummaryDto>>> List(CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var plans = await db.TripPlans
            .Where(t => t.OwnerId == user.Id || t.Members.Any(m => m.UserId == user.Id))
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TripPlanSummaryDto(
                t.Id, t.Name, t.OwnerId == user.Id, t.Bookings.Count, t.Members.Count, t.CreatedAt))
            .ToListAsync(ct);
        return Ok(plans);
    }

    /// <summary>docs/01 CĐ-10 — start a trip.</summary>
    [HttpPost]
    public async Task<ActionResult<TripPlanSummaryDto>> Create([FromBody] CreateTripPlanRequest req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var name = (req.Name ?? "").Trim();
        if (name.Length is < 2 or > StayHost.Domain.TripPlans.NameMax)
            return BadRequest(new { message = "Tên chuyến cần từ 2 ký tự." });

        var plan = new TripPlan { OwnerId = user.Id, Name = name };
        db.TripPlans.Add(plan);
        await db.SaveChangesAsync(ct);
        return Ok(new TripPlanSummaryDto(plan.Id, plan.Name, true, 0, 0, plan.CreatedAt));
    }

    /// <summary>docs/01 CĐ-10/CĐ-11 — the full trip: bookings, companions, itinerary by day.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TripPlanDetailDto>> Detail(int id, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var plan = await Load(id, ct);
        if (plan is null) return NotFound();

        var memberIds = plan.Members.Select(m => m.UserId).ToList();
        if (!StayHost.Domain.TripPlans.CanEdit(plan.OwnerId, memberIds, user.Id)) return this.Denied();

        return Ok(ToDetail(plan, user.Id));
    }

    /// <summary>docs/01 CĐ-10 — merge one of my bookings into the trip.</summary>
    [HttpPost("{id:int}/bookings")]
    public async Task<IActionResult> AddBooking(int id, [FromBody] AddTripBookingRequest req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var plan = await db.TripPlans.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (plan is null) return NotFound();
        if (!StayHost.Domain.TripPlans.IsOwner(plan.OwnerId, user.Id)) return this.Denied();

        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == req.BookingId, ct);
        if (booking is null || booking.GuestUserId != user.Id)
            return BadRequest(new { message = "Chỉ thêm được đơn của chính bạn." });

        if (!await db.TripPlanBookings.AnyAsync(x => x.TripPlanId == id && x.BookingId == req.BookingId, ct))
        {
            db.TripPlanBookings.Add(new TripPlanBooking { TripPlanId = id, BookingId = req.BookingId });
            await db.SaveChangesAsync(ct);
        }
        return NoContent();
    }

    [HttpDelete("{id:int}/bookings/{bookingId:int}")]
    public async Task<IActionResult> RemoveBooking(int id, int bookingId, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var plan = await db.TripPlans.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (plan is null) return NotFound();
        if (!StayHost.Domain.TripPlans.IsOwner(plan.OwnerId, user.Id)) return this.Denied();

        var row = await db.TripPlanBookings.FirstOrDefaultAsync(x => x.TripPlanId == id && x.BookingId == bookingId, ct);
        if (row is not null) { db.TripPlanBookings.Remove(row); await db.SaveChangesAsync(ct); }
        return NoContent();
    }

    /// <summary>docs/01 CĐ-11 — invite a friend to co-edit the itinerary.</summary>
    [HttpPost("{id:int}/members")]
    public async Task<IActionResult> AddMember(int id, [FromBody] AddTripMemberRequest req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var plan = await db.TripPlans.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (plan is null) return NotFound();
        if (!StayHost.Domain.TripPlans.IsOwner(plan.OwnerId, user.Id)) return this.Denied();
        if (req.UserId == user.Id) return BadRequest(new { message = "Bạn đã là chủ chuyến." });

        // docs/01 CĐ-11 — only a friend can be invited along.
        var friends = await db.Friendships.AnyAsync(
            f => f.Status == FriendshipStatus.Accepted
                 && ((f.RequesterId == user.Id && f.AddresseeId == req.UserId)
                     || (f.RequesterId == req.UserId && f.AddresseeId == user.Id)), ct);
        if (!friends) return BadRequest(new { message = "Chỉ mời được bạn bè vào chuyến." });

        if (!await db.TripPlanMembers.AnyAsync(m => m.TripPlanId == id && m.UserId == req.UserId, ct))
        {
            db.TripPlanMembers.Add(new TripPlanMember { TripPlanId = id, UserId = req.UserId });
            await db.SaveChangesAsync(ct);
        }
        return NoContent();
    }

    /// <summary>docs/01 CĐ-11 — the owner or any companion adds a place to a day.</summary>
    [HttpPost("{id:int}/items")]
    public async Task<IActionResult> AddItem(int id, [FromBody] AddItineraryItemRequest req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var plan = await db.TripPlans.Include(t => t.Members).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (plan is null) return NotFound();
        if (!StayHost.Domain.TripPlans.CanEdit(plan.OwnerId, plan.Members.Select(m => m.UserId), user.Id))
            return this.Denied();

        if (StayHost.Domain.TripPlans.ValidateItem(req.Title) is { } invalid)
            return BadRequest(new { message = invalid });

        var order = await db.TripItineraryItems.CountAsync(x => x.TripPlanId == id && x.Day == req.Day, ct);
        db.TripItineraryItems.Add(new TripItineraryItem
        {
            TripPlanId = id, Day = req.Day, Title = req.Title!.Trim(),
            Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim(),
            AddedByUserId = user.Id, SortOrder = order
        });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}/items/{itemId:int}")]
    public async Task<IActionResult> RemoveItem(int id, int itemId, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var plan = await db.TripPlans.Include(t => t.Members).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (plan is null) return NotFound();
        if (!StayHost.Domain.TripPlans.CanEdit(plan.OwnerId, plan.Members.Select(m => m.UserId), user.Id))
            return this.Denied();

        var item = await db.TripItineraryItems.FirstOrDefaultAsync(x => x.Id == itemId && x.TripPlanId == id, ct);
        if (item is not null) { db.TripItineraryItems.Remove(item); await db.SaveChangesAsync(ct); }
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var plan = await db.TripPlans.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (plan is null) return NoContent();
        if (!StayHost.Domain.TripPlans.IsOwner(plan.OwnerId, user.Id)) return this.Denied();

        db.TripPlans.Remove(plan);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private Task<TripPlan?> Load(int id, CancellationToken ct) =>
        db.TripPlans
            .Include(t => t.Members).ThenInclude(m => m.User)
            .Include(t => t.Bookings).ThenInclude(b => b.Booking!).ThenInclude(bk => bk.Listing)
            .Include(t => t.Items)
            .Include(t => t.Owner)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    private static TripPlanDetailDto ToDetail(TripPlan plan, int viewerId)
    {
        var memberIds = plan.Members.Select(m => m.UserId).ToList();
        var addedByName = plan.Members.ToDictionary(m => m.UserId, m =>
            Profiles.DisplayNameOf(m.User!.DisplayName, m.User.FullName));
        addedByName[plan.OwnerId] = Profiles.DisplayNameOf(plan.Owner!.DisplayName, plan.Owner.FullName);

        var bookings = plan.Bookings
            .Where(b => b.Booking?.Listing is not null)
            .Select(b => new TripPlanBookingDto(
                b.BookingId, b.Booking!.Reference, b.Booking.Listing!.Title, b.Booking.Listing.City,
                b.Booking.CheckIn, b.Booking.CheckOut))
            .OrderBy(b => b.CheckIn)
            .ToList();

        var members = new List<TripMemberDto>
        {
            new(plan.OwnerId, addedByName[plan.OwnerId], plan.Owner!.Initials, plan.Owner.AvatarUrl, true)
        };
        members.AddRange(plan.Members.Select(m => new TripMemberDto(
            m.UserId, Profiles.DisplayNameOf(m.User!.DisplayName, m.User.FullName),
            m.User.Initials, m.User.AvatarUrl, false)));

        var items = plan.Items
            .OrderBy(i => i.Day).ThenBy(i => i.SortOrder)
            .Select(i => new TripItineraryItemDto(
                i.Id, i.Day, i.Title, i.Note,
                addedByName.TryGetValue(i.AddedByUserId, out var n) ? n : "Thành viên", i.SortOrder))
            .ToList();

        return new TripPlanDetailDto(
            plan.Id, plan.Name,
            StayHost.Domain.TripPlans.CanEdit(plan.OwnerId, memberIds, viewerId),
            StayHost.Domain.TripPlans.IsOwner(plan.OwnerId, viewerId),
            bookings, members, items);
    }
}
