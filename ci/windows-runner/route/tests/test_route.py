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
