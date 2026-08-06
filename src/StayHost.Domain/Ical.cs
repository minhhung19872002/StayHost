using System.Globalization;
using System.Text;

namespace StayHost.Domain;

/// <summary>One booked stretch on a calendar. Half-open, like everywhere else.</summary>
public readonly record struct IcalEvent(string Uid, DateOnly From, DateOnly To, string Summary);

/// <summary>
/// docs/01 QL-10 — the format every other booking site speaks. Only the parts
/// that carry availability: whole-day events with a start, an end and an id.
/// Times of day are thrown away deliberately; a night is a night.
/// </summary>
public static class Ical
{
    public const string ContentType = "text/calendar";

    public static string Write(string calendarName, IEnumerable<IcalEvent> events, DateTime stampUtc)
    {
        var sb = new StringBuilder();
        sb.Append("BEGIN:VCALENDAR\r\n");
        sb.Append("VERSION:2.0\r\n");
        sb.Append("PRODID:-//StayHost//Lich cho nghi//VI\r\n");
        sb.Append("CALSCALE:GREGORIAN\r\n");
        sb.Append("METHOD:PUBLISH\r\n");
        Line(sb, "X-WR-CALNAME", calendarName);

        var stamp = stampUtc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

        foreach (var e in events)
        {
            sb.Append("BEGIN:VEVENT\r\n");
            Line(sb, "UID", e.Uid);
            sb.Append("DTSTAMP:").Append(stamp).Append("\r\n");
            sb.Append("DTSTART;VALUE=DATE:").Append(Date(e.From)).Append("\r\n");
            // DTEND is exclusive in iCalendar, which is exactly a check-out date.
            sb.Append("DTEND;VALUE=DATE:").Append(Date(e.To)).Append("\r\n");
            Line(sb, "SUMMARY", e.Summary);
            sb.Append("TRANSP:OPAQUE\r\n");
            sb.Append("END:VEVENT\r\n");
        }

        sb.Append("END:VCALENDAR\r\n");
        return sb.ToString();
    }

    public static IReadOnlyList<IcalEvent> Read(string text)
    {
        var events = new List<IcalEvent>();
        if (string.IsNullOrWhiteSpace(text)) return events;

        string? uid = null, summary = null;
        DateOnly? start = null, end = null;
        var inEvent = false;

        foreach (var line in Unfold(text))
        {
            var name = NameOf(line);

            switch (name)
            {
                case "BEGIN" when Value(line) == "VEVENT":
                    inEvent = true;
                    uid = summary = null;
                    start = end = null;
                    continue;

                case "END" when Value(line) == "VEVENT":
                    if (inEvent && start is { } from)
                    {
                        // A single-day event may carry no DTEND at all.
                        var to = end ?? from.AddDays(1);
                        if (to > from)
                            events.Add(new IcalEvent(uid ?? $"{from:yyyyMMdd}-{to:yyyyMMdd}", from, to, summary ?? ""));
                    }
                    inEvent = false;
                    continue;
            }

            if (!inEvent) continue;

            switch (name)
            {
                case "UID": uid = Value(line); break;
                case "SUMMARY": summary = Unescape(Value(line)); break;
                case "DTSTART": start = ParseDate(Value(line)); break;
                case "DTEND": end = ParseDate(Value(line)); break;
            }
        }

        return events;
    }

    /// <summary>A long property is split across lines and continued with a space or tab.</summary>
    private static IEnumerable<string> Unfold(string text)
    {
        var current = new StringBuilder();

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length > 0 && (line[0] == ' ' || line[0] == '\t'))
            {
                current.Append(line[1..]);
                continue;
            }

            if (current.Length > 0) yield return current.ToString();
            current.Clear().Append(line);
        }

        if (current.Length > 0) yield return current.ToString();
    }

    private static string NameOf(string line)
    {
        var colon = line.IndexOf(':');
        var head = colon < 0 ? line : line[..colon];
        var semi = head.IndexOf(';');
        return (semi < 0 ? head : head[..semi]).Trim().ToUpperInvariant();
    }

    private static string Value(string line)
    {
        var colon = line.IndexOf(':');
        return colon < 0 ? "" : line[(colon + 1)..].Trim();
    }

    /// <summary>
    /// Accepts the two shapes that turn up in the wild: 20260915 and
    /// 20260915T140000Z. The clock is dropped either way.
    /// </summary>
    public static DateOnly? ParseDate(string value)
    {
        var text = value.Trim();
        if (text.Length >= 8 &&
            DateOnly.TryParseExact(text[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d;
        return null;
    }

    private static string Date(DateOnly d) => d.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    private static void Line(StringBuilder sb, string name, string value) =>
        sb.Append(name).Append(':').Append(Escape(value)).Append("\r\n");

    private static string Escape(string value) => value
        .Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,")
        .Replace("\r\n", "\\n").Replace("\n", "\\n");

    private static string Unescape(string value) => value
        .Replace("\\n", "\n").Replace("\\,", ",").Replace("\\;", ";").Replace("\\\\", "\\");
}
