#!/usr/bin/env python3
"""PRIVATE graph-only diagnostic: exact failed source5f binary control, then test-only diagnostics."""
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
import time
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

SOURCE = "5f29ebc72fc4a308e714f971ba73e3558e25552f"
TREE = "e875c5613f7b8f235dcc16456aac6ac61cf9bc07"
BRANCH = "refs/heads/codex/windows-graph-durability-diagnostic"
RUN, ATTEMPT, PRODUCER_JOB, ORIGINAL_SHELL_JOB = 37145136380, 1, 111271439057, 111277640608
ARTIFACT, ARCHIVE_BYTES = 11283098671, 432727276
ARCHIVE = "5e0277d288b9836c34d1116a58e218699c37e0670c58dacd2272a35f09346f96"
MANIFEST = "aab1f367734363cdeccaaafdf7be4f2cb6cfae11a418be88414aae6ecf93893d"
NATIVE = "4cfba03540f66805329ce9ac4c6133365374876f02741fc4ccd0fdfe789e6d34"
SHELL_ASSEMBLY = "164975f6f1c6323c7782cc4a0428493b6c6e058dc1725e333347f363e4325d7f"
APP_ASSEMBLY = "e367c91affe6c3008913144ea9277f24e9251a600056e2074e421cf541e69c8f"
BINDING_ASSEMBLY = "44ed973e7d4d9c4dcf517547870513af8b39eaaaa5f76685e60d118e143c8cce"
PATCH = "f014421cddf3858f606f74b1d6e297c79fd82ac840fdc3ba919685a0f4159082"
FACT = "SlateWindows.AccessibilityTests.ShellAccessibilityTests.GraphInspector_FiltersGroupsAndForces_AreClean"
TEST_FILE = "apps/slate-windows/tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.cs"
TEST_PROJECT = "tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj"
SHELL_BIN = "tests/SlateWindows.AccessibilityTests/bin/Release/net10.0-windows"
APP_BIN = "src/SlateWindows/bin/Release/net10.0-windows"
APPHOST_SOURCE = APP_BIN + "/SlateWindows.exe"
APPHOST_TARGET = "src/SlateWindows/obj/Release/net10.0-windows/apphost.exe"
APPHOST_BYTES = 163328
APPHOST_SHA = "daeb52233709a008ba2ed6697fa873a9bb8ff3079d19d92c16018d6fb82a45ca"
OUTPUT_ROOTS = ("src/SlateWindows/bin/Release", "tests/SlateWindows.Tests/bin/Release",
                "tests/SlateWindows.AccessibilityTests/bin/Release", "tools/GridConformanceHost/bin/Release")
REQUIRED = {
    APP_BIN + "/SlateWindows.exe", APP_BIN + "/slate_uniffi.dll",
    "tests/SlateWindows.Tests/bin/Release/net10.0-windows/SlateWindows.Tests.dll",
    "tests/SlateWindows.Tests/bin/Release/net10.0-windows/slate_uniffi.dll",
    SHELL_BIN + "/SlateWindows.AccessibilityTests.dll", SHELL_BIN + "/slate_uniffi.dll",
    "tools/GridConformanceHost/bin/Release/net10.0-windows/GridConformanceHost.exe",
}

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


def stage_single_apphost(root):
    """Stage one original apphost metadata input; never build or substitute application bytes."""
    require(root.is_absolute() and root.is_dir() and not root.is_symlink(), "Unsafe apphost root")
    manifest_path = root / "pilot-binaries-manifest.json"
    require(manifest_path.is_file() and not manifest_path.is_symlink() and sha(manifest_path) == MANIFEST,
            "Apphost requires the exact verified source5f manifest")
    manifest = read_json(manifest_path)
    require(all(manifest.get(k) == v for k, v in {
        "schemaVersion": 1, "revision": SOURCE, "runId": str(RUN), "producerAttempt": str(ATTEMPT),
        "candidate": "namespace-8x16", "relativeRoot": "apps/slate-windows", "nativeSha256": NATIVE}.items()),
        "Wrong apphost source provenance")
    entries = [entry for entry in manifest.get("files", []) if entry.get("path") == APPHOST_SOURCE]
    require(len(entries) == 1 and entries[0].get("bytes") == APPHOST_BYTES
            and entries[0].get("sha256") == APPHOST_SHA, "Wrong original source5f apphost entry")
    source, target = root / APPHOST_SOURCE, root / APPHOST_TARGET
    for path in (source, target):
        relative = path.relative_to(root)
        current = root
        for part in relative.parts:
            current /= part
            require(not current.is_symlink(), "Unsafe symbolic apphost source/target path")
        require(path.resolve().is_relative_to(root.resolve()), "Apphost path escapes its original checkout")
    require(source.is_file() and source.stat().st_size == APPHOST_BYTES and sha(source) == APPHOST_SHA,
            "Original source5f apphost missing or corrupt")
    reused = target.exists()
    if reused:
        require(target.is_file() and target.stat().st_size == APPHOST_BYTES and sha(target) == APPHOST_SHA,
                "Refuse a different existing apphost metadata input")
    else:
        target.parent.mkdir(parents=True, exist_ok=True)
        # Exclusive creation also refuses a target introduced after the checks above.
        with source.open("rb") as original, target.open("xb") as staged:
            shutil.copyfileobj(original, staged)
    require(target.is_file() and not target.is_symlink() and target.stat().st_size == APPHOST_BYTES
            and sha(target) == APPHOST_SHA, "Staged apphost metadata input failed verification")
    return {"sourcePath": APPHOST_SOURCE, "targetPath": APPHOST_TARGET, "bytes": APPHOST_BYTES,
            "sha256": APPHOST_SHA, "manifestSha256": MANIFEST, "revision": SOURCE, "sourceTree": TREE,
            "producerRunId": RUN, "producerAttempt": ATTEMPT, "producerJob": PRODUCER_JOB,
            "existingExactTargetReused": reused,
            "scope": "One fixed original apphost metadata input; no application or native rebuild"}


def verify_metadata(run, producer, shell, artifact, now):
    require(all(run.get(k) == v for k, v in {
        "id": RUN, "run_attempt": ATTEMPT, "head_sha": SOURCE,
        "status": "completed", "conclusion": "failure"}.items()), "Expected original failed full pipeline")
    require(all(producer.get(k) == v for k, v in {
        "id": PRODUCER_JOB, "run_id": RUN, "status": "completed", "conclusion": "success",
        "name": "Build, platform witnesses and app (namespace-8x16)"}.items()), "Original producer did not pass")
    for name in ("Build pinned native Windows host", "App tests", "Stage one Release build and hash every transferred file",
                 "Publish same-attempt binaries only after all producer gates pass"):
        steps = [s for s in producer.get("steps", []) if s.get("name") == name]
        require(len(steps) == 1 and steps[0].get("status") == "completed" and steps[0].get("conclusion") == "success",
                "Missing/failed original producer gate: " + name)
    require(all(shell.get(k) == v for k, v in {
        "id": ORIGINAL_SHELL_JOB, "run_id": RUN, "status": "completed", "conclusion": "failure",
        "name": "Complete hosted shell gate (namespace-8x16)"}.items()), "Wrong original shell failure")
    require(all(artifact.get(k) == v for k, v in {
        "id": ARTIFACT, "name": f"windows-pilot-binaries-{RUN}-{ATTEMPT}", "size_in_bytes": ARCHIVE_BYTES,
        "expired": False, "digest": "sha256:" + ARCHIVE}.items()), "Wrong failed-run binary artifact")
    origin = artifact.get("workflow_run", {})
    require(origin.get("id") == RUN and origin.get("head_sha") == SOURCE
            and origin.get("repository_id") == 1241111067, "Wrong archive repository/source")
    expires = dt.datetime.fromisoformat(artifact["expires_at"].replace("Z", "+00:00"))
    require(expires > now + dt.timedelta(hours=2), "Original artifact retention too short")


def verify_trx(path):
    doc = ET.parse(path).getroot()
    rows = [x for x in doc.iter() if x.tag.endswith("}UnitTestResult")]
    counters = [x.attrib for x in doc.iter() if x.tag.endswith("}Counters")]
    summaries = [x.attrib for x in doc.iter() if x.tag.endswith("}ResultSummary")]
    require(len(rows) == len(counters) == len(summaries) == 1, "Expected exactly one fact/counter/summary")
    row, c = rows[0], counters[0]
    require(row.attrib.get("testName") == FACT and row.attrib.get("outcome") in ("Passed", "Failed"),
            "Wrong, skipped or incomplete selected fact")
    require(c.get("total") == c.get("executed") == "1", "Incomplete single-fact execution")
    for key in ("error", "timeout", "aborted", "inconclusive", "passedButRunAborted", "notRunnable", "notExecuted",
                "disconnected", "warning", "completed", "inProgress", "pending"):
        require(c.get(key) == "0", "Missing/adverse TRX counter: " + key)
    passed = row.attrib["outcome"] == "Passed"
    require(c.get("passed") == ("1" if passed else "0") and c.get("failed") == ("0" if passed else "1"),
            "Single-fact outcome/counter disagreement")
    require(summaries[0].get("outcome") == ("Completed" if passed else "Failed"), "Incomplete test-run summary")
    return {"sha256": sha(path), "counters": c, "result": dict(row.attrib),
            "message": row.findtext(".//{*}Message"), "stack": row.findtext(".//{*}StackTrace")}


def verify_axe(folder, passed, harness_revision):
    paths = list(folder.glob("axe-*.json"))
    require(len(paths) <= 1 and all(p.name == "axe-graph-inspector.json" for p in paths), "Extra/wrong axe surface")
    if passed:
        require(len(paths) == 1, "Passed graph fact did not produce its required axe scan")
    values = []
    for path in paths:
        d = read_json(path)
        require(d.get("schemaVersion") == 2 and d.get("surface") == "graph-inspector"
                and d.get("sourceRevision") == harness_revision, "Wrong axe runtime revision/surface")
        require(d.get("userInteractive") is True and type(d.get("scannedWindowCount")) is int
                and d["scannedWindowCount"] > 0, "Axe was not an interactive scan")
        require(isinstance(d.get("errors"), list) and isinstance(d.get("waived"), list), "Missing axe arrays")
        if passed:
            require(d.get("outcome") == "pass" and d["errors"] == [] and d["waived"] == [], "Passed fact has adverse axe findings")
        else:
            require(d.get("outcome") in ("pass", "fail"), "Unclassified reached axe outcome")
        values.append({"sha256": sha(path), "report": d})
    return {"present": len(paths), "missingOnFailedFactPreserved": not passed and not paths, "reports": values,
            "scope": "This one-case runtime report is not the full135/58 reference gate"}


class GraphHarness:
    def __init__(self, source, evidence):
        self.source, self.evidence = source, evidence
        self.root = source / "apps/slate-windows"
        self.evidence.mkdir(parents=True, exist_ok=False)
        self.deadline = time.monotonic() + 14 * 60
        self.record = {"scope": "GRAPH-ONLY original source5f payload and diagnostic test overlay; not full reference acceptance",
            "status": "incomplete", "sourceRevision": SOURCE, "sourceTree": TREE,
            "producerRunId": RUN, "producerAttempt": ATTEMPT, "producerJob": PRODUCER_JOB,
            "originalPipelineConclusion": "failure", "originalShellJob": ORIGINAL_SHELL_JOB,
            "artifactId": ARTIFACT, "archiveSha256": ARCHIVE, "manifestSha256": MANIFEST, "nativeSha256": NATIVE,
            "executionRunId": os.environ.get("GITHUB_RUN_ID"), "executionAttempt": os.environ.get("GITHUB_RUN_ATTEMPT"),
            "harnessRevision": os.environ.get("GITHUB_SHA"), "commands": [], "states": [], "runs": [],
            "axeRevisionQualification": "WriteAxeEvidence reads GITHUB_SHA: raw sourceRevision annotates scratch harness runtime; original application/source and test overlay identities are separate."}
        self.natives = []
        self.app_hashes = {}

    def save(self):
        (self.evidence / "summary.json").write_text(json.dumps(self.record, indent=2) + "\n")

    def binaries_unchanged(self):
        require(self.natives and all(p.is_file() and sha(p) == NATIVE for p in self.natives), "Native bytes changed")
        require(sha(self.root / APP_BIN / "SlateWindows.dll") == APP_ASSEMBLY, "Application bytes changed")
        require(sha(self.root / SHELL_BIN / "SlateUniffi.dll") == BINDING_ASSEMBLY, "Original managed binding changed")
        require(self.app_hashes and {p.relative_to(self.root).as_posix(): sha(p)
            for p in (self.root / APP_BIN).rglob("*") if p.is_file()} == self.app_hashes, "Application output set/bytes changed")
        if "singleApphostMetadataSupport" in self.record:
            target = self.root / APPHOST_TARGET
            require(target.is_file() and not target.is_symlink() and target.stat().st_size == APPHOST_BYTES
                    and sha(target) == APPHOST_SHA, "Staged original apphost metadata bytes changed")

    def command(self, name, args, cwd=None, seconds=180, test=False, env=None):
        require(self.deadline - time.monotonic() > 1, "Diagnostic deadline exhausted")
        log = self.evidence / (name + ".log")
        started = time.monotonic()
        item = {"name": name, "argv": args, "cwd": str(cwd or self.source), "status": "running"}
        self.record["commands"].append(item)
        self.save()
        with log.open("w", encoding="utf-8") as stream:
            child = subprocess.Popen(args, cwd=cwd or self.source, stdout=stream, stderr=subprocess.STDOUT, env=env)
            try:
                code = child.wait(timeout=min(seconds, self.deadline - time.monotonic()))
            except BaseException:
                # This exact owned child only. No names/global/shared desktop process kills.
                item["status"] = "interrupted-or-timeout"
                self.save()
                subprocess.run(["taskkill", "/PID", str(child.pid), "/T", "/F"], stdout=stream,
                               stderr=subprocess.STDOUT, timeout=15, check=False)
                child.wait(timeout=10)
                raise
        item.update({"status": "completed", "exitCode": code, "elapsedSeconds": time.monotonic() - started, "logSha256": sha(log)})
        self.save()
        require(code == 0 or (test and code == 1), "Command failed: " + name)
        return code, log.read_text(encoding="utf-8-sig")

    def state(self, label):
        self.binaries_unchanged()
        target = self.evidence / label
        target.mkdir()
        hashes = {}
        for p in self.root.rglob("*"):
            if p.is_file() and ((p.suffix in (".cs", ".csproj", ".targets") and "bin" not in p.parts and "obj" not in p.parts)
                               or p.name in ("project.assets.json", "global.json")):
                hashes[p.relative_to(self.source).as_posix()] = sha(p)
        for folder in (self.root / APP_BIN, self.root / SHELL_BIN):
            for p in folder.rglob("*"):
                if p.is_file():
                    hashes[p.relative_to(self.source).as_posix()] = sha(p)
        for p in (self.source / TEST_FILE, self.root / TEST_PROJECT,
                  self.root / SHELL_BIN / "SlateWindows.AccessibilityTests.dll",
                  self.root / SHELL_BIN / "SlateWindows.AccessibilityTests.pdb"):
            require(p.is_file(), "Required source/project/assembly/PDB missing: " + str(p))
            shutil.copyfile(p, target / p.name)
        for p in self.root.rglob("project.assets.json"):
            out = target / p.relative_to(self.root)
            out.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(p, out)
        hp = target / "source-project-and-assembly-hashes.json"
        hp.write_text(json.dumps(hashes, indent=2) + "\n")
        self.record["states"].append({"label": label, "hashManifestSha256": sha(hp),
            "shellAssemblySha256": sha(self.root / SHELL_BIN / "SlateWindows.AccessibilityTests.dll"),
            "sourceFileSha256": sha(self.source / TEST_FILE), "nativeApplicationBindingUnchanged": True})
        self.save()

    def run_case(self, label):
        self.binaries_unchanged()
        folder = self.evidence / label
        folder.mkdir()
        output = folder / "shell-evidence"
        output.mkdir()
        environment = dict(os.environ, SLATE_REQUIRE_UI_AUTOMATION="1", SLATE_ACCESSIBILITY_EVIDENCE_DIR=str(output))
        # GITHUB_SHA is deliberately not overridden: raw axe annotation retains harness runtime identity.
        code, _ = self.command(label, ["dotnet", "test", str(self.root / SHELL_BIN / "SlateWindows.AccessibilityTests.dll"),
            "--filter", "FullyQualifiedName=" + FACT, "--logger", "trx;LogFileName=graph.trx", "--results-directory", str(folder),
            "--blame-hang-timeout", "5m", "--blame-hang-dump-type", "mini"], seconds=330, test=True, env=environment)
        value = verify_trx(folder / "graph.trx")
        passed = value["result"]["outcome"] == "Passed"
        require(code == (0 if passed else 1), "Process exit/TRX disagree")
        value.update({"label": label, "exitCode": code, "nativeApplicationBindingUnchanged": True,
            "shellAssemblySha256": sha(self.root / SHELL_BIN / "SlateWindows.AccessibilityTests.dll"),
            "compiledTestSourceSha256": sha(self.source / TEST_FILE),
            "axe": verify_axe(output, passed, self.record["harnessRevision"])})
        self.record["runs"].append(value)
        self.save()
        self.binaries_unchanged()
        return value

    def stage_binding(self):
        project = "src/SlateUniffi/SlateUniffi.csproj"
        _, value = self.command("original-binding-target-path", ["dotnet", "msbuild", project, "-nologo", "-t:GetTargetPath",
            "-getProperty:TargetPath,TargetRefPath", "-p:Configuration=Release", "-p:BuildProjectReferences=false"], cwd=self.root)
        properties = json.loads(value)["Properties"]
        original = self.root / SHELL_BIN / "SlateUniffi.dll"
        for key in ("TargetPath", "TargetRefPath"):
            if key == "TargetRefPath" and not properties.get(key):
                continue
            target = Path(properties[key])
            require(target.is_absolute() and target.suffix == ".dll" and target.resolve().is_relative_to(self.root.resolve()),
                    "Unexpected original binding target/ref path")
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(original, target)
            require(sha(target) == BINDING_ASSEMBLY, "Staged binding mismatch")
            self.record.setdefault("stagedBindingReferences", []).append({"property": key, "path": str(target), "sha256": sha(target)})
        generated = self.root / "src/SlateUniffi/generated/slate_uniffi.dll"
        generated.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(self.natives[0], generated)
        self.natives.append(generated)
        require(not (generated.parent / "slate_uniffi.cs").exists(), "Unexpected generated binding C# source")
        self.save()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    parser.add_argument("--patch", type=Path, required=True)
    args = parser.parse_args()
    h = GraphHarness(args.source.resolve(), args.evidence.resolve())
    original = (h.source / TEST_FILE).read_bytes()
    project_hash = sha(h.root / TEST_PROJECT)
    try:
        require(os.name == "nt" and os.environ.get("PROCESSOR_ARCHITECTURE") == "AMD64" and os.cpu_count() == 4
                and platform.win32_ver()[1] == "10.0.26100", "Original hosted Windows topology/build required")
        require(os.environ.get("GITHUB_REPOSITORY") == "coryj627/slate" and os.environ.get("GITHUB_EVENT_NAME") == "workflow_dispatch"
                and os.environ.get("GITHUB_REF") == BRANCH, "Wrong manual scratch execution")
        require(isinstance(h.record["harnessRevision"], str) and len(h.record["harnessRevision"]) == 40, "Missing harness revision")
        h.record["runner"] = {"logicalProcessors": os.cpu_count(), "windowsVersion": platform.win32_ver(),
            "imageOS": os.environ.get("ImageOS"), "imageVersion": os.environ.get("ImageVersion")}
        for name, expression, expected in (("source-revision", "HEAD", SOURCE), ("source-tree", "HEAD^{tree}", TREE)):
            _, value = h.command(name, ["git", "rev-parse", expression])
            require(value.strip() == expected, "Original source revision/tree mismatch")
        _, dirty = h.command("source-status", ["git", "status", "--porcelain"])
        require(not dirty.strip(), "Original checkout must be clean before payload/patch")
        _, sdk = h.command("sdk", ["dotnet", "--version"], cwd=h.root)
        require(sdk.strip() == "10.0.401", "Effective SDK changed")
        _, runtimes = h.command("runtimes", ["dotnet", "--list-runtimes"], cwd=h.root)
        for framework in ("Microsoft.NETCore.App", "Microsoft.WindowsDesktop.App"):
            versions = [tuple(map(int, line.split()[1].split("."))) for line in runtimes.splitlines()
                        if line.startswith(framework + " 10.0.")]
            require(versions and max(versions) == (10, 0, 12), "Effective runtime patch changed")
        require(os.environ.get("DOTNET_ROLL_FORWARD") in (None, "", "Minor"), "Unexpected roll-forward override")
        require(sha(args.patch) == PATCH, "Diagnostic patch bytes changed")
        metadata = []
        for route in (f"/actions/runs/{RUN}/attempts/{ATTEMPT}", f"/actions/jobs/{PRODUCER_JOB}",
                      f"/actions/jobs/{ORIGINAL_SHELL_JOB}", f"/actions/artifacts/{ARTIFACT}"):
            with github(route) as response:
                metadata.append(json.load(response))
        verify_metadata(*metadata, dt.datetime.now(dt.timezone.utc))
        (h.evidence / "original-failed-pipeline-and-successful-producer.json").write_text(json.dumps(metadata, indent=2) + "\n")
        archive = h.evidence.parent / (h.evidence.name + "-original-payload.zip")
        with github(f"/actions/artifacts/{ARTIFACT}/zip") as response, archive.open("wb") as output:
            size = 0
            while block := response.read(1024 * 1024):
                size += len(block)
                require(size <= ARCHIVE_BYTES and time.monotonic() < h.deadline, "Original archive size/deadline exceeded")
                output.write(block)
        require(size == ARCHIVE_BYTES and sha(archive) == ARCHIVE, "Original failed-producer archive changed")
        with zipfile.ZipFile(archive) as z:
            verify_archive_entries(z.infolist())
            z.extractall(h.root)
        manifest, h.natives = verify_payload(h.root)
        h.app_hashes = {entry["path"]: entry["sha256"] for entry in manifest["files"] if entry["path"].startswith(APP_BIN + "/")}
        require(sha(h.root / SHELL_BIN / "SlateWindows.AccessibilityTests.dll") == SHELL_ASSEMBLY, "Original shell assembly changed")
        h.binaries_unchanged()
        shutil.copyfile(h.root / "pilot-binaries-manifest.json", h.evidence / "pilot-binaries-manifest.json")
        h.record.update({"verifiedPayloadFiles": 449, "archiveBytesIndependentlyHashed": size,
            "patchSha256": PATCH, "harnessScriptSha256": sha(Path(__file__)), "buildMode": "original-project-only-no-fallback"})
        h.state("original-inputs")
        # An ordinary completed control failure is retained and does not erase the diagnostic run.
        h.run_case("original-control")
        h.command("restore-original-accessibility-project", ["dotnet", "restore", TEST_PROJECT], cwd=h.root, seconds=180)
        h.stage_binding()
        h.record["singleApphostMetadataSupport"] = stage_single_apphost(h.root)
        h.save()
        h.command("diagnostic-patch-check", ["git", "apply", "--check", str(args.patch.resolve())])
        h.command("diagnostic-patch-apply", ["git", "apply", str(args.patch.resolve())])
        h.command("build-only-original-accessibility-project", ["dotnet", "build", TEST_PROJECT,
            "--configuration", "Release", "--no-restore", "-p:BuildProjectReferences=false"], cwd=h.root, seconds=240)
        require(sha(h.root / TEST_PROJECT) == project_hash, "Original AccessibilityTests project changed")
        require(sha(h.root / SHELL_BIN / "SlateWindows.AccessibilityTests.dll") != SHELL_ASSEMBLY, "Instrumented shell DLL did not change")
        h.state("instrumented-inputs")
        h.run_case("instrumented-case")
        h.record["status"] = "completed-two-observations"
        h.record["interpretation"] = "Original and instrumented outcomes retained without retry; diagnostic-only, no causal repair/full-suite acceptance."
    except BaseException as error:
        h.record["status"] = "failed-or-incomplete"
        h.record["error"] = str(error)
        raise
    finally:
        (h.source / TEST_FILE).write_bytes(original)
        h.record["ownedSourceRestored"] = sha(h.source / TEST_FILE) == hashlib.sha256(original).hexdigest()
        h.record["originalProjectUnchanged"] = sha(h.root / TEST_PROJECT) == project_hash
        h.record["nativeApplicationBindingUnchanged"] = bool(h.natives) and all(p.is_file() and sha(p) == NATIVE for p in h.natives)
        if h.record["nativeApplicationBindingUnchanged"]:
            try:
                h.binaries_unchanged()
            except ValueError:
                h.record["nativeApplicationBindingUnchanged"] = False
        h.save()


if __name__ == "__main__":
    main()
