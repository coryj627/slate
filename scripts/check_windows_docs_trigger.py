#!/usr/bin/env python3
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Prove the Windows workflow runs for every docs file its checks read.

`.github/workflows/windows.yml` runs the Windows lane only for the paths in
its one path list, `on.push.paths`: GitHub applies it to pushes to main, and
the lane's `changes` job (scripts/lane_changes.py, #1565) applies it to pull
requests. The lane's censuses read plan and help documents, so a docs-only
change to a file they read must run the lane, while a change to a planning
document none of them reads should not pay for a full Windows run.

This check collects every docs path the lane reads and fails if the list does
not match a path, or if `on.pull_request` carries a list of its own. It reads:

* string literals and `"docs", "plans"|"help", ...` path chains in the
  Windows sources (`apps/slate-windows/**/*.cs`), in any case and with
  either separator, resolving same-file string
  constants, plus literals shaped like a docs/plans-relative path
  (`NN_name.md`, `NN_name/...`), so a helper such as `ReadPlan(SpecDoc)` is
  covered through its constant;
* the `decision` paths in `a11y_trigger_designations.json`, which the trigger
  census checks for existence;
* the checklist and contract links in `at_navigation_map.md`, which the AT
  navigation map census opens relative to its folder;
* the docs paths in the string literals of the scripts the workflow runs, as
  `python` or `python3`, with or without switches such as `-I`: a whole path
  (`"docs/plans/38_notification_dispatcher_contracts.md"`), or one a script
  joins segment by segment (`os.path.join(ROOT, "docs", "plans",
  "34_canvas_contracts.md")`, the same as a tuple, a list or `/` operands).

A directory path (no file extension) requires the whole folder to be covered.
Every read, whichever scan found it, is checked where it lands, not as it is
spelled: each `..` resolved, either separator read, and each name in the
checkout's own case, because Windows opens a file in any case and GitHub
matches the list with case.

In the Windows sources, a path built from a helper's parameter is resolved
through every call of that helper in the same file; a call whose argument is
not a string literal or same-file constant, or a helper with no call, fails
the check, naming the file, so a new kind of dynamic read is reviewed rather
than silently missed.

The scan of a script is weaker. It reads two shapes and no other: a string
literal that starts `docs/plans` or `docs/help`, and a run of string
literals that starts `"docs"`, then one that starts `plans` or `help`, and
runs to the end of its call, tuple, list or `/` expression. Its blind spot is
a path built from a value: an f-string, a chain with a segment that is not a
string literal, `Path("docs") / ...` or a join inside a join is not seen at
all. Such a chain is skipped whole, never read as its literal prefix,
because `"docs", "plans", name` is not a read of the folder docs/plans. A
literal that is only the fixed part of a path is read as itself all the
same: `"docs/plans/" + name` reads the folder docs/plans, and
`"docs/plans/%s.md" % name` a file of that name, so the check fails and the
read is looked at. A script the scan cannot parse fails the check.

Exit 0 when every path is covered by the one list; exit 1 with the problems
otherwise.
"""

from __future__ import annotations

import ast
import json
from pathlib import Path
import posixpath
import re
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))

# GitHub's filter-pattern rules live in one place, the script that decides
# pull requests, so this check and that decision cannot disagree.
from lane_changes import covered, glob_regex, trigger_lists  # noqa: E402,F401

REPO = Path(__file__).resolve().parent.parent
WORKFLOW = Path(".github/workflows/windows.yml")
WINDOWS_SOURCES = Path("apps/slate-windows")
DESIGNATIONS = Path("docs/plans/18_windows_port/a11y_trigger_designations.json")
AT_MAP = Path("docs/plans/18_windows_port/at_navigation_map.md")
AT_MAP_DIR = "docs/plans/18_windows_port"

_CONST = re.compile(
    r'(?:const\s+string|static\s+(?:readonly\s+)?string)\s+(\w+)\s*(?:=|=>)\s*"([^"\\]+)"\s*;'
)
# In any case and with either separator, as Windows opens a path: `\` in a
# verbatim string, `\\` in a regular one. landing() gives each read the
# checkout's spelling before it is checked.
_SEPARATOR = r'(?:/|\\{1,2})'
_CHAIN = re.compile(r'"(?i:docs)"\s*,\s*"((?i:plans|help))"((?:\s*,\s*(?:@?"[^"]*"|\w+))*)')
_CHAIN_ARG = re.compile(r'@?"([^"]*)"|(\w+)')
_DOCS_LITERAL = re.compile(r'"((?i:docs)' + _SEPARATOR + r'(?i:plans|help)' + _SEPARATOR + r'[^"\s]+)"')
_PLAN_RELATIVE = re.compile(r'"(\d\d_[A-Za-z0-9_-]+(?:\.md|/[^"\\\s]+))"')
_MAP_LINK = re.compile(r'(?<![\w/.])((?:\.\./)?(?:reports|specs)/[A-Za-z0-9_./-]+\.md|\.\./\d\d_[A-Za-z0-9_-]+\.md)')
# `python`, `python3` or `python3.13`, then any switches (`-I`, `-B`, `-X utf8`), then the script.
_SCRIPT_RUN = re.compile(r'python3?(?:\.\d+)?(?:\s+-(?:[XW]\s*\S+|[A-Za-z]+))*\s+(scripts/[A-Za-z0-9_./-]+\.py)')


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


def requirement(path: str) -> str:
    """The path whose coverage proves a read is covered: a folder read
    stands for any file beneath it."""
    path = path.rstrip("/")
    last = path.rsplit("/", 1)[-1]
    return path if "." in last else f"{path}/any/file.md"


def _split(value: str) -> list[str]:
    """A literal's path segments, split at either separator."""
    return [segment for segment in re.split(r"[/\\]+", value) if segment]


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
                parts.extend(_split(value))
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
                reads.setdefault("/".join(parts + _split(value)), set()).add(rel)
        for path in _DOCS_LITERAL.findall(text):
            reads.setdefault("/".join(_split(path.split("#", 1)[0])), set()).add(rel)
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


def _segments(node: ast.expr) -> list[str] | None:
    """A string literal as the path segments it holds, split at either
    separator (the app job's scripts run on Windows); None for anything else."""
    if not (isinstance(node, ast.Constant) and isinstance(node.value, str)):
        return None
    return [segment for segment in _split(node.value) if segment != "."]


def _under_docs(first: str, second: str) -> bool:
    # Without case, as Windows opens it. landing() then gives the path the
    # checkout's spelling, which is the one GitHub matches the list against.
    return first.lower() == "docs" and second.lower() in ("plans", "help")


def _division_operands(node: ast.expr) -> list[ast.expr]:
    """`a / b / c` as [a, b, c]: the operator joins path segments."""
    operands: list[ast.expr] = []
    while isinstance(node, ast.BinOp) and isinstance(node.op, ast.Div):
        operands.append(node.right)
        node = node.left
    return [node] + operands[::-1]


# The strings Python builds from fixed text and values: f-strings, and template strings from 3.14.
_BUILT_STRINGS = tuple(kind for kind in (ast.JoinedStr, getattr(ast, "TemplateStr", None)) if kind)


def script_paths(tree: ast.AST) -> list[str]:
    """The docs paths a script names in its string literals, in two shapes.

    A whole path: one literal that starts `docs/plans` or `docs/help`, with
    either separator and no white space (`"docs/help"`, or the same path
    written with backslashes). It is read as itself, whatever stands beside it.
    The fixed text of an f-string or a template string is not one:
    `f"docs/plans/{name}.md"` names neither the folder docs/plans nor any file
    the scan can know.

    A chain: the literal `"docs"`, then one that starts `plans` or `help`, and
    the string literals after it, as the arguments of a call, the items of a
    tuple or list, or the operands of `/`. A chain is read only when every item
    from it to the end of its sequence is a string literal. One that goes on
    with anything else is skipped whole: its literal prefix is not the path it
    names (`"docs", "plans", name` is not a read of the folder docs/plans).
    """
    paths: list[str] = []
    sequences: list[list[ast.expr]] = []
    inner: set[int] = set()
    fragments: set[int] = set()
    for node in ast.walk(tree):  # a parent before its children
        if isinstance(node, _BUILT_STRINGS):
            # Its fixed text is part of a path the scan cannot name, not a path.
            fragments.update(id(value) for value in node.values)
        whole = None if id(node) in fragments else _segments(node)
        if whole is not None and len(whole) >= 2 and _under_docs(whole[0], whole[1]) and not re.search(r"\s", node.value):
            paths.append("/".join(whole))
        if isinstance(node, ast.Call):
            sequences.append(node.args)
        elif isinstance(node, (ast.Tuple, ast.List)):
            sequences.append(node.elts)
        elif isinstance(node, ast.BinOp) and isinstance(node.op, ast.Div) and id(node) not in inner:
            # ast.walk yields the outermost `/` of a run first; its left operands are the same run.
            below = node.left
            while isinstance(below, ast.BinOp) and isinstance(below.op, ast.Div):
                inner.add(id(below))
                below = below.left
            sequences.append(_division_operands(node))
    for items in sequences:
        at = 0
        while at + 1 < len(items):
            first, second = _segments(items[at]), _segments(items[at + 1])
            if not first or len(first) != 1 or not second or not _under_docs(first[0], second[0]):
                at += 1
                continue
            parts = list(first)
            at += 1
            while at < len(items) and _segments(items[at]) is not None:
                parts.extend(_segments(items[at]))
                at += 1
            if at == len(items):
                paths.append("/".join(parts))
    return paths


def script_reads(repo: Path, workflow_text: str) -> tuple[dict[str, set[str]], list[str]]:
    """The docs paths the scripts the workflow runs name, and the scripts the
    scan could not parse. A script is one run as `python` or `python3`, with
    or without switches such as `-I`."""
    reads: dict[str, set[str]] = {}
    unparsed: list[str] = []
    for script in sorted(set(_SCRIPT_RUN.findall(workflow_text))):
        path = repo / script
        if not path.is_file() or path.name == Path(__file__).name:
            continue
        # The parsed script, not its text: a path in a comment, or in a docstring that says more than the path,
        # is not a read. Its bytes, so Python reads a byte-order mark or a coding line as it does when it runs
        # the script.
        try:
            tree = ast.parse(path.read_bytes())
        except (SyntaxError, ValueError, RecursionError) as error:
            where = f"line {error.lineno}: {error.msg}" if isinstance(error, SyntaxError) and error.lineno else str(error) or type(error).__name__
            unparsed.append(
                f"{script}: cannot be parsed ({where}), so the docs paths it names were "
                "not checked; the docs census job's Python must be able to read every script the workflow runs"
            )
            continue
        for found in script_paths(tree):
            reads.setdefault(found, set()).add(script)
    return reads, unparsed


def landing(repo: Path, path: str) -> str:
    """Where a read lands: each `..` resolved, either separator read, and each
    folder and file spelled as the checkout spells it.

    The lane runs on Windows, which opens `Docs/Plans/X.md` as readily as the
    file's own spelling, and GitHub matches the path list with case. A read
    is checked under the name a change to the file would carry. From the first
    segment the checkout does not have, the rest stays as it is spelled.
    """
    segments = posixpath.normpath(path.replace("\\", "/")).split("/")
    here, out = repo, []
    for index, segment in enumerate(segments):
        try:
            names = [entry.name for entry in here.iterdir()]
        except OSError:
            names = []
        matches = [segment] if segment in names else [name for name in names if name.lower() == segment.lower()]
        if len(matches) != 1:
            return "/".join(out + segments[index:])
        out.append(matches[0])
        here = here / matches[0]
    return "/".join(out)


def check(repo: Path = REPO) -> list[str]:
    """Return the problems found; empty when the trigger is complete."""
    workflow_text = (repo / WORKFLOW).read_text(encoding="utf-8")
    lists = trigger_lists(workflow_text)
    problems: list[str] = []
    if "push" not in lists:
        return [f"{WORKFLOW}: on.push.paths not found"]
    if "pull_request" in lists:
        problems.append(
            f"{WORKFLOW}: on.pull_request.paths found; the changes job decides pull requests from on.push.paths, "
            "and a second list would keep the lane from reporting on some pull requests (#1565)"
        )

    from_sources, unresolved = source_reads(repo)
    from_scripts, unparsed = script_reads(repo, workflow_text)
    # A path is checked where it lands, not as it is spelled: docs/help/../plans/x.md,
    # with either separator, is a plan document, whatever the list says about docs/help.
    reads: dict[str, set[str]] = {}
    for extra in (from_sources, designation_reads(repo), at_map_reads(repo), from_scripts):
        for path, sources in extra.items():
            reads.setdefault(landing(repo, path), set()).update(sources)
    problems.extend(unresolved)
    problems.extend(unparsed)

    for path in sorted(reads):
        if not covered(requirement(path), lists["push"]):
            readers = ", ".join(sorted(reads[path]))
            problems.append(f"{path} is read by the Windows lane ({readers}) but on.push.paths in {WORKFLOW} does not match it")
    return problems


def main() -> int:
    problems = check()
    if problems:
        for problem in problems:
            print(f"::error::{problem}")
        print(
            f"Add each path (or its folder with /**) to on.push.paths in {WORKFLOW}, the lane's one path list, "
            "so a docs-only change to it still runs the Windows lane on main and on pull requests."
        )
        return 1
    print("Every docs path the Windows lane reads is in windows.yml's path list.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
