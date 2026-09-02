#!/usr/bin/env python3
"""W6-1 §H TH-4 (H4, IH-37, IH-38): the canvas trigger ledger.

Derives the STRUCTURAL key set of the canvas announcement vocabulary from
the generated C# binding — every `CanvasA11yEvent` arm, with the four
discriminant families (`CanvasStatusNote`, `CanvasBlockedReason`,
`CanvasFailedAction`, `CanvasMutationRefusal`) expanded to their arms,
because each arm selects a sentence and has a trigger of its own; the
payload qualifiers (`CanvasZoomContext`, `CanvasResizePreset`,
`CanvasOverlapTransition`, the verbosity) do not split a key, being
data one trigger carries — and, for every key, finds the mac and the
Windows construction sites (file#member) and the Windows facts that
construct the same key.

    python scripts/canvas_trigger_ledger.py            # the markdown table
    python scripts/canvas_trigger_ledger.py --report   # coverage summary

The table is pasted into docs/plans/34_canvas_contracts.md (§H, "The
trigger ledger") and `CanvasTriggerParityCensus` validates it against
the sources; the two are kept in step by regenerating here.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
BINDING = REPO / "apps/slate-windows/src/SlateUniffi/generated/slate_uniffi.cs"
WIN_SRC = REPO / "apps/slate-windows/src/SlateWindows"
WIN_TESTS = REPO / "apps/slate-windows/tests"
MAC_SRC = REPO / "apps/slate-mac/Sources"

FAMILIES = {
    "CanvasStatus": ("CanvasStatusNote", "note", "record"),
    "CanvasBlocked": ("CanvasBlockedReason", "reason", "record"),
    "CanvasActionFailed": ("CanvasFailedAction", "action", "enum"),
    "CanvasMutationRefused": ("CanvasMutationRefusal", "refusal", "enum"),
}

# Keys a platform never fires, by owner-recorded designation (§H HD-D3 and
# the section's list). A key listed here needs no site on that platform.
DESIGNATED = {
    "windows": {
        "CanvasUndoMenuTitle": "no Edit menu on Windows (HD-D3); the history verbs' palette rows carry the registry's static labels",
        "CanvasHistoryQuarantinedTitle": "no Edit menu on Windows (HD-D3)",
        "CanvasMutationRefused/Reopening": "Windows retargets synchronously (§A): no pending-preparation window exists for the refusal to arise in; the read-side Reopening status carries the state",
    },
    "mac": {
        "CanvasViewportNoPane": "Windows-only (§D ID-7): mac's canvas always has a pane to address",
        "CanvasHistoryQuarantinedTitle": "mac's undo stack has no basis quarantine (TE-2/TE-4 are Windows's mechanism) — with no Edit menu on Windows either, an orphaned label arm, owed to an owner decision",
        "CanvasBlocked/UndoQuarantined": "mac's undo stack has no basis quarantine (TE-2/TE-4 are Windows's); mac's `.quarantine` is BatchTrash's path quarantine, another mechanism",
        "CanvasBlocked/RedoQuarantined": "mac's undo stack has no basis quarantine (TE-2/TE-4 are Windows's)",
        "CanvasMutationRefused/RefreshPending": "Windows-only (§E TE-0): the landed-but-unindexed refresh state has no mac twin",
    },
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
    for arm in family_arms(binding, "CanvasA11yEvent"):
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
MEMBER_SWIFT = re.compile(r"^\s*(?:@\w+\s+)*(?:private\s+|fileprivate\s+|internal\s+|public\s+|open\s+)?"
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
    outer = r"CanvasA11yEvent\.(?P<outer>\w+)"
    nested = r"(?:CanvasStatusNote|CanvasBlockedReason|CanvasFailedAction|CanvasMutationRefusal)\.(?P<inner>\w+)"
    return re.compile(r"(?P<key>" + outer + "|" + nested + ")")


def mac_scan() -> dict[str, set[str]]:
    """Swift constructions, over the whole text: a case's arguments may
    span lines (`.canvasActionFailed(\n action: .newCard, …`), a
    parameterless case has no parentheses (`.canvasSaveConflict`), and
    a discriminant may arrive as a variable (`reason: refusal`), whose
    arms are then read from the file that builds it."""
    found: dict[str, set[str]] = {}
    families = {v[0]: k for k, v in FAMILIES.items()}
    inner_arms = {}
    binding = BINDING.read_text(encoding="utf-8")
    for outer, (fam, _, kind) in FAMILIES.items():
        arms = family_arms(binding, fam) if kind == "record" else enum_arms(binding, fam)
        inner_arms[outer] = arms
    for path in sorted(MAC_SRC.rglob("*.swift")):
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

        for m in re.finditer(r"\.canvas([A-Z]\w*)(\(|\b)", text):
            outer = "Canvas" + m.group(1)
            if outer in FAMILIES:
                # the discriminant: a literal arm within the call, else a variable
                span = text[m.end():m.end() + 400]
                arm = re.match(r"\s*(?:note|reason|action|refusal):\s*\.(\w+)", span)
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
            if fam not in text and f".canvas{outer[6:]}" not in text:
                continue
            for arm in arms:
                camel = lower_camel(arm)
                for m in re.finditer(r"\." + re.escape(camel) + r"\b(?!\()", text):
                    add(f"{outer}/{arm}", m.start())
    return found


def normalise_windows(found: dict[str, set[str]]) -> dict[str, set[str]]:
    out: dict[str, set[str]] = {}
    for key, sites in found.items():
        if key.startswith("CanvasA11yEvent."):
            out.setdefault(key.split(".", 1)[1], set()).update(sites)
        else:
            fam, arm = key.split(".", 1)
            outer = next(o for o, (f, _, _) in FAMILIES.items() if f == fam)
            out.setdefault(f"{outer}/{arm}", set()).update(sites)
    return out


def normalise_mac(found: dict[str, set[str]]) -> dict[str, set[str]]:
    out: dict[str, set[str]] = {}
    for key, sites in found.items():
        m = re.match(r"\.canvas(\w+)\((?:note|reason|action|refusal): \.(\w+)", key)
        if m:
            outer = "Canvas" + m.group(1)
            inner = m.group(2)[0].upper() + m.group(2)[1:]
            out.setdefault(f"{outer}/{inner}", set()).update(sites)
        else:
            m2 = re.match(r"\.canvas(\w+)\(", key)
            if m2:
                out.setdefault("Canvas" + m2.group(1), set()).update(sites)
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
    rows = ["| Key | mac site(s) | Windows site(s) | Windows fact(s) | Note |", "|---|---|---|---|---|"]
    for outer, inner in ks:
        name = key_name(outer, inner)
        note = ""
        if name not in win:
            note = ("designated: " + DESIGNATED["windows"][name]) if name in DESIGNATED["windows"] else "UNCONSUMED on Windows"
        if name not in mac and name in DESIGNATED["mac"]:
            note = (note + "; " if note else "") + "mac designated: " + DESIGNATED["mac"][name]
        elif name not in mac and not note:
            note = "no mac site found"
        rows.append(f"| `{name}` | {sites(mac, name)} | {sites(win, name)} | {sites(facts, name)} | {note} |")
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
    return 0


if __name__ == "__main__":
    if "--report" in sys.argv:
        sys.exit(report())
    print(table())
