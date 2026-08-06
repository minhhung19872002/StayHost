using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

/// <summary>Guest ↔ host conversations, one thread per (listing, guest) pair.</summary>
[ApiController]
[Route("api/messages")]
public class MessagesController(StayHostDbContext db, AuthService auth, NotificationService notifications)
    : ControllerBase
{
    [HttpGet("threads")]
    public async Task<ActionResult<IReadOnlyList<ThreadSummaryDto>>> Threads(CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var threads = await db.MessageThreads
            .Where(t => t.GuestUserId == user.Id || t.HostUserId == user.Id)
            .Include(t => t.Listing!).ThenInclude(l => l.Images)
            .Include(t => t.GuestUser)
            .Include(t => t.HostUser)
            .Include(t => t.Messages)
            .OrderByDescending(t => t.LastMessageAt)
            .AsSplitQuery()
            .ToListAsync(ct);

        // The preview line in the list obeys the same masking as the thread itself.
        var unlocked = await UnlockedThreadIdsAsync(threads, ct);
        return Ok(threads.Select(t => Summarize(t, user.Id, unlocked.Contains(t.Id))).ToList());
    }

    [HttpGet("threads/{id:int}")]
    public async Task<ActionResult<ThreadDetailDto>> Thread(int id, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var thread = await LoadThreadAsync(id, ct);
        if (thread is null) return NotFound();
        if (thread.GuestUserId != user.Id && thread.HostUserId != user.Id) return Forbid();

        // Opening a thread marks the other side's messages as read.
        var unread = thread.Messages.Where(m => m.SenderUserId != user.Id && m.ReadAt is null).ToList();
        if (unread.Count > 0)
        {
            foreach (var m in unread) m.ReadAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return Ok(await DetailAsync(thread, user, ct));
    }

    [HttpPost]
    public async Task<ActionResult<ThreadDetailDto>> Send([FromBody] SendMessageRequest req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var body = (req.Body ?? "").Trim();
        var hasPhotos = (req.Attachments ?? []).Any(u => !string.IsNullOrWhiteSpace(u));
        if (body.Length == 0 && !hasPhotos) return BadRequest(new { message = "Tin nhắn trống." });
        if (body.Length > 4000) return BadRequest(new { message = "Tin nhắn quá dài." });

        MessageThread? thread;

        if (req.ThreadId is int threadId)
        {
            thread = await LoadThreadAsync(threadId, ct);
            if (thread is null) return NotFound();
            if (thread.GuestUserId != user.Id && thread.HostUserId != user.Id) return Forbid();
        }
        else
        {
            if (req.ListingId is not int listingId)
                return BadRequest(new { message = "Thiếu chỗ nghỉ để bắt đầu hội thoại." });

            var listing = await db.Listings.Include(l => l.Host).FirstOrDefaultAsync(l => l.Id == listingId, ct);
            if (listing is null) return NotFound();

            var hostUserId = listing.Host?.UserId;
            if (hostUserId is null)
                return BadRequest(new { message = "Chủ nhà demo này chưa có tài khoản nhận tin nhắn." });
            if (hostUserId == user.Id)
                return BadRequest(new { message = "Bạn không thể nhắn tin cho chính mình." });

            thread = await db.MessageThreads
                .Include(t => t.Messages)
                .FirstOrDefaultAsync(t => t.ListingId == listingId && t.GuestUserId == user.Id, ct);

            if (thread is null)
            {
                thread = new MessageThread
                {
                    ListingId = listingId,
                    GuestUserId = user.Id,
                    HostUserId = hostUserId.Value
                };
                db.MessageThreads.Add(thread);
                await db.SaveChangesAsync(ct);
            }
        }

        // docs/01 TN-02 — photos ride along with the text, capped so one message
        // cannot become an album.
        var attachments = string.Join('\n',
            (req.Attachments ?? []).Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).Take(6));

        thread.Messages.Add(new Message
        {
            ThreadId = thread.Id, SenderUserId = user.Id, Body = body, Attachments = attachments
        });
        thread.LastMessageAt = DateTime.UtcNow;

        var recipientId = thread.GuestUserId == user.Id ? thread.HostUserId : thread.GuestUserId;
        var recipient = await db.Users.FirstOrDefaultAsync(u => u.Id == recipientId, ct);
        notifications.Queue(recipientId, NotificationKind.MessageReceived,
            $"Tin nhắn mới từ {user.FullName}",
            body.Length > 140 ? body[..140] + "…" : body,
            "/messages");
        _ = recipient;

        await db.SaveChangesAsync(ct);

        var fresh = await LoadThreadAsync(thread.Id, ct);
        return Ok(await DetailAsync(fresh!, user, ct));
    }

    /// <summary>
    /// One place builds the thread payload: the masked messages, the order card
    /// of docs/01 TN-03, and the host's saved phrases of TN-08.
    /// </summary>
    private async Task<ThreadDetailDto> DetailAsync(MessageThread thread, User viewer, CancellationToken ct)
    {
        var open = await ContactsUnlockedAsync(thread, ct);
        var viewerIsHost = thread.HostUserId == viewer.Id;

        // TN-03 — the most relevant order for these two on this listing: the live
        // one if there is one, otherwise the most recent.
        var booking = await db.Bookings
            .Where(b => b.ListingId == thread.ListingId && b.GuestUserId == thread.GuestUserId)
            .OrderByDescending(b => BookingLifecycle.BlocksDates.Contains(b.Status))
            .ThenByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var quickReplies = viewerIsHost
            ? await db.QuickReplies
                .Where(q => q.HostUserId == viewer.Id)
                .OrderBy(q => q.SortOrder).ThenBy(q => q.Id)
                .Select(q => new QuickReplyDto(q.Id, q.Title, q.Body, q.SortOrder))
                .ToListAsync(ct)
            : [];

        return new ThreadDetailDto(
            Summarize(thread, viewer.Id, open),
            thread.Messages.OrderBy(m => m.SentAt).Select(m => ToDto(m, viewer.Id, open)).ToList(),
            open,
            booking is null ? null : new ThreadBookingDto(
                booking.Id, booking.Reference, booking.CheckIn, booking.CheckOut,
                booking.Nights, booking.Guests, booking.Total,
                BookingLifecycle.Label(booking.Status), BookingLifecycle.BadgeClass(booking.Status),
                viewerIsHost && booking.Status == BookingStatus.PendingHostApproval),
            quickReplies);
    }

    /* ------------------------------------------------------------- TN-08 */

    [HttpGet("quick-replies")]
    public async Task<ActionResult<IReadOnlyList<QuickReplyDto>>> QuickReplies(CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        return Ok(await db.QuickReplies
            .Where(q => q.HostUserId == user.Id)
            .OrderBy(q => q.SortOrder).ThenBy(q => q.Id)
            .Select(q => new QuickReplyDto(q.Id, q.Title, q.Body, q.SortOrder))
            .ToListAsync(ct));
    }

    [HttpPost("quick-replies")]
    public async Task<ActionResult<QuickReplyDto>> AddQuickReply(
        [FromBody] SaveQuickReplyRequest req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var title = (req.Title ?? "").Trim();
        var body = (req.Body ?? "").Trim();
        if (title.Length == 0 || body.Length == 0)
            return BadRequest(new { message = "Mẫu trả lời cần cả tên và nội dung." });

        var reply = new QuickReply
        {
            HostUserId = user.Id, Title = title, Body = body, SortOrder = req.SortOrder
        };
        db.QuickReplies.Add(reply);
        await db.SaveChangesAsync(ct);

        return Ok(new QuickReplyDto(reply.Id, reply.Title, reply.Body, reply.SortOrder));
    }

    [HttpDelete("quick-replies/{id:int}")]
    public async Task<IActionResult> DeleteQuickReply(int id, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var reply = await db.QuickReplies.FirstOrDefaultAsync(q => q.Id == id && q.HostUserId == user.Id, ct);
        if (reply is null) return NoContent();

        db.QuickReplies.Remove(reply);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// docs/03 §10 — contact details stay hidden until this guest has a
    /// confirmed booking at this listing. Before that, the two sides trade only
    /// through the platform.
    /// </summary>
    private Task<bool> ContactsUnlockedAsync(MessageThread thread, CancellationToken ct) =>
        db.Bookings.AnyAsync(b =>
            b.ListingId == thread.ListingId &&
            b.GuestUserId == thread.GuestUserId &&
            (b.Status == BookingStatus.Confirmed
             || b.Status == BookingStatus.InProgress
             || b.Status == BookingStatus.Completed), ct);

    private async Task<HashSet<int>> UnlockedThreadIdsAsync(
        IReadOnlyCollection<MessageThread> threads, CancellationToken ct)
    {
        if (threads.Count == 0) return [];

        var listingIds = threads.Select(t => t.ListingId).Distinct().ToList();
        var guestIds = threads.Select(t => t.GuestUserId).Distinct().ToList();

        var confirmed = await db.Bookings
            .Where(b => listingIds.Contains(b.ListingId)
                        && b.GuestUserId != null && guestIds.Contains(b.GuestUserId.Value)
                        && (b.Status == BookingStatus.Confirmed
                            || b.Status == BookingStatus.InProgress
                            || b.Status == BookingStatus.Completed))
            .Select(b => new { b.ListingId, b.GuestUserId })
            .ToListAsync(ct);

        var pairs = confirmed.Select(c => (c.ListingId, c.GuestUserId)).ToHashSet();
        return threads
            .Where(t => pairs.Contains((t.ListingId, t.GuestUserId)))
            .Select(t => t.Id)
            .ToHashSet();
    }

    private Task<MessageThread?> LoadThreadAsync(int id, CancellationToken ct) =>
        db.MessageThreads
            .Include(t => t.Listing!).ThenInclude(l => l.Images)
            .Include(t => t.GuestUser)
            .Include(t => t.HostUser)
            .Include(t => t.Messages)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    private static ThreadSummaryDto Summarize(MessageThread t, int viewerId, bool contactsUnlocked)
    {
        var viewerIsHost = t.HostUserId == viewerId;
        var other = viewerIsHost ? t.GuestUser : t.HostUser;
        var last = t.Messages.OrderByDescending(m => m.SentAt).FirstOrDefault();

        return new ThreadSummaryDto(
            t.Id,
            t.ListingId,
            t.Listing?.Slug ?? "",
            t.Listing?.Title ?? "",
            t.Listing?.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault() ?? "",
            other?.FullName ?? "Người dùng",
            other?.Initials ?? "??",
            viewerIsHost,
            last is null ? null : Visible(last, contactsUnlocked),
            t.LastMessageAt,
            t.Messages.Count(m => m.SenderUserId != viewerId && m.ReadAt is null));
    }

    private static MessageDto ToDto(Message m, int viewerId, bool contactsUnlocked) => new(
        m.Id, m.SenderUserId, m.SenderUser?.FullName ?? "",
        Visible(m, contactsUnlocked), m.SentAt, m.SenderUserId == viewerId, m.IsSystem,
        !contactsUnlocked && !m.IsSystem && ContactGuardHit(m.Body),
        m.Attachments.Split('\n', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// What actually goes over the wire. The stored text is never altered — the
    /// masking happens on the way out, so unlocking a thread reveals the
    /// original rather than a permanently damaged copy.
    /// </summary>
    private static string Visible(Message m, bool contactsUnlocked) =>
        contactsUnlocked || m.IsSystem ? m.Body : ContentGuard.MaskContacts(m.Body);

    private static bool ContactGuardHit(string body) => ContentGuard.Inspect(body).Any;
}
