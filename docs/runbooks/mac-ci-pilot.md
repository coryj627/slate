# Matched Mac CI pilot

This branch-only experiment compares complete native Mac coverage on the
standard GitHub ARM runner and a direct Namespace ARM runner. It does not change
production routing, subscription terms, cache tags, cache sizes or branch rules.
Keep the PR draft until fresh human accessibility acceptance is complete.

> **Reference moved (2026-10-10).** `REFERENCE` and the workflow default `source_sha` now name `c247b865d5146f006b914bf9ba68fe3ecb648632`, the squash-merge of PR 1328 on `main`. The earlier reference `a64eb393f5470b9cdb94f3f537a85ceeeda3f7c4` lived on the Codex branch, which was deleted after the merge, so a checkout of branch refs no longer has it (the Studio pilot attempt 3 failed on exactly that). Both commits have the same `apps/slate-mac/Tests` tree (5a12505b) and inventory digest a145b175.

## Source and environment qualification

Use the literal application source
`a64eb393f5470b9cdb94f3f537a85ceeeda3f7c4`. It adds the bounded settle handoff
witness and the citationberg pin that resolves quick-xml 0.41.0 to the Mac
Release stripping repair and the deterministic Debug cancellation witness.
The October 3 pair used `704ab907e0df753dd24c0c6af688dc3a8975e4e7` and earlier
pairs `e6a8337be2905b3f43841e6ab456b0f57a7d91e4`; their results remain
qualified to those older sources. All pairs require the unchanged 3,032-case
name inventory digest, and preflight pins the exact test source tree of this repaired reference. Do not
compare a before/after witness repair as if it were one source. The pilot
workflow and harness belong to a later commit; record both commits. Separate checkouts keep the tested application source unchanged, and
preflight requires it to be an ancestor of the harness.

Both candidates must provide ARM macOS and **Xcode 27.0 build 27A266a**. The
harness selects that exact installed Xcode, verifies the effective Swift path,
sets `SDKROOT` and absolute `CC`/`CXX` from its qualified SDK and Clang tools,
and installs/selects repository-pinned Rust/Cargo **1.97.1**. An unavailable
toolchain or unexpected source/test-tree change fails qualification. Do not
substitute a compiler or change a shared profile to obtain a pass.

| Candidate input | Runner label | Comparison boundary |
| --- | --- | --- |
| `hosted-xcode27` | `xcode-27` | Standard public GitHub ARM preview image; inspect actual image, hardware and OS. |
| `namespace-tahoeslim6x14` | `nscloud-macos-tahoe-slim-arm64-6x14` | Direct 6 CPU/14 GiB Tahoe image offering only current Xcodes (26.6.2 with Xcode 27 in the dashboard), without a cache suffix or custom tag; reject an existing Namespace cache mount. |
| `self-hosted-tart` | `slate-mac-tart` | The owner's Mac Studio: a throwaway Tart VM per job (12 vCPU, 16 GB, macOS 27.0.1 with Xcode 27.0 27A266a), host and LAN blocked; see `docs/plans/42_self_hosted_mac_runner_plan.md` and `docs/runbooks/mac-self-hosted-runner.md`. The pilot checks out `source` fresh, so it measures a cold build on the Studio, not the warm tree PR jobs get. |

Finder answered no Apple Events on the Golden Gate image
(`nscloud-macos-goldengate-arm64-6x14`) on October 3 and 7, so the Tahoe
slim label replaced it on October 7. The dashboard does not name its labels;
preflight's recorded OS and Xcode builds confirm the image. Restore Golden
Gate only after its Finder diagnostic passes.

CPU generation, allocated memory and OS builds may differ. This compares the
available configurations; it does not isolate the provider name as a cause.
Actual preflight observations take precedence over image documentation. See the
[GitHub ARM image](https://github.com/actions/runner-images/blob/main/images/macos/xcode-27-arm64-Readme.md)
and [Namespace runner labels](https://namespace.so/docs/reference/github-actions/runner-configuration).

## Dispatch a bounded pair

Publish the harness to a recorded experiment branch or the existing draft PR
branch. Run its parser tests before dispatch; PR pushes also run the cheap
Linux registration check. Qualify repairs on an experiment branch before a PR
push that would repeat unrelated native checks. Dispatch one candidate at a time with
the same frozen source, harness revision and `pair_id`; a pair may execute on
the providers concurrently. Verify each captured run head and its input values.

```bash
gh workflow run mac-ci-pilot.yml --repo coryj627/slate \
  --ref codex/windows-ci-cost-and-test-repairs \
  -f source_sha=a64eb393f5470b9cdb94f3f537a85ceeeda3f7c4 \
  -f runner=hosted-xcode27 -f pair_id=mac-pair-current-1

gh workflow run mac-ci-pilot.yml --repo coryj627/slate \
  --ref codex/windows-ci-cost-and-test-repairs \
  -f source_sha=a64eb393f5470b9cdb94f3f537a85ceeeda3f7c4 \
  -f runner=namespace-tahoeslim6x14 -f pair_id=mac-pair-current-1
```

Workflow serialization uses branch, candidate and pair identity with
`cancel-in-progress: false`. Separate provider pickup queue from deliberate
workflow serialization. A new `pair_id` starts a new repetition; begin with one
pair, review qualification, failures and actual usage, then decide whether more
repetitions will resolve a decision. A few runs cannot establish p95 latency or
flake rate.

## Complete independent gates

The native job runs these phases cold, then warm on the same VM and paths:

1. Native Debug Rust/FFI/Swift build using the existing script and its
   `--skip-a11y-check` flag.
2. Complete XCTest with three workers, XML and the exact **3,032-case** reference
   multiset. Require zero errors, failures or skips, including declared XML
   root/suite counters. Canonical inventory hashing sorts
   `classname + '.' + name`, preserves duplicate entries, joins with newline
   without a final newline, then hashes UTF-8. Expected SHA256 is
   `a145b175c021c7268815d3615659f65bf8889a13b09bbcd592c86b5d8b6ec316`.
3. Swift CLI build/run with all three expected sample headings.
4. Actual Release `.app` build through `build-and-launch.sh --no-open`.
5. Independent fresh-process bundled dylib load/FFI contract 30, relative native
   link, ARM architecture, strict signature/plist checks and every bundle file's
   size/SHA256. Debug or static-analysis success cannot replace this witness.

The analyzer runs on its own VM, with exact source pin
`bcaddd56931ce14d32cebcf42ea9f5b08ed5f7d8` and three build workers. Its cold pass
builds the tool and runs version, human, JSON and SARIF modes over
`apps/slate-mac/Sources/SlateMac`; warm repeats all scans using the same verified
binary. Preserve the existing numeric score floor **100** and zero-error gate.
`--skip-a11y-check` in the native job does not replace this separate gate.

The aggregate requires both independent jobs to succeed, every required phase,
both complete passes, exact source/harness/run/attempt identity, clean tracked
source and lock evidence, native artifact manifest and analyzer provenance.
Missing, skipped, failed, cancelled or stale evidence cannot produce success.
The Linux registration check exercises negative parser/aggregate cases and a
TERM-ignoring child whose wrapper exits before it.

## Measurement and cache meaning

Cold is **cold products with no explicit cache restoration**. Fresh source
`target/` and Swift `.build/` must be absent. Image/shared dependency caches may
already contain data: record their paths, symlinks and measured contents before
and after each pass. Nothing runs between the cold pass's after-observation and
the warm pass's before-observation, so one measurement serves both. Do not call
the first pass a fully cold dependency or OS cache experiment. No account cache
reset is needed or authorized.

Warm is **same-VM incremental reuse**, following a successful complete cold pass.
It is separate from a fresh runner restoring GitHub or Namespace caches. Native
builds use each provider's toolchain-default build parallelism; XCTest and the
analyzer build use three workers on both providers. Record this policy and do
not pool it with production's automatic XCTest worker count.

Every command records UTC start/end, monotonic wall time, `/usr/bin/time -l`
user/system time and child maximum RSS. Process-tree RSS is sampled every
0.5 seconds; machine VM/swap observations occur every five seconds. Sampling
wakes when the command exits, so wall time does not include an unfinished
sampling interval. These are command and sampled process-tree values, not proof
of a whole-machine maximum.

`/usr/bin/time` is SIP-protected, so dyld removes `DYLD_*` variables from its
environment before it launches the command. The harness re-exports them through
`/usr/bin/env`, and each phase record names that wrapper beside the environment
its command actually receives. As in `swift-tests.yml`, only XCTest receives
`DYLD_LIBRARY_PATH`: the build script and `make` enter through protected
binaries that would drop it anyway. Pilots recorded before this change list
`DYLD_LIBRARY_PATH` for phases whose commands never received it.
Retain raw observations and failed XML as well as summaries. If Release fails,
retain the actual generated native dylib when available.

On a signal, the harness terminates its launched process group, waits a bounded
grace, then kills remaining live members and records observed survivors or
measurement failure. This covers that group only: escaped sessions and shared
Finder are outside it. Unit verification does not establish actual hosted
runner teardown. Preserve cancelled-run partial artifacts and inspect provider
lifetime/destruction before claiming cancellation cleanup.

The two long-running workflow steps use `exec python` so Python replaces the
entry shell and receives the runner's cancellation signal directly. GitHub
documents SIGINT to the step entry process, a 7.5-second grace, then SIGTERM
and a further 2.5 seconds before killing its process tree; see
[workflow cancellation](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-cancellation).
The signal regression exercises the workflow entrypoint, cancelled summary and
owned group cleanup. A destroyed provider VM is separate evidence; it does not
prove that Python received a signal or ran its cleanup handler.

Download private evidence promptly: preflight and final/partial native/analyzer
artifacts retain for 14 days. Combine run/job pickup and step timing with
authenticated Namespace instance/usage reports. GitHub job time alone is not
billed lifetime, and a pricing formula is not a measured billed-unit result.
Public hosted compute and artifact storage have separate terms. Never compare
an incomplete failed attempt's charge with a complete reference run's coverage.

## Human acceptance and adoption

The repaired hosted qualification [37141130833](https://github.com/coryj627/slate/actions/runs/37141130833)
passed every aggregate gate on October 3, using source `704ab907` and harness
`8b538b89`. Independent raw XML verification confirms 3,032 cases with the
reference digest and no failures/errors/skips in both passes. Native phase
totals were 1,160.664 seconds cold and 460.398 seconds warm; the independent
analyzer scored 100 with zero errors or warnings in both passes. Release
verification includes actual native loading and FFI contract 30. These results
qualify one hosted cold/same-VM-warm execution. The earlier Namespace Golden
Gate Finder stall and provider cancellation cleanup remain unresolved, and
fresh human VoiceOver acceptance is still required.

Use the actual verified Release bundle and the existing feature VoiceOver
runbook with a disposable copied vault. Record actual keyboard landing, words
heard, task completion and disk effects; preserve the canonical user vault.
Automated analyzer/XCTest results remain separate from human acceptance.

Retain the full reference inventory while testing smaller packaging options
against reviewed fault-detection evidence. A passing pair demonstrates that
attempt's coverage and measurements, not provider adoption or stable latency.
Subscription changes, cache resets/deletions, provider cutover, branch-rule
changes and merging still require explicit user approval.
