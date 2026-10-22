# Slate testing strategy audit

September 30, 2026. Source audit at `39dd2457ace1c8358aa9c954604dfb0e1437d6b0`.

## Judgment

There is a real alignment problem, but it is more specific than “too many tests.” Slate has unusually strong domain, FFI, lifecycle, state-transition and architectural verification. It has spent substantial effort proving that declared contracts, source sites, generated tables and test evidence agree. The remaining risks that matter most to an accessibility-first desktop app are whether a user can actually finish a task, keep useful focus, hear the right result at the right time, and trust the bytes on disk. Those outcomes need more weight in the verification portfolio. Paying for a faster runner cannot repair that mismatch.

There are also two concrete coverage gaps in the current workflow configuration: the expanded Windows stress tier has no scheduled automated execution, despite its helper saying nightly runs it; and Mac tests have document/fixture dependencies omitted from their trigger paths. These should be fixed before reducing test frequency.

This audit read workflows, project configuration, representative behavioral and source tests, supporting infrastructure, the model inventory and assertions, and recorded real-AT evidence. It did not run builds or product tests, trigger CI, change workflows, remove scenarios or change repository settings. Per-test duration, mutation-detection value and current AT defect status were not measured. The audit is a portfolio assessment, not a claim that every source census is redundant or that historical defects remain unfixed.

## What the current layers accomplish

| Layer | Actual witness | Assessment |
| --- | --- | --- |
| Rust ARM Linux nextest | Domain/FS behavior, randomized censuses, real-FS persistence, isolated root-listing performance guard, doctests | Strong, appropriately structured. `ci` optimization keeps overflow/debug checks; tmpfs is limited to logical censuses; durability remains on real FS. |
| Windows Rust | Windows filesystem semantics, case-insensitive identity, reserved names, long paths, CLI, Windows ARM64 cross-compile | Justified second platform witness, not interchangeable with the Linux suite. Cross-compilation is weaker than native ARM64 behavior and must remain labeled as such. |
| Windows WPF/FFI tests | Real native sessions plus in-process state/UI/peer behavior, lifetime/concurrency/error/cancellation checks | High value. Calling this entire assembly “unit tests” understates its integration cost and process-global isolation needs. |
| Connections model | 13,069 reachable routes/reroot/composed scenarios, fresh workspaces, prescribed concurrency schedules, state/ordered announcements/focus/load assertions | Valuable exhaustive state coverage. Full cardinality is a coverage contract, not an outcome metric for the whole app. Repair the harness wait before questioning its size. |
| Windows Roslyn/source/doc censuses | Central funnel usage, symbols and construction sites, inventories, contract/evidence tables, help parity, historical ledger ancestry | Some are important architecture checks; others are historical/document governance. Their current packaging makes unrelated input changes purchase full behavioral execution. |
| Windows FlaUI + axe | Real WinExe, UIA patterns, keyboard routing/focus and accessibility structure on an interactive desktop | Load-bearing delivery witness. Its separate process-owning host is intentional and correct. UIA/axe success still does not establish actual NVDA/JAWS speech behavior. |
| Mac XCTest | Native state/render/command behavior, FFI parity, source censuses, performance tripwires | Useful broad coverage. Source presence checks deserve individual review; native behavior should be distinguished from source conformance. |
| Mac SwiftSyntax accessibility analyzer | Static SwiftUI anti-pattern analysis and a 100-point/zero-error gate | Useful lint. It cannot certify VoiceOver speech, keyboard completion, dynamic type reflow or motion behavior. |
| Nightly | Rust 400-seed tier (where honored), real-FS non-census suite and full thin-LTO benchmark compilation | Good existing tiering, but currently Rust-only. Benchmark compilation proves executables build; it does not measure performance regressions. |
| Recorded AT passes | Actual NVDA/VoiceOver speech and task completion, human acceptance residuals | Closest to product acceptance. These are external/manual evidence in this repository rather than a current scheduled CI lane. |

Evidence: [Rust workflow](../../.github/workflows/rust.yml), [Windows workflow](../../.github/workflows/windows.yml), [Mac workflow](../../.github/workflows/swift-tests.yml), [static analyzer](../../.github/workflows/a11y-check.yml), [nightly](../../.github/workflows/nightly.yml). Repository-relative links in this report are intended for source browsing; see the source index below for precise locations.

## Concrete findings

### 1. User-level failures are a better portfolio signal than the test census

The September 22 production NVDA matrix pass recorded fourteen headline findings, plus focus-loss cases and lower-severity findings. They include a Files tree that could not be walked, unnamed rows, template-sheet keystrokes reaching the background editor, a tag filter emitting the wrong core grammar, external changes not appearing, and result text present in a pane name but never spoken. This historical record demonstrates how source/renderer/peer correctness can coexist with failure at the assembled task boundary. It explicitly calls itself agent evidence rather than acceptance; speech was recorded from logs, no human confirmed audibility, and no human NVDA/JAWS acceptance cell was filled. Some rows record subsequent fixes, and F14 was a checklist error, so the fourteen rows are not fourteen current open bugs or fourteen valid test failures.

The corpus test's useful guarantee is that equivalent canonical events render equivalent text/priority through each binding. Its comment that both hosts “speak identical announcements” reaches beyond what it actually observes: constructing an event and calling `a11yRender` cannot observe listener registration, delivery, AT rate limiting, focus or speech interruption. Preserve that corpus; put a separate delivery/task assertion next to it.

A practical acceptance slice is keyboard-only open → navigate → edit/save → disk/readback → switch/restore → recover from failure, plus representative cancellation, pending async work and real speech/focus observations. The existing VoiceOver runbook already requires actual utterances plus disk probes for mutations. Apply that outcome discipline to automation wherever achievable, and retain human NVDA/JAWS/VoiceOver residuals with version/commit evidence.

References: `docs/plans/18_windows_port/reports/nvda_agent_matrix_pass_2026-09-22.md:1–43`; `apps/slate-mac/Tests/SlateMacTests/A11yCorpusCensusTests.swift:8–20`; `docs/runbooks/voiceover-feature-test.md:1–4,83–87,129–145`. No workflow in the current `.github/workflows` inventory executes NVDA, JAWS or VoiceOver.

### 2. Windows expanded stress coverage has no automated home

`Support/Recorders.cs:185–196` states that `SLATE_CENSUS_FULL=1` selects the full nightly tier. Windows tests use this switch for cancellation attempts/vault size, callback churn, native buffer/session/registry/layout lifetime counts, graph lifetime rounds and AvalonDocumentBuffer operations. The only workflow setting this variable is `nightly.yml:140`, whose job runs Linux Rust nextest. `windows.yml` never sets it. Therefore the current YAML does not schedule those expanded Windows branches.

This is a coverage gap, not a reason to inflate all PRs. Give the existing full Windows tier a scheduled/release home with complete evidence and measured duration, or explicitly retire its claim if it is not wanted. Keep the existing moderate per-PR layer and preserve the Windows-specific foreign callback/lifetime witnesses. A full tier on Rust cannot substitute for a full tier through the C# runtime, generated binding, WPF owner thread and Windows cleanup semantics.

### 3. Mac trigger paths omit inputs that tests consume

`swift-tests.yml:10–31` includes Mac sources, Rust crates/manifests/toolchain, the workflow and the Swift CLI. It excludes `docs/help/**` and `tests/fixtures/**`. However `SlateCommandsTests.swift:1307–1408` reads the Canvas/Graph/Bases help docs, and `A11yCorpusCensusTests.swift:35–45` reads `tests/fixtures/a11y/corpus.json`. A PR changing only these documents or the global corpus can skip the Mac consumer. The broad Windows workflow includes these paths, but its platform mirror is not a substitute for exercising Swift's construction/registry assertions.

Dependency mapping must first be conservative and complete, then selective. Add the actual omitted inputs; later split doc/source checks into dedicated execution where feasible. Do not narrow by directory name alone.

### 4. Governance checks and behavioral tests are tied together

The Windows workflow triggers Rust, app, both full model partitions and FlaUI for any `docs/plans/**`, `docs/help/**`, `scripts/**` or `apps/slate-mac/**` change. This was deliberately widened because the Windows censuses read those inputs. The dependency is real: `AtNavigationMapCensus` reads Mac sources and plans, `A11yTriggerParityCensus` parses a Markdown contract table, and `CanvasReconciliationCensus` pins twelve historical PR records, subjects and ancestry, 329 document keys, sixty verified entries, and counts of decisions/issue records. Its ancestry assertion also requires full Git history in the app and model build jobs.

The important distinction is between executing a source/doc census and re-executing every filesystem/state/UI test. A documentation change can require a contract/table verification without changing a Connections lifecycle route. A native frontend change can require cross-platform source parity without making every platform-sensitive Rust test relevant. Currently all are paid together.

Separate an explicitly named source/contract/document verification target from behavioral execution; share the exact built inputs when binding is required. Some source checks need .NET/WPF metadata and native projects, so this is not a promise that all move to trivial Linux text scans. Once the dependency map exists, use a fail-closed classifier that treats unknown inputs as broad changes, and retains aggregate failure/cancellation semantics. Require the final revision to have evidence for all applicable checks. Do not introduce an optional review label that produces a green skipped check.

Historical milestone-ledger checks merit an explicit owner decision: are they a permanent release invariant, an archive-validity check, or implementation-closeout evidence? A change to PR subject history is not the same risk as data loss. Keep archive checks if useful, but give them a separate name, runtime and failure meaning.

### 5. Some tests pin implementation spellings instead of the safety property

The census mechanism is not inherently bad. For example, symbol-bound enumeration proving that no alternate native notification raiser bypasses the central dispatcher catches a global architectural defect that a single injected sink can miss. Negative controls and mutation cases in the Windows censuses further improve their credibility.

But there are concrete implementation-coupled assertions. `AnnouncementSeamCensus.cs:100–129` pins exact normalized lambda/environment expressions and the exact peer-resolution initializer. Mac `BatchTrashQuarantineCensusTests.swift:264–375` asserts source strings such as `.onAppear`, `ProgressView()`, `.fixedSize(horizontal: false, vertical: true)` and `Text(reason)` while describing persistent recovery/status rendering. Its earlier funnel checks assert required code substrings in a stripped function body. These establish source presence/shape; they do not establish that the guard executes on every path, before the operation, with the right identity, or that the rendered status remains available after dismissal.

For each such assertion ask: what harmful behavior would it catch; can a harmless refactor break it; can a broken branch still satisfy it; and is there an independent behavioral witness? Preserve genuinely structural boundary checks, prefer symbols/control-flow where architecture is the property, and make persistent-state/guard-order/recovery outcomes behavioral when possible. Source check strengthening must not become its own endless substitute for product evidence.

This is an oracle/maintenance issue. The audit did not measure enough per-test timing to claim source censuses explain the expensive minutes; the measured Shutdown wait is a much stronger immediate cost attribution.

### 6. Exhaustive state coverage is strong, but belongs to one subsystem

The model crosses four following/pinned modes, two pane states, two active-leaf states, three root states, five presentations, three pending-work modes and 35 routes. Its exclusions are explicit, and actual assertions compare ordered event timelines, loads, retirement, root/depth/presentation, complete pin/back stack, shared selection, mount state and requested focus. The independent shard verifier proves a complete, disjoint scenario union and rejects inventory changes. This is substantial correctness testing rather than 13,069 copies of an arbitrary assert.

Its scenarios mostly prescribe transitions and parked concurrency points; they do not automatically cover arbitrary long operation histories, external edits, actual routed keys, live focus or AT speech. Composed model cases add history coverage, but the scenario total is not a certificate for the whole application. Preserve it while repairing the 288 pinned/in-flight Shutdown cases that wait on the production bound because their harness releases the parked fetch too late. Keep a focused deliberate timeout-contract witness; ordinary route cells should observe retirement and then let canceled work finish.

Only after the measured harness repair should model cost motivate a smaller PR tier. Pairwise sampling is not an automatic substitute for these higher-order lifecycle interactions. Any reduction needs a documented equivalence/fault-detection argument, retained explicit race/boundary/known-regression witnesses, rotating histories/stress at a suitable frequency, and full final/release verification. Changes to product contracts and model-generation inputs must remain deliberate.

### 7. Process isolation is justified; whole-assembly serialization is a packaging tradeoff

The WPF assembly globally disables parallel execution because native live-object counters and timing assertions are process global. `StaThread` additionally cleans up the registered window classes left after a test thread exits and rejects subsequent work beside a timed-out live dispatcher. The interactive FlaUI host is separate to avoid racing the application's single-instance identity. These are evidence-backed isolation measures; enabling broad xUnit parallelism or reusing mutable sessions would weaken the tests.

The cost opportunity is a classified process boundary: extract pure non-STA logic and source governance where it is actually independent, keep real native/global-census and timing facts in controlled processes, and retain fresh mutable sessions. Measure before partitioning: more processes can shorten wall time while duplicating startup/build/setup and raising aggregate compute. Shards are not savings merely because a green check arrives sooner.

### 8. Performance assertions need a different evidence contract from correctness

The Rust root-listing guard is already isolated from the long filesystem census sweep. Mac InteractionBudgetTests explicitly call their absolute ceilings generous regression tripwires and separately invoke XCTest `measure`. Those choices are sound. However the Mac workflow uses `swift test --parallel`, so process scheduling/resource contention can still affect ceiling measurements. Windows in-process timing is serialized, but a timeout on the 10,050-paragraph accessibility range query can reflect actual user-impacting traversal cost as well as hardware speed.

Retain semantic completeness in ordinary tests and measure meaningful latency/allocations on controlled representative runners. Where possible add work-count/asymptotic assertions that are hardware-independent. Publish distributions and versioned baselines, distinguish setup timeout from product budget, and require memory/CPU measurements before runner sizing. Do not loosen the huge-document assertion merely to migrate to a smaller runner. Conversely, do not require high-end paid hardware just to keep a noisy one-shot microbenchmark green. Benchmark compilation in nightly does not replace actual benchmark trend evidence.

## Recommended operating model

Keep useful existing coverage while changing its execution contract in this order:

1. **Repair measured waste and improve evidence.** Shutdown barrier/cleanup; exact current Rust failure; app TRX/slowest/last-active/hang artifacts; cold/warm resource baseline. These have clearer causality than test deletion.
2. **Close the concrete coverage holes.** Missing Mac dependency paths and a Windows full-stress scheduled/release home. Correct misleading coverage comments.
3. **Create a test-purpose inventory.** For each family record user failure/property, layer, real inputs, fixtures/mutable/global resources, duration/cost, negative-control or known-regression evidence, and required cadence. Report actual tests/scenarios and durations separately.
4. **Separate governance from behavior.** Dedicated doc/source/evidence check, pure logic where feasible, controlled native/STA integration, process-owning desktop journeys. Introduce conservative dependency selection only with complete mapping and exact-revision enforcement.
5. **Rebalance toward outcomes.** Turn observed AT/task failures into small behavior regressions at the cheapest sufficient boundary, plus a representative real executable keyboard/AT journey that observes delivery/focus and disk state. Maintain human acceptance for speech etiquette and AT behaviors that automation cannot honestly certify.
6. **Evaluate selective/deep cadences from evidence.** Fast applicable feedback on PR updates; complete applicable final-revision checks; expanded stress/random histories/nightly and release AT matrix. Do not treat a change-frequency label as proof of correctness, and do not lower the complete model until its faults/risks and post-repair duration justify that change.
7. **Choose hardware and plan after measurement.** Free standard hosted lanes remain an option. A $250 Namespace plan can be rational if it buys demonstrated feedback time, reliability and local developer time on the remaining native lanes. It should pay for useful product evidence rather than accidental waits, duplicated setup or unrelated source-governance coupling.

Suggested decision measures: time to the first actionable failure; time to all applicable final-revision evidence; compute/storage cost per completed validated revision; cancel/retry wastage; flake/unclassified-failure rate; user-impacting escaped regressions; which test would catch each recorded real task failure; and freshness of AT/version/commit acceptance evidence. Raw test cardinality and code coverage alone cannot answer the alignment question.

## Source index

- `.github/workflows/windows.yml:119–162` — broad input triggers; `:438–446` app build/tests; `:631–645` duplicate model build and shard execution; `:664–701` complete union/rollup; `:704–770` separate desktop host and fail-closed final gate.
- `.github/workflows/rust.yml:244–294` — tmpfs logical censuses, real-FS suite, isolated performance guard and doctests; `:371–388` benchmark compilation.
- `.github/workflows/nightly.yml:47–166` — Rust-only full tier and real-FS/bench checks.
- `.github/workflows/swift-tests.yml:10–31,141–151` — trigger inputs and parallel XCTest.
- `.github/workflows/a11y-check.yml:34–52,87–103` — static source target, score and three reporting formats.
- `apps/slate-windows/tests/SlateWindows.Tests/AssemblyInfo.cs:4–18` — process global serial policy and dispatcher leak guard.
- `apps/slate-windows/tests/SlateWindows.Tests/Support/StaThread.cs:15–58,134–197` — resource cleanup, timeout unwind and contaminated-process protection.
- `apps/slate-windows/tests/SlateWindows.Tests/Support/Recorders.cs:185–196` — Windows stress tier switch/claim.
- `apps/slate-windows/tests/SlateWindows.Tests/Censuses/HandleLifetimeCensus.cs:72–108`; `HandleLifetimeCensus.Graph.cs:27`; `CallbackConcurrencyCensus.cs:22,221`; `CancellationCensus.cs:21–22`; `AvalonDocumentBufferCensus.cs:584` — expanded Windows tier consumers.
- `apps/slate-windows/tests/SlateWindows.Tests/Censuses/CanvasReconciliationCensus.cs:9–140` — historical ledger/full history/document table assertions.
- `apps/slate-windows/tests/SlateWindows.Tests/Censuses/AtNavigationMapCensus.cs:19–69` and `A11yTriggerParityCensus.cs:16–31` — cross-platform/document dependencies.
- `apps/slate-windows/tests/SlateWindows.Tests/Censuses/AnnouncementSeamCensus.cs:47–129` — useful symbol-bound census alongside expression pins.
- `apps/slate-windows/tests/EvidenceCompilation.targets:4–26` and `Support/TestEvidenceCompilation.cs:35–72` — exact inputs/hash/absolute-path build evidence; build artifact portability constraints.
- `apps/slate-windows/tests/SlateWindows.Tests/ConnectionsLeafTests.Model.cs:55–155,2030–2045,2100–2221` — model dimensions/census/driver/timeline/final-state/cleanup.
- `apps/slate-mac/Tests/SlateMacTests/BatchTrashQuarantineCensusTests.swift:61–87,264–375` — source string/shape checks.
- `apps/slate-mac/Tests/SlateMacTests/InteractionBudgetTests.swift:8–22,69–163` — regression tripwires and measurements.
- `apps/slate-mac/Tests/SlateMacTests/SlateCommandsTests.swift:1307–1408` and `A11yCorpusCensusTests.swift:8–45` — omitted Mac trigger inputs and rendering vs speech scope.
- `docs/plans/18_windows_port/reports/nvda_agent_matrix_pass_2026-09-22.md:1–43` — historical production task failures; later annotations distinguish fixes/checklist correction.
- `docs/runbooks/voiceover-feature-test.md:1–4,83–87,129–145` — real speech/task/disk outcome discipline and explicit human behavior residuals.
