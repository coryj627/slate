# Windows palette publication waits for queued input

The palette now queues every WPF candidate publication at Background priority. Input already queued can advance the query generation or dismiss the palette before the existing publication guard runs. Rows and selection still publish together; a genuine selection change still announces, initial opening remains silent, and the filter count retains its 150 ms trailing window. Non-WPF synchronous-owner and custom synchronization-context delivery remain unchanged.

The original worker handoff used SynchronizationContext.Post. WPF's default DispatcherSynchronizationContext posts at Normal priority, above Input, so a prefix could publish and announce its replacement selection before a later key already queued was dispatched. The count already used Background priority; the selection did not share that protection. See [WPF's synchronization-context implementation](https://raw.githubusercontent.com/dotnet/wpf/main/src/Microsoft.DotNet.Wpf/src/WindowsBase/System/Windows/Threading/DispatcherSynchronizationContext.cs).

The first minimal patch yielded off-owner publications but retained a same-owner fast path. Independent review identified a second legal schedule: the production worker finishes before its await checks completion, and the continuation resumes on the owner. A second deterministic case reproduced that bypass. WPF dispatch now precedes the same-owner fast path, covering both schedules without changing the intended synchronous fixtures, which explicitly capture no dispatcher context.

## Regression and evidence

AWorkersPublicationWaitsForTheQueryAlreadyQueuedAheadOfIt is a two-case Theory over the actual WPF dispatcher, actual CommandPaletteWorkLane, and real core ranking. A test-only wrapper controls when the real worker result returns. The off-owner case parks and releases the prefix rank, observes RankCompletion without pumping, then queues the final query at Input priority. The completed-task case waits without pumping for the real worker result, queues Input, and returns the completed task to its owner. Timeouts bound failures; they do not establish the ordering.

Both cases assert opening silence, a deliberate Tasks Review selection announcement, unchanged rows before input is dispatched, only final Quick Open rows and selection, cancellation of the prefix count window, and exactly one final selection followed by one final count. The finally path releases its owned gate, shuts down, observes lane/rank/count work and queued handoffs, then disposes registrations and gates. Existing fixture assertions were preserved.

| Product state | Native regression result |
| --- | --- |
| Original Normal handoff | RED: expected only Quick Open; published New Note / Quick Open / Toggle Bold first, then Quick Open. |
| Background off-owner, owner-first fast path | Off-owner case PASS; completed-task case RED: prefix rows published while inputRan remained false. |
| Final dispatcher-first Background handoff | GREEN: both regression cases pass; full CommandPaletteTests class passes 99/99. |

Both RED common Release builds succeeded with zero warnings/errors. Native test runtime was .NET 10.0.12 x64. The original RED fact failed in 81 ms. The second run passed the off-owner case in 85 ms and failed the completed-task case in 14 ms; failures were behavioral assertions, not setup or cleanup failures.

Exact source copies, test/product/native binary copies and SHA-256 hashes, common-build logs, the generated evidence-compilation manifests, and TRX files are preserved locally:
- [Original RED](C:/Users/cory/AppData/Local/Temp/slate-windows-implementation-2026-09-30/palette-red/manifest.json)
- [Completed-task RED](C:/Users/cory/AppData/Local/Temp/slate-windows-implementation-2026-09-30/palette-owner-red/manifest.json)

These runs used the working tree based on 1107ebc3c2e7a48c6129731c13517d8eed3768f4; the manifests distinguish the exact modified sources and binaries from that base commit. Root coordinates final build/projection manifests and all desktop tests.

## Final verification

The root-coordinated final common Release build and formatter verification passed after palette and Canvas source/projection freeze. Native verification then used those exact binaries with --no-build:
- The new two-case Theory passed 2/2 in 0.7847 s total; off-owner case 79 ms and completed-on-owner case 12 ms.
- The entire CommandPaletteTests class passed 99/99 in 18.1269 s, with zero failed or not-executed cases. This includes the Lane, Invoking and View partials, four count-versus-queued-input cases, opening silence, deliberate/replacement selection, pending-row Enter, custom owner context, failed ranks, modal seals, teardown and the shipped results list.

No existing assertion was removed or relaxed. Source/binary SHA-256 checks after the full class match the pre-run manifest. The tests ran on .NET 10.0.12 x64 with approved native WPF access. This is deterministic dispatcher and class evidence; it does not claim physical or human screen-reader acceptance or a final full-application rerun at the new source state.

- [GREEN source/binary/compiler manifest](C:/Users/cory/AppData/Local/Temp/slate-windows-implementation-2026-09-30/palette-green/manifest.json)
- [Two-case GREEN TRX](C:/Users/cory/AppData/Local/Temp/slate-windows-implementation-2026-09-30/palette-green/palette-dispatcher-green.trx)
- [Full class TRX](C:/Users/cory/AppData/Local/Temp/slate-windows-implementation-2026-09-30/palette-final-class/palette-final-class.trx)
- [Parsed counts and unchanged-hash checks](C:/Users/cory/AppData/Local/Temp/slate-windows-implementation-2026-09-30/palette-final-class/verification-summary.json)

## Scope of the FlaUI finding

The earlier full desktop suite heard Selected: New Canvas, Selected: Split Down (with its availability clause), and the correct final count for split. Its isolated unchanged rerun passed. The fixture marks before palette opening and lacks open/typing/query-generation boundaries, so those callback times alone do not establish which transition produced New Canvas. Opening is contractually silent and does not excuse the extra selection. The deterministic regressions establish two actual publication-order defects; they do not retroactively prove causation for that specific desktop event or classify it as harmless flakiness.

FlaUI v5 Keyboard.Type loops characters; unmodified lowercase typing has no intentional per-key delay, but each keydown/up is a separate SendInput call, so that journey's burst is not atomic. See [the exact FlaUI implementation](https://raw.githubusercontent.com/FlaUI/FlaUI/v5.0.0/src/FlaUI.Core/Input/Keyboard.cs). Physical screen-reader acceptance and exact desktop-listener diagnosis remain separate from the deterministic dispatcher results.
