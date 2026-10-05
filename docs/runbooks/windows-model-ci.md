# Windows model CI

The Connections model exercises 13,069 scenarios through real filesystem
sessions and workspaces. Windows CI runs two independent partitions of that
complete inventory. Each test process retains serial dispatcher execution and
creates fresh mutable state for every scenario.

The `app model (windows x64)` check requires both partitions to pass and verifies
their reports before the existing `build + test (windows x64)` gate can pass.
It rejects missing families, duplicate partitions, changed census counts,
inconsistent inventory hashes, incomplete scenarios and overlapping coverage.
General xUnit parallelism remains disabled for the suite's process-global native
counters and timing tests.

## Inventory and partition contract

| Family | Total cells | Named exclusions | Executed scenarios |
| --- | ---: | ---: | ---: |
| routes | 25,200 | 12,812 | 12,388 |
| reroot | 5,760 | 5,216 | 544 |
| composed | 196 | 59 | 137 |

Every process enumerates the complete inventory and validates these counts.
Reachable scenarios receive their original one-based ordinal before partition
selection. Partition `index` of `count` owns scenarios for which
`(ordinal - 1) % count == index`. Arrangement stamps retain the original global
ordinal. Changing the partition count does not change a scenario's initial
state, expected result or assertions.

`SLATE_MODEL_SHARD_INDEX` and `SLATE_MODEL_SHARD_COUNT` must either both be absent
(the complete local run) or both be valid. The developer-only
`SLATE_MODEL_ONLY` text filter cannot be combined with partitioning or report
output. It must never substitute for a complete CI partition.

When deliberately changing the model, update both pins on each side: the
counts and inventory digest passed to `AssertInventory` in the C# model facts,
and the independent `CENSUSES` and `INVENTORY_SHA256` in
`scripts/verify_windows_model_shards.py`. Renaming a route or rewording an
exclusion reason changes only the digest. Preserve the named exclusions; a
previously impossible state becoming reachable must be reviewed as a model
change.

## Local verification

Build the Release test project with the normal generated Release native bindings.
With no model environment variables set, the existing command still runs the
complete model:

```powershell
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ConnectionsLeafTests.TheModelOf"
```

For a complete partitioned run, run the same command in two separate processes,
setting these variables for each process and using a separate report directory:

```powershell
$env:SLATE_MODEL_SHARD_INDEX = '0' # use '1' in the other process
$env:SLATE_MODEL_SHARD_COUNT = '2'
$env:SLATE_MODEL_REPORT_DIR = 'C:\temp\slate-model-results\shard-0'
$env:SLATE_MODEL_ONLY = ''
```

Verify the complete union of the two report directories:

```powershell
python scripts/verify_windows_model_shards.py --reports-dir C:\temp\slate-model-results --shard-count 2
python -m unittest discover -s scripts/tests -p test_windows_model_shards.py
```

Use fresh report directories for each local run. Remove the model environment
variables when returning to an ordinary full-suite run. An unpartitioned full
run can also emit reports; validate those with `--shard-count 1`.

## Evidence and performance

Each CI partition uploads its TRX plus one JSON report per model family, including
the full inventory digest, selected/completed ordinals, success status, family
elapsed time and aggregated route/phase timings. The verification job summarizes
family durations in its job summary. Reports remain available on failures.

During execution each family also keeps a bounded, atomically replaced
`*.progress.txt` checkpoint containing the active case and phase. A case thread
only records each transition in memory; a background flusher writes the latest
one about once a second, so no phase timing includes a write and a case stalled
in a phase still reaches the file. A completed case clears the active case; one
that threw stays named in the final `failed` checkpoint. A completed family
records at most sixteen slow cases with phase durations. These diagnostic text
files do not change schema 1 JSON, the inventory digest, or the independent
coverage verifier. They can identify where a stopped process last made progress,
up to a second behind it; a hard runner termination can still prevent artifact
upload.

Checkpoint filesystem access is best effort: a Windows reader can briefly deny
atomic replacement without invalidating model behavior. The canonical report
records `missedProgressWrites`; inventory assertions, completion accounting and
final coverage JSON writes remain strict. A checkpoint may therefore be stale
even when canonical evidence establishes full completion.

Artifact names include the workflow run and partition, with replacement enabled.
This preserves a successful sibling's evidence when GitHub reruns only failed
jobs; the rerun replaces the failed partition's report within the same run/SHA.
The two build jobs retain the existing Namespace model tag: private forks contain
the same release/NuGet build graph. A custom tag shares its persisted volume
across profiles and repositories; main/PR profile names do not establish cache
isolation. Verify provider enforcement as described in
[CI cache policy](ci-cache-policy.md). Reports live outside that cache.

The downloaded reports from [the September baseline](https://github.com/coryj627/slate/actions/runs/36749141900)
sum to 1,851.423 seconds of family execution across both partitions. The first
[repaired CI run](https://github.com/coryj627/slate/actions/runs/36797768521) sums to
491.320 seconds, a 73.5% reduction in this observed pair. Shutdown drive fell from
1,439.754 to 2.188 seconds. All six inventory digests and selected-ordinal lists
match, and the verifier establishes exactly-once completion of all scenarios.
The slower partition's family execution is about 4.25 minutes; setup, queue,
build and downstream checks remain additional costs. These are observed runs,
not stable tail latency or a monthly billing forecast.

The September 30 implementation removes a test-induced five-second shutdown
fallback: a retained helper observes retirement, then releases the parked worker
while owner-thread disposal waits for it. Helpers and workers are joined before
their resources are disposed, including assertion-failure paths. Following work
is now parked as well as pinned work. Production timeout policy is unchanged;
the focused six-second rescan witness still exercises its real boundary. Compare
the repaired full inventory on the same CI image before updating latency targets.

Use the phase reports to decide whether further work on session setup,
arrangement, persistence or cleanup is worthwhile. The other Windows lanes,
especially the app-build-to-accessibility sequence, can become the overall
critical path. Do not reduce scenario coverage, shorten failure timeouts, share
mutable sessions, or remove dispatcher drains to reach a timing target.
