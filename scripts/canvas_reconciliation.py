#!/usr/bin/env python3
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
"""The W6-1 issue reconciliation's generated tables (#745, PR H, contract H9).

Reads `docs/plans/34_canvas_contracts.md` and derives, from the document's
own records, the three tables the reconciliation section carries between
its `<!-- reconciliation:generated:start -->` / `:end -->` markers:

  (b) contract -> evidence, one row per KEY, a key being the triple
      (section, register kind, id).  The same id recurs across sections
      (C-unit's D1 and PR D's D1; C-unit's T1 as obligation and record),
      which is why the section qualifies it.  The keys are every head of
      the form `**ID — ` (or `- **ID — `) inside a PR section, plus the
      two vocabulary additions, plus the "Verified during implementation"
      bullets (keyed V-n by position).  A row names the record
      subsections of the same section that cite the id ("discharged by")
      and the long PascalCase identifiers those citing paragraphs
      backtick ("pinned by"), each checked against the Windows tree —
      a name that no longer exists is rendered plain, marked "(not in
      the tree)", never silently dropped.  A key nothing cites reads
      "unevidenced" rather than being omitted.
  (c) the divergence register CD-1…CD-48 and the accepted-risk register
      CR-1…CR-5 as an index: id, the head's first clause, where recorded.
  (d) the owner decisions D-1…D-7 with their resolution and evidence.

Usage:
  python scripts/canvas_reconciliation.py           # print the tables
  python scripts/canvas_reconciliation.py --write   # splice them into the doc
  python scripts/canvas_reconciliation.py --check   # exit 1 unless the doc
                                                    # carries exactly the
                                                    # regenerated tables

Every run asserts the counts: each key's head occurs exactly once in its
section, the (b) table has exactly one row per key, and no two keys share
a row.  `--check` is what CanvasReconciliationCensus mirrors.
"""

from __future__ import annotations

import os
import re
import sys
from collections import Counter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
DOC = os.path.join(ROOT, "docs", "plans", "34_canvas_contracts.md")
SHELL = os.path.join(ROOT, "apps", "slate-windows")
START = "<!-- reconciliation:generated:start -->"
END = "<!-- reconciliation:generated:end -->"
NL = "\n"

# The PR sections whose heads are keys, by the label the key carries.
PR_SECTIONS = [
    ("0a", "## PR 0a — "),
    ("0b", "## PR 0b — "),
    ("A", "## PR A — "),
    ("B", "## PR B — "),
    ("C", "## PR C — "),
    ("C-unit", "## PR C-unit — "),
    ("D", "## PR D — "),
    ("E", "## PR E — "),
    ("F", "## PR F — "),
    ("G", "## PR G — "),
    ("G2", "## PR G2 — "),
    ("H", "## PR H — "),
    ("VA", "## Vocabulary additions"),
]
VERIFIED = "## Verified during implementation"
DIVERGENCES = "## Recorded divergences"
RISKS = "## Accepted risks"
DECISIONS = "## Owner decisions"

HEAD = re.compile(r"^(?:- )?\*\*([0-9A-Za-z][0-9A-Za-z-]*?) — ", re.M)
# Record subsections: the task records, the pinning-test lists, the
# implementation and verification records, the close-outs, the task
# loops.  Round records and freezes discuss contracts; they are not
# evidence and are not scanned.
RECORD_HEADING = re.compile(
    r"^### (?:T[A-Z0-9]*-\d+[a-z]? — |Implementation record|Tests that pin|Verification plan"
    r"|§\S* close-out|The task loop|C-lite reconciliation|.*close-out|PR review round)",
)
NOT_KEY_HEADING = re.compile(
    r"Round record|THE FREEZE|The ledger|The IF ledger|The record of how it got here|Codoki round"
    r"|CI round|The sweep|Hand-off|Red-team round|What carries out",
)
LONG_NAME = re.compile(r"`([A-Z][A-Za-z0-9_]{14,})`")

KIND_BY_PREFIX = {
    "0a": "contract", "0b": "contract", "A": "contract", "B": "contract",
    "C": "contract", "U": "contract", "E": "contract", "F": "contract",
    "G": "contract", "G2": "contract", "H": "contract",
    "DD": "decision", "G2D": "decision", "HD": "decision",
    "HD-D": "divergence", "HR": "risk",
    "ID": "obligation", "TD": "task", "TE": "task", "TF": "task",
    "VA": "vocabulary", "IN": "scope", "OUT": "scope",
}


def _prefix(ident: str) -> str:
    if ident in ("IN", "OUT"):
        return ident
    m = re.match(r"^(HD-D|G2D|G2|0a|0b|[A-Z]+)-?\d", ident)
    return m.group(1) if m else ident


def kind_of(section: str, ident: str) -> str:
    p = _prefix(ident)
    if section == "C-unit":
        return {"U": "contract", "D": "design", "T": "task", "I": "invariant", "IN": "scope", "OUT": "scope"}.get(p, "head")
    if section == "D" and p == "D":
        return "contract"
    return KIND_BY_PREFIX.get(p, "head")


def read_doc() -> str:
    with open(DOC, encoding="utf-8") as handle:
        return handle.read()


def sections(doc: str) -> list[tuple[str, int, int]]:
    heads = [(m.start(), m.group(0)) for m in re.finditer(r"^## .+$", doc, re.M)]
    out = []
    for i, (start, title) in enumerate(heads):
        end = heads[i + 1][0] if i + 1 < len(heads) else len(doc)
        out.append((title, start, end))
    return out


def section_text(doc: str, prefix: str) -> tuple[str, int]:
    for title, start, end in sections(doc):
        if title.startswith(prefix):
            return doc[start:end], start
    raise SystemExit(f"section not found: {prefix}")


def subsections(text: str) -> list[tuple[str, str]]:
    """(heading, body) pairs; the preamble before the first ### is ("", body)."""
    parts = re.split(r"^(### .+)$", text, flags=re.M)
    out = [("", parts[0])]
    for i in range(1, len(parts), 2):
        out.append((parts[i], parts[i + 1] if i + 1 < len(parts) else ""))
    return out


COMMENT = re.compile(r"//[^\n]*|/\*.*?\*/", re.S)
DECLARATION = re.compile(
    r"\b(?:class|record|struct|interface|enum|namespace)\s+([A-Z][A-Za-z0-9_]{14,})\b"  # types
    r"|\b([A-Z][A-Za-z0-9_]{14,})\s*(?:\(|\{|=>|;|\s=\s)"                              # members, methods, facts
    r"|\bvar\s+([A-Z][A-Za-z0-9_]{14,})\s*=",                                          # locals
)


def declared_names() -> set[str]:
    """Every long PascalCase identifier DECLARED in the Windows tree — the
    shell, the generated bindings, the tests, the tools, the benchmarks —
    read syntactically with comments stripped, a regex approximation of
    the Roslyn declaration scan ContractsCitationCensus runs; that census
    remains the authority, and a name this set admits wrongly fails there."""
    names: set[str] = set()
    for base, dirs, files in os.walk(SHELL):
        dirs[:] = [d for d in dirs if d not in ("bin", "obj", ".vs")]
        for name in files:
            if not name.endswith(".cs"):
                continue
            with open(os.path.join(base, name), encoding="utf-8", errors="replace") as handle:
                text = COMMENT.sub("", handle.read())
            for match in DECLARATION.finditer(text):
                names.add(next(g for g in match.groups() if g))
    return names


def key_bearing(text: str) -> str:
    """The section minus its record subsections: a contract's head is a
    key where it is STATED; a round record or a task record that quotes
    the head is evidence, not a second key."""
    kept = []
    for heading, body in subsections(text):
        if heading and (RECORD_HEADING.match(heading) or NOT_KEY_HEADING.search(heading)):
            continue
        kept.append(body)
    return NL.join(kept)


def keys(doc: str) -> list[tuple[str, str, str, str]]:
    """(section, kind, id, section text) for every key, in document order."""
    out = []
    for label, prefix in PR_SECTIONS:
        text, _ = section_text(doc, prefix)
        counts = Counter(HEAD.findall(key_bearing(text)))
        for ident, n in counts.items():
            if ident.startswith(("IH-", "IG2-", "IF-", "IE-", "R-")) or ident in ("BLOCKER", "MAJOR", "MINOR"):
                continue
            if not (ident in ("IN", "OUT") or re.search(r"\d", ident)):
                continue
            kind = kind_of(label, ident)
            assert n == 1, f"§{label}: the head {ident} occurs {n} times; a key must be a head exactly once"
            out.append((label, kind, ident, text))
        if label == "C-unit":
            # C-unit numbers its implementation record's tasks
            # independently of its obligations — its record's T1 is not
            # its obligation T1 — so the record's heads are keys of
            # their own kind, the case H9 names.
            for heading, body in subsections(text):
                if heading.startswith("### Implementation record"):
                    counts = Counter(m for m in HEAD.findall(body) if m.startswith("T") and m[1:].isdigit())
                    for ident, n in counts.items():
                        assert n == 1, f"§C-unit record: the head {ident} occurs {n} times"
                        out.append((label, "record", ident, text))
    text, _ = section_text(doc, VERIFIED)
    bullets = re.findall(r"^- \*\*(.+?)\*\*", text, re.M)
    for i, lead in enumerate(bullets, 1):
        out.append(("Verified", "verified", f"V-{i}", lead))
    return out


def _token(ident: str) -> re.Pattern[str]:
    return re.compile(r"(?<![A-Za-z0-9-])" + re.escape(ident) + r"(?![A-Za-z0-9-])")


def evidence(section: str, ident: str, text: str, declared: set[str]) -> tuple[str, str]:
    if section == "Verified":
        return "its own bullet", "—"
    token = _token(ident)
    records: list[str] = []
    names: list[str] = []
    for heading, body in subsections(text):
        if not heading or not RECORD_HEADING.match(heading):
            continue
        cited = False
        for paragraph in re.split(r"\n\s*\n", body):
            if token.search(paragraph):
                cited = True
                for name in LONG_NAME.findall(paragraph):
                    if name not in names:
                        names.append(name)
        if cited:
            short = re.sub(r"^### ", "", heading)
            short = re.sub(r" — .*$", "", short)
            records.append(short)
    if not records:
        pinning = [h for h, _ in subsections(text) if h.startswith("### Tests that pin")]
        if pinning:
            # The early sections list their pinning facts once for the
            # whole section, not per contract id: the evidence exists
            # and is NOT keyed — say so, and still count it as a key
            # without evidence of its own.
            return f"unevidenced by id — §{section}'s pinning list is not keyed per contract", "—"
        return "unevidenced", "—"
    rendered = [f"`{n}`" if n in declared else f"{n} (not in the tree)" for n in names[:8]]
    if len(names) > 8:
        rendered.append(f"+{len(names) - 8} more")
    return ", ".join(records), ", ".join(rendered) if rendered else "—"


def evidence_table(doc: str, declared: set[str]) -> tuple[str, int]:
    rows = ["| Section | Kind | Id | Discharged by | Pinned by |", "|---|---|---|---|---|"]
    ks = keys(doc)
    seen = Counter((s, k, i) for s, k, i, _ in ks)
    dup = [k for k, n in seen.items() if n > 1]
    assert not dup, f"duplicate keys: {dup}"
    for section, kind, ident, text in ks:
        by, pins = evidence(section, ident, text, declared)
        label = "Verified during implementation" if section == "Verified" else f"§{section}"
        rows.append(f"| {label} | {kind} | {ident} | {by} | {pins} |")
    return NL.join(rows), len(ks)


def _first_clause(head_line: str) -> str:
    m = re.match(r"^\*\*[A-Z]+-\d+ — (.+?)\*\*", head_line, re.S)
    clause = m.group(1) if m else head_line
    clause = re.sub(r"\s+", " ", clause).strip()
    return clause[:140] + ("…" if len(clause) > 140 else "")


def register_index(doc: str, prefix: str, ident_prefix: str, where: str) -> tuple[str, int]:
    text, base = section_text(doc, prefix)
    rows = ["| Id | Head | Recorded |", "|---|---|---|"]
    n = 0
    for m in re.finditer(r"^\*\*(" + ident_prefix + r"-\d+) — ", text, re.M):
        head = text[m.start():m.start() + 400].replace(NL, " ")
        line = doc.count(NL, 0, base + m.start()) + 1
        rows.append(f"| {m.group(1)} | {_first_clause(head)} | {where}, line {line} |")
        n += 1
    return NL.join(rows), n


DECISION_EVIDENCE = {
    "D-1": "Held: §W-G's register closed in PR H (TH-7) — no reading-order, containment, placement or phrasing math in the Windows canvas; the two demotions recorded as divergences (HD-D3).",
    "D-2": "Held: `whereAmI` = Ctrl+Alt+Shift+I and `connectTo` = Ctrl+Alt+C in `chords.json` with delivery evidence; `ChordsJson_IsExactlyTheTablesProjection` pins the projection (TH-9).",
    "D-3": "Settled in 0b-1's second branch: ordinals renumber on delete identically on both hosts (CD-20).",
    "D-4": "Action performed: the mac issue is filed (the mac list below, item 1); Windows offers no Color Marked row — the registry is the parity.",
    "D-5": "Held: the identical placeholder formula on both hosts, host-designated (PR F's resize preset facts).",
    "D-6": "Held: Ctrl+Z / Ctrl+Y on the canvas domain (`CanvasUndo` / `CanvasRedo` rows in `chords.json`; the E2E undo chain, TH-0).",
    "D-7": "Held: Windows Voice Access (\"show numbers\") is the recorded twin in the AT checklist's row 6, Narrator smoke only (TH-10).",
}


def decisions_table(doc: str) -> tuple[str, int]:
    text, _ = section_text(doc, DECISIONS)
    rows = ["| # | Decision | Adopted | Resolution and evidence |", "|---|---|---|---|"]
    n = 0
    for line in text.split(NL):
        m = re.match(r"^\| (D-\d+) \| (.+?) \| (.+?) \|$", line)
        if not m:
            continue
        ident = m.group(1)
        adopted = m.group(3)
        adopted = adopted[:160] + ("…" if len(adopted) > 160 else "")
        rows.append(f"| {ident} | {m.group(2)} | {adopted} | {DECISION_EVIDENCE[ident]} |")
        n += 1
    assert n == 7, f"seven owner decisions expected, found {n}"
    return NL.join(rows), n


def generated(doc: str) -> str:
    declared = declared_names()
    table, count = evidence_table(doc, declared)
    cd, ncd = register_index(doc, DIVERGENCES, "CD", "Recorded divergences")
    cr, ncr = register_index(doc, RISKS, "CR", "Accepted risks")
    assert ncd == 48, f"CD-1…CD-48 expected, found {ncd}"
    assert ncr == 5, f"CR-1…CR-5 expected, found {ncr}"
    decisions, _ = decisions_table(doc)
    return NL.join([
        START,
        "",
        f"**(b) Contract → evidence — {count} keys, one row each, keyed (section, kind, id).** "
        "Generated by `scripts/canvas_reconciliation.py` from the document's own records: "
        "\"discharged by\" lists the record subsections of the key's section that cite the id; "
        "\"pinned by\" the long identifiers those paragraphs backtick, each checked against the "
        "Windows tree. \"unevidenced\" is a key no record cites.",
        "",
        table,
        "",
        f"**(c) The divergence register (CD-1…CD-{ncd}) and the accepted risks (CR-1…CR-{ncr}), as an index.**",
        "",
        cd,
        "",
        cr,
        "",
        "**(d) Owner decisions D-1…D-7, with their resolution and evidence.**",
        "",
        decisions,
        "",
        END,
    ])


def main(argv: list[str]) -> int:
    doc = read_doc()
    block = generated(doc)
    if "--write" in argv:
        s = doc.index(START)
        e = doc.index(END) + len(END)
        doc = doc[:s] + block + doc[e:]
        with open(DOC, "w", encoding="utf-8", newline=NL) as handle:
            handle.write(doc)
        print("reconciliation tables written")
        return 0
    if "--check" in argv:
        s = doc.index(START)
        e = doc.index(END) + len(END)
        if doc[s:e] != block:
            print("the reconciliation tables in the doc differ from the regenerated ones", file=sys.stderr)
            return 1
        print("reconciliation tables verified")
        return 0
    sys.stdout.buffer.write((block + NL).encode("utf-8"))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
