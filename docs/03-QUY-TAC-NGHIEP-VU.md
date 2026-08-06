# 03 — Quy tắc nghiệp vụ

Đây là phần quan trọng nhất và dễ sai nhất. Mỗi quy tắc chỉ được định nghĩa **một lần** và dùng chung cho tìm kiếm, trang chi tiết và trang thanh toán — ba nơi này không bao giờ được ra kết quả khác nhau.

---

## 1. Cách tính giá một đơn đặt chỗ ở

Đầu vào: chỗ ở, ngày nhận, ngày trả, số người lớn/trẻ em/em bé/thú cưng, mã giảm giá.

**Tính theo đúng thứ tự sau:**

**Bước 1 — Tiền phòng từng đêm.**
Số đêm = ngày trả − ngày nhận. Với mỗi đêm, lấy giá theo thứ tự ưu tiên:
giá chủ nhà đặt riêng cho ngày đó → giá theo mùa → giá cuối tuần (nếu là thứ 6, 7) → giá cơ bản.
Cộng lại thành **tiền phòng gốc**.

**Bước 2 — Giảm giá theo độ dài kỳ ở.** Chọn **một** mức, ưu tiên mức dài hơn:
từ 28 đêm → giảm theo tháng; từ 7 đêm → giảm theo tuần.

**Bước 3 — Giảm giá theo thời điểm đặt.** Chọn **một**, lấy mức lớn hơn:
đặt sớm (cách ngày nhận ≥ N ngày) hoặc đặt phút chót (cách ngày nhận ≤ M ngày).

**Bước 4 — Giảm giá tin mới.** Ba đơn đầu tiên của một tin đăng được giảm thêm 20%.

Tổng mọi phần trăm giảm cộng lại **không vượt quá 60%**. Giảm giá chỉ áp lên **tiền phòng**, không áp lên phí vệ sinh và các phụ phí.

**Bước 5 — Phụ thu.**
- Phụ thu khách thêm: (người lớn + trẻ em − ngưỡng miễn phí) × mức phụ thu × số đêm. **Em bé không tính.**
- Phí thú cưng: theo lượt ở hoặc theo đêm, tuỳ chủ nhà đặt
- Phí vệ sinh: một lần cho cả kỳ ở, không nhân với số đêm

**Bước 6 — Tạm tính.** = tiền phòng sau giảm + phụ thu khách + phí thú cưng + phí vệ sinh

**Bước 7 — Phí dịch vụ khách.** = tạm tính × 14%. Tính **trước** thuế.

**Bước 8 — Thuế.** Áp theo quy tắc thuế của khu vực chỗ ở. Có thể là phần trăm, hoặc số tiền cố định mỗi đêm, mỗi khách, hoặc mỗi lượt ở. Một khu vực có thể có nhiều loại thuế chồng nhau.

**Bước 9 — Giảm trừ.** Mã giảm giá và số dư khuyến mãi trừ sau cùng, hiển thị thành dòng riêng.

**Bước 10 — Tổng khách trả** = tạm tính + phí dịch vụ khách + thuế − giảm trừ

**Bước 11 — Phần chủ nhà.**
Phí dịch vụ chủ nhà = tạm tính × 3%.
Chủ nhà nhận = tạm tính − phí dịch vụ chủ nhà.
Thuế do sàn thu hộ thì không nằm trong khoản chủ nhà nhận.

### Quy tắc làm tròn
Làm tròn ở **từng dòng phí**, không làm tròn một lần ở cuối. Tổng cuối cùng phải đúng bằng tổng các dòng đã hiển thị — nếu lệch do làm tròn thì điều chỉnh vào dòng cuối.

### Hiển thị nhiều loại tiền tệ
Giá gốc luôn theo tiền tệ chủ nhà đặt. Khi khách xem bằng tiền tệ khác thì quy đổi để hiển thị nhưng **luôn ghi kèm giá gốc**. Trừ tiền theo tiền tệ gốc.

### Các trường hợp phải kiểm tra
| Tình huống | Kết quả đúng |
|---|---|
| 3 đêm, không giảm giá gì | tiền phòng ×3 + phí vệ sinh + 14% + thuế |
| 7 đêm, giảm tuần 10% | 10% chỉ trừ vào tiền phòng, phí vệ sinh giữ nguyên |
| 30 đêm | dùng mức giảm theo tháng, bỏ mức theo tuần |
| Chủ nhà đặt giá riêng cho 2 trong 3 đêm | chỉ 1 đêm dùng giá cơ bản |
| 5 khách, ngưỡng miễn phí 2 | phụ thu cho 3 khách × số đêm |
| 2 người lớn + 1 em bé | không phụ thu |
| Giảm tuần 10% + đặt sớm 15% | tổng giảm 25% trên tiền phòng, không phải nhân chuỗi ra 23,5% |
| Tổng giảm tính ra 70% | chặn lại còn 60% |

---

## 2. Cách xác định một chỗ ở có đặt được không

Kiểm tra lần lượt, gặp lỗi đầu tiên thì dừng và nêu đúng lý do đó cho khách:

1. Tin đăng đang ở trạng thái hiển thị, không tạm nghỉ
2. Số người lớn + trẻ em ≤ sức chứa (em bé không tính)
3. Nếu mang thú cưng: chủ nhà phải cho phép và không vượt số lượng tối đa
4. **Báo trước:** ngày nhận phải cách hôm nay ít nhất khoảng thời gian chủ nhà yêu cầu. Nếu chủ nhà cho đặt trong ngày thì phải đặt trước giờ cắt (tính theo múi giờ của chỗ ở, không phải của khách)
5. **Tầm nhìn lịch:** ngày trả không vượt quá khoảng thời gian chủ nhà mở lịch (3/6/9/12 tháng hoặc không giới hạn)
6. **Số đêm:** nằm trong khoảng tối thiểu–tối đa. Số đêm tối thiểu có thể được chủ nhà đặt riêng cho từng ngày, ưu tiên giá trị riêng đó
7. **Ngày trong tuần bị chặn:** một số chủ nhà không cho nhận phòng hoặc trả phòng vào một số thứ nhất định
8. **Mọi đêm phải còn trống.** Đêm cuối cùng là ngày trước ngày trả phòng — **không kiểm tra ngày trả phòng**
9. **Thời gian dọn dẹp:** không có đơn nào kết thúc quá sát trước ngày nhận, hoặc bắt đầu quá sát sau ngày trả

### Chống đặt trùng
Một khoảng ngày chỉ được bán một lần. Khi hai người cùng đặt một khoảng ngày trong cùng thời điểm, chỉ một người thành công; người còn lại nhận thông báo rõ ràng và được gợi ý ngày khác. Đây là yêu cầu bắt buộc, không phải tối ưu hoá.

### Giữ chỗ tạm
Khi khách bắt đầu bước thanh toán, ngày được giữ **15 phút**. Trong thời gian này người khác không đặt được. Hết giờ mà chưa trả tiền xong thì ngày được mở lại tự động và khách được thông báo.

### Yêu cầu đặt thì không giữ ngày
Với chế độ "yêu cầu đặt", ngày **không bị khoá** trong lúc chờ chủ nhà duyệt. Ai trả tiền xong trước thì được. Phải nói rõ điều này cho khách khi họ gửi yêu cầu.

---

## 3. Vòng đời một đơn đặt

```
Hỏi trước khi đặt
      │
      ├─ (Đặt ngay) ─────────────────────► Chờ thanh toán
      │
      └─ (Yêu cầu đặt) ─► Chờ chủ nhà duyệt
                                │
                    chấp nhận   │   từ chối / quá 24h
                                ▼         ▼
                        Chờ thanh toán   Bị từ chối / Hết hạn
                                │
                trả tiền xong   │   thất bại / quá 15 phút
                                ▼         ▼
                          Đã xác nhận   Không thành công
                                │
        ┌───────────────────────┼──────────────────────┐
   khách huỷ              chủ nhà huỷ            tới ngày nhận
        ▼                       ▼                      ▼
  Khách đã huỷ           Chủ nhà đã huỷ           Đang lưu trú
                        (chủ nhà bị phạt)              │
                                                  tới ngày trả
                                                       ▼
                                                  Đã hoàn tất
                                                       │
                                            trong 14 ngày viết đánh giá
```

Quy tắc:
- Chỉ được chuyển theo đúng các mũi tên trên. Mỗi lần chuyển đều ghi lại: ai làm, lúc nào, vì sao.
- Yêu cầu đặt tự hết hạn sau **24 giờ**, báo cho cả hai bên.
- Chuyển sang "đang lưu trú" và "đã hoàn tất" tính theo **múi giờ của chỗ ở**.
- "Đã hoàn tất" mới mở form đánh giá và lên lịch trả tiền cho chủ nhà.

---

## 4. Chính sách huỷ và cách tính hoàn tiền

### Bốn quy tắc áp trước mọi chính sách
1. **Ân hạn 48 giờ:** huỷ trong vòng 48 giờ kể từ lúc đặt **và** còn ít nhất 14 ngày tới ngày nhận → hoàn 100%, kể cả phí dịch vụ.
2. **Phí dịch vụ khách** chỉ được hoàn tối đa 3 lần mỗi năm cho mỗi tài khoản.
3. **Chủ nhà huỷ** → khách luôn hoàn 100% và được tặng thêm số dư bằng 10% giá trị đơn.
4. **Bất khả kháng** (thiên tai, dịch bệnh, sự kiện được quản trị công nhận) → hoàn 100%, chủ nhà được đền bù một phần từ quỹ của sàn.

### Bảng chính sách

| Chính sách | Thời điểm huỷ | Hoàn tiền phòng | Phí vệ sinh | Phí dịch vụ |
|---|---|---|---|---|
| **Linh hoạt** | ≥24 giờ trước nhận phòng | 100% | 100% | 100% |
| | <24 giờ | mất đêm đầu, hoàn các đêm còn lại | 100% | 0% |
| **Vừa phải** | ≥5 ngày | 100% | 100% | 100% |
| | <5 ngày | 50% các đêm còn lại | 100% | 0% |
| **Chặt** | ≥30 ngày | 100% | 100% | 100% |
| | 7–30 ngày | 50% | 100% | 0% |
| | <7 ngày | 0% | 100% | 0% |
| **Rất chặt** | trong ân hạn 48h và ≥14 ngày | 100% | 100% | 100% |
| | ≥7 ngày | 50% | 100% | 0% |
| | <7 ngày | 0% | 100% | 0% |
| **Không hoàn** | mọi lúc | 0% (đổi lại được giảm 10% giá) | 100% | 0% |
| **Dài hạn – chặt** (≥28 đêm) | ≥30 ngày trước | 100% | 100% | 100% |
| | <30 ngày | trả 30 đêm đầu, hoàn phần còn lại | 100% | 0% |

**Nguyên tắc chung:** nếu khách đã nhận phòng rồi mới huỷ, tính hoàn tiền theo số đêm **chưa ở** kể từ ngày huỷ, rồi áp tỉ lệ của chính sách.

Chủ nhà chọn một chính sách cho mỗi tin đăng. Chính sách phải hiển thị rõ trên trang chi tiết và trong bước thanh toán, kèm ngày cụ thể ("Huỷ miễn phí trước 14:00 ngày 12/09").

### Hậu quả khi chủ nhà tự huỷ đơn đã xác nhận
- Bị trừ một khoản phạt, mức phạt tăng dần khi càng sát ngày nhận phòng
- Những ngày đó bị chặn, không cho đặt lại
- Tin đăng tự động hiện dòng ghi chú công khai: "Chủ nhà đã huỷ một đơn trước ngày nhận phòng X ngày"
- Mất tư cách Chủ nhà Ưu tú trong 1 năm
- Huỷ 3 lần trong 1 năm → tin đăng bị tạm ẩn để xem xét

Ngoại lệ: trường hợp bất khả kháng được quản trị duyệt thì không bị phạt.

---

## 5. Dòng tiền

```
Khách trả  →  Sàn giữ  →  (sau khi khách nhận phòng 24 giờ)  →  Chủ nhà
                 │
                 ├─► phí dịch vụ khách   (doanh thu sàn)
                 ├─► phí dịch vụ chủ nhà (doanh thu sàn)
                 └─► thuế                (nộp cho cơ quan thuế)
```

**Thời điểm trừ tiền khách:**
- Đặt ngay → trừ ngay khi xác nhận
- Yêu cầu đặt → chỉ trừ khi chủ nhà chấp nhận

**Trả một phần:** cọc tối thiểu 50% ngay, phần còn lại tự động thu vào thời điểm 14 ngày trước ngày nhận (hoặc ngay nếu đặt sát ngày). Thu lần hai thất bại → thử lại trong 72 giờ, vẫn thất bại thì huỷ đơn và áp chính sách huỷ của khách.

**Chia hoá đơn:** người khởi tạo trả phần mình trước, những người còn lại có 72 giờ (và không quá 24 giờ trước ngày nhận) để trả. Không đủ → hoàn lại toàn bộ và huỷ đơn.

**Thời điểm trả tiền chủ nhà:** 24 giờ sau khi khách nhận phòng. Đơn từ 28 đêm trở lên trả theo từng tháng. Chủ nhà mới hoặc có dấu hiệu rủi ro thì giữ thêm vài ngày.

**Ghi sổ:** mọi khoản đều được ghi hai chiều. Tổng tiền vào phải bằng tổng tiền ra. Đối soát tự động mỗi ngày, lệch là báo động ngay.

---

## 6. Xếp hạng kết quả tìm kiếm

**Lọc trước** (bắt buộc phải thoả, không thoả thì loại khỏi kết quả): trong vùng địa lý đang tìm, đúng dòng cung ứng, đủ sức chứa, còn trống đủ ngày, thoả mọi bộ lọc khách chọn, giá nằm trong khoảng.

**Xếp hạng sau** (điểm tổng hợp):

| Yếu tố | Trọng số | Ý nghĩa |
|---|---|---|
| Gần trung tâm khu vực tìm | 30% | càng gần càng cao |
| Chất lượng | 25% | điểm đánh giá có tính tới số lượng đánh giá |
| Tỉ lệ xem→đặt gần đây | 15% | chỗ được nhiều người chốt thì đẩy lên |
| Giá cạnh tranh | 10% | so với giá trung vị của chỗ tương đương cùng khu vực |
| Chất lượng phục vụ | 10% | tỉ lệ phản hồi + có bật đặt ngay |
| Chất lượng ảnh | 5% | số lượng và độ phân giải ảnh |
| Tin mới | 5% | ưu đãi hiển thị trong 30 ngày đầu |

**Trừ điểm:** tỉ lệ tự huỷ cao, điểm đánh giá dưới 4.0, thiếu ảnh, thông tin không đầy đủ.

**Đa dạng hoá:** trong 12 kết quả đầu, không quá 2 chỗ của cùng một chủ nhà.

Tìm địa điểm phải bỏ dấu được: gõ "da lat" phải ra "Đà Lạt", gõ "hcm" phải ra "Thành phố Hồ Chí Minh".

---

## 7. Đánh giá

- Chỉ đơn **đã hoàn tất** mới được đánh giá. Đơn bị huỷ thì không, trừ trường hợp chủ nhà huỷ (sinh ghi chú hệ thống).
- Cửa sổ 14 ngày kể từ ngày trả phòng. Nhắc vào ngày 1, 7, 13.
- **Mù hai chiều:** không ai đọc được đánh giá của bên kia cho tới khi cả hai đã gửi, hoặc hết 14 ngày.
- Khách chấm 6 hạng mục: sạch sẽ, đúng mô tả, nhận phòng, giao tiếp, vị trí, đáng giá tiền. Kèm nhận xét công khai và góp ý riêng.
- Chủ nhà chấm khách: sạch sẽ, giao tiếp, tuân thủ nội quy + có/không khuyến nghị.
- Chủ nhà được trả lời công khai **một lần**, trong 30 ngày sau khi đánh giá hiện ra.
- Sửa được trong 48 giờ sau khi gửi, và chỉ khi bên kia chưa gửi.
- Tự động chặn nội dung chứa số điện thoại, email, đường link, ngôn từ phân biệt đối xử.
- Điểm của tin đăng được tính lại ngay sau mỗi đánh giá công khai.

---

## 8. Danh hiệu

### Chủ nhà Ưu tú
Xét lại mỗi quý (1/1, 1/4, 1/7, 1/10), phải thoả cả bốn:
- Điểm đánh giá tổng ≥ 4.8
- Từ 10 chuyến trở lên trong năm (hoặc từ 3 chuyến với tổng ≥ 100 đêm)
- Tỉ lệ phản hồi tin nhắn ≥ 90%
- Tỉ lệ tự huỷ đơn < 1%

Không đạt thì mất danh hiệu, được giữ lại nếu quý sau đạt lại.

### Khách chọn
Xét lại hằng tuần: điểm ≥ 4.9, có ít nhất 5 đánh giá, tỉ lệ huỷ thấp, không có báo cáo vi phạm nghiêm trọng.

---

## 9. Đồng bộ lịch với nền tảng khác

- **Xuất đi:** mỗi tin đăng có một đường dẫn lịch riêng, chứa các ngày đã bị đặt hoặc bị chặn. Không để lộ thông tin khách.
- **Nhập về:** kiểm tra tự động mỗi 2 giờ, ngày nào bị đặt ở nơi khác thì chặn ở đây.
- **Xung đột:** nếu lịch nhập về báo bận vào ngày đã có đơn xác nhận ở sàn này thì **giữ đơn của sàn**, đồng thời cảnh báo chủ nhà xử lý ngay.
- Hiển thị thời điểm đồng bộ gần nhất để chủ nhà biết dữ liệu có mới không.

---

## 10. Bảo vệ thông tin và chống lạm dụng

- Địa chỉ chính xác, số điện thoại chủ nhà và mã cửa chỉ hiện sau khi đơn được xác nhận (mã cửa hiện từ 48 giờ trước ngày nhận).
- Trước khi đơn được xác nhận, số điện thoại/email/link trong tin nhắn bị che và có cảnh báo giao dịch ngoài sàn không được bảo vệ.
- Ảnh tải lên bị xoá thông tin vị trí kèm theo trước khi lưu.
- Giới hạn tần suất: tìm kiếm, đăng nhập, gửi tin nhắn, tạo đơn — để chống dò quét và spam.
- Dấu hiệu cần chặn hoặc xem xét thủ công: tài khoản mới đặt đơn giá trị lớn, nhiều thẻ khác nhau trong thời gian ngắn, nhiều đơn bị huỷ liên tiếp, chủ nhà và khách trùng thiết bị (đánh giá giả).
- Xoá tài khoản: ẩn danh thông tin cá nhân nhưng giữ lại dữ liệu giao dịch cho nghĩa vụ kế toán, tên hiển thị đổi thành "Người dùng đã xoá".

---

## 11. Thông báo

| Sự kiện | Báo cho khách | Báo cho chủ nhà |
|---|---|---|
| Có yêu cầu đặt mới | xác nhận đã gửi, đang chờ | **cần trả lời trong 24 giờ** |
| Chủ nhà chấp nhận/từ chối | ✓ | — |
| Đơn được xác nhận | ✓ kèm hành trình | ✓ kèm thông tin khách |
| Còn 7 ngày / 24 giờ tới ngày nhận | nhắc + hướng dẫn nhận phòng | nhắc chuẩn bị |
| Sáng ngày trả phòng | nhắc giờ trả | — |
| Sau khi trả phòng | mời viết đánh giá | mời viết đánh giá |
| Có tin nhắn mới | ✓ | ✓ |
| Đơn bị huỷ | ✓ kèm số tiền hoàn | ✓ |
| Đã chuyển tiền | — | ✓ |
| Đánh giá được công khai | ✓ | ✓ |
| Chỗ đã lưu giảm giá | ✓ (nếu bật) | — |

Thông báo giao dịch (xác nhận đơn, huỷ, thanh toán) luôn gửi, không cho tắt. Thông báo tiếp thị phải cho tắt.
