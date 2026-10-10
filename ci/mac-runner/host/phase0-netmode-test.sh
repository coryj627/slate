#!/bin/bash
# Phase 0: which Softnet mode lets the host reach the guest (for Packer's SSH
# during image builds) while the guest still cannot open connections to the
# host? Boots the Ubuntu test image three ways and reports. Runs as slate-ci.
# shellcheck disable=SC2016  # the single-quoted awk programs run inside the guest
set -uo pipefail
export PATH=/usr/local/libexec/slate-runner/bin:/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin
cd "$HOME" || exit 1
IMAGE="ghcr.io/cirruslabs/ubuntu:latest"
VM="nettest-$$"

cleanup() { tart stop "$VM" >/dev/null 2>&1 || true; sleep 1; tart delete "$VM" >/dev/null 2>&1 || true; }
trap cleanup EXIT

in_guest() { tart exec "$VM" sh -c "$1" 2>/dev/null | tr -d '\r'; }

try() {
  local label="$1"; shift
  cleanup
  tart clone "$IMAGE" "$VM" >/dev/null && tart set "$VM" --cpu 2 --memory 2048
  tart run "$VM" --no-graphics "$@" >"$HOME/.slate-runner/$VM.log" 2>&1 &
  local ip=""
  for _ in $(seq 1 45); do sleep 2; ip="$(tart ip "$VM" 2>/dev/null)" && [ -n "$ip" ] && break; done
  for _ in $(seq 1 20); do sleep 2; tart exec "$VM" true >/dev/null 2>&1 && break; done
  echo "== $label   guest ip $ip"
  echo "   listeners in guest: $(in_guest 'ss -ltn 2>/dev/null | awk "NR>1 {print \$4}" | tr "\n" " "')"
  echo "   sshd in guest:      $(in_guest 'pgrep -x sshd >/dev/null && echo running || echo not running')"
  if nc -z -G 5 "$ip" 22 2>/dev/null; then echo "   host -> guest:22    OPEN"; else echo "   host -> guest:22    closed or filtered"; fi
  local gw; gw="$(in_guest 'ip route | awk "/default/ {print \$3; exit}"')"
  echo "   guest -> host:22    $(in_guest "timeout 4 sh -c 'exec 3<>/dev/tcp/$gw/22' 2>/dev/null && echo reachable || echo blocked")"
  echo "   guest -> 1.1.1.1    $(in_guest 'curl -sS -m 8 -o /dev/null https://1.1.1.1/ && echo reachable || echo blocked')"
}

try "default NAT, no Softnet"
try "softnet: block out @host"                      --net-softnet-block="out @host"
try "softnet: block out @host, allow in @host"      --net-softnet-block="out @host" --net-softnet-allow="in @host"
