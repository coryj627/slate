#!/usr/bin/env bash
# Build and run the Swift smoke-test CLI for yana-uniffi.
#
# Validates Rust -> uniffi-rs -> Swift end-to-end on Mac.
# Run from anywhere; cd's to the workspace root.

set -euo pipefail

WORKSPACE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$WORKSPACE_ROOT"

PROFILE="${PROFILE:-debug}"
CARGO_PROFILE_FLAG=""
if [[ "$PROFILE" == "release" ]]; then
    CARGO_PROFILE_FLAG="--release"
fi

TARGET_DIR="target/$PROFILE"
GENERATED_DIR="target/generated/swift"
SWIFT_OUT_DIR="target/swift-cli"
SWIFT_OUT="$SWIFT_OUT_DIR/yana-swift-cli"

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

echo "==> Compiling Swift CLI"
mkdir -p "$SWIFT_OUT_DIR"
swiftc \
    -emit-executable \
    -o "$SWIFT_OUT" \
    -L "$TARGET_DIR" \
    -lyana_uniffi \
    -I "$GENERATED_DIR" \
    -import-objc-header "$GENERATED_DIR/yana_uniffiFFI.h" \
    "$GENERATED_DIR/yana_uniffi.swift" \
    examples/swift-cli/main.swift

echo "==> Running yana-swift-cli"
DYLD_LIBRARY_PATH="$WORKSPACE_ROOT/$TARGET_DIR" "$SWIFT_OUT" "$@"
