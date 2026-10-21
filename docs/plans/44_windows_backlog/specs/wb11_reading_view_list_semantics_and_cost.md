# WB-11 — The reading view renders the authored list marker, its list peers cost nothing per keystroke, and Ctrl+A is heard

Issues: Closes #1422, Closes #1329, Closes #1330. Size M. Lanes: windows + FlaUI (shell gate). AT: NVDA timing pass (the #1329 recipe), Narrator (the #1330 recipe). Wave: W4. Depends on: nothing (first on `Reading/ReadingSurface.cs` and `ReadingDocumentBuilder.cs`; WB-20 and WB-21b follow, §6). Parallel with: WB-10a/WB-10, WB-12. Decision: none.

## Goal

Three reading-view defects on one surface, in the order #1422 → #1329 →
#1330 because the second measures what the first builds. An ordered list
authored `3. 4. 5.` renders and reads as `1. 2. 3.` because core's authored
`list_marker` is never read; a 2,000-block note takes NVDA about 5 s to
speak each line because every UIA query re-mints every list-item peer and
walks the list from its start; and Narrator says nothing on Ctrl+A because
the selection change never reaches it. After this chunk the marker is the
author's (as mac reads it, "4. Fourth"), list-item peers are cached with
indexed positions and lazy names, a timed cross-process probe bounds the
cost per caret move, and Ctrl+A raises `TextSelectionChanged` through one
cached text provider so Narrator announces the selection and reads it back.

## Current state on main

Verified in the worktree. Drift from the reports: the marker pins
`report_a11y.md` placed at `ReadingViewTests.cs:253` are at `:2359` and
`:2364` (`NestedListsKeepTheirAuthoredDepthAndMarkers`); the peer lines
hold.

- **#1422:** `ReadingDocumentBuilder.cs:252-256` decides "marker change
  at this level" by `stack[depth].List.MarkerStyle == TextMarkerStyle.Decimal`
  against `kind.Ordered`; `:258-263` creates `new WpfList { MarkerStyle =
  kind.Ordered ? Decimal : Disc }` with no `StartIndex`, then
  `ReadingSemantics.MarkList(list)` (`ReadingSemantics.cs:82`). Nothing
  under `apps/slate-windows/src` reads `ReadingBlockInlines.ListMarker`
  (grep: zero hits); the binding carries it (`lib.rs:7919-7927`) from core
  `reading.rs:793` (`list_marker: Option<String>`, "Carried here so both
  hosts read one value"; the strip-and-report fact `:3843-3849` pins `"3."`
  and `"-"`). Pins: `ReadingViewTests.cs:2359` outer `Disc`, `:2364` sublist
  `Decimal`.
- **#1329:** `ReadingSurface.cs:949` `ReadingSurfacePeer`; `:962-971`
  `GetPattern` returns `new HeadingStyleTextProvider(text)` on every call;
  `:973-990` `GetChildrenCore` walks every block of the `FlowDocument`
  through `AppendStructural` (`:992-1020`) and formats two `CensusDiag`
  strings per call (`CensusDiag` at `:732`). `:1131` `ReadingListPeer`:
  `GetNameCore` counts entries (`:1143-1144`); `GetChildrenCore`
  (`:1150-1167`) allocates `new ReadingListItemPeer(item)` for every item on
  every call. `:1170` `ReadingListItemPeer`: `GetNameCore` builds a
  `TextRange` per call (`:1182-1183`); `GetPositionInSetCore` walks
  `list.ListItems` from the start (`:1185-1195`); `GetSizeOfSetCore`
  (`:1197-1198`). Both the list's name and `GetSizeOfSetCore` read
  `ListItems.Count`, which WPF computes by walking the collection
  (measured 2026-10-21 on this machine with a scratch WPF probe: 2,000
  `Count` reads take 88 ms on a 1,000-item list, 176 ms on 2,000, 354 ms on
  4,000). Both call sites of `InvalidateAutomationChildren()` (`:696`) —
  the merge `:439` and the streamed append `Model_BlocksAppended` `:366` —
  change the document's lists. Budget `ReadingContentViewModel.cs:52`
  `MaximumRenderedBlocks = 2_000`. Precedent for a weak peer registry:
  `Canvas/CanvasRendererView.cs:525-527` (`Dictionary<CanvasPeerKey,
  WeakReference<CanvasCardAutomationPeer>>`) with `PeerFor` (`:568-595`).
- **#1330:** `ReadingSurface.cs:30` is a read-only `RichTextBox`;
  `HeadingStyleText.cs:39` `HeadingStyleTextProvider : ITextProvider` (the
  Text pattern only; no `ITextProvider2`), `:63-67` wraps `_inner`, `:74`
  `GetSelection() => WrapAll(_inner.GetSelection())`. The only automation
  event the surface raises itself is `StructureChanged` after a merge
  (`:712-719`); nothing raises `TextPatternOnTextSelectionChanged`, and
  whether WPF's own `TextBoxBase` path raises it for this peer is the first
  thing the probe settles (issue: "not checked yet").
- Existing cost facts: `ReadingViewTests.cs:681`
  `SyntheticAttributeWalkSurvivesHugeDocuments`, `:730`
  `HugeDocumentSyntheticQueriesHaveBoundedCost` (allocation-bounded, the
  #1320 shape). FlaUI: `ShellAccessibilityTests.cs:1381`
  `ReadingIdentityContract_ExeWindowAndSurfaceIds_AreStable`, `:1720`
  `ReadingTextPattern_RangeFromChildResolvesEveryCustomPeer`,
  `ShellAccessibilityTests.ReadingFocus.cs:27` `ReadingView_IsTheEditorStop`.
- Register: `w_c_matrix.md:20` (Reading view row) records "list Name is
  its entry count and list items publish their current
  PositionInSet/SizeOfSet" and mac's shape "an ordered item reads its
  authored ordinal with its text". `parity_matrix.md` has no list-marker
  row.

## Reference

- Core `reading.rs:785-793`: the authored marker is carried so neither
  host re-derives or renumbers it (#967, decision 4). Mac
  `Reading/ReadingView.swift:702` renders `ordered ? (inline.listMarker ??
  "1.") : "•"`; since plan 43 PR 16 (#1048) VoiceOver reads it with the
  item ("4. Fourth group (2 of 3)", `w_c_matrix.md:20`).
- #1329 and #1330 are Windows-only surfaces; the issues' text (recipes,
  timings, "facts wanted") is the behavioural spec. The NVDA log recipe is
  `18_windows_port/reports/nvda_agent_matrix_pass_2026-09-22.md`.

## Design

**#1422 — authored markers.** In `AppendListItem`: an ordered list is
`MarkerStyle = TextMarkerStyle.None`, and each ordered item's first
paragraph begins with a `Run` of `inline.ListMarker ?? "1."` plus a space,
marked `ReadingSemantics.MarkListMarker(run)` (new; `ReadingSemantics.cs`
beside `MarkListItem :85`) so later code can tell it from content. The
"marker change" test at `:252-256` keys on a new `Ordered` bool kept in the
stack tuple, not on `MarkerStyle`. Unordered lists keep `Disc` (mac shows
`•`; NVDA/Narrator speak "bullet" from WPF's marker). `ReadingListItemPeer.GetNameCore`
(`:1182`) keeps `new TextRange(ContentStart, ContentEnd).Text.Trim()`, which
now begins with the authored marker — the mac shape. Not changed: the
List/ListItem peers, `PositionInSet`/`SizeOfSet`, task-item checkboxes.

**#1329 — measure, then cache.** First the timed probe (Tests), seen
failing against the 5 s/line baseline with the screen reader's query
workload. Then: (1) `ReadingListPeer` keeps a
`ConditionalWeakTable<ListItem, ReadingListItemPeer>` and returns the same
peer per item across calls, and a `ConditionalWeakTable<WpfList,
ReadingListPeer>` for the nested list peers it parents (`:1158-1162`
mints them per call today, which would drop their tables on every call);
(2) every read of `_list.ListItems` in the list peers goes through one
new `ReadingListPeer.Snapshot()` that builds, once per structural
generation, the item array, a `Dictionary<ListItem,int>` index and the
count, and increments `WalkProbeForTests`; `ReadingListItemPeer.GetPositionInSetCore`,
`GetSizeOfSetCore` and `ReadingListPeer.GetNameCore` read that snapshot and
never `ListItems.Count` per query — WPF computes `ListItems.Count` by
walking the collection (measured 2026-10-21 on this machine with a scratch
WPF probe: 2,000 reads of `Count` take 88 ms on a 1,000-item list, 176 ms
on 2,000 and 354 ms on 4,000), so a per-query count check would keep every
query linear. The snapshot is
dropped only by `InvalidateStructuralPeers()` (6): the document changes
only by a merge or a streamed append, and both call it; (3) `GetNameCore`
caches the trimmed text per peer and clears it in
`InvalidateStructuralPeers()`; (4) `ReadingSurfacePeer.GetChildrenCore`
caches its structural list per structural generation and drops it in
`InvalidateStructuralPeers()` before `ResetChildrenCache`;
(5) `CensusDiag` formatting is gated: `ReadingSurface.CensusDiagEnabled`
(from the existing switch `:732` reads) is checked before the
interpolated string is built. (6) One explicit invalidation entry:
`internal void ReadingSurface.InvalidateStructuralPeers()` (new) bumps the
structural generation (dropping the surface peer's cached children list,
every list peer's snapshot and the cached names) and runs today's
`InvalidateAutomationChildren()` reset (`:696-719`); it replaces both of
today's `InvalidateAutomationChildren()` call sites — the merge
(`ApplyBuiltDocument`, `:439`) and the streamed append
(`Model_BlocksAppended`, `:366`, which can append items to a list opened
in an earlier chunk, `ReadingDocumentBuilder.cs:228-231`). WB-20's in-place
diagram completion edits Runs inside one top-level diagram paragraph and
keeps its element: none of these caches holds that paragraph's text
(diagram blocks are never inside a list — `ReadingDocumentBuilder.cs:213-215`
clears the list stack and adds every non-list block to `document.Blocks`),
so it does not call this entry unless the paragraph is not a direct child
of the document. Not changed: the hierarchy itself (W3-1's measured reason
for it, `:945-948`).

**#1330 — one provider, one event.** `ReadingSurfacePeer` holds one
`HeadingStyleTextProvider` per inner provider (`GetPattern` `:966-969`
mints it once and returns the same object), so an out-of-process client's
provider identity is stable. `ReadingSurface` subscribes to its own
`SelectionChanged` and, when a peer exists,
`peer.RaiseAutomationEvent(AutomationEvents.TextPatternOnTextSelectionChanged)`
— only if the cross-process probe shows WPF does not already raise it
(then the fact documents which half fixed Narrator). `GetSelection`
(`HeadingStyleText.cs:74`) stays a wrap; the probe asserts its text.

No owner decision is consumed.

## Tests

Facts fail first.

- `ReadingViewTests.AnOrderedListRendersAndReadsItsAuthoredMarkers` —
  `3. 4. 5.` → items named "3. Third", "4. Fourth", "5. Fifth";
  `1. 1. 1.` keeps three "1."s; `12)` keeps its `)`; `MarkerStyle` is
  `None`; `PositionInSet`/`SizeOfSet` unchanged. Mutation: `StartIndex`
  from the first item instead → the `1. 1. 1.` and `)` cases fail.
- `NestedListsKeepTheirAuthoredDepthAndMarkers` (`:2330-2365`): the
  `Decimal` pin at `:2364` becomes `None` plus a marker-run assertion.
- `ReadingViewTests.ListItemPeersAreStableAcrossQueries` — two
  `GetChildren()` calls on a list peer return reference-equal item peers.
  Mutation: `new` per call → fails.
- `ReadingViewTests.PositionInSetOnTheLastOf2000ItemsIsIndexed` — the
  2,000-item fixture: 1,000 `GetPositionInSet` and 1,000 `GetSizeOfSet`
  queries on the last item and 1,000 `GetName` queries on the list peer
  allocate under a bound in the `HugeDocumentSyntheticQueriesHaveBoundedCost`
  style (`GC.GetAllocatedBytesForCurrentThread`), `WalkProbeForTests` stays
  at one, and the 3,000 queries take at most 3× the same queries on a
  20-item list (floor 20 ms; a ratio, so it stays in the lane per WB-7).
  Mutations: restore the linear `PositionInSet` walk → the counter climbs;
  `GetSizeOfSetCore` or `ReadingListPeer.GetNameCore` back to
  `ListItems.Count` → the ratio fails (each `Count` walks 2,000 items:
  ~88 ms per 1,000 reads).
- `ReadingViewTests.AListThatGrowsInALaterChunkReindexes` — the
  `ChunkBoundariesNeverSplitAList` fixture (`:2258`: 240 paragraphs, then a
  30-item list straddling the 250-block chunk boundary), pumped as
  `LargeNoteStreamsInChunksIntoTheSurface` (`:2160`) does: after the first
  projection the list peer's last item reports a `SizeOfSet` n below 30
  (the items before the boundary; asserting n < 30 first proves the
  fixture straddles, and the query builds the snapshot); once the next
  `BlocksAppended` lands, the same list's last item reports `PositionInSet`
  30 and `SizeOfSet` 30. Mutation: `Model_BlocksAppended` keeps today's
  `InvalidateAutomationChildren()` instead of `InvalidateStructuralPeers()`
  → the stale n is served → fails.
- `ReadingViewTests.SurfaceChildrenAreCachedUntilTheNextMerge` — repeated
  `GetChildren()` on the surface peer rebuilds once; a merge rebuilds
  again. Mutation: drop the `InvalidateStructuralPeers()` call from
  `ApplyBuiltDocument` → the stale list fact (the `:700-703` comment
  records the 2026-08-01 measurement) fails.
- FlaUI `ShellAccessibilityTests.ReadingList_ArrowingThroughTheBlockCapIsBounded`
  (new partial file `ShellAccessibilityTests.ReadingList.cs`). Fixtures:
  the issue's `big-list.md` (`# Big list`, blank, `Intro line.`, blank,
  `- item 1` … `- item 2000`, blank, `## After the list`, blank,
  `Closing paragraph.`) and `small-list.md`, the same shape with three
  items. The out-of-process UIA3 client does what NVDA does, because a
  client that only listens for focus and selection never asks for the
  costly peer answers:
  (a) **Registrations, held for the whole journey** (NVDA's set; WPF raises
  automation events, and does the peer work that feeds them, only while a
  client listens — `AutomationPeer.ListenerExists`):
  `automation.RegisterFocusChangedEvent`; on the main window with
  `TreeScope.Subtree`, `RegisterAutomationEvent` for
  `EventLibrary.Text.TextSelectionChangedEvent` and `TextChangedEvent`,
  `RegisterStructureChangedEvent`, and `RegisterPropertyChangedEvent` for
  `Name`, `ItemStatus`, `IsEnabled`, `Value`, `ToggleState` and
  `ExpandCollapseState`.
  (b) **The per-move query workload**, timed from the key press to its
  completion, run once per move before the next key:
  (1) `text.GetSelection()[0].Clone()`, `ExpandToEnclosingUnit(TextUnit.Line)`,
  `GetText(-1)` — polled until the line is the move's expected line
  (listed under Moves; a 30 s bound; the poll is inside the measured time);
  (2) `line.GetAttributeValue(TextAttributeLibrary.StyleId)`;
  (3) `line.GetEnclosingElement()`, then
  `TreeWalkerFactory.GetRawViewWalker().GetParent` up to the
  `ReadingSurface` element, reading `Name`, `ControlType`,
  `Properties.PositionInSet`, `Properties.SizeOfSet` and
  `Properties.Level` on each element (NVDA's control-field properties);
  (4) `line.GetChildren()` and `text.RangeFromChild(child)` for each child
  (WPF's text peer refreshes the surface peer's children before it answers
  a range's children — `ReadingSurfacePeer.GetChildrenCore`);
  (5) for a line reading `item N`, the list context: from the surface,
  `GetFirstChild`/`GetNextSibling` to the `List` element
  (`ReadingSurfacePeer.GetChildrenCore`), its `Name`
  (`ReadingListPeer.GetNameCore`), then item N by sibling steps from
  `GetFirstChild` (N ≤ 3) or `GetLastChild` (N ≥ 1998)
  (`ReadingListPeer.GetChildrenCore`), and the item's `Name`,
  `PositionInSet` and `SizeOfSet` (`ReadingListItemPeer`).
  **Moves:** Ctrl+Shift+E (entry: until `DocumentRange.GetText(-1)` holds
  `Closing paragraph.`, then one workload run on the caret's line),
  Ctrl+Home (`Big list`), four Downs
  (`Intro line.`, `item 1`, `item 2`, `item 3`), Ctrl+End
  (`Closing paragraph.`), four Ups (`After the list`, `item 2000`,
  `item 1999`, `item 1998`); the same keys on `small-list.md` are the
  baseline (its Ups land on `item 3`, `item 2`, `item 1`).
  **Reach assertions** (so a workload that stops asking cannot pass): on
  every list move the item's `Name` is `item N`, its `PositionInSet` is N,
  its `SizeOfSet` is the list's item count (2,000; 3), and the list's
  `Name` is `2000 entries` (`3 entries`).
  **Bound** (WB-7's ratio form, because the shell gate runs every FlaUI
  fact with no filter — `windows.yml:845-851` — so a `kind=benchmark`
  trait would not keep it out): every move on `big-list.md` takes at most
  max(5 × the median of the same kind of move on `small-list.md`, 250 ms),
  the kinds being the entry, a move to a non-list line, and a move to a
  list line.
  **Seen failing on main, with this workload:** before any peer change the
  journey must fail, its per-move times recorded in the PR body (the issue
  measured ~5.1 s per move and 28 s for the entry under NVDA). If it
  passes on main, the workload is not yet the screen reader's: capture an
  NVDA log at UIA debug level for one arrow on `big-list.md` (the issue's
  second "fact wanted", the 2026-09-22 `nvda.log` recipe), add the provider
  calls it shows to (b), and repeat until main fails; record the log
  excerpt and the final workload in the PR body.
  **Mutation:** restore main's `ReadingSurfacePeer`, `ReadingListPeer` and
  `ReadingListItemPeer` bodies together (from `git show
  origin/main:apps/slate-windows/src/SlateWindows/Reading/ReadingSurface.cs`)
  → the journey fails; the PR body records which single restoration (a new
  item peer per call, the linear `PositionInSet` walk, `ListItems.Count`
  per query, the uncached surface children) fails it alone — the
  deterministic per-path mutations belong to the unit facts above.
- FlaUI `ShellAccessibilityTests.ReadingSelectAll_RaisesTextSelectionChangedAndReturnsTheNote`
  — `trailing.md` (`body`, blank, `#`), Ctrl+Home, Ctrl+A: the client
  receives `TextSelectionChanged` within 2 s and
  `TextPattern.GetSelection()[0].GetText(-1)` is the whole note. Fails today
  on the event if WPF does not raise it, or passes and narrows the Narrator
  defect to provider identity (record which in the PR body).
- `ReadingViewTests.TheTextPatternProviderIsOnePerPeer` — two
  `GetPattern(Text)` calls return the same object. Mutation: `new` per
  call → fails.
- `NavigationHelp`/doctrine censuses (`ReadingDoctrineCensusTests`) stay
  green; `ReadingTextPattern_RangeFromChildResolvesEveryCustomPeer`
  (`:1720`) covers the cached item peers' ranges.

## Docs and registers

- `w_c_matrix.md:20`: amend the Reading view row — list items read the
  authored ordinal (matches mac since #1048); peers cached, positions
  indexed; Ctrl+A raises `TextSelectionChanged`. Cite the two FlaUI
  journeys.
- `parity_matrix.md`: add a row "reading list marker: authored ordinal
  rendered and read" — implemented both hosts.
- `18_windows_port/reports/`: the NVDA timing table (before/after, the
  issue's format) and the Narrator read-back go into the PR body and a
  dated report under `reports/` (the `_at_pass_template.md` shape).
- No contract row (contract 40 has no reading-list row to append to).

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb11'
function Assert-Ok([string] $What) { if ($LASTEXITCODE -ne 0) { throw "$What exited $LASTEXITCODE" } }
function Assert-Discovered([string[]] $Listed, [string[]] $Required) {
  $names = $Listed | ForEach-Object { ($_ -split '\(', 2)[0] }
  $missing = @($Required | Where-Object { $names -notcontains $_ })
  if ($missing.Count -gt 0) { throw "the filter does not select: $($missing -join ', ')" }
}
./apps/slate-windows/generate-bindings.ps1 -Locked; Assert-Ok 'generate-bindings'
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release; Assert-Ok 'dotnet build'   # and Error(s)=0
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
$filter = 'FullyQualifiedName~SlateWindows.Tests.ReadingViewTests|FullyQualifiedName~SlateWindows.Tests.ReadingDoctrineCensusTests|FullyQualifiedName~SlateWindows.Tests.ReadingFocusTests'
$listed = @(dotnet test $tests --no-build --configuration Release --list-tests --filter $filter | Select-String '^\s{4}\S' | ForEach-Object { $_.Line.Trim() }); Assert-Ok 'unit discovery'
Assert-Discovered $listed @(
  'SlateWindows.Tests.ReadingViewTests.AnOrderedListRendersAndReadsItsAuthoredMarkers',
  'SlateWindows.Tests.ReadingViewTests.NestedListsKeepTheirAuthoredDepthAndMarkers',
  'SlateWindows.Tests.ReadingViewTests.ListItemPeersAreStableAcrossQueries',
  'SlateWindows.Tests.ReadingViewTests.PositionInSetOnTheLastOf2000ItemsIsIndexed',
  'SlateWindows.Tests.ReadingViewTests.AListThatGrowsInALaterChunkReindexes',
  'SlateWindows.Tests.ReadingViewTests.SurfaceChildrenAreCachedUntilTheNextMerge',
  'SlateWindows.Tests.ReadingViewTests.TheTextPatternProviderIsOnePerPeer')
dotnet test $tests --no-build --configuration Release --filter $filter; Assert-Ok 'unit tests'
$at = 'apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj'
$atFilter = 'FullyQualifiedName~ShellAccessibilityTests.ReadingList_ArrowingThroughTheBlockCapIsBounded|FullyQualifiedName~ShellAccessibilityTests.ReadingSelectAll_RaisesTextSelectionChangedAndReturnsTheNote|FullyQualifiedName~ShellAccessibilityTests.ReadingTextPattern_RangeFromChildResolvesEveryCustomPeer|FullyQualifiedName~ShellAccessibilityTests.ReadingView_IsTheEditorStop'
$atListed = @(dotnet test $at --no-build --configuration Release --list-tests --filter $atFilter | Select-String '^\s{4}\S' | ForEach-Object { $_.Line.Trim() }); Assert-Ok 'FlaUI discovery'
Assert-Discovered $atListed @(
  'SlateWindows.AccessibilityTests.ShellAccessibilityTests.ReadingList_ArrowingThroughTheBlockCapIsBounded',
  'SlateWindows.AccessibilityTests.ShellAccessibilityTests.ReadingSelectAll_RaisesTextSelectionChangedAndReturnsTheNote',
  'SlateWindows.AccessibilityTests.ShellAccessibilityTests.ReadingTextPattern_RangeFromChildResolvesEveryCustomPeer',
  'SlateWindows.AccessibilityTests.ShellAccessibilityTests.ReadingView_IsTheEditorStop')
# NVDA closed, desktop free; the client's own registrations stand in for NVDA's:
dotnet test $at --no-build --configuration Release --filter $atFilter; Assert-Ok 'FlaUI journeys'
# seen failing first: the same two commands against a build of origin/main plus only the new test files must exit nonzero, with the per-move times in the log
$cs = @(git diff --name-only origin/main -- '*.cs') + @(git ls-files --others --exclude-standard -- '*.cs')   # Reading/ReadingSurface.cs, Reading/ReadingDocumentBuilder.cs, Reading/ReadingSemantics.cs, Reading/HeadingStyleText.cs, ReadingViewTests.cs, ShellAccessibilityTests.ReadingList.cs (new)
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $cs; Assert-Ok 'dotnet format'
```

`ReadingSurface.cs` has 7 CRs (mixed endings) — check before and after.
CI: the Windows lane with the FlaUI shell gate; the timing journey runs in
the gate (its step at `windows.yml:845-851` has no filter), ratio-bounded
against `small-list.md` in the same run. AT (§4 rule 4, launched
with `SLATE_LOCAL_APP_DATA` once WB-1 is in): NVDA — the #1329 recipe,
Release build, `big-list.md`: time Ctrl+Shift+E to first speech, Ctrl+End,
three Ups, four Downs; expected under 1 s each and no watchdog freeze in
`nvda.log`; an ordered list `3. 4. 5.` reads "3. Third", "4. Fourth".
Narrator — the #1330 recipe: Ctrl+A says the selection (Narrator's
"selected" phrase), Narrator+Shift+Down reads "body … blank"; read back
with Narrator+Ctrl+X. Record both tables in the PR body.

## Acceptance

- [ ] Ten facts above pass, seen failing first; mutation list run; both discovery checks find every required fact by fully qualified name (they throw otherwise) and every Verification command exits 0.
- [ ] The timing journey, with the registrations of (a) and the per-move workload of (b), seen failing on `origin/main` (per-move times recorded); its reach assertions hold on every list move; restoring main's three peer classes together fails it again; the PR body records the before/after per-move table, which single restoration fails it alone, and — if the workload had to grow — the NVDA UIA-debug log excerpt it grew from.
- [ ] Shell gate green including the two new journeys (the timing one ratio-bounded, not benchmark-tagged: the gate step has no filter).
- [ ] `w_c_matrix.md` row amended, `parity_matrix.md` row added, dated report filed.
- [ ] NVDA timing table and Narrator read-back recorded in the PR body.
- [ ] PR body: `Closes #1422`, `Closes #1329`, `Closes #1330`. Not delivered: a Narrator pass of Ctrl+A in the EDITOR view (the issue's open question; out of this chunk), and no scaling study beyond the 2,000-block cap.

## Brief

Read `ReadingDocumentBuilder.cs:213-275`, `ReadingSurface.cs:357-367,
401-440, 696-720, 945-1000, 1131-1200`, `HeadingStyleText.cs:36-80`,
`ReadingSemantics.cs:82-90`, core `reading.rs:785-793`, mac
`Reading/ReadingView.swift:702`, the pins and patterns at
`ReadingViewTests.cs:681, 730, 2160, 2258, 2330-2365`, the FlaUI reading
journeys at `ShellAccessibilityTests.cs:1381, 1720` and the caret-line read
at `ShellAccessibilityTests.ReadingFocus.cs:150-185`, `windows.yml:845-851`
(the shell gate, no filter), and issue #1329's recipe and table. Build, in
that order: (1) in
`ReadingDocumentBuilder.AppendListItem`, ordered lists get
`MarkerStyle = TextMarkerStyle.None` and each item a leading `Run` of
`inline.ListMarker ?? "1."` marked by a new
`ReadingSemantics.MarkListMarker(run)`, the level-change test keyed on an
`Ordered` bool in the stack tuple; (2) `ReadingListPeer` keeps a
`ConditionalWeakTable<ListItem, ReadingListItemPeer>` and a
`ConditionalWeakTable<WpfList, ReadingListPeer>` for nested list peers;
every read of `_list.ListItems` goes through a new
`ReadingListPeer.Snapshot()` (item array, `ListItem → int` index and count,
built once per structural generation, counted by `WalkProbeForTests`) that
`GetPositionInSetCore`, `GetSizeOfSetCore` and the list's `GetNameCore`
read — never `ListItems.Count` per query, which WPF computes by walking the
collection (measured: 2,000 reads on a 2,000-item list, 176 ms);
`ReadingListItemPeer` caches its trimmed name; `ReadingSurfacePeer`
caches its structural children per structural generation;
`ReadingSurface.CensusDiag` formats only when enabled; a new
`internal void ReadingSurface.InvalidateStructuralPeers()` bumps the
generation (dropping every cache) and runs today's
`InvalidateAutomationChildren()` reset, replacing both of its call sites —
the merge (`ReadingSurface.cs:439`) and the streamed append
(`Model_BlocksAppended`, `:366`, which can grow a list opened in an earlier
chunk) — WB-20's later in-place diagram completion does not call it, since
it edits Runs in a top-level paragraph that no cache reads (diagram blocks
are never in a list, `ReadingDocumentBuilder.cs:213-215`); (3)
`ReadingSurfacePeer.GetPattern` returns
one `HeadingStyleTextProvider` per peer, and `ReadingSurface` raises
`AutomationEvents.TextPatternOnTextSelectionChanged` from its
`SelectionChanged` if the probe shows WPF does not. Facts, written first
and seen failing: `ReadingViewTests.AnOrderedListRendersAndReadsItsAuthoredMarkers`
(`3. 4. 5.` reads "3. Third"…, `1. 1. 1.` and `12)` kept, `MarkerStyle`
None, positions unchanged); `NestedListsKeepTheirAuthoredDepthAndMarkers`
(`:2364` pin → None + marker run);
`ReadingViewTests.ListItemPeersAreStableAcrossQueries` (two
`GetChildren()` calls return reference-equal item peers);
`ReadingViewTests.PositionInSetOnTheLastOf2000ItemsIsIndexed` (1,000
each of `PositionInSet` and `SizeOfSet` on item 2000 and list `Name` stay
under an allocation bound, one `WalkProbeForTests` walk, and at most 3× the
same queries on a 20-item list, floor 20 ms);
`ReadingViewTests.AListThatGrowsInALaterChunkReindexes` (the
`ChunkBoundariesNeverSplitAList` fixture, `ReadingViewTests.cs:2258`: the
last item's `SizeOfSet` is some n < 30 after the first projection, then
`PositionInSet` 30 and `SizeOfSet` 30 once the next `BlocksAppended`
lands); `ReadingViewTests.SurfaceChildrenAreCachedUntilTheNextMerge` (one
rebuild per merge); `ReadingViewTests.TheTextPatternProviderIsOnePerPeer`
(same provider object twice); FlaUI
`ShellAccessibilityTests.ReadingList_ArrowingThroughTheBlockCapIsBounded`
in a new `ShellAccessibilityTests.ReadingList.cs` — the issue's
`big-list.md` (`# Big list`, `Intro line.`, `- item 1`…`- item 2000`,
`## After the list`, `Closing paragraph.`) against `small-list.md` (three
items); the UIA3 client holds NVDA's registrations for the whole journey
(`RegisterFocusChangedEvent`; on the window's subtree TextSelectionChanged,
TextChanged, StructureChanged, and PropertyChanged for `Name`,
`ItemStatus`, `IsEnabled`, `Value`, `ToggleState`, `ExpandCollapseState`)
and after every key runs, timed from the press, NVDA's per-move queries:
the caret line (`GetSelection()[0].Clone()`, `ExpandToEnclosingUnit(Line)`,
`GetText(-1)`, polled until it is the move's expected line),
`GetAttributeValue(StyleId)`,
`GetEnclosingElement()` and a raw-view `GetParent` walk to the surface
reading `Name`, `ControlType`, `PositionInSet`, `SizeOfSet`, `Level`,
`line.GetChildren()` with `RangeFromChild` per child, and for a line
`item N` the list context (surface children to the `List`, its `Name`,
item N by sibling steps from `GetFirstChild`/`GetLastChild`, and the
item's `Name`, `PositionInSet`, `SizeOfSet`); moves are Ctrl+Shift+E,
Ctrl+Home, four Downs, Ctrl+End, four Ups; on every list move the item is
named `item N` with `PositionInSet` N and `SizeOfSet` 2,000 and the list is
named `2000 entries`; every move takes at most max(5 × the small note's
median for that kind of move — entry, non-list line, list line —, 250 ms) — a ratio, because the shell gate (`windows.yml:845-851`)
has no filter; it must fail on `origin/main` (per-move times recorded),
and if it does not, an NVDA UIA-debug log of one arrow supplies the calls
to add until it does; and
`ShellAccessibilityTests.ReadingSelectAll_RaisesTextSelectionChangedAndReturnsTheNote`
(`trailing.md`, Ctrl+A: `TextSelectionChanged` within 2 s and
`GetSelection()[0].GetText(-1)` is the whole note). Mutations:
`StartIndex` from the first item; `new` peer per call; the linear walk
back; `ListItems.Count` per query back; the append path's
`InvalidateStructuralPeers()` dropped; the merge's
`InvalidateStructuralPeers()` dropped; `new` provider per call; main's three
peer classes restored together (fails the timing journey). Local loop, each
native command followed by a throw on a nonzero `$LASTEXITCODE`: bindings
regenerated (`$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb11'`), Release
build, `dotnet test --list-tests` over
`FullyQualifiedName~SlateWindows.Tests.ReadingViewTests |
…ReadingDoctrineCensusTests | …ReadingFocusTests` throwing unless the seven
`ReadingViewTests` facts above are listed by fully qualified name, the run,
then the same for the accessibility project over the four
`ShellAccessibilityTests` journeys (the two new ones,
`ReadingTextPattern_RangeFromChildResolvesEveryCustomPeer`,
`ReadingView_IsTheEditorStop`) with NVDA closed, and the format gate over
the touched `.cs` files. Registers:
amend `docs/plans/18_windows_port/w_c_matrix.md:20` (authored ordinal,
cached peers, Ctrl+A event); add a "reading list marker" row to
`docs/plans/18_windows_port/parity_matrix.md`; file the NVDA timing table
and the Narrator read-back as a dated report under
`docs/plans/18_windows_port/reports/` (the `_at_pass_template.md` shape).
No owner decision. Not delivered: a Narrator pass of Ctrl+A in the editor
view; no scaling study past the 2,000-block cap. Done when the Windows
lane with the shell gate is green (the timing journey in it), the format
gate is clean with `ReadingSurface.cs`'s CR count unchanged, the mutation
run is recorded (including the timing journey failing on `origin/main` and
again with main's three peer classes restored, and which single restoration
fails it alone),
and the NVDA timing table (under 1 s per move, no watchdog freeze) and the
Narrator read-back (Ctrl+A announced, Narrator+Shift+Down reads "body …
blank") are in the PR body with `Closes #1422`, `Closes #1329`, `Closes
#1330`. Commit: `fix(windows): reading lists keep their authored markers,
cost nothing per arrow, and say so on select all`.
