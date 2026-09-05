# docs/02 G7 — the host's report, checked against the database it claims to read.
#
# Three of G7's four blocks existed in pieces before this: the view counts
# (QL-16), the tax year (TC-04) and the improvement checklist (QL-18). What was
# missing is what turns those numbers into a decision — money split by whether it
# has actually arrived, the rate a room really sold for beside what the
# neighbours charge, and which of the six review categories is moving.
#
# The scenarios below are the claims a reader would take from that screen, each
# one checked against the rows behind it. Two are about the shape of a series
# rather than its contents, and they matter as much: a chart drawn straight from
# a GROUP BY skips the months that earned nothing, and a line joining March to
# June with no gap says business was steady when in fact it stopped.
#
#   ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/StayHost.Web
#   python scripts/hostreport_acceptance.py
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


def sign_in(email):
    o = urllib.request.build_opener(
        urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
    st, res = call(o, "/api/account/login", {"email": email, "password": PW})
    if isinstance(res, dict) and res.get("challenge"):
        if not res.get("devCode"):
            raise SystemExit("Chay server voi ASPNETCORE_ENVIRONMENT=Development.")
        call(o, "/api/account/two-factor",
             {"challenge": res["challenge"], "code": res["devCode"]})
    return o


def report(email, days=30):
    st, r = call(sign_in(email), f"/api/host/report?days={days}")
    if st != 200:
        raise RuntimeError(f"report {st}: {r}")
    return r


def host_with_past_stays():
    """A host whose calendar has something behind it, so the money blocks have
    anything to say. Seeded hosts are mostly booked into the future."""
    email = sql("""
        select u."Email" from bookings b
        join listings l on l."Id"=b."ListingId"
        join hosts h on h."Id"=l."HostId"
        join users u on u."Id"=h."UserId"
        where b."Status" in (1,2,3,4) and b."CheckOut" < current_date
          and b."CheckOut" >= date_trunc('month', current_date) - interval '11 months'
        group by u."Email" order by count(*) desc limit 1;""")
    return email or "host1@staylio.vn"


# ---------------------------------------------------------------- scenarios

def scenario_the_month_series_has_no_holes():
    """Twelve months, oldest first, every one present. The empty ones are the
    point: without them the line joins March to June and reads as steady."""
    r = report(host_with_past_stays())
    months = r["months"]
    labels = [m["label"] for m in months]
    ordered = all(
        (months[i]["year"], months[i]["month"]) < (months[i + 1]["year"], months[i + 1]["month"])
        for i in range(len(months) - 1))
    ok("1. Chuoi thang lien tuc, 12 thang, cu truoc moi sau",
       len(months) == 12 and ordered and len(set(labels)) == 12,
       f"{len(months)} thang: {labels[0]} … {labels[-1]}, tang dan={ordered}")


def scenario_an_empty_month_reads_zero_not_missing():
    """A month that earned nothing is a zero, not a gap — the opposite call from
    the review series, where an empty month is left out because nobody rated a
    place a nought."""
    r = report(host_with_past_stays())
    empties = [m for m in r["months"] if float(m["paid"]) == 0 and float(m["upcoming"]) == 0]
    ok("2. Thang khong co doanh thu van co mat, doc ra 0",
       len(empties) > 0 and all(m["nights"] == 0 for m in empties),
       f"{len(empties)}/12 thang rong, deu 0 dem")


def scenario_only_a_bank_transfer_counts_as_paid():
    """PayoutStatus.Paid is the only state that means the bank moved it. Sent is
    a line on a file somebody still has to put through internet banking, and
    calling that 'da tra' would have the screen promising what the ledger has
    not posted. Flipped in the database and put back."""
    email = host_with_past_stays()
    booking = sql(f"""
        select b."Id" from bookings b
        join listings l on l."Id"=b."ListingId"
        join hosts h on h."Id"=l."HostId"
        join users u on u."Id"=h."UserId"
        where u."Email"='{email}' and b."Status" in (1,2,3,4)
          and b."CheckOut" < current_date
          and b."CheckOut" >= date_trunc('month', current_date) - interval '11 months'
        order by b."CheckOut" desc limit 1;""")
    if not booking:
        return ok("3. Chi ngan hang chuyen roi moi tinh la 'da tra'", False,
                  "khong tim thay don da tra phong trong cua so")

    was = sql(f'select "PayoutStatus" from payments where "BookingId"={booking};')
    before = report(email)
    sql(f'update payments set "PayoutStatus"=1 where "BookingId"={booking};')
    after = report(email)
    sql(f'update payments set "PayoutStatus"={was or 0} where "BookingId"={booking};')

    moved = sum(float(m["paid"]) for m in after["months"]) - sum(float(m["paid"]) for m in before["months"])
    fell = sum(float(m["upcoming"]) for m in before["months"]) - sum(float(m["upcoming"]) for m in after["months"])

    ok("3. Chi ngan hang chuyen roi moi tinh la 'da tra'",
       moved > 0 and abs(moved - fell) < 1,
       f"don {booking}: 'da tra' +{moved:,.0f}, 'sap tra' -{fell:,.0f} (phai bang nhau)")


def scenario_the_average_rate_is_the_room_alone():
    """docs/02 G7 puts this beside the area's going rate, and CN-10 samples
    PricePerNight. Comparing a Subtotal — which carries the cleaning fee and the
    extra-guest and pet surcharges — against a list of nightly asking prices
    would make every listing look dearer than its market for a reason that has
    nothing to do with the room."""
    email = host_with_past_stays()
    r = report(email, days=365)
    sold = [l for l in r["listings"] if float(l["avgNightlyRate"]) > 0]
    if not sold:
        return ok("4. Gia trung binh chi tinh tien phong", False, "khong tin nao ban duoc dem nao")

    listing = sold[0]
    expected = sql(f"""
        select round(sum(b."RoomBeforeDiscount" - b."RoomDiscount") / nullif(sum(b."Nights"),0))
        from bookings b
        where b."ListingId"={listing['listingId']} and b."Status" in (1,2,3,4)
          and b."CheckOut" > current_date - 365 and b."CheckIn" < current_date;""")
    # Not the subtotal, which is what a careless version would have used.
    with_fees = sql(f"""
        select round(sum(b."Subtotal") / nullif(sum(b."Nights"),0))
        from bookings b
        where b."ListingId"={listing['listingId']} and b."Status" in (1,2,3,4)
          and b."CheckOut" > current_date - 365 and b."CheckIn" < current_date;""")

    shown = float(listing["avgNightlyRate"])
    ok("4. Gia trung binh chi tinh tien phong",
       abs(shown - float(expected or 0)) < 1,
       f"hien {shown:,.0f}, tien phong {float(expected or 0):,.0f}, "
       f"neu tinh ca phi thi la {float(with_fees or 0):,.0f}")


def scenario_the_market_is_comparable_places_only():
    """Same city, same room type, within a bedroom either way — exactly CN-10's
    terms, because a studio and a five-bedroom villa are not each other's
    market. Shared with the price suggestion through Performance.Percentile so
    the two screens cannot quote a host different medians for one city."""
    email = host_with_past_stays()
    r = report(email)
    withmarket = [l for l in r["listings"] if l["marketSample"] > 0]
    if not withmarket:
        return ok("5. Mat bang chi lay cho tuong duong", False, "khong tin nao co mau so sanh")

    listing = withmarket[0]
    expected = sql(f"""
        select count(*) from listings peer
        join listings mine on mine."Id"={listing['listingId']}
        -- ListingReviewStatus.Approved is 0, not 1; 1 is Pending.
          where peer."IsPublished" and peer."ReviewStatus"=0
          and peer."City" = mine."City" and peer."RoomType" = mine."RoomType"
          and peer."Bedrooms" between mine."Bedrooms"-1 and mine."Bedrooms"+1;""")

    ok("5. Mat bang chi lay cho tuong duong",
       int(expected or 0) == listing["marketSample"] and float(listing["marketMedian"]) > 0,
       f"tin {listing['listingId']}: bao {listing['marketSample']} cho, DB dem {expected}, "
       f"trung vi {float(listing['marketMedian']):,.0f}")


def scenario_the_review_trend_carries_all_six():
    """The overall star average is already on every listing; what it cannot show
    is which of the six is dragging it. A host whose score slipped from 4.9 to
    4.6 can act on 'cleanliness fell in July' and can do nothing with 'the
    average fell'."""
    email = host_with_past_stays()
    r = report(email)
    six = ["cleanliness", "accuracy", "checkIn", "communication", "location", "value"]
    months = r["reviews"]
    if not months:
        return ok("6. Xu huong danh gia du sau hang muc", r["reviewCount"] == 0,
                  "chu nha nay chua co danh gia nao")

    complete = all(all(k in m and 0 < m[k] <= 5 for k in six) for m in months)
    counted = all(m["count"] > 0 for m in months)
    ok("6. Xu huong danh gia du sau hang muc",
       complete and counted,
       f"{len(months)} thang, moi thang du 6 hang muc, khong thang nao 0 danh gia")


def scenario_a_month_with_no_reviews_is_left_out():
    """Unlike the money series. An empty month in a score series is not a zero —
    nobody rated this place a nought — and drawing it as one invents a collapse
    that never happened."""
    email = host_with_past_stays()
    r = report(email)
    if not r["reviews"]:
        return ok("7. Thang khong co danh gia thi khong ve", True, "khong co danh gia de ve")

    labels = {m["label"] for m in r["reviews"]}
    money_labels = {m["label"] for m in r["months"]}
    ok("7. Thang khong co danh gia thi khong ve",
       labels < money_labels and all(m["count"] > 0 for m in r["reviews"]),
       f"{len(labels)} thang co danh gia trong {len(money_labels)} thang cua chuoi tien")


def scenario_a_host_with_nothing_gets_an_empty_report():
    """Not a 500, and not a row of nulls."""
    email = sql("""
        select u."Email" from users u
        join hosts h on h."UserId"=u."Id"
        left join listings l on l."HostId"=h."Id"
        where u."Email" like 'host%@staylio.vn'
        group by u."Email" having count(l."Id")=0 limit 1;""")
    if not email:
        return ok("8. Chu nha chua co tin dang: bao cao rong, khong loi", True,
                  "moi chu nha deu co tin dang")

    r = report(email)
    ok("8. Chu nha chua co tin dang: bao cao rong, khong loi",
       r["listings"] == [] and r["months"] == [] and r["reviews"] == [],
       f"{email}: {len(r['listings'])} tin, {len(r['months'])} thang")


def main():
    print(f"\ndocs/02 G7 — bao cao cua chu nha, doi chieu voi chinh DB\n{'=' * 70}\n")
    for fn in (scenario_the_month_series_has_no_holes,
               scenario_an_empty_month_reads_zero_not_missing,
               scenario_only_a_bank_transfer_counts_as_paid,
               scenario_the_average_rate_is_the_room_alone,
               scenario_the_market_is_comparable_places_only,
               scenario_the_review_trend_carries_all_six,
               scenario_a_month_with_no_reviews_is_left_out,
               scenario_a_host_with_nothing_gets_an_empty_report):
        try:
            fn()
        except Exception as e:  # a broken scenario must not hide the rest
            ok(fn.__name__, False, f"loi script: {e}")

    passed = sum(1 for _, p, _ in results if p)
    print(f"\n{'=' * 70}\nKET QUA: {passed}/{len(results)} dat")
    raise SystemExit(0 if passed == len(results) else 1)


if __name__ == "__main__":
    main()
