# Reading inline segments — executable spec (#967, W3-1 prerequisite)

Issue: [#967](https://github.com/coryj627/slate/issues/967) · Program: [00_program.md](../00_program.md) (decisions 4/5, §W-A) · Wave spec: [w3_spec.md](w3_spec.md) §W3-1 · Consumer: [#728](https://github.com/coryj627/slate/issues/728). One PR per issue.

**Decision record (owner, 2026-07-19): Option A** — canonicalize inline segments in `slate-core`; the mac `ReadingInlineMapper` migrates onto the core API (Swift logic deleted, the W0.5 shape); W3-1 consumes the same API. No gap_analysis row: this is the decisions-4/5 doctrine path, not a divergence. Evidence base recorded on the issue (Element X timeline / Automerge spans / Signal body-ranges / AccessKit as canonical-core precedents; Babelmark divergence, and iA Writer's still-missing Windows wikilinks, as the duplicated-implementation failure mode). Deciding factors beyond doctrine: core already parses every construct involved (`editor_spans.rs`, `links.rs`, `citations.rs` — the mapper's own header says it "never re-derives syntax"); and the current mac pipeline routes inline CommonMark through Foundation's `AttributedString(markdown:)` — a parser Slate does not control — which Option A retires from the semantic path. The owner also anticipates **more complex inline content types later**; under this contract each lands once in core, never per host.

**Pre-unpark eligibility:** core + FFI + mac migration + §W-A twin serializers only — no WPF app code (the C# twin lives in `apps/slate-windows/tools/ParityHarness`, a W0-3 artifact). Same justification as W0.5: a mac-side refactor verifiable with today's test suite (`ReadingViewTests` — ~40 mapper tests), executable while W1–W8 stay parked.

---

## 1. The model

Today's mac pipeline, per paragraph-family block: Swift strips block chrome (`ReadingBlockSource`), Swift selects core-classified token spans and splices `[label](scheme-url)` markdown (`ReadingInlineMapper`), Foundation parses the resynthesized markdown, Swift walks the attributed runs to restyle, rewrite, and strip affordances. Canonical replacement — **core emits, per reading block, rendered inline segments**:

```
reading_inline_segments_source(
    source: &str,
    citations: &[RenderedCitation],   // the owning note's rendered citations (join key: raw)
    records: &[OutgoingLink],         // the owning note's outgoing-link records (resolution input)
) -> Vec<ReadingBlockInlines>         // 1:1 with reading_blocks_source(source), same order

ReadingBlockInlines {
    segments: Vec<ReadingInlineSegment>,   // empty for non-inline kinds (code/math/diagram/table/html/thematic-break)
    block_embed_key: Option<String>,       // §5 — Some(cache-key) when the block IS one wikilink embed
    list_marker: Option<String>,           // §2 amendment — the authored marker (`-`, `3.`, `12)`), verbatim
}
ReadingInlineSegment {
    content: String,                  // the RENDERED inline text (§2): chrome stripped, token display substituted
    runs: Vec<ReadingInlineRun>,      // partition of content — concat(run slices) == content, no gaps/overlaps
    task_completed: Option<bool>,     // Some for task list-items, from core task semantics (§2)
}
ReadingInlineRun {
    start: u32, end: u32,             // byte offsets into content, half-open
    styles: Vec<ReadingInlineStyle>,  // Emphasis | Strong | Strikethrough | InlineCode — sorted, deduped
    kind: ReadingInlineRunKind,
    ax_text: Option<String>,          // §7 — citation speech text / "Unresolved link"; None otherwise
}
ReadingInlineRunKind {
    Text,
    ExternalLink { url: String },                                     // http/https/mailto (links.rs allowlist)
    Wikilink { target: String,            // anchor-attached authored form ("Note#Sec") — router input
               base_target: String,       // anchor-cut form per grammar (§6)
               anchor: Option<LinkAnchor>,
               grammar: ReadingWikiGrammar,   // Wikilink | MarkdownDestination
               resolved: bool },              // §6
    Embed { key: String },                                            // cache-key form (§5); no resolved field — card-level state owns it
    Tag { name: String },                                             // without '#'; run text keeps the '#'
    Citation { raw: String, speech: String },                         // display text is the run's content slice
}
```

FFI: proc-macro records/enums in `crates/slate-uniffi` with `From<core::…>` impls, the `ReadingBlockKind` mirror convention. Reuse the existing `LinkAnchor`, `RenderedCitation`, `OutgoingLink` FFI types. Core home: `crates/slate-core/src/reading.rs` (same module as the block walk; the runs walker composes `editor_spans`, `links`, `citations`, `tasks` — the no-second-classifier rule extends, never re-derives).

**Flatness rule.** Runs are flat and non-overlapping, in the `AttributedString`/WPF-`Run` shape both hosts consume natively. A logical link whose label carries styles (`[**b** c](t)`) emits adjacent runs with identical `kind` payload; hosts stamp attributes per run and attribute-equality merges the affordance — no host-side grouping.

**Determinism rule.** Output is a pure function of `(source, citations, records)`. Same inputs, same bytes, all platforms — this is what makes the §W-A artifact (§8) meaningful.

## 2. Content derivation (supersedes `ReadingBlockSource` inline paths)

`content` is derived by core, per block kind, reproducing the shipped stripping semantics — pulldown's structure events are the authority, replacing the Swift re-parse (`ReadingBlockSource.headingText`'s ATX/setext handling, list/task marker split, `quoteContent` depth-strip):

- **Paragraph** — the block source verbatim (whitespace-preserving, as `.inlineOnlyPreservingWhitespace` does today).
- **Heading** — text content sans ATX markers/closing-hash run, or the setext first line; trim per shipped rules.
- **ListItem** — content after the list marker; for task items, content after the checkbox, with `task_completed` computed by the same core rule the Tasks panel rows carry (`tasks.rs`; the Swift `taskChar.lowercased() == "x"` fallback retires). **Amendment (implementation PR):** the authored marker itself rides back on `ReadingBlockInlines.list_marker`. The mac renderer displays the AUTHORED ordinal verbatim ("the source carries the real ordinal, so no re-derivation — and no wrong renumbering — is possible", `ReadingView.listItemRow`), so a host without this field would have to re-split the marker out of the block source: exactly the per-host derivation decision 4 forbids. One value, both hosts.
- **BlockQuote** — per-line `>`-prefix strip at the block's depth, lines joined with `\n` (today's `quoteContent` join).
- Degradation contract preserved: when expected chrome is absent, content is the verbatim slice — authored bytes are never dropped.

`ReadingBlockSource`'s inline-content functions are then deleted on mac; presentation-only helpers (fonts, bullets, quote bars) stay.

## 3. Token selection (the `mappableSpans` policy, verbatim)

Over each block's content-bearing source, core selects wikilink/embed/tag/citation spans exactly as the mapper does today: candidates from the span classifier; sort by start, **outermost-first at equal start**; drop spans nested inside a kept span; drop spans overlapping `InlineCode`/`CodeFence`/`Code(_)` ranges (code stays literal); drop spans overlapping CommonMark `Link`/`Image` spans (the markdown construct owns that range). A token whose interior fails to split (`[[]]`) contributes its bytes as plain text.

## 4. Token payloads (the `splitWikiBody`/`mapRun` contracts, verbatim)

- **Wikilink** — interior split on the first `|`; whitespace-trim mirroring `links.rs` (the `[[ Missing ]]` red-team probe stays pinned); anchors stay attached in `target`; run text = alias ?? target, non-empty fallback to target. Implementation reuses `links.rs::split_wikilink_body` — the mapper's Swift re-derivation of it is the drift pocket being deleted.
- **Embed (mid-paragraph run)** — `!`-strip then wikilink split; run text = alias ?? last path component of the anchor-cut base target, never empty; `key` = the cache-key form `target_raw` + (`#`|`^`) + anchor text — **the exact `AppState.embedTargetKey` composition**, which core now owns (`reading_embed_key`; single home, mac delegates to it). Note this is the ANCHOR-CUT `target_raw` plus a re-composed marker, **not** the authored segment verbatim: the retired Swift mapper used the verbatim segment and therefore composed a key the resolution dictionary could not match for the canonical `![[Note#^blk]]` block-ref form (`Note#^blk` vs the record-derived `Note^blk`) or for padded interiors (`![[ Note # Sec ]]`). Recorded as a **core fix** row in the implementation PR's deltas ledger.
- **Tag** — requires `#` prefix and length > 1; run text keeps the `#`, `name` drops it.
- **Citation** — joined in core by `RenderedCitation.raw ==` span text; run text = `visual_text` (fallback: raw), `speech` = `speech_text` (fallback: raw). The Swift `citations.first { $0.raw == … }` matching retires.

## 5. Block-level embed detection (`blockEmbedTarget`, verbatim)

`block_embed_key` is `Some` iff the block is a Paragraph whose selected tokens are **exactly one** span of kind Embed covering every non-whitespace byte (ASCII whitespace set `{0x20,09,0A,0D,0C,0B}`, byte-level scan). Key = §4's cache-key form. Scope pinned as shipped (#511): wikilink embeds only; markdown images never block-expand. The host keeps the card state machine, `BaseEmbedRequest` dispatch, and the inline-leaf fallback — it just stops computing detection.

## 6. CommonMark structure, destinations, and resolution

**Inline walk.** CommonMark inline structure (emphasis/strong/strikethrough/inline-code, links, images, hard/soft breaks) is computed by pulldown-cmark over the token-masked content under `READING_PARSE_OPTIONS` (the factored const — the no-divergence guarantee extends to the inline walk). **Implementation note (this PR) — inline-only, indexed masks.** pulldown-cmark has no inline-only mode, so the walk runs over a *probe*: a scaffold mark at offset 0 (so no line can begin with a block trigger) plus every line ending replaced by a space (so no blank line can split a paragraph). That is what stops a BLOCK re-parse from consuming chrome-stripped bytes — `# ---` strips to `---`, which a block parse renders as nothing. Authored terminators are restored by slicing the unmasked string, so CRLF and mixed-ending fixtures survive; a CRLF collapses to ONE space (CommonMark's rule) with the offset recorded so slices still map back exactly.

Each selected token is masked as `<marker><index><marker>`, where the marker is a single U+FFFC (category So, so a delimiter beside a token flanks exactly as it did beside the retired `[label](url)` splice). Collision-freedom comes from ESCAPING every authored U+FFFC into a literal mask as the masked text is built, not from growing the delimiter: sizing the marker to the longest authored run is collision-free but makes construction Θ(longest_run × token_count), which a crafted or imported note can turn into an out-of-memory crash well under any file-size refusal threshold. Escaping keeps it linear in source size plus token count. Expansion is keyed on the INDEX, never on the order marks are encountered: pulldown exposes a link's destination and title as Tag metadata that no event renders, so a mark can legitimately never be seen, and order-based assignment would then expand every later token with its predecessor's payload — a wrong label and a wrong activation target.

Inside a construct that binds tighter than a link (a code span, raw HTML), a token renders its AUTHORED bytes with no affordance: selection runs on the block-parsed content while the walk runs on the flattened probe, so such a construct can form across a blank line that separated its delimiters at selection time, and code-styled text that silently navigates would be the alternative.

**Splice-equivalence rule:** selected token ranges are opaque to delimiter pairing — a `*` or `` ` `` inside `[[a*b]]` neither opens nor closes anything outside it, and token run text never re-parses (today's escape-label behavior, achieved structurally instead of by backslash escaping; `escapeMarkdownLabel` and the splice machinery die with nothing replacing them).

**Destination classes** (the `style()`-pass rewrite/strip logic, moved down):
- **External** (`links.rs::looks_external` semantics; activation allowlist http/https/mailto) → `ExternalLink { url }`.
- **Internal scheme-less markdown destination** → `Wikilink { grammar: MarkdownDestination }`, target = the authored destination **verbatim** (never percent-decoded — the `target_raw` contract); `^` is a path character in this grammar (the `[[note^block]]` vs `[m](note^block)` round-2/3 fixes stay pinned via `base_target`).
- **Never-activatable** (`file:`/`javascript:`/unknown schemes, protocol-relative `//host`, fragment-only `#anchor`) → plain `Text` run(s) of the label — no dead affordance, visually or to AT.
- **Markdown images** — unchanged shipped behavior (inline `.image` path; not block embeds).

**Resolution** (`resolved`, replacing `isUnresolvedWikiLink` + `LinkRecordSets`): records filtered to `!is_embed && !is_external`, partitioned by record `kind` ("wikilink"/"markdown"); candidate keys per grammar — wikilink grammar cuts at first `#`, else first `^`; markdown grammar cuts only at `#`; verbatim target closes the list (the pre-#509 defense); **first key with a same-grammar record decides** membership in that grammar's unresolved set; no same-grammar record → unresolved (live-buffer links). Empty `records` (the host's stale-ownership window) classifies every link run unresolved — the honest value, exactly today's semantics. Cross-grammar records never vouch (Codex round 3 stays pinned).

**Activation matching.** Core additionally exports `reading_match_link(target, grammar, embed: bool, records) -> Option<u32>` (index of the matching record) so the router's activation path and the render-time classifier share one implementation. Mac deletes `candidateKeys`, `recordKindMatches`, and matching uses of `baseTarget`; the router keeps schemes, URL codec, `recordsBelongToNote` gating, dispositions, and every navigation/announcement action — trigger ownership stays at the interaction sites (WGA-7 boundary).

## 7. Accessible text

`ax_text` carries exactly what the mac stamps as per-range AX custom text today, moved verbatim (decision 18: strings move, no new core copy): citation runs → the citation speech text; unresolved `Wikilink` runs → `"Unresolved link"`; all other runs → `None` (wiki/embed/tag run text is its own accessible text). Hosts stamp it via their per-range AX text mechanism (mac `AccessibilityAttributes.TextCustomAttribute`; Windows per §W-C when W3-1 lands). Announcement-class strings (e.g. activation outcomes) are out of scope here — they live in the W0.5-3 vocabulary.

## 8. §W-A artifact: `inline_runs`

Both serializer twins (`ParityHarnessTests.swift` / `SurfaceSerializer.cs` — "mirrors every rule here; change both together") add a fourth per-file array `inline_runs`, 1:1 with `blocks`:

```
"inline_runs":[{"embed":<key|null>,"marker":<str|null>,"segments":[{"content":<str>,"task":<bool|null>,
  "runs":[{"start":N,"end":N,"styles":["strong",…],"kind":"wikilink:…",…payload fields…,"ax":<str|null>}]}]}, …]
```

Kind strings and their payload fields (implementation PR — `kind` is the
snake_case discriminator with **enum-ish scalars colon-joined**, mirroring
`list_item:{depth}:{ordered}:{task}`; **free-text payloads are separate
escaped fields**, because a URL or target contains `:` and colon-joining
would make the value ambiguous):

| Run kind | `kind` string | Following payload fields, in order |
|---|---|---|
| `Text` | `text` | — |
| `ExternalLink` | `external_link` | `"url"` |
| `Wikilink` | `wikilink:{wikilink\|markdown_destination}:{resolved\|unresolved}` | `"target"`, `"base_target"`, `"anchor"` (`"h:<text>"` / `"b:<text>"` / null) |
| `Embed` | `embed` | `"key"` |
| `Tag` | `tag` | `"name"` |
| `Citation` | `citation` | `"raw"`, `"speech"` |

Style strings: `emphasis`, `strong`, `strikethrough`, `inline_code`, emitted
in core's sorted order (no re-sorting host-side).

**Citation join, deliberately empty in the harness.** The fixture vault
ships no CSL style and no bibliography, so there is nothing deterministic to
render citations against; both twins pass an EMPTY citation list. Core still
emits `citation` runs (the kind comes from the span classifier, not from the
rendered list) with the raw text as both display and speech — deterministic
on every platform. Matched-citation rendering is covered by core unit tests
instead; committing a style + `.bib` fixture to bring it under §W-A is a
recorded W8-4 candidate.

Kind strings follow the harness naming convention (snake_case, colon-joined payloads, mirroring `BlockKindName`); exact field order is pinned by the goldens; canonical-JSON rules unchanged. `citations`/`records` inputs come from the fixture-vault session (`ListCitationsInFile`/`OutgoingLinks`) — deterministic by construction. Corpus grows fixtures covering the six behavior families: alias/anchor/trim probes (`[[ Missing ]]`, `[[a|b|c]]`, `note^draft#sec`), the markdown-destination `^`-grammar probe, resolved/unresolved pairs, tags, citations (unmatched only — see the citation-join note below), emphasis spanning a token, tokens inside inline code/fences, mid-paragraph + block embeds, task/quote/heading chrome — with CRLF/mixed-ending twins (never normalized, decision 9). Goldens committed under `crates/slate-core/tests/fixtures/parity_golden/`.

## 9. Mac migration (the W0.5 shape)

1. `ReadingInlineMapper` becomes a thin applier: segments in → `AttributedString` out (per-run attributes: link URL construction from run kind via the router's schemes, accent/warning + underline policy, `ax_text` stamping). Selection, splitting, splicing, Foundation markdown parsing, destination rewriting, and unresolved classification are **deleted, not left as fallback**.
2. `ReadingBlockSource` inline-content functions deleted (§2); `ReadingLinkRouter` matching helpers deleted (§6); `AppState.embedTargetKey` delegates to the core key (§4). `ReadingPrintComposer` consumes the same segments.
3. **Pre-deletion differential census, in-PR:** old pipeline vs. new applier over the fixture corpus plus randomized documents (adversarial-census methodology), comparing rendered text, per-range link/style/AX attributes, and block-embed detection. Every delta is triaged in a **deltas ledger** in the PR description — {accepted-as-canonical (pulldown-vs-Foundation divergence; e.g. strikethrough support is a known candidate) | core fix} — no silent behavior change. The ledger's accepted rows become golden-pinned canonical behavior.

   **Executed form (implementation PR) — read this before assuming the checkbox below is unconditionally met.** The ledger was produced by a line-by-line differential of the retired implementation against the new one, and **every** row is pinned by a named test rather than by a runtime comparison harness. The literal old-vs-new *runtime* census was **not** run: it requires resurrecting ~750 lines of just-deleted Swift (`ReadingInlineMapper` plus the `ReadingBlockSource` strippers it consumed) as a test-only fixture that the spec then deletes again in the final commit, and it can only execute on macOS CI. Its incremental evidence over the per-delta tests is the randomized-input coverage — which the Rust side already supplies at 100k documents (`census_reading_inline_segments_align_and_partition`, `census_reading_inline_tokens_never_reparse`) against the same pipeline both hosts now consume. **Owner call to reinstate it is open**; if taken, it lands as a follow-up rather than blocking the arc.
4. `ReadingViewTests` behavior tests stay green, re-expressed against segments where they pinned `MappedRun` shapes; test intent (the six families) is preserved 1:1.

## 10. Windows consumption contract (binds W3-1/#728)

C# maps segments/runs to WPF `Run`/`Hyperlink` inlines (+ UIA text ranges per §W-C). The "C# may contain" line for this surface: marshalling, attribute application, gesture wiring, embed-card state machine, focus/navigation. **Prohibited:** any C# markdown/inline parsing on this path (no Markdig), interior splitting, candidate-key or resolution logic, AX-string composition — §W-G grep-audits accordingly.

## Acceptance

- [x] Core `reading_inline_segments_source` + `reading_match_link` + FFI mirrors; unit + golden tests; censuses (content partition invariant, splice-equivalence, blocks↔inlines alignment) per repo convention — plus `reading_embed_key` and `reading_block_embed_key` (§4/§5 single homes the hosts delegate to)
- [x] Mac consumes; Swift selection/splitting/parsing/classification deleted; deltas ledger recorded; `ReadingViewTests` re-expressed (six families 1:1) — **except** the runtime old-vs-new census, see §9 item 3 for what was done instead and the open owner call
- [x] §W-A `inline_runs` in both twins over the grown corpus; goldens committed; CRLF/mixed twins included
- [x] `w3_spec.md` §W3-1 + #728 updated to consume this contract (done in the decision PR); #967 closed by the implementation PR
