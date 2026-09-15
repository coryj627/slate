#!/usr/bin/env python3
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
"""The W6-2 issue reconciliation's generated tables (#746, PR F, contract F9).

Reads `docs/plans/35_graph_contracts.md` and derives, from the document's
own records, the two tables the reconciliation section carries between
its `<!-- graph-reconciliation:generated:start -->` / `:end -->` markers:

  (b) contract -> evidence, one row per KEY, a key being the triple
      (section, register kind, id).  The keys are every head of the form
      `**ID — ` (or `- **ID — `) inside a PR section's KEY-BEARING
      subsections — contracts (`X-n`, and F's `Fn`), decisions (`XD-n`),
      divergences (`X-Dn`), risks (`XR-n`), owner questions (`XD-Qn`)
      and the design Terms of C, D and E (`Term Xn`); the task records
      (`TG*-n`), the round ledgers and the post-implementation passes are
      EVIDENCE, not keys.  A row names the record subsections of the
      same section that cite the id ("discharged by") and the long
      PascalCase identifiers those citing paragraphs backtick ("pinned
      by"), each checked against the Windows tree — a name that no
      longer exists is rendered plain, marked "(not in the tree)", never
      silently dropped.  A key nothing cites reads "unevidenced" rather
      than being omitted.
  (c) the divergence registers (0a-D … FD-D) and the risk registers
      (0aR … FR) as one index each: id, the head's first clause, where
      recorded.

Usage:
  python scripts/graph_reconciliation.py           # print the tables
  python scripts/graph_reconciliation.py --write   # splice them into the doc
  python scripts/graph_reconciliation.py --check   # exit 1 unless the doc
                                                   # carries exactly the
                                                   # regenerated tables

Every run asserts the counts: each key's head occurs exactly once in its
section, the (b) table has exactly one row per key, no two keys share a
row, and the key total equals the pin.  `--check` is what
GraphReconciliationCensus mirrors.
"""

from __future__ import annotations

import os
import re
import sys
from collections import Counter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
DOC = os.path.join(ROOT, "docs", "plans", "35_graph_contracts.md")
SHELL = os.path.join(ROOT, "apps", "slate-windows")
# The pinned total: a document that lost a head does not regenerate a
# smaller table and bless it; it fails here until the pin is bumped on
# purpose. GraphReconciliationCensus carries the same constant.
EXPECTED_KEYS = 519
START = "<!-- graph-reconciliation:generated:start -->"
END = "<!-- graph-reconciliation:generated:end -->"
NL = "\n"

# The PR sections whose heads are keys, by the label the key carries.
PR_SECTIONS = [
    ("0a", "## PR 0a — "),
    ("0b", "## PR 0b — "),
    ("A", "## PR A — "),
    ("B", "## PR B — "),
    ("B2", "## PR B2 — "),
    ("C", "## PR C — "),
    ("D", "## PR D — "),
    ("E", "## PR E — "),
    ("F", "## PR F — "),
]

# (section label, divergence id prefix, risk id prefix)
REGISTERS = [
    ("0a", "0a-D", "0aR-"),
    ("0b", "0b-D", "0bR-"),
    ("A", "A-D", "AR-"),
    ("B", "B-D", None),
    ("B2", "B2-D", None),
    ("C", "C-D", "CR-"),
    ("D", "D-D", "DR-"),
    ("E", "E-D", "ER-"),
    ("F", "FD-D", "FR-"),
]

HEAD = re.compile(r"^(?:- )?\*\*((?:Term )?[0-9A-Za-z][0-9A-Za-z-]*?) — ", re.M)
# Record subsections: the task records, the pinning-test lists, the
# post-implementation passes, the close-outs. Round ledgers, freezes and
# the design passes' narrative discuss contracts; the design passes'
# Terms are keys (they live in key-bearing subsections), the ledgers are
# not evidence and are not scanned.
RECORD_HEADING = re.compile(
    r"^### (?:TG[0-9A-Za-z]*-\d+[a-z]? — |Task loop — records|Tests that pin"
    r"|Post-implementation passes|.*close-out|Close-out)",
)
NOT_KEY_HEADING = re.compile(
    r"^### (?:Round \d|THE FREEZE|The rounds — the ledger|What stands today|The mac, traced"
    r"|The mac's re-root, traced|Mac details recorded)",
)
LONG_NAME = re.compile(r"`([A-Z][A-Za-z0-9_]{14,})`")


def kind_of(ident: str) -> str:
    if ident.startswith("Term "):
        return "term"
    m = re.match(r"^(0a|0b|B2|[A-Z])(D?)-(D?)(Q?)(\d+)[a-z]?$", ident)
    if m is None:
        # F's contracts are F1…F10 (the canvas's H1…H10 form).
        if re.match(r"^F\d+$", ident):
            return "contract"
        return "head"
    _, dec, div, q, _ = m.groups()
    if q:
        return "question"
    if div:
        return "divergence"
    if dec:
        return "decision"
    return "contract"


def risk_kind(ident: str) -> bool:
    return re.match(r"^(0a|0b|[A-Z])R-\d+$", ident) is not None


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
    read syntactically with comments stripped; GraphContractsCitationCensus
    remains the authority for the document's own citations."""
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
    """The section minus its record and ledger subsections: a head is a
    key where it is STATED; a record or a round ledger that quotes the
    head is evidence or discussion, not a second key."""
    kept = []
    for heading, body in subsections(text):
        if heading and (RECORD_HEADING.match(heading) or NOT_KEY_HEADING.match(heading)):
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
            if not re.search(r"\d", ident):
                continue
            if ident.startswith(("IG", "IP", "TG", "R-")) or ident in ("BLOCKER", "MAJOR", "MINOR"):
                continue
            kind = "risk" if risk_kind(ident) else kind_of(ident)
            if kind == "head":
                continue
            assert n == 1, f"§{label}: the head {ident} occurs {n} times; a key must be a head exactly once"
            out.append((label, kind, ident, text))
    return out


def _token(ident: str) -> re.Pattern[str]:
    return re.compile(r"(?<![A-Za-z0-9-])" + re.escape(ident) + r"(?![A-Za-z0-9-])")


def evidence(section: str, ident: str, text: str, declared: set[str]) -> tuple[str, str]:
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
            return f"unevidenced by id — §{section}'s pinning list is not keyed per id", "—"
        return "unevidenced", "—"
    rendered = [f"`{n}`" if n in declared else f"{n} (not in the tree)" for n in names[:8]]
    if len(names) > 8:
        rendered.append(f"+{len(names) - 8} more")
    return ", ".join(records), ", ".join(rendered) if rendered else "—"


def evidence_table(doc: str, declared: set[str]) -> tuple[str, int]:
    rows = ["| Section | Kind | Id | Discharged by | Pinned by |", "|---|---|---|---|---|"]
    ks = keys(doc)
    assert len(ks) == EXPECTED_KEYS, f"{len(ks)} keys derived; the pin says {EXPECTED_KEYS} — bump the pin deliberately or restore the head"
    seen = Counter((s, k, i) for s, k, i, _ in ks)
    dup = [k for k, n in seen.items() if n > 1]
    assert not dup, f"duplicate keys: {dup}"
    for section, kind, ident, text in ks:
        by, pins = evidence(section, ident, text, declared)
        rows.append(f"| §{section} | {kind} | {ident} | {by} | {pins} |")
    return NL.join(rows), len(ks)


def _first_clause(head_line: str) -> str:
    m = re.match(r"^(?:- )?\*\*[0-9A-Za-z-]+ — (.+?)\*\*", head_line, re.S)
    clause = m.group(1) if m else head_line
    clause = re.sub(r"\s+", " ", clause).strip()
    return clause[:140] + ("…" if len(clause) > 140 else "")


def register_index(doc: str, ident_prefix: str) -> list[str]:
    rows: list[str] = []
    for label, prefix in PR_SECTIONS:
        text, base = section_text(doc, prefix)
        kb = key_bearing(text)
        for m in re.finditer(r"^(?:- )?\*\*(" + re.escape(ident_prefix) + r"\d+) — ", kb, re.M):
            head = kb[m.start():m.start() + 400].replace(NL, " ")
            # the line in the document: find the head's first occurrence in the section
            at = text.find(m.group(0))
            line = doc.count(NL, 0, base + at) + 1 if at >= 0 else 0
            rows.append(f"| {m.group(1)} | {_first_clause(head)} | §{label}, line {line} |")
    return rows


def generated(doc: str) -> str:
    declared = declared_names()
    table, count = evidence_table(doc, declared)
    div_rows = ["| Id | Head | Recorded |", "|---|---|---|"]
    risk_rows = ["| Id | Head | Recorded |", "|---|---|---|"]
    ndiv = nrisk = 0
    for _, dprefix, rprefix in REGISTERS:
        rows = register_index(doc, dprefix)
        ndiv += len(rows)
        div_rows.extend(rows)
        if rprefix:
            rows = register_index(doc, rprefix)
            nrisk += len(rows)
            risk_rows.extend(rows)
    assert ndiv == sum(1 for _, k, _, _ in keys(doc) if k == "divergence"), "the divergence index and the keys disagree"
    assert nrisk == sum(1 for _, k, _, _ in keys(doc) if k == "risk"), "the risk index and the keys disagree"
    return NL.join([
        START,
        "",
        f"**(b) Contract → evidence — {count} keys, one row each, keyed (section, kind, id).** "
        "Generated by `scripts/graph_reconciliation.py` from the document's own records: "
        "\"discharged by\" lists the record subsections of the key's section that cite the id; "
        "\"pinned by\" the long identifiers those paragraphs backtick, each checked against the "
        "Windows tree. \"unevidenced\" is a key no record cites.",
        "",
        table,
        "",
        f"**(c) The divergence registers ({ndiv} rows, 0a-D … FD-D) and the risk registers ({nrisk} rows, 0aR … FR), as an index.**",
        "",
        NL.join(div_rows),
        "",
        NL.join(risk_rows),
        "",
        END,
    ])


def block_span(doc: str) -> tuple[int, int]:
    """The generated block's span: the markers at LINE START — the F9 prose
    quotes the marker text in backticks, which is not the block (TGF-8)."""
    s = doc.index(NL + START) + 1
    e = doc.index(NL + END, s) + 1 + len(END)
    return s, e


def main(argv: list[str]) -> int:
    doc = read_doc()
    block = generated(doc)
    if "--write" in argv:
        s, e = block_span(doc)
        doc = doc[:s] + block + doc[e:]
        with open(DOC, "w", encoding="utf-8", newline=NL) as handle:
            handle.write(doc)
        print("reconciliation tables written")
        return 0
    if "--check" in argv:
        s, e = block_span(doc)
        if doc[s:e] != block:
            print("the reconciliation tables in the doc differ from the regenerated ones", file=sys.stderr)
            return 1
        print("reconciliation tables verified")
        return 0
    sys.stdout.buffer.write((block + NL).encode("utf-8"))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
