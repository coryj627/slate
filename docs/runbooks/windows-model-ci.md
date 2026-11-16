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
in a phase still reaches the file. The checkpoint's `elapsedMilliseconds` is the
family clock read when the phase was entered, not when the file was written: a
case that has stood in one phase for four minutes still shows the time it
entered that phase, and at five minutes the stall rule below rewrites the
checkpoint. A completed case clears the active case; one
that threw stays named in the final `failed` checkpoint. A completed family
records at most sixteen slow cases with phase durations. These diagnostic text
files do not change schema 1 JSON, the inventory digest, or the independent
coverage verifier. They can identify where a stopped process last made progress,
up to a second behind it; a hard runner termination can still prevent artifact
upload.

The flusher's tick is also the stall rule (#1624). The rule speaks when 300
ticks in a row have read the same published checkpoint
(`ModelTestRun.StallBound`, five minutes at one tick a second) and that
checkpoint has also stood for 299 seconds on the family clock, the time those
ticks span. Both are needed. The timer queues a callback for every interval
whether or not the thread pool runs it, so a pool that ran none for a while runs
them all at once, and ticks alone could then count against a checkpoint that is
milliseconds old. The clock alone would count the time a machine spent
suspended, where ticks add at most one or two.

The flusher then rewrites the checkpoint with `"phase":"stalled"`. The stalled
checkpoint adds `stalledPhase`, the phase the family stood in, and
`stalledMilliseconds`, how long it had stood there, and its
`elapsedMilliseconds` is read at the stall. If a reader holds the file, the
write is tried five times over less than a second. If the reader outlasts those
tries, the stalled checkpoint goes to a file of its own beside the held one,
`<family>-shard-<n>.stalled.progress.txt`, which the print step below also
reads, and the held file keeps the old phase. The flusher then reads the
published checkpoint once more. If the family advanced, completed or began to be
disposed while the flusher was writing, the stall is withdrawn: nothing on disk
says stalled and the process goes on. Otherwise the flusher writes one
sentence to standard error that names the family, the shard, the case, the
phase, the ticks and the completed count, and ends the process with
`Environment.FailFast`, whether or not the file took the stalled checkpoint. The
stalled thread is in synchronous code and cannot be unwound. The model step sets
the `DOTNET_DbgEnableMiniDump` variables, so the runtime writes a full dump of
every thread to `crash-<process>-<pid>.dmp` in the results folder, which the job
creates before the test step because the runtime's dump writer does not create a
missing one.

Five minutes is above every hold a case has a bound for. A healthy phase is well
under a second. One bounded wait that runs to its bound holds a phase for about
70 seconds: a settle is a 10-second pump and two 30-second drains. A case that
is failing can hold one phase for up to about 165 seconds while it unwinds. The
rule never speaks after a family completed or under a debugger. It runs only
where `SLATE_MODEL_REPORT_DIR` is set, because the flusher runs only there. That
includes the pilot (`windows-ci-pilot.yml`), whose file is unchanged: a stalled
pilot shard now ends after about five minutes with the stalled checkpoint in its
coverage artifact and the sentence in its TRX, and with no dump and no line in
the log, because the pilot sets no dump variables and has no print step.

Two timers can end a model run. The stall rule ends a case that stops advancing,
after about five minutes. VSTest's blame collector ends a fact after 70 minutes
(`--blame-hang-timeout 70m`) and writes its own dump. A family is one fact, and
blame's timer resets only between facts, so that timer measures a whole family.
It is the backstop for what the stall rule cannot see: a hang before a family
publishes its first checkpoint (the flusher starts with it), a hang after the
family is disposed, and anything that stops the flusher itself. The rule does
see a stall between two cases. The timer must also leave a slow family room to
finish: run 40119788856 lost a routes fact to the earlier 45 minutes while it
was still advancing at 0.60 seconds a case on a loaded host, a projected 62
minutes. The job's limit stays
90 minutes, which leaves 20 for the build, the other families, the dump and the
uploads, so the timer ends a slow run and its evidence is uploaded. After the
test step, `Show where each model family stopped` prints every checkpoint into
the job log, with an error line for a stalled one, so the log names the case
whatever the test adapter printed. In a local run of a planted stall the
adapter's console showed only `[FATAL ERROR] Xunit.Sdk.TestPipelineException`;
the sentence and the fail-fast's stack were in the TRX.

Checkpoint filesystem access is best effort: a Windows reader can briefly deny
atomic replacement without invalidating model behavior. The canonical report
records `missedProgressWrites`; inventory assertions, completion accounting and
final coverage JSON writes remain strict. A checkpoint may therefore be stale
even when canonical evidence establishes full completion.

Each partition uploads up to two artifacts. The report,
`windows-model-<run>-<partition>`, holds the TRX, the JSON reports and the
checkpoints, without dumps, for 30 days. Its name has no attempt and replacement
is enabled, because the verification job reads it by run. This preserves a
successful sibling's evidence when GitHub reruns only failed jobs; the rerun
replaces the failed partition's report within the same run/SHA. An attempt that
failed or was cancelled also uploads its whole results folder, dumps included,
as `slate-windows-model-evidence-<run>-<partition>-<attempt>` for 7 days, so a
rerun cannot replace a failed attempt's dumps. The job log, with the printed
checkpoints, is kept 90 days. The repository is public, so a dump is published
only because the model job holds no secret for the test process to have read;
`TestHostCrashDumpCensus` fails if the job gains one.
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
