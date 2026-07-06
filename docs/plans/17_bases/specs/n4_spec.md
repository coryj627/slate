# N4 executable spec — Authoring & close-out: query builder, sidebar/palette, dashboards, docs/E2E/AT

Issues: N4-1 ([#707](https://github.com/coryj627/slate/issues/707)) · N4-2 ([#708](https://github.com/coryj627/slate/issues/708)) · N4-3 ([#709](https://github.com/coryj627/slate/issues/709)) · N4-4 ([#710](https://github.com/coryj627/slate/issues/710)) · N4-5 ([#711](https://github.com/coryj627/slate/issues/711)). Milestone: [GH 14](https://github.com/coryj627/slate/milestone/14). One PR per issue.
Program: [00_program.md](../00_program.md) (locked decisions 17, 20–21; DoD §N-G). 05 §8.6 is the builder authority — its six-part UX shape is normative, quoted per rule below.
Swift norms as n3. The builder is **the** genuinely hard design problem of this milestone (05 §8.6's own words) — when a rule here conflicts with keyboard/VO ergonomics discovered in implementation, the a11y outcome wins and the spec gets amended, not silently diverged from.

**Execution order: { N4-1 → N4-2 } ∥ N4-3 ∥ N4-4 → N4-5.**

Baseline facts (verified 2026-07-06, this worktree):

- Builder produces the same `SlateQuery` AST as `.base` parsing (05 §8.6 closing rule); save path = `save_query_as_base` / `save_query` (N2-1/N2-2).
- Property-key inventory for pickers: `list_property_keys` (session.rs:1590) + `PropertyKeySummary` (properties_db.rs:347); tag inventory via the tags table; folder inventory via the dirs table (migration 016).
- Leaf enum: Workspace/RightPaneView.swift:21 — the sidebar-section integration point.
- Filter-condition chip UX precedent: the milestone-14 description mandates "structured chips + boolean joiners, not a free-text editor — VoiceOver hears each filter as a structured node".
- Command palette: CommandPaletteModel.swift; `CommandSection::Bases` exists after N3-1.
- Live preview grid: the N3-1 component, embedded small.

---

## N4-1 · Builder core: source picker + conditions list (#707) — PR 1

A sheet/panel reachable from: BasesView toolbar ("Edit view filters"), the command palette ("Bases: New query"), and the Queries sidebar section (N4-3). One builder, three entry points; editing an existing view loads its AST.

### Normative rules

1. **Source picker** (05 §8.6 item 1): a labeled radio-list — All notes / Folder… / Tag… / Recently edited (days stepper) / Linked from note… / **Tasks** (decision 8). Folder/tag/note pickers are searchable lists fed by the shipped inventories (baseline facts) — never free-text-only. Maps to `QuerySource` (05 §8.2) / `source: tasks` + an `inFolder`/`hasTag` filter on serialization (recorded: Obsidian has no source clause, so **source compiles into filters** on save-as-.base; the builder round-trips it back by recognizing its own canonical first-conjunct shape — §N-G test).
2. **Conditions list** (05 §8.6 item 2): each condition = one **independently navigable row** (AX group) of three structured controls: property (picker: note keys / file fields / formulas / task fields when tasks source), operator (per-type menu: the brief-§2 comparison set + `contains`/`startsWith`/`endsWith`/`isEmpty`/`hasTag`/`hasLink`/`matches`), value (typed editor per property kind — the N3-4 editor family, incl. date picker and "N days ago" relative form compiling to `now() - "Nd"`).
3. Explicit keyboard commands (05 §8.6): Add condition / Remove condition / Edit condition — registry commands, buttons in the row, no drag anywhere. VO reads each row as "Condition N: <property> <operator> <value>" and the list announces the combinator: "Combined with AND" (milestone-14 grammar).
4. Boolean structure: v1 builder composes **one level of grouping** — top-level ALL/ANY toggle + optional nested groups each with their own ALL/ANY/NONE (covers the brief-§1 example shape). Deeper nesting or expressions the structured UI can't express round-trip untouched and render as **read-only advanced chips** ("Advanced condition: <verbatim>") editable only via N4-2's raw editor — never dropped, never mangled (§N-A discipline).
5. The builder edits an `SlateQuery` draft in memory; nothing writes until Save (N4-2). Cancel is lossless.

### Tests (PR 1)

XCTest: full keyboard walk (construct the brief-§1 example filter tree without a pointer — the acceptance bar); VO string goldens per rule 3; advanced-chip round-trip (parse → builder → save ⇒ byte-identical for untouched conditions); source↔filter compilation round-trip (§N-G).

- [ ] Rules 1–5
- [ ] XCTests incl. keyboard-only construction; a11y-check 100/100; APCA both appearances
- [ ] Swift conventions

## N4-2 · Builder completion: sort/group/columns/formulas, preview, save (#708) — PR 2

### Normative rules

1. **Sort & group sections** (05 §8.6 item 3): add/remove/reorder sort keys (listed as structured rows, ⌥↑/↓ reorder — the properties-menu convention); group picker (one property + direction).
2. **Columns/view picker** (05 §8.6 item 4): checkbox list of available properties (checked = in `order`), ⌥↑/↓ reorder; view-type radio (table/list); per-column displayName editor (writes the `properties` map).
3. **Formula editor**: name + expression text field with **live validation** (parse via N0-1; green-check/error message with the offending span read to VO — Obsidian's checkmark parity, brief §8); function-name completion from the pinned v1 table; inserted formulas appear in the columns picker.
4. **Live preview** (05 §8.6 item 5): an embedded N3-1 grid in a **separate accessible region** below the builder, re-executing (debounced 300 ms, cancel-superseded) as the draft changes; its `audio_summary` is announced on settle: "Query returns 23 notes. First result: 'Project Roadmap', modified yesterday" (05 §8.6 grammar verbatim). Preview errors surface the decision-6 banner — the builder is where fail-loud is most useful.
5. **Raw-expression escape hatch** (decision 21): per-condition "Edit as expression" swaps the structured row for a validated text field (same live validation); the advanced chips from N4-1 rule 4 open here. Raw editing is the secondary path — reachable, never required for the v1 operator set.
6. **Save** (05 §8.6 item 6): three commits — Save to view (`base_apply_edit` splice into the open `.base`), Save as new `.base` file (canonical style, N0-3 rule 3), Save as saved query (N2-2). Builder→AST→`.base`→AST identity is the §N-G gate.

### Tests (PR 2)

XCTest: preview debounce/cancel; validation goldens (good/bad expressions incl. unknown function naming); all three save paths byte/AST-asserted; §N-G identity suite (builder-constructed corpus queries survive the full loop).

- [ ] Rules 1–6
- [ ] XCTests + §N-G suite; a11y-check; APCA
- [ ] Swift conventions

## N4-3 · Queries sidebar section, palette, pins (#709) — PR 3

### Normative rules

1. **Queries sidebar section**: a new `Leaf` case (`queries`) listing saved queries (name, description tooltip) + `.base` files (from `bases_list`) in two labeled groups; Return opens (saved query ⇒ ephemeral tab via `open_saved_query`; `.base` ⇒ funnel); context/keyboard actions: run, edit in builder, rename, delete (confirm), export as `.base` (N2-2 rule 3), pin.
2. **Pins**: pinned queries surface at the top of the section; VO reads "<name>, saved query" (milestone-14 checkpoint verbatim). Pin state is app-level prefs (not vault bytes).
3. **Palette**: each saved query registers "Run query: <name>" under `CommandSection::Bases` (05 §8.10), refreshed on save/rename/delete; drift tests cover the dynamic registration pattern (the canvas dynamic-command precedent).
4. Sidebar/leaf a11y: the section is a labeled landmark; counts announced ("Queries, 12 items, 3 pinned").

### Tests (PR 3)

XCTest: section rendering + actions; pin persistence; palette registration lifecycle; VO goldens.

- [ ] Rules 1–4
- [ ] XCTests; a11y-check; APCA
- [ ] Swift conventions

## N4-4 · Dashboards + sidebar follow-active `this` (#710) — PR 4

### Normative rules

1. **Dashboard surface** (05 §8.10, decision 17): a tab rendering an ordered list of sections; each section = heading (saved-query name or override) + live grid (its own execution, shared cache). AX structure = **heading hierarchy + grid hierarchy, never flat blobs** (milestone-14 checkpoint verbatim): dashboard title H1, sections H2, VO rotor-navigable by headings.
2. Dashboard editor: structured list editor (add section from saved-query picker, reorder ⌥↑/↓, heading override, view override) — same builder ergonomics; CRUD via N2-2.
3. **Sidebar follow-active** (decision 20): a `.base`/saved query/dashboard docked in the right pane resolves `this` = the active note and re-executes on note switch (debounced 500 ms, cancel-superseded). The "better backlinks" pattern (`file.hasLink(this)` — brief §4) is the acceptance fixture. Follow-mode announces on settle only when membership changed (N3-1 rule 8 discipline).
4. Missing/dangling saved-query refs render labeled missing-sections (N2-2 rule 3), actionable ("Remove section / Pick replacement").
5. Empty dashboard/empty section states carry instructive AX text, never blank regions.

### Tests (PR 4)

XCTest: heading-hierarchy AX snapshot; section CRUD; follow-active fixture (note switch ⇒ correct rows; debounce; announcement-on-change-only); dangling-ref handling.

- [ ] Rules 1–5
- [ ] XCTests; a11y-check; APCA
- [ ] Swift conventions

## N4-5 · Docs, help, E2E corpus, AT checklist, benchmarks (#711) — PR 5

### Deliverables

1. **User help**: Bases overview; `.base` syntax reference (brief §1–§4 distilled); the pinned v1 function table with per-function status (evaluate / parse-only / excluded) and the fail-loud rule; **Slate extensions page** (`source: tasks`, `file.tasks.*`, `file.matches`, `slate` view sub-key) each with its interop caveat (decisions 8/9/3); quick-filter help ("temporary — never changes your file"); builder walkthrough (keyboard-first narrative); saved queries/dashboards incl. the export-as-`.base` durability note (decision 17); the N1-3 rule-6 summaries-vs-limit stance; **Dataview migration page**: what converts (N0-5 mapping tables rendered as user-facing tables), what fails loud and why (CALENDAR, FLATTEN, `rows`, unmapped functions, the null-comparison delta with its `typeof` fix), the convert-to-`.base` command, and the permanent DataviewJS answer (rewrite as Slate queries or V2 WASM plugins — 05 §10).
2. **E2E** (fixture vault in-repo, CLI-driven where possible via N2-3 — the verb doubles as the harness): open Obsidian-authored `.base` ⇒ expected rows (goldens); edit property in grid ⇒ exact file bytes; quick filter ⇒ zero byte/dirty change; builder loop ⇒ byte-identical save; embeds resolve `this`; `slate query` json/csv goldens match the in-app export.
3. **Human-AT smoke checklist** (the milestone's residual, XD/T precedent — keeps the GH milestone open until a human passes it): the 05 §8.7 matrix + milestone-14 checkpoints as a written script (grid entry, column/cell/sort/summary announcements, filter builder as structured nodes, quick-filter counts, pin reading, dashboard heading navigation).
4. **BENCHMARKS.md close-out**: decision-16 numbers at 1k/10k/50k from N2-1 re-run at milestone tip; scan-bench diff re-asserted.
5. Program doc status flip + gap-analysis refresh (what shipped vs. reserved N-E1…E7).

### Tests

The E2E suite **is** the test; CI-runs the CLI-driven subset; the AT checklist is human-executed and recorded in the milestone (not CI).

- [ ] Help + extensions/interop pages
- [ ] E2E green in CI; goldens committed
- [ ] AT checklist document + BENCHMARKS.md close-out
- [ ] Program/gap-analysis status updates

**Wave-5 exit / milestone DoD (from the milestone-14 description, restated):** `.base` round-trip byte-equal on the corpus; real-vault queries render correctly; saved queries persist across launches; §9.5 performance on a 10k vault; the §8.7 VoiceOver matrix passes; no data-loss path (§N-F census + atomic writes). The GH milestone closes only after the human AT pass (item 3).
