#!/usr/bin/env bash
# Build the Mac app in RELEASE, wrap it in a launchable .app bundle,
# refresh a dated double-click launcher symlink at the repo root, and
# open it. The one-command manual-testing entrypoint (e.g. the
# Milestone T accessibility smoke pass): after this, no terminal is
# needed to relaunch — double-click the dated slate-mac-<date>.app.
#
# Why release + bundle: the AX bridge (VoiceOver) only works from a
# LaunchServices-registered .app bundle, and release gives performance
# representative of a shipped build (matters for the large-canvas
# responsiveness checks). The a11y-check dev gate is skipped here —
# this is a launch-to-test path, not CI; run `make mac-app` for that.
#
# Usage:
#   ./scripts/build-and-launch.sh            # build release + launch
#   ./scripts/build-and-launch.sh --no-open  # build + relink, don't launch
#   any other args pass through to build-mac-app.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

OPEN=1
BUILD_ARGS=()
for arg in "$@"; do
    case "$arg" in
        --no-open) OPEN=0 ;;
        *) BUILD_ARGS+=("$arg") ;;
    esac
done

# Release build + .app bundle; a11y gate skipped (see header).
# `${arr[@]+"${arr[@]}"}` expands to nothing (not an unbound-variable
# error) for an empty array under `set -u` on bash 3.2 (macOS default).
PROFILE=release ./scripts/build-mac-app.sh --bundle --skip-a11y-check \
    ${BUILD_ARGS[@]+"${BUILD_ARGS[@]}"}

BUNDLE="apps/slate-mac/.build/release/SlateMac.app"
if [[ ! -x "$BUNDLE/Contents/MacOS/SlateMac" ]]; then
    echo "error: expected release bundle not found at $BUNDLE" >&2
    exit 1
fi

# Refresh the dated launcher symlink: drop any prior ones so only the
# newest remains, then link the current build with a build-time stamp.
# (.gitignore covers /slate-mac-*.app so these never show in git status.)
rm -f slate-mac-*.app
LINK="slate-mac-$(date +%Y-%m-%d-%H-%M).app"
ln -sfn "$BUNDLE" "$LINK"
echo "==> Launcher ready: $ROOT/$LINK -> $BUNDLE"

if [[ "$OPEN" == "1" ]]; then
    echo "==> Launching (double-click $LINK to relaunch later)"
    open "$LINK"
fi
