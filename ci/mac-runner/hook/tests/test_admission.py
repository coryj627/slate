"""Unit tests for the admission hook (ci/mac-runner/hook/admission.py).

Run:  python3 -m unittest discover -s ci/mac-runner/hook/tests -t ci/mac-runner/hook
Each case is one of the situations listed in plan §7 Phase 2.
"""

import json
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
import admission  # noqa: E402

REPO = 1241111067
OWNER = 933688
OWNER_LOGIN = "coryj627"
OTHER = 4242
OTHER_LOGIN = "someone-else"
FORK = 999999

ALLOWLIST = {
    "repository_id": REPO,
    "hook_events": ["pull_request", "push", "workflow_dispatch"],
    "push_refs": ["refs/heads/main"],
    "actors": [{"id": OWNER, "login": OWNER_LOGIN, "type": "User"}],
}


def env_for(event, ref="refs/heads/main", actor_id=OWNER, trigger=OWNER_LOGIN, repo_id=REPO, **extra):
    env = {
        "GITHUB_EVENT_NAME": event,
        "GITHUB_EVENT_PATH": "/dev/null",
        "GITHUB_REPOSITORY_ID": str(repo_id),
        "GITHUB_ACTOR_ID": str(actor_id),
        "GITHUB_TRIGGERING_ACTOR": trigger,
        "GITHUB_REF": ref,
    }
    env.update(extra)
    return env


def pr_payload(author=OWNER, head_repo=REPO, base_repo=REPO, sender=OWNER, number=1234):
    return {
        "repository": {"id": REPO},
        "sender": {"id": sender},
        "pull_request": {
            "number": number,
            "user": {"id": author},
            "head": {"repo": {"id": head_repo}},
            "base": {"repo": {"id": base_repo}},
        },
    }


def push_payload(ref="refs/heads/main", sender=OWNER):
    return {"repository": {"id": REPO}, "sender": {"id": sender}, "ref": ref}


def dispatch_payload(sender=OWNER):
    return {"repository": {"id": REPO}, "sender": {"id": sender}}


class AllowlistValidation(unittest.TestCase):
    def test_valid(self):
        out = admission.validate_allowlist(ALLOWLIST)
        self.assertEqual(out["repository_id"], REPO)
        self.assertEqual(out["actor_ids"], {OWNER})
        self.assertEqual(out["actor_logins"], {OWNER_LOGIN})

    def test_rejects_missing_actors(self):
        bad = dict(ALLOWLIST, actors=[])
        with self.assertRaises(admission.Deny):
            admission.validate_allowlist(bad)

    def test_rejects_non_integer_ids(self):
        bad = dict(ALLOWLIST, actors=[{"id": "933688x", "login": OWNER_LOGIN}])
        with self.assertRaises(admission.Deny):
            admission.validate_allowlist(bad)

    def test_rejects_non_object(self):
        with self.assertRaises(admission.Deny):
            admission.validate_allowlist(["nope"])

    def test_extra_keys_ignored(self):
        out = admission.validate_allowlist(dict(ALLOWLIST, candidates=[{"id": 1}], _comment="x"))
        self.assertEqual(out["actor_ids"], {OWNER})


class Decide(unittest.TestCase):
    def setUp(self):
        self.allow = admission.validate_allowlist(ALLOWLIST)

    def admit(self, env, payload):
        return admission.decide(env, payload, self.allow)

    def deny(self, env, payload, fragment):
        with self.assertRaises(admission.Deny) as ctx:
            admission.decide(env, payload, self.allow)
        self.assertIn(fragment, str(ctx.exception))

    # Admitted
    def test_owner_pr_from_same_repo(self):
        self.assertIn("pull_request #1234", self.admit(env_for("pull_request", ref="refs/pull/1234/merge"), pr_payload()))

    def test_push_to_main_by_owner(self):
        self.assertIn("push to refs/heads/main", self.admit(env_for("push"), push_payload()))

    def test_dispatch_by_owner(self):
        self.assertIn("workflow_dispatch", self.admit(env_for("workflow_dispatch"), dispatch_payload()))

    # Fork and author checks
    def test_fork_pr_denied_even_with_owner_actor(self):
        self.deny(env_for("pull_request"), pr_payload(head_repo=FORK), "head repo")

    def test_fork_pr_by_other_denied(self):
        self.deny(env_for("pull_request", actor_id=OTHER, trigger=OTHER_LOGIN),
                  pr_payload(author=OTHER, head_repo=FORK, sender=OTHER), "actor id")

    def test_same_repo_pr_by_other_author_denied(self):
        self.deny(env_for("pull_request"), pr_payload(author=OTHER), "author id")

    def test_pr_against_other_base_repo_denied(self):
        self.deny(env_for("pull_request"), pr_payload(base_repo=FORK), "head repo")

    # Events
    def test_push_to_other_branch_denied(self):
        self.deny(env_for("push", ref="refs/heads/feature"), push_payload(ref="refs/heads/feature"), "push to")

    def test_push_to_tag_denied(self):
        self.deny(env_for("push", ref="refs/tags/v1"), push_payload(ref="refs/tags/v1"), "push to")

    def test_push_ref_mismatch_denied(self):
        self.deny(env_for("push"), push_payload(ref="refs/heads/other"), "does not match")

    def test_schedule_denied(self):
        self.deny(env_for("schedule"), dispatch_payload(), "not admitted")

    def test_pull_request_target_denied(self):
        self.deny(env_for("pull_request_target"), pr_payload(), "not admitted")

    def test_workflow_run_denied(self):
        self.deny(env_for("workflow_run"), dispatch_payload(), "not admitted")

    def test_issue_comment_denied(self):
        self.deny(env_for("issue_comment"), dispatch_payload(), "not admitted")

    # Actors
    def test_dispatch_by_other_denied(self):
        self.deny(env_for("workflow_dispatch", actor_id=OTHER, trigger=OTHER_LOGIN), dispatch_payload(sender=OTHER), "actor id")

    def test_rerun_by_other_denied(self):
        # GITHUB_ACTOR stays the original actor; only the triggering login changes.
        self.deny(env_for("push", trigger=OTHER_LOGIN), push_payload(), "triggering actor")

    def test_sender_mismatch_denied(self):
        self.deny(env_for("push"), push_payload(sender=OTHER), "sender id")

    def test_actor_id_not_integer_denied(self):
        self.deny(env_for("push", actor_id="coryj627"), push_payload(), "not an integer")

    # Repository
    def test_wrong_repository_id_denied(self):
        self.deny(env_for("push", repo_id=1), push_payload(), "repository id")

    def test_payload_repository_mismatch_denied(self):
        payload = push_payload()
        payload["repository"]["id"] = 2
        self.deny(env_for("push"), payload, "payload repository id")

    # Missing or malformed input
    def test_missing_pull_request_key_denied(self):
        self.deny(env_for("pull_request"), {"repository": {"id": REPO}, "sender": {"id": OWNER}}, "payload lacks")

    def test_missing_sender_denied(self):
        self.deny(env_for("push"), {"repository": {"id": REPO}, "ref": "refs/heads/main"}, "payload lacks sender")

    def test_missing_env_denied(self):
        env = env_for("push")
        del env["GITHUB_ACTOR_ID"]
        self.deny(env, push_payload(), "environment lacks GITHUB_ACTOR_ID")

    def test_empty_env_value_denied(self):
        self.deny(env_for("push", trigger=""), push_payload(), "environment lacks GITHUB_TRIGGERING_ACTOR")

    def test_payload_not_object_denied(self):
        self.deny(env_for("push"), ["list"], "not an object")


class Main(unittest.TestCase):
    """End to end through main(): files on disk, exit codes, fail-closed paths."""

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.allow_path = os.path.join(self.tmp.name, "allowlist.json")
        self.event_path = os.path.join(self.tmp.name, "event.json")
        with open(self.allow_path, "w", encoding="utf-8") as handle:
            json.dump(ALLOWLIST, handle)
        admission.LOG_PATH = os.path.join(self.tmp.name, "log")

    def tearDown(self):
        self.tmp.cleanup()

    def run_main(self, env, payload):
        with open(self.event_path, "w", encoding="utf-8") as handle:
            json.dump(payload, handle)
        env = dict(env, GITHUB_EVENT_PATH=self.event_path, SLATE_ALLOWLIST=self.allow_path,
                   GITHUB_REPOSITORY="coryj627/slate", GITHUB_RUN_ID="1", GITHUB_RUN_ATTEMPT="1")
        return admission.main(env=env)

    def test_admit_exit_zero(self):
        self.assertEqual(self.run_main(env_for("push"), push_payload()), 0)
        with open(admission.LOG_PATH, encoding="utf-8") as handle:
            self.assertIn("ADMIT", handle.read())

    def test_deny_exit_one(self):
        self.assertEqual(self.run_main(env_for("pull_request"), pr_payload(head_repo=FORK)), 1)
        with open(admission.LOG_PATH, encoding="utf-8") as handle:
            self.assertIn("DENY", handle.read())

    def test_missing_allowlist_denies(self):
        os.remove(self.allow_path)
        self.assertEqual(self.run_main(env_for("push"), push_payload()), 1)

    def test_malformed_allowlist_denies(self):
        with open(self.allow_path, "w", encoding="utf-8") as handle:
            handle.write("{not json")
        self.assertEqual(self.run_main(env_for("push"), push_payload()), 1)

    def test_malformed_payload_denies(self):
        with open(self.event_path, "w", encoding="utf-8") as handle:
            handle.write("[")
        env = dict(env_for("push"), GITHUB_EVENT_PATH=self.event_path, SLATE_ALLOWLIST=self.allow_path)
        self.assertEqual(admission.main(env=env), 1)

    def test_missing_event_path_denies(self):
        env = dict(env_for("push"), SLATE_ALLOWLIST=self.allow_path)
        del env["GITHUB_EVENT_PATH"]
        self.assertEqual(admission.main(env=env), 1)


if __name__ == "__main__":
    unittest.main()
