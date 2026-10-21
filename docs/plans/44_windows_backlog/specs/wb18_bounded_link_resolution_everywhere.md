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
the fault guard fails alone: the guard cannot be poisoned, and the
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
  `canonical_identity.rs` 1, `change_feed.rs:1336`. The fifteen variables
  set under it are `SLATE_TEST_FAULT_{AFTER_WRITE, PRE_WRITE, REINDEX,
  REINDEX_DB, INTENT_CLEAR, PLANT_MARKERS, RECOVERY_FINALIZE}` plus
  `SLATE_TEST_INTENT_ABANDON_ZERO_FOR`, `SLATE_TEST_COMMIT_AFTER_INTENT`,
  `SLATE_TEST_SWEEP_AFTER_INTENT`, `SLATE_TEST_RESOLVE_BEFORE_QUARANTINE`,
  `SLATE_TEST_SECOND_WRITER_BEFORE_QUARANTINE` (its call is split across
  `tasks.rs:938-939`, so a one-line `set_var("SLATE_TEST_` grep misses it),
  `SLATE_TEST_STALL_ORPHAN_REPAIR`, `SLATE_TEST_TORN_FILE_TASKS`,
  `SLATE_TEST_TORN_NOTE_READ` — every multi-line match of
  `set_var\(\s*"SLATE_TEST_` in `session/tests` except
  `SLATE_TEST_DISABLE_VAULT_WATCHER`, which `watch.rs:1150-1163`
  `the_test_switch_keeps_the_watcher_off` sets under its own
  `WATCHER_SWITCH` write lock (not a fault seam; it stays as it is). Every
  fault variable is a path-substring trigger (`session.rs:2861`, `:4242`,
  `:4371`, `:4386`, `:4510`, `:4626`, `:6168`, `:7689`, `:7908`, `:8026`,
  `:8121`, `:8137`, `:8268`, `:8283`; `tasks_db.rs:110`, `:172`), so a
  value no fixture path contains fires nothing. Each setting site pairs
  `set_var` with a `remove_var` that an unwinding panic skips
  (`tasks.rs:198-200`, `:294-296`, `:345-351`). Ten sites take the guard
  for exclusion only and set nothing: `structural_identity.rs:118`, `:143`, `:179`, `:203`,
  `structural_batch.rs:4098`, `tasks.rs:1660`, `:1833`, `:1917`, `:2079`,
  `:2226`.
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
  path back through `MoveMapping::old_path_of`, **preserving the post
  index's interleaving**), so it takes `VaultIndex::offer_candidates`'s
  default (`link_resolver.rs:67-71`: every path, in index order).
  `InMemoryVaultIndex::offer_candidates` (`:181-185`) looks candidates up
  in a `CandidateTable`, whose `offer` (`:133-151`) walks the probe's keys
  in hash order and, within one key, positions ascending — so two paths
  under *different* keys are not offered in index order relative to each
  other. The tie rule: `LinkCandidateSelector::offer` keeps the first
  equal-ranked exact match (`:553-558`, "Strictly better only"). Only an
  exact target — rooted (`/foo`, `./foo`) or qualified (`sub/foo`),
  `:484-495` — can tie on order: its rank is the index of the key a path
  lowercases to (`SelectorRank::Exact(usize)`, `:456-458`), so `Foo.md` and
  `foo.md` both rank `Exact(1)` for `/foo` and the first offered stays. A
  basename target (`foo`) ranks by directory distance, then the path itself
  (`:460`, `:548-551`), a total order over distinct paths, so its winner is
  the same in any offer order.
  `LinkCandidateSelector` is a concrete struct (`:505-566`), not a trait.
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
template for every new fact here. The tie rule to preserve: the
pre-move winner must be the one `all_paths()`'s mapped post-order
interleaving gives today (`link_rewrite.rs:92-98`).

## Design

Four commits, in this order (each a green tree):

1. **The guard (#1481).** `common.rs`: the mutex becomes a private
   `static FAULT_MUTEX: Mutex<()>` (no `pub(super)`; the old name goes, so
   every unmigrated `super::common::ENV_FAULT_GUARD` site is a compile
   error, which is the enforcement). New items, all in `common.rs`:
   - `fn lock_recovering() -> MutexGuard<'static, ()>` (new, private):
     `FAULT_MUTEX.lock().unwrap_or_else(PoisonError::into_inner)` — the one
     place the lock is taken, so one poisoning panic never fails the facts
     after it.
   - `const FAULT_VARIABLES: [&str; 15]` (new, private, so the guard's own
     facts cannot seed their expectations from it): the fifteen names
     above.
   - `pub(super) struct FaultGuard` (new) holds the
     `MutexGuard<'static, ()>` and a `Vec<&'static str>` of the names it
     set. `pub(super) fn clearing(lock: MutexGuard<'static, ()>) ->
     FaultGuard` (new) removes every `FAULT_VARIABLES` name and wraps the
     lock, so a stale trigger from an unwound holder is cleared before the
     next fact runs. `pub(super) fn fault_guard() -> FaultGuard` (new) is
     `FaultGuard::clearing(lock_recovering())`.
   - `FaultGuard::set(&mut self, name: &'static str, value: &str)` (new)
     records the name and sets it; `FaultGuard::clear(&mut self, name:
     &'static str)` (new) removes it mid-body; `impl Drop for FaultGuard`
     (new) removes every recorded name. `Drop::drop` runs before the
     struct's fields drop, so the names are gone while the lock is still
     held, and unwinding runs it.
   - Two test-only accessors for the guard's own facts (new):
     `pub(super) fn lock_fault_mutex_without_clearing() -> MutexGuard<'static, ()>`
     (`lock_recovering()` and nothing else) and
     `pub(super) fn fault_mutex_is_poisoned() -> bool`
     (`FAULT_MUTEX.is_poisoned()`).

   The 42 sites migrate: the ten that set nothing (listed under Current
   state) become `let _env = fault_guard();`; the other 32 become
   `let mut _env = fault_guard();` (mutable because `set` and `clear` take
   `&mut self`; a `mut` binding that calls neither fails clippy's
   `unused_mut` under `-D warnings`), and their paired `set_var` /
   `remove_var` become `_env.set(…)` (the explicit `remove_var` lines go,
   except where a fact clears a variable mid-body to prove the fault is
   off, which becomes `_env.clear(name)`). A census fact searches the tests
   tree with the multi-line pattern `set_var\(\s*"SLATE_TEST_` (the split
   call at `tasks.rs:938-939` must count) and expects no match outside
   `common.rs` except the two `SLATE_TEST_DISABLE_VAULT_WATCHER` calls in
   `watch.rs`'s `the_test_switch_keeps_the_watcher_off`, so a future fact
   cannot set a trigger without the guard. Its ceiling: it sees literal
   names only; a `set_var` through a variable name passes it.
2. **The repair (#1427).** `reindex_path_locked`: delete `:8306-8308`, pass
   `&crate::links_db::FoldedNameLookup` to `index_file` as
   `ensure_open_base_indexed` does. The `UPDATE files SET mtime_ms = -1 …`
   at `:8299-8302` already guarantees the row exists before `index_file`
   upserts it; where the row is absent (a repair of a path never indexed),
   `index_file`'s own upsert precedes link resolution, so the lookup sees
   it. Nothing else changes; the deletion-convergence arm (`:8319-8338`) is
   untouched.
3. **Rename planning (#1485).** `VaultIndex` gains
   `fn candidate_paths(&self, probe: &CandidateProbe) -> Vec<&str>` (new;
   default: every path from `all_paths()` whose lowercase path or final
   component matches a key, in index order; `InMemoryVaultIndex`
   implements it from the `CandidateTable` by collecting the matched
   positions for every key and **sorting the positions**, so the union is
   in index order, not key-hash order). `PreMoveIndex::offer_candidates`
   then: (a) take `selector.probe()` (pre-move keys); (b) build the
   post-move probe: the same keys plus, for each `(old, new)` in
   `self.mapping.forward`, the post key that `old` would have answered
   (`Exact`: if `old.to_lowercase()` is in the keys, add
   `new.to_lowercase()`; `Names`: if `old`'s final component matches a
   key under `basename_matches`, add `new`'s final component), through a
   new `LinkCandidateSelector::probe_names(&self, path: &str) -> bool`
   (new; the probe-side twin of `offer`'s matching, so the mapping walk
   reuses the selector's rules); (c) `self.post.candidate_paths(&post_probe)`
   — in post index order — mapped each through `old_path_of` and offered
   to the selector in that order, which filters with its own rules. The
   first-offered-wins tie is then identical to `all_paths()`'s mapped
   interleaving: with `Foo.md` moved to `A.md` and an unchanged `foo.md`,
   post order `A.md, foo.md` maps to `Foo.md, foo.md`, the same sequence
   today's default offers, so the rooted target `/foo` (both paths rank
   `Exact(1)`) keeps `Foo.md`; offered the other way round it would pick
   `foo.md`. The same holds for a folder move and the qualified target
   `sub/foo`. A bare `foo` is no witness: its basename rank breaks the tie
   by path, whatever the order. The default `all_paths` stays for the
   planner's other uses.
4. **Bases and DQL (#1486).** `VaultLookup::resolve_link` becomes
   `fn resolve_link(&self, target: &str) -> Result<Option<String>, EvalError>`
   (default `Ok(None)`); `EvalError` gains `LinkLookupFailed { message:
   String }` (new; `#[error("link lookup failed: {message}")]`). The four
   callers propagate with `?` (`eval.rs:1214`, `:2251`, `:4791`,
   `engine.rs:538`; at `:4791` the `.or_else(|| link.resolved_path.clone())`
   applies to the `Ok` value). `SqlVaultLookup` drops `link_index`; its
   `resolve_link_from` keeps the `link_resolutions` memo and resolves by
   building a `LinkCandidateSelector::new(target, source_path)`, calling
   `FoldedNameLookup.offer_to(self.conn, &mut selector)` on the query's
   connection and taking `into_winner()`, with a `?` from `VaultError`
   into `EvalError::LinkLookupFailed { message: error.to_string() }`.
   (`resolve_link`'s normalisation — anchors, external targets — is reused
   through `links_db::resolve_remembered` (`:235`) where its signature
   fits; otherwise the selector path above, which is what `resolve_link`
   itself does.) The query result then carries the error the way other
   `EvalError`s already reach the host (no new FFI; the Bases failed state
   and DQL error rows already render `EvalError` text).
   `tests/bases_eval.rs:1226` updates its signature.

Deliberately not changed: `re_resolve_unresolved_links_under`'s snapshot
(WB-19 owns it); the scan's and rename's `InMemoryVaultIndex` snapshots
(#1429 decided they keep theirs); `collect_rewrite_candidates`'s
whole-`links` read (the issue excludes it); no FFI.

## Tests

Rust facts, snake_case, in the files named. Each fails first on `main`'s
code (the mutation column says how it fails again if the fix regresses).
The two new session test modules, `fault_guard.rs` and
`rename_planning.rs`, are registered in `session.rs`'s test module list
(`:22300-22305`, beside `mod descriptor_interior;`).

| Fact | Asserts | Mutation that fails it |
|---|---|---|
| `session/tests/fault_guard.rs` (new): `a_panicking_holder_leaves_no_variable_it_set` | a spawned thread takes `fault_guard()`, calls `set("SLATE_TEST_FAULT_GUARD_PROBE", "1")` — a name deliberately outside `FAULT_VARIABLES`, so no acquisition by this or any concurrent fact can clear it — and panics; the join is asserted `Err`; then, before this fact acquires anything, `std::env::var_os("SLATE_TEST_FAULT_GUARD_PROBE")` is `None` (only `Drop` can have removed it) | delete `impl Drop for FaultGuard` |
| `a_panicking_holder_leaves_the_fault_mutex_usable` | a spawned thread takes `fault_guard()` and panics; the join is `Err`; `fault_mutex_is_poisoned()` is `true` (the precondition: the unwind did poison the lock); a following `fault_guard()` on the test thread returns a guard instead of panicking | restore `FAULT_MUTEX.lock().unwrap()` in `lock_recovering` |
| `the_fault_guard_clears_every_fault_variable_on_acquisition` | the fact's own literal array of the fifteen names (written out in the test, not read from `FAULT_VARIABLES`): holding `lock_fault_mutex_without_clearing()`, it sets each to `"wb18-no-fixture-path"` (a substring no fixture path contains, so no concurrent unguarded fact's trigger fires), hands the lock to `FaultGuard::clearing`, and asserts all fifteen absent while the guard is held | delete any one name from `FAULT_VARIABLES` |
| `no_test_sets_a_fault_variable_outside_the_guard` (census) | the multi-line pattern `set_var\(\s*"SLATE_TEST_` over every `.rs` under `src/session/tests` has no match outside `common.rs` except the two `SLATE_TEST_DISABLE_VAULT_WATCHER` calls of `watch.rs`'s `the_test_switch_keeps_the_watcher_off` | add back one `std::env::set_var("SLATE_TEST_FAULT_REINDEX", …)`, or one split across two lines as `tasks.rs:938-939` was |
| `save_link_lookup.rs`: `a_repair_through_reindex_path_reads_no_snapshot_of_the_vault` | after `reindex_path` on a note with the census targets, `snapshot_paths() == 0`, `PATH_ORDER_SNAPSHOT_PATHS == 0`, `resolution_rows_visited()` ≤ the note's own candidate rows | restore `:8306-8308` |
| `a_repair_writes_the_links_a_scan_writes` | `link_rows` after repair equal `link_rows` after a full scan over `census_vault` | offer a wrong key set |
| `link_resolver.rs` tests: `candidate_paths_returns_the_union_in_index_order` | for a probe with two keys whose matches interleave, positions ascending | return in key order |
| `link_rewrite.rs` tests: `the_pre_move_index_offers_the_same_winner_as_every_path` | for each mapping shape (rename, move, folder move, case-only) and every `INDEXED_PATHS` target, `offer_candidates` and the `all_paths` default pick one winner | drop the moved-paths arm |
| `a_case_colliding_rename_keeps_todays_tie_winner` | two order-sensitive exact fixtures, source `notes/x.md`: (a) rename — post index `[A.md, foo.md]`, mapping `Foo.md → A.md`, rooted target `/foo`; (b) folder move — post index `[other/A.md, sub/foo.md]`, mapping `sub/Foo.md → other/A.md`, qualified target `sub/foo`. Each first proves it is a witness: a fresh `LinkCandidateSelector::new(target, "notes/x.md")` offered the two pre-move paths in post order picks the moved one (`Foo.md`, `sub/Foo.md`) and offered them reversed picks the unmoved one (`foo.md`, `sub/foo.md`). Then `PreMoveIndex::offer_candidates`'s winner equals the `all_paths` default's winner and is the moved one | offer unmoved paths before moved ones (or offer in key-hash order) |
| `session/tests/rename_planning.rs` (new): `a_rename_plans_its_rewrites_from_candidate_rows_only` | row counter: `PATHS_OFFERED` after renaming a note linked by N sources ≤ sources × their distinct targets' candidates, not × files | restore the default |
| `session/tests/bases.rs` (the `#[cfg(test)]` counters and `LOOKUP_FAULT` are visible to the library's own tests only, so these three live there; `tests/bases_eval.rs:1226` only takes the new signature): `a_query_that_resolves_links_materializes_no_snapshot` | a Bases query with `link()` over a 200-note vault: `snapshot_paths() == 0` and `CANDIDATE_TABLES_BUILT == 0` | restore `link_index` |
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

PowerShell, repo root. Each filter is listed first and the block throws
unless every required fact is discovered under it; every command's
`$LASTEXITCODE` is checked (no output grepping for "Failed").

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb18'
function Assert-Ok([string]$step) { if ($LASTEXITCODE -ne 0) { throw "$step failed ($LASTEXITCODE)" } }
function Assert-Facts([string]$filter, [string[]]$facts) {
    $listed = cargo test -p slate-core --locked $filter -- --list
    Assert-Ok "listing $filter"
    foreach ($fact in $facts) {
        if (-not ($listed -match "(^|::)$([regex]::Escape($fact)): test$")) { throw "filter '$filter' does not select $fact" }
    }
}
$required = [ordered]@{
    'fault_guard'      = @('a_panicking_holder_leaves_no_variable_it_set', 'a_panicking_holder_leaves_the_fault_mutex_usable', 'the_fault_guard_clears_every_fault_variable_on_acquisition', 'no_test_sets_a_fault_variable_outside_the_guard')
    'save_link_lookup' = @('a_repair_through_reindex_path_reads_no_snapshot_of_the_vault', 'a_repair_writes_the_links_a_scan_writes')
    'link_resolver'    = @('candidate_paths_returns_the_union_in_index_order')
    'link_rewrite'     = @('the_pre_move_index_offers_the_same_winner_as_every_path', 'a_case_colliding_rename_keeps_todays_tie_winner')
    'rename_planning'  = @('a_rename_plans_its_rewrites_from_candidate_rows_only')
    'bases'            = @('a_query_that_resolves_links_materializes_no_snapshot', 'a_failed_link_lookup_fails_the_query_instead_of_reading_as_unresolved', 'a_dql_inline_link_field_resolves_through_the_lookup')
}
foreach ($filter in $required.Keys) {
    Assert-Facts $filter $required[$filter]
    cargo test -p slate-core --locked $filter; Assert-Ok "cargo test $filter"
}
cargo test -p slate-core --locked --no-fail-fast; Assert-Ok 'the whole core suite'
# what windows.yml:337-341 runs:
cargo test -p slate-core --profile ci --locked -- --skip census_ --skip perf_guard_root_listing_under_100ms_on_10k_files --skip perf_guard_unresolved_link_rescan_within_twice_the_link_free_one; Assert-Ok 'windows lane core tests'
# the gates CONTRIBUTING.md:93-96 says to run on Windows:
cargo fmt --all -- --check; Assert-Ok 'fmt'
cargo clippy --all-targets --workspace -- -D warnings; Assert-Ok 'clippy'
cargo test --workspace --locked; Assert-Ok 'workspace tests'
cargo bench --workspace --no-run; Assert-Ok 'bench compile'
bash scripts/check-license-headers.sh; Assert-Ok 'licence headers'
```

Mutation run: apply each row's mutation locally, run the named fact, see
it fail, revert (the #1517 recipe; no probe PR).

CI: `windows.yml` rust-tests (always on) plus `rust.yml` (Linux proves
`cfg(unix)` and runs the two perf guards isolated, `rust.yml:269-290`).
AT pass: none — no speech changes.

## Acceptance

- [ ] The thirteen facts above exist, were seen failing, pass, and each
      filter's discovery check passed (no required fact missing).
- [ ] `FAULT_MUTEX` and `FAULT_VARIABLES` are private; no
      `set_var\(\s*"SLATE_TEST_` match outside `common.rs` but the
      watcher switch's two; the ten exclusion-only sites bind `_env`
      without `mut`.
- [ ] Each guard mutation (delete `Drop`, restore `.unwrap()`, drop a
      name from `FAULT_VARIABLES`) was seen failing its own fact.
- [ ] `reindex_path_locked`, `PreMoveIndex` and `SqlVaultLookup` read no
      `files` snapshot (the counters prove it); the case-collision fact
      pins today's tie winner on the exact targets `/foo` and `sub/foo`,
      whose reversed offer is shown to pick the other path.
- [ ] `VaultLookup::resolve_link` returns `Result`; no FFI change.
- [ ] `BENCHMARKS.md` entry; PR body `Closes #1481`, `Closes #1427`,
      `Closes #1485`, `Closes #1486`.
- [ ] Not delivered: the whole-`links` read in `collect_rewrite_candidates`
      (named in #1485 as out of scope; file a follow-up if the row counter
      shows it dominates).

## Brief

Make every remaining link resolution in core read its own candidate rows,
and make the test fault guard poison-proof. No owner decision is pending.
Read `links_db.rs:85-235` (counters, `LinkCandidates`, `FoldedNameLookup`),
`session.rs:8258-8345` (`reindex_path_locked`) beside `:18729-18770` (the
model) and `:10803-10815` (`index_file`), `link_rewrite.rs:60-130`
(`PreMoveIndex`), `link_resolver.rs:50-75`, `:130-160`, `:440-600`
(`VaultIndex`, `CandidateTable::offer`, `SelectorRank`, the selector and
probe: an exact target — `/foo`, `sub/foo` — ranks by key index, so two
paths lowercasing to one key tie and the first offered wins; a bare `foo`
ranks by distance then path and ignores order), `bases/engine.rs:3564-3672`,
`bases/eval.rs:336-352`, `:481-500`, `:1208-1222`, `:2246-2258`,
`:4783-4800`, `tests/bases_eval.rs:1226`, `session/tests/common.rs:1-40`,
`tasks.rs:938-939` (a `set_var` split across two lines),
`watch.rs:1150-1163` (the watcher switch, not a fault seam: it keeps its
own `WATCHER_SWITCH`), and `save_link_lookup.rs:93-135`. The fifteen fault
variables: `SLATE_TEST_FAULT_{AFTER_WRITE, PRE_WRITE, REINDEX, REINDEX_DB,
INTENT_CLEAR, PLANT_MARKERS, RECOVERY_FINALIZE}`,
`SLATE_TEST_INTENT_ABANDON_ZERO_FOR`, `SLATE_TEST_COMMIT_AFTER_INTENT`,
`SLATE_TEST_SWEEP_AFTER_INTENT`, `SLATE_TEST_RESOLVE_BEFORE_QUARANTINE`,
`SLATE_TEST_SECOND_WRITER_BEFORE_QUARANTINE`,
`SLATE_TEST_STALL_ORPHAN_REPAIR`, `SLATE_TEST_TORN_FILE_TASKS`,
`SLATE_TEST_TORN_NOTE_READ`.

Build, in order: (1) in `common.rs`, all new: the private `FAULT_MUTEX`;
private `fn lock_recovering() -> MutexGuard<'static, ()>`
(`.lock().unwrap_or_else(PoisonError::into_inner)`, the only place the
lock is taken); private `const FAULT_VARIABLES: [&str; 15]`;
`pub(super) struct FaultGuard` with `fn clearing(lock) -> FaultGuard`
(removes every `FAULT_VARIABLES` name, then wraps the lock),
`set(&mut self, name: &'static str, value: &str)`,
`clear(&mut self, name: &'static str)` and `impl Drop` removing every
recorded name; `pub(super) fn fault_guard() -> FaultGuard`
(`FaultGuard::clearing(lock_recovering())`); and the two accessors for
the guard's own facts, `lock_fault_mutex_without_clearing() ->
MutexGuard<'static, ()>` and `fault_mutex_is_poisoned() -> bool`. Migrate
the 42 sites: the ten exclusion-only ones (`structural_identity.rs:118`,
`:143`, `:179`, `:203`, `structural_batch.rs:4098`, `tasks.rs:1660`,
`:1833`, `:1917`, `:2079`, `:2226`) to `let _env = fault_guard();`, the
other 32 to `let mut _env = fault_guard();` with `_env.set(…)` /
`_env.clear(…)` (a needless `mut` fails clippy). (2) `reindex_path_locked`
passing `&FoldedNameLookup` to `index_file` instead of a snapshot. (3)
`VaultIndex::candidate_paths` (index-order union of a probe's matches;
`InMemoryVaultIndex` sorts positions), `LinkCandidateSelector::probe_names`,
and `PreMoveIndex::offer_candidates` that offers the post index's
candidates for the pre keys plus the moved files' post keys, mapped back
through `old_path_of`, in post index order. (4)
`VaultLookup::resolve_link -> Result<Option<String>, EvalError>`,
`EvalError::LinkLookupFailed { message: String }`, and `SqlVaultLookup`
resolving through `FoldedNameLookup` on the query's connection.

Facts, each seen failing first, with the mutation that fails it again:
- `fault_guard.rs` `a_panicking_holder_leaves_no_variable_it_set`: a
  spawned holder sets `SLATE_TEST_FAULT_GUARD_PROBE` (outside
  `FAULT_VARIABLES`, so no acquisition clears it) and panics; after the
  `Err` join and before any reacquisition the variable is unset (delete
  `Drop`).
- `a_panicking_holder_leaves_the_fault_mutex_usable`: after a holder's
  panic `fault_mutex_is_poisoned()` is true and the next `fault_guard()`
  returns (restore `.unwrap()` in `lock_recovering`).
- `the_fault_guard_clears_every_fault_variable_on_acquisition`: the
  fact's own literal list of the fifteen names, set by hand to
  `"wb18-no-fixture-path"` under `lock_fault_mutex_without_clearing()`,
  are all absent once `FaultGuard::clearing` takes the lock (drop a name
  from `FAULT_VARIABLES`).
- `no_test_sets_a_fault_variable_outside_the_guard` (census): no
  multi-line `set_var\(\s*"SLATE_TEST_` outside `common.rs` but the
  watcher switch's two (add one back, single-line or split).
- `save_link_lookup.rs` `a_repair_through_reindex_path_reads_no_snapshot_of_the_vault`:
  `snapshot_paths() == 0`, `PATH_ORDER_SNAPSHOT_PATHS == 0`, rows visited
  within the note's own candidates (restore `:8306-8308`).
- `a_repair_writes_the_links_a_scan_writes`: link rows after repair equal
  those after a full scan of the census vault (offer a wrong key set).
- `link_resolver.rs` `candidate_paths_returns_the_union_in_index_order`:
  two interleaving keys' matches come back in ascending position (return
  in key order).
- `link_rewrite.rs` `the_pre_move_index_offers_the_same_winner_as_every_path`:
  for rename, move, folder move and case-only mappings over every
  `INDEXED_PATHS` target, `offer_candidates` and the `all_paths` default
  pick one winner (drop the moved-paths arm).
- `a_case_colliding_rename_keeps_todays_tie_winner`: `/foo` over post
  `[A.md, foo.md]` with `Foo.md → A.md`, and `sub/foo` over post
  `[other/A.md, sub/foo.md]` with `sub/Foo.md → other/A.md`, from
  `notes/x.md`; each first shows a fresh selector picks the moved path in
  post order and the unmoved one reversed, then the pre-move index's
  winner equals the default's and is the moved path (offer unmoved paths
  first).
- `rename_planning.rs` `a_rename_plans_its_rewrites_from_candidate_rows_only`:
  `PATHS_OFFERED` ≤ sources × their distinct targets' candidates, not ×
  files (restore the default).
- `session/tests/bases.rs` (the counters are `#[cfg(test)]`, so not in
  `tests/bases_eval.rs`) `a_query_that_resolves_links_materializes_no_snapshot`:
  a `link()` query over 200 notes leaves `snapshot_paths() == 0` and
  `CANDIDATE_TABLES_BUILT == 0` (restore `link_index`).
- `a_failed_link_lookup_fails_the_query_instead_of_reading_as_unresolved`:
  with `LOOKUP_FAULT` the query is `Err(EvalError::LinkLookupFailed)` and
  no row reads unresolved (restore `.ok()?`).
- `a_dql_inline_link_field_resolves_through_the_lookup`: the `:2251` arm
  resolves a basename target to the census answer (break the probe
  mapping).

Registers: `BENCHMARKS.md` (a WB-18 entry with the row counts). Not
delivered: the whole-`links` read in `collect_rewrite_candidates`. Done
when the Verification block runs clean in PowerShell — every filter's
discovery check finds its required facts and every command's
`$LASTEXITCODE` is 0 (the six filters, the whole core suite with
`--no-fail-fast`, the Windows lane's skip list under `--profile ci`, fmt,
clippy, workspace tests, bench compile, licence headers) — the Windows
and rust lanes are green, each fact's mutation was run locally, and the
four issues close with `Closes`. Commit:
`fix(core): every link resolution reads its own candidate rows and a panicking fault fact poisons no guard`.
