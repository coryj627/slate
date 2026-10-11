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
import subprocess
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


def decide(mode="home", lanes=LANES, text=None, now=NOW, concurrent=(), **overrides):
    kwargs = dict(stale_seconds=180, slots_max=8, event="push", head_repo="", repo=REPO, durations=TEST_DURATIONS)
    kwargs.update(overrides)
    return route.decide(mode, lanes, state() if text is None else text, now, list(concurrent), **kwargs)


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
    def death(self, text, now=NOW):
        parsed, error = route.parse_state(text)
        return route.state_death(parsed, error, now, 180, 8)

    def test_fresh_serving_state_lives(self):
        self.assertIsNone(self.death(state(at=NOW - 170)))
        self.assertIsNone(self.death(state(at=NOW - 180)))

    def test_stale_past_the_window_by_the_routers_clock(self):
        self.assertIn("181 s old", self.death(state(at=NOW - 181)))
        self.assertIn("190 s old by the router's clock (stale after 180 s)", self.death(state(at=NOW - 190)))

    def test_a_future_at_is_fresh(self):
        self.assertIsNone(self.death(state(at=NOW + 600)))

    def test_not_serving(self):
        self.assertIn("not serving", self.death(state(serving=False)))

    def test_slot_count_boundaries_on_both_sides(self):
        self.assertIsNone(self.death(state(slots=1)))
        self.assertIsNone(self.death(state(slots=8)))
        self.assertIn("no slot", self.death(state(slots=0)))
        self.assertIn("9 slots, above the 8", self.death(state(slots=9)))

    def test_unparseable_state_is_dead_with_the_parse_error(self):
        self.assertIn("no published state", self.death(""))


class Simulate(unittest.TestCase):
    def simulate(self, text):
        parsed, _ = route.parse_state(text)
        return route.simulate(parsed, NOW, TEST_DURATIONS)

    def test_idle_pool(self):
        self.assertEqual(self.simulate(state()), [0, 0])

    def test_running_entry_frees_at_duration_plus_handover(self):
        self.assertEqual(self.simulate(state(running=[("app", NOW - 10 * M)])), [0, 16 * M + H])

    def test_overrun_entry_frees_after_the_floor_plus_handover(self):
        self.assertEqual(self.simulate(state(running=[("rust", NOW - 60 * M)])), [0, route.OVERRUN_FLOOR_SECONDS + H])

    def test_running_entry_without_a_time_counts_from_now(self):
        # Spec §3: taken as having just started, its own home duration from now.
        self.assertEqual(self.simulate(state(running=[("rust", "soon")])), [0, 11 * M + H])

    def test_running_entry_with_an_unknown_lane_occupies_the_longest_home_duration(self):
        # One minute into an unknown lane: 26 - 1 minutes left, then the handover.
        self.assertEqual(self.simulate(state(running=[("mystery", NOW - 1 * M)])), [0, 25 * M + H])

    def test_queued_entries_take_slots_oldest_first(self):
        text = state(running=[("app", NOW - 16 * M), ("app", NOW - 11 * M)], queued=[("rust", NOW - 50), ("model", NOW - 40)])
        # Slots free at 10+H and 15+H; rust (oldest) takes the first, model the second.
        self.assertEqual(self.simulate(text), [10 * M + H + 11 * M + H, 15 * M + H + 25 * M + H])

    def test_more_running_than_slots_keeps_the_latest_ending(self):
        text = state(running=[("app", NOW - 1 * M), ("app", NOW - 2 * M), ("app", NOW - 3 * M)])
        self.assertEqual(self.simulate(text), [24 * M + H, 25 * M + H])

    def test_queued_entry_with_an_unknown_lane_occupies_the_longest_home_duration(self):
        self.assertEqual(self.simulate(state(queued=[("mystery", NOW)])), [0, 26 * M + H])


class Allocate(unittest.TestCase):
    def test_idle_pool_worked_example(self):
        # Spec §3: app and model-0 take the slots; model-1 would finish at 51.8 against a 39 deadline; rust fits.
        decisions, deadline, schedule = route.allocate(LANES, 2, [0, 0], TEST_DURATIONS)
        self.assertEqual(pools(decisions), {"app": "home", "model-0": "home", "model-1": "vendor", "rust": "home"})
        self.assertEqual(deadline, (26 + 13) * M)
        # The home lanes stay placed: app's slot, and model-0's with rust queued behind it.
        self.assertEqual(schedule, [26 * M + H, 25 * M + H + 11 * M + H])
        # Minutes with one decimal, as spec §3 writes them.
        self.assertIn("starts at 26.8 min, finishes at 37.8 min, deadline 39.0 min", decisions["rust"]["reason"])
        self.assertIn("would start at 26.8 min and finish at 51.8 min, past the deadline of 39.0 min",
                      decisions["model-1"]["reason"])

    def test_both_slots_busy_worked_example(self):
        # Two running 10 and 5 min in: slots free at 17.8 and 21.8; app vendor (deadline 35);
        # model-0 and model-1 would finish at 42.8: vendor; rust 17.8 to 28.8: home.
        free_at = [16 * M + H, 20 * M + H]
        decisions, deadline, schedule = route.allocate(LANES, 0, free_at, TEST_DURATIONS)
        self.assertEqual(schedule, [20 * M + H, 16 * M + H + 11 * M + H])
        self.assertEqual(pools(decisions), {"app": "vendor", "model-0": "vendor", "model-1": "vendor", "rust": "home"})
        self.assertEqual(deadline, (22 + 13) * M)

    def test_app_never_queues_even_when_the_simulation_says_a_slot_is_free_now(self):
        # Occupancy says both slots are taken (free 0) although the estimate reads 0: the app goes to the vendor.
        decisions, _, _ = route.allocate(["app"], 0, [0, 0], TEST_DURATIONS)
        self.assertEqual(decisions["app"]["pool"], "vendor")

    def test_deadline_boundary_is_inclusive(self):
        at_limit = (26 + 13 - 25) * M
        decisions, _, _ = route.allocate(["app", "model-0"], 2, [0, at_limit], TEST_DURATIONS)
        self.assertEqual(decisions["model-0"]["pool"], "home")
        decisions, _, _ = route.allocate(["app", "model-0"], 2, [0, at_limit + 1], TEST_DURATIONS)
        self.assertEqual(decisions["model-0"]["pool"], "vendor")

    def test_cost_order_whatever_the_input_order(self):
        decisions, _, _ = route.allocate(["rust", "model-1", "model-0", "app"], 2, [0, 0], TEST_DURATIONS)
        self.assertEqual(list(decisions), ["rust", "model-1", "model-0", "app"])
        self.assertEqual(pools(decisions), {"app": "home", "model-0": "home", "model-1": "vendor", "rust": "home"})

    def test_without_the_app_lane_the_deadline_is_the_vendor_critical_path(self):
        decisions, deadline, _ = route.allocate(["rust"], 2, [0, 0], TEST_DURATIONS)
        self.assertEqual(deadline, (22 + 13) * M)
        self.assertEqual(decisions["rust"]["pool"], "home")

    def test_unknown_lanes_get_no_decision(self):
        decisions, _, _ = route.allocate(["rust", "shell"], 2, [0, 0], TEST_DURATIONS)
        self.assertEqual(list(decisions), ["rust"])

    def test_the_app_lanes_slot_turns_over_after_a_handover(self):
        # One slot: rust queues behind the app, 26 + 1.8 minutes, then runs its own 11.
        decisions, _, schedule = route.allocate(["app", "rust"], 1, [0], TEST_DURATIONS)
        self.assertEqual(decisions["rust"]["pool"], "home")
        self.assertIn("starts at 27.8 min, finishes at 38.8 min", decisions["rust"]["reason"])
        self.assertEqual(schedule, [26 * M + H + 11 * M + H])


class Replay(unittest.TestCase):
    """Spec §3: each earlier router's own allocation, oldest first, against the schedule so far."""

    def test_no_earlier_router_leaves_the_schedule_as_published(self):
        self.assertEqual(route.replay_earlier_routers([0, 0], 2, 0, TEST_DURATIONS), (2, [0, 0]))

    def test_one_earlier_router_on_two_idle_slots_places_its_app_model_and_rust(self):
        # Its app (26 + H) and model-0 (25 + H) take both slots; its rust queues behind model-0 (+ 11 + H).
        free, schedule = route.replay_earlier_routers([0, 0], 2, 1, TEST_DURATIONS)
        self.assertEqual((free, schedule), (0, [26 * M + H, 25 * M + H + 11 * M + H]))

    def test_a_second_earlier_router_sees_only_what_the_first_left(self):
        # No free slot for the second: its app goes to the vendor and nothing of its fits the vendor deadline.
        free, schedule = route.replay_earlier_routers([0, 0], 2, 2, TEST_DURATIONS)
        self.assertEqual((free, schedule), (0, [26 * M + H, 25 * M + H + 11 * M + H]))

    def test_one_earlier_router_on_three_idle_slots_places_all_four_lanes(self):
        free, schedule = route.replay_earlier_routers([0, 0, 0], 3, 1, TEST_DURATIONS)
        self.assertEqual((free, schedule), (0, [25 * M + H, 26 * M + H, 25 * M + H + 11 * M + H]))

    def test_each_earlier_router_counts_on_five_slots(self):
        # The first places all four lanes at once and leaves one slot free.
        self.assertEqual(route.replay_earlier_routers([0] * 5, 5, 1, TEST_DURATIONS),
                         (1, [0, 11 * M + H, 25 * M + H, 25 * M + H, 26 * M + H]))
        # The second takes it with its app, queues model-0 behind the first's rust and its rust behind a model shard.
        self.assertEqual(route.replay_earlier_routers([0] * 5, 5, 2, TEST_DURATIONS),
                         (0, [25 * M + H, 26 * M + H, 26 * M + H, 11 * M + H + 25 * M + H, 25 * M + H + 11 * M + H]))


class SettleEarly(unittest.TestCase):
    """The decision before occupancy, shared by decide and main: the fork rule, the mode and the state's death."""

    def settle(self, mode="home", text=None, **overrides):
        kwargs = dict(stale_seconds=180, slots_max=8, event="push", head_repo="", repo=REPO)
        kwargs.update(overrides)
        return route.settle_early(mode, LANES + ["shell"], state() if text is None else text, NOW, **kwargs)

    def test_a_fork_the_mode_or_a_dead_state_settles_every_lane(self):
        cases = {"fork": dict(event="pull_request", head_repo="someone/slate"), "home-only": dict(mode="home-only"),
                 "namespace": dict(mode="namespace"), "stale state": dict(text=state(at=NOW - 181)),
                 "unparseable state": dict(text="{")}
        for name, overrides in cases.items():
            decisions, summary, parsed = self.settle(**overrides)
            self.assertIsNone(parsed, name)
            self.assertEqual(sorted(decisions), sorted(LANES), name)
            self.assertEqual(summary["unknown_lanes"], ["shell"], name)

    def test_a_live_state_is_handed_on_parsed_for_occupancy(self):
        text = state(running=[("app", NOW - 600)])
        decisions, summary, parsed = self.settle(text=text)
        self.assertIsNone(decisions)
        self.assertEqual(parsed, route.parse_state(text)[0])
        self.assertEqual((summary["state_age"], summary["slots"], summary["running"], summary["dead"]), (5, 2, 1, False))


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
        for text in (state(at=NOW - 181), state(serving=False), state(slots=0), "", "{"):
            decisions, summary = decide(text=text)
            self.assertEqual(set(pools(decisions).values()), {"vendor"}, text)
            self.assertTrue(summary["dead"], text)

    def test_a_future_at_is_fresh_and_the_skew_is_named(self):
        decisions, summary = decide(text=state(at=NOW + 40))
        self.assertEqual(pools(decisions), {"app": "home", "model-0": "home", "model-1": "vendor", "rust": "home"})
        self.assertFalse(summary["dead"])
        self.assertIn("40 s in the future (clock skew)", summary["reason"])

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

    def test_fork_pull_requests_route_to_the_vendor_under_home_only_too(self):
        decisions, summary = decide(mode="home-only", text="", event="pull_request", head_repo="someone/slate")
        self.assertEqual(set(pools(decisions).values()), {"vendor"})
        self.assertIn("pull request from someone/slate", summary["reason"])

    def test_same_repository_pull_request_is_routed(self):
        decisions, _ = decide(event="pull_request", head_repo=REPO)
        self.assertEqual(decisions["app"]["pool"], "home")

    def test_a_router_that_completed_inside_the_margin_before_the_state_still_counts(self):
        # Its lanes may not be published yet: counted when it completed at most 30 s before `at`.
        at = NOW - 5
        decisions, summary = decide(text=state(at=at), concurrent=[(41, at - 30)])
        self.assertEqual(summary["concurrent"], 1)
        self.assertEqual(decisions["app"]["pool"], "vendor")
        decisions, summary = decide(text=state(at=at), concurrent=[(41, at - 31)])
        self.assertEqual(summary["concurrent"], 0)
        self.assertEqual(decisions["app"]["pool"], "home")

    def test_a_router_still_queued_or_running_counts_whatever_the_state(self):
        _, summary = decide(text=state(at=NOW - 5), concurrent=[(41, None), (43, NOW - 3600)])
        self.assertEqual(summary["concurrent"], 1)

    def test_earlier_routers_take_the_free_slots(self):
        decisions, summary = decide(concurrent=[(41, None)])
        self.assertEqual(summary["free"], 0)
        self.assertEqual(pools(decisions), {"app": "vendor", "model-0": "vendor", "model-1": "vendor", "rust": "vendor"})
        # The earlier router's app (busy to 27.8) and model-0 with its rust behind it (to 39.7) hold both slots;
        # rust would start at 27.8 and end past the vendor deadline of 35.
        self.assertIn("would start at 27.8 min and finish at 38.8 min, past the deadline of 35.0 min",
                      decisions["rust"]["reason"])

    def test_an_earlier_router_on_three_slots_leaves_no_free_slot(self):
        # It places app, both model shards and rust; free slots by occupancy (3) minus its placements is none.
        decisions, summary = decide(text=state(slots=3), concurrent=[(41, None)])
        self.assertEqual(summary["free"], 0)
        self.assertEqual(pools(decisions), {"app": "vendor", "model-0": "vendor", "model-1": "vendor", "rust": "vendor"})

    def test_unknown_lanes_are_withheld_and_named(self):
        decisions, summary = decide(lanes=["rust", "shell"])
        self.assertEqual(list(decisions), ["rust"])
        self.assertEqual(summary["unknown_lanes"], ["shell"])

    def test_durations_default_to_the_production_table(self):
        decisions, _ = route.decide("home", LANES, state(), NOW, [], stale_seconds=180, slots_max=8, event="push",
                                    head_repo="", repo=REPO)
        self.assertEqual(decisions["app"]["pool"], "home")


class Constants(unittest.TestCase):
    def test_production_durations(self):
        self.assertEqual(route.DURATIONS, TEST_DURATIONS)
        self.assertEqual(route.HANDOVER_SECONDS, 110)
        self.assertEqual(route.OVERRUN_FLOOR_SECONDS, 300)
        self.assertEqual(route.DEFAULT_STALE_SECONDS, 180)

    def test_concurrency_constants(self):
        self.assertEqual(route.CONCURRENT_COMPLETION_MARGIN_SECONDS, 30)
        self.assertEqual(route.CONCURRENT_RUNS_MAX, 10)

    def test_lane_table_is_consistent(self):
        self.assertEqual(sorted(route.LANES), sorted(LANES))
        self.assertEqual(sorted(rank for _, rank in route.LANES.values()), [0, 1, 2, 3])
        for host_lane, _ in route.LANES.values():
            self.assertIn(host_lane, route.DURATIONS)
        self.assertIn("shell", route.DURATIONS)

    def windows_yml(self):
        path = os.path.join(os.path.dirname(__file__), "..", "..", "..", "..", ".github", "workflows", "windows.yml")
        if not os.path.exists(path):
            self.skipTest("windows.yml is not beside this checkout")
        with open(path, encoding="utf-8") as handle:
            return handle.read()

    def test_workflow_routes_exactly_the_lane_table(self):
        match = re.search(r"ROUTE_LANES: ([a-z0-9,-]+)", self.windows_yml())
        self.assertIsNotNone(match)
        self.assertEqual(match.group(1).split(","), ["rust", "app", "model-0", "model-1"])
        self.assertEqual(sorted(match.group(1).split(",")), sorted(route.LANES))

    def test_workflow_names_the_routing_job_the_router_looks_for(self):
        # fetch_concurrent finds earlier routers by this job name.
        self.assertIn("name: " + route.ROUTE_JOB_NAME, self.windows_yml())


class Reads(unittest.TestCase):
    def test_gh_api_retries_once_after_a_failure(self):
        calls = []

        def run(args, **kwargs):
            calls.append((args, kwargs["timeout"]))
            if len(calls) == 1:
                return subprocess.CompletedProcess(args, 1, stdout="", stderr="HTTP 502: Bad Gateway")
            return subprocess.CompletedProcess(args, 0, stdout='{"id": 42}', stderr="")

        self.assertEqual(route.gh_api("/repos/%s/actions/runs/42" % REPO, run=run), {"id": 42})
        self.assertEqual(calls, [(["gh", "api", "/repos/%s/actions/runs/42" % REPO], 15)] * 2)

    def test_gh_api_raises_with_stderr_after_two_failures(self):
        calls = []

        def run(args, **kwargs):
            calls.append(args)
            return subprocess.CompletedProcess(args, 1, stdout="", stderr="HTTP 404: Not Found\n")

        with self.assertRaises(RuntimeError) as caught:
            route.gh_api("/repos/%s/actions/runs/42" % REPO, run=run)
        self.assertIn("exited 1: HTTP 404: Not Found", str(caught.exception))
        self.assertEqual(len(calls), 2)

    def test_fetch_run_started_prefers_run_started_at(self):
        paths = []

        def get(path):
            paths.append(path)
            return {"run_started_at": iso(NOW), "created_at": iso(NOW - 86400)}

        self.assertEqual(route.fetch_run_started(REPO, "7", get), NOW)
        self.assertEqual(paths, ["/repos/%s/actions/runs/7" % REPO])
        self.assertEqual(route.fetch_run_started(REPO, "7", lambda path: {"created_at": iso(NOW - 60)}), NOW - 60)
        with self.assertRaises(ValueError):
            route.fetch_run_started(REPO, "7", lambda path: {})

    def test_fetch_concurrent_pairs_earlier_runs_with_their_routing_jobs_completion(self):
        calls = []

        def get(path):
            calls.append(path)
            if "/runs?status=queued" in path:
                return {"workflow_runs": [{"id": 5, "run_started_at": iso(NOW - 30)}]}
            if "/runs?status=in_progress" in path:
                return {"workflow_runs": [{"id": 5, "run_started_at": iso(NOW - 30)}, {"id": 6, "run_started_at": iso(NOW - 10)},
                                          {"id": 3, "run_started_at": iso(NOW - 20)}, {"id": 8, "run_started_at": iso(NOW + 10)},
                                          {"id": 9, "run_started_at": iso(NOW)}]}
            if "/runs/5/jobs" in path:
                return {"jobs": [{"name": route.ROUTE_JOB_NAME, "status": "in_progress"}]}
            if "/runs/6/jobs" in path:
                return {"jobs": [{"name": "rust tests", "status": "queued"},
                                 {"name": route.ROUTE_JOB_NAME, "status": "completed", "completed_at": iso(NOW - 8)}]}
            if "/runs/3/jobs" in path:
                return {"jobs": [{"name": "rust tests", "status": "queued"}]}
            raise AssertionError(path)

        # Oldest first; 3 has no routing job; 8 started after this run; 9 is this run.
        self.assertEqual(route.fetch_concurrent(REPO, "9", NOW, get), [(5, None), (6, NOW - 8)])
        self.assertEqual(sum(1 for path in calls if "/runs/5/jobs" in path), 1)
        self.assertFalse(any("/runs/8/jobs" in path or "/runs/9/jobs" in path for path in calls))

    def test_fetch_concurrent_orders_by_attempt_start_and_breaks_a_tie_by_id(self):
        def get(path):
            if "/runs?status=queued" in path:
                return {"workflow_runs": []}
            if "/runs?status=in_progress" in path:
                return {"workflow_runs": [
                    {"id": 7, "run_started_at": iso(NOW), "created_at": iso(NOW - 600)},     # same start, lower id: earlier
                    {"id": 11, "run_started_at": iso(NOW), "created_at": iso(NOW - 900)},    # same start, higher id: later
                    {"id": 4, "run_started_at": iso(NOW + 60), "created_at": iso(NOW - 3600)},  # re-run after this one started
                    {"id": 12, "created_at": iso(NOW - 60)}]}                                # no start: its creation stands in
            if "/jobs" in path:
                return {"jobs": [{"name": route.ROUTE_JOB_NAME, "status": "queued"}]}
            raise AssertionError(path)

        self.assertEqual(route.fetch_concurrent(REPO, "9", NOW, get), [(12, None), (7, None)])

    def test_fetch_concurrent_reads_only_the_newest_ten(self):
        jobs_read = []

        def get(path):
            if "/runs?status=queued" in path:
                return {"workflow_runs": [{"id": 100 + n, "run_started_at": iso(NOW - 100 + n)} for n in range(12)]}
            if "/runs?status=in_progress" in path:
                return {"workflow_runs": []}
            jobs_read.append(int(re.search(r"/runs/(\d+)/jobs", path).group(1)))
            return {"jobs": [{"name": route.ROUTE_JOB_NAME, "status": "in_progress"}]}

        pairs = route.fetch_concurrent(REPO, "999", NOW, get)
        self.assertEqual([run for run, _ in pairs], list(range(102, 112)))
        self.assertEqual(sorted(jobs_read), list(range(102, 112)))

    def test_fetch_concurrent_follows_the_runs_listing_to_a_short_page(self):
        pages = []

        def get(path):
            if "/runs?status=" in path:
                status, page = re.search(r"status=(\w+)&per_page=100&page=(\d+)$", path).groups()
                pages.append((status, int(page)))
                if (status, page) == ("in_progress", "1"):
                    # GitHub lists newest first: a full page of runs that started after this one.
                    return {"workflow_runs": [{"id": 1000 + n, "run_started_at": iso(NOW + 60)} for n in range(100)]}
                if (status, page) == ("in_progress", "2"):
                    return {"workflow_runs": [{"id": 41, "run_started_at": iso(NOW - 60)}]}
                return {"workflow_runs": []}
            if "/runs/41/jobs" in path:
                return {"jobs": [{"name": route.ROUTE_JOB_NAME, "status": "in_progress"}]}
            raise AssertionError(path)

        self.assertEqual(route.fetch_concurrent(REPO, "9", NOW, get), [(41, None)])
        self.assertEqual(pages, [("queued", 1), ("in_progress", 1), ("in_progress", 2)])

    def test_fetch_concurrent_keeps_what_it_read_when_a_later_page_fails(self):
        def get(path):
            if "status=queued" in path:
                return {"workflow_runs": []}
            if "status=in_progress" in path and path.endswith("&page=1"):
                return {"workflow_runs": [{"id": 41, "run_started_at": iso(NOW - 60)}] +
                                         [{"id": 1000 + n, "run_started_at": iso(NOW + 60)} for n in range(99)]}
            if "status=in_progress" in path:
                raise RuntimeError("gh api %s exited 1: HTTP 502: Bad Gateway" % path)
            if "/runs/41/jobs" in path:
                return {"jobs": [{"name": route.ROUTE_JOB_NAME, "status": "in_progress"}]}
            raise AssertionError(path)

        buffer = io.StringIO()
        with redirect_stdout(buffer):
            self.assertEqual(route.fetch_concurrent(REPO, "9", NOW, get), [(41, None)])
        self.assertIn("::notice::", buffer.getvalue())
        self.assertIn("HTTP 502: Bad Gateway", buffer.getvalue())

    def test_fetch_concurrent_reads_at_most_five_pages_per_status(self):
        paths = []

        def get(path):
            paths.append(path)
            return {"workflow_runs": [{"id": 1000 + n, "run_started_at": iso(NOW + 60)} for n in range(100)]}

        self.assertEqual(route.fetch_concurrent(REPO, "9", NOW, get), [])
        self.assertEqual(len(paths), 10)
        self.assertTrue(paths[4].endswith("status=queued&per_page=100&page=5"))


class Main(unittest.TestCase):
    def env(self, mode="home", text=None, **extra):
        env = {"WINDOWS_RUNNER_MODE": mode, "WINDOWS_HOME_STATE": state() if text is None else text,
               "ROUTE_LANES": "rust,app,model-0,model-1", "ROUTE_ATTEMPT": "1", "ROUTE_RUN_ID": "42", "ROUTE_EVENT": "push",
               "ROUTE_HEAD_REPO": "", "ROUTE_REPO": REPO, "ROUTE_NOW": str(NOW), "ROUTE_RUN_STARTED": str(NOW),
               "ROUTE_CONCURRENT": "[]"}
        env.update(extra)
        return env

    def run_main(self, env, get=None):
        """main with the output and summary in temporary files; without `get`, any read fails the test
        (main swallows a failing read, so the reads are recorded and asserted, not just raised)."""
        unexpected_reads = []

        def no_read(path):
            unexpected_reads.append(path)
            raise AssertionError("no read expected: " + path)

        fd, out = tempfile.mkstemp()
        os.close(fd)
        fd, summary = tempfile.mkstemp()
        os.close(fd)
        env["GITHUB_OUTPUT"] = out
        env["GITHUB_STEP_SUMMARY"] = summary
        buffer = io.StringIO()
        try:
            with redirect_stdout(buffer):
                code = route.main(env, get=get or no_read, clock=lambda: NOW)
            with open(out, encoding="utf-8") as handle:
                outputs = dict(line.rstrip("\n").split("=", 1) for line in handle if "=" in line)
            with open(summary, encoding="utf-8") as handle:
                text = handle.read()
        finally:
            os.remove(out)
            os.remove(summary)
        self.assertEqual(unexpected_reads, [])
        return code, outputs, text, buffer.getvalue()

    def without_replay_inputs(self, env):
        """The environment the workflow gives: no ROUTE_RUN_STARTED or ROUTE_CONCURRENT, so main may read."""
        env.pop("ROUTE_RUN_STARTED")
        env.pop("ROUTE_CONCURRENT")
        return env

    def test_home_idle_pool_writes_pools_and_the_attempt(self):
        code, outputs, text, _ = self.run_main(self.env())
        self.assertEqual(code, 0)
        self.assertEqual(outputs, {"attempt": "1", "rust-pool": "home", "app-pool": "home", "model-0-pool": "home", "model-1-pool": "vendor"})
        self.assertIn("| app | home |", text)
        self.assertIn("run 42", text)
        self.assertIn("Replay: `WINDOWS_RUNNER_MODE=\"home\"", text)
        self.assertIn("ROUTE_RUN_STARTED=%d " % NOW, text)

    def test_a_rerun_attempt_routes_around_a_host_that_died_after_the_run_started(self):
        env = self.env(text=state(at=NOW - 3600), ROUTE_ATTEMPT="2", ROUTE_RUN_STARTED=str(NOW - 86400))
        code, outputs, text, _ = self.run_main(env)
        self.assertEqual(code, 0)
        self.assertEqual(outputs, {"attempt": "2", "rust-pool": "vendor", "app-pool": "vendor", "model-0-pool": "vendor",
                                   "model-1-pool": "vendor"})
        self.assertIn("state is 3600 s old", text)

    def test_replay_line_reproduces_the_decision(self):
        env = self.env(text=state(slots=5, running=[("app", NOW - 600)]), ROUTE_CONCURRENT=json.dumps([[41, None]]))
        _, outputs, text, _ = self.run_main(env)
        self.assertIn('ROUTE_CONCURRENT="[[41, null]]"', text)
        # The earlier router fills the four free slots; only this run's rust fits behind its rust.
        self.assertEqual(outputs, {"attempt": "1", "rust-pool": "home", "app-pool": "vendor", "model-0-pool": "vendor",
                                   "model-1-pool": "vendor"})
        line = re.search(r"Replay: `(.*) python3", text).group(1)
        env = dict(part.split("=", 1) for part in re.findall(r"[A-Z_]+=(?:\"(?:[^\"\\]|\\.)*\"|\S+)", line))
        env = {key: json.loads(value) if value.startswith('"') else value for key, value in env.items()}
        env.update({"ROUTE_ATTEMPT": "1", "ROUTE_RUN_ID": "42"})
        _, replayed, _, _ = self.run_main(env)
        self.assertEqual(replayed, outputs)

    def test_namespace_mode_makes_no_read(self):
        reads = []
        code, outputs, _, _ = self.run_main(self.without_replay_inputs(self.env(mode="namespace")),
                                            get=lambda path: reads.append(path) or {})
        self.assertEqual(code, 0)
        self.assertEqual(outputs["app-pool"], "vendor")
        self.assertEqual(reads, [])

    def test_no_read_when_the_fork_rule_or_the_state_settles_every_lane(self):
        cases = {"fork": dict(ROUTE_EVENT="pull_request", ROUTE_HEAD_REPO="someone/slate"),
                 "fork under home-only": dict(mode="home-only", ROUTE_EVENT="pull_request", ROUTE_HEAD_REPO="someone/slate"),
                 "unparseable state": dict(text="{"),
                 "stale state": dict(text=state(at=NOW - 3600))}
        for name, extra in cases.items():
            reads = []
            _, outputs, _, _ = self.run_main(self.without_replay_inputs(self.env(**extra)),
                                             get=lambda path: reads.append(path) or {})
            self.assertEqual({outputs[l + "-pool"] for l in LANES}, {"vendor"}, name)
            self.assertEqual(reads, [], name)

    def test_optional_reads_are_used_and_their_failure_is_tolerated(self):
        seen = []

        def get(path):
            seen.append(path)
            if path.endswith("/actions/runs/42"):
                return {"run_started_at": iso(NOW - 300), "created_at": iso(NOW - 86400)}
            if "/runs?status=queued" in path:
                return {"workflow_runs": []}
            if "/runs?status=in_progress" in path:
                # 41 started before this attempt (NOW - 300), 43 after it.
                return {"workflow_runs": [{"id": 41, "run_started_at": iso(NOW - 400)},
                                          {"id": 43, "run_started_at": iso(NOW - 200)}]}
            if "/runs/41/jobs" in path:
                return {"jobs": [{"name": route.ROUTE_JOB_NAME, "status": "in_progress"}]}
            raise AssertionError(path)

        _, outputs, text, _ = self.run_main(self.without_replay_inputs(self.env(text=state(at=NOW - 50))), get=get)
        # The state's age is measured against now; the start orders the routers, and both are replayed.
        self.assertIn("| 50 s |", text)
        self.assertIn("ROUTE_RUN_STARTED=%d " % (NOW - 300), text)
        self.assertIn('ROUTE_CONCURRENT="[[41, null]]"', text)
        self.assertEqual(outputs["app-pool"], "vendor")
        self.assertEqual(seen[0], "/repos/%s/actions/runs/42" % REPO)
        self.assertFalse(any("/runs/43/jobs" in path for path in seen))
        _, outputs, text, printed = self.run_main(self.without_replay_inputs(self.env(text=state(at=NOW - 50))),
                                                  get=lambda path: (_ for _ in ()).throw(RuntimeError("boom")))
        # Without the reads, now stands in for the start and no earlier router is assumed: a notice, no warning.
        self.assertEqual(outputs["app-pool"], "home")
        self.assertIn("ROUTE_RUN_STARTED=%d " % NOW, text)
        self.assertIn("::notice::", printed)
        self.assertNotIn("::warning", printed)

    def test_annotation_text_is_escaped(self):
        # % first, so an escape already in the text is not mistaken for one; a line break would end the annotation.
        self.assertEqual(route.annotation_text("100%0A done\r\nnext"), "100%250A done%0D%0Anext")
        failure = RuntimeError("gh api /repos/x exited 1: HTTP 502 at 100%\nretry later")
        _, _, _, printed = self.run_main(self.without_replay_inputs(self.env(text=state(at=NOW - 50))),
                                         get=lambda path: (_ for _ in ()).throw(failure))
        notices = [line for line in printed.splitlines() if line.startswith("::notice::")]
        self.assertEqual(len(notices), 2)
        for notice in notices:
            self.assertIn("HTTP 502 at 100%25%0Aretry later", notice)
        self.assertFalse(any(line.startswith("retry later") for line in printed.splitlines()))

    def test_a_summary_that_cannot_be_written_is_a_warning_after_the_outputs(self):
        with tempfile.TemporaryDirectory() as scratch:
            out = os.path.join(scratch, "output")
            env = self.env(GITHUB_OUTPUT=out, GITHUB_STEP_SUMMARY=os.path.join(scratch, "missing", "summary.md"))
            buffer = io.StringIO()
            with redirect_stdout(buffer):
                code = route.main(env, get=lambda path: {}, clock=lambda: NOW)
            with open(out, encoding="utf-8") as handle:
                outputs = handle.read()
        self.assertEqual(code, 0)
        self.assertIn("app-pool=home", outputs)
        self.assertIn("::warning title=Windows lane router::step summary not written", buffer.getvalue())

    def test_outputs_that_cannot_be_written_are_a_warning_and_the_summary_still_lands(self):
        with tempfile.TemporaryDirectory() as scratch:
            summary = os.path.join(scratch, "summary.md")
            env = self.env(GITHUB_OUTPUT=os.path.join(scratch, "missing", "output"), GITHUB_STEP_SUMMARY=summary)
            buffer = io.StringIO()
            with redirect_stdout(buffer):
                code = route.main(env, get=lambda path: {}, clock=lambda: NOW)
            with open(summary, encoding="utf-8") as handle:
                text = handle.read()
        self.assertEqual(code, 0)
        self.assertIn("| app | home |", text)
        self.assertIn("::warning title=Windows lane router::outputs not written", buffer.getvalue())

    def test_an_unexpected_failure_falls_back_to_the_vendor_with_a_warning(self):
        # Not JSON, and the revision-3 shape (bare run ids) instead of [run id, completion] pairs.
        for concurrent, error in (("not json", "JSONDecodeError"), ("[41]", "ValueError")):
            env = self.env(ROUTE_CONCURRENT=concurrent)
            code, outputs, text, printed = self.run_main(env)
            self.assertEqual(code, 0)
            self.assertEqual({outputs[l + "-pool"] for l in LANES}, {"vendor"}, concurrent)
            self.assertIn("::warning title=Windows lane router::", printed)
            self.assertIn(error, text)

    def test_malformed_numbers_fall_back_to_defaults(self):
        code, outputs, _, _ = self.run_main(self.env(ROUTE_STALE_SECONDS="soon", ROUTE_NOW="later"))
        self.assertEqual(code, 0)
        self.assertEqual(outputs["app-pool"], "home")
        buffer = io.StringIO()
        with redirect_stdout(buffer):
            self.assertEqual(route._int_env({"X": "abc"}, "X", 7), 7)
            self.assertEqual(route._int_env({"X": " 42 "}, "X", 7), 42)
        self.assertEqual(buffer.getvalue(), "ignoring malformed X='abc'\n")

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
