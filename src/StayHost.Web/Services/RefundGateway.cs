using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Services.Gateways;

namespace StayHost.Web.Services;

/// <summary>
/// docs/07 §10 — the one place that actually sends a guest's money back.
///
/// Five paths cancel a booking: the guest, the host, an admin's manual refund, a
/// suspension, and the sweep that gives up on a part-paid stay. Before this, one
/// of them asked the stand-in gateway and the other four passed
/// <c>cardRefundAccepted: true</c> without asking anything at all — a default
/// that was harmless while no real money existed and became a lie the day VNPay
/// was switched on.
///
/// The answer that matters is not yes or no but <em>refused</em> versus
/// <em>don't know</em>. A refusal is §10's own case: the card is closed, so the
/// money becomes balance and the guest is told. Not knowing is different, and
/// the gateways are asked again rather than guessed at.
/// </summary>
public class RefundGateway(
    StayHostDbContext db, PspRouter router, PaymentGateway gateway, ILogger<RefundGateway> log)
{
    /// <summary>
    /// True when the money is on its way back to where it came from; false when
    /// it has to become balance instead (docs/07 §10, <c>Refunds.Redirect</c>).
    /// </summary>
    public async Task<bool> SendAsync(
        Booking booking, decimal amount, string by, string reason, CancellationToken ct)
    {
        if (amount <= 0) return true;

        // The gateway visit that actually took this booking's money. A stay paid
        // through the stand-in has none, and falls through to it below.
        var session = await db.PaymentSessions
            .Where(s => s.BookingId == booking.Id && s.Status == PaymentSessionStatus.Paid)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(ct);

        if (session is null || router.ByKey(session.Provider) is not { } provider)
        {
            return gateway.Refund(amount, booking.Payment?.Method ?? "card",
                booking.Payment?.CardLast4);
        }

        var result = await provider.RefundAsync(new PspRefund(
            session.OrderRef, amount, session.Amount, session.ProviderTxnId,
            session.ProviderPaidAt, session.CreatedAt,
            // VNPay puts this on the guest's statement, so it is plain and short
            // rather than an internal reason code.
            $"Hoan tien don {booking.Reference}", by), ct);

        // docs/07 §7 — a refund the platform cannot see is a day that will not
        // reconcile and nobody able to say why, so what the gateway answered is
        // written down whichever way it went.
        session.RefundedAmount = amount;
        session.RefundTxnId = result.TxnId;
        session.RefundCode = result.Code;
        session.RefundedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        switch (result.Outcome)
        {
            case Psp.RefundOutcome.Accepted:
                log.LogInformation("Đã yêu cầu {Provider} hoàn {Amount} cho đơn {Reference}.",
                    session.Provider, amount, booking.Reference);
                return true;

            case Psp.RefundOutcome.Refused:
                // docs/07 §10 — "Không được thì chuyển thành số dư trong tài khoản
                // sàn và báo khách." The caller does both.
                log.LogWarning("{Provider} từ chối hoàn {Amount} cho đơn {Reference}; chuyển sang số dư.",
                    session.Provider, amount, booking.Reference);
                return false;

            default:
                // Nobody knows. The money becomes balance so the guest is not left
                // with nothing, and this is shouted about because it is the one
                // outcome that can end in a guest being paid twice — the refund
                // may yet land at the gateway under the request id in the log.
                log.LogError(
                    "Không rõ {Provider} đã hoàn {Amount} cho đơn {Reference} hay chưa. " +
                    "Đã chuyển sang số dư — cần đối chiếu tay ở cổng.",
                    session.Provider, amount, booking.Reference);
                return false;
        }
    }
}
