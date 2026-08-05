using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;

namespace StayHost.Web.Services;

/// <summary>Everything the browse / detail pages need, in one place.</summary>
public class CatalogService(StayHostDbContext db)
{
    public static readonly (PlaceType Type, string Key, string Label, string Icon)[] Categories =
    [
        (0,                  "all",       "Tất cả",        "◈"),
        (PlaceType.Villa,     "villa",     "Villa",         "⌂"),
        (PlaceType.Apartment, "apartment", "Căn hộ",        "▤"),
        (PlaceType.Homestay,  "homestay",  "Homestay",      "✿"),
        (PlaceType.House,     "house",     "Nhà nguyên căn", "⬡"),
        (PlaceType.Cabin,     "cabin",     "Cabin gỗ",      "⛰"),
        (PlaceType.Boutique,  "boutique",  "Boutique",      "✧")
    ];

    private static readonly RoomTypeDto[] RoomTypes =
    [
        new("any", "Bất kỳ", "Mọi loại chỗ ở"),
        new("entire", "Nguyên căn", "Bạn dùng trọn chỗ nghỉ"),
        new("private", "Phòng riêng", "Phòng riêng, chia sẻ khu vực chung"),
        new("shared", "Phòng chung", "Ngủ chung phòng với khách khác")
    ];

    private static readonly CurrencyDto[] Currencies =
    [
        new("VND", "Việt Nam Đồng", "₫", 1m),
        new("USD", "US Dollar", "$", 0.0000392m),
        new("EUR", "Euro", "€", 0.0000362m),
        new("JPY", "Japanese Yen", "¥", 0.00596m),
        new("KRW", "South Korean Won", "₩", 0.0535m),
        new("SGD", "Singapore Dollar", "S$", 0.0000508m),
        new("AUD", "Australian Dollar", "A$", 0.0000602m),
        new("GBP", "British Pound", "£", 0.0000309m)
    ];

    private static readonly LanguageDto[] Languages =
    [
        new("vi", "Tiếng Việt", "Việt Nam"),
        new("en", "English", "United States"),
        new("ja", "日本語", "日本"),
        new("ko", "한국어", "대한민국"),
        new("zh", "中文 (简体)", "中国"),
        new("fr", "Français", "France"),
        new("de", "Deutsch", "Deutschland"),
        new("es", "Español", "España")
    ];

    public static string CategoryKey(PlaceType t) =>
        Categories.FirstOrDefault(c => c.Type == t).Key ?? "all";

    public static string CategoryLabel(PlaceType t) =>
        Categories.FirstOrDefault(c => c.Type == t).Label ?? t.ToString();

    public static string RoomTypeLabel(RoomType r) => r switch
    {
        RoomType.EntirePlace => "Nguyên căn",
        RoomType.PrivateRoom => "Phòng riêng",
        _ => "Phòng chung"
    };

    public async Task<MetaDto> GetMetaAsync(CancellationToken ct)
    {
        var counts = await db.Listings
            .GroupBy(l => l.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var total = counts.Sum(c => c.Count);

        var categories = Categories.Select(c => new CategoryDto(
            c.Key, c.Label, c.Icon,
            c.Key == "all" ? total : counts.FirstOrDefault(x => x.Type == c.Type)?.Count ?? 0)).ToList();

        var amenities = await db.Amenities
            .Where(a => a.IsFilterable)
            .OrderBy(a => a.SortOrder)
            .Select(a => new AmenityDto(a.Key, a.Label, a.Icon, a.Group))
            .ToListAsync(ct);

        var cities = await db.Listings.Select(l => l.City).Distinct().OrderBy(c => c).ToListAsync(ct);
        var prices = await db.Listings.Select(l => l.PricePerNight).ToListAsync(ct);

        var min = prices.Count == 0 ? 0 : prices.Min();
        var max = prices.Count == 0 ? 6_000_000m : prices.Max();
        var histogram = BuildHistogram(prices, min, max, 26);

        return new MetaDto(
            categories,
            amenities,
            amenities.Take(6).ToList(),
            cities,
            RoomTypes,
            Math.Floor(min / 100_000m) * 100_000m,
            Math.Ceiling(max / 100_000m) * 100_000m,
            histogram,
            Currencies,
            Languages);
    }

    private static List<int> BuildHistogram(List<decimal> prices, decimal min, decimal max, int buckets)
    {
        var result = Enumerable.Repeat(0, buckets).ToList();
        if (prices.Count == 0 || max <= min) return result;
        foreach (var p in prices)
        {
            var idx = (int)((p - min) / (max - min) * (buckets - 1));
            result[Math.Clamp(idx, 0, buckets - 1)]++;
        }
        return result;
    }

    /// <summary>
    /// The landing page mirrors Airbnb's discovery layout: horizontal carousels grouped by
    /// destination, then a few themed rows, then an inspiration link grid.
    /// </summary>
    public async Task<HomeDto> GetHomeAsync(string sessionId, CancellationToken ct)
    {
        var all = await db.Listings
            .Where(l => l.IsPublished)
            .Include(l => l.Images)
            .Include(l => l.Amenities).ThenInclude(la => la.Amenity)
            .ToListAsync(ct);

        var favIds = await FavoriteIdsAsync(sessionId, ct);
        var cards = all.Select(l => ToCard(l, favIds)).ToList();

        var sections = new List<HomeSectionDto>();

        foreach (var group in cards.GroupBy(c => c.City)
                     .Where(g => g.Count() >= 3)
                     .OrderByDescending(g => g.Count())
                     .ThenByDescending(g => g.Max(c => c.Rating))
                     .Take(5))
        {
            sections.Add(new HomeSectionDto(
                $"city-{group.Key}",
                $"Chỗ nghỉ được yêu thích ở {group.Key}",
                null,
                $"/?q={Uri.EscapeDataString(group.Key)}",
                group.OrderByDescending(c => c.Rating).ToList()));
        }

        // A rail is a horizontal scroller, not an index — keep it to a browsable length.
        const int railSize = 12;

        var pools = cards.Where(c => c.AmenityKeys.Contains("pool")).OrderByDescending(c => c.Rating).Take(railSize).ToList();
        if (pools.Count > 0)
            sections.Add(new HomeSectionDto("theme-pool", "Chỗ nghỉ có hồ bơi riêng",
                "Bơi lúc nào cũng được, không phải chia sẻ với ai", "/?amenities=pool", pools));

        var budget = cards.Where(c => c.PricePerNight <= 1_200_000m).OrderBy(c => c.PricePerNight).Take(railSize).ToList();
        if (budget.Count > 0)
            sections.Add(new HomeSectionDto("theme-budget", "Dưới 1,2 triệu mỗi đêm",
                "Tiết kiệm mà vẫn được đánh giá cao", "/?maxPrice=1200000", budget));

        var pets = cards.Where(c => c.AmenityKeys.Contains("pet")).OrderByDescending(c => c.Rating).Take(railSize).ToList();
        if (pets.Count > 0)
            sections.Add(new HomeSectionDto("theme-pet", "Cho mang theo thú cưng",
                "Đi đâu cũng có bạn bốn chân đi cùng", "/?amenities=pet", pets));

        var cities = cards.Select(c => c.City).Distinct().OrderBy(c => c).ToList();

        var inspiration = new List<InspirationGroupDto>
        {
            new("Phổ biến", cities.Take(8)
                .Select(c => new InspirationLinkDto(c, "Chỗ nghỉ nguyên căn", $"/?q={Uri.EscapeDataString(c)}")).ToList()),
            new("Ven biển", cards.Where(c => c.AmenityKeys.Contains("beach")).Select(c => c.City).Distinct().Take(8)
                .Select(c => new InspirationLinkDto(c, "Chỗ nghỉ sát biển", $"/?q={Uri.EscapeDataString(c)}")).ToList()),
            new("Vùng núi", cities.Where(c => c is "Đà Lạt" or "Sa Pa" or "Tam Đảo" or "Ninh Bình")
                .Select(c => new InspirationLinkDto(c, "Cabin & homestay vùng cao", $"/?q={Uri.EscapeDataString(c)}")).ToList()),
            new("Loại chỗ ở", Categories.Where(c => c.Key != "all")
                .Select(c => new InspirationLinkDto(c.Label, "Trên khắp Việt Nam", $"/?category={c.Key}")).ToList())
        };

        return new HomeDto(sections, inspiration.Where(g => g.Links.Count > 0).ToList());
    }

    public record SearchQuery(
        string? Q,
        string? Category,
        decimal? MinPrice,
        decimal? MaxPrice,
        int Guests,
        string[] Amenities,
        string? Sort,
        string? RoomType,
        int? Bedrooms,
        int? Beds,
        int? Bathrooms,
        bool SuperhostOnly,
        bool GuestFavoriteOnly,
        bool InstantBookOnly,
        int Page,
        int PageSize);

    public async Task<SearchResultDto> SearchAsync(SearchQuery q, string sessionId, CancellationToken ct)
    {
        IQueryable<Listing> query = db.Listings
            .Where(l => l.IsPublished)
            .Include(l => l.Images)
            .Include(l => l.Amenities).ThenInclude(la => la.Amenity);

        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var term = q.Q.Trim();
            query = query.Where(l =>
                EF.Functions.ILike(l.City, $"%{term}%") ||
                EF.Functions.ILike(l.Title, $"%{term}%") ||
                EF.Functions.ILike(l.Country, $"%{term}%"));
        }

        if (!string.IsNullOrWhiteSpace(q.Category) && q.Category != "all")
        {
            var match = Categories.FirstOrDefault(c => c.Key == q.Category);
            if (match.Key is not null && match.Key != "all")
                query = query.Where(l => l.Type == match.Type);
        }

        if (q.MinPrice is > 0) query = query.Where(l => l.PricePerNight >= q.MinPrice);
        if (q.MaxPrice is > 0) query = query.Where(l => l.PricePerNight <= q.MaxPrice);
        if (q.Guests > 0) query = query.Where(l => l.MaxGuests >= q.Guests);
        if (q.Bedrooms is > 0) query = query.Where(l => l.Bedrooms >= q.Bedrooms);
        if (q.Beds is > 0) query = query.Where(l => l.Beds >= q.Beds);
        if (q.Bathrooms is > 0) query = query.Where(l => l.Bathrooms >= q.Bathrooms);
        if (q.SuperhostOnly) query = query.Where(l => l.IsSuperhost);
        if (q.GuestFavoriteOnly) query = query.Where(l => l.IsGuestFavorite);
        if (q.InstantBookOnly) query = query.Where(l => l.InstantBook);

        if (!string.IsNullOrWhiteSpace(q.RoomType) && q.RoomType != "any")
        {
            var rt = q.RoomType switch
            {
                "entire" => RoomType.EntirePlace,
                "private" => RoomType.PrivateRoom,
                "shared" => RoomType.SharedRoom,
                _ => (RoomType?)null
            };
            if (rt is not null) query = query.Where(l => l.RoomType == rt);
        }

        foreach (var key in q.Amenities.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct())
        {
            var k = key;
            query = query.Where(l => l.Amenities.Any(la => la.Amenity!.Key == k));
        }

        query = q.Sort switch
        {
            "low" => query.OrderBy(l => l.PricePerNight).ThenBy(l => l.Id),
            "high" => query.OrderByDescending(l => l.PricePerNight).ThenBy(l => l.Id),
            "rating" => query.OrderByDescending(l => l.Rating).ThenByDescending(l => l.ReviewCount),
            "reviews" => query.OrderByDescending(l => l.ReviewCount).ThenByDescending(l => l.Rating),
            _ => query.OrderByDescending(l => l.IsGuestFavorite)
                      .ThenByDescending(l => l.Rating)
                      .ThenBy(l => l.Id)
        };

        var total = await query.CountAsync(ct);
        var pageSize = Math.Clamp(q.PageSize, 1, 60);
        var page = Math.Max(1, q.Page);

        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var favIds = await FavoriteIdsAsync(sessionId, ct);

        return new SearchResultDto(total, page, pageSize, items.Select(l => ToCard(l, favIds)).ToList());
    }

    public async Task<HashSet<int>> FavoriteIdsAsync(string sessionId, CancellationToken ct) =>
        (await db.Favorites.Where(f => f.SessionId == sessionId).Select(f => f.ListingId).ToListAsync(ct)).ToHashSet();

    public static ListingCardDto ToCard(Listing l, HashSet<int> favIds) => new(
        l.Id,
        l.Slug,
        l.Title,
        l.City,
        l.Country,
        CategoryKey(l.Type),
        CategoryLabel(l.Type),
        RoomTypeLabel(l.RoomType),
        l.Bedrooms,
        l.Beds,
        l.Bathrooms,
        l.MaxGuests,
        l.PricePerNight,
        l.DiscountPercent > 0
            ? Math.Round(l.PricePerNight * 100m / (100 - l.DiscountPercent), 0, MidpointRounding.AwayFromZero)
            : null,
        l.DiscountPercent,
        l.InstantBook,
        Math.Round(l.Rating, 2),
        l.ReviewCount,
        l.IsSuperhost,
        l.IsGuestFavorite,
        l.Latitude,
        l.Longitude,
        l.SpaceHighlight,
        l.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).ToList(),
        l.Amenities.Select(a => a.Amenity!.Key).ToList(),
        favIds.Contains(l.Id));

    public async Task<ListingDetailDto?> GetDetailAsync(string idOrSlug, string sessionId, CancellationToken ct)
    {
        var query = db.Listings
            .Include(l => l.Images)
            .Include(l => l.Amenities).ThenInclude(la => la.Amenity)
            .Include(l => l.Reviews)
            .Include(l => l.Host)
            .AsSplitQuery();

        var listing = int.TryParse(idOrSlug, out var id)
            ? await query.FirstOrDefaultAsync(l => l.Id == id, ct)
            : await query.FirstOrDefaultAsync(l => l.Slug == idOrSlug, ct);

        if (listing is null) return null;

        var favIds = await FavoriteIdsAsync(sessionId, ct);

        var groups = listing.Amenities
            .Select(a => a.Amenity!)
            .OrderBy(a => a.SortOrder)
            .GroupBy(a => a.Group)
            .Select(g => new AmenityGroupDto(g.Key,
                g.Select(a => new AmenityDto(a.Key, a.Label, a.Icon, a.Group)).ToList()))
            .ToList();

        var reviews = listing.Reviews
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReviewDto(r.Id, r.AuthorName, r.AuthorInitials, r.AuthorLocation, r.When, r.Text, Math.Round(r.Rating, 1)))
            .ToList();

        var rb = listing.Reviews.Count == 0
            ? new RatingBreakdownDto(5, 5, 5, 5, 5, 5)
            : new RatingBreakdownDto(
                Math.Round(listing.Reviews.Average(r => r.Cleanliness), 1),
                Math.Round(listing.Reviews.Average(r => r.Accuracy), 1),
                Math.Round(listing.Reviews.Average(r => r.CheckIn), 1),
                Math.Round(listing.Reviews.Average(r => r.Communication), 1),
                Math.Round(listing.Reviews.Average(r => r.Location), 1),
                Math.Round(listing.Reviews.Average(r => r.Value), 1));

        var hostListings = await db.Listings.Where(l => l.HostId == listing.HostId)
            .Select(l => new { l.Rating, l.ReviewCount }).ToListAsync(ct);

        var host = listing.Host!;
        var hostDto = new HostDto(
            host.Id, host.Name, host.Initials, host.IsSuperhost, host.YearsHosting, host.Bio,
            host.ResponseRate, host.ResponseTime,
            $"Tham gia StayHost tháng {host.JoinedAt.Month}, {host.JoinedAt.Year}",
            hostListings.Count,
            hostListings.Count == 0 ? 5 : Math.Round(hostListings.Average(h => h.Rating), 2),
            hostListings.Sum(h => h.ReviewCount),
            host.UserId);

        var similar = await db.Listings
            .Include(l => l.Images)
            .Include(l => l.Amenities).ThenInclude(la => la.Amenity)
            .Where(l => l.Id != listing.Id && (l.City == listing.City || l.Type == listing.Type))
            .OrderByDescending(l => l.Rating)
            .Take(4)
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var liveBookings = await db.Bookings
            .Where(b => b.ListingId == listing.Id && b.Status != BookingStatus.Cancelled && b.CheckOut >= today)
            .Select(b => new { b.CheckIn, b.CheckOut })
            .ToListAsync(ct);

        var hostBlocks = await db.CalendarBlocks
            .Where(b => b.ListingId == listing.Id && b.To >= today)
            .Select(b => new { From = b.From, To = b.To })
            .ToListAsync(ct);

        // A stay blocks every night from check-in up to (but excluding) check-out;
        // a host block covers both of its endpoints.
        var unavailable = liveBookings
            .SelectMany(b => Enumerable
                .Range(0, Math.Max(0, b.CheckOut.DayNumber - b.CheckIn.DayNumber))
                .Select(offset => b.CheckIn.AddDays(offset)))
            .Concat(hostBlocks.SelectMany(b => Enumerable
                .Range(0, Math.Max(0, b.To.DayNumber - b.From.DayNumber + 1))
                .Select(offset => b.From.AddDays(offset))))
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        return new ListingDetailDto(
            ToCard(listing, favIds),
            listing.Description,
            listing.CancellationPolicy,
            Split(listing.HouseRules),
            Split(listing.SafetyInfo),
            groups,
            reviews,
            rb,
            hostDto,
            similar.Select(l => ToCard(l, favIds)).ToList(),
            unavailable);
    }

    private static string[] Split(string s) =>
        s.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public async Task<QuoteDto?> QuoteAsync(int listingId, DateOnly checkIn, DateOnly checkOut, int guests, CancellationToken ct)
    {
        var l = await db.Listings.FirstOrDefaultAsync(x => x.Id == listingId, ct);
        if (l is null) return null;

        var nights = Math.Max(1, checkOut.DayNumber - checkIn.DayNumber);
        var subtotal = l.PricePerNight * nights;
        var service = Math.Round(subtotal * l.ServiceFeeRate, 0, MidpointRounding.AwayFromZero);
        var total = subtotal + l.CleaningFee + service;

        return new QuoteDto(l.Id, nights, guests, l.PricePerNight, subtotal, l.CleaningFee, service, total,
            guests > l.MaxGuests, l.MaxGuests);
    }
}
