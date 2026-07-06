# N3 executable spec — macOS views: BasesView tab, table + list renderers, quick filter, row actions, embeds

Issues: N3-1 ([#702](https://github.com/coryj627/slate/issues/702)) · N3-2 ([#703](https://github.com/coryj627/slate/issues/703)) · N3-3 ([#704](https://github.com/coryj627/slate/issues/704)) · N3-4 ([#705](https://github.com/coryj627/slate/issues/705)) · N3-5 ([#706](https://github.com/coryj627/slate/issues/706)). Milestone: [GH 14](https://github.com/coryj627/slate/milestone/14). One PR per issue.
Program: [00_program.md](../00_program.md) (locked decisions 10–15, 19–20; DoD §N-D/§N-F/§N-G). A11y matrix: 05 §8.7 + the milestone-14 accessibility checkpoints, both normative.
Swift norms: a11y-check 100/100 on each PR's own tip, APCA Lc ≥ 75 both appearances (Slate chrome only — grid *content* is user data), XCTest keyboard matrices, no new global chords.

**Execution order: N3-1 → { N3-2 ∥ N3-3 ∥ N3-4 } → N3-5.**

Baseline facts (verified 2026-07-06, this worktree):

- `AccessibleDataGrid` v2 (apps/slate-mac/Sources/SlateMac/AccessibleDataGrid.swift:7) — NSTableView-backed, built in T #519 explicitly "shared with Milestone N": sortable headers (`applySort` :255 returns the announcement string), type-ahead (:399), keyboard handling (:175/:370), selection binding, double-click action. Gaps N3-1 must add **inside the shared component** (flagged to existing callers, additive defaults): grouped sections, summary row, per-cell (not per-row) navigation mode, editable cells.
- `openFile` funnel: AppState.swift:799 (`openFile(_:target:)`), canvas arm precedent :807-ish (`case .canvas` :386/:460/:5671). New `.base` arm mirrors it.
- Quick-open listing: `FileFilter` (session.rs:140, `MarkdownAndCanvas` :148/:3522) — XD2-1 extends it for drawings; N3-1 adds bases the same additive way.
- Right-pane leaves: `Leaf` enum (Workspace/RightPaneView.swift:21 — outline/backlinks/…/tasks/syncDiagnostics). N4-3/N4-4 add there; N3 is tabs + embeds only.
- Embed rendering: EmbedView.swift + NoteContentView.swift (reading view); the editor renders fences as editable source with inline preview per the K/T conventions.
- Property write path: `session.set_property` (session.rs:1713) / `delete_property` (:1755) — atomic; the in-note properties panel (Milestone U) is the UX precedent for typed editors.
- Command registry: commands.rs:38 (`CommandSection`, Canvas = 8; **XD reserves 9** — Bases takes the next free discriminant at merge, landing with N3-1, the first command-registering PR). Drift tests: SlateCommandsTests.swift (registration forward, menu-scrape reverse #330, help-table #526).
- VoiceOver announcement plumbing: the shipped announcement helper used by search/canvas (post-notification pattern); reuse, don't reinvent.

---

## N3-1 · BasesView tab: routing, table renderer, view switcher, summary row (#702) — PR 1

### Normative rules

1. **Routing:** `.base` paths take a new arm in the `openFile` funnel (AppState.swift:799) → workspace tab `case base(path:)`. File tree, links, recents need no special cases (funnel property). Quick-open: extend `FileFilter` additively (XD gap-R7 precedent) so `.base` files list.
2. **Tab layout:** header = base name + **view switcher** (menu-button listing `base_views`; first view default — brief §1; ⌘-less cycling command "Bases: Next/Previous view" in the registry); toolbar = Results count (activates a popover: total/shown counts, limit readout), Quick filter (N3-3 placeholder slot), Export (N3-4). Body = renderer for the active view; footer = summary row when any summary is assigned.
3. **Table renderer** on `AccessibleDataGrid` v2 — additive component work, existing callers compile unchanged: (a) **cell-navigation mode** (arrow keys move cell-by-cell; `⌥←/→` column, `←/→` within text per AX conventions; Home/End/PageUp/PageDown per the milestone-14 matrix), (b) **grouped sections** (group header rows: AX heading trait + "Group: <label>, N rows"), (c) **summary row** separately addressable from data rows (milestone-14 checkpoint), announced "Summary: <column>: <value>, …", (d) editable-cell hooks (consumed by N3-4).
4. **Announcement grammar** (pinned, milestone-14 checkpoints verbatim): entering grid ⇒ `audio_summary`; column move ⇒ "Column: <label>, sortable, current sort: <asc/desc/none>"; cell move ⇒ "<column label>: <value>"; sort change ⇒ "Sorted by <column>, <direction>" (grid `applySort` return, AccessibleDataGrid.swift:255); row move announces the `ColumnRole::Primary` value first (05 §8.4).
5. **Sort affordance:** header click **and** keyboard command per column (05 §8.7 "no header-click-only"); v1 sort is **session-transient view state** unless explicitly saved ("Save sort to view" command writes the `slate` sub-key via `base_apply_edit` — decision 3; never Obsidian's undocumented keys).
6. **Degraded views** (decision 6): `view_error` ⇒ banner naming the construct + empty grid with AX text = the error; `warnings` ⇒ non-modal banner list; cards/map/unknown types ⇒ table fallback + notice naming the requested layout (decision 4).
7. **Commands** (decision 15): `CommandSection::Bases` lands here (cross-language enum + bindings regen); registered commands this PR: open-view-switcher, next/previous view, sort-by-column, save-sort-to-view, results-popover. All three drift tests extend. Zero new global chords.
8. Live updates: vault-generation bump (file change) ⇒ re-execute the visible view (debounced 250 ms), preserving selection by row identity `(path, ordinal)` and announcing "Updated: <audio_summary>" only when membership changed (no announcement spam).

### Tests (PR 1)

XCTest: keyboard matrix (arrows/Home/End/PageUp/PageDown/sort — milestone-14 list); announcement-string goldens per rule 4; view-switcher + fallback-banner rendering; routing (funnel + quick-open). Grid component tests extend `AccessibleDataGridTests.swift` (existing callers' behavior unchanged — regression suite must stay green untouched).

- [ ] Rules 1–8; CommandSection::Bases + bindings regen; drift tests extended
- [ ] XCTest matrices + goldens; a11y-check 100/100; APCA both appearances
- [ ] fmt/clippy (Rust side of enum) + Swift lint conventions

## N3-2 · List renderer (#703) — PR 2

### Normative rules

1. `type: list` views render as a native list (NSOutlineView flat/one-level): **primary item** = first `order` property (brief: the property at the top of the Properties menu); remaining properties either **indented sub-items** (Obsidian "Indent properties" parity) or **separator-joined** into the primary line (default comma — brief §5 List view), per view state under the `slate` sub-key; markers bullets/numbers/none likewise.
2. Projection equivalence (§N-G): same `BasesResultSet`; selection is shared row identity; every N3-4 row action works identically; groups render as section headers; summaries as a trailing summary section.
3. AX: rows announce `audio_description`; list mode is **row-navigation** (no cell mode — that's what table is for; recorded so the asymmetry never reads as an omission).
4. Renderer choice = the view's `type` (a `.base` written `list` renders list); a per-tab "View as table/list" override command exists (transient, never persisted) for AT users who want cell navigation over any view — decision 11's spirit, recorded in help.

### Tests (PR 2)

XCTest: parity suite runs the same fixtures through table and list asserting identical row sets/actions (§N-G); indent/separator/marker variants; announcement goldens.

- [ ] Rules 1–4
- [ ] Parity + variant tests; a11y-check; APCA
- [ ] Swift conventions

## N3-3 · Transient quick filter (#704) — PR 3

The owner-pinned request (brief §5.1). **Strictly in-memory** (decision 12).

### Normative rules

1. Surfaces: toolbar search field; **⌘F focuses it when a Bases surface has focus** (scoped — find-in-note keeps ⌘F elsewhere; the funnel is first-responder-based, canvas chord-scoping precedent). Registry command "Bases: Quick filter" (no chord of its own).
2. Matching: case-insensitive substring across **displayed** column values (post-formula, display strings — matches Obsidian 1.12 "displayed properties" behavior, brief §5.1); diacritic-insensitive per the `value_text_norm` folding tables. Rows filter live per keystroke (debounced 150 ms).
3. AX: result announcement "N of M results" on settle (milestone-14 grammar family); field labeled "Quick filter — temporary, does not change the base"; grid `audio_summary` reflects the filtered state while active; Where-am-I readback includes "quick filter: <text>".
4. Lifecycle: Esc in the field clears + returns focus to the grid at the previously-selected row when it survived the filter (else first row); switching view/tab clears; **never** written to the `.base`, never dirties the tab (the git-vault concern from the thread is a test: file mtime/bytes unchanged after any quick-filter session — §N-F extension).
5. Summary row while filtering recomputes over **visible rows** with the banner "Summaries: filtered" (honesty rule; full-set summaries return on clear).
6. Interaction with export (N3-4): export while a quick filter is active prompts "Export N filtered rows / Export all M rows" (decision 13).
7. Reserved (N-E2, not here): per-column search, distinct-value checklists, field-scoped syntax.

### Tests (PR 3)

XCTest: matching/normalization goldens; Esc/selection restoration; view-switch clearing; announcement strings; the never-dirties test (bytes + tab-dirty state); summary-recompute banner.

- [ ] Rules 1–6
- [ ] XCTests incl. never-dirties; a11y-check; APCA
- [ ] Swift conventions

## N3-4 · Row actions, in-grid editing, export (#705) — PR 4

### Normative rules

1. **Row actions** (05 §8.7 list, keyboard-first: Return = open; context menu + registry commands for all): open note (funnel), copy link (wikilink to path — existing copy-link command semantics), show backlinks (opens the backlinks leaf for the row's file), edit property (rule 2). Tasks-source rows: open note **at the task's line** (existing task-activation precedent from the Tasks panel).
2. **Cell editing** (decision 10): editable when the column is a `note.*` property and the property kind has a shipped editor (text/number/checkbox/date/list — the U3 in-note properties panel editors, reused); commit ⇒ `session.set_property` (session.rs:1713); clear-cell ⇒ `delete_property` (:1755). Formula/file/task columns read-only with AX hint "read-only: computed" / "read-only: file metadata". After commit: re-execute (cache invalidation is automatic via generation bump), selection preserved, announce "Saved. <column>: <new value>" — and if the row **leaves the result set** because of the edit, announce "Saved. Row no longer matches this view" (fail-honest, no silent vanish).
3. Editing enters via Return-on-cell (cell mode) or the Edit-property action (row mode); Esc cancels; Tab/Shift-Tab commit-and-move (Obsidian table parity, brief §4-editing). Undo: v1 scope = re-edit (the property write path has no undo stack yet); recorded gap → gap analysis (Obsidian has property undo; Slate tracks as follow-up).
4. **Export** (decision 13): commands "Export view as CSV", "Export view as Markdown table", "Copy view as Markdown" — `base_export` output; NSSavePanel default name `<base> — <view>.csv/md`; quick-filter interaction per N3-3 rule 6; completion announced.
5. All actions are registry commands under `CommandSection::Bases`; drift tests extend.

### Tests (PR 4)

XCTest: per-kind editor commit/cancel/clear round-trips against a fixture vault (bytes asserted — frontmatter edited atomically, body untouched); row-departure announcement; read-only hints; export goldens (CSV RFC 4180 quoting, Markdown escaping); task-row open-at-line.

- [ ] Rules 1–5
- [ ] XCTests incl. byte-level write assertions; a11y-check; APCA
- [ ] Swift conventions

## N3-5 · Embeds: `![[x.base]]`, `#View`, fences, `this` (#706) — PR 5

### Normative rules

1. Five forms, one renderer (decision 14 + amended decision 2): `![[x.base]]` (first view), `![[x.base#View]]` (view by name; unknown ⇒ labeled error chip listing views), ` ```base ` (inline YAML via `open_base_inline`), ` ```slate-query ` (YAML body: `query: <saved name or id>`, optional `view: <name>`; unknown ⇒ labeled error chip listing saved queries), and ` ```dataview ` (block DQL via `open_dql` — read-only render; conversion losses surface the decision-6 banner naming the construct; a "Convert to .base…" action on the banner/context menu runs `dql_as_base`). ` ```dataviewjs ` stays an ordinary code block — no renderer, no banner. Discovery from the N0-4 index; render on scroll-into-view (parse-on-open economy).
2. Context: `this_path` = the **embedding note** (decision 20; brief §4). Same grid component as N3-1 — full keyboard/AX inside the embed; the embed region is a labeled AX group "Embedded base: <name>, view <view>" with an "Open in tab" action (funnel).
3. Reading view: fully interactive grid except editing (rule 4). Editor: the fence/embed source stays editable text; the rendered grid appears as a **non-editable inline group** below/adjacent per the K math-block editor convention (milestone-14 commitment: "renders as a non-editable inline group; the source fence remains editable").
4. In-embed cell editing is **enabled in reading view, disabled in the editor** (edit the fence there instead — one writer per surface; recorded stance). Quick filter available in both (transient).
5. Embed execution shares the cache (same AST hash + `this`); a note with five embeds of one base costs one execution per distinct (view, this) pair.
6. Failure honesty: a fence that fails `parse_base` renders the degraded banner + raw source remains visible in editor; never a blank hole (§XD placeholder discipline).

### Tests (PR 5)

XCTest: all five forms render + announce; `#View` selection; unknown-target chips; `this` resolution golden (`file.hasLink(this)` fixture — the better-backlinks pattern); editor/reading mode split; cache-sharing (execution-count probe); reading-view edit round-trip; DQL fence render + Unsupported banner + convert-to-`.base` action; `dataviewjs` fence stays a plain code block.

- [ ] Rules 1–6
- [ ] XCTests; a11y-check; APCA
- [ ] Swift conventions

**Wave-4 exit:** milestone-14 accessibility checkpoints all demonstrably met on fixtures (announcement goldens), §N-G table/list parity suite green, quick-filter never-dirties test green.
