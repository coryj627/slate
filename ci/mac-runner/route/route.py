#!/usr/bin/env python3
"""Pick where the mac jobs run: the Studio or Namespace. Plan §6.1 (D6).

Runs as the first job of each mac workflow on hosted Linux and writes
`label` and `route` to $GITHUB_OUTPUT (or stdout when unset).

Inputs, from repository variables:
  MAC_RUNNER_MODE       auto (default) | studio | namespace
  MAC_RUNNER_HEARTBEAT  the controller's last check-in, ISO 8601 UTC
                        ("2026-10-10T03:04:05Z") or epoch seconds; empty or
                        "0" means no heartbeat.
From the run, for the bot rule (plan 42 §3.7): ROUTE_ACTOR,
ROUTE_TRIGGERING_ACTOR, ROUTE_PR_AUTHOR and ROUTE_HEAD_REF, read against
ROUTE_ALLOWLIST (default: the allow-list beside this directory).
Optional, for tests: ROUTE_NOW (epoch seconds), ROUTE_STALE_SECONDS (600).

This is routing, not security: a PR controls its own YAML, so the hook and
GitHub's settings carry the trust (§3.2). It only chooses where the owner's
own jobs run.
"""

import calendar
import json
import os
import sys
import time

STUDIO_LABEL = "slate-mac-tart"
NAMESPACE_LABEL = "nscloud-macos-tahoe-slim-arm64-6x14"
DEFAULT_STALE_SECONDS = 600
DEFAULT_ALLOWLIST = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "allowlist.json")


def parse_heartbeat(value):
    """Return the heartbeat as epoch seconds, or None when absent/unparseable."""
    if value is None:
        return None
    value = value.strip()
    if value in ("", "0"):
        return None
    if value.isdigit():
        return int(value)
    for fmt in ("%Y-%m-%dT%H:%M:%SZ", "%Y-%m-%dT%H:%M:%S+00:00"):
        try:
            return calendar.timegm(time.strptime(value, fmt))
        except ValueError:
            continue
    return None


# A policy-only actor's branches when the allow-list cannot be read.
DEFAULT_HEAD_PREFIXES = ("dependabot/",)


def load_actor_lists(path):
    """Plan 42 §3.7: (trusted logins, policy-only logins, policy-only head
    prefixes) from the allow-list, logins in lower case, or None when it
    cannot be read."""
    try:
        with open(path, encoding="utf-8") as handle:
            data = json.load(handle)

        def entries(key):
            return [a for a in data.get(key, []) if isinstance(a, dict)]

        def logins(key):
            return {a["login"].strip().lower() for a in entries(key)
                    if isinstance(a.get("login"), str) and a["login"].strip()}

        prefixes = tuple(a["head_prefix"] for a in entries("policy_only_actors")
                         if isinstance(a.get("head_prefix"), str) and a["head_prefix"])
        return logins("actors"), logins("policy_only_actors"), prefixes
    except (OSError, ValueError, AttributeError, TypeError):
        return None


def vendor_only_actor(actors, lists):
    """The first of a run's actors that may run on vendor runners only, or
    None: a policy-only actor, or a [bot] login the allow-list does not trust.
    With no allow-list, every [bot] login is one."""
    trusted, policy_only, _ = lists if lists is not None else (set(), set(), ())
    for actor in actors:
        login = (actor or "").strip()
        lowered = login.lower()
        if lowered and (lowered in policy_only or (lowered.endswith("[bot]") and lowered not in trusted)):
            return login
    return None


def vendor_only_reason(actors, head_ref, lists):
    """Why a run must stay on vendor runners (plan 42 §3.7), or None. It
    covers who started the run (the actor and the triggering actor), who wrote
    its pull request (the author), and whose branch it runs (the head ref):
    the owner updating, reopening or dispatching on a Dependabot branch still
    runs Dependabot's code."""
    bot = vendor_only_actor(actors, lists)
    if bot:
        return "run by %s, which runs on vendor runners only (plan 42 §3.7)" % bot
    prefixes = lists[2] if lists is not None else DEFAULT_HEAD_PREFIXES
    head = (head_ref or "").strip()
    for prefix in prefixes:
        if head.startswith(prefix):
            return "a run of %s, a branch that runs on vendor runners only (plan 42 §3.7)" % head
    return None


def decide(mode, heartbeat, now, stale_seconds=DEFAULT_STALE_SECONDS, actors=(), actor_lists=None, head_ref=""):
    """Return (route, reason). route is "studio" or "namespace"."""
    bot = vendor_only_reason(actors, head_ref, actor_lists)
    if bot:
        return "namespace", bot
    mode = (mode or "auto").strip().lower()
    if mode == "studio":
        return "studio", "MAC_RUNNER_MODE=studio"
    if mode == "namespace":
        return "namespace", "MAC_RUNNER_MODE=namespace"
    if mode != "auto":
        return "namespace", f"unknown MAC_RUNNER_MODE {mode!r}, falling back"
    beat = parse_heartbeat(heartbeat)
    if beat is None:
        return "namespace", "no heartbeat"
    age = now - beat
    if age < 0:
        return "studio", f"heartbeat {-age}s in the future (clock skew), treating as fresh"
    if age <= stale_seconds:
        return "studio", f"heartbeat {age}s old"
    return "namespace", f"heartbeat {age}s old, stale after {stale_seconds}s"


def label_for(route):
    return STUDIO_LABEL if route == "studio" else NAMESPACE_LABEL


def _int_env(env, name, default):
    """An integer from the environment, or the default when unset or malformed."""
    value = (env.get(name) or "").strip()
    if not value:
        return default
    try:
        return int(value)
    except ValueError:
        print(f"ignoring malformed {name}={value!r}")
        return default


def main(env=None):
    env = os.environ if env is None else env
    now = _int_env(env, "ROUTE_NOW", int(time.time()))
    stale = _int_env(env, "ROUTE_STALE_SECONDS", DEFAULT_STALE_SECONDS)
    actors = (env.get("ROUTE_ACTOR") or "", env.get("ROUTE_TRIGGERING_ACTOR") or "", env.get("ROUTE_PR_AUTHOR") or "")
    lists = load_actor_lists(env.get("ROUTE_ALLOWLIST") or DEFAULT_ALLOWLIST)
    route, reason = decide(env.get("MAC_RUNNER_MODE"), env.get("MAC_RUNNER_HEARTBEAT"), now, stale, actors, lists,
                           env.get("ROUTE_HEAD_REF") or "")
    label = label_for(route)
    lines = [f"route={route}", f"label={label}"]
    out = env.get("GITHUB_OUTPUT")
    if out:
        with open(out, "a", encoding="utf-8") as handle:
            handle.write("\n".join(lines) + "\n")
    print(f"mac jobs route to {route} ({label}): {reason}")
    if not out:
        print("\n".join(lines))
    return 0


if __name__ == "__main__":
    sys.exit(main())
