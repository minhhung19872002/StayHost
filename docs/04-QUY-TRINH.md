# 04 — Quy trình nghiệp vụ từ đầu đến cuối

Mỗi quy trình mô tả: ai làm gì, hệ thống làm gì, và điều gì xảy ra khi có sự cố.

---

## QT-1. Khách tìm và đặt chỗ ở (Đặt ngay)

| # | Người thực hiện | Hành động | Hệ thống làm gì |
|---|---|---|---|
| 1 | Khách | Nhập địa điểm, ngày, số khách | Gợi ý địa điểm; kiểm tra ngày hợp lệ |
| 2 | Khách | Xem kết quả, lọc thêm | Chỉ hiện chỗ còn trống đủ ngày và đủ sức chứa; giá trên thẻ là giá cho cả kỳ |
| 3 | Khách | Mở một chỗ ở | Tính lại giá chi tiết cho đúng khoảng ngày đó |
| 4 | Khách | Bấm "Đặt ngay" | Kiểm tra lại điều kiện đặt; **giữ ngày 15 phút**; nếu chưa đăng nhập thì yêu cầu đăng nhập trước |
| 5 | Khách | Xác nhận ngày, khách, chọn cách trả | Tính lại giá mỗi lần khách đổi |
| 6 | Khách | Chọn phương thức thanh toán, nhập mã giảm giá | Kiểm tra mã còn hiệu lực và đủ điều kiện |
| 7 | Khách | Bấm xác nhận và trả tiền | **Máy chủ tính lại giá lần cuối.** Lệch với giá đang hiện → dừng, báo giá mới, chờ khách đồng ý |
| 8 | Hệ thống | | Trừ tiền; chuyển đơn sang **Đã xác nhận**; khoá ngày vĩnh viễn; ghi sổ |
| 9 | Hệ thống | | Gửi xác nhận cho khách kèm hành trình; báo cho chủ nhà có khách mới; tạo cuộc trò chuyện |
| 10 | Hệ thống | | Lên lịch trả tiền cho chủ nhà vào 24 giờ sau ngày nhận phòng |

**Sự cố:**
- Hết 15 phút giữ chỗ → mở lại ngày, báo khách, cho thử lại
- Trả tiền thất bại → giữ đơn ở trạng thái chờ, cho chọn phương thức khác, giữ ngày thêm 15 phút
- Người khác đặt trước trong lúc khách đang điền → báo rõ và gợi ý ngày khác

---

## QT-2. Khách gửi yêu cầu đặt, chủ nhà duyệt

| # | Ai | Hành động |
|---|---|---|
| 1 | Khách | Chọn ngày, viết lời nhắn giới thiệu, gửi yêu cầu |
| 2 | Hệ thống | **Không khoá ngày.** Báo khách rằng ngày vẫn có thể bị người khác đặt |
| 3 | Hệ thống | Báo chủ nhà, bắt đầu đếm ngược 24 giờ, nhắc lại lúc còn 12 giờ và 2 giờ |
| 4 | Chủ nhà | Xem hồ sơ khách, đánh giá cũ, lời nhắn |
| 5a | Chủ nhà | **Chấp nhận** → hệ thống trừ tiền khách, xác nhận đơn, khoá ngày |
| 5b | Chủ nhà | **Từ chối** → bắt buộc chọn lý do; báo khách; gợi ý chỗ tương tự |
| 5c | Chủ nhà | **Gửi ưu đãi riêng** với giá hoặc ngày khác, hiệu lực 24 giờ |
| 5d | Không ai làm gì | Quá 24 giờ → tự hết hạn, báo cả hai, tính vào tỉ lệ phản hồi của chủ nhà |

**Sự cố:** chủ nhà chấp nhận nhưng ngày đã bị người khác đặt mất → báo lỗi rõ ràng cho chủ nhà, đơn chuyển sang hết hạn, khách được báo và gợi ý chỗ khác. Không tính vào tỉ lệ từ chối của chủ nhà.

---

## QT-3. Trước, trong và sau kỳ lưu trú

| Mốc | Việc xảy ra |
|---|---|
| Ngay khi xác nhận | Khách nhận hành trình; chủ nhà nhận thông tin khách; mở cuộc trò chuyện |
| 7 ngày trước | Nhắc khách; nhắc chủ nhà chuẩn bị |
| 48 giờ trước | Hiện mã cửa và hướng dẫn vào nhà cho khách |
| 24 giờ trước | Hiện số điện thoại chủ nhà; gửi hướng dẫn đường đi |
| Ngày nhận phòng | Đơn chuyển sang **Đang lưu trú** (theo múi giờ chỗ ở) |
| 24 giờ sau khi nhận phòng | Chuyển tiền cho chủ nhà |
| Sáng ngày trả phòng | Nhắc khách giờ trả phòng và việc cần làm trước khi đi |
| Ngày trả phòng | Đơn chuyển sang **Đã hoàn tất**; mở form đánh giá cho cả hai |
| 14 ngày sau | Đóng cửa sổ đánh giá; công khai những gì đã viết |

---

## QT-4. Khách đổi lịch

1. Khách mở chi tiết chuyến, chọn ngày mới hoặc số khách mới
2. Hệ thống kiểm tra ngày mới có trống không và tính chênh lệch tiền
3. Gửi đề nghị cho chủ nhà, hiệu lực 24 giờ, ngày cũ **vẫn giữ nguyên** trong lúc chờ
4. Chủ nhà chấp nhận → thu thêm hoặc hoàn bớt phần chênh lệch, cập nhật lịch, giải phóng ngày cũ
5. Chủ nhà từ chối hoặc quá hạn → đơn giữ nguyên như cũ

Chủ nhà cũng có thể chủ động đề nghị đổi lịch theo cùng cơ chế.

---

## QT-5. Khách huỷ

1. Khách bấm huỷ → hệ thống hiện **bảng tính hoàn tiền chi tiết**: được hoàn bao nhiêu, mất bao nhiêu, vì sao, bao lâu tiền về
2. Khách chọn lý do và xác nhận hai bước
3. Hệ thống: chuyển trạng thái đơn, hoàn tiền theo bảng, mở lại ngày trên lịch, ghi sổ hai chiều
4. Báo cho chủ nhà kèm số tiền chủ nhà vẫn được nhận (nếu có)
5. Nếu tiền đã chuyển cho chủ nhà rồi → khấu trừ vào đợt chuyển tiền tiếp theo

---

## QT-6. Chủ nhà huỷ

1. Chủ nhà bấm huỷ → hệ thống hiện **rõ hậu quả** trước khi xác nhận: mức phạt, ngày bị chặn, ghi chú công khai trên tin đăng, ảnh hưởng tới danh hiệu
2. Chủ nhà chọn lý do và xác nhận
3. Hệ thống: hoàn 100% cho khách + tặng số dư bằng 10% giá trị đơn; trừ phạt của chủ nhà; chặn những ngày đó; ghi chú công khai lên tin đăng
4. Gửi cho khách danh sách chỗ tương tự còn trống cùng khoảng ngày
5. Nếu lý do là bất khả kháng → chuyển sang hàng chờ quản trị xem xét miễn phạt

---

## QT-7. Chủ nhà đăng tin mới

1. Chủ nhà bắt đầu quy trình từng bước; mỗi bước lưu nháp, thoát giữa chừng vẫn quay lại đúng chỗ
2. Nhập thông tin cơ bản → tiện nghi → ảnh (tối thiểu 5) → tiêu đề và mô tả → cách nhận đơn → giá → giảm giá → khai báo pháp lý
3. Xem trước đúng như khách sẽ thấy
4. Bấm xuất bản → tin vào hàng chờ kiểm duyệt
5. Kiểm duyệt xong → tin hiển thị trong kết quả tìm kiếm; nhắc chủ nhà cập nhật lịch và nội quy
6. Từ chối duyệt → nêu rõ mục nào không đạt và cách sửa, cho gửi lại

---

## QT-8. Chủ nhà quản lý lịch và giá hằng ngày

1. Mở lịch tổng, xem nhiều tin đăng cùng lúc
2. Chọn một khoảng ngày → đặt giá riêng, chặn/mở, đổi số đêm tối thiểu, ghi chú
3. Hệ thống áp ngay; giá mới xuất hiện tức thì ở tìm kiếm và trang chi tiết
4. Với ngày đã có đơn: không cho sửa giá, chỉ cho xem chi tiết đơn
5. Nếu có kết nối lịch bên ngoài, hệ thống tự động kiểm tra 2 giờ một lần và cảnh báo khi có xung đột

---

## QT-9. Hai bên đánh giá nhau

1. Ngày trả phòng: cả hai nhận lời mời viết đánh giá, hạn 14 ngày
2. Khách chấm 6 hạng mục + nhận xét công khai + góp ý riêng
3. Chủ nhà chấm khách + nhận xét công khai + có/không khuyến nghị
4. Khi **cả hai** đã gửi → công khai cùng lúc
5. Nếu chỉ một bên gửi → đến ngày thứ 14 mới công khai bên đã gửi
6. Điểm của tin đăng và của chủ nhà được tính lại
7. Chủ nhà có 30 ngày để trả lời công khai một lần
8. Nội dung vi phạm bị chặn tự động hoặc bị gỡ sau khi bị báo cáo

---

## QT-10. Tranh chấp và bồi thường

1. Một bên mở yêu cầu trong vòng 14 ngày sau ngày trả phòng: chọn loại, số tiền, mô tả, ảnh bằng chứng
2. Bên kia có **24 giờ** để đồng ý, trả một phần, hoặc phản đối
3. Đồng ý → hệ thống chuyển tiền và đóng hồ sơ
4. Phản đối hoặc im lặng → chuyển lên quản trị
5. Quản trị xem hồ sơ hai bên, có thể yêu cầu bổ sung, ra quyết định chia tiền
6. Quyết định được thi hành: trừ vào đợt chuyển tiền, hoặc thu thêm từ phương thức thanh toán của khách, hoặc chi từ quỹ bảo vệ
7. Hai bên nhận thông báo kèm lý do quyết định

---

## QT-11. Chuyển tiền cho chủ nhà

1. 24 giờ sau khi khách nhận phòng, hệ thống lên lịch một khoản chuyển
2. Kiểm tra trước khi chuyển: đơn không bị huỷ, không có tranh chấp đang mở, tài khoản nhận tiền đã xác minh
3. Chuyển tiền, ghi sổ, báo chủ nhà
4. Thất bại → thử lại, báo chủ nhà kiểm tra thông tin tài khoản
5. Có tranh chấp đang mở → tạm giữ khoản này cho tới khi xử lý xong
6. Đơn dài từ 28 đêm: chia thành nhiều đợt theo tháng

---

## QT-12. Kiểm duyệt nội dung

**Kích hoạt tự động khi:** tin đăng mới, ảnh mới, đánh giá bị gắn cờ, người dùng bị báo cáo, hoặc hệ thống phát hiện dấu hiệu bất thường.

1. Nội dung vào hàng chờ kèm điểm rủi ro và lý do
2. Kiểm duyệt viên xem, quyết định: duyệt / yêu cầu sửa / gỡ / khoá tài khoản
3. Quyết định phải kèm ghi chú lý do
4. Người dùng nhận thông báo nêu rõ vi phạm điều nào và cách khắc phục
5. Người dùng có thể khiếu nại một lần, do người khác xét lại

---

## QT-13. Khách liên hệ hỗ trợ

1. Khách bấm xin trợ giúp từ đúng đơn đang gặp vấn đề
2. Trợ lý tự động nhận biết ngữ cảnh đơn và đề xuất hành động sẵn (đổi ngày, thêm khách, xin hoàn tiền, liên hệ chủ nhà)
3. Giải quyết được → thực hiện ngay trong cuộc trò chuyện
4. Không giải quyết được hoặc khách yêu cầu → chuyển cho nhân viên, kèm toàn bộ ngữ cảnh đã trao đổi
5. Vấn đề an toàn khẩn cấp → chuyển thẳng lên hàng ưu tiên cao nhất, không qua trợ lý tự động

---

## QT-14. Chủ nhà mời co-host

1. Chủ nhà nhập email, chọn tin đăng và phạm vi quyền, tuỳ chọn chia % thu nhập
2. Người được mời nhận lời mời, xem rõ mình sẽ được làm gì
3. Chấp nhận → có quyền ngay trong phạm vi được cấp
4. Mọi hành động của co-host đều ghi rõ do ai làm
5. Chủ nhà thu hồi quyền bất cứ lúc nào, có hiệu lực ngay

---

## Danh sách tình huống bắt buộc phải chạy thử được

1. Người chưa đăng nhập tìm chỗ ở Đà Lạt cho 2 người 3 đêm → mở chi tiết → giá hiển thị đúng ở cả ba nơi (thẻ kết quả, trang chi tiết, trang thanh toán)
2. Đăng ký bằng mã OTP → lưu yêu thích → chia sẻ danh sách cho người khác
3. Đặt ngay và trả tiền → thấy trong danh sách chuyến → tải hoá đơn
4. Gửi yêu cầu đặt → chủ nhà chấp nhận → tiền được trừ → đơn xác nhận
5. Huỷ đơn chính sách Vừa phải trước 5 ngày → hoàn 100% → sổ sách cân bằng
6. Chủ nhà đăng tin mới từ đầu → xuất bản → tin xuất hiện trong kết quả tìm kiếm
7. Chủ nhà đổi giá 5 ngày trên lịch tổng → khách thấy giá mới ngay lập tức
8. Hai người cùng đặt một khoảng ngày cùng lúc → chỉ một người thành công, người kia nhận thông báo rõ ràng
9. Cả hai bên viết đánh giá → công khai cùng lúc → điểm tin đăng được cập nhật
10. Mở yêu cầu bồi thường → chủ nhà phản đối → quản trị phân xử → tiền được chia đúng quyết định
