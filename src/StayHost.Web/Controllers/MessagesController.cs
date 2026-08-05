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
public class MessagesController(StayHostDbContext db, AuthService auth) : ControllerBase
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

        return Ok(threads.Select(t => Summarize(t, user.Id)).ToList());
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

        return Ok(new ThreadDetailDto(
            Summarize(thread, user.Id),
            thread.Messages.OrderBy(m => m.SentAt).Select(m => ToDto(m, user.Id)).ToList()));
    }

    [HttpPost]
    public async Task<ActionResult<ThreadDetailDto>> Send([FromBody] SendMessageRequest req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var body = (req.Body ?? "").Trim();
        if (body.Length == 0) return BadRequest(new { message = "Tin nhắn trống." });
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

        thread.Messages.Add(new Message { ThreadId = thread.Id, SenderUserId = user.Id, Body = body });
        thread.LastMessageAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var fresh = await LoadThreadAsync(thread.Id, ct);
        return Ok(new ThreadDetailDto(
            Summarize(fresh!, user.Id),
            fresh!.Messages.OrderBy(m => m.SentAt).Select(m => ToDto(m, user.Id)).ToList()));
    }

    private Task<MessageThread?> LoadThreadAsync(int id, CancellationToken ct) =>
        db.MessageThreads
            .Include(t => t.Listing!).ThenInclude(l => l.Images)
            .Include(t => t.GuestUser)
            .Include(t => t.HostUser)
            .Include(t => t.Messages)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    private static ThreadSummaryDto Summarize(MessageThread t, int viewerId)
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
            last?.Body,
            t.LastMessageAt,
            t.Messages.Count(m => m.SenderUserId != viewerId && m.ReadAt is null));
    }

    private static MessageDto ToDto(Message m, int viewerId) => new(
        m.Id, m.SenderUserId, m.SenderUser?.FullName ?? "", m.Body, m.SentAt, m.SenderUserId == viewerId);
}
