# T5 executable spec — Parity, polish, close-out (Wave 5)

Issues: #525 (parity extras) · #370 (color accessibility) · #371 (Dynamic Type) · #373 (search/filter) · #365 (E2E + close-out) · #526 (user docs).
Every issue satisfies 08 DoD §A–§G + 09 deltas §H–§L + the [t0 contract](t0_interaction_contract.md). One PR per issue.

**Execution order: #525 → #370 → #371 → #373 → #526 → #365** (close-out last).

---

## #525 — Obsidian-parity authoring extras

Per the issue body (create-connected-card, duplicate, convert-to-note, edit edge label, `#heading` subpaths, URL cards). Spec pins:

- Create-connected-card = New Card (#368) + auto-connect (#523 auto-side) + `direction_hint` (#517) in one op-log entry; ⌃⌥⌘N default direction below, with a direction variant (prompt or per-direction palette commands). Lands in edit mode.
- Duplicate: deep-copies the node (kind, size, color, unknown fields); marked-set duplicate = one entry; announced with placement.
- Convert-to-note: U2-2 create API; canvas write and file creation are **one logical undo step** (journaled-undo convention); the new file card keeps geometry/color; announced with the new note title.
- Subpath cards: `file#heading` parses (#359 `subpath`), labels per t0 §1.1 ("Note › Heading"), activation opens the note at the anchor; detail view renders the narrowed content via U3's reading-view embed rendering.
- URL cards: label = title/host (never a raw URL blob); activation + explicit action "Open in Browser"; the **no-live-embeds divergence** is documented in `docs/help/canvas.md` and the program table.

**Tests:** per action round-trip + announcement; connected-card direction variants; convert undo restores text card and removes file; subpath open-to-anchor; URL activation.

## #370 — Color & visual-attribute accessibility

As issued: Increase Contrast collapse via the `EditorSyntaxPalette.color(for:increaseContrast:)` convention for fills/borders/strokes/group backgrounds; color exposed as **named preset text** in outline labels (verbose level, t0 §1.2) and the sortable table column (landed structurally in #363 — correctness + naming finalized here); APCA Lc > 75 for text on every preset fill and a hex sample, both appearances, including the #367 focus indicator against colored fills.
**Tests:** exhaustive preset × appearance × increaseContrast matrix; APCA measurements asserted; sort-by-color; label naming (hex → nearest preset name + "custom" suffix — pin the mapping in the PR).

## #371 — Dynamic Type / text scaling

As issued, **scope widened**: renderer labels (card titles, edge labels, group labels) via `@ScaledMetric(relativeTo: .body)` discipline (`MathView` precedent) **plus** canvas chrome — toolbar, surface switcher, pickers, marks list, mode HUD, empty states — and confirmation that outline/table inherit correctly. Interaction of large type × zoom: labels truncate with full text available via AX label and tooltip; **WCAG 1.4.13**: any hover-revealed content (edge labels, truncation tooltips) is also keyboard-triggerable and dismissible (Esc respects the t0 ladder).
**Tests:** metric assertions across sizes incl. largest accessibility size; no clipping/overlap on the fixture at max size; 1.4.13 keyboard trigger/dismiss.

## #373 — In-canvas search / filter

As issued (filter field scoped to the open canvas narrowing outline + table by title/type/target/group; `SearchState`/`CommandPaletteModel` patterns) plus contract work: input debounced through #518 coalescing; result announcements "3 cards match" and per-row "n of m, filtered" values (t0 §3 — filter state inspectable); ⌘F binds while canvas focused (program table); **Esc-ladder position**: Esc in the filter clears it (announced "Filter cleared — 40 cards"), next Esc exits per t0 M5; Clear Filter is also a palette command. Filtered-out cards remain in the file (filter is a view, never a mutation).
**Tests:** narrowing per field; selection preserved through filter/clear; announcement + values; Esc ladder; navigator honors the filtered set with a "filtered" cue.

## #526 — User docs

Per the issue body: `docs/help/canvas.md` (first draft ships with this spec PR — see the file), README link, drift check wiring (every `CommandSection.canvas` command appears in the doc's reference table with the matching chord; test or script in the a11y/test lane). This issue tracks keeping the doc true as waves land + the drift check; the Help-index routing is a noted follow-up, not in scope.

## #365 — E2E integration + close-out

As issued (Milestone{Letter} convention; sample + **2,000-node** fixtures; through `AppState` + FFI, not mocks) — final scope across all waves:
- E2E: open sample canvas → outline structure/labels → table rows/sort → navigator traversal → author (create card, connect, group marked set, move) → undo chain → serializer round-trip byte-compare.
- Benchmarks in `BENCHMARKS.md`: parse+derive, outline/table build, VO-traversal, renderer pan/zoom, AX-tree windowing — at 2,000 nodes; no quadratic blow-ups (§K).
- Gates: a11y-check 100/100; APCA matrix (#370) green; announcement-grammar conformance suite green; **manual AT smoke checklist** executed and recorded (t0 §4: VO walk, FKA tab-through, Voice Control "Show numbers" + 5 dictated commands, Switch Control mode cycle, braille inspectability).
- Tester close-out: run the voiceover-feature-test runbook with the cohort; file credited tester-feedback issues; mark shipped in `06_v1_milestones.md`.

---

## Acceptance (milestone close)

All 25 issues closed; E2E green; gates recorded; `docs/help/canvas.md` matches shipped behavior (drift check green); divergences documented; milestone marked shipped.
