using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;
using StayHost.Web.Infrastructure;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

/// <summary>
/// docs/01 AT-09 — reaching a human support agent. A guest raises a ticket; the
/// support desk works the queue, urgent first.
/// </summary>
[ApiController]
[Route("api/support")]
public class SupportController(
    StayHostDbContext db, AuthService auth, AdminAudit audit, NotificationService notifications)
    : ControllerBase
{
    /// <summary>docs/01 AT-09 — the topics offered, so the client need not hard-code them.</summary>
    [HttpGet("topics")]
    public ActionResult<object> Topics() =>
        Ok(SupportTickets.Topics.Select(t => new { key = t.Key, label = t.Label }));

    /// <summary>
    /// docs/01 AT-08 — the automated assistant. Reads the caller's live situation
    /// and returns the actions that actually apply, each with a link. Anonymous
    /// callers get the login/help fallback rather than an error.
    /// </summary>
    [HttpGet("assistant")]
    public async Task<ActionResult<object>> Assistant(CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        SupportAssistant.Context context;

        if (user is null)
        {
            context = new SupportAssistant.Context(false, false, false, false, false, false, false, false);
        }
        else
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var soon = today.AddDays(2);

            var mine = db.Bookings.Where(b => b.GuestUserId == user.Id);

            var arrivalSoon = await mine.AnyAsync(b =>
                b.Status == BookingStatus.Confirmed && b.CheckIn >= today && b.CheckIn <= soon, ct);
            var balanceDue = await mine.AnyAsync(b =>
                b.BalanceStatus == BalanceStatus.Scheduled && b.BalanceDue > 0, ct);
            var pending = await mine.AnyAsync(b => b.Status == BookingStatus.PendingHostApproval, ct);
            var unreviewed = await mine.AnyAsync(b =>
                b.Status == BookingStatus.Completed
                && !db.Reviews.Any(r => r.BookingId == b.Id && r.AuthorUserId == user.Id), ct);
            var dispute = await db.ResolutionCases.AnyAsync(c =>
                c.OpenedByUserId == user.Id
                && (c.Status == ResolutionStatus.AwaitingResponse || c.Status == ResolutionStatus.Disputed), ct);

            var host = await db.Hosts.FirstOrDefaultAsync(h => h.UserId == user.Id, ct);
            var hostReqs = host is not null && await db.Bookings.AnyAsync(b =>
                b.Listing!.HostId == host.Id && b.Status == BookingStatus.PendingHostApproval, ct);

            context = new SupportAssistant.Context(
                true, arrivalSoon, balanceDue, pending, unreviewed, dispute, host is not null, hostReqs);
        }

        var suggestions = SupportAssistant.Suggest(context)
            .Select(s => new { text = s.Text, actionLabel = s.ActionLabel, actionLink = s.ActionLink })
            .ToList();
        return Ok(new { suggestions });
    }

    [HttpPost("tickets")]
    public async Task<ActionResult<object>> Create([FromBody] CreateSupportTicketRequest req, CancellationToken ct)
    {
        if (SupportTickets.Validate(req.Subject, req.Message) is { } invalid)
            return BadRequest(new { message = invalid });

        var user = await auth.CurrentUserAsync(ct);

        // A booking may be named, but only if it is the requester's own — a ticket
        // must not become a way to attach yourself to someone else's stay.
        int? bookingId = null;
        if (req.BookingId is { } bid && user is not null)
        {
            var owns = await db.Bookings.AnyAsync(b => b.Id == bid && b.GuestUserId == user.Id, ct);
            if (owns) bookingId = bid;
        }

        var ticket = new SupportTicket
        {
            UserId = user?.Id,
            SessionId = HttpContext.SessionId(),
            BookingId = bookingId,
            Subject = req.Subject.Trim(),
            Message = req.Message.Trim(),
            Priority = SupportTickets.PriorityFor(req.Topic)
        };
        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            id = ticket.Id,
            urgent = ticket.Priority == SupportPriority.Urgent,
            message = ticket.Priority == SupportPriority.Urgent
                ? "Đã chuyển tới nhân viên hỗ trợ theo mức ưu tiên khẩn cấp. Chúng tôi sẽ liên hệ ngay."
                : "Đã gửi tới nhân viên hỗ trợ. Chúng tôi sẽ phản hồi sớm."
        });
    }

    /* --------------------------------------------------------------- admin */

    [HttpGet("tickets")]
    public async Task<ActionResult<IReadOnlyList<SupportTicketDto>>> Queue(CancellationToken ct)
    {
        if (await audit.RequireAsync(AdminScope.Support, ct) is null)
            return StatusCode(403, new { message = "Cần quyền Hỗ trợ để xem hàng chờ." });

        var tickets = await db.SupportTickets
            .Include(t => t.User)
            .Include(t => t.Booking)
            .Where(t => t.Status == SupportTicketStatus.Open)
            .ToListAsync(ct);

        return Ok(SupportTickets.Queue(tickets).Select(ToDto).ToList());
    }

    [HttpPost("tickets/{id:int}/resolve")]
    public async Task<IActionResult> Resolve(int id, [FromBody] ResolveSupportTicketRequest req, CancellationToken ct)
    {
        var admin = await audit.RequireAsync(AdminScope.Support, ct);
        if (admin is null) return StatusCode(403, new { message = "Cần quyền Hỗ trợ." });

        var ticket = await db.SupportTickets.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null) return NotFound();

        ticket.Status = SupportTicketStatus.Resolved;
        ticket.AdminReply = req.Reply?.Trim();
        ticket.HandledByUserId = admin.Id;
        ticket.ResolvedAt = DateTime.UtcNow;

        audit.Record(admin, "support.resolve", $"ticket:{ticket.Id}", "Open", "Resolved", ticket.AdminReply);

        if (ticket.User is not null)
            await notifications.QueueWithEmailAsync(ticket.User, NotificationKind.System,
                "Phản hồi từ nhân viên hỗ trợ",
                $"Về \"{ticket.Subject}\": {ticket.AdminReply ?? "Vấn đề của bạn đã được xử lý."}", "/help", ct);

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static SupportTicketDto ToDto(SupportTicket t) => new(
        t.Id, t.Subject, t.Message,
        t.Priority == SupportPriority.Urgent,
        t.User?.FullName ?? "Khách ẩn danh",
        t.Booking?.Reference,
        t.CreatedAt);
}
