# FL3 executable spec — Organization: sort, grouping, pins, shortcuts, recents, navigation polish

Issues: FL3-1 ([#658](https://github.com/coryj627/slate/issues/658)) · FL3-2 ([#659](https://github.com/coryj627/slate/issues/659)) · FL3-3 ([#660](https://github.com/coryj627/slate/issues/660)) · FL3-4 ([#661](https://github.com/coryj627/slate/issues/661)). Milestone: [GH 31](https://github.com/coryj627/slate/milestone/31). One PR per issue.
FL3-1 requires FL0-2 (created dates) and FL1-1 (display names). FL3-2/3/4 require only FL1-2's `SidebarVaultPrefs` accessor.
Program: [00_program.md](../00_program.md) (locked decisions 5–6, 15; DoD §FL-A/§FL-E). U §A–§G apply.

Baseline facts (verified 2026-07-05):

- Level ordering today is fixed: dirs-then-files, case-insensitive name (FileTreeViewModel:457; API returns that order). Levels are small post-lazy-fetch (≤ page size), so re-sorting app-side is cell-cheap (locked decision 5).
- `SidebarVaultPrefs` (`.slate/sidebar.json`, versioned/atomic/forward-tolerant) ships in FL1-2 with no consumers; FL3 adds the first keys. Device-local state → UserDefaults.
- Palette registration: `CommandPaletteModel.swift`; menu bar for structural commands; no chord may be the only path (program decision 15).
- Announce seam + `TreeMutation` conventions per fl2 baseline. Section/heading AX conventions: List section headers must be real headers (VO rotor), not styled text rows.

---

## FL3-1 · Sort options + date grouping (#658) — PR 1

1. **Sort** applies to the *file* portion of each level (dirs stay first, name-ordered — mixing folders into date sorts makes the tree unpredictable for AT users): `name | created | modified`, each `asc | desc`. Name sorts on the FL1-1 *effective* label (display_name ?? stem), case-insensitive, locale-aware (`localizedStandardCompare`), NFC keys per the #459 convention. Date sorts: NULL `created_ms` sorts last, tie-break name asc — total order, deterministic.
2. **Scope:** a vault-wide default (`sidebar.sort` in sidebar.json — vault-local, syncs with the vault) plus **per-folder overrides** (`sidebar.json: folderOverrides.{path}.sort`). Override set/cleared via folder context menu → "Sort" submenu (radio state; "Use Vault Default" clears). A toolbar sort button + palette commands mirror the same submenu for the selected container.
3. **Date grouping**: `sidebar.grouping ∈ {none, dateBuckets}` (default none; per-folder overridable, same mechanism). Buckets computed over the active date field (created when sorting by created, else modified), in the user's calendar/timezone: `Today, Yesterday, Previous 7 Days, Previous 30 Days, <Month YYYY>…, <YYYY>…` — buckets that would be empty are omitted; grouping forces the matching date sort desc (mixed sort+group combinations are not offered — matches Navigator and keeps AX simple).
4. Group headers render as **section headers** (AX header trait, VO rotor-navigable, not selectable, not focus stops in arrow navigation).
5. Pinned section (FL3-2) always precedes groups. Rename/create keep their select-and-reveal behavior under any sort (the U2 select-after-mutate seam re-finds the row by path, not index — verify, it's an easy regression).
6. AX: changing sort/group announces (`"Sorted by modified, newest first."`); the tree's AX summary mentions active non-default sort.

Tests: comparator total-order property (incl. NULL created, case/diacritic names); bucket boundaries (midnight, month/year rollover — injected clock, no wall-time in tests); override precedence (folder > vault > default); header AX traits; select-after-rename under date sort.

- [ ] Comparators + per-folder override storage/menu
- [ ] Bucket computation + section headers
- [ ] Palette/menu/toolbar surfaces + announcements
- [ ] Tests; a11y 100/100 on tip

## FL3-2 · Pinned notes (#659) — PR 2

1. Pin/unpin via file context menu + palette (`"Pin to Top of Folder"` / `"Unpin"`). Storage: `sidebar.json: pins.{folderPath} = [filePath…]` (vault-local; authored order = pin order, newest appended).
2. Presentation: a **Pinned** section at the top of the file portion of that folder's level (above FL3-1 groups), rows identical to normal rows plus a pin glyph; AX value appends `", pinned"`. Pinned rows also remain in their natural position? **No** — they move to the section (one row per file; duplicates would double VO walks).
3. Pin integrity: rename/move updates pin entries via the existing `TreeMutation` stream (move to another folder drops the pin — pins are per-folder context, Navigator semantics); delete drops it; stale entries (file gone) are pruned lazily on level render, never crash. Prune rewrites sidebar.json at most once per session per folder.
4. Unpin-all appears on the folder context menu when the folder has pins.

Tests: pin order stability; rename/move/delete integrity via mutation replay; stale-prune idempotence; AX value.

- [ ] Pin storage + section rendering + glyph/AX
- [ ] Mutation-stream integrity + lazy prune
- [ ] Commands + tests; a11y 100/100

## FL3-3 · Shortcuts + Recents sections (#660) — PR 3

1. Two collapsible sections rendered **above the folder tree** inside the sidebar scroll view, order: **Shortcuts**, **Recents**, then the tree. Each is an AX-labeled group with a header (rotor-navigable); collapsed state is device-local. Empty sections render a single quiet placeholder row ("No shortcuts — right-click a file to add one") rather than disappearing (discoverability, esp. for VO users who can't see the affordance appear).
2. **Shortcuts** (vault-local, `sidebar.json: shortcuts = [{kind: file|folder, path}]`): add via context menu "Add to Shortcuts" (toggle to "Remove from Shortcuts"); reorder via drag and via context-menu Move Up/Move Down (keyboard parity — decision 15); activate = open file / reveal-and-select folder. Chords: `⌃1`–`⌃9` open shortcuts 1–9 **when sidebar has key focus** (`⌘1–9` belong to tabs); palette commands "Open Shortcut N" work regardless of focus. Integrity via `TreeMutation` replay (rename/move retarget; delete removes + one-time notice).
3. **Recents** (device-local, UserDefaults ring buffer, cap 10): every successful file open (any surface — tab open seam in AppState, not the tree click handler, so palette/search opens count) prepends; duplicates move to front; current file excluded from display. Activate = open. Clear Recents command. Rename/move retargets via mutation stream; unresolvable entries drop silently.
4. Rows reuse the FL1 file-row component (display name, date) with a compact variant; folder shortcut rows show the folder icon + path subtitle when ambiguous.
5. Tags become addable shortcuts in FL5-2 (kind: tag) — the storage schema above reserves `kind` for it; FL3-3 ships file|folder only.

Tests: section AX structure (headers, groups); shortcut add/remove/reorder persistence + chord dispatch under focus; recents ring semantics (dedup, cap, exclusion); mutation retargeting both sections.

- [ ] Sections + placeholder rows + AX structure
- [ ] Shortcuts storage/commands/chords; Recents ring
- [ ] Mutation integrity; tests; a11y 100/100

## FL3-4 · Navigation polish: collapse/expand-all, selection history (#661) — PR 4

1. **Collapse All** / **Expand All** (palette + View menu + toolbar): collapse-all keeps ancestors of the current selection expanded (setting-free, always-on behavior — matches Navigator's default and avoids dumping VO focus onto a vanished row); expand-all expands **fetched** levels and fetches one level deeper at most (a 10k-vault full expansion is a foot-gun; announce `"Expanded loaded folders."`).
2. **Selection history**: per-window ring (cap 50) of sidebar selections (file or folder). Back/Forward via palette commands + `⌘⌃[` / `⌘⌃]` when sidebar has key focus (⌘[ / ⌘] stay with the editor). Navigating re-selects and reveals (expanding ancestors as needed via the existing reveal seam); history entries invalidated by delete are skipped. Programmatic reveals (editor mirror) don't push duplicate consecutive entries.
3. Announce: back/forward announces the newly selected row per the existing selection announce convention.

Tests: collapse-all ancestor preservation; expand-all fetch bound; history push/dedup/skip-deleted; chord scoping to sidebar focus.

- [ ] Commands + chords (focus-scoped) + menu items
- [ ] History ring + reveal integration
- [ ] Tests; a11y 100/100
