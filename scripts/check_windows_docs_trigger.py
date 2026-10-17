#!/usr/bin/env python3
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Prove the Windows workflow runs for every docs file its checks read.

`.github/workflows/windows.yml` runs the Windows lane only for the paths in
its `on.pull_request.paths` and `on.push.paths` lists. The lane's censuses
read plan and help documents, so a docs-only change to a file they read must
start the lane, while a change to a planning document none of them reads
should not pay for a full Windows run.

This check collects every docs path the lane reads and fails if a path is not
matched by both trigger lists. It reads:

* string literals and `"docs", "plans"|"help", ...` path chains in the
  Windows sources (`apps/slate-windows/**/*.cs`), resolving same-file string
  constants, plus literals shaped like a docs/plans-relative path
  (`NN_name.md`, `NN_name/...`), so a helper such as `ReadPlan(SpecDoc)` is
  covered through its constant;
* the `decision` paths in `a11y_trigger_designations.json`, which the trigger
  census checks for existence;
* the checklist and contract links in `at_navigation_map.md`, which the AT
  navigation map census opens relative to its folder;
* `docs/...` literals in the scripts the workflow runs.

A directory path (no file extension) requires the whole folder to be covered.
A path built from a helper's parameter is resolved through every call of
that helper in the same file; a call whose argument is not a string literal
or same-file constant, or a helper with no call, fails the check, naming the
file, so a new kind of dynamic read is reviewed rather than silently missed.

Exit 0 when every path is covered and both trigger lists agree; exit 1 with
the uncovered paths otherwise.
"""

from __future__ import annotations

import json
from pathlib import Path
import re
import sys

REPO = Path(__file__).resolve().parent.parent
WORKFLOW = Path(".github/workflows/windows.yml")
WINDOWS_SOURCES = Path("apps/slate-windows")
DESIGNATIONS = Path("docs/plans/18_windows_port/a11y_trigger_designations.json")
AT_MAP = Path("docs/plans/18_windows_port/at_navigation_map.md")
AT_MAP_DIR = "docs/plans/18_windows_port"

_CONST = re.compile(
    r'(?:const\s+string|static\s+(?:readonly\s+)?string)\s+(\w+)\s*(?:=|=>)\s*"([^"\\]+)"\s*;'
)
_CHAIN = re.compile(r'"docs"\s*,\s*"(plans|help)"((?:\s*,\s*(?:"[^"\\]*"|\w+))*)')
_CHAIN_ARG = re.compile(r'"([^"\\]*)"|(\w+)')
_DOCS_LITERAL = re.compile(r'"(docs/(?:plans|help)/[^"\\\s]+)"')
_PLAN_RELATIVE = re.compile(r'"(\d\d_[A-Za-z0-9_-]+(?:\.md|/[^"\\\s]+))"')
_MAP_LINK = re.compile(r'(?<![\w/.])((?:\.\./)?(?:reports|specs)/[A-Za-z0-9_./-]+\.md|\.\./\d\d_[A-Za-z0-9_-]+\.md)')
_SCRIPT_RUN = re.compile(r'python3\s+(scripts/[A-Za-z0-9_./-]+\.py)')
_SCRIPT_DOCS = re.compile(r'["\'](docs/(?:plans|help)(?:/[^"\'\s]*)?)["\']')


_METHOD = re.compile(r"(\w+)\s*\(([^()]*)\)\s*(?:=>|\{)")


def _call_arguments(text: str, open_paren: int) -> list[str] | None:
    """Split a call's arguments at top-level commas, from just after `(`."""
    depth, quote, current, out = 0, False, [], []
    for i in range(open_paren, len(text)):
        ch = text[i]
        if quote:
            current.append(ch)
            if ch == '"' and text[i - 1] != "\\":
                quote = False
            continue
        if ch == '"':
            quote = True
            current.append(ch)
        elif ch in "([{":
            depth += 1
            current.append(ch)
        elif ch in ")]}":
            if depth == 0:
                out.append("".join(current).strip())
                return [a for a in out if a] if out != [""] else []
            depth -= 1
            current.append(ch)
        elif ch == "," and depth == 0:
            out.append("".join(current).strip())
            current = []
        else:
            current.append(ch)
    return None


def _helper_arguments(text: str, chain_at: int, parameter: str, consts: dict[str, str]) -> list[str] | None:
    """Resolve every argument passed for `parameter` to the helper that
    builds a docs path from it, or None if any cannot be resolved."""
    helper = None
    for method in _METHOD.finditer(text, 0, chain_at):
        params = [p.strip() for p in method.group(2).split(",") if p.strip()]
        for index, param in enumerate(params):
            if re.fullmatch(r"(?:\w+\s+)*string\??\s+" + re.escape(parameter), param):
                helper = (method.group(1), index, method.start(), method.end())
    if helper is None:
        return None
    name, index, start, end = helper
    values: list[str] = []
    for call in re.finditer(r"\b" + re.escape(name) + r"\s*\(", text):
        if start <= call.start() < end:
            continue
        args = _call_arguments(text, call.end())
        if args is None or index >= len(args):
            return None
        literal = re.fullmatch(r'"([^"\\]*)"', args[index])
        if literal:
            values.append(literal.group(1))
        elif args[index] in consts:
            values.append(consts[args[index]])
        else:
            return None
    return values or None


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

    `**` matches any characters including `/`, `*` any characters but `/`,
    `?` one character but `/`. A trailing `/**` also matches the folder's
    own files at any depth.
    """
    out = []
    i = 0
    while i < len(pattern):
        if pattern.startswith("**", i):
            out.append(".*")
            i += 2
        elif pattern[i] == "*":
            out.append("[^/]*")
            i += 1
        elif pattern[i] == "?":
            out.append("[^/]")
            i += 1
        else:
            out.append(re.escape(pattern[i]))
            i += 1
    return re.compile("".join(out) + r"\Z")


def covered(path: str, patterns: list[str]) -> bool:
    """Whether GitHub would run the workflow for a change to `path`.

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


def requirement(path: str) -> str:
    """The path whose coverage proves a read is covered: a folder read
    stands for any file beneath it."""
    path = path.rstrip("/")
    last = path.rsplit("/", 1)[-1]
    return path if "." in last else f"{path}/any/file.md"


def source_reads(repo: Path) -> tuple[dict[str, set[str]], list[str]]:
    reads: dict[str, set[str]] = {}
    unresolved: list[str] = []
    for file in sorted((repo / WINDOWS_SOURCES).rglob("*.cs")):
        rel = file.relative_to(repo).as_posix()
        text = file.read_text(encoding="utf-8", errors="replace")
        consts = dict(_CONST.findall(text))
        for match in _CHAIN.finditer(text):
            parts = ["docs", match.group(1)]
            args = _CHAIN_ARG.findall(match.group(2))
            dynamic = None
            for position, (literal, name) in enumerate(args):
                value = literal if literal else consts.get(name)
                if value is None:
                    dynamic = (name, position == len(args) - 1)
                    break
                parts.extend(segment for segment in value.split("/") if segment)
            if dynamic is None:
                reads.setdefault("/".join(parts), set()).add(rel)
                continue
            # The rest of the path is a helper's parameter, as in
            # ReadPlan(string relative) => Path.Combine(..., "docs", "plans",
            # relative). Every call of that helper in this file must pass a
            # literal or a same-file constant; each one is a read.
            name, last = dynamic
            values = _helper_arguments(text, match.start(), name, consts) if last else None
            if values is None:
                unresolved.append(
                    f"{rel}: a docs path is built from `{name}` at run time and not every call of its helper "
                    "in this file passes a string literal or constant; resolve it, or read the document by name"
                )
                continue
            for value in values:
                path = parts + [segment for segment in value.split("/") if segment]
                reads.setdefault("/".join(path), set()).add(rel)
        for path in _DOCS_LITERAL.findall(text):
            reads.setdefault(path.split("#", 1)[0], set()).add(rel)
        for relative in _PLAN_RELATIVE.findall(text):
            reads.setdefault(f"docs/plans/{relative.split('#', 1)[0]}", set()).add(rel)
    return reads, unresolved


def designation_reads(repo: Path) -> dict[str, set[str]]:
    reads: dict[str, set[str]] = {}

    def walk(node: object) -> None:
        if isinstance(node, dict):
            for key, value in node.items():
                if key.lower() == "decision" and isinstance(value, str) and value.startswith("docs/"):
                    reads.setdefault(value.split("#", 1)[0], set()).add(DESIGNATIONS.as_posix())
                walk(value)
        elif isinstance(node, list):
            for value in node:
                walk(value)

    walk(json.loads((repo / DESIGNATIONS).read_text(encoding="utf-8")))
    return reads


def at_map_reads(repo: Path) -> dict[str, set[str]]:
    reads: dict[str, set[str]] = {}
    for link in _MAP_LINK.findall((repo / AT_MAP).read_text(encoding="utf-8")):
        parts: list[str] = AT_MAP_DIR.split("/")
        for segment in link.split("/"):
            if segment == "..":
                parts.pop()
            elif segment not in ("", "."):
                parts.append(segment)
        reads.setdefault("/".join(parts), set()).add(AT_MAP.as_posix())
    return reads


def script_reads(repo: Path, workflow_text: str) -> dict[str, set[str]]:
    reads: dict[str, set[str]] = {}
    for script in sorted(set(_SCRIPT_RUN.findall(workflow_text))):
        path = repo / script
        if not path.is_file() or path.name == Path(__file__).name:
            continue
        for found in _SCRIPT_DOCS.findall(path.read_text(encoding="utf-8")):
            reads.setdefault(found.rstrip("/"), set()).add(script)
    return reads


def check(repo: Path = REPO) -> list[str]:
    """Return the problems found; empty when the trigger is complete."""
    workflow_text = (repo / WORKFLOW).read_text(encoding="utf-8")
    lists = trigger_lists(workflow_text)
    problems: list[str] = []
    for event in ("pull_request", "push"):
        if event not in lists:
            problems.append(f"{WORKFLOW}: on.{event}.paths not found")
    if problems:
        return problems
    if lists["pull_request"] != lists["push"]:
        problems.append(f"{WORKFLOW}: on.pull_request.paths and on.push.paths differ; keep them identical")

    reads, unresolved = source_reads(repo)
    for extra in (designation_reads(repo), at_map_reads(repo), script_reads(repo, workflow_text)):
        for path, sources in extra.items():
            reads.setdefault(path, set()).update(sources)
    problems.extend(unresolved)

    for path in sorted(reads):
        need = requirement(path)
        for event in ("pull_request", "push"):
            if not covered(need, lists[event]):
                readers = ", ".join(sorted(reads[path]))
                problems.append(
                    f"{path} is read by the Windows lane ({readers}) but on.{event}.paths in {WORKFLOW} does not match it"
                )
    return problems


def main() -> int:
    problems = check()
    if problems:
        for problem in problems:
            print(f"::error::{problem}")
        print(
            "Add each path (or its folder with /**) to BOTH on.pull_request.paths and on.push.paths in "
            f"{WORKFLOW}, so a docs-only change to it still runs the Windows lane."
        )
        return 1
    print("Every docs path the Windows lane reads is in both of windows.yml's path triggers.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
