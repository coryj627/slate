# Windows test obligations and fault register

Use this register when changing CI placement, splitting test projects, selecting
runner shapes, or proposing a smaller fast-feedback portfolio. A passing test
count is execution evidence. The obligation is the behavior that must remain
protected, at the seam where a user or another component observes it.

The current PR checks retain their full applicable coverage. The new expanded
Windows stress lane adds a scheduled home for existing full-tier obligations.
The comparative pilot supplies infrastructure evidence; it does not replace a
required production check.

## Obligations and existing witnesses

| Obligation | Existing behavioral witness | Observation and boundary | Required home |
| --- | --- | --- | --- |
| A structural move either finalizes one undo operation or truthfully recovers/contains partial work. | [`structural_batch.rs`](../../crates/slate-core/src/session/tests/structural_batch.rs): `successful_batch_finalizes_one_undo_row_and_no_inflight_residue`, `crash_after_index_and_link_commit_restores_paths_index_links_and_history`, and journal/rollback fault cases. | Real filesystem bytes, index links, undo rows, inflight journal, recovery markers, and reopened-session truth. Injected crashes are controlled seams; they do not simulate every power-loss or storage-device failure. | Windows Rust PR/main lane; real filesystem retained. |
| Windows filesystem identity and containment cannot clobber another entry or escape the trusted vault. | [`vault/fs.rs`](../../crates/slate-core/src/vault/fs.rs) no-replace race, case-preserving identity, reserved-name and long-path tests; [`sync_detect.rs`](../../crates/slate-core/src/sync_detect.rs) final/parent/trusted-root symlink cases; anchored-entry tests. | Native Windows APIs and real filesystem topology. A machine unable to create a symlink has not executed the symlink obligation. Record privilege failures separately and retain the CI witness. | Windows Rust PR/main lane. |
| Managed disposal/finalization releases native handles; concurrent foreign callbacks make progress and deliver their required events. | [`HandleLifetimeCensus`](../../apps/slate-windows/tests/SlateWindows.Tests/Censuses/HandleLifetimeCensus.cs), its [graph partial](../../apps/slate-windows/tests/SlateWindows.Tests/Censuses/HandleLifetimeCensus.Graph.cs), and [`CallbackConcurrencyCensus`](../../apps/slate-windows/tests/SlateWindows.Tests/Censuses/CallbackConcurrencyCensus.cs). | Rust live-object counters, dispose/reopen/use-after-dispose behavior, callback thread identities, progress/event choreography, and listener release. Weak-reference collection alone is insufficient. These counters require serial execution within a process. | Moderate tier in PR/main; `SLATE_CENSUS_FULL=1` nightly on Windows. |
| Cancellation and retirement drain admitted work and prevent stale publication before native session release. | [`CancellationCensus`](../../apps/slate-windows/tests/SlateWindows.Tests/Censuses/CancellationCensus.cs), [`W1VaultCloseBarrierTests`](../../apps/slate-windows/tests/SlateWindows.Tests/W1HardeningTests.cs), and [`ConnectionsLeafTests.Rescan`](../../apps/slate-windows/tests/SlateWindows.Tests/ConnectionsLeafTests.Rescan.cs). | Terminal typed cancellation, token propagation into real tree/graph query seams, joined work, and no late publication. Successful-path liveness waits are distinct from a deliberately exercised timeout policy. | Moderate PR/main and expanded nightly stress; focused close/timeout witnesses retained. |
| Editor deltas preserve authored bytes, dirty state, save safety, and cross-pane undo. | [`AvalonDocumentBufferCensus`](../../apps/slate-windows/tests/SlateWindows.Tests/Censuses/AvalonDocumentBufferCensus.cs): same-path peer deltas, grouped moves, same-length drift, revision-gated saving, and randomized edit storm. | Actual Avalon document, native buffer, peer histories, and saved file bytes. Exact state assertions protect integrity; timing tripwires are separate obligations. | Moderate PR/main; larger edit storm nightly on Windows. |
| Connections navigation/load/speech policy follows the contract through competing publications and lifecycle transitions. | The independent route, reroot and composed [Connections model](windows-model-ci.md), plus [`ConnectionsLeafTests.Receiver`](../../apps/slate-windows/tests/SlateWindows.Tests/ConnectionsLeafTests.Receiver.cs). | Real workspace/filesystem session compared with independently derived timelines and states; rejected/reversed completions and lifecycle replacements. The model exhausts its declared bounded inventory, not every possible topology, history, or interleaving. | Complete PR/main model inventory and verified partitions. |
| A navigation request lands keyboard focus in the intended live surface and remains there through asynchronous load. | [`ConnectionsReRootLandingTests.TheTablesShowConnectionsLandsTheKeysInTheReRootedLeaf`](../../apps/slate-windows/tests/SlateWindows.Tests/ConnectionsReRootLandingTests.cs), and the executable shell's `GraphConnections_LeafWalkDepthAndReRoot_AreClean` journey. | Shown WPF shell records actual `Keyboard.FocusedElement` transitions; FlaUI exercises the real WinExe through UIA. A model's focus-request counter alone cannot establish landing. | App PR/main and hosted interactive shell gate. |
| Reading text exposes the correct semantic ranges without work that grows disproportionately with document size. | [`ReadingViewTests`](../../apps/slate-windows/tests/SlateWindows.Tests/ReadingViewTests.cs): heading/quote mixed-range attributes, `SyntheticAttributeWalkSurvivesHugeDocuments`, `HugeDocumentSyntheticQueriesHaveBoundedCost`, range clamping, nested ordering, and fallback behavior. | Real WPF text providers, ranges, semantic attributes, and traversal/allocation budgets. Record build configuration, document shape, and runner context when interpreting timing evidence. | App PR/main; retain semantic parity during performance repairs. |
| A user can complete the assembled keyboard/UIA task, and assistive technology delivers usable feedback. | [`ShellAccessibilityTests`](../../apps/slate-windows/tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.cs) task journeys and the [recorded NVDA matrix](../plans/18_windows_port/reports/nvda_agent_matrix_pass_2026-09-22.md). | Real process, focus, names, patterns, activation, and axe checks. NVDA logs establish observed speech events; human confirmation is still needed for audibility, timing, comprehension, and task acceptance. Historical findings are not automatically current defects. | Hosted interactive PR/main shell gate; reviewed NVDA/JAWS task acceptance before release. |
| Declarative contracts stay attached to active production symbols and current documented behavior. | Existing Roslyn/source/doc censuses, shared fixture parity, and historical evidence validation. | Symbol/construction/consumer evidence and documented obligations. Source conformity does not prove assembled runtime behavior. Keep semantic checks and dependent-input triggers if extracting this layer. | Applicable PR/main checks until a validated split preserves the obligation. |

## Initial fault seeds

These are candidates for a reviewed, executable fault corpus. Historical ledger
claims are evidence for selecting faults, not a freshly measured mutation score.
Restore every fault before continuing, and never run a faulted build against a
real user vault.

| Fault seed | Expected detecting seam | Evidence status |
| --- | --- | --- |
| Gate a Connections summary on pane visibility; omit the panel line on reveal; speak a silent load; leave the inflight flag set after a rejected echo; issue another load at the depth bound. | Independent model expectations; retain direct receiver/surface witnesses where applicable. | Model-only detections recorded in [contract 35](../plans/35_graph_contracts.md), TGB-7 and TGB-11. Recreate against the current tree and measure before using these to justify portfolio selection. |
| Misclassify the re-rooted leaf's row focus request as a removed graph row handing focus to its former list. | Shown-shell focus transition witness and executable Graph Connections journey. | The existing #1318 regression explains the real focus-to-rail defect and asserts the complete landing path. |
| Apply a different synthetic style answer, truncate a range, reverse nested range order, or restore the repeated large-document attribute traversal. | Reading range parity witnesses and bounded-cost test; real shell text-pattern journey. | Existing executable obligations. Measure deliberate fault detection on the current tree; do not infer equivalent semantics from a speedup alone. |
| Break rollback/reopen ordering, consume an unresolved inflight journal, or finalize the wrong number of undo rows. | Structural batch fault/recovery witnesses with physical-byte and database assertions. | Existing executable injected-fault seams. The September main finalization failure remains unexplained; local passes do not establish a fix or a flake classification. |

For each trial record the revision, exact restored fault, triggering input,
detecting test/layer, assertion, time to actionable failure, diagnostic artifact,
and observations on other runners. A fault that survives is a finding requiring
investigation; do not replace it silently with an easier fault to improve a score.

## Rules for using this register

1. Link every proposed move or omission to its obligation and replacement
   witness. Keep the current witness until the replacement has been validated.
2. Separate logical time policy, real dispatcher/timer integration, hardware
   performance, and deadlock protection. Each needs its own pass/fail evidence.
3. Record native build configuration, runner/image/toolchain, cache restoration,
   selected tier, applicable scope, and all exclusions with every comparison.
   An unsupported desktop or missing filesystem privilege is a coverage boundary.
4. Preserve failures through aggregate checks. Upload partial results and progress
   on failure when the runner is still available; hard runner termination can
   prevent artifact upload and must not be described as complete diagnostics.
5. Evaluate useful overlap by demonstrated fault detection and diagnosis time.
   No test-count reduction follows from this register.
