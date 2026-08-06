using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Controllers;

namespace StayHost.Web.Services;

/// <summary>
/// docs/01 ĐP-06 and docs/03 §1 — takes the rest of a part-paid booking on its
/// date, tries again for 72 hours if the card refuses, and cancels the booking
/// under the guest's own policy if it never goes through.
/// </summary>
public class BalanceCollector(
    StayHostDbContext db,
    PaymentGateway gateway,
    NotificationService notifications,
    ILogger<BalanceCollector> log)
{
    public sealed class Result
    {
        public int Collected { get; set; }
        public int Refused { get; set; }
        public int Cancelled { get; set; }

        public bool Any => Collected + Refused + Cancelled > 0;

        public override string ToString() =>
            $"{Collected} thu đủ, {Refused} bị từ chối, {Cancelled} huỷ vì không thu được";
    }

    public async Task<Result> SweepAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var result = new Result();

        var due = await db.Bookings
            .Include(b => b.Payment).Include(b => b.Events).Include(b => b.Listing)
            .Where(b => b.BalanceDue > 0
                        && (b.BalanceStatus == BalanceStatus.Scheduled || b.BalanceStatus == BalanceStatus.Retrying)
                        && b.BalanceDueOn != null && b.BalanceDueOn <= today
                        && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.InProgress))
            .ToListAsync(ct);

        foreach (var booking in due)
        {
            // A refusal is retried on a schedule rather than on every tick.
            if (booking.BalanceStatus == BalanceStatus.Retrying)
            {
                var first = booking.BalanceFirstFailedAt ?? now;

                if (PartialPayment.GaveUp(first, now))
                {
                    await GiveUpAsync(booking, ct);
                    result.Cancelled++;
                    continue;
                }

                if (!PartialPayment.ShouldRetry(first, booking.BalanceLastAttemptAt ?? first, now)) continue;
            }

            var attempt = gateway.Charge(
                booking.BalanceDue, booking.Payment?.Method ?? "card", booking.Payment?.CardLast4);

            booking.BalanceAttempts++;
            booking.BalanceLastAttemptAt = now;

            if (!attempt.Ok)
            {
                booking.BalanceFirstFailedAt ??= now;
                booking.BalanceStatus = BalanceStatus.Retrying;
                result.Refused++;

                await NotifyAsync(booking, "Chưa thu được phần còn lại",
                    $"Chúng tôi chưa thu được {booking.BalanceDue:#,##0}₫ của đơn {booking.Reference}. " +
                    "Vui lòng cập nhật thẻ trong vòng 72 giờ, nếu không đơn sẽ bị huỷ.", ct);
                continue;
            }

            db.LedgerEntries.AddRange(Ledger.CollectBalance(booking, booking.BalanceDue, now));
            db.BookingEvents.Add(BookingLifecycle.Note(
                booking, "system", $"Đã thu nốt {booking.BalanceDue:#,##0}₫ theo lịch."));

            booking.DepositPaid += booking.BalanceDue;
            booking.BalanceDue = 0;
            booking.BalanceStatus = BalanceStatus.Paid;
            booking.BalanceFirstFailedAt = null;
            result.Collected++;

            await NotifyAsync(booking, "Đã thu nốt phần còn lại",
                $"Đơn {booking.Reference} đã được thanh toán đủ.", ct);
        }

        if (result.Any) await db.SaveChangesAsync(ct);
        return result;
    }

    /// <summary>
    /// The 72 hours are up. The booking is cancelled as the guest's own policy
    /// would have it, so the host keeps whatever that policy entitles them to
    /// rather than losing the dates for nothing.
    /// </summary>
    private async Task GiveUpAsync(Booking booking, CancellationToken ct)
    {
        var yearAgo = DateTime.UtcNow.AddYears(-1);
        var used = booking.GuestUserId is null
            ? 0
            : await db.Bookings.CountAsync(b =>
                b.GuestUserId == booking.GuestUserId &&
                b.Status == BookingStatus.CancelledByGuest &&
                b.RefundedAmount > 0 &&
                b.CreatedAt >= yearAgo, ct);

        var outcome = Cancellation.Refund(new Cancellation.Context
        {
            Booking = booking,
            Now = DateTime.UtcNow,
            By = CancelledBy.Guest,
            ServiceFeeRefundsUsed = used
        });

        BookingsController.PostCancellation(
            db, booking, outcome, CancelledBy.Guest, "Không thu được phần còn lại trong 72 giờ.");

        log.LogInformation("Booking {Reference} cancelled: balance never collected.", booking.Reference);

        await NotifyAsync(booking, "Đơn đã bị huỷ",
            $"Đơn {booking.Reference} bị huỷ vì chưa thu được phần còn lại sau 72 giờ. " +
            $"Số tiền hoàn lại: {outcome.Amount:#,##0}₫.", ct);
    }

    private async Task NotifyAsync(Booking booking, string title, string body, CancellationToken ct)
    {
        if (booking.GuestUserId is not { } guestId) return;

        var guest = await db.Users.FirstOrDefaultAsync(u => u.Id == guestId, ct);
        await notifications.QueueWithEmailAsync(
            guest, NotificationKind.System, title, body, $"/trips/{booking.Id}", ct);
    }
}
