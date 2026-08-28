# 07 — Thanh toán

Chi tiết hoá `01-DANH-MUC-CHUC-NANG.md` mục TC và ĐP-05 → ĐP-09.
Đọc kèm `03-QUY-TAC-NGHIEP-VU.md` §5 (dòng tiền) và §1 (cách tính giá).

---

## 1. Nguyên tắc gốc: sàn giữ tiền hộ

Tiền của khách **luôn đi qua sàn**, không đi thẳng cho chủ nhà. Sàn cầm tiền từ lúc đơn được xác nhận cho tới 24 giờ sau khi khách nhận phòng.

Đây không phải lựa chọn kỹ thuật mà là điều kiện tồn tại của mô hình. Không giữ tiền thì:
- Không hoàn tiền được khi khách huỷ → chính sách huỷ vô nghĩa
- Không có gì ràng buộc chủ nhà giữ lời → khách không dám đặt
- Không có Staylio Shield, không có phân xử tranh chấp
- Không thu được phí dịch vụ

Hệ quả bắt buộc:
1. **Không nhận tiền mặt, không nhận chuyển khoản thủ công từ khách.** Khách phải trả bằng phương thức sàn thu được ngay và hoàn được ngay.
2. **Giao dịch ngoài sàn là vi phạm điều khoản.** Chủ nhà rủ khách trả riêng → cảnh cáo, tái phạm thì gỡ tin đăng. Tin nhắn phải tự động phát hiện và cảnh báo.
3. Chủ nhà nhận tiền bằng **kênh khác** với kênh khách trả — chuyển khoản ngân hàng là chính.

---

## 2. Khách trả bằng gì

### 2.1. Bắt buộc có (P0)

| Nhóm | Cụ thể |
|---|---|
| Thẻ quốc tế | **Visa**, **Mastercard**, JCB, American Express |
| Thẻ nội địa | Thẻ ATM/ghi nợ nội địa qua NAPAS |
| Ví điện tử VN | MoMo, ZaloPay |
| Số dư trong tài khoản | Số dư khuyến mãi, thẻ quà tặng |

### 2.2. Nên có (P1)

Apple Pay, Google Pay (giảm ma sát rất mạnh trên điện thoại), thẻ trả trước, thẻ ghi nợ xử lý được như thẻ tín dụng.

### 2.3. Có thể thêm sau (P2)

PayPal (khách quốc tế), trả góp qua ngân hàng phát hành thẻ, VietQR.

### 2.4. Không nhận

Tiền mặt · chuyển khoản thủ công · tiền mã hoá · séc.

Khi khách hỏi những cách này, hệ thống phải giải thích lý do ngắn gọn: tiền được giữ để bảo vệ cả hai bên cho tới khi khách nhận phòng.

> **"Trả khi nhận phòng" từng nằm trong danh sách này. Khách đã đảo lại ngày
> 28/08/2026** — xem §2.5. Lý do ở trên vẫn đúng với bốn cách còn lại, và vẫn đúng
> về cái mà khách đánh đổi khi chọn §2.5.

### 2.5. Trả tại nơi ở, và đặt không cần tài khoản (khách chốt 28/08/2026)

Hai việc đi cùng nhau vì cùng trả lời một người: khách không muốn đưa thẻ cho một
trang web. Đây là cách Booking.com đang làm ở thị trường Việt Nam.

**Đặt không cần tài khoản.** Khách nhập họ tên, email và số điện thoại là đặt được.
Email là bắt buộc vì mã đơn gửi qua đó, và mã đơn là đường duy nhất quay lại đơn
sau này. Tra cứu bằng **mã đơn + chính email đó** ở `/dat-cho`; đăng nhập sau bằng
email ấy thì đơn tự về tài khoản.

Ba thứ gắn với tài khoản nên **từ chối có nêu tên**, không im lặng bỏ qua: số dư,
mã giảm giá (có giới hạn theo người), và ưu đãi riêng. Tin đăng bật yêu cầu bắt
buộc của `docs/01 ĐP-10` (phải có ảnh hồ sơ / phải xác minh danh tính) thì **không
đặt ẩn danh được** — chủ nhà bật hai công tắc đó chính là để khỏi phải quyết định.
Điều kiện *Đặt ngay* của `ĐP-03` thì khác: khách ẩn danh là khách chưa xác minh rõ
ràng nhất, nên đơn **chuyển thành yêu cầu đặt** để chủ nhà duyệt, đúng như mọi
khách chưa xác minh khác.

**Trả tại nơi ở.** Không phải "thêm một cách trả tiền" mà là **sàn đứng ra ngoài
luồng tiền**, và mọi hệ quả đều từ đó:

| | |
|---|---|
| Bút toán | **Không ghi gì cả** khi đặt và khi xác nhận. Tiền không đi qua Staylio thì sổ Staylio không được nói là có |
| Huỷ đơn | **Không có gì để hoàn.** Bản xem trước của `docs/01 CĐ-07` phải nói thẳng, thay vì báo một con số không ai trả. Chủ nhà huỷ thì khách vẫn nhận số dư bù đắp `docs/06` — đó là lời xin lỗi của sàn, không phải trả lại tiền của khách |
| Phí dịch vụ | Sàn **vẫn thu đủ hai phần phí**. Khách trả nguyên tổng tiền đã báo giá — đổi cách trả không đổi giá — nên chủ nhà đang giữ hộ cả 14% của sàn. Ghi vào `OwedToPlatform`, trừ vào lần chuyển tiền kế tiếp, **đúng cơ chế một vụ chargeback thua đang dùng** |
| Thuế | Chủ nhà thu cùng tiền mặt và **tự nộp**. Sàn chưa từng giữ khoản đó thì không thể là bên nộp hộ |
| Bật ở đâu | **Từng tin đăng, do chủ nhà tự bật.** Sàn không bao giờ tự bật: thứ bị đánh đổi là sự bảo vệ của chính chủ nhà, và khách không tới thì không có khoản nào để bù |
| Không đi cùng | Đặt cọc (`ĐP-06`), chia hoá đơn (`ĐP-07`), số dư và mã giảm giá — tất cả đều cần sàn ở giữa |
| Ghi nhận | Chủ nhà bấm **"Đã nhận tiền"**. Đó là lúc duy nhất đơn này chạm vào sổ sách, và cũng chỉ để tính phí |

**Rủi ro đã biết, ghi ra để không bị nhầm là bỏ sót:** `OwedToPlatform` chỉ thu
được khi chủ nhà còn đơn khác để trừ. Chủ nhà chỉ bán theo cách này thì sàn không
có đường thu phí. Đây là **cùng một rủi ro** mà đường chargeback đã mang sẵn, nên
nó dùng chung một cơ chế thay vì đẻ ra cơ chế thứ hai.

---

## 3. Kết hợp nhiều nguồn tiền trong một đơn

Một đơn có thể trả bằng nhiều nguồn. **Thứ tự trừ cố định, không được đảo:**

```
1. Mã giảm giá        → giảm vào tổng tiền, không phải nguồn tiền
2. Thẻ quà tặng       → trừ hết số dư thẻ trước
3. Số dư khuyến mãi   → trừ tiếp, ưu tiên khoản sắp hết hạn trước
4. Phần còn lại       → thu bằng một phương thức thanh toán duy nhất
```

Quy tắc:
- Chỉ được chọn **một** phương thức thanh toán bên ngoài cho phần còn lại. Không cho chia hai thẻ (trừ trường hợp chia hoá đơn ở §9).
- Nếu số dư đủ trả toàn bộ thì vẫn **bắt buộc gắn một phương thức dự phòng**, dùng khi có phát sinh — **đổi lịch**, và phần chênh khi khách đổi sang chỗ đắt hơn. **Không dùng để thu tiền bồi thường**: từ 17/08/2026 bồi thường là chuyện khách và chủ nhà tự thoả thuận bằng tiền mặt lúc trả phòng (`docs/06 §3.3`), sàn không thu.
- Bảng giá phải hiển thị từng nguồn thành dòng riêng, khách nhìn là hiểu tiền ở đâu ra.

---

## 4. Lưu và quản lý phương thức thanh toán

- Khách lưu được nhiều thẻ, đặt một cái làm mặc định.
- Chỉ hiển thị: thương hiệu, 4 số cuối, tháng/năm hết hạn. **Sàn không bao giờ lưu số thẻ đầy đủ và mã CVV** — xem §14.
- Xoá thẻ: chặn nếu thẻ đó đang gắn với đơn chưa hoàn tất hoặc còn lịch thu tự động. Báo rõ lý do và yêu cầu thay thẻ khác trước.
- Thẻ sắp hết hạn mà còn lịch thu tự động → nhắc khách cập nhật trước 14 ngày.
- Ai thêm thẻ thì chỉ người đó thấy. Chủ nhà không bao giờ thấy thông tin thẻ của khách.

---

## 5. Xác thực giao dịch (3-D Secure / OTP ngân hàng)

Thẻ Việt Nam và phần lớn thẻ quốc tế bắt buộc xác thực thêm bước OTP. Luồng nghiệp vụ:

1. Khách bấm xác nhận trả tiền
2. Hệ thống **giữ chỗ vẫn còn hiệu lực** trong lúc khách đi xác thực — không được để hết giờ giữ chỗ giữa chừng. Nếu thời gian giữ chỗ còn dưới 5 phút thì tự động gia hạn thêm 10 phút.
3. Khách chuyển sang trang xác thực của ngân hàng, nhập OTP
4. Quay lại sàn với một trong bốn kết quả:

| Kết quả | Xử lý |
|---|---|
| Thành công | Trừ tiền, xác nhận đơn |
| Sai OTP / hết hạn OTP | Cho thử lại tối đa 3 lần, giữ nguyên đơn |
| Khách đóng tab giữa chừng | Đơn giữ ở trạng thái chờ thanh toán tới hết thời gian giữ chỗ; khách quay lại tiếp tục được từ đúng chỗ |
| Ngân hàng từ chối | Báo lý do, gợi ý đổi phương thức |

**Bắt buộc:** hệ thống phải tự kiểm tra lại kết quả với cổng thanh toán, không tin vào việc khách quay về trang nào. Có trường hợp tiền đã trừ nhưng khách mất mạng giữa chừng — phải tự đối chiếu và xác nhận đơn.

---

## 6. Tiền tệ

- Giá gốc theo tiền tệ chủ nhà niêm yết. **Trừ tiền theo tiền tệ gốc**, không quy đổi khi charge.
- Khách xem bằng tiền tệ khác thì chỉ quy đổi để hiển thị, luôn ghi kèm giá gốc và một dòng lưu ý rằng số tiền thực trừ có thể lệch do tỉ giá ngân hàng.
- Tỉ giá cập nhật ít nhất mỗi 6 giờ. Tỉ giá dùng để hiển thị phải được ghi lại cùng đơn để sau này đối chiếu.
- Hoàn tiền luôn hoàn **đúng số tiền gốc bằng tiền tệ gốc**, không tính lại theo tỉ giá mới. Nếu khách bị lệch do tỉ giá thì đó là giữa khách và ngân hàng phát hành thẻ, phải nói rõ điều này trong điều khoản.

---

## 7. Vòng đời một lần thu tiền

```
Tạo yêu cầu thu  ──► Chờ xác thực  ──► Đang xử lý  ──► Thành công
       │                  │                │
       │                  │                └──► Thất bại ──► Thử lại / Đổi phương thức
       │                  └──► Khách bỏ dở ──► Hết hạn
       └──► Bị chặn bởi kiểm tra gian lận ──► Xem xét thủ công
```

**Mỗi yêu cầu thu tiền phải có mã chống trùng.** Nếu vì lỗi mạng mà cùng một yêu cầu bị gửi hai lần, chỉ được trừ tiền một lần. Đây là lỗi nghiêm trọng nhất trong module thanh toán — phải có kịch bản kiểm thử riêng.

**Đối soát bắt buộc mỗi ngày:** so danh sách giao dịch của sàn với danh sách của cổng thanh toán. Lệch một giao dịch là báo động, không được bỏ qua.

---

## 8. Khi thu tiền thất bại

Thông điệp báo cho khách phải nói **đúng nguyên nhân và việc cần làm**, không được ghi chung chung "giao dịch thất bại".

| Nguyên nhân | Nói với khách | Cho thử lại? |
|---|---|---|
| Không đủ số dư | Thẻ không đủ số dư cho khoản này | Có, hoặc đổi thẻ |
| Thẻ hết hạn | Thẻ đã hết hạn, vui lòng dùng thẻ khác | Không, phải đổi thẻ |
| Sai thông tin thẻ | Thông tin thẻ chưa đúng, kiểm tra lại số thẻ và ngày hết hạn | Có |
| Ngân hàng từ chối | Ngân hàng phát hành đã từ chối, liên hệ ngân hàng hoặc dùng thẻ khác | Có, tối đa 2 lần |
| Vượt hạn mức giao dịch | Vượt hạn mức của thẻ, thử thẻ khác hoặc liên hệ ngân hàng | Có |
| Thẻ chưa mở thanh toán trực tuyến | Thẻ chưa được mở thanh toán trực tuyến — rất phổ biến với thẻ nội địa VN | Có, sau khi khách mở |
| Nghi ngờ gian lận | Giao dịch cần xác minh thêm, bộ phận hỗ trợ sẽ liên hệ | Không tự động |
| Lỗi cổng thanh toán | Hệ thống thanh toán đang bận, thử lại sau ít phút | Có |

**Không bao giờ hiển thị mã lỗi kỹ thuật của ngân hàng cho khách.**

Giới hạn: tối đa **5 lần thử thất bại** trên một đơn trong 1 giờ, sau đó khoá đơn và yêu cầu liên hệ hỗ trợ. Chống dò thẻ trộm.

---

## 9. Trả một phần và chia hoá đơn

### 9.1. Trả một phần

Điều kiện được dùng: đơn còn cách ngày nhận phòng ít nhất **`[THAM SỐ TT-A]`** ngày, giá trị đơn từ **`[THAM SỐ TT-B]`** trở lên, khách trả bằng thẻ hoặc số dư (không áp dụng cho ví điện tử nếu ví không hỗ trợ thu định kỳ).

- Đợt 1: tối thiểu 50%, thu ngay khi xác nhận đơn
- Đợt 2: phần còn lại, thu tự động vào **14 ngày trước ngày nhận phòng**
- Nhắc khách trước 3 ngày và trước 1 ngày
- Khách chủ động trả sớm được bất cứ lúc nào
- Đổi thẻ trước ngày thu được

**Thu đợt 2 thất bại:** thử lại vào giờ thứ 6, 24, 48 và 72. Vẫn thất bại sau 72 giờ → huỷ đơn, áp chính sách huỷ của khách lên phần đã trả, mở lại lịch cho chủ nhà, báo cả hai bên.

### 9.2. Chia hoá đơn

- Người khởi tạo trả phần mình trước, đơn ở trạng thái **chờ đủ tiền**, ngày vẫn được giữ
- Tối đa 16 người, mỗi người nhận một đường dẫn riêng
- Hạn trả: **72 giờ**, và không muộn hơn 24 giờ trước ngày nhận phòng — lấy mốc nào đến trước
- Người khởi tạo thấy ai đã trả ai chưa, gửi nhắc được
- Không đủ tiền đúng hạn → hoàn lại toàn bộ cho những người đã trả, huỷ đơn, mở lại lịch. **Không tính là khách huỷ, không áp phí phạt.**
- Một người rút giữa chừng → người khởi tạo chọn: trả thay phần đó, hoặc huỷ toàn bộ

---

## 10. Hoàn tiền

- **Hoàn về đúng nguồn đã trả.** Trả thẻ thì hoàn về thẻ, trả bằng số dư thì hoàn về số dư, trả nhiều nguồn thì hoàn ngược thứ tự đã trừ.
- Thời gian tiền về tài khoản khách: **5–10 ngày làm việc** với thẻ, ngay lập tức với số dư. Phải nói rõ mốc này trước khi khách xác nhận huỷ, và nói rõ đây là thời gian của ngân hàng, không phải của sàn.
- **Thẻ đã hết hạn hoặc đã đóng:** vẫn hoàn về thẻ đó trước — ngân hàng thường tự chuyển vào tài khoản chủ thẻ. Không được thì chuyển thành số dư trong tài khoản sàn và báo khách, cho khách yêu cầu rút về ngân hàng.
- Hoàn một phần được, hoàn nhiều lần trên cùng một giao dịch được, nhưng tổng hoàn không bao giờ vượt số đã thu.
- Mọi lần hoàn đều ghi sổ và gửi thông báo kèm số tiền, lý do, mốc thời gian dự kiến.

---

## 11. Khách khiếu nại với ngân hàng

Khi khách báo với ngân hàng rằng giao dịch có vấn đề, ngân hàng sẽ tạm thu hồi tiền của sàn. Quy trình:

1. Nhận thông báo → **tạm giữ ngay** khoản chưa chuyển cho chủ nhà của đơn đó
2. Nếu đã chuyển cho chủ nhà rồi → đánh dấu khoản phải thu hồi, trừ vào đợt chuyển tiền tiếp theo
3. Tập hợp bằng chứng trong **7 ngày**: lịch sử đặt đơn, tin nhắn hai bên, xác nhận nhận phòng, chính sách huỷ đã hiển thị, hoá đơn
4. Gửi ngân hàng phản hồi
5. Thắng → tiền quay lại, gỡ tạm giữ. Thua → sàn chịu, ghi sổ vào mục thất thoát
6. Tài khoản khách có nhiều lần khiếu nại vô căn cứ → gắn cờ, yêu cầu xác minh cho các đơn sau

Chủ nhà không bị mất tiền vì khiếu nại của khách, **trừ khi** phân xử cho thấy lỗi thuộc về chủ nhà.

---

## 12. Trả tiền cho chủ nhà

### 12.1. Phương thức nhận

| Ưu tiên | Phương thức | Ghi chú |
|---|---|---|
| 1 | Chuyển khoản ngân hàng trong nước | Mặc định ở VN |
| 2 | Ví điện tử | Nhanh, phù hợp chủ nhà nhỏ |
| 3 | PayPal / Payoneer | Chủ nhà ở nước ngoài |

Chủ nhà đăng ký nhiều tài khoản nhận tiền, chọn một cái mặc định, gán tài khoản khác nhau cho từng tin đăng nếu muốn.

### 12.2. Xác minh trước khi trả lần đầu

- Tên chủ tài khoản phải khớp tên trên hồ sơ đã xác minh danh tính. Không khớp → phải giải trình và được duyệt thủ công.
- Chuyển thử một khoản nhỏ để xác nhận tài khoản đúng (áp dụng lần đầu).
- Đổi tài khoản nhận tiền → **tạm hoãn mọi khoản trả trong 3 ngày** và gửi cảnh báo tới email cũ. Đây là biện pháp chống chiếm tài khoản, không được bỏ.

### 12.3. Lịch trả

- Mặc định: 24 giờ sau khi khách nhận phòng
- Đơn từ 28 đêm: chia theo tháng, đợt đầu sau khi nhận phòng 24 giờ
- Chủ nhà mới (dưới 3 đơn hoàn tất): giữ thêm **`[THAM SỐ TT-C]`** ngày
- Gom nhiều đơn thành một lần chuyển trong ngày để giảm phí, nhưng báo cáo vẫn tách theo từng đơn

### 12.4. Tạm giữ không chuyển

Có bất kỳ điều nào sau đây → giữ lại, báo chủ nhà lý do:
- Đơn đang có tranh chấp hoặc hồ sơ Staylio Shield mở
- Khách đang khiếu nại với ngân hàng
- Tin đăng đang bị đình chỉ xem xét
- Tài khoản nhận tiền chưa xác minh hoặc vừa đổi
- Chủ nhà đang nợ sàn (phạt huỷ, bồi thường phải trả)

### 12.5. Chuyển thất bại

Thử lại sau 1 ngày, 3 ngày, 7 ngày. Vẫn thất bại → chuyển sang trạng thái cần chủ nhà xử lý, gửi email và thông báo, giữ tiền lại cho tới khi chủ nhà cập nhật tài khoản đúng.

---

## 13. Ba phương án pháp lý cho thị trường Việt Nam

Việc thu tiền của người này để trả cho người khác là **hoạt động trung gian thanh toán**, cần giấy phép của Ngân hàng Nhà nước. Anh chọn một trong ba, và lựa chọn này quyết định module thanh toán được xây thế nào.

### Phương án A — Đi qua cổng thanh toán có giấy phép *(khuyến nghị khi bắt đầu)*

Dùng VNPay / OnePay / MoMo / ZaloPay làm đơn vị thu hộ. Họ có giấy phép, sàn không cần xin.

- **Được:** khởi động nhanh, không cần giấy phép, họ lo tuân thủ và chống gian lận thẻ
- **Mất:** phí cao hơn, phụ thuộc, việc chia tiền cho chủ nhà sàn phải tự làm bằng chuyển khoản hàng loạt
- **Yêu cầu xây thêm:** công cụ tạo lệnh chuyển khoản hàng loạt, đối soát ngân hàng, quản lý số dư tạm giữ

### Phương án B — Tự xin giấy phép trung gian thanh toán

- **Được:** chủ động hoàn toàn, phí thấp nhất, làm được ví nội bộ cho chủ nhà
- **Mất:** vốn điều lệ lớn, thủ tục dài, nghĩa vụ báo cáo và tuân thủ nặng
- **Chỉ nên tính tới khi** khối lượng giao dịch đã đủ lớn để bù chi phí

### Phương án C — Không giữ tiền, chỉ thu hoa hồng

Khách trả thẳng chủ nhà, sàn thu phí riêng.

- **Được:** nhẹ nhất về pháp lý
- **Mất:** **phá vỡ toàn bộ mô hình.** Không hoàn tiền được, không có chính sách huỷ thực thi được, không có Staylio Shield, không có phân xử. Sàn tụt xuống thành trang rao vặt.
- Tui không khuyến nghị. Nếu chọn phương án này thì phải viết lại `03 §4`, `03 §5` và bỏ hẳn file `06`.

### Nếu bán cho khách quốc tế

Phát sinh thêm: thu ngoại tệ, chuyển tiền ra nước ngoài cho chủ nhà ở nước ngoài, nghĩa vụ thuế nhà thầu. Cần tư vấn riêng, đừng gộp vào giai đoạn đầu.

---

## 14. An toàn và tuân thủ

1. **Sàn không lưu số thẻ đầy đủ, không lưu mã CVV, không lưu dữ liệu dải từ.** Số thẻ do cổng thanh toán giữ, sàn chỉ giữ một mã tham chiếu.
2. Trang nhập thẻ phải là thành phần do cổng thanh toán cung cấp, không phải ô nhập tự viết.
3. Thông tin tài khoản nhận tiền của chủ nhà phải được mã hoá khi lưu, chỉ hiện 4 số cuối trên giao diện.
4. Mọi thao tác liên quan tiền đều ghi nhật ký: ai, lúc nào, từ thiết bị nào.
5. Cảnh báo gian lận cần theo dõi: tài khoản mới đặt đơn giá trị lớn · nhiều thẻ khác nhau trong thời gian ngắn · thẻ phát hành ở nước khác xa nơi đặt · nhiều lần thử thẻ thất bại liên tiếp · khách và chủ nhà cùng thiết bị · đơn được đặt rồi huỷ liên tục để rút số dư.
6. Đơn bị gắn cờ → giữ lại xem xét thủ công trước khi chuyển tiền, không chặn khách một cách im lặng.

---

## 15. Chức năng cần xây

| Mã | Chức năng |
|---|---|
| TC-P-01 | Chọn phương thức thanh toán ở bước thanh toán, hiện đủ nhóm ở §2 — **đã làm** (`PaymentMethods.cs`; đã bỏ "chuyển khoản ngân hàng" vì §2.4 từ chối) |
| TC-P-02 | Thêm, sửa, xoá, đặt mặc định phương thức thanh toán trong cài đặt tài khoản — **đã làm** (`SavedCards.cs`, tab Thanh toán) |
| TC-P-03 | Kết hợp nhiều nguồn tiền theo thứ tự §3, hiển thị từng dòng — **đã làm**, xem ghi chú §15.1 |
| TC-P-04 | Luồng xác thực OTP ngân hàng, gia hạn giữ chỗ trong lúc xác thực — **đã làm** (`CardAuth.cs`, thẻ thử `0002`) |
| TC-P-05 | Tự đối chiếu kết quả với cổng thanh toán, không tin vào trang khách quay về — **đã làm** (`CardAuthSweeper`, chạy trước vòng quét vòng đời) |
| TC-P-06 | Chống trừ tiền hai lần cho cùng một yêu cầu — **đã làm** (`Payments.cs`, bảng `payment_attempts`) |
| TC-P-07 | Bảng thông điệp lỗi theo §8, giới hạn số lần thử — **đã làm** |
| TC-P-08 | Trả một phần: tính lịch, thu tự động, nhắc trước, xử lý thất bại — **đã làm** (`PartialPayment.cs`, `BalanceCollector`) |
| TC-P-09 | Chia hoá đơn: tạo đường dẫn từng người, theo dõi, nhắc, hoàn khi không đủ — **đã làm** (`SplitBillService`) |
| TC-P-10 | Hoàn tiền về đúng nguồn, hoàn một phần, xử lý thẻ đã đóng — **đã làm** (`Refunds.cs`) |
| TC-P-11 | Quy đổi và hiển thị nhiều tiền tệ, ghi lại tỉ giá theo đơn — **đã làm** |
| TC-P-12 | Xử lý khiếu nại ngân hàng: tạm giữ, thu thập bằng chứng, theo dõi kết quả — **đã làm** (`Chargebacks.cs` + màn hình quản trị) |
| TC-O-01 | Đăng ký và xác minh tài khoản nhận tiền của chủ nhà — **đã làm** (khớp tên hồ sơ đã xác minh + chuyển thử 2.000₫) |
| TC-O-02 | Cảnh báo và hoãn 3 ngày khi chủ nhà đổi tài khoản nhận tiền — **đã làm** (`Payouts.cs`) |
| TC-O-03 | Lên lịch và thực hiện chuyển tiền, gom theo ngày — **đã làm** (`PayoutService`, một mã chuyển cho mỗi chủ nhà mỗi ngày) |
| TC-O-04 | Quy tắc tạm giữ theo §12.4 — **đã làm**, job đánh giá 5 lý do và báo chủ nhà |
| TC-O-05 | Thử lại khi chuyển thất bại, thông báo chủ nhà — **đã làm** (1/3/7 ngày, hết lượt thì báo chủ nhà) |
| TC-O-06 | Màn hình chủ nhà xem lịch sử và lịch chuyển tiền sắp tới — **đã làm** (tab Nhận tiền, gom theo mã chuyển) |
| TC-A-01 | Đối soát hằng ngày với cổng thanh toán, báo động khi lệch — **đã làm** (`gateway_charges` là sổ riêng của cổng, chỉ `PaymentGateway` ghi) |
| TC-A-02 | Màn hình quản trị: tra cứu giao dịch, hoàn tiền thủ công, điều chỉnh khoản chuyển — **đã làm** (`FinanceController`, cần quyền `Finance`, mọi thao tác có nhật ký) |
| TC-A-03 | Bảng theo dõi gian lận theo §14.5 — **đã làm** (`RiskWatch`, bảng Cảnh báo bất thường) |
| TC-A-04 | Báo cáo tài chính: doanh thu phí, tiền đang giữ hộ, thuế phải nộp, thất thoát — **đã làm** (đọc thẳng từ sổ ghi tiền) |
| TC-P-13 | **VietQR** (§2.3): sinh mã, giữ chỗ chờ tiền, đọc sao kê, khớp về đơn — **đã làm 13/08/2026**, xem §15.2 |
| TC-P-14 | **Cổng thanh toán có giấy phép** (§13 phương án A): VNPay / MoMo / ZaloPay — mở đơn, chuyển khách sang trang của họ, đọc IPN có chữ ký, tự hỏi lại kết quả — **đã làm 17/08/2026**, xem §15.3 |
| TC-P-16 | **Hoàn tiền qua cổng thật** (§10): VNPay/MoMo/ZaloPay, phân biệt từ chối vĩnh viễn với chưa biết, thử lại cùng mã yêu cầu, và đối soát hằng ngày hỏi đúng sổ của cổng — **đã làm 17/08/2026**, xem §15.6 |
| TC-P-15 | **Token hoá thẻ** (§4 với §14.2): khách chọn lưu thẻ, cổng giữ thẻ và trả về bốn số cuối — thứ duy nhất khôi phục được `CardLast4` sau khi bỏ ô nhập thẻ — **đã làm 17/08/2026**, xem §15.5 |
| TC-O-07 | **Lệnh chuyển tiền hàng loạt cho chủ nhà** (§13): lưu số tài khoản đã mã hoá, gom lệnh theo chủ nhà theo ngày, xuất file cho internet banking, xác nhận ngân hàng đã thực hiện rồi mới ghi sổ — **đã làm 17/08/2026**, xem §15.4 |

### 15.1. Hai chỗ của §3 cần khách xác nhận

1. **"Ưu tiên khoản sắp hết hạn trước"** — **đã dựng xong phần máy móc (09/08/2026),
   còn chờ khách chốt con số.** `CreditEntry` giờ có cột `ExpiresAt`, và
   `CreditLedger` đọc sổ chỉ-thêm ra từng gói: tiêu tiền lấy gói sắp hết hạn trước
   đúng như dòng này đòi, gói không hạn để cuối. Số dư đã hết hạn **không tiêu được
   ngay tại thời điểm hết hạn**, không đợi tác vụ quét; tác vụ quét chỉ ghi thêm một
   dòng âm để sổ vẫn là tổng các dòng.

   Nhưng **thời hạn bao lâu thì chưa ai chọn**, nên toàn bộ `Credits:` trong
   `appsettings.json` để trống và **không gì hết hạn cả** — y hệt hành vi trước đây.
   Chốt con số ở §16 là bật được, không cần sửa mã. Xem `TC-07` ở bảng §16.

2. **"Số dư đủ trả toàn bộ thì vẫn bắt buộc gắn phương thức dự phòng"** — luật đã cài
   (`PaymentMethods.NeedsFallbackMethod`, có test, máy chủ chặn thật). Nhưng theo
   `docs/03 §1` số dư **chỉ trừ vào tiền phòng**, không trừ phí dịch vụ và thuế, nên
   một đơn ở bình thường không bao giờ về 0₫. Luật đúng nhưng chưa có đường chạm tới.
   Nếu khách muốn số dư trừ được cả phí thì phải sửa `docs/03 §1` trước.

---

### 15.2. VietQR — chuyển khoản là một chặng, không phải một lần bấm

§2.3 xếp VietQR vào nhóm "có thể thêm sau", và điều khiến nó khác mọi phương thức
ở §2.1 là **sàn không nhìn thấy tiền đi**. Khách rời trang sang ứng dụng ngân hàng,
tiền về lúc nào không ai báo, và dấu vết duy nhất là một dòng trên sao kê. Vì thế
nó không dùng chung đường với thẻ được:

| Bước | Ai làm | Ghi lại ở đâu |
|---|---|---|
| Chọn VietQR ở bước thanh toán | Khách | Đơn ở trạng thái **chờ chuyển khoản**, giữ ngày/ghế/lịch, **chưa có bút toán nào** |
| Quét mã | Khách | Mã dựng từ chính đơn (`VietQr.cs`), nội dung là mã đơn nên không gõ nhầm được |
| Dán sao kê | Người trực (quyền `Finance`) | Mỗi dòng thành một `bank_credits`, khoá theo mã giao dịch ngân hàng |
| Khớp | Máy (`BankTransfers.Judge`) | Đúng mã + đúng số tiền thì đơn đi qua **đúng đường xác nhận mà thẻ đang đi** |
| Không khớp | Người trực | Dòng nằm lại hàng chờ tới khi có người ghi đã xử lý thế nào |

**Sáu phán quyết, chỉ hai cái im lặng.** Khớp thì xác nhận đơn; đã nhập trước đó thì
bỏ qua. Bốn cái còn lại — không tìm thấy mã đơn, không có đơn nào chờ mã đó, lệch số
tiền, tiền về sau khi đơn đã hết hạn — đều thành **việc cho người**, vì trong cả bốn
trường hợp tiền là tiền thật và chỉ người mới biết nên hoàn hay nên gọi cho khách.
**Trả thiếu không phải là trả**: §7 không cho phép bù hai cái sai thành một cái đúng.

**Giữ chỗ 2 giờ** (`BankTransfers.Window`) thay cho 15 phút của thẻ: khách phải mở
được ứng dụng ngân hàng và thường phải đăng nhập lại. Hết hạn thì ngày, ghế và lịch
được trả lại; **không có bút toán nào phải đảo, vì chưa từng có bút toán nào**. Tiền
về muộn trong vòng **7 ngày** (`LateWindow`) vẫn nhận ra đơn cũ, nhưng ra phán quyết
riêng chứ không tự khôi phục — chỗ đã cho người khác rồi.

**Chưa làm:** đặt cọc bằng chuyển khoản (sẽ là hai lần chuyển, hai mã, một lịch đòi
nốt — bị từ chối thẳng với câu giải thích), và tự động lấy sao kê từ ngân hàng.

---

### 15.3. Cổng thanh toán thật — §13 phương án A (17/08/2026)

Cho tới hôm nay **toàn bộ tầng thanh toán là giả lập**: `PaymentGateway` nói "có"
với mọi thẻ trừ thẻ thử `0000`, và ô nhập số thẻ là ô do sàn tự viết — trái thẳng
§14.1–2. Giờ bốn phương thức của §2.1 đi qua đơn vị thu hộ có giấy phép.

| Ô trên màn hình | Cổng | Vì sao |
|---|---|---|
| Thẻ tín dụng / ghi nợ | **VNPay** (`INTCARD`) | Visa / Mastercard / JCB / Amex |
| Thẻ ATM nội địa | **VNPay** (`VNBANK`) | Cùng một cổng, khác danh sách ngân hàng — nên không phải ký hợp đồng hai nơi |
| Ví MoMo | **MoMo** | |
| ZaloPay | **ZaloPay** | |

**Cổng nào chưa điền khoá thì phương thức đó vẫn chạy bằng bản giả lập** — đúng
quy tắc của nút đăng nhập mạng xã hội và của VietQR: thà thiếu còn hơn có mà
không chạy. Điền khoá vào là phương thức đó **thôi không bị trừ tiền ở đây nữa**,
khách rời sang trang của cổng.

**Ba đường có thể báo tin về, và không đường nào được tin một mình:**

| Đường | Ai gọi | Tin được không |
|---|---|---|
| Khách quay lại (`/return`) | Trình duyệt | **Không.** §5 nói thẳng: không tin vào việc khách quay về trang nào. Nó chỉ là cách khách về tới trang kết quả |
| IPN (`/ipn`, `/callback`) | Máy chủ của cổng | Có, **sau khi kiểm chữ ký** |
| Sàn tự hỏi (`PspSweeper`, mỗi phút) | Sàn | Có. Đây là câu trả lời cuối cùng, và trên máy lập trình nó là **đường duy nhất** vì IPN không tới được `localhost` |

Cái đầu tiên tới với chữ ký thật thì thắng; hai cái sau thấy phiên đã chốt rồi
thì không làm gì. Đó là lý do ba đường chạy đua với nhau được — §7 gọi trừ tiền
hai lần là lỗi nặng nhất module.

**Chữ ký sai thì bị bỏ qua, không phải bị coi là thất bại.** Không có gì xác thực
người gọi vào `/api/payments/momo/ipn`, nên nếu một callback không chữ ký đánh
hỏng được phiên thì bất kỳ ai đoán ra mã đơn cũng giết được lượt thanh toán của
người lạ — và người khách sau đó trả tiền thật sẽ quay về một đơn đã bị xoá sổ.
Lỗi này **đã có thật** trong bản đầu và bị `scripts/gateway_acceptance.py` bắt.

**Cổng báo số tiền khác với đơn thì không xác nhận**, dù chữ ký đúng: hoặc ai đó
sửa địa chỉ, hoặc bên kia có lỗi, và cả hai đều không phải lý do để giao phòng.

Sàn **không lưu số thẻ, không lưu CVV, không có ô nhập thẻ nào** khi cổng đã bật
— §14.1–2. Với cổng chưa bật thì ô nhập cũ vẫn còn, và nó vẫn chỉ là bản demo.

**Trả lời IPN phải đúng từng chữ hoa.** Bảng mã của VNPay: `00` và `02` là "đã
ghi nhận, thôi hỏi"; `01`, `04`, `97`, `99` là "hỏi lại". Nên chữ ký mà bên này
không kiểm được thì trả `97` — **xin hỏi lại**, chứ không đóng sổ một khoản tiền
chưa ai hiểu.

**Đã chạy thật (17/08/2026).** `scripts/gateway_acceptance.py`: **30/30**. VNPay
sandbox nhận cả hai ô thẻ và mở đúng trang *"Thẻ thanh toán quốc tế"* / danh sách
ngân hàng nội địa; MoMo và ZaloPay mở đơn thật; IPN ký đúng xác nhận đơn, gửi lại
lần hai trả `02` và không ghi sổ thêm dòng nào; sổ lệch **0**.

**Cái mất khi bật cổng thật: sàn không còn biết bốn số cuối của thẻ.** Khách gõ
thẻ trên trang của VNPay, nên `payments.CardLast4` là null. Ba thứ đọc cột đó và
đều mất chỗ dựa: thẻ đã lưu của §4, lời nhắc "thẻ sắp hết hạn còn lịch thu tự
động", và nhánh "thẻ đã đóng" của §10 (`Refunds.Redirect`). Khắc phục đúng cách là
dùng **token hoá** của cổng — VNPay có, nhưng phải đăng ký riêng — chứ không phải
lưu con số khách khai. Chưa làm.

**Chưa làm:** hoàn tiền ngược về cổng (`Refunds` hiện đi qua bản giả lập), trả
góp qua thẻ (§2.3), token hoá thẻ, và đối soát cuối ngày lấy sao kê **từ cổng**
thay vì từ bảng `gateway_charges` mà chính sàn ghi.

---

### 15.4. Chuyển tiền cho chủ nhà — cái §13 gọi là "sàn phải tự làm" (17/08/2026)

Phương án A chỉ giải quyết vế thu. Cổng trả **toàn bộ** tiền đơn về tài khoản
sàn, và §13 nói thẳng phần còn lại: *"việc chia tiền cho chủ nhà sàn phải tự làm
bằng chuyển khoản hàng loạt"*. Không có API nào sau câu đó.

Trước hôm nay bản dựng làm ba việc sai với chính câu ấy:

1. **Không lưu số tài khoản chủ nhà.** `SavePayout` đọc §14.3 thành "đừng giữ" và
   chỉ giữ bốn số cuối. Đúng là an toàn tuyệt đối, và cũng có nghĩa sàn thu tiền
   của khách rồi **không có gì để chuyển cho ai**. §14.3 nói *mã hoá khi lưu*,
   *chỉ hiện 4 số cuối* — hai việc khác nhau.
2. **Gọi `PaymentGateway.Charge(..., "bank-transfer", ...)`** — tức bản giả lập —
   rồi coi như đã chuyển.
3. **Ghi sổ "đã trả chủ nhà" ngay lúc đó.** Sổ nói tiền đã ra khỏi sàn trong khi
   nó vẫn nằm nguyên ở tài khoản sàn.

Giờ đường đi là:

| Bước | Ai làm | Trạng thái | Sổ sách |
|---|---|---|---|
| Tới hạn, đủ điều kiện §12.4 | Máy | Lệnh `payout_batches` **Chờ tải file**, đơn `Sent` | **Chưa ghi gì** |
| Tải file `.csv` | Người, quyền `Finance` | **Đã tải, chờ ngân hàng** | Chưa ghi gì |
| Ngân hàng thực hiện xong → bấm *Đã chuyển* | Người | **Ngân hàng đã chuyển**, đơn `Paid` | **Ghi bút toán ở đây** |
| Ngân hàng từ chối → bấm *Bị từ chối* | Người | **Ngân hàng từ chối** | Không có gì phải đảo |

`PayoutStatus.Sent` là một trạng thái riêng chứ không phải `Paid` sớm, vì khác
biệt giữa hai cái là **tiền còn là của sàn hay không**. Chủ nhà cũng đọc đúng chữ
đó: "đã lên lệnh chuyển", không phải "đã chuyển".

**File cố ý không theo mẫu riêng của ngân hàng nào.** Mỗi ngân hàng công bố mẫu
chuyển khoản hàng loạt của riêng họ và không mẫu nào giống nhau; đoán sai một mẫu
thì file vẫn tải lên được và **trả tiền cho nhầm người**. Sáu cột là phần chung
của mọi mẫu, người vận hành ánh xạ một lần. Số tài khoản để trong dấu nháy — một
bảng tính đọc `0123456789` thành số sẽ ăn mất số 0 và chuyển cho một tài khoản có
thật của người khác. File có BOM, vì Excel đọc CSV UTF-8 không BOM thành
Windows-1252 và làm hỏng mọi tên tiếng Việt trên đúng màn hình mà tên phải đúng.

**Đã chạy thật:** `scripts/payout_acceptance.py`, **23/23** — gồm cả ca đã bắt
được một lỗi thật: hai lệnh cho **cùng một chủ nhà trong cùng một ngày** từng
sinh trùng mã (số thứ tự đếm theo `PaidOutAt`, cột giờ chỉ được đặt lúc ngân hàng
xác nhận), ràng buộc duy nhất ném lỗi, và vì nó ném trong tick của worker nên các
vòng quét phía sau chết theo — im lặng.

**Đối chiếu sao kê — làm ngày 18/08/2026.** Người trực dán các dòng **chuyển đi**
của sao kê; dòng nào ngân hàng ghi đúng mã lệnh và đúng số tiền thì lệnh đó được
xác nhận qua **đúng lời gọi mà nút *Đã chuyển* dùng**, nên bút toán, thông báo và
nhật ký không khác một chữ. Mọi trường hợp khác chỉ được báo lại: sai số tiền
không ghi sổ, khoản chi không mang mã lệnh nào thì để yên, và **một dòng khớp
nhiều lệnh cùng lúc thì từ chối đoán** — `PO-20260818-42` và `PO-20260818-4-2` bỏ
dấu gạch đi là một chuỗi, đoán bừa sẽ trả tiền cho nhầm chủ nhà và sổ vẫn cân khi
làm thế.

Nó **chỉ xác nhận, không bao giờ đánh hỏng một lệnh**. Một lệnh vắng mặt trong sao
kê hôm nay hầu như luôn là sao kê chưa kịp, chứ không phải ngân hàng từ chối, và
từ đây nhìn thì hai thứ đó giống hệt nhau. Nên kết quả kèm thêm **danh sách lệnh
đã tải file mà ngân hàng chưa xác nhận, kèm số ngày chờ** — đó là nửa mà không
dòng sao kê nào nói ra được, và cũng là kiểu hỏng mà màn hình này sinh ra để bắt.

**Chưa làm:** mẫu file riêng cho từng ngân hàng.

---

### 15.5. Token hoá thẻ — lấy lại bốn số cuối (17/08/2026)

`§15.3` để lại một chỗ mất: cổng thật thì khách gõ thẻ ở trang VNPay, nên
`payments.CardLast4` là null, và ba thứ đọc cột đó mất chỗ dựa — thẻ đã lưu của
`§4`, nhắc "thẻ sắp hết hạn còn lịch thu tự động", và nhánh "thẻ đã đóng" của
`§10`. **API token của VNPay là đường duy nhất lấy lại nó**, vì đó là lần duy
nhất họ nói cho sàn biết gì về cái thẻ.

Khách tick **"Lưu thẻ này cho lần sau"** thì đơn đi qua `pay_and_create` thay vì
`pay`. Trả về: `vnp_token` (chỉ VNPay dùng được), `vnp_card_number` **đã che**
(`970419xxxxxx2198`), `vnp_card_type` (01 nội địa / 02 quốc tế). Sàn giữ bốn số
cuối, và giữ token **đã mã hoá** — khoá riêng dẫn xuất từ khoá chung của `§14.3`,
nên một ô lấy được từ bảng này không mở được ở bảng kia.

**Hai thứ khác hẳn API thanh toán, và cả hai đều dễ sai im lặng:**

1. **Tên tham số viết thường có gạch dưới** — `vnp_command`, không phải
   `vnp_Command`. Trộn hai kiểu thì nhận một trang lỗi trắng không nói field nào.
2. **Đường dẫn riêng** (`/token_ui/pay-create-token.html`).

Quy tắc ký thì **tài liệu của họ không nói**. Đã xác định bằng cách gửi sandbox
mỗi kiểu một lần: **sorted-query giống hệt API thanh toán** thì vào được trang
thanh toán, còn hai biến thể pipe-joined đều rơi vào `error.html`.

**Thẻ do cổng giữ thì không có ngày hết hạn ở đây.** API token không trả ngày
nào cả, nên `SavedCards.ExpiryKnown` nói thẳng là không biết thay vì bịa một
tháng; gọi nó "hết hạn" sẽ giấu mất một cái thẻ còn tốt. Khi thẻ hết hạn thật thì
VNPay từ chối token, và lời từ chối đó là cái khách nhìn thấy.

**Đã trả tiền thật, trên chính trang của VNPay.**
`scripts/vnpay_browser_acceptance.py` mở trình duyệt thật, gõ thẻ thử NCB của
VNPay, qua modal điều khoản, nhập OTP, và quay về: **14/14**. Đơn `Confirmed`,
`CardLast4 = 2198`, thẻ đã lưu với token mã hoá, `ProviderTxnId` là mã giao dịch
của chính VNPay, sổ lệch 0. Đây là bộ duy nhất mà **VNPay ký câu trả lời chứ
không phải sàn tự ký**.

**Chưa làm:** trả bằng token là một lần chuyển hướng nữa (`token_pay`), nên nó
vẫn cần khách có mặt — **không dùng để thu tiền bồi thường sau này** như `docs/06
§3.3` mong. Và `token_remove` chưa nối, nên xoá thẻ ở sàn thì token vẫn còn bên
VNPay.

---

### 15.6. Hoàn tiền qua cổng thật (17/08/2026)

`§15.3` mở đường cho tiền **vào**. Đường **ra** vẫn đi qua bản giả lập: huỷ đơn
gọi `PaymentGateway.Refund`, hàm nói "được" với mọi thứ trừ thẻ thử `0009`. Đơn
ghi "đã hoàn", sổ ghi bút toán, khách nhận thông báo — và **không đồng nào rời
khỏi tài khoản sàn**. Đúng một lỗi soi gương với `§15.4`.

Tệ hơn: trong **năm** đường huỷ đơn, chỉ một đường hỏi gì đó. Bốn đường còn lại
truyền `cardRefundAccepted: true` — mặc định vô hại khi chưa có tiền thật, thành
lời nói dối đúng ngày bật VNPay.

Giờ cả năm đi qua `RefundGateway`, và cái nó phân biệt **không phải "được /
không được"** mà là:

| Cổng trả lời | Nghĩa | Sàn làm gì |
|---|---|---|
| `00` + trạng thái `00`/`05`/`06`, hoặc `94` | Đã nhận, tiền đang về | Ghi sổ hoàn về thẻ |
| trạng thái `09`, hoặc mã `91`/`95` | **Từ chối vĩnh viễn** | Đúng ca `§10`: chuyển thành số dư, báo khách |
| `02`/`03`/`97`/`99`, hoặc gọi không được | **Chưa biết** | Thử lại 3 lần **cùng một `vnp_RequestId`** (VNPay nhận ra là một, trả `94`) rồi mới chuyển số dư, kèm log `Error` có mã yêu cầu để đối chiếu tay |

Trộn "chưa biết" vào "bị từ chối" là cách khách **được hoàn hai lần**: một lần
vào số dư, một lần vào thẻ khi lệnh cũ vẫn kịp chạy.

**Câu trả lời của cổng được lưu lại** (`payment_sessions.RefundCode`,
`RefundTxnId`, `RefundedAmount`). Không có nó thì `§7` đối soát một ngày có hoàn
tiền sẽ lệch mà không ai truy ra vì sao.

**Bẫy `User-Agent`.** `merchant_webapi` của VNPay trả **403 kèm HTML** cho mọi
request không có header `User-Agent`, và `HttpClient` mặc định không gửi. Nó
không giống lỗi chữ ký và không giống lỗi gì cả — log chỉ nói "không parse được
JSON". Nó đã âm thầm tắt **cả `refund` lẫn `querydr`**, tức cả lưới an toàn của
`§5`. Xác định bằng thực nghiệm: cùng một request, có `User-Agent` thì 200, bỏ đi
thì 403.

**Đối soát giờ hỏi đúng bên kia.** `§7` nói so danh sách của sàn với **danh sách
của cổng**; màn hình cũ đọc `gateway_charges` cho cả hai vế — tức so sổ của mình
với sổ của mình, cân mỗi ngày và không chứng minh gì. `GatewayStatement` dựng vế
kia bằng cách hỏi lại từng phiên đã chốt trong ngày.

**Đã chạy thật:** `scripts/refund_acceptance.py` — **11/11**. Trả tiền thật trên
trang VNPay, huỷ đơn, VNPay trả `ResponseCode 00` với mã giao dịch hoàn riêng,
tiền **không** bị đẩy sang số dư, sổ lệch 0.

**Chưa làm:** VNPay không có API tra cứu riêng cho giao dịch hoàn (`querydr` chỉ
trả về giao dịch thanh toán gốc), nên trạng thái cuối của một khoản hoàn
`05`/`06` phải xem ở cổng quản trị của họ.

## 16. Tham số cần chốt

| Mã | Tham số | Gợi ý | Giá trị chốt |
|---|---|---|---|
| TT-A | Trả một phần: cách ngày nhận tối thiểu bao nhiêu ngày | 14 ngày | |
| TT-B | Trả một phần: giá trị đơn tối thiểu | 5 triệu ₫ | |
| TT-C | Chủ nhà mới bị giữ thêm bao nhiêu ngày | 3 ngày | |
| TC-07a | Số dư bù đắp (Goodwill) hết hạn sau bao lâu | 12 tháng | **12 tháng** (chốt 11/08/2026) |
| TC-07b | Thưởng giới thiệu bạn hết hạn sau bao lâu | 12 tháng | **12 tháng** (chốt 11/08/2026) |
| TC-07c | Số dư hoàn lại khi huỷ đơn hết hạn sau bao lâu | 12 tháng | **12 tháng** (chốt 11/08/2026) |
| TC-07d | Thẻ quà tặng hết hạn sau bao lâu | **không hết hạn** — khách đã trả tiền thật cho nó | **không hết hạn** (chốt 11/08/2026) |
| — | Chọn phương án pháp lý A, B hay C (§13) | A | **A** (làm 17/08/2026, xem §15.3) |
| — | Cổng thanh toán chính | VNPay (một cổng lo cả thẻ quốc tế lẫn thẻ nội địa) | **VNPay**, thêm MoMo và ZaloPay cho hai ô ví. Sandbox đã chạy thật; prod cần giấy phép kinh doanh + hợp đồng |
| — | Có nhận khách quốc tế ngay từ đầu không? | không | |
| — | Có làm ví nội bộ cho chủ nhà không? | không, giai đoạn đầu | |

---

## 17. Ảnh hưởng tới các file khác

1. **`01` ĐP-08** — thay bằng danh sách cụ thể ở §2.
2. **`03 §5`** — bổ sung: quy tắc tạm giữ ở §12.4 và xử lý khiếu nại ngân hàng ở §11.
3. **`05` thực thể "Giao dịch thanh toán"** — bổ sung: mã chống trùng, số lần thử, tỉ giá tại thời điểm giao dịch, trạng thái xác thực OTP.
4. **`05` thực thể "Khoản chuyển cho chủ nhà"** — bổ sung: lý do tạm giữ, số lần thử lại, khoản khấu trừ do nợ sàn.
5. **`06 §3.3`** — thứ tự thu tiền bồi thường phải dùng đúng phương thức dự phòng đã lưu ở §3.

---

## 18. Kịch bản bắt buộc phải chạy thử

1. Trả bằng thẻ Visa có OTP → thành công → đơn xác nhận, sổ sách cân
2. Trả bằng thẻ, khách đóng tab giữa lúc nhập OTP → quay lại tiếp tục được, không bị trừ hai lần
3. Tiền đã trừ nhưng khách mất mạng → hệ thống tự đối chiếu và xác nhận đơn
4. Cùng một yêu cầu thu tiền gửi hai lần → chỉ trừ một lần
5. Thẻ không đủ số dư → thông điệp đúng nguyên nhân → đổi thẻ khác → thành công
6. Thử thẻ sai 5 lần → khoá đơn, yêu cầu liên hệ hỗ trợ
7. Trả một phần → đợt 2 thu tự động thành công → đơn giữ nguyên
8. Trả một phần → đợt 2 thất bại 4 lần trong 72 giờ → đơn tự huỷ, áp chính sách huỷ, lịch mở lại
9. Chia hoá đơn 4 người, 1 người không trả → hoàn cả 3 người kia, huỷ đơn, không phạt ai
10. Huỷ đơn đã trả bằng thẻ quà tặng + thẻ Visa → hoàn ngược thứ tự, từng nguồn đúng số
11. Chủ nhà đổi tài khoản nhận tiền → khoản chuyển bị hoãn 3 ngày, email cũ nhận cảnh báo
12. Đơn có hồ sơ Staylio Shield đang mở → khoản chuyển bị giữ, chủ nhà thấy lý do
13. Đối soát cuối ngày phát hiện một giao dịch lệch → hệ thống báo động, không tự làm ngơ
