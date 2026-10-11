# Windows overflow routing and host cleanup — design

Date: 2026-10-11, revised the same day after an independent red team
(23 findings, none critical; every high and medium is resolved below and
named where it changed the design). Extends
`docs/superpowers/specs/2026-10-10-self-hosted-windows-runner-design.md`
(the home pool, its orchestrator and its manual fallback, D-3 there).

## 1. Goal

A `windows.yml` lane waits for a home slot only when waiting costs the run
no time. When a slot is free, the lane that would cost the most on the
vendor takes it; when none is free, a lane queues at home if it would still
finish inside the run's critical path, and otherwise runs on Namespace at
once. The owner's words: concurrency over a queue, first in first out, with
the expensive lanes preferred for home; the revision adds "and never pay the
vendor to save no time".

The shell gate always runs on `windows-latest`: it is free on a public
repository and measured faster there (10–14 min) than at home (15–17 min),
so a home slot would buy nothing and cost a paid lane its place.

Two things ride along because the owner asked for them in the same breath:
the host cleans up after itself without a hand where that is safe, and the
public repository keeps revealing nothing it did not already reveal.

`nightly.yml` and the pilot keep their own rules (§11): with the mode on
`home` the nightly stress job still queues home unconditionally.

## 2. Decisions

- **D-1 Overflow, not a blind queue.** `WINDOWS_RUNNER_MODE=home` now means
  "home first; queue only when it costs no time; vendor otherwise". A new
  value `home-only` keeps today's behaviour (every lane queues home) for
  golden-refresh validation and fidelity runs. `namespace`, unset or any
  other value still sends every lane to the vendor exactly as before, and
  does so without running any router code (M-5).
- **D-2 The decision is made per job, when it becomes schedulable,** by a
  routing job on hosted Linux that reads the Actions jobs queue — the shape
  of the mac runner's `ci/mac-runner/route/route.py`. GitHub fixes a job's
  runner at queue time and offers no fallback, so this is the only place a
  dynamic choice can be made.
- **D-3 Cost ranking with a critical-path bound.** Lanes are considered in
  vendor-cost order (app build + test, model shard 0, model shard 1, rust
  tests). A free slot goes to the next lane in that order. A lane with no
  free slot queues at home when the pool's simulated schedule finishes it
  no later than the run's critical path (the app lane plus the shell gate);
  otherwise it goes to the vendor now. The app lane never queues: any wait
  on it lengthens the run.
- **D-4 A dead or crippled host routes away by itself.** An admissible
  `slate-win-*` job queued for more than five minutes while fewer jobs are
  in progress than there are slots means the pool cannot serve, and the
  router sends everything to the vendor until the host takes its queue
  again. Queued jobs older than two hours are stuck, not demand, and are
  ignored (H-1, L-6).
- **D-5 Fork pull requests never route home, enforced by the host.** A fork
  runs its own workflow file and its own router, so the workflow-side rule
  is only defence in depth (H-2). The control is the host: at admission it
  skips any job whose run's head repository is not `TrustedRepo`. The
  approval policy for outside contributors (`all_external_contributors`,
  verified) stays the gate that decides whether a fork's workflow runs at
  all. The first spec's allowance of an approved fork on a throwaway VM is
  withdrawn.
- **D-6 A partial re-run never trusts a stale decision.** GitHub reuses a
  successful routing job's outputs when only failed jobs are re-run (H-3),
  so each lane uses the routed label only when the router ran in the lane's
  own attempt and otherwise takes the vendor expression. "Re-run all jobs"
  is the way to re-route; the runbook says so.
- **D-7 Cleanup stays within each actor's rights.** The orchestrator prunes
  its own logs. It decides when the previous golden image is no longer
  needed, and an elevated task registered by `setup-host.ps1` deletes it,
  because the orchestrator's account has read and execute on `golden` by
  design (H-4). The branch sweep reports; it deletes only what `main`
  already contains, and only on an explicit dispatch (H-5).

## 3. The routing rule

**Decision point.** One per `windows.yml` run: the `route` job at the
start, for rust tests, app build + test, model shard 0 and model shard 1.
The shell gate is fixed to `windows-latest` (goal) and needs no routing;
the two Ubuntu gates are not Windows lanes.

**Admissible job.** A job whose labels contain exactly one label matching
`^slate-win-(rust|app|model|shell)$` (case-insensitive) and no other
`slate-win-*` label — the host's own admission predicate
(`SlateCiHost.psm1`, `Resolve-RunnerJob`); the Pester and Python suites
share test vectors for it. Jobs of runs whose `head_repository.full_name`
is not the repository are excluded (D-5). A queued admissible job whose
`created_at` is more than `ROUTE_STALE_SECONDS` (7 200) old is **stuck**:
listed in the summary, counted nowhere.

**Occupancy.** Every admissible, non-stuck job with status `queued` or
`in_progress` in any run of the three routed workflows (`windows.yml`,
`nightly.yml`, `windows-ci-pilot.yml`), the run being routed included: its
own already-routed lanes genuinely hold or wait for slots (M-1); its
unrouted lanes are absent from GitHub's listing while they wait on `needs`
(verified live). Liveness comes from `status` only; a queued job's
`started_at` equals its `created_at` and means nothing (L-8).

**Free slots** = `ROUTE_SLOTS` − occupancy, floored at zero. `ROUTE_SLOTS`
defaults to 2 and a unit test asserts it equals the `Slots` count in the
repository's `ci/windows-runner/config.json` (L-5); drift against the
host's installed copy is the runbook's capacity step.

**Dead or crippled host (D-4).** If some admissible, non-stuck queued job
is older than `ROUTE_DEAD_SECONDS` (300) while the number of in-progress
admissible jobs is below `ROUTE_SLOTS`, free slots are zero and no lane
may queue: everything goes to the vendor. Normal handoff is one to two
minutes (12–14 s to hand off, up to a minute for the runner to come
online, up to a minute for GitHub to report), so five minutes leaves
margin, and "below the slot count" catches a host with one wedged slot
(a VM stuck in teardown, an unclaimed VM after a cancellation) as well as
a dead one.

**Allocation (D-3).** Constants, one table in the module, revised as the
warm-cache figures come in:

| lane | home duration | vendor duration | vendor target |
|---|---|---|---|
| app build + test | 25 min | 22 min (measured 19–25) | `namespace-profile-winx64-fast[-pr];overrides.cache-tag=slate-windows-app` |
| model shard (each) | 20 min (17 idle, 26 beside the app job) | 6 min | `namespace-profile-winx64-fast[-pr];overrides.cache-tag=slate-windows-model` |
| rust tests | 10 min | 2 min | `namespace-profile-winx64-fast[-pr];overrides.cache-tag=slate-windows-rust` |
| shell gate | — | 13 min (measured 10–14) | `windows-latest`, always |

The router simulates the home pool: each slot is busy until the expected
finish of its in-progress job (`started_at` plus the home duration of the
job's lane, never earlier than now), then the queued admissible jobs, oldest
first, take slots as they free (FIFO across runs). The run's **deadline**
is the app lane's expected finish plus the shell gate's duration, where the
app lane's finish is `now` plus its home duration if it gets a free slot
and `now` plus its vendor duration otherwise. Then, in cost order:

1. **app**: a free slot → home; none → vendor. It never queues.
2. **model-0, model-1, rust**, each in turn: its expected start is the
   earliest slot free in the simulation after everything already placed;
   if start + home duration ≤ deadline → home (and the slot is busy until
   then for the next lane); otherwise → vendor now.

Worked example, idle pool, cold constants: app home (slot 1 busy 25 min),
model-0 home (slot 2 busy 20 min), model-1 would start at 20 and finish at
40 against a deadline of 38 → vendor (6 paid minutes), rust starts at 20
and finishes at 30 → home. Pool busy with another run's app lane 10 min
in: app → vendor (deadline 35), model-0 starts at 15 on the freed slot and
finishes at 35 → home, model-1 → vendor, rust starts at 35 → vendor. With
warm constants (shard 15 min) model-1 fits and the floor is zero.

**Cost floor stated (M-6).** With cold constants the rule pays the vendor
about 6 minutes per idle-pool run (model shard 1); with warm ones nothing.
Pure overflow would have paid about 8 per run, roughly 1 700 a month at
the current rate of about 214 completed runs a month, which is why D-3
bounds the queue by the critical path instead of forbidding it.

**Fork pull requests (D-5, workflow side).** When the event is
`pull_request` and the head repository is not `github.repository`, every
lane gets its vendor target and the summary says why. This is defence in
depth only; the host check is the control.

**Modes.** The variable is compared trimmed and case-insensitively, the
semantics GitHub's `==` already gave `Home` (L-1); `github.ref` is compared
exactly, so only `refs/heads/main` gets the main vendor profile:

| value | behaviour |
|---|---|
| `home` | the rule above |
| `home-only` | every lane to its home label, queueing as today (validation and fidelity runs, M-8) |
| `namespace` | every lane to its vendor target; no API call is made |
| unset or anything else | as `namespace`; the summary names the value |

**Caches and provenance follow the chosen pool.** A lane sent to the vendor
runs its Namespace cache step; a lane sent home runs none and gets the
lane's cache disk; the host's commit rule (trusted repo, `main`, push /
schedule / dispatch, success, generation unchanged) is untouched.

**Two operating systems (M-3).** Under `home`, a lane runs on Windows 11
Pro as a standard user at home or on Windows Server as administrator on the
vendor, chosen by occupancy, and a commit can pass on one and fail on the
other. The trade is accepted: each lane's first step prints its pool, the
aggregate gate's failure line names the pool per lane, and the nightly
stress job (home, unconditionally) plus `home-only` runs after a golden
refresh keep the home environment exercised on schedule.

## 4. The router

**Location.** `ci/windows-runner/route/route.py`, tests in
`ci/windows-runner/route/tests/test_route.py`, run by
`windows-runner-tests.yml` with
`python -I -m unittest discover -v -s ci/windows-runner/route/tests -t ci/windows-runner/route`
next to the Pester suite. The mac router lives at the matching path and the
two workflows call theirs the same way.

**Inputs, all from the environment**, with test seams:

| variable | meaning |
|---|---|
| `WINDOWS_RUNNER_MODE` | the repository variable |
| `ROUTE_LANES` | comma list of the lanes to route: `rust,app,model-0,model-1` |
| `ROUTE_REF` | `github.ref`, for the `-pr` suffix |
| `ROUTE_ATTEMPT` | `github.run_attempt`, echoed as the `attempt` output (D-6) |
| `ROUTE_RUN_ID` | `github.run_id`, for the summary only |
| `ROUTE_EVENT`, `ROUTE_HEAD_REPO`, `ROUTE_REPO` | the fork rule's inputs |
| `ROUTE_SLOTS` | pool size, default 2 |
| `ROUTE_DEAD_SECONDS`, `ROUTE_STALE_SECONDS` | defaults 300 and 7 200 |
| `ROUTE_NOW` | epoch seconds, for tests |
| `ROUTE_JOBS_JSON` | a path to a reduced listing to use instead of the API, for tests and for replaying a decision |
| `GH_TOKEN` | `${{ github.token }}`, set explicitly in the step's `env` (M-4) |
| `GITHUB_REPOSITORY`, `GITHUB_OUTPUT`, `GITHUB_STEP_SUMMARY` | the usual |

**Reads.** With `gh api` and the token above, under the job permissions
`actions: read` and `contents: read` (M-4): for each routed workflow,
`GET /repos/{repo}/actions/workflows/{file}/runs?status=queued` and
`?status=in_progress` (both, because a run's status does not track its
jobs' — a run listed as `queued` held two in-progress jobs live), then
`GET /repos/{repo}/actions/runs/{id}/jobs` for each run returned, every
call paginated at `per_page=100` and given a 15-second timeout, 60 seconds
in all (M-5). The listing is reduced to
`(job id, run id, head repository, status, labels, created_at)` before any
rule runs, and the reduced listing is written to the log and the summary
so `ROUTE_JOBS_JSON` can replay the decision a day later (L-4). A
`created_at` that does not parse keeps the job in occupancy and out of
every age test (L-8).

**Decision.** A pure function

```
decide(mode, lanes, jobs, now, *, slots, dead_seconds, stale_seconds,
       ref, event, head_repo, repo, durations)
  -> {lane: Decision(pool, label, reason)},
     Occupancy(in_progress, queued, stuck, oldest_queued_age, dead, deadline)
```

with no I/O, so every rule in §3 is a unit test on a hand-written listing:
free slots; the cost order; the critical-path simulation with the worked
examples above and at the deadline boundary; the app lane never queueing;
the dead-host rule at 299, 300 and 301 seconds with in-progress counts of
0, 1 and 2; a wedged slot; a stuck job ignored; the admissibility
predicate (shared vectors with the Pester suite, including `slate-win-x`,
two labels, `SLATE-WIN-APP`); fork runs excluded and the fork rule; mode
`home-only`; mode `namespace`; `Home ` with case and whitespace; an unknown
mode; an empty listing; the routed run's own lanes counted; a malformed
timestamp; every vendor string verbatim for `refs/heads/main` and for a
branch (M-7); `DEFAULT_SLOTS` and `ROUTED_WORKFLOWS` against
`config.json`. `main()` wraps `decide` in the reads and the writes; any
exception on the way makes every lane a vendor lane with the exception's
type in the reason and a `::warning::` annotation on the run (M-4), never
a failed step.

**Outputs**, appended to `$GITHUB_OUTPUT` and echoed when it is unset:

| output | value |
|---|---|
| `<lane>` | the full `runs-on` string for the lane |
| `<lane>-pool` | `home` or `vendor` |
| `attempt` | `ROUTE_ATTEMPT`, so a lane can tell a reused output from its own (D-6) |

Lane names in outputs are `rust`, `app`, `model-0`, `model-1`.

**Summary.** One table in `$GITHUB_STEP_SUMMARY`: mode, occupancy (in
progress, queued, stuck, oldest queued age), free slots, the deadline, then
a row per lane with pool, label and reason, then the reduced listing. The
title names the mode, not the host's state (L-9).

**Constants**, declared at the top of the module with the measurements
that justify them: `HOME_LABELS`, `VENDOR_TARGETS`, `COST_ORDER`,
`DURATIONS` (the table in §3), `DEFAULT_SLOTS = 2`,
`DEFAULT_DEAD_SECONDS = 300`, `DEFAULT_STALE_SECONDS = 7200`,
`ROUTED_WORKFLOWS` (the three paths, equal to the host's `config.json`).

## 5. Workflow changes (`windows.yml`)

- **`route`**: first job, `name: Route Windows lanes`, `runs-on:
  ubuntu-latest`, `timeout-minutes: 5`, `permissions: { actions: read,
  contents: read }`, sparse checkout of `ci/windows-runner/route` with
  `persist-credentials: false`, one step running the router with
  `continue-on-error: true` (M-5), `env` carrying `GH_TOKEN: ${{ github.token }}`,
  `WINDOWS_RUNNER_MODE: ${{ vars.WINDOWS_RUNNER_MODE }}`,
  `ROUTE_LANES: rust,app,model-0,model-1`, `ROUTE_ATTEMPT: ${{ github.run_attempt }}`
  and the rest of §4's inputs; job outputs `rust`, `app`, `model-0`,
  `model-1`, their `-pool` twins and `attempt`.
- **Every routed lane** gains `needs: [route]` (the model matrix job has
  no `needs` today and gains this one), `if: ${{ !cancelled() }}` so a
  failed routing job does not skip it (M-5), and a `runs-on` that trusts the
  router only in its own attempt and falls back to today's vendor expression
  otherwise (D-6, M-5), for example for rust:
  `${{ needs.route.outputs.attempt == github.run_attempt && needs.route.outputs.rust || format('namespace-profile-winx64-fast{0};overrides.cache-tag=slate-windows-rust', github.ref != 'refs/heads/main' && '-pr' || '') }}`.
  The model matrix picks `model-0` or `model-1` by `matrix.shard` inside
  the same shape. The three Namespace cache steps run unless the lane is
  home in this attempt:
  `if: ${{ !(needs.route.outputs.attempt == github.run_attempt && needs.route.outputs.rust-pool == 'home') }}`.
- **Each routed lane's first step prints its pool** (M-3), and the
  aggregate gate's failure message names the pool per lane.
- **The shell gate** (`flaui`): `runs-on: ${{ vars.WINDOWS_RUNNER_MODE == 'home-only' && 'slate-win-shell' || 'windows-latest' }}`
  — `windows-latest` in every mode but `home-only` (goal, M-2); its
  `needs` and its `needs.windows.result == 'success'` condition are
  unchanged.
- **Unchanged**: job and check names, the aggregate gate's `needs`, the
  concurrency group, every `timeout-minutes`, the model coverage gate,
  `nightly.yml`, the pilot, and the host's `RoutedWorkflows`.
- **Path filter**: `ci/windows-runner/route/**` is added to `windows.yml`'s
  own filter so a router change runs the workflow it steers (L-3).
- **The header comment** describes the four modes, the critical-path rule,
  the fork rule, "Re-run all jobs" after a flip, and where to read a run's
  routing.
- **`ci/windows-runner/tests/Workflows.Tests.ps1`** is rewritten for the
  new shape and keeps every pin it has today or moves it to the Python
  suite (M-7): the routing job exists with both permissions, the sparse
  checkout, `continue-on-error`, `GH_TOKEN`, the mode variable in `env`
  and `ROUTE_LANES`; every routed lane's `runs-on` has the attempt-guarded
  shape above with today's vendor expression verbatim as the fallback; the
  three cache gates have the attempt-guarded shape; every routed lane has
  `if: ${{ !cancelled() }}`; the shell gate's `runs-on` is the `home-only`
  expression; `vars.WINDOWS_RUNNER_MODE` appears in `windows.yml` only in
  the routing job's `env` and the shell gate's `runs-on`; `nightly.yml`
  and the pilot keep their guards verbatim; `windows-runner-tests.yml` runs
  the router's unit tests; `windows.yml`'s path filter names the router.
- **`windows-runner-tests.yml`** gains the Python step;
  `ci/windows-runner/route/**` is inside its filter already.

Latency added per run: the `route` job before the lanes (API reads of a
few seconds; hosted pickup measured 5–10 s).

## 6. Host changes

- **Admission skips other repositories (D-5).** `Invoke-Admission` reads
  the run of each candidate job (`GetRun`, Actions read, already granted,
  conditional request) and skips any whose `head_repository.full_name`
  differs from `TrustedRepo`, logging
  `admission: skipped job <id> of <repo> (not the trusted repository)` once
  per job. Such a job stays queued until GitHub drops it; the approval
  policy is what keeps it from existing.
- **Log retention (D-7).** `Get-ExpiredLogNames -Names -Today -RetentionDays`
  is pure and tested: names of the form `orchestrator-YYYY-MM-DD.log` whose
  local date (the writer uses local dates) is older than `RetentionDays`
  (config key `LogRetentionDays`, **defaulted to 30 when absent** so an
  older `config.json` keeps working, L-7); names that do not parse are
  kept. Runs at startup and at the first tick after local midnight
  (journal key `LogsPrunedOn`, a local date); a delete that fails logs
  and continues.
- **Golden acceptance (D-7).** At settle with conclusion `success`, when
  the VM's `CreationTime` is later than the golden's `LastWriteTime`, the
  orchestrator records the lane in journal key `GoldenAccepted`
  (`{ Golden = <LastWriteTime>; Lanes = @(...) }`, reset when the golden's
  `LastWriteTime` changes). Once `rust`, `app` and `model` are all present
  and no VM directory holds a VM created before the golden, it logs once:
  `golden: the previous image is no longer needed (rust, app and model are
  green on the current one)` and writes `GoldenPrevRemovableAt`. The
  orchestrator never deletes under `golden`.
- **The prune task.** `setup-host.ps1` registers `slate-ci-golden-prune`,
  daily at 04:00 local, running as SYSTEM, which deletes
  `golden\win11-runner.prev.vhdx` only when the journal's
  `GoldenPrevRemovableAt` is later than the prev's `LastWriteTime` and no
  VM directory predates the current golden; it appends one line to the
  orchestrator log either way. `build-golden.ps1` gains `-Rotate`: an
  existing `win11-runner.vhdx` is renamed to `.prev` (replacing an older
  prev) before the build, which is the only moment someone elevated is
  present; the runbook's manual rename becomes that switch, and its
  rollback (rename the prev back) is unchanged and still possible until the
  prune task runs after a green rust, app and model.

## 7. Failure modes and observability

- **The router never fails a run** (M-5): the routing step is
  `continue-on-error`, every API call has a timeout, every exception becomes
  vendor labels with a warning annotation, and a lane whose router output is
  empty, stale or missing takes today's vendor expression and still runs.
  `namespace` mode runs no router code at all (D-1).
- **Dead or crippled host** (D-4) recovers by itself once the host takes
  its queue; a stuck job cannot hold the pool off (H-1).
- **Stranded lanes.** D-4 stops future routing only; a lane already queued
  home when the host dies waits until it is cancelled. The runbook's pool
  switch says: flip to `namespace`, cancel the run, **re-run all jobs**
  (not "failed jobs", D-6), and notes that the main concurrency group
  holds later main runs while a stranded lane waits.
- **Double booking.** Two runs routing within the same seconds can both
  see the last slot free; the loser queues one lane's length, which the
  critical-path bound already tolerates for the lanes it would have
  queued, and both summaries show what each saw.
- **The settle window.** A VM holds its slot until GitHub reports the
  completion and the host settles it (up to a minute); the router counts
  that slot free; the next home lane may wait that minute.
- **Reduced capacity** (L-6) is covered by the "in progress below the
  slot count" form of D-4.
- **Pool size drift** between the repository's `config.json` and the
  host's installed copy: the runbook's capacity step.
- **Namespace outages** are untouched.
- **Reading a run's routing**: the summary table and the reduced listing;
  the host log's new lines (`admission: skipped job …`, `golden: …`); a
  runbook section beside the log-line list.

## 8. Security

- **What the public repository reveals is unchanged.** `config.json`
  carries an RFC 1918 NAT prefix, slot addresses on it, lane caps,
  timeouts, the trusted repo, branch and events, and the routed workflow
  paths; it never carried the token, the product key or the router's public
  address. The new module adds the ranking, the durations, the pool size
  and the label templates the workflow file already publishes. The routing
  summary publishes occupancy counts and ages that the public job timings
  already imply (L-9).
- **What the router sees** is the jobs listing of a public repository,
  public already, read with the run's `GITHUB_TOKEN` under `actions: read`
  and `contents: read`. A fork PR's token is read-only.
- **What the router decides** is which of two already-trusted targets a
  lane runs on; a fork can rewrite the router, which is why the fork rule
  lives on the host (D-5). Labels still carry no trust: the host still
  commits caches only for trusted events on `main`, every VM is still
  thrown away, and the approval policy still gates outside contributors.
- **What a malicious workflow edit could do** is unchanged: it could
  already choose `runs-on`; the approval policy and now the host's
  repository check answer it.
- **The host's PAT** keeps its scope (Actions read, Administration read
  and write); the router never uses it; `GetRun` is a read it already has.
  No new secret anywhere.
- **The prune task** runs as SYSTEM but acts on one fixed path under a
  condition the unprivileged orchestrator wrote; the orchestrator's account
  gains no right on `golden`.
- **`pull_request_target`** is not used by any Windows workflow and the
  fork rule does not cover it; adding it would need its own review (L-8).

## 9. Branch cleanup

A weekly workflow `.github/workflows/branch-cleanup.yml` (`schedule`,
Mondays 06:00 UTC, and `workflow_dispatch` with a boolean input `delete`,
default false; `permissions: { contents: write, pull-requests: read }`)
lists every `refs/heads/claude/*` branch with, for each, whether its tip is
contained in `main` (`compare main...<branch>`, `ahead_by == 0`), whether
it is the head or the base of an open pull request, and its tip's committer
date. It writes the list to its summary. With `delete=true` it deletes only
branches whose tip is contained in `main` and that are the base of no open
pull request, asserting the `claude/` prefix on every name before the call.
Unmerged branches are never deleted by it: the repository already deletes
merged heads, so the `claude/*` branches that remain are exactly the
unmerged ones (two with never-reviewed work today, H-5), and no branch is
protected. The four present on 2026-10-11
(`claude/1285-deferred-summary-wip`, `claude/home-pilot-smoke`,
`claude/laughing-meitner-86n5do`, `claude/runbook-realtek-ndis`) are the
owner's one-off; the runbook change lists them with their ahead-by counts.

## 10. Testing

- `route.py`: the unit tests of §4, run in `windows-runner-tests.yml`.
- Host: Pester tests for the admission repository check (skipped with the
  log line; trusted admitted; `GetRun` conditional), `Get-ExpiredLogNames`,
  the once-a-day gate across a restart, the default for a missing
  `LogRetentionDays`, the golden acceptance (lanes recorded only for VMs
  newer than the golden; reset on a new golden; the signal once all three
  lanes are present and no older VM exists; never a delete), the prune
  task's registration in `Install.Tests.ps1`, and `-Rotate` in
  `Golden.Tests.ps1`.
- Guards: `Workflows.Tests.ps1` as rewritten in §5.
- Integration, on the host, in the runbook's integration pass: with one
  run in flight, dispatch a second and read its summary: its app lane went
  to the vendor, its shards queued or overflowed as the deadline says, its
  rust lane queued home; with the loop stopped, dispatch a run and read
  that it routed everything away within the five-minute rule; after a
  golden refresh, run with `home-only` and read that every lane ran home
  and the acceptance line appeared.

## 11. Documents touched

- `docs/runbooks/self-hosted-windows-runner.md`: the routing section
  (modes including `home-only`, the critical-path rule, the fork rule),
  "Reading a run's routing", the pool switch ("re-run all jobs"; stranded
  lanes), the capacity step (both places), the refresh steps (`-Rotate`,
  `home-only` validation, the acceptance line, the prune task), the cleanup
  section (what is automatic now, the one-off branch list), the log-line
  list, and the hosted shell figure (10–14 min, not 17).
- `docs/superpowers/specs/2026-10-10-self-hosted-windows-runner-design.md`:
  D-3's "manual" and the parked `auto` follow-up point here; the fork-PR
  allowance is withdrawn (D-5).
- The `windows.yml` header comment.

## 12. Out of scope

`nightly.yml` and the pilot keep their own rules: the nightly stress job
queues home unconditionally under `home`, so it can overbook the pool or
strand when the host is down, and can adopt the router later (L-10). A
heartbeat from the host is unnecessary while the queue itself carries the
dead-host signal. Mitigating the double-booking race is not worth a host
change. Whether the model coverage inventories differ by operating system
is unverified and worth one check when the first `home` run of a PR lands
on the vendor for a shard (M-3).
