# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Facts for scripts/lane_changes.py (#1565).

Run:  python3 -I -m unittest discover -v -s scripts/tests -p test_lane_changes.py
"""

import os
from pathlib import Path
import subprocess
import sys
import tempfile
import textwrap
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import lane_changes  # noqa: E402

SCRIPT = Path(__file__).resolve().parent.parent / "lane_changes.py"

WORKFLOW = textwrap.dedent(
    """\
    name: lane

    on:
      # The lane's paths are on.push.paths (#1565).
      pull_request:
      push:
        branches: [main]
        paths:
          - 'crates/**'
          # a comment between items
          - '**/*.rs'
          - '.github/workflows/lane.yml'

    jobs:
      noop:
        runs-on: ubuntu-latest
    """
)

# A lane with a census tier (#1945): its changes job hands lane-changes.yml the
# census-only patterns, a sub-list of on.push.paths.
CENSUS_WORKFLOW = textwrap.dedent(
    """\
    name: lane

    on:
      pull_request:
      push:
        branches: [main]
        paths:
          - 'crates/**'
          - 'docs/plans/18_x/**'
          - 'BENCHMARKS.md'
          - '.github/workflows/lane.yml'

    jobs:
      changes:
        name: changes
        uses: ./.github/workflows/lane-changes.yml
        permissions:
          contents: read
        with:
          workflow: .github/workflows/lane.yml
          census-paths: |
            # The plan folder the censuses read.
            docs/plans/18_x/**

            BENCHMARKS.md
      noop:
        needs: [changes]
        runs-on: ubuntu-latest
    """
)


class GitHubPatternFacts(unittest.TestCase):
    """GitHub's filter-pattern cheat sheet, for the syntax the lanes use."""

    def test_a_single_star_stays_in_its_folder(self):
        self.assertTrue(lane_changes.covered("README.md", ["*.md"]))
        self.assertFalse(lane_changes.covered("docs/README.md", ["*.md"]))
        self.assertTrue(lane_changes.covered("docs/plans/35_graph_contracts.md", ["docs/plans/*.md"]))
        self.assertFalse(lane_changes.covered("docs/plans/43_x/00_program.md", ["docs/plans/*.md"]))

    def test_a_trailing_double_star_matches_every_file_beneath(self):
        self.assertTrue(lane_changes.covered("crates/slate-core/src/lib.rs", ["crates/**"]))
        self.assertFalse(lane_changes.covered("crates2/lib.rs", ["crates/**"]))
        self.assertFalse(lane_changes.covered("Cargo.toml", ["crates/**"]))

    def test_a_double_star_folder_matches_zero_folders_too(self):
        # The cheat sheet: '**/README.md' matches README.md, and 'docs/**/*.md' matches docs/README.md.
        self.assertTrue(lane_changes.covered("README.md", ["**/README.md"]))
        self.assertTrue(lane_changes.covered("js/README.md", ["**/README.md"]))
        self.assertTrue(lane_changes.covered("docs/README.md", ["docs/**/*.md"]))
        self.assertTrue(lane_changes.covered("docs/mona/octocat.md", ["docs/**/*.md"]))
        # The lanes' own uses: a root build.rs and the root global.json.
        self.assertTrue(lane_changes.covered("build.rs", ["**/*.rs"]))
        self.assertTrue(lane_changes.covered("crates/slate-core/src/lib.rs", ["**/*.rs"]))
        self.assertFalse(lane_changes.covered("crates/slate-core/src/lib.rsx", ["**/*.rs"]))
        self.assertTrue(lane_changes.covered("global.json", ["**/global.json"]))
        self.assertTrue(lane_changes.covered("apps/slate-windows/global.json", ["**/global.json"]))
        self.assertTrue(lane_changes.covered("apps/slate-mac/App.swift", ["apps/slate-mac/**/*.swift"]))
        self.assertTrue(lane_changes.covered("apps/slate-mac/Sources/A/B.swift", ["apps/slate-mac/**/*.swift"]))
        self.assertFalse(lane_changes.covered("apps/slate-mac/README.md", ["apps/slate-mac/**/*.swift"]))

    def test_a_double_star_inside_a_name_crosses_folders(self):
        self.assertTrue(lane_changes.covered("a/b/app.js", ["**.js"]))

    def test_dots_are_literal(self):
        self.assertFalse(lane_changes.covered("CargoXtoml", ["Cargo.toml"]))

    def test_a_later_negation_excludes_and_the_last_match_decides(self):
        self.assertTrue(lane_changes.covered("hello.md", ["*.md", "!README.md"]))
        self.assertFalse(lane_changes.covered("README.md", ["*.md", "!README.md"]))
        self.assertTrue(lane_changes.covered("README.md", ["*.md", "!README.md", "README*"]))

    def test_syntax_with_a_meaning_this_check_does_not_model_is_refused(self):
        # '?' and '+' repeat the character before them in GitHub's patterns, and '[' opens a class: rather
        # than guess, the check refuses them, and a refused pattern runs the lane.
        for pattern in ("*.jsx?", "a+.md", "[ab].md", "docs/]"):
            with self.subTest(pattern=pattern):
                with self.assertRaises(ValueError):
                    lane_changes.glob_regex(pattern)


class TriggerListFacts(unittest.TestCase):
    def test_the_push_list_is_read_in_order_without_comments(self):
        lists = lane_changes.trigger_lists(WORKFLOW)
        self.assertEqual(["crates/**", "**/*.rs", ".github/workflows/lane.yml"], lists["push"])
        self.assertNotIn("pull_request", lists)

    def test_a_hash_inside_quotes_is_part_of_the_pattern(self):
        # Cutting at the '#' would narrow the list, and the lane would skip where GitHub's filter matches.
        text = WORKFLOW.replace("      - 'crates/**'\n", "      - 'docs/#notes/**'  # a comment\n      - \"a#b.md\"\n      - plain.md # comment\n")
        self.assertEqual(["docs/#notes/**", "a#b.md", "plain.md", "**/*.rs", ".github/workflows/lane.yml"],
                         lane_changes.trigger_lists(text)["push"])

    def test_a_quote_inside_a_quoted_pattern_is_refused(self):
        for item in ("'it''s.md'", "'open.md"):
            with self.subTest(item=item):
                with self.assertRaises(ValueError):
                    lane_changes.trigger_lists(WORKFLOW.replace("'crates/**'", item))

    def test_every_lane_in_the_repository_has_a_push_list_the_check_can_read(self):
        for lane in ("a11y-check", "audit", "license-headers", "mac-runner-tests", "rust", "swift-tests",
                     "windows-runner-tests", "windows"):
            with self.subTest(lane=lane):
                text = (lane_changes.REPO / ".github" / "workflows" / f"{lane}.yml").read_text(encoding="utf-8")
                patterns = lane_changes.lane_patterns(text)
                self.assertIn(f".github/workflows/{lane}.yml", patterns)


class DecisionFacts(unittest.TestCase):
    def test_the_lane_runs_when_one_changed_file_is_in_its_paths(self):
        patterns = ["crates/**", "Cargo.toml"]
        self.assertEqual(["Cargo.toml"], lane_changes.matching(["README.md", "Cargo.toml"], patterns))
        self.assertEqual([], lane_changes.matching(["README.md", "docs/a.md"], patterns))
        self.assertEqual([], lane_changes.matching([], patterns))

    def test_a_workflow_without_a_push_list_is_refused(self):
        with self.assertRaises(ValueError):
            lane_changes.lane_patterns(WORKFLOW.replace("    paths:\n", "    tags:\n"))

    def test_a_workflow_with_an_unsupported_pattern_is_refused(self):
        with self.assertRaises(ValueError):
            lane_changes.lane_patterns(WORKFLOW.replace("'crates/**'", "'crates/*.r?'"))


class CensusScopeFacts(unittest.TestCase):
    """#1945: the census tier runs only when every lane path the pull request touches is census-only."""

    def test_the_census_list_is_the_changes_jobs_block_without_blank_or_comment_lines(self):
        self.assertEqual(["docs/plans/18_x/**", "BENCHMARKS.md"], lane_changes.census_patterns(CENSUS_WORKFLOW))

    def test_a_lane_without_a_census_list_has_none(self):
        self.assertEqual([], lane_changes.census_patterns(WORKFLOW))

    def test_a_census_list_in_another_job_or_key_is_not_the_lanes(self):
        moved = CENSUS_WORKFLOW.replace("  changes:\n    name: changes", "  other:\n    name: other")
        self.assertEqual([], lane_changes.census_patterns(moved))

    def test_a_census_list_that_is_not_a_literal_block_is_refused(self):
        # A folded block joins the lines with spaces: one pattern GitHub and this script would read differently.
        for style in (">", ">-", "'docs/plans/18_x/**'"):
            with self.subTest(style=style):
                with self.assertRaises(ValueError):
                    lane_changes.census_patterns(CENSUS_WORKFLOW.replace("census-paths: |", f"census-paths: {style}"))

    def test_a_census_pattern_must_be_one_of_the_lanes_paths(self):
        lane = lane_changes.lane_patterns(CENSUS_WORKFLOW)
        self.assertEqual(["docs/plans/18_x/**", "BENCHMARKS.md"],
                         lane_changes.checked_census(lane_changes.census_patterns(CENSUS_WORKFLOW), lane))
        with self.assertRaises(ValueError, msg="a census path outside the lane"):
            lane_changes.checked_census(["docs/plans/18_x/**", "docs/help/**"], lane)
        for extra in ("!BENCHMARKS.md", "docs/plans/18_x/a?.md"):
            with self.subTest(extra=extra):
                with self.assertRaises(ValueError):
                    lane_changes.checked_census(["docs/plans/18_x/**", extra], lane + [extra])

    def test_census_only_when_every_lane_path_touched_is_census_only(self):
        census = ["docs/plans/18_x/**", "BENCHMARKS.md"]
        self.assertEqual("census", lane_changes.scope(["BENCHMARKS.md"], census))
        self.assertEqual("census", lane_changes.scope(["docs/plans/18_x/a.md", "BENCHMARKS.md"], census))
        self.assertEqual("full", lane_changes.scope(["docs/plans/18_x/a.md", "crates/lib.rs"], census))
        self.assertEqual("full", lane_changes.scope(["crates/lib.rs"], census))

    def test_full_when_nothing_matched_or_the_lane_has_no_census_list(self):
        self.assertEqual("full", lane_changes.scope([], ["BENCHMARKS.md"]))
        self.assertEqual("full", lane_changes.scope(["BENCHMARKS.md"], []))

    def test_the_windows_lane_has_a_census_list_inside_its_path_list(self):
        text = (lane_changes.REPO / ".github" / "workflows" / "windows.yml").read_text(encoding="utf-8")
        census = lane_changes.checked_census(lane_changes.census_patterns(text), lane_changes.lane_patterns(text))
        self.assertIn("apps/slate-mac/**", census)
        for full in ("apps/slate-windows/**", "crates/**", "scripts/**", ".github/workflows/windows.yml"):
            self.assertNotIn(full, census)


def git(repo: Path, *args: str) -> str:
    env = dict(os.environ, GIT_CONFIG_NOSYSTEM="1", GIT_CONFIG_GLOBAL=os.devnull)
    command = ["git", "-c", "user.name=t", "-c", "user.email=t@example.invalid", "-c", "commit.gpgsign=false",
               "-c", "init.defaultBranch=main", *args]
    return subprocess.run(command, cwd=repo, env=env, check=True, capture_output=True, text=True).stdout


class EndToEndFacts(unittest.TestCase):
    """The script in a real repository: a base, a pull request branch and GitHub's merge commit."""

    def make_merge(self, root: Path, change, workflow: str = WORKFLOW) -> Path:
        repo = root / "repo"
        repo.mkdir()
        git(repo, "init", "-q")
        (repo / ".github/workflows").mkdir(parents=True)
        (repo / ".github/workflows/lane.yml").write_text(workflow, encoding="utf-8")
        (repo / "crates").mkdir()
        (repo / "crates/lib.rs").write_text("base\n", encoding="utf-8")
        # A second file, so renaming lib.rs away is not a rename of the whole folder to git's merge.
        (repo / "crates/keep.rs").write_text("base\n", encoding="utf-8")
        (repo / "README.md").write_text("base\n", encoding="utf-8")
        git(repo, "add", "-A")
        git(repo, "commit", "-q", "-m", "base")
        git(repo, "switch", "-q", "-c", "topic")
        change(repo)
        git(repo, "add", "-A")
        git(repo, "commit", "-q", "-m", "topic")
        git(repo, "switch", "-q", "main")
        # Main moves on in a lane path the pull request does not touch: the merge commit carries main's
        # version, so the diff from its base parent does not list it.
        (repo / "crates/main_only.rs").write_text("main\n", encoding="utf-8")
        git(repo, "add", "-A")
        git(repo, "commit", "-q", "-m", "main moves")
        git(repo, "merge", "-q", "--no-ff", "-m", "merge", "topic")
        return repo

    def run_script(self, repo: Path, root: Path, census: str | None = None) -> tuple[int, str, str]:
        output = root / "github_output"
        output.write_text("", encoding="utf-8")
        env = dict(os.environ, GITHUB_OUTPUT=str(output), GIT_CONFIG_NOSYSTEM="1", GIT_CONFIG_GLOBAL=os.devnull)
        env.pop("CENSUS_PATHS", None)
        if census is not None:
            # What lane-changes.yml hands the script: the input as GitHub read it.
            env["CENSUS_PATHS"] = census
        result = subprocess.run([sys.executable, "-I", str(SCRIPT), ".github/workflows/lane.yml"], cwd=repo,
                                env=env, capture_output=True, text=True)
        return result.returncode, output.read_text(encoding="utf-8"), result.stdout + result.stderr

    def test_a_pull_request_that_changes_a_lane_path_runs_the_lane(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            repo = self.make_merge(root, lambda r: (r / "crates/lib.rs").write_text("topic\n", encoding="utf-8"))
            code, output, log = self.run_script(repo, root)
        self.assertEqual(0, code, log)
        self.assertEqual("run=true\nscope=full\n", output)
        self.assertIn("crates/lib.rs", log)

    def test_a_pull_request_that_changes_no_lane_path_skips_the_lane(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            repo = self.make_merge(root, lambda r: (r / "README.md").write_text("topic\n", encoding="utf-8"))
            code, output, log = self.run_script(repo, root)
        self.assertEqual(0, code, log)
        self.assertEqual("run=false\nscope=full\n", output, "main's own change to crates/ is not the pull request's")

    def test_a_file_renamed_out_of_the_lane_paths_runs_the_lane(self):
        # A rename is a delete and an add: the lane's file is gone, which the lane must see.
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            repo = self.make_merge(root, lambda r: (r / "crates/lib.rs").rename(r / "lib.txt"))
            code, output, log = self.run_script(repo, root)
        self.assertEqual(0, code, log)
        self.assertEqual("run=true\nscope=full\n", output)

    def test_a_head_that_is_not_a_merge_commit_fails_with_no_output(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            repo = self.make_merge(root, lambda r: (r / "README.md").write_text("topic\n", encoding="utf-8"))
            git(repo, "switch", "-q", "topic")
            code, output, log = self.run_script(repo, root)
        self.assertNotEqual(0, code, log)
        self.assertEqual("", output, "no output: the lane's jobs read a missing decision as run")

    def test_an_unreadable_path_list_fails_with_no_output(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            repo = self.make_merge(root, lambda r: (r / ".github/workflows/lane.yml").write_text(
                WORKFLOW.replace("    paths:\n", "    tags:\n"), encoding="utf-8"))
            code, output, log = self.run_script(repo, root)
        self.assertNotEqual(0, code, log)
        self.assertEqual("", output)


    CENSUS_INPUT = "# The plan folder the censuses read.\ndocs/plans/18_x/**\n\nBENCHMARKS.md\n"

    def census_merge(self, root: Path, *paths: str) -> Path:
        def change(repo: Path) -> None:
            for path in paths:
                (repo / path).parent.mkdir(parents=True, exist_ok=True)
                (repo / path).write_text("topic\n", encoding="utf-8")
        return self.make_merge(root, change, CENSUS_WORKFLOW)

    def test_a_pull_request_that_touches_only_census_paths_runs_the_census_tier(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            repo = self.census_merge(root, "docs/plans/18_x/a.md", "BENCHMARKS.md", "README.md")
            code, output, log = self.run_script(repo, root, self.CENSUS_INPUT)
        self.assertEqual(0, code, log)
        self.assertEqual("run=true\nscope=census\n", output)
        self.assertIn("census tier", log)

    def test_a_pull_request_that_also_touches_a_full_path_runs_the_whole_lane(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            repo = self.census_merge(root, "docs/plans/18_x/a.md", "crates/lib.rs")
            code, output, log = self.run_script(repo, root, self.CENSUS_INPUT)
        self.assertEqual(0, code, log)
        self.assertEqual("run=true\nscope=full\n", output)
        self.assertIn("crates/lib.rs", log)

    def test_a_census_input_that_differs_from_the_workflows_block_fails_with_no_output(self):
        # The guard reads the block in the file and the decision reads what GitHub passed: they must agree.
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            repo = self.census_merge(root, "BENCHMARKS.md")
            code, output, log = self.run_script(repo, root, "BENCHMARKS.md\ncrates/**\n")
        self.assertNotEqual(0, code, log)
        self.assertEqual("", output, "no output: the lane's jobs read a missing scope as full")

    def test_the_workflows_block_decides_when_no_input_is_handed_over(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            repo = self.census_merge(root, "BENCHMARKS.md")
            code, output, log = self.run_script(repo, root)
        self.assertEqual(0, code, log)
        self.assertEqual("run=true\nscope=census\n", output)


if __name__ == "__main__":
    unittest.main()
