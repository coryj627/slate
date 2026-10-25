#!/usr/bin/env python3
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Decide whether a pull request runs a lane (#1565, plan 45 XB-13).

Each lane workflow keeps one path list, its `on.push.paths`. GitHub applies it
to pushes to main. On a pull request the lane triggers every time, and its
first job, `changes` (`.github/workflows/lane-changes.yml`), runs this script
in a checkout of the pull request's merge commit: it lists the files that
commit changes on its base parent (`git diff HEAD^1 HEAD`, a rename counted as
a delete and an add) and matches them against that list with GitHub's
filter-pattern rules. The lane's other jobs are skipped when nothing matches,
and GitHub reports a skipped job as passed, so every lane reports on every
pull request and main's ruleset can require its checks.

The diff is from the base main as it stands in the merge commit, so a file
main changed after the branch was cut is not the pull request's change. It has
no file limit, unlike GitHub's own filter, which reads the first 300 files.

`run=true` or `run=false` goes to $GITHUB_OUTPUT as the script's last act.
Anything it cannot decide (no `on.push.paths`, a pattern whose syntax it does
not model, a checkout that is not a merge commit, a git error) exits 1 and
writes nothing. The lane's jobs read a missing decision as "run", so a failure
here costs a full lane run, never a skipped lane.

Usage, from the root of the checkout:  lane_changes.py .github/workflows/<lane>.yml
"""

from __future__ import annotations

import os
from pathlib import Path
import re
import subprocess
import sys

REPO = Path(__file__).resolve().parent.parent

# '?' and '+' repeat the character before them in GitHub's filter patterns and
# '[' opens a character class; the lanes use none of them, so rather than model
# them the check refuses them.
_UNMODELLED = set("?+[]")


def trigger_lists(workflow_text: str) -> dict[str, list[str]]:
    """Return the `paths:` list of each `on:` event, in file order.

    A small line parser keeps the check free of a YAML dependency: it reads
    the `on:` block up to the next top-level key and collects `- 'pattern'`
    items under each event's `paths:`.
    """
    lists: dict[str, list[str]] = {}
    in_on = False
    event: str | None = None
    in_paths = False
    for raw in workflow_text.splitlines():
        line = raw.split("#", 1)[0].rstrip() if not raw.lstrip().startswith("- '") else raw.rstrip()
        if not line.strip():
            continue
        indent = len(line) - len(line.lstrip())
        if indent == 0:
            in_on = line.strip() == "on:"
            event = None
            in_paths = False
            continue
        if not in_on:
            continue
        stripped = line.strip()
        if indent == 2 and stripped.endswith(":"):
            event = stripped[:-1]
            in_paths = False
            continue
        if event and indent == 4:
            in_paths = stripped == "paths:"
            continue
        if event and in_paths and stripped.startswith("- "):
            item = stripped[2:].split("#", 1)[0].strip().strip("'\"")
            lists.setdefault(event, []).append(item)
    return lists


def glob_regex(pattern: str) -> re.Pattern[str]:
    """Translate a GitHub Actions path pattern to a regular expression.

    `*` matches any characters but `/`; `**` matches any characters including
    `/`; `**/` matches zero or more whole folders, so `**/global.json` matches
    the root `global.json` and `docs/**/*.md` matches `docs/README.md`, as
    GitHub's filter-pattern cheat sheet shows. Raises ValueError for the
    syntax the check does not model.
    """
    out = []
    i = 0
    while i < len(pattern):
        if pattern.startswith("**/", i):
            out.append("(?:.*/)?")
            i += 3
        elif pattern.startswith("**", i):
            out.append(".*")
            i += 2
        elif pattern[i] == "*":
            out.append("[^/]*")
            i += 1
        elif pattern[i] in _UNMODELLED:
            raise ValueError(
                f"{pattern!r}: {pattern[i]!r} has a meaning in GitHub's filter patterns this check does not model; "
                "write the list with * and ** only"
            )
        else:
            out.append(re.escape(pattern[i]))
            i += 1
    return re.compile("".join(out) + r"\Z")


def covered(path: str, patterns: list[str]) -> bool:
    """Whether GitHub's path filter would run the workflow for a change to `path`.

    Patterns apply in order; a later `!pattern` excludes what an earlier one
    included, and the last pattern that matches decides.
    """
    decision = False
    for pattern in patterns:
        negated = pattern.startswith("!")
        body = pattern[1:] if negated else pattern
        if glob_regex(body).match(path):
            decision = not negated
    return decision


def lane_patterns(workflow_text: str) -> list[str]:
    """The lane's path list, `on.push.paths`, every pattern checked."""
    patterns = trigger_lists(workflow_text).get("push")
    if not patterns:
        raise ValueError("on.push.paths not found: the lane's path list is the one its push trigger keeps")
    for pattern in patterns:
        glob_regex(pattern[1:] if pattern.startswith("!") else pattern)
    return patterns


def matching(changed: list[str], patterns: list[str]) -> list[str]:
    """The changed files the lane's paths cover."""
    return [path for path in changed if covered(path, patterns)]


def _git(*args: str) -> str:
    return subprocess.run(["git", *args], check=True, capture_output=True, text=True).stdout


def changed_files() -> list[str]:
    """Every file the checked-out merge commit changes on its base parent."""
    parents = _git("rev-list", "--parents", "-n", "1", "HEAD").split()
    if len(parents) != 3:
        count = len(parents) - 1
        raise ValueError(
            f"HEAD {parents[0] if parents else '?'} has {count} parent{'' if count == 1 else 's'}; "
            "a pull request's merge commit has two"
        )
    listing = _git("diff", "--name-only", "--no-renames", "-z", "HEAD^1", "HEAD")
    return [path for path in listing.split("\0") if path]


def main(argv: list[str]) -> int:
    if len(argv) != 2:
        print("usage: lane_changes.py .github/workflows/<lane>.yml", file=sys.stderr)
        return 2
    workflow = argv[1]
    try:
        patterns = lane_patterns(Path(workflow).read_text(encoding="utf-8"))
        changed = changed_files()
    except (OSError, ValueError, subprocess.CalledProcessError) as error:
        detail = getattr(error, "stderr", "") or ""
        print(f"::error::{workflow}: cannot decide, so the lane runs: {error} {detail}".rstrip())
        return 1
    hits = matching(changed, patterns)
    if hits:
        shown = ", ".join(hits[:20]) + (f" and {len(hits) - 20} more" if len(hits) > 20 else "")
        print(f"The lane runs: {len(hits)} of the {len(changed)} changed files are in {workflow}'s on.push.paths: {shown}")
    else:
        print(
            f"The lane is skipped: none of the {len(changed)} changed files is in {workflow}'s on.push.paths. "
            "Its jobs report as skipped, which counts as passed (#1565)."
        )
    decision = f"run={'true' if hits else 'false'}\n"
    output = os.environ.get("GITHUB_OUTPUT")
    if output:
        with open(output, "a", encoding="utf-8") as handle:
            handle.write(decision)
    else:
        print(decision, end="")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
