# W3 executable spec — Content rendering: reading view, math, diagrams, code, embeds

Issues: W3-1 ([#728](https://github.com/coryj627/slate/issues/728)) · W3-2 ([#729](https://github.com/coryj627/slate/issues/729)) · W3-3 ([#730](https://github.com/coryj627/slate/issues/730)) · W3-4 ([#731](https://github.com/coryj627/slate/issues/731)) · W3-5 ([#732](https://github.com/coryj627/slate/issues/732); Excalidraw rows feature-conditional on XD, and its `.base`-embed row conditional on N + deferred to W4-6 — see the program wave table). Milestone: [GH 22](https://github.com/coryj627/slate/milestone/22). One PR per issue.
Program: [00_program.md](../00_program.md) (decisions 4, 6, 7; DoD §W-A/§W-C). Doctrine anchor: Milestone K is "the model every other milestone should look like" (07 §3) — all pipelines here consume canonical Rust artifacts.

**Execution order: W3-1 first (the container); W3-2..W3-5 parallel after.**

**W3-1 prerequisite decided (owner, 2026-07-19): [#967](https://github.com/coryj627/slate/issues/967) → Option A.** Inline segments are canonical in core — executable spec: [w3_inline_runs_spec.md](w3_inline_runs_spec.md) (pre-unpark, mac-side; the mac `ReadingInlineMapper` migrates onto `reading_inline_segments_source`, and the §W-A harness gains the `inline_runs` artifact in both twins). W3-1 consumes that API; it never re-implements the retired mapper (prohibition list: inline-runs spec §10).

**W0/W1 execution baseline (2026-07-19 refresh — facts the original spec predates):**

- **The read-side FFI for this wave is bound** (`SlateUniffi`, `public`): `reading_blocks`/`reading_blocks_source`, `get_math_blocks`, `get_diagram_blocks`, `get_syntax_tokens`, `resolve_embed`, `read_attachment`, plus the reading table/inline helpers — no new FFI is needed to start; the W0 censuses exercise the reading-blocks and span subset end-to-end. The W0-3 §W-A skeleton already serializes reading blocks (kind incl. heading levels, list depth/ordered/task, quote depth, code-fence language, math/diagram/table/thematic-break/html) over the `tests/fixtures/markdown/` corpus with committed goldens both platforms diff — W3's §W-A structure/token rows **extend that harness, corpus, and goldens** (tables, callouts, embeds at reading scale), not a new mechanism. Line-ending fixtures (CRLF/mixed) are in the corpus and are never normalized.
- **Fluent theme (program decision 2 addendum):** reading-view text renders on Slate-token surfaces (the tokens own every text-bearing surface, carry APCA Lc ≥ 75, and switch through the two-layer Contrast mechanism from W1-1); Fluent supplies the stock chrome (scrollbars, context menus) and §W-C runs against those templates. The W1-1 Mica policy applies — reading text never sits on a translucent backdrop.
- **Dependency currency checks at execution (the 07 §4.2 / AvalonEdit pattern, extended):** W3-2 records a **WPFMath/xaml-math** maintenance + .NET-compat check in its PR; W3-3 records the same for **SharpVectors**. A negative finding is a program-level alarm with an owner decision (alternative renderer or scoped fallback rows), never routed around silently.
- **C# census conventions** (W0-3) apply to any new censuses here; the canonical-serialization rules live in `CanonicalJson.cs` + the Swift twin and change only together.
- **XD status at the W0-4 snapshot:** unshipped (matrix dropped-rows table) — W3-5's Excalidraw rows stay dropped unless XD ships before port start; re-run the matrix generator at wave start.

## W3-1 · Reading view — PR 1

1. Block-model rendering from the core reading pipeline (same block/segment APIs the mac `ReadingView` consumes: headings, lists, tables via cell-segmentation, quotes, callouts-as-shipped), with inline content — links, tags, citations, unresolved state, per-run accessible text — arriving as core-computed runs from the canonical inline-segment API (#967 Option A; [w3_inline_runs_spec.md](w3_inline_runs_spec.md)). C# maps runs to WPF `Run`/`Hyperlink` inlines and applies attributes; no inline parsing, splitting, or resolution logic host-side.
2. Mode toggle parity (reading ⇄ editing, same command + per-leaf state), link routing (`ReadingLinkRouter` semantics; activation record-matching via core `reading_match_link`), in-place block-embed expansion (#598 behavior; detection via the core `block_embed_key`).
3. Tables render on the W4-1 grid substrate where mac uses `AccessibleDataGrid` (#566 parity) — a **deferred cross-wave row** (program wave table): W3-1 closes its wave with plain accessible table rendering, and the substrate-backed rows complete after W4-1 lands; matrix-tracked, not wave-blocking.
4. UIA: document structure exposed so JAWS/NVDA heading/link/list navigation works natively (decision 6: no outline crutch); text ranges expose the reading order.

- [ ] Block parity rows + mode toggle + link routing; §W-A structure **and `inline_runs`** rows green *(substrate-backed table rows excluded — they transfer to W4-1's acceptance and close, §W-C included, with Wave 4)*
- [ ] Heading/link/list AT navigation verified (§W-C)

## W3-2 · Math: WPFMath + the math AutomationPeer — PR 2

1. Render from canonical `{LaTeX, MathML, speech, braille}`; WPFMath renders the LaTeX; the peer exposes Name = canonical speech text, braille per artifact, and MathML via a **UIA route pinned as this issue's first task** — candidates: the registered custom UIA property NVDA's math stack consumes (the MathPlayer-era convention MathCAT inherited) vs. an attached automation property; record the choice + a JAWS/NVDA read test *here* when made. Display + inline parity, prefs parity (`MathPrefs`).
2. MathCAT integration only if the canonical speech artifact delegates to it by then — the artifact is the contract, the host never generates speech (§1.2).
3. **Budgeted risk:** 05 §5.4 allots 2–4 weeks; if WPFMath coverage gaps force fallback rendering for constructs the mac renders, each fallback is a matrix row with an owner decision, never silent degradation. First task alongside the UIA-route pin: the **WPFMath/xaml-math currency check** (maintenance state + .NET 10 compatibility, recorded in the PR per the baseline block above).

- [ ] Math parity + peer semantics (JAWS/NVDA read = VoiceOver read); prefs parity
- [ ] Coverage gaps triaged as matrix rows

## W3-3 · Diagrams: canonical SVG via SharpVectors — PR 3

1. Mermaid/diagram blocks render the Rust-produced SVG artifact; accessible name/description = the canonical description artifact (AT-first diagram contract). No JS engine (§W-G).
2. Fallback parity: source-visible fallback for unrenderable diagrams, as mac.
3. **SharpVectors currency check** recorded in the PR (maintenance state + .NET 10 compatibility, per the baseline block); SVG features the canonical artifact uses that SharpVectors cannot render are triaged as matrix rows, never silent.

- [ ] Diagram rows green; description exposure verified with JAWS/NVDA

## W3-4 · Code blocks — PR 4

1. Render from canonical `{source, syntax_tokens, semantic_spans}` + AT preamble (language announcement etc. per K contract); `CodePrefs` parity; copy actions.

- [ ] Code block parity incl. AT preamble; §W-A token rows green

## W3-5 · Embeds + Excalidraw viewer* — PR 5

1. Embed rendering parity across contexts (editor W2-3 affordances; reading W3-1 expansion): note/heading/block embeds, image/attachment embeds, `.base` embeds (render via W4-6's grid — **deferred cross-wave row** per the program wave table), `.canvas` references (open-target behavior as mac).
2. *If XD shipped:* read-only Excalidraw viewer parity — consumes XD's canonical scene/description artifacts, renders via the same SVG substrate as W3-3; wrapper-format handling per XD's locked decisions.

- [ ] Embed matrix rows green in both editor + reading contexts *(`.base`-embed rows excluded — they transfer to W4-6's acceptance)*
- [ ] (*) Excalidraw viewer rows green
