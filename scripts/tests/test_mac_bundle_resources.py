# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Facts for scripts/check_mac_bundle_resources.py, the #1589 resource tripwire.

The fixtures mirror what `swift build` leaves in apps/slate-mac/.build/debug
with the swift-build backend (Swift 6.4, macOS 27.0): `debug` is a symlink to
`out/Products/Debug`, which holds the SlateMac executable beside one
`<Package>_<Target>.bundle` folder per package with resources. The facts need
no Mac: they build those folders in a temporary directory.
"""

from pathlib import Path
import contextlib
import io
import os
import subprocess
import sys
import tempfile
import unittest

SCRIPTS = Path(__file__).resolve().parent.parent
REPO = SCRIPTS.parent
sys.path.insert(0, str(SCRIPTS))

import check_mac_bundle_resources as tripwire  # noqa: E402

# The two resource bundles SlateMac's packages build today, with a file each
# of the kinds they carry (MathJaxSwift's `mjn` tree, the privacy manifests).
TODAY = {
    "MathJaxSwift_MathJaxSwift.bundle": ["Contents/Info.plist", "Contents/Resources/mjn/es5/tex-mml-svg.js", "Contents/Resources/PrivacyInfo.xcprivacy"],
    "LaTeXSwiftUI_LaTeXSwiftUI.bundle": ["Contents/Info.plist", "Contents/Resources/PrivacyInfo.xcprivacy"],
}


def _write(root, relative, text="x"):
    path = root / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


class Tree:
    """A products directory reached through a `debug` symlink, and an app bundle."""

    def __init__(self, tmp):
        self.root = Path(tmp)
        real = self.root / "out" / "Products" / "Debug"
        real.mkdir(parents=True)
        (self.root / "debug").symlink_to(real, target_is_directory=True)
        self.products = self.root / "debug"
        _write(real, "SlateMac", "binary")
        self.app = real / "SlateMac.app"
        self.resources = self.app / "Contents" / "Resources"
        self.resources.mkdir(parents=True)
        _write(self.resources, "AppIcon.icns", "icon")

    def build(self, bundles=TODAY):
        for name, files in bundles.items():
            for relative in files:
                _write(self.products / name, relative)

    def stage(self, bundles=TODAY):
        for name, files in bundles.items():
            for relative in files:
                _write(self.resources / name, relative)


class Judged:
    def __init__(self, status, out, err):
        self.status, self.out, self.err = status, out, err


def judge(tree, products=None, app=None):
    out, err = io.StringIO(), io.StringIO()
    status = tripwire.check(str(products or tree.products), str(app or tree.app), out=out, err=err, annotate=False)
    return Judged(status, out.getvalue(), err.getvalue())


class ABundleCarryingEveryResourceBundlePasses(unittest.TestCase):
    def test_todays_two_bundles_staged_in_full_pass(self):
        with tempfile.TemporaryDirectory() as tmp:
            tree = Tree(tmp)
            tree.build()
            tree.stage()
            result = judge(tree)
        self.assertEqual(0, result.status, result.err)
        self.assertIn("LaTeXSwiftUI_LaTeXSwiftUI.bundle", result.out)
        self.assertIn("MathJaxSwift_MathJaxSwift.bundle", result.out)
        self.assertEqual("", result.err)

    def test_resources_the_app_has_besides_them_are_not_judged(self):
        with tempfile.TemporaryDirectory() as tmp:
            tree = Tree(tmp)
            tree.build()
            tree.stage()
            tree.stage({"Older_Leftover.bundle": ["Contents/Info.plist"]})
            self.assertEqual(0, judge(tree).status)

    def test_the_products_directory_may_be_given_through_its_symlink_or_directly(self):
        with tempfile.TemporaryDirectory() as tmp:
            tree = Tree(tmp)
            tree.build()
            tree.stage()
            self.assertEqual(0, judge(tree, products=tree.products).status)
            self.assertEqual(0, judge(tree, products=tree.products.resolve()).status)


class AMissingOrPartialCopyFails(unittest.TestCase):
    def test_a_bundle_with_none_of_them_fails_and_names_both(self):
        # The #1589 crash: build-mac-app.sh copied neither, and the first
        # math render died in MathJaxSwift's Bundle.module accessor.
        with tempfile.TemporaryDirectory() as tmp:
            tree = Tree(tmp)
            tree.build()
            result = judge(tree)
        self.assertEqual(1, result.status)
        self.assertIn("MathJaxSwift_MathJaxSwift.bundle", result.err)
        self.assertIn("LaTeXSwiftUI_LaTeXSwiftUI.bundle", result.err)
        self.assertIn("unable to find bundle named", result.err)
        self.assertIn("#1589", result.err)

    def test_one_missing_bundle_fails_and_names_only_it(self):
        with tempfile.TemporaryDirectory() as tmp:
            tree = Tree(tmp)
            tree.build()
            tree.stage({"LaTeXSwiftUI_LaTeXSwiftUI.bundle": TODAY["LaTeXSwiftUI_LaTeXSwiftUI.bundle"]})
            result = judge(tree)
        self.assertEqual(1, result.status)
        self.assertIn("MathJaxSwift_MathJaxSwift.bundle", result.err)
        self.assertNotIn("LaTeXSwiftUI_LaTeXSwiftUI.bundle is", result.err)

    def test_a_copy_missing_a_file_fails_and_names_the_file(self):
        with tempfile.TemporaryDirectory() as tmp:
            tree = Tree(tmp)
            tree.build()
            partial = dict(TODAY)
            partial["MathJaxSwift_MathJaxSwift.bundle"] = ["Contents/Info.plist", "Contents/Resources/PrivacyInfo.xcprivacy"]
            tree.stage(partial)
            result = judge(tree)
        self.assertEqual(1, result.status)
        self.assertIn("Contents/Resources/mjn/es5/tex-mml-svg.js", result.err)

    def test_a_file_in_place_of_the_bundle_folder_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            tree = Tree(tmp)
            tree.build()
            tree.stage({"LaTeXSwiftUI_LaTeXSwiftUI.bundle": TODAY["LaTeXSwiftUI_LaTeXSwiftUI.bundle"]})
            _write(tree.resources, "MathJaxSwift_MathJaxSwift.bundle", "not a folder")
            self.assertEqual(1, judge(tree).status)


class WhatCannotBeReadFailsClosed(unittest.TestCase):
    def test_a_products_directory_without_resource_bundles_fails(self):
        # SlateMac links LaTeXSwiftUI, which always builds resources. Finding
        # none means the products moved, and the check would otherwise pass
        # every bundle: it fails instead of switching itself off.
        with tempfile.TemporaryDirectory() as tmp:
            tree = Tree(tmp)
            result = judge(tree)
        self.assertEqual(1, result.status)
        self.assertIn("no *.bundle", result.err)

    def test_a_products_directory_that_does_not_exist_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            tree = Tree(tmp)
            tree.build()
            tree.stage()
            self.assertEqual(1, judge(tree, products=tree.root / "nowhere").status)

    def test_an_app_without_contents_resources_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            tree = Tree(tmp)
            tree.build()
            bare = tree.root / "Bare.app"
            (bare / "Contents" / "MacOS").mkdir(parents=True)
            result = judge(tree, app=bare)
        self.assertEqual(1, result.status)
        self.assertIn("Contents/Resources", result.err)


class TheCommandLine(unittest.TestCase):
    def run_script(self, *args):
        return subprocess.run(
            [sys.executable, "-I", str(SCRIPTS / "check_mac_bundle_resources.py"), *args],
            capture_output=True,
            text=True,
            env={key: value for key, value in os.environ.items() if key != "GITHUB_ACTIONS"},
        )

    def test_no_arguments_or_one_exits_2(self):
        self.assertEqual(2, self.run_script().returncode)
        self.assertEqual(2, self.run_script("only-one").returncode)

    def test_a_good_bundle_exits_0_and_a_bare_one_exits_1(self):
        with tempfile.TemporaryDirectory() as tmp:
            tree = Tree(tmp)
            tree.build()
            self.assertEqual(1, self.run_script(str(tree.products), str(tree.app)).returncode)
            tree.stage()
            self.assertEqual(0, self.run_script(str(tree.products), str(tree.app)).returncode)

    def test_github_actions_gets_an_error_annotation(self):
        with tempfile.TemporaryDirectory() as tmp:
            tree = Tree(tmp)
            tree.build()
            out = io.StringIO()
            with contextlib.redirect_stderr(io.StringIO()):
                status = tripwire.check(str(tree.products), str(tree.app), out=out, annotate=True)
        self.assertEqual(1, status)
        self.assertIn("::error title=Resource bundle tripwire (#1589)::", out.getvalue())


class TheTripwireIsWired(unittest.TestCase):
    """The copy and the check sit in the --bundle branch, before the bundle is signed."""

    def test_the_build_script_copies_then_checks_the_resource_bundles_before_signing(self):
        lines = [line.strip() for line in (REPO / "scripts" / "build-mac-app.sh").read_text(encoding="utf-8").splitlines()]

        def index(text, start=0):
            for i in range(start, len(lines)):
                if lines[i] == text:
                    return i
            self.fail(f"build-mac-app.sh has no line {text!r} after line {start + 1}")

        bundle_block = index('if [[ "$BUNDLE" == "1" ]]; then')
        made = index('rm -rf "$APP_BUNDLE"', bundle_block)
        copied = index('cp -R "$bundle" "$APP_BUNDLE/Contents/Resources/"', made)
        checked = index(
            'python3 -I "$WORKSPACE_ROOT/scripts/check_mac_bundle_resources.py" "$PRODUCTS_DIR" "$APP_BUNDLE"', copied
        )
        signed = index('codesign --force --sign - "$APP_BUNDLE"', checked)
        self.assertLess(checked, signed, "the resource bundles are in place before the bundle is signed")

    def test_the_facts_run_on_hosted_linux_and_the_lane_starts_when_they_change(self):
        workflow = (REPO / ".github" / "workflows" / "swift-tests.yml").read_text(encoding="utf-8")
        for path in ("scripts/check_mac_bundle_resources.py", "scripts/tests/test_mac_bundle_resources.py"):
            # Once, under on.push.paths: lane-changes.yml decides pull requests from the same list (#1565).
            self.assertEqual(1, workflow.count(f"      - '{path}'\n"), path)
        self.assertIn("python3 -I -m unittest discover -v -s scripts/tests -p test_mac_bundle_resources.py", workflow)


if __name__ == "__main__":
    unittest.main()
