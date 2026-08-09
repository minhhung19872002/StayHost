namespace StayHost.Domain;

/// <summary>
/// docs/01 QL-09, QL-18, CN-14 — the numbers a host is shown before they decide.
///
/// Everything here is advice, never an action: <see cref="SuggestPrice"/> hands
/// back a number the host chooses to apply (QL-09 is explicit that the platform
/// never changes the price itself), <see cref="Improvements"/> is a checklist, and
/// <see cref="EstimateIncome"/> is a what-if. None of it touches a listing.
/// </summary>
public static class HostAdvice
{
    /* ------------------------------------------------------ CN-14, income */

    public readonly record struct IncomeScenario(
        string Label, int OccupancyPercent, decimal MonthlyNet, decimal AnnualNet);

    /// <summary>
    /// docs/01 CN-14 — what a place might earn before it is even listed. Net of the
    /// host service fee of docs/03 §1, across three occupancy scenarios so nobody
    /// mistakes the rosy one for a promise. Cleaning fees are counted per stay, not
    /// per night, which is why the average stay length matters.
    /// </summary>
    public static IReadOnlyList<IncomeScenario> EstimateIncome(
        decimal pricePerNight, decimal cleaningFee, int avgStayNights = 3, decimal? hostFeeRate = null)
    {
        var fee = hostFeeRate ?? PricingSettings.Current.HostServiceFeeRate;
        var stay = Math.Max(1, avgStayNights);

        IncomeScenario At(string label, int occ)
        {
            var nights = (int)Math.Round(30m * occ / 100m, MidpointRounding.AwayFromZero);
            var stays = nights <= 0 ? 0m : Math.Max(1, (int)Math.Round((decimal)nights / stay));
            var subtotal = nights * pricePerNight + stays * cleaningFee;
            var net = Math.Round(subtotal * (1 - fee), 0, MidpointRounding.AwayFromZero);
            return new IncomeScenario(label, occ, net, net * 12);
        }

        return [At("Thận trọng", 40), At("Trung bình", 60), At("Tốt", 80)];
    }

    /* -------------------------------------------------- QL-09, suggested price */

    public readonly record struct PriceSuggestion(
        decimal SuggestedPrice, bool IsFirm, string Rationale);

    /// <summary>
    /// docs/01 QL-09 — a price to consider, drawn from what comparable places
    /// charge. The median is the anchor; too few comparables makes it a hint rather
    /// than a benchmark, and the host is told which. Never applied automatically.
    /// </summary>
    public static PriceSuggestion SuggestPrice(
        decimal currentPrice, int comparables, decimal low, decimal median, decimal high)
    {
        if (comparables < 5)
            return new PriceSuggestion(
                currentPrice, false,
                $"Chỉ có {comparables} chỗ tương đương — chưa đủ để gợi ý một mức giá chắc chắn.");

        var suggested = median;
        var rationale =
            currentPrice < low
                ? $"Giá hiện tại thấp hơn phần lớn khu vực (phổ biến {low:#,##0}–{high:#,##0}₫). " +
                  $"Cân nhắc nâng lên mức giữa {median:#,##0}₫."
            : currentPrice > high
                ? $"Giá hiện tại cao hơn phần lớn khu vực (phổ biến {low:#,##0}–{high:#,##0}₫). " +
                  $"Cân nhắc hạ về mức giữa {median:#,##0}₫ để tăng lượt đặt."
                : $"Giá hiện tại đã nằm trong khoảng phổ biến {low:#,##0}–{high:#,##0}₫. " +
                  $"Mức giữa của khu vực là {median:#,##0}₫.";

        return new PriceSuggestion(suggested, true, rationale);
    }

    /* -------------------------------------------------- QL-18, improvements */

    public enum PriceStanding { Unknown, Below, Within, Above }

    /// <summary>What the advice engine reads off a listing. No entities, so it tests cheaply.</summary>
    public readonly record struct ListingFacts(
        int PhotoCount, bool InstantBook, int DescriptionLength, int AmenityCount,
        bool HasHighlight, bool FlexibleCancellation, PriceStanding Price, double Rating, int ReviewCount);

    public readonly record struct Improvement(string Area, string Suggestion, string EstimatedImpact);

    /// <summary>
    /// docs/01 QL-18 — concrete things to fix, each with a rough sense of what it
    /// buys. The impact figures are heuristics, phrased as estimates, not
    /// guarantees; the list is ordered so the cheapest, highest-leverage fixes come
    /// first. An empty list means the listing is in good shape.
    /// </summary>
    public static IReadOnlyList<Improvement> Improvements(ListingFacts f)
    {
        var list = new List<Improvement>();

        if (f.PhotoCount < 5)
            list.Add(new("Ảnh", $"Thêm ảnh — hiện có {f.PhotoCount}, nên có tối thiểu 5 ảnh rõ nét.",
                "Ước tính +20–30% lượt xem"));
        else if (f.PhotoCount < 10)
            list.Add(new("Ảnh", "Bổ sung thêm ảnh cho đủ 10+ để khách hình dung rõ hơn.",
                "Ước tính +10% lượt xem"));

        if (f.DescriptionLength < 200)
            list.Add(new("Mô tả", "Mô tả còn ngắn — viết chi tiết hơn về không gian, tiện ích, khu vực.",
                "Ước tính +10% tỉ lệ xem→đặt"));

        if (!f.InstantBook)
            list.Add(new("Đặt ngay", "Bật Đặt ngay để khách không phải chờ duyệt.",
                "Ước tính +15% lượt đặt"));

        if (f.AmenityCount < 8)
            list.Add(new("Tiện nghi", $"Khai báo thêm tiện nghi — hiện có {f.AmenityCount}, khách lọc theo mục này.",
                "Tăng khả năng xuất hiện khi lọc"));

        if (!f.HasHighlight)
            list.Add(new("Điểm nổi bật", "Thêm một câu điểm nổi bật để tin nổi giữa danh sách.",
                "Tăng tỉ lệ nhấp vào"));

        if (f.Price == PriceStanding.Above)
            list.Add(new("Giá", "Giá đang cao hơn mặt bằng khu vực — xem gợi ý giá thị trường.",
                "Hạ về khoảng phổ biến giúp tăng lượt đặt"));

        if (!f.FlexibleCancellation)
            list.Add(new("Chính sách huỷ", "Cân nhắc chính sách huỷ linh hoạt hơn để giảm do dự khi đặt.",
                "Ước tính +5–10% chuyển đổi"));

        if (f.ReviewCount == 0)
            list.Add(new("Đánh giá", "Chưa có đánh giá — ưu đãi nhẹ cho vài lượt đặt đầu để có đánh giá.",
                "Đánh giá đầu tiên tăng độ tin cậy"));

        return list;
    }
}
