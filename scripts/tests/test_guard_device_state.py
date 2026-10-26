# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Facts for scripts/windows/guard-device-state.ps1 (#1331).

Every case starts the wrapper the way CI does, through `pwsh -NoProfile
-File`, never in process, and points it at a surrogate profile: either
explicitly (`-Profile`) or, for CI's exact default-profile shape, through
the child's LOCALAPPDATA. Nothing here touches the real profile.
"""

import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

GUARD = Path(__file__).resolve().parent.parent / "windows" / "guard-device-state.ps1"
PWSH = shutil.which("pwsh")
RUNNABLE = sys.platform == "win32" and PWSH is not None
if not RUNNABLE and os.environ.get("SLATE_REQUIRE_DEVICE_STATE_SCRIPT_TESTS") == "1":
    raise RuntimeError("CI runs these facts on Windows with PowerShell 7; this runner has neither.")

RECORDER = (
    "import json, sys\n"
    "with open(sys.argv[1], 'w', encoding='utf-8') as out:\n"
    "    json.dump(sys.argv[2:], out)\n"
)


def fake_slate(folder: Path) -> Path:
    """A process named SlateWindows: a copy of ping, which waits as long as it is told to."""
    folder.mkdir(parents=True, exist_ok=True)
    fake = folder / "SlateWindows.exe"
    shutil.copyfile(Path(os.environ.get("SystemRoot", r"C:\Windows")) / "System32" / "PING.EXE", fake)
    return fake


@unittest.skipUnless(RUNNABLE, "the wrapper is a Windows PowerShell 7 script")
class GuardDeviceStateTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = Path(tempfile.mkdtemp(prefix="guard-device-state-"))
        # A cleanup, not tearDown: cleanups run last-in-first-out, so a fake
        # Slate a case registers is killed before its folder goes.
        self.addCleanup(shutil.rmtree, self.temp, ignore_errors=True)

    def kill_later(self, pid: int) -> None:
        self.addCleanup(lambda: subprocess.run(["taskkill", "/PID", str(pid), "/F"], capture_output=True))

    def guard(self, *tokens: str, localappdata: Path | None = None, extra_env: dict | None = None) -> subprocess.CompletedProcess:
        env = dict(os.environ)
        env.update(extra_env or {})
        if localappdata is not None:
            env["LOCALAPPDATA"] = str(localappdata)
        return subprocess.run(
            [PWSH, "-NoProfile", "-File", str(GUARD), *tokens],
            capture_output=True,
            text=True,
            env=env,
            timeout=180,
        )

    def pwsh_writes(self, path: Path) -> list[str]:
        return [PWSH, "-NoProfile", "-Command", f"Set-Content -LiteralPath '{path}' -Value x"]

    # ---- against an explicit surrogate profile -------------------------

    def test_a_write_into_the_profile_exits_1_and_names_it(self) -> None:
        surrogate = self.temp / "Slate"
        surrogate.mkdir()
        run = self.guard("-Profile", str(surrogate), "--", *self.pwsh_writes(surrogate / "recent-vaults.json"))
        self.assertEqual(run.returncode, 1, run.stdout + run.stderr)
        self.assertIn("created recent-vaults.json", run.stdout)

    def test_a_profile_the_command_creates_exits_1_and_names_it(self) -> None:
        surrogate = self.temp / "Slate"
        command = [PWSH, "-NoProfile", "-Command", f"New-Item -ItemType Directory -Path '{surrogate}' | Out-Null"]
        run = self.guard("-Profile", str(surrogate), "--", *command)
        self.assertEqual(run.returncode, 1, run.stdout + run.stderr)
        self.assertTrue(surrogate.is_dir())
        self.assertIn("created .", run.stdout)

    def test_a_command_that_writes_nothing_exits_0(self) -> None:
        surrogate = self.temp / "Slate"
        surrogate.mkdir()
        (surrogate / "preferences.json").write_text("{}", encoding="utf-8")
        run = self.guard("-Profile", str(surrogate), "--", sys.executable, "-c", "pass")
        self.assertEqual(run.returncode, 0, run.stdout + run.stderr)
        self.assertIn("unchanged", run.stdout)

    def test_a_failing_command_keeps_its_own_exit_code(self) -> None:
        surrogate = self.temp / "Slate"
        surrogate.mkdir()
        run = self.guard("-Profile", str(surrogate), "--", sys.executable, "-c", "raise SystemExit(3)")
        self.assertEqual(run.returncode, 3, run.stdout + run.stderr)

    def test_a_cmdlet_command_that_succeeds_exits_0(self) -> None:
        # A cmdlet sets no exit code; the guard must not read that as a failure.
        surrogate = self.temp / "Slate"
        surrogate.mkdir()
        run = self.guard("-Profile", str(surrogate), "--", "Write-Output", "hello")
        self.assertEqual(run.returncode, 0, run.stdout + run.stderr)
        self.assertIn("hello", run.stdout)

    def test_a_launch_that_outlives_the_wait_exits_1_and_names_it(self) -> None:
        surrogate = self.temp / "Slate"
        surrogate.mkdir()
        fake = fake_slate(self.temp / "fake")
        pid_file = self.temp / "fake.pid"
        start = (
            "import subprocess, sys\n"
            f"p = subprocess.Popen([r'{fake}', '-n', '120', '127.0.0.1'], stdin=subprocess.DEVNULL,"
            " stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, creationflags=subprocess.DETACHED_PROCESS)\n"
            f"open(r'{pid_file}', 'w').write(str(p.pid))\n"
        )
        run = self.guard(
            "-Profile", str(surrogate), "--", sys.executable, "-c", start,
            extra_env={"SLATE_DEVICE_STATE_GUARD_EXIT_WAIT_SECONDS": "2"})
        pid = int(pid_file.read_text())
        self.kill_later(pid)
        self.assertEqual(run.returncode, 1, run.stdout + run.stderr)
        self.assertIn(f"pid {pid} was still running", run.stdout)

    def test_an_invalid_wait_runs_nothing(self) -> None:
        marker = self.temp / "marker.txt"
        run = self.guard(
            "-Profile", str(self.temp / "Slate"), "--", sys.executable, "-c", f"open(r'{marker}', 'w')",
            extra_env={"SLATE_DEVICE_STATE_GUARD_EXIT_WAIT_SECONDS": "abc"})
        self.assertEqual(run.returncode, 2, run.stdout + run.stderr)
        self.assertFalse(marker.exists())

    def test_a_slate_already_running_is_named_and_not_blamed_on_the_run(self) -> None:
        surrogate = self.temp / "Slate"
        surrogate.mkdir()
        fake = subprocess.Popen([str(fake_slate(self.temp / "fake")), "-n", "120", "127.0.0.1"], stdout=subprocess.DEVNULL)
        self.kill_later(fake.pid)
        run = self.guard(
            "-Profile", str(surrogate), "--", sys.executable, "-c", "pass",
            extra_env={"SLATE_DEVICE_STATE_GUARD_EXIT_WAIT_SECONDS": "2"})
        self.assertEqual(run.returncode, 0, run.stdout + run.stderr)
        self.assertIn(f"pid {fake.pid} was already running", run.stdout)

    # ---- CI's exact default-profile shape -------------------------------

    def ci_shaped(self, parent: Path, out: Path) -> list[str]:
        recorder = parent / "recorder.py"
        recorder.write_text(RECORDER, encoding="utf-8")
        return [
            sys.executable, str(recorder), str(out),
            "test", "x.csproj", "--configuration", "Release", "--no-build",
            "--filter", "FullyQualifiedName!~ConnectionsLeafTests.TheModelOf",
            "--logger", "trx;LogFileName=slate-windows-app.trx",
            "--results-directory", str(parent / "results dir"),
            "--blame-hang-timeout", "10m", "--blame-hang-dump-type", "mini",
            "-c", "Release", "-v", "n",
        ]

    def test_the_default_profile_invocation_passes_the_command_verbatim(self) -> None:
        parent = self.temp / "appdata"
        parent.mkdir()
        out = parent / "argv.json"
        command = self.ci_shaped(parent, out)
        run = self.guard("--", *command, localappdata=parent)
        self.assertEqual(run.returncode, 0, run.stdout + run.stderr)
        self.assertEqual(json.loads(out.read_text(encoding="utf-8")), command[3:])
        self.assertEqual(run.stdout.splitlines()[0], f"guard-device-state: profile {parent / 'Slate'}")

    def test_the_default_profile_invocation_watches_localappdata_slate(self) -> None:
        parent = self.temp / "appdata"
        (parent / "Slate").mkdir(parents=True)
        run = self.guard("--", *self.pwsh_writes(parent / "Slate" / "recent-vaults.json"), localappdata=parent)
        self.assertEqual(run.returncode, 1, run.stdout + run.stderr)
        self.assertIn("created recent-vaults.json", run.stdout)

    def test_a_pwsh_step_passes_the_separator_through(self) -> None:
        # CI's own step shell is pwsh, which starts the wrapper's pwsh: the
        # `--` must survive that hop too.
        parent = self.temp / "appdata"
        parent.mkdir()
        out = parent / "argv.json"
        recorder = parent / "recorder.py"
        recorder.write_text(RECORDER, encoding="utf-8")
        step = (
            f"pwsh -NoProfile -File '{GUARD}' -- '{sys.executable}' '{recorder}' '{out}' "
            f"test --logger \"trx;LogFileName=a.trx\" -c Release -v n"
        )
        env = dict(os.environ, LOCALAPPDATA=str(parent))
        run = subprocess.run([PWSH, "-NoProfile", "-Command", step], capture_output=True, text=True, env=env, timeout=180)
        self.assertEqual(run.returncode, 0, run.stdout + run.stderr)
        self.assertEqual(
            json.loads(out.read_text(encoding="utf-8")),
            ["test", "--logger", "trx;LogFileName=a.trx", "-c", "Release", "-v", "n"])

    def test_a_call_without_the_separator_runs_nothing(self) -> None:
        marker = self.temp / "marker.txt"
        run = self.guard(sys.executable, "-c", f"open(r'{marker}', 'w')", localappdata=self.temp)
        self.assertEqual(run.returncode, 2, run.stdout + run.stderr)
        self.assertIn("usage:", run.stderr)
        self.assertFalse(marker.exists())


if __name__ == "__main__":
    unittest.main()
