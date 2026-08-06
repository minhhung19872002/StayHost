#!/usr/bin/env bash
# Turns an already-registered runner into a systemd service so it survives a reboot.
# Run once, after deploy/install-runner.sh (or after the runner was configured by hand).
#
#   sudo bash deploy/setup-runner-service.sh
set -euo pipefail

RUNNER_DIR="/home/hung/actions-runner"
RUNNER_USER="hung"

if [ ! -f "$RUNNER_DIR/.runner" ]; then
  echo "No configured runner at $RUNNER_DIR — run deploy/install-runner.sh first." >&2
  exit 1
fi

cd "$RUNNER_DIR"

# A runner started by hand holds the same registration; systemd cannot take it over
# while that process is alive.
if pgrep -u "$RUNNER_USER" -f Runner.Listener >/dev/null; then
  echo "==> Stopping the hand-started runner"
  pkill -u "$RUNNER_USER" -f Runner.Listener || true
  for _ in $(seq 1 20); do
    pgrep -u "$RUNNER_USER" -f Runner.Listener >/dev/null || break
    sleep 1
  done
fi

echo "==> Installing the systemd service"
./svc.sh install "$RUNNER_USER"
./svc.sh start
sleep 3
./svc.sh status

echo
echo "DONE — the runner now starts on boot."
