#!/usr/bin/env python3
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
"""Diff two §W-A parity-artifact directories byte-for-byte.

The §W-A skeleton's diff step (w0_spec §W0-3 item 5): compares every
artifact in two directories produced by the platform harnesses (the
Windows ``ParityHarness`` tool / the mac ``ParityHarnessTests`` twin) or
against the committed goldens. Exit 0 = byte-identical; exit 1 = any
missing, extra, or differing artifact, each listed.

Normalization list (must stay exhaustive per §W-A — W8-4 owns growing
it): none. Path separators are already normalized to forward slashes by
the serializers, and line endings are deliberately compared byte-exact
(program decision 9).

Usage: python3 scripts/diff-parity-artifacts.py <dir-a> <dir-b>
"""

from __future__ import annotations

import sys
from pathlib import Path


def main() -> int:
    if len(sys.argv) != 3:
        print(__doc__, file=sys.stderr)
        return 2
    a, b = Path(sys.argv[1]), Path(sys.argv[2])
    for d in (a, b):
        if not d.is_dir():
            print(f"not a directory: {d}", file=sys.stderr)
            return 2

    names_a = {p.name for p in a.glob("*.json")}
    names_b = {p.name for p in b.glob("*.json")}
    failures = []
    for name in sorted(names_a - names_b):
        failures.append(f"only in {a}: {name}")
    for name in sorted(names_b - names_a):
        failures.append(f"only in {b}: {name}")
    for name in sorted(names_a & names_b):
        bytes_a = (a / name).read_bytes()
        bytes_b = (b / name).read_bytes()
        if bytes_a != bytes_b:
            offset = next(
                (i for i, (x, y) in enumerate(zip(bytes_a, bytes_b)) if x != y),
                min(len(bytes_a), len(bytes_b)),
            )
            failures.append(
                f"differs: {name} (first divergence at byte {offset}; "
                f"{len(bytes_a)} vs {len(bytes_b)} bytes)"
            )

    if failures:
        print(f"parity diff FAILED ({len(failures)} problem(s)):")
        for f in failures:
            print(f"  {f}")
        return 1
    print(f"parity diff OK: {len(names_a)} artifacts byte-identical")
    return 0


if __name__ == "__main__":
    sys.exit(main())
