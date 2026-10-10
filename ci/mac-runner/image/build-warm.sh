#!/bin/bash
# Build the slate-mac-warm layer (plan §4.5): slate-mac-toolchain + main
# checked out and built. Runs as slate-ci; the controller runs it nightly.
#
#   ci/mac-runner/host/run-as-slate-ci.sh bash "$SLATE_RUNNER_TREE"/image/build-warm.sh
#
# Environment:
#   REF              git ref to build (default main)
#   CPU, MEMORY_GB   VM shape for the build and for jobs (default 12, 16)
#   NET=softnet|nat  guest networking during the build (default softnet)
#   KEEP_STAGE=1     keep the stage VM after a failure
#
# Refreshes the Actions runner first: downloads the latest release on the
# host, checks its SHA-256 against the release notes, and uploads it.
set -euo pipefail
export PATH=/usr/local/libexec/slate-runner/bin:/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin

state="$HOME/.slate-runner"
cache="$state/cache"
tree="${SLATE_RUNNER_TREE:-$state/mac-runner}"
cd "$tree/image"

REF="${REF:-main}"
CPU="${CPU:-12}"
MEMORY_GB="${MEMORY_GB:-16}"
NET="${NET:-softnet}"
stage=slate-mac-warm-stage

vm_exists() { tart list 2>/dev/null | awk 'NR>1 {print $2}' | grep -qx "$1"; }

# As an environment variable, never a -var argument: ps shows arguments.
export PKR_VAR_admin_password
PKR_VAR_admin_password="$(cat "$state/guest-admin-password")"

echo "== latest Actions runner, verified on the host"
mkdir -p "$cache"
json="$(curl -fsSL https://api.github.com/repos/actions/runner/releases/latest)"
tag="$(printf '%s' "$json" | /usr/bin/python3 -I -c 'import json,sys; print(json.load(sys.stdin)["tag_name"])')"
asset="actions-runner-osx-arm64-${tag#v}.tar.gz"
sha="$(printf '%s' "$json" | /usr/bin/python3 -I -c '
import json, re, sys
m = re.search(r"<!-- BEGIN SHA osx-arm64 -->([0-9a-f]{64})<!-- END SHA osx-arm64 -->", json.load(sys.stdin)["body"])
print(m.group(1) if m else "")')"
[ -n "$sha" ] || { echo "no osx-arm64 SHA in the $tag release notes" >&2; exit 1; }
[ -s "$cache/$asset" ] || curl -fsSL -o "$cache/$asset" "https://github.com/actions/runner/releases/download/$tag/$asset"
(cd "$cache" && echo "$sha  $asset" | shasum -a 256 -c -)

case "$NET" in
  softnet) net_args='["--net-softnet-block=out @host"]' ;;
  nat)     net_args='[]' ;;
  *) echo "NET must be softnet or nat" >&2; exit 1 ;;
esac

vm_exists slate-mac-toolchain || { echo "slate-mac-toolchain does not exist; run build-toolchain.sh first" >&2; exit 1; }
vm_exists "$stage" && tart delete "$stage"

echo "== stage 4: checkout and build $REF as builder"
packer init 04-warm.pkr.hcl >/dev/null
start=$(date +%s)
packer build \
  -var "ref=$REF" -var "runner_tar=$cache/$asset" \
  -var "cpu_count=$CPU" -var "memory_gb=$MEMORY_GB" -var "net_args=$net_args" \
  04-warm.pkr.hcl
echo "stage 4 took $(( $(date +%s) - start )) s"

echo "== promote"
if vm_exists slate-mac-warm; then
  vm_exists slate-mac-warm.prev && tart delete slate-mac-warm.prev
  tart rename slate-mac-warm slate-mac-warm.prev
fi
tart rename "$stage" slate-mac-warm
# Tell the controller to replace its idle VM with one from the new image.
mkdir -p "$state/state"
date -u +%Y-%m-%dT%H:%M:%SZ > "$state/state/warm-stamp"
touch "$state/state/recycle"
tart list
echo "== done: slate-mac-warm"
