# U5 executable spec — Iconography & presentation polish

Issues: #474 (U5-1) · #475 (U5-2) · #476 (U5-3) · #477 (U5-4).
Milestone: GH 28. Depends on U3 + U4 complete. This is the program's close-out gate:
U5-4 decides whether the whole thing reads as presentation-ready.

Execution order matches issue order. U5-1/U5-2/U5-3 are sweeps over finished surfaces;
each is one PR. U5-4 is the verification pass; its PR carries benchmarks, runbook
updates, and any small fixes it surfaces (larger findings become `audit` issues fixed
one-per-PR before the milestone closes — the program is not done with open U5-4
findings).

---

## U5-1 · SlateSymbol everywhere + macOS 26 styling (#474) — PR 1

- **Sweep:** the `testNoRawSFSymbolsOutsideLayer` source-lint already fails on raw
  symbols in new code; this PR clears the remaining pre-U0 call sites. Inventory
  command (run at PR time, list in PR body):
  `grep -rn 'systemImage\|systemSymbolName\|Image(systemName' apps/slate-mac/Sources/SlateMac --include='*.swift' | grep -v SlateSymbol.swift`
  Every hit becomes a `SlateSymbol` role (new roles added with correct v7/fallback pairs
  + titles; the lint test's allowlist shrinks to `SlateSymbol.swift` only — if the
  allowlist currently exempts legacy files, delete the exemptions so the gate is total).
- **macOS 26 adoption** behind `if #available(macOS 26, *)`, with the 15–25 path pinned
  by existing snapshots:
  - Toolbar + rail + tab strip adopt the Tahoe control materials (`glassEffect`
    variants) where they exist; fall back to today's `Material` styles below 26.
  - `SlateSymbol` roles whose v7 slot should diverge from the fallback get their real
    v7 glyphs now (audit each role against the SF Symbols 7 catalogue; the
    `(v7, fallback)` seam was built for exactly this — one-line changes).
  - No conditional layout differences (26 vs 15 differ in material/glyph rendering only
    — layout identity keeps snapshots meaningful).
- Rendering-mode consistency check per surface (DoD §B): toolbar = monochrome,
  rail = hierarchical, tab strip = monochrome, tree = hierarchical folders — encode the
  chosen mode as a per-surface constant next to the call sites (a `SlateSymbol.Surface`
  enum with a `renderingMode` property, applied at the container level), so it is a
  decision, not an accident.
- Tests: lint gate total; every role resolves on both availability paths (extend the
  existing both-paths test to the new roles); snapshot suite re-baselined once with the
  PR (reviewed visually in both appearances before accepting).

## U5-2 · Layout / density / typography / emphasis polish (#475) — PR 2

A deliberate pass over the six primary surfaces (tree, tab strip, editor+properties,
reading view, leaves, rail). For each surface, the PR includes a before/after screenshot
pair (light + dark) in the PR body — the review artifact for "reads as considered".

Normative checklist applied to each surface:
1. **Spacing to the token grid:** every `padding`/`spacing` literal in the six surfaces'
   files is either a `Tokens.Spacing` value or gets a line comment naming why not
   (target: zero exceptions; the grep for `padding(` with a numeric literal is the
   audit tool, run + pasted in the PR).
2. **Type ramp:** headers use `sectionHeader`, row primary text `body`, metadata
   `caption` + `textSecondary` — no ad-hoc `.font(.system(size:))` (a11y-check already
   flags fixed sizes; this pass fixes the flagged + the merely-inconsistent).
3. **Interactive states, all six:** rest / hover / pressed / selected / focused /
   disabled defined for: tree rows, tab items, close buttons, rail items, leaf rows,
   toolbar buttons, utility buttons. Hover = `surfaceSecondary` wash; pressed = deepened
   wash; selected = `selection` fill + shape indicator; focused = the system focus ring
   (never suppressed — audit for `focusable(false)`/`focusEffectDisabled` and remove
   unless justified in a comment); disabled = system opacity + non-interactive help.
   Implemented via a shared `InteractiveRowStyle` ButtonStyle/modifier so the states are
   one implementation, not seven.
4. **Empty/loading/error states:** every stateful surface's four states reviewed against
   DoD §A (the U1–U4 specs already required them; this pass verifies copy tone
   consistency — sentence case, period, action button where recovery exists).
5. **Density:** row heights — tree 24pt, tab 30pt, leaf rows per existing panels;
   confirm Obsidian-grade density without sub-24pt hit targets (44pt-equivalent rule
   applies to click targets via padding, not row height).
6. **Motion:** the only animations are leaf/mode transitions (150ms ease) and tab strip
   reorder; each is wrapped in the Reduce-Motion guard (a11y-check verifies the guard
   exists; the runbook verifies behavior).

Tests: the shared style unit tests (state → expected token mapping), re-baselined
snapshots (both appearances), a11y-check 100, no behavioral test changes (this PR must
be visually transformative and behaviorally inert — the suite proves the latter).

## U5-3 · Dark + light correctness pass (#476) — PR 3

- **Literal audit:** `grep -rn 'Color(\.\|Color(red:\|NSColor(\|\.white\|\.black\|srgb('
  apps/slate-mac/Sources/SlateMac --include='*.swift'` minus `DesignTokens.swift` —
  every hit is either migrated to a token/system dynamic color or justified in a
  comment (the `EditorSyntaxPalette` has its own APCA-gated palette — exempt, it IS a
  token system). Paste the final (empty or justified) list in the PR.
- **New-pairing registration:** every text-on-surface pairing the U1–U4 surfaces
  introduced (tab title on strip, dirty dot meaning-carrier check, rail glyph on rail,
  selection text on `selection` fill, error text on error rows, properties header, etc.)
  is added to `Tokens.contrastPairings` so `DesignTokensTests` +
  `PresentationReady.assertContrastFloor` gate them **forever**, both appearances. The
  PR body lists the measured Lc for the five tightest pairs in each mode (the plan's
  "record the measured Lc" requirement).
- **Balance review:** screenshot matrix (6 surfaces × 2 appearances) reviewed for
  washed-out/black-on-near-black; fixes go through token values (never call sites), so
  a re-tune is two lines in `DesignTokens.swift` + re-measured tests.
- `assertResolvesDistinctlyPerAppearance` extended over all new roles.
- Tests: the extended pairing gates ARE the tests; plus snapshot re-baseline if token
  values moved.

## U5-4 · Presentation-ready verification sweep (#477) — PR 4 + close-out

### Automated half

- **Benchmarks:** run `make bench` (Rust suite: unchanged baselines expected —
  U2 added `list_dir_children` + rewrite benches per u2_spec; record them as new
  baseline rows in `BENCHMARKS.md` with the standard format). Run the #404 keystroke
  bench and paste flat-vs-document-size numbers proving the U3-5 body-only buffer held
  the budget. Add a `BENCHMARKS.md` §"Milestone U interaction budgets" table: tab
  switch, mode toggle, leaf switch, tree expand (10k fixture) — measured via the XCTest
  `measure` blocks added in this PR (baseline-recorded so future regressions fail).
- **Full gates:** `make ci` green; `swift test` green; a11y-check 100/100; every census
  in the program run once in release mode this PR (the U1 model census, U2 path/link/
  undo censuses, U3 round-trip censuses) — paste iteration counts + clean results.

### Manual half (the honest-coverage residual from U0-4 — this is where it gets paid)

Extend `docs/runbooks/voiceover-feature-test.md` with **§U — Workspace shell** scripted
end-to-end path (each step has an expected announcement/outcome, pass/fail checkbox):

1. Open vault → tree navigate (expand 2 levels, level announcements) →
2. Open note (Enter) → open second note in new tab (⌘-click) → tab values ("tab 2 of
   2…") → reorder by keyboard → ⌘1/⌘2 →
3. Split right (⌘\) → pane announcements → ⌘⌥arrows across panes → divider resize
   (⌘⌥+/-) →
4. Reading mode (⌘⇧E) → continuous read across a fixture note containing headings,
   list, task, code, math, mermaid, embed, citation, wikilink → heading rotor (VO+H) →
   activate a wikilink (lands per U1-5) → toggle a task →
5. Properties: edit a text property, add a property, show source (⌘⇧D), apply a YAML
   edit, trigger + resolve a conflict (scripted external edit) →
6. Leaves: rail navigation (arrows), each of the 10 leaves announces + shows content,
   ⌘⌥→/← editor↔leaf round-trip →
7. File management: new folder, rename file (inline), move file (picker), delete file →
   announcements + focus per U2-6 → verify a wikilink to the moved file still resolves →
8. Session restore: quit, relaunch → layout + modes restored; delete a tabbed file
   externally before relaunch → error-state tab.
9. Dynamic Type XXL pass over surfaces 1–8 (no truncation/clip; properties header
   scrolls; tab strip scrolls) · Reduce Motion pass (leaf/mode/tab animations
   suppressed) · keyboard-only pass (unplug the concept of the mouse; every step above
   completes).

Execution protocol: run via `scripts/vo.sh` driver where automatable (the runbook's
existing vocabulary), manual where not; record pass/fail per step **in the runbook
file** (the §U table gains a "2026-07 baseline" column, the project's runbook
convention); every FAIL becomes an `audit` issue; the milestone does not close with an
open FAIL.

### Close-out mechanics (the program's definition of done)

- All 25 issues closed, all five GH milestones (24–28) closed.
- `docs/plans/08_ui_parity/00_program.md` status header updated (statuses, PR numbers
  per issue, the U1-internal-order + U1-6-promotion deltas noted).
- Each milestone doc gets its "✅ shipped" header rewrite in the U0 style (what shipped,
  PR refs, deviations).
- `/graphify` refresh over the changed corpus (project convention after milestones).
- Memory files updated (program shipped; new invariants recorded: workspace censuses,
  composed-save law, rewrite invariant).

## Follow-ups filed during U5

- Any U5-4 FAIL → `audit` issue (fixed pre-close, one per PR).
- Perf follow-ups only if measurement warrants (reading-view virtualization decision
  lands here — see u3_spec).
