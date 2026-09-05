# docs/01 QT-06/TC-12, docs/07 §6 — exchange rates as configuration, and the
# booking's frozen "price the guest was shown".
#
# Two dead things had to come alive here. The eight display rates were constants
# compiled into CatalogService, drifting from the day of each deploy with
# nothing anywhere to say so. And bookings.DisplayCurrency/DisplayRate — the
# docs/07 §6 evidence column — was null on all 155 bookings, because the writer
# trusted a request field no client ever sent. The scenarios drive the real
# server and read Postgres back; none looks at a screen.
#
#   ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/StayHost.Web
#   python scripts/fx_acceptance.py
import json
import os
import subprocess
import sys
import time
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
RUN = str(int(time.time()))[-6:]
# A different window per run, so repeated runs never collide on bookings_no_overlap.
OFFSET = 200 + int(RUN) % 120
results = []


def ok(name, passed, detail=""):
    results.append((name, passed, detail))
    print(("PASS " if passed else "FAIL ") + name + (f" - {detail}" if detail else ""))


def opener():
    return urllib.request.build_opener(
        urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))


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


def sign_in(email):
    o = opener()
    st, res = call(o, "/api/account/login", {"email": email, "password": PW})
    if isinstance(res, dict) and res.get("challenge"):
        if not res.get("devCode"):
            raise SystemExit("Chay server voi ASPNETCORE_ENVIRONMENT=Development.")
        call(o, "/api/account/two-factor",
             {"challenge": res["challenge"], "code": res["devCode"]})
    return o


def usd_in_meta():
    with urllib.request.urlopen(B + "/api/meta", timeout=30) as r:
        meta = json.loads(r.read().decode())
    return next((c for c in meta["currencies"] if c["code"] == "USD"), None)


# ---------------------------------------------------------------- scenarios

def scenario_seeded_by_the_migration():
    """Eight rows, every one active, VND pinned to exactly 1, all born Feed.
    Seeded by InsertData in the migration, NOT DbSeeder — the seeder only runs
    on a blank database, which is how prod kept admin@stayhost.vn through the
    domain rename."""
    # Non-text columns cast explicitly: Postgres has no || for booleans, and a
    # query that errors comes back as an empty string that reads like "no rows"
    # — a test failing for its own reasons, not the product's.
    rows = sql("select \"Code\"||':'||\"RateFromVnd\"::text||':'||\"IsActive\"::text "
               'from exchange_rates order by "SortOrder";').split("\n")
    vnd = sql('select "RateFromVnd" from exchange_rates where "Code"=\'VND\';')
    inactive = sql('select count(*) from exchange_rates where not "IsActive";')
    ok("1. Migration tu seed 8 dong, VND ghim bang 1, tat ca dang bat",
       len(rows) == 8 and float(vnd) == 1.0 and inactive == "0",
       f"{len(rows)} dong, VND={vnd}, tat={inactive}")


def scenario_a_db_change_reaches_meta_without_a_deploy():
    """The whole point of the feature: the rate lives in the database, so
    changing it changes what every guest sees on the next request — no rebuild,
    no restart."""
    before = usd_in_meta()
    sql('update exchange_rates set "RateFromVnd"=0.00005 where "Code"=\'USD\';')
    try:
        after = usd_in_meta()
    finally:
        sql('update exchange_rates set "RateFromVnd"=0.0000392 where "Code"=\'USD\';')
    restored = usd_in_meta()

    ok("2. Doi ti gia trong DB la meta doi theo, khong can redeploy",
       before and after and abs(after["rateFromVnd"] - 0.00005) < 1e-9
       and abs(restored["rateFromVnd"] - 0.0000392) < 1e-9,
       f"truoc={before['rateFromVnd']}, sau={after['rateFromVnd']}, tra lai={restored['rateFromVnd']}")


def scenario_admin_edit_is_scoped_audited_and_pins_manual():
    """QT-06 through the real endpoint: Finance scope required, the change
    lands, the row flips to Manual so a future feed cannot overwrite a person,
    and QT-09 gets its audit line."""
    admin = sign_in("admin@staylio.vn")
    audits_before = int(sql("select count(*) from admin_audit where \"Action\"='fx.update';") or 0)

    st, _ = call(admin, "/api/admin/exchange-rates/EUR",
                 {"rateFromVnd": 0.000037, "isActive": True}, method="PUT")
    row = sql("select \"RateFromVnd\"::text||':'||\"Source\"::text from exchange_rates where \"Code\"='EUR';")
    audits_after = int(sql("select count(*) from admin_audit where \"Action\"='fx.update';") or 0)

    # put it back, also through the endpoint, so the audit trail tells the truth
    call(admin, "/api/admin/exchange-rates/EUR",
         {"rateFromVnd": 0.0000362, "isActive": True}, method="PUT")

    ok("3. Admin sua duoc, co nhat ky, va dong chuyen sang Manual",
       st == 204 and row.startswith("0.000037") and row.endswith(":1")
       and audits_after == audits_before + 1,
       f"http={st}, dong={row}, nhat ky {audits_before}->{audits_after}")


def scenario_vnd_cannot_be_moved():
    """A VND row at anything but 1 rescales every price on the site in one
    keystroke. The guard answers in Vietnamese and changes nothing."""
    admin = sign_in("admin@staylio.vn")
    st, res = call(admin, "/api/admin/exchange-rates/VND",
                   {"rateFromVnd": 0.9, "isActive": True}, method="PUT")
    vnd = sql('select "RateFromVnd" from exchange_rates where "Code"=\'VND\';')
    ok("4. VND la tien goc — khong doi duoc khoi 1",
       st == 400 and float(vnd) == 1.0,
       f"http={st}: {res.get('message') if isinstance(res, dict) else res}; VND={vnd}")


def scenario_zero_and_negative_are_refused():
    admin = sign_in("admin@staylio.vn")
    st1, _ = call(admin, "/api/admin/exchange-rates/USD",
                  {"rateFromVnd": 0, "isActive": True}, method="PUT")
    st2, _ = call(admin, "/api/admin/exchange-rates/USD",
                  {"rateFromVnd": -0.00004, "isActive": True}, method="PUT")
    usd = sql('select "RateFromVnd" from exchange_rates where "Code"=\'USD\';')
    ok("5. Ti gia 0 va am bi tu choi", st1 == 400 and st2 == 400 and float(usd) > 0,
       f"0->{st1}, am->{st2}, USD con {usd}")


def scenario_the_booking_freezes_the_servers_rate():
    """docs/07 §6 — the snapshot is stamped server-side from exchange_rates.
    The request field that once accepted a browser's own rate is gone from the
    DTO, and a caller still sending displayRate=999 changes nothing: the row
    carries the platform's rate of that instant."""
    guest = sign_in(GUEST := "guest@staylio.vn")

    # A listing with open dates far out; eight windows tried like acceptance.py.
    with urllib.request.urlopen(B + "/api/listings?limit=60", timeout=30) as r:
        cards = json.loads(r.read().decode()).get("items", [])
    booking = None
    for card in cards[:20]:
        for w in range(8):
            ci = OFFSET + w * 9
            st, held = call(guest, "/api/bookings", {
                "listingId": card["id"],
                "checkIn": time.strftime("%Y-%m-%d", time.gmtime(time.time() + ci * 86400)),
                "checkOut": time.strftime("%Y-%m-%d", time.gmtime(time.time() + (ci + 2) * 86400)),
                "guests": 2, "adults": 2,
                "displayCurrency": "USD",
                "displayRate": 999,          # must be ignored — the field no longer exists
                "agreedToRules": True,
            })
            if st == 201 and isinstance(held, dict) and held.get("id"):
                booking = held
                break
        if booking:
            break

    if not booking:
        return ok("6. Don dat dong bang ti gia cua san, khong phai cua trinh duyet", False,
                  "khong giu duoc cho nao de thu")

    row = sql("select \"DisplayCurrency\"||':'||\"DisplayRate\"::text from bookings "
              f"where \"Id\"={booking['id']};")
    usd = sql('select "RateFromVnd" from exchange_rates where "Code"=\'USD\';')
    ok("6. Don dat dong bang ti gia cua san, khong phai cua trinh duyet",
       row.startswith("USD:") and abs(float(row.split(":")[1]) - float(usd)) < 1e-12
       and "999" not in row,
       f"dong bang='{row}', ti gia san={usd}")


def scenario_books_still_balance():
    off = sql('select coalesce(sum(case when "Direction"=1 then "Amount" else -"Amount" end),0) '
              'from ledger_entries;')
    ok("7. So sach van can bang — ti gia khong cham vao tien that", float(off) == 0.0, f"lech={off}")


def main():
    print(f"\nQT-06/TC-12 + docs/07 §6 — ti gia la cau hinh, khong phai hang so\n{'=' * 70}\n")
    for fn in (scenario_seeded_by_the_migration,
               scenario_a_db_change_reaches_meta_without_a_deploy,
               scenario_admin_edit_is_scoped_audited_and_pins_manual,
               scenario_vnd_cannot_be_moved,
               scenario_zero_and_negative_are_refused,
               scenario_the_booking_freezes_the_servers_rate,
               scenario_books_still_balance):
        try:
            fn()
        except Exception as e:  # a broken scenario must not hide the rest
            ok(fn.__name__, False, f"loi script: {e}")

    passed = sum(1 for _, p, _ in results if p)
    print(f"\n{'=' * 70}\nKET QUA: {passed}/{len(results)} dat")
    raise SystemExit(0 if passed == len(results) else 1)


if __name__ == "__main__":
    main()
