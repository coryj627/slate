#!/bin/bash
# Phase 0 step 5: soak. Clone, boot in job mode, run a short command as the
# runner account, stop and delete, N times in a row; watch for host kernel
# panics (plan §9, openai/tart#1308) and for leaked VMs or Softnet processes.
# Runs as slate-ci.
#
#   phase0-soak-test.sh [cycles]        default 30
set -uo pipefail
export PATH=/usr/local/libexec/slate-runner/bin:/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin
cd "$HOME" || exit 1

BASE="${BASE:-slate-mac-warm}"
cycles="${1:-30}"
VM="soak-$$"
start_epoch=$(date +%s)
marker="$HOME/.slate-runner/soak.started"
touch "$marker"

count_panics() { find /Library/Logs/DiagnosticReports -name '*.panic' 2>/dev/null | wc -l | tr -d ' '; }
panics_before="$(count_panics)"

# shellcheck disable=SC2329,SC2317  # invoked through the EXIT trap
cleanup() { tart stop "$VM" >/dev/null 2>&1 || true; sleep 1; tart delete "$VM" >/dev/null 2>&1 || true; }
trap cleanup EXIT

fails=0
printf '%-6s %-7s %-7s %-7s %s\n' cycle clone_s boot_s total_s result
for n in $(seq 1 "$cycles"); do
  t0=$(date +%s)
  if ! tart clone "$BASE" "$VM" >/dev/null 2>&1; then
    printf '%-6s %s\n' "$n" "clone failed"; fails=$((fails + 1)); continue
  fi
  t1=$(date +%s)
  tart run "$VM" --no-graphics --net-softnet-block=@host --root-disk-opts="sync=none" >"$HOME/.slate-runner/soak.log" 2>&1 &
  run_pid=$!
  ok=0
  for _ in $(seq 1 150); do
    sleep 1
    kill -0 "$run_pid" 2>/dev/null || break
    tart exec "$VM" true >/dev/null 2>&1 && { ok=1; break; }
  done
  t2=$(date +%s)
  result=ok
  if [ "$ok" != 1 ]; then
    result="no-exec"; fails=$((fails + 1))
  else
    who="$(tart exec "$VM" id -un 2>/dev/null | tr -d '\r')"
    if [ "$who" != runner ]; then result="exec-as-$who"; fails=$((fails + 1)); fi
  fi
  tart stop "$VM" >/dev/null 2>&1 || true
  wait "$run_pid" 2>/dev/null
  if ! tart delete "$VM" >/dev/null 2>&1; then result="$result,delete-failed"; fails=$((fails + 1)); fi
  t3=$(date +%s)
  printf '%-6s %-7s %-7s %-7s %s\n' "$n" "$((t1 - t0))" "$((t2 - t1))" "$((t3 - t0))" "$result"
done

echo
echo "leaked VMs:         $(tart list 2>/dev/null | awk 'NR>1 && $2 ~ /^soak-/ {print $2}' | tr '\n' ' ')"
echo "softnet processes:  $(pgrep -f /slate-runner/bin/softnet | wc -l | tr -d ' ')"
panics_after="$(count_panics)"
new_panics="$(find /Library/Logs/DiagnosticReports -name '*.panic' -newer "$marker" 2>/dev/null | wc -l | tr -d ' ')"
echo "host panic reports: before $panics_before, after $panics_after, new since start $new_panics"
echo "elapsed: $(( $(date +%s) - start_epoch )) s, failures: $fails"
[ "$fails" -eq 0 ] && [ "$new_panics" = 0 ]
