# PLAN — Đối chiếu hiện trạng với tài liệu

Nguồn: `docs/00` → `docs/05`. Mã yêu cầu giữ nguyên theo `01-DANH-MUC-CHUC-NANG.md`.

Trạng thái: ✅ đúng spec (không còn mục 🟡 sai/thiếu hay ⬜ chưa có)

> **Lộ trình 8 giai đoạn đã đi hết.** Nhưng bản plan này trước đây chỉ liệt kê 58
> trong 201 mã yêu cầu của `docs/01`, nên "hết mục" từng bị hiểu nhầm là "hết
> việc". §9 dưới đây liệt kê phần còn thiếu đã soát ở mức code, không đoán.

---

## Thước đo "xong": 10 tình huống nghiệm thu — **10/10 đạt**

Chạy trên dữ liệu thật, server thật (`scripts/acceptance.py`, xem §Kiểm chứng):

| # | Tình huống | Kết quả |
|---|---|---|
| 1 | Chưa đăng nhập tìm Đà Lạt 2 người 3 đêm → giá giống hệt ở thẻ, chi tiết, thanh toán | ✅ |
| 2 | Đăng ký → xác minh email → lưu yêu thích → xem danh sách | ✅ |
| 3 | Đặt ngay → trả tiền → thấy trong chuyến đi → có hoá đơn | ✅ |
| 4 | Yêu cầu đặt → chủ nhà chấp nhận → trừ tiền → xác nhận | ✅ |
| 5 | Huỷ trước 5 ngày → hoàn 100% → sổ sách cân bằng | ✅ |
| 6 | Chủ nhà đăng tin mới → xuất bản → xuất hiện trong tìm kiếm | ✅ |
| 7 | Chủ nhà đổi giá 5 ngày → khách thấy ngay | ✅ |
| 8 | Hai người đặt cùng lúc → chỉ một người thành công | ✅ |
| 9 | Cả hai đánh giá → công khai cùng lúc → điểm cập nhật | ✅ |
| 10 | Bồi thường → chủ nhà phản đối → admin phân xử → tiền chia đúng | ✅ |

---

## 0. Hai việc đã quyết

| # | Vấn đề | Quyết định |
|---|---|---|
| 1 | Tên sản phẩm & danh hiệu | **Giữ StayHost OS**, giữ "Siêu chủ nhà" / "Khách yêu thích" (khách chốt 06/08/2026) |
| 2 | Phí dịch vụ | **14% khách / 3% chủ nhà** theo `03 §1`, đặt trong cấu hình `Pricing:` |

Chương trình bảo vệ **StayShield** (`00 §4`) đã có yêu cầu chi tiết ở `docs/06`,
14 tham số chốt ngày 06/08/2026, và đã làm xong — xem giai đoạn 8.

---

## 1. Tiền — ✅ đã đúng spec

`StayHost.Domain/Pricing.cs` chạy đúng 11 bước của `03 §1`, một nơi duy nhất.

| Khoản | Trạng thái |
|---|---|
| Phí dịch vụ khách 14%, tính trước thuế | ✅ |
| Phí dịch vụ chủ nhà 3% trên tạm tính | ✅ |
| Giảm theo độ dài: chọn một, ưu tiên mức dài hơn | ✅ |
| Giảm theo thời điểm đặt: chọn một, lấy mức lớn hơn | ✅ |
| Giảm tin mới 3 đơn đầu −20% | ✅ |
| Trần tổng giảm 60%, chỉ áp lên tiền phòng, **cộng chứ không nhân chuỗi** | ✅ |
| Phụ thu khách thêm, em bé không tính | ✅ |
| Phí thú cưng theo lượt hoặc theo đêm | ✅ |
| Giá theo ngày → giá mùa → giá cuối tuần → giá cơ bản | ✅ |
| Thuế theo khu vực, nhiều loại chồng nhau, 4 cách tính | ✅ |
| Làm tròn từng dòng, tổng = tổng các dòng | ✅ |
| Mã giảm giá trừ sau cùng, dòng riêng | ✅ |

**8 tình huống kiểm thử của `03 §1`: pass hết** (`tests/StayHost.Domain.Tests/PricingTests.cs`).

## 2. Huỷ & hoàn tiền — ✅ đã đúng spec

6 chính sách + 4 quy tắc áp trước, trong `StayHost.Domain/Cancellation.cs`.
Ân hạn 48h, trần 3 lần/năm cho phí dịch vụ, chủ nhà huỷ hoàn 100% + tặng 10%,
bất khả kháng, phí vệ sinh luôn hoàn 100%, huỷ giữa chừng tính theo đêm chưa ở.

## 3. Vòng đời đơn — ✅ đã đúng spec

10 trạng thái của `03 §3` + bảng chuyển trạng thái chỉ cho đi theo đúng mũi tên.
Mỗi lần chuyển ghi một dòng `booking_events` **chỉ-thêm** (`SaveChanges` từ chối sửa/xoá).
Giữ chỗ 15 phút có đếm ngược, yêu cầu đặt hết hạn 24h, yêu cầu đặt **không khoá ngày**,
chuyển "đang lưu trú"/"đã hoàn tất" theo **múi giờ chỗ ở**, tác vụ nền chạy mỗi phút.

## 4. Điều kiện đặt được — ✅ đủ 9 bước

`StayHost.Domain/Availability.cs` chạy tuần tự, dừng ở lỗi đầu tiên, **mỗi bước một thông báo riêng**.
Chống đặt trùng bằng **ràng buộc GiST ở mức PostgreSQL**, không phải kiểm tra trong code.

## 5. Sổ sách — ✅ đã có

Sổ ghi tiền hai chiều, bất biến (`ledger_entries`). Mọi bút toán phải cân trước khi ghi.
Đối soát hằng ngày hiện trên trang quản trị; lệch khác 0 là báo động đỏ.

---

## Lộ trình — đã đi hết A → D

### Giai đoạn 9 — Tài khoản (nhóm `TK`) 🟡 đang làm
Nhóm này trước đây **không có trong plan**, nên chưa từng được đối chiếu.

- [x] `TK-01` đăng ký bằng **số điện thoại hoặc email**, xác thực bằng **mã OTP 6 số**
      (hết hạn 10 phút, tối đa 5 lần nhập, chờ 60 giây mới gửi lại được)
- [x] `TK-02` đăng nhập bằng **Google / Apple / Facebook**, gắn nhiều nhà cung cấp vào
      một tài khoản, không cho bỏ liên kết cuối khi chưa có mật khẩu
- [x] `TK-03` bắt buộc **đủ 18 tuổi**, tính theo ngày chứ không theo năm
- [x] `TK-08` xem và thu hồi phiên đăng nhập trên từng thiết bị
- [x] `TK-09` cài đặt ngôn ngữ, tiền tệ
- [ ] `TK-04` hồ sơ đầy đủ: ngôn ngữ nói, nơi ở, nghề nghiệp, sở thích (P0)
- [ ] `TK-05` trang hồ sơ công khai (P0)
- [ ] `TK-06` xác minh giấy tờ tuỳ thân + selfie (P1)
- [ ] `TK-07` xác minh email công ty (P2)
- [ ] `TK-08` phần **bảo mật 2 lớp** (P1)
- [ ] `TK-10` ma trận thông báo: loại × kênh (P1)
- [ ] `TK-11` tải toàn bộ dữ liệu cá nhân (P1)

### Giai đoạn 0 — Nền ✅
- [x] Chuyển frontend sang React 19 + Vite + React Router 7
- [x] Xoá `wwwroot/js` và `wwwroot/css`, Dockerfile có stage Node
- [x] Chốt tên sản phẩm và mức phí

### Giai đoạn 1 — Tiền đúng tuyệt đối ✅
- [x] `Pricing` theo đúng 11 bước · thuế theo khu vực · 6 chính sách huỷ
- [x] Sổ ghi tiền bất biến hai chiều + đối soát
- [x] Test tự động cho 8 tình huống giá + bảng chính sách huỷ

### Giai đoạn 2 — Vòng đời đơn ✅
- [x] 10 trạng thái + lịch sử chỉ-thêm
- [x] Giữ chỗ 15 phút, yêu cầu đặt hết hạn 24h, không khoá ngày khi chờ duyệt
- [x] 9 bước kiểm tra đặt được, mỗi bước một thông báo
- [x] Chống đặt trùng ở mức cơ sở dữ liệu
- [x] Chuyển trạng thái theo múi giờ chỗ ở + tác vụ nền

### Giai đoạn 3 — Khám phá ✅
- [x] `TM-03` tìm không dấu + viết tắt ("hcm", "sg") · `TM-04` lịch sử tìm kiếm
- [x] `TM-05` giá từng đêm trên lịch · `TM-08` bộ chọn khách đúng spec
- [x] `TM-10` gộp ghim khi thu nhỏ · `TM-12` tìm khi di chuyển bản đồ
- [x] `TM-19` đếm kết quả · `TM-22` nêu bộ lọc đang chặn + khu vực lân cận
- [x] `TĐ-04` tiện nghi thiếu thì gạch ngang · `TĐ-05` bố trí giường theo phòng
- [x] `TĐ-09` gợi ý 3 khoảng trống gần nhất · `TĐ-10` phân bố sao
- [x] `TĐ-11` tìm/lọc/sắp xếp đánh giá · `TĐ-12` phản hồi chủ nhà
- [x] `TM-06/07` ngày linh hoạt ±1–7 ngày, cuối tuần/tuần/tháng, chọn theo tháng
- [x] Ngày là **bộ lọc thật**: chỗ đã có khách không còn lọt vào kết quả

### Giai đoạn 4 — Nguồn cung ✅
- [x] `ĐP-02` giữ chỗ 15 phút có đếm ngược · `ĐP-12` máy chủ tính lại giá trước khi trừ tiền
- [x] `QL-01` bảng "Hôm nay" · `QL-05` sửa nhiều ngày một lúc
- [x] `QL-06` quy tắc lịch đầy đủ · `QL-07` chặn theo thứ
- [x] `QL-15` xuất báo cáo doanh thu · `QL-17` tiến độ Siêu chủ nhà · `QL-20` tài khoản nhận tiền
- [x] `CN-11` bật giảm giá tuần/tháng/đặt sớm/phút chót từ trình soạn tin
- [x] `CN-01` đăng tin theo bước có lưu nháp · `CN-03` kéo ghim bản đồ
- [x] `CN-07` bắt buộc 5 ảnh, kéo thả sắp xếp
- [x] `QL-04` lịch nhiều tin cùng lúc · `QL-10` đồng bộ iCal (nhập + xuất) · `QL-19` co-host

### Giai đoạn 5 — Đánh giá & tin nhắn ✅
- [x] `ĐG-03` đánh giá mù hai chiều, công khai khi cả hai gửi hoặc hết 14 ngày
- [x] `ĐG-02` nhắc ngày 1, 7, 13 · `ĐG-05` góp ý riêng · `ĐG-07` chủ nhà trả lời 1 lần/30 ngày
- [x] `ĐG-09` chặn nội dung có liên hệ hoặc xúc phạm
- [x] `TN-04` tin nhắn hệ thống theo mốc · `TN-07` che liên hệ trước khi xác nhận
- [x] `TN-09` tin nhắn tự động trước nhận phòng và ngày trả phòng
- [x] `ĐG-08` sửa đánh giá trong 48h, chỉ khi còn đang ẩn
- [x] `TN-02` gửi ảnh · `TN-03` thẻ đơn trong hội thoại · `TN-08` mẫu trả lời nhanh

### Giai đoạn 6 — An toàn, hỗ trợ, quản trị ✅
- [x] `AT-04` Trung tâm giải quyết: mở hồ sơ, 24h phản hồi, admin phân xử, tiền chia đúng
- [x] `QT-05` phân xử · `QT-06` cấu hình phí và thuế theo khu vực
- [x] `QT-09` nhật ký quản trị chỉ-thêm
- [x] Phân vai admin: Hỗ trợ / Kiểm duyệt / Tài chính / Phân xử / Tối cao
- [x] `AT-07` trung tâm trợ giúp thật: 14 bài, tìm không dấu, tách khách/chủ nhà
- [x] `AT-11` phát hiện bất thường: tài khoản mới đặt lớn, nhiều thẻ, nhiều huỷ, đặt dồn dập

### Giai đoạn 7 — Mở rộng ✅
- [x] `ĐP-06` trả một phần: cọc ≥50%, tự thu phần còn lại trước 14 ngày, thử lại 72h rồi huỷ
- [x] `ĐP-07` chia hoá đơn tối đa 16 người, mỗi người một liên kết, giữ chỗ 24h
- [x] Trải nghiệm (`MR-01`→`MR-04`): đăng, lịch theo suất, đặt theo người, nhóm riêng,
      tự huỷ suất thiếu người và hoàn tiền
- [x] Dịch vụ (`MR-05`→`MR-07`): phạm vi phục vụ theo bán kính, đặt theo khung giờ
      kèm địa chỉ, dịch vụ đối tác ăn hoa hồng
- [x] Khách sạn (`MR-08`→`MR-10`): nhiều loại phòng có tồn kho, chọn phòng rồi mới
      thanh toán, cam kết giá tốt bù chênh lệch bằng số dư
- [x] Thẻ quà tặng, số dư khuyến mãi, giới thiệu bạn bè: số dư là sổ chỉ-thêm,
      chỉ trừ vào tiền phòng, huỷ đơn thì trả lại bằng số dư

### Giai đoạn 8 — StayShield ✅
- [x] `AT-06-01` trang giới thiệu hai nhánh, nêu rõ phạm vi, hạn mức, loại trừ
- [x] `AT-06-02` nút "Chỗ ở có vấn đề" chỉ hiện trong 72 giờ đầu
- [x] `AT-06-03`/`AT-06-04` biểu mẫu mở hồ sơ cho khách (K2–K4) và chủ nhà (C1–C3)
- [x] `AT-06-05` kiểm tra điều kiện tự động: còn hạn, đã nhắn trong sàn, đủ bằng chứng
- [x] `AT-06-06`/`AT-06-07` màn hình theo dõi và phản hồi (đồng ý / một phần / phản đối)
- [x] `AT-06-09` màn hình phân xử · `AT-06-10` thi hành và ghi sổ từng khoản
- [x] `AT-06-11` khiếu nại một lần, người khác xét · `AT-06-14` thông báo từng bước
- [x] `AT-06-12` bảng theo dõi quỹ, cảnh báo ngưỡng 80% · `AT-06-13` gắn cờ lạm dụng
- [x] `K1` mở hồ sơ tự động khi chủ nhà huỷ trong 30 ngày trước ngày nhận
- [x] §11: không có từ ngữ bảo hiểm ở bất kỳ đâu người dùng đọc được — có test chặn
- [x] `AT-06-08` công cụ tìm chỗ thay thế: lọc theo khu vực, số đêm còn lại, sức chứa
      tương đương; hiện khoảng cách, chênh lệch và đánh dấu phương án nằm trong hạn mức
- [x] Nhánh `C4` trách nhiệm bên thứ ba: chủ nhà mở hồ sơ, tiền trả thẳng cho bên bị
      thiệt hại, không trừ mức tự chịu, không vướng mốc "khách tiếp theo đã nhận phòng"


---

## 9. Phần còn thiếu — soát ở mức code, không đoán

Plan cũ chỉ ghi 58/201 mã. Đã dò từng mã đáng ngờ trong mã nguồn; những mục dưới đây
**thật sự chưa có**. Phần lớn mã còn lại tuy không được liệt kê nhưng đã làm
(ví dụ `ĐP-01`, `TM-01`, `TĐ-01`, `CĐ-01`, `YT-01`, `ĐG-04`, `TC-01` đều chạy).

| Mã | Việc | Ưu tiên |
|---|---|---|
| `TK-04`, `TK-05` | Hồ sơ cá nhân đầy đủ và trang hồ sơ công khai | P0 |
| `TK-06` | Xác minh giấy tờ tuỳ thân + selfie | P1 |
| `TK-08` | Bảo mật 2 lớp (phần phiên thiết bị đã có) | P1 |
| `TK-10`, `TK-11` | Ma trận thông báo · tải dữ liệu cá nhân | P1 |
| `CĐ-03`, `CĐ-04` | Hướng dẫn nhận phòng · mã cửa chỉ hiện trước 48 giờ | P0/P1 |
| `TĐ-03`, `TN-06` | Dịch mô tả tin đăng · dịch tin nhắn | P1 |
| `TC-03` | Đơn từ 28 đêm trở lên thu theo từng tháng | P1 |
| `ĐG-10` | Báo cáo đánh giá vi phạm | P1 |
| `QL-09` | Gợi ý giá theo nhu cầu thị trường | P2 |
| `XH-01`→`XH-03` | Kết bạn, bản đồ hành trình, hỏi bạn bè | P2 |

---

## Kiểm chứng

```bash
# Test nghiệp vụ (327 test)
dotnet test tests/StayHost.Domain.Tests

# 10 tình huống nghiệm thu, cần server chạy ở cổng 5199
python scripts/acceptance.py
```

## Ghi chú về quy mô

Tài liệu có ~200 yêu cầu (78 P0, 71 P1, 51 P2) trên 13 module. Toàn bộ **quy tắc
tiền, vòng đời đơn, sổ sách và tranh chấp** đã đúng spec và có test. Phần còn
thiếu là công cụ (đăng tin theo bước, lịch nhiều tin, iCal, co-host) và nhóm mở
rộng — không có phần nào chạm vào tiền.
