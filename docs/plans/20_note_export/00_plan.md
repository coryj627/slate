# 20 — Milestone E plan: Note export (semantic HTML + DOCX)

**Status:** 📝 Planned (2026-07-11). Not started. GitHub [milestone 36](https://github.com/coryj627/slate/milestone/36).
**Executable spec:** [specs/export_spec.md](specs/export_spec.md) — grounded against the shipped core (2026-07-11): the draft proposal's comrak/docx-rs architecture was rejected after review against the locked parser pick (`05` §2.4) and the reading-view pipeline; writer/embeds/math/frontmatter decisions resolved with CJ the same day.
**Inherits:** the UI-parity Presentation-Ready Definition of Done (`../08_ui_parity/00_program.md` §A–§G) — a11y-check 100/100, APCA Lc ≥ 75 both appearances, census-gated invariants, atomic writes, one PR per issue.

**Goal.** A note shared out of Slate is as navigable to an AT user as it was inside Slate: a
heading in the note is a real heading in Word and in the browser, a list is real numbering, a
link is a hyperlink, a table announces its header row, an image carries its alt text (or an
explicit decorative mark), and math is real OMML/MathML — not a visual imitation of any of
those. One session call returns the bytes; the save panel, share sheet, or CLI decides where
they go. No pandoc, no LibreOffice, no external binary, no network.

---

## What already exists (why this milestone is smaller than it reads)

Export is a **projection of structures core already owns** — the spec's reuse map (§3) is the
design. The load-bearing pieces:

- **Block model:** `reading.rs::reading_blocks` + `READING_PARSE_OPTIONS` — the reading view's
  ordered whole-document segmentation (headings, flattened list items with task chars, quote
  depth, code fences, math blocks, diagrams, tables, breaks, opaque HTML).
- **Inline truth:** `links.rs` (wikilinks/embeds/MD links, escape- and code-aware),
  `link_resolver.rs` (resolution + heading-anchor normalization), `citations.rs` +
  hayagriva renderer (`visual_text`), `tasks.rs::task_status_char`, `editor_spans.rs`'s
  `#tag`/`%%comment%%` scanners and its #388 fused-scan composition pattern.
- **Math:** the locked `05` §6.2 pipeline — LaTeX → pulldown-latex MathML → MathCAT
  speech/braille. HTML export emits that MathML directly; the OMML converter (E3) consumes it.
- **Diagrams:** `diagram.rs` — mermaid → SVG + structured AT description, both ready to embed.
- **Embeds:** `embeds.rs::resolve_embed` — the locked FullNote/Section/Block/Image resolution,
  nested pre-resolved to depth 3.
- **Frontmatter:** `frontmatter.rs` typed properties (for the opt-in properties table).
- **Test culture:** proptest in the workspace, differential-oracle precedent in
  `editor_spans.rs`, the adversarial-census methodology.

## Scope decisions (locked for this milestone)

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | **Projection, not a parser.** The exporter consumes `reading_blocks`, the canonical scanners, and the math/diagram/citation bundles. No comrak, no second classifier, no dialect re-derivation — `READING_PARSE_OPTIONS` is the dialect, and there are no per-export dialect knobs. | pulldown-cmark is a locked pick (`05` §2.4); `reading.rs`'s own rule is "the specialized-kind rules are reused, never re-derived." A parallel parse *will* diverge from the reading view on wikilinks, tasks, math, frontmatter. |
| 2 | **Reading-view parity, both directions.** Export renders nothing the reading view doesn't (no footnotes, no callout styling, no `==highlight==` — they inherit in the same milestone the dialect gains them) and loses nothing it shows. Raw HTML is escaped, never interpreted (`ReadingBlockKind::Html` is opaque source). Heading levels are preserved exactly as authored — no renumbering to please checkers. | What an AT user hears in Slate is what the artifact's reader gets; that equivalence is the product. Escaping raw HTML also deletes the XSS class and any sanitizer dependency. |
| 3 | **OMML gates DOCX (v1).** DOCX does not ship until math is real OMML (`m:oMath`/`m:oMathPara`) — what Word's math tools, NVDA, and JAWS actually read. The MathML-Core→OMML converter is its own milestone (E3) with per-expression LaTeX fallback + typed warning; the export never fails on math. | Decided 2026-07-11 (CJ, stronger than the reviewer's recommendation). Monospace LaTeX in a shared Word doc is not accessible math; HTML gets real MathML for free either way. |
| 4 | **Content follows the reading experience.** `![[…]]` embeds transclude (depth-3 `resolve_embed`, labeled containers, cycle-safe); frontmatter — which the reading view never renders (the Properties panel owns it) — is omitted by default, with an opt-in typed properties table since an exported file has no Properties panel. | An exported doc that silently drops transcluded content reads differently than the note; properties are metadata, not body, and often private-ish. |
| 5 | **No network, ever; self-contained artifacts.** Local images embed (DOCX media parts; HTML `data:` URIs). Remote images are never fetched — HTML keeps the URL, DOCX renders a hyperlink + alt, both warn. No HTTP client enters the dependency tree. | Privacy/local-first; kills the SSRF surface and the fetch-policy option space outright. |
| 6 | **Deterministic to the byte.** Same note + same options → identical bytes: fixed zip entry order/timestamps, monotonic rIds/bookmark/num ids, no wall-clock reads (timestamps only via options), no creator metadata. Double-run BLAKE3 equality is censused. | Project-wide invariant (determinism has decided library choices before); also what makes golden tests trustworthy. |
| 7 | **Images: honest alt or honest silence.** `descr` = author alt; empty alt → the decorative mark (HTML `alt=""`; DOCX decorative `extLst` copied from a Word-authored fixture), never fabricated filename-alt. Unsupported formats (webp/heic/svg) degrade to placeholder + warning. Widths clamp to the 6.5″ content width at 96 dpi. | Fake alt is worse than declared-decorative; an unclamped screenshot overflows the page for every reader. |
| 8 | **Privacy strips by default.** `%%comments%%` never leave the vault unless `include_comments` is set; trailing `^block-id` markers become bookmarks/anchors, not visible text; nothing identifies the author unless explicitly passed. | Comments are private annotations; leaking them into a shared file is the worst-case export bug. |
| 9 | **Verification is structural, differential, and adversarial.** Per-format a11y walkers run in *every* test (DOCX: heading styles + outlineLvl, real numbering + restart isolation, hyperlinks, `tblHeader`, descr-or-decorative; HTML: lang/title/alt/scope/labels/no-raw-HTML). Differential oracles pin the IR to core's own extractors (`extract_links`, `tasks.rs`, `reading_blocks`, `extract_citations`). Censuses loop until clean. Human AT pass is a recorded residual, not a CI gate. | The draft's five acceptance criteria, kept and extended — regressions in any mapping must fail loudly, and "walker passes" must mean "core agrees," not "exporter agrees with itself." |
| 10 | **Own OOXML writer.** Hand-rolled parts over the `zip` crate (`xml.rs` escaping helpers; zero XML-parsing deps in prod; `roxmltree` dev-only). `docx-rs` rejected: unmaintained ~3 years, all five a11y-critical features unverified-or-missing, and a patch layer would mean two sources of truth for the XML. New deps (`zip`, `imagesize`, `roxmltree`) must clear the audit + license CI jobs in their first PR. | The DOCX subset Markdown needs is closed; the a11y-critical XML **is** the product and must be under direct control. Matches the house pattern of owning format-critical paths (wikilink scanner, JSON-Canvas, YAML walk). |
| 11 | **Bytes-out product surface.** `VaultSession::export_note(path, format, opts) → ExportReport { bytes, warnings }`; callers own placement (NSSavePanel / share sheet / CLI `--out`). Every degradation is a typed warning — no silent caps. UI announces completion + warning summary through the existing conduit and never moves VO focus. | One entry point, testable core, and the no-silent-degradation rule the repo already lives by. |
| 12 | **References section defaults on.** When citations resolve against the session bibliography, exports append a hayagriva-rendered References section for cited keys (flag to disable; no sources configured → no-op). | A shared document with citations and no references is incomplete; the renderer already exists (Milestone L). |

## Milestone plan

| Phase | Scope | Exit gate |
|-------|-------|-----------|
| **E1** | Export IR (`ExportDoc`) + inline fusion; full-dialect HTML emitter; walker/oracle/census harness; CLI `--format html` + uniffi | Oracle parity + clean census; HTML walker green in every test |
| **E2** | Images (resolution, data URIs, alt/decorative, format matrix, confinement) + embed transclusion | HTML export product-complete |
| **E3** | MathML-Core → OMML converter + fallback + golden/census verification | Gates E4; recorded NVDA/Word residual |
| **E4** | Own OOXML writer: parts/styles/numbering, document walker, images/transclusion/OMML, CLI `--format docx` | Five-criteria walker in every test; determinism census; Word + LibreOffice open-smoke |
| **E5** | Mac File → Export… UI; properties table + References; HTML token spans; docs + recorded human AT pass | Program close |

## Issue map

| ID | Issue | Track | Depends on | Labels |
|----|-------|-------|-----------|--------|
| E1-1 [#812](https://github.com/coryj627/slate/issues/812) | Export IR — `ExportDoc` from `reading_blocks` + inline fusion, options/report types | Rust | — | `backend` |
| E1-2 [#813](https://github.com/coryj627/slate/issues/813) | HTML emitter — full dialect, MathML, diagram SVG + description, deterministic bytes | Rust | E1-1 | `backend`, `a11y` |
| E1-3 [#814](https://github.com/coryj627/slate/issues/814) | Export test harness — HTML a11y walker, differential oracles, censuses, determinism | Rust | E1-2 | `backend`, `test` |
| E1-4 [#815](https://github.com/coryj627/slate/issues/815) | CLI `slate export --format html` + uniffi `export_note` | Rust/CLI | E1-2 | `backend` |
| E2-1 [#816](https://github.com/coryj627/slate/issues/816) | Image embedding — resolution, data URIs, alt/decorative policy, format matrix, confinement | Rust | E1-2 | `backend`, `a11y` |
| E2-2 [#817](https://github.com/coryj627/slate/issues/817) | Embed transclusion — note/section/block containers, depth-3 cycle census | Rust | E1-2 | `backend` |
| E3-1 [#818](https://github.com/coryj627/slate/issues/818) | MathML-Core → OMML converter — structure mapping, nary/accents/matrices | Rust | — | `backend`, `a11y` |
| E3-2 [#819](https://github.com/coryj627/slate/issues/819) | OMML fallback + verification — golden fixtures, census, NVDA/Word residual | Rust | E3-1 | `backend`, `a11y`, `test` |
| E4-1 [#820](https://github.com/coryj627/slate/issues/820) | OOXML writer skeleton — parts, styles, numbering, settings, deterministic zip | Rust | E1-1 | `backend`, `a11y` |
| E4-2 [#821](https://github.com/coryj627/slate/issues/821) | document.xml walker — blocks/inlines, hyperlinks + bookmarks, tables with `tblHeader` | Rust | E4-1 | `backend`, `a11y` |
| E4-3 [#822](https://github.com/coryj627/slate/issues/822) | DOCX images + transclusion + OMML embedding; five-criteria walker everywhere | Rust | E4-2, E3-2, E2-1, E2-2 | `backend`, `a11y`, `test` |
| E4-4 [#823](https://github.com/coryj627/slate/issues/823) | CLI `--format docx` + binary stdout guard; cross-format determinism census | Rust/CLI | E4-3 | `backend` |
| E5-1 [#824](https://github.com/coryj627/slate/issues/824) | Mac File → Export… — save panel, options sheet, progress + VO announcements | Swift | E1-4 (HTML), E4-4 (DOCX) | `swift-ui`, `a11y` |
| E5-2 [#825](https://github.com/coryj627/slate/issues/825) | Properties table mode + References/bibliography section | Rust | E1-2 | `backend`, `a11y` |
| E5-3 [#826](https://github.com/coryj627/slate/issues/826) | HTML code-block token spans + program docs + recorded human AT pass | Rust | E4-4, E5-1 | `backend`, `a11y` |

**Sequencing notes.** E3 has no dependency on E1/E2 and can proceed in parallel; it gates only
E4-3. HTML ships product-complete at E2 while OMML lands. E5-1 can ship HTML-only export UI
after E1-4 and grow the DOCX option when E4-4 merges.
