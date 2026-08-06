# CLAUDE.md — StayHost

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
| `docs/PLAN.md` | **Đối chiếu hiện trạng ↔ spec + việc còn lại** |

**Bắt đầu phiên mới: đọc `docs/PLAN.md` trước.**

---

## 2. Hai câu hỏi đã được khách trả lời (06/08/2026)

1. **Tên sản phẩm:** giữ **StayHost OS**, giữ danh hiệu "Siêu chủ nhà" và "Khách yêu thích".
   Không đổi sang StayHub. (Chương trình **StayShield** vẫn chưa làm — chưa có yêu cầu chi tiết.)
2. **Phí dịch vụ:** **14% khách / 3% chủ nhà** đúng theo `docs/03 §1`. Nằm trong
   `PricingSettings`, đổi được qua cấu hình `Pricing:` mà không phải sửa code.

---

## 3. Hiện trạng

**10/10 tình huống nghiệm thu của `docs/04` đạt** — chạy được đầu-cuối trên dữ liệu thật.
150 test nghiệp vụ pass.

### Backend — .NET 9 + EF Core + PostgreSQL 17

Nghiệp vụ tiền, vòng đời đơn, sổ sách và tranh chấp **đã đúng spec**:

- `StayHost.Domain/Pricing.cs` — 11 bước của `03 §1`, một nơi duy nhất
- `StayHost.Domain/Cancellation.cs` — 6 chính sách + 4 quy tắc áp trước
- `StayHost.Domain/Ledger.cs` — sổ ghi tiền hai chiều, bất biến, luôn cân
- `StayHost.Domain/BookingLifecycle.cs` — 10 trạng thái + bảng chuyển hợp lệ
- `StayHost.Domain/Availability.cs` — 9 bước kiểm tra đặt được, mỗi bước một lý do
- `StayHost.Domain/Resolution.cs` — Trung tâm giải quyết + phân xử
- `StayHost.Domain/ContentGuard.cs` — che liên hệ trong tin nhắn, chặn trong đánh giá
- `StayHost.Domain/SearchText.cs` — tìm không dấu và theo viết tắt

### Frontend — React 19 + Vite + React Router 7

`src/StayHost.Web/ClientApp/`. Vanilla JS cũ **đã xoá hẳn**. Store là external store
(`subscribe`/`getSnapshot`/`notify`) bind vào React qua `useSyncExternalStore`.

### Việc còn lại

Xem `docs/PLAN.md` §Lộ trình. Tóm tắt: đăng tin theo bước có lưu nháp (`CN-01`),
lịch nhiều tin cùng lúc (`QL-04`), đồng bộ iCal (`QL-10`), co-host (`QL-19`),
gửi ảnh trong tin nhắn (`TN-02`), và toàn bộ nhóm mở rộng (Trải nghiệm, Dịch vụ,
Khách sạn). **Không phần nào trong số đó chạm vào tiền.**

---

## 4. Bài học đã trả giá — đừng lặp lại

- **`{action}` là tên bị ASP.NET Core chiếm dụng trong attribute routing.** Route
  `bookings/{id}/{action}` bị thay bằng tên method nên `/confirm` chưa bao giờ chạy.
  Đừng đặt tên tham số route là `action`, `controller`, `area`, `page`, `handler`.
- **Sau khi `dotnet ef migrations add`, phải `dotnet build` lại** trước khi chạy.
- **Phải dừng `dotnet run` trước khi build lại**, nếu không MSBuild báo file bị khoá.
- Entity `Host` từng đụng tên với `Microsoft.Extensions.Hosting.Host` → đã đổi thành
  **`HostProfile`**. Đừng đặt lại tên `Host`.
- `/api/account/me` trả **204** khi chưa đăng nhập (không phải `200` với body rỗng).
- **EF không project được record có constructor positional từ trong `GroupBy`.**
  Project sang anonymous type rồi map ngoài bộ nhớ.
- **Vite 8 (rolldown) chỉ nhận `manualChunks` dạng hàm**, không nhận object.
- **Đổi giá trị enum đã lưu trong DB thì phải reset DB.** `BookingStatus` đã đánh số lại
  ở Giai đoạn 2.

---

## 5. Chạy dự án

```bash
# Postgres (cổng 5544 vì 5432/5433 đã bị chiếm trên máy này)
docker compose up -d db

# API (dev) — phục vụ luôn bản React đã build trong wwwroot
dotnet run --project src/StayHost.Web --urls http://localhost:5199

# Frontend React (dev, proxy sang 5199)
cd src/StayHost.Web/ClientApp && npm run dev     # cổng 5273

# Build lại bản production của frontend
cd src/StayHost.Web/ClientApp && npm run build

# Toàn bộ bằng Docker
docker compose up -d --build                      # web: http://localhost:8090
```

**Production và CI/CD: xem `DEPLOY.md`.** Push lên `main` là tự chạy test → build image
lên GHCR → deploy vào VPS. `docker-compose.yml` chỉ dùng cho máy dev;
`docker-compose.prod.yml` mới là bản chạy thật.

**Reset DB khi đổi schema:**
```bash
docker exec stayhost-db psql -U stayhost -d stayhost -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"
```

### Kiểm chứng
```bash
dotnet test tests/StayHost.Domain.Tests    # 150 test nghiệp vụ
python scripts/acceptance.py               # 10 tình huống nghiệm thu, cần server ở 5199
```

### Tài khoản demo (mật khẩu `stayhost123`)
`guest@stayhost.vn` · `host1@stayhost.vn` … `host10@stayhost.vn` · `admin@stayhost.vn`

---

## 6. Quy ước

- **Giao tiếp với khách bằng tiếng Việt.** Code, comment, commit message bằng tiếng Anh.
- Nội dung hiển thị trên UI: **tiếng Việt**.
- Mọi quy tắc tính tiền chỉ định nghĩa **một lần** trong `StayHost.Domain/Pricing.cs`
  (`docs/00 §6.8`) — tìm kiếm, trang chi tiết và thanh toán phải ra **cùng một con số**.
  Frontend không được tự tính giá: nó đọc `stayTotal` và `lines` từ máy chủ.
- Sổ sách, lịch sử đơn, lịch sử hồ sơ và nhật ký quản trị đều **chỉ-thêm**.
  `SaveChanges` sẽ ném lỗi nếu có ai sửa hoặc xoá.
- Kiểm chứng bằng **chrome-devtools-mcp** trên app đang chạy thật, không chỉ đọc code.
- Commit theo từng mốc có nghĩa, push lên `origin main`.

---

## 7. Git

Remote: **https://github.com/minhhung19872002/StayHost**

```
66bea48  Add the resolution centre, admin roles and an admin audit log
2123d1c  Make reviews blind both ways and hide contact details until a booking exists
2282bfe  Hold dates while the guest pays, and give hosts the tools to run a listing
5fda339  Fill the discovery gaps in the search and room pages
b2af907  Give bookings the ten-state lifecycle the spec describes
3728ee0  Clear stale hashed assets before each front-end build
2c94b9d  Rewrite the money engine to match the spec, and put every amount in a ledger
fca8c96  Replace the vanilla front end with a React SPA
4dc45c7  Add CLAUDE.md handoff notes and a spec gap analysis
```
