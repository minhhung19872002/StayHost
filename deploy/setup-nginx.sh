#!/usr/bin/env bash
# Puts Nginx in front of the app on port 80/443 and obtains a Let's Encrypt certificate.
# Run after the stack is up, because Certbot's HTTP-01 challenge needs the site live.
#
#   sudo bash deploy/setup-nginx.sh <domain> <email-for-expiry-notices>
set -euo pipefail

DOMAIN="${1:-}"
EMAIL="${2:-}"
if [ -z "$DOMAIN" ] || [ -z "$EMAIL" ]; then
  echo "usage: sudo bash deploy/setup-nginx.sh <domain> <email>" >&2
  exit 1
fi

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "==> Checking that $DOMAIN resolves to this machine"
RESOLVED="$(getent ahostsv4 "$DOMAIN" | awk 'NR==1 {print $1}' || true)"
PUBLIC="$(curl -fsS --max-time 10 https://api.ipify.org || true)"
if [ -n "$RESOLVED" ] && [ -n "$PUBLIC" ] && [ "$RESOLVED" != "$PUBLIC" ]; then
  echo "    WARNING: $DOMAIN -> $RESOLVED but this host is $PUBLIC." >&2
  echo "    Certbot's HTTP-01 challenge will fail until the A record points here." >&2
fi

echo "==> Installing the site"
sed "s/__DOMAIN__/$DOMAIN/g" "$HERE/nginx/stayhost.conf" > /etc/nginx/sites-available/stayhost
ln -sf /etc/nginx/sites-available/stayhost /etc/nginx/sites-enabled/stayhost
rm -f /etc/nginx/sites-enabled/default
nginx -t
systemctl reload nginx

echo "==> Requesting a certificate for $DOMAIN"
# --redirect makes Certbot rewrite the port-80 block to send everything to HTTPS.
certbot --nginx --non-interactive --agree-tos --redirect \
  -d "$DOMAIN" -m "$EMAIL"

echo "==> Verifying automatic renewal"
systemctl list-timers certbot.timer --no-pager || true

echo
echo "DONE — https://$DOMAIN should now serve the app."
