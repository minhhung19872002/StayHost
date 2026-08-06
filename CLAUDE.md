# CLAUDE.md — StayHub / StayHost

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
| `docs/PLAN.md` | **Đối chiếu hiện trạng ↔ spec + lộ trình 8 giai đoạn** |

**Bắt đầu phiên mới: đọc `docs/PLAN.md` trước.** File đó đã ghi rõ cái gì đúng, cái gì
sai, cái gì chưa có.

---

## 2. Hai câu hỏi đang chờ khách trả lời

Chưa có câu trả lời thì **đừng tự quyết**:

1. **Đổi tên sang StayHub?** Tài liệu gọi sản phẩm là **StayHub**; code hiện tên
   *StayHost OS*. `docs/00 §4` cũng yêu cầu đổi tên danh hiệu:
   "Siêu chủ nhà" → **"Chủ nhà Ưu tú"**, "Khách yêu thích" → **"Khách chọn"**,
   và thêm chương trình bảo vệ **StayShield**.
2. **Phí 14% khách / 3% chủ nhà** trong `docs/03 §1` là con số thật hay chỉ là ví dụ?
   Hiện code đang để 9% khách / 0% chủ nhà — **sai so với tài liệu**.

---

## 3. Hiện trạng

### Backend — chạy được, nhưng nghiệp vụ tiền đang SAI so với spec

.NET 9 + EF Core + PostgreSQL 17. Đã có: tài khoản (PBKDF2 + phiên cookie), tìm kiếm &
lọc, đặt chỗ, wishlist nhiều danh sách, tin nhắn, đánh giá, trang chủ nhà (CRUD tin,
lịch, giá mùa, doanh thu), thông báo + hàng đợi email, báo cáo, trang admin.

**Sai/thiếu đã xác định** (chi tiết trong `docs/PLAN.md`):
- Phí dịch vụ khách 9% (spec: **14%**), thiếu phí chủ nhà **3%**
- Thiếu: trần giảm giá 60%, phụ thu khách thêm, phí thú cưng, giảm đặt sớm/phút chót/tin mới
- Thuế cứng 8% (spec: **theo khu vực**, nhiều loại chồng nhau, 4 cách tính)
- Chính sách huỷ có 3 (spec: **6**), thiếu ân hạn 48h, phí vệ sinh phải **luôn hoàn 100%**
- Vòng đời đơn có 3 trạng thái (spec: **9**), **chưa có bảng lịch sử đơn**
- **Chưa có sổ ghi tiền hai chiều** — `docs/00 §6.1` coi đây là nguyên tắc số một
- Thiếu giữ chỗ 15 phút, yêu cầu đặt hết hạn 24h, 6/9 bước kiểm tra "đặt được không"

### Frontend — ĐANG CHUYỂN DỞ sang React

Khách yêu cầu chuyển hết sang React (đã xác minh airbnb.com dùng React 19 +
React Router 7 + Metro). **Đang ở giữa chừng:**

| | Trạng thái |
|---|---|
| Vanilla JS cũ (`src/StayHost.Web/wwwroot/js/`) | **vẫn đang chạy production** — chưa xoá |
| `ClientApp/` scaffold Vite + React 19 + React Router 7 + Leaflet | ✅ xong |
| `ClientApp/src/lib/{api,format,store}.js` | ✅ đã port |
| `ClientApp/src/components/` | ⬜ **trống — làm tiếp từ đây** |
| `ClientApp/src/pages/` | ⬜ trống |
| `main.jsx`, `App.jsx` | ⬜ chưa có |
| Dockerfile thêm stage Node | ⬜ chưa |

**Cách port:** store đã là external store (`subscribe`/`getSnapshot`/`notify`) → bind vào
React bằng `useSyncExternalStore`. Các view cũ là hàm `render(state) -> HTML` nên map gần
1:1 sang JSX. Copy `ClientApp/src/styles.css` (đã có sẵn, giữ nguyên).

Cần port 9 trang: Browse (discovery + results), Detail, Wishlists, Trips, Trip, Host,
Hosting, Messages, Admin. Và các component: Header, Footer, Card, Calendar, Map, Modals, Icons.

Khi React chạy xong: **xoá `wwwroot/js/` và `wwwroot/css/`**, giữ `wwwroot/uploads/`.

---

## 4. Bài học đã trả giá — đừng lặp lại

- **Đừng ghi đè `innerHTML` cả trang mỗi lần đổi state.** Bản vanilla từng tạo 150 thẻ
  `<img>` và dựng lại bản đồ 2 lần chỉ vì một cú click ♥. Đã vá bằng memo theo vùng +
  giữ node `#map` sống. React sẽ giải quyết triệt để việc này.
- **Sau khi `dotnet ef migrations add`, phải `dotnet build` lại** trước khi chạy, nếu không
  app báo `PendingModelChangesWarning` và không khởi động được.
- Entity `Host` từng đụng tên với `Microsoft.Extensions.Hosting.Host` → đã đổi thành
  **`HostProfile`**. Đừng đặt lại tên `Host`.
- `/api/account/me` trả **204** khi chưa đăng nhập (không phải `200` với body rỗng).

---

## 5. Chạy dự án

```bash
# Postgres (cổng 5544 vì 5432/5433 đã bị chiếm trên máy này)
docker compose up -d db

# API (dev)
dotnet run --project src/StayHost.Web --urls http://localhost:5199

# Frontend React (dev, proxy sang 5199)
cd src/StayHost.Web/ClientApp && npm run dev     # cổng 5273

# Toàn bộ bằng Docker
docker compose up -d --build                      # web: http://localhost:8090
```

**Reset DB khi đổi schema:**
```bash
docker exec stayhost-db psql -U stayhost -d stayhost -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"
```

### Tài khoản demo (mật khẩu `stayhost123`)
`guest@stayhost.vn` · `host1@stayhost.vn` … `host10@stayhost.vn` · `admin@stayhost.vn`

---

## 6. Quy ước

- **Giao tiếp với khách bằng tiếng Việt.** Code, comment, commit message bằng tiếng Anh.
- Nội dung hiển thị trên UI: **tiếng Việt**.
- Mọi quy tắc tính tiền chỉ định nghĩa **một lần** trong `StayHost.Domain/Pricing.cs`
  (`docs/00 §6.8`) — tìm kiếm, trang chi tiết và thanh toán phải ra **cùng một con số**.
- Sổ sách: mọi khoản tiền ghi hai chiều, **bất biến**, không sửa không xoá (`docs/05`).
- Kiểm chứng bằng **chrome-devtools-mcp** trên app đang chạy thật, không chỉ đọc code.
- Commit theo từng mốc có nghĩa, push lên `origin main`.

---

## 7. Git

Remote: **https://github.com/minhhung19872002/StayHost**

Lịch sử tới hiện tại:
```
7d25fa2  Stop rebuilding the whole page on every state change   ← perf fix
4ff6fd6  Complete host tooling: uploads, calendar, seasonal pricing, guest reviews
6d2d978  Add named wishlists, notifications, reports and an admin console
7c0ecb2  Add account recovery, device management and search polish
e2af08a  Fix six confirmed bugs and put real money rules behind bookings
bbfb63c  Match airbnb.com page layouts and finish the business flows
9d27ed2  Add accounts, host console, messaging and guest reviews to the UI
a5b0efb  StayHost OS: Airbnb-style short-stay marketplace on .NET 9 + PostgreSQL
```

---

## 8. Làm tiếp từ đâu

Theo `docs/PLAN.md`:

1. **Xong React migration** (đang dở ở `ClientApp/src/components/`) — làm nốt trước, vì
   sửa engine tiền trên codebase chuyển đổi dở sẽ phải làm hai lần.
2. **Giai đoạn 1 — tiền đúng tuyệt đối**: viết lại `Pricing` theo đúng 11 bước của
   `docs/03 §1`, thuế theo khu vực, 6 chính sách huỷ, sổ ghi tiền hai chiều.
   Viết test tự động cho **8 tình huống giá** ở `docs/03 §1` và bảng chính sách huỷ.
3. **Giai đoạn 2 — vòng đời đơn**: 9 trạng thái + lịch sử chỉ-thêm + giữ chỗ 15 phút +
   9 bước kiểm tra đặt được + chống đặt trùng ở mức DB.
4. Sau đó theo thứ tự A→B→C→D của `docs/00 §5`, **không nhảy cóc**.

Thước đo "xong": chạy được đủ **10 tình huống nghiệm thu** ở cuối `docs/04`.
