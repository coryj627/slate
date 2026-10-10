#!/usr/bin/env python3
"""Admission hook for the slate self-hosted mac runner.

GitHub runs this as the runner's ACTIONS_RUNNER_HOOK_JOB_STARTED script, the
"Set up runner" step, before any action's pre: step, before checkout and
before any workflow step. A non-zero exit fails the job before it starts.
docs/plans/42_self_hosted_mac_runner_plan.md, section 3.2, control 5.

Decision inputs are only things a pull request cannot write: the runner's
GITHUB_* environment, the event payload GitHub delivered, and the allow-list
the host controller streamed into this VM just before the runner started.
Every check compares numeric IDs where GitHub provides them. Anything
missing, malformed or unexpected denies.

Standalone, stdlib only: /usr/bin/python3 -I admission.py
"""

import json
import os
import sys
import time

DEFAULT_ALLOWLIST = "/Users/runner/.slate-runner/allowlist.json"
LOG_PATH = "/tmp/slate-admission.log"

EVENT_KEYS = ("GITHUB_EVENT_NAME", "GITHUB_EVENT_PATH", "GITHUB_REPOSITORY_ID",
              "GITHUB_ACTOR_ID", "GITHUB_TRIGGERING_ACTOR", "GITHUB_REF")


class Deny(Exception):
    """Raised with the reason a job is refused."""


def _int(value, what):
    try:
        return int(value)
    except (TypeError, ValueError):
        raise Deny(f"{what} is not an integer: {value!r}")


def _get(mapping, path, what):
    """Walk a nested mapping; a missing or wrong-typed step denies."""
    cur = mapping
    for key in path:
        if not isinstance(cur, dict) or key not in cur:
            raise Deny(f"payload lacks {what} ({'.'.join(path)})")
        cur = cur[key]
    return cur


def load_allowlist(path):
    """Parse and validate the allow-list. Returns a dict or raises Deny."""
    try:
        with open(path, "r", encoding="utf-8") as handle:
            data = json.load(handle)
    except (OSError, ValueError) as exc:
        raise Deny(f"allow-list unreadable at {path}: {exc}")
    return validate_allowlist(data)


def validate_allowlist(data):
    if not isinstance(data, dict):
        raise Deny("allow-list is not an object")
    out = {}
    out["repository_id"] = _int(data.get("repository_id"), "allow-list repository_id")
    events = data.get("hook_events")
    if not isinstance(events, list) or not events or not all(isinstance(e, str) for e in events):
        raise Deny("allow-list hook_events must be a non-empty list of strings")
    out["hook_events"] = set(events)
    refs = data.get("push_refs")
    if not isinstance(refs, list) or not all(isinstance(r, str) for r in refs):
        raise Deny("allow-list push_refs must be a list of strings")
    out["push_refs"] = set(refs)
    actors = data.get("actors")
    if not isinstance(actors, list) or not actors:
        raise Deny("allow-list actors must be a non-empty list")
    ids, logins = set(), set()
    for actor in actors:
        if not isinstance(actor, dict):
            raise Deny("allow-list actor is not an object")
        ids.add(_int(actor.get("id"), "allow-list actor id"))
        login = actor.get("login")
        if not isinstance(login, str) or not login:
            raise Deny("allow-list actor login must be a non-empty string")
        logins.add(login)
    out["actor_ids"] = ids
    out["actor_logins"] = logins
    return out


def decide(env, payload, allowlist):
    """Return a reason string when the job is admitted; raise Deny otherwise."""
    for key in EVENT_KEYS:
        if key not in env or env[key] == "":
            raise Deny(f"environment lacks {key}")
    if not isinstance(payload, dict):
        raise Deny("event payload is not an object")

    repo_id = allowlist["repository_id"]
    if _int(env["GITHUB_REPOSITORY_ID"], "GITHUB_REPOSITORY_ID") != repo_id:
        raise Deny(f"repository id {env['GITHUB_REPOSITORY_ID']} is not {repo_id}")
    if _int(_get(payload, ("repository", "id"), "repository id"), "payload repository id") != repo_id:
        raise Deny("payload repository id does not match")

    event = env["GITHUB_EVENT_NAME"]
    if event not in allowlist["hook_events"]:
        raise Deny(f"event {event!r} is not admitted")

    actor_id = _int(env["GITHUB_ACTOR_ID"], "GITHUB_ACTOR_ID")
    if actor_id not in allowlist["actor_ids"]:
        raise Deny(f"actor id {actor_id} is not on the allow-list")
    # A re-run is attributed to whoever re-ran it, and only by login.
    trigger = env["GITHUB_TRIGGERING_ACTOR"]
    if trigger not in allowlist["actor_logins"]:
        raise Deny(f"triggering actor {trigger!r} is not on the allow-list")
    sender_id = _int(_get(payload, ("sender", "id"), "sender id"), "payload sender id")
    if sender_id not in allowlist["actor_ids"]:
        raise Deny(f"sender id {sender_id} is not on the allow-list")

    if event == "pull_request":
        head_repo = _int(_get(payload, ("pull_request", "head", "repo", "id"), "head repository id"), "head repo id")
        base_repo = _int(_get(payload, ("pull_request", "base", "repo", "id"), "base repository id"), "base repo id")
        if head_repo != repo_id:
            raise Deny(f"pull request head repo {head_repo} is not this repository")
        if base_repo != repo_id:
            raise Deny(f"pull request base repo {base_repo} is not this repository")
        author = _int(_get(payload, ("pull_request", "user", "id"), "pull request author id"), "author id")
        if author not in allowlist["actor_ids"]:
            raise Deny(f"pull request author id {author} is not on the allow-list")
        number = _get(payload, ("pull_request", "number"), "pull request number")
        return f"pull_request #{number} by {author}, same-repo head, actor {actor_id}"

    if event == "push":
        ref = env["GITHUB_REF"]
        if ref not in allowlist["push_refs"]:
            raise Deny(f"push to {ref!r} is not admitted")
        if _get(payload, ("ref",), "push ref") != ref:
            raise Deny("payload ref does not match GITHUB_REF")
        return f"push to {ref} by {actor_id}"

    if event == "workflow_dispatch":
        return f"workflow_dispatch by {actor_id}"

    raise Deny(f"event {event!r} has no admission rule")


def _log(line):
    try:
        with open(LOG_PATH, "a", encoding="utf-8") as handle:
            handle.write(time.strftime("%Y-%m-%dT%H:%M:%SZ ", time.gmtime()) + line + "\n")
    except OSError:
        pass


def main(argv=None, env=None):
    env = dict(os.environ if env is None else env)
    allowlist_path = env.get("SLATE_ALLOWLIST", DEFAULT_ALLOWLIST)
    context = "{} {} run {} attempt {}".format(
        env.get("GITHUB_REPOSITORY", "?"), env.get("GITHUB_EVENT_NAME", "?"),
        env.get("GITHUB_RUN_ID", "?"), env.get("GITHUB_RUN_ATTEMPT", "?"))
    try:
        allowlist = load_allowlist(allowlist_path)
        path = env.get("GITHUB_EVENT_PATH")
        if not path:
            raise Deny("environment lacks GITHUB_EVENT_PATH")
        try:
            with open(path, "r", encoding="utf-8") as handle:
                payload = json.load(handle)
        except (OSError, ValueError) as exc:
            raise Deny(f"event payload unreadable: {exc}")
        reason = decide(env, payload, allowlist)
    except Deny as exc:
        line = f"DENY {context}: {exc}"
        _log(line)
        print(f"::error::slate mac runner admission: {exc}")
        print(line)
        return 1
    except Exception as exc:  # anything unexpected is a denial, never a pass
        line = f"DENY {context}: unexpected {type(exc).__name__}: {exc}"
        _log(line)
        print(f"::error::slate mac runner admission: {exc}")
        print(line)
        return 1
    line = f"ADMIT {context}: {reason}"
    _log(line)
    print(line)
    return 0


if __name__ == "__main__":
    sys.exit(main())
