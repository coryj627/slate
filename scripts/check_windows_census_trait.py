#!/usr/bin/env python3
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Prove every Windows test that reads a census-only path runs in the census tier (#1945).

A pull request that touches only the Windows lane's census-only paths (the
`census-paths` block of `.github/workflows/windows.yml`: the Mac sources and
the plan and help documents the censuses read) runs the census tier: the app
job builds, then runs only the tests tagged `[Trait("tier", "census")]`. A test
that reads one of those paths without the tag would silently stop running on
exactly the change that can break it. This check fails when one does.

It reads the C# sources under `apps/slate-windows` with comments set aside and
string contents kept apart, and finds each read of a census-only path:

* a string literal that is such a path (`"apps/slate-mac/Sources/X.swift"`), or
  a plan-relative one (`"38_notification_dispatcher_contracts.md"`, read under
  docs/plans);
* an interpolated string whose text before its first hole is a census folder
  (`$"apps/slate-mac/{name}"`);
* a run of literal or same-file constant arguments of one call, joined as path
  segments (`Path.Combine(root, "docs", "plans", ContractsDoc)`), and a run
  whose next argument is computed at run time when the run already names a
  census folder (`Path.Combine(root, "apps", "slate-mac", name)`).

Each read belongs to the outermost type around it. A test type (one with a
`[Fact]` or `[Theory]`) must carry the trait on one of its declarations. A type
with no tests (a helper such as `A11yTriggerInventory`) passes the duty on to
every type that names it, transitively, so the tests that call the helper carry
the trait. A read in the app, a tool or the FlaUI gate's project fails outright:
the census tier runs none of them.

The census tier runs the trait's tests through a filter, and a filter that
matches nothing passes. `--trx FILE` checks the census run's results instead:
every type this check requires the trait on ran at least one test.

Exit 0 when every reader carries the trait (or, with --trx, every one ran);
exit 1 with the problems otherwise.

Usage, from the root of the checkout:
  check_windows_census_trait.py                  the guard
  check_windows_census_trait.py --list           the types that need the trait
  check_windows_census_trait.py --trx FILE.trx   the census run ran each of them
"""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ElementTree

sys.path.insert(0, str(Path(__file__).resolve().parent))

# The lane decision's own readers, so this check and the decision read one list.
from lane_changes import census_patterns, checked_census, covered, lane_patterns  # noqa: E402

REPO = Path(__file__).resolve().parent.parent
WORKFLOW = Path(".github/workflows/windows.yml")
WINDOWS_SOURCES = Path("apps/slate-windows")
TEST_PROJECT = Path("apps/slate-windows/tests/SlateWindows.Tests")
TRAIT = ("tier", "census")
TRAIT_SOURCE = f'[Trait("{TRAIT[0]}", "{TRAIT[1]}")]'

_TRAIT = re.compile(r'\bTrait\s*\(\s*"' + TRAIT[0] + r'"\s*,\s*"' + TRAIT[1] + r'"\s*\)')
_TEST_ATTRIBUTE = re.compile(r"\[\s*(?:Xunit\.)?(?:Fact|Theory)\b")
_TYPE = re.compile(
    r"(?<![\w.])((?:(?:public|internal|private|protected|sealed|static|partial|abstract|file|unsafe|readonly|ref|new)\s+)*)"
    r"(class|struct|interface|record(?:\s+class|\s+struct)?)\s+([A-Za-z_]\w*)"
)
_CONST = re.compile(r'(?:const\s+string|static\s+(?:readonly\s+)?string)\s+(\w+)\s*(?:=|=>)\s*"([^"\\]+)"\s*;')
_PLAN_RELATIVE = re.compile(r"\d\d_[A-Za-z0-9_-]+(?:\.md|/\S+)")
_IDENTIFIER = re.compile(r"[A-Za-z_][\w.]*")


@dataclass
class Literal:
    start: int
    end: int
    value: str
    interpolated: bool = False


@dataclass
class Source:
    """One C# file: the text, the text without comments, and the text without
    comments or string contents, all the same length, plus its literals."""

    path: str
    text: str
    no_comments: str
    code: str
    literals: list[Literal]
    types: list[tuple[str, int, int, int]] = field(default_factory=list)  # name, declaration, body start, body end


def scrub(text: str) -> tuple[str, str, list[Literal]]:
    """Blank comments, then string contents, keeping every offset and newline."""
    no_comments = list(text)
    code = list(text)
    literals: list[Literal] = []
    i, n = 0, len(text)

    def blank(chars: list[str], start: int, end: int) -> None:
        for k in range(start, end):
            if chars[k] != "\n":
                chars[k] = " "

    while i < n:
        ch = text[i]
        if text.startswith("//", i):
            end = text.find("\n", i)
            end = n if end < 0 else end
            blank(no_comments, i, end)
            blank(code, i, end)
            i = end
            continue
        if text.startswith("/*", i):
            end = text.find("*/", i + 2)
            end = n if end < 0 else end + 2
            blank(no_comments, i, end)
            blank(code, i, end)
            i = end
            continue
        if ch == "'":
            end = i + 1
            while end < n and text[end] != "'":
                end += 2 if text[end] == "\\" else 1
            blank(code, i + 1, min(end, n))
            i = end + 1
            continue
        prefix = re.match(r'(\$+@?|@\$+|@)?("""+|")', text[i:i + 8])
        if prefix and (i == 0 or not (text[i - 1].isalnum() or text[i - 1] == "_")):
            sigils, quotes = prefix.group(1) or "", prefix.group(2)
            open_end = i + len(prefix.group(0))
            interpolated = "$" in sigils
            if len(quotes) >= 3:
                close = text.find(quotes, open_end)
                close = n if close < 0 else close
                literals.append(Literal(i, close + len(quotes), text[open_end:close], interpolated))
                blank(code, open_end, close)
                i = close + len(quotes)
                continue
            verbatim = "@" in sigils
            k, value, depth = open_end, [], 0
            while k < n:
                c = text[k]
                if depth == 0:
                    if verbatim and text.startswith('""', k):
                        value.append('"')
                        k += 2
                        continue
                    if not verbatim and c == "\\" and k + 1 < n:
                        value.append({"\\": "\\", '"': '"', "n": "\n", "t": "\t"}.get(text[k + 1], text[k + 1]))
                        k += 2
                        continue
                    if c == '"':
                        break
                    if interpolated and text.startswith("{{", k):
                        value.append("{")
                        k += 2
                        continue
                    if interpolated and c == "{":
                        depth = 1
                    value.append(c)
                else:
                    if c == "{":
                        depth += 1
                    elif c == "}":
                        depth -= 1
                    elif c == '"':
                        # A literal inside an interpolation hole: skip it.
                        k = text.find('"', k + 1)
                        k = n - 1 if k < 0 else k
                    value.append(c)
                k += 1
            literals.append(Literal(i, k + 1, "".join(value), interpolated))
            blank(code, open_end, k)
            i = k + 1
            continue
        i += 1
    return "".join(no_comments), "".join(code), literals


def closing_brace(code: str, open_at: int) -> int:
    depth = 0
    for k in range(open_at, len(code)):
        if code[k] == "{":
            depth += 1
        elif code[k] == "}":
            depth -= 1
            if depth == 0:
                return k
    return len(code)


def load(repo: Path, rel: str) -> Source:
    text = (repo / rel).read_text(encoding="utf-8", errors="replace")
    no_comments, code, literals = scrub(text)
    source = Source(rel, text, no_comments, code, literals)
    for match in _TYPE.finditer(code):
        body = re.compile(r"[{;]").search(code, match.end())
        if not body or code[body.start()] != "{":
            continue
        source.types.append((match.group(3), match.start(), body.start(), closing_brace(code, body.start())))
    return source


def outermost_type(source: Source, at: int) -> str | None:
    best = None
    for name, _, start, end in source.types:
        if start <= at <= end and (best is None or start < best[1]):
            best = (name, start)
    return best[0] if best else None


def requirement(path: str) -> str:
    """A folder read stands for any file beneath it."""
    path = path.strip("/")
    last = path.rsplit("/", 1)[-1]
    return path if "." in last else f"{path}/any/file.md"


def census_prefixes(census: list[str]) -> set[str]:
    """Each census pattern's literal folders of two or more segments: a run of
    arguments that names one and continues at run time can land on a census path."""
    out: set[str] = set()
    for pattern in census:
        literal = re.split(r"[*]", pattern, maxsplit=1)[0]
        parts = [part for part in literal.split("/") if part]
        if "*" in pattern or "." not in parts[-1]:
            folders = parts
        else:
            folders = parts[:-1]
        for size in range(2, len(folders) + 1):
            out.add("/".join(folders[:size]))
    return out


def call_arguments(code: str, open_paren: int) -> list[tuple[int, int]] | None:
    """The spans of a call's top-level arguments, from its `(`."""
    depth, start, spans = 0, open_paren + 1, []
    for k in range(open_paren + 1, len(code)):
        c = code[k]
        if c in "([{":
            depth += 1
        elif c in ")]}":
            if depth == 0:
                if code[start:k].strip():
                    spans.append((start, k))
                return spans
            depth -= 1
        elif c == "," and depth == 0:
            spans.append((start, k))
            start = k + 1
    return None


def reads(source: Source, census: list[str]) -> list[tuple[int, str]]:
    """Each census-only path the file reads, with where it reads it."""
    found: list[tuple[int, str]] = []
    prefixes = census_prefixes(census)

    def is_census(path: str) -> bool:
        path = path.replace("\\", "/").strip()
        while path.startswith("./"):
            path = path[2:]
        if not path or " " in path:
            return False
        return covered(requirement(path), census) or covered(path, census)

    for literal in source.literals:
        if literal.interpolated and "{" in literal.value:
            # `$"apps/slate-mac/{name}"`: what comes before the first hole.
            head = literal.value.split("{", 1)[0].replace("\\", "/").rstrip("/")
            if head and (is_census(head) or head in prefixes):
                found.append((literal.start, head))
            continue
        value = literal.value.split("#", 1)[0]
        if is_census(value):
            found.append((literal.start, value))
        elif _PLAN_RELATIVE.fullmatch(value):
            # Read under docs/plans; joined, so check_windows_docs_trigger.py's
            # scan of this script does not take the pattern for a read.
            plan = "/".join(("docs", "plans", value))
            if is_census(plan):
                found.append((literal.start, plan))

    consts = dict(_CONST.findall(source.no_comments))
    by_start = {literal.start: literal for literal in source.literals}
    for paren in re.finditer(r"\(", source.code):
        spans = call_arguments(source.code, paren.start())
        if not spans:
            continue
        run: list[str] = []
        run_start = None
        for start, end in spans + [(None, None)]:
            value = None
            if start is not None:
                lead = start + (len(source.code[start:end]) - len(source.code[start:end].lstrip()))
                arg = source.code[start:end].strip()
                literal = by_start.get(lead)
                if literal and literal.end == lead + len(arg) and not literal.interpolated:
                    value = literal.value
                elif _IDENTIFIER.fullmatch(arg) and arg.rsplit(".", 1)[-1] in consts:
                    value = consts[arg.rsplit(".", 1)[-1]]
            if value is not None:
                run_start = lead if not run else run_start
                run.extend(segment for segment in value.replace("\\", "/").split("/") if segment)
                continue
            if run:
                path = "/".join(run)
                dynamic_tail = start is not None
                if is_census(path) or (dynamic_tail and path in prefixes):
                    found.append((run_start, path))
            run, run_start = [], None
    return found


@dataclass
class Census:
    required: dict[str, set[str]]  # type -> why (the paths, or the helper it names)
    problems: list[str]
    tagged: set[str]
    tests: set[str]


def analyse(repo: Path = REPO) -> Census:
    workflow_text = (repo / WORKFLOW).read_text(encoding="utf-8")
    problems: list[str] = []
    try:
        census = checked_census(census_patterns(workflow_text), lane_patterns(workflow_text))
    except ValueError as error:
        return Census({}, [f"{WORKFLOW}: {error}"], set(), set())
    if not census:
        return Census({}, [f"{WORKFLOW}: the changes job hands lane-changes.yml no census-paths block"], set(), set())

    files = sorted(p.relative_to(repo).as_posix() for p in (repo / WINDOWS_SOURCES).rglob("*.cs")
                   if "/obj/" not in p.as_posix() and "/bin/" not in p.as_posix())
    sources = {rel: load(repo, rel) for rel in files}
    test_project = TEST_PROJECT.as_posix() + "/"

    tests: set[str] = set()
    tagged: set[str] = set()
    for source in sources.values():
        if not source.path.startswith(test_project):
            continue
        for name, declared, start, end in source.types:
            if _TEST_ATTRIBUTE.search(source.code, start, end):
                tests.add(name)
            head = source.no_comments[max(0, declared - 600):declared]
            attributes = re.search(r"((?:\[[^\[\]]*\]\s*)+)$", head)
            if attributes and _TRAIT.search(attributes.group(1)):
                tagged.add(name)

    # Which outermost type reads what.
    readers: dict[str, set[str]] = {}
    for source in sources.values():
        for at, path in reads(source, census):
            owner = outermost_type(source, at)
            line = source.text.count("\n", 0, at) + 1
            if not source.path.startswith(test_project):
                problems.append(
                    f"{source.path}:{line} reads {path}, a census-only path, outside the test project: the census "
                    "tier does not run it; read it from a test in SlateWindows.Tests, or move the path out of census-paths"
                )
                continue
            if owner is None:
                problems.append(f"{source.path}:{line} reads {path} outside any type")
                continue
            readers.setdefault(owner, set()).add(path)

    # Who names each type, by the outermost type around the name.
    words = {rel: set(re.findall(r"[A-Za-z_]\w*", source.code)) for rel, source in sources.items()
             if rel.startswith(test_project)}

    def namers(name: str) -> set[str]:
        out: set[str] = set()
        pattern = re.compile(r"(?<![\w.])" + re.escape(name) + r"\b")
        for rel, present in words.items():
            if name not in present:
                continue
            source = sources[rel]
            for match in pattern.finditer(source.code):
                owner = outermost_type(source, match.start())
                if owner and owner != name:
                    out.add(owner)
        return out

    required: dict[str, set[str]] = {}
    queue = [(name, f"reads {', '.join(sorted(paths))}") for name, paths in sorted(readers.items())]
    seen: set[str] = set()
    while queue:
        name, why = queue.pop(0)
        if name in tests:
            required.setdefault(name, set()).add(why)
        if name in seen:
            continue
        seen.add(name)
        users = namers(name)
        if name not in tests and not users:
            problems.append(f"{name} {why}, and no test names it: a reader no test reaches")
        for user in sorted(users):
            queue.append((user, f"names {name}"))

    for name in sorted(required):
        if name not in tagged:
            problems.append(
                f"{name} {'; '.join(sorted(required[name]))}, so it must carry {TRAIT_SOURCE} on its declaration: "
                "the census tier runs only those tests"
            )
    return Census(required, problems, tagged, tests)


def trx_classes(trx: Path) -> set[str]:
    """The simple class names of every test the results file records as executed."""
    root = ElementTree.parse(trx).getroot()
    ns = {"t": root.tag.split("}")[0].strip("{")} if root.tag.startswith("{") else {}
    prefix = "t:" if ns else ""
    executed = {result.get("testId") for result in root.iterfind(f".//{prefix}UnitTestResult", ns)
                if result.get("outcome") not in (None, "NotExecuted")}
    classes: set[str] = set()
    for test in root.iterfind(f".//{prefix}UnitTest", ns):
        method = test.find(f"{prefix}TestMethod", ns)
        if test.get("id") in executed and method is not None and method.get("className"):
            classes.add(method.get("className").rsplit(".", 1)[-1].split("+", 1)[0])
    return classes


def main(argv: list[str], repo: Path = REPO) -> int:
    result = analyse(repo)
    if len(argv) == 2 and argv[1] == "--list":
        for name in sorted(result.required):
            print(f"{name}: {'; '.join(sorted(result.required[name]))}")
        return 0
    if len(argv) == 3 and argv[1] == "--trx":
        if result.problems:
            for problem in result.problems:
                print(f"::error::{problem}")
            return 1
        ran = trx_classes(Path(argv[2]))
        missing = sorted(set(result.required) - ran)
        if not ran or missing:
            print(f"::error::the census run executed {len(ran)} test classes; these need the trait and ran nothing: "
                  f"{', '.join(missing) or '(every one)'}")
            return 1
        print(f"The census run executed tests of {len(ran)} classes, including all {len(result.required)} that read "
              "a census-only path.")
        return 0
    if len(argv) != 1:
        print(__doc__.split("Usage, from the root of the checkout:")[1], file=sys.stderr)
        return 2
    if result.problems:
        for problem in result.problems:
            print(f"::error::{problem}")
        return 1
    print(f"Every Windows test type that reads a census-only path carries {TRAIT_SOURCE}: "
          f"{len(result.required)} types, {len(result.tagged)} tagged.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
