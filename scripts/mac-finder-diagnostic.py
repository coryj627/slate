#!/usr/bin/env python3
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
"""One unmodified native case, then a distinct ordinary probe.

No PATH shim, Finder restart, TCC change, retry, cache mutation, or real vault.
This is diagnostic evidence, never the complete 3,032-case acceptance gate.
"""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import signal
import subprocess
import sys
import tempfile
import time
import uuid
import xml.etree.ElementTree as ET

SOURCE = "704ab907e0df753dd24c0c6af688dc3a8975e4e7"
CASE = "SlateMacTests.FileManagementCommandsTests/testDeleteFileRemovesItFromDisk"
TOTAL_SECONDS = 540
OSA_SECONDS = 130  # Observe ordinary ~120-second boundary; no AppleScript timeout override.
MAX_OUTPUT_BYTES = 8 * 1024 * 1024


class Interrupted(Exception):
    pass


class BudgetExpired(Exception):
    pass


def interrupted(signum, frame):
    raise Interrupted(f"wrapper received signal {signum}")


def budget_expired(signum, frame):
    raise BudgetExpired("overall nine-minute budget: stopping with 20-second cleanup/evidence reserve")


def utc():
    from datetime import datetime, timezone
    return datetime.now(timezone.utc).isoformat()


def save(path, value):
    temp = path.with_suffix(path.suffix + ".tmp")
    temp.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n")
    temp.replace(path)


class Diagnostic:
    def __init__(self, source, evidence):
        self.source, self.evidence = source, evidence
        self.harness = Path(__file__).resolve().parent.parent
        self.evidence.mkdir(parents=True, exist_ok=False)
        self.started = time.monotonic()
        self.deadline = self.started + TOTAL_SECONDS
        self.env = dict(os.environ)
        self.sample_count = 0
        self.native_requests = {}
        self.fixture_paths = set()
        self.summary = {"schema": 1, "status": "running", "scope": "diagnostic, not full inventory acceptance",
                        "source_sha": SOURCE, "harness_sha": os.environ.get("GITHUB_SHA"),
                        "run_id": os.environ.get("GITHUB_RUN_ID"), "run_attempt": os.environ.get("GITHUB_RUN_ATTEMPT"),
                        "instance_id": os.environ.get("NSC_INSTANCE_ID"), "runner": "nscloud-macos-goldengate-arm64-6x14",
                        "started_utc": utc(), "total_wrapper_budget_seconds": TOTAL_SECONDS,
                        "phases": {}, "native_case": CASE, "ordinary_probe": {"status": "not started"},
                        "billing": {"shape_units_per_minute": 60, "instance_billed_units": None,
                                    "scope": "provider job-running interval and rounded billed units must be retrieved separately"}}
        spec = importlib.util.spec_from_file_location("mac_pilot", self.harness / "scripts/mac-ci-pilot.py")
        self.pilot = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.pilot)
        self.persist()

    def persist(self):
        save(self.evidence / "diagnostic-summary.json", self.summary)

    def small(self, name, argv, limit=3):
        """Read-only supporting command; retain failure/permission denial verbatim."""
        return self.run(name, argv, min(limit, self.remaining()), observe=False, required=False)

    def remaining(self):
        return max(0, self.deadline - time.monotonic() - 20)  # reserve cleanup and final atomic evidence

    def processes(self):
        # Darwin 'sess' is retained raw; do not claim it is a portable POSIX session ID.
        p = subprocess.run(["/bin/ps", "-axo", "pid,ppid,pgid,sess,uid,stat,pcpu,rss,command"],
                           text=True, capture_output=True, timeout=1)
        if p.returncode != 0:
            return {}, {"exit_code": p.returncode, "stderr": p.stderr}
        rows = {}
        for line in p.stdout.splitlines()[1:]:
            bits = line.strip().split(None, 8)
            if len(bits) == 9 and bits[0].isdigit():
                rows[int(bits[0])] = dict(zip(["pid", "ppid", "pgid", "darwin_sess", "uid", "stat", "pcpu", "rss_kib", "command"], bits))
        return rows, {"exit_code": 0}

    def sample(self, phase, proc, stream):
        rows, status = self.processes()
        descendants = {proc.pid}
        while True:
            expanded = descendants | {pid for pid, row in rows.items() if int(row["ppid"]) in descendants}
            if expanded == descendants:
                break
            descendants = expanded
        support = {pid for pid, row in rows.items() if any(token in row["command"] for token in
                   ["/Finder.app/Contents/MacOS/Finder", "/tccd", "/appleeventsd", "/loginwindow"])}
        selected = {pid: rows[pid] for pid in descendants | support if pid in rows}
        request_age = 0
        for pid in descendants:
            row = rows.get(pid, {})
            if "osascript" not in row.get("command", ""):
                continue
            key = f"{phase}:{pid}"
            request = self.native_requests.setdefault(key, {"first_seen_monotonic": time.monotonic(), "pid": pid,
                      "phase": phase, "command": row["command"], "first_seen_utc": utc(), "stack_thresholds": []})
            request["last_seen_utc"] = utc()
            age = time.monotonic() - request["first_seen_monotonic"]
            request["observed_age_seconds"] = age
            request_age = max(request_age, age)
            if phase == "native-case":
                for quoted in re.findall(r'POSIX file "((?:[^"\\]|\\.)*)"', row["command"]):
                    path = Path(quoted.replace('\\"', '"').replace('\\\\', '\\'))
                    # Only metadata for the exact case's disposable a.md/b.md paths.
                    if re.fullmatch(r"file-mgmt-[0-9A-Fa-f-]{36}", path.parent.parent.name) and path.parent.name == "vault" and path.name == "a.md" and "T" in path.parts:
                        self.fixture_paths.update([path, path.with_name("b.md")])
            for threshold in (5, 105):
                if age >= threshold and threshold not in request["stack_thresholds"]:
                    request["stack_thresholds"].append(threshold)
                    targets = [pid] + [int(row["ppid"])] + [p for p in support if "/Finder.app/" in rows[p]["command"]]
                    for target in dict.fromkeys(targets):
                        if self.sample_count >= 12 or self.remaining() < 8:
                            break
                        self.sample_count += 1
                        # Bounded supporting sample, never a consent/UI interaction.
                        self.small(f"stack-{self.sample_count}-{target}", ["/usr/bin/sample", str(target), "1", "1"], 3)
        stream.write(json.dumps({"utc": utc(), "phase": phase, "sampling": status,
                                 "processes": list(selected.values()), "file_effects": self.effects(self.fixture_paths)}) + "\n")
        stream.flush()
        return request_age

    def effects(self, paths):
        result = []
        for path in sorted(paths):
            row = {"path": str(path)}
            try:
                st = path.lstat()
                row.update(exists=True, mode=oct(st.st_mode), uid=st.st_uid, gid=st.st_gid, size=st.st_size, inode=st.st_ino)
            except FileNotFoundError:
                row["exists"] = False
            except OSError as exc:
                row["error"] = str(exc)
            result.append(row)
        return result

    def run(self, name, argv, limit, observe=True, required=True, cwd=None, env=None):
        limit = min(limit, self.remaining())
        if limit <= 0:
            raise TimeoutError("overall diagnostic budget exhausted")
        record = {"argv": argv, "started_utc": utc(), "timeout_seconds": limit, "status": "running"}
        self.summary["phases"][name] = record
        self.persist()
        proc = None
        started = time.monotonic()
        try:
            with (self.evidence / f"{name}.stdout.log").open("w") as out, (self.evidence / f"{name}.stderr.log").open("w") as err, (self.evidence / f"{name}.processes.jsonl").open("w") as samples:
                proc = subprocess.Popen(argv, cwd=cwd or self.source, env=env or self.env,
                                        stdout=out, stderr=err, start_new_session=True)
                record["launched_group"] = proc.pid
                while proc.poll() is None:
                    age = self.sample(name, proc, samples) if observe else 0
                    if time.monotonic() - started >= limit or age >= OSA_SECONDS:
                        raise TimeoutError("phase deadline" if age < OSA_SECONDS else "observed osascript request exceeded 130-second diagnostic watchdog")
                    if out.tell() + err.tell() > MAX_OUTPUT_BYTES:
                        raise RuntimeError("diagnostic output limit reached")
                    time.sleep(0.5)
                record.update(exit_code=proc.returncode, status="success" if proc.returncode == 0 else "failed")
        except BaseException as exc:
            record.update(status="cancelled" if isinstance(exc, Interrupted) else "failed", error=str(exc))
            if proc:
                record["process_cleanup"] = self.pilot.terminate_group(proc)
                record["exit_code"] = proc.returncode
            if isinstance(exc, (Interrupted, BudgetExpired, KeyboardInterrupt)):
                raise
        finally:
            record.update(ended_utc=utc(), elapsed_seconds=time.monotonic() - started)
            if proc:
                # Wrapper exit alone is not evidence of native child/group exit.
                try:
                    live, zombies = self.pilot.group_members(proc.pid)
                    record["group_after"] = {"live": live, "zombies": zombies}
                    if live:
                        record["unexpected_live_cleanup"] = self.pilot.terminate_group(proc)
                        record.update(status="failed", error="live launched-group members after wrapper exit; cleanup is separate evidence")
                except Exception as exc:
                    record["group_after"] = {"error": str(exc)}
            self.persist()
        if required and record["status"] != "success":
            raise RuntimeError(f"{name} did not succeed; retained evidence")
        return record

    def qualify(self):
        if self.env.get("SOURCE_SHA") != SOURCE or self.env.get("CANDIDATE") != "namespace-goldengate6x14":
            raise ValueError("fixed source/candidate mismatch")
        self.run("preflight", [sys.executable, str(self.harness / "scripts/mac-ci-pilot.py"), "preflight", "--layer", "native",
                 "--source", str(self.source), "--evidence", str(self.evidence)], 45, observe=False)
        metadata = json.loads((self.evidence / "metadata.json").read_text())
        if not metadata["qualified"] or metadata["cpus"] != 6 or metadata["physical_memory_bytes"] != 14 * 1024**3:
            raise ValueError("qualified GoldenGate 6CPU/14GiB required")
        if metadata["sdk_version"] != "27.0" or metadata["sdk_build"] != "26A425" or "Swift version 6.4" not in metadata["swift"]:
            raise ValueError("exact Xcode/SDK/Swift qualification changed")
        if not re.search(r"ProductVersion:\s*27\.0\s", metadata["os"]) or not re.search(r"BuildVersion:\s*26A428\b", metadata["os"]):
            raise ValueError("GoldenGate observed OS27.0/26A428 changed; preserve preflight and classify a different image")
        self.env.update(DEVELOPER_DIR=metadata["developer_dir"], SDKROOT=metadata["sdk_path"],
                        CC=metadata["clang_path"], CXX=metadata["clangxx_path"])
        self.env["PATH"] = os.pathsep.join([str(Path(metadata["swift_path"]).parent), str(Path.home() / ".cargo/bin"), self.env.get("PATH", "")])
        self.run("pinned-rust-install", [metadata["rustup"], "toolchain", "install", "1.97.1", "--profile", "minimal", "--component", "rustfmt", "--component", "clippy"], 90, observe=False)
        cargo = self.small("pinned-cargo-path", [metadata["rustup"], "which", "--toolchain", "1.97.1", "cargo"])
        if cargo["status"] != "success":
            raise ValueError("pinned Cargo unavailable")
        pinned_path = Path((self.evidence / "pinned-cargo-path.stdout.log").read_text().strip()).parent
        self.env["PATH"] = str(pinned_path) + os.pathsep + self.env["PATH"]
        self.run("effective-rust", ["rustc", "-Vv"], 5, observe=False)
        if not (self.evidence / "effective-rust.stdout.log").read_text().startswith("rustc 1.97.1 "):
            raise ValueError("effective Rust differs from repository pin")
        self.small("effective-swift", ["swift", "--version"])
        self.small("console-owner", ["/usr/bin/stat", "-f", "%Su %u %g", "/dev/console"])
        self.small("runner-identity", ["/usr/bin/id"])
        self.small("filesystem", ["/bin/df", "-k", str(self.source)])

    def native(self):
        env = dict(self.env, PROFILE="debug", SLATE_LINK_PROFILE="debug", DYLD_LIBRARY_PATH=str(self.source / "target/debug"))
        self.run("debug-build", ["/usr/bin/time", "-l", "./scripts/build-mac-app.sh", "--skip-a11y-check"], 240, env=env)
        xml = self.evidence / "native-case.xml"
        case = self.run("native-case", ["/usr/bin/time", "-l", "swift", "test", "--parallel", "--num-workers", "3", "--filter", "^" + re.escape(CASE) + "$", "--xunit-output", str(xml)],
                        240, required=False, cwd=self.source / "apps/slate-mac", env=env)
        witness = {"status": "incomplete", "raw_osascript_stdout_stderr": "unmodified backend consumes these; no instrumentation added",
                   "native_requests": self.native_requests, "fixture_final_metadata": self.effects(self.fixture_paths),
                   "fixture_scope": "sampling may miss brief calls; post-test absence can be fixture teardown; XCTest assertions provide native effects witness",
                   "put_back": "Finder backend retained; no human Put Back acceptance performed"}
        if xml.exists():
            cases = list(ET.parse(xml).getroot().iter("testcase"))
            witness["xml_cases"] = [{"classname": x.get("classname"), "name": x.get("name"),
                                      "failure": len(x.findall("failure")), "error": len(x.findall("error")), "skip": len(x.findall("skipped"))} for x in cases]
            counters_clean = True
            for suite in ET.parse(xml).getroot().iter():
                if suite.tag in {"testsuite", "testsuites"}:
                    for key in ["errors", "failures", "skipped"]:
                        if key in suite.attrib and suite.attrib[key] != "0":
                            counters_clean = False
            if counters_clean and len(cases) == 1 and cases[0].get("name") == CASE.split("/")[1] and cases[0].get("classname") == CASE.split("/")[0] and not any(cases[0].find(tag) is not None for tag in ("failure", "error", "skipped")) and case["status"] == "success":
                witness["status"] = "success"
        self.summary["native_witness"] = witness
        self.persist()
        return case

    def support(self):
        helper = self.evidence / "finder-consent-helper"
        built = self.run("consent-helper-build", [self.env["CC"], "-fobjc-arc", "-isysroot", self.env["SDKROOT"],
                         "-framework", "AppKit", "-framework", "Carbon", str(self.harness / "scripts/mac-finder-consent.m"), "-o", str(helper)], 20, observe=False, required=False)
        if built["status"] == "success":
            self.small("nonprompting-consent-support-only", [str(helper)], 5)
            self.small("helper-signature", ["/usr/bin/codesign", "-dvv", str(helper)], 3)
        self.small("tcc-existing-logs", ["/usr/bin/log", "show", "--last", "8m", "--style", "compact", "--info",
                    "--predicate", 'process == "tccd" OR subsystem == "com.apple.TCC"'], 5)

    def ordinary_probe(self, native):
        if "error" in native or native.get("group_after", {}).get("live") or "error" in native.get("group_after", {}) or self.remaining() < 140:
            self.summary["ordinary_probe"] = {"status": "skipped", "reason": "native watchdog/cleanup uncertainty or insufficient remaining budget; do not add another request"}
            self.persist()
            return
        root = Path(tempfile.mkdtemp(prefix="slate-finder-diagnostic-"))
        target = root / f"slate-finder-diagnostic-{uuid.uuid4()}.md"
        trash_candidate = Path.home() / ".Trash" / target.name
        if os.path.lexists(trash_candidate):
            raise ValueError("unique disposable Trash destination unexpectedly exists")
        payload = b"# Disposable Finder diagnostic fixture\n"
        with target.open("xb") as stream:
            stream.write(payload)
        paths = {target, trash_candidate}
        probe = {"scope": "ordinary harness-to-osascript subprocess; not XCTest sender authorization", "root": str(root),
                 "fixture_sha256": hashlib.sha256(payload).hexdigest(), "before": self.effects(paths),
                 "no_retry": True, "no_restore_or_empty_trash": True}
        quoted = str(target.resolve()).replace("\\", "\\\\").replace('"', '\\"')
        argv = ["/usr/bin/osascript", "-e", f'tell application "Finder" to delete {{ POSIX file "{quoted}" }}']
        result = self.run("ordinary-osascript", argv, OSA_SECONDS, required=False)
        probe.update(phase=result, after=self.effects(paths), status=result["status"],
                     stderr_file="ordinary-osascript.stderr.log", stdout_file="ordinary-osascript.stdout.log")
        try:
            probe["trash_candidate_sha256"] = hashlib.sha256(trash_candidate.read_bytes()).hexdigest()
        except OSError as exc:
            probe["trash_candidate_read_error"] = str(exc)
        probe["file_effects_verified"] = not os.path.lexists(target) and probe.get("trash_candidate_sha256") == probe["fixture_sha256"]
        if not probe["file_effects_verified"]:
            probe["status"] = "failed"
        # Observe delayed effects, never repeat deletion after uncertain outcome.
        delayed = []
        for seconds in range(1, 6):
            if self.remaining() < 3:
                break
            time.sleep(1)
            delayed.append({"seconds_after_child": seconds, "utc": utc(), "effects": self.effects(paths)})
        probe["delayed_effects"] = delayed
        self.summary["ordinary_probe"] = probe
        self.persist()

    def finish(self):
        self.summary.update(ended_utc=utc(), elapsed_seconds=time.monotonic() - self.started,
                            native_requests=self.native_requests)
        after = {}
        for key, argv in {"sha": ["git", "rev-parse", "HEAD"], "tree": ["git", "rev-parse", "HEAD^{tree}"],
                          "tracked_status": ["git", "status", "--porcelain", "--untracked-files=no"]}.items():
            try:
                result = subprocess.run(argv, cwd=self.source, capture_output=True, text=True, timeout=3)
                after[key] = {"exit_code": result.returncode, "stdout": result.stdout.strip(), "stderr": result.stderr}
            except BaseException as exc:
                after[key] = {"error": str(exc)}
        self.summary["source_after"] = after
        if any(row.get("exit_code") != 0 for row in after.values()) or after.get("sha", {}).get("stdout") != SOURCE or after.get("tracked_status", {}).get("stdout"):
            self.summary.update(status="failed", source_qualification_error="post-diagnostic source SHA/status did not qualify")
        self.persist()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    args = parser.parse_args()
    signal.signal(signal.SIGTERM, interrupted)
    signal.signal(signal.SIGINT, interrupted)
    signal.signal(signal.SIGALRM, budget_expired)
    diag = Diagnostic(args.source.resolve(), args.evidence.resolve())
    signal.setitimer(signal.ITIMER_REAL, max(0.01, diag.remaining()))
    try:
        diag.qualify()
        native = diag.native()
        diag.support()
        diag.ordinary_probe(native)
        diag.summary["status"] = "success" if diag.summary.get("native_witness", {}).get("status") == "success" and diag.summary["ordinary_probe"]["status"] == "success" else "failed"
    except BaseException as exc:
        diag.summary.update(status="cancelled" if isinstance(exc, Interrupted) else "failed", error=str(exc))
    finally:
        signal.setitimer(signal.ITIMER_REAL, 0)
        diag.finish()
    print(json.dumps({"status": diag.summary["status"], "evidence": str(diag.evidence)}, sort_keys=True), flush=True)
    return 0 if diag.summary["status"] == "success" else 1


if __name__ == "__main__":
    sys.exit(main())
