#!/usr/bin/env bash
# What production is actually serving, checked by content rather than by status code.
#
# A 200 proves nothing here: another project on this machine once took the port and
# answered 200 for every address, so both the readiness wait and the acceptance
# scripts believed they were talking to Staylio. Every check below looks for
# something only this app returns.
set -uo pipefail

URL="${STAYHOST_URL:-https://staylio.vn}"
SSH_HOST="${STAYHOST_SSH:-hung@14.225.83.93}"

pass=0
fail=0

check() {
  local name="$1" got="$2" want="$3"
  if [ "$got" = "$want" ]; then
    printf '  ok    %-46s %s\n' "$name" "$got"
    pass=$((pass + 1))
  else
    printf '  HONG  %-46s %s (mong doi %s)\n' "$name" "$got" "$want"
    fail=$((fail + 1))
  fi
}

code() { curl -s -o /dev/null -m 20 -w '%{http_code}' "$URL$1"; }

echo "Kiem chung $URL"
echo
echo "1. La dung app Staylio, khong phai project khac chiem cong"
body=$(curl -s -m 20 "$URL/api/meta")
check "/api/meta co danh muc" \
      "$(printf '%s' "$body" | grep -c '"categories"' | tr -d ' ')" "1"

echo
echo "2. Dia chi that tra 200"
for p in / /help /host; do check "GET $p" "$(code $p)" "200"; done

echo
echo "3. Dia chi khong co that tra 404, khong phai 200 kem shell rong"
for p in /rooms/khong-co-that-999 /thanh-pho/khong-co-thanh-pho /linh-tinh; do
  check "GET $p" "$(code $p)" "404"
done
check "GET /api/account/send-verification (sai dong tu)" \
      "$(code /api/account/send-verification)" "404"

echo
echo "4. www gop ve ten mien tran"
check "www -> 301" \
      "$(curl -s -o /dev/null -m 20 -w '%{http_code}' "https://www.staylio.vn/help")" "301"

echo
echo "5. SEO: robots + sitemap, ca GET lan HEAD"
check "GET /robots.txt"  "$(code /robots.txt)"  "200"
check "GET /sitemap.xml" "$(code /sitemap.xml)" "200"
check "HEAD /robots.txt" \
      "$(curl -sI -o /dev/null -m 20 -w '%{http_code}' "$URL/robots.txt")" "200"
check "HEAD /sitemap.xml" \
      "$(curl -sI -o /dev/null -m 20 -w '%{http_code}' "$URL/sitemap.xml")" "200"
locs=$(curl -s -m 30 "$URL/sitemap.xml" | grep -c '<loc>' | tr -d ' ')
printf '  --    %-46s %s dia chi\n' "sitemap co bao nhieu duong" "$locs"

echo
echo "6. The chia se do MAY CHU sinh (Facebook/Zalo khong chay JS nen chi doc duoc cai nay)"
slug=$(curl -s -m 30 "$URL/sitemap.xml" \
       | grep -o '<loc>[^<]*/rooms/[^<]*</loc>' | head -1 \
       | sed 's|.*/rooms/||; s|</loc>||')
if [ -n "$slug" ]; then
  # Bo comment truoc khi dem. index.html co mot khoi comment giai thich vi sao
  # KHONG dat <link rel="canonical"> co dinh — va no viet nguyen ca the do ra,
  # nen dem tho se ra 2 canonical tren mot trang chi co mot. Bao dong gia keo
  # dai tu 08c1069.
  head=$(curl -s -m 20 "$URL/rooms/$slug" | perl -0777 -pe 's/<!--.*?-->//gs')
  check "og:image co mat"   "$(printf '%s' "$head" | grep -c 'property="og:image"' | tr -d ' ')" "1"
  check "canonical co mat"  "$(printf '%s' "$head" | grep -c 'rel="canonical"'     | tr -d ' ')" "1"
  printf '  --    %-46s %s\n' "tin dem thu" "$slug"
else
  printf '  --    %-46s\n' "sitemap chua co tin dang nao de thu"
fi

echo
echo "7. Container tren VPS"
if img=$(ssh -o BatchMode=yes -o ConnectTimeout=10 "$SSH_HOST" \
         'docker inspect stayhost-web --format "{{.Config.Image}}"' 2>/dev/null); then
  printf '  --    %-46s %s\n' "image dang chay" "${img##*:}"
  status=$(ssh -o BatchMode=yes "$SSH_HOST" \
           'docker ps --filter name=stayhost-web --format "{{.Status}}"' 2>/dev/null)
  printf '  --    %-46s %s\n' "trang thai" "$status"

  # The only claim that matters: is production serving the commit in this checkout?
  if head_sha=$(git rev-parse HEAD 2>/dev/null); then
    if [ "${img##*:sha-}" = "$head_sha" ]; then
      printf '  ok    %-46s %s\n' "prod dang chay dung HEAD" "${head_sha:0:7}"
      pass=$((pass + 1))
    else
      printf '  HONG  %-46s prod=%s HEAD=%s\n' \
             "prod KHAC HEAD" "${img##*:sha-}" "${head_sha:0:7}"
      printf '        Doi them vai phut (GitHub tung tao run tre ca tieng),\n'
      printf '        roi moi: gh workflow run ci-cd.yml --ref main\n'
      fail=$((fail + 1))
    fi
  fi
else
  printf '  --    %-46s\n' "khong SSH duoc vao VPS, bo qua muc nay"
fi

echo
echo "-------------------------------------------------------------"
echo "$pass dat · $fail hong"
[ "$fail" -eq 0 ]
