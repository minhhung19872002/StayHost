#!/usr/bin/env bash
# Installs the nightly database dump as a systemd timer.
#
#   sudo bash deploy/setup-backup.sh
#
# Idempotent: re-running refreshes the script and units in place.
set -euo pipefail

RUN_AS="hung"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "==> Installing /usr/local/bin/stayhost-backup"
# Copied out of the working copy on purpose: the timer must keep working even if
# the git clone is moved or deleted.
install -m 0755 "$HERE/backup-db.sh" /usr/local/bin/stayhost-backup

echo "==> Writing systemd units"
cat > /etc/systemd/system/stayhost-backup.service <<EOF
[Unit]
Description=StayHost Postgres dump
Requires=docker.service
After=docker.service

[Service]
Type=oneshot
User=$RUN_AS
ExecStart=/usr/local/bin/stayhost-backup
EOF

cat > /etc/systemd/system/stayhost-backup.timer <<'EOF'
[Unit]
Description=Nightly StayHost Postgres dump

[Timer]
OnCalendar=*-*-* 03:30:00
# Spread the load off the exact minute; the box has one CPU.
RandomizedDelaySec=15m
# Catch up after a reboot that spanned the scheduled time.
Persistent=true

[Install]
WantedBy=timers.target
EOF

systemctl daemon-reload
systemctl enable --now stayhost-backup.timer

echo "==> Running one dump now to prove the path works"
systemctl start stayhost-backup.service
systemctl status stayhost-backup.service --no-pager -n 20 || true

echo
systemctl list-timers stayhost-backup.timer --no-pager
echo
echo "DONE — dumps land in /home/$RUN_AS/backups, kept for 7 days."
