#!/bin/bash
# runnerctl: the owner's handle on the mac runner controller. Installed
# root-owned as /usr/local/libexec/slate-runner/bin/runnerctl and run as
# slate-ci (ci/mac-runner/host/runnerctl wraps the sudo). Plan §4.1, §5.4.
#
#   runnerctl status            flags, heartbeat age, VMs, last log lines
#   runnerctl pause             finish the current job, boot no new VM, clear the heartbeat
#   runnerctl resume            back to normal
#   runnerctl recycle           replace the idle VM (after an image rebuild)
#   runnerctl clear-panic       reset the panic circuit breaker after the owner has looked
#   runnerctl rebuild-warm      rebuild slate-mac-warm from main now
#   runnerctl set-token         read a GitHub token from stdin into the token file (never printed)
#   runnerctl logs [n]          last n controller log lines (default 50)
set -euo pipefail
export PATH=/usr/local/libexec/slate-runner/bin:/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin
state="$HOME/.slate-runner"
flags="$state/state"
mkdir -p "$flags"

case "${1:-status}" in
  status)
    echo "user:       $(id -un)"
    for f in paused tripped recycle; do
      if [ -e "$flags/$f" ]; then printf '%-11s yes %s\n' "$f:" "$(head -c 200 "$flags/$f" | tr '\n' ' ')"; else printf '%-11s no\n' "$f:"; fi
    done
    if [ -e "$flags/last-clean-job" ]; then
      echo "last clean job: $(tr -d '\n' < "$flags/last-clean-job") at $(date -r "$flags/last-clean-job" '+%Y-%m-%d %H:%M:%S')"
    else
      echo "last clean job: none yet"
    fi
    echo "token file: $([ -s "$state/github-token" ] && echo present || echo MISSING)"
    echo "softnet:    $(sudo -n /usr/local/libexec/slate-runner/bin/softnet --version 2>/dev/null || echo 'root rule FAILED')"
    echo "free disk:  $(df -g / | awk 'NR==2 {print $4 " GB"}')"
    echo "controller: $(pgrep -fl 'controller.py' >/dev/null && echo running || echo not running)"
    echo "vms:"; tart list 2>/dev/null | awk 'NR==1 || /slate-mac|job-/' | sed 's/^/  /'
    echo "log tail:"; tail -n 8 "$state/logs/controller.log" 2>/dev/null | sed 's/^/  /' || true
    ;;
  pause)      date -u +%Y-%m-%dT%H:%M:%SZ > "$flags/paused"; echo "paused: the current job finishes; no new VM boots; heartbeat clears" ;;
  resume)     rm -f "$flags/paused"; echo "resumed" ;;
  recycle)    touch "$flags/recycle"; echo "the idle VM will be replaced" ;;
  clear-panic) rm -f "$flags/tripped"; touch "$flags/last-clean-job"; echo "panic breaker cleared" ;;
  rebuild-warm)
    tree="${SLATE_RUNNER_TREE:-$state/mac-runner}"
    exec bash "$tree/image/build-warm.sh"
    ;;
  set-token)
    umask 077
    tmp="$(mktemp "$state/.token.XXXXXX")"
    tr -d '\r\n' > "$tmp"
    if [ ! -s "$tmp" ]; then rm -f "$tmp"; echo "no token on stdin" >&2; exit 1; fi
    mv "$tmp" "$state/github-token"
    echo "token stored ($(wc -c < "$state/github-token" | tr -d ' ') bytes)"
    ;;
  logs)       tail -n "${2:-50}" "$state/logs/controller.log" 2>/dev/null || echo "no log yet" ;;
  *)          sed -n '2,15p' "$0" | sed 's/^# \{0,1\}//'; exit 2 ;;
esac
