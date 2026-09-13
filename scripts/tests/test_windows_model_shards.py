# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


SPEC = importlib.util.spec_from_file_location(
    "model_shards", Path(__file__).resolve().parents[1] / "verify_windows_model_shards.py"
)
verifier = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(verifier)


class ModelShardVerificationTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.reports = []
        for family, (total, excluded, reachable) in verifier.CENSUSES.items():
            for index in range(2):
                selected = list(range(index + 1, reachable + 1, 2))
                self.reports.append({
                    "schemaVersion": 1, "family": family,
                    "shardIndex": index, "shardCount": 2,
                    "totalCells": total, "unreachableCells": excluded,
                    "reachableCells": reachable, "inventorySha256": "a" * 64,
                    "selectedOrdinals": selected, "completedOrdinals": selected.copy(),
                    "success": True, "elapsedMilliseconds": 1234.5,
                    "routes": [{"route": "sample", "cases": len(selected),
                                "elapsedMilliseconds": 1200,
                                "phases": {"fixtureSetup": 250, "drive": 950}}],
                })

    def verify(self):
        paths = []
        for index, report in enumerate(self.reports):
            path = self.root / f"report-{index}.json"
            path.write_text(json.dumps(report), encoding="utf-8")
            paths.append(path)
        return verifier.verify(paths, shard_count=2)

    def test_complete_partitions_pass_and_account_for_every_case(self):
        summary = self.verify()
        self.assertEqual(13069, sum(row["cases"] for row in summary))
        self.assertEqual({"routes", "reroot", "composed"}, {row["family"] for row in summary})

    def test_missing_family_or_shard_fails(self):
        self.reports.pop()
        with self.assertRaisesRegex(ValueError, "report"):
            self.verify()

    def test_duplicate_report_cannot_replace_a_missing_shard(self):
        self.reports[-1] = copy.deepcopy(self.reports[-2])
        with self.assertRaisesRegex(ValueError, "duplicate"):
            self.verify()

    def test_failed_or_unfinished_case_fails(self):
        for mutation in (lambda r: r.update(success=False),
                         lambda r: r["completedOrdinals"].pop()):
            with self.subTest(mutation=mutation):
                original = copy.deepcopy(self.reports[0])
                mutation(self.reports[0])
                with self.assertRaises(ValueError):
                    self.verify()
                self.reports[0] = original

    def test_equal_count_duplicate_and_wrong_partition_fail(self):
        for replacement in (3, 2, 0, 12389):
            with self.subTest(replacement=replacement):
                original = copy.deepcopy(self.reports[0])
                self.reports[0]["selectedOrdinals"][0] = replacement
                self.reports[0]["completedOrdinals"][0] = replacement
                with self.assertRaisesRegex(ValueError, "partition"):
                    self.verify()
                self.reports[0] = original

    def test_mismatched_inventory_or_census_fails(self):
        for field, value in (("inventorySha256", "b" * 64), ("totalCells", 25201),
                             ("unreachableCells", 12813), ("reachableCells", 12389)):
            with self.subTest(field=field):
                original = self.reports[0][field]
                self.reports[0][field] = value
                with self.assertRaises(ValueError):
                    self.verify()
                self.reports[0][field] = original

    def test_invalid_schema_and_metadata_fail(self):
        for field, value in (("schemaVersion", 2), ("family", "unknown"),
                             ("shardCount", 3), ("shardIndex", -1),
                             ("shardIndex", True), ("success", "true"),
                             ("inventorySha256", "not-a-digest")):
            with self.subTest(field=field):
                original = self.reports[0][field]
                self.reports[0][field] = value
                with self.assertRaises(ValueError):
                    self.verify()
                self.reports[0][field] = original

    def test_invalid_timing_and_incomplete_route_count_fail(self):
        for value in (-1, float("nan"), float("inf"), True):
            with self.subTest(value=value):
                self.reports[0]["elapsedMilliseconds"] = value
                with self.assertRaises(ValueError):
                    self.verify()
        self.reports[0]["elapsedMilliseconds"] = 1200
        self.reports[0]["routes"][0]["cases"] -= 1
        with self.assertRaisesRegex(ValueError, "route"):
            self.verify()

    def test_malformed_json_fails(self):
        path = self.root / "malformed.json"
        path.write_text("{", encoding="utf-8")
        with self.assertRaises(ValueError):
            verifier.verify([path], shard_count=2)

    def test_invalid_shard_count_fails(self):
        for count in (0, -1, True):
            with self.subTest(count=count), self.assertRaises(ValueError):
                verifier.verify([], shard_count=count)


if __name__ == "__main__":
    unittest.main()
