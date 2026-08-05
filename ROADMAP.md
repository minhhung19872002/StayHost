# StayHost OS — Roadmap

Mục tiêu: đưa StayHost từ bản demo lên mức **vận hành kinh doanh thật**, ngang tính
năng cốt lõi với airbnb.com.

Trạng thái: ✅ xong · 🔨 đang làm · ⬜ chưa bắt đầu

---

## Nền tảng ✅

| Hạng mục | Trạng thái |
|---|---|
| Trang chủ dạng rail carousel theo thành phố + chủ đề | ✅ |
| Trang kết quả: map cố định, chip lọc, card kiểu Airbnb | ✅ |
| Trang chi tiết: gallery, sub-nav dính, banner khách yêu thích, lịch 4 tháng, đánh giá có breakdown + chip chủ đề, bản đồ khu vực | ✅ |
| Bộ lọc đầy đủ (histogram giá, loại nơi ở, phòng/giường, tiện nghi) | ✅ |
| Đặt chỗ, chặn trùng ngày, wishlist, chuyến đi | ✅ |
| Tài khoản: đăng ký/đăng nhập PBKDF2, phiên cookie, gộp dữ liệu ẩn danh | ✅ |
| Trang chủ nhà: CRUD chỗ nghỉ, khoá lịch, duyệt đơn, doanh thu | ✅ |
| Tin nhắn guest ↔ host · Đánh giá sau kỳ nghỉ | ✅ |
| Docker Compose + migrations + seed | ✅ |

## P1 — Sửa lỗi đã xác nhận ✅

| # | Lỗi | Trạng thái |
|---|---|---|
| 1 | Chip "Huỷ miễn phí" thực chất lọc *đặt ngay* | ✅ tách thành 2 chip đúng nghĩa |
| 2 | Lịch chủ nhà không hiện lượt đặt đã có | ✅ |
| 3 | Link "Xem chỗ nghỉ" trong tin nhắn dùng id làm slug → 404 | ✅ |
| 4 | Bộ lọc `instantBook` không ghi vào URL | ✅ |
| 5 | Đặt chỗ không yêu cầu đăng nhập | ✅ |
| 6 | Ảnh chỗ nghỉ chỉ dán URL, không upload được | ✅ upload thật + kiểm magic bytes |

## P2 — Tiền bạc đúng nghiệp vụ ✅

- ✅ `Pricing` tập trung: phụ thu cuối tuần, giảm giá ở dài ngày, **thuế VAT 8%**
- ✅ **3 bậc chính sách huỷ** + tính hoàn tiền thật, xem trước trước khi huỷ
- ✅ Checkout **3 bước** (chuyến đi → thanh toán → xác nhận), 3 phương thức trả tiền
- ✅ Trang **chi tiết chuyến đi** + **hoá đơn in được** (`/trips/{id}`)

## P3 — Hoàn thiện tài khoản ✅

- ✅ Quên mật khẩu (token dùng một lần, hết hạn 2 giờ) · Đổi mật khẩu
- ✅ Xác minh email
- ✅ Quản lý phiên đăng nhập: xem thiết bị & thu hồi từng phiên

## P4 — Trải nghiệm tìm kiếm ✅

- ✅ Gợi ý điểm đến khi gõ (thành phố + chỗ nghỉ)
- ✅ Lightbox ảnh: prev/next, filmstrip, phím mũi tên, bộ đếm
- ✅ Phân trang số trang
- ✅ Hover card ↔ sáng marker trên bản đồ

## P5 — Wishlist nhiều danh sách ✅

- ✅ Tạo / đổi tên / xoá danh sách có tên, danh sách mặc định
- ✅ Chuyển chỗ nghỉ giữa các danh sách
- ✅ Trang chỉ mục có ảnh bìa 2×2 và trang chi tiết từng danh sách

## P6 — Công cụ chủ nhà ✅

- ✅ Upload ảnh thật (file → server, kiểm định dạng bằng magic bytes)
- ✅ Lịch 2 tháng trực quan: đã đặt / bị khoá / giá mùa
- ✅ Giá theo mùa (quy tắc theo khoảng ngày, ghi đè giá cơ bản)
- ✅ Chủ nhà đánh giá khách
- ✅ Tự động tính Siêu chủ nhà (≥4.8 sao, ≥5 kỳ nghỉ hoàn tất, không huỷ)

## P7 — Nền tảng vận hành ✅

- ✅ Thông báo trong app (chuông + badge + đánh dấu đã đọc)
- ✅ Hàng đợi email giao dịch cho mọi sự kiện đặt / duyệt / huỷ / tin nhắn
- ✅ Báo cáo chỗ nghỉ lưu vào DB
- ✅ **Trang quản trị** `/admin`: doanh thu nền tảng, kiểm duyệt chỗ nghỉ, xử lý báo cáo

---

## Tiếp theo (chưa làm)

| Hạng mục | Ghi chú |
|---|---|
| Cổng thanh toán thật (Stripe / VNPay) | Cần tài khoản merchant; hiện `Payment` đã có đủ trường để nối |
| Gửi email thật (SMTP / SendGrid) | Bảng `email_messages` đã là hàng đợi, chỉ thiếu worker |
| Trải nghiệm & Dịch vụ | Hai dòng sản phẩm riêng của Airbnb |
| Đa ngôn ngữ thật | Hiện đổi ngôn ngữ mới đổi nhãn hiển thị |
| Tìm theo vùng bản đồ đang xem | API đã có sẵn hook `currentMapBounds()` |
| Ứng dụng di động native | — |
