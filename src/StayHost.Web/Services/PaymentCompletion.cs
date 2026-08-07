using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;

namespace StayHost.Web.Services;

/// <summary>
/// Everything that happens once money has actually moved for a booking.
///
/// It lives here rather than inside the pay endpoint because docs/07 §5 says the
/// platform must confirm a booking it later discovers was paid — the guest whose
/// connection dropped between the bank taking the money and the reply reaching
/// them. That case arrives from a background sweep, not from a request, and both
/// have to do exactly the same thing or the two paths drift.
/// </summary>
public class PaymentCompletion(
    StayHostDbContext db, NotificationService notifications, ThreadMessenger messenger,
    WalletService wallet, RiskWatch risk)
{
    public async Task ConfirmAsync(
        Booking booking, Pricing.Breakdown price, decimal charged, bool partial,
        DateOnly today, int guestUserId, string? method, string? cardLast4, CancellationToken ct)
    {
        db.BookingEvents.Add(BookingLifecycle.Transition(
            booking, BookingStatus.Confirmed, $"guest:{guestUserId}",
            partial
                ? $"Đã đặt cọc {charged:#,##0}₫ trên tổng {price.Total:#,##0}₫."
                : "Thanh toán thành công."));

        booking.DepositPaid = charged;
        booking.BalanceDue = price.Total - charged;
        booking.BalanceDueOn = partial ? PartialPayment.BalanceDueOn(booking.CheckIn, today) : null;
        booking.BalanceStatus = partial ? BalanceStatus.Scheduled : BalanceStatus.None;

        if (booking.Payment is not null)
        {
            booking.Payment.Status = PaymentStatus.Captured;
            booking.Payment.CapturedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(method)) booking.Payment.Method = method;
            if (!string.IsNullOrWhiteSpace(cardLast4)) booking.Payment.CardLast4 = cardLast4;
        }

        if (booking.CreditUsed > 0)
        {
            wallet.Add(guestUserId, -booking.CreditUsed, CreditReason.Spent,
                $"Dùng cho đơn {booking.Reference}", booking.Id);
        }

        db.LedgerEntries.AddRange(
            Ledger.CaptureBooking(booking, price, DateTime.UtcNow, charged, booking.CreditUsed));
        await db.SaveChangesAsync(ct);

        var listing = booking.Listing!;

        // docs/01 TN-04 — the conversation carries the order's own milestones.
        await messenger.PostAsync(booking,
            $"Đơn {booking.Reference} đã được xác nhận: {booking.CheckIn:dd/MM} – {booking.CheckOut:dd/MM}, " +
            $"{booking.Nights} đêm, {booking.Guests} khách.", ct);

        var hostUser = await db.Users.FirstOrDefaultAsync(u => u.HostProfile!.Id == listing.HostId, ct);

        await notifications.QueueWithEmailAsync(hostUser, NotificationKind.BookingConfirmed,
            "Bạn có lượt đặt mới",
            $"{booking.GuestName} đặt \"{listing.Title}\" từ {booking.CheckIn:dd/MM} đến {booking.CheckOut:dd/MM} " +
            $"({booking.Nights} đêm, {booking.Guests} khách).",
            "/hosting", ct);

        var guest = await db.Users.FirstOrDefaultAsync(u => u.Id == guestUserId, ct);

        await notifications.QueueWithEmailAsync(guest, NotificationKind.BookingConfirmed,
            "Đặt chỗ đã được xác nhận",
            $"Mã đặt chỗ {booking.Reference} · {listing.Title} · {booking.Nights} đêm.",
            $"/trips/{booking.Id}", ct);

        // docs/01 AT-11 — a paid booking is the moment worth looking at the
        // account's pattern; the flag never stands in the guest's way.
        await risk.EvaluateAsync(guestUserId, booking, ct);

        await db.SaveChangesAsync(ct);
    }
}
