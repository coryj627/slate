# Milestone 14 brief — vendored snapshot (normative anchor)

This is the GitHub [milestone 14](https://github.com/coryj627/slate/milestone/14) description as of 2026-07-06, vendored so that every spec reference to "the milestone-14 test list" or "the milestone-14 accessibility checkpoints" points at **versioned repo content**, not a mutable GitHub field. Where the program/specs deliberately diverge from this brief, the divergence is recorded in [`specs/gap_analysis.md`](specs/gap_analysis.md) — per [`../06_v1_milestones.md`](../06_v1_milestones.md), the in-repo program supersedes this text where they differ.

The two subsections below are the ones specs cite as normative; the remainder of the milestone description (user-facing capability list, Rust/Swift work sketches, schema, DoD, tester-feedback questions) is superseded in full by the program + specs and is not reproduced.

## Tests (cited by n1/n2 specs as "the milestone-14 test list")

- Unit (Rust): `.base` round-trip on Obsidian fixture corpus (byte-equal preservation).
- Unit (Rust): query AST coverage — every operator and property per §8.2.
- Unit (Rust): formula evaluator per named function with edge cases (empty input, type coercion, division by zero, missing properties).
- Unit (Rust): `groupBy` ordering stability; summary correctness per default summary.
- Unit (Rust): query cancellation under load (10k-file vault).
- XCTest: data grid keyboard matrix — arrow / Home / End / Page Up/Down / sort affordance.
- XCTest: filter builder composition round-trips to the same AST as direct query input.
- Benchmark: large-vault query performance hits the V1 release-gate target from `05` §9.5.
- Integration: open an Obsidian vault with `.base` files; queries render correctly; saved queries persist.

## Accessibility checkpoints (cited by n3/n4 specs; per 05 §8.7)

- Column header announcement on column move (`"Column: Title, sortable, current sort: ascending"`).
- Cell announcement on cell move (`"<column header>: <cell value>"`).
- Summary row separately addressable from data rows.
- Filter builder is structured navigation (chips + boolean joiners), not a free-text editor — VoiceOver hears each filter as a structured node.
- Sort change announces `"Sorted by <column>, <direction>"`.
- Saved-query pin reads as `"<query name>, saved query"`.
- Dashboards surface as a heading hierarchy + grid hierarchy, not flat blobs.
