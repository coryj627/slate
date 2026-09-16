#!/usr/bin/env python3
"""Generate a linked vault large enough to cross the graph's tier-B
boundary, for the human AT checklist's item 3 ("Large graph", one
`GraphTierSummary` element in place of per-node peers) — the same shape
`GraphEndToEndTests` builds for its 1,501-node vault, at a path you
choose so it can be opened in the shipped build.

    python scripts/make_graph_tier_b_vault.py <directory> [--notes 1500]

Every note links to the next one and to the hub, so the graph is one
component with a clear most-linked node. The hub is one more node, so
the default 1,500 notes make 1,501 visible nodes — one past tier A's
ceiling (core's BARNES_HUT_THRESHOLD, 1,500) — while `--notes 1499`
makes exactly 1,500 and stays in tier A for the comparison. The
directory must not already exist. Standard library only.
"""
from __future__ import annotations

import argparse
import pathlib
import sys


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("directory", help="where to create the vault (must not exist)")
    parser.add_argument("--notes", type=int, default=1500, help="how many notes besides the hub (default 1500: 1,501 nodes, one past tier A's ceiling; 1499 stays in tier A)")
    args = parser.parse_args()
    root = pathlib.Path(args.directory)
    if args.notes < 2:
        print("refusing: at least two notes", file=sys.stderr)
        return 1
    # One step, not a check and then a create: an existing directory, an
    # invalid path or a permission refusal each come back as a message,
    # never a traceback.
    try:
        root.mkdir(parents=True, exist_ok=False)
    except FileExistsError:
        # The target itself, or a parent that is a file (Windows reports
        # both the same way).
        what = "already exists" if root.exists() else "cannot be created: a parent is a file"
        print(f"refusing: {root} {what}", file=sys.stderr)
        return 1
    except OSError as error:
        print(f"refusing: cannot create {root}: {error}", file=sys.stderr)
        return 1
    width = len(str(args.notes))
    for i in range(1, args.notes + 1):
        name = f"note-{i:0{width}d}"
        nxt = f"note-{(i % args.notes) + 1:0{width}d}"
        body = (
            f"# {name}\n\n"
            f"Note {i} of {args.notes} in the tier-B vault. Next: [[{nxt}]]. Hub: [[hub]].\n"
        )
        (root / f"{name}.md").write_text(body, encoding="utf-8", newline="\n")
    hub = "# hub\n\nThe hub every note links to; it links to the first and last notes.\n\n" \
          f"[[note-{1:0{width}d}]] · [[note-{args.notes:0{width}d}]]\n"
    (root / "hub.md").write_text(hub, encoding="utf-8", newline="\n")
    total = args.notes + 1
    tier = "B (Large graph: one summary element)" if total > 1500 else "A (per-node peers)"
    print(f"wrote {total} notes to {root} — {total} visible nodes, tier {tier}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
