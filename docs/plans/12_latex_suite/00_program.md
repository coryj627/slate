# 12 — LaTeX Suite Program (Milestone X): an optional LaTeX authoring layer that stays invisible when off

**Status:** 📝 Specs locked (2026-07-04); implementation not started. GH [milestone 30](https://github.com/coryj627/slate/milestone/30), issues [#582–#596](https://github.com/coryj627/slate/milestone/30) (X0: #582–585 · X1: #587–591 · X2: #592–594 · settings/manager/docs: #586, #595, #596). Evidence base: the [obsidian-latex-suite](https://github.com/artisticat1/obsidian-latex-suite) plugin (MIT) as the capability reference, and Milestone K's shipped math pipeline as the substrate.

**Strategic goal.** Add the *new authoring aids* the [obsidian-latex-suite](https://github.com/artisticat1/obsidian-latex-suite) plugin provides that Slate does not already have — snippets, tabstops, auto-fraction, matrix shortcuts, tabout, visual snippets, and opt-in editing-surface visuals (conceal / bracket-match / preview) — each as an **opt-in enhancement that is completely inert when off**. Slate already *renders* LaTeX (Milestone K: `slate-core/src/math.rs` scans `$…$`/`$$…$$` → MathML + MathCAT speech/braille; `MathView.swift` draws it in **reading mode**, `ReadingView.swift:251`). This milestone is therefore not a rendering project; it is the layer that helps a user *write* LaTeX quickly, projected over the existing math model. The authoring core is keyboard- and VoiceOver-complete **before** any visual layer ships, and that visual layer never alters the accessibility text. Obsidian is the capability reference, not the interaction reference — same stance as the Graph (`../11_graph/00_program.md`) and Canvas (`../09_canvas/00_program.md`) programs.

**Scope boundary — what is *not* part of the toggle.** The switch this milestone adds gates **only the new authoring aids above**. Everything Slate already ships stays exactly as it is and is **never** enabled/disabled by it: reading-mode math rendering (`MathView`), MathCAT speech + braille, and the existing `MathPrefs` (speech style / verbosity / braille code, on the Math settings tab). "Off" is not a reduced experience — it is today's *full* experience, rendering and all, with none of the new authoring aids. The core LaTeX experience is not a feature to enable/disable; only the enhancements on top of it are.

Everything here inherits the UI-parity Presentation-Ready DoD (`../08_ui_parity/00_program.md` §A–§G): a11y-check 100/100, APCA Lc ≥ 75 measured in both appearances, census-gated invariants, atomic writes, one PR per issue, fmt/clippy pre-push. This document adds only what is LaTeX-Suite-specific.

---

## Locked scope decisions (owner review, 2026-07-04)

| # | Area | Decision |
|---|------|----------|
| 1 | Identity | **Milestone X — LaTeX authoring ("LaTeX Suite")**. `X` = TeX. Specs in `docs/plans/12_latex_suite/`; phase prefixes X0–X3. Confirmed at kickoff: full parity with the upstream feature set; guarded concealment. |
| 2 | What the toggle governs | The toggle gates **only the new authoring aids** this milestone adds (snippets, tabstops, auto-fraction, matrix keys, tabout, visual snippets, and the opt-in editing-surface visuals). It does **not** gate anything already shipped — reading-mode rendering (`MathView`), MathCAT speech/braille, and `MathPrefs` are default and untouched by it. A master **Enable LaTeX authoring aids** toggle, **OFF by default** (opt-in power mode), plus per-feature sub-toggles; persisted app-global in `PreferencesStore` (new `latexKey`), mirroring the shipped `MathPrefs`/`CodePrefs` persistence (`PreferencesStore.swift`). The master toggle is a convenience umbrella over the aids, **not** a switch on "LaTeX" as such. |
| 3 | **Bypass invariant (load-bearing)** | With the suite disabled, a keystroke produces **byte-identical text + `editor_spans`** to today. This is the operational meaning of "on top of the default experience," and it is enforced *twice*: a `slate-core` census (X0-4, §X-B) and a suite-off editor E2E. Any feature that cannot be fully bypassed is out of scope. |
| 4 | Accessible-first build order | Backend engine (X0) → keyboard authoring (X1) → **then** the optional visual layer (X2). No X2 issue merges until X1 is keyboard- and VoiceOver-complete (§X-A) — the same accessibility gate the Graph program uses (§P-A). The visual layer arrives as a projection over a proven, shipped authoring core. |
| 5 | One model, reuse | Math-mode gating reuses `math.rs`'s delimiter + fenced-code scan (X0-2 extends it to answer "mode at offset"); the inline preview reuses `MathView`; spoken feedback reuses MathCAT. No second math parser, no second renderer. |
| 6 | Engine lives in Rust | The snippet model, matcher, options parser, tabstop AST, and the auto-fraction / tabout / auto-enlarge / matrix transforms are host-independent `slate-core` — deterministic, censusable, and free for the parked Windows port (W). Swift wires keystrokes and presentation only; nothing in X0 may take a macOS dependency. |
| 7 | Concealment = pure projection | Concealment (hide `\alpha`, show α) renders via **NSTextView temporary attributes only**; the AX-exposed text is *always* the real LaTeX (§X-D); the cursor entering a concealed span reveals its source; the sub-toggle is OFF by default; glyph styling is APCA Lc ≥ 75 in both appearances. This is a deliberate divergence from Obsidian — visual sugar that never mutates the accessibility tree — and it is documented as such in help. |
| 8 | Snippet library | Bundle a default library (Greek, sub/superscripts, fractions, roots, accents, operators — ported behavior from upstream defaults, MIT-attributed) plus user overrides in a vault-local `.slate/latex-snippets` file (the `.slate/*.json` convention). A manager UI (X3-2) views/adds/edits/removes/imports/exports/resets. |
| 9 | Licensing | Upstream obsidian-latex-suite is **MIT** (`LICENSE.md`), compatible with Slate's AGPL-3.0. No upstream code is copied — behavior is re-implemented in Rust/Swift. Any ported default-snippet / conceal-map tables carry an attribution header (obsidian-latex-suite, MIT). |
| 10 | Commands, no new global chords | Every action is a `CommandRegistry` command in a new `CommandSection.latex` (palette + menu). Tab / Shift-Tab / Enter are **contextual editor keys active only inside a live expansion or math environment**, drift-tested so they never land-grab a global chord (Canvas T rule R1). Box/Select-equation are palette/menu commands. |
| 11 | Settings home — **Editor Intelligence** bucket | The LaTeX authoring-aids settings are **not** a lone top-level tab; they live under a shared **"Editor Intelligence"** grouping of feature settings, alongside **Milestone V — Editor autocomplete** (`docs/plans/12_autocomplete/`, GH #29 — the IntelliSense-style completion milestone). Editor Intelligence is the settings bucket for *opt-in editor-enhancement features* (autocomplete, LaTeX authoring aids, and future intelligence features); each member keeps its own master enable + sub-toggles, and the bucket is the organizing information architecture so a user has **one place to opt into editor intelligence**. The existing Math/Code/Bibliography tabs (rendering, not intelligence) stay where they are. |

---

## Phase map, waves & dependencies

```
Wave 1 (backend gate)   X0-1 engine ─▶ X0-2 math-context ─▶ X0-3 default lib + file format + FFI + feature flag ─▶ X0-4 census/bench gate
Wave 2 (authoring)      X1-1 expand+insert ─▶ X1-2 tabstops ─▶ X1-3 tabout/auto-frac/enlarge ─ X1-4 matrix ─ X1-5 visual snippets + Box/Select
                        (∥ X3-1 settings master toggle — needed to switch features on for testing; dep X0-3)
Wave 3 (visual, opt-in) X2-1 concealment ─ X2-2 bracket match ─ X2-3 inline preview   (all OFF by default; gate = X1 complete)
Wave 4 (close-out)      X3-2 snippet manager + user file ─ X3-3 docs/help/latex.md + milestone close-out sweep
```

| Wave | Issues | Gate |
|------|--------|------|
| 1 — Backend core | X0-1 (#582) → X0-2 (#583) → X0-3 (#584) → X0-4 (#585) | none (start any time; pure slate-core, no macOS dependency) |
| 2 — Keyboard authoring | X1-1 (#587) → X1-2 (#588) → X1-3 (#589) → X1-4 (#590) → X1-5 (#591); X3-1 (#586) runs in parallel (dep X0-3, lands early so the suite is switchable) | Wave 1 complete (X0-4 censuses clean) |
| 3 — Visual projection (opt-in) | X2-1 (#592), X2-2 (#593), X2-3 (#594) — parallel | Wave 2 **feature-complete** (§X-A) |
| 4 — Close-out | X3-2 (#595), X3-3 (#596) | X3-2 after X3-1; X3-3 last |

**Sequencing vs. other programs:** the editor surface (`NoteEditorView.swift`, U3) and command palette (Q) are shipped seams this milestone binds to. Wave 1 and the concealment *core* (the conceal-map computation) are pure slate-core and can interleave with any other program's UI work without contention; the X1/X2/X3 UI waves touch the editor Coordinator and Settings, so they should not run concurrently with another program mutating those same files.

## Relationship to other milestones (do not duplicate)

- **K — Math (shipped):** reuse `math.rs` scanning, `MathView`, and MathCAT wholesale. X0-2 *extends* the existing delimiter/code scan to a mode-at-offset query; it does not fork it. No new math parser or renderer anywhere in X.
- **Q — Command palette:** every LaTeX action is a `CommandRegistry` command in `CommandSection.latex`; presets/commands are the paths. No new global chords (T rule R1). Cross-language enum change lands backend + Swift in one PR (#369 precedent).
- **R — Themes:** conceal-glyph and bracket-match colors are semantic tokens, always paired with a non-color channel; R re-skins them later. X hardcodes nothing.
- **V — Editor autocomplete (sibling "Editor Intelligence" milestone):** the IntelliSense-style completion milestone (`docs/plans/12_autocomplete/`, GH #29, issues #568–581). X and V are the two members of the **Editor Intelligence** settings bucket (locked decision 11) and share one shape — a deterministic engine in slate-core, an accessible-first surface over NSTextView, opt-in and inert when off. They are **complementary, not duplicative**: V's LaTeX/MathJax *completion provider* is a typeahead popup; X's snippets are *expansion on a trigger* with tabstops. The specs cross-reference so the two never reimplement each other's math-context detection or settings plumbing.
- **U — UI parity (shipped):** X inherits the full Presentation-Ready DoD (§A–§G) and the §B "never color alone" rule for every visual signal.
- **W — Windows (parked):** the X0 engine, math-context resolver, tabstop machine, and conceal-map computation are host-independent slate-core; nothing in X0 (or the X2 conceal-map core) may take a macOS dependency — this keeps the authoring core free for W.

## Definition of Done (LaTeX-Suite-specific, additive to U §A–§G)

- **§X-A Accessible-first gate:** X1 is fully usable keyboard-only and with VoiceOver — expand a snippet, walk every tabstop, tabout, build a matrix, box an equation — **before any X2 issue merges.** The X1-5 PR records the keyboard/VoiceOver walkthrough that becomes the `docs/help/latex.md` source.
- **§X-B Bypass invariant:** with the authoring aids disabled, the experience is **exactly today's** — keystroke → byte-identical text + `editor_spans`, and reading-mode rendering / MathCAT speech / braille completely unchanged (the toggle never touches them). Enforced as a slate-core census (X0-4) *and* a suite-off editor E2E. The defining guarantee: the toggle *adds* aids, it never *subtracts* the default experience.
- **§X-C Determinism:** same source + same cursor + same snippet set ⇒ identical expansion and identical math-context; census-gated. No `thread_rng`, no wall-clock, no locale-dependent matching.
- **§X-D Concealment never degrades AX:** with concealment ON, the accessibility value/read of a math span is the exact source string; the cursor reveals source; an automated AX-text-equivalence test asserts it. Concealment is presentation only.
- **§X-E Perf:** expansion and context lookup are O(local window); no regression to `scan_initial` or the save paths (the engine rides existing edit paths). Baselined in `BENCHMARKS.md` at milestone close.

## Specs

- [gap analysis — what Milestone K already gives us vs. the authoring gap](specs/gap_analysis.md)
- [x0 — Backend engine: snippet model/matcher, math-context, default library + FFI + feature flag, censuses](specs/x0_spec.md)
- [x1 — Keyboard authoring: expansion, tabstops, tabout/auto-fraction, matrix, visual snippets + commands](specs/x1_spec.md)
- [x2 — Opt-in visual layer: concealment, bracket matching, inline preview](specs/x2_spec.md)
- [x3 — Settings, snippet manager, docs + close-out](specs/x3_spec.md)

X2+ (deferred, filed when X1 lands if demand appears): snippet-suggestion popup / completion menu, per-note frontmatter snippet scoping, LaTeX-environment folding, custom conceal-map authoring in the manager UI, non-math (`t`-mode) text snippets as a general expander.
