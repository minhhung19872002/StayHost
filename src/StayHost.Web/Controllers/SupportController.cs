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
