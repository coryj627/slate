# WB-10a — Core: a change-feed window that carries another session's change drops the link graph, so the hosts' generation probes rebuild it

Issues: #1519 (closes here). Size S–M. Lanes: rust, windows (`cargo test -p slate-core` runs on the Windows lane too, `windows.yml:228-346`). AT: none (no speech changes). Wave: W4 (slot 1, before WB-10). Depends on: nothing (21a is on `main`). Parallel with: WB-11, WB-12. Decision: none.

## Goal

A session's in-memory link graph learns only its own commits, so after another Slate session adds or removes a link, this session's Connections leaf and graph views stay on the old links even though a Refresh, a watcher run or a change-feed window applied the change everywhere else (the SQL-backed backlinks are current). After this chunk, opening a change-feed window that carries any record from another writer — or that cannot say what changed (a `Gap` or a `TooLarge` window, behind which the host's full re-sync cannot see links another session already indexed) — drops the graph index and bumps the generation floor, so the hosts' existing `graph_generation()` probes (Windows R-9 (d), mac's three `refresh…IfGraphChanged`) see "changed" and rebuild lazily. No FFI and no host change; a window with only this session's own writes (no records) leaves the graph alone.

## Current state on main

Verified against the worktree; the report's lines hold.

- `crates/slate-core/src/session.rs:2086-2109` `graph_apply(sink)`: the only path that mutates the live index — applies a batch after the session's own mutation transactions, keeps `graph_generation_floor` in lockstep (`:2101-2104`), and drops the index on a poisoned sink or a failed batch.
- `session.rs:2025-2033` `graph_drop_locked(&mut guard)`: `*guard = None`, `graph_generation_floor.fetch_add(1)`, clears `graph_metrics`. LOCK ORDER (`:1388`, `:2023`, `:2167`): `conn` → `graph` → `graph_metrics`; the caller holds the graph lock.
- `session.rs:2182-2196` `graph_generation()`: the index's generation when built, else the floor — "after a defensive drop the bumped floor tells the refresh contract 'changed'".
- `session.rs:3388-3414` `change_feed_window(after, cancel)`: locks `conn` (`:3397`), calls `change_feed::open_window`, and on `OpenedWindow::Gap` records the gap in a fenced transaction and returns `gap_window(…)`. It never touches the graph.
- `crates/slate-core/src/change_feed.rs:100-129`: `ChangeFeedGap { NoCursor, Trimmed, Reset, ResyncPending }`, `ChangeFeedStatus { Exact, TooLarge, Gap(ChangeFeedGap) }`; `ChangeFeedWindow` (`:131`) carries `record_count`, `counted`, `changed`, `removed`. `TooLarge` and `Gap` carry no records (doc comment `:128-129`).
- Records exist only for paths a row that is not this session's own write touched, or whose shown and index states differ at the end (design §4.3, `21_change_feed_design.md:553-554`); a window of own writes alone has `record_count == 0` (`two_sessions_count_and_apply_each_others_create_modify_and_delete`, `session/tests/change_feed.rs:176-233`, asserts `own.record_count == 0`).
- Host probes: Windows `Graph/GraphDocumentViewModel.cs:2078-2080` (`CrossingsForTests["graph_generation"]++; return _session.GraphGeneration();`) and `Graph/ConnectionsLeafViewModel.cs:795-797` (`CountCrossing("graph_generation"); return _session.GraphGeneration();`), both run by `WorkspaceViewModel.Rescan.cs:743-760` `NotifyGraphOfRescanAsync` on every run; mac `AppState+Connections.swift:160`, `AppState+GraphDiagram.swift:144`, `AppState+GraphTable.swift:737`.
- Test harness: `session/tests/change_feed.rs:26-60` (`open`, `two_hosts(setup) -> (tmp, a, b)`, `window`, `window_after`, `records`, `ack`); no fact in that file mentions the graph. The issue's XCTest evidence was never committed.
- A correction to the plan's row: a `Renamed` or `Removed` record of entry `Directory` also invalidates the graph (the index is keyed by path, and a folder another session renamed moves every note's path under it), so the trigger is "any record", not "a file record". The plan's lane cell lists `swift (compile)`; this chunk changes no FFI record, so `swift-tests.yml` is not needed unless `lib.rs` is touched.

## Reference

The issue's "possible direction" and the change-feed design §4.3 (`21_change_feed_design.md:652-657`): "the Bases and dashboard re-runs and the graph probe (when any file record exists)" — the probe exists; this chunk makes it find something. Contract 40 R-9 (d) (Windows), the mac change-feed paragraph (`40_nvda_matrix_remediation_contracts.md:27`: "the leaves other files feed (backlinks, links and embeds) when any file record exists"). AR-30's remainder AR-76 (`:79`) names the gap's full re-sync as the fallback: the graph must not be the one surface that fallback leaves stale.

## Design

In `VaultSession::change_feed_window` (`session.rs:3388`), with `conn` held (so the graph lock is taken in LOCK ORDER): after `open_window` returns, decide `drop_graph`:

- `OpenedWindow::Window(window)` → `window.record_count > 0 || matches!(window.status, ChangeFeedStatus::TooLarge)`;
- `OpenedWindow::Gap(_)` → `true` (after the fenced `record_gap` commits).

When `drop_graph`, `let mut guard = self.graph.lock().expect("graph index mutex"); if guard.is_some() { self.graph_drop_locked(&mut guard); } else { self.graph_generation_floor.fetch_add(1, SeqCst); }` — the second arm matters: a session whose index was never built (or already dropped) reports the floor, and the probes compare generations, so the floor must move even with no index to drop (otherwise two foreign changes in a row, with no query between, would read the same generation; `graph_generation` documents the floor as the "changed" signal). Factor that into a private `graph_invalidate_locked_conn(&self)` helper beside `graph_drop_locked`, with a doc comment naming #1519 and the LOCK ORDER. `Exact` with no records: untouched (own writes already reached the graph through `graph_apply`). The drop is conservative on `TooLarge` and `Gap` by design: the host's full re-sync cannot tell the graph what another session indexed.

Nothing else changes: `graph_apply`, `graph_generation`, the FFI (`slate-uniffi/src/lib.rs:1442` `change_feed_window` returns the same record), the hosts (their probes already run after every run), the scan (a scan's own rows reach the graph through `graph_apply`? — no: the scan re-indexes nothing another session already indexed, which is the bug; the window is the one place that knows a foreign change happened). Not changed either: `graph_neighborhood`'s lazy rebuild (`graph_ensure_built`, `:2112`), which is what the next probe's query triggers.

## Tests

`crates/slate-core/src/session/tests/change_feed.rs`, with `two_hosts` (Rust snake_case facts; each builds A's graph first through `graph_neighborhood("b.md")` so the generation is non-zero, then reads `graph_generation()` before and after):

- `a_foreign_link_change_in_an_exact_window_drops_this_sessions_graph` — B saves `a.md` with `[[b]]`; A's `change_feed_window(None)` is `Exact` with records; A's `graph_generation()` moved and `graph_neighborhood("b.md")` now has one edge (the lazy rebuild). Mutation: remove the `record_count > 0` arm → the generation is unchanged and the neighborhood has zero edges (the issue's table).
- `a_foreign_folder_rename_in_an_exact_window_drops_the_graph` — B renames `dir/` holding a linked note; the window's records are directory and file renames; the generation moved. Mutation: restrict the trigger to `entry == File` with a window whose only record is the directory → unchanged generation (document in the fact why directories count).
- `an_exact_window_of_own_writes_only_leaves_the_graph_alone` — A saves `a.md` with `[[b]]` itself (the graph learns it through `graph_apply`), opens its own window (`record_count == 0`): the generation equals the value `graph_apply` set, no drop (the metrics cache survives: `graph_metrics` still `Some`, read through a test-only accessor or the existing bench seam). Mutation: drop on every `Exact` window → the generation moves.
- `a_trimmed_cursor_window_drops_the_graph` — the `a_trimmed_window_is_a_gap_and_the_acknowledgment_heals_it` setup (`:865`); A's window is `Gap(Trimmed)`; the generation moved.
- `a_window_with_no_cursor_drops_the_graph` — a session that never registered: `Gap(NoCursor)`; the generation moved.
- `an_oversized_window_drops_the_graph` — the `a_window_over_the_row_limit_is_too_large_and_reads_nothing` setup (`:965`, `indexed_rows` past `CHANGE_FEED_WINDOW_ROW_LIMIT`); `TooLarge`; the generation moved.
- `a_drop_with_no_built_index_still_moves_the_generation` — A never queried the graph (floor 0); B's change; A's `graph_generation()` is 1, then another foreign change gives 2.

No Windows or mac test change: the host probes are already pinned (`ConnectionsLeafTests.Rescan.cs:255` `TheRescansTokenReachesTheProbesGraphQuery`). Optional Windows witness if cheap: `RescanTests.AnotherSessionsLinkReachesTheConnectionsLeafAfterRefresh` (B saves a link, A's Refresh, the leaf's rows include it) — it depends on WB-10's window call, so it belongs to WB-10's list, not here.

## Docs and registers

- Contract 40: a dated addendum under R-9 (d) and the mac change-feed paragraph: "(WB-10a, #1519) a window carrying any other writer's record, a gap or an oversized window drops the session's link graph, so the generation probes rebuild it." Append; never rewrite.
- `21_change_feed_design.md` §9: a residual-risk note is not needed (the issue is closed, not accepted); add one sentence to §4.3's graph-probe bullet pointing at #1519's fix.
- No corpus, census, ledger, designation or parity change.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb10a'
cargo test -p slate-core change_feed -- --list | Select-String 'drops_the_graph|drops_this_sessions_graph|leaves_the_graph_alone|moves_the_generation' | Measure-Object -Line   # 7
cargo test -p slate-core change_feed            # the new facts fail first, then pass
cargo test -p slate-core --no-fail-fast          # every target (the deferred-tx and lock-order traps)
# `make ci` = fmt-check + clippy + test + bench-check + check-license-headers (Makefile:60-73); no make here, so per CONTRIBUTING.md:95-98:
cargo fmt --all --check
cargo clippy --all-targets --workspace -- -D warnings
cargo test --workspace
cargo bench --workspace --no-run
bash scripts/check-license-headers.sh      # Git Bash; the script has no PowerShell twin
```

Lanes: `rust.yml` and `windows.yml` (its `rust-tests` job runs `cargo test -p slate-core` on Windows, `windows.yml:228`); `swift-tests.yml` only if `lib.rs` changes (it should not). AT: none — no sentence changes; the graph views update silently as R-9 (d) already specifies.

## Acceptance

- [ ] The seven Rust facts exist, fail first (mutation per fact recorded in the PR body), pass on Windows and Linux.
- [ ] `change_feed_window` invalidates the graph on records, `TooLarge` and `Gap`, in LOCK ORDER, and never on an own-writes-only `Exact` window.
- [ ] No FFI or host change; both bindings unchanged.
- [ ] Contract 40 addendum and the design §4.3 pointer appended.
- [ ] `cargo test -p slate-core --no-fail-fast`, `cargo fmt --all --check`, `cargo clippy --all-targets --workspace -- -D warnings`, `cargo test --workspace`, `cargo bench --workspace --no-run` and `bash scripts/check-license-headers.sh` green locally; `rust.yml` and `windows.yml` green.
- [ ] PR body: `Closes #1519`; names WB-10 as the Windows consumer whose Refresh now shows another session's link in Connections.

## Brief

Read `crates/slate-core/src/session.rs:2014-2040` (`graph_drop_locked`), `:2086-2110` (`graph_apply`), `:2160-2200` (`graph_generation` and its LOCK ORDER notes), `:3372-3470` (the five change-feed methods), `crates/slate-core/src/change_feed.rs:95-170` (the public records), and `crates/slate-core/src/session/tests/change_feed.rs:26-233` (the `two_hosts` harness and the first fact). Build, in `VaultSession::change_feed_window`: after `open_window` returns and with `conn` still held, a new private `graph_invalidate_locked_conn(&self)` that locks `graph` (LOCK ORDER conn → graph → graph_metrics) and calls `graph_drop_locked` when an index is built or bumps `graph_generation_floor` when none is; call it when the window is `Exact` with `record_count > 0` (any record — file or directory; this is the same "any foreign record" trigger WB-10's Windows host probes on), `TooLarge`, or a `Gap` (after `record_gap` commits); never on an own-writes-only `Exact` window. No FFI or host change. Facts in `session/tests/change_feed.rs`, each seen failing first: `a_foreign_link_change_in_an_exact_window_drops_this_sessions_graph` (B saves `[[b]]`; A's generation moves and `graph_neighborhood("b.md")` has one edge), `a_foreign_folder_rename_in_an_exact_window_drops_the_graph` (a directory-only record moves the generation), `an_exact_window_of_own_writes_only_leaves_the_graph_alone` (generation equals what `graph_apply` set; metrics cache intact), `a_trimmed_cursor_window_drops_the_graph`, `a_window_with_no_cursor_drops_the_graph`, `an_oversized_window_drops_the_graph` (each `Gap`/`TooLarge` variant moves the generation), `a_drop_with_no_built_index_still_moves_the_generation` (floor 0 → 1 → 2 across two foreign changes with no query between). Registers: append the dated sentence under R-9 (d) and under the mac change-feed paragraph in `docs/plans/40_nvda_matrix_remediation_contracts.md` (`:23`, `:27`); one pointer sentence in `docs/plans/43_mac_backlog_delivery/21_change_feed_design.md` §4.3's graph-probe bullet (`:652-657`). No decision is pending for this chunk. Not delivered: nothing. Done when the seven facts are listed by `cargo test -- --list`, `cargo test -p slate-core --no-fail-fast`, `cargo fmt --all --check`, `cargo clippy --all-targets --workspace -- -D warnings`, `cargo test --workspace`, `cargo bench --workspace --no-run` and `bash scripts/check-license-headers.sh` pass locally, `rust.yml` and `windows.yml` are green, and #1519 closes. Commit: `fix(core): a change-feed window carrying another session's change drops the link graph so the hosts rebuild it`.
