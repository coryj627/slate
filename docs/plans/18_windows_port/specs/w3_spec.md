# W3 executable spec — Content rendering: reading view, math, diagrams, code, embeds

Issues: W3-1 ([#728](https://github.com/coryj627/slate/issues/728)) · W3-2 ([#729](https://github.com/coryj627/slate/issues/729)) · W3-3 ([#730](https://github.com/coryj627/slate/issues/730)) · W3-4 ([#731](https://github.com/coryj627/slate/issues/731)) · W3-5 ([#732](https://github.com/coryj627/slate/issues/732); Excalidraw rows feature-conditional on XD, and its `.base`-embed row conditional on N + deferred to W4-6 — see the program wave table). Milestone: [GH 22](https://github.com/coryj627/slate/milestone/22). One PR per issue.
Program: [00_program.md](../00_program.md) (decisions 4, 6, 7; DoD §W-A/§W-C). Doctrine anchor: Milestone K is "the model every other milestone should look like" (07 §3) — all pipelines here consume canonical Rust artifacts.

**Execution order: W3-1 first (the container); W3-2..W3-5 parallel after.**

## W3-1 · Reading view — PR 1

1. Block-model rendering from the core reading pipeline (same block/segment APIs the mac `ReadingView` consumes: headings, lists, tables via cell-segmentation, quotes, callouts-as-shipped, links, tags, tasks).
2. Mode toggle parity (reading ⇄ editing, same command + per-leaf state), link routing (`ReadingLinkRouter` semantics), in-place block-embed expansion (#598 behavior).
3. Tables render on the W4-1 grid substrate where mac uses `AccessibleDataGrid` (#566 parity) — a **deferred cross-wave row** (program wave table): W3-1 closes its wave with plain accessible table rendering, and the substrate-backed rows complete after W4-1 lands; matrix-tracked, not wave-blocking.
4. UIA: document structure exposed so JAWS/NVDA heading/link/list navigation works natively (decision 6: no outline crutch); text ranges expose the reading order.

- [ ] Block parity rows + mode toggle + link routing; §W-A structure rows green *(substrate-backed table rows excluded — they transfer to W4-1's acceptance and close, §W-C included, with Wave 4)*
- [ ] Heading/link/list AT navigation verified (§W-C)

## W3-2 · Math: WPFMath + the math AutomationPeer — PR 2

1. Render from canonical `{LaTeX, MathML, speech, braille}`; WPFMath renders the LaTeX; the peer exposes Name = canonical speech text, braille per artifact, and MathML via a **UIA route pinned as this issue's first task** — candidates: the registered custom UIA property NVDA's math stack consumes (the MathPlayer-era convention MathCAT inherited) vs. an attached automation property; record the choice + a JAWS/NVDA read test *here* when made. Display + inline parity, prefs parity (`MathPrefs`).
2. MathCAT integration only if the canonical speech artifact delegates to it by then — the artifact is the contract, the host never generates speech (§1.2).
3. **Budgeted risk:** 05 §5.4 allots 2–4 weeks; if WPFMath coverage gaps force fallback rendering for constructs the mac renders, each fallback is a matrix row with an owner decision, never silent degradation.

- [ ] Math parity + peer semantics (JAWS/NVDA read = VoiceOver read); prefs parity
- [ ] Coverage gaps triaged as matrix rows

## W3-3 · Diagrams: canonical SVG via SharpVectors — PR 3

1. Mermaid/diagram blocks render the Rust-produced SVG artifact; accessible name/description = the canonical description artifact (AT-first diagram contract). No JS engine (§W-G).
2. Fallback parity: source-visible fallback for unrenderable diagrams, as mac.

- [ ] Diagram rows green; description exposure verified with JAWS/NVDA

## W3-4 · Code blocks — PR 4

1. Render from canonical `{source, syntax_tokens, semantic_spans}` + AT preamble (language announcement etc. per K contract); `CodePrefs` parity; copy actions.

- [ ] Code block parity incl. AT preamble; §W-A token rows green

## W3-5 · Embeds + Excalidraw viewer* — PR 5

1. Embed rendering parity across contexts (editor W2-3 affordances; reading W3-1 expansion): note/heading/block embeds, image/attachment embeds, `.base` embeds (render via W4-6's grid — **deferred cross-wave row** per the program wave table), `.canvas` references (open-target behavior as mac).
2. *If XD shipped:* read-only Excalidraw viewer parity — consumes XD's canonical scene/description artifacts, renders via the same SVG substrate as W3-3; wrapper-format handling per XD's locked decisions.

- [ ] Embed matrix rows green in both editor + reading contexts *(`.base`-embed rows excluded — they transfer to W4-6's acceptance)*
- [ ] (*) Excalidraw viewer rows green
