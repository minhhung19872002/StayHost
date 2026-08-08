# The ten acceptance scenarios at the end of docs/04, run against a live server.
import json, urllib.request, urllib.parse, http.cookiejar, threading, queue, datetime, subprocess, time

B = "http://localhost:5199"
results = []


def opener():
    return urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))


def admin_login():
    """docs/08 §3 - an admin account cannot sign in without the second factor,
    so the demo admin goes through the code step like everybody else."""
    st, res = call(admin, "/api/account/login",
                   {"email": "admin@stayhost.vn", "password": "stayhost123"})

    if not (res and res.get("challenge")):
        return st

    if not res.get("devCode"):
        raise SystemExit(
            "Khong lay duoc ma 2 lop cua admin. Chay server voi "
            "ASPNETCORE_ENVIRONMENT=Development de kich ban chay duoc (docs/08 §3).")

    st, _ = call(admin, "/api/account/two-factor",
                 {"challenge": res["challenge"], "code": res["devCode"]})
    return st


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
    subprocess.run(["docker", "exec", "stayhost-db", "psql", "-U", "stayhost", "-d", "stayhost",
                    "-c", statement], capture_output=True)


def record(n, title, ok, detail):
    results.append((n, title, ok, detail))
    print(f"{'PASS' if ok else 'FAIL'}  {n:>2}. {title}\n        {detail}")


# Every run picks a different stretch of the calendar so a second run does not
# collide with the bookings the first one made.
RUN_SHIFT = (int(time.time()) // 60) % 90


def future(days):
    return (datetime.date.today() + datetime.timedelta(days=days + RUN_SHIFT)).isoformat()


def bookable(op, *, instant=True, nights=3, offset=45):
    """A listing whose nine checks all pass for the dates we want."""
    _, s = call(op, f"/api/listings?pageSize=60&checkIn={future(offset)}&checkOut={future(offset + nights)}")
    for i in s['items']:
        if i['instantBook'] != instant:
            continue
        st, _ = call(op, "/api/bookings", body_for(i, offset, nights) | {"dryRun": True})
        return i
    return None


def body_for(listing, offset, nights, **kw):
    return {"listingId": listing['id'], "checkIn": future(offset), "checkOut": future(offset + nights),
            "guests": 1, "adults": 1, "children": 0, "infants": 0, "pets": 0,
            "guestName": "Khách Demo", "guestEmail": "guest@stayhost.vn", "guestNote": None,
            "paymentMethod": "card", "cardLast4": "4242"} | kw


def book_and_pay(op, listing, offset, nights=3):
    st, held = call(op, "/api/bookings", body_for(listing, offset, nights))
    if st != 201:
        return st, held
    return call(op, f"/api/bookings/{held['id']}/pay", {})


anon = opener()

# --- 1 ---------------------------------------------------------------------
ci, co = future(30), future(33)
_, s = call(anon, f"/api/listings?q=da%20lat&guests=2&checkIn={ci}&checkOut={co}")
same = []
for i in s['items'][:3]:
    _, q = call(anon, f"/api/quote?listingId={i['id']}&checkIn={ci}&checkOut={co}&guests=2")
    _, d = call(anon, f"/api/listings/{i['slug']}?checkIn={ci}&checkOut={co}&guests=2")
    same.append(i['stayTotal'] == q['total'] == d['card']['stayTotal'])
record(1, "Tìm Đà Lạt 2 khách 3 đêm, giá giống nhau ở 3 nơi",
       s['total'] > 0 and all(same),
       f"{s['total']} kết quả, {sum(same)}/{len(same)} chỗ khớp giá thẻ = chi tiết = báo giá")

# --- 2 ---------------------------------------------------------------------
newbie = opener()
email = f"acceptance{int(time.time())}@stayhost.vn"
st, user = call(newbie, "/api/account/register",
                {"email": email, "password": "stayhost123", "fullName": "Khách Nghiệm Thu",
                 "phone": None, "dateOfBirth": "1995-06-15"})   # docs/01 TK-03: đủ 18 tuổi
st_v, verify = call(newbie, "/api/account/send-verification")
_, listings = call(newbie, "/api/listings?pageSize=3")
first = listings['items'][0]
st_f, fav = call(newbie, f"/api/favorites/{first['id']}")
st_w, lists = call(newbie, "/api/wishlists")
record(2, "Đăng ký, xác minh email, lưu yêu thích, xem danh sách",
       st == 200 and st_v == 200 and st_f == 200 and st_w == 200 and len(lists) >= 1,
       f"tài khoản {user['email']}, xác minh {'có link' if verify.get('verifyLink') else 'đã gửi'}, "
       f"{len(lists)} danh sách, {fav.get('count', '?')} chỗ đã lưu")

# --- 3 ---------------------------------------------------------------------
guest = opener()
call(guest, "/api/account/login", {"email": "guest@stayhost.vn", "password": "stayhost123"})
_, s3 = call(guest, f"/api/listings?pageSize=60&checkIn={future(45)}&checkOut={future(48)}")
inst = next(i for i in s3['items'] if i['instantBook'])
st3, paid = book_and_pay(guest, inst, 45)
_, trips = call(guest, "/api/bookings")
in_trips = any(t['id'] == paid.get('id') for t in trips) if st3 == 200 else False
has_invoice = bool(paid.get('lines')) if st3 == 200 else False
record(3, "Đặt ngay, thanh toán, thấy trong chuyến đi, có hoá đơn",
       st3 == 200 and in_trips and has_invoice,
       f"{paid.get('statusLabel')}, {len(paid.get('lines', []))} dòng hoá đơn, "
       f"{'có' if in_trips else 'không'} trong danh sách chuyến đi")

# --- 4 ---------------------------------------------------------------------
_, s4 = call(guest, f"/api/listings?pageSize=60&checkIn={future(60)}&checkOut={future(63)}")
req_listing = next((i for i in s4['items'] if not i['instantBook']), None)
st4a, request = call(guest, "/api/bookings", body_for(req_listing, 60, 3))
_, det4 = call(guest, f"/api/listings/{req_listing['slug']}")

owner = None
for n in range(1, 11):
    op = opener()
    call(op, "/api/account/login", {"email": f"host{n}@stayhost.vn", "password": "stayhost123"})
    _, me = call(op, "/api/account/me")
    if me and me['id'] == det4['host']['userId']:
        owner = op
        break

st4b, _ = call(owner, f"/api/host/bookings/{request['id']}/confirm", {"reason": None})
_, after = call(guest, f"/api/bookings/{request['id']}")
record(4, "Yêu cầu đặt, chủ nhà chấp nhận, trừ tiền, xác nhận",
       st4a == 201 and st4b == 204 and after['status'] == 'Confirmed' and after['paymentStatus'] == 'Captured',
       f"{request['statusLabel']} → {after['statusLabel']}, thanh toán {after['paymentStatus']}, "
       f"{len(after['history'])} dòng lịch sử")

# --- 5 ---------------------------------------------------------------------
_, s5 = call(guest, f"/api/listings?pageSize=60&checkIn={future(80)}&checkOut={future(83)}")
mod = None
for i in s5['items']:
    if not i['instantBook']:
        continue
    _, d = call(guest, f"/api/listings/{i['slug']}")
    if d['cancellationPolicy'].startswith("Huỷ miễn phí đến 5 ngày"):
        mod = i
        break
mod = mod or next(i for i in s5['items'] if i['instantBook'])

st5, booked5 = book_and_pay(guest, mod, 80)
st5b, refund = call(guest, f"/api/bookings/{booked5['id']}/cancel", {}, m="POST")

admin = opener()
admin_login()
_, ov5 = call(admin, "/api/admin/overview")
record(5, "Huỷ trước 5 ngày, hoàn 100%, sổ sách cân bằng",
       st5b == 200 and refund['refund'] == booked5['total'] and ov5['ledger']['imbalance'] == 0,
       f"hoàn {refund['refund']:,.0f}/{booked5['total']:,.0f}₫, sổ lệch {ov5['ledger']['imbalance']}")

# --- 6 ---------------------------------------------------------------------
host1 = opener()
call(host1, "/api/account/login", {"email": "host1@stayhost.vn", "password": "stayhost123"})
title = f"Nhà nghiệm thu {int(time.time())}"
st6, created = call(host1, "/api/host/listings", {
    "title": title, "city": "Quy Nhơn", "typeKey": "house", "roomTypeKey": "entire",
    "bedrooms": 2, "beds": 2, "bathrooms": 1, "maxGuests": 4,
    "pricePerNight": 900000, "cleaningFee": 200000, "minNights": 1,
    "instantBook": True, "isPublished": True, "cancellationTier": "Moderate",
    "description": "Nhà mới đăng để chạy kịch bản nghiệm thu số sáu của tài liệu.",
    "highlight": None, "latitude": None, "longitude": None,
    # docs/01 CN-07 — five photos is the bar for going public.
    "images": [f"https://images.pexels.com/photos/{pid}/pexels-photo-{pid}.jpeg"
               for pid in (271624, 271639, 1571460, 106399, 275484)],
    "imageCaptions": ["Ảnh bìa", "Phòng khách", "Phòng ngủ", "Bếp", "Phòng tắm"],
    "amenityKeys": ["wifi", "kitchen"], "pricing": None,
    "legal": {"licenseNumber": None, "hasSecurityCameras": False, "securityCameraNote": None,
              "hasWeaponsOnProperty": False, "hasDangerousAnimals": False},
    "wizardStep": 0, "isComplete": True})
_, found = call(anon, f"/api/listings?q={urllib.parse.quote('Quy Nhon')}&pageSize=60")
appears = any(x['title'] == title for x in found['items'])
record(6, "Chủ nhà đăng tin mới, xuất bản, xuất hiện trong tìm kiếm",
       st6 == 201 and appears,
       f"tạo {created.get('slug')}, tìm không dấu 'Quy Nhon' {'thấy' if appears else 'KHÔNG thấy'}")

# --- 7 ---------------------------------------------------------------------
lid = created['id']
st7, _ = call(host1, f"/api/host/listings/{lid}/days",
              {"from": future(100), "to": future(104), "nightlyRate": 2222000,
               "minNights": None, "blocked": None, "label": "Nghiệm thu"})
_, cal7 = call(anon, f"/api/listings/{lid}/calendar?from={future(100)}&to={future(105)}")
seen = [n['rate'] for n in cal7['nights'][:5]]
_, q7 = call(anon, f"/api/quote?listingId={lid}&checkIn={future(100)}&checkOut={future(103)}&guests=1")
record(7, "Chủ nhà đổi giá 5 ngày, khách thấy ngay",
       st7 == 204 and all(r == 2222000 for r in seen) and q7['roomBeforeDiscount'] == 2222000 * 3,
       f"lịch báo {seen[0]:,.0f}₫/đêm, báo giá 3 đêm {q7['roomBeforeDiscount']:,.0f}₫")

# --- 8 ---------------------------------------------------------------------
a, c = opener(), opener()
call(a, "/api/account/login", {"email": "guest@stayhost.vn", "password": "stayhost123"})
call(c, "/api/account/login", {"email": "host2@stayhost.vn", "password": "stayhost123"})
race_body = body_for(created, 120, 3)
out = queue.Queue()


def go(op, tag):
    out.put((tag,) + call(op, "/api/bookings", race_body))


t1, t2 = threading.Thread(target=go, args=(a, "A")), threading.Thread(target=go, args=(c, "B"))
t1.start(); t2.start(); t1.join(); t2.join()
race = [out.get(), out.get()]
won = [r for r in race if r[1] == 201]
record(8, "Hai người đặt cùng lúc, chỉ một người thành công",
       len(won) == 1,
       "; ".join(f"{tag}: {st}" for tag, st, _ in race))

# --- 9 ---------------------------------------------------------------------
# Push the scenario-4 booking into the past and complete it, then both sides review.
sql(f'UPDATE bookings SET "CheckIn"=CURRENT_DATE-5, "CheckOut"=CURRENT_DATE-2, "Status"=4 '
    f'WHERE "Id"={request["id"]};')
_, before9 = call(anon, f"/api/listings/{req_listing['slug']}")
st9a, r9a = call(guest, f"/api/bookings/{request['id']}/review",
                 {"bookingId": request['id'], "rating": 5, "text": "Chỗ nghỉ rất sạch và đúng mô tả.",
                  "cleanliness": 5, "accuracy": 5, "checkIn": 5, "communication": 5,
                  "location": 5, "value": 5, "privateNote": None})
_, mid9 = call(anon, f"/api/listings/{req_listing['slug']}")
st9b, r9b = call(owner, f"/api/host/bookings/{request['id']}/review-guest",
                 {"rating": 5, "text": "Khách giữ gìn nhà cửa, trao đổi rõ ràng.", "wouldHostAgain": True})
_, after9 = call(anon, f"/api/listings/{req_listing['slug']}")
blind_held = len(mid9['reviews']) == len(before9['reviews'])
published = len(after9['reviews']) == len(before9['reviews']) + 1
record(9, "Cả hai đánh giá, công khai cùng lúc, điểm cập nhật",
       st9a == 200 and st9b == 200 and blind_held and published,
       f"sau khi khách gửi: {len(mid9['reviews'])} đánh giá (mù), sau khi chủ nhà gửi: "
       f"{len(after9['reviews'])}, điểm {before9['card']['rating']} → {after9['card']['rating']}")

# --- 10 --------------------------------------------------------------------
st10a, kase = call(owner, "/api/resolutions", {
    "bookingId": request['id'], "kind": "Damage", "amountClaimed": 1200000,
    "description": "Khách làm vỡ bàn kính, có ảnh chụp lúc dọn phòng sau khi trả phòng.",
    "evidenceUrls": []})
case_id = (kase or {}).get("id")
if case_id is None:
    # A crash here used to hide the reason the case was refused; the run should
    # say what happened and carry on to the summary line.
    record(10, "Bồi thường, chủ nhà phản đối, admin phân xử, tiền chia đúng", False,
           f"không mở được hồ sơ: {st10a} {str(kase)[:200]}")
    print()
    print("=" * 70)
    print(f"KẾT QUẢ: {sum(RESULTS)}/{len(RESULTS)} tình huống nghiệm thu đạt")
    raise SystemExit(1)

st10b, disputed = call(guest, f"/api/resolutions/{case_id}/respond",
                       {"accept": False, "note": "Bàn đã nứt sẵn từ trước."})
st10c, decided = call(admin, f"/api/resolutions/{case_id}/decide",
                      {"amountAwarded": 500000, "decision": "Chia đôi trách nhiệm theo bằng chứng hai bên."})
_, ov10 = call(admin, "/api/admin/overview")
record(10, "Bồi thường, chủ nhà phản đối, admin phân xử, tiền chia đúng",
       st10a == 201 and st10b == 200 and st10c == 200
       and decided['amountAwarded'] == 500000 and ov10['ledger']['imbalance'] == 0,
       f"yêu cầu {kase['amountClaimed']:,.0f}₫ → phân xử {decided['amountAwarded']:,.0f}₫, "
       f"sổ lệch {ov10['ledger']['imbalance']}, nhật ký {len(ov10['auditLog'])} dòng")

passed = sum(1 for r in results if r[2])
print(f"\n{'=' * 70}\nKẾT QUẢ: {passed}/10 tình huống nghiệm thu đạt")
for n, title, ok, _ in results:
    if not ok:
        print(f"   chưa đạt: {n}. {title}")
