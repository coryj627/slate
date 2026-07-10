# N3 executable spec — macOS views: BasesView tab, table + list renderers, quick filter, row actions, embeds

Issues: N3-1 ([#702](https://github.com/coryj627/slate/issues/702)) · N3-2 ([#703](https://github.com/coryj627/slate/issues/703)) · N3-3 ([#704](https://github.com/coryj627/slate/issues/704)) · N3-4 ([#705](https://github.com/coryj627/slate/issues/705)) · N3-5 ([#706](https://github.com/coryj627/slate/issues/706)). Milestone: [GH 14](https://github.com/coryj627/slate/milestone/14). One PR per issue.
Program: [00_program.md](../00_program.md) (locked decisions 10–15, 19–20; DoD §N-D/§N-F/§N-G). A11y matrix: 05 §8.7 + the vendored accessibility checkpoints ([02_milestone_brief.md](../02_milestone_brief.md)), both normative.
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
3. **Table renderer** on `AccessibleDataGrid` v2 — additive component work, existing callers compile unchanged: (a) **cell-navigation mode** (plain arrows move cell-by-cell in all four directions; Home/End = first/last column, PageUp/PageDown = viewport rows, per the vendored keyboard matrix ../02_milestone_brief.md; within-text arrow movement applies only *inside an open cell editor* — Return/F2 opens one, Esc closes, N3-4 rule 3), (b) **grouped sections** (group header rows: AX heading trait + "Group: <label>, N rows"), (c) **summary row** separately addressable from data rows (vendored checkpoint), announced "Summary: <column>: <value>, …", (d) editable-cell hooks (consumed by N3-4). Cell selection and transient-sort state are keyed by the exact stable column ID, never an array offset or Swift's canonically-equivalent `String` identity; a disappeared ID clears the state rather than retargeting another column.
4. **Announcement grammar** (pinned, vendored checkpoints verbatim — ../02_milestone_brief.md): entering grid ⇒ `audio_summary`; column move ⇒ "Column: <label>, sortable, current sort: <asc/desc/none>"; cell move ⇒ "<column label>: <value>"; sort change ⇒ "Sorted by <column>, <direction>" (grid `applySort` return, AccessibleDataGrid.swift:255); row move announces the `ColumnRole::Primary` value first (05 §8.4).
5. **Sort affordance:** header click **and** keyboard command per column (05 §8.7 "no header-click-only"); v1 sort is **session-transient view state** unless explicitly saved ("Save sort to view" command writes the `slate` sub-key via `base_apply_edit` — decision 3; never Obsidian's undocumented keys).
6. **Degraded views** (decision 6): `view_error` ⇒ banner naming the construct + empty grid with AX text = the error; `warnings` ⇒ non-modal banner list; cards/map/unknown types ⇒ table fallback + notice naming the requested layout (decision 4).
7. **Commands** (decision 15): `CommandSection::Bases` lands here (cross-language enum + bindings regen); registered commands this PR: open-view-switcher, next/previous view, sort-by-column, save-sort-to-view, results-popover. All three drift tests extend. Zero new global chords.
8. Live updates follow the repo's shipped stance: **in-app writes** (grid property edit N3-4, note save, `.base` edit) re-execute the visible view through the existing post-write refresh path, preserving selection by row identity `(path, ordinal)` plus exact column ID and preserving the active view by its exact name across definition reorder. Announce "Updated: <audio_summary>" only when membership changed (no announcement spam). **External changes: Refresh is the path** — a registry command "Bases: Refresh" (the XD external-change precedent, `../16_excalidraw/specs/xd2_spec.md` rule "no automatic external-change reload"); no filesystem watcher is added by N.

### Tests (PR 1)

XCTest: keyboard matrix (arrows/Home/End/PageUp/PageDown/sort — vendored test list); announcement-string goldens per rule 4; view-switcher + fallback-banner rendering; routing (funnel + quick-open). Grid component tests extend `AccessibleDataGridTests.swift` (existing callers' behavior unchanged — regression suite must stay green untouched).

- [x] Rules 1–8; CommandSection::Bases + bindings regen; drift tests extended
- [x] XCTest matrices + goldens; a11y-check 100/100; APCA both appearances
- [x] fmt/clippy (Rust side of enum) + Swift lint conventions

## N3-2 · List renderer (#703) — PR 2

### Normative rules

1. `type: list` views render as a native list (NSOutlineView flat/one-level): **primary item** = first `order` property (brief: the property at the top of the Properties menu); remaining properties either **indented sub-items** (Obsidian "Indent properties" parity) or **separator-joined** into the primary line (default comma — brief §4 "List view settings"), per view state under the `slate` sub-key; markers bullets/numbers/none likewise.
2. Projection equivalence (§N-G): same `BasesResultSet`; selection is shared row identity; every N3-4 row action works identically; groups render as section headers; summaries as a trailing summary section.
3. AX: rows announce `audio_description`; list mode is **row-navigation** (no cell mode — that's what table is for; recorded so the asymmetry never reads as an omission).
4. Renderer choice = the view's `type` (a `.base` written `list` renders list); a per-tab "View as table/list" override command exists (transient, never persisted) for AT users who want cell navigation over any view — decision 11's spirit, recorded in help.

### Tests (PR 2)

XCTest: parity suite runs the same fixtures through table and list asserting identical row sets/actions (§N-G); indent/separator/marker variants; announcement goldens.

- [x] Rules 1–4
- [x] Parity + variant tests; a11y-check; APCA
- [x] Swift conventions

## N3-3 · Transient quick filter (#704) — PR 3

The owner-pinned request (brief §5.1). **Strictly in-memory** (decision 12).

### Normative rules

1. Surfaces: toolbar search field; **⌘F focuses it when a Bases surface has focus** (scoped — find-in-note keeps ⌘F elsewhere; the funnel is first-responder-based, canvas chord-scoping precedent). Registry command "Bases: Quick filter" (no chord of its own).
2. Matching happens **engine-side** — the field's debounced (150 ms) text goes to `base_execute(…, quick_filter: Some(text), …)` (N2-1): case-insensitive, diacritic-insensitive substring across displayed column values, folded with the same `value_text_norm` tables the index uses (one folding implementation, Rust — never a Swift re-implementation). Matches Obsidian 1.12 "displayed properties" behavior (brief §5.1). Never cached, never persisted (a parameter, not state).
3. AX: result announcement "<shown_count> of <total_count> results" on settle — both counts and the filtered `audio_summary`/`audio_description` strings come from the returned `BasesResultSet` (single source of truth, decision 19); field labeled "Quick filter — temporary, does not change the base"; Where-am-I readback includes "quick filter: <text>".
4. Lifecycle: Esc in the field clears + returns focus to the grid at the previously-selected row when it survived the filter (else first row); switching view/tab clears; **never** written to the `.base`, never dirties the tab (the git-vault concern from the thread is a test: file mtime/bytes unchanged after any quick-filter session — §N-F extension).
5. Summary row while filtering is the engine's recompute over matching rows (rule 2 — it comes back in the same result set) with the banner "Summaries: filtered" (honesty rule; full-set summaries return on clear).
6. Interaction with export: while a quick filter is active the export commands prompt "Export N filtered rows / Export all M rows" (decision 13) — **the prompt ships with N3-4's export commands** (whichever PR merges second wires the two; N3-4 owns the dialog, this rule owns the requirement).
7. Reserved (N-E2, not here): per-column search, distinct-value checklists, field-scoped syntax.

### Tests (PR 3)

XCTest: matching/normalization goldens (incl. diacritics — engine-side, asserted through the FFI); Esc/selection restoration; view-switch clearing; announcement strings from the result set; the never-dirties test (bytes + tab-dirty state); summary-recompute banner.

- [x] Rules 1–6 (rule 7 is a reserved-scope note, no code)
- [x] XCTests incl. never-dirties; a11y-check; APCA
- [x] Swift conventions

## N3-4 · Row actions, in-grid editing, export (#705) — PR 4

### Normative rules

1. **Row actions** (05 §8.7 list, keyboard-first: Return = open; context menu + registry commands for all): open note (funnel), copy link (wikilink to path — existing copy-link command semantics), show backlinks (opens the backlinks leaf for the row's file), edit property (rule 2). §8.7's fifth action "show local graph" is **deferred until Milestone P ships its local-graph leaf** (recorded, gap O15 — the action slot is reserved in the context menu ordering). Tasks-source rows: open note **at the task's line** (existing task-activation precedent from the Tasks panel).
2. **Cell editing** (decision 10): editable when the column is a `note.*` property and the property kind has a shipped editor (text/number/checkbox/date/list — the U3 in-note properties panel editors, reused); commit ⇒ `session.set_property` (session.rs:1713); clear-cell ⇒ `delete_property` (:1755). Formula/file/task columns read-only with AX hint "read-only: computed" / "read-only: file metadata". After commit: re-execute (cache invalidation is automatic via generation bump), selection preserved, announce "Saved. <column>: <new value>" — and if the row **leaves the result set** because of the edit, announce "Saved. Row no longer matches this view" (fail-honest, no silent vanish).
3. Editing enters via Return-on-cell (cell mode) or the Edit-property action (row mode); Esc cancels; Tab/Shift-Tab commit-and-move (Obsidian table parity, brief §4-editing). Undo: v1 scope = re-edit (the property write path has no undo stack yet); recorded gap → gap analysis (Obsidian has property undo; Slate tracks as follow-up).
4. **Export** (decision 13): commands "Export view as CSV", "Export view as Markdown table", "Copy view as Markdown" — `base_export` output (passing the active `quick_filter` when the user chooses "Export N filtered rows" in the confirmation this rule owns — N3-3 rule 6); NSSavePanel default name `<base> — <view>.csv/md`; completion announced.
5. All actions are registry commands under `CommandSection::Bases`; drift tests extend.

### Tests (PR 4)

XCTest: per-kind editor commit/cancel/clear round-trips against a fixture vault (bytes asserted — frontmatter edited atomically, body untouched); row-departure announcement; read-only hints; export goldens (CSV RFC 4180 quoting, Markdown escaping); task-row open-at-line.

- [x] Rules 1–5
- [x] XCTests incl. byte-level write assertions; a11y-check; APCA
- [x] Swift conventions

## N3-5 · Embeds: `![[x.base]]`, `#View`, fences, `this` (#706) — PR 5

### Normative rules

1. Five forms, one renderer (decision 14 + amended decision 2): `![[x.base]]` (first view), `![[x.base#View]]` (view by exact name; unknown ⇒ labeled error chip listing views), ` ```base ` (inline YAML via `open_base_inline`), ` ```slate-query ` (**dual-mode**, decision 14: Core's full-YAML classifier treats a top-level scalar `query` as a saved-query reference with optional scalar `view`; otherwise valid YAML is inline Bases YAML exactly like ` ```base ` — the milestone's "tester defines queries in slate-query blocks" form), and ` ```dataview ` (block DQL via `open_dql` — read-only render; conversion losses surface the decision-6 banner naming the construct; a "Convert to .base…" action on the banner/context menu runs `dql_as_base`). The fence interior passed to Core is sliced verbatim, including the line ending before the closing fence, so YAML `|`, `|-`, and `|+` chomping semantics survive. Invalid reference YAML fails loud instead of being reinterpreted as inline Base content. ` ```dataviewjs ` stays an ordinary code block — no renderer, no banner. Discovery comes from the N0-4 index.
2. Context: `this_path` = the **embedding note** (decision 20; brief §4). Same grid component as N3-1 — full keyboard/AX inside the embed; the embed region is a labeled AX group "Embedded base: <name>, view <view>" with an "Open in tab" action (funnel).
3. **Embeds are read-only in v1, both surfaces** (decision 14): the grid is fully interactive for navigation, sort, and quick filter, but cell editing is disabled with the AX hint "read-only in embeds — open in tab to edit" ("Open in tab" is one keystroke). This honors the milestone's "non-editable inline group" wording literally; in-embed editing is a possible post-v1 relaxation, not a v1 ambiguity.
4. Editor surface: the fence/embed source stays editable text; a labeled semantic placeholder mounts immediately below/adjacent per the K math-block editor convention. Heavy open/execute work begins only after the embed first becomes scroll-visible, but the placeholder remains in the reading structure and holds its mounted registry lease until unmount so VoiceOver never loses the region while execution is deferred.
5. Embed execution shares the cache (same AST hash + `this`); a note with five embeds of one base costs one execution per distinct (view, this) pair. File paths, saved-query IDs/names, requested views, and result column identities use exact UTF-8 identity so canonically equivalent spellings cannot alias one another.
6. Failure honesty: a fence that fails `parse_base` renders the degraded banner + raw source remains visible in editor; never a blank hole (XD placeholder discipline, `../../16_excalidraw/specs/xd0_spec.md` render rule 8).

### Tests (PR 5)

XCTest: all five forms render + announce (both ` ```slate-query ` modes); `#View` selection; unknown-target chips; `this` resolution golden (`file.hasLink(this)` fixture — the better-backlinks pattern); editor/reading mode split; cache-sharing (execution-count probe); read-only enforcement (edit attempts refused with the hint, both surfaces); DQL fence render + Unsupported banner + convert-to-`.base` action; `dataviewjs` fence stays a plain code block.

- [x] Rules 1–6
- [x] XCTests; a11y-check; APCA
- [x] Swift conventions

**Wave-4 exit:** the vendored accessibility checkpoints (`../02_milestone_brief.md`) all demonstrably met on fixtures (announcement goldens), §N-G table/list parity suite green, quick-filter never-dirties test green.
