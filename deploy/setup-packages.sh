#!/usr/bin/env bash
# Staylio deploy — step 1: system packages (Docker, Nginx, Certbot).
# Idempotent: safe to re-run.
set -euo pipefail

DEPLOY_USER="hung"

echo "==> Installing prerequisites"
apt-get update -qq
apt-get install -y -qq ca-certificates curl gnupg nginx

echo "==> Adding Docker's official apt repository"
install -m 0755 -d /etc/apt/keyrings
if [ ! -f /etc/apt/keyrings/docker.asc ]; then
  curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
  chmod a+r /etc/apt/keyrings/docker.asc
fi
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
  > /etc/apt/sources.list.d/docker.list

echo "==> Installing Docker Engine + Compose plugin"
apt-get update -qq
apt-get install -y -qq docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

echo "==> Installing Certbot (nginx plugin)"
apt-get install -y -qq certbot python3-certbot-nginx

echo "==> Enabling services"
systemctl enable --now docker
systemctl enable --now nginx

echo "==> Granting '$DEPLOY_USER' access to the Docker socket"
usermod -aG docker "$DEPLOY_USER"

echo "==> Opening HTTP/HTTPS in the firewall (if ufw is in use)"
if command -v ufw >/dev/null 2>&1 && ufw status | grep -q "Status: active"; then
  ufw allow 80/tcp
  ufw allow 443/tcp
  ufw reload
else
  echo "    ufw is not active — skipping"
fi

echo
echo "==> Versions"
docker --version
docker compose version
nginx -v 2>&1
certbot --version
echo
echo "DONE — step 1 complete."
