#!/usr/bin/env python3
"""W6-2 §F (F4; the canvas ledger's twin, W6-1 §H TH-4): the graph trigger ledger.

Derives the STRUCTURAL key set of the graph announcement vocabulary from
the generated C# binding — every `GraphA11yEvent` arm, with the two
discriminant families (`GraphStatusNote`, `GraphBlockedReason`) expanded
to their arms, because each arm selects a sentence and has a trigger of
its own; the payload qualifiers (the verbosity, `GraphPresetOutcome`,
`GraphWhereAmISelection`, the row copy's kind, the zoom's fit flag) do
not split a key, being data one trigger carries — and, for every key,
finds the mac and the Windows construction sites (file#member) and the
Windows facts that construct the same key.

    python scripts/graph_trigger_ledger.py            # the markdown table
    python scripts/graph_trigger_ledger.py --report   # coverage summary

The table is pasted into docs/plans/35_graph_contracts.md (§F, "The
trigger ledger (F4)") and `GraphTriggerParityCensus` validates it
against the sources; the two are kept in step by regenerating here.
"""
from __future__ import annotations

import os
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
BINDING = REPO / "apps/slate-windows/src/SlateUniffi/generated/slate_uniffi.cs"
WIN_SRC = REPO / "apps/slate-windows/src/SlateWindows"
WIN_TESTS = REPO / "apps/slate-windows/tests"
MAC_SRC = REPO / "apps/slate-mac/Sources"

FAMILIES = {
    "GraphStatus": ("GraphStatusNote", "note", "record"),
    "GraphBlocked": ("GraphBlockedReason", "reason", "record"),
}

# Keys a platform never fires, by owner-recorded designation (§F FD-8 and
# F4's list). A key listed here needs no site on that platform.
DESIGNATED = {
    "windows": {
        "GraphStatus/AlreadyOpen": "the mac's only site is Duplicate Tab (AppState.swift); the Windows graph tab is excluded from Duplicate since W1 (A-13) and rule L speaks Opened alone on the effective-active tab — no trigger exists (FD-8, owed to an owner decision)",
    },
    "mac": {
        "GraphStatus/NoConnections": "0a-D3: the mac's panel shows the text (ConnectionsPanel.swift) and posts nothing; Windows posts it from ConnectionsLeafViewModel",
        "GraphStatus/LoadingConnections": "0a-D3: the mac's panel shows the text (ConnectionsPanel.swift) and posts nothing; Windows posts it from ConnectionsLeafViewModel",
    },
}

# Each key's ROLE, from 0a's site manifest (F4, FD-11): posted keys are
# constructed for the announcer; label keys are rendered into a name or
# custom content and never posted (C1, C2).
LABEL_KEYS = {
    "GraphTierSummary": "static label — the tier-B element's name (0a manifest C2)",
    "GraphNeighborsContent": "custom content — the node's Connects-to content (0a manifest C1)",
}

# The F1 fact that OBSERVES each posted key delivered through the
# production seam on Windows (F4, IGZ-7); a failure arm no healthy
# session reaches is site-only, pinned by the unit fact that injects the
# failure. Label keys are observed by the grammar fact's peer reads.
OBSERVED = {
    "GraphRow": "TheDiagramReproducesTheGoldensSixtiethTickThenConvergesStepsZoomsAndReadsBack",
    "GraphReRooted": "TheConnectionsLeafWalksReRootsAndCreatesAgainstTheGolden",
    "GraphSnapshotSummary": "OpenGraphVaultExposesTheTableTheSummaryAndTheSortAgainstTheGolden",
    "GraphNeighborhoodSummary": "TheConnectionsLeafWalksReRootsAndCreatesAgainstTheGolden",
    "GraphPreset": "OpenGraphVaultExposesTheTableTheSummaryAndTheSortAgainstTheGolden",
    "GraphFilterCount": "OpenGraphVaultExposesTheTableTheSummaryAndTheSortAgainstTheGolden",
    "GraphForceValue": "TheConfigRoundTripsThroughTheInspectorAndTheStore",
    "GraphLayoutSettled": "TheDiagramReproducesTheGoldensSixtiethTickThenConvergesStepsZoomsAndReadsBack",
    "GraphPinned": "TheDiagramReproducesTheGoldensSixtiethTickThenConvergesStepsZoomsAndReadsBack",
    "GraphZoom": "TheDiagramReproducesTheGoldensSixtiethTickThenConvergesStepsZoomsAndReadsBack",
    "GraphMode": "TheDiagramReproducesTheGoldensSixtiethTickThenConvergesStepsZoomsAndReadsBack",
    "GraphWhereAmI": "OpenGraphVaultExposesTheTableTheSummaryAndTheSortAgainstTheGolden",
    "GraphTierEntered": "AnnouncementGrammarConformsPerVerbosity",
    "GraphTierSummary": "AnnouncementGrammarConformsPerVerbosity (the peer's Name)",
    "GraphNeighborsContent": "AnnouncementGrammarConformsPerVerbosity (the peer's HelpText)",
    "GraphStatus/Opened": "OpenGraphVaultExposesTheTableTheSummaryAndTheSortAgainstTheGolden",
    "GraphStatus/AlreadyOpen": "— (Windows-designated)",
    "GraphStatus/ConnectionsPanel": "TheConnectionsLeafWalksReRootsAndCreatesAgainstTheGolden",
    "GraphStatus/NoteCreated": "TheConnectionsLeafWalksReRootsAndCreatesAgainstTheGolden",
    "GraphStatus/NoConnections": "TheConnectionsLeafWalksReRootsAndCreatesAgainstTheGolden",
    "GraphStatus/LoadingConnections": "TheConnectionsLeafWalksReRootsAndCreatesAgainstTheGolden",
    "GraphBlocked/LoadFailed": "site-only: `GraphDocumentTests.cs#LoadFailed` injects the failure",
    "GraphBlocked/ConnectionsLoadFailed": "site-only: `ConnectionsLeafTests.Model.Composed.cs#FailureLine` injects the failure",
    "GraphBlocked/NoteCreateFailed": "TheConnectionsLeafWalksReRootsAndCreatesAgainstTheGolden (the Exists arm)",
}


def lower_camel(name: str) -> str:
    return name[0].lower() + name[1:]


def family_arms(binding: str, name: str) -> list[str]:
    i = binding.index(f"public record {name} {{")
    j = binding.index("\n}\n", i)
    return re.findall(r"public record (\w+)\s*(?:\([^)]*\))?\s*: " + name, binding[i:j])


def enum_arms(binding: str, name: str) -> list[str]:
    m = re.search(r"public enum " + name + r": int \{([^}]*)\}", binding)
    assert m, name
    return [x.strip() for x in m.group(1).split(",") if x.strip()]


def keys() -> list[tuple[str, str | None]]:
    binding = BINDING.read_text(encoding="utf-8")
    out: list[tuple[str, str | None]] = []
    for arm in family_arms(binding, "GraphA11yEvent"):
        if arm in FAMILIES:
            fam, _, kind = FAMILIES[arm]
            arms = family_arms(binding, fam) if kind == "record" else enum_arms(binding, fam)
            out.extend((arm, inner) for inner in arms)
        else:
            out.append((arm, None))
    return out


MEMBER_CS = re.compile(
    r"^\s+(?:public|internal|private|protected)\s+(?:static\s+|override\s+|async\s+|virtual\s+|sealed\s+|new\s+|readonly\s+)*"
    r"(?:[\w<>?,\[\]\.]+\s+)?(\w+)\s*(?:\(|=>|=|\{|$)")
# A Swift MEMBER is declared at the type's own indentation (four spaces
# inside `extension X {` / `final class X {`); a `let`/`var` deeper than
# that is a local of the member above it, never a site (review round 1,
# IH-56).
MEMBER_SWIFT = re.compile(r"^ {0,4}(?:@\w+\s+)*(?:private\s+|fileprivate\s+|internal\s+|public\s+|open\s+)?"
                          r"(?:static\s+|final\s+|override\s+)*(?:func|var|let)\s+(\w+)")
FACT_CS = re.compile(r"^\s+public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\s*\(")


def enclosing(lines: list[str], index: int, pattern: re.Pattern[str]) -> str | None:
    for back in range(index, -1, -1):
        m = pattern.match(lines[back])
        if m and m.group(1) not in ("if", "for", "foreach", "while", "switch", "return", "new", "using", "catch", "lock"):
            return m.group(1)
    return None


def scan(root: Path, suffix: str, needle: re.Pattern[str], member: re.Pattern[str]) -> dict[str, set[str]]:
    found: dict[str, set[str]] = {}
    for path in sorted(root.rglob(f"*{suffix}")):
        if "/obj/" in path.as_posix() or "/bin/" in path.as_posix() or "generated" in path.as_posix():
            continue
        lines = path.read_text(encoding="utf-8", errors="replace").split("\n")
        for i, line in enumerate(lines):
            if line.lstrip().startswith("//"):
                continue
            for m in needle.finditer(line):
                key = m.group("key")
                site = f"{path.name}#{enclosing(lines, i, member) or '?'}"
                found.setdefault(key, set()).add(site)
    return found


def windows_needle() -> re.Pattern[str]:
    outer = r"GraphA11yEvent\.(?P<outer>\w+)"
    nested = r"(?:GraphStatusNote|GraphBlockedReason)\.(?P<inner>\w+)"
    return re.compile(r"(?P<key>" + outer + "|" + nested + ")")


def mac_scan() -> dict[str, set[str]]:
    """Swift constructions, over the whole text: a case's arguments may
    span lines (`.graphBlocked(\n reason: .loadFailed(…)`), a
    parameterless case has no parentheses (`.graphLayoutSettled`), and
    a discriminant may arrive as a variable, whose arms are then read
    from the file that builds it."""
    found: dict[str, set[str]] = {}
    families = {v[0]: k for k, v in FAMILIES.items()}
    inner_arms = {}
    binding = BINDING.read_text(encoding="utf-8")
    for outer, (fam, _, kind) in FAMILIES.items():
        arms = family_arms(binding, fam) if kind == "record" else enum_arms(binding, fam)
        inner_arms[outer] = arms
    for path in sorted(MAC_SRC.rglob("*.swift")):
        # Generated codec cases spell every event but are not host triggers.
        # Their local presence must not change the committed evidence ledger.
        if path.name == "slate_uniffi.swift" or "generated" in path.relative_to(MAC_SRC).parts:
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        lines = text.split("\n")
        offsets = []
        pos = 0
        for line in lines:
            offsets.append(pos)
            pos += len(line) + 1

        def line_of(index: int) -> int:
            lo, hi = 0, len(offsets) - 1
            while lo < hi:
                mid = (lo + hi + 1) // 2
                if offsets[mid] <= index:
                    lo = mid
                else:
                    hi = mid - 1
            return lo

        def add(key: str, index: int) -> None:
            site = f"{path.name}#{enclosing(lines, line_of(index), MEMBER_SWIFT) or '?'}"
            found.setdefault(key, set()).add(site)

        def builds_event(index: int) -> bool:
            """An EVENT construction (F4): the spelling sits inside an
            `announce(` call — the call may open several lines above —
            or inside a member whose declaration names GraphA11yEvent
            (an event-returning helper such as `graphPresetEvent`); a
            command id (`SlateCommandID.graphWhereAmI`) or another
            enum's `.opened` is not a site."""
            before = text[max(0, index - 300):index].rsplit("\n\n", 1)[-1]
            if "announce(" in before or "a11yRender(" in before:
                return True
            line = line_of(index)
            for back in range(line, -1, -1):
                d = MEMBER_SWIFT.match(lines[back])
                if d:
                    return "GraphA11yEvent" in lines[back]
            return False

        for m in re.finditer(r"\.graph([A-Z]\w*)(\(|\b)", text):
            if not builds_event(m.start()):
                continue
            outer = "Graph" + m.group(1)
            if outer in FAMILIES:
                # the discriminant: a literal arm within the call, else a variable
                span = text[m.end():m.end() + 400]
                arm = re.match(r"\s*(?:note|reason):\s*\.(\w+)", span)
                if arm:
                    add(f"{outer}/{arm.group(1)[0].upper()}{arm.group(1)[1:]}", m.start())
                else:
                    add(outer, m.start())
            else:
                add(outer, m.start())
        # discriminant arms built away from the announce call (a mapping
        # switch or a stored refusal): each family's arms by name
        for outer, arms in inner_arms.items():
            fam = FAMILIES[outer][0]
            if fam not in text and f".graph{outer[5:]}" not in text:
                continue
            # only where a discriminant is bound to a VARIABLE (`note: x`);
            # a file whose every discriminant is a literal arm is fully
            # read by the call scan above (the graph sources today).
            if not re.search(r"\.graph" + re.escape(outer[5:]) + r"\(\s*(?:note|reason):\s*[A-Za-z_]", text):
                continue
            for arm in arms:
                camel = lower_camel(arm)
                for m in re.finditer(r"\." + re.escape(camel) + r"\b(?!\()", text):
                    add(f"{outer}/{arm}", m.start())
    return found


def normalise_windows(found: dict[str, set[str]]) -> dict[str, set[str]]:
    out: dict[str, set[str]] = {}
    for key, sites in found.items():
        if key.startswith("GraphA11yEvent."):
            out.setdefault(key.split(".", 1)[1], set()).update(sites)
        else:
            fam, arm = key.split(".", 1)
            outer = next(o for o, (f, _, _) in FAMILIES.items() if f == fam)
            out.setdefault(f"{outer}/{arm}", set()).update(sites)
    return out


def normalise_mac(found: dict[str, set[str]]) -> dict[str, set[str]]:
    out: dict[str, set[str]] = {}
    for key, sites in found.items():
        m = re.match(r"\.graph(\w+)\((?:note|reason): \.(\w+)", key)
        if m:
            outer = "Graph" + m.group(1)
            inner = m.group(2)[0].upper() + m.group(2)[1:]
            out.setdefault(f"{outer}/{inner}", set()).update(sites)
        else:
            m2 = re.match(r"\.graph(\w+)\(", key)
            if m2:
                out.setdefault("Graph" + m2.group(1), set()).update(sites)
    return out


def key_name(outer: str, inner: str | None) -> str:
    return f"{outer}/{inner}" if inner else outer


def build() -> tuple[list[tuple[str, str | None]], dict, dict, dict]:
    win = normalise_windows(scan(WIN_SRC, ".cs", windows_needle(), MEMBER_CS))
    # a "fact" is any test-tree member that constructs the key: a [Fact]
    # method, a theory's data, a helper, or the corpus mirror's field —
    # each is asserted through the test that reads it
    facts = normalise_windows(scan(WIN_TESTS, ".cs", windows_needle(), MEMBER_CS))
    mac = mac_scan()
    return keys(), win, mac, facts


def sites(found: dict[str, set[str]], name: str, limit: int = 3) -> str:
    items = sorted(found.get(name, ()))
    if not items:
        return "—"
    shown = ", ".join(f"`{s}`" for s in items[:limit])
    return shown + (f" (+{len(items) - limit})" if len(items) > limit else "")


def table() -> str:
    ks, win, mac, facts = build()
    rows = ["| Key | Role | mac site(s) | Windows site(s) | Windows fact(s) | Observed end to end by | Note |", "|---|---|---|---|---|---|---|"]
    for outer, inner in ks:
        name = key_name(outer, inner)
        role = "label" if name in LABEL_KEYS else "posted"
        note = LABEL_KEYS.get(name, "")
        if name not in win:
            note = (note + "; " if note else "") + (("designated: " + DESIGNATED["windows"][name]) if name in DESIGNATED["windows"] else "UNCONSUMED on Windows")
        if name not in mac and name in DESIGNATED["mac"]:
            note = (note + "; " if note else "") + "mac designated: " + DESIGNATED["mac"][name]
        elif name not in mac and not note:
            note = "no mac site found"
        rows.append(f"| `{name}` | {role} | {sites(mac, name)} | {sites(win, name)} | {sites(facts, name)} | {OBSERVED[name]} | {note} |")
    return "\n".join(rows)


def report() -> int:
    ks, win, mac, facts = build()
    names = [key_name(o, i) for o, i in ks]
    print(f"keys: {len(names)} (outer arms {len(set(o for o, _ in ks))})")
    missing_win = [n for n in names if n not in win and n not in DESIGNATED["windows"]]
    missing_fact = [n for n in names if n not in facts and n not in DESIGNATED["windows"]]
    missing_mac = [n for n in names if n not in mac and n not in DESIGNATED["mac"]]
    print(f"no Windows site: {len(missing_win)} -> {missing_win}")
    print(f"no Windows fact: {len(missing_fact)} -> {missing_fact}")
    print(f"no mac site: {len(missing_mac)} -> {missing_mac}")
    missing_observed = [n for n in names if n not in OBSERVED]
    print(f"no observation entry: {len(missing_observed)} -> {missing_observed}")
    return 1 if (missing_win or missing_fact or missing_mac or missing_observed) else 0


if __name__ == "__main__":
    if "--report" in sys.argv:
        sys.exit(report())
    print(table())
