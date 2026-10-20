# WB-19 — A create heals only the dangling links its path could resolve

Issues: #1426. Size M. Lanes: rust, windows (the Windows lane's
`cargo test -p slate-core`), swift (compile only: no FFI change, but a
schema bump touches every cache both hosts open). AT: none. Wave: W7,
slot 1. Depends on: WB-18 (shared `links_db.rs` and the `session.rs`
create path; §6 sequencing "`links_db.rs`, `session.rs` link paths: WB-18,
then WB-19"). Parallel with: WB-20, WB-21. Decision: none.

## Goal

Creating a note stops reading every `files` row and every unresolved link
in the vault. The heal that makes a ghost link resolve the moment its file
appears (W6-2 PR B, #746) reads only the dangling rows whose authored
target could name the created path — found through an index on a folded
form of `links.target_raw`, so finding them costs the rows, not a table
scan — and resolves each through the folded-name lookup, so the answer is
the whole-index answer. A create's cost tracks the note again (locked 05
§4, §9.3.1).

## Current state on main

Verified against the worktree; the plan's lines hold.

- `session.rs:6107` `create_exclusive_binding` (every create path:
  `create_exclusive`, `_reporting`, `_bytes`, the deleted-file recovery)
  calls `crate::links_db::re_resolve_unresolved_links(&tx)` at
  `:6207-6208` inside the create's transaction, then replays each affected
  source's linkset to the graph (`:6209-6220`). The comment at `:6197-6206`
  records why the heal exists.
- `links_db.rs:458-463` `re_resolve_unresolved_links(tx)` wraps
  `re_resolve_unresolved_links_under(tx, &CancelToken::new())`
  (`:465-508`): `SELECT links.rowid, files.path, links.target_raw,
  links.target_anchor FROM links JOIN files … WHERE links.target_path IS
  NULL AND links.is_external = 0` (every dangling row, `:470-475`), then
  `InMemoryVaultIndex::new(indexed_paths_in_path_order(tx)?)` (every
  `files` row, `:490`), then `resolve_link(&target_raw, anchor,
  &source_path, &index)` per row (`:497`). Since #1429 each row resolves
  from its own candidates in the snapshot; the two whole-table reads
  remain, as the issue's 2026 comment says. The same function serves the
  scan (`session.rs:10558`, under the scan's token) and the rename and
  batch paths (`:13139`, `:13485`, `:14446`, `:17013`).
- `links` schema: `migrations/004_links.sql:27-45`: `target_raw TEXT NOT
  NULL`, `target_path TEXT`, indexes `idx_links_source(source_file_id)`
  and `idx_links_target(target_path)`. No index on `target_raw` or a fold
  of it.
- The fold indexes to mirror: `migrations/037_path_fold_indexes.sql`
  (`idx_files_path_fold ON files(slate_tree_sort_key(path))`) and
  `039_file_name_fold_index.sql` (`idx_files_name_fold ON
  files(slate_tree_sort_key(name))`), both with the "DROP INDEX IF EXISTS
  … then CREATE" replay discipline. The UDF is registered by
  `db::register_connection_functions` (`db.rs:328`); its Rust twin is
  `db::tree_sort_key` (`:393`), which `FoldedNameLookup` folds its keys
  with (`links_db.rs:216-219`).
- The migration runner: `db.rs:75` `const MIGRATIONS` (40 entries; the
  last is `040_change_feed.sql`, `:233-234`), `migrate_with` (`:469-500`).
  **Two consequences of a bump the plan does not spell out:** a migration
  at version ≥ `change_feed::FEED_MIGRATION_VERSION` (40,
  `change_feed.rs:75`) runs `change_feed::clear_after_migration` in the
  same transaction (`db.rs:489-491`), clearing every session's cursor and
  row; and a session opened at the older schema is fenced out of writing
  (`DbError::SchemaVersionSkew`, `:60-64`; `begin_fenced`) until it
  restarts. Migration 041 inherits both. The upgrade-path fixtures use
  `migrate_up_to` (`:528`).
- The probe to invert: `LinkCandidateSelector::probe`
  (`link_resolver.rs:512-525`): `Exact { keys }` for a qualified or rooted
  target (the literal lowercased path, then each implied Markdown
  extension), `Basename` for a bare name (the literal, then each implied
  extension; `MD_EXTENSIONS`). `candidate_probe(target_raw)` (`:583-586`)
  computes it for a target with no source.
- Counters and fixtures: `link_resolver::SNAPSHOT_PATHS` (`:164`),
  `links_db::PATH_ORDER_SNAPSHOT_PATHS` (`:109`),
  `census_fixtures::INDEXED_PATHS` (`:670`); the existing create fact
  `save_link_lookup.rs:137`
  `a_create_resolves_its_links_from_its_own_targets_rows_only` (it counts
  the create's *own* resolution and tolerates the heal; `links_db.rs:88-93`
  says so) and `link_pass.rs:72`
  `a_created_file_still_resolves_a_previously_unresolved_link`.

## Reference

Core only; no host twin. The behavioural spec is #1426's text plus
`LinkCandidates`'s contract (`links_db.rs:122-130`). The heal must heal
exactly what today's vault-wide heal heals: the issue's second fact.
Migration conventions: 037 and 039 (`migrations/`), and the test
`migrate_from_empty_lands_at_latest_version` (`db.rs:760`).

## Design

1. **Migration 041** `migrations/041_links_target_fold_index.sql` (new):
   `DROP INDEX IF EXISTS idx_links_target_fold; CREATE INDEX
   idx_links_target_fold ON links(slate_tree_sort_key(target_raw)) WHERE
   target_path IS NULL AND is_external = 0;` and a second partial index on
   the folded *final component* of `target_raw`, `idx_links_target_name_fold
   ON links(slate_tree_sort_key(slate_link_target_name(target_raw)))` with
   the same `WHERE`, where `slate_link_target_name` is a new deterministic
   UDF registered in `register_connection_functions` (new: strips an anchor
   `#…`/`^…` and any folder prefix, the same normalisation `resolve_link`
   applies before matching; a UDF persisted in an index is schema state, so
   it follows 033/037/039's "rebuild the owned index" discipline and gets
   the same `migrate_registers_required_functions_on_a_raw_connection`
   coverage, `db.rs:807`). Append to `MIGRATIONS` with description
   `"links: folded target indexes so a create heals only the rows that
   could name it (#1426)"`. Partial indexes keep them to dangling rows.
2. **The targeted heal.** `links_db::re_resolve_unresolved_links_naming(tx:
   &Transaction, created_path: &str) -> Result<Vec<String>, VaultError>`
   (new). Keys: the inverse of `probe` — for the path form,
   `tree_sort_key` of the created path, of the path minus each
   `MD_EXTENSIONS` suffix it carries, and of each rooted spelling
   (`/path`); for the name form, the same over the final component. Query
   the dangling rows by `slate_tree_sort_key(target_raw) IN (…)` OR
   `slate_tree_sort_key(slate_link_target_name(target_raw)) IN (…)` (two
   prepared statements, each served by its partial index). Resolve each
   row with `resolve_remembered` (`:235`) over `&FoldedNameLookup` on
   `tx`, writing `target_path` where it resolves and collecting affected
   sources as today. The fold is a superset of the selector's lowercase
   comparison, so no row the old heal would have fixed is missed; the
   selector still decides, so no extra row is wrongly fixed.
3. **The create** calls the new function at `session.rs:6207` with the
   bound path; nothing else about the create changes. The scan, rename and
   batch paths keep `re_resolve_unresolved_links_under` (their snapshot is
   #1429's decision and WB-18 does not touch it).
4. **Hosts.** No FFI change. Both hosts' caches migrate on first open;
   the Windows host's `VaultLifecycleViewModel` already surfaces
   `SchemaVersionSkew` as a refusal (the mac host too). The PR body names
   the change-feed clear (`db.rs:489-491`) as expected on first open after
   upgrade: the sessions re-register at the head, which WB-10's design
   (`21_change_feed_design.md` §4.2) already requires.

Deliberately not changed: the vault-wide heal for scans and moves; the
`links` primary key and existing indexes; no `A11yEvent`.

## Tests

| Fact (file) | Asserts | Mutation |
|---|---|---|
| `db.rs` tests: `migration_041_adds_the_folded_target_indexes` | after `migrate`, `sqlite_master` lists both indexes with the expected `sql`, and `user_version == 41` | drop one `CREATE` |
| `migrate_from_empty_lands_at_latest_version` (existing, `:760`) | updates its expected version | — |
| `a_later_migration_clears_every_cursor_and_row` (existing, change feed) | still green: 041 ≥ 40 clears the feed | — |
| `save_link_lookup.rs`: `a_create_heals_only_the_dangling_rows_that_could_name_it` | vault of N notes with M unrelated dangling links and K that name the new path (bare, with extension, rooted, folder path, a case and a Unicode variant): after the create, exactly K rows resolve, `snapshot_paths() == 0`, `PATH_ORDER_SNAPSHOT_PATHS == 0`, and a new counter `HEAL_ROWS_VISITED` (new, `links_db.rs` test seam) equals K | call the old heal; or widen the key set |
| `a_create_heals_exactly_what_the_vault_wide_heal_heals` | over `census_fixtures::INDEXED_PATHS` as the dangling targets (one source note each), create every path in turn in two vaults — one healed by the new function, one by `re_resolve_unresolved_links_under` — and compare `link_rows` after each step | drop the name-form keys |
| `a_create_whose_heal_lookup_fails_is_published_but_leaves_the_rows_dangling` | with `LOOKUP_FAULT`, the create reports `published` and the dangling rows are unchanged, no row written unresolved (the #1295 create rule, `:244`) | swallow the error |
| `link_pass.rs:72` `a_created_file_still_resolves_a_previously_unresolved_link` (existing) | stays green through the new path | — |
| `db.rs`: `the_folded_target_lookup_uses_its_partial_index` | `EXPLAIN QUERY PLAN` of each statement names `idx_links_target_fold` / `idx_links_target_name_fold` (as `the_whole_table_snapshot_reads_in_path_order` pins its plan) | drop the `WHERE` |

No census row; no FlaUI journey.

## Docs and registers

- `BENCHMARKS.md`: "Plan 44 WB-19" entry: create cost with 20,000
  unrelated dangling links before and after (the PR 20.0 entry at `:1233`
  measured the vault-wide heal at 6–22 ms on the Mac Studio; measure here
  on this machine and say which).
- `docs/plans/05_locked_architecture_decisions.md`: no edit (the rule is
  already written).
- PR body: `Closes #1426`; names migration 041, the change-feed clear and
  the schema-skew refusal older sessions see.

## Verification

```
$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb19'
cargo test -p slate-core --locked db::                   # migrations
cargo test -p slate-core --locked save_link_lookup
cargo test -p slate-core --locked link_pass
cargo test -p slate-core --locked --no-fail-fast
make ci
# both hosts open an existing cache once (upgrade path), on this machine:
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --filter "FullyQualifiedName~VaultLifecycle"
```

CI: `windows.yml` (rust-tests and the app build, which opens caches),
`rust.yml`, `swift-tests.yml` for the compile (no FFI delta, but the
cache the mac tests open migrates). AT pass: none.

## Acceptance

- [ ] Migration 041 and its two partial indexes; `user_version` 41.
- [ ] The seven facts pass, seen failing first where they are new.
- [ ] The create path calls the targeted heal; scans and moves unchanged.
- [ ] `BENCHMARKS.md` entry; `Closes #1426`.
- [ ] Not delivered: a targeted heal for the rename/move paths (they heal
      after a mapping, not a single path; out of #1426's scope — say so).

## Brief

Make a create heal only the dangling links that could name it. Read
`session.rs:6107-6225`, `links_db.rs:85-235` and `:455-510`,
`link_resolver.rs:505-600` (the probe to invert), `migrations/037…`,
`039…` and `004_links.sql`, `db.rs:60-80`, `:328-400`, `:455-535`. Add
migration 041 (two partial fold indexes and the `slate_link_target_name`
UDF), `re_resolve_unresolved_links_naming`, and switch the create to it;
leave the scan and move heals alone. Facts as in Tests (two row-counter
facts, one equivalence fact over the census fixtures, one fault fact, two
migration facts), failing first. Registers: `BENCHMARKS.md`. Done when
the suite and both lanes are green and an existing cache opens on both
hosts after the bump. Commit:
`perf(core): a create heals only the dangling links its path could resolve, through folded target indexes`.
