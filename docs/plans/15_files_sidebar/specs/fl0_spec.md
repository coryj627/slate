# FL0 executable spec — Derived note metadata: scanner pipeline, schema, FFI, censuses

Issues: FL0-1 ([#650](https://github.com/coryj627/slate/issues/650)) · FL0-2 ([#651](https://github.com/coryj627/slate/issues/651)) · FL0-3 ([#652](https://github.com/coryj627/slate/issues/652)). Milestone: [GH 31](https://github.com/coryj627/slate/milestone/31). One PR per issue.
Program: [00_program.md](../00_program.md) (locked decisions 3–5; DoD §FL-B/§FL-C). Backend norms apply: fmt/clippy pre-push, censuses for correctness invariants, host-independent slate-core (no macOS deps).

**Execution order: FL0-1 → FL0-2 → FL0-3.** (FL0-3's census harness may be developed alongside FL0-1; it gates the wave.)

Baseline facts (verified 2026-07-05, this worktree):

- `files` table (migrations/001_init.sql): `id` INTEGER PK, `path` TEXT UNIQUE (vault-relative, `/`-separated), `is_markdown` 0|1, `mtime_ms`, `size_bytes`, `ctime_ms`, `content_hash` (blake3). Change detection: slow path runs when the `(mtime_ms, size_bytes, ctime_ms)` triple differs from the cached row.
- Derived-data pipeline: the slow path and the save path each call, inside one transaction, `headings_db::replace_headings_for_file`, `links_db::replace_links_for_file` (session.rs:968–972 scan; :3233–3236 save), `properties_db::replace_properties_for_file`, `tasks_db::replace_tasks_for_file` (tasks_db.rs:41–73), `tags_db::replace_tags_for_file` (tags_db.rs:50–71). **These two call sites are the complete set of derived-data hook points; FL0 adds one more `replace_*` call at each, nothing else.**
- `tasks` table already exists (migrations/008_tasks.sql): per-task rows `(file_id, ordinal, text, status_char, completed, due_ms, scheduled_ms, priority, recurrence, line, byte_offset)`, PK `(file_id, ordinal)`, extraction in tasks.rs (`extract_tasks`, :81; frontmatter/fence-aware; `[`+`]` byte prefilter :89). **Per-file task counts are therefore an aggregate query, not new derived state.**
- `properties` table already exists (migrations/005_properties.sql): `(file_id, ordinal, key, value_kind ∈ {text,number,boolean,date,datetime,wikilink,list,tag_list}, value_text)`; frontmatter parsed by frontmatter.rs (`extract_frontmatter` :465, yaml-rust2). **Frontmatter `title`/`created` lookups are joins, not new extraction.**
- Latest migration at spec time: `019_file_tags.sql`. FL0-1 takes the next free number (020 at spec time — verify at implementation).
- FFI records: `FileSummary { path, name, mtime_ms, size_bytes, is_markdown }` (slate-uniffi/src/lib.rs:1159–1166); `DirNodeSummary { id, path, name, child_dir_count, child_file_count }` (:1376–1383); `DirListing { dirs, files: FileSummaryPage }` (:1482–1486). Session methods: `list_files(filter, paging)` (:273–280), `list_dir_children(parent_path, paging)` (:336–343). Swift bindings regenerate via `scripts/build-mac-app.sh:61–75` / `make regenerate-bindings`.
- **CLI coupling:** `FileSummary` shapes the `slate.cli.v1` JSON for M-5 `read`/`list`/`search` (PR #646). The contract treats *additive* fields as non-breaking; FL0-2 must verify this against the contract doc (docs/plans/09_sync_cli/) and add fields only, never rename/retype.
- Census convention: `census_*` test fns, `SLATE_CENSUS_FULL=1` scaling via `census_scale()`; bench harness `crates/slate-core/benches/scan_bench.rs` (criterion, 1k/10k/50k synthetic vaults via `benches/common`); baselines in `BENCHMARKS.md` (v1 gates: first-open < 15 s @10k, < 60 s @50k).

---

## FL0-1 · `file_meta` derived table + scanner hook (#650) — PR 1

### Schema (new migration, next free number)

```sql
CREATE TABLE file_meta (
    file_id     INTEGER PRIMARY KEY REFERENCES files(id) ON DELETE CASCADE,
    word_count  INTEGER NOT NULL,
    char_count  INTEGER NOT NULL,
    preview     TEXT NOT NULL,        -- normalized excerpt, ≤ 300 chars, '' for empty body
    created_ms  INTEGER               -- filesystem birthtime; NULL when the platform can't provide one
);
```

Backfill in the migration is **not** required: rows appear as files pass through the slow path, and `list_*` queries LEFT JOIN (absent row ⇒ NULL fields, Swift renders fallbacks). A one-time full-vault backfill would re-read every file — instead FL0-1 bumps the scanner's schema-cookie so the next open takes the slow path per file exactly once (same mechanism previous derived-table migrations used — locate the cookie beside the migration list at implementation; if no such cookie exists, add `meta_version` to the scan fast-path triple comparison and document it in the PR).

### Rust: new module `crates/slate-core/src/file_meta_db.rs`

`pub fn replace_meta_for_file(tx: &Transaction, file_id: i64, path: &Path, contents: &str) -> Result<(), VaultError>` — called at exactly the two hook sites (scan slow path, save path), after `properties_db` in the sequence. Non-markdown files get a row with `word_count = 0, char_count = 0, preview = ''` (uniform LEFT JOIN semantics; `created_ms` still populated).

**Normative derivation rules:**

1. **Body** = `contents` minus the frontmatter block per `frontmatter_range` (frontmatter.rs:137) — reuse it, never re-implement detection.
2. **`word_count`** = count of maximal runs of non-whitespace `char`s in body (Unicode `char::is_whitespace`). Code fences and inline code **count** (matches Obsidian's status-bar convention; cheaper; normative so the census is exact). **`char_count`** = body `chars().count()`.
3. **`preview`**: take body; drop fenced code blocks (``` and ~~~ fences, the fence lines and their content) and HTML blocks; per remaining line, strip leading heading markers (`#`+space), blockquote `>` runs, list markers (`-`/`*`/`+`/`1.` + space), and task prefixes (`- [x] ` any status char); replace wikilinks `[[t|alias]]`→alias, `[[t]]`→t (anchor-stripped), markdown links `[l](u)`→l; remove emphasis/strike markers (`*`, `_`, `~~`) and inline-code backticks; collapse all whitespace runs to single spaces; trim; truncate to **300 chars on a char boundary** (no ellipsis in storage — presentation adds it). Empty result ⇒ `''`.
4. **`created_ms`** = filesystem birthtime (`std::fs::Metadata::created()`), millis; `Err` ⇒ NULL. Captured on **insert only** — never updated by later scans (birthtime is immutable; rename/move keeps the row via `file_id`). Frontmatter `created` overrides happen at *query* time (FL0-2), not here — derived state stays a pure function of (file bytes, fs metadata).
5. Determinism: no wall clock, no locale; the same `contents` bytes always produce identical `(word_count, char_count, preview)`.

### Tests (PR 1)

- Unit fixtures: frontmatter-only file, fences (nested/tilde/unclosed), wikilinks with aliases+anchors, task lines, unicode whitespace, > 300-char bodies (boundary on a multibyte char), non-markdown file.
- Property (proptest): `word_count` ≡ naive split-whitespace count on the stripped body; preview never contains `\n` and never exceeds 300 chars.
- Hook-site integration: create → row present; save with changed body → row updated in the same transaction; delete → row cascades.

- [ ] Migration + schema-cookie slow-path replay
- [ ] `file_meta_db::replace_meta_for_file` + both hook sites
- [ ] Normative rules 1–5 with unit + property tests
- [ ] fmt/clippy clean; host-independent

## FL0-2 · FFI: enriched `FileSummary` (#651) — PR 2

Extend `FileSummary` (additive only — CLI coupling above):

```rust
pub struct FileSummary {
    // existing five fields unchanged …
    pub display_name: Option<String>, // frontmatter `title` (value_kind='text', non-empty after trim); None ⇒ caller falls back to stem
    pub created_ms: Option<i64>,      // frontmatter `created` (date|datetime, parsed to UTC-midnight/instant ms) if present, else file_meta.created_ms
    pub word_count: Option<u32>,      // None for non-markdown or missing meta row
    pub preview: Option<String>,      // None when empty/missing
    pub task_total: u32,              // aggregate over tasks table
    pub task_open: u32,               //   … completed = 0
}
```

Rules:

- Populated by **one SQL statement** per listing (LEFT JOIN `file_meta`, LEFT JOIN a `tasks` GROUP BY subquery, LEFT JOIN `properties` on `key='title'` / `key='created'`) — no per-row query loops, no N+1. Applies to both `list_files` and `list_dir_children`; paging and the existing case-insensitive name ordering are unchanged (sorting by the new fields is app-side, locked decision 5).
- `display_name`: `properties.value_kind='text'` and trimmed-non-empty only; any other kind ⇒ None (a list-valued `title:` is authoring noise, not a name).
- `created` property parse: `date` ⇒ UTC midnight ms; `datetime` ⇒ instant ms; unparseable ⇒ fall back to `file_meta.created_ms`.
- Verify additive-fields stance against the `slate.cli.v1` contract doc and note the verification in the PR; regenerate bindings (`make regenerate-bindings`).
- Budget: `list_dir_children` on the 10k-vault root ≤ **10 ms** (it is on the sidebar's first-paint path; bench in FL0-3).

- [ ] Record extension + single-statement joins in both listings
- [ ] `slate.cli.v1` additive-check noted in PR; bindings regenerated
- [ ] FFI-shape unit tests: title precedence, created precedence, task aggregates, non-markdown rows
- [ ] Swift binding smoke test

## FL0-3 · Censuses + benchmarks — the wave gate (#652) — PR 3

**Censuses** (`crates/slate-core/src/session/tests/file_meta.rs`, `census_*` convention):

1. `census_file_meta_matches_rescan` — adversarial random walk (N ops from {create/edit/save/rename/move/delete, frontmatter add/remove/retype of `title`/`created`, task-list edits}); after **every** op, every file's `file_meta` row and the joined `FileSummary` fields ≡ a from-scratch recompute of the same file's bytes. Random + exhaustive small-vault sweep per the adversarial-census methodology.
2. `census_file_meta_scan_parity` — full vault scanned cold ≡ vault built incrementally op-by-op (catches hook-site omissions).

**Benchmarks** (`scan_bench.rs` additions): `scan_initial/{1k,10k,50k}` re-run — overhead vs. recorded baseline ≤ **5%** (DoD §FL-C); `list_dir_children_meta/{10k}` ≤ 10 ms; `save_path/{10k}` unchanged O(changed-file). Record in `BENCHMARKS.md`.

Wave-1 exit: both censuses clean (incl. one `SLATE_CENSUS_FULL=1` release run), baselines recorded, no scan/save regression.

- [ ] Both censuses + exhaustive sweep
- [ ] Bench baselines recorded in BENCHMARKS.md
- [ ] One `SLATE_CENSUS_FULL=1` release run in the PR description
