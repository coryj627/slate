#!/usr/bin/env bash
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Fails if any tracked .rs, .swift, or .cs file is missing the
# `SPDX-License-Identifier` line in its first 20 lines. Wired into
# .github/workflows/license-headers.yml and `make ci`; see issue #266.
#
# To fix locally:  python3 scripts/apply-license-header.py

set -euo pipefail

cd "$(git rev-parse --show-toplevel)"

missing=()
while IFS= read -r path; do
    if ! head -n 20 -- "$path" | grep -qF 'SPDX-License-Identifier'; then
        missing+=("$path")
    fi
done < <(git ls-files '*.rs' '*.swift' '*.cs')

if [[ ${#missing[@]} -gt 0 ]]; then
    printf 'Missing SPDX-License-Identifier header in %d file(s):\n' "${#missing[@]}" >&2
    printf '  %s\n' "${missing[@]}" >&2
    printf '\nRun: python3 scripts/apply-license-header.py\n' >&2
    exit 1
fi

printf 'OK: all %d tracked .rs/.swift/.cs files carry an SPDX-License-Identifier header.\n' \
    "$(git ls-files '*.rs' '*.swift' '*.cs' | wc -l | tr -d ' ')"
