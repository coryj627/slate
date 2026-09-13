#!/usr/bin/env python3
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Verify the complete Connections model census across independent CI jobs."""

import argparse
import json
import math
from pathlib import Path
import re
import sys


# Independent gate pins: a deliberate model expansion updates these alongside
# the C# model's total, named exclusions and driven count.
CENSUSES = {
    "routes": (25200, 12812, 12388),
    "reroot": (5760, 5216, 544),
    "composed": (196, 59, 137),
}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def milliseconds(value, label):
    require(type(value) in (int, float) and math.isfinite(value) and value >= 0,
            f"{label}: expected finite, nonnegative milliseconds")
    return value


def verify(paths, shard_count):
    """Return per-family timing summaries; fail closed on incomplete evidence."""
    require(type(shard_count) is int and 0 < shard_count <= 137,
            "shard count must be an integer from 1 to 137")
    reports = {}
    for path in paths:
        report = json.loads(Path(path).read_text(encoding="utf-8-sig"))
        require(type(report) is dict, f"{path}: expected a report object")
        family = report.get("family")
        require(type(family) is str and family in CENSUSES,
                f"{path}: unknown report family")
        index = report.get("shardIndex")
        require(type(index) is int and 0 <= index < shard_count,
                f"{path}: invalid shard index")
        label = f"{family} shard {index}"
        key = (family, index)
        require(key not in reports, f"{label}: duplicate report")
        require(type(report.get("schemaVersion")) is int and report["schemaVersion"] == 1,
                f"{label}: unsupported report schema")
        require(type(report.get("shardCount")) is int and report["shardCount"] == shard_count,
                f"{label}: inconsistent shard count")
        require(report.get("success") is True, f"{label}: model did not succeed")
        for field, expected in zip(
                ("totalCells", "unreachableCells", "reachableCells"), CENSUSES[family]):
            require(type(report.get(field)) is int and report[field] == expected,
                    f"{label}: {field} does not match the pinned census ({expected})")
        digest = report.get("inventorySha256")
        require(type(digest) is str and re.fullmatch(r"[0-9a-fA-F]{64}", digest),
                f"{label}: invalid inventory digest")
        expected_ordinals = list(range(index + 1, CENSUSES[family][2] + 1, shard_count))
        for field in ("selectedOrdinals", "completedOrdinals"):
            ordinals = report.get(field)
            require(type(ordinals) is list and all(type(item) is int for item in ordinals)
                    and ordinals == expected_ordinals,
                    f"{label}: {field} does not match its complete ordered partition")
        milliseconds(report.get("elapsedMilliseconds"), label)
        routes = report.get("routes")
        require(type(routes) is list and routes, f"{label}: missing route timings")
        names = set()
        route_cases = 0
        for route in routes:
            require(type(route) is dict, f"{label}: invalid route timing")
            name = route.get("route")
            require(type(name) is str and name and name not in names,
                    f"{label}: invalid or duplicate route name")
            names.add(name)
            cases = route.get("cases")
            require(type(cases) is int and cases > 0, f"{label}: invalid route case count")
            route_cases += cases
            milliseconds(route.get("elapsedMilliseconds"), f"{label}/{name}")
            phases = route.get("phases")
            require(type(phases) is dict and phases, f"{label}/{name}: missing phase timings")
            for phase, elapsed in phases.items():
                require(type(phase) is str and phase, f"{label}/{name}: invalid phase name")
                milliseconds(elapsed, f"{label}/{name}/{phase}")
        require(route_cases == len(expected_ordinals), f"{label}: route counts do not cover the partition")
        reports[key] = report

    expected_keys = {(family, index) for family in CENSUSES for index in range(shard_count)}
    require(set(reports) == expected_keys, f"missing model reports: {sorted(expected_keys - set(reports))}")
    summaries = []
    for family, (_, _, reachable) in CENSUSES.items():
        siblings = [reports[family, index] for index in range(shard_count)]
        require(len({report["inventorySha256"].lower() for report in siblings}) == 1,
                f"{family}: shard inventories differ")
        # Exact per-shard equality above also proves no duplicates. Keep the
        # union proof explicit so the aggregate cannot accept a partial census.
        completed = sorted(ordinal for report in siblings for ordinal in report["completedOrdinals"])
        require(completed == list(range(1, reachable + 1)), f"{family}: incomplete or overlapping coverage")
        summaries.append({
            "family": family, "cases": reachable,
            "shardSeconds": [round(report["elapsedMilliseconds"] / 1000, 3) for report in siblings],
        })
    return summaries


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reports-dir", type=Path, required=True)
    parser.add_argument("--shard-count", type=int, required=True)
    parser.add_argument("--summary", type=Path)
    args = parser.parse_args()
    try:
        summaries = verify(sorted(args.reports_dir.rglob("*.json")), args.shard_count)
    except (OSError, ValueError) as error:
        print(f"Model coverage verification failed: {error}", file=sys.stderr)
        return 1
    lines = ["### Windows model coverage", "", "| Family | Cases | Seconds per shard |",
             "| --- | ---: | --- |"]
    for summary in summaries:
        durations = ", ".join(f"{seconds:.1f}" for seconds in summary["shardSeconds"])
        lines.append(f"| {summary['family']} | {summary['cases']} | {durations} |")
    lines.extend(["", f"All {sum(item['cases'] for item in summaries):,} scenarios completed exactly once.", ""])
    output = "\n".join(lines)
    print(output)
    if args.summary:
        with args.summary.open("a", encoding="utf-8") as destination:
            destination.write(output)
    return 0


if __name__ == "__main__":
    sys.exit(main())
