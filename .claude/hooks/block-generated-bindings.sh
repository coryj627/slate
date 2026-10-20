#!/usr/bin/env bash
# PreToolUse: refuse edits to the FFI bindings that the build scripts
# regenerate from the built slate-uniffi library.
set -euo pipefail

path=$(jq -r '.tool_input.file_path // empty')

case "$path" in
  *apps/slate-mac/Sources/SlateMac/slate_uniffi.swift | \
  *apps/slate-mac/Sources/slate_uniffiFFI/slate_uniffiFFI.h | \
  *apps/slate-windows/src/SlateUniffi/generated/*)
    echo "$path is generated. Change the API in crates/slate-uniffi, then run make regenerate-bindings (Swift) or make regenerate-bindings-windows (C#)." >&2
    exit 2
    ;;
esac
