# U5 — Iconography & presentation polish

**Goal.** The finishing pass. Apply the `SlateSymbol` language everywhere, adopt the macOS 26 look where available, and take a deliberate run at layout clarity, density, emphasis, and dark/light balance across every new surface — then verify the whole shell against the presentation-ready bar end to end. This is what turns "functionally complete" into "you can demo it."

**Depends on:** U3, U4 (polish over the finished surfaces). **Parallel:** none (last).

**Milestone-level risk:** low individually, but this is the gate that decides whether the program actually reads as presentation-ready. Budget real time for the verification sweep (U5-4) — it will surface issues the feature milestones didn't.

## Issues

### U5-1 · Mac UI: apply `SlateSymbol` across all surfaces + macOS 26 styling `swift-ui` `design`
- Sweep every remaining raw symbol/`Label` through the semantic layer; adopt macOS 26 control/material styling (e.g. Liquid Glass) behind `if #available(macOS 26, *)` with a clean macOS 15–25 fallback.
- **DoD focus:** DoD §B in full — consistent size/weight/rendering-mode per surface, recognizable metaphors, no unlabeled icon-only control.
- **Acceptance:** no raw SF Symbol strings remain; the app adopts macOS 26 styling on Tahoe and degrades cleanly below it.

### U5-2 · Mac UI: layout, density, typography & emphasis polish `swift-ui` `design`
- A deliberate pass on visual hierarchy, spacing rhythm (to the token grid), alignment, density (Obsidian-grade information density without clutter), and emphasis (primary/secondary/destructive control styling). Define rest/hover/pressed/selected/focused/disabled states everywhere. Tighten every empty/loading/error state.
- **DoD focus:** DoD §A + §C in full.
- **Acceptance:** each primary surface (tree, tabs, editor+properties, reading view, leaves, rail) reads as considered and finished; hierarchy is obvious; all interactive states are defined and visible; focus is always visibly indicated.

### U5-3 · Mac UI: dark + light mode correctness pass `swift-ui` `a11y` `design`
- Audit every new surface in both appearances; eliminate any hardcoded/appearance-leaking color; re-measure APCA in both modes for the tightest pairings.
- **DoD focus:** DoD §D in full — zero literals, balanced in both appearances, APCA Lc ≥ 75 measured (not eyeballed) in **both**.
- **Acceptance:** every U0–U4 surface is correct and balanced in light and dark; measured Lc for the tightest pairs recorded in the PR.

### U5-4 · Test/a11y: presentation-ready verification sweep `test` `a11y` `benchmark`
- End-to-end pass over the whole new shell: VoiceOver walkthrough (open vault → tree navigate → open tabs → split → read/edit toggle → edit properties → switch leaves → file management), Dynamic Type at XXL, Reduce Motion, keyboard-only traversal. Run the benchmark suite; record baselines in `BENCHMARKS.md`; confirm no keystroke/scroll/interaction regressions vs. the U-program budgets.
- **Acceptance:** the documented end-to-end VoiceOver + keyboard-only script completes with no dead ends; Dynamic Type and Reduce Motion clean; benchmarks within budget. This is the milestone's — and the program's — close-out gate.
