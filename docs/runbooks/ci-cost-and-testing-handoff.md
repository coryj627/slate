# CI cost and testing handoff

September 30, 2026. The Windows implementation is on
`codex/windows-ci-cost-and-test-repairs`, based on
`39dd2457ace1c8358aa9c954604dfb0e1437d6b0`. Use the final branch revision for
comparisons; a green result from an earlier revision is not final acceptance.

## Changes prepared on Windows

- Remove the model fixture's repeated shutdown fallback with retirement-before-
  release synchronization and owned cleanup. Preserve all 13,069 scenarios,
  production timeouts, and the focused real six-second rescan witness.
- Replace repeated WPF text-adaptor paragraph traversal with a block traversal.
  Retain the original adaptor's query and endpoint semantics, checked by a
  192-span differential witness. Bound allocation for the large-document query.
- Preserve the structural-batch failure, but expose its original panic payload
  despite MathCAT's global silent panic hook. The original CI failure has not
  been reproduced or classified as a flake.
- Add app TRX/hang evidence and bounded model progress/slow-case diagnostics.
- Schedule expanded native Windows stress; retain moderate PR coverage.
- Complete Mac workflow triggers for consumed help, fixtures and build scripts.
- Move license, lockfile audit, model verification and aggregate checks to hosted
  Linux. Native production lanes and required-check identities remain intact.
- Prepare a comparative native Windows pilot on hosted and two documented
  Namespace shapes, without new Namespace cache volumes.

The [obligation register](windows-test-obligations.md) records why expensive
witnesses exist. It is not evidence that their scenarios can be removed. The
[cache runbook](ci-cache-policy.md) supersedes the old claim that profile names
isolate a shared custom tag.

## Pick up on the Mac

1. Inspect the Mac checkout's branch, worktrees and dirty files. Preserve local
   work. Fetch this branch and use a clean checkout or worktree at its final SHA;
   do not reset the existing checkout to make it match.
2. Record revision, `sw_vers`, `uname -m`, `xcodebuild -version`, `swift --version`,
   `rustc --version`, memory and cache state. Use the repository's pinned Rust
   toolchain and a Swift/Xcode combination compatible with the current app.
   The workflow records a Swift 6.2.4 expression-type-checker regression; reproduce
   any toolchain failure before attributing it to these changes.
3. Build native debug bindings and the app, then run the complete XCTest suite
   and Swift CLI smoke. Match the current workflow's environment:

   ```bash
   repo_root="$PWD"
   ./scripts/build-mac-app.sh --skip-a11y-check
   (
     cd apps/slate-mac
     DYLD_LIBRARY_PATH="$repo_root/target/debug" swift test --parallel
   )
   make swift-cli
   git status --short
   ```

   Capture build/test logs and durations. Inspect generated-file or lockfile
   differences rather than committing them automatically. `--skip-a11y-check`
   follows the XCTest lane; it does not certify the separate accessibility gate.
4. Run the pinned SwiftUI accessibility analyzer from `a11y-check.yml` with the
   same source paths and score/error enforcement. Preserve the floor of 100 and
   zero errors. Treat analyzer failures separately from native XCTest results.
5. Build the Release `.app` with `./scripts/build-and-launch.sh`. Use the existing
   [VoiceOver feature runbook](voiceover-feature-test.md) against disposable
   fixtures. Record actual keyboard landing, delivered speech, task completion
   and file effects for navigation, Connections, reading headings/quotes,
   cancellation, root changes and close/reopen. Automated AX results and human
   VoiceOver acceptance are distinct evidence.

## Measure infrastructure after the fixes

Use the same source revision, toolchains, test inventory and native obligations.
Start with one cold comparison and several warm comparisons, then accumulate
normal-operation evidence. Record checkout/setup/build/test time, queue time,
push-to-actionable-failure time, memory pressure, working-set peaks, cache restore
and persistence, cleanup after cancellation, and billed units. A few runs cannot
establish stable p95 latency or flake rates.

The Windows pilot automatically performs a hosted run with cache restore disabled
when its workflow/action changes in a PR. Once the workflow is registered on the
default branch, manual inputs select `hosted`, `namespace-8x16`, or `namespace-4x8`
and cache restore. Both Namespace candidates deliberately omit an attached
Namespace volume. Their non-interactive session cannot replace the separately
hosted FlaUI desktop check. GitHub job/step timing plus uploaded family reports
and TRX provide the comparison evidence.

Compare the current Namespace Mac shape with a hosted Apple Silicon candidate.
Validate its actual Xcode/Swift version and memory before adopting it. Preserve
native Rust/FFI, complete XCTest, CLI smoke and accessibility obligations. Keep
the Linux ARM Rust and real-filesystem durability/performance witnesses in the
decision; a Windows pass does not replace them. Set Mac hang bounds from observed
full-suite tails, preserving cold-build headroom and actionable partial logs.

Remeasure one versus two model processes after the shutdown repair. Separate
immutable build-artifact reuse from mutable native session state. Do not enable
whole-assembly xUnit parallelism: native counters and dispatcher tests retain
their process-global constraints.

## Authenticated Namespace work

On the authenticated Mac session, inventory effective cache policies, tags,
capacity, consumers and working-set peaks. Prove persistence restrictions across
alternate profiles/direct labels before resetting a generation. Schedule the
drained transition in the cache runbook; no policy change, volume reset or cache
deletion was performed by this Windows work.

Retire obsolete cache consumers only after their deployed jobs have drained.
Right-size useful graphs from measurements and the provider's current minimum,
rather than forcing an arbitrary total capacity. Keep requested storage and
active snapshot meters separate. Verify alerts/quota behavior and obtain the
effective Business transition/proration quote before selecting a subscription.
Team, Business at $250, and hybrid remain candidates; compare complete-cycle
demand and feedback quality after the execution repairs.

## Further testing-strategy experiments

Keep the current full inventory as the reference. Recreate a small reviewed fault
corpus from the obligation register in disposable builds and record which layer
detects each fault, how quickly, and with what diagnostic. Extend selected
multi-step histories and reordered completions where the bounded model lacks
them. Extract source/document/ancestry checks only with equivalent semantic
enforcement and complete input triggers. Separate controllable time policy from
real dispatcher/timer delivery and hardware latency witnesses.

These experiments can justify a later portfolio change. Neither test counts nor
passing shards establish equivalent fault detection for an untested subset.
