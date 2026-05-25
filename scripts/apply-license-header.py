#!/usr/bin/env python3
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
"""Insert the AGPL-3.0-or-later boilerplate header into first-party source files.

Scope: tracked .rs and .swift files (per ``git ls-files``). Idempotent: files
that already carry an SPDX-License-Identifier line in their first 20 lines are
left untouched. Run from the repository root; exits non-zero if any file could
not be written. See issue #266 for the rationale.

Special case: ``Package.swift`` keeps its ``// swift-tools-version`` directive
on line 1 — the SPDX header is inserted on the lines immediately below it.
"""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

HEADER_LINES = (
    "// Copyright (C) 2026 Cory Joseph",
    "// SPDX-License-Identifier: AGPL-3.0-or-later",
)
SCAN_LINES = 20  # how many lines to scan for an existing SPDX marker
SPDX_MARKER = "SPDX-License-Identifier"
SWIFT_TOOLS_PREFIX = "// swift-tools-version"


def tracked_source_files(repo_root: Path) -> list[Path]:
    """Return tracked .rs / .swift files relative to repo_root."""
    out = subprocess.check_output(
        ["git", "ls-files", "*.rs", "*.swift"],
        cwd=repo_root,
        text=True,
    )
    return [repo_root / line for line in out.splitlines() if line]


def already_headered(text: str) -> bool:
    head = text.splitlines()[:SCAN_LINES]
    return any(SPDX_MARKER in line for line in head)


def insert_header(text: str, path: Path) -> str:
    """Return text with the SPDX header inserted at the right offset."""
    lines = text.splitlines(keepends=True)
    eol = "\n"
    if lines and lines[0].endswith("\r\n"):
        eol = "\r\n"

    header_block = [line + eol for line in HEADER_LINES]

    # Package.swift: preserve the swift-tools-version directive on line 1.
    if path.name == "Package.swift" and lines and lines[0].startswith(SWIFT_TOOLS_PREFIX):
        return "".join(lines[:1] + header_block + [eol] + lines[1:])

    # Default: prepend header at the very top, separated by a blank line.
    return "".join(header_block + [eol] + lines)


def main() -> int:
    repo_root = Path(__file__).resolve().parent.parent
    files = tracked_source_files(repo_root)
    changed = 0
    skipped = 0
    for path in files:
        text = path.read_text(encoding="utf-8")
        if already_headered(text):
            skipped += 1
            continue
        path.write_text(insert_header(text, path), encoding="utf-8")
        changed += 1
    print(f"applied: {changed}, skipped (already headered): {skipped}, total: {len(files)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
