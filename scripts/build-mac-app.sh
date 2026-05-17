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
for arg in "$@"; do
    case "$arg" in
        --run) RUN=1 ;;
        --help|-h)
            echo "usage: $0 [--run]"
            echo "  --run   Launch the app after building (otherwise build only)."
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

if [[ "$RUN" == "1" ]]; then
    echo
    echo "==> Launching YanaMac"
    DYLD_LIBRARY_PATH="$WORKSPACE_ROOT/$TARGET_DIR" "$ABS_BINARY"
fi
