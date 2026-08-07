"""docs/08 §13 — the ten scenarios user administration has to pass.

Runs against a live server the same way scripts/acceptance.py does. It creates
its own throwaway admins and users, so it can be run repeatedly without a reset.

    ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/StayHost.Web
    python scripts/admin_acceptance.py

The environment matters: docs/08 §3 makes two-factor compulsory for admins, and
only a development build hands the code back over the API.
"""
import http.cookiejar
import json
import subprocess
import time
import urllib.error
import urllib.request

B = "http://localhost:5199"
PW = "stayhost123"
RUN = str(int(time.time()))[-6:]
REASON = "Kiem tra kich ban nghiem thu cua docs/08"


def opener():
    return urllib.request.build_opener(
        urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))


def call(op, p, b=None, m=None):
    d = json.dumps(b).encode() if b is not None else None
    r = urllib.request.Request(
        B + p, data=d,
        headers={"Content-Type": "application/json"} if d else {},
        method=m or ("POST" if d else "GET"))
    try:
        x = op.open(r)
        raw = x.read().decode()
        try:
            return x.status, (json.loads(raw) if raw.strip() else None)
        except json.JSONDecodeError:
            return x.status, {"raw": raw[:300]}
    except urllib.error.HTTPError as e:
        raw = e.read().decode()
        try:
            return e.code, json.loads(raw)
        except json.JSONDecodeError:
            return e.code, {"raw": raw[:300]}


def sql(q):
    """Two things need the database directly: making the first admin, which
    docs/08 §3 says only a Super may do through the platform, and scenario 10,
    which is about what happens when somebody bypasses the platform entirely."""
    out = subprocess.run(
        ["docker", "exec", "stayhost-db", "psql", "-U", "stayhost", "-d", "stayhost",
         "-t", "-A", "-c", q],
        capture_output=True, text=True, encoding="utf-8")
    return out.returncode, (out.stdout or "").strip(), (out.stderr or "").strip()


def sql_ok(q):
    code, out, err = sql(q)
    if code != 0:
        raise SystemExit(f"psql failed: {err}")
    return out


RESULTS = []


def record(n, name, ok, detail=""):
    RESULTS.append(ok)
    print(f"{'PASS' if ok else 'FAIL'} {n:2d}. {name}")
    if detail:
        print(f"        {detail}")


def register(email, name):
    op = opener()
    st, res = call(op, "/api/account/register",
                   {"email": email, "password": PW, "fullName": name,
                    "dateOfBirth": "1990-01-01"})
    if st not in (200, 201):
        raise SystemExit(f"register {email} failed: {st} {res}")
    return op, int(sql_ok(f"select \"Id\" from users where \"Email\"='{email}'"))


def sign_in(email):
    """docs/08 §3 — an admin signs in through the code step, no exceptions."""
    op = opener()
    st, res = call(op, "/api/account/login", {"email": email, "password": PW})
    if res and res.get("challenge"):
        if not res.get("devCode"):
            raise SystemExit(
                "Khong lay duoc ma 2 lop. Chay server voi ASPNETCORE_ENVIRONMENT=Development.")
        call(op, "/api/account/two-factor",
             {"challenge": res["challenge"], "code": res["devCode"]})
    return op


def make_admin(slug, name, scope):
    """scope is the AdminScope flag: 1 Support, 2 Moderation, 4 Finance, 31 Super."""
    email = f"{slug}{RUN}@stayhost.vn"
    _, uid = register(email, name)
    sql_ok(f'update users set "Role"=2, "AdminScope"={scope}, "TwoFactorEnabled"=true where "Id"={uid}')
    return sign_in(email), uid


print("=" * 70)
print("docs/08 §13 — nghiệm thu quản trị người dùng")
print("=" * 70)

support, support_id = make_admin("support", "Hỗ trợ Test", 1)
moderation, mod_id = make_admin("mod", "Kiểm duyệt Test", 2)
moderation2, mod2_id = make_admin("mod2", "Kiểm duyệt Hai", 2)
finance, fin_id = make_admin("fin", "Tài chính Test", 4)
super_admin, super_id = make_admin("super", "Tối cao Test", 31)

_, victim_id = register(f"victim{RUN}@stayhost.vn", "Người Dùng Test")


# --- 1. Support tries to lock somebody out -----------------------------------

st1, res1 = call(support, f"/api/admin/users/{victim_id}/sanction",
                 {"level": "Suspension", "reason": REASON})

record(1, "Vai Hỗ trợ thử khoá tài khoản → bị từ chối, nêu rõ thiếu quyền",
       st1 == 403 and "Kiểm duyệt" in (res1 or {}).get("message", ""),
       f"{st1}: {(res1 or {}).get('message', '')[:90]}")


# --- 2. An admin acting on their own account ---------------------------------

st2, res2 = call(moderation, f"/api/admin/users/{mod_id}/sanction",
                 {"level": "Warning", "reason": REASON, "policy": "docs/08"})

record(2, "Admin thử thao tác lên tài khoản của chính mình → bị chặn",
       st2 == 403 and "chính tài khoản của mình" in (res2 or {}).get("message", ""),
       f"{st2}: {(res2 or {}).get('message', '')[:90]}")


# --- 3. Locking a host mid-season --------------------------------------------
# One guest already staying, five confirmed stays still to come.

host_email = f"lockhost{RUN}@stayhost.vn"
host_op, host_id = register(host_email, "Chủ Nhà Test")
call(host_op, "/api/account/become-host", {})
host_profile = int(sql_ok(f'select "Id" from hosts where "UserId"={host_id}'))

listing_ids = []
for i in range(6):
    st, made = call(host_op, "/api/host/listings", {
        "title": f"Nhà thử nghiệm {RUN}-{i}", "city": "Đà Nẵng",
        "typeKey": "House", "roomTypeKey": "EntirePlace",
        "bedrooms": 1, "beds": 1, "bathrooms": 1, "maxGuests": 2,
        "pricePerNight": 1000000, "cleaningFee": 0, "minNights": 1,
        "instantBook": True, "isPublished": True, "cancellationTier": "Flexible",
        "highlight": None,
        "description": "Nhà dùng cho kịch bản nghiệm thu docs/08, không phải tin thật.",
        "latitude": 16.05, "longitude": 108.2,
        # A listing needs five photos before it may go public (docs/01 CN-07).
        "images": [f"https://images.pexels.com/photos/271624/pexels-photo-271624.jpeg?n={k}"
                   for k in range(5)],
        "amenityKeys": ["wifi"]})
    if st in (200, 201):
        listing_ids.append(made["id"])
        call(host_op, f"/api/host/listings/{made['id']}/publish", {"published": True})

guest_op, guest_id = register(f"lockguest{RUN}@stayhost.vn", "Khách Test")

booked = []
for i, lid in enumerate(listing_ids):
    start = f"2027-0{(i % 6) + 3}-10"
    end = f"2027-0{(i % 6) + 3}-12"
    st, bk = call(guest_op, "/api/bookings", {
        "listingId": lid, "checkIn": start, "checkOut": end, "guests": 1,
        "guestName": "Khách Test", "guestEmail": f"lockguest{RUN}@stayhost.vn",
        "paymentMethod": "card", "cardLast4": "4242"})
    if st in (200, 201):
        call(guest_op, f"/api/bookings/{bk['id']}/pay",
             {"paymentMethod": "card", "cardLast4": "4242", "idempotencyKey": f"lock{RUN}-{i}"})
        booked.append(bk["id"])

# The first stay is under way: somebody is in that house tonight.
if booked:
    sql_ok(f'update bookings set "Status"=3 where "Id"={booked[0]}')

st3, preview = call(moderation, f"/api/admin/users/{host_id}/lock-preview")

staying = (preview or {}).get("guestsStaying", -1)
cancelled = (preview or {}).get("bookingsCancelled", -1)
full_refund = all(l["action"] == "CancelRefundFull"
                  for l in (preview or {}).get("lines", [])
                  if l["action"].startswith("Cancel") and l["action"] != "CancelRequest")
no_penalty = all("không tính phạt huỷ cho chủ nhà" in l["note"]
                 for l in (preview or {}).get("lines", [])
                 if l["action"] == "CancelRefundFull")

record(3, "Khoá chủ nhà có 1 khách đang ở và 5 đơn sắp tới",
       st3 == 200 and staying == 1 and cancelled == len(booked) - 1 and full_refund and no_penalty,
       f"{staying} khách đang ở không bị đụng, {cancelled} đơn huỷ hoàn 100%, "
       f"không phạt chủ nhà: {no_penalty}")


# --- 4. Impersonation cannot change where the money goes ---------------------
# docs/08 §7.1: it opens from a case, so the guest raises one first.

st_case, case = call(guest_op, "/api/resolutions", {
    "bookingId": booked[0],
    "kind": "Other", "amountClaimed": 100000,
    "description": "Ho so mo de thu kich ban dang nhap thay mat docs/08 §7."})

case_id = (case or {}).get("id")
if case_id is None:
    print(f"        (mở hồ sơ: {st_case} {str(case)[:150]})")

st4a, imp = call(support, "/api/admin/impersonate",
                 {"userId": guest_id, "ticketId": case_id, "reason": REASON})

st4b, blocked = call(support, "/api/host/payout",
                     {"bankName": "Vietcombank", "accountName": "NGUYEN VAN A",
                      "accountNumber": "0071000987654", "schedule": "PerBooking"},
                     m="PUT")

record(4, "Đăng nhập thay mặt → thử đổi tài khoản nhận tiền → bị chặn",
       st4a == 200 and st4b == 403 and "thay mặt" in (blocked or {}).get("message", ""),
       f"vào phiên {st4a} {(imp or {}).get('message','')[:60]}, "
       f"đổi tài khoản {st4b}: {(blocked or {}).get('message', '')[:60]}")


# --- 5. The session ends by itself, and the person was told ------------------

sql_ok(f'update impersonation_sessions set "ExpiresAt" = now() - interval \'1 minute\' '
       f'where "AdminUserId"={support_id} and "EndedAt" is null')

st5, still = call(support, "/api/admin/impersonate/current")

told = sql_ok(f'select count(*) from notifications where "UserId"={guest_id} '
              f"and \"Title\" like '%truy cập tài khoản%'")

record(5, "Đăng nhập thay mặt → hết giờ → tự thoát, người dùng nhận thông báo",
       st5 == 204 and int(told or 0) >= 1,
       f"phiên còn lại: {st5} (204 = đã thoát), thông báo gửi cho khách: {told}")

call(support, "/api/admin/impersonate/end", {})


# --- 6. A large refund needs a second signature ------------------------------

paid_booking = booked[1] if len(booked) > 1 else booked[0]
sql_ok(f'update bookings set "DepositPaid"=20000000, "Total"=20000000 where "Id"={paid_booking}')
sql_ok(f'update payments set "Amount"=20000000, "Status"=2 where "BookingId"={paid_booking}')

st6, res6 = call(finance, f"/api/admin/finance/transactions/{paid_booking}/refund",
                 {"amount": 15000000, "reason": "Kiem tra nguong duyet hai nguoi docs/08 §10"})

approval = sql_ok('select count(*) from money_approvals where "Amount"=15000000')

record(6, "Hoàn tiền 15 triệu → yêu cầu người thứ hai duyệt",
       st6 == 202 and (res6 or {}).get("needsSecondApproval") is True and int(approval or 0) >= 1,
       f"{st6}: {(res6 or {}).get('message', '')[:80]}")


# --- 7. An appeal is never read by the person who made the call --------------

st7a, _ = call(moderation, f"/api/admin/users/{victim_id}/sanction",
               {"level": "Warning", "reason": REASON, "policy": "docs/03 §9"})

sanction_id = int(sql_ok(f'select "Id" from sanctions where "UserId"={victim_id} order by "Id" desc limit 1'))

sql_ok(f'insert into appeals ("SanctionId","UserId","Argument","Status","CreatedAt","DueBy") '
       f"values ({sanction_id},{victim_id},'Toi khong dong y voi quyet dinh nay.',0,now(),now() + interval '7 days')")

appeal_id = int(sql_ok(f'select "Id" from appeals where "SanctionId"={sanction_id}'))

outcome = ("Chung toi da xem lai toan bo bang chung va giu nguyen quyet dinh "
           "vi anh tin dang van khong phai cua cho nghi nhu da neu.")

st7b, res7b = call(moderation, f"/api/admin/appeals/{appeal_id}/decide",
                   {"result": "Upheld", "outcome": outcome})

st7c, _ = call(moderation2, f"/api/admin/appeals/{appeal_id}/decide",
               {"result": "Upheld", "outcome": outcome})

record(7, "Người dùng khiếu nại → không cho chính người ra quyết định xét lại",
       st7a == 200 and st7b == 403 and st7c == 200,
       f"người ra quyết định: {st7b} (403 = chặn đúng), người khác: {st7c}")


# --- 8. Looking at somebody's identity card ----------------------------------

sql_ok(f'insert into identity_checks '
       f'("UserId","Document","DocumentLast4","FrontImageUrl","BackImageUrl","SelfieImageUrl","Status","SubmittedAt") '
       f"values ({victim_id},0,'1234','/uploads/front.jpg','/uploads/back.jpg','/uploads/selfie.jpg',0,now())")

st8a, res8a = call(moderation, f"/api/admin/users/{victim_id}/identity", {"reason": ""})
st8b, res8b = call(moderation, f"/api/admin/users/{victim_id}/identity", {"reason": REASON})
st8c, _ = call(finance, f"/api/admin/users/{victim_id}/identity", {"reason": REASON})

separate_line = sql_ok(f'select count(*) from admin_audit '
                       f"where \"Action\" like 'admin.read.viewidentitydocuments' "
                       f"and \"Target\"='user:{victim_id}'")

watermarked = "Kiểm duyệt Test" in (res8b or {}).get("watermark", "")

record(8, "Xem giấy tờ → bắt nhập lý do → nhật ký riêng → ảnh có dấu mờ tên admin",
       st8a == 400 and st8b == 200 and st8c == 403
       and int(separate_line or 0) >= 1 and watermarked,
       f"thiếu lý do {st8a}, có lý do {st8b}, vai Tài chính {st8c}, "
       f"dòng nhật ký riêng {separate_line}, dấu mờ: {(res8b or {}).get('watermark', '')[:40]}")


# --- 9. Erasing an account that has already travelled ------------------------

erase_op, erase_id = register(f"erase{RUN}@stayhost.vn", "Người Xoá Test")

st, done_booking = call(erase_op, "/api/bookings", {
    "listingId": listing_ids[-1], "checkIn": "2027-11-10", "checkOut": "2027-11-12",
    "guests": 1, "guestName": "Người Xoá Test", "guestEmail": f"erase{RUN}@stayhost.vn",
    "paymentMethod": "card", "cardLast4": "4242"})

if st in (200, 201):
    call(erase_op, f"/api/bookings/{done_booking['id']}/pay",
         {"paymentMethod": "card", "cardLast4": "4242", "idempotencyKey": f"erase{RUN}"})
    sql_ok(f'update bookings set "Status"=4 where "Id"={done_booking["id"]}')

ledger_before = sql_ok(f'select count(*) from ledger_entries where "BookingId"={done_booking["id"]}')

sql_ok(f'insert into data_requests ("UserId","Kind","Status","CreatedAt","DueBy") '
       f"values ({erase_id},1,0,now(),now() + interval '30 days')")
request_id = int(sql_ok(f'select "Id" from data_requests where "UserId"={erase_id}'))

st9, res9 = call(super_admin, f"/api/admin/data-requests/{request_id}/erase", {"reason": REASON})

name_after = sql_ok(f'select "FullName" from users where "Id"={erase_id}')
booking_after = sql_ok(f'select count(*) from bookings where "Id"={done_booking["id"]}')
ledger_after = sql_ok(f'select count(*) from ledger_entries where "BookingId"={done_booking["id"]}')

record(9, "Xoá tài khoản có đơn đã hoàn tất → ẩn danh, đơn và sổ tiền còn nguyên",
       st9 == 200 and "Người Xoá Test" not in name_after
       and int(booking_after or 0) == 1 and ledger_after == ledger_before,
       f"{st9} | tên sau khi xoá: {name_after}, "
       f"đơn còn: {booking_after}, bút toán {ledger_before} → {ledger_after}")


# --- 10. The audit log cannot be edited, by anyone ---------------------------

code_del, _, err_del = sql('delete from admin_audit where "Id" = (select min("Id") from admin_audit)')
code_upd, _, err_upd = sql('update admin_audit set "Note" = \'sua trom\' '
                           'where "Id" = (select min("Id") from admin_audit)')

blocked_both = code_del != 0 and code_upd != 0

record(10, "Thử xoá một dòng nhật ký admin → không có cách nào xoá được",
       blocked_both and "docs/08" in (err_del + err_upd),
       f"xoá: {'bị chặn' if code_del else 'THÀNH CÔNG'}, "
       f"sửa: {'bị chặn' if code_upd else 'THÀNH CÔNG'}")


print()
print("=" * 70)
print(f"KẾT QUẢ: {sum(RESULTS)}/{len(RESULTS)} tình huống nghiệm thu đạt")
raise SystemExit(0 if all(RESULTS) else 1)
