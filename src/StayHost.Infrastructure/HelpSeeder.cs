using Microsoft.EntityFrameworkCore;
using StayHost.Domain;

namespace StayHost.Infrastructure;

/// <summary>
/// docs/01 AT-07 — the help centre's own content. Every article here restates a
/// rule that lives somewhere in docs/03; if a rule changes, the article next to
/// it changes with it.
/// </summary>
public static class HelpSeeder
{
    private record Seed(
        string Slug, string Title, string Category, HelpAudience Audience, string Summary, string Body);

    public static async Task SeedAsync(StayHostDbContext db, CancellationToken ct = default)
    {
        if (await db.HelpArticles.AnyAsync(ct)) return;

        var order = 0;
        foreach (var s in Articles)
        {
            var article = new HelpArticle
            {
                Slug = s.Slug,
                Title = s.Title,
                Category = s.Category,
                Audience = s.Audience,
                Summary = s.Summary,
                Body = s.Body.Trim(),
                SortOrder = order++
            };
            article.RefreshSearchText();
            db.HelpArticles.Add(article);
        }

        await db.SaveChangesAsync(ct);
    }

    private static readonly Seed[] Articles =
    [
        new("phi-dich-vu", "Phí dịch vụ được tính thế nào?", "Tiền bạc", HelpAudience.Everyone,
            "Khách trả 14% phí dịch vụ, chủ nhà chịu 3% trên phần của mình.",
            """
            Giá bạn thấy trên thẻ chỗ nghỉ đã là giá cuối cùng: tiền phòng, phí dọn dẹp, phụ thu, phí dịch vụ và thuế đều đã nằm trong đó. Không có khoản nào xuất hiện thêm ở bước thanh toán.

            Phí dịch vụ khách là 14% của tiền phòng sau giảm giá cộng các khoản phụ thu. Phí này giúp vận hành sàn: thanh toán, hỗ trợ, giải quyết tranh chấp.

            Chủ nhà chịu 3% trên cùng phần đó, trừ thẳng vào khoản chuyển về tài khoản của họ.

            Thuế được tính theo khu vực của chỗ nghỉ và có thể có nhiều loại chồng nhau. Bảng chi tiết ở bước thanh toán liệt kê từng dòng một.
            """),

        new("huy-dat-cho", "Huỷ đặt chỗ và được hoàn bao nhiêu", "Huỷ và hoàn tiền", HelpAudience.Guest,
            "Sáu chính sách huỷ, cộng 48 giờ ân hạn sau khi đặt.",
            """
            Mỗi chỗ nghỉ chọn một trong sáu chính sách huỷ, từ Linh hoạt đến Không hoàn tiền. Chính sách áp dụng cho đơn của bạn được ghi rõ ở bước thanh toán và trên trang chuyến đi.

            Dù chính sách nào, bạn cũng có 48 giờ ân hạn kể từ lúc đặt để huỷ và được hoàn toàn bộ — miễn là lúc đó vẫn còn ít nhất 14 ngày nữa mới đến ngày nhận phòng.

            Phí dọn dẹp luôn được hoàn đủ nếu bạn chưa nhận phòng. Bạn không phải trả tiền dọn dẹp cho một chỗ mình chưa đặt chân tới.

            Phí dịch vụ được hoàn tối đa ba lần mỗi năm. Từ lần thứ tư trở đi, phần phí dịch vụ sẽ được giữ lại.

            Muốn biết chính xác con số trước khi bấm huỷ, mở chuyến đi và chọn "Huỷ chuyến đi" — màn hình sẽ hiện số tiền hoàn trước khi bạn xác nhận.
            """),

        new("chu-nha-huy", "Chủ nhà huỷ đơn của bạn", "Huỷ và hoàn tiền", HelpAudience.Guest,
            "Hoàn 100% và một khoản tín dụng để bạn tìm chỗ khác.",
            """
            Khi chủ nhà huỷ một đơn đã xác nhận, bạn được hoàn lại toàn bộ số tiền đã trả, không trừ khoản nào.

            Ngoài ra bạn nhận thêm một khoản tín dụng bằng 10% giá trị đơn, dùng cho lần đặt sau. Khoản này là của sàn, không lấy từ chủ nhà.

            Chủ nhà huỷ đơn nhiều lần sẽ mất danh hiệu và bị hạn chế hiển thị.
            """),

        new("tra-mot-phan", "Trả trước một nửa, phần còn lại trả sau", "Thanh toán", HelpAudience.Guest,
            "Đặt cọc từ 50%, phần còn lại tự động thu 14 ngày trước ngày nhận phòng.",
            """
            Nếu ngày nhận phòng còn cách hơn 14 ngày, ở bước thanh toán bạn có thể chọn trả trước một nửa thay vì trả toàn bộ.

            Phần còn lại được thu tự động vào đúng 14 ngày trước ngày nhận phòng, bằng chính thẻ bạn đã dùng. Bạn cũng có thể vào trang chuyến đi trả sớm bất cứ lúc nào.

            Nếu lần thu thứ hai không thành công, chúng tôi thử lại trong 72 giờ và báo cho bạn. Quá thời gian đó mà vẫn chưa thu được, đơn sẽ bị huỷ theo đúng chính sách huỷ của chỗ nghỉ.

            Đặt sát ngày hơn 14 ngày thì không có lựa chọn này — bạn trả đủ ngay khi đặt.
            """),

        new("giu-cho-15-phut", "Vì sao chỉ có 15 phút để thanh toán?", "Thanh toán", HelpAudience.Guest,
            "Ngày được giữ riêng cho bạn trong 15 phút, sau đó mở lại cho người khác.",
            """
            Khi bạn vào bước thanh toán, những ngày đó được giữ riêng cho bạn và biến mất khỏi lịch của người khác.

            Đồng hồ đếm ngược 15 phút để việc giữ chỗ này không kéo dài vô hạn. Hết giờ mà chưa thanh toán xong, ngày sẽ mở lại và bạn cần đặt lại từ đầu.

            Rời khỏi bước thanh toán giữa chừng cũng trả ngay ngày về lịch, không cần chờ hết 15 phút.
            """),

        new("yeu-cau-dat", "Đặt ngay và Yêu cầu đặt khác nhau ra sao", "Đặt chỗ", HelpAudience.Guest,
            "Đặt ngay là xong luôn; Yêu cầu đặt cần chủ nhà duyệt trong 24 giờ.",
            """
            Chỗ nghỉ có nhãn "Đặt ngay" được xác nhận ngay khi bạn thanh toán xong.

            Với chỗ nghỉ còn lại, bạn gửi một yêu cầu và chủ nhà có 24 giờ để trả lời. Trong thời gian chờ, ngày vẫn mở cho người khác đặt — chúng tôi không giữ chỗ cho một yêu cầu chưa được duyệt.

            Chủ nhà không trả lời trong 24 giờ thì yêu cầu tự hết hạn và bạn không bị trừ tiền.
            """),

        new("danh-gia-hai-chieu", "Đánh giá hai chiều hoạt động thế nào", "Đánh giá", HelpAudience.Everyone,
            "Cả hai bên có 14 ngày; đánh giá chỉ hiện khi cả hai đã viết.",
            """
            Sau khi trả phòng, khách và chủ nhà đều có 14 ngày để viết đánh giá về nhau.

            Đánh giá của bạn được giữ kín cho tới khi bên kia cũng viết xong, hoặc cho tới khi hết 14 ngày. Cách này giúp không ai viết theo người khác.

            Trong 48 giờ đầu và khi đánh giá còn đang ẩn, bạn vẫn sửa lại được. Một khi đã công khai thì không sửa nữa — đó chính là ý nghĩa của việc giữ kín trước đó.

            Chủ nhà được trả lời công khai một lần dưới mỗi đánh giá.
            """),

        new("thong-tin-lien-he", "Vì sao số điện thoại của tôi bị che?", "An toàn", HelpAudience.Everyone,
            "Thông tin liên hệ chỉ hiện sau khi đơn được xác nhận.",
            """
            Trước khi một đơn được xác nhận, số điện thoại, email và đường liên kết trong tin nhắn sẽ được che.

            Lý do rất thực tế: giao dịch bên ngoài Staylio không được bảo vệ. Nếu có chuyện xảy ra, chúng tôi không thể hoàn tiền, không thể phân xử và không có gì trong tay để giúp bạn.

            Ngay khi đơn được xác nhận, hai bên nhìn thấy đầy đủ thông tin của nhau.
            """),

        new("trung-tam-giai-quyet", "Đòi bồi thường hoặc phản đối một yêu cầu", "An toàn", HelpAudience.Everyone,
            "Bên bị yêu cầu có 24 giờ phản hồi trước khi sàn vào phân xử.",
            """
            Chủ nhà có thể mở yêu cầu bồi thường hư hỏng; khách có thể yêu cầu hoàn tiền hoặc phản đối một khoản phí phát sinh.

            Bên còn lại có 24 giờ để phản hồi. Trong thời gian đó hai bên vẫn có thể tự thoả thuận, và phần lớn vụ việc kết thúc ở đây.

            Không thoả thuận được thì Staylio vào phân xử, xem bằng chứng cả hai bên gửi và quyết định số tiền. Mọi khoản tiền đều được ghi vào sổ, không sửa không xoá.
            """),

        new("nhan-tien-khi-nao", "Khi nào chủ nhà nhận được tiền?", "Tiền bạc", HelpAudience.Host,
            "24 giờ sau khi khách nhận phòng.",
            """
            Tiền được chuyển 24 giờ sau giờ nhận phòng của khách, không phải ngay lúc khách đặt.

            Khoảng chờ này để hai bên kịp xử lý nếu chỗ nghỉ không đúng như mô tả.

            Bạn chọn nhận từng đơn một hoặc gộp theo tuần, theo tháng trong mục Nhận tiền. Staylio chỉ lưu 4 số cuối của tài khoản.
            """),

        new("dong-bo-lich", "Nối lịch với nền tảng khác", "Lịch", HelpAudience.Host,
            "Nhập lịch từ nơi khác về, và cho nơi khác đọc lịch ở đây.",
            """
            Trong mục Lịch của trang chủ nhà, bạn dán địa chỉ .ics của nền tảng khác để nhập lịch về. Những ngày bên đó đã bán sẽ tự động bị khoá ở đây.

            Lịch nhập về chỉ khoá ngày, không tạo đơn và không sinh tiền.

            Chiều ngược lại, mỗi chỗ nghỉ có một địa chỉ riêng để nền tảng khác đọc lịch của bạn. Địa chỉ đó chứa một mã bí mật — ai có nó là xem được lịch, nên đừng đăng công khai.

            Lịch được làm mới mỗi giờ. Nếu một lần đồng bộ thất bại, những ngày đã khoá vẫn được giữ nguyên cho tới lần đồng bộ thành công tiếp theo.
            """),

        new("dong-quan-ly", "Mời người khác cùng quản lý", "Vận hành", HelpAudience.Host,
            "Chọn tin đăng và phạm vi quyền, thu hồi bất cứ lúc nào.",
            """
            Bạn có thể mời người khác giúp mình vận hành: mở lịch, đổi giá, trả lời tin nhắn, duyệt đơn, sửa nội dung tin đăng — chọn đúng những việc bạn muốn giao.

            Lời mời gửi theo email, kể cả khi người đó chưa có tài khoản Staylio.

            Người đồng quản lý không bao giờ nhìn thấy tài khoản nhận tiền của bạn, dù được cấp quyền nào.

            Thu hồi quyền có hiệu lực ngay lập tức.
            """),

        new("gia-theo-mua", "Đặt giá theo mùa và theo ngày", "Tiền bạc", HelpAudience.Host,
            "Giá theo ngày thắng giá mùa, giá mùa thắng giá cuối tuần.",
            """
            Giá cơ bản là mức nền. Bạn có thể phụ thu cuối tuần, đặt giá riêng cho một mùa, hoặc sửa giá của từng ngày trên lịch.

            Khi các mức chồng lên nhau, mức cụ thể hơn thắng: giá đặt cho một ngày cụ thể thắng giá mùa, giá mùa thắng phụ thu cuối tuần, và tất cả đều thắng giá cơ bản.

            Giảm giá theo độ dài (theo tuần, theo tháng) và giảm theo thời điểm đặt (đặt sớm, phút chót) được cộng lại, nhưng tổng mức giảm không bao giờ vượt 60%.
            """),

        new("chu-nha-uu-tu", "Điều kiện trở thành Chủ nhà Ưu tú", "Vận hành", HelpAudience.Host,
            "Xét lại mỗi quý dựa trên đánh giá, tỉ lệ trả lời, tỉ lệ huỷ và số đơn.",
            """
            Danh hiệu được xét lại mỗi quý, dựa trên bốn điều kiện cùng lúc: điểm đánh giá trung bình, tỉ lệ trả lời tin nhắn, tỉ lệ huỷ đơn và số lượt đón khách trong năm.

            Mục Nhận tiền trong trang chủ nhà hiện bạn đang đạt điều kiện nào và còn thiếu gì.

            Mất danh hiệu không phải là hình phạt vĩnh viễn — quý sau đạt lại thì có lại.
            """)
    ];
}
