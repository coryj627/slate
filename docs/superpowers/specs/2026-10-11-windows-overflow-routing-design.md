# Windows overflow routing and host cleanup — design

Date: 2026-10-11. Revision 3: after a design red team (23 findings) and a
four-way plan red team (correctness, reliability, maintainability,
performance; 76 findings, one critical). The one structural change in this
revision is where the router learns the pool's state: from the host itself,
not from GitHub's jobs listing (OR-D2). Extends
`docs/superpowers/specs/2026-10-10-self-hosted-windows-runner-design.md`
(the home pool, its orchestrator and its manual fallback, D-3 there).
Decisions here are prefixed `OR-` so they never collide with that spec's.

## 1. Goal

A `windows.yml` lane waits for a home slot only when waiting costs the run
no time. When a slot is free, the lane that would cost the most on the
vendor takes it; when none is free, a lane queues at home if it would still
finish inside the run's critical path, and otherwise runs on Namespace at
once. The owner's words: concurrency over a queue, first in first out, the
expensive lanes preferred for home, and never pay the vendor to save no
time.

The shell gate always runs on `windows-latest`: free on a public
repository and measured faster there (10–14 min, median 13) than at home
(15–17 min), so a home slot would buy nothing and cost a paid lane its
place.

Two things ride along: the host cleans up after itself where that is safe
(logs; the previous golden image, through an elevated task on the host's
own evidence), and the public repository keeps revealing nothing it did
not already reveal.

`nightly.yml` and the pilot keep their own rules (§12); the nightly stress
job queues home under `home` and `home-only` alike.

## 2. Decisions

- **OR-D1 Overflow, not a blind queue.** `WINDOWS_RUNNER_MODE=home` means
  "home first; queue only when it costs no time; vendor otherwise". The new
  value `home-only` keeps today's behaviour (every lane queues home) for
  golden-image validation and fidelity runs. `namespace`, unset or any
  other value sends every lane to the vendor exactly as before, and the
  routing job does not run at all.
- **OR-D2 The host publishes its state; the router reads it.** Whenever
  its state changes, and at least once a minute, the orchestrator writes
  the repository variable `WINDOWS_HOME_STATE`: a
  heartbeat, whether it is serving, how many slots it has, what runs in
  them and what it has queued (§4). The router reads the variable from the
  `vars` context, with no API call, and decides. This is the mac runner's
  pattern (`MAC_RUNNER_HEARTBEAT`, written by the Studio controller).
  Inferring the pool from GitHub's jobs listing was tried in revision 2
  and shown unreliable: a dead host could only be noticed after a run had
  stranded on it, every normal slot handover looked like a dead host, and
  the router had to mirror admission rules it could not see. It costs no
  new permission: the host's token was issued with the mac controller's
  set (Actions read, Administration read and write, Variables read and
  write), so it already writes repository variables.
- **OR-D3 Cost ranking with a critical-path bound.** Lanes are considered
  in vendor-cost order (app build + test, model shard 0, model shard 1,
  rust tests). The app lane takes a free slot or the vendor; it never
  queues, and "free" is counted from occupancy, never estimated. Every
  other lane queues at home when the simulated schedule finishes it no
  later than the run's deadline (the app lane plus the shell gate); else
  the vendor now. Durations are measured constants in one table, injected
  into the rule so tests do not pin production numbers.
- **OR-D4 A dead or failing host routes away by itself.** The published
  state is stale (its `at` more than 180 s before the routing job's own
  clock), says it is not serving, or offers no slot: every lane goes to
  the vendor. No run has to strand to discover an outage; a crashed host
  is stale within three minutes. A host whose loop runs but whose VMs
  never come up says it is not serving through a breaker: three discards
  of its own making in a row (a VM it could not provision, hand off or
  hear from, or that shut down without running a job) with no runner
  taking its job between them trip it, and the log says why once. It
  serves again after a fifteen-minute cool-down (the next failure trips
  it at once) or when the loop restarts, which is what follows a repair;
  a runner taking its job forgets the failures. `BreakerFailures` and
  `BreakerCooldownSeconds` in `config.json`, 3 and 900 when absent. The
  reference for staleness is the router's clock, not
  the run's creation time: GitHub resolves `vars` when the job starts
  (verified on a re-run whose routing job read a heartbeat written eleven
  minutes after the run was created), and a run's creation time never
  moves across re-runs or a concurrency hold, so measuring against it let
  a dead host look alive on exactly the recovery path the runbook
  prescribes.
- **OR-D5 Fork pull requests never run on the pool, and the host enforces
  what it can.** A fork runs its own workflow file and its own router, so
  the workflow-side rule is defence in depth. The host (a) never
  provisions a VM for a job whose run comes from another repository and
  (b) forces a VM off within one tick when GitHub hands its runner to such
  a job, which GitHub may do because a JIT runner goes to whichever queued
  job matches its label (a JIT runner carries only the labels in its
  configuration, here one lane label, verified on the live runner
  listing; so only a job naming that label can take it, and the host
  matches runners against the full in-progress listing regardless). The approval policy for outside contributors
  (`all_external_contributors`, verified) remains the gate that decides
  whether a fork's workflow runs at all; what the host adds is that a
  fork's job gets at most seconds on a throwaway VM with the fork's own
  read-only token. One gap stays, by design of the listing: the host reads
  only the routed workflows' jobs, so a fork that adds a workflow of its
  own naming the home labels is not evicted (its cache fork is still
  refused); the approval click is the control for that case. A second,
  smaller residue: a job that starts and finishes between two ticks is
  never seen in progress, so the VM settles as "ran no job the host knows
  of", is discarded, and the trusted job it was provisioned for takes one
  retry strike.
- **OR-D6 A partial re-run never trusts a stale decision.** GitHub reuses
  a successful routing job's outputs when only failed jobs are re-run, so
  each lane uses the routed pool only when the router ran in the lane's
  own attempt and otherwise takes the vendor expression that lives in the
  workflow file. "Re-run all jobs" (`gh run rerun <id>` without `--failed`)
  is the way to re-route; the runbook says so and says to flip the mode
  first when the host is down.
- **OR-D7 Cleanup stays within each actor's rights, on facts not
  verdicts.** The orchestrator prunes its own logs and records which lanes
  went green on which golden image. An elevated task registered by
  `setup-host.ps1` decides at deletion time, with one pure predicate the
  module owns, whether the previous image can go, against the current,
  sealed image. `build-golden.ps1 -Rotate` is the only thing that creates
  the previous image, and only from a sealed one. The branch sweep reports;
  it deletes only what `main` already contains, only on an explicit
  dispatch, and lands as its own pull request.

## 3. The routing rule

**Decision point.** One per `windows.yml` run: the `route` job at the
start, for rust tests, app build + test, model shard 0 and model shard 1.
It runs only when the mode is `home` or `home-only`. The shell gate is
`windows-latest` (goal) and the two Ubuntu gates are not Windows lanes.

**Inputs.** The mode; the published state (§4); the lane list; the run's
attempt; the router's own clock, which every age is measured against; the
start time of this attempt (`run_started_at`, one API read,
`GET /actions/runs/{id}`, optional: `now` when it fails; used only to
order concurrent routers); the fork inputs; and, optionally, the other
runs whose routing job started before this one or completed at most
30 s before the state's `at` (a paged listing of `windows.yml`'s queued
and in-progress runs, 100 a page up to five pages, plus their jobs; a
failure on the first page means no earlier router is assumed, a failure
on a later page keeps what was read).

**Dead or failing host (OR-D4).** The state is missing, does not parse,
has `v` other than 1, has `at` more than `ROUTE_STALE_SECONDS` (180)
before the router's clock, has `serving` false, or has `slots` below 1.
Then every lane goes to the vendor and the summary says which test
failed. An `at` in the future counts as fresh and the summary names the
skew. In `home-only` the state is ignored: every lane queues home.

**Occupancy** is what the host published: `running` (one entry per slot
in use: lane and the time the VM was provisioned) and `queued` (the
admissible jobs the host will serve, oldest first: lane and the time the
job was created). The host already excludes what it will never serve
(other repositories, jobs at the retry cap or in back-off), so the router
mirrors no admission rule. Free slots = `slots − len(running) −
len(queued)`, floored at zero.

**Durations**, one table in the router, injected into the rule:

| lane | home (min) | vendor (min) | basis (2026-10-11 runs, 30-day Namespace history) |
|---|---|---|---|
| app build + test | 26 | 22 | home 22–27 beside a neighbour; Namespace 19–25 |
| model shard (each) | 25 | 6 | home 25.3 median beside a neighbour, 15–17 alone |
| rust tests | 11 | 2 | home 10–11 cold |
| shell gate | 16 (`home-only` only) | 13 | windows-latest median of 10, 13, 14; home 15.8 on the acceptance run |

plus `HANDOVER` = 110 s (a slot's turnover: settle, provision, hand off,
runner online; measured 94–131 s) and `OVERRUN_FLOOR` = 300 s (a job past
its duration is not free; it is assumed to need five more minutes). These
are revised from the artifacts, not from the runbook, and the runbook
points at the table.

**Simulation.** Each running entry occupies its slot until
`max(since + duration_home(lane), now + OVERRUN_FLOOR) + HANDOVER`; more
running entries than slots keep the latest-ending ones. Each queued entry,
oldest first, takes the earliest-free slot for `duration_home(lane) +
HANDOVER`. A running entry whose time the router cannot read is taken as having
just started (its own home duration from now); an entry whose lane the
router does not know occupies its slot for the longest home duration in
the table: finite and conservative either way. Then every other run whose routing
job started before this run's, or completed at most 30 s before the
state's `at` (its lanes may not be published yet), has its own allocation
replayed, oldest first, against the schedule so far: its free count, this
same allocation for its lanes, and its home lanes placed where they land.
Concurrent routers thus resolve by age instead of all taking the same
slots, and the replay holds for any slot count.

**Allocation (OR-D3).** Deadline = (app home duration if the app lane gets
a free slot, else app vendor duration) + shell vendor duration. In cost
order: the app lane goes home when free slots > 0 and otherwise to the
vendor; each other lane's expected start is the earliest free slot in the
simulation, and it goes home when start + its home duration ≤ deadline
(the slot then stays busy to that finish plus `HANDOVER`), else to the
vendor.

Worked example, idle pool: app home (slot busy to 26 + 1.8), model-0 home
(slot busy to 25 + 1.8), deadline 26 + 13 = 39; model-1 would start at
26.8 and finish at 51.8 → vendor; rust starts at 26.8 and finishes at
37.8 → home. Two running entries 10 and 5 min in (busy to 17.8 and 21.8):
app → vendor (deadline 35), model-0 starts 17.8 and finishes 42.8 →
vendor, model-1 the same → vendor, rust starts 17.8 and finishes 28.8 →
home.

**What it costs and saves (measured, 30-day history).** Expected Namespace
spend under this rule about 3 900 minutes a month at 214 completed runs
(14.6 per run: about 27 % of app lanes find the pool busy); plain overflow
about 4 500; all-Namespace about 10 350; today's queueing nothing, at an
average run of about 128 minutes. A single push on an idle pool takes
about 40 minutes under the rule against about 35 all-Namespace: the home
pool is slower per lane and is chosen for cost. Warm caches shorten setup
by about 4.5 minutes but the test steps dominate, so shard 1 keeps going
to the vendor (about 6 minutes a run); the revision-2 claim that warm
caches made it free was wrong.

**Fork pull requests (OR-D5, workflow side).** When the event is
`pull_request` and the head repository is empty or differs from
`github.repository`, every lane goes to the vendor before anything else
is read, in every mode including `home-only`.

**Modes.** The router compares the mode trimmed and case-insensitively;
GitHub's `==` in the workflow ignores case but not spaces, so the runbook
says to set the value without spaces:

| value | behaviour |
|---|---|
| `home` | the rule above |
| `home-only` | every lane home, queueing as today; the state is ignored; a fork still goes to the vendor |
| `namespace` | every lane to the vendor; the routing job is skipped |
| unset or anything else | as `namespace` |

**Caches and provenance follow the chosen pool.** A lane sent to the vendor
runs its Namespace cache step; a lane sent home runs none and gets the
lane's cache disk; the host's commit rule is untouched.

**Two operating systems.** Under `home`, a lane runs on Windows 11 Pro as
a standard user at home or on Windows Server as administrator on the
vendor, chosen by occupancy, and a commit can pass on one and fail on the
other. The trade is accepted: each lane's first step prints its runner,
the aggregate gate's failure line names the pools, and the nightly stress
job plus `home-only` runs after a golden refresh keep the home environment
exercised on schedule.

## 4. The published state

Repository variable `WINDOWS_HOME_STATE`, JSON, one line, for example:

```json
{"v":1,"at":"2026-10-11T15:04:05Z","serving":true,"slots":2,
 "running":[{"lane":"app","since":"2026-10-11T14:48:51Z"}],
 "queued":[{"lane":"model","since":"2026-10-11T14:40:00Z"}]}
```

- `at`: the tick's UTC time. `serving`: `slots ≥ 1` and the breaker not
  tripped (OR-D4). `slots`: the
  configured slot count minus slots held by VMs in teardown
  (`discarding`). `running`: every journal VM in phase `provisioned` or
  `handed` that holds a slot (a `PendingCommit` VM holds none), with its
  provisioning time. `queued`: the queued jobs the host would admit if it
  had slots, oldest first (admissible lane label, this repository or a
  repository not yet read, not at the retry cap, not in back-off), with
  their `created_at`. A job whose run the host has not yet read counts
  here, the conservative direction, but is never admitted until it has.
- Written by the orchestrator at the end of every tick in which the
  serialised value changed, and at least every 60 s otherwise, through
  `PATCH /repos/{owner}/{repo}/actions/variables/WINDOWS_HOME_STATE`
  (`POST /actions/variables` once when it does not exist). A failed write
  is logged and never stops the tick, and the next attempt waits until
  that failure is a minute old, even for a changed state: the router then
  sees a stale heartbeat and routes away, the conservative outcome.
  Budget: at most six writes a minute, in practice one or two; the PAT's
  5 000 an hour is untouched by reads, which stay conditional.
- Pure function `Get-PublishedState -Config -Journal -Queued -Now` returns
  the hashtable; `Publish-HomeState` compares its JSON with the last
  published one and the last publish time and calls the adapter.
- Read by the router from `${{ vars.WINDOWS_HOME_STATE }}`. The variable
  is public on a public repository; it states occupancy counts and ages
  that the public job timings already imply.
- The owner creates the variable once with
  `gh variable set WINDOWS_HOME_STATE --body '{"v":1,"at":"1970-01-01T00:00:00Z","serving":false,"slots":0,"running":[],"queued":[]}'`
  (or the host's first POST does). The token already holds Variables read
  and write; nothing is re-issued.

## 5. The router

**Location.** `ci/windows-runner/route/route.py`, tests in
`ci/windows-runner/route/tests/test_route.py`, run by
`windows-runner-tests.yml` with
`python -I -m unittest discover -v -s ci/windows-runner/route/tests -t ci/windows-runner/route`
in a step that runs even when the Pester step failed. The mac router lives
at the matching path.

**Inputs, from the environment:**

| variable | meaning |
|---|---|
| `WINDOWS_RUNNER_MODE` | the repository variable |
| `WINDOWS_HOME_STATE` | the published state (`vars`) |
| `ROUTE_LANES` | `rust,app,model-0,model-1` (a test asserts it equals the router's lane list) |
| `ROUTE_ATTEMPT` | `github.run_attempt`, echoed as `attempt` |
| `ROUTE_RUN_ID` | `github.run_id`, for the start-time read and the summary |
| `ROUTE_EVENT`, `ROUTE_HEAD_REPO`, `ROUTE_REPO` | the fork rule |
| `ROUTE_SLOTS_MAX` | upper clamp on `slots`, default 8 (a published value above it is treated as not serving) |
| `ROUTE_STALE_SECONDS` | 180 |
| `ROUTE_NOW`, `ROUTE_RUN_STARTED` | epoch seconds, for tests and replay (`ROUTE_NOW` is the clock every age is measured against) |
| `ROUTE_CONCURRENT` | a JSON list of `[run id, routing job completion epoch or null]` pairs for the earlier routers, the newest ten, for tests and replay (replaces the listing read) |
| `GH_TOKEN` | `${{ github.token }}`, for the two optional reads |
| `GITHUB_OUTPUT`, `GITHUB_STEP_SUMMARY` | the usual |

**Decision.** A pure function

```
decide(mode, lanes, state_text, now, concurrent, *,
       stale_seconds, slots_max, event, head_repo, repo, durations)
  -> {lane: Decision(pool, reason)}, Summary
```

with no I/O; `durations` defaults to the production table and tests pass
a frozen copy. Every rule in §3 is a unit test: the state's five death
conditions each at the boundary, on both sides; a re-run attempt with an
old creation time and a dead state; a future `at`; `home-only` ignoring a
dead state; occupancy-based free slots; the overrun floor; the handover
on the app lane's slot; the worked examples; the deadline boundary; the
concurrent-router replay on two and three slots and on both sides of the
30 s completion margin; the newest-ten cap and the id tie-break; a running
entry with an unknown lane; the fork rule in `home` and `home-only`; the
mode table with case and spaces; an empty lane list; a lane missing from
the table (rejected with a reason, outputs withheld, so the lane takes the
vendor fallback); a replay line that reproduces a decision. `main()` wraps
it in the two optional reads (`gh api`, 15 s each, one retry, `stderr` in
the error, both pinned through an injected `run=`), made only when neither
the fork rule nor the state's death test has already settled the answer
(the run's start time is read only to order the listing, so `decide`
never sees it); then
the outputs, written first, and the summary, whose failure is a warning;
any exception makes every lane a vendor lane with the exception's text in
the reason and a `::warning::` annotation (escaped for the annotation
syntax), never a failed step. Test seams are injected (`get=`, `clock=`,
`run=`), never monkeypatched.

**Outputs** (`$GITHUB_OUTPUT`, echoed when unset): `attempt`, then
`<lane>-pool` = `home` or `vendor` for every requested lane. The router
emits no labels: both labels live in the workflow file, so a vendor rename
is one edit pinned by one guard.

**Summary.** Mode; the state as read (age, serving, slots, running,
queued); free slots and the deadline; a row per lane (pool, reason); and
one copy-paste replay line (`WINDOWS_RUNNER_MODE=… WINDOWS_HOME_STATE='…'
ROUTE_NOW=… ROUTE_RUN_STARTED=… ROUTE_CONCURRENT='…' ROUTE_LANES=…
python3 ci/windows-runner/route/route.py`) that reproduces the decision.

**Constants**: `LANES` (one table: lane → host lane, cost rank), the
durations table, `HANDOVER_SECONDS`, `OVERRUN_FLOOR_SECONDS`,
`DEFAULT_STALE_SECONDS`, `DEFAULT_SLOTS_MAX`; an invariant test checks the
table is consistent and equals the workflow's `ROUTE_LANES`.

## 6. Workflow changes (`windows.yml`)

- **`route`**: first job, `name: Route Windows lanes`,
  `if: ${{ vars.WINDOWS_RUNNER_MODE == 'home' || vars.WINDOWS_RUNNER_MODE == 'home-only' }}`,
  `runs-on: ubuntu-latest`, `timeout-minutes: 5`, job-level
  `continue-on-error: true` (a timeout or a lost runner then cannot fail
  the run), `permissions: { actions: read, contents: read }`, a sparse checkout of
  `ci/windows-runner/route` with `persist-credentials: false` and
  `continue-on-error: true`, then the routing step with
  `continue-on-error: true` and `env` carrying `GH_TOKEN`,
  `WINDOWS_RUNNER_MODE`, `WINDOWS_HOME_STATE`, `ROUTE_LANES`,
  `ROUTE_ATTEMPT`, `ROUTE_RUN_ID`, `ROUTE_EVENT`, `ROUTE_HEAD_REPO`,
  `ROUTE_REPO`; job outputs `attempt`, `rust-pool`, `app-pool`,
  `model-0-pool`, `model-1-pool`.
- **Every routed lane**: `needs: [route]`, `if: ${{ !cancelled() }}` (a
  skipped or failed routing job must not skip it), and one predicate used
  twice, for example rust:
  `runs-on: ${{ (needs.route.outputs.attempt == github.run_attempt && needs.route.outputs.rust-pool == 'home') && 'slate-win-rust' || format('namespace-profile-winx64-fast{0};overrides.cache-tag=slate-windows-rust', github.ref != 'refs/heads/main' && '-pr' || '') }}`
  and the Namespace cache step
  `if: ${{ !(needs.route.outputs.attempt == github.run_attempt && needs.route.outputs.rust-pool == 'home') }}`.
  The model matrix selects per shard with explicit booleans:
  `((matrix.shard == 0 && needs.route.outputs.model-0-pool == 'home') || (matrix.shard == 1 && needs.route.outputs.model-1-pool == 'home'))`.
  Each routed lane's first step prints `runner.name`, the effective pool.
- **The shell gate**: `runs-on: ${{ (vars.WINDOWS_RUNNER_MODE == 'home-only' && (github.event_name != 'pull_request' || github.event.pull_request.head.repo.full_name == github.repository)) && 'slate-win-shell' || 'windows-latest' }}`
  (a fork never goes home, even under `home-only`) and, new,
  `if: ${{ !cancelled() && needs.windows.result == 'success' }}`, so a
  routing-job failure cannot skip it.
- **`gate`**: `needs` gains `route`; it keeps `if: ${{ always() }}`,
  pinned by a guard, because a gate skipped with its `route` need would
  count as a passing required check; its failure line prints
  `route=<result> router-attempt='<attempt>'` and the pools. GitHub
  reports a `continue-on-error` job's result as it sees fit (it can read
  `success` for a routing job that failed), so nothing depends on it; an
  empty `router-attempt=''` in that line is the sign the router produced
  nothing, and the lanes then took the vendor.
- **`nightly.yml`**: `home-only` routes home like `home`
  (`vars.WINDOWS_RUNNER_MODE == 'home' || vars.WINDOWS_RUNNER_MODE == 'home-only'`),
  in both its `runs-on` and its cache condition.
- **Path filters**: `ci/windows-runner/route/**` in `windows.yml`'s.
- **The header comment** describes the four modes, the published state,
  the critical-path rule, the fork rule, "Re-run all jobs", and where to
  read a run's routing.
- **Guards** (`Workflows.Tests.ps1`): job blocks are sliced by a helper
  that finds the next job key, so key order and comments do not break
  them; the pins are the predicates and verbatim fallback strings, the
  routing job's `if`, permissions, `continue-on-error` on the job and both
  steps, the routing step's `id` and the job's timeout, the env names,
  the first step of every routed lane printing `runner.name`, job-level
  `!cancelled()` anchored at job indentation, the shell gate's `if`, the
  model selector, the gate's `needs` and `always()`, the routing job's
  name equal to the router's `ROUTE_JOB_NAME`, `nightly.yml`'s two
  `home-only` reads, one cross-workflow pin
  that every `vars.WINDOWS_RUNNER_MODE` comparison in the three workflows
  is with `home` or `home-only`, the path filters, and that
  `windows-runner-tests.yml` runs the router's tests with
  `if: ${{ !cancelled() }}`.

Latency: the routing job's hosted pickup (5–10 s measured) plus two
optional API reads; nothing in `namespace` mode.

## 7. Host changes

- **Publishing (OR-D2).** `Get-PublishedState` (pure, tested) and
  `Publish-HomeState` (throttled; a failed write is logged and retried a
  minute later at the soonest) in
  the module; `Set-GhVariable -Name -Value` in `adapters/GitHub.ps1`
  (PATCH, POST on 404) exposed as the `PublishState` adapter; called at
  the end of `Invoke-OrchestratorTick`, except on a tick whose admission
  threw: then nothing is published and the last state ages into stale,
  so a host that cannot list or admit routes away by itself. Jobs whose
  run is still unknown count in the published queue (the conservative
  direction) without being admitted, and `running` is ordered by instant,
  never by string. The journal remembers `PublishedState` (the last
  JSON), `PublishedAt` and `PublishFailedAt`.
- **Admission (OR-D5 a).** `Invoke-Admission` reads the run of each
  candidate job once (`GetRun`; the answer is remembered on the `SeenJobs`
  entry as `Foreign`, present from creation as `$null` = unknown, with the
  head repository as `Repo`, which a handoff eviction names; a read
  that fails is retried at most once a minute and logged on the first
  failure only) and never admits a foreign job:
  `admission: skipped job <id> of <repo> (not the trusted repository)`. A
  null head repository is foreign.
- **Handoff (OR-D5 b).** In the reconciliation, in-progress jobs are
  matched to the host's runners from the full job listing, not the
  lane-filtered one; when a handed VM's runner is seen on a job the host
  did not provision for, the job's run is read (the answer remembered on
  `SeenJobs` first, a failed read logged once a minute); if it is
  foreign, or the job carries no lane label, the VM is forced off at once
  with the log line
  `handoff: <vm> runs job <id> of <repo> (<why>); forcing it off`
  and the discard reason
  `runner taken by job <id> of <repo> (<why>)`, where `<why>` is
  `not the trusted repository` for a foreign job and `no lane label` for
  an unlabelled job of the trusted repository; no retry strike, and the
  trusted job it was provisioned for stays queued for the next VM. A VM
  whose runner was seen busy but ran no job the host knows of is logged
  as such and gets no retry strike either.
- **Log retention (OR-D7).** `Get-ExpiredLogNames -Names -Today
  -RetentionDays` (pure; local dates, as the writer names files;
  unparseable names kept), run at the first tick of each local day and
  after every start (`LogsPrunedOn` in the journal, cleared by the startup
  sweep); `LogRetentionDays` defaults to 30 when absent; a failed delete
  is logged and the day still counts.
- **Golden acceptance (OR-D7).** At every green completion
  (`conclusion == success`, whatever the cache outcome) on a VM provisioned
  after the current golden's seal time, the orchestrator records the lane
  under the golden's stamp: `GoldenAccepted = @{ Golden = <stamp>; Lanes =
  @(...) }`, reset when the stamp differs (compared as instants through
  `ConvertTo-DateTimeOffset`, never as strings: the journal's JSON round
  trip turns them into dates). It records facts only and logs once when
  all four lanes (rust, app, model, shell) are present:
  `golden: rust, app, model and shell are green on the current image`.
  Wrapped so a failure never wedges a settle.
- **One predicate.** `Test-GoldenPrevRemovable -Accepted -CurrentStamp
  -CurrentSealed -VmCreationTimes -Lanes` is true only when the accepted
  stamp equals the current golden's stamp, the current golden is sealed,
  every lane in the list is present, and no VM directory predates the
  stamp. The module owns it; the prune calls it.
- **The prune task.** `install/prune-golden.ps1 -Root`, registered by
  `setup-host.ps1` on every run (outside the password-bound task pair) as
  `slate-ci-golden-prune`, SYSTEM, daily at 04:00 local, 10-minute limit:
  reads the journal without side effects (a read-only parse, not
  `Read-Journal`), derives every path from `Get-CiHostConfig`, evaluates
  the predicate, and only then clears the read-only bit and deletes
  `win11-runner.prev.vhdx`; one log line either way; exit 0, or exit 1 on
  a failure so the task history shows it.
- **Rotation.** `build-golden.ps1 -Rotate`, after the VM-exists check,
  refuses while any `slate-win-*` VM exists; only when the current image is
  sealed (`IsReadOnly`) does it set an older prev aside under a temporary
  name, rename the current image to prev, and only then delete the older
  one (a failed rename puts it back); an unsealed leftover is
  refused with a pointer to "The golden build fails". One shared helper
  (`Remove-SealedFile`) clears the read-only bit and deletes, used by the
  rotation and the prune and tested on `TestDrive`.
- **Journal schema.** One `$script:JournalDefaults` feeds `New-Journal`
  and `Read-Journal`; a shape test pins the top-level keys (`Vms`,
  `Retries`, `SeenJobs`, `LogsPrunedOn`, `GoldenAccepted`,
  `PublishedState`, `PublishedAt`, `PublishFailedAt`) and the `SeenJobs`
  entry keys; the runbook's "State" bullet lists them.

## 8. Failure modes and observability

- **The router never fails a run.** The routing job is skipped in
  `namespace` mode, the job and its two steps are `continue-on-error`
  (so a timeout or a lost runner cannot fail the run either), every exception
  becomes vendor pools with a warning annotation, and a lane whose outputs
  are empty, stale or missing takes the vendor expression in the workflow
  file and still runs. The shell gate's own condition keeps it running
  after a routing-job failure.
- **Dead host** (OR-D4): stale within three minutes of a crash, including a
  crash mid-job; recovers as soon as the loop publishes again. No run
  strands to discover it. The only strand left is a lane already queued
  home when the host dies: the runbook says flip to `namespace`, cancel
  the run, then "Re-run all jobs", and notes that the `main` concurrency
  group holds later `main` runs while a stranded lane waits.
- **Concurrent routers**: ordered by age through the optional listing
  read; when that read fails both routers see the same state and the
  loser's app lane queues one lane's length, the revision-2 behaviour.
- **A failed publish**, or a tick whose admission threw: the router
  routes away after three minutes; the log says why once a minute; the
  loop keeps serving queued home jobs.
- **Failing host** (OR-D4): the third discard of the host's making in a
  row trips the breaker; the next state says not serving and the log
  says why once; the jobs already queued home keep their retries. The
  state says serving again after the cool-down (one lane's worth comes
  home to try; a failure trips it at once) or after a loop restart.
- **Pool size**: published by the host, so there is nothing to keep in
  step; the router only clamps absurd values.
- **Partial re-runs** always take the vendor (OR-D6); the runbook states
  the cost and the UI and CLI names of the full re-run.
- **Reading a run's routing**: the routing job's summary and its replay
  line; the host log's new lines (`admission: skipped job …`,
  `handoff: <vm> runs job … forcing it off`, `publish: …`, `golden: …`,
  `logs: …`); a runbook
  section beside the log-line list.

## 9. Security

- **What the public repository reveals is unchanged** but for the state
  variable, which states occupancy counts and ages the public job timings
  already imply; `config.json` never carried the token, the product key or
  the router's public address.
- **What the router reads** is the `vars` context and, optionally, this
  repository's runs listing and one run, with the run's own token under
  `actions: read` and `contents: read`. A fork PR's token is read-only.
- **What the router decides** is which of two already-trusted targets a
  lane runs on; a fork can rewrite the router, which is why the fork rules
  live on the host and the approval policy stays the gate (OR-D5).
- **The host's PAT** keeps the permissions it was issued with, the mac
  controller's set: Actions read, Administration read and write, Variables
  read and write. Nothing is re-issued and it never appears in the
  repository; the runbook's issuance step, which listed only the first
  two, is corrected to the set actually granted.
- **The prune task** runs as SYSTEM but acts on one fixed path under a
  predicate evaluated against the current image; the orchestrator's
  account gains no right on `golden`.
- **`pull_request_target`** is not used by any Windows workflow and the
  fork rule does not cover it.

## 10. Branch cleanup (its own pull request)

`.github/workflows/branch-cleanup.yml` (`schedule` Mondays 06:00 UTC;
`workflow_dispatch` with boolean `delete`, default false, read as
`inputs.delete && 'true' || 'false'`; `permissions: { contents: write,
pull-requests: read }`) checks out only `ci/branch-cleanup` and runs
`ci/branch-cleanup/branch-cleanup.sh`, which reads `REPO`, `DELETE`,
`GITHUB_STEP_SUMMARY` and `BRANCH_MIN_AGE_DAYS` (default 7, a whole
number of days from 0 to 9999; anything else fails the run) and reaches
GitHub only through `gh`; besides `gh` it needs only bash, awk, `date`,
`mktemp` and `rm` (gh's own `--jq` does the JSON, and names are
URL-encoded byte by byte in bash). The script lists every
`refs/heads/claude/*` branch (`per_page=100`, paginated) and, before
looking at any branch, takes one snapshot of the open pull requests (`gh
pr list --limit 1000`, printed by `--jq` as an `open<TAB>N` line and
then one `head<TAB>base` line each); a snapshot that fails, is empty or
malformed, is shorter than its own count or may be cut at the limit
fails the run, since every delete decision depends on it. For each
branch, in its own error scope with its name URL-encoded, it reads the
tip's sha and committer date, compares that sha with `main` (`ahead_by`,
`per_page=1`) and counts the branch as head and as base in the snapshot;
every value read is validated, and an empty or malformed one is a failed
lookup, never a zero. The compare names the recorded sha, not the
branch, so the commit `main` was checked against is the one a delete
removes. The age floor reads the tip's committer date, so a branch just
cut from a `main` tip older than the floor is not protected by it;
GitHub's repository-activity endpoint, which records branch creation and
pushes, could refine that later. It writes the table (with the tip's
short sha) to its summary and, with `delete`, deletes only a branch
whose tip `main` contains, whose tip is at least `BRANCH_MIN_AGE_DAYS`
old, and that is neither the base nor the head of any open pull request
(deleting a head would close its pull request), asserting the `claude/`
prefix; just before the delete it re-reads the ref and keeps the branch,
as a failure, if its sha moved since classification. The table names why
each branch was kept; table cells and annotations are escaped. A failed
listing or snapshot fails the run with the red-run note under the table,
so an empty report is never silent; a failed lookup, re-check or delete
for one branch warns, the loop continues, and the run fails at the end.
Unmerged branches are never deleted by it (the repository already
deletes merged heads; what remains is unmerged work). Its behaviour test
is `ci/windows-runner/tests/BranchCleanup.Tests.ps1`, which runs the
script under bash (Git for Windows' bash on Windows) with a fake `gh`
that answers canned text from files and records every call; a describe
in `Workflows.Tests.ps1` pins what the workflow still owns, and
`windows-runner-tests.yml`'s path filters include `branch-cleanup.yml`
and `ci/branch-cleanup/**` so both run when either changes.

## 11. Testing

- `route.py`: the unit tests of §5 (frozen durations; injected seams).
- Host: Pester for `Get-PublishedState`, the publish throttle and its
  failure log, the admission repository check (with a default trusted run
  in the fakes so only foreign-run tests define a run), the handoff kill,
  `Get-ExpiredLogNames`, the once-a-day gate across a restart, golden
  acceptance including a `Write-Journal`/`Read-Journal` round trip, the
  predicate (every clause), the prune script against a `TestDrive` tree
  including the "signal from the previous image" vector, `-Rotate` on a
  sealed and on an unsealed image, the journal shape, the adapter key
  lists.
- Guards: `Workflows.Tests.ps1` as in §6.
- Integration, on the host (runbook): confirm the token lists Variables
  read and write on GitHub's fine-grained tokens page and create the
  variable; after `setup-host.ps1` and the loop restart, read
  `gh variable get WINDOWS_HOME_STATE` and see it move within a minute of a
  job; with one run in flight dispatch a second only after the first's
  lanes show and read both summaries; stop the loop, wait three minutes,
  dispatch a run and read that it routed away with "state stale"; restart
  the loop; after a golden refresh run `home-only`, read the acceptance
  line, run the prune task by hand and read its line.

## 12. Documents touched

- `docs/runbooks/self-hosted-windows-runner.md`: the routing section
  (modes, the published state, the rule, the fork rule, the measured
  costs), "Reading a run's routing", the pool switch (flip first, then
  cancel, then "Re-run jobs → Re-run all jobs" or `gh run rerun <id>`),
  the token section (the issued set, Variables read and write included,
  corrected from the two it listed), the "Capacity" bullet and
  the security row, "both tasks" → the three tasks and that the prune task
  re-registers on every run, the refresh steps (`-Rotate`, `home-only`,
  the acceptance line, the prune task), the cleanup section, the log-line
  list, the state keys, the branch-cleanup subsection.
- `docs/superpowers/specs/2026-10-10-self-hosted-windows-runner-design.md`:
  D-3 points here; §5 notes the router; the threat model's fork residual
  risk points at OR-D5.
- This spec gains a dated "Planning deviations" list whenever the plan
  departs from it.

## 13. Out of scope

`nightly.yml` and the pilot keep their rules and can adopt the router
later (its deadline formula is `windows.yml`'s). Whether the model
coverage inventories differ by operating system is unverified. The
heartbeat's three-minute window after a crash is accepted.

## 14. Planning deviations (2026-10-11)

- The gate's `needs` gains `route` (§6) so its failure line can name the
  pools.
- The router's three tasks of revision 2 are one task: its two files were
  run as a whole before the plan was committed.
- `Get-CiHostPaths` is the one statement of the host tree; `Get-CiHostConfig`
  derives its path keys from it, and `GoldenPrevPath` and `JournalPath` are
  new derived keys.
- The acceptance record carries a `Complete` flag so the "all four lanes"
  line is logged once; the prune's predicate ignores it and checks the
  lanes itself.
- Staleness is measured against the router's own clock with a 180 s window;
  the run's creation time is read for nothing (GitHub resolves `vars` at job
  start, verified on a11y-check run 38060200124 attempt 4; `created_at`
  never moves across re-runs). §13's three-minute window is this one.
- Earlier routers are replayed through the same allocation instead of being
  assumed to place two lanes; a router that completed at most 30 s before
  the published state still counts.
- A running entry whose time cannot be read counts from now; an unknown
  lane occupies the longest home duration.
- The fork rule precedes `home-only`, in the router and in the shell gate's
  `runs-on`.
- The route job is `continue-on-error` at job level; the gate keeps
  `if: ${{ always() }}`, pinned, because a gate skipped with its `route`
  need would count as a passing required check.
- A tick whose admission threw publishes nothing; jobs whose run is unknown
  count as queued; `PublishedAt` and `PublishFailedAt` that do not parse are
  treated as never.
- Handoff reconciliation matches runners against the full in-progress
  listing; a job without a lane label is evicted with `(no lane label)`; a
  VM whose runner was seen busy gets no retry strike. JIT runners carry
  only the label in their configuration (verified on the live listing), so
  this is defence in depth.
- Log files are named with the invariant culture, as the retention parser
  reads them.
- The host's token already held Variables read and write (the mac
  controller's set); the runbook's issuance step was corrected, nothing
  re-issued.
- The runbook writes "the orchestrator and store-token tasks" where §12
  says "the three tasks": the prune task holds no password and sits outside
  that pair. The runbook has no "cleanup section" or "branch-cleanup
  subsection" of the size §12 implies: one short "Host cleanup" subsection
  under "Daily operation" names the three mechanisms (log retention, the
  prune task, the weekly branch report) and points at where each is
  described; the branch report itself is §10 and lands on its own pull
  request.
