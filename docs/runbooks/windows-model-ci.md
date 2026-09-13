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

When deliberately expanding the model, update its pinned counts and the
independent `CENSUSES` in `scripts/verify_windows_model_shards.py`. Preserve the
named exclusions; a previously impossible state becoming reachable must be
reviewed as a model change.

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

Artifact names include the workflow run and partition, with replacement enabled.
This preserves a successful sibling's evidence when GitHub reruns only failed
jobs; the rerun replaces the failed partition's report within the same run/SHA.
The two build jobs retain the existing Namespace model cache lineage: private
cache forks contain the same release/NuGet build graph, and trusted-main versus
untrusted-PR profiles remain separate. Reports live outside that cache.

The pre-change baseline was about 31 minutes executing the model plus two to
three minutes of setup in runs 34769234555 and 34768717157. Two balanced partitions
target roughly 18 minutes elapsed, subject to runner availability and scenario
cost. This is an estimate until measured on the new CI jobs. It repeats some
setup and uses two runners concurrently; it retains every scenario.

Use the phase reports to decide whether further work on session setup,
arrangement, persistence or cleanup is worthwhile. The other Windows lanes,
especially the app-build-to-accessibility sequence, can become the overall
critical path. Do not reduce scenario coverage, shorten failure timeouts, share
mutable sessions, or remove dispatcher drains to reach a timing target.
