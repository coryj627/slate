#!/bin/bash
# Warm layer: check out and build main as builder, hand the result to runner.
# Runs inside the guest as admin over SSH (04-warm.pkr.hcl). admin's sudo
# needs the password, which arrives in ADMIN_PASSWORD from the host.
set -euo pipefail

WORK=/Users/runner/actions-runner/_work
REPO_DIR="$WORK/slate/slate"            # where actions/checkout puts coryj627/slate
TOOLCHAINS=/Users/runner/toolchains
export CARGO_HOME="$TOOLCHAINS/cargo" RUSTUP_HOME="$TOOLCHAINS/rustup"

# The password goes to sudo on stdin from the printf builtin, which spawns no
# process. It must never be a process argument: main's own build runs as builder while these commands run,
# and macOS shows every user's process arguments, so an argument would hand a
# hostile build step the admin password, and with it `su admin` and root.
as_root() { printf '%s\n' "$ADMIN_PASSWORD" | sudo -S -p '' "$@"; }
as_builder() {
  # builder's environment is set explicitly; it has no login shell history.
  printf '%s\n' "$ADMIN_PASSWORD" | sudo -S -p '' -u builder -H \
    env PATH="/usr/local/slate-runner/bin:$CARGO_HOME/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin" \
        CARGO_HOME="$CARGO_HOME" RUSTUP_HOME="$RUSTUP_HOME" HOME=/Users/builder \
        CARGO_NET_RETRY=5 CARGO_TERM_COLOR=never \
    "$@"
}

echo "==> Refresh the Actions runner (host-verified tarball)"
as_root tar -C /Users/runner/actions-runner -xzf /Users/admin/actions-runner.tar.gz
rm -f /Users/admin/actions-runner.tar.gz

echo "==> Hand toolchains and workspace to builder"
as_root install -d -o runner -g staff -m 755 "$WORK"
as_root chown -R builder:staff "$TOOLCHAINS" "$WORK"

echo "==> Checkout $REPO_URL @ $REF"
# Always a fresh clone: this stage starts from slate-mac-toolchain, which has
# no checkout, so there is nothing to fetch into. REPO_URL must stay byte for
# byte what actions/checkout compares against (see 04-warm.pkr.hcl), or the
# job's checkout step throws the warm products away.
as_builder install -d "$WORK/slate"
as_builder git clone --depth 1 --branch "$REF" "$REPO_URL" "$REPO_DIR"
as_builder git -C "$REPO_DIR" log -1 --format='   %H %s'
# Read while builder still owns the tree; afterwards git refuses the
# ownership mismatch ("dubious ownership").
warm_commit="$(as_builder git -C "$REPO_DIR" rev-parse HEAD)"

echo "==> Build, as swift-tests.yml does"
start=$(date +%s)
as_builder bash -c "cd '$REPO_DIR' && ./scripts/build-mac-app.sh --skip-a11y-check"
as_builder bash -c "cd '$REPO_DIR/apps/slate-mac' && swift build --build-tests"
as_builder bash -c "cd '$REPO_DIR' && make swift-cli"
echo "   build took $(( $(date +%s) - start )) s"

echo "==> Hand everything to runner"
as_root chown -R runner:staff "$TOOLCHAINS" "$WORK" /Users/runner/actions-runner
as_root chmod 755 /Users/runner/actions-runner

echo "==> Record"
# as_root feeds sudo its password on stdin, so it must never wrap a command
# that reads stdin itself (tee, cat). Build the record in a file and install it.
record="$(mktemp)"
{
  grep -v '^layer=\|^built=\|^warm_\|^runner=' /etc/slate-image-info
  echo "built=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "layer=warm"
  echo "warm_ref=$REF"
  echo "warm_commit=$warm_commit"
  echo "runner=$(printf '%s\n' "$ADMIN_PASSWORD" | sudo -S -p '' -u runner /Users/runner/actions-runner/bin/Runner.Listener --version 2>/dev/null | tail -1 || echo unknown)"
} > "$record"
as_root install -o root -g wheel -m 644 "$record" /etc/slate-image-info
rm -f "$record"
cat /etc/slate-image-info
df -h / | tail -1
