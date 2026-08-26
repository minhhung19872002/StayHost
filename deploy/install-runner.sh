#!/usr/bin/env bash
# Registers this machine as a GitHub Actions self-hosted runner for the repo and
# installs it as a systemd service, so the `deploy` job can reach the Docker daemon.
#
#   bash deploy/install-runner.sh <REGISTRATION_TOKEN>
#
# Get the token (valid for one hour) at:
#   https://github.com/minhhung19872002/Staylio/settings/actions/runners/new
#
# Re-running the script re-registers the same runner name (--replace), so it is the
# recovery path as well as the install path.
set -euo pipefail

TOKEN="${1:-}"
if [ -z "$TOKEN" ]; then
  echo "usage: bash deploy/install-runner.sh <REGISTRATION_TOKEN>" >&2
  echo "token: https://github.com/minhhung19872002/Staylio/settings/actions/runners/new" >&2
  exit 1
fi

REPO_URL="https://github.com/minhhung19872002/Staylio"
RUNNER_VERSION="2.336.0"
RUNNER_DIR="$HOME/actions-runner"
# ci-cd.yml targets this label; changing it here means changing it there too.
LABELS="stayhost-vps"

echo "==> Downloading runner ${RUNNER_VERSION}"
mkdir -p "$RUNNER_DIR"
cd "$RUNNER_DIR"
if [ ! -x ./config.sh ]; then
  TARBALL="actions-runner-linux-x64-${RUNNER_VERSION}.tar.gz"
  curl -fsSLO "https://github.com/actions/runner/releases/download/v${RUNNER_VERSION}/${TARBALL}"
  tar xzf "$TARBALL"
  rm -f "$TARBALL"
else
  echo "    already downloaded"
fi

echo "==> Installing runner OS dependencies (needs sudo)"
sudo ./bin/installdependencies.sh

echo "==> Registering with $REPO_URL"
# Stop any prior service first; config.sh refuses to run while the runner is live.
if [ -f ./.service ]; then
  sudo ./svc.sh stop || true
  sudo ./svc.sh uninstall || true
fi
./config.sh --unattended --replace \
  --url "$REPO_URL" \
  --token "$TOKEN" \
  --name "$(hostname)" \
  --labels "$LABELS" \
  --work _work

echo "==> Installing as a systemd service running as $USER"
sudo ./svc.sh install "$USER"
sudo ./svc.sh start
sudo ./svc.sh status

echo
echo "DONE — the runner should now show as Idle under Settings → Actions → Runners."
