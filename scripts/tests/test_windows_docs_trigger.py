# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Facts for scripts/check_windows_docs_trigger.py."""

from pathlib import Path
import sys
import tempfile
import textwrap
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import check_windows_docs_trigger as trigger  # noqa: E402

WORKFLOW = textwrap.dedent(
    """\
    name: windows

    on:
      pull_request:
      push:
        branches: [main]
        paths:
          - 'apps/slate-windows/**'
          # a comment between items
          - 'docs/plans/*.md'
          - 'docs/plans/18_windows_port/**'
      workflow_dispatch:

    jobs:
      noop:
        runs-on: ubuntu-latest
    """
)


def script_workflow(command: str) -> str:
    """The fixture workflow with one step that runs scripts/reader.py."""
    return WORKFLOW + f"    steps:\n      - name: Check\n        run: {command} scripts/reader.py --check\n"


def make_repo(root: Path, workflow: str = WORKFLOW, source: str = "", script: str | None = None) -> Path:
    (root / ".github/workflows").mkdir(parents=True)
    (root / ".github/workflows/windows.yml").write_text(workflow, encoding="utf-8")
    if script is not None:
        (root / "scripts").mkdir()
        (root / "scripts/reader.py").write_text(script, encoding="utf-8")
    port = root / "docs/plans/18_windows_port"
    port.mkdir(parents=True)
    (port / "a11y_trigger_designations.json").write_text(
        '{"designated": {"Mac": {"X": {"decision": "docs/plans/38_ledger.md", "status": "recorded"}}}}',
        encoding="utf-8",
    )
    (port / "at_navigation_map.md").write_text(
        "| a | b | reports/w1_shell_at_checklist.md#1 |\n| c | ../25_grid.md |\n", encoding="utf-8"
    )
    windows = root / "apps/slate-windows/tests"
    windows.mkdir(parents=True)
    (windows / "Census.cs").write_text(source, encoding="utf-8")
    return root


class GlobFacts(unittest.TestCase):
    def test_a_single_star_does_not_cross_a_folder(self):
        self.assertTrue(trigger.covered("docs/plans/35_graph_contracts.md", ["docs/plans/*.md"]))
        self.assertFalse(trigger.covered("docs/plans/43_mac_backlog_delivery/00_program.md", ["docs/plans/*.md"]))

    def test_an_exact_entry_covers_only_that_file(self):
        exact = ["docs/plans/35_graph_contracts.md"]
        self.assertTrue(trigger.covered("docs/plans/35_graph_contracts.md", exact))
        self.assertFalse(trigger.covered("docs/plans/36_cancellable_file_management_contracts.md", exact))
        self.assertFalse(trigger.covered("docs/plans/44_windows_backlog_plan.md", exact))

    def test_the_lane_trigger_names_no_top_level_plan_glob(self):
        # Top-level plan documents are listed by name, so a plan no check
        # reads (plan 44, an unread contracts document) starts nothing.
        lists = trigger.trigger_lists((trigger.REPO / trigger.WORKFLOW).read_text(encoding="utf-8"))
        self.assertNotIn("docs/plans/*.md", lists["push"])
        self.assertFalse(trigger.covered("docs/plans/44_windows_backlog_plan.md", lists["push"]))
        self.assertTrue(trigger.covered("docs/plans/35_graph_contracts.md", lists["push"]))

    def test_a_double_star_matches_any_depth(self):
        self.assertTrue(trigger.covered("docs/plans/18_windows_port/reports/w1.md", ["docs/plans/18_windows_port/**"]))

    def test_a_later_negation_excludes_and_the_last_match_decides(self):
        patterns = ["docs/**", "!docs/plans/43_x/**", "docs/plans/43_x/keep.md"]
        self.assertFalse(trigger.covered("docs/plans/43_x/a.md", patterns))
        self.assertTrue(trigger.covered("docs/plans/43_x/keep.md", patterns))

    def test_a_folder_read_requires_the_whole_folder(self):
        self.assertEqual("docs/help/any/file.md", trigger.requirement("docs/help"))
        self.assertEqual("docs/plans/a.md", trigger.requirement("docs/plans/a.md"))


class ParserFacts(unittest.TestCase):
    def test_the_push_list_is_read_in_order_without_comments(self):
        lists = trigger.trigger_lists(WORKFLOW)
        expected = ["apps/slate-windows/**", "docs/plans/*.md", "docs/plans/18_windows_port/**"]
        self.assertEqual(expected, lists["push"])
        self.assertNotIn("pull_request", lists)


class CheckFacts(unittest.TestCase):
    def test_the_repository_trigger_covers_every_docs_read(self):
        self.assertEqual([], trigger.check())

    def test_the_plan_43_design_documents_do_not_start_the_lane(self):
        lists = trigger.trigger_lists((trigger.REPO / trigger.WORKFLOW).read_text(encoding="utf-8"))
        self.assertFalse(trigger.covered("docs/plans/43_mac_backlog_delivery/20_vault_watcher_design.md", lists["push"]))
        self.assertTrue(trigger.covered("docs/plans/38_notification_dispatcher_contracts.md", lists["push"]))
        self.assertTrue(trigger.covered("docs/plans/18_windows_port/w_c_matrix.md", lists["push"]))

    def test_a_census_reading_an_uncovered_document_fails(self):
        source = 'File.ReadAllText(Path.Combine(Root, "docs", "plans", "43_mac_backlog_delivery", "x.md"));'
        with tempfile.TemporaryDirectory() as tmp:
            problems = trigger.check(make_repo(Path(tmp), source=source))
        self.assertTrue(any("docs/plans/43_mac_backlog_delivery/x.md" in p for p in problems), problems)

    def test_a_constant_passed_to_a_helper_is_resolved(self):
        source = textwrap.dedent(
            """\
            private const string SpecDoc = "43_mac_backlog_delivery/specs/pr19.md";
            private static string ReadPlan(string relative) =>
                File.ReadAllText(Path.Combine(Root, "docs", "plans", relative));
            string text = ReadPlan(SpecDoc);
            """
        )
        with tempfile.TemporaryDirectory() as tmp:
            problems = trigger.check(make_repo(Path(tmp), source=source))
        self.assertTrue(any("43_mac_backlog_delivery/specs/pr19.md" in p for p in problems), problems)

    def test_a_dynamic_read_with_no_plan_relative_literal_fails(self):
        source = 'string Read(string r) => File.ReadAllText(Path.Combine(Root, "docs", "plans", r));'
        with tempfile.TemporaryDirectory() as tmp:
            problems = trigger.check(make_repo(Path(tmp), source=source))
        self.assertTrue(any("built from `r` at run time" in p for p in problems), problems)

    def test_a_dynamic_read_beside_an_unrelated_plan_literal_still_fails(self):
        # Codoki on #1459: an unrelated plan-relative literal in the same file
        # must not hide a read the check cannot resolve.
        source = textwrap.dedent(
            """\
            private const string Other = "35_graph_contracts.md";
            string Read(string r) => File.ReadAllText(Path.Combine(Root, "docs", "plans", r));
            """
        )
        with tempfile.TemporaryDirectory() as tmp:
            problems = trigger.check(make_repo(Path(tmp), source=source))
        self.assertTrue(any("built from `r` at run time" in p for p in problems), problems)

    def test_a_helper_called_with_an_unresolvable_argument_fails(self):
        source = textwrap.dedent(
            """\
            private const string SpecDoc = "18_windows_port/specs/w1_spec.md";
            static string ReadPlan(string relative) =>
                File.ReadAllText(Path.Combine(Root, "docs", "plans", relative));
            string a = ReadPlan(SpecDoc);
            string b = ReadPlan(Compute());
            """
        )
        with tempfile.TemporaryDirectory() as tmp:
            problems = trigger.check(make_repo(Path(tmp), source=source))
        self.assertTrue(any("built from `relative` at run time" in p for p in problems), problems)

    def test_every_resolved_call_of_a_helper_is_a_read(self):
        source = textwrap.dedent(
            """\
            private const string SpecDoc = "18_windows_port/specs/w1_spec.md";
            static string ReadPlan(string relative) =>
                File.ReadAllText(Path.Combine(Root, "docs", "plans", relative));
            string a = ReadPlan(SpecDoc);
            string b = ReadPlan("44_new_plan/notes.md");
            """
        )
        with tempfile.TemporaryDirectory() as tmp:
            problems = trigger.check(make_repo(Path(tmp), source=source))
        self.assertTrue(any("docs/plans/44_new_plan/notes.md" in p for p in problems), problems)
        self.assertFalse(any("w1_spec.md" in p for p in problems), problems)

    def test_ledger_decisions_and_map_links_must_be_covered(self):
        with tempfile.TemporaryDirectory() as tmp:
            problems = trigger.check(make_repo(Path(tmp)))
        # Both resolve under the fixture's trigger: 38_ledger.md is top level,
        # the checklist is under 18_windows_port, ../25_grid.md is top level.
        self.assertEqual([], problems)
        narrow = WORKFLOW.replace("      - 'docs/plans/*.md'\n", "")
        with tempfile.TemporaryDirectory() as tmp:
            problems = trigger.check(make_repo(Path(tmp), workflow=narrow))
        self.assertTrue(any("docs/plans/38_ledger.md" in p for p in problems), problems)
        self.assertTrue(any("docs/plans/25_grid.md" in p for p in problems), problems)

    def test_a_pull_request_path_list_fails(self):
        # #1565: lane-changes.yml decides pull requests from on.push.paths; a second list would filter some
        # pull requests out before the decision, and the lane would not report on them.
        filtered = WORKFLOW.replace("  pull_request:\n", "  pull_request:\n    paths:\n      - 'apps/slate-windows/**'\n")
        with tempfile.TemporaryDirectory() as tmp:
            problems = trigger.check(make_repo(Path(tmp), workflow=filtered))
        self.assertNotEqual(WORKFLOW, filtered)
        self.assertTrue(any("on.pull_request.paths" in p for p in problems), problems)

    def test_a_workflow_without_a_push_list_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            problems = trigger.check(make_repo(Path(tmp), workflow=WORKFLOW.replace("    paths:\n", "    tags:\n")))
        self.assertTrue(any("on.push.paths not found" in p for p in problems), problems)

    def test_a_script_run_as_python_dash_i_is_scanned(self):
        # #1699: the app job runs its generators as `python -I scripts/...`, and the scan read only `python3 scripts/...`.
        script = 'DOC = "docs/plans/43_mac_backlog_delivery/x.md"\n'
        for command in ("python3", "python", "python -I", "python3 -I", "python  -I"):
            with self.subTest(command=command), tempfile.TemporaryDirectory() as tmp:
                problems = trigger.check(make_repo(Path(tmp), workflow=script_workflow(command), script=script))
                self.assertTrue(any("docs/plans/43_mac_backlog_delivery/x.md" in p and "scripts/reader.py" in p
                                    for p in problems), problems)

    def test_a_docs_path_a_script_joins_from_segments_is_a_read(self):
        # The reconciliation generators name their documents this way.
        for script in ('DOC = os.path.join(ROOT, "docs", "plans", "43_mac_backlog_delivery", "x.md")\n',
                       "DOC = os.path.join(ROOT, 'docs', 'plans', '43_mac_backlog_delivery', 'x.md')\n",
                       'DOC = os.path.join(\n    ROOT,\n    "docs",\n    "plans",\n    "43_mac_backlog_delivery",  # the folder\n    "x.md",\n)\n',
                       'DOC = "/".join(["docs", "plans", "43_mac_backlog_delivery/x.md"])\n',
                       'DOC = ROOT / "docs" / "plans" / "43_mac_backlog_delivery" / "x.md"\n',
                       # A segment may hold several folders.
                       'DOC = os.path.join(ROOT, "docs", "plans/43_mac_backlog_delivery/x.md")\n',
                       'DOC = ROOT / "docs" / "plans/43_mac_backlog_delivery" / "x.md"\n'):
            with self.subTest(script=script), tempfile.TemporaryDirectory() as tmp:
                problems = trigger.check(make_repo(Path(tmp), workflow=script_workflow("python -I"), script=script))
                self.assertTrue(any("docs/plans/43_mac_backlog_delivery/x.md" in p and "scripts/reader.py" in p
                                    for p in problems), problems)

    def test_a_path_that_climbs_out_of_a_covered_folder_is_checked_where_it_lands(self):
        # docs/help/../plans/x.md is spelled under docs/help, which the fixture covers, and names a plan it does not.
        covering = WORKFLOW.replace("      - 'docs/plans/*.md'\n", "      - 'docs/help/**'\n")
        landed = "docs/plans/43_mac_backlog_delivery.md"
        scripts = ('DOC = ROOT / "docs" / "help" / ".." / "plans" / "43_mac_backlog_delivery.md"\n',
                   'DOC = os.path.join(ROOT, "docs", "help", "..", "plans", "43_mac_backlog_delivery.md")\n',
                   'DOC = ROOT / "docs/help/../plans/43_mac_backlog_delivery.md"\n')
        for script in scripts:
            with self.subTest(script=script), tempfile.TemporaryDirectory() as tmp:
                workflow = covering + "    steps:\n      - name: Check\n        run: python -I scripts/reader.py\n"
                problems = trigger.check(make_repo(Path(tmp), workflow=workflow, script=script))
                self.assertTrue(any(p.startswith(f"{landed} is read") and "scripts/reader.py" in p for p in problems), problems)
        sources = ('File.ReadAllText(Path.Combine(Root, "docs", "help", "..", "plans", "43_mac_backlog_delivery.md"));',
                   'File.ReadAllText(Path.Combine(Root, "docs/help/../plans/43_mac_backlog_delivery.md"));')
        for source in sources:
            with self.subTest(source=source), tempfile.TemporaryDirectory() as tmp:
                problems = trigger.check(make_repo(Path(tmp), workflow=covering, source=source))
                self.assertTrue(any(p.startswith(f"{landed} is read") for p in problems), problems)

    def test_the_census_scans_the_three_generators_the_app_job_checks(self):
        workflow_text = (trigger.REPO / trigger.WORKFLOW).read_text(encoding="utf-8")
        reads, unparsed = trigger.script_reads(trigger.REPO, workflow_text)
        self.assertEqual([], unparsed)
        for document, script in (("docs/plans/34_canvas_contracts.md", "scripts/canvas_reconciliation.py"),
                                 ("docs/plans/35_graph_contracts.md", "scripts/graph_reconciliation.py"),
                                 ("docs/plans/38_notification_dispatcher_contracts.md", "scripts/a11y_trigger_ledger.py")):
            self.assertIn(script, reads.get(document, set()), reads)

    def test_a_chain_with_a_non_literal_segment_is_skipped_whole(self):
        # Read as its literal prefix, such a chain would name the folder docs/plans, which the lane's list does not
        # cover and must not (test_the_lane_trigger_names_no_top_level_plan_glob). scripts/check_windows_census_trait.py
        # holds two of them, and neither is a read.
        for script in ('PLAN = os.path.join(ROOT, "docs", "plans", name)\n',
                       'plan = "/".join(("docs", "plans", value))\n',
                       'PLAN = os.path.join(ROOT, "docs", "plans", "43_mac_backlog_delivery", name)\n',
                       'PLAN = os.path.join(ROOT, "docs", "plans", f"{number}_contracts.md")\n',
                       'PLAN = os.path.join(ROOT, "docs", "plans", *parts)\n',
                       'PLAN = os.path.join(ROOT, "docs", "plans", "43_x" + suffix)\n',
                       'PLAN = ROOT / "docs" / "plans" / name\n'):
            with self.subTest(script=script), tempfile.TemporaryDirectory() as tmp:
                repo = make_repo(Path(tmp), workflow=script_workflow("python -I"), script=script)
                self.assertEqual(({}, []), trigger.script_reads(repo, script_workflow("python -I")))
                self.assertFalse(any("scripts/reader.py" in p for p in trigger.check(repo)))

    def test_a_chain_in_a_script_s_docstring_or_comment_is_not_a_read(self):
        script = '"""Joins like `os.path.join(root, "docs", "plans", "43_x.md")`."""\n# ("docs", "plans", "43_y.md")\n'
        with tempfile.TemporaryDirectory() as tmp:
            repo = make_repo(Path(tmp), workflow=script_workflow("python -I"), script=script)
            self.assertEqual(({}, []), trigger.script_reads(repo, script_workflow("python -I")))

    def test_a_script_the_scan_cannot_parse_fails(self):
        # A script the scan cannot read is a script whose docs paths nobody checked.
        with tempfile.TemporaryDirectory() as tmp:
            repo = make_repo(Path(tmp), workflow=script_workflow("python -I"), script="def broken(:\n")
            problems = trigger.check(repo)
        self.assertTrue(any("scripts/reader.py" in p and "cannot be parsed" in p for p in problems), problems)


if __name__ == "__main__":
    unittest.main()
