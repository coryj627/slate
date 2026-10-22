# Complete Windows pilot within independent job deadlines

Revision `4958e62e61d012d0a6924ec38433a283999f00e1` changes only `.github/workflows/windows-ci-pilot.yml` in [draft PR #1328](https://github.com/coryj627/slate/pull/1328). Native/C# products and tests are unchanged from `61b52bc5`; the graph fixes' normal CI passed completely at that preceding revision. Current complete acceptance must come from the new runs, not from those prior results.

## Observed failure

[Pilot 36864606986](https://github.com/coryj627/slate/actions/runs/36864606986) exceeded its 120-minute job deadline. App passed 4,806/4,806 in 1,616.526s, format and all earlier native witnesses passed. The model step ran 61m53 before cancellation. The independent coverage verifier and hosted shell were explicitly skipped, so this run is not full comparative acceptance.

Canonical reroot proves 544 cases completed at the unchanged digest in 188.836s, with zero missed progress writes. Its phases were fixture setup 4.483s, session setup 28.816s, arrangement 82.046s, drive 16.955s, settle/verify 0.711s and cleanup 55.489s. Routes last checkpoint recorded 10,366/12,388 completed, active ordinal 10,367 in arrangement, elapsed 3,521.775s close to cancellation. No canonical routes/composed reports or model TRX were written. The existing verifier correctly rejects the missing reports.

This supports continued throughput exhausting the serial build/app/model/shell job, not a frozen-cell hang. Normal model reports total 414.761s across the same inventory; losing two-way parallelism cannot explain the pilot's much longer summed work. Runner/storage behavior, checkpoint I/O and long-process resource growth remain hypotheses. No underlying hosted throughput cause is established or repaired here. Raw partial reports, cancellation annotations, full logs and verified source/tree provenance are retained under the earlier Temp evidence root's `ci-pilot-61b52bc5` folder.

## Workflow repair

The producer performs the same cold native build, ARM64/Rust/CLI witnesses, complete app phase and formatter. It publishes one Release output set with per-file lengths/SHA-256, native-copy equality, exact checkout revision, candidate, run and producer attempt. Consumers preserve the proven Windows relative output layout and verify every declared file and the complete output file set before execution.

Two model jobs use the existing normal global-ordinal partition, indices 0/1 and count 2, with fresh source checkout and the same producer binaries. They execute all three model facts without another native/C# build. Both complete canonical report sets must match exact producer/source/run/manifest/native digest; the unchanged independent verifier then proves all 13,069 cases exactly once and unchanged inventory digests. The hosted shell follows the producer independently of model completion, for every candidate, with its complete UIA/axe gate. The existing pilot aggregate requires producer, both model jobs, coverage verification and hosted shell success, rejecting failure/cancellation/skips.

Model jobs use the existing normal 90-minute job and 45-minute hang-evidence limits. Shell uses the existing 20-minute job and five-minute hang-evidence limits. All product budgets, graph row ceilings, case inventory, cold-cache policy and production provider/check identities remain. This changes scheduling/deadline ownership rather than reducing obligations.

Selective consumer retries reuse the pinned successful producer artifact; producer attempt and consumer execution attempt are recorded separately. Canonical artifacts are one run/shard report set, overwritten only when that shard executes again. Attempt-scoped diagnostics also preserve each attempt's canonical/partial checkpoints, TRX and dumps. A changed producer manifest invalidates earlier reports; failed latest jobs cannot create a green aggregate from old artifacts. Producer binary retention remains one day, matching the normal handoff policy.

## Local validation and current CI

Actionlint 1.7.12, YAML parsing, embedded Python AST and all seven inline PowerShell body parses passed. All 11 existing model-verifier tests passed. Independent review found no blocker, including selective retry provenance and artifact download patterns.

An actual local transfer smoke staged 449 compiled output files (1,465,843,925 bytes), accepted both model/shell consumers with producer attempt 1 and execution attempt 2, and rejected a changed binary. This tests transfer/provenance behavior only; no model or shell facts executed in that smoke. Logs/scripts/results are under `C:\Users\cory\AppData\Local\Temp\slate-ci-failure-repairs-2026-10-01`.

## Complete exact-revision CI acceptance

All current PR checks passed on `4958e62e61d012d0a6924ec38433a283999f00e1`. [Normal Windows](https://github.com/coryj627/slate/actions/runs/36883467437) and the [cold hosted pilot](https://github.com/coryj627/slate/actions/runs/36883467281) both completed every required app, formatter, model, shell and aggregate gate. Rust, Swift, audit, license, Semgrep and Codoki also passed. No earlier green result substitutes for this acceptance.

| Evidence | Normal Windows | Cold hosted pilot |
| --- | --- | --- |
| Complete app | 4,806/4,806 passed; TRX 1,272.284335s | 4,806/4,806 passed; TRX 1,926.811661s |
| Complete models | All 13,069 scenarios exactly once | All 13,069 scenarios exactly once |
| Model discovery | Each shard passed the same three model facts | Each shard passed the same three model facts, using transferred assemblies |
| Canonical inventory | Routes 12,388, reroot 544, composed 137; exact global ordinals and unchanged inventory digests in all six reports | Same complete inventory, exact ordinals and unchanged digests |
| Progress | Six complete successful checkpoints; zero missed progress writes | Six complete successful checkpoints; zero missed progress writes |
| Hosted shell | 135/135 passed; TRX 934.979828s | Same 135-name inventory, 135/135 passed; TRX 854.647716s |
| Axe | 58 scans, zero errors or waivers | Same 58-surface inventory, zero errors or waivers |
| Required aggregates | Success | Strict coverage and existing Native Windows pilot aggregate success |

App/model/shell TRXs have zero failures, skips, errors or timeouts. The original graph paging, real extra-cleanup slice, unload-refusal control, large-graph performance gate, citation provider-retirement regression and all four privilege facts passed in both app executions. Numeric hosted warm-tick/iteration output is absent from normal-verbosity logs; passing the unchanged budget assertion does not establish an exact hosted tick value.

All tested source is actual PR merge `b682434a80317480a0ef0d43860420f1becd28d0`, whose tree `bf2192e94b9592ed5481159df7b6b02a4d77c870` equals the head tree, with base `39dd2457ace1c8358aa9c954604dfb0e1437d6b0` and this head as parents. All axe source revisions match that tested merge.

The producer artifact independently verifies all **449 files / 1,465,769,935 bytes**, the exact file set, all seven required outputs and identical native DLL copies. Manifest SHA-256 is `8994f2eadc325354eb0d193f73d5ba012e018c3f3535de57ddc2347b1b210027`; native SHA-256 is `37f7f63d9456404ccbab8c5829a29b406eeedd72928d63b5bc8a8e20fa1f3263`. Both model consumers and the shell match producer run `36883467281`, producer attempt `1`, execution attempt `1`, source and both digests. Current CI exercises the first successful attempt; failed-consumer retry acceptance is supported by the local transfer smoke and static review, not claimed as an actual CI retry.

The producer completed at 16:33:15 UTC, hosted shell at 16:48:26, models at 17:18:06/17:18:27, strict coverage at 17:18:41 and final aggregate at **17:18:46 UTC on October 1**. Canonical routes-family execution took 2,522.414875s / 2,514.267266s. Actual TRX route-fact durations were 2,522.4314496s / 2,514.2983306s (approximately 42 minutes), below the existing 45-minute watchdog. Complete model TRXs took 2,630.000507s / 2,625.845139s. Hosted model throughput remains much slower than Namespace in these observations; this scheduling repair does not identify that cause or certify a provider cutover. Matched cold/warm measurements remain follow-up work.

Swift independently reports 3,032 XCTest cases; anchored and global log parsers observe 3,031 unique progress entries, ordinal 61 absent, no duplicates. This is a log evidence gap, not proof of missing execution. Linux Rust passed 2,765 selected nextest cases plus two doctests; Windows Rust passed 2,592 top-level cases, retaining the existing ignored fixture writer and separate nested-child record. Configured audit passed with three allowed warnings; it is not an assertion of zero advisories.

Full raw logs, TRXs, canonical reports/progress, all 58 scans per shell, source/tree proof, producer binary audit, consumer provenance and file hashes are preserved under `C:\Users\cory\AppData\Local\Temp\slate-windows-implementation-2026-09-30`, in `ci-app-4958e62e`, `ci-model-4958e62e`, `ci-shell-4958e62e`, `ci-pilot-4958e62e`, `ci-pilot-shell-4958e62e`, `ci-swift-4958e62e` and `ci-rust-4958e62e`. The PR remains draft for fresh native Mac, the pinned analyzer and human AT acceptance.
