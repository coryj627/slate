# WB-18 — Every link resolution reads its own candidates, and a panicking fault fact poisons nothing

Issues: #1481 #1427 #1485 #1486. Size M. Lanes: rust (the Windows lane's
`cargo test -p slate-core` and `rust.yml`). AT: none. Wave: W6, slot 3.
Depends on: nothing (WB-19 depends on this: shared `links_db.rs` and the
`session.rs` link paths, §6 sequencing table). Parallel with: WB-16, WB-17.
Decision: none.

## Goal

Four core facts of locked decision 05 §4 (the memory bound) and §9.3.1
come true: the index repair for one path, rename planning, and a Bases or
DQL query that resolves a link each read only the candidate rows their own
targets name, never a snapshot of every `files` row — the rule #1295 gave
saves and creates and #1429 gave scans. A Bases lookup that fails is the
query's error, not an unresolved link. And a fact that panics while holding
`ENV_FAULT_GUARD` fails alone: the guard cannot be poisoned, and the
fault-seam variables it protects are cleared when the holder unwinds, so
one real failure never reads as twenty-four.

## Current state on main

Verified against the worktree (the plan's lines hold; the issue's count of
guard sites is one short).

- **#1481.** `session/tests/common.rs:24` `pub(super) static ENV_FAULT_GUARD:
  std::sync::Mutex<()>`. 42 sites take it with `.lock().unwrap()` (the
  issue counted 41; `change_feed.rs:1336` was added since): `tasks.rs` 30,
  `structural_batch.rs` 5 (`:3830`, `:3901`, `:3956`, `:4043`, `:4098`;
  `:3235` is a comment), `structural_identity.rs` 4, `canvas.rs:1370`,
  `canonical_identity.rs` 1, `change_feed.rs:1336`. The variables set under
  it are `SLATE_TEST_FAULT_{AFTER_WRITE, PRE_WRITE, REINDEX, REINDEX_DB,
  INTENT_CLEAR, PLANT_MARKERS, RECOVERY_FINALIZE}` plus
  `SLATE_TEST_INTENT_ABANDON_ZERO_FOR`, `SLATE_TEST_COMMIT_AFTER_INTENT`,
  `SLATE_TEST_SWEEP_AFTER_INTENT`, `SLATE_TEST_RESOLVE_BEFORE_QUARANTINE`,
  `SLATE_TEST_STALL_ORPHAN_REPAIR`, `SLATE_TEST_TORN_FILE_TASKS`,
  `SLATE_TEST_TORN_NOTE_READ` (every `set_var("SLATE_TEST_…")` in
  `session/tests`). Each site pairs `set_var` with a `remove_var` that an
  unwinding panic skips (`tasks.rs:198-200`, `:294-296`, `:345-351`).
- **#1427.** `session.rs:8258` `reindex_path_locked`: `:8306-8308` reads
  `links_db::indexed_paths_in_path_order(&tx)`, pushes `path` and builds an
  `InMemoryVaultIndex`, handed to `index_file` (`:10803-10815`, whose
  `links: &dyn links_db::LinkCandidates` parameter is already the
  abstraction). `ensure_open_base_indexed` (`:18729-18770`) is the model:
  `let links = crate::links_db::FoldedNameLookup;` at `:18749` with the
  comment "`index_file` upserts this path's row before resolving any link
  of it, so the lookup sees the row in this transaction". The save path
  does the same at `:5129`.
- **#1485.** `link_rewrite.rs:86-99` `PreMoveIndex { post, mapping }`
  implements only `VaultIndex::all_paths` (`:92-98`, mapping each post
  path back through `MoveMapping::old_path_of`), so it takes
  `VaultIndex::offer_candidates`'s default (`link_resolver.rs:67-71`:
  every path, in index order). `InMemoryVaultIndex::offer_candidates`
  (`:181-185`) looks candidates up in a `CandidateTable`.
  `plan_rewrites_for_source` (`:118`) builds the `PreMoveIndex` at
  `:126-129`.
- **#1486.** `bases/engine.rs:3564-3575` `SqlVaultLookup` holds
  `link_index: RefCell<Option<InMemoryVaultIndex>>` and a
  `link_resolutions` memo keyed `(source_path, target)`;
  `resolve_link_from` (`:3638-3659`) reads the snapshot with
  `indexed_paths_in_path_order(self.conn).ok()?` at `:3646` (the comment
  names #1486) and resolves with `crate::resolve_link(…, index.as_ref()?)`.
  `VaultLookup::resolve_link` (`bases/eval.rs:341-344`) returns
  `Option<String>` with a defaulted `None` body. Callers: `eval.rs:1214`
  (the `link()` function), `:2251` (DQL inline link fields), `:4791`
  (`dql_link_page_day`), `engine.rs:538` (an extensionless `Linked`
  source). `EvalError` (`eval.rs:481-500`) has no lookup-failure variant;
  `exists_query` (`engine.rs:3581-3596`) maps a SQLite error to
  `EvalError::InvalidArgument { function, message }`. The only other
  implementor is `tests/bases_eval.rs:1226` `impl VaultLookup for TestVault`.
- **The counters and seams the facts reuse.** `links_db.rs:88-114`:
  `RESOLUTION_ROWS_VISITED`, `LOOKUP_FAULT`, `SNAPSHOT_FAULT`,
  `PATH_ORDER_SNAPSHOT_PATHS`; `link_resolver.rs:161-173`:
  `SNAPSHOT_PATHS`, `PATHS_OFFERED`, `CANDIDATE_TABLES_BUILT`.
  `link_resolver::census_fixtures::INDEXED_PATHS` (`:670-700`). Existing
  facts of this shape: `session/tests/save_link_lookup.rs:110`
  `a_save_resolves_its_links_from_its_own_targets_rows_only`, `:160`
  `an_open_base_repair_reads_no_snapshot_of_the_vault`, `:213`
  `a_failed_lookup_fails_the_save_and_drops_no_link`, `:417`
  `the_save_lookup_resolves_exactly_as_the_whole_index_does`;
  `session/tests/link_pass.rs:268`
  `every_resolution_snapshot_is_read_through_the_path_order_helper`.
- **A naming correction to the reports:** there is no
  `LookupLinkResolver` in the tree. The lookup type is
  `links_db::FoldedNameLookup` (`:200`), implementing
  `links_db::LinkCandidates` (`:122-135`), whose `offer_to` reads
  `candidate_rows_sql` by `slate_tree_sort_key` through
  `idx_files_path_fold` / `idx_files_name_fold`.

## Reference

Core only; no mac or Windows twin. The behavioural spec is the issues'
text plus `LinkCandidates`'s doc contract (`links_db.rs:122-130`): offer
at least every path the probe names, in path order among lowercase-equal
paths, and "a link whose candidates could not be read is never dropped,
and never written unresolved". Locked decision 05 §4 and §9.3.1
(`docs/plans/05_locked_architecture_decisions.md:1556`: every list-like
operation is paged or streamed). The #1295 row-counter fact shape is the
template for every new fact here.

## Design

Four commits, in this order (each a green tree):

1. **The guard (#1481).** `common.rs` gains `pub(super) struct FaultGuard`
   (new) holding the `MutexGuard<'static, ()>` and a `Vec<&'static str>` of
   variable names, with `ENV_FAULT_GUARD.lock()` taken as
   `.unwrap_or_else(PoisonError::into_inner)`. `pub(super) fn fault_guard()
   -> FaultGuard` (new) acquires it and, on acquisition, removes every
   variable in a `FAULT_VARIABLES: &[&str]` list (the fifteen names above),
   so a stale trigger from an unwound holder is cleared before the next
   fact runs. `FaultGuard::set(&mut self, name: &'static str, value: &str)`
   records the name and sets it; `Drop` removes every recorded variable
   (unwinding runs it). All 42 `ENV_FAULT_GUARD.lock().unwrap()` sites
   become `let _env = fault_guard();` and their paired `set_var` /
   `remove_var` become `_env.set(…)` (the explicit `remove_var` lines go,
   except where a fact clears mid-body to prove the fault is off, which
   stays). The static stays `pub(super)` so an unmigrated `.lock().unwrap()`
   is a compile-visible leftover; a census fact greps the tests tree for
   `ENV_FAULT_GUARD.lock()` outside `common.rs` and expects zero.
2. **The repair (#1427).** `reindex_path_locked`: delete `:8306-8308`, pass
   `&crate::links_db::FoldedNameLookup` to `index_file` as
   `ensure_open_base_indexed` does. The `UPDATE files SET mtime_ms = -1 …`
   at `:8299-8302` already guarantees the row exists before `index_file`
   upserts it; where the row is absent (a repair of a path never indexed),
   `index_file`'s own upsert precedes link resolution, so the lookup sees
   it. Nothing else changes; the deletion-convergence arm (`:8319-8338`) is
   untouched.
3. **Rename planning (#1485).** `impl VaultIndex for PreMoveIndex` gains
   `fn offer_candidates(&self, selector: &mut LinkCandidateSelector)`: run
   the probe against `self.post` through a forwarding selector that maps
   each offered post path with `self.mapping.old_path_of(path)` and offers
   only paths the mapping did not change (so the post-move candidates whose
   pre-move name is the same); then, for each `(old, new)` in
   `self.mapping` (`MoveMapping::forward`), offer `old` if the selector's
   probe keys name it (an exact key equal to `old.to_lowercase()`, or a
   basename key matching its final component under `basename_matches`),
   in the post index's order of `new`. The winner must equal what
   `all_paths()` gives today: a fact proves it over the census fixtures
   for every mapping shape (rename, move, folder move, case-only). The
   default `all_paths` stays for the planner's other uses. New helper on
   `LinkCandidateSelector` if needed: `pub fn probe_names(&self, path: &str)
   -> bool` (new) — whether this path could match the probe — so the
   mapping walk does not duplicate the matching rules.
4. **Bases and DQL (#1486).** `VaultLookup::resolve_link` becomes
   `fn resolve_link(&self, target: &str) -> Result<Option<String>, EvalError>`
   (default `Ok(None)`); `EvalError` gains `LinkLookupFailed { message:
   String }` (new; `#[error("link lookup failed: {message}")]`). The four
   callers propagate with `?` (`eval.rs:1214`, `:2251`, `:4791`,
   `engine.rs:538`; at `:4791` the `.or_else(|| link.resolved_path.clone())`
   applies to the `Ok` value). `SqlVaultLookup` drops `link_index`; its
   `resolve_link_from` keeps the `link_resolutions` memo and resolves
   through `crate::links_db::resolve_remembered`-style code over
   `FoldedNameLookup::offer_to(self.conn, &mut selector)` on the query's
   connection, with a `?` from `VaultError` into
   `EvalError::LinkLookupFailed { message: error.to_string() }`. The query
   result then carries the error the way other `EvalError`s already reach
   the host (no new FFI; the Bases failed state and DQL error rows already
   render `EvalError` text). `tests/bases_eval.rs:1226` updates its
   signature.

Deliberately not changed: `re_resolve_unresolved_links_under`'s snapshot
(WB-19 owns it); the scan's and rename's `InMemoryVaultIndex` snapshots
(#1429 decided they keep theirs); `collect_rewrite_candidates`'s
whole-`links` read (the issue excludes it); no FFI.

## Tests

Rust facts, snake_case, in the files named. Each fails first on `main`'s
code (the mutation column says how it fails again if the fix regresses).

| Fact | Asserts | Mutation that fails it |
|---|---|---|
| `session/tests/common.rs` → new `fault_guard.rs`: `a_panic_under_the_fault_guard_poisons_nothing_for_the_next_fact` | a thread takes `fault_guard()`, sets `SLATE_TEST_FAULT_AFTER_WRITE`, panics; the next `fault_guard()` succeeds and the variable is unset | restore `.unwrap()`, or drop the `Drop` impl |
| `the_fault_guard_clears_every_fault_variable_on_acquisition` | set each `FAULT_VARIABLES` name by hand, acquire, all absent | remove a name from the list |
| `no_test_takes_the_fault_mutex_directly` (census) | zero `ENV_FAULT_GUARD.lock()` matches outside `common.rs` | add one back |
| `save_link_lookup.rs`: `a_repair_through_reindex_path_reads_no_snapshot_of_the_vault` | after `reindex_path` on a note with the census targets, `snapshot_paths() == 0`, `PATH_ORDER_SNAPSHOT_PATHS == 0`, `resolution_rows_visited()` ≤ the note's own candidate rows | restore `:8306-8308` |
| `a_repair_writes_the_links_a_scan_writes` | `link_rows` after repair equal `link_rows` after a full scan over `census_vault` | offer a wrong key set |
| `link_rewrite.rs` tests: `the_pre_move_index_offers_the_same_winner_as_every_path` | for each census mapping shape and every `INDEXED_PATHS` target, `offer_candidates` and the `all_paths` default pick one winner | drop the moved-paths arm |
| `session/tests/structural_batch.rs` (or a new `rename_planning.rs`): `a_rename_plans_its_rewrites_from_candidate_rows_only` | row counter: `PATHS_OFFERED` after renaming a note linked by N sources ≤ sources × their distinct targets' candidates, not × files | restore the default |
| `tests/bases_eval.rs` + `session/tests/bases.rs`: `a_query_that_resolves_links_materializes_no_snapshot` | a Bases query with `link()` over a 200-note vault: `snapshot_paths() == 0` and `CANDIDATE_TABLES_BUILT == 0` | restore `link_index` |
| `a_failed_link_lookup_fails_the_query_instead_of_reading_as_unresolved` | with `LOOKUP_FAULT` set, the query returns `Err(EvalError::LinkLookupFailed)`, no row shows an unresolved link | restore `.ok()?` |
| `a_dql_inline_link_field_resolves_through_the_lookup` | the `:2251` arm resolves a basename target to the census answer | break the probe mapping |

No FlaUI journey; no census row beyond the fault-guard census.

## Docs and registers

- `BENCHMARKS.md`: a short "Plan 44 WB-18" entry under the PR 20.0 one
  (`:1233`) with the row counts before and after for the repair and the
  rename plan (numbers, not claims).
- Issues: each of the four closes with `Closes #N` in one PR body (all four
  close here per §5's matrix: "53 close in the one chunk that lists them").
- No contract row (core-internal); no `w_c_matrix.md` / `parity_matrix.md`
  change; no designations.

## Verification

```
$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb18'
cargo test -p slate-core --locked fault_guard
cargo test -p slate-core --locked save_link_lookup
cargo test -p slate-core --locked link_rewrite
cargo test -p slate-core --locked bases
cargo test -p slate-core --locked --no-fail-fast          # the whole suite, once
cargo test -p slate-core --locked -- --skip census_ --skip perf_guard_   # what the Windows lane runs (windows.yml:337-341)
make ci                                                   # fmt, clippy, bench-check, licence headers (Makefile:74)
```

CI: `windows.yml` rust-tests (always on) plus `rust.yml` (Linux proves
`cfg(unix)` and runs the two perf guards isolated, `rust.yml:269-290`).
AT pass: none — no speech changes.

## Acceptance

- [ ] The ten facts above exist, were seen failing, and pass.
- [ ] `ENV_FAULT_GUARD.lock()` appears only in `common.rs`.
- [ ] `reindex_path_locked`, `PreMoveIndex` and `SqlVaultLookup` read no
      `files` snapshot (the counters prove it).
- [ ] `VaultLookup::resolve_link` returns `Result`; the mac Swift lane is
      unaffected (no FFI change; confirm `swift-tests.yml` is not triggered
      or is green).
- [ ] `BENCHMARKS.md` entry; PR body `Closes #1481`, `Closes #1427`,
      `Closes #1485`, `Closes #1486`.
- [ ] Not delivered: the whole-`links` read in `collect_rewrite_candidates`
      (named in #1485 as out of scope; file a follow-up if the row counter
      shows it dominates).

## Brief

Make every remaining link resolution in core read its own candidate rows,
and make the test fault guard poison-proof. Read `links_db.rs:85-235`
(counters, `LinkCandidates`, `FoldedNameLookup`), `session.rs:8258-8345`
(`reindex_path_locked`) beside `:18729-18770` (the model),
`link_rewrite.rs:60-130` and `link_resolver.rs:50-75`, `:505-600`
(`PreMoveIndex`, `VaultIndex`, the probe), `bases/engine.rs:3564-3672` and
`bases/eval.rs:336-352`, `:481-500`, and `session/tests/common.rs:1-40`
with `save_link_lookup.rs:93-135`. Build the `FaultGuard` first and migrate
the 42 sites; then the three resolution changes in the order above, each
with its row-counter and whole-index-equivalence facts (names in Tests),
seen failing first. Registers: a `BENCHMARKS.md` entry. Done when the
suite passes locally and on the Windows and rust lanes, the counters read
zero snapshots, and the four issues close. Commit:
`fix(core): every link resolution reads its own candidate rows and a panicking fault fact poisons no guard`.
