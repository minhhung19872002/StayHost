# StayHost OS

Marketplace thuê nhà ngắn hạn theo mô hình Airbnb, dựng từ thiết kế
**StayHost Marketplace.dc.html** (Claude Design).

Stack: **ASP.NET Core 9** · **PostgreSQL 17** (EF Core / Npgsql) · **Docker Compose** ·
frontend vanilla ES modules (không build step).

---

## Chạy nhanh

### Docker (khuyến nghị)

```bash
docker compose up -d --build
```

- Web: <http://localhost:8090>
- Health: <http://localhost:8090/health>
- Postgres: `localhost:5544` (db `stayhost`, user/pass `stayhost`)

Migration và seed data chạy tự động lúc khởi động (idempotent — chỉ seed khi bảng
`listings` rỗng).

### Chạy local (không Docker)

```bash
docker compose up -d db          # chỉ cần Postgres
dotnet run --project src/StayHost.Web --urls http://localhost:5199
```

`appsettings.Development.json` trỏ sẵn vào `localhost:5544`.

---

## Cấu trúc

```
src/
├─ StayHost.Domain/           entity + enum thuần, không phụ thuộc EF
├─ StayHost.Infrastructure/   DbContext, migrations, DbSeeder
└─ StayHost.Web/              API controllers, DTO, CatalogService, SPA tĩnh
   ├─ Contracts/Dtos.cs       hợp đồng JSON giữa API và frontend
   ├─ Services/CatalogService.cs  toàn bộ logic tìm kiếm / trang chủ / báo giá
   └─ wwwroot/
      ├─ index.html           shell
      ├─ css/styles.css       design token lấy từ file design
      └─ js/
         ├─ app.js            router + 1 delegated event handler duy nhất
         ├─ store.js          state + action
         ├─ api.js            wrapper fetch
         ├─ util.js           format tiền/ngày, escape HTML, toast
         ├─ components/       header, footer, card, calendar, modals, map, icons
         └─ views/            browse, detail, wishlists, trips, host
```

Frontend không dùng framework: mỗi view là một hàm `render(state) -> HTML`, mọi
tương tác đi qua thuộc tính `data-act` và một handler delegated trong `app.js`.

---

## API

| Method | Endpoint | Mô tả |
|---|---|---|
| GET | `/api/meta` | danh mục, tiện nghi lọc được, thành phố, khoảng giá + histogram, tiền tệ, ngôn ngữ |
| GET | `/api/home` | các rail carousel trang chủ + link gợi ý |
| GET | `/api/listings` | tìm kiếm (q, category, minPrice, maxPrice, guests, amenities, roomType, bedrooms, beds, bathrooms, superhost, guestFavorite, sort, page, pageSize) |
| GET | `/api/listings/{id\|slug}` | chi tiết: ảnh, tiện nghi theo nhóm, đánh giá + breakdown, chủ nhà, chỗ tương tự, **ngày đã kín lịch** |
| GET | `/api/quote` | tính giá theo listingId + ngày + số khách |
| GET/POST/DELETE | `/api/favorites[/{id}]` | wishlist theo cookie phiên `sh_sid` |
| GET/POST | `/api/bookings` | đặt chỗ (chặn trùng ngày) và xem chuyến đi |
| POST | `/api/bookings/{id}/cancel` | huỷ đặt chỗ |
| GET | `/health` | health check kèm kiểm tra kết nối Postgres |

Danh tính người dùng là cookie ẩn danh `sh_sid` (HttpOnly, 1 năm) — wishlist và
booking gắn theo cookie này, không cần đăng ký.

---

## Tính năng

**Trang chủ** — tab Chỗ ở / Trải nghiệm / Dịch vụ, thanh tìm kiếm phân đoạn
(Địa điểm · Ngày · Khách), các rail carousel theo thành phố và theo chủ đề, khối
"Gợi ý cho chuyến đi sắp tới" có tab.

**Kết quả tìm kiếm** — dải danh mục cuộn ngang, modal bộ lọc đầy đủ (histogram giá +
dual range slider, loại nơi ở, số phòng/giường, loại chỗ ở, tiện nghi theo nhóm,
siêu chủ nhà, khách yêu thích, sắp xếp), công tắc "Hiển thị tổng giá", chế độ bản đồ
chia đôi màn hình với marker giá (Leaflet + OpenStreetMap), phân trang "xem thêm".

**Thẻ chỗ nghỉ** — carousel ảnh (mũi tên + dot), nhãn Khách yêu thích / Siêu chủ nhà,
nút tim lưu ngay (optimistic update).

**Chi tiết** — gallery 5 ảnh + modal xem tất cả, chia sẻ/lưu, thông tin chủ nhà,
điểm nổi bật, "Nơi bạn sẽ ngủ", tiện nghi + modal đầy đủ, lịch 2 tháng (ngày đã có
người đặt bị làm mờ và không chọn được), đánh giá kèm 6 tiêu chí + tìm kiếm/sắp xếp
trong modal, bản đồ vị trí gần đúng, hồ sơ chủ nhà, "Những điều cần biết", chỗ nghỉ
tương tự, panel đặt phòng dính kèm báo giá thời gian thực, modal thanh toán, thanh
đặt chỗ cố định trên mobile.

**Khác** — wishlist, chuyến đi (xem/huỷ), trang cho thuê nhà kèm máy tính doanh thu
và FAQ, modal đăng nhập, đổi ngôn ngữ (8) và tiền tệ (8, quy đổi trực tiếp trên UI),
footer nhiều cột.

Responsive từ 320px trở lên; toàn bộ nội dung tiếng Việt.

---

## Ghi chú kỹ thuật

- Giá tiền tính **phía server** (`CatalogService.QuoteAsync` / `BookingsController`);
  frontend chỉ hiển thị, nên đổi tiền tệ không ảnh hưởng số tiền lưu trong DB (luôn là VND).
- `POST /api/bookings` từ chối khoảng ngày chồng lấn (409) và số khách vượt sức chứa (400).
- Ảnh lấy từ Pexels CDN; bản đồ dùng tile OpenStreetMap — cả hai cần internet.
- Tab **Trải nghiệm** / **Dịch vụ** hiện là placeholder: dữ liệu và luồng đặt chỗ mới
  có cho mảng chỗ ở.
- Đăng nhập là demo UI, chưa gắn nhà cung cấp danh tính thật.

## Lệnh hay dùng

```bash
dotnet build                                     # build toàn solution
dotnet ef migrations add <Ten> \                 # thêm migration
  --project src/StayHost.Infrastructure \
  --startup-project src/StayHost.Web
docker compose logs -f web                       # xem log container
docker compose down -v                           # xoá luôn dữ liệu Postgres
```
