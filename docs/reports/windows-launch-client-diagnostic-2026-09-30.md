# Windows launch-client capture diagnostic

The local launch capture remains unresolved, but the bounded probe rules out the suggested simple repair of moving this particular listener from STA to MTA. Its original xUnit owner was already COM MTA, and its actual automation RCW was `Interop.UIAutomationClient.CUIAutomation8Class`. A dedicated explicit MTA owner with an explicitly activated CUIAutomation8 group client reproduced the same zero-callback failure. A later separately labelled control received a real production notification through a direct handler on the existing window while its original desktop group remained empty. One other control attempt passed the unchanged prelaunch group sequence itself. These observations establish working managed ingress in some schedules, not a repaired capture or its cause. No tracked source, production binary, NVDA configuration/process or OS setting changed.

## Scope and method

The frozen production revision was `8c8a4c55c6124402c31d1e0f252c997960d35146`. A temporary copied accessibility project retained the complete original launch fact's fixture, prelaunch desktop-root/subtree event-handler group, three-second queue switch, initial untouched-window interval, exact four-line assertion and cleanup. Only diagnostic logging, a dedicated-owner branch and an absolute path to the frozen executable were added. For these original two probes, no post-window event subscription was added; the separately labelled late control below occurs only after recording the exact baseline observation. The instrumented copy built with zero warnings/errors from cached packages; the tracked tree remained clean apart from the private untracked reports directory.

Logging records native and managed owner threads, `CoGetApartmentType` type/qualifier/HRESULT, group creation/registration/removal, and callback entry before any cached sender-property read. Cached-property COM errors would have a separate HRESULT record. `IPersist.GetClassID` introspection was attempted and returned E_NOINTERFACE (`0x80004002`); this does not provide a runtime CLSID. Original activation evidence is the actual RCW type `CUIAutomation8Class`; the dedicated group client is CUIAutomation8 by explicit construction. The dedicated branch retains the original UIA3 query automation object as well as its separately owned group client; it is not evidence from an entirely single-client process.

Both runs failed their unchanged exact launch assertion. These diagnostic failures are retained, not converted into a passing accessibility test or substituted for the actual source test inventory.

## Observations

| Field | Original xUnit owner | Dedicated explicit MTA/CUIAutomation8 group owner |
| --- | --- | --- |
| Fixture | Announcements Vault 15055dba | Announcements Vault 621ffdc2 |
| Fact duration | 20.135s complete run | 20.111s complete run |
| Native registering/removing thread | 43336 | 19176 |
| Managed body owner | Thread 8, MTA | Thread 18, MTA |
| CoGetApartmentType | HRESULT 0, type 1 (MTA), qualifier 0 | HRESULT 0, type 1 (MTA), qualifier 0 |
| Registration complete UTC, October 1 | 03:38:29.3453688 | 03:39:17.8989606 |
| Removal complete UTC, October 1 | 03:38:48.8110700 | 03:39:37.3502236 |
| Registration/removal | Returned without COM exception on same owner thread | Returned without COM exception on same owner thread |
| Callback entries before sender-cache reads | 0 | 0 |
| Sender-cache failure records | 0, because no callback reached that code | 0, because no callback reached that code |
| Captured Slate / other-process notifications | 0 / 0 | 0 / 0 |
| Production queue drain | Four lines, droppedOldest=0, lost=0, advise absent, timeout fallback at 6496ms | Four lines, droppedOldest=0, lost=0, advise absent, timeout fallback at 6498ms |

Each app reported clientsListening=True, status provider null during the deliberate hold and then connected. The original listener remained registered until after its failed assertion; the dedicated listener was registered and removed on its dedicated owner. No evidence indicates an early removal or cached-sender classification failure.

NVDA remained PID 12792, start `2026-10-01T01:38:46.4500735Z`, throughout both probes. A read-only extraction from its existing log establishes fresh same-run delivery, independently of the older 02afad11 evidence:

- Fixture 15055dba: four NVDA UIA notification records at local 23:38:37.155–.167, kind Other / processing All, activity IDs `.1` through `.4`; four complete matching speech payloads in order at 23:38:37.171–.178.
- Fixture 621ffdc2: four corresponding notification records at 23:39:25.485–.502 and four complete matching speech payloads in order at 23:39:25.499–.515.

The NVDA notification log truncates the first display string after the fixture-specific prefix; its full text is not certified by that notification record. All four corresponding speech payloads are complete. This establishes app-to-NVDA notification identification and speech-payload delivery for these probes, not human audibility or acceptance.

## Remaining boundary and next work

The failing desktop-group observations localize that discrepancy before managed notification callback entry. It does not establish an OS, NVDA, provider, COM marshaling or client-coexistence cause. The existing Microsoft guidance to own UIA event clients on MTA and remove subscriptions on their registering thread is relevant, but these two particular listeners already satisfy those observed conditions.

The separate positive control below now establishes valid managed notification ingress for a late real production notification. Registration mechanism, target and timing all changed between the original desktop group and that direct window control; the difference cannot be attributed to only event groups or only prewindow timing. A next diagnostic matrix would compare direct-desktop/prelaunch and group-window/postlaunch while retaining the original baseline first. A minimal native client and a matched Windows 26100 image could further distinguish managed marshaling, image and provider boundaries if needed. No such broader experiment was performed here. Do not replace this assertion with a post-window listener or infer an OS cause solely from build numbers.

## Evidence

Everything is local under `C:\Users\cory\AppData\Local\Temp\slate-launch-client-probe-2026-09-30`:

- `original-xunit-trace.jsonl` and `dedicated-mta-cui8-trace.jsonl`: registration, COM apartment, owner and callback-ingress trace.
- `original-xunit.trx` and `dedicated-mta-cui8.trx`: unchanged exact assertion failures and the unique fixture expectations.
- Each mode's `*-evidence/announcements-launch.json`: original app drain/listener/provider diagnostics and empty capture.
- `nvda-before.json`, `nvda-after.json`, `nvda-same-run-fixtures-readonly.json`: process continuity and strictly fixture-bounded delivery extraction.
- `probe-summary.json`, `product-binary-hashes.json`, `evidence-manifest.json`: measured summary, frozen artifact hashes and selected temporary source/evidence hashes.
- Temporary copied source and project retain the actual instrumentation for review; bin/obj outputs are excluded from the selected evidence manifest.

## Separate late notification positive control

Three further bounded attempts used a temporary owner mode that preserves the exact original launch assertion before any new subscription. If that assertion fails, its message and zero-capture state are recorded first, then a separately managed handler is registered directly through `IUIAutomation5.AddNotificationEventHandler` on the actual Slate window, subtree scope, with cached process/automation IDs. The original desktop group stays registered. The late control uses the existing physical Ctrl+Alt+I Files-tree journey to obtain core's exact `Right pane hidden.` line, and afterwards rethrows the original launch assertion. A successful control never turns the failed baseline into a passing test.

The first attempt unexpectedly passed the complete original five-line journey in 11.1993 seconds: fixture `Announcements Vault 8b335b0e`, original group callbacks five, all cached process/automation IDs valid. The direct control branch was never entered and no direct registration was added. Both group owner and callback threads were COM MTA; registration/removal used native owner 40168. Original group registration returned at 03:47:08.8051072 UTC and removal at 03:47:19.3666758 UTC. This is a retained passing observation, not a repair, harmless-flake classification or explanation for prior empty captures.

The second attempt, fixture `Announcements Vault a920ba43`, retained the original exact `Heard[]` failure and an independently incremented group callback tally of zero. Its control preparation failed before direct registration because the temporary diagnostic tried serializing an IntPtr window handle, which System.Text.Json does not support. That diagnostic error and original failure are retained. The handle logging was corrected to a numeric long; no production code changed.

The corrected final attempt yielded a trustworthy separately labelled control:

| Boundary | Final observation |
| --- | --- |
| Fixture / production process | Announcements Vault 68839f40 / PID 14100 |
| Original owner | Native thread 20588; managed thread 8; COM MTA, qualifier 0, CoGetApartmentType HRESULT 0 |
| Original prelaunch group registration returned | 03:49:11.2519480 UTC |
| Exact baseline assertion failed, before direct subscription | 03:49:30.7029254 UTC; four expected launch lines, Heard[], originalReceived=0 |
| App baseline diagnostics | Four lines, droppedOldest=0, lost=0, timeout fallback, advise absent, drained at 6255ms |
| Late direct subscription returned | 03:49:30.7106781 UTC; actual HWND 32311614, CUIAutomation8Class, subtree scope |
| Late direct callback ingress | 03:49:31.0892234 UTC; native callback thread 40268, managed thread 18, COM MTA / qualifier 0 / HRESULT 0 |
| Exact late payload | Right pane hidden.; Other / All; activity slate-accessibility-announcement.5 |
| Cached sender | PID 14100; AutomationId VaultStatus; no cache error |
| Late assertion / counts | Passed separately; one direct callback, zero cache errors, original desktop group still zero |
| Direct removal returned | 03:49:32.2655640 UTC, same registering owner 20588 |
| Original group removal returned | 03:49:32.2677794 UTC, same owner, atomic callback tally zero |
| Test outcome | Original launch failure rethrown; TRX remains FAIL1/1, complete duration 21.6701 seconds |

NVDA remained PID 12792/start `2026-10-01T01:38:46.4500735Z` across these attempts. Fresh bounded log extraction records five notifications/five complete matching speech payloads for the first passing fixture; four/four for the control-preparation-error fixture; and five/five for the corrected final fixture, including the late line. The first notification text is still truncated in NVDA's own log; the matching speech payloads are complete. Delivery is distinct from human audibility/acceptance.

This rules out a blanket inability of this environment to deliver managed UIA notification callbacks. It preserves the narrower unresolved discrepancy between the observed desktop/prelaunch group path and direct/window/postlaunch path. Those paths differ in several factors, and the passing original-group observation prevents declaring that groups universally fail here.

Additional evidence under the same temporary root:

- `late-direct-positive-control.trx`, its trace and app evidence: original group PASS, no late direct subscription.
- `late-direct-positive-control-second.trx`, trace and app evidence: original failure plus explicit diagnostic setup error before direct registration.
- `late-direct-positive-control-repaired.trx`, trace and app evidence: preserved original failure and separately successful direct late control.
- `late-control-summary.json`, `nvda-control-attempt-fixtures-readonly.json`, and `late-control*-nvda*.json`: control details, bounded same-run delivery and process continuity.
- `pre-control-source/ShellAccessibilityTests.Announcements.cs.txt`: exact temporary pre-control source retained for the original two probes.

No further UI runs, source edits, settings changes or process interventions followed. The UI slot was released after cleanup.

