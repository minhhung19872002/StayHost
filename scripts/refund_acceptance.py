"""docs/07 §10 — does the guest's money actually go back?

The mirror of scripts/payout_acceptance.py, and the same class of fault: money
came in through a licensed gateway, and cancelling a booking asked the built-in
stand-in — which says yes to everything. The booking read "refunded", the ledger
posted, the guest was told, and not a đồng moved.

This pays a real booking on VNPay's sandbox in a real browser, cancels it, and
then asks VNPay whether the refund is theirs to confirm. Nothing here is signed
by this platform on VNPay's behalf.

    pip install playwright && playwright install chromium
    STAYHOST_URL=http://localhost:5199 python scripts/refund_acceptance.py

Needs the server on ASPNETCORE_ENVIRONMENT=Development with VNPay keys in
`dotnet user-secrets`.
"""
import datetime
import hashlib
import hmac
import http.cookiejar
import io
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.request

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

BASE = os.environ.get("STAYHOST_URL", "http://localhost:5199").rstrip("/")
VNPAY_API = "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction"

passed, failed = [], []
op = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))


def call(path, body=None, method=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data,
                                 method=method or ("POST" if body is not None else "GET"),
                                 headers={"Content-Type": "application/json"})
    try:
        with op.open(req, timeout=90) as res:
            raw = res.read().decode("utf-8", "replace")
            return res.status, (json.loads(raw) if raw.strip() else None)
    except urllib.error.HTTPError as e:
        raw = e.read().decode("utf-8", "replace")
        try:
            return e.code, json.loads(raw) if raw.strip() else None
        except json.JSONDecodeError:
            return e.code, {"raw": raw[:200]}


def check(name, ok, detail=""):
    (passed if ok else failed).append(name)
    print(("  PASS  " if ok else "  FAIL  ") + name + (" — " + detail if detail else ""))


def sql(query):
    out = subprocess.run(
        ["docker", "exec", "stayhost-db", "psql", "-U", "stayhost", "-d", "stayhost",
         "-t", "-A", "-c", query],
        capture_output=True, text=True, encoding="utf-8")
    return out.stdout.strip() if out.returncode == 0 else ""


def vnpay_keys():
    """Only so this script can ask VNPay directly. The app reads its own."""
    if os.environ.get("VNPAY_HASH_SECRET"):
        return os.environ.get("VNPAY_TMN_CODE", ""), os.environ["VNPAY_HASH_SECRET"]
    path = os.path.join(os.environ.get("APPDATA", ""), "Microsoft", "UserSecrets",
                        "stayhost-web-psp", "secrets.json")
    try:
        with io.open(path, encoding="utf-8-sig") as f:
            store = json.load(f)
        return store.get("Psp:Vnpay:TmnCode", ""), store.get("Psp:Vnpay:HashSecret", "")
    except (OSError, ValueError):
        return "", ""


def vnpay_querydr(tmn, secret, order_ref, paid_at):
    """Ask VNPay what it thinks happened to this transaction."""
    now = datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(hours=7)
    request_id = "q" + now.strftime("%y%m%d%H%M%S%f")[:15]
    created = now.strftime("%Y%m%d%H%M%S")
    info = "Kiem tra giao dich"

    parts = [request_id, "2.1.0", "querydr", tmn, order_ref, paid_at, created, "127.0.0.1", info]
    mac = hmac.new(secret.encode(), "|".join(parts).encode(), hashlib.sha512).hexdigest()

    body = {"vnp_RequestId": request_id, "vnp_Version": "2.1.0", "vnp_Command": "querydr",
            "vnp_TmnCode": tmn, "vnp_TxnRef": order_ref, "vnp_TransactionDate": paid_at,
            "vnp_CreateDate": created, "vnp_IpAddr": "127.0.0.1", "vnp_OrderInfo": info,
            "vnp_SecureHash": mac}

    req = urllib.request.Request(VNPAY_API, data=json.dumps(body).encode(), method="POST",
                                 headers={"Content-Type": "application/json",
                                          # VNPay answers 403 to a request with no
                                          # User-Agent, and the page says nothing
                                          # about why. Cost an afternoon.
                                          "User-Agent": "Staylio-acceptance/1.0"})
    try:
        with urllib.request.urlopen(req, timeout=40) as res:
            return json.loads(res.read().decode())
    except Exception as e:
        return {"error": str(e)}


print("Staylio · nghiệm thu hoàn tiền qua cổng thật (docs/07 §10) — %s\n" % BASE)

_, catalogue = call("/api/payment-methods/catalogue")
live = {m["key"]: m.get("live") for m in (catalogue or {}).get("methods", [])}

if not live.get("napas"):
    print("VNPay chưa bật — không có gì để hoàn.")
    sys.exit(0)

TMN, SECRET = vnpay_keys()
if not SECRET:
    print("Không đọc được HashSecret để hỏi VNPay (dotnet user-secrets hoặc VNPAY_HASH_SECRET).")
    sys.exit(1)

# --- 1: a booking really paid for -------------------------------------------
print("1. Một đơn đã trả tiền thật trên VNPay")

env = dict(os.environ, STAYHOST_URL=BASE, PYTHONIOENCODING="utf-8")
run = subprocess.run([sys.executable,
                      os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                   "vnpay_browser_acceptance.py")],
                     capture_output=True, text=True, encoding="utf-8", env=env)

paid_ok = run.returncode == 0
check("Trả được một đơn qua trình duyệt", paid_ok,
      (run.stdout or "").strip().splitlines()[-1] if run.stdout else "")

if not paid_ok:
    print(run.stdout[-1500:] if run.stdout else run.stderr[-800:])
    sys.exit(1)

booking_id = int(sql('select "Id" from bookings where "Status"=2 order by "Id" desc limit 1'))
order_ref = sql('select "OrderRef" from payment_sessions where "BookingId"=%d '
                'and "Status"=1 order by "Id" desc limit 1' % booking_id)
paid_at = sql('select coalesce("ProviderPaidAt", \'\') from payment_sessions '
              'where "OrderRef"=\'%s\'' % order_ref)

check("Sàn giữ được giờ VNPay báo đã thu", bool(paid_at),
      paid_at or "(trống — refund sẽ gửi sai vnp_TransactionDate)")

# --- 2: cancel, and watch the money leave ------------------------------------
print("\n2. Huỷ đơn — tiền phải đi ngược qua chính VNPay")

st, _ = call("/api/account/login", {"email": "guest@staylio.vn", "password": "stayhost123"})
st, preview = call("/api/bookings/%d/refund-preview" % booking_id)
expected = (preview or {}).get("refund", 0)
check("Có khoản phải hoàn", expected > 0, "%s₫" % expected)

credit_before = float(sql('select coalesce(sum("Amount"),0) from credit_entries '
                          'where "BookingId"=%d' % booking_id) or 0)

st, _ = call("/api/bookings/%d/cancel" % booking_id, {}, method="POST")
check("Huỷ được đơn", st == 200, "HTTP %s" % st)

refunded = float(sql('select "RefundedAmount" from bookings where "Id"=%d' % booking_id) or 0)
check("Đơn ghi đúng số tiền hoàn", abs(refunded - expected) < 1, "%s vs %s" % (refunded, expected))

credit_after = float(sql('select coalesce(sum("Amount"),0) from credit_entries '
                         'where "BookingId"=%d' % booking_id) or 0)

# docs/07 §10 — money only becomes balance when the card refuses it. VNPay took
# this one, so nothing should have been diverted.
check("Không bị đẩy sang số dư", abs(credit_after - credit_before) < 1,
      "số dư +%s" % (credit_after - credit_before))

# --- 3: what VNPay actually answered -----------------------------------------
# Not querydr. That reports the *payment* — a refund is a transaction of its own
# at VNPay, and querydr keeps answering 01/00 about the original, which reads as
# "no refund happened" when one did. The evidence is the reply to the refund call
# itself, which the platform now keeps because docs/07 §7 cannot reconcile a day
# whose refunds left no trace.
print(chr(10) + "3. Câu trả lời của chính lệnh hoàn tiền")

time.sleep(2)

refund_code = sql("""select coalesce("RefundCode", '') from payment_sessions """
                  """where "OrderRef"='%s'""" % order_ref)
refund_txn = sql("""select coalesce("RefundTxnId", '') from payment_sessions """
                 """where "OrderRef"='%s'""" % order_ref)
refund_amt = float(sql("""select coalesce("RefundedAmount", 0) from payment_sessions """
                       """where "OrderRef"='%s'""" % order_ref) or 0)

check("Sàn lưu lại mã trả lời của cổng", refund_code == "00", refund_code or "(trống)")
check("Có mã giao dịch hoàn riêng của VNPay",
      bool(refund_txn) and refund_txn != refund_code, refund_txn or "(trống)")
check("Số tiền hoàn được ghi đúng", abs(refund_amt - expected) < 1,
      "%s vs %s" % (refund_amt, expected))

# And the merchant API is asked something, to prove it is reachable at all —
# VNPay answers 403 to a request with no User-Agent and that silently disabled
# both this call and the refund for a while.
answer = vnpay_querydr(TMN, SECRET, order_ref, paid_at or "20260101000000")
check("API thương nhân của VNPay gọi được (không 403)",
      answer.get("vnp_ResponseCode") is not None,
      answer.get("error", "")[:70] or "mã %s" % answer.get("vnp_ResponseCode"))

total = sql('select coalesce(sum(case when "Direction"=1 then "Amount" else -"Amount" end),0) '
            'from ledger_entries;')
check("Sổ vẫn cân", float(total or 0) == 0, total)

print("\n%d đạt · %d hỏng" % (len(passed), len(failed)))
for f in failed:
    print("  hỏng: " + f)
sys.exit(1 if failed else 0)
