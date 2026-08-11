# CLAUDE.md — StayHost OS

Đọc file này trước khi làm bất cứ việc gì trong repo.

---

## 1. Tài liệu là nguồn sự thật

`docs/00` → `docs/05` là **đặc tả nghiệp vụ do khách hàng đưa**. Khi code khác tài liệu
thì **code sai**, không phải tài liệu sai.

| File | Nội dung |
|---|---|
| `docs/00-TONG-QUAN.md` | Mô hình kinh doanh, các bên tham gia, 8 nguyên tắc xuyên suốt |
| `docs/01-DANH-MUC-CHUC-NANG.md` | ~200 yêu cầu có mã `FR` + ưu tiên P0/P1/P2 |
| `docs/02-MAN-HINH.md` | Từng màn hình có gì, làm được gì |
| `docs/03-QUY-TAC-NGHIEP-VU.md` | **Quan trọng nhất** — giá, huỷ/hoàn tiền, vòng đời đơn, xếp hạng |
| `docs/04-QUY-TRINH.md` | 14 quy trình đầu-cuối + 10 tình huống nghiệm thu |
| `docs/05-THUC-THE.md` | Sàn cần biết thông tin gì về từng đối tượng |
| `docs/06-STAYSHIELD.md` | Chương trình bảo vệ hai đầu. **§10 là bảng tham số đã chốt** |
| `docs/07-THANH-TOAN.md` | Thanh toán đầu-cuối. §15 là danh sách chức năng, §18 là 11 kịch bản |
| `docs/08-QUAN-TRI-NGUOI-DUNG.md` | Quyền admin, thang xử lý tài khoản, khiếu nại, chống lạm quyền |
| `docs/09-TRAI-NGHIEM-DICH-VU.md` | Trải nghiệm & Dịch vụ — **quy tắc khác hẳn chỗ ở**. §7 là bảng tham số đã chốt |
| `docs/PLAN.md` | **Đối chiếu hiện trạng ↔ spec.** Bắt đầu phiên mới thì đọc file này trước |

---

## 2. Ba việc khách đã quyết (06/08/2026)

1. **Giữ tên StayHost OS**, giữ danh hiệu "Siêu chủ nhà" / "Khách yêu thích".
   Không đổi sang StayHub.
2. **Phí dịch vụ 14% khách / 3% chủ nhà** theo `docs/03 §1`, để trong cấu hình
   `Pricing:` chứ không rải hằng số khắp nơi.
3. **14 tham số StayShield** của `docs/06 §10` đã chốt: bù đổi chỗ 40%, tặng số dư
   10%, trần chi phí phát sinh 3 triệu; chủ nhà 75 triệu/đơn, 350 triệu/năm, tự chịu
   500k, 5 đêm mất thu nhập, 15 triệu mỗi món giá trị cao; quỹ trích 5% phí dịch vụ,
   cảnh báo ở 80%, gắn cờ từ hồ sơ thứ 4. **Có trực 24/7**, **có làm nhánh C4**.
   Tất cả nằm trong `ShieldSettings`, một nơi duy nhất.

---

## 3. Hiện trạng

**Toàn bộ xanh (11/08/2026).** 936 test nghiệp vụ · **10/10** kịch bản của `docs/04`
(`scripts/acceptance.py`) · **10/10** kịch bản quản trị của `docs/08 §13`
(`scripts/admin_acceptance.py`) · **19/19** kịch bản của `docs/09`
(`scripts/doc09_acceptance.py`, gồm cả 12 tình huống bắt buộc của `docs/09 §9`).
Sổ sách lệch 0. Cả 201 mã của `docs/01` đã làm xong (`docs/PLAN.md §9`).

> **`acceptance.py` cần DB sạch.** Nó ra 8/10 trên DB đã chạy nhiều lần — **không phải
> lỗi code**: dữ liệu tích luỹ làm bước lùi ngày một đơn về quá khứ đụng ràng buộc GiST
> `bookings_no_overlap`. Reset DB theo §5 rồi chạy lại là 10/10 (đã xác nhận 11/08/2026).
> Trong lúc truy đã sửa **hai lỗi thật của chính script**: `bookable()` gọi dry-run rồi
> **bỏ qua kết quả** (nên hứa những tin đã bị đặt), và chỉ thử **một khung ngày**. Giờ nó
> đọc kết quả dry-run (đạt là **201**, không phải 200) và thử tám khung ngày.

### Nền

.NET 9 + EF Core 9 + PostgreSQL 17 (cổng 5544). Frontend React 19 + Vite +
React Router 7 + Leaflet trong `src/StayHost.Web/ClientApp`, build ra
`wwwroot/assets`. Bản vanilla JS cũ đã xoá.

### Đã có

| Nhóm | Nội dung |
|---|---|
| Tiền | `Pricing.cs` chạy đủ 11 bước của `03 §1`, một nơi duy nhất. Thuế theo khu vực, 6 chính sách huỷ, trần giảm 60% |
| Sổ sách | Ghi hai chiều, bất biến, `SaveChanges` chặn UPDATE/DELETE. Mọi luồng tiền đều kiểm tra lệch = 0 |
| Vòng đời đơn | 9 trạng thái + lịch sử chỉ-thêm, giữ chỗ 15 phút, yêu cầu hết hạn 24h, 9 bước kiểm tra đặt được |
| Chống đặt trùng | Ràng buộc GiST trong PostgreSQL — **chỉ áp cho tin nguyên căn** (`RoomTypeId IS NULL`); khách sạn đếm tồn kho theo loại phòng |
| Khám phá | Tìm không dấu, ngày là bộ lọc thật, ngày linh hoạt ±1–7, cuối tuần/tuần/tháng, chọn theo tháng |
| Chủ nhà | Đăng tin theo bước, lịch nhiều tin, giá mùa/theo ngày, đồng bộ iCal hai chiều, co-host có phạm vi quyền |
| Thanh toán | Trả đủ, trả một phần (cọc ≥50% + tự thu trước 14 ngày), chia hoá đơn tối đa 16 người |
| Đánh giá & tin nhắn | Đánh giá mù hai chiều, sửa trong 48h, gửi ảnh, thẻ đơn trong hội thoại, mẫu trả lời nhanh |
| An toàn | Trung tâm giải quyết, trung tâm trợ giúp 14 bài, phát hiện bất thường, nhật ký quản trị chỉ-thêm |
| Quản trị người dùng | Ma trận quyền §2, thang bậc §5 tuần tự, **khoá tài khoản thực thi đúng bảng §6** (huỷ + hoàn tiền + giữ tiền, khách đang ở không bị đụng), khiếu nại người dùng tự nộp được, ảnh giấy tờ không nằm ở thư mục công khai, phiên admin hết hạn sau 30 phút, cấm đăng ký lại sau khoá vĩnh viễn |
| Tài khoản | Đăng ký bằng SĐT hoặc email + OTP 6 số, đăng nhập Google/Apple/Facebook, chặn dưới 18 tuổi |
| Gửi email | Hàng đợi `EmailMessages` được `EmailWorker` gửi thật qua SMTP (MailKit) mỗi 15 giây. Retry 1-5-15-60-240 phút (`EmailDelivery.cs`); 5xx là từ chối vĩnh viễn, bỏ ngay. Mã OTP không bao giờ nằm trong subject. Chưa cấu hình `Email:Host` thì thư nằm chờ, không giả vờ gửi; mật khẩu đặt qua `Email__Password` |
| Hồ sơ | Ảnh đại diện, tên hiển thị, ngôn ngữ nói, nơi ở, nghề nghiệp, sở thích; trang công khai `/users/:id` có huy hiệu xác minh và đánh giá hai chiều |
| Nhận phòng | Hướng dẫn nhận phòng đầy đủ trên trang chuyến đi; địa chỉ và số điện thoại chỉ hiện sau khi đơn được xác nhận, **mã cửa chỉ hiện từ 48 giờ trước giờ nhận** |
| Bảo mật tài khoản | Xác minh danh tính có người duyệt, bảo mật 2 lớp bằng mã 6 số, ma trận thông báo loại × kênh, tải toàn bộ dữ liệu cá nhân |
| Danh hiệu | Siêu chủ nhà xét mỗi quý, Khách chọn xét hằng tuần — cấp và thu hồi tự động. Ngưỡng chỉ nằm trong `Badges.cs` |
| Xếp hạng | Điểm tổng hợp 7 yếu tố của `docs/03 §6` trong `Ranking.cs`, có trừ điểm và đa dạng hoá 12 kết quả đầu ≤ 2 chỗ mỗi chủ nhà |
| StayShield | Hai nhánh K1–K4 / C1–C4 (kể cả bên thứ ba), cửa sổ khiếu nại, thứ tự thu tiền, quỹ trích từ phí dịch vụ, khiếu nại một lần do người khác xét |
| Mở rộng | Khách sạn (nhiều loại phòng có tồn kho), thẻ quà tặng, số dư, giới thiệu bạn bè |
| Trải nghiệm (`docs/09`) | Thẩm định có người duyệt + phân loại rủi ro theo danh mục, hàng chờ kiểm duyệt, suất lặp lại và chặn chồng giờ, **giữ chỗ 10 phút**, nhiều đơn chung một suất, thuê trọn nhóm, tự huỷ khi thiếu người + gợi ý suất khác, điểm danh, huỷ theo bậc 7 ngày/50%, đánh giá 4 tiêu chí riêng |
| Dịch vụ (`docs/09`) | Chủ nhà tự đăng, chứng chỉ hành nghề có hạn **tự ẩn tin khi hết hạn**, tuỳ chọn thêm có giá riêng, phí di chuyển ngoài bán kính, lịch theo thứ + đệm + chặn hai đơn quá xa, ghi chú bắt buộc theo danh mục, xác nhận điều kiện tại chỗ, huỷ theo bậc 72 giờ |
| Tiền hai dòng mới | **Dịch vụ có mức phí riêng 0% khách / 15% NCC**; Trải nghiệm giữ 14%/3% như chỗ ở (khách chốt). Trả tiền người cung cấp **sau khi buổi kết thúc 24 giờ**, không phải từ lúc bắt đầu |
| Đa ngôn ngữ | 8 thứ tiếng (vi/en/ja/ko/zh/fr/de/es), **1967 khoá mỗi thứ, không thiếu khoá nào**. Dịch cả **chữ do server sinh** (trạng thái, dòng hoá đơn, tiện nghi, nhóm tiện nghi, loại chỗ ở, lời khuyên chủ nhà) nhờ từ điển khoá bằng chính chuỗi tiếng Việt. Ngày/giờ/số theo ngôn ngữ đang chọn. Nội dung **người dùng tự viết** (tên tin, mô tả, đánh giá, tiểu sử, nội quy chủ nhà tự gõ) được **máy dịch tự động** kèm dòng "Đã dịch tự động · Xem bản gốc" |
| Máy dịch | `libretranslate` tự host trong cả hai compose — **không cần khoá API, không tính tiền theo ký tự**. Đủ 8 thứ tiếng, trùng khít danh sách giao diện. Kết quả cache trong `translation_caches`, mỗi (chuỗi × ngôn ngữ) chỉ dịch một lần |
| Hạn dùng số dư | `docs/07 §16` đã chốt (11/08/2026): bù đắp / giới thiệu bạn / hoàn khi huỷ **12 tháng**, thẻ quà tặng **không hết hạn**. Hạn đóng dấu **lúc cấp**, nên đổi tham số về sau không với ngược lại số dư khách đang giữ |

---

## 4. Bài học đã trả giá — đừng lặp lại

- **Dừng `dotnet run` trước khi build lại.** Tiến trình đang chạy giữ DLL, MSBuild
  báo `MSB3027`/`MSB3021`. Dùng `TaskStop` rồi mới build.
- **Sau `dotnet ef migrations add` phải `dotnet build` lại** trước khi chạy, nếu không
  app báo `PendingModelChangesWarning`.
- **`{action}` là token dành riêng trong route ASP.NET.** `[HttpPost("{id:int}/{action}")]`
  bị viết lại thành tên method và trả 405. Dùng `{decision}`.
- **`Forbid()` ném exception ở app này.** Phiên đăng nhập là cookie tự phát, không có
  authentication scheme, nên `Forbid()` thành 500. Dùng `this.Denied()`
  (`Infrastructure/SessionAccessor.cs`).
- **EF không project được positional record ra khỏi `GroupBy`** — project sang anonymous
  type rồi map trong bộ nhớ.
- Entity `Host` từng đụng `Microsoft.Extensions.Hosting.Host` → đã đổi thành `HostProfile`.
- `/api/account/me` trả **204** khi chưa đăng nhập (không phải 200 rỗng).
- **Đơn đặt đừng đếm chính nó.** Tạo giữ chỗ đã tính là một lượt bán, nên nếu lúc thanh
  toán tính lại giá mà vẫn đếm nó thì đơn tự làm mất giảm giá tin mới rồi tự fail.
- **Số dư đã cam kết lúc giữ chỗ phải được đưa vào lần tính giá lại lúc thanh toán**,
  nếu không đơn dùng số dư luôn trượt kiểm tra "giá có đổi không".
- Vite 8 (rolldown) chỉ nhận `manualChunks` dạng hàm.
- **Có file `.cs` không có nghĩa là có chạy.** Soát `docs/08` ngày 08/08 phát hiện
  `SuspensionImpact` tính đúng cả bảng §6 mà chưa bao giờ được gọi khi khoá thật,
  `Appeals` đủ luật mà người dùng không có đường nộp. Kiểm chứng bằng **kết quả
  trong cơ sở dữ liệu**, đừng kiểm chứng bằng màn hình xem trước.
- **Đơn do sàn huỷ đừng ghi sang phía khách.** `PostCancellation` map
  Platform/ForceMajeure sang `CancelledByHost`; mọi chỗ đếm số lần huỷ để đánh giá
  một người phải lọc thêm `CancelledBy`. Ghi nhầm là khách mất quyền hoàn phí dịch
  vụ 3 lần/năm của `docs/03 §4` vì lỗi của người khác.
- **Đừng mượn quy tắc §8 (khiếu nại phải người khác xét) áp cho §1.3.** Chặn admin
  "đã từng ra quyết định với người này" làm thang bậc §5 không đi được: leo thang
  bắt buộc phải cùng một người viết bước tiếp theo.
- **`ledger_entries.BookingId` là khoá ngoại tới bảng `bookings` (chỗ ở).** Trải nghiệm
  và dịch vụ có cột riêng (`ExperienceBookingId`, `ServiceBookingId`) — truyền nhầm id
  vào `BookingId` thì mọi lần chi tiền đều ném, và vì nó ném trong tick của worker nên
  **các sweep phía sau cũng chết theo** mà không ai thấy.
- **Không `dotnet ef migrations add --no-build`.** Migration khi đó sinh từ assembly cũ,
  chạy lên là `PendingModelChangesWarning`. Phải dừng app → build → mới `migrations add`.
  Và khi lọc log build, đừng chỉ `grep "error CS"`: lỗi khoá DLL là `MSB3027`, build coi
  như hỏng mà mình tưởng xanh.
- **Giữ chỗ rồi thì lúc thanh toán đừng kiểm tra như người lạ.** `CanBook` thấy ghế đã bị
  trừ sẽ từ chối chính đơn mà lượt giữ chỗ sinh ra để bảo vệ — phải cộng lại phần mình
  đang giữ trước khi kiểm tra.
- **Dịch giao diện (`lib/i18n.js`): từ điển khoá bằng chính chuỗi tiếng Việt.** `t(s, ctx?)`
  tra theo thứ tự `"s|ctx"` → khớp đúng → **mẫu số `{}`** → trả nguyên bản. Chữ **do
  server sinh** cũng dịch được bằng cách bọc `t(field)` ở chỗ render rồi thêm cặp vào từ
  điển. Bản dịch **phải giữ đúng số lượng và thứ tự `{}`**, thiếu một cái là thay số sai.
  Không hard-code `'vi-VN'` cho `Intl` — dùng `dateFormat()`/`number()` của `format.js`.
  Nội dung **người dùng tự viết** (tên tin, mô tả, đánh giá, tiểu sử) **không vào từ
  điển** — `TranslatedText.jsx` máy dịch tự động và ghi rõ "Đã dịch tự động".
- **Coi chừng biến tên `t` che mất hàm dịch.** Đã gặp `const t = encodeURIComponent(...)`
  và `map(t => …)`; khi đó `t('…')` âm thầm chạy sai chứ không báo lỗi.
- **Thiếu khoá dịch không báo lỗi ở đâu cả** — `t()` trả nguyên bản, console sạch,
  test xanh, build xanh. Đó là lý do khách phải tự mắt tìm ra **năm đợt** liên tiếp.
  Chạy `python scripts/i18n_audit.py` (phải ra **0**). Nó chỉ soát được literal;
  `t(giá_trị_server)` thì phải mở trang bằng ngôn ngữ khác rồi tìm chữ còn dấu tiếng
  Việt trong DOM. **Có component chưa từng import `t`** — `PhotoMosaic`,
  `CardCarousel`, `Maps` đứng ngoài toàn bộ hệ dịch mà không ai biết.
- **Chữ do server sinh phải bọc `t()` ở *mọi* chỗ render, không chỉ chỗ hay nhìn.**
  Khách báo hộp "Nơi này có những gì" hiện từng món bằng tiếng Nhật nhưng **tên nhóm**
  ("Tiện nghi", "Ngoài trời") vẫn tiếng Việt: `SearchModals` viết `t(group)`, còn
  `ListingModals` và `ListingWizard` viết thẳng `{group}`. Khoá đã có sẵn trong từ
  điển — thiếu đúng cái bọc. Cùng lỗi ở nhãn loại chỗ ở (`Header`, `Browse`, wizard).
  Khi thêm chỗ render dữ liệu server, hỏi "cái này có nằm trong từ điển không?"
- **`LT_LOAD_ONLY` chỉ có tác dụng lần chạy đầu của volume.** Thêm `de,es` rồi restart
  thì container vẫn **healthy**, `/languages` vẫn 6 thứ tiếng, và `/translate` trả
  `"de is not supported"` — hỏng mà không có dấu hiệu nào. Đã bật `LT_UPDATE_MODELS`
  ở cả hai compose. Danh sách này phải khớp `Translations.Targets`, khớp luôn cả danh
  sách ngôn ngữ giao diện (`CatalogService.Languages`).
- **Đừng tự dựng `CreditEntry` bằng tay.** `docs/01 TC-07` đóng dấu hạn dùng **lúc cấp**;
  đường hoàn số dư khi huỷ đơn trong `BookingsController` tự `new CreditEntry` nên bỏ
  qua bước đó, và số dư hoàn lại **không bao giờ hết hạn** dù cấu hình nói 12 tháng.
  Giờ mọi dòng số dư đi qua `CreditLedger.Grant`, có test chặn.

---

## 5. Chạy dự án

```bash
docker compose up -d db                                   # Postgres cổng 5544
dotnet run --project src/StayHost.Web --urls http://localhost:5199
cd src/StayHost.Web/ClientApp && npm run dev              # dev frontend, cổng 5273
docker compose up -d --build                              # tất cả, web: http://localhost:8090
```

**Reset DB khi đổi schema:**
```bash
docker exec stayhost-db psql -U stayhost -d stayhost -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"
```

### Tài khoản demo (mật khẩu `stayhost123`)
`guest@stayhost.vn` · `host1@stayhost.vn` … `host10@stayhost.vn` · `admin@stayhost.vn`

**Tài khoản admin bắt buộc có bảo mật 2 lớp** (`docs/08 §3`, không có ngoại lệ), nên
đăng nhập admin đi qua hai bước. Chạy server với `ASPNETCORE_ENVIRONMENT=Development`
thì API trả luôn mã trong `devCode`; đó cũng là điều kiện để
`scripts/admin_acceptance.py` chạy được.

### Máy dịch (`TĐ-03`, `TN-06`) — tự host, không cần khoá API

`docker compose up -d` đã kéo luôn `libretranslate` và trỏ app vào đó, nên bản chạy
bằng compose **có sẵn nút "Dịch"**, không phải mua gì. Đủ **8 thứ tiếng**, đúng bằng
danh sách giao diện cho chọn.

Chạy app bằng `dotnet run` thì trỏ tay vào container (compose đã publish cổng 5555
ở loopback):

```bash
docker compose up -d libretranslate
dotnet run --project src/StayHost.Web --urls http://localhost:5199
# với Translation__Provider=libretranslate Translation__Url=http://localhost:5555
```

Không cấu hình gì thì `/api/translate/config` trả `enabled:false` và giao diện **không
đổi gì** — đó là mặc định đúng, không phải lỗi. Muốn chất lượng cao hơn thì đổi
`Translation__Provider=google` + `Translation__ApiKey` (biến môi trường, không để trong
`appsettings.json`).

### Thẻ thử nghiệm
Mọi thẻ đều thành công, **trừ thẻ kết thúc `0000`** — thẻ đó luôn bị từ chối. Đó là cách
duy nhất chạy được nhánh "thu lần hai thất bại" của `docs/03 §1`.

Thẻ kết thúc **`0002`** luôn đòi OTP (`docs/07 §5`), mã đúng là **`123456`**. Trang ngân
hàng là một chặng riêng — `POST /api/bookings/{id}/bank-otp` — vì ngoài đời nó là một
trang khác: ngân hàng trừ tiền ở đó rồi sàn mới biết. Gọi chặng đó xong mà **không** quay
lại `/pay` chính là kịch bản 3 của `docs/07 §18`; `CardAuthSweeper` hỏi lại cổng thanh
toán rồi tự xác nhận đơn.

### Đăng nhập Google / Apple / Facebook (`docs/01 TK-02`)

Nút của nhà cung cấp nào **chưa có mã thì không hiện** trên hộp đăng nhập — thà thiếu nút
còn hơn có nút bấm vào không chạy. Điền vào `ExternalLogin:` trong `appsettings.json`,
riêng `FacebookAppSecret` đặt qua biến môi trường `ExternalLogin__FacebookAppSecret`.

| Khoá | Lấy ở đâu |
|---|---|
| `GoogleClientId` | console.cloud.google.com → Credentials → OAuth client ID (Web). Khai **Authorised JavaScript origins**: `https://staylio.bluestar.com.vn` và `http://localhost:5199` |
| `AppleServicesId` + `AppleRedirectUri` | developer.apple.com → Services ID. Return URL phải **trùng từng ký tự** với `AppleRedirectUri`. Cần tài khoản Apple Developer trả phí |
| `FacebookAppId` + `FacebookAppSecret` | developers.facebook.com → App → Facebook Login for Web |

Máy chủ **không tin gì trình duyệt gửi lên**: token của Google/Apple được kiểm chữ ký
RS256 theo bộ khoá công khai của chính họ (`ExternalTokenVerifier`), token Facebook được
đem hỏi lại Graph `debug_token`. Email chưa được nhà cung cấp xác thực thì **không**
được phép ghép vào tài khoản sẵn có.

---

## 6. Kiểm chứng trước khi commit

```bash
dotnet test tests/StayHost.Domain.Tests            # 936 test nghiệp vụ
python scripts/acceptance.py                       # 10 tình huống của docs/04
python scripts/admin_acceptance.py                 # 10 tình huống của docs/08 §13
python scripts/doc09_acceptance.py                 # 19 kịch bản của docs/09
python scripts/i18n_audit.py                       # khoá dịch còn thiếu (phải ra 0)
cd src/StayHost.Web/ClientApp && npm run build && npx oxlint src

# Sổ sách phải luôn cân bằng: kết quả duy nhất chấp nhận được là 0
docker exec stayhost-db psql -U stayhost -d stayhost -t \
  -c 'select coalesce(sum(case when "Direction"=1 then "Amount" else -"Amount" end),0) from ledger_entries;'
```

---

## 7. Quy ước

- **Giao tiếp với khách bằng tiếng Việt.** Code, comment, commit message bằng tiếng Anh.
- Nội dung hiển thị trên UI: **tiếng Việt**.
- Mọi quy tắc tính tiền chỉ định nghĩa **một lần** trong `StayHost.Domain/Pricing.cs`
  (`docs/00 §6.8`) — tìm kiếm, trang chi tiết và thanh toán phải ra **cùng một con số**.
- Sổ sách: mọi khoản tiền ghi hai chiều, **bất biến**, không sửa không xoá (`docs/05`).
- Số dư khách cũng là sổ chỉ-thêm: số dư là tổng các dòng, không phải một cột bị ghi đè.
- **StayShield không bao giờ được gọi là bảo hiểm** (`docs/06 §11`). Mọi chữ hiển thị
  là "chính sách hỗ trợ". Có `Shield.ReadsAsInsurance` và test chặn từ ngữ này.
- **`docs/PLAN.md §9` đã soát đủ cả 201 mã: 201 xong · 0 một phần · 0 chưa có**
  (soát 07/08/2026, dọn nốt 10/08/2026). Hai việc từng "chờ khách quyết" đã xong
  ngày 11/08/2026: `TĐ-03`/`TN-06` chạy bằng máy dịch tự host, `TC-07` chốt tham số
  ở `docs/07 §16`. Plan đã ba lần đếm lệch (hai lần bỏ sót việc thật, một lần kê
  tám mã đã xong vào bảng "làm một phần"), nên thêm mã mới thì sửa §9.1/§9.2 ngay
  lúc đó.
- Kiểm chứng bằng app đang chạy thật, không chỉ đọc code.
- Commit theo từng mốc có nghĩa, push lên `origin main`.

Remote: **https://github.com/minhhung19872002/StayHost**
