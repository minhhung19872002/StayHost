using Microsoft.AspNetCore.Mvc;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

/// <summary>
/// Listing photo uploads. Files land under wwwroot/uploads and are served straight
/// back as static content, so no object store is needed to run the app.
/// </summary>
[ApiController]
[Route("api/uploads")]
public class UploadsController(IWebHostEnvironment env, AuthService auth, ILogger<UploadsController> log)
    : ControllerBase
{
    private const long MaxBytes = 8 * 1024 * 1024;

    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/avif"] = ".avif"
    };

    [HttpPost("images")]
    [RequestSizeLimit(MaxBytes * 12)]
    public async Task<ActionResult<object>> UploadImages([FromForm] IFormFileCollection files, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });
        if (files.Count == 0) return BadRequest(new { message = "Chưa chọn ảnh nào." });
        if (files.Count > 12) return BadRequest(new { message = "Tối đa 12 ảnh mỗi lần." });

        var root = Path.Combine(env.WebRootPath, "uploads");
        Directory.CreateDirectory(root);

        var urls = new List<string>();

        foreach (var file in files)
        {
            if (file.Length == 0) continue;
            if (file.Length > MaxBytes)
                return BadRequest(new { message = $"\"{file.FileName}\" vượt quá 8MB." });
            if (!AllowedTypes.TryGetValue(file.ContentType, out var extension))
                return BadRequest(new { message = $"\"{file.FileName}\" không phải ảnh JPG/PNG/WebP/AVIF." });

            // Server-generated name: the client never controls the path.
            var name = $"{user.Id}-{Guid.NewGuid():N}{extension}";
            var path = Path.Combine(root, name);

            await using (var stream = System.IO.File.Create(path))
            {
                await file.CopyToAsync(stream, ct);
            }

            if (!await LooksLikeImageAsync(path, ct))
            {
                System.IO.File.Delete(path);
                return BadRequest(new { message = $"\"{file.FileName}\" không đọc được như ảnh hợp lệ." });
            }

            urls.Add($"/uploads/{name}");
        }

        log.LogInformation("User {UserId} uploaded {Count} image(s).", user.Id, urls.Count);
        return Ok(new { urls });
    }

    /// <summary>
    /// docs/08 §4 — identity documents. These are copies of government papers, so
    /// they never land under wwwroot: the static file middleware would hand them
    /// to anyone holding the URL, with no audit line and no role check, which
    /// defeats the whole reasoned-view flow of QT-U-11. They are stored outside
    /// the web root and served only by <see cref="IdentityFilesController"/>.
    /// </summary>
    [HttpPost("identity")]
    [RequestSizeLimit(MaxBytes * 3)]
    public async Task<ActionResult<object>> UploadIdentity([FromForm] IFormFileCollection files, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });
        if (files.Count is 0 or > 3) return BadRequest(new { message = "Cần 1–3 ảnh giấy tờ." });

        var root = IdentityFilesController.Root(env);
        Directory.CreateDirectory(root);

        var urls = new List<string>();

        foreach (var file in files)
        {
            if (file.Length == 0) continue;
            if (file.Length > MaxBytes)
                return BadRequest(new { message = $"\"{file.FileName}\" vượt quá 8MB." });
            if (!AllowedTypes.TryGetValue(file.ContentType, out var extension))
                return BadRequest(new { message = $"\"{file.FileName}\" không phải ảnh JPG/PNG/WebP/AVIF." });

            var name = $"{user.Id}-{Guid.NewGuid():N}{extension}";
            var path = Path.Combine(root, name);

            await using (var stream = System.IO.File.Create(path))
            {
                await file.CopyToAsync(stream, ct);
            }

            if (!await LooksLikeImageAsync(path, ct))
            {
                System.IO.File.Delete(path);
                return BadRequest(new { message = $"\"{file.FileName}\" không đọc được như ảnh hợp lệ." });
            }

            urls.Add($"/api/identity-files/{name}");
        }

        log.LogInformation("User {UserId} uploaded {Count} identity document(s).", user.Id, urls.Count);
        return Ok(new { urls });
    }

    /// <summary>Checks the magic bytes so a renamed file cannot masquerade as an image.</summary>
    private static async Task<bool> LooksLikeImageAsync(string path, CancellationToken ct)
    {
        var header = new byte[12];
        await using var stream = System.IO.File.OpenRead(path);
        var read = await stream.ReadAsync(header, ct);
        if (read < 12) return false;

        // JPEG
        if (header[0] == 0xFF && header[1] == 0xD8) return true;
        // PNG
        if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47) return true;
        // RIFF....WEBP
        if (header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50) return true;
        // ....ftyp (AVIF and friends)
        if (header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70) return true;

        return false;
    }
}
