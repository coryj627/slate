# X0 executable spec — Backend engine: snippet model/matcher, math-context, default library + FFI + feature flag, censuses

Issues: X0-1 ([#582](https://github.com/coryj627/slate/issues/582)) · X0-2 ([#583](https://github.com/coryj627/slate/issues/583)) · X0-3 ([#584](https://github.com/coryj627/slate/issues/584)) · X0-4 ([#585](https://github.com/coryj627/slate/issues/585)).
Milestone: [GH 30](https://github.com/coryj627/slate/milestone/30). One PR per issue.
Program: [00_program.md](../00_program.md) (locked decisions 3, 5, 6, 8, 9; DoD §X-B/§X-C/§X-E).

**Execution order: X0-1 → X0-2 → X0-3 → X0-4.** No external gate — this is pure `slate-core`, host-independent (nothing here may take a macOS dependency; keeps the parked Windows port W free). **X0-4 is the milestone's backend gate: no X1 issue starts until its censuses are clean.**

Baseline facts (verified 2026-07-04, this worktree):

- `crates/slate-core/src/math.rs` already scans `$…$`/`$$…$$` with fenced-code exclusion: `extract_math_blocks(source) -> Vec<RawMathBlock>` (`math.rs:148`), and the escape/code-range handling around `math.rs:166–232`. X0-2 builds *on* this, not beside it.
- FFI is uniffi (`crates/slate-uniffi/src/lib.rs`); `SessionConfig` carries per-user prefs today (e.g. `MathPrefs`). Generated Swift lands in `slate_uniffi.swift` (do not hand-edit — generator output).
- The stateful buffer + span pipeline (#404): `doc_buffer.rs`, `editor_spans.rs`. The bypass census (X0-4) diffs `editor_spans` output with the suite off vs. the pre-X baseline.
- Determinism house rule (graph §P-C precedent): no `thread_rng`, no wall-clock, no locale-dependent ordering. Censuses gate at the standing 1k/10k/50k synthetic scales (`BENCHMARKS.md`), `SLATE_CENSUS_FULL=1` for exhaustive runs.

---

## X0-1 · Snippet engine — model + matcher + options (#582) — PR 1

### Model

A `Snippet` is `{ trigger, replacement, options, priority, description }`. Trigger kinds (upstream parity):

- **string** — literal prefix ending at the cursor.
- **regex** (`r`) — anchored at the cursor's left context; captures usable in the replacement.
- **word-boundary** (`w`) — string trigger that only fires at a word boundary.
- **visual** (`v`) — single-character trigger applied to a non-empty selection (X1-5 consumes this).
- **auto-expand** (`A`) — fires on insertion without a Tab press; otherwise the trigger fires on the configured trigger key.

Options string parser accepts `t/m/M/n/A/r/v/w/c/C` (text / block-math / inline-math / any-math / auto / regex / visual / word / code-block / inline-code) and rejects unknown flags with a typed error. Mode flags gate matching against X0-2's context; `m`+`M` or their absence resolve to "any math" per upstream semantics.

### Matcher

`match_at(context, left_text, selection) -> Option<Expansion>`: over the enabled snippet set, choose the **highest-priority** snippet whose trigger matches at the cursor and whose mode flags admit the current `LatexContext`; ties break by (priority desc, trigger length desc, stable insertion index) — fully deterministic. Returns an `Expansion` (see below) or `None`.

### Replacement AST

Parse the replacement string once into `Vec<ReplPart>` where a part is `Literal(String)`, `Tabstop { index, placeholder: Option<String> }`, or `Mirror { index }` (a second+ occurrence of an index). `$0` is the final cursor. The `Expansion` carries the rendered text, the tabstop ranges (byte offsets relative to insertion), the mirror links, and the replaced source range. Regex capture substitution (`$1`-in-regex vs. tabstop `$1`) follows upstream's rule: regex snippets use a distinct capture syntax so tabstops stay unambiguous — the parser documents and tests the disambiguation.

### Tests

Options round-trip + malformed-flag rejection; each trigger kind matches/doesn't at constructed cursors; priority + tie-break determinism (permute insertion order ⇒ identical winner); replacement-AST parse incl. tabstops, mirrors, `$0`, escaped `\$`; regex-capture vs. tabstop disambiguation. **No macOS dependency; no clock; no `thread_rng`.**

## X0-2 · Math-context resolver — mode at offset (#583) — PR 2

### Surface

`latex_context_at(source: &str, offset: usize) -> LatexContext` where `LatexContext = { mode: Text | InlineMath | BlockMath, environment: Option<String> }`. Built on `extract_math_blocks`'s existing delimiter + fenced-code scan — a `$` inside a fenced/inline code block is never math (reuse the code-range pass). Environment is the innermost `\begin{env}…\end{env}` enclosing the offset when in math (`matrix`, `pmatrix`, `bmatrix`, `align`, `aligned`, `cases`, `array`, …); `None` otherwise.

### Interaction

Called on the editor hot path (per keystroke, at the cursor) — must be **O(local window)**: resolve mode from the nearest enclosing block without re-tokenizing the whole document where the incremental path allows (may reuse `RawMathBlock` offsets already computed for the note). The environment scan is bounded to the enclosing block's byte range.

### Tests

Mode correctness at boundaries (`$|`, `|$`, `$$|`, inside vs. outside); code-block exclusion (`$` in ```` ``` ````/inline code ⇒ `Text`); nested-environment innermost-wins; environment `None` outside math; **census hook**: for a corpus, `latex_context_at` at every offset ≡ a fresh full re-scan classification (feeds X0-4). No `scan_initial`/`extract_math_blocks` regression (bench).

## X0-3 · Default library + file format + FFI + feature flag (#584) — PR 3

### Default library

A bundled default snippet set (data, not code) ported in behavior from upstream defaults, MIT-attributed in a source header (`// Default snippets adapted from obsidian-latex-suite (MIT), artisticat1 & contributors`): Greek (`@a`→`\alpha`, `pi`→`\pi`, variants), sub/superscripts (`sr`→`^{2}`, `x1`→`x_{1}`), fractions (`//`→`\frac{ }{ }`, `x/y`→`\frac{x}{y}`), roots (`sq`→`\sqrt{ }`), accents (`xhat`/`xbar`/`xdot`), operators, `text`/`mathbf` styling. Each carries mode flags so it only fires in the right context.

### User file format

`.slate/latex-snippets` — a documented, human-editable format (JSON array of snippet records; the exact schema is defined here and consumed by X3-2). Parsed + validated on load; validation produces a typed `Vec<SnippetParseError>` (line/field + message) returned to the UI (X3-2 surfaces them) — a malformed user snippet never panics and never silently drops the whole file. User snippets override defaults by trigger+mode.

### Config + FFI

`LatexSuiteConfig { enabled: bool /* default false */, snippets: bool, auto_fraction: bool, tabout: bool, matrix: bool, conceal: bool, bracket_match: bool, preview: bool }` added to `SessionConfig` (all sub-flags default per program §2; master default **false**). This config gates the **authoring aids only** — it is orthogonal to `MathPrefs` and the existing render/speech/braille pipeline, which are unaffected by its value (program §2 boundary; the disabled path must not perturb `extract_math_blocks`/`render_math`). FFI surface: `latex_expand(source, cursor, selection, config) -> Option<Expansion>`, `latex_context_at(source, offset) -> LatexContext`, and tabstop-session helpers (X1-2 consumes). All host-independent.

### Tests

Default library loads + every entry parses; user-file parse success + each malformed case yields the right typed error (not a panic); user-overrides-default resolution; config default is `enabled: false`; FFI round-trip of `Expansion`/`LatexContext`. Attribution header present.

## X0-4 · Census + bench gate — bypass invariant + determinism (#585) — PR 4

The milestone's backend gate. Three censuses, gated at the standing scales, `SLATE_CENSUS_FULL=1` for exhaustive runs:

- **§X-B Bypass invariant (defining guarantee):** for a corpus of notes and a stream of edits, with `LatexSuiteConfig::default()` (disabled), the resulting text **and** `editor_spans` output are **byte-identical** to the pre-X code path. Adversarial random edit streams + exhaustive small-input enumeration. This census is the proof that the feature is truly "on top of" the default experience.
- **§X-C Expansion determinism:** same (source, cursor, selection, snippet set) ⇒ identical `Expansion`; permuting snippet insertion order ⇒ identical winner and identical rendered text.
- **§X0-2 Context equivalence:** `latex_context_at` at every offset ≡ fresh full-scan classification over the corpus.

Bench: `latex_expand` and `latex_context_at` are O(local window); **no regression** to `scan_initial` or save paths in `BENCHMARKS.md` (the config lookup on the disabled path must be effectively free).

### Tests

The three censuses above (named, `SLATE_CENSUS_FULL`-scaling, per graph §P-D discipline); BENCHMARKS.md rows for expansion + context added; a CI assertion that the disabled-path benchmark matches the pre-X baseline within noise.

---

**Wave-1 exit (= gate for Wave 2):** X0-1…X0-4 merged; all three censuses clean at `SLATE_CENSUS_FULL=1`; BENCHMARKS.md shows no regression on `scan_initial`/save and the disabled path is free. The engine is proven inert-when-off before any editor wiring lands.
