// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Census for `reading::reading_blocks_source` (U3-1, #465).
//!
//! `census_reading_blocks_cover_body_exactly` generates 100k random
//! Markdown documents and asserts the segmentation invariants the reading
//! view relies on:
//!
//! 1. **Document order** — `byte_start` is non-decreasing.
//! 2. **Non-overlapping** — each block's `byte_end <= the next block's
//!    `byte_start`.
//! 3. **Covers every non-blank byte of the body** — the union of block
//!    ranges (mapped back into the frontmatter-stripped body) contains
//!    every non-whitespace byte; only blank runs may be gaps.
//! 4. **Slice fidelity** — `full_source[byte_start..byte_end] == source`
//!    for every block.
//!
//! Deterministic + replayable: each document is built from a
//! `SplitMix64(seed)`, so a failure prints the seed and reproduces
//! byte-for-byte. The generator draws from a block palette covering every
//! specialized + generic kind, nesting (lists/quotes), task items with
//! every status char the vault supports, adjacent blocks with no blank
//! line, HTML blocks, code fences with `---` inside, and unicode — the
//! fixture families the spec pins.
//!
//! The census lives under `session::tests` (the project's census home)
//! but exercises the pure free function directly — no vault/session/IO.

use crate::reading::{ReadingBlock, reading_blocks_source};

// =========================================================================
// Deterministic PRNG (splitmix64) — self-contained, matching the pattern in
// `dir_tree.rs` so every failure replays from its seed with no `rand` dep.
// =========================================================================

struct SplitMix64(u64);

impl SplitMix64 {
    fn new(seed: u64) -> Self {
        Self(seed)
    }
    fn next_u64(&mut self) -> u64 {
        self.0 = self.0.wrapping_add(0x9E37_79B9_7F4A_7C15);
        let mut z = self.0;
        z = (z ^ (z >> 30)).wrapping_mul(0xBF58_476D_1CE4_E5B9);
        z = (z ^ (z >> 27)).wrapping_mul(0x94D0_49BB_1331_11EB);
        z ^ (z >> 31)
    }
    fn below(&mut self, n: usize) -> usize {
        (self.next_u64() % n as u64) as usize
    }
    fn chance(&mut self, numerator: u32, denominator: u32) -> bool {
        (self.next_u64() % denominator as u64) < numerator as u64
    }
}

/// Inline fragments a paragraph / heading / list item / quote line is
/// built from — includes unicode (NFC + NFD é), an emoji, wiki/embed/tag/
/// citation syntax (the reading view's inline pipeline territory), and a
/// bare `$` (price, not math) to keep the math classifier honest.
const INLINE_ATOMS: &[&str] = &[
    "alpha",
    "Beta word",
    "\u{00e9}dition", // é precomposed
    "cafe\u{0301}",   // é decomposed (same glyph)
    "emoji 🎉 tail",
    "[[Wikilink]]",
    "[[Target|alias]]",
    "![[embed.png]]",
    "#tag",
    "[@citekey]",
    "a $ price 50",
    "trailing spaces   ",
    "mixed ED 見出し",
];

/// One block the generator can emit. Each renders to a chunk of Markdown;
/// the generator joins chunks with either a blank line or (sometimes) a
/// bare newline so "adjacent blocks with no blank line" is exercised.
#[derive(Clone, Copy)]
enum BlockGen {
    AtxHeading,
    SetextHeading,
    Paragraph,
    UnorderedList,
    OrderedList,
    TaskList,
    NestedList,
    BlockQuote,
    NestedQuote,
    QuoteWithList,
    CodeFence,
    CodeFenceWithDashes,
    MermaidFence,
    IndentedCode,
    DisplayMath,
    Table,
    HtmlBlock,
    ThematicBreak,
}

const BLOCK_PALETTE: &[BlockGen] = &[
    BlockGen::AtxHeading,
    BlockGen::SetextHeading,
    BlockGen::Paragraph,
    BlockGen::Paragraph, // weight paragraphs up a little
    BlockGen::UnorderedList,
    BlockGen::OrderedList,
    BlockGen::TaskList,
    BlockGen::NestedList,
    BlockGen::BlockQuote,
    BlockGen::NestedQuote,
    BlockGen::QuoteWithList,
    BlockGen::CodeFence,
    BlockGen::CodeFenceWithDashes,
    BlockGen::MermaidFence,
    BlockGen::IndentedCode,
    BlockGen::DisplayMath,
    BlockGen::Table,
    BlockGen::HtmlBlock,
    BlockGen::ThematicBreak,
];

/// Task status chars the vault supports (tasks.rs stores the raw char;
/// only `x`/`X` are "completed"). The generator uses the full set so the
/// census exercises every status char, not just pulldown's `[ ]`/`[x]`.
const STATUS_CHARS: &[char] = &[' ', 'x', 'X', '/', '-', '>', '?', '!'];

fn inline(rng: &mut SplitMix64) -> &'static str {
    INLINE_ATOMS[rng.below(INLINE_ATOMS.len())]
}

fn render_block(g: BlockGen, rng: &mut SplitMix64) -> String {
    match g {
        BlockGen::AtxHeading => {
            let level = 1 + rng.below(6);
            format!("{} {}", "#".repeat(level), inline(rng))
        }
        BlockGen::SetextHeading => {
            let underline = if rng.chance(1, 2) { "===" } else { "---" };
            format!("{}\n{}", inline(rng), underline)
        }
        BlockGen::Paragraph => {
            let lines = 1 + rng.below(3);
            (0..lines)
                .map(|_| inline(rng))
                .collect::<Vec<_>>()
                .join("\n")
        }
        BlockGen::UnorderedList => {
            let bullet = ['-', '*', '+'][rng.below(3)];
            let n = 1 + rng.below(4);
            (0..n)
                .map(|_| format!("{bullet} {}", inline(rng)))
                .collect::<Vec<_>>()
                .join("\n")
        }
        BlockGen::OrderedList => {
            let n = 1 + rng.below(4);
            (0..n)
                .map(|i| format!("{}. {}", i + 1, inline(rng)))
                .collect::<Vec<_>>()
                .join("\n")
        }
        BlockGen::TaskList => {
            let n = 1 + rng.below(4);
            (0..n)
                .map(|_| {
                    let c = STATUS_CHARS[rng.below(STATUS_CHARS.len())];
                    format!("- [{c}] {}", inline(rng))
                })
                .collect::<Vec<_>>()
                .join("\n")
        }
        BlockGen::NestedList => {
            // Parent items with indented children (2 spaces per level).
            format!(
                "- {}\n  - {}\n    - {}\n- {}",
                inline(rng),
                inline(rng),
                inline(rng),
                inline(rng)
            )
        }
        BlockGen::BlockQuote => {
            let n = 1 + rng.below(3);
            (0..n)
                .map(|_| format!("> {}", inline(rng)))
                .collect::<Vec<_>>()
                .join("\n")
        }
        BlockGen::NestedQuote => {
            format!(
                "> {}\n> > {}\n> > > {}",
                inline(rng),
                inline(rng),
                inline(rng)
            )
        }
        BlockGen::QuoteWithList => {
            format!(
                "> {}\n>\n> - {}\n> - {}\n>\n> {}",
                inline(rng),
                inline(rng),
                inline(rng),
                inline(rng)
            )
        }
        BlockGen::CodeFence => {
            let lang = ["rust", "python", "js", "", "text", "toml"][rng.below(6)];
            format!("```{lang}\nlet x = 1;\nsome code {}\n```", inline(rng))
        }
        BlockGen::CodeFenceWithDashes => {
            // A `---` inside a fence must NOT break the block / become a
            // setext underline / thematic break.
            "```\n---\nnot a break\n---\ncontent\n```".to_string()
        }
        BlockGen::MermaidFence => "```mermaid\nflowchart LR\nA --> B\n```".to_string(),
        BlockGen::IndentedCode => {
            format!("    indented code line\n    second {}", inline(rng))
        }
        BlockGen::DisplayMath => {
            if rng.chance(1, 2) {
                "$$\n\\sum_{i=0}^n i^2\n$$".to_string()
            } else {
                "$$x^2 + y^2 = z^2$$".to_string()
            }
        }
        BlockGen::Table => {
            format!(
                "| {} | {} |\n|---|---|\n| 1 | 2 |\n| 3 | 4 |",
                inline(rng),
                inline(rng)
            )
        }
        BlockGen::HtmlBlock => {
            let which = rng.below(3);
            match which {
                0 => format!("<div>\n{}\n</div>", inline(rng)),
                1 => format!(
                    "<details>\n<summary>{}</summary>\nbody\n</details>",
                    inline(rng)
                ),
                _ => format!("<p>{}</p>", inline(rng)),
            }
        }
        BlockGen::ThematicBreak => ["---", "***", "___"][rng.below(3)].to_string(),
    }
}

/// Build one random Markdown document: optional frontmatter, then a run of
/// blocks joined by a blank line (usually) or a single newline (sometimes,
/// to exercise adjacency), with occasional leading/trailing blank lines.
fn generate_document(rng: &mut SplitMix64) -> String {
    let mut doc = String::new();

    // Optional frontmatter — the block walk must skip it but rebase offsets.
    if rng.chance(1, 3) {
        let crlf = rng.chance(1, 4);
        let nl = if crlf { "\r\n" } else { "\n" };
        doc.push_str("---");
        doc.push_str(nl);
        doc.push_str("title: ");
        doc.push_str(inline(rng));
        doc.push_str(nl);
        if rng.chance(1, 2) {
            doc.push_str("tags: [a, b]");
            doc.push_str(nl);
        }
        doc.push_str("---");
        doc.push_str(nl);
    }

    // Occasional leading blank lines (a legal gap before the first block).
    if rng.chance(1, 5) {
        doc.push('\n');
    }

    let block_count = rng.below(8); // 0..=7 blocks (0 exercises the empty body)
    for i in 0..block_count {
        if i > 0 {
            // Usually a blank-line separator; sometimes a single newline so
            // two blocks are adjacent with no blank line between them.
            if rng.chance(3, 4) {
                doc.push_str("\n\n");
            } else {
                doc.push('\n');
            }
        }
        let g = BLOCK_PALETTE[rng.below(BLOCK_PALETTE.len())];
        doc.push_str(&render_block(g, rng));
    }

    // Occasional trailing whitespace / blank lines.
    match rng.below(4) {
        0 => doc.push('\n'),
        1 => doc.push_str("\n\n"),
        2 => doc.push_str("   \n"),
        _ => {}
    }

    doc
}

/// Assert the four invariants on one document; `seed` is threaded through
/// for a replayable failure message.
fn assert_invariants(seed: u64, source: &str) {
    let body = crate::frontmatter::body_after_frontmatter(source);
    let fm_offset = source.len() - body.len();
    let blocks: Vec<ReadingBlock> = reading_blocks_source(source);

    // (1) + (2) order + non-overlap, and (4) slice fidelity.
    let mut prev_end: u64 = 0;
    for (i, b) in blocks.iter().enumerate() {
        assert!(
            b.byte_start <= b.byte_end,
            "seed {seed}: block {i} reversed range {}..{}\nsource: {source:?}",
            b.byte_start,
            b.byte_end
        );
        assert!(
            (b.byte_end as usize) <= source.len(),
            "seed {seed}: block {i} end {} past source len {}\nsource: {source:?}",
            b.byte_end,
            source.len()
        );
        if i > 0 {
            assert!(
                b.byte_start >= prev_end,
                "seed {seed}: block {i} overlaps previous (start {} < prev_end {prev_end})\nsource: {source:?}",
                b.byte_start
            );
        }
        // Ordering: starts non-decreasing (implied by the above, but pin it).
        // (4) slice fidelity.
        assert_eq!(
            &source[b.byte_start as usize..b.byte_end as usize],
            b.source,
            "seed {seed}: block {i} slice mismatch\nsource: {source:?}"
        );
        prev_end = b.byte_end;
    }

    // (3) every non-blank byte of the BODY is covered by some block.
    // Blocks never start before the body (offsets are rebased by fm_offset),
    // so map coverage into body coordinates and check each non-whitespace
    // body byte falls inside some block's [start,end).
    let mut covered = vec![false; body.len()];
    for b in &blocks {
        // Block offsets are whole-source; subtract fm_offset to index body.
        let start = (b.byte_start as usize).saturating_sub(fm_offset);
        let end = (b.byte_end as usize).saturating_sub(fm_offset);
        for c in covered.iter_mut().take(end.min(body.len())).skip(start) {
            *c = true;
        }
    }
    for (i, &byte) in body.as_bytes().iter().enumerate() {
        if !byte.is_ascii_whitespace() && !covered[i] {
            panic!(
                "seed {seed}: non-blank body byte {i} ({:?}) not covered by any block\n\
                 body: {body:?}\nblocks: {:#?}",
                byte as char,
                blocks
                    .iter()
                    .map(|b| (b.byte_start, b.byte_end, &b.kind))
                    .collect::<Vec<_>>()
            );
        }
    }
}

// =========================================================================
// The census.
// =========================================================================

#[test]
fn census_reading_blocks_cover_body_exactly() {
    // The release guarantee is 100k random documents; each asserts blocks
    // in document order, non-overlapping, union covers every non-blank
    // body byte (blank gaps allowed), and every block's `source` equals
    // its whole-source slice.
    //
    // Profile-scaled count (the codebase's convention for a census this
    // heavy — cf. the #404 buffer censuses that are "the release
    // guarantee"): the full 100k runs in release (`cargo test --release`
    // / CI's release census run, ~0.4 s); the debug `cargo test` gate runs
    // a 2,000-document smoke of the SAME deterministic seed prefix so the
    // per-PR suite stays within budget while still exercising the walk.
    // Seeds are `0..N`, so the debug subset is a strict prefix of the
    // release set — a debug failure reproduces identically in release.
    const DOCUMENTS: u64 = if cfg!(debug_assertions) {
        2_000
    } else {
        100_000
    };
    for seed in 0..DOCUMENTS {
        let mut rng = SplitMix64::new(seed);
        let doc = generate_document(&mut rng);
        assert_invariants(seed, &doc);
    }
}

/// A handful of hand-picked adversarial shapes checked with the same
/// invariant harness — the kinds of edge cases the random generator hits
/// only rarely, pinned explicitly so a regression names them directly.
#[test]
fn reading_blocks_edge_case_shapes_satisfy_invariants() {
    let cases = [
        "",
        "\n\n\n",
        "   ",
        "# only a heading",
        "---\ntitle: x\n---\n",               // frontmatter only, empty body
        "---\ntitle: x\n---\n# body",         // frontmatter + one block
        "\u{FEFF}---\nk: v\n---\n# bom body", // BOM + frontmatter
        "- a\n  - b\n    - c\n      - d\n",   // deep nesting
        "> > > deeply quoted\n",
        "```\n---\n```\n",              // fence with only a dashes line
        "$$\n$$\n",                     // empty-ish display math delimiters
        "text $x$ and $$block$$ mix\n", // inline + something dollar
        "a\nb\nc\n\n- x\n- y\n",        // adjacency then list
        "見出し\n=====\n\n本文\n",      // unicode setext
        "| a |\n|---|\n| 🎉 |\n",       // unicode table cell
    ];
    for (i, case) in cases.iter().enumerate() {
        assert_invariants(1_000_000 + i as u64, case);
    }
}

// =========================================================================
// Inline segments (#967 · specs/w3_inline_runs_spec.md)
// =========================================================================
//
// Three invariants the hosts' attributed-text mapping depends on, over the
// SAME deterministic generator (seeds `0..N`, so the debug subset is a
// strict prefix of the release set and a debug failure reproduces
// identically in release):
//
// 1. **Alignment** — `reading_inline_segments_source` is 1:1 and
//    same-order with `reading_blocks_source`; kinds with no inline
//    content carry zero segments, inline kinds carry exactly one.
// 2. **Partition** — each segment's runs are ascending, non-overlapping,
//    gapless, cover `0..content.len()`, and land on char boundaries.
//    That is what makes `concat(content[run]) == content` hold, which is
//    what lets a host stamp attributes run by run without re-slicing.
// 3. **Splice-equivalence** — a selected token's own text never re-parses
//    as Markdown: its run text equals the payload's display form exactly,
//    with no delimiter consumed.

use crate::reading::{ReadingBlockInlines, ReadingInlineRunKind, reading_inline_segments_source};

fn assert_inline_invariants(seed: u64, source: &str) {
    let blocks = reading_blocks_source(source);
    let inlines = reading_inline_segments_source(source, &[], &[]);

    assert_eq!(
        blocks.len(),
        inlines.len(),
        "seed {seed}: inline results must be 1:1 with blocks\nsource: {source:?}"
    );

    for (block, inline) in blocks.iter().zip(inlines.iter()) {
        let expects_segment = matches!(
            block.kind,
            crate::reading::ReadingBlockKind::Heading { .. }
                | crate::reading::ReadingBlockKind::Paragraph
                | crate::reading::ReadingBlockKind::ListItem { .. }
                | crate::reading::ReadingBlockKind::BlockQuote { .. }
        );
        assert_eq!(
            inline.segments.len(),
            usize::from(expects_segment),
            "seed {seed}: {:?} segment count\nsource: {source:?}",
            block.kind
        );

        // A block-embed key is only ever reported for a Paragraph.
        if inline.block_embed_key.is_some() {
            assert!(
                matches!(block.kind, crate::reading::ReadingBlockKind::Paragraph),
                "seed {seed}: block_embed_key on {:?}\nsource: {source:?}",
                block.kind
            );
        }
        // A list marker is only ever reported for a ListItem.
        if inline.list_marker.is_some() {
            assert!(
                matches!(
                    block.kind,
                    crate::reading::ReadingBlockKind::ListItem { .. }
                ),
                "seed {seed}: list_marker on {:?}\nsource: {source:?}",
                block.kind
            );
        }

        for segment in &inline.segments {
            let mut cursor = 0u32;
            for run in &segment.runs {
                assert_eq!(
                    run.start, cursor,
                    "seed {seed}: run gap/overlap in {:?}\nsource: {source:?}",
                    segment.content
                );
                assert!(
                    run.end > run.start,
                    "seed {seed}: empty run in {:?}\nsource: {source:?}",
                    segment.content
                );
                assert!(
                    segment.content.is_char_boundary(run.start as usize)
                        && segment.content.is_char_boundary(run.end as usize),
                    "seed {seed}: run splits a scalar in {:?}\nsource: {source:?}",
                    segment.content
                );
                let mut styles = run.styles.clone();
                styles.sort_unstable();
                styles.dedup();
                assert_eq!(
                    styles, run.styles,
                    "seed {seed}: styles must ship sorted + deduped\nsource: {source:?}"
                );
                cursor = run.end;
            }
            // Mask-leakage guard: the pipeline masks selected tokens with
            // runs of U+FFFC, so rendered content must never carry MORE of
            // that scalar than the author wrote. A scanner that mis-parsed
            // a mask would spill marker scalars here and drop the token's
            // affordance — exactly what an authored U+FFFC sitting against
            // a wikilink used to produce.
            let authored = block.source.matches('\u{FFFC}').count();
            let rendered = segment.content.matches('\u{FFFC}').count();
            assert!(
                rendered <= authored,
                "seed {seed}: rendered content carries {rendered} marker scalars \
                 from {authored} authored\ncontent: {:?}\nsource: {source:?}",
                segment.content
            );

            assert_eq!(
                cursor as usize,
                segment.content.len(),
                "seed {seed}: runs must cover every byte of {:?}\nsource: {source:?}",
                segment.content
            );
        }
    }
}

/// Splice-equivalence: a selected token's run text is exactly its payload
/// display form — the Markdown walk never consumed a delimiter out of it,
/// and no delimiter inside it paired with anything outside.
fn assert_token_text_never_reparses(seed: u64, source: &str) {
    for inline in reading_inline_segments_source(source, &[], &[]) {
        for segment in &inline.segments {
            for run in &segment.runs {
                let text = &segment.content[run.start as usize..run.end as usize];
                match &run.kind {
                    ReadingInlineRunKind::Tag { name } => assert_eq!(
                        text,
                        format!("#{name}"),
                        "seed {seed}: tag run text must be the hash plus name\nsource: {source:?}"
                    ),
                    ReadingInlineRunKind::Citation { raw, .. } => assert_eq!(
                        text, raw,
                        "seed {seed}: an unmatched citation renders its raw text\nsource: {source:?}"
                    ),
                    // Wikilink/Embed display text is alias-dependent, so the
                    // pinned invariant is non-emptiness: a token must never
                    // render as a zero-width, invisible affordance.
                    ReadingInlineRunKind::Wikilink { .. } | ReadingInlineRunKind::Embed { .. } => {
                        assert!(
                            !text.is_empty(),
                            "seed {seed}: token run must render non-empty text\nsource: {source:?}"
                        )
                    }
                    _ => {}
                }
            }
        }
    }
}

/// Determinism: the same inputs produce the same bytes, which is what
/// makes the §W-A `inline_runs` artifact meaningful.
fn assert_inline_determinism(seed: u64, source: &str) {
    let a: Vec<ReadingBlockInlines> = reading_inline_segments_source(source, &[], &[]);
    let b: Vec<ReadingBlockInlines> = reading_inline_segments_source(source, &[], &[]);
    assert_eq!(a, b, "seed {seed}: output is not deterministic");
}

#[test]
fn census_reading_inline_segments_align_and_partition() {
    const DOCUMENTS: u64 = if cfg!(debug_assertions) {
        2_000
    } else {
        100_000
    };
    for seed in 0..DOCUMENTS {
        let mut rng = SplitMix64::new(seed);
        let doc = generate_document(&mut rng);
        assert_inline_invariants(seed, &doc);
    }
}

#[test]
fn census_reading_inline_tokens_never_reparse() {
    const DOCUMENTS: u64 = if cfg!(debug_assertions) {
        2_000
    } else {
        100_000
    };
    for seed in 0..DOCUMENTS {
        let mut rng = SplitMix64::new(seed);
        let doc = generate_document(&mut rng);
        assert_token_text_never_reparses(seed, &doc);
        if seed < 256 {
            assert_inline_determinism(seed, &doc);
        }
    }
}

/// The same hand-picked adversarial shapes the block census pins, run
/// through the inline invariants — plus the inline-specific families the
/// random generator hits only rarely.
#[test]
fn reading_inline_edge_case_shapes_satisfy_invariants() {
    let cases = [
        "",
        "   ",
        "# only a heading",
        "- a\n  - b\n    - c\n",
        "> > > deeply quoted\n",
        "```\n[[not a link]]\n```\n",
        "`[[not a link]]` and `#nottag`\n",
        "[[ Missing ]]\n",
        "[[Missing #Anchor]]\n",
        "[[a|b|c]]\n",
        "[[note^draft#sec]]\n",
        "[[note#^blk]]\n",
        "![[Note#^blk]]\n",
        "  ![[ Note # Sec ]]  \n",
        "see ![[a]] and ![[b]] here\n",
        "**a [[b]]** *c [[d]]* ~~e [@f]~~\n",
        "[about #intro](note.md)\n",
        "[x](javascript:alert(1)) [y](file:///etc) [z](//host) [w](#frag)\n",
        "[t](my%20note.md) [u](C:\\notes\\x.md)\n",
        "[[a*b]] * not emphasis *\n",
        "line one\r\nline two\r\n",
        "a\r\nb\nc\r\n",
        "> a\r\n> b\r\n",
        "- first para\n\n  second para\n",
        "- [ ] open\n- [x] done\n- [/] doing\n- [v]x\n",
        "1. [v] Visible\n",
        "見出し\n=====\n\n本文 [[リンク]] 🎉\n",
        "| a |\n|---|\n| [[x]] |\n",
        "$$\n[[x]]\n$$\n",
    ];
    for (i, case) in cases.iter().enumerate() {
        let seed = 2_000_000 + i as u64;
        assert_inline_invariants(seed, case);
        assert_token_text_never_reparses(seed, case);
        assert_inline_determinism(seed, case);
    }
}
