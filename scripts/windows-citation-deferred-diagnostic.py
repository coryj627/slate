#!/usr/bin/env python3
"""PRIVATE full-app citation qualification variant; old focused history remains distinct."""
import argparse
import collections
import datetime as dt
import hashlib
import json
import os
from pathlib import Path, PurePosixPath, PureWindowsPath
import platform
import shutil
import subprocess
import sys
import time
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

SOURCE = "98c73f45ac2b81518ce652a0f6612bd3ef83c5e1"
TREE = "ae7f9507253ed87552e357dd1a373210b04bff72"
BRANCH = "refs/heads/codex/windows-citation-deferred-diagnostic"
RUN, ATTEMPT, ARTIFACT = 37138568771, 1, 11280550921
ARCHIVE = "6f1cd53f75d980e25f0629638867c839b01fbae740c58952e8b4ce3c80257f90"
MANIFEST = "93a097242cb3dc0383c8db245411054189e9c7cc2571e88be8f7e420ad67b9dd"
NATIVE = "8a0b43d62d760aabe6f37b216afb517b0f6c06b13ad047ac1e7a0be8f5ceb133"
APP_REFERENCE_SHA = "2c8d892a2f9e881b829c6f6e17df40d014879267252019cae1f1d7c3c3291974"
APP_FACTS = 4806
APP_FILTER = "FullyQualifiedName!~ConnectionsLeafTests.TheModelOf"
FACTS = (
    "SlateWindows.Tests.CitationAsyncInterleavingTests.ADeferredSummaryDoesNotAnswerForADifferentNote",
    "SlateWindows.Tests.CitationAsyncInterleavingTests.ADeferredSummaryStillAnswersForTheNoteItWasAskedAbout",
)
TEST_FILE = "apps/slate-windows/tests/SlateWindows.Tests/CitationAsyncInterleavingTests.cs"
APP_FILE = "apps/slate-windows/src/SlateWindows/WorkspaceViewModel.Citations.cs"
TEST_PROJECT = "tests/SlateWindows.Tests/SlateWindows.Tests.csproj"
APP_PROJECT = "src/SlateWindows/SlateWindows.csproj"
TEST_BIN = "tests/SlateWindows.Tests/bin/Release/net10.0-windows"
APP_BIN = "src/SlateWindows/bin/Release/net10.0-windows"
OUTPUT_ROOTS = ("src/SlateWindows/bin/Release", "tests/SlateWindows.Tests/bin/Release",
                "tests/SlateWindows.AccessibilityTests/bin/Release", "tools/GridConformanceHost/bin/Release")
REQUIRED = {
    APP_BIN + "/SlateWindows.exe", APP_BIN + "/slate_uniffi.dll",
    TEST_BIN + "/SlateWindows.Tests.dll", TEST_BIN + "/slate_uniffi.dll",
    "tests/SlateWindows.AccessibilityTests/bin/Release/net10.0-windows/SlateWindows.AccessibilityTests.dll",
    "tests/SlateWindows.AccessibilityTests/bin/Release/net10.0-windows/slate_uniffi.dll",
    "tools/GridConformanceHost/bin/Release/net10.0-windows/GridConformanceHost.exe",
}


# Exact seven missing original-project support files. No glob/name/hash fallback.
SUPPORT_FILES = (
    {'project': 'tools/HostLogProbe/HostLogProbe.csproj', 'source': 'tests/SlateWindows.Tests/bin/Release/net10.0-windows/HostLogProbe.runtimeconfig.json', 'target': 'tools/HostLogProbe/bin/Release/net10.0/HostLogProbe.runtimeconfig.json', 'bytes': 449, 'sha256': '2ca90a18bac3b48286c20acd3c1ae4f83fa38de5acf4b2f8cdd277bf90dc9bd7'},
    {'project': 'tools/HostLogProbe/HostLogProbe.csproj', 'source': 'tests/SlateWindows.Tests/bin/Release/net10.0-windows/HostLogProbe.deps.json', 'target': 'tools/HostLogProbe/bin/Release/net10.0/HostLogProbe.deps.json', 'bytes': 813, 'sha256': '131cf40698be9a6efd92d97908150075bb22f7823e674897e9b3a9be3fa6f4c4'},
    {'project': 'tools/HostLogProbe/HostLogProbe.csproj', 'source': 'tests/SlateWindows.Tests/bin/Release/net10.0-windows/HostLogProbe.exe', 'target': 'tools/HostLogProbe/obj/Release/net10.0/apphost.exe', 'bytes': 162304, 'sha256': '383253f23b240152f4a6303b1531a2503a1358033d87803c58fec56276a670b7'},
    {'project': 'tools/ParityHarness/ParityHarness.csproj', 'source': 'tests/SlateWindows.Tests/bin/Release/net10.0-windows/ParityHarness.runtimeconfig.json', 'target': 'tools/ParityHarness/bin/Release/net10.0/ParityHarness.runtimeconfig.json', 'bytes': 449, 'sha256': '2ca90a18bac3b48286c20acd3c1ae4f83fa38de5acf4b2f8cdd277bf90dc9bd7'},
    {'project': 'tools/ParityHarness/ParityHarness.csproj', 'source': 'tests/SlateWindows.Tests/bin/Release/net10.0-windows/ParityHarness.deps.json', 'target': 'tools/ParityHarness/bin/Release/net10.0/ParityHarness.deps.json', 'bytes': 816, 'sha256': 'a31f9e197dbcfcbc7e64b4640b9ae8be1501a5700adc574a393ad6299f77369c'},
    {'project': 'tools/ParityHarness/ParityHarness.csproj', 'source': 'tests/SlateWindows.Tests/bin/Release/net10.0-windows/ParityHarness.exe', 'target': 'tools/ParityHarness/obj/Release/net10.0/apphost.exe', 'bytes': 162304, 'sha256': '894aa25b352c513eeab053194d9842ed8832a31aef2795b6a3fb16474ef5c625'},
    {'project': 'src/SlateWindows/SlateWindows.csproj', 'source': 'src/SlateWindows/bin/Release/net10.0-windows/SlateWindows.exe', 'target': 'src/SlateWindows/obj/Release/net10.0-windows/apphost.exe', 'bytes': 163328, 'sha256': '3b240123866eabebdca5bc127b508b0ca81fd465c53ebaa2cb8ac2a082dba7b9'},
)

def require(ok, message):
    if not ok:
        raise ValueError(message)


def sha(path):
    h = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def safe_relative(value):
    require(isinstance(value, str) and value and "\\" not in value and ":" not in value,
            "Unsafe payload path")
    p, win = PurePosixPath(value), PureWindowsPath(value)
    require(not p.is_absolute() and not win.is_absolute() and not win.drive
            and all(part not in ("", ".", "..") for part in value.split("/")), "Unsafe payload path")
    return p


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def verify_archive_entries(entries):
    seen = set()
    require(sum(i.file_size for i in entries) <= 1700 * 1024 * 1024, "Expanded payload unexpectedly large")
    for item in entries:
        value = item.filename.rstrip("/")
        p = safe_relative(value)
        require(str(p).casefold() not in seen and not ((item.external_attr >> 16) & 0o170000) == 0o120000,
                "Duplicate or symbolic archive member")
        seen.add(str(p).casefold())
        allowed_file = str(p) == "pilot-binaries-manifest.json" or any(str(p).startswith(root + "/") for root in OUTPUT_ROOTS)
        allowed_directory = item.is_dir() and any(root == str(p) or root.startswith(str(p) + "/") for root in OUTPUT_ROOTS)
        require(allowed_file or allowed_directory, "Archive member outside fixed binary outputs")


def verify_metadata(run, artifact, now):
    require(all(run.get(k) == v for k, v in {
        "id": RUN, "run_attempt": ATTEMPT, "head_sha": SOURCE,
        "status": "completed", "conclusion": "success"}.items()), "Wrong successful producer")
    require(all(artifact.get(k) == v for k, v in {
        "id": ARTIFACT, "name": f"windows-pilot-binaries-{RUN}-{ATTEMPT}",
        "expired": False, "digest": "sha256:" + ARCHIVE}.items()), "Wrong fixed archive")
    origin = artifact.get("workflow_run", {})
    require(origin.get("id") == RUN and origin.get("head_sha") == SOURCE
            and origin.get("repository_id") == 1241111067, "Wrong artifact repository/source")
    expires = dt.datetime.fromisoformat(artifact["expires_at"].replace("Z", "+00:00"))
    require(expires > now + dt.timedelta(hours=2), "Artifact retention too short")


class NoCrossHostAuth(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        require(urllib.parse.urlparse(newurl).scheme == "https", "Archive redirect must use HTTPS")
        result = super().redirect_request(req, fp, code, msg, headers, newurl)
        if result is not None:
            result.remove_header("Authorization")
        return result


def github(route):
    req = urllib.request.Request("https://api.github.com/repos/coryj627/slate" + route,
        headers={"Authorization": "Bearer " + os.environ["GH_TOKEN"],
                 "Accept": "application/vnd.github+json", "X-GitHub-Api-Version": "2022-11-28"})
    return urllib.request.build_opener(NoCrossHostAuth()).open(req, timeout=30)


def verify_payload(root):
    path = root / "pilot-binaries-manifest.json"
    require(sha(path) == MANIFEST, "Manifest bytes changed")
    m = read_json(path)
    require(all(m.get(k) == v for k, v in {
        "schemaVersion": 1, "revision": SOURCE, "runId": str(RUN), "producerAttempt": str(ATTEMPT),
        "candidate": "namespace-8x16", "relativeRoot": "apps/slate-windows", "nativeSha256": NATIVE}.items()),
        "Manifest provenance changed")
    require(set(m.get("requiredFiles", [])) == REQUIRED, "Required payload set changed")
    require(len(m.get("files", [])) == 449, "Expected exactly 449 transferred payload files")
    paths, natives = set(), []
    for entry in m["files"]:
        relative = safe_relative(entry["path"])
        require(str(relative).casefold() not in paths, "Duplicate payload path")
        paths.add(str(relative).casefold())
        p = root / str(relative)
        require(p.is_file() and not p.is_symlink() and type(entry["bytes"]) is int
                and p.stat().st_size == entry["bytes"] and sha(p) == entry["sha256"], "Payload hash mismatch: " + str(relative))
        if relative.name == "slate_uniffi.dll":
            require(entry["sha256"] == NATIVE, "Native identity mismatch")
            natives.append(p)
    require(len(natives) == 4, "Expected four original native copies")
    actual = {p.relative_to(root).as_posix().casefold()
              for folder in OUTPUT_ROOTS for p in (root / folder).rglob("*") if p.is_file()}
    require(actual == paths, "Missing or unrecorded output files")
    return m, natives


def support_plan(manifest):
    """Bind every source to its exact pinned payload row and original project."""
    index = {}
    for entry in manifest.get("files", []):
        key = str(safe_relative(entry["path"]))
        require(key.casefold() not in index, "Duplicate support manifest path")
        index[key.casefold()] = entry
    planned = []
    for specification in SUPPORT_FILES:
        source = str(safe_relative(specification["source"]))
        target = str(safe_relative(specification["target"]))
        entry = index.get(source.casefold())
        require(entry is not None and entry["path"] == source,
                "Missing exact support source: " + source)
        require(type(entry.get("bytes")) is int and entry["bytes"] == specification["bytes"]
                and entry.get("sha256") == specification["sha256"],
                "Pinned support metadata changed: " + source)
        planned.append(dict(specification, source=source, target=target))
    return planned


def stage_support_files(root, manifest):
    """Copy only pinned verified support bytes; never rebuild dependencies."""
    root = root.resolve()
    manifest_file = root / "pilot-binaries-manifest.json"
    require(sha(manifest_file) == MANIFEST and read_json(manifest_file) == manifest,
            "Support staging requires the unchanged verified manifest")
    plan = support_plan(manifest)
    # Verify all inputs and destinations before the first directory/file write.
    for entry in plan:
        source, target = root / entry["source"], root / entry["target"]
        require(source.resolve().is_relative_to(root) and source.is_file() and not source.is_symlink()
                and source.stat().st_size == entry["bytes"] and sha(source) == entry["sha256"],
                "Pinned support source missing/corrupt: " + entry["source"])
        require(target.resolve().is_relative_to(root) and not target.is_symlink(),
                "Unsafe support target: " + entry["target"])
        require(not target.exists() or (target.is_file() and target.stat().st_size == entry["bytes"]
                                       and sha(target) == entry["sha256"]),
                "Existing support target has different bytes: " + entry["target"])
    records = []
    for entry in plan:
        source, target = root / entry["source"], root / entry["target"]
        already_present = target.exists()
        target.parent.mkdir(parents=True, exist_ok=True)
        if not already_present:
            shutil.copyfile(source, target)
        require(target.stat().st_size == entry["bytes"] and sha(target) == entry["sha256"],
                "Staged support target mismatch: " + entry["target"])
        records.append({"project": entry["project"], "sourcePath": entry["source"],
                        "sourceAbsolutePath": str(source), "sourceBytes": entry["bytes"],
                        "sourceSha256": entry["sha256"], "targetPath": entry["target"],
                        "targetAbsolutePath": str(target), "targetBytes": target.stat().st_size,
                        "targetSha256": sha(target), "alreadyPresent": already_present,
                        "manifestSha256": MANIFEST, "sourceRevision": SOURCE})
    return records


def verify_trx(path):
    doc = ET.parse(path).getroot()
    rows = [x for x in doc.iter() if x.tag.endswith("}UnitTestResult")]
    counters = [x.attrib for x in doc.iter() if x.tag.endswith("}Counters")]
    require(len(counters) == 1 and len(rows) == 2, "Expected exactly two fact results/counters")
    names = [x.attrib["testName"] for x in rows]
    require(collections.Counter(names) == collections.Counter(FACTS), "Wrong filtered fact inventory")
    c = counters[0]
    require(c.get("total") == c.get("executed") == "2", "Incomplete filtered execution")
    adverse = ("error", "timeout", "aborted", "inconclusive", "passedButRunAborted", "notRunnable",
               "notExecuted", "disconnected", "warning", "completed", "inProgress", "pending")
    require(all(c.get(k) == "0" for k in adverse), "Missing/adverse TRX counter")
    result = {}
    for row in rows:
        require(row.attrib.get("outcome") in ("Passed", "Failed"), "Unclassified fact outcome")
        result[row.attrib["testName"]] = {"outcome": row.attrib["outcome"], "duration": row.attrib["duration"],
            "message": row.findtext(".//{*}Message"), "stack": row.findtext(".//{*}StackTrace")}
    require(c.get("passed") == str(sum(x["outcome"] == "Passed" for x in result.values()))
            and c.get("failed") == str(sum(x["outcome"] == "Failed" for x in result.values())), "TRX counter disagreement")
    return {"sha256": sha(path), "counters": c, "facts": result}


def read_app_reference(path):
    require(path.is_file() and not path.is_symlink() and sha(path) == APP_REFERENCE_SHA,
            "Full app reference bytes changed")
    value = read_json(path)
    require(all(value.get(k) == v for k, v in {
        "schemaVersion": 1, "sourceRevision": SOURCE, "producerRunId": RUN, "producerAttempt": ATTEMPT,
        "manifestSha256": MANIFEST, "nativeSha256": NATIVE, "expectedTotal": APP_FACTS,
        "originalTestAssemblySha256": "523ba3f79243456b40a796e1f946f15c1edca1abc3712330e5e0143b8ffd16b4",
        "filter": APP_FILTER}.items()), "Wrong full app reference provenance")
    counts = value.get("expectedNameCounts")
    require(isinstance(counts, dict) and counts and all(isinstance(name, str) and name
            and type(count) is int and count > 0 for name, count in counts.items())
            and sum(counts.values()) == APP_FACTS, "Malformed full app name multiset")
    return value


def verify_full_app_trx(path, reference):
    doc = ET.parse(path).getroot()
    rows = [x for x in doc.iter() if x.tag.endswith("}UnitTestResult")]
    counters = [x.attrib for x in doc.iter() if x.tag.endswith("}Counters")]
    summaries = [x.attrib for x in doc.iter() if x.tag.endswith("}ResultSummary")]
    require(len(rows) == APP_FACTS and len(counters) == len(summaries) == 1,
            "Full app execution is missing, duplicated or incomplete")
    require(collections.Counter(x.attrib.get("testName") for x in rows)
            == collections.Counter(reference["expectedNameCounts"]), "Full app name multiset changed")
    execution_ids = [x.attrib.get("executionId") for x in rows]
    require(all(isinstance(value, str) and value.strip() for value in execution_ids)
            and len(set(execution_ids)) == APP_FACTS, "Missing/duplicate full app execution ID")
    c = counters[0]
    require(c.get("total") == c.get("executed") == c.get("passed") == str(APP_FACTS)
            and c.get("failed") == "0", "Full app pass/execution counters failed")
    for key in ("error", "timeout", "aborted", "inconclusive", "passedButRunAborted", "notRunnable",
                "notExecuted", "disconnected", "warning", "completed", "inProgress", "pending"):
        require(c.get(key) == "0", "Missing/adverse full app TRX counter: " + key)
    require(all(x.attrib.get("outcome") == "Passed" for x in rows), "Full app has failed, skipped or unclassified facts")
    require(summaries[0].get("outcome") == "Completed", "Full app ResultSummary is not completed")
    return {"trxSha256": sha(path), "counters": c, "executionIdCount": len(execution_ids),
            "uniqueNameCount": len(reference["expectedNameCounts"]), "referenceNameMultisetMatches": True,
            "referenceSha256": APP_REFERENCE_SHA, "referenceTrxSha256": reference["referenceTrxSha256"],
            "scope": "Complete original4806 app reference; model facts remain separately excluded"}


def mutant_was_killed(value, expected_line=None):
    wrong, legit = (value["facts"][fact] for fact in FACTS)
    require(wrong["outcome"] == "Failed" and legit["outcome"] == "Passed", "Expected wrong-note kill and legitimate delivery")
    require("Assert.Null() Failure" in (wrong.get("message") or "")
            and "ADeferredSummaryDoesNotAnswerForADifferentNote" in (wrong.get("stack") or ""),
            "Mutant failed at an unrelated assertion")
    if expected_line is not None:
        require("line " + str(expected_line) in (wrong.get("stack") or ""), "Mutant did not fail at final wrong-note null")


class Harness:
    def __init__(self, source, evidence):
        self.source, self.evidence = source, evidence
        self.root = source / "apps/slate-windows"
        self.evidence.mkdir(parents=True, exist_ok=False)
        self.deadline = time.monotonic() + 40 * 60
        self.record = {"scope": "PRIVATE citation fixture plus mandatory full4806 app qualification; no complete model/shell/human acceptance", "diagnosticGlobalBudgetSeconds": 2400, "budgetQualification": "New full-app scope:40m wrapper/40m step/45m job; unchanged original app10m blame watchdog, focused90s watchdog and native/product bounds. Old14m/20m focused history remains distinct.",
            "status": "incomplete", "sourceRevision": SOURCE, "sourceTree": TREE,
            "producerRunId": RUN, "producerAttempt": ATTEMPT, "artifactId": ARTIFACT,
            "archiveSha256": ARCHIVE, "manifestSha256": MANIFEST, "nativeSha256": NATIVE,
            "executionRunId": os.environ.get("GITHUB_RUN_ID"), "executionAttempt": os.environ.get("GITHUB_RUN_ATTEMPT"),
            "harnessRevision": os.environ.get("GITHUB_SHA"), "commands": [], "states": [], "runs": []}
        self.natives = []

    def save(self):
        (self.evidence / "summary.json").write_text(json.dumps(self.record, indent=2) + "\n")

    def native_unchanged(self):
        require(self.natives and all(p.is_file() and sha(p) == NATIVE for p in self.natives), "Native bytes changed")

    def run_full_app(self, reference_path):
        reference = read_app_reference(reference_path)
        self.native_unchanged()
        require(all(sha(self.root / project) == digest for project, digest in self.record["originalProjectSha256"].items()),
                "Original project changed before full app qualification")
        folder = self.evidence / "pumped-candidate-full-app"
        folder.mkdir()
        assembly = self.root / TEST_BIN / "SlateWindows.Tests.dll"
        app = self.root / TEST_BIN / "SlateWindows.dll"
        test_hash, app_hash = sha(assembly), sha(app)
        candidate_runs = [value for value in self.record["runs"] if value["phase"] == "pumped-candidate"]
        require(len(candidate_runs) == 5 and all(value["testAssemblySha256"] == test_hash
                and value["appAssemblySha256"] == app_hash for value in candidate_runs),
                "Full app is not the same freshly compiled five-pair candidate")
        require(app_hash == self.record["originalAssemblies"]["appSha256"],
                "Full app gate must precede any production guard mutation")
        gate = {"status": "running", "filter": APP_FILTER, "expectedFacts": APP_FACTS,
                "originalAppReferenceSha256": APP_REFERENCE_SHA, "testAssemblySha256": test_hash,
                "appAssemblySha256": app_hash, "nativeSha256": NATIVE, "blameHangTimeout": "10m",
                "diagnosticCommandBudgetSeconds": 2100}
        self.record["fullAppGate"] = gate
        self.save()
        try:
            code, _ = self.command("pumped-candidate-full-app", ["dotnet", "test", str(assembly),
                "--filter", APP_FILTER, "--logger", "trx;LogFileName=app.trx", "--results-directory", str(folder),
                "--blame-hang-timeout", "10m", "--blame-hang-dump-type", "mini"],
                max_seconds=35 * 60, allow_test_failure=True)
            self.native_unchanged()
            require(sha(assembly) == test_hash and sha(app) == app_hash,
                    "Full app candidate or production assembly changed during execution")
            gate.update(verify_full_app_trx(folder / "app.trx", reference))
            require(code == 0, "Full app process did not pass")
            gate.update({"status": "success", "exitCode": code, "nativeUnchanged": True,
                         "candidateAndOriginalProductionUnchanged": True})
        except BaseException as error:
            gate.update({"status": "failed-or-incomplete", "error": str(error)})
            if (folder / "app.trx").is_file():
                gate["failedOrIncompleteRawTrxSha256"] = sha(folder / "app.trx")
            raise
        finally:
            self.save()

    def command(self, name, args, cwd=None, max_seconds=180, allow_test_failure=False):
        remaining = self.deadline - time.monotonic()
        require(remaining > 1, "Diagnostic deadline exhausted")
        log = self.evidence / (name + ".log")
        started = time.monotonic()
        with log.open("w", encoding="utf-8") as output:
            child = subprocess.Popen(args, cwd=cwd or self.source, stdout=output, stderr=subprocess.STDOUT)
            try:
                result = child.wait(timeout=min(max_seconds, remaining))
            except BaseException:
                # Only this explicitly owned child's tree; no name/global process kills.
                subprocess.run(["taskkill", "/PID", str(child.pid), "/T", "/F"], stdout=output,
                               stderr=subprocess.STDOUT, timeout=15, check=False)
                child.wait(timeout=10)
                raise
        self.record["commands"].append({"name": name, "argv": args, "cwd": str(cwd or self.source),
            "exitCode": result, "elapsedSeconds": time.monotonic() - started, "logSha256": sha(log)})
        self.save()
        require(result == 0 or (allow_test_failure and result == 1), "Command failed: " + name)
        return result, log.read_text(encoding="utf-8-sig")

    def state(self, name):
        self.native_unchanged()
        folder = self.evidence / name
        folder.mkdir()
        files = [self.source / TEST_FILE, self.source / APP_FILE,
                 self.root / TEST_PROJECT, self.root / APP_PROJECT, self.root / "global.json"]
        files += [p for p in self.root.rglob("*.cs") if "bin" not in p.parts and "obj" not in p.parts]
        files += [p for p in self.root.rglob("*.csproj") if "bin" not in p.parts and "obj" not in p.parts]
        for assembly in (self.root / TEST_BIN / "SlateWindows.Tests.dll", self.root / TEST_BIN / "SlateWindows.dll"):
            shutil.copyfile(assembly, folder / assembly.name)
        for p in (self.source / TEST_FILE, self.source / APP_FILE, self.root / TEST_PROJECT, self.root / APP_PROJECT):
            shutil.copyfile(p, folder / p.name)
        for p in self.root.rglob("project.assets.json"):
            target = folder / p.relative_to(self.root)
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(p, target)
        manifest = {str(p.relative_to(self.source)): sha(p) for p in sorted(set(files))}
        for directory in (self.root / TEST_BIN, self.root / APP_BIN):
            for p in sorted(directory.rglob("*")):
                if p.is_file():
                    manifest[str(p.relative_to(self.source))] = sha(p)
        (folder / "source-and-assembly-hashes.json").write_text(json.dumps(manifest, indent=2) + "\n")
        self.record["states"].append({"name": name, "hashManifestSha256": sha(folder / "source-and-assembly-hashes.json"),
                                       "nativeUnchanged": True})
        self.save()

    def run_pair(self, phase, number):
        self.native_unchanged()
        label = f"{phase}-{number}"
        folder = self.evidence / label
        folder.mkdir()
        code, _ = self.command(label, ["dotnet", "test", str(self.root / TEST_BIN / "SlateWindows.Tests.dll"),
            "--filter", "|".join("FullyQualifiedName=" + fact for fact in FACTS),
            "--logger", "trx;LogFileName=citation.trx", "--results-directory", str(folder),
            "--blame-hang-timeout", "90s", "--blame-hang-dump-type", "mini"],
            max_seconds=110, allow_test_failure=True)
        value = verify_trx(folder / "citation.trx")
        require(code == (0 if all(x["outcome"] == "Passed" for x in value["facts"].values()) else 1),
                "Test process/TRX outcome disagreement")
        value.update({"phase": phase, "number": number, "exitCode": code, "nativeUnchanged": True,
            "testAssemblySha256": sha(self.root / TEST_BIN / "SlateWindows.Tests.dll"),
            "appAssemblySha256": sha(self.root / TEST_BIN / "SlateWindows.dll")})
        self.record["runs"].append(value)
        self.save()
        return value

    def stage_reference(self, project, prior_dll):
        _, value = self.command("targetpath-" + Path(project).stem, ["dotnet", "msbuild", project,
            "-nologo", "-t:GetTargetPath", "-getProperty:TargetPath,TargetRefPath",
            "-p:Configuration=Release", "-p:BuildProjectReferences=false"], cwd=self.root)
        properties = json.loads(value)["Properties"]
        staged = []
        for key in ("TargetPath", "TargetRefPath"):
            if key == "TargetRefPath" and not properties.get(key):
                continue
            target = Path(properties[key])
            require(target.is_absolute() and target.suffix == ".dll" and target.resolve().is_relative_to(self.root.resolve()),
                    "Unexpected GetTargetPath/ref result")
            target.parent.mkdir(parents=True, exist_ok=True)
            if target.resolve() != prior_dll.resolve():
                shutil.copyfile(prior_dll, target)
            require(sha(target) == sha(prior_dll), "Staged reference mismatch")
            staged.append({"property": key, "path": str(target), "sha256": sha(target)})
        self.record.setdefault("stagedReferences", []).append({"project": project, "targets": staged,
            "originalPayloadReference": str(prior_dll), "sha256": sha(prior_dll)})
        self.save()

    def build(self, phase, project):
        self.native_unchanged()
        self.command(phase, ["dotnet", "build", project, "--configuration", "Release", "--no-restore",
                            "-p:BuildProjectReferences=false"], cwd=self.root, max_seconds=240)
        self.native_unchanged()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    parser.add_argument("--patch", type=Path, required=True)
    parser.add_argument("--build-mode", choices=("staged-projects",), default="staged-projects")
    parser.add_argument("--app-reference", type=Path, required=True)
    args = parser.parse_args()
    h = Harness(args.source.resolve(), args.evidence.resolve())
    original_test = (h.source / TEST_FILE).read_bytes()
    original_app = (h.source / APP_FILE).read_bytes()
    try:
        read_app_reference(args.app_reference)
        h.record["originalProjectSha256"] = {TEST_PROJECT: sha(h.root / TEST_PROJECT), APP_PROJECT: sha(h.root / APP_PROJECT)}
        h.save()
        require(os.name == "nt" and os.environ.get("PROCESSOR_ARCHITECTURE") == "AMD64", "Windows x64 required")
        require(os.cpu_count() == 4 and platform.win32_ver()[1] == "10.0.26100", "Original hosted topology/OS changed")
        h.record["runner"] = {"logicalProcessors": os.cpu_count(), "windowsVersion": platform.win32_ver(),
                              "imageOS": os.environ.get("ImageOS"), "imageVersion": os.environ.get("ImageVersion")}
        require(os.environ.get("GITHUB_EVENT_NAME") == "workflow_dispatch" and os.environ.get("GITHUB_REF") == BRANCH
                and os.environ.get("GITHUB_REPOSITORY") == "coryj627/slate", "Wrong manual scratch execution")
        _, head = h.command("source-revision", ["git", "rev-parse", "HEAD"])
        _, tree = h.command("source-tree", ["git", "rev-parse", "HEAD^{tree}"])
        _, dirty = h.command("source-status", ["git", "status", "--porcelain"])
        require(head.strip() == SOURCE and tree.strip() == TREE and not dirty.strip(), "Source must be clean exact98")
        _, sdk = h.command("sdk", ["dotnet", "--version"], cwd=h.root)
        require(sdk.strip() == "10.0.401", "Effective SDK changed")
        _, runtimes = h.command("runtimes", ["dotnet", "--list-runtimes"], cwd=h.root)
        for framework in ("Microsoft.NETCore.App", "Microsoft.WindowsDesktop.App"):
            versions = [tuple(map(int, line.split()[1].split("."))) for line in runtimes.splitlines()
                        if line.startswith(framework + " 10.0.")]
            require(versions and max(versions) == (10, 0, 12), "Effective runtime patch changed")
        require(os.environ.get("DOTNET_ROLL_FORWARD") in (None, "", "Minor"), "Unexpected runtime roll-forward")
        require(sha(args.patch) == "8fd19c5370b8eed3956b028c4f572c479fd9163eca328bd2e9660c1dca2e8e13", "Candidate patch changed")
        with github(f"/actions/runs/{RUN}/attempts/{ATTEMPT}") as response:
            run = json.load(response)
        with github(f"/actions/artifacts/{ARTIFACT}") as response:
            artifact = json.load(response)
        verify_metadata(run, artifact, dt.datetime.now(dt.timezone.utc))
        (h.evidence / "original-producer-metadata.json").write_text(json.dumps({"run": run, "artifact": artifact}, indent=2))
        archive = h.evidence.parent / (h.evidence.name + "-original-payload.zip")
        with github(f"/actions/artifacts/{ARTIFACT}/zip") as response, archive.open("wb") as target:
            size = 0
            while block := response.read(1024 * 1024):
                size += len(block)
                require(size <= 640 * 1024 * 1024 and time.monotonic() < h.deadline, "Archive size/time limit")
                target.write(block)
        require(sha(archive) == ARCHIVE and size == artifact["size_in_bytes"], "Original archive hash/size mismatch")
        with zipfile.ZipFile(archive) as z:
            verify_archive_entries(z.infolist())
            z.extractall(h.root)
        manifest, h.natives = verify_payload(h.root)
        shutil.copyfile(h.root / "pilot-binaries-manifest.json", h.evidence / "pilot-binaries-manifest.json")
        h.record.update({"verifiedPayloadFiles": 449, "archiveBytesIndependentlyHashed": size,
                         "buildMode": args.build_mode, "candidatePatchSha256": sha(args.patch)})
        original_test_hash = sha(h.root / TEST_BIN / "SlateWindows.Tests.dll")
        original_app_hash = sha(h.root / TEST_BIN / "SlateWindows.dll")
        h.record["originalAssemblies"] = {"testSha256": original_test_hash, "appSha256": original_app_hash,
            "ffiBindingSha256": sha(h.root / TEST_BIN / "SlateUniffi.dll"),
            "parityHarnessSha256": sha(h.root / TEST_BIN / "ParityHarness.dll")}
        h.state("original")
        # Do not retry/erase any control failure; all five outcomes remain evidence.
        for i in range(1, 6):
            h.run_pair("original-ambient", i)
        # Restore never compiles Rust or generated bindings. Only original package graph is restored.
        h.command("restore-original-test-project", ["dotnet", "restore", TEST_PROJECT], cwd=h.root, max_seconds=180)
        for project, name in (("src/SlateUniffi/SlateUniffi.csproj", "SlateUniffi.dll"),
                              (APP_PROJECT, "SlateWindows.dll"),
                              ("tools/ParityHarness/ParityHarness.csproj", "ParityHarness.dll")):
            h.stage_reference(project, h.root / TEST_BIN / name)
        if args.build_mode == "staged-projects":
            h.native_unchanged()
            h.record["stagedSupportFiles"] = stage_support_files(h.root, manifest)
            h.native_unchanged()
            h.save()
        # Never create generated C# bindings; their absence makes accidental core compilation fail.
        generated_native = h.root / "src/SlateUniffi/generated/slate_uniffi.dll"
        generated_native.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(h.natives[0], generated_native)
        h.natives.append(generated_native)
        test_project, app_project = Path(TEST_PROJECT), Path(APP_PROJECT)
        h.command("check-candidate-patch", ["git", "apply", "--check", str(args.patch.resolve())])
        h.command("apply-candidate-patch", ["git", "apply", str(args.patch.resolve())])
        candidate_text = (h.source / TEST_FILE).read_text(encoding="utf-8")
        wrong_start = candidate_text.index("public void ADeferredSummaryDoesNotAnswerForADifferentNote()")
        wrong_end = candidate_text.index("public void ADeferredSummaryStillAnswersForTheNoteItWasAskedAbout()", wrong_start)
        nulls = []
        at = wrong_start
        while (at := candidate_text.find("Assert.Null(workspace.CitationSummary);", at, wrong_end)) != -1:
            nulls.append(at)
            at += 1
        require(len(nulls) == 2, "Candidate must retain first-null and final wrong-note null")
        final_null_line = candidate_text[:nulls[-1]].count("\n") + 1
        h.record["expectedMutantFinalNullLine"] = final_null_line
        h.build("build-pumped-original-project", str(test_project))
        require(sha(h.root / TEST_BIN / "SlateWindows.Tests.dll") != original_test_hash, "Candidate test assembly did not change")
        require(sha(h.root / TEST_BIN / "SlateWindows.dll") == original_app_hash, "Candidate unexpectedly changed production assembly")
        h.state("pumped-candidate")
        candidate_runs = [h.run_pair("pumped-candidate", i) for i in range(1, 6)]
        require(all(x["outcome"] == "Passed" for run in candidate_runs for x in run["facts"].values()),
                "Candidate failed; retain all five completed pair outcomes")
        h.run_full_app(args.app_reference)
        guard = "string.Equals(Citations.Path, asked, StringComparison.Ordinal)"
        body = original_app.decode("utf-8")
        require(body.count(guard) == 1, "Guard mutation must target exactly one expression")
        (h.source / APP_FILE).write_text(body.replace(guard, "asked is not null"), encoding="utf-8", newline="")
        h.build("build-guard-mutant-app", str(app_project))
        h.build("build-guard-mutant-tests", str(test_project))
        h.state("guard-removal-mutant")
        require(sha(h.root / TEST_BIN / "SlateWindows.dll") != original_app_hash, "Mutant production assembly did not change")
        require(sha(h.root / TEST_BIN / "SlateWindows.dll") == sha(h.root / APP_BIN / "SlateWindows.dll"), "Mutant app was not copied to test execution")
        mutant_was_killed(h.run_pair("guard-removal-mutant", 1), expected_line=final_null_line)
        h.record["status"] = "qualified-candidate-full-app-diagnostic"
        h.record["interpretation"] = "Candidate five pairs and complete4806 app reference passed before targeted mutant kill; original controls retained. Not a sole-cause proof or complete model/shell/human certificate."
    except BaseException as error:
        h.record["status"] = "failed-or-incomplete"
        h.record["error"] = str(error)
        raise
    finally:
        # Preserve mutant evidence before restoring only the two owned source bytes.
        (h.source / TEST_FILE).write_bytes(original_test)
        (h.source / APP_FILE).write_bytes(original_app)
        h.record["ownedSourcesRestored"] = sha(h.source / TEST_FILE) == hashlib.sha256(original_test).hexdigest() and sha(h.source / APP_FILE) == hashlib.sha256(original_app).hexdigest()
        h.record["originalProjectsStillOriginal"] = all(sha(h.root / project) == digest for project, digest in h.record.get("originalProjectSha256", {}).items())
        h.record["nativeBytesStillOriginal"] = bool(h.natives) and all(p.is_file() and sha(p) == NATIVE for p in h.natives)
        h.save()


if __name__ == "__main__":
    main()
