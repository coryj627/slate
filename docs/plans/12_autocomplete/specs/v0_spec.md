# V0 executable spec — Completion engine: context, providers, ranking, censuses

Issues: V0-1 ([#568](https://github.com/coryj627/slate/issues/568)) · V0-2 ([#569](https://github.com/coryj627/slate/issues/569)) · V0-3 ([#570](https://github.com/coryj627/slate/issues/570)) · V0-4 ([#571](https://github.com/coryj627/slate/issues/571)) · V0-5 ([#572](https://github.com/coryj627/slate/issues/572)) · V0-6 ([#573](https://github.com/coryj627/slate/issues/573)).
Milestone: [GH 29](https://github.com/coryj627/slate/milestone/29). One PR per issue.
Program: [00_program.md](../00_program.md) (locked decisions 1–4, 8–9; DoD §V-C/§V-D/§V-E). Backend norms apply: fmt/clippy pre-push, censuses for correctness invariants, **host-independent slate-core (no macOS deps)**.

**Execution order: V0-1 → V0-2 → {V0-3 ∥ V0-4 ∥ V0-5} → V0-6.** (V0-6's census harness may be developed alongside V0-3; it gates the wave.)

## Baseline facts (verified 2026-07-04, this worktree)

- **Incremental buffer:** `crates/slate-core/src/doc_buffer.rs` — `DocBufferState { buffer: TextBuffer, structure, body_structure, fm_end, comment_index }` (struct ~:35, `fm_end` ~:57). `apply_edit(start_utf16, old_len_utf16, new_text)` (~:101) maintains block structure incrementally per keystroke; this is the hook path the word index (V0-3) and classifier (V0-2) ride. `TextBuffer` (`text_buffer.rs`, `ropey::Rope`, ~:44) provides byte↔UTF-16↔line conversions.
- **Editor spans:** `crates/slate-core/src/editor_spans.rs` — `EditorSpanKind` (~:49–90: Heading, Emphasis, Strong, InlineCode, CodeFence, Wikilink, Embed, Tag, Citation, Comment, Frontmatter, Link, Image, BlockQuote, Code(token)). **No `Math` or `Callout` variant today** — V0-2 adds them. `highlight_spans(source) -> Vec<EditorSpan>` (~:133) + the ranged variant used for #379 windowed highlighting.
- **Links:** `crates/slate-core/src/links.rs` — `LinkKind`/`LinkAnchor`/`ParsedLink`, `extract_links()` (~:123) walks source to byte-indexed link records. `crates/slate-core/src/link_resolver.rs` — `VaultIndex` trait with `all_paths() -> Box<dyn Iterator<Item=&str>>` (~:52–57), `ResolvedLink::{Resolved{target_path,anchor}, Unresolved{target_raw,anchor}, External}` (~:77–98), resolution rules (folder-qualified → basename → distance → alphabetic, ~:21–39). `links_db::OutgoingLink` (~:22–40).
- **Front matter:** `crates/slate-core/src/frontmatter.rs` — `extract_frontmatter(src) -> (Vec<Property>, Vec<Warning>)` (~:49), `Property { key, value, .. }` (~:79, value types text/number/boolean/date/datetime/wikilink/list/tag_list), `body_after_frontmatter` (~:85). `crates/slate-core/src/properties_db.rs` — `replace_properties_for_file(tx, file_id, src)` (~:36); schema `migrations/005_properties.sql` (`properties`, `properties_list_values`).
- **Fuzzy scorer (host-side reference, to be mirrored/ported):** `apps/slate-mac/Sources/SlateMac/CommandPaletteModel.swift` `fuzzyScore(query:target:) -> Int?` (~:299–331; pure `nonisolated static`; 10/match, +5 word-boundary, +3 consecutive, +50 prefix). `QuickSwitcherModel.swift` `score` (~:195–207) adds `nameMatchBonus=20`; tie-break score → recency → path (~:166–176). V0 ports the scoring rules into slate-core so ranking is deterministic and testable without the UI; Swift keeps using its copy for command/file search unchanged.
- **No trie/prefix structure exists** anywhere in `crates/slate-core/src/`. Full-text search uses SQLite FTS5; command/file filtering uses `Vec` + fuzzy. V0-3 builds the first prefix/frequency structure.
- **Census convention:** `census_*` fns in `crates/slate-core/src/session/tests/*.rs`; `census_scale()` (link_integrity.rs ~:13–18, 40 seeds / 120 under `SLATE_CENSUS_FULL=1`); adversarial-census methodology (random + exhaustive small-vault sweep) per project standard.
- **Bench harness:** `crates/slate-core/benches/scan_bench.rs` (criterion); synthetic vault generator `benches/common/mod.rs` `generate_vault(file_count)` (~:93–99) at 1k/10k/50k; `make bench`; baselines in `BENCHMARKS.md`.
- **Workspace deps** (`Cargo.toml`): `unicode-normalization` is already a direct dep (NFC, #459) — reuse for case/diacritic folding. No new crate is required by V0; if a prefix structure warrants one, justify inbound-compatible licensing (project is AGPL-3.0-or-later) in the PR.

---

## V0-1 · Completion engine core — context, provider trait, ranking (#568) — PR 1

New module `crates/slate-core/src/completion/` (`mod.rs`, `context.rs`, `engine.rs`, `item.rs`).

```rust
/// Everything a provider needs, derived once per query. Pure data — no session, no I/O.
pub struct CompletionContext<'a> {
    pub before: &'a str,   // text from the line/look-back start up to the caret
    pub token: &'a str,    // current query token (see tokenizer)
    pub token_start_utf16: u32, pub token_end_utf16: u32, // replace range = the token
    pub site: CompletionSite,   // from V0-2; BodyWord for V0-1 unit tests
}

pub struct CompletionConfig {   // mirrors completr settings; defaults per research brief §1
    pub character_class: WordCharClass,   // default a-zA-Z0-9 + Unicode letters (configurable, §V2-1)
    pub max_look_back: u32,               // 50
    pub min_word_length: u32,             // 2  (min length to *show* an item)
    pub min_word_trigger_length: u32,     // 3  (min token length to auto-trigger)
    pub word_insertion_mode: WordInsertionMode, // MatchCaseReplace | IgnoreCaseReplace | IgnoreCaseAppend
    pub ignore_diacritics: bool,          // false
    // per-provider toggles + latex_* live here too; providers read what they need.
}

pub trait CompletionProvider {
    fn complete(&self, ctx: &CompletionContext, cfg: &CompletionConfig) -> Vec<CompletionItem>;
    /// If true and this provider returns ≥1 item, no other provider runs (completr blocksAllOtherProviders).
    fn blocks_all_other_providers(&self, _ctx: &CompletionContext) -> bool { false }
}

pub struct CompletionItem {
    pub label: String,          // shown to the user
    pub replacement: String,    // inserted text (may be a snippet template, see V0-4)
    pub replace_start_utf16: u32, pub replace_end_utf16: u32, // default = token range; providers may widen
    pub kind: CompletionKind,   // Word | Latex | LatexEnv | Callout | WikilinkTarget | Anchor | Tag | FrontmatterKey | FrontmatterValue | Custom
    pub detail: Option<String>, // secondary text (e.g. target path for a heading) — VoiceOver reads it
    pub is_snippet: bool,       // replacement carries #/~/\n tab-stops (V0-4)
    pub sort_key: SortKey,      // (score, provider_rank, label) — engine fills score
}
```

**Tokenizer** (`context.rs`): from the caret, walk backward up to `max_look_back` UTF-16 units while the char is in `character_class`, yielding `token` + its range. Must be UTF-8/UTF-16 correct at diacritics and CJK (a CJK run is not a word char under the default class → empty token → no word completion, matching completr's `characterRegex`). Provide `tokenize_before(before: &str, cfg) -> (token, start_utf16)`.

**Ranking/merge** (`engine.rs`): run enabled providers in a fixed order; if any `blocks_all_other_providers` fires, keep only its items. Score each item's `label` against `token` with the ported fuzzy scorer (subsequence + word-boundary/consecutive/prefix bonuses; `nameMatchBonus` analog available to providers). Drop items shorter than `min_word_length` or below a score floor. **Stable sort by (−score, provider_rank, label)** — deterministic total order, no map iteration, no RNG (DoD §V-C, §8).

**Tests (PR 1):** unit — tokenizer boundaries (ASCII, `ö`, `é`, CJK, digits, punctuation); ranking order for a fixed provider set; `blocks_all_other_providers` short-circuit. Property (proptest) — ranking is invariant under input permutation of provider output; tokenizer never slices a multi-byte char. fmt/clippy clean; no macOS deps.

## V0-2 · Syntactic context classifier — where completion fires (#569) — PR 2

Add `pub fn completion_context_at(state: &DocBufferState, caret_utf16: u32) -> CompletionSite` (in `doc_buffer.rs` or a sibling `completion/site.rs` reading the snapshot), reusing the **already-maintained** incremental structure — no re-parse.

```rust
pub enum CompletionSite {
    FrontmatterKey,
    FrontmatterValue { key: String },
    WikilinkTarget,                  // caret inside [[ … | before a | or # ]]
    WikilinkAnchor { target: String }, // caret after [[Target# or [[Target^
    Tag,                             // caret in a #tag token
    LatexMath,                       // caret inside $…$ / $$…$$ (or a code block iff latexTriggerInCodeBlocks)
    CalloutHeader,                   // caret on a `> [!` line, before the ]
    BodyWord,                        // ordinary prose word
    Suppressed,                      // inline/fenced code (no completion) unless overridden by LatexMath
}
```

**Rules (normative):**
- Extend `EditorSpanKind` (`editor_spans.rs`) with `Math` (inline `$…$` + block `$$…$$`) and `Callout` (a `> [!type]` first line of a block quote). Cover both in `highlight_spans` and the ranged variant so the classifier and the editor highlighter agree. This is the only place math/callout syntax enters the editor structure (K did math *rendering*, not editor spans).
- Frontmatter key vs. value uses the cached `fm_end` boundary + the current line's `key:` split; caret before the first `:` on a line inside frontmatter ⇒ `FrontmatterKey`, after ⇒ `FrontmatterValue`.
- `WikilinkTarget`/`Anchor` derive from `extract_links`/`ParsedLink` spans containing the caret (an open, unterminated `[[` is still classified — the parser must tolerate the in-progress link).
- Code spans ⇒ `Suppressed` **unless** the caret is in a `Math` span and the config allows LaTeX in code.
- The function is **total**: every caret offset in every buffer returns a variant; it never panics (DoD §V-D).

**Tests:** unit fixtures placing the caret in each site kind (incl. open `[[`, `[[T#`, `> [!`, `$x`, YAML key vs value, fenced code). Property: `completion_context_at` returns without panic for every offset `0..=len_utf16` over random buffers; classification is stable under vault-file insertion-order permutation.

## V0-3 · Word providers — vault index, current-file scan, word-list, blacklist (#570) — PR 3

Fires in `BodyWord`. Four sources behind one `WordProvider`:

1. **Incremental vault word index** — `crates/slate-core/src/completion/word_index.rs`: a prefix + frequency structure (word → occurrence count across the vault). Maintained on the DocumentBuffer edit path: on `apply_edit`, diff the affected line's word multiset and update counts (O(edit)). **Laziness (locked decision 9):** the index is `Option`, built lazily on first completion query from the FTS/word source; while `None`, the edit hook is a no-op — cold sessions and the read path pay zero. Provide `deep_equals(&other)` + a `rebuild()` for the V0-6 census.
2. **Current-file scanner** — words from the active buffer (`fileScannerScanCurrent`), always available even before the vault index warms.
3. **Word-list / custom dictionary loader** — read external files under `.slate/` (one word per line; "performant with big lists" ⇒ load into the same prefix structure, deduped).
4. **Blacklist filter** — drop any item whose label is in the blacklist set (loaded from `.slate/…blacklist`, V2-2).

`WordInsertionMode` semantics: `MatchCaseReplace`/`IgnoreCaseReplace` replace the token; `IgnoreCaseAppend` appends the completion tail preserving the typed prefix's case.

**Tests:** unit — insertion-mode variants, blacklist exclusion, word-list load. Property — index `deep_equals` a full `rebuild()` after a random edit sequence; big-list (≥100k words) query latency sanity. The full mutation census is V0-6.

## V0-4 · Static-table providers — LaTeX/MathJax + callouts (#571) — PR 4

Data-driven, no session:

- **LaTeX/MathJax** (`completion/latex.rs` + a checked-in `assets/latex_commands.json`, completr's shape): fires in `LatexMath`. Prefix-match MathJax command names; `\begin{` offers environments; entries may be **snippets** (`#` tab-stop, `~` final cursor, `\n` newline) → `CompletionItem { is_snippet: true, replacement: "<template>" }` for the UI (V1-3) to expand. Honor `latexMinWordTriggerLength` (2), `latexIgnoreCase`, `latexTriggerInCodeBlocks`.
- **Callouts** (`completion/callout.rs`): fires in `CalloutHeader`. A built-in callout-type table (note, abstract, info, tip, success, question, warning, failure, danger, bug, example, quote, …); selecting inserts the `[!type]` (replace range covers the partial type). completr's "Callout Manager" source is out of scope (no such plugin in Slate); the built-in table is the only source.

Tables are **data**: ship as JSON assets, parsed at load; a user-override path is reserved for V-next. No `blocks_all_other_providers` (a `\` token in math shouldn't suppress word completion elsewhere — but in `LatexMath` the word provider doesn't fire anyway, per V0-2).

**Tests:** golden — table loads to the expected item set; snippet templates parse to the right tab-stop offsets; `\begin{` environment list; ignore-case + min-trigger gating.

## V0-5 · Slate-native providers — `[[` wikilinks, headings/blocks, `#tags`, front-matter (#572) — PR 5

The parity-plus set (program locked decision 2). Each is context-gated by V0-2:

- **Wikilink target** (`WikilinkTarget`): query `VaultIndex::all_paths`, fuzzy-rank with the filename bonus + recency tie-break (QuickSwitcherModel parity). `replacement` = the vault-relative target (or basename per link style); `detail` = full path for VoiceOver. Unresolved-but-referenced targets may be offered too (ghost parity with the links table).
- **Heading / `^block` anchor** (`WikilinkAnchor { target }`): resolve `target`, enumerate its headings (reuse the outline/heading extraction) and block ids; complete after the `#`/`^`. `detail` = heading level / surrounding text.
- **`#tag`** (`Tag`): a vault tag index (distinct tags + counts) — build from the same scan that feeds properties/links; fuzzy-rank by prefix then frequency.
- **Front matter** (`FrontmatterKey` / `FrontmatterValue{key}`): keys learned from `properties_db` across the vault + the current note's own keys (`frontmatter.rs`); values learned per key (completr "any key with any value"); `frontMatterIgnoreCase`, tag-suffix append for tag-typed keys. Reads the property model — does **not** re-parse YAML.

All deterministic (sorted, permutation-invariant) and filter-aware. These providers need read access to the session/vault index; expose them behind the same `CompletionProvider` trait with an injected read handle (keep the trait pure by passing the needed slices/iterators in `CompletionContext` extensions or a provider-construction handle — do not smuggle a `Connection` into the pure engine).

**Tests:** unit fixtures — target ranking (filename bonus, recency), anchor enumeration for a resolved target, tag ranking, frontmatter key/value learning + ignore-case. Property — deterministic ordering under vault insertion-order permutation.

## V0-6 · Censuses + benchmarks — the wave gate (#573) — PR 6

**Censuses** (`crates/slate-core/src/session/tests/completion.rs`, `census_*`, `census_scale()`):

1. `census_completion_index_matches_rebuild` — adversarial random walk: N ops from {create note w/ random words+links+tags, edit body, add/remove frontmatter key, rename, delete}; after **every** op, the incremental `word_index` (and tag index) `deep_equals` a fresh `rebuild()`. Random + an exhaustive small-vault sweep (every op pair over a 4-file vault) per the adversarial-census methodology.
2. `census_completion_determinism` — same buffer + config ⇒ identical ranked `CompletionItem` list across two independent engine runs; permutation of vault-file insertion order ⇒ identical word/tag index and (up to tie-break) identical ranking.
3. `census_context_classification_total` — `completion_context_at` returns without panic for every offset over random buffers; classification stable under permutation.

**Benchmarks** (`crates/slate-core/benches/completion_bench.rs`, reusing `benches/common`): `completion_query/{1k,10k,50k}` (full pipeline: classify → providers → rank; budget < 16 ms at 10k, DoD §V-E), `word_index_incremental/{10k}` (one line's words replaced; budget O(edit), < 1 ms), `word_index_build/{10k,50k}` (lazy first-query cost). Record baselines in `BENCHMARKS.md`; re-run `scan_initial` and the save-path benches to prove the edit hook is **free** when the index is unbuilt and O(edit) when built (DoD §V-E).

**Wave-1 exit:** all three censuses clean (incl. one `SLATE_CENSUS_FULL=1` release run), baselines recorded, no scan/save regression.
