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
  (`:1197-1198`). Budget `ReadingContentViewModel.cs:52`
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
failing against the 5 s/line baseline. Then: (1) `ReadingListPeer` keeps a
`ConditionalWeakTable<ListItem, ReadingListItemPeer>` and returns the same
peer per item across calls; (2) `ReadingListItemPeer.GetPositionInSetCore`
reads an index the list peer builds once per `ListItems` snapshot
(`Dictionary<ListItem,int>`, rebuilt when `_list.ListItems.Count` or the
first/last item identity changes — the document is immutable between
merges); (3) `GetNameCore` caches the trimmed text per peer and clears it
when the surface's `ApplyBuiltDocument` (`:401`) runs — the peers of the
old document are dropped with it; (4) `ReadingSurfacePeer.GetChildrenCore`
caches its structural list per applied `FlowDocument` identity and
invalidates it in the `:712-719` path before `ResetChildrenCache`;
(5) `CensusDiag` formatting is gated: `ReadingSurface.CensusDiagEnabled`
(from the existing switch `:732` reads) is checked before the
interpolated string is built. Not changed: the hierarchy itself (W3-1's
measured reason for it, `:945-948`).

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
  2,000-item fixture: 100 `GetPositionInSet` queries on the last item
  allocate under a bound in the `HugeDocumentSyntheticQueriesHaveBoundedCost`
  style (`GC.GetAllocatedBytesForCurrentThread`), and a `WalkProbeForTests`
  counter stays at one. Mutation: restore the linear walk → fails.
- `ReadingViewTests.SurfaceChildrenAreCachedUntilTheNextMerge` — repeated
  `GetChildren()` on the surface peer rebuilds once; a merge rebuilds
  again. Mutation: drop the invalidation at `:712` → the stale list fact
  (the comment there records the 2026-08-01 measurement) fails.
- FlaUI `ShellAccessibilityTests.ReadingList_ArrowingThroughTheBlockCapIsBounded`
  (benchmark-tagged, ratio-bounded per WB-7's rule): the issue's
  `big-list.md` fixture (`# Big list`, intro, `- item 1`…`- item 2000`,
  closing), Ctrl+Shift+E, then Ctrl+End and three Ups with a UIA client
  subscribed to focus and text-selection events; the per-move time is at
  most 5× the same moves on a five-line note and under 1 s absolute.
  Fails today at ~5 s/move.
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

```
$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb11'; ./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --filter "FullyQualifiedName~ReadingViewTests|FullyQualifiedName~ReadingDoctrineCensusTests|FullyQualifiedName~ReadingFocusTests"
dotnet test apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj --filter "FullyQualifiedName~ReadingList_|FullyQualifiedName~ReadingSelectAll_|FullyQualifiedName~ReadingTextPattern_|FullyQualifiedName~ReadingView_IsTheEditorStop"   # NVDA closed, desktop free
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include apps/slate-windows/src/SlateWindows/Reading/ReadingSurface.cs apps/slate-windows/src/SlateWindows/Reading/ReadingDocumentBuilder.cs apps/slate-windows/src/SlateWindows/Reading/ReadingSemantics.cs apps/slate-windows/src/SlateWindows/Reading/HeadingStyleText.cs <tests>
```

`ReadingSurface.cs` has 7 CRs (mixed endings) — check before and after.
CI: the Windows lane with the FlaUI shell gate. AT (§4 rule 4, launched
with `SLATE_LOCAL_APP_DATA` once WB-1 is in): NVDA — the #1329 recipe,
Release build, `big-list.md`: time Ctrl+Shift+E to first speech, Ctrl+End,
three Ups, four Downs; expected under 1 s each and no watchdog freeze in
`nvda.log`; an ordered list `3. 4. 5.` reads "3. Third", "4. Fourth".
Narrator — the #1330 recipe: Ctrl+A says the selection (Narrator's
"selected" phrase), Narrator+Shift+Down reads "body … blank"; read back
with Narrator+Ctrl+X. Record both tables in the PR body.

## Acceptance

- [ ] Nine facts above pass, seen failing first; mutation list run.
- [ ] Shell gate green including the two new journeys (one benchmark-tagged).
- [ ] `w_c_matrix.md` row amended, `parity_matrix.md` row added, dated report filed.
- [ ] NVDA timing table and Narrator read-back recorded in the PR body.
- [ ] PR body: `Closes #1422`, `Closes #1329`, `Closes #1330`. Not delivered: a Narrator pass of Ctrl+A in the EDITOR view (the issue's open question; out of this chunk), and no scaling study beyond the 2,000-block cap.

## Brief

Read `ReadingDocumentBuilder.cs:245-275`, `ReadingSurface.cs:700-720,
945-1000, 1131-1200`, `HeadingStyleText.cs:36-80`, `ReadingSemantics.cs:82-90`,
core `reading.rs:785-793`, mac `Reading/ReadingView.swift:702`, the pins at
`ReadingViewTests.cs:681, 730, 2330-2365`, and the FlaUI reading journeys at
`ShellAccessibilityTests.cs:1381, 1720`. Write the timed FlaUI probe and the
Ctrl+A probe first and watch them fail. Then: render the authored marker as
a leading run on `MarkerStyle.None` ordered lists; cache item peers
(`ConditionalWeakTable`), index positions once per list, cache names until
the next merge, cache the surface's structural children and gate
`CensusDiag`; keep one `HeadingStyleTextProvider` per peer and raise
`TextPatternOnTextSelectionChanged` from `SelectionChanged` if WPF does not.
Amend `w_c_matrix.md:20`, add the parity row, run the NVDA timing and the
Narrator read-back and record them. Done when the shell gate is green and
both AT tables are in the PR body. Commit: `fix(windows): reading lists keep
their authored markers, cost nothing per arrow, and say so on select all`.
