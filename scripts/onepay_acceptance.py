"""
Staylio · nghiệm thu OnePay trên trình duyệt thật (docs/07 §13, §15.3)

Trả tiền bằng **thẻ Visa quốc tế** trên chính trang của OnePay, rồi kiểm những
gì sàn biết sau đó. Đây là thứ không chạy được với VNPay: sandbox của họ chỉ
công bố thẻ NCB nội địa, nên ô "Thẻ tín dụng / ghi nợ" mở được trang nhưng
không có thẻ nào để trả xong. OnePay có thẻ test quốc tế, nên nhánh này mới
chứng minh được từ đầu tới cuối.

    STAYHOST_URL=http://localhost:5199 python scripts/onepay_acceptance.py

Chạy với máy chủ khác thì đặt thêm STAYHOST_DB_SSH để câu psql đi tới đúng cơ
sở dữ liệu của máy chủ đó — đọc DB trên máy mình trong khi hỏi API ở nơi khác
là báo cáo về hai hệ thống cùng lúc.

Cần Playwright: pip install playwright && playwright install chromium
"""

import datetime
import http.cookiejar
import json
import os
import shlex
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

try:
    from playwright.sync_api import sync_playwright
except ImportError:
    print("Cần Playwright: pip install playwright && playwright install chromium")
    sys.exit(2)

BASE = os.environ.get("STAYHOST_URL", "http://localhost:5199").rstrip("/")
HOST = urllib.parse.urlsplit(BASE).hostname or "localhost"
DB_SSH = os.environ.get("STAYHOST_DB_SSH")
SHOTS = os.environ.get("STAYHOST_SHOTS") or ""

# OnePay's published international test card. Not a secret and not real money.
CARD, EXPIRY, CSC = "4005550000000001", "1227", "100"
HOLDER, EMAIL = "NGUYEN VAN A", "guest@staylio.vn"

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
        argv = ["ssh", "-o", "BatchMode=yes", DB_SSH,
                " ".join(shlex.quote(a) for a in argv)]
    out = subprocess.run(argv, capture_output=True, text=True, encoding="utf-8")
    return out.stdout.strip() if out.returncode == 0 else ""


def future(days):
    return (datetime.date.today() + datetime.timedelta(days=days)).isoformat()


def pay_on_onepay(page):
    """
    Fills OnePay's international card form and presses pay.

    Both scenarios below need this and they must stay identical: the second one
    differs only in that the trip home is blocked, so any drift between two
    copies would make it test something other than what it claims to.
    """
    for _ in range(12):
        time.sleep(3)
        if "generalv2" in page.url:
            break

    page.click("text=Thẻ tín dụng / Ghi nợ", timeout=20000)
    time.sleep(4)

    def put(placeholder, value):
        box = page.locator("input[placeholder='%s']" % placeholder)
        if box.count() == 0:
            return False
        box.first.click()
        page.keyboard.type(value, delay=70)
        return True

    put("1234 5678 9101 1234", CARD)
    time.sleep(1)
    put("12/27", EXPIRY)
    put("123", CSC)
    put("Nhập tên chủ thẻ", HOLDER)
    put("name@email.com", EMAIL)

    # Only the terms box. The one above it is "không sử dụng email", and ticking
    # that turns the form into one that demands a phone number instead.
    page.locator("input[type=checkbox]").last.check(force=True)
    time.sleep(1)

    page.click("text=Xác nhận thanh toán", timeout=15000)


def hold_a_booking(from_day):
    """A bookable stay paid by card, or None when the calendar is full."""
    for week in range(0, 14):
        at = from_day + week * 7
        _, page_of = call("/api/listings?pageSize=60&checkIn=%s&checkOut=%s"
                          % (future(at), future(at + 2)))
        for listing in page_of.get("items", []):
            if not listing["instantBook"]:
                continue
            st, booking = call("/api/bookings", {
                "listingId": listing["id"], "checkIn": future(at), "checkOut": future(at + 2),
                "guests": 1, "adults": 1, "children": 0, "infants": 0, "pets": 0,
                "guestName": "Khách Demo", "guestEmail": "guest@staylio.vn",
                "agreedToRules": True, "paymentMethod": "card"})
            if st == 201:
                return booking
    return None


print("Staylio · nghiệm thu OnePay, thẻ Visa quốc tế (docs/07 §13) — %s\n" % BASE)

_, catalogue = call("/api/payment-methods/catalogue")
card = next((m for m in (catalogue or {}).get("methods", []) if m["key"] == "card"), None)

if not card or not card.get("live"):
    print("Ô thẻ chưa nối cổng thật nào — không có gì để chạy.")
    sys.exit(0)

st, _ = call("/api/account/login", {"email": "guest@staylio.vn", "password": "stayhost123"})
if st != 200:
    print("Không đăng nhập được khách.")
    sys.exit(1)

# --- 1: a booking, and an order at OnePay ------------------------------------
print("1. Giữ chỗ rồi mở đơn ở OnePay")

held = hold_a_booking(120)

if held is None:
    print("Không giữ được chỗ nào — lịch đã kín trong tầm ngày đã thử.")
    sys.exit(1)

st, paid = call("/api/bookings/%d/pay" % held["id"], {"paymentMethod": "card", "saveCard": False})
url = (paid or {}).get("gatewayRedirectUrl") or ""
order_ref = (paid or {}).get("gatewayOrderRef")

check("Đơn nhận địa chỉ của OnePay", st == 200 and "onepay.vn" in url, url[:60])
check("Chưa thu tiền: đơn vẫn chờ thanh toán",
      (paid or {}).get("status") == "PendingPayment", str((paid or {}).get("status")))

if not url:
    sys.exit(1)

print("     đơn %s · %s₫ · mã %s" % (held["reference"], held["total"], order_ref))

# --- 2: pay on OnePay's own pages --------------------------------------------
print("\n2. Trả tiền bằng thẻ Visa trên chính trang của OnePay")

came_back = []

with sync_playwright() as p:
    browser = p.chromium.launch(headless=True)
    page = browser.new_page()

    # The return trip is recorded as it happens rather than read off page.url at
    # the end. OnePay's page keeps working after the redirect fires, so a check
    # that looks once, later, can miss a return that did arrive — which is a
    # failing test for a payment that succeeded.
    page.on("framenavigated",
            lambda fr: fr == page.main_frame and HOST in fr.url and came_back.append(fr.url))

    page.goto(url, wait_until="domcontentloaded", timeout=60000)

    for _ in range(12):
        time.sleep(3)
        if "generalv2" in page.url:
            break

    check("OnePay mở trang đơn hàng cho đúng đơn này",
          order_ref in page.inner_text("body"), page.url[:60])

    pay_on_onepay(page)

    if SHOTS:
        page.screenshot(path=os.path.join(SHOTS, "onepay-card.png"), full_page=True)

    for _ in range(20):
        time.sleep(3)
        if came_back:
            break

    time.sleep(3)
    if SHOTS:
        page.screenshot(path=os.path.join(SHOTS, "onepay-back.png"), full_page=True)
    browser.close()

check("Khách được đưa về Staylio",
      any("/thanh-toan/ket-qua" in u or "/api/payments/onepay/return" in u for u in came_back),
      (came_back[-1][-70:] if came_back else "(không quay về)"))

# --- 3: what the platform now knows ------------------------------------------
print("\n3. Sau khi tiền đã chuyển thật")

time.sleep(2)
_, mine = call("/api/bookings")
row = next((b for b in (mine or []) if b["id"] == held["id"]), None)
check("Đơn đã được xác nhận", row is not None and row["status"] == "Confirmed",
      str(row and row["status"]))

# docs/07 §4 — the difference from VNPay: four digits without a token API.
last4 = sql('select coalesce("CardLast4", \'\') from payments where "BookingId"=%d' % held["id"])
check("Sàn biết 4 số cuối mà không cần token hoá", last4 == CARD[-4:],
      "%s (thẻ %s)" % (last4 or "(trống)", CARD[-4:]))

status = sql("""select "Status" from payment_sessions where "OrderRef"='%s'""" % order_ref)
settled = sql("""select coalesce("SettledBy", '') from payment_sessions where "OrderRef"='%s'""" % order_ref)
provider = sql("""select "Provider" from payment_sessions where "OrderRef"='%s'""" % order_ref)
txn = sql("""select coalesce("ProviderTxnId", '') from payment_sessions where "OrderRef"='%s'""" % order_ref)

check("Phiên thanh toán chốt bằng câu trả lời của OnePay", status == "1", "Status=%s" % status)
check("Chốt bằng đường có chữ ký, không phải đoán", settled in ("return", "ipn"), settled or "(trống)")
check("Ghi đúng cổng đã thu tiền", provider == "onepay", provider)
check("Có mã giao dịch của OnePay để đối soát", bool(txn), txn or "(trống)")

# --- 4: the guest who never comes back ---------------------------------------
# docs/07 §5 — "không tin vào việc khách quay về trang nào". The trip home is
# blocked outright here, so the only thing that can settle this payment is the
# platform asking OnePay itself. Without an ApiUser it cannot ask and the
# booking would sit pending until the hold expired — which makes this the
# scenario that proves the query API is wired rather than merely written.
print("\n4. Khách trả tiền xong nhưng KHÔNG quay về")

held2 = hold_a_booking(300)

if held2 is None:
    print("     (bỏ qua: không giữ được chỗ nào nữa)")
else:
    _, paid2 = call("/api/bookings/%d/pay" % held2["id"], {"paymentMethod": "card", "saveCard": False})
    url2 = (paid2 or {}).get("gatewayRedirectUrl") or ""
    ref2 = (paid2 or {}).get("gatewayOrderRef")
    print("     đơn %s · mã %s" % (held2["reference"], ref2))

    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        page = browser.new_page()
        # Matched on the hostname, not on the text of the URL: OnePay's own
        # address carries vpc_ReturnURL inside its query string, so a glob like
        # "**localhost*" blocks the trip *out* as well as the trip home and the
        # scenario never gets as far as paying.
        page.route(lambda u: urllib.parse.urlsplit(u).hostname == HOST,
                   lambda route: route.abort())

        page.goto(url2, wait_until="domcontentloaded", timeout=60000)
        pay_on_onepay(page)
        time.sleep(25)

        stranded = "onepay.vn" in page.url
        browser.close()

    check("Khách kẹt lại ở cổng, không có đường quay về", stranded)

    settled2 = ""
    for _ in range(12):
        time.sleep(15)
        settled2 = sql("""select coalesce("SettledBy", '') from payment_sessions where "OrderRef"='%s'""" % ref2)
        if settled2:
            break

    _, mine2 = call("/api/bookings")
    row2 = next((b for b in (mine2 or []) if b["id"] == held2["id"]), None)
    txn2 = sql("""select coalesce("ProviderTxnId", '') from payment_sessions where "OrderRef"='%s'""" % ref2)

    check("Sàn tự hỏi lại OnePay và chốt được", settled2 == "sweep", settled2 or "(chưa chốt)")
    check("Đơn được xác nhận dù khách không quay về",
          row2 is not None and row2["status"] == "Confirmed", str(row2 and row2["status"]))
    check("Vẫn lấy được mã giao dịch để đối soát", bool(txn2), txn2 or "(trống)")

total = sql('select coalesce(sum(case when "Direction"=1 then "Amount" else -"Amount" end),0) '
            'from ledger_entries;')
check("Sổ vẫn cân", float(total or 0) == 0, total)

print("\n%d đạt · %d hỏng" % (len(passed), len(failed)))
for f in failed:
    print("  hỏng: " + f)
sys.exit(1 if failed else 0)
