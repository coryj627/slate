#!/bin/bash
# Toolchain layer: Python for actions/setup-python, without sudo at job time.
#
# On a self-hosted Mac, setup-python installs the requested Python with
# `sudo installer`, and the runner account has no sudo (plan §4.4; Phase 3
# pilot attempt 1 failed exactly there). GitHub's hosted images avoid this by
# shipping Pythons in the runner tool cache; setup-python then finds a
# matching version there and installs nothing. This does the same for one
# series, using the very tarball setup-python would download
# (actions/python-versions) and its own setup.sh, run as root at build time.
#
# Runs inside the guest as admin: with build-time passwordless sudo (stage 3)
# or with ADMIN_PASSWORD set (in-place stage 03f).
set -euo pipefail

SERIES="${PYTHON_SERIES:-3.13}"
# setup-python hard-codes this path on macOS (src/setup-python.ts: IS_MAC ->
# AGENT_TOOLSDIRECTORY=/Users/runner/hostedtoolcache), and the runner takes
# RUNNER_TOOL_CACHE from its environment, so the runner's .env names the same
# place. Pilot attempt 2 failed with the cache under _work/_tool instead.
TOOLCACHE=/Users/runner/hostedtoolcache
RUNNER_ENV=/Users/runner/actions-runner/.env

sudo_() {
  if sudo -n /usr/bin/true 2>/dev/null; then sudo "$@"
  else printf '%s\n' "${ADMIN_PASSWORD:?admin sudo needs a password}" | sudo -S -p '' "$@"; fi
}

echo "==> newest $SERIES build in actions/python-versions"
tag="$(curl -fsSL "https://api.github.com/repos/actions/python-versions/releases?per_page=60" \
  | /usr/bin/python3 -I -c 'import json, sys
series = sys.argv[1] + "."
tags = [r["tag_name"] for r in json.load(sys.stdin) if r["tag_name"].startswith(series)]
print(tags[0] if tags else "")' "$SERIES")"
[ -n "$tag" ] || { echo "no $SERIES release found" >&2; exit 1; }
ver="${tag%%-*}"
asset="python-$ver-darwin-arm64.tar.gz"
echo "    $tag -> $asset"

stage="$HOME/py-stage"
rm -rf "$stage" && mkdir -p "$stage" && cd "$stage"
curl -fsSL -o "$asset" "https://github.com/actions/python-versions/releases/download/$tag/$asset"
tar -xzf "$asset"
[ -f setup.sh ] || { echo "tarball has no setup.sh" >&2; ls; exit 1; }

echo "==> setup.sh as root with RUNNER_TOOL_CACHE=$TOOLCACHE"
sudo_ install -d -o runner -g staff -m 755 "$TOOLCACHE"
sudo_ env RUNNER_TOOL_CACHE="$TOOLCACHE" HOME=/var/root bash ./setup.sh 2>&1 | grep -vE "^\s*$|Requirement already|WARNING: Running pip" | tail -15
sudo_ chown -R runner:staff "$TOOLCACHE"
# An earlier layout under _work/_tool is dead weight; the runner looks where .env says.
sudo_ rm -rf /Users/runner/actions-runner/_work/_tool/Python

echo "==> runner .env names the tool cache"
# sudo_ feeds the password on stdin, so never pipe data into a command it
# wraps (tee would read the password, not the line). Append from the shell.
if ! sudo_ grep -q '^RUNNER_TOOL_CACHE=' "$RUNNER_ENV"; then
  sudo_ /bin/sh -c "printf '%s\n' 'RUNNER_TOOL_CACHE=$TOOLCACHE' >> '$RUNNER_ENV'"
fi
sudo_ chown runner:staff "$RUNNER_ENV"
sudo_ grep -q "^RUNNER_TOOL_CACHE=$TOOLCACHE\$" "$RUNNER_ENV" || { echo ".env did not take the tool cache line" >&2; exit 1; }
sed 's/^/    /' "$RUNNER_ENV"

echo "==> check"
"$TOOLCACHE/Python/$ver/arm64/bin/python3" --version
[ -f "$TOOLCACHE/Python/$ver/arm64.complete" ] && echo "    $TOOLCACHE/Python/$ver/arm64.complete present"
stat -f '    %Su %N' "$TOOLCACHE/Python/$ver" "$TOOLCACHE/Python/$ver/arm64/python"
cd / && rm -rf "$stage"
