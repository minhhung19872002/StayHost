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
  `admin@staylio.vn` — địa chỉ đó chỉ nhận được thư nếu hòm thư ấy thật sự tồn tại
  trên tên miền, nếu không thì **mã gửi vào hư không**.
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
`https://staylio.vn` (không dấu `/` ở cuối). Kiểm tra sau khi bật:

```bash
curl -s https://staylio.vn/api/account/external/config
```

Trả về `googleClientId` khác `null` là máy chủ đã nhận cấu hình; còn `null` nghĩa là
biến chưa tới được container — soát lại chính tả tên biến trong env file.

## 2.3. Dịch tự động — LibreTranslate tự host

`docs/01 TĐ-03, TN-06`. Dịch mô tả tin đăng và tin nhắn sang ngôn ngữ khác. Stack đã
kèm sẵn một container **LibreTranslate** (mã nguồn mở, **miễn phí, không giới hạn, không
cần khoá API** — engine chạy ngay trên máy chủ), và `web` mặc định trỏ vào nó, nên **không
cần làm gì thêm**: deploy xong là nút "Dịch" tự hiện.

- Lần khởi động đầu, `libretranslate` **tải model** (`en,vi,zh,ko,ja,fr`) mất **vài phút**;
  model lưu ở volume `lt-models` nên các lần sau nhanh. Kiểm tra:
  ```bash
  docker compose -p stayhost -f docker-compose.prod.yml exec libretranslate \
    python -c "import urllib.request,json; print([x['code'] for x in json.load(urllib.request.urlopen('http://localhost:5000/languages'))])"
  curl -s https://staylio.vn/api/translate/config   # enabled:true là đã bật
  ```
- Kết quả dịch được **cache trong DB** (`translation_caches`) nên mỗi câu chỉ dịch một lần.
- Thêm/bớt ngôn ngữ: đặt `TRANSLATE_LANGS` trong env file (mặc định `en,vi,zh,ko,ja,fr`).
- **Tắt** dịch: đặt `TRANSLATION_PROVIDER=` (để trống) trong env file rồi khởi động lại —
  nút "Dịch" biến mất, mọi thứ khác giữ nguyên. Có thể dừng luôn container `libretranslate`
  để tiết kiệm RAM.
- Muốn đổi sang dịch vụ trả phí chất lượng cao hơn (Google/DeepL): đặt
  `TRANSLATION_PROVIDER=google` + `TRANSLATION_API_KEY=<khoá>` (adapter Google đã có sẵn).

> **RAM:** LibreTranslate cần ~1–2GB. Nếu VPS eo hẹp, giảm `TRANSLATE_LANGS` xuống còn
> `en,vi` hoặc tắt hẳn như trên.

## 2.4. Bật nhận chuyển khoản VietQR

`docs/07 §2.3`. Mặc định **tắt**: chưa khai số tài khoản thì phương thức không xuất hiện
ở trang thanh toán, y như nút đăng nhập của nhà cung cấp chưa cấu hình. Bật bằng cách
thêm bốn dòng vào `~/deploy/stayhost.env` rồi khởi động lại `web`:

```bash
cat >> ~/deploy/stayhost.env <<'EOF'
BANK_BIN=970418
BANK_NAME=BIDV
BANK_ACCOUNT_NUMBER=<số tài khoản>
BANK_ACCOUNT_NAME=<TEN CHU TAI KHOAN KHONG DAU>
EOF

cd ~/actions-runner/_work/StayHost/StayHost
docker compose -p stayhost -f docker-compose.prod.yml --env-file ~/deploy/stayhost.env up -d web

curl -s https://staylio.vn/api/payment-methods/catalogue | grep -o vietqr
```

- `BANK_BIN` là mã NAPAS của ngân hàng, tra ở vietqr.io/danh-sach-ngan-hang
  (BIDV `970418`, MB `970422`, Vietcombank `970436`, Techcombank `970407`).
- `BANK_ACCOUNT_NAME` **viết hoa không dấu**. Tên này không nằm trong mã QR — ứng dụng
  ngân hàng tự tra ra từ số tài khoản — nó chỉ hiện trên trang cho khách đối chiếu.
- Bốn giá trị này **không phải bí mật**, nhưng chúng quyết định tiền khách rơi vào đâu,
  nên để trong env file chứ không commit vào repo.

**Bật xong thì có người phải trực.** Tiền về không tự báo: mỗi ngày một lần, người có
quyền `Finance` vào trang quản trị, mục *Chuyển khoản ngân hàng*, dán sao kê. Đơn khớp
được xác nhận ngay; các dòng còn lại nằm ở hàng chờ tới khi có người ghi đã xử lý thế
nào. **Đơn chỉ giữ chỗ 2 giờ** — sao kê nhập muộn hơn thì đơn đã nhả chỗ và tiền về
thành phán quyết "tiền về muộn" phải xử lý tay.

> **`docs/07 §1`:** sổ ghi các khoản này là "tiền sàn giữ hộ khách". Điều đó chỉ đúng
> nếu tài khoản là **của pháp nhân**. Dùng tài khoản cá nhân thì sổ và thực tế lệch
> nhau, và `docs/07 §13` (phương án pháp lý A/B/C) vẫn chưa chốt. Bật công tắc này
> không chốt thay.

**Tắt lại:** xoá dòng `BANK_ACCOUNT_NUMBER` (hoặc để trống) rồi `up -d web`. Đơn đang
chờ chuyển khoản vẫn tự hết hạn và nhả chỗ như thường.

## 2.5. Bật cổng thanh toán thật (VNPay / MoMo / ZaloPay)

`docs/07 §13` phương án A, chi tiết ở `§15.3`. Mặc định **tắt cả ba**: cổng nào chưa
có khoá thì phương thức đó vẫn chạy bằng bản giả lập trong mã, y như trước. Bật một
cổng là phương thức của nó **thôi không bị trừ tiền trên máy chủ sàn nữa** — khách rời
sang trang của cổng và tiền chạy thật.

**Cần trước khi bật:** giấy phép kinh doanh + hợp đồng với cổng. Phí khoảng
**1.1–1.65%** mỗi giao dịch với VNPay, **~2%** với MoMo, **1.5–2%** với ZaloPay. Sandbox
thì miễn phí và không cần giấy tờ.

> **Đăng ký sandbox VNPay:** `https://sandbox.vnpayment.vn/devreg/`. Đường dẫn `devreg`
> là một phần của địa chỉ — gõ mỗi `sandbox.vnpayment.vn` sẽ ra trang 404 của VNPAY.
> Form hỏi tên website, địa chỉ URL, email, mật khẩu; `TmnCode` và `HashSecret` được
> gửi về email đó, và cổng quản trị sandbox ở `https://sandbox.vnpayment.vn/merchantv2/`.

```bash
cat >> ~/deploy/stayhost.env <<'EOF'
# Địa chỉ cổng gọi ngược về. Phải là tên miền thật, không phải localhost.
PSP_PUBLIC_URL=https://staylio.vn

# OnePay — cổng thứ hai cho ô "Thẻ tín dụng / ghi nợ" (thẻ quốc tế)
# Đặt PSP_METHODS_CARD=
PSP_METHODS_NAPAS=onepay thì ô thẻ đi OnePay thay vì VNPay; bỏ trống thì
# giữ nguyên VNPay. Sandbox công khai của họ là TESTONEPAY/6BEB2546.
# ApiUser/ApiPassword là tài khoản OnePay cấp RIÊNG, không phải hash secret —
# thiếu nó thì vẫn thu tiền được, nhưng docs/07 §5 (tự hỏi lại) và §10 (hoàn
# tiền) im lặng và ghi log cảnh báo.
ONEPAY_MERCHANT=<mã merchant>
ONEPAY_ACCESS_CODE=<access code>
ONEPAY_HASH_SECRET=<chuỗi bí mật, 32 ký tự hex>
ONEPAY_PAY_URL=https://onepay.vn/vpcpay/vpcpay.op
ONEPAY_DOMESTIC_PAY_URL=https://onepay.vn/onecomm-pay/vpc.op
ONEPAY_API_URL=https://onepay.vn/vpcpay/Vpcdps.op
ONEPAY_DOMESTIC_API_URL=https://onepay.vn/onecomm-pay/Vpcdps.op
ONEPAY_API_USER=
ONEPAY_API_PASSWORD=
PSP_METHODS_CARD=

# VNPay — lo cả ô "Thẻ tín dụng / ghi nợ" lẫn ô "Thẻ ATM nội địa"
VNPAY_TMN_CODE=<mã website>
VNPAY_HASH_SECRET=<chuỗi bí mật>
VNPAY_PAY_URL=https://vnpayment.vn/paymentv2/vpcpay.html
VNPAY_API_URL=https://vnpayment.vn/merchant_webapi/api/transaction

# MoMo
MOMO_PARTNER_CODE=<...>
MOMO_ACCESS_KEY=<...>
MOMO_SECRET_KEY=<...>
MOMO_ENDPOINT=https://payment.momo.vn/v2/gateway/api

# ZaloPay
ZALOPAY_APP_ID=<...>
ZALOPAY_KEY1=<...>
ZALOPAY_KEY2=<...>
ZALOPAY_ENDPOINT=https://openapi.zalopay.vn/v2
EOF

cd ~/actions-runner/_work/StayHost/StayHost
docker compose -p stayhost -f docker-compose.prod.yml --env-file ~/deploy/stayhost.env up -d web

# Ô nào đã có cổng thật thì trả "live": true
curl -s https://staylio.vn/api/payment-methods/catalogue | python3 -m json.tool
```

- **VNPay sandbox không có thẻ quốc tế để thử.** Thẻ test họ công bố là NCB, thẻ nội
  địa; ô "Thẻ tín dụng / ghi nợ" mở được trang thẻ quốc tế của họ nhưng không có số
  thẻ nào trả xong được (nhập vào bị chặn sau 3 lần). Muốn nghiệm thu nhánh Visa
  trước khi ký hợp đồng thì dùng **OnePay**: sandbox công khai của họ cho thẻ Visa
  `4005 5500 0000 0001` (12/27, CSC 100) và trả về `vpc_TxnResponseCode=0`.
  Chạy `python scripts/onepay_acceptance.py` với `PSP_METHODS_CARD=onepay`.
- **OnePay có hai cổng, cùng merchant cùng khoá, chỉ khác địa chỉ.** Thẻ quốc tế đi
  `vpcpay/vpcpay.op`, thẻ nội địa đi `onecomm-pay/vpc.op`. Gửi nhầm thì **không có gì
  trông sai cả**: chữ ký vẫn đúng, OnePay vẫn nhận, khách chỉ đơn giản gặp một biểu
  mẫu mà thẻ của họ không điền được. `PSP_METHODS_NAPAS=onepay` để chuyển ô nội địa.
- **Nhánh nội địa của OnePay chưa trả xong lần nào trong nghiệm thu.** Đơn tới đúng
  cổng, OnePay mở đúng danh sách 28 ngân hàng NAPAS với đúng số tiền — nhưng sandbox
  của họ không công bố thẻ ATM thử nghiệm nào, nên bước cuối phải bấm tay. VNPay thì
  nhánh này **đã** chạy xong 14/14 với thẻ NCB.
- **OnePay cho biết 4 số cuối của thẻ ngay trong giao dịch thường** (`vpc_CardNum`
  dạng `400555xxxxxx0001`), không cần token hoá — khác VNPay, nơi `docs/07 §14.2`
  để lại `payments.CardLast4` rỗng trừ khi khách bấm lưu thẻ.
- **Ba `*_ENDPOINT`/`*_URL` mặc định là sandbox**, cố ý: một server cấu hình dở dang
  không được phép chuyển tiền thật vì quên một dòng. Lên prod phải đặt cả ba.
- `VNPAY_HASH_SECRET`, `MOMO_SECRET_KEY`, `ZALOPAY_KEY1/KEY2` **là bí mật thật** — ai có
  chúng thì ký được callback giả. Chỉ để trong env file trên máy chủ, quyền `600`.
- Khai **địa chỉ IPN** ở trang quản trị của từng cổng:
  `…/api/payments/vnpay/ipn`, `…/api/payments/momo/ipn`, `…/api/payments/zalopay/callback`.
  Không khai cũng vẫn chạy — `PspSweeper` mỗi phút tự hỏi lại cổng (`docs/07 §5`) —
  nhưng đơn sẽ được xác nhận chậm hơn một phút.

**Tắt lại:** xoá khoá của cổng đó rồi `up -d web`. Phương thức quay về bản giả lập; các
phiên đang chờ tự hết hạn sau 15 phút và nhả chỗ, chưa có bút toán nào phải đảo.

## 2.6. Bật chuyển tiền cho chủ nhà

`docs/07 §13`, `§15.4`. Cổng thanh toán trả **toàn bộ** tiền đơn về tài khoản StayHost.
Phần của chủ nhà — phần lớn nhất — sàn phải tự chuyển đi. Không có API nào cho việc đó:
mỗi ngày một người tải file, đưa lên internet banking, rồi quay lại nói ngân hàng có
thực hiện hay không.

**Bắt buộc trước khi nhận tiền thật:** khoá mã hoá số tài khoản chủ nhà (`§14.3`).
Không có nó thì số tài khoản **không được lưu**, và sàn thu tiền của khách mà không có
gì để chuyển cho ai.

```bash
# 32 byte. Giữ cùng chỗ với bản sao lưu cơ sở dữ liệu.
openssl rand -base64 32

cat >> ~/deploy/stayhost.env <<'EOF'
PAYOUTS_ACCOUNT_KEY=<chuỗi vừa sinh>
EOF

cd ~/actions-runner/_work/StayHost/StayHost
docker compose -p stayhost -f docker-compose.prod.yml --env-file ~/deploy/stayhost.env up -d web
```

> **Đổi khoá này là mọi số tài khoản đã lưu không đọc được nữa**, và mọi chủ nhà thành
> không chuyển tiền được cho tới khi họ khai lại. Nó không xoay vòng được, hãy coi nó
> như một phần của cơ sở dữ liệu.

**Việc hằng ngày của người có quyền `Finance`:**

1. Trang quản trị → *Chuyển tiền cho chủ nhà* → **Tải file chuyển tiền (.csv)**.
2. Mở file, chuyển sáu cột (`SoTaiKhoan`, `TenNguoiHuong`, `NganHang`, `SoTien`,
   `NoiDung`) sang mẫu chuyển khoản hàng loạt của ngân hàng đang dùng, rồi tải lên
   internet banking. **Giữ nguyên `NoiDung`** — đó là mã đối chiếu khi chủ nhà hỏi.
3. Ngân hàng thực hiện xong thì quay lại bấm **Đã chuyển** trên từng lệnh. *Đây mới là
   lúc sổ sách ghi "đã trả chủ nhà"* — trước đó tiền vẫn là tiền sàn đang giữ hộ.
4. Lệnh nào ngân hàng từ chối thì bấm **Bị từ chối**: đơn quay lại hàng chờ theo thang
   thử lại 1/3/7 ngày của `§12.5`, và chủ nhà được báo.

File cố ý **không** theo mẫu riêng của một ngân hàng nào. Đoán sai mẫu thì file vẫn tải
lên được và trả tiền cho nhầm người; sáu cột này là phần chung của mọi mẫu, người vận
hành ánh xạ một lần.

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
