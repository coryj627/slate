#!/bin/bash
# Toolchain layer: Python for actions/setup-python, without sudo at job time.
#
# On a self-hosted Mac, setup-python installs the requested Python with
# `sudo installer`, and the runner account has no sudo (plan §4.4; Phase 3
# pilot attempt 1 failed exactly there). GitHub's hosted images avoid this by
# shipping Pythons in the runner tool cache; setup-python then finds a
# matching version there and installs nothing. This does the same for one
# pinned release, using the very tarball setup-python would download
# (actions/python-versions) and its own setup.sh, run as root at build time.
#
# Runs inside the guest as admin: with build-time passwordless sudo (stage 3)
# or with ADMIN_PASSWORD set (in-place stage 03f).
set -euo pipefail

# One actions/python-versions release and the SHA-256 of its darwin-arm64
# asset, pinned together. To bump: pick the tag on
# https://github.com/actions/python-versions/releases and take the digest from
# that release's hashes.sha256 (upper-case there; shasum prints the same
# digest in lower case after a download). Taking the newest 3.13 unverified
# would let a substituted release run as root in the image (review finding,
# 2026-10-10). Packer passes both through as empty when not overridden.
PYTHON_TAG="${PYTHON_TAG:-3.13.16-36805956071}"
PYTHON_SHA256="${PYTHON_SHA256:-d1ae6449f4ce55578440560bb07e4ba14e59ab64627517db533ac3345b6aa3aa}"
# setup-python hard-codes this path on macOS (src/setup-python.ts: IS_MAC ->
# AGENT_TOOLSDIRECTORY=/Users/runner/hostedtoolcache), and the runner takes
# RUNNER_TOOL_CACHE from its environment, so the runner's .env names the same
# place. Pilot attempt 2 failed with the cache under _work/_tool instead.
TOOLCACHE=/Users/runner/hostedtoolcache
RUNNER_ENV=/Users/runner/actions-runner/.env

sudo_() {
  # Password on stdin from the printf builtin, never as an argument (see warm-build.sh).
  if sudo -n /usr/bin/true 2>/dev/null; then sudo "$@"
  else printf '%s\n' "${ADMIN_PASSWORD:?admin sudo needs a password}" | sudo -S -p '' "$@"; fi
}

ver="${PYTHON_TAG%%-*}"
asset="python-$ver-darwin-arm64.tar.gz"
echo "==> actions/python-versions $PYTHON_TAG -> $asset"

stage="$HOME/py-stage"
rm -rf "$stage" && mkdir -p "$stage" && cd "$stage"
curl -fsSL -o "$asset" "https://github.com/actions/python-versions/releases/download/$PYTHON_TAG/$asset"
echo "$(printf '%s' "$PYTHON_SHA256" | tr 'A-F' 'a-f')  $asset" | shasum -a 256 -c -
tar -xzf "$asset"
[ -f setup.sh ] || { echo "tarball has no setup.sh" >&2; ls; exit 1; }

echo "==> setup.sh as root with RUNNER_TOOL_CACHE=$TOOLCACHE"
sudo_ install -d -o runner -g staff -m 755 "$TOOLCACHE"
# `|| true` on the filter: under pipefail an empty grep result would fail the
# pipeline even though setup.sh succeeded.
sudo_ env RUNNER_TOOL_CACHE="$TOOLCACHE" HOME=/var/root bash ./setup.sh 2>&1 | { grep -vE "^\s*$|Requirement already|WARNING: Running pip" || true; } | tail -15
sudo_ chown -R runner:staff "$TOOLCACHE"
# An earlier layout under _work/_tool is dead weight; the runner looks where .env says.
old_layout=/Users/runner/actions-runner/_work/_tool/Python
[ -d "$old_layout" ] && sudo_ rm -rf "$old_layout"

echo "==> runner .env (installed from data/runner.env by the runner stage) names the tool cache?"
grep -q "^RUNNER_TOOL_CACHE=$TOOLCACHE\$" "$RUNNER_ENV" && echo "    yes" || echo "    NOT YET: install data/runner.env (03e-hook stage or a toolchain rebuild)"

echo "==> check"
"$TOOLCACHE/Python/$ver/arm64/bin/python3" --version
[ -f "$TOOLCACHE/Python/$ver/arm64.complete" ] && echo "    $TOOLCACHE/Python/$ver/arm64.complete present"
stat -f '    %Su %N' "$TOOLCACHE/Python/$ver" "$TOOLCACHE/Python/$ver/arm64/python"
cd / && rm -rf "$stage"
