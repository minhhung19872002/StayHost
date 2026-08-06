using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;
using StayHost.Web.Infrastructure;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

/// <summary>
/// docs/01 QL-19 (people helping run a listing) and QL-10 (calendars kept on
/// other platforms). Both are about letting something outside this account
/// touch a listing, so both live behind the same ownership check.
/// </summary>
[ApiController]
[Route("api/host")]
public class HostTeamController(
    StayHostDbContext db,
    AuthService auth,
    HostAccess access,
    CalendarSyncService sync,
    NotificationService notifications) : ControllerBase
{
    /* ------------------------------------------------------------- QL-19 */

    /// <summary>Everyone this host invited, and everywhere this user is a co-host.</summary>
    [HttpGet("co-hosts")]
    public async Task<ActionResult<CoHostBoardDto>> CoHosts(CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var invited = await db.CoHosts
            .Where(c => c.OwnerUserId == user.Id && c.Status != CoHostStatus.Revoked)
            .OrderByDescending(c => c.InvitedAt)
            .Select(c => new CoHostDto(
                c.Id, c.Email, c.CoHostUser!.FullName, c.ListingId, c.Listing!.Title,
                CoHostScopes.Keys(c.Scope), CoHostScopes.Describe(c.Scope),
                c.Status.ToString().ToLower(), StatusLabel(c.Status), c.InvitedAt))
            .ToListAsync(ct);

        var helping = await db.CoHosts
            .Where(c => (c.CoHostUserId == user.Id || c.Email == user.Email) && c.Status != CoHostStatus.Revoked)
            .OrderByDescending(c => c.InvitedAt)
            .Select(c => new CoHostInviteDto(
                c.Id, c.InviteToken, c.OwnerUser!.FullName, c.ListingId, c.Listing!.Title,
                CoHostScopes.Describe(c.Scope), c.Status.ToString().ToLower(), StatusLabel(c.Status)))
            .ToListAsync(ct);

        return Ok(new CoHostBoardDto(
            invited, helping,
            CoHostScopes.All.Select(s => new ScopeOptionDto(s.Key, s.Label)).ToList()));
    }

    /// <summary>
    /// The invite is keyed by email, not by account, so a host can bring in
    /// someone who has not signed up yet.
    /// </summary>
    [HttpPost("co-hosts")]
    public async Task<ActionResult<CoHostDto>> Invite([FromBody] InviteCoHostRequest req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        if (email.Length == 0 || !email.Contains('@'))
            return BadRequest(new { message = "Email không hợp lệ." });
        if (string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Bạn đã là chủ nhà của tin này rồi." });

        var scope = CoHostScopes.Parse(req.Scopes);
        if (scope == CoHostScope.None)
            return BadRequest(new { message = "Chọn ít nhất một quyền cho người đồng quản lý." });

        // A listing named here must be one of the inviter's own; a co-host does
        // not get to hand out access they were lent.
        if (req.ListingId is { } listingId)
        {
            var profile = await db.Hosts.FirstOrDefaultAsync(h => h.UserId == user.Id, ct);
            var owns = profile is not null &&
                       await db.Listings.AnyAsync(l => l.Id == listingId && l.HostId == profile.Id, ct);
            if (!owns) return this.Denied();
        }

        var existing = await db.CoHosts.FirstOrDefaultAsync(c =>
            c.OwnerUserId == user.Id && c.Email == email && c.ListingId == req.ListingId
            && c.Status != CoHostStatus.Revoked, ct);

        if (existing is not null)
        {
            existing.Scope = scope;
            await db.SaveChangesAsync(ct);
            return Ok(await ToDtoAsync(existing, ct));
        }

        var invitee = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        var invite = new CoHost
        {
            OwnerUserId = user.Id,
            Email = email,
            CoHostUserId = invitee?.Id,
            ListingId = req.ListingId,
            Scope = scope
        };
        db.CoHosts.Add(invite);
        await db.SaveChangesAsync(ct);

        if (invitee is not null)
        {
            await notifications.QueueWithEmailAsync(
                invitee, NotificationKind.System,
                "Lời mời đồng quản lý",
                $"{user.FullName} mời bạn cùng quản lý chỗ nghỉ ({CoHostScopes.Describe(scope)}).",
                "/hosting?tab=team", ct);
            await db.SaveChangesAsync(ct);
        }

        return Ok(await ToDtoAsync(invite, ct));
    }

    [HttpPost("co-hosts/{id:int}/{decision}")]
    public async Task<IActionResult> Respond(int id, string decision, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var invite = await db.CoHosts.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (invite is null) return NotFound();

        var mine = invite.CoHostUserId == user.Id ||
                   string.Equals(invite.Email, user.Email, StringComparison.OrdinalIgnoreCase);
        if (!mine) return this.Denied();
        if (invite.Status is CoHostStatus.Revoked) return BadRequest(new { message = "Lời mời đã bị thu hồi." });

        invite.CoHostUserId = user.Id;
        invite.RespondedAt = DateTime.UtcNow;
        invite.Status = decision == "accept" ? CoHostStatus.Active : CoHostStatus.Declined;
        await db.SaveChangesAsync(ct);

        var owner = await db.Users.FirstOrDefaultAsync(u => u.Id == invite.OwnerUserId, ct);
        await notifications.QueueWithEmailAsync(
            owner, NotificationKind.System,
            invite.Status == CoHostStatus.Active ? "Đã nhận lời mời đồng quản lý" : "Đã từ chối đồng quản lý",
            $"{user.FullName} {(invite.Status == CoHostStatus.Active ? "đã nhận" : "đã từ chối")} lời mời của bạn.",
            "/hosting?tab=team", ct);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>Taking the access back, which the spec asks for by name.</summary>
    [HttpDelete("co-hosts/{id:int}")]
    public async Task<IActionResult> Revoke(int id, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var invite = await db.CoHosts.FirstOrDefaultAsync(c => c.Id == id && c.OwnerUserId == user.Id, ct);
        if (invite is null) return NotFound();

        invite.Status = CoHostStatus.Revoked;
        invite.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        if (invite.CoHostUserId is { } coHostId)
        {
            var coHost = await db.Users.FirstOrDefaultAsync(u => u.Id == coHostId, ct);
            await notifications.QueueWithEmailAsync(
                coHost, NotificationKind.System, "Quyền đồng quản lý đã kết thúc",
                "Chủ nhà đã thu hồi quyền đồng quản lý của bạn.", "/hosting", ct);
            await db.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    /* ------------------------------------------------------------- QL-10 */

    [HttpGet("listings/{id:int}/feeds")]
    public async Task<ActionResult<CalendarSyncDto>> Feeds(int id, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var listing = await access.ListingAsync(user, id, CoHostScope.Calendar, ct);
        if (listing is null) return this.Denied();

        return Ok(await BoardAsync(listing, ct));
    }

    [HttpPost("listings/{id:int}/feeds")]
    public async Task<ActionResult<CalendarSyncDto>> AddFeed(
        int id, [FromBody] AddCalendarFeedRequest req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var listing = await access.ListingAsync(user, id, CoHostScope.Calendar, ct);
        if (listing is null) return this.Denied();

        var url = (req.Url ?? "").Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return BadRequest(new { message = "Địa chỉ lịch phải là http hoặc https." });

        if (await db.CalendarFeeds.CountAsync(f => f.ListingId == id, ct) >= 5)
            return BadRequest(new { message = "Mỗi chỗ nghỉ chỉ nối được tối đa 5 lịch." });

        var feed = new CalendarFeed
        {
            ListingId = id,
            Label = string.IsNullOrWhiteSpace(req.Label) ? uri.Host : req.Label!.Trim(),
            Url = url
        };
        db.CalendarFeeds.Add(feed);
        await db.SaveChangesAsync(ct);

        // Sync on the spot: a host who just pasted a link wants to see whether
        // it worked, not wait an hour to find out it did not.
        await sync.SyncAsync(feed, ct);

        return Ok(await BoardAsync(listing, ct));
    }

    [HttpPost("listings/{id:int}/feeds/{feedId:int}/sync")]
    public async Task<ActionResult<CalendarSyncDto>> SyncFeed(int id, int feedId, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var listing = await access.ListingAsync(user, id, CoHostScope.Calendar, ct);
        if (listing is null) return this.Denied();

        var feed = await db.CalendarFeeds.FirstOrDefaultAsync(f => f.Id == feedId && f.ListingId == id, ct);
        if (feed is null) return NotFound();

        await sync.SyncAsync(feed, ct);
        return Ok(await BoardAsync(listing, ct));
    }

    [HttpDelete("listings/{id:int}/feeds/{feedId:int}")]
    public async Task<ActionResult<CalendarSyncDto>> RemoveFeed(int id, int feedId, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var listing = await access.ListingAsync(user, id, CoHostScope.Calendar, ct);
        if (listing is null) return this.Denied();

        var feed = await db.CalendarFeeds.FirstOrDefaultAsync(f => f.Id == feedId && f.ListingId == id, ct);
        if (feed is null) return NotFound();

        // The blocks go with it: they were never this host's decision.
        db.CalendarBlocks.RemoveRange(db.CalendarBlocks.Where(b => b.FeedId == feedId));
        db.CalendarFeeds.Remove(feed);
        await db.SaveChangesAsync(ct);

        return Ok(await BoardAsync(listing, ct));
    }

    /// <summary>
    /// The export other platforms poll. No cookie, no session — the token in the
    /// URL is the credential, so a wrong one is a plain 404.
    /// </summary>
    [HttpGet("/calendars/{id:int}/{token}.ics")]
    public async Task<IActionResult> Export(int id, string token, CancellationToken ct)
    {
        var listing = await db.Listings.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (listing is null || listing.IcalToken != token) return NotFound();

        var body = await sync.ExportAsync(listing, ct);
        return Content(body, Ical.ContentType, System.Text.Encoding.UTF8);
    }

    private async Task<CalendarSyncDto> BoardAsync(Listing listing, CancellationToken ct)
    {
        var feeds = await db.CalendarFeeds
            .Where(f => f.ListingId == listing.Id)
            .OrderBy(f => f.Id)
            .Select(f => new CalendarFeedDto(
                f.Id, f.Label, f.Url, f.LastSyncedAt, f.LastError, f.EventCount))
            .ToListAsync(ct);

        var export = $"{Request.Scheme}://{Request.Host}/calendars/{listing.Id}/{listing.IcalToken}.ics";
        return new CalendarSyncDto(listing.Id, listing.Title, export, feeds);
    }

    private async Task<CoHostDto> ToDtoAsync(CoHost invite, CancellationToken ct)
    {
        var name = invite.CoHostUserId is null
            ? null
            : await db.Users.Where(u => u.Id == invite.CoHostUserId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        var title = invite.ListingId is null
            ? null
            : await db.Listings.Where(l => l.Id == invite.ListingId).Select(l => l.Title).FirstOrDefaultAsync(ct);

        return new CoHostDto(
            invite.Id, invite.Email, name, invite.ListingId, title,
            CoHostScopes.Keys(invite.Scope), CoHostScopes.Describe(invite.Scope),
            invite.Status.ToString().ToLower(), StatusLabel(invite.Status), invite.InvitedAt);
    }

    private static string StatusLabel(CoHostStatus status) => status switch
    {
        CoHostStatus.Invited => "Đang chờ nhận lời",
        CoHostStatus.Active => "Đang đồng quản lý",
        CoHostStatus.Declined => "Đã từ chối",
        _ => "Đã thu hồi"
    };
}
