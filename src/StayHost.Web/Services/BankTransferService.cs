using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;

namespace StayHost.Web.Services;

/// <summary>
/// docs/07 §2.3 — the half of a bank transfer the platform does on its own.
///
/// A guest who pays by VietQR leaves the site entirely: they open a banking app,
/// send the money, and nothing comes back to say so. The only trace is a line on
/// a statement, and the only thing tying that line to a booking is the reference
/// in the memo. So this class does three things and nothing else — say what is
/// being waited for, read a statement against it, and confirm the bookings whose
/// money is in it.
///
/// The confirmations go through the same methods a card goes through
/// (<see cref="PaymentCompletion.ConfirmAsync"/>, <c>ConfirmTransferAsync</c>),
/// because docs/00 §6.8's rule about one place for money applies here too: a
/// second way to confirm a booking is a second way for the ledger to drift.
/// </summary>
public class BankTransferService(
    StayHostDbContext db,
    PaymentCompletion completion,
    ExperienceService experiences,
    ServiceMarketService services,
    ILogger<BankTransferService> log)
{
    /// <summary>One line of a statement, after an operator has said which column is which.</summary>
    public readonly record struct Line(string BankReference, decimal Amount, string Description);

    public sealed record Row(
        string BankReference,
        decimal Amount,
        string Description,
        BankTransfers.Verdict Verdict,
        string? MatchedReference,
        decimal Expected,
        string Explanation)
    {
        public bool NeedsSomebody => Verdict is not (BankTransfers.Verdict.Paid or BankTransfers.Verdict.AlreadySeen);
    }

    public sealed record Import(IReadOnlyList<Row> Rows)
    {
        public int Settled => Rows.Count(r => r.Verdict == BankTransfers.Verdict.Paid);
        public int Pending => Rows.Count(r => r.NeedsSomebody);
        public int Skipped => Rows.Count(r => r.Verdict == BankTransfers.Verdict.AlreadySeen);
    }

    /* ------------------------------------------------- what is being waited for */

    /// <summary>
    /// Every reference the platform is holding something for right now, and what
    /// each is owed. Three product lines, one dictionary, because a memo says
    /// nothing about which of them it belongs to.
    /// </summary>
    public async Task<Dictionary<string, decimal>> AwaitedAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var cutoff = now - BankTransfers.Window;
        var awaited = new Dictionary<string, decimal>();

        var stays = await db.Bookings
            .Where(b => b.Status == BookingStatus.PendingPayment
                        && b.Payment != null && b.Payment.Method == "vietqr"
                        && b.HoldExpiresAt != null && b.HoldExpiresAt > now)
            .Select(b => new { b.Reference, b.Total })
            .ToListAsync(ct);

        foreach (var s in stays) awaited[s.Reference] = s.Total;

        var tickets = await db.ExperienceBookings
            .Where(b => b.Status == ExperienceBookingStatus.AwaitingPayment && b.CreatedAt > cutoff)
            .Select(b => new { b.Reference, b.Total })
            .ToListAsync(ct);

        foreach (var t in tickets) awaited[t.Reference] = t.Total;

        var jobs = await db.ServiceBookings
            .Where(b => b.Status == ServiceBookingStatus.AwaitingPayment && b.CreatedAt > cutoff)
            .Select(b => new { b.Reference, b.Total })
            .ToListAsync(ct);

        foreach (var j in jobs) awaited[j.Reference] = j.Total;

        return awaited;
    }

    /// <summary>
    /// References whose window ran out recently. Money for one of these is real
    /// money against a booking that has already given its dates or seats away,
    /// so it never confirms anything — it becomes a line somebody has to answer.
    /// </summary>
    public async Task<Dictionary<string, decimal>> LapsedAsync(CancellationToken ct)
    {
        var since = DateTime.UtcNow - BankTransfers.LateWindow;
        var lapsed = new Dictionary<string, decimal>();

        // Anchored on when the booking was made, not on HoldExpiresAt: leaving
        // PendingPayment nulls the hold (BookingLifecycle.Transition), so a
        // predicate on it would match nothing and this whole verdict would
        // never fire. The two differ by the length of the window, which is
        // hours against a limit measured in days.
        var stays = await db.Bookings
            .Where(b => b.Status == BookingStatus.PaymentFailed
                        && b.Payment != null && b.Payment.Method == "vietqr"
                        && b.CreatedAt > since)
            .Select(b => new { b.Reference, b.Total })
            .ToListAsync(ct);

        foreach (var s in stays) lapsed[s.Reference] = s.Total;

        var tickets = await db.ExperienceBookings
            .Where(b => b.Status == ExperienceBookingStatus.PaymentExpired && b.CreatedAt > since)
            .Select(b => new { b.Reference, b.Total })
            .ToListAsync(ct);

        foreach (var t in tickets) lapsed[t.Reference] = t.Total;

        var jobs = await db.ServiceBookings
            .Where(b => b.Status == ServiceBookingStatus.PaymentExpired && b.CreatedAt > since)
            .Select(b => new { b.Reference, b.Total })
            .ToListAsync(ct);

        foreach (var j in jobs) lapsed[j.Reference] = j.Total;

        return lapsed;
    }

    /* --------------------------------------------------------------- importing */

    /// <summary>
    /// Reads a statement against what is being waited for, writes down every
    /// line, and confirms the bookings whose money is in it.
    ///
    /// Each line is saved before its booking is confirmed. That order is the
    /// point: <c>bank_credits.BankReference</c> is unique, so a line that has
    /// been imported before collides on the way in and never reaches the
    /// confirmation. Re-importing the same statement is therefore harmless, and
    /// so is importing one that overlaps yesterday's.
    /// </summary>
    public async Task<Import> ImportAsync(int adminUserId, IReadOnlyList<Line> lines, CancellationToken ct)
    {
        var awaited = await AwaitedAsync(ct);
        var lapsed = await LapsedAsync(ct);

        var seen = (await db.BankCredits
                .Where(c => lines.Select(l => l.BankReference).Contains(c.BankReference))
                .Select(c => c.BankReference)
                .ToListAsync(ct))
            .ToHashSet();

        var rows = new List<Row>(lines.Count);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line.BankReference))
            {
                // docs/07 §2.3 — a credit with no id of its own cannot be told
                // apart from the same credit tomorrow, so it is refused rather
                // than guessed at.
                rows.Add(new Row("", line.Amount, line.Description, BankTransfers.Verdict.Unidentified,
                    null, 0, "Dòng này không có mã giao dịch của ngân hàng."));
                continue;
            }

            var credit = new BankTransfers.Credit(line.BankReference.Trim(), line.Amount, line.Description ?? "");
            var outcome = BankTransfers.Judge(credit, awaited, seen, lapsed);

            rows.Add(new Row(
                credit.BankReference, credit.Amount, credit.Description,
                outcome.Verdict, outcome.Booking, outcome.Expected, BankTransfers.Explain(outcome)));

            if (outcome.Verdict == BankTransfers.Verdict.AlreadySeen) continue;

            var record = new BankCredit
            {
                BankReference = credit.BankReference,
                Amount = credit.Amount,
                Description = credit.Description,
                Verdict = outcome.Verdict,
                MatchedReference = outcome.Booking,
                Expected = outcome.Expected,
                ImportedByUserId = adminUserId,
                // A clean match needs nobody; the other verdicts wait for a person.
                ResolvedAt = BankTransfers.Settles(outcome.Verdict) ? DateTime.UtcNow : null,
                ResolvedByUserId = BankTransfers.Settles(outcome.Verdict) ? adminUserId : null
            };

            db.BankCredits.Add(record);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Another import claimed this credit between the read above and
                // here. It is already recorded, so this one must not confirm.
                db.ChangeTracker.Clear();
                continue;
            }

            seen.Add(credit.BankReference);

            if (!BankTransfers.Settles(outcome.Verdict)) continue;

            // The booking is no longer waiting for anything, so a second credit
            // carrying the same memo cannot confirm it twice.
            awaited.Remove(outcome.Booking!);
            await SettleAsync(outcome.Booking!, ct);
        }

        var result = new Import(rows);

        log.LogInformation(
            "Nhập sao kê: {Total} dòng, {Settled} khớp đơn, {Pending} cần xử lý, {Skipped} đã nhập trước đó.",
            rows.Count, result.Settled, result.Pending, result.Skipped);

        return result;
    }

    /// <summary>
    /// The money is in. Which of the three lines it belongs to is read off the
    /// reference's own prefix, and each is confirmed by its own existing path.
    /// </summary>
    private async Task SettleAsync(string reference, CancellationToken ct)
    {
        if (reference.StartsWith("SH", StringComparison.Ordinal))
        {
            var booking = await db.Bookings
                .Include(b => b.Payment).Include(b => b.Events)
                .Include(b => b.Listing!).ThenInclude(l => l.Images)
                .FirstOrDefaultAsync(b => b.Reference == reference, ct);

            if (booking is null || booking.Status != BookingStatus.PendingPayment) return;

            var price = await completion.QuoteFromRecordAsync(booking, ct);
            if (price is null) return;

            await completion.ConfirmAsync(
                booking, price, booking.Total, partial: false,
                DateOnly.FromDateTime(DateTime.UtcNow), booking.GuestUserId ?? 0, "vietqr", null, ct);

            log.LogInformation("Đơn {Reference} đã xác nhận sau khi tiền chuyển khoản về.", reference);
            return;
        }

        if (reference.StartsWith("XP", StringComparison.Ordinal))
        {
            var ticket = await db.ExperienceBookings.FirstOrDefaultAsync(b => b.Reference == reference, ct);
            if (ticket is not null) await experiences.ConfirmTransferAsync(ticket, ct);
            return;
        }

        var job = await db.ServiceBookings.FirstOrDefaultAsync(b => b.Reference == reference, ct);
        if (job is not null) await services.ConfirmTransferAsync(job, ct);
    }

    /* -------------------------------------------------------------- follow-up */

    /// <summary>Credits nobody has dealt with yet — the queue this screen exists for.</summary>
    public Task<List<BankCredit>> OpenAsync(CancellationToken ct) =>
        db.BankCredits
            .Where(c => c.ResolvedAt == null)
            .OrderByDescending(c => c.ImportedAt)
            .Take(200)
            .ToListAsync(ct);

    /// <summary>
    /// A person has dealt with a line — refunded it, chased the guest, corrected
    /// a reference by hand. The note is what they did, and it is kept: this row
    /// is the platform's only record of where that money went.
    /// </summary>
    public async Task<bool> ResolveAsync(long id, int adminUserId, string note, CancellationToken ct)
    {
        var credit = await db.BankCredits.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (credit is null || credit.ResolvedAt is not null) return false;

        credit.ResolvedAt = DateTime.UtcNow;
        credit.ResolvedByUserId = adminUserId;
        credit.ResolutionNote = note.Trim();
        await db.SaveChangesAsync(ct);

        return true;
    }
}
