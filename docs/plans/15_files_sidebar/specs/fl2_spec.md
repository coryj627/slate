# FL2 executable spec — Selection & operations: multi-select, batch ops, menu completeness, templates, Finder drop

Issues: FL2-1 ([#655](https://github.com/coryj627/slate/issues/655)) · FL2-2 ([#656](https://github.com/coryj627/slate/issues/656)) · FL2-3 ([#657](https://github.com/coryj627/slate/issues/657)). Milestone: [GH 31](https://github.com/coryj627/slate/milestone/31). One PR per issue. No FL0 dependency — may start immediately.
Program: [00_program.md](../00_program.md) (locked decisions 11, 13, 14; DoD §FL-A). U §A–§G apply.

**Execution order: FL2-1 → FL2-2; FL2-3 independent after FL2-1.**

Baseline facts (verified 2026-07-05):

- Selection is single (`@State private var listSelection: RowID?`, FileTreeSidebar.swift:546); selection→open wiring :752–782; editor→tree mirror :791–810. Single-click fix uses primary `.onTapGesture` (PR #643 — onDrag swallows label clicks; do not regress).
- Drag & drop: internal moves only, private UTType `com.slate.tree-node-path` (:1273–1324, :1278).
- Context menus :1154–1169 (open actions) and :1235–1271 (file management). Inline rename `RenameField` :1558–1617 (commit/cancel/collision rules). Move sheet `MoveToFolderSheet.swift`.
- Mutations route through AppState wrappers (`createFolder` :5224, `createNote` :5261, rename/move/delete nearby); each publishes `TreeMutation` (:5176–5180, Kind enum :5082–5093) driving announcement + scoped tree invalidation; tabs retarget on rename/move, error-state on delete (U2-5). Link-rewrite skips surface as a consolidated alert.
- Templates in core: `list_templates()` (session.rs:570–577; `Templates/` default, alphabetical) and `render_template(path, ctx) -> RenderedTemplate { body, cursor_byte_offset }` (:751–758; allowlist `{{date}}`, `{{time}}`, `{{title}}`, `{{vault}}`, `{{cursor}}`, `{{prompt:Label}}`; unknown variables survive verbatim). An app-side template picker exists (M-6/U-era); locate its view for reuse at implementation.
- Scanner picks up external filesystem changes via the rescan path; `list_dir_children` is the refresh seam the tree already uses after mutations.

---

## FL2-1 · Multi-selection model (#655) — PR 1

1. `listSelection` becomes `Set<RowID>` (SwiftUI `List(selection: Binding<Set<RowID>>)`) with a separate `anchorRow: RowID?` for range semantics. Native List behavior supplies ⇧/⌘-click and ⇧↑/⇧↓; verify against the #643 tap-gesture fix — the primary-click open must fire only on single, unmodified selection.
2. **Open semantics:** single selection opens as today; a modifier/multi selection **never** auto-opens (opening 12 files because ⌘A is a bug, not a feature). Return/⌘↓ on a multi-selection opens all, gated by a confirmation ≥ 10 items.
3. ⌘A selects all **visible rows of the current level scope** (the flattened visible tree), matching List default; document it in help.
4. **AX:** selection changes announce count when > 1: `"5 selected."` (single-selection announce unchanged: `"Selected: <name>."`). The announce dedup keys on the selection set, not the last row.
5. **Multi-drag:** dragging a selected row carries the whole selection (array of path payloads in the existing private UTType); dragging an unselected row carries just it (Finder convention). Drop applies the FL2-2 batch move.
6. Programmatic single-select reveal (editor→tree mirror :791–810) still works and collapses the set to one.
7. Mixed dir+file selections are legal; capability gating is FL2-2's job.

Tests: set-selection state transitions (click, ⇧-click, ⌘-click, ⇧↑↓, ⌘A); no-auto-open on multi; announce composition; drag payload contents for selected vs unselected origin; reveal collapses set.

- [ ] `Set<RowID>` selection + anchor; open semantics; ⌘A
- [ ] Multi-drag payload
- [ ] AX announcements
- [ ] Tests incl. #643 regression

## FL2-2 · Batch operations + context-menu completeness (#656) — PR 2

**Batch operations** (enabled on multi-selection; one operation = one confirmation, one announcement, one tree refresh, one consolidated skip-alert — locked decision 11):

1. **Move** — `MoveToFolderSheet` accepts N sources; AppState gains `moveItems(_ paths: [String], to: String)` looping the existing per-item core move inside **one** Task, collecting per-item outcomes; destination-descendant-of-source guarded per item; announce `"Moved 12 items to Research."`; partial failure alert lists failures, successes stand.
2. **Delete** — confirmation names count and kinds (`"Move 3 files and 1 folder to Trash?"`); announce `"Moved 4 items to Trash."`; open tabs flip to error state per existing U2-5 behavior.
3. Menu verbs on multi-selection: Move To…, Move to Trash only (plus Open per FL2-1). Everything else (rename, duplicate, …) requires single selection — items disable, never hide (VO users must hear why-not: disabled items expose the standard AX disabled state).

**Context-menu completeness** (single selection):

4. **Duplicate** (files only): copy `<stem> 2.<ext>` (increment until free — Finder convention) in the same folder via core read+create (no link rewrite: duplicates keep their outgoing links verbatim); announce + select the copy.
   *Amendment (2026-07-12, #853):* Duplicate SHIPPED ahead of FL with a `<stem> copy.<ext>` suffix walk (not `<stem> 2.<ext>`) over exclusive-create, via context menu + File menu + palette + rotor. When FL lands, inherit the shipped naming — do not introduce the ` 2` convention alongside it. Folders remain unshipped.
5. **Reveal in Finder**: `NSWorkspace.shared.activateFileViewerSelecting` on the absolute URL (files and folders).
6. **Copy Wikilink** (markdown files): `[[stem]]` when the stem is unique vault-wide under the resolver's case-insensitive convention, else `[[<vault-relative path sans .md>]]`; uniqueness via one indexed query. **Copy Path**: vault-relative path verbatim. Both announce `"Copied."`.
7. **Name-validity warnings** in `RenameField` (and the new-folder/new-note flows): live inline warning line for `/` `:` `\0` (filesystem) and `[ ] # ^ |` (link-breaking) — warn-don't-block until commit; committing a filesystem-invalid name keeps the existing error path. AX: warning is a live region, polite.

Tests: batch move/delete outcome aggregation + announce strings; duplicate suffix walk; wikilink uniqueness both branches; warning triggers per character class; disabled-not-hidden menu state.

- [ ] `moveItems`/batch delete + consolidated alerts
- [ ] Duplicate / Reveal / Copy Wikilink / Copy Path
- [ ] Rename-field warnings (live region)
- [ ] Tests; a11y 100/100 on tip

## FL2-3 · Template creation + Finder drop-in (#657) — PR 3

1. **New Note from Template…** on folder rows + palette. Reuses the existing template picker view and `list_templates`/`render_template` (baseline facts) — no sidebar-private template list (locked decision 14). Flow: pick template → name prompt (default `Untitled`) → `render_template` with `{{title}}` = chosen name → create in folder → open → caret at `cursor_byte_offset` when present. Empty `Templates/` ⇒ item disabled with tooltip/AX hint "No templates in Templates/".
2. **Finder drop-in** (locked decision 13): accept `.fileURL` drops on folder rows and tree background (= root). Files **copy** into the vault via `FileManager` (never move; security-scoped access if sandboxed); collision → ` 2` suffix walk; directories copy recursively. After the copy batch, trigger the existing rescan/invalidation seam for the target level and announce `"Imported 3 files into Research."`. Drops of already-in-vault URLs are ignored (internal moves use the private UTType path).
3. Drop feedback: folder rows highlight on hover (existing internal-drag affordance reused); spring-loaded expansion explicitly **out of scope** (FL has no hover-timer interactions; keyboard path is Move To…).
   *Amendment (2026-07-12, #851):* superseded — drop-target highlight AND spring-loaded expansion (600ms, watchdog re-collapse) shipped via the tree-UX PR. FL inherits both; the out-of-scope premise no longer holds.
4. AX: import announce; a failed/partial import lists failures in one alert.

Tests: template render→create→caret; empty-templates disabled state; import collision suffix; directory recursion; in-vault URL ignored; partial-failure aggregation.

- [ ] Template flow wired to existing picker + core API
- [ ] `.fileURL` drop targets (rows + background), copy-in + rescan + announce
- [ ] Tests; a11y 100/100 on tip
