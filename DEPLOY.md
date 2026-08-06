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
| `~/deploy/stayhost.env` | Mật khẩu Postgres + image mặc định. `chmod 600`, **không nằm trong git**. |
| `~/actions-runner/` | GitHub Actions self-hosted runner, chạy như systemd service. |
| `~/actions-runner/_work/StayHost/StayHost/` | Bản checkout mà job deploy dùng để chạy compose. |
| `/etc/nginx/sites-enabled/stayhost` | Reverse proxy + TLS. |
| Docker volume `stayhost_pgdata` | Dữ liệu Postgres. |
| Docker volume `stayhost_uploads` | Ảnh tin đăng (`wwwroot/uploads`). |

Hai volume nói trên tồn tại độc lập với container, nên deploy lại **không** mất dữ liệu.

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

### Sao lưu cơ sở dữ liệu

```bash
docker exec stayhost-db pg_dump -U stayhost stayhost | gzip > ~/backup-$(date +%F).sql.gz
```

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
