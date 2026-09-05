# What a crawler and a chat-app scraper actually receive.
#
# Seo.cs and SpaRoutes.cs have 31 domain tests. ShellSeo.cs and PageExistence.cs
# have none and cannot get any: they live in StayHost.Web, and the only test
# project is StayHost.Domain.Tests, which cannot reference it. Between them they
# decide the status code, the title and the share card for every address on the
# site, so the two newest pieces of the SEO work were the two unverified ones.
#
# Every scenario below is a bug that happened, not a rule someone imagined:
#
#   1  robots/sitemap answered GET but not HEAD, and SEO tools ask with HEAD
#   2  MapFallbackToFile answered 200 for every address — soft 404s everywhere
#   3  /api/ typos came back as HTML, so a GET into a POST route read as success
#   4  five sitemap pages carried the home page's title, word for word
#   5  og:* were set in JavaScript, which Facebook, Zalo and Messenger never run
#   6  DEFAULTS was read off a head the server had already rewritten per address
#   7  a canonical that dropped every query took ?trang=N down with it
#   8  addresses carrying a secret in the path must never reach the sitemap
#   9  listings, then experiences and services, had no inbound <a href> at all
#
#   ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/StayHost.Web
#   python scripts/seo_acceptance.py
import os
import re
import sys
import urllib.error
import urllib.parse
import urllib.request

# A Windows console runs cp1258 — the Vietnamese code page, and it spells
# Vietnamese with combining marks, so it cannot encode the precomposed letters
# the server actually sends. A verdict must never be lost to a character the
# terminal cannot draw.
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8')

B = os.environ.get("STAYHOST_URL", "http://localhost:5199").rstrip("/")
results = []


def ok(name, passed, detail=""):
    results.append((name, passed, detail))
    print(("PASS " if passed else "FAIL ") + name + (f" - {detail}" if detail else ""))


def fetch(path, method="GET"):
    """Status, body and content-type. A 404 is an answer here, not an error."""
    req = urllib.request.Request(B + path, method=method)
    # VNPay taught this codebase that a missing User-Agent gets you a 403 from
    # somebody; a crawler always sends one, so this should look like a crawler.
    req.add_header("User-Agent", "Mozilla/5.0 (compatible; StaylioSeoCheck/1.0)")
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            raw = r.read()
            return r.status, raw.decode("utf-8", "replace"), r.headers.get("Content-Type", "")
    except urllib.error.HTTPError as e:
        raw = e.read()
        return e.code, raw.decode("utf-8", "replace"), e.headers.get("Content-Type", "")


def tag(html, pattern):
    m = re.search(pattern, html, re.I | re.S)
    return (m.group(1).strip() if m else "")


def title_of(html):
    return tag(html, r"<title>(.*?)</title>")


def meta_prop(html, prop):
    return tag(html, rf'<meta\s+property="{re.escape(prop)}"\s+content="(.*?)"')


def canonical_of(html):
    return tag(html, r'<link\s+rel="canonical"\s+href="(.*?)"')


def sitemap_urls():
    _, body, _ = fetch("/sitemap.xml")
    return [u for u in re.findall(r"<loc>(.*?)</loc>", body)]


def path_of(url):
    return urllib.parse.urlsplit(url).path


# ---------------------------------------------------------------- scenarios

def scenario_head_is_answered():
    """[HttpGet] does not answer HEAD, and most SEO tools ask with HEAD."""
    bad = []
    for p in ("/robots.txt", "/sitemap.xml"):
        g, _, _ = fetch(p)
        h, _, _ = fetch(p, "HEAD")
        if g != 200 or h != 200:
            bad.append(f"{p} GET={g} HEAD={h}")
    ok("1. robots.txt va sitemap.xml tra loi ca GET lan HEAD", not bad,
       "; ".join(bad) or "ca hai deu 200/200")


def scenario_every_sitemap_url_is_real():
    """A sitemap that lists a 404 spends the crawl budget on nothing."""
    urls = sitemap_urls()
    bad = [(path_of(u), st) for u in urls for st in [fetch(path_of(u))[0]] if st != 200]
    ok("2. Moi dia chi trong sitemap deu tra 200", not bad and len(urls) > 0,
       f"{len(urls)} dia chi" + (f", hong: {bad[:4]}" if bad else ""))


def scenario_missing_pages_are_404():
    """MapFallbackToFile used to answer 200 with an empty shell for everything."""
    cases = {
        "/rooms/khong-co-that-999": 404,
        "/thanh-pho/khong-co-that": 404,
        "/experiences/khong-co-that": 404,
        "/services/khong-co-that": 404,
        "/help/khong-co-that": 404,
        "/duong-dan-hoan-toan-bia-dat": 404,
    }
    bad = [f"{p}={st}" for p, want in cases.items() for st in [fetch(p)[0]] if st != want]
    ok("3. Dia chi khong co trang tra 404, khong phai 200 kem shell rong", not bad,
       "; ".join(bad) or f"{len(cases)}/{len(cases)} dung")


def scenario_api_404_is_not_html():
    """Returning the shell for an /api/ typo is how a GET into a POST route
    read as success for months, and three acceptance calls 'passed' on it."""
    st, body, ctype = fetch("/api/khong-co-endpoint-nay")
    html = "<html" in body.lower() or "<!doctype" in body.lower()
    ok("4. /api/ khong khop tra 404 rong, khong tra HTML", st == 404 and not html,
       f"http={st}, html={html}, ctype={ctype or 'khong co'}, dai={len(body)}")


def scenario_no_page_wears_the_home_title():
    """/experiences, /services, /host, /help and /shield/terms all fell through
    to ShellSeo's Default and were submitted to Google carrying the home page's
    title and description word for word.

    The assertion is 'nobody else wears the home page's title', not 'every title
    is unique': two real listings may honestly share a name, and the acceptance
    suites leave fixtures behind that do (doc09 creates the same experience with
    a fresh slug on every run). Uniqueness would fail on a used database and say
    nothing about the bug."""
    _, home, _ = fetch("/")
    home_title, home_desc = title_of(home), tag(home, r'<meta name="description" content="(.*?)"')

    borrowed = []
    for u in sitemap_urls():
        p = path_of(u)
        if p == "/":
            continue
        _, body, _ = fetch(p)
        if title_of(body) == home_title or tag(body, r'<meta name="description" content="(.*?)"') == home_desc:
            borrowed.append(p)

    ok("5. Khong trang nao khac mang tieu de cua trang chu", not borrowed,
       f"tieu de trang chu='{home_title[:40]}…'"
       + (f"; muon: {borrowed[:5]}" if borrowed else "; khong trang nao muon"))


def scenario_share_card_is_server_rendered():
    """Facebook, Zalo and Messenger do not run JavaScript. Whatever the first
    response says is the only version they will ever read."""
    urls = [u for u in sitemap_urls()
            if re.search(r"/(rooms|experiences|services)/", u)]
    picked = []
    for prefix in ("/rooms/", "/experiences/", "/services/"):
        hit = next((u for u in urls if prefix in u), None)
        if hit:
            picked.append(hit)

    bad = []
    for u in picked:
        p = path_of(u)
        _, body, _ = fetch(p)
        t, img, desc = meta_prop(body, "og:title"), meta_prop(body, "og:image"), meta_prop(body, "og:description")
        if not t or not img or not desc:
            bad.append(f"{p} thieu the")
        elif img.endswith("/og-default.png"):
            bad.append(f"{p} dung anh mac dinh")
    ok("6. Trang chi tiet mang og:title/description/image cua chinh no", not bad and len(picked) == 3,
       f"{len(picked)}/3 dong san pham" + (f"; {bad}" if bad else ""))


def scenario_defaults_block_is_the_site_default():
    """lib/seo.js reads this to know what to put back when leaving a page. It
    used to read the live head, which ShellSeo had already rewritten for the
    address being served — so a room became 'the default' and its title followed
    the visitor onto the next page."""
    room = next((u for u in sitemap_urls() if "/rooms/" in u), None)
    if not room:
        return ok("7. Khoi seo-defaults la mac dinh cua san, khong phai cua trang", False,
                  "khong co tin dang nao trong sitemap")

    _, body, _ = fetch(path_of(room))
    block = tag(body, r'<script type="application/json" id="seo-defaults">(.*?)</script>')
    page_title = title_of(body)
    # The block is JSON with \uXXXX escapes; comparing to the page title is
    # enough - they must not be the same string.
    import json
    try:
        served = json.loads(block) if block else {}
    except ValueError:
        served = {}

    same_as_page = served.get("title", "") == page_title
    looks_like_home = "homestay" in served.get("title", "").lower()
    ok("7. Khoi seo-defaults la mac dinh cua san, khong phai cua trang",
       bool(block) and not same_as_page and looks_like_home,
       f"trang='{page_title[:34]}…' mac dinh='{served.get('title', '')[:34]}…'")


def scenario_canonical_keeps_only_trang():
    """Dropping every query killed pagination; keeping every query gave each
    filter its own address claiming to be the original."""
    city = next((u for u in sitemap_urls() if "/thanh-pho/" in u), None)
    if not city:
        return ok("8. Canonical bo loc ngay/khach nhung giu ?trang", False, "khong co trang thanh pho")

    p = path_of(city)
    _, plain, _ = fetch(p)
    _, filtered, _ = fetch(p + "?ngay-nhan=2026-10-01&khach=2")
    base = canonical_of(plain)
    ok("8. Canonical bo loc ngay/khach nhung giu ?trang",
       base and canonical_of(filtered) == base and "?" not in base,
       f"tran='{base}' loc='{canonical_of(filtered)}'")


def scenario_secrets_never_reach_the_sitemap():
    """/split/, /wishlist/ and /chuyen-khoan/ carry a token in the address
    itself: listing one publishes somebody else's private link."""
    _, robots, _ = fetch("/robots.txt")

    def rules(kind):
        return [l.split(":", 1)[1].strip() for l in robots.splitlines()
                if l.lower().startswith(kind)]

    disallowed, allowed = rules("disallow:"), rules("allow:")
    urls = [path_of(u) for u in sitemap_urls()]

    # Allow wins over Disallow — /shield/terms is opened on purpose while the
    # rest of /shield is closed, so matching Disallow alone reads it as a leak.
    def is_allowed(u):
        return any(a and u.startswith(a) for a in allowed)

    leaked = [u for u in urls for d in disallowed
              if d and d != "/" and not d.startswith("/*")
              and u.startswith(d) and not is_allowed(u)]
    secret = [u for u in urls
              if re.search(r"/(split|wishlist|chuyen-khoan|thanh-toan)/", u)]
    ok("9. Khong dia chi bi cam nao lot vao sitemap", not leaked and not secret,
       f"{len(disallowed)} dong Disallow, {len(urls)} dia chi"
       + (f"; lot: {(leaked + secret)[:3]}" if (leaked or secret) else ""))


def scenario_product_lines_have_inbound_links():
    """The thing that decides discovery is not the sitemap, it is links. Card.jsx
    was a div that navigated, so no listing had one; the fix stopped at the
    accommodation card and both other lines stayed unreachable."""
    try:
        from playwright.sync_api import sync_playwright
    except ImportError:
        return ok("10. Ca ba dong san pham deu co <a href> that tro toi", False,
                  "can Playwright: pip install playwright && playwright install chromium")

    with sync_playwright() as pw:
        browser = pw.chromium.launch()
        page = browser.new_page()
        try:
            page.goto(B + "/", wait_until="networkidle", timeout=45000)
            home = page.eval_on_selector_all(
                "a[href]", "els => els.map(e => e.getAttribute('href'))")

            page.goto(B + "/experiences", wait_until="networkidle", timeout=45000)
            exp = page.eval_on_selector_all(
                "a[href^='/experiences/']", "els => els.length")

            page.goto(B + "/services", wait_until="networkidle", timeout=45000)
            svc = page.eval_on_selector_all(
                "a[href^='/services/']", "els => els.length")
        finally:
            browser.close()

    to_exp = sum(1 for h in home if h == "/experiences")
    to_svc = sum(1 for h in home if h == "/services")
    to_rooms = sum(1 for h in home if h and h.startswith("/rooms/"))

    ok("10. Ca ba dong san pham deu co <a href> that tro toi",
       to_exp > 0 and to_svc > 0 and to_rooms > 0 and exp > 0 and svc > 0,
       f"tu trang chu: {to_rooms} tin dang, {to_exp} -> /experiences, {to_svc} -> /services; "
       f"trong danh muc: {exp} trai nghiem, {svc} dich vu")


def main():
    print(f"\nSEO — thu ma mot crawler va mot trinh doc the chia se that su nhan duoc\n{'=' * 70}\n")
    for fn in (scenario_head_is_answered,
               scenario_every_sitemap_url_is_real,
               scenario_missing_pages_are_404,
               scenario_api_404_is_not_html,
               scenario_no_page_wears_the_home_title,
               scenario_share_card_is_server_rendered,
               scenario_defaults_block_is_the_site_default,
               scenario_canonical_keeps_only_trang,
               scenario_secrets_never_reach_the_sitemap,
               scenario_product_lines_have_inbound_links):
        try:
            fn()
        except Exception as e:  # a broken scenario must not hide the rest
            ok(fn.__name__, False, f"loi script: {e}")

    passed = sum(1 for _, p, _ in results if p)
    print(f"\n{'=' * 70}\nKET QUA: {passed}/{len(results)} dat")
    raise SystemExit(0 if passed == len(results) else 1)


if __name__ == "__main__":
    main()
