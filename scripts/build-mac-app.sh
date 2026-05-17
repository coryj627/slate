#!/usr/bin/env bash
# Build and optionally run the YanaMac SwiftUI smoke-test app.
#
# Pipeline:
#   1. cargo build -p yana-uniffi
#   2. uniffi-bindgen generate (Swift)
#   3. copy generated headers + Swift into apps/yana-mac/Sources/
#   4. swift build inside apps/yana-mac/
#   5. (optional, with --run) launch the resulting binary

set -euo pipefail

WORKSPACE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$WORKSPACE_ROOT"

PROFILE="${PROFILE:-debug}"
CARGO_PROFILE_FLAG=""
if [[ "$PROFILE" == "release" ]]; then
    CARGO_PROFILE_FLAG="--release"
fi

RUN=0
SKIP_A11Y_CHECK=0
for arg in "$@"; do
    case "$arg" in
        --run) RUN=1 ;;
        --skip-a11y-check) SKIP_A11Y_CHECK=1 ;;
        --help|-h)
            echo "usage: $0 [--run] [--skip-a11y-check]"
            echo "  --run               Launch the app after building."
            echo "  --skip-a11y-check   Don't run a11y-check after swift build."
            echo "                      Useful for CI jobs that only care about"
            echo "                      build/test pass and leave a11y to its"
            echo "                      own workflow."
            exit 0
            ;;
    esac
done

TARGET_DIR="target/$PROFILE"
GENERATED_DIR="target/generated/swift"
APP_DIR="apps/yana-mac"

# Source rustup environment if cargo isn't already on PATH.
if ! command -v cargo >/dev/null 2>&1; then
    if [[ -f "$HOME/.cargo/env" ]]; then
        # shellcheck disable=SC1091
        source "$HOME/.cargo/env"
    fi
fi

echo "==> Building yana-uniffi ($PROFILE)"
cargo build -p yana-uniffi $CARGO_PROFILE_FLAG

echo "==> Generating Swift bindings"
mkdir -p "$GENERATED_DIR"
cargo run -p yana-uniffi $CARGO_PROFILE_FLAG --bin uniffi-bindgen -- \
    generate \
    --library "$TARGET_DIR/libyana_uniffi.dylib" \
    --language swift \
    --out-dir "$GENERATED_DIR"

echo "==> Staging generated bindings into $APP_DIR"
cp "$GENERATED_DIR/yana_uniffi.swift"  "$APP_DIR/Sources/YanaMac/yana_uniffi.swift"
cp "$GENERATED_DIR/yana_uniffiFFI.h"   "$APP_DIR/Sources/yana_uniffiFFI/yana_uniffiFFI.h"

echo "==> swift build ($APP_DIR)"
cd "$APP_DIR"
swift build

BINARY=".build/$PROFILE/YanaMac"
ABS_BINARY="$WORKSPACE_ROOT/$APP_DIR/$BINARY"

echo
echo "Built: $ABS_BINARY"
echo "Run with:"
echo "  DYLD_LIBRARY_PATH=\"$WORKSPACE_ROOT/$TARGET_DIR\" $ABS_BINARY"

# Best-effort SwiftUI accessibility check using
# cvs-health/ios-swiftui-accessibility-techniques' a11y-check. CI runs
# the canonical version of this; locally we surface findings inline so
# you don't have to push to hear about a regression. CI jobs that only
# care about build/test (e.g. the Swift tests workflow) pass
# `--skip-a11y-check` to avoid conflating an a11y regression with a
# build/test failure — a11y has its own dedicated workflow.
cd "$WORKSPACE_ROOT"
if [[ "$SKIP_A11Y_CHECK" == "1" ]]; then
    echo
    echo "==> Skipping a11y-check (--skip-a11y-check)."
elif command -v a11y-check >/dev/null 2>&1; then
    echo
    echo "==> Running a11y-check on apps/yana-mac/Sources/YanaMac"
    if ! a11y-check apps/yana-mac/Sources/YanaMac --only error; then
        echo
        echo "a11y-check reported errors above. CI will fail until they're fixed." >&2
        exit 1
    fi
else
    echo
    echo "==> Skipping a11y-check (not installed)."
    echo "    Install with:"
    echo "      brew tap cvs-health/ios-swiftui-accessibility-techniques \\"
    echo "        https://github.com/cvs-health/ios-swiftui-accessibility-techniques.git"
    echo "      brew install --HEAD cvs-health/ios-swiftui-accessibility-techniques/a11y-check"
fi

if [[ "$RUN" == "1" ]]; then
    echo
    echo "==> Launching YanaMac"
    DYLD_LIBRARY_PATH="$WORKSPACE_ROOT/$TARGET_DIR" "$ABS_BINARY"
fi
