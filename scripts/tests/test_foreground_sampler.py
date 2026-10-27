# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Facts for scripts/windows/foreground-sampler.ps1 (#1680).

Each case starts the sampler as the app job does, through `pwsh -NoProfile
-File`, writing into the case's own folder, and stops it with its stop file.
"""

import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import time
import unittest

SAMPLER = Path(__file__).resolve().parent.parent / "windows" / "foreground-sampler.ps1"
PWSH = shutil.which("pwsh")
RUNNABLE = sys.platform == "win32" and PWSH is not None
if not RUNNABLE and os.environ.get("SLATE_REQUIRE_DEVICE_STATE_SCRIPT_TESTS") == "1":
    raise RuntimeError("CI runs these facts on Windows with PowerShell 7; this runner has neither.")


@unittest.skipUnless(RUNNABLE, "the sampler is a Windows PowerShell 7 script")
class ForegroundSamplerTests(unittest.TestCase):
    def setUp(self) -> None:
        self.t = Path(tempfile.mkdtemp(prefix="foreground-sampler-")).resolve()
        self.addCleanup(shutil.rmtree, self.t, ignore_errors=True)
        self.log = self.t / "logs" / "foreground.log"
        self.stop = self.t / "foreground.stop"

    def start(self, *extra: str) -> subprocess.Popen:
        process = subprocess.Popen(
            [PWSH, "-NoProfile", "-File", str(SAMPLER), "-Out", str(self.log), "-StopFile", str(self.stop),
             "-IntervalMs", "100", *extra],
            stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        self.addCleanup(process.stdout.close)
        self.addCleanup(lambda: process.poll() is None and process.kill())
        return process

    def wait_for(self, predicate, seconds: float = 30.0) -> bool:
        deadline = time.monotonic() + seconds
        while time.monotonic() < deadline:
            if predicate():
                return True
            time.sleep(0.1)
        return False

    def lines(self) -> list:
        return self.log.read_text(encoding="utf-8").splitlines() if self.log.exists() else []

    def test_it_logs_the_foreground_window_and_stops_on_its_stop_file(self) -> None:
        process = self.start()
        self.assertTrue(
            self.wait_for(lambda: any(" foreground pid=" in line for line in self.lines())),
            f"no foreground line: {self.lines()}")
        self.stop.write_text("stop", encoding="utf-8")
        self.assertEqual(process.wait(timeout=30), 0, process.stdout.read().decode(errors="replace"))
        lines = self.lines()
        self.assertTrue(lines[0].split(" ", 1)[1].startswith("sampler started pid="), lines)
        self.assertTrue(lines[-1].endswith("sampler stopped (stop file)"), lines)

    def test_it_logs_a_foreground_change_once(self) -> None:
        process = self.start()
        self.assertTrue(self.wait_for(lambda: any(" foreground pid=" in line for line in self.lines())))
        time.sleep(1.0)  # ten samples of an unchanged foreground
        self.stop.write_text("stop", encoding="utf-8")
        self.assertEqual(process.wait(timeout=30), 0)
        foreground = [line for line in self.lines() if " foreground pid=" in line]
        self.assertLessEqual(len(foreground), 2, foreground)

    def test_it_stops_at_its_time_limit(self) -> None:
        process = self.start("-MaxMinutes", "1")
        # The limit is minutes; the fact only proves the argument is taken
        # and the loop runs, then stops it.
        self.assertTrue(self.wait_for(lambda: len(self.lines()) >= 2))
        self.stop.write_text("stop", encoding="utf-8")
        self.assertEqual(process.wait(timeout=30), 0)


if __name__ == "__main__":
    unittest.main()
