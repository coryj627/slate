#!/usr/bin/env python3
"""PRIVATE proposal. Two existing Windows facts only; not full acceptance."""
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
        self.deadline = time.monotonic() + 14 * 60
        self.record = {"scope": "PRIVATE two-fact citation diagnostic; no full-suite acceptance",
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


def substitute_project(original, references):
    """Explicit fallback only. Preserve packages/settings; pin reference bytes separately."""
    path = original.with_name(original.stem + ".diagnostic.csproj")
    doc = ET.parse(original)
    require(doc.getroot().tag == "Project", "Unexpected project XML namespace")
    removed = []
    for group in doc.getroot().findall("ItemGroup"):
        for item in list(group):
            if item.tag == "ProjectReference":
                removed.append(item.attrib)
                group.remove(item)
    require(removed, "No project references to substitute")
    properties = ET.SubElement(doc.getroot(), "PropertyGroup")
    ET.SubElement(properties, "AssemblyName").text = original.stem
    # Match the original SDK default if not already explicitly declared.
    if doc.getroot().find(".//RootNamespace") is None:
        ET.SubElement(properties, "RootNamespace").text = original.stem
    group = ET.SubElement(doc.getroot(), "ItemGroup")
    for name, target in references.items():
        item = ET.SubElement(group, "Reference", {"Include": name})
        ET.SubElement(item, "HintPath").text = str(target)
        ET.SubElement(item, "Private").text = "true"
    doc.write(path, encoding="utf-8", xml_declaration=True)
    return path, removed


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    parser.add_argument("--patch", type=Path, required=True)
    parser.add_argument("--build-mode", choices=("staged-projects", "exact-dll-references"), default="staged-projects")
    args = parser.parse_args()
    h = Harness(args.source.resolve(), args.evidence.resolve())
    original_test = (h.source / TEST_FILE).read_bytes()
    original_app = (h.source / APP_FILE).read_bytes()
    try:
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
        # Never create generated C# bindings; their absence makes accidental core compilation fail.
        generated_native = h.root / "src/SlateUniffi/generated/slate_uniffi.dll"
        generated_native.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(h.natives[0], generated_native)
        h.natives.append(generated_native)
        test_project, app_project = Path(TEST_PROJECT), Path(APP_PROJECT)
        if args.build_mode == "exact-dll-references":
            app_project, app_removed = substitute_project(h.root / APP_PROJECT, {"SlateUniffi": h.root / TEST_BIN / "SlateUniffi.dll"})
            test_project, test_removed = substitute_project(h.root / TEST_PROJECT, {
                "SlateUniffi": h.root / TEST_BIN / "SlateUniffi.dll", "SlateWindows": h.root / APP_BIN / "SlateWindows.dll",
                "ParityHarness": h.root / TEST_BIN / "ParityHarness.dll"})
            h.record["explicitSubstitutedProjects"] = {"appRemoved": app_removed, "testRemoved": test_removed,
                "appSha256": sha(app_project), "testSha256": sha(test_project)}
            shutil.copyfile(app_project, h.evidence / app_project.name)
            shutil.copyfile(test_project, h.evidence / test_project.name)
            h.command("restore-explicit-test-project", ["dotnet", "restore", str(test_project)], cwd=h.root, max_seconds=180)
            h.command("restore-explicit-app-project", ["dotnet", "restore", str(app_project)], cwd=h.root, max_seconds=180)
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
        h.record["status"] = "qualified-focused-diagnostic"
        h.record["interpretation"] = "Candidate five pairs passed and targeted mutant killed; control outcomes retained. Not a causal repair or full-suite certificate."
    except BaseException as error:
        h.record["status"] = "failed-or-incomplete"
        h.record["error"] = str(error)
        raise
    finally:
        # Preserve mutant evidence before restoring only the two owned source bytes.
        (h.source / TEST_FILE).write_bytes(original_test)
        (h.source / APP_FILE).write_bytes(original_app)
        h.record["ownedSourcesRestored"] = sha(h.source / TEST_FILE) == hashlib.sha256(original_test).hexdigest() and sha(h.source / APP_FILE) == hashlib.sha256(original_app).hexdigest()
        h.record["nativeBytesStillOriginal"] = bool(h.natives) and all(p.is_file() and sha(p) == NATIVE for p in h.natives)
        h.save()


if __name__ == "__main__":
    main()
