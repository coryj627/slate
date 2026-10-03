#!/usr/bin/env python3
"""Paired Mac evidence at a frozen source; no account cache operations."""

from __future__ import annotations

import argparse
import ctypes
import hashlib
import json
import math
import os
from pathlib import Path
import platform
import re
import shutil
import signal
import subprocess
import sys
import time
from datetime import datetime, timezone
import xml.etree.ElementTree as ET


REFERENCE = "e6a8337be2905b3f43841e6ab456b0f57a7d91e4"
INVENTORY_SHA = "a145b175c021c7268815d3615659f65bf8889a13b09bbcd592c86b5d8b6ec316"
ANALYZER = "bcaddd56931ce14d32cebcf42ea9f5b08ed5f7d8"
CANDIDATES = {"hosted-xcode27", "namespace-goldengate6x14"}
STATES = {"cold": "cold-products-no-explicit-restore", "warm": "warm-same-vm-incremental"}
NATIVE_PHASES = ("debug", "xctest", "cli", "release", "release-witness")
ANALYZER_PHASES = ("version", "human", "json", "sarif")


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def utc() -> str:
    return datetime.now(timezone.utc).isoformat()


def sha(path: Path) -> str:
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_suffix(path.suffix + ".tmp")
    temp.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n")
    temp.replace(path)


def command(args: list[str], cwd: Path | None = None, env: dict | None = None) -> str:
    return subprocess.check_output(args, cwd=cwd, env=env, text=True, stderr=subprocess.STDOUT).strip()


def validate_inputs(source: str, candidate: str, pair: str) -> None:
    require(re.fullmatch(r"[0-9a-f]{40}", source) is not None, "source_sha must be a literal lowercase 40-character commit")
    require(candidate in CANDIDATES, "unsupported candidate")
    require(re.fullmatch(r"[a-z0-9][a-z0-9.-]{0,63}", pair) is not None, "invalid pair_id")


def context() -> dict:
    result = {key: os.environ.get(env, "") for key, env in {
        "source_sha": "SOURCE_SHA", "harness_sha": "GITHUB_SHA", "candidate": "CANDIDATE",
        "pair_id": "PAIR_ID", "run_id": "GITHUB_RUN_ID", "run_attempt": "GITHUB_RUN_ATTEMPT",
    }.items()}
    validate_inputs(result["source_sha"], result["candidate"], result["pair_id"])
    require(re.fullmatch(r"[0-9a-f]{40}", result["harness_sha"]) is not None, "invalid harness commit")
    require(result["run_id"].isdigit() and result["run_attempt"].isdigit(), "missing run identity")
    return result


def source_state(source: Path) -> dict:
    locks = ["Cargo.lock", "rust-toolchain.toml", "apps/slate-mac/Package.resolved"]
    return {
        "sha": command(["git", "rev-parse", "HEAD"], source),
        "tree": command(["git", "rev-parse", "HEAD^{tree}"], source),
        "tracked_status": command(["git", "status", "--porcelain", "--untracked-files=no"], source),
        "locks": {path: sha(source / path) for path in locks},
        "input_trees": {path: command(["git", "rev-parse", f"HEAD:{path}"], source) for path in
                        ["apps/slate-mac", "crates/slate-core", "crates/slate-uniffi"]},
        "tests_tree": command(["git", "rev-parse", "HEAD:apps/slate-mac/Tests"], source),
    }


def inspect_caches(source: Path) -> list[dict]:
    home = Path.home()
    paths = [source / "target", source / "apps/slate-mac/.build", home / ".cargo/registry",
             home / ".cargo/git", home / "Library/Caches/org.swift.swiftpm",
             home / "Library/Developer/Xcode/DerivedData"]
    if os.environ.get("NSC_CACHE_PATH"):
        paths.append(Path(os.environ["NSC_CACHE_PATH"]))
    rows = []
    for path in paths:
        row = {"path": str(path), "resolved": str(path.resolve()), "exists": path.exists(),
               "symlink": path.is_symlink()}
        if path.exists():
            measured = subprocess.run(["du", "-sk", str(path.resolve())], capture_output=True, text=True)
            row.update(measurement_exit=measured.returncode, measurement=measured.stdout.strip(),
                       measurement_error=measured.stderr.strip())
        rows.append(row)
    return rows


def preflight(source: Path, evidence: Path, layer: str) -> None:
    identity = context()
    evidence.mkdir(parents=True, exist_ok=True)
    metadata = {"schema": 1, "layer": layer, **identity, "observed_utc": utc(), "qualified": False,
                "cache_policy": "no explicit restore, no attached Namespace volume requested; shared image/dependency caches observed"}
    write_json(evidence / "metadata.json", metadata)
    try:
        require(platform.system() == "Darwin" and platform.machine() == "arm64", "native ARM macOS required")
        harness = Path(__file__).resolve().parent.parent
        require(command(["git", "rev-parse", "HEAD"], harness) == identity["harness_sha"], "harness checkout mismatch")
        state = source_state(source)
        metadata["source"] = state
        require(state["sha"] == identity["source_sha"] and not state["tracked_status"], "source checkout mismatch or dirty tracked files")
        require(subprocess.run(["git", "merge-base", "--is-ancestor", identity["source_sha"], identity["harness_sha"]], cwd=harness).returncode == 0,
                "source must be an ancestor of the workflow/harness commit")
        metadata["source_is_harness_ancestor"] = True
        reference_tests = command(["git", "rev-parse", f"{REFERENCE}:apps/slate-mac/Tests"], harness)
        require(state["tests_tree"] == reference_tests, "XCTest inventory source differs from the verified reference")
        metadata["reference_tests_tree"] = reference_tests
        require(re.search(r'^channel\s*=\s*"1\.97\.1"\s*$', (source / "rust-toolchain.toml").read_text(), re.M), "Rust pin changed")
        for path in [source / "target", source / "apps/slate-mac/.build"]:
            require(not os.path.lexists(path), f"cold products already exist: {path}")
        metadata.update(os=command(["sw_vers"]), cpu_model=command(["sysctl", "-n", "machdep.cpu.brand_string"]),
                        cpus=int(command(["sysctl", "-n", "hw.logicalcpu"])),
                        physical_memory_bytes=int(command(["sysctl", "-n", "hw.memsize"])),
                        disk=command(["df", "-k", str(source)]), caches=inspect_caches(source),
                        runner_image={key: os.environ.get(key) for key in ["ImageOS", "ImageVersion", "RUNNER_NAME", "RUNNER_ARCH", "NSC_CACHE_PATH", "NSC_INSTANCE_ID"]})
        if identity["candidate"] == "namespace-goldengate6x14" and os.environ.get("NSC_CACHE_PATH"):
            require(not os.path.lexists(os.environ["NSC_CACHE_PATH"]), "unexpected Namespace cache path present; no-volume comparison cannot qualify")
        developers = sorted({p.resolve() for p in Path("/Applications").glob("Xcode*.app/Contents/Developer")})
        attempts = []
        for developer in developers:
            env = dict(os.environ, DEVELOPER_DIR=str(developer))
            result = subprocess.run(["xcodebuild", "-version"], env=env, capture_output=True, text=True)
            attempts.append({"developer_dir": str(developer), "exit_code": result.returncode, "stdout": result.stdout, "stderr": result.stderr})
            if result.returncode == 0 and result.stdout.strip() == "Xcode 27.0\nBuild version 27A266a":
                metadata["developer_dir"] = str(developer)
                metadata["xcode"] = result.stdout.strip()
                break
        metadata["xcode_candidates"] = attempts
        require("developer_dir" in metadata, "required Xcode 27.0 build 27A266a is unavailable")
        env = dict(os.environ, DEVELOPER_DIR=metadata["developer_dir"])
        metadata.update(swift=command(["xcrun", "swift", "--version"], env=env),
                        swift_path=command(["xcrun", "--find", "swift"], env=env),
                        sdk_path=command(["xcrun", "--sdk", "macosx", "--show-sdk-path"], env=env),
                        sdk_version=command(["xcrun", "--sdk", "macosx", "--show-sdk-version"], env=env),
                        sdk_build=command(["xcrun", "--sdk", "macosx", "--show-sdk-build-version"], env=env),
                        rustup=shutil.which("rustup", path=str(Path.home() / ".cargo/bin") + os.pathsep + os.environ.get("PATH", "")))
        require(metadata["rustup"] is not None, "rustup is required for the pinned toolchain")
        metadata["qualified"] = True
    except Exception as exc:
        metadata["error"] = str(exc)
        raise
    finally:
        write_json(evidence / "metadata.json", metadata)


class Cancelled(Exception):
    pass


def cancelled(signum, frame):
    raise Cancelled(f"received signal {signum}")


class Runner:
    def __init__(self, source: Path, evidence: Path, layer: str):
        self.source, self.evidence = source, evidence
        self.metadata = json.loads((evidence / "metadata.json").read_text())
        require(self.metadata.get("qualified") is True and self.metadata["layer"] == layer, "preflight is not qualified")
        require(all(self.metadata[key] == value for key, value in context().items()), "preflight identity mismatch")
        self.env = dict(os.environ, DEVELOPER_DIR=self.metadata["developer_dir"])
        self.env["PATH"] = os.pathsep.join([str(Path(self.metadata["swift_path"]).parent),
                                         str(Path.home() / ".cargo/bin"), self.env.get("PATH", "")])
        require(command(["swift", "--version"], env=self.env) == self.metadata["swift"], "bare Swift is not the qualified Xcode tool")
        self.summary = {"schema": 1, "layer": layer, **context(), "status": "incomplete", "metadata": self.metadata,
                        "passes": {}, "phases": {}, "started_utc": utc(), "test_workers": 3,
                        "build_workers": "provider toolchain defaults"}
        self.save()

    def save(self):
        write_json(self.evidence / "summary.json", self.summary)

    def run(self, name: str, args: list[str], cwd: Path | None = None, env: dict | None = None, stdout_file: Path | None = None) -> dict:
        log = self.evidence / (name + ".log")
        output = stdout_file or log
        error = (self.evidence / (name + ".stderr")) if stdout_file else log
        active_env = env or self.env
        record = {"command": args, "cwd": str(cwd or self.source), "started_utc": utc(), "status": "running",
                  "environment": {key: active_env.get(key) for key in ["DEVELOPER_DIR", "PROFILE", "SLATE_LINK_PROFILE", "DYLD_LIBRARY_PATH", "PATH"]},
                  "machine_before": machine_memory()}
        self.summary["phases"][name] = record
        self.save()
        print(f"Mac pilot | phase {name} started", flush=True)
        start = time.monotonic()
        proc = None
        peak_rss = 0
        try:
            with output.open("w") as out:
                with error.open("w") if stdout_file else open(os.devnull, "w") as err:
                    proc = subprocess.Popen(["/usr/bin/time", "-l", *args], cwd=cwd or self.source,
                                            env=active_env, stdout=out, stderr=err if stdout_file else subprocess.STDOUT,
                                            start_new_session=True)
                    samples = self.evidence / (name + ".processes.jsonl")
                    with samples.open("w") as stream:
                        next_memory_sample = 0.0
                        while proc.poll() is None:
                            snapshot = subprocess.run(["ps", "-eo", "pid,ppid,pcpu,rss,command"], capture_output=True, text=True)
                            rows = {}
                            for line in snapshot.stdout.splitlines()[1:]:
                                bits = line.strip().split(None, 4)
                                if len(bits) == 5 and bits[0].isdigit() and bits[1].isdigit():
                                    rows[int(bits[0])] = bits
                            descendants = {proc.pid}
                            for _ in range(len(rows)):
                                extended = descendants | {pid for pid, bits in rows.items() if int(bits[1]) in descendants}
                                if extended == descendants:
                                    break
                                descendants = extended
                            selected = [rows[pid] for pid in descendants if pid in rows]
                            peak_rss = max(peak_rss, sum(int(bits[3]) for bits in selected))
                            sample = {"utc": utc(), "processes": selected, "sampling_exit": snapshot.returncode}
                            if time.monotonic() >= next_memory_sample:
                                sample["machine_memory"] = machine_memory()
                                next_memory_sample = time.monotonic() + 5
                            stream.write(json.dumps(sample) + "\n")
                            time.sleep(0.5)
                    record.update(exit_code=proc.returncode, status="success" if proc.returncode == 0 else "failed")
        except BaseException as exc:
            record.update(status="cancelled" if isinstance(exc, Cancelled) else "failed", error=str(exc))
            if proc:
                record["cancellation_cleanup"] = terminate_group(proc)
                record["exit_code"] = proc.returncode
            raise
        finally:
            record.update(ended_utc=utc(), wall_seconds=time.monotonic() - start,
                          sampled_descendant_peak_rss_kib=peak_rss,
                          resource_scope="time -l child maximum plus sampled descendant RSS; not whole-machine peak",
                          machine_after=machine_memory())
            if error.exists():
                text = error.read_text(errors="replace")
                cpu = re.search(r"([\d.]+)\s+real\s+([\d.]+)\s+user\s+([\d.]+)\s+sys", text)
                maximum = re.search(r"(\d+)\s+maximum resident set size", text)
                record["time_l"] = {
                    "real_seconds": float(cpu[1]) if cpu else None,
                    "user_seconds": float(cpu[2]) if cpu else None,
                    "system_seconds": float(cpu[3]) if cpu else None,
                    "child_max_rss_bytes_darwin": int(maximum[1]) if maximum else None,
                    "scope": "maximum reported by this command's time -l; not cumulative rusage or sum of processes",
                }
            self.save()
        require(record["exit_code"] == 0, f"{name} exited {record['exit_code']}; see {output}")
        print(f"Mac pilot | phase {name} passed in {record['wall_seconds']:.3f}s", flush=True)
        return record

    def rust(self):
        self.run("toolchain", [self.metadata["rustup"], "toolchain", "install", "1.97.1", "--profile", "minimal", "--component", "rustfmt", "--component", "clippy"])
        pinned_cargo = command([self.metadata["rustup"], "which", "--toolchain", "1.97.1", "cargo"], env=self.env)
        pinned_rust = command([self.metadata["rustup"], "which", "--toolchain", "1.97.1", "rustc"], env=self.env)
        require(Path(pinned_cargo).parent == Path(pinned_rust).parent, "pinned Rust/Cargo directories differ")
        self.env["PATH"] = str(Path(pinned_cargo).parent) + os.pathsep + self.env["PATH"]
        version = command(["rustc", "-Vv"], self.source, self.env)
        require(version.startswith("rustc 1.97.1 "), "effective Rust is not the repository pin")
        cargo_version = command(["cargo", "-V"], self.source, self.env)
        require(cargo_version.startswith("cargo 1.97.1 "), "effective Cargo is not the repository pin")
        self.metadata.update(rust=version, cargo=cargo_version, rustc_path=pinned_rust, cargo_path=pinned_cargo,
                             active_toolchain=command(["rustup", "show", "active-toolchain"], self.source, self.env))
        write_json(self.evidence / "metadata.json", self.metadata)
        self.save()


def group_members(group: int) -> tuple[list[int], list[int]]:
    snapshot = subprocess.run(["ps", "-eo", "pid,pgid,stat"], capture_output=True, text=True, timeout=0.5)
    require(snapshot.returncode == 0, "cannot observe process-group cleanup")
    live, zombies = [], []
    for line in snapshot.stdout.splitlines()[1:]:
        bits = line.split()
        if len(bits) >= 3 and bits[0].isdigit() and bits[1].isdigit() and int(bits[1]) == group:
            (zombies if bits[2].startswith("Z") else live).append(int(bits[0]))
    return live, zombies


def terminate_group(proc: subprocess.Popen, grace: float = 3.0) -> dict:
    """Wait for the group, not just time(1); retain unknown/escaped boundaries."""
    result = {"scope": "launched process group only; escaped sessions/shared Finder are not covered"}
    started = time.monotonic()
    try:
        try:
            os.killpg(proc.pid, signal.SIGTERM)
        except ProcessLookupError:
            pass
        deadline = started + grace
        while True:
            proc.poll()  # Reap the wrapper without mistaking its exit for child exit.
            live, zombies = group_members(proc.pid)
            if not live or time.monotonic() >= deadline:
                break
            time.sleep(0.05)
        result["pids_after_term_grace"] = live
        if live:
            try:
                os.killpg(proc.pid, signal.SIGKILL)
                result["sigkill_sent"] = True
            except ProcessLookupError:
                pass
        deadline = time.monotonic() + 1
        while True:
            proc.poll()
            live, zombies = group_members(proc.pid)
            if not live or time.monotonic() >= deadline:
                break
            time.sleep(0.05)
        result.update(live_group_pids_after=live, zombie_group_pids_after=zombies,
                      group_has_no_live_members=not live, wrapper_exit_code=proc.poll())
    except (OSError, subprocess.TimeoutExpired, ValueError) as exc:
        result.update(error=str(exc), group_has_no_live_members=False)
        try:
            os.killpg(proc.pid, signal.SIGKILL)
            result["sigkill_sent_without_confirmation"] = True
        except ProcessLookupError:
            pass
        except OSError as kill_error:
            result["kill_error"] = str(kill_error)
    try:
        result["wrapper_exit_code"] = proc.wait(timeout=0.5)
    except subprocess.TimeoutExpired:
        result.update(wrapper_exit_code=None, group_has_no_live_members=False)
    result["cleanup_seconds"] = time.monotonic() - started
    return result


def machine_memory() -> dict:
    observations = {}
    for label, args in {"vm_stat": ["vm_stat"], "swap": ["sysctl", "vm.swapusage"]}.items():
        try:
            row = subprocess.run(args, capture_output=True, text=True, timeout=2)
            observations[label] = {"exit_code": row.returncode, "stdout": row.stdout.strip(), "stderr": row.stderr.strip()}
        except (OSError, subprocess.TimeoutExpired) as exc:
            observations[label] = {"error": str(exc)}
    return observations


def parse_xctest(path: Path, enforce: bool = True) -> dict:
    root = ET.parse(path).getroot()
    cases = list(root.iter("testcase"))
    require(all(case.get("classname") and case.get("name") for case in cases), "unnamed XCTest case")
    names = sorted(case.attrib["classname"] + "." + case.attrib["name"] for case in cases)
    result = {"count": len(cases), "digest": hashlib.sha256("\n".join(names).encode()).hexdigest(),
              "failures": sum(len(case.findall("failure")) for case in cases),
              "errors": sum(len(case.findall("error")) for case in cases),
              "skips": sum(len(case.findall("skipped")) for case in cases)}
    for suite in root.iter():
        if suite.tag not in {"testsuite", "testsuites"}:
            continue
        for attribute, key in [("failures", "failures"), ("errors", "errors"), ("skipped", "skips")]:
            if attribute in suite.attrib:
                require(re.fullmatch(r"\d+", suite.attrib[attribute]) is not None, "invalid suite counter")
                result[key] = max(result[key], int(suite.attrib[attribute]))
    if enforce:
        validate_xctest(result)
    return result


def validate_xctest(result: dict) -> None:
    require(type(result.get("count")) is int and result["count"] == 3032, "complete 3032-case XCTest inventory required")
    require(result.get("digest") == INVENTORY_SHA, "XCTest inventory digest mismatch")
    require(all(type(result.get(key)) is int and result[key] == 0 for key in ["failures", "errors", "skips"]), "XCTest failures/errors/skips are forbidden")


def analyzer_floor(results: dict, raw_min: str = "100") -> dict:
    score = results.get("score", {})
    value, errors = score.get("score"), score.get("totalErrors")
    require(type(value) in (int, float) and math.isfinite(value) and 0 <= value <= 100, "invalid analyzer score")
    require(type(errors) is int and errors >= 0, "invalid analyzer error count")
    require(raw_min.strip() and re.fullmatch(r"(?:\d+(?:\.\d*)?|\.\d+)", raw_min.strip()) is not None, "invalid MIN_SCORE")
    minimum = float(raw_min)
    require(math.isfinite(minimum) and minimum == 100, "pilot must enforce MIN_SCORE=100")
    require(errors == 0 and value >= minimum, "analyzer score/error gate failed")
    return {"score": value, "errors": errors, "minimum": minimum, "warnings": score.get("totalWarnings"), "passed": True}


def verify_release(source: Path, output: Path) -> None:
    bundle = source / "apps/slate-mac/.build/release/SlateMac.app"
    binary, library = bundle / "Contents/MacOS/SlateMac", bundle / "Contents/Frameworks/libslate_uniffi.dylib"
    require(binary.is_file() and library.is_file(), "actual Release bundle is missing")
    checks = {}
    for key, args in {
        "signature": ["codesign", "--verify", "--deep", "--strict", str(bundle)],
        "plist": ["plutil", "-lint", str(bundle / "Contents/Info.plist")],
        "links": ["otool", "-L", str(binary)], "library_id": ["otool", "-D", str(library)],
        "load_commands": ["otool", "-l", str(library)], "architecture": ["lipo", "-archs", str(binary)],
    }.items():
        checks[key] = command(args)
    expected = "@executable_path/../Frameworks/libslate_uniffi.dylib"
    links = [line.strip().split()[0] for line in checks["links"].splitlines()[1:] if "libslate_uniffi.dylib" in line]
    require(links == [expected], "bundle links the wrong native library")
    require(checks["library_id"].splitlines()[1:] == [expected], "bundled dylib ID is not relative")
    require(checks["architecture"].strip() == "arm64", "Release executable is not ARM64")
    # This mode is a fresh process for each pass: dyld cannot reuse a prior load.
    loaded = ctypes.CDLL(str(library.resolve()))
    contract = loaded.ffi_slate_uniffi_uniffi_contract_version
    contract.restype = ctypes.c_uint32
    require(contract() == 30, "bundled FFI contract mismatch")
    exports = sorted(command(["nm", "-gU", str(library)]).splitlines())
    result = {"passed": True, "ffi_contract": 30, "relative_link": expected, "checks": checks,
              "exports_sha256": hashlib.sha256("\n".join(exports).encode()).hexdigest(),
              "files": [{"path": str(path.relative_to(bundle)), "bytes": path.stat().st_size, "sha256": sha(path)}
                        for path in sorted(bundle.rglob("*")) if path.is_file()]}
    write_json(output, result)


def unchanged(runner: Runner) -> dict:
    state = source_state(runner.source)
    before = runner.metadata["source"]
    require(state == before, "tracked source, locks or input trees changed during execution")
    return state


def native(runner: Runner) -> None:
    runner.rust()
    for state in ["cold", "warm"]:
        row = {"state": STATES[state], "status": "incomplete", "caches_before": inspect_caches(runner.source)}
        runner.summary["passes"][state] = row
        runner.save()
        env = dict(runner.env, PROFILE="debug", SLATE_LINK_PROFILE="debug", DYLD_LIBRARY_PATH=str(runner.source / "target/debug"))
        runner.run(state + ".debug", ["./scripts/build-mac-app.sh", "--skip-a11y-check"], env=env)
        xml = runner.evidence / (state + ".xctest.xml")
        runner.run(state + ".xctest", ["swift", "test", "--parallel", "--num-workers", "3", "--xunit-output", str(xml)], runner.source / "apps/slate-mac", env)
        row["xctest"] = parse_xctest(xml)
        runner.run(state + ".cli", ["make", "swift-cli"], env=env)
        cli_log = (runner.evidence / (state + ".cli.log")).read_text()
        require(all(text in cli_log for text in ["Got 3 headings:", "# Hello, Slate", "## A subheading", "### Deeper still"]), "CLI smoke output is incomplete")
        row["cli"] = {"passed": True}
        release_env = dict(runner.env)
        release_env.pop("DYLD_LIBRARY_PATH", None)
        runner.run(state + ".release", ["./scripts/build-and-launch.sh", "--no-open"], env=release_env)
        result = runner.evidence / (state + ".release.json")
        runner.run(state + ".release-witness", [sys.executable, str(Path(__file__).resolve()), "verify-release", "--source", str(runner.source), "--output", str(result)], env=release_env)
        row["release"] = json.loads(result.read_text())
        row["source_after"] = unchanged(runner)
        row.update(status="success", caches_after=inspect_caches(runner.source))
        runner.save()


def analyzer(runner: Runner) -> None:
    runner.rust()
    checkout = runner.evidence.parent / (runner.evidence.name + "-build")
    require(not checkout.exists(), "analyzer cold source/products already exist")
    runner.run("analyzer.clone", ["git", "clone", "--filter=blob:none", "--no-checkout", "https://github.com/cvs-health/ios-swiftui-accessibility-techniques.git", str(checkout)])
    runner.run("analyzer.checkout", ["git", "checkout", "--detach", ANALYZER], checkout)
    require(command(["git", "rev-parse", "HEAD"], checkout) == ANALYZER, "analyzer pin mismatch")
    build = checkout / "a11y-check"
    runner.run("analyzer.build", ["swift", "build", "-c", "release", "--jobs", "3"], build)
    binary = build / ".build/release/a11y-check"
    binary_sha = sha(binary)
    for state in ["cold", "warm"]:
        row = {"state": STATES[state], "status": "incomplete", "analyzer_sha": ANALYZER, "binary_sha256": sha(binary),
               "build_reused": state == "warm"}
        runner.summary["passes"][state] = row
        runner.save()
        require(row["binary_sha256"] == binary_sha, "analyzer binary changed before warm reuse")
        runner.run(state + ".version", [str(binary), "--version"])
        target = "apps/slate-mac/Sources/SlateMac"
        runner.run(state + ".human", [str(binary), target])
        result = runner.evidence / (state + ".analyzer.json")
        runner.run(state + ".json", [str(binary), target, "--format", "json", "--no-trend"], stdout_file=result)
        runner.run(state + ".sarif", [str(binary), target, "--format", "sarif", "--no-trend"], stdout_file=runner.evidence / (state + ".analyzer.sarif"))
        row["floor"] = analyzer_floor(json.loads(result.read_text()))
        row["source_after"] = unchanged(runner)
        row["status"] = "success"
        runner.save()
    # Keep evidence and the binary's manifest, not the multi-GB analyzer build tree.
    runner.summary["analyzer_binary_sha256"] = binary_sha


def validate_summary(summary: dict, expected: dict, layer: str) -> None:
    require(summary.get("schema") == 1 and summary.get("layer") == layer, "summary schema/layer mismatch")
    require(all(summary.get(key) == value for key, value in expected.items()), "summary identity mismatch")
    require(summary.get("status") == "success", f"{layer} did not succeed")
    require(type(summary.get("test_workers")) is int and summary["test_workers"] == 3, "pilot worker policy mismatch")
    metadata = summary.get("metadata", {})
    require(metadata.get("qualified") is True and all(metadata.get(key) == value for key, value in expected.items()), "metadata identity/qualification mismatch")
    require(metadata.get("schema") == 1 and metadata.get("layer") == layer and metadata.get("source_is_harness_ancestor") is True,
            "metadata schema/layer/ancestry mismatch")
    require(metadata.get("xcode") == "Xcode 27.0\nBuild version 27A266a", "Xcode qualification mismatch")
    require(metadata.get("rust", "").startswith("rustc 1.97.1 "), "Rust qualification mismatch")
    require(metadata.get("cargo", "").startswith("cargo 1.97.1 "), "Cargo qualification mismatch")
    source = metadata.get("source", {})
    require(source.get("sha") == expected["source_sha"] and source.get("tracked_status") == "", "source metadata mismatch")
    require(re.fullmatch(r"[0-9a-f]{40}", source.get("tree", "")) is not None, "source tree identity missing")
    require(source.get("locks") and all(re.fullmatch(r"[0-9a-f]{64}", value) is not None for value in source["locks"].values()), "source lock evidence missing")
    require(source.get("tests_tree") == metadata.get("reference_tests_tree") and source.get("tests_tree"), "reference XCTest source identity missing")
    phases = summary.get("phases", {})
    setup_phases = ["toolchain"] + (["analyzer.clone", "analyzer.checkout", "analyzer.build"] if layer == "analyzer" else [])
    for name in setup_phases:
        phase = phases.get(name, {})
        require(phase.get("status") == "success" and type(phase.get("exit_code")) is int and phase["exit_code"] == 0,
                f"required setup {name} did not pass")
    passes = summary.get("passes", {})
    require(set(passes) == {"cold", "warm"}, "both complete cold/warm passes required")
    required = NATIVE_PHASES if layer == "native" else ANALYZER_PHASES
    for state in ["cold", "warm"]:
        row = passes[state]
        require(row.get("state") == STATES[state] and row.get("status") == "success", "pass is incomplete or mislabeled")
        require(row.get("source_after") == source, "source changed or final evidence missing")
        for suffix in required:
            phase = phases.get(state + "." + suffix, {})
            require(phase.get("status") == "success" and type(phase.get("exit_code")) is int and phase["exit_code"] == 0,
                    f"required phase {state}.{suffix} did not pass")
        if layer == "native":
            validate_xctest(row.get("xctest", {}))
            require(row.get("cli", {}).get("passed") is True, "CLI witness missing")
            release = row.get("release", {})
            require(release.get("passed") is True and release.get("ffi_contract") == 30 and release.get("relative_link") == "@executable_path/../Frameworks/libslate_uniffi.dylib", "Release witness missing")
            files = release.get("files", [])
            require(files and all(type(file.get("bytes")) is int and file["bytes"] > 0 and re.fullmatch(r"[0-9a-f]{64}", file.get("sha256", "")) for file in files), "Release file manifest missing")
            require({"Contents/MacOS/SlateMac", "Contents/Frameworks/libslate_uniffi.dylib", "Contents/Info.plist"}.issubset({file.get("path") for file in files}), "required bundle manifest entries missing")
            require(all(type(release.get("checks", {}).get(key)) is str for key in ["signature", "plist", "links", "library_id", "load_commands", "architecture"]), "Release check evidence missing")
        else:
            require(row.get("analyzer_sha") == ANALYZER, "analyzer source pin mismatch")
            floor = row.get("floor", {})
            analyzer_floor({"score": {"score": floor.get("score"), "totalErrors": floor.get("errors")}})
            require(floor.get("passed") is True and floor.get("minimum") == 100, "analyzer strict gate missing")
            require(re.fullmatch(r"[0-9a-f]{64}", row.get("binary_sha256", "")), "analyzer binary provenance missing")
    if layer == "analyzer":
        require(passes["cold"]["binary_sha256"] == passes["warm"]["binary_sha256"] and passes["warm"].get("build_reused") is True, "warm analyzer is not the verified cold binary")


def aggregate(evidence: Path, native_result: str, analyzer_result: str) -> None:
    require(native_result == "success" and analyzer_result == "success", "every independent job must succeed")
    summaries = list(evidence.glob("**/summary.json"))
    require(len(summaries) == 2, "exactly two job summaries required")
    expected = context()
    layers = set()
    for path in summaries:
        summary = json.loads(path.read_text())
        layer = summary.get("layer")
        require(layer in {"native", "analyzer"} and layer not in layers, "missing/duplicate job layer")
        validate_summary(summary, expected, layer)
        layers.add(layer)
    require(layers == {"native", "analyzer"}, "missing required layer")
    print(json.dumps({"passed": True, **expected, "xctest_cases_each_pass": 3032,
                      "xctest_inventory_sha256": INVENTORY_SHA, "human_acceptance": "separate, not certified by this pilot"}, indent=2))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=["validate-inputs", "preflight", "native", "analyzer", "verify-release", "aggregate"])
    parser.add_argument("--source", type=Path)
    parser.add_argument("--evidence", type=Path)
    parser.add_argument("--layer", choices=["native", "analyzer"])
    parser.add_argument("--output", type=Path)
    parser.add_argument("--native-result", default="")
    parser.add_argument("--analyzer-result", default="")
    args = parser.parse_args()
    if args.mode == "validate-inputs":
        context()
        return 0
    if args.mode == "aggregate":
        aggregate(args.evidence, args.native_result, args.analyzer_result)
        return 0
    if args.mode == "verify-release":
        verify_release(args.source, args.output)
        return 0
    source, evidence = args.source.resolve(), args.evidence.resolve()
    if args.mode == "preflight":
        preflight(source, evidence, args.layer)
        return 0
    runner = Runner(source, evidence, args.mode)
    signal.signal(signal.SIGTERM, cancelled)
    signal.signal(signal.SIGINT, cancelled)
    try:
        (native if args.mode == "native" else analyzer)(runner)
        runner.summary["status"] = "success"
        validate_summary(runner.summary, context(), args.mode)
        return 0
    except BaseException as exc:
        runner.summary.update(status="cancelled" if isinstance(exc, Cancelled) else "failed", error=str(exc))
        # Retain failed XML facts even when swift test returned nonzero first.
        if args.mode == "native":
            for state in ["cold", "warm"]:
                xml = evidence / (state + ".xctest.xml")
                if xml.exists():
                    try:
                        runner.summary["passes"][state]["xctest"] = parse_xctest(xml, enforce=False)
                    except Exception as parse_error:
                        runner.summary["passes"][state]["xctest_parse_error"] = str(parse_error)
        # Preserve the actual native output when the Release boundary failed.
        library = source / "target/release/libslate_uniffi.dylib"
        release_failed = any(key.endswith((".release", ".release-witness")) and phase.get("status") != "success"
                             for key, phase in runner.summary["phases"].items())
        if args.mode == "native" and release_failed and library.is_file():
            shutil.copy2(library, evidence / "failed-libslate_uniffi.dylib")
        print(f"Mac pilot | {args.mode} failed: {exc}", file=sys.stderr)
        return 1
    finally:
        runner.summary["ended_utc"] = utc()
        runner.save()


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"Mac pilot | {exc}", file=sys.stderr)
        raise SystemExit(1)
