using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

/// <summary>
/// docs/08 §8, the user's side of it — seeing what was decided about you and
/// filing the one appeal the section promises.
///
/// A warning or a restriction leaves the account signed in, so those appeal from
/// this page. A suspension does not — which is why the sanction email carries an
/// <see cref="TokenPurpose.AppealAccess"/> token: the promise "bạn có thể khiếu
/// nại" has to survive the person being locked out.
/// </summary>
[ApiController]
[Route("api/account/sanctions")]
public class SanctionsController(StayHostDbContext db, AuthService auth) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MySanctionDto>>> Mine(CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var sanctions = await db.Sanctions
            .Where(s => s.UserId == user.Id)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        var appeals = await db.Appeals
            .Where(a => a.UserId == user.Id)
            .ToDictionaryAsync(a => a.SanctionId, ct);

        var now = DateTime.UtcNow;

        return Ok(sanctions.Select(s =>
        {
            var appeal = appeals.GetValueOrDefault(s.Id);
            var mayFile = Appeals.MayFile(appeal is not null, s.CreatedAt, now);

            return new MySanctionDto(
                s.Id, Sanctions.Label(s.Level),
                s.Restriction is { } k ? Sanctions.RestrictionLabel(k) : null,
                s.Policy, s.Reason, s.LiftedWhen,
                s.CreatedAt, s.ExpiresAt, s.LiftedAt, s.OverturnedOnAppeal,
                mayFile,
                mayFile ? null : Appeals.CannotFileMessage(appeal is not null, s.CreatedAt),
                appeal is null ? null : Appeals.StatusLabel(appeal.Status),
                appeal?.Outcome,
                appeal?.DueBy);
        }).ToList());
    }

    /// <summary>docs/08 §8 — one appeal, within the window, argued in words.</summary>
    [HttpPost("{id:int}/appeal")]
    public async Task<ActionResult> Appeal(int id, [FromBody] FileAppealRequest req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var sanction = await db.Sanctions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == user.Id, ct);
        if (sanction is null) return NotFound(new { message = "Không tìm thấy quyết định này." });

        return await FileAsync(sanction, user, req.Argument, ct);
    }

    /// <summary>
    /// docs/08 §8 for the locked-out — the email's appeal link lands here. The
    /// token names the person; the appeal lands on the sanction that locked them.
    /// </summary>
    [HttpPost("appeal-by-token")]
    public async Task<ActionResult> AppealByToken([FromBody] AppealByTokenRequest req, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var token = await db.UserTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == (req.Token ?? "")
                                      && t.Purpose == TokenPurpose.AppealAccess
                                      && t.UsedAt == null && t.ExpiresAt > now, ct);

        if (token?.User is null)
            return BadRequest(new { message = "Liên kết khiếu nại không hợp lệ hoặc đã hết hạn." });

        var sanction = await db.Sanctions
            .Where(s => s.UserId == token.UserId && s.Level >= SanctionLevel.Suspension && s.LiftedAt == null)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (sanction is null)
            return BadRequest(new { message = "Không còn quyết định nào đang chờ khiếu nại." });

        var result = await FileAsync(sanction, token.User, req.Argument, ct);
        if (result is OkObjectResult)
        {
            token.UsedAt = now;
            await db.SaveChangesAsync(ct);
        }
        return result;
    }

    private async Task<ActionResult> FileAsync(Sanction sanction, User user, string? argument, CancellationToken ct)
    {
        if (!Appeals.ArgumentIsUsable(argument))
            return BadRequest(new { message = Appeals.CurtArgumentMessage() });

        var already = await db.Appeals.AnyAsync(a => a.SanctionId == sanction.Id, ct);
        var now = DateTime.UtcNow;

        if (!Appeals.MayFile(already, sanction.CreatedAt, now))
            return BadRequest(new { message = Appeals.CannotFileMessage(already, sanction.CreatedAt) });

        db.Appeals.Add(new Appeal
        {
            SanctionId = sanction.Id,
            UserId = user.Id,
            Argument = argument!.Trim(),
            DueBy = Appeals.DueBy(now)
        });

        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            message = $"Đã nhận khiếu nại. Một người khác với người ra quyết định sẽ xét lại " +
                      $"và trả lời bạn trong {Appeals.AnswerWorkingDays} ngày làm việc."
        });
    }

    /// <summary>Issues the token the suspension email carries. Valid exactly as long as the window.</summary>
    internal static UserToken IssueAppealToken(int userId) => new()
    {
        Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('='),
        UserId = userId,
        Purpose = TokenPurpose.AppealAccess,
        ExpiresAt = DateTime.UtcNow.AddDays(Appeals.WindowDays)
    };
}
