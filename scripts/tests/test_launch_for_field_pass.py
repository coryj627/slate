# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Facts for scripts/windows/launch-for-field-pass.ps1 (#1331, plan 44 §4 rule 4).

Every case starts the launcher as every caller does, through `pwsh
-NoProfile -File`, with the child's TEMP and TMP set to the case's own
folder, so a fresh root lands there and nothing touches the real %TEMP% or
profile. The "app" is this interpreter (`-Exe sys.executable`), and the first
vault holds a recorder `__main__.py`: Python runs a folder given as its
argument through that file, and the recorder writes what it was started
with, its device-state root and that root's recents to launched.json.
"""

import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

LAUNCHER = Path(__file__).resolve().parent.parent / "windows" / "launch-for-field-pass.ps1"
PWSH = shutil.which("pwsh")

RECORDER = """\
import json, os, sys
root = os.environ.get("SLATE_LOCAL_APP_DATA", "")
recents_path = os.path.join(root, "recent-vaults.json")
recents = None
if os.path.exists(recents_path):
    with open(recents_path, "rb") as file:
        recents = json.loads(file.read().decode("utf-8"))
with open(os.path.join(os.environ["FIELD_PASS_TEST_OUT"], "launched.json"), "w", encoding="utf-8") as out:
    json.dump({"argv": sys.argv, "root": root, "recents": recents}, out)
"""


@unittest.skipUnless(sys.platform == "win32" and PWSH, "the launcher is a Windows PowerShell 7 script")
class LaunchForFieldPassTests(unittest.TestCase):
    def setUp(self) -> None:
        self.t = Path(tempfile.mkdtemp(prefix="field-pass-"))
        self.v1 = self.t / "My Documents" / "Notes"
        self.v2 = self.t / "Drop box" / "Notes"
        self.v1.mkdir(parents=True)
        self.v2.mkdir(parents=True)
        (self.v1 / "__main__.py").write_text(RECORDER, encoding="utf-8")

    def tearDown(self) -> None:
        shutil.rmtree(self.t, ignore_errors=True)

    def launch(self, *tokens: str) -> subprocess.CompletedProcess:
        env = dict(os.environ, TEMP=str(self.t), TMP=str(self.t), FIELD_PASS_TEST_OUT=str(self.t))
        env.pop("SLATE_LOCAL_APP_DATA", None)
        return subprocess.run(
            [PWSH, "-NoProfile", "-File", str(LAUNCHER), *tokens],
            capture_output=True, text=True, env=env, timeout=180)

    def launched(self) -> dict:
        return json.loads((self.t / "launched.json").read_text(encoding="utf-8"))

    def roots(self) -> list[Path]:
        return sorted(self.t.glob("slate-field-pass-*"))

    def printed_root(self, run: subprocess.CompletedProcess) -> str:
        lines = [line for line in run.stdout.splitlines() if line.startswith("Root: ")]
        self.assertEqual(len(lines), 1, run.stdout)
        return lines[0][len("Root: "):]

    def test_two_vaults_arrive_separately_and_in_order(self) -> None:
        run = self.launch("-Vault", f"{self.v1}|{self.v2}", "-Seed", "-Exe", sys.executable)
        self.assertEqual(run.returncode, 0, run.stdout + run.stderr)
        launched = self.launched()
        self.assertEqual(launched["argv"], [str(self.v1)])
        self.assertEqual(launched["root"], self.printed_root(run))
        recents = launched["recents"]
        self.assertEqual([entry["path"] for entry in recents], [str(self.v1), str(self.v2)])
        self.assertEqual([entry["displayName"] for entry in recents], ["Notes", "Notes"])
        self.assertGreater(recents[0]["lastOpenedMs"], recents[1]["lastOpenedMs"])
        self.assertFalse(Path(launched["root"]).exists(), "a fresh root without -Keep outlived the launch")

    def test_one_vault_seeds_one_entry_and_opens_it(self) -> None:
        run = self.launch("-Vault", str(self.v1), "-Seed", "-Exe", sys.executable)
        self.assertEqual(run.returncode, 0, run.stdout + run.stderr)
        launched = self.launched()
        self.assertIsInstance(launched["recents"], list)
        self.assertEqual([entry["path"] for entry in launched["recents"]], [str(self.v1)])
        self.assertEqual(launched["argv"], [str(self.v1)])

    def test_a_bad_vault_list_exits_2_and_starts_nothing(self) -> None:
        nine = []
        for index in range(9):
            folder = self.t / "nine" / f"v{index}"
            folder.mkdir(parents=True)
            nine.append(str(folder))
        cases = {
            "empty": ["-Vault", ""],
            "doubled separator": ["-Vault", f"{self.v1}||{self.v2}"],
            "trailing separator": ["-Vault", f"{self.v1}|"],
            "leading separator": ["-Vault", f"|{self.v1}"],
            "white-space entry": ["-Vault", f"{self.v1}| "],
            "relative entry": ["-Vault", f"{self.v1}|Notes"],
            "missing folder": ["-Vault", f"{self.v1}|{self.t / 'missing'}"],
            "one folder twice": ["-Vault", f"{self.v1}|{str(self.v1).upper()}\\"],
            "nine folders": ["-Vault", "|".join(nine)],
            "-Seed alone": [],
        }
        for name, tokens in cases.items():
            with self.subTest(name):
                run = self.launch(*tokens, "-Seed", "-Exe", sys.executable)
                self.assertEqual(run.returncode, 2, run.stdout + run.stderr)
                self.assertIn("launch-for-field-pass:", run.stderr)
                self.assertFalse((self.t / "launched.json").exists())
                self.assertEqual(self.roots(), [])

    def test_the_array_spelling_fails_before_anything_runs(self) -> None:
        # What PowerShell's `-Vault $v1,$v2` hands a native pwsh: two tokens.
        run = self.launch("-Vault", str(self.v1), str(self.v2), "-Seed", "-Exe", sys.executable)
        self.assertNotEqual(run.returncode, 0, run.stdout + run.stderr)
        self.assertIn("positional parameter", run.stderr)
        self.assertFalse((self.t / "launched.json").exists())
        self.assertEqual(self.roots(), [])
        self.assertFalse((self.v2 / "recent-vaults.json").exists())

    def test_a_reused_root_is_kept_and_read_again(self) -> None:
        root = self.t / "kept-root"
        root.mkdir()
        run = self.launch("-Vault", str(self.v1), "-Seed", "-Root", str(root), "-Exe", sys.executable)
        self.assertEqual(run.returncode, 0, run.stdout + run.stderr)
        self.assertTrue(root.is_dir(), "a reused root was deleted")
        again = self.launch("-Vault", str(self.v1), "-Root", str(root), "-Exe", sys.executable)
        self.assertEqual(again.returncode, 0, again.stdout + again.stderr)
        self.assertEqual([entry["path"] for entry in self.launched()["recents"]], [str(self.v1)])
        (root / "stray.txt").write_text("x", encoding="utf-8")
        refused = self.launch("-Vault", str(self.v1), "-Root", str(root), "-Exe", sys.executable)
        self.assertEqual(refused.returncode, 2, refused.stdout + refused.stderr)


if __name__ == "__main__":
    unittest.main()
