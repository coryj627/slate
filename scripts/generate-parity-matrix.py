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
                if not marker or marker not in path.read_text(encoding="utf-8"):
                    fail(f"delivery-evidence marker {marker!r} missing from {relative}")

    delivered_commands = {
        cid for cid, _, _, _, issue in cmd_rows
        if issue.startswith(("#720", "#721", "#722", "#723", "#724"))
    }
    mapped_commands = set(command_map)
    if mapped_commands != delivered_commands:
        missing = sorted(delivered_commands - mapped_commands)
        extra = sorted(mapped_commands - delivered_commands)
        fail(f"delivery-evidence command drift: missing={missing}, extra={extra}")

    for command_id, group_name in command_map.items():
        if group_name not in groups:
            fail(f"command {command_id} references unknown evidence group {group_name!r}")

    expected_issues = {"#720", "#721", "#722", "#723", "#724"}
    if set(issue_map) != expected_issues:
        fail(
            "delivery-evidence issue drift: expected "
            f"{sorted(expected_issues)}, got {sorted(issue_map)}"
        )
    for issue, group_name in issue_map.items():
        if group_name not in groups:
            fail(f"issue {issue} references unknown evidence group {group_name!r}")

    return {"commands": command_map, "issues": issue_map}


def command_delivery_status(
    command_id: str,
    evidence: dict[str, dict[str, str]],
) -> str:
    if command_id not in evidence["commands"]:
        return "pending"
    return (
        W2_IMPLEMENTED_STATUS
        if command_id == "slate.editor.save"
        else IMPLEMENTED_STATUS
    )


def issue_delivery_status(
    issue: str,
    evidence: dict[str, dict[str, str]],
) -> str:
    issue_number = issue.split(" ", 1)[0]
    if issue_number not in evidence["issues"]:
        return "pending"
    return W2_IMPLEMENTED_STATUS if issue_number == "#724" else IMPLEMENTED_STATUS


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
        a(f"| `{cid}` | {label or '—'} | {chord or '—'} | {spoke or '—'} | {issue} | {command_delivery_status(cid, delivery_evidence)} |")
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
