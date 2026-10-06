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
- Correct Canvas media and anchored metadata native long-path ingress on machines
  whose long-path registry policy is disabled. Preserve physical identity,
  containment and reparse protection. Deep media and 240/360-character metadata
  roots protect these boundaries; this does not certify every external launcher.
- Post completed palette worker results below queued WPF input, retaining the
  generation guard, legitimate selection speech, opening silence and the count
  window. Real dispatcher witnesses cover a queued worker callback and a worker
  task that completes before the owner's await. Both retain pending input ahead
  of publication, distinguishing these schedules from ordinary supersession.
- Give Windows Canvas New Card a canonical Ctrl+Alt+T binding, retaining
  Ctrl+Alt+N as a Canvas-only legacy alias. The old chord conflicts with NVDA's
  installed desktop restart shortcut. Keep the Mac chord, Reading table
  navigation and Create Connected Card bindings; reserve the old chord in the
  physical authoring witness rather than disabling the screen reader.
- Refresh the Canvas board's existing WPF child-peer cache after a winning
  installed state and its own visibility changes. Connected-provider and
  hidden-ancestor regressions protect these boundaries without manual resets;
  the original physical arrow/reveal journey passes. Preserve peer identity
  and virtualization. The former hidden-board unit helper reset its cache
  manually, masking the missing production seam.
- Check citation-sheet return focus through the live, exact logical row after
  an unchanged-data republish. A real shell witness proves that the old provider
  correctly retires while production Escape focuses the replacement selected
  row. Retain the ten-second deadline and actual focus requirement; unrelated
  errors and duplicate matches still fail. This lifecycle witness does not
  reconstruct the exact publication schedule of the earlier hosted failure.
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
   PROFILE=debug ./scripts/build-mac-app.sh --skip-a11y-check
   (
     cd apps/slate-mac
     SLATE_LINK_PROFILE=debug DYLD_LIBRARY_PATH="$repo_root/target/debug" swift test --parallel
   )
   PROFILE=debug make swift-cli
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

Keep the composed Windows findings in the handoff too. On the Windows VM, the
old Ctrl+Alt+N authoring journey restarted installed NVDA 2026.2; the global
registration probe confirmed the chord was already owned. NV Access documents
[that restart shortcut](https://download.nvaccess.org/documentation/en/keyCommands.html).
Test the canonical replacement with NVDA running, preserving actual keyboard
landing, editor scope and undo. A clean hosted desktop alone does not exercise
this composition.

The local desktop handler-group listener also captured no launch lines in one
isolated run, while NVDA's existing log identified four fixture notifications
and recorded all four complete matching speech payloads in order. The bounded
slice retains the first notification's fixture prefix and activity ID rather
than its complete text. Treat client capture,
app-to-reader delivery and human audibility as distinct observations. Retain
the failing capture evidence and investigate registration/client coexistence;
do not weaken its assertion or infer missing speech from an empty capture.
Hosted Windows build 26100 captured its complete journey after listener advice;
the local build 26300 took the no-advice fallback. Those differences do not by
themselves establish an OS cause.

For the Windows client follow-up, retain the prelaunch desktop-root/subtree
subscription and exact sequence before any post-window subscription. Record
the actual COM apartment, selected automation class, registration/removal
times, callback entries before cache reads, and cache HRESULTs. Compare with a
single explicitly owned MTA client; the separate STA FluentShell fixture's
event subscriptions also warrant that ownership review. Microsoft recommends
[MTA event clients and removal on the registering thread](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-threading).
The existing [launch contract](../plans/40_nvda_matrix_remediation_contracts.md)
already records the pre-window capture seam. Cache-read failures still enqueue
an unknown sender, so zero callbacks and zero other-process records are a
different boundary from failed sender classification.

## Measure infrastructure after the fixes

Use the same source revision, toolchains, test inventory and native obligations.
Start with one cold comparison and several warm comparisons, then accumulate
normal-operation evidence. Record checkout/setup/build/test time, queue time,
push-to-actionable-failure time, memory pressure, working-set peaks, cache restore
and persistence, cleanup after cancellation, and billed units. A few runs cannot
establish stable p95 latency or flake rates.

The Windows pilot automatically performs a hosted run with cache restore disabled
when its workflow/action changes in a PR. After a workflow has run once, the
GitHub API/CLI can dispatch its existing branch version even when the web page
has no **Run workflow** button. This was verified on October 3 before merging
the pilot. Manual inputs select `hosted`, `namespace-8x16`, or `namespace-4x8`
and cache restore. Both Namespace candidates deliberately omit an attached
Namespace volume. Their non-interactive session cannot replace the separately
hosted FlaUI desktop check. GitHub job/step timing plus uploaded family reports
and TRX provide the comparison evidence.

### Mac continuation: Release artifacts and fresh acceptance

The separate [matched Mac pilot](mac-ci-pilot.md) freezes the repaired application
source, keeps native and analyzer gates independent, and records cold products
and same-VM incremental reuse without changing production routing or cache tags.

The October 3 Mac continuation found a Release-only native artifact failure at
`4958e62e61d012d0a6924ec38433a283999f00e1`: Xcode 27's linker and the native
loader both rejected the Rust 1.97.1 dylib's misaligned Mach-O string table.
Debug builds and static accessibility scans did not expose this shipping-artifact
failure. The workspace's `.cargo/config.toml` now disables rustc's Mach-O
stripping for every macOS build. An October 4 review found that the original
build-script override left other Release paths broken and made alternating
builds recompile. The Mac build script uses Apple's `strip -x` on the generated
dylib before Swift linking and bundling. The pinned Rust version and Release
optimization settings remain unchanged. See the upstream
[Rust issue](https://github.com/rust-lang/rust/issues/157750).

At repair revision `e6a8337be2905b3f43841e6ab456b0f57a7d91e4`, the full Release
bundle built successfully. Its signature, plist, relative dylib link, native
load and FFI contract were checked independently. The complete native debug
suite passed all 3,032 XCTest cases with zero failures, errors or skips, and
the Swift CLI smoke passed. The separate pinned analyzer scored 100 with zero
errors or warnings; its SwiftUI input tree is identical at baseline and repair.
These automated results do not constitute fresh human VoiceOver acceptance.

Earlier complete XCTest attempts encountered Finder Trash timeout/busy errors.
Unchanged complete reruns passed, including runs at 18 and 6 workers, but no
causal repair was established. Preserve those failures rather than classifying
them as explained by a later green run. Finder Put Back remains part of the
native Trash contract.

The paired Xcode 27 pilot exposed an ordering assumption in
`testAwaitingACancelledPassAppliesNothingButSettleFollowsIt`: awaiting a
cancelled seed also permits its valid replacement to paint before the test
resumes. The repaired fixture holds both real passes at explicit barriers,
installs the successor while settle awaits the seed, verifies that the
cancelled seed paints nothing, and then requires settle to follow the successor
to its repaint. Its name and the 3,032-case inventory are unchanged. Debug-only
nil-default hooks provide the barriers; Release excludes them. A one-pass
settle mutant and a missing final cancellation-guard mutant both fail the
repaired assertions. Full provider qualification remains separate.

The repaired application source `704ab907e0df753dd24c0c6af688dc3a8975e4e7`
passed the complete [hosted Mac pilot](https://github.com/coryj627/slate/actions/runs/37141130833)
with harness `8b538b890bd387b84bcc69ed6e76b83fec923b00` on October 3.
Independent downloaded XML verification found all 3,032 cases, the unchanged
inventory digest and zero failures/errors/skips in each cold and same-VM warm
pass. Both passes completed Debug, CLI and actual Release load/link gates;
the separate pinned analyzer scored 100 with zero errors or warnings in both.
Native phase totals were 19m21s cold and 7m40s warm on a 3 CPU/7 GiB hosted M1.
This is one successful qualification, not a latency distribution or a fresh-VM
cache-restoration measurement. Fresh human VoiceOver acceptance remains open.

The cold Namespace Golden Gate 27.0 attempt progressed slowly through repeated
approximately two-minute Finder deletion requests and was cancelled with
partial evidence preserved. The normal Namespace Mac workflow passed on Tahoe
26.6.2. Preserve this image distinction and the unresolved Finder condition;
do not infer a provider-wide limitation or replace Finder Put Back semantics.
The pilot's long steps now replace their entry shell with Python so cancellation
reaches its handler. Keep actual process cleanup and provider VM destruction as
separate observations.

The complete [hosted Windows pilot](https://github.com/coryj627/slate/actions/runs/37136112114)
and [Namespace 8x16 pilot](https://github.com/coryj627/slate/actions/runs/37138568771)
also passed on October 3. Hosted tested merge `85469b47f7ea54fb2d5abc27fa09520fe68096b4`;
Namespace tested head `98c73f45ac2b81518ce652a0f6612bd3ef83c5e1`.
Their complete source trees are identical, but their producers built separate
binary payloads. Both restored no explicit build cache and retained the full
app/model/hosted-shell obligations. Keep this source-tree qualification separate
from literal same-commit or common-binary experiments and from the older
unclassified failure attempts. The shell uses standard hosted Windows for both
candidates; a candidate name on its job does not identify a Namespace VM.

Use the verified `.app` bundle for human acceptance. Hold Option while opening
it until Welcome appears to use the existing restore-vault escape hatch, then
open a disposable copied fixture. Record actual keyboard landing, words heard,
task completion and disk effects. The canonical user vault must remain intact.
Retain a real Release artifact load/link witness when evaluating build-toolchain
or packaging changes; debug and analyzer results alone do not protect that seam.

The first cold hosted pilot, [run 36797768639](https://github.com/coryj627/slate/actions/runs/36797768639),
executed 4,793 app tests: 4,791 passed and two failed. A graph warm tick took
132.8 ms against its unchanged 100 ms budget; graph paging exposed 89 live
containers against its 85 bound. Later model/format/shell stages did not run,
so this is incomplete parity evidence. The app step took 29m05s and native build
16m26s. Preserve both bounds and profile the measurement/cleanup boundaries
before adopting hosted Windows; a timeout increase would not repair these
assertions. The tests and relevant graph production sources were unchanged by
that first revision.

The regular Namespace app TRX from the first repaired revision passed all 4,793
tests in 17m22s. Its largest class totals were GraphDiagramTests 172.7 seconds,
CommandPaletteTests 159.5 seconds, GraphNavigatorCensus 82.3 seconds and
ReadingFocusTests 64.3 seconds. These are profiling leads, not redundancy
claims. In particular, the palette scaling fact performs 52 GC/finalizer
barriers outside its measured query clocks; its complete test cost and query
latency measure different work. Source/document checks also merit immutable
inventory reuse only where equivalent symbol and input enforcement is retained.

Record the Windows long-path registry policy as well. The existing deep-media
fact passed the original runner image yet exposed a production portability bug
on a policy-off Windows machine. A policy-off witness guards this environment
boundary; changing the VM setting would conceal that defect.
See Microsoft's [native path-limit guidance](https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation)
for the registry/manifest requirements and explicit extended-path form.

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

Before introducing deferred checks or a provider cutover, inspect current main
rules and require successful complete evidence for the final revision. The
September audit found main unprotected. Stable aggregate names and fail-closed
dependencies are prepared here; they do not create branch-rule enforcement.

Choose the subscription after these observations. Business is justified if its
effective full-cycle price and added concurrency buy useful feedback or absorb
the retained native workload. Hosted or hybrid is justified only after the same
native obligations and acceptable feedback latency pass on the candidate.
Record compute, retained storage and active snapshots separately, and include
other repositories in the workspace. The model phase reduction alone is not a
monthly savings estimate or evidence that a cheaper plan will fit.

## Further testing-strategy experiments

Keep the current full inventory as the reference. Recreate a small reviewed fault
corpus from the obligation register in disposable builds and record which layer
detects each fault, how quickly, and with what diagnostic. Extend selected
multi-step histories and reordered completions where the bounded model lacks
them. Extract source/document/ancestry checks only with equivalent semantic
enforcement and complete input triggers. Separate controllable time policy from
real dispatcher/timer delivery and hardware latency witnesses.

The independent shard verifier pins the original inventory hashes as well as
the census counts; agreement between shards alone cannot accept a changed
inventory. The original SHA-256 values are:

- routes: `e847440e2e4a1c18142b44faa91f8891209b2866296ea15fd31a5385657737fd`
- reroot: `48c2daa03565b2e82a202e211d44b32555ef92d1b28a793fc4561a8b5912312e`
- composed: `81ba0fdae0e51f8c33e110ea0d3ccd8278157f2f390560fdba268712472a629a`

A deliberate model expansion or inventory change must update the model's
counts and named exclusions, the verifier's `CENSUSES`/`INVENTORY_SHA256`, and
the independently captured test fixtures together under model-change review.

These experiments can justify a later portfolio change. Neither test counts nor
passing shards establish equivalent fault detection for an untested subset.

### Further continuation: native fixtures and model-consumer sizing

The two deferred-summary citation facts now use an explicitly owned, pumped STA
and `DispatcherSynchronizationContext`. They still require the initial null,
the final wrong-note null and legitimate deferred delivery through the original
5,000-entry native fixture. The [focused replay](https://github.com/coryj627/slate/actions/runs/37155288183)
reproduced the original final-null signature; five candidate pairs passed. A
production guard-removal mutant failed the final wrong-note assertion while
legitimate delivery passed. This is specific fault-detection evidence, not a
sole-cause explanation of the earlier CI event or permission to omit other tests.

The [fresh native qualification](https://github.com/coryj627/slate/actions/runs/37161038529)
at `0646a6ce2f935c36eb9ad467e85b4e49f534f6f0` completed all six workflow jobs.
Its independent producer audit verifies the additive 693-row probe and original
4,806-execution name multiset, including repeated names and distinct execution
IDs. Both citation facts and the unchanged sidebar boundary pass in each. The
independent downstream audit verifies all 13,069 original model scenarios exactly
once, the 135-test shell reference, 58 axe scans with zero errors or waivers,
and all 449 transferred files. The original native/platform/CLI producer,
strict union and aggregate passed. Cache restoration was enabled, but the native
graph missed and was seeded; only the binding-generator cache hit. The new producer payload is distinct
from source 98's binaries. An [earlier reconstructed-package replay](https://github.com/coryj627/slate/actions/runs/37157223104)
failed full acceptance: transferred runtime binaries did not supply complete
generated/source/ancestry inputs, and a separate sidebar timing failure remains
unclassified. Fresh faithful builds and revision-bound downstream artifact
verification are necessary; neither focused passes nor rewritten compilation
sidecars substitute for that closure.

The [three-candidate model/resource matrix](https://github.com/coryj627/slate/actions/runs/37159865788),
with harness `c942a1089c09de82177fefb729dc8ade56a78799`, reused the immutable
producer payload from `98c73f45ac2b81518ce652a0f6612bd3ef83c5e1`. All six
consumers independently verified the same archive and all 449 files. Namespace
4×8, Namespace 4×16 and hosted Server 2022 each completed all 13,069 scenarios
exactly once with the original three inventory digests; all six resource gates
and eleven jobs passed. This supports
evaluating smaller model consumers while preserving the complete reference.
Authenticated billing attribution remains in the private investigation ledger.
Producer, storage, subscription and other attempts remain separate; no integrated
smaller-runner pipeline or monthly saving is inferred.

The 4×8 route facts took 254.612/254.486 seconds versus 247.833/234.347 on 4×16.
Observed resident/private-commit values support evaluating model consumers,
not downsizing the producer. Samples can miss exit tails and sampled available
RAM minima are upper bounds on actual minima. Hosted routes remained
7.823×/9.821× slower despite identical product bytes; measured CPU grew only
1.636×/1.754× while sampling intervals grew 8.039×/10.062×. Arrangement and
cleanup contain 72.07% of recorded phase excess; explicit settle/verification
contains 0.0272%. These mixed buckets do not identify timer, dispatcher or I/O
causes. Hardware, patch levels and observer Python differ; preserve repeats,
cancellation evidence and the unchanged 45-minute watchdog.

Main/PR profile names do not isolate workspace-shared custom tags. Observed
profile settings do not prove effective persistence protection across alternate
profiles, repositories or direct labels. Verify that enforcement before any
approved drained transition. Concurrency controls are not spend caps; alerts
and quota behavior need independent verification. Right-size from generation
maxima and headroom, not latest contents; requested capacity and attached
snapshot time are separate meters. No policy change, reset or resize follows.

The isolated Golden Gate 27 Finder request encountered an unanswered Automation
consent gate and a 120-second TCC timeout. It does not establish a provider-wide
limitation. The [cancellation probe](https://github.com/coryj627/slate/actions/runs/37152941647)
removed all 38 witnessed owned identities, including the separate XCTest process
group, in 3.017 seconds. The build tracker saturated its 128-identity bound, so
full-build cleanup remains unqualified; VM destruction is separate. Fresh
human VoiceOver keyboard landing, speech, task completion and file effects on
the frozen Release app remain NOT RUN. Keep PR #1328 draft and every reference
inventory while evaluating packaging or smaller portfolios.

### October 4 security dependency qualification

The advisory exception expired at the UTC date boundary and stopped the gate
before its vulnerability scan. Re-review confirmed that shared or downloaded
CSL styles are untrusted input to the affected namespace-aware parser. The
released citationberg 0.7.0 dependency still selected quick-xml 0.38.4. Rather
than renew the exceptions, the workspace pins upstream citationberg commit
`06a591e2f237d25e1dfdedac3f3d1494c496c52d`, whose sole change from the published
source is the quick-xml dependency update. The committed lock resolves 0.41.0;
both advisory ignores are removed, and the existing deadline enforcement stays
unchanged for any future exception. A fresh cargo-audit 0.22.2 scan found zero
vulnerabilities, with existing unmaintained-crate warnings reported separately.

The CSL entry-point regression accepts 256 namespace declarations and requires
a typed refusal for 257, checking the underlying namespace-limit cause. The
original parser accepted the excessive declarations; the repaired parser
passes. This is fault-detection evidence for the namespace allocation advisory,
not a CPU performance benchmark or a general resource bound on CSL input.
Replace the Git patch only when a fixed registry release is admitted through
the citation dependency chain, then commit the new registry lock/source and
repeat the audit and native qualification. The dependency change requires new
Mac Debug, complete XCTest, CLI, separate analyzer and actual Release-load
evidence. Earlier Release bytes remain historical evidence; fresh human
VoiceOver acceptance must use the newly qualified app. Keep this PR draft.

### October 4 review repairs: Mac first, then Windows

A review of `12427d28404e7c7a50df27fa9d5b1127c9763ae4` ranked 15 findings and
verified several lower-priority items. The repairs below were made and checked
on macOS 27.0.1 with Xcode 27.0 and the pinned Rust 1.97.1. Windows-only code
waits for the Windows machine.

Repaired on the Mac:

- Marker-fault fixtures in `structural_batch.rs` set process-global triggers
  (`b.md`, `x.md`, `y.md`, `a.md`, `left/`) that every session matches as a
  path substring. Holding the old `b.md` trigger across the module failed 17
  tests, including the two instrumented for the payload-less main failure. That
  makes the shared trigger a likely cause; it is not a reproduction of the main
  event. Each faulted fixture now names unique paths. With each new trigger held
  across the module, every other test passed, and 30 module runs at four threads
  passed. The barrier fixture no longer holds `ENV_FAULT_GUARD`, so its failure
  cannot poison the guard, and one helper prints fixture panics.
- The settle handoff XCTest detaches the appearance observer and bounds its
  waits. A notification replayed after the seed capture hung the previous
  version and passes now. The Tests tree changed, so a Mac pilot of a source
  containing this repair needs a new reviewed `REFERENCE`; the frozen
  `704ab907` qualification is unaffected.
- cargo-audit skips Git sources. The gate now also scans a lock copy naming the
  admitted citationberg revision by its published identity, and a synthetic
  0.7.0 advisory fails it.
- `.cargo/config.toml` disables rustc stripping on macOS. The Release app passed
  the pilot's Release witness with an aligned dylib string table, the Release
  Swift CLI linked and ran, and a plain Release build after the scripts
  recompiled nothing.
- The Mac pilot re-exports `DYLD_*` past SIP-protected `/usr/bin/time`, gives
  `DYLD_LIBRARY_PATH` to XCTest alone as `swift-tests.yml` does, and no longer
  counts an unfinished sampling interval as phase wall time.
- The `windows-native-build` cache key no longer names the commit, and
  `apps/slate-windows/uniffi-bindgen-cs.version` holds the generator tag for
  every lane.
- The Windows pilot verifies producer binaries through one composite action,
  checks each model shard's runner class against its candidate, and keeps
  producer binaries for seven days of retries. Hosted model shards get a
  70-minute hang limit. Two of seven hosted runs (37147911816, 37167597172)
  lost a still-progressing routes fact at 45 minutes, because VSTest's blame
  timer resets only between tests. This is a hosted exception to the unchanged
  45-minute watchdog above; Namespace candidates keep 45 minutes, and earlier
  hosted runs measured under the shorter limit.

Not run on this Mac: Finder answered no Apple Events from the review session
(`AppleEvent timed out`, -1712). Of the library tests, 1,986 passed, 10 failed
on that Finder timeout and 150 Trash, delete or census tests were skipped. The
complete XCTest suite, the separate analyzer and human VoiceOver acceptance
remain open; the changed XCTest class passed.

Queued for the Windows machine from the same review, and what happened to it
on October 5. The machine: Windows 11 Pro 26H2 build 26300.9550,
`LongPathsEnabled` = 1 (left as found), only the en-US keyboard layout,
NVDA 2026.2, .NET SDK 10.0.401, Rust 1.97.1 through rustup. Baseline on
`8974fb31` before any change: 4,806 app facts passed in 17 minutes, both model
partitions passed (13,069 scenarios exactly once; routes about 1,669 s a
shard), and the slate-core suite passed with CI's skips (2,087 library tests).

- Repaired (`99841283`). AltGr reaches WPF as Ctrl+Alt, so AltGr+T in the
  canvas filter ran New Card and lost `ț`, `₺` or `þ`. The surface now uses the
  modal surfaces' AltGr rule (`TextEditingChords.IsAltGr`): with right Alt down,
  an editable field keeps every Ctrl+Alt canvas chord. A fact over every
  Canvas-scope Ctrl+Alt row failed before the gate. With NVDA on the Release
  build, left Ctrl and right Alt with T in "Filter cards" echoed only the keys
  and the board kept three cards; the same keys on the outline announced
  "Created text card "Untitled" above "Core question"", and left Ctrl and left
  Alt with T in the filter still created a card.
- Owner decision and repair (`28a46318`). A queued Enter now publishes the
  latest query's finished rank first and acts on its selection; a rank still
  running is not waited for. Contract 28's T7, its pending-state row and I3 are
  amended. A fact queues Enter at Input priority behind a finished rank: it ran
  New Note before and runs Quick Open now. `_countDispatcher` is
  `_ownerDispatcher` (`30c7582e`).
- Repaired (`8d3c2051`, `e0c999ca`). The StyleId walk starts at the query's
  first paragraph and moves forward, so line 2,000 of one list, table or
  section costs what line 2 does (73,672 bytes each in Debug; it was 2,026,648,
  4,137,832 and 1,034,648 bytes). Decisions use WPF's normalization of a private
  clone, so raw DocumentRange spans answer as base did (70001 and NotSupported
  for the review's two scenarios, Ctrl+A included) without the caller's range
  being normalized. 1,192 raw-span answers match the original adaptor walk.
  Normalizing the clone costs about 28 KB of WPF work a query. The reflection
  write to `TextRangeAdaptor._start` stays by owner decision: it touches only a
  private clone and degrades to "no match" if the field disappears, but a
  servicing change to its meaning could mis-position a found range or trip a
  WPF assert, which fails fast. In reading view NVDA said "heading level 1,
  Title", "body line one", "heading level 2, Second heading", "body line two"
  and "a quoted line", and read Ctrl+A in `trailing.md` as "body selected".
  Narrator said "heading level 2 Second heading" when arrowing onto the second
  heading and "heading level 1 Title" for Narrator+I on the first line, gave
  body lines no level, and read the empty last heading of `trailing.md` as
  "heading level 1, blank".
- Repaired (`9067e552`). Canvas card peers are held weakly, a realization
  retains only live keys, and an install drops tombstones whose peers are gone.
  The board's children are rebuilt without `ResetChildrenCache`'s per-child
  diff, which handed every windowed card's provider to UIA whenever a
  structure-changed listener existed; a changed child list raises one
  ChildrenInvalidated event instead. Panning a 48-card row then realizing one
  card retained 48 keys before and at most four beyond the materialized cards
  after. With NVDA, Down and Up on the visual board of a 12-card row read
  "Text card "Card 1", 2 of 12 in canvas" through "Card 5" and back to "Card
  4", each with its position.
- Repaired (`a3f99fe0`). `ForCreateFile` leaves every device spelling .NET
  recognizes as given, so `\??\` and forward-slash device paths are no longer
  prefixed again.
- Repaired (`7f6ff956`, `728e08ab`). Model checkpoints are written by a
  background flusher, the failed checkpoint names only an unfinished case, the
  slow-case fact checks which cases were kept, and the C# facts pin the three
  inventory digests. Summed over both shards, routes `settleAndVerify` fell from
  15.6 s to 1.8 s; a reworded exclusion reason now fails the composed fact in
  21 ms. Both partitions passed with no missed checkpoint writes.
- Repaired (`4eefc19f`), not as the review suggested. SQLite's two opens take
  the verbatim path only when the database or its `-journal` sibling would reach
  MAX_PATH. A verbatim cache directory would have put `\\?\` into the path the
  open-failure and prefs messages read out. A 240-character root failed with
  SQLITE_CANTOPEN before and now opens, saves, compacts and reopens; the test
  binary has no longPathAware manifest, so this policy-on machine reproduced it.
- Repaired (`311baf9b`, `f2252b18`, `95780e7e`). The shell gate's editor-chord
  check waits for a real row read and samples a one-second window; the graph
  bound loses its unreachable arms; one `NativeWindow.RequestUiaRoot` replaces
  three `WM_GETOBJECT` declarations.
- Repaired (`21c96321`). The pilot runs on pull requests that change its
  binaries action.

Final verification: 4,831 app facts passed in 18 minutes on the app code of
`28a46318`, and the shell accessibility gate passed 135 of 135 with
`SLATE_REQUIRE_UI_AUTOMATION=1` on `95780e7e`. Both model partitions passed on
the committed harness and pins, routes in about 1,580 s a shard; the later
commits change nothing the model facts construct. The slate-core suite passed
with CI's skips (2,090 library tests, three of them the new long-path tests).
`dotnet format --verify-no-changes`, `cargo fmt --check` and clippy with
`-D warnings` were clean.

The screen-reader runs drove the Release build with SendInput and refused to
type unless Slate held the foreground. NVDA's speech came from its log;
Narrator's came from its copy-last-phrase command (Narrator+Ctrl+X). Narrator
Home and its "Narrator updates" carousel take the foreground at every start, so
the driver closes and minimizes them through UIA. Two results match the
`8974fb31` build exactly and predate this work. NVDA took about 5.1 s to speak
each line of the 2,000-item list at either end, and 28 s to speak at all after
the switch to reading view, logging watchdog freezes on both builds; the
StyleId walk is not the cause (#1329). Narrator spoke only the key echo for
Ctrl+A in reading view on both builds (#1330).

Not done here: no AltGr layout is installed, so the AltGr check used left Ctrl
with right Alt on en-US, which reaches WPF as AltGr does but types nothing. A
Romanian or Turkish layout run is waived by owner decision (October 5). The Mac
session's two calls (the hosted 70-minute model limit and the Mac pilot's
duplicated jobs) stay as they are by owner decision.

CI on `b4a6ff3b` passed all eight runs, including the hosted pilot's cold run
(pull-request runs restore no caches), which took about two hours. The
pinned-tag step, the shared binaries action and each shard's runner check ran
and passed. An Actions incident that day left four `ubuntu-latest` jobs
unassigned ("The job was not acquired by Runner of type hosted"); after GitHub
recovered, `gh run rerun --failed` re-ran only those jobs, and they passed.
