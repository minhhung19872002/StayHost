using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;

namespace StayHost.Web.Services;

/// <summary>
/// docs/07 §12 — actually sends hosts their money.
///
/// Until this existed a payout was scheduled and then sat there: every booking
/// ever completed still read "Scheduled", and the five hold reasons of §12.4
/// were a list nobody consulted. The transfer itself goes through the same
/// stand-in as a card charge — there is no bank behind this build — but every
/// decision around it is real.
/// </summary>
public class PayoutService(
    StayHostDbContext db, PaymentGateway gateway, NotificationService notifications, ILogger<PayoutService> log)
{
    public sealed record Result(int Paid, int Held, int Failed)
    {
        public bool Any => Paid + Held + Failed > 0;
        public override string ToString() => $"{Paid} đã chuyển, {Held} tạm giữ, {Failed} thất bại";
    }

    public async Task<Result> SweepAsync(CancellationToken ct, DateOnly? asOf = null)
    {
        var today = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;

        var due = await db.Payments
            .Where(p => p.PayoutStatus != PayoutStatus.Paid
                        && p.PayoutDueOn != null && p.PayoutDueOn <= today
                        && p.Status == PaymentStatus.Captured)
            .Include(p => p.Booking!).ThenInclude(b => b.Listing!).ThenInclude(l => l.Host!).ThenInclude(h => h.User)
            .Take(200)
            .ToListAsync(ct);

        var paid = 0;
        var held = 0;
        var failed = 0;

        // Everything that survives the holds, kept per host so one bank transfer
        // can carry the lot (docs/07 §12.3).
        var payable = new Dictionary<int, (HostProfile Host, List<Payment> Payments)>();

        foreach (var payment in due)
        {
            var booking = payment.Booking;
            var host = booking?.Listing?.Host;
            if (booking is null || host is null) continue;

            // docs/07 §12.5 — a failed transfer waits its turn before the next go.
            if (payment.PayoutAttempts > 0 && payment.PayoutLastAttemptOn is { } last)
            {
                var next = Payouts.NextAttemptOn(last, payment.PayoutAttempts);
                if (next is null || next > today) continue;
            }

            var reason = Payouts.HoldReason(await ConditionsAsync(booking, host, payment.HostPayout, ct), now);

            if (reason != PayoutHoldReason.None)
            {
                if (payment.PayoutStatus != PayoutStatus.OnHold || payment.PayoutHoldReason != reason)
                {
                    payment.PayoutStatus = PayoutStatus.OnHold;
                    payment.PayoutHoldReason = reason;

                    await notifications.QueueWithEmailAsync(host.User, NotificationKind.PayoutSent,
                        "Khoản chuyển tiền đang tạm giữ",
                        $"Đơn {booking.Reference}: {Payouts.HoldLabel(reason)}.",
                        "/hosting", ct);
                }
                held++;
                continue;
            }

            if (!payable.TryGetValue(host.Id, out var batch))
            {
                batch = (host, []);
                payable[host.Id] = batch;
            }
            batch.Payments.Add(payment);
        }

        foreach (var (hostId, batch) in payable)
        {
            var (host, payments) = batch;
            // A host normally gets one transfer a day; anything that only became
            // payable after the first sweep is a transfer of its own.
            var soFar = await db.Payments
                .Where(p => p.PaidOutAt != null && p.PayoutLastAttemptOn == today
                            && p.Booking!.Listing!.HostId == hostId)
                .Select(p => p.PayoutReference)
                .Distinct()
                .CountAsync(ct);

            var reference = Payouts.BatchReference(hostId, today, soFar + 1);
            var gross = payments.Sum(p => p.HostPayout);
            var deduction = Payouts.Deduct(gross, host.OwedToPlatform);

            foreach (var payment in payments)
            {
                payment.PayoutAttempts++;
                payment.PayoutLastAttemptOn = today;
            }

            // One transfer for the batch — the fee is charged per transfer, not
            // per booking, which is the whole reason for grouping.
            var transfer = gateway.Charge(deduction.Transfer, "bank-transfer", host.PayoutAccountLast4);

            if (!transfer.Ok)
            {
                failed += payments.Count;

                foreach (var payment in payments)
                {
                    payment.PayoutStatus = PayoutStatus.OnHold;
                    payment.PayoutHoldReason = PayoutHoldReason.None;
                }

                if (payments.Any(p => Payouts.OutOfAttempts(p.PayoutAttempts)))
                {
                    await notifications.QueueWithEmailAsync(host.User, NotificationKind.PayoutSent,
                        "Không chuyển được tiền cho bạn", Payouts.ExhaustedNotice(), "/hosting", ct);
                }
                continue;
            }

            // The debt comes off the batch as a whole, so it is spread across the
            // bookings in it — otherwise the first booking of the day would wear
            // the entire deduction and its report line would read as nonsense.
            var left = deduction.Applied;

            foreach (var payment in payments)
            {
                payment.PayoutStatus = PayoutStatus.Paid;
                payment.PayoutHoldReason = PayoutHoldReason.None;
                payment.PaidOutAt = now;
                payment.PayoutReference = reference;

                var share = payment == payments[^1] ? left : Math.Min(left,
                    Math.Round(deduction.Applied * payment.HostPayout / gross, 0, MidpointRounding.AwayFromZero));
                left -= share;
                payment.PayoutDeducted = share;

                db.LedgerEntries.AddRange(Ledger.RecoverFromHost(payment.Booking!, share, now));
                db.LedgerEntries.AddRange(Ledger.PayoutHost(payment.Booking!, payment.HostPayout - share, now));
                paid++;
            }

            host.OwedToPlatform = deduction.StillOwed;

            // The money arrives as one line on the statement, so say so — and say
            // how many bookings made it up, because that is the question a host
            // asks next (docs/07 §12.3, "báo cáo vẫn tách theo từng đơn").
            var what = payments.Count == 1
                ? $"đơn {payments[0].Booking!.Reference}"
                : $"{payments.Count} đơn";

            var note = deduction.Applied > 0
                ? " " + Payouts.DeductionNote(deduction.Applied, deduction.StillOwed)
                : "";

            await notifications.QueueWithEmailAsync(host.User, NotificationKind.PayoutSent,
                "Đã chuyển tiền cho bạn",
                $"{deduction.Transfer:#,##0}₫ cho {what} đang trên đường về tài khoản của bạn " +
                $"(mã chuyển {reference}).{note}",
                "/hosting/earnings", ct);
        }

        if (paid + held + failed > 0)
        {
            await db.SaveChangesAsync(ct);
            log.LogInformation("Chuyển tiền {Today}: {Paid} đã chuyển, {Held} tạm giữ, {Failed} thất bại.",
                today, paid, held, failed);
        }

        return new Result(paid, held, failed);
    }

    /// <summary>
    /// docs/01 TC-03, docs/07 §12.3 — the monthly payout for long stays. Each due
    /// instalment is paid on its own, held by the same conditions as any payout,
    /// with the same retry backoff. When the last one settles, the booking's
    /// payment is marked paid so the reports agree.
    /// </summary>
    public async Task<Result> InstallmentSweepAsync(CancellationToken ct, DateOnly? asOf = null)
    {
        var today = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;

        var due = await db.PayoutInstallments
            .Where(i => !i.Paid && i.DueOn <= today)
            .Include(i => i.Booking!).ThenInclude(b => b.Listing!).ThenInclude(l => l.Host!).ThenInclude(h => h.User)
            .Include(i => i.Booking!).ThenInclude(b => b.Payment)
            .Take(200)
            .ToListAsync(ct);

        int paid = 0, held = 0, failed = 0;

        foreach (var inst in due)
        {
            var booking = inst.Booking;
            var host = booking?.Listing?.Host;
            if (booking is null || host is null || booking.Payment?.Status != PaymentStatus.Captured) continue;

            // Same retry cadence as an ordinary payout.
            if (inst.Attempts > 0 && inst.LastAttemptOn is { } last)
            {
                var next = Payouts.NextAttemptOn(last, inst.Attempts);
                if (next is null || next > today) continue;
            }

            var reason = Payouts.HoldReason(await ConditionsAsync(booking, host, inst.Amount, ct), now);
            if (reason != PayoutHoldReason.None) { held++; continue; }

            inst.Attempts++;
            inst.LastAttemptOn = today;

            var deduction = Payouts.Deduct(inst.Amount, host.OwedToPlatform);
            var transfer = gateway.Charge(deduction.Transfer, "bank-transfer", host.PayoutAccountLast4);
            if (!transfer.Ok) { failed++; continue; }

            host.OwedToPlatform = deduction.StillOwed;
            inst.Paid = true;
            inst.PaidAt = now;

            db.LedgerEntries.AddRange(Ledger.RecoverFromHost(booking, deduction.Applied, now));
            db.LedgerEntries.AddRange(Ledger.PayoutHost(booking, inst.Amount - deduction.Applied, now));
            paid++;

            // When the final instalment lands, the payment itself is settled so the
            // finance report stops showing this stay as still-to-pay.
            var remaining = await db.PayoutInstallments
                .CountAsync(x => x.BookingId == booking.Id && !x.Paid && x.Id != inst.Id, ct);
            if (remaining == 0 && booking.Payment is not null)
            {
                booking.Payment.PayoutStatus = PayoutStatus.Paid;
                booking.Payment.PaidOutAt = now;
            }

            await notifications.QueueWithEmailAsync(host.User, NotificationKind.PayoutSent,
                "Đã chuyển đợt tiền theo tháng",
                $"{deduction.Transfer:#,##0}₫ cho đơn {booking.Reference} (đơn dài, trả theo tháng).",
                "/hosting/earnings", ct);
        }

        if (paid + held + failed > 0)
        {
            await db.SaveChangesAsync(ct);
            log.LogInformation("Trả theo tháng {Today}: {Paid} đợt đã chuyển, {Held} giữ, {Failed} lỗi.",
                today, paid, held, failed);
        }

        return new Result(paid, held, failed);
    }

    /// <summary>docs/07 §12.4 — the five questions, answered from live data.</summary>
    private async Task<Payouts.Conditions> ConditionsAsync(
        Booking booking, HostProfile host, decimal payable, CancellationToken ct)
    {
        // Still running, either way: a case that has been ruled on or dropped
        // has stopped being a reason to sit on somebody's money.
        var disputed = await db.ResolutionCases.AnyAsync(
            c => c.BookingId == booking.Id
                 && (c.Status == ResolutionStatus.AwaitingResponse || c.Status == ResolutionStatus.Disputed), ct);

        if (!disputed)
        {
            disputed = await db.ShieldClaims.AnyAsync(
                c => c.BookingId == booking.Id
                     && (c.Status == ShieldStatus.Open
                         || c.Status == ShieldStatus.UnderReview
                         || c.Status == ShieldStatus.Appealed), ct);
        }

        var chargeback = await db.Chargebacks
            .Where(c => c.BookingId == booking.Id)
            .Select(c => c.Status)
            .ToListAsync(ct);

        return new Payouts.Conditions(
            HasOpenDispute: disputed,
            HasChargeback: chargeback.Any(Chargebacks.HoldsPayout),
            ListingSuspended: booking.Listing is { IsPublished: false },
            AccountVerified: host.PayoutAccountVerified,
            AccountChangedAt: host.PayoutAccountChangedAt,
            OwedToPlatform: host.OwedToPlatform,
            Payable: payable,
            // docs/08 §5.2/§6 — read from the account itself every sweep, so an
            // admin hold is never undone by this recomputation.
            AccountUnderReview: host.User is { } hu
                && (hu.IsSuspended || hu.IsBanned
                    || Restrictions.Has(hu.RestrictionMask, RestrictionKind.PayoutsHeld)));
    }
}
