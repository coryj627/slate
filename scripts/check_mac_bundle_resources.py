#!/usr/bin/env python3
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Fail the mac build when SlateMac.app lacks a SwiftPM resource bundle.

The #1589 tripwire. `swift build` writes one `<Package>_<Target>.bundle`
folder beside the SlateMac executable for every package target with
resources (today MathJaxSwift's and LaTeXSwiftUI's). The `Bundle.module`
accessor SwiftPM generates for those targets looks for its bundle only in
the app's `Contents/Resources`, beside the executable's own bundle, and at
`Bundle.main.bundleURL`. Older SwiftPM accessors also tried the build
directory, so the wrapped .app used to find them there; the swift-build
backend's accessor does not. A bundle without them crashes on the first
math render: `Fatal error: unable to find bundle named
MathJaxSwift_MathJaxSwift`, raised on LaTeXSwiftUI's render thread.

Usage: check_mac_bundle_resources.py PRODUCTS_DIR APP_BUNDLE

PRODUCTS_DIR is the directory `swift build` wrote the executable to
(apps/slate-mac/.build/<profile>, a symlink is fine). APP_BUNDLE is the
SlateMac.app built from it. The check only reads.

Exit 0 when every `*.bundle` folder in PRODUCTS_DIR is in
APP_BUNDLE/Contents/Resources with every file and link it holds. Exit 1 when
one is missing or incomplete, and also when there is nothing to judge:
PRODUCTS_DIR is not a directory or holds no `*.bundle` folder, or
APP_BUNDLE has no Contents/Resources folder. Failing closed means a change
in where SwiftPM puts the bundles cannot switch the tripwire off silently.
Exit 2 when the two paths are not given.
"""

from __future__ import annotations

import os
from pathlib import Path
import sys
from typing import List, Optional, Set, TextIO

ISSUE = "https://github.com/coryj627/slate/issues/1589"

# How many of a partial copy's missing paths a message lists.
_LISTED = 5


class Unreadable(Exception):
    """There is nothing sound to judge; the check fails closed."""


def resource_bundles(products: Path) -> List[Path]:
    """Every `*.bundle` folder `swift build` left in the products directory."""
    if not products.is_dir():
        raise Unreadable(f"{products} is not a directory")
    found = sorted(p for p in products.iterdir() if p.name.endswith(".bundle") and p.is_dir())
    if not found:
        raise Unreadable(
            f"{products} holds no *.bundle folder, though SlateMac links LaTeXSwiftUI, whose resources always "
            "build one. SwiftPM may have moved its products"
        )
    return found


def resources_of(app: Path) -> Path:
    resources = app / "Contents" / "Resources"
    if not resources.is_dir():
        raise Unreadable(f"{app} has no Contents/Resources folder")
    return resources


def entries(root: Path) -> Set[str]:
    """The relative paths of every file and link under root (folders are implied)."""
    found: Set[str] = set()
    for folder, dirs, files in os.walk(root):
        base = Path(folder)
        for name in files:
            found.add((base / name).relative_to(root).as_posix())
        for name in dirs:
            if (base / name).is_symlink():
                found.add((base / name).relative_to(root).as_posix())
    return found


def problems(products: Path, app: Path) -> List[str]:
    """What the app bundle lacks; an empty list when it carries everything."""
    resources = resources_of(app)
    found: List[str] = []
    for bundle in resource_bundles(products):
        staged = resources / bundle.name
        if not staged.is_dir():
            what = "is a file, not a folder" if staged.exists() else "is missing"
            found.append(f"{bundle.name} {what} in {resources}")
            continue
        missing = sorted(entries(bundle) - entries(staged))
        if missing:
            listed = ", ".join(missing[:_LISTED]) + (f" and {len(missing) - _LISTED} more" if len(missing) > _LISTED else "")
            found.append(f"{bundle.name} in {resources} lacks {listed}")
    return found


def failed_message(app: str, found: List[str]) -> str:
    lines = "\n".join(f"  - {item}" for item in found)
    return (
        f"Resource bundle tripwire (#1589): {app} does not carry every SwiftPM resource bundle:\n"
        f"{lines}\n"
        "\n"
        "The Bundle.module accessor SwiftPM generates looks for its bundle in the app's Contents/Resources, "
        "not in the build directory. Without it, the first math render crashes the app with "
        "'Fatal error: unable to find bundle named MathJaxSwift_MathJaxSwift'. build-mac-app.sh's --bundle "
        f"step must copy every *.bundle folder beside the executable into Contents/Resources. See {ISSUE}"
    )


def unreadable_message(app: str, reason: str) -> str:
    return (
        f"Resource bundle tripwire (#1589): cannot judge {app}: {reason}.\n"
        "\n"
        "The tripwire fails closed, so that a change in where SwiftPM writes resource bundles cannot switch "
        "it off silently. Find where `swift build` now puts the <Package>_<Target>.bundle folders, then teach "
        "build-mac-app.sh and scripts/check_mac_bundle_resources.py the new place, with a fact in "
        f"scripts/tests/test_mac_bundle_resources.py. Why it matters: {ISSUE}"
    )


def check(
    products: str,
    app: str,
    out: TextIO = sys.stdout,
    err: TextIO = sys.stderr,
    annotate: Optional[bool] = None,
) -> int:
    """Judge the app bundle; return the exit status."""
    if annotate is None:
        annotate = os.environ.get("GITHUB_ACTIONS") == "true"
    try:
        found = problems(Path(products), Path(app))
    except Unreadable as problem:
        summary = f"cannot judge {app}: {problem}"
        message = unreadable_message(app, str(problem))
    else:
        if not found:
            names = ", ".join(b.name for b in resource_bundles(Path(products)))
            print(f"Resource bundle tripwire (#1589): {app} carries every SwiftPM resource bundle: {names}.", file=out)
            return 0
        summary = f"{app} lacks SwiftPM resource bundles ({'; '.join(found)}); the first math render would crash"
        message = failed_message(app, found)
    if annotate:
        print(f"::error title=Resource bundle tripwire (#1589)::{summary}", file=out)
    print(message, file=err)
    return 1


def main(argv: Optional[List[str]] = None) -> int:
    args = sys.argv[1:] if argv is None else argv
    if args and args[0] in ("-h", "--help"):
        print((__doc__ or "").strip())
        return 0
    if len(args) != 2:
        print("usage: check_mac_bundle_resources.py PRODUCTS_DIR APP_BUNDLE", file=sys.stderr)
        return 2
    return check(args[0], args[1])


if __name__ == "__main__":
    sys.exit(main())
