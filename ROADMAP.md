# StayHost OS — Roadmap

Mục tiêu: đưa StayHost từ bản demo lên mức **vận hành kinh doanh thật**, ngang tính
năng cốt lõi với airbnb.com.

Trạng thái: ✅ xong · 🔨 đang làm · ⬜ chưa bắt đầu

---

## Đã có (nền tảng)

| Hạng mục | Trạng thái |
|---|---|
| Trang chủ dạng rail carousel theo thành phố + chủ đề | ✅ |
| Trang kết quả: map cố định, chip lọc, card kiểu Airbnb | ✅ |
| Trang chi tiết: gallery, sub-nav dính, banner khách yêu thích, lịch 4 tháng, đánh giá có breakdown + chip chủ đề, bản đồ khu vực | ✅ |
| Bộ lọc đầy đủ (histogram giá, loại nơi ở, phòng/giường, tiện nghi) | ✅ |
| Đặt chỗ, chặn trùng ngày, wishlist, chuyến đi | ✅ |
| Tài khoản: đăng ký/đăng nhập PBKDF2, phiên cookie, gộp dữ liệu ẩn danh | ✅ |
| Trang chủ nhà: CRUD chỗ nghỉ, khoá lịch, duyệt đơn, doanh thu | ✅ |
| Tin nhắn guest ↔ host | ✅ |
| Đánh giá sau kỳ nghỉ | ✅ |
| Bản ghi thanh toán tách phí nền tảng / payout | ✅ |
| Docker Compose + migrations + seed | ✅ |

---

## P1 — Sửa lỗi đã xác nhận 🔨

| # | Lỗi | Trạng thái |
|---|---|---|
| 1 | Chip "Huỷ miễn phí" thực chất lọc *đặt ngay* — sai nhãn, sai hành vi | 🔨 |
| 2 | Lịch chủ nhà không hiện lượt đặt đã có (API trả về, UI bỏ qua) | 🔨 |
| 3 | Link "Xem chỗ nghỉ" trong tin nhắn dùng `listingId` làm slug → 404 | 🔨 |
| 4 | Bộ lọc `instantBook` không ghi vào URL → reload mất filter | 🔨 |
| 5 | Đặt chỗ không yêu cầu đăng nhập (không truy được trách nhiệm) | 🔨 |
| 6 | Ảnh chỗ nghỉ chỉ dán URL, không upload được file | 🔨 |

## P2 — Tiền bạc đúng nghiệp vụ 🔨

- Công cụ tính giá tập trung: phụ thu cuối tuần, giảm giá theo độ dài, **thuế VAT 8%**
- **3 bậc chính sách huỷ** (Linh hoạt / Trung bình / Nghiêm ngặt) + tính hoàn tiền thật
- Luồng checkout **3 bước**: xác nhận → thanh toán → hoàn tất
- Trang **chi tiết chuyến đi** + **hoá đơn in được**
- Huỷ chỗ hiển thị số tiền hoàn trước khi xác nhận

## P3 — Hoàn thiện tài khoản ⬜

- Quên mật khẩu (token hết hạn) · Đổi mật khẩu
- Xác minh email / số điện thoại
- Quản lý phiên đăng nhập (xem & thu hồi thiết bị)

## P4 — Trải nghiệm tìm kiếm ⬜

- Gợi ý điểm đến khi gõ (autocomplete)
- Lightbox ảnh có prev/next + đếm ảnh
- Phân trang số trang (thay "xem thêm")
- Hover card ↔ sáng marker trên bản đồ
- Tìm theo vùng bản đồ đang xem

## P5 — Wishlist nhiều danh sách ⬜

- Tạo / đổi tên / xoá danh sách có tên
- Chọn danh sách khi bấm ♥
- Trang chi tiết từng danh sách

## P6 — Công cụ chủ nhà ⬜

- **Upload ảnh thật** (file → server)
- Lịch dạng tháng xem & khoá trực quan
- Giá theo mùa / cuối tuần cấu hình được
- Chủ nhà đánh giá khách
- Tự động tính danh hiệu Siêu chủ nhà

## P7 — Nền tảng vận hành ⬜

- Thông báo trong app (chuông + danh sách)
- Email giao dịch (đặt / xác nhận / huỷ)
- Báo cáo chỗ nghỉ lưu vào DB
- **Trang quản trị**: duyệt chỗ nghỉ, xử lý báo cáo, xem giao dịch

---

## Ngoài phạm vi hiện tại

- Cổng thanh toán thật (Stripe / VNPay) — cần tài khoản merchant
- Trải nghiệm & Dịch vụ (2 dòng sản phẩm riêng của Airbnb)
- Ứng dụng di động native
