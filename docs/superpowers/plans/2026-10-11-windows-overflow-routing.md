# Windows Overflow Routing and Host Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A `windows.yml` lane runs on the home pool when a slot is free or when queueing costs the run no time, and on Namespace otherwise, decided per lane by a routing job that reads the state the host publishes; the host publishes that state every tick, refuses and evicts jobs of other repositories, prunes its logs and records which lanes went green on which golden image; an elevated task deletes the previous image on one predicate; the branch sweep lands as its own pull request.

**Architecture:** `ci/windows-runner/route/route.py` (pure `decide` over the published state, two optional `gh` reads, pools and a replay line out) is the first job of `windows.yml` when the mode is `home` or `home-only`; every routed lane keeps both labels in the workflow file and uses the router's pool only in the router's own attempt. The host module gains state publishing, a repository check at admission and at handoff, log retention, golden acceptance and the deletion predicate; `setup-host.ps1` registers an elevated prune task; `build-golden.ps1 -Rotate` rotates a sealed image.

**Tech Stack:** Python 3 (unittest; `gh api` via subprocess with injected seams), GitHub Actions YAML, PowerShell 7 (Pester 5.9.1), the existing `SlateCiHost.psm1` adapter pattern.

**Spec:** `docs/superpowers/specs/2026-10-11-windows-overflow-routing-design.md`, revision 3 (binding; its §3–§7 are implemented here). It extends `docs/superpowers/specs/2026-10-10-self-hosted-windows-runner-design.md`.

## Global Constraints

- The router runs only on `ubuntu-latest`, only when the mode is `home` or `home-only`, and never fails a run: both of its steps are `continue-on-error: true`, every exception becomes vendor pools plus a `::warning::`, every routed lane has `if: ${{ !cancelled() }}` and a `runs-on` whose fallback is today's vendor expression, verbatim (spec §6, §8).
- The router emits pools (`<lane>-pool` = `home` | `vendor`) and `attempt`; it never emits a label. Both labels live in `windows.yml` (spec OR-D6, §5).
- The host publishes `WINDOWS_HOME_STATE` (spec §4) with the PAT's Variables permission, which the token already holds (it was issued with the mac controller's permission set); a failed publish never stops a tick.
- The host never provisions for, and evicts within a tick, a job of another repository (spec OR-D5). A null head repository is foreign.
- Durations and timing constants live in `route.py` alone (app 26/22, model 25/6, rust 11/2, shell 13; handover 110 s; overrun floor 300 s; stale 120 s); tests inject a frozen copy (spec §3, §5).
- The orchestrator's account never gains a right on `golden`; only `slate-ci-golden-prune` (SYSTEM) deletes the previous image, and only on `Test-GoldenPrevRemovable` against the current, sealed image (spec OR-D7, §7).
- Timestamps that pass through the journal are compared as instants through `ConvertTo-DateTimeOffset`, never as strings (the JSON round trip turns them into dates).
- The branch sweep is its own pull request (Task 9): never merged with the rest.
- File headers: every new `.py` and `.ps1` file starts with `# Copyright (C) 2026 Cory Joseph` and `# SPDX-License-Identifier: AGPL-3.0-or-later` (`.py` after the shebang). Line endings LF.
- Pester: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1`; Python: `python -I -m unittest discover -v -s ci/windows-runner/route/tests -t ci/windows-runner/route`. Every task ends with both green, and every task's new tests are listed in it.
- Commit messages follow `type(scope): sentence` and end with the session's attribution line.

## Review Focus

- A published state whose `running` entry has no readable `since`, or whose lane is one the router does not know: the simulation must stay finite and conservative — Task 1 pins both.
- A run attempt greater than 1 whose router output is reused: the lane takes the vendor expression and the cache step runs — Task 2's guard pins the predicate shape.
- A routing job that is skipped (mode `namespace`) or fails on its checkout: every routed lane and the shell gate still run — Task 2 pins `if: ${{ !cancelled() }}` on the lanes and the shell gate's own condition.
- The journal written and read back before a green completion: acceptance must keep its lanes — Task 6 pins a round trip.
- A `-Rotate` on an unsealed leftover of a failed build: it must refuse — Task 7 pins it.

## Rulings recorded while planning

- The gate's `needs` gains `route` so its failure line can name the pools (spec §6 says so; the first spec said "unchanged"). Cost if wrong: one edge in the graph.
- Revision 2's three router tasks are one task here: the router's two files were written and run as a whole (50 tests) before this plan was committed, and an "append in order" plan was where the previous revision's test defects came from.
- Revision 2's branch cleanup stays as Task 9 but on its own branch and pull request (`claude/branch-cleanup` from `main`), committed last.

---

### Task 1: The router

**Files:**
- Create: `ci/windows-runner/route/route.py`
- Create: `ci/windows-runner/route/tests/__init__.py` (empty)
- Create: `ci/windows-runner/route/tests/test_route.py`
- Modify: `.github/workflows/windows-runner-tests.yml` (the Python step)

**Interfaces:**
- Produces, for Task 2: outputs `attempt`, `rust-pool`, `app-pool`, `model-0-pool`, `model-1-pool`; the env names `WINDOWS_RUNNER_MODE`, `WINDOWS_HOME_STATE`, `ROUTE_LANES`, `ROUTE_ATTEMPT`, `ROUTE_RUN_ID`, `ROUTE_EVENT`, `ROUTE_HEAD_REPO`, `ROUTE_REPO`, `GH_TOKEN`; the job name `Route Windows lanes` (`ROUTE_JOB_NAME`, which `fetch_concurrent` looks for).
- Consumes, from Task 3: the state shape of spec §4 (`parse_state`).

The two files below were written and run as a whole before this plan was committed (50 tests), and the plan was then executed once in a scratch worktree, suite by suite; the corrections that run produced are already folded in.

- [ ] **Step 1: Write the tests**

Create `ci/windows-runner/route/tests/__init__.py` empty, and `ci/windows-runner/route/tests/test_route.py`. One method of `class Constants`, `test_workflow_routes_exactly_the_lane_table`, is NOT written here: it reads `windows.yml`, which gains `ROUTE_LANES` only in Task 2, and Task 2 adds the method. Everything else verbatim:

```python
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
"""Tests for the Windows lane router (ci/windows-runner/route/route.py).

Run:  python -I -m unittest discover -v -s ci/windows-runner/route/tests -t ci/windows-runner/route

Every rule test injects TEST_DURATIONS, a frozen copy of the production
table at the time of writing, so revising the production numbers never
touches these expectations; one test pins the production table itself.
"""

import calendar
import io
import json
import os
import re
import sys
import tempfile
import time
import unittest
from contextlib import redirect_stdout

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
import route  # noqa: E402

NOW = calendar.timegm((2026, 10, 11, 12, 0, 0, 0, 0, 0))  # 2026-10-11T12:00:00Z
REPO = "coryj627/slate"
LANES = ["rust", "app", "model-0", "model-1"]
TEST_DURATIONS = {
    "app": {"home": 26, "vendor": 22},
    "model": {"home": 25, "vendor": 6},
    "rust": {"home": 11, "vendor": 2},
    "shell": {"home": 16, "vendor": 13},
}
H = route.HANDOVER_SECONDS  # 110
M = 60


def iso(epoch):
    return "%04d-%02d-%02dT%02d:%02d:%02dZ" % time.gmtime(epoch)[:6]


def state(at=NOW - 5, serving=True, slots=2, running=(), queued=(), v=1):
    """A published state as the host writes it; running/queued are (lane, since) pairs."""
    return json.dumps({"v": v, "at": iso(at), "serving": serving, "slots": slots,
                       "running": [{"lane": lane, "since": iso(since) if isinstance(since, int) else since} for lane, since in running],
                       "queued": [{"lane": lane, "since": iso(since) if isinstance(since, int) else since} for lane, since in queued]})


def decide(mode="home", lanes=LANES, text=None, now=NOW, created=NOW, concurrent=(), **overrides):
    kwargs = dict(stale_seconds=120, slots_max=8, event="push", head_repo="", repo=REPO, durations=TEST_DURATIONS)
    kwargs.update(overrides)
    return route.decide(mode, lanes, state() if text is None else text, now, created, list(concurrent), **kwargs)


def pools(decisions):
    return {lane: decisions[lane]["pool"] for lane in decisions}


class ParseTime(unittest.TestCase):
    def test_iso_forms(self):
        self.assertEqual(route.parse_time("2026-10-11T12:00:00Z"), NOW)
        self.assertEqual(route.parse_time("2026-10-11T12:00:00+00:00"), NOW)
        self.assertEqual(route.parse_time("2026-10-11T12:00:00.1234567Z"), NOW)

    def test_absent_or_garbage(self):
        for value in (None, "", "yesterday", 42):
            self.assertIsNone(route.parse_time(value))


class ParseState(unittest.TestCase):
    def test_well_formed(self):
        parsed, error = route.parse_state(state(running=[("app", NOW - 600)], queued=[("model", NOW - 60)]))
        self.assertEqual(error, "")
        self.assertEqual(parsed["slots"], 2)
        self.assertEqual(parsed["running"], [{"lane": "app", "since": NOW - 600}])
        self.assertEqual(parsed["queued"], [{"lane": "model", "since": NOW - 60}])

    def test_rejections_name_the_defect(self):
        cases = [("", "no published state"), ("{", "does not parse"), ("[]", "not an object"),
                 (state(v=2), "version"), (json.dumps({"v": 1, "at": "soon", "serving": True, "slots": 2, "running": [], "queued": []}), "'at'"),
                 (json.dumps({"v": 1, "at": iso(NOW), "serving": "yes", "slots": 2, "running": [], "queued": []}), "'serving'"),
                 (json.dumps({"v": 1, "at": iso(NOW), "serving": True, "slots": True, "running": [], "queued": []}), "'slots'"),
                 (json.dumps({"v": 1, "at": iso(NOW), "serving": True, "slots": 2, "running": {}, "queued": []}), "'running'"),
                 (json.dumps({"v": 1, "at": iso(NOW), "serving": True, "slots": 2, "running": [], "queued": [{"since": iso(NOW)}]}), "without a lane")]
        for text, fragment in cases:
            parsed, error = route.parse_state(text)
            self.assertIsNone(parsed, text)
            self.assertIn(fragment, error, text)

    def test_unreadable_since_is_kept_as_none(self):
        parsed, _ = route.parse_state(state(running=[("app", "soon")]))
        self.assertEqual(parsed["running"], [{"lane": "app", "since": None}])


class Death(unittest.TestCase):
    def death(self, text, created=NOW):
        parsed, error = route.parse_state(text)
        return route.state_death(parsed, error, created, 120, 8)

    def test_fresh_serving_state_lives(self):
        self.assertIsNone(self.death(state(at=NOW - 119)))
        self.assertIsNone(self.death(state(at=NOW - 120)))

    def test_stale_at_the_boundary(self):
        self.assertIn("stale", self.death(state(at=NOW - 121)))

    def test_staleness_is_measured_at_the_runs_creation(self):
        # A run that waited for a hosted runner is judged on what vars held when it was created.
        self.assertIsNone(self.death(state(at=NOW - 600), created=NOW - 500))

    def test_not_serving_no_slot_too_many_slots(self):
        self.assertIn("not serving", self.death(state(serving=False)))
        self.assertIn("no slot", self.death(state(slots=0)))
        self.assertIn("above", self.death(state(slots=9)))

    def test_unparseable_state_is_dead_with_the_parse_error(self):
        self.assertIn("no published state", self.death(""))


class Simulate(unittest.TestCase):
    def simulate(self, text, concurrent=()):
        parsed, _ = route.parse_state(text)
        return route.simulate(parsed, NOW, list(concurrent), TEST_DURATIONS)

    def test_idle_pool(self):
        self.assertEqual(self.simulate(state()), [0, 0])

    def test_running_entry_frees_at_duration_plus_handover(self):
        self.assertEqual(self.simulate(state(running=[("app", NOW - 10 * M)])), [0, 16 * M + H])

    def test_overrun_entry_frees_after_the_floor_plus_handover(self):
        self.assertEqual(self.simulate(state(running=[("rust", NOW - 60 * M)])), [0, route.OVERRUN_FLOOR_SECONDS + H])

    def test_running_entry_without_a_time_counts_from_now(self):
        self.assertEqual(self.simulate(state(running=[("rust", "soon")])), [0, 11 * M + H])

    def test_queued_entries_take_slots_oldest_first(self):
        text = state(running=[("app", NOW - 16 * M), ("app", NOW - 11 * M)], queued=[("rust", NOW - 50), ("model", NOW - 40)])
        # Slots free at 10+H and 15+H; rust (oldest) takes the first, model the second.
        self.assertEqual(self.simulate(text), [10 * M + H + 11 * M + H, 15 * M + H + 25 * M + H])

    def test_more_running_than_slots_keeps_the_latest_ending(self):
        text = state(running=[("app", NOW - 1 * M), ("app", NOW - 2 * M), ("app", NOW - 3 * M)])
        self.assertEqual(self.simulate(text), [24 * M + H, 25 * M + H])

    def test_earlier_routers_place_an_app_and_a_model_each(self):
        self.assertEqual(self.simulate(state(), concurrent=[41]), [25 * M + H, 26 * M + H])

    def test_unknown_lane_in_the_state_costs_the_floor(self):
        self.assertEqual(self.simulate(state(queued=[("mystery", NOW)])), [0, route.OVERRUN_FLOOR_SECONDS + H])


class Allocate(unittest.TestCase):
    def test_idle_pool_worked_example(self):
        # Spec §3: app and model-0 take the slots; model-1 would finish at 51.8 against a 39 deadline; rust fits.
        decisions, deadline = route.allocate(LANES, 2, [0, 0], TEST_DURATIONS)
        self.assertEqual(pools(decisions), {"app": "home", "model-0": "home", "model-1": "vendor", "rust": "home"})
        self.assertEqual(deadline, (26 + 13) * M)
        self.assertIn("finishes at 37 min", decisions["rust"]["reason"])
        self.assertIn("past the deadline of 39 min", decisions["model-1"]["reason"])

    def test_both_slots_busy_worked_example(self):
        # Two running 10 and 5 min in: slots free at 17.8 and 21.8; app vendor (deadline 35);
        # model-0 and model-1 would finish at 42.8: vendor; rust 17.8 to 28.8: home.
        free_at = [16 * M + H, 20 * M + H]
        decisions, deadline = route.allocate(LANES, 0, free_at, TEST_DURATIONS)
        self.assertEqual(pools(decisions), {"app": "vendor", "model-0": "vendor", "model-1": "vendor", "rust": "home"})
        self.assertEqual(deadline, (22 + 13) * M)

    def test_app_never_queues_even_when_the_simulation_says_a_slot_is_free_now(self):
        # Occupancy says both slots are taken (free 0) although the estimate reads 0: the app goes to the vendor.
        decisions, _ = route.allocate(["app"], 0, [0, 0], TEST_DURATIONS)
        self.assertEqual(decisions["app"]["pool"], "vendor")

    def test_deadline_boundary_is_inclusive(self):
        at_limit = (26 + 13 - 25) * M
        decisions, _ = route.allocate(["app", "model-0"], 2, [0, at_limit], TEST_DURATIONS)
        self.assertEqual(decisions["model-0"]["pool"], "home")
        decisions, _ = route.allocate(["app", "model-0"], 2, [0, at_limit + 1], TEST_DURATIONS)
        self.assertEqual(decisions["model-0"]["pool"], "vendor")

    def test_cost_order_whatever_the_input_order(self):
        decisions, _ = route.allocate(["rust", "model-1", "model-0", "app"], 2, [0, 0], TEST_DURATIONS)
        self.assertEqual(list(decisions), ["rust", "model-1", "model-0", "app"])
        self.assertEqual(pools(decisions), {"app": "home", "model-0": "home", "model-1": "vendor", "rust": "home"})

    def test_without_the_app_lane_the_deadline_is_the_vendor_critical_path(self):
        decisions, deadline = route.allocate(["rust"], 2, [0, 0], TEST_DURATIONS)
        self.assertEqual(deadline, (22 + 13) * M)
        self.assertEqual(decisions["rust"]["pool"], "home")

    def test_unknown_lanes_get_no_decision(self):
        decisions, _ = route.allocate(["rust", "shell"], 2, [0, 0], TEST_DURATIONS)
        self.assertEqual(list(decisions), ["rust"])


class Decide(unittest.TestCase):
    def test_home_idle_pool(self):
        decisions, summary = decide()
        self.assertEqual(pools(decisions), {"app": "home", "model-0": "home", "model-1": "vendor", "rust": "home"})
        self.assertEqual(summary["free"], 2)
        self.assertFalse(summary["dead"])
        self.assertEqual(summary["deadline"], (26 + 13) * M)
        self.assertEqual(summary["state_age"], 5)

    def test_home_with_a_full_pool(self):
        decisions, summary = decide(text=state(running=[("app", NOW - 10 * M), ("model", NOW - 5 * M)]))
        self.assertEqual(pools(decisions), {"app": "vendor", "model-0": "vendor", "model-1": "vendor", "rust": "home"})
        self.assertEqual(summary["free"], 0)

    def test_home_queued_entries_count_against_free_slots(self):
        decisions, summary = decide(text=state(running=[("app", NOW - 60)], queued=[("rust", NOW - 30)]))
        self.assertEqual(summary["free"], 0)
        self.assertEqual(decisions["app"]["pool"], "vendor")

    def test_home_dead_states_route_everything_away(self):
        for text in (state(at=NOW - 121), state(serving=False), state(slots=0), "", "{"):
            decisions, summary = decide(text=text)
            self.assertEqual(set(pools(decisions).values()), {"vendor"}, text)
            self.assertTrue(summary["dead"], text)

    def test_home_only_ignores_the_state(self):
        decisions, summary = decide(mode="home-only", text="")
        self.assertEqual(set(pools(decisions).values()), {"home"})
        self.assertFalse(summary["dead"])

    def test_other_modes_route_to_the_vendor_without_reading_the_state(self):
        for mode in ("namespace", "", None, "beta", " NAMESPACE "):
            decisions, summary = decide(mode=mode, text="{")
            self.assertEqual(set(pools(decisions).values()), {"vendor"}, mode)
            self.assertIsNone(summary["slots"], mode)

    def test_mode_is_trimmed_and_case_insensitive(self):
        self.assertEqual(decide(mode=" Home ")[0]["app"]["pool"], "home")
        self.assertEqual(decide(mode="HOME-ONLY")[0]["model-1"]["pool"], "home")

    def test_fork_and_unknown_head_pull_requests_route_to_the_vendor_before_the_state_is_read(self):
        for head in ("someone/slate", ""):
            decisions, summary = decide(text="{", event="pull_request", head_repo=head)
            self.assertEqual(set(pools(decisions).values()), {"vendor"}, head)
            self.assertIn("pull request", summary["reason"])

    def test_same_repository_pull_request_is_routed(self):
        decisions, _ = decide(event="pull_request", head_repo=REPO)
        self.assertEqual(decisions["app"]["pool"], "home")

    def test_earlier_routers_take_the_free_slots(self):
        decisions, summary = decide(concurrent=[41])
        self.assertEqual(summary["free"], 0)
        self.assertEqual(decisions["app"]["pool"], "vendor")
        # Their app (26) and model (25) occupy the slots; rust would start at 25+H and end past the vendor deadline of 35.
        self.assertEqual(decisions["rust"]["pool"], "vendor")

    def test_unknown_lanes_are_withheld_and_named(self):
        decisions, summary = decide(lanes=["rust", "shell"])
        self.assertEqual(list(decisions), ["rust"])
        self.assertEqual(summary["unknown_lanes"], ["shell"])

    def test_durations_default_to_the_production_table(self):
        decisions, _ = route.decide("home", LANES, state(), NOW, NOW, [], stale_seconds=120, slots_max=8, event="push",
                                    head_repo="", repo=REPO)
        self.assertEqual(decisions["app"]["pool"], "home")


class Constants(unittest.TestCase):
    def test_production_durations(self):
        self.assertEqual(route.DURATIONS, TEST_DURATIONS)
        self.assertEqual(route.HANDOVER_SECONDS, 110)
        self.assertEqual(route.OVERRUN_FLOOR_SECONDS, 300)
        self.assertEqual(route.DEFAULT_STALE_SECONDS, 120)

    def test_lane_table_is_consistent(self):
        self.assertEqual(sorted(route.LANES), sorted(LANES))
        self.assertEqual(sorted(rank for _, rank in route.LANES.values()), [0, 1, 2, 3])
        for host_lane, _ in route.LANES.values():
            self.assertIn(host_lane, route.DURATIONS)
        self.assertIn("shell", route.DURATIONS)



class Reads(unittest.TestCase):
    def test_fetch_run_created(self):
        self.assertEqual(route.fetch_run_created(REPO, "7", lambda path: {"created_at": iso(NOW)}), NOW)
        with self.assertRaises(ValueError):
            route.fetch_run_created(REPO, "7", lambda path: {})

    def test_fetch_concurrent_keeps_earlier_runs_with_a_live_routing_job(self):
        calls = []

        def get(path):
            calls.append(path)
            if "/runs?status=queued" in path:
                return {"workflow_runs": [{"id": 5, "created_at": iso(NOW - 30)}]}
            if "/runs?status=in_progress" in path:
                return {"workflow_runs": [{"id": 5, "created_at": iso(NOW - 30)}, {"id": 6, "created_at": iso(NOW - 10)},
                                          {"id": 8, "created_at": iso(NOW + 10)}, {"id": 9, "created_at": iso(NOW)}]}
            if "/runs/5/jobs" in path:
                return {"jobs": [{"name": route.ROUTE_JOB_NAME, "status": "in_progress"}]}
            if "/runs/6/jobs" in path:
                return {"jobs": [{"name": route.ROUTE_JOB_NAME, "status": "completed"}, {"name": "rust tests", "status": "queued"}]}
            raise AssertionError(path)

        self.assertEqual(route.fetch_concurrent(REPO, "9", NOW, get), [5])
        self.assertEqual(sum(1 for path in calls if "/runs/5/jobs" in path), 1)
        self.assertFalse(any("/runs/8/jobs" in path or "/runs/9/jobs" in path for path in calls))


class Main(unittest.TestCase):
    def env(self, mode="home", text=None, **extra):
        env = {"WINDOWS_RUNNER_MODE": mode, "WINDOWS_HOME_STATE": state() if text is None else text,
               "ROUTE_LANES": "rust,app,model-0,model-1", "ROUTE_ATTEMPT": "1", "ROUTE_RUN_ID": "42", "ROUTE_EVENT": "push",
               "ROUTE_HEAD_REPO": "", "ROUTE_REPO": REPO, "ROUTE_NOW": str(NOW), "ROUTE_RUN_CREATED": str(NOW),
               "ROUTE_CONCURRENT": "[]"}
        env.update(extra)
        return env

    def run_main(self, env, get=None):
        fd, out = tempfile.mkstemp()
        os.close(fd)
        fd, summary = tempfile.mkstemp()
        os.close(fd)
        env["GITHUB_OUTPUT"] = out
        env["GITHUB_STEP_SUMMARY"] = summary
        buffer = io.StringIO()
        try:
            with redirect_stdout(buffer):
                code = route.main(env, get=get or (lambda path: (_ for _ in ()).throw(AssertionError("no read expected: " + path))),
                                  clock=lambda: NOW)
            with open(out, encoding="utf-8") as handle:
                outputs = dict(line.rstrip("\n").split("=", 1) for line in handle if "=" in line)
            with open(summary, encoding="utf-8") as handle:
                text = handle.read()
        finally:
            os.remove(out)
            os.remove(summary)
        return code, outputs, text, buffer.getvalue()

    def test_home_idle_pool_writes_pools_and_the_attempt(self):
        code, outputs, text, _ = self.run_main(self.env())
        self.assertEqual(code, 0)
        self.assertEqual(outputs, {"attempt": "1", "rust-pool": "home", "app-pool": "home", "model-0-pool": "home", "model-1-pool": "vendor"})
        self.assertIn("| app | home |", text)
        self.assertIn("run 42", text)
        self.assertIn("Replay: `WINDOWS_RUNNER_MODE=\"home\"", text)

    def test_replay_line_reproduces_the_decision(self):
        _, outputs, text, _ = self.run_main(self.env(text=state(running=[("app", NOW - 600)])))
        line = re.search(r"Replay: `(.*) python3", text).group(1)
        env = dict(part.split("=", 1) for part in re.findall(r"[A-Z_]+=(?:\"(?:[^\"\\]|\\.)*\"|\S+)", line))
        env = {key: json.loads(value) if value.startswith('"') else value for key, value in env.items()}
        env.update({"ROUTE_ATTEMPT": "1", "ROUTE_RUN_ID": "42"})
        _, replayed, _, _ = self.run_main(env)
        self.assertEqual(replayed, outputs)

    def test_namespace_mode_makes_no_read(self):
        code, outputs, _, _ = self.run_main(self.env(mode="namespace"))
        self.assertEqual(code, 0)
        self.assertEqual(outputs["app-pool"], "vendor")

    def test_optional_reads_are_used_and_their_failure_is_tolerated(self):
        seen = []

        def get(path):
            seen.append(path)
            if path.endswith("/actions/runs/42"):
                return {"created_at": iso(NOW - 300)}
            if "/runs?status=" in path:
                return {"workflow_runs": []}
            raise AssertionError(path)

        env = self.env(text=state(at=NOW - 350))
        env.pop("ROUTE_RUN_CREATED")
        env.pop("ROUTE_CONCURRENT")
        _, outputs, text, _ = self.run_main(env, get=get)
        # Measured at creation (NOW-300) the state is 50 s old: alive.
        self.assertEqual(outputs["app-pool"], "home")
        self.assertIn("| 50 s |", text)
        env = self.env(text=state(at=NOW - 350))
        env.pop("ROUTE_RUN_CREATED")
        env.pop("ROUTE_CONCURRENT")
        _, outputs, _, printed = self.run_main(env, get=lambda path: (_ for _ in ()).throw(RuntimeError("boom")))
        # Without the read, now stands in and the state is stale: vendor, with a notice, no warning.
        self.assertEqual(outputs["app-pool"], "vendor")
        self.assertIn("::notice::", printed)
        self.assertNotIn("::warning", printed)

    def test_an_unexpected_failure_falls_back_to_the_vendor_with_a_warning(self):
        env = self.env()
        env["ROUTE_CONCURRENT"] = "not json"
        code, outputs, text, printed = self.run_main(env)
        self.assertEqual(code, 0)
        self.assertEqual({outputs[l + "-pool"] for l in LANES}, {"vendor"})
        self.assertIn("::warning title=Windows lane router::", printed)
        self.assertIn("JSONDecodeError", text)

    def test_malformed_numbers_fall_back_to_defaults(self):
        code, outputs, _, _ = self.run_main(self.env(ROUTE_STALE_SECONDS="soon", ROUTE_NOW="later"))
        self.assertEqual(code, 0)
        self.assertEqual(outputs["app-pool"], "home")
        self.assertEqual(route._int_env({"X": "abc"}, "X", 7), 7)
        self.assertEqual(route._int_env({"X": " 42 "}, "X", 7), 42)

    def test_empty_lane_list_writes_only_the_attempt(self):
        _, outputs, _, _ = self.run_main(self.env(ROUTE_LANES=""))
        self.assertEqual(outputs, {"attempt": "1"})

    def test_no_output_file_prints_the_outputs(self):
        env = self.env(mode="namespace")
        buffer = io.StringIO()
        with redirect_stdout(buffer):
            self.assertEqual(route.main(env, get=lambda path: {}, clock=lambda: NOW), 0)
        self.assertIn("app-pool=vendor", buffer.getvalue())


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run them to verify they fail**

Run: `python -I -m unittest discover -v -s ci/windows-runner/route/tests -t ci/windows-runner/route`
Expected: `ModuleNotFoundError: No module named 'route'`.

- [ ] **Step 3: Write the router**

Create `ci/windows-runner/route/route.py`:

```python
#!/usr/bin/env python3
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
"""Pick a pool for each windows.yml lane: the home pool or the vendor.

Spec: docs/superpowers/specs/2026-10-11-windows-overflow-routing-design.md
(§3 the rule, §4 the state the host publishes, §5 this router). Runs as
the first job of windows.yml on hosted Linux, only when WINDOWS_RUNNER_MODE
is home or home-only, and writes `attempt` plus one `<lane>-pool` per lane
to $GITHUB_OUTPUT (or stdout when unset). It emits pools, never labels:
both labels live in the workflow file.

Inputs, from the environment:
  WINDOWS_RUNNER_MODE   home | home-only (anything else: every lane vendor)
  WINDOWS_HOME_STATE    the state the host publishes (vars context, JSON)
  ROUTE_LANES           comma list: rust,app,model-0,model-1
  ROUTE_ATTEMPT         github.run_attempt, echoed as `attempt`
  ROUTE_RUN_ID          github.run_id (the creation-time read, the summary)
  ROUTE_EVENT, ROUTE_HEAD_REPO, ROUTE_REPO   the fork rule
  ROUTE_STALE_SECONDS   120: a state older than this at run creation is dead
  ROUTE_SLOTS_MAX       8: a published slot count above it is not serving
  ROUTE_NOW, ROUTE_RUN_CREATED   epoch seconds (tests, replay)
  ROUTE_CONCURRENT      JSON list of earlier routers' run ids (tests, replay)
  GH_TOKEN              for the two optional reads
  GITHUB_OUTPUT, GITHUB_STEP_SUMMARY

This is routing, not security: a fork runs its own copy of this file, so
the host's admission and handoff checks and the approval policy carry the
trust (spec §9).
"""

import calendar
import json
import os
import re
import subprocess
import sys
import time

# One table: lane name in ROUTE_LANES -> (the host's lane label suffix, cost
# rank). The free slots go to the lowest rank first (spec OR-D3).
LANES = {
    "app": ("app", 0),
    "model-0": ("model", 1),
    "model-1": ("model", 2),
    "rust": ("rust", 3),
}
# Minutes. Home: measured on the pool on 2026-10-11 (runs 38125616320,
# 38129938362, 38138011672, 38140861459), beside a neighbour where the
# lane usually has one; vendor: the 30-day Namespace history, windows-latest
# for the shell gate. Revise from the artifacts; tests inject their own copy.
DURATIONS = {
    "app": {"home": 26, "vendor": 22},
    "model": {"home": 25, "vendor": 6},
    "rust": {"home": 11, "vendor": 2},
    "shell": {"home": 16, "vendor": 13},
}
HANDOVER_SECONDS = 110        # settle, provision, hand off, runner online (94-131 s measured)
OVERRUN_FLOOR_SECONDS = 300   # a job past its duration is assumed to need five more minutes
DEFAULT_STALE_SECONDS = 120
DEFAULT_SLOTS_MAX = 8
STATE_VERSION = 1
ROUTE_JOB_NAME = "Route Windows lanes"
API_TIMEOUT_SECONDS = 15
CONCURRENT_RUNS_MAX = 10


def minutes(value):
    return int(value) * 60


def parse_time(value):
    """An ISO 8601 UTC timestamp as epoch seconds, or None when absent or malformed.

    Fractions are dropped first: PowerShell's 'o' format writes seven digits,
    more than strptime's %f takes.
    """
    if not isinstance(value, str):
        return None
    text = re.sub(r"\.\d+(?=Z$|\+00:00$)", "", value.strip())
    for fmt in ("%Y-%m-%dT%H:%M:%SZ", "%Y-%m-%dT%H:%M:%S+00:00"):
        try:
            return calendar.timegm(time.strptime(text, fmt))
        except ValueError:
            continue
    return None


def parse_state(text):
    """The published state as a dict, or (None, why) when it is unusable.

    Shape (spec §4): {"v": 1, "at": iso, "serving": bool, "slots": int,
    "running": [{"lane", "since"}], "queued": [{"lane", "since"}]}.
    """
    if not isinstance(text, str) or not text.strip():
        return None, "no published state"
    try:
        raw = json.loads(text)
    except ValueError as failure:
        return None, "state does not parse: %s" % failure
    if not isinstance(raw, dict):
        return None, "state is not an object"
    if raw.get("v") != STATE_VERSION:
        return None, "state version %r is not %d" % (raw.get("v"), STATE_VERSION)
    at = parse_time(raw.get("at"))
    if at is None:
        return None, "state has no readable 'at'"
    if not isinstance(raw.get("serving"), bool):
        return None, "state has no boolean 'serving'"
    slots = raw.get("slots")
    if isinstance(slots, bool) or not isinstance(slots, int):
        return None, "state has no integer 'slots'"
    entries = {}
    for key in ("running", "queued"):
        items = raw.get(key)
        if not isinstance(items, list):
            return None, "state has no list '%s'" % key
        reduced = []
        for item in items:
            if not isinstance(item, dict) or not isinstance(item.get("lane"), str):
                return None, "state '%s' entry without a lane" % key
            since = parse_time(item.get("since"))
            reduced.append({"lane": item["lane"], "since": since})
        entries[key] = reduced
    return {"at": at, "serving": raw["serving"], "slots": slots,
            "running": entries["running"], "queued": entries["queued"]}, ""


def state_death(state, error, run_created, stale_seconds, slots_max):
    """Why the pool cannot serve (spec OR-D4), or None when it can."""
    if state is None:
        return error
    age = run_created - state["at"]
    if age > stale_seconds:
        return "state is %d s old at the run's creation (stale after %d s)" % (age, stale_seconds)
    if not state["serving"]:
        return "the host reports it is not serving"
    if state["slots"] < 1:
        return "the host reports no slot"
    if state["slots"] > slots_max:
        return "the host reports %d slots, above the %d the router trusts" % (state["slots"], slots_max)
    return None


def home_duration(lane_key, durations):
    return minutes(durations[lane_key]["home"])


def simulate(state, now, concurrent, durations):
    """Seconds from now at which each slot frees, ascending.

    A running entry occupies its slot to max(since + duration, now + overrun
    floor) plus a handover; more running entries than slots keep the
    latest-ending ones. The host's queued entries take slots oldest first;
    then each router that started before this one is assumed to place an
    app lane and a model shard (the idle-pool outcome), so concurrent
    routers resolve by age.
    """
    slots = state["slots"]
    busy = []
    for entry in state["running"]:
        duration = home_duration(entry["lane"], durations) if entry["lane"] in durations else OVERRUN_FLOOR_SECONDS
        since = entry["since"] if entry["since"] is not None else now
        until = max(since + duration, now + OVERRUN_FLOOR_SECONDS) + HANDOVER_SECONDS
        busy.append(until - now)
    busy.sort()
    if len(busy) > slots:
        busy = busy[-slots:]
    while len(busy) < slots:
        busy.insert(0, 0)
    demand = [entry["lane"] for entry in sorted(state["queued"], key=lambda e: (e["since"] if e["since"] is not None else 0))]
    demand += ["app", "model"] * len(concurrent)
    for lane in demand:
        duration = home_duration(lane, durations) if lane in durations else OVERRUN_FLOOR_SECONDS
        busy.sort()
        busy[0] = max(busy[0], 0) + duration + HANDOVER_SECONDS
    return sorted(busy)


def allocate(lanes, free_slots, free_at, durations):
    """OR-D3: the app lane takes a free slot (counted, never estimated) or
    the vendor; every other lane, in cost order, queues at home only when the
    simulated schedule finishes it inside the deadline (app plus shell)."""
    slots = sorted(free_at)
    decisions = {}
    wanted = sorted((lane for lane in lanes if lane in LANES), key=lambda lane: LANES[lane][1])
    if "app" in wanted:
        if free_slots > 0 and slots:
            app_finish = home_duration("app", durations)
            slots[0] = app_finish + HANDOVER_SECONDS
            decisions["app"] = {"pool": "home", "reason": "free slot (%d free)" % free_slots}
        else:
            app_finish = minutes(durations["app"]["vendor"])
            decisions["app"] = {"pool": "vendor", "reason": "no free slot (%d free) and the app lane never queues" % free_slots}
    else:
        app_finish = minutes(durations["app"]["vendor"])
    deadline = app_finish + minutes(durations["shell"]["vendor"])
    for lane in wanted:
        if lane == "app":
            continue
        slots.sort()
        start = max(0, slots[0]) if slots else 0
        finish = start + home_duration(LANES[lane][0], durations)
        if finish <= deadline:
            if slots:
                slots[0] = finish + HANDOVER_SECONDS
            what = "free slot" if start == 0 else "queues at home"
            decisions[lane] = {"pool": "home", "reason": "%s: starts at %d min, finishes at %d min, deadline %d min" % (
                what, start // 60, finish // 60, deadline // 60)}
        else:
            decisions[lane] = {"pool": "vendor", "reason": "would start at %d min and finish at %d min, past the deadline of %d min" % (
                start // 60, finish // 60, deadline // 60)}
    return {lane: decisions[lane] for lane in lanes if lane in decisions}, deadline


def _everything(lanes, pool, reason):
    return {lane: {"pool": pool, "reason": reason} for lane in lanes if lane in LANES}


def _summary(mode):
    return {"mode": mode, "state_age": None, "serving": None, "slots": None, "running": None, "queued": None,
            "concurrent": None, "free": None, "deadline": None, "dead": False, "unknown_lanes": [], "reason": ""}


def decide(mode, lanes, state_text, now, run_created, concurrent, *, stale_seconds, slots_max, event, head_repo, repo,
           durations=None):
    """The whole rule (spec §3) as a pure function."""
    durations = DURATIONS if durations is None else durations
    normalized = (mode or "").strip().lower()
    summary = _summary(normalized or "(unset)")
    summary["unknown_lanes"] = [lane for lane in lanes if lane not in LANES]
    lanes = [lane for lane in lanes if lane in LANES]
    if normalized == "home-only":
        summary["reason"] = "WINDOWS_RUNNER_MODE=home-only: every lane queues at home"
        return _everything(lanes, "home", summary["reason"]), summary
    if normalized != "home":
        summary["reason"] = "WINDOWS_RUNNER_MODE=%s routes every lane to the vendor" % summary["mode"]
        return _everything(lanes, "vendor", summary["reason"]), summary
    if event == "pull_request" and (not head_repo or head_repo != repo):
        summary["reason"] = "pull request from %s: the vendor, whatever the pool says" % (head_repo or "an unknown repository")
        return _everything(lanes, "vendor", summary["reason"]), summary
    state, error = parse_state(state_text)
    death = state_death(state, error, run_created, stale_seconds, slots_max)
    if state is not None:
        summary.update({"state_age": run_created - state["at"], "serving": state["serving"], "slots": state["slots"],
                        "running": len(state["running"]), "queued": len(state["queued"])})
    if death:
        summary["dead"] = True
        summary["reason"] = "the pool cannot serve: %s" % death
        return _everything(lanes, "vendor", summary["reason"]), summary
    concurrent = list(concurrent or [])
    summary["concurrent"] = len(concurrent)
    free_slots = max(0, state["slots"] - len(state["running"]) - len(state["queued"]) - 2 * len(concurrent))
    summary["free"] = free_slots
    decisions, deadline = allocate(lanes, free_slots, simulate(state, now, concurrent, durations), durations)
    summary["deadline"] = deadline
    summary["reason"] = "home first; a lane queues only inside the deadline"
    return decisions, summary


def gh_api(path, timeout=API_TIMEOUT_SECONDS):
    """One GET through gh (GH_TOKEN from the step's env), one retry, stderr in the error."""
    last = None
    for attempt in (1, 2):
        try:
            completed = subprocess.run(["gh", "api", path], capture_output=True, text=True, timeout=timeout, check=False)
        except (OSError, subprocess.TimeoutExpired) as failure:
            last = RuntimeError("gh api %s: %s" % (path, failure))
            continue
        if completed.returncode == 0:
            return json.loads(completed.stdout)
        last = RuntimeError("gh api %s exited %d: %s" % (path, completed.returncode, (completed.stderr or "").strip()[:300]))
    raise last


def fetch_run_created(repo, run_id, get):
    """The run's creation time (the moment the vars snapshot was taken)."""
    body = get("/repos/%s/actions/runs/%s" % (repo, run_id))
    created = parse_time(body.get("created_at"))
    if created is None:
        raise ValueError("run %s has no readable created_at" % run_id)
    return created


def fetch_concurrent(repo, run_id, run_created, get):
    """Ids of windows.yml runs created before this one whose routing job is still queued or running."""
    earlier = []
    seen = set()
    for status in ("queued", "in_progress"):
        body = get("/repos/%s/actions/workflows/windows.yml/runs?status=%s&per_page=30" % (repo, status))
        for run in body.get("workflow_runs") or []:
            other = run.get("id")
            if other is None or other == int(run_id) or other in seen:
                continue
            seen.add(other)
            created = parse_time(run.get("created_at"))
            if created is None or created > run_created or (created == run_created and other > int(run_id)):
                continue
            earlier.append(other)
    concurrent = []
    for other in sorted(earlier)[:CONCURRENT_RUNS_MAX]:
        jobs = get("/repos/%s/actions/runs/%s/jobs?filter=latest&per_page=100" % (repo, other)).get("jobs") or []
        if any(job.get("name") == ROUTE_JOB_NAME and job.get("status") in ("queued", "in_progress") for job in jobs):
            concurrent.append(other)
    return concurrent


def replay_line(env, now, run_created, concurrent):
    parts = ["WINDOWS_RUNNER_MODE=%s" % json.dumps(env.get("WINDOWS_RUNNER_MODE") or ""),
             "WINDOWS_HOME_STATE=%s" % json.dumps(env.get("WINDOWS_HOME_STATE") or ""),
             "ROUTE_LANES=%s" % (env.get("ROUTE_LANES") or ""),
             "ROUTE_NOW=%d" % now, "ROUTE_RUN_CREATED=%d" % run_created,
             "ROUTE_CONCURRENT=%s" % json.dumps(json.dumps(concurrent)),
             "ROUTE_EVENT=%s" % (env.get("ROUTE_EVENT") or ""),
             "ROUTE_HEAD_REPO=%s" % (env.get("ROUTE_HEAD_REPO") or ""),
             "ROUTE_REPO=%s" % (env.get("ROUTE_REPO") or "")]
    return " ".join(parts) + " python3 ci/windows-runner/route/route.py"


def render_summary(summary, decisions, lanes, run_id, attempt, replay):
    lines = ["## Windows lanes: mode %s (run %s, attempt %s)" % (summary["mode"], run_id, attempt), ""]
    if summary["slots"] is not None:
        lines += ["| state age at creation | serving | slots | running | queued | earlier routers | free slots | deadline |",
                  "|---|---|---|---|---|---|---|---|",
                  "| %s s | %s | %s | %s | %s | %s | %s | %s |" % (
                      summary["state_age"], summary["serving"], summary["slots"], summary["running"], summary["queued"],
                      "-" if summary["concurrent"] is None else summary["concurrent"],
                      "-" if summary["free"] is None else summary["free"],
                      "-" if summary["deadline"] is None else "%d min" % (summary["deadline"] // 60)), ""]
    lines += [summary["reason"], "", "| lane | pool | reason |", "|---|---|---|"]
    for lane in lanes:
        decision = decisions.get(lane)
        if decision:
            lines.append("| %s | %s | %s |" % (lane, decision["pool"], decision["reason"]))
    for lane in summary["unknown_lanes"]:
        lines.append("| %s | (withheld) | not a routed lane; the workflow's fallback applies |" % lane)
    lines += ["", "Replay: `%s`" % replay]
    return "\n".join(lines) + "\n"


def write_outputs(env, decisions, attempt):
    lines = ["attempt=%s" % attempt] + ["%s-pool=%s" % (lane, decision["pool"]) for lane, decision in decisions.items()]
    out = env.get("GITHUB_OUTPUT")
    if out:
        with open(out, "a", encoding="utf-8") as handle:
            handle.write("\n".join(lines) + "\n")
    else:
        print("\n".join(lines))


def _int_env(env, name, default):
    value = (env.get(name) or "").strip()
    if not value:
        return default
    try:
        return int(value)
    except ValueError:
        print("ignoring malformed %s=%r" % (name, value))
        return default


def main(env=None, get=gh_api, clock=time.time):
    env = os.environ if env is None else env
    lanes = [lane.strip() for lane in (env.get("ROUTE_LANES") or "").split(",") if lane.strip()]
    attempt = (env.get("ROUTE_ATTEMPT") or "").strip() or "1"
    run_id = (env.get("ROUTE_RUN_ID") or "").strip() or "0"
    repo = env.get("ROUTE_REPO") or ""
    mode = env.get("WINDOWS_RUNNER_MODE")
    now = _int_env(env, "ROUTE_NOW", int(clock()))
    run_created = now
    concurrent = []
    try:
        normalized = (mode or "").strip().lower()
        if normalized == "home":
            if (env.get("ROUTE_RUN_CREATED") or "").strip():
                run_created = _int_env(env, "ROUTE_RUN_CREATED", now)
            else:
                try:
                    run_created = fetch_run_created(repo, run_id, get)
                except Exception as failure:  # noqa: BLE001 - optional read; the clock stands in
                    print("::notice::run creation time unavailable (%s); using now" % failure)
            if (env.get("ROUTE_CONCURRENT") or "").strip():
                concurrent = [int(item) for item in json.loads(env["ROUTE_CONCURRENT"])]
            else:
                try:
                    concurrent = fetch_concurrent(repo, run_id, run_created, get)
                except Exception as failure:  # noqa: BLE001 - optional read; assume no earlier router
                    print("::notice::earlier routers unknown (%s); assuming none" % failure)
        decisions, summary = decide(mode, lanes, env.get("WINDOWS_HOME_STATE"), now, run_created, concurrent,
                                    stale_seconds=_int_env(env, "ROUTE_STALE_SECONDS", DEFAULT_STALE_SECONDS),
                                    slots_max=_int_env(env, "ROUTE_SLOTS_MAX", DEFAULT_SLOTS_MAX),
                                    event=env.get("ROUTE_EVENT") or "", head_repo=env.get("ROUTE_HEAD_REPO") or "", repo=repo)
    except Exception as failure:  # noqa: BLE001 - every failure routes to the vendor, never fails the run
        reason = "%s: %s; every lane goes to the vendor" % (type(failure).__name__, failure)
        print("::warning title=Windows lane router::%s" % reason)
        decisions = _everything(lanes, "vendor", reason)
        summary = _summary((mode or "").strip().lower() or "(unset)")
        summary["reason"] = reason
    text = render_summary(summary, decisions, lanes, run_id, attempt, replay_line(env, now, run_created, concurrent))
    print(text)
    step_summary = env.get("GITHUB_STEP_SUMMARY")
    if step_summary:
        with open(step_summary, "a", encoding="utf-8") as handle:
            handle.write(text)
    write_outputs(env, decisions, attempt)
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `python -I -m unittest discover -v -s ci/windows-runner/route/tests -t ci/windows-runner/route`
Expected: `Ran 49 tests … OK`.

- [ ] **Step 5: Replay two decisions by hand**

Run (on Windows `python`; `python3` on the owner's machine is the Microsoft Store stub; on Linux and macOS `python3`):

```bash
WINDOWS_RUNNER_MODE=home WINDOWS_HOME_STATE='{"v":1,"at":"2026-10-11T15:00:00Z","serving":true,"slots":2,"running":[],"queued":[]}' ROUTE_LANES=rust,app,model-0,model-1 ROUTE_NOW=1791730800 ROUTE_RUN_CREATED=1791730800 ROUTE_CONCURRENT='[]' ROUTE_REPO=coryj627/slate python ci/windows-runner/route/route.py
```

Expected: `rust-pool=home`, `app-pool=home`, `model-0-pool=home`, `model-1-pool=vendor`, and a `Replay:` line. Then the same with `"at":"2026-10-11T14:00:00Z"`: every pool `vendor` and the reason `state is 3600 s old at the run's creation`.

- [ ] **Step 6: Run the router's tests in CI**

In `.github/workflows/windows-runner-tests.yml`, after the `Run the suite` step, add (the same pinned setup-python as `mac-runner-tests.yml`):

```yaml
      - name: Set up Python
        if: ${{ !cancelled() }}
        uses: actions/setup-python@5fda3b95a4ea91299a34e894583c3862153e4b97  # v7.0.0
        with:
          python-version: '3.13'

      - name: Run the router's tests
        # The Windows lane router (ci/windows-runner/route/route.py) is
        # Python, like the mac runner's; same discover shape as
        # mac-runner-tests.yml. Runs even when the Pester step failed, so
        # one red suite cannot hide the other.
        if: ${{ !cancelled() }}
        run: python -I -m unittest discover -v -s ci/windows-runner/route/tests -t ci/windows-runner/route
```

- [ ] **Step 7: Commit**

```bash
git add ci/windows-runner/route .github/workflows/windows-runner-tests.yml
git commit -m "feat(ci-route): the Windows lane router over the state the host publishes"
```

---

### Task 2: `windows.yml` and `nightly.yml` route through the router, and the guards pin it

**Files:**
- Modify: `.github/workflows/windows.yml` (header comment lines 36–51; both path filters; a `route` job after `jobs:`; the four routed lanes; `flaui`; `gate`)
- Modify: `.github/workflows/nightly.yml` (lines 163 and 176: `home-only` routes home like `home`)
- Modify: `ci/windows-runner/tests/Workflows.Tests.ps1` (replace `Describe 'windows.yml pool switch'` and `Describe 'nightly.yml pool switch'`; add a block-slicing helper)

**Interfaces:**
- Consumes: Task 1's outputs and env names.
- Produces: the predicate each lane uses twice, verbatim (rust):
  `(needs.route.outputs.attempt == github.run_attempt && needs.route.outputs.rust-pool == 'home')`.

- [ ] **Step 1: Rewrite the guards so they fail against today's workflows, and add the lane-table test**

In `ci/windows-runner/route/tests/test_route.py`, `class Constants`, after `test_lane_table_is_consistent`, add the method Task 1 left out (it fails until Step 5 adds `ROUTE_LANES`):

```python
    def test_workflow_routes_exactly_the_lane_table(self):
        path = os.path.join(os.path.dirname(__file__), "..", "..", "..", "..", ".github", "workflows", "windows.yml")
        if not os.path.exists(path):
            self.skipTest("windows.yml is not beside this checkout")
        with open(path, encoding="utf-8") as handle:
            text = handle.read()
        match = re.search(r"ROUTE_LANES: ([a-z0-9,-]+)", text)
        self.assertIsNotNone(match)
        self.assertEqual(match.group(1).split(","), ["rust", "app", "model-0", "model-1"])
        self.assertEqual(sorted(match.group(1).split(",")), sorted(route.LANES))
```

In `ci/windows-runner/tests/Workflows.Tests.ps1`, add to `BeforeAll` a helper that slices one job's block (from `\n  <job>:` to the next line that starts a top-level or two-space key), so key order and comments inside a job do not break the pins:

```powershell
    function Get-JobBlock([string]$Text, [string]$Job) {
        $start = $Text.IndexOf("`n  ${Job}:")
        if ($start -lt 0) { return '' }
        $rest = $Text.Substring($start + 1)
        $next = [regex]::Match($rest.Substring(1), '(?m)^(\S|  \S)')
        if ($next.Success) { return $rest.Substring(0, $next.Index + 1) }
        return $rest
    }
    $script:vendor = @{
        rust  = "format('namespace-profile-winx64-fast{0};overrides.cache-tag=slate-windows-rust', github.ref != 'refs/heads/main' && '-pr' || '')"
        app   = "format('namespace-profile-winx64-fast{0};overrides.cache-tag=slate-windows-app', github.ref != 'refs/heads/main' && '-pr' || '')"
        model = "format('namespace-profile-winx64-fast{0};overrides.cache-tag=slate-windows-model', github.ref != 'refs/heads/main' && '-pr' || '')"
    }
    function Get-Predicate([string]$Pool) { "(needs.route.outputs.attempt == github.run_attempt && needs.route.outputs.${Pool} == 'home')" }
```

Replace `Describe 'windows.yml pool switch' { … }` with:

```powershell
Describe 'windows.yml routing through the router' {
    It 'has one routing job on hosted Linux that runs only on home or home-only, with both permissions, the sparse checkout, the token and continue-on-error on both steps' {
        $route = Get-JobBlock $windows 'route'
        $route | Should -Not -BeNullOrEmpty
        $route | Should -Match 'name: Route Windows lanes'
        $route.Contains("if: `${{ vars.WINDOWS_RUNNER_MODE == 'home' || vars.WINDOWS_RUNNER_MODE == 'home-only' }}") | Should -BeTrue
        $route | Should -Match 'runs-on: ubuntu-latest'
        $route | Should -Match '(?m)^\s+permissions:\r?\n\s+actions: read\r?\n\s+contents: read'
        $route | Should -Match 'sparse-checkout: ci/windows-runner/route'
        $route | Should -Match 'persist-credentials: false'
        ([regex]::Matches($route, 'continue-on-error: true')).Count | Should -Be 2
        foreach ($env in 'GH_TOKEN: ${{ github.token }}', 'WINDOWS_RUNNER_MODE: ${{ vars.WINDOWS_RUNNER_MODE }}', 'WINDOWS_HOME_STATE: ${{ vars.WINDOWS_HOME_STATE }}',
            'ROUTE_LANES: rust,app,model-0,model-1', 'ROUTE_ATTEMPT: ${{ github.run_attempt }}', 'ROUTE_RUN_ID: ${{ github.run_id }}',
            'ROUTE_EVENT: ${{ github.event_name }}', 'ROUTE_HEAD_REPO: ${{ github.event.pull_request.head.repo.full_name }}', 'ROUTE_REPO: ${{ github.repository }}') {
            $route.Contains($env) | Should -BeTrue -Because "the routing step needs $env"
        }
        $route | Should -Match 'run: python3 ci/windows-runner/route/route\.py'
        foreach ($output in 'attempt', 'rust-pool', 'app-pool', 'model-0-pool', 'model-1-pool') {
            $route.Contains("      ${output}: `${{ steps.route.outputs.$output }}") | Should -BeTrue -Because "output $output"
        }
    }
    It 'every routed lane depends on route, runs when route was skipped or failed, and uses the router only in its own attempt with today''s vendor string as the fallback, verbatim' {
        foreach ($pair in @(@('rust-tests', 'rust', 'rust'), @('windows', 'app', 'app'))) {
            $block = Get-JobBlock $windows $pair[0]
            $block | Should -Match '(?m)^    needs: \[route\]'
            $block.Contains("if: `${{ !cancelled() }}") | Should -BeTrue
            $block.Contains("runs-on: `${{ $(Get-Predicate "$($pair[1])-pool") && 'slate-win-$($pair[1])' || $($vendor[$pair[2]]) }}") | Should -BeTrue -Because "lane $($pair[0])"
            $block.Contains("if: `${{ !$(Get-Predicate "$($pair[1])-pool") }}") | Should -BeTrue -Because "the cache gate of $($pair[0])"
        }
        $shards = Get-JobBlock $windows 'windows-model-shard'
        $shards | Should -Match '(?m)^    needs: \[route\]'
        $shards.Contains("if: `${{ !cancelled() }}") | Should -BeTrue
        $selector = "(needs.route.outputs.attempt == github.run_attempt && ((matrix.shard == 0 && needs.route.outputs.model-0-pool == 'home') || (matrix.shard == 1 && needs.route.outputs.model-1-pool == 'home')))"
        $shards.Contains("runs-on: `${{ $selector && 'slate-win-model' || $($vendor.model) }}") | Should -BeTrue
        $shards.Contains("if: `${{ !$selector }}") | Should -BeTrue
        ([regex]::Matches($windows, 'namespacelabs/nscloud-cache-action')).Count | Should -Be 3
    }
    It 'runs the shell gate on windows-latest in every mode but home-only, and only after a green app build whatever happened to route' {
        $flaui = Get-JobBlock $windows 'flaui'
        $flaui.Contains("runs-on: `${{ vars.WINDOWS_RUNNER_MODE == 'home-only' && 'slate-win-shell' || 'windows-latest' }}") | Should -BeTrue
        $flaui.Contains("if: `${{ !cancelled() && needs.windows.result == 'success' }}") | Should -BeTrue
    }
    It 'the gate waits on route too and names the pools when it fails' {
        $gate = Get-JobBlock $windows 'gate'
        $gate | Should -Match ([regex]::Escape('needs: [route, rust-tests, windows, windows-model, flaui]'))
        $gate | Should -Match 'pools: rust=\$\{\{ needs\.route\.outputs\.rust-pool \}\}'
    }
    It 'every routed lane prints its runner first' {
        foreach ($job in 'rust-tests', 'windows', 'windows-model-shard') {
            (Get-JobBlock $windows $job) | Should -Match '- name: Record the runner'
        }
    }
    It 'keeps no bare vendor runs-on, never compares the variable with namespace, and reads it only where the spec says' {
        ([regex]::Matches($windows, 'runs-on: namespace-profile')).Count | Should -Be 0
        ([regex]::Matches($windows, 'runs-on: windows-latest')).Count | Should -Be 0
        foreach ($text in $windows, $nightly, $pilot) { ([regex]::Matches($text, "WINDOWS_RUNNER_MODE == 'namespace'")).Count | Should -Be 0 }
        # Every comparison in the three workflows is with home or home-only.
        $reads = @([regex]::Matches($windows + "`n" + $nightly + "`n" + $pilot, "vars\.WINDOWS_RUNNER_MODE (==|!=) '([^']*)'"))
        $reads.Count | Should -Be 7 -Because 'windows.yml: the route if (2) and the shell gate (1); nightly.yml: runs-on (2) and cache (2)'
        foreach ($read in $reads) { @('home', 'home-only') | Should -Contain $read.Groups[2].Value }
    }
    It 'runs the workflow when the router changes' {
        ([regex]::Matches($windows, [regex]::Escape("- 'ci/windows-runner/route/**'"))).Count | Should -Be 2
    }
    It 'documents the modes, the published state and the full re-run in the header' {
        $windows | Should -Match 'self-hosted-windows-runner\.md'
        $joined = $windows -replace '\r?\n#\s*', ' '
        foreach ($phrase in 'home-only', 'WINDOWS_HOME_STATE', 'Re-run all jobs', "anything else, including unset, is today's providers") {
            $joined | Should -Match ([regex]::Escape($phrase))
        }
    }
}

Describe 'nightly.yml pool switch' {
    It 'routes the Windows stress job home on home and home-only, and restores the GitHub cache everywhere else' {
        $nightly.Contains("runs-on: `${{ (vars.WINDOWS_RUNNER_MODE == 'home' || vars.WINDOWS_RUNNER_MODE == 'home-only') && 'slate-win-app' || 'windows-latest' }}") | Should -BeTrue
        $nightly.Contains("cache: `${{ (vars.WINDOWS_RUNNER_MODE != 'home' && vars.WINDOWS_RUNNER_MODE != 'home-only') && 'true' || 'false' }}") | Should -BeTrue
    }
}
```

Add to `Describe 'windows-runner-tests.yml path filters'`:

```powershell
    It 'runs the router''s Python tests even when Pester failed' {
        $lane | Should -Match ([regex]::Escape('python -I -m unittest discover -v -s ci/windows-runner/route/tests -t ci/windows-runner/route'))
        $step = $lane.Substring($lane.IndexOf("Run the router's tests"))
        $step.Substring(0, 400) | Should -Match ([regex]::Escape('if: ${{ !cancelled() }}'))
    }
```

- [ ] **Step 2: Run the guards to verify they fail**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1 -Filter 'windows.yml routing*'`
Expected: every `It` in the new describe fails; `nightly.yml pool switch` fails on both strings.

- [ ] **Step 3: Rewrite the header comment**

Replace lines 36–51 of `.github/workflows/windows.yml` (from `# Runner selection, revised 2026-10-10:` through `# scheme both replaced is in git history at a5037b35.`) with:

```yaml
# Runner selection, revised 2026-10-11: the repository variable
# WINDOWS_RUNNER_MODE and the routing job `route` decide per lane, from
# the state the home host publishes every tick in the repository variable
# WINDOWS_HOME_STATE (heartbeat, slots, what runs, what is queued).
# `home` means home first: a lane takes a free slot of the self-hosted
# pool on the owner's desktop (one throwaway Hyper-V VM per job, labels
# slate-win-{rust,app,model}); when none is free it queues there only if
# it would still finish inside the run's critical path (the app lane plus
# the shell gate), and otherwise runs on Namespace at once. A state older
# than two minutes, or one that says the host is not serving, sends every
# lane to Namespace by itself. `home-only` queues every lane at home
# (golden-image validation, fidelity runs). `namespace` or anything else,
# including unset, is today's providers, verbatim, and the routing job
# does not run at all: a run that cannot read repository variables (a
# fork or Dependabot pull request) never waits on the home host, and a
# fork pull request is sent to Namespace by the router and refused and
# evicted by the host even if its copy of the router says otherwise. The
# shell gate stays on GitHub-hosted windows-latest in every mode but
# home-only: free on a public repository and faster than the home pool.
# ci/windows-runner/route/route.py is the rule; its step summary shows
# what each run saw and decided, with a replay line. A lane trusts the
# router's answer only in the attempt it ran in, so "Re-run failed jobs"
# (which reuses it) always lands on Namespace: after a flip or an outage
# use "Re-run all jobs" (`gh run rerun <id>`). The runbook
# docs/runbooks/self-hosted-windows-runner.md has the design, the
# fallback flip and the trust model; today's providers are the 2026-07-31
# arrangement: namespace-profile-winx64-fast[-pr] (dashboard-configured,
# Cache Volumes enabled) for the build/test lanes and windows-latest for
# the shell gate. Flip with `gh variable set WINDOWS_RUNNER_MODE --body
# namespace` (and back with `--body home`). The per-attempt actions/cache
# scheme both replaced is in git history at a5037b35.
```

- [ ] **Step 4: Add the router to both path filters**

In both `pull_request.paths` and `push.paths`, after `- '.github/workflows/windows.yml'`, add `      - 'ci/windows-runner/route/**'`.

- [ ] **Step 5: Add the routing job**

Immediately after `jobs:`, insert:

```yaml
  # The rule lives in ci/windows-runner/route/route.py (spec §3–§5): it
  # reads the state the host publishes (vars) and decides a pool per lane.
  # It runs only when the mode is home or home-only and never fails the
  # run: both steps continue on error, and every routed lane falls back to
  # the vendor when an output is missing or was written by an earlier
  # attempt.
  route:
    name: Route Windows lanes
    if: ${{ vars.WINDOWS_RUNNER_MODE == 'home' || vars.WINDOWS_RUNNER_MODE == 'home-only' }}
    runs-on: ubuntu-latest
    timeout-minutes: 5
    permissions:
      actions: read
      contents: read
    outputs:
      attempt: ${{ steps.route.outputs.attempt }}
      rust-pool: ${{ steps.route.outputs.rust-pool }}
      app-pool: ${{ steps.route.outputs.app-pool }}
      model-0-pool: ${{ steps.route.outputs.model-0-pool }}
      model-1-pool: ${{ steps.route.outputs.model-1-pool }}
    steps:
      - name: Checkout the route script
        continue-on-error: true
        uses: actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0  # v7
        with:
          sparse-checkout: ci/windows-runner/route
          persist-credentials: false
      - name: Pick a pool for each lane from the mode and the host's published state
        id: route
        continue-on-error: true
        env:
          GH_TOKEN: ${{ github.token }}
          WINDOWS_RUNNER_MODE: ${{ vars.WINDOWS_RUNNER_MODE }}
          WINDOWS_HOME_STATE: ${{ vars.WINDOWS_HOME_STATE }}
          ROUTE_LANES: rust,app,model-0,model-1
          ROUTE_ATTEMPT: ${{ github.run_attempt }}
          ROUTE_RUN_ID: ${{ github.run_id }}
          ROUTE_EVENT: ${{ github.event_name }}
          ROUTE_HEAD_REPO: ${{ github.event.pull_request.head.repo.full_name }}
          ROUTE_REPO: ${{ github.repository }}
        run: python3 ci/windows-runner/route/route.py

```

- [ ] **Step 6: Route the rust and app lanes**

The `rust-tests` job head becomes (its cache step's `if` changes too):

```yaml
  rust-tests:
    name: rust tests (windows x64)
    needs: [route]
    if: ${{ !cancelled() }}
    runs-on: ${{ (needs.route.outputs.attempt == github.run_attempt && needs.route.outputs.rust-pool == 'home') && 'slate-win-rust' || format('namespace-profile-winx64-fast{0};overrides.cache-tag=slate-windows-rust', github.ref != 'refs/heads/main' && '-pr' || '') }}
    timeout-minutes: 60
    steps:
      - name: Record the runner
        run: Write-Host "runner=${{ runner.name }} environment=${{ runner.environment }} attempt=${{ github.run_attempt }} router-attempt=${{ needs.route.outputs.attempt }}"

      - name: Checkout
```

```yaml
        if: ${{ !(needs.route.outputs.attempt == github.run_attempt && needs.route.outputs.rust-pool == 'home') }}
```

The `windows` job the same with `app`: `needs: [route]`, `if: ${{ !cancelled() }}`,

```yaml
    runs-on: ${{ (needs.route.outputs.attempt == github.run_attempt && needs.route.outputs.app-pool == 'home') && 'slate-win-app' || format('namespace-profile-winx64-fast{0};overrides.cache-tag=slate-windows-app', github.ref != 'refs/heads/main' && '-pr' || '') }}
```

the same `Record the runner` first step, and its cache step
`if: ${{ !(needs.route.outputs.attempt == github.run_attempt && needs.route.outputs.app-pool == 'home') }}`.

- [ ] **Step 7: Route the model shards**

The `windows-model-shard` job head becomes:

```yaml
  windows-model-shard:
    name: app model (windows x64, shard ${{ matrix.shard }})
    needs: [route]
    if: ${{ !cancelled() }}
    runs-on: ${{ (needs.route.outputs.attempt == github.run_attempt && ((matrix.shard == 0 && needs.route.outputs.model-0-pool == 'home') || (matrix.shard == 1 && needs.route.outputs.model-1-pool == 'home'))) && 'slate-win-model' || format('namespace-profile-winx64-fast{0};overrides.cache-tag=slate-windows-model', github.ref != 'refs/heads/main' && '-pr' || '') }}
    strategy:
```

(keep `strategy`, `timeout-minutes` and the comments), its first step `Record the runner` as above, and its cache step:

```yaml
        if: ${{ !(needs.route.outputs.attempt == github.run_attempt && ((matrix.shard == 0 && needs.route.outputs.model-0-pool == 'home') || (matrix.shard == 1 && needs.route.outputs.model-1-pool == 'home'))) }}
```

- [ ] **Step 8: The shell gate, the aggregate, the nightly**

`flaui` gains, under `needs: [windows]`:

```yaml
    if: ${{ !cancelled() && needs.windows.result == 'success' }}
    runs-on: ${{ vars.WINDOWS_RUNNER_MODE == 'home-only' && 'slate-win-shell' || 'windows-latest' }}
```

`gate`: `needs: [route, rust-tests, windows, windows-model, flaui]` and its error line:

```yaml
            echo "::error::rust-tests=${{ needs.rust-tests.result }} app=${{ needs.windows.result }} model=${{ needs.windows-model.result }} flaui=${{ needs.flaui.result }} pools: rust=${{ needs.route.outputs.rust-pool }} app=${{ needs.route.outputs.app-pool }} model-0=${{ needs.route.outputs.model-0-pool }} model-1=${{ needs.route.outputs.model-1-pool }} (router attempt ${{ needs.route.outputs.attempt }} of ${{ github.run_attempt }})"
```

`nightly.yml` line 163 becomes
`runs-on: ${{ (vars.WINDOWS_RUNNER_MODE == 'home' || vars.WINDOWS_RUNNER_MODE == 'home-only') && 'slate-win-app' || 'windows-latest' }}`
and line 176 `cache: ${{ (vars.WINDOWS_RUNNER_MODE != 'home' && vars.WINDOWS_RUNNER_MODE != 'home-only') && 'true' || 'false' }}`; its comment line 161 becomes `    # The home pool when WINDOWS_RUNNER_MODE is `home` or `home-only`; unset or` (line 162 unchanged).

- [ ] **Step 9: Run both suites to verify they pass**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1` and `python -I -m unittest discover -v -s ci/windows-runner/route/tests -t ci/windows-runner/route`
Expected: all green; the Python suite `Ran 50 tests … OK`. Then `python -c "import yaml,sys; [yaml.safe_load(open(p, encoding='utf-8')) for p in sys.argv[1:]]" .github/workflows/windows.yml .github/workflows/nightly.yml` prints nothing (`python -m pip install --user pyyaml` if PyYAML is absent; `encoding='utf-8'` matters on Windows, the files hold UTF-8 punctuation).

- [ ] **Step 10: Commit**

```bash
git add .github/workflows/windows.yml .github/workflows/nightly.yml ci/windows-runner/tests/Workflows.Tests.ps1
git commit -m "ci(windows): route every lane through the router's pools, trust it only in its own attempt, keep the shell gate hosted"
```

---

### Task 3: The host publishes its state every tick (OR-D2)

**Files:**
- Modify: `ci/windows-runner/SlateCiHost.psm1` (new `$script:JournalDefaults`, `New-Journal`, `Read-Journal`, new `Get-PublishedState`, new `Publish-HomeState`, `Invoke-Admission` returns the publishable queue, `Invoke-OrchestratorTick`, exports)
- Modify: `ci/windows-runner/adapters/GitHub.ps1` (new `Set-GhVariable`; `New-GitHubAdapters` gains `PublishState`)
- Modify: `ci/windows-runner/tests/SlateCiHost.Tests.ps1`, `ci/windows-runner/tests/Orchestrator.Tests.ps1`, `ci/windows-runner/tests/GitHubAdapter.Tests.ps1`

**Interfaces:**
- Produces: `Get-PublishedState -Config -Journal -Queued -Now -> [ordered]` with keys `v`, `at`, `serving`, `slots`, `running`, `queued` (spec §4); `Publish-HomeState -Config -Journal -Adapters -State -Now`; journal keys `PublishedState` (string), `PublishedAt`, `PublishFailedAt`; adapter `PublishState <json>`; `Invoke-Admission` returns the array of queued jobs the host would admit with unlimited slots (its `Queued`).
- Consumed by Task 1's `parse_state` (the shape) and Task 4 (admission's queue is what Task 4's foreign filter leaves).

- [ ] **Step 1: Write the failing tests**

`ci/windows-runner/tests/SlateCiHost.Tests.ps1`, in the journal describe (and in later tasks every new key is added to this one list):

```powershell
    It 'New-Journal and Read-Journal share one set of top-level keys' {
        $p = Join-Path $TestDrive 'bare.json'
        '{"Vms":{},"Retries":{},"SeenJobs":{}}' | Set-Content $p
        # The expected side is sorted the same way: Sort-Object ignores case, ordinal order differs.
        @((New-Journal).Keys | Sort-Object) | Should -Be @('PublishFailedAt', 'PublishedAt', 'PublishedState', 'Retries', 'SeenJobs', 'Vms' | Sort-Object)
        @((Read-Journal -Path $p).Keys | Sort-Object) | Should -Be @((New-Journal).Keys | Sort-Object)
        (Read-Journal -Path $p).PublishedState | Should -Be $null
    }
```

and a new describe:

```powershell
Describe 'Get-PublishedState' {
    BeforeAll {
        $script:cfg = Get-CiHostConfig -Path (Join-Path $PSScriptRoot '..' 'config.json')
        $script:now = [datetimeoffset]::Parse('2026-10-11T15:04:05Z')
    }
    It 'reports an idle pool' {
        $s = Get-PublishedState -Config $cfg -Journal (New-Journal) -Queued @() -Now $now
        $s.v | Should -Be 1
        $s.at | Should -Be '2026-10-11T15:04:05Z'
        $s.serving | Should -BeTrue
        $s.slots | Should -Be 2
        @($s.running).Count | Should -Be 0
        @($s.queued).Count | Should -Be 0
        ($s | ConvertTo-Json -Compress -Depth 5) | Should -Be '{"v":1,"at":"2026-10-11T15:04:05Z","serving":true,"slots":2,"running":[],"queued":[]}'
    }
    It 'lists every VM that holds a slot with its provisioning time, skips a pending commit, and counts a VM in teardown against the slots' {
        $j = New-Journal
        $j.Vms['a'] = @{ Name = 'a'; Lane = 'app'; Phase = 'handed'; StartedAt = '2026-10-11T14:48:51+00:00'; PendingCommit = $false }
        $j.Vms['b'] = @{ Name = 'b'; Lane = 'model'; Phase = 'handed'; StartedAt = '2026-10-11T14:50:00Z'; PendingCommit = $true }
        $j.Vms['c'] = @{ Name = 'c'; Lane = 'rust'; Phase = 'discarding'; StartedAt = '2026-10-11T14:00:00Z'; PendingCommit = $false }
        $s = Get-PublishedState -Config $cfg -Journal $j -Queued @() -Now $now
        $s.slots | Should -Be 1
        $s.serving | Should -BeTrue
        @($s.running | ForEach-Object { "$($_.lane)@$($_.since)" }) | Should -Be @('app@2026-10-11T14:48:51Z')
    }
    It 'is not serving when every slot is held by a teardown' {
        $j = New-Journal
        $j.Vms['c'] = @{ Name = 'c'; Lane = 'rust'; Phase = 'discarding'; StartedAt = '2026-10-11T14:00:00Z'; PendingCommit = $false }
        $j.Vms['d'] = @{ Name = 'd'; Lane = 'app'; Phase = 'discarding'; StartedAt = '2026-10-11T14:00:00Z'; PendingCommit = $false }
        $s = Get-PublishedState -Config $cfg -Journal $j -Queued @() -Now $now
        $s.slots | Should -Be 0
        $s.serving | Should -BeFalse
    }
    It 'lists the queue oldest first with creation times' {
        $queued = @([pscustomobject]@{ JobId = 2; Lane = 'model'; CreatedAt = [datetimeoffset]::Parse('2026-10-11T14:40:00Z') },
                    [pscustomobject]@{ JobId = 1; Lane = 'rust'; CreatedAt = [datetimeoffset]::Parse('2026-10-11T14:39:00Z') })
        $s = Get-PublishedState -Config $cfg -Journal (New-Journal) -Queued $queued -Now $now
        @($s.queued | ForEach-Object { "$($_.lane)@$($_.since)" }) | Should -Be @('rust@2026-10-11T14:39:00Z', 'model@2026-10-11T14:40:00Z')
    }
}
```

`ci/windows-runner/tests/Orchestrator.Tests.ps1`: in `New-World` add `Published = [System.Collections.ArrayList]::new()`; in `New-FakeAdapters` add

```powershell
            PublishState  = { param($json) & $script:record 'PublishState' @($json); [void]$script:world.Published.Add($json) }
```

and a context:

```powershell
Context 'publishing the state' {
    It 'publishes after the first tick, again only when the state changed, and at least once a minute' {
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        (Get-Calls 'PublishState').Count | Should -Be 1
        $journal.PublishedState | Should -Be ([string]$world.Published[0])
        ($journal.PublishedState | ConvertFrom-Json).slots | Should -Be 2
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        (Get-Calls 'PublishState').Count | Should -Be 1
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(20)
        (Get-Calls 'PublishState').Count | Should -Be 2
        $state = $journal.PublishedState | ConvertFrom-Json
        @($state.running).Count | Should -Be 1
        $state.running[0].lane | Should -Be 'app'
        $world.Queued = @()
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(30)
        (Get-Calls 'PublishState').Count | Should -Be 2
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(81)
        (Get-Calls 'PublishState').Count | Should -Be 3 -Because 'the heartbeat refreshes after 60 s even when nothing changed'
    }
    It 'publishes the queue it would admit with unlimited slots, not the jobs it skipped' {
        $world.Queued = @((New-QueuedJob 1 10 'app'), (New-QueuedJob 2 10 'rust'), (New-QueuedJob 3 10 'model'), (New-QueuedJob 4 10 'app'))
        $journal.Retries['4'] = @{ Count = 3; NextAt = $t0.AddDays(-1).ToString('o') }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $state = $journal.PublishedState | ConvertFrom-Json
        @($state.running).Count | Should -Be 2
        @($state.queued | ForEach-Object { $_.lane }) | Should -Be @('model')
    }
    It 'logs a failed publish once a minute and keeps ticking' {
        $adapters.PublishState = { param($json) throw '403' }
        { Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0 } | Should -Not -Throw
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        ([regex]::Matches(($logs -join "`n"), 'publish: 403')).Count | Should -Be 1
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(70)
        ([regex]::Matches(($logs -join "`n"), 'publish: 403')).Count | Should -Be 2
        $journal.PublishedState | Should -Be $null
    }
}
```

`ci/windows-runner/tests/GitHubAdapter.Tests.ps1`: next to `Remove-GhRunner / Get-GhRunners`, using that file's existing `Invoke-RestMethod` mocking style:

```powershell
Describe 'Set-GhVariable' {
    It 'patches the variable, creates it with POST when the patch answers 404, and rethrows anything else' {
        Mock Invoke-RestMethod { [pscustomobject]@{} }
        Set-GhVariable -Name 'WINDOWS_HOME_STATE' -Value '{"v":1}'
        Should -Invoke Invoke-RestMethod -Times 1 -Exactly -ParameterFilter {
            $Method -eq 'PATCH' -and $Uri -like '*/actions/variables/WINDOWS_HOME_STATE' -and
            ($Body | ConvertFrom-Json).name -eq 'WINDOWS_HOME_STATE' -and ($Body | ConvertFrom-Json).value -eq '{"v":1}'
        }
        Should -Invoke Invoke-RestMethod -Times 0 -Exactly -ParameterFilter { $Method -eq 'POST' }
        Mock Invoke-RestMethod {
            if ($Method -eq 'PATCH') {
                $response = [System.Net.Http.HttpResponseMessage]::new([System.Net.HttpStatusCode]::NotFound)
                throw [Microsoft.PowerShell.Commands.HttpResponseException]::new('not found', $response)
            }
            [pscustomobject]@{}
        }
        Set-GhVariable -Name 'WINDOWS_HOME_STATE' -Value '{"v":1}'
        Should -Invoke Invoke-RestMethod -Times 1 -Exactly -ParameterFilter {
            $Method -eq 'POST' -and $Uri -like '*/actions/variables' -and
            ($Body | ConvertFrom-Json).name -eq 'WINDOWS_HOME_STATE' -and ($Body | ConvertFrom-Json).value -eq '{"v":1}'
        }
        Mock Invoke-RestMethod {
            $response = [System.Net.Http.HttpResponseMessage]::new([System.Net.HttpStatusCode]::Forbidden)
            throw [Microsoft.PowerShell.Commands.HttpResponseException]::new('forbidden', $response)
        }
        { Set-GhVariable -Name 'WINDOWS_HOME_STATE' -Value '{"v":1}' } | Should -Throw
    }
}
```

(That file's `BeforeAll` already initialises the adapter with a fake token, as the other describes rely on.) Also add `'PublishState'` to `New-GitHubAdapters`'s expected key list in that file's `New-GitHubAdapters` describe.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1`
Expected: the new tests fail (`Get-PublishedState` not a command; no `PublishState` call; journal keys differ).

- [ ] **Step 3: Implement**

In `SlateCiHost.psm1`, replace `New-Journal` and the defaults loop in `Read-Journal` with one source of keys:

```powershell
# Every top-level journal key, present from creation and after a read of
# an older file: the state machine reads them with index syntax and a
# missing key would throw under strict mode.
$script:JournalDefaults = @{ Vms = @{}; Retries = @{}; SeenJobs = @{}; PublishedState = $null; PublishedAt = $null; PublishFailedAt = $null }

function New-Journal {
    $journal = @{}
    foreach ($key in $script:JournalDefaults.Keys) {
        $value = $script:JournalDefaults[$key]
        $journal[$key] = $(if ($value -is [hashtable]) { @{} } else { $value })
    }
    return $journal
}
```

and in `Read-Journal` replace `foreach ($key in 'Vms', 'Retries', 'SeenJobs') { … }` with:

```powershell
    foreach ($key in $script:JournalDefaults.Keys) {
        if (-not $journal.Contains($key)) { $journal[$key] = $(if ($script:JournalDefaults[$key] -is [hashtable]) { @{} } else { $null }) }
        elseif ($null -eq $journal[$key] -and $script:JournalDefaults[$key] -is [hashtable]) { $journal[$key] = @{} }
    }
```

New functions after `Get-FreeSlots`:

```powershell
function ConvertTo-StateTime {
    # Invariant culture: a calendar or time separator of the host's locale
    # would publish a state the router cannot parse.
    param([Parameter(Mandatory)][datetimeoffset]$Value)
    return $Value.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ', [cultureinfo]::InvariantCulture)
}

function Get-PublishedState {
    # The state the router reads (overflow spec §4): the heartbeat, the
    # slots the host can serve with (a VM in teardown holds one it cannot),
    # every VM that holds a slot with its provisioning time, and the jobs it
    # would admit with unlimited slots, oldest first. A PendingCommit VM is
    # Off and holds no slot (Get-FreeSlots agrees).
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Config, [Parameter(Mandatory)]$Journal, [object[]]$Queued = @(), [Parameter(Mandatory)][datetimeoffset]$Now)
    $held = 0
    $running = @()
    foreach ($vm in @($Journal.Vms.Values | Sort-Object { [string]$_['StartedAt'] })) {
        if ($vm.Contains('PendingCommit') -and $vm['PendingCommit']) { continue }
        if ([string]$vm['Phase'] -eq 'discarding') { $held++; continue }
        $running += [ordered]@{ lane = [string]$vm['Lane']; since = ConvertTo-StateTime (ConvertTo-DateTimeOffset -Value $vm['StartedAt']) }
    }
    $slots = @($Config.Slots).Count - $held
    # Not $queued: PowerShell names ignore case, so that would be $Queued, the parameter, emptied.
    $waiting = @()
    foreach ($job in @($Queued | Sort-Object CreatedAt, JobId)) {
        $waiting += [ordered]@{ lane = [string]$job.Lane; since = ConvertTo-StateTime $job.CreatedAt }
    }
    return [ordered]@{ v = 1; at = ConvertTo-StateTime $Now; serving = ($slots -ge 1); slots = [int]$slots; running = @($running); queued = @($waiting) }
}

function Publish-HomeState {
    # Once per change, and at least once a minute as a heartbeat; a failed
    # write is logged once a minute and never stops the tick (the router
    # treats a stale state as a dead host, the conservative outcome).
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Config, [Parameter(Mandatory)]$Journal, [Parameter(Mandatory)][hashtable]$Adapters, [Parameter(Mandatory)]$State, [Parameter(Mandatory)][datetimeoffset]$Now)
    $json = $State | ConvertTo-Json -Compress -Depth 5
    $previous = [string]$Journal['PublishedState']
    $publishedAt = $Journal['PublishedAt']
    $fresh = $publishedAt -and ($Now - (ConvertTo-DateTimeOffset -Value $publishedAt)).TotalSeconds -lt 60
    # The heartbeat field alone differs every tick: compare without it.
    $sameButTime = $previous -and (($previous -replace '"at":"[^"]*"', '') -eq ($json -replace '"at":"[^"]*"', ''))
    if ($sameButTime -and $fresh) { return }
    try {
        & $Adapters.PublishState $json
        $Journal['PublishedState'] = $json
        $Journal['PublishedAt'] = $Now.ToString('o')
        $Journal['PublishFailedAt'] = $null
    } catch {
        $failedAt = $Journal['PublishFailedAt']
        if (-not $failedAt -or ($Now - (ConvertTo-DateTimeOffset -Value $failedAt)).TotalSeconds -ge 60) {
            $Journal['PublishFailedAt'] = $Now.ToString('o')
            & $Adapters.Log 'warn' "publish: $_"
        }
    }
}
```

`Invoke-Admission` returns the queue it would admit with unlimited slots: at its end (after the admission loop) add

```powershell
    return @(Select-JobsToAdmit -Candidates $queued -ActiveVms $Journal.Vms -FreeSlots ([int]::MaxValue) -Retries $Journal.Retries -Now $Now -RetryCap ([int]$Config.RetryCap))
```

(Task 4 changes `$queued` there to the non-foreign list.) In `Invoke-OrchestratorTick` replace the admission call with:

```powershell
    $queued = @()
    try { $queued = @(Invoke-Admission -Config $Config -Journal $Journal -Adapters $Adapters -Now $Now) }
    catch { & $Adapters.Log 'error' "admission: $_" }
    try { Publish-HomeState -Config $Config -Journal $Journal -Adapters $Adapters -State (Get-PublishedState -Config $Config -Journal $Journal -Queued $queued -Now $Now) -Now $Now }
    catch { & $Adapters.Log 'error' "publish: $_" }
```

Every other `Invoke-Admission` caller (there are none outside tests) is unaffected: a returned array is ignored by `[void]` or assignment. Export `Get-PublishedState, Publish-HomeState`.

`adapters/GitHub.ps1`, after `Get-GhRunners`:

```powershell
function Set-GhVariable {
    # The published state (overflow spec §4). PATCH updates; a 404 means the
    # variable does not exist yet and POST creates it. The token needs the
    # Variables permission (read and write).
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][AllowEmptyString()][string]$Value)
    try {
        Invoke-GhApi -Method PATCH -Path "/actions/variables/$Name" -Body @{ name = $Name; value = $Value } | Out-Null
    } catch {
        if (-not (Test-GhNotFound -ErrorRecord $_)) { throw }
        Invoke-GhApi -Method POST -Path '/actions/variables' -Body @{ name = $Name; value = $Value } | Out-Null
    }
}
```

with, placed before `Get-GhRunner`, the 404 test that function already applies lifted into one helper both call (`Get-GhRunner`'s catch becomes `if (Test-GhNotFound -ErrorRecord $_) { return $null }; throw`, and its tests keep passing):

```powershell
function Test-GhNotFound {
    # True when a failed Invoke-GhApi call was GitHub answering 404 (the web
    # cmdlets keep the response on the exception).
    [CmdletBinding()]
    param([Parameter(Mandatory)][System.Management.Automation.ErrorRecord]$ErrorRecord)
    $response = $ErrorRecord.Exception.Response
    return ($null -ne $response -and [int]$response.StatusCode -eq 404)
}
```

Add `'Set-GhVariable'` to `New-GitHubAdapters`'s command list and `PublishState = { param($json) & $commands['Set-GhVariable'] -Name 'WINDOWS_HOME_STATE' -Value $json }` to its hashtable.

- [ ] **Step 4: Run both suites to verify they pass**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1`
Expected: all green, including every existing orchestrator test (they now also publish; their call counts on other adapters are unchanged).

- [ ] **Step 5: Commit**

```bash
git add ci/windows-runner/SlateCiHost.psm1 ci/windows-runner/adapters/GitHub.ps1 ci/windows-runner/tests
git commit -m "feat(ci-host): publish the pool's state to WINDOWS_HOME_STATE every tick"
```

---

### Task 4: The host refuses jobs of other repositories at admission and evicts them at handoff (OR-D5)

**Files:**
- Modify: `ci/windows-runner/SlateCiHost.psm1` (`Invoke-Admission`)
- Modify: `ci/windows-runner/tests/Orchestrator.Tests.ps1` (the fake `GetRun` answers a trusted run by default; `Context 'admission'`; `Context 'handoff reconciliation'`; one existing count)

**Interfaces:**
- Consumes: the `GetRun` adapter; `$Config.TrustedRepo`.
- Produces: `SeenJobs` entries carry `Foreign` (`$null` unknown, `$true`, `$false`) and `RunCheckedAt` from creation; only `Foreign = $false` jobs are admitted or published; log lines `admission: skipped job <id> of <repo> (not the trusted repository)`, `admission: job <id>: run <run> could not be read: <error>` (once a minute per job), `handoff: <vm> runs job <id> of <repo> (not the trusted repository); forcing it off`.

- [ ] **Step 1: Change the fakes and write the failing tests**

In `New-FakeAdapters`, `GetRun` answers a trusted push run on `main` for any id without a defined run:

```powershell
            GetRun        = { param($id) & $script:record 'GetRun' @($id)
                              if ($script:world.Runs.ContainsKey([string]$id)) { $script:world.Runs[[string]$id] }
                              else { [pscustomobject]@{ id = $id; event = 'push'; head_branch = 'main'; head_repository = [pscustomobject]@{ full_name = 'coryj627/slate' } } } }
```

In the settle test that asserts `(Get-Calls 'GetRun').Count | Should -Be 0` (line 354 today), change it to assert the count taken before that tick does not grow: capture `$before = (Get-Calls 'GetRun').Count` at the start of the `It` and assert `(Get-Calls 'GetRun').Count | Should -Be $before` where the 0 was. In the first admission test extend the `SeenJobs['1']` checks with `$journal.SeenJobs['1'].Foreign | Should -BeFalse` and `$journal.SeenJobs['1'].Keys.Count | Should -Be 5` (`RunId`, `Lane`, `FirstSeenAt`, `Foreign`, `RunCheckedAt`).

Add to `Context 'admission'`:

```powershell
    It 'never admits a job whose run comes from another repository, says so once, and keeps it out of the published queue' {
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        Set-Run 10 'pull_request' 'feature' 'someone/slate'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        (Get-Calls 'NewVm').Count | Should -Be 0
        (Get-Calls 'GetRun').Count | Should -Be 1
        $journal.SeenJobs['1'].Foreign | Should -BeTrue
        ([regex]::Matches(($logs -join "`n"), 'admission: skipped job 1 of someone/slate \(not the trusted repository\)')).Count | Should -Be 1
        @(($journal.PublishedState | ConvertFrom-Json).queued).Count | Should -Be 0
    }
    It 'treats a run without a head repository as foreign' {
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        $world.Runs['10'] = [pscustomobject]@{ id = 10; event = 'pull_request'; head_branch = 'x'; head_repository = $null }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        (Get-Calls 'NewVm').Count | Should -Be 0
        $journal.SeenJobs['1'].Foreign | Should -BeTrue
    }
    It 'retries a run read that failed at most once a minute, logging the first failure only, then admits' {
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        $script:runCalls = 0
        $adapters.GetRun = { param($id) $script:runCalls++; if ($script:runCalls -le 2) { throw '502' }; [pscustomobject]@{ id = $id; event = 'push'; head_branch = 'main'; head_repository = [pscustomobject]@{ full_name = 'coryj627/slate' } } }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        $script:runCalls | Should -Be 1
        (Get-Calls 'NewVm').Count | Should -Be 0
        ([regex]::Matches(($logs -join "`n"), 'admission: job 1: run 10 could not be read')).Count | Should -Be 1
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(61)
        $script:runCalls | Should -Be 2
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(122)
        $script:runCalls | Should -Be 3
        (Get-Calls 'NewVm').Count | Should -Be 1
    }
```

Add to `Context 'handoff reconciliation'` (it exists; see its swap test for the shape: a VM provisioned for job 1 whose runner the listing shows on job 2):

```powershell
    It 'forces a handed VM off when the job that took its runner comes from another repository, and leaves its own job queued' {
        $world.Queued = @((New-QueuedJob 1 10 'model'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $name = @($journal.Vms.Keys)[0]
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        $journal.Vms[$name].Phase | Should -Be 'handed'
        Set-Run 99 'pull_request' 'feature' 'someone/slate'
        $world.InProgress = @([pscustomobject]@{ id = 2; run_id = 99; status = 'in_progress'; labels = @('slate-win-model'); created_at = $t0.ToString('o'); runner_name = $name })
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(20)
        (Get-Calls 'StopVmForce').Count | Should -Be 1
        $journal.Vms.ContainsKey($name) | Should -BeFalse
        $journal.SeenJobs.ContainsKey('1') | Should -BeTrue
        $journal.Retries.ContainsKey('1') | Should -BeFalse
        ($logs -join "`n") | Should -Match "handoff: ${name} runs job 2 of someone/slate \(not the trusted repository\); forcing it off"
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1`
Expected: the four new tests and the extended first admission test fail (five); everything else passes (the default trusted run keeps the old tests green).

- [ ] **Step 3: Implement**

In `Invoke-Admission`, replace from `$queued = @($laneJobs | …)` through the end of the `foreach ($q in $queued) { … }` loop that records seen jobs (the reconciliation loop below it stays, and is edited afterwards) with:

```powershell
    $queued = @($laneJobs | Where-Object { $_.Status -eq 'queued' })
    # Only this repository's runs get a VM (overflow spec OR-D5): a fork
    # pull request runs its own workflow file and can name the home label,
    # so the check lives here, where the fork cannot edit it. Each job's run
    # is read once and remembered; a read that fails is retried once a
    # minute and logged on its first failure, and the job waits meanwhile.
    $admissible = @()
    foreach ($q in $queued) {
        $key = [string]$q.JobId
        if (-not $Journal.SeenJobs.Contains($key)) {
            $Journal.SeenJobs[$key] = @{ RunId = [int64]$q.RunId; Lane = $q.Lane; FirstSeenAt = $Now.ToString('o'); Foreign = $null; RunCheckedAt = $null }
        }
        $seen = $Journal.SeenJobs[$key]
        if (-not $seen.Contains('Foreign')) { $seen['Foreign'] = $null; $seen['RunCheckedAt'] = $null }
        if ($null -eq $seen['Foreign']) {
            $checked = $seen['RunCheckedAt']
            if ($checked -and ($Now - (ConvertTo-DateTimeOffset -Value $checked)).TotalSeconds -lt 60) { continue }
            $seen['RunCheckedAt'] = $Now.ToString('o')
            try {
                $run = & $Adapters.GetRun ([int64]$q.RunId)
                $seen['Foreign'] = -not (Test-TrustedRun -Run $run -TrustedRepo $Config.TrustedRepo)
            } catch {
                if (-not $checked) { & $Adapters.Log 'warn' "admission: job $($q.JobId): run $($q.RunId) could not be read: $_" }
                continue
            }
            if ($seen['Foreign']) {
                & $Adapters.Log 'warn' "admission: skipped job $($q.JobId) of $(Get-RunRepository -Run $run) (not the trusted repository)"
            }
        }
        if ($seen['Foreign'] -eq $false) { $admissible += $q }
    }
```

(One `GetRun` per job: `Get-GhRun` is not a conditional request, so a second read would be a second counted call.) Then two small pure functions, next to `Test-CommitEligible`:

```powershell
function Get-RunRepository {
    param($Run)
    if ($null -eq $Run -or -not $Run.PSObject.Properties['head_repository'] -or $null -eq $Run.head_repository) { return '' }
    return [string]$Run.head_repository.full_name
}

function Test-TrustedRun {
    # A null head repository (a deleted fork) is not trusted.
    param($Run, [Parameter(Mandatory)][string]$TrustedRepo)
    $repo = Get-RunRepository -Run $Run
    return [bool]$repo -and [string]::Equals($repo, $TrustedRepo, [System.StringComparison]::OrdinalIgnoreCase)
}
```

In the reconciliation loop, where a handed VM's runner is found on another job (`if ([int64]$job.JobId -ne [int64]$vm.JobId) {`), before adopting it:

```powershell
            $takenRun = $null
            try { $takenRun = & $Adapters.GetRun ([int64]$job.RunId) } catch { & $Adapters.Log 'warn' "handoff: ${name}: run $($job.RunId) could not be read: $_"; continue }
            if (-not (Test-TrustedRun -Run $takenRun -TrustedRepo $Config.TrustedRepo)) {
                & $Adapters.Log 'warn' "handoff: ${name} runs job $($job.JobId) of $(Get-RunRepository -Run $takenRun) (not the trusted repository); forcing it off"
                Remove-ActiveVm -Config $Config -Journal $Journal -Adapters $Adapters -Name $name -Reason "runner taken by job $($job.JobId) of another repository" -Retry $false -Now $Now
                continue
            }
```

and the `SeenJobs` entry it creates for the adopted job carries `Foreign = $false; RunCheckedAt = $Now.ToString('o')` (the run was just read and is trusted). Change the two `Select-JobsToAdmit` calls (admission and the published queue) to pass `-Candidates $admissible`. Export `Get-RunRepository, Test-TrustedRun`.

- [ ] **Step 4: Run both suites to verify they pass**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1`
Expected: all green.

- [ ] **Step 5: Commit**

```bash
git add ci/windows-runner/SlateCiHost.psm1 ci/windows-runner/tests/Orchestrator.Tests.ps1
git commit -m "fix(ci-host): never provision for, and evict within a tick, a job of another repository"
```

---

### Task 5: The host prunes its logs (OR-D7)

**Files:**
- Modify: `ci/windows-runner/SlateCiHost.psm1` (`$script:JournalDefaults` gains `LogsPrunedOn`; `Get-CiHostConfig`; new `Get-ExpiredLogNames`, `Invoke-LogPrune`; `Invoke-OrchestratorTick`; `Invoke-StartupSweep`; exports)
- Modify: `ci/windows-runner/adapters/HyperV.ps1` (`Get-OrchestratorLogNames`, `Remove-OrchestratorLog`, adapters `ListLogs`, `RemoveLog`; the header comment now reads "the only file that touches Hyper-V and the host's own files")
- Modify: `ci/windows-runner/config.json` (`"LogRetentionDays": 30`)
- Modify: `ci/windows-runner/tests/SlateCiHost.Tests.ps1`, `ci/windows-runner/tests/Orchestrator.Tests.ps1`, `ci/windows-runner/tests/HyperVAdapter.Tests.ps1`

**Interfaces:**
- Produces: `Get-ExpiredLogNames -Names -Today -RetentionDays -> string[]`; `Invoke-LogPrune -Config -Journal -Adapters -Today`; journal key `LogsPrunedOn`; config key `LogRetentionDays` (default 30); adapters `ListLogs`, `RemoveLog <name>`.

- [ ] **Step 1: Write the failing tests**

`SlateCiHost.Tests.ps1`: the expected list of Task 3's journal shape test becomes `@('LogsPrunedOn', 'PublishFailedAt', 'PublishedAt', 'PublishedState', 'Retries', 'SeenJobs', 'Vms' | Sort-Object)`, and add:

```powershell
Describe 'Get-ExpiredLogNames' {
    It 'selects dated log names older than the retention, by the date in the name' {
        $names = 'orchestrator-2026-09-10.log', 'orchestrator-2026-09-11.log', 'orchestrator-2026-09-12.log', 'orchestrator-2026-10-11.log'
        @(Get-ExpiredLogNames -Names $names -Today '2026-10-11' -RetentionDays 30) | Should -Be @('orchestrator-2026-09-10.log')
    }
    It 'keeps today, names that do not parse and other files' {
        $names = 'orchestrator-2026-10-11.log', 'orchestrator-yesterday.log', 'notes.txt', 'orchestrator-2026-13-40.log'
        @(Get-ExpiredLogNames -Names $names -Today '2026-10-11' -RetentionDays 1).Count | Should -Be 0
    }
    It 'returns nothing for an empty directory' {
        @(Get-ExpiredLogNames -Names @() -Today '2026-10-11' -RetentionDays 30).Count | Should -Be 0
    }
}
```

In the `Get-CiHostConfig` describe:

```powershell
    It 'defaults LogRetentionDays to 30 when the file predates the key, and rejects a bad value' {
        $cfg = Get-Content -Raw (Join-Path $PSScriptRoot '..' 'config.json') | ConvertFrom-Json -AsHashtable
        $cfg.Remove('LogRetentionDays')
        $p = Join-Path $TestDrive 'old.json'; $cfg | ConvertTo-Json -Depth 5 | Set-Content $p
        (Get-CiHostConfig -Path $p).LogRetentionDays | Should -Be 30
        $cfg['LogRetentionDays'] = 0
        $cfg | ConvertTo-Json -Depth 5 | Set-Content $p
        { Get-CiHostConfig -Path $p } | Should -Throw '*LogRetentionDays*'
    }
```

`Orchestrator.Tests.ps1`: in `New-World` add `Logs = @('orchestrator-2026-09-01.log', 'orchestrator-2026-10-10.log')`; in `New-FakeAdapters` add

```powershell
            ListLogs      = { & $script:record 'ListLogs' @(); @($script:world.Logs) }
            RemoveLog     = { param($name) & $script:record 'RemoveLog' @($name); $script:world.Logs = @($script:world.Logs | Where-Object { $_ -ne $name }) }
```

and a context:

```powershell
Context 'log retention' {
    It 'prunes once per local day, at the first tick, and remembers the day in the journal' {
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        (Get-Calls 'RemoveLog').Count | Should -Be 1
        (Get-Calls 'RemoveLog')[0].Args[0] | Should -Be 'orchestrator-2026-09-01.log'
        $journal.LogsPrunedOn | Should -Be $t0.ToLocalTime().ToString('yyyy-MM-dd', [cultureinfo]::InvariantCulture)
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(5)
        (Get-Calls 'ListLogs').Count | Should -Be 1
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddDays(1)
        (Get-Calls 'ListLogs').Count | Should -Be 2
    }
    It 'logs and continues when a delete fails' {
        $adapters.RemoveLog = { param($name) throw 'in use' }
        { Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0 } | Should -Not -Throw
        ($logs -join "`n") | Should -Match 'logs: orchestrator-2026-09-01.log: in use'
        $journal.LogsPrunedOn | Should -Not -Be $null
    }
    It 'the startup sweep forgets the day so the first tick prunes again' {
        $journal.LogsPrunedOn = '2026-10-10'
        Invoke-StartupSweep -Config $config -Journal $journal -Adapters $adapters
        $journal.LogsPrunedOn | Should -Be $null
    }
}
```

`HyperVAdapter.Tests.ps1`: extend the exact key list at line 252 with `'ListLogs'`, `'RemoveLog'` (sorted), and add:

```powershell
Describe 'log adapters' {
    It 'list only dated orchestrator logs and delete by bare name inside LogDir' {
        $logDir = Join-Path $TestDrive 'logs'; New-Item -ItemType Directory -Force $logDir | Out-Null
        'x' | Set-Content (Join-Path $logDir 'orchestrator-2026-09-01.log')
        'x' | Set-Content (Join-Path $logDir 'notes.txt')
        @(Get-OrchestratorLogNames -LogDir $logDir) | Should -Be @('orchestrator-2026-09-01.log')
        Remove-OrchestratorLog -LogDir $logDir -Name 'orchestrator-2026-09-01.log'
        Test-Path (Join-Path $logDir 'orchestrator-2026-09-01.log') | Should -BeFalse
        { Remove-OrchestratorLog -LogDir $logDir -Name '..\notes.txt' } | Should -Throw
        @(Get-OrchestratorLogNames -LogDir (Join-Path $TestDrive 'none')).Count | Should -Be 0
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1`
Expected: the new tests fail; the shape test fails on the missing key.

- [ ] **Step 3: Implement**

`config.json`: after `"RetryBackoffSeconds": 600,` add `"LogRetentionDays": 30,`. `$script:JournalDefaults` gains `LogsPrunedOn = $null`. `Get-CiHostConfig`, after the `RoutedWorkflows` validation:

```powershell
    # Older config files predate the key: default it rather than crash-loop
    # a host installed before it existed.
    if (-not $config.Contains('LogRetentionDays')) { $config['LogRetentionDays'] = 30 }
    $days = $config.LogRetentionDays
    if (-not ($days -is [int] -or $days -is [int64] -or $days -is [double]) -or [double]$days -ne [math]::Floor([double]$days) -or [int]$days -lt 1) {
        throw "config ${Path}: LogRetentionDays must be a whole number of days, 1 or more"
    }
    $config['LogRetentionDays'] = [int]$days
```

New functions after `Write-CiLog`:

```powershell
function Get-ExpiredLogNames {
    # Dated orchestrator logs older than the retention, by the local date in
    # the name (orchestrator.ps1 names them with the local date). Names that
    # do not parse are kept: a human can decide about those.
    [CmdletBinding()]
    param([string[]]$Names = @(), [Parameter(Mandatory)][string]$Today, [Parameter(Mandatory)][int]$RetentionDays)
    $cutoff = [datetime]::ParseExact($Today, 'yyyy-MM-dd', [cultureinfo]::InvariantCulture).AddDays(-$RetentionDays)
    $expired = @()
    foreach ($name in @($Names)) {
        if ($name -notmatch '^orchestrator-(\d{4}-\d{2}-\d{2})\.log$') { continue }
        $date = [datetime]::MinValue
        if (-not [datetime]::TryParseExact($Matches[1], 'yyyy-MM-dd', [cultureinfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::None, [ref]$date)) { continue }
        if ($date -lt $cutoff) { $expired += $name }
    }
    return @($expired)
}

function Invoke-LogPrune {
    # Once per local day (journal LogsPrunedOn), and at the first tick after
    # a start (the sweep clears the day). A delete that fails is logged and
    # the day still counts as done: the file is tried again tomorrow.
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Config, [Parameter(Mandatory)]$Journal, [Parameter(Mandatory)][hashtable]$Adapters, [Parameter(Mandatory)][string]$Today)
    if ([string]$Journal['LogsPrunedOn'] -eq $Today) { return }
    $names = @(& $Adapters.ListLogs)
    foreach ($name in @(Get-ExpiredLogNames -Names $names -Today $Today -RetentionDays ([int]$Config.LogRetentionDays))) {
        try { & $Adapters.RemoveLog $name; & $Adapters.Log 'info' "logs: removed $name" }
        catch { & $Adapters.Log 'warn' "logs: ${name}: $_" }
    }
    $Journal['LogsPrunedOn'] = $Today
}
```

In `Invoke-OrchestratorTick`, first thing:

```powershell
    try { Invoke-LogPrune -Config $Config -Journal $Journal -Adapters $Adapters -Today $Now.ToLocalTime().ToString('yyyy-MM-dd', [cultureinfo]::InvariantCulture) }
    catch { & $Adapters.Log 'error' "logs: $_" }
```

(The `log retention` test's expected `LogsPrunedOn` uses the same invariant format.) In `Invoke-StartupSweep`, as its last line: `$Journal['LogsPrunedOn'] = $null`. Export `Get-ExpiredLogNames, Invoke-LogPrune`.

`adapters/HyperV.ps1`, before `New-HyperVAdapters`:

```powershell
function Get-OrchestratorLogNames {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$LogDir)
    if (-not (Test-Path -LiteralPath $LogDir)) { return @() }
    return @(Get-ChildItem -LiteralPath $LogDir -File -Filter 'orchestrator-*.log' | ForEach-Object { $_.Name })
}

function Remove-OrchestratorLog {
    # By bare name only, inside LogDir: a name with either path separator is
    # refused (on Linux GetFileName keeps a backslash, so both are checked).
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$LogDir, [Parameter(Mandatory)][string]$Name)
    $ErrorActionPreference = 'Stop'
    if ($Name -match '[\\/]' -or $Name -ne [System.IO.Path]::GetFileName($Name)) { throw "log name '$Name' is not a bare file name" }
    Remove-Item -LiteralPath (Join-Path $LogDir $Name) -Force
}
```

add both names to `New-HyperVAdapters`'s command list and

```powershell
        ListLogs      = { & $commands['Get-OrchestratorLogNames'] -LogDir $Config.LogDir }
        RemoveLog     = { param($name) & $commands['Remove-OrchestratorLog'] -LogDir $Config.LogDir -Name $name }
```

- [ ] **Step 4: Run both suites to verify they pass**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1`
Expected: all green.

- [ ] **Step 5: Commit**

```bash
git add ci/windows-runner/SlateCiHost.psm1 ci/windows-runner/adapters/HyperV.ps1 ci/windows-runner/config.json ci/windows-runner/tests
git commit -m "feat(ci-host): prune orchestrator logs older than LogRetentionDays once a day"
```

---

### Task 6: Golden acceptance records facts, and one predicate decides deletion (OR-D7)

**Files:**
- Modify: `ci/windows-runner/SlateCiHost.psm1` (`$script:JournalDefaults` gains `GoldenAccepted`; new `$script:AcceptanceLanes`, `Register-GoldenAcceptance`, `Test-GoldenPrevRemovable`; `Complete-ActiveVm`; exports)
- Modify: `ci/windows-runner/adapters/HyperV.ps1` (`Get-GoldenStamp`, `Get-GoldenSealed`, `Get-VmCreationTimes`; adapters `GetGoldenStamp`, `GetGoldenSealed`, `GetVmCreationTimes`)
- Modify: `ci/windows-runner/tests/Orchestrator.Tests.ps1`, `ci/windows-runner/tests/SlateCiHost.Tests.ps1`, `ci/windows-runner/tests/HyperVAdapter.Tests.ps1`

**Interfaces:**
- Produces: journal key `GoldenAccepted` = `$null` or `@{ Golden = <ISO UTC stamp>; Lanes = @(...); Complete = $bool }`; `$script:AcceptanceLanes = 'rust', 'app', 'model', 'shell'`; `Register-GoldenAcceptance -Journal -Adapters -Vm -Now`; `Test-GoldenPrevRemovable -Accepted -CurrentStamp -CurrentSealed -VmCreationTimes [-Lanes] -> bool`; adapters `GetGoldenStamp` (ISO UTC `o` of `LastWriteTimeUtc`, `$null` when absent), `GetGoldenSealed` (`IsReadOnly`), `GetVmCreationTimes` (ISO UTC `o` of every directory's `CreationTimeUtc` under `VmDir`); the log line `golden: rust, app, model and shell are green on the current image`.
- Consumed by Task 7's prune script (the predicate and the journal key).

- [ ] **Step 1: Write the failing tests**

`Orchestrator.Tests.ps1`: in `New-World` add `GoldenStamp = '2026-10-10T11:00:00Z'` (one hour BEFORE `$t0`, so VMs the tests provision at `$t0` are newer than the golden), `GoldenSealed = $true`, `VmTimes = @()`; in `New-FakeAdapters` add

```powershell
            GetGoldenStamp     = { & $script:record 'GetGoldenStamp' @(); $script:world.GoldenStamp }
            GetGoldenSealed    = { & $script:record 'GetGoldenSealed' @(); $script:world.GoldenSealed }
            GetVmCreationTimes = { & $script:record 'GetVmCreationTimes' @(); @($script:world.VmTimes) }
```

and a context whose helper lives in `BeforeAll` (Pester 5 runs a Context body at discovery only):

```powershell
Context 'golden acceptance' {
    BeforeAll {
        function Complete-GreenJob([string]$Lane, [datetimeoffset]$At, [string]$Conclusion = 'success', [string]$Event = 'push') {
            $script:jobId++
            $runId = $script:jobId * 10
            $script:world.Queued = @((New-QueuedJob $script:jobId $runId $Lane))
            Set-Run $runId $Event $(if ($Event -eq 'push') { 'main' } else { 'feature' })
            Invoke-OrchestratorTick -Config $script:config -Journal $script:journal -Adapters $script:adapters -Now $At
            $name = @($script:journal.Vms.Keys | Where-Object { $script:journal.Vms[$_].JobId -eq $script:jobId })[0]
            Invoke-OrchestratorTick -Config $script:config -Journal $script:journal -Adapters $script:adapters -Now $At.AddSeconds(10)
            $script:world.Queued = @()
            $script:world.VmStates[$name] = 'Off'
            Set-DoneJob $script:jobId $runId $name $Conclusion
            Invoke-OrchestratorTick -Config $script:config -Journal $script:journal -Adapters $script:adapters -Now $At.AddMinutes(20)
        }
    }
    BeforeEach { $script:jobId = 100 }
    It 'records each lane that went green on a VM newer than the golden, and logs once when all four did' {
        Complete-GreenJob 'rust' $t0
        $journal.GoldenAccepted.Golden | Should -Be '2026-10-10T11:00:00Z'
        @($journal.GoldenAccepted.Lanes) | Should -Be @('rust')
        $journal.GoldenAccepted.Complete | Should -BeFalse
        Complete-GreenJob 'app' $t0.AddHours(1)
        Complete-GreenJob 'rust' $t0.AddHours(2)
        Complete-GreenJob 'model' $t0.AddHours(3)
        @($journal.GoldenAccepted.Lanes | Sort-Object) | Should -Be @('app', 'model', 'rust')
        $journal.GoldenAccepted.Complete | Should -BeFalse
        Complete-GreenJob 'shell' $t0.AddHours(4)
        $journal.GoldenAccepted.Complete | Should -BeTrue
        ([regex]::Matches(($logs -join "`n"), 'golden: rust, app, model and shell are green on the current image')).Count | Should -Be 1
        Complete-GreenJob 'app' $t0.AddHours(5)
        ([regex]::Matches(($logs -join "`n"), 'green on the current image')).Count | Should -Be 1
    }
    It 'survives the journal being written and read back between two green jobs' {
        Complete-GreenJob 'rust' $t0
        $p = Join-Path $TestDrive 'journal.json'
        Write-Journal -Path $p -Journal $journal
        $script:journal = Read-Journal -Path $p
        Complete-GreenJob 'app' $t0.AddHours(1)
        @($journal.GoldenAccepted.Lanes | Sort-Object) | Should -Be @('app', 'rust')
    }
    It 'ignores a green job on a VM provisioned before the golden was sealed' {
        $world.GoldenStamp = $t0.AddMinutes(5).ToString('o')
        Complete-GreenJob 'rust' $t0
        @($journal.GoldenAccepted.Lanes).Count | Should -Be 0
    }
    It 'starts over when the golden changes' {
        Complete-GreenJob 'rust' $t0
        $world.GoldenStamp = '2026-10-12T01:00:00Z'
        Complete-GreenJob 'rust' $t0.AddDays(2)
        $journal.GoldenAccepted.Golden | Should -Be '2026-10-12T01:00:00Z'
        @($journal.GoldenAccepted.Lanes) | Should -Be @('rust')
    }
    It 'records nothing for a red job, and records a green pull-request job (the image ran it, whatever the cache did)' {
        Complete-GreenJob 'app' $t0 'failure'
        $journal.GoldenAccepted | Should -Be $null
        Complete-GreenJob 'app' $t0.AddHours(1) 'success' 'pull_request'
        @($journal.GoldenAccepted.Lanes) | Should -Be @('app')
    }
    It 'a failing stamp read never wedges the settle' {
        $adapters.GetGoldenStamp = { throw 'disk gone' }
        Complete-GreenJob 'rust' $t0
        $journal.Vms.Count | Should -Be 0
        ($logs -join "`n") | Should -Match 'golden: disk gone'
    }
}
```

`SlateCiHost.Tests.ps1`: the journal shape test's expected list becomes `@('GoldenAccepted', 'LogsPrunedOn', 'PublishFailedAt', 'PublishedAt', 'PublishedState', 'Retries', 'SeenJobs', 'Vms' | Sort-Object)`, and add:

```powershell
Describe 'Test-GoldenPrevRemovable' {
    BeforeAll { $script:accepted = @{ Golden = '2026-10-10T22:55:00.0000000Z'; Lanes = @('rust', 'app', 'model', 'shell'); Complete = $true } }
    It 'is true only for a complete acceptance of the current, sealed golden with no older VM' {
        Test-GoldenPrevRemovable -Accepted $accepted -CurrentStamp '2026-10-10T22:55:00.0000000Z' -CurrentSealed $true -VmCreationTimes @('2026-10-11T01:00:00Z') | Should -BeTrue
    }
    It 'compares the stamps as instants, so a journal round trip (a [datetime]) still matches' {
        $roundTripped = @{ Golden = [datetime]::Parse('2026-10-10T22:55:00Z').ToUniversalTime(); Lanes = $accepted.Lanes; Complete = $true }
        Test-GoldenPrevRemovable -Accepted $roundTripped -CurrentStamp '2026-10-10T22:55:00.0000000Z' -CurrentSealed $true -VmCreationTimes @() | Should -BeTrue
    }
    It 'is false for an acceptance of another image (the previous one, after a rotation)' {
        Test-GoldenPrevRemovable -Accepted $accepted -CurrentStamp '2026-10-12T01:00:00Z' -CurrentSealed $true -VmCreationTimes @() | Should -BeFalse
    }
    It 'is false while the current golden is not sealed, for a missing lane, for a VM older than the golden, and without an acceptance' {
        Test-GoldenPrevRemovable -Accepted $accepted -CurrentStamp '2026-10-10T22:55:00.0000000Z' -CurrentSealed $false -VmCreationTimes @() | Should -BeFalse
        Test-GoldenPrevRemovable -Accepted @{ Golden = $accepted.Golden; Lanes = @('rust', 'app', 'model'); Complete = $false } -CurrentStamp $accepted.Golden -CurrentSealed $true -VmCreationTimes @() | Should -BeFalse
        Test-GoldenPrevRemovable -Accepted $accepted -CurrentStamp $accepted.Golden -CurrentSealed $true -VmCreationTimes @('2026-10-10T22:00:00Z') | Should -BeFalse
        Test-GoldenPrevRemovable -Accepted $null -CurrentStamp $accepted.Golden -CurrentSealed $true -VmCreationTimes @() | Should -BeFalse
    }
}
```

`HyperVAdapter.Tests.ps1`: extend the exact key list with `'GetGoldenSealed'`, `'GetGoldenStamp'`, `'GetVmCreationTimes'` (sorted), and add:

```powershell
Describe 'golden adapters' {
    It 'read the golden stamp and seal and every VM directory creation time as ISO UTC' -Skip:(-not $IsWindows) {
        # Windows only: the test sets directory creation times, which Linux cannot.
        $golden = Join-Path $TestDrive 'golden.vhdx'; 'g' | Set-Content $golden
        (Get-Item $golden).LastWriteTimeUtc = [datetime]'2026-10-10T22:55:00Z'
        Get-GoldenStamp -GoldenPath $golden | Should -Be '2026-10-10T22:55:00.0000000Z'
        Get-GoldenSealed -GoldenPath $golden | Should -BeFalse
        Set-ItemProperty -LiteralPath $golden -Name IsReadOnly -Value $true
        Get-GoldenSealed -GoldenPath $golden | Should -BeTrue
        Get-GoldenStamp -GoldenPath (Join-Path $TestDrive 'none.vhdx') | Should -Be $null
        $vmDir = Join-Path $TestDrive 'vms'; New-Item -ItemType Directory -Force (Join-Path $vmDir 'a'), (Join-Path $vmDir 'b') | Out-Null
        (Get-Item (Join-Path $vmDir 'a')).CreationTimeUtc = [datetime]'2026-10-11T01:00:00Z'
        (Get-Item (Join-Path $vmDir 'b')).CreationTimeUtc = [datetime]'2026-10-11T02:00:00Z'
        @(Get-VmCreationTimes -VmDir $vmDir | Sort-Object) | Should -Be @('2026-10-11T01:00:00.0000000Z', '2026-10-11T02:00:00.0000000Z')
        @(Get-VmCreationTimes -VmDir (Join-Path $TestDrive 'none')).Count | Should -Be 0
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1`
Expected: the new contexts fail (`GoldenAccepted` absent, no adapter, no predicate).

- [ ] **Step 3: Implement**

`$script:JournalDefaults` gains `GoldenAccepted = $null`. After `Test-SiblingRunning`:

```powershell
$script:AcceptanceLanes = 'rust', 'app', 'model', 'shell'

function Register-GoldenAcceptance {
    # Facts only (overflow spec OR-D7): a green job on a VM provisioned after
    # the golden's seal vouches for its lane on that image. A new seal starts
    # over. Stamps are compared as instants: a journal round trip turns them
    # into [datetime]. Deletion is decided elsewhere, by Test-GoldenPrevRemovable.
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Journal, [Parameter(Mandatory)][hashtable]$Adapters, [Parameter(Mandatory)]$Vm, [Parameter(Mandatory)][datetimeoffset]$Now)
    $stamp = & $Adapters.GetGoldenStamp
    if (-not $stamp) { return }
    $sealed = ConvertTo-DateTimeOffset -Value $stamp
    $accepted = $Journal['GoldenAccepted']
    if ($null -eq $accepted -or (ConvertTo-DateTimeOffset -Value $accepted['Golden']) -ne $sealed) {
        $accepted = @{ Golden = [string]$stamp; Lanes = @(); Complete = $false }
        $Journal['GoldenAccepted'] = $accepted
    }
    if ((ConvertTo-DateTimeOffset -Value $Vm.StartedAt) -le $sealed) { return }
    if (@($accepted['Lanes']) -notcontains [string]$Vm.Lane) { $accepted['Lanes'] = @(@($accepted['Lanes']) + [string]$Vm.Lane) }
    if ($accepted['Complete']) { return }
    foreach ($lane in $script:AcceptanceLanes) { if (@($accepted['Lanes']) -notcontains $lane) { return } }
    $accepted['Complete'] = $true
    & $Adapters.Log 'info' "golden: $($script:AcceptanceLanes[0..2] -join ', ') and $($script:AcceptanceLanes[3]) are green on the current image"
}

function Test-GoldenPrevRemovable {
    # The one statement of when the previous image may go: the acceptance is
    # of THIS image (stamps equal as instants), this image is sealed, every
    # lane vouched, and no VM directory predates the seal.
    [CmdletBinding()]
    param($Accepted, [AllowNull()][string]$CurrentStamp, [bool]$CurrentSealed, [string[]]$VmCreationTimes = @(), [string[]]$Lanes = $script:AcceptanceLanes)
    if ($null -eq $Accepted -or -not $CurrentStamp -or -not $CurrentSealed) { return $false }
    if ($null -eq $Accepted['Golden']) { return $false }
    $sealed = ConvertTo-DateTimeOffset -Value $CurrentStamp
    if ((ConvertTo-DateTimeOffset -Value $Accepted['Golden']) -ne $sealed) { return $false }
    foreach ($lane in $Lanes) { if (@($Accepted['Lanes']) -notcontains $lane) { return $false } }
    foreach ($created in @($VmCreationTimes)) { if ((ConvertTo-DateTimeOffset -Value $created) -le $sealed) { return $false } }
    return $true
}
```

In `Complete-ActiveVm`, in the `PendingCommit` branch right after the commit's log line, and in the main path right before `$run = $null` (the job is `completed` there), add:

```powershell
    if ($null -ne $job -and [string]$job.conclusion -eq 'success') {
        try { Register-GoldenAcceptance -Journal $Journal -Adapters $Adapters -Vm $vm -Now $Now } catch { & $Adapters.Log 'warn' "golden: $_" }
    }
```

(In the `PendingCommit` branch there is no `$job` in scope: the job was green or the commit would not be pending, so call it unconditionally there.) Export `Register-GoldenAcceptance, Test-GoldenPrevRemovable`.

`adapters/HyperV.ps1`, before `New-HyperVAdapters`:

```powershell
function Get-GoldenStamp {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$GoldenPath)
    if (-not (Test-Path -LiteralPath $GoldenPath)) { return $null }
    return (Get-Item -LiteralPath $GoldenPath).LastWriteTimeUtc.ToString('o')
}

function Get-GoldenSealed {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$GoldenPath)
    if (-not (Test-Path -LiteralPath $GoldenPath)) { return $false }
    return [bool](Get-Item -LiteralPath $GoldenPath).IsReadOnly
}

function Get-VmCreationTimes {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$VmDir)
    if (-not (Test-Path -LiteralPath $VmDir)) { return @() }
    return @(Get-ChildItem -LiteralPath $VmDir -Directory | ForEach-Object { $_.CreationTimeUtc.ToString('o') })
}
```

registered in the command list, with adapters `GetGoldenStamp = { & $commands['Get-GoldenStamp'] -GoldenPath $Config.GoldenPath }`, `GetGoldenSealed = { & $commands['Get-GoldenSealed'] -GoldenPath $Config.GoldenPath }`, `GetVmCreationTimes = { & $commands['Get-VmCreationTimes'] -VmDir $Config.VmDir }`.

- [ ] **Step 4: Run both suites to verify they pass**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1`
Expected: all green, including the round-trip test.

- [ ] **Step 5: Commit**

```bash
git add ci/windows-runner/SlateCiHost.psm1 ci/windows-runner/adapters/HyperV.ps1 ci/windows-runner/tests
git commit -m "feat(ci-host): record which lanes went green on the current golden, and one predicate for when the previous image may go"
```

---

### Task 7: The elevated prune task and `build-golden.ps1 -Rotate` (OR-D7)

**Files:**
- Modify: `ci/windows-runner/SlateCiHost.psm1` (new `Get-CiHostPaths`, `Read-JournalSnapshot`, `Remove-SealedFile`; `Get-CiHostConfig` derives its paths from `Get-CiHostPaths`; exports)
- Create: `ci/windows-runner/install/prune-golden.ps1`
- Modify: `ci/windows-runner/install/setup-host.ps1` (register `slate-ci-golden-prune` on every run; the closing message)
- Modify: `ci/windows-runner/golden/build-golden.ps1` (`-Rotate`)
- Modify: `ci/windows-runner/tests/SlateCiHost.Tests.ps1`, `ci/windows-runner/tests/Install.Tests.ps1`, `ci/windows-runner/tests/Golden.Tests.ps1`

**Interfaces:**
- Consumes: Task 6's `GoldenAccepted`, `Test-GoldenPrevRemovable`, `Get-GoldenStamp`, `Get-GoldenSealed`, `Get-VmCreationTimes`.
- Produces: `Get-CiHostPaths -Root -> @{ GoldenPath; GoldenPrevPath; CacheDir; VmDir; StateDir; LogDir; JournalPath }` (the one statement of the tree); `Read-JournalSnapshot -Path` (a read with no side effects: a missing or unparseable file reads as a new journal, nothing is moved); `Remove-SealedFile -Path` (clears the read-only bit, deletes); `prune-golden.ps1 -Root` (exit 0 always); the task `slate-ci-golden-prune` (SYSTEM, daily 04:00, 10-minute limit); `build-golden.ps1 -Rotate`.

- [ ] **Step 1: Write the failing tests**

`SlateCiHost.Tests.ps1`:

```powershell
Describe 'Get-CiHostPaths and Read-JournalSnapshot' {
    It 'derives every path of the tree from the root, and Get-CiHostConfig uses them' {
        $paths = Get-CiHostPaths -Root 'C:\slate-ci'
        $paths.GoldenPath | Should -Be 'C:\slate-ci\golden\win11-runner.vhdx'
        $paths.GoldenPrevPath | Should -Be 'C:\slate-ci\golden\win11-runner.prev.vhdx'
        $paths.JournalPath | Should -Be 'C:\slate-ci\state\journal.json'
        $paths.LogDir | Should -Be 'C:\slate-ci\logs'
        $cfg = Get-CiHostConfig -Path (Join-Path $PSScriptRoot '..' 'config.json')
        foreach ($key in 'GoldenPath', 'GoldenPrevPath', 'CacheDir', 'VmDir', 'StateDir', 'LogDir', 'JournalPath') { $cfg[$key] | Should -Be $paths[$key] }
    }
    It 'a snapshot reads a journal without moving anything, and reads a missing or broken file as a new journal' {
        $p = Join-Path $TestDrive 'j.json'
        $j = New-Journal; $j.GoldenAccepted = @{ Golden = '2026-10-10T22:55:00Z'; Lanes = @('rust'); Complete = $false }
        Write-Journal -Path $p -Journal $j
        (Read-JournalSnapshot -Path $p).GoldenAccepted.Lanes | Should -Be @('rust')
        'not json' | Set-Content $p
        (Read-JournalSnapshot -Path $p).GoldenAccepted | Should -Be $null
        Test-Path $p | Should -BeTrue -Because 'a snapshot never moves the loop''s journal aside'
        @(Get-ChildItem $TestDrive -Filter 'j.json.corrupt-*').Count | Should -Be 0
        (Read-JournalSnapshot -Path (Join-Path $TestDrive 'none.json')).Vms.Count | Should -Be 0
    }
    It 'Remove-SealedFile deletes a read-only file and tolerates a missing one' {
        $f = Join-Path $TestDrive 'sealed.bin'; 'x' | Set-Content $f
        Set-ItemProperty -LiteralPath $f -Name IsReadOnly -Value $true
        Remove-SealedFile -Path $f
        Test-Path $f | Should -BeFalse
        { Remove-SealedFile -Path $f } | Should -Not -Throw
    }
}
```

`Install.Tests.ps1`, in the `install scripts` describe:

```powershell
    It 'setup registers the elevated prune task for the previous golden image, daily, as SYSTEM, on every run and outside the password-bound pair' {
        $text = Get-Content -Raw (Join-Path $installDir 'setup-host.ps1')
        $start = $text.IndexOf("Register-ScheduledTask -TaskName 'slate-ci-golden-prune'")
        $start | Should -BeGreaterThan -1
        $end = $text.IndexOf("-User 'NT AUTHORITY\SYSTEM'", $start)
        $end | Should -BeGreaterThan $start
        $prune = $text.Substring($start, $end - $start)
        $prune | Should -Match 'prune-golden\.ps1'
        $prune | Should -Match '-Daily -At 4am'
        $prune | Should -Not -Match '\$Account'
        $text.Substring($end, 120) | Should -Match '-RunLevel Highest'
        $text | Should -Match ([regex]::Escape("`$taskNames = @('slate-ci-orchestrator', 'slate-ci-store-token')"))
        $nine = $text.IndexOf("'9/9 scheduled tasks'")
        $start | Should -BeGreaterThan $nine
        $start | Should -BeLessThan $text.IndexOf('if ($keepAccount) {', $nine)
        $text | Should -Match 'slate-ci-golden-prune once the loop has accepted'
    }
```

and a describe that runs the script against a fake tree:

```powershell
Describe 'prune-golden.ps1' {
    BeforeEach {
        # One Join-Path segment per directory: the suite also runs on Linux,
        # where a backslash inside a segment is part of the name for .NET
        # (Write-Journal's File.Move) though not for the cmdlets.
        $script:root = Join-Path $TestDrive ('root-' + [guid]::NewGuid().ToString('N'))
        foreach ($d in 'golden', 'state', 'vms', 'logs', 'bin') { New-Item -ItemType Directory -Force (Join-Path $root $d) | Out-Null }
        New-Item -ItemType Directory -Force (Join-Path $root 'bin' 'adapters') | Out-Null
        Copy-Item (Join-Path $PSScriptRoot '..' 'SlateCiHost.psm1') (Join-Path $root 'bin')
        Copy-Item (Join-Path $PSScriptRoot '..' 'adapters' 'HyperV.ps1') (Join-Path $root 'bin' 'adapters')
        $script:golden = Join-Path $root 'golden' 'win11-runner.vhdx'; 'new' | Set-Content $golden
        $script:prev = Join-Path $root 'golden' 'win11-runner.prev.vhdx'; 'old' | Set-Content $prev
        (Get-Item $prev).LastWriteTimeUtc = [datetime]'2026-10-10T20:00:00Z'
        (Get-Item $golden).LastWriteTimeUtc = [datetime]'2026-10-10T22:55:00Z'
        Set-ItemProperty -LiteralPath $prev -Name IsReadOnly -Value $true
        Set-ItemProperty -LiteralPath $golden -Name IsReadOnly -Value $true
        $script:journalPath = Join-Path $root 'state' 'journal.json'
        $script:prune = Join-Path $installDir 'prune-golden.ps1'
        function Set-Accepted([string]$Stamp, [string[]]$Lanes = @('rust', 'app', 'model', 'shell')) {
            $j = New-Journal
            $j.GoldenAccepted = @{ Golden = $Stamp; Lanes = $Lanes; Complete = ($Lanes.Count -eq 4) }
            Write-Journal -Path $script:journalPath -Journal $j
        }
        function Get-PruneLog { Get-Content -Raw (Get-ChildItem (Join-Path $root 'logs') | Select-Object -First 1).FullName }
    }
    It 'deletes the previous image once the current, sealed image is accepted on every lane' {
        Set-Accepted '2026-10-10T22:55:00.0000000Z'
        & $prune -Root $root
        Test-Path $prev | Should -BeFalse
        Test-Path $golden | Should -BeTrue
        Get-PruneLog | Should -Match 'golden: removed win11-runner.prev.vhdx'
    }
    It 'keeps it when the acceptance is of another image (the previous one, right after a rotation)' {
        Set-Accepted '2026-10-10T20:00:00.0000000Z'
        & $prune -Root $root
        Test-Path $prev | Should -BeTrue
        Get-PruneLog | Should -Match 'the previous image stays'
    }
    It 'keeps it while the current image is unsealed or a lane is missing' {
        Set-ItemProperty -LiteralPath $golden -Name IsReadOnly -Value $false
        Set-Accepted '2026-10-10T22:55:00.0000000Z'
        & $prune -Root $root
        Test-Path $prev | Should -BeTrue
        Set-ItemProperty -LiteralPath $golden -Name IsReadOnly -Value $true
        Set-Accepted '2026-10-10T22:55:00.0000000Z' @('rust', 'app', 'model')
        & $prune -Root $root
        Test-Path $prev | Should -BeTrue
    }
    It 'keeps it while a VM older than the image exists' -Skip:(-not $IsWindows) {
        # Windows only: setting a directory's creation time is a Windows
        # file-system operation (Linux reports a birth time it cannot set).
        Set-Accepted '2026-10-10T22:55:00.0000000Z'
        $old = Join-Path $root 'vms' 'slate-win-app-00000000'; New-Item -ItemType Directory -Force $old | Out-Null
        (Get-Item $old).CreationTimeUtc = [datetime]'2026-10-10T21:00:00Z'
        & $prune -Root $root
        Test-Path $prev | Should -BeTrue
    }
    It 'does nothing without a previous image, and survives a broken journal' {
        Remove-Item $prev -Force
        Set-Accepted '2026-10-10T22:55:00.0000000Z'
        { & $prune -Root $root } | Should -Not -Throw
        'broken' | Set-Content $journalPath
        'old' | Set-Content $prev
        { & $prune -Root $root } | Should -Not -Throw
        Test-Path $prev | Should -BeTrue
        Test-Path $journalPath | Should -BeTrue
    }
}
```

(The `Install.Tests.ps1` `parse as PowerShell` test gains `prune-golden.ps1`.) `Golden.Tests.ps1`, next to `the build script applies install.wim…`:

```powershell
    It 'the build script rotates only a sealed image to .prev with -Rotate, after the VM-exists check, through the shared helper' {
        $text = Get-Content -Raw (Join-Path $goldenDir 'build-golden.ps1')
        $text | Should -Match ([regex]::Escape('[switch]$Rotate'))
        $rotate = $text.IndexOf('if ($Rotate')
        # Single quotes: the test has no $vmName, and "VM $vmName exists" would search for "VM  exists" and always pass.
        $rotate | Should -BeGreaterThan $text.IndexOf('VM $vmName exists')
        $rotate | Should -BeLessThan $text.IndexOf('exists; move it aside to rebuild')
        $block = $text.Substring($rotate, $text.IndexOf('exists; move it aside to rebuild') - $rotate)
        $block | Should -Match 'IsReadOnly\) \{ throw'
        $block | Should -Match 'Remove-SealedFile -Path \$prevPath'
        $block | Should -Match 'Rename-Item -LiteralPath \$OutPath -NewName ''win11-runner\.prev\.vhdx'''
        $block | Should -Not -Match 'Remove-Item'
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1`
Expected: the new describes fail (no `Get-CiHostPaths`, no script, no rotation).

- [ ] **Step 3: The module's three helpers**

In `SlateCiHost.psm1`, before `Get-CiHostConfig`:

```powershell
function Get-CiHostPaths {
    # The one statement of the host tree: the loop, the install scripts and
    # the prune task derive every path from it.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Root)
    return @{
        GoldenPath     = Join-WinPath $Root 'golden\win11-runner.vhdx'
        GoldenPrevPath = Join-WinPath $Root 'golden\win11-runner.prev.vhdx'
        CacheDir       = Join-WinPath $Root 'cache'
        VmDir          = Join-WinPath $Root 'vms'
        StateDir       = Join-WinPath $Root 'state'
        LogDir         = Join-WinPath $Root 'logs'
        JournalPath    = Join-WinPath $Root 'state\journal.json'
    }
}
```

and in `Get-CiHostConfig` replace the five `$config['GoldenPath'] = …` … `$config['LogDir'] = …` lines with:

```powershell
    $paths = Get-CiHostPaths -Root $config.Root
    foreach ($key in $paths.Keys) { $config[$key] = $paths[$key] }
```

After `Read-Journal`:

```powershell
function Read-JournalSnapshot {
    # A reader's view (the prune task): never moves a broken journal aside,
    # never writes; a missing or unparseable file is an empty journal.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return (New-Journal) }
    try {
        $journal = Get-Content -Raw -LiteralPath $Path -ErrorAction Stop | ConvertFrom-Json -AsHashtable
        if ($null -eq $journal -or -not ($journal -is [System.Collections.IDictionary])) { return (New-Journal) }
    } catch { return (New-Journal) }
    foreach ($key in $script:JournalDefaults.Keys) {
        if (-not $journal.Contains($key)) { $journal[$key] = $(if ($script:JournalDefaults[$key] -is [hashtable]) { @{} } else { $null }) }
    }
    return $journal
}

function Remove-SealedFile {
    # A sealed image is read-only: clear the bit, then delete. Missing is fine.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)
    $ErrorActionPreference = 'Stop'
    if (-not (Test-Path -LiteralPath $Path)) { return }
    Set-ItemProperty -LiteralPath $Path -Name IsReadOnly -Value $false
    Remove-Item -LiteralPath $Path -Force
}
```

Export `Get-CiHostPaths, Read-JournalSnapshot, Remove-SealedFile`. `orchestrator.ps1` keeps working unchanged (`$config.LogDir`, `$config.StateDir`, `$config.GoldenPath` are still set).

- [ ] **Step 4: The prune script**

Create `ci/windows-runner/install/prune-golden.ps1`:

```powershell
#Requires -Version 7
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Deletes golden\win11-runner.prev.vhdx when, and only when, the module's
# Test-GoldenPrevRemovable holds for the CURRENT image: the orchestrator's
# journal records every lane green on it, it is sealed, and no VM from
# before it exists. Runs as SYSTEM from the slate-ci-golden-prune task
# (install/setup-host.ps1), daily: the orchestrator's account has read and
# execute on golden by policy. Reads the journal without side effects.
# Appends one line to the orchestrator log whenever a previous image exists
# (nothing when there is none). Never fails the task.
[CmdletBinding()]
param([string]$Root = 'C:\slate-ci')

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $Root 'bin' 'SlateCiHost.psm1') -Force
. (Join-Path $Root 'bin' 'adapters' 'HyperV.ps1')
$paths = Get-CiHostPaths -Root $Root
$log = {
    param($level, $message)
    Write-CiLog -Path (Join-Path $paths.LogDir ('orchestrator-{0}.log' -f (Get-Date -Format 'yyyy-MM-dd'))) -Level $level -Message $message
}
try {
    if (-not (Test-Path -LiteralPath $paths.GoldenPrevPath)) { exit 0 }
    $journal = Read-JournalSnapshot -Path $paths.JournalPath
    $removable = Test-GoldenPrevRemovable -Accepted $journal['GoldenAccepted'] `
        -CurrentStamp (Get-GoldenStamp -GoldenPath $paths.GoldenPath) `
        -CurrentSealed (Get-GoldenSealed -GoldenPath $paths.GoldenPath) `
        -VmCreationTimes @(Get-VmCreationTimes -VmDir $paths.VmDir)
    if (-not $removable) {
        & $log 'info' 'golden: the previous image stays (the current image is not yet accepted on every lane, is unsealed, or an older VM exists)'
        exit 0
    }
    Remove-SealedFile -Path $paths.GoldenPrevPath
    & $log 'info' 'golden: removed win11-runner.prev.vhdx (the current image is accepted on every lane)'
} catch {
    try { & $log 'error' "golden: prune failed: $_" } catch { }
}
exit 0
```

- [ ] **Step 5: Register the task on every setup run**

In `setup-host.ps1`, between `Write-Host '9/9 scheduled tasks'` and `if ($keepAccount) {`:

```powershell
# The previous golden image is deleted by an elevated task on the module's
# predicate (install/prune-golden.ps1): the account the loop runs as has
# read and execute on golden by policy (plus WDAC for Hyper-V's chain
# ACLs). Registered on every run: it holds no password, so it stays outside
# the keepAccount pair and an older install gains it on a plain re-run.
Register-ScheduledTask -TaskName 'slate-ci-golden-prune' -Force `
    -Action (New-ScheduledTaskAction -Execute $msiPwsh -Argument "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File `"$Root\bin\install\prune-golden.ps1`" -Root `"$Root`"" -WorkingDirectory "$Root\bin") `
    -Trigger (New-ScheduledTaskTrigger -Daily -At 4am) -User 'NT AUTHORITY\SYSTEM' -RunLevel Highest `
    -Settings (New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Minutes 10) -StartWhenAvailable) | Out-Null
```

and the `'Host setup complete. Next:'` branch gains, as its last `Write-Host`:

```powershell
    Write-Host '  The previous golden image is pruned daily at 04:00 by slate-ci-golden-prune once the loop has accepted the current one on every lane.'
```

(`robocopy /MIR … /XD tests` already mirrors `install\prune-golden.ps1` into `bin`.)

- [ ] **Step 6: Rotate in the build**

In `build-golden.ps1`: add `[switch]$Rotate` after `$TimeoutMinutes`; move the `VM $vmName exists` check above the output check; replace the output check with:

```powershell
if (Get-VM -Name $vmName -ErrorAction SilentlyContinue) { throw "VM $vmName exists; remove it first" }
if ($Rotate -and (Test-Path -LiteralPath $OutPath)) {
    # Only a sealed image becomes the rollback; an unsealed one is a failed
    # build's leftover and must be removed by hand first (runbook: "The
    # golden build fails"). The older rollback goes through the shared
    # helper. This is the one elevated moment in a refresh.
    if (-not (Get-Item -LiteralPath $OutPath).IsReadOnly) { throw "$OutPath is not sealed (a failed build left it); remove it first, see the runbook 'The golden build fails'" }
    $prevPath = Join-Path (Split-Path -Parent $OutPath) 'win11-runner.prev.vhdx'
    Remove-SealedFile -Path $prevPath
    Rename-Item -LiteralPath $OutPath -NewName 'win11-runner.prev.vhdx'
    Write-Host "Rotated the sealed image to $prevPath"
}
if (Test-Path -LiteralPath $OutPath) { throw "$OutPath exists; move it aside to rebuild, or pass -Rotate for a sealed image" }
```

- [ ] **Step 7: Run both suites to verify they pass**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1`
Expected: all green, the four prune scenarios included.

- [ ] **Step 8: Commit**

```bash
git add ci/windows-runner/SlateCiHost.psm1 ci/windows-runner/install ci/windows-runner/golden/build-golden.ps1 ci/windows-runner/tests
git commit -m "feat(ci-host): an elevated daily task prunes the previous golden image on the module's predicate; build-golden -Rotate rotates a sealed image"
```

---

### Task 8: Documents

**Files:**
- Modify: `docs/runbooks/self-hosted-windows-runner.md`
- Modify: `docs/superpowers/specs/2026-10-10-self-hosted-windows-runner-design.md`
- Modify: `docs/superpowers/specs/2026-10-11-windows-overflow-routing-design.md` (append "Planning deviations")

**Interfaces:** prose only; every command is one the owner can paste.

- [ ] **Step 1: "Pool switch (fallback and return)"**

Replace the section body (from `The repository variable \`WINDOWS_RUNNER_MODE\` routes` to the paragraph ending `(see "Before you start" under Install).`) with:

```markdown
The repository variable `WINDOWS_RUNNER_MODE` routes `windows.yml` and
`nightly.yml`. In `windows.yml` a routing job (`Route Windows lanes`,
`ci/windows-runner/route/route.py`) reads it together with the state this
host publishes every tick in the repository variable `WINDOWS_HOME_STATE`
(a heartbeat, whether it serves, its slots, what runs, what it has
queued), and decides a pool per lane. The shell gate runs on
GitHub-hosted `windows-latest` in every mode but `home-only`.

- `home`: home first. A lane takes a free slot; when none is free it queues
  at home only if it would still finish inside the run's critical path (the
  app lane plus the shell gate), and otherwise runs on Namespace at once.
  Lanes are considered in vendor-cost order: app build + test, model shard
  0, model shard 1, rust tests; the app lane never queues. The router's
  estimates live in `route.py`'s `DURATIONS` table (app 26 min at home,
  model 25, rust 11; the shell gate 13 on `windows-latest`), with 110 s per
  slot handover. With those figures an idle pool keeps app, shard 0 and
  rust home and pays Namespace about six minutes for shard 1; over a
  month at the 2026 run rate the rule spends about 3 900 Namespace
  minutes against about 4 500 for plain overflow and about 10 350 for
  all-Namespace, and a single push on an idle pool takes about 40 minutes
  against about 35 all-Namespace. A state older than two minutes at the
  run's creation, or one that says the host is not serving, sends every
  lane to Namespace by itself, and routing resumes as soon as the loop
  publishes again; no run has to strand to discover an outage. The nightly
  stress job queues home unconditionally.
- `home-only`: every lane queues home, as `home` did before 2026-10-11, and
  the shell gate runs home too. For validating a golden refresh and for
  fidelity runs.
- `namespace`: the 2026-07-31 arrangement, verbatim; the routing job does
  not run. The rust, app and model lanes run on the Namespace profile
  `namespace-profile-winx64-fast` (`-fast-pr` off `main`), with the cache
  tags `slate-windows-rust`, `slate-windows-app` and `slate-windows-model`.
  The shell gate and the nightly stress job run on `windows-latest`.
- Any other value, a typo or an unset variable included, means `namespace`.
  The comparison ignores case but not spaces: set the value without them.

Reading a run's routing: the routing job's step summary has the mode, the
state it read (its age at the run's creation, serving, slots, running,
queued, earlier routers), the free slots and the deadline, a row per lane
with its pool and reason, and one `Replay:` line that reproduces the
decision by hand. Each routed lane's first step prints `runner=…`, the
runner it landed on, and the aggregate gate's failure line names the
pools.

Fork pull requests never run on the pool: the router sends them to the
vendor, the host never provisions for a job whose run comes from another
repository (`admission: skipped job <id> of <repo> (not the trusted
repository)`), and a VM whose runner GitHub handed to such a job is
forced off within a tick (`handoff: <vm> runs job <id> of <repo> (not the
trusted repository); forcing it off`). Every fork pull request still waits
for your approval first (see "Before you start" under Install), and that
approval is the control: what the host adds is that a fork gets at most
seconds on a throwaway VM.

The pilot, `windows-ci-pilot.yml`, ignores the variable. Its `runner` input
picks the pool, and `home` is one of the choices. On `home`, its `cache`
input only switches the GitHub-scoped cache restore. `cache=false` is not a
cold build there: `CARGO_TARGET_DIR` and `NUGET_PACKAGES` live on the
lane's warm cache disk. A dispatch on `main` is also a trusted run, so the
host commits its cache fork like any other. Dispatch the pilot from a
branch unless you mean to warm the caches.

Fallback, when the host is down, being rebuilt or misbehaving:

```powershell
gh variable set WINDOWS_RUNNER_MODE --body namespace --repo coryj627/slate
```

Return:

```powershell
gh variable set WINDOWS_RUNNER_MODE --body home --repo coryj627/slate
```

Confirm after either flip:

```powershell
gh variable get WINDOWS_RUNNER_MODE --repo coryj627/slate
```

Jobs already queued for `slate-win-*` keep waiting for the home pool for up
to 24 hours; nothing re-routes a queued job. Flip to `namespace` first,
then cancel the run, then re-run it in full: in the run's page "Re-run
jobs → Re-run all jobs", or `gh run rerun <id>` (not `--failed`). A partial
re-run reuses the routing job's earlier answer and the lanes then take the
vendor expression, which is safe, but it cannot bring a cancelled lane
back. While a stranded lane waits, the `main` concurrency group holds later
`main` runs behind it. The flip never touches caches on either side.

The published state: `gh variable get WINDOWS_HOME_STATE` shows the last
tick's view; it moves whenever a VM or the queue changes and at least once
a minute. The owner creates it once, before the loop first runs on this
code:

```powershell
gh variable set WINDOWS_HOME_STATE --repo coryj627/slate --body '{"v":1,"at":"1970-01-01T00:00:00Z","serving":false,"slots":0,"running":[],"queued":[]}'
```
```

- [ ] **Step 2: Token, capacity, security, tasks**

In "Rotating the token" and the install step that issues the PAT (step 1 of "Install", which listed only two permissions although the token was issued with the mac controller's set), the permission list becomes "Actions: Read; Administration: Read and write; Variables: Read and write (the published state); GitHub adds Metadata read-only by itself", with one sentence: "This is the mac controller's set, so one token recipe serves both hosts." In the "Capacity" bullet of "Daily operation", replace "A main push has five Windows jobs, which run two at a time" with "The router sends a lane here only when a slot is free or the queue costs the run no time; the host publishes its slot count, so nothing else needs to know it." In "Security model in one screen", the first row's sentence "Only `WINDOWS_RUNNER_MODE=home` routes here…" becomes "Only `home` and `home-only` route here; a fork pull request is sent to the vendor by the router, refused at admission and evicted at handoff by this host, and still waits for an owner click first", and add a row: "`slate-ci-golden-prune` task | SYSTEM, daily at 04:00; deletes one fixed path only when the module's predicate holds for the current, sealed image; the orchestrator's account gains no right on `golden`." Every "both tasks" in the runbook (steps 4 and 7 of the refresh, "Re-running setup", "Rotating the token") becomes "the orchestrator and store-token tasks", with one sentence in "Re-running setup": "`slate-ci-golden-prune` holds no password and re-registers on every run."

- [ ] **Step 3: Daily operation, state, refresh and rollback**

In the "Logs" bullet add item 6:

```markdown
  6. After a green job on a VM provisioned after the current golden's seal,
     the lane counts as accepted on that image; once rust, app, model and
     shell all do (a `home-only` run does all four):
     `golden: rust, app, model and shell are green on the current image`.
     The `slate-ci-golden-prune` task (SYSTEM, daily at 04:00) then deletes
     `win11-runner.prev.vhdx` and logs
     `golden: removed win11-runner.prev.vhdx (the current image is accepted on every lane)`,
     or `golden: the previous image stays (…)`. Lines starting
     `admission: skipped job` and `handoff: … forcing it off` name jobs of
     other repositories; `publish: …` is a failed state publish, once a
     minute; `logs: …` is the once-a-day pruning of files older than
     `LogRetentionDays` (30).
```

In the "State" bullet, after "lists the active VMs, retries and seen jobs", add: ", the day the logs were last pruned (`LogsPrunedOn`), which lanes went green on the current golden (`GoldenAccepted`: its stamp, lanes, `Complete`), the last published state and when (`PublishedState`, `PublishedAt`, `PublishFailedAt`); a `SeenJobs` entry carries `Foreign` once its run was read".

Refresh steps: 3 becomes "Nothing to move: the build rotates the sealed disk to `win11-runner.prev.vhdx` itself (step 5), replacing an older rollback."; 5's command gains `-Rotate`; 8 becomes "Set the pool to `home-only`, then dispatch `windows.yml` on a branch: every lane, the shell gate included, runs on the new image" with the two commands (`gh variable set WINDOWS_RUNNER_MODE --body home-only …`, `gh workflow run windows.yml --ref <branch> …`); 9 becomes "Once that run is green, set the pool back to `home`. The loop logs `golden: rust, app, model and shell are green on the current image` and the prune task deletes the old disk at 04:00; to delete it sooner: `Start-ScheduledTask -TaskName slate-ci-golden-prune`." In "Rolling back a refresh": step 1's first sentence "If step 8 set the pool to `home-only`, set it to `namespace`."; step 6 "Set the pool back to `home`."; after step 4 add "The prune task cannot remove a rollback you have just restored: the acceptance it reads is of the image it was recorded on, and a restored image has a different stamp." In "The golden build fails", add: "A `-Rotate` refuses an unsealed leftover; remove `win11-runner.vhdx` by hand, then rebuild."

- [ ] **Step 4: The first spec and the deviations list**

In `docs/superpowers/specs/2026-10-10-self-hosted-windows-runner-design.md`: D-3's text becomes "- **D-3 Fallback:** manual at first; automatic since the overflow routing design of 2026-10-11 (`2026-10-11-windows-overflow-routing-design.md`, OR-D2/OR-D4: the host publishes its state, the router reads it)."; in §5 after the table add "Superseded on 2026-10-11 for `windows.yml`: a routing job sets each lane's pool; the table's expressions remain the vendor fallback inside the lanes' `runs-on`, verbatim."; in the threat model's residual-risk sentence about an approved fork on a throwaway VM, add "(withdrawn 2026-10-11: OR-D5 of the overflow design has the host refuse and evict such jobs)"; in "Out of scope" replace "- Automatic fallback (D-3)." with "- Automatic fallback (D-3): done on 2026-10-11."

Append to the overflow spec:

```markdown
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
```

- [ ] **Step 5: Run the guards and commit**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1`
Expected: all green.

```bash
git add docs/runbooks/self-hosted-windows-runner.md docs/superpowers/specs/2026-10-10-self-hosted-windows-runner-design.md docs/superpowers/specs/2026-10-11-windows-overflow-routing-design.md
git commit -m "docs(runbooks): overflow routing — the modes, the published state, reading a run's routing, the full re-run, the rotated golden and its prune task"
```

---

### Task 9: The weekly branch report (its own branch and pull request)

**Files:**
- Create: `.github/workflows/branch-cleanup.yml`
- Modify: `.github/workflows/windows-runner-tests.yml` (both path filters gain `- '.github/workflows/branch-cleanup.yml'`)
- Modify: `ci/windows-runner/tests/Workflows.Tests.ps1` (a guard describe; the path-filter pin gains the file)

Done on `claude/branch-cleanup`, created from `origin/main` after Task 8 is committed on the routing branch, and opened as its own pull request.

**Interfaces:**
- Produces: the workflow (schedule Mondays 06:00 UTC; `workflow_dispatch` input `delete`, boolean, default false); deletions only when `delete` is true, the name starts with `claude/`, `ahead_by` is 0 and the branch is the base of no open pull request.

- [ ] **Step 1: Write the failing guard**

Append to `ci/windows-runner/tests/Workflows.Tests.ps1`:

```powershell
Describe 'branch-cleanup.yml' {
    BeforeAll { $script:cleanup = Get-Content -Raw (Join-Path $wf 'branch-cleanup.yml') }
    It 'reports weekly and deletes only on an explicit dispatch, only claude/* tips main already contains' {
        $cleanup | Should -Match '(?m)^    - cron: ''0 6 \* \* 1'''
        $cleanup | Should -Match '(?m)^      delete:\r?\n        description:'
        $cleanup | Should -Match '(?m)^        type: boolean\r?\n        default: false'
        $cleanup | Should -Match '(?m)^  contents: write\r?\n  pull-requests: read'
        $cleanup.Contains("DELETE: `${{ github.event_name == 'workflow_dispatch' && inputs.delete && 'true' || 'false' }}") | Should -BeTrue
        $cleanup | Should -Match 'matching-refs/heads/claude/'
        $cleanup | Should -Match 'ahead_by'
        $cleanup | Should -Match '--base "\$branch"'
        $cleanup | Should -Match ([regex]::Escape('[[ "$branch" == claude/* ]]'))
        $cleanup | Should -Match 'encoded='
        $cleanup | Should -Match 'GITHUB_STEP_SUMMARY'
        $cleanup | Should -Not -Match '(?i)(^|\s)--force\b'
    }
}
```

and in `Describe 'windows-runner-tests.yml path filters'` extend the routed list with `'branch-cleanup.yml'` (two occurrences expected, like the others).

- [ ] **Step 2: Run the guard to verify it fails**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1 -Filter 'branch-cleanup.yml.*'`
Expected: fails: the file does not exist.

- [ ] **Step 3: Write the workflow**

Create `.github/workflows/branch-cleanup.yml`:

```yaml
# Stale claude/* branches: a weekly report, and a delete only when asked.
# The repository already deletes merged pull-request branches, so the
# claude/* branches that remain are unmerged work or never-reviewed WIP.
# Nothing here deletes a tip main does not already contain, or a branch an
# open pull request is based on. Spec:
# docs/superpowers/specs/2026-10-11-windows-overflow-routing-design.md §10.

name: Branch cleanup

on:
  schedule:
    - cron: '0 6 * * 1'
  workflow_dispatch:
    inputs:
      delete:
        description: Delete the claude/* branches whose tip main already contains (never anything else).
        type: boolean
        default: false

permissions:
  contents: write
  pull-requests: read

concurrency:
  group: branch-cleanup
  cancel-in-progress: false

jobs:
  report:
    name: Report stale claude/* branches
    runs-on: ubuntu-latest
    timeout-minutes: 10
    env:
      GH_TOKEN: ${{ github.token }}
      REPO: ${{ github.repository }}
      DELETE: ${{ github.event_name == 'workflow_dispatch' && inputs.delete && 'true' || 'false' }}
    steps:
      - name: List, classify and (on dispatch) delete
        run: |
          set -uo pipefail
          {
            echo "## claude/* branches ($(date -u +%Y-%m-%dT%H:%MZ), delete=$DELETE)"
            echo
            echo "| branch | ahead of main | last commit | open PR as head | open PR as base | action |"
            echo "|---|---|---|---|---|---|"
          } >> "$GITHUB_STEP_SUMMARY"
          failures=0
          while read -r branch; do
            [ -n "$branch" ] || continue
            [[ "$branch" == claude/* ]] || { echo "::error::unexpected branch name $branch"; failures=$((failures + 1)); continue; }
            encoded=$(printf '%s' "$branch" | jq -sRr @uri)
            if ! ahead=$(gh api "repos/$REPO/compare/main...$encoded?per_page=1" --jq '.ahead_by' 2>&1); then
              echo "| $branch | ? | ? | ? | ? | skipped: compare failed ($ahead) |" >> "$GITHUB_STEP_SUMMARY"; failures=$((failures + 1)); continue
            fi
            last=$(gh api "repos/$REPO/branches/$encoded" --jq '.commit.commit.committer.date' 2>/dev/null || echo '?')
            as_head=$(gh pr list --repo "$REPO" --state open --head "$branch" --json number --jq 'length' 2>/dev/null || echo '?')
            as_base=$(gh pr list --repo "$REPO" --state open --base "$branch" --json number --jq 'length' 2>/dev/null || echo '?')
            action="kept"
            if [ "$ahead" = "0" ] && [ "$as_base" = "0" ]; then
              if [ "$DELETE" = "true" ]; then
                if gh api -X DELETE "repos/$REPO/git/refs/heads/$encoded" >/dev/null 2>&1; then action="deleted (main contains its tip)"; else action="delete failed"; failures=$((failures + 1)); fi
              else
                action="deletable (main contains its tip)"
              fi
            elif [ "$ahead" != "0" ]; then
              action="kept: $ahead commit(s) main does not have"
            else
              action="kept: base of an open pull request, or unknown"
            fi
            echo "| $branch | $ahead | $last | $as_head | $as_base | $action |" >> "$GITHUB_STEP_SUMMARY"
            echo "$branch: $action"
          done < <(gh api "repos/$REPO/git/matching-refs/heads/claude/" --paginate --jq '.[].ref' | sed 's#^refs/heads/##')
          [ "$failures" -eq 0 ] || { echo "::warning::$failures branch(es) could not be classified or deleted"; }
```

- [ ] **Step 4: Run the guard, push, dry-run**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1 -Filter 'branch-cleanup.yml.*'` → passes. After the branch is pushed, `gh workflow run branch-cleanup.yml --ref claude/branch-cleanup --repo coryj627/slate` is a dry run; its summary lists every `claude/*` branch with its ahead-by count and deletes nothing.

- [ ] **Step 5: Commit and open the pull request**

```bash
git add .github/workflows/branch-cleanup.yml .github/workflows/windows-runner-tests.yml ci/windows-runner/tests/Workflows.Tests.ps1
git commit -m "ci: a weekly report of stale claude/* branches that deletes only what main contains, on dispatch"
```

---

## Integration pass (after merge, owner-driven; spec §11)

1. The PAT `slate-ci-host-cdesk` already holds Variables read and write (it was issued with the mac controller's permission set: Actions read, Administration read and write, Variables read and write); confirm it once on GitHub's fine-grained tokens page, then create the variable once (the `gh variable set WINDOWS_HOME_STATE …` command in the runbook). Nothing is re-issued and `store-token.ps1` does not run again.
2. Merge; on the host `git pull --ff-only origin main`, re-run `setup-host.ps1` (registers `slate-ci-golden-prune`, refreshes `bin`), `Stop-ScheduledTask -TaskName slate-ci-orchestrator`; within a minute `gh variable get WINDOWS_HOME_STATE` shows `"serving":true,"slots":2`.
3. With one run in flight (its lanes visible in `gh variable get WINDOWS_HOME_STATE`), dispatch a second `windows.yml` run on a branch and read both routing summaries: the second's app lane went to the vendor, its shards as the deadline says, its rust lane queued home.
4. Stop the loop, wait three minutes, dispatch a run: its summary says the state is stale and every lane went to the vendor; restart the loop.
5. Dispatch `branch-cleanup.yml` without `delete` from its own branch; read the report.
6. On the next golden refresh follow the rewritten steps; read the acceptance line after the `home-only` run and the prune line after `Start-ScheduledTask -TaskName slate-ci-golden-prune`.
