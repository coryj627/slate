# FL7 executable spec — Dual-pane layout option

Issues: FL7-1 ([#668](https://github.com/coryj627/slate/issues/668)) · FL7-2 ([#669](https://github.com/coryj627/slate/issues/669)). Milestone: [GH 31](https://github.com/coryj627/slate/milestone/31). Grouped delivery: FL-13 closes #668 after FL-07, FL-09, and FL-11; FL-14 closes #669 after FL-06 and FL-13.
Program: [00_program.md](../00_program.md) (locked decision 12; DoD §FL-A/§FL-D). U §A–§G apply.

Delivery baseline (reconciled 2026-07-14):

- At FL-13's base, the sidebar remains one NSSplitView pane hosting the single-tree assembly plus FL-07 sections and FL-09's topmost filter.
- FL-08 already supports validated scoped empty-query listing; FL-10's `tag_tree` and FL-09's shared flat list are the tag-side building blocks. `SidebarVaultPrefsStore` holds per-folder overrides; device-local view state uses typed UserDefaults preferences.
- Focus/pane conventions: ←/→ semantics inside the tree are disclosure keys (U2); pane-level focus movement must not steal them while the tree has focus (only unhandled ← at a collapsed root row escapes to pane navigation — same escape rule List already uses for focus rings).
- DoD §FL-D: single-tree mode can do everything dual-pane can; the drift test enumerates list-pane actions vs tree actions.
- Interaction review uses Apple's official [Sidebars](https://developer.apple.com/design/human-interface-guidelines/sidebars), [Outline views](https://developer.apple.com/design/human-interface-guidelines/outline-views), and [Focus and selection](https://developer.apple.com/design/human-interface-guidelines/focus-and-selection) guidance; the spec adds no custom HIG measurements.

---

## FL7-1 · Internally gated dual-pane container (#668) — closing PR FL-13

1. Add device-local `sidebar.layout ∈ {tree, dualPane}` with default `tree`, but keep it behind an **internal/test gate** in FL-13. Do not expose a Settings control, public palette command, or other user-facing toggle until FL-14's complete list pane and equivalence tests pass. Tree mode performs zero extra work and preserves shipped behavior.
2. **Navigation pane** (top or leading — vertical stack default, matching the sidebar's width): Shortcuts, Recents (FL3-3), **folders-only tree** (dirs; file rows suppressed; same VM, a presentation filter — lazy fetch unchanged), Tags section (FL5-2). **List pane**: the FL7-2 list for the selected container. Split via `VSplitView` with persisted fraction (UserDefaults), min heights, and an AX-labeled divider.
3. Selection contract: direct folder/tag/Untagged rows and shortcuts targeting those scopes are nav-pane **containers**; selecting or activating one drives the list pane. File shortcuts and Recents rows are **leaves**, not containers: focusing/selecting one does not replace list contents, and activation opens it immediately through the normal file-open seam. The resulting editor→sidebar mirror selects the containing folder in the nav pane **and** the opened file row in the list pane. The same mirror applies to files opened elsewhere.
4. **Focus:** each pane is an AX group with a header ("Folders", "Files"); Tab order: filter field → nav pane → list pane. → on a selected container row that is already expanded or has no disclosure children moves focus to the list pane (Navigator convention); → on a file shortcut or Recent leaf does not enter or retarget the list, and Return activates the leaf. ← in the list pane returns to the selected container in the nav pane; inside-tree ←/→ disclosure semantics keep priority (escape rule per baseline). VO: pane transitions announce the pane header.
5. The FL4-2 filter field stays above the nav pane and scopes to the selected container (`scope_dir` / tag query composition); active filter replaces the **list pane** contents only (nav pane stays navigable — richer than single-tree mode, where filter replaces the tree; equivalence still holds because single-tree filter covers the same reachable set).
6. Mode switching preserves state both ways (expansion, selection, pins, filter text); no re-fetch beyond what lazy levels require.

Tests: internal-toggle round trip and zero extra tree-mode work; folders-only projection; container selection matrix (folder/tag/Untagged and their shortcuts → list); leaf matrix (file shortcut/Recent focus does not retarget, activation uses normal open then mirrors containing folder + file); focus walk including leaf Return, container-only → transfer, and disclosure escape priority; VO pane announcements; split persistence.

- [ ] Internal gate + container + folders-only tree + panes/AX
- [ ] Selection + focus contracts; filter scoping
- [ ] Tests; a11y 100/100 on tip

## FL7-2 · Complete list pane, overrides, and public setting (#669) — closing PR FL-14

1. **List pane content** for the selected container: pinned section (FL3-2), then FL3-1 sort/group applied, FL1 rows (preview/badges/density per settings). Folder, tag, and Untagged shortcuts resolve to the same container listing as their direct navigation rows; tag containers list `filter_files(#tag)` results with path subtitles and Untagged uses its reserved scope. File shortcuts and Recents rows never supply list-pane content. Empty container ⇒ quiet empty state + "New Note" affordance.
2. **Descendants toggle** (per-container): folder containers get `Include subfolders` (`folderOverrides.{path}.descendants`, vault-local; default off). On uses FL-08's already-landed validated scoped empty-query listing with path subtitles. Tag containers are inherently descendant-inclusive; no toggle.
3. **Per-container display overrides UI**: the list-pane header menu exposes Sort, Grouping (FL3-1's existing override storage — same keys, now with a second surface), preview-lines and density overrides (`folderOverrides.{path}.previewLines/density`); "Use Vault Default" clears each. Single-tree mode reads the same overrides where applicable (sort/group already does per FL3-1; preview/density overrides apply to that folder's rows in tree mode too — one storage, two projections).
4. All row actions = the tree's actions (context menu, drag to nav-pane folders, chords); **the §FL-D drift test** lives here: an enumeration test asserting every list-pane menu/command verb exists in single-tree mode (compile-time shared verb list preferred over string comparison).
5. Multi-select (FL2-1) works in the list pane with the same batch semantics; selection is per-pane (nav selection ≠ list selection; announce conventions per pane).
6. Expose the tree/dual-pane Settings control and palette command only after the complete container/content matrix, shared-action equivalence, and tree-mode regression tests pass. Mode switching preserves state.
7. Perf: list pane virtualizes/pages; descendant listings use FL-08 conventions; 10k-subtree listing stays within the filter budget.

Tests: container→content matrix (folder/tag/Untagged direct rows and container shortcuts); file-shortcut/Recent leaf non-container regression + normal-open mirror; descendants toggle + paging; override storage round-trip + two-surface consistency (tree mode honors preview/density override); drift enumeration; multi-select batch ops in pane; empty states.

- [ ] List pane content + descendants mode using FL-08 listing contract
- [ ] Override UI + shared storage; drift test (§FL-D)
- [ ] Publish Settings/palette toggle only after equivalence gates
- [ ] Tests; a11y 100/100 on tip; APCA both appearances
