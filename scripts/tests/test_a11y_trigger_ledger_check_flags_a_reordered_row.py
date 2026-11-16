# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
"""#1699: the trigger ledger generator's `--check` catches a row placed by
hand, prints the rows to copy, and `--write` repairs it.

The census that guards contract 38's ledger compares a row's sites as a set,
so a row whose sites stand in another order than the generator writes them
passed every check until the lane ran `--check`. A contributor without a
Windows build cannot run the generator, so a stale `--check` prints each
differing row with the generator's row under it, and those printed rows,
pasted into the document, must pass.

Hermetic: a temp folder holds a two-key inventory, a designations file and a
prose-only document, and `--inventory` stands in for the `dotnet test` run
that exports the real one.
"""

import contextlib
import importlib.util
import io
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "a11y_trigger_ledger.py")

HAND_ORDER = "`A.cs#M@2`; `A.cs#M@1`; `B.cs#N@1`"
GENERATOR_ORDER = "`A.cs#M@1`; `A.cs#M@2`; `B.cs#N@1`"


def site(key, member, ordinal):
    return {"Key": key, "Member": member, "Ordinal": ordinal}


def inventory_data():
    return {
        "Keys": ["Alpha", "Beta"],
        "Windows": [site("Alpha", "A.cs#M", 1), site("Alpha", "A.cs#M", 2), site("Alpha", "B.cs#N", 1),
                    site("Beta", "C.cs#P", 1)],
        "Mac": [site("Alpha", "A.swift#m", 1), site("Beta", "C.swift#p", 1)],
    }


class LedgerCheckFlagsAReorderedRow(unittest.TestCase):
    def setUp(self):
        folder = tempfile.TemporaryDirectory()
        self.addCleanup(folder.cleanup)
        self.folder = Path(folder.name)
        self.inventory = self.folder / "inventory.json"
        self.config = self.folder / "designations.json"
        self.document = self.folder / "38_contracts.md"
        self.inventory.write_text(json.dumps(inventory_data()), encoding="utf-8")
        self.config.write_text(json.dumps({"designated": {"Windows": {}, "Mac": {}},
                                           "residue": {"Windows": {}, "Mac": {}}, "roles": {}}), encoding="utf-8")
        self.document.write_bytes("# Contract 38\n\nProse the generator leaves alone.\n".encode("utf-8"))

        spec = importlib.util.spec_from_file_location("a11y_trigger_ledger", SCRIPT)
        self.module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.module)
        self.module.CONFIG = self.config
        self.module.DOCUMENT = self.document

        self.run_main("--write")
        self.written = self.document.read_bytes()
        self.assertIn(GENERATOR_ORDER, self.written.decode("utf-8"), "premise: the generator sorts a row's sites")
        self.assertIn("current", self.run_main("--check"), "premise: a freshly written ledger does not check clean")

    def run_main(self, flag):
        """Run main() on the temp files; answer what it printed."""
        printed = io.StringIO()
        with contextlib.redirect_stdout(printed), contextlib.redirect_stderr(io.StringIO()):
            self.module.main([flag, "--inventory", str(self.inventory)])
        return printed.getvalue()

    def stale_check(self):
        """Run --check on a ledger that must be stale; answer the lines it printed before it exited."""
        printed = io.StringIO()
        with contextlib.redirect_stdout(printed), contextlib.redirect_stderr(io.StringIO()):
            with self.assertRaises(SystemExit) as stopped:
                self.module.main(["--check", "--inventory", str(self.inventory)])
        self.assertIsInstance(stopped.exception.code, str, "a stale ledger exits with a sentence, which is exit code 1")
        self.assertIn("stale", stopped.exception.code)
        return printed.getvalue().split("\n")

    def reorder_the_row(self):
        text = self.written.decode("utf-8")
        self.assertEqual(1, text.count(GENERATOR_ORDER))
        self.document.write_bytes(text.replace(GENERATOR_ORDER, HAND_ORDER).encode("utf-8"))

    def test_a_reordered_row_fails_the_check_and_both_orders_are_printed(self):
        self.reorder_the_row()
        row = next(number for number, line in enumerate(self.document.read_text(encoding="utf-8").split("\n"), 1)
                   if HAND_ORDER in line)

        lines = self.stale_check()

        removed = [line for line in lines if line.startswith("-|")]
        added = [line for line in lines if line.startswith("+|")]
        self.assertEqual(1, len(removed), lines)
        self.assertEqual(1, len(added), lines)
        self.assertIn(HAND_ORDER, removed[0])
        self.assertIn(GENERATOR_ORDER, added[0])
        self.assertEqual([f"@@ -{row} +{row} @@"], [line for line in lines if line.startswith("@@")],
                         "the header gives the row's line in the document")
        # The '+' row is the committed row, character for character.
        self.assertIn(added[0][1:], self.written.decode("utf-8").split("\n"))

    def test_write_repairs_a_reordered_row(self):
        self.reorder_the_row()
        self.stale_check()

        self.run_main("--write")

        self.assertIn("current", self.run_main("--check"))
        self.assertEqual(self.written, self.document.read_bytes())

    def test_the_printed_rows_pasted_into_the_document_pass_the_check(self):
        # The route of a contributor with no Windows build: the lane's log is all there is.
        data = inventory_data()
        data["Windows"].append(site("Alpha", "A.cs#M", 3))
        self.inventory.write_text(json.dumps(data), encoding="utf-8")

        lines = self.stale_check()

        removed = [line[1:] for line in lines if line.startswith("-|")]
        added = [line[1:] for line in lines if line.startswith("+|")]
        self.assertEqual(1, len(removed), lines)
        self.assertEqual(1, len(added), lines)
        self.assertIn("`A.cs#M@3`", added[0])
        document = self.document.read_text(encoding="utf-8").split("\n")
        for old, new in zip(removed, added):
            self.assertEqual(1, document.count(old))
            document[document.index(old)] = new
        self.document.write_bytes("\n".join(document).encode("utf-8"))

        self.assertIn("current", self.run_main("--check"))

    def test_a_piped_check_prints_its_rows_as_utf_8(self):
        # Nearly every row holds an em dash. The lane runs the script as `python -I`, which ignores PYTHONUTF8, and
        # a Windows pipe defaults to the ANSI code page: a row printed in it would not paste back into the document.
        self.reorder_the_row()
        driver = (
            "import importlib.util, pathlib, sys\n"
            "spec = importlib.util.spec_from_file_location('a11y_trigger_ledger', sys.argv[1])\n"
            "module = importlib.util.module_from_spec(spec)\n"
            "spec.loader.exec_module(module)\n"
            "module.CONFIG = pathlib.Path(sys.argv[2])\n"
            "module.DOCUMENT = pathlib.Path(sys.argv[3])\n"
            "module.main(['--check', '--inventory', sys.argv[4]])\n"
        )
        run = subprocess.run([sys.executable, "-I", "-c", driver, SCRIPT, str(self.config), str(self.document),
                              str(self.inventory)], stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False)

        self.assertEqual(1, run.returncode, run.stderr)
        added = [line for line in run.stdout.decode("utf-8").splitlines() if line.startswith("+|")]
        self.assertEqual(1, len(added), run.stdout)
        self.assertIn("—", added[0], "premise: the row holds an em dash")
        self.assertIn(added[0][1:], self.written.decode("utf-8").split("\n"))
        # The sentence that says what to do with the rows is the exit message, on stderr.
        self.assertIn(b"stale", run.stderr)


if __name__ == "__main__":
    unittest.main()
