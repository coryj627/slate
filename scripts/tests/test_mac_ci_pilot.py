"""Fault cases for the pilot's independent aggregate and owned cancellation."""

import copy
import importlib.util
import json
import os
from pathlib import Path
import re
import select
import signal
import subprocess
import sys
import tempfile
import time
import unittest
from unittest.mock import patch


SPEC = importlib.util.spec_from_file_location("mac_ci_pilot", Path(__file__).resolve().parents[1] / "mac-ci-pilot.py")
pilot = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(pilot)

IDENTITY = {"source_sha": pilot.REFERENCE, "harness_sha": "a" * 40,
            "candidate": "hosted-xcode27", "pair_id": "pair-1", "run_id": "123", "run_attempt": "1"}


def fixture(layer):
    source = {"sha": pilot.REFERENCE, "tree": "b" * 40, "tracked_status": "", "tests_tree": "c" * 40,
              "locks": {"Cargo.lock": "d" * 64}}
    metadata = {"schema": 1, "layer": layer, **IDENTITY, "qualified": True,
                "source_is_harness_ancestor": True, "source": source, "reference_tests_tree": "c" * 40,
                "xcode": "Xcode 27.0\nBuild version 27A266a", "rust": "rustc 1.97.1 (pinned)", "cargo": "cargo 1.97.1 (pinned)"}
    summary = {"schema": 1, "layer": layer, **IDENTITY, "status": "success", "metadata": metadata,
               "test_workers": 3, "passes": {}, "phases": {"toolchain": {"status": "success", "exit_code": 0}}}
    if layer == "analyzer":
        for name in ["analyzer.clone", "analyzer.checkout", "analyzer.build"]:
            summary["phases"][name] = {"status": "success", "exit_code": 0}
    for state in ["cold", "warm"]:
        row = {"status": "success", "state": pilot.STATES[state], "source_after": copy.deepcopy(source)}
        if layer == "native":
            row.update(xctest={"count": 3032, "digest": pilot.INVENTORY_SHA, "failures": 0, "errors": 0, "skips": 0},
                       cli={"passed": True}, release={"passed": True, "ffi_contract": 30,
                           "relative_link": "@executable_path/../Frameworks/libslate_uniffi.dylib",
                           "files": [{"path": path, "bytes": 100, "sha256": "e" * 64} for path in
                                     ["Contents/MacOS/SlateMac", "Contents/Frameworks/libslate_uniffi.dylib", "Contents/Info.plist"]],
                           "checks": {key: "" for key in ["signature", "plist", "links", "library_id", "load_commands", "architecture"]}})
            suffixes = pilot.NATIVE_PHASES
        else:
            row.update(analyzer_sha=pilot.ANALYZER, binary_sha256="f" * 64, build_reused=state == "warm",
                       floor={"score": 100, "errors": 0, "minimum": 100, "passed": True})
            suffixes = pilot.ANALYZER_PHASES
        summary["passes"][state] = row
        for suffix in suffixes:
            summary["phases"][state + "." + suffix] = {"status": "success", "exit_code": 0}
    return summary


class MeasurementWrapperTests(unittest.TestCase):
    def test_dyld_free_phases_keep_the_bare_time_wrapper(self):
        self.assertEqual(pilot.measurement_wrapper({"PATH": "/usr/bin", "PROFILE": "debug"}),
                         ["/usr/bin/time", "-l"])

    def test_dyld_variables_are_reexported_after_protected_time(self):
        env = {"PATH": "/usr/bin", "DYLD_LIBRARY_PATH": "/source/target/debug", "DYLD_PRINT_LIBRARIES": "1"}
        self.assertEqual(pilot.measurement_wrapper(env),
                         ["/usr/bin/time", "-l", "/usr/bin/env", "DYLD_LIBRARY_PATH=/source/target/debug",
                          "DYLD_PRINT_LIBRARIES=1"])

    @unittest.skipUnless(sys.platform == "darwin", "SIP environment purging is macOS behavior")
    def test_measured_command_receives_the_recorded_dyld_path(self):
        python = os.path.realpath(sys.executable)
        if python.startswith(("/usr/", "/System/", "/bin/", "/sbin/")):
            self.skipTest("this interpreter is SIP-protected and would drop DYLD_* itself")
        env = dict(os.environ, DYLD_LIBRARY_PATH="/mac-pilot/dyld-probe")
        probe = [python, "-c", "import os; print(os.environ.get('DYLD_LIBRARY_PATH'))"]
        measured = subprocess.run([*pilot.measurement_wrapper(env), *probe], env=env,
                                  capture_output=True, text=True, check=True)
        self.assertEqual(measured.stdout.strip(), "/mac-pilot/dyld-probe")


class MacPilotGateTests(unittest.TestCase):
    def test_complete_two_layers_are_accepted(self):
        for layer in ["native", "analyzer"]:
            pilot.validate_summary(fixture(layer), IDENTITY, layer)

    def test_missing_or_failed_phase_cannot_be_hidden_by_success_summary(self):
        for layer, names in [("native", pilot.NATIVE_PHASES), ("analyzer", pilot.ANALYZER_PHASES)]:
            for state in ["cold", "warm"]:
                for suffix in names:
                    for status in [None, "failed", "cancelled", "skipped", "running"]:
                        with self.subTest(layer=layer, state=state, phase=suffix, status=status):
                            summary = fixture(layer)
                            name = state + "." + suffix
                            if status is None:
                                del summary["phases"][name]
                            else:
                                summary["phases"][name]["status"] = status
                            with self.assertRaises(ValueError):
                                pilot.validate_summary(summary, IDENTITY, layer)

    def test_partial_inventory_error_skip_and_dirty_source_are_rejected(self):
        for key, value in [("count", 3031), ("count", True), ("digest", "0" * 64),
                           ("failures", 1), ("errors", 1), ("skips", 1), ("errors", False)]:
            summary = fixture("native")
            summary["passes"]["warm"]["xctest"][key] = value
            with self.subTest(key=key, value=value), self.assertRaises(ValueError):
                pilot.validate_summary(summary, IDENTITY, "native")
        summary = fixture("native")
        summary["passes"]["warm"]["source_after"]["tracked_status"] = " M generated.swift"
        with self.assertRaises(ValueError):
            pilot.validate_summary(summary, IDENTITY, "native")

    def test_wrong_identity_toolchain_ancestry_and_layer_are_rejected(self):
        for key in IDENTITY:
            summary = fixture("native")
            summary[key] = "different"
            with self.subTest(key=key), self.assertRaises(ValueError):
                pilot.validate_summary(summary, IDENTITY, "native")
        for key, value in [("qualified", False), ("layer", "analyzer"), ("source_is_harness_ancestor", False),
                           ("xcode", "Xcode 27.1\nBuild version different"), ("rust", "rustc 1.98.1"),
                           ("cargo", "cargo 1.98.1"), ("reference_tests_tree", "0" * 40)]:
            summary = fixture("native")
            summary["metadata"][key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                pilot.validate_summary(summary, IDENTITY, "native")

    def test_real_release_and_analyzer_provenance_are_required(self):
        for key, value in [("passed", False), ("ffi_contract", 29), ("relative_link", "/tmp/stale.dylib"), ("files", [])]:
            summary = fixture("native")
            summary["passes"]["cold"]["release"][key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                pilot.validate_summary(summary, IDENTITY, "native")
        summary = fixture("native")
        del summary["passes"]["warm"]["release"]["checks"]["signature"]
        with self.assertRaises(ValueError):
            pilot.validate_summary(summary, IDENTITY, "native")
        for key, value in [("analyzer_sha", "0" * 40), ("binary_sha256", "0" * 64), ("build_reused", False)]:
            summary = fixture("analyzer")
            summary["passes"]["warm"][key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                pilot.validate_summary(summary, IDENTITY, "analyzer")
        summary = fixture("analyzer")
        del summary["phases"]["analyzer.build"]
        with self.assertRaises(ValueError):
            pilot.validate_summary(summary, IDENTITY, "analyzer")

    def test_analyzer_malformed_or_weaker_gate_is_rejected(self):
        for score in [None, "100", True, float("nan"), float("inf"), -1, 101, 99.9]:
            with self.subTest(score=score), self.assertRaises(ValueError):
                pilot.analyzer_floor({"score": {"score": score, "totalErrors": 0}})
        for errors in [None, "0", False, -1, 0.1, 1]:
            with self.subTest(errors=errors), self.assertRaises(ValueError):
                pilot.analyzer_floor({"score": {"score": 100, "totalErrors": errors}})
        for minimum in ["", " ", "90", "100%", "NaN", "101"]:
            with self.subTest(minimum=minimum), self.assertRaises(ValueError):
                pilot.analyzer_floor({"score": {"score": 100, "totalErrors": 0}}, minimum)

    def test_failed_xml_is_preserved_and_declared_errors_are_not_ignored(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "results.xml"
            for xml in [
                '<testsuites><testsuite errors="1"><testcase classname="A" name="b" /></testsuite></testsuites>',
                '<testsuites errors="1"><testsuite><testcase classname="A" name="b" /></testsuite></testsuites>',
            ]:
                with self.subTest(xml=xml):
                    path.write_text(xml)
                    self.assertEqual(pilot.parse_xctest(path, enforce=False)["errors"], 1)
                    with self.assertRaises(ValueError):
                        pilot.parse_xctest(path)
            path.write_text('<testsuites errors="unknown"><testsuite /></testsuites>')
            with self.assertRaisesRegex(ValueError, "invalid suite counter"):
                pilot.parse_xctest(path, enforce=False)

    def test_only_literal_commits_and_known_candidates_are_allowed(self):
        for source in ["main", "e6a8337b", "a" * 39, "$(date)", "A" * 40]:
            with self.subTest(source=source), self.assertRaises(ValueError):
                pilot.validate_inputs(source, "hosted-xcode27", "pair-1")
        # Golden Gate answers no Finder Apple Events; it is withdrawn until it does.
        for candidate in ["new-paid-profile", "namespace-goldengate6x14"]:
            with self.subTest(candidate=candidate), self.assertRaises(ValueError):
                pilot.validate_inputs(pilot.REFERENCE, candidate, "pair-1")
        for candidate in ["hosted-xcode27", "namespace-tahoeslim6x14"]:
            with self.subTest(candidate=candidate):
                pilot.validate_inputs(pilot.REFERENCE, candidate, "pair-1")

    def test_workflow_offers_and_routes_exactly_the_known_candidates(self):
        workflow = (Path(__file__).resolve().parents[2] / ".github/workflows/mac-ci-pilot.yml").read_text()
        options = re.search(r"options: \[([^\]]*)\]", workflow).group(1)
        self.assertEqual(sorted(item.strip() for item in options.split(",")), sorted(pilot.CANDIDATES))
        routes = re.findall(r"^    runs-on: (\$\{\{ inputs\.runner .*)$", workflow, re.M)
        self.assertEqual(len(routes), 2)
        for candidate, label in pilot.LABELS.items():
            expected = f"|| '{label}'" if candidate == "hosted-xcode27" else f"inputs.runner == '{candidate}' && '{label}'"
            for route in routes:
                with self.subTest(candidate=candidate):
                    self.assertIn(expected, route)

    def test_aggregate_requires_both_jobs_and_exact_attempt_artifacts(self):
        with tempfile.TemporaryDirectory() as folder, patch.object(pilot, "context", return_value=IDENTITY):
            root = Path(folder)
            for layer in ["native", "analyzer"]:
                pilot.write_json(root / layer / "summary.json", fixture(layer))
            pilot.aggregate(root, "success", "success")
            for result in ["failure", "cancelled", "skipped", ""]:
                with self.subTest(result=result), self.assertRaises(ValueError):
                    pilot.aggregate(root, result, "success")
            summary = fixture("analyzer")
            summary["run_attempt"] = "0"
            pilot.write_json(root / "analyzer/summary.json", summary)
            with self.assertRaises(ValueError):
                pilot.aggregate(root, "success", "success")
            pilot.write_json(root / "analyzer/summary.json", fixture("analyzer"))
            pilot.write_json(root / "duplicate/summary.json", fixture("native"))
            with self.assertRaises(ValueError):
                pilot.aggregate(root, "success", "success")

    @unittest.skipUnless(os.name == "posix", "process-group signal contract requires Unix")
    def test_term_ignoring_child_is_killed_after_wrapper_exits(self):
        child = "import signal,time; signal.signal(signal.SIGTERM,signal.SIG_IGN); print('ready',flush=True); time.sleep(60)"
        leader_code = f"import subprocess,sys,time; child=subprocess.Popen([sys.executable,'-c',{child!r}],stdout=subprocess.PIPE,text=True); print(child.stdout.readline(),flush=True); time.sleep(60)"
        leader = subprocess.Popen([sys.executable, "-c", leader_code], stdout=subprocess.PIPE, text=True, start_new_session=True)
        try:
            self.assertTrue(select.select([leader.stdout], [], [], 3)[0], "child did not become ready")
            self.assertEqual(leader.stdout.readline().strip(), "ready")
            result = pilot.terminate_group(leader, grace=0.1)
            self.assertTrue(result.get("sigkill_sent"), result)
            self.assertTrue(result["pids_after_term_grace"], result)
            self.assertTrue(result["group_has_no_live_members"], result)
            self.assertEqual(result["live_group_pids_after"], [], result)
        finally:
            try:
                os.killpg(leader.pid, signal.SIGKILL)
            except (ProcessLookupError, PermissionError):
                try:
                    leader.kill()
                except ProcessLookupError:
                    pass
            leader.wait(timeout=3)
            leader.stdout.close()

    @unittest.skipUnless(os.name == "posix", "workflow signal contract requires Unix")
    def test_workflow_entry_signal_reaches_main_and_records_phase_cleanup(self):
        workflow = (Path(__file__).resolve().parents[2] / ".github/workflows/mac-ci-pilot.yml").read_text()
        steps = [("native", "Complete native cold and same-VM warm passes"),
                 ("analyzer", "Build exact analyzer and enforce every cold/warm scan")]
        for layer, title in steps:
            lines = workflow.splitlines()
            start = lines.index("      - name: " + title)
            self.assertEqual(lines[start + 1], "        run: |")
            body = []
            for line in lines[start + 2:]:
                if line and not line.startswith("          "):
                    break
                body.append(line[10:])
            actual_step = "\n".join(body) + "\n"
            # Negative control: the old shell entry receives SIGINT while
            # Python remains parked. Then exercise the actual fixed step.
            for direct_entry in [False, True]:
                with self.subTest(layer=layer, direct_entry=direct_entry), tempfile.TemporaryDirectory() as folder:
                    root = Path(folder)
                    source = root / "source"
                    source.mkdir()
                    evidence = root / "temp" / ("mac-pilot-" + layer)
                    metadata = {"schema": 1, "layer": layer, **IDENTITY, "qualified": True,
                                "developer_dir": "/signal-test/Xcode/Contents/Developer",
                                "swift_path": "/usr/bin/swift", "swift": "signal-test Swift",
                                "sdk_path": "/signal-test/MacOSX.sdk", "clang_path": "/usr/bin/cc",
                                "clangxx_path": "/usr/bin/c++"}
                    pilot.write_json(evidence / "metadata.json", metadata)
                    ready = evidence / "controlled-child.json"
                    bootstrap = root / "pilot-harness/scripts/mac-ci-pilot.py"
                    bootstrap.parent.mkdir(parents=True)
                    bootstrap.write_text(f'''import importlib.util,json,os,subprocess,sys
from pathlib import Path
spec=importlib.util.spec_from_file_location("pilot",{str(Path(SPEC.origin).resolve())!r})
pilot=importlib.util.module_from_spec(spec)
spec.loader.exec_module(pilot)
real_command=pilot.command
pilot.command=lambda args,*a,**k: "signal-test Swift" if args==["swift","--version"] else real_command(args,*a,**k)
# Linux registration has GNU time; adapt only the resource wrapper, leaving
# real main, signals, Runner.run and process-group ownership exercised.
real_popen=subprocess.Popen
def launch(args,*a,**k):
    if sys.platform!="darwin" and args[:2]==["/usr/bin/time","-l"]:
        args=args[2:]
    return real_popen(args,*a,**k)
pilot.subprocess.Popen=launch
def controlled(runner):
    child="import json,os,signal,time; from pathlib import Path; signal.signal(signal.SIGTERM,signal.SIG_IGN); Path(os.environ['MAC_PILOT_SIGNAL_READY']).write_text(json.dumps({{'pid':os.getpid(),'group':os.getpgrp()}})); time.sleep(60)"
    runner.run("cold.signal-regression",[sys.executable,"-c",child])
pilot.native=controlled
pilot.analyzer=controlled
raise SystemExit(pilot.main())
''')
                    script = root / "step.sh"
                    script.write_text(actual_step if direct_entry else actual_step.replace("exec python ", "python ", 1))
                    python_bin = root / "bin"
                    python_bin.mkdir()
                    (python_bin / "python").symlink_to(sys.executable)
                    env = dict(os.environ, SOURCE_SHA=IDENTITY["source_sha"], GITHUB_SHA=IDENTITY["harness_sha"],
                               CANDIDATE=IDENTITY["candidate"], PAIR_ID=IDENTITY["pair_id"],
                               GITHUB_RUN_ID=IDENTITY["run_id"], GITHUB_RUN_ATTEMPT=IDENTITY["run_attempt"],
                               GITHUB_WORKSPACE=str(root), RUNNER_TEMP=str(root / "temp"),
                               MAC_PILOT_SIGNAL_READY=str(ready))
                    env["PATH"] = str(python_bin) + os.pathsep + env.get("PATH", "")
                    entry = subprocess.Popen(["bash", "--noprofile", "--norc", "-e", "-o", "pipefail", str(script)],
                                             cwd=root, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                             text=True, start_new_session=True)
                    child_pid = None
                    try:
                        deadline = time.monotonic() + 10
                        while not ready.exists() and entry.poll() is None and time.monotonic() < deadline:
                            time.sleep(0.02)
                        self.assertTrue(ready.exists(), "controlled phase child did not become ready")
                        child_pid = json.loads(ready.read_text())["pid"]
                        os.kill(entry.pid, signal.SIGINT)  # GitHub signals the step entry, not its whole group.
                        if not direct_entry:
                            time.sleep(0.3)
                            summary = json.loads((evidence / "summary.json").read_text())
                            self.assertEqual(summary["status"], "incomplete", summary)
                            self.assertEqual(summary["phases"]["cold.signal-regression"]["status"], "running", summary)
                            continue
                        output, _ = entry.communicate(timeout=7)
                        self.assertEqual(entry.returncode, 1, output)
                        summary = json.loads((evidence / "summary.json").read_text())
                        self.assertEqual(summary["status"], "cancelled", summary)
                        phase = summary["phases"]["cold.signal-regression"]
                        self.assertEqual(phase["status"], "cancelled", phase)
                        cleanup = phase["cancellation_cleanup"]
                        self.assertTrue(cleanup["sigkill_sent"], cleanup)
                        self.assertIn(child_pid, cleanup["pids_after_term_grace"], cleanup)
                        self.assertTrue(cleanup["group_has_no_live_members"], cleanup)
                        self.assertEqual(cleanup["live_group_pids_after"], [], cleanup)
                    finally:
                        # The negative control deliberately bypasses Python's
                        # cleanup. Never leave its isolated child behind.
                        if child_pid is not None:
                            try:
                                os.kill(child_pid, signal.SIGKILL)
                            except ProcessLookupError:
                                pass
                        try:
                            os.killpg(entry.pid, signal.SIGKILL)
                        except ProcessLookupError:
                            pass
                        entry.wait(timeout=3)
                        entry.stdout.close()


if __name__ == "__main__":
    unittest.main()
