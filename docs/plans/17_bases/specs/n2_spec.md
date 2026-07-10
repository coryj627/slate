# N2 executable spec — Surface: FFI + gates, saved queries/dashboards, CLI verb

Issues: N2-1 ([#699](https://github.com/coryj627/slate/issues/699)) · N2-2 ([#700](https://github.com/coryj627/slate/issues/700)) · N2-3 ([#701](https://github.com/coryj627/slate/issues/701)). Milestone: [GH 14](https://github.com/coryj627/slate/milestone/14). One PR per issue.
Program: [00_program.md](../00_program.md) (locked decisions 7, 16–18; DoD §N-A–§N-F). 05 §8.4/§8.10/§8.11 are the API authority.
Backend norms: fmt/clippy pre-push, censuses, `make regenerate-bindings` on FFI changes.

**Execution order: N2-1 → { N2-2 ∥ N2-3 }.**

Baseline facts (verified 2026-07-06; implementation and remediation re-verified 2026-07-10):

- Handle-based FFI naming to mirror: canvas family (slate-uniffi/src/lib.rs:4573 — `open_canvas` returns `CanvasOpenInfo` whose `handle` field is the `u64`; `close_canvas` :4578, idempotent); XD mirrors it. Bases live views follow the same shape.
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
    pub shown_count: u64,               // post-limit rows actually shown (deliberately NOT 05 §8.4's `filtered_count` — named for what it carries; gap O14)
    pub executed_at_ms: i64,            // the §N-B now-capture
    pub warnings: Vec<String>,          // view warnings (banner strings)
    pub view_error: Option<String>,     // decision-6 fail-loud (banner; rows empty)
    pub audio_summary: String,
}
pub enum ColumnRole { Primary, Identifier, Metadata, Metric, Action }
```

Values cross the FFI as a tagged `BasesValue` record family (text/number/bool/date/link/list-of-text rendering forms + `raw_kind`, `display`, and `sort_key`) — display-shaped, not a general value model; the Rust `Value` never crosses raw. `sort_key` is an engine-produced, lexicographically comparable encoding of the native `Value` comparator, including numeric total order and exact typed-family precedence. Engine-backed grids preserve the returned row order; local-only preview/dashboard projections use `sort_key` instead of reimplementing Rust ordering. A result column's `value_kind` is inferred from its first non-null value across the result, not from row zero; an all-null column remains `null`.

### Session API (pinned; 05 §8.11 refined to the handle idiom)

```rust
impl Session {
    pub fn bases_list(&self) -> Result<Vec<BaseFileSummary>, VaultError>;          // from bases_files (N0-4)
    pub fn open_base(&self, path: &str) -> Result<u64, VaultError>;                // parse via provider read; degraded ⇒ handle with warnings (never refuses)
    pub fn open_base_inline(&self, source: &str, this_path: Option<String>) -> Result<u64, VaultError>; // ```base fences + builder preview
    pub fn close_base(&self, handle: u64);                                         // idempotent
    pub fn base_views(&self, handle: u64) -> Result<Vec<BaseViewSummary>, VaultError>; // name, type, source, executable|fallback|error
    pub fn base_execute(&self, handle: u64, view: u32, this_path: Option<String>,
                        quick_filter: Option<String>, cancel: &CancelToken)
        -> Result<BasesResultSet, VaultError>;                                     // N1 engine; cache per N1-2 rule 5. quick_filter (decision 12) narrows rows engine-side over displayed column values (value_text_norm folding) so summaries, counts, audio_summary and audio_description all reflect it from ONE source of truth — never a Swift re-implementation; a quick-filtered execution is never cached and never persisted (it's a param, not state)
    pub fn open_query(&self, query_json: &str, this_path: Option<String>) -> Result<u64, VaultError>; // ephemeral handle over an unsaved SlateQuery AST — the builder live preview (N4-2) and open_saved_query's substrate; 05 §8.11's execute_query in handle form (gap G8)
    pub fn open_saved_query(&self, id: &str) -> Result<u64, VaultError>;           // N2-2 store → open_query
    pub fn base_apply_edit(&self, handle: u64, edit: BaseEdit) -> Result<(), VaultError>; // one-edit convenience wrapper
    pub fn base_apply_edits(&self, handle: u64, edits: Vec<BaseEdit>) -> Result<(), VaultError>; // validate + sequentially reparse the full batch, then one atomic save
    pub fn save_query_as_base(&self, query_json: &str, path: &str) -> Result<(), VaultError>; // canonical style (N0-3 rule 3)
    pub fn open_dql(&self, source: &str, this_path: Option<String>) -> Result<u64, VaultError>; // N0-5 parse_dql → same handle family (05 §8.11's parse_dql, handle-shaped); ```dataview fences + migration paste-in
    pub fn dql_as_base(&self, source: &str) -> Result<String, VaultError>;                     // one-shot converter: DQL → canonical .base text (the "convert this Dataview query" migration command; conversion losses ⇒ Err naming them, decision 6)
    pub fn base_export(&self, handle: u64, view: u32, format: ExportFormat,        // Csv | Markdown (decision 13)
                       quick_filter: Option<String>) -> Result<String, VaultError>; // same engine-side quick-filter semantics as base_execute
}

pub fn classify_slate_query_fence(source: &str)
    -> Result<SlateQueryFenceClassification, SlateQueryFenceError>; // Core free function; mirrored one-shot through UniFFI
```

**05 §8.11 mapping (recorded — gap G8):** `execute_query` → `open_query` + `base_execute` (handle idiom); `parse_base_file` → `open_base`; `save_as_base_file` → `save_query_as_base`; `parse_dql` → `open_dql`/`dql_as_base`; `create_query_builder` → dropped (the builder is a UI surface over the AST, not a session object); `save_query`/`list_saved_queries` → N2-2 as sketched.

### Normative rules

1. uniffi mirrors 1:1 (records + `#[uniffi::export]`); regenerate bindings in the same PR. **CLI non-impact noted** in the PR (N2-3 is the CLI change, separately).
2. `open_base` parse failures follow N0-2 rule 1 — a degraded handle with `ParseFailed` warning, never an error return for content problems; only I/O errors return `Err`.
3. `base_execute` honors `this_path` (decision 20): tab context passes the base's own path; embeds pass the host note; sidebar passes the active note (N4-4). Precedence (pinned): an open-time `this_path` (`open_base_inline`/`open_dql`/`open_query`) is the default; a `Some` at `base_execute` overrides it for that execution. `this`-mentioning queries with no `this_path` from either source fail loud per N1-1 rule 5.
4. Export (decision 13): CSV per RFC 4180 (quoted, CRLF), Markdown as a pipe table with the display labels; both reflect the **executed view** (sort/group/limit applied), and `quick_filter: Some(_)` exports exactly what a quick-filtered grid shows — the include-or-not confirmation UX is N3-4's.
5. Cancellation: `base_execute` takes the standard `CancelToken`; Swift-side epoch pattern per the canvas/search precedent.
6. **Atomic edit batches:** `base_apply_edits` validates and sequentially reparses every dependent edit in memory before one provider save. Validation, serialization, write-conflict, or persistence failure changes neither vault bytes nor the open handle, query projections, transient state, or cache. `base_apply_edit` delegates to the same path with a one-element vector; an empty batch is a true no-op.
7. **`slate-query` classification:** Core parses the complete fence body as one YAML document. A top-level scalar `query` (string, number, or boolean) selects saved-query-reference mode and may carry a scalar `view`; a mapping without `query`, a non-mapping root, or an empty document selects inline-Base mode. Malformed/multi-document YAML, non-scalar or null reference fields, and an empty `query` fail loud through the typed error mirrored by UniFFI. Swift never line-sniffs or trims the body to make this decision.

### Censuses (the wave gate)

1. `census_bases_roundtrip` (from N0-3) runs against the session-level open→save path too (provider I/O included) — §N-A end-to-end.
2. `census_bases_determinism` (§N-B): corpus queries × permuted-insertion fixture vaults ⇒ identical `BasesResultSet` (serialize + compare).
3. `census_bases_cache_fresh` (§N-C) at session level (edits via session write APIs, not raw conn).
4. `census_bases_fail_loud` (§N-D): corpus × mutation set — every `Unsupported` construct is either inert when unreferenced, a named error-marker cell when referenced only by a column/summary (membership identical to the un-mutated baseline), or a `view_error` naming it when used in source/filter/sort/group positions. The DQL golden corpus (N0-5) runs through `open_dql` here too. Every case has exactly one disposition — `supported`, `unsupported`, or `runtime_fail_loud` — and every declared compatibility coverage tag has exactly one owning case; the test requires exact set equality, not subset coverage.
5. `census_bases_read_only` (§N-F): every corpus query leaves vault bytes hash-identical.

### Benches

`benches/bases_bench.rs` (criterion) @ 1k/10k/50k synthetic vaults (property-rich fixture generator shared with the FL benches where possible): indexed query p50 < 50 ms @ 10k, < 200 ms @ 50k (decision 16); parse+serialize round-trip p50 < 5 ms per file; cache-hit re-execute < 2 ms. Record in `BENCHMARKS.md`. Scan-bench diff re-asserted (§N-E).

**Close-out evidence (2026-07-10, through `dacb2b0`):** the DQL corpus contains 170 unique cases
(45 `supported`, 117 `unsupported`, 8 `runtime_fail_loud`) and exactly owns all
426 expected coverage tags. The generated DQL census executes 4,096 statements;
session, scanner, cache, cancellation, read-only, and mutation censuses pass in
default and `SLATE_CENSUS_FULL=1` modes. Ordered DQL tags (migration 024) and
inline fields (migration 025) rebuild transactionally with rollback, reindex,
delete, and large-file purge coverage. Migration 026 safely reindexes typed
Date/Datetime/Wikilink list elements. UniFFI bindings regenerate cleanly.

- [x] Session API + uniffi mirrors + regenerated bindings
- [x] Censuses 1–5 clean incl. one `SLATE_CENSUS_FULL=1` run recorded in the milestone audit
- [x] Bench baselines in BENCHMARKS.md
- [x] fmt/clippy clean

## N2-2 · Saved queries + dashboards storage (#700) — PR 2

### Schema (migrations 022 + 023; 05 §8.3/§8.10 with shipped idioms)

```sql
CREATE TABLE saved_queries (
  id              TEXT PRIMARY KEY,     -- uuid
  name            TEXT NOT NULL UNIQUE, -- palette/pin display; rename = update
  description     TEXT,
  query_json      TEXT NOT NULL,        -- SlateQuery AST, versioned envelope {v, query}
  source_syntax   INTEGER NOT NULL,     -- 0=builder, 1=.base, 2=DQL (recorded when a converted DQL query is saved)
  created_at_ms   INTEGER NOT NULL,
  modified_at_ms  INTEGER NOT NULL
);
CREATE TABLE dashboards (
  id              TEXT PRIMARY KEY,
  name            TEXT NOT NULL UNIQUE,
  sections_json   TEXT NOT NULL,        -- ordered [{saved_query_id, heading_override?, view_override?}]; override is renderer state: absent/blank=Default, table, or list; other values remain preserved and fail visibly
  created_at_ms   INTEGER NOT NULL,
  modified_at_ms  INTEGER NOT NULL
);
```

### Normative rules

1. API: `save_query(name, query_json) -> id`, `list_saved_queries() -> Vec<SavedQuerySummary>`, `get_saved_query(id)`, `rename_saved_query`, `delete_saved_query`; dashboard CRUD mirrors it; all uniffi-exported. Name uniqueness enforced; collision ⇒ typed error (UI offers rename).
2. `query_json` envelope is versioned (`{"v":1, …}`); unknown future versions load as inert entries with a warning (forward-compat, parser_version idiom).
3. **Durable-form rule** (decision 17): `export_saved_query_as_base(id, path)` writes canonical-style `.base` (N0-3 rule 3) — one command; the help doc (N4-5) names this as the backup/portability path. Deleting a dashboard never touches saved queries (refs, not ownership); dangling refs render as a labeled missing-section, never dropped silently.
4. Writes are transactional; `modified_at_ms` from the session clock.
5. A saved query executes through `open_saved_query` (N2-1's API block — a thin wrapper over `open_query`), so caching, cancellation, and export come for free. `this` is **not** free: a saved query has no backing file, so its `this` context comes entirely from where it's opened (sidebar = active note per N4-4; ephemeral tab = none, N4-3 rule 1).

### Tests (PR 2)

Unit per rule; round-trip: builder-AST → save → export `.base` → parse → same AST (§N-G precursor); forward-compat envelope test; dangling-dashboard-ref rendering; **relaunch persistence** (the vendored test list's "saved queries persist"): write queries + dashboards, drop and reopen the session against the same vault, `list_saved_queries`/dashboards identical.

- [x] Migrations 022/023 + API + uniffi + bindings
- [x] Tests incl. §N-G round-trip precursor
- [x] fmt/clippy clean

## N2-3 · CLI `slate query` verb (#701) — PR 3

### Normative rules

1. Verb: `slate query (--base <vault-rel-path> [--view <name>] | --saved <name>) [--format json|csv|markdown] [--limit N] [--this <vault-rel-path>]`. Default format `json` (array of objects keyed by column label + `path`; the Obsidian-CLI parity shape, brief §7); `csv`/`markdown` reuse `base_export` exactly.
2. `slate.cli.v1` conventions (Milestone M — do not re-derive): announce-once capability gate lists the new verb; structural credential safety (no secrets in output paths); errors as machine-readable envelopes; exit 0 on success, 2 on view-error (fail-loud surfaces in the envelope), 1 on I/O.
3. Read-only guarantee: the verb takes the read session path; §N-F census covers it.
4. `--view` defaults to the first view; unknown view/name ⇒ error envelope listing available views (discoverability).
5. Contract doc: the `slate.cli.v1` contract lives in [`../../09_sync_cli/m_spec.md`](../../09_sync_cli/m_spec.md) (plus the `slate-cli` crate's doc comments) — that spec's contract section gains the verb's schema in the same PR (M convention: contract and implementation land together).

### Tests (PR 3)

CLI integration tests over a fixture vault (json/csv/markdown goldens; view-error exit path; unknown-view listing); contract-schema check extended.

- [x] Verb + contract update + tests
- [x] fmt/clippy clean

**Wave-3 exit:** all five censuses clean at session level, baselines recorded, bindings regenerated, CLI contract updated.
