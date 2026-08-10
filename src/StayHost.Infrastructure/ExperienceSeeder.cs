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
        decimal Price, decimal? Private, string[] Included, string[] Images);

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
                Images = s.Images.Select((u, j) => new ExperienceImage { Url = Pexels(u), SortOrder = j }).ToList()
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
             "https://images.pexels.com/photos/3757942/pexels-photo-3757942.jpeg"]),

        new("Chèo SUP bình minh trên sông Hoài", "Hội An",
            "Hai tiếng trên nước trước khi phố thức dậy",
            "Xuất phát 5 giờ sáng, chèo dọc rừng dừa Bảy Mẫu khi mặt nước còn phẳng. Người mới chèo được " +
            "hướng dẫn riêng 15 phút trước khi xuống nước. Kết thúc bằng cà phê ở bờ sông.",
            120, 10, 3, "Bến thuyền An Hội", 15.8770, 108.3260,
            420_000m, null,
            ["Ván SUP và áo phao", "Hướng dẫn viên cứu hộ", "Cà phê sau buổi chèo"],
            ["https://images.pexels.com/photos/1223649/pexels-photo-1223649.jpeg",
             "https://images.pexels.com/photos/1008155/pexels-photo-1008155.jpeg",
             "https://images.pexels.com/photos/1918291/pexels-photo-1918291.jpeg"]),

        new("Đi bộ nhiếp ảnh phố cổ Hà Nội", "Hà Nội",
            "Ba tiếng chụp phố, từ hàng nước tới ban công cũ",
            "Đi qua sáu con phố ít khách du lịch, chụp người và nhịp sống buổi sáng. Hướng dẫn cách xin " +
            "phép trước khi chụp chân dung. Buổi cuối cùng ngồi lại chọn ảnh và chỉnh nhanh.",
            180, 6, 2, "Số 1 Hàng Bạc, Hoàn Kiếm", 21.0345, 108.9370,
            600_000m, 3_000_000m,
            ["Hướng dẫn viên là nhiếp ảnh gia", "Cà phê giữa buổi", "Chỉnh ảnh cuối buổi"],
            ["https://images.pexels.com/photos/1076429/pexels-photo-1076429.jpeg",
             "https://images.pexels.com/photos/264636/pexels-photo-264636.jpeg",
             "https://images.pexels.com/photos/2506988/pexels-photo-2506988.jpeg"]),

        new("Tour cà phê Đà Lạt: từ vườn tới tách", "Đà Lạt",
            "Hái, phơi, rang và pha trong một buổi chiều",
            "Ra vườn arabica trên đồi, hái vài cân quả chín, xem mẻ đang phơi, rồi tự rang một mẻ nhỏ và " +
            "pha ba kiểu khác nhau. Ai cũng mang về 200 gram cà phê mình vừa rang.",
            210, 12, 4, "Vườn cà phê Cầu Đất, xã Xuân Trường", 11.8000, 108.5000,
            520_000m, 5_000_000m,
            ["Xe đưa đón từ trung tâm", "200g cà phê mang về", "Bánh và cà phê thử"],
            ["https://images.pexels.com/photos/4820817/pexels-photo-4820817.jpeg",
             "https://images.pexels.com/photos/1264210/pexels-photo-1264210.jpeg",
             "https://images.pexels.com/photos/1008155/pexels-photo-1008155.jpeg"]),

        new("Chợ đêm và ăn vặt Sài Gòn bằng xe máy", "TP. Hồ Chí Minh",
            "Bảy món ở năm quận, ngồi sau xe người địa phương",
            "Bắt đầu ở quận 4 với ốc, qua quận 5 ăn hủ tiếu hồ, dừng ở một quán chè mở từ 1975. Mỗi " +
            "người đi cùng một tài xế riêng, có mũ bảo hiểm và áo mưa.",
            240, 8, 2, "Công viên bến Bạch Đằng, quận 1", 10.7720, 106.7060,
            890_000m, null,
            ["Xe máy và tài xế riêng", "Bảy món ăn", "Nước uống", "Mũ bảo hiểm"],
            ["https://images.pexels.com/photos/2233729/pexels-photo-2233729.jpeg",
             "https://images.pexels.com/photos/262978/pexels-photo-262978.jpeg",
             "https://images.pexels.com/photos/136739/pexels-photo-136739.jpeg"])
    ];

    private static string Slugify(string title, int n)
    {
        var normalised = SearchText.Normalize(title);
        var slug = new string(normalised.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return $"{slug.Trim('-')}-{n}";
    }
}
