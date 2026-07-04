# X2 executable spec — Opt-in visual layer: concealment, bracket matching, inline preview

Issues: X2-1 ([#592](https://github.com/coryj627/slate/issues/592)) · X2-2 ([#593](https://github.com/coryj627/slate/issues/593)) · X2-3 ([#594](https://github.com/coryj627/slate/issues/594)).
Milestone: [GH 30](https://github.com/coryj627/slate/milestone/30). One PR per issue.
Program: [00_program.md](../00_program.md) (locked decision 7; DoD §X-D; U §B "never color alone").

**Gate: the entire X1 wave is feature-complete and keyboard/VoiceOver-verified (§X-A) before any issue here merges.** Every X2 feature has its **own sub-toggle, OFF by default**; with the sub-toggle off there is zero rendering change (§X-B). X2-1/X2-2/X2-3 are independent and may run in parallel.

**Scope note (program §2 boundary):** these are aids on the **editing surface** (which today shows plain source) — concealment renders glyphs inline while editing, the preview popover shows a render on the cursor's block. They are *additive* and opt-in; they do **not** modify, replace, or gate Slate's existing **reading-mode** math rendering (`ReadingView.swift` → `MathView`), which stays default and always-on regardless of any toggle here. X2-3 reuses `MathView` to render; it does not introduce a second renderer or touch the reading pane.

Baseline facts (verified 2026-07-04, this worktree):

- Editor renders through `NoteEditorView.swift`; syntax styling precedent is `EditorSyntaxPalette.swift` (how spans become attributes today) and the windowed `applyHighlight` path (#379).
- Concealment must use **NSTextView temporary attributes** (`NSLayoutManager.setTemporaryAttributes(_:forCharacterRange:)` / equivalent), which change presentation without mutating `textStorage` — so the underlying string, the DocumentBuffer, and the AX text are untouched (§X-D).
- Visual math rendering already exists: `MathView.swift` (X2-3 reuses it). The block's MathCAT speech is available from the shipped math path.
- Semantic color tokens: `DesignTokens.swift`; APCA standard is G-4g, Lc ≥ 75 (project a11y rule); "never color alone" is U §B.
- The conceal *map* (which source spans render as which glyphs) is computed in `slate-core` over the editor spans — host-independent, so it's free for the parked Windows port W; only the attribute application is macOS.

---

## X2-1 · Concealment — opt-in, AX-preserving symbol rendering (#592) — PR 1

### Model (core)

`conceal_map(source, config) -> Vec<Conceal>` in `slate-core`, where `Conceal = { source_range, glyph: String, cursor_reveal: bool }`. Covers the upstream conceal set inside math only: Greek command → letter (`\alpha`→α), accents (`\dot{x}`→ẋ, `\hat{x}`→x̂), superscripts/subscripts to Unicode where representable (`^{2}`→², `_{1}`→₁), common operators (`\to`→→, `\times`→×, `\leq`→≤). Never conceals inside fenced code (reuses X0-2 context). Purely a function of source — deterministic, censusable.

### Presentation (macOS)

The Mac layer maps each `Conceal` to a temporary attribute that displays `glyph` in place of `source_range` (an attachment/replacement-glyph technique that leaves `textStorage` unchanged). **The AX-exposed value of the range remains the exact source string.** When the caret enters (or, per upstream, is adjacent to) a concealed range, that range reveals its raw source until the caret leaves — with the optional small delay so arrow-key traversal reveals progressively. Glyph rendering uses the editor font with a fallback for Unicode-math glyphs; contrast APCA Lc ≥ 75 in both appearances.

### Accessibility (normative — this is the divergence from Obsidian)

Concealment is **presentation only**. VoiceOver reading, `Select all`+copy, find, and the DocumentBuffer all see real LaTeX. Sub-toggle `conceal` default **off**; when off, no temporary attributes are set (identical to today). This is documented in `docs/help/latex.md` (X3-3) as a deliberate divergence: Obsidian's conceal is a display convenience; Slate's is a display convenience *that never touches the accessibility tree*.

### Tests

Conceal-map correctness + determinism (core); code-block exclusion; **AX-text-equivalence: the VoiceOver value / `accessibilityValue` of every concealed range equals its source substring** (the §X-D gate, automated); caret-reveal on enter + restore on leave; APCA table both appearances; sub-toggle off ⇒ no temporary attributes set (assert the layout manager has none); a11y-check 100/100.

## X2-2 · Math bracket matching + highlight (#593) — PR 2

### Model & presentation

Inside math (`latex_context_at`), compute matching bracket pairs and, for the caret's position, the enclosing / adjacent pair (`match_brackets(source, offset) -> Option<(Range, Range)>`, core). The Mac layer highlights the matched pair with a **semantic token color paired with a non-color channel** (bold weight or underline) — never color alone (U §B). Sub-toggle `bracket_match` default off.

### Tests

Pair computation: nested pairs innermost-wins, unbalanced input yields `None` (no crash), `\left(…\right)` and `\{…\}` handled, code-block excluded; non-color channel asserted present; APCA both appearances; sub-toggle off ⇒ no styling; determinism.

## X2-3 · Inline math preview popover (#594) — PR 3

### Surface & interaction

When the caret is inside a `$…$`/`$$…$$` block and sub-toggle `preview` is on, show a small, **non-focus-stealing** popover anchored to the block that renders the equation via the existing `MathView` (Milestone K). **Esc** dismisses; moving the caret out of the block dismisses; the popover never takes VoiceOver focus from the editor and carries an AX label (`"Equation preview"`). Rendering is debounced so rapid typing doesn't thrash; large blocks render without editor reflow jank. Sub-toggle `preview` default off.

### VoiceOver copy (normative)

The popover is not focus-stealing, so it announces nothing on appear; on request (the block is focused) the shipped math speech is available. Dismiss is silent.

### Tests

Popover appears only inside a math block with `preview` on; reuses `MathView` (no second renderer); Esc + caret-exit dismiss; **no VoiceOver focus theft** (editor keeps focus; assert first responder unchanged); AX label present; debounce (no render per keystroke); sub-toggle off ⇒ never shown; a11y-check 100/100.

---

**Wave-3 exit:** X2-1…X2-3 merged; each defaults off and, when off, leaves the editor byte- and pixel-identical to the X1-only state; with each on, the §X-D AX-equivalence test (X2-1), the non-color-channel assertion (X2-2), and the no-focus-theft assertion (X2-3) all pass; APCA tables recorded in both appearances.
