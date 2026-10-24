// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! #1576: the token classes and semantic spans slate-core derives for
//! MALFORMED code, pinned.
//!
//! Well-formed code is pinned elsewhere (the per-grammar facts in
//! `src/code.rs` and the cross-host parity golden). For broken code the
//! classes come from each tree-sitter grammar's error recovery, which a
//! tree-sitter or grammar bump can change silently: tree-sitter 0.27's new
//! error recovery reads some broken C as an `identifier` where 0.26 read a
//! `type_identifier` (a screen-reader user hears "type" or not), and turns
//! some JS/TS shorthand patterns into `shorthand_property_identifier`, which
//! the classifiers map to the same class, so that one is inaudible. Every
//! snippet in `fixtures/highlight_golden/malformed.txt` (at least one per
//! language the highlighter knows, plus inputs whose class moved between
//! tree-sitter 0.26 and 0.27) must highlight exactly as `expected.txt`
//! says, and a bump that changes any of them fails here, naming them.
//!
//! Regenerate on purpose, then review the diff:
//! `SLATE_HIGHLIGHT_GOLDEN_UPDATE=1 cargo test -p slate-core --test highlight_golden`

use std::collections::BTreeMap;
use std::fmt::Write as _;
use std::path::{Path, PathBuf};

use slate_core::code::{CodeBlock, RawCodeBlock, highlight_code, highlighted_language_tags};

const UPDATE: &str = "SLATE_HIGHLIGHT_GOLDEN_UPDATE";

fn fixture_dir() -> PathBuf {
    Path::new(env!("CARGO_MANIFEST_DIR"))
        .join("tests")
        .join("fixtures")
        .join("highlight_golden")
}

/// Line endings are the fixtures' format, not their content: a CRLF copy
/// from an editor reads the same.
fn read_fixture(name: &str) -> Option<String> {
    std::fs::read_to_string(fixture_dir().join(name))
        .ok()
        .map(|text| text.replace("\r\n", "\n"))
}

/// One snippet: its `=== <tag> #<n>` header and its source.
struct Snippet {
    header: String,
    tag: String,
    source: String,
}

/// `malformed.txt` is blocks of a `=== <tag> #<n>` header line followed by
/// the snippet's lines, up to the next header. A snippet's trailing newlines
/// are not part of it; nothing may precede the first header.
fn snippets() -> Vec<Snippet> {
    let text = read_fixture("malformed.txt").expect("read fixtures/highlight_golden/malformed.txt");
    let mut snippets: Vec<Snippet> = Vec::new();
    let mut header: Option<String> = None;
    let mut lines: Vec<&str> = Vec::new();
    let mut finish = |header: Option<String>, lines: &mut Vec<&str>| {
        match header {
            Some(header) => {
                let tag = header
                    .trim_start_matches("=== ")
                    .split(' ')
                    .next()
                    .unwrap_or_default()
                    .to_string();
                let source = lines.join("\n").trim_end_matches('\n').to_string();
                assert!(!source.is_empty(), "{header} has no source");
                snippets.push(Snippet {
                    header,
                    tag,
                    source,
                });
            }
            None => assert!(
                lines.iter().all(|line| line.trim().is_empty()),
                "malformed.txt has text before its first `=== ` header"
            ),
        }
        lines.clear();
    };
    for line in text.lines() {
        if line.starts_with("=== ") {
            finish(header.take(), &mut lines);
            header = Some(line.to_string());
        } else {
            lines.push(line);
        }
    }
    finish(header, &mut lines);
    snippets
}

/// The snippet as a fenced block's body, which always ends in a newline
/// (`extract_code_blocks`).
fn highlight(source: &str, tag: &str) -> CodeBlock {
    highlight_code(&RawCodeBlock {
        source: format!("{source}\n"),
        language: Some(tag.to_string()),
        line: 1,
        byte_offset: 0,
    })
}

/// A value on one golden line: newlines and carriage returns made visible.
fn one_line(value: &str) -> String {
    value.replace('\r', "␍").replace('\n', "⏎")
}

/// The golden block for one snippet: its header, its source, then one line
/// per token (`token <kind> <start>..<end> <text>`) and per semantic span.
fn render(snippet: &Snippet) -> String {
    let block = highlight(&snippet.source, &snippet.tag);
    let text = |start: u32, end: u32| {
        block
            .source
            .get(start as usize..end as usize)
            .map_or_else(|| "<not on a char boundary>".to_string(), one_line)
    };
    let mut out = String::new();
    writeln!(out, "{}", snippet.header).expect("write to a String");
    writeln!(out, "source: {}", one_line(&block.source)).expect("write to a String");
    for token in &block.tokens {
        writeln!(
            out,
            "token {:?} {}..{} {}",
            token.kind,
            token.start_byte,
            token.end_byte,
            text(token.start_byte, token.end_byte)
        )
        .expect("write to a String");
    }
    for span in &block.semantic_spans {
        writeln!(
            out,
            "semantic {:?} {}..{} {}",
            span.kind,
            span.start_byte,
            span.end_byte,
            one_line(&span.name)
        )
        .expect("write to a String");
    }
    out
}

fn render_corpus(snippets: &[Snippet]) -> String {
    snippets.iter().map(render).collect::<Vec<_>>().join("\n")
}

/// A rendered golden, keyed by its blocks' headers.
fn blocks_by_header(golden: &str) -> BTreeMap<String, String> {
    golden
        .split("\n=== ")
        .map(|block| {
            let block = if block.starts_with("=== ") {
                block.to_string()
            } else {
                format!("=== {block}")
            };
            let header = block.lines().next().unwrap_or_default().to_string();
            (header, block.trim_end().to_string())
        })
        .collect()
}

#[test]
fn the_malformed_corpus_covers_every_highlighted_language() {
    let snippets = snippets();
    let languages: Vec<&str> = highlighted_language_tags().map(|tags| tags[0]).collect();
    for language in &languages {
        assert!(
            snippets.iter().any(|snippet| snippet.tag == *language),
            "no malformed snippet for {language}"
        );
    }
    for snippet in &snippets {
        assert!(
            languages.contains(&snippet.tag.as_str()),
            "{} must name a language by its own tag, one of {languages:?}",
            snippet.header
        );
    }
    let mut headers: Vec<&str> = snippets.iter().map(|s| s.header.as_str()).collect();
    headers.sort_unstable();
    headers.dedup();
    assert_eq!(headers.len(), snippets.len(), "two snippets share a header");
}

/// An alias (`rs`, `ts`, `c++`, …) reaches the grammar through one list and
/// the classifier through another; both must agree, so a block tagged with
/// an alias reads exactly like one tagged with the language's own name.
#[test]
fn every_alias_highlights_like_its_language() {
    let snippets = snippets();
    for tags in highlighted_language_tags() {
        let snippet = snippets
            .iter()
            .find(|snippet| snippet.tag == tags[0])
            .expect("the corpus covers every language");
        let own = highlight(&snippet.source, tags[0]);
        for alias in &tags[1..] {
            let aliased = highlight(&snippet.source, alias);
            assert_eq!(
                (aliased.tokens, aliased.semantic_spans),
                (own.tokens.clone(), own.semantic_spans.clone()),
                "{} tagged `{alias}` does not highlight like `{}`",
                snippet.header,
                tags[0]
            );
        }
    }
}

#[test]
fn malformed_code_tokens_match_the_golden() {
    let snippets = snippets();
    let actual = render_corpus(&snippets);
    let path = fixture_dir().join("expected.txt");
    if std::env::var(UPDATE).as_deref() == Ok("1") {
        std::fs::write(&path, &actual).expect("write the highlight golden");
        return;
    }
    let Some(expected) = read_fixture("expected.txt") else {
        panic!(
            "cannot read {}\nGenerate the golden with\n  {UPDATE}=1 cargo test -p slate-core \
             --test highlight_golden\nand review it: it is how malformed code is classified for \
             screen-reader users.",
            path.display()
        );
    };
    if expected != actual {
        let expected = blocks_by_header(&expected);
        let actual = blocks_by_header(&actual);
        let mut report = String::new();
        for (header, block) in &actual {
            match expected.get(header) {
                Some(pinned) if pinned == block => {}
                Some(_) => writeln!(report, "changed: {header}").expect("write to a String"),
                None => writeln!(report, "not in the golden: {header}").expect("write to a String"),
            }
        }
        for header in expected
            .keys()
            .filter(|header| !actual.contains_key(*header))
        {
            writeln!(report, "only in the golden: {header}").expect("write to a String");
        }
        panic!(
            "{report}\nThe highlighting of malformed code changed (above). If the change is \
             intended, regenerate the golden with\n  {UPDATE}=1 cargo test -p slate-core --test \
             highlight_golden\nand say in the PR what changes for screen-reader users."
        );
    }
}
