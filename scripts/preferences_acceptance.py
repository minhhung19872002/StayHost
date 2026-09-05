# docs/01 TK-09 (P0) — "Cài đặt ngôn ngữ, tiền tệ, múi giờ hiển thị", the
# account-side half.
#
# Until 05/09/2026 the choice lived only in one browser's localStorage — it
# evaporated on every new device and private window, and the server never knew
# it existed (store.js said so against itself: "Nothing on the server knows it:
# the choice lives in this browser"). These scenarios drive the real endpoint
# and read users back from Postgres; the decisive one signs in from a SECOND
# cookie jar, which is what "a new device" is.
#
# Deliberately absent, said out loud: nothing server-side READS Language yet.
# Emails are still composed in Vietnamese — storing the choice is the half that
# shipped, and counting the stored half as the feature would be the YT-08
# lesson again.
#
#   ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/StayHost.Web
#   python scripts/preferences_acceptance.py
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
GUEST = "guest@staylio.vn"
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


def db_prefs(email):
    return sql("select coalesce(\"Language\",'-')||':'||coalesce(\"Currency\",'-')"
               "||':'||coalesce(\"TimeZoneId\",'-') "
               f"from users where \"Email\"='{email}';")


def clear(email):
    o = sign_in(email)
    call(o, "/api/account/preferences",
         {"language": "", "currency": "", "timeZoneId": ""}, method="PUT")


# ---------------------------------------------------------------- scenarios

def scenario_saved_on_the_account():
    """The choice lands in the users row and comes back on /me."""
    o = sign_in(GUEST)
    st, me = call(o, "/api/account/preferences",
                  {"language": "ko", "currency": "USD", "timeZoneId": "Asia/Seoul"}, method="PUT")
    row = db_prefs(GUEST)
    ok("1. Luu tren tai khoan, doc lai duoc tu DB va /me",
       st == 200 and row == "ko:USD:Asia/Seoul"
       and me.get("language") == "ko" and me.get("currency") == "USD"
       and me.get("timeZoneId") == "Asia/Seoul",
       f"http={st}, DB='{row}', me={me.get('language')}/{me.get('currency')}/{me.get('timeZoneId')}")


def scenario_survives_a_new_device():
    """The whole point. A second cookie jar is a second device: no localStorage
    travelled, and the preference is there anyway."""
    other = sign_in(GUEST)               # fresh jar — "the phone"
    st, me = call(other, "/api/account/me")
    ok("2. Sang thiet bi moi van con — vi no nam tren tai khoan",
       st == 200 and me.get("language") == "ko" and me.get("currency") == "USD"
       and me.get("timeZoneId") == "Asia/Seoul",
       f"thiet bi thu hai doc ra {me.get('language')}/{me.get('currency')}/{me.get('timeZoneId')}")


def scenario_invalid_values_are_refused_not_swallowed():
    """A typo must fail with a name, never quietly clear the preference."""
    o = sign_in(GUEST)
    st1, r1 = call(o, "/api/account/preferences",
                   {"language": "xx", "currency": "USD", "timeZoneId": "Asia/Seoul"}, method="PUT")
    st2, r2 = call(o, "/api/account/preferences",
                   {"language": "ko", "currency": "XXX", "timeZoneId": "Asia/Seoul"}, method="PUT")
    st3, r3 = call(o, "/api/account/preferences",
                   {"language": "ko", "currency": "USD", "timeZoneId": "Asia/Khong_Co"}, method="PUT")
    row = db_prefs(GUEST)
    ok("3. Gia tri sai bi tu choi co ten, khong lang le xoa",
       st1 == 400 and st2 == 400 and st3 == 400 and row == "ko:USD:Asia/Seoul",
       f"lang={st1}, currency={st2}, zone={st3}; DB van '{row}'")


def scenario_currency_must_be_on_sale():
    """The currency list is exchange_rates — one source. A code that is real
    money but not offered here is refused the same as a typo."""
    o = sign_in(GUEST)
    st, res = call(o, "/api/account/preferences",
                   {"language": "ko", "currency": "CHF", "timeZoneId": "Asia/Seoul"}, method="PUT")
    ok("4. Tien te phai nam trong exchange_rates dang bat",
       st == 400, f"CHF -> {st}: {res.get('message') if isinstance(res, dict) else res}")


def scenario_clearing_goes_back_to_never_chosen():
    """Empty means clear; null on the account means the device decides again."""
    o = sign_in(GUEST)
    st, me = call(o, "/api/account/preferences",
                  {"language": "", "currency": "", "timeZoneId": ""}, method="PUT")
    row = db_prefs(GUEST)
    ok("5. Xoa ve 'chua chon' — thiet bi tu quyet nhu truoc",
       st == 200 and row == "-:-:-" and me.get("language") is None,
       f"http={st}, DB='{row}'")


def scenario_requires_a_login():
    st, _ = call(opener(), "/api/account/preferences",
                 {"language": "ko", "currency": "USD", "timeZoneId": None}, method="PUT")
    ok("6. Doi dang nhap", st == 401, f"http={st}")


def scenario_old_accounts_are_untouched():
    """Every account from before the columns behaves exactly as before: null,
    and the client falls back to its own localStorage."""
    others = sql("select count(*) from users where \"Language\" is not null "
                 "or \"Currency\" is not null or \"TimeZoneId\" is not null;")
    ok("7. Tai khoan cu giu nguyen null — khong migration nao dat ho",
       int(others or 0) == 0,
       f"{others} tai khoan co gia tri (guest da duoc xoa lai o kich ban 5)")


def main():
    print(f"\ndocs/01 TK-09 — tuy chon nam tren tai khoan, khong bay theo trinh duyet\n{'=' * 70}\n")
    try:
        for fn in (scenario_saved_on_the_account,
                   scenario_survives_a_new_device,
                   scenario_invalid_values_are_refused_not_swallowed,
                   scenario_currency_must_be_on_sale,
                   scenario_clearing_goes_back_to_never_chosen,
                   scenario_requires_a_login,
                   scenario_old_accounts_are_untouched):
            try:
                fn()
            except Exception as e:  # a broken scenario must not hide the rest
                ok(fn.__name__, False, f"loi script: {e}")
    finally:
        clear(GUEST)

    passed = sum(1 for _, p, _ in results if p)
    print(f"\n{'=' * 70}\nKET QUA: {passed}/{len(results)} dat")
    raise SystemExit(0 if passed == len(results) else 1)


if __name__ == "__main__":
    main()
