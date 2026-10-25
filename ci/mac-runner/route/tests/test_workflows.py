"""Guards on the workflows that route through route.py (.github/workflows/
swift-tests.yml and a11y-check.yml).

The routing job runs the checked-out head's own route.py, so a pull request
controls its outputs. A run: script that expanded ${{ needs.route.outputs.* }}
would execute that text as shell; the outputs reach scripts only through a
step's env:. runs-on, if: and with: keep reading the expressions, which
GitHub evaluates without a shell.

Run:  python3 -m unittest discover -s ci/mac-runner/route/tests -t ci/mac-runner/route
"""

import os
import re
import unittest

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
WORKFLOWS = ("swift-tests.yml", "a11y-check.yml")

# Each run: with its block: the following lines belong to it while they are
# indented deeper than the key, blank lines included.
RUN_BLOCK = re.compile(r"^(?P<indent>[ ]+)run:[^\n]*(?:\n(?:(?P=indent)[ ]+\S[^\n]*|[ \t]*(?=\n)))*", re.M)
# needs.route (any case, dotted or indexed) anywhere inside one ${{ }}
# expression, format() arguments included.
ROUTE_IN_EXPRESSION = re.compile(r"\$\{\{(?:(?!\}\}).)*needs\s*(?:\.\s*route\b|\[\s*'route'\s*\])", re.I)


def read(name):
    with open(os.path.join(REPO, ".github", "workflows", name), encoding="utf-8") as handle:
        return handle.read()


def run_blocks(text):
    return [match.group(0) for match in RUN_BLOCK.finditer(text)]


class RouterOutputsReachScriptsOnlyThroughEnv(unittest.TestCase):
    def test_no_run_block_expands_the_routing_jobs_outputs(self):
        for name in WORKFLOWS:
            for block in run_blocks(read(name)):
                self.assertIsNone(
                    ROUTE_IN_EXPRESSION.search(block),
                    f"{name}: a router output in a script runs as shell; pass it through env:\n{block}",
                )

    def test_the_blocks_include_the_multi_line_scripts(self):
        # Without this the pin above could pass against nothing.
        swift = run_blocks(read("swift-tests.yml"))
        self.assertEqual(
            sum('echo "route: $ROUTE ($(id -un) on $(hostname))"' in block for block in swift),
            1,
            "swift-tests.yml: the Rust on PATH script is among the blocks",
        )
        a11y = run_blocks(read("a11y-check.yml"))
        self.assertEqual(
            sum("ref_file=/usr/local/slate-runner/bin/a11y-check.ref" in block for block in a11y),
            1,
            "a11y-check.yml: the Studio pin check's script is among the blocks",
        )

    def test_both_mac_lanes_hand_the_router_the_bot_rules_inputs(self):
        # Plan 42 §3.7: without these the router cannot keep a bot's run, or a run of its branch, off the Studio.
        step = (
            "          MAC_RUNNER_MODE: ${{ vars.MAC_RUNNER_MODE }}",
            "          MAC_RUNNER_HEARTBEAT: ${{ vars.MAC_RUNNER_HEARTBEAT }}",
        )
        entries = (
            "          ROUTE_ACTOR: ${{ github.actor }}",
            "          ROUTE_TRIGGERING_ACTOR: ${{ github.triggering_actor }}",
            "          ROUTE_PR_AUTHOR: ${{ github.event.pull_request.user.login }}",
            "          ROUTE_HEAD_REF: ${{ github.head_ref || github.ref_name }}",
            "          ROUTE_ALLOWLIST: ci/mac-runner/allowlist.json",
            "        run: python3 ci/mac-runner/route/route.py",
        )
        for name in ("swift-tests.yml", "a11y-check.yml"):
            text = read(name)
            start = text.index("\n".join(step))
            block = text[start:text.index("run: python3 ci/mac-runner/route/route.py", start) + len(
                "run: python3 ci/mac-runner/route/route.py")]
            for entry in entries:
                self.assertIn(entry, block, name)
            # The cone-mode checkout of the route directory brings ci/mac-runner's own files, the allow-list among them.
            self.assertIn("          sparse-checkout: ci/mac-runner/route\n", text, name)

    def test_swift_tests_prints_the_route_from_its_environment(self):
        step = (
            "        env:",
            "          ROUTE: ${{ needs.route.outputs.route }}",
            "        run: |",
            '          if [ -d "$HOME/.cargo/bin" ]; then echo "$HOME/.cargo/bin" >> "$GITHUB_PATH"; fi',
            '          echo "route: $ROUTE ($(id -un) on $(hostname))"',
        )
        self.assertTrue(
            "\n".join(step) + "\n" in read("swift-tests.yml"),
            "swift-tests.yml: the Rust on PATH step reads the route through env: and prints $ROUTE",
        )


if __name__ == "__main__":
    unittest.main()
