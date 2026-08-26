using System.Collections.Concurrent;
using StayHost.Domain;

namespace StayHost.Web.Services;

/// <summary>
/// The live visitor count, kept in memory.
///
/// Deliberately not a table. This is written on nearly every request and read
/// once in a while by one operator, so a row per visit would be the busiest
/// write path in the application in exchange for a number nobody needs to keep.
/// What it costs instead is honesty about two limits, both worth knowing before
/// trusting the figure:
///
///   * It resets when the process restarts. A deploy shows an empty site for a
///     few minutes while people's next requests arrive. <see cref="Since"/> is
///     reported alongside the peak so that reads as "measured from" rather than
///     as a real drop.
///   * It counts one process. Run two containers behind the proxy and each has
///     its own half of the traffic; the numbers would need adding up, and the
///     same person could land on both. Today the stack runs a single web
///     container, and the day it does not this needs a shared store.
/// </summary>
public sealed class PresenceTracker
{
    private readonly ConcurrentDictionary<string, Presence.Visitor> _seen = new();

    /// <summary>When this process started counting.</summary>
    public DateTime Since { get; } = DateTime.UtcNow;

    private int _peak;

    /// <summary>
    /// Guard against a flood of one-request identities filling memory. Each
    /// entry is tiny, but "tiny times unbounded" is still unbounded, and the
    /// cookie filter is not a promise — a crawler that keeps cookies would walk
    /// straight past it.
    /// </summary>
    private const int MaxTracked = 50_000;

    /// <summary>
    /// Records that <paramref name="sessionId"/> is still here.
    ///
    /// <paramref name="userId"/> is what the request managed to learn: the id
    /// when a controller resolved the signed-in user, and null when it did not
    /// ask. Null does not mean signed out — most endpoints never look — so a
    /// known id is kept rather than overwritten. <paramref name="hasAuthCookie"/>
    /// is what settles it: no auth cookie on the request means genuinely signed
    /// out, and the id is dropped.
    /// </summary>
    public void Touch(string sessionId, int? userId, bool hasAuthCookie, DateTime now)
    {
        if (string.IsNullOrEmpty(sessionId)) return;

        _seen.AddOrUpdate(
            sessionId,
            _ => new Presence.Visitor(now, userId),
            (_, old) => new Presence.Visitor(
                now,
                userId ?? (hasAuthCookie ? old.UserId : null)));

        if (_seen.Count > MaxTracked) Prune(now, force: true);
    }

    /// <summary>The count as of <paramref name="now"/>, pruning what it passes.</summary>
    public Snapshot Read(DateTime now)
    {
        Prune(now, force: false);

        var tally = Presence.Count(_seen.Values, now);

        // Interlocked rather than "if greater, assign": Read can run on two
        // requests at once, and a lost update here would quietly under-report
        // the busiest moment of the day.
        int seen;
        while (tally.Total > (seen = Volatile.Read(ref _peak)))
            if (Interlocked.CompareExchange(ref _peak, tally.Total, seen) == seen) break;

        return new Snapshot(
            tally.Total, tally.SignedIn, tally.Guests,
            Volatile.Read(ref _peak), Since, (int)Presence.Window.TotalMinutes, now);
    }

    private void Prune(DateTime now, bool force)
    {
        foreach (var (key, visitor) in _seen)
            if (Presence.Stale(visitor.LastSeen, now)) _seen.TryRemove(key, out _);

        // Still over the cap after dropping the stale ones: the traffic is not
        // what this was built for. Keeping the most recent is the least wrong
        // thing to do, and the count is understated rather than invented.
        if (!force || _seen.Count <= MaxTracked) return;

        foreach (var (key, _) in _seen.OrderBy(kv => kv.Value.LastSeen).Take(_seen.Count - MaxTracked))
            _seen.TryRemove(key, out _);
    }

    /// <param name="Total">Distinct visitors seen inside the window.</param>
    /// <param name="SignedIn">Of those, how many have an account open.</param>
    /// <param name="Guests">The rest — most of a booking site's traffic.</param>
    /// <param name="Peak">The highest Total since <paramref name="Since"/>.</param>
    /// <param name="Since">When this process started counting.</param>
    /// <param name="WindowMinutes">How far back "here" reaches.</param>
    /// <param name="At">When this reading was taken.</param>
    public record Snapshot(
        int Total, int SignedIn, int Guests,
        int Peak, DateTime Since, int WindowMinutes, DateTime At);
}
