#!/usr/bin/env bash
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Run one SlateWindows.Tests class (or one fact) N times under Wine, each
# iteration a fresh Windows process: an isolated run. Prints one line per
# run and a TOTAL line; exits non-zero when any run failed.
#
# Usage: run-loop.sh <count> <logdir> <class full name> [method name]
# Needs an Xvfb display (DISPLAY, default :77) and an initialized
# WINEPREFIX (default ~/.wine-slate). See README.md.

set -u
here="$(cd "$(dirname "$0")" && pwd)"
exe="$here/bin/Release/net10.0-windows/win-x64/WineTestRunner.exe"
export WINEPREFIX="${WINEPREFIX:-$HOME/.wine-slate}"
export DISPLAY="${DISPLAY:-:77}"
export WINEDEBUG=-all
# mshtml off skips the Gecko prompt; mscoree must stay ON — Wine's loader
# needs it to map IL-only assemblies such as System.Runtime.dll.
export WINEDLLOVERRIDES="mshtml="

count="$1"; logdir="$2"; class="$3"; method="${4:-}"
[ -f "$exe" ] || { echo "not built: $exe" >&2; exit 2; }
mkdir -p "$logdir"
pass=0; fail=0
for i in $(seq 1 "$count"); do
  log="$logdir/run-$i.log"
  start=$(date +%s)
  timeout 900 wine "$exe" "$class" $method > "$log" 2>&1
  rc=$?
  if [ "$rc" -eq 0 ]; then pass=$((pass + 1)); else fail=$((fail + 1)); fi
  echo "run $i rc=$rc $(grep -a RESULT "$log") $(( $(date +%s) - start ))s $(grep -a '^FAIL' "$log" | cut -c1-160 | tr '\n' ' ')"
done
echo "TOTAL pass=$pass fail=$fail of $count"
[ "$fail" -eq 0 ]
