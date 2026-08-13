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

Kết quả (cập nhật 10/08/2026): **201 xong · 0 làm một phần · 0 chưa có — HẾT.** Con số 105 mã "không thấy
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

### 9.0 P0 — **đã đủ**; mã cuối chỉ chờ khách cắm khoá API

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
| `TĐ-03` | Dịch mô tả tin đăng | **chạy thật 11/08/2026** bằng máy dịch tự host, xem dưới |

`TĐ-03` và `TN-06` **đã bật, không tốn khoá API**. Lần soát trước ghi là "chờ khách
chọn nhà cung cấp và trả tiền", nhưng cả hai compose vốn đã kéo sẵn một container
`libretranslate` và trỏ app vào đó — nghĩa là việc chờ ấy không tồn tại. Ngày
11/08/2026 bật lên và kiểm chứng thật; mấy chỗ ghi "chờ khách" là tài liệu nói sai
chứ không phải tính năng thiếu.

Ba việc phải sửa lúc bật:

1. **`Translations.Targets` chỉ có 6 thứ tiếng** trong khi giao diện cho chọn 8. Người
   đổi sang tiếng Đức thì `/api/translate` từ chối, và trang **im lặng trả về bản gốc** —
   hỏng mà không ai thấy. Đã bổ sung `de`, `es`.
2. **`LT_LOAD_ONLY` chỉ chạy lần đầu của volume.** Thêm ngôn ngữ rồi restart thì container
   vẫn healthy mà engine trả `"de is not supported"`. Đã bật `LT_UPDATE_MODELS` ở cả hai
   compose để danh sách ngôn ngữ là thứ quyết định thật.
3. **Nút "Dịch" hard-code chữ tiếng Việt** và giữ bản sao riêng của nhãn ngôn ngữ (thiếu
   `de`/`es` nên hiện ra mã thô). Giờ chữ đi qua `t()`, nhãn lấy từ chính danh sách server trả.

Chưa cấu hình gì thì nút "Dịch" **không hiện** — theo đúng tiền lệ đăng nhập mạng xã hội
ở `CLAUDE.md §5`, thà thiếu nút còn hơn nút bấm vào không chạy. Muốn chất lượng cao hơn
thì đổi sang `google` + khoá API, không phải sửa mã.

**`TC-07` — hạn dùng số dư — chốt 11/08/2026.** `docs/07 §16` giờ có cột "Giá trị chốt":
bù đắp / giới thiệu bạn / hoàn khi huỷ **12 tháng**, thẻ quà tặng **không hết hạn** (khách
đã trả tiền thật cho nó). Số nằm ở `appsettings.json`, không biên dịch vào mã. Bật lên mới
lộ ra một lỗi thật: đường **hoàn số dư khi huỷ đơn** trong `BookingsController` tự dựng
`CreditEntry` nên không đóng dấu hạn dùng — số dư hoàn lại lẽ ra 12 tháng thì **không bao
giờ hết hạn**. Đã gom về `CreditLedger.Grant`, một cửa duy nhất, kèm hai test. Hạn đóng dấu
**lúc cấp**, nên đổi tham số về sau không với ngược lại số dư khách đang giữ.

### 9.1 Chưa có — không còn mã nào (hoàn tất 10/08/2026)

Cả 201 mã của `docs/01` đã làm xong. Mã P0 cuối (`TĐ-03`) là cơ chế dịch, bật khi
khách cắm khoá API. Chi tiết từng mã đợt cuối ở §9.3.



### 9.2 Làm một phần — **không còn mã nào** (dọn xong 10/08/2026)

Năm mã của lần soát trước đã làm nốt: `AT-02`, `TC-07`, `TC-04`, `TĐ-18`, `TM-02`.
`TC-07` xong phần máy móc, còn thời hạn thì chờ khách chốt ở `docs/07 §16`.


### 9.3 Ghi chú về cách soát

Kết luận dựa trên đọc mã nguồn, không dựa trên việc mã yêu cầu có xuất hiện trong
comment hay không. Ví dụ `CĐ-05`, `CĐ-07`, `ĐG-01`, `YT-02`, `TĐ-02`, `TM-25`,
`TC-02`, `TN-01` đều **chạy được** dù không chỗ nào trong code viết tên mã ra.

`TC-11` (tranh chấp thẻ + theo dõi gian lận) đã dựng đủ từ trước — domain `Chargebacks`,
các thao tác mở/nộp bằng chứng/phân xử ở `FinanceController`, kế toán thất thoát, `RiskWatch`,
và panel admin `ChargebackPanel` — nhưng nằm trong danh sách "chưa có". Xác minh sống bằng
endpoint (10/08/2026) rồi đánh dấu xong.

`CĐ-10` (gộp chuyến + lịch trình theo ngày) + `CĐ-11` (mời bạn cùng lên lịch) làm xong
10/08/2026 — **hai mã cuối của toàn bộ 201 mã**. `TripPlans.cs` (thuần, có test): quyền
`CanEdit` (chủ + bạn được mời), `IsOwner` (quản lý thành viên/đơn), validate mục. Entity
`TripPlan` + `TripPlanBooking` (gộp đơn) + `TripPlanMember` (bạn cùng đi) + `TripItineraryItem`
(mục theo ngày). `TripPlansController`: tạo/xoá chuyến, thêm/bỏ đơn của mình, mời **bạn bè**
đồng chỉnh, chủ+bạn cùng thêm/xoá mục lịch trình; người ngoài bị chặn xem/sửa. UI trang
`/trip-plans`. Xác minh sống: tạo→gộp đơn→chủ thêm mục→mời bạn→bạn thêm mục→người lạ 403 cả
xem lẫn sửa; chi tiết đúng 1 đơn, 2 thành viên, 2 mục qua 2 ngày.

`XH-03` (nhắn bạn hỏi về nơi họ từng ở) làm xong 10/08/2026. Entity `FriendMessage` (DM ngang
hàng, gắn `ListingId` nơi được hỏi), endpoint `GET/POST /api/friends/{id}/messages` — chỉ giữa
bạn bè đã chấp nhận, chặn nếu có block (AT-10), tự đánh dấu đã đọc. UI: bấm chip địa điểm trong
hành trình của bạn để "hỏi về nơi này", và khung chat trên hồ sơ bạn bè. Xác minh sống: non-friend
403, gửi kèm địa điểm ("Marble Mountain Villa"), hội thoại hai chiều, chặn tin rỗng 400.

`XH-01` (kết bạn) + `XH-02` (bản đồ hành trình + riêng tư) làm xong 10/08/2026. `Friendships.cs`
(thuần, có test): quy tắc kết bạn (không tự kết, chỉ người nhận duyệt), và `CanSeeJourney` theo
quyền riêng tư (riêng tư/bạn bè/công khai). Entity `Friendship` (một hàng mỗi cặp), `User.JourneyVisibility`.
`FriendsController`: gửi lời mời (gửi ngược = tự chấp nhận), duyệt/từ chối, huỷ kết bạn, đặt quyền
riêng tư, và `journey` (nơi đã đến/sắp đi từ đơn đặt) có kiểm quyền xem. UI: trang `/friends`, nút
"Kết bạn"/hành trình ở hồ sơ công khai, link ở menu tài khoản. Xác minh sống: A mời→B duyệt→A thấy
B; hành trình bạn bè xem được, Private→403, Public→người lạ xem được, tự kết 400, huỷ kết bạn; hành
trình guest có 7 đã đến/12 sắp đi.

`YT-06` (bình chọn thích/không thích trong nhóm) làm xong 10/08/2026. Entity `WishlistVote`
(một phiếu mỗi voter mỗi chỗ, voter = session hoặc `u{id}`), endpoint `POST
/api/shared-wishlists/{token}/vote` (bấm cùng chiều để bỏ, chiều khác để lật); Shared GET trả
kèm số 👍/👎 và phiếu của người xem. UI nút 👍/👎 trên từng thẻ ở trang danh sách chia sẻ.
Xác minh sống: 2 session bầu đếm riêng (1→2), lật phiếu, bấm lại để bỏ, tallies bền, chặn tin
ngoài danh sách.

`TM-24` (vẽ vùng tìm kiếm trên bản đồ) làm xong 10/08/2026. `GeoPolygon.cs` (thuần, có test)
làm point-in-polygon (ray-casting, chịu cả đa giác lõm) + bounding box + parse. Search nhận
param `polygon`; `CatalogService.ResolveAreaAsync` lọc thô bằng bbox trong SQL rồi soi chính
xác trong bộ nhớ ra tập id, `BaseQuery` lọc theo tập đó nên đếm/phân trang khớp. UI: nút "Vẽ
vùng" trên bản đồ split → chạm thả đỉnh → "Xong" chạy tìm; store giữ `searchPolygon`. Xác minh
sống: polygon quanh Đà Nẵng ra 39 (đúng cả 2 Hội An sát ranh), vùng biển trống ra 0, nút hiện.

`ĐG-11` (phát hiện đánh giá gian lận qua tài khoản phụ) làm xong 10/08/2026. Đánh giá luôn
gắn đơn thật, nên gian lận là chủ nhà tự đặt chỗ mình qua tài khoản phụ rồi tự cho 5 sao —
`ReviewFraud.cs` (thuần, có test) chấm các tín hiệu: người đánh giá chính là chủ nhà, tạo
cùng session với chủ nhà, tài khoản mới chỉ từng ở đúng chủ nhà này rồi cho điểm cao. Admin
`GET /api/admin/review-fraud` gom tín hiệu (join review→đơn→khách→chủ) và trả các review bị
gắn cờ (mức + lý do), panel hiển thị. Xác minh sống: chèn self-review → "Nguy cơ cao: Người
đánh giá chính là chủ nhà", quyền chặn 403.

`AT-08` (trợ lý hỗ trợ tự động) làm xong 10/08/2026. `SupportAssistant.cs` (thuần, có test)
là luật trên sự kiện: nhận trạng thái đơn hiện tại (sắp nhận phòng, còn số dư, yêu cầu chờ
duyệt, chưa đánh giá, hồ sơ đang mở, chủ nhà có đơn chờ) và trả các hành động áp dụng, khẩn
trước, luôn có lối "trợ giúp" + "người thật" (nối AT-09) nên không bao giờ là ngõ cụt.
`GET /api/support/assistant` dựng ngữ cảnh từ dữ liệu người dùng; khối trợ lý ở đầu Trung tâm
trợ giúp. Xác minh sống: khách vãng lai → gợi đăng nhập/trợ giúp; guest có yêu cầu chờ → "Xem
yêu cầu → /trips" đứng trước fallback.

`AT-12` (giám sát từ chối khách) và `AT-03` (kênh hàng xóm) làm xong 10/08/2026. AT-12:
`AntiDiscrimination.cs` (thuần, có test) dò lý do từ chối theo biên từ (không auto-chặn), admin
monitor `GET /api/admin/decline-monitor` gom tỉ lệ từ chối + gắn cờ lý do nghi phân biệt. AT-03:
`NeighborReport` + `NeighborReports.cs`, form công khai `/neighbors` (không cần tài khoản, nối
từ footer "Báo cáo lo ngại khu dân cư"), có chống trùng theo session, admin xử lý ở panel. Xác
minh sống: AT-12 gắn cờ đúng "Gia đình/trẻ em"; AT-03 gửi ẩn danh→admin thấy→xử lý, quyền chặn 403.

`YT-07` (so sánh 2–5 chỗ) làm xong 10/08/2026. `CatalogService.CompareAsync` trả thẻ cho
tối đa 5 tin (giữ đúng thứ tự chọn, chỉ tin công khai), endpoint `GET /api/listings/compare?ids=`.
UI: nút "So sánh" trong danh sách yêu thích mở bảng cạnh nhau (giá/đánh giá/loại/số phòng/đặt
ngay/siêu chủ nhà…), cuộn ngang trong `.table-wrap` cho hợp mobile. Xác minh sống: ids=1,2,3 ra
3 thẻ đủ thuộc tính, giữ thứ tự (3,1→[3,1]), id không hợp lệ bị lọc.

`TĐ-03` · `TN-06` (dịch mô tả tin đăng · dịch tin nhắn) — **cơ chế xong 10/08/2026**, tắt
mặc định theo tiền lệ TC-07/đăng nhập MXH. `Translation.cs` (thuần, có test): settings tắt
khi chưa có `Provider`, tập ngôn ngữ đích, khoá cache SHA-256 theo (nguồn, đích), stub tất
định để test. `TranslationService` dịch qua `ITranslator` (`StubTranslator` cho dev/test,
`GoogleTranslator` cho thật — khoá đọc từ `Translation__ApiKey`, không vào appsettings) và
**cache DB** để mỗi (văn bản, ngôn ngữ) chỉ gọi API trả phí một lần. `GET /api/translate/config`
cho FE biết có bật không → nút "Dịch" chỉ hiện khi bật (trang chi tiết TĐ-03, bong bóng tin
nhắn TN-06). Xác minh sống: `Provider=stub` → config bật, dịch ra "[en] …", gọi lần hai trúng
cache (1 row); mặc định (không provider) → config tắt, endpoint trả 400, nút ẩn.

`YT-08` (báo khi chỗ đã lưu giảm giá) làm xong 10/08/2026. Khi host lưu tin với giá thấp
hơn giá cũ (bắt ngay trong `HostController.Update`, so `oldPrice` trước `ApplyAsync`) và tin
đang hiển thị công khai, gửi thông báo `PriceDrop` (đã có sẵn, topic Marketing → tắt được)
cho từng user **đã đăng nhập** có lưu tin (bỏ chính chủ nhà). Không entity/migration mới.
Xác minh sống: lưu tin→hạ 3,2tr→2,7tr→1 thông báo; nâng giá lại→không báo thêm.

`TM-23` (lưu bộ tìm kiếm + báo khi có chỗ mới) làm xong 10/08/2026. Entity `SavedSearch`
lưu các bộ lọc thành cột (bỏ ngày — tin mới có lịch mở), `LastNotifiedListingId` là mốc
nước cao nên chỉ tin tạo sau đó mới báo, không báo trùng. `CatalogService.MatchNewAsync`
tái dùng `BaseQuery` lọc `Id > mốc`; `SavedSearchSweeper` (trong vòng quét 60s) gom tin mới
khớp thành **một** thông báo (kind `SavedSearchMatch`, topic Marketing → tắt được). Endpoint
`account/saved-searches` (GET/POST/DELETE), UI nút "Lưu tìm kiếm" ở bộ lọc + danh sách ở tab
Thông báo. Xác minh sống: lưu (mốc=88) → host đăng tin Quy Nhơn (id 89) → sweep ~32s tạo
thông báo, mốc 88→89 → xoá được.

`QT-07` (quản lý bài trợ giúp) làm xong 10/08/2026. Bài trợ giúp vốn đã là entity DB
(`HelpArticle`, HelpSeeder), nên chỉ thêm CRUD admin (scope Hỗ trợ, có nhật ký):
`GET/POST/DELETE admin/help-articles`, tự sinh slug từ tiêu đề, chặn trùng slug, validate
tiêu đề/nội dung tối thiểu, `RefreshSearchText` để tìm không dấu; UI editor ở panel admin.
"Nội dung trang giới thiệu" tác giả bằng chính hệ bài trợ giúp này (một bài Chung). Xác
minh sống: tạo→hiện công khai→sửa→xoá(404)→trùng slug 400→nội dung ngắn 400→khách 403.

`QT-08` (bật tính năng theo tỉ lệ người dùng) làm xong 10/08/2026. `FeatureRollout.cs`
(thuần, có test) chia người dùng vào 100 nhóm bằng FNV-1a trên `khoá-tính-năng:khoá-người`
— tất định, ổn định qua tiến trình/khởi động lại (không dùng `GetHashCode` ngẫu nhiên theo
run), trộn khoá tính năng nên một người không nằm cùng lát cắt cho mọi tính năng. Entity
`FeatureFlag` (công tắc tổng + %). Admin (scope Super) quản lý ở panel; `GET /api/features`
trả map bật/tắt theo user (bucket theo id) hoặc theo session cho khách vãng lai. Xác minh
sống: bật 100%→thấy, 0%→ẩn, tạo mã mới, clamp 150→100, khách vãng lai cũng nhận map.

`AT-10` (danh sách chặn) làm xong 10/08/2026. Entity `UserBlock` (cặp blocker/blocked,
unique), `Blocks.cs` cho thông điệp; khi gửi tin (`MessagesController.Send`) chặn cả hai
chiều nếu tồn tại block giữa hai bên — áp cho cả thread cũ lẫn thread mới vì kiểm tra đặt
sau khi resolve thread. Endpoint `account/blocks` (GET/POST/DELETE), UI nút chặn/bỏ chặn ở
hồ sơ công khai. Xác minh sống: trước chặn gửi được, sau chặn cả hai chiều 403, tự chặn
400, bỏ chặn xong gửi lại được; 20/20 nghiệm thu xanh, sổ cân = 0.

`TM-18` (lọc theo ngôn ngữ chủ nhà) làm xong 10/08/2026. Thêm `HostLanguages` vào
`SearchQuery`; `CatalogService.BaseQuery` khớp tin khi chủ nhà nói **ít nhất một** mã đã
chọn, đọc từ `Host.User.SpokenLanguages` (nguồn TĐ-14) — dựng bằng `UNION` để mỗi
`Contains` là một vị từ SQL, không phải Any-lambda không dịch được; cũng thêm vào danh
sách "bỏ bớt bộ lọc" của TM-22. UI là hàng chip trong bộ lọc, lấy mã từ `profile-options`.
Xác minh sống: lọc `ko` ra đúng tin của host nói Hàn, `vi` ra tất cả, `ja` ra 0, và OR
nhiều mã (`ko,ja`) đúng; 10/10 nghiệm thu vẫn xanh.

`TK-07` · `TK-13` làm xong 10/08/2026. TK-07 (xác minh email công ty): thêm
`IdentifierKind.WorkEmail`, tái dùng cơ chế OTP `OneTimeCode`; `WorkEmail.cs` (thuần,
có test) chặn email cá nhân (gmail/yahoo/outlook…) vì huy hiệu là "thuộc về tổ chức";
endpoint `account/work-email` (đặt+gửi mã) · `work-email/confirm` · DELETE. TK-13 (liên
hệ khẩn cấp): ba trường trên `User`, sửa qua trang hồ sơ, riêng tư. Xác minh sống: gmail
bị từ chối 400, email công ty nhận mã→xác nhận→`WorkEmailConfirmed=true`, mã sai bị chặn,
liên hệ khẩn cấp lưu đúng vào DB; 20/20 nghiệm thu vẫn xanh.

`QL-09` · `QL-18` · `CN-14` · `CN-15` (cụm công cụ chủ nhà) làm xong 10/08/2026, gói
trong `HostAdvice.cs` (logic thuần, có test): CN-14 ước lượng thu nhập ròng theo ba
mức lấp đầy (net phí chủ nhà 3%), QL-09 gợi mức giá giữa khu vực dựa trên phân vị của
`CN-10` — **chỉ gợi ý, host tự bấm áp dụng, sàn không tự đổi**, QL-18 checklist cải
thiện kèm ước lượng tác động (đọc từ chính dữ liệu tin: số ảnh, đặt ngay, mô tả, tiện
nghi, giá so mặt bằng…). CN-15 nhân bản tin thành **bản nháp** mới (không kéo theo đơn,
đánh giá, lịch, iCal token), có guard quyền sở hữu và chặn host bị cấm đăng tin mới.
Endpoint ở `HostController` (`income-estimate`, `listings/{id}/advice`,
`listings/{id}/duplicate`), UI ở wizard giá và thẻ tin chủ nhà. Xác minh sống: thu nhập
tăng dần theo lấp đầy, guard host khác = 403, bản sao nháp, sổ cân = 0.

`AT-01` (kiểm duyệt tin đăng mới trước khi hiển thị) làm xong 10/08/2026. Cổng tắt
mặc định (`Moderation:NewListingsRequireApproval`, theo tiền lệ `TC-07`/đăng nhập
mạng xã hội): không bật thì host đăng là hiển thị ngay, đúng hành vi cũ, nên 690 test
và cả 20 kịch bản nghiệm thu vẫn xanh. Bật lên thì tin mới vào trạng thái
`ReviewStatus=Pending` (`ListingModeration.cs`), bị loại khỏi tìm kiếm/rails thành
phố/hồ sơ công khai và **không đặt được** (`Availability.Check`) cho tới khi admin
duyệt; hàng đợi + duyệt/từ chối kèm lý do ở `AdminController` (dùng chung gate
`TakeDownContent` và nhật ký §1.4), host thấy trạng thái + lý do và sửa để gửi lại.
Xác minh sống với cổng **bật**: đăng tin → Pending, không thấy trong search, admin
duyệt → hiện; từ chối có lý do → host sửa gửi lại → Pending. Sổ vẫn cân = 0.

`TC-03` (đơn ≥28 đêm trả theo tháng, `docs/07 §12.3`) làm xong 10/08/2026. Lịch chia
theo tháng ở `Payouts.MonthlySchedule` (khối 30 đêm, tháng đầu gánh phần lớn, tháng
cuối lấy phần dư — tổng khớp đến từng đồng); `PaymentCompletion` dựng `PayoutInstallment`
lúc xác nhận và tắt payout một lần (nulls `PayoutDueOn`); `PayoutService.InstallmentSweepAsync`
(chạy trong vòng quét 60s của `BookingService`) trả từng đợt đến hạn, dùng chung năm điều
kiện giữ tiền `§12.4` và nhịp thử lại `§12.5`, đợt cuối trả xong thì đánh dấu `Payment`
là đã trả. Xác minh sống: đơn 30 đêm → 1 đợt = đúng `HostPayout`, sweep chi ra, sổ cân
bằng = 0, `PayoutStatus=Paid`.

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
về · `ĐP-17`+`QL-14` ưu đãi riêng trong tin nhắn. (`TC-03` thu theo tháng cho đơn
≥28 đêm và `TC-11` tranh chấp thẻ đã xong — xem `§9.3`.)

**Đợt 3 — giữ chân sau khi đặt.** `CĐ-06` đổi ngày/số khách có chủ nhà duyệt ·
`CĐ-12` nút trợ giúp gắn đúng đơn · `ĐG-10` báo cáo đánh giá vi phạm · `ĐG-12` ghi
chú công khai khi chủ nhà huỷ.

**Đợt 4 — công cụ chủ nhà.** `QL-16` báo cáo hiệu suất (dữ liệu đã có sẵn, chỉ
thiếu đường đọc) · `QL-11` cảnh báo iCal trùng đơn đã xác nhận · `ĐP-03` điều kiện
Đặt ngay · `ĐP-10` yêu cầu bắt buộc trước khi đặt.

**Đợt 5 — khám phá và an toàn.** `TM-17` lọc khả năng tiếp cận · `TM-26` trang
thành phố cho tìm kiếm ngoài sàn · `AT-01` kiểm duyệt tin trước khi hiện · `AT-09`
chuyển tiếp nhân viên hỗ trợ · `TN-05` lọc hộp thư.

**Không còn mã nào chờ khách quyết (11/08/2026).** `TĐ-03`/`TN-06` đã bật bằng máy
dịch tự host và `TC-07` đã chốt tham số ở `docs/07 §16` — chi tiết cả hai ở `§9.0`.
Các mục còn để trống trong `docs/07 §16` (`TT-A`, `TT-B`, `TT-C`, phương án pháp lý,
cổng thanh toán) là quyết định vận hành, chưa chặn tính năng nào.

### 9.3. Việc ngoài 201 mã: VietQR (13/08/2026)

`docs/07 §2.3` xếp VietQR vào nhóm P2 "có thể thêm sau", nên nó **không nằm trong
201 mã của `docs/01`** — con số đó giữ nguyên. Đã làm đủ vòng: sinh mã, đơn chờ
chuyển khoản cho cả ba dòng, nhập sao kê, khớp về đơn, hết hạn thì trả lại chỗ.
Chi tiết ở `docs/07 §15.2`, mã `TC-P-13`.

**Chưa bật ở bản chạy thật.** Phương thức chỉ xuất hiện khi có
`BankTransfer:AccountNumber`, và `docker-compose.prod.yml` chưa truyền biến đó
xuống container. Trước khi bật cần một tài khoản **của pháp nhân**, không phải
tài khoản cá nhân: `docs/07 §1` nói tiền khách phải do sàn giữ hộ, và §13 vẫn
chưa chốt phương án pháp lý A/B/C.

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
