#!/usr/bin/env bash
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Fails unless every tracked .rs, .swift, or .cs file carries the
# repository's licence line, `// SPDX-License-Identifier: AGPL-3.0-or-later`,
# in its first 20 lines, and no other SPDX line there. Another identifier
# fails, whether instead of the repository's or beside it, unless
# `licence_for` below names that file (#1618): an identifier is the owner's
# call, so a stray one never passes. Wired into
# .github/workflows/license-headers.yml and `make ci`; see issue #266.
#
# To fix locally:  python3 scripts/apply-license-header.py

set -euo pipefail

cd "$(git rev-parse --show-toplevel)"

# Sets `licence` to the identifier a tracked file must carry. A file
# genuinely under another licence gets its own arm here, with the reason;
# none is today. (A case arm, not an associative array: `make ci` also runs
# under macOS's bash 3.2.)
licence_for() {
    case "$1" in
        *) licence='AGPL-3.0-or-later' ;;
    esac
}

total=0
missing=()
mismatched=()
# NUL-separated and unquoted, so a path with spaces or non-ASCII characters
# arrives as itself.
while IFS= read -r -d '' path; do
    total=$((total + 1))
    if [[ ! -r "$path" ]]; then
        missing+=("$path (unreadable)")
        continue
    fi
    licence_for "$path"
    # Read the first 20 lines in bash itself: no process per file, which
    # keeps the check quick under Git Bash on Windows.
    exact=no
    other=no
    count=0
    line=''
    while (( count < 20 )) && { IFS= read -r line || [[ -n "$line" ]]; }; do
        count=$((count + 1))
        # A UTF-8 byte-order mark is not part of the first line's text.
        if (( count == 1 )); then
            line="${line#$'\xEF\xBB\xBF'}"
        fi
        # The whole line, compared literally, so `AGPL-3.0-or-later-foo`
        # never passes; trailing white space (a CRLF file's \r) is dropped.
        line="${line%"${line##*[![:space:]]}"}"
        if [[ "$line" == "// SPDX-License-Identifier: $licence" ]]; then
            exact=yes
        elif [[ "$line" == *SPDX-License-Identifier* ]]; then
            other=yes
        fi
        line=''
    done < "$path"
    if [[ $exact == yes && $other == no ]]; then
        continue
    fi
    if [[ $exact == no && $other == no ]]; then
        missing+=("$path")
    else
        mismatched+=("$path")
    fi
done < <(git -c core.quotePath=false ls-files -z -- '*.rs' '*.swift' '*.cs')

status=0
if [[ ${#missing[@]} -gt 0 ]]; then
    printf 'Missing SPDX-License-Identifier header in %d file(s):\n' "${#missing[@]}" >&2
    printf '  %s\n' "${missing[@]}" >&2
    printf '\nRun: python3 scripts/apply-license-header.py\n' >&2
    status=1
fi
if [[ ${#mismatched[@]} -gt 0 ]]; then
    printf 'No exact repository licence line, or another SPDX line beside it, in %d file(s):\n' \
        "${#mismatched[@]}" >&2
    printf '  %s\n' "${mismatched[@]}" >&2
    printf '\nThe first 20 lines must hold "// SPDX-License-Identifier: AGPL-3.0-or-later"\n' >&2
    printf 'and no other SPDX line; a file genuinely under another licence gets an arm\n' >&2
    printf 'in licence_for (scripts/check-license-headers.sh).\n' >&2
    status=1
fi
if [[ $status -ne 0 ]]; then
    exit "$status"
fi

printf 'OK: all %d tracked .rs/.swift/.cs files carry the repository licence header.\n' "$total"
