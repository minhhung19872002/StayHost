"""docs/07 §4, §13, §15.5 — one booking paid for real, on VNPay's own pages.

Every other suite stops at the gateway's door: it proves Staylio hands over a
request VNPay accepts, and then signs the reply itself. This one goes through.
It drives a real browser to VNPay's sandbox, types their published test card,
answers the OTP, and comes back — so the return leg, the signature check, the
card capture and the confirmation are exercised by VNPay rather than by us.

    pip install playwright && playwright install chromium
    STAYHOST_URL=http://localhost:5199 python scripts/vnpay_browser_acceptance.py

Needs the server on ASPNETCORE_ENVIRONMENT=Development with VNPay keys in
`dotnet user-secrets` and `Psp:Vnpay:Tokens` on. It books a real stay on the
running database and leaves it confirmed and paid.

The card is VNPay's own sandbox card for NCB. It moves no money and works only
against sandbox.vnpayment.vn.

Two traps this script fell into, kept as comments where they bit:
substring matching clicked "Hủy thanh toán" for "Thanh toán" and "Không đồng ý"
for "Đồng ý" — twice choosing the opposite of what was meant.
"""
import datetime
import http.cookiejar
import json
import os
import subprocess
import sys
import tempfile
import shlex
import time
import urllib.error
import urllib.parse
import urllib.request

# A Windows console runs cp1258 — the Vietnamese code page, and it spells
# Vietnamese with combining marks, so it cannot encode the precomposed letters
# the server actually sends. Any scenario that echoes a server message then dies
# inside print(), the runner writes it down as FAIL, and a correct product
# reports 10/13. Proven: the same run is 10/10 under PYTHONIOENCODING=utf-8.
# A verdict must never be lost to a character the terminal cannot draw.
import sys
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8')


try:
    from playwright.sync_api import sync_playwright
except ImportError:
    print("Cần Playwright: pip install playwright && playwright install chromium")
    sys.exit(2)

BASE = os.environ.get("STAYHOST_URL", "http://localhost:5199").rstrip("/")
HOST = urllib.parse.urlsplit(BASE).hostname or "localhost"
# Where the database lives, when it is not this machine. Pointing STAYHOST_URL at
# a server while `sql()` kept reading the container on the laptop is how a run
# reports on two different databases at once and believes every word of it.
DB_SSH = os.environ.get("STAYHOST_DB_SSH")
SHOTS = os.environ.get("STAYHOST_SHOTS") or tempfile.mkdtemp(prefix="stayhost-vnpay-")

# VNPay's published sandbox card (NCB). Not a secret and not real money.
CARD, HOLDER, ISSUED, OTP = "9704198526191432198", "NGUYEN VAN A", "07/15", "123456"

passed, failed = [], []
op = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))


def call(path, body=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data,
                                 method="POST" if body is not None else "GET",
                                 headers={"Content-Type": "application/json"})
    try:
        with op.open(req, timeout=30) as res:
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
    argv = ["docker", "exec", "stayhost-db", "psql", "-U", "stayhost", "-d", "stayhost",
            "-t", "-A", "-c", query]
    if DB_SSH:
        # ssh hands the remote shell one string, which parses it again, so every
        # argument has to survive a second round of quoting.
        argv = ["ssh", "-o", "BatchMode=yes", DB_SSH,
                " ".join(shlex.quote(a) for a in argv)]
    out = subprocess.run(argv, capture_output=True, text=True, encoding="utf-8")
    return out.stdout.strip() if out.returncode == 0 else ""


def future(days):
    return (datetime.date.today() + datetime.timedelta(days=days)).isoformat()


def dismiss_modals(page):
    """VNPay pops its terms of use over the form, and it eats every click."""
    for _ in range(4):
        modal = page.query_selector("div.modal.show")
        if not modal:
            return
        clicked = False
        for el in modal.query_selector_all("button, a"):
            try:
                text = (el.inner_text() or "").strip().lower()
            except Exception:
                continue
            # "Đồng ý" is a substring of "Không đồng ý", and the obvious match
            # clicks the refusal.
            if "không" in text or "khong" in text:
                continue
            if any(w in text for w in ("đồng ý", "dong y", "đóng", "close", "ok", "xác nhận")):
                try:
                    el.click(timeout=4000)
                    clicked = True
                except Exception:
                    pass
                break
        if not clicked:
            page.keyboard.press("Escape")
        time.sleep(1)


def press(page, *labels):
    """Click whatever carries this text. VNPay's buttons are not always <button>."""
    for label in labels:
        for el in page.query_selector_all("button, a, input[type=submit], div[role=button]"):
            try:
                text = (el.inner_text() or el.get_attribute("value") or "").strip()
            except Exception:
                continue
            low = text.lower()
            # "Thanh toán" is a substring of "Hủy thanh toán". Clicking that is
            # how a test cancels the very payment it was written to complete.
            if "hủy" in low and "hủy" not in label.lower():
                continue
            if label.lower() in low and el.is_visible():
                # A short timeout on purpose: when the terms modal is over the
                # form this click cannot land, and the caller's loop wants to go
                # round and dismiss it rather than sit here for thirty seconds.
                try:
                    el.click(timeout=4000)
                    return True
                except Exception:
                    return False
    return False


print("Staylio · nghiệm thu VNPay trên trình duyệt thật (docs/07 §15.5) — %s\n" % BASE)

_, catalogue = call("/api/payment-methods/catalogue")
live = {m["key"]: (m.get("live"), m.get("tokens")) for m in (catalogue or {}).get("methods", [])}

if not live.get("napas", (False,))[0]:
    print("VNPay chưa bật cho ô thẻ nội địa — không có gì để chạy.")
    sys.exit(0)

st, _ = call("/api/account/login", {"email": "guest@staylio.vn", "password": "stayhost123"})
if st != 200:
    print("Không đăng nhập được khách.")
    sys.exit(1)

# --- 1: a booking, and an order at VNPay -------------------------------------
print("1. Giữ chỗ rồi mở đơn ở VNPay")

held = None
for week in range(0, 14):
    at = 150 + week * 7
    _, page_of = call("/api/listings?pageSize=60&checkIn=%s&checkOut=%s" % (future(at), future(at + 2)))
    for listing in page_of.get("items", []):
        if not listing["instantBook"]:
            continue
        st, booking = call("/api/bookings", {
            "listingId": listing["id"], "checkIn": future(at), "checkOut": future(at + 2),
            "guests": 1, "adults": 1, "children": 0, "infants": 0, "pets": 0,
            "guestName": "Khách Demo", "guestEmail": "guest@staylio.vn",
            "agreedToRules": True, "paymentMethod": "napas"})
        if st == 201:
            held = booking
            break
    if held:
        break

if held is None:
    print("Không giữ được chỗ nào — lịch đã kín trong tầm ngày đã thử.")
    sys.exit(1)

st, paid = call("/api/bookings/%d/pay" % held["id"], {"paymentMethod": "napas", "saveCard": True})
url = (paid or {}).get("gatewayRedirectUrl") or ""
order_ref = (paid or {}).get("gatewayOrderRef")

check("Đơn nhận địa chỉ của VNPay", st == 200 and "vnpayment.vn" in url, url[:60])
check("Đi nhánh token (vì khách chọn lưu thẻ)", "token_ui" in url, url[:60])
check("Chưa thu tiền: đơn vẫn chờ thanh toán",
      (paid or {}).get("status") == "PendingPayment", str((paid or {}).get("status")))

if not url:
    sys.exit(1)

print("     đơn %s · %s₫ · mã %s" % (held["reference"], held["total"], order_ref))

# --- 2: pay on VNPay's own pages ---------------------------------------------
print("\n2. Trả tiền trên chính trang của VNPay")

landed = ""
with sync_playwright() as p:
    browser = p.chromium.launch(headless=True)
    page = browser.new_page()
    page.goto(url, wait_until="domcontentloaded", timeout=60000)
    page.wait_for_selector("#card_number_mask", timeout=40000)

    check("VNPay mở form thẻ nội địa cho đúng đơn này", order_ref in page.inner_text("body"),
          page.url[:70])

    # A masked input ignores an assigned value; it wants keystrokes.
    page.click("#card_number_mask")
    page.keyboard.type(CARD, delay=60)
    time.sleep(2)
    page.click("#cardHolder")
    page.keyboard.type(HOLDER, delay=40)
    page.click("#cardDate")
    page.keyboard.type(ISSUED.replace("/", ""), delay=60)
    time.sleep(1)
    page.screenshot(path=os.path.join(SHOTS, "1-card.png"), full_page=True)

    press(page, "Tiếp tục", "Continue")

    # The terms modal opens after Tiếp tục and has to be accepted before the
    # bank's page will load, so this keeps nudging rather than waiting once.
    for _ in range(20):
        if "confirm-payment" in page.url:
            break
        dismiss_modals(page)
        if "confirm-payment" in page.url:
            break
        press(page, "Tiếp tục", "Continue")
        time.sleep(3)

    check("Tới được trang OTP của ngân hàng", "confirm-payment" in page.url, page.url[:70])
    page.screenshot(path=os.path.join(SHOTS, "2-otp.png"), full_page=True)

    # The OTP box has no name worth guessing; it is the only typeable input left.
    for el in page.query_selector_all("input"):
        try:
            if not el.is_visible():
                continue
            if (el.get_attribute("type") or "text").lower() in (
                    "hidden", "checkbox", "radio", "submit", "button"):
                continue
            el.click()
            page.keyboard.type(OTP, delay=80)
            break
        except Exception:
            pass

    time.sleep(1)
    dismiss_modals(page)
    press(page, "Xác nhận", "Thanh toán", "Continue")

    try:
        page.wait_for_url("**%s**" % HOST, timeout=60000)
    except Exception:
        pass

    time.sleep(4)
    landed = page.url
    page.screenshot(path=os.path.join(SHOTS, "3-back.png"), full_page=True)
    browser.close()

check("Khách được đưa về Staylio", "/thanh-toan/ket-qua" in landed, landed[-70:])
print("     ảnh chụp: %s" % SHOTS)

# --- 3: what the platform now knows ------------------------------------------
print("\n3. Sau khi tiền đã chuyển thật")

_, mine = call("/api/bookings")
row = next((b for b in (mine or []) if b["id"] == held["id"]), None)
check("Đơn đã được xác nhận", row is not None and row["status"] == "Confirmed",
      str(row and row["status"]))

last4 = sql('select coalesce("CardLast4", \'\') from payments where "BookingId"=%d' % held["id"])
check("Sàn biết 4 số cuối của thẻ", last4 == CARD[-4:], "%s (thẻ %s)" % (last4 or "(trống)", CARD[-4:]))

_, cards = call("/api/payment-methods")
saved = next((c for c in (cards or []) if c["last4"] == CARD[-4:]), None)
check("Thẻ đã được lưu", saved is not None, str(saved and saved["brandLabel"]))
check("Thẻ do cổng giữ, không phải sàn giữ", bool(saved and saved.get("gatewayHeld")),
      str(saved and saved.get("provider")))

token = sql("""select coalesce("GatewayTokenSealed", '') from saved_cards where "Last4"='%s'"""
            % CARD[-4:])
check("Token của cổng được lưu ở dạng mã hoá", bool(token) and CARD not in token,
      (token[:24] + "…") if token else "(trống)")

status = sql("""select "Status" from payment_sessions where "OrderRef"='%s'""" % order_ref)
txn = sql("""select coalesce("ProviderTxnId", '') from payment_sessions where "OrderRef"='%s'"""
          % order_ref)
check("Phiên thanh toán chốt bằng câu trả lời của VNPay", status == "1", "Status=%s" % status)
check("Có mã giao dịch của VNPay để đối soát", bool(txn), txn or "(trống)")

total = sql('select coalesce(sum(case when "Direction"=1 then "Amount" else -"Amount" end),0) '
            'from ledger_entries;')
check("Sổ vẫn cân", float(total or 0) == 0, total)

print("\n%d đạt · %d hỏng" % (len(passed), len(failed)))
for f in failed:
    print("  hỏng: " + f)
sys.exit(1 if failed else 0)
