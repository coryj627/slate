# PR 13 — The graph table's authority model converges to Windows (D1)

Issues: #1189 (IPG-15, the mixed publication), #1190 (the rows receiver
admits a result with no held snapshot). Size M. Lanes: swift, a11y,
windows. Depends on PR 5 (shared `AppState+GraphTable.swift`) and on D1
(decided: converge). Parallel with PRs 14, 15, 15b, 25.

## Goal

The mac graph table can never show old rows under a new authority. It
adopts Windows' publication model: one immutable publication record
installed in one assignment; a result publishes only when its sequence
and request are the document's current ones, else it is dropped whole; a
rows-only result needs a held publication with the same filter and
generation, else it re-fetches as a pair under its own request. The
frozen facts that pin the old design are rewritten first, with the
owner's recorded call.

## Current state on main

- `Graph/AppState+GraphTable.swift:326` `receiveGraphTableRows(token:
  result:)`: guards seq and request (`:327`); a filter mismatch re-fetches
  under its own request (`:331-341`, C-2 (iii)); a generation mismatch
  re-fetches (`:342-348`); then publishes rows, total, sort, published
  request, answered seq (`:349-357`), clears the error (`:366`), and when
  `graphTableSnapshot == nil` asks for a snapshot back (`:367+`, TGC-17/
  TGC-18) but has already published. The failure arm (`:468-480`,
  `failGraphTableRows`) rolls back the pending sort and the announce
  only, never a snapshot.
- The pair receiver installs the candidate snapshot before validating it
  against the authority it replaces (A-2's record: "the mac installs the
  candidate at `:219-222` before `:226`"); a pair superseded by a rows
  request therefore installs its snapshot while the newer rows are
  unanswered; if those rows fail, the table shows the old rows under the
  new snapshot, `graphTablePublishedRequest` trails `graphTableRequest`
  (Where-am-I unavailable), and `graphTableSeenGraphGeneration` has
  advanced, so the probe finds nothing to repair (IPG-15).
- Frozen facts, `Tests/SlateMacTests/GraphTableViewTests.swift:144-235`:
  `testSortRequestPublishesRowsAndAcceptedSortTogether` (`:144`),
  `testSupersededTokenIsDroppedWhole` (`:159`),
  `testReorderedSameGenerationAnswersUnderDifferentNeedlesAreDropped`
  (`:173`), `testFailedQueryRollsBackTheRequest` (`:187`),
  `testRowsUnderAnotherFilterThanTheHeldSnapshotAreDropped` (`:199`),
  `testStaleGenerationResultIsDiscarded` (`:214`),
  `testPresetAfterFolderSortPublishesDefaultSortAndHeadlineTogether`
  (`:229`); they drive the receiver on a bare `AppState` with no snapshot
  and require it to publish. T1's
  `testANeedleDuringAnOrphansPairRefetchesUnderItsOwnRequest` parks the
  rows publish gate for a needle typed during a preset's pair; TGC-17
  (`35_graph_contracts.md:11658`) records that changing the kind rule
  without rewriting that fact hung the Swift lane for five hours.

## Windows reference

- `Graph/GraphPublication.cs:119` `FromPair(…)` and `:136` `WithRows(query,
  rows, acceptedSort)`: one immutable record; `WithRows` needs the held
  record's snapshot.
- A-2 (contract 35 PR A): a result publishes only if "its seq and request
  equal the document's current ones — else drop"; ONE record in ONE swap;
  the rows-only arm refuses a result with no held snapshot and re-fetches
  as a pair. The Where-am-I readback requires a READY record held
  (`GraphDocumentViewModel.cs:625-640`, `publication.Snapshot is null`).
- TGC-13 (`:11396`, IPG-15 "RECORDED FOR THE OWNER, NOT FIXED") and
  TGC-17 (`:11658`, the divergence and the process rule).

## Design

1. **Record.** `GraphTablePublication { snapshot, filter, generation,
   rows, total, sort, request, answeredSeq }` on `AppState`, replacing the
   separate `graphTableSnapshot`, `graphTableSnapshotFilter`,
   `graphTableRows`, `graphTableTotal`, `graphTableSort`,
   `graphTablePublishedRequest`, `graphTableAnsweredSeq` (keep computed
   accessors with the old names so views and facts change minimally).
   Installed in one assignment; `graphTableSeenGraphGeneration` advances
   only on an install.
2. **Pair result.** Publish only if `token.seq == graphTableSeq` and
   `token.request == graphTableRequest`; otherwise drop whole (no
   snapshot install). This makes IPG-15 unreachable: nothing partial is
   ever installed.
3. **Rows result.** Publish only if current AND a held publication exists
   with the same filter and generation (`withRows`); a filter mismatch or
   a missing/stale authority re-fetches as a PAIR under the rows request
   (its sort included, inheriting the announce, C-2 (iv)); the
   `graphTableSnapshot == nil` arm at `:367+` goes away because the
   receiver no longer publishes without an authority.
4. **Failure arms.** A pair failure installs the error (as today); a rows
   failure rolls back the pending sort and the announce (as today); with
   rule 2 there is no half-installed state to repair. Where-am-I reads
   the held publication.
5. **Order of work (the TGC-17 rule).** (a) Append the owner's call (D1,
   converge, 2026-10-09) under a new TGC entry in contract 35 with the
   facts to be rewritten named; (b) rewrite the frozen facts to the new
   rule: each seeds a held pair publication before driving rows; T1's
   fact awaits the needle's PAIR publish (not the rows gate); (c) then
   change the receivers; (d) read and simulate every fact that touches
   the changed members before the first push.

## Changes

- `Graph/AppState+GraphTable.swift` (receivers, the record), the
  `@Published` state in `AppState.swift` around `:3018`, `Graph/
  GraphTableView.swift` and `Graph/AppState+GraphDiagram.swift` readers.
- `GraphTableViewTests.swift:144-235` and T1's fact.

## Tests

- The rewritten frozen facts (same names, new seeding).
- `testASupersededPairInstallsNothing`,
  `testARowsFailureAfterASupersededPairLeavesTheOldPublicationWholeAndWhereAmIAnswers`
  (the IPG-15 scenario), `testARowsResultWithNoHeldSnapshotRefetchesAsAPair`
  (the #1190 scenario), `testAGenerationMismatchRefetchesAsAPair`,
  `testThePublicationInstallsInOneAssignment` (observe the published
  record once, never an intermediate).
- `GraphActionParityTests` / `GraphAnnouncerTests`: announcement policy
  unchanged (speak only what published).

## Accessibility

No new copy; the never-silent policy is unchanged. A failure now leaves
a coherent table and a working Where-am-I.

## Docs and registers

`docs/plans/35_graph_contracts.md`: a new TGC entry recording D1 and the
fact rewrites; TGC-13's IPG-15 and TGC-17's divergence marked closed;
A-2's "not the mac's" sentence amended; the #746 reconciliation rows for
#1189 and #1190 closed.

## Acceptance

- [ ] The record exists and installs atomically; the two scenarios are
      pinned and pass.
- [ ] Every frozen fact is rewritten with the owner's record, not
      deleted.
- [ ] The Swift lane finishes in its normal time (no parked gate).
- [ ] Registers updated; lanes green.

## Risks

The one named in TGC-17. The fact rewrite comes first; the PR body
lists, per rewritten fact, what it now awaits.

## Brief

Converge the mac graph table's authority model to Windows' (owner
decision D1). Read `Graph/AppState+GraphTable.swift:200-370` and
`:460-485`, `GraphTableViewTests.swift:140-240` and T1's needle fact,
the Windows `Graph/GraphPublication.cs:119-140` and
`GraphDocumentViewModel.cs:625-640`, and contract 35 TGC-13 and TGC-17.
First append the TGC record of D1 and rewrite the frozen facts to the
new rule (seed a held pair; T1 awaits the pair). Then introduce one
immutable publication record installed in one assignment, drop a
superseded pair whole, and make a rows result require a held
publication with the same filter and generation or re-fetch as a pair
under its own request. Simulate every touched fact before pushing. Add
the five new facts. Close TGC-13/TGC-17's items and the two
reconciliation rows. Done when the lane is green in normal time.
