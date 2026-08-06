using Microsoft.EntityFrameworkCore;
using StayHost.Domain;

namespace StayHost.Infrastructure;

/// <summary>docs/01 MR-05 → MR-07 — a spread of services, including partner ones.</summary>
public static class ServiceSeeder
{
    private record Seed(
        string Title, string Category, string City, string Summary, string Description,
        ServicePricing Pricing, decimal Price, int MinQty, int MaxQty, int Minutes,
        bool Travels, int RadiusKm, double Lat, double Lng, int Opens, int Closes,
        string? Partner, decimal Commission, string Image);

    public static async Task SeedAsync(StayHostDbContext db, CancellationToken ct = default)
    {
        if (await db.ServiceOfferings.AnyAsync(ct)) return;

        var hosts = await db.Hosts.OrderBy(h => h.Id).ToListAsync(ct);
        if (hosts.Count == 0) return;

        for (var i = 0; i < Seeds.Length; i++)
        {
            var s = Seeds[i];
            var offering = new ServiceOffering
            {
                HostId = hosts[i % hosts.Count].Id,
                Slug = Slugify(s.Title, i + 1),
                Title = s.Title,
                Category = s.Category,
                City = s.City,
                Summary = s.Summary,
                Description = s.Description,
                Pricing = s.Pricing,
                BasePrice = s.Price,
                MinQuantity = s.MinQty,
                MaxQuantity = s.MaxQty,
                DurationMinutes = s.Minutes,
                TravelsToGuest = s.Travels,
                ServiceRadiusKm = s.RadiusKm,
                Latitude = s.Lat,
                Longitude = s.Lng,
                OpensAtHour = s.Opens,
                ClosesAtHour = s.Closes,
                IsPartner = s.Partner is not null,
                PartnerName = s.Partner,
                CommissionRate = s.Commission,
                IsPublished = true,
                Rating = 4.7 + (i % 4) * 0.05,
                ReviewCount = 12 + i * 5,
                Images = [new ServiceImage { Url = s.Image, SortOrder = 0 }]
            };

            offering.RefreshSearchText();
            db.ServiceOfferings.Add(offering);
        }

        await db.SaveChangesAsync(ct);
    }

    private static readonly Seed[] Seeds =
    [
        new("Đầu bếp nấu tại nhà — mâm Việt 5 món", "chef", "Đà Nẵng",
            "Đi chợ, nấu và dọn dẹp ngay trong bếp chỗ bạn ở",
            "Bếp trưởng tới trước hai tiếng, mang theo nguyên liệu đã chọn sáng cùng ngày. Nấu năm món " +
            "theo mùa, dọn bàn, và rửa sạch bếp trước khi về. Báo trước nếu có ai dị ứng.",
            ServicePricing.PerPerson, 650_000m, 2, 12, 240, true, 15, 16.0544, 108.2022, 9, 21,
            null, 0.03m,
            "https://images.pexels.com/photos/262978/pexels-photo-262978.jpeg"),

        new("Chụp ảnh chân dung du lịch", "photo", "Hội An",
            "90 phút chụp quanh phố cổ, giao 40 ảnh đã chỉnh",
            "Đi bộ qua ba khu vực có ánh sáng đẹp nhất theo giờ bạn chọn. Giao 40 ảnh chỉnh màu trong " +
            "48 giờ, kèm toàn bộ ảnh gốc. Có gợi ý dáng cho người ngại máy ảnh.",
            ServicePricing.PerSession, 2_400_000m, 1, 1, 90, true, 10, 15.8801, 108.3380, 6, 18,
            null, 0.03m,
            "https://images.pexels.com/photos/1264210/pexels-photo-1264210.jpeg"),

        new("Massage trị liệu tại phòng", "massage", "Đà Nẵng",
            "Kỹ thuật viên mang giường và dầu tới tận nơi",
            "Massage cổ vai gáy hoặc toàn thân, 60 hoặc 90 phút. Kỹ thuật viên có chứng chỉ, mang theo " +
            "giường gấp, khăn sạch và dầu dừa ép lạnh.",
            ServicePricing.PerHour, 480_000m, 1, 3, 90, true, 12, 16.0600, 108.2400, 9, 22,
            "Spa Hương Việt", 0.18m,
            "https://images.pexels.com/photos/3757942/pexels-photo-3757942.jpeg"),

        new("Đưa đón sân bay Nội Bài", "transfer", "Hà Nội",
            "Xe 4 hoặc 7 chỗ, tài xế đợi sẵn ở sảnh đến",
            "Theo dõi số hiệu chuyến bay, đợi thêm 60 phút miễn phí nếu máy bay trễ. Tài xế cầm bảng tên, " +
            "hỗ trợ hành lý, xe có nước và wifi.",
            ServicePricing.PerOrder, 480_000m, 1, 4, 90, true, 40, 21.0285, 105.8542, 0, 23,
            "Xanh Taxi Hà Nội", 0.12m,
            "https://images.pexels.com/photos/136739/pexels-photo-136739.jpeg"),

        new("Giữ hành lý theo giờ", "luggage", "Thành phố Hồ Chí Minh",
            "Gửi vali giữa lúc trả phòng và giờ bay",
            "Điểm giữ ngay trung tâm quận 1, có camera và niêm phong từng kiện. Nhận từ 6 giờ sáng, " +
            "trả tới nửa đêm.",
            ServicePricing.PerOrder, 60_000m, 1, 8, 60, false, 0, 10.7769, 106.7009, 6, 23,
            "Lockbox Sài Gòn", 0.20m,
            "https://images.pexels.com/photos/1008155/pexels-photo-1008155.jpeg"),

        new("Đi chợ hộ và giao tận cửa", "groceries", "Đà Lạt",
            "Danh sách của bạn, chợ Đà Lạt của chúng tôi",
            "Gửi danh sách trước 20 giờ hôm trước, hàng được giao trước 9 giờ sáng hôm sau. Hoá đơn " +
            "chợ giữ nguyên, phí dịch vụ tính riêng.",
            ServicePricing.PerOrder, 180_000m, 1, 3, 120, true, 8, 11.9404, 108.4583, 6, 12,
            null, 0.03m,
            "https://images.pexels.com/photos/264636/pexels-photo-264636.jpeg")
    ];

    private static string Slugify(string title, int n)
    {
        var normalised = SearchText.Normalize(title);
        var slug = new string(normalised.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return $"{slug.Trim('-')}-{n}";
    }
}
