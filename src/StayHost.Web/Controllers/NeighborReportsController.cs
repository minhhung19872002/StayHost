using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;
using StayHost.Web.Infrastructure;

namespace StayHost.Web.Controllers;

/// <summary>
/// docs/01 AT-03 — the neighbour channel. Open to anyone, no account: a neighbour
/// files a concern about a nearby short-let and it lands in the admin queue.
/// </summary>
[ApiController]
[Route("api/neighbor-reports")]
public class NeighborReportsController(StayHostDbContext db) : ControllerBase
{
    [HttpGet("concerns")]
    public ActionResult<IReadOnlyList<NeighborConcernDto>> Concerns() =>
        Ok(NeighborReports.Concerns.Select(c => new NeighborConcernDto(c.Concern.ToString(), c.Label)).ToList());

    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] NeighborReportRequest req, CancellationToken ct)
    {
        var error = NeighborReports.Validate(req.Location, req.Detail);
        if (error is not null) return BadRequest(new { message = error });

        var concern = NeighborReports.TryParseConcern(req.Category, out var c) ? c : NeighborConcern.Other;
        var sessionId = HttpContext.SessionId();

        // A light brake on spam: one open report per session per location.
        var location = req.Location!.Trim();
        var dupe = await db.NeighborReports.AnyAsync(
            r => r.SessionId == sessionId && r.Location == location && r.Status == ReportStatus.Open, ct);
        if (dupe)
            return Ok(new { message = "Chúng tôi đã nhận phản ánh của bạn về địa điểm này. Cảm ơn bạn." });

        db.NeighborReports.Add(new NeighborReport
        {
            Location = location,
            Category = concern,
            Detail = req.Detail!.Trim(),
            Contact = string.IsNullOrWhiteSpace(req.Contact) ? null : req.Contact.Trim(),
            SessionId = sessionId
        });
        await db.SaveChangesAsync(ct);

        return Ok(new { message = "Đã gửi phản ánh. Đội ngũ Staylio sẽ xem xét. Cảm ơn bạn đã lên tiếng." });
    }
}
