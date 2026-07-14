# FL0 executable spec — Derived note metadata: scanner pipeline, schema, FFI, censuses

Issues: FL0-1 ([#650](https://github.com/coryj627/slate/issues/650)) · FL0-2 ([#651](https://github.com/coryj627/slate/issues/651)) · FL0-3 ([#652](https://github.com/coryj627/slate/issues/652)). Milestone: [GH 31](https://github.com/coryj627/slate/milestone/31). Grouped delivery: FL-01 closes all three issues; see the program's authoritative ownership-table link.
Program: [00_program.md](../00_program.md) (locked decisions 3–5; DoD §FL-B/§FL-C). Backend norms apply: fmt/clippy pre-push, censuses for correctness invariants, host-independent slate-core (no macOS deps).

**Execution order inside FL-01: FL0-1 → FL0-2 → FL0-3.** (FL0-3's census harness may be developed alongside FL0-1; the complete group gates the PR.)

Baseline facts (verified 2026-07-14 at `origin/main` `6aa9fce`):

- `files` table includes `birthtime_ms` from migration `030_files_birthtime.sql`, populated by scanner/save-path `FileStat` upserts and preserved when a later stat cannot supply it. Change detection still uses the cached `(mtime_ms, size_bytes, ctime_ms)` triple. FL0 reuses `files.birthtime_ms`; it does not add another filesystem-created column.
- The scan slow path and save path refresh headings, links, properties, tags, and tasks inside their existing transaction seams in `session.rs`. FL0 adds `file_meta_db::replace_meta_for_file` to those same seams; implementation must locate the symbols rather than rely on the obsolete July 5 line numbers.
- `tasks` table already exists (migration `008_tasks.sql`) and is maintained through `tasks_db::replace_tasks_for_file`; per-file task counts are aggregate queries, not new derived state.
- `properties` table already exists (migration `005_properties.sql`) and frontmatter is parsed by `frontmatter::extract_frontmatter`; `title`/`created` lookups are joins, not new extraction.
- Latest migration is `030_files_birthtime.sql` in `db.rs::MIGRATIONS`; FL0-1 takes migration **031**. Migrations remain append-only and transactionally applied; adding one means a new `.sql` file plus a final `Migration` entry.
- Current FFI seams are the `FileSummary`, `DirNodeSummary`, `FileSummaryPage`, and `DirListing` records plus `VaultSession::list_files` and `VaultSession::list_dir_children` in `crates/slate-uniffi/src/lib.rs`. Their July 5 line references are stale; extend the named records and conversions additively, and regenerate Swift bindings with `make regenerate-bindings`.
- **CLI coupling:** `FileSummary` shapes the `slate.cli.v1` JSON for M-5 `read`/`list`/`search` (PR #646). The contract treats *additive* fields as non-breaking; FL0-2 must verify this against the contract doc (docs/plans/09_sync_cli/) and add fields only, never rename/retype.
- Census convention: `census_*` test fns, `SLATE_CENSUS_FULL=1` scaling via `census_scale()`; bench harness `crates/slate-core/benches/scan_bench.rs` (criterion, 1k/10k/50k synthetic vaults via `benches/common`); baselines in `BENCHMARKS.md` (v1 gates: first-open < 15 s @10k, < 60 s @50k).

---

## FL0-1 · `file_meta` derived table + scanner hook (#650) — closing PR FL-01

### Schema (new migration `031`)

```sql
CREATE TABLE file_meta (
    file_id     INTEGER PRIMARY KEY REFERENCES files(id) ON DELETE CASCADE,
    word_count  INTEGER NOT NULL,
    char_count  INTEGER NOT NULL,
    preview     TEXT NOT NULL         -- normalized excerpt, ≤ 300 chars, '' for empty body
);
```

Existing vaults are backfilled by the established derived-table slow-path replay: migration 031 ends with `UPDATE files SET mtime_ms = 0;`. The next scan populates `file_meta`; listing queries LEFT JOIN so an absent row has safe Swift fallbacks during replay. Record the one-time full reindex cost in FL-01. Migration 031 leaves `files.birthtime_ms` untouched.

### Rust: new module `crates/slate-core/src/file_meta_db.rs`

`pub fn replace_meta_for_file(tx: &Transaction, file_id: i64, contents: &str) -> Result<(), VaultError>` — called at the scan slow path and save path, after `properties_db` in the sequence. Non-markdown files get a row with `word_count = 0, char_count = 0, preview = ''` for uniform LEFT JOIN semantics.

**Normative derivation rules:**

1. **Body** = `contents` minus the frontmatter block per `frontmatter::frontmatter_range` — reuse it, never re-implement detection.
2. **`word_count`** = count of maximal runs of non-whitespace `char`s in body (Unicode `char::is_whitespace`). Code fences and inline code **count** (matches Obsidian's status-bar convention; cheaper; normative so the census is exact). **`char_count`** = body `chars().count()`.
3. **`preview`**: take body; drop fenced code blocks (``` and ~~~ fences, the fence lines and their content) and HTML blocks; per remaining line, strip leading heading markers (`#`+space), blockquote `>` runs, list markers (`-`/`*`/`+`/`1.` + space), and task prefixes (`- [x] ` any status char); replace wikilinks `[[t|alias]]`→alias, `[[t]]`→t (anchor-stripped), markdown links `[l](u)`→l; remove emphasis/strike markers (`*`, `_`, `~~`) and inline-code backticks; collapse all whitespace runs to single spaces; trim; truncate to **300 chars on a char boundary** (no ellipsis in storage — presentation adds it). Empty result ⇒ `''`.
4. Determinism: no wall clock, no locale; the same `contents` bytes always produce identical `(word_count, char_count, preview)`. Preview stripping stays isolated in `file_meta_db` and reuses the existing frontmatter and Markdown/source-span machinery; it must not create a second editor parser.

### Tests (FL-01)

- Unit fixtures: frontmatter-only file, fences (nested/tilde/unclosed), wikilinks with aliases+anchors, task lines, unicode whitespace, > 300-char bodies (boundary on a multibyte char), non-markdown file.
- Property (proptest): `word_count` ≡ naive split-whitespace count on the stripped body; preview never contains `\n` and never exceeds 300 chars.
- Hook-site integration: create → row present; save with changed body → row updated in the same transaction; delete → row cascades.

- [ ] Migration + schema-cookie slow-path replay
- [ ] `file_meta_db::replace_meta_for_file` + both hook sites
- [ ] Normative rules 1–4 with unit + property tests
- [ ] fmt/clippy clean; host-independent

## FL0-2 · FFI: enriched `FileSummary` (#651) — closing PR FL-01

Extend `FileSummary` (additive only — CLI coupling above):

```rust
pub struct FileSummary {
    // existing five fields unchanged …
    pub display_name: Option<String>, // frontmatter `title` (value_kind='text', non-empty after trim); None ⇒ caller falls back to stem
    pub created_date: Option<String>, // validated canonical frontmatter date-only `YYYY-MM-DD`; preserves the authored civil day
    pub created_ms: Option<i64>,      // parsed frontmatter datetime instant, else files.birthtime_ms when > 0; never UTC midnight for a date-only value
    pub word_count: Option<u32>,      // None for non-markdown or missing meta row
    pub preview: Option<String>,      // None when empty/missing
    pub task_total: u32,              // aggregate over tasks table
    pub task_open: u32,               //   … completed = 0
}
```

Rules:

- Populated by **one SQL statement** per listing (LEFT JOIN `file_meta`, LEFT JOIN a `tasks` GROUP BY subquery, LEFT JOIN `properties` on `key='title'` / `key='created'`) — no per-row query loops, no N+1. Applies to both `list_files` and `list_dir_children`; paging and the existing case-insensitive name ordering are unchanged (sorting by the new fields is app-side, locked decision 5).
- `display_name`: `properties.value_kind='text'` and trimmed-non-empty only; any other kind ⇒ None (a list-valued `title:` is authoring noise, not a name).
- Resolve the scalar `created` value in this order: a strict canonical `YYYY-MM-DD` whose year/month/day form a valid civil date; a valid datetime with an explicit or parser-defined offset; then positive `files.birthtime_ms`. A valid date-only value is copied unchanged into `created_date`; it never becomes an epoch value. A datetime becomes `created_ms`. Missing/invalid date syntax falls through to datetime parsing, and missing/invalid datetime falls through to birthtime.
- `created_ms` may carry the birthtime fallback alongside a non-NULL `created_date`; consumers must give `created_date` presentation and created-sort/group precedence. The fallback remains useful to older/additive-field consumers, but it cannot override the authored civil day.
- Swift parses `created_date` into year/month/day components in the user's current calendar/timezone, presents that authored day without an instant or UTC-midnight conversion, and derives local start-of-day only when it needs a numeric sort/group key. Datetime and birthtime values remain ordinary instants in `created_ms`.
- Verify additive-fields stance against the `slate.cli.v1` contract doc and note the verification in the PR; regenerate bindings (`make regenerate-bindings`).
- Budget: `list_dir_children` on the 10k-vault root ≤ **10 ms** (it is on the sidebar's first-paint path; bench in FL0-3).

- [ ] Record extension + single-statement joins in both listings
- [ ] `slate.cli.v1` additive-check noted in PR; bindings regenerated
- [ ] FFI-shape unit tests: title precedence; valid/leap/invalid date-only; datetime offsets; date-only + birthtime coexistence and precedence; datetime/birthtime fallback; task aggregates; non-markdown rows
- [ ] Swift binding smoke test

## FL0-3 · Censuses + benchmarks — the wave gate (#652) — closing PR FL-01

**Censuses** (`crates/slate-core/src/session/tests/file_meta.rs`, `census_*` convention):

1. `census_file_meta_matches_rescan` — adversarial random walk (N ops from {create/edit/save/rename/move/delete, frontmatter add/remove/retype of `title`/`created`, task-list edits}); after **every** op, every file's `file_meta` row and the joined `FileSummary` fields (including the distinct `created_date`/`created_ms` resolution) ≡ a from-scratch recompute of the same file's bytes. Random + exhaustive small-vault sweep per the adversarial-census methodology.
2. `census_file_meta_scan_parity` — full vault scanned cold ≡ vault built incrementally op-by-op (catches hook-site omissions).

**Benchmarks** (`scan_bench.rs` additions): `scan_initial/{1k,10k,50k}` re-run — overhead vs. recorded baseline ≤ **5%** (DoD §FL-C); `list_dir_children_meta/{10k}` ≤ 10 ms; `save_path/{10k}` unchanged O(changed-file). Record in `BENCHMARKS.md`.

Wave-1 exit: both censuses clean (incl. one `SLATE_CENSUS_FULL=1` release run), baselines recorded, no scan/save regression.

- [ ] Both censuses + exhaustive sweep
- [ ] Bench baselines recorded in BENCHMARKS.md
- [ ] One `SLATE_CENSUS_FULL=1` release run in the PR description
