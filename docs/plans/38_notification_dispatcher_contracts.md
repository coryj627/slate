# W7-2 notification dispatcher contracts (#748)

This contract-only commit precedes implementation and review.
Scope: [W7 executable spec](18_windows_port/specs/w7_spec.md) section 3. The dispatcher is the only UIA raiser;
core owns copy and priority, while hosts own delivery timing.

## Contracts

**D-1 — Exact native delivery.** Every Medium event uses Other / All;
every High event uses Other / ImportantMostRecent. Both preserve the
rendered text and activity ID `slate-accessibility-announcement`. There
are exactly two priority enum values today; an exhaustive fact must fail
when another is introduced. Test the arguments reaching the native peer
boundary, not a second mapping implementation. Both Post overloads share
that boundary, and typed Post renders once through core.

**D-2 — One production raiser.** AccessibilityNotificationDispatcher is
the sole authored RaiseNotificationEvent caller. Extend the existing
AnnouncementSeamCensus: the real MainWindow still passes both typed and
rendered seams into the workspace and all family announcers retain the
same production destination. No default no-op sink can satisfy a test.

**D-3 — Typed scan identity and copy.** VaultScanStarted(total_files),
VaultScanProgress(indexed,total), VaultScanFinished(files_indexed) are
Medium core events. Their copy is exactly Scanning vault. N file/files to
index.; Indexed I of T file/files.; Scan complete. N file/files indexed.
Core owns singular/plural grammar. Windows and Mac construct the same
events and render through the existing funnel. Cancelled/failed scan
progress remains silent; existing failure UI is separate.

**D-4 — Host timing remains host timing.** The 350 ms injected-clock
minimum interval suppresses only progress. Start/finish always fire and
advance the guard; Reset re-arms it. No timer or wall-clock policy moves
into core. Existing progress mailbox ordering/bounding remains intact.

**D-5 — Per-family etiquette.** Audit all nine families against their
actual Mac paths. Pin scan, canvas, graph, sidebar, palette, search,
Quick Open and Bases with deterministic production-mode facts; list the
sync-marker watcher as refresh timing already owned by its existing tests.
Preserve per-surface cancellation/publication rules, and add no global
announcement scheduler. Sidebar dedup uses query and total. Palette text
includes query, so different queries with equal counts are different
announcements; an unchanged state must be silent. Search already dedups
summary text. Quick Open needs an injected delay to exercise its 60 ms
ranking window deterministically. Bases dedups text within one refresh
funnel and clears it for the next funnel.

**D-6 — One core coalescing-class inventory.** Canvas/graph class keys
remain defined by a11y.rs and its mirror checks. This change adds no
new coalescing class or duplicated classifier.

**D-7 — Complete trigger ledger.** Derive all top-level A11yEvent kinds
from the generated binding; Canvas and Graph refer to their existing
structural ledgers rather than duplicating them. Each remaining row has
role, both host construction members, Windows facts, observation class
and explicit designation where one platform has no construction. Validate
member-level evidence and missing/stale coverage in both directions.
Posted, label and dialog-copy are distinct; a corpus-render fact cannot
be mislabeled as a production user-journey observation. Generator output
and the committed table must agree. No generated binding is host evidence.

**D-8 — Exhaustive residue register.** Every HostComposed construction
is either removed by the scan conversion or names its existing engine
and accepted decision. Count actual sites separately from residue markers:
the current Windows 76 constructions have 52 markers, because family markers
cover several sites. The scan conversion removes 3 of each. Mac's marker
pin drops 28 to 27. Every designation is explicit and stale rows fail; this
is no blanket exemption for future sites.

**D-9 — Deliberate corpus delta.** Add singular/plural corpus rows for
all three scan events, regenerate the canonical JSON, then run clean.
Update the UniFFI enum/conversion plus both host mirrors in the same PR.
Rebuild Release DLL and generated bindings before Windows tests so they
exercise the new core rather than an old native binary.

## Upstream behavior pinned for D-1

NVDA 2026.1.1 release commit
`0af636a2f11d7aca74210fbf5f214a5aa41fab13`,
[source/NVDAObjects/UIA/__init__.py](https://github.com/nvaccess/nvda/blob/0af636a2f11d7aca74210fbf5f214a5aa41fab13/source/NVDAObjects/UIA/__init__.py#L2305).
Its event_UIA_notification branch explicitly selects:

```python
if notificationProcessing in (
    UIAHandler.NotificationProcessing_ImportantMostRecent,
    UIAHandler.NotificationProcessing_MostRecent,
):
```

That branch uses NOW during Say All, otherwise cancelSpeech, before
ui.message. All bypasses it. Human audible behavior under stock NVDA and
licensed JAWS stays in the separate checklist; a UIA parameter fact is
not a human listening result. Braille is owner-deferred; Narrator is W8.

## Etiquette evidence register (planned witnesses are explicit)

- Scan: ScanAnnouncementGateTests plus UiProgressListenerTests.
- Canvas: CanvasAnnouncerTests and CanvasAnnouncerCensus.
- Graph: GraphAnnouncerTests and GraphAnnouncerCensus.
- Sidebar: FilesSidebarViewModel.Filter.ApplyFilterOutcome lacks the
  Mac query/total guard; add it without erasing mutation status reassertion.
- Palette: CommandPaletteViewModel.Rebuild emits only after query change;
  review P10 alongside Mac clearFilterAnnouncement before modifying it.
- Search: SearchOverlayViewModelTests.DuplicateSummaryIsNotReAnnouncedButAChangedSummaryIs
  plus the injected 150 ms debounce facts.
- Quick Open: QuickSwitcherRankCoordinatorTests already exercise stale
  publication; add deterministic window and initial-count witnesses.
- Bases: WorkspaceViewModel.Bases.OnBaseMembershipChanged has per-funnel
  HashSet guard; strengthen the existing funnel fact to count duplicate
  same-summary membership notifications across surfaces.
- Sync-marker watcher: out of announcement scope, SyncMarkerWatcherTests.

## Dispatcher test boundary investigation

WPF AutomationPeer.RaiseNotificationEvent is public nonvirtual (verified
Microsoft reference assembly XML and API documentation). A subclass cannot
record it by overriding. Prefer a narrow constructor-injected native raise
delegate: the production FrameworkElement constructor supplies the only
RaiseNotificationEvent call and resolves its current peer at posting time;
the test constructor records the exact same argument tuple. No optional
no-op/default sink. Extend the existing source seam census to bind that
production constructor and ensure all MainWindow dispatchers use it.
Mapping tests cover every priority and both overloads. A corpus dispatch
fact may assert every canonical event's emitted tuple against the golden,
explicitly classified unit-observed at the dispatcher boundary rather than
claiming every user trigger was exercised. Existing per-family trigger
facts and actual journeys keep their narrower source/behavior role.


## Accepted scope and evidence rules

- A-1: Medium queues with All; High retains ImportantMostRecent. No new
  activity IDs, global scheduler, priority tier or coalescing class.
- A-2: Stock NVDA and licensed JAWS are independent human acceptance runs.
  Narrator belongs to the W8 smoke pass. Braille is owner-deferred as of
  2026-09-18; no braille or human audible result is inferred from automation.
- A-3: Existing platform differences remain explicit: W3 printing waiver,
  W8-owned help, contracts22 property source/recovery exclusions, contracts25
  D-18/D-19/D-20 builder/dashboard/fence decisions. Ledger designations must
  cite the applicable decision and shipped replacement route; unclassified
  missing constructions fail instead of acquiring a generic exemption.
- A-4: Engine-composed availability, recovery, structural-mutation and
  dialog-guidance residue is enumerated per site with its named engine.
  The register freezes current scope; new sites require a deliberate row.
  Scan progress is converted, not designated.
- A-5: Source evidence proves declarations and construction membership.
  Tests separately prove behavior and dispatch arguments. A unit-observed
  dispatcher tuple is labeled as such, never as a user-journey or audible
  result. The Mac parser retains the recorded SwiftSource/#1108 boundary;
  generated codecs and comments cannot manufacture host sites.
- A-6: Palette count copy includes the query. Equal counts for different
  queries remain different announcements under P10; the change-only check
  applies to the rendered state. No numeric-count-only suppression.

## Planned validation

AccessibilityNotificationDispatcherTests pins both Post overloads, every
priority tuple and the full canonical corpus at the native raise boundary.
AnnouncementSeamCensus extends its production-constructor proof and sole
raiser inventory. ScanAnnouncementGateTests retain timing and add typed
identity; the Rust corpus and both host mirrors pin singular/plural copy.
A11yTriggerParityCensus validates ledger coverage, source/fact references,
roles, designations and every residue site, with mutation witnesses.
The etiquette table names deterministic facts for every in-scope family.
The W-C notification cells and W7-2 human checklist are reconciled only
against the evidence actually collected.

## Review record

No review or implementation result is claimed by this initial contract.
