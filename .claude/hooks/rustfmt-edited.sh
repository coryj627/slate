#!/usr/bin/env bash
# PostToolUse: format each Rust file Claude edits, so make fmt-check never
# fails on an agent edit. The edition matches the workspace Cargo.toml.
set -euo pipefail

path=$(jq -r '.tool_input.file_path // empty')
[[ "$path" == *.rs && -f "$path" ]] || exit 0

rustfmt --edition 2024 "$path" || { echo "rustfmt failed on $path" >&2; exit 2; }
