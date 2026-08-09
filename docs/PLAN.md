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

## 6. Xếp hạng kết quả tìm kiếm — ✅ đúng bảng trọng số của `03 §6`

Trước đây sắp xếp mặc định là `IsGuestFavorite → Rating → Id`. Giờ là điểm tổng
hợp trong `StayHost.Domain/Ranking.cs`; mỗi yếu tố quy về thang 0–1 rồi mới nhân
trọng số, nên bảng dưới là **thứ duy nhất** quyết định yếu tố nào nặng hơn.

| Yếu tố | Trọng số | Cách tính |
|---|---|---|
| Gần trung tâm khu vực tìm | 30% | ở tâm được 1, ở rìa vùng được 0 |
| Chất lượng | 25% | điểm kéo về trung bình theo số lượng đánh giá — 3 đánh giá 5 sao **không** hơn 200 đánh giá 4.8 |
| Tỉ lệ xem→đặt gần đây | 15% | đặt/xem trong 30 ngày, 1/5 là kịch trần |
| Giá cạnh tranh | 10% | so với **trung vị** của tập kết quả cùng vùng; bằng trung vị được 0.5 |
| Chất lượng phục vụ | 10% | 70% tỉ lệ phản hồi + 30% có bật đặt ngay |
| Chất lượng ảnh | 5% | 10 ảnh là đủ bộ |
| Tin mới | 5% | 30 ngày đầu, giảm dần |

**Trừ điểm:** điểm < 4.0 (−0.25) · chủ nhà tự huỷ > 5% (−0.20) · dưới 5 ảnh
(−0.10) · tin chưa hoàn tất (−0.15). Điểm không bao giờ âm.

**Đa dạng hoá:** 12 kết quả đầu tối đa 2 chỗ mỗi chủ nhà. Chỗ thứ ba **bị đẩy ra
sau cửa sổ chứ không bị loại** — nó vẫn là kết quả khách đã lọc ra. Khi không đủ
chủ nhà khác nhau để lấp 12 chỗ thì nới quy tắc và lấp tiếp theo thứ tự điểm, vì
trả về 4 kết quả thay vì 12 còn tệ hơn cho khách.

**Hai chỗ tự quyết, cần khách xác nhận:**
- *"Trung tâm khu vực tìm"* = tâm khung bản đồ khi khách đang tìm bằng bản đồ,
  còn lại là **tâm của chính tập kết quả**. Không cần bảng địa danh riêng, và
  theo định nghĩa thì đó là vùng khách đang xem. Bán kính = chỗ xa nhất trong tập,
  sàn 5km để một thành phố nhỏ không biến 200m thành cả thang điểm.
- *"Chỗ tương đương cùng khu vực"* để so giá = tập kết quả hiện tại.

**Lượt xem là dữ liệu mới.** Bảng `listing_views` ghi một dòng mỗi tin mỗi ngày,
tăng khi trang chi tiết được phục vụ. Đếm cả đời sẽ trả lời sai câu hỏi "gần đây".
Việc đếm không bao giờ làm hỏng request — một tín hiệu xếp hạng không đáng để trang
khách đang đọc trả về 500.

**Giới hạn đã biết:** điểm được tính trong bộ nhớ trên toàn bộ tập đã lọc, vì công
thức cân bảy thứ mà truy vấn không gộp một lượt được. Ổn khi một lượt tìm khớp
hàng nghìn dòng; tới hàng triệu thì cần cột điểm tính sẵn và job chạy đêm —
`Ranking` không phải sửa gì cho việc đó. Các kiểu sắp xếp có tên (giá, đánh giá,
số đánh giá) vẫn chạy bằng SQL.

## 7. Danh hiệu — ✅ đã cấp và thu hồi tự động

`docs/03 §8`. Ngưỡng nằm **một chỗ duy nhất** trong `StayHost.Domain/Badges.cs`,
để màn hình tiến độ (`QL-17`) và job xét danh hiệu không thể nói khác nhau —
trước đây mỗi bên tự tính lấy.

| Việc | Chu kỳ | Trạng thái |
|---|---|---|
| Cấp / thu hồi **Chủ nhà Ưu tú** (đủ cả 4 tiêu chí) | mỗi quý: 1/1, 1/4, 1/7, 1/10 | ✅ |
| Cấp / thu hồi **Khách chọn** (điểm ≥ 4.9, ≥ 5 đánh giá, ít huỷ, không bị báo cáo) | hằng tuần, mốc thứ Hai | ✅ |
| Hiện tiến độ 4 tiêu chí cho chủ nhà | — | ✅ `QL-17`, dùng chung phép tính |
| Mất danh hiệu rồi đạt lại thì có lại | — | ✅ mỗi kỳ tính lại từ đầu |
| Báo cho chủ nhà khi được cấp / bị dừng | — | ✅ kèm ngày xét lại kế tiếp |

**Xét theo dấu kỳ, không theo "hôm nay có phải ngày 1 không".** Mỗi chủ nhà và
mỗi tin đăng mang một cột ghi kỳ đã xét gần nhất; job so cột đó với đầu quý (hoặc
thứ Hai của tuần). Nhờ vậy máy chủ tắt đúng ngày 1/4 thì ngày 2/4 vẫn xét bù, và
chạy job hai lần trong ngày không đổi gì.

**Cờ trên tin đăng là bản sao, không phải sự thật thứ hai.** Bộ lọc tìm kiếm đọc
`listing.IsSuperhost`, nên tin lệch pha sẽ lọt vào kết quả "Siêu chủ nhà" của một
chủ nhà không có danh hiệu. Đã bịt cả ba đường sinh ra lệch:

- job xét danh hiệu đồng bộ tin **mỗi lần xét**, không chỉ khi danh hiệu đổi;
- lưu tin (`HostController`) luôn lấy cờ từ chủ nhà — tin mới đăng của một Siêu
  chủ nhà trước đây không có huy hiệu cho tới kỳ xét sau;
- dữ liệu mẫu suy cờ từ chủ nhà thay vì gán riêng cho từng tin (trước là 15 tin lệch).

**Đã gỡ một quy tắc Siêu chủ nhà thứ hai.** `HostController` có một hàm riêng
tính lại danh hiệu ngay khi chủ nhà đánh giá khách, với ngưỡng khác spec
(`điểm ≥ 4.8 && ≥ 5 chuyến && 0 lần tự huỷ` — không xét tỉ lệ phản hồi, không xét
mốc 10 chuyến/năm) và **không theo kỳ nào cả**. Nó âm thầm tước danh hiệu giữa
quý. Giờ chỉ còn một nơi quyết định.

**Một chỗ tự quyết cần khách xác nhận:** `docs/03 §8` chỉ nói "tỉ lệ huỷ thấp" cho
Khách chọn mà không nêu số. Đang lấy **< 5%** (gấp năm lần mức 1% của Chủ nhà Ưu
tú, vì danh hiệu này nói về chỗ ở chứ không nói về người vận hành). Đổi ở
`Badges.FavoriteCancelRate`.

**Lưu ý về dữ liệu mẫu:** danh mục seed được đóng dấu "đã xét cho kỳ này", nếu
không lần quét đầu tiên sẽ tước sạch danh hiệu của một cơ sở dữ liệu vừa dựng.
Sang kỳ sau thì các chủ nhà mẫu **sẽ mất danh hiệu thật** — họ không có lượt đón
khách nào trong năm. Đó là hành vi đúng theo spec, không phải lỗi.

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
- [x] `TK-04` hồ sơ đầy đủ: ảnh đại diện, tên hiển thị, ngôn ngữ nói, nơi ở, nghề nghiệp,
      sở thích. Ảnh chỉ nhận tệp vừa tải lên sàn, không nhận địa chỉ bên ngoài
- [x] `TK-05` trang hồ sơ công khai `/users/:id`: ảnh, năm tham gia, huy hiệu xác minh,
      giới thiệu, ngôn ngữ, tin đăng đang có, đánh giá nhận được từ **cả hai phía**
- [x] `TK-06` xác minh danh tính: ảnh giấy tờ + ảnh chân dung, người thật duyệt, chỉ giữ
      4 số cuối của số giấy tờ; duyệt xong mới có huy hiệu trên hồ sơ công khai
- [x] `TK-08` **bảo mật 2 lớp**: mật khẩu đúng chưa mở phiên, còn phải nhập mã 6 số;
      bật cần mã, tắt cần mật khẩu
- [x] `TK-10` ma trận thông báo loại × kênh. Thông báo đơn đặt và thanh toán **không tắt
      được** (`docs/03 §11`), tiếp thị mặc định tắt
- [x] `TK-11` tải toàn bộ dữ liệu cá nhân về một tệp JSON, tải ngay chứ không chờ email.
      Ngoài ra `docs/08 §9`: gửi yêu cầu chính thức, admin cấp đường dẫn có hạn 7 ngày
- [x] `TK-12` tạm vô hiệu hoá / xoá tài khoản, ẩn danh dữ liệu giao dịch — người dùng
      tự gửi yêu cầu xoá; ẩn danh giữ nguyên đơn, giao dịch và sổ tiền (`docs/08 §9`)
- [ ] `TK-07` xác minh email công ty (P2)
- [ ] `TK-13` liên hệ khẩn cấp (P2)

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
- [x] `QT-01` bảng điều khiển số liệu · `QT-02` hàng chờ kiểm duyệt nội dung
- [x] `QT-03` tra cứu, khoá, mở khoá tài khoản (chi tiết ở `docs/08`)
- [x] `QT-04` tra cứu đơn, hoàn tiền thủ công, điều chỉnh khoản trả cho chủ nhà
- [x] `QT-05` phân xử · `QT-06` cấu hình phí và thuế theo khu vực
- [x] `QT-09` nhật ký quản trị chỉ-thêm · `QT-10` đăng nhập thay mặt (`docs/08 §7`)
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

### Giai đoạn 10 — Nhận phòng (nhóm `CĐ`) ✅
- [x] `CĐ-03` hướng dẫn nhận phòng: giờ nhận/trả, cách vào nhà, địa chỉ đầy đủ, số điện
      thoại chủ nhà, wifi, chỉ đường, hướng dẫn thiết bị — chủ nhà tự điền ở bước
      "Nhận phòng" của trình soạn tin
- [x] `CĐ-04` mã cửa **chỉ hiện từ 48 giờ trước giờ nhận phòng**, và chỉ với đơn đã xác
      nhận (`docs/03 §10`). Máy chủ **không gửi** mã chưa tới hạn chứ không chỉ ẩn trên
      giao diện; đơn bị huỷ mất luôn cả hướng dẫn
- [x] Giờ nhận/trả là **một nguồn duy nhất**: trang chi tiết, trang chuyến đi và nội quy
      nhà đều đọc từ `CheckInGuide.WindowLabel`

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

**Soát lại toàn bộ ngày 07/08/2026.** Trước đó mục này chỉ liệt kê những mã "đáng
ngờ", nên hai lần liên tiếp bỏ sót việc thật (`TK-12`, `TK-13`, `ĐP-03`). Lần này
đã dò **cả 201 mã** của `docs/01` ở mức mã nguồn.

Kết quả: **162 xong · 0 làm một phần · 39 chưa có.** Con số 105 mã "không thấy
nhắc tên trong code" ở lần soát trước phần lớn chỉ là **thiếu mã tham chiếu**, không
phải thiếu tính năng — hai phần ba trong số đó đã chạy được.

**Sửa lại ngày 09/08/2026.** Bảng §9.2 vẫn kê tám mã mà §9.0 ngay phía trên nói là
đã làm xong trong cùng ngày 07/08 — `TĐ-13`, `TĐ-14`, `CĐ-02`, `TM-15`, `TM-20`,
`CN-08`, `CN-10`, `QL-13`. Đã kiểm lại từng mã ở mức mã nguồn (`Landmarks.cs` gọi
từ `CatalogService.cs:957`, `ListingCopy.cs` từ `HostController.cs:360`, công tắc
thuế ở `Header.jsx:551`, đếm ngược ở `Trip.jsx:157`) và bỏ khỏi bảng. Trước lần sửa
này con số "13 làm một phần" đếm thừa đúng tám mã.

**Soát riêng `docs/08` ngày 08/08/2026** (ba lượt đọc code độc lập). Bài học đắt
nhất của lượt này: **có `.cs` không có nghĩa là có chạy.** `SuspensionImpact.cs`
tính đúng toàn bộ bảng §6 nhưng chưa từng được thực thi — khoá tài khoản không huỷ
đơn nào; `Appeals.cs` đủ luật nhưng người dùng không có đường nộp; `ActorTag`,
`BanBlocks`, `IdleTimeout`, `AnonymousReviewerName` đều chỉ được gọi từ test. Kịch
bản §13 vẫn xanh vì nó kiểm tra **màn hình xem trước**, không kiểm tra hậu quả thật.
Đã sửa hết trong ngày, và kịch bản 3 giờ bấm khoá thật rồi đọc lại cơ sở dữ liệu.
Khi thêm việc mới, viết nghiệm thu theo **kết quả**, đừng theo màn hình.

### 9.0 P0 — còn **một** mã, và nó chờ khách quyết chứ không chờ code

Tám trong chín mã P0 của lần soát 07/08/2026 đã làm xong trong cùng ngày:

| Mã | Việc | Làm gì |
|---|---|---|
| `TĐ-13` | Khoảng cách tới các điểm chính | `Landmarks.cs` — danh sách địa danh theo thành phố, đo từ toạ độ tin đăng |
| `TĐ-14` | Ngôn ngữ chủ nhà + co-host | đọc từ hồ sơ chủ nhà (`TK-04`) và `QL-19`, không tạo bản sao |
| `CĐ-02` | Đếm ngược tới ngày nhận phòng | tính từ **giờ nhận phòng của tin đăng**, không phải nửa đêm |
| `TM-15` | Nhóm "Tuỳ chọn đặt" trong bộ lọc | gom đặt ngay · tự nhận phòng · thú cưng · huỷ miễn phí |
| `TM-20` | Công tắc "giá đã gồm thuế và phí" | hiện **giá mỗi đêm đã gồm tất cả**, kèm tổng kỳ nghỉ |
| `CN-08` | Gợi ý tiêu đề & mô tả | `ListingCopy.cs` — dựng từ chính dữ liệu host đã nhập |
| `CN-10` | Giá thị trường khu vực | phân vị 25/50/75 của chỗ tương đương cùng thành phố |
| `QL-13` | Cảnh báo hậu quả trước khi huỷ | tiền hoàn + hồ sơ StayShield + tỉ lệ tự huỷ sau khi huỷ |
| `TĐ-03` | Dịch mô tả tin đăng | **chưa làm** — cần nhà cung cấp dịch thuật, xem `§9.1` |

`TĐ-03` là mã P0 duy nhất còn lại và nó chờ một quyết định của khách chứ không
chờ code: chọn nhà cung cấp dịch (Google Translate / DeepL / Azure) và trả tiền
khoá API. Theo tiền lệ đăng nhập mạng xã hội ở `CLAUDE.md §5`, nút nào chưa có
mã thì không hiện — thà thiếu nút còn hơn nút bấm vào không chạy.

### 9.1 Chưa có (39 mã)

| Mã | Việc | Ưu tiên |
|---|---|---|
| `CĐ-06` | Yêu cầu đổi ngày / số khách sau khi đặt, chủ nhà duyệt, tự tính chênh lệch | P1 |
| `CĐ-12` | Nút xin trợ giúp gắn với đúng đơn đang gặp vấn đề | P1 |
| `QL-11` | Cảnh báo khi lịch iCal nhập về trùng đơn đã xác nhận | P1 |
| `QL-16` | Báo cáo hiệu suất tin đăng: lượt xem, lượt lưu, tỉ lệ đặt, tỉ lệ lấp đầy | P1 |
| `TM-18` | Lọc theo ngôn ngữ chủ nhà | P2 |
| `TM-23` | Lưu bộ tìm kiếm + thông báo khi có chỗ mới phù hợp | P2 |
| `TM-24` | Vẽ vùng tìm kiếm trên bản đồ | P2 |
| `TM-26` | Trang giới thiệu theo thành phố / loại hình (cho tìm kiếm ngoài sàn) | P1 |
| `TĐ-03` · `TN-06` | Dịch mô tả tin đăng (**P0**) · dịch tin nhắn (P1) — cần nhà cung cấp dịch thuật | P0/P1 |
| `TC-03` | Đơn từ 28 đêm trở lên thu theo từng tháng | P1 |
| `TC-11` | Xử lý tranh chấp thẻ và giao dịch nghi ngờ gian lận | P1 |
| `TK-07` · `TK-13` | Xác minh email công ty · liên hệ khẩn cấp | P2 |
| `ĐG-11` | Phát hiện đánh giá gian lận qua tài khoản phụ | P2 |
| `AT-01` | Kiểm duyệt tin đăng mới **trước** khi hiển thị | P1 |
| `AT-09` | Chuyển tiếp lên nhân viên hỗ trợ | P1 |
| `AT-03` · `AT-08` · `AT-10` · `AT-12` | Kênh hàng xóm · trợ lý tự động · danh sách chặn · chống phân biệt đối xử | P2 |
| `QT-07` · `QT-08` | Quản lý bài trợ giúp · bật tính năng theo tỉ lệ | P2 |
| `YT-05` | Chia sẻ và cùng sửa danh sách yêu thích | P1 |
| `YT-06` · `YT-07` · `YT-08` | Bình chọn nhóm · so sánh 2–5 chỗ · báo khi chỗ đã lưu giảm giá | P2 |
| `QL-09` · `QL-18` · `CN-14` · `CN-15` | Gợi ý giá thị trường · gợi ý cải thiện · ước lượng thu nhập · nhân bản tin | P2 |
| `CĐ-10` · `CĐ-11` · `XH-01`→`XH-03` | Gộp chuyến & lịch trình · mời bạn cùng đi · kết bạn, bản đồ hành trình | P2 |

### 9.2 Làm một phần — **không còn mã nào** (dọn xong 10/08/2026)

Năm mã của lần soát trước đã làm nốt: `AT-02`, `TC-07`, `TC-04`, `TĐ-18`, `TM-02`.
`TC-07` xong phần máy móc, còn thời hạn thì chờ khách chốt ở `docs/07 §16`.


### 9.3 Ghi chú về cách soát

Kết luận dựa trên đọc mã nguồn, không dựa trên việc mã yêu cầu có xuất hiện trong
comment hay không. Ví dụ `CĐ-05`, `CĐ-07`, `ĐG-01`, `YT-02`, `TĐ-02`, `TM-25`,
`TC-02`, `TN-01` đều **chạy được** dù không chỗ nào trong code viết tên mã ra.

Một dạng thiếu không hiện ra khi tìm theo tên mã: **dữ liệu được ghi mà không ai
đọc.** `QL-16` là ví dụ — bảng `ListingViews` nhận lượt xem thật từ
`CatalogService.cs:451`, nhưng không controller nào truy vấn nó, nên chủ nhà không
có màn hình nào thấy được. Giống hệt `SuspensionImpact` của lượt soát `docs/08`:
mã chạy, dữ liệu đúng, không có đường tới người dùng. Khi soát, hỏi thêm câu "ai
đọc cái này?" chứ đừng dừng ở "có ghi chưa?".

### 9.4 Thứ tự làm phần còn lại

Sắp theo giá trị thu về trên công bỏ ra, không theo thứ tự mã. Mỗi đợt xong thì
cập nhật §9.1/§9.2 ngay tại đây, đừng để đếm lệch lần thứ ba.

**Đợt 1 — nốt 5 mã dở.** Rẻ nhất vì phần lõi đã có: `AT-02` (mở báo cáo cho người
dùng / tin nhắn / đánh giá) · `TC-07` (hạn dùng số dư) · `TC-04` (xuất báo cáo
thuế) · `TĐ-18` (nút chia sẻ) · `TM-02` (tab "Tất cả").

**Đợt 2 — tiền và chuyển đổi.** `ĐP-09`+`TC-09` mã giảm giá · `ĐP-14` hoá đơn tải
về · `ĐP-17`+`QL-14` ưu đãi riêng trong tin nhắn · `TC-03` thu theo tháng cho đơn
≥28 đêm · `TC-11` tranh chấp thẻ.

**Đợt 3 — giữ chân sau khi đặt.** `CĐ-06` đổi ngày/số khách có chủ nhà duyệt ·
`CĐ-12` nút trợ giúp gắn đúng đơn · `ĐG-10` báo cáo đánh giá vi phạm · `ĐG-12` ghi
chú công khai khi chủ nhà huỷ.

**Đợt 4 — công cụ chủ nhà.** `QL-16` báo cáo hiệu suất (dữ liệu đã có sẵn, chỉ
thiếu đường đọc) · `QL-11` cảnh báo iCal trùng đơn đã xác nhận · `ĐP-03` điều kiện
Đặt ngay · `ĐP-10` yêu cầu bắt buộc trước khi đặt.

**Đợt 5 — khám phá và an toàn.** `TM-17` lọc khả năng tiếp cận · `TM-26` trang
thành phố cho tìm kiếm ngoài sàn · `AT-01` kiểm duyệt tin trước khi hiện · `AT-09`
chuyển tiếp nhân viên hỗ trợ · `TN-05` lọc hộp thư.

**Chờ khách quyết, không chờ code:** `TĐ-03` (P0) và `TN-06` cần khoá API dịch
thuật. `TC-07` đã dựng xong máy móc hạn dùng số dư nhưng **thời hạn bao lâu thì
chưa ai chọn** — bảng tham số `docs/07 §16`. Để trống thì không gì hết hạn, đúng
hành vi cũ; chốt số là bật được mà không sửa mã. **Đợt cuối:** nhóm P2 còn lại — mạng xã hội `XH-01`→`XH-03`, so sánh và
bình chọn danh sách yêu thích, gợi ý giá cho chủ nhà, trợ lý tự động.

---

## Kiểm chứng

```bash
# Test nghiệp vụ (654 test)
dotnet test tests/StayHost.Domain.Tests

# 10 tình huống nghiệm thu, cần server chạy ở cổng 5199
python scripts/acceptance.py
```

## Ghi chú về quy mô

Tài liệu có ~200 yêu cầu (78 P0, 71 P1, 51 P2) trên 13 module. Toàn bộ **quy tắc
tiền, vòng đời đơn, sổ sách và tranh chấp** đã đúng spec và có test. Phần còn
thiếu là công cụ (đăng tin theo bước, lịch nhiều tin, iCal, co-host) và nhóm mở
rộng — không có phần nào chạm vào tiền.
