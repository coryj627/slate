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
import json
import re
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
COMMANDS_SWIFT = REPO / "apps/slate-mac/Sources/SlateMac/SlateCommands.swift"
SIDEBAR_CATALOG = REPO / "apps/slate-mac/Sources/SlateMac/Sidebar/SidebarActionCatalog.swift"
SETTINGS_SWIFT = REPO / "apps/slate-mac/Sources/SlateMac/SettingsView.swift"
LEAF_SWIFT = REPO / "apps/slate-mac/Sources/SlateMac/Workspace/RightPaneView.swift"
WORKSPACE_SWIFT = REPO / "apps/slate-mac/Sources/SlateMac/Workspace/WorkspaceModel.swift"
HELP_DIR = REPO / "docs/help"
OUT = REPO / "docs/plans/18_windows_port/parity_matrix.md"
WINDOWS_CHORDS = REPO / "apps/slate-windows/chords.json"

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
    # Reassigned out of #742 at the W5-2 close-out: find-in-note shares
    # no surface with vault search (mac ships it as the NSTextView find
    # bar; core's SearchScope::File is reserved and unreachable), and
    # the Windows equivalent was measured unusable as shipped — see
    # docs/plans/29_search_overlay_contracts.md "Find-in-note is not in
    # this issue".
    "slate.editor.findInNote": "#1112 (find-in-note, split from #742)",
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
    # Registered under .view (the reveal/refresh lives in the View menu)
    # but owned by their feature surfaces:
    "slate.history.showPanel": "#739 (W4-7)",
    "slate.diagnostics.refreshSync": "#740 (W4-8)",
    # Historical slate.file.* ids are projected into the .sidebar section
    # (FL04-A); triaged per capability: the sidebar import engine is
    # W1-2's, the file-management command set is W5-4's.
    "slate.file.cancelImport": "#721 (W1-2)",
    "slate.file.importFilesAndFolders": "#721 (W1-2)",
    "slate.file.copyPath": "#744 (W5-4)",
    "slate.file.delete": "#744 (W5-4)",
    "slate.file.duplicate": "#744 (W5-4)",
    "slate.file.moveTo": "#744 (W5-4)",
    "slate.file.newFolder": "#744 (W5-4)",
    "slate.file.newNote": "#744 (W5-4)",
    "slate.file.rename": "#744 (W5-4)",
    "slate.file.revealInFinder": "#744 (W5-4)",
}

# Ids whose labels are computed at runtime (numbered slot families,
# dynamic-prefix families) — exempt from the metadata-completeness gate.
DYNAMIC_LABEL_OK_PREFIXES = (
    "slate.sidebar.openShortcut",
    "slate.bases.savedQuery.run.",
)

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

# Leaves whose WINDOWS body + activation shipped: leaf -> delivery
# evidence (date, surface, tests). A leaf absent here renders
# "pending"; an entry here must name checkable evidence.
LEAF_DELIVERED = {
    "outline": (
        "implemented 2026-08-01 (#734): `RightPanePanelsViewModel` + "
        "MainWindow leaf body; `RightPanePanelsTests` + FlaUI "
        "`RightPanePanels_LeafBodiesCarryRowsAndBacklinkNavigates`"),
    "backlinks": (
        "implemented 2026-08-01 (#734): `RightPanePanelsViewModel` + "
        "MainWindow leaf body; `RightPanePanelsTests` + FlaUI "
        "`RightPanePanels_LeafBodiesCarryRowsAndBacklinkNavigates`"),
    "outgoingLinks": (
        "implemented 2026-08-01 (#734): `RightPanePanelsViewModel` + "
        "MainWindow leaf body; `RightPanePanelsTests` + FlaUI "
        "`RightPanePanels_LeafBodiesCarryRowsAndBacklinkNavigates`"),
    "embeds": (
        "implemented 2026-08-01 (#734): `RightPanePanelsViewModel` + "
        "shared `EditorEmbedPreviewView` cards; `RightPanePanelsTests` "
        "+ FlaUI `RightPanePanels_LeafBodiesCarryRowsAndBacklinkNavigates`"),
    "tasks": (
        "implemented 2026-08-01 (#735): `RightPanePanelsViewModel` task "
        "sections + MainWindow leaf body; `TasksPanelTests` + FlaUI "
        "`TaskPanels_RowsToggleAndReviewCarriesTheMacShapes`"),
    "tasksReview": (
        "implemented 2026-08-01 (#735): `TasksReviewViewModel` + "
        "MainWindow leaf body + Ctrl+R command; `TasksReviewTests` + "
        "FlaUI `TaskPanels_RowsToggleAndReviewCarriesTheMacShapes`"),
    "citations": (
        "implemented 2026-08-04 (#737): `CitationsPanelViewModel` + "
        "MainWindow leaf body + details/summary sheets; "
        "`CitationsPanelTests` + FlaUI "
        "`CitationSurfaces_GridsSheetsAndChords_AreClean`"),
    "bibliography": (
        "implemented 2026-08-04 (#737): `BibliographyViewModel` + "
        "MainWindow leaf body, BOTH segments on `AccessibleDataGrid`; "
        "`BibliographyPanelTests` + FlaUI "
        "`CitationSurfaces_GridsSheetsAndChords_AreClean`"),
    "queries": (
        "implemented 2026-08-08 (#738): workspace BaseQueriesState + "
        "MainWindow leaf body (saved queries / base files / dashboards, "
        "pin + rename + delete + export + dock); `BasesQueriesTests` + "
        "FlaUI `BasesSurfaces_GridBuilderAndLeaves_AreClean`"),
    "basesDock": (
        "implemented 2026-08-08 (#738): workspace dock target following "
        "the active note (this_path, 500 ms debounce) over read-only "
        "`BaseSurfaceView`/`DashboardSurfaceView`; `BasesQueriesTests` + "
        "FlaUI `BasesSurfaces_GridBuilderAndLeaves_AreClean` (docks a "
        "base from the leaf and axe-scans the revealed BasesDockGrid)"),
    "history": (
        "implemented 2026-08-09 (#739): `HistoryViewModel` + "
        "`HistorySurfaceView` MainWindow leaf body (two segments, "
        "day-grouped versions, StructuredDiff walkthrough, restore + "
        "Restore As + deleted recovery, since-open opt-in, markers "
        "toggle); `HistoryPanelTests` + FlaUI "
        "`HistorySurfaces_LeafDiffAndRestore_AreClean`"),
    "syncDiagnostics": (
        "implemented 2026-08-09 (#740): `SyncDiagnosticsViewModel` + "
        "`SyncDiagnosticsSurfaceView` MainWindow leaf body (five-state "
        "report, per-provider peered rows + evidence, LiveSync config "
        "section, bounded marker watcher); `SyncDiagnosticsPanelTests` "
        "+ FlaUI `SyncDiagnostics_LeafReportAndRefresh_AreClean`"),
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

# HotkeySpoken.swift mirrors — exact copies of the private glyphWord /
# keyWord tables (keep in lockstep; anything outside them passes through
# unchanged, exactly as the mac per-character walk does).
GLYPH_WORD = {"⌘": "Command", "⇧": "Shift", "⌥": "Option", "⌃": "Control"}
KEY_WORD = {
    ",": "Comma", ".": "Period", "/": "Slash", "\\": "Backslash",
    ";": "Semicolon", "'": "Quote", "[": "Left Bracket",
    "]": "Right Bracket", "-": "Minus", "=": "Equals", "`": "Backtick",
    " ": "Space",
    "↑": "Up Arrow", "↓": "Down Arrow", "←": "Left Arrow", "→": "Right Arrow",
}


def unescape_swift(literal: str) -> str:
    """Decode the Swift string-literal escapes chords/labels can carry
    (`\\\\` and `\\"`) into their runtime characters — HotkeySpoken sees
    runtime characters, so the mirror must too."""
    return literal.replace('\\\\', '\\').replace('\\"', '"')


def spoken(chord: str) -> str:
    return " ".join(GLYPH_WORD.get(c) or KEY_WORD.get(c, c) for c in chord)


def fail(msg: str) -> None:
    print(f"generate-parity-matrix: FATAL: {msg}", file=sys.stderr)
    sys.exit(1)


def swift_enum_cases(body: str) -> list[str]:
    """Case names from a Swift enum body: handles payloads and
    comma-separated declarations (`case a, b`) so a style change cannot
    silently drop a case from the inventory."""
    names: list[str] = []
    for decl in re.findall(r"^[ \t]*case[ \t]+(.+)", body, re.MULTILINE):
        decl = re.sub(r"\([^)]*\)", "", decl)  # strip payloads
        for part in decl.split(","):
            name = part.strip().rstrip(":")
            if re.fullmatch(r"\w+", name):
                names.append(name)
    return names


def commands() -> list[tuple[str, str, str, str, str]]:
    """(id, label, chord, spoken, issue) for every SlateCommandID."""
    text = COMMANDS_SWIFT.read_text(encoding="utf-8")
    catalog = SIDEBAR_CATALOG.read_text(encoding="utf-8")
    ids: dict[str, str] = {}
    for name, cid in re.findall(
        r'static let (\w+)(?::\s*String)?\s*=\s*"(slate\.[a-zA-Z0-9.]+)"', text
    ):
        ids[name] = cid
    if "sidebarOpenShortcutSlots" in text:
        for slot in range(1, 10):
            ids[f"sidebarOpenShortcut{slot}"] = f"slate.sidebar.openShortcut{slot}"

    labels: dict[str, str] = {}
    sections: dict[str, str] = {}
    chords: dict[str, str] = {}
    attributed_hotkeys = 0

    # Shape 1: register(...) / registerStructural(...) blocks.
    for chunk in re.split(r"\bregister(?:Structural)?\(", text)[1:]:
        body = chunk.split(") {", 1)[0]
        id_match = re.search(r"SlateCommandID\.(\w+)", body)
        if not id_match:
            continue
        name = id_match.group(1)
        if m := re.search(r'label:\s*"([^"]*)"', body):
            labels.setdefault(name, unescape_swift(m.group(1)))
        if m := re.search(r"section:\s*\.(\w+)", body):
            sections.setdefault(name, m.group(1))
        if m := re.search(r'hotkey:\s*"([^"]*)"', body):
            chords.setdefault(name, unescape_swift(m.group(1)))
            attributed_hotkeys += 1

    # Shape 2: command-contract types (static let id = SlateCommandID.x
    # … label / section / hotkeyHint statics in the same block).
    for m in re.finditer(r"static let id = SlateCommandID\.(\w+)", text):
        name = m.group(1)
        window = text[m.end():m.end() + 600]
        if lm := re.search(r'static let label = "([^"]+)"', window):
            labels.setdefault(name, unescape_swift(lm.group(1)))
        if sm := re.search(r"static let section: CommandSection = \.(\w+)", window):
            sections.setdefault(name, sm.group(1))
        if hm := re.search(r'static let hotkeyHint = "([^"]+)"', window):
            chords.setdefault(name, unescape_swift(hm.group(1)))
            attributed_hotkeys += 1

    # Shape 3: the sidebar action catalog's positional factory calls
    # (SlateCommandID.x, "Label", …) — always section .sidebar.
    for name, label in re.findall(r'SlateCommandID\.(\w+),\s*\n?\s*"([^"]+)"', catalog):
        labels.setdefault(name, unescape_swift(label))
        sections.setdefault(name, "sidebar")

    # Shape 4: definition-table chord switches
    # (case SlateCommandID.x: hotkey = "…").
    switch_entries = re.findall(
        r"case SlateCommandID\.(\w+):\s*hotkey(?:Hint)?\s*=\s*\"([^\"]+)\"", text
    )
    for name, chord in switch_entries:
        if name not in chords:
            chords[name] = unescape_swift(chord)
            attributed_hotkeys += 1

    # Fail-fast: every chord literal in every recognized shape must be
    # attributed to a command id — a silent drop misreports chord parity.
    expected_hotkeys = (
        len(re.findall(r'hotkey:\s*"', text))
        + len(re.findall(r'static let hotkeyHint = "', text))
        + len(switch_entries)
    )
    if attributed_hotkeys != expected_hotkeys:
        fail(
            f"attributed {attributed_hotkeys} of {expected_hotkeys} chord "
            "literals — a registration shape no longer matches the parser; "
            "fix the parser before regenerating"
        )

    rows = []
    unmapped: list[str] = []
    missing_meta: list[str] = []
    ownership_review: list[str] = []
    for name, cid in sorted(ids.items(), key=lambda kv: kv[1]):
        display = cid + "<dynamic>" if cid.endswith(".") else cid
        dynamic = any(cid.startswith(p) for p in DYNAMIC_LABEL_OK_PREFIXES) or cid.endswith(".")
        reg_section = sections.get(name, "")
        ns_section = cid.split(".")[1] if cid.count(".") >= 2 else ""
        section = reg_section or ns_section
        issue = ID_ISSUE_OVERRIDES.get(cid) or SECTION_ISSUE.get(section)
        if issue is None:
            unmapped.append(cid)
            continue
        # Metadata-completeness gate: every non-dynamic id must resolve
        # to a label and a registered/derived section.
        label = labels.get(name, "")
        if not dynamic and (not label or not section):
            missing_meta.append(cid)
        # Cross-ownership tripwire: when the registered section and the
        # id namespace would map to different issues, the id must be
        # explicitly triaged in ID_ISSUE_OVERRIDES — a valid-but-wrong
        # default must not pass silently.
        if (
            cid not in ID_ISSUE_OVERRIDES
            and reg_section
            and ns_section
            and SECTION_ISSUE.get(reg_section)
            and SECTION_ISSUE.get(ns_section)
            and SECTION_ISSUE[reg_section] != SECTION_ISSUE[ns_section]
        ):
            ownership_review.append(cid)
        chord = chords.get(name, "")
        rows.append((display, label, chord, spoken(chord) if chord else "", issue))
    if unmapped:
        fail("unmapped command ids (add to SECTION_ISSUE/ID_ISSUE_OVERRIDES): "
             + ", ".join(unmapped))
    if missing_meta:
        fail("ids with no parsed label/section (extend the parser or the "
             "dynamic whitelist): " + ", ".join(missing_meta))
    if ownership_review:
        fail("ids whose registered section and namespace map to different "
             "issues — triage each into ID_ISSUE_OVERRIDES: "
             + ", ".join(ownership_review))
    return rows


def leaves() -> list[tuple[str, str]]:
    text = LEAF_SWIFT.read_text(encoding="utf-8")
    enum_body = re.search(r"enum Leaf: String, CaseIterable.*?\n(.*?)\n    var id",
                          text, re.DOTALL)
    if not enum_body:
        fail("could not locate `enum Leaf` in RightPaneView.swift")
    cases = swift_enum_cases(enum_body.group(1))
    unmapped = [c for c in cases if c not in LEAF_ISSUE]
    if unmapped:
        fail("unmapped Leaf cases (add to LEAF_ISSUE): " + ", ".join(unmapped))
    return [(c, LEAF_ISSUE[c]) for c in cases]


def editor_item_kinds() -> list[str]:
    """The persisted workspace tab-content kinds (`enum EditorItem`) —
    what `WorkspaceStore` round-trips; distinct from the right-pane Leaf
    registry. All rows consume #722 (W1-3)."""
    text = WORKSPACE_SWIFT.read_text(encoding="utf-8")
    body_match = re.search(r"enum EditorItem[^{]*\{(.*?)\n\}", text, re.DOTALL)
    if not body_match:
        fail("could not locate `enum EditorItem` in WorkspaceModel.swift")
    kinds = swift_enum_cases(body_match.group(1))
    if not kinds:
        fail("`enum EditorItem` parsed empty — parser no longer matches")
    return kinds


def settings_tabs() -> list[str]:
    text = SETTINGS_SWIFT.read_text(encoding="utf-8")
    return list(dict.fromkeys(re.findall(r"(\w+)SettingsTab\(\)", text)))


def help_docs() -> list[str]:
    return sorted(p.name for p in HELP_DIR.glob("*.md"))


def cli_verbs_live() -> list[str]:
    try:
        out = subprocess.run(
            ["cargo", "run", "-q", "-p", "slate-cli", "--", "--help"],
            cwd=REPO, capture_output=True, text=True, timeout=600,
        ).stdout
    except (OSError, subprocess.TimeoutExpired):
        out = ""
    verbs = re.findall(r"^  (\w[\w-]*)\s{2,}", out, re.MULTILINE)
    return [v for v in verbs if v != "help"]


def cli_verbs_fallback() -> list[str]:
    """clap derives kebab-case verb names from the subcommand enum's
    variants by default. Scope both the extraction and the explicit-name
    ambiguity guard to the ``enum Command`` body — the root ``Cli``
    struct's ``#[command(name = "slate")]`` names the executable, not a
    verb, and must not abort the fallback."""
    main_rs = (REPO / "crates/slate-cli/src/main.rs").read_text(encoding="utf-8")
    body_match = re.search(r"\benum Command\s*\{(.*?)\n\}", main_rs, re.DOTALL)
    if not body_match:
        fail("could not locate `enum Command` in slate-cli main.rs")
    body = body_match.group(1)
    if re.search(r"#\[command\(\s*name\s*=", body):
        fail("slate-cli subcommands use explicit #[command(name=...)] "
             "attributes; run with cargo available so verbs come from "
             "live --help")
    return [
        re.sub(r"(?<!^)(?=[A-Z])", "-", v).lower()
        for v in re.findall(r"^\s{4}(\w+)\s*[({]", body, re.MULTILINE)
    ]


def cli_verbs() -> list[str]:
    if "--verify-fallback" in sys.argv:
        live, derived = cli_verbs_live(), cli_verbs_fallback()
        if live != derived:
            fail(f"CLI verb fallback drifted from live --help: live={live} "
                 f"fallback={derived}")
        print(f"cli fallback verified against live --help ({len(live)} verbs)")
        return live
    return cli_verbs_live() or cli_verbs_fallback()


IMPLEMENTED_STATUS = (
    "implemented; local gates green 2026-07-20; interactive CI + human AT pending"
)
W2_IMPLEMENTED_STATUS = (
    "implemented; local gates green 2026-07-23; interactive CI + human AT pending"
)
W3_IMPLEMENTED_STATUS = (
    "implemented; local gates green 2026-07-27; interactive CI + human AT pending"
)

# W3 delivery is tracked per COMMAND, not per issue prefix: #728 also
# owns waived rows (printNote), and a prefix rule would demand evidence
# for a command the owner explicitly waived out of the wave.
W3_DELIVERED_COMMANDS = {
    "slate.editor.toggleViewMode",
}

W4_IMPLEMENTED_STATUS = (
    "implemented; local gates green 2026-08-03; interactive CI + human AT pending"
)

# The gate date is per-ISSUE, not per-wave: stamping every W4 row with
# one date would claim gates ran on a day they did not for whichever
# issue landed later.
W4_STATUS_BY_COMMAND = {
    "slate.navigation.jumpToBibliography":
        "implemented; local gates green 2026-08-04; "
        "interactive CI + human AT pending",
    "slate.editor.citationSummary":
        "implemented; local gates green 2026-08-04; "
        "interactive CI + human AT pending",
}

W4_6_STATUS = (
    "implemented; local gates green 2026-08-08; "
    "interactive CI + human AT pending"
)

W4_6_COMMANDS = {
    "slate.bases.builder.addCondition",
    "slate.bases.builder.addGroup",
    "slate.bases.builder.editCondition",
    "slate.bases.builder.removeCondition",
    "slate.bases.copyLink",
    "slate.bases.copyMarkdown",
    "slate.bases.editProperty",
    "slate.bases.editViewFilters",
    "slate.bases.exportCsv",
    "slate.bases.exportMarkdown",
    "slate.bases.newQuery",
    "slate.bases.nextView",
    "slate.bases.openRow",
    "slate.bases.openViewSwitcher",
    "slate.bases.previousView",
    "slate.bases.quickFilter",
    "slate.bases.refresh",
    "slate.bases.resultsPopover",
    "slate.bases.saveSortToView",
    "slate.bases.savedQuery.run.<dynamic>",
    "slate.bases.showBacklinks",
    "slate.bases.sortByColumn",
    "slate.bases.viewAsList",
    "slate.bases.viewAsTable",
    "slate.bases.whereAmI",
}
W4_STATUS_BY_COMMAND.update({command: W4_6_STATUS for command in W4_6_COMMANDS})

W4_7_STATUS = (
    "implemented; local gates green 2026-08-09; "
    "interactive CI + human AT pending"
)

# W4-7 (#739): mac registers exactly ONE history command — the row
# actions (compare/restore/restore-as/recover) are deliberately not
# commands (they need row context).
W4_7_COMMANDS = {
    "slate.history.showPanel",
}
W4_STATUS_BY_COMMAND.update({command: W4_7_STATUS for command in W4_7_COMMANDS})

W4_8_STATUS = (
    "implemented; local gates green 2026-08-09; "
    "interactive CI + human AT pending"
)

# W5-1 (#741): the palette is a SURFACE, not a command row — mac keeps
# its own chord deliberately unregistered, so this issue delivers no
# slate.* id of its own. Its status therefore rides the surface row
# rather than any command.
W5_1_STATUS = (
    "implemented; local gates green 2026-08-13; "
    "interactive CI + human AT pending"
)

# W4-8 (#740): mac registers exactly ONE sync command, and it is
# CHORDLESS on both platforms ("refresh is a rare, deliberate action");
# it refreshes only and never reveals the leaf (contract SD7).
W4_8_COMMANDS = {
    "slate.diagnostics.refreshSync",
}
W4_STATUS_BY_COMMAND.update({command: W4_8_STATUS for command in W4_8_COMMANDS})

# W5-2 (#742): the vault-search overlay. mac registers exactly ONE
# search command; the overlay's own keys are surface interactions
# (chordSurface rows), and find-in-note moved to #1112 at the
# close-out (see ID_ISSUE_OVERRIDES).
W5_2_STATUS = (
    "implemented; local gates green 2026-08-16; "
    "interactive CI + human AT pending"
)

W5_2_DELIVERED_COMMANDS = {
    "slate.view.toggleSearch",
}

# W5-3 (#743): create-from-template. mac registers exactly ONE template
# command; the picker and prompt/name sheets are surface interactions.
W5_3_STATUS = (
    "implemented; local gates green 2026-08-20; "
    "interactive CI + human AT pending"
)

W5_3_DELIVERED_COMMANDS = {
    "slate.file.newFromTemplate",
}

# W5-4 (#744): file management — the verbs, the Move-To picker,
# structural undo, and the two-platform mutation harness. Bulk
# PROPERTY rename shipped in W4-4; the file verbs are this issue.
W5_4_STATUS = (
    "implemented; local gates green 2026-08-21; "
    "interactive CI + human AT pending"
)

W5_4_DELIVERED_COMMANDS = {
    "slate.file.newNote",
    "slate.file.newFolder",
    "slate.file.rename",
    "slate.file.moveTo",
    "slate.file.delete",
    "slate.file.duplicate",
    "slate.file.copyPath",
    "slate.file.revealInFinder",
}

# W6-1 (#745): the canvas, as a STACKED SERIES — so this set grows one
# PR at a time rather than once at the issue's close. The rule the
# series adopted with PR B: a surface command joins the set in the PR
# that makes it executable, not in the PR that registers it. PR A
# registered all three `show*` rows and enabled `showOutline` when the
# outline projection shipped; PR B enabled `showTable` with the table
# projection. `showVisual` stays out until PR D ships the renderer,
# where its resolver stops answering CanExecute false.
W6_1_STATUS = (
    "implemented; local gates green 2026-09-02; "
    "interactive CI + human AT pending"
)

W6_1_DELIVERED_COMMANDS = {
    # PR A / PR B / §D TD-6: the three projections that exist.
    "slate.canvas.showOutline",
    "slate.canvas.showTable",
    "slate.canvas.showVisual",
    # §D TD-6: the viewport verbs, executable with the renderer — B12's
    # rule (a command joins this set in the PR that makes it
    # EXECUTABLE) finally paying out for the rows PR C registered.
    "slate.canvas.zoomIn",
    "slate.canvas.zoomOut",
    "slate.canvas.actualSize",
    "slate.canvas.fitCanvas",
    "slate.canvas.zoomToSelection",
    "slate.canvas.toggleFollowSelection",
    # PR C: the navigator command layer, the filter, Where-am-I and the
    # mode transitions.
    #
    # `commitMode` and `cancelMode` are absent for the SAME reason, and
    # the split is what made that true. They gate on `CanCommitOrCancel`,
    # so they execute the moment a mode is running — but nothing in the
    # shipped code ENTERS a mode: the entrants are PR F's (move, resize,
    # connect), and C-lite ships the machine, not a way in. The M1-M7
    # conformance suite drives a TEST mode, which is exactly the thing
    # §B12's rule distinguishes from executable. Both rows return here
    # with F, which is the PR that makes them reachable.
    "slate.canvas.whereAmI",
    "slate.canvas.nextCard",
    "slate.canvas.previousCard",
    "slate.canvas.enterGroup",
    "slate.canvas.exitGroup",
    "slate.canvas.followConnectionForward",
    "slate.canvas.followConnectionBack",
    "slate.canvas.tracePath",
    "slate.canvas.filterCards",
    "slate.canvas.clearFilter",
    # §G2 TG2-8 (G2-14, IG2-3): every EXECUTABLE canvas id joins in the
    # PR that made it executable. §E's five verbs shipped without a
    # front door and §G2 gave them one; the §G2 residue, the card and
    # canvas creators, §F's thirteen (the modes, the placements, the
    # resizes — `commitMode` and `cancelMode` return here as promised
    # above, F being the PR that made them reachable) and §G's six
    # including `colorMarked`. Evidence groups canvasMutations,
    # canvasModes and canvasMarks in chords.json carry the anchors.
    "slate.canvas.delete",
    "slate.canvas.editCard",
    "slate.canvas.renameGroup",
    "slate.canvas.setColor",
    "slate.canvas.clearColor",
    "slate.canvas.newGroup",
    "slate.canvas.addLink",
    "slate.canvas.moveIntoGroup",
    "slate.canvas.editConnection",
    "slate.canvas.deleteConnection",
    "slate.canvas.addNote",
    "slate.canvas.addMedia",
    "slate.canvas.locateFile",
    "slate.canvas.removeFromGroup",
    "slate.canvas.createConnectedCard",
    "slate.canvas.createConnectedCardDirectional",
    "slate.canvas.duplicate",
    "slate.canvas.convertToNote",
    "slate.canvas.newCard",
    "slate.file.newCanvas",
    "slate.canvas.moveMode",
    "slate.canvas.resizeMode",
    "slate.canvas.connectMode",
    "slate.canvas.commitMode",
    "slate.canvas.cancelMode",
    "slate.canvas.placeBelow",
    "slate.canvas.placeRightOf",
    "slate.canvas.placeAbove",
    "slate.canvas.placeLeftOf",
    "slate.canvas.alignWith",
    "slate.canvas.connectTo",
    "slate.canvas.resizeDefaultSize",
    "slate.canvas.resizeFitContent",
    "slate.canvas.toggleMark",
    "slate.canvas.showMarks",
    "slate.canvas.clearMarks",
    "slate.canvas.deleteMarked",
    "slate.canvas.groupMarked",
    "slate.canvas.colorMarked",
}

# W4 delivery, same per-command shape as W3.
# slate.editor.togglePropertiesSource stays PENDING: YAML source mode
# was scoped out of W4-4 (no set_frontmatter_source call site) — the
# deferral is recorded in docs/plans/22_property_panel_contracts.md.
W4_DELIVERED_COMMANDS = {
    "slate.tasks.review",
    "slate.editor.addProperty",
    "slate.editor.bulkRenameProperties",
    # W4-5 (#737)
    "slate.navigation.jumpToBibliography",
    "slate.editor.citationSummary",
}
# W4-6 (#738)
W4_DELIVERED_COMMANDS |= W4_6_COMMANDS
# W4-7 (#739)
W4_DELIVERED_COMMANDS |= W4_7_COMMANDS
# W4-8 (#740)
W4_DELIVERED_COMMANDS |= W4_8_COMMANDS

# §W-F waivers: status text the generator must preserve across
# regeneration — a waiver that lives only in the generated file is
# silently erased by the next run.
COMMAND_STATUS_OVERRIDES = {
    "slate.file.printNote": (
        "**§W-F waiver** — out of W3-1 (owner, 2026-07-25); a second "
        "composition path, as mac shows by routing print through a separate "
        "`ReadingPrintComposer` that re-segments through core. Needs its own "
        "unit; tracked, not unshipped."
    ),
}


_DECLARATION_HEAD = re.compile(
    r"^[ \t]*(?:\[[^\]]*\][ \t]*)*(?:public|internal|private|protected)\b[^;{=(]*?\b(?P<name>\w+)[ \t]*(?:\(|\{|=>|=|;|\r?$)",
    re.MULTILINE,
)
_TYPE_HEAD = re.compile(r"\b(?:class|record|interface|enum|struct)[ \t]+(?P<name>\w+)\b")


_CODE_ONLY = re.compile(r'//[^\n]*|/\*.*?\*/|"(?:[^"\\\n]|\\.)*"', re.S)


def declares(text: str, marker: str) -> bool:
    """Whether `marker` is DECLARED in the C# text: a type head, or an
    accessible member head (a method, constructor, property, event or
    field) whose name is the marker. A name that appears only as a
    call, an argument, a comment or a string literal does not declare —
    comments and strings are stripped before the heads are read (review
    round 1, IH-58)."""
    text = _CODE_ONLY.sub("", text)
    for m in _TYPE_HEAD.finditer(text):
        if m.group("name") == marker:
            return True
    for m in _DECLARATION_HEAD.finditer(text):
        if m.group("name") == marker:
            return True
    return False


def load_delivery_evidence(
    cmd_rows: list[tuple[str, str, str, str, str]],
) -> dict[str, dict[str, str]]:
    """Load and validate explicit delivered-issue evidence.

    Status is evidence-driven, never inferred from an issue-number prefix. The
    exact command-key comparison is intentional: a new delivered inventory row makes
    generation fail until a reviewer maps it to checked implementation and
    test anchors.
    """
    try:
        catalog = json.loads(WINDOWS_CHORDS.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exception:
        fail(f"could not load Windows chord/evidence catalog: {exception}")

    evidence = catalog.get("deliveryEvidence")
    if not isinstance(evidence, dict):
        fail("chords.json has no deliveryEvidence object")
    groups = evidence.get("groups")
    command_map = evidence.get("commands")
    issue_map = evidence.get("issues")
    if not all(isinstance(value, dict) for value in (groups, command_map, issue_map)):
        fail("deliveryEvidence groups, commands, and issues must be objects")

    for group_name, group in groups.items():
        if not isinstance(group, dict):
            fail(f"delivery-evidence group {group_name!r} must be an object")
        for kind in ("implementation", "tests"):
            references = group.get(kind)
            if not isinstance(references, list) or not references:
                fail(f"delivery-evidence group {group_name!r} has no {kind} references")
            for reference in references:
                if not isinstance(reference, str) or "#" not in reference:
                    fail(f"invalid delivery-evidence reference: {reference!r}")
                relative, marker = reference.split("#", 1)
                path = REPO / relative
                if not path.is_file():
                    fail(f"delivery-evidence file does not exist: {relative}")
                text = path.read_text(encoding="utf-8")
                if not marker or marker not in text:
                    fail(f"delivery-evidence marker {marker!r} missing from {relative}")
                # (13) W6-1 §H TH-9 (H7, IH-19, IH-45): a marker into C# names a
                # DECLARATION — a type, a method, a constructor, a property, an
                # event or a field — not a substring that could live in a
                # comment or an unrelated call.
                # A TEST anchor may instead name an automation id the journey
                # drives — a quoted string the file reads, never a comment.
                if relative.endswith(".cs") and not declares(text, marker) and not (
                    kind == "tests" and f'"{marker}"' in text
                ):
                    fail(f"delivery-evidence marker {marker!r} is not a declaration in {relative}")

    delivered_commands = {
        cid for cid, _, _, _, issue in cmd_rows
        if issue.startswith(("#720", "#721", "#722", "#723", "#724", "#725"))
    } | W3_DELIVERED_COMMANDS | W4_DELIVERED_COMMANDS | W5_2_DELIVERED_COMMANDS \
        | W5_3_DELIVERED_COMMANDS | W5_4_DELIVERED_COMMANDS | W6_1_DELIVERED_COMMANDS
    mapped_commands = set(command_map)
    if mapped_commands != delivered_commands:
        missing = sorted(delivered_commands - mapped_commands)
        extra = sorted(mapped_commands - delivered_commands)
        fail(f"delivery-evidence command drift: missing={missing}, extra={extra}")

    for command_id, group_name in command_map.items():
        if group_name not in groups:
            fail(f"command {command_id} references unknown evidence group {group_name!r}")

    expected_issues = {
        "#381", "#720", "#721", "#722", "#723", "#724", "#725", "#728", "#735", "#736",
        "#737", "#738", "#739", "#740", "#741", "#742", "#743", "#744",
        # W6-1 §H TH-9 (H7): the canvas issue, evidenced by the `canvas`
        # aggregate group — anchors from every command group and the
        # close-out gates (validation 14 makes the aggregate complete).
        "#745",
    }
    if set(issue_map) != expected_issues:
        fail(
            "delivery-evidence issue drift: expected "
            f"{sorted(expected_issues)}, got {sorted(issue_map)}"
        )
    for issue, group_name in issue_map.items():
        if group_name not in groups:
            fail(f"issue {issue} references unknown evidence group {group_name!r}")
        # (14) W6-1 §H TH-9 (H7, IH-18): an issue's group is SCOPE-COMPLETE —
        # for every command group any of the issue's commands maps to, the
        # issue's group carries one of that group's implementation anchors
        # and one of its test anchors; one command group never stands for
        # the issue.
        issue_number = issue.split(" ", 1)[0]
        command_groups = {
            command_map[cid]
            for cid, _, _, _, row_issue in cmd_rows
            if row_issue.startswith(issue_number) and cid in command_map
        }
        for command_group in sorted(command_groups - {group_name}):
            for kind in ("implementation", "tests"):
                if not set(groups[group_name][kind]) & set(groups[command_group][kind]):
                    fail(
                        f"issue {issue} group {group_name!r} carries no {kind} anchor "
                        f"of command group {command_group!r}"
                    )

    return {"commands": command_map, "issues": issue_map}


def command_delivery_status(
    command_id: str,
    issue: str,
    evidence: dict[str, dict[str, str]],
) -> str:
    if command_id in COMMAND_STATUS_OVERRIDES:
        return COMMAND_STATUS_OVERRIDES[command_id]
    if command_id not in evidence["commands"]:
        return "pending"
    if command_id in W3_DELIVERED_COMMANDS:
        return W3_IMPLEMENTED_STATUS
    if command_id in W4_DELIVERED_COMMANDS:
        return W4_STATUS_BY_COMMAND.get(command_id, W4_IMPLEMENTED_STATUS)
    if command_id in W5_2_DELIVERED_COMMANDS:
        return W5_2_STATUS
    if command_id in W5_3_DELIVERED_COMMANDS:
        return W5_3_STATUS
    if command_id in W5_4_DELIVERED_COMMANDS:
        return W5_4_STATUS
    if command_id in W6_1_DELIVERED_COMMANDS:
        return W6_1_STATUS
    return (
        W2_IMPLEMENTED_STATUS
        if issue.startswith(("#381", "#724", "#725"))
        else IMPLEMENTED_STATUS
    )


def issue_delivery_status(
    issue: str,
    evidence: dict[str, dict[str, str]],
) -> str:
    issue_number = issue.split(" ", 1)[0]
    if issue_number not in evidence["issues"]:
        return "pending"
    if issue_number in {"#381", "#724", "#725"}:
        return W2_IMPLEMENTED_STATUS
    if issue_number == "#741":
        return W5_1_STATUS
    if issue_number == "#742":
        return W5_2_STATUS
    if issue_number == "#743":
        return W5_3_STATUS
    if issue_number == "#745":
        return W6_1_STATUS
    return IMPLEMENTED_STATUS


def main() -> int:
    cmd_rows = commands()
    delivery_evidence = load_delivery_evidence(cmd_rows)
    if "--validate-delivery-evidence" in sys.argv:
        print(
            "delivery evidence verified "
            f"({len(delivery_evidence['commands'])} command rows, "
            f"{len(delivery_evidence['issues'])} issue surfaces)"
        )
        return 0
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
        a(f"| `{cid}` | {label or '—'} | {chord or '—'} | {spoke or '—'} | {issue} | {command_delivery_status(cid, issue, delivery_evidence)} |")
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
        status = LEAF_DELIVERED.get(leaf, "pending")
        a(f"| `{leaf}` | {issue} | {status} |")
    a("")
    a("## Workspace persisted tab-content kinds (`enum EditorItem`)")
    a("")
    a("What `WorkspaceStore` round-trips — a **separate** inventory from the "
      "right-pane leaves above. Includes the U1-6 forward-compatibility "
      "contract: an unknown discriminator drops that tab, never the "
      "workspace (W1-3 mirrors it; cross-platform round-trip fixtures are "
      "W1-3 acceptance).")
    a("")
    a("| tab kind | consuming W issue | status |")
    a("|---|---|---|")
    for kind in editor_item_kinds():
        issue = "#722 (W1-3)"
        a(f"| `{kind}` | {issue} | {issue_delivery_status(issue, delivery_evidence)} |")
    a("")
    a("## Primary surfaces")
    a("")
    a("| surface | source | consuming W issue | status |")
    a("|---|---|---|---|")
    a(f"| App shell, window chrome, vault lifecycle | `SlateMacApp.swift` | #720 (W1-1) | {issue_delivery_status('#720 (W1-1)', delivery_evidence)} |")
    a(f"| Files sidebar (tree CRUD, filter, tags, pins, shortcuts, folder notes) | `FileTreeSidebar.swift` + FL program | #721 (W1-2) | {issue_delivery_status('#721 (W1-2)', delivery_evidence)} |")
    a(f"| Workspace: tabs, splits, leaves, persistence, focus routing | `Workspace/` | #722 (W1-3) | {issue_delivery_status('#722 (W1-3)', delivery_evidence)} |")
    a(f"| Quick switcher | `QuickSwitcherModel.swift` (core ranking, W0.5-2) | #723 (W1-4) | {issue_delivery_status('#723 (W1-4)', delivery_evidence)} |")
    a(f"| Editor host (AvalonEdit ⇄ DocumentBuffer, undo, save, IME) | `NoteEditorView.swift` | #724 (W2-1) | {issue_delivery_status('#724 (W2-1)', delivery_evidence)} |")
    a(f"| Editor canonical spans | #381 span API consumers | #381 (W2-2) | {issue_delivery_status('#381 (W2-2)', delivery_evidence)} |")
    a(f"| In-editor interactions (links, tags, citations, embeds, checkboxes) | `NoteEditorView.swift` | #725 (W2-3) | {issue_delivery_status('#725 (W2-3)', delivery_evidence)} |")
    a("| Reading view (block model, mode toggle, heading/link AT nav, print) | `Reading/` | #728 (W3-1) | implemented (PR #1052, merged 2026-07-27; NVDA field-verified 2026-07-26/27; print §W-F-waived) |")
    a("| Math rendering + canonical speech/braille artifact | core `math.rs` consumers | #729 (W3-2) | implemented (PR #1057, merged 2026-07-28; human AT pending) |")
    a("| Diagrams (canonical Rust SVG + description) | core `diagram.rs` consumers | #730 (W3-3) | implemented (PR #1058, merged 2026-07-29; human AT pending) |")
    a("| Code blocks (canonical tokens + AT preamble) | `CodeBlockView.swift` | #731 (W3-4) | implemented (PR #1054, merged 2026-07-27; human AT pending) |")
    a("| Embeds across contexts | editor/reading embeds | #732 (W3-5; XD rows dropped) | implemented (PR #1059, merged 2026-07-30; human AT pending; `.base` row closed by W4-6's layered embed card, D-15) |")
    a("| Accessible grid substrate | `AccessibleDataGrid.swift` | #733 (W4-1) | pending |")
    a("| Properties (in-note header, panel, typed rows, add-property) | `Properties*` views | #736 (W4-4) | pending |")
    a("| Bases grid + builder (N shipped) | `Bases/` | #738 (W4-6) | implemented; local gates green 2026-08-08; interactive CI + human AT pending |")
    a(f"| Command palette | `CommandPaletteModel.swift` (core ranking, W0.5-1) | #741 (W5-1) | {issue_delivery_status('#741 (W5-1)', delivery_evidence)} |")
    a(f"| Search overlay | search UI over `full_text_search` | #742 (W5-2) | {issue_delivery_status('#742 (W5-2)', delivery_evidence)} |")
    a(f"| Templates picker + prompt flow | template views | #743 (W5-3) | {issue_delivery_status('#743 (W5-3)', delivery_evidence)} |")
    a(
        "| File management + bulk rename | sidebar/file commands | #744 (W5-4) | "
        "implemented; local gates green 2026-08-21; interactive CI + human AT "
        "pending (bulk PROPERTY rename shipped in W4-4; the W5-4 scope is the "
        "file verbs, the Move-To picker, structural undo, and the mutation "
        "harness) |"
    )
    a(f"| Accessible canvas (T parity) | `Canvas/` | #745 (W6-1) | {issue_delivery_status('#745 (W6-1)', delivery_evidence)} |")
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
