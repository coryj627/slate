# Gap analysis — what Milestone K already ships vs. the LaTeX-Suite authoring gap

Program: [00_program.md](../00_program.md). This document justifies the "not a rendering project" framing (locked decision 5) by inventorying what exists.

## Already shipped (Milestone K, #217) — reuse, do not rebuild

| Capability | Where | X reuses it for |
|---|---|---|
| `$…$` / `$$…$$` delimiter scanning, with fenced-code exclusion | `crates/slate-core/src/math.rs` — `extract_math_blocks` | X0-2 extends this to `latex_context_at` (mode + environment at a byte offset). No new scanner. |
| LaTeX → MathML (`pulldown-latex`) | `math.rs` — `render_math` | Not on the authoring hot path; available if a feature needs structure. |
| MathML → speech / braille (MathCAT, single dedicated worker thread) | `math.rs`; prefs `MathSpeechStyle`/`MathVerbosity`/`BrailleCode` | X1-1 announces an inserted snippet's spoken form; X2-3 preview reuses the block's speech. |
| Visual math rendering | `apps/slate-mac/Sources/SlateMac/MathView.swift` | X2-3 inline preview popover renders through it verbatim. |
| Per-user math prefs, Codable, schema-drift-tolerant, UserDefaults-persisted | `MathPrefs.swift`, `PreferencesStore.swift` (`mathKey`/`codeKey`) | X3-1 `LatexSuitePrefs` copies this exact pattern (new `latexKey`, stable persistence tags). |
| Settings tab pattern (native TabView AT shape, audit #262 M2) | `SettingsView.swift` (Math/Code/Bibliography tabs) | X3-1 adds a LaTeX tab beside them. |
| Editor keystroke seam | `NoteEditorView.swift` — `Coordinator: NSTextViewDelegate, NSTextStorageDelegate` (`textDidChange`, `doCommandBy`) | X1 hooks expansion / tabstops / tabout / matrix here. |
| Stateful O(edit) buffer + spans | `doc_buffer.rs`, `editor_spans.rs` (#404) | X1 feeds expansion deltas; X0-4 bypass census diffs `editor_spans`. |
| Command registry + palette + menu (Q) | `commands.rs`, `SlateCommands.swift`, `CommandPalette*` | X1-5 registers `CommandSection.latex`; presets are commands (no chords). |
| Vault-local config convention | `.slate/prefs.json`, `.slate/graph.json` (atomic temp+rename) | X0-3/X3-2 user snippet file `.slate/latex-snippets`. |
| Semantic-token theming + "never color alone" (§B) | `DesignTokens.swift`, U §B | X2-1/X2-2 glyph + bracket-match styling. |

## The gap (this milestone) — the *authoring* layer, absent today

Nothing in Slate helps a user **type** LaTeX faster or reshapes it as they write. Every capability below is new:

1. **Snippet expansion** — trigger→replacement text expansion, math-mode-gated (`t/m/M/n`), with regex/word-boundary/visual/auto-expand trigger kinds. *(X0-1, X0-3, X1-1)*
2. **Tabstops** — `$1…$n`/`$0` placeholder navigation and linked mirrors after an expansion. *(X0-1, X1-2)*
3. **Auto-fraction / auto-enlarge / tabout** — inline transforms that reshape math as typed. *(X1-3)*
4. **Matrix/align/cases key behavior** — Tab→`&`, Enter→`\\`, environment-gated. *(X0-2, X1-4)*
5. **Visual snippets + editor commands** — selection-wrap, Box/Select-equation. *(X1-5)*
6. **Concealment** — inline symbol rendering while editing (opt-in, AX-preserving). *(X2-1)*
7. **Bracket matching + inline preview** — math-scoped visual aids. *(X2-2, X2-3)*
8. **Enable/disable + snippet management UI** — the master toggle, sub-toggles, and library editor. *(X3-1, X3-2)*

## The invariant that ties it together

Because all of the above is *additive authoring behavior* layered on a shipped editor, the milestone's defining constraint (program §X-B) is that **turning the aids off returns the experience to byte-for-byte its current behavior** — verified by a census over `editor_spans` and a suite-off E2E, not merely asserted. Nothing in the left-hand "reuse" table above is gated by the toggle: reading-mode rendering, MathCAT speech, and braille are the default experience and are never enabled/disabled here. The toggle governs only the right-hand "gap" list.
