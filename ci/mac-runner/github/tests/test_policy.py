# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
"""Tests for the policy builder (ci/mac-runner/github/policy.py).
Run:  python3 -I -m unittest discover -s ci/mac-runner/github/tests -t ci/mac-runner/github
"""
import json
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
import policy  # noqa: E402

ALLOWLIST = os.path.join(os.path.dirname(__file__), "..", "..", "allowlist.json")
OWNER = {"id": 933688, "login": "coryj627", "type": "User"}
DEPENDABOT = {"id": 49699333, "login": "dependabot[bot]", "type": "Bot"}


def actors_of(body):
    rule = next(r for r in body["rules"] if r["type"] == "restrict_actions_actors")
    return rule["parameters"]["allowed_actors"]


class BuildPolicy(unittest.TestCase):
    def test_the_policy_allows_actors_and_policy_only_actors(self):
        data = {"actors": [OWNER], "policy_only_actors": [DEPENDABOT], "policy_events": ["push"]}
        self.assertEqual(
            actors_of(policy.build_policy(data, "p")),
            [{"id": 933688, "type": "User"}, {"id": 49699333, "type": "Bot"}])

    def test_an_allowlist_without_policy_only_actors_allows_actors_alone(self):
        data = {"actors": [OWNER], "policy_events": ["push"]}
        self.assertEqual(actors_of(policy.build_policy(data, "p")), [{"id": 933688, "type": "User"}])

    def test_an_actor_in_both_lists_is_refused(self):
        data = {"actors": [OWNER], "policy_only_actors": [OWNER], "policy_events": ["push"]}
        with self.assertRaises(ValueError):
            policy.build_policy(data, "p")

    def test_a_policy_only_actor_must_name_its_type(self):
        untyped = {"id": 49699333, "login": "dependabot[bot]"}
        data = {"actors": [OWNER], "policy_only_actors": [untyped], "policy_events": ["push"]}
        with self.assertRaises(ValueError):
            policy.build_policy(data, "p")

    def test_the_events_and_the_name_come_from_the_allowlist(self):
        data = {"actors": [OWNER], "policy_events": ["push", "schedule"]}
        body = policy.build_policy(data, "slate")
        self.assertEqual(body["name"], "slate")
        self.assertEqual(body["enforcement"], "active")
        events = next(r for r in body["rules"] if r["type"] == "restrict_action_events")
        self.assertEqual(events["parameters"]["allowed_events"], ["push", "schedule"])

    def test_the_repository_allowlist_grants_dependabot_the_policy_only(self):
        with open(ALLOWLIST, encoding="utf-8") as handle:
            data = json.load(handle)
        self.assertIn({"id": 49699333, "type": "Bot"}, actors_of(policy.build_policy(data, "p")))
        self.assertNotIn(49699333, [a["id"] for a in data["actors"]])


if __name__ == "__main__":
    unittest.main()
