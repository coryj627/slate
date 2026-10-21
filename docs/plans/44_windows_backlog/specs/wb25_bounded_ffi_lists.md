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
  `list_files_impl` (`session.rs:11868-11931`) orders `path COLLATE BINARY`.
  The host pages it: `VaultLifecycleViewModel.cs:1747-1767` `LoadSwitcherFiles`
  (`FileFilter.OpenableDocuments`, 500-row pages, the whole list kept, so
  the host's list is in `path COLLATE BINARY` order) with
  `SwitcherPageLoadingForTests` (`:1745`); the open's `sessionLoadWorker`
  seam is typed `(ScanReport Report, SwitcherFile[] SwitcherFiles)`
  (`:93-94`, `:190-191`), filled at `:529-542` and handed to the switcher
  at `:1220`.
- **The listing seam's other users, and the open's remaining boundary.**
  Production reads `SwitcherPageLoadingForTests` once more: the rescan's
  Quick Open re-sync, `VaultLifecycleViewModel.Rescan.cs:606-648`
  `ReSyncQuickOpenAsync`, opens a journal (`switcher.BeginReplacement()`,
  `:616`), lists through `RunRescanCoreAsync("list", () => LoadSwitcherFiles(session, cancel, SwitcherPageLoadingForTests), cancellation)`
  (`:619-622`) and hands the list to `ReplaceFilesAsync(files, journal, cancellation)`
  (`:628`). In `QuickSwitcherViewModel.cs` the `ReplacementJournal`
  (`:320-341`, `_openJournals` `:49`, `JournalChange` `:343-349`) exists
  only to replay Slate-owned changes onto that listed set, and
  `ReplaceFilesAsync` (`:350-399`) completes inside the silent re-rank's
  publication through `_rescanRankWaiters` (`:44-46`). Two facts assign
  the seam: `W1HardeningTests.cs:207-246`
  `W1VaultCloseBarrierTests.ACancelledOpenLoadStopsInsideQuickOpensListingAndPublishesNothing`
  — the tuple-typed `load` at `:217`, the assignment at `:224` (a
  callback, on the load's worker before the first page, that waits for
  the disposal to cancel the open's token and then lets the listing run
  with it), the fact's wait on that callback at `:235`, and the
  listing-cancellation asserts at `:240-241` — and `RescanTests.Round3.cs:106-145`
  `RescanTests.ACloseWaitsForEveryRescanCoreCallInFlight`, whose
  assignment at `:117` parks the rescan's listing so a close must drain
  it, asserting `Assert.Single(… "list")` at `:140`. `W1HardeningTests.cs`
  holds no class named `W1HardeningTests` (namespace `SlateWindows.Tests`;
  the close-barrier facts are class `W1VaultCloseBarrierTests`, `:169`),
  and main has no `QuickSwitcherViewModelTests` class. Without the
  listing, the open's load makes one core call: `StartVaultWatch(activeSession)`
  (`VaultLifecycleViewModel.cs:535`), then
  `ScanInitialWithProgress(activeCancel, activeProgressListener)` (`:536`).
  `CloseSession` cancels that token (`_scanCancel?.Cancel()`, `:1028`)
  before it drains the rescan's calls (`DrainRescanCoreCalls()`, `:1038`)
  and joins the load (`_sessionLoadCompletion.GetAwaiter().GetResult()`,
  `:1042`, which swallows `VaultException.Cancelled`), and core's
  `scan_vault` checks the token before it counts or walks anything
  (`crates/slate-core/src/session.rs:10331-10333`: `Err(Cancelled)`, no
  listener call), then per file after `Started`.
- **Tag tree.** `lib.rs:851-853` `pub fn tag_tree(&self) -> Result<TagTree, VaultError>`
  (tokenless, unpaged); FFI records `:653-665` `TagTreeEntry { segment, full, file_count, direct_count, depth }`,
  `:669-674` `TagTree { entries, untagged_count, audio_summary }`; the
  FFI flattens core's nested tree pre-order in `flatten_tag_tree`
  (`lib.rs:718-740`). Core's own type is nested: `tag_tree.rs:25-39`
  `TagTreeNode { segment, full, file_count, direct_count, children }`
  and `TagTree { roots, untagged_count, audio_summary }` — core has no
  `entries` field. `session.rs:8493-8510` reads every `file_tags` row
  into memory and `tag_tree::build_tag_tree` (`tag_tree.rs:93-117`)
  splits each tag with `tag.split('/')` (empty segments kept) into a
  `BTreeMap<String, _>` keyed by SEGMENT at each level (siblings in
  `str` byte order, children after their parent), so the flattened
  order is segment-tree pre-order, not lexical order of the full tag:
  `a`, `a/z`, `a-b` (lexically `a-b` < `a/z`, since `-` is 0x2D and `/`
  is 0x2F).
- **Tag strings are arbitrary.** `tags_db.rs:266-271` `normalize_tag`
  trims, strips one `#` and lowercases — it forbids no character — and a
  frontmatter `tags:` list takes any string (`frontmatter.rs:758-802`
  `classify_list`); yaml-rust2 resolves the `\0` and `\x01` escapes of a
  double-quoted scalar, so `tags: ["a\x01z", "a\0b"]` indexes tags
  holding U+0001 and U+0000. Any character below `/` can therefore sit
  inside a segment. The workspace's rusqlite has no `collation` feature
  (`Cargo.toml:28`: `bundled`, `functions`, `hooks`), so SQLite cannot
  order by a custom comparator. `file_tags.tag_norm` is BINARY `TEXT`
  with `idx_file_tags_tag ON file_tags(tag_norm, file_id)`
  (`migrations/019_file_tags.sql:31-36`).
- **A node's `full` is not its identity.** `Assembly::materialize`
  (`tag_tree.rs:56-61`) builds `full` as the bare segment whenever the
  prefix is empty, and a leading empty segment's own `full` is empty, so
  leading empty segments add no separator: a node's `full` is its
  segment path joined with `/`, leading `/`s trimmed. `normalize_tag`
  (`tags_db.rs:266-270`) keeps a leading `/`, and a frontmatter `tags:`
  list carries it (`classify_list`, `frontmatter.rs:794-801`, strips only
  `#`), so the tags `/a`, `a` and `//a` build five nodes in pre-order —
  the implied `""` root, the implied `""` under it, `//a`'s leaf, `/a`'s
  leaf, the root `a` — whose `full`s are `""`, `""`, `a`, `a`, `a`. Two
  nodes share a `full` while their segment paths (`[""]` and `["", ""]`;
  `["", "", "a"]`, `["", "a"]` and `["a"]`) differ. Windows keys its tag
  filter (`FilesSidebarViewModel.cs:1089-1091`, `ApplyTagFilter(tag.Full)`),
  re-selection (`:1496`) and expansion (`:1518`, `:1534`) on `Full`, so
  today those three `a` rows filter, re-select and expand as one; mac
  shows the same tree through `tag_tree`.
- The LIKE trap: `bases/engine.rs:1460-1465` `escape_like` and the
  `(tag_norm = ? OR tag_norm LIKE ? ESCAPE '\\')` prefix arm at `:1549-1551`;
  `session/tests/search.rs:461-476` `tag_scope_like_metachars_do_not_over_match`
  pins that `a_b`'s descendants exclude `aXb/child`. SQLite's `LIKE` also
  stops at an embedded U+0000 (probe, SQLite 3.50.4: the pattern
  `'a' || char(0) || 'b/%'` matched `a`, `a\0b` and `a\0b/c`), while a
  BINARY range comparison of bound parameters compares the whole bytes
  (`>= 'a\0b/' AND < 'a\0b0'` matched only `a\0b/c`).
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
- **The switcher's order.** `switcher.rs:241-279` the empty-query arm:
  the present recents first (each the FIRST file in input order whose
  NFC `path_key` matches; a duplicated recent repeats), then the other
  files in input order; `total` = present-recent entries plus the rest.
  `:282-288` the non-empty arm's recency map: the recents' NFC
  `path_key`s, pruned to present files, each mapped to its last index;
  `:289-337` a `BinaryHeap<RankedCandidate>` of at most `limit`
  candidates; `:383-393` `RankedCandidate::ranking_order_values`:
  descending score, then recency rank (`usize::MAX` when never opened —
  it breaks ties for a non-empty query too), then the NFC `path_key`
  (`:420-422`), then input index. Pinned by `:585`
  `recents_match_canonically_equivalent_path_spellings`, `:619`
  `ranked_path_tiebreak_orders_canonically` and `:671`
  `bounded_rank_preserves_duplicate_recents_and_stable_canonical_ties`.
- Mac callers (untouched): `QuickSwitcherModel.swift:177` `switcherRankTop(files:query:recentPaths:limit:)`;
  `AppState.swift:6754`, `:7205` `tagTree()`.
- Tests that pin the host-side list: `QuickSwitcherRescanLandingTests.cs:94`
  (`initialFiles: Files`); `FilePathsForTests` at `RescanTests.cs:1209`,
  `:1335-1345`, `:1642`, `:1747-1748`, `:1782`, `:1791`,
  `RescanTests.Round3.cs:410`, `RescanTests.Round4.cs:128`,
  `RescanTests.Watcher.cs:59`, `:99-100`, `:150`; the load tuple at
  `RescanTests.cs:2068` and `W1HardeningTests.cs:217`; switchers built
  from host lists at `SharedNameTests.cs:561-575` and
  `W1WorkspaceTests.cs:501-545` (class `W1QuickSwitcherAndChordTests` —
  the file holds no class named `W1WorkspaceTests`); the `rankTop` seam
  in `QuickSwitcherRankCoordinatorTests.cs` — all rewritten in stage 2.

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

Every node has a KEY: its segment path joined with `/` — a real tag's
key is its `tag_norm`, an implied ancestor's key the proper prefix of a
tag's `split('/')` sequence joined with `/`. Keys, never `full`s, carry
order, cursor and counts, because `full` is not a node's identity:
`materialize` trims a node's leading empty segments out of its `full`
(Current state), so the tags `/a`, `a` and `//a` give five nodes with
the keys `""`, `/`, `//a`, `/a`, `a` and the `full`s `""`, `""`, `a`,
`a`, `a`. A key is unique per node, since splitting a key on `/` gives
the node's segment path back. The page still emits `materialize`'s
`full`, through one new private helper in `tag_tree.rs`,
`fn full_of_key(key: &str) -> &str` = `key.trim_start_matches('/')`.
That helper equals `materialize`'s rule for every node: a leading run of
k empty segments is exactly k leading `/`s in the key. The pages
therefore equal `tag_tree`'s entries byte for byte, and mac's tree and
today's Windows tree do not change. Nothing structural reads `full`.

Order and cursor are SEGMENT-AWARE pre-order over keys, the order
`build_tag_tree` produces, defined by one new pure comparator in
`tag_tree.rs`:
`pub fn cmp_segments(a: &str, b: &str) -> std::cmp::Ordering` (new) —
`a.split('/').cmp(b.split('/'))`, the segment sequences compared
lexicographically, each segment in `str` (byte, code-point) order and a
sequence that is a proper prefix of another first. That is exactly
`build_tag_tree`'s `BTreeMap` pre-order over keys, because it splits with
the same `tag.split('/')` (`a`, `a/z`, `a-b`; a parent before its
descendants; the `""` root and its `/a` subtree before the root `a`),
and it is injective: two different strings never compare `Equal`,
because the split sequence gives the string back when joined with `/`.
No character is rewritten, so a tag holding U+0001, U+0000 or anything
else below `/` can neither collide with a separator nor sort across one.
(Round 5's sort key, `replace(tag_norm, '/', char(1))`, was not
injective: `a/z` and `a<U+0001>z` shared it, and the strict greater-than
resume skipped whichever of them followed a page boundary.) SQLite
cannot sort by it without a registered collation, so a page reads
`SELECT tag_norm, COUNT(DISTINCT file_id) FROM file_tags GROUP BY tag_norm`
— the distinct tags (each its own key) and their direct counts, held in
core and never marshalled; the unpaged `tag_tree` already holds every
row — under `with_sqlite_cancellation`, polling the token per row, and
sorts them with `cmp_segments`. The cursor is the last emitted entry's
KEY, opaque to the host, which passes `next_cursor` back unread. It is
never the entry's `full`: a `full` cursor would repeat or skip nodes.
Both implied `""` nodes would leave the cursor `""`, which resumes
before the implied `""` under the root, so that node would repeat on
every page. The `a` that `/a`'s leaf would leave compares `Equal` to the
root `a`, so the root `a` would be skipped. The
page starts at the first tag that compares `Greater` than the cursor
under `cmp_segments` (`partition_point`), so the ancestor stack a host
keeps across pages is never broken and no entry is skipped or repeated.
Before emitting a tag, the page emits, shallowest first, every proper
prefix of its `split('/')` sequence whose key compares `Greater` than the
last emitted key (the cursor, at the page's start) — an implied
ancestor, a prefix with no rows of its own (a prefix that is a real tag
sorts first and already came out as its own entry); an implied ancestor
counts toward `limit` like any entry, and a page may end on one. An
entry's `segment` is its key's last `split('/')` segment, its `depth`
the segment count less one, its `full` `full_of_key(key)`. Each entry's
`direct_count` is its key's group count (0 for an implied ancestor) and
its `file_count` is
`SELECT COUNT(DISTINCT file_id) FROM file_tags WHERE tag_norm = ?1 OR (tag_norm >= ?2 AND tag_norm < ?3)`
with `?1 = key`, `?2 = key + "/"` and `?3 = key + "0"` bound from
Rust (`'0'` is the byte after `'/'`, so the half-open BINARY range is
exactly the tags under `key/`; for the key `""` it is every tag that
begins with `/`, the `""` root's legacy subtree, and for `/a`'s leaf
`/a` and `/a/…`, never the root `a`'s): a byte-range predicate on
`idx_file_tags_tag` that has no metacharacters to escape — `a_b` never
counts `axb/child`, the trap `search.rs:461-476` pins for `LIKE` — and
that compares whole byte strings where `LIKE` stops at an embedded
U+0000 (so `escape_like` stays where it is). The token is polled per row
and per entry, and a cancellation is `Cancelled`, never a partial page.
`untagged_count` and `audio_summary` (the existing
`summary(tag_count, untagged)`) ride every page; `total_entries` counts
distinct tags plus implied ancestors. FFI (`lib.rs`):
`#[derive(uniffi::Record)] pub struct TagTreePage {…}` and
`pub fn tag_tree_page(&self, paging: Paging, cancel: Arc<CancelToken>) -> Result<TagTreePage, VaultError>`
beside `tag_tree`, which stays. Windows: `BuildTags` pages with the
`LoadSwitcherFiles` loop shape (a `CancelToken` cancelled from the
caller's `CancellationToken` registration; page limit 500), appending
`SidebarTagViewModel`s per page with the existing ancestor stack;
`AfterTagTreeForTests` fires per page; a cancellation before the last
page publishes nothing (`TagLoadOutcome` cancelled). The `#1290`
comment at `:1383-1387` goes. The census lands here scraping
`.TagTree(` only; stage 2 adds `SwitcherRankTop(` (a stage-1 census over
both would fail while Quick Open still calls the old entry point).

**Stage 2 — the index-backed switcher.** Core: new
`pub fn switcher_rank_top_indexed(&self, query: &str, recent_paths: &[String], limit: usize, cancel: &CancelToken) -> Result<SwitcherRankPage, VaultError>`
on `VaultSession`: one statement over `files` with the
`OpenableDocuments` where-clause, `ORDER BY path COLLATE BINARY` — the
order `list_files_impl` uses, so the stream's input order is the order
`LoadSwitcherFiles` hands the host today — the token polled per row.
The non-empty arm keeps `switcher_rank_top`'s whole comparator by
running its code, not a copy: its loop and heap (`switcher.rs:289-347`)
move, unchanged, into new
`pub(crate) fn rank_stream<F: std::borrow::Borrow<SwitcherFile>>(files: impl Iterator<Item = F>, query: &str, rank: &HashMap<String, usize>, limit: usize) -> SwitcherRankPage`,
which `switcher_rank_top` now calls over `files.iter()` (borrowed, so
mac's ranker clones no more than today) with its own pruned map, and the
indexed path over owned rows; rows are scored with `score_with_display` and retained by
`RankedCandidate` under `ranking_order_values` (`:383-393`): descending
score, then recency rank (applied to a non-empty query, not only the
empty one), then the NFC `path_key`, then input index (the row's
position in the stream). The indexed path cannot prune its recents
against the whole file set before the scan, so it passes the unpruned
map (each recent's NFC `path_key` to its last index, `switcher_rank_top`'s
duplicate rule): pruning drops absent recents without reordering the
survivors, and only present rows are ever compared, so both maps order
every pair of rows identically and the rows are equal. The session
feeds `rank_stream` through an adapter that polls the token per row and
ends the stream at the first cancellation or statement error, then
returns that error instead of the page. The empty-query arm reproduces
`switcher_rank_top`'s (`:241-279`) in one pass over the same statement:
it keeps the first row whose `path_key` matches each recent, buffers the
first `limit` non-recent rows, and returns the present recents in
recency order (a duplicated recent repeats) followed by the buffer, with
`total` the present-recent entries plus every non-recent row. The row
shape and every tie-break are pinned equal to `switcher_rank_top` over
the host's list for the same vault (differential facts, equal-score
recents and canonically equivalent paths included). FFI:
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
`SwitcherPageLoadingForTests` are deleted, and the `sessionLoadWorker`
seam's tuple loses `SwitcherFiles` (`:93-94`, `:190-191`, `:529-542`,
`:1220`); the open no longer loads the list (the §6 interlock with
WB-9/WB-28/WB-29 on `VaultLifecycleViewModel.cs` is these deletions).
The rescan stops listing too: `ReSyncQuickOpenAsync`
(`VaultLifecycleViewModel.Rescan.cs:606-648`) makes no `"list"` core
call and opens no journal; it awaits **new**
`internal Task QuickSwitcherViewModel.ReRankForRescanAsync(CancellationToken cancellation)`,
which is `ReplaceFilesAsync` (`QuickSwitcherViewModel.cs:350-399`)
without its `files` and `journal` parameters and its replay, under the
same contract — a closed switcher completes at once; an open one re-runs
its query silently, and the task completes inside the publication of
that rank or a later one, faults on its terminal failure, completes when
the switcher closes, and is cancelled with the token — because the
index the rescan's scan just committed is what the indexed rank reads.
`ReplaceFilesAsync`, `ReplacementJournal`, `BeginReplacement`,
`EndReplacement`, `JournalChange` and `_openJournals` go with the file
list they replayed onto. **The open's cancellation boundary moves to its
scan.** Without the listing, the open's load makes one core call, so the
listing seam's role — a hook on the load's worker, handed the open's
token, where a fact holds the load until the disposal has cancelled it —
moves to the scan: **new** `internal Action<CancelToken>? OpenScanStartingForTests { get; set; }`
on `VaultLifecycleViewModel` (where `SwitcherPageLoadingForTests` was,
`:1743-1745`), read once into a local before `_runSessionLoad` (as
`switcherPageHook` is at `:528`) and invoked on the load's worker after
`StartVaultWatch(activeSession)` and immediately before
`ScanInitialWithProgress(activeCancel, activeProgressListener)`, with
`activeCancel`. Production never sets it, and `CloseSession` does not
change: its cancel-then-join order (`:1028`, then `:1042`) is what the
replacement fact pins.
The census adds `SwitcherRankTop(`. Not changed: `DisplayCap`, the row
view models, recents (`FileRecentsStore`), the Quick Open journey, mac.

## Tests

Rust — unit facts in `tag_tree.rs` and `switcher.rs` `mod tests`;
session facts beside the real-vault harness in
`crates/slate-core/tests/tag_tree_exec.rs` (`write`, `open`, `flatten`)
and in `session/tests`. Stage 1:
A walk below means: request pages at the given limit from no cursor,
passing each `next_cursor` back, until it is `None`; the walk fails if it
takes more pages than `flatten_preorder` has entries, plus one (a resume
that repeats cannot loop forever). A segment path is rebuilt from each
entry's `(depth, segment)` with the host's ancestor stack.
- `cmp_segments_is_build_tag_trees_preorder_and_never_equates_two_tags`
  — over `a`, `a/`, `a//b`, `a/z`, `a\u{0}b`, `a\u{0}b/c`, `a\u{1}z`,
  `a\u{1}z/k`, `a b`, `a-b`, `a.b/c`, `a_b/child`, `axb/child`, `/a`,
  `//a`: every node's key (its rebuilt segment path joined with `/`) of
  `build_tag_tree` (implied ancestors included) sorted with
  `cmp_segments` equals `flatten_preorder`'s order; every node's `full`
  equals `full_of_key(key)` (`/a`'s leaf, `//a`'s leaf and the root `a`
  all `a`); and `cmp_segments(x, y) == Equal` only when `x == y` for
  every pair. Mutations: compare `x.replace('/', "\u{1}")` → `a/z` and
  `a\u{1}z` compare `Equal` and the fact fails; `full_of_key` returning
  the key → `/a`'s leaf reads `/a`, not `materialize`'s `a`.
- `tag_tree_page_with_a_tiny_limit_walks_every_entry_in_pre_order` — limit 1 and limit 2 over the fixture `a`, `a/z`, `a-b`, `p/q/r` (implied `p` and `p/q`), `a_b/child`, `axb/child`, plus `/a` and `//a` through frontmatter `tags:` lists (implied: the `""` root and the `""` under it): walks whose concatenated pages equal `tag_tree::flatten_preorder(&session.tag_tree()?)` entry for entry (segment, full, depth, counts) — the `full`s `""`, `""`, `a`, `a`, `a` lead, in that order, while the rebuilt segment paths are pairwise distinct; `next_cursor` None on the last; `total_entries`, `untagged_count` and `audio_summary` identical on every page. Mutations: sort the distinct tags by their bytes (lexical full tag) → `a-b` lands between `a` and `a/z` and the fact fails; cursor by `segment` → duplicates; cursor by the last entry's `full` → at limit 1 the implied `""` under the root comes back on every page until the walk exceeds its page bound, and at limit 2 it repeats while `/a`'s leaf and the root `a` are skipped; counts bound from `full` instead of the key → `/a`'s leaf counts the notes tagged `a` and `a/z` (2, not 1).
- `tag_tree_page_keeps_tags_holding_characters_below_the_separator_apart_at_every_page_boundary`
  — one note per tag, written through frontmatter `tags:` lists with
  YAML escapes: `a`, `a/z`, `"a\x01z"`, `"a\x01z/k"`, `"a\0b"`,
  `"a\0b/c"`, `a-b`, `a.b/c` (implied `a.b`), `"/a"`, `"//a"`; at limits
  1, 2 and 3 the walks' concatenated pages equal
  `flatten_preorder(&session.tag_tree()?)` entry for entry (counts
  included), every rebuilt segment path appears exactly once, and both
  `a/z` and `a\u{1}z` are present. Mutations: the round-5 sort key (sort
  and resume on `key.replace('/', "\u{1}")`) → `a/z` and `a\u{1}z`
  share it, so at limit 1 whichever of them a page ends on, the strict
  greater-than resume skips the other and an entry is missing; sort by
  the full tag's bytes → the order differs; count descendants with
  `LIKE` → `a\u{0}b`'s `file_count` counts the note tagged `a`; cursor
  by `full` → the root `a` is missing at limits 2 and 3.
- `tag_tree_page_resumes_after_a_cursor_inside_a_subtree` — over the
  tiny fixture: a cursor at `a` yields `a/z` first, not `a-b`; a cursor
  at `p/q` yields `p/q/r`; a cursor at `/a` (the key `/a`'s leaf
  leaves; its `full` is `a`) yields the root `a`; a cursor at `/` (the
  implied `""` under the root) yields `//a`'s leaf at depth 2.
- `tag_tree_page_counts_match_the_unpaged_tree` (`file_count`, `direct_count` per entry over the tiny fixture; the `""` root counts the two notes whose tags begin with `/`, and `/a`'s leaf counts only its own note).
- `tag_tree_page_like_metachars_do_not_over_match` — `a_b` has `file_count` 1 (its own child only), never counting `axb/child`. Mutation: count descendants with an unescaped `LIKE key || '/%'` → 2.
- `tag_tree_page_cancelled_mid_statement_returns_cancelled_not_a_partial_page` (the `list_files` cancellation fact's shape).
- `tag_tree_page_rejects_a_limit_over_the_cap`.

Stage 2 — the oracle is `switcher_rank_top` over the host's list for the
same vault: the `list_files(OpenableDocuments)` pages concatenated, as
`LoadSwitcherFiles` builds it today.
- `switcher_rank_top_indexed_matches_switcher_rank_top_over_the_same_files` — a 1,000-file fixture, ten queries incl. empty with recents (one absent, one duplicated): rows and `total` equal the oracle's. Mutation: break the heap's tie-break → order differs.
- `switcher_rank_top_indexed_keeps_recency_and_canonical_path_tie_breaks`
  — files `x/todo.md`, `y/todo.md`, `z/todo.md` (equal scores for
  `todo`), `f/note.md` and `e\u{301}/note.md`, plus off macOS
  (`#[cfg(not(target_os = "macos"))]`, since APFS will not hold both
  spellings) the precomposed sibling `\u{e9}/note.md`; recents
  `R = [z/todo.md, gone.md, x/todo.md, z/todo.md]` (one absent, one
  duplicated) and `R` plus `\u{e9}/note.md`; for `todo`, `note` and the
  empty query under each recents list at limits 1, 2 and 50, rows and
  `total` equal the oracle's — `todo` ranks `x`, `z`, then `y` (recency
  breaks the score tie; `z`'s last index wins); `note` ranks `f/note.md`
  before `e\u{301}/note.md` under `R` (canonical order puts `é` after
  `f`) and the decomposed file first once its precomposed recent is
  added; equal `path_key`s keep statement order. Mutations: a heap keyed
  by score then byte path → `todo` comes out `x`, `y`, `z`; recency
  dropped for a non-empty query → the same; byte `path` instead of
  `path_key` → `e\u{301}/note.md` sorts before `f/note.md` under `R`
  and is not found by its precomposed recent.
- `switcher_rank_top_indexed_marshals_at_most_limit_rows_with_the_exact_total`.
- `switcher_rank_top_indexed_cancelled_returns_cancelled`.
- `switcher_rank_top_indexed_sees_a_file_the_index_just_gained` (create, then rank, no host list).
- The existing `switcher.rs` facts (`:585`, `:619`, `:671` and
  `bounded_rank_matches_full_order_and_exact_total`) keep passing over
  `switcher_rank_top`'s delegation to `rank_stream`.

Windows:
- Stage 1: `SidebarTagFilterTests.TheTagTreeArrivesInPagesAndACancelledRefreshPublishesNothing` — a fake session paging 3 entries at a time; cancel after page one → no `Tags` change, `AfterTagTreeForTests` called once. Mutation: ignore the cancel between pages → the tree publishes.
- Stage 1: `RescanTests` `:1236`, `:1258`, `:1675` keep passing (the seam fires per page; the throw fact still throws once).
- Census, new `Censuses/BoundedFfiListCensus.cs` (class `BoundedFfiListCensus`, namespace `SlateWindows.Tests.Censuses`) with `EveryVaultListOperationTheWindowsHostCallsIsPagedOrBounded`, scraping `apps/slate-windows/src` (the `AutomationIdInventory` scrape shape): stage 1 zero `.TagTree(` call sites; stage 2 adds zero `SwitcherRankTop(` call sites. Mutation: put one call back → the census names its file and line.
- Stage 2: `QuickSwitcherViewModelTests.AKeystrokeRanksThroughCoreWithNoHostFileList` (**new** class and file, `tests/SlateWindows.Tests/QuickSwitcherViewModelTests.cs`; main has none) — the seam receives `(query, recents, token)` only; `FilePathsForTests` no longer exists (compile-time).
- Stage 2: `QuickSwitcherViewModelTests.ASupersededKeystrokeCancelsTheCoreQuery` (same new class) — the first rank's token is cancelled when the second starts. Mutation: do not link the source to the core token → fails.
- Stage 2: `QuickSwitcherRescanLandingTests.ARescansSilentReRankKeepsTheKeysOnTheReadersResult` (`:27`) rewritten without `initialFiles`, awaiting `ReRankForRescanAsync` where it called `BeginReplacement` and `ReplaceFilesAsync` (`:42-43`), same assertions.
- Stage 2: every `FilePathsForTests` site listed under Current state asserts through a rank (`Rows` contains / does not contain); `RescanTests.cs:2068` drops the tuple's file list (`RunOnWorker<ScanReport>`); `SharedNameTests.cs:561-575`, `W1WorkspaceTests.cs:501-545` (`W1QuickSwitcherAndChordTests`) and `QuickSwitcherRankCoordinatorTests.cs` build their switchers on real fixture files or the new `rankTop` seam, same assertions.
- Stage 2: `W1VaultCloseBarrierTests.ACancelledOpenLoadStopsAtItsScanAndPublishesNothing` (`W1HardeningTests.cs`) — **replaces** `ACancelledOpenLoadStopsInsideQuickOpensListingAndPublishesNothing` (`:207-246`), deleted with the listing it names; its doc comment moves with it and says the open no longer lists. Boundary: the open's scan, the load's one remaining core call. A three-note `FixtureVault`; `sessionLoadWorker: work => load = Task.Run(work)` with `load` typed `Task<ScanReport>?`; `lifecycle.OpenScanStartingForTests = cancel => { scanStarting.Set(); Assert.True(SpinWait.SpinUntil(cancel.IsCancelled, TimeSpan.FromSeconds(10)), "the disposal cancelled the open's token before it joined the load"); }`; the fact asserts `scanStarting.Wait(TimeSpan.FromSeconds(10))`, runs `lifecycle.Dispose` on the pool under a 10 s `WaitAsync`, awaits the open under 10 s, then asserts `load.IsFaulted`, `load.Exception!.InnerException` is `VaultException.Cancelled` (the scan's pre-walk check answered for the token the disposal cancelled, rather than a completed scan), and `Workspace`, `FileSidebar` and `QuickSwitcher` null. Witness: the hook, the only code that sees the open's token, observes it cancelled while the disposal is still inside `CloseSession`, and the load ends in `Cancelled`. The hook holds nothing the disposal waits on except by waiting for the disposal's own cancel, which `CloseSession` issues before it joins (`:1028` before `:1042`) — so a disposal that joined first would deadlock the hook out to its bound and fail, not pass. Mutations: hand the scan a token the disposal never cancels (`ScanInitialWithProgress(new CancelToken(), …)`) → the scan completes and `load` is not faulted; move `_scanCancel?.Cancel()` after the join at `:1042` → the hook's 10 s wait expires while the disposal waits on the load, and the load faults with the hook's assertion, not `Cancelled` (or the 10 s `Dispose` bound elapses first); invoke the hook after the scan instead of before it → the scan has completed, the load is not faulted.
- Stage 2: `RescanTests.ACloseWaitsForEveryRescanCoreCallInFlight` (`RescanTests.Round3.cs:106-145`) — **migrated**, same name and claim: the rescan makes no `"list"` call to park, so the in-flight call becomes the workspace's `"hashes"` read, parked inside its disk probe. The harness seeds `ghost.md`; the fact opens it (`h.Open("ghost.md")`), deletes the file outside Slate, and sets `h.Lifecycle.CanonicalPathForTests` (`VaultLifecycleViewModel.Rescan.cs:779`) to a probe that, on its first call for `ghost.md`, sets `parked`, waits on `release` (30 s bound) and returns `null`: the rescan's scan drops the row, so `ReadIndexedPaths` (`:746-773`) finds no hash and asks the disk through that seam (`:766`, `StoredSpelling` `:825-826`) inside the `"hashes"` core call. After `h.Context.RunUntil(() => parked.IsSet, …)`, a timer releases 800 ms later (as today) and `CloseVault()` runs; then `Assert.Single(h.CoreCallOutcomes, o => o.Operation == "hashes")` with `At <= closedAt` (the close returned only after the hash read ended), `releasedAt > 0`, a failure that is null or `VaultException.Cancelled`, no `"list"` outcome at all, and no `VaultRescanFinished` or `VaultRescanIncomplete`. The doc comment says the parked call is the hash read now that Quick Open no longer lists. Mutations: `CloseSession` without `DrainRescanCoreCalls()` (`VaultLifecycleViewModel.cs:1038`) → the close returns while the hash read is parked, `At > closedAt`; a `"list"` call left in `ReSyncQuickOpenAsync` → the no-`"list"` assertion fails.

## Docs and registers

- Contract 40: append a note under AR-63's family: "#1290 closed for the tag tree and Quick Open on Windows; mac stays on `tag_tree` / `switcher_rank_top` until its follow-up (WD17)".
- `w_c_matrix.md`: the Files tree row's notification cell gains "tag tree paged under the refresh token"; the Quick Open row's evidence cell cites the new facts.
- WB-0 files the mac follow-up ("mac: move `QuickSwitcherModel` to `switcherRankTopIndexed` and the two `tagTree()` sites to `tagTreePage`; then retire the old entry points; then decide a node identity for `full` on both hosts — a node under leading empty segments shares its `full` with a root tag (`/a`, `a` and `//a` all read `a`), and Windows' tag filter, re-selection and expansion key on it").
- Stage 1's PR body names, as not delivered, the shared `full` of leading-empty-segment nodes: the pages keep `tag_tree`'s `full` byte for byte (only the cursor and the counts use the key), because a new identity changes mac's tree too.
- No `chords.json`, designation or sentence change.

## Verification

Shared prelude (run once per PR, from the repo root):

```powershell
$ErrorActionPreference = 'Stop'
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb25'
function Assert-RustFacts([string]$Filter, [string[]]$Facts) {
  $log = cargo test -p slate-core $Filter
  if ($LASTEXITCODE -ne 0) { throw "cargo test -p slate-core $Filter failed ($LASTEXITCODE)" }
  $missing = @($Facts | Where-Object { -not ($log -match ('^test (\S+::)?' + [regex]::Escape($_) + ' \.\.\. ok$')) })
  if ($missing.Count -gt 0) { throw "not run and passed under '$Filter': $($missing -join ', ')" }
}
function Assert-DotnetFacts([string]$Project, [string]$Filter, [string[]]$Facts, [string]$Run) {
  $results = Join-Path $env:TEMP "wb25-$Run"
  if (Test-Path $results) { Remove-Item $results -Recurse -Force }
  dotnet test $Project --no-build --configuration Release --filter $Filter --logger "trx;LogFileName=$Run.trx" --results-directory $results
  if ($LASTEXITCODE -ne 0) { throw "dotnet test $Run failed ($LASTEXITCODE)" }
  [xml]$trx = Get-Content (Join-Path $results "$Run.trx")
  $passed = @($trx.TestRun.Results.UnitTestResult | Where-Object { $_.outcome -eq 'Passed' } | ForEach-Object { $_.testName })
  $missing = @($Facts | Where-Object { $fact = $_; -not ($passed | Where-Object { $_ -like "*.$fact" }) })
  if ($missing.Count -gt 0) { throw "not selected and passed in ${Run}: $($missing -join ', ')" }
}
function Assert-Gates {
  cargo fmt --all -- --check; if ($LASTEXITCODE -ne 0) { throw "cargo fmt ($LASTEXITCODE)" }
  cargo clippy --all-targets --workspace -- -D warnings; if ($LASTEXITCODE -ne 0) { throw "cargo clippy ($LASTEXITCODE)" }
  cargo test --workspace; if ($LASTEXITCODE -ne 0) { throw "cargo test --workspace ($LASTEXITCODE)" }
  cargo bench --workspace --no-run; if ($LASTEXITCODE -ne 0) { throw "cargo bench --workspace --no-run ($LASTEXITCODE)" }   # plan §4 rule 3: the bench compile
  # plan §4 rule 3: the license-header check under Git Bash, by full path (a bare `bash` in PowerShell here resolves to WSL's C:\WINDOWS\system32\bash.exe)
  & "$env:ProgramFiles\Git\bin\bash.exe" scripts/check-license-headers.sh; if ($LASTEXITCODE -ne 0) { throw "scripts/check-license-headers.sh ($LASTEXITCODE)" }
  ./apps/slate-windows/generate-bindings.ps1 -Locked   # throws on failure
  dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
  if ($LASTEXITCODE -ne 0) { throw "dotnet build failed ($LASTEXITCODE)" }
}
function Assert-Format {
  $base = git merge-base origin/main HEAD
  $files = @(git diff --name-only $base -- '*.cs') + @(git ls-files --others --exclude-standard -- '*.cs')
  if ($files.Count -eq 0) { throw 'no C# changes found' }
  dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $files
  if ($LASTEXITCODE -ne 0) { throw "dotnet format reports changes ($LASTEXITCODE)" }
}
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
```

Stage 1 PR:

```powershell
Assert-RustFacts 'tag_tree' @(
  'cmp_segments_is_build_tag_trees_preorder_and_never_equates_two_tags',
  'tag_tree_page_with_a_tiny_limit_walks_every_entry_in_pre_order',
  'tag_tree_page_keeps_tags_holding_characters_below_the_separator_apart_at_every_page_boundary',
  'tag_tree_page_resumes_after_a_cursor_inside_a_subtree',
  'tag_tree_page_counts_match_the_unpaged_tree',
  'tag_tree_page_like_metachars_do_not_over_match',
  'tag_tree_page_cancelled_mid_statement_returns_cancelled_not_a_partial_page',
  'tag_tree_page_rejects_a_limit_over_the_cap')
Assert-Gates
Assert-DotnetFacts $tests 'FullyQualifiedName~SidebarTagFilterTests|FullyQualifiedName~RescanTests|FullyQualifiedName~BoundedFfiListCensus' @(
  'TheTagTreeArrivesInPagesAndACancelledRefreshPublishesNothing',
  'EveryVaultListOperationTheWindowsHostCallsIsPagedOrBounded') 'stage1'
Assert-Format
```

Stage 2 PR:

```powershell
Assert-RustFacts 'switcher' @(
  'switcher_rank_top_indexed_matches_switcher_rank_top_over_the_same_files',
  'switcher_rank_top_indexed_keeps_recency_and_canonical_path_tie_breaks',
  'switcher_rank_top_indexed_marshals_at_most_limit_rows_with_the_exact_total',
  'switcher_rank_top_indexed_cancelled_returns_cancelled',
  'switcher_rank_top_indexed_sees_a_file_the_index_just_gained',
  'recents_match_canonically_equivalent_path_spellings',
  'ranked_path_tiebreak_orders_canonically',
  'bounded_rank_preserves_duplicate_recents_and_stable_canonical_ties',
  'bounded_rank_matches_full_order_and_exact_total')
Assert-Gates
Assert-DotnetFacts $tests 'FullyQualifiedName~SidebarTagFilterTests|FullyQualifiedName~RescanTests|FullyQualifiedName~QuickSwitcherViewModelTests|FullyQualifiedName~QuickSwitcherRescanLandingTests|FullyQualifiedName~QuickSwitcherRankCoordinatorTests|FullyQualifiedName~W1QuickSwitcherAndChordTests|FullyQualifiedName~SharedNameTests|FullyQualifiedName~SlateWindows.Tests.W1VaultCloseBarrierTests|FullyQualifiedName~CommandPaletteTests|FullyQualifiedName~BoundedFfiListCensus' @(
  'TheTagTreeArrivesInPagesAndACancelledRefreshPublishesNothing',
  'QuickSwitcherViewModelTests.AKeystrokeRanksThroughCoreWithNoHostFileList',
  'QuickSwitcherViewModelTests.ASupersededKeystrokeCancelsTheCoreQuery',
  'ARescansSilentReRankKeepsTheKeysOnTheReadersResult',
  'W1VaultCloseBarrierTests.ACancelledOpenLoadStopsAtItsScanAndPublishesNothing',
  'W1VaultCloseBarrierTests.DirectDisposalJoinsActiveSessionLoadingBeforeRelease',
  'RescanTests.ACloseWaitsForEveryRescanCoreCallInFlight',
  'EveryVaultListOperationTheWindowsHostCallsIsPagedOrBounded') 'stage2'
# desktop free, NVDA closed (partial class ShellAccessibilityTests; select by method):
Assert-DotnetFacts 'apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj' 'FullyQualifiedName~FluentShell_UiaPatternsKeyboardFocusAndAxe_AreClean' @(
  'FluentShell_UiaPatternsKeyboardFocusAndAxe_AreClean') 'stage2-flaui'
Assert-Format
```

(Cargo takes one positional filter, so each stage runs its own; `tag_tree`
selects the `tag_tree::tests` module and every `tag_tree_page_*` fact,
`switcher` the `switcher::tests` module and every
`switcher_rank_top_indexed_*` fact. The checks throw on a nonzero exit
code and on any required fact missing from the passed results; xUnit's
TRX `testName` is the fully qualified `Namespace.Class.Method`.
`W1QuickSwitcherAndChordTests` is the class in `W1WorkspaceTests.cs`; a
`W1WorkspaceTests` filter selects nothing. Likewise `W1HardeningTests.cs`
holds no class of its name (namespace `SlateWindows.Tests`), so stage 2
selects the close-barrier class itself, `SlateWindows.Tests.W1VaultCloseBarrierTests`
— the replacement fact and the class's other facts over the open's load
— and requires the replacement and the migrated
`RescanTests.ACloseWaitsForEveryRescanCoreCallInFlight` by
class-qualified name, so a fact left behind under its old name, or
dropped instead of replaced, fails the check. `Assert-Format` passes every
C# file the branch changed or added since its merge base, new test files
included.)

CI: `rust.yml`, the Windows lane (both model shards and the FlaUI gate
for the Quick Open journey), `swift-tests.yml` for the compile. AT:
none — nothing a reader hears changes; if a Quick Open timing changes
what NVDA reads mid-typing, §4 rule 4 applies and the PR body records a
spot check.

## Acceptance

- [ ] Stage 1 merged on its own PR (`Refs #1290`), stage 2 on a second (`Closes #1290`), each with its facts failing first and mutations run (the delimiter-collision, `full`-cursor, `full`-bound-count and tie-break mutations among them).
- [ ] Stage 1's pages equal `tag_tree`'s entries byte for byte over a fixture holding `/a`, `a` and `//a`; the cursor and the descendant counts are bound from the node's key, never its `full`.
- [ ] After stage 1 `apps/slate-windows/src` has no `.TagTree(` call, and after stage 2 no `SwitcherRankTop(` call; the census pins each as its stage lands.
- [ ] Stage 2 deletes `SwitcherPageLoadingForTests` and `LoadSwitcherFiles` with no stranded user: the rescan's `ReSyncQuickOpenAsync` lists nothing and awaits `ReRankForRescanAsync`; `W1VaultCloseBarrierTests.ACancelledOpenLoadStopsAtItsScanAndPublishesNothing` replaces the listing fact (boundary: the open's scan; witness: new `OpenScanStartingForTests` seeing the open's token cancelled before the scan, and the load ending `Cancelled` with nothing published); `RescanTests.ACloseWaitsForEveryRescanCoreCallInFlight` parks the hash read in place of the listing; both are required by class-qualified name in the stage-2 TRX check, which selects the class `SlateWindows.Tests.W1VaultCloseBarrierTests` (never the file name `W1HardeningTests`), and each is killed by its mutations.
- [ ] Both bindings regenerated per PR; the Swift lane compiles; mac source untouched.
- [ ] Contract 40 note and `w_c_matrix.md` cells updated; the mac follow-up filed by WB-0 and named in the PR body.
- [ ] Not delivered, named: the mac migration and the retirement of `tag_tree` / `switcher_rank_top` (WD17); a node identity for `full` (a leading-empty-segment node shares its `full` with a root tag on both hosts; recorded in WB-0's mac follow-up).

## Brief

Read `docs/plans/05_locked_architecture_decisions.md:1554-1556`;
`crates/slate-uniffi/src/lib.rs:650-674`, `:718-740`, `:845-862`,
`:900-920`, `:2505-2515`, `:2725-2735`, `:8775-8862`;
`crates/slate-core/src/session.rs:6313-6325`, `:8493-8510`,
`:11868-11931`; `crates/slate-core/src/tag_tree.rs` whole;
`crates/slate-core/src/tags_db.rs:258-271`;
`crates/slate-core/src/frontmatter.rs:758-815`;
`crates/slate-core/migrations/019_file_tags.sql:31-37`; `Cargo.toml:28`;
`crates/slate-core/src/switcher.rs:98-131`, `:232-425`, `:575-686`;
`crates/slate-core/tests/tag_tree_exec.rs:1-80`;
`crates/slate-core/src/bases/engine.rs:1455-1465`, `:1545-1552`;
`crates/slate-core/src/session/tests/search.rs:455-480`;
`apps/slate-windows/src/SlateWindows/FilesSidebarViewModel.cs:1368-1420`,
`:1085-1095`, `:1475-1540` (the filter, re-selection and expansion keyed
on `Full`); `QuickSwitcherViewModel.cs:20-75`, `:240-320`, `:360-400`, `:460-500`;
`VaultLifecycleViewModel.cs:90-95`, `:185-240`, `:525-545`, `:1020-1060`
(`CloseSession`: the open's token cancelled at `:1028`, the rescan's
calls drained at `:1038`, the load joined at `:1042`), `:1215-1225`,
`:1740-1770`; `VaultLifecycleViewModel.Rescan.cs:598-648`
(`ReSyncQuickOpenAsync`, the rescan's `"list"` call at `:619-622`) and
`:740-830` (`ReadIndexedPaths`, `CanonicalPathForTests` at `:779`);
`QuickSwitcherViewModel.cs:40-50`, `:320-399` (the replacement journal
and `ReplaceFilesAsync`); `crates/slate-core/src/session.rs:10325-10335`
(the scan's pre-walk token check); `apps/slate-windows/tests/SlateWindows.Tests/W1HardeningTests.cs:169-246`
(class `W1VaultCloseBarrierTests`; the listing fact at `:207-246`);
`RescanTests.Round3.cs:100-146` (the close-drain fact);
`RescanTests.cs:1200-1260`, `:1330-1350`, `:1640`,
`:1670-1680`, `:2395-2410` (`Harness.Open`); `QuickSwitcherRescanLandingTests.cs:27-100`;
`CONTRIBUTING.md:76-98`. Decision WD17 is pending (recommended: keep
`tag_tree` and `switcher_rank_top` for mac, add the siblings, WB-0 files
the mac follow-up; under "retire now" the mac call sites
`QuickSwitcherModel.swift:177`, `AppState.swift:6754` and `:7205` move
in the same PR and the Swift lane runs its tests). Stage 1 (its own PR,
`Refs #1290`): in `tag_tree.rs` add `TagTreePageEntry`, `TagTreePage`,
`flatten_preorder(&TagTree) -> Vec<TagTreePageEntry>` (and make `lib.rs`'s
`flatten_tag_tree` use it), the new comparator
`pub fn cmp_segments(a: &str, b: &str) -> std::cmp::Ordering` =
`a.split('/').cmp(b.split('/'))` — `build_tag_tree`'s pre-order over
node keys, injective for any string (tags may hold U+0000, U+0001 or
anything below `/`: `normalize_tag` forbids nothing and frontmatter YAML
escapes reach the index) — and the new private
`fn full_of_key(key: &str) -> &str` = `key.trim_start_matches('/')`. A
node's KEY is its segment path joined with `/` (a real tag's key is its
`tag_norm`; an implied ancestor's, the joined proper prefix); its `full`
is NOT its identity, because `materialize` (`tag_tree.rs:56-61`) drops
leading empty segments from `full`: `normalize_tag` keeps a leading `/`,
so the tags `/a`, `a` and `//a` give the keys `""`, `/`, `//a`, `/a`, `a`
and the `full`s `""`, `""`, `a`, `a`, `a`, and `full_of_key` reproduces
those `full`s for every node. In `session.rs` add
`tag_tree_page(&self, paging: Paging, cancel: &CancelToken) -> Result<TagTreePage, VaultError>`:
read `SELECT tag_norm, COUNT(DISTINCT file_id) FROM file_tags GROUP BY tag_norm`
under `with_sqlite_cancellation` (token per row), sort the keys with
`cmp_segments` (rusqlite has no collation feature; never a
rewritten-string sort key such as `replace(tag_norm, '/', char(1))`,
which collides `a/z` with `a<U+0001>z`), start after the cursor — the
last emitted entry's KEY, opaque to the host, never its `full` (a `full`
cursor `""` re-emits the implied `""` under the root on every page, and
a `full` cursor `a` left by `/a`'s leaf skips the root `a`) — at the
first tag `Greater` under `cmp_segments`, emit before each tag its
proper prefixes whose keys compare `Greater` than the last emitted key
(implied ancestors, counting toward `limit`), give each entry the
segment, depth and `full_of_key(key)` of its key and its key's group
count as `direct_count`, and count descendants with
`tag_norm = ?1 OR (tag_norm >= ?2 AND tag_norm < ?3)` bound as `key`,
`key + "/"`, `key + "0"` (no `LIKE`: no metacharacters, and `LIKE`
stops at U+0000; for the key `""` the range is every tag beginning with
`/`, the `""` root's legacy subtree); the pages then equal `tag_tree`'s
entries byte for byte, so mac's tree and today's Windows tree are
unchanged; export
`tag_tree_page(&self, paging: Paging, cancel: Arc<CancelToken>) -> Result<TagTreePage, VaultError>`
beside `tag_tree`; regenerate both bindings; page `BuildTags` in 500-row
pages under a `CancelToken` cancelled from the caller's token,
`AfterTagTreeForTests` per page, nothing published on cancellation; add
the census scraping `.TagTree(`. Stage 1 facts, failing first (a walk
requests pages from no cursor, passing each `next_cursor` back until
`None`, and fails past one page more than `flatten_preorder` has
entries; segment paths are rebuilt from each entry's depth and segment
with an ancestor stack):
`cmp_segments_is_build_tag_trees_preorder_and_never_equates_two_tags`
(over tags with U+0000, U+0001, `-`, `.`, space, empty segments, `/a`
and `//a`, sorting every node's key with `cmp_segments` equals
`flatten_preorder`, every `full` equals `full_of_key(key)`, and only
equal strings compare `Equal`),
`tag_tree_page_with_a_tiny_limit_walks_every_entry_in_pre_order`
(fixture `a`, `a/z`, `a-b`, `p/q/r`, `a_b/child`, `axb/child`, and the
frontmatter tags `/a` and `//a`; walks at limits 1 and 2 concatenated
equal `flatten_preorder` entry for entry, the `full`s `""`, `""`, `a`,
`a`, `a` leading while the rebuilt segment paths are pairwise distinct),
`tag_tree_page_keeps_tags_holding_characters_below_the_separator_apart_at_every_page_boundary`
(frontmatter tags `a`, `a/z`, `"a\x01z"`, `"a\x01z/k"`, `"a\0b"`,
`"a\0b/c"`, `a-b`, `a.b/c`, `"/a"`, `"//a"`; walks at limits 1, 2 and 3
concatenated equal `flatten_preorder` with counts, each rebuilt segment
path once, `a/z` and `a\u{1}z` both present),
`tag_tree_page_resumes_after_a_cursor_inside_a_subtree` (cursor `a` →
`a/z`; cursor `p/q` → `p/q/r`; cursor `/a` → the root `a`; cursor `/` →
`//a`'s leaf at depth 2),
`tag_tree_page_counts_match_the_unpaged_tree` (the `""` root counts the
two notes whose tags begin with `/`, `/a`'s leaf only its own),
`tag_tree_page_like_metachars_do_not_over_match` (`a_b` counts 1),
`tag_tree_page_cancelled_mid_statement_returns_cancelled_not_a_partial_page`,
`tag_tree_page_rejects_a_limit_over_the_cap`;
`SidebarTagFilterTests.TheTagTreeArrivesInPagesAndACancelledRefreshPublishesNothing`
(cancel after page one: no `Tags` change, the seam called once);
`BoundedFfiListCensus.EveryVaultListOperationTheWindowsHostCallsIsPagedOrBounded`
(zero `.TagTree(` under `apps/slate-windows/src`); `RescanTests.cs:1236`,
`:1258`, `:1675` still green. Stage 2 (second PR, `Closes #1290`): in
`switcher.rs` move the non-empty arm's loop and heap (`:289-347`)
unchanged into new
`pub(crate) fn rank_stream<F: std::borrow::Borrow<SwitcherFile>>(files: impl Iterator<Item = F>, query: &str, rank: &HashMap<String, usize>, limit: usize) -> SwitcherRankPage`
and have `switcher_rank_top` call it over `files.iter()` (borrowed, no
extra clones on mac's path) — one comparator,
`RankedCandidate::ranking_order_values`: descending score, then recency
rank (non-empty queries too), then NFC `path_key`, then input index —
and in `session.rs` add
`switcher_rank_top_indexed(&self, query: &str, recent_paths: &[String], limit: usize, cancel: &CancelToken) -> Result<SwitcherRankPage, VaultError>`:
one statement over `files` with the `OpenableDocuments` where-clause and
`ORDER BY path COLLATE BINARY` (today's host-list order), fed to
`rank_stream` through an adapter that polls the token per row and turns
a cancellation or statement error into the call's error, with the
unpruned recency map (NFC `path_key` → last index; pruning never
reorders present recents, so the rows are identical), and an empty-query
arm that keeps the first row per recent, buffers the first `limit`
others, and returns recents (duplicates repeated) then the buffer with
the exact `total`; export it beside `switcher_rank_top`; in
`QuickSwitcherViewModel` drop `_files`, `initialFiles`, `Applied`,
`FilePathsForTests` and the file-list half of the replacement journal,
make the `_rankTop` seam `Func<string, string[], CancelToken, SwitcherRankPage>`
defaulting to `_session.SwitcherRankTopIndexed(query, recents, DisplayCap, cancel)`,
turn `ApplyFileChange` / `ApplyRecordsAsync` and the rescan replay into
"re-rank while open", link the keystroke `CancellationTokenSource` to
the core token; delete `LoadSwitcherFiles`, `SwitcherPageLoadingForTests`
and the `SwitcherFiles` half of the `sessionLoadWorker` tuple
(`VaultLifecycleViewModel.cs:93-94`, `:190-191`, `:529-542`, `:1220`);
make the rescan list nothing — `ReSyncQuickOpenAsync`
(`VaultLifecycleViewModel.Rescan.cs:606-648`) drops its `"list"` core
call and its journal and awaits new
`internal Task QuickSwitcherViewModel.ReRankForRescanAsync(CancellationToken cancellation)`
(`ReplaceFilesAsync`, `QuickSwitcherViewModel.cs:350-399`, without the
`files` and `journal` parameters and the replay, same contract: a closed
switcher completes at once, an open one re-ranks silently and completes
inside that publication or a later one, faults on its terminal failure,
completes on close, cancels with the token), and `ReplaceFilesAsync`,
`ReplacementJournal`, `BeginReplacement`, `EndReplacement`,
`JournalChange` and `_openJournals` go; and move the open's test seam to
its one remaining core call — new
`internal Action<CancelToken>? OpenScanStartingForTests { get; set; }`
on `VaultLifecycleViewModel` (where `SwitcherPageLoadingForTests` was),
read into a local before `_runSessionLoad` like `switcherPageHook`
(`:528`) and invoked on the load's worker after
`StartVaultWatch(activeSession)` and immediately before
`ScanInitialWithProgress(activeCancel, activeProgressListener)`, with
`activeCancel` (production never sets it; `CloseSession` keeps cancelling
that token at `:1028` before it joins the load at `:1042`, and core's
scan answers `Cancelled` before walking, `session.rs:10331-10333`);
the census adds `SwitcherRankTop(`. Stage 2 facts, failing first, each
against the oracle `switcher_rank_top` over the `list_files(OpenableDocuments)`
pages for the same vault: `switcher_rank_top_indexed_matches_switcher_rank_top_over_the_same_files`
(1,000 files, ten queries incl. empty with recents, one absent and one
duplicated: rows and `total` equal),
`switcher_rank_top_indexed_keeps_recency_and_canonical_path_tie_breaks`
(equal-score `x/`, `y/`, `z/todo.md` with recents
`R = [z/todo.md, gone.md, x/todo.md, z/todo.md]` rank `x`, `z`, `y`;
`note` ranks `f/note.md` before `e\u{301}/note.md` under `R` and the
decomposed file first once `R` adds the recent `\u{e9}/note.md`; off
macOS a precomposed `\u{e9}/note.md` sibling keeps statement order; for
`todo`, `note` and the empty query under both recents lists at limits
1, 2 and 50, rows and `total` equal the oracle's),
`switcher_rank_top_indexed_marshals_at_most_limit_rows_with_the_exact_total`,
`switcher_rank_top_indexed_cancelled_returns_cancelled`,
`switcher_rank_top_indexed_sees_a_file_the_index_just_gained`; the
existing `switcher.rs` facts still green;
`QuickSwitcherViewModelTests.AKeystrokeRanksThroughCoreWithNoHostFileList`
(new class and file `tests/SlateWindows.Tests/QuickSwitcherViewModelTests.cs`;
the seam receives `(query, recents, token)` only),
`QuickSwitcherViewModelTests.ASupersededKeystrokeCancelsTheCoreQuery`
(the first rank's token cancelled when the second starts),
`QuickSwitcherRescanLandingTests.ARescansSilentReRankKeepsTheKeysOnTheReadersResult`
rewritten without `initialFiles`, awaiting `ReRankForRescanAsync` where
it called `BeginReplacement`/`ReplaceFilesAsync` (`:42-43`); every
`FilePathsForTests` site
(`RescanTests.cs:1209`, `:1335-1345`, `:1642`, `:1747-1748`, `:1782`,
`:1791`; `RescanTests.Round3.cs:410`; `RescanTests.Round4.cs:128`;
`RescanTests.Watcher.cs:59`, `:99-100`, `:150`) asserting through a rank;
`RescanTests.cs:2068` without the tuple's file list (`RunOnWorker<ScanReport>`);
`W1VaultCloseBarrierTests.ACancelledOpenLoadStopsAtItsScanAndPublishesNothing`,
replacing `ACancelledOpenLoadStopsInsideQuickOpensListingAndPublishesNothing`
(`W1HardeningTests.cs:207-246`, deleted with the listing; the boundary
is now the open's scan): `load` typed `Task<ScanReport>?` from
`sessionLoadWorker: work => load = Task.Run(work)`, the hook
`OpenScanStartingForTests = cancel => { scanStarting.Set(); Assert.True(SpinWait.SpinUntil(cancel.IsCancelled, TimeSpan.FromSeconds(10)), …); }`,
`scanStarting` waited (10 s, asserted), `lifecycle.Dispose` on the pool
under 10 s, the open awaited under 10 s, then `load.IsFaulted` with a
`VaultException.Cancelled` inner exception and `Workspace`,
`FileSidebar`, `QuickSwitcher` null — the hook releases nothing the
disposal waits on except by awaiting the disposal's own cancel, which
`CloseSession` issues before its join; and
`RescanTests.ACloseWaitsForEveryRescanCoreCallInFlight`
(`RescanTests.Round3.cs:106-145`) migrated, same name and claim, with
the workspace's `"hashes"` read parked in place of the listing: a tab on
the harness's `ghost.md`, the file deleted outside Slate, and
`h.Lifecycle.CanonicalPathForTests` parking its first `ghost.md` probe
(inside the `"hashes"` call, since the rescan's scan dropped the row) on
a `release` a timer sets 800 ms later; `CloseVault()` then returns only
after that read ended (`Assert.Single` `"hashes"` outcome with
`At <= closedAt`, failure null or `Cancelled`), with no `"list"` outcome
and no `VaultRescanFinished`/`VaultRescanIncomplete`; `SharedNameTests.cs:561-575`, `W1WorkspaceTests.cs:501-545`
(class `W1QuickSwitcherAndChordTests`) and
`QuickSwitcherRankCoordinatorTests.cs` on real fixture files or the new
seam; and the census (zero `.TagTree(` and `SwitcherRankTop(` under
`apps/slate-windows/src`). Mutations to run locally: sort tags by their
bytes; cursor by segment; cursor by the last entry's `full` (the implied
`""` under the root repeats until the page bound, and the root `a` is
skipped); counts bound from `full` (`/a`'s leaf counts 2); `full_of_key`
returning the key; the round-5 `replace('/', U+0001)` sort key; count
descendants with `LIKE` (escaped: U+0000 over-counts; unescaped: `a_b`
counts 2); a heap keyed by score then byte path; recency dropped for a
non-empty query; byte path instead of `path_key`; ignore the cancel
between pages; unlink the keystroke source from the core token; put a
`.TagTree(` or `SwitcherRankTop(` call back; hand the open's scan a
token the disposal never cancels (the load completes, not `Cancelled`);
move `CloseSession`'s `_scanCancel?.Cancel()` after its join of the load
(the hook's wait expires and the load faults with its assertion, or the
`Dispose` bound elapses); invoke `OpenScanStartingForTests` after the
scan (the scan completes first); drop `DrainRescanCoreCalls()` from
`CloseSession` (the close returns while the hash read is parked); leave
a `"list"` call in `ReSyncQuickOpenAsync` (the migrated rescan fact sees
it). Registers: a note under
AR-63's family in `docs/plans/40_nvda_matrix_remediation_contracts.md`;
the Files tree and Quick Open cells in `docs/plans/18_windows_port/w_c_matrix.md`;
the mac follow-up WB-0 files, named in both PR bodies, whose text also
asks for a node identity for `full` on both hosts. Gates, in
PowerShell with every native command's `$LASTEXITCODE` checked: per
stage `cargo test -p slate-core tag_tree` (stage 1) or `switcher`
(stage 2) with each named Rust fact required as `... ok` in the output;
`cargo fmt --all -- --check`, `cargo clippy --all-targets --workspace
-- -D warnings`, `cargo test --workspace`, `cargo bench --workspace
--no-run` (the bench compile, plan §4 rule 3) and the license-header
check `& "$env:ProgramFiles\Git\bin\bash.exe" scripts/check-license-headers.sh`
(Git Bash by full path — a bare `bash` in PowerShell here resolves to
WSL), each exiting 0; `generate-bindings.ps1 -Locked`; `dotnet build`; `dotnet test` by real class names
(`SidebarTagFilterTests`, `RescanTests`, `BoundedFfiListCensus`, and in
stage 2 also `QuickSwitcherViewModelTests`, `QuickSwitcherRescanLandingTests`,
`QuickSwitcherRankCoordinatorTests`, `W1QuickSwitcherAndChordTests`,
`SharedNameTests`, `SlateWindows.Tests.W1VaultCloseBarrierTests` — the
close-barrier class in `W1HardeningTests.cs`, which holds no class of
the file's name, so a `W1HardeningTests` filter selects nothing —
`CommandPaletteTests`) with a TRX log and each named fact required among
the passed results, in stage 2 including
`W1VaultCloseBarrierTests.ACancelledOpenLoadStopsAtItsScanAndPublishesNothing`,
`W1VaultCloseBarrierTests.DirectDisposalJoinsActiveSessionLoadingBeforeRelease`
and `RescanTests.ACloseWaitsForEveryRescanCoreCallInFlight` by
class-qualified name; in stage 2
the FlaUI `FluentShell_UiaPatternsKeyboardFocusAndAxe_AreClean` run
(desktop free, NVDA closed); `dotnet format --verify-no-changes
--include` over every C# file changed or added since the merge base
with `origin/main`. Done when both PRs pass those, `rust.yml`, the
Windows lane and the Swift compile are green, and mac source is
untouched; no AT pass (nothing a reader hears changes). Not delivered:
the mac migration and the retirement of the old entry points (WD17); a
node identity for `full` — a node under leading empty segments keeps
sharing its `full` with a root tag (`/a`, `a` and `//a` all read `a`,
and Windows' filter, re-selection and expansion key on `Full`), byte for
byte as `tag_tree` gives it, since a new identity changes mac's tree too
(named in stage 1's PR body).
Commits: `feat(core,windows): the tag tree arrives in cancellable
pages`, then `feat(core,windows): quick open ranks against the index
and the host holds no file list`.
