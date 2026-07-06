# 15 — Files Sidebar Navigator Program (Milestone FL): a Notebook Navigator-class sidebar, accessible first

**Status:** 📝 Specs locked (2026-07-05); implementation not started. GH [milestone 31](https://github.com/coryj627/slate/milestone/31), issues [#650–#670](https://github.com/coryj627/slate/milestone/31) (FL0: #650–652 · FL1: #653–654 · FL2: #655–657 · FL3: #658–661 · FL4: #662–663 · FL5: #664–666 · FL6: #667 · FL7: #668–669 · docs: #670). Evidence base: Notebook Navigator gap analysis (this session, 2026-07-05) — feature inventory from [notebook-navigator README + docs/](https://github.com/johansan/notebook-navigator) vs. the U2 file tree as shipped (`../08_ui_parity/specs/u2_spec.md`, PRs #493–#513).

**Strategic goal.** The U2 file tree is a correct, accessible *file browser*: lazy per-level fetch, full VoiceOver coverage, safe rename/move with link rewrite, tab retargeting. What it is not — and what Obsidian users get from the Notebook Navigator plugin — is a *navigation surface driven by note metadata*: rows that show what a note **is** (title, dates, tasks, length, preview) rather than what its file is called; organization tools (sort, group, pin, shortcuts, recents); a filter that turns 10k files into the eight you mean; and first-class tag browsing. This program closes that gap. Notebook Navigator is the **capability reference, not the interaction reference** (same stance as Canvas `../09_canvas/00_program.md` and Graph `../11_graph/00_program.md`): every feature here must be fully operable keyboard-only and with VoiceOver *as the primary design target*, not as a compliance pass at the end.

Everything here inherits the UI-parity Presentation-Ready DoD (`../08_ui_parity/00_program.md` §A–§G): a11y-check 100/100 gated on each PR's own tip, APCA Lc ≥ 75 measured in both appearances, census-gated invariants, atomic writes, one PR per issue, fmt/clippy pre-push. This document adds only what is sidebar-specific.

---

## Locked scope decisions (owner review, 2026-07-05)

| # | Area | Decision |
|---|------|----------|
| 1 | Scope source | The prioritized gap list from the 2026-07-05 Notebook Navigator analysis, items 1–12, 14–17, 21. **Dropped by owner decision:** hidden/excluded folders (item 13), per-folder/tag icons & colors (18), feature images/thumbnails (19), user-facing theming system (20), vault profiles + per-setting sync granularity (23). Do not re-propose these inside FL; if they return it is as their own milestone. |
| 2 | Parked elsewhere | Sidebar-control API (item 22) → **Milestone EX — External API and Extensions** ([milestone 32](https://github.com/coryj627/slate/milestone/32), scoping issue [#671](https://github.com/coryj627/slate/issues/671)). Calendar/daily-notes pane (item 24) → **Milestone PS — Planner and Scheduling** ([milestone 33](https://github.com/coryj627/slate/milestone/33), scoping issue [#672](https://github.com/coryj627/slate/issues/672)). No FL work may take a dependency on either. |
| 3 | Build order | **Metadata backbone first.** FL0 (derived note metadata in core + FFI) → FL1 (row presentation) unblocks the highest-priority wins (frontmatter display names, dates). FL2 (multi-select + operations) runs in parallel — it is pure app-side and gates every batch feature. Filter (FL4) and tags (FL5) ride FL0's data and FL4's presentation respectively. Dual-pane (FL7) is last: it is the biggest structural change and only pays off once rows are rich enough to justify a dedicated list pane. |
| 4 | Derived metadata | One scanner-owned pipeline: display name (frontmatter `title`), created timestamp (frontmatter override > filesystem), task counts (total/unfinished), word count, preview text. Computed on the existing scan/save paths, persisted in SQLite beside the file row, **O(changed-file)** maintenance, census-gated (incremental ≡ full rescan). Derived data is never user-persisted state — it regenerates from source (same invariant Notebook Navigator's storage docs enforce, and same stance as Milestone M sync: derived state never syncs). |
| 5 | Sorting/grouping locus | Backend ships raw fields; **the app sorts and groups per level** (a tree level is ≤ a few hundred rows after lazy fetch — no need to push ORDER BY variants through FFI). Per-folder overrides are UI prefs, not schema. |
| 6 | Sidebar prefs persistence | Two tiers, mirroring the graph.json precedent: **vault-local** `.slate/sidebar.json` (versioned, atomic temp+rename, same convention as `.slate/graph.json` / `prefs.json`) for pins, shortcuts, per-folder sort/display overrides, folder-note assignments — things that describe the *vault*; **device-local** (UserDefaults) for view state — expansion, selection, recents, pane sizes, last filter. Nothing derived is persisted in either. |
| 7 | Filter presentation | An active filter replaces the tree with a **flat result list** (path-annotated rows), Navigator-style — not a pruned tree. Pruned trees force VoiceOver users to walk empty scaffolding; a flat list announces "N results" and puts every hit one arrow apart. Esc clears and restores the tree with prior expansion/selection intact. |
| 8 | Filter language | The Navigator subset that maps to data Slate already indexes: bare words = name match (AND), `#tag` (+ nested prefix), `@today`/`@yesterday`/`@last7d`/`@last30d`/`@YYYY-MM-DD`, `has:task`, `ext:pdf`, `-` prefix negation. Parsed in **slate-core** (one grammar for sidebar, CLI, and future surfaces), executed against SQLite. Full-text content search stays in the ⌘F overlay — the sidebar filter is a *metadata* filter. |
| 9 | Tag tree | Tags section renders the nested tag hierarchy (`/`-split) with counts and an Untagged row, from the existing tags/file_tags data (tag-scope semantics per #564–#567 — **do not re-litigate**). Selecting a tag shows the FL4 flat-list presentation scoped to that tag. Batch tag add/remove requires FL2 multi-select and edits frontmatter `tags` via a core API (never regex over note bodies in Swift). |
| 10 | Folder notes | Convention: a folder's note is `<Folder>/<Folder>.md` (exact stem match, case per filesystem). No `index.md` fallback in v1 (one convention, censusable; fallback ambiguity is how the Obsidian plugin ecosystem got three incompatible folder-note plugins). Folder with note: label opens the note, chevron still discloses; distinct AX value ("has folder note"). Rename/move rides the existing link-rewrite path — the folder note renames *with* its folder atomically. |
| 11 | Multi-select semantics | Selection becomes `Set<RowID>` with an anchor; Shift/⌘ click, ⇧↑/⇧↓ extension, ⌘A select-visible-level. Batch operations run as **one core transaction per operation** (not N UI loops): one undo unit, one announcement ("Moved 12 files to Research."), one tree refresh, one consolidated skip-alert. Mixed file+folder selections allow move/delete only. |
| 12 | Dual-pane | Setting-gated (default **off**; single tree remains the default experience). When on: navigation pane (folders + tags + shortcuts) and list pane (rows of the selected container) with the standard focus-follows pattern (←/→ moves between panes, same as Navigator). Both panes are complete AX trees; no datum or action is dual-pane-only — the single tree can always do everything (projection-equivalence, same rule as Graph DoD §P-B). |
| 13 | External drops | Finder drag-in = **copy into vault** at the drop folder (never move; never link out). Name collisions get the rename-with-suffix flow. Drops of non-indexable types follow existing attachment rules. |
| 14 | Templates | Sidebar "New Note from Template…" reuses the existing template picker + `render-template` core path (M-6, PR #645) — no second template engine, no sidebar-private template list. |
| 15 | Commands & shortcuts | Every FL action registers in the command palette (`CommandPaletteModel.swift`) and, where structural, in the menu bar — there is no formal `CommandRegistry` enum today (verified 2026-07-05) and FL does not introduce one. Shortcut chords: open-shortcut `⌃1`–`⌃9` scoped to sidebar focus (final chords settled in fl3_spec), collapse-all/expand-all, selection back/forward. Palette + menu always work (T rule R1: no chord is the only path). |
| 16 | i18n/RTL | Out of FL scope; the l10n program (`../14_l10n.md`) owns string externalization. FL specs must not hardcode user-facing strings in ways that block it (use the existing localization seams where they exist). |

---

## Phase map, waves & dependencies

```
Wave 1 (backend)   FL0-1 derived metadata ─▶ FL0-2 FFI surface ─▶ FL0-3 census+bench gate
Wave 2 (parallel)  FL1-1 rich rows ─ FL1-2 previews/badges/settings     (needs Wave 1)
                   FL2-1 multi-select ─▶ FL2-2 batch ops+menus ─ FL2-3 templates+Finder drop   (no Wave-1 dep)
Wave 3 (organize)  FL3-1 sort+grouping ─ FL3-2 pinned ─ FL3-3 shortcuts+recents ─ FL3-4 nav polish
Wave 4 (filter)    FL4-1 filter engine (core) ─▶ FL4-2 filter UI
Wave 5 (tags)      FL5-1 tag queries ─▶ FL5-2 tag tree UI ─ FL5-3 batch tag ops
Wave 6 (folders)   FL6-1 folder notes
Wave 7 (structure) FL7-1 dual-pane container ─▶ FL7-2 list pane + per-folder overrides
Wave 8 (close-out) FL-D docs/help/sidebar.md
```

| Wave | Issues | Gate |
|------|--------|------|
| 1 — Metadata backbone | FL0-1 → FL0-2 → FL0-3 | none (pure slate-core; start any time) |
| 2 — Rows & selection | FL1-1, FL1-2 (need Wave 1) ∥ FL2-1 → FL2-2, FL2-3 (app-only, may start immediately) | FL1 needs FL0-2 merged |
| 3 — Organization | FL3-1 (needs FL0 created-date), FL3-2, FL3-3, FL3-4 | FL3-1 after Wave 1; rest independent |
| 4 — Filter | FL4-1 (core, after FL0-1) → FL4-2 (needs FL1 row components) | filter list reuses FL1 rows |
| 5 — Tag tree | FL5-1 → FL5-2 (needs FL4-2 presentation); FL5-3 (needs FL2-1) | Wave 4 |
| 6 — Folder notes | FL6-1 | independent; any time after Wave 2 |
| 7 — Dual-pane | FL7-1 → FL7-2 | Waves 2–4 complete (the list pane hosts FL1 rows, FL3 overrides, FL4 filter) |
| 8 — Close-out | FL-D | all waves |

**Priority note (owner ranking, 2026-07-05):** the highest-ranked wins are frontmatter display names (FL0/FL1), in-sidebar filter (FL4), multi-select (FL2-1), sort options (FL3-1), pins (FL3-2), shortcuts+recents (FL3-3). Waves are dependency order, not value order — if capacity forces a cut line, cut from Wave 7 backward, never from Wave 1.

## Relationship to other milestones (do not duplicate)

- **U — UI parity (shipped):** FL builds strictly *on top of* the U2 tree (`FileTreeSidebar.swift`, `FileTreeViewModel`) — extend, don't fork. U2's invariants (lazy per-level fetch, mutation-scoped invalidation, tab retargeting, announce conventions) are load-bearing; every FL spec names the ones it touches.
- **M — Sync/CLI (shipped):** `.slate/sidebar.json` follows M's structural-credential-safety and atomic-write conventions; the FL4 filter grammar lands in core partly so the CLI can adopt it later (`slate list --filter`), but CLI adoption is **not** an FL deliverable.
- **N — Bases:** FL0's task/word-count columns are exactly the kind of per-file fields Bases will query; N consumes the same session surface — no parallel computation.
- **P — Graph:** "Reveal in tree" from graph nodes targets the FL selection API; FL2-1's multi-select model should not preclude programmatic single-select reveal (it already exists today).
- **Q — Command palette:** all FL commands register in `CommandRegistry` under `CommandSection` sidebar grouping; presets/shortcuts are commands.
- **V / X — Editor intelligence:** none; FL touches no editor surface except "open" actions.
- **W — Windows (parked):** FL0/FL4-1/FL5-1 are host-independent slate-core; nothing in those issues may take a macOS dependency.
- **EX (32) / PS (33):** parked scoping issues only; see locked decision 2.

## Definition of Done (sidebar-specific, additive to U §A–§G)

- **§FL-A Accessible-first gate:** every FL feature is fully operable keyboard-only and with VoiceOver *in the same PR that ships it* — selection counts announced, filter result counts announced, group headers navigable, pinned/shortcut sections labeled landmarks. No visual-only affordance ever merges (drag-and-drop always has a menu/command twin).
- **§FL-B Derived-metadata census:** after every mutation in an adversarial random + exhaustive walk, incrementally-maintained derived metadata (names, dates, task/word counts, previews) ≡ a full rescan's output. Census names and scales are normative in fl0_spec; `SLATE_CENSUS_FULL=1` scaling per the standing protocol.
- **§FL-C Perf:** scan/save stay O(changed-file) with the new derived columns — `scan_initial` and save-path benches re-run against `BENCHMARKS.md` baselines at 1k/10k/50k with budgets named in fl0_spec. Filter queries < 50 ms at 10k. Tree render budgets from U2 (10k collapsed-root < 1 s) unchanged.
- **§FL-D Single-tree equivalence:** dual-pane mode exposes no datum or action unavailable in single-tree mode (locked decision 12); the drift test enumerates list-pane actions against tree actions.
- **§FL-E Prefs integrity:** `.slate/sidebar.json` writes are atomic temp+rename, versioned, and forward-tolerant (unknown keys preserved round-trip); corrupted file ⇒ defaults + non-blocking notice, never a crash or a data wipe.

## Specs

- [fl0 — Derived note metadata: scanner pipeline, schema, FFI, censuses](specs/fl0_spec.md)
- [fl1 — Row presentation: display names, dates, previews, badges, settings](specs/fl1_spec.md)
- [fl2 — Selection & operations: multi-select, batch ops, menu completeness, templates, Finder drop](specs/fl2_spec.md)
- [fl3 — Organization: sort, grouping, pins, shortcuts, recents, navigation polish](specs/fl3_spec.md)
- [fl4 — Filter: core query engine + sidebar filter UI](specs/fl4_spec.md)
- [fl5 — Tag tree: queries, sidebar section, batch tag operations](specs/fl5_spec.md)
- [fl6 — Folder notes](specs/fl6_spec.md)
- [fl7 — Dual-pane layout option](specs/fl7_spec.md)

Deferred beyond FL (issues filed only when demanded): hidden/excluded folders, icon/color customization, feature images, theming, vault profiles (owner-dropped, decision 1); saved-search shortcuts beyond the FL3-3 set; CLI `--filter` adoption; Omnisearch-style content excerpts in filter results.
