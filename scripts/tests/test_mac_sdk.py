# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Facts for scripts/check_mac_sdk.py, the #1460 SDK tripwire.

The canned otool output is cut from real binaries read on macOS 27.0.1 with
Xcode 27.1 (27A9275): the SwiftPM-built SlateMac (thin arm64, `sdk 15.0`) and
Xcode's xctest runner (universal, `sdk 27.0` in each slice). The facts need
no Mac: a fake otool stands in for the real one.
"""

from pathlib import Path
import contextlib
import io
import os
import plistlib
import stat
import subprocess
import sys
import tempfile
import textwrap
import unittest

SCRIPTS = Path(__file__).resolve().parent.parent
REPO = SCRIPTS.parent
sys.path.insert(0, str(SCRIPTS))

import check_mac_sdk as tripwire  # noqa: E402

# Load commands 10 to 14 of today's SlateMac, a segment with a section in front.
BODY = textwrap.dedent(
    """\
    Load command 1
          cmd LC_SEGMENT_64
      cmdsize 2152
      segname __TEXT
       vmaddr 0x0000000100000000
       nsects 26
        flags 0x0
    Section
      sectname __text
       segname __TEXT
          addr 0x0000000100001d18
    Load command 10
         cmd LC_UUID
     cmdsize 24
        uuid 4A358662-EF85-36A6-9240-FFA40B6695B3
    {version}Load command 12
          cmd LC_SOURCE_VERSION
      cmdsize 16
      version 0.0
    Load command 13
           cmd LC_MAIN
       cmdsize 24
      entryoff 15952700
     stacksize 0
    Load command 14
              cmd LC_LOAD_DYLIB
          cmdsize 128
             name @executable_path/../Frameworks/libslate_uniffi.dylib (offset 24)
       time stamp 2 Wed Dec 31 19:00:02 1969
          current version 0.0.0
    compatibility version 0.0.0
    """
)

BUILD_VERSION = textwrap.dedent(
    """\
    Load command 11
          cmd LC_BUILD_VERSION
      cmdsize 32
     platform {platform}
        minos 15.0
          sdk {sdk}
       ntools 1
         tool 3
      version 27037.1
    """
)

VERSION_MIN = textwrap.dedent(
    """\
    Load command 11
          cmd {command}
      cmdsize 16
      version 10.13
          sdk {sdk}
    """
)

PATH = "/work/apps/slate-mac/.build/debug/SlateMac"


def body(sdk="15.0", platform="1", version=None):
    """One slice's load commands; version=None gives LC_BUILD_VERSION."""
    if version is None:
        version = BUILD_VERSION.format(platform=platform, sdk=sdk)
    return BODY.format(version=version)


def thin(sdk="15.0", platform="1", version=None, path=PATH):
    return f"{path}:\n" + body(sdk, platform, version)


def universal(*slices, path=PATH):
    """slices: (arch, sdk) pairs; sdk None leaves the slice without a build version."""
    out = []
    for arch, sdk in slices:
        out.append(f"{path} (architecture {arch}):\n")
        out.append(body(version="") if sdk is None else body(sdk))
    return "".join(out)


def otool(stdout="", returncode=0, stderr=""):
    """A fake subprocess.run for otool that records what it was asked."""

    def run(argv, **kwargs):
        run.argv = argv
        return subprocess.CompletedProcess(argv, returncode, stdout, stderr)

    run.argv = None
    return run


class Judged:
    def __init__(self, status, out, err):
        self.status, self.out, self.err = status, out, err


def judge(output, returncode=0, stderr="", annotate=False):
    """Run check() on a real temporary file, with otool faked."""
    with tempfile.TemporaryDirectory() as tmp:
        executable = Path(tmp) / "SlateMac"
        executable.write_bytes(b"\xcf\xfa\xed\xfe")
        out, err = io.StringIO(), io.StringIO()
        status = tripwire.check(
            [str(executable)], run=otool(output, returncode, stderr), out=out, err=err, annotate=annotate
        )
    return Judged(status, out.getvalue(), err.getvalue())


class BelowTwentySevenPasses(unittest.TestCase):
    def test_todays_swiftpm_slatemac_at_sdk_15_passes(self):
        self.assertEqual([tripwire.Reading("", "LC_BUILD_VERSION", "15.0", 15)], tripwire.read_sdks(thin("15.0")))
        judged = judge(thin("15.0"))
        self.assertEqual(0, judged.status, judged.err)
        self.assertIn("records macOS SDK 15.0, below 27", judged.out)
        self.assertEqual("", judged.err)

    def test_sdk_26_passes(self):
        for sdk in ("26.0", "26.5", "26.99.1"):
            with self.subTest(sdk=sdk):
                judged = judge(thin(sdk))
                self.assertEqual(0, judged.status, judged.err)

    def test_a_universal_binary_below_27_in_every_slice_passes(self):
        judged = judge(universal(("x86_64", "15.0"), ("arm64", "26.0")))
        self.assertEqual(0, judged.status, judged.err)
        self.assertIn("15.0 (x86_64), 26.0 (arm64)", judged.out)

    def test_the_platform_may_be_printed_as_a_number_or_a_name(self):
        # Apple's otool prints 1; vtool prints MACOS; LLVM's objdump prints macos.
        for platform in ("1", "MACOS", "macos"):
            with self.subTest(platform=platform):
                self.assertEqual(15, tripwire.read_sdks(thin("15.0", platform=platform))[0].major)

    def test_the_older_version_min_command_is_read_too(self):
        version = VERSION_MIN.format(command="LC_VERSION_MIN_MACOSX", sdk="10.15")
        self.assertEqual(
            [tripwire.Reading("", "LC_VERSION_MIN_MACOSX", "10.15", 10)], tripwire.read_sdks(thin(version=version))
        )


class TwentySevenOrLaterFails(unittest.TestCase):
    def test_sdk_27_0_and_27_1_fail(self):
        for sdk in ("27.0", "27.1", "27", "28.0"):
            with self.subTest(sdk=sdk):
                judged = judge(thin(sdk))
                self.assertEqual(1, judged.status)
                self.assertIn(f"records macOS SDK {sdk}.", judged.err)

    def test_one_slice_at_27_fails_a_universal_binary(self):
        # Xcode's xctest runner records 27.0 in both slices; one is enough.
        judged = judge(universal(("x86_64", "15.0"), ("arm64", "27.0")))
        self.assertEqual(1, judged.status)
        self.assertIn("27.0 (arm64)", judged.err)

    def test_the_failure_names_the_issue_the_silent_loss_and_what_must_land_first(self):
        message = judge(thin("27.0")).err
        for needed in (
            "https://github.com/coryj627/slate/issues/1460",
            "VoiceOver users would lose reading-link activation silently",
            "reading-link elements the app owns, whose press calls ReadingLinkRouter",
            "a VoiceOver field pass on both builds",
        ):
            self.assertIn(needed, message)

    def test_on_github_actions_the_failure_is_also_an_annotation(self):
        judged = judge(thin("27.1"), annotate=True)
        self.assertEqual(1, judged.status)
        self.assertTrue(judged.out.startswith("::error title=SDK tripwire (#1460)::"), judged.out)
        self.assertIn("https://github.com/coryj627/slate/issues/1460", judged.out)
        self.assertNotIn("::error", judge(thin("27.1")).out)


class AnythingUnreadableFails(unittest.TestCase):
    def assertUnreadable(self, judged, reason):
        self.assertEqual(1, judged.status)
        self.assertIn("cannot read the SDK", judged.err)
        self.assertIn(reason, judged.err)
        self.assertIn("fails closed", judged.err)
        self.assertIn("https://github.com/coryj627/slate/issues/1460", judged.err)

    def test_empty_output_fails(self):
        self.assertUnreadable(judge(""), "otool named no file")

    def test_a_file_otool_cannot_read_fails_although_otool_exits_0(self):
        # Measured: otool prints this for a text file and exits 0.
        self.assertUnreadable(judge(f"{PATH}: is not an object file\n"), "otool named no file")

    def test_a_binary_without_a_build_version_fails(self):
        self.assertUnreadable(judge(thin(version="")), "no LC_BUILD_VERSION or LC_VERSION_MIN_MACOSX")

    def test_a_universal_slice_without_a_build_version_fails(self):
        self.assertUnreadable(
            judge(universal(("arm64", "15.0"), ("x86_64", None))),
            "the x86_64 slice has no LC_BUILD_VERSION",
        )

    def test_an_sdk_that_does_not_parse_fails(self):
        # otool prints n/a for an SDK of 0.
        for sdk in ("n/a", "27.x", "fifteen", "15.0.0.1"):
            with self.subTest(sdk=sdk):
                self.assertUnreadable(judge(thin(sdk)), "not a version")

    def test_a_build_version_without_its_sdk_line_fails(self):
        version = BUILD_VERSION.format(platform="1", sdk="15.0").replace("      sdk 15.0\n", "")
        self.assertNotIn("sdk", version)
        self.assertUnreadable(judge(thin(version=version)), "LC_BUILD_VERSION has no sdk line")

    def test_a_build_version_for_another_platform_fails(self):
        for platform in ("6", "ios", "MACCATALYST"):
            with self.subTest(platform=platform):
                self.assertUnreadable(judge(thin(platform=platform)), f"platform {platform}, not macOS")

    def test_a_version_min_command_for_another_platform_fails(self):
        version = VERSION_MIN.format(command="LC_VERSION_MIN_IPHONEOS", sdk="15.0")
        self.assertUnreadable(judge(thin(version=version)), "LC_VERSION_MIN_IPHONEOS, not a macOS version")

    def test_a_load_command_before_any_file_header_fails(self):
        self.assertUnreadable(judge(body("15.0")), "before naming the file")

    def test_otool_failing_fails(self):
        judged = judge("", returncode=1, stderr="error: otool-classic: can't open file: SlateMac\n")
        self.assertUnreadable(judged, "otool exited 1: error: otool-classic: can't open file")

    def test_otool_missing_fails(self):
        def missing(argv, **kwargs):
            raise FileNotFoundError(2, "No such file or directory", "otool")

        with tempfile.TemporaryDirectory() as tmp:
            executable = Path(tmp) / "SlateMac"
            executable.write_bytes(b"")
            err = io.StringIO()
            status = tripwire.check([str(executable)], run=missing, out=io.StringIO(), err=err, annotate=False)
        self.assertEqual(1, status)
        self.assertIn("otool could not run", err.getvalue())

    def test_a_missing_executable_fails_without_running_otool(self):
        run = otool(thin("15.0"))
        err = io.StringIO()
        status = tripwire.check(["/no/such/SlateMac"], run=run, out=io.StringIO(), err=err, annotate=False)
        self.assertEqual(1, status)
        self.assertIn("/no/such/SlateMac does not exist", err.getvalue())
        self.assertIsNone(run.argv)

    def test_no_path_at_all_is_a_usage_error(self):
        err = io.StringIO()
        with contextlib.redirect_stderr(err):
            self.assertEqual(2, tripwire.main([]))
        self.assertIn("usage: check_mac_sdk.py PATH", err.getvalue())


class BundlesAreJudgedByTheirExecutable(unittest.TestCase):
    def make_bundle(self, root, info):
        bundle = Path(root) / "SlateMac.app"
        (bundle / "Contents" / "MacOS").mkdir(parents=True)
        (bundle / "Contents" / "MacOS" / "SlateMac").write_bytes(b"")
        if info is not None:
            with (bundle / "Contents" / "Info.plist").open("wb") as handle:
                plistlib.dump(info, handle)
        return bundle

    def test_the_executable_the_info_plist_names_is_read(self):
        with tempfile.TemporaryDirectory() as tmp:
            bundle = self.make_bundle(tmp, {"CFBundleExecutable": "SlateMac"})
            run = otool(thin("15.0"))
            status = tripwire.check([str(bundle)], run=run, out=io.StringIO(), err=io.StringIO(), annotate=False)
            self.assertEqual(0, status)
            self.assertEqual(["otool", "-arch", "all", "-l", str(bundle / "Contents" / "MacOS" / "SlateMac")], run.argv)

    def test_a_bundle_without_a_readable_executable_name_fails(self):
        names = (None, {}, {"CFBundleExecutable": ""}, {"CFBundleExecutable": "../SlateMac"}, {"CFBundleExecutable": "Other"})
        for info in names:
            with self.subTest(info=info), tempfile.TemporaryDirectory() as tmp:
                bundle = self.make_bundle(tmp, info)
                err = io.StringIO()
                status = tripwire.check(
                    [str(bundle)], run=otool(thin("15.0")), out=io.StringIO(), err=err, annotate=False
                )
                self.assertEqual(1, status)
                self.assertIn("cannot read the SDK", err.getvalue())


class TheScriptRunsTheRealOtoolCommand(unittest.TestCase):
    """End to end through a fake otool on PATH: argv, exit status, output."""

    def run_script(self, canned, *, exit_status=0):
        with tempfile.TemporaryDirectory() as tmp:
            tmp = Path(tmp)
            (tmp / "canned.txt").write_text(canned, encoding="utf-8")
            fake = tmp / "otool"
            fake.write_text(
                "#!/bin/sh\n"
                f'printf "%s\\n" "$@" > "{tmp / "argv.txt"}"\n'
                f'cat "{tmp / "canned.txt"}"\n'
                f"exit {exit_status}\n",
                encoding="utf-8",
            )
            fake.chmod(fake.stat().st_mode | stat.S_IXUSR)
            executable = tmp / "SlateMac"
            executable.write_bytes(b"")
            env = dict(os.environ, PATH=f"{tmp}{os.pathsep}{os.environ.get('PATH', '')}")
            env.pop("GITHUB_ACTIONS", None)
            result = subprocess.run(
                [sys.executable, "-I", str(SCRIPTS / "check_mac_sdk.py"), str(executable)],
                capture_output=True,
                text=True,
                env=env,
            )
            argv = (tmp / "argv.txt").read_text(encoding="utf-8").splitlines()
        return result, argv, str(executable)

    def test_sdk_15_exits_0_and_sdk_27_exits_1(self):
        result, argv, executable = self.run_script(thin("15.0"))
        self.assertEqual(["-arch", "all", "-l", executable], argv)
        self.assertEqual(0, result.returncode, result.stderr)
        result, _, _ = self.run_script(thin("27.0"))
        self.assertEqual(1, result.returncode)
        self.assertIn("VoiceOver users would lose reading-link activation silently", result.stderr)

    def test_otools_own_failure_exits_1(self):
        result, _, _ = self.run_script("", exit_status=1)
        self.assertEqual(1, result.returncode)
        self.assertIn("otool exited 1", result.stderr)


class TheTripwireIsWired(unittest.TestCase):
    """The gate stays where it bites: after swift build, and in the mac lane."""

    def test_the_build_script_checks_the_binary_after_swift_build_and_the_bundle_after_signing(self):
        lines = [line.strip() for line in (REPO / "scripts" / "build-mac-app.sh").read_text(encoding="utf-8").splitlines()]

        def index(text, start=0):
            for i in range(start, len(lines)):
                if lines[i] == text:
                    return i
            self.fail(f"build-mac-app.sh has no line {text!r} after line {start + 1}")

        build = index("swift build $SWIFT_CONFIG_FLAG")
        binary = index('python3 -I "$WORKSPACE_ROOT/scripts/check_mac_sdk.py" "$ABS_BINARY"', build)
        bundle_block = index('if [[ "$BUNDLE" == "1" ]]; then', binary)
        signed = index('codesign --force --sign - "$APP_BUNDLE"', bundle_block)
        bundle = index('python3 -I "$WORKSPACE_ROOT/scripts/check_mac_sdk.py" "$APP_BUNDLE"', signed)
        register = index('"$LSREGISTER" -f "$APP_BUNDLE" >/dev/null 2>&1 || true', bundle_block)
        self.assertLess(bundle, register, "the bundle is checked before LaunchServices registers it")

    def test_the_mac_lane_builds_through_the_script_and_starts_when_the_check_changes(self):
        workflow = (REPO / ".github" / "workflows" / "swift-tests.yml").read_text(encoding="utf-8")
        self.assertIn("run: ./scripts/build-mac-app.sh --skip-a11y-check\n", workflow)
        for path in ("scripts/check_mac_sdk.py", "scripts/tests/test_mac_sdk.py"):
            # Once under on.pull_request.paths, once under on.push.paths.
            self.assertEqual(2, workflow.count(f"      - '{path}'\n"), path)
        self.assertIn("python3 -I -m unittest discover -v -s scripts/tests -p test_mac_sdk.py", workflow)


if __name__ == "__main__":
    unittest.main()
