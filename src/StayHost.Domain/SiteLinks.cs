namespace StayHost.Domain;

/// <summary>
/// What a link inside an outgoing email is allowed to look like.
///
/// A notification carries a path — <c>/trips/12</c> — because in the browser that
/// is all it needs. An email is read outside that tab, so the path alone is not
/// clickable and the platform's own address has to be put in front of it.
///
/// That address used to be written into the source, and it named a domain the
/// platform does not own: every notification email went out with a link to
/// nowhere, and nothing anywhere said so. The mail left the queue, the log was
/// clean, the tests were green. So the rule here is deliberately blunt — either
/// the address is configured and the link is absolute, or there is no link at
/// all. Guessing a host is what caused the bug.
/// </summary>
public static class SiteLinks
{
    /// <summary>
    /// <paramref name="path"/> prefixed with <paramref name="baseUrl"/>, or null
    /// when there is nothing to prefix it with. Null means the caller leaves the
    /// line out: a reader who cannot click is better served by no link than by a
    /// bare path or an invented domain.
    /// </summary>
    public static string? Absolute(string? baseUrl, string? path)
    {
        var root = (baseUrl ?? "").Trim().TrimEnd('/');
        if (root.Length == 0) return null;

        var tail = (path ?? "").Trim();
        if (tail.Length == 0) return null;

        // The caller's paths start with "/", but a stored one might not, and a
        // link glued together as "https://staylio.vntrips/12" is worse than the
        // dead one it replaced.
        return tail[0] == '/' ? root + tail : $"{root}/{tail}";
    }
}
