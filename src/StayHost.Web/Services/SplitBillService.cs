using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;

namespace StayHost.Web.Services;

/// <summary>
/// docs/01 ĐP-07 — the parts of a split bill that happen away from a request:
/// sending the invitations, turning a fully-paid split into a real booking, and
/// giving the money back when the day runs out.
/// </summary>
public class SplitBillService(StayHostDbContext db, ILogger<SplitBillService> log)
{
    public async Task InviteAsync(BillSplit split, Booking booking, CancellationToken ct)
    {
        foreach (var share in split.Shares.Where(s => s.Email != null))
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == share.Email, ct);

            // docs/01 TK-09 — the frame follows the invitee's language when they
            // have an account. The pay TOKEN in the body is a secret, so
            // RawTitle stays null and the machine-translation pass skips this.
            var name = share.Name ?? share.Email;
            db.EmailMessages.Add(new EmailMessage
            {
                ToEmail = share.Email,
                ToName = name,
                Subject = $"Trả phần của bạn cho chuyến đi {booking.Reference}",
                Body = Emails.Compose(user?.Language, name,
                    $"Bạn được mời cùng trả cho chuyến đi tại {booking.Listing?.Title}.",
                    $"Phần của bạn: {share.Amount:#,##0}₫.\n" +
                    $"Mở liên kết này để trả: /split/{share.Token}\n" +
                    "Liên kết có hiệu lực trong 24 giờ.", null),
                Language = user?.Language
            });

            if (user is not null)
            {
                db.Notifications.Add(new Notification
                {
                    UserId = user.Id,
                    Kind = NotificationKind.System,
                    Title = "Bạn được mời cùng trả một chuyến đi",
                    Body = $"Phần của bạn là {share.Amount:#,##0}₫ cho đơn {booking.Reference}.",
                    Link = $"/split/{share.Token}"
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Everyone paid. The money held in escrow becomes the booking's, and from
    /// here on it is an ordinary confirmed booking with an ordinary receipt.
    /// </summary>
    public async Task CompleteAsync(
        BillSplit split, Booking booking, CatalogService catalog, NotificationService notifications,
        CancellationToken ct)
    {
        var party = new PartySize(booking.Adults, booking.Children, booking.Infants, booking.Pets);
        var request = await catalog.BuildQuoteRequestAsync(
            booking.ListingId, booking.CheckIn, booking.CheckOut, party, ct, booking.Id);
        var price = Pricing.Quote(request!);

        db.LedgerEntries.AddRange(Ledger.ReleaseEscrow(booking, split.Total, DateTime.UtcNow));
        db.LedgerEntries.AddRange(Ledger.CaptureBooking(booking, price, DateTime.UtcNow, split.Total));

        db.BookingEvents.Add(BookingLifecycle.Transition(
            booking, BookingStatus.Confirmed, $"guest:{split.OrganiserUserId}",
            $"Chia hoá đơn cho {split.Shares.Count} người, đã trả đủ."));

        booking.DepositPaid = split.Total;
        booking.BalanceDue = 0;
        booking.BalanceStatus = BalanceStatus.None;

        if (booking.Payment is not null)
        {
            booking.Payment.Status = PaymentStatus.Captured;
            booking.Payment.CapturedAt = DateTime.UtcNow;
            booking.Payment.Method = "split";
        }

        split.Status = BillSplitStatus.Complete;
        split.CompletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        var organiser = await db.Users.FirstOrDefaultAsync(u => u.Id == split.OrganiserUserId, ct);
        await notifications.QueueWithEmailAsync(
            organiser, NotificationKind.BookingConfirmed,
            "Mọi người đã trả đủ",
            $"Đơn {booking.Reference} đã được xác nhận.",
            $"/trips/{booking.Id}", ct);

        await db.SaveChangesAsync(ct);
        log.LogInformation("Split bill for {Reference} completed.", booking.Reference);
    }

    /// <summary>
    /// The split is over without completing. Everything collected goes back to
    /// the people who sent it, and the dates return to the market.
    /// </summary>
    public async Task UnwindAsync(BillSplit split, BillSplitStatus status, string reason, CancellationToken ct)
    {
        var booking = split.Booking ?? await db.Bookings
            .Include(b => b.Events)
            .FirstAsync(b => b.Id == split.BookingId, ct);

        // One query for every payer's language, not one per share. Most shares
        // belong to strangers with no account; null means Vietnamese.
        var paidEmails = split.Shares
            .Where(s => s.Status == BillShareStatus.Paid && s.Email != null)
            .Select(s => s.Email!.ToLower()).ToList();
        var languages = await db.Users
            .Where(u => paidEmails.Contains(u.Email))
            .ToDictionaryAsync(u => u.Email, u => u.Language, ct);

        foreach (var share in split.Shares.Where(s => s.Status == BillShareStatus.Paid))
        {
            db.LedgerEntries.AddRange(
                Ledger.ReturnShare(booking.Id, booking.Reference, share.Amount, DateTime.UtcNow));
            share.Status = BillShareStatus.Returned;

            var lang = share.Email != null && languages.TryGetValue(share.Email.ToLower(), out var l)
                ? l : null;
            var name = share.Name ?? share.Email;
            db.EmailMessages.Add(new EmailMessage
            {
                ToEmail = share.Email,
                ToName = name,
                Subject = $"Đã hoàn lại phần của bạn — đơn {booking.Reference}",
                Body = Emails.Compose(lang, name ?? "",
                    $"Đã hoàn lại phần của bạn cho đơn {booking.Reference}.",
                    $"{reason}\nSố tiền {share.Amount:#,##0}₫ đã được hoàn về phương thức bạn đã dùng.",
                    null),
                Language = lang
            });
        }

        split.Status = status;

        if (booking.Status == BookingStatus.PendingPayment)
        {
            db.BookingEvents.Add(BookingLifecycle.Transition(
                booking, BookingStatus.PaymentFailed, "system", reason));
        }

        await db.SaveChangesAsync(ct);
        log.LogInformation("Split bill for {Reference} unwound: {Reason}", booking.Reference, reason);
    }

    /// <summary>Splits whose day ran out. Called from the lifecycle sweep.</summary>
    public async Task<int> SweepAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var stale = await db.BillSplits
            .Include(s => s.Shares)
            .Include(s => s.Booking!).ThenInclude(b => b.Events)
            .Where(s => s.Status == BillSplitStatus.Collecting && s.ExpiresAt <= now)
            .ToListAsync(ct);

        foreach (var split in stale)
            await UnwindAsync(split, BillSplitStatus.Expired, "Quá 24 giờ mà chưa đủ người trả.", ct);

        return stale.Count;
    }
}
