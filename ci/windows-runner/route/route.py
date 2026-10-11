#!/usr/bin/env python3
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
"""Pick a pool for each windows.yml lane: the home pool or the vendor.

Spec: docs/superpowers/specs/2026-10-11-windows-overflow-routing-design.md
(§3 the rule, §4 the state the host publishes, §5 this router). Runs as
the first job of windows.yml on hosted Linux, only when WINDOWS_RUNNER_MODE
is home or home-only, and writes `attempt` plus one `<lane>-pool` per lane
to $GITHUB_OUTPUT (or stdout when unset). It emits pools, never labels:
both labels live in the workflow file.

Inputs, from the environment:
  WINDOWS_RUNNER_MODE   home | home-only (anything else: every lane vendor)
  WINDOWS_HOME_STATE    the state the host publishes (vars context, JSON)
  ROUTE_LANES           comma list: rust,app,model-0,model-1
  ROUTE_ATTEMPT         github.run_attempt, echoed as `attempt`
  ROUTE_RUN_ID          github.run_id (the creation-time read, the summary)
  ROUTE_EVENT, ROUTE_HEAD_REPO, ROUTE_REPO   the fork rule
  ROUTE_STALE_SECONDS   120: a state older than this at run creation is dead
  ROUTE_SLOTS_MAX       8: a published slot count above it is not serving
  ROUTE_NOW, ROUTE_RUN_CREATED   epoch seconds (tests, replay)
  ROUTE_CONCURRENT      JSON list of earlier routers' run ids (tests, replay)
  GH_TOKEN              for the two optional reads
  GITHUB_OUTPUT, GITHUB_STEP_SUMMARY

This is routing, not security: a fork runs its own copy of this file, so
the host's admission and handoff checks and the approval policy carry the
trust (spec §9).
"""

import calendar
import json
import os
import re
import subprocess
import sys
import time

# One table: lane name in ROUTE_LANES -> (the host's lane label suffix, cost
# rank). The free slots go to the lowest rank first (spec OR-D3).
LANES = {
    "app": ("app", 0),
    "model-0": ("model", 1),
    "model-1": ("model", 2),
    "rust": ("rust", 3),
}
# Minutes. Home: measured on the pool on 2026-10-11 (runs 38125616320,
# 38129938362, 38138011672, 38140861459), beside a neighbour where the
# lane usually has one; vendor: the 30-day Namespace history, windows-latest
# for the shell gate. Revise from the artifacts; tests inject their own copy.
DURATIONS = {
    "app": {"home": 26, "vendor": 22},
    "model": {"home": 25, "vendor": 6},
    "rust": {"home": 11, "vendor": 2},
    "shell": {"home": 16, "vendor": 13},
}
HANDOVER_SECONDS = 110        # settle, provision, hand off, runner online (94-131 s measured)
OVERRUN_FLOOR_SECONDS = 300   # a job past its duration is assumed to need five more minutes
DEFAULT_STALE_SECONDS = 120
DEFAULT_SLOTS_MAX = 8
STATE_VERSION = 1
ROUTE_JOB_NAME = "Route Windows lanes"
API_TIMEOUT_SECONDS = 15
CONCURRENT_RUNS_MAX = 10


def minutes(value):
    return int(value) * 60


def parse_time(value):
    """An ISO 8601 UTC timestamp as epoch seconds, or None when absent or malformed.

    Fractions are dropped first: PowerShell's 'o' format writes seven digits,
    more than strptime's %f takes.
    """
    if not isinstance(value, str):
        return None
    text = re.sub(r"\.\d+(?=Z$|\+00:00$)", "", value.strip())
    for fmt in ("%Y-%m-%dT%H:%M:%SZ", "%Y-%m-%dT%H:%M:%S+00:00"):
        try:
            return calendar.timegm(time.strptime(text, fmt))
        except ValueError:
            continue
    return None


def parse_state(text):
    """The published state as a dict, or (None, why) when it is unusable.

    Shape (spec §4): {"v": 1, "at": iso, "serving": bool, "slots": int,
    "running": [{"lane", "since"}], "queued": [{"lane", "since"}]}.
    """
    if not isinstance(text, str) or not text.strip():
        return None, "no published state"
    try:
        raw = json.loads(text)
    except ValueError as failure:
        return None, "state does not parse: %s" % failure
    if not isinstance(raw, dict):
        return None, "state is not an object"
    if raw.get("v") != STATE_VERSION:
        return None, "state version %r is not %d" % (raw.get("v"), STATE_VERSION)
    at = parse_time(raw.get("at"))
    if at is None:
        return None, "state has no readable 'at'"
    if not isinstance(raw.get("serving"), bool):
        return None, "state has no boolean 'serving'"
    slots = raw.get("slots")
    if isinstance(slots, bool) or not isinstance(slots, int):
        return None, "state has no integer 'slots'"
    entries = {}
    for key in ("running", "queued"):
        items = raw.get(key)
        if not isinstance(items, list):
            return None, "state has no list '%s'" % key
        reduced = []
        for item in items:
            if not isinstance(item, dict) or not isinstance(item.get("lane"), str):
                return None, "state '%s' entry without a lane" % key
            since = parse_time(item.get("since"))
            reduced.append({"lane": item["lane"], "since": since})
        entries[key] = reduced
    return {"at": at, "serving": raw["serving"], "slots": slots,
            "running": entries["running"], "queued": entries["queued"]}, ""


def state_death(state, error, run_created, stale_seconds, slots_max):
    """Why the pool cannot serve (spec OR-D4), or None when it can."""
    if state is None:
        return error
    age = run_created - state["at"]
    if age > stale_seconds:
        return "state is %d s old at the run's creation (stale after %d s)" % (age, stale_seconds)
    if not state["serving"]:
        return "the host reports it is not serving"
    if state["slots"] < 1:
        return "the host reports no slot"
    if state["slots"] > slots_max:
        return "the host reports %d slots, above the %d the router trusts" % (state["slots"], slots_max)
    return None


def home_duration(lane_key, durations):
    return minutes(durations[lane_key]["home"])


def simulate(state, now, concurrent, durations):
    """Seconds from now at which each slot frees, ascending.

    A running entry occupies its slot to max(since + duration, now + overrun
    floor) plus a handover; more running entries than slots keep the
    latest-ending ones. The host's queued entries take slots oldest first;
    then each router that started before this one is assumed to place an
    app lane and a model shard (the idle-pool outcome), so concurrent
    routers resolve by age.
    """
    slots = state["slots"]
    busy = []
    for entry in state["running"]:
        duration = home_duration(entry["lane"], durations) if entry["lane"] in durations else OVERRUN_FLOOR_SECONDS
        since = entry["since"] if entry["since"] is not None else now
        until = max(since + duration, now + OVERRUN_FLOOR_SECONDS) + HANDOVER_SECONDS
        busy.append(until - now)
    busy.sort()
    if len(busy) > slots:
        busy = busy[-slots:]
    while len(busy) < slots:
        busy.insert(0, 0)
    demand = [entry["lane"] for entry in sorted(state["queued"], key=lambda e: (e["since"] if e["since"] is not None else 0))]
    demand += ["app", "model"] * len(concurrent)
    for lane in demand:
        duration = home_duration(lane, durations) if lane in durations else OVERRUN_FLOOR_SECONDS
        busy.sort()
        busy[0] = max(busy[0], 0) + duration + HANDOVER_SECONDS
    return sorted(busy)


def allocate(lanes, free_slots, free_at, durations):
    """OR-D3: the app lane takes a free slot (counted, never estimated) or
    the vendor; every other lane, in cost order, queues at home only when the
    simulated schedule finishes it inside the deadline (app plus shell)."""
    slots = sorted(free_at)
    decisions = {}
    wanted = sorted((lane for lane in lanes if lane in LANES), key=lambda lane: LANES[lane][1])
    if "app" in wanted:
        if free_slots > 0 and slots:
            app_finish = home_duration("app", durations)
            slots[0] = app_finish + HANDOVER_SECONDS
            decisions["app"] = {"pool": "home", "reason": "free slot (%d free)" % free_slots}
        else:
            app_finish = minutes(durations["app"]["vendor"])
            decisions["app"] = {"pool": "vendor", "reason": "no free slot (%d free) and the app lane never queues" % free_slots}
    else:
        app_finish = minutes(durations["app"]["vendor"])
    deadline = app_finish + minutes(durations["shell"]["vendor"])
    for lane in wanted:
        if lane == "app":
            continue
        slots.sort()
        start = max(0, slots[0]) if slots else 0
        finish = start + home_duration(LANES[lane][0], durations)
        if finish <= deadline:
            if slots:
                slots[0] = finish + HANDOVER_SECONDS
            what = "free slot" if start == 0 else "queues at home"
            decisions[lane] = {"pool": "home", "reason": "%s: starts at %d min, finishes at %d min, deadline %d min" % (
                what, start // 60, finish // 60, deadline // 60)}
        else:
            decisions[lane] = {"pool": "vendor", "reason": "would start at %d min and finish at %d min, past the deadline of %d min" % (
                start // 60, finish // 60, deadline // 60)}
    return {lane: decisions[lane] for lane in lanes if lane in decisions}, deadline


def _everything(lanes, pool, reason):
    return {lane: {"pool": pool, "reason": reason} for lane in lanes if lane in LANES}


def _summary(mode):
    return {"mode": mode, "state_age": None, "serving": None, "slots": None, "running": None, "queued": None,
            "concurrent": None, "free": None, "deadline": None, "dead": False, "unknown_lanes": [], "reason": ""}


def decide(mode, lanes, state_text, now, run_created, concurrent, *, stale_seconds, slots_max, event, head_repo, repo,
           durations=None):
    """The whole rule (spec §3) as a pure function."""
    durations = DURATIONS if durations is None else durations
    normalized = (mode or "").strip().lower()
    summary = _summary(normalized or "(unset)")
    summary["unknown_lanes"] = [lane for lane in lanes if lane not in LANES]
    lanes = [lane for lane in lanes if lane in LANES]
    if normalized == "home-only":
        summary["reason"] = "WINDOWS_RUNNER_MODE=home-only: every lane queues at home"
        return _everything(lanes, "home", summary["reason"]), summary
    if normalized != "home":
        summary["reason"] = "WINDOWS_RUNNER_MODE=%s routes every lane to the vendor" % summary["mode"]
        return _everything(lanes, "vendor", summary["reason"]), summary
    if event == "pull_request" and (not head_repo or head_repo != repo):
        summary["reason"] = "pull request from %s: the vendor, whatever the pool says" % (head_repo or "an unknown repository")
        return _everything(lanes, "vendor", summary["reason"]), summary
    state, error = parse_state(state_text)
    death = state_death(state, error, run_created, stale_seconds, slots_max)
    if state is not None:
        summary.update({"state_age": run_created - state["at"], "serving": state["serving"], "slots": state["slots"],
                        "running": len(state["running"]), "queued": len(state["queued"])})
    if death:
        summary["dead"] = True
        summary["reason"] = "the pool cannot serve: %s" % death
        return _everything(lanes, "vendor", summary["reason"]), summary
    concurrent = list(concurrent or [])
    summary["concurrent"] = len(concurrent)
    free_slots = max(0, state["slots"] - len(state["running"]) - len(state["queued"]) - 2 * len(concurrent))
    summary["free"] = free_slots
    decisions, deadline = allocate(lanes, free_slots, simulate(state, now, concurrent, durations), durations)
    summary["deadline"] = deadline
    summary["reason"] = "home first; a lane queues only inside the deadline"
    return decisions, summary


def gh_api(path, timeout=API_TIMEOUT_SECONDS):
    """One GET through gh (GH_TOKEN from the step's env), one retry, stderr in the error."""
    last = None
    for attempt in (1, 2):
        try:
            completed = subprocess.run(["gh", "api", path], capture_output=True, text=True, timeout=timeout, check=False)
        except (OSError, subprocess.TimeoutExpired) as failure:
            last = RuntimeError("gh api %s: %s" % (path, failure))
            continue
        if completed.returncode == 0:
            return json.loads(completed.stdout)
        last = RuntimeError("gh api %s exited %d: %s" % (path, completed.returncode, (completed.stderr or "").strip()[:300]))
    raise last


def fetch_run_created(repo, run_id, get):
    """The run's creation time (the moment the vars snapshot was taken)."""
    body = get("/repos/%s/actions/runs/%s" % (repo, run_id))
    created = parse_time(body.get("created_at"))
    if created is None:
        raise ValueError("run %s has no readable created_at" % run_id)
    return created


def fetch_concurrent(repo, run_id, run_created, get):
    """Ids of windows.yml runs created before this one whose routing job is still queued or running."""
    earlier = []
    seen = set()
    for status in ("queued", "in_progress"):
        body = get("/repos/%s/actions/workflows/windows.yml/runs?status=%s&per_page=30" % (repo, status))
        for run in body.get("workflow_runs") or []:
            other = run.get("id")
            if other is None or other == int(run_id) or other in seen:
                continue
            seen.add(other)
            created = parse_time(run.get("created_at"))
            if created is None or created > run_created or (created == run_created and other > int(run_id)):
                continue
            earlier.append(other)
    concurrent = []
    for other in sorted(earlier)[:CONCURRENT_RUNS_MAX]:
        jobs = get("/repos/%s/actions/runs/%s/jobs?filter=latest&per_page=100" % (repo, other)).get("jobs") or []
        if any(job.get("name") == ROUTE_JOB_NAME and job.get("status") in ("queued", "in_progress") for job in jobs):
            concurrent.append(other)
    return concurrent


def replay_line(env, now, run_created, concurrent):
    parts = ["WINDOWS_RUNNER_MODE=%s" % json.dumps(env.get("WINDOWS_RUNNER_MODE") or ""),
             "WINDOWS_HOME_STATE=%s" % json.dumps(env.get("WINDOWS_HOME_STATE") or ""),
             "ROUTE_LANES=%s" % (env.get("ROUTE_LANES") or ""),
             "ROUTE_NOW=%d" % now, "ROUTE_RUN_CREATED=%d" % run_created,
             "ROUTE_CONCURRENT=%s" % json.dumps(json.dumps(concurrent)),
             "ROUTE_EVENT=%s" % (env.get("ROUTE_EVENT") or ""),
             "ROUTE_HEAD_REPO=%s" % (env.get("ROUTE_HEAD_REPO") or ""),
             "ROUTE_REPO=%s" % (env.get("ROUTE_REPO") or "")]
    return " ".join(parts) + " python3 ci/windows-runner/route/route.py"


def render_summary(summary, decisions, lanes, run_id, attempt, replay):
    lines = ["## Windows lanes: mode %s (run %s, attempt %s)" % (summary["mode"], run_id, attempt), ""]
    if summary["slots"] is not None:
        lines += ["| state age at creation | serving | slots | running | queued | earlier routers | free slots | deadline |",
                  "|---|---|---|---|---|---|---|---|",
                  "| %s s | %s | %s | %s | %s | %s | %s | %s |" % (
                      summary["state_age"], summary["serving"], summary["slots"], summary["running"], summary["queued"],
                      "-" if summary["concurrent"] is None else summary["concurrent"],
                      "-" if summary["free"] is None else summary["free"],
                      "-" if summary["deadline"] is None else "%d min" % (summary["deadline"] // 60)), ""]
    lines += [summary["reason"], "", "| lane | pool | reason |", "|---|---|---|"]
    for lane in lanes:
        decision = decisions.get(lane)
        if decision:
            lines.append("| %s | %s | %s |" % (lane, decision["pool"], decision["reason"]))
    for lane in summary["unknown_lanes"]:
        lines.append("| %s | (withheld) | not a routed lane; the workflow's fallback applies |" % lane)
    lines += ["", "Replay: `%s`" % replay]
    return "\n".join(lines) + "\n"


def write_outputs(env, decisions, attempt):
    lines = ["attempt=%s" % attempt] + ["%s-pool=%s" % (lane, decision["pool"]) for lane, decision in decisions.items()]
    out = env.get("GITHUB_OUTPUT")
    if out:
        with open(out, "a", encoding="utf-8") as handle:
            handle.write("\n".join(lines) + "\n")
    else:
        print("\n".join(lines))


def _int_env(env, name, default):
    value = (env.get(name) or "").strip()
    if not value:
        return default
    try:
        return int(value)
    except ValueError:
        print("ignoring malformed %s=%r" % (name, value))
        return default


def main(env=None, get=gh_api, clock=time.time):
    env = os.environ if env is None else env
    lanes = [lane.strip() for lane in (env.get("ROUTE_LANES") or "").split(",") if lane.strip()]
    attempt = (env.get("ROUTE_ATTEMPT") or "").strip() or "1"
    run_id = (env.get("ROUTE_RUN_ID") or "").strip() or "0"
    repo = env.get("ROUTE_REPO") or ""
    mode = env.get("WINDOWS_RUNNER_MODE")
    now = _int_env(env, "ROUTE_NOW", int(clock()))
    run_created = now
    concurrent = []
    try:
        normalized = (mode or "").strip().lower()
        if normalized == "home":
            if (env.get("ROUTE_RUN_CREATED") or "").strip():
                run_created = _int_env(env, "ROUTE_RUN_CREATED", now)
            else:
                try:
                    run_created = fetch_run_created(repo, run_id, get)
                except Exception as failure:  # noqa: BLE001 - optional read; the clock stands in
                    print("::notice::run creation time unavailable (%s); using now" % failure)
            if (env.get("ROUTE_CONCURRENT") or "").strip():
                concurrent = [int(item) for item in json.loads(env["ROUTE_CONCURRENT"])]
            else:
                try:
                    concurrent = fetch_concurrent(repo, run_id, run_created, get)
                except Exception as failure:  # noqa: BLE001 - optional read; assume no earlier router
                    print("::notice::earlier routers unknown (%s); assuming none" % failure)
        decisions, summary = decide(mode, lanes, env.get("WINDOWS_HOME_STATE"), now, run_created, concurrent,
                                    stale_seconds=_int_env(env, "ROUTE_STALE_SECONDS", DEFAULT_STALE_SECONDS),
                                    slots_max=_int_env(env, "ROUTE_SLOTS_MAX", DEFAULT_SLOTS_MAX),
                                    event=env.get("ROUTE_EVENT") or "", head_repo=env.get("ROUTE_HEAD_REPO") or "", repo=repo)
    except Exception as failure:  # noqa: BLE001 - every failure routes to the vendor, never fails the run
        reason = "%s: %s; every lane goes to the vendor" % (type(failure).__name__, failure)
        print("::warning title=Windows lane router::%s" % reason)
        decisions = _everything(lanes, "vendor", reason)
        summary = _summary((mode or "").strip().lower() or "(unset)")
        summary["reason"] = reason
    text = render_summary(summary, decisions, lanes, run_id, attempt, replay_line(env, now, run_created, concurrent))
    print(text)
    step_summary = env.get("GITHUB_STEP_SUMMARY")
    if step_summary:
        with open(step_summary, "a", encoding="utf-8") as handle:
            handle.write(text)
    write_outputs(env, decisions, attempt)
    return 0


if __name__ == "__main__":
    sys.exit(main())
