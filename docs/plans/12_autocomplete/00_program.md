# 12 — Autocomplete Program (Milestone V): the first inline completion popup a screen-reader user can actually use

**Status:** 📝 Specs locked (2026-07-04); implementation not started. Specs land via PR [#597](https://github.com/coryj627/slate/pull/597). GH [milestone 29](https://github.com/coryj627/slate/milestone/29), issues [#568–#581](https://github.com/coryj627/slate/milestone/29) (V0: #568–573 · V1: #574–577 · V2: #578–580 · docs: #581). Capability reference: the [obsidian-completr](https://github.com/tth05/obsidian-completr) plugin. Evidence base: [`01_research_brief.md`](01_research_brief.md).

**Strategic goal.** Ship an IntelliSense-style inline autocomplete for the editor — as-you-type suggestions with keyboard navigation and insertion — built as **one deterministic completion engine in `slate-core`** surfaced through **a first-class accessible combobox** over the note editor. obsidian-completr is the feature reference (its providers — LaTeX/MathJax, vault word-scan, word lists, front-matter, callouts, blacklist — define the surface), but its popup is a floating visual overlay a screen reader cannot enter; autocomplete popups are *the* canonical accessibility failure. Slate inverts that: the engine is host-independent and pure, and the macOS surface is a proper NSAccessibility combobox — per-row elements, selection announcements, keyboard-and-VoiceOver-complete, no datum or action reachable only by mouse. Obsidian/completr are the capability reference, not the interaction reference — the same stance as the Graph (`../11_graph/00_program.md`) and Canvas (`../09_canvas/00_program.md`) programs.

Beyond parity, Slate ships the completions completr never needed because Obsidian provides them in core: **`[[` wikilink targets, `#heading`/`^block` anchors, and `#tags`** — the completions a vault user actually reaches for most, built on the existing `links` table + resolver and the quick-switcher fuzzy matcher (#495).

Everything here inherits the UI-parity Presentation-Ready DoD (`../08_ui_parity/00_program.md` §A–§G): a11y-check 100/100, APCA Lc ≥ 75 measured in both appearances, census-gated invariants, atomic writes, one PR per issue, fmt/clippy pre-push. This document adds only what is autocomplete-specific.

---

## Locked scope decisions (owner review, 2026-07-04)

| # | Area | Decision |
|---|------|----------|
| 1 | Build order | **Engine first, accessible surface second, polish third.** V0 (completion engine, pure slate-core) → V1 (FFI + accessible popup) → V2 (settings/commands/management) → V3 (close-out). The visible popup arrives as a projection over a proven, census-gated engine — same inversion the Graph program used. Rationale: the correctness-critical part (what to suggest, deterministically, at O(edit) cost) is host-independent and testable without any UI. |
| 2 | Provider scope (owner decision 2026-07-04) | **Full completr parity + Slate-native.** Ships all six completr providers — LaTeX/MathJax, vault word-scan, word-list/custom dictionary, front-matter keys/values, callouts, blacklist — **plus** `[[` wikilink targets, `#heading`/`^block` anchors, and `#tag` completion (completr omits these because Obsidian core supplies them). |
| 3 | One engine, many providers | A single `CompletionEngine` in slate-core owns the tokenizer, the `CompletionProvider` trait, ranking/merge, and `blocks_all_other_providers` short-circuit. Providers are pure functions of a `CompletionContext`. No provider owns UI; the engine owns no policy a provider could. |
| 4 | Context gates providers | Which providers fire is decided by a **syntactic context classifier** (`completion_context_at`) reusing the incremental per-keystroke structure already maintained in `DocBufferState` (`doc_buffer.rs`: `fm_end`, block structure, comment index) and `EditorSpanKind` (`editor_spans.rs`). Wikilink files are never offered inside a code span; LaTeX commands fire only in math (or code, if opted in). |
| 5 | Surface = accessible combobox | The macOS popup is a caret-anchored floating panel attached to the editor text view. The **text view keeps focus and carries combobox semantics**; the suggestion list exposes per-row `NSAccessibilityElement`s with `.accessibilityIsSelected` (the `AccessibleDataGrid`/`CommandPaletteView` precedent). Not a focus-stealing sheet, not a bare visual overlay. |
| 6 | Trigger default (owner decision 2026-07-04) | **Auto-trigger by default, VoiceOver-aware.** Suggestions appear as you type past `minWordTriggerLength`, but announcements are `.medium`-priority and **coalesced** so typing echoes don't clobber them and rapid keystrokes don't garble ("1—5—12 suggestions"). A manual trigger command (⌃Space) and a global on/off toggle exist regardless of the default. |
| 7 | Key ownership while open | An `NSEvent` local `.keyDown` monitor (the `CommandPaletteView` pattern) owns arrows/Tab/Enter/Esc/PageUp-Down **only while the popup is open**, passes modified chords through, and respects IME composition (`hasMarkedText()`). Bare typing always reaches the text view; the popup never eats a keystroke it isn't consuming for navigation/insertion. |
| 8 | Determinism | Same buffer + same config ⇒ identical ranked suggestions, byte-for-byte. No `thread_rng`, no wall-clock ordering, no rayon in ranking. Ties break by (score → provider order → label). Census-gated. |
| 9 | Incremental, laziness-first | The vault word index is maintained incrementally on the existing DocumentBuffer edit path at O(edit) cost, built lazily on first completion query — an unbuilt index makes the edit hook a no-op, so cold sessions and the read path pay zero (the `GraphIndex` laziness precedent). |
| 10 | Persistence & settings | Completion settings live in vault-local `.slate/prefs.json` under an `autocomplete` key (Rust `AutocompletePrefs` mirrored by a Swift store, atomic temp+rename, forward-compat unknown-key preservation — the `citations` prefs precedent). Word lists / custom completions / blacklist are file-backed under `.slate/`, the blacklist mirroring completr's `blacklisted_suggestions.txt`. |
| 11 | Settings home — **Editor Intelligence** bucket | Autocomplete settings are **not** a lone top-level tab; they live under a shared **"Editor Intelligence"** grouping of feature settings — the single place a user opts into optional editor-enhancement features — alongside **Milestone X — LaTeX authoring aids** (`docs/plans/12_latex_suite/`, GH #30 — the LaTeX snippet/tabstop authoring milestone). Editor Intelligence is the settings bucket for opt-in editor-enhancement features (autocomplete, LaTeX authoring aids, and future intelligence features); each member keeps its own master enable + sub-toggles, and the bucket is the organizing information architecture. The existing Math/Code/Bibliography tabs (rendering config, not intelligence) stay where they are. Whichever milestone lands its settings surface first *introduces* the Editor Intelligence container; the second *joins* it — one grouping, not two competing tabs. |

---

## Phase map, waves & dependencies

```
Wave 1 (engine)   V0-1 engine core ─▶ V0-2 context classifier ─▶ {V0-3 word · V0-4 static · V0-5 native} ─▶ V0-6 census+bench gate
Wave 2 (surface)  V1-1 FFI ─▶ V1-2 accessible popup ─▶ V1-3 insertion+snippets ─▶ V1-4 a11y closure
Wave 3 (settings) V2-1 prefs+settings ─ V2-2 list/blacklist management ─ V2-3 commands
Wave 4 (close)    V3-1 help doc + benchmarks + milestone audit
```

| Wave | Issues | Gate |
|------|--------|------|
| 1 — Engine core | V0-1 → V0-2 → (V0-3 ∥ V0-4 ∥ V0-5) → V0-6 | none (start any time; pure slate-core). V0-3/4/5 parallel once V0-2 lands. |
| 2 — Accessible surface | V1-1 → V1-2 → V1-3 → V1-4 | Wave 1 complete (V0-6 green). V1-2 prefers the `AccessibleDataGrid`/announcement-coordinator conventions already shipped. |
| 3 — Settings & management | V2-1, V2-2, V2-3 | V1-2 (something to configure). Can interleave with V1-3/4. |
| 4 — Close-out | V3-1 | Wave 3 |

**Sequencing vs. other programs:** the editor text surface (`NoteEditorView.swift`), the incremental `DocBufferState`, the command registry (Milestone Q, shipped), the front-matter/properties model (Milestones D/I), math read-path rendering (Milestone K), the fuzzy matcher + quick switcher (#495), and the settings/prefs infra (Milestone U) all already exist — V builds on landed seams, not in-flight ones. No dependency on Graph (P), Canvas (T), or Bases (N).

## Relationship to other milestones (do not duplicate)

- **X — LaTeX authoring aids (sibling "Editor Intelligence" milestone):** the LaTeX snippet / tabstop / auto-fraction authoring milestone (`docs/plans/12_latex_suite/`, GH #30, issues #582–596). V and X are the two members of the **Editor Intelligence** settings bucket (locked decision 11) and share one shape — a deterministic engine in slate-core, an accessible-first editor surface, opt-in and inert when off. They are **complementary, not duplicative**: V's LaTeX/MathJax provider (decision 2) is a typeahead *completion* popup; X's snippets are *expansion on a trigger* with tabstops. The two coordinate on math-context detection (V's `completion_context_at` / `Math` `EditorSpanKind`; X's `latex_context_at` over `math.rs`) and on the settings grouping rather than reimplementing either — the specs cross-reference.
- **Q — Command palette (shipped):** completion actions register as `CommandRegistry` commands in `CommandSection::Editor` (V2-3). The popup's key-monitor and announcement patterns are lifted from `CommandPaletteView`/`QuickSwitcherView`, not reinvented.
- **P — Graph:** shares the "one deterministic model in slate-core, laziness-first incremental maintenance, census-gated equality vs. rebuild" spine. The word index mirrors `GraphIndex`'s lazy-hook design.
- **D/I — Front-matter & properties:** V0-5's front-matter provider reads `properties_db` + `frontmatter.rs`; it does not re-parse YAML or duplicate the property model.
- **K — Math/code pipelines:** math *rendering* is done (read path); V adds only a `Math` `EditorSpanKind` for the *editor* so the classifier knows when the caret is in math. No new math renderer.
- **R — Themes:** popup row colors are semantic tokens, APCA-checked in both appearances; V hardcodes nothing.
- **W — Windows (parked):** the entire `completion` engine and all providers are host-independent slate-core; nothing in V0 may take a macOS dependency. The popup (V1) is macOS-only by construction.

## Definition of Done (autocomplete-specific, additive to U §A–§G)

- **§V-A Keyboard-and-VoiceOver-complete:** every provider path — trigger, navigate, insert, dismiss, snippet-field traversal, blacklist-current — is completable keyboard-only and with VoiceOver, verified end-to-end before V milestone close (V1-4).
- **§V-B Announcement discipline:** suggestion count and selection ("5 suggestions; `\alpha` selected, 1 of 5") announce at `.medium` priority, coalesced so typing at speed never garbles them; the announcement-verbosity setting is honored.
- **§V-C Determinism:** same buffer + config ⇒ identical ranked suggestions; permutation of vault-file insertion order ⇒ identical word index and (up to tie-break) identical ranking. Golden- and census-tested (V0-6).
- **§V-D Census:** the incremental word index `deep_equals` a fresh rebuild after every edit op (adversarial random + exhaustive small-vault sweep); the context classifier is total (never panics) and permutation-stable. Census names/scales are normative in the specs (V0-6).
- **§V-E Perf:** completion query < 16 ms at 10k-file vault scale (one frame); incremental word-index update O(edit), < 1 ms at 10k; **zero** regression to `scan_initial` or save paths (the edit hook is free when the index is unbuilt). Baselined in `BENCHMARKS.md` at close.

## Specs

- [v0 — Completion engine: context, providers, ranking, censuses](specs/v0_spec.md)
- [v1 — Accessible surface: FFI, combobox popup, insertion & snippets, a11y closure](specs/v1_spec.md)
- [v2 — Settings, management, commands](specs/v2_spec.md)
- [v3 — Close-out: help doc, benchmarks, milestone audit](specs/v3_spec.md)

V-next (deferred, filed when V ships): sentence/phrase completion, LSP-style multi-line snippets library, per-vault learned ranking, template-body field completion, and a completr-`latex_commands.json`-style user-editable LaTeX table UI.
