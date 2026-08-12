using Microsoft.EntityFrameworkCore;
using StayHost.Domain;

namespace StayHost.Infrastructure;

/// <summary>
/// docs/09 §2.10 and §5 — the history behind the ratings on experience and
/// service cards.
///
/// A stay review is a row with a name typed into it, so the stay seeder can
/// invent one out of nothing. An experience or a service review cannot: it is
/// signed by the account that booked it and hangs off the booking by a foreign
/// key, exactly so that nobody can score a session they never sat in. That left
/// the demo advertising "★4.85 · 27 đánh giá" above a review block with nothing
/// in it, on every session and every job on the site.
///
/// So the history is seeded as history: sessions that already ran, guests who
/// were marked present, providers who were already paid — and the money trail
/// each of those implies. The capture and the payout go through
/// <see cref="Ledger"/> like any real booking, so the books still add to zero
/// afterwards. The headline rating is then recomputed from the reviews rather
/// than left at whatever the offering seeder guessed, which is the same thing
/// that happens the moment a real guest writes the first one.
/// </summary>
public static class ReviewSeeder
{
    /// <summary>One guest's word, and the four scores behind it.</summary>
    private record Note(string Text, int A, int B, int C, int D);

    /// <summary>docs/09 §2.10 — người dẫn · đúng như mô tả · tổ chức và an toàn · đáng giá tiền.</summary>
    private static readonly Note[] ExperienceNotes =
    [
        new("Người dẫn kể chuyện hay hơn cả phần thực hành. Nhóm bảy người mà ai cũng có việc để làm.", 5, 5, 5, 5),
        new("Đúng như mô tả, không thêm không bớt. Xong buổi còn được gói phần thừa mang về.", 5, 5, 4, 5),
        new("Đi chậm lại cho hai bạn nhỏ theo kịp, nhắc chỗ trơn trượt từ xa. Cả buổi thấy yên tâm.", 5, 4, 5, 4),
        new("Vui, nhưng đông hơn mình tưởng một chút nên có lúc phải chờ. Vẫn đáng tiền.", 4, 4, 5, 4),
        new("Đặt buổi sáng sớm, trời mưa mà vẫn chạy đúng giờ. Chuẩn bị kỹ, có sẵn áo mưa cho cả nhóm.", 5, 5, 5, 4),
        new("Lần thứ hai mình đi và vẫn thích. Người dẫn nhớ cả tên khách cũ.", 5, 5, 5, 5)
    ];

    /// <summary>docs/09 §5 — tay nghề · đúng như mô tả · đúng giờ · đáng giá tiền.</summary>
    private static readonly Note[] ServiceNotes =
    [
        new("Tới sớm mười lăm phút, làm gọn và dọn sạch chỗ trước khi về.", 5, 5, 5, 5),
        new("Đúng như trong tin đăng. Hỏi gì cũng trả lời rõ ràng, không giục thêm dịch vụ.", 5, 5, 4, 5),
        new("Tay nghề tốt, chỉ tội tới trễ hai mươi phút vì kẹt xe — có nhắn báo trước.", 5, 5, 3, 4),
        new("Giá hợp lý cho chất lượng này. Nhà mình sẽ đặt lại lần sau.", 4, 5, 5, 5),
        new("Chu đáo, mang đủ đồ nghề, không phải nhắc gì thêm.", 5, 4, 5, 4),
        new("Nhắn tin xác nhận từ hôm trước nên mình không phải chờ đợi gì.", 5, 5, 5, 4)
    ];

    /// <summary>
    /// Six of each, which is how many notes there are: every listing then gets
    /// the whole set exactly once, rotated, so no page shows the same sentence
    /// twice and no two pages open with the same one.
    /// </summary>
    private const int PastSessions = 6;
    private const int PastJobs = 6;

    /// <summary>Seats sold on each finished session, in order.</summary>
    private static readonly int[] SeatsPerSession = [2, 3, 2, 4, 2, 3];

    public static async Task SeedAsync(StayHostDbContext db, CancellationToken ct = default)
    {
        // Written once. A second run must not double the history, and must not
        // add a second review to a booking that already has one.
        if (await db.ExperienceReviews.AnyAsync(ct) || await db.ServiceReviews.AnyAsync(ct)) return;

        var guests = await db.Users
            .Where(u => u.Role == UserRole.Guest && u.Email.StartsWith("khach"))
            .OrderBy(u => u.Id)
            .ToListAsync(ct);
        if (guests.Count == 0) return;

        var taxRules = await db.TaxRules.Where(r => r.IsActive).OrderBy(r => r.SortOrder).ToListAsync(ct);

        await SeedExperiencesAsync(db, guests, taxRules, ct);
        await SeedServicesAsync(db, guests, taxRules, ct);
    }

    /* ------------------------------------------------------------ sessions */

    private static async Task SeedExperiencesAsync(
        StayHostDbContext db, List<User> guests, List<TaxRule> taxRules, CancellationToken ct)
    {
        var experiences = await db.Experiences.OrderBy(x => x.Id).ToListAsync(ct);
        if (experiences.Count == 0) return;

        var midnight = DateTime.UtcNow.Date;
        var written = 0;

        for (var i = 0; i < experiences.Count; i++)
        {
            var x = experiences[i];
            var scores = new List<(int A, int B, int C, int D)>();

            for (var p = 0; p < PastSessions; p++)
            {
                // Well past the fortnight the host console reaches back over, so a
                // finished session never turns up in anybody's picker.
                var startsAt = DateTime.SpecifyKind(
                    midnight.AddDays(-24 - p * 13).AddHours(8 + i % 5), DateTimeKind.Utc);

                var seats = SeatsPerSession[p % SeatsPerSession.Length];
                var slot = new ExperienceSlot
                {
                    ExperienceId = x.Id,
                    StartsAt = startsAt,
                    Capacity = Math.Max(seats, x.MaxGroup),
                    SeatsTaken = seats
                };
                db.ExperienceSlots.Add(slot);
                await db.SaveChangesAsync(ct);

                var guest = guests[written % guests.Count];
                var note = ExperienceNotes[written % ExperienceNotes.Length];

                var price = Pricing.QuoteExperience(new Pricing.ExperienceRequest
                {
                    Experience = x,
                    Seats = seats,
                    StartsAt = startsAt,
                    TaxRules = taxRules
                });

                var paidOutAt = startsAt.AddMinutes(x.DurationMinutes).AddDays(1);
                var booking = new ExperienceBooking
                {
                    Reference = $"XP{Reference(x.Id, p)}",
                    SlotId = slot.Id,
                    GuestUserId = guest.Id,
                    Seats = seats,
                    Subtotal = price.Subtotal,
                    ServiceFee = price.GuestServiceFee,
                    Tax = price.Tax,
                    Total = price.Total,
                    HostServiceFee = price.HostServiceFee,
                    HostPayout = price.HostPayout,
                    Status = ExperienceBookingStatus.Completed,
                    CreatedAt = startsAt.AddDays(-6),
                    // docs/09 §2.10 — only somebody the host marked present may write
                    // a review, so the register has to have been taken.
                    Attended = true,
                    AttendanceMarkedAt = startsAt.AddMinutes(x.DurationMinutes),
                    // docs/09 §4 (MR-C-03) — paid a day after the session ended.
                    // Already settled, so the payout sweeper leaves these alone.
                    PayoutStatus = PayoutStatus.Paid,
                    PaidOutAt = paidOutAt,
                    PayoutReference = $"CK{Reference(x.Id, p)}"
                };
                db.ExperienceBookings.Add(booking);
                await db.SaveChangesAsync(ct);

                db.LedgerEntries.AddRange(Ledger.CaptureExperience(booking, price, booking.CreatedAt));
                db.LedgerEntries.AddRange(Ledger.PayoutExperience(booking, booking.HostPayout, paidOutAt));

                db.ExperienceReviews.Add(new ExperienceReview
                {
                    BookingId = booking.Id,
                    ExperienceId = x.Id,
                    AuthorUserId = guest.Id,
                    HostScore = note.A,
                    AsDescribedScore = note.B,
                    SafetyScore = note.C,
                    ValueScore = note.D,
                    Comment = note.Text,
                    CreatedAt = paidOutAt
                });

                scores.Add((note.A, note.B, note.C, note.D));
                written++;
            }

            // The number on the card is the average of what is written under it,
            // which is what WriteReviewAsync does on the first real review anyway.
            x.ReviewCount = scores.Count;
            x.Rating = Math.Round(
                scores.Average(s => ExperienceReviews.Average(s.A, s.B, s.C, s.D)), 2);

            await db.SaveChangesAsync(ct);
        }
    }

    /* ---------------------------------------------------------------- jobs */

    private static async Task SeedServicesAsync(
        StayHostDbContext db, List<User> guests, List<TaxRule> taxRules, CancellationToken ct)
    {
        var offerings = await db.ServiceOfferings.OrderBy(o => o.Id).ToListAsync(ct);
        if (offerings.Count == 0) return;

        var midnight = DateTime.UtcNow.Date;
        var written = 0;

        for (var i = 0; i < offerings.Count; i++)
        {
            var o = offerings[i];
            var scores = new List<(int A, int B, int C, int D)>();

            for (var p = 0; p < PastJobs; p++)
            {
                // Spread across separate days: two finished jobs sitting on top of
                // each other would be a diary the provider could never have worked.
                var startsAt = DateTime.SpecifyKind(
                    midnight.AddDays(-9 - p * 11).AddHours(o.OpensAtHour + 1), DateTimeKind.Utc);

                var guest = guests[written % guests.Count];
                var note = ServiceNotes[written % ServiceNotes.Length];
                var quantity = Math.Clamp(o.MinQuantity + p % 2, o.MinQuantity, o.MaxQuantity);

                var price = Pricing.QuoteService(new Pricing.ServiceRequest
                {
                    Offering = o,
                    Quantity = quantity,
                    StartsAt = startsAt,
                    TaxRules = taxRules
                });

                var paidOutAt = startsAt.AddMinutes(o.DurationMinutes).AddDays(1);
                var booking = new ServiceBooking
                {
                    Reference = $"SV{Reference(o.Id, p)}",
                    OfferingId = o.Id,
                    GuestUserId = guest.Id,
                    StartsAt = startsAt,
                    DurationMinutes = o.DurationMinutes,
                    Quantity = price.Quantity,
                    Address = o.TravelsToGuest ? $"Chỗ ở của khách, {o.City}" : "",
                    Latitude = o.TravelsToGuest ? o.Latitude + 0.01 : 0,
                    Longitude = o.TravelsToGuest ? o.Longitude + 0.01 : 0,
                    Subtotal = price.Subtotal,
                    ServiceFee = price.GuestServiceFee,
                    Tax = price.Tax,
                    Total = price.Total,
                    PlatformCut = price.PlatformCut,
                    ProviderPayout = price.ProviderPayout,
                    AddOnsTotal = price.AddOnsTotal,
                    TravelFee = price.TravelFee,
                    // docs/09 §3.3 (MR-S-07) — a job that went ahead is a job whose
                    // conditions were confirmed; the provider turned up and worked.
                    ConditionsConfirmed = true,
                    Status = ServiceBookingStatus.Completed,
                    CreatedAt = startsAt.AddDays(-4),
                    PayoutStatus = PayoutStatus.Paid,
                    PaidOutAt = paidOutAt,
                    PayoutReference = $"CK{Reference(o.Id, p)}"
                };
                db.ServiceBookings.Add(booking);
                await db.SaveChangesAsync(ct);

                db.LedgerEntries.AddRange(Ledger.CaptureService(booking, price, booking.CreatedAt));
                db.LedgerEntries.AddRange(Ledger.PayoutService(booking, booking.ProviderPayout, paidOutAt));

                db.ServiceReviews.Add(new ServiceReview
                {
                    BookingId = booking.Id,
                    OfferingId = o.Id,
                    AuthorUserId = guest.Id,
                    SkillScore = note.A,
                    AsDescribedScore = note.B,
                    PunctualityScore = note.C,
                    ValueScore = note.D,
                    Comment = note.Text,
                    CreatedAt = paidOutAt
                });

                scores.Add((note.A, note.B, note.C, note.D));
                written++;
            }

            o.ReviewCount = scores.Count;
            o.Rating = Math.Round(
                scores.Average(s => ServiceReviews.Average(s.A, s.B, s.C, s.D)), 2);

            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// A stable eight-character reference. Not random: a seeder that produced a
    /// different one every run would make two databases seeded from the same code
    /// impossible to compare.
    /// </summary>
    private static string Reference(int ownerId, int n) =>
        $"{ownerId:D4}{n:D2}00";
}
