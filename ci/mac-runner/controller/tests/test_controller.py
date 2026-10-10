"""Tests for the host controller's decision logic (ci/mac-runner/controller/controller.py).

Run:  python3 -m unittest discover -s ci/mac-runner/controller/tests -t ci/mac-runner/controller

Nothing here touches tart, launchd or GitHub: the pieces that decide run
against fakes; the pieces that act (clone, boot, exec) are not run.
"""

import base64
import json
import os
import subprocess
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
import controller  # noqa: E402


class StateDir(unittest.TestCase):
    """A temporary state directory in place of ~/.slate-runner."""

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.saved = (controller.STATE, controller.LOGS, controller.TOKEN_FILE)
        controller.STATE = os.path.join(self.tmp.name, "state")
        controller.LOGS = os.path.join(self.tmp.name, "logs")
        controller.TOKEN_FILE = os.path.join(self.tmp.name, "github-token")
        os.makedirs(controller.STATE)

    def tearDown(self):
        controller.STATE, controller.LOGS, controller.TOKEN_FILE = self.saved
        self.tmp.cleanup()


class FakeGitHub:
    """Stands in for controller.github: scripted answers, recorded calls."""

    def __init__(self, *answers):
        self.answers = list(answers)
        self.calls = []

    def __call__(self, method, path, body=None, auth=True, timeout=30):
        self.calls.append((method, path, body))
        return self.answers.pop(0) if self.answers else (0, None)


class HeartbeatTests(unittest.TestCase):
    def setUp(self):
        self.github = FakeGitHub()
        self.saved = controller.github
        controller.github = self.github
        self.now = 1_000_000.0
        self.hb = controller.Heartbeat(clock=lambda: self.now)

    def tearDown(self):
        controller.github = self.saved

    def values(self):
        return [body["value"] for _, _, body in self.github.calls]

    def test_healthy_beats_are_throttled(self):
        self.github.answers = [(204, None), (204, None)]
        self.hb.send(True)
        self.now += 100
        self.hb.send(True)
        self.assertEqual(len(self.github.calls), 1)
        self.now += controller.HEARTBEAT_INTERVAL
        self.hb.send(True)
        self.assertEqual(len(self.github.calls), 2)

    def test_zero_is_written_once(self):
        self.github.answers = [(204, None)]
        self.hb.send(False)
        self.hb.send(False)
        self.assertEqual(self.values(), ["0"])

    def test_recovery_after_zero_is_not_throttled(self):
        # The review's finding: a "0" also set last_sent, so the next healthy
        # beat waited out the interval while the variable still said 0.
        self.github.answers = [(204, None), (204, None), (204, None)]
        self.hb.send(True)
        self.now += 10
        self.hb.send(False)
        self.now += 10
        self.hb.send(True)
        self.assertEqual(len(self.github.calls), 3)
        self.assertNotEqual(self.values()[-1], "0")

    def test_missing_variable_is_created(self):
        self.github.answers = [(404, None), (201, None)]
        self.hb.send(True)
        self.assertEqual([m for m, _, _ in self.github.calls], ["PATCH", "POST"])

    def test_failed_write_is_retried_next_time(self):
        self.github.answers = [(500, None), (204, None)]
        self.hb.send(True)
        self.hb.send(True)
        self.assertEqual(len(self.github.calls), 2)


class GitHubWithoutToken(StateDir):
    def test_missing_token_file_does_not_raise(self):
        # The idle loop sends heartbeats before the token exists; the daemon
        # must idle, not crash and restart every 30 s.
        self.assertEqual(controller.github("PATCH", "/x", {"a": 1}), (0, None))


class BackoffTests(unittest.TestCase):
    def test_outcomes_that_back_off(self):
        for outcome in ("clone failed: x", "vm exited during boot (code 1): y",
                        "guest agent did not answer within 180 s", "no jit config",
                        "could not write allow-list into the guest", "could not set the VM shape: z",
                        "idle: no allow-list", "cycle crashed", "runner exited without a job (code 7)",
                        "job ended without a result after 4 s (runner killed: admission refusal or crash, code 0)"):
            self.assertTrue(controller.needs_backoff(outcome), outcome)

    def test_outcomes_that_continue_at_once(self):
        for outcome in ("job finished in 300 s: Succeeded", "idle VM replaced (recycle)",
                        "idle VM recycled after 6 h", "controller stopping", "WATCHDOG: job exceeded 75 min"):
            self.assertFalse(controller.needs_backoff(outcome), outcome)


class FakeProc:
    def __init__(self, lines):
        self.stdout = [line.encode() for line in lines]


class RunnerOutputTests(StateDir):
    def read(self, lines):
        reader = controller.RunnerOutput(FakeProc(lines), "job-test")
        reader.run()
        return reader

    def test_job_with_result(self):
        reader = self.read(["2026-10-10 03:00:00Z: Listening for Jobs",
                            "2026-10-10 03:01:00Z: Running job: native",
                            "2026-10-10 03:05:00Z: Job native completed with result: Succeeded"])
        self.assertIsNotNone(reader.job_started_at)
        self.assertEqual(reader.job_result, "Succeeded")
        self.assertTrue(controller.flag("job-running"))
        outcome = controller.runner_outcome(reader, 0)
        self.assertIn("job finished", outcome)
        self.assertIn("Succeeded", outcome)
        self.assertFalse(controller.needs_backoff(outcome))

    def test_refused_job_has_no_result(self):
        # The admission hook SIGKILLs the listener, so run-helper prints its
        # "unknown error code" line and no result line ever appears.
        reader = self.read(["Running job: native", "Exiting with unknown error code: 137"])
        self.assertIsNotNone(reader.job_started_at)
        self.assertIsNone(reader.job_result)
        outcome = controller.runner_outcome(reader, 0)
        self.assertIn("without a result", outcome)
        self.assertTrue(controller.needs_backoff(outcome))

    def test_no_job(self):
        reader = self.read(["Listening for Jobs", "Runner listener exit with terminated error"])
        self.assertIsNone(reader.job_started_at)
        self.assertFalse(controller.flag("job-running"))
        self.assertEqual(controller.runner_outcome(reader, 1), "runner exited without a job (code 1)")


class FlagTests(StateDir):
    def test_stop_reason_order(self):
        self.assertIsNone(controller.stop_reason())
        controller.set_flag("recycle")
        self.assertEqual(controller.stop_reason(), "recycle")
        controller.set_flag("paused")
        self.assertEqual(controller.stop_reason(), "paused")

    def test_building_marker(self):
        self.assertTrue(controller.advertise_healthy())
        controller.set_flag("building", "build-warm.sh\n")
        self.assertTrue(controller.building_active())
        self.assertEqual(controller.stop_reason(), "building")
        self.assertFalse(controller.advertise_healthy())
        self.assertIn("rebuilding", controller.idle_reason())

    def test_stale_building_marker_is_cleared(self):
        controller.set_flag("building")
        path = os.path.join(controller.STATE, "building")
        old = controller.time.time() - controller.BUILDING_STALE - 60
        os.utime(path, (old, old))
        self.assertFalse(controller.building_active())
        self.assertFalse(controller.flag("building"))

    def test_paused_idles_and_stops_advertising(self):
        controller.set_flag("paused")
        self.assertEqual(controller.idle_reason(), "paused")
        self.assertFalse(controller.advertise_healthy())


class AllowlistTests(StateDir):
    GOOD = {"repository_id": 1241111067, "actors": [{"id": 1, "login": "x"}]}

    def setUp(self):
        super().setUp()
        self.saved_github = controller.github

    def tearDown(self):
        controller.github = self.saved_github
        super().tearDown()

    @staticmethod
    def contents(data):
        raw = json.dumps(data).encode()
        return 200, {"encoding": "base64", "content": base64.b64encode(raw).decode()}

    def test_fresh_copy_is_cached(self):
        controller.github = FakeGitHub(self.contents(self.GOOD))
        raw = controller.fetch_allowlist()
        self.assertEqual(json.loads(raw)["repository_id"], 1241111067)
        with open(os.path.join(controller.STATE, "allowlist.json"), encoding="utf-8") as handle:
            self.assertEqual(json.load(handle), self.GOOD)

    def test_api_failure_uses_cache(self):
        controller.github = FakeGitHub(self.contents(self.GOOD), (0, None))
        controller.fetch_allowlist()
        self.assertEqual(json.loads(controller.fetch_allowlist()), self.GOOD)

    def test_malformed_copy_uses_cache(self):
        controller.github = FakeGitHub(self.contents(self.GOOD), self.contents({"repository_id": 0, "actors": []}))
        controller.fetch_allowlist()
        self.assertEqual(json.loads(controller.fetch_allowlist()), self.GOOD)

    def test_nothing_available(self):
        controller.github = FakeGitHub((0, None))
        self.assertIsNone(controller.fetch_allowlist())


class TartListTests(unittest.TestCase):
    def setUp(self):
        self.saved = controller.run

    def tearDown(self):
        controller.run = self.saved

    def test_names_from_tart_list(self):
        out = ("Source Name                 Disk Size State\n"
               "local  slate-mac-warm       80   19   stopped\n"
               "local  job-20261010-030000  80   20   running\n")
        controller.run = lambda cmd, **kw: subprocess.CompletedProcess(cmd, 0, out.encode(), b"")
        self.assertEqual(controller.list_vms(), ["slate-mac-warm", "job-20261010-030000"])
        self.assertTrue(controller.vm_exists("slate-mac-warm"))
        self.assertFalse(controller.vm_exists("job-"))

    def test_failed_list_is_empty(self):
        controller.run = lambda cmd, **kw: subprocess.CompletedProcess(cmd, 1, b"", b"locked")
        self.assertEqual(controller.list_vms(), [])


class TimeoutWrapper(unittest.TestCase):
    def test_timeout_becomes_exit_124(self):
        res = controller.run([sys.executable, "-c", "import time; time.sleep(5)"], timeout=0.2)
        self.assertEqual(res.returncode, 124)
        self.assertIn(b"timed out", res.stderr)


if __name__ == "__main__":
    unittest.main()
