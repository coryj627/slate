#!/bin/bash
# Phase 0 step 5: timing grid over VM shapes (plan §5.1), on the warm layer.
# For each "cpu:memory_gb" shape: clone slate-mac-warm, boot in job mode, and
# as the runner account time what a PR job does on a warm tree:
#   build-mac-app.sh (no-op rebuild), swift build --build-tests, swift test
# Also times boot-to-exec. Prints one table row per shape. Runs as slate-ci.
#
#   phase0-shape-test.sh [shape ...]      default: 8:12 12:16 14:20
set -uo pipefail
export PATH=/usr/local/libexec/slate-runner/bin:/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin
cd "$HOME" || exit 1

BASE="${BASE:-slate-mac-warm}"
VM="shape-test-$$"
REPO=/Users/runner/actions-runner/_work/slate/slate
shapes=("$@"); [ ${#shapes[@]} -gt 0 ] || shapes=(8:12 12:16 14:20)

cleanup() { tart stop "$VM" >/dev/null 2>&1 || true; sleep 1; tart delete "$VM" >/dev/null 2>&1 || true; }
trap cleanup EXIT

in_guest() { tart exec "$VM" /bin/bash -lc "$1" 2>&1 | tr -d '\r'; }
timed() {  # label, command -> seconds (prints the tail of output on failure)
  local label="$1" cmd="$2" t0 t1 out rc
  t0=$(date +%s); out="$(in_guest "$cmd")"; rc=$?; t1=$(date +%s)
  if [ $rc -ne 0 ]; then echo "   FAILED $label (exit $rc):" >&2; printf '%s\n' "$out" | tail -15 >&2; echo "fail"; return; fi
  echo $((t1 - t0))
}

printf '%-8s %-6s %-10s %-10s %-10s %-10s\n' shape boot_s noop_bld_s bld_tests_s swift_test_s host_load
for shape in "${shapes[@]}"; do
  cpu="${shape%%:*}"; mem_gb="${shape##*:}"
  cleanup
  tart clone "$BASE" "$VM" >/dev/null || { echo "clone failed"; exit 1; }
  tart set "$VM" --cpu "$cpu" --memory $((mem_gb * 1024))
  t0=$(date +%s)
  tart run "$VM" --no-graphics --net-softnet-block=@host --root-disk-opts="sync=none" >"$HOME/.slate-runner/$VM.log" 2>&1 &
  for _ in $(seq 1 120); do sleep 1; tart exec "$VM" true >/dev/null 2>&1 && break; done
  boot=$(( $(date +%s) - t0 ))
  env_cmd="export CARGO_HOME=/Users/runner/toolchains/cargo RUSTUP_HOME=/Users/runner/toolchains/rustup PATH=/usr/local/slate-runner/bin:/Users/runner/toolchains/cargo/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin; cd $REPO"
  noop=$(timed "build-mac-app" "$env_cmd && ./scripts/build-mac-app.sh --skip-a11y-check >/dev/null")
  bt=$(timed "swift build --build-tests" "$env_cmd/apps/slate-mac && swift build --build-tests >/dev/null")
  load_before="$(sysctl -n vm.loadavg | awk '{print $2}')"
  st=$(timed "swift test" "$env_cmd/apps/slate-mac && DYLD_LIBRARY_PATH=$REPO/target/debug swift test --parallel 2>&1 | tail -1")
  load_after="$(sysctl -n vm.loadavg | awk '{print $2}')"
  printf '%-8s %-6s %-10s %-10s %-10s %s->%s\n' "$shape" "$boot" "$noop" "$bt" "$st" "$load_before" "$load_after"
  cleanup
done
