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
  ROUTE_RUN_ID          github.run_id (the start-time read, the summary)
  ROUTE_EVENT, ROUTE_HEAD_REPO, ROUTE_REPO   the fork rule
  ROUTE_ACTOR, ROUTE_TRIGGERING_ACTOR, ROUTE_PR_AUTHOR, ROUTE_HEAD_REF
                        the bot rule (plan 42 §3.7): who started the run,
                        who wrote its pull request, whose branch it runs
  ROUTE_ALLOWLIST       the allow-list the bot rule reads (default:
                        ci/mac-runner/allowlist.json beside this tree)
  ROUTE_STALE_SECONDS   180: a state whose `at` is older than this by the router's clock is dead
  ROUTE_SLOTS_MAX       8: a published slot count above it is not serving
  ROUTE_NOW, ROUTE_RUN_STARTED   epoch seconds (tests, replay); ROUTE_NOW is the
                        clock every age is measured against, the start only
                        orders concurrent routers
  ROUTE_CONCURRENT      JSON list of [run id, routing job completion epoch or
                        null] pairs for the earlier routers (tests, replay)
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
DEFAULT_ALLOWLIST = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "mac-runner", "allowlist.json")
DEFAULT_STALE_SECONDS = 180
DEFAULT_SLOTS_MAX = 8
STATE_VERSION = 1
ROUTE_JOB_NAME = "Route Windows lanes"
API_TIMEOUT_SECONDS = 15
RUNS_PER_PAGE = 100                         # the runs listing, per status: pages of 100 ...
RUNS_PAGES_MAX = 5                          # ... until a short page, five at most
CONCURRENT_RUNS_MAX = 10                    # earlier runs inspected: the newest ten
CONCURRENT_COMPLETION_MARGIN_SECONDS = 30   # a router done this long before the state's `at` may not be in it yet


def minutes(value):
    return int(value) * 60


def in_minutes(seconds):
    """Seconds as minutes with one decimal, the way spec §3 writes a schedule (26.8, 37.8)."""
    return "%.1f min" % (seconds / 60)


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


def state_death(state, error, now, stale_seconds, slots_max):
    """Why the pool cannot serve (spec OR-D4), or None when it can.

    The age is measured against the router's own clock: GitHub resolves vars
    when the job starts, and a run's creation time never moves across a
    re-run or a concurrency hold. A future `at` (the host's clock ahead)
    counts as fresh.
    """
    if state is None:
        return error
    age = now - state["at"]
    if age > stale_seconds:
        return "state is %d s old by the router's clock (stale after %d s)" % (age, stale_seconds)
    if not state["serving"]:
        return "the host reports it is not serving"
    if state["slots"] < 1:
        return "the host reports no slot"
    if state["slots"] > slots_max:
        return "the host reports %d slots, above the %d the router trusts" % (state["slots"], slots_max)
    return None


def home_duration(lane_key, durations):
    return minutes(durations[lane_key]["home"])


def entry_duration(entry, durations):
    """A published entry's home duration; the longest in the table when its
    lane is unknown (spec §3: finite and conservative)."""
    if entry["lane"] in durations:
        return home_duration(entry["lane"], durations)
    return max(home_duration(lane_key, durations) for lane_key in durations)


def simulate(state, now, durations):
    """Seconds from now at which each slot frees, ascending, from what the host published.

    A running entry occupies its slot to max(since + duration, now + overrun
    floor) plus a handover; more running entries than slots keep the
    latest-ending ones. The host's queued entries take slots oldest first.
    """
    slots = state["slots"]
    busy = []
    for entry in state["running"]:
        since = entry["since"] if entry["since"] is not None else now
        until = max(since + entry_duration(entry, durations), now + OVERRUN_FLOOR_SECONDS) + HANDOVER_SECONDS
        busy.append(until - now)
    busy.sort()
    if len(busy) > slots:
        busy = busy[-slots:]
    while len(busy) < slots:
        busy.insert(0, 0)
    for entry in sorted(state["queued"], key=lambda e: (e["since"] if e["since"] is not None else 0)):
        busy.sort()
        busy[0] = max(busy[0], 0) + entry_duration(entry, durations) + HANDOVER_SECONDS
    return sorted(busy)


def allocate(lanes, free_slots, free_at, durations):
    """OR-D3: the app lane takes a free slot (counted, never estimated) or
    the vendor; every other lane, in cost order, queues at home only when the
    simulated schedule finishes it inside the deadline (app plus shell).

    Returns the decisions, the deadline and the schedule with the home lanes
    placed (each slot busy to its lane's finish plus a handover).
    """
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
            decisions[lane] = {"pool": "home", "reason": "%s: starts at %s, finishes at %s, deadline %s" % (
                what, in_minutes(start), in_minutes(finish), in_minutes(deadline))}
        else:
            decisions[lane] = {"pool": "vendor", "reason": "would start at %s and finish at %s, past the deadline of %s" % (
                in_minutes(start), in_minutes(finish), in_minutes(deadline))}
    return {lane: decisions[lane] for lane in lanes if lane in decisions}, deadline, sorted(slots)


def replay_earlier_routers(free_at, free_slots, count, durations):
    """Spec §3: `count` earlier routers' allocations replayed against the schedule so far.

    Each earlier router is replayed with the same allocation this run makes;
    every router routes the same lanes, so oldest first is immaterial to the
    result. Each one sees the free slots the routers before it left, and its
    home lanes stay in the schedule, so concurrent routers resolve by age
    for any slot count. Returns this run's free slots and schedule.
    """
    schedule = sorted(free_at)
    for _ in range(count):
        _, _, schedule = allocate(list(LANES), free_slots, schedule, durations)
        free_slots = min(free_slots, sum(1 for until in schedule if until <= 0))
    return free_slots, schedule


def _everything(lanes, pool, reason):
    return {lane: {"pool": pool, "reason": reason} for lane in lanes if lane in LANES}


def normalize_mode(mode):
    return (mode or "").strip().lower()


def is_fork(event, head_repo, repo):
    """OR-D5: a pull request whose head repository is unknown or another one."""
    return event == "pull_request" and (not head_repo or head_repo != repo)


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


def _summary(mode):
    return {"mode": mode, "state_age": None, "serving": None, "slots": None, "running": None, "queued": None,
            "concurrent": None, "free": None, "deadline": None, "dead": False, "unknown_lanes": [], "reason": ""}


def _skew(state_age):
    """The reason's note for a state whose `at` is ahead of the router's clock, else nothing."""
    if state_age is None or state_age >= 0:
        return ""
    return "; the state's 'at' is %d s in the future (clock skew), counted as fresh" % -state_age


def settle_early(mode, lanes, state_text, now, *, stale_seconds, slots_max, event, head_repo, repo,
                 actors=(), head_ref="", actor_lists=None):
    """The decision before occupancy (spec §3), shared by decide and main.

    Returns (decisions, summary, None) when the fork rule, the mode or the
    state's death (missing, unparseable, stale, not serving, no slot)
    settles every lane, else (None, summary, state) with the state parsed
    once for decide_from_occupancy. Only in the second case do main's two
    reads matter.
    """
    normalized = normalize_mode(mode)
    summary = _summary(normalized or "(unset)")
    summary["unknown_lanes"] = [lane for lane in lanes if lane not in LANES]
    if is_fork(event, head_repo, repo):
        summary["reason"] = "pull request from %s: the vendor in every mode, whatever the pool says" % (
            head_repo or "an unknown repository")
        return _everything(lanes, "vendor", summary["reason"]), summary, None
    bot = vendor_only_reason(actors, head_ref, actor_lists)
    if bot:
        summary["reason"] = bot + ": the vendor in every mode"
        return _everything(lanes, "vendor", summary["reason"]), summary, None
    if normalized == "home-only":
        summary["reason"] = "WINDOWS_RUNNER_MODE=home-only: every lane queues at home"
        return _everything(lanes, "home", summary["reason"]), summary, None
    if normalized != "home":
        summary["reason"] = "WINDOWS_RUNNER_MODE=%s routes every lane to the vendor" % summary["mode"]
        return _everything(lanes, "vendor", summary["reason"]), summary, None
    state, error = parse_state(state_text)
    death = state_death(state, error, now, stale_seconds, slots_max)
    if state is not None:
        summary.update({"state_age": now - state["at"], "serving": state["serving"], "slots": state["slots"],
                        "running": len(state["running"]), "queued": len(state["queued"])})
    if death:
        summary["dead"] = True
        summary["reason"] = "the pool cannot serve: %s%s" % (death, _skew(summary["state_age"]))
        return _everything(lanes, "vendor", summary["reason"]), summary, None
    return None, summary, state


def decide_from_occupancy(lanes, state, now, concurrent, summary, durations):
    """OR-D3 over a live state: the earlier routers replayed, then this run's allocation; completes `summary`."""
    # An earlier router counts while its routing job is queued or running, and after it completed only when that was
    # at most the margin before the state's `at`: its lanes may not be in the state yet.
    earlier = sum(1 for _, completed in concurrent or []
                  if completed is None or completed >= state["at"] - CONCURRENT_COMPLETION_MARGIN_SECONDS)
    summary["concurrent"] = earlier
    free_slots = max(0, state["slots"] - len(state["running"]) - len(state["queued"]))
    free_slots, schedule = replay_earlier_routers(simulate(state, now, durations), free_slots, earlier, durations)
    summary["free"] = free_slots
    decisions, deadline, _ = allocate(lanes, free_slots, schedule, durations)
    summary["deadline"] = deadline
    summary["reason"] = "home first; a lane queues only inside the deadline" + _skew(summary["state_age"])
    return decisions, summary


def decide(mode, lanes, state_text, now, concurrent, *, stale_seconds, slots_max, event, head_repo, repo, durations=None,
           actors=(), head_ref="", actor_lists=None):
    """The whole rule (spec §3) as a pure function; every age is measured against `now`, the router's clock."""
    decisions, summary, state = settle_early(mode, lanes, state_text, now, stale_seconds=stale_seconds,
                                             slots_max=slots_max, event=event, head_repo=head_repo, repo=repo,
                                             actors=actors, head_ref=head_ref, actor_lists=actor_lists)
    if state is None:
        return decisions, summary
    return decide_from_occupancy(lanes, state, now, concurrent, summary, DURATIONS if durations is None else durations)


def gh_api(path, timeout=API_TIMEOUT_SECONDS, run=subprocess.run):
    """One GET through gh (GH_TOKEN from the step's env), one retry, stderr in the error; `run` is the test seam."""
    last = None
    for _ in (1, 2):
        try:
            completed = run(["gh", "api", path], capture_output=True, text=True, timeout=timeout, check=False)
        except (OSError, subprocess.TimeoutExpired) as failure:
            last = RuntimeError("gh api %s: %s" % (path, failure))
            continue
        if completed.returncode == 0:
            return json.loads(completed.stdout)
        last = RuntimeError("gh api %s exited %d: %s" % (path, completed.returncode, (completed.stderr or "").strip()[:300]))
    raise last


def run_start(run):
    """A run's latest attempt start: run_started_at (it resets on a re-run), else created_at; None when neither reads."""
    started = parse_time(run.get("run_started_at"))
    return started if started is not None else parse_time(run.get("created_at"))


def fetch_run_started(repo, run_id, get):
    """This attempt's start time; it orders concurrent routers and moves no age."""
    started = run_start(get("/repos/%s/actions/runs/%s" % (repo, run_id)))
    if started is None:
        raise ValueError("run %s has no readable run_started_at or created_at" % run_id)
    return started


def list_runs(repo, status, get):
    """windows.yml's runs with this status, page by page until a short page or RUNS_PAGES_MAX pages.

    A failing first page raises (the caller then assumes no earlier
    router); a later page that fails keeps the runs already read, since
    nothing safer is known, and says so in a notice.
    """
    runs = []
    for page in range(1, RUNS_PAGES_MAX + 1):
        path = "/repos/%s/actions/workflows/windows.yml/runs?status=%s&per_page=%d&page=%d" % (
            repo, status, RUNS_PER_PAGE, page)
        try:
            batch = get(path).get("workflow_runs") or []
        except Exception as failure:  # noqa: BLE001 - a later page: keep what was read
            if page == 1:
                raise
            print("::notice::%s" % annotation_text("%s runs past page %d unreadable (%s); using the %d read" % (
                status, page - 1, failure, len(runs))))
            break
        runs += batch
        if len(batch) < RUNS_PER_PAGE:
            break
    return runs


def fetch_concurrent(repo, run_id, run_started, get):
    """The windows.yml runs that started before this one, as (run id, routing job completion epoch or None) pairs.

    "Before" compares attempt starts (run_start), ties going to the lower
    id, so every router agrees on the order. Only the newest ten are
    inspected, and a run counts when its routing job is queued or in
    progress (None) or completed (its completion time; decide_from_occupancy
    keeps it only inside the margin before the state's `at`). A completed
    job without a readable completion time counts as still running: toward
    the vendor. Oldest first.
    """
    this = int(run_id)
    starts = {}
    seen = set()
    for status in ("queued", "in_progress"):
        for run in list_runs(repo, status, get):
            other = run.get("id")
            if other is None or other == this or other in seen:
                continue
            seen.add(other)
            started = run_start(run)
            if started is not None and (started, other) < (run_started, this):
                starts[other] = started
    newest = sorted(starts, key=lambda other: (starts[other], other))[-CONCURRENT_RUNS_MAX:]
    concurrent = []
    for other in newest:
        jobs = get("/repos/%s/actions/runs/%s/jobs?filter=latest&per_page=100" % (repo, other)).get("jobs") or []
        for job in jobs:
            if job.get("name") != ROUTE_JOB_NAME:
                continue
            if job.get("status") in ("queued", "in_progress"):
                concurrent.append((other, None))
            elif job.get("status") == "completed":
                concurrent.append((other, parse_time(job.get("completed_at"))))
            break
    return concurrent


def parse_concurrent(text):
    """ROUTE_CONCURRENT: a JSON list of [run id, routing job completion epoch or null] pairs."""
    pairs = []
    for item in json.loads(text):
        if not isinstance(item, list) or len(item) != 2:
            raise ValueError("ROUTE_CONCURRENT entry %r is not a [run id, completion epoch or null] pair" % (item,))
        run, completed = item
        pairs.append((int(run), None if completed is None else int(completed)))
    return pairs


def replay_line(env, now, run_started, concurrent):
    parts = ["WINDOWS_RUNNER_MODE=%s" % json.dumps(env.get("WINDOWS_RUNNER_MODE") or ""),
             "WINDOWS_HOME_STATE=%s" % json.dumps(env.get("WINDOWS_HOME_STATE") or ""),
             "ROUTE_LANES=%s" % (env.get("ROUTE_LANES") or ""),
             "ROUTE_NOW=%d" % now, "ROUTE_RUN_STARTED=%d" % run_started,
             "ROUTE_CONCURRENT=%s" % json.dumps(json.dumps(concurrent)),
             "ROUTE_EVENT=%s" % (env.get("ROUTE_EVENT") or ""),
             "ROUTE_HEAD_REPO=%s" % (env.get("ROUTE_HEAD_REPO") or ""),
             "ROUTE_REPO=%s" % (env.get("ROUTE_REPO") or "")]
    # The bot rule's inputs, when the run carried them: without them a replay of a bot's run decides otherwise.
    for name in ("ROUTE_ACTOR", "ROUTE_TRIGGERING_ACTOR", "ROUTE_PR_AUTHOR", "ROUTE_HEAD_REF", "ROUTE_ALLOWLIST"):
        if env.get(name):
            parts.append("%s=%s" % (name, json.dumps(env[name])))
    return " ".join(parts) + " python3 ci/windows-runner/route/route.py"


def render_summary(summary, decisions, lanes, run_id, attempt, replay):
    lines = ["## Windows lanes: mode %s (run %s, attempt %s)" % (summary["mode"], run_id, attempt), ""]
    if summary["slots"] is not None:
        lines += ["| state age | serving | slots | running | queued | earlier routers | free slots | deadline |",
                  "|---|---|---|---|---|---|---|---|",
                  "| %s s | %s | %s | %s | %s | %s | %s | %s |" % (
                      summary["state_age"], summary["serving"], summary["slots"], summary["running"], summary["queued"],
                      "-" if summary["concurrent"] is None else summary["concurrent"],
                      "-" if summary["free"] is None else summary["free"],
                      "-" if summary["deadline"] is None else in_minutes(summary["deadline"])), ""]
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


def annotation_text(message):
    """A message escaped for a workflow command (::warning::, ::notice::): `%`
    first, then CR and LF, so a multi-line gh stderr stays one annotation."""
    return message.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")


def main(env=None, get=gh_api, clock=time.time):
    env = os.environ if env is None else env
    lanes = [lane.strip() for lane in (env.get("ROUTE_LANES") or "").split(",") if lane.strip()]
    attempt = (env.get("ROUTE_ATTEMPT") or "").strip() or "1"
    run_id = (env.get("ROUTE_RUN_ID") or "").strip() or "0"
    repo = env.get("ROUTE_REPO") or ""
    mode = env.get("WINDOWS_RUNNER_MODE")
    state_text = env.get("WINDOWS_HOME_STATE")
    now = _int_env(env, "ROUTE_NOW", int(clock()))
    run_started = now
    concurrent = []
    try:
        rule = dict(stale_seconds=_int_env(env, "ROUTE_STALE_SECONDS", DEFAULT_STALE_SECONDS),
                    slots_max=_int_env(env, "ROUTE_SLOTS_MAX", DEFAULT_SLOTS_MAX),
                    event=env.get("ROUTE_EVENT") or "", head_repo=env.get("ROUTE_HEAD_REPO") or "", repo=repo,
                    actors=(env.get("ROUTE_ACTOR") or "", env.get("ROUTE_TRIGGERING_ACTOR") or "",
                            env.get("ROUTE_PR_AUTHOR") or ""),
                    head_ref=env.get("ROUTE_HEAD_REF") or "",
                    actor_lists=load_actor_lists(env.get("ROUTE_ALLOWLIST") or DEFAULT_ALLOWLIST))
        # The fork rule, the mode and the state come first: the reads (or their replayed values) matter only when
        # occupancy decides, and the state parsed here is the one occupancy uses.
        decisions, summary, state = settle_early(mode, lanes, state_text, now, **rule)
        if state is not None:
            if (env.get("ROUTE_RUN_STARTED") or "").strip():
                run_started = _int_env(env, "ROUTE_RUN_STARTED", now)
            else:
                try:
                    run_started = fetch_run_started(repo, run_id, get)
                except Exception as failure:  # noqa: BLE001 - optional read; the clock stands in
                    print("::notice::%s" % annotation_text("run start time unavailable (%s); using now" % failure))
            if (env.get("ROUTE_CONCURRENT") or "").strip():
                concurrent = parse_concurrent(env["ROUTE_CONCURRENT"])
            else:
                try:
                    concurrent = fetch_concurrent(repo, run_id, run_started, get)
                except Exception as failure:  # noqa: BLE001 - optional read; assume no earlier router
                    print("::notice::%s" % annotation_text("earlier routers unknown (%s); assuming none" % failure))
            decisions, summary = decide_from_occupancy(lanes, state, now, concurrent, summary, DURATIONS)
    except Exception as failure:  # noqa: BLE001 - every failure routes to the vendor, never fails the run
        reason = "%s: %s; every lane goes to the vendor" % (type(failure).__name__, failure)
        print("::warning title=Windows lane router::%s" % annotation_text(reason))
        decisions = _everything(lanes, "vendor", reason)
        summary = _summary(normalize_mode(mode) or "(unset)")
        summary["reason"] = reason
    # The outputs first: a summary that cannot be written must not cost the pools.
    try:
        write_outputs(env, decisions, attempt)
    except OSError as failure:
        print("::warning title=Windows lane router::%s" % annotation_text(
            "outputs not written (%s); every routed lane takes the workflow's vendor fallback" % failure))
    text = render_summary(summary, decisions, lanes, run_id, attempt, replay_line(env, now, run_started, concurrent))
    print(text)
    step_summary = env.get("GITHUB_STEP_SUMMARY")
    if step_summary:
        try:
            with open(step_summary, "a", encoding="utf-8") as handle:
                handle.write(text)
        except OSError as failure:
            print("::warning title=Windows lane router::%s" % annotation_text("step summary not written (%s)" % failure))
    return 0


if __name__ == "__main__":
    sys.exit(main())
