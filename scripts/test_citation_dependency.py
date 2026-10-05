"""Rejection controls for dependency admission and shipping-graph reachability."""

import copy
import unittest

from verify_citation_dependency import registry_audit_lock, verify


REV = "06a591e2f237d25e1dfdedac3f3d1494c496c52d"
SOURCE = f"git+https://github.com/typst/citationberg?rev={REV}#{REV}"
REGISTRY = "registry+https://github.com/rust-lang/crates.io-index"
CHECKSUM = "e660451e55124f798a69a5af3f49ccfbefbd41910eefd25caf2393e1f3473ec1"


def fixture():
    manifest = {"patch": {"crates-io": {"citationberg": {
        "git": "https://github.com/typst/citationberg", "rev": REV}}}}
    packages = [
        {"id": "native", "name": "slate-uniffi", "version": "0.1.0", "source": None},
        {"id": "cli", "name": "slate-cli", "version": "0.1.0", "source": None},
        {"id": "core", "name": "slate-core", "version": "0.1.0", "source": None},
        {"id": "citation", "name": "citationberg", "version": "0.7.0", "source": SOURCE},
        {"id": "xml", "name": "quick-xml", "version": "0.41.0", "source": REGISTRY},
    ]
    lock = {"package": copy.deepcopy(packages[3:])}
    lock["package"][1]["checksum"] = CHECKSUM
    edges = {"native": ["core"], "cli": ["core"], "core": ["citation"],
             "citation": ["xml"], "xml": []}
    metadata = {"version": 1, "packages": packages, "workspace_members": ["native", "cli", "core"],
                "resolve": {"nodes": [{"id": name, "deps": [
                    {"pkg": dep, "dep_kinds": [{"kind": None, "target": None}]}
                    for dep in deps]} for name, deps in edges.items()]}}
    return manifest, lock, metadata


class DependencyPolicyTests(unittest.TestCase):
    def setUp(self):
        self.manifest, self.lock, self.metadata = fixture()

    def check(self):
        return verify(self.manifest, self.lock, self.metadata)

    def test_accepts_approved_pin_on_both_shipping_paths(self):
        paths = self.check()["applicationPaths"]
        self.assertEqual(paths["slate-uniffi"]["quick-xml"], ["native", "core", "citation", "xml"])
        self.assertEqual(paths["slate-cli"]["quick-xml"], ["cli", "core", "citation", "xml"])

    def test_rejects_floating_manifest_reference(self):
        self.manifest["patch"]["crates-io"]["citationberg"]["rev"] = "main"
        with self.assertRaisesRegex(ValueError, "manifest pin"):
            self.check()

    def test_rejects_additional_manifest_branch_selector(self):
        self.manifest["patch"]["crates-io"]["citationberg"]["branch"] = "main"
        with self.assertRaisesRegex(ValueError, "manifest pin"):
            self.check()

    def test_rejects_different_locked_git_revision(self):
        self.lock["package"][0]["source"] = SOURCE.replace(REV, "0" * 40)
        with self.assertRaisesRegex(ValueError, "lock: unapproved Git"):
            self.check()

    def test_rejects_different_resolved_git_source(self):
        self.metadata["packages"][3]["source"] = SOURCE.replace("typst/", "another-owner/")
        with self.assertRaisesRegex(ValueError, "resolved graph: unapproved Git"):
            self.check()

    def test_rejects_an_additional_git_dependency(self):
        self.metadata["packages"].append({"id": "extra", "name": "extra", "version": "1.0",
                                          "source": "git+https://example.com/extra#abc"})
        with self.assertRaisesRegex(ValueError, "resolved graph: unapproved Git"):
            self.check()

    def test_rejects_registry_checksum_drift(self):
        self.lock["package"][1]["checksum"] = "0" * 64
        with self.assertRaisesRegex(ValueError, "checksum changed"):
            self.check()

    def test_rejects_vulnerable_resolved_parser(self):
        self.metadata["packages"][4]["version"] = "0.38.4"
        with self.assertRaisesRegex(ValueError, "unapproved quick-xml"):
            self.check()

    def test_rejects_a_second_parser_version(self):
        other = dict(self.metadata["packages"][4], id="old-xml", version="0.38.4")
        self.metadata["packages"].append(other)
        with self.assertRaisesRegex(ValueError, "exactly one quick-xml"):
            self.check()

    def test_rejects_dev_only_native_reachability(self):
        self.metadata["resolve"]["nodes"][0]["deps"][0]["dep_kinds"][0]["kind"] = "dev"
        with self.assertRaisesRegex(ValueError, "no normal dependency path from native"):
            self.check()

    def test_rejects_build_only_cli_reachability(self):
        self.metadata["resolve"]["nodes"][1]["deps"][0]["dep_kinds"][0]["kind"] = "build"
        with self.assertRaisesRegex(ValueError, "no normal dependency path from cli"):
            self.check()

    def test_rejects_disconnected_citation_parser_edge(self):
        self.metadata["resolve"]["nodes"][3]["deps"] = []
        with self.assertRaisesRegex(ValueError, "no normal dependency path"):
            self.check()

    def test_rejects_missing_resolved_node(self):
        self.metadata["resolve"]["nodes"].pop()
        with self.assertRaisesRegex(ValueError, "incomplete or duplicate"):
            self.check()

    def test_rejects_duplicate_resolved_package_identity(self):
        self.metadata["packages"].append(copy.deepcopy(self.metadata["packages"][2]))
        with self.assertRaisesRegex(ValueError, "duplicate resolved package"):
            self.check()

    def test_rejects_external_application_substitution(self):
        self.metadata["workspace_members"].remove("native")
        with self.assertRaisesRegex(ValueError, "not the workspace application"):
            self.check()


LOCK = f'''[[package]]
name = "citationberg"
version = "0.7.0"
source = "{SOURCE}"
dependencies = [
 "quick-xml",
]

[[package]]
name = "quick-xml"
version = "0.41.0"
source = "{REGISTRY}"
checksum = "{CHECKSUM}"
'''


class RegistryAuditLockTests(unittest.TestCase):
    """cargo-audit skips Git sources, so the gate audits citationberg's published identity."""

    def test_names_only_the_admitted_revision_by_its_registry_source(self):
        audited = registry_audit_lock(LOCK)
        self.assertNotIn("git+", audited)
        self.assertEqual(audited.count(f'source = "{REGISTRY}"'), 2)
        self.assertEqual(audited, LOCK.replace(SOURCE, REGISTRY))

    def test_rejects_a_lock_without_the_admitted_revision(self):
        with self.assertRaisesRegex(ValueError, "admitted Git source exactly once"):
            registry_audit_lock(LOCK.replace(REV, "0" * 40))

    def test_rejects_a_lock_naming_the_admitted_revision_twice(self):
        with self.assertRaisesRegex(ValueError, "admitted Git source exactly once"):
            registry_audit_lock(LOCK + LOCK)


if __name__ == "__main__":
    unittest.main()
