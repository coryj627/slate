# N2 executable spec — Surface: FFI + gates, saved queries/dashboards, CLI verb

Issues: N2-1 ([#699](https://github.com/coryj627/slate/issues/699)) · N2-2 ([#700](https://github.com/coryj627/slate/issues/700)) · N2-3 ([#701](https://github.com/coryj627/slate/issues/701)). Milestone: [GH 14](https://github.com/coryj627/slate/milestone/14). One PR per issue.
Program: [00_program.md](../00_program.md) (locked decisions 7, 16–18; DoD §N-A–§N-F). 05 §8.4/§8.10/§8.11 are the API authority.
Backend norms: fmt/clippy pre-push, censuses, `make regenerate-bindings` on FFI changes.

**Execution order: N2-1 → { N2-2 ∥ N2-3 }.**

Baseline facts (verified 2026-07-06, this worktree):

- Handle-based FFI naming to mirror: canvas family (slate-uniffi/src/lib.rs:3672 — `open_canvas → u64`, projections, `close_canvas` idempotent); XD mirrors it. Bases live views follow the same shape.
- uniffi mirrors are 1:1 records, no logic; `#[uniffi::export]` on `VaultSession`; regenerate via `make regenerate-bindings` (CONTRIBUTING, repo-structure ADR).
- FTS `QueryResultSet` (search_db.rs:100) stays untouched (decision 7) — new types use the `Bases` prefix to avoid collision.
- CLI: `crates/slate-cli/src/commands/` verbs (read/list/search/links/properties/tasks/…) under the `slate.cli.v1` contract (Milestone M: announce-once gate, structural credential safety, machine-readable output conventions).
- Migrations: next free slots after N0-4's 021 ⇒ **022 saved_queries, 023 dashboards**.
- Atomic writes: temp+rename convention (DoD §D inherited).

---

## N2-1 · FFI surface + censuses + benches (#699) — PR 1

### Result types (pinned; 05 §8.4 refined)

```rust
pub struct BasesResultSet {
    pub columns: Vec<BasesColumn>,      // id, label (displayName or id), value_kind, role: ColumnRole
    pub rows: Vec<BasesRow>,            // file_path, task_ordinal: Option<u64>, values (column-ordered), audio_description
    pub groups: Vec<BasesGroup>,        // empty when ungrouped; label, row range, per-column summaries
    pub summaries: Vec<BasesSummaryCell>,
    pub total_count: u64,               // post-filter (N1-3 rule 6)
    pub filtered_count: u64,            // post-limit shown rows
    pub executed_at_ms: i64,            // the §N-B now-capture
    pub warnings: Vec<String>,          // view warnings (banner strings)
    pub view_error: Option<String>,     // decision-6 fail-loud (banner; rows empty)
    pub audio_summary: String,
}
pub enum ColumnRole { Primary, Identifier, Metadata, Metric, Action }
```

Values cross the FFI as a tagged `BasesValue` record family (text/number/bool/date/link/list-of-text rendering forms + `raw_kind`) — display-shaped, not a general value model; the Rust `Value` never crosses raw.

### Session API (pinned; 05 §8.11 refined to the handle idiom)

```rust
impl Session {
    pub fn bases_list(&self) -> Result<Vec<BaseFileSummary>, VaultError>;          // from bases_files (N0-4)
    pub fn open_base(&self, path: &str) -> Result<u64, VaultError>;                // parse via provider read; degraded ⇒ handle with warnings (never refuses)
    pub fn open_base_inline(&self, source: &str, this_path: Option<String>) -> Result<u64, VaultError>; // ```base fences + builder preview
    pub fn close_base(&self, handle: u64);                                         // idempotent
    pub fn base_views(&self, handle: u64) -> Result<Vec<BaseViewSummary>, VaultError>; // name, type, source, executable|fallback|error
    pub fn base_execute(&self, handle: u64, view: u32, this_path: Option<String>, cancel: &CancelToken)
        -> Result<BasesResultSet, VaultError>;                                     // N1 engine; cache per N1-2 rule 5
    pub fn base_apply_edit(&self, handle: u64, edit: BaseEdit) -> Result<(), VaultError>; // N0-3 splice + atomic save
    pub fn save_query_as_base(&self, query_json: &str, path: &str) -> Result<(), VaultError>; // canonical style (N0-3 rule 3)
    pub fn open_dql(&self, source: &str, this_path: Option<String>) -> Result<u64, VaultError>; // N0-5 parse_dql → same handle family (05 §8.11's parse_dql, handle-shaped); ```dataview fences + migration paste-in
    pub fn dql_as_base(&self, source: &str) -> Result<String, VaultError>;                     // one-shot converter: DQL → canonical .base text (the "convert this Dataview query" migration command; conversion losses ⇒ Err naming them, decision 6)
    pub fn base_export(&self, handle: u64, view: u32, format: ExportFormat)        // Csv | Markdown (decision 13)
        -> Result<String, VaultError>;
}
```

### Normative rules

1. uniffi mirrors 1:1 (records + `#[uniffi::export]`); regenerate bindings in the same PR. **CLI non-impact noted** in the PR (N2-3 is the CLI change, separately).
2. `open_base` parse failures follow N0-2 rule 1 — a degraded handle with `ParseFailed` warning, never an error return for content problems; only I/O errors return `Err`.
3. `base_execute` honors `this_path` (decision 20): tab context passes the base's own path; embeds pass the host note; sidebar passes the active note (N4-4). `this`-mentioning queries with `this_path: None` fail loud per N1-1 rule 5.
4. Export (decision 13): CSV per RFC 4180 (quoted, CRLF), Markdown as a pipe table with the display labels; both reflect the **executed view** (sort/group/limit applied; grid-side quick filter is a UI overlay and is N3-4's concern to include with confirmation).
5. Cancellation: `base_execute` takes the standard `CancelToken`; Swift-side epoch pattern per the canvas/search precedent.

### Censuses (the wave gate)

1. `census_bases_roundtrip` (from N0-3) runs against the session-level open→save path too (provider I/O included) — §N-A end-to-end.
2. `census_bases_determinism` (§N-B): corpus queries × permuted-insertion fixture vaults ⇒ identical `BasesResultSet` (serialize + compare).
3. `census_bases_cache_fresh` (§N-C) at session level (edits via session write APIs, not raw conn).
4. `census_bases_fail_loud` (§N-D): corpus × mutation set — every `Unsupported` construct either inert (unreferenced) or `view_error` naming it; membership never silently differs from the un-mutated baseline except via that error. The DQL golden corpus (N0-5) runs through `open_dql` here too: every converted query executes without panic, and every expected-`Unsupported` fixture surfaces its named error.
5. `census_bases_read_only` (§N-F): every corpus query leaves vault bytes hash-identical.

### Benches

`benches/bases_bench.rs` (criterion) @ 1k/10k/50k synthetic vaults (property-rich fixture generator shared with the FL benches where possible): indexed query p50 < 50 ms @ 10k, < 200 ms @ 50k (decision 16); parse+serialize round-trip p50 < 5 ms per file; cache-hit re-execute < 2 ms. Record in `BENCHMARKS.md`. Scan-bench diff re-asserted (§N-E).

- [ ] Session API + uniffi mirrors + regenerated bindings
- [ ] Censuses 1–5 clean incl. one `SLATE_CENSUS_FULL=1` release run in the PR description
- [ ] Bench baselines in BENCHMARKS.md
- [ ] fmt/clippy clean

## N2-2 · Saved queries + dashboards storage (#700) — PR 2

### Schema (migrations 022 + 023; 05 §8.3/§8.10 with shipped idioms)

```sql
CREATE TABLE saved_queries (
  id              TEXT PRIMARY KEY,     -- uuid
  name            TEXT NOT NULL UNIQUE, -- palette/pin display; rename = update
  description     TEXT,
  query_json      TEXT NOT NULL,        -- SlateQuery AST, versioned envelope {v, query}
  source_syntax   INTEGER NOT NULL,     -- 0=builder, 1=.base, 2=DQL (reserved, decision 2)
  created_at_ms   INTEGER NOT NULL,
  modified_at_ms  INTEGER NOT NULL
);
CREATE TABLE dashboards (
  id              TEXT PRIMARY KEY,
  name            TEXT NOT NULL UNIQUE,
  sections_json   TEXT NOT NULL,        -- ordered [{saved_query_id, heading_override?, view_override?}]
  created_at_ms   INTEGER NOT NULL,
  modified_at_ms  INTEGER NOT NULL
);
```

### Normative rules

1. API: `save_query(name, query_json) -> id`, `list_saved_queries() -> Vec<SavedQuerySummary>`, `get_saved_query(id)`, `rename_saved_query`, `delete_saved_query`; dashboard CRUD mirrors it; all uniffi-exported. Name uniqueness enforced; collision ⇒ typed error (UI offers rename).
2. `query_json` envelope is versioned (`{"v":1, …}`); unknown future versions load as inert entries with a warning (forward-compat, parser_version idiom).
3. **Durable-form rule** (decision 17): `export_saved_query_as_base(id, path)` writes canonical-style `.base` (N0-3 rule 3) — one command; the help doc (N4-5) names this as the backup/portability path. Deleting a dashboard never touches saved queries (refs, not ownership); dangling refs render as a labeled missing-section, never dropped silently.
4. Writes are transactional; `modified_at_ms` from the session clock.
5. A saved query executes through the same `base_execute`-family path (an ephemeral handle over the AST — `open_saved_query(id) -> u64`), so caching, `this`, cancellation, and export come for free.

### Tests (PR 2)

Unit per rule; round-trip: builder-AST → save → export `.base` → parse → same AST (§N-G precursor); forward-compat envelope test; dangling-dashboard-ref rendering.

- [ ] Migrations 022/023 + API + uniffi + bindings
- [ ] Tests incl. §N-G round-trip precursor
- [ ] fmt/clippy clean

## N2-3 · CLI `slate query` verb (#701) — PR 3

### Normative rules

1. Verb: `slate query (--base <vault-rel-path> [--view <name>] | --saved <name>) [--format json|csv|markdown] [--limit N] [--this <vault-rel-path>]`. Default format `json` (array of objects keyed by column label + `path`; the Obsidian-CLI parity shape, brief §7); `csv`/`markdown` reuse `base_export` exactly.
2. `slate.cli.v1` conventions (Milestone M — do not re-derive): announce-once capability gate lists the new verb; structural credential safety (no secrets in output paths); errors as machine-readable envelopes; exit 0 on success, 2 on view-error (fail-loud surfaces in the envelope), 1 on I/O.
3. Read-only guarantee: the verb takes the read session path; §N-F census covers it.
4. `--view` defaults to the first view; unknown view/name ⇒ error envelope listing available views (discoverability).
5. Contract doc: the M-owned CLI contract file gains the verb's schema in the same PR (M convention: contract and implementation land together).

### Tests (PR 3)

CLI integration tests over a fixture vault (json/csv/markdown goldens; view-error exit path; unknown-view listing); contract-schema check extended.

- [ ] Verb + contract update + tests
- [ ] fmt/clippy clean

**Wave-3 exit:** all five censuses clean at session level, baselines recorded, bindings regenerated, CLI contract updated.
