# Note Export — Semantic HTML + DOCX (Executable Spec)

**Component:** `crates/slate-core/src/export/` (module in `slate-core`, not a standalone crate)
**Scope:** Export a single Slate note to **semantic HTML** and **DOCX**, fully in-process. No pandoc, LibreOffice, LaTeX toolchain, or any external binary at runtime.
**Program:** [../00_plan.md](../00_plan.md) · GitHub [milestone 36](https://github.com/coryj627/slate/milestone/36) · issues [#812](https://github.com/coryj627/slate/issues/812)–[#826](https://github.com/coryj627/slate/issues/826)
**Provenance:** rewritten 2026-07-11 from a chat-authored draft after review against the shipped core; Section 1 records the rejected alternatives so they aren't re-proposed.

---

## 0. The one-sentence architecture

Export is a **new representation over the canonical structure Slate already owns** (05 §1.2 "one canonical structure, many accessible representations") — not a new parser. One intermediate `ExportDoc` is built from the existing reading-view segmentation and canonical inline scanners; two emitters (`html.rs`, `docx/`) project it. Nothing about the note's meaning is re-derived.

---

## 1. Rationale record — rejected alternatives

| # | Draft proposed | This spec does | Why |
|---|---|---|---|
| 1 | Parse with **comrak** | Parse with **core's existing pipeline** (pulldown-cmark + canonical scanners) | pulldown-cmark is a **locked pick** (05 §2.4). `reading.rs` states the rule: "the specialized-kind rules are **reused, never re-derived**." A comrak parse is a second classifier that will diverge from the reading view on wikilinks, tasks, math, code fences, frontmatter. |
| 2 | Generic CommonMark+GFM input | **Slate dialect** input: wikilinks/embeds (`links.rs`, `embeds.rs`), math (`math.rs`), citations (`citations.rs`), tags/`%%comments%%` (`editor_spans.rs` scanners), tasks w/ custom status chars (`tasks.rs`), frontmatter (`frontmatter.rs`), diagrams (`diagram.rs`) | The draft ignored every Slate-specific construct — the actual content of real notes. |
| 3 | `docx-rs` writer | **Own OOXML writer** (plan decision 10) | All five a11y-critical OOXML features are unverified-or-missing in docx-rs (unmaintained ~3 yrs; old `zip 0.6`/`xml-rs` tree); a patch layer would mean two sources of truth for the XML. The needed DOCX subset is closed. |
| 4 | Free function `export_note(markdown, …)` | **`VaultSession::export_note(path, …)`** + a pure, resolver-injected core for tests | Wikilink/embed/image/citation resolution requires the session (`resolve_embed`, `read_attachment`, `set_bibliography_sources`). A bare-string API can't do the dialect. |
| 5 | Footnotes in scope | **Out of scope** — footnotes are not in the reading dialect (`READING_PARSE_OPTIONS` = tables ∣ strikethrough ∣ tasklists; no footnote span kind) | Export must never render syntax the reading view doesn't. When the dialect gains footnotes, export inherits them in the same milestone. |
| 6 | Math unmentioned | **MathML in HTML (free, existing pipeline); OMML required for v1 DOCX** (plan decision 3) | Locked math pipeline (05 §6.2): LaTeX → pulldown-latex MathML → MathCAT speech/braille. OMML is what Word + NVDA/JAWS actually read. |
| 7 | Publishable crate, license header, edition 2021 | **Internal module** of `slate-core`; edition is already **2024**, MSRV 1.89 | Monorepo ADR (13): core is internal-only. Publishing framing deleted. |
| 8 | `ammonia` + optional raw-HTML passthrough | **No raw HTML ever** — render HTML blocks/inlines as escaped code | Reading-view parity: `ReadingBlockKind::Html` is "raw — rendered as monospace source, never interpreted". Kills the XSS class and the ammonia dep outright. |
| 9 | Remote images behind `allow_remote_images` | **No network, ever.** HTML keeps the remote URL in `src`; DOCX renders a hyperlink | Privacy/local-first; deletes the HTTP-client dependency and its SSRF surface. |
| 10 | `image` crate for dimensions | **`imagesize`** (header-only dimension read, no decoder) | Orders of magnitude lighter; we never decode pixels. |
| 11 | GfmOptions per-export toggles | **No dialect knobs.** The dialect is `READING_PARSE_OPTIONS` — one const, same as the reading view | Fewer knobs, no untested combos, structural parity guarantee. |
| 12 | — (absent) | **`%%comments%%` stripped by default** (privacy), block-ID suffixes stripped, deterministic bytes, vault-root confinement for attachment resolution | Repo invariants: structural credential safety (M), determinism (P), no silent leaks. |

Kept from the draft, gladly: the five DOCX a11y acceptance criteria, assert-on-unzipped-OOXML testing, the format-agnostic sink seam, PDF/ODT scope cuts, and "a structurally valid file a screen reader cannot navigate is a failure."

---

## 2. Goals and non-goals

### Goals
- `VaultSession::export_note(path, format, opts)` → **bytes + warnings**; the caller (Mac save panel / share sheet, CLI `--out`) decides where bytes go.
- Pure Rust, in-process, deterministic output (same note + same options → identical bytes).
- **Accessibility is the primary acceptance criterion.** DOCX: real heading styles + outline levels, real numbering, real hyperlinks, flagged table header rows, alt text or decorative marking on every image, real OMML math, document language set. HTML: semantic elements, `lang`, MathML, labeled figures.
- **Reading-view parity.** What an AT user hears in Slate's reading view is what a reader of the exported artifact gets. Export never renders more (footnotes, callouts) or less (embeds, math) than the reading view.

### Non-goals (deliberate cuts)
- **PDF** — reachable via print-to-PDF from Word or a browser. Keep `ExportFormat` `#[non_exhaustive]` so it can return.
- **ODT** — DOCX opens in LibreOffice; Save As covers it.
- **Vault/folder/multi-note export** — v1 is single-note. The options/warnings shapes are `#[non_exhaustive]` so cross-note link modes can be added without breaking call sites.
- **Raw HTML passthrough, remote fetching, footnotes, `==highlight==`** — not in the current dialect / posture (Section 1, rows 5, 8, 9).

---

## 3. Reuse map (the actual design)

| Concern | Owner (existing) | Export uses it for |
|---|---|---|
| Block segmentation | `reading.rs::reading_blocks` + `READING_PARSE_OPTIONS` | The block skeleton of `ExportDoc`: headings, paragraphs, flattened list items (depth/ordered/task char), quote depth, code fences, math blocks, diagrams, tables, breaks, opaque HTML |
| Table cells | `reading.rs::reading_table_cells` | Header + body cell extraction (single source with the block walk) |
| Wikilinks / embeds / MD links | `links.rs` (scanner), `link_resolver.rs` (resolution + heading-anchor normalization) | Inline link runs, display-text rules, same-note anchor slugs/bookmarks |
| Embed transclusion | `embeds.rs::resolve_embed` (locked API, depth 3) | `![[note]]` / `#section` / `^block` / image resolution, cycle-safe |
| Math | `math.rs` (LaTeX → MathML via pulldown-latex; MathCAT speech/braille) | HTML `<math>` directly; OMML converter (Section 7) consumes the same MathML |
| Diagrams | `diagram.rs` (mermaid → SVG + structured description) | HTML inline SVG + accessible description; DOCX description + source fallback |
| Citations | `citations.rs` + hayagriva renderer (`RenderedCitation.visual_text`) | Inline citation text; optional References section |
| Tasks | `tasks.rs::task_status_char` grammar | Task glyph/bracket rendering incl. custom status chars |
| Tags / comments | `editor_spans.rs` scanners (`scan_comments`, tag scanner) | Tag styling; **comment stripping** |
| Frontmatter | `frontmatter.rs` (typed properties) | Opt-in properties table (plan decision 4) |
| Attachments | `VaultSession::read_attachment` | Image bytes + mime, vault-root confined |
| Inline fusion precedent | `editor_spans.rs` (#388 fused scan; masks comments/code; `#[cfg(test)]` differential oracles) | The exact composition + testing pattern `inline.rs` follows |

**Module layout**

```
crates/slate-core/src/export/
  mod.rs        // ExportFormat, ExportOptions, ExportReport, errors, session glue
  doc.rs        // ExportDoc IR: blocks + inline tree (canonical structure)
  inline.rs     // inline fusion: pulldown events ∪ canonical scanners → inline tree
  html.rs       // ExportDoc → semantic HTML (self-contained single file)
  docx/
    mod.rs      // orchestration; part assembly; deterministic zip
    xml.rs      // escaping + tiny element-writer helpers (no XML dep in prod)
    styles.rs   // styles.xml (Heading1–6, Normal, Quote, CodeBlock, Hyperlink, ListParagraph, Table)
    numbering.rs// numbering.xml: 2 abstract defs; per-list concrete instances
    document.rs // document.xml walker (blocks → w:p/w:tbl/w:hyperlink/drawings/oMath)
    media.rs    // image parts, content types, rels, EMU sizing + clamp
  math_omml.rs  // MathML-Core → OMML converter (Section 7; own milestone)
```

### 3.1 The IR (`ExportDoc`)

Blocks mirror `ReadingBlockKind` with payloads resolved for emission: each block carries an **inline tree** (not flat spans — nesting must round-trip: bold inside a link inside a list item), tables carry `header: Vec<InlineTree>` + `rows`, images carry `{bytes, mime, dims, alt: Option<String>}`, math carries the full `{source, mathml, speech}` bundle, diagrams `{svg?, description, source}`, embeds a labeled sub-`ExportDoc`.

### 3.2 Inline fusion (`inline.rs`)

Per block source slice, in `editor_spans`' proven order:
1. Collect masked ranges from canonical scanners: `%%comments%%` (dropped), wikilinks/embeds (`links.rs`, already code-suppressed and escape-aware), citations, inline `$math$` (`math.rs` delimiter scanner), inline `#tags`.
2. Run pulldown-cmark (same options const) with `into_offset_iter()`; interpret emphasis/strong/strikethrough/code/links/images **only outside masked ranges**.
3. Splice scanner tokens into the tree at their byte offsets; thread a `RunFormat { bold, italic, strike, code }` down to leaves.
4. Strip trailing ` ^block-id` markers from block ends (they're addresses, not content); attach the id to the block for bookmark/anchor emission instead.

Every offset is a UTF-8 byte offset into the same host source — the coordinate system all core scanners already share.

---

## 4. Node → output mapping (full dialect)

| Construct (owner) | HTML | DOCX |
|---|---|---|
| Heading 1–6 | `<hN id="slug">` — ids via `link_resolver`'s heading normalization, so `[[#Heading]]` anchors resolve identically to in-app | `pStyle Heading{N}` + `outlineLvl N-1` + `bookmarkStart/End` named by the same slug |
| Paragraph | `<p>` | `Normal` |
| Bold / italic / strike / inline code | `<strong>/<em>/<del>/<code>` | run props `b`/`i`/`strike`; code runs: monospace + shading + `w:noProof` (kills spell-check noise for AT) |
| Markdown link (external) | `<a href>` | `w:hyperlink r:id` (rel `TargetMode="External"`) + `Hyperlink` char style |
| Markdown link (internal `note.md#frag`) | display text (plain); same-note fragment → `<a href="#slug">` | same-note fragment → `w:hyperlink w:anchor`; cross-note → display text |
| Wikilink `[[t]] / [[t\|d]] / [[t#h]]` | display text exactly as the inline pipeline computes it (alias ▸ resolved title ▸ raw target), `<span class="wikilink">`; same-note `[[#h]]` → real anchor | same; same-note → bookmark hyperlink |
| Embed `![[…]]` (note/section/block) | transcluded content in `<section class="embed" aria-label="Embedded: {title}">`, depth-3 via `resolve_embed`; unresolved → labeled placeholder + warning | transcluded blocks bracketed by an `Embed` label paragraph + left-indent style |
| Embed `![[img]]` / `![alt](img)` | `<img alt src="data:{mime};base64,…">` (self-contained) | `wp:inline` drawing, `docPr/@descr` = alt; **empty alt → decorative extension**, not fake alt |
| Remote image | `<img>` keeps the remote URL (reader's browser decides; we never fetch) | hyperlink to the URL + alt text; warning |
| Bullet / ordered list | `<ul>` / `<ol start>` | `numPr` referencing a **fresh concrete num per top-level list** (prevents Word continuing 1-2-3 across separate lists) + `startOverride` for `start≠1`; item depth = `IndentLevel`; continuation paragraphs in an item: `ListParagraph` + indent, **no** `numPr` |
| Task item (any status char) | `<li class="task">` with `<input type=checkbox disabled [checked]>` for ` `/`x`/`X`; other chars → `[c]` literal prefix | paragraph (no numPr), depth indent; ` `→☐ U+2610, `x/X`→☑ U+2611, other → literal `[c]` (always announceable); optional SDT checkboxes = later polish |
| Blockquote (depth n) | nested `<blockquote>` | `Quote` style, indent × depth, left border |
| Code fence | `<pre><code class="language-x">` (escaped; no JS) | `CodeBlock` style, one paragraph per line, `contextualSpacing`, `noProof` |
| Math inline / block (`math.rs`) | real `<math>` (MathML from the existing pipeline), display attr per style | **OMML** `m:oMath` / `m:oMathPara` (Section 7); per-expression fallback → monospace LaTeX + warning |
| Mermaid diagram | `<figure role="img" aria-label="{structured description}">` + inline SVG + `<details>` with source | structured-description paragraph + source as code block (SVG-in-DOCX needs a PNG fallback we can't produce without a rasterizer; `resvg` is a future option) |
| Table | `<table><thead><th scope="col">` | `w:tbl` + table style; row 0 `trPr/tblHeader` + `tblLook firstRow="1"`; cells via `reading_table_cells` |
| Thematic break | `<hr>` | empty paragraph + bottom border (decorative, as Word itself does) |
| HTML block / inline | escaped, as `<pre><code>` / `<code>` — **never interpreted** (reading-view parity; exported HTML contains no markup we didn't generate) | monospace source |
| `#tag` | `<span class="tag">#tag</span>` (text, no link) | plain run |
| `%%comment%%` | **stripped** (default; `include_comments` opt-in for personal archiving) | stripped |
| `^block-id` suffix | stripped from text; becomes the block's `id`/bookmark | stripped; bookmark |
| Citation `[@key]` (`citations.rs`) | rendered `visual_text` from the CSL renderer; unresolved → raw + warning | same |
| Frontmatter | omitted by default; `properties: PropertiesTable` renders typed table after the title (plan decision 4) | same — 2-col table w/ flagged header row |
| Callouts / footnotes / `==highlight==` | **not in today's dialect** — render as their current reading-view forms (plain quote / plain text). Reserved mappings (HTML `<aside role="note">`, DOCX shaded single-cell table) activate in the same milestone the reading view gains them | same |

Heading levels are **preserved exactly as authored** — a note that jumps H1→H3 exports that way. Rewriting the author's hierarchy to please a checker is a worse a11y failure than the skip; parity wins.

---

## 5. Public API

```rust
// crates/slate-core/src/export/mod.rs

#[non_exhaustive]
pub enum ExportFormat { Html, Docx }

#[non_exhaustive]
pub struct ExportOptions {
    /// Document title. Callers pass the note's filename stem (the vault
    /// convention); fallback: first H1, then "Untitled".
    pub title: Option<String>,
    /// BCP-47; default "en". HTML <html lang>; DOCX docDefaults w:lang
    /// + settings themeFontLang.
    pub lang: String,
    /// Embed local images (bytes in DOCX, data: URIs in HTML). Default true.
    /// false → alt-text placeholder + warning.
    pub embed_images: bool,
    /// Frontmatter rendering. Default Omit (reading-view parity).
    pub properties: PropertiesMode,      // Omit | Table
    /// Include %%comments%%. Default false (privacy).
    pub include_comments: bool,
    /// Append a References section when citations resolve against the
    /// session bibliography. Default true (a shared doc with citations
    /// and no references is incomplete). No sources configured → no-op.
    pub bibliography: bool,
    /// Optional created/modified for DOCX core.xml. None → omitted
    /// entirely (determinism; no wall-clock reads in core).
    pub timestamps: Option<ExportTimestamps>,
}

#[non_exhaustive]
pub struct ExportReport {
    pub bytes: Vec<u8>,
    /// Typed, per-site degradations: UnresolvedLink, MissingAttachment,
    /// UnsupportedImageFormat, OmmlFallback, EmbedDepthLimit,
    /// UnresolvedCitation… Never silent (repo rule: no silent caps).
    pub warnings: Vec<ExportWarning>,
}

impl VaultSession {
    pub fn export_note(&self, path: &str, format: ExportFormat, opts: &ExportOptions)
        -> Result<ExportReport, VaultError>;
}
```

- **Testability seam:** internally, IR construction takes an `ExportResolver` trait (resolve link title / embed / attachment / citation); `VaultSession` implements it, unit tests use fixtures. Emitters are pure `ExportDoc → bytes` — no vault, no I/O.
- **UniFFI:** one `#[uniffi::export]` method mirroring the session call; bytes cross as `Vec<u8>` → `Data`. Swift calls off-main and hands bytes to `NSSavePanel`/share sheet; completion announced via the app's existing announcement conduit.
- **CLI (slate.cli.v1-conformant additive verb):** `slate export <vault> <note> --format html|docx [--out FILE] [--properties] [--include-comments]`. `--out` defaults to the note stem + extension in the CWD; DOCX refuses bare stdout unless `--out -` (binary safety); warnings → stderr; existing exit-code discipline.
- No `w:creator`/author metadata is ever written unless explicitly provided (privacy). `app.xml` Application = "Slate".

---

## 6. DOCX writer (own OOXML emission)

Plan decision 10: hand-rolled, purpose-built for the a11y contract. Parts:

`[Content_Types].xml`, `_rels/.rels`, `word/document.xml`, `word/styles.xml`, `word/numbering.xml`, `word/settings.xml`, `word/_rels/document.xml.rels`, `docProps/core.xml`, `docProps/app.xml`, `word/media/*`.

Ground rules:
- **XML by construction, not by crate:** `xml.rs` provides escaping + element helpers; prod code has zero XML-parsing deps. Tests parse with `roxmltree` (dev-dep).
- **Determinism:** fixed zip entry order, fixed compression level, fixed DOS timestamps (1980-01-01), monotonically allocated `rId`s/bookmark ids/num ids, no wall-clock anywhere. Census-tested (two runs → identical BLAKE3).
- **Styles:** `Heading1..6` (`w:styleId Heading{N}`, `w:name "heading N"`, basedOn Normal, `qFormat`, **`outlineLvl N-1` in the style's `pPr`** — this is what drives JAWS/VoiceOver/NVDA heading nav and the Navigation Pane), `Normal`, `Quote`, `CodeBlock` (monospace, shading, noProof), `Hyperlink` (character), `ListParagraph`, one table style. Sizes in half-points.
- **Language:** `styles.xml` `docDefaults/rPrDefault/rPr/w:lang @w:val` (+ `settings.xml` `themeFontLang`). *(The draft put `w:lang` in settings.xml — that alone doesn't set run language for AT.)*
- **Numbering:** two abstract definitions (bullet: `•/◦/▪` cycling text glyphs — AT announces list semantics from `numPr`, glyph is cosmetic; decimal: `%N.` per level, 9 levels each). One **concrete `num` per top-level list** + `startOverride` (Section 4, list row).
- **Images:** `wp:inline` + `wp:extent` in EMU (px × 9525 @96dpi), **clamped to 6.5″ content width** preserving aspect (an unclamped screenshot otherwise overflows the page); `docPr @descr` = alt; empty alt → the decorative `extLst` extension (copy the exact extension URI/GUID from a Word-authored fixture kept in `tests/fixtures/` — that fixture is also the golden reference for OMML and tblHeader shapes). Formats: png/jpeg/gif/bmp/tiff pass through; webp/heic/svg → placeholder + `UnsupportedImageFormat` warning (transcode is out of scope; revisit with `resvg`/`image` if demand shows).
- **New deps:** `zip` (prod), `imagesize` (prod), `roxmltree` (dev). All small, maintained, permissive; must clear the audit + license CI jobs in the first PR (the repo gates on both).

## 7. MathML-Core → OMML (`math_omml.rs`, gating milestone)

Plan decision 3: DOCX does not ship until math is real OMML.

- **Input is closed:** only what pulldown-latex emits (MathML Core) — not arbitrary MathML. Element coverage: `mrow, mi, mn, mo, mtext, mspace, mfrac, msqrt, mroot, msub, msup, msubsup, munder, mover, munderover, mtable/mtr/mtd, mstyle/mpadded/mphantom` (style/pass-through).
- **Structure mapping:** `mfrac→m:f`, `msub/msup/msubsup→m:sSub/sSup/sSubSup`, `msqrt/mroot→m:rad` (`degHide` for sqrt), `munder/mover/munderover→m:limLow/limUpp` — except large operators (`∑ ∏ ∫ ⋃ ⋂ …` detected on the base `mo`) which become **`m:nary`** with sub/sup; `mover accent="true"→m:acc`; fence-pair `mo`s → `m:d` delimiters; `mtable→m:m` (matrix); token runs → `m:r/m:t` with `m:sty` italic/normal per `mi` convention. Display blocks wrap in `m:oMathPara`.
- **Graceful degradation, never failure:** any unconvertible node fails **that one expression** → monospace LaTeX-source run + `OmmlFallback` warning naming the node kind. The export always completes.
- **Verification:** golden OMML fixtures authored in Word for a canonical expression set; census over the full LaTeX corpus already in `math.rs` tests + the Milestone X snippet set (zero panics, well-formed XML, fallback rate reported, loop-until-clean per the adversarial-census methodology); **human residual:** NVDA + Word reading pass (MathCAT is NVDA's engine, so parity with in-app speech is the expectation) — recorded, per the Milestone T convention.

---

## 8. Security & privacy invariants

1. **No network, ever** — no HTTP client in the dependency tree.
2. **`%%comments%%` stripped by default** — private annotations don't leak into shared artifacts.
3. **Attachment resolution is vault-root-confined** (session semantics; test pins a `../` escape attempt) — a malicious synced note can't exfiltrate arbitrary local files into a document you then share.
4. **Exported HTML contains no markup we didn't generate** — raw HTML is escaped, always (also reading-view parity).
5. **No identity or wall-clock metadata** unless explicitly passed (no `dc:creator`, no build timestamps; deterministic bytes).
6. `#![forbid(unsafe_code)]` on the module tree (matches core's posture).

---

## 9. Testing strategy

- **Structural asserts (DOCX):** unzip in-memory, parse parts with `roxmltree`, assert: heading → `pStyle Heading{n}` **and** effective `outlineLvl`; list item → `numPr` and no literal `•` in text; consecutive lists → distinct `numId`s (the restart bug); link → `w:hyperlink` with external rel; table row 0 → `tblHeader`; image → non-empty `descr` **or** decorative ext; math → `m:oMath` count matches `extract_math_blocks`; `docDefaults` lang present.
- **A11y walker, both formats:** one function per format enforcing the criteria table above; called by **every** test so any mapping regression fails loudly. (HTML walker parses with `scraper` or equivalent dev-dep: lang/title present, `alt` on every `img`, `th scope`, figure labels, no raw-HTML leakage.)
- **Differential oracles vs core** (the `editor_spans` precedent): links in the IR ≡ `extract_links` (non-code, non-comment); task statuses ≡ `tasks.rs`; block sequence ≡ `reading_blocks`; comments absent from output text; citation sites ≡ `extract_citations`.
- **Determinism:** double-run BLAKE3 equality, both formats, across the corpus.
- **Censuses (proptest is already a workspace dev-dep):** generated + fixture corpora (empty note; headings-only; 10-deep lists; unclosed emphasis; CRLF; NFD filenames; RTL/CJK/emoji; 8 MB body; missing/oversized/unsupported images; embed cycles at depth 3; malformed YAML; pathological `$$` nesting) — invariants: no panic, walker passes, warnings enumerate every degradation. Loop until a full census is clean.
- **Fixtures:** one Word-authored reference .docx (decorative ext, tblHeader, OMML shapes) checked in as the golden donor.
- **Human residual (recorded, not gating CI):** VoiceOver/Safari + Word/NVDA pass over a real exported note set — same convention as Milestone T's AT smoke.

---

## 10. Decisions

The locked scope decisions live in [../00_plan.md](../00_plan.md) (table of 12, with rationale). Resolved with CJ 2026-07-11: own OOXML writer; embeds transclude; **OMML required for v1 DOCX**; frontmatter omitted by default with an opt-in typed properties table.

**Remaining open (small):**
- HTML embedded CSS: minimal system-ui sheet with an APCA-checked (Lc > 75) light/dark pair via `prefers-color-scheme`; final palette can borrow from Milestone R tokens.
- Word SDT checkboxes for task items — optional later polish; glyph/bracket runs ship first.

---

## 11. Milestones

- **E1 — IR + HTML emitter + harness** ([#812](https://github.com/coryj627/slate/issues/812)–[#815](https://github.com/coryj627/slate/issues/815)). `ExportDoc` from `reading_blocks` + inline fusion (masks, splices, block-id strip); full-dialect HTML (headings+ids, lists+tasks, quotes, code, tables, links/wikilinks, MathML, diagram SVG+description, escaped-HTML posture, comment stripping); options/report types; determinism; HTML walker + differential oracles + first censuses; CLI verb (`--format html`) + UniFFI export. *Exit gate: oracle parity + clean census.*
- **E2 — Attachments: images + transclusion** ([#816](https://github.com/coryj627/slate/issues/816), [#817](https://github.com/coryj627/slate/issues/817)). Session-integrated resolution; data URIs; alt/decorative policy; format matrix + warnings; note/section/block transclusion containers (depth 3, cycle census); vault-root confinement test. *HTML export is product-complete here.*
- **E3 — MathML→OMML converter** ([#818](https://github.com/coryj627/slate/issues/818), [#819](https://github.com/coryj627/slate/issues/819)). Section 7 in full, standalone; parallel-safe with E1/E2. *Gates E4-3. Human NVDA/Word residual recorded.*
- **E4 — DOCX emitter** ([#820](https://github.com/coryj627/slate/issues/820)–[#823](https://github.com/coryj627/slate/issues/823)). Own writer per Section 6, full mapping table incl. OMML, images, transclusion, bookmarks/anchors; five-criteria walker on every test; determinism census; Word + LibreOffice open-smoke. CLI gains `--format docx`.
- **E5 — Product surface + polish** ([#824](https://github.com/coryj627/slate/issues/824)–[#826](https://github.com/coryj627/slate/issues/826)). Mac File → Export… (save panel, progress + VO announcements, options sheet: format / properties / comments / bibliography); properties table mode; References section; optional tree-sitter token spans in HTML code blocks (zero-JS syntax color from `code.rs`); docs; recorded human AT pass over real exports.

---

## 12. Summary contract

One session call that turns a note into either a self-contained semantic HTML file or a DOCX whose **headings are real heading styles with outline levels, lists are real numbering that restarts correctly, links are real hyperlinks, tables have flagged header rows, images carry alt text or an explicit decorative mark, math is real OMML, and the document language is set** — built as a projection of the same canonical structures the reading view consumes (never a second parse), stripped of private comments, deterministic to the byte, with every degradation surfaced as a typed warning — verified by structural walkers, differential oracles against core's own extractors, and adversarial censuses, because correctness here is measured by whether a screen reader can navigate the result, not by whether it looks right.
