---
name: staylio-deploy-verify
description: Use this skill when deploying Staylio / StayHost (staylio.vn) to production, when checking whether a pushed commit actually reached the VPS, when a GitHub Actions self-hosted runner deploy looks stuck or missing, or when editing Caddy / TLS / domain config on bluestar01. Covers the push → CI/CD → container-sha → live-site verification chain, and the shared-proxy rules that make a careless reload take down four other projects. Trigger phrases in Vietnamese - "deploy lên prod", "đã deploy chưa", "kiểm tra prod", "sao chưa thấy thay đổi", "thêm tên miền".
---

# Deploy Staylio lên prod và kiểm chứng

## Khi nào dùng

- Vừa push và muốn biết prod **thật sự** đã chạy mã đó chưa.
- Prod trông như còn bản cũ, hoặc GitHub Actions không thấy run nào.
- Cần đụng vào Caddy trên `bluestar01` (thêm tên miền, redirect, TLS).

## Sự thật về máy chủ — đọc trước khi gõ lệnh

| | |
|---|---|
| Host | `bluestar01` — `14.225.83.93`, đăng nhập `hung@` bằng SSH key |
| Tên miền | `staylio.vn` (+ `www` → 301 về apex). `staylio.bluestar.com.vn` **đã gỡ** |
| Container app | `stayhost-web` (tên container giữ tên cũ, cố ý) |
| CI/CD | `.github/workflows/ci-cd.yml`, chạy trên **self-hosted runner** đặt ngay trên VPS |
| Env prod | `~/deploy/stayhost.env` — bí mật nằm đây, **không** trong repo |

**`hung` không nằm trong sudoers**, và nhóm `sudo` trên máy rỗng — máy được quản trị
bằng root trực tiếp mà khách không giữ mật khẩu. Tài liệu nào bảo `sudo` thì bước đó
chưa từng chạy được. Nhưng `hung` **có** nhóm `docker`, mà nhóm docker là root trá
hình, nên hầu hết việc vẫn làm được qua `docker exec`.

## Quy trình kiểm chứng deploy

Chạy theo đúng thứ tự. Bước 3 là bước duy nhất nói sự thật.

### 1. Push

```bash
git push origin main
```

### 2. Xem CI/CD — nhưng đừng vội kết luận

```bash
gh run list --workflow=ci-cd.yml --limit 3
gh api "repos/minhhung19872002/Staylio/actions/runs?head_sha=$(git rev-parse HEAD)" --jq .total_count
```

`total_count: 0` **không có nghĩa là GitHub bỏ qua push.** Ngày 26/08/2026 nó trả 0
hai lần liên tiếp rồi một tiếng sau mới tạo run — cả hai `event=push`, cả hai
`success`. Kích thủ công lúc đó làm một commit deploy **hai lần**. Xem `CLAUDE.md §4`.

Đang chạy thì chờ:

```bash
gh run watch <run-id> --exit-status --interval 20
```

### 3. Prod đang chạy sha nào — đây mới là câu trả lời

```bash
ssh hung@14.225.83.93 'docker inspect stayhost-web --format "{{.Config.Image}}"; \
  docker ps --filter name=stayhost-web --format "{{.Status}}"'
```

Image phải là `ghcr.io/minhhung19872002/stayhost:sha-<40 ký tự của commit>` và trạng
thái phải `healthy`. Sha khớp `git rev-parse HEAD` là xong; lệch thì mới xét bước 4.

### 4. Chỉ khi sha lệch và đã đợi đủ lâu

```bash
gh workflow run ci-cd.yml --ref main
```

### 5. Kiểm chứng trên tên miền thật

```bash
bash .claude/skills/staylio-deploy-verify/scripts/verify-prod.sh
```

Kiểm **nội dung**, không kiểm mã 200. Một project khác từng chiếm cổng và trả 200 cho
mọi đường dẫn, khiến cả bước "chờ server sẵn sàng" lẫn script nghiệm thu tưởng đang
nói chuyện với Staylio. Đường đúng để nhận diện là **`/api/meta`** (không phải
`/api/listings/meta` — 404 vĩnh viễn).

### 6. Thứ chỉ trình duyệt thấy

`curl` không chạy JavaScript, nên **canonical, JSON-LD và mọi thẻ do `lib/seo.js` đặt
sau khi trang tải xong đều không hiện trong `curl`**. Muốn kiểm thì mở bằng
chrome-devtools MCP rồi đọc `document.querySelector('link[rel="canonical"]')`.

Ngược lại, thẻ do **máy chủ** sinh (`ShellSeo.cs`, khối giữa `<!--seo:start-->` và
`<!--seo:end-->`) thì `curl` thấy — và đó chính là bản mà Facebook/Zalo/Messenger đọc,
vì chúng không chạy JS.

## Đụng vào Caddy: đọc kỹ

Cổng 443 do **một container Caddy dùng chung** giữ, phục vụ cùng lúc `bluedental`,
`blueidea`, `foodsafe`, `starlab` và Staylio. Host **không có** `/etc/nginx` lẫn
`/etc/letsencrypt`. Chạy `deploy/setup-nginx.sh` sẽ giành cổng 443 và **làm sập cả
năm dự án**.

```bash
# 1. Sao lưu, luôn luôn
ssh hung@14.225.83.93 'cp ~/proxy/sites/stayhost.caddy ~/proxy/sites/stayhost.caddy.bak-$(date +%Y%m%d-%H%M)'

# 2. Sửa file, rồi VALIDATE trước khi reload
ssh hung@14.225.83.93 'docker exec proxy-caddy caddy validate --config /etc/caddy/Caddyfile'

# 3. reload, KHÔNG restart
ssh hung@14.225.83.93 'docker exec proxy-caddy caddy reload --config /etc/caddy/Caddyfile'

# 4. Bốn dự án kia còn sống không
for u in https://bluedental.bluestar.com.vn https://blueidea.bluestar.com.vn \
         https://attp.bluestar.com.vn https://uat.tihelab.vn; do
  printf "%-42s -> %s\n" "$u" "$(curl -s -o /dev/null -m 15 -w '%{http_code}' $u)"
done
```

## Pitfalls

- **Đừng bật container `stayhost-web` cùng lúc với `dotnet run` trên máy lập trình.**
  Nó chạy bản Release cũ trên **cùng một database** và cũng chạy vòng quét mỗi phút.
  Đã mất một tiếng mới tin nổi chuyện đó. `docker ps` trước khi kết luận "code không chạy".
- **`TaskStop` chỉ giết `dotnet run`**; tiến trình con `StayHost.Web.exe` sống tiếp và
  khoá DLL, gây `MSB3027` ở lần build sau. Dọn bằng
  `Get-Process StayHost.Web | Stop-Process -Force`.
- **`pgrep -f "Runner.Listener"` tự khớp chính nó** khi chạy qua `sh -c`. Dùng ngoặc
  vuông: `pgrep -f "[R]unner.Listener"`. Cùng bẫy đó, `pkill -f` gõ qua SSH **tự cắt
  phiên SSH của mình**.
- **Seeder chỉ chạy trên DB trắng.** Đổi email/tên trong `DbSeeder` xong deploy thì DB
  prod **không đổi theo** — phải `UPDATE` tay.
- **Bí mật không nằm trong repo.** Muốn thêm biến thì sửa `~/deploy/stayhost.env` rồi
  `docker compose up -d`; kiểm bằng `docker exec stayhost-web printenv <TÊN>`.
- **Repo đã đổi tên `StayHost` → `Staylio` (27/08/2026), nhưng runner thì chưa.**
  `~/actions-runner/.runner` trên VPS vẫn ghi URL cũ, và nó chạy được **chỉ nhờ
  GitHub tự chuyển hướng tên cũ sang tên mới**. Chuyển hướng đó mất hiệu lực ngay khi
  có ai tạo một repo mới trùng tên `StayHost` dưới cùng tài khoản — lúc đó runner trỏ
  vào một repo lạ và deploy im lặng ngừng chạy. Đăng ký lại runner khi có dịp
  (`deploy/install-runner.sh`, cần token từ Settings → Actions → Runners → New).
  Tên **container** (`stayhost-web`, `stayhost-db`), tên **package GHCR**
  (`ghcr.io/minhhung19872002/stayhost`) và **namespace C#** thì giữ nguyên có chủ ý —
  đổi chúng là đổi thứ mà database, image cũ và lệnh rollback đang trỏ tới.

## Tài khoản quản trị prod

`admin@staylio.vn`. Mật khẩu **không nằm trong repo** và đã đổi tay ít nhất hai lần.
Quên thì đặt lại bằng chính bộ băm của app — PBKDF2-SHA256, 210.000 vòng, salt 16
byte, cả hai lưu base64 (`PasswordHasher.cs`):

```bash
python .claude/skills/staylio-deploy-verify/scripts/reset-admin-password.py "<mat khau moi>"
```

Rồi dán câu `UPDATE` nó in ra vào `docker exec stayhost-db psql -U stayhost -d stayhost`.

`ADMIN_REQUIRE_2FA=false` là **ngoại lệ khẩn cấp** vì `EMAIL_HOST` chưa cấu hình nên
máy chủ không gửi được mã. Bật lại **chỉ sau khi** email chạy — bật trước thì chính
mình cũng không vào được.
