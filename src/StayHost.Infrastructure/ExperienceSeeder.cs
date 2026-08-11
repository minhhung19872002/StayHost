using Microsoft.EntityFrameworkCore;
using StayHost.Domain;

namespace StayHost.Infrastructure;

/// <summary>
/// docs/01 MR-01 → MR-04 — a handful of real-looking sessions so the vertical
/// has something to show the moment the app starts.
/// </summary>
public static class ExperienceSeeder
{
    private record Seed(
        string Title, string City, string Summary, string Description, int Minutes,
        int MaxGroup, int MinGuests, string Meeting, double Lat, double Lng,
        decimal Price, decimal? Private, string[] Included, string[] Images,
        (string Title, string Body)[] Steps);

    /// <summary>
    /// docs/01 MR-01 — the running order of one session. The photographs come from
    /// the experience's own gallery rather than a second set of URLs: a demo host
    /// has the pictures they have, and cycling them is what a real one short of
    /// photos would end up doing anyway.
    /// </summary>
    private static List<ExperienceStep> StepsFor(Seed s) =>
        s.Steps.Select((step, i) => new ExperienceStep
        {
            Title = step.Title,
            Description = step.Body,
            ImageUrl = s.Images.Length > 0 ? Pexels(s.Images[i % s.Images.Length]) : null,
            SortOrder = i
        }).ToList();

    /// <summary>
    /// Pexels serves the bare photo URL at full resolution (~1MB); the compressed
    /// variant with these params is ~15× smaller. Listings already use it, so this
    /// makes experiences match and load fast. Idempotent: a URL that already has a
    /// query string is left alone.
    /// </summary>
    private static string Pexels(string url) =>
        url.Contains("images.pexels.com") && !url.Contains('?')
            ? url + "?auto=compress&cs=tinysrgb&fit=crop&w=1200"
            : url;

    public static async Task SeedAsync(StayHostDbContext db, CancellationToken ct = default)
    {
        // Upgrade any existing bare full-res Pexels URLs to the compressed variant,
        // so old rows stop shipping 1MB images (and the new URL sidesteps a browser
        // that cached the empty state).
        var heavy = await db.ExperienceImages
            .Where(i => i.Url.Contains("images.pexels.com") && !i.Url.Contains("?"))
            .ToListAsync(ct);
        if (heavy.Count > 0)
        {
            foreach (var img in heavy) img.Url = Pexels(img.Url);
            await db.SaveChangesAsync(ct);
        }

        // Top up photos to match the seed, by title. Covers rows seeded before
        // images existed (0 photos) and rows from when the seed had fewer, so an
        // older deployment gains the extra slideshow images without a reseed.
        var counts = await db.Experiences
            .Select(x => new { x.Id, x.Title, Have = x.Images.Count })
            .ToListAsync(ct);
        if (counts.Count > 0)
        {
            var bySeed = Seeds.ToDictionary(s => s.Title, s => s.Images);
            var added = false;
            foreach (var x in counts)
                if (bySeed.TryGetValue(x.Title, out var urls) && x.Have < urls.Length)
                {
                    for (var j = x.Have; j < urls.Length; j++)
                        db.ExperienceImages.Add(new ExperienceImage
                        {
                            ExperienceId = x.Id, Url = Pexels(urls[j]), SortOrder = j
                        });
                    added = true;
                }
            if (added) await db.SaveChangesAsync(ct);
        }

        // docs/01 MR-01 — the running order arrived after these rows were seeded,
        // so give it to any seeded experience that still has none. Only ever adds:
        // a host who wrote their own itinerary keeps it.
        var withoutSteps = await db.Experiences
            .Where(x => x.Itinerary.Count == 0)
            .Select(x => new { x.Id, x.Title })
            .ToListAsync(ct);
        if (withoutSteps.Count > 0)
        {
            var seedByTitle = Seeds.ToDictionary(s => s.Title);
            var wrote = false;
            foreach (var x in withoutSteps)
                if (seedByTitle.TryGetValue(x.Title, out var seed))
                {
                    foreach (var step in StepsFor(seed))
                    {
                        step.ExperienceId = x.Id;
                        db.ExperienceSteps.Add(step);
                    }
                    wrote = true;
                }
            if (wrote) await db.SaveChangesAsync(ct);
        }

        // docs/09 §2.2 (MR-E-03) — moderation arrived after these rows did. Anything
        // already on sale was on sale with the platform's blessing, so it counts as
        // approved; leaving it Draft would bounce the host into the review queue the
        // first time they touched their own listing.
        var unreviewed = await db.Experiences
            .Where(x => x.IsPublished && x.ModerationStatus == ExperienceModeration.Draft)
            .ToListAsync(ct);
        if (unreviewed.Count > 0)
        {
            foreach (var x in unreviewed)
            {
                x.ModerationStatus = ExperienceModeration.Approved;
                x.ReviewedAt ??= DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct);
        }

        if (await db.Experiences.AnyAsync(ct)) return;

        var hosts = await db.Hosts.OrderBy(h => h.Id).Take(Seeds.Length).ToListAsync(ct);
        if (hosts.Count == 0) return;

        // Sessions run from tomorrow so nothing in the seed is already in the past.
        var first = DateTime.UtcNow.Date.AddDays(1).AddHours(1);

        for (var i = 0; i < Seeds.Length; i++)
        {
            var s = Seeds[i];
            var experience = new Experience
            {
                HostId = hosts[i % hosts.Count].Id,
                Slug = Slugify(s.Title, i + 1),
                Title = s.Title,
                City = s.City,
                Summary = s.Summary,
                Description = s.Description,
                DurationMinutes = s.Minutes,
                MaxGroup = s.MaxGroup,
                MinGuests = s.MinGuests,
                Languages = "vi,en",
                MinAge = 12,
                MeetingPoint = s.Meeting,
                Latitude = s.Lat,
                Longitude = s.Lng,
                Included = string.Join('\n', s.Included),
                PricePerPerson = s.Price,
                PrivateGroupPrice = s.Private,
                IsPublished = true,
                // Seeded rows stand for experiences a reviewer has already passed.
                ModerationStatus = ExperienceModeration.Approved,
                ReviewedAt = DateTime.UtcNow,
                Rating = 4.8 + (i % 3) * 0.05,
                ReviewCount = 18 + i * 7,
                Images = s.Images.Select((u, j) => new ExperienceImage { Url = Pexels(u), SortOrder = j }).ToList(),
                Itinerary = StepsFor(s)
            };

            // Three weeks of sessions, one a day, at a sensible hour.
            for (var day = 0; day < 21; day++)
            {
                experience.Slots.Add(new ExperienceSlot
                {
                    StartsAt = DateTime.SpecifyKind(first.AddDays(day).AddHours(7 + i % 5), DateTimeKind.Utc),
                    Capacity = s.MaxGroup
                });
            }

            experience.RefreshSearchText();
            db.Experiences.Add(experience);
        }

        await db.SaveChangesAsync(ct);
    }

    private static readonly Seed[] Seeds =
    [
        new("Lớp nấu ăn Hội An với đi chợ sớm", "Hội An",
            "Đi chợ Bà Lê lúc 6 giờ rồi nấu bốn món Quảng",
            "Bắt đầu ở chợ Bà Lê khi hàng rau vừa dọn ra, chọn nguyên liệu cùng nhau, rồi về bếp nhà nấu " +
            "cao lầu, mì Quảng, nem lụi và chè bắp. Ăn ngay tại chỗ, phần thừa gói mang về.",
            240, 8, 2, "Cổng chợ Bà Lê, phường Minh An", 15.8801, 108.3380,
            750_000m, 4_500_000m,
            ["Nguyên liệu và dụng cụ", "Bữa ăn bốn món", "Công thức mang về", "Nước uống"],
            ["https://images.pexels.com/photos/2544829/pexels-photo-2544829.jpeg",
             "https://images.pexels.com/photos/1640777/pexels-photo-1640777.jpeg",
             "https://images.pexels.com/photos/262978/pexels-photo-262978.jpeg",
             "https://images.pexels.com/photos/3757942/pexels-photo-3757942.jpeg"],
            [
             ("Gặp nhau ở cổng chợ", "6 giờ sáng, khi hàng rau vừa dọn ra."),
             ("Chọn nguyên liệu", "Đi một vòng chợ Bà Lê, chọn rau và thịt cùng nhau."),
             ("Về bếp nhà", "Sơ chế và học cách pha nước dùng cao lầu."),
             ("Nấu bốn món", "Cao lầu, mì Quảng, nem lụi và chè bắp."),
             ("Ăn ngay tại chỗ", "Ngồi xuống ăn, phần thừa gói mang về.")]),

        new("Chèo SUP bình minh trên sông Hoài", "Hội An",
            "Hai tiếng trên nước trước khi phố thức dậy",
            "Xuất phát 5 giờ sáng, chèo dọc rừng dừa Bảy Mẫu khi mặt nước còn phẳng. Người mới chèo được " +
            "hướng dẫn riêng 15 phút trước khi xuống nước. Kết thúc bằng cà phê ở bờ sông.",
            120, 10, 3, "Bến thuyền An Hội", 15.8770, 108.3260,
            420_000m, null,
            ["Ván SUP và áo phao", "Hướng dẫn viên cứu hộ", "Cà phê sau buổi chèo"],
            ["https://images.pexels.com/photos/1223649/pexels-photo-1223649.jpeg",
             "https://images.pexels.com/photos/1008155/pexels-photo-1008155.jpeg",
             "https://images.pexels.com/photos/1918291/pexels-photo-1918291.jpeg"],
            [
             ("Nhận ván và áo phao", "5 giờ sáng ở bến An Hội, trời còn chưa sáng hẳn."),
             ("Học 15 phút trên bờ", "Người mới được hướng dẫn riêng trước khi xuống nước."),
             ("Chèo dọc rừng dừa", "Mặt nước phẳng nhất trong ngày, đúng lúc mặt trời lên."),
             ("Cà phê bờ sông", "Kết thúc bằng một ly cà phê nhìn ra sông Hoài.")]),

        new("Đi bộ nhiếp ảnh phố cổ Hà Nội", "Hà Nội",
            "Ba tiếng chụp phố, từ hàng nước tới ban công cũ",
            "Đi qua sáu con phố ít khách du lịch, chụp người và nhịp sống buổi sáng. Hướng dẫn cách xin " +
            "phép trước khi chụp chân dung. Buổi cuối cùng ngồi lại chọn ảnh và chỉnh nhanh.",
            180, 6, 2, "Số 1 Hàng Bạc, Hoàn Kiếm", 21.0345, 108.9370,
            600_000m, 3_000_000m,
            ["Hướng dẫn viên là nhiếp ảnh gia", "Cà phê giữa buổi", "Chỉnh ảnh cuối buổi"],
            ["https://images.pexels.com/photos/1076429/pexels-photo-1076429.jpeg",
             "https://images.pexels.com/photos/264636/pexels-photo-264636.jpeg",
             "https://images.pexels.com/photos/2506988/pexels-photo-2506988.jpeg"],
            [
             ("Hàng nước đầu phố", "Bắt đầu ở số 1 Hàng Bạc với một ly trà đá."),
             ("Sáu con phố ít khách", "Chụp người và nhịp sống buổi sáng."),
             ("Xin phép trước khi chụp", "Cách hỏi, và cách nhận lời từ chối."),
             ("Cà phê giữa buổi", "Nghỉ chân, xem lại ảnh vừa chụp."),
             ("Chọn và chỉnh ảnh", "Ngồi lại cuối buổi, chỉnh nhanh vài tấm ưng nhất.")]),

        new("Tour cà phê Đà Lạt: từ vườn tới tách", "Đà Lạt",
            "Hái, phơi, rang và pha trong một buổi chiều",
            "Ra vườn arabica trên đồi, hái vài cân quả chín, xem mẻ đang phơi, rồi tự rang một mẻ nhỏ và " +
            "pha ba kiểu khác nhau. Ai cũng mang về 200 gram cà phê mình vừa rang.",
            210, 12, 4, "Vườn cà phê Cầu Đất, xã Xuân Trường", 11.8000, 108.5000,
            520_000m, 5_000_000m,
            ["Xe đưa đón từ trung tâm", "200g cà phê mang về", "Bánh và cà phê thử"],
            ["https://images.pexels.com/photos/4820817/pexels-photo-4820817.jpeg",
             "https://images.pexels.com/photos/1264210/pexels-photo-1264210.jpeg",
             "https://images.pexels.com/photos/1008155/pexels-photo-1008155.jpeg"],
            [
             ("Xe đón ở trung tâm", "Chạy lên đồi Cầu Đất, khoảng 40 phút."),
             ("Hái quả chín", "Ra vườn arabica, hái vài cân quả chín đỏ."),
             ("Xem mẻ đang phơi", "Hiểu vì sao phơi lâu lại ngọt hơn."),
             ("Tự rang một mẻ", "Mỗi người rang 200 gram của riêng mình."),
             ("Pha ba kiểu", "Phin, pour-over và espresso, so vị cạnh nhau.")]),

        new("Chợ đêm và ăn vặt Sài Gòn bằng xe máy", "TP. Hồ Chí Minh",
            "Bảy món ở năm quận, ngồi sau xe người địa phương",
            "Bắt đầu ở quận 4 với ốc, qua quận 5 ăn hủ tiếu hồ, dừng ở một quán chè mở từ 1975. Mỗi " +
            "người đi cùng một tài xế riêng, có mũ bảo hiểm và áo mưa.",
            240, 8, 2, "Công viên bến Bạch Đằng, quận 1", 10.7720, 106.7060,
            890_000m, null,
            ["Xe máy và tài xế riêng", "Bảy món ăn", "Nước uống", "Mũ bảo hiểm"],
            ["https://images.pexels.com/photos/2233729/pexels-photo-2233729.jpeg",
             "https://images.pexels.com/photos/262978/pexels-photo-262978.jpeg",
             "https://images.pexels.com/photos/136739/pexels-photo-136739.jpeg"],
            [
             ("Nhận mũ và áo mưa", "Gặp tài xế riêng ở công viên bến Bạch Đằng."),
             ("Ốc quận 4", "Món đầu tiên, ăn ngay vỉa hè bên sông."),
             ("Hủ tiếu hồ quận 5", "Chạy qua Chợ Lớn, ăn món của người Tiều."),
             ("Quán chè từ 1975", "Chủ quán đời thứ hai, công thức không đổi."),
             ("Về lại quận 1", "Kết thúc ở chỗ xuất phát, khoảng 10 giờ đêm.")])
    ];

    private static string Slugify(string title, int n)
    {
        var normalised = SearchText.Normalize(title);
        var slug = new string(normalised.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return $"{slug.Trim('-')}-{n}";
    }
}
