# 08 — Quản trị người dùng

Chi tiết hoá `00-TONG-QUAN.md` §3.4 và `01-DANH-MUC-CHUC-NANG.md` QT-03, QT-09, QT-10.

---

## 1. Bốn nguyên tắc

1. **Quyền tối thiểu.** Mỗi vai chỉ có đúng quyền cần cho công việc. Không có vai "xem hết làm hết" ngoài Quản trị tối cao, và vai đó chỉ nên có 1–2 người.
2. **Mọi thao tác để lại dấu vết.** Kể cả thao tác đọc dữ liệu nhạy cảm. Nhật ký không ai xoá được, kể cả Quản trị tối cao.
3. **Không ai tự xử lý việc của mình.** Admin không được thao tác lên tài khoản của chính mình, người thân, hoặc bất kỳ tài khoản nào mình có liên quan trong đơn đặt. Hệ thống phải tự phát hiện và chặn.
4. **Quyết định nào cũng phải có lý do ghi lại.** Không cho lưu quyết định mà bỏ trống ô lý do.

---

## 2. Ma trận quyền

| Hành động | Hỗ trợ | Kiểm duyệt | Tài chính | Tối cao |
|---|:---:|:---:|:---:|:---:|
| Tìm và xem hồ sơ người dùng | ✓ | ✓ | ✓ | ✓ |
| Xem lịch sử đơn đặt của người dùng | ✓ | ✓ | ✓ | ✓ |
| Xem nội dung tin nhắn của một đơn cụ thể | ✓* | ✓* | — | ✓* |
| Xem ảnh giấy tờ tuỳ thân | — | ✓* | — | ✓* |
| Xem thông tin tài khoản nhận tiền (4 số cuối) | — | — | ✓ | ✓ |
| Sửa thông tin hồ sơ người dùng | ✓* | — | — | ✓ |
| Gửi cảnh cáo | ✓ | ✓ | — | ✓ |
| Hạn chế một phần | — | ✓ | — | ✓ |
| Tạm khoá tài khoản | — | ✓ | — | ✓ |
| Khoá vĩnh viễn | — | — | — | ✓ |
| Khôi phục tài khoản đã khoá | — | ✓* | — | ✓ |
| Gỡ tin đăng / ảnh / đánh giá | — | ✓ | — | ✓ |
| Buộc đổi mật khẩu, huỷ mọi phiên đăng nhập | ✓ | ✓ | — | ✓ |
| Buộc xác minh lại danh tính | — | ✓ | — | ✓ |
| Hoàn tiền thủ công | — | — | ✓ | ✓ |
| Điều chỉnh khoản trả cho chủ nhà | — | — | ✓ | ✓ |
| Đăng nhập thay mặt người dùng | ✓* | — | — | ✓ |
| Hợp nhất tài khoản trùng | — | — | — | ✓ |
| Xoá dữ liệu theo yêu cầu người dùng | — | — | — | ✓ |
| Tạo và phân quyền tài khoản admin | — | — | — | ✓ |
| Xem nhật ký thao tác của admin khác | — | — | — | ✓ |

`✓*` = được phép nhưng **cần lý do bắt buộc và bị ghi nhật ký riêng**, có thể yêu cầu duyệt cấp trên tuỳ tham số.

Một người có thể mang nhiều vai. Quyền là hợp của các vai.

---

## 3. Quản lý chính tài khoản admin

- Chỉ Quản trị tối cao tạo được tài khoản admin. Không cho tự đăng ký.
- **Bắt buộc bảo mật 2 lớp.** Không bật thì không đăng nhập được, không có ngoại lệ.
- Phiên làm việc tự hết hạn sau **`[THAM SỐ QT-A]`** phút không hoạt động.
- Giới hạn theo địa chỉ mạng hoặc thiết bị đã đăng ký (tuỳ chọn, nên bật cho vai Tài chính và Tối cao).
- **Rà soát quyền định kỳ mỗi 3 tháng:** hệ thống liệt kê ai đang có quyền gì, ai chưa dùng quyền nào quá 90 ngày để thu hồi.
- Admin nghỉ việc → thu hồi quyền ngay lập tức, huỷ mọi phiên đang mở, không xoá tài khoản (để giữ nhật ký).
- Cảnh báo tự động khi: một admin xem quá nhiều hồ sơ trong thời gian ngắn, xem hồ sơ không liên quan tới ticket nào đang mở, thao tác ngoài giờ làm việc, hoặc tra cứu cùng một người dùng nhiều lần.

---

## 4. Tìm và xem hồ sơ người dùng

**Tìm được bằng:** email, số điện thoại, tên, mã đơn đặt, mã tin đăng, mã giao dịch.

**Hồ sơ hiển thị:**
- Thông tin cơ bản, trạng thái tài khoản, ngày tham gia, lần hoạt động gần nhất
- Trạng thái xác minh (email, điện thoại, giấy tờ)
- Vai trò: khách, chủ nhà, co-host, danh hiệu
- Danh sách tin đăng và tình trạng từng cái
- Lịch sử đơn đặt hai chiều, số đơn huỷ, tỉ lệ huỷ
- Lịch sử đánh giá viết và nhận
- Lịch sử vi phạm: cảnh cáo, hạn chế, khoá, hồ sơ tranh chấp, báo cáo bị nhận
- Số dư, thẻ quà tặng, phương thức thanh toán (chỉ 4 số cuối)
- Thiết bị và địa chỉ mạng đăng nhập gần đây
- Tài khoản có liên quan (trùng thiết bị, trùng số điện thoại, trùng tài khoản nhận tiền)

**Admin không bao giờ xem được:** mật khẩu, số thẻ đầy đủ, mã CVV, mã OTP, nội dung tin nhắn không liên quan tới đơn đang xử lý.

**Ảnh giấy tờ tuỳ thân:** chỉ vai Kiểm duyệt và Tối cao, mỗi lần xem phải nhập lý do, mỗi lần xem ghi một dòng nhật ký riêng, ảnh có đóng dấu mờ tên admin đang xem.

---

## 5. Các mức xử lý tài khoản

Từ nhẹ tới nặng. **Bắt buộc đi tuần tự**, trừ vi phạm nghiêm trọng ở §5.6.

### 5.1. Cảnh cáo
Gửi thông báo nêu rõ vi phạm điều nào, dẫn chiếu chính sách, yêu cầu khắc phục. Tài khoản hoạt động bình thường. Ghi vào hồ sơ vi phạm.

### 5.2. Hạn chế một phần
Chặn đúng hành vi có vấn đề, giữ nguyên phần còn lại:

| Hạn chế | Hậu quả |
|---|---|
| Không được đặt đơn mới | Đơn đang có vẫn chạy bình thường |
| Không được đăng tin mới | Tin cũ vẫn hiển thị |
| Tin đăng bị ẩn khỏi tìm kiếm | Đơn đã xác nhận vẫn thực hiện |
| Không được viết đánh giá | |
| Không được nhắn tin cho người mới | Cuộc trò chuyện đang có vẫn dùng được |
| Khoản chuyển tiền bị giữ lại | Cho tới khi làm rõ |

Hạn chế có thời hạn hoặc tới khi khắc phục xong. Phải nêu rõ điều kiện gỡ.

### 5.3. Tạm khoá
Không đăng nhập được. Có thời hạn hoặc chờ điều tra.
**Xử lý đơn đang có — xem §6, đây là phần dễ gây thiệt hại nhất.**

### 5.4. Khoá vĩnh viễn
Chỉ Quản trị tối cao. Chỉ dùng cho: gian lận tiền, giả mạo danh tính, đe doạ an toàn người khác, tái phạm nhiều lần sau khi đã cảnh cáo và tạm khoá.
Chặn cả việc tạo tài khoản mới bằng cùng email, số điện thoại, thiết bị, tài khoản nhận tiền.

### 5.5. Khôi phục
Gỡ hạn chế hoặc mở khoá. Phải ghi lý do khôi phục. Tài khoản đã khoá vĩnh viễn chỉ Quản trị tối cao mở được.

### 5.6. Vi phạm nghiêm trọng — được nhảy thẳng lên tạm khoá
Đe doạ hoặc bạo lực · nội dung liên quan trẻ em · gian lận thanh toán có bằng chứng · giả mạo giấy tờ · chiếm đoạt tài khoản người khác · lừa đảo có tổ chức.
Xử lý ngay, thông báo sau, nhưng vẫn phải ghi lý do và chuyển hồ sơ cho Quản trị tối cao xem lại trong 24 giờ.

---

## 6. Khoá tài khoản thì các đơn đang có xử lý thế nào

Đây là chỗ dễ gây thiệt hại nhất. **Hệ thống phải hiện cảnh báo trước khi admin bấm xác nhận**, liệt kê rõ sẽ ảnh hưởng bao nhiêu đơn và bao nhiêu tiền.

### Khoá một chủ nhà

| Loại đơn | Xử lý mặc định |
|---|---|
| Khách đang ở | **Không đụng vào.** Để khách ở hết kỳ. |
| Đã xác nhận, chưa nhận phòng | Huỷ, hoàn 100% cho khách, **không tính phạt huỷ cho chủ nhà** (vì sàn huỷ, không phải chủ nhà huỷ), gửi khách danh sách chỗ thay thế |
| Đang chờ chủ nhà duyệt | Tự động huỷ yêu cầu, báo khách |
| Tiền chưa chuyển | Giữ lại cho tới khi xử lý xong vi phạm; không tịch thu tự động |
| Tin đăng | Ẩn khỏi tìm kiếm ngay |

Ngoại lệ: vi phạm liên quan an toàn khách → hỗ trợ chuyển khách đang ở sang chỗ khác ngay, chi phí theo StayShield (`06 §2.3`).

### Khoá một khách

| Loại đơn | Xử lý mặc định |
|---|---|
| Đang ở | Để ở hết kỳ, trừ khi có nguy hiểm cho chủ nhà |
| Đã xác nhận, chưa nhận phòng | Huỷ, hoàn tiền theo chính sách huỷ **hoặc** hoàn 100% tuỳ mức độ vi phạm — admin chọn và ghi lý do |
| Có tranh chấp đang mở | Giữ tài khoản mở đủ để họ phản hồi, không được cắt quyền tự vệ |
| Số dư khuyến mãi | Đóng băng, không xoá |

**Quy tắc chung:** người đang trong chuyến đi không bị bỏ rơi giữa đường vì quyết định hành chính.

---

## 7. Đăng nhập thay mặt người dùng

Dùng để hỗ trợ khi không tả được lỗi qua lời. Rủi ro lạm dụng rất cao nên phải siết:

1. **Chỉ mở được từ một ticket hỗ trợ đang mở**, không mở tự do từ trang hồ sơ
2. Nhập lý do bắt buộc trước khi vào
3. Phiên tối đa **`[THAM SỐ QT-B]`** phút, tự thoát
4. **Người dùng nhận thông báo** rằng nhân viên hỗ trợ đã truy cập tài khoản, kèm thời điểm và lý do — trừ trường hợp điều tra gian lận có phê duyệt riêng
5. Trên màn hình admin luôn hiện dải cảnh báo rõ đang ở chế độ thay mặt và đang là ai
6. **Cấm tuyệt đối trong chế độ này:** đổi mật khẩu, đổi email, đổi số điện thoại, đổi tài khoản nhận tiền, thêm hoặc xoá phương thức thanh toán, tạo đơn mới, huỷ đơn, rút tiền, xoá tài khoản
7. Mọi thao tác trong phiên ghi nhật ký kèm dấu "thực hiện bởi admin X thay mặt người dùng Y" — không được ghi như thể người dùng tự làm

---

## 8. Khiếu nại quyết định

- Người dùng khiếu nại **một lần** trong **`[THAM SỐ QT-C]`** ngày kể từ khi nhận quyết định
- Người xét lại **phải khác** người ra quyết định ban đầu
- Trả lời trong 7 ngày làm việc
- Kết quả: giữ nguyên / giảm mức / gỡ bỏ hoàn toàn. Gỡ bỏ thì xoá luôn khỏi hồ sơ vi phạm.
- Thông báo kết quả phải nêu lý do, không được trả lời cụt lủn

---

## 9. Yêu cầu dữ liệu cá nhân

| Yêu cầu | Xử lý |
|---|---|
| Xuất toàn bộ dữ liệu của tôi | Tạo bản xuất, gửi qua đường dẫn có hạn, xong trong `[THAM SỐ QT-D]` ngày |
| Sửa thông tin sai | Người dùng tự sửa được phần lớn; phần đã xác minh cần admin duyệt |
| Xoá tài khoản | Ẩn danh hoá: xoá tên, ảnh, email, điện thoại, giấy tờ. **Giữ lại** đơn đặt, giao dịch, ghi sổ tiền và nhật ký vì nghĩa vụ kế toán và pháp lý |
| Xoá đánh giá đã viết | Không xoá — đánh giá thuộc về cộng đồng. Chỉ ẩn tên người viết |

Không xoá được khi: còn đơn chưa hoàn tất, còn tranh chấp mở, còn nợ sàn, hoặc đang bị điều tra.

---

## 10. Chống lạm quyền

Ngoài nhật ký, cần theo dõi chủ động:

- Bảng thống kê mỗi admin: số hồ sơ đã xem, số quyết định đã ra, tỉ lệ bị khiếu nại thành công
- Cảnh báo khi một admin có tỉ lệ khiếu nại thành công cao bất thường
- Cảnh báo khi admin xem hồ sơ mà không có ticket liên quan
- Cảnh báo khi admin hoàn tiền hoặc điều chỉnh khoản chuyển vượt ngưỡng `[THAM SỐ QT-E]` — bắt buộc duyệt hai người
- Rà soát ngẫu nhiên **`[THAM SỐ QT-F]`%** quyết định mỗi tháng, do người khác đọc lại
- Quản trị tối cao thao tác gì cũng bị ghi và gửi thông báo cho một người thứ hai

---

## 11. Chức năng cần xây

| Mã | Chức năng |
|---|---|
| QT-U-01 | Tìm người dùng theo email, điện thoại, tên, mã đơn, mã tin đăng, mã giao dịch — **đã làm** |
| QT-U-02 | Trang hồ sơ người dùng tổng hợp theo §4 — **đã làm** |
| QT-U-03 | Bảng tài khoản có liên quan (trùng thiết bị, số điện thoại, tài khoản nhận tiền) — **đã làm** |
| QT-U-04 | Gửi cảnh cáo kèm dẫn chiếu chính sách — **đã làm** |
| QT-U-05 | Áp và gỡ hạn chế một phần theo §5.2 — **đã làm**, chặn thật ở đặt đơn, đăng tin, đánh giá, nhắn tin, chuyển tiền |
| QT-U-06 | Tạm khoá, khoá vĩnh viễn, khôi phục — **đã làm** (`Sanctions.cs`, thang bậc bắt buộc tuần tự) |
| QT-U-07 | **Màn hình xem trước hậu quả** trước khi khoá — **đã làm** (`SuspensionImpact.cs`) |
| QT-U-08 | Tự động xử lý đơn đang có theo §6, cho admin chọn phương án và ghi lý do — **đã làm**, và **thực thi thật**: `UserAdminController.ExecuteFalloutAsync` chạy đúng bảng §6 qua chính đường huỷ đơn thường (`PostCancellation`), nên sổ sách vẫn cân. Đơn bị sàn huỷ ghi là `CancelledBy = Platform` và **không** tính vào lịch sử huỷ của khách |
| QT-U-09 | Buộc đổi mật khẩu, huỷ mọi phiên đăng nhập — **đã làm** |
| QT-U-10 | Buộc xác minh lại danh tính — **đã làm** |
| QT-U-11 | Xem ảnh giấy tờ có kiểm soát: nhập lý do, đóng dấu mờ, ghi nhật ký riêng — **đã làm**. Ảnh **không** nằm trong thư mục công khai: `IdentityFilesController` chỉ mở cho chính chủ, hoặc cho admin vừa ghi lý do xem trong 15 phút gần nhất |
| QT-U-12 | Đăng nhập thay mặt theo đúng ràng buộc §7 — **đã làm** (`Impersonation.cs` + middleware). Chặn theo **đường dẫn thật** của ứng dụng, có test khoá lại; mọi thao tác trong phiên ghi nhật ký "admin X thay mặt Y"; im lặng điều tra gian lận cần **một Tối cao khác** phê duyệt; dải cảnh báo hiện trên mọi trang |
| QT-U-13 | Hợp nhất hai tài khoản trùng của cùng một người — **đã làm**, chuyển đơn/số dư/thẻ/tin đăng rồi ẩn danh tài khoản cũ |
| QT-U-14 | Xử lý yêu cầu xuất và xoá dữ liệu cá nhân — **đã làm** (`DataRequests.cs`, ẩn danh tại chỗ). Người dùng **tự gửi yêu cầu** trong phần Tài khoản; bản xuất giao bằng **đường dẫn có hạn 7 ngày**; ẩn danh cũng xoá tên trên đánh giá đã viết |
| QT-U-15 | Hồ sơ vi phạm: lịch sử đầy đủ các lần bị xử lý và kết quả khiếu nại — **đã làm** |
| QT-U-16 | Luồng khiếu nại: tiếp nhận, phân cho người khác, ra kết quả — **đã làm** (`Appeals.cs`). Người dùng **nộp được thật**: còn đăng nhập thì vào `/account/sanctions`, đã bị khoá thì theo liên kết kèm mã trong thư (`/appeal?token=…`) |
| QT-A-01 | Tạo, phân quyền, thu hồi tài khoản admin — **đã làm**, thu hồi hết quyền thì huỷ mọi phiên và giữ lại tài khoản để giữ nhật ký |
| QT-A-02 | Bắt buộc bảo mật 2 lớp cho admin — **đã làm**, chặn ngay ở bước đăng nhập |
| QT-A-03 | Nhật ký thao tác admin, không xoá được — **đã làm**, có trigger PostgreSQL nên psql cũng không xoá được |
| QT-A-04 | Rà soát quyền định kỳ mỗi quý — **đã làm** (cột trong bảng giám sát: quyền chưa dùng 90 ngày, tới hạn rà soát), có nút cấp/thu hồi quyền và ký rà soát ngay trên bảng |
| QT-A-05 | Bảng cảnh báo lạm quyền theo §10 — **đã làm** (`AdminOversight.cs`), **cả bốn** cảnh báo của §3 đều bắn: xem nhiều hồ sơ trong một giờ, xem không gắn hồ sơ, tra cùng một người nhiều lần, thao tác ngoài giờ (giờ Việt Nam) |
| QT-A-06 | Duyệt hai người cho thao tác vượt ngưỡng tiền — **đã làm**, hoàn tiền **và mở khoá khoản chuyển** ≥ 10 triệu đều chờ người thứ hai. Bàn tài chính đi qua `AdminGate` nên có cả kiểm tra xung đột §1.3 |
| QT-A-07 | Chặn admin thao tác lên tài khoản có liên quan tới chính mình — **đã làm** (`AdminConflict.cs`) |

---

## 12. Tham số cần chốt

| Mã | Tham số | Gợi ý | Giá trị chốt |
|---|---|---|---|
| QT-A | Phiên admin hết hạn sau bao lâu không hoạt động | 30 phút | 30 phút *(đang chạy theo gợi ý — `AuthService.CurrentUserAsync` thu hồi phiên admin quá 30 phút không hoạt động; người dùng thường vẫn 30 ngày)* |
| QT-B | Phiên đăng nhập thay mặt tối đa | 30 phút | 30 phút *(đang chạy theo gợi ý)* |
| QT-C | Hạn khiếu nại quyết định | 30 ngày | 30 ngày *(đang chạy theo gợi ý)* |
| QT-D | Hạn hoàn thành yêu cầu xuất dữ liệu | 30 ngày | 30 ngày *(đang chạy theo gợi ý)* |
| QT-E | Ngưỡng tiền phải duyệt hai người | 10 triệu ₫ | 10 triệu ₫ *(đang chạy theo gợi ý)* |
| QT-F | Tỉ lệ quyết định bị rà soát ngẫu nhiên | 5% | 5% *(đang chạy theo gợi ý)* |
| — | Vai Hỗ trợ có được đăng nhập thay mặt không? | có, nhưng phải từ ticket | |
| — | Cần duyệt cấp trên trước khi tạm khoá không? | không, nhưng rà soát sau | |

---

## 13. Kịch bản bắt buộc phải chạy thử

1. Vai Hỗ trợ thử khoá tài khoản → bị từ chối, nêu rõ thiếu quyền
2. Admin thử thao tác lên tài khoản của chính mình → bị chặn
3. Khoá một chủ nhà có 1 khách đang ở và 5 đơn sắp tới → khách đang ở không bị ảnh hưởng, 5 đơn kia được hoàn 100% và chủ nhà không bị tính phạt huỷ
4. Đăng nhập thay mặt → thử đổi tài khoản nhận tiền → bị chặn
5. Đăng nhập thay mặt → hết 30 phút → tự thoát, người dùng nhận thông báo
6. Hoàn tiền 15 triệu → yêu cầu người thứ hai duyệt
7. Người dùng khiếu nại → hệ thống không cho chính người ra quyết định xét lại
8. Xem ảnh giấy tờ → bắt nhập lý do → ghi nhật ký riêng → ảnh có dấu mờ tên admin
9. Xoá tài khoản có đơn đã hoàn tất → hồ sơ bị ẩn danh nhưng đơn và ghi sổ tiền vẫn còn nguyên
10. Thử xoá một dòng nhật ký admin → không có cách nào xoá được, kể cả bằng vai Tối cao

---

## 14. Ghi chú khi làm

**Kịch bản §13 chạy bằng `scripts/admin_acceptance.py`** — cần server chạy với
`ASPNETCORE_ENVIRONMENT=Development` vì §3 bắt admin có 2 lớp, mà chỉ bản dev mới
trả mã về qua API.

**Tham số §12 chưa có giá trị chốt.** Sáu tham số đang chạy đúng theo cột "gợi ý"
của chính tài liệu này. Đổi ở một nơi: `AdminActions`, `Impersonation`, `Appeals`,
`DataRequests`, `AdminOversight`.

**Hai chỗ tự quyết, cần khách xác nhận:**

1. **"Ticket hỗ trợ đang mở" của §7.1** hiện ánh xạ sang **hồ sơ ở Trung tâm giải
   quyết** (`ResolutionCase`) — sàn chưa có hệ thống ticket riêng. Hồ sơ phải đang
   mở *và* liên quan tới đúng người thì mới vào chế độ thay mặt được. Nếu sau này
   có ticket riêng thì đổi một chỗ trong `AdminOversightController.Impersonate`.

2. **§1.3 chặn *quyết định*, không chặn *đọc*.** Chặn cả việc mở hồ sơ khi trùng
   dấu vân tay thiết bị sẽ làm nhân viên hỗ trợ không làm việc được; lượt đọc vẫn
   bị ghi và vẫn vào bảng cảnh báo §10.

3. **"Đã từng ra quyết định với người này" không chặn bước kế tiếp.** §1.3 liệt kê
   ba thứ: tài khoản của chính mình, người thân, và người mình có liên quan trong
   đơn đặt — không có "đã từng xử lý". Chặn ở đây làm **thang bậc §5 không đi được**,
   vì thang bậc chính là leo thang: người viết cảnh cáo phải là người viết được hạn
   chế tiếp theo. Quy tắc thật sự cần người khác đọc là §8 (khiếu nại), và nó nằm
   đúng chỗ của nó trong `Appeals.MayReview`. Dấu hiệu này vẫn được ghi để bảng
   giám sát §10 nhìn thấy ai hay quyết định về cùng một người.

**Nhật ký quản trị được khoá ở tầng cơ sở dữ liệu**, không chỉ ở ứng dụng: có
trigger PostgreSQL chặn `update`/`delete`/`truncate` trên bảng `admin_audit`, nên
"kể cả Quản trị tối cao" (§1.2) đúng cả khi có người vào thẳng psql.

**Đơn bị sàn huỷ không phải là đơn khách huỷ.** Khi khoá một chủ nhà, các đơn sắp
tới bị huỷ với `CancelledBy = Platform` và trạng thái nằm ở phía chủ nhà. Ghi sang
phía khách sẽ làm khách mang tiếng huỷ đơn họ không huỷ, và tệ hơn là **tiêu mất
quyền hoàn phí dịch vụ 3 lần/năm** của `docs/03 §4`. Mọi chỗ đếm số lần huỷ để
đánh giá một người đều lọc theo `CancelledBy`.
