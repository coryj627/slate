# W4 executable spec — Panels & data surfaces

Issues: W4-1 ([#733](https://github.com/coryj627/slate/issues/733)) · W4-2 ([#734](https://github.com/coryj627/slate/issues/734)) · W4-3 ([#735](https://github.com/coryj627/slate/issues/735)) · W4-4 ([#736](https://github.com/coryj627/slate/issues/736)) · W4-5 ([#737](https://github.com/coryj627/slate/issues/737)) · W4-6* ([#738](https://github.com/coryj627/slate/issues/738)) · W4-7* ([#739](https://github.com/coryj627/slate/issues/739)) · W4-8 ([#740](https://github.com/coryj627/slate/issues/740)). Milestone: [GH 22](https://github.com/coryj627/slate/milestone/22). One PR per issue. *(\* W4-6 iff Milestone N shipped; W4-7 iff Milestone O shipped.)*
Program: [00_program.md](../00_program.md) (decisions 4, 13; DoD §W-A/§W-C). Behavioral reference: mac right-pane leaves + panels (backlinks, outgoing links, outline, embeds, tasks/review, properties incl. in-note header + editor rows + add-property, citations suite, sync diagnostics) and the 05 §8.7 grid behavior matrix.

**Execution order: W4-1 → { W4-2..W4-8 } (parallel).**

**W0/W1 execution baseline (2026-07-19 refresh — facts the original spec predates):**

- **The read/write FFI for this wave is bound** (`SlateUniffi`, `public`): backlinks/outgoing/unresolved pages, `tasks_for_file`/`tasks_in_vault`/`toggle_task_status`, `set_property`/`delete_property`/`rename_property_across_vault` + property listings, the citations suite (incl. `speech_text` on rendered references), `list_versions`/`version_content`/`diff_versions`/`restore_version` + deleted-file recovery, `detect_sync`, and the full Bases surface **including core-side `base_export`** (CSV/Markdown text composed in Rust — the export-parity precedent for every grid). No new read/write FFI is needed to start.
- **§W-A rows extend the shipped harness** (W0-3): search and backlink/outgoing serialization already exists over the shared corpus with committed goldens; the task/property/citation/bases row artifacts are additions to both serializer twins + goldens, not a new mechanism.
- **§W-D reality — the announcement anchors for this wave do not exist yet (#969):** the residue census pins 49 `.hostComposed` sites, and the families owned by W4 surfaces are all still Swift-composed — the `AccessibleDataGrid` announce relay (W4-1), task status phrases (`TaskStatusPhrase.swift` — the W4-3 "if not already" is resolved: it did **not** move), `AddPropertySheet` (W4-4), history announcements (W4-7), and the Bases family (W4-6). **#969** (per-family conversion to canonical vocabulary, or recorded designation + goldens) is pre-unpark-executable; each issue's §W-D acceptance consumes its family's status.
- **Fluent theme (program decision 2 addendum):** the W4-1 substrate wraps a **Fluent-restyled** WPF DataGrid — the 05 §8.7 matrix, the UIA-virtualization trap, and the FlaUI gate are validated against the **Fluent templates** (not Aero defaults), grid text sits on W1-1 Slate tokens, and the two-layer Contrast behavior (Fluent.HC + the Slate Contrast dictionary) is asserted on grid chrome and cell text both.
- **Conditionals resolved at the W0-4 snapshot:** N and O are shipped — W4-6/W4-7 are conditional in name only; their matrix rows (incl. the `queries`/`basesDock`/`history` leaves and tab kinds) are live burn-down lists.
- **C# census conventions** (W0-3) apply; the §W-C gate project introduced at W1-1 is the substrate this wave's FlaUI conformance suite builds on.

## W4-1 · Accessible grid substrate — PR 1

1. One wrapped WPF DataGrid component playing the `AccessibleDataGrid` v2 role: 05 §8.7 matrix verbatim — headers announced on entry, cell-by-cell arrow navigation, keyboard sort/filter hooks, row-level actions, separately-addressable summary row, CSV/Markdown export commands, `ColumnRole`-driven row announcements, `audio_description`/`audio_summary` consumption where the surface provides them.
2. Column virtualization safe for AT (UIA ItemContainerPattern correctness under virtualization is a known WPF trap — test with JAWS/NVDA on 10k-row fixtures before feature grids build on it). **Validate against the Fluent DataGrid templates specifically** (decision 2 addendum): Fluent restyles the control chrome, and the §8.7 matrix, focus visuals, and virtualization behavior must hold on what actually ships.
3. FlaUI conformance suite = the reusable §W-C gate every consuming surface inherits.
3b. **Announcement grammar + export sourcing:** the grid's announce relay is a #969 residue family — its §W-D anchor lands via that conversion (or a recorded designation), never a C# re-composition. Export text comes **from core** wherever the surface provides it (`base_export` is the precedent); a surface with no core export is an owner designation decision, not silent host composition.
4. **Owns the transferred W3-1 table rows** (program wave table, deferred cross-wave rows): the reading-view tables' substrate-backed acceptance — §W-C included — closes here, not in Wave 3.

- [ ] §8.7 matrix green under FlaUI + human AT smoke on large fixtures
- [ ] Export commands + announcement grammar parity

## W4-2 · Link & structure panels: backlinks, outgoing, outline, embeds — PR 2

1. Same core row APIs as mac (backlink pages/snippets, outgoing resolution states, heading tree, embeds list); leaf docking + per-leaf context per W1-3.
2. Outline ships as a *feature* (nav utility), not the heading-a11y crutch (W3-1 owns native heading nav).

## W4-3 · Tasks panel + review flow — PR 3

1. Task rows/toggles/priority/scheduling data from core; review flow parity (`TasksReviewView` behavior); status phrases via canonical vocabulary — **resolved 2026-07-19: `TaskStatusPhrase` did *not* move with W0.5-3** (it remains `TaskStatusPhrase.swift`); its conversion is the W4-3 family of **#969** and is this issue's §W-D prerequisite.

## W4-4 · Properties — PR 4

1. In-note properties header + panel editing parity: typed editor rows, add-property flow, list values, type inference display — all writes via `set_property`/`delete_property` paths (§W-G: no parallel write machinery, mirroring N's decision 10).

## W4-5 · Citations suite — PR 5

1. Panel, popover, summary, bibliography: all from core citation artifacts (rendered references, `speech_text`, bibliography composition). Vault-config bibliography sources parity.

## W4-6 · Bases grid* — PR 6

1. N parity on the W4-1 substrate: `.base` open-as-tab, views (table + list + fallback banner), quick filter (transient, Ctrl+F grid-scoped), in-grid property editing, embeds (with W3-5), builder + raw-editor authoring surfaces, saved queries/dashboards — exactly N's shipped scope per matrix rows; `BasesResultSet`/`audio_*` artifacts consumed as-is. **Owns the transferred W3-5 `.base`-embed rows** (deferred cross-wave rows): their acceptance + §W-C close here.

## W4-7 · Local history* — PR 7

1. O parity (O shipped 2026-07-11 — conditional in name only): `Leaf.history` with **two segments** ("This note" + "Deleted" — deleted-file recovery), day-grouped version list, structured accessible diff (the `StructuredDiff` FFI — consumed, not re-derived), restore + **Restore As…**, changes-since-last-open (opt-in), markers toggle, `.canvas`/`.base` history coverage (#797).

## W4-8 · Sync diagnostics — PR 8

1. M parity: sync-detection report leaf (`Leaf.syncDiagnostics` equivalent) over `detect_sync_providers`; Windows provider probes (OneDrive/Dropbox markers) land core-side (decision 9) with fixtures; marker re-detection watcher = bounded `FileSystemWatcher` twin of #638's design (bounded scope, debounce, re-detect trigger only — no content watching). **Pull-forward note (2026-07-19):** the core-side probe work is marker-file/fixture-driven and platform-testable in ordinary CI — it is **pre-unpark-executable** (the W0.5/#963 shape) and may land ahead of the wave if capacity allows.

- [ ] (each) matrix rows green; §W-A rows for data-bearing surfaces; §W-C via the W4-1 inherited gate
