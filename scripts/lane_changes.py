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

A lane may also keep a census tier (#1945): its changes job hands
lane-changes.yml a `census-paths: |` block, a sub-list of `on.push.paths`
whose files only some of the lane's checks read (the Windows lane's censuses
read the Mac sources and some plan documents). When every changed file the
lane's paths match is also in that sub-list, the scope is `census` and the
lane runs only those checks; otherwise it is `full`. The script reads the
block twice, as GitHub handed it over (`CENSUS_PATHS`) and from the workflow
file, which is the copy the lane's guards read, and refuses to decide when the
two differ.

`run=true` or `run=false`, then `scope=census` or `scope=full`, go to
$GITHUB_OUTPUT as the script's last act. Anything it cannot decide (no
`on.push.paths`, a pattern whose syntax it does not model, a census list that
is not a literal block of the lane's own paths, a checkout that is not a merge
commit, a git error) exits 1 and writes nothing. The lane's jobs read a
missing decision as "run" and a missing scope as "full", so a failure here
costs a full lane run, never a skipped job.

Usage, from the root of the checkout:  lane_changes.py .github/workflows/<lane>.yml
"""

from __future__ import annotations

import functools
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
        line = raw.split("#", 1)[0].rstrip() if not raw.lstrip().startswith(("- '", '- "')) else raw.rstrip()
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
            lists.setdefault(event, []).append(_item(stripped[2:]))
    return lists


def _item(text: str) -> str:
    """One list item: a quoted pattern keeps a `#` inside its quotes, and a
    comment after it is dropped; an unquoted one ends at its comment."""
    text = text.strip()
    if text[:1] in ("'", '"'):
        quote = text[0]
        end = text.find(quote, 1)
        if end < 0 or text[end + 1:end + 2] == quote:
            raise ValueError(f"{text!r}: write the pattern in one pair of quotes, with no quote inside")
        return text[1:end]
    return text.split("#", 1)[0].strip()


@functools.lru_cache(maxsize=None)
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


def census_patterns(workflow_text: str) -> list[str]:
    """The lane's census-only patterns, [] when it keeps none (#1945).

    They are the `census-paths: |` block under `jobs.changes.with`, one pattern
    a line; blank lines and lines that start with `#` are notes. A literal
    block is the one style whose lines GitHub hands over as they are written,
    so any other is refused.
    """
    patterns: list[str] = []
    section = ""
    block_indent: int | None = None
    collecting = False
    for raw in workflow_text.splitlines():
        stripped = raw.strip()
        indent = len(raw) - len(raw.lstrip())
        if collecting:
            if not stripped:
                continue
            if block_indent is None:
                block_indent = indent
            if indent >= block_indent and indent > 6:
                if not stripped.startswith("#"):
                    patterns.append(stripped)
                continue
            collecting = False
        if not stripped or stripped.startswith("#"):
            continue
        if indent == 0:
            section = "jobs" if stripped == "jobs:" else ""
        elif section.startswith("jobs") and indent == 2:
            section = "jobs.changes" if stripped == "changes:" else "jobs"
        elif section.startswith("jobs.changes") and indent == 4:
            section = "jobs.changes.with" if stripped == "with:" else "jobs.changes"
        elif section == "jobs.changes.with" and indent == 6 and stripped.startswith("census-paths:"):
            style = stripped[len("census-paths:"):].split("#", 1)[0].strip()
            if style not in ("|", "|-"):
                raise ValueError(f"census-paths: {style!r}: write the census list as a literal block, `census-paths: |`")
            collecting, block_indent = True, None
    return patterns


def checked_census(census: list[str], lane: list[str]) -> list[str]:
    """The census list, each pattern one of the lane's own paths, written out
    as it is there: a census pattern outside the lane could never be the only
    thing a pull request touches, and a negation would make the sub-list
    harder to read than the list it narrows."""
    for pattern in census:
        if pattern.startswith("!"):
            raise ValueError(f"census-paths: {pattern!r}: list the census-only paths, with no negation")
        glob_regex(pattern)
        if pattern not in lane:
            raise ValueError(f"census-paths: {pattern!r} is not in on.push.paths; a census path is one of the lane's paths")
    return census


def scope(hits: list[str], census: list[str]) -> str:
    """`census` when the lane runs and every lane path touched is census-only;
    otherwise `full`, which is also the answer for a lane with no census list."""
    if census and hits and all(covered(path, census) for path in hits):
        return "census"
    return "full"


def handed_census(value: str | None) -> list[str] | None:
    """The census list as lane-changes.yml handed it over, read like the block."""
    if value is None:
        return None
    return [line.strip() for line in value.splitlines() if line.strip() and not line.strip().startswith("#")]


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
        text = Path(workflow).read_text(encoding="utf-8")
        patterns = lane_patterns(text)
        census = checked_census(census_patterns(text), patterns)
        handed = handed_census(os.environ.get("CENSUS_PATHS"))
        if handed is not None and handed != census:
            raise ValueError(
                f"the census list lane-changes.yml was handed ({handed}) is not the census-paths block in {workflow} ({census})"
            )
        changed = changed_files()
    except (OSError, ValueError, subprocess.CalledProcessError) as error:
        detail = getattr(error, "stderr", "") or ""
        print(f"::error::{workflow}: cannot decide, so the lane runs in full: {error} {detail}".rstrip())
        return 1
    hits = matching(changed, patterns)
    tier = scope(hits, census)
    if hits:
        shown = ", ".join(hits[:20]) + (f" and {len(hits) - 20} more" if len(hits) > 20 else "")
        print(f"The lane runs: {len(hits)} of the {len(changed)} changed files are in {workflow}'s on.push.paths: {shown}")
        if tier == "census":
            print(f"Every one of them is in its census-paths, so the lane runs its census tier only (#1945).")
        elif census:
            full = [path for path in hits if not covered(path, census)]
            shown = ", ".join(full[:20]) + (f" and {len(full) - 20} more" if len(full) > 20 else "")
            print(f"The lane runs in full: {len(full)} of them are outside its census-paths: {shown}")
    else:
        print(
            f"The lane is skipped: none of the {len(changed)} changed files is in {workflow}'s on.push.paths. "
            "Its jobs report as skipped, which counts as passed (#1565)."
        )
    decision = f"run={'true' if hits else 'false'}\nscope={tier}\n"
    output = os.environ.get("GITHUB_OUTPUT")
    if output:
        with open(output, "a", encoding="utf-8") as handle:
            handle.write(decision)
    else:
        print(decision, end="")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
