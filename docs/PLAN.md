# PLAN — Đối chiếu hiện trạng với tài liệu & lộ trình hoàn thành

Nguồn: `docs/00` → `docs/05`. Mã yêu cầu giữ nguyên theo `01-DANH-MUC-CHUC-NANG.md`.

Trạng thái: ✅ đúng spec · 🟡 có nhưng **sai/thiếu so với spec** · ⬜ chưa có

---

## 0. Ba việc phải quyết trước

| # | Vấn đề | Hiện tại | Spec |
|---|---|---|---|
| 1 | **Tên sản phẩm** | StayHost OS | **StayHub** (`00 §4`) |
| 2 | **Tên danh hiệu** | "Siêu chủ nhà", "Khách yêu thích" | **"Chủ nhà Ưu tú"**, **"Khách chọn"** (`00 §4`) |
| 3 | **Chương trình bảo vệ** | chưa có | **StayShield** |

Đổi tên là việc rẻ nhưng chạm nhiều nơi — làm sớm để không phải sửa lại sau.

---

## 1. Sai lệch nghiêm trọng về TIỀN (ưu tiên cao nhất)

`03 §1` nói rõ: *"Mỗi quy tắc chỉ được định nghĩa một lần"*. Hiện `Pricing.cs` đã tập trung
nhưng **công thức sai**:

| Khoản | Hiện tại | Spec `03 §1` | Mức độ |
|---|---|---|---|
| Phí dịch vụ khách | 9% | **14%**, tính **trước thuế** | 🔴 sai số tiền |
| Phí dịch vụ chủ nhà | 0% | **3%** trên tạm tính | 🔴 thiếu nguồn thu |
| Giảm theo độ dài | 7đ −10%, 28đ −20% | đúng cơ chế, nhưng phải **chọn một**, ưu tiên mức dài hơn | 🟡 |
| Giảm theo thời điểm đặt | ⬜ | đặt sớm / phút chót, **chọn một, lấy mức lớn hơn** | 🔴 thiếu |
| Giảm tin mới | ⬜ | 3 đơn đầu −20% | 🔴 thiếu |
| Trần tổng giảm | ⬜ | **≤60%**, chỉ áp lên tiền phòng | 🔴 thiếu |
| Phụ thu khách thêm | ⬜ | (người lớn+trẻ em − ngưỡng) × mức × số đêm, **em bé không tính** | 🔴 thiếu |
| Phí thú cưng | ⬜ | theo lượt hoặc theo đêm | 🔴 thiếu |
| Giá theo ngày cụ thể | ⬜ | ưu tiên: giá ngày → giá mùa → giá cuối tuần → giá cơ bản | 🟡 thiếu tầng "giá ngày" |
| Thuế | cố định 8% | theo **quy tắc khu vực**, nhiều loại chồng nhau, 4 cách tính | 🔴 sai mô hình |
| Làm tròn | 1 lần cuối | **từng dòng**, chênh lệch dồn vào dòng cuối | 🟡 |
| Mã giảm giá / số dư | ⬜ | trừ sau cùng, dòng riêng | 🔴 thiếu |

**8 tình huống kiểm thử bắt buộc** ở `03 §1` phải pass hết.

## 2. Sai lệch về HUỶ & HOÀN TIỀN

| Khoản | Hiện tại | Spec `03 §4` |
|---|---|---|
| Số chính sách | 3 | **6** (Linh hoạt, Vừa phải, Chặt, Rất chặt, Không hoàn, Dài hạn–chặt) |
| Ân hạn 48h | ⬜ | huỷ trong 48h sau đặt **và** còn ≥14 ngày → hoàn 100% |
| Phí vệ sinh | gộp chung | **luôn hoàn 100%** ở mọi chính sách |
| Phí dịch vụ | luôn hoàn | chỉ hoàn khi huỷ sớm; tối đa **3 lần/năm/tài khoản** |
| Chủ nhà huỷ | chỉ đổi trạng thái | hoàn 100% + **tặng 10% số dư**, phạt chủ nhà, chặn ngày, ghi chú công khai, mất danh hiệu 1 năm |
| Đã nhận phòng mới huỷ | ⬜ | tính theo số đêm **chưa ở** |

## 3. Vòng đời đơn — thiếu 5 trạng thái và toàn bộ lịch sử

Hiện: `Pending → Confirmed → Cancelled`.
Spec `03 §3` cần: `Chờ duyệt → Chờ thanh toán → Đã xác nhận → Đang lưu trú → Đã hoàn tất`,
cộng `Bị từ chối`, `Hết hạn`, `Không thành công`, `Khách huỷ`, `Chủ nhà huỷ`.

Thiếu hoàn toàn:
- **Lịch sử đơn** (`05`): mỗi lần đổi trạng thái ghi ai/lúc nào/vì sao, **chỉ thêm, không sửa**
- **Giữ chỗ 15 phút** khi vào thanh toán (`03 §2`)
- **Yêu cầu đặt tự hết hạn 24h** (`ĐP-16`)
- **Yêu cầu đặt KHÔNG khoá ngày** (`03 §2`)
- Chuyển trạng thái theo **múi giờ chỗ ở**

## 4. Điều kiện đặt được — thiếu 6/9 bước kiểm tra

`03 §2` yêu cầu kiểm tra tuần tự 9 bước, dừng ở lỗi đầu tiên và nêu đúng lý do.
Hiện chỉ có: trạng thái hiển thị, sức chứa, số đêm tối thiểu, trùng ngày, ngày bị khoá.

Thiếu: thú cưng · **báo trước + giờ cắt** · **tầm nhìn lịch** · số đêm tối đa ·
**số đêm tối thiểu riêng theo ngày** · **chặn thứ trong tuần** · **thời gian dọn dẹp**.

## 5. Sổ sách — chưa có

`00 §6.1` và `05` yêu cầu **sổ ghi tiền bất biến**, ghi hai chiều, đối soát hằng ngày.
Hiện chỉ có bảng `payments` một chiều. Đây là điều kiện bắt buộc để vận hành thật.

---

## Lộ trình

Thứ tự theo `00 §5`: **A → B → C → D**, không nhảy cóc.

### Giai đoạn 0 — Nền (đang làm)
- [x] Chuyển frontend sang **React + Vite + React Router** (theo yêu cầu; Airbnb cũng dùng React 19 + React Router 7)
- [ ] Đổi thương hiệu StayHost → **StayHub**, đổi tên danh hiệu

### Giai đoạn 1 — Tiền đúng tuyệt đối (chặn mọi thứ khác)
- [ ] Viết lại `Pricing` theo đúng 11 bước của `03 §1`
- [ ] Mô hình **thuế theo khu vực** (`TaxRule`), nhiều loại chồng nhau
- [ ] **6 chính sách huỷ** + 4 quy tắc áp trước + hoàn theo đêm chưa ở
- [ ] **Sổ ghi tiền bất biến** hai chiều + đối soát hằng ngày
- [ ] Bộ test tự động cho **8 tình huống giá** + bảng chính sách huỷ

### Giai đoạn 2 — Vòng đời đơn đúng
- [ ] 9 trạng thái + bảng **lịch sử đơn** chỉ-thêm
- [ ] Giữ chỗ 15 phút, yêu cầu đặt hết hạn 24h, không khoá ngày khi chờ duyệt
- [ ] 9 bước kiểm tra đặt được, mỗi bước có thông báo lỗi riêng
- [ ] Chống đặt trùng ở mức cơ sở dữ liệu (không chỉ kiểm tra trong code)
- [ ] Chuyển trạng thái theo múi giờ chỗ ở + tác vụ nền

### Giai đoạn 3 — Khám phá (nhóm A còn thiếu)
- [ ] `TM-03` tìm **không dấu** ("da lat" → Đà Lạt), `TM-04` lịch sử tìm kiếm
- [ ] `TM-05` hiện **giá từng đêm trên lịch**, `TM-06/07` ngày linh hoạt & theo tháng
- [ ] `TM-08` bộ chọn khách đúng spec (em bé không tính sức chứa)
- [ ] `TM-10` **gộp ghim** khi thu nhỏ, `TM-12` **tìm khi di chuyển bản đồ**
- [ ] `TM-19` **đếm kết quả ngay khi đổi lọc** (chưa cần áp dụng)
- [ ] `TM-22` không kết quả → nêu bộ lọc đang chặn + khu vực lân cận
- [ ] `TĐ-04` tiện nghi **không có thì gạch ngang**, `TĐ-05` bố trí giường theo phòng
- [ ] `TĐ-09` ngày kín → gợi ý 3 khoảng trống gần nhất
- [ ] `TĐ-10` **phân bố sao**, `TĐ-11` tìm/lọc/sắp xếp đánh giá, `TĐ-12` phản hồi chủ nhà

### Giai đoạn 4 — Nguồn cung (nhóm C)
- [ ] `CN-01` quy trình đăng tin **theo bước, lưu nháp**
- [ ] `CN-03` kéo ghim bản đồ, `CN-05` cấu hình giường theo phòng
- [ ] `CN-07` tối thiểu 5 ảnh, kéo thả sắp xếp, gắn nhãn phòng
- [ ] `CN-12` khai báo pháp lý & an toàn
- [ ] `QL-01` bảng **"Hôm nay"**, `QL-04` lịch nhiều tin cùng lúc
- [ ] `QL-05` chọn nhiều ngày đặt giá/chặn hàng loạt
- [ ] `QL-06` quy tắc lịch đầy đủ, `QL-07` chặn theo thứ
- [ ] `QL-10` **đồng bộ lịch iCal** hai chiều + cảnh báo xung đột
- [ ] `QL-17` theo dõi tiến độ Chủ nhà Ưu tú theo 4 tiêu chí
- [ ] `QL-19` **co-host** với 5 phạm vi quyền
- [ ] `QL-20` tài khoản nhận tiền + lịch trả tiền

### Giai đoạn 5 — Đánh giá & tin nhắn đúng spec
- [ ] `ĐG-03` **đánh giá mù hai chiều**, công khai khi cả hai gửi hoặc hết 14 ngày
- [ ] `ĐG-02` nhắc ngày 1, 7, 13 · `ĐG-07` chủ nhà trả lời 1 lần trong 30 ngày
- [ ] `ĐG-08` sửa trong 48h · `ĐG-09` chặn nội dung vi phạm
- [ ] `TN-07` **che số điện thoại/email/link** trước khi đơn xác nhận
- [ ] `TN-02` gửi ảnh · `TN-03` thẻ đơn trong hội thoại · `TN-08` mẫu trả lời nhanh
- [ ] `TN-09` tin nhắn tự động theo mốc

### Giai đoạn 6 — An toàn, hỗ trợ, quản trị (nhóm D)
- [ ] `AT-04` **Trung tâm giải quyết** (bồi thường, 24h phản hồi, admin phân xử)
- [ ] `AT-07` trung tâm trợ giúp · `AT-11` phát hiện bất thường
- [ ] `QT-02` hàng chờ kiểm duyệt · `QT-05` phân xử · `QT-06` cấu hình phí/thuế
- [ ] `QT-09` **nhật ký quản trị** đầy đủ
- [ ] Phân vai admin: Hỗ trợ / Kiểm duyệt / Tài chính / Tối cao

### Giai đoạn 7 — Mở rộng
- [ ] Trải nghiệm (`MR-01`→`MR-04`), Dịch vụ (`MR-05`→`MR-07`), Khách sạn (`MR-08`→`MR-10`)
- [ ] Thẻ quà tặng, số dư khuyến mãi, giới thiệu bạn bè

---

## 10 tình huống nghiệm thu (`04`)

Đây là thước đo "xong" — phải chạy được đầu-cuối trên dữ liệu thật:

1. Chưa đăng nhập tìm Đà Lạt 2 người 3 đêm → giá **giống hệt** ở thẻ kết quả, trang chi tiết, trang thanh toán
2. Đăng ký OTP → lưu yêu thích → chia sẻ danh sách
3. Đặt ngay → trả tiền → thấy trong chuyến đi → tải hoá đơn
4. Yêu cầu đặt → chủ nhà chấp nhận → trừ tiền → xác nhận
5. Huỷ chính sách Vừa phải trước 5 ngày → hoàn 100% → **sổ sách cân bằng**
6. Chủ nhà đăng tin mới → xuất bản → xuất hiện trong tìm kiếm
7. Chủ nhà đổi giá 5 ngày → khách thấy ngay
8. **Hai người đặt cùng lúc → chỉ một người thành công**
9. Cả hai đánh giá → công khai cùng lúc → điểm cập nhật
10. Bồi thường → chủ nhà phản đối → admin phân xử → tiền chia đúng

---

## Ghi chú về quy mô

Tài liệu có **~200 yêu cầu** (78 P0, 71 P1, 51 P2) trên 13 module. Phần đã làm phủ
khoảng **35% nhóm P0** và một phần P1, nhưng **các quy tắc tiền đang sai** nên phải
sửa trước khi xây thêm — đúng theo nguyên tắc `00 §6.8`.
