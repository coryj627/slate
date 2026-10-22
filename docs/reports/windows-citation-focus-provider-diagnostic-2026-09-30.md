# Windows citation-focus provider diagnostic

October 1, 2026, continuation of the September 30 Windows investigation. The final normal CI revision `8c8a4c55c6124402c31d1e0f252c997960d35146` failed one hosted shell fact: `CitationSurfaces_GridsSheetsAndChords_AreClean`, source line 3478. Reading `HasKeyboardFocus` from the retained resolved citation row raised UIA_E_ELEMENTNOTAVAILABLE (`0x80040201`). It was not a reported ten-second focus timeout. The artifact has no fresh logical row or actual focused-element snapshot at that failure, so it cannot establish whether focus was genuinely lost or an obsolete proxy was queried.

Two bounded local runs passed. They do not explain the hosted failure or establish a fix. The unmodified frozen fact passed 1/1 in 27.0817 seconds test duration / 27.5541 seconds complete run. A temporary copied fact with read-only identity/provider/focus snapshots passed 1/1 in 27.1769 seconds test duration / 27.6635 seconds complete run. Both used the existing frozen executable/native bindings, without a product rebuild, source edit, setting change or NVDA intervention.

## Identity and observations

The fixture contains one resolved Knuth citation, generated from `knuth1984` in its disposable bibliography. The diagnostic's fresh logical row predicate matches the existing test: ListItem under `PanelCitationsList` whose Name contains `Knuth`, using OrdinalIgnoreCase. Exactly one row matched in the observed snapshots. RuntimeId describes the physical provider instance; it is not the logical citation identity and need not survive a legitimate row-container republish.

The test refreshes `resolvedRow` once after the preceding Ctrl+S republish and then retains that UIA proxy across the details sheet, DOI navigation and accessibility audit. Its close assertion polls this retained proxy. Instrumentation did not reassign it, catch-and-retry a stale proxy, replace its assertion, set focus, or perform any input beyond the original fact. Read-only snapshots were inserted before Escape, after `CitationDetailsSheet` disappeared and after the original focus assertion passed or failed. A failure would be logged and rethrown. Each property has separate availability/exception metadata, including nested COM HRESULTs; a missing provider is not reported as false focus.

For the instrumented fixture, process PID 40488:

| Phase (UTC) | Retained / fresh logical row | Actual UIA focus | Native foreground |
| --- | --- | --- | --- |
| Before Escape, 04:02:31.8392721 | Both RuntimeId `[7,40488,29848896]`, HasKeyboardFocus=false | Close button, AutomationId `CitationDetailsClose`, RuntimeId `[7,40488,28525799]` | PID 40488 |
| Sheet disappeared, before original assertion, 04:02:32.3248589 | Same RuntimeId on both, HasKeyboardFocus=true | Same Knuth ListItem and RuntimeId, HasKeyboardFocus=true | PID 40488 |
| Original assertion passed, 04:02:32.3946147 | Same RuntimeId on both, HasKeyboardFocus=true | Same Knuth ListItem and RuntimeId, HasKeyboardFocus=true | PID 40488 |

The citation row name was `Citation: Knuth 1984`, AutomationId empty, ControlType ListItem, IsKeyboardFocusable=true, IsOffscreen=false. All retained/fresh/focused property reads were available with no recorded COM errors. The owning test thread was native 18144 / managed 11, COM MTA, qualifier 0, CoGetApartmentType HRESULT 0. The snapshots are sequential UIA reads, not an atomic state capture or a continuous focus trace; read timing can affect a race. They establish provider availability and UIA keyboard focus in this passing local schedule, alongside native foreground ownership.

NVDA remained PID 12792/start `2026-10-01T01:38:46.4500735Z` before and after both runs. This is process continuity evidence, not human speech/focus acceptance.

## Residual and next proof

No row retirement occurred in the observed return boundary: retained, freshly found and focused rows kept one physical RuntimeId. Consequently these local passes cannot show that the CI old proxy was stale while a replacement was correctly focused, nor that a fresh replacement lacked focus. Do not classify the hosted result as a harmless flake or infer an OS cause from these passes.

The independent WPF source review identified the relevant legitimate retirement schedule: Citations Publish clears rows and adds fresh rows; the product permits republish while details are open and restores focus via a retained ListBoxItem token with a selected-row fallback. A separate controlled real-WPF witness is being prepared by the WPF reviewer to republish the same logical key under the sheet, close with actual Escape, and distinguish old-provider unavailability from current logical-row focus. That witness is separate evidence; its outcome is not asserted here.

Until provider retirement/focus behavior is proved and the final check repaired, the hosted normal shell gate remains red: 134 passed, one failed, 135 total. The two passing local facts do not supersede that complete final-revision failure.

## Evidence

Local evidence is under `C:\Users\cory\AppData\Local\Temp\slate-citation-focus-probe-2026-09-30`:

- `unchanged-citation-focus.trx`, `unchanged-evidence/`: original frozen fact and its existing accessibility artifacts.
- `instrumented-exact-citation-focus.trx`, `instrumented-exact-trace.jsonl`, `instrumented-exact-evidence/`: exact assertion with read-only snapshots and original artifacts.
- `instrumented/`: copied test sources, project and diagnostic helper for review; bin/obj are excluded from the selected manifest.
- `nvda-before.json`, `nvda-after-unchanged.json`, `nvda-after-instrumented.json`: unchanged screen-reader process identity.
- `probe-summary.json`, `evidence-manifest.json`: measured snapshots, outcomes, selected-source/evidence SHA256 and frozen production/test binary hashes.

The original hosted failure and independent parser evidence remain separately under `slate-windows-implementation-2026-09-30/ci-shell-8c8a4c55`. No remote rerun was requested. The UI slot was released to the independent WPF retirement witness after both local test-owned processes exited.
