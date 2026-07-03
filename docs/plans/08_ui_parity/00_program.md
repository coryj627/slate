# 08 — UI Parity Program (Milestone U): Obsidian-parity, presentation-ready macOS workspace

**Status:** 📋 Planned (2026-07-03). Next stream after L/K close; sequenced **ahead of** Canvas (T) and Graph (P) per owner decision 2026-07-03.

**Strategic goal.** Slate's functionality and its accessibility are in a decent place. This program brings the *presentation* to parity with what users expect from an app like Obsidian — a tabbed, split-pane workspace; an in-note properties experience; a real reading/editing split; a proper file tree with folder management; docked right-hand panels; and a coherent macOS 26 / SF Symbols v7 visual language — **without ceding one inch of the accessibility bar**, and while raising the app to a **presentation-ready** finish across visual polish, dark/light modes, reliability, and performance.

"Presentation-ready" is the operative phrase: at the end of Milestone U, Slate should be something you can put in front of a stranger, a reviewer, or a conference audience and have it read as a finished, considered product in both light and dark mode — not a functional prototype.

---

## The six scope decisions (locked 2026-07-03)

| # | Feature | Decision |
|---|---------|----------|
| 1 | Tabbed UI | **Tabs + split panes** (no pop-out windows in v1). Extensible tab-content types so future editor kinds slot in. |
| 2 | Properties in-note | **Rich, editable widget at the top of the note in both modes**, with a **"show source" YAML toggle**. |
| 3 | Preview / edit | **Reading ↔ Editing mode toggle** — Reading is a fully rendered, navigable read-only view; Editing is the source `NSTextView`. Each mode is one coherent accessibility tree. |
| 4 | Left-pane functions | **Docked right pane with a vertical icon rail** (workspace "leaves"); bottom-left utilities become icon buttons. |
| 5 | Iconography | **macOS 15 minimum**; adopt **SF Symbols v7** / macOS 26 styling, with fallbacks for macOS 15–25. |
| 6 | File list | **Tree, with full file management** — create / move / rename folders, and link-integrity rewriting on move. |

---

## Milestone map & dependencies

```
U0 Baseline ──┬──▶ U1 Shell (tabs + splits) ──┬──▶ U3 Editor modes ────────┐
              │                                └──▶ U4 Right-pane leaves ──┴──▶ U5 Presentation polish
              └──▶ U2 File tree (parallel with U1) ──────────────────────────▶
```

| ID | Milestone | Depends on | Runs parallel with | Primary surfaces |
|----|-----------|-----------|--------------------|------------------|
| **U0** | Baseline & design foundation | — | — | Deployment target, `SlateSymbol` icon layer, design tokens, test harness |
| **U1** | Workspace shell: tabs + split panes | U0 | U2 | `WorkspaceModel`, tab bar, split panes, `MainSplitView` migration |
| **U2** | File tree + full file management | U0 | U1 | slate-core directory API + mutations + link-rewrite, `FileTreeSidebar` |
| **U3** | Editor: reading/editing + inline properties | U1 | U4 | Reading view, mode toggle, in-note properties widget |
| **U4** | Right-hand leaves + utility rail | U1 | U3 | Leaf container + icon rail, panel port, utility icons |
| **U5** | Iconography & presentation polish | U3, U4 | — | Icon application, layout/density polish, dark/light pass, verification sweep |

**Execution order:** `U0 → (U1 ∥ U2) → (U3 ∥ U4) → U5`. U1 (center workspace) and U2 (left sidebar + core) touch disjoint surfaces and can run concurrently in separate worktrees.

---

## Relationship to existing milestones (do not duplicate)

- **Milestone N — Bases v1** (Obsidian "database files"). U1's `EditorItem` type **reserves a `.base` case** so a Bases document opens as a tab type; N implements the editor, U1 provides the shell that hosts it. Same for **T — Canvas** (`.canvas`) and **P — Graph** (`.graph`). U1 is the seam that lets T/P/N plug in as tab kinds rather than bespoke windows.
- **Milestone R — Themes and dark-mode polish.** R owns the *theming system* (user-selectable themes, theme tokens, customization). Milestone U does **not** build a theming system; instead, **every U issue must ship correct in the built-in light and dark appearances** as a hard gate (see DoD §D). U0's design tokens are authored so R can later re-skin them. If U work surfaces a themable value, it goes through a token, never a literal.
- **Milestone Q — Command palette.** New file/tab/mode/leaf actions register in the existing command registry (`SlateCommands.swift`, `CommandRegistryTests`) so they are reachable by keyboard and mirrored in the palette. No parallel command surface.

---

## Presentation-Ready Definition of Done (applies to EVERY issue)

This is the standing bar. An issue is not done until it satisfies the dimensions relevant to it. Each issue body carries a condensed checklist; this section is the normative reference. `a11y-check 100/100` and a green test suite are necessary but **not sufficient** — the visual and experiential bar below is co-equal.

### A. Layout & information architecture
- The surface has an obvious visual hierarchy: the eye lands on the primary content first, then secondary, then chrome. Nothing competes for attention that shouldn't.
- Regions are predictable and labeled; a user can build a mental model in seconds. Related controls are grouped; unrelated ones are separated by real whitespace, not guesswork.
- Everything aligns to the design-token spacing grid (U0-3). No off-grid one-off paddings.
- Every stateful surface defines **empty, loading, error, and populated** states — none is a blank rectangle. (Matches the existing `FileListSidebar` / `NoteContentView` state discipline.)

### B. Iconography
- Icons come from the `SlateSymbol` semantic layer (U0-2) — no raw `Image(systemName:)` at call sites. This guarantees consistent SF Symbols v7 usage with macOS 15–25 fallbacks.
- Each icon earns its place: the metaphor is recognizable and conventional (a magnifying glass is search, not filter). Where a metaphor is ambiguous, it is paired with a text label, not left to the icon alone.
- Icons are visually consistent in size, weight, and optical alignment within a control group. Rendering mode (monochrome / hierarchical / palette) is chosen deliberately and is consistent per surface.
- **No icon-only control ships without an accessible label** (see §E). Icon + text is the default for primary actions; icon-only is reserved for dense, conventional affordances (toolbar, rail) that also carry `help` tooltips.

### C. Emphasis & control styling
- Emphasis is expressed through the token system — type scale, weight, color role, and control prominence — never ad-hoc hex or font sizes.
- Action hierarchy is legible: primary, secondary, and destructive actions are visually distinct and consistent with the rest of the app (the existing alert button conventions in `MainSplitView` are the reference).
- Interactive states are all defined and visible: rest, hover, pressed, selected, focused, disabled. Keyboard focus is **always** visibly indicated (focus ring or equivalent), never suppressed.
- Motion is subtle, purposeful, and short; it clarifies a state change rather than decorating it (and is gated by Reduce Motion — §E).

### D. Dark + light mode (hard gate)
- Every color is a semantic token or a system dynamic color. **Zero hardcoded appearance-specific colors** at call sites (the `NSColor.textColor` nil-gotcha is the cautionary tale — set dynamic colors explicitly).
- The surface is visually correct and balanced in **both** appearances: no washed-out text, no black-on-near-black, no light-mode assumptions leaking into dark.
- Contrast is measured (not eyeballed) in **both** appearances and meets the project standard **APCA Lc ≥ 75** (project uses APCA-W3 G-4g, not WCAG 4.5:1). Include the measured Lc for the tightest pairs in the PR.

### E. Accessibility (the non-negotiable floor)
- VoiceOver: every element has a correct label, value, and traits; reading order matches visual order; new containers scope children correctly (`.contain` vs `.combine` chosen deliberately).
- Keyboard parity: every action is reachable and operable by keyboard alone. **No drag-only or mouse-only affordance** — drag is an enhancement whose keyboard/command equivalent is the source of truth.
- Focus management: focus moves predictably on open/close/navigate; focus is returned on dismissal (WCAG 2.4.3); no keyboard traps (2.1.2).
- Dynamic Type: layout reflows without truncation or clipping at large text sizes; no `lineLimit(1)` on user-facing prose.
- Reduce Motion honored; color is never the only carrier of meaning (WCAG 1.4.1 — pair with text/icon/shape).
- `a11y-check 100/100` (a11y-inspect baseline) passes.

### F. Reliability
- All file writes remain atomic (temp + rename); never overwrite in place. External-change conflict detection is preserved on every new write/edit path.
- No data-loss path. Any new correctness invariant (workspace tree integrity, folder move + link rewrite, property round-trip) is **census-gated**: an adversarial random + exhaustive census that must run clean, per the project's adversarial-census methodology — not merely a `debug_assert` (debug-only) or a single proptest.
- Graceful, specific error states; no silent failures. No regressions to existing tests.

### G. Performance
- Editing stays within the established budgets: incremental structure/keystroke work stays at the Milestone-#404 level (sub-millisecond structural updates on large notes, flat with document size). No new per-keystroke or per-selection host round-trips.
- New interactions feel instant: tab switch, split focus move, tree expand/collapse, and mode toggle complete within ~1 frame of perceived latency; scrolling holds 60fps.
- Large-vault discipline preserved: file tree and leaves stay lazy (10k+ files render only what's visible); no "load whole vault" shortcuts.
- Benchmarks run at each milestone close; baselines recorded in `BENCHMARKS.md`. Regressions block the milestone.

---

## Cross-cutting process (unchanged project norms)

- **One PR per issue.** Backend and Mac UI split into separate issues/PRs where the seam is clean (matches Milestone T's Backend/Mac-UI split).
- **Pre-push:** `cargo fmt --check` + `clippy` locally before pushing (CI's fmt gate fails fast); Swift builds + tests green.
- **Red-team the bundled/risky surfaces** in a worktree before push; file findings as `audit` issues; fix one per PR. The two highest-risk surfaces in this program are **U1-3 split-pane focus routing** and **U2-3 link-integrity-on-move** — census these hardest.
- **Babysit PRs** after push: re-check CI + reviewer feedback and report back.
- Don't block merges waiting on Codoki until its quota resets.

---

## Per-milestone specs

- [U0 — Baseline & design foundation](u0_baseline.md)
- [U1 — Workspace shell: tabs + split panes](u1_workspace_shell.md)
- [U2 — File tree + full file management](u2_file_tree.md)
- [U3 — Editor: reading/editing + inline properties](u3_editor_modes.md)
- [U4 — Right-hand leaves + utility rail](u4_right_pane_leaves.md)
- [U5 — Iconography & presentation polish](u5_presentation_polish.md)
