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


if __name__ == "__main__":
    unittest.main()
