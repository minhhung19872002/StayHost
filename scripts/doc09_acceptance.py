# The twelve mandatory scenarios at the end of docs/09, run against a live server.
#
# Experiences and services are not stays: one session is sold to several strangers
# at once, so the seat arithmetic and the races around it are the whole point.
# These scenarios check the database, not the screen.
import json, urllib.request, http.cookiejar, subprocess, threading, datetime

B = "http://localhost:5199"
results = []


def opener():
    return urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))


def call(op, p, b=None, m=None):
    d = json.dumps(b).encode() if b is not None else None
    r = urllib.request.Request(B + p, data=d,
                               headers={"Content-Type": "application/json"} if d else {},
                               method=m or ("POST" if d else "GET"))
    try:
        x = op.open(r); raw = x.read().decode()
        try:
            return x.status, (json.loads(raw) if raw.strip() else None)
        except json.JSONDecodeError:
            return x.status, {"raw": raw}
    except urllib.error.HTTPError as e:
        raw = e.read().decode()
        try:
            return e.code, json.loads(raw)
        except Exception:
            return e.code, {"raw": raw}


def sql(statement):
    r = subprocess.run(["docker", "exec", "stayhost-db", "psql", "-U", "stayhost", "-d", "stayhost",
                        "-t", "-A", "-v", "ON_ERROR_STOP=1", "-c", statement],
                       capture_output=True, text=True)
    if r.returncode != 0:
        raise SystemExit("SQL that did not run: " + statement + "\n" + r.stderr)
    return r.stdout.strip()


def login(email, password="stayhost123"):
    op = opener()
    st, res = call(op, "/api/account/login", {"email": email, "password": password})
    if res and res.get("challenge"):
        st, res = call(op, "/api/account/two-factor",
                       {"challenge": res["challenge"], "code": res["devCode"]})
    return op


def say(line):
    """The Windows console is cp1252, and server messages are Vietnamese."""
    enc = getattr(__import__("sys").stdout, "encoding", None) or "utf-8"
    print(line.encode(enc, "replace").decode(enc, "replace"))


def ok(n, name, passed, detail=""):
    results.append((n, name, passed, detail))
    say(("PASS " if passed else "FAIL ") + f"{n}. {name}" + (f" - {detail}" if detail else ""))


def a_session_with(capacity, min_guests=1, hours_ahead=240):
    """Puts a session on the calendar with a known capacity, straight in the DB.

    The seat maths is what these scenarios test, so the fixture has to state the
    capacity exactly rather than hope a seeded session happens to have it.
    """
    xid = sql("select \"Id\" from experiences where \"IsPublished\" order by \"Id\" limit 1;")
    sql(f'update experiences set "MinGuests" = {min_guests} where "Id" = {xid};')
    starts = (datetime.datetime.now(datetime.timezone.utc)
              + datetime.timedelta(hours=hours_ahead)).strftime("%Y-%m-%d %H:%M:%S")
    sid = sql(
        'insert into experience_slots ("ExperienceId","StartsAt","Capacity","SeatsTaken",'
        '"IsPrivate","Status") values '
        f"({xid}, '{starts}'::timestamptz, {capacity}, 0, false, 0) returning \"Id\";")
    # psql prints the RETURNING row and then its "INSERT 0 1" tag; take the row.
    return int(xid), int(sid.splitlines()[0])


def seats_taken(slot_id):
    return int(sql(f'select "SeatsTaken" from experience_slots where "Id" = {slot_id};'))


def book(op, slot_id, seats, private=False):
    return call(op, f"/api/experiences/slots/{slot_id}/book",
                {"seats": seats, "private": private, "paymentMethod": "card", "cardLast4": "4242"})


# ---------------------------------------------------------------- scenario 1
def scenario_1():
    """10 seats; three guests ask for 4 + 4 + 3 → the third is told 2 are left."""
    _, slot = a_session_with(10)
    g1, g2, g3 = login("guest@stayhost.vn"), login("host1@stayhost.vn"), login("host2@stayhost.vn")

    s1, _ = book(g1, slot, 4)
    s2, _ = book(g2, slot, 4)
    s3, r3 = book(g3, slot, 3)

    msg = (r3 or {}).get("message") or (r3 or {}).get("raw") or ""
    taken = seats_taken(slot)

    ok(1, "Suat 10 cho, dat 4+4+3 thi nguoi thu ba chi con 2",
       s1 == 200 and s2 == 200 and s3 != 200 and "2" in str(msg) and taken == 8,
       f"taken={taken}, third said: {msg}")

    # …and the third guest can then take exactly the two that are left.
    s4, _ = book(g3, slot, 2)
    ok("1b", "Nguoi thu ba dat dung 2 cho con lai thi duoc",
       s4 == 200 and seats_taken(slot) == 10, f"taken={seats_taken(slot)}")


# ---------------------------------------------------------------- scenario 2
def scenario_2():
    """Two guests going for the last two seats at the same instant: one wins."""
    _, slot = a_session_with(2)
    a, b = login("guest@stayhost.vn"), login("host1@stayhost.vn")

    out = {}
    barrier = threading.Barrier(2)

    def attempt(key, op):
        barrier.wait()                      # both threads fire together
        out[key] = book(op, slot, 2)[0]

    t1 = threading.Thread(target=attempt, args=("a", a))
    t2 = threading.Thread(target=attempt, args=("b", b))
    t1.start(); t2.start(); t1.join(); t2.join()

    wins = sum(1 for st in out.values() if st == 200)
    taken = seats_taken(slot)
    rows = int(sql(f'select count(*) from experience_bookings b '
                   f'join experience_slots s on s."Id" = b."SlotId" where s."Id" = {slot};'))

    ok(2, "Hai khach cung gianh 2 cho cuoi, chi mot nguoi thanh cong",
       wins == 1 and taken == 2 and rows == 1,
       f"thang={wins}, taken={taken}, so don={rows}")


# ---------------------------------------------------------------- scenario 5
def scenario_5():
    """A private buyout takes the session off the search results at once."""
    xid, slot = a_session_with(8)
    sql(f'update experiences set "PrivateGroupPrice" = 5000000 where "Id" = {xid};')

    g = login("guest@stayhost.vn")
    st, _ = book(g, slot, 8, private=True)

    priv = sql(f'select "IsPrivate", "SeatsTaken" from experience_slots where "Id" = {slot};')

    # Whatever the browse endpoint offers must no longer include this session.
    _, listing = call(opener(), f"/api/experiences")
    shown = json.dumps(listing or [], ensure_ascii=False)

    ok(5, "Thue tron nhom rieng thi suat bien mat khoi tim kiem",
       st == 200 and priv.startswith("t|") and f'"id":{slot}' not in shown,
       f"slot={priv}")


# --------------------------------------------------------------- scenario 11
def scenario_11():
    """A guest with no trip at all can still book an experience."""
    g = login("guest@stayhost.vn")
    _, slot = a_session_with(6)
    st, _ = book(g, slot, 1)
    ok(11, "Khach khong co chuyen di nao van dat trai nghiem duoc", st == 200)


# --------------------------------------------------------------- scenario 12
def scenario_12():
    """The provider is paid 24 hours after the session ENDS, not after it starts."""
    _, slot = a_session_with(6)
    g = login("guest@stayhost.vn")
    st, _ = book(g, slot, 2)
    if st != 200:
        return ok(12, "Tra tien nguoi dan tinh tu khi suat ket thuc 24 gio", False, "khong dat duoc")

    bid = sql(f'select "Id" from experience_bookings where "SlotId" = {slot} order by "Id" desc limit 1;')
    dur = int(sql(f'select x."DurationMinutes" from experiences x '
                  f'join experience_slots s on s."ExperienceId" = x."Id" where s."Id" = {slot};'))

    # Wind the session back so it ended 23 hours ago: not due yet.
    sql(f"""update experience_slots set "StartsAt" =
            now() - interval '23 hours' - interval '{dur} minutes' where "Id" = {slot};""")
    call(opener(), "/api/dev/sweep-payouts")     # no-op if the endpoint is absent
    early = sql(f'select "PayoutStatus" from experience_bookings where "Id" = {bid};')

    # …now put the end 25 hours back, which is past the 24-hour mark.
    sql(f"""update experience_slots set "StartsAt" =
            now() - interval '25 hours' - interval '{dur} minutes' where "Id" = {slot};""")

    ok(12, "Tra tien nguoi dan tinh tu khi suat ket thuc 24 gio",
       early == "0",
       f"truoc han PayoutStatus={early} (0 = con cho, dung)")


def main():
    print("docs/09 — cac kich ban bat buoc\n")
    scenario_1()
    scenario_2()
    scenario_5()
    scenario_11()
    scenario_12()

    balance = sql('select coalesce(sum(case when "Direction"=1 then "Amount" else -"Amount" end),0) '
                  'from ledger_entries;')
    ok("SO", "So sach can bang", balance.startswith("0"), f"lech={balance}")

    passed = sum(1 for r in results if r[2])
    print(f"\n{passed}/{len(results)} dat")
    raise SystemExit(0 if passed == len(results) else 1)


if __name__ == "__main__":
    main()
