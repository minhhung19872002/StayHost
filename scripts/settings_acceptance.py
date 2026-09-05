# docs/02 F1 — the settings page, and the one endpoint built new for it.
#
# The hub itself is wiring around panels that already had suites; what needs a
# net of its own is (a) the address rules — nine literal groups, an invented
# tenth answering an honest 404, the whole tree blocked in robots and absent
# from the sitemap — and (b) GET /api/account/payments, which reads the three
# lines of business the platform deliberately stores in three tables, plus gift
# cards, and must return every amount exactly as stored. A history that
# recomputes through today's Pricing changes when the rules do, and docs/00
# §6.2 says a receipt must still add up years later.
#
#   ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/StayHost.Web
#   python scripts/settings_acceptance.py
import json
import os
import subprocess
import sys
import urllib.error
import urllib.request
import http.cookiejar

# A Windows console runs cp1258 — the Vietnamese code page, and it spells
# Vietnamese with combining marks, so it cannot encode the precomposed letters
# the server sends. A verdict must never be lost to a character the terminal
# cannot draw.
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8')

B = os.environ.get("STAYHOST_URL", "http://localhost:5199").rstrip("/")
PW = "stayhost123"
GUEST = "guest@staylio.vn"
results = []


def ok(name, passed, detail=""):
    results.append((name, passed, detail))
    print(("PASS " if passed else "FAIL ") + name + (f" - {detail}" if detail else ""))


def call(o, path, body=None, method=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(
        B + path, data=data,
        headers={"Content-Type": "application/json"} if data else {},
        method=method or ("POST" if data else "GET"))
    try:
        with o.open(req, timeout=40) as r:
            text = r.read().decode("utf-8", "replace")
            return r.status, (json.loads(text) if text.strip().startswith(("{", "[")) else text)
    except urllib.error.HTTPError as e:
        text = e.read().decode("utf-8", "replace")
        return e.code, (json.loads(text) if text.strip().startswith(("{", "[")) else text)


def sql(query):
    out = subprocess.run(
        ["docker", "exec", "stayhost-db", "psql", "-U", "stayhost", "-d", "stayhost",
         "-t", "-A", "-c", query],
        capture_output=True, text=True, encoding="utf-8")
    return (out.stdout or "").strip()


def opener():
    return urllib.request.build_opener(
        urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))


def sign_in(email):
    o = opener()
    st, res = call(o, "/api/account/login", {"email": email, "password": PW})
    if isinstance(res, dict) and res.get("challenge"):
        if not res.get("devCode"):
            raise SystemExit("Chay server voi ASPNETCORE_ENVIRONMENT=Development.")
        call(o, "/api/account/two-factor",
             {"challenge": res["challenge"], "code": res["devCode"]})
    return o


def history(email):
    st, rows = call(sign_in(email), "/api/account/payments")
    if st != 200:
        raise RuntimeError(f"payments {st}: {rows}")
    return rows


def db_counts(email):
    uid = sql(f"select \"Id\" from users where \"Email\"='{email}';")
    return uid, {
        # PaymentStatus 2=Captured, 3=Refunded — the two states where money moved.
        "stay": int(sql(f'select count(*) from payments p join bookings b on b."Id"=p."BookingId" '
                        f'where b."GuestUserId"={uid} and p."Status" in (2,3);') or 0),
        "experience": int(sql(f'select count(*) from experience_bookings where "GuestUserId"={uid};') or 0),
        "service": int(sql(f'select count(*) from service_bookings where "GuestUserId"={uid};') or 0),
        # GiftCardStatus 2=Cancelled, 3=AwaitingPayment — the two that took no money.
        "gift-card": int(sql(f'select count(*) from gift_cards where "PurchasedByUserId"={uid} '
                             f'and "Status" not in (2,3);') or 0),
    }


# ---------------------------------------------------------------- scenarios

def scenario_addresses():
    """Nine literal groups, not a fallback arm — an invented tenth answers 404
    instead of the empty-shell 200 MapFallbackToFile used to leave."""
    def status(p):
        req = urllib.request.Request(B + p, method="GET")
        try:
            with urllib.request.urlopen(req, timeout=30) as r: return r.status
        except urllib.error.HTTPError as e: return e.code

    groups = ["ho-so", "bao-mat", "thanh-toan", "nhan-tien", "thong-bao",
              "quyen-rieng-tu", "tuy-chinh", "cong-tac", "gioi-thieu"]
    real = [status(f"/cai-dat/{g}") for g in groups] + [status("/cai-dat")]
    fake = status("/cai-dat/khong-co-that")

    ok("1. Muoi dia chi that tra 200, dia chi bia ra tra 404",
       all(s == 200 for s in real) and fake == 404,
       f"that={sorted(set(real))}, bia={fake}")


def scenario_never_indexed():
    """Somebody's own devices and preferences. Blocked in robots, absent from
    the sitemap — an address carrying a session behind it has no business in a
    public index."""
    with urllib.request.urlopen(B + "/robots.txt", timeout=30) as r:
        robots = r.read().decode()
    with urllib.request.urlopen(B + "/sitemap.xml", timeout=30) as r:
        sitemap = r.read().decode()
    ok("2. /cai-dat bi chan trong robots va vang mat trong sitemap",
       "Disallow: /cai-dat" in robots and "cai-dat" not in sitemap,
       f"robots={'co' if 'Disallow: /cai-dat' in robots else 'KHONG'}, "
       f"sitemap chua {sitemap.count('cai-dat')} lan")


def scenario_history_requires_a_login():
    st, _ = call(opener(), "/api/account/payments")
    ok("3. Lich su tra tien doi dang nhap", st == 401, f"http={st}")


def scenario_history_matches_the_database():
    """The endpoint reads four sources; each is counted straight off the tables
    it claims to read."""
    rows = history(GUEST)
    got = {}
    for r in rows:
        got[r["kind"]] = got.get(r["kind"], 0) + 1
    _, want = db_counts(GUEST)
    ok("4. So dong tung loai khop voi dem truc tiep trong DB",
       all(got.get(k, 0) == want[k] for k in want),
       f"api={got} db={want}")


def scenario_amounts_are_as_stored():
    """Sums compared to the đồng. The endpoint must never recompute a total on
    the way out — this is the assertion that catches it if somebody 'improves'
    it through Pricing later."""
    uid, _ = db_counts(GUEST)
    rows = history(GUEST)
    api_stay = sum(float(r["amount"]) for r in rows if r["kind"] == "stay")
    db_stay = float(sql(f'select coalesce(sum(p."Amount"),0) from payments p '
                        f'join bookings b on b."Id"=p."BookingId" '
                        f'where b."GuestUserId"={uid} and p."Status" in (2,3);') or 0)
    api_exp = sum(float(r["amount"]) for r in rows if r["kind"] == "experience")
    db_exp = float(sql(f'select coalesce(sum("Total"),0) from experience_bookings '
                       f'where "GuestUserId"={uid};') or 0)
    ok("5. So tien la so da luu, khong tinh lai",
       abs(api_stay - db_stay) < 1 and abs(api_exp - db_exp) < 1,
       f"cho o: api={api_stay:,.0f} db={db_stay:,.0f}; trai nghiem: api={api_exp:,.0f} db={db_exp:,.0f}")


def scenario_order_and_invoice_links():
    """Newest first, and the invoice link only where an invoice exists — stays.
    Experience and service bookings have no /invoice endpoint, and a link that
    404s is worse than no link."""
    rows = history(GUEST)
    ordered = all(rows[i]["at"] >= rows[i + 1]["at"] for i in range(len(rows) - 1))
    stray = [r["kind"] for r in rows if r["bookingId"] is not None and r["kind"] != "stay"]
    stays_have = all(r["bookingId"] is not None for r in rows if r["kind"] == "stay")
    ok("6. Moi nhat truoc, va lien ket hoa don chi nam tren cho o",
       ordered and not stray and stays_have,
       f"thu tu dung={ordered}, lac cho={stray or 'khong'}, cho o du link={stays_have}")


def scenario_no_leakage_between_accounts():
    """One account's history is that account's. Counted for a second seeded
    guest against their own rows."""
    other = "khach1@staylio.vn"
    rows = history(other)
    got = {}
    for r in rows:
        got[r["kind"]] = got.get(r["kind"], 0) + 1
    _, want = db_counts(other)
    ok("7. Lich su cua nguoi nay khong lan sang nguoi kia",
       all(got.get(k, 0) == want[k] for k in want),
       f"{other}: api={got} db={want}")


def scenario_unpaid_gift_cards_stay_off_the_history():
    """A card still AwaitingPayment took no money, so it is not a payment.
    Asserted against the database state rather than by buying one, so the
    scenario holds whatever earlier suites left behind."""
    uid, _ = db_counts(GUEST)
    unpaid = int(sql(f'select count(*) from gift_cards where "PurchasedByUserId"={uid} '
                     f'and "Status" in (2,3);') or 0)
    rows = history(GUEST)
    listed = {r["reference"] for r in rows if r["kind"] == "gift-card"}
    leaked = sql(f'select coalesce(string_agg("Code", \',\'), \'\') from gift_cards '
                 f'where "PurchasedByUserId"={uid} and "Status" in (2,3);')
    overlap = [c for c in leaked.split(",") if c and c in listed]
    ok("8. The chua tra tien khong nam tren lich su",
       not overlap,
       f"{unpaid} the chua tra/da huy trong DB, lot vao lich su: {overlap or 'khong'}")


def main():
    print(f"\ndocs/02 F1 — trang cai dat va lich su tra tien\n{'=' * 70}\n")
    for fn in (scenario_addresses,
               scenario_never_indexed,
               scenario_history_requires_a_login,
               scenario_history_matches_the_database,
               scenario_amounts_are_as_stored,
               scenario_order_and_invoice_links,
               scenario_no_leakage_between_accounts,
               scenario_unpaid_gift_cards_stay_off_the_history):
        try:
            fn()
        except Exception as e:  # a broken scenario must not hide the rest
            ok(fn.__name__, False, f"loi script: {e}")

    passed = sum(1 for _, p, _ in results if p)
    print(f"\n{'=' * 70}\nKET QUA: {passed}/{len(results)} dat")
    raise SystemExit(0 if passed == len(results) else 1)


if __name__ == "__main__":
    main()
