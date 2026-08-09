# Triển khai & CI/CD

Máy chủ hiện tại: VPS Ubuntu 24.04, 1 vCPU / 2 GB RAM, IP `45.119.215.96`.

## 1. Kiến trúc

```
push main  ──►  GitHub Actions (runner của GitHub)
                  ├── job "test"    dotnet build/test + npm ci/lint/build
                  └── job "image"   docker build → đẩy lên ghcr.io/minhhung19872002/stayhost
                                             │
                  job "deploy" (self-hosted runner chạy trên VPS)
                                             ▼
                        docker compose pull + up -d  →  kiểm tra /health
                                             ▼
                        Nginx (443, TLS) ──► 127.0.0.1:8090 ──► container web
                                                                      │
                                                                  container db
```

Build nặng chạy trên runner miễn phí của GitHub; VPS 1 vCPU chỉ kéo image về và khởi
động lại, nên mỗi lần deploy chỉ mất vài chục giây và không có nguy cơ hết RAM.

Self-hosted runner **gọi ra ngoài** để nhận việc, nên không cần mở thêm cổng vào và
không cần lưu khoá SSH trong GitHub Secrets. Việc đăng nhập GHCR dùng `GITHUB_TOKEN`
tự sinh theo từng lần chạy.

## 2. Bố trí trên máy chủ

| Đường dẫn | Nội dung |
|---|---|
| `~/deploy/stayhost.env` | Mật khẩu Postgres, image đang chạy, SMTP và địa chỉ quản trị. `chmod 600`, **không nằm trong git**. Dòng `STAYHOST_IMAGE` do job deploy ghi lại sau mỗi lần lên khoẻ mạnh, nên nó luôn đúng bằng bản đang chạy. |
| `~/actions-runner/` | GitHub Actions self-hosted runner, chạy như systemd service. |
| `~/actions-runner/_work/StayHost/StayHost/` | Bản checkout mà job deploy dùng để chạy compose. |
| `/etc/nginx/sites-enabled/stayhost` | Reverse proxy + TLS. |
| Docker volume `stayhost_pgdata` | Dữ liệu Postgres. |
| Docker volume `stayhost_uploads` | Ảnh tin đăng (`wwwroot/uploads`). |
| Docker volume `stayhost_identity` | Ảnh giấy tờ tuỳ thân (`protected/identity`). Cố tình nằm **ngoài** `wwwroot` để static files không phát tán được — `docs/08 §4`. |

Ba volume nói trên tồn tại độc lập với container, nên deploy lại **không** mất dữ liệu.

## 2.1. Đăng nhập trang quản trị

`docs/08 §3` bắt tài khoản quản trị có bảo mật 2 lớp, không ngoại lệ, nên đăng nhập
luôn đi qua **mã 6 số gửi tới email của chính tài khoản đó**. Hai biến trong
`~/deploy/stayhost.env` quyết định mã có tới nơi hay không:

- `ADMIN_EMAIL` — hòm thư thật của người quản trị. Bỏ trống thì tài khoản vẫn là
  `admin@stayhost.vn`, một tên miền không ai sở hữu, nên **mã gửi vào hư không**.
  Đặt giá trị rồi khởi động lại là tài khoản chuyển sang địa chỉ đó, và từ đó
  đăng nhập cũng bằng địa chỉ đó.
- `EMAIL_HOST` và bạn bè — chưa đặt thì **không có thư nào rời hàng đợi**. Với
  Gmail cần *App Password* 16 ký tự ở `myaccount.google.com/apppasswords` (phải
  bật xác minh 2 bước trước mới thấy mục này); mật khẩu tài khoản thường không
  dùng được, và `EMAIL_FROM` phải trùng địa chỉ đã xác thực.

Khi chưa cấu hình SMTP, mã vẫn đọc được từ hàng đợi:

```bash
docker exec stayhost-db psql -U stayhost -d stayhost -t -A \
  -c "select \"Body\" from email_messages order by \"Id\" desc limit 1;"
```

### Tắt tạm bảo mật 2 lớp — `ADMIN_REQUIRE_2FA=false`

`docs/08 §3` nói "không có ngoại lệ", nên đây **là** ngoại lệ và cần hiểu nó đánh
đổi cái gì: tắt đi thì **mật khẩu quản trị là thứ duy nhất** đứng giữa người lạ và
hồ sơ, giấy tờ tuỳ thân, tiền của người dùng thật. Chỉ dùng khi máy chủ chưa gửi
được email, và trong lúc đó:

- **Đừng để mật khẩu quản trị ở chỗ công khai.** Hộp "Tài khoản dùng thử" trên màn
  hình đăng nhập cố tình chỉ liệt kê khách và chủ nhà — tài khoản quản trị không
  nằm ở đó, và đừng thêm vào.
- Đổi mật khẩu quản trị sang một chuỗi không đoán được, đừng để `stayhost123`.
- Bật lại (`ADMIN_REQUIRE_2FA=true`) ngay khi `EMAIL_HOST` chạy. Log khởi động in
  rõ trạng thái mỗi lần lên, để không ai quên nó đang tắt.

## 2.2. Bật đăng nhập / đăng ký bằng Google, Apple, Facebook

`docs/01 TK-02`. Toàn bộ luồng đã dựng sẵn trong mã nguồn — kiểm chữ ký token, tạo
tài khoản mới ở lần bấm đầu tiên, ghép vào tài khoản cũ, bỏ liên kết. Việc còn lại
chỉ là **điền mã của nhà cung cấp** vào `~/deploy/stayhost.env` rồi khởi động lại;
xem khối `GOOGLE_CLIENT_ID` trong `deploy/stayhost.env.example` để biết lấy ở đâu.

Bỏ trống thì nút của nhà cung cấp đó **không hiện** trên hộp đăng nhập — cố ý như
vậy, thà thiếu nút còn hơn có nút bấm vào không chạy. Bỏ trống cả ba thì cả cụm nút
lẫn gạch ngang "hoặc" đều biến mất, hộp đăng nhập trở lại chỉ có email và mật khẩu.

Với máy chủ hiện tại, "Authorised JavaScript origins" của Google phải có đúng
`https://staylio.bluestar.com.vn` (không dấu `/` ở cuối). Kiểm tra sau khi bật:

```bash
curl -s https://staylio.bluestar.com.vn/api/account/external/config
```

Trả về `googleClientId` khác `null` là máy chủ đã nhận cấu hình; còn `null` nghĩa là
biến chưa tới được container — soát lại chính tả tên biến trong env file.

## 3. Các lệnh vận hành

Tất cả chạy với user `hung`:

```bash
cd ~/actions-runner/_work/StayHost/StayHost
alias shc='docker compose -p stayhost -f docker-compose.prod.yml --env-file ~/deploy/stayhost.env'

shc ps                 # trạng thái
shc logs -f web        # log ứng dụng
shc restart web
shc down               # dừng (giữ nguyên volume)
```

### Deploy tay / rollback

Mọi commit trên `main` đều có image gắn thẻ `sha-<commit>`. Quay về bản cũ:

```bash
STAYHOST_IMAGE=ghcr.io/minhhung19872002/stayhost:sha-<commit đầy đủ> shc up -d
```

Hoặc chạy lại workflow của commit đó trong tab Actions trên GitHub.

Rollback bằng tay **không** sửa `~/deploy/stayhost.env` — chỉ job deploy mới ghi vào
đó. Nên sau khi lùi bản, file vẫn đang trỏ bản mới, và một lệnh `shc up -d` trần sau
đó sẽ kéo prod trở lại bản mới ấy. Muốn giữ bản đã lùi thì sửa luôn dòng
`STAYHOST_IMAGE` trong file cho khớp. Lùi bằng cách chạy lại workflow cũ thì không
vướng chuyện này, vì job deploy ghi lại file.

### Sao lưu cơ sở dữ liệu

Timer `stayhost-backup.timer` chạy **03:30 mỗi đêm** (lệch ngẫu nhiên trong 15 phút),
ghi vào `~/backups/stayhost-<ngày>-<giờ>.sql.gz` và **giữ 7 ngày**. Dump được viết
ra tên `.partial` trước rồi mới đổi tên, nên không bao giờ có file cụt trông như bản
sao lưu tốt. Nếu dump nhỏ bất thường (<10 KB) thì script vứt bỏ và báo lỗi.

```bash
systemctl list-timers stayhost-backup.timer   # lần chạy kế tiếp
journalctl -u stayhost-backup -n 20           # kết quả gần nhất
sudo systemctl start stayhost-backup          # chạy ngay một bản
ls -lh ~/backups
```

**Phục hồi** — thử vào DB tạm trước khi ghi đè bản thật:

```bash
gunzip -c ~/backups/stayhost-<...>.sql.gz | docker exec -i stayhost-db psql -U stayhost -d stayhost
```

Dump có `--clean --if-exists` nên nó tự xoá bảng cũ trước khi dựng lại.

> Backup nằm **cùng ổ đĩa** với dữ liệu gốc. Nó cứu được lỗi xoá nhầm hoặc migration
> hỏng, **không** cứu được hỏng ổ hay mất VPS. Muốn an toàn thật thì phải đẩy bản dump
> sang nơi khác (S3, máy khác, Google Drive…).

### Chứng chỉ TLS

Certbot tự gia hạn qua timer `certbot.timer`. Kiểm tra: `sudo certbot renew --dry-run`.

## 4. Dựng lại từ đầu

Ba script dưới đây cần `sudo`, nên phải chạy trong terminal SSH thật (chế độ `!` của
Claude Code không có TTY để nhập mật khẩu).

1. `sudo bash deploy/setup-packages.sh` — Docker, Nginx, Certbot, mở firewall 80/443.
2. Tạo `~/deploy/stayhost.env` theo mẫu `deploy/stayhost.env.example`, rồi `chmod 600`.
3. `bash deploy/install-runner.sh <REGISTRATION_TOKEN>` — lấy token tại
   *Settings → Actions → Runners → New self-hosted runner* của repo, hoặc
   `gh api -X POST /repos/minhhung19872002/StayHost/actions/runners/registration-token --jq .token`.
   Runner phải có nhãn **`stayhost-vps`**. Rồi `sudo bash deploy/setup-runner-service.sh`
   để nó chạy như systemd service và sống qua reboot.
4. Push lên `main` (hoặc bấm *Run workflow*) để CI/CD chạy lần đầu và dựng stack.
5. `sudo bash deploy/setup-nginx.sh <tên miền> <email>` — reverse proxy + TLS.
   Chạy sau bước 4 vì Certbot cần site trả lời được trên cổng 80.
