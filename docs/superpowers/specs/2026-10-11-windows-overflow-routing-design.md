# Windows overflow routing and host cleanup — design

Date: 2026-10-11. Extends
`docs/superpowers/specs/2026-10-10-self-hosted-windows-runner-design.md`
(the home pool, its orchestrator and its manual fallback, D-3 there).

## 1. Goal

A Windows lane never waits for a home slot. When a slot is free, the lane
that would cost the most on the vendor takes it; otherwise the lane runs on
Namespace (or windows-latest for the shell gate) at once, and later lanes
come back to the home pool as slots free up. The owner's words: concurrency
over a queue, first in first out, with the expensive lanes preferred for
home.

Two things ride along because the owner asked for them in the same breath:
the host cleans up after itself without a hand (logs, the previous golden
image), and the public repository keeps revealing nothing it did not already
reveal.

## 2. Decisions

- **D-1 Overflow, not queueing.** `WINDOWS_RUNNER_MODE=home` now means "home
  first, vendor when home is full". `namespace`, unset or any other value
  still sends every lane to the vendor, exactly as before, so the manual
  switch keeps its meaning.
- **D-2 The decision is made per job, when it becomes schedulable,** by a
  routing job on hosted Linux that reads the Actions jobs queue — the same
  shape as the mac runner's `ci/mac-runner/route/route.py`. GitHub fixes a
  job's runner at queue time and offers no fallback, so this is the only
  place a dynamic choice can be made. Nothing on the host changes for it.
- **D-3 Cost ranking.** When slots are short, the lanes being routed are
  ranked by what they cost on the vendor: app build + test, then each model
  shard, then rust tests, then the shell gate. The top lanes take the free
  slots, shard 0 before shard 1 on ties.
- **D-4 A dead host routes away by itself.** A `slate-win-*` job queued for
  more than five minutes while none is in progress means the pool is down,
  and the router sends everything to the vendor until the host takes its
  queue again. This is the `auto` mode the first spec parked.
- **D-5 Fork pull requests never route home.** The mac workflows keep fork
  PRs off the Studio with a same-repo guard; the Windows router applies the
  same rule by routing a fork PR's lanes to the vendor whatever the slots
  say. The first spec allowed an approved fork PR on a throwaway VM; this
  spec withdraws that, because nothing is lost by it and one attack surface
  (a fork's workflow file choosing the home label) goes away.
- **D-6 Cleanup is the host's job.** Log retention and the previous golden
  image are handled by the orchestrator; stale `claude/*` branches by a
  weekly workflow. What is already automatic stays as it is.

## 3. The routing rule

**Decision points.** Two per `windows.yml` run: `route` at the start, for
rust tests, app build + test, model shard 0 and model shard 1; and
`route-shell` after the app build, for the shell gate, which cannot queue
before its binaries exist. The two Ubuntu gates (model coverage, the
aggregate) are not Windows lanes and are untouched.

**Occupancy.** Every job carrying a label that starts with `slate-win-`
and whose status is `queued` or `in_progress`, in any run of the three
routed workflows (`windows.yml`, `nightly.yml`, `windows-ci-pilot.yml`),
excluding the run being routed. Jobs still waiting on `needs` are not in
GitHub's listing (verified live on 2026-10-10), so a run's own unrouted
lanes never count against it.

**Free slots** = pool size − occupancy, floored at zero. The pool size is
2 today and is an input of the router (`ROUTE_SLOTS`, default 2); the
host's `config.json` stays the source of truth and the runbook says the two
move together.

**Allocation.** Rank the lanes being routed by vendor cost:

| lane | vendor target | vendor cost, measured 2026-10-10/11 |
|---|---|---|
| app build + test | `namespace-profile-winx64-fast[-pr];overrides.cache-tag=slate-windows-app` | about 19 min |
| app model shard (each) | `namespace-profile-winx64-fast[-pr];overrides.cache-tag=slate-windows-model` | about 6 min |
| rust tests | `namespace-profile-winx64-fast[-pr];overrides.cache-tag=slate-windows-rust` | about 2 min |
| shell accessibility gate | `windows-latest` | hosted, free on a public repo |

The first `free` lanes in that order get their home label (`slate-win-app`,
`slate-win-model`, `slate-win-rust`, `slate-win-shell`); every other lane
gets its vendor target at once. `-pr` is appended to the Namespace profile
exactly as today: on every ref but `refs/heads/main`.

**First in, first out across runs** follows from counting queued home jobs
as occupancy: a lane an earlier run sent home holds its slot in the count
until it runs, so a later run cannot jump ahead of it, and since nothing is
ever sent to a full pool, the home queue never grows beyond what the race
in §6 allows.

**Dead host (D-4).** If the oldest queued `slate-win-*` job has been queued
for more than `ROUTE_DEAD_SECONDS` (300) and no `slate-win-*` job is in
progress, free slots are zero. Normal handoff is one to two minutes
(12–14 s to hand off, up to a minute for the runner to come online, up to a
minute for GitHub to report), so five minutes leaves margin.

**Fork pull requests (D-5).** When the event is `pull_request` and the head
repository is not `github.repository`, every lane gets its vendor target
and the summary says why. The router receives both names as inputs.

**Modes.** `WINDOWS_RUNNER_MODE` is read once per routing job:

| value | behaviour |
|---|---|
| `home` | the rule above |
| `namespace` | every lane to its vendor target; no API call is made |
| unset or anything else | as `namespace`, and the summary names the value |

**Caches and provenance follow the chosen pool.** A lane sent to the vendor
runs its Namespace cache step; a lane sent home runs none and gets the
lane's cache disk, and the host's commit rule (trusted repo, `main`, push /
schedule / dispatch, success, generation unchanged) is untouched.

## 4. The router

**Location.** `ci/windows-runner/route/route.py`, tests in
`ci/windows-runner/route/tests/test_route.py`, run by
`windows-runner-tests.yml` with
`python -I -m unittest discover -v -s ci/windows-runner/route/tests -t ci/windows-runner/route`
next to the Pester suite. The mac router lives at the matching path and the
two workflows call theirs the same way, so a reader of one knows the other.
(Section 2 of the chat design said a composite action under
`.github/actions/`; the repository's precedent is a script and a sparse
checkout, and the spec follows the precedent.)

**Inputs, all from the environment** (the mac pattern), with test seams:

| variable | meaning |
|---|---|
| `WINDOWS_RUNNER_MODE` | the repository variable |
| `ROUTE_LANES` | comma list of the lanes to route: `rust,app,model-0,model-1` or `shell` |
| `ROUTE_REF` | `github.ref`, for the `-pr` suffix |
| `ROUTE_RUN_ID` | `github.run_id`, excluded from occupancy |
| `ROUTE_EVENT`, `ROUTE_HEAD_REPO`, `ROUTE_REPO` | the fork rule's inputs |
| `ROUTE_SLOTS` | pool size, default 2 |
| `ROUTE_DEAD_SECONDS` | default 300 |
| `ROUTE_NOW` | epoch seconds, for tests |
| `ROUTE_JOBS_JSON` | a path to a jobs listing to use instead of the API, for tests and for reading a decision back by hand |
| `GITHUB_TOKEN`, `GITHUB_REPOSITORY`, `GITHUB_OUTPUT`, `GITHUB_STEP_SUMMARY` | the usual |

**Reads.** With `gh api` (present on hosted Linux) and the run's own
`GITHUB_TOKEN` under the job permission `actions: read`: for each routed
workflow, `GET /repos/{repo}/actions/workflows/{file}/runs?status=queued`
and `?status=in_progress`, then `GET /repos/{repo}/actions/runs/{id}/jobs`
for each run returned (paginated, `per_page=100`). The listing is reduced
to `(job id, run id, status, labels, created_at, started_at)` before any
rule runs. These are the host's own calls on a different budget: the run's
token, not the owner's PAT.

**Decision.** A pure function

```
decide(mode, lanes, jobs, now, *, slots, dead_seconds, ref, event, head_repo, repo)
  -> {lane: Decision(pool, label, reason)}, Occupancy(in_progress, queued, oldest_queued_age)
```

with no I/O, so every rule in §3 is a unit test with a hand-written jobs
list: free slots; the ranking and the shard tie-break; a lane list of one
(`shell`); the five-minute rule at 299, 300 and 301 seconds with and
without a job in progress; the fork rule; mode `namespace`; an unknown
mode; an empty listing; a listing that includes this run's own jobs; labels
that merely contain `slate-win-` without starting with it; a malformed
timestamp. `main()` wraps `decide` in the reads and the writes, and any
exception on the way makes every lane a vendor lane with the exception's
type in the reason, never a failed job.

**Outputs**, appended to `$GITHUB_OUTPUT` and echoed when it is unset:

| output | value |
|---|---|
| `<lane>` | the full `runs-on` string for the lane, e.g. `slate-win-app` or `namespace-profile-winx64-fast-pr;overrides.cache-tag=slate-windows-app` |
| `<lane>-pool` | `home` or `vendor` |

Lane names in outputs are `rust`, `app`, `model-0`, `model-1`, `shell`.

**Summary.** One table in `$GITHUB_STEP_SUMMARY`: mode, occupancy (in
progress, queued, oldest queued age), free slots, then a row per lane with
pool, label and reason. The same text goes to the log.

**Constants**, declared at the top of the module with the measurements
that justify them: `HOME_LABELS`, `VENDOR_TARGETS`, `COST_ORDER`,
`DEFAULT_SLOTS = 2`, `DEFAULT_DEAD_SECONDS = 300`, `ROUTED_WORKFLOWS`
(the same three paths as the host's `config.json`).

## 5. Workflow changes (`windows.yml`)

- **`route`**: first job, `name: Route Windows lanes`, `runs-on:
  ubuntu-latest`, `timeout-minutes: 5`, `permissions: { actions: read }`,
  sparse checkout of `ci/windows-runner/route` with
  `persist-credentials: false`, one step running the router with
  `ROUTE_LANES=rust,app,model-0,model-1`; job outputs `rust`, `app`,
  `model-0`, `model-1` and their `-pool` twins.
- **`route-shell`**: `needs: [windows]`, otherwise identical, with
  `ROUTE_LANES=shell`; outputs `shell` and `shell-pool`.
- **`rust-tests`**: `needs: [route]`; `runs-on: ${{ needs.route.outputs.rust }}`;
  the Namespace cache step `if: ${{ needs.route.outputs.rust-pool == 'vendor' }}`.
- **`windows`** (app build + test): the same with `app`.
- **`windows-model-shard`**: `needs` gains `route`;
  `runs-on: ${{ matrix.shard == 0 && needs.route.outputs.model-0 || needs.route.outputs.model-1 }}`;
  the cache step's condition uses the matching `-pool` output. The two
  shards may land in different pools.
- **`flaui`** (shell gate): `needs: [windows, route-shell]`;
  `runs-on: ${{ needs.route-shell.outputs.shell }}`.
- **Unchanged**: job and check names, the aggregate gate's `needs`, the
  path filters, the concurrency group, every `timeout-minutes`, the model
  coverage gate, `nightly.yml`, the pilot, and the host's `RoutedWorkflows`.
- **The header comment** (`WINDOWS_RUNNER_MODE routes every Windows lane…`)
  describes the overflow rule, the fork rule and where to read a run's
  routing.
- **`ci/windows-runner/tests/Workflows.Tests.ps1`** is rewritten for the
  new shape: the two routing jobs exist with `actions: read` and the sparse
  checkout; every Windows lane's `runs-on` reads the matching router
  output; the three cache gates read the matching `-pool` output and
  nothing reads `vars.WINDOWS_RUNNER_MODE` in `windows.yml` any more except
  the routing jobs' environment; no bare vendor label remains; `nightly.yml`
  and the pilot keep their guards verbatim; `windows-runner-tests.yml` runs
  the router's unit tests.
- **`windows-runner-tests.yml`** gains the Python step and
  `ci/windows-runner/route/**` is already inside its path filter.

Latency added per run: the `route` job before the lanes (API reads of a
few seconds; hosted pickup typically 5–20 s) and `route-shell` before the
shell gate.

## 6. Failure modes and observability

- **The router never fails a run.** API error, rate limit, malformed
  listing, missing input: every lane to its vendor target, reason in the
  summary. The job fails only if no hosted runner starts it, the outage
  every hosted gate already shares.
- **Dead host** (D-4) recovers by itself: once the host takes its queued
  work, the next router sees a job in progress and resumes sending lanes
  home. `namespace` remains the planned-outage switch and makes no API
  calls.
- **Double booking.** Two runs routing within the same seconds can both
  see the last slot free; the loser waits one lane's length, which is
  today's behaviour, and both summaries show the occupancy each saw.
- **The settle window.** A VM that has finished its job holds its slot
  until GitHub reports the completion and the host settles it (up to a
  minute); the router already counts that slot free, so the next home lane
  may wait that minute.
- **Pool size drift.** `ROUTE_SLOTS` and `config.json`'s `Slots` cannot be
  cross-checked at run time (GitHub cannot read the host). The runbook's
  capacity step lists both.
- **Namespace outages** are untouched: a lane sent there queues there.
- **Reading a run's routing**: the step summary table of each routing job;
  the host log unchanged; a runbook section beside the log-line list.

## 7. Security

- **What the public repository reveals is unchanged.** `config.json`
  carries an RFC 1918 NAT prefix, slot addresses on it, lane caps,
  timeouts, the trusted repo, branch and events, and the routed workflow
  paths; it never carried the token, the product key or the router's public
  address, and the new module adds only the ranking, the pool size and the
  label templates the workflow file already publishes.
- **What the router sees** is the jobs listing of a public repository,
  public already, read with the run's `GITHUB_TOKEN` under `actions: read`
  and nothing else. A fork PR's token is read-only.
- **What the router decides** is which of two already-trusted targets a
  lane runs on. Labels still carry no trust: the host still commits caches
  only for trusted events on `main`, every VM is still thrown away, and the
  fork-PR approval policy still gates outside contributors. D-5 adds that a
  fork PR never reaches the home pool at all.
- **What a malicious workflow edit could do** is unchanged: a PR could
  already edit `runs-on`; that is what the approval policy is for.
- **The host's PAT** keeps its scope (Actions read, Administration read
  and write); the router never uses it. No new secret anywhere.
- **Dead-host detection** is read-only and can only steer work away from
  the home machine.

## 8. Cleanup automation

- **Host logs.** The orchestrator deletes `logs\orchestrator-*.log` files
  whose date in the name is older than `LogRetentionDays` (new
  `config.json` key, 30) at startup and at the first tick after midnight
  (a `LogsPrunedOn` date in the journal). Selection is a pure function
  (`Get-ExpiredLogNames -Names -Today -RetentionDays`) with Pester tests;
  deletion goes through a new adapter entry. Today's and the dated files of
  the last 30 days are never touched.
- **The previous golden image.** `build-golden.ps1` keeps writing
  `golden\win11-runner.prev.vhdx`. At settle, when a VM's job concluded
  `success` and the VM was created after the current golden's
  `LastWriteTime`, the orchestrator deletes the prev if it exists and no VM
  directory holds a disk whose parent is the prev (the Hyper-V adapter
  answers that; a prev any chain still uses is left alone and the log says
  so). One log line: `golden: removed win11-runner.prev.vhdx after the
  first green job on the current image`. Rollback before that point stays
  the manual step the runbook already describes.
- **Stale branches.** A weekly workflow `.github/workflows/branch-cleanup.yml`
  (`schedule`, Mondays 06:00 UTC, and `workflow_dispatch`) lists
  `refs/heads/claude/*` with no open pull request and no commit in 14
  days, deletes them with the run's `GITHUB_TOKEN` under `contents: write`,
  and writes the list to its summary. Protected branches are excluded by
  GitHub itself. The four branches present on 2026-10-11
  (`claude/1285-deferred-summary-wip`, `claude/home-pilot-smoke`,
  `claude/laughing-meitner-86n5do`, `claude/runbook-realtek-ndis`) are the
  owner's one-off; the runbook change lists them.
- **Already automatic, left alone**: runner registrations (removed after
  every job, swept at startup), VM directories and cache forks (removed
  with the VM), journal retries (pruned after 24 h), artifact retention
  (7 or 30 days per upload). Nothing is left on the Namespace side by an
  overflow job.

## 9. Testing

- `route.py`: the unit tests of §4, run in `windows-runner-tests.yml`.
- Host changes: Pester tests for `Get-ExpiredLogNames`, the once-a-day
  gate, the prev-golden rule (deleted after the first green job on a newer
  image; left alone while a chain uses it; left alone when absent), and the
  new adapter entries faked like the existing ones.
- Guards: `Workflows.Tests.ps1` as rewritten in §5.
- Integration, on the host, in the runbook's integration pass: with the
  pool busy (one run in flight), dispatch a second run and read both
  summaries: its lanes went to the vendor while the first run's lanes stay
  home, and the shell gate of the first run, routed after the app build,
  came home if a slot was free. Then with the loop stopped, dispatch a run
  and read that it routed everything away within the five-minute rule.

## 10. Documents touched

- `docs/runbooks/self-hosted-windows-runner.md`: the routing section
  (rule, modes, fork rule), "Reading a run's routing", the capacity step
  (both places), the cleanup section (what is automatic now, the one-off
  branch list), and the log-line list (the golden line).
- `docs/superpowers/specs/2026-10-10-self-hosted-windows-runner-design.md`:
  D-3's "manual" and the parked `auto` follow-up point here.
- The `windows.yml` header comment.

## 11. Out of scope

`nightly.yml` and the pilot keep their simple rules and can adopt the
router later; a heartbeat from the host (the mac pattern) is unnecessary
while the queue itself carries the dead-host signal; mitigating the
double-booking race (a lease or a tie-break on run id) is not worth a host
change for a wait that happens rarely and costs one lane's length.
