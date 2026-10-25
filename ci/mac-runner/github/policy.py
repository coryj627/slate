#!/usr/bin/env python3
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
"""The workflow execution policy apply-policy.sh writes, built from
ci/mac-runner/allowlist.json. Plan 42 §3.2 control 2, §3.6 and §3.7.

The policy allows `actors` and `policy_only_actors` (§3.7: a bot whose runs
may start but never reach the owner's hardware), and only `policy_events`,
for every workflow (D3). The hook reads `actors` alone, so a policy-only
actor's jobs are refused on the Studio whatever this policy allows.

    python3 -I policy.py <allowlist.json> <policy name>   # prints the body
"""

import json
import sys


def allowed_actors(data):
    """`actors` then `policy_only_actors`, by id and type, each id once. A
    policy-only entry must name its type, and no id may be in both lists:
    the hook would trust what the routers send away."""
    policy_only = list(data.get("policy_only_actors", []))
    trusted_ids = {int(a["id"]) for a in data["actors"]}
    for actor in policy_only:
        if not actor.get("type"):
            raise ValueError("policy_only_actors entry %s names no type" % actor.get("login", actor.get("id")))
        if int(actor["id"]) in trusted_ids:
            raise ValueError("actor %s is in both actors and policy_only_actors" % actor["id"])
    out, seen = [], set()
    for actor in list(data["actors"]) + policy_only:
        actor_id = int(actor["id"])
        if actor_id in seen:
            continue
        seen.add(actor_id)
        out.append({"id": actor_id, "type": actor.get("type", "User")})
    return out


def build_policy(data, name):
    return {
        "name": name,
        "enforcement": "active",
        "rules": [
            {"type": "restrict_actions_actors", "parameters": {"allowed_actors": allowed_actors(data)}},
            {"type": "restrict_action_events", "parameters": {"allowed_events": list(data["policy_events"])}},
        ],
    }


def main(argv):
    with open(argv[1], encoding="utf-8") as handle:
        data = json.load(handle)
    print(json.dumps(build_policy(data, argv[2]), indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
