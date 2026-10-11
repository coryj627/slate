# Windows Overflow Routing and Host Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A `windows.yml` lane runs on the home pool when a slot is free or when queueing costs the run no time, and on Namespace otherwise, decided per lane by a routing job on hosted Linux; the host prunes its logs, signals when the previous golden image can go, an elevated task deletes it, and a weekly workflow reports stale `claude/*` branches.

**Architecture:** `ci/windows-runner/route/route.py` (pure `decide`, API reads behind it, outputs and a summary) is the first job of `windows.yml`; every routed lane reads its `runs-on` from the router's outputs, trusting them only in the router's own attempt and falling back to today's vendor expression. The host module gains an admission check on the run's repository, log retention, and golden acceptance; `setup-host.ps1` registers an elevated prune task; `build-golden.ps1` rotates the previous image; `branch-cleanup.yml` reports and, on dispatch, deletes only what `main` contains.

**Tech Stack:** Python 3 (unittest, `gh api` via subprocess), GitHub Actions YAML, PowerShell 7 (Pester 5.9.1), the existing `SlateCiHost.psm1` adapter pattern.

**Spec:** `docs/superpowers/specs/2026-10-11-windows-overflow-routing-design.md` (revised after the red team; its §3 rule and §4 router are binding). It extends `docs/superpowers/specs/2026-10-10-self-hosted-windows-runner-design.md`.

## Global Constraints

- The router runs only on `ubuntu-latest`; no host, PAT or new secret is involved (spec §8). The host's PAT scope stays Actions read, Administration read and write.
- The router never fails a run: every exception becomes vendor labels plus a `::warning::` annotation; the routing step is `continue-on-error: true`; every routed lane has `if: ${{ !cancelled() }}` and a `runs-on` that falls back to today's vendor expression, verbatim (spec §5).
- Mode values: `home` (the rule), `home-only` (every lane home), `namespace` and anything else (every lane vendor, no API call). The mode is compared trimmed and case-insensitively; `github.ref` is compared exactly against `refs/heads/main` (spec §3).
- Admissible job = exactly one label matching `^slate-win-(rust|app|model|shell)$` case-insensitively, as the host's `Get-LaneFromLabels` decides; jobs of other repositories are excluded; queued jobs older than 7 200 s are stuck (spec §3).
- Dead host = an admissible, non-stuck queued job older than 300 s while in-progress admissible jobs are fewer than `ROUTE_SLOTS` (spec §3, D-4).
- Durations in minutes (spec §3): app home 25 / vendor 22; model home 20 / vendor 6; rust home 10 / vendor 2; shell vendor 13. Pool size 2.
- Vendor targets, verbatim: `namespace-profile-winx64-fast{suffix};overrides.cache-tag=slate-windows-{rust|app|model}` with `suffix` = `-pr` off `refs/heads/main`, empty on it; the shell gate is `windows-latest` in every mode but `home-only`.
- The orchestrator's account never gains a right on `golden`; deletion of the previous image is the elevated `slate-ci-golden-prune` task's (spec §6, D-7).
- The branch sweep deletes only on `workflow_dispatch` with `delete=true`, only `refs/heads/claude/*`, only tips contained in `main`, never the base of an open pull request (spec §9).
- File headers: every new `.py` and `.ps1` file starts with the two lines `# Copyright (C) 2026 Cory Joseph` and `# SPDX-License-Identifier: AGPL-3.0-or-later`; `.py` files put them after the shebang. Line endings LF.
- Pester runs through `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1`; the Python suite through `python -I -m unittest discover -v -s ci/windows-runner/route/tests -t ci/windows-runner/route`. Every task ends with both green.
- Commit messages follow the repository's `type(scope): sentence` style and end with `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.

## Review Focus

- A listing with more in-progress jobs than slots (a stale ghost after a host crash): the simulation must stay pessimistic, never negative, never index past the slot list — Task 2 pins it.
- A queued job whose `created_at` is missing or malformed: it must count as occupancy and stay out of both age tests — Task 1 pins it.
- A run attempt greater than 1 whose router output is reused: the lane must take the vendor expression, and the cache step must run — Task 5's guard pins the expression shape.
- A router step that throws before writing any output (for example `gh` missing): outputs are empty, lanes fall back, the run shows a warning — Task 3 pins `main()`'s fallback, Task 5 the `continue-on-error`.
- The golden acceptance when the golden is rebuilt while the journal remembers an older stamp: the lane set must reset and the removable signal must clear — Task 8 pins it.

## Rulings recorded while planning

- The spec's §5 lists "the aggregate gate's `needs`" as unchanged and §3 asks the gate's failure line to name the pool per lane. The gate cannot read the router's outputs without depending on it, so `gate` gains `route` in its `needs` (it already runs with `if: always()`, so nothing else changes). Cost if wrong: one extra edge in the graph.
- The spec's §3 second worked example describes a pool with both slots busy; Task 2's test states that scenario precisely (two in-progress jobs) rather than the example's shorthand.

---

### Task 1: The router's listing, admissibility and occupancy

**Files:**
- Create: `ci/windows-runner/route/route.py`
- Create: `ci/windows-runner/route/tests/__init__.py` (empty)
- Create: `ci/windows-runner/route/tests/test_route.py`
- Modify: `.github/workflows/windows-runner-tests.yml` (add the Python step)

**Interfaces:**
- Produces: `lane_of_labels(labels) -> str | None`, `parse_time(value) -> int | None`, `reduce_job(job, run_id, head_repo) -> dict`, `classify(jobs, now, repo, stale_seconds) -> dict` with keys `in_progress`, `queued`, `stuck` (lists of entries `{job_id, run_id, lane, created_at, started_at, age}`), `foreign`, `inadmissible`; `is_dead(occupancy, slots, dead_seconds) -> bool`; the constants `LANES`, `LANE_OF`, `HOME_LABELS`, `VENDOR_TAGS`, `VENDOR_PROFILE`, `MAIN_REF`, `COST_ORDER`, `DURATIONS`, `DEFAULT_SLOTS`, `DEFAULT_DEAD_SECONDS`, `DEFAULT_STALE_SECONDS`, `ROUTED_WORKFLOWS`, `KNOWN_LANES`, `API_TIMEOUT_SECONDS`, `TOTAL_TIMEOUT_SECONDS`.
- Consumed by Tasks 2 and 3.

- [ ] **Step 1: Write the failing tests**

Create `ci/windows-runner/route/tests/__init__.py` empty, and `ci/windows-runner/route/tests/test_route.py`:

```python
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
"""Tests for the Windows lane router (ci/windows-runner/route/route.py).

Run:  python -I -m unittest discover -v -s ci/windows-runner/route/tests -t ci/windows-runner/route
"""

import calendar
import json
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
import route  # noqa: E402

NOW = calendar.timegm((2026, 10, 11, 12, 0, 0, 0, 0, 0))  # 2026-10-11T12:00:00Z
REPO = "coryj627/slate"


def iso(epoch):
    return "%04d-%02d-%02dT%02d:%02d:%02dZ" % __import__("time").gmtime(epoch)[:6]


def job(job_id, lane, status, created=NOW - 60, started=None, run_id=1, head=REPO, labels=None):
    """A reduced listing entry as reduce_job writes it."""
    return {
        "job_id": job_id,
        "run_id": run_id,
        "head_repo": head,
        "status": status,
        "labels": labels if labels is not None else ["self-hosted", "slate-win-%s" % lane],
        "created_at": iso(created) if isinstance(created, int) else created,
        "started_at": iso(started) if isinstance(started, int) else started,
    }


class LaneOfLabels(unittest.TestCase):
    # Shared vectors with ci/windows-runner/tests/SlateCiHost.Tests.ps1
    # 'Get-LaneFromLabels': the router must admit exactly what the host admits.
    def test_one_known_label(self):
        self.assertEqual(route.lane_of_labels(["self-hosted", "slate-win-app"]), "app")

    def test_case_insensitive_like_github(self):
        self.assertEqual(route.lane_of_labels(["SLATE-WIN-APP"]), "app")

    def test_two_known_labels_is_not_ours(self):
        self.assertIsNone(route.lane_of_labels(["slate-win-app", "slate-win-rust"]))

    def test_unknown_lane_is_ignored(self):
        self.assertIsNone(route.lane_of_labels(["slate-win-x"]))
        self.assertEqual(route.lane_of_labels(["slate-win-app", "slate-win-x"]), "app")

    def test_no_label(self):
        self.assertIsNone(route.lane_of_labels([]))
        self.assertIsNone(route.lane_of_labels(["windows-latest"]))

    def test_shell_is_a_lane(self):
        self.assertEqual(route.lane_of_labels(["slate-win-shell"]), "shell")


class ParseTime(unittest.TestCase):
    def test_iso_utc(self):
        self.assertEqual(route.parse_time("2026-10-11T12:00:00Z"), NOW)

    def test_absent_or_garbage(self):
        for value in (None, "", "yesterday", 42):
            self.assertIsNone(route.parse_time(value))


class ReduceJob(unittest.TestCase):
    def test_keeps_only_the_fields_the_rule_reads(self):
        api_job = {"id": 7, "run_id": 99, "status": "queued", "labels": ["slate-win-app"], "created_at": "2026-10-11T12:00:00Z",
                   "started_at": "2026-10-11T12:00:00Z", "name": "app", "runner_name": None, "steps": []}
        self.assertEqual(route.reduce_job(api_job, 3, REPO), {
            "job_id": 7, "run_id": 3, "head_repo": REPO, "status": "queued", "labels": ["slate-win-app"],
            "created_at": "2026-10-11T12:00:00Z", "started_at": "2026-10-11T12:00:00Z"})

    def test_missing_labels_and_times(self):
        self.assertEqual(route.reduce_job({"id": 1, "status": "queued"}, 2, "")["labels"], [])
        self.assertIsNone(route.reduce_job({"id": 1, "status": "queued"}, 2, "")["created_at"])


class Classify(unittest.TestCase):
    def test_counts_queued_and_in_progress_admissible_jobs(self):
        occ = route.classify([job(1, "app", "in_progress", started=NOW - 300), job(2, "rust", "queued", created=NOW - 30)], NOW, REPO, 7200)
        self.assertEqual([e["job_id"] for e in occ["in_progress"]], [1])
        self.assertEqual([e["job_id"] for e in occ["queued"]], [2])
        self.assertEqual(occ["queued"][0]["age"], 30)
        self.assertEqual(occ["in_progress"][0]["started_at"], NOW - 300)

    def test_inadmissible_and_foreign_jobs_are_counted_but_not_occupancy(self):
        occ = route.classify([job(1, "x", "queued"), job(2, "app", "queued", head="someone/slate"),
                              job(3, "app", "queued", labels=["slate-win-app", "slate-win-rust"])], NOW, REPO, 7200)
        self.assertEqual(occ["queued"], [])
        self.assertEqual(occ["inadmissible"], 2)
        self.assertEqual(occ["foreign"], 1)

    def test_empty_head_repo_counts_as_own(self):
        # A replayed listing may carry no repository; it is the owner's own.
        occ = route.classify([job(1, "app", "queued", head="")], NOW, REPO, 7200)
        self.assertEqual(len(occ["queued"]), 1)

    def test_stuck_queued_job_is_listed_and_counted_nowhere(self):
        occ = route.classify([job(1, "app", "queued", created=NOW - 7201), job(2, "app", "queued", created=NOW - 7200)], NOW, REPO, 7200)
        self.assertEqual([e["job_id"] for e in occ["stuck"]], [1])
        self.assertEqual([e["job_id"] for e in occ["queued"]], [2])

    def test_malformed_created_at_is_occupancy_without_an_age(self):
        occ = route.classify([job(1, "app", "queued", created="soon")], NOW, REPO, 7200)
        self.assertEqual(len(occ["queued"]), 1)
        self.assertIsNone(occ["queued"][0]["age"])
        self.assertIsNone(occ["queued"][0]["created_at"])

    def test_completed_jobs_are_ignored(self):
        occ = route.classify([job(1, "app", "completed")], NOW, REPO, 7200)
        self.assertEqual(occ["queued"], [])
        self.assertEqual(occ["in_progress"], [])

    def test_own_run_is_counted_like_any_other(self):
        # M-1: a run's already-routed lanes genuinely hold or wait for slots.
        occ = route.classify([job(1, "model", "queued", run_id=5)], NOW, REPO, 7200)
        self.assertEqual(len(occ["queued"]), 1)


class IsDead(unittest.TestCase):
    def occ(self, queued_age, in_progress):
        return route.classify(
            [job(1, "app", "queued", created=NOW - queued_age)] + [job(10 + i, "app", "in_progress", started=NOW - 60) for i in range(in_progress)],
            NOW, REPO, 7200)

    def test_boundary_at_the_dead_window(self):
        self.assertFalse(route.is_dead(self.occ(299, 0), 2, 300))
        self.assertFalse(route.is_dead(self.occ(300, 0), 2, 300))
        self.assertTrue(route.is_dead(self.occ(301, 0), 2, 300))

    def test_one_wedged_slot_counts_as_dead(self):
        self.assertTrue(route.is_dead(self.occ(301, 1), 2, 300))

    def test_full_pool_is_alive(self):
        self.assertFalse(route.is_dead(self.occ(301, 2), 2, 300))

    def test_stuck_and_ageless_jobs_never_trip_it(self):
        occ = route.classify([job(1, "app", "queued", created=NOW - 7201), job(2, "app", "queued", created="soon")], NOW, REPO, 7200)
        self.assertFalse(route.is_dead(occ, 2, 300))

    def test_no_queue_is_alive(self):
        self.assertFalse(route.is_dead(route.classify([], NOW, REPO, 7200), 2, 300))


class Constants(unittest.TestCase):
    def test_routed_workflows_and_slots_match_the_host_config(self):
        path = os.path.join(os.path.dirname(__file__), "..", "..", "config.json")
        with open(path, encoding="utf-8") as handle:
            config = json.load(handle)
        self.assertEqual(list(route.ROUTED_WORKFLOWS), config["RoutedWorkflows"])
        self.assertEqual(route.DEFAULT_SLOTS, len(config["Slots"]))

    def test_vendor_strings_verbatim(self):
        self.assertEqual(route.vendor_label("rust", "refs/heads/main"), "namespace-profile-winx64-fast;overrides.cache-tag=slate-windows-rust")
        self.assertEqual(route.vendor_label("app", "refs/heads/feature"), "namespace-profile-winx64-fast-pr;overrides.cache-tag=slate-windows-app")
        self.assertEqual(route.vendor_label("model-0", "refs/pull/5/merge"), "namespace-profile-winx64-fast-pr;overrides.cache-tag=slate-windows-model")
        self.assertEqual(route.vendor_label("model-1", "refs/heads/MAIN"), "namespace-profile-winx64-fast-pr;overrides.cache-tag=slate-windows-model")

    def test_home_strings(self):
        self.assertEqual(route.home_label("rust"), "slate-win-rust")
        self.assertEqual(route.home_label("model-1"), "slate-win-model")


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `python -I -m unittest discover -v -s ci/windows-runner/route/tests -t ci/windows-runner/route`
Expected: `ModuleNotFoundError: No module named 'route'` (or an ImportError), every test erroring.

- [ ] **Step 3: Write the module's first half**

Create `ci/windows-runner/route/route.py`:

```python
#!/usr/bin/env python3
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
"""Pick where each windows.yml lane runs: the home pool or the vendor.

Spec: docs/superpowers/specs/2026-10-11-windows-overflow-routing-design.md
(§3 the rule, §4 this router). Runs as the first job of windows.yml on
hosted Linux and writes, per lane, one `runs-on` string and one pool, plus
the attempt it ran in, to $GITHUB_OUTPUT (or stdout when unset).

Inputs, from the environment:
  WINDOWS_RUNNER_MODE   home | home-only | namespace | anything else
  ROUTE_LANES           comma list of the lanes to route: rust,app,model-0,model-1
  ROUTE_REF             github.ref: -pr off refs/heads/main
  ROUTE_ATTEMPT         github.run_attempt, echoed as `attempt`
  ROUTE_RUN_ID          github.run_id, for the summary only
  ROUTE_EVENT, ROUTE_HEAD_REPO, ROUTE_REPO   the fork rule
  ROUTE_SLOTS           pool size (2)
  ROUTE_DEAD_SECONDS, ROUTE_STALE_SECONDS   300 and 7200
  ROUTE_NOW             epoch seconds (tests)
  ROUTE_JOBS_JSON       a reduced listing file instead of the API (tests, replay)
  GH_TOKEN, GITHUB_REPOSITORY, GITHUB_OUTPUT, GITHUB_STEP_SUMMARY

This is routing, not security: a fork runs its own copy of this file, so
the host's admission check and the approval policy carry the trust (§8).
"""

import calendar
import json
import os
import re
import subprocess
import sys
import time

LANES = ("rust", "app", "model-0", "model-1")
LANE_OF = {"rust": "rust", "app": "app", "model-0": "model", "model-1": "model"}
KNOWN_LANES = ("rust", "app", "model", "shell")
HOME_LABELS = {"rust": "slate-win-rust", "app": "slate-win-app", "model": "slate-win-model", "shell": "slate-win-shell"}
VENDOR_TAGS = {"rust": "slate-windows-rust", "app": "slate-windows-app", "model": "slate-windows-model"}
VENDOR_PROFILE = "namespace-profile-winx64-fast{suffix};overrides.cache-tag={tag}"
MAIN_REF = "refs/heads/main"
# Vendor cost order (spec D-3): the free slots go to the front of it.
COST_ORDER = ("app", "model-0", "model-1", "rust")
# Minutes, measured 2026-10-10/11 (runbook "Observed timings"): `home` is
# the pool's figure, `vendor` Namespace's, or windows-latest's for the
# shell gate. Revise as the warm-cache figures come in.
DURATIONS = {
    "app": {"home": 25, "vendor": 22},
    "model": {"home": 20, "vendor": 6},
    "rust": {"home": 10, "vendor": 2},
    "shell": {"home": 16, "vendor": 13},
}
DEFAULT_SLOTS = 2
DEFAULT_DEAD_SECONDS = 300
DEFAULT_STALE_SECONDS = 7200
# The host's config.json RoutedWorkflows, verbatim (a test checks).
ROUTED_WORKFLOWS = (
    ".github/workflows/windows.yml",
    ".github/workflows/nightly.yml",
    ".github/workflows/windows-ci-pilot.yml",
)
API_TIMEOUT_SECONDS = 15
TOTAL_TIMEOUT_SECONDS = 60
_LABEL = re.compile(r"^slate-win-([a-z]+)$", re.IGNORECASE)


def lane_of_labels(labels):
    """The host's Get-LaneFromLabels: exactly one known lane label, lower-cased; else None."""
    found = []
    for label in labels or ():
        match = _LABEL.match(str(label))
        if match:
            lane = match.group(1).lower()
            if lane in KNOWN_LANES:
                found.append(lane)
    return found[0] if len(found) == 1 else None


def parse_time(value):
    """An ISO 8601 UTC timestamp as epoch seconds, or None when absent or malformed."""
    if not isinstance(value, str):
        return None
    for fmt in ("%Y-%m-%dT%H:%M:%SZ", "%Y-%m-%dT%H:%M:%S+00:00"):
        try:
            return calendar.timegm(time.strptime(value.strip(), fmt))
        except ValueError:
            continue
    return None


def home_label(lane):
    return HOME_LABELS[LANE_OF[lane]]


def vendor_label(lane, ref):
    suffix = "" if ref == MAIN_REF else "-pr"
    return VENDOR_PROFILE.format(suffix=suffix, tag=VENDOR_TAGS[LANE_OF[lane]])


def reduce_job(api_job, run_id, head_repo):
    """The six fields the rule reads, nothing else (the listing is logged)."""
    return {
        "job_id": api_job.get("id"),
        "run_id": run_id,
        "head_repo": head_repo or "",
        "status": api_job.get("status"),
        "labels": list(api_job.get("labels") or []),
        "created_at": api_job.get("created_at"),
        "started_at": api_job.get("started_at"),
    }


def classify(jobs, now, repo, stale_seconds):
    """Occupancy from a reduced listing: admissible, own-repository jobs that are queued or in progress."""
    occupancy = {"in_progress": [], "queued": [], "stuck": [], "foreign": 0, "inadmissible": 0}
    for entry in jobs:
        lane = lane_of_labels(entry.get("labels"))
        if lane is None:
            occupancy["inadmissible"] += 1
            continue
        head = entry.get("head_repo") or ""
        if head and head != repo:
            occupancy["foreign"] += 1
            continue
        created = parse_time(entry.get("created_at"))
        started = parse_time(entry.get("started_at"))
        age = None if created is None else now - created
        reduced = {"job_id": entry.get("job_id"), "run_id": entry.get("run_id"), "lane": lane,
                   "created_at": created, "started_at": started, "age": age}
        status = entry.get("status")
        if status == "in_progress":
            occupancy["in_progress"].append(reduced)
        elif status == "queued":
            if age is not None and age > stale_seconds:
                occupancy["stuck"].append(reduced)
            else:
                occupancy["queued"].append(reduced)
    return occupancy


def is_dead(occupancy, slots, dead_seconds):
    """D-4: an admissible job queued past the window while fewer jobs run than there are slots."""
    if len(occupancy["in_progress"]) >= slots:
        return False
    return any(entry["age"] is not None and entry["age"] > dead_seconds for entry in occupancy["queued"])
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `python -I -m unittest discover -v -s ci/windows-runner/route/tests -t ci/windows-runner/route`
Expected: every test in `LaneOfLabels`, `ParseTime`, `ReduceJob`, `Classify`, `IsDead` and `Constants` passes (`OK`).

- [ ] **Step 5: Run the router's tests in CI**

In `.github/workflows/windows-runner-tests.yml`, after the `Run the suite` step, add:

```yaml
      - name: Run the router's tests
        # The Windows lane router (ci/windows-runner/route/route.py) is
        # Python, like the mac runner's; same discover shape as
        # mac-runner-tests.yml.
        run: python -I -m unittest discover -v -s ci/windows-runner/route/tests -t ci/windows-runner/route
```

(`ci/windows-runner/**` is already in both of that workflow's path filters.)

- [ ] **Step 6: Commit**

```bash
git add ci/windows-runner/route .github/workflows/windows-runner-tests.yml
git commit -m "feat(ci-route): the Windows lane router's listing, admissibility and occupancy

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: The simulation, the critical-path allocation and `decide`

**Files:**
- Modify: `ci/windows-runner/route/route.py` (append after `is_dead`)
- Modify: `ci/windows-runner/route/tests/test_route.py` (append)

**Interfaces:**
- Consumes: Task 1's `classify`, `is_dead`, `home_label`, `vendor_label`, `DURATIONS`, `COST_ORDER`, `LANE_OF`.
- Produces: `minutes(m) -> int` seconds; `simulate(occupancy, now, slots) -> list[int]` (seconds from `now` at which each slot frees, length `slots`); `allocate(lanes, free_at, ref) -> (decisions, deadline)` where `decisions` maps lane to `{"pool": "home"|"vendor", "label": str, "reason": str}` and `deadline` is seconds from now; `decide(mode, lanes, jobs, now, *, slots, dead_seconds, stale_seconds, ref, event, head_repo, repo) -> (decisions, summary)` where `summary` is a dict with `mode`, `in_progress`, `queued`, `stuck`, `foreign`, `inadmissible`, `oldest_queued_age`, `dead`, `free`, `deadline`, `reason`.
- Consumed by Task 3.

- [ ] **Step 1: Write the failing tests**

Append to `ci/windows-runner/route/tests/test_route.py` (before the `if __name__` block):

```python
class Simulate(unittest.TestCase):
    def test_idle_pool(self):
        self.assertEqual(route.simulate(route.classify([], NOW, REPO, 7200), NOW, 2), [0, 0])

    def test_in_progress_jobs_run_to_their_home_duration(self):
        # The app lane's home duration is 25 min; 10 min in, 15 remain.
        occ = route.classify([job(1, "app", "in_progress", started=NOW - 600)], NOW, REPO, 7200)
        self.assertEqual(route.simulate(occ, NOW, 2), [0, 15 * 60])

    def test_overrun_job_frees_now_not_in_the_past(self):
        occ = route.classify([job(1, "rust", "in_progress", started=NOW - 3600)], NOW, REPO, 7200)
        self.assertEqual(route.simulate(occ, NOW, 2), [0, 0])

    def test_in_progress_without_a_start_time_runs_a_full_duration(self):
        occ = route.classify([job(1, "rust", "in_progress", started=None)], NOW, REPO, 7200)
        self.assertEqual(route.simulate(occ, NOW, 2), [0, 10 * 60])

    def test_queued_jobs_take_slots_oldest_first(self):
        occ = route.classify([job(1, "app", "in_progress", started=NOW - 900), job(2, "app", "in_progress", started=NOW - 600),
                              job(3, "rust", "queued", created=NOW - 50), job(4, "model", "queued", created=NOW - 40)], NOW, REPO, 7200)
        # Slots free at 10 and 15 min; rust (oldest) takes the 10 and frees it
        # at 20, model takes the 15 and frees it at 35. Sorted ascending.
        self.assertEqual(route.simulate(occ, NOW, 2), [20 * 60, 35 * 60])

    def test_more_running_than_slots_is_pessimistic_and_never_negative(self):
        occ = route.classify([job(i, "app", "in_progress", started=NOW - 60 * i) for i in (1, 2, 3)], NOW, REPO, 7200)
        free = route.simulate(occ, NOW, 2)
        self.assertEqual(len(free), 2)
        self.assertEqual(free, [23 * 60, 24 * 60])
        self.assertTrue(all(f >= 0 for f in free))

    def test_ageless_queued_job_still_takes_a_slot(self):
        occ = route.classify([job(1, "rust", "queued", created="soon")], NOW, REPO, 7200)
        self.assertEqual(route.simulate(occ, NOW, 2), [0, 10 * 60])


class Allocate(unittest.TestCase):
    LANES = ["rust", "app", "model-0", "model-1"]

    def pools(self, decisions):
        return {lane: decisions[lane]["pool"] for lane in decisions}

    def test_idle_pool_cold_constants(self):
        # Spec §3 worked example: app and model-0 take the slots, model-1 would
        # finish at 40 min against a 38 min deadline, rust fits behind model-0.
        decisions, deadline = route.allocate(self.LANES, [0, 0], "refs/heads/main")
        self.assertEqual(self.pools(decisions), {"app": "home", "model-0": "home", "model-1": "vendor", "rust": "home"})
        self.assertEqual(deadline, (25 + 13) * 60)
        self.assertEqual(decisions["app"]["label"], "slate-win-app")
        self.assertEqual(decisions["model-1"]["label"], "namespace-profile-winx64-fast;overrides.cache-tag=slate-windows-model")

    def test_both_slots_busy_app_never_queues(self):
        # Two in-progress jobs, free at 15 and 20 min: app goes to the vendor
        # (deadline 22 + 13 = 35); model-0 starts at 15 and finishes at 35, home;
        # model-1 starts at 20 and finishes at 40, vendor; rust takes the slot
        # model-1 left, 20 to 30, home.
        decisions, deadline = route.allocate(self.LANES, [15 * 60, 20 * 60], "refs/heads/feature")
        self.assertEqual(self.pools(decisions), {"app": "vendor", "model-0": "home", "model-1": "vendor", "rust": "home"})
        self.assertEqual(deadline, (22 + 13) * 60)
        self.assertEqual(decisions["app"]["label"], "namespace-profile-winx64-fast-pr;overrides.cache-tag=slate-windows-app")

    def test_deadline_boundary_is_inclusive(self):
        # One slot frees at exactly deadline - model duration: model-0 fits.
        decisions, deadline = route.allocate(["app", "model-0"], [0, (25 + 13 - 20) * 60], "refs/heads/main")
        self.assertEqual(decisions["model-0"]["pool"], "home")
        decisions, _ = route.allocate(["app", "model-0"], [0, (25 + 13 - 20) * 60 + 1], "refs/heads/main")
        self.assertEqual(decisions["model-0"]["pool"], "vendor")

    def test_lanes_are_placed_in_cost_order_whatever_the_input_order(self):
        decisions, _ = route.allocate(["rust", "model-1", "model-0", "app"], [0, 0], "refs/heads/main")
        self.assertEqual(self.pools(decisions), {"app": "home", "model-0": "home", "model-1": "vendor", "rust": "home"})

    def test_without_the_app_lane_the_deadline_is_the_vendor_critical_path(self):
        decisions, deadline = route.allocate(["rust"], [0, 0], "refs/heads/main")
        self.assertEqual(deadline, (22 + 13) * 60)
        self.assertEqual(decisions["rust"]["pool"], "home")

    def test_reasons_name_the_numbers(self):
        decisions, _ = route.allocate(self.LANES, [0, 0], "refs/heads/main")
        self.assertIn("free slot", decisions["app"]["reason"])
        self.assertIn("finish at 40 min", decisions["model-1"]["reason"])
        self.assertIn("deadline 38 min", decisions["model-1"]["reason"])
        self.assertIn("finishes at 30 min", decisions["rust"]["reason"])


class Decide(unittest.TestCase):
    LANES = ["rust", "app", "model-0", "model-1"]

    def decide(self, mode, jobs, **overrides):
        kwargs = dict(slots=2, dead_seconds=300, stale_seconds=7200, ref="refs/heads/main",
                      event="push", head_repo="", repo=REPO)
        kwargs.update(overrides)
        return route.decide(mode, self.LANES, jobs, NOW, **kwargs)

    def pools(self, decisions):
        return {lane: decisions[lane]["pool"] for lane in decisions}

    def test_home_idle_pool(self):
        decisions, summary = self.decide("home", [])
        self.assertEqual(self.pools(decisions), {"app": "home", "model-0": "home", "model-1": "vendor", "rust": "home"})
        self.assertEqual(summary["free"], 2)
        self.assertFalse(summary["dead"])
        self.assertEqual(summary["deadline"], (25 + 13) * 60)

    def test_home_dead_host_routes_everything_away(self):
        decisions, summary = self.decide("home", [job(1, "app", "queued", created=NOW - 301)])
        self.assertEqual(set(self.pools(decisions).values()), {"vendor"})
        self.assertTrue(summary["dead"])
        self.assertEqual(summary["oldest_queued_age"], 301)

    def test_home_with_a_full_pool_queues_only_inside_the_deadline(self):
        jobs = [job(1, "app", "in_progress", started=NOW - 10 * 60), job(2, "model", "in_progress", started=NOW - 5 * 60)]
        decisions, summary = self.decide("home", jobs)
        # Slots free at 15 (app) and 15 (model): app vendor, deadline 35;
        # model-0 15..35 home; model-1 15..35 home; rust 35..45 vendor.
        self.assertEqual(self.pools(decisions), {"app": "vendor", "model-0": "home", "model-1": "home", "rust": "vendor"})
        self.assertEqual(summary["free"], 0)

    def test_home_only_queues_every_lane_at_home(self):
        decisions, summary = self.decide("home-only", [job(1, "app", "queued", created=NOW - 301)])
        self.assertEqual(set(self.pools(decisions).values()), {"home"})
        self.assertEqual(summary["reason"], "WINDOWS_RUNNER_MODE=home-only")

    def test_namespace_and_unknown_modes_route_to_the_vendor_without_reading_the_queue(self):
        for mode in ("namespace", "", None, "beta", " NAMESPACE "):
            decisions, summary = self.decide(mode, [job(1, "app", "in_progress", started=NOW)])
            self.assertEqual(set(self.pools(decisions).values()), {"vendor"}, mode)
            self.assertIsNone(summary["in_progress"], mode)

    def test_mode_is_trimmed_and_case_insensitive(self):
        decisions, _ = self.decide(" Home ", [])
        self.assertEqual(decisions["app"]["pool"], "home")
        decisions, _ = self.decide("HOME-ONLY", [])
        self.assertEqual(decisions["model-1"]["pool"], "home")

    def test_fork_pull_request_routes_to_the_vendor(self):
        decisions, summary = self.decide("home", [], event="pull_request", head_repo="someone/slate")
        self.assertEqual(set(self.pools(decisions).values()), {"vendor"})
        self.assertIn("fork", summary["reason"])

    def test_same_repository_pull_request_is_routed(self):
        decisions, _ = self.decide("home", [], event="pull_request", head_repo=REPO, ref="refs/pull/7/merge")
        self.assertEqual(decisions["app"]["pool"], "home")
        self.assertEqual(decisions["model-1"]["label"], "namespace-profile-winx64-fast-pr;overrides.cache-tag=slate-windows-model")

    def test_foreign_and_stuck_jobs_do_not_hold_the_pool(self):
        jobs = [job(1, "app", "queued", created=NOW - 400, head="someone/slate"), job(2, "app", "queued", created=NOW - 8000)]
        decisions, summary = self.decide("home", jobs)
        self.assertEqual(decisions["app"]["pool"], "home")
        self.assertFalse(summary["dead"])
        self.assertEqual(summary["stuck"], 1)
        self.assertEqual(summary["foreign"], 1)

    def test_single_lane_list(self):
        decisions, _ = route.decide("home", ["rust"], [], NOW, slots=2, dead_seconds=300, stale_seconds=7200,
                                    ref="refs/heads/main", event="push", head_repo="", repo=REPO)
        self.assertEqual(list(decisions), ["rust"])
        self.assertEqual(decisions["rust"]["pool"], "home")
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `python -I -m unittest discover -v -s ci/windows-runner/route/tests -t ci/windows-runner/route`
Expected: `AttributeError: module 'route' has no attribute 'simulate'` (and `allocate`, `decide`); Task 1's tests still pass.

- [ ] **Step 3: Write the rule**

Append to `ci/windows-runner/route/route.py` after `is_dead`:

```python
def minutes(value):
    return int(value) * 60


def simulate(occupancy, now, slots):
    """Seconds from now at which each slot frees.

    An in-progress job runs to its lane's home duration from its start (never
    earlier than now); more running jobs than slots keeps the latest-ending
    ones (the pessimistic view of a stale ghost); then the queued jobs take
    slots oldest first, FIFO across runs.
    """
    busy = []
    for entry in occupancy["in_progress"]:
        duration = minutes(DURATIONS[entry["lane"]]["home"])
        if entry["started_at"] is None:
            remaining = duration
        else:
            remaining = entry["started_at"] + duration - now
        busy.append(max(0, remaining))
    busy.sort()
    if len(busy) > slots:
        busy = busy[-slots:]
    while len(busy) < slots:
        busy.insert(0, 0)
    for entry in sorted(occupancy["queued"], key=lambda e: (e["created_at"] or 0, e["job_id"] or 0)):
        busy.sort()
        busy[0] += minutes(DURATIONS[entry["lane"]]["home"])
    return sorted(busy)


def allocate(lanes, free_at, ref):
    """D-3: the app lane takes a free slot or the vendor, never a queue; every
    other lane, in cost order, queues at home only when the simulated schedule
    finishes it inside the run's deadline (the app lane plus the shell gate)."""
    slots = sorted(free_at)
    decisions = {}
    wanted = [lane for lane in COST_ORDER if lane in lanes]
    if "app" in wanted:
        if slots and slots[0] <= 0:
            app_finish = minutes(DURATIONS["app"]["home"])
            slots[0] = app_finish
            decisions["app"] = {"pool": "home", "label": home_label("app"), "reason": "free slot"}
        else:
            app_finish = minutes(DURATIONS["app"]["vendor"])
            decisions["app"] = {"pool": "vendor", "label": vendor_label("app", ref),
                                "reason": "no free slot and the app lane never queues"}
    else:
        app_finish = minutes(DURATIONS["app"]["vendor"])
    deadline = app_finish + minutes(DURATIONS["shell"]["vendor"])
    for lane in wanted:
        if lane == "app":
            continue
        slots.sort()
        start = max(0, slots[0]) if slots else 0
        finish = start + minutes(DURATIONS[LANE_OF[lane]]["home"])
        if finish <= deadline:
            if slots:
                slots[0] = finish
            what = "free slot" if start == 0 else "queues behind the pool"
            decisions[lane] = {"pool": "home", "label": home_label(lane),
                               "reason": "%s, finishes at %d min, deadline %d min" % (what, finish // 60, deadline // 60)}
        else:
            decisions[lane] = {"pool": "vendor", "label": vendor_label(lane, ref),
                               "reason": "would start at %d min and finish at %d min, deadline %d min" % (start // 60, finish // 60, deadline // 60)}
    ordered = {lane: decisions[lane] for lane in lanes if lane in decisions}
    return ordered, deadline


def _everything(lanes, pool, ref, reason):
    return {lane: {"pool": pool, "label": home_label(lane) if pool == "home" else vendor_label(lane, ref), "reason": reason}
            for lane in lanes}


def decide(mode, lanes, jobs, now, *, slots, dead_seconds, stale_seconds, ref, event, head_repo, repo):
    """The whole rule (spec §3) as a pure function of a reduced listing."""
    lanes = [lane for lane in lanes if lane in LANE_OF]
    normalized = (mode or "").strip().lower()
    summary = {"mode": normalized or "(unset)", "in_progress": None, "queued": None, "stuck": None, "foreign": None,
               "inadmissible": None, "oldest_queued_age": None, "dead": False, "free": None, "deadline": None, "reason": ""}
    if normalized == "home-only":
        summary["reason"] = "WINDOWS_RUNNER_MODE=home-only"
        return _everything(lanes, "home", ref, summary["reason"]), summary
    if normalized != "home":
        summary["reason"] = "WINDOWS_RUNNER_MODE=%s routes every lane to the vendor" % summary["mode"]
        return _everything(lanes, "vendor", ref, summary["reason"]), summary
    if event == "pull_request" and head_repo and head_repo != repo:
        summary["reason"] = "fork pull request from %s: the vendor, whatever the pool says" % head_repo
        return _everything(lanes, "vendor", ref, summary["reason"]), summary
    occupancy = classify(jobs, now, repo, stale_seconds)
    ages = [e["age"] for e in occupancy["queued"] if e["age"] is not None]
    summary.update({"in_progress": len(occupancy["in_progress"]), "queued": len(occupancy["queued"]),
                    "stuck": len(occupancy["stuck"]), "foreign": occupancy["foreign"],
                    "inadmissible": occupancy["inadmissible"], "oldest_queued_age": max(ages) if ages else None})
    summary["free"] = max(0, slots - len(occupancy["in_progress"]) - len(occupancy["queued"]))
    if is_dead(occupancy, slots, dead_seconds):
        summary["dead"] = True
        summary["reason"] = "an admissible job has waited more than %d s with %d of %d slots serving: the pool cannot serve" % (
            dead_seconds, len(occupancy["in_progress"]), slots)
        return _everything(lanes, "vendor", ref, summary["reason"]), summary
    decisions, deadline = allocate(lanes, simulate(occupancy, now, slots), ref)
    summary["deadline"] = deadline
    summary["reason"] = "home first; a lane queues only inside the deadline"
    return decisions, summary
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `python -I -m unittest discover -v -s ci/windows-runner/route/tests -t ci/windows-runner/route`
Expected: `OK`, every class including `Simulate`, `Allocate` and `Decide`.

- [ ] **Step 5: Commit**

```bash
git add ci/windows-runner/route
git commit -m "feat(ci-route): the critical-path allocation and the decide rule

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 3: `main()`: the API reads, the outputs, the summary and the fallback

**Files:**
- Modify: `ci/windows-runner/route/route.py` (append after `decide`)
- Modify: `ci/windows-runner/route/tests/test_route.py` (append)

**Interfaces:**
- Consumes: Task 2's `decide`, Task 1's `reduce_job`, `ROUTED_WORKFLOWS`, `API_TIMEOUT_SECONDS`, `TOTAL_TIMEOUT_SECONDS`.
- Produces: `gh_api(path, timeout) -> object` (subprocess; replaced in tests), `paginate(path, key, deadline) -> list`, `fetch_listing(repo, deadline) -> list[dict]` (reduced jobs), `load_listing(path) -> list[dict]`, `render_summary(summary, decisions, lanes, jobs, run_id, attempt) -> str` (Markdown), `write_outputs(env, decisions, attempt)`, `main(env=None) -> int` (always 0).
- Consumed by Task 5 (the workflow runs `python3 ci/windows-runner/route/route.py`).

- [ ] **Step 1: Write the failing tests**

Append to `ci/windows-runner/route/tests/test_route.py` (before the `if __name__` block):

```python
class Paginate(unittest.TestCase):
    def test_follows_pages_until_a_short_one(self):
        calls = []

        def fake(path, timeout):
            calls.append(path)
            page = int(path.rsplit("page=", 1)[1])
            items = [{"id": i} for i in range(100)] if page == 1 else [{"id": 100}]
            return {"workflow_runs": items}

        original = route.gh_api
        route.gh_api = fake
        try:
            items = route.paginate("/repos/x/y/actions/workflows/windows.yml/runs?status=queued&per_page=100", "workflow_runs", NOW + 60)
        finally:
            route.gh_api = original
        self.assertEqual(len(items), 101)
        self.assertEqual(calls, ["/repos/x/y/actions/workflows/windows.yml/runs?status=queued&per_page=100&page=1",
                                 "/repos/x/y/actions/workflows/windows.yml/runs?status=queued&per_page=100&page=2"])

    def test_total_deadline_stops_the_reads(self):
        route_now = route.time.time
        try:
            route.time.time = lambda: NOW + 61
            with self.assertRaises(TimeoutError):
                route.paginate("/x?per_page=100", "jobs", NOW + 60)
        finally:
            route.time.time = route_now


class FetchListing(unittest.TestCase):
    def test_reads_both_run_states_of_every_routed_workflow_once_and_reduces_the_jobs(self):
        seen = []

        def fake(path, timeout):
            seen.append(path)
            if "/runs?" in path and "/runs/" not in path:
                if "windows.yml" in path and "status=queued" in path:
                    return {"workflow_runs": [{"id": 5, "head_repository": {"full_name": REPO}}]}
                if "windows.yml" in path and "status=in_progress" in path:
                    return {"workflow_runs": [{"id": 5, "head_repository": {"full_name": REPO}}, {"id": 6, "head_repository": None}]}
                return {"workflow_runs": []}
            if path.startswith("/repos/%s/actions/runs/5/jobs" % REPO):
                return {"jobs": [{"id": 50, "status": "queued", "labels": ["slate-win-app"], "created_at": iso(NOW)}]}
            if path.startswith("/repos/%s/actions/runs/6/jobs" % REPO):
                return {"jobs": [{"id": 60, "status": "completed", "labels": ["slate-win-rust"], "created_at": iso(NOW)}]}
            raise AssertionError(path)

        original = route.gh_api
        route.gh_api = fake
        try:
            jobs = route.fetch_listing(REPO, NOW + 60)
        finally:
            route.gh_api = original
        self.assertEqual([j["job_id"] for j in jobs], [50, 60])
        self.assertEqual(jobs[0]["head_repo"], REPO)
        self.assertEqual(jobs[1]["head_repo"], "")
        self.assertEqual(sum(1 for p in seen if "/runs/5/jobs" in p), 1)
        self.assertEqual(sum(1 for p in seen if "/runs?" in p and "page=1" in p), 6)


class Main(unittest.TestCase):
    def env(self, mode, listing=None, **extra):
        env = {"WINDOWS_RUNNER_MODE": mode, "ROUTE_LANES": "rust,app,model-0,model-1", "ROUTE_REF": "refs/heads/main",
               "ROUTE_ATTEMPT": "1", "ROUTE_RUN_ID": "42", "ROUTE_EVENT": "push", "ROUTE_HEAD_REPO": "", "ROUTE_REPO": REPO,
               "GITHUB_REPOSITORY": REPO, "ROUTE_NOW": str(NOW)}
        if listing is not None:
            fd, path = tempfile.mkstemp(suffix=".json")
            with os.fdopen(fd, "w", encoding="utf-8") as handle:
                json.dump(listing, handle)
            env["ROUTE_JOBS_JSON"] = path
        env.update(extra)
        return env

    def run_main(self, env):
        fd, out = tempfile.mkstemp()
        os.close(fd)
        fd, summary = tempfile.mkstemp()
        os.close(fd)
        env["GITHUB_OUTPUT"] = out
        env["GITHUB_STEP_SUMMARY"] = summary
        try:
            code = route.main(env)
            with open(out, encoding="utf-8") as handle:
                outputs = dict(line.rstrip("\n").split("=", 1) for line in handle if "=" in line)
            with open(summary, encoding="utf-8") as handle:
                text = handle.read()
        finally:
            os.remove(out)
            os.remove(summary)
            if env.get("ROUTE_JOBS_JSON"):
                os.remove(env["ROUTE_JOBS_JSON"])
        return code, outputs, text

    def test_home_idle_pool_writes_every_output_and_the_attempt(self):
        code, outputs, text = self.run_main(self.env("home", []))
        self.assertEqual(code, 0)
        self.assertEqual(outputs["app"], "slate-win-app")
        self.assertEqual(outputs["app-pool"], "home")
        self.assertEqual(outputs["model-1"], "namespace-profile-winx64-fast;overrides.cache-tag=slate-windows-model")
        self.assertEqual(outputs["model-1-pool"], "vendor")
        self.assertEqual(outputs["rust-pool"], "home")
        self.assertEqual(outputs["attempt"], "1")
        self.assertIn("| app | home | slate-win-app |", text)
        self.assertIn("run 42", text)
        self.assertNotIn("dead", text.splitlines()[0])

    def test_namespace_mode_makes_no_api_call(self):
        original = route.gh_api
        route.gh_api = lambda path, timeout: (_ for _ in ()).throw(AssertionError("no API call in namespace mode"))
        try:
            code, outputs, _ = self.run_main(self.env("namespace"))
        finally:
            route.gh_api = original
        self.assertEqual(code, 0)
        self.assertEqual(outputs["app-pool"], "vendor")
        self.assertEqual(outputs["rust"], "namespace-profile-winx64-fast;overrides.cache-tag=slate-windows-rust")

    def test_an_api_failure_falls_back_to_the_vendor_with_a_warning(self):
        original = route.gh_api
        route.gh_api = lambda path, timeout: (_ for _ in ()).throw(RuntimeError("boom"))
        import io
        from contextlib import redirect_stdout
        buffer = io.StringIO()
        try:
            with redirect_stdout(buffer):
                code, outputs, text = self.run_main(self.env("home"))
        finally:
            route.gh_api = original
        self.assertEqual(code, 0)
        self.assertEqual({outputs[l + "-pool"] for l in ("rust", "app", "model-0", "model-1")}, {"vendor"})
        self.assertIn("::warning::", buffer.getvalue())
        self.assertIn("RuntimeError", text)

    def test_replayed_listing_is_echoed_in_the_summary(self):
        listing = [{"job_id": 9, "run_id": 3, "head_repo": REPO, "status": "in_progress", "labels": ["slate-win-app"],
                    "created_at": iso(NOW - 120), "started_at": iso(NOW - 60)}]
        code, outputs, text = self.run_main(self.env("home", listing))
        self.assertEqual(outputs["app-pool"], "home")
        self.assertIn("| 9 | 3 | in_progress | slate-win-app |", text)

    def test_malformed_numbers_fall_back_to_defaults(self):
        code, outputs, _ = self.run_main(self.env("home", [], ROUTE_SLOTS="two", ROUTE_DEAD_SECONDS="", ROUTE_NOW="later"))
        self.assertEqual(code, 0)
        self.assertEqual(outputs["app-pool"], "home")
        self.assertEqual(route._int_env({"X": "abc"}, "X", 7), 7)
        self.assertEqual(route._int_env({"X": " 42 "}, "X", 7), 42)

    def test_no_output_file_prints_the_outputs(self):
        env = self.env("namespace")
        import io
        from contextlib import redirect_stdout
        buffer = io.StringIO()
        with redirect_stdout(buffer):
            self.assertEqual(route.main(env), 0)
        self.assertIn("app-pool=vendor", buffer.getvalue())
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `python -I -m unittest discover -v -s ci/windows-runner/route/tests -t ci/windows-runner/route`
Expected: `AttributeError: module 'route' has no attribute 'gh_api'` (and `paginate`, `fetch_listing`, `main`, `_int_env`); earlier classes still pass.

- [ ] **Step 3: Write the I/O half**

Append to `ci/windows-runner/route/route.py` after `decide`:

```python
def gh_api(path, timeout):
    """One GET through gh (present on hosted Linux; GH_TOKEN from the step's env)."""
    completed = subprocess.run(["gh", "api", path], capture_output=True, text=True, timeout=timeout, check=True)
    return json.loads(completed.stdout)


def paginate(path, key, deadline):
    """Every item under `key` across pages of 100, within the total deadline."""
    items = []
    page = 1
    while True:
        if time.time() > deadline:
            raise TimeoutError("the API reads passed %d s" % TOTAL_TIMEOUT_SECONDS)
        body = gh_api("%s&page=%d" % (path, page), API_TIMEOUT_SECONDS)
        batch = list(body.get(key) or [])
        items.extend(batch)
        if len(batch) < 100:
            return items
        page += 1


def fetch_listing(repo, deadline):
    """The reduced jobs of every queued or in-progress run of the routed workflows.

    Both run states are read: a run's status does not track its jobs' (a run
    listed as queued held two in-progress jobs live). A run in both lists is
    read once.
    """
    jobs = []
    seen = set()
    for workflow in ROUTED_WORKFLOWS:
        file = workflow.rsplit("/", 1)[-1]
        for status in ("queued", "in_progress"):
            runs = paginate("/repos/%s/actions/workflows/%s/runs?status=%s&per_page=100" % (repo, file, status), "workflow_runs", deadline)
            for run in runs:
                run_id = run.get("id")
                if run_id in seen:
                    continue
                seen.add(run_id)
                head = ((run.get("head_repository") or {}).get("full_name")) or ""
                for api_job in paginate("/repos/%s/actions/runs/%s/jobs?filter=latest&per_page=100" % (repo, run_id), "jobs", deadline):
                    jobs.append(reduce_job(api_job, run_id, head))
    return jobs


def load_listing(path):
    with open(path, encoding="utf-8") as handle:
        return list(json.load(handle))


def render_summary(summary, decisions, lanes, jobs, run_id, attempt):
    lines = ["## Windows lanes: mode %s (run %s, attempt %s)" % (summary["mode"], run_id, attempt), ""]
    if summary["in_progress"] is not None:
        oldest = "-" if summary["oldest_queued_age"] is None else "%d s" % summary["oldest_queued_age"]
        lines += ["| in progress | queued | stuck | foreign | not ours | oldest queued | free slots | pool serving | deadline |",
                  "|---|---|---|---|---|---|---|---|---|",
                  "| %s | %s | %s | %s | %s | %s | %s | %s | %s |" % (
                      summary["in_progress"], summary["queued"], summary["stuck"], summary["foreign"], summary["inadmissible"],
                      oldest, summary["free"], "no" if summary["dead"] else "yes",
                      "-" if summary["deadline"] is None else "%d min" % (summary["deadline"] // 60)), ""]
    lines += [summary["reason"], "", "| lane | pool | label | reason |", "|---|---|---|---|"]
    for lane in lanes:
        decision = decisions.get(lane)
        if decision:
            lines.append("| %s | %s | %s | %s |" % (lane, decision["pool"], decision["label"], decision["reason"]))
    if jobs:
        lines += ["", "<details><summary>Listing the decision was made on (replay with ROUTE_JOBS_JSON)</summary>", "",
                  "| job | run | status | labels | created | started | repository |", "|---|---|---|---|---|---|---|"]
        for entry in jobs:
            lines.append("| %s | %s | %s | %s | %s | %s | %s |" % (
                entry.get("job_id"), entry.get("run_id"), entry.get("status"), " ".join(entry.get("labels") or []),
                entry.get("created_at") or "-", entry.get("started_at") or "-", entry.get("head_repo") or "-"))
        lines += ["", "```json", json.dumps(jobs), "```", "", "</details>"]
    return "\n".join(lines) + "\n"


def write_outputs(env, decisions, attempt):
    lines = ["attempt=%s" % attempt]
    for lane, decision in decisions.items():
        lines.append("%s=%s" % (lane, decision["label"]))
        lines.append("%s-pool=%s" % (lane, decision["pool"]))
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


def main(env=None):
    env = os.environ if env is None else env
    lanes = [lane.strip() for lane in (env.get("ROUTE_LANES") or "").split(",") if lane.strip()]
    ref = env.get("ROUTE_REF") or ""
    attempt = (env.get("ROUTE_ATTEMPT") or "").strip() or "1"
    run_id = env.get("ROUTE_RUN_ID") or "-"
    repo = env.get("ROUTE_REPO") or env.get("GITHUB_REPOSITORY") or ""
    now = _int_env(env, "ROUTE_NOW", int(time.time()))
    slots = _int_env(env, "ROUTE_SLOTS", DEFAULT_SLOTS)
    dead_seconds = _int_env(env, "ROUTE_DEAD_SECONDS", DEFAULT_DEAD_SECONDS)
    stale_seconds = _int_env(env, "ROUTE_STALE_SECONDS", DEFAULT_STALE_SECONDS)
    mode = env.get("WINDOWS_RUNNER_MODE")
    jobs = []
    try:
        if (mode or "").strip().lower() == "home":
            listing = env.get("ROUTE_JOBS_JSON")
            jobs = load_listing(listing) if listing else fetch_listing(repo, time.time() + TOTAL_TIMEOUT_SECONDS)
        decisions, summary = decide(mode, lanes, jobs, now, slots=slots, dead_seconds=dead_seconds, stale_seconds=stale_seconds,
                                    ref=ref, event=env.get("ROUTE_EVENT") or "", head_repo=env.get("ROUTE_HEAD_REPO") or "", repo=repo)
    except Exception as failure:  # noqa: BLE001 - every failure routes to the vendor, never fails the run
        reason = "%s: %s; every lane goes to the vendor" % (type(failure).__name__, failure)
        print("::warning title=Windows lane router::%s" % reason)
        decisions = _everything([lane for lane in lanes if lane in LANE_OF], "vendor", ref, reason)
        summary = {"mode": (mode or "").strip().lower() or "(unset)", "in_progress": None, "queued": None, "stuck": None,
                   "foreign": None, "inadmissible": None, "oldest_queued_age": None, "dead": False, "free": None,
                   "deadline": None, "reason": reason}
    text = render_summary(summary, decisions, lanes, jobs, run_id, attempt)
    print(text)
    step_summary = env.get("GITHUB_STEP_SUMMARY")
    if step_summary:
        with open(step_summary, "a", encoding="utf-8") as handle:
            handle.write(text)
    write_outputs(env, decisions, attempt)
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `python -I -m unittest discover -v -s ci/windows-runner/route/tests -t ci/windows-runner/route`
Expected: `OK` across every class.

- [ ] **Step 5: Run the router by hand against a replay file and against the live API**

Run (replay): `ROUTE_JOBS_JSON=/dev/null`-style replay is not valid JSON, so write a two-line file first:

```bash
printf '[{"job_id":1,"run_id":2,"head_repo":"coryj627/slate","status":"in_progress","labels":["slate-win-app"],"created_at":"2026-10-11T12:00:00Z","started_at":"2026-10-11T12:05:00Z"}]' > /tmp/listing.json
WINDOWS_RUNNER_MODE=home ROUTE_LANES=rust,app,model-0,model-1 ROUTE_REF=refs/heads/main ROUTE_REPO=coryj627/slate ROUTE_JOBS_JSON=/tmp/listing.json python3 ci/windows-runner/route/route.py
```

Expected: the summary table printed, `app-pool=home` (one slot free), `model-1-pool=vendor`, `attempt=1`.

Run (live, read-only; needs `gh auth status` green): `WINDOWS_RUNNER_MODE=home ROUTE_LANES=rust,app,model-0,model-1 ROUTE_REF=refs/heads/feature ROUTE_REPO=coryj627/slate python3 ci/windows-runner/route/route.py`
Expected: a summary whose counts match `gh api repos/coryj627/slate/actions/runs?status=in_progress`; no exception; exit 0.

- [ ] **Step 6: Commit**

```bash
git add ci/windows-runner/route
git commit -m "feat(ci-route): read the queue through gh, write the outputs and the summary, never fail

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 4: `windows.yml` routes through the router, and the guards pin it

**Files:**
- Modify: `.github/workflows/windows.yml` (header comment lines 36–51; path filters; `jobs:`; the four routed lanes; `flaui`; `gate`)
- Modify: `ci/windows-runner/tests/Workflows.Tests.ps1` (rewrite the `windows.yml pool switch` describe; add `branch-cleanup.yml` guards in Task 9)

**Interfaces:**
- Consumes: the router's outputs `rust`, `app`, `model-0`, `model-1`, `<lane>-pool`, `attempt` (Task 3).
- Produces: the job `route` (outputs as above), the attempt-guarded `runs-on` shape every routed lane carries, verbatim:
  - rust: `${{ needs.route.outputs.attempt == github.run_attempt && needs.route.outputs.rust || format('namespace-profile-winx64-fast{0};overrides.cache-tag=slate-windows-rust', github.ref != 'refs/heads/main' && '-pr' || '') }}`
  - app: the same with `app` and `slate-windows-app`
  - model: `${{ needs.route.outputs.attempt == github.run_attempt && (matrix.shard == 0 && needs.route.outputs.model-0 || needs.route.outputs.model-1) || format('namespace-profile-winx64-fast{0};overrides.cache-tag=slate-windows-model', github.ref != 'refs/heads/main' && '-pr' || '') }}`
  - the cache gates: `if: ${{ !(needs.route.outputs.attempt == github.run_attempt && needs.route.outputs.rust-pool == 'home') }}` (and `app-pool`; for the matrix `(matrix.shard == 0 && needs.route.outputs.model-0-pool || needs.route.outputs.model-1-pool) == 'home'`)
  - the shell gate: `runs-on: ${{ vars.WINDOWS_RUNNER_MODE == 'home-only' && 'slate-win-shell' || 'windows-latest' }}`

- [ ] **Step 1: Rewrite the guard tests so they fail against today's workflow**

Replace the whole `Describe 'windows.yml pool switch' { … }` block in `ci/windows-runner/tests/Workflows.Tests.ps1` with:

```powershell
Describe 'windows.yml routing through the router' {
    BeforeAll {
        $script:vendor = @{
            rust  = "format('namespace-profile-winx64-fast{0};overrides.cache-tag=slate-windows-rust', github.ref != 'refs/heads/main' && '-pr' || '')"
            app   = "format('namespace-profile-winx64-fast{0};overrides.cache-tag=slate-windows-app', github.ref != 'refs/heads/main' && '-pr' || '')"
            model = "format('namespace-profile-winx64-fast{0};overrides.cache-tag=slate-windows-model', github.ref != 'refs/heads/main' && '-pr' || '')"
        }
    }
    It 'has one routing job on hosted Linux with both permissions, the sparse checkout, the token and continue-on-error' {
        $windows | Should -Match '(?m)^  route:\r?\n    name: Route Windows lanes\r?\n    runs-on: ubuntu-latest'
        $route = $windows.Substring($windows.IndexOf("`n  route:"), $windows.IndexOf("`n  rust-tests:") - $windows.IndexOf("`n  route:"))
        $route | Should -Match '(?m)^    permissions:\r?\n      actions: read\r?\n      contents: read'
        $route | Should -Match 'sparse-checkout: ci/windows-runner/route'
        $route | Should -Match 'persist-credentials: false'
        $route | Should -Match 'continue-on-error: true'
        $route | Should -Match ([regex]::Escape('GH_TOKEN: ${{ github.token }}'))
        $route | Should -Match ([regex]::Escape('WINDOWS_RUNNER_MODE: ${{ vars.WINDOWS_RUNNER_MODE }}'))
        $route | Should -Match 'ROUTE_LANES: rust,app,model-0,model-1'
        $route | Should -Match ([regex]::Escape('ROUTE_ATTEMPT: ${{ github.run_attempt }}'))
        $route | Should -Match ([regex]::Escape('ROUTE_HEAD_REPO: ${{ github.event.pull_request.head.repo.full_name }}'))
        $route | Should -Match 'run: python3 ci/windows-runner/route/route\.py'
        foreach ($output in 'rust', 'app', 'model-0', 'model-1', 'rust-pool', 'app-pool', 'model-0-pool', 'model-1-pool', 'attempt') {
            $route | Should -Match ([regex]::Escape("      ${output}: `${{ steps.route.outputs.$output }}"))
        }
    }
    It 'every routed lane trusts the router only in its own attempt and falls back to today''s vendor string, verbatim' {
        $windows.Contains("runs-on: `${{ needs.route.outputs.attempt == github.run_attempt && needs.route.outputs.rust || $($vendor.rust) }}") | Should -BeTrue
        $windows.Contains("runs-on: `${{ needs.route.outputs.attempt == github.run_attempt && needs.route.outputs.app || $($vendor.app) }}") | Should -BeTrue
        $windows.Contains("runs-on: `${{ needs.route.outputs.attempt == github.run_attempt && (matrix.shard == 0 && needs.route.outputs.model-0 || needs.route.outputs.model-1) || $($vendor.model) }}") | Should -BeTrue
    }
    It 'every routed lane depends on route and still runs when route failed' {
        foreach ($job in 'rust-tests', 'windows', 'windows-model-shard') {
            $start = $windows.IndexOf("`n  ${job}:")
            $block = $windows.Substring($start, 600)
            $block | Should -Match '(?m)^    needs: \[route\]' -Because "$job must wait for the router"
            $block | Should -Match ([regex]::Escape('if: ${{ !cancelled() }}')) -Because "$job must run when the routing job failed"
        }
    }
    It 'gates the three Namespace cache mounts off a home lane of this attempt only' {
        ([regex]::Matches($windows, 'namespacelabs/nscloud-cache-action')).Count | Should -Be 3
        $windows.Contains("if: `${{ !(needs.route.outputs.attempt == github.run_attempt && needs.route.outputs.rust-pool == 'home') }}") | Should -BeTrue
        $windows.Contains("if: `${{ !(needs.route.outputs.attempt == github.run_attempt && needs.route.outputs.app-pool == 'home') }}") | Should -BeTrue
        $windows.Contains("if: `${{ !(needs.route.outputs.attempt == github.run_attempt && (matrix.shard == 0 && needs.route.outputs.model-0-pool || needs.route.outputs.model-1-pool) == 'home') }}") | Should -BeTrue
    }
    It 'runs the shell gate on windows-latest in every mode but home-only' {
        $windows.Contains("runs-on: `${{ vars.WINDOWS_RUNNER_MODE == 'home-only' && 'slate-win-shell' || 'windows-latest' }}") | Should -BeTrue
    }
    It 'reads the variable only in the routing job and the shell gate, never compares it with namespace, and keeps no bare vendor runs-on' {
        ([regex]::Matches($windows, "WINDOWS_RUNNER_MODE == 'namespace'")).Count | Should -Be 0
        ([regex]::Matches($windows, "vars\.WINDOWS_RUNNER_MODE (==|!=) '")).Count | Should -Be 1
        ([regex]::Matches($windows, 'runs-on: namespace-profile')).Count | Should -Be 0
        ([regex]::Matches($windows, 'runs-on: windows-latest')).Count | Should -Be 0
        ([regex]::Matches($windows, "runs-on: `\$`{`{ vars\.WINDOWS_RUNNER_MODE == 'home' &&")).Count | Should -Be 0
    }
    It 'every routed lane prints its pool first, and the gate names the pools' {
        ([regex]::Matches($windows, [regex]::Escape('- name: Record the pool'))).Count | Should -Be 3
        $windows | Should -Match ([regex]::Escape('needs: [route, rust-tests, windows, windows-model, flaui]'))
        $windows | Should -Match 'pools: rust=\$\{\{ needs\.route\.outputs\.rust-pool \}\}'
    }
    It 'runs the workflow when the router changes' {
        ([regex]::Matches($windows, [regex]::Escape("- 'ci/windows-runner/route/**'"))).Count | Should -Be 2
    }
    It 'documents the four modes, the critical-path rule, the fork rule and re-run all jobs in the header' {
        $windows | Should -Match 'self-hosted-windows-runner\.md'
        $joined = $windows -replace '\r?\n#\s*', ' '
        $joined | Should -Match 'home-only'
        $joined | Should -Match 'critical path'
        $joined | Should -Match ([regex]::Escape('Re-run all jobs'))
        $joined | Should -Match ([regex]::Escape("anything else, including unset, is today's providers"))
    }
}
```

Also change the `compares the variable with home only` expectation that lived in the old block: `nightly.yml`'s two reads are now the only `vars.WINDOWS_RUNNER_MODE (==|!=) '…'` reads outside `windows.yml`; keep the existing `nightly.yml pool switch` describe as it is.

- [ ] **Step 2: Run the guards to verify they fail**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1 -Filter 'windows.yml routing through the router.*'`
Expected: every `It` in the new describe fails (the workflow has no `route` job yet); the other describes still pass.

- [ ] **Step 3: Rewrite the header comment**

Replace lines 36–51 of `.github/workflows/windows.yml` (from `# Runner selection, revised 2026-10-10:` to `# scheme both replaced is in git history at a5037b35.`) with:

```yaml
# Runner selection, revised 2026-10-11: the repository variable
# WINDOWS_RUNNER_MODE and the routing job `route` decide per lane.
# `home` means home first: a lane takes a free slot of the self-hosted
# pool on the owner's desktop (one throwaway Hyper-V VM per job, labels
# slate-win-{rust,app,model}); when none is free it queues there only if
# it would still finish inside the run's critical path (the app lane plus
# the shell gate), and otherwise runs on Namespace at once. A host that
# leaves an admissible job queued five minutes while a slot is idle, or
# one that is down, sends every lane to Namespace by itself. `home-only`
# queues every lane at home (golden-image validation, fidelity runs).
# `namespace` or anything else, including unset, is today's providers,
# verbatim, with no routing call at all: a run that cannot read repository
# variables (a fork or Dependabot pull request) never waits on the home
# host, and a fork pull request is sent to the vendor by the router and
# refused by the host even if its copy of the router says otherwise. The
# shell gate stays on GitHub-hosted windows-latest in every mode but
# home-only: free on a public repository and faster than the home pool.
# ci/windows-runner/route/route.py is the rule; its step summary shows
# what each run saw and decided. A lane trusts the router's answer only
# in the attempt it ran in, so "Re-run failed jobs" (which reuses it)
# always lands on the vendor: after a flip or an outage use "Re-run all
# jobs". docs/runbooks/self-hosted-windows-runner.md has the design, the
# fallback flip and the trust model; today's providers are the 2026-07-31
# arrangement: namespace-profile-winx64-fast[-pr] (dashboard-configured,
# Cache Volumes enabled) for the build/test lanes and windows-latest for
# the shell gate. Flip with `gh variable set WINDOWS_RUNNER_MODE --body
# namespace` (and back with `--body home`). The per-attempt actions/cache
# scheme both replaced is in git history at a5037b35.
```

- [ ] **Step 4: Add the router to both path filters**

In both `pull_request.paths` and `push.paths`, after `- '.github/workflows/windows.yml'`, add:

```yaml
      - 'ci/windows-runner/route/**'
```

- [ ] **Step 5: Add the routing job**

Immediately after `jobs:` (before `  rust-tests:`), insert:

```yaml
  # The rule lives in ci/windows-runner/route/route.py (spec §3–4): it
  # reads the home pool's queue with this run's own token and decides a
  # pool per lane. It never fails the run: continue-on-error, and every
  # routed lane falls back to the vendor when an output is missing or was
  # written by an earlier attempt.
  route:
    name: Route Windows lanes
    runs-on: ubuntu-latest
    timeout-minutes: 5
    permissions:
      actions: read
      contents: read
    outputs:
      rust: ${{ steps.route.outputs.rust }}
      app: ${{ steps.route.outputs.app }}
      model-0: ${{ steps.route.outputs.model-0 }}
      model-1: ${{ steps.route.outputs.model-1 }}
      rust-pool: ${{ steps.route.outputs.rust-pool }}
      app-pool: ${{ steps.route.outputs.app-pool }}
      model-0-pool: ${{ steps.route.outputs.model-0-pool }}
      model-1-pool: ${{ steps.route.outputs.model-1-pool }}
      attempt: ${{ steps.route.outputs.attempt }}
    steps:
      - name: Checkout the route script
        uses: actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0  # v7
        with:
          sparse-checkout: ci/windows-runner/route
          persist-credentials: false
      - name: Pick a pool for each lane from WINDOWS_RUNNER_MODE and the home queue
        id: route
        continue-on-error: true
        env:
          GH_TOKEN: ${{ github.token }}
          WINDOWS_RUNNER_MODE: ${{ vars.WINDOWS_RUNNER_MODE }}
          ROUTE_LANES: rust,app,model-0,model-1
          ROUTE_REF: ${{ github.ref }}
          ROUTE_ATTEMPT: ${{ github.run_attempt }}
          ROUTE_RUN_ID: ${{ github.run_id }}
          ROUTE_EVENT: ${{ github.event_name }}
          ROUTE_HEAD_REPO: ${{ github.event.pull_request.head.repo.full_name }}
          ROUTE_REPO: ${{ github.repository }}
        run: python3 ci/windows-runner/route/route.py

```

- [ ] **Step 6: Route the rust lane**

Replace the `rust-tests` job head (its `runs-on:` line and the line after it) so the job begins:

```yaml
  rust-tests:
    name: rust tests (windows x64)
    needs: [route]
    if: ${{ !cancelled() }}
    runs-on: ${{ needs.route.outputs.attempt == github.run_attempt && needs.route.outputs.rust || format('namespace-profile-winx64-fast{0};overrides.cache-tag=slate-windows-rust', github.ref != 'refs/heads/main' && '-pr' || '') }}
    timeout-minutes: 60
    steps:
      - name: Record the pool
        run: Write-Host "runner=${{ runner.name }} environment=${{ runner.environment }} routed=${{ needs.route.outputs.rust-pool }} attempt=${{ github.run_attempt }}"

      - name: Checkout
```

and change its cache step's condition from `if: ${{ vars.WINDOWS_RUNNER_MODE != 'home' }}` to:

```yaml
        if: ${{ !(needs.route.outputs.attempt == github.run_attempt && needs.route.outputs.rust-pool == 'home') }}
```

- [ ] **Step 7: Route the app lane**

The `windows` job head becomes:

```yaml
  windows:
    name: app build + test (windows x64)
    needs: [route]
    if: ${{ !cancelled() }}
    runs-on: ${{ needs.route.outputs.attempt == github.run_attempt && needs.route.outputs.app || format('namespace-profile-winx64-fast{0};overrides.cache-tag=slate-windows-app', github.ref != 'refs/heads/main' && '-pr' || '') }}
    timeout-minutes: 60
    steps:
      - name: Record the pool
        run: Write-Host "runner=${{ runner.name }} environment=${{ runner.environment }} routed=${{ needs.route.outputs.app-pool }} attempt=${{ github.run_attempt }}"

      - name: Checkout
```

and its cache step's condition becomes:

```yaml
        if: ${{ !(needs.route.outputs.attempt == github.run_attempt && needs.route.outputs.app-pool == 'home') }}
```

- [ ] **Step 8: Route the model shards**

The `windows-model-shard` job head becomes:

```yaml
  windows-model-shard:
    name: app model (windows x64, shard ${{ matrix.shard }})
    needs: [route]
    if: ${{ !cancelled() }}
    runs-on: ${{ needs.route.outputs.attempt == github.run_attempt && (matrix.shard == 0 && needs.route.outputs.model-0 || needs.route.outputs.model-1) || format('namespace-profile-winx64-fast{0};overrides.cache-tag=slate-windows-model', github.ref != 'refs/heads/main' && '-pr' || '') }}
    strategy:
```

(keep `strategy`, `timeout-minutes` and the comments as they are), insert as its first step:

```yaml
      - name: Record the pool
        run: Write-Host "runner=${{ runner.name }} environment=${{ runner.environment }} routed=${{ matrix.shard == 0 && needs.route.outputs.model-0-pool || needs.route.outputs.model-1-pool }} attempt=${{ github.run_attempt }}"

```

and its cache step's condition becomes:

```yaml
        if: ${{ !(needs.route.outputs.attempt == github.run_attempt && (matrix.shard == 0 && needs.route.outputs.model-0-pool || needs.route.outputs.model-1-pool) == 'home') }}
```

- [ ] **Step 9: Pin the shell gate and extend the aggregate**

`flaui`'s `runs-on` becomes:

```yaml
    runs-on: ${{ vars.WINDOWS_RUNNER_MODE == 'home-only' && 'slate-win-shell' || 'windows-latest' }}
```

`gate`'s `needs` becomes `needs: [route, rust-tests, windows, windows-model, flaui]` and its error line gains the pools:

```yaml
            echo "::error::rust-tests=${{ needs.rust-tests.result }} app=${{ needs.windows.result }} model=${{ needs.windows-model.result }} flaui=${{ needs.flaui.result }} pools: rust=${{ needs.route.outputs.rust-pool }} app=${{ needs.route.outputs.app-pool }} model-0=${{ needs.route.outputs.model-0-pool }} model-1=${{ needs.route.outputs.model-1-pool }} (router attempt ${{ needs.route.outputs.attempt }} of ${{ github.run_attempt }})"
```

- [ ] **Step 10: Run the whole Pester suite to verify it passes**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1`
Expected: every test passes, the new describe included. Then `python3 -c "import yaml,sys; yaml.safe_load(open('.github/workflows/windows.yml'))"` (install PyYAML if absent: `python3 -m pip install --user pyyaml`) prints nothing: the YAML parses.

- [ ] **Step 11: Commit**

```bash
git add .github/workflows/windows.yml ci/windows-runner/tests/Workflows.Tests.ps1
git commit -m "ci(windows): route every lane through the router, trust it only in its own attempt, keep the shell gate hosted

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 5: The host refuses jobs of other repositories at admission (D-5)

**Files:**
- Modify: `ci/windows-runner/SlateCiHost.psm1` (`Invoke-Admission`)
- Modify: `ci/windows-runner/tests/Orchestrator.Tests.ps1` (Context `admission`)

**Interfaces:**
- Consumes: the `GetRun` adapter (exists; conditional request in `adapters/GitHub.ps1`), `$Config.TrustedRepo`, the journal's `SeenJobs` entries.
- Produces: a `SeenJobs` entry gains `Foreign = $true` once its run's `head_repository.full_name` differs from `TrustedRepo`; such jobs are never admitted; one log line `admission: skipped job <id> of <repo> (not the trusted repository)` per job.

- [ ] **Step 1: Write the failing tests**

In `ci/windows-runner/tests/Orchestrator.Tests.ps1`, inside `Context 'admission'`, add:

```powershell
    It 'never admits a job whose run comes from another repository, and says so once' {
        # D-5 of the overflow spec: a fork can route itself to the home label;
        # the approval policy decides whether its workflow runs at all, and
        # this check is what keeps an approved fork off the pool.
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        Set-Run 10 'pull_request' 'feature' 'someone/slate'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        (Get-Calls 'NewVm').Count | Should -Be 0
        $journal.SeenJobs['1'].Foreign | Should -BeTrue
        ([regex]::Matches(($logs -join "`n"), 'admission: skipped job 1 of someone/slate \(not the trusted repository\)')).Count | Should -Be 1
    }
    It 'admits a job of the trusted repository after one run lookup' {
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        Set-Run 10 'pull_request' 'feature' 'coryj627/slate'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        (Get-Calls 'NewVm').Count | Should -Be 1
        (Get-Calls 'GetRun').Count | Should -Be 1
        $journal.SeenJobs['1'].Foreign | Should -BeFalse
    }
    It 'skips a job this tick when its run cannot be read, and tries again next tick' {
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        $script:runCalls = 0
        $adapters.GetRun = { param($id) $script:runCalls++; if ($script:runCalls -eq 1) { throw '502' }; $script:world.Runs[[string]$id] }
        Set-Run 10
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        (Get-Calls 'NewVm').Count | Should -Be 0
        ($logs -join "`n") | Should -Match 'admission: job 1: run 10 could not be read, not admitted this tick: 502'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        (Get-Calls 'NewVm').Count | Should -Be 1
    }
```

Also update the existing `admits a queued app job into slot 1 and starts a VM` test: its `$journal.SeenJobs['1'].Lane | Should -Be 'app'` line gains, right after it, `$journal.SeenJobs['1'].Foreign | Should -BeFalse`. Every existing admission test that provisions must now define a run: add `Set-Run 10` (and `Set-Run 11`, `Set-Run 12` where other run ids are queued) at the top of each `It` in `Context 'admission'`, `Context 'settle'`, `Context 'shell lane'`, `Context 'handed-phase guards'` and any other context whose `BeforeEach` queues jobs without a run (search the file for `New-QueuedJob` and make sure the run id it names has a `Set-Run` before the first tick). A test that deliberately leaves the run undefined to exercise `GetRun` failure keeps its shape.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1 -Filter 'orchestrator.admission.*'`
Expected: the three new tests fail (`NewVm` is called for the foreign job; `Foreign` is null; no log line).

- [ ] **Step 3: Implement the check**

In `Invoke-Admission`, replace the block that records seen jobs:

```powershell
    $queued = @($laneJobs | Where-Object { $_.Status -eq 'queued' })
    foreach ($q in $queued) {
        $key = [string]$q.JobId
        if (-not $Journal.SeenJobs.Contains($key)) {
            $Journal.SeenJobs[$key] = @{ RunId = [int64]$q.RunId; Lane = $q.Lane; FirstSeenAt = $Now.ToString('o') }
        }
    }
```

with:

```powershell
    $queued = @($laneJobs | Where-Object { $_.Status -eq 'queued' })
    # Only this repository's runs get a VM (overflow spec D-5): a fork pull
    # request runs its own workflow file and can name the home label, so
    # the check lives here, where the fork cannot edit it. The run is read
    # once per job (SeenJobs remembers the answer; the adapter's request is
    # conditional anyway); a read that fails leaves the job for the next
    # tick rather than admitting it on no evidence.
    $admissible = @()
    foreach ($q in $queued) {
        $key = [string]$q.JobId
        if (-not $Journal.SeenJobs.Contains($key)) {
            $Journal.SeenJobs[$key] = @{ RunId = [int64]$q.RunId; Lane = $q.Lane; FirstSeenAt = $Now.ToString('o') }
        }
        $seen = $Journal.SeenJobs[$key]
        if (-not $seen.Contains('Foreign')) {
            try {
                $run = & $Adapters.GetRun ([int64]$q.RunId)
            } catch {
                & $Adapters.Log 'warn' "admission: job $($q.JobId): run $($q.RunId) could not be read, not admitted this tick: $_"
                continue
            }
            $headRepo = ''
            if ($null -ne $run -and $run.PSObject.Properties['head_repository'] -and $null -ne $run.head_repository) {
                $headRepo = [string]$run.head_repository.full_name
            }
            $seen['Foreign'] = -not [string]::Equals($headRepo, [string]$Config.TrustedRepo, [System.StringComparison]::OrdinalIgnoreCase)
            if ($seen['Foreign']) {
                & $Adapters.Log 'warn' "admission: skipped job $($q.JobId) of $headRepo (not the trusted repository)"
            }
        }
        if (-not $seen['Foreign']) { $admissible += $q }
    }
```

and change the admission line `Select-JobsToAdmit -Candidates $queued …` to `Select-JobsToAdmit -Candidates $admissible …`. The reconciliation block (`runs job …, not job …`) keeps using `$laneJobs` and, where it creates a `SeenJobs` entry for a job it did not see queued, that entry gains `Foreign = $false` (the runner is ours: it was handed to a job of a run this host already vetted).

- [ ] **Step 4: Run the whole Pester suite to verify it passes**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1`
Expected: all green, including every test you gave a `Set-Run`.

- [ ] **Step 5: Commit**

```bash
git add ci/windows-runner/SlateCiHost.psm1 ci/windows-runner/tests/Orchestrator.Tests.ps1
git commit -m "fix(ci-host): admit only jobs of the trusted repository, where a fork cannot edit the rule

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 6: The host prunes its logs (D-7)

**Files:**
- Modify: `ci/windows-runner/SlateCiHost.psm1` (`Get-CiHostConfig`, `New-Journal`, `Read-Journal`, new `Get-ExpiredLogNames`, new `Invoke-LogPrune`, `Invoke-OrchestratorTick`, `Invoke-StartupSweep`, exports)
- Modify: `ci/windows-runner/adapters/HyperV.ps1` (two file adapters: `ListLogs`, `RemoveLog`)
- Modify: `ci/windows-runner/config.json` (`"LogRetentionDays": 30`)
- Modify: `ci/windows-runner/tests/SlateCiHost.Tests.ps1`, `ci/windows-runner/tests/Orchestrator.Tests.ps1`, `ci/windows-runner/tests/HyperVAdapter.Tests.ps1`

**Interfaces:**
- Produces: `Get-ExpiredLogNames -Names <string[]> -Today <string yyyy-MM-dd> -RetentionDays <int> -> string[]`; `Invoke-LogPrune -Config -Journal -Adapters -Today`; journal key `LogsPrunedOn` (a local date string or `$null`); config key `LogRetentionDays` defaulted to 30; adapters `ListLogs` (names of `orchestrator-*.log` in `LogDir`) and `RemoveLog <name>`.

- [ ] **Step 1: Write the failing tests**

In `ci/windows-runner/tests/SlateCiHost.Tests.ps1` add a describe:

```powershell
Describe 'Get-ExpiredLogNames' {
    It 'selects dated log names older than the retention, by the date in the name' {
        $names = 'orchestrator-2026-09-10.log', 'orchestrator-2026-09-11.log', 'orchestrator-2026-09-12.log', 'orchestrator-2026-10-11.log'
        @(Get-ExpiredLogNames -Names $names -Today '2026-10-11' -RetentionDays 30) | Should -Be @('orchestrator-2026-09-10.log')
    }
    It 'keeps today, names that do not parse and other files' {
        $names = 'orchestrator-2026-10-11.log', 'orchestrator-yesterday.log', 'notes.txt', 'orchestrator-2026-13-40.log'
        @(Get-ExpiredLogNames -Names $names -Today '2026-10-11' -RetentionDays 1).Count | Should -Be 0
    }
    It 'returns nothing for an empty directory' {
        @(Get-ExpiredLogNames -Names @() -Today '2026-10-11' -RetentionDays 30).Count | Should -Be 0
    }
}
```

In the `Get-CiHostConfig` describe of the same file add:

```powershell
    It 'defaults LogRetentionDays to 30 when the file predates the key, and rejects a bad value' {
        $cfg = Get-Content -Raw (Join-Path $PSScriptRoot '..' 'config.json') | ConvertFrom-Json -AsHashtable
        $cfg.Remove('LogRetentionDays')
        $p = Join-Path $TestDrive 'old.json'; $cfg | ConvertTo-Json -Depth 5 | Set-Content $p
        (Get-CiHostConfig -Path $p).LogRetentionDays | Should -Be 30
        $cfg['LogRetentionDays'] = 0
        $cfg | ConvertTo-Json -Depth 5 | Set-Content $p
        { Get-CiHostConfig -Path $p } | Should -Throw '*LogRetentionDays*'
    }
```

In the journal describe add, after the existing round-trip test:

```powershell
    It 'a journal without LogsPrunedOn reads back with it null' {
        $p = Join-Path $TestDrive 'j.json'
        '{"Vms":{},"Retries":{},"SeenJobs":{}}' | Set-Content $p
        (Read-Journal -Path $p).Contains('LogsPrunedOn') | Should -BeTrue
        (Read-Journal -Path $p).LogsPrunedOn | Should -Be $null
    }
```

In `ci/windows-runner/tests/Orchestrator.Tests.ps1`: in `New-World` add `Logs = @('orchestrator-2026-09-01.log', 'orchestrator-2026-10-10.log')` to the world hashtable; in `New-FakeAdapters` add

```powershell
            ListLogs      = { & $script:record 'ListLogs' @(); @($script:world.Logs) }
            RemoveLog     = { param($name) & $script:record 'RemoveLog' @($name); $script:world.Logs = @($script:world.Logs | Where-Object { $_ -ne $name }) }
```

and a context:

```powershell
Context 'log retention' {
    It 'prunes once per local day, at the first tick, and remembers the day in the journal' {
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        (Get-Calls 'RemoveLog').Count | Should -Be 1
        (Get-Calls 'RemoveLog')[0].Args[0] | Should -Be 'orchestrator-2026-09-01.log'
        $journal.LogsPrunedOn | Should -Be $t0.ToLocalTime().ToString('yyyy-MM-dd')
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(5)
        (Get-Calls 'ListLogs').Count | Should -Be 1
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddDays(1)
        (Get-Calls 'ListLogs').Count | Should -Be 2
    }
    It 'logs and continues when a delete fails' {
        $adapters.RemoveLog = { param($name) throw 'in use' }
        { Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0 } | Should -Not -Throw
        ($logs -join "`n") | Should -Match 'logs: orchestrator-2026-09-01.log: in use'
        $journal.LogsPrunedOn | Should -Not -Be $null
    }
    It 'the startup sweep forgets the day so the first tick prunes again' {
        $journal.LogsPrunedOn = '2026-10-10'
        Invoke-StartupSweep -Config $config -Journal $journal -Adapters $adapters
        $journal.LogsPrunedOn | Should -Be $null
    }
}
```

In `ci/windows-runner/tests/HyperVAdapter.Tests.ps1` add (next to the existing file-only adapter tests):

```powershell
Describe 'log adapters' {
    It 'list only dated orchestrator logs and delete by name inside LogDir' {
        $logDir = Join-Path $TestDrive 'logs'; New-Item -ItemType Directory -Force $logDir | Out-Null
        'x' | Set-Content (Join-Path $logDir 'orchestrator-2026-09-01.log')
        'x' | Set-Content (Join-Path $logDir 'notes.txt')
        $config = @{ LogDir = $logDir }
        @(Get-OrchestratorLogNames -LogDir $logDir) | Should -Be @('orchestrator-2026-09-01.log')
        Remove-OrchestratorLog -LogDir $logDir -Name 'orchestrator-2026-09-01.log'
        Test-Path (Join-Path $logDir 'orchestrator-2026-09-01.log') | Should -BeFalse
        { Remove-OrchestratorLog -LogDir $logDir -Name '..\notes.txt' } | Should -Throw
        $adapters = New-HyperVAdapters -Config $config
        $adapters.Keys | Should -Contain 'ListLogs'
        $adapters.Keys | Should -Contain 'RemoveLog'
    }
}
```

(The existing `New-HyperVAdapters` tests that count its keys or stub `Get-Command` must include the two new command names.)

- [ ] **Step 2: Run the tests to verify they fail**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1`
Expected: the new tests fail (`Get-ExpiredLogNames` is not a command; `LogRetentionDays` is absent; `ListLogs` is not called).

- [ ] **Step 3: Implement**

`ci/windows-runner/config.json`: after `"RetryBackoffSeconds": 600,` add `"LogRetentionDays": 30,`.

`Get-CiHostConfig`, after the `RoutedWorkflows` validation:

```powershell
    # Older config files predate the key: default it rather than crash-loop
    # a host that was installed before it existed.
    if (-not $config.Contains('LogRetentionDays')) { $config['LogRetentionDays'] = 30 }
    if (-not ($config.LogRetentionDays -is [int] -or $config.LogRetentionDays -is [int64]) -or [int]$config.LogRetentionDays -lt 1) {
        throw "config ${Path}: LogRetentionDays must be a whole number of days, 1 or more"
    }
```

`New-Journal` returns `@{ Vms = @{}; Retries = @{}; SeenJobs = @{}; LogsPrunedOn = $null }`; in `Read-Journal`, after the loop that defaults the three dictionaries, add `if (-not $journal.Contains('LogsPrunedOn')) { $journal['LogsPrunedOn'] = $null }`.

New functions, placed after `Write-CiLog`:

```powershell
function Get-ExpiredLogNames {
    # Dated orchestrator logs older than the retention, by the local date in
    # the name (orchestrator.ps1 names them with the local date). Names that
    # do not parse are kept: a human can decide about those.
    [CmdletBinding()]
    param([string[]]$Names = @(), [Parameter(Mandatory)][string]$Today, [Parameter(Mandatory)][int]$RetentionDays)
    $cutoff = [datetime]::ParseExact($Today, 'yyyy-MM-dd', [cultureinfo]::InvariantCulture).AddDays(-$RetentionDays)
    $expired = @()
    foreach ($name in @($Names)) {
        if ($name -notmatch '^orchestrator-(\d{4}-\d{2}-\d{2})\.log$') { continue }
        $date = [datetime]::MinValue
        if (-not [datetime]::TryParseExact($Matches[1], 'yyyy-MM-dd', [cultureinfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::None, [ref]$date)) { continue }
        if ($date -lt $cutoff) { $expired += $name }
    }
    return @($expired)
}

function Invoke-LogPrune {
    # Once per local day (journal LogsPrunedOn), and at the first tick after
    # a start (the sweep clears the day). A delete that fails is logged and
    # the day still counts as done: the file is tried again tomorrow.
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Config, [Parameter(Mandatory)]$Journal, [Parameter(Mandatory)][hashtable]$Adapters, [Parameter(Mandatory)][string]$Today)
    if ([string]$Journal['LogsPrunedOn'] -eq $Today) { return }
    $names = @(& $Adapters.ListLogs)
    foreach ($name in @(Get-ExpiredLogNames -Names $names -Today $Today -RetentionDays ([int]$Config.LogRetentionDays))) {
        try { & $Adapters.RemoveLog $name; & $Adapters.Log 'info' "logs: removed $name" }
        catch { & $Adapters.Log 'warn' "logs: ${name}: $_" }
    }
    $Journal['LogsPrunedOn'] = $Today
}
```

In `Invoke-OrchestratorTick`, before the `foreach ($name in @($Journal.Vms.Keys))` loop:

```powershell
    try { Invoke-LogPrune -Config $Config -Journal $Journal -Adapters $Adapters -Today $Now.ToLocalTime().ToString('yyyy-MM-dd') }
    catch { & $Adapters.Log 'error' "logs: $_" }
```

In `Invoke-StartupSweep`, as its last line: `$Journal['LogsPrunedOn'] = $null`. Add `Get-ExpiredLogNames, Invoke-LogPrune` to `Export-ModuleMember`.

`adapters/HyperV.ps1`, before `New-HyperVAdapters`:

```powershell
function Get-OrchestratorLogNames {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$LogDir)
    if (-not (Test-Path -LiteralPath $LogDir)) { return @() }
    return @(Get-ChildItem -LiteralPath $LogDir -File -Filter 'orchestrator-*.log' | ForEach-Object { $_.Name })
}

function Remove-OrchestratorLog {
    # By name only, inside LogDir: a name with a path separator is refused.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$LogDir, [Parameter(Mandatory)][string]$Name)
    $ErrorActionPreference = 'Stop'
    if ($Name -ne [System.IO.Path]::GetFileName($Name)) { throw "log name '$Name' is not a bare file name" }
    Remove-Item -LiteralPath (Join-Path $LogDir $Name) -Force
}
```

add `'Get-OrchestratorLogNames', 'Remove-OrchestratorLog'` to the `foreach ($command in …)` list of `New-HyperVAdapters`, and two entries to its hashtable:

```powershell
        ListLogs      = { & $commands['Get-OrchestratorLogNames'] -LogDir $Config.LogDir }
        RemoveLog     = { param($name) & $commands['Remove-OrchestratorLog'] -LogDir $Config.LogDir -Name $name }
```

- [ ] **Step 4: Run the whole Pester suite to verify it passes**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1`
Expected: all green. Then the parse check the suite already runs for the adapters passes.

- [ ] **Step 5: Commit**

```bash
git add ci/windows-runner/SlateCiHost.psm1 ci/windows-runner/adapters/HyperV.ps1 ci/windows-runner/config.json ci/windows-runner/tests
git commit -m "feat(ci-host): prune orchestrator logs older than LogRetentionDays once a day

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 7: The orchestrator records golden acceptance and signals when the previous image can go (D-7)

**Files:**
- Modify: `ci/windows-runner/SlateCiHost.psm1` (`New-Journal`, `Read-Journal`, new `Register-GoldenAcceptance`, `Complete-ActiveVm`, exports)
- Modify: `ci/windows-runner/adapters/HyperV.ps1` (`Get-GoldenStamp`, `Get-OldestVmCreation`, two adapters)
- Modify: `ci/windows-runner/tests/Orchestrator.Tests.ps1`, `ci/windows-runner/tests/SlateCiHost.Tests.ps1`, `ci/windows-runner/tests/HyperVAdapter.Tests.ps1`

**Interfaces:**
- Produces: journal keys `GoldenAccepted` (`@{ Golden = <ISO UTC LastWriteTime of the golden>; Lanes = @(...) }` or `$null`) and `GoldenPrevRemovableAt` (ISO UTC or `$null`); adapters `GetGoldenStamp` (ISO UTC string of the golden's `LastWriteTimeUtc`) and `GetOldestVmCreation` (ISO UTC string of the oldest `CreationTimeUtc` among directories under `VmDir`, or `$null`); `Register-GoldenAcceptance -Journal -Adapters -Vm -Now`; the log line `golden: the previous image is no longer needed (rust, app and model are green on the current one)`.
- Consumed by Task 8's prune script (reads `GoldenPrevRemovableAt` from `state\journal.json`).

- [ ] **Step 1: Write the failing tests**

`ci/windows-runner/tests/Orchestrator.Tests.ps1`: in `New-World` add `GoldenStamp = '2026-10-10T22:55:00Z'` and `OldestVm = $null`; in `New-FakeAdapters` add

```powershell
            GetGoldenStamp      = { & $script:record 'GetGoldenStamp' @(); $script:world.GoldenStamp }
            GetOldestVmCreation = { & $script:record 'GetOldestVmCreation' @(); $script:world.OldestVm }
```

and a context (its helper runs one lane green on a fresh VM):

```powershell
Context 'golden acceptance' {
    BeforeAll {
        # Pester 5 runs a Context body at discovery only: a helper the tests
        # call must be defined in BeforeAll (as the top-level helpers are).
        function Complete-GreenJob([string]$Lane, [datetimeoffset]$At) {
            $script:jobId++
            $script:world.Queued = @((New-QueuedJob $script:jobId ($script:jobId * 10) $Lane))
            Set-Run ($script:jobId * 10)
            Invoke-OrchestratorTick -Config $script:config -Journal $script:journal -Adapters $script:adapters -Now $At
            $name = @($script:journal.Vms.Keys | Where-Object { $script:journal.Vms[$_].JobId -eq $script:jobId })[0]
            Invoke-OrchestratorTick -Config $script:config -Journal $script:journal -Adapters $script:adapters -Now $At.AddSeconds(10)
            $script:world.Queued = @()
            $script:world.VmStates[$name] = 'Off'
            Set-DoneJob $script:jobId ($script:jobId * 10) $name
            Invoke-OrchestratorTick -Config $script:config -Journal $script:journal -Adapters $script:adapters -Now $At.AddMinutes(20)
        }
    }
    BeforeEach { $script:jobId = 100 }
    It 'records each lane that went green on a VM newer than the golden, then signals once all three did' {
        Complete-GreenJob 'rust' $t0
        $journal.GoldenAccepted.Golden | Should -Be '2026-10-10T22:55:00Z'
        @($journal.GoldenAccepted.Lanes) | Should -Be @('rust')
        $journal.GoldenPrevRemovableAt | Should -Be $null
        Complete-GreenJob 'app' $t0.AddHours(1)
        Complete-GreenJob 'rust' $t0.AddHours(2)
        @($journal.GoldenAccepted.Lanes | Sort-Object) | Should -Be @('app', 'rust')
        Complete-GreenJob 'model' $t0.AddHours(3)
        $journal.GoldenPrevRemovableAt | Should -Be $t0.AddHours(3).AddMinutes(20).ToString('o')
        ([regex]::Matches(($logs -join "`n"), 'golden: the previous image is no longer needed \(rust, app and model are green on the current one\)')).Count | Should -Be 1
        Complete-GreenJob 'app' $t0.AddHours(4)
        ([regex]::Matches(($logs -join "`n"), 'no longer needed')).Count | Should -Be 1
    }
    It 'ignores a green job on a VM created before the golden was sealed' {
        $world.GoldenStamp = $t0.AddMinutes(5).ToString('o')
        Complete-GreenJob 'rust' $t0
        $journal.GoldenAccepted.Lanes.Count | Should -Be 0
    }
    It 'starts over when the golden changes, and clears the signal' {
        foreach ($lane in 'rust', 'app', 'model') { Complete-GreenJob $lane ($t0.AddHours([int]('rust', 'app', 'model').IndexOf($lane))) }
        $journal.GoldenPrevRemovableAt | Should -Not -Be $null
        $world.GoldenStamp = '2026-10-12T01:00:00Z'
        Complete-GreenJob 'rust' $t0.AddDays(1)
        $journal.GoldenAccepted.Golden | Should -Be '2026-10-12T01:00:00Z'
        @($journal.GoldenAccepted.Lanes) | Should -Be @('rust')
        $journal.GoldenPrevRemovableAt | Should -Be $null
    }
    It 'withholds the signal while a VM older than the golden still exists' {
        $world.OldestVm = '2026-10-10T22:00:00Z'
        foreach ($lane in 'rust', 'app', 'model') { Complete-GreenJob $lane ($t0.AddHours([int]('rust', 'app', 'model').IndexOf($lane))) }
        $journal.GoldenPrevRemovableAt | Should -Be $null
        ($logs -join "`n") | Should -Match 'golden: rust, app and model are green on the current image, but a VM from before it still exists'
    }
    It 'a red job and a discarded fork record nothing' {
        $script:jobId++
        $world.Queued = @((New-QueuedJob $script:jobId 900 'app')); Set-Run 900
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $name = @($journal.Vms.Keys)[0]
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        $world.Queued = @(); $world.VmStates[$name] = 'Off'
        Set-DoneJob $script:jobId 900 $name 'failure'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20)
        $journal.GoldenAccepted | Should -Be $null
    }
    It 'a green pull-request job still counts: the image ran it, whatever the cache did' {
        $script:jobId++
        $world.Queued = @((New-QueuedJob $script:jobId 901 'app')); Set-Run 901 'pull_request' 'feature'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $name = @($journal.Vms.Keys)[0]
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        $world.Queued = @(); $world.VmStates[$name] = 'Off'
        Set-DoneJob $script:jobId 901 $name
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20)
        @($journal.GoldenAccepted.Lanes) | Should -Be @('app')
    }
}
```

`ci/windows-runner/tests/SlateCiHost.Tests.ps1`, in the journal describe: `New-Journal` must contain `GoldenAccepted` (null) and `GoldenPrevRemovableAt` (null), and a journal file without them reads back with both null (extend the test written in Task 6).

`ci/windows-runner/tests/HyperVAdapter.Tests.ps1`:

```powershell
Describe 'golden adapters' {
    It 'read the golden stamp and the oldest VM directory as ISO UTC' {
        $golden = Join-Path $TestDrive 'golden.vhdx'; 'g' | Set-Content $golden
        (Get-Item $golden).LastWriteTimeUtc = [datetime]'2026-10-10T22:55:00Z'
        Get-GoldenStamp -GoldenPath $golden | Should -Be '2026-10-10T22:55:00.0000000Z'
        $vmDir = Join-Path $TestDrive 'vms'; New-Item -ItemType Directory -Force (Join-Path $vmDir 'a'), (Join-Path $vmDir 'b') | Out-Null
        (Get-Item (Join-Path $vmDir 'a')).CreationTimeUtc = [datetime]'2026-10-11T01:00:00Z'
        (Get-Item (Join-Path $vmDir 'b')).CreationTimeUtc = [datetime]'2026-10-11T02:00:00Z'
        Get-OldestVmCreation -VmDir $vmDir | Should -Be '2026-10-11T01:00:00.0000000Z'
        Get-OldestVmCreation -VmDir (Join-Path $TestDrive 'none') | Should -Be $null
        $adapters = New-HyperVAdapters -Config @{ GoldenPath = $golden; VmDir = $vmDir; LogDir = $TestDrive }
        $adapters.Keys | Should -Contain 'GetGoldenStamp'
        $adapters.Keys | Should -Contain 'GetOldestVmCreation'
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1`
Expected: the new context's tests fail (`GoldenAccepted` absent, no log line); the adapter tests fail (`Get-GoldenStamp` not a command).

- [ ] **Step 3: Implement**

`New-Journal`: `@{ Vms = @{}; Retries = @{}; SeenJobs = @{}; LogsPrunedOn = $null; GoldenAccepted = $null; GoldenPrevRemovableAt = $null }`; `Read-Journal` defaults both to `$null` when absent, next to `LogsPrunedOn`.

New function after `Test-SiblingRunning`:

```powershell
function Register-GoldenAcceptance {
    # A green job on a VM created after the golden's seal vouches for that
    # lane on that image. Once rust, app and model all vouch and no VM from
    # before the seal still exists, the previous image is no longer needed:
    # the journal says so and the elevated prune task deletes it (the
    # orchestrator never writes under golden). A new seal starts over.
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Journal, [Parameter(Mandatory)][hashtable]$Adapters, [Parameter(Mandatory)]$Vm, [Parameter(Mandatory)][datetimeoffset]$Now)
    $stamp = [string](& $Adapters.GetGoldenStamp)
    if (-not $stamp) { return }
    $accepted = $Journal['GoldenAccepted']
    if ($null -eq $accepted -or [string]$accepted['Golden'] -ne $stamp) {
        $accepted = @{ Golden = $stamp; Lanes = @() }
        $Journal['GoldenAccepted'] = $accepted
        $Journal['GoldenPrevRemovableAt'] = $null
    }
    $sealed = ConvertTo-DateTimeOffset -Value $stamp
    if ((ConvertTo-DateTimeOffset -Value $Vm.StartedAt) -le $sealed) { return }
    if (@($accepted['Lanes']) -notcontains [string]$Vm.Lane) { $accepted['Lanes'] = @($accepted['Lanes']) + [string]$Vm.Lane }
    if ($Journal['GoldenPrevRemovableAt']) { return }
    foreach ($lane in 'rust', 'app', 'model') { if (@($accepted['Lanes']) -notcontains $lane) { return } }
    $oldest = & $Adapters.GetOldestVmCreation
    if ($oldest -and (ConvertTo-DateTimeOffset -Value ([string]$oldest)) -le $sealed) {
        & $Adapters.Log 'info' 'golden: rust, app and model are green on the current image, but a VM from before it still exists; the previous image stays'
        return
    }
    $Journal['GoldenPrevRemovableAt'] = $Now.ToString('o')
    & $Adapters.Log 'info' 'golden: the previous image is no longer needed (rust, app and model are green on the current one)'
}
```

In `Complete-ActiveVm`, call it on every green completion, before the VM is removed:
- in the `PendingCommit` branch, right after `$generation = & $Adapters.CommitCache …` and its log line (the job was green, or it would not be pending);
- in the main path, after `$status` is known to be `completed`: insert, right before `$run = $null`, the line
  `if ([string]$job.conclusion -eq 'success') { Register-GoldenAcceptance -Journal $Journal -Adapters $Adapters -Vm $vm -Now $Now }`
  (this covers the commit, the discard-for-provenance and the no-cache shell outcomes alike: acceptance is about the image, not the cache).

`adapters/HyperV.ps1`, before `New-HyperVAdapters`:

```powershell
function Get-GoldenStamp {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$GoldenPath)
    if (-not (Test-Path -LiteralPath $GoldenPath)) { return $null }
    return (Get-Item -LiteralPath $GoldenPath).LastWriteTimeUtc.ToString('o')
}

function Get-OldestVmCreation {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$VmDir)
    if (-not (Test-Path -LiteralPath $VmDir)) { return $null }
    $oldest = Get-ChildItem -LiteralPath $VmDir -Directory | Sort-Object CreationTimeUtc | Select-Object -First 1
    if ($null -eq $oldest) { return $null }
    return $oldest.CreationTimeUtc.ToString('o')
}
```

register both names in `New-HyperVAdapters`'s command list and add:

```powershell
        GetGoldenStamp      = { & $commands['Get-GoldenStamp'] -GoldenPath $Config.GoldenPath }
        GetOldestVmCreation = { & $commands['Get-OldestVmCreation'] -VmDir $Config.VmDir }
```

Export `Register-GoldenAcceptance`.

- [ ] **Step 4: Run the whole Pester suite to verify it passes**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1`
Expected: all green (the existing settle tests still pass: the fake `GetGoldenStamp` answers a stamp older than `$t0`).

- [ ] **Step 5: Commit**

```bash
git add ci/windows-runner/SlateCiHost.psm1 ci/windows-runner/adapters/HyperV.ps1 ci/windows-runner/tests
git commit -m "feat(ci-host): record which lanes went green on the current golden and signal when the previous image can go

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 8: The elevated prune task deletes the previous image; the build rotates it (D-7)

**Files:**
- Create: `ci/windows-runner/install/prune-golden.ps1`
- Modify: `ci/windows-runner/install/setup-host.ps1` (register `slate-ci-golden-prune`)
- Modify: `ci/windows-runner/golden/build-golden.ps1` (`-Rotate`)
- Modify: `ci/windows-runner/tests/Install.Tests.ps1`, `ci/windows-runner/tests/Golden.Tests.ps1`

**Interfaces:**
- Consumes: journal key `GoldenPrevRemovableAt` (Task 7), `config.json`'s `Root`.
- Produces: `prune-golden.ps1 -Root <path>` (exit 0 always; one log line to the orchestrator log either way); the scheduled task `slate-ci-golden-prune` (SYSTEM, daily 04:00 local); `build-golden.ps1 -Rotate`.

- [ ] **Step 1: Write the failing tests**

`ci/windows-runner/tests/Install.Tests.ps1`, in the `install scripts` describe:

```powershell
    It 'setup registers the elevated prune task for the previous golden image, daily, as SYSTEM' {
        $text = Get-Content -Raw (Join-Path $installDir 'setup-host.ps1')
        $start = $text.IndexOf("Register-ScheduledTask -TaskName 'slate-ci-golden-prune'")
        $start | Should -BeGreaterThan -1
        $end = $text.IndexOf("-User 'NT AUTHORITY\SYSTEM'", $start)
        $end | Should -BeGreaterThan $start
        # The prune registration, from its first line to its principal.
        $prune = $text.Substring($start, $end - $start)
        $prune | Should -Match 'prune-golden\.ps1'
        $prune | Should -Match '-Daily -At 4am'
        # Never the orchestrator's account: it has no delete right on golden by design.
        $prune | Should -Not -Match '\$Account'
        $text.Substring($end, 120) | Should -Match '-RunLevel Highest'
    }
    It 'registers the prune task on every run, outside the password-bound pair, so an older install gains it on a plain re-run' {
        $text = Get-Content -Raw (Join-Path $installDir 'setup-host.ps1')
        # It holds no password, so it is neither in $taskNames (whose absence
        # forces a password reset) nor inside the keepAccount branches.
        $text | Should -Match ([regex]::Escape("`$taskNames = @('slate-ci-orchestrator', 'slate-ci-store-token')"))
        $nine = $text.IndexOf("'9/9 scheduled tasks'")
        $prune = $text.IndexOf("TaskName 'slate-ci-golden-prune'")
        $keep = $text.IndexOf('if ($keepAccount) {', $nine)
        $prune | Should -BeGreaterThan $nine
        $prune | Should -BeLessThan $keep
    }
```

and a new describe that runs the script against a fake tree:

```powershell
Describe 'prune-golden.ps1' {
    BeforeEach {
        $script:root = Join-Path $TestDrive ('root-' + [guid]::NewGuid().ToString('N'))
        foreach ($d in 'golden', 'state', 'vms', 'logs', 'bin') { New-Item -ItemType Directory -Force (Join-Path $root $d) | Out-Null }
        Copy-Item (Join-Path $PSScriptRoot '..' 'SlateCiHost.psm1') (Join-Path $root 'bin')
        $script:golden = Join-Path $root 'golden\win11-runner.vhdx'; 'new' | Set-Content $golden
        $script:prev = Join-Path $root 'golden\win11-runner.prev.vhdx'; 'old' | Set-Content $prev
        (Get-Item $prev).LastWriteTimeUtc = [datetime]'2026-10-10T20:00:00Z'
        (Get-Item $golden).LastWriteTimeUtc = [datetime]'2026-10-10T22:55:00Z'
        Set-ItemProperty -LiteralPath $prev -Name IsReadOnly -Value $true
        $script:journalPath = Join-Path $root 'state\journal.json'
        $script:prune = Join-Path $installDir 'prune-golden.ps1'
    }
    It 'deletes the previous image once the journal says it is no longer needed' {
        @{ Vms = @{}; Retries = @{}; SeenJobs = @{}; GoldenPrevRemovableAt = '2026-10-11T13:00:00Z' } | ConvertTo-Json | Set-Content $journalPath
        & $prune -Root $root
        Test-Path $prev | Should -BeFalse
        Test-Path $golden | Should -BeTrue
        (Get-Content (Get-ChildItem (Join-Path $root 'logs') | Select-Object -First 1).FullName) | Should -Match 'golden: removed win11-runner.prev.vhdx'
    }
    It 'leaves it alone without the signal, with a signal older than the prev, or with a VM older than the golden' {
        @{ Vms = @{}; Retries = @{}; SeenJobs = @{} } | ConvertTo-Json | Set-Content $journalPath
        & $prune -Root $root
        Test-Path $prev | Should -BeTrue
        @{ Vms = @{}; Retries = @{}; SeenJobs = @{}; GoldenPrevRemovableAt = '2026-10-10T19:00:00Z' } | ConvertTo-Json | Set-Content $journalPath
        & $prune -Root $root
        Test-Path $prev | Should -BeTrue
        @{ Vms = @{}; Retries = @{}; SeenJobs = @{}; GoldenPrevRemovableAt = '2026-10-11T13:00:00Z' } | ConvertTo-Json | Set-Content $journalPath
        $old = Join-Path $root 'vms\slate-win-app-00000000'; New-Item -ItemType Directory -Force $old | Out-Null
        (Get-Item $old).CreationTimeUtc = [datetime]'2026-10-10T21:00:00Z'
        & $prune -Root $root
        Test-Path $prev | Should -BeTrue
        (Get-Content (Get-ChildItem (Join-Path $root 'logs') | Select-Object -First 1).FullName -Raw) | Should -Match 'a VM from before the current image still exists'
    }
    It 'does nothing when there is no previous image' {
        Remove-Item $prev -Force
        @{ Vms = @{}; Retries = @{}; SeenJobs = @{}; GoldenPrevRemovableAt = '2026-10-11T13:00:00Z' } | ConvertTo-Json | Set-Content $journalPath
        { & $prune -Root $root } | Should -Not -Throw
    }
}
```

`ci/windows-runner/tests/Golden.Tests.ps1`, next to `the build script applies install.wim…`:

```powershell
    It 'the build script rotates the current image to .prev with -Rotate, replacing an older prev, before refusing an existing output' {
        $text = Get-Content -Raw (Join-Path $goldenDir 'build-golden.ps1')
        $text | Should -Match ([regex]::Escape('[switch]$Rotate'))
        $rotate = $text.IndexOf('if ($Rotate')
        $rotate | Should -BeGreaterThan -1
        $rotate | Should -BeLessThan $text.IndexOf('exists; move it aside to rebuild')
        $text | Should -Match ([regex]::Escape("win11-runner.prev.vhdx"))
        $text | Should -Match 'IsReadOnly -Value \$false'
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1`
Expected: the prune describe fails (`prune-golden.ps1` missing), the two setup tests and the rotate test fail.

- [ ] **Step 3: Write the prune script**

Create `ci/windows-runner/install/prune-golden.ps1`:

```powershell
#Requires -Version 7
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Deletes golden\win11-runner.prev.vhdx once the orchestrator's journal says
# the previous image is no longer needed (GoldenPrevRemovableAt, written
# after rust, app and model went green on the current image) and no VM from
# before the current image still exists. Runs as SYSTEM from the
# slate-ci-golden-prune task (install/setup-host.ps1), daily: the
# orchestrator's account has no delete right on golden by design. Appends
# one line to the orchestrator log either way. Never fails the task.
[CmdletBinding()]
param([string]$Root = 'C:\slate-ci')

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $Root 'bin' 'SlateCiHost.psm1') -Force
$log = {
    param($level, $message)
    Write-CiLog -Path (Join-Path $Root 'logs' ('orchestrator-{0}.log' -f (Get-Date -Format 'yyyy-MM-dd'))) -Level $level -Message $message
}
try {
    $golden = Join-Path $Root 'golden\win11-runner.vhdx'
    $prev = Join-Path $Root 'golden\win11-runner.prev.vhdx'
    if (-not (Test-Path -LiteralPath $prev)) { exit 0 }
    $journal = Read-Journal -Path (Join-Path $Root 'state\journal.json')
    $signal = $journal['GoldenPrevRemovableAt']
    if (-not $signal) { & $log 'info' 'golden: the previous image stays (no acceptance signal yet)'; exit 0 }
    $signalAt = ConvertTo-DateTimeOffset -Value ([string]$signal)
    if ($signalAt -le [datetimeoffset](Get-Item -LiteralPath $prev).LastWriteTimeUtc) {
        & $log 'info' 'golden: the previous image stays (the acceptance signal predates it)'
        exit 0
    }
    $sealed = [datetimeoffset](Get-Item -LiteralPath $golden).LastWriteTimeUtc
    $vmDir = Join-Path $Root 'vms'
    if (Test-Path -LiteralPath $vmDir) {
        $older = @(Get-ChildItem -LiteralPath $vmDir -Directory | Where-Object { [datetimeoffset]$_.CreationTimeUtc -le $sealed })
        if ($older.Count -gt 0) {
            & $log 'info' "golden: the previous image stays (a VM from before the current image still exists: $($older[0].Name))"
            exit 0
        }
    }
    Set-ItemProperty -LiteralPath $prev -Name IsReadOnly -Value $false
    Remove-Item -LiteralPath $prev -Force
    & $log 'info' 'golden: removed win11-runner.prev.vhdx (rust, app and model are green on the current image)'
} catch {
    try { & $log 'error' "golden: prune failed: $_" } catch { }
}
exit 0
```

- [ ] **Step 4: Register the task and the reset**

In `setup-host.ps1`, in step 9, between `Write-Host '9/9 scheduled tasks'` and `if ($keepAccount) {`, add (it runs on every setup, kept account or not: the task holds no password, so `-Force` re-registers it idempotently and an install that predates it gains it on a plain re-run; `$taskNames` stays the password-bound pair):

```powershell
# The previous golden image is deleted by an elevated task on the
# orchestrator's signal (install/prune-golden.ps1): the account the loop
# runs as has read and execute on golden only, by design. Registered on
# every run: it holds no password, so it is outside the keepAccount pair.
Register-ScheduledTask -TaskName 'slate-ci-golden-prune' -Force `
    -Action (New-ScheduledTaskAction -Execute $msiPwsh -Argument "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File `"$Root\bin\install\prune-golden.ps1`" -Root `"$Root`"" -WorkingDirectory "$Root\bin") `
    -Trigger (New-ScheduledTaskTrigger -Daily -At 4am) -User 'NT AUTHORITY\SYSTEM' -RunLevel Highest `
    -Settings (New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Minutes 10) -StartWhenAvailable) | Out-Null
```

Note the `robocopy /MIR … /XD tests` mirror already copies `install\prune-golden.ps1` into `bin`, and the closing `'Host setup complete. Next:'` message gains one line: `the previous golden image is pruned daily at 04:00 by slate-ci-golden-prune once the loop has accepted the current one`.

- [ ] **Step 5: Rotate in the build**

In `build-golden.ps1`: add `[switch]$Rotate` to `param(...)` (after `$TimeoutMinutes`), and replace

```powershell
if (Test-Path -LiteralPath $OutPath) { throw "$OutPath exists; move it aside to rebuild" }
```

with

```powershell
if ($Rotate -and (Test-Path -LiteralPath $OutPath)) {
    # The current image becomes the rollback; an older rollback goes. This
    # is the one elevated moment in a refresh, so the rotation lives here.
    $prevPath = Join-Path (Split-Path -Parent $OutPath) 'win11-runner.prev.vhdx'
    if (Test-Path -LiteralPath $prevPath) {
        Set-ItemProperty -LiteralPath $prevPath -Name IsReadOnly -Value $false
        Remove-Item -LiteralPath $prevPath -Force
    }
    Rename-Item -LiteralPath $OutPath -NewName 'win11-runner.prev.vhdx'
    Write-Host "Rotated the current image to $prevPath"
}
if (Test-Path -LiteralPath $OutPath) { throw "$OutPath exists; move it aside to rebuild (or pass -Rotate)" }
```

- [ ] **Step 6: Run the whole Pester suite to verify it passes**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1`
Expected: all green, including the three prune scenarios against the fake tree.

- [ ] **Step 7: Commit**

```bash
git add ci/windows-runner/install ci/windows-runner/golden/build-golden.ps1 ci/windows-runner/tests
git commit -m "feat(ci-host): an elevated daily task prunes the previous golden image on the orchestrator's signal; build-golden -Rotate

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 9: The weekly branch report, deleting only what `main` contains, on dispatch

**Files:**
- Create: `.github/workflows/branch-cleanup.yml`
- Modify: `ci/windows-runner/tests/Workflows.Tests.ps1` (a guard describe)

**Interfaces:**
- Produces: the workflow (schedule Mondays 06:00 UTC; `workflow_dispatch` input `delete`, boolean, default false); its step summary table; deletions only when `delete` is true, the name starts with `claude/`, `ahead_by` is 0 and the branch is the base of no open pull request.

- [ ] **Step 1: Write the failing guard**

Append to `ci/windows-runner/tests/Workflows.Tests.ps1`:

```powershell
Describe 'branch-cleanup.yml' {
    BeforeAll { $script:cleanup = Get-Content -Raw (Join-Path $wf 'branch-cleanup.yml') }
    It 'reports weekly and deletes only on an explicit dispatch, only claude/* tips main already contains' {
        $cleanup | Should -Match '(?m)^    - cron: ''0 6 \* \* 1'''
        $cleanup | Should -Match '(?m)^      delete:\r?\n        description:'
        $cleanup | Should -Match '(?m)^        type: boolean\r?\n        default: false'
        $cleanup | Should -Match '(?m)^  contents: write\r?\n  pull-requests: read'
        $cleanup | Should -Match 'refs/heads/claude/'
        $cleanup | Should -Match 'compare/main\.\.\.'
        $cleanup | Should -Match 'ahead_by'
        $cleanup | Should -Match '--base "\$branch"'
        $cleanup | Should -Match ([regex]::Escape('[[ "$branch" == claude/* ]]'))
        $cleanup | Should -Match ([regex]::Escape("inputs.delete == 'true'"))
        $cleanup | Should -Match 'GITHUB_STEP_SUMMARY'
        $cleanup | Should -Not -Match 'force'
    }
}
```

- [ ] **Step 2: Run the guard to verify it fails**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1 -Filter 'branch-cleanup.yml.*'`
Expected: fails: the file does not exist.

- [ ] **Step 3: Write the workflow**

Create `.github/workflows/branch-cleanup.yml`:

```yaml
# Stale claude/* branches: a weekly report, and a delete only when asked.
# The repository already deletes merged pull-request branches, so the
# claude/* branches that remain are unmerged work or never-reviewed WIP.
# Nothing here deletes a tip main does not already contain, or a branch an
# open pull request is based on. Spec:
# docs/superpowers/specs/2026-10-11-windows-overflow-routing-design.md §9.

name: Branch cleanup

on:
  schedule:
    - cron: '0 6 * * 1'
  workflow_dispatch:
    inputs:
      delete:
        description: Delete the claude/* branches whose tip main already contains (never anything else).
        type: boolean
        default: false

permissions:
  contents: write
  pull-requests: read

concurrency:
  group: branch-cleanup
  cancel-in-progress: false

jobs:
  report:
    name: Report stale claude/* branches
    runs-on: ubuntu-latest
    timeout-minutes: 10
    env:
      GH_TOKEN: ${{ github.token }}
      REPO: ${{ github.repository }}
      DELETE: ${{ github.event_name == 'workflow_dispatch' && inputs.delete == 'true' && 'true' || 'false' }}
    steps:
      - name: List, classify and (on dispatch) delete
        run: |
          set -euo pipefail
          {
            echo "## claude/* branches ($(date -u +%Y-%m-%dT%H:%MZ), delete=$DELETE)"
            echo
            echo "| branch | ahead of main | last commit | open PR as head | open PR as base | action |"
            echo "|---|---|---|---|---|---|"
          } >> "$GITHUB_STEP_SUMMARY"
          gh api "repos/$REPO/git/matching-refs/heads/claude/" --paginate --jq '.[].ref' | sed 's#^refs/heads/##' | while read -r branch; do
            [[ "$branch" == claude/* ]] || { echo "::error::unexpected branch name $branch"; exit 1; }
            ahead=$(gh api "repos/$REPO/compare/main...$branch" --jq '.ahead_by')
            last=$(gh api "repos/$REPO/branches/$branch" --jq '.commit.commit.committer.date')
            as_head=$(gh pr list --repo "$REPO" --state open --head "$branch" --json number --jq 'length')
            as_base=$(gh pr list --repo "$REPO" --state open --base "$branch" --json number --jq 'length')
            action="kept"
            if [ "$ahead" = "0" ] && [ "$as_base" = "0" ]; then
              if [ "$DELETE" = "true" ]; then
                gh api -X DELETE "repos/$REPO/git/refs/heads/$branch"
                action="deleted (main contains its tip)"
              else
                action="deletable (main contains its tip)"
              fi
            elif [ "$ahead" != "0" ]; then
              action="kept: $ahead commit(s) main does not have"
            else
              action="kept: base of an open pull request"
            fi
            echo "| $branch | $ahead | $last | $as_head | $as_base | $action |" >> "$GITHUB_STEP_SUMMARY"
            echo "$branch: $action"
          done
```

- [ ] **Step 4: Run the guard and a dry run**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1 -Filter 'branch-cleanup.yml.*'`
Expected: passes. Then, after the branch is pushed, `gh workflow run branch-cleanup.yml --ref <this branch>` is a dry run (default `delete=false`); its summary must list the four branches of 2026-10-11 as kept with their ahead-by counts and nothing deleted.

- [ ] **Step 5: Commit**

```bash
git add .github/workflows/branch-cleanup.yml ci/windows-runner/tests/Workflows.Tests.ps1
git commit -m "ci: a weekly report of stale claude/* branches that deletes only what main contains, on dispatch

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 10: Documents

**Files:**
- Modify: `docs/runbooks/self-hosted-windows-runner.md` (sections "Pool switch (fallback and return)", "Daily operation", "Refreshing the golden image", "Rolling back a refresh", "Security model in one screen", "Observed timings")
- Modify: `docs/superpowers/specs/2026-10-10-self-hosted-windows-runner-design.md` (D-3, §5, §6, "Out of scope")

**Interfaces:** none; prose only. Every command below is one the owner can paste.

- [ ] **Step 1: Rewrite "Pool switch (fallback and return)"**

Replace the section's body (from `The repository variable \`WINDOWS_RUNNER_MODE\` routes` to the paragraph ending `(see "Before you start" under Install).`) with:

```markdown
The repository variable `WINDOWS_RUNNER_MODE` routes `windows.yml` and
`nightly.yml`. In `windows.yml` a routing job (`Route Windows lanes`,
`ci/windows-runner/route/route.py`) reads it and the home pool's queue
when each run starts, and decides a pool per lane; the shell gate runs on
GitHub-hosted `windows-latest` in every mode but `home-only`.

- `home`: home first. A lane takes a free slot; when none is free it queues
  at home only if it would still finish inside the run's critical path (the
  app lane plus the shell gate), and otherwise runs on Namespace at once.
  The lanes are considered in vendor-cost order: app build + test, model
  shard 0, model shard 1, rust tests. With cold caches an idle pool keeps
  app, shard 0 and rust home and pays Namespace about six minutes for shard
  1; with warm caches it pays nothing. A host that leaves an admissible job
  queued for five minutes while a slot is idle, or one that is down, sends
  every lane to Namespace by itself and resumes when it takes its queue
  again. The nightly stress job still queues home unconditionally.
- `home-only`: every lane queues home, as `home` did before 2026-10-11. For
  validating a golden refresh and for fidelity runs.
- `namespace`: the 2026-07-31 arrangement, verbatim, with no routing call.
  The rust, app and model lanes run on the Namespace profile
  `namespace-profile-winx64-fast` (`-fast-pr` off `main`), with the cache
  tags `slate-windows-rust`, `slate-windows-app` and `slate-windows-model`.
  The shell gate and the nightly stress job run on `windows-latest`.
- Any other value, a typo or an unset variable included, means `namespace`.
  The comparison ignores case and surrounding spaces, so `Home` also
  selects this host; `refs/heads/main` is compared exactly.

Reading a run's routing: the routing job's step summary has the mode, the
occupancy it saw (in progress, queued, stuck, foreign, not ours, the oldest
queued age, free slots, whether the pool is serving, the deadline), a row
per lane with its pool, label and reason, and the listing the decision
was made on. `ROUTE_JOBS_JSON=<that listing saved to a file>` replays it
by hand. Each routed lane's first step prints `runner=… environment=…
routed=…`, and the aggregate gate's failure line names the pools.

Fork pull requests never route home: the router sends them to the vendor,
and the host refuses any job whose run comes from another repository
(`admission: skipped job <id> of <repo> (not the trusted repository)`),
so a fork that edits the workflow or the router gains nothing. Every fork
pull request still waits for your approval first (see "Before you start"
under Install).

The pilot, `windows-ci-pilot.yml`, ignores the variable. Its `runner` input
picks the pool, and `home` is one of the choices. On `home`, its `cache`
input only switches the GitHub-scoped cache restore. `cache=false` is not a
cold build there: `CARGO_TARGET_DIR` and `NUGET_PACKAGES` live on the
lane's warm cache disk. A dispatch on `main` is also a trusted run, so the
host commits its cache fork like any other. Dispatch the pilot from a
branch unless you mean to warm the caches.

Fallback, when the host is down, being rebuilt or misbehaving:

```powershell
gh variable set WINDOWS_RUNNER_MODE --body namespace --repo coryj627/slate
```

Return:

```powershell
gh variable set WINDOWS_RUNNER_MODE --body home --repo coryj627/slate
```

Confirm after either flip:

```powershell
gh variable get WINDOWS_RUNNER_MODE --repo coryj627/slate
```

Jobs already queued for `slate-win-*` keep waiting for the home pool for up
to 24 hours; the router cannot move them. Cancel their run, then use
**Re-run all jobs**, never "Re-run failed jobs": a partial re-run reuses
the routing job's earlier answer, and the lanes then take the vendor
expression rather than the stale home label, but a cancelled lane's run
must start afresh to be routed again. While a stranded lane waits, the
`main` concurrency group holds later `main` runs behind it. The flip never
touches caches on either side.

Capacity: the router's `ROUTE_SLOTS` (default 2, in
`ci/windows-runner/route/route.py`) and `Slots` in
`ci/windows-runner/config.json` describe the same pool; change both in one
pull request, then re-run `setup-host.ps1` so the host's copy follows.
```

- [ ] **Step 2: Extend "Daily operation"**

In the "Logs" bullet, after item 5, add a sixth item and a note:

```markdown
  6. After a green job on a VM created after the current golden's seal, the
     lane counts as accepted on that image; once rust, app and model all
     do and no older VM exists:
     `golden: the previous image is no longer needed (rust, app and model are green on the current one)`.
     The `slate-ci-golden-prune` task (SYSTEM, daily at 04:00) then deletes
     `win11-runner.prev.vhdx` and logs
     `golden: removed win11-runner.prev.vhdx (rust, app and model are green on the current image)`,
     or why it stayed. Lines starting `admission: skipped job` name jobs of
     other repositories the host refused; lines starting `logs:` are the
     once-a-day pruning of files older than `LogRetentionDays` (30).
```

In the "State" bullet, after "lists the active VMs, retries and seen jobs", add: ", the day the logs were last pruned (`LogsPrunedOn`), and which lanes went green on the current golden (`GoldenAccepted`, `GoldenPrevRemovableAt`)".

- [ ] **Step 3: Rewrite the refresh steps 3, 5, 8 and 9 and the rollback**

Step 3 becomes:

```markdown
3. Nothing to move: the build rotates the current disk to
   `win11-runner.prev.vhdx` itself (step 5), replacing an older rollback.
```

Step 5's command becomes `.\ci\windows-runner\golden\build-golden.ps1 -Rotate -IsoPath C:\Users\cory\Downloads\Windows11_Client_x64_en-us_26300_9457.iso`.

Step 8 becomes:

```markdown
8. Set the pool to `home-only`, so every lane of the check runs on the new
   image, then dispatch `windows.yml` on a branch:

   ```powershell
   gh variable set WINDOWS_RUNNER_MODE --body home-only --repo coryj627/slate
   gh workflow run windows.yml --ref <branch> --repo coryj627/slate
   ```
```

Step 9 becomes:

```markdown
9. Once that run is green, set the pool back to `home`. The old disk goes
   by itself: the loop logs `golden: the previous image is no longer
   needed` once rust, app and model have each gone green on a VM created
   after the new seal, and the `slate-ci-golden-prune` task deletes it at
   04:00. To delete it sooner, run the task by hand:

   ```powershell
   Start-ScheduledTask -TaskName slate-ci-golden-prune
   ```
```

In "Rolling back a refresh", step 1's first sentence becomes "If step 8 set the pool to `home-only`, set it to `namespace`." and step 6 becomes "Set the pool back to `home`." Add after step 4: "The prune task cannot remove a rollback you have just restored: its signal is cleared the moment the loop sees a different golden stamp."

- [ ] **Step 4: Security table and timings**

In "Security model in one screen", add two rows: `slate-ci-golden-prune` task — SYSTEM, daily, deletes one fixed path on the orchestrator's signal; the routing job — hosted Linux, the run's own token with `actions: read` and `contents: read`, reads a public listing, decides only between two trusted targets. In "Observed timings", under the `windows.yml` first-green section, add the shell gate figures on `windows-latest` (10 min 12 s, 12 min 49 s, 14 min 6 s on 2026-10-10) and the sentence "The shell gate runs on `windows-latest` in every mode but `home-only` since 2026-10-11: free and faster."

- [ ] **Step 5: The first spec**

In `docs/superpowers/specs/2026-10-10-self-hosted-windows-runner-design.md`: D-3's text becomes "- **D-3 Fallback:** manual at first; automatic since the overflow routing design of 2026-10-11 (`2026-10-11-windows-overflow-routing-design.md`), which also decides per lane between the pool and the vendor." In §5, after the table, add "Superseded on 2026-10-11 for `windows.yml`: a routing job sets each lane's `runs-on`; the table's expressions remain the vendor fallback inside them, verbatim. The fork-PR allowance of D-5 there is withdrawn: the host refuses jobs of other repositories at admission." In "Out of scope", replace "- Automatic fallback (D-3)." with "- Automatic fallback (D-3): done on 2026-10-11, see the overflow routing design."

- [ ] **Step 6: Run the guards (they read the runbook for nothing, but the suite must stay green) and commit**

Run: `pwsh -NoProfile -NonInteractive -File ci/windows-runner/tests/Invoke-Tests.ps1`
Expected: all green.

```bash
git add docs/runbooks/self-hosted-windows-runner.md docs/superpowers/specs/2026-10-10-self-hosted-windows-runner-design.md
git commit -m "docs(runbooks): overflow routing — the four modes, reading a run's routing, re-run all jobs, the rotated golden and its prune task

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Integration pass (after merge, owner-driven; from the spec's §10)

1. Merge; on the host `git pull --ff-only origin main` and re-run `setup-host.ps1` (registers `slate-ci-golden-prune`, refreshes `bin`), then `Stop-ScheduledTask -TaskName slate-ci-orchestrator` so the loop relaunches on the new code.
2. With `WINDOWS_RUNNER_MODE=home` and one run in flight, dispatch a second `windows.yml` run on a branch and read both routing summaries: the second run's app lane went to the vendor, its shards queued or overflowed as the deadline says, its rust lane queued home.
3. Stop the loop (`Disable-ScheduledTask`, `Stop-ScheduledTask`), dispatch a run, and read that its summary says the pool is not serving within the five-minute rule; re-enable the loop.
4. Dispatch `branch-cleanup.yml` without `delete`; read the report.
5. On the next golden refresh, follow the rewritten steps and read the acceptance line and the prune line.
