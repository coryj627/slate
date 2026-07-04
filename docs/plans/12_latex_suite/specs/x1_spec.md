# X1 executable spec — Keyboard authoring: expansion, tabstops, tabout/auto-fraction, matrix, visual snippets + commands

Issues: X1-1 ([#587](https://github.com/coryj627/slate/issues/587)) · X1-2 ([#588](https://github.com/coryj627/slate/issues/588)) · X1-3 ([#589](https://github.com/coryj627/slate/issues/589)) · X1-4 ([#590](https://github.com/coryj627/slate/issues/590)) · X1-5 ([#591](https://github.com/coryj627/slate/issues/591)).
Milestone: [GH 30](https://github.com/coryj627/slate/milestone/30). One PR per issue.
Program: [00_program.md](../00_program.md) (DoD §X-A/§X-B; locked decisions 4, 6, 10). The full U-program Presentation-Ready DoD (`../../08_ui_parity/00_program.md` §A–§G) applies to every X1 issue.

**Execution order: X1-1 → X1-2 → X1-3 → X1-4 → X1-5.** Gate: Wave 1 complete (X0-4 censuses clean). **X1 is the milestone's accessibility gate: no X2 issue merges until X1 is feature-complete and keyboard/VoiceOver-verified (§X-A).** X3-1 (#586, settings toggle) should land alongside X1-1 so the suite is switchable during development; until it does, a debug flag enables it.

Baseline facts (verified 2026-07-04, this worktree):

- Editor keystroke seam: `apps/slate-mac/Sources/SlateMac/NoteEditorView.swift` — `final class Coordinator: NSObject, NSTextViewDelegate, NSTextStorageDelegate` (`:287`); `textDidChange(_:)` (`:917`); the keyDown/`doCommandBy` handling and the coordinator wiring note (`:1139`). Expansion inserts through the existing text path so the DocumentBuffer (#404) stays authoritative.
- Announcements: route through the existing `postAccessibilityAnnouncement(_:priority:)` path and honor the math verbosity setting (the Canvas t0 interaction contract, `../../09_canvas/specs/t0_interaction_contract.md`, is the normative announcement/verbosity reference — X1 adopts it rather than restating it).
- Commands: `commands.rs` + `SlateCommands.swift` + `CommandPalette*`; a cross-language `CommandSection` add lands backend+Swift in one PR (#369 precedent).
- Spoken feedback reuses MathCAT via the shipped math path — an inserted snippet's LaTeX can be spoken with the user's `MathSpeechStyle`/`MathVerbosity`.

**Tab-key precedence (normative, resolved once here; each issue implements its slice):** `active tabstop session (X1-2) → matrix environment (X1-4) → tabout (X1-3) → default NSTextView Tab`. Every issue that consumes Tab checks the higher-precedence conditions first and calls `super`/default when it doesn't own the event. A drift test asserts the order.

---

## X1-1 · Snippet expansion + insertion wiring (#587) — PR 1

### Surface & interaction

In the Coordinator, on text insertion (and on the trigger key), compute the left context + selection and call `latex_expand(source, cursor, selection, config)`. On `Some(Expansion)`: replace the trigger range with the rendered text in a **single coalesced undo group**, move the cursor to the first tabstop (or `$0`), and hand any tabstops to the X1-2 session. Auto-expand (`A`) snippets fire on insertion; non-`A` fire on the trigger key. **When `config.enabled == false` or `config.snippets == false`, the code path is not entered at all** (§X-B) — verified by the suite-off E2E.

### VoiceOver copy (normative)

On expansion: announce the inserted LaTeX followed by its spoken form, e.g. `"\frac{ }{ }, fraction"` (spoken form via MathCAT at the user's verbosity). No announcement when nothing expands.

### Tests

Expansion inserts + coalesces to one undo (undo restores the trigger text exactly); auto-expand vs. trigger-key firing; cursor lands on first tabstop; announcement copy; **suite-off E2E: a scripted keystroke sequence yields byte-identical text to the current editor** (the Swift-side companion to X0-4's census); a11y-check 100/100.

## X1-2 · Tabstop session — navigation, mirrors, announcements (#588) — PR 2

### Surface & interaction

A `TabstopSession` (Swift, driven by the X0 tabstop data) tracks ordered stops `$1…$n` then `$0`. **Tab** advances, **Shift-Tab** retreats; entering a stop selects its placeholder text; typing replaces it and **live-updates all linked mirrors** of that index; **Esc**, a click/caret move outside the stop set, or advancing past `$0` commits and ends the session. While a session is active it owns Tab (highest precedence); with no session, Tab falls through to X1-3/X1-4/default.

### VoiceOver copy (normative)

On each move: `"field {k} of {n}"` then the placeholder or current field text; on commit: `"snippet complete"`. Mirror edits are silent (the announced field is the one focused).

### Tests

Forward/back/wrap/commit; mirror synchronization (edit `$1`, all `$1` mirrors update); placeholder selection on entry; Esc + edit-outside both commit; `"field k of n"` copy verbatim; Tab-fallthrough when no session; keyboard-only E2E (expand → fill three fields → commit).

## X1-3 · Tabout + auto-fraction + auto-enlarge brackets (#589) — PR 3

### Interaction

- **Tabout:** when no tabstop session owns Tab and the cursor is in math (`latex_context_at`), Tab moves the caret out of the nearest `$`, past a `\right…`, or to just after the next closer `)`/`]`/`}`/`\rangle`/`\rvert` — never inserting a literal tab in that context.
- **Auto-fraction:** typing `/` after an operand converts it to `\frac{operand}{ }` with the caret in the denominator; the numerator is the balanced token to the left (`(a+b(c+d))/` balances nested parens; a bare `x/` takes `x`). Tab exits the fraction (via tabout).
- **Auto-enlarge:** when a `\sum`/`\int`/`\frac` is inserted inside an enclosing bracket pair, enlarge that pair to `\left…\right` (upstream parity).

Each is independently gated by its sub-flag (`auto_fraction`, `tabout`) and by math context; disabled ⇒ Tab/`/` behave exactly as today (§X-B).

### VoiceOver copy (normative)

Tabout: `"exited math"` / `"after bracket"`. Auto-fraction: `"fraction, denominator"`. Auto-enlarge: `"brackets enlarged"`.

### Tests

Tabout target resolution at each delimiter kind; auto-fraction numerator balancing (nested parens, braces, bare token, no-op outside math); auto-enlarge on the trigger set only; **Tab-precedence order** test (tabstop > matrix > tabout > default); announcements verbatim; disabled-path no-op.

## X1-4 · Matrix / align / cases shortcuts (#590) — PR 4

### Interaction

When `latex_context_at().environment` is a tabular math env (`matrix`/`align`/`cases`/`array`/…): **Tab** inserts `&`, **Enter** inserts `\\` + newline (aligned to the environment's indentation), **Shift-Enter** moves to the line after `\end{…}` (exit). Outside such environments these keys behave normally. Matrix Tab sits below an active tabstop session in precedence but above tabout.

### VoiceOver copy (normative)

`"new column"` (Tab), `"new row"` (Enter), `"left matrix"` (Shift-Enter).

### Tests

Env-gated behavior for each key; correct fallthrough outside environments (no `&`/`\\` injected in a paragraph); indentation of new rows; precedence vs. tabstop + tabout; announcements verbatim; **keyboard-only E2E: build a 2×2 `pmatrix` and exit** — no pointer.

## X1-5 · Visual snippets + Box/Select-equation commands (#591) — PR 5

**Authoring completes here — this PR's merge is the §X-A gate for Wave 3.**

### Surface & interaction

- **Visual snippets:** with a non-empty selection, a single-character `v` trigger wraps the selection — `\underbrace{…}`, `\overbrace{…}`, `\cancel{…}`, `\cancelto{}{…}`, `\underset{}{…}` (upstream set). Consumes the X0-1 visual trigger kind.
- **Editor commands** in a new `CommandSection.latex` (registry + palette + menu; cross-language enum add, one PR, #369 precedent):
  - **Box current equation** — wraps the equation containing the caret in `\boxed{…}`.
  - **Select current equation** — selects the whole `$…$`/`$$…$$` block containing the caret.

Palette + menu are the only paths; **no new global chords** (T rule R1) — the chord↔surface drift test asserts none were added.

### VoiceOver copy (normative)

Visual snippet: `"wrapped in {name}"`. Box: `"equation boxed"`. Select: `"equation selected, {n} characters"`.

### Tests

Visual-snippet wrap for each entry (selection preserved inside the wrapper, caret placed per upstream); `CommandSection.latex` + both commands registered, palette-reachable, menu-present; **chord-drift test: no new global chords**; `CommandRegistryTests` extended; VoiceOver command reachability; a11y-check 100/100.

---

**Wave-2 exit (= DoD §X-A gate for Wave 3):** all five PRs merged; a keyboard-only + VoiceOver walkthrough completes with zero pointer use — enable the suite, expand `//` to a fraction, fill both tabstops, tabout, build a 2×2 matrix, box the equation — every step announced and legible. Record the walkthrough script in the X1-5 PR description; it becomes the `docs/help/latex.md` §keyboard source (X3-3).
