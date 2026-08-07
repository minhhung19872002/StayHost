# 07 — Thanh toán

Chi tiết hoá `01-DANH-MUC-CHUC-NANG.md` mục TC và ĐP-05 → ĐP-09.
Đọc kèm `03-QUY-TAC-NGHIEP-VU.md` §5 (dòng tiền) và §1 (cách tính giá).

---

## 1. Nguyên tắc gốc: sàn giữ tiền hộ

Tiền của khách **luôn đi qua sàn**, không đi thẳng cho chủ nhà. Sàn cầm tiền từ lúc đơn được xác nhận cho tới 24 giờ sau khi khách nhận phòng.

Đây không phải lựa chọn kỹ thuật mà là điều kiện tồn tại của mô hình. Không giữ tiền thì:
- Không hoàn tiền được khi khách huỷ → chính sách huỷ vô nghĩa
- Không có gì ràng buộc chủ nhà giữ lời → khách không dám đặt
- Không có StayShield, không có phân xử tranh chấp
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

Tiền mặt · chuyển khoản thủ công · tiền mã hoá · séc · trả khi nhận phòng.

Khi khách hỏi những cách này, hệ thống phải giải thích lý do ngắn gọn: tiền được giữ để bảo vệ cả hai bên cho tới khi khách nhận phòng.

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
- Nếu số dư đủ trả toàn bộ thì vẫn **bắt buộc gắn một phương thức dự phòng**, dùng khi có phát sinh (đổi lịch, bồi thường).
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
- Đơn đang có tranh chấp hoặc hồ sơ StayShield mở
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
- **Mất:** **phá vỡ toàn bộ mô hình.** Không hoàn tiền được, không có chính sách huỷ thực thi được, không có StayShield, không có phân xử. Sàn tụt xuống thành trang rao vặt.
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
| TC-P-01 | Chọn phương thức thanh toán ở bước thanh toán, hiện đủ nhóm ở §2 |
| TC-P-02 | Thêm, sửa, xoá, đặt mặc định phương thức thanh toán trong cài đặt tài khoản |
| TC-P-03 | Kết hợp nhiều nguồn tiền theo thứ tự §3, hiển thị từng dòng |
| TC-P-04 | Luồng xác thực OTP ngân hàng, gia hạn giữ chỗ trong lúc xác thực |
| TC-P-05 | Tự đối chiếu kết quả với cổng thanh toán, không tin vào trang khách quay về |
| TC-P-06 | Chống trừ tiền hai lần cho cùng một yêu cầu — **đã làm** (`Payments.cs`, bảng `payment_attempts`) |
| TC-P-07 | Bảng thông điệp lỗi theo §8, giới hạn số lần thử — **đã làm** |
| TC-P-08 | Trả một phần: tính lịch, thu tự động, nhắc trước, xử lý thất bại |
| TC-P-09 | Chia hoá đơn: tạo đường dẫn từng người, theo dõi, nhắc, hoàn khi không đủ |
| TC-P-10 | Hoàn tiền về đúng nguồn, hoàn một phần, xử lý thẻ đã đóng — **đã làm** (`Refunds.cs`) |
| TC-P-11 | Quy đổi và hiển thị nhiều tiền tệ, ghi lại tỉ giá theo đơn — **đã làm** |
| TC-P-12 | Xử lý khiếu nại ngân hàng: tạm giữ, thu thập bằng chứng, theo dõi kết quả — **quy tắc + bảng đã làm** (`Chargebacks.cs`), còn màn hình vận hành |
| TC-O-01 | Đăng ký và xác minh tài khoản nhận tiền của chủ nhà — **đã làm** (khớp tên hồ sơ đã xác minh + chuyển thử 2.000₫) |
| TC-O-02 | Cảnh báo và hoãn 3 ngày khi chủ nhà đổi tài khoản nhận tiền — **đã làm** (`Payouts.cs`) |
| TC-O-03 | Lên lịch và thực hiện chuyển tiền, gom theo ngày — **đã làm** (`PayoutService`, một mã chuyển cho mỗi chủ nhà mỗi ngày) |
| TC-O-04 | Quy tắc tạm giữ theo §12.4 — **đã làm**, job đánh giá 5 lý do và báo chủ nhà |
| TC-O-05 | Thử lại khi chuyển thất bại, thông báo chủ nhà — **đã làm** (1/3/7 ngày, hết lượt thì báo chủ nhà) |
| TC-O-06 | Màn hình chủ nhà xem lịch sử và lịch chuyển tiền sắp tới — **đã làm** (tab Nhận tiền, gom theo mã chuyển) |
| TC-A-01 | Đối soát hằng ngày với cổng thanh toán, báo động khi lệch — **phép so đã làm** (`Reconciliation.cs`), còn nối nguồn dữ liệu cổng |
| TC-A-02 | Màn hình quản trị: tra cứu giao dịch, hoàn tiền thủ công, điều chỉnh khoản chuyển |
| TC-A-03 | Bảng theo dõi gian lận theo §14.5 |
| TC-A-04 | Báo cáo tài chính: doanh thu phí, tiền đang giữ hộ, thuế phải nộp, thất thoát |

---

## 16. Tham số cần chốt

| Mã | Tham số | Gợi ý | Giá trị chốt |
|---|---|---|---|
| TT-A | Trả một phần: cách ngày nhận tối thiểu bao nhiêu ngày | 14 ngày | |
| TT-B | Trả một phần: giá trị đơn tối thiểu | 5 triệu ₫ | |
| TT-C | Chủ nhà mới bị giữ thêm bao nhiêu ngày | 3 ngày | |
| — | Chọn phương án pháp lý A, B hay C (§13) | A | |
| — | Cổng thanh toán chính | | |
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
12. Đơn có hồ sơ StayShield đang mở → khoản chuyển bị giữ, chủ nhà thấy lý do
13. Đối soát cuối ngày phát hiện một giao dịch lệch → hệ thống báo động, không tự làm ngơ
