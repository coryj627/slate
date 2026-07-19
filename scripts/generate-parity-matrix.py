#!/usr/bin/env python3
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
"""Generate docs/plans/18_windows_port/parity_matrix.md (W0-4, #716).

The §W-F row-level checklist every W issue burns down: an inventory pass
over the shipped mac app producing one row per surface/capability with
its consuming W issue. Re-runnable by design — matrix drift = re-run,
diff, re-triage (program §moving-target).

Sources (all mechanical, all drift-test- or CI-enforced in the mac app):
- Command inventory: ``SlateCommandID`` statics in ``SlateCommands.swift``
  (the stability-contract catalog; ``SlateCommandsTests`` asserts every id
  resolves to a registered ``Command``) plus the chord tables
  (``hotkey = "…"`` switch arms and ``hotkeyHint`` constants).
- Panel inventory: ``*Panel.swift`` under the mac app source.
- Settings tabs: ``*SettingsTab()`` uses in ``SettingsView.swift``.
- Help docs: ``docs/help/*.md``.
- CLI verbs: ``slate-cli --help`` (run live when cargo is available, else
  parsed from the clap ``Commands`` enum in ``crates/slate-cli``).
- File-type handlers: pinned from program decision 15 (the SwiftPM mac
  app declares no CFBundleDocumentTypes; Windows registration is W8-3).

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
MAC_SRC = REPO / "apps/slate-mac/Sources/SlateMac"
HELP_DIR = REPO / "docs/help"
OUT = REPO / "docs/plans/18_windows_port/parity_matrix.md"

# Command-id section token -> the W issue that ships that surface on
# Windows. Palette/chords themselves are W5-1 (#741); each command's
# *capability* lands with its surface's issue.
SECTION_ISSUE = {
    "sidebar": "#721 (W1-2)",
    "file": "#744 (W5-4)",
    "view": "#722 (W1-3)",
    "workspace": "#722 (W1-3)",
    "editor": "#725 (W2-3)",
    "search": "#742 (W5-2)",
    "template": "#743 (W5-3)",
    "templates": "#743 (W5-3)",
    "tasks": "#735 (W4-3)",
    "properties": "#736 (W4-4)",
    "citations": "#737 (W4-5)",
    "bibliography": "#737 (W4-5)",
    "history": "#739 (W4-7)",
    "canvas": "#745 (W6-1)",
    "graph": "#746 (W6-2)",
    "bases": "#738 (W4-6)",
    "sync": "#740 (W4-8)",
    "vault": "#720 (W1-1)",
    "app": "#720 (W1-1)",
    "help": "#756 (W8-6)",
    "settings": "#751 (W8-1)",
    "math": "#729 (W3-2)",
    "reading": "#728 (W3-1)",
}

PANEL_ISSUE = {
    "BacklinksPanel": "#734 (W4-2)",
    "OutgoingLinksPanel": "#734 (W4-2)",
    "EmbedsPanel": "#734 (W4-2)",
    "ContentBlockPanels": "#734 (W4-2)",
    "TasksPanel": "#735 (W4-3)",
    "CitationsPanel": "#737 (W4-5)",
    "BibliographyPanel": "#737 (W4-5)",
    "HistoryPanel": "#739 (W4-7)",
    "SyncDiagnosticsPanel": "#740 (W4-8)",
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


def commands() -> list[tuple[str, str, str]]:
    """(id, chord-or-'', section-token) for every SlateCommandID."""
    text = COMMANDS_SWIFT.read_text(encoding="utf-8")
    ids: dict[str, str] = {}
    for name, cid in re.findall(
        r'static let (\w+)(?::\s*String)?\s*=\s*"(slate\.[a-zA-Z0-9.]+)"', text
    ):
        ids[name] = cid
    # Numbered shortcut slots (sidebarOpenShortcut(1...9)).
    if "sidebarOpenShortcutSlots" in text:
        for slot in range(1, 10):
            ids[f"sidebarOpenShortcut{slot}"] = f"slate.sidebar.openShortcut{slot}"

    chords: dict[str, str] = {}
    for name, chord in re.findall(
        r"case SlateCommandID\.(\w+):\s*hotkey(?:Hint)?\s*=\s*\"([^\"]+)\"", text
    ):
        chords[name] = chord

    rows = []
    for name, cid in sorted(ids.items(), key=lambda kv: kv[1]):
        section = cid.split(".")[1] if cid.count(".") >= 2 else "app"
        # Trailing-dot constants are dynamic-id prefixes (e.g. per-saved-
        # query commands) — one row per family, marked as such.
        display = cid + "<dynamic>" if cid.endswith(".") else cid
        rows.append((display, chords.get(name, ""), section))
    return rows


def panels() -> list[str]:
    return sorted(p.stem for p in MAC_SRC.glob("*Panel*.swift"))


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
    head = subprocess.run(
        ["git", "rev-parse", "--short", "HEAD"], cwd=REPO,
        capture_output=True, text=True).stdout.strip()
    today = datetime.date.today().isoformat()

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
      "(drift-test-enforced; chords from the registration chord tables — "
      "blank chord = palette/menu-only or focus-scoped by design). Windows "
      "chord mapping is by platform convention (⌘→Ctrl, ⌥→Alt; decision 12), "
      "declared in one table in W5-1.")
    a("")
    a("| command id | mac chord | consuming W issue | status |")
    a("|---|---|---|---|")
    for cid, chord, section in cmd_rows:
        issue = SECTION_ISSUE.get(section, "#741 (W5-1)")
        a(f"| `{cid}` | {chord or '—'} | {issue} | pending |")
    a("")
    a("The palette surface itself (ranking via the W0.5-1 core engine, "
      "sections, recents, chord display) is **#741 (W5-1)**; the quick "
      "switcher is **#723 (W1-4)**.")
    a("")
    a("## Leaf / panel / tab inventory")
    a("")
    a("| surface | source | consuming W issue | status |")
    a("|---|---|---|---|")
    a("| App shell, window chrome, vault lifecycle | `SlateMacApp.swift` | #720 (W1-1) | pending |")
    a("| Files sidebar (tree CRUD, filter, tags, pins, shortcuts, folder notes) | `FileTreeSidebar.swift` + FL program | #721 (W1-2) | pending |")
    a("| Workspace: tabs, splits, leaves, persistence, focus routing | `Workspace/` | #722 (W1-3) | pending |")
    a("| Quick switcher | `QuickSwitcherModel.swift` (core ranking, W0.5-2) | #723 (W1-4) | pending |")
    a("| Editor (AvalonEdit ⇄ DocumentBuffer, spans, interactions) | `NoteEditorView.swift` | #724/#381/#725 (W2-1/2/3) | pending |")
    a("| Reading view (block model, mode toggle, heading/link AT nav) | `Reading/` | #728 (W3-1) | pending |")
    a("| Math rendering + speech/braille artifact | `MathBlockView` + core `math.rs` | #729 (W3-2) | pending |")
    a("| Diagrams (canonical Rust SVG + description) | core `diagram.rs` consumers | #730 (W3-3) | pending |")
    a("| Code blocks (canonical tokens + AT preamble) | `CodeBlockView.swift` | #731 (W3-4) | pending |")
    a("| Embeds across contexts | `EmbedsPanel.swift` + editor embeds | #732 (W3-5; XD rows dropped) | pending |")
    a("| Accessible grid substrate | `AccessibleDataGrid.swift` | #733 (W4-1) | pending |")
    for p in panels():
        a(f"| {p} | `{p}.swift` | {PANEL_ISSUE.get(p, '#734 (W4-2)')} | pending |")
    a("| Properties (in-note header, panel, typed rows) | `Properties*` views | #736 (W4-4) | pending |")
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
    print(f"wrote {OUT.relative_to(REPO)} ({len(cmd_rows)} command rows)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
