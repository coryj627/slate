#!/usr/bin/env python3
"""Pick where the mac jobs run: the Studio or Namespace. Plan §6.1 (D6).

Runs as the first job of each mac workflow on hosted Linux and writes
`label` and `route` to $GITHUB_OUTPUT (or stdout when unset).

Inputs, from repository variables:
  MAC_RUNNER_MODE       auto (default) | studio | namespace
  MAC_RUNNER_HEARTBEAT  the controller's last check-in, ISO 8601 UTC
                        ("2026-10-10T03:04:05Z") or epoch seconds; empty or
                        "0" means no heartbeat.
Optional, for tests: ROUTE_NOW (epoch seconds), ROUTE_STALE_SECONDS (600).

This is routing, not security: a PR controls its own YAML, so the hook and
GitHub's settings carry the trust (§3.2). It only chooses where the owner's
own jobs run.
"""

import calendar
import os
import sys
import time

STUDIO_LABEL = "slate-mac-tart"
NAMESPACE_LABEL = "nscloud-macos-tahoe-slim-arm64-6x14"
DEFAULT_STALE_SECONDS = 600


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


def decide(mode, heartbeat, now, stale_seconds=DEFAULT_STALE_SECONDS):
    """Return (route, reason). route is "studio" or "namespace"."""
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


def main(env=None):
    env = os.environ if env is None else env
    now = int(env.get("ROUTE_NOW") or time.time())
    stale = int(env.get("ROUTE_STALE_SECONDS") or DEFAULT_STALE_SECONDS)
    route, reason = decide(env.get("MAC_RUNNER_MODE"), env.get("MAC_RUNNER_HEARTBEAT"), now, stale)
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
