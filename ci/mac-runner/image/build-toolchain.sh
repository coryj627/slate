#!/bin/bash
# Build the slate-mac-toolchain layer (plan §4.5): slate-mac-base -> Xcode,
# accounts, runner, agent, Rust -> Finder grants -> (optionally) SIP back on.
# Then boots the result once in job mode and probes it. Runs as slate-ci:
#
#   ci/mac-runner/host/run-as-slate-ci.sh bash "$SLATE_RUNNER_TREE"/image/build-toolchain.sh
#
# Environment:
#   SIP=on|off       re-enable SIP in stage 3c (default off: skip, keep SIP off)
#   XCODE_BUILD      expected Xcode build (default 27A266a)
#   RUST_VERSION     default 1.97.1
#   NET=softnet|nat  guest networking during the build (default softnet:
#                    "out @host"; nat is the fallback if SSH-in fails)
#   KEEP_STAGE=1     keep the stage VM after a failure
#
# Inputs in ~/.slate-runner/cache (see Phase 0 notes): Xcode-<build>.tar,
# actions-runner-osx-arm64-*.tar.gz, tart-guest-agent-*-darwin-all.tar.gz,
# rustup-init.sh. Secrets created on first use under ~/.slate-runner, mode 600:
# guest-runner-password, guest-builder-password, guest-builder-key(.pub).
set -euo pipefail
export PATH=/usr/local/libexec/slate-runner/bin:/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin

state="$HOME/.slate-runner"
cache="$state/cache"
tree="${SLATE_RUNNER_TREE:-$state/mac-runner}"
cd "$tree/image"

XCODE_BUILD="${XCODE_BUILD:-27A266a}"
RUST_VERSION="${RUST_VERSION:-1.97.1}"
SIP="${SIP:-off}"
NET="${NET:-softnet}"
stage=slate-mac-toolchain-stage

vm_exists() { tart list 2>/dev/null | awk 'NR>1 {print $2}' | grep -qx "$1"; }
secret() {  # file -> prints value, creating it if missing
  local f="$state/$1"
  if [ ! -s "$f" ]; then (umask 077; openssl rand -base64 48 | tr -dc 'A-Za-z0-9' | head -c 32 > "$f"); fi
  cat "$f"
}

# Secrets reach Packer as PKR_VAR_<name> environment variables, never as
# -var arguments, which ps would show to every local account.
export PKR_VAR_admin_password PKR_VAR_runner_password PKR_VAR_builder_password PKR_VAR_builder_pubkey
PKR_VAR_admin_password="$(secret guest-admin-password)"
PKR_VAR_runner_password="$(secret guest-runner-password)"
PKR_VAR_builder_password="$(secret guest-builder-password)"
if [ ! -s "$state/guest-builder-key" ]; then
  (umask 077; ssh-keygen -q -t ed25519 -N '' -C "slate-ci builder" -f "$state/guest-builder-key")
fi
PKR_VAR_builder_pubkey="$(cat "$state/guest-builder-key.pub")"

xcode_tar="$cache/Xcode-$XCODE_BUILD.tar"
runner_tar="$(find "$cache" -maxdepth 1 -name 'actions-runner-osx-arm64-*.tar.gz' | sort -V | tail -1)"
agent_tar="$(find "$cache" -maxdepth 1 -name 'tart-guest-agent-*-darwin-all.tar.gz' | sort -V | tail -1)"
rustup_init="$cache/rustup-init.sh"
for f in "$xcode_tar" "$runner_tar" "$agent_tar" "$rustup_init"; do
  [ -s "$f" ] || { echo "missing input: $f" >&2; exit 1; }
done
echo "inputs: $(basename "$xcode_tar"), $(basename "$runner_tar"), $(basename "$agent_tar")"

case "$NET" in
  softnet) net_args='["--net-softnet-block=out @host"]' ;;
  nat)     net_args='[]' ;;
  *) echo "NET must be softnet or nat" >&2; exit 1 ;;
esac

vm_exists slate-mac-base || { echo "slate-mac-base does not exist; run build-base.sh first" >&2; exit 1; }
vm_exists "$stage" && tart delete "$stage"

# Packer deletes the stage VM when a provisioner fails; KEEP_STAGE=1 keeps it
# for inspection instead.
on_error=cleanup; [ "${KEEP_STAGE:-0}" = 1 ] && on_error=abort

echo "== stage 3: Xcode, accounts, runner, agent, Rust"
packer init 03-toolchain.pkr.hcl >/dev/null
packer build -on-error="$on_error" \
  -var "xcode_tar=$xcode_tar" -var "xcode_build=$XCODE_BUILD" \
  -var "runner_tar=$runner_tar" -var "agent_tar=$agent_tar" -var "rustup_init=$rustup_init" \
  -var "rust_version=$RUST_VERSION" -var "net_args=$net_args" \
  03-toolchain.pkr.hcl

echo "== stage 3b: Finder grants, tighten admin sudo"
packer build -var "net_args=$net_args" 03b-toolchain-tcc.pkr.hcl

if [ "$SIP" = on ]; then
  echo "== stage 3c: SIP back on"
  packer build 03c-enable-sip.pkr.hcl
fi

echo "== probe: boot in job mode, check the session, Finder, toolchains"
tart run "$stage" --no-graphics --net-softnet-block=@host --root-disk-opts=sync=none >"$state/$stage.probe.log" 2>&1 &
run_pid=$!
ok=0
for _ in $(seq 1 90); do
  sleep 2
  kill -0 "$run_pid" 2>/dev/null || break
  tart exec "$stage" true >/dev/null 2>&1 && { ok=1; break; }
done
if [ "$ok" != 1 ]; then
  echo "guest agent never answered; run log:" >&2; cat "$state/$stage.probe.log" >&2
  tart stop "$stage" 2>/dev/null || true; exit 1
fi
probe() { echo "-- $1"; shift; tart exec "$stage" "$@" 2>&1 | sed 's/^/   /'; }
probe "who runs tart exec"            id -un
probe "session"                       launchctl managername
probe "Finder Apple Events (20 s cap)" perl -e 'alarm 20; exec @ARGV' osascript -e 'tell application "Finder" to get name of startup disk'
probe "Xcode"                         xcodebuild -version
probe "Rust"                          /bin/sh -c 'CARGO_HOME=/Users/runner/toolchains/cargo RUSTUP_HOME=/Users/runner/toolchains/rustup /Users/runner/toolchains/cargo/bin/cargo --version'
probe "hook is root's"                stat -f '%Su %Sp %N' /usr/local/slate-runner/hooks/job-started.sh
probe "sudo for runner"               /bin/sh -c 'sudo -n true 2>&1 || echo "runner has no sudo (expected)"'
probe "image info"                    cat /etc/slate-image-info
probe "SIP"                           csrutil status
tart stop "$stage" >/dev/null 2>&1 || true
wait "$run_pid" 2>/dev/null || true

echo "== promote"
if vm_exists slate-mac-toolchain; then
  vm_exists slate-mac-toolchain.prev && tart delete slate-mac-toolchain.prev
  tart rename slate-mac-toolchain slate-mac-toolchain.prev
fi
tart rename "$stage" slate-mac-toolchain
tart list
echo "== done: slate-mac-toolchain"
