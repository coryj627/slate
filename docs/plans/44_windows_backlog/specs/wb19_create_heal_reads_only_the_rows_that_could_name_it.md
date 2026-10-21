# WB-19 — A create heals only the dangling links its path could resolve

Issues: #1426. Size M. Lanes: rust, windows (the Windows lane's
`cargo test -p slate-core` and the app build, which opens caches), swift
(compile only: no FFI change, but a schema bump migrates every cache the
mac tests open). AT: none. Wave: W7, slot 1. Depends on: WB-18 (shared
`links_db.rs` and the `session.rs` create path; §6 sequencing
"`links_db.rs`, `session.rs` link paths: WB-18, then WB-19"). Parallel
with: WB-20, WB-21. Decision: none.

## Goal

Creating a note stops reading every `files` row and every unresolved link
in the vault. The heal that makes a ghost link resolve the moment its file
appears (W6-2 PR B, #746) reads only the dangling rows whose authored
target could name the created path — found through two indexes on folded
forms of `links.target_raw`, one holding exact (rooted or qualified)
targets by their path and one holding bare-name targets by their name, so
finding them costs the rows, not a table scan, and a qualified link to
another folder's same-named file is never read — and resolves each
through the folded-name lookup, so the answer is
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
  remain, as the issue's comment says. The same function serves the scan
  (`session.rs:10558`, under the scan's token) and the rename and batch
  paths (`:13139`, `:13485`, `:14446`, `:17013`).
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
  computes it for a target with no source. The classification is
  `LinkCandidateSelector::new` (`:466-507`): trim; `None` for an empty or
  external target; a leading `/` or `./` is stripped and makes the target
  rooted; a rooted target or one whose remainder contains `/` is **exact**
  (it matches whole paths only, never a final component), anything else is
  a **basename**. So `missing/foo` can never resolve to `foo.md`; only a
  bare `foo` (or `foo.md`) can. A basename matches a path whose final
  component equals it, or equals it plus `.ext` for an `MD_EXTENSIONS`
  entry when the target has no extension (`basename_matches`,
  `:611-620`). `target_raw` is stored anchor-free (`links.rs:80-85`; the
  anchor is the `target_anchor` column), and the selector reads it exactly
  as stored — it strips no `#` or `^`.
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

1. **One classification, shared.** `link_resolver::selector_target(
   target_raw: &str) -> Option<SelectorTarget>` (new, `pub(crate)`) with
   `pub(crate) enum SelectorTarget { Exact(String), Basename(String) }`
   (new): the normalisation `LinkCandidateSelector::new` performs today
   (`:466-507` — trim; `None` for empty or external; strip a leading `./`
   or `/`; `Exact` when rooted or the remainder contains `/`, else
   `Basename`), returning the normalised target as authored (not
   lowercased, no extension added). `LinkCandidateSelector::new` is
   refactored to call it, so the index keys below and the selector cannot
   classify one target two ways. It strips no anchor: `target_raw` is
   stored anchor-free, and stripping `#`/`^` would key a Markdown target
   such as `a^b` differently from how the selector reads it.
2. **Migration 041** `migrations/041_links_target_fold_index.sql` (new),
   two partial expression indexes, each `DROP INDEX IF EXISTS` then
   `CREATE`, both `WHERE target_path IS NULL AND is_external = 0`:
   `idx_links_target_path_fold ON
   links(slate_tree_sort_key(slate_link_target_path(target_raw)))` and
   `idx_links_target_name_fold ON
   links(slate_tree_sort_key(slate_link_target_name(target_raw)))`. Two new
   deterministic UDFs in `register_connection_functions`, thin wrappers
   over `selector_target`: `slate_link_target_path(target_raw)` returns the
   `Exact` string and NULL otherwise; `slate_link_target_name(target_raw)`
   returns the `Basename` string and NULL otherwise (`slate_tree_sort_key`
   passes NULL through, `db.rs:349-356`, and a NULL key matches no `IN`
   list). So a qualified `missing/foo` row is in the path index only, under
   `missing/foo`, and never answers a name lookup for `foo`; a bare `foo`
   is in the name index only. Extension handling stays the selector's: the
   key is the target as authored, and the created path's keys (step 3)
   carry the implied-extension inverse. A UDF persisted in an index is
   schema state: both UDFs get
   `migrate_registers_required_functions_on_a_raw_connection` coverage
   (`db.rs:807`), and changing `selector_target`'s normalisation later
   needs a migration that rebuilds both indexes (the rule
   `db.rs:319-321` states for `slate_tree_sort_key`). Append to
   `MIGRATIONS` with description `"links: folded target indexes so a
   create heals only the rows that could name it (#1426)"`. Partial
   indexes keep them to dangling rows.
3. **The targeted heal.** `links_db::re_resolve_unresolved_links_naming(tx:
   &Transaction, created_path: &str) -> Result<Vec<String>, VaultError>`
   (new). Keys, the inverse of the selector's two modes for the created
   path `P` (no rooted spellings: the UDF already strips `/` and `./`):
   - **path keys**, matched against `idx_links_target_path_fold`:
     `tree_sort_key(P)`, and, when `P`'s final component ends in `.ext`
     for an `MD_EXTENSIONS` entry (ASCII case-insensitive),
     `tree_sort_key` of `P` without that suffix — the inverse of
     `Exact { keys }` (the literal, then the literal plus each implied
     extension);
   - **name keys**, matched against `idx_links_target_name_fold`: the same
     two over `P`'s final component — the inverse of `basename_matches`.

   Two prepared statements, each repeating its index's partial predicate
   so the planner can use it: `SELECT links.rowid, files.path,
   links.target_raw, links.target_anchor FROM links JOIN files ON files.id
   = links.source_file_id WHERE links.target_path IS NULL AND
   links.is_external = 0 AND
   slate_tree_sort_key(slate_link_target_path(links.target_raw)) IN (…)`,
   and the same with `slate_link_target_name`. A row answers at most one
   statement (its target is exact or basename, never both). Resolve each
   row with `resolve_remembered` (`:235`) over `&FoldedNameLookup` on
   `tx`, its stored anchor passed through unchanged, writing `target_path`
   where it resolves and collecting affected sources as today;
   `HEAL_ROWS_VISITED` (new, `#[cfg(test)]` thread-local in `links_db.rs`)
   counts the rows the two statements return. Every error propagates with
   `?` — a failed statement or a row whose `resolve_remembered` errs is
   never skipped and never treated as unresolved (`LinkCandidates`'s
   contract, `links_db.rs:127-129`) — so the create maps it through
   `published` (`session.rs:6207-6208`), reports `PublishedUnindexed`, and
   its transaction rolls back with every heal `UPDATE` in it. Within each mode the fold
   (NFC then lowercase, `db.rs:393-396`) is a superset of the selector's
   lowercase comparison, so no row the old heal would have fixed is
   missed; the selector still decides, so no extra row is wrongly fixed;
   and no row of the other mode is read at all.
4. **The create** calls the new function at `session.rs:6207` with the
   bound path; nothing else about the create changes. The scan, rename and
   batch paths keep `re_resolve_unresolved_links_under` (their snapshot is
   #1429's decision and WB-18 does not touch it).
5. **Hosts.** No FFI change. Both hosts' caches migrate on first open;
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
| `db.rs` tests: `migration_041_adds_the_folded_target_indexes` | after `migrate`, `sqlite_master` lists `idx_links_target_path_fold` and `idx_links_target_name_fold` with the expected `sql`, and `user_version == 41` | drop one `CREATE` |
| `migrate_from_empty_lands_at_latest_version` (existing, `db.rs:760`) | updates its expected version | — |
| `migrate_registers_required_functions_on_a_raw_connection` (existing, `db.rs:807`) | extended: `slate_link_target_path` and `slate_link_target_name` are registered on a raw connection | drop one registration |
| `a_later_migration_clears_every_cursor_and_row` (existing, `db.rs:1139`) | still green: 041 ≥ 40 clears the feed | — |
| `db.rs`: `the_target_key_functions_classify_every_target_as_the_selector_does` (new) | for every `census_fixtures::TARGETS` entry plus `missing/foo`, `/missing/foo.md`, `./foo.md`, ` foo `, `a^b` and an external URL, evaluated through SQL on a raw migrated connection: both keys are NULL iff `candidate_probe(target)` is `None`; otherwise exactly one is non-NULL — the path key iff the probe is `Paths` — and `tree_sort_key` of it equals `tree_sort_key` of the probe's first key | make `slate_link_target_name` return the final component of every target (non-NULL for `missing/foo`), or strip a `^…` suffix |
| `save_link_lookup.rs`: `a_create_heals_only_the_dangling_rows_that_could_name_it` | created path `notes/föo.md` (NFC); K dangling rows that name it — bare `föo`, `föo.md`, the Unicode case variant `FÖO`, rooted `/notes/föo`, `./notes/föo.md`, qualified `notes/föo` and `Notes/Föo.md`; M unrelated dangling basename links (`bar_1` … `bar_M`); and Q = 200 dangling **qualified and rooted** targets that share the created file's final component but cannot name it (`missing_1/föo` … `missing_100/föo`, `/missing_101/föo.md` … `/missing_200/föo.md`). After the create: exactly K rows resolve, all Q stay dangling, `snapshot_paths() == 0`, `PATH_ORDER_SNAPSHOT_PATHS == 0`, and `HEAL_ROWS_VISITED == K` (not K + Q) | call the old heal; or make `slate_link_target_name` strip folder prefixes from every target (the round-5 design), which reads K + Q rows |
| `a_create_heals_exactly_what_the_vault_wide_heal_heals` | with every `census_fixtures::TARGETS` entry as a dangling link (one source note each, so both modes and their shared final components are present), create every `census_fixtures::INDEXED_PATHS` path in turn in two vaults — one healed by the new function, one by `re_resolve_unresolved_links_under` — and compare `link_rows` after each step | drop the name-form keys; or drop the path-form extension inverse |
| `a_create_whose_heal_fails_is_published_unindexed_and_commits_nothing` | a fixture whose own content indexes and whose heal fails: three notes each holding a dangling `[[fresh]]`, and the create `create_exclusive_reporting("fresh.md", "# fresh\n")` — a link-free body, so the create's own resolution makes no lookup and `LOOKUP_FAULT` (`links_db.rs:98`, which fails `FoldedNameLookup::offer_to` at `:211-213`) can fire only in the heal. Control first, in a second vault built the same way without the fault: the outcome is `Committed` and the three rows resolve to `fresh.md` (so the heal reaches them). Then, with the fault set and cleared after the call: the outcome matches `Ok(CreateExclusiveOutcome::PublishedUnindexed { path, content_hash, .. })` with `path == "fresh.md"` and `content_hash == crate::vault::content_hash(b"# fresh\n")` (anything else panics, as main's `a_failed_lookup_leaves_a_create_published_but_unindexed`, `save_link_lookup.rs:243-270`, does); the file on disk holds exactly `# fresh\n`; `SELECT COUNT(*) FROM files WHERE path = 'fresh.md'` is 0; and the three rows' `target_path` are still NULL (the create's transaction, its own `files` and `links` rows and any heal `UPDATE`, rolled back — #1295's create rule, `links_db.rs:127-129`: a link whose candidates could not be read is never dropped and never written unresolved) | swallow the heal's error at either level — `re_resolve_unresolved_links_naming(&tx, path).unwrap_or_default()` at the call site, or skipping a row whose `resolve_remembered` errs inside the heal: the create then commits as `Committed` with a `files` row, and the fact fails |
| `link_pass.rs:72` `a_created_file_still_resolves_a_previously_unresolved_link` (existing) | stays green through the new path | — |
| `db.rs`: `the_folded_target_lookup_uses_its_partial_index` | `EXPLAIN QUERY PLAN` of each statement names `idx_links_target_path_fold` / `idx_links_target_name_fold` (as `links_db.rs:991` `the_whole_table_snapshot_reads_in_path_order` pins its plan) | drop the partial predicate from one statement's `WHERE` |

The three `save_link_lookup.rs` facts sit in
`session/tests/save_link_lookup.rs` beside the #1295 facts, whose
`#[cfg(test)]` counters they read. No census row; no FlaUI journey.

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

PowerShell, repo root. Each filter is listed first and the block throws
unless every required fact is discovered under it; every command's
`$LASTEXITCODE` is checked (no output grepping for "Failed").

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb19'
function Assert-Ok([string]$step) { if ($LASTEXITCODE -ne 0) { throw "$step failed ($LASTEXITCODE)" } }
function Assert-Facts([string]$filter, [string[]]$facts) {
    $listed = cargo test -p slate-core --locked $filter -- --list
    Assert-Ok "listing $filter"
    foreach ($fact in $facts) {
        if (-not ($listed -match "(^|::)$([regex]::Escape($fact)): test$")) { throw "filter '$filter' does not select $fact" }
    }
}
$required = [ordered]@{
    'db::'             = @('migration_041_adds_the_folded_target_indexes', 'migrate_from_empty_lands_at_latest_version', 'migrate_registers_required_functions_on_a_raw_connection', 'a_later_migration_clears_every_cursor_and_row', 'the_target_key_functions_classify_every_target_as_the_selector_does', 'the_folded_target_lookup_uses_its_partial_index')
    'save_link_lookup' = @('a_create_heals_only_the_dangling_rows_that_could_name_it', 'a_create_heals_exactly_what_the_vault_wide_heal_heals', 'a_create_whose_heal_fails_is_published_unindexed_and_commits_nothing')
    'link_pass'        = @('a_created_file_still_resolves_a_previously_unresolved_link')
}
foreach ($filter in $required.Keys) {
    Assert-Facts $filter $required[$filter]
    cargo test -p slate-core --locked $filter; Assert-Ok "cargo test $filter"
}
cargo test -p slate-core --locked --no-fail-fast; Assert-Ok 'the whole core suite'
cargo fmt --all -- --check; Assert-Ok 'fmt'
cargo clippy --all-targets --workspace -- -D warnings; Assert-Ok 'clippy'
cargo test --workspace --locked; Assert-Ok 'workspace tests'
cargo bench --workspace --no-run; Assert-Ok 'bench compile'
bash scripts/check-license-headers.sh; Assert-Ok 'licence headers'
# the Windows host opens an existing (version-40) cache once, the upgrade path:
./apps/slate-windows/generate-bindings.ps1 -Locked; Assert-Ok 'bindings'
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release; Assert-Ok 'build'
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
$filter = 'FullyQualifiedName~SlateWindows.Tests.RescanTests'
$n = (dotnet test $tests --no-build --configuration Release --list-tests --filter $filter | Select-String '^\s{4}\S').Count
Assert-Ok 'listing RescanTests'
if ($n -lt 1) { throw "filter selects nothing: $filter" }
dotnet test $tests --no-build --configuration Release --filter $filter; Assert-Ok 'RescanTests'
```

No C# file changes, so no `dotnet format` step. Mutation run: each row's
mutation applied locally, the fact seen failing, reverted.

CI: `windows.yml` (rust-tests and the app build, which opens caches),
`rust.yml`, `swift-tests.yml` for the compile. AT pass: none.

## Acceptance

- [ ] `selector_target` is the one classification: `LinkCandidateSelector::new`
      calls it, and the two UDFs wrap it.
- [ ] Migration 041 and its two partial indexes
      (`idx_links_target_path_fold`, `idx_links_target_name_fold`);
      `user_version` 41.
- [ ] The ten facts pass (the six new ones seen failing first, the
      extended `migrate_registers_required_functions_on_a_raw_connection`
      seen failing with a registration dropped), and every filter's
      discovery check passed.
- [ ] A create beside Q qualified and rooted dangling targets that share
      its final component reads K rows, not K + Q.
- [ ] The create path calls the targeted heal; scans and moves unchanged.
- [ ] A heal that fails makes the create `PublishedUnindexed` with nothing
      committed; swallowing the heal's error was run as a mutation and
      failed `a_create_whose_heal_fails_is_published_unindexed_and_commits_nothing`.
- [ ] `BENCHMARKS.md` entry; `Closes #1426`.
- [ ] Not delivered: a targeted heal for the rename/move paths (they heal
      after a mapping, not a single path; out of #1426's scope — say so).

## Brief

Make a create heal only the dangling links that could name it. No owner
decision is pending. Read `session.rs:6107-6225` (the create and its heal
call at `:6207`), `links_db.rs:85-235` and `:455-510`,
`link_resolver.rs:466-600` (the selector's classification at `:466-507`:
a rooted `/`/`./` target or one containing `/` is exact and matches whole
paths only; anything else is a basename; `basename_matches` `:611-620`),
`links.rs:80-85` (`target_raw` is stored anchor-free),
`migrations/037_path_fold_indexes.sql`, `039_file_name_fold_index.sql`,
`004_links.sql`, `db.rs:60-80`, `:310-400`, `:455-535`,
`change_feed.rs:75`. Build:
- `link_resolver::selector_target(target_raw) -> Option<SelectorTarget>`
  and `enum SelectorTarget { Exact(String), Basename(String) }` (both new,
  `pub(crate)`): the selector's own normalisation (trim; `None` for empty
  or external; strip `./` or `/`; exact when rooted or containing `/`),
  no anchor stripping; `LinkCandidateSelector::new` refactored to call it.
- Migration `041_links_target_fold_index.sql`: partial indexes
  `idx_links_target_path_fold` on
  `slate_tree_sort_key(slate_link_target_path(target_raw))` and
  `idx_links_target_name_fold` on
  `slate_tree_sort_key(slate_link_target_name(target_raw))`, both `WHERE
  target_path IS NULL AND is_external = 0`; the two new deterministic UDFs
  in `register_connection_functions` (`slate_link_target_path` returns the
  exact target, else NULL; `slate_link_target_name` the basename target,
  else NULL); the `MIGRATIONS` entry.
- `links_db::re_resolve_unresolved_links_naming(tx: &Transaction,
  created_path: &str) -> Result<Vec<String>, VaultError>` (new): path
  keys `tree_sort_key(P)` and `P` minus a trailing `MD_EXTENSIONS` suffix
  against the path index; the same two over `P`'s final component against
  the name index; two statements each repeating its index's predicate;
  each row resolved with `resolve_remembered` over `FoldedNameLookup`, its
  anchor passed through; every error propagated with `?` (never a skipped
  row, never a row left as if unresolved), so the create's existing
  `map_err(&published)` at `session.rs:6207-6208` turns it into
  `PublishedUnindexed` and rolls the transaction back; the
  `HEAL_ROWS_VISITED` test counter (new).
- The create switched to it; the scan and move heals left alone.

Facts (mutation in brackets), new ones seen failing first:
- `migration_041_adds_the_folded_target_indexes`: both indexes' `sql` in
  `sqlite_master` and `user_version == 41` [drop one `CREATE`].
- `migrate_from_empty_lands_at_latest_version` (updated to 41) and
  `a_later_migration_clears_every_cursor_and_row` (still green: 041 clears
  the feed).
- `migrate_registers_required_functions_on_a_raw_connection` (extended):
  both new UDFs registered [drop one registration].
- `the_target_key_functions_classify_every_target_as_the_selector_does`:
  over `census_fixtures::TARGETS` plus `missing/foo`, `/missing/foo.md`,
  `./foo.md`, ` foo `, `a^b` and a URL, exactly one key is non-NULL when
  the probe is not `None` — the path key iff the probe is `Paths` — and it
  folds to the probe's first key [non-NULL name key for `missing/foo`, or
  strip `^…`].
- `a_create_heals_only_the_dangling_rows_that_could_name_it`: creating
  `notes/föo.md` beside K naming rows (bare, `.md`, `FÖO`, rooted,
  `./`, qualified, case-varied qualified), M unrelated basenames and Q =
  200 qualified/rooted `missing_i/föo` targets: exactly K resolve, all Q
  stay dangling, `snapshot_paths() == 0`,
  `PATH_ORDER_SNAPSHOT_PATHS == 0`, `HEAL_ROWS_VISITED == K` [the old
  heal; or a name UDF that strips every folder prefix, which reads K + Q].
- `a_create_heals_exactly_what_the_vault_wide_heal_heals`: every
  `census_fixtures::TARGETS` entry dangling, every `INDEXED_PATHS` path
  created in turn in two vaults, `link_rows` equal after each step [drop
  the name-form keys, or the path-form extension inverse].
- `a_create_whose_heal_fails_is_published_unindexed_and_commits_nothing`:
  a fixture whose own content indexes and whose heal fails — three notes
  each holding a dangling `[[fresh]]`, then
  `create_exclusive_reporting("fresh.md", "# fresh\n")`, a link-free body,
  so `LOOKUP_FAULT` (`links_db.rs:98`, failing `FoldedNameLookup::offer_to`
  at `:211-213`) can fire only in the heal. A control vault without the
  fault returns `Committed` and the three rows resolve. With the fault the
  outcome must be `Ok(CreateExclusiveOutcome::PublishedUnindexed { path,
  content_hash, .. })` with `path == "fresh.md"` and `content_hash ==
  crate::vault::content_hash(b"# fresh\n")` (anything else panics, as
  main's `a_failed_lookup_leaves_a_create_published_but_unindexed`,
  `save_link_lookup.rs:243-270`, does), the file on disk holds exactly
  `# fresh\n`, `files` has no `fresh.md` row, and the three rows'
  `target_path` are still NULL (the whole transaction rolled back). The
  heal therefore propagates every error with `?`, and the create maps it
  through `published` at `session.rs:6207-6208`. Swallowing the heal's
  error must fail this fact [`.unwrap_or_default()` on the heal at the
  call site, or skipping a row whose `resolve_remembered` errs: either
  commits the create as `Committed` with a `files` row].
- `a_created_file_still_resolves_a_previously_unresolved_link`
  (existing, `link_pass.rs:72`): green through the new path.
- `the_folded_target_lookup_uses_its_partial_index`: `EXPLAIN QUERY
  PLAN` names each index [drop one statement's partial predicate].

Registers: `BENCHMARKS.md` (a WB-19 entry with the create cost before and
after, 20,000 unrelated dangling links, measured on this machine). Not
delivered: a targeted heal for the rename and move paths. Done when the
Verification block runs clean in PowerShell — every filter's discovery
check finds its required facts and every command's `$LASTEXITCODE` is 0
(the three filters, the whole core suite, fmt, clippy, workspace tests,
bench compile, licence headers, the bindings, the Windows build and
`RescanTests` opening a version-40 cache) — the windows, rust and swift
lanes are green, each mutation was run locally, and `Closes #1426` is in
the PR body naming migration 041, the change-feed clear and the
schema-skew refusal. Commit:
`perf(core): a create heals only the dangling links its path could resolve, through folded target indexes`.
