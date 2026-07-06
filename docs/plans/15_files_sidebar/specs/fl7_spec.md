# FL7 executable spec — Dual-pane layout option

Issues: FL7-1 ([#668](https://github.com/coryj627/slate/issues/668)) · FL7-2 ([#669](https://github.com/coryj627/slate/issues/669)). Milestone: [GH 31](https://github.com/coryj627/slate/milestone/31). One PR per issue.
Gate: Waves 2–4 complete (the list pane is FL1 rows + FL3 organization + FL4 filter — building it earlier means building it twice).
Program: [00_program.md](../00_program.md) (locked decision 12; DoD §FL-A/§FL-D). U §A–§G apply.

Baseline facts (verified 2026-07-05):

- The sidebar is one NSSplitView pane hosting `FileTreeSidebar` (single tree + FL3-3 sections + FL4-2 filter, as landed by earlier waves).
- FL4-1 `filter_files(query, scope_dir, …)` already takes a directory scope; FL5-1 `tag_tree` and FL4-2's flat list are the tag-side building blocks. `SidebarVaultPrefs` holds per-folder overrides (`folderOverrides.{path}`); device-local view state → UserDefaults.
- Focus/pane conventions: ←/→ semantics inside the tree are disclosure keys (U2); pane-level focus movement must not steal them while the tree has focus (only unhandled ← at a collapsed root row escapes to pane navigation — same escape rule List already uses for focus rings).
- DoD §FL-D: single-tree mode can do everything dual-pane can; the drift test enumerates list-pane actions vs tree actions.

---

## FL7-1 · Dual-pane container + navigation wiring (#668) — PR 1

1. Setting `sidebar.layout ∈ {tree, dualPane}` (device-local; default `tree`; Settings pane + palette toggle command). Off = zero change to the shipped experience.
2. **Navigation pane** (top or leading — vertical stack default, matching the sidebar's width): Shortcuts, Recents (FL3-3), **folders-only tree** (dirs; file rows suppressed; same VM, a presentation filter — lazy fetch unchanged), Tags section (FL5-2). **List pane**: the FL7-2 list for the selected container. Split via `VSplitView` with persisted fraction (UserDefaults), min heights, and an AX-labeled divider.
3. Selection contract: nav-pane selection (folder | tag | shortcut-target) drives the list pane; opening a *file* still goes through list-pane activation only. Editor→sidebar mirror (auto-reveal) selects the containing folder in the nav pane **and** the file row in the list pane.
4. **Focus:** each pane is an AX group with a header ("Folders", "Files"); Tab order: filter field → nav pane → list pane. → on a nav folder row that is already expanded-or-leaf moves focus to the list pane (Navigator convention); ← in the list pane returns to the nav pane; inside-tree ←/→ disclosure semantics keep priority (escape rule per baseline). VO: pane transitions announce the pane header.
5. The FL4-2 filter field stays above the nav pane and scopes to the selected container (`scope_dir` / tag query composition); active filter replaces the **list pane** contents only (nav pane stays navigable — richer than single-tree mode, where filter replaces the tree; equivalence still holds because single-tree filter covers the same reachable set).
6. Mode switching preserves state both ways (expansion, selection, pins, filter text); no re-fetch beyond what lazy levels require.

Tests: mode toggle round-trip state; folders-only presentation filter; selection contract (folder/tag/shortcut → list); focus walk incl. escape rule; VO pane announcements; split persistence.

- [ ] Setting + container + folders-only tree + panes/AX
- [ ] Selection + focus contracts; filter scoping
- [ ] Tests; a11y 100/100 on tip

## FL7-2 · List pane + per-container display overrides (#669) — PR 2

1. **List pane content** for the selected container: pinned section (FL3-2), then FL3-1 sort/group applied, FL1 rows (preview/badges/density per settings). Tag containers list `filter_files(#tag)` results with path subtitles; Untagged likewise. Empty container ⇒ quiet empty state + "New Note" affordance.
2. **Descendants toggle** (per-container, the Navigator "show notes from subfolders" feature): folder containers get a header toggle `Include subfolders` (stored `folderOverrides.{path}.descendants: Bool`, vault-local; default off). On ⇒ the pane lists the whole subtree via `filter_files` with empty-name query semantics — **FL4-1 gains a listing mode** (`query` may be empty when `scope_dir` is set; revisit the FL4-1 empty-query error to allow this one composed form, documented in the PR) — with path subtitles. Tag containers are inherently descendant-inclusive (nested semantics) — no toggle.
3. **Per-container display overrides UI**: the list-pane header menu exposes Sort, Grouping (FL3-1's existing override storage — same keys, now with a second surface), preview-lines and density overrides (`folderOverrides.{path}.previewLines/density`); "Use Vault Default" clears each. Single-tree mode reads the same overrides where applicable (sort/group already does per FL3-1; preview/density overrides apply to that folder's rows in tree mode too — one storage, two projections).
4. All row actions = the tree's actions (context menu, drag to nav-pane folders, chords); **the §FL-D drift test** lives here: an enumeration test asserting every list-pane menu/command verb exists in single-tree mode (compile-time shared verb list preferred over string comparison).
5. Multi-select (FL2-1) works in the list pane with the same batch semantics; selection is per-pane (nav selection ≠ list selection; announce conventions per pane).
6. Perf: list pane virtualizes (plain `List`); descendant listings page per FL4 conventions; 10k-subtree listing ≤ filter budget (50 ms backend + paint).

Tests: container→content matrix (folder/tag/untagged/shortcut); descendants toggle + paging; override storage round-trip + two-surface consistency (tree mode honors preview/density override); drift enumeration; multi-select batch ops in pane; empty states.

- [ ] List pane content + descendants mode (+ FL4-1 listing-mode amendment)
- [ ] Override UI + shared storage; drift test (§FL-D)
- [ ] Tests; a11y 100/100 on tip; APCA both appearances
