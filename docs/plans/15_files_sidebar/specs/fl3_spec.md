# FL3 executable spec — Organization: sort, grouping, pins, shortcuts, recents, navigation polish

Issues: FL3-1 ([#658](https://github.com/coryj627/slate/issues/658)) · FL3-2 ([#659](https://github.com/coryj627/slate/issues/659)) · FL3-3 ([#660](https://github.com/coryj627/slate/issues/660)) · FL3-4 ([#661](https://github.com/coryj627/slate/issues/661)). Milestone: [GH 31](https://github.com/coryj627/slate/milestone/31). Grouped delivery: FL-06 closes #658 and #659 after FL-02; FL-07 closes #660 and #661 after FL-06.
Program: [00_program.md](../00_program.md) (locked decisions 5–6, 15; DoD §FL-A/§FL-E). U §A–§G apply.

Baseline facts (verified 2026-07-14 at `origin/main` `6aa9fce`):

- Level ordering is dirs-then-files with case-insensitive name order. Levels are small after lazy fetch, so FL-06 builds total app-side sort keys once per row; row bodies do not allocate formatters or query SQLite.
- FL-02 supplies the AppState-owned `SidebarVaultPrefsStore`; FL-06 adds the first authored organization keys. Device-local view state remains in typed UserDefaults preferences.
- `CommandRegistry`, `SlateCommandID`, and `CommandSection` already back palette/menu/utility surfaces. FL navigation commands use the shared `.sidebar` section and AppState action funnels; no chord may be the only path.
- `FileRecentsStore` currently retains 50 paths in vault-local `.slate/file-recents.json` for Quick Switcher. FL-07 migrates that history into one device-local store rather than creating a second sidebar-only ring.
- Announce seam + `TreeMutation` conventions per fl2 baseline. Section/heading AX conventions: List section headers must be real headers (VO rotor), not styled text rows.

---

## FL3-1 · Sort options + date grouping (#658) — closing PR FL-06

1. **Sort** applies to the *file* portion of each level (dirs stay first, name-ordered — mixing folders into date sorts makes the tree unpredictable for AT users): `name | created | modified`, each `asc | desc`. Name sorts on the FL1-1 *effective* label (display_name ?? stem), case-insensitive, locale-aware (`localizedStandardCompare`), NFC keys per the #459 convention. A created sort key uses `created_date` first, converted from its authored civil components to local start-of-day in the user's calendar/timezone **for sorting only**; otherwise it uses the `created_ms` instant. Only rows with neither value are NULL-last. Tie-break name asc — total order, deterministic.
2. **Scope:** a vault-wide default (`sidebar.sort` in sidebar.json — vault-local, syncs with the vault) plus **per-folder overrides** (`sidebar.json: folderOverrides.{path}.sort`). Override set/cleared via folder context menu → "Sort" submenu (radio state; "Use Vault Default" clears). A toolbar sort button + palette commands mirror the same submenu for the selected container.
3. **Date grouping**: `sidebar.grouping ∈ {none, dateBuckets}` (default none; per-folder overridable, same mechanism). Buckets computed over the active date field (created when sorting by created, else modified), in the user's calendar/timezone: `Today, Yesterday, Previous 7 Days, Previous 30 Days, <Month YYYY>…, <YYYY>…` — buckets that would be empty are omitted; grouping forces the matching date sort desc (mixed sort+group combinations are not offered — matches Navigator and keeps AX simple).
4. Group headers render as **section headers** (AX header trait, VO rotor-navigable, not selectable, not focus stops in arrow navigation).
5. Pinned section (FL3-2) always precedes groups. Rename/create keep their select-and-reveal behavior under any sort (the U2 select-after-mutate seam re-finds the row by path, not index — verify, it's an easy regression).
6. AX: changing sort/group announces (`"Sorted by modified, newest first."`); the tree's AX summary mentions active non-default sort.

Tests: comparator total-order property (incl. `created_date` precedence over simultaneous birthtime `created_ms`, datetime instants, NULL created, case/diacritic names); date-only local-start-of-day and bucket boundaries in timezones on both sides of UTC (DST, midnight, month/year rollover — injected clock, no wall-time in tests); override precedence (folder > vault > default); header AX traits; select-after-rename under date sort.

- [ ] Comparators + per-folder override storage/menu
- [ ] Bucket computation + section headers
- [ ] Palette/menu/toolbar surfaces + announcements
- [ ] Tests; a11y 100/100 on tip

## FL3-2 · Pinned notes (#659) — closing PR FL-06

1. Pin/unpin via file context menu + palette (`"Pin to Top of Folder"` / `"Unpin"`). Storage: `sidebar.json: pins.{folderPath} = [filePath…]` (vault-local; authored order = pin order, newest appended).
2. Presentation: a **Pinned** section at the top of the file portion of that folder's level (above FL3-1 groups), rows identical to normal rows plus a pin glyph; AX value appends `", pinned"`. Pinned rows also remain in their natural position? **No** — they move to the section (one row per file; duplicates would double VO walks).
3. Pin integrity: rename/move updates pin entries via the existing `TreeMutation` stream (move to another folder drops the pin — pins are per-folder context, Navigator semantics); delete drops it; stale entries (file gone) are pruned lazily on level render, never crash. Prune rewrites sidebar.json at most once per session per folder.
4. Unpin-all appears on the folder context menu when the folder has pins.

Tests: pin order stability; rename/move/delete integrity via mutation replay; stale-prune idempotence; AX value.

- [ ] Pin storage + section rendering + glyph/AX
- [ ] Mutation-stream integrity + lazy prune
- [ ] Commands + tests; a11y 100/100

## FL3-3 · Shortcuts + one Recents history (#660) — closing PR FL-07

1. Two collapsible sections rendered **above the folder tree** inside the sidebar scroll view, order: **Shortcuts**, **Recents**, then the tree. Each is an AX-labeled group with a header (rotor-navigable); collapsed state is device-local. Empty sections render a single quiet placeholder row ("No shortcuts — right-click a file to add one") rather than disappearing (discoverability, esp. for VO users who can't see the affordance appear).
2. **Shortcuts** (vault-local, `sidebar.json: shortcuts = [{kind: file|folder, path}]`): add via context menu "Add to Shortcuts" (toggle to "Remove from Shortcuts"); reorder via drag and via context-menu Move Up/Move Down (keyboard parity — decision 15). A folder shortcut is a navigation **container**: in dual-pane mode activation selects the folder and drives the list; in single-tree mode it reveals/selects the folder. A file shortcut is a **leaf**: activation opens immediately through the normal file-open seam, then normal editor→sidebar mirroring selects its containing folder and file in dual-pane mode. It never becomes a list container. Chords: `⌃1`–`⌃9` activate shortcuts 1–9 **when sidebar has key focus** (`⌘1–9` belong to tabs); palette commands "Open Shortcut N" work regardless of focus. Integrity via `TreeMutation` replay (rename/move retarget; delete removes + one-time notice).
3. **Recents**: move the existing `FileRecentsStore` history to bounded UserDefaults data keyed by stable vault identity. Keep one most-recent-first history for Quick Switcher and sidebar: retain **50**, display the first **10 eligible** sidebar rows excluding the current file. Every Recents row is a file **leaf**, never a list container: activation opens immediately through the same normal file-open seam and then mirrors the containing folder/file selection in dual-pane mode. Every successful file open at the AppState seam prepends; duplicates move to front; rename/move retargets and unresolvable entries drop. Clear Recents clears the shared history.
4. **Migration**: read legacy `.slate/file-recents.json` once, merge/dedupe into the device-local history, and remove or mark the legacy source only after a successful durable write. Missing, malformed, oversized, or repeated migration is safe and idempotent; never keep two stores in active use.
5. Rows reuse the FL1 file-row component (display name, date) with a compact variant; folder shortcut rows show the folder icon + path subtitle when ambiguous.
6. Tags and Untagged become addable container shortcuts in FL5-2 (`kind: tag|untagged`) — the storage schema above reserves `kind` for them; FL3-3 ships file|folder only. Like folder shortcuts, these targets drive the dual-pane list; they are not file leaves.

Tests: section AX structure (headers, groups); shortcut add/remove/reorder persistence + chord dispatch under focus; folder shortcut container activation versus file shortcut leaf open/mirror; Recents leaf open/mirror and proof that neither file-leaf source becomes a list container; recents migration idempotence/corruption/oversize; two-window writes; retain-50/display-10/dedup/current exclusion; mutation retargeting both sections. Apply Apple's [Sidebars](https://developer.apple.com/design/human-interface-guidelines/sidebars), [Outline views](https://developer.apple.com/design/human-interface-guidelines/outline-views), and [Focus and selection](https://developer.apple.com/design/human-interface-guidelines/focus-and-selection) guidance without introducing custom measurements.

- [ ] Sections + placeholder rows + AX structure
- [ ] Shortcuts storage/commands/chords; one migrated Recents history
- [ ] Mutation integrity; tests; a11y 100/100

## FL3-4 · Navigation polish: collapse/expand-loaded, selection history (#661) — closing PR FL-07

1. **Collapse All** / **Expand Loaded** (palette + View menu + toolbar): collapse-all keeps ancestors of the current selection expanded (setting-free, always-on behavior — matches Navigator's default and avoids dumping VO focus onto a vanished row); expand-loaded expands fetched levels and fetches one level deeper at most (a 10k-vault full expansion is a foot-gun; announce `"Expanded loaded folders."`).
2. **Selection history**: per-window ring (cap 50) of sidebar selections (file or folder). Back/Forward via palette commands + `⌘⌃[` / `⌘⌃]` when sidebar has key focus (⌘[ / ⌘] stay with the editor). Navigating re-selects and reveals (expanding ancestors as needed via the existing reveal seam); history entries invalidated by delete are skipped. Programmatic reveals (editor mirror) don't push duplicate consecutive entries.
3. Announce: back/forward announces the newly selected row per the existing selection announce convention.

Tests: collapse-all ancestor preservation; expand-all fetch bound; history push/dedup/skip-deleted; chord scoping to sidebar focus.

- [ ] Commands + chords (focus-scoped) + menu items
- [ ] History ring + reveal integration
- [ ] Tests; a11y 100/100
