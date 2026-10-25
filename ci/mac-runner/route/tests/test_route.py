"""Tests for the route decision (ci/mac-runner/route/route.py).

Run:  python3 -m unittest discover -s ci/mac-runner/route/tests -t ci/mac-runner/route
"""

import calendar
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
import route  # noqa: E402

NOW = 1_760_000_000  # any fixed epoch
T = calendar.timegm((2026, 10, 10, 3, 4, 5, 0, 0, 0))  # 2026-10-10T03:04:05Z


class ParseHeartbeat(unittest.TestCase):
    def test_iso_utc(self):
        self.assertEqual(route.parse_heartbeat("2026-10-10T03:04:05Z"), T)

    def test_iso_offset_zero(self):
        self.assertEqual(route.parse_heartbeat("2026-10-10T03:04:05+00:00"), T)

    def test_epoch(self):
        self.assertEqual(route.parse_heartbeat(str(T)), T)

    def test_absent(self):
        for value in (None, "", " ", "0"):
            self.assertIsNone(route.parse_heartbeat(value))

    def test_garbage(self):
        self.assertIsNone(route.parse_heartbeat("yesterday"))


class Decide(unittest.TestCase):
    def test_auto_fresh_heartbeat_goes_to_studio(self):
        self.assertEqual(route.decide("auto", str(NOW - 120), NOW)[0], "studio")

    def test_auto_at_threshold_goes_to_studio(self):
        self.assertEqual(route.decide("auto", str(NOW - 600), NOW)[0], "studio")

    def test_auto_stale_heartbeat_goes_to_namespace(self):
        self.assertEqual(route.decide("auto", str(NOW - 601), NOW)[0], "namespace")

    def test_auto_missing_heartbeat_goes_to_namespace(self):
        self.assertEqual(route.decide("auto", "", NOW)[0], "namespace")
        self.assertEqual(route.decide("auto", "0", NOW)[0], "namespace")
        self.assertEqual(route.decide("auto", None, NOW)[0], "namespace")

    def test_auto_garbage_heartbeat_goes_to_namespace(self):
        self.assertEqual(route.decide("auto", "soon", NOW)[0], "namespace")

    def test_empty_mode_is_auto(self):
        self.assertEqual(route.decide("", str(NOW - 10), NOW)[0], "studio")
        self.assertEqual(route.decide(None, "", NOW)[0], "namespace")

    def test_forced_studio_ignores_heartbeat(self):
        self.assertEqual(route.decide("studio", "", NOW)[0], "studio")

    def test_forced_namespace_ignores_heartbeat(self):
        self.assertEqual(route.decide("namespace", str(NOW), NOW)[0], "namespace")

    def test_unknown_mode_falls_back_to_namespace(self):
        self.assertEqual(route.decide("beta", str(NOW), NOW)[0], "namespace")

    def test_mode_is_case_insensitive(self):
        self.assertEqual(route.decide(" Studio ", "", NOW)[0], "studio")

    def test_future_heartbeat_is_fresh(self):
        self.assertEqual(route.decide("auto", str(NOW + 30), NOW)[0], "studio")

    def test_custom_stale_window(self):
        self.assertEqual(route.decide("auto", str(NOW - 100), NOW, stale_seconds=60)[0], "namespace")


class BotRule(unittest.TestCase):
    """Plan 42 §3.7: a bot's run goes to Namespace whatever the mode."""

    LISTS = ({"coryj627"}, {"dependabot[bot]"}, ("dependabot/",))

    def test_a_policy_only_actor_goes_to_namespace_even_in_studio_mode(self):
        for mode in ("studio", "auto", "namespace"):
            route_name, reason = route.decide(mode, str(NOW - 5), NOW, actors=("dependabot[bot]", ""),
                                              actor_lists=self.LISTS)
            self.assertEqual(route_name, "namespace", mode)
            self.assertIn("dependabot[bot]", reason)

    def test_the_triggering_actor_is_checked_too(self):
        route_name, _ = route.decide("studio", str(NOW - 5), NOW, actors=("coryj627", "dependabot[bot]"),
                                     actor_lists=self.LISTS)
        self.assertEqual(route_name, "namespace")

    def test_a_bot_not_in_actors_goes_to_namespace_and_the_owner_is_unchanged(self):
        bot, _ = route.decide("studio", str(NOW - 5), NOW, actors=("renovate[bot]", ""), actor_lists=self.LISTS)
        self.assertEqual(bot, "namespace")
        owner, _ = route.decide("studio", str(NOW - 5), NOW, actors=("coryj627", "coryj627"), actor_lists=self.LISTS)
        self.assertEqual(owner, "studio")

    def test_without_an_allowlist_every_bot_goes_to_namespace(self):
        route_name, _ = route.decide("studio", str(NOW - 5), NOW, actors=("dependabot[bot]", ""), actor_lists=None)
        self.assertEqual(route_name, "namespace")

    def test_the_owner_updating_a_dependabot_pull_request_goes_to_namespace(self):
        for actors in (("coryj627", "coryj627", "dependabot[bot]"), ("coryj627", "coryj627", "")):
            route_name, _ = route.decide("studio", str(NOW - 5), NOW, actors=actors, actor_lists=self.LISTS,
                                         head_ref="dependabot/github_actions/actions-checkout-7.0.2")
            self.assertEqual(route_name, "namespace", actors)

    def test_the_owners_own_branch_goes_to_the_studio(self):
        route_name, _ = route.decide("studio", str(NOW - 5), NOW, actors=("coryj627", "coryj627", "coryj627"),
                                     actor_lists=self.LISTS, head_ref="claude/xb-12-dependabot-vendor-only")
        self.assertEqual(route_name, "studio")

    def test_the_repository_allowlist_names_dependabot_policy_only(self):
        trusted, policy_only, prefixes = route.load_actor_lists(route.DEFAULT_ALLOWLIST)
        self.assertIn("dependabot[bot]", policy_only)
        self.assertNotIn("dependabot[bot]", trusted)
        self.assertEqual(prefixes, ("dependabot/",))

    def test_main_reads_the_actors_from_the_environment(self):
        with tempfile.NamedTemporaryFile("r+", delete=False) as out:
            path = out.name
        try:
            env = {"MAC_RUNNER_MODE": "studio", "MAC_RUNNER_HEARTBEAT": str(NOW - 5), "ROUTE_NOW": str(NOW),
                   "ROUTE_ACTOR": "dependabot[bot]", "ROUTE_TRIGGERING_ACTOR": "dependabot[bot]",
                   "GITHUB_OUTPUT": path}
            self.assertEqual(route.main(env), 0)
            with open(path, encoding="utf-8") as handle:
                text = handle.read()
            self.assertIn("route=namespace\n", text)
            self.assertIn("label=nscloud-macos-tahoe-slim-arm64-6x14\n", text)
        finally:
            os.remove(path)


class Labels(unittest.TestCase):
    def test_labels(self):
        self.assertEqual(route.label_for("studio"), "slate-mac-tart")
        self.assertEqual(route.label_for("namespace"), "nscloud-macos-tahoe-slim-arm64-6x14")


class Main(unittest.TestCase):
    def test_writes_github_output(self):
        with tempfile.NamedTemporaryFile("r+", delete=False) as out:
            path = out.name
        try:
            env = {"MAC_RUNNER_MODE": "auto", "MAC_RUNNER_HEARTBEAT": str(NOW - 5),
                   "ROUTE_NOW": str(NOW), "GITHUB_OUTPUT": path}
            self.assertEqual(route.main(env), 0)
            with open(path, encoding="utf-8") as handle:
                text = handle.read()
            self.assertIn("route=studio\n", text)
            self.assertIn("label=slate-mac-tart\n", text)
        finally:
            os.remove(path)

    def test_namespace_without_output_file(self):
        env = {"MAC_RUNNER_MODE": "auto", "MAC_RUNNER_HEARTBEAT": "", "ROUTE_NOW": str(NOW)}
        self.assertEqual(route.main(env), 0)

    def test_malformed_overrides_fall_back(self):
        env = {"MAC_RUNNER_MODE": "auto", "MAC_RUNNER_HEARTBEAT": "", "ROUTE_NOW": "abc", "ROUTE_STALE_SECONDS": "soon"}
        self.assertEqual(route.main(env), 0)
        self.assertEqual(route._int_env({"X": "abc"}, "X", 7), 7)
        self.assertEqual(route._int_env({"X": " 42 "}, "X", 7), 42)
        self.assertEqual(route._int_env({}, "X", 7), 7)


if __name__ == "__main__":
    unittest.main()
