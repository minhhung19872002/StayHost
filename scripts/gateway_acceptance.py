"""docs/07 §13 — the licensed gateways, against the real sandboxes.

Everything here talks to somebody else's server. It opens a genuine order at
MoMo and at ZaloPay, follows the address they hand back, and then asks them what
they think happened — which is the check docs/07 §5 says the platform must make
rather than believing the browser.

What it does not do is finish a payment: that needs a human with the MoMo app.
So the last scenario proves the other half instead, the half that costs money if
it is wrong — that a callback nobody signed cannot confirm a booking.

    STAYHOST_URL=http://localhost:5199 python scripts/gateway_acceptance.py

The server must run with ASPNETCORE_ENVIRONMENT=Development, which is where the
sandbox credentials live (appsettings.Development.json). Without them the
gateways report themselves not live and the run says so instead of failing.
"""
import datetime
import hashlib
import hmac
import http.cookiejar
import io
import json
import os
import random
import re
import subprocess
import sys
import urllib.error
import urllib.request

BASE = os.environ.get("STAYHOST_URL", "http://localhost:5199").rstrip("/")

HERE = os.path.dirname(os.path.abspath(__file__))
DEV_SETTINGS = os.path.join(HERE, os.pardir, "src", "StayHost.Web", "appsettings.Development.json")


def dev_key2():
    """ZaloPay's callback key, as configured for this machine.

    Only a development script may do this. In production the key is an
    environment variable and nothing reads it back — the point of key2 is that
    only ZaloPay and the server know it.
    """
    try:
        with io.open(DEV_SETTINGS, encoding="utf-8") as f:
            text = re.sub(r'"//[^"]*"\s*:\s*\[[^\]]*\],?', "", f.read())
        return json.loads(text).get("Psp", {}).get("Zalopay", {}).get("Key2")
    except (OSError, ValueError):
        return None


def ledger_total():
    """docs/00 §6.5 — every movement is written both ways, so this must be zero."""
    out = subprocess.run(
        ["docker", "exec", "stayhost-db", "psql", "-U", "stayhost", "-d", "stayhost", "-t", "-c",
         'select coalesce(sum(case when "Direction"=1 then "Amount" else -"Amount" end),0) '
         'from ledger_entries;'],
        capture_output=True, text=True)
    try:
        return float(out.stdout.strip())
    except ValueError:
        return None

# The same ninety-minute shift acceptance.py uses, so two runs in one session do
# not fight over the same nights.
RUN_SHIFT = int(datetime.datetime.now().timestamp() // 5400 % 90)

passed, failed = [], []


def opener():
    return urllib.request.build_opener(
        urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))


def call(op, path, body=None, m=None, follow=True):
    method = m or ("POST" if body is not None else "GET")
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method,
                                 headers={"Content-Type": "application/json"})
    try:
        with op.open(req, timeout=30) as res:
            raw = res.read().decode("utf-8", "replace")
            if not raw.strip():
                return res.status, None
            try:
                return res.status, json.loads(raw)
            except json.JSONDecodeError:
                # A return route redirects into the app, so the body is the page.
                return res.status, {"raw": raw[:200], "url": res.geturl()}
    except urllib.error.HTTPError as e:
        raw = e.read().decode("utf-8", "replace")
        try:
            return e.code, json.loads(raw) if raw.strip() else None
        except json.JSONDecodeError:
            return e.code, {"raw": raw[:400]}


def check(name, ok, detail=""):
    (passed if ok else failed).append(name)
    print(("  PASS  " if ok else "  FAIL  ") + name + (" — " + detail if detail else ""))


def future(days):
    return (datetime.date.today() + datetime.timedelta(days=days + RUN_SHIFT)).isoformat()


def body_for(listing, offset, nights, **kw):
    return {"listingId": listing["id"], "checkIn": future(offset),
            "checkOut": future(offset + nights), "guests": 1, "adults": 1,
            "children": 0, "infants": 0, "pets": 0, "guestName": "Khách Demo",
            "guestEmail": "guest@stayhost.vn", "guestNote": None,
            "agreedToRules": True} | kw


def hold(op, method, nights=3, offset=60):
    """Dates actually taken off the market, not merely promised.

    A dry run answering 201 is not the same thing as a hold. On a database that
    has been run against many times the GiST constraint decides between two
    checkouts a moment apart, so the only honest test of "can this be booked" is
    to book it — and to keep looking when it cannot, rather than reporting the
    gateway broken because the calendar was full.
    """
    last = None

    for week in range(0, 10):
        at = offset + week * 7
        _, page = call(op, "/api/listings?pageSize=60&checkIn=%s&checkOut=%s"
                       % (future(at), future(at + nights)))

        for listing in page.get("items", []):
            if not listing["instantBook"]:
                continue

            st, held = call(op, "/api/bookings", body_for(listing, at, nights, paymentMethod=method))
            last = st
            if st == 201:
                return held, st

    return None, last


print("StayHost · nghiệm thu cổng thanh toán (docs/07 §13) — %s\n" % BASE)

# --- 0: which gateways are wired --------------------------------------------
_, catalogue = call(opener(), "/api/payment-methods/catalogue")
live = {m["key"]: m.get("live", False) for m in catalogue["methods"]}
print("Cổng đang bật: %s\n" % (", ".join(k for k, v in live.items() if v) or "(chưa có)"))

guest = opener()
st, _ = call(guest, "/api/account/login",
             {"email": "guest@stayhost.vn", "password": "stayhost123"})
if st != 200:
    print("Không đăng nhập được guest@stayhost.vn — dừng.")
    sys.exit(1)

momo_ref = momo_booking = None
zalo_ref = zalo_booking = None
zalo_amount = 0

# --- 1: MoMo opens a real order ---------------------------------------------
print("1. MoMo mở đơn thật và trả về địa chỉ của chính họ")
if not live.get("momo"):
    check("MoMo đang bật", False, "chưa cấu hình")
else:
    held, code = hold(guest, "momo")
    if held is None:
        check("Giữ chỗ được", False, "HTTP %s" % code)
    else:
        momo_booking = held["id"]
        st, paid = call(guest, "/api/bookings/%d/pay" % held["id"], {"paymentMethod": "momo"})
        url = (paid or {}).get("gatewayRedirectUrl") or ""
        check("Đơn nhận địa chỉ chuyển hướng", st == 200 and url.startswith("http"),
              url[:70] or "HTTP %s: %s" % (st, paid))
        check("Địa chỉ là của MoMo, không phải của sàn", "momo.vn" in url, url[:70])
        # docs/07 §13 — nothing may be charged before the guest has paid.
        check("Đơn vẫn đang chờ thanh toán", (paid or {}).get("status") == "PendingPayment",
              str((paid or {}).get("status")))
        check("Có mã giao dịch để đối chiếu", bool((paid or {}).get("gatewayOrderRef")),
              str((paid or {}).get("gatewayOrderRef")))

        # docs/07 §7 — the same request twice is one order at the gateway.
        st2, again = call(guest, "/api/bookings/%d/pay" % held["id"], {"paymentMethod": "momo"})
        check("Bấm lại không mở đơn thứ hai ở cổng",
              (again or {}).get("gatewayOrderRef") == (paid or {}).get("gatewayOrderRef"),
              "%s vs %s" % ((paid or {}).get("gatewayOrderRef"), (again or {}).get("gatewayOrderRef")))
        momo_ref = (paid or {}).get("gatewayOrderRef")

# --- 2: ZaloPay opens a real order ------------------------------------------
print("\n2. ZaloPay mở đơn thật và trả về địa chỉ của chính họ")
if not live.get("zalopay"):
    check("ZaloPay đang bật", False, "chưa cấu hình")
else:
    held, code = hold(guest, "zalopay")
    if held is None:
        check("Giữ chỗ được", False, "HTTP %s" % code)
    else:
        zalo_booking = held["id"]
        st, paid = call(guest, "/api/bookings/%d/pay" % held["id"], {"paymentMethod": "zalopay"})
        url = (paid or {}).get("gatewayRedirectUrl") or ""
        check("Đơn nhận địa chỉ chuyển hướng", st == 200 and url.startswith("http"),
              url[:70] or "HTTP %s: %s" % (st, paid))
        check("Địa chỉ là của ZaloPay", "zalopay" in url or "zlp" in url, url[:70])
        check("Đơn vẫn đang chờ thanh toán", (paid or {}).get("status") == "PendingPayment",
              str((paid or {}).get("status")))
        zalo_ref = (paid or {}).get("gatewayOrderRef")
        zalo_amount = (paid or {}).get("total") or 0

# --- 3: a callback nobody signed confirms nothing -----------------------------
# The half that costs money if it is wrong. Anyone can post to these routes.
print("\n3. Callback không có chữ ký thì không xác nhận được gì")
anon = opener()

st, res = call(anon, "/api/payments/zalopay/callback",
               {"data": json.dumps({"app_trans_id": "260101_00000000000000000001",
                                    "amount": 9_999_999}),
                "mac": "0" * 64})
check("ZaloPay: mac sai bị từ chối", (res or {}).get("return_code") in (0, -1),
      json.dumps(res, ensure_ascii=False)[:70])

st, res = call(anon, "/api/payments/vnpay/ipn?vnp_TxnRef=00000000000000000001"
                     "&vnp_Amount=100&vnp_ResponseCode=00&vnp_SecureHash=" + "0" * 128)
check("VNPay: chữ ký sai không xác nhận đơn", (res or {}).get("RspCode") != "00",
      json.dumps(res, ensure_ascii=False)[:70])

# The forged MoMo IPN aims at a real, still-unpaid order, which is the case that
# actually matters: a signature check that only refuses unknown orders is no
# check at all.
if momo_ref:
    st, res = call(anon, "/api/payments/momo/ipn", {
        "partnerCode": "MOMOBKUN20180529", "orderId": momo_ref, "requestId": momo_ref,
        "amount": 1, "orderInfo": "x", "orderType": "momo_wallet", "transId": 1,
        "resultCode": 0, "message": "Successful.", "payType": "webApp",
        "responseTime": 1, "extraData": "", "signature": "0" * 64})

    _, mine = call(guest, "/api/bookings")
    row = next((b for b in (mine or []) if b["id"] == momo_booking), None)

    check("MoMo: chữ ký giả không xác nhận được đơn thật",
          st in (200, 204) and row is not None and row["status"] == "PendingPayment",
          "HTTP %s · %s" % (st, row and row["status"]))

    # And it must not kill the payment either. Nothing authenticates a caller
    # here, so a forged callback that could mark the attempt failed would let a
    # stranger stop anyone's booking — and strand a guest who then really paid.
    st, again = call(guest, "/api/bookings/%d/pay" % momo_booking, {"paymentMethod": "momo"})
    check("Chữ ký giả cũng không làm hỏng lượt thanh toán đang chờ",
          st == 200 and bool((again or {}).get("gatewayRedirectUrl")),
          "HTTP %s: %s" % (st, (again or {}).get("message")))

# --- 4: the platform asks the gateway itself ---------------------------------
# docs/07 §5 — the return route does not read what the browser brought back; it
# asks ZaloPay. A round trip that answers "still deciding" proves the query call
# is real, signed correctly and understood.
print("\n4. Sàn tự hỏi lại cổng thay vì tin trình duyệt")
if live.get("zalopay") and zalo_ref:
    st, page = call(anon, "/api/payments/zalopay/return?ref=%s" % zalo_ref)
    landed = (page or {}).get("url", "")
    check("Khách được đưa về trang kết quả của sàn", st == 200 and "ket-qua" in landed,
          "HTTP %s · %s" % (st, landed[-60:]))
    # ZaloPay was asked and said "still deciding", so the page says pending —
    # not "paid" because the guest happened to land on the success URL.
    check("Cổng trả lời chưa xong nên chưa xác nhận", "ket-qua=pending" in landed,
          landed[-60:])
    _, mine = call(guest, "/api/bookings")
    row = next((b for b in mine if b["id"] == zalo_booking), None)
    check("Đơn vẫn chưa được xác nhận", row is not None and row["status"] == "PendingPayment",
          str(row and row["status"]))

# --- 5: a properly signed callback does confirm ------------------------------
# The other half. Finishing a payment for real needs a human with the MoMo app,
# so this stands in for ZaloPay's server the only way a test can: by signing the
# callback with the merchant key ZaloPay signs it with. Everything downstream —
# the signature check, the amount check, the ledger, the confirmation — is the
# production path, untouched.
print("\n5. Callback đúng chữ ký thì xác nhận đơn, và gọi lại lần nữa không ghi sổ hai lần")
if live.get("zalopay") and zalo_ref:
    key2 = os.environ.get("ZALOPAY_KEY2") or dev_key2()

    if not key2:
        check("Đọc được key2 để ký thay ZaloPay", False, "đặt ZALOPAY_KEY2")
    else:
        data = json.dumps({
            "app_id": 2553,
            "app_trans_id": "%s_%s" % (datetime.datetime.utcnow().strftime("%y%m%d"), zalo_ref),
            "app_time": 0, "app_user": "stayhost", "amount": int(zalo_amount),
            "zp_trans_id": random.randint(10 ** 11, 10 ** 12)
        }, separators=(",", ":"))

        mac = hmac.new(key2.encode(), data.encode(), hashlib.sha256).hexdigest()

        st, res = call(anon, "/api/payments/zalopay/callback", {"data": data, "mac": mac})
        check("ZaloPay: chữ ký đúng được chấp nhận", (res or {}).get("return_code") == 1,
              json.dumps(res, ensure_ascii=False)[:70])

        _, mine = call(guest, "/api/bookings")
        row = next((b for b in mine if b["id"] == zalo_booking), None)
        check("Đơn đã được xác nhận", row is not None and row["status"] == "Confirmed",
              str(row and row["status"]))

        # docs/07 §7 — the worst fault in the module. ZaloPay retries its callback
        # until it gets a 1, so this happens in production every time a reply is
        # slow.
        before = ledger_total()
        st, res = call(anon, "/api/payments/zalopay/callback", {"data": data, "mac": mac})
        after = ledger_total()
        check("Gọi lại callback không ghi thêm bút toán nào", before == after,
              "%s → %s" % (before, after))

# --- 6: the books still balance ----------------------------------------------
print("\n6. Sổ sách vẫn cân")
total = ledger_total()
check("Tổng nợ trừ có bằng 0", total == 0, str(total))

print("\n%d đạt · %d hỏng" % (len(passed), len(failed)))
if failed:
    for f in failed:
        print("  hỏng: " + f)
sys.exit(1 if failed else 0)
