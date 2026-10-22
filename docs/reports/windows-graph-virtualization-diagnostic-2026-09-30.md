# Windows graph virtualization diagnostic

Recorded 2026-10-01. The initial diagnostic made no tracked change. The evidence-backed test synchronization repair and validation are recorded below; no product behavior or OS settings were changed.

Normal Namespace Windows CI on `be6b0065b247ec09897c891ddfec37b69c3eba1c` failed only `GraphTableTests.TenThousandRowsStayVirtualisedAndTheActionInventoryStaysThree`: 104 live containers after page 19, against 100 (18 viewport rows + 2 cached, multiplied by five), duration 3.4049134 s. This fact and the production graph grid were unchanged from the base. The failure contains no cleanup-state trace, so its specific cause remains unobserved.

## Source findings

`GraphTableTests.cs` reads the initial viewport/cache, asserts Standard item virtualization, and jumps 500 rows twenty times. Each jump calls `UpdateLayout` and one `PumpedDispatcher.Drain` Background sentinel before asserting the original five-capacity ceiling. That sentinel does not establish a dispatcher fixed point or completion of future timers. The final assertion also uses the paging ceiling, although the nearby comment claims the final count is tested against capacity plus two.

The historical TGA-13/TGA-14 ledger in `docs/plans/35_graph_contracts.md` explicitly describes five capacities as an empirical allowance above a locally observed cadence. Its assertion that cleanup does not use a timer is overgeneral: a below-threshold count can remain after pumping, while other schedules defer cleanup using a timer. The earlier mutation evidence is historical; this investigation did not rerun that mutation.

The production `GraphTableView` uses `AccessibleDataGrid`, which enables row virtualization in Standard mode. No graph-specific retention or cleanup cancellation was found.

In [WPF v10.0.12 VirtualizingStackPanel](https://raw.githubusercontent.com/dotnet/wpf/v10.0.12/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/VirtualizingStackPanel.cs), Standard cleanup begins at Background priority. A pending cache measure postpones cleanup; cleanup exceeding its 50 ms slice can leave more work. Those continuations use a 500 ms dispatcher timer. Cache passes and scroll completion can also post further Background work. Cleanup ordinarily runs when extra children exceed twice the current extended viewport, leaving counts up to three such viewports legitimate. Focus, the bring-into-view container, cancellation, nonvirtualizable containers, and a held mouse button can affect removal. Consequently neither a single sentinel nor timer expiry alone proves a tight resting count. Relevant source regions: 3968–4051, 8105–8132, 8272–8408, 8552–8636, 8726–8738.

## Bounded observations

A Temp-only console harness extracted the original fact, its Host/STA/dispatcher/vault helpers and its unchanged assertions. It references frozen product DLLs without a project reference, so building it does not regenerate or build the product. Read-only snapshots record loading/unloading counts, the live row set, attached visual rows, viewport/cache, cleanup events/cancellation, mouse/focus and private WPF scheduling state. No private cleanup, focus repair, cache reset or settings change is used.

If an original ceiling assertion fails, the harness captures that failure first, stops further scrolling, observes only natural dispatcher cleanup for at most three seconds, then rethrows the original RED. Neither of the two authorized identical attempts reached that branch; no further attempts were made.

| Observation | Attempt 1 | Attempt 2 |
|---|---:|---:|
| Original fact result | PASS | PASS |
| Whole harness time | 4.293554 s | 4.056856 s |
| Viewport / cache / original ceiling | 15 / 2 / 85 | 15 / 2 / 85 |
| Peak before drain / after drain | 70 / 54 | 70 / 54 |
| Final live containers | 16 | 16 |
| Total loaded / unloaded | 392 / 376 | 392 / 376 |
| Snapshots with count disagreement | 0 of 43 | 0 of 43 |
| Observed cleanup cancellation | 0 | 0 |

Every live event count agreed with both the tracked live row set and attached visual rows. All sampled mouse states were Released and no sampled row held keyboard focus. The original distant-container, both-row-kinds, total-realization and three-action-inventory assertions passed.

Both passing traces independently show that a successful Background drain can return with pending cache measurement and cleanup operations. Neither sampled a delayed-cleanup timer. More decisively, after page 19 both retained 54 actual attached rows with an extended viewport count of 18, no pending cleanup/cache/scroll-clear operation, and no timer. That is consistent with WPF's strict threshold and does not satisfy the comment's capacity-plus-two promise (19 locally). The final jump crossed the threshold and naturally removed old rows; its final count alone does not establish a general tight resting invariant.

## Remaining proof

No observed failure justifies a product repair or a wider ceiling. The hosted 104/100 failure still needs the same trace at its first violation. A natural decrease after a pending 500 ms continuation, with row/event counts agreeing and no retention exemptions, would establish a settling mismatch. Persistent excess after the relevant operations settle would instead require examining dynamic capacity, protected/cancelled rows, generator retention or event accounting. Below-threshold retention must not be mistaken for an unfinished cleanup pass. The five-capacity number is not a documented WPF scheduling guarantee.

The frozen harness can collect that missing evidence in the hosted environment while keeping the original failure RED. An eventual test change should name the supported capacity and cleanup condition it actually verifies; blind retries, arbitrary sleeps and a larger unexplained multiplier do not supply that proof.

## Evidence and commands

Evidence directory: `C:\Users\cory\AppData\Local\Temp\slate-windows-implementation-2026-09-30\graph-cleanup-probe`.

- `GraphCleanupProbe.csproj`, `GraphTableTests.cs`, `GraphCleanupObserver.cs`, `Program.cs` and copied helper sources: executable diagnostic setup.
- `input-manifest.json`: source HEAD and SHA-256 of original inputs, frozen DLLs and harness sources.
- `attempt-1/console.log`, `attempt-1/graph-cleanup.jsonl`, `attempt-2/console.log`, `attempt-2/graph-cleanup.jsonl`: both outcomes and monotonic snapshots.
- CI original TRX: `C:\Users\cory\AppData\Local\Temp\slate-windows-implementation-2026-09-30\ci-app-be6b0065\results\slate-windows-app.trx`.

Build: `dotnet build <evidence-dir>/GraphCleanupProbe.csproj -c Release --nologo` (final build zero warnings/errors). Run with `SLATE_GRAPH_PROBE_OUTPUT` set to the attempt directory: `dotnet <evidence-dir>/bin/Release/net10.0-windows/SlateWindows.Tests.dll`. Both native executions exited zero; their owned windows and STA helpers cleaned up. Desktop ownership was returned to the parent. No tracked source, product binary, OS, NVDA or shortcut setting was changed.

## Reproduction and repair after authorization

The subsequent user request authorized fixing the CI failures. One controlled real `CleanUpVirtualizedItem` callback occupied 70.002 ms, exceeding WPF's 50 ms cleanup slice without cancelling any removal or modifying private state. The unchanged fact then failed **87 attached rows against 85 after page 3**, reproducing the earlier hosted 89/85 boundary class. Event live counts and actual attached rows agreed. The trace recorded an enabled 500 ms cleanup timer. After scrolling stopped, natural cleanup reduced 87 to 18 at the first snapshot 325.869 ms after the failure; no cleanup or cache work remained by the next snapshot. The original failed assertion was rethrown, exit 1. No cancellation, focused row or held mouse explained the excess.

This establishes a real deferred-cleanup schedule that defeats the original single-frame assertion. The exact 104/100 hosted schedule is still unobserved; no claim is made that its internal timer state was captured.

The regression was then added to `apps/slate-windows/tests/SlateWindows.Tests/GraphTableTests.cs` before the repair. Against the unchanged synchronization it failed the same **87/85 after page 3** predicate in 1.7831042 s. The repair keeps the initial **capacity + 2** and paging **capacity × 5** limits. Only when the event live count exceeds the latter limit does a local dispatcher frame wait for actual row-unload/layout progress, with a finite three-second deadline permitting WPF's 500 ms continuations. A queued Background check runs after container removal; the wait does not force private cleanup, sleep a fixed interval, retry the whole test, or enlarge the ceiling. After each completed paging stage, event counts must also equal attached visual `DataGridRow` counts. Current geometry, initial geometry, mouse state and focus are included on failure.

Both positive facts retain all population, row-kind, distant-container, unloading, total-realization and three-action-inventory checks. The documented one-item cache on each side and Item cache unit now have explicit assertions. A real public cleanup-cancellation control proves that a grid which refuses all unloading cannot pass the new synchronization: it must hit the specific bounded-cleanup failure with `unloaded 0`. The existing shared `PumpedDispatcher` helper and all product sources remain unchanged. The owned deadline, event handlers and pending check are cleaned up in `finally`, and showing the owned window is covered by its existing cleanup block.

Validation after scoped whitespace formatting and a Release build with zero warnings/errors:

| Witness | Result | Duration |
|---|---|---:|
| Forced cleanup slice before repair | RED, original 87/85 failure | 1.7831042 s |
| Original 10k fact after repair | PASS | 3.7311128 s |
| Forced cleanup slice after repair | PASS | 4.2606700 s |
| Never-unloading rejection control | PASS, expected bounded failure observed | 4.6412085 s |
| All GraphTable partials + AccessibleDataGrid closure | PASS 130/130, zero skips/failures | 59 s |

`git diff --check` passed. Independent read-only review found no blocker in the wait lifetime, unchanged ceiling, strengthened premises or refusal control. Desktop and build ownership were released to the parent after the closure. No additional rerun was made.

Additional evidence under `C:\Users\cory\AppData\Local\Temp\slate-windows-implementation-2026-09-30`:

- `graph-cleanup-probe/controlled-cleanup-red-1/console.log` and `graph-cleanup.jsonl`: original failure plus natural convergence; the executable harness accepts `SLATE_GRAPH_SLOW_CLEANUP=1`.
- `graph-deferred-cleanup-red/graph-deferred-cleanup-red.trx`: tracked regression before repair.
- `graph-deferred-cleanup-green/graph-deferred-cleanup-green.trx`: focused three witnesses after repair.
- `graph-table-closure-green/graph-table-closure-green.trx`: full 130-test closure.

Build: `dotnet build apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --no-restore --nologo`. Native tests use `dotnet test` on the same project with `-c Release --no-build --no-restore`; the closure filter is `FullyQualifiedName~SlateWindows.Tests.GraphTableTests|FullyQualifiedName~SlateWindows.Tests.AccessibleDataGridTests`. Normal hosted validation remains the parent's next acceptance step.
