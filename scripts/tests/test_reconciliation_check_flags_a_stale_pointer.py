# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
"""WB-17 (#1376, #1386): the reconciliation generators' `--check` catches a
register record that moved, and `--write` repairs it.

Each generator's (c) index points at the line each divergence record sits on.
A record that moves by one line — an edit above it — leaves the committed
pointer stale, which is how contracts 34 and 35 drifted before CI ran the
checks. Each case works on a copy of its contract: normalised by `--write` (so
the copy is the generator's output on this tree, whatever the binding), then
a line inserted above the first record, after which `--check` must exit 1 and
`--write` bring it back to 0.
"""

import contextlib
import importlib.util
import io
import os
import shutil
import tempfile
import unittest

SCRIPTS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def load(name):
    spec = importlib.util.spec_from_file_location(name, os.path.join(SCRIPTS, f"{name}.py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class ReconciliationCheckFlagsAStalePointer(unittest.TestCase):
    def check_flags_a_moved_record(self, name, first_record):
        module = load(name)
        with tempfile.TemporaryDirectory() as folder:
            copy = os.path.join(folder, os.path.basename(module.DOC))
            shutil.copyfile(module.DOC, copy)
            module.DOC = copy

            def run(flag):
                with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                    return module.main([flag])

            self.assertEqual(run("--write"), 0)
            self.assertEqual(run("--check"), 0, "premise: a freshly written copy does not check clean")

            with open(copy, encoding="utf-8") as handle:
                text = handle.read()
            at = text.index(first_record)
            with open(copy, "w", encoding="utf-8", newline="\n") as handle:
                handle.write(text[:at] + "\n" + text[at:])

            self.assertEqual(run("--check"), 1, "a record moved by one line, and --check did not notice")
            self.assertEqual(run("--write"), 0)
            self.assertEqual(run("--check"), 0, "--write did not repair the moved record's pointer")

    def test_the_canvas_register(self):
        self.check_flags_a_moved_record("canvas_reconciliation", "\n**CD-1 — ")

    def test_the_graph_register(self):
        self.check_flags_a_moved_record("graph_reconciliation", "\n- **A-D1 — ")


if __name__ == "__main__":
    unittest.main()
