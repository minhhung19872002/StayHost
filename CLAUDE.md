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

**10/10 tình huống nghiệm thu** của `docs/04` chạy được trên server thật
(`scripts/acceptance.py`). **418 test nghiệp vụ** xanh.

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
| Tài khoản | Đăng ký bằng SĐT hoặc email + OTP 6 số, đăng nhập Google/Apple/Facebook, chặn dưới 18 tuổi |
| Hồ sơ | Ảnh đại diện, tên hiển thị, ngôn ngữ nói, nơi ở, nghề nghiệp, sở thích; trang công khai `/users/:id` có huy hiệu xác minh và đánh giá hai chiều |
| Nhận phòng | Hướng dẫn nhận phòng đầy đủ trên trang chuyến đi; địa chỉ và số điện thoại chỉ hiện sau khi đơn được xác nhận, **mã cửa chỉ hiện từ 48 giờ trước giờ nhận** |
| Bảo mật tài khoản | Xác minh danh tính có người duyệt, bảo mật 2 lớp bằng mã 6 số, ma trận thông báo loại × kênh, tải toàn bộ dữ liệu cá nhân |
| Danh hiệu | Siêu chủ nhà xét mỗi quý, Khách chọn xét hằng tuần — cấp và thu hồi tự động. Ngưỡng chỉ nằm trong `Badges.cs` |
| Xếp hạng | Điểm tổng hợp 7 yếu tố của `docs/03 §6` trong `Ranking.cs`, có trừ điểm và đa dạng hoá 12 kết quả đầu ≤ 2 chỗ mỗi chủ nhà |
| StayShield | Hai nhánh K1–K4 / C1–C4 (kể cả bên thứ ba), cửa sổ khiếu nại, thứ tự thu tiền, quỹ trích từ phí dịch vụ, khiếu nại một lần do người khác xét |
| Mở rộng | Trải nghiệm (bán theo vé), Dịch vụ (bán theo khung giờ + bán kính), Khách sạn (nhiều loại phòng có tồn kho), thẻ quà tặng, số dư, giới thiệu bạn bè |

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

### Thẻ thử nghiệm
Mọi thẻ đều thành công, **trừ thẻ kết thúc `0000`** — thẻ đó luôn bị từ chối. Đó là cách
duy nhất chạy được nhánh "thu lần hai thất bại" của `docs/03 §1`.

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
dotnet test tests/StayHost.Domain.Tests            # 418 test nghiệp vụ
python scripts/acceptance.py                       # 10 tình huống của docs/04
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
- **`docs/PLAN.md §9` đã soát đủ cả 201 mã (07/08/2026): 137 xong · 13 một phần ·
  51 chưa có.** Đọc **§9.0** trước — chín mã P0 còn nợ. Trước lần soát này plan chỉ
  ghi 58/201 mã và đã hai lần bỏ sót việc thật, nên nếu thêm tính năng mới thì cập
  nhật §9 luôn, đừng để nó lệch lại.
- Kiểm chứng bằng app đang chạy thật, không chỉ đọc code.
- Commit theo từng mốc có nghĩa, push lên `origin main`.

Remote: **https://github.com/minhhung19872002/StayHost**
