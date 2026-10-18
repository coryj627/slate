#!/usr/bin/env python3
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Fail the mac build when the SlateMac executable records macOS SDK 27 or later.

The #1460 tripwire. SwiftUI picks the reading view's selectable-text
implementation by the SDK the main executable records in its
LC_BUILD_VERSION load command:

* below SDK 27, an AppKit text field whose link runs are AppKit link elements
  (`AXLink`, `AXPress`). VoiceOver's press opens the URL with NSWorkspace,
  where the #1421 guard (`ReadingLinkSystemOpen`) hands it to
  `ReadingLinkRouter`;
* at SDK 27 or later, SwiftUI's own text, whose link elements carry no action
  at all: VO-Space on a reading-view link does nothing, silently.

No hosted test can notice the flip, because the XCTest runner (`xctest`)
already records SDK 27. So this reads the built executable itself: every
architecture slice of `otool -arch all -l <executable>`, and the `sdk` of its
macOS LC_BUILD_VERSION (or LC_VERSION_MIN_MACOSX) load command.

Usage: check_mac_sdk.py PATH [PATH ...]

PATH is an executable or an .app bundle; for a bundle the check reads the
executable its Info.plist names (CFBundleExecutable). It only reads: the
binary is never changed.

Exit 0 when every slice of every PATH records a macOS SDK below 27. Exit 1
when one records 27 or later, and also when the SDK cannot be read: otool
fails or names no file, a slice has no macOS build-version load command, the
command is for another platform, or its `sdk` does not parse. Failing closed
means a change in otool's output or in the binary's format cannot switch the
tripwire off silently. Exit 2 when no PATH is given.
"""

from __future__ import annotations

import os
from pathlib import Path
import plistlib
import re
import subprocess
import sys
from typing import Callable, Dict, List, NamedTuple, Optional, TextIO, Tuple

ISSUE = "https://github.com/coryj627/slate/issues/1460"

# The first SDK major at which SwiftUI renders the reading view's selectable
# text itself, so its links have no press (measured in #1460's spike: SDK 15
# and 26 keep AppKit's text field, SDK 27 does not).
SILENT_FROM_SDK = 27

# Apple's otool prints the platform as its number, cctools' vtool and LLVM's
# objdump as its name.
_MACOS = {"1", "macos"}

# `otool -arch all -l` names the file before its load commands: `path:` for a
# thin binary, `path (architecture arm64):` for each slice of a universal one.
_HEADER = re.compile(r"^.*?(?: \(architecture (?P<arch>[^)]+)\))?:$")
_FIELD = re.compile(r"^\s+(?P<key>[a-z_]+)\s+(?P<value>\S.*?)\s*$")
_VERSION = re.compile(r"^(?P<major>\d+)(?:\.\d+){0,2}$")

Runner = Callable[..., "subprocess.CompletedProcess[str]"]


class Unreadable(Exception):
    """The SDK the executable records cannot be read; the check fails closed."""


class Reading(NamedTuple):
    arch: str
    command: str
    sdk: str
    major: int


def slices(output: str) -> List[Tuple[str, List[Dict[str, str]]]]:
    """Split `otool -arch all -l` output into (architecture, load commands).

    Each load command is the dict of its `key value` lines, first value kept.
    A thin binary's one slice has the architecture "".
    """
    found: List[Tuple[str, List[Dict[str, str]]]] = []
    commands: Optional[List[Dict[str, str]]] = None
    fields: Optional[Dict[str, str]] = None
    for line in output.splitlines():
        if not line.strip():
            continue
        if not line[0].isspace():
            if line.startswith("Load command "):
                if commands is None:
                    raise Unreadable("otool printed a load command before naming the file")
                fields = {}
                commands.append(fields)
            elif line.endswith(":"):
                commands = []
                fields = None
                found.append((_HEADER.match(line).group("arch") or "", commands))
            else:
                # "Section" opens a segment's section; its fields are not the
                # command's. Other flush-left lines (a dylib's "compatibility
                # version") carry nothing this check reads.
                fields = None if line == "Section" else fields
            continue
        if fields is None:
            continue
        match = _FIELD.match(line)
        if match:
            fields.setdefault(match.group("key"), match.group("value"))
    return found


def _slice_name(arch: str) -> str:
    return f"the {arch} slice" if arch else "the executable"


def read_sdks(output: str) -> List[Reading]:
    """Every macOS SDK the slices record; raise Unreadable when one cannot be read."""
    found = slices(output)
    if not found:
        raise Unreadable("otool named no file or architecture slice (is it a Mach-O executable?)")
    readings: List[Reading] = []
    for arch, commands in found:
        records = []
        for fields in commands:
            command = fields.get("cmd", "")
            if command == "LC_BUILD_VERSION":
                platform = fields.get("platform", "")
                if platform.lower() not in _MACOS:
                    raise Unreadable(
                        f"{_slice_name(arch)} records LC_BUILD_VERSION for platform {platform or '(none)'}, not macOS"
                    )
                records.append((command, fields))
            elif command == "LC_VERSION_MIN_MACOSX":
                records.append((command, fields))
            elif command.startswith("LC_VERSION_MIN_"):
                raise Unreadable(f"{_slice_name(arch)} records {command}, not a macOS version")
        if not records:
            raise Unreadable(f"{_slice_name(arch)} has no LC_BUILD_VERSION or LC_VERSION_MIN_MACOSX load command")
        for command, fields in records:
            sdk = fields.get("sdk")
            if sdk is None:
                raise Unreadable(f"{_slice_name(arch)}'s {command} has no sdk line")
            version = _VERSION.match(sdk)
            if version is None:
                raise Unreadable(f"{_slice_name(arch)}'s {command} sdk is {sdk!r}, not a version")
            readings.append(Reading(arch, command, sdk, int(version.group("major"))))
    return readings


def executable_of(path: Path) -> Path:
    """The executable to read: PATH itself, or the one an .app bundle names."""
    if path.is_dir():
        plist = path / "Contents" / "Info.plist"
        try:
            with plist.open("rb") as handle:
                info = plistlib.load(handle)
        except Exception as error:  # any unreadable plist fails closed
            raise Unreadable(f"{path} is a folder without a readable Contents/Info.plist ({error})")
        name = info.get("CFBundleExecutable") if isinstance(info, dict) else None
        if not isinstance(name, str) or not name or "/" in name:
            raise Unreadable(f"{plist} names no CFBundleExecutable")
        path = path / "Contents" / "MacOS" / name
    if not path.is_file():
        raise Unreadable(f"{path} does not exist")
    return path


def otool_load_commands(executable: Path, run: Runner = subprocess.run) -> str:
    try:
        result = run(["otool", "-arch", "all", "-l", str(executable)], capture_output=True, text=True)
    except OSError as error:
        raise Unreadable(f"otool could not run ({error})")
    if result.returncode != 0:
        detail = (result.stderr or result.stdout or "").strip().splitlines()
        raise Unreadable(f"otool exited {result.returncode}" + (f": {detail[-1]}" if detail else ""))
    return result.stdout


def _recorded(readings: List[Reading]) -> str:
    return ", ".join(f"{r.sdk} ({r.arch})" if r.arch else r.sdk for r in readings)


def tripped_message(path: str, readings: List[Reading]) -> str:
    return (
        f"SDK tripwire (#1460): {path} records macOS SDK {_recorded(readings)}. "
        f"The SlateMac executable must record an SDK below {SILENT_FROM_SDK} until #1460 is fixed.\n"
        "\n"
        f"From SDK {SILENT_FROM_SDK}, SwiftUI draws the reading view's selectable text itself, and the link "
        "elements it gives VoiceOver have no press action. VoiceOver users would lose reading-link "
        "activation silently: VO-Space on a link in the reading view would do nothing, with no alert and "
        "nothing spoken. The #1421 guard (ReadingLinkSystemOpen) catches only the press of AppKit's text "
        f"field, which SwiftUI uses below SDK {SILENT_FROM_SDK}, and no XCTest can notice the change "
        "because the xctest runner already records SDK 27.\n"
        "\n"
        f"Before the binary may record SDK {SILENT_FROM_SDK} or later, land the fix tracked in {ISSUE}: "
        "reading-link elements the app owns, whose press calls ReadingLinkRouter, plus a VoiceOver field "
        f"pass on both builds (one recording an SDK below {SILENT_FROM_SDK}, one recording SDK "
        f"{SILENT_FROM_SDK} or later). Retire this check (scripts/check_mac_sdk.py) in that PR."
    )


def unreadable_message(path: str, reason: str) -> str:
    return (
        f"SDK tripwire (#1460): cannot read the SDK {path} records: {reason}.\n"
        "\n"
        "The tripwire fails closed, so that a change in otool's output or in the binary's format cannot "
        f"switch it off silently. Read `otool -arch all -l {path}` by hand. If the binary is sound, teach "
        "scripts/check_mac_sdk.py the new shape, with a fact in scripts/tests/test_mac_sdk.py. Why the SDK "
        f"matters to VoiceOver users: {ISSUE}"
    )


def check(
    paths: List[str],
    run: Runner = subprocess.run,
    out: TextIO = sys.stdout,
    err: TextIO = sys.stderr,
    annotate: Optional[bool] = None,
) -> int:
    """Judge every PATH; return the exit status."""
    if annotate is None:
        annotate = os.environ.get("GITHUB_ACTIONS") == "true"
    status = 0
    for given in paths:
        try:
            readings = read_sdks(otool_load_commands(executable_of(Path(given)), run))
        except Unreadable as problem:
            status = 1
            summary = f"cannot read the SDK {given} records: {problem}"
            message = unreadable_message(given, str(problem))
        else:
            tripped = [r for r in readings if r.major >= SILENT_FROM_SDK]
            if not tripped:
                print(
                    f"SDK tripwire (#1460): {given} records macOS SDK {_recorded(readings)}, below "
                    f"{SILENT_FROM_SDK}: the reading view keeps the AppKit link press the #1421 guard routes.",
                    file=out,
                )
                continue
            status = 1
            summary = (
                f"{given} records macOS SDK {_recorded(tripped)}; VoiceOver users would lose reading-link "
                f"activation silently. See {ISSUE}"
            )
            message = tripped_message(given, readings)
        if annotate:
            print(f"::error title=SDK tripwire (#1460)::{summary}", file=out)
        print(message, file=err)
    return status


def main(argv: Optional[List[str]] = None) -> int:
    args = sys.argv[1:] if argv is None else argv
    if args and args[0] in ("-h", "--help"):
        print((__doc__ or "").strip())
        return 0
    if not args:
        print("usage: check_mac_sdk.py PATH [PATH ...]  (an executable or an .app bundle)", file=sys.stderr)
        return 2
    return check(args)


if __name__ == "__main__":
    sys.exit(main())
