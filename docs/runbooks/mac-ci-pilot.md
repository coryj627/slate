# Matched Mac CI pilot

This branch-only experiment compares complete native Mac coverage on the
standard GitHub ARM runner and a direct Namespace ARM runner. It does not change
production routing, subscription terms, cache tags, cache sizes or branch rules.
Keep the PR draft until fresh human accessibility acceptance is complete.

## Source and environment qualification

Use the literal repaired application source
`e6a8337be2905b3f43841e6ab456b0f57a7d91e4`. Its only change from the Windows
reference `4958e62e61d012d0a6924ec38433a283999f00e1` is the Mac Release stripping
repair. The pilot workflow and harness belong to a later commit; record both
commits. Separate checkouts keep the tested application source unchanged, and
preflight requires it to be an ancestor of the harness.

Both candidates must provide ARM macOS and **Xcode 27.0 build 27A266a**. The
harness selects that exact installed Xcode, verifies the effective Swift path,
and installs/selects repository-pinned Rust/Cargo **1.97.1**. An unavailable
toolchain or unexpected source/test-tree change fails qualification. Do not
substitute a compiler or change a shared profile to obtain a pass.

| Candidate input | Runner label | Comparison boundary |
| --- | --- | --- |
| `hosted-xcode27` | `xcode-27` | Standard public GitHub ARM preview image; inspect actual image, hardware and OS. |
| `namespace-goldengate6x14` | `nscloud-macos-goldengate-arm64-6x14` | Direct 6 CPU/14 GiB label without a cache suffix or custom tag; reject an existing Namespace cache mount. |

CPU generation, allocated memory and OS builds may differ. This compares the
available configurations; it does not isolate the provider name as a cause.
Actual preflight observations take precedence over image documentation. See the
[GitHub ARM image](https://github.com/actions/runner-images/blob/main/images/macos/xcode-27-arm64-Readme.md)
and [Namespace runner labels](https://namespace.so/docs/reference/github-actions/runner-configuration).

## Dispatch a bounded pair

First publish the harness to the existing draft PR branch and allow its cheap
Linux registration/parser check to pass. Dispatch one candidate at a time with
the same frozen source, harness revision and `pair_id`; a pair may execute on
the providers concurrently. Verify each captured run head and its input values.

```bash
gh workflow run mac-ci-pilot.yml --repo coryj627/slate \
  --ref codex/windows-ci-cost-and-test-repairs \
  -f source_sha=e6a8337be2905b3f43841e6ab456b0f57a7d91e4 \
  -f runner=hosted-xcode27 -f pair_id=mac-pair-1

gh workflow run mac-ci-pilot.yml --repo coryj627/slate \
  --ref codex/windows-ci-cost-and-test-repairs \
  -f source_sha=e6a8337be2905b3f43841e6ab456b0f57a7d91e4 \
  -f runner=namespace-goldengate6x14 -f pair_id=mac-pair-1
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
and after each pass. Do not call the first pass a fully cold dependency or OS
cache experiment. No account cache reset is needed or authorized.

Warm is **same-VM incremental reuse**, following a successful complete cold pass.
It is separate from a fresh runner restoring GitHub or Namespace caches. Native
builds use each provider's toolchain-default build parallelism; XCTest and the
analyzer build use three workers on both providers. Record this policy and do
not pool it with production's automatic XCTest worker count.

Every command records UTC start/end, monotonic wall time, `/usr/bin/time -l`
user/system time and child maximum RSS. Process-tree RSS is sampled every
0.5 seconds; machine VM/swap observations occur every five seconds. These are
command and sampled process-tree values, not proof of a whole-machine maximum.
Retain raw observations and failed XML as well as summaries. If Release fails,
retain the actual generated native dylib when available.

On a signal, the harness terminates its launched process group, waits a bounded
grace, then kills remaining live members and records observed survivors or
measurement failure. This covers that group only: escaped sessions and shared
Finder are outside it. Unit verification does not establish actual hosted
runner teardown. Preserve cancelled-run partial artifacts and inspect provider
lifetime/destruction before claiming cancellation cleanup.

Download private evidence promptly: preflight and final/partial native/analyzer
artifacts retain for 14 days. Combine run/job pickup and step timing with
authenticated Namespace instance/usage reports. GitHub job time alone is not
billed lifetime, and a pricing formula is not a measured billed-unit result.
Public hosted compute and artifact storage have separate terms. Never compare
an incomplete failed attempt's charge with a complete reference run's coverage.

## Human acceptance and adoption

Use the actual verified Release bundle and the existing feature VoiceOver
runbook with a disposable copied vault. Record actual keyboard landing, words
heard, task completion and disk effects; preserve the canonical user vault.
Automated analyzer/XCTest results remain separate from human acceptance.

Retain the full reference inventory while testing smaller packaging options
against reviewed fault-detection evidence. A passing pair demonstrates that
attempt's coverage and measurements, not provider adoption or stable latency.
Subscription changes, cache resets/deletions, provider cutover, branch-rule
changes and merging still require explicit user approval.
