# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Facts for scripts/check_windows_census_trait.py (#1945).

Run:  python3 -I -m unittest discover -v -s scripts/tests -p test_windows_census_trait.py
"""

import contextlib
import io
from pathlib import Path
import sys
import tempfile
import textwrap
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import check_windows_census_trait as census  # noqa: E402

WORKFLOW = textwrap.dedent(
    """\
    name: windows

    on:
      pull_request:
      push:
        branches: [main]
        paths:
          - 'apps/slate-windows/**'
          - 'crates/**'
          - 'docs/plans/38_x_contracts.md'
          - 'docs/plans/18_windows_port/**'
          - 'apps/slate-mac/**'
          - 'BENCHMARKS.md'

    jobs:
      changes:
        name: changes
        uses: ./.github/workflows/lane-changes.yml
        with:
          workflow: .github/workflows/windows.yml
          census-paths: |
            apps/slate-mac/**
            docs/plans/38_x_contracts.md
            docs/plans/18_windows_port/**
            BENCHMARKS.md
      noop:
        needs: [changes]
        runs-on: ubuntu-latest
    """
)

TESTS = "apps/slate-windows/tests/SlateWindows.Tests"


def test_class(name: str, body: str, tagged: bool = False, attributes: str = "") -> str:
    trait = '[Trait("tier", "census")]\n' if tagged else ""
    return textwrap.dedent(
        """\
        namespace SlateWindows.Tests;

        {attributes}{trait}public sealed class {name}
        {{
        {body}
            [Fact]
            public void Holds() {{ }}
        }}
        """
    ).format(name=name, body=textwrap.indent(textwrap.dedent(body), "    "), trait=trait, attributes=attributes)


def make_repo(root: Path, files: dict[str, str], workflow: str = WORKFLOW) -> Path:
    (root / ".github/workflows").mkdir(parents=True)
    (root / ".github/workflows/windows.yml").write_text(workflow, encoding="utf-8")
    for rel, text in files.items():
        (root / rel).parent.mkdir(parents=True, exist_ok=True)
        (root / rel).write_text(text, encoding="utf-8")
    return root


def problems(files: dict[str, str], workflow: str = WORKFLOW) -> list[str]:
    with tempfile.TemporaryDirectory() as tmp:
        return census.analyse(make_repo(Path(tmp), files, workflow)).problems


MAC_READ = 'private static string Mac => Path.Combine(SourceText.RepoRoot(), "apps", "slate-mac", "Sources", "App.swift");'


class ScrubFacts(unittest.TestCase):
    def test_comments_are_blanked_and_offsets_kept(self):
        text = 'a // "apps/slate-mac"\nb /* "x" */ c "y"\n'
        no_comments, code, literals = census.scrub(text)
        self.assertEqual(len(text), len(no_comments))
        self.assertEqual(len(text), len(code))
        self.assertNotIn("slate-mac", no_comments)
        self.assertEqual(["y"], [literal.value for literal in literals])
        self.assertEqual('"y"', text[literals[0].start:literals[0].end])

    def test_every_kind_of_string_is_a_literal_and_its_contents_leave_the_code(self):
        text = 'f(@"a\\b""c", "d\\"e", $"g{h}i", """raw "q" {}""", \'"\', "k{")'
        _, code, literals = census.scrub(text)
        self.assertEqual(['a\\b"c', 'd"e', "g{h}i", 'raw "q" {}', "k{"], [literal.value for literal in literals])
        self.assertTrue(literals[2].interpolated)
        self.assertNotIn("{", code.replace("{h}", ""), "a brace inside a string is not code")


class ReadFacts(unittest.TestCase):
    def test_an_untagged_test_that_reads_a_mac_source_fails_and_a_tagged_one_passes(self):
        found = problems({f"{TESTS}/MacTests.cs": test_class("MacTests", MAC_READ)})
        self.assertEqual(1, len(found), found)
        self.assertIn("MacTests reads apps/slate-mac/Sources/App.swift", found[0])
        self.assertIn('[Trait("tier", "census")]', found[0])
        self.assertEqual([], problems({f"{TESTS}/MacTests.cs": test_class("MacTests", MAC_READ, tagged=True)}))

    def test_the_trait_may_share_an_attribute_list(self):
        source = test_class("MacTests", MAC_READ).replace(
            "public sealed class", '[Trait("census", "mac"), Trait("tier", "census")]\npublic sealed class')
        self.assertEqual([], problems({f"{TESTS}/MacTests.cs": source}))

    def test_the_trait_of_the_type_before_does_not_tag_the_next_one(self):
        source = test_class("Tagged", "int n = 1;", tagged=True) + test_class("MacTests", MAC_READ).split("\n", 2)[2]
        found = problems({f"{TESTS}/Two.cs": source})
        self.assertEqual(1, len(found), found)
        self.assertIn("MacTests reads", found[0])

    def test_a_trait_with_another_value_or_on_a_method_is_not_the_census_trait(self):
        for attributes in ('[Trait("tier", "full")]\n', '[Trait("census", "mac")]\n'):
            with self.subTest(attributes=attributes):
                found = problems({f"{TESTS}/MacTests.cs": test_class("MacTests", MAC_READ, attributes=attributes)})
                self.assertEqual(1, len(found), found)

    def test_each_shape_of_read_is_found(self):
        shapes = {
            "a literal path": 'string p = "apps/slate-mac/Sources/App.swift";',
            "a Windows separator": 'string p = @"apps\\slate-mac\\Sources\\App.swift";',
            "a constant in the chain": 'const string Doc = "38_x_contracts.md";\nstring p = Path.Combine(r, "docs", "plans", Doc);',
            "a plan-relative literal": 'string p = ReadPlan("18_windows_port/w_c_matrix.md");',
            # docs/plans is not itself census-only, but a computed name under it can be.
            "a census folder with a computed tail": 'string p = Path.Combine(r, "docs", "plans", name);',
            "a root file": 'string p = Path.Combine(SourceText.RepoRoot(), "BENCHMARKS.md");',
            "an interpolated path into a census folder": 'string p = Path.Combine(r, $"apps/slate-mac/{name}");',
            "an interpolated path under docs/plans": 'string p = $"docs/plans/{name}";',
            "a chain split over lines": 'string p = Path.Combine(\n    r,\n    "docs",\n    "plans",\n    "18_windows_port");',
        }
        for shape, body in shapes.items():
            with self.subTest(shape=shape):
                found = problems({f"{TESTS}/ReadTests.cs": test_class("ReadTests", body)})
                self.assertEqual(1, len(found), found)
                self.assertIn("ReadTests reads", found[0])

    def test_what_is_not_a_census_read(self):
        shapes = {
            "a comment": "// apps/slate-mac/Sources/App.swift\n/// <c>docs/plans/38_x_contracts.md</c>",
            "a message": 'string m = "see docs/plans/38_x_contracts.md for why";',
            "the crate's fixtures": 'string p = Path.Combine(r, "crates", "slate-core", "tests", "fixtures", "a.md");',
            "a vault's own docs folder": 'string p = Path.Combine(vault, "docs", name);',
            "a plan the lane does not list as census": 'string p = Path.Combine(r, "docs", "plans", "44_plan.md");',
            "the Windows sources": 'string p = Path.Combine(r, "apps", "slate-windows", "src");',
            "an interpolated message": 'string m = $"{count} files under docs/plans/18_windows_port";',
        }
        for shape, body in shapes.items():
            with self.subTest(shape=shape):
                self.assertEqual([], problems({f"{TESTS}/ReadTests.cs": test_class("ReadTests", body)}))


class OwnerFacts(unittest.TestCase):
    HELPER = textwrap.dedent(
        """\
        namespace SlateWindows.Tests;

        internal static class MacInventory
        {
            internal static string Root() => Path.Combine(SourceText.RepoRoot(), "apps", "slate-mac", "Sources");
        }
        """
    )

    def test_a_helper_passes_the_duty_to_every_test_that_names_it(self):
        files = {
            f"{TESTS}/Support/MacInventory.cs": self.HELPER,
            f"{TESTS}/UsesHelperTests.cs": test_class("UsesHelperTests", "string r = MacInventory.Root();"),
            f"{TESTS}/OtherTests.cs": test_class("OtherTests", "int n = 1;"),
        }
        found = problems(files)
        self.assertEqual(1, len(found), found)
        self.assertIn("UsesHelperTests names MacInventory", found[0])

    def test_the_duty_passes_through_a_chain_of_helpers(self):
        files = {
            f"{TESTS}/Support/MacInventory.cs": self.HELPER,
            f"{TESTS}/Support/Wrapper.cs": "namespace X;\ninternal static class Wrapper\n{\n    internal static string R() => MacInventory.Root();\n}\n",
            f"{TESTS}/UsesWrapperTests.cs": test_class("UsesWrapperTests", "string r = Wrapper.R();"),
        }
        found = problems(files)
        self.assertEqual(1, len(found), found)
        self.assertIn("UsesWrapperTests names Wrapper", found[0])

    def test_a_helper_no_test_reaches_fails(self):
        found = problems({f"{TESTS}/Support/MacInventory.cs": self.HELPER})
        self.assertEqual(1, len(found), found)
        self.assertIn("no test names it", found[0])

    def test_one_tagged_part_of_a_partial_class_tags_the_class(self):
        files = {
            f"{TESTS}/SplitTests.cs": '[Trait("tier", "census")]\npublic sealed partial class SplitTests\n{\n    [Fact] public void A() { }\n}\n',
            f"{TESTS}/SplitTests.Mac.cs": "public sealed partial class SplitTests\n{\n    " + MAC_READ + "\n}\n",
        }
        self.assertEqual([], problems(files))

    def test_a_read_outside_the_test_project_fails(self):
        for project in ("apps/slate-windows/src/SlateWindows", "apps/slate-windows/tests/SlateWindows.AccessibilityTests"):
            with self.subTest(project=project):
                found = problems({f"{project}/Reader.cs": "namespace X;\ninternal static class Reader\n{\n    " + MAC_READ + "\n}\n"})
                self.assertEqual(1, len(found), found)
                self.assertIn("outside the test project", found[0])


class WorkflowFacts(unittest.TestCase):
    def test_a_lane_without_a_census_list_fails(self):
        found = problems({}, WORKFLOW.split("      census-paths: |")[0] + "  noop:\n    runs-on: ubuntu-latest\n")
        self.assertEqual(1, len(found), found)
        self.assertIn("no census-paths block", found[0])

    def test_a_census_path_outside_the_lane_fails(self):
        found = problems({}, WORKFLOW.replace("        BENCHMARKS.md\n  noop", "        BENCHMARKS.md\n        docs/help/**\n  noop"))
        self.assertEqual(1, len(found), found)
        self.assertIn("not in on.push.paths", found[0])


TRX = textwrap.dedent(
    """\
    <?xml version="1.0" encoding="utf-8"?>
    <TestRun id="r" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
      <Results>
        {results}
      </Results>
      <TestDefinitions>
        <UnitTest name="a" id="t1"><TestMethod className="SlateWindows.Tests.MacTests" name="Holds" /></UnitTest>
        <UnitTest name="b" id="t2"><TestMethod className="SlateWindows.Tests.Other+Nested" name="Holds" /></UnitTest>
      </TestDefinitions>
    </TestRun>
    """
)


class TrxFacts(unittest.TestCase):
    def run_main(self, results: str) -> tuple[int, str]:
        with tempfile.TemporaryDirectory() as tmp:
            root = make_repo(Path(tmp), {f"{TESTS}/MacTests.cs": test_class("MacTests", MAC_READ, tagged=True)})
            trx = root / "run.trx"
            trx.write_text(TRX.format(results=results), encoding="utf-8")
            out = io.StringIO()
            with contextlib.redirect_stdout(out):
                code = census.main(["check", "--trx", str(trx)], root)
            return code, out.getvalue()

    def test_the_executed_classes_are_read_from_the_results(self):
        with tempfile.TemporaryDirectory() as tmp:
            trx = Path(tmp) / "run.trx"
            trx.write_text(TRX.format(results='<UnitTestResult testId="t1" outcome="Passed" />'
                                      '<UnitTestResult testId="t2" outcome="Failed" />'), encoding="utf-8")
            self.assertEqual({"MacTests", "Other"}, census.trx_classes(trx))

    def test_a_census_run_that_ran_every_tagged_class_passes(self):
        code, out = self.run_main('<UnitTestResult testId="t1" outcome="Passed" />')
        self.assertEqual(0, code, out)

    def test_a_census_run_that_missed_a_tagged_class_fails(self):
        # A filter that names the wrong trait still passes dotnet test: it runs other tests, or none.
        code, out = self.run_main('<UnitTestResult testId="t2" outcome="Passed" /><UnitTestResult testId="t1" outcome="NotExecuted" />')
        self.assertEqual(1, code, out)
        self.assertIn("MacTests", out)

    def test_a_census_run_that_ran_nothing_fails(self):
        code, out = self.run_main("")
        self.assertEqual(1, code, out)


class RepositoryFacts(unittest.TestCase):
    def test_the_checks_the_issue_names_are_census_readers(self):
        result = census.analyse()
        self.assertEqual([], result.problems)
        for name in ("A11yTriggerParityCensus", "MacCatalogParityTests", "CanvasTriggerParityCensus",
                     "GraphTriggerParityCensus", "AtNavigationMapCensus", "BenchmarksRollupCensus"):
            self.assertIn(name, result.required)
            self.assertIn(name, result.tagged)


if __name__ == "__main__":
    unittest.main()
