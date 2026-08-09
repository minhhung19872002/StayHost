using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Infrastructure;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

/// <summary>
/// docs/01 QT-08 — which features are on for whoever is asking. A logged-in user
/// is bucketed by their id so the answer is stable across devices; an anonymous
/// visitor by their session, so a partial rollout still behaves consistently
/// within a visit.
/// </summary>
[ApiController]
[Route("api")]
public class FeaturesController(StayHostDbContext db, AuthService auth) : ControllerBase
{
    [HttpGet("features")]
    public async Task<ActionResult<Dictionary<string, bool>>> Features(CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        var stableKey = user is not null ? $"u{user.Id}" : HttpContext.SessionId();

        var flags = await db.FeatureFlags.ToListAsync(ct);
        var map = flags.ToDictionary(f => f.Key, f => FeatureRollout.IsOn(f, stableKey));
        return Ok(map);
    }
}
