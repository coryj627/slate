#!/bin/zsh
# VoiceOver test driver for Slate.
#
# Model: the KEYBOARD drives the app (as a screen-reader user works);
# VoiceOver OBSERVES — assertions read what VO actually spoke, via the
# AppleScript dictionary (`last phrase`, `text under cursor`).
#
# Companion runbook (preconditions, gotchas, per-feature test paths):
#   docs/runbooks/voiceover-feature-test.md
#
# Usage: scripts/vo.sh <cmd> [args]
#
#   start-vo              Start VoiceOver (open -a, Cmd+F5 fallback)
#   stop-vo               Quit VoiceOver
#   ping                  Prove AppleScript control is live (errors -1708 if not)
#   last                  Print VO's last spoken phrase
#   under                 Print text under the VO cursor
#   vo-move <dir> [n]     Move VO cursor right|left|up|down n times, print landing
#   vo-into / vo-out      Interact into / out of the current item
#   vo-act                Press the item under the VO cursor (VO+Space)
#   activate              Bring the app frontmost (SLATE_APP, default SlateMac)
#   keys "<char>" [mods]  Send keystroke with cmd/shift/opt/ctrl modifiers
#   key <code> [mods]     Send key code (36=Return 48=Tab 53=Esc 49=Space
#                         125=Down 126=Up 123=Left 124=Right 115=Home 119=End)
#   wait-phrase <substr> [timeout-s]   Poll last phrase until substring match

set -u
CMD="${1:-help}"; shift 2>/dev/null || true
APP="${SLATE_APP:-SlateMac}"

osa() { osascript -e "$1" 2>&1; }

case "$CMD" in
  start-vo)
    pgrep -x VoiceOver >/dev/null && { echo "already-running"; exit 0; }
    open -a VoiceOver 2>/dev/null
    for i in {1..10}; do pgrep -x VoiceOver >/dev/null && { sleep 2; echo "started"; exit 0; }; sleep 0.5; done
    osa 'tell application "System Events" to key code 96 using {command down}'
    for i in {1..10}; do pgrep -x VoiceOver >/dev/null && { sleep 2; echo "started-kbd"; exit 0; }; sleep 0.5; done
    echo "FAILED-to-start"; exit 1 ;;
  stop-vo)
    osa 'tell application "VoiceOver" to quit' >/dev/null; sleep 1
    pgrep -x VoiceOver >/dev/null && { pkill -x VoiceOver; sleep 1; }
    pgrep -x VoiceOver >/dev/null && echo "still-running" || echo "stopped" ;;
  ping)
    osa 'tell application "VoiceOver" to output "ping"' ;;
  last)
    osa 'tell application "VoiceOver" to get content of last phrase' ;;
  under)
    R=$(osa 'tell application "VoiceOver" to get text under cursor of vo cursor')
    [[ "$R" == *error* ]] && R=$(osa 'tell application "VoiceOver" to get content of vo cursor')
    echo "$R" ;;
  vo-move) # vo-move right|left|up|down [n]
    D="${1:-right}"; N="${2:-1}"
    for i in $(seq 1 $N); do osa "tell application \"VoiceOver\" to tell vo cursor to move $D" >/dev/null; sleep 0.35; done
    osa 'tell application "VoiceOver" to get text under cursor of vo cursor' ;;
  vo-into) osa 'tell application "VoiceOver" to tell vo cursor to move into item'; sleep 0.4; osa 'tell application "VoiceOver" to get text under cursor of vo cursor' ;;
  vo-out) osa 'tell application "VoiceOver" to tell vo cursor to move out of item'; sleep 0.4; osa 'tell application "VoiceOver" to get text under cursor of vo cursor' ;;
  vo-act) osa 'tell application "VoiceOver" to tell vo cursor to perform action' ;;
  vo-first) osa 'tell application "VoiceOver" to tell vo cursor to move to first item'; sleep 0.4; osa 'tell application "VoiceOver" to get text under cursor of vo cursor' ;;
  activate)
    osa "tell application \"$APP\" to activate" >/dev/null
    sleep 0.5; osa 'tell application "System Events" to get name of first application process whose frontmost is true' ;;
  keys) # keys "<keystroke>" [modifiers: cmd shift opt ctrl]
    K="$1"; shift 2>/dev/null || true
    MODS=""
    for m in "$@"; do case $m in
      cmd) MODS="$MODS command down," ;; shift) MODS="$MODS shift down," ;;
      opt) MODS="$MODS option down," ;; ctrl) MODS="$MODS control down," ;;
    esac; done
    MODS="${MODS%,}"
    if [[ -n "$MODS" ]]; then
      osa "tell application \"System Events\" to keystroke \"$K\" using {$MODS}"
    else
      osa "tell application \"System Events\" to keystroke \"$K\""
    fi ;;
  key) # key <code> [modifiers]
    C="$1"; shift 2>/dev/null || true
    MODS=""
    for m in "$@"; do case $m in
      cmd) MODS="$MODS command down," ;; shift) MODS="$MODS shift down," ;;
      opt) MODS="$MODS option down," ;; ctrl) MODS="$MODS control down," ;;
    esac; done
    MODS="${MODS%,}"
    if [[ -n "$MODS" ]]; then
      osa "tell application \"System Events\" to key code $C using {$MODS}"
    else
      osa "tell application \"System Events\" to key code $C"
    fi ;;
  wait-phrase) # wait-phrase <substring> [timeout-s]
    WANT="$1"; T="${2:-5}"; END=$((SECONDS + T)); P=""
    while (( SECONDS < END )); do
      P=$(osa 'tell application "VoiceOver" to get content of last phrase')
      [[ "$P" == *"$WANT"* ]] && { echo "MATCH: $P"; exit 0; }
      sleep 0.4
    done
    echo "TIMEOUT lastPhrase: $P"; exit 1 ;;
  *) echo "cmds: start-vo stop-vo ping last under vo-move vo-into vo-out vo-act vo-first activate keys key wait-phrase"; echo "see docs/runbooks/voiceover-feature-test.md" ;;
esac
