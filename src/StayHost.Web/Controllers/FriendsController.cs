using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;
using StayHost.Web.Infrastructure;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

/// <summary>
/// docs/01 XH-01, XH-02 — connecting with other members and seeing where friends
/// have been and are going, subject to each person's journey privacy.
/// </summary>
[ApiController]
[Route("api/friends")]
public class FriendsController(StayHostDbContext db, AuthService auth) : ControllerBase
{
    private static readonly string[] Decisions = ["accept", "decline"];

    private Task<Friendship?> PairAsync(int a, int b, CancellationToken ct) =>
        db.Friendships.FirstOrDefaultAsync(
            f => (f.RequesterId == a && f.AddresseeId == b) || (f.RequesterId == b && f.AddresseeId == a), ct);

    /// <summary>docs/01 XH-01 — the accepted friends.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FriendDto>>> List(CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var rows = await db.Friendships
            .Where(f => f.Status == FriendshipStatus.Accepted && (f.RequesterId == user.Id || f.AddresseeId == user.Id))
            .Select(f => f.RequesterId == user.Id ? f.Addressee! : f.Requester!)
            .Select(u => new FriendDto(u.Id, Profiles.DisplayNameOf(u.DisplayName, u.FullName), u.Initials, u.AvatarUrl))
            .ToListAsync(ct);
        return Ok(rows);
    }

    /// <summary>docs/01 XH-01 — friend requests waiting on this account.</summary>
    [HttpGet("requests")]
    public async Task<ActionResult<IReadOnlyList<FriendRequestDto>>> Requests(CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var rows = await db.Friendships
            .Where(f => f.Status == FriendshipStatus.Pending && f.AddresseeId == user.Id)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new FriendRequestDto(
                f.Id, f.Requester!.Id, Profiles.DisplayNameOf(f.Requester.DisplayName, f.Requester.FullName),
                f.Requester.Initials, f.Requester.AvatarUrl, f.CreatedAt))
            .ToListAsync(ct);
        return Ok(rows);
    }

    /// <summary>docs/01 XH-01 — send a friend request (or accept a reverse one already pending).</summary>
    [HttpPost("request/{userId:int}")]
    public async Task<IActionResult> Request(int userId, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        if (Friendships.ValidateRequest(user.Id, userId) is { } invalid)
            return BadRequest(new { message = invalid });
        if (!await db.Users.AnyAsync(u => u.Id == userId, ct))
            return NotFound(new { message = "Không tìm thấy người dùng." });

        var existing = await PairAsync(user.Id, userId, ct);
        if (existing is not null)
        {
            if (existing.Status == FriendshipStatus.Accepted)
                return Ok(new { message = "Hai bạn đã là bạn bè." });
            // They already asked us — sending back accepts it.
            if (existing.Status == FriendshipStatus.Pending && existing.AddresseeId == user.Id)
            {
                existing.Status = FriendshipStatus.Accepted;
                existing.RespondedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                return Ok(new { message = "Đã trở thành bạn bè." });
            }
            if (existing.Status == FriendshipStatus.Pending)
                return Ok(new { message = "Đã gửi lời mời, đang chờ phản hồi." });
            // A previously declined pair may be asked again.
            existing.RequesterId = user.Id; existing.AddresseeId = userId;
            existing.Status = FriendshipStatus.Pending; existing.CreatedAt = DateTime.UtcNow; existing.RespondedAt = null;
            await db.SaveChangesAsync(ct);
            return Ok(new { message = "Đã gửi lời mời kết bạn." });
        }

        db.Friendships.Add(new Friendship { RequesterId = user.Id, AddresseeId = userId });
        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Đã gửi lời mời kết bạn." });
    }

    /// <summary>docs/01 XH-01 — accept or decline a request. {decision} not {action}.</summary>
    [HttpPost("{id:int}/respond/{decision}")]
    public async Task<IActionResult> Respond(int id, string decision, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });
        if (!Decisions.Contains(decision)) return BadRequest(new { message = "Quyết định không hợp lệ." });

        var f = await db.Friendships.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (f is null) return NotFound();
        if (!Friendships.CanRespond(f, user.Id)) return this.Denied();

        f.Status = decision == "accept" ? FriendshipStatus.Accepted : FriendshipStatus.Declined;
        f.RespondedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>docs/01 XH-01 — unfriend.</summary>
    [HttpDelete("{userId:int}")]
    public async Task<IActionResult> Remove(int userId, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var f = await PairAsync(user.Id, userId, ct);
        if (f is not null) { db.Friendships.Remove(f); await db.SaveChangesAsync(ct); }
        return NoContent();
    }

    /// <summary>docs/01 XH-02 — set who may see my journey map.</summary>
    [HttpPut("journey-visibility")]
    public async Task<IActionResult> SetVisibility([FromBody] JourneyVisibilityRequest req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        user.JourneyVisibility = Enum.TryParse<JourneyVisibility>(req.Visibility, true, out var v)
            ? v : JourneyVisibility.Friends;
        await db.SaveChangesAsync(ct);
        return Ok(new { visibility = user.JourneyVisibility.ToString() });
    }

    /// <summary>
    /// docs/01 XH-01/XH-02 — where a member has been and is going, if the viewer is
    /// allowed to see it. The owner always can; others depend on the owner's setting.
    /// </summary>
    [HttpGet("{userId:int}/journey")]
    public async Task<ActionResult<FriendJourneyDto>> Journey(int userId, CancellationToken ct)
    {
        var viewer = await auth.CurrentUserAsync(ct);
        var target = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (target is null) return NotFound();

        var isSelf = viewer?.Id == target.Id;
        var areFriends = viewer is not null && await db.Friendships.AnyAsync(
            f => f.Status == FriendshipStatus.Accepted
                 && ((f.RequesterId == viewer.Id && f.AddresseeId == target.Id)
                     || (f.RequesterId == target.Id && f.AddresseeId == viewer.Id)), ct);

        if (!Friendships.CanSeeJourney(target.JourneyVisibility, isSelf, areFriends))
            return StatusCode(403, new { message = "Hành trình của người này ở chế độ riêng tư." });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var stays = await db.Bookings
            .Where(b => b.GuestUserId == target.Id
                        && (b.Status == BookingStatus.Completed || b.Status == BookingStatus.Confirmed
                            || b.Status == BookingStatus.InProgress))
            .Select(b => new
            {
                b.Status, b.CheckIn, b.Nights,
                b.Listing!.City, b.Listing.Latitude, b.Listing.Longitude
            })
            .ToListAsync(ct);

        var been = stays.Where(s => s.Status == BookingStatus.Completed || s.CheckIn <= today)
            .OrderByDescending(s => s.CheckIn)
            .Select(s => new JourneyStopDto(s.City, s.Latitude, s.Longitude, s.Nights, s.CheckIn)).ToList();
        var upcoming = stays.Where(s => s.Status != BookingStatus.Completed && s.CheckIn > today)
            .OrderBy(s => s.CheckIn)
            .Select(s => new JourneyStopDto(s.City, s.Latitude, s.Longitude, s.Nights, s.CheckIn)).ToList();

        var name = Profiles.DisplayNameOf(target.DisplayName, target.FullName);
        return Ok(new FriendJourneyDto(name, target.JourneyVisibility.ToString(), been, upcoming));
    }
}
