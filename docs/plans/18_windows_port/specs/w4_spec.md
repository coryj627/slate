# W4 executable spec — Panels & data surfaces

Issues: W4-1 ([#733](https://github.com/coryj627/slate/issues/733)) · W4-2 ([#734](https://github.com/coryj627/slate/issues/734)) · W4-3 ([#735](https://github.com/coryj627/slate/issues/735)) · W4-4 ([#736](https://github.com/coryj627/slate/issues/736)) · W4-5 ([#737](https://github.com/coryj627/slate/issues/737)) · W4-6* ([#738](https://github.com/coryj627/slate/issues/738)) · W4-7* ([#739](https://github.com/coryj627/slate/issues/739)) · W4-8 ([#740](https://github.com/coryj627/slate/issues/740)). Milestone: [GH 22](https://github.com/coryj627/slate/milestone/22). One PR per issue. *(\* W4-6 iff Milestone N shipped; W4-7 iff Milestone O shipped.)*
Program: [00_program.md](../00_program.md) (decisions 4, 13; DoD §W-A/§W-C). Behavioral reference: mac right-pane leaves + panels (backlinks, outgoing links, outline, embeds, tasks/review, properties incl. in-note header + editor rows + add-property, citations suite, sync diagnostics) and the 05 §8.7 grid behavior matrix.

**Execution order: W4-1 → { W4-2..W4-8 } (parallel).**

## W4-1 · Accessible grid substrate — PR 1

1. One wrapped WPF DataGrid component playing the `AccessibleDataGrid` v2 role: 05 §8.7 matrix verbatim — headers announced on entry, cell-by-cell arrow navigation, keyboard sort/filter hooks, row-level actions, separately-addressable summary row, CSV/Markdown export commands, `ColumnRole`-driven row announcements, `audio_description`/`audio_summary` consumption where the surface provides them.
2. Column virtualization safe for AT (UIA ItemContainerPattern correctness under virtualization is a known WPF trap — test with JAWS/NVDA on 10k-row fixtures before feature grids build on it).
3. FlaUI conformance suite = the reusable §W-C gate every consuming surface inherits.
4. **Owns the transferred W3-1 table rows** (program wave table, deferred cross-wave rows): the reading-view tables' substrate-backed acceptance — §W-C included — closes here, not in Wave 3.

- [ ] §8.7 matrix green under FlaUI + human AT smoke on large fixtures
- [ ] Export commands + announcement grammar parity

## W4-2 · Link & structure panels: backlinks, outgoing, outline, embeds — PR 2

1. Same core row APIs as mac (backlink pages/snippets, outgoing resolution states, heading tree, embeds list); leaf docking + per-leaf context per W1-3.
2. Outline ships as a *feature* (nav utility), not the heading-a11y crutch (W3-1 owns native heading nav).

## W4-3 · Tasks panel + review flow — PR 3

1. Task rows/toggles/priority/scheduling data from core; review flow parity (`TasksReviewView` behavior); status phrases via canonical vocabulary (`TaskStatusPhrase` semantics move with W0.5-3 if not already).

## W4-4 · Properties — PR 4

1. In-note properties header + panel editing parity: typed editor rows, add-property flow, list values, type inference display — all writes via `set_property`/`delete_property` paths (§W-G: no parallel write machinery, mirroring N's decision 10).

## W4-5 · Citations suite — PR 5

1. Panel, popover, summary, bibliography: all from core citation artifacts (rendered references, `speech_text`, bibliography composition). Vault-config bibliography sources parity.

## W4-6 · Bases grid* — PR 6

1. N parity on the W4-1 substrate: `.base` open-as-tab, views (table + list + fallback banner), quick filter (transient, Ctrl+F grid-scoped), in-grid property editing, embeds (with W3-5), builder + raw-editor authoring surfaces, saved queries/dashboards — exactly N's shipped scope per matrix rows; `BasesResultSet`/`audio_*` artifacts consumed as-is. **Owns the transferred W3-5 `.base`-embed rows** (deferred cross-wave rows): their acceptance + §W-C close here.

## W4-7 · Local history* — PR 7

1. O parity (O shipped 2026-07-11 — conditional in name only): `Leaf.history` with **two segments** ("This note" + "Deleted" — deleted-file recovery), day-grouped version list, structured accessible diff (the `StructuredDiff` FFI — consumed, not re-derived), restore + **Restore As…**, changes-since-last-open (opt-in), markers toggle, `.canvas`/`.base` history coverage (#797).

## W4-8 · Sync diagnostics — PR 8

1. M parity: sync-detection report leaf (`Leaf.syncDiagnostics` equivalent) over `detect_sync_providers`; Windows provider probes (OneDrive/Dropbox markers) land core-side (decision 9) with fixtures; marker re-detection watcher = bounded `FileSystemWatcher` twin of #638's design (bounded scope, debounce, re-detect trigger only — no content watching).

- [ ] (each) matrix rows green; §W-A rows for data-bearing surfaces; §W-C via the W4-1 inherited gate
