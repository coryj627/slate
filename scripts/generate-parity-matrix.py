#!/usr/bin/env python3
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
"""Generate docs/plans/18_windows_port/parity_matrix.md (W0-4, #716).

The §W-F row-level checklist every W issue burns down: an inventory pass
over the shipped mac app producing one row per surface/capability with
its consuming W issue. Re-runnable by design — matrix drift = re-run,
diff, re-triage (program §moving-target).

Sources (all mechanical, all drift-test- or CI-enforced in the mac app):
- Command inventory: ``SlateCommandID`` statics (the stability-contract
  catalog; ``SlateCommandsTests`` asserts every id resolves to a
  registered ``Command``), enriched from the ``register(...)`` blocks
  (label, registered section, ``hotkey:``) and the definition-table
  chord switches (``case SlateCommandID.x: hotkey = "…"``). Spoken
  hotkeys are derived from chords by mirroring ``HotkeySpoken.spoken``
  (the canonical per-character glyph walk).
- Leaf inventory: the authoritative ``enum Leaf: CaseIterable`` registry
  in ``Workspace/RightPaneView.swift`` — one row per shipped leaf.
- Settings tabs: ``*SettingsTab()`` uses in ``SettingsView.swift``.
- Help docs: ``docs/help/*.md``.
- CLI verbs: ``slate-cli --help`` (run live when cargo is available,
  else parsed from the clap enum).
- File-type handlers: pinned from program decision 15 (the SwiftPM mac
  app declares no CFBundleDocumentTypes; Windows registration is W8-3).

Fail-fast contract: generation aborts when a ``hotkey:`` literal is not
attributed to a command, when a command id or leaf case has no W-issue
mapping, or when a registered section is unknown — a silent drop would
let §W-F report parity that was never inventoried.

Deviation from the w0_spec §W0-4 wording ("driven via the mac app/test
target"): this generator reads the drift-test-enforced *source catalog*
instead of a runtime registry dump — recorded as gap-analysis row G16.

Usage: python3 scripts/generate-parity-matrix.py  (from the repo root)
"""

from __future__ import annotations

import datetime
import re
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
COMMANDS_SWIFT = REPO / "apps/slate-mac/Sources/SlateMac/SlateCommands.swift"
SETTINGS_SWIFT = REPO / "apps/slate-mac/Sources/SlateMac/SettingsView.swift"
LEAF_SWIFT = REPO / "apps/slate-mac/Sources/SlateMac/Workspace/RightPaneView.swift"
HELP_DIR = REPO / "docs/help"
OUT = REPO / "docs/plans/18_windows_port/parity_matrix.md"

# Registered-section (the `section:` field of the actual registration,
# not the id namespace) -> consuming W issue. Palette/chords themselves
# are W5-1 (#741); each command's *capability* lands with its surface.
SECTION_ISSUE = {
    "sidebar": "#721 (W1-2)",
    "file": "#744 (W5-4)",
    "view": "#722 (W1-3)",
    "workspace": "#722 (W1-3)",
    "editor": "#725 (W2-3)",
    "search": "#742 (W5-2)",
    "tasks": "#735 (W4-3)",
    "properties": "#736 (W4-4)",
    "citations": "#737 (W4-5)",
    "history": "#739 (W4-7)",
    "canvas": "#745 (W6-1)",
    "graph": "#746 (W6-2)",
    "bases": "#738 (W4-6)",
    "app": "#720 (W1-1)",
    "vault": "#720 (W1-1)",
    "help": "#756 (W8-6)",
    "settings": "#751 (W8-1)",
}

# Cross-namespace capabilities: the id namespace/registered section is
# NOT the owning surface. Exhaustive by review; a wrong consumer here
# lets the real owner close without burning its rows down.
ID_ISSUE_OVERRIDES = {
    "slate.workspace.quickOpen": "#723 (W1-4)",
    "slate.view.toggleSearch": "#742 (W5-2)",
    "slate.editor.findInNote": "#742 (W5-2)",
    "slate.editor.save": "#724 (W2-1)",
    "slate.editor.toggleViewMode": "#728 (W3-1)",
    "slate.editor.addProperty": "#736 (W4-4)",
    "slate.editor.bulkRenameProperties": "#736 (W4-4)",
    "slate.editor.togglePropertiesSource": "#736 (W4-4)",
    "slate.editor.citationSummary": "#737 (W4-5)",
    "slate.file.newFromTemplate": "#743 (W5-3)",
    "slate.file.newCanvas": "#745 (W6-1)",
    "slate.file.printNote": "#728 (W3-1)",
    "slate.vault.open": "#720 (W1-1)",
    "slate.vault.close": "#720 (W1-1)",
    "slate.help.open": "#756 (W8-6)",
    "slate.settings.open": "#751 (W8-1)",
    "slate.navigation.jumpToBibliography": "#737 (W4-5)",
}

# The authoritative Leaf registry (Workspace/RightPaneView.swift) -> W
# issue. Generation fails on an unmapped case so a newly shipped leaf
# can never be silently absent from the matrix.
LEAF_ISSUE = {
    "outline": "#734 (W4-2)",
    "backlinks": "#734 (W4-2)",
    "outgoingLinks": "#734 (W4-2)",
    "connections": "#746 (W6-2)",
    "embeds": "#734 (W4-2)",
    "math": "#729 (W3-2)",
    "code": "#731 (W3-4)",
    "diagrams": "#730 (W3-3)",
    "tasks": "#735 (W4-3)",
    "tasksReview": "#735 (W4-3)",
    "history": "#739 (W4-7)",
    "citations": "#737 (W4-5)",
    "bibliography": "#737 (W4-5)",
    "queries": "#738 (W4-6)",
    "basesDock": "#738 (W4-6)",
    "syncDiagnostics": "#740 (W4-8)",
}

# Milestones unshipped at the 2026-07-19 snapshot: their rows drop out
# with one-line notes (program §moving-target item 3).
DROPPED = [
    ("Milestone V — editor autocomplete", "#726 (W2-4)",
     "V unshipped at snapshot (GH milestone 29: 15 open)"),
    ("Milestone X — LaTeX authoring aids", "#727 (W2-5)",
     "X unshipped at snapshot (GH milestone 30: 15 open)"),
    ("Milestone XD — Excalidraw viewer", "#732 (W3-5, XD rows only)",
     "XD unshipped at snapshot (GH milestone 34: 13 open); non-XD embed rows stay"),
    ("Milestone E — note export (HTML + DOCX)", "W5/W8 rows per G1",
     "E unshipped at snapshot (GH milestone 36: 15 open)"),
    ("Milestone PD — accessible image OCR", "W3/W4 rows per G1",
     "PD unshipped at snapshot (GH milestone 35: 7 open)"),
    ("Milestone R — themes", "#752 (W8-2 consumes R's shared APCA spec)",
     "R unstarted at snapshot (GH milestone 18 empty); W8-2 falls back to the Swift-test predecessor per its spec"),
    ("Milestone S — explain-this-function", "(no W issue — post-R/S mac feature)",
     "S unstarted at snapshot (GH milestone 19 empty)"),
]

# HotkeySpoken.swift mirrors (glyph walk; keep in lockstep — the mac
# tables are private by design, so this is a reviewed copy, and chords
# using glyphs outside it pass characters through unchanged exactly as
# the mac walk does).
GLYPH_WORD = {"⌘": "Command", "⇧": "Shift", "⌥": "Option", "⌃": "Control"}
KEY_WORD = {
    ",": "Comma", ".": "Period", ";": "Semicolon", "'": "Apostrophe",
    "[": "Left Bracket", "]": "Right Bracket", "\\": "Backslash",
    "/": "Slash", "-": "Minus", "=": "Equals", "`": "Backtick",
    "↑": "Up Arrow", "↓": "Down Arrow", "←": "Left Arrow", "→": "Right Arrow",
    "0": "Zero",
}


def spoken(chord: str) -> str:
    return " ".join(GLYPH_WORD.get(c) or KEY_WORD.get(c, c) for c in chord)


def fail(msg: str) -> None:
    print(f"generate-parity-matrix: FATAL: {msg}", file=sys.stderr)
    sys.exit(1)


def commands() -> list[tuple[str, str, str, str, str]]:
    """(id, label, chord, spoken, issue) for every SlateCommandID."""
    text = COMMANDS_SWIFT.read_text(encoding="utf-8")
    ids: dict[str, str] = {}
    for name, cid in re.findall(
        r'static let (\w+)(?::\s*String)?\s*=\s*"(slate\.[a-zA-Z0-9.]+)"', text
    ):
        ids[name] = cid
    if "sidebarOpenShortcutSlots" in text:
        for slot in range(1, 10):
            ids[f"sidebarOpenShortcut{slot}"] = f"slate.sidebar.openShortcut{slot}"

    # register(SlateCommandID.x, label: "…", section: .y[, hotkey: "…"]…)
    # blocks: chunk on `register(` and read fields up to the action
    # closure. Definition-table registrations (no literal id) contribute
    # via the chord switch below instead.
    labels: dict[str, str] = {}
    sections: dict[str, str] = {}
    chords: dict[str, str] = {}
    block_hotkeys = 0
    for chunk in re.split(r"\bregister\(", text)[1:]:
        body = chunk.split(") {", 1)[0]
        id_match = re.search(r"SlateCommandID\.(\w+)", body)
        if not id_match:
            continue
        name = id_match.group(1)
        if m := re.search(r'label:\s*"([^"]*)"', body):
            labels[name] = m.group(1)
        if m := re.search(r"section:\s*\.(\w+)", body):
            sections[name] = m.group(1)
        if m := re.search(r'hotkey:\s*"([^"]*)"', body):
            chords[name] = m.group(1)
            block_hotkeys += 1

    # Definition-table chord switches: case SlateCommandID.x: hotkey = "…"
    for name, chord in re.findall(
        r"case SlateCommandID\.(\w+):\s*hotkey(?:Hint)?\s*=\s*\"([^\"]+)\"", text
    ):
        chords.setdefault(name, chord)

    # Fail-fast: every `hotkey: "` literal in the file must have been
    # attributed to a command id — a silent drop misreports chord parity.
    literal_hotkeys = len(re.findall(r'hotkey:\s*"', text))
    if block_hotkeys != literal_hotkeys:
        fail(
            f"attributed {block_hotkeys} of {literal_hotkeys} `hotkey:` "
            "literals — the register-block parser no longer matches "
            "SlateCommands.swift; fix the parser before regenerating"
        )

    rows = []
    unmapped: list[str] = []
    for name, cid in sorted(ids.items(), key=lambda kv: kv[1]):
        display = cid + "<dynamic>" if cid.endswith(".") else cid
        section = sections.get(name) or (cid.split(".")[1] if cid.count(".") >= 2 else "")
        issue = ID_ISSUE_OVERRIDES.get(cid) or SECTION_ISSUE.get(section)
        if issue is None:
            unmapped.append(cid)
            continue
        chord = chords.get(name, "")
        base = name[: -1] if False else name
        label = labels.get(base, "")
        rows.append((display, label, chord, spoken(chord) if chord else "", issue))
    # Numbered shortcut slots share the base definition's mapping.
    if unmapped:
        fail("unmapped command ids (add to SECTION_ISSUE/ID_ISSUE_OVERRIDES): "
             + ", ".join(unmapped))
    return rows


def leaves() -> list[tuple[str, str]]:
    text = LEAF_SWIFT.read_text(encoding="utf-8")
    enum_body = re.search(r"enum Leaf: String, CaseIterable.*?\n(.*?)\n    var id",
                          text, re.DOTALL)
    if not enum_body:
        fail("could not locate `enum Leaf` in RightPaneView.swift")
    cases = re.findall(r"^\s*case (\w+)", enum_body.group(1), re.MULTILINE)
    unmapped = [c for c in cases if c not in LEAF_ISSUE]
    if unmapped:
        fail("unmapped Leaf cases (add to LEAF_ISSUE): " + ", ".join(unmapped))
    return [(c, LEAF_ISSUE[c]) for c in cases]


def settings_tabs() -> list[str]:
    text = SETTINGS_SWIFT.read_text(encoding="utf-8")
    return list(dict.fromkeys(re.findall(r"(\w+)SettingsTab\(\)", text)))


def help_docs() -> list[str]:
    return sorted(p.name for p in HELP_DIR.glob("*.md"))


def cli_verbs() -> list[str]:
    try:
        out = subprocess.run(
            ["cargo", "run", "-q", "-p", "slate-cli", "--", "--help"],
            cwd=REPO, capture_output=True, text=True, timeout=600,
        ).stdout
    except (OSError, subprocess.TimeoutExpired):
        out = ""
    verbs = re.findall(r"^  (\w[\w-]*)\s{2,}", out, re.MULTILINE)
    if not verbs:
        main_rs = (REPO / "crates/slate-cli/src/main.rs").read_text(encoding="utf-8")
        verbs = [v.lower() for v in re.findall(r"^\s{4}(\w+)\s*[({]", main_rs, re.MULTILINE)]
    return [v for v in verbs if v not in ("help",)]


def main() -> int:
    cmd_rows = commands()
    leaf_rows = leaves()
    head = subprocess.run(
        ["git", "rev-parse", "--short", "HEAD"], cwd=REPO,
        capture_output=True, text=True).stdout.strip()
    today = datetime.date.today().isoformat()
    with_chords = sum(1 for _, _, c, _, _ in cmd_rows if c)

    lines: list[str] = []
    a = lines.append
    a("# Milestone W parity matrix (§W-F row-level checklist)")
    a("")
    a(f"Generated {today} at `{head}` by `scripts/generate-parity-matrix.py` "
      "(W0-4, #716). **Re-runnable:** matrix drift = re-run, diff, re-triage "
      "(program §moving-target). Every row is burned down by its consuming W "
      "issue; §W-F gates close-out on zero unshipped/unwaived rows.")
    a("")
    a("## Entry-criteria snapshot (w0_spec §W0-4 item 3)")
    a("")
    a("Recorded 2026-07-19 (the W0 unpark owner call; program §Entry criteria "
      "gate snapshot and the GH milestone description carry the same record):")
    a("")
    a("1. **Milestone T residual closed** — GH milestone 20 closed. ✔")
    a("2. **Milestone P shipped** with the graph's canonical accessible textual "
      "representation in Rust — GH milestone 16 closed. ✔")
    a("3. **Queue state (owner call)** — shipped at snapshot: the pre-W core "
      "program plus Milestones N (Bases), O (local history), P (graph), "
      "Q (commands), T (canvas), U (UI parity), and the FL files-sidebar "
      "program's shipped majority (GH milestone 31: 18 closed / 4 open). "
      "**Not shipped:** V, X, XD, E, PD (open), R and S (unstarted) — their "
      "rows drop below. The owner directed execution of the complete W0 set "
      "2026-07-19; W1–W8 remain parked pending the full-milestone unpark.")
    a("4. **W0.5 canonicalization landed** — #717/#718/#719 closed. ✔")
    a("5. **W0-1 binding spike concluded** — #714 closed; `uniffi-bindgen-cs` "
      "per w0_spec §Decision. ✔")
    a("")
    a("## §W-B keystroke budgets (w0_spec §W0-4 item 2)")
    a("")
    a("Pinned from the then-current `BENCHMARKS.md` mac baselines — the #407 "
      "rope-native windowed-highlight rows (`doc_buffer_keystroke`, Apple M5 "
      "Pro reference box) and the #375 Swift end-to-end row for marshalling "
      "context — plus an explicit marshalling allowance:")
    a("")
    a("| fixture | mac core p50 (#407/#404) | marshalling allowance | pinned Windows p50 budget |")
    a("|---|---|---|---|")
    a("| 100 KB | 86.7 µs (Slice B row; #407 improves it further) | +250 µs | **≤ 0.5 ms** |")
    a("| 1 MB | 80.7 µs | +250 µs | **≤ 0.5 ms** |")
    a("| 8 MB | 244.7 µs | +250 µs | **≤ 1.0 ms** |")
    a("")
    a("**Allowance rationale (not \"same as mac\"):** the W0-1 spike measured "
      "the uniffi `apply_edit` round-trip at ~112 µs/edit in a **debug** "
      "build (raw P/Invoke 101 µs — the generator's own overhead is ~11 µs); "
      "release-build marshalling is strictly cheaper, so 250 µs is >2× the "
      "debug-measured whole-call cost. Budgets are rounded up to absorb "
      "CI-runner-class variance vs the mac reference box; W8-5 measures with "
      "BenchmarkDotNet on the pinned runner class and records actuals in "
      "`BENCHMARKS.md`. **Flatness gate:** p50(8 MB) ≤ 4× p50(1 MB) — the mac "
      "profile is ~3× (245 µs vs 81 µs); no size-correlated growth beyond it.")
    a("")
    a("## Command inventory")
    a("")
    a(f"{len(cmd_rows)} stable command ids from the `SlateCommandID` catalog "
      f"(drift-test-enforced), {with_chords} carrying chords from the "
      "registration blocks and definition-table chord switches (blank chord "
      "= palette/menu-only or focus-scoped by design; the generator fails if "
      "a `hotkey:` literal goes unattributed). Spoken hotkeys derive from "
      "chords via the `HotkeySpoken` glyph walk (mirrored here); Windows "
      "chord mapping is by platform convention (⌘→Ctrl, ⌥→Alt; decision 12), "
      "declared in one table in W5-1 with spoken strings substituted "
      "per-platform through the canonical vocabulary.")
    a("")
    a("| command id | capability (mac label) | mac chord | spoken hotkey | consuming W issue | status |")
    a("|---|---|---|---|---|---|")
    for cid, label, chord, spoke, issue in cmd_rows:
        a(f"| `{cid}` | {label or '—'} | {chord or '—'} | {spoke or '—'} | {issue} | pending |")
    a("")
    a("The palette surface itself (ranking via the W0.5-1 core engine, "
      "sections, recents, chord display) is **#741 (W5-1)**; the quick "
      "switcher is **#723 (W1-4)**.")
    a("")
    a("## Leaf inventory (`enum Leaf`, the shipped right-pane registry)")
    a("")
    a("| leaf | consuming W issue | status |")
    a("|---|---|---|")
    for leaf, issue in leaf_rows:
        a(f"| `{leaf}` | {issue} | pending |")
    a("")
    a("## Primary surfaces")
    a("")
    a("| surface | source | consuming W issue | status |")
    a("|---|---|---|---|")
    a("| App shell, window chrome, vault lifecycle | `SlateMacApp.swift` | #720 (W1-1) | pending |")
    a("| Files sidebar (tree CRUD, filter, tags, pins, shortcuts, folder notes) | `FileTreeSidebar.swift` + FL program | #721 (W1-2) | pending |")
    a("| Workspace: tabs, splits, leaves, persistence, focus routing | `Workspace/` | #722 (W1-3) | pending |")
    a("| Quick switcher | `QuickSwitcherModel.swift` (core ranking, W0.5-2) | #723 (W1-4) | pending |")
    a("| Editor host (AvalonEdit ⇄ DocumentBuffer, undo, save, IME) | `NoteEditorView.swift` | #724 (W2-1) | pending |")
    a("| Editor canonical spans | #381 span API consumers | #381 (W2-2) | pending |")
    a("| In-editor interactions (links, tags, citations, embeds, checkboxes) | `NoteEditorView.swift` | #725 (W2-3) | pending |")
    a("| Reading view (block model, mode toggle, heading/link AT nav, print) | `Reading/` | #728 (W3-1) | pending |")
    a("| Math rendering + canonical speech/braille artifact | core `math.rs` consumers | #729 (W3-2) | pending |")
    a("| Diagrams (canonical Rust SVG + description) | core `diagram.rs` consumers | #730 (W3-3) | pending |")
    a("| Code blocks (canonical tokens + AT preamble) | `CodeBlockView.swift` | #731 (W3-4) | pending |")
    a("| Embeds across contexts | editor/reading embeds | #732 (W3-5; XD rows dropped) | pending |")
    a("| Accessible grid substrate | `AccessibleDataGrid.swift` | #733 (W4-1) | pending |")
    a("| Properties (in-note header, panel, typed rows, add-property) | `Properties*` views | #736 (W4-4) | pending |")
    a("| Bases grid + builder (N shipped) | `Bases/` | #738 (W4-6) | pending |")
    a("| Command palette | `CommandPaletteModel.swift` (core ranking, W0.5-1) | #741 (W5-1) | pending |")
    a("| Search overlay | search UI over `full_text_search` | #742 (W5-2) | pending |")
    a("| Templates picker + prompt flow | template views | #743 (W5-3) | pending |")
    a("| File management + bulk rename | sidebar/file commands | #744 (W5-4) | pending |")
    a("| Accessible canvas (T parity) | `Canvas/` | #745 (W6-1) | pending |")
    a("| Graph view (P parity, canonical textual representation) | `Graph/` | #746 (W6-2) | pending |")
    a("")
    a("## Settings surface")
    a("")
    a("| tab | consuming W issue | status |")
    a("|---|---|---|")
    for tab in settings_tabs():
        a(f"| {tab} | #751 (W8-1) | pending |")
    a("| Windows-only section (theme/contrast behavior, file associations) | #751 (W8-1, additive) | pending |")
    a("")
    a("## Help-doc index")
    a("")
    a("| doc | consuming W issue | status |")
    a("|---|---|---|")
    for doc in help_docs():
        a(f"| `docs/help/{doc}` | #756 (W8-6; shared prose, per-platform chords per decision 20) | pending |")
    a("")
    a("## `slate.cli.v1` surface")
    a("")
    a("Verbs (from `slate-cli --help`): " + ", ".join(f"`{v}`" for v in cli_verbs()) + ".")
    a("")
    a("| capability | consuming W issue | status |")
    a("|---|---|---|")
    a("| CLI builds + full test suite green on the Windows runner | #715 (W0-3) | **shipped** (windows.yml step) |")
    a("| Distribution/packaging beyond CI | reserved (W-E5, decision 19) | out of scope |")
    a("")
    a("## File-type handlers")
    a("")
    a("The SwiftPM mac app declares no `CFBundleDocumentTypes`; the shipped "
      "handler set is pinned from program decision 15.")
    a("")
    a("| type | Windows behavior | consuming W issue | status |")
    a("|---|---|---|---|")
    a("| `.md` | association optional per user choice | #753 (W8-3) | pending |")
    a("| `.base` | registered | #753 (W8-3) | pending |")
    a("| `.canvas` | registered | #753 (W8-3) | pending |")
    a("| `.excalidraw` | dropped — XD unshipped at snapshot | — | dropped |")
    a("")
    a("## Dropped feature-conditional rows (program §moving-target item 3)")
    a("")
    a("| milestone | would-be consumer | one-line note |")
    a("|---|---|---|")
    for name, issue, note in DROPPED:
        a(f"| {name} | {issue} | {note} |")
    a("")
    a("## Foundation rows already shipped by W0")
    a("")
    a("| capability | issue | status |")
    a("|---|---|---|")
    a("| `apps/slate-windows/` scaffold, windows.yml CI, hello-core app | #603 (W0-2) | **shipped** (#956) |")
    a("| Full-surface C# binding + §W-E censuses + §W-A harness skeleton + app log | #715 (W0-3) | **shipped** |")
    a("| Parity matrix + §W-B budgets + entry-criteria snapshot | #716 (W0-4) | **this document** |")
    a("")

    OUT.write_text("\n".join(lines), encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(REPO)} "
          f"({len(cmd_rows)} command rows, {with_chords} with chords; "
          f"{len(leaf_rows)} leaves)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
