using StayHost.Domain;

namespace StayHost.Web.Services;

/// <summary>
/// The address the outside world reaches this app on — the one that belongs in a
/// link somebody clicks from outside the browser tab, e.g. the "Xem chi tiết" line
/// of a notification email.
///
/// This used to be a constant in <see cref="NotificationService"/>, pointing at a
/// domain the platform does not own, so every notification email sent guests to a
/// dead address. Nothing failed and no log said so — the mail left, the link just
/// went nowhere.
///
/// Left empty it falls back to <c>Psp:PublicUrl</c>, which any deployment taking
/// real money has already had to set to the same address. Two settings that must
/// agree is one setting too many; the fallback keeps it at one.
/// </summary>
public class SiteSettings
{
    /// <summary>No trailing slash — callers append a leading-slash path.</summary>
    public string PublicUrl { get; set; } = "";

    /// <summary>
    /// An absolute link to <paramref name="path"/>, or null when this deployment
    /// has no public address configured — see <see cref="SiteLinks"/> for why
    /// null rather than a guess.
    /// </summary>
    public string? Absolute(string? path) => SiteLinks.Absolute(PublicUrl, path);
}
