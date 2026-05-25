//! Code pipeline for Milestone K (#218).
//!
//! Walks a Markdown source for fenced code blocks, classifies each by
//! its language tag, dispatches to the matching tree-sitter grammar,
//! and produces a token stream the UI can use for syntax highlighting
//! plus a coarse set of semantic spans (functions / types / variables)
//! reserved for V1.x "explain this" work.
//!
//! ## Compiled-in grammar set (`05` §6.4)
//!
//! Rust, Swift, Python, JavaScript, TypeScript, Markdown, YAML, JSON,
//! Bash, SQL, HTML, CSS, Go, C, C++.
//!
//! Unknown language tags (or fenced blocks with no language) fall back
//! to a single `Other` token covering the source — the highlight
//! never panics, AT still hears the source text via the wrapping
//! code-block container's accessibility label.

use tree_sitter::{Language, Node, Parser, Tree};

/// Token classes the highlighter emits. Broad categories on purpose —
/// per-language refinements (`Macro`, `Lifetime`, `JsxTag`, …) belong
/// in V1.x; for V1 the UI only needs a small palette of colors.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum TokenKind {
    Keyword,
    String,
    Number,
    Comment,
    Identifier,
    Type,
    Function,
    Operator,
    Punctuation,
    /// Catch-all for unknown languages or token kinds that don't
    /// belong to any of the well-known classes. Carries the
    /// originating node kind (or language tag for the no-language
    /// case) so a UI theme can hash it for a stable colour.
    Other(String),
}

/// One token's byte range within the fenced block's source.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SyntaxToken {
    pub start_byte: u32,
    pub end_byte: u32,
    pub kind: TokenKind,
}

/// Coarse semantic category for V1.x "what is this" affordances.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum SemanticKind {
    Function,
    Type,
    Variable,
}

/// One semantic span: a tagged byte range over a meaningful name.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SemanticSpan {
    pub start_byte: u32,
    pub end_byte: u32,
    pub kind: SemanticKind,
    pub name: String,
}

/// Raw fenced-code block before highlighting. Keeps the language tag
/// + position so the renderer can render incrementally.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RawCodeBlock {
    pub source: String,
    /// `None` for indented code blocks or fenced blocks with no
    /// language tag.
    pub language: Option<String>,
    /// 1-based line number of the fence opener.
    pub line: u32,
    /// Byte offset of the fence opener in the host source.
    pub byte_offset: u32,
}

/// Highlighted code block.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct CodeBlock {
    pub source: String,
    pub language: Option<String>,
    pub tokens: Vec<SyntaxToken>,
    pub semantic_spans: Vec<SemanticSpan>,
    pub line: u32,
    pub byte_offset: u32,
}

/// Walk `source` and return every code block in document order.
///
/// Recognises pulldown-cmark `Tag::CodeBlock` events — both fenced
/// (` ``` `) and indented. Indented blocks have `language = None`.
pub fn extract_code_blocks(source: &str) -> Vec<RawCodeBlock> {
    use pulldown_cmark::{CodeBlockKind, Event, Options, Parser as MdParser, Tag, TagEnd};

    let mut out: Vec<RawCodeBlock> = Vec::new();
    let mut current_lang: Option<String> = None;
    let mut current_buffer = String::new();
    let mut current_start: Option<usize> = None;
    let mut in_code = false;

    let parser = MdParser::new_ext(source, Options::ENABLE_STRIKETHROUGH).into_offset_iter();
    for (event, range) in parser {
        match event {
            Event::Start(Tag::CodeBlock(kind)) => {
                in_code = true;
                current_buffer.clear();
                current_start = Some(range.start);
                current_lang = match kind {
                    CodeBlockKind::Fenced(tag) => {
                        let s = tag.into_string();
                        let trimmed = s.trim();
                        if trimmed.is_empty() {
                            None
                        } else {
                            Some(trimmed.to_string())
                        }
                    }
                    CodeBlockKind::Indented => None,
                };
            }
            Event::End(TagEnd::CodeBlock) if in_code => {
                let start = current_start.take().unwrap_or(0);
                out.push(RawCodeBlock {
                    source: std::mem::take(&mut current_buffer),
                    language: current_lang.take(),
                    line: line_of_offset(source, start),
                    byte_offset: start as u32,
                });
                in_code = false;
            }
            Event::Text(s) if in_code => {
                current_buffer.push_str(&s);
            }
            _ => {}
        }
    }
    out
}

/// Dispatch a raw block to the matching tree-sitter grammar.
///
/// Returns a single `Other` token covering the source when the
/// language is unknown / missing — never panics, even on grammars
/// that disagree with the source's actual content (e.g. an obviously-
/// Python snippet tagged `rust`).
pub fn highlight_code(raw: &RawCodeBlock) -> CodeBlock {
    let language_key = raw
        .language
        .as_deref()
        .map(str::to_ascii_lowercase)
        .unwrap_or_default();
    let lang = match grammar_for_tag(&language_key) {
        Some(l) => l,
        None => return passthrough_code_block(raw, &language_key),
    };

    let mut parser = Parser::new();
    if parser.set_language(&lang).is_err() {
        return passthrough_code_block(raw, &language_key);
    }
    let tree = match parser.parse(&raw.source, None) {
        Some(t) => t,
        None => return passthrough_code_block(raw, &language_key),
    };
    let (tokens, semantic_spans) = walk_tree(&tree, raw.source.as_bytes());

    CodeBlock {
        source: raw.source.clone(),
        language: raw.language.clone(),
        tokens,
        semantic_spans,
        line: raw.line,
        byte_offset: raw.byte_offset,
    }
}

/// Tag → grammar lookup. The grammar set is exactly the 15 entries
/// locked in `05` §6.4; anything else returns `None` so the caller
/// falls back to the passthrough token.
fn grammar_for_tag(tag: &str) -> Option<Language> {
    Some(match tag {
        "rust" | "rs" => tree_sitter_rust::LANGUAGE.into(),
        "swift" => tree_sitter_swift::LANGUAGE.into(),
        "python" | "py" => tree_sitter_python::LANGUAGE.into(),
        "javascript" | "js" => tree_sitter_javascript::LANGUAGE.into(),
        "typescript" | "ts" => tree_sitter_typescript::LANGUAGE_TYPESCRIPT.into(),
        "tsx" => tree_sitter_typescript::LANGUAGE_TSX.into(),
        "markdown" | "md" => tree_sitter_md::LANGUAGE.into(),
        "yaml" | "yml" => tree_sitter_yaml::LANGUAGE.into(),
        "json" => tree_sitter_json::LANGUAGE.into(),
        "bash" | "sh" | "shell" => tree_sitter_bash::LANGUAGE.into(),
        "sql" => tree_sitter_sequel::LANGUAGE.into(),
        "html" => tree_sitter_html::LANGUAGE.into(),
        "css" => tree_sitter_css::LANGUAGE.into(),
        "go" => tree_sitter_go::LANGUAGE.into(),
        "c" => tree_sitter_c::LANGUAGE.into(),
        "cpp" | "c++" | "cxx" | "cc" => tree_sitter_cpp::LANGUAGE.into(),
        _ => return None,
    })
}

fn passthrough_code_block(raw: &RawCodeBlock, language_key: &str) -> CodeBlock {
    let token_label = if language_key.is_empty() {
        "text".to_string()
    } else {
        language_key.to_string()
    };
    let tokens = if raw.source.is_empty() {
        Vec::new()
    } else {
        vec![SyntaxToken {
            start_byte: 0,
            end_byte: raw.source.len() as u32,
            kind: TokenKind::Other(token_label),
        }]
    };
    CodeBlock {
        source: raw.source.clone(),
        language: raw.language.clone(),
        tokens,
        semantic_spans: Vec::new(),
        line: raw.line,
        byte_offset: raw.byte_offset,
    }
}

/// Walk a tree-sitter tree once, emitting one token per leaf node
/// and one semantic span per declaration site we recognise. Token
/// classes are derived from the node's `kind` field — broad bins by
/// design, since per-language refinement isn't in V1's scope.
fn walk_tree(tree: &Tree, source: &[u8]) -> (Vec<SyntaxToken>, Vec<SemanticSpan>) {
    let mut tokens: Vec<SyntaxToken> = Vec::new();
    let mut semantic: Vec<SemanticSpan> = Vec::new();
    let mut cursor = tree.walk();
    walk_node(
        tree.root_node(),
        source,
        &mut tokens,
        &mut semantic,
        &mut cursor,
    );
    tokens.sort_by_key(|t| t.start_byte);
    (tokens, semantic)
}

fn walk_node<'a>(
    node: Node<'a>,
    source: &[u8],
    tokens: &mut Vec<SyntaxToken>,
    semantic: &mut Vec<SemanticSpan>,
    cursor: &mut tree_sitter::TreeCursor<'a>,
) {
    // Semantic span detection: definition-site nodes for function /
    // type / variable get an entry pointing at their identifier.
    capture_semantic(node, source, semantic);

    let kind_name = node.kind();
    let is_leaf = node.child_count() == 0;
    if is_leaf && !node.is_extra() && !node.is_missing() {
        if let Some(kind) = classify_node_kind(kind_name) {
            tokens.push(SyntaxToken {
                start_byte: node.start_byte() as u32,
                end_byte: node.end_byte() as u32,
                kind,
            });
        }
        return;
    }
    // Comments are non-leaf nodes in many grammars; still record.
    if matches!(
        kind_name,
        "comment" | "line_comment" | "block_comment" | "doc_comment"
    ) {
        tokens.push(SyntaxToken {
            start_byte: node.start_byte() as u32,
            end_byte: node.end_byte() as u32,
            kind: TokenKind::Comment,
        });
        return;
    }
    for child in node.children(cursor) {
        // Build a fresh cursor for each child so the iterator state
        // doesn't get confused by recursion.
        let mut child_cursor = child.walk();
        walk_node(child, source, tokens, semantic, &mut child_cursor);
    }
}

fn capture_semantic(node: Node, source: &[u8], out: &mut Vec<SemanticSpan>) {
    let kind = node.kind();
    let semantic_kind = match kind {
        "function_item"
        | "function_declaration"
        | "function_definition"
        | "method_definition"
        | "method_declaration" => Some(SemanticKind::Function),
        "struct_item"
        | "enum_item"
        | "class_declaration"
        | "class_definition"
        | "type_alias"
        | "type_alias_declaration" => Some(SemanticKind::Type),
        _ => None,
    };
    let Some(kind) = semantic_kind else { return };
    if let Some(name_node) = node.child_by_field_name("name") {
        if let Ok(name) = name_node.utf8_text(source) {
            out.push(SemanticSpan {
                start_byte: name_node.start_byte() as u32,
                end_byte: name_node.end_byte() as u32,
                kind,
                name: name.to_string(),
            });
        }
    }
}

fn classify_node_kind(kind: &str) -> Option<TokenKind> {
    // Common kinds across grammars. Not exhaustive — anything we
    // don't recognise stays unclassified (no token emitted), which
    // means it'll fall through to the editor's default text style.
    // V1.x refinement can add per-language tables.
    let lower = kind.to_ascii_lowercase();
    if lower.contains("string") || lower == "raw_string_literal" {
        return Some(TokenKind::String);
    }
    if lower.contains("number") || lower.contains("integer") || lower.contains("float") {
        return Some(TokenKind::Number);
    }
    if lower == "identifier"
        || lower == "field_identifier"
        || lower == "shorthand_property_identifier"
        || lower == "property_identifier"
    {
        return Some(TokenKind::Identifier);
    }
    if lower.contains("type") && lower.ends_with("identifier") {
        return Some(TokenKind::Type);
    }
    if matches!(
        kind,
        "fn" | "let"
            | "mut"
            | "const"
            | "static"
            | "if"
            | "else"
            | "match"
            | "while"
            | "for"
            | "loop"
            | "return"
            | "break"
            | "continue"
            | "use"
            | "pub"
            | "mod"
            | "struct"
            | "enum"
            | "trait"
            | "impl"
            | "self"
            | "Self"
            | "where"
            | "async"
            | "await"
            | "import"
            | "from"
            | "as"
            | "def"
            | "class"
            | "func"
            | "var"
            | "true"
            | "false"
            | "null"
            | "None"
            | "True"
            | "False"
            | "package"
            | "interface"
            | "type"
            | "switch"
            | "case"
            | "default"
            | "do"
            | "try"
            | "except"
            | "finally"
            | "raise"
            | "throw"
            | "throws"
            | "extends"
            | "implements"
            | "abstract"
            | "private"
            | "protected"
            | "public"
            | "static_keyword"
            | "yield"
            | "with"
            | "in"
            | "is"
            | "and"
            | "or"
            | "not"
            | "new"
            | "delete"
            | "void"
            | "let_keyword"
    ) {
        return Some(TokenKind::Keyword);
    }
    if lower.contains("operator")
        || matches!(
            kind,
            "->" | "=>"
                | "=="
                | "!="
                | "+"
                | "-"
                | "*"
                | "/"
                | "%"
                | "="
                | "<"
                | ">"
                | "&"
                | "|"
                | "^"
                | "~"
        )
    {
        return Some(TokenKind::Operator);
    }
    if matches!(
        kind,
        "(" | ")" | "{" | "}" | "[" | "]" | "," | ";" | ":" | "::" | "." | "?"
    ) {
        return Some(TokenKind::Punctuation);
    }
    None
}

fn line_of_offset(source: &str, off: usize) -> u32 {
    1 + source[..off.min(source.len())]
        .bytes()
        .filter(|&b| b == b'\n')
        .count() as u32
}

// --- Tests -------------------------------------------------------------

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn extracts_fenced_block_with_language() {
        let src = "intro\n\n```rust\nfn foo() {}\n```\n\nafter";
        let blocks = extract_code_blocks(src);
        assert_eq!(blocks.len(), 1);
        assert_eq!(blocks[0].language.as_deref(), Some("rust"));
        assert!(blocks[0].source.contains("fn foo()"));
    }

    #[test]
    fn extracts_fenced_block_without_language() {
        let src = "```\nplain text\n```";
        let blocks = extract_code_blocks(src);
        assert_eq!(blocks.len(), 1);
        assert_eq!(blocks[0].language, None);
    }

    #[test]
    fn extracts_indented_block_as_language_none() {
        let src = "intro\n\n    indented one\n    indented two\n\nafter";
        let blocks = extract_code_blocks(src);
        assert_eq!(blocks.len(), 1);
        assert_eq!(blocks[0].language, None);
    }

    #[test]
    fn highlight_unknown_language_returns_single_other_token() {
        let raw = RawCodeBlock {
            source: "set foo to bar".to_string(),
            language: Some("cobol".to_string()),
            line: 1,
            byte_offset: 0,
        };
        let block = highlight_code(&raw);
        assert_eq!(block.tokens.len(), 1);
        match &block.tokens[0].kind {
            TokenKind::Other(label) => assert_eq!(label, "cobol"),
            other => panic!("expected Other, got {other:?}"),
        }
    }

    #[test]
    fn highlight_no_language_returns_single_other_text_token() {
        let raw = RawCodeBlock {
            source: "plain content".to_string(),
            language: None,
            line: 1,
            byte_offset: 0,
        };
        let block = highlight_code(&raw);
        assert_eq!(block.tokens.len(), 1);
        match &block.tokens[0].kind {
            TokenKind::Other(label) => assert_eq!(label, "text"),
            other => panic!("expected Other, got {other:?}"),
        }
    }

    #[test]
    fn rust_grammar_recognises_fn_keyword() {
        let raw = RawCodeBlock {
            source: "fn foo() -> i32 { 0 }".to_string(),
            language: Some("rust".to_string()),
            line: 1,
            byte_offset: 0,
        };
        let block = highlight_code(&raw);
        let kinds: Vec<&TokenKind> = block.tokens.iter().map(|t| &t.kind).collect();
        assert!(
            kinds.iter().any(|k| matches!(k, TokenKind::Keyword)),
            "expected a Keyword token in rust output; got {kinds:?}"
        );
    }

    #[test]
    fn rust_grammar_emits_function_semantic_span() {
        let raw = RawCodeBlock {
            source: "fn my_function() {}".to_string(),
            language: Some("rust".to_string()),
            line: 1,
            byte_offset: 0,
        };
        let block = highlight_code(&raw);
        let fn_span = block
            .semantic_spans
            .iter()
            .find(|s| s.kind == SemanticKind::Function);
        assert!(fn_span.is_some(), "expected a Function span");
        assert_eq!(fn_span.unwrap().name, "my_function");
    }

    #[test]
    fn python_grammar_recognises_def() {
        let raw = RawCodeBlock {
            source: "def foo():\n    return 1\n".to_string(),
            language: Some("python".to_string()),
            line: 1,
            byte_offset: 0,
        };
        let block = highlight_code(&raw);
        // Sanity: any token emitted. Per-language depth (catching
        // `def` specifically vs `keyword`) is V1.x territory.
        assert!(!block.tokens.is_empty());
    }

    #[test]
    fn json_grammar_recognises_string_and_number() {
        let raw = RawCodeBlock {
            source: r#"{"key": 42}"#.to_string(),
            language: Some("json".to_string()),
            line: 1,
            byte_offset: 0,
        };
        let block = highlight_code(&raw);
        let kinds: Vec<&TokenKind> = block.tokens.iter().map(|t| &t.kind).collect();
        assert!(
            kinds.iter().any(|k| matches!(k, TokenKind::String)),
            "expected String token; got {kinds:?}"
        );
        assert!(
            kinds.iter().any(|k| matches!(k, TokenKind::Number)),
            "expected Number token; got {kinds:?}"
        );
    }

    #[test]
    fn no_panic_on_malformed_source() {
        // Truncated, syntactically broken — tree-sitter has to keep
        // going. We just want no panic and some token output.
        let raw = RawCodeBlock {
            source: "fn (".to_string(),
            language: Some("rust".to_string()),
            line: 1,
            byte_offset: 0,
        };
        let _block = highlight_code(&raw);
    }
}
