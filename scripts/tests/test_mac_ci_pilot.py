"""Fault cases for the pilot's independent aggregate and owned cancellation."""

import copy
import importlib.util
import json
import os
from pathlib import Path
import select
import signal
import subprocess
import sys
import tempfile
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
        with self.assertRaises(ValueError):
            pilot.validate_inputs(pilot.REFERENCE, "new-paid-profile", "pair-1")

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


if __name__ == "__main__":
    unittest.main()
