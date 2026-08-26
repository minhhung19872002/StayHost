using Microsoft.EntityFrameworkCore;
using StayHost.Domain;

namespace StayHost.Infrastructure;

/// <summary>
/// Populates the catalogue on first run. Idempotent: it bails out as soon as any listing exists.
/// </summary>
public static class DbSeeder
{
    /// <summary>Pexels ids reused across the catalogue; every one of them is a known-good photo.</summary>
    private static readonly int[] Pool =
    [
        1029599, 271624, 261102, 271639, 1571460, 271816, 323780, 106399, 275484, 210617,
        1643383, 302769, 1974596, 271618, 247431, 1080721, 2082087, 276724, 261101, 271643,
        338504, 1571453, 1643384, 1457842, 2029667, 439227, 164595, 259588, 1454804, 3155581,
        271795, 1457847, 209315, 462235, 276554, 271815, 533182, 258154, 280229, 1080696,
        323772, 1918291, 1732414, 2506988, 89927, 1268871, 1571468, 269077, 342800, 731082
    ];

    private static string Pic(int id, int w) =>
        $"https://images.pexels.com/photos/{id}/pexels-photo-{id}.jpeg?auto=compress&cs=tinysrgb&fit=crop&w={w}";

    private record AmenitySeed(string Key, string Label, string Icon, string Group, bool Filterable);

    private static readonly AmenitySeed[] AmenitySeeds =
    [
        new("pool",       "Hồ bơi",              "◎", "Nổi bật",   true),
        new("wifi",       "Wifi tốc độ cao",     "≋", "Nổi bật",   true),
        new("kitchen",    "Bếp đầy đủ",          "◍", "Nổi bật",   true),
        new("parking",    "Chỗ đậu xe miễn phí", "▢", "Nổi bật",   true),
        new("ac",         "Máy lạnh",            "❄", "Nổi bật",   true),
        new("pet",        "Cho mang thú nuôi",   "☘", "Nổi bật",   true),
        new("washer",     "Máy giặt",            "◌", "Tiện nghi", true),
        new("tv",         "TV màn hình phẳng",   "▭", "Tiện nghi", true),
        new("workspace",  "Góc làm việc riêng",  "▥", "Tiện nghi", true),
        new("gym",        "Phòng gym",           "◆", "Tiện nghi", true),
        new("bbq",        "Khu BBQ ngoài trời",  "♨", "Ngoài trời", true),
        new("hottub",     "Bồn tắm nước nóng",   "≈", "Ngoài trời", true),
        new("fire",       "Lò sưởi",             "▲", "Tiện nghi", false),
        new("bike",       "Xe đạp miễn phí",     "◇", "Ngoài trời", false),
        new("beach",      "Sát biển",            "⌒", "Ngoài trời", false),
        new("breakfast",  "Bữa sáng miễn phí",   "☕", "Dịch vụ",   false),
        new("selfcheckin", "Tự nhận phòng",      "⚿", "Dịch vụ",   false),
        new("crib",       "Nôi cho em bé",       "☖", "Gia đình",  false),
        new("view",       "View đẹp",            "◬", "Nổi bật",   false),
        new("ev",         "Sạc xe điện",         "⚡", "Ngoài trời", false),
        // docs/01 TM-17 — accessibility, its own filterable group.
        new("step-free",  "Lối vào bằng phẳng",  "▱", "Tiếp cận",  true),
        new("elevator",   "Thang máy",           "▤", "Tiếp cận",  true),
        new("wide-door",  "Cửa rộng cho xe lăn", "◫", "Tiếp cận",  true),
        new("grab-bars",  "Tay vịn trong phòng tắm", "▬", "Tiếp cận", true),
        new("ground-floor","Phòng tầng trệt",    "▦", "Tiếp cận",  true)
    ];

    private record GuideSeed(GuidebookCategory Category, string Name, string Note, string? Address);

    /// <summary>
    /// docs/01 TĐ-22 — a starter guidebook per city, so the section on the listing
    /// page has something in it in the demo. Real hosts write their own; nothing
    /// here is generated for them at publish time.
    ///
    /// The entries carry no coordinates on purpose: an invented pin would put a
    /// real-sounding restaurant at a wrong address on a map, and
    /// <see cref="Guidebooks.HasPin"/> already treats "no pin" as ordinary.
    /// </summary>
    private static readonly Dictionary<string, GuideSeed[]> GuidebookSeeds = new()
    {
        ["Đà Nẵng"] =
        [
            new(GuidebookCategory.Food, "Bún chả cá 109 Nguyễn Chí Thanh",
                "Ăn sáng ở đây, gọi thêm chả bò. Đông nhất 7–8h, đi trước 7h là vừa.", "109 Nguyễn Chí Thanh"),
            new(GuidebookCategory.Cafe, "Cà phê muối Bảo Trâm",
                "Ngồi tầng hai nhìn ra sông Hàn. Mình hay ngồi đây làm việc buổi chiều.", null),
            new(GuidebookCategory.Sightseeing, "Cầu Rồng phun lửa",
                "Chỉ phun tối thứ Bảy và Chủ nhật, 21h. Đứng bờ đông ít đông hơn bờ tây.", null),
            new(GuidebookCategory.Nature, "Bán đảo Sơn Trà lúc bình minh",
                "Thuê xe máy đi sớm, 5h30 xuất phát là kịp. Đừng đi khi trời mưa, đường trơn.", null),
            new(GuidebookCategory.Transport, "Đi sân bay",
                "Taxi từ nhà ra sân bay khoảng 15 phút, 90–120k. Gọi hộ được nếu bạn nhắn trước một hôm.", null)
        ],
        ["Hội An"] =
        [
            new(GuidebookCategory.Food, "Cao lầu bà Bé",
                "Quán trong chợ, không có biển to. Hỏi người bán hàng ai cũng chỉ được.", "Chợ Hội An"),
            new(GuidebookCategory.Cafe, "Cà phê Mót",
                "Nước thảo mộc chứ không phải cà phê. Rẻ, mát, ngay góc phố cổ.", null),
            new(GuidebookCategory.Sightseeing, "Phố cổ sau 21h",
                "Đoàn khách đã về, đèn lồng vẫn sáng. Đây là lúc phố cổ đẹp nhất.", null),
            new(GuidebookCategory.Nature, "Biển An Bàng",
                "Đạp xe 15 phút từ nhà, có xe đạp miễn phí. Ít người hơn Cửa Đại.", null),
            new(GuidebookCategory.Tip, "Vé tham quan phố cổ",
                "Mua ở quầy đầu đường, dùng được cho 5 điểm. Đừng mua lại của người mời chào giữa phố.", null)
        ],
        ["Đà Lạt"] =
        [
            new(GuidebookCategory.Food, "Bánh căn Lệ",
                "Ăn tối, gọi bánh căn trứng cút chấm nước mắm xíu mại. Đi trước 18h30.", "27/44 Yersin"),
            new(GuidebookCategory.Cafe, "Cà phê ngắm mây Cầu Đất",
                "Đi từ 5h sáng mới có mây. Cách nhà 25km, đường dễ đi.", null),
            new(GuidebookCategory.Nature, "Hồ Tuyền Lâm buổi sớm",
                "Sương trên mặt hồ trước 7h. Mang áo ấm, dưới 15 độ.", null),
            new(GuidebookCategory.Shopping, "Chợ đêm Đà Lạt",
                "Mua dâu và hồng treo gió ở dãy trong, ngoài mặt tiền đắt hơn khoảng 30%.", null)
        ],
        ["Hà Nội"] =
        [
            new(GuidebookCategory.Food, "Phở Bát Đàn",
                "Xếp hàng tự bưng. Đi trước 8h, sau đó hết nước dùng ngon.", "49 Bát Đàn"),
            new(GuidebookCategory.Cafe, "Cà phê trứng Giảng",
                "Vào ngõ, đi hết mới tới quán. Gọi thêm ca cao trứng cho người không uống cà phê.", "39 Nguyễn Hữu Huân"),
            new(GuidebookCategory.Sightseeing, "Hồ Gươm cuối tuần",
                "Tối thứ Sáu đến Chủ nhật cấm xe, đi bộ được cả vòng hồ.", null),
            new(GuidebookCategory.Transport, "Xe buýt sân bay 86",
                "Rẻ hơn taxi nhiều, chạy 5h–22h, dừng ngay bờ hồ.", null)
        ],
        ["TP. Hồ Chí Minh"] =
        [
            new(GuidebookCategory.Food, "Cơm tấm Ba Ghiền",
                "Sườn to hơn đĩa. Trưa rất đông, đi 11h hoặc sau 13h30.", "84 Đặng Văn Ngữ"),
            new(GuidebookCategory.Cafe, "Chung cư cà phê 42 Nguyễn Huệ",
                "Cả toà nhà là quán. Lên tầng cao nhìn xuống phố đi bộ.", "42 Nguyễn Huệ"),
            new(GuidebookCategory.Nightlife, "Bùi Viện",
                "Ồn và đông. Nếu muốn yên hơn thì sang khu Thảo Điền.", null),
            new(GuidebookCategory.Tip, "Đi lại trong thành phố",
                "Dùng ứng dụng gọi xe, đừng bắt taxi dọc đường ở khu trung tâm.", null)
        ],
        ["Nha Trang"] =
        [
            new(GuidebookCategory.Food, "Bún cá Nguyên Loan",
                "Gọi bún cá sứa. Ăn sáng, 6h–10h là ngon nhất.", "123 Ngô Gia Tự"),
            new(GuidebookCategory.Nature, "Bãi Dài",
                "Cách trung tâm 25km, vắng và sạch hơn bãi trước thành phố.", null),
            new(GuidebookCategory.Sightseeing, "Tháp Bà Ponagar",
                "Đi buổi chiều muộn, đỡ nắng và ngược sáng đẹp.", null)
        ],
        ["Huế"] =
        [
            new(GuidebookCategory.Food, "Bún bò Mụ Rơi",
                "Người Huế ăn ở đây chứ không ăn ở quán trên đường du lịch.", null),
            new(GuidebookCategory.Sightseeing, "Đại Nội lúc mở cửa",
                "7h sáng gần như không có ai. Sau 9h là đoàn kéo tới.", null),
            new(GuidebookCategory.Nature, "Sông Hương đoạn Thiên Mụ",
                "Thuê thuyền rồng buổi chiều, thương lượng giá trước khi lên.", null)
        ],
        ["Phú Quốc"] =
        [
            new(GuidebookCategory.Food, "Chợ đêm Dinh Cậu",
                "Hải sản tươi, nhưng hỏi giá theo ký trước khi gật đầu.", null),
            new(GuidebookCategory.Nature, "Bãi Sao",
                "Đi buổi sáng, chiều gió lên là nước đục.", null),
            new(GuidebookCategory.Transport, "Thuê xe máy",
                "Mình gửi được xe tận nhà, nhắn trước một ngày. Đường ra Bãi Sao khá xấu.", null)
        ],
        ["Sa Pa"] =
        [
            new(GuidebookCategory.Food, "Lẩu cá tầm",
                "Ăn tối cho ấm. Quán nào ở khu trung tâm cũng làm được, gọi thêm rau cải mèo.", null),
            new(GuidebookCategory.Nature, "Bản Cát Cát đi bộ",
                "Đi bộ xuống, bắt xe ôm lên. Xuống thì dễ, lên thì dốc.", null),
            new(GuidebookCategory.Tip, "Thời tiết",
                "Sáng nắng chiều mưa quanh năm. Luôn mang theo áo mưa mỏng.", null)
        ]
    };

    /// <summary>docs/01 TM-17 — which accessibility features a demo listing advertises.</summary>
    private static string[] AccessibilityFor(int i) => (i % 4) switch
    {
        0 => ["step-free", "elevator", "wide-door"],
        1 => ["ground-floor", "step-free"],
        2 => ["elevator"],
        _ => []
    };

    private record HostSeed(string Name, bool Superhost, int Years, string Bio);

    private static readonly HostSeed[] HostSeeds =
    [
        new("Minh Trần",   true,  6, "Mình sống ở Đà Nẵng và cho thuê nhà từ 2019. Rất vui được đón bạn."),
        new("Hà Nguyễn",   true,  5, "Yêu Đà Lạt, yêu cà phê sáng sớm và những vị khách kể chuyện hay."),
        new("Linh Phạm",   false, 3, "Mình quản lý vài căn hộ view biển ở Nha Trang."),
        new("Tuấn Lê",     false, 4, "Nhà vườn của gia đình mình, được cải tạo lại cho khách nghỉ dưỡng."),
        new("Khang Đỗ",    true,  7, "Chuyên căn hộ cao cấp tại TP.HCM, phản hồi trong vài phút."),
        new("Mai Vũ",      false, 2, "Người Dao đỏ, mình làm homestay để giới thiệu văn hoá bản địa."),
        new("Quyên Hoàng", true,  8, "Hội An là nhà, mình sẽ chỉ bạn những quán ăn không có trên bản đồ."),
        new("Duy Bùi",     true,  6, "Villa của mình ở Phú Quốc có đầu bếp riêng theo yêu cầu."),
        new("An Nguyễn",   true,  4, "Mình yêu kiến trúc cũ Hà Nội và phục dựng lại từng căn."),
        new("Thảo Trịnh",  false, 3, "Chào bạn, mình ở Huế và luôn sẵn sàng gợi ý lịch trình.")
    ];

    private record ListingSeed(
        string Title, string City, PlaceType Type, RoomType Room,
        int Bedrooms, int Beds, int Baths, int MaxGuests, decimal Price,
        double Rating, int Reviews, bool Superhost, bool GuestFavorite,
        int HostIndex, double Lat, double Lng, string[] Amenities, string Desc, string Highlight);

    private static readonly ListingSeed[] ListingSeeds =
    [
        new("Sunset Villa hồ bơi riêng", "Đà Nẵng", PlaceType.Villa, RoomType.EntirePlace,
            4, 5, 3, 8, 3_200_000m, 4.92, 64, true, true, 0, 16.0544, 108.2022,
            ["pool", "wifi", "kitchen", "parking", "ac", "bbq", "washer", "tv", "beach", "view"],
            "Villa hai tầng cách biển Mỹ Khê 6 phút đi bộ, hồ bơi riêng và sân vườn rộng. Phù hợp cho gia đình hoặc nhóm bạn muốn không gian yên tĩnh nhưng vẫn gần trung tâm.",
            "Hồ bơi riêng nhìn ra vườn dừa"),

        new("Lakeview Retreat gỗ ấm", "Đà Lạt", PlaceType.Homestay, RoomType.EntirePlace,
            2, 3, 2, 4, 1_450_000m, 4.88, 57, true, true, 1, 11.9404, 108.4583,
            ["wifi", "kitchen", "fire", "pet", "breakfast", "view", "parking"],
            "Nhà gỗ nhìn ra hồ Tuyền Lâm, lò sưởi thật và hiên ngắm sương sớm. Bữa sáng do chủ nhà chuẩn bị mỗi ngày.",
            "Lò sưởi thật và hiên ngắm sương"),

        new("Palm Paradise căn hộ biển", "Nha Trang", PlaceType.Apartment, RoomType.EntirePlace,
            2, 2, 1, 4, 980_000m, 4.90, 38, false, false, 2, 12.2388, 109.1967,
            ["wifi", "pool", "ac", "gym", "workspace", "tv", "washer", "view"],
            "Căn hộ tầng 18 view vịnh, bàn làm việc rộng và internet 300Mbps — lý tưởng cho khách làm việc từ xa dài ngày.",
            "Internet 300Mbps, view vịnh tầng 18"),

        new("Desert Oasis nhà mái ngói", "Phan Thiết", PlaceType.House, RoomType.EntirePlace,
            3, 4, 2, 6, 1_750_000m, 4.85, 42, false, false, 3, 10.9280, 108.1020,
            ["pool", "parking", "kitchen", "bbq", "ac", "wifi", "ev"],
            "Nhà vườn cát trắng, bể bơi nước mặn và khu BBQ ngoài trời. Cách đồi cát Bàu Trắng 20 phút xe.",
            "Bể bơi nước mặn giữa vườn cát"),

        new("Camelback Views penthouse", "TP. Hồ Chí Minh", PlaceType.Apartment, RoomType.EntirePlace,
            3, 4, 2, 6, 4_100_000m, 4.93, 65, true, true, 4, 10.7769, 106.7009,
            ["wifi", "gym", "ac", "parking", "pool", "workspace", "tv", "washer", "selfcheckin"],
            "Penthouse hai tầng nhìn toàn thành phố, thang máy riêng và dịch vụ dọn phòng hằng ngày.",
            "Thang máy riêng lên tầng thượng"),

        new("Ruộng bậc thang bungalow", "Sa Pa", PlaceType.Homestay, RoomType.PrivateRoom,
            1, 1, 1, 2, 720_000m, 4.79, 31, false, false, 5, 22.3364, 103.8438,
            ["wifi", "fire", "kitchen", "breakfast", "view", "hottub"],
            "Bungalow tre nhìn thẳng ra thung lũng Mường Hoa, bồn tắm lá thuốc người Dao đỏ.",
            "Bồn tắm lá thuốc người Dao đỏ"),

        new("Riverside Loft phố cổ", "Hội An", PlaceType.House, RoomType.EntirePlace,
            2, 3, 2, 5, 1_290_000m, 4.91, 73, true, true, 6, 15.8801, 108.3380,
            ["wifi", "kitchen", "bike", "ac", "washer", "tv", "selfcheckin"],
            "Nhà cải tạo từ kho gạo bên sông Thu Bồn, xe đạp miễn phí và 4 phút đi bộ tới Chùa Cầu.",
            "Xe đạp miễn phí, 4 phút tới Chùa Cầu"),

        new("Cliffside Villa toàn cảnh vịnh", "Phú Quốc", PlaceType.Villa, RoomType.EntirePlace,
            5, 6, 4, 10, 5_400_000m, 4.96, 48, true, true, 7, 10.2270, 103.9670,
            ["pool", "wifi", "bbq", "parking", "ac", "kitchen", "beach", "view", "hottub", "tv"],
            "Villa trên mỏm đá riêng biệt với hồ bơi vô cực, đầu bếp theo yêu cầu và đường xuống bãi tắm riêng.",
            "Hồ bơi vô cực trên vách đá"),

        new("Nhà cổ Hàng Bạc cải tạo", "Hà Nội", PlaceType.House, RoomType.EntirePlace,
            2, 2, 2, 4, 1_150_000m, 4.87, 91, true, true, 8, 21.0278, 105.8342,
            ["wifi", "kitchen", "ac", "washer", "tv", "workspace", "selfcheckin"],
            "Nhà ống phố cổ 1930 được phục dựng nguyên bản: gạch bông, cửa gỗ lim và giếng trời đón nắng.",
            "Giếng trời và gạch bông nguyên bản"),

        new("Garden Suite bên sông Hương", "Huế", PlaceType.Homestay, RoomType.PrivateRoom,
            1, 2, 1, 3, 650_000m, 4.83, 44, false, false, 9, 16.4637, 107.5909,
            ["wifi", "breakfast", "bike", "ac", "kitchen", "parking"],
            "Phòng riêng trong nhà vườn kiểu Huế, ăn sáng bún bò do mẹ chủ nhà nấu.",
            "Bún bò sáng do gia đình nấu"),

        new("Ocean Deck căn hộ studio", "Vũng Tàu", PlaceType.Apartment, RoomType.EntirePlace,
            1, 1, 1, 2, 780_000m, 4.76, 29, false, false, 2, 10.3460, 107.0843,
            ["wifi", "ac", "tv", "pool", "gym", "beach", "selfcheckin"],
            "Studio ban công hướng Bãi Sau, thức dậy là thấy bình minh trên biển.",
            "Ban công đón bình minh Bãi Sau"),

        new("Eo Gió Cabin gỗ thông", "Quy Nhơn", PlaceType.Cabin, RoomType.EntirePlace,
            2, 3, 1, 5, 1_050_000m, 4.89, 36, true, false, 3, 13.7829, 109.2196,
            ["wifi", "kitchen", "bbq", "parking", "view", "pet", "fire"],
            "Cabin gỗ thông trên đồi, chỉ 10 phút xe tới Eo Gió và Kỳ Co.",
            "Đồi thông riêng nhìn ra vịnh"),

        new("Tràng An Riverside homestay", "Ninh Bình", PlaceType.Homestay, RoomType.EntirePlace,
            2, 3, 2, 5, 890_000m, 4.84, 52, false, false, 5, 20.2506, 105.9745,
            ["wifi", "kitchen", "bike", "parking", "breakfast", "view", "crib"],
            "Nhà sàn cạnh bến thuyền Tràng An, sáng chèo thuyền, chiều đạp xe qua đồng lúa.",
            "Bến thuyền Tràng An ngay trước nhà"),

        new("Bay Window Suite vịnh Hạ Long", "Hạ Long", PlaceType.Apartment, RoomType.EntirePlace,
            2, 2, 2, 4, 1_650_000m, 4.90, 61, true, true, 4, 20.9101, 107.1839,
            ["wifi", "ac", "gym", "pool", "tv", "view", "parking", "washer"],
            "Cửa sổ kính suốt trần nhìn thẳng ra vịnh, tầng 25 khu Bãi Cháy.",
            "Cửa sổ kính suốt trần nhìn ra vịnh"),

        new("Côn Đảo Beach Bungalow", "Côn Đảo", PlaceType.Boutique, RoomType.EntirePlace,
            1, 2, 1, 3, 2_400_000m, 4.95, 27, true, true, 7, 8.6833, 106.6167,
            ["beach", "wifi", "ac", "breakfast", "pool", "view", "hottub"],
            "Bungalow cách mép nước 30 bước chân, buổi tối nghe sóng và ngắm sao rất rõ.",
            "Cách mép nước đúng 30 bước chân"),

        new("Mekong Loft gạch trần", "Cần Thơ", PlaceType.House, RoomType.EntirePlace,
            3, 3, 2, 6, 950_000m, 4.81, 33, false, false, 8, 10.0452, 105.7469,
            ["wifi", "kitchen", "ac", "parking", "washer", "tv", "bike"],
            "Loft gạch trần bên rạch nhỏ, 5 phút xe tới chợ nổi Cái Răng lúc rạng sáng.",
            "5 phút tới chợ nổi Cái Răng"),

        new("Sand Dune Villa hồ bơi vô cực", "Mũi Né", PlaceType.Villa, RoomType.EntirePlace,
            4, 5, 3, 9, 2_900_000m, 4.88, 55, true, false, 3, 10.9330, 108.2870,
            ["pool", "bbq", "wifi", "kitchen", "parking", "ac", "beach", "ev", "tv"],
            "Villa sát biển Mũi Né với hồ bơi vô cực và ván lướt gió cho khách mượn.",
            "Ván lướt gió cho khách mượn"),

        new("Tam Đảo Cloud Cabin", "Tam Đảo", PlaceType.Cabin, RoomType.EntirePlace,
            2, 2, 1, 4, 1_180_000m, 4.86, 40, false, false, 1, 21.4560, 105.6440,
            ["fire", "wifi", "kitchen", "view", "parking", "pet", "hottub"],
            "Cabin trên độ cao 900m, buổi sáng mây tràn vào tận hiên nhà.",
            "Mây tràn vào hiên mỗi sáng sớm"),

        // --- Đà Nẵng ---------------------------------------------------------
        new("An Thượng Studio ban công", "Đà Nẵng", PlaceType.Apartment, RoomType.EntirePlace,
            1, 1, 1, 2, 640_000m, 4.81, 88, false, false, 0, 16.0470, 108.2470,
            ["wifi", "ac", "kitchen", "workspace", "washer", "selfcheckin"],
            "Studio trong hẻm An Thượng, quán cà phê và bãi biển đều trong 5 phút đi bộ.", "Khu An Thượng nhiều quán xá"),
        new("Marble Mountain Villa 3 tầng", "Đà Nẵng", PlaceType.Villa, RoomType.EntirePlace,
            4, 6, 4, 9, 2_650_000m, 4.89, 51, true, true, 0, 16.0025, 108.2630,
            ["pool", "wifi", "kitchen", "parking", "ac", "bbq", "tv", "ev"],
            "Villa dưới chân Ngũ Hành Sơn, hồ bơi dài 12m và phòng karaoke riêng.", "Hồ bơi dài 12m, phòng karaoke"),
        new("Han River Loft tầng 22", "Đà Nẵng", PlaceType.Apartment, RoomType.EntirePlace,
            2, 3, 2, 5, 1_380_000m, 4.87, 64, true, false, 4, 16.0678, 108.2270,
            ["wifi", "ac", "gym", "pool", "view", "workspace", "washer", "tv"],
            "Nhìn thẳng cầu Rồng, tối thứ Bảy xem phun lửa ngay từ ban công.", "Xem cầu Rồng phun lửa từ ban công"),
        new("Non Nuoc Beach House", "Đà Nẵng", PlaceType.House, RoomType.EntirePlace,
            3, 4, 2, 7, 1_890_000m, 4.84, 39, false, false, 3, 16.0010, 108.2740,
            ["beach", "wifi", "kitchen", "parking", "ac", "bbq", "crib"],
            "Nhà sát bãi Non Nước, cửa sau mở thẳng ra cát.", "Cửa sau mở thẳng ra bãi cát"),
        new("Phòng riêng nhà chủ Sơn Trà", "Đà Nẵng", PlaceType.Homestay, RoomType.PrivateRoom,
            1, 1, 1, 2, 380_000m, 4.72, 120, false, false, 5, 16.0990, 108.2660,
            ["wifi", "ac", "breakfast", "bike", "kitchen"],
            "Phòng riêng trong nhà chủ dưới chân Sơn Trà, có xe máy cho thuê giá gốc.", "Chủ nhà dẫn đi ăn hải sản"),

        // --- Đà Lạt ----------------------------------------------------------
        new("Nhà kính giữa vườn hồng", "Đà Lạt", PlaceType.Cabin, RoomType.EntirePlace,
            1, 1, 1, 2, 1_320_000m, 4.94, 76, true, true, 1, 11.9550, 108.4420,
            ["fire", "wifi", "kitchen", "view", "hottub", "parking", "pet"],
            "Nhà kính mái trong suốt giữa vườn hồng, đêm nằm ngắm sao không cần ra ngoài.", "Mái kính ngắm sao từ giường"),
        new("Măng Đen Wood House", "Đà Lạt", PlaceType.House, RoomType.EntirePlace,
            3, 4, 2, 6, 1_680_000m, 4.85, 47, false, false, 1, 11.9280, 108.4700,
            ["fire", "wifi", "kitchen", "parking", "bbq", "pet", "view"],
            "Nhà gỗ thông ba phòng ngủ, sân đốt lửa trại và bếp nướng ngoài trời.", "Sân đốt lửa trại riêng"),
        new("Studio dốc Nhà Làng", "Đà Lạt", PlaceType.Apartment, RoomType.EntirePlace,
            1, 1, 1, 2, 590_000m, 4.78, 93, false, false, 9, 11.9420, 108.4380,
            ["wifi", "kitchen", "workspace", "tv", "washer", "selfcheckin"],
            "Studio trên con dốc Nhà Làng, đi bộ 3 phút tới chợ đêm.", "3 phút đi bộ tới chợ đêm"),
        new("Pine Hill Villa hồ bơi nước nóng", "Đà Lạt", PlaceType.Villa, RoomType.EntirePlace,
            4, 5, 3, 8, 3_450_000m, 4.91, 34, true, true, 1, 11.9180, 108.4520,
            ["pool", "hottub", "fire", "wifi", "kitchen", "parking", "bbq", "view"],
            "Villa đồi thông với hồ bơi nước nóng ngoài trời, hơi nước bốc lên giữa sương sớm.", "Hồ bơi nước nóng giữa rừng thông"),

        // --- Hội An ----------------------------------------------------------
        new("An Bàng Garden Bungalow", "Hội An", PlaceType.Homestay, RoomType.EntirePlace,
            1, 2, 1, 3, 860_000m, 4.90, 112, true, true, 6, 15.9110, 108.3480,
            ["beach", "wifi", "bike", "breakfast", "ac", "kitchen"],
            "Bungalow trong vườn cách biển An Bàng 4 phút đạp xe.", "4 phút đạp xe ra biển An Bàng"),
        new("Nhà rường gỗ mít 100 năm", "Hội An", PlaceType.House, RoomType.EntirePlace,
            3, 3, 2, 6, 2_150_000m, 4.93, 58, true, true, 6, 15.8770, 108.3270,
            ["wifi", "ac", "kitchen", "bike", "parking", "washer", "view"],
            "Nhà rường gỗ mít nguyên bản, sân gạch Bát Tràng và giếng cổ còn dùng được.", "Nhà gỗ mít nguyên bản 100 năm"),
        new("Tra Que Farmstay", "Hội An", PlaceType.Homestay, RoomType.PrivateRoom,
            1, 1, 1, 2, 520_000m, 4.86, 74, false, false, 9, 15.9040, 108.3220,
            ["wifi", "breakfast", "bike", "ac", "pet"],
            "Ở cùng gia đình trồng rau Trà Quế, sáng ra vườn hái rau nấu ăn.", "Hái rau Trà Quế mỗi sáng"),
        new("Riverfront Pool Villa Cẩm Thanh", "Hội An", PlaceType.Villa, RoomType.EntirePlace,
            4, 5, 4, 8, 2_980_000m, 4.92, 41, true, false, 6, 15.8830, 108.3730,
            ["pool", "wifi", "kitchen", "parking", "ac", "bbq", "bike", "tv"],
            "Villa bên rừng dừa Cẩm Thanh, thuyền thúng đón khách ngay bến trước nhà.", "Bến thuyền thúng trước nhà"),

        // --- Nha Trang -------------------------------------------------------
        new("Vĩnh Hòa Sea View 2PN", "Nha Trang", PlaceType.Apartment, RoomType.EntirePlace,
            2, 3, 2, 5, 1_120_000m, 4.83, 67, false, false, 2, 12.2720, 109.1990,
            ["wifi", "ac", "pool", "gym", "view", "kitchen", "washer", "parking"],
            "Căn hộ hai phòng ngủ hướng vịnh Nha Trang, hồ bơi vô cực tầng 30.", "Hồ bơi vô cực tầng 30"),
        new("Hòn Chồng Boutique Room", "Nha Trang", PlaceType.Boutique, RoomType.PrivateRoom,
            1, 1, 1, 2, 780_000m, 4.88, 55, true, false, 2, 12.2670, 109.2040,
            ["wifi", "ac", "breakfast", "beach", "tv", "selfcheckin"],
            "Phòng boutique nhìn ra Hòn Chồng, thiết kế tối giản gỗ và đá.", "View Hòn Chồng từ giường"),
        new("Bãi Dài Pool Villa", "Nha Trang", PlaceType.Villa, RoomType.EntirePlace,
            4, 5, 3, 9, 3_650_000m, 4.94, 29, true, true, 7, 12.1690, 109.1930,
            ["pool", "beach", "wifi", "kitchen", "bbq", "parking", "ac", "ev"],
            "Villa sát Bãi Dài, hồ bơi riêng và lối đi bộ 60 giây xuống biển.", "60 giây đi bộ xuống biển"),

        // --- TP. Hồ Chí Minh -------------------------------------------------
        new("Thảo Điền Garden Apartment", "TP. Hồ Chí Minh", PlaceType.Apartment, RoomType.EntirePlace,
            2, 2, 2, 4, 1_450_000m, 4.86, 82, true, false, 4, 10.8030, 106.7370,
            ["wifi", "ac", "pool", "gym", "kitchen", "workspace", "washer", "parking"],
            "Căn hộ Thảo Điền yên tĩnh, nhiều cây xanh, gần trường quốc tế và quán brunch.", "Khu Thảo Điền nhiều cây xanh"),
        new("Studio Bùi Viện trung tâm Q1", "TP. Hồ Chí Minh", PlaceType.Apartment, RoomType.EntirePlace,
            1, 1, 1, 2, 690_000m, 4.74, 143, false, false, 4, 10.7670, 106.6930,
            ["wifi", "ac", "tv", "washer", "selfcheckin", "workspace"],
            "Studio ngay Bùi Viện, cách chợ Bến Thành 8 phút đi bộ.", "Ngay trung tâm Quận 1"),
        new("Penthouse Landmark 81 view sông", "TP. Hồ Chí Minh", PlaceType.Apartment, RoomType.EntirePlace,
            3, 4, 3, 6, 5_200_000m, 4.95, 37, true, true, 4, 10.7950, 106.7220,
            ["wifi", "ac", "pool", "gym", "view", "parking", "kitchen", "tv", "washer"],
            "Penthouse nhìn thẳng Landmark 81 và khúc sông Sài Gòn.", "View Landmark 81 và sông Sài Gòn"),
        new("Nhà phố Bình Thạnh cho nhóm", "TP. Hồ Chí Minh", PlaceType.House, RoomType.EntirePlace,
            4, 6, 3, 10, 2_100_000m, 4.79, 46, false, false, 8, 10.8030, 106.7100,
            ["wifi", "ac", "kitchen", "parking", "washer", "tv", "bbq", "crib"],
            "Nhà phố bốn tầng cho nhóm đông, sân thượng nướng BBQ ngắm thành phố.", "Sân thượng BBQ cho 10 người"),

        // --- Hà Nội ----------------------------------------------------------
        new("Studio Tây Hồ view hoàng hôn", "Hà Nội", PlaceType.Apartment, RoomType.EntirePlace,
            1, 1, 1, 2, 850_000m, 4.88, 97, true, true, 8, 21.0640, 105.8200,
            ["wifi", "ac", "kitchen", "workspace", "washer", "view", "selfcheckin"],
            "Studio mặt hồ Tây, chiều nào cũng có hoàng hôn đỏ rực qua cửa sổ.", "Hoàng hôn hồ Tây qua cửa sổ"),
        new("Căn hộ Ba Đình 2PN gia đình", "Hà Nội", PlaceType.Apartment, RoomType.EntirePlace,
            2, 3, 2, 5, 1_240_000m, 4.82, 63, false, false, 8, 21.0350, 105.8340,
            ["wifi", "ac", "kitchen", "parking", "crib", "washer", "tv"],
            "Căn hộ gia đình gần Lăng Bác và Văn Miếu, có nôi và ghế ăn cho bé.", "Đầy đủ đồ dùng cho em bé"),
        new("Biệt thự Pháp cổ Hoàn Kiếm", "Hà Nội", PlaceType.Boutique, RoomType.EntirePlace,
            3, 4, 3, 6, 3_900_000m, 4.96, 44, true, true, 8, 21.0250, 105.8520,
            ["wifi", "ac", "kitchen", "parking", "breakfast", "view", "washer", "tv"],
            "Biệt thự Pháp cổ được phục dựng, trần cao 4m và cầu thang gỗ nguyên bản.", "Kiến trúc Pháp cổ phục dựng"),

        // --- Phú Quốc & Vũng Tàu ---------------------------------------------
        new("Ông Lang Beach Bungalow", "Phú Quốc", PlaceType.Homestay, RoomType.EntirePlace,
            1, 2, 1, 3, 1_150_000m, 4.87, 69, true, false, 7, 10.2540, 103.9270,
            ["beach", "wifi", "ac", "breakfast", "kitchen", "view"],
            "Bungalow gỗ trên bãi Ông Lang, hoàng hôn đẹp nhất đảo.", "Hoàng hôn bãi Ông Lang"),
        new("Sunset Town Studio", "Phú Quốc", PlaceType.Apartment, RoomType.EntirePlace,
            1, 1, 1, 2, 720_000m, 4.75, 51, false, false, 7, 10.0400, 103.8320,
            ["wifi", "ac", "pool", "tv", "selfcheckin", "gym"],
            "Studio ở Sunset Town, đi bộ ra cầu Hôn xem hoàng hôn mỗi tối.", "Đi bộ ra cầu Hôn"),
        new("Bãi Trường Family Villa", "Phú Quốc", PlaceType.Villa, RoomType.EntirePlace,
            3, 4, 3, 7, 2_750_000m, 4.90, 43, true, true, 7, 10.1450, 103.9660,
            ["pool", "beach", "wifi", "kitchen", "ac", "bbq", "parking", "crib"],
            "Villa gia đình sát Bãi Trường, hồ bơi có phần nông riêng cho trẻ nhỏ.", "Hồ bơi có khu nông cho bé"),
        new("Ocean Vista 2PN Bãi Sau", "Vũng Tàu", PlaceType.Apartment, RoomType.EntirePlace,
            2, 2, 2, 4, 1_050_000m, 4.80, 58, false, false, 2, 10.3390, 107.0930,
            ["wifi", "ac", "pool", "beach", "kitchen", "parking", "tv"],
            "Căn hộ hai phòng ngủ ngay Bãi Sau, xuống thang máy là ra biển.", "Xuống thang máy là ra biển"),
        new("Villa hồ bơi Hồ Tràm", "Vũng Tàu", PlaceType.Villa, RoomType.EntirePlace,
            4, 5, 3, 10, 3_100_000m, 4.89, 36, true, false, 3, 10.4650, 107.3140,
            ["pool", "beach", "bbq", "wifi", "kitchen", "parking", "ac", "pet"],
            "Villa Hồ Tràm cho nhóm lớn, hồ bơi 15m và sân cỏ chơi bóng.", "Hồ bơi 15m và sân cỏ")
    ];

    /// <summary>
    /// The guests who wrote the demo history. Same names the stay reviews already
    /// use, so one person's word turns up on a stay, a session and a job rather
    /// than the site looking like six separate crowds.
    /// </summary>
    private record GuestSeed(string Name, string Bio);

    private static readonly GuestSeed[] GuestSeeds =
    [
        new("Ngọc Anh", "Đi làm ở Hà Nội, cuối tuần trốn ra biển."),
        new("Trần Hùng", "Mê đồ ăn đường phố, đi đâu cũng tìm chợ sớm."),
        new("Phương Vy", "Làm remote, hay ở lại một thành phố cả tháng."),
        new("Lê Bảo", "Đi cùng vợ và hai con nhỏ, cần chỗ rộng."),
        new("Mai Chi", "Chụp ảnh phim, thích phố cổ và ánh sáng chiều."),
        new("Đức Thắng", "Hay bay đêm nên quen với nhận phòng muộn.")
    ];

    private record ReviewSeed(string Name, string Location, string When, string Text, double Rating);

    private static readonly ReviewSeed[] ReviewSeeds =
    [
        new("Ngọc Anh", "Hà Nội", "Tháng 4, 2026", "Chủ nhà phản hồi trong vài phút, nhà đúng như ảnh và sạch hơn mong đợi. Sẽ quay lại.", 5),
        new("Trần Hùng", "TP. Hồ Chí Minh", "Tháng 3, 2026", "Vị trí quá tiện, đi bộ ra biển. Bếp đầy đủ nên nhóm mình tự nấu được cả tuần.", 5),
        new("Phương Vy", "Đà Nẵng", "Tháng 2, 2026", "Không gian yên tĩnh, đội vận hành nhắc giờ nhận phòng rất chu đáo.", 4.8),
        new("Lê Bảo", "Cần Thơ", "Tháng 1, 2026", "Giá hợp lý cho chất lượng này. Wifi mạnh, mình làm việc remote cả tuần không vấn đề.", 4.9),
        new("Mai Chi", "Hải Phòng", "Tháng 12, 2025", "Ảnh thật 100%. Chủ nhà còn để sẵn trái cây và nước suối, rất tinh tế.", 5),
        new("Đức Thắng", "Nha Trang", "Tháng 11, 2025", "Check-in tự động dễ dàng dù mình tới lúc nửa đêm. Giường êm, ngủ rất ngon.", 4.9)
    ];

    public static async Task SeedAsync(StayHostDbContext db, CancellationToken ct = default)
    {
        if (await db.Listings.AnyAsync(ct)) return;

        // Some amenities may already be present from a data migration (the
        // accessibility set of TM-17 ships that way too), so only the missing ones
        // are added; the map below is built from the whole table afterwards.
        var existingKeys = await db.Amenities.Select(a => a.Key).ToListAsync(ct);
        var fresh = AmenitySeeds
            .Select((a, i) => (a, i))
            .Where(x => !existingKeys.Contains(x.a.Key))
            .Select(x => new Amenity
            {
                Key = x.a.Key,
                Label = x.a.Label,
                Icon = x.a.Icon,
                Group = x.a.Group,
                IsFilterable = x.a.Filterable,
                SortOrder = x.i
            }).ToList();
        db.Amenities.AddRange(fresh);

        // Every demo host gets a real account so messaging and the host dashboard work
        // out of the box. Password for all demo accounts: "stayhost123".
        var hostUsers = HostSeeds.Select((h, i) => NewUser(
            $"host{i + 1}@staylio.vn", h.Name, UserRole.Host, h.Bio)).ToList();
        db.Users.AddRange(hostUsers);
        db.Users.Add(NewUser("guest@staylio.vn", "Khách Demo", UserRole.Guest,
            "Mình hay đi cuối tuần quanh miền Trung."));
        // An experience or a service review is signed by the account that booked
        // it (docs/09 §5), unlike a stay review, which carries a name typed into
        // the row. Seeded history therefore needs guests who exist: without them
        // every session and every job on the demo would show an empty review
        // block under a rating the card is already advertising.
        db.Users.AddRange(GuestSeeds.Select((g, i) => NewUser(
            $"khach{i + 1}@staylio.vn", g.Name, UserRole.Guest, g.Bio)));
        var admin = NewUser("admin@staylio.vn", "Quản trị viên", UserRole.Admin,
            "Đội vận hành StayHost.");
        admin.AdminScope = AdminScope.Super;
        // docs/08 §3 — "Bắt buộc bảo mật 2 lớp. Không bật thì không đăng nhập
        // được, không có ngoại lệ." The demo account is not an exception, so
        // signing in as admin goes through the code step like anyone else.
        admin.TwoFactorEnabled = true;
        admin.AdminAccessReviewedOn = DateOnly.FromDateTime(DateTime.UtcNow);
        db.Users.Add(admin);

        await db.SaveChangesAsync(ct);

        var hosts = HostSeeds.Select((h, i) => new HostProfile
        {
            Name = h.Name,
            Initials = Initials(h.Name),
            IsSuperhost = h.Superhost,
            // docs/03 §8 — the demo catalogue is a snapshot of a platform that
            // has been running, so its titles count as already decided for this
            // period. Without the stamp the first sweep would strip every badge
            // off a freshly seeded database within the minute.
            SuperhostReviewedOn = Badges.CurrentQuarterStart(DateOnly.FromDateTime(DateTime.UtcNow)),
            YearsHosting = h.Years,
            Bio = h.Bio,
            ResponseRate = h.Superhost ? "100%" : "95%",
            ResponseTime = h.Superhost ? "trong vòng 1 giờ" : "trong vòng vài giờ",
            JoinedAt = new DateTime(2026 - h.Years, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            // docs/07 §12.2 — a demo host has been paid before, so their account
            // is already proved. Seeding them unverified would hold every payout
            // in the catalogue on day one for a reason that is not being tested.
            PayoutBankName = "Vietcombank",
            PayoutAccountName = h.Name,
            PayoutAccountLast4 = $"{1000 + i}",
            PayoutAccountVerified = true,
            UserId = hostUsers[i].Id
        }).ToList();
        db.Hosts.AddRange(hosts);

        await db.SaveChangesAsync(ct);

        // Built from the whole table, not just the freshly-added ones, so
        // migration-inserted amenities (TM-17 accessibility) can be assigned too.
        var amenityByKey = await db.Amenities.ToDictionaryAsync(a => a.Key, ct);
        var photoCursor = 0;

        for (var i = 0; i < ListingSeeds.Length; i++)
        {
            var s = ListingSeeds[i];
            var listing = new Listing
            {
                Slug = Slugify(s.Title, i + 1),
                Title = s.Title,
                City = s.City,
                Type = s.Type,
                RoomType = s.Room,
                Bedrooms = s.Bedrooms,
                Beds = s.Beds,
                Bathrooms = s.Baths,
                MaxGuests = s.MaxGuests,
                PricePerNight = s.Price,
                Rating = s.Rating,
                ReviewCount = s.Reviews,
                // A listing carries its host's title, by definition — it is not a
                // second fact that can disagree. The seed's own per-listing flag
                // used to leave 15 listings badged under hosts who held nothing.
                IsSuperhost = hosts[s.HostIndex].IsSuperhost,
                IsGuestFavorite = s.GuestFavorite,
                FavoriteReviewedOn = Badges.CurrentWeekStart(DateOnly.FromDateTime(DateTime.UtcNow)),
                // Roughly a third of the catalogue carries a promotion, so the search
                // results show struck-through pricing the way airbnb.com does.
                DiscountPercent = i % 3 == 1 ? 10 + (i % 4) * 5 : 0,
                // Spread all six policies across the catalogue so the filter has teeth.
                CancellationTier = (CancellationTier)(i % 6),
                // Length-of-stay and booking-time discounts (docs/03 §1 steps 2–3).
                WeeklyDiscountPercent = 10,
                MonthlyDiscountPercent = 20,
                EarlyBirdDays = i % 2 == 0 ? 60 : 0,
                EarlyBirdPercent = i % 2 == 0 ? 5 : 0,
                LastMinuteDays = i % 4 == 3 ? 3 : 0,
                LastMinutePercent = i % 4 == 3 ? 8 : 0,
                // Extra-guest and pet surcharges (step 5).
                FreeGuestThreshold = Math.Max(1, s.MaxGuests / 2),
                ExtraGuestFee = 150_000m + (i % 3) * 50_000m,
                PetsAllowed = s.Amenities.Contains("pet"),
                PetFee = s.Amenities.Contains("pet") ? 200_000m : 0m,
                CleaningFee = 200_000m + (i % 5) * 50_000m,
                // Every fourth listing takes requests instead of instant bookings,
                // so the 24-hour approval flow of docs/03 §3 has something to run on.
                InstantBook = i % 4 != 3,
                // A spread of booking constraints (docs/03 §2) so the nine checks
                // are exercised by the demo data rather than only by tests.
                MinNights = 1 + (i % 3),
                MaxNights = i % 5 == 0 ? 30 : 0,
                AdvanceNoticeHours = i % 3 == 0 ? 24 : 0,
                SameDayCutoffHour = i % 3 == 0 ? null : 14,
                CalendarVisibilityMonths = 12,
                TurnoverDays = i % 6 == 0 ? 1 : 0,
                Latitude = s.Lat,
                Longitude = s.Lng,
                Description = s.Desc,
                SpaceHighlight = s.Highlight,
                HostId = hosts[s.HostIndex].Id,
                // docs/01 CĐ-03 — an arrival guide on every listing, and a door
                // code on the self-check-in ones so CĐ-04's 48-hour gate has
                // something real to withhold.
                CheckInFrom = new TimeOnly(14, 0),
                CheckInTo = new TimeOnly(i % 3 == 0 ? 23 : 22, 0),
                CheckOutBefore = new TimeOnly(12, 0),
                CheckInMethod = (CheckInMethod)(i % 5),
                AddressLine = $"{12 + i} Đường Nguyễn Văn Linh, {s.City}",
                Directions = "Từ sân bay đi taxi khoảng 20 phút.\nToà nhà nằm ngay góc ngã tư, cổng sơn xanh.",
                WifiName = $"Staylio-{i + 1:000}",
                WifiPassword = $"staycation{1000 + i}",
                ApplianceNotes = "Điều hoà: bấm nút xanh trên điều khiển, để 26°C là mát nhất.\n"
                                 + "Bình nóng lạnh: bật công tắc ngoài phòng tắm, chờ 10 phút.\n"
                                 + "Bếp từ: xoay núm sang mức 3 rồi đặt nồi lên.",
                DoorCode = CheckInGuide.NeedsDoorCode((CheckInMethod)(i % 5)) ? $"{4200 + i * 7}#" : null,
                HostPhone = $"09{12_345_678 + i * 137}"
            };

            // Five photos per listing so the card carousel and the detail gallery both have material.
            string[] captions = ["Ảnh chính", "Phòng khách", "Phòng ngủ", "Không gian ngoài trời", "Phòng tắm"];
            for (var k = 0; k < captions.Length; k++)
            {
                var pid = Pool[(photoCursor + k) % Pool.Length];
                listing.Images.Add(new ListingImage
                {
                    Url = Pic(pid, k == 0 ? 1200 : 800),
                    Caption = captions[k],
                    SortOrder = k
                });
            }
            photoCursor += 3;

            foreach (var key in s.Amenities.Where(amenityByKey.ContainsKey))
                listing.Amenities.Add(new ListingAmenity { AmenityId = amenityByKey[key].Id });

            // docs/01 TM-17 — spread accessibility features across the catalogue so
            // the filter has something to return in the demo. Real listings carry
            // whatever the host actually ticks.
            foreach (var key in AccessibilityFor(i).Where(amenityByKey.ContainsKey))
                listing.Amenities.Add(new ListingAmenity { AmenityId = amenityByKey[key].Id });

            // docs/01 TĐ-22 — the host's guidebook. Same city, same shortlist: these
            // are places in a city, not features of one house.
            if (GuidebookSeeds.TryGetValue(listing.City, out var guide))
                for (var g = 0; g < guide.Length; g++)
                    listing.Guidebook.Add(new GuidebookPlace
                    {
                        Category = guide[g].Category,
                        Name = guide[g].Name,
                        Note = guide[g].Note,
                        Address = guide[g].Address,
                        SortOrder = g
                    });

            var reviewCount = Math.Min(ReviewSeeds.Length, 4 + (i % 3));
            for (var r = 0; r < reviewCount; r++)
            {
                var rs = ReviewSeeds[(i + r) % ReviewSeeds.Length];
                listing.Reviews.Add(new Review
                {
                    AuthorName = rs.Name,
                    AuthorInitials = Initials(rs.Name),
                    AuthorLocation = rs.Location,
                    When = rs.When,
                    Text = rs.Text,
                    Rating = rs.Rating,
                    Cleanliness = Clamp(s.Rating + 0.05),
                    Accuracy = Clamp(s.Rating + 0.02),
                    CheckIn = Clamp(s.Rating + 0.06),
                    Communication = Clamp(s.Rating + 0.07),
                    Location = Clamp(s.Rating - 0.03),
                    Value = Clamp(s.Rating - 0.06),
                    CreatedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(-30 * r),
                    // Seeded reviews stand in for history, so they are already public.
                    PublishedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(-30 * r)
                });
            }

            listing.RefreshSearchText();
            db.Listings.Add(listing);
        }

        db.TaxRules.AddRange(TaxRuleSeeds());

        // docs/01 TC-09 — two demo campaigns so the code path is exercisable: one
        // percentage with a cap and a per-guest limit, one flat with a total cap.
        db.Coupons.AddRange(
            new Coupon
            {
                Code = "CHAOMUNG10", Campaign = "Chào mừng khách mới",
                Kind = CouponKind.Percentage, Value = 10m, MaxDiscount = 500_000m,
                MaxPerUser = 1
            },
            new Coupon
            {
                Code = "HE2026", Campaign = "Ưu đãi mùa hè 2026",
                Kind = CouponKind.Fixed, Value = 300_000m, MinBookingTotal = 2_000_000m,
                MaxRedemptions = 100
            });

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// docs/03 §1 step 8 wants tax modelled per region, stacking, and in more
    /// shapes than a percentage. These are plausible Vietnamese levies rather
    /// than legal advice — an admin edits them in production.
    /// </summary>
    private static IEnumerable<TaxRule> TaxRuleSeeds() =>
    [
        new TaxRule
        {
            Country = "Việt Nam",
            Name = "Thuế GTGT 8%",
            Method = TaxMethod.Percentage,
            Base = TaxBase.SubtotalPlusGuestFee,
            Value = 0.08m,
            SortOrder = 1
        },
        new TaxRule
        {
            Country = "Việt Nam",
            City = "Đà Lạt",
            Name = "Phí môi trường Đà Lạt",
            Method = TaxMethod.PerGuestPerNight,
            Value = 5_000m,
            SortOrder = 2
        },
        new TaxRule
        {
            Country = "Việt Nam",
            City = "Hội An",
            Name = "Phí tham quan phố cổ",
            Method = TaxMethod.PerStay,
            Value = 80_000m,
            SortOrder = 2
        },
        new TaxRule
        {
            Country = "Việt Nam",
            City = "Phú Quốc",
            Name = "Phí hạ tầng du lịch",
            Method = TaxMethod.PerNight,
            Value = 20_000m,
            SortOrder = 2
        }
    ];

    /// <summary>Demo accounts all share the password <c>stayhost123</c>.</summary>
    public const string DemoPassword = "stayhost123";

    private static User NewUser(string email, string name, UserRole role, string? bio)
    {
        var (hash, salt) = PasswordHasher.Hash(DemoPassword);
        return new User
        {
            Email = email,
            FullName = name,
            Initials = Initials(name),
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = role,
            Bio = bio,
            // docs/01 TĐ-14 — the host card lists what they speak, so the demo
            // accounts have to speak something. Everyone has Vietnamese; hosts
            // also have English, which is what a guest checks the card for.
            SpokenLanguages = role == UserRole.Guest ? "vi" : "vi,en",
            EmailConfirmed = true,
            IsIdentityVerified = true,
            CreatedAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    private static double Clamp(double v) => Math.Round(Math.Min(5.0, Math.Max(1.0, v)), 2);

    private static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var letters = parts.Select(p => p[0]).ToArray();
        var take = Math.Min(2, letters.Length);
        return new string(letters[^take..]).ToUpperInvariant();
    }

    private static string Slugify(string title, int id)
    {
        var normalized = title.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in normalized)
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            else if (c is ' ' or '-' && sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        var slug = sb.ToString().Replace('đ', 'd').Trim('-');
        return $"{slug}-{id}";
    }
}
