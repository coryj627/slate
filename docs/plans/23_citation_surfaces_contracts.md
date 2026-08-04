# W4-5 Citation Surfaces — Feature Contracts & Accepted-Risk Register

Companion to `22_property_panel_contracts.md` (W4-4) and
`21_write_intent_protocol_invariants.md` (W4-3).

**Status: COMPLETE.** Contracts 1–7 and 9–12 and divergences D-2, D-3,
D-4, D-5, D-8 were reconstructed from code comments, each with the
citation that evidences it. Contract 8 and D-1, D-6, D-7, D-9, D-10,
D-11, D-12 are reproduced verbatim from the W4-5 research plan.

**The protocol failure was not that these were never written.** They
were written — into a session-local scratchpad plan that no reviewer,
and no later session, could reach. Seven of the twelve divergences
were invisible to a reconstruction working from the repo alone, and
contract 8 read as a numbering hole. **The rule this earns: the
contracts document lands in `docs/` as its own commit BEFORE the first
review round.** A plan held anywhere else does not count as written
down, which is why round 2 re-litigated design choices with nothing
marked off-limits.

The view layer landed 2026-08-04, so contract 8 and D-1, D-6, D-7,
D-12 are now cited in code rather than pending.

Numbering is PER-WAVE: W4-4 also has a "contract 7" and a "contract
10" and they are unrelated. Only the citation surfaces
(`Citation*.cs`, `Bibliography*.cs`, `FilesCiting*.cs`,
`WorkspaceViewModel.Citations.cs`) carry W4-5 numbering.

## Feature contracts

**1 — Rows are built from core artifacts and nothing else.** A row's
spoken name is `RenderedCitation.SpeechText` verbatim, with the single
sanctioned `"Unresolved citation key. "` prefix. The VM never composes
citation speech and never parses `VisualText`.
*(CitationsPanelViewModel.cs:13-20, CitationPhrase.cs:18,58)*

**2 — A style id is never invented.** The only ids passed to
`RenderCitation` come from `CitationsPrefs`. An empty style id yields a
placeholder per reference with no render calls at all.
*(CitationsPanelViewModel.cs:17-19,176,302)*

**3 — Publication is guarded by generation + requestId.** A path
change bumps the generation and clears rows synchronously, so no stale
row is ever actionable while a newer read is in flight. Both
bibliography segments carry the same guard.
*(CitationsPanelViewModel.cs:27-30, BibliographyViewModel.cs:28,322)*

**4 — Announcement identity.** The count announcement fires once per
path, only for a NON-EMPTY set, and never on a stale publish. The
walk-through event is announced exactly once, and core owns it.
*(CitationsPanelViewModel.cs:221-222, CitationSummaryViewModel.cs:74-77)*

**5 — Degradation honesty.** Load notices are surfaced verbatim and
never swallowed. "No sources configured" is a DISTINCT state from
"sources exist but yielded no entries". Seeding is silent on both
success and failure — the leaf's notice region is the surface.
*(BibliographyViewModel.cs:74-77,161-164, WorkspaceViewModel.Citations.cs:97)*

**6 — Read-only safety.** `WorkspaceViewModel.Citations.cs` holds the
ONLY `SetBibliographySources` call site in the application. No panel
VM, row VM, overlay, command, or context-menu action may write. A test
asserts note bytes are unchanged across a full exercise of both
leaves.
*(WorkspaceViewModel.Citations.cs:12-17, BibliographyViewModel.cs:21-23)*

**7 — Unresolved is a real state, not an error.** A key with no
bibliography entry despite a real style renders as unresolved rather
than failing the load. By contrast a render FAILURE fails the whole
load — one bad style is a panel-level condition, not N independently
broken rows.
*(CitationsPanelViewModel.cs:21-25,283)*

**8 — Grid substrate conformance.** Both bibliography views bind
through `AccessibleDataGrid.Bind` with a row-header column (Title /
Key), per-column comparators (no `SortMemberPath` member paths), and
`rowAudioDescription`; they inherit headers-on-entry, cell-by-cell
arrows, Ctrl+Alt+S sort, grid-scoped Ctrl+F, and the addressable
summary region unmodified. `exportProducer` is `null` on both
(O8/G29). No re-implementation of grid behaviour lives in the
citations code.
*(MainWindow.xaml bibliography leaf body, MainWindow.Citations.cs:29-104
column definitions)*

**9 — The entry grid is bounded and the bound is SPOKEN.** The cap is
`MaxEntryRows` = 5000 and the truncation sentence rides the grid
summary verbatim, so the bound is announced rather than silent. See
D-3.
*(BibliographyViewModel.cs:38-42,183-195, CitationPhrase.cs:125-127)*

**10 — Absent values never produce a row.** The details overlay omits
rows for absent values entirely rather than rendering empty ones.
*(CitationDetailsViewModel.cs:8-9,28,68)*

**11 — Escape returns focus exactly to the opening row.** Every
overlay and sheet carries the identity of the row that opened it.
*(CitationDetailsViewModel.cs:82-83, FilesCitingViewModel.cs:41-42)*

**12 — Unique-key counting comes from the structural references.** The
summary counts unique keys from `References`, never from rendered
rows, because rendered rows cannot expose multi-key sites.
*(CitationsPanelViewModel.cs:55-58, CitationSummaryViewModel.cs:12)*

## Accepted risks / recorded divergences (do not re-litigate)

**D-1** — Details are an in-window overlay sheet, not an anchored
popover. mac anchors a `.popover` to the row (`arrowEdge: .leading`)
and pins `expandedCitationRowAnchored`. WPF `Popup` renders in a
separate HWND whose UIA subtree is a sibling window — a known AT
hazard — and the shipped Windows precedent for a focus-trapped detail
surface is the in-window `AutomationLandmarkBorder` overlay with
`IsDialog=True` (`AddPropertySheet`). The WCAG 2.4.3 / 2.1.2
focus-return contract mac cites is preserved verbatim (contract 11);
only the visual anchoring differs.

**D-2** — mac says "Open Settings → Bibliography to add one." Windows
has no bibliography settings pane until W8-1, and pointing a
screen-reader user at a menu that does not exist is an accessibility
lie. **Flip to the mac string when W8-1 ships the pane.**
*(CitationPhrase.cs:97-101)*

**D-3** — Windows ADDITION: the entry grid is capped where mac is not,
and the cap is spoken (contract 9). Core's own guidance is "<10k
entries typically", so the bound is unreachable in practice.
*(BibliographyViewModel.cs:38-42)*

**D-4** — Search is a HOST-SIDE predicate over the loaded set. Core's
`SearchBibliography` is a LIKE over title and authors only, while mac
matches title, key, family AND given name. Matching mac requires the
host predicate, so `SearchBibliography` is deliberately not called.
*(BibliographyViewModel.cs:30-34,460)*

**D-5** — Core owns the walk-through wording, including its "sidebar
tab" phrasing, which is wrong for the Windows shell. Forking the
string would break §W-D, so the mac phrasing is spoken as-is.
*(CitationSummaryViewModel.cs:73-77)*

**D-6** — Unresolved citations render as a grid, not a grouped list.
mac uses `List`/`Section` with a path header per group. WPF grouped
`ListBox` UIA shapes have no precedent in this app, and the data is
two-column tabular. The grid gives cell-by-cell navigation and
sortable columns; the mac row label `Unresolved key: {key} in
{path}.` survives verbatim as the grid's `rowAudioDescription`.

**D-7** — No CSV/Markdown export on either citation grid (O8).
`exportProducer: null`; the substrate's export commands are inert.
Recorded as **G29**; composing export text in C# is forbidden by
`w4_spec.md:17` item 3b.

**D-8** — Windows has no `extractCitationKey` fallback. mac needs one
because its refs and rendered lists load independently and can
disagree; the Windows leaf publishes both from the same worker in the
same publish, so they are structurally 1:1.
*(CitationSummaryViewModel.cs:19-22)*

**D-9** — `abstract_present` rather than the abstract text in the
§W-A artifact. `citations_db.rs:353` hardcodes `abstract_text: None`
on the DB path while `bibliography.rs:517` populates it in memory;
which path a platform lands on is a cache-state property, not a
platform property. The boolean is the honest cross-platform claim.

**D-10** — `raw_csl_json` is excluded from the §W-A artifact. It is a
serde re-serialization of a foreign document; field ordering there is
a serializer contract, not a citation contract. `render_citation`'s
outputs — which is what users hear — are fully pinned.

**D-11** — The editor's citation cache and the leaf's are separate.
Two reads of `ListCitationsInFile` for the same note. Unifying them
would restructure a shipped W2-3 surface for no user-visible gain
(O6). Both are generation-guarded independently.

**D-12** — `citations`/`bibliography` are the first production
consumers of `AccessibleDataGrid` outside the bulk-rename preview and
reading tables. Any §8.7 conformance gap this exposes is a W4-1
substrate bug to fix in `Grids/`, not a citations workaround.

## Initialization lifecycle — OPEN, design pass pending

The red-team arc produced blockers in this area two rounds running,
with round 2's blockers created by round 1's fix. Protocol rule (3)
fired: no more patching until the model below is written.

### Confirmed defect, PRE-EXISTING (not introduced by the round-1 fix)

A failed seed leaves the PREVIOUS session's bibliography live, and
citations resolve against it. Verified end to end:

- `session.rs:2085-2088` builds the initial `BibIndex` from whatever
  is already in `bibliography_entries` — "populated after a re-open if
  the previous session ran `set_bibliography_sources`".
- `session.rs:8491` — `load_source(src, &vault_root)?` propagates, so
  ANY unreadable source returns early, BEFORE
  `replace_bibliography_entries` (8515) and before the `bib_index`
  rebuild (8521-8526). Both the table and the index survive untouched.
- Schedule: seed succeeds once → the configured source is later
  removed or made unreadable → reopen → key K renders as RESOLVED
  against last session's data, under an error notice.

This violates contract 5 (degradation honesty): stale data is
presented as authoritative while the notice says loading failed. It is
reachable without coincidences and predates the gate — before it,
panels queried the same stale index concurrently.

### Decisions the design pass must make

1. **Terminal seed outcome.** Replace "gate released" with a typed
   result (seeded / failed-with-notices / cancelled). Panels branch on
   it instead of querying blindly. Required by the defect above.
2. **Teardown.** Who completes the gate on `Dispose`, and gated bodies
   must recheck `IsShutDown` AFTER the wait. Note the trap: a naive
   `TrySetCanceled` throws into the `catch (AggregateException)` in
   `PanelWorkScheduler.StartWork` and the body runs anyway.
3. **Wait mechanism.** Async continuation instead of blocking a pool
   thread, plus coalescing superseded requests so opening several tabs
   during init cannot accumulate stale work.
4. **Ownership.** Does the prerequisite gate belong on the shared
   `PanelWorkScheduler` (current, affects every panel) or on a
   W4-5-owned initialization type? The shared placement was chosen for
   cost, not fit.
5. **Ctrl+J completion semantics — AUTHOR DECISION.** Two presses
   before publication currently overwrite the first callback, so the
   first press announces NOTHING; a reload mid-jump does the same.
   Silence after a keypress is wrong in a screen-reader-first app, but
   which outcome is right (supersede-and-announce vs resolve both) is
   a product call.
6. **Test seam.** Build a controllable scheduler seam (manually
   released tasks + deterministic `SynchronizationContext`) or accept
   fixture-speed determinism as a recorded risk. Either way it becomes
   a register entry so it stops recurring every round.

### Settled

**The Ctrl+J focus request is an event, not a property** (`df2d6a0`).
The jump resolves ASYNCHRONOUSLY on first press, so a view cannot
invoke the command and then read a property — it would see null.
`KeyFocusRequested` + atomic `ConsumeKeyFocusRequest()`, mirroring
`EditorInteractions.PopoverFocusRequested` /
`ConsumePopoverFocusRequest` (consumer pattern:
`EditorInteractionPopoverHost.cs:69,84`). `Shutdown` and `ForceReload`
drop unconsumed requests. **The W4-5 XAML must SUBSCRIBE, not poll.**

## Substrate gaps the view layer exposed (D-12 in practice)

D-12 said the citation leaves are the first production consumers of
`AccessibleDataGrid` outside the bulk-rename preview and reading
tables, and that any conformance gap they expose is a W4-1 substrate
bug to fix in `Grids/` rather than a citations workaround. Two
appeared, and both were fixed there:

- **`AccessibleDataGrid.GridAutomationId`.** The inner grid's
  automation id was hardcoded to `AccessibleDataGrid` for every
  instance. W4-5 is the first window to show more than one grid — two
  bibliography segments plus the bulk-rename preview — so an AT user
  and a FlaUI lookup alike could not tell them apart. The id is now
  settable and renames the summary region with it; the default is
  unchanged, so W4-1's conformance fixture and bulk-rename keep the
  ids they already publish.
- **`AccessibleDataGrid.FocusRow`.** Ctrl+J must land on a NAMED row.
  Reaching into the exposed `Grid` to set currency by hand would have
  been a second implementation of cell focus, which contract 8
  forbids. `FocusRow` delegates to the same private
  `FocusCellElement` that `FocusFirstCell` uses. Its bool reports
  whether the row was in the bound set, NOT whether the OS granted
  focus — those differ off-window, and conflating them made a
  successful jump indistinguishable from a missing key (caught by the
  test, which failed against the first implementation).

## Review resolutions

### Round 1 (2026-08-04, low effort) — 2 highs, both fixed in `4d8bec5`

Both were production-only ordering races, invisible to the suite
because every test ran `startInteractionBackgroundWork: false`, which
makes `StartWork` inline and orders everything.

- `SyncPanels` raced `SeedBibliographySources` as two independent
  `Task.Run`s. A citation render that won published every key as
  unresolved PERMANENTLY — same-path `NoteChanged` is a no-op and
  `ApplySeedOutcome` does not re-query, so nothing repaired it.
  Fixed: seeding runs before the first note selection and both leaves
  gate their background work on its completion.
- `JumpToBibliography` read `Entries` two statements after
  `EnsureLoaded` merely STARTED the load, so the first Ctrl+J
  announced "Searching for" and never set focus. Fixed: the leaf
  decides the outcome, at publish time when deferred.

Also corrected in passing: membership is asked of the loaded set, not
the visible rows — those are filtered by the search box and capped at
`MaxEntryRows` (contract 9 / D-3), so a present entry could be
reported missing whenever the search box had text.

### Round 2 (2026-08-04, high effort) — 4 highs + 1 low, MOSTLY AGAINST THE ROUND-1 FIX

Open, pending the design pass: see the six decisions above. I1
(stale-on-failure) is confirmed and documented above as pre-existing.
The low finding is the test-seam critique, decision 6.

**Protocol note:** this round ran at `high`, not the `xhigh` the
protocol specifies. The findings stand but the round was one tier
below conformance.

## Rules for future changes

- Contract numbering is per-wave. Never grep `src/SlateWindows`
  wholesale for "contract N" — W4-4 and W4-5 collide.
- Before any further review round, cite these contract numbers and
  paste the accepted-risk register inline, per protocol step (2).
- Concurrency work here checks `21_write_intent_protocol_invariants.md`
  first, per protocol step (4).
