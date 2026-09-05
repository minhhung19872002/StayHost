# docs/01 TK-09 (P0) — the SERVER half: mail in the reader's own language.
#
# The account half (preferences_acceptance.py) stored the choice; until
# 05/09/2026 nothing read it — every mail left in Vietnamese regardless. Now
# there are two paths, and the split is the point being tested:
#
#   - HAND: the frame (greeting/link/sign-off) and every secret-bearing
#     template (OTP, reset link) come from Emails.cs, translated by a person.
#     The machine must NEVER touch these — one "improved" digit of a sign-in
#     code locks somebody out with no error anywhere.
#   - MACHINE: informational notification content is translated by the
#     dispatcher AT DISPATCH, from RawTitle/RawBody, with the Vietnamese
#     original as the designed fallback. Never at queue time.
#
# These scenarios drive the real endpoints and read email_messages back from
# Postgres. Run:
#   docker compose up -d db libretranslate
#   ASPNETCORE_ENVIRONMENT=Development Translation__Provider=libretranslate \
#     Translation__Url=http://localhost:5555 \
#     dotnet run --project src/StayHost.Web --urls http://localhost:5199
#   python scripts/email_language_acceptance.py
# (Without libretranslate the machine scenarios shrink to the frame + stamp —
#  said out loud in the verdict, not silently skipped.)
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.request
import http.cookiejar

# cp1258 cannot encode precomposed Vietnamese; a verdict must never be lost to
# a character the terminal cannot draw.
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8')

B = os.environ.get("STAYHOST_URL", "http://localhost:5199").rstrip("/")
PW = "stayhost123"
GUEST = "guest@staylio.vn"
HOST = "host1@staylio.vn"
KHACH = "khach1@staylio.vn"
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


def set_language(email, lang):
    o = sign_in(email)
    call(o, "/api/account/preferences",
         {"language": lang, "currency": "", "timeZoneId": ""}, method="PUT")
    return o


def newest_mail(to_email):
    """Id|Subject|Language|RawTitle?|TranslatedAt?|Body of the newest queued mail."""
    row = sql(
        'select "Id"||\'\x01\'||"Subject"||\'\x01\'||coalesce("Language",\'-\')'
        '||\'\x01\'||coalesce("RawTitle",\'<null>\')'
        '||\'\x01\'||coalesce("TranslatedAt"::text,\'<null>\')'
        '||\'\x01\'||"Body" '
        f'from email_messages where "ToEmail"=\'{to_email}\' '
        'order by "Id" desc limit 1;')
    if not row:
        return None
    mail_id, subject, lang, raw, translated, body = row.split('\x01', 5)
    return {"id": int(mail_id), "subject": subject, "language": lang,
            "raw_title": raw, "translated_at": translated, "body": body}


def translate_enabled():
    st, cfg = call(opener(), "/api/translate/config")
    return st == 200 and isinstance(cfg, dict) and cfg.get("enabled") is True


# ---------------------------------------------------------------- scenarios

def scenario_reset_mail_is_hand_translated():
    """The reset link mail follows the account language from a HAND template:
    Japanese subject, the link intact, and RawTitle null so the machine pass
    can never touch a mail that carries a token."""
    set_language(GUEST, "ja")
    call(opener(), "/api/account/forgot-password", {"email": GUEST})
    m = newest_mail(GUEST)
    ok("1. Thu dat lai mat khau: tieu de tieng Nhat, RawTitle null (duong tay)",
       m is not None and m["subject"] == "Staylio パスワードの再設定"
       and m["language"] == "ja" and m["raw_title"] == "<null>"
       and "/reset-password?token=" in m["body"]
       and "2時間" in m["body"],
       f"subject='{m and m['subject']}', lang={m and m['language']}, raw={m and m['raw_title']}")
    return m


def scenario_otp_mail_is_hand_translated():
    """The six-digit code mail: Japanese template, code present in the body,
    and not one digit in the subject — subjects show on lock screens."""
    o = sign_in(GUEST)
    st, res = call(o, "/api/account/send-code", {"kind": "email"})
    code = res.get("devCode") if isinstance(res, dict) else None
    m = newest_mail(GUEST)
    ok("2. Thu ma 6 so: mau tay tieng Nhat, ma nam trong than, khong so nao o tieu de",
       st == 200 and code and m is not None
       and m["subject"] == "Staylio 認証コード"
       and not any(c.isdigit() for c in m["subject"])
       and code in m["body"] and m["language"] == "ja" and m["raw_title"] == "<null>",
       f"subject='{m and m['subject']}', code_in_body={bool(m and code and code in m['body'])}")
    return m


def scenario_notification_carries_frame_and_raws():
    """A real notification (co-host invite): queued with the Japanese frame
    around Vietnamese content, RawTitle/RawBody saved for the dispatcher, and
    an absolute link — a mail reader has no origin to resolve '/hosting' from."""
    sql(f"delete from co_hosts where \"Email\"='{GUEST}';")
    o = sign_in(HOST)
    st, res = call(o, "/api/host/co-hosts", {"email": GUEST, "scopes": ["calendar"]})
    m = newest_mail(GUEST)
    ok("3. Thong bao thuong: khung tieng Nhat + noi dung Viet + RawTitle cho may dich",
       st == 200 and m is not None and m["language"] == "ja"
       and m["raw_title"] == "Lời mời đồng quản lý"
       and "様" in m["body"] and "Staylio チーム" in m["body"]
       and "Lời mời đồng quản lý" in m["body"]
       and "詳細はこちら： http" in m["body"],
       f"http={st}, lang={m and m['language']}, raw='{m and m['raw_title'][:30]}'")
    return m


def scenario_dispatcher_translates_at_dispatch(mail):
    """The EmailWorker's next sweep machine-translates the CONTENT and stamps
    TranslatedAt — success or failure, so a mail never waits on a translator."""
    if mail is None:
        ok("4. May dich luc gui: co TranslatedAt", False, "khong co thu tu kich ban 3")
        return
    live = translate_enabled()
    deadline = time.time() + 50
    m = None
    while time.time() < deadline:
        m = newest_mail(GUEST)
        if m and m["id"] == mail["id"] and m["translated_at"] != "<null>":
            break
        time.sleep(3)
    stamped = m is not None and m["id"] == mail["id"] and m["translated_at"] != "<null>"
    if live:
        # A real translation replaced the Vietnamese title in subject and body,
        # and the mail says out loud that a machine wrote it.
        translated = (stamped and m["subject"] != mail["raw_title"]
                      and "Lời mời đồng quản lý" not in m["body"]
                      and "自動翻訳" in m["body"])
        ok("4. May dich luc gui: tieu de/noi dung sang tieng Nhat + dong 'tu dong dich'",
           translated,
           f"subject='{m and m['subject'][:40]}', stamped={stamped}")
    else:
        # No translator configured: the stamp still lands and the reader gets
        # the Japanese frame around the Vietnamese original — the designed
        # fallback, not a mail stuck in the queue.
        ok("4. Khong co may dich: van dong dau TranslatedAt, noi dung Viet giu nguyen",
           stamped and "Lời mời đồng quản lý" in m["body"],
           f"stamped={stamped} (libretranslate tat — kiem ban rut gon)")


def scenario_vietnamese_reader_is_untouched():
    """The control: a vi reader gets exactly the frame BuildEmailBody always
    produced, and the translation pass never even looks at the row."""
    set_language(KHACH, "vi")
    sql(f"delete from co_hosts where \"Email\"='{KHACH}';")
    o = sign_in(HOST)
    call(o, "/api/host/co-hosts", {"email": KHACH, "scopes": ["calendar"]})
    m = newest_mail(KHACH)
    ok("5. Nguoi doc tieng Viet: khung cu tung byte, may dich khong dong den",
       m is not None and m["language"] == "vi"
       and m["body"].startswith("Chào ") and "— Đội ngũ Staylio" in m["body"]
       and "Xem chi tiết: http" in m["body"] and m["translated_at"] == "<null>",
       f"lang={m and m['language']}, translated={m and m['translated_at']}")


def scenario_old_mail_is_immune():
    """Every pre-feature row has Language null — composed Vietnamese long ago.
    If the sweep ever stamps one, it is translating mail it must not touch."""
    n = sql('select count(*) from email_messages '
            'where "Language" is null and "TranslatedAt" is not null;')
    ok("6. Thu cu (Language null) khong bao gio bi dong dau dich", n == "0",
       f"{n} dong bi dong dau")


def scenario_secret_mail_stays_hand_written():
    """After the sweeps above ran, the OTP/reset mails must still read exactly
    as the hand template wrote them: RawTitle null keeps the machine out."""
    n = sql(f'select count(*) from email_messages '
            f'where "ToEmail"=\'{GUEST}\' and "RawTitle" is null '
            f'and "Language"=\'ja\' and "TranslatedAt" is not null;')
    ok("7. Thu mang bi mat: may dich chua tung cham vao", n == "0",
       f"{n} thu bi mat bi dich")


def cleanup():
    for email in (GUEST, KHACH):
        o = sign_in(email)
        call(o, "/api/account/preferences",
             {"language": "", "currency": "", "timeZoneId": ""}, method="PUT")
    sql(f"delete from co_hosts where \"Email\" in ('{GUEST}','{KHACH}');")


def main():
    st, _ = call(opener(), "/api/meta")
    if st != 200:
        raise SystemExit(f"Server chua chay o {B} (HTTP {st}).")

    try:
        m1 = scenario_reset_mail_is_hand_translated()
        scenario_otp_mail_is_hand_translated()
        m3 = scenario_notification_carries_frame_and_raws()
        scenario_dispatcher_translates_at_dispatch(m3)
        scenario_vietnamese_reader_is_untouched()
        scenario_old_mail_is_immune()
        scenario_secret_mail_stays_hand_written()
    finally:
        cleanup()

    passed = sum(1 for _, p, _ in results if p)
    print(f"\n{passed}/{len(results)} kich ban dat.")
    sys.exit(0 if passed == len(results) else 1)


if __name__ == "__main__":
    main()
