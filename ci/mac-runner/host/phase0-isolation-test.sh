#!/bin/bash
# Phase 0 step 2 of docs/plans/42_self_hosted_mac_runner_plan.md.
#
# Runs as slate-ci. Boots a throwaway Linux VM the way jobs will boot, with
# Softnet isolation and the host blocked, then checks from inside the guest
# that the internet works and that the host, the LAN and IPv6 do not. Prints
# a PASS/FAIL line per probe and exits non-zero if any probe fails.
#
# Usage, from the owner's account:
#   sudo -n -u slate-ci -H /path/to/phase0-isolation-test.sh [lan-ip ...]
# Extra arguments are more addresses the guest must NOT reach, for example
# the router and another machine on the LAN.
#
# Requires: the Ubuntu image pulled as slate-ci (tart pull
# ghcr.io/cirruslabs/ubuntu:latest), and slate-ci's keychain password in
# ~/.slate-runner/keychain-password.

set -uo pipefail
export PATH=/usr/local/libexec/slate-runner/bin:/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin
export HOME="${HOME:-/Users/slate-ci}"

IMAGE="ghcr.io/cirruslabs/ubuntu:latest"
VM="phase0-isolation-$$"
BLOCK="${SOFTNET_BLOCK:-@host}"
fails=0

say()  { printf '%s\n' "$*"; }
pass() { say "PASS  $*"; }
fail() { say "FAIL  $*"; fails=$((fails + 1)); }

# shellcheck disable=SC2329,SC2317  # invoked through the EXIT trap
cleanup() {
  tart stop "$VM" >/dev/null 2>&1 || true
  sleep 1
  tart delete "$VM" >/dev/null 2>&1 || true
}
trap cleanup EXIT

say "== context: $(id -un) uid=$(id -u) session=$(launchctl managername 2>/dev/null || echo unknown)"
say "== tart $(tart --version), softnet at $(command -v softnet)"

# A Linux guest boots without an unlocked keychain; macOS guests may need one
# (Tart FAQ, macOS 15+). Report, but do not fail on it here.
if security unlock-keychain -p "$(cat "$HOME/.slate-runner/keychain-password")" login.keychain 2>/dev/null; then
  pass "keychain unlocked"
else
  say "NOTE  login keychain did not unlock (open item for the macOS guest)"
fi

if sudo -n /usr/local/libexec/slate-runner/bin/softnet --help >/dev/null 2>&1; then
  pass "softnet root rule works for $(id -un)"
else
  fail "softnet root rule"; exit 1
fi

tart clone "$IMAGE" "$VM" || { fail "clone $IMAGE"; exit 1; }
tart set "$VM" --cpu 2 --memory 2048

say "== booting with --net-softnet-block=$BLOCK"
tart run "$VM" --no-graphics --net-softnet-block="$BLOCK" --root-disk-opts="sync=none" \
  >"$HOME/.slate-runner/$VM.run.log" 2>&1 &
RUN_PID=$!

ip=""
for _ in $(seq 1 60); do
  sleep 2
  if ! kill -0 "$RUN_PID" 2>/dev/null; then
    fail "tart run exited early:"; sed 's/^/      /' "$HOME/.slate-runner/$VM.run.log"; exit 1
  fi
  ip="$(tart ip "$VM" 2>/dev/null)" && [ -n "$ip" ] && break
done
if [ -n "$ip" ]; then pass "guest booted, ip $ip"; else fail "no guest ip after 120 s"; exit 1; fi

# tart exec goes over vsock, not the network, so it must work even with the
# host blocked. Give the guest agent a moment to come up.
ok=0
for _ in $(seq 1 30); do
  sleep 2
  tart exec "$VM" true >/dev/null 2>&1 && { ok=1; break; }
done
if [ "$ok" = 1 ]; then
  pass "tart exec reaches the guest with the host blocked"
else
  fail "tart exec never answered"; exit 1
fi

gw="$(tart exec "$VM" sh -c "ip route | awk '/default/ {print \$3; exit}'" 2>/dev/null | tr -d '\r')"
say "== guest default gateway (the host): ${gw:-unknown}"

# Host to guest. Image builds boot with "out @host" so Packer can SSH in;
# jobs boot with "@host" and must not answer the host at all.
if nc -z -G 5 "$ip" 22 >/dev/null 2>&1; then host_reaches=0; else host_reaches=1; fi
case "$BLOCK" in
  out*) if [ $host_reaches -eq 0 ]; then pass "host reaches guest TCP 22 (build mode)"; else fail "host cannot reach guest TCP 22 in build mode"; fi ;;
  *)    if [ $host_reaches -ne 0 ]; then pass "host cannot reach guest TCP 22 (job mode)"; else fail "host reached guest TCP 22 in job mode"; fi ;;
esac

# Each probe: expect "yes" (must succeed) or "no" (must fail).
probe() {
  local expect="$1" label="$2" cmd="$3" out rc
  out="$(tart exec "$VM" sh -c "$cmd" 2>&1)"; rc=$?
  if [ "$expect" = yes ] && [ $rc -eq 0 ]; then pass "$label"
  elif [ "$expect" = no ] && [ $rc -ne 0 ]; then pass "$label"
  else fail "$label (exit $rc): $(printf '%s' "$out" | tail -1)"; fi
}

probe yes "internet by IP (https://1.1.1.1)"      "curl -sS -m 10 -o /dev/null https://1.1.1.1/"
probe yes "DNS via a public resolver (1.1.1.1)"   "getent hosts github.com >/dev/null 2>&1 || nslookup github.com 1.1.1.1 >/dev/null 2>&1 || dig @1.1.1.1 github.com +short | grep -q ."
probe no  "DNS via the gateway (host) is blocked" "nslookup github.com ${gw:-0.0.0.1} >/dev/null 2>&1"
probe no  "host gateway TCP 22"                   "timeout 5 sh -c 'exec 3<>/dev/tcp/${gw:-0.0.0.1}/22'"
probe no  "host gateway ICMP"                      "ping -c1 -W2 ${gw:-0.0.0.1}"
for lan in "$@"; do
  probe no "LAN address $lan TCP 80"             "timeout 5 sh -c 'exec 3<>/dev/tcp/$lan/80'"
  probe no "LAN address $lan ICMP"               "ping -c1 -W2 $lan"
done
probe no  "RFC1918 192.168.0.1"                   "timeout 5 sh -c 'exec 3<>/dev/tcp/192.168.0.1/80' || ping -c1 -W2 192.168.0.1"
probe no  "RFC1918 10.0.0.1"                      "ping -c1 -W2 10.0.0.1"
probe no  "link-local 169.254.169.254 (metadata)" "timeout 5 sh -c 'exec 3<>/dev/tcp/169.254.169.254/80'"
probe no  "IPv6 to the internet (2606:4700:4700::1111)" "ping6 -c1 -W2 2606:4700:4700::1111"
probe no  "IPv6 global address assigned"          "ip -6 addr show scope global | grep -q inet6"

say "== softnet processes on the host:"
pgrep -fl softnet | sed 's/^/      /' || say "      (none)"

say
if [ "$fails" -eq 0 ]; then say "RESULT: all probes passed"; else say "RESULT: $fails probe(s) failed"; fi
exit "$fails"
