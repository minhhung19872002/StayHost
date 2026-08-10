using Microsoft.AspNetCore.Mvc;
using StayHost.Domain;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

/// <summary>
/// docs/01 TĐ-03, TN-06 — machine translation for listing descriptions and chat
/// messages. The config endpoint lets the browser show the "Dịch" button only
/// when translation is actually on, the same way it decides whether to draw a
/// social-login button.
/// </summary>
[ApiController]
[Route("api/translate")]
public class TranslateController(TranslationService translation) : ControllerBase
{
    public record TranslateConfigDto(bool Enabled, IReadOnlyList<TargetDto> Targets);
    public record TargetDto(string Code, string Label);
    public record TranslateRequest(string? Text, string? TargetLang);
    public record TranslateResultDto(string Text, string TargetLang);

    [HttpGet("config")]
    public ActionResult<TranslateConfigDto> Config() =>
        Ok(new TranslateConfigDto(
            translation.Enabled,
            Translations.Targets.Select(t => new TargetDto(t.Code, t.Label)).ToList()));

    [HttpPost]
    public async Task<ActionResult<TranslateResultDto>> Translate(
        [FromBody] TranslateRequest req, CancellationToken ct)
    {
        var result = await translation.TranslateAsync(req.Text, req.TargetLang, ct);
        if (!result.Ok) return BadRequest(new { message = result.Error });
        return Ok(new TranslateResultDto(result.Text!, (req.TargetLang ?? "vi").Trim().ToLowerInvariant()));
    }
}
