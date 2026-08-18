using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;

namespace StayHost.Web.Services;

/// <summary>
/// docs/07 §15.4 — checking the bank's own record of what left the account
/// against the transfers this platform said it made.
///
/// Until this existed, the only thing standing between "the bank paid the host"
/// and the ledger saying so was a person pressing a button. That button is the
/// single place a payout is posted, so a mis-press writes money out of StayHost
/// that never left the bank, and the books balance either way. docs/07 §7 asks
/// exactly this question of the gateways and answers it by comparing against
/// their list; this is the same answer for the outgoing side.
///
/// It only ever confirms. A line the bank shows for the right reference and the
/// right amount settles the batch — the same call the button makes, so the
/// ledger entry, the notification and the audit trail are identical. Anything
/// else is reported and left for a person: this cannot mark a transfer failed,
/// because a transfer missing from today's statement is far more often a
/// statement that has not caught up than a transfer the bank refused.
/// </summary>
public class PayoutStatementService(StayHostDbContext db, PayoutService payouts)
{
    /// <summary>One row of the statement, already split into columns by a person.</summary>
    public readonly record struct Line(string? BankReference, decimal Amount, string? Description);

    public readonly record struct Row(
        string BankReference, decimal Amount, string Description,
        PayoutStatements.Verdict Verdict, string? MatchedReference, decimal Expected,
        string Explanation);

    public readonly record struct Import(int Settled, int Pending, int Skipped, IReadOnlyList<Row> Rows);

    /// <summary>
    /// Reads a statement and settles what it proves.
    /// </summary>
    /// <param name="actor">Recorded on every batch this settles, as the button does.</param>
    public async Task<Import> ImportAsync(
        string actor, string? note, IReadOnlyList<Line> lines, CancellationToken ct)
    {
        // Only transfers already in somebody's hands can be confirmed by a
        // statement. A Pending batch has not been downloaded yet, so a debit
        // claiming to be one is a statement about a transfer nobody has made.
        var open = await db.PayoutBatches
            .Where(b => b.Status == PayoutBatchStatus.Exported || b.Status == PayoutBatchStatus.Pending)
            .Select(b => new { b.Id, b.Reference, b.Amount })
            .ToListAsync(ct);

        var outstanding = open.ToDictionary(b => b.Reference, b => b.Amount);
        var idOf = open.ToDictionary(b => b.Reference, b => b.Id);

        // Everything already confirmed, so re-pasting the same day is boring
        // rather than dangerous. Restricted to the references this statement
        // actually mentions rather than every payout ever made.
        var settledBefore = (await db.PayoutBatches
                .Where(b => b.Status == PayoutBatchStatus.Settled)
                .Select(b => b.Reference)
                .ToListAsync(ct))
            .ToHashSet();

        var rows = new List<Row>(lines.Count);
        int settled = 0, pending = 0, skipped = 0;

        foreach (var line in lines)
        {
            var debit = new PayoutStatements.Debit(
                (line.BankReference ?? "").Trim(),
                line.Amount,
                line.Description ?? "");

            var outcome = PayoutStatements.Judge(debit, outstanding, settledBefore);

            if (PayoutStatements.Settles(outcome.Verdict) && outcome.Batch is { } reference)
            {
                // The same call the operator's button makes. If it returns false
                // the batch was settled by someone else between the query above
                // and here, which is not an error — it is the answer.
                var posted = await payouts.SettleAsync(idOf[reference], actor, note, ct);

                if (posted)
                {
                    settled++;
                    // Within one import a reference must not settle twice, and
                    // a second line naming it should read as a duplicate.
                    outstanding.Remove(reference);
                    settledBefore.Add(reference);
                }
                else
                {
                    skipped++;
                    outcome = outcome with { Verdict = PayoutStatements.Verdict.AlreadySeen };
                }
            }
            else if (outcome.Verdict == PayoutStatements.Verdict.AlreadySeen)
            {
                skipped++;
            }
            else
            {
                pending++;
            }

            rows.Add(new Row(
                debit.BankReference, debit.Amount, debit.Description,
                outcome.Verdict, outcome.Batch, outcome.Expected,
                PayoutStatements.Explain(outcome)));
        }

        return new Import(settled, pending, skipped, rows);
    }

    /// <summary>
    /// docs/07 §15.4 — transfers the platform believes it made and the bank has
    /// not confirmed, oldest first.
    ///
    /// The other half of reconciliation, and the half a statement cannot show:
    /// what is missing from it. A batch downloaded days ago and never seen on a
    /// statement is either a file nobody uploaded to the bank or a transfer that
    /// silently failed, and both leave a host unpaid while the console looks
    /// tidy.
    /// </summary>
    public async Task<IReadOnlyList<PayoutBatch>> AwaitingBankAsync(CancellationToken ct) =>
        await db.PayoutBatches
            .Include(b => b.Host!).ThenInclude(h => h.User)
            .Where(b => b.Status == PayoutBatchStatus.Exported)
            .OrderBy(b => b.DueOn)
            .ToListAsync(ct);
}
