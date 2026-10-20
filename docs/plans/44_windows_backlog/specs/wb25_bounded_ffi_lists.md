# WB-25 — No Windows path marshals a whole-vault list across the FFI: the tag tree is paged and cancellable, and Quick Open ranks against the index

Issues: Closes #1290. Size L (stage 1 S–M: the tag tree; stage 2 L: the switcher). Lanes: rust, windows, swift (compile). AT: none (no speech changes). Wave: W8 (stage 1, after WB-21b in slot 3), W9 (stage 2). Depends on: WB-10 (the change feed's `QuickSwitcherViewModel.cs` edits land first, §6). Parallel with: WB-22, WB-23 (W8); WB-26, WB-27 (W9). Decision: WD17 (pending; recommended: keep `tag_tree` and `switcher_rank_top` until mac migrates, add the siblings, WB-0 files the mac follow-up; under "retire now": the mac call sites move in the same PR and the Swift lane runs its tests, not only the compile).

## Goal

Locked decision 05 §9.3.1 forbids unbounded lists across the FFI. Two
Windows paths still carry them: Quick Open holds the vault's whole file
list host-side and passes it back into core on every keystroke, and the
Files tree fetches the whole tag projection tokenless and unpaged.
After this chunk the tag tree arrives in pages under a `CancelToken`
(a cancelled refresh stops between pages and publishes nothing), and
Quick Open's ranking is a bounded top-N query core runs against its
own index, so the host never holds or marshals the file set and a
keystroke's work grows with the vault only inside core's cursor. Mac
keeps `tag_tree` and `switcher_rank_top` until its follow-up.

## Current state on main

Verified against the worktree. The issue's line numbers drifted (the
plan §2.2 records it); one correction to the plan: `report_rel.md` says
`lib.rs:852` for `tag_tree` — it is `:851-853`.

- `docs/plans/05_locked_architecture_decisions.md:1554-1556` §9.3.1:
  "All vault operations are paged or streamed … `Paging` is required on
  every list-like operation. Methods that could return unbounded results
  simply don't exist."
- Done already: `crates/slate-uniffi/src/lib.rs:909-918` `list_files(filter, paging, cancel: Arc<CancelToken>)`;
  core `session.rs:6313-6325` polls the token inside the statement.
  The host pages it: `VaultLifecycleViewModel.cs:1747-1767` `LoadSwitcherFiles`
  (500-row pages, the whole list kept) with `SwitcherPageLoadingForTests` (`:1745`).
- **Tag tree.** `lib.rs:851-853` `pub fn tag_tree(&self) -> Result<TagTree, VaultError>`
  (tokenless, unpaged); FFI records `:653-665` `TagTreeEntry { segment, full, file_count, direct_count, depth }`,
  `:669-674` `TagTree { entries, untagged_count, audio_summary }`; the
  FFI flattens core's nested tree pre-order in `flatten_tag_tree`
  (`lib.rs:718-740`). Core's own type is nested: `tag_tree.rs:25-39`
  `TagTreeNode { segment, full, file_count, direct_count, children }`
  and `TagTree { roots, untagged_count, audio_summary }` — core has no
  `entries` field. `session.rs:8493-8510` reads every `file_tags` row
  into memory and `tag_tree::build_tag_tree` (`tag_tree.rs:54`)
  assembles a `BTreeMap` keyed by SEGMENT at each level (siblings
  alphabetical by segment, children after their parent), so the
  flattened order is segment-tree pre-order, not lexical order of the
  full tag: `a`, `a/z`, `a-b` (lexically `a-b` < `a/z`, since `-` is
  0x2D and `/` is 0x2F).
- The LIKE trap: `bases/engine.rs:1460-1465` `escape_like` and the
  `(tag_norm = ? OR tag_norm LIKE ? ESCAPE '\\')` prefix arm at `:1549-1551`;
  `session/tests/search.rs:461-476` `tag_scope_like_metachars_do_not_over_match`
  pins that `a_b`'s descendants exclude `aXb/child`.
- Windows consumer
  `FilesSidebarViewModel.cs:1377-1392` `BuildTags(cancellationToken)`:
  checks the token before and after the tokenless call (`:1383-1387`,
  the comment names #1290), `AfterTagTreeForTests` (`:1375`) fires once.
  Tests: `RescanTests.cs:1236`, `:1258`, `:1675` use that seam.
- **Quick Open.** `QuickSwitcherViewModel.cs:32` `_files`; `:28` the
  `_rankTop` seam `Func<SwitcherFile[], string, string[], SwitcherRankPage>`;
  `:66-67` the default calls `SlateUniffiMethods.SwitcherRankTop(files, query, recents, DisplayCap)`
  (`DisplayCap = 50`, `:23`); `:474` and `:486` pass `_files` on every
  refresh; `:244-252` `ApplyFileChange` and `:258` `Applied` keep the
  list in step; `:370-379` the rescan journal replay; `:430`
  `FilePathsForTests`. FFI `lib.rs:8849-8862` `switcher_rank_top(files: Vec<SwitcherFile>, query, recent_paths, limit) -> SwitcherRankPage`;
  core `switcher.rs:232` `switcher_rank_top(&[SwitcherFile], …)` and
  `:98` `score(query, file) -> Option<(i32, Vec<MatchSpan>)>`. The
  index has what the ranker needs: `files(id, path, name, mtime_ms, size_bytes, is_markdown, birthtime_ms)`
  (`session.rs:11888`), filtered by `FileFilter::OpenableDocuments`
  (`:11878`), paged in `list_files_impl` with
  `directory_page::with_sqlite_cancellation` (`:6322`).
- Mac callers (untouched): `QuickSwitcherModel.swift:177` `switcherRankTop(files:query:recentPaths:limit:)`;
  `AppState.swift:6754`, `:7205` `tagTree()`.
- Tests that pin the host-side list: `QuickSwitcherRescanLandingTests.cs:94`
  (`initialFiles: Files`), `RescanTests.cs:1209`, `:1335-1345`, `:1642`
  (`FilePathsForTests`) — rewritten in stage 2 to assert through a rank.

## Reference

Locked 05 §9.3.1 (above). Contract 40 AR-63/AR-64/AR-65 (the graph and
re-resolution lists) record the same shape as accepted risks; this
chunk closes the two Windows instances the issue names. Mac: no twin
change (WD17).

## Design

WD17 recommends: "Yes; add the indexed and paged siblings, file a mac
follow-up, retire the old entry points when both hosts are off them."
If the owner answers "retire now", the mac call sites above move in the
same PR and the Swift lane runs its tests, not only the compile; the
facts below do not change.

**Stage 1 — the paged, cancellable tag tree.** Core (`tag_tree.rs`,
new types): `pub struct TagTreePageEntry { segment, full, file_count, direct_count, depth: u32 }`
(the flattened row; the FFI `TagTreeEntry` becomes `From<TagTreePageEntry>`),
`pub struct TagTreePage { entries: Vec<TagTreePageEntry>, next_cursor: Option<String>, total_entries: u32, untagged_count: u32, audio_summary: String }`,
and `pub fn flatten_preorder(tree: &TagTree) -> Vec<TagTreePageEntry>`
— the existing pre-order lifted out of `lib.rs:718-740`, which then
calls it (one flattening, shared by both entry points). Core
(`session.rs`): new `pub fn tag_tree_page(&self, paging: Paging, cancel: &CancelToken) -> Result<TagTreePage, VaultError>`;
`limit` validated like `MAX_LIST_FILES_PAGE_LIMIT`.

Order and cursor are SEGMENT-AWARE pre-order, the order `build_tag_tree`
produces, not lexical order of the full tag: the stream is
`SELECT tag_norm, COUNT(DISTINCT file_id) FROM file_tags GROUP BY tag_norm ORDER BY replace(tag_norm, '/', char(1))`
— replacing the separator with U+0001, which sorts below every character
a normalized tag can hold, makes string order equal segment-tree
pre-order (`a` < `a\u{1}z` < `a-b`, so `a`, `a/z`, `a-b`; a parent
always precedes its descendants; an implied ancestor — a prefix with no
rows of its own, emitted when its first descendant is seen — sorts
immediately before that descendant). The cursor is the last emitted
entry's `full`, and a page resumes with
`WHERE replace(tag_norm, '/', char(1)) > replace(?1, '/', char(1))`
on the same key, so the ancestor stack a host keeps across pages is
never broken and no descendant is skipped. Each emitted entry's
`file_count` is `SELECT COUNT(DISTINCT file_id) FROM file_tags WHERE tag_norm = ?1 OR tag_norm LIKE ?2 ESCAPE '\\'`
with `?2 = escape_like(full) || "/%"` — `escape_like` moved from
`bases/engine.rs:1460` to a shared `sql_like` helper so `a_b` never
counts `aXb/child` (the trap `search.rs:461-476` pins). The token is
polled per entry inside `with_sqlite_cancellation`, and a cancellation
is `Cancelled`, never a partial page. `untagged_count` and
`audio_summary` (the existing `summary(tag_count, untagged)`) ride every
page; `total_entries` counts distinct tags plus implied ancestors. FFI (`lib.rs`): `#[derive(uniffi::Record)] pub struct TagTreePage {…}`
and `pub fn tag_tree_page(&self, paging: Paging, cancel: Arc<CancelToken>) -> Result<TagTreePage, VaultError>`
beside `tag_tree`, which stays. Windows: `BuildTags` pages with the
`LoadSwitcherFiles` loop shape (a `CancelToken` cancelled from the
caller's `CancellationToken` registration; page limit 500), appending
`SidebarTagViewModel`s per page with the existing ancestor stack;
`AfterTagTreeForTests` fires per page; a cancellation before the last
page publishes nothing (`TagLoadOutcome` cancelled). The `#1290`
comment at `:1383-1387` goes.

**Stage 2 — the index-backed switcher.** Core: new
`pub fn switcher_rank_top_indexed(&self, query: &str, recent_paths: &[String], limit: usize, cancel: &CancelToken) -> Result<SwitcherRankPage, VaultError>`
on `VaultSession` (`switcher.rs` gains `rank_stream(rows, query, recents, limit)` over an
iterator of `SwitcherFile`): one statement over `files` with the
`OpenableDocuments` where-clause, `ORDER BY path COLLATE BINARY`, scored
row by row with `score`, a bounded top-`limit` min-heap keyed by the
existing sort (score, then path), `total` the exact match count, the
token polled per row; the empty-query arm reproduces `switcher_rank_top`'s
recents-first order (`:238-250`) by checking presence with the same
statement. The row shape and tie-break are pinned equal to
`switcher_rank_top` over the same inputs (a differential fact). FFI:
`pub fn switcher_rank_top_indexed(&self, query: String, recent_paths: Vec<String>, limit: u32, cancel: Arc<CancelToken>) -> Result<SwitcherRankPage, VaultError>`
beside the free `switcher_rank_top`, which stays. Windows:
`QuickSwitcherViewModel` drops `_files`, `initialFiles`, `Applied`,
`FilePathsForTests` and the file-list half of the replacement journal;
the `_rankTop` seam becomes `Func<string, string[], CancelToken, SwitcherRankPage>`
defaulting to `_session.SwitcherRankTopIndexed(query, recents, DisplayCap, cancel)`;
`ApplyFileChange` and WB-10's `ApplyRecordsAsync` keep their signatures
and now only schedule a re-rank while open (the index is already
current when a Slate-owned event or a feed record arrives); the rescan
replay (`:370-379`) becomes "re-rank once the rescan's index is in",
keeping the rank-generation waiters (`:44-47`). The ranking already
runs off the dispatcher under a `CancellationTokenSource` (`:481-485`);
that source now cancels the core token too, so a superseded keystroke's
query stops inside core. `VaultLifecycleViewModel.LoadSwitcherFiles` and
`SwitcherPageLoadingForTests` are deleted (the open no longer loads the
list; the §6 interlock with WB-9/WB-28/WB-29 on `VaultLifecycleViewModel.cs`
is one deletion). Not changed: `DisplayCap`, the row view models, recents
(`FileRecentsStore`), the Quick Open journey, mac.

## Tests

Rust (`tag_tree.rs`, `switcher.rs`, `session/tests`):
- `tag_tree_page_with_a_tiny_limit_walks_every_entry_in_pre_order` — limit 1 and limit 2 over the fixture `a`, `a/z`, `a-b`, `p/q/r` (implied `p` and `p/q`), `a_b/child`, `axb/child`: concatenated pages equal `tag_tree::flatten_preorder(&session.tag_tree()?)` entry for entry (segment, full, depth, counts); `next_cursor` None on the last; `total_entries`, `untagged_count` and `audio_summary` identical on every page. Mutations: `ORDER BY tag_norm` (lexical) → `a-b` lands between `a` and `a/z` and the fact fails; cursor by `segment` → duplicates.
- `tag_tree_page_resumes_after_a_cursor_inside_a_subtree` — a cursor at `a` yields `a/z` first, not `a-b`; a cursor at `p/q` yields `p/q/r`.
- `tag_tree_page_counts_match_the_unpaged_tree` (`file_count`, `direct_count` per entry over the same fixture).
- `tag_tree_page_like_metachars_do_not_over_match` — `a_b` has `file_count` 1 (its own child only), never counting `axb/child`. Mutation: drop `ESCAPE` / the escaping → 2.
- `tag_tree_page_cancelled_mid_statement_returns_cancelled_not_a_partial_page` (the `list_files` cancellation fact's shape).
- `tag_tree_page_rejects_a_limit_over_the_cap`.
- `switcher_rank_top_indexed_matches_switcher_rank_top_over_the_same_files` — a 1,000-file fixture, ten queries incl. empty with recents: rows and `total` equal. Mutation: break the heap's tie-break → order differs.
- `switcher_rank_top_indexed_marshals_at_most_limit_rows_with_the_exact_total`.
- `switcher_rank_top_indexed_cancelled_returns_cancelled`.
- `switcher_rank_top_indexed_sees_a_file_the_index_just_gained` (create, then rank, no host list).

Windows:
- `SidebarTagFilterTests.TheTagTreeArrivesInPagesAndACancelledRefreshPublishesNothing` — a fake session paging 3 entries at a time; cancel after page one → no `Tags` change, `AfterTagTreeForTests` called once. Mutation: ignore the cancel between pages → the tree publishes.
- `RescanTests` `:1236`, `:1258`, `:1675` keep passing (the seam fires per page; the throw fact still throws once).
- `QuickSwitcherViewModelTests.AKeystrokeRanksThroughCoreWithNoHostFileList` — the seam receives `(query, recents, token)` only; `FilePathsForTests` no longer exists (compile-time).
- `QuickSwitcherViewModelTests.ASupersededKeystrokeCancelsTheCoreQuery` — the first rank's token is cancelled when the second starts. Mutation: do not link the source to the core token → fails.
- `QuickSwitcherRescanLandingTests.ARescansSilentReRankKeepsTheKeysOnTheReadersResult` (`:27`) rewritten without `initialFiles`, same assertions.
- `RescanTests.cs:1209`, `:1335-1345`, `:1642` assert through a rank (`Rows` contains / does not contain) instead of `FilePathsForTests`.
- Census: new `Censuses/BoundedFfiListCensus.cs` with `EveryVaultListOperationTheWindowsHostCallsIsPagedOrBounded`, scraping `apps/slate-windows/src` for `.TagTree(` and `SwitcherRankTop(`: zero call sites (the `AutomationIdInventory` scrape shape).

## Docs and registers

- Contract 40: append a note under AR-63's family: "#1290 closed for the tag tree and Quick Open on Windows; mac stays on `tag_tree` / `switcher_rank_top` until its follow-up (WD17)".
- `w_c_matrix.md`: the Files tree row's notification cell gains "tag tree paged under the refresh token"; the Quick Open row's evidence cell cites the new facts.
- WB-0 files the mac follow-up ("mac: move `QuickSwitcherModel` to `switcherRankTopIndexed` and the two `tagTree()` sites to `tagTreePage`; then retire the old entry points").
- No `chords.json`, designation or sentence change.

## Verification

```powershell
$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb25'
cargo test -p slate-core tag_tree; cargo test -p slate-core switcher; cargo test -p slate-core tag_tree_page; cargo test -p slate-core switcher_rank_top_indexed   # failing first; each run summary lists the named facts
cargo fmt --all -- --check; cargo clippy --all-targets --workspace -- -D warnings; cargo test --workspace   # CONTRIBUTING.md:95-97, the `make ci` gates on PowerShell
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0 before reading counts
$filter = "FullyQualifiedName~SidebarTagFilterTests|FullyQualifiedName~RescanTests|FullyQualifiedName~QuickSwitcherViewModelTests|FullyQualifiedName~QuickSwitcherRescanLandingTests|FullyQualifiedName~QuickSwitcherRankCoordinatorTests|FullyQualifiedName~W1WorkspaceTests|FullyQualifiedName~CommandPaletteTests|FullyQualifiedName~BoundedFfiListCensus"
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --no-build --configuration Release --list-tests --filter $filter | Select-String "TheTagTreeArrivesInPagesAndACancelledRefreshPublishesNothing|AKeystrokeRanksThroughCoreWithNoHostFileList|ASupersededKeystrokeCancelsTheCoreQuery|EveryVaultListOperationTheWindowsHostCallsIsPagedOrBounded" | Measure-Object | ForEach-Object { if ($_.Count -lt 4) { throw "new facts not discovered: $($_.Count)" } }
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --no-build --configuration Release --filter $filter
# desktop free, NVDA closed (partial class ShellAccessibilityTests; select by method):
dotnet test apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj --no-build --configuration Release --filter "FullyQualifiedName~FluentShell_UiaPatternsKeyboardFocusAndAxe_AreClean"
$files = @(
  'apps/slate-windows/src/SlateWindows/FilesSidebarViewModel.cs',
  'apps/slate-windows/src/SlateWindows/QuickSwitcherViewModel.cs',
  'apps/slate-windows/src/SlateWindows/VaultLifecycleViewModel.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/SidebarTagFilterTests.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/RescanTests.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/QuickSwitcherViewModelTests.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/QuickSwitcherRescanLandingTests.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/Censuses/BoundedFfiListCensus.cs')
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $files
```

(Stage 1 touches only `FilesSidebarViewModel.cs`, `SidebarTagFilterTests.cs`
and the census; stage 2 the rest. `QuickSwitcherViewModelTests.cs` and
`Censuses/BoundedFfiListCensus.cs` are new files.)

CI: `rust.yml`, the Windows lane (both model shards and the FlaUI gate
for the Quick Open journey), `swift-tests.yml` for the compile. AT:
none — nothing a reader hears changes; if a Quick Open timing changes
what NVDA reads mid-typing, §4 rule 4 applies and the PR body records a
spot check.

## Acceptance

- [ ] Stage 1 merged on its own PR (`Refs #1290`), stage 2 on a second (`Closes #1290`), each with its facts failing first and mutations run.
- [ ] `apps/slate-windows/src` has no call to `TagTree()` or `SwitcherRankTop(`; the census pins it.
- [ ] Both bindings regenerated per PR; the Swift lane compiles; mac source untouched.
- [ ] Contract 40 note and `w_c_matrix.md` cells updated; the mac follow-up filed by WB-0 and named in the PR body.
- [ ] Not delivered, named: the mac migration and the retirement of `tag_tree` / `switcher_rank_top` (WD17).

## Brief

Read `docs/plans/05_locked_architecture_decisions.md:1554-1556`;
`crates/slate-uniffi/src/lib.rs:650-674`, `:718-740`, `:845-862`,
`:900-920`, `:2505-2515`, `:2725-2735`, `:8775-8862`;
`crates/slate-core/src/session.rs:6313-6325`, `:8493-8510`,
`:11870-11900`; `crates/slate-core/src/tag_tree.rs` whole;
`crates/slate-core/src/switcher.rs:40-110`, `:232-300`;
`crates/slate-core/src/bases/engine.rs:1455-1465`, `:1545-1552`;
`crates/slate-core/src/session/tests/search.rs:455-480`;
`apps/slate-windows/src/SlateWindows/FilesSidebarViewModel.cs:1368-1420`;
`QuickSwitcherViewModel.cs:20-75`, `:240-320`, `:360-400`, `:460-500`;
`VaultLifecycleViewModel.cs:1740-1770`; `RescanTests.cs:1200-1260`,
`:1330-1350`, `:1640`, `:1670-1680`; `QuickSwitcherRescanLandingTests.cs:27-100`;
`CONTRIBUTING.md:76-98`. Decision WD17 is pending (recommended: keep
`tag_tree` and `switcher_rank_top` for mac, add the siblings, WB-0 files
the mac follow-up; under "retire now" the mac call sites
`QuickSwitcherModel.swift:177`, `AppState.swift:6754` and `:7205` move
in the same PR and the Swift lane runs its tests). Stage 1 (its own PR,
`Refs #1290`): in `tag_tree.rs` add `TagTreePageEntry`, `TagTreePage`
and `flatten_preorder(&TagTree) -> Vec<TagTreePageEntry>` and make
`lib.rs`'s `flatten_tag_tree` use it; in `session.rs` add
`tag_tree_page(&self, paging: Paging, cancel: &CancelToken) -> Result<TagTreePage, VaultError>`
streaming `SELECT tag_norm, COUNT(DISTINCT file_id) FROM file_tags GROUP BY tag_norm ORDER BY replace(tag_norm, '/', char(1))`
(segment-tree pre-order), resuming after the cursor on the same key,
emitting implied ancestors, counting descendants with
`LIKE ? ESCAPE '\\'` over `escape_like(full) || "/%"` (helper lifted to a
shared module), polling the token per entry; export
`tag_tree_page(&self, paging: Paging, cancel: Arc<CancelToken>) -> Result<TagTreePage, VaultError>`
beside `tag_tree`; regenerate both bindings; page `BuildTags` in 500-row
pages under a `CancelToken` cancelled from the caller's token,
`AfterTagTreeForTests` per page, nothing published on cancellation.
Stage 1 facts, failing first: `tag_tree_page_with_a_tiny_limit_walks_every_entry_in_pre_order`
(fixture `a`, `a/z`, `a-b`, `p/q/r`, `a_b/child`, `axb/child`; pages
concatenated equal `flatten_preorder`), `tag_tree_page_resumes_after_a_cursor_inside_a_subtree`,
`tag_tree_page_counts_match_the_unpaged_tree`, `tag_tree_page_like_metachars_do_not_over_match`
(`a_b` counts 1), `tag_tree_page_cancelled_mid_statement_returns_cancelled_not_a_partial_page`,
`tag_tree_page_rejects_a_limit_over_the_cap`;
`SidebarTagFilterTests.TheTagTreeArrivesInPagesAndACancelledRefreshPublishesNothing`;
`RescanTests.cs:1236`, `:1258`, `:1675` still green. Stage 2 (second
PR, `Closes #1290`): in `switcher.rs` add `rank_stream` and in
`session.rs` `switcher_rank_top_indexed(&self, query: &str, recent_paths: &[String], limit: usize, cancel: &CancelToken) -> Result<SwitcherRankPage, VaultError>`
over `files` with the `OpenableDocuments` where-clause, a bounded
top-`limit` heap with `switcher_rank_top`'s tie-break, the exact
`total`, the recents-first empty-query arm, the token polled per row;
export it beside `switcher_rank_top`; in `QuickSwitcherViewModel` drop
`_files`, `initialFiles`, `Applied`, `FilePathsForTests` and the
file-list half of the replacement journal, make the `_rankTop` seam
`Func<string, string[], CancelToken, SwitcherRankPage>` defaulting to
`_session.SwitcherRankTopIndexed(query, recents, DisplayCap, cancel)`,
turn `ApplyFileChange` / `ApplyRecordsAsync` and the rescan replay into
"re-rank while open", link the keystroke `CancellationTokenSource` to
the core token; delete `LoadSwitcherFiles` and `SwitcherPageLoadingForTests`.
Stage 2 facts, failing first: `switcher_rank_top_indexed_matches_switcher_rank_top_over_the_same_files`
(1,000 files, ten queries incl. empty with recents),
`switcher_rank_top_indexed_marshals_at_most_limit_rows_with_the_exact_total`,
`switcher_rank_top_indexed_cancelled_returns_cancelled`,
`switcher_rank_top_indexed_sees_a_file_the_index_just_gained`;
`QuickSwitcherViewModelTests.AKeystrokeRanksThroughCoreWithNoHostFileList`,
`QuickSwitcherViewModelTests.ASupersededKeystrokeCancelsTheCoreQuery`,
`QuickSwitcherRescanLandingTests.ARescansSilentReRankKeepsTheKeysOnTheReadersResult`
rewritten without `initialFiles`, `RescanTests.cs:1209`, `:1335-1345`,
`:1642` asserting through a rank, and
`BoundedFfiListCensus.EveryVaultListOperationTheWindowsHostCallsIsPagedOrBounded`
(zero `.TagTree(` / `SwitcherRankTop(` call sites under `apps/slate-windows/src`).
Mutations to run locally: lexical `ORDER BY tag_norm`; cursor by segment;
drop `ESCAPE`; break the heap tie-break; ignore the cancel between pages;
unlink the keystroke source from the core token. Registers: a note under
AR-63's family in `docs/plans/40_nvda_matrix_remediation_contracts.md`;
the Files tree and Quick Open cells in `docs/plans/18_windows_port/w_c_matrix.md`;
the mac follow-up WB-0 files, named in both PR bodies. Rust gates on
PowerShell: `cargo fmt --all -- --check`, `cargo clippy --all-targets
--workspace -- -D warnings`, `cargo test --workspace`. Done when each
PR's facts pass locally with the discovered count checked, `dotnet
format --include` over its files is clean, `rust.yml`, the Windows lane
and the Swift compile are green, and mac source is untouched; no AT
pass (nothing a reader hears changes). Not delivered: the mac migration
and the retirement of the old entry points (WD17). Commits:
`feat(core,windows): the tag tree arrives in cancellable pages`, then
`feat(core,windows): quick open ranks against the index and the host
holds no file list`.
