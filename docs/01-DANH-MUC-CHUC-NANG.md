# 01 — Danh mục chức năng đầy đủ

Mỗi chức năng có mã `FR-<module>-<số>` để tham chiếu và theo dõi tiến độ.
Ưu tiên: **P0** bắt buộc cho bản chạy được · **P1** cần cho vận hành thật · **P2** mở rộng.

---

## TK — Tài khoản & danh tính

| Mã | Chức năng | Ưu tiên |
|---|---|---|
| TK-01 | Đăng ký bằng số điện thoại hoặc email, xác thực bằng mã OTP 6 số | P0 |
| TK-02 | Đăng nhập bằng Google / Apple / Facebook | P1 |
| TK-03 | Bắt buộc đủ 18 tuổi khi tạo tài khoản | P0 |
| TK-04 | Hồ sơ cá nhân: ảnh, tên hiển thị, giới thiệu, ngôn ngữ nói, nơi ở, nghề nghiệp, sở thích | P0 |
| TK-05 | Trang hồ sơ công khai: người khác xem được ảnh, năm tham gia, huy hiệu xác minh, đánh giá nhận được | P0 |
| TK-06 | Xác minh danh tính bằng giấy tờ tuỳ thân + ảnh selfie | P1 |
| TK-07 | Xác minh email công ty (dành cho công tác) | P2 |
| TK-08 | Bảo mật 2 lớp, xem và thu hồi phiên đăng nhập trên từng thiết bị | P1 |
| TK-09 | Cài đặt ngôn ngữ, tiền tệ, múi giờ hiển thị | P0 |
| TK-10 | Bảng cài đặt thông báo theo ma trận: loại thông báo × kênh (trong ứng dụng, email, đẩy, SMS) | P1 |
| TK-11 | Tải toàn bộ dữ liệu cá nhân của tôi | P1 |
| TK-12 | Tạm vô hiệu hoá hoặc xoá tài khoản; dữ liệu giao dịch được ẩn danh chứ không xoá | P1 |
| TK-13 | Liên hệ khẩn cấp (dùng khi có sự cố trong chuyến đi) | P2 |

## TM — Tìm kiếm & khám phá

| Mã | Chức năng | Ưu tiên |
|---|---|---|
| TM-01 | Ô tìm kiếm 4 thành phần: địa điểm, ngày nhận, ngày trả, số khách | P0 |
| TM-02 | Chuyển tab tìm theo dòng: Tất cả / Chỗ ở / Trải nghiệm / Dịch vụ | P1 |
| TM-03 | Gợi ý địa điểm khi gõ, không dấu vẫn ra kết quả ("da lat" → Đà Lạt) | P0 |
| TM-04 | Gợi ý điểm đến phổ biến + "gần tôi" + lịch sử tìm kiếm gần đây | P0 |
| TM-05 | Chọn ngày theo lịch, hiện giá từng đêm ngay trên ô ngày | P0 |
| TM-06 | Chọn ngày linh hoạt: cuối tuần / 1 tuần / 1 tháng, cộng trừ 1–7 ngày | P1 |
| TM-07 | Chọn theo tháng: ở bao nhiêu tháng, bắt đầu từ tháng nào | P1 |
| TM-08 | Bộ chọn khách: người lớn, trẻ em, em bé, thú cưng — em bé không tính vào sức chứa | P0 |
| TM-09 | Kết quả tìm kiếm dạng lưới thẻ, mỗi thẻ có ảnh trượt, giá cho cả kỳ, điểm đánh giá | P0 |
| TM-10 | Bản đồ song song danh sách; ghim hiển thị giá; gộp ghim khi thu nhỏ | P0 |
| TM-11 | Rê chuột lên thẻ thì ghim tương ứng nổi lên và ngược lại | P1 |
| TM-12 | Tìm lại khi di chuyển bản đồ (bật/tắt được) | P0 |
| TM-13 | Lọc: khoảng giá (có biểu đồ phân bố), loại chỗ ở, số phòng ngủ/giường/phòng tắm | P0 |
| TM-14 | Lọc tiện nghi theo nhóm | P0 |
| TM-15 | Lọc tuỳ chọn đặt: đặt ngay, tự nhận phòng, cho thú cưng, huỷ miễn phí | P0 |
| TM-16 | Lọc nổi bật: Khách chọn, Chủ nhà Ưu tú | P1 |
| TM-17 | Lọc khả năng tiếp cận (lối vào bằng phẳng, thang máy, cửa rộng…) | P1 |
| TM-18 | Lọc theo ngôn ngữ chủ nhà | P2 |
| TM-19 | Đếm số kết quả cập nhật ngay khi đổi bộ lọc, trước khi bấm áp dụng | P0 |
| TM-20 | Công tắc "hiện giá đã gồm thuế và phí" | P0 |
| TM-21 | Sắp xếp kết quả theo mức độ phù hợp / giá / đánh giá | P1 |
| TM-22 | Khi không có kết quả: gợi ý bỏ bớt bộ lọc và hiện chỗ ở khu vực lân cận | P0 |
| TM-23 | Lưu bộ tìm kiếm và nhận thông báo khi có chỗ mới phù hợp | P2 |
| TM-24 | Vẽ vùng tìm kiếm trên bản đồ | P2 |
| TM-25 | Trang chủ: các dải gợi ý theo thành phố, theo cuối tuần này, theo cảm hứng | P0 |
| TM-26 | Trang giới thiệu theo thành phố/loại hình để người dùng tìm từ công cụ tìm kiếm | P1 |

## TĐ — Tin đăng & trang chi tiết (phía khách xem)

| Mã | Chức năng | Ưu tiên |
|---|---|---|
| TĐ-01 | Bộ ảnh chính + xem toàn bộ ảnh chia theo từng phòng | P0 |
| TĐ-02 | Thông tin cơ bản: loại chỗ ở, sức chứa, số phòng ngủ/giường/phòng tắm | P0 |
| TĐ-03 | Mô tả chi tiết + nút dịch sang ngôn ngữ người xem | P0 |
| TĐ-04 | Danh sách tiện nghi theo nhóm, tiện nghi không có thì gạch ngang | P0 |
| TĐ-05 | Bố trí giường theo từng phòng | P0 |
| TĐ-06 | Lịch trống 2 tháng ngay trên trang, chọn ngày tại chỗ | P0 |
| TĐ-07 | Khung đặt phòng bám theo cuộn: giá, ngày, khách, nút đặt, bảng giá chi tiết | P0 |
| TĐ-08 | Bảng giá chi tiết từng dòng, mỗi dòng có giải thích | P0 |
| TĐ-09 | Ngày đã bị đặt: báo rõ và gợi ý 3 khoảng ngày còn trống gần nhất | P1 |
| TĐ-10 | Tổng quan đánh giá: điểm chung, 6 hạng mục, phân bố sao | P0 |
| TĐ-11 | Tìm kiếm trong đánh giá, lọc theo ngôn ngữ, sắp xếp theo thời gian/điểm | P1 |
| TĐ-12 | Phản hồi của chủ nhà hiển thị dưới đánh giá | P1 |
| TĐ-13 | Bản đồ vị trí gần đúng + mô tả khu vực + khoảng cách tới các điểm chính | P0 |
| TĐ-14 | Thẻ giới thiệu chủ nhà: tỉ lệ phản hồi, thời gian phản hồi, ngôn ngữ, co-host | P0 |
| TĐ-15 | Nội quy nhà, thông tin an toàn, chính sách huỷ | P0 |
| TĐ-16 | Huy hiệu "Khách chọn" kèm lý do | P1 |
| TĐ-17 | Lưu vào danh sách yêu thích ngay từ trang chi tiết | P0 |
| TĐ-18 | Chia sẻ tin đăng qua link, mạng xã hội, email | P1 |
| TĐ-19 | Báo cáo tin đăng | P1 |
| TĐ-20 | Gợi ý chỗ ở tương tự | P1 |
| TĐ-21 | Tóm tắt đánh giá theo chủ đề (vị trí, sạch sẽ, tiện nghi, hợp gia đình…) | P2 |
| TĐ-22 | Cẩm nang địa phương do chủ nhà tự viết: quán ăn, cà phê, tham quan, đi lại, lời khuyên — có lý do giới thiệu và khoảng cách | P1 |
| TĐ-23 | Dấu "Hiếm có" khi lịch 60 ngày tới gần kín, kèm lý do đọc được từ chính lịch bên dưới | P2 |

## YT — Danh sách yêu thích & lên kế hoạch

| Mã | Chức năng | Ưu tiên |
|---|---|---|
| YT-01 | Tạo nhiều danh sách, đặt tên, đặt riêng tư/công khai | P0 |
| YT-02 | Lưu/bỏ lưu chỗ ở, trải nghiệm, dịch vụ | P0 |
| YT-03 | Ghi chú riêng cho từng mục đã lưu | P1 |
| YT-04 | Xem danh sách trên bản đồ | P1 |
| YT-05 | Chia sẻ danh sách qua link, mời người khác cùng sửa | P1 |
| YT-06 | Bình chọn thích/không thích trong nhóm | P2 |
| YT-07 | So sánh 2–5 chỗ ở đã lưu theo tiêu chí | P2 |
| YT-08 | Báo khi chỗ ở đã lưu giảm giá hoặc sắp hết phòng | P2 |

## ĐP — Đặt phòng & thanh toán

| Mã | Chức năng | Ưu tiên |
|---|---|---|
| ĐP-01 | Hai chế độ: **Đặt ngay** (xác nhận tức thì) và **Yêu cầu đặt** (chủ nhà duyệt trong 24h) | P0 |
| ĐP-02 | Giữ chỗ tạm 15 phút khi khách vào bước thanh toán, có đồng hồ đếm ngược | P0 |
| ĐP-03 | Điều kiện Đặt ngay của chủ nhà (chỉ khách đã xác minh, chỉ khách có đánh giá tốt) | P1 |
| ĐP-04 | Sửa ngày và số khách ngay tại bước thanh toán, giá tính lại tức thì | P0 |
| ĐP-05 | Trả toàn bộ ngay | P0 |
| ĐP-06 | Trả một phần: cọc ≥50%, phần còn lại tự động thu trước ngày nhận phòng | P1 |
| ĐP-07 | Chia hoá đơn cho tối đa 16 người, mỗi người nhận link trả phần mình | P2 |
| ĐP-08 | Nhiều phương thức: thẻ, ví điện tử, chuyển khoản, số dư khuyến mãi, thẻ quà tặng | P0 |
| ĐP-09 | Nhập mã giảm giá, kiểm tra điều kiện áp dụng | P1 |
| ĐP-10 | Yêu cầu bắt buộc trước khi đặt (có ảnh hồ sơ, đã xác minh, đồng ý nội quy) | P1 |
| ĐP-11 | Viết lời nhắn cho chủ nhà khi gửi yêu cầu đặt | P0 |
| ĐP-12 | Máy chủ tính lại giá trước khi trừ tiền; nếu lệch thì dừng và báo giá mới | P0 |
| ĐP-13 | Mã đặt phòng dễ đọc để tra cứu và hỗ trợ | P0 |
| ĐP-14 | Hoá đơn tải về được | P1 |
| ĐP-15 | Thêm chuyến đi vào lịch cá nhân | P2 |
| ĐP-16 | Yêu cầu đặt tự hết hạn sau 24 giờ nếu chủ nhà không trả lời | P0 |
| ĐP-17 | Ưu đãi riêng: chủ nhà gửi giá đặc biệt trong tin nhắn, hiệu lực 24h | P1 |

## CĐ — Chuyến đi (sau khi đặt)

| Mã | Chức năng | Ưu tiên |
|---|---|---|
| CĐ-01 | Danh sách chuyến: sắp tới, đang diễn ra, đã đi, đã huỷ | P0 |
| CĐ-02 | Chi tiết chuyến: đếm ngược, địa chỉ đầy đủ, chỉ đường | P0 |
| CĐ-03 | Hướng dẫn nhận phòng: giờ, cách vào nhà, wifi, hướng dẫn thiết bị | P0 |
| CĐ-04 | Mã cửa/khoá chỉ hiện từ 48 giờ trước ngày nhận phòng | P1 |
| CĐ-05 | Nhắn tin thẳng với chủ nhà từ chi tiết chuyến | P0 |
| CĐ-06 | Yêu cầu đổi ngày hoặc số khách, chủ nhà duyệt, chênh lệch tiền tự tính | P1 |
| CĐ-07 | Xem trước số tiền được hoàn rồi mới huỷ | P0 |
| CĐ-08 | Huỷ chuyến, chọn lý do, nhận xác nhận số tiền và thời gian hoàn | P0 |
| CĐ-09 | Xem lại hoá đơn và các khoản đã trả | P1 |
| CĐ-10 | Gộp nhiều đơn thành một chuyến, có lịch trình theo ngày | P2 |
| CĐ-11 | Mời người cùng đi vào lịch trình chung, cùng thêm địa điểm | P2 |
| CĐ-12 | Nút xin trợ giúp gắn với đúng đơn đang gặp vấn đề | P1 |

## TN — Tin nhắn

| Mã | Chức năng | Ưu tiên |
|---|---|---|
| TN-01 | Hộp thư theo cuộc trò chuyện, gắn với đơn đặt hoặc câu hỏi trước khi đặt | P0 |
| TN-02 | Gửi văn bản và ảnh | P0 |
| TN-03 | Thẻ tóm tắt đơn đặt trong cuộc trò chuyện, kèm nút hành động nhanh | P0 |
| TN-04 | Tin nhắn hệ thống: đơn đã xác nhận, đã huỷ, có ưu đãi mới, có yêu cầu đổi lịch | P0 |
| TN-05 | Lọc: chưa đọc, cần trả lời, đã lưu trữ | P1 |
| TN-06 | Dịch tin nhắn sang ngôn ngữ của người đọc | P1 |
| TN-07 | Che số điện thoại, email, link ngoài trước khi đơn được xác nhận | P0 |
| TN-08 | Mẫu trả lời nhanh của chủ nhà | P1 |
| TN-09 | Tin nhắn tự động theo mốc: xác nhận đơn, trước nhận phòng 24h, sáng ngày trả phòng | P1 |
| TN-10 | Đánh dấu đã đọc, đếm số chưa đọc, thông báo tin mới | P0 |

## ĐG — Đánh giá

| Mã | Chức năng | Ưu tiên |
|---|---|---|
| ĐG-01 | Chỉ đơn đã hoàn tất mới được đánh giá | P0 |
| ĐG-02 | Cửa sổ viết 14 ngày, nhắc vào ngày 1, 7, 13 | P0 |
| ĐG-03 | Đánh giá mù hai chiều: chỉ công khai khi cả hai đã gửi hoặc hết hạn | P0 |
| ĐG-04 | Khách chấm 6 hạng mục: sạch sẽ, đúng mô tả, nhận phòng, giao tiếp, vị trí, đáng giá tiền | P0 |
| ĐG-05 | Góp ý riêng gửi chủ nhà, không công khai | P1 |
| ĐG-06 | Chủ nhà đánh giá khách: nhận xét công khai + có/không khuyến nghị | P0 |
| ĐG-07 | Chủ nhà trả lời công khai một lần trong 30 ngày | P1 |
| ĐG-08 | Sửa đánh giá trong 48 giờ nếu bên kia chưa gửi | P1 |
| ĐG-09 | Tự động chặn đánh giá chứa số điện thoại, email, link, ngôn từ phân biệt đối xử | P1 |
| ĐG-10 | Báo cáo đánh giá vi phạm | P1 |
| ĐG-11 | Phát hiện đánh giá gian lận (tự đánh giá qua tài khoản phụ) | P2 |
| ĐG-12 | Đơn bị chủ nhà huỷ thì tự sinh ghi chú công khai trên tin đăng | P1 |

## CN — Chủ nhà: đăng tin

| Mã | Chức năng | Ưu tiên |
|---|---|---|
| CN-01 | Quy trình đăng tin theo bước, lưu nháp tự động, thoát giữa chừng vẫn quay lại được | P0 |
| CN-02 | Chọn loại hình chỗ ở và mức độ riêng tư | P0 |
| CN-03 | Nhập địa chỉ, kéo ghim trên bản đồ để xác nhận toạ độ | P0 |
| CN-04 | Khai sức chứa, số phòng ngủ, giường, phòng tắm | P0 |
| CN-05 | Cấu hình giường theo từng phòng | P1 |
| CN-06 | Chọn tiện nghi, tách riêng nhóm thiết bị an toàn | P0 |
| CN-07 | Tải ảnh tối thiểu 5 tấm, kéo thả sắp xếp, chọn ảnh bìa, gắn nhãn phòng | P0 |
| CN-08 | Viết tiêu đề (giới hạn ký tự) và mô tả, có gợi ý tự động | P0 |
| CN-09 | Chọn cách nhận đơn: Đặt ngay hoặc duyệt yêu cầu | P0 |
| CN-10 | Đặt giá, tham khảo khoảng giá thị trường của khu vực | P0 |
| CN-11 | Bật giảm giá tin mới, giảm theo tuần, theo tháng | P1 |
| CN-12 | Khai báo pháp lý: giấy phép, thiết bị ghi hình, vũ khí trong nhà | P1 |
| CN-13 | Xem trước tin đăng đúng như khách sẽ thấy trước khi xuất bản | P0 |
| CN-14 | Máy tính ước lượng thu nhập trước khi quyết định đăng | P2 |
| CN-15 | Nhân bản tin đăng để tạo nhanh chỗ ở tương tự | P2 |

## QL — Chủ nhà: quản lý vận hành

| Mã | Chức năng | Ưu tiên |
|---|---|---|
| QL-01 | Bảng "Hôm nay": việc cần làm, khách sắp đến, khách đang ở, khách sắp đi | P0 |
| QL-02 | Danh sách tin đăng: ẩn/hiện, tạm nghỉ, sửa, xoá | P0 |
| QL-03 | Trình sửa tin theo từng mục, sửa mục nào lưu mục đó | P0 |
| QL-04 | Lịch nhiều tin đăng cùng lúc, cuộn ngang theo ngày | P0 |
| QL-05 | Chọn nhiều ngày một lúc để đặt giá, chặn/mở, đổi số đêm tối thiểu | P0 |
| QL-06 | Quy tắc lịch: số đêm tối thiểu/tối đa, báo trước bao lâu, thời gian dọn dẹp, mở lịch bao xa | P0 |
| QL-07 | Chặn ngày nhận phòng hoặc trả phòng theo thứ trong tuần | P1 |
| QL-08 | Giá theo mùa/khoảng ngày | P1 |
| QL-09 | Gợi ý giá theo nhu cầu thị trường, chủ nhà bấm áp dụng chứ không tự đổi | P2 |
| QL-10 | Đồng bộ lịch với nền tảng khác: nhập và xuất | P1 |
| QL-11 | Cảnh báo khi lịch nhập về trùng với đơn đã xác nhận | P1 |
| QL-12 | Danh sách đơn theo trạng thái, chấp nhận/từ chối kèm lý do | P0 |
| QL-13 | Chủ nhà chủ động huỷ đơn, được cảnh báo rõ hậu quả trước khi xác nhận | P0 |
| QL-14 | Gửi ưu đãi riêng cho khách đang hỏi | P1 |
| QL-15 | Báo cáo thu nhập theo tháng/năm, đã trả và sắp trả, xuất file | P0 |
| QL-16 | Báo cáo hiệu suất: lượt xem, lượt lưu, tỉ lệ đặt, tỉ lệ lấp đầy, so với chỗ tương tự | P1 |
| QL-17 | Theo dõi tiến độ đạt Chủ nhà Ưu tú theo 4 tiêu chí | P1 |
| QL-18 | Gợi ý cải thiện kèm ước lượng tác động | P2 |
| QL-19 | Mời co-host, chọn tin đăng và phạm vi quyền, thu hồi quyền | P1 |
| QL-20 | Cài đặt tài khoản nhận tiền, xem lịch trả tiền | P0 |

## TC — Tài chính

| Mã | Chức năng | Ưu tiên |
|---|---|---|
| TC-01 | Thu tiền khách và giữ tại sàn cho tới khi khách nhận phòng | P0 |
| TC-02 | Trả tiền chủ nhà sau khi khách nhận phòng 24 giờ | P0 |
| TC-03 | Đơn dài từ 28 đêm trở lên thì trả theo từng tháng | P1 |
| TC-04 | Tính và tách thuế theo khu vực, xuất báo cáo thuế | P1 |
| TC-05 | Sổ ghi mọi khoản thu chi, đối soát tự động hằng ngày | P0 |
| TC-06 | Hoàn tiền tự động theo chính sách; hoàn thủ công khi admin quyết định | P0 |
| TC-07 | Số dư khuyến mãi (credit) và hạn sử dụng | P2 |
| TC-08 | Thẻ quà tặng: mua, tặng, đổi | P2 |
| TC-09 | Mã giảm giá theo chiến dịch, giới hạn lượt dùng | P1 |
| TC-10 | Giới thiệu bạn bè, thưởng cho cả hai bên khi hoàn tất chuyến đầu | P2 |
| TC-11 | Xử lý tranh chấp thẻ và giao dịch nghi ngờ gian lận | P1 |
| TC-12 | Quy đổi và hiển thị nhiều loại tiền tệ, nêu rõ tiền gốc | P1 |

## AT — An toàn, tin cậy & hỗ trợ

| Mã | Chức năng | Ưu tiên |
|---|---|---|
| AT-01 | Kiểm duyệt tin đăng mới trước khi hiển thị | P1 |
| AT-02 | Báo cáo tin đăng, người dùng, tin nhắn, đánh giá | P1 |
| AT-03 | Kênh phản ánh dành cho hàng xóm, không cần tài khoản | P2 |
| AT-04 | Trung tâm giải quyết: đòi bồi thường hư hỏng, xin hoàn tiền, thu phí phát sinh | P1 |
| AT-05 | Bên bị yêu cầu có 24 giờ phản hồi trước khi sàn vào phân xử | P1 |
| AT-06 | Chương trình bảo vệ Staylio Shield cho cả khách và chủ nhà | P2 |
| AT-07 | Trung tâm trợ giúp có bài viết, tìm kiếm, phân tách nội dung khách/chủ nhà | P1 |
| AT-08 | Trợ lý hỗ trợ tự động hiểu ngữ cảnh đơn của người dùng, có nút hành động | P2 |
| AT-09 | Chuyển tiếp lên nhân viên hỗ trợ khi tự động không giải quyết được | P1 |
| AT-10 | Danh sách chặn giữa hai người dùng | P2 |
| AT-11 | Phát hiện hành vi bất thường: tài khoản mới đặt đơn giá trị lớn, nhiều thẻ, nhiều đơn huỷ | P1 |
| AT-12 | Chính sách chống phân biệt đối xử, giám sát lý do từ chối khách của chủ nhà | P2 |

## QT — Quản trị

| Mã | Chức năng | Ưu tiên |
|---|---|---|
| QT-01 | Bảng điều khiển số liệu vận hành | P1 |
| QT-02 | Hàng chờ kiểm duyệt nội dung, quyết định kèm ghi chú | P1 |
| QT-03 | Tra cứu, khoá, mở khoá tài khoản | P1 |
| QT-04 | Tra cứu đơn, hoàn tiền thủ công, điều chỉnh khoản trả cho chủ nhà | P1 |
| QT-05 | Phân xử tranh chấp, quyết định chia tiền | P1 |
| QT-06 | Cấu hình mức phí, thuế theo khu vực, tỉ giá | P1 |
| QT-07 | Quản lý bài viết trợ giúp và nội dung trang giới thiệu | P2 |
| QT-08 | Bật/tắt từng tính năng theo tỉ lệ người dùng | P2 |
| QT-09 | Nhật ký mọi thao tác của quản trị viên | P1 |
| QT-10 | Đăng nhập thay mặt người dùng để hỗ trợ, có ghi nhận và giới hạn thời gian | P2 |

## MR — Trải nghiệm, dịch vụ, khách sạn

| Mã | Chức năng | Ưu tiên |
|---|---|---|
| MR-01 | Đăng trải nghiệm: thời lượng, nhóm tối đa, ngôn ngữ, độ tuổi, điểm hẹn, những gì bao gồm | P2 |
| MR-02 | Lịch trải nghiệm theo suất giờ, còn bao nhiêu chỗ | P2 |
| MR-03 | Đặt trải nghiệm theo số người, có tuỳ chọn thuê trọn nhóm riêng | P2 |
| MR-04 | Huỷ suất nếu không đủ số người tối thiểu, hoàn tiền tự động | P2 |
| MR-05 | Đăng dịch vụ: phạm vi phục vụ, cách tính giá, có tới chỗ khách hay không | P2 |
| MR-06 | Đặt dịch vụ theo khung giờ, nhập địa chỉ thực hiện | P2 |
| MR-07 | Dịch vụ qua đối tác bên thứ ba, sàn ăn hoa hồng | P2 |
| MR-08 | Khách sạn: một cơ sở nhiều loại phòng, mỗi loại có số lượng tồn | P2 |
| MR-09 | Chọn loại phòng rồi mới vào bước thanh toán | P2 |
| MR-10 | Cam kết giá tốt: tìm thấy rẻ hơn thì bù chênh lệch bằng số dư | P2 |

## XH — Kết nối xã hội

| Mã | Chức năng | Ưu tiên |
|---|---|---|
| XH-01 | Kết bạn trong sàn, xem nơi bạn bè đã đi và sắp đi | P2 |
| XH-02 | Bản đồ hành trình cá nhân, đặt chế độ riêng tư | P2 |
| XH-03 | Nhắn tin hỏi bạn bè về nơi họ từng ở | P2 |
