# 00 — Tổng quan & phạm vi nghiệp vụ

Sản phẩm: **StayHub** — sàn giao dịch hai chiều kết nối người có chỗ ở / dịch vụ với người đi du lịch.
Tài liệu này mô tả **nghiệp vụ**: sàn làm được gì, cho ai, theo quy tắc nào. Không mô tả công nghệ.

---

## 1. Mô hình kinh doanh

Sàn không sở hữu chỗ ở. Sàn:
1. Tập hợp nguồn cung (chủ nhà đăng tin)
2. Giúp khách tìm đúng thứ họ cần
3. Giữ tiền của khách, trả cho chủ nhà sau khi khách nhận phòng
4. Thu phí dịch vụ hai đầu
5. Đứng giữa xử lý huỷ, tranh chấp, bồi thường

**Nguồn thu:** phí dịch vụ khách (~14% giá trước thuế) + phí dịch vụ chủ nhà (~3%) + hoa hồng dịch vụ bên thứ ba + phí chuyển đổi ngoại tệ.

## 2. Bốn dòng cung ứng

| Dòng | Bán cái gì | Đơn vị | Đặc thù nghiệp vụ |
|---|---|---|---|
| **Chỗ ở (Homes)** | Nhà, căn hộ, phòng | đêm | Lịch theo ngày, tối thiểu/tối đa số đêm, thời gian dọn dẹp giữa 2 khách |
| **Khách sạn** | Phòng khách sạn | đêm × loại phòng | Một cơ sở có nhiều loại phòng, mỗi loại có số lượng tồn |
| **Trải nghiệm** | Hoạt động do người địa phương dẫn | vé/người | Lịch theo suất giờ, sức chứa nhóm, có thể huỷ nếu không đủ người |
| **Dịch vụ** | Đầu bếp, chụp ảnh, massage, PT, đưa đón sân bay, giữ hành lý, đi chợ hộ, thuê xe | buổi hoặc đơn hàng | Có phạm vi phục vụ theo bán kính, có thể di chuyển tới chỗ khách |

Bốn dòng dùng chung: tìm kiếm, danh sách yêu thích, tin nhắn, thanh toán, đánh giá, chuyến đi, thông báo, hỗ trợ.

## 3. Các bên tham gia

### 3.1. Khách (Guest)
Tìm, đặt, thanh toán, nhắn tin với chủ nhà, quản lý chuyến đi, đổi/huỷ, đánh giá, khiếu nại.

### 3.2. Chủ nhà (Host)
Đăng tin, đặt giá và lịch, duyệt yêu cầu đặt, nhắn tin, chuẩn bị đón khách, nhận tiền, đánh giá khách, xử lý sự cố.

### 3.3. Chủ nhà đồng hành (Co-host)
Được chủ nhà uỷ quyền theo từng phạm vi. Có thể chia % thu nhập.

| Phạm vi quyền | Được làm gì |
|---|---|
| Lịch | Sửa giá, chặn/mở ngày |
| Tin nhắn | Trả lời khách |
| Tin đăng | Sửa nội dung, ảnh, tiện nghi |
| Đặt phòng | Chấp nhận/từ chối, huỷ, đổi lịch |
| Tài chính | Chỉ **xem** báo cáo, không rút tiền |

Co-host **không bao giờ** được: đổi tài khoản nhận tiền, xoá tin đăng, thu hồi quyền của chủ nhà.

### 3.4. Quản trị viên (Admin)
Kiểm duyệt tin đăng, xử lý báo cáo, phân xử tranh chấp, hoàn tiền thủ công, cấu hình phí/thuế theo khu vực, khoá tài khoản, xem sổ sách.

Phân vai admin: **Hỗ trợ** (chỉ đọc + trả lời ticket) · **Kiểm duyệt** (duyệt/gỡ nội dung) · **Tài chính** (hoàn tiền, đối soát, payout) · **Quản trị tối cao**.

Mọi hành động của admin phải để lại dấu vết ai làm, lúc nào, trước/sau ra sao. Việc đăng nhập thay mặt người dùng phải được ghi nhận và có thời hạn.

## 4. Ranh giới thương hiệu

Sao chép **cách vận hành**, không sao chép **thương hiệu**. Không dùng tên, logo, ảnh, phông chữ, câu chữ marketing hay điều khoản của Airbnb. Các chương trình con đặt tên riêng:

| Airbnb | StayHub |
|---|---|
| AirCover | **StayShield** — chương trình bảo vệ hai đầu |
| Superhost | **Chủ nhà Ưu tú** |
| Guest Favorite | **Khách chọn** |

## 5. Bốn nhóm chức năng, xếp theo thứ tự triển khai

| Nhóm | Nội dung | Ưu tiên |
|---|---|---|
| **A. Khám phá** | Tìm kiếm, lọc, bản đồ, trang chi tiết, yêu thích, tài khoản | Bắt buộc — nền của mọi thứ |
| **B. Giao dịch** | Đặt chỗ, thanh toán, huỷ, hoàn tiền, chuyến đi, hoá đơn, trả tiền cho chủ nhà | Bắt buộc |
| **C. Nguồn cung** | Đăng tin, quản lý lịch & giá, duyệt đơn, tin nhắn, đánh giá, báo cáo thu nhập | Bắt buộc |
| **D. Mở rộng** | Trải nghiệm, dịch vụ, khách sạn, an toàn & tranh chấp, quà tặng, giới thiệu bạn bè, quản trị | Sau khi A–C chạy ổn |

Không làm nhóm sau khi nhóm trước chưa chạy được từ đầu đến cuối trên dữ liệu thật.

## 6. Nguyên tắc nghiệp vụ xuyên suốt

1. **Tiền phải khớp tuyệt đối.** Mọi đồng tiền vào ra đều ghi sổ. Tổng tiền khách trả = tiền chủ nhà nhận + phí sàn + thuế + hoàn trả. Đối soát hằng ngày, lệch một đồng là báo động.
2. **Không có bước nào làm mất dấu vết.** Đơn đặt, thanh toán, huỷ, đổi lịch — mỗi lần đổi trạng thái là một dòng lịch sử, không ghi đè.
3. **Giá hiển thị cho khách phải là giá cuối cùng.** Có công tắc "hiện giá gồm thuế và phí"; không bao giờ phát sinh phí lạ ở bước cuối.
4. **Một khoảng ngày chỉ bán được một lần.** Chống đặt trùng là yêu cầu bắt buộc, không phải tối ưu.
5. **Địa chỉ chính xác chỉ lộ sau khi đơn được xác nhận.** Trước đó chỉ hiện vị trí gần đúng trong bán kính ~300m.
6. **Đánh giá phải trung thực.** Chỉ người đã ở thật mới được đánh giá; hai bên viết mù, không ai đọc được của người kia trước khi cả hai gửi.
7. **Khách và chủ nhà chỉ trao đổi trong sàn** cho tới khi đơn được xác nhận. Số điện thoại, email, link ngoài bị che trong giai đoạn này.
8. **Mọi quy tắc tính tiền chỉ được định nghĩa một lần.** Tìm kiếm, trang chi tiết và trang thanh toán phải cho ra cùng một con số.

## 7. Chỉ số cần theo dõi

**Sàn:** số lượt tìm → xem chi tiết → đặt (tỉ lệ chuyển đổi từng bước), giá trị đơn trung bình, tổng giá trị giao dịch, tỉ lệ huỷ, tỉ lệ đơn bị chủ nhà từ chối, thời gian phản hồi trung vị.

**Chủ nhà:** tỉ lệ lấp đầy, doanh thu/đêm khả dụng, lượt xem tin, tỉ lệ xem→đặt, điểm đánh giá, tỉ lệ phản hồi, tỉ lệ chấp nhận, tỉ lệ tự huỷ.

**Chất lượng:** tỉ lệ đơn có khiếu nại, thời gian xử lý tranh chấp, tỉ lệ tin bị gỡ, tỉ lệ hoàn tiền do lỗi chủ nhà.
