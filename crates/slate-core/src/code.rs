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

/// Largest code-block source we hand to tree-sitter (audit #245 M3).
/// Above the cap we skip parsing and emit a single `Other("oversized")`
/// token; the visual fallback is plain-monospace rendering which is
/// honest for huge blocks anyway. 256 KiB comfortably covers
/// real-world fenced blocks; pathological / minified inputs (10MB+
/// of one-line JS) would otherwise blow memory on the token stream.
const CODE_BLOCK_MAX_BYTES: usize = 256 * 1024;

// Note on parse-time bounds (audit #245 M3, partial address):
// tree-sitter 0.26 replaced `set_timeout_micros` with a
// `progress_callback` on `ParseOptions`. For this PR we rely on
// the size cap (`CODE_BLOCK_MAX_BYTES`) as the primary bound —
// 256 KiB of source has a hard ceiling on parse cost in practice.
// A proper wall-clock callback can be added as a follow-up if real-
// world profiling shows it's needed.

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

    // Audit #245 M3: size cap. Anything past the cap skips tree-
    // sitter entirely and emits one `Other("oversized")` token so
    // downstream consumers see "no syntax highlighting available"
    // rather than a multi-million-entry token vector.
    if raw.source.len() > CODE_BLOCK_MAX_BYTES {
        return CodeBlock {
            source: raw.source.clone(),
            language: raw.language.clone(),
            tokens: vec![SyntaxToken {
                start_byte: 0,
                end_byte: raw.source.len() as u32,
                kind: TokenKind::Other("oversized".to_string()),
            }],
            semantic_spans: Vec::new(),
            line: raw.line,
            byte_offset: raw.byte_offset,
        };
    }

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
    let (tokens, semantic_spans) = walk_tree(&tree, raw.source.as_bytes(), &language_key);

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
fn walk_tree(tree: &Tree, source: &[u8], language: &str) -> (Vec<SyntaxToken>, Vec<SemanticSpan>) {
    let mut tokens: Vec<SyntaxToken> = Vec::new();
    let mut semantic: Vec<SemanticSpan> = Vec::new();
    let mut cursor = tree.walk();
    walk_node(
        tree.root_node(),
        source,
        language,
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
    language: &str,
    tokens: &mut Vec<SyntaxToken>,
    semantic: &mut Vec<SemanticSpan>,
    cursor: &mut tree_sitter::TreeCursor<'a>,
) {
    capture_semantic(node, source, semantic);

    let kind_name = node.kind();
    let is_leaf = node.child_count() == 0;
    if is_leaf && !node.is_extra() && !node.is_missing() {
        if let Some(kind) = classify_node_kind(kind_name, language) {
            tokens.push(SyntaxToken {
                start_byte: node.start_byte() as u32,
                end_byte: node.end_byte() as u32,
                kind,
            });
        }
        return;
    }
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
        let mut child_cursor = child.walk();
        walk_node(child, source, language, tokens, semantic, &mut child_cursor);
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

/// Audit #247: per-grammar classifier tables replace the previous
/// substring heuristics (`contains("string")`, `contains("type")`)
/// that over-matched and under-matched across grammars.
fn classify_node_kind(kind: &str, language: &str) -> Option<TokenKind> {
    let result = match language {
        "rust" | "rs" => classify_rust(kind),
        "swift" => classify_swift(kind),
        "python" | "py" => classify_python(kind),
        "javascript" | "js" => classify_javascript(kind),
        "typescript" | "ts" | "tsx" => classify_typescript(kind),
        "json" => classify_json(kind),
        "yaml" | "yml" => classify_yaml(kind),
        "bash" | "sh" | "shell" => classify_bash(kind),
        "sql" => classify_sql(kind),
        "html" => classify_html(kind),
        "css" => classify_css(kind),
        "go" => classify_go(kind),
        "c" => classify_c(kind),
        "cpp" | "c++" | "cxx" | "cc" => classify_cpp(kind),
        "markdown" | "md" => classify_markdown(kind),
        _ => None,
    };
    result.or_else(|| classify_universal(kind))
}

fn classify_rust(kind: &str) -> Option<TokenKind> {
    match kind {
        "identifier" | "field_identifier" | "shorthand_field_identifier" => {
            Some(TokenKind::Identifier)
        }
        "type_identifier" | "primitive_type" => Some(TokenKind::Type),
        "string_content" | "char_literal" | "escape_sequence" => Some(TokenKind::String),
        "integer_literal" | "float_literal" => Some(TokenKind::Number),
        "fn" | "let" | "mut" | "const" | "static" | "if" | "else" | "match" | "while" | "for"
        | "loop" | "return" | "break" | "continue" | "use" | "pub" | "mod" | "struct" | "enum"
        | "trait" | "impl" | "self" | "Self" | "where" | "async" | "await" | "as" | "in"
        | "ref" | "move" | "true" | "false" | "crate" | "super" | "dyn" | "type" | "unsafe"
        | "extern" | "mutable_specifier" => Some(TokenKind::Keyword),
        _ => None,
    }
}

fn classify_swift(kind: &str) -> Option<TokenKind> {
    match kind {
        "simple_identifier" => Some(TokenKind::Identifier),
        "type_identifier" => Some(TokenKind::Type),
        "line_str_text" | "multi_line_str_text" | "escape_sequence" => Some(TokenKind::String),
        "integer_literal" | "real_literal" => Some(TokenKind::Number),
        "func" | "let" | "var" | "return" | "if" | "else" | "for" | "while" | "switch" | "case"
        | "default" | "break" | "continue" | "struct" | "class" | "enum" | "protocol"
        | "import" | "self" | "Self" | "true" | "false" | "nil" | "guard" | "do" | "try"
        | "catch" | "throw" | "throws" | "async" | "await" | "in" | "is" | "as" | "typealias"
        | "static" | "private" | "public" | "internal" | "override" | "init" | "deinit"
        | "where" | "repeat" => Some(TokenKind::Keyword),
        _ => None,
    }
}

fn classify_python(kind: &str) -> Option<TokenKind> {
    match kind {
        "identifier" => Some(TokenKind::Identifier),
        "string_start" | "string_content" | "string_end" | "escape_sequence" => {
            Some(TokenKind::String)
        }
        "integer" | "float" => Some(TokenKind::Number),
        "def" | "class" | "return" | "if" | "elif" | "else" | "for" | "while" | "break"
        | "continue" | "import" | "from" | "as" | "with" | "try" | "except" | "finally"
        | "raise" | "pass" | "del" | "and" | "or" | "not" | "in" | "is" | "lambda" | "yield"
        | "global" | "nonlocal" | "assert" | "True" | "False" | "None" | "async" | "await" => {
            Some(TokenKind::Keyword)
        }
        _ => None,
    }
}

fn classify_javascript(kind: &str) -> Option<TokenKind> {
    match kind {
        "identifier"
        | "property_identifier"
        | "shorthand_property_identifier"
        | "shorthand_property_identifier_pattern" => Some(TokenKind::Identifier),
        "string_fragment" | "escape_sequence" | "template_fragment" => Some(TokenKind::String),
        "number" => Some(TokenKind::Number),
        "function" | "let" | "const" | "var" | "return" | "if" | "else" | "for" | "while"
        | "do" | "switch" | "case" | "default" | "break" | "continue" | "class" | "extends"
        | "new" | "delete" | "typeof" | "instanceof" | "void" | "in" | "of" | "import"
        | "export" | "from" | "as" | "try" | "catch" | "finally" | "throw" | "yield" | "async"
        | "await" | "true" | "false" | "null" | "undefined" | "this" | "super" => {
            Some(TokenKind::Keyword)
        }
        _ => None,
    }
}

fn classify_typescript(kind: &str) -> Option<TokenKind> {
    match kind {
        "identifier"
        | "property_identifier"
        | "shorthand_property_identifier"
        | "shorthand_property_identifier_pattern" => Some(TokenKind::Identifier),
        "type_identifier" => Some(TokenKind::Type),
        "string_fragment" | "escape_sequence" | "template_fragment" => Some(TokenKind::String),
        "number" => Some(TokenKind::Number),
        "string" | "boolean" | "void" | "any" | "never" | "unknown" | "object" | "symbol"
        | "bigint" => Some(TokenKind::Type),
        "function" | "let" | "const" | "var" | "return" | "if" | "else" | "for" | "while"
        | "do" | "switch" | "case" | "default" | "break" | "continue" | "class" | "extends"
        | "implements" | "interface" | "new" | "delete" | "typeof" | "instanceof" | "in" | "of"
        | "import" | "export" | "from" | "as" | "try" | "catch" | "finally" | "throw" | "yield"
        | "async" | "await" | "true" | "false" | "null" | "undefined" | "this" | "super"
        | "abstract" | "declare" | "enum" | "type" | "namespace" | "module" | "readonly"
        | "private" | "protected" | "public" | "static" | "keyof" | "infer" => {
            Some(TokenKind::Keyword)
        }
        _ => None,
    }
}

fn classify_json(kind: &str) -> Option<TokenKind> {
    match kind {
        "string_content" => Some(TokenKind::String),
        "number" => Some(TokenKind::Number),
        "true" | "false" | "null" => Some(TokenKind::Keyword),
        _ => None,
    }
}

fn classify_yaml(kind: &str) -> Option<TokenKind> {
    match kind {
        "string_scalar" | "double_quote_scalar" | "single_quote_scalar" | "block_scalar" => {
            Some(TokenKind::String)
        }
        "integer_scalar" | "float_scalar" => Some(TokenKind::Number),
        "boolean_scalar" | "null_scalar" => Some(TokenKind::Keyword),
        "anchor" | "alias" | "tag" => Some(TokenKind::Identifier),
        _ => None,
    }
}

fn classify_bash(kind: &str) -> Option<TokenKind> {
    match kind {
        "variable_name" | "word" | "special_variable_name" => Some(TokenKind::Identifier),
        "string_content" | "raw_string" | "ansii_c_string" | "heredoc_body" => {
            Some(TokenKind::String)
        }
        "number" => Some(TokenKind::Number),
        "if" | "then" | "else" | "elif" | "fi" | "for" | "while" | "do" | "done" | "case"
        | "esac" | "in" | "function" | "return" | "local" | "export" | "declare" | "readonly"
        | "unset" => Some(TokenKind::Keyword),
        _ => None,
    }
}

fn classify_sql(kind: &str) -> Option<TokenKind> {
    match kind {
        "identifier" => Some(TokenKind::Identifier),
        "literal" => Some(TokenKind::Number),
        k if k.starts_with("keyword_") => Some(TokenKind::Keyword),
        _ => None,
    }
}

fn classify_html(kind: &str) -> Option<TokenKind> {
    match kind {
        "tag_name" => Some(TokenKind::Keyword),
        "attribute_name" => Some(TokenKind::Identifier),
        "attribute_value" | "text" => Some(TokenKind::String),
        _ => None,
    }
}

fn classify_css(kind: &str) -> Option<TokenKind> {
    match kind {
        "identifier" | "class_name" | "id_name" | "property_name" | "feature_name" => {
            Some(TokenKind::Identifier)
        }
        "tag_name" | "nesting_selector" | "universal_selector" | "important" => {
            Some(TokenKind::Keyword)
        }
        "string_value" | "plain_value" => Some(TokenKind::String),
        "integer_value" | "float_value" | "color_value" => Some(TokenKind::Number),
        "unit" => Some(TokenKind::Type),
        _ => None,
    }
}

fn classify_go(kind: &str) -> Option<TokenKind> {
    match kind {
        "identifier" | "field_identifier" | "package_identifier" => Some(TokenKind::Identifier),
        "type_identifier" => Some(TokenKind::Type),
        "interpreted_string_literal_content"
        | "raw_string_literal_content"
        | "rune_literal"
        | "escape_sequence" => Some(TokenKind::String),
        "int_literal" | "float_literal" | "imaginary_literal" => Some(TokenKind::Number),
        "package" | "import" | "func" | "return" | "if" | "else" | "for" | "range" | "switch"
        | "case" | "default" | "break" | "continue" | "go" | "defer" | "select" | "chan"
        | "map" | "struct" | "interface" | "type" | "const" | "var" | "fallthrough" | "goto"
        | "true" | "false" | "nil" | "iota" => Some(TokenKind::Keyword),
        _ => None,
    }
}

fn classify_c(kind: &str) -> Option<TokenKind> {
    match kind {
        "identifier" | "field_identifier" => Some(TokenKind::Identifier),
        "type_identifier" | "primitive_type" | "sized_type_specifier" => Some(TokenKind::Type),
        "string_content" | "char_literal" | "escape_sequence" | "system_lib_string" => {
            Some(TokenKind::String)
        }
        "number_literal" => Some(TokenKind::Number),
        "if" | "else" | "for" | "while" | "do" | "switch" | "case" | "default" | "break"
        | "continue" | "return" | "goto" | "struct" | "union" | "enum" | "typedef" | "sizeof"
        | "static" | "extern" | "const" | "volatile" | "inline" | "register" | "auto" | "void"
        | "signed" | "unsigned" | "true" | "false" | "NULL" => Some(TokenKind::Keyword),
        _ => None,
    }
}

fn classify_cpp(kind: &str) -> Option<TokenKind> {
    match kind {
        "identifier" | "field_identifier" | "namespace_identifier" | "destructor_name" => {
            Some(TokenKind::Identifier)
        }
        "type_identifier" | "primitive_type" | "sized_type_specifier" | "auto" => {
            Some(TokenKind::Type)
        }
        "string_content" | "char_literal" | "escape_sequence" | "raw_string_content"
        | "system_lib_string" => Some(TokenKind::String),
        "number_literal" => Some(TokenKind::Number),
        "if" | "else" | "for" | "while" | "do" | "switch" | "case" | "default" | "break"
        | "continue" | "return" | "goto" | "struct" | "class" | "union" | "enum" | "typedef"
        | "sizeof" | "static" | "extern" | "const" | "volatile" | "inline" | "virtual"
        | "override" | "final" | "explicit" | "friend" | "namespace" | "using" | "template"
        | "typename" | "public" | "private" | "protected" | "new" | "delete" | "throw" | "try"
        | "catch" | "noexcept" | "nullptr" | "true" | "false" | "this" | "operator"
        | "constexpr" | "static_cast" | "dynamic_cast" | "reinterpret_cast" | "const_cast" => {
            Some(TokenKind::Keyword)
        }
        _ => None,
    }
}

fn classify_markdown(kind: &str) -> Option<TokenKind> {
    match kind {
        "inline" | "text" => Some(TokenKind::Other("text".into())),
        "atx_h1_marker"
        | "atx_h2_marker"
        | "atx_h3_marker"
        | "atx_h4_marker"
        | "atx_h5_marker"
        | "atx_h6_marker"
        | "setext_h1_underline"
        | "setext_h2_underline"
        | "thematic_break" => Some(TokenKind::Keyword),
        "list_marker_minus"
        | "list_marker_plus"
        | "list_marker_star"
        | "list_marker_dot"
        | "list_marker_parenthesis"
        | "code_span_delimiter"
        | "fenced_code_block_delimiter" => Some(TokenKind::Punctuation),
        _ => None,
    }
}

fn classify_universal(kind: &str) -> Option<TokenKind> {
    match kind {
        "->" | "=>" | "==" | "!=" | ">=" | "<=" | "&&" | "||" | "+=" | "-=" | "*=" | "/="
        | "%=" | "&=" | "|=" | "^=" | "<<" | ">>" | "++" | "--" | "**" | ":=" | "+" | "-" | "/"
        | "%" | "=" | "!" | "~" | "*" | "&" | "|" | "^" | "<" | ">" => Some(TokenKind::Operator),
        "\"" | "'" | "`" => Some(TokenKind::String),
        "(" | ")" | "{" | "}" | "[" | "]" | "," | ";" | ":" | "::" | "." | "?" | "</" | "#"
        | "@" | "\\" | "$" => Some(TokenKind::Punctuation),
        _ => None,
    }
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
    fn no_panic_on_malformed_source() {
        let raw = RawCodeBlock {
            source: "fn (".to_string(),
            language: Some("rust".to_string()),
            line: 1,
            byte_offset: 0,
        };
        let _block = highlight_code(&raw);
    }

    #[test]
    fn oversized_block_returns_single_oversized_token() {
        let big = "fn foo() {}\n".repeat(30_000);
        assert!(big.len() > CODE_BLOCK_MAX_BYTES);
        let raw = RawCodeBlock {
            source: big.clone(),
            language: Some("rust".to_string()),
            line: 1,
            byte_offset: 0,
        };
        let block = highlight_code(&raw);
        assert_eq!(block.tokens.len(), 1);
        match &block.tokens[0].kind {
            TokenKind::Other(label) => assert_eq!(label, "oversized"),
            other => panic!("expected Other(\"oversized\"); got {other:?}"),
        }
        assert!(block.semantic_spans.is_empty());
    }

    // --- Per-grammar token classification tests (audit #247) ---

    fn has_kind(block: &CodeBlock, kind: &TokenKind) -> bool {
        block
            .tokens
            .iter()
            .any(|t| std::mem::discriminant(&t.kind) == std::mem::discriminant(kind))
    }

    #[test]
    fn grammar_rust() {
        let block = highlight_code(&RawCodeBlock {
            source: "fn foo(x: i32) -> String { let s = \"hello\"; return s; }".into(),
            language: Some("rust".into()),
            line: 1,
            byte_offset: 0,
        });
        assert!(has_kind(&block, &TokenKind::Keyword));
        assert!(has_kind(&block, &TokenKind::Identifier));
        assert!(has_kind(&block, &TokenKind::Type));
        assert!(has_kind(&block, &TokenKind::String));
    }

    #[test]
    fn grammar_swift_simple_identifier() {
        let block = highlight_code(&RawCodeBlock {
            source: "func foo(x: Int) -> String { let s = \"hello\"; return s }".into(),
            language: Some("swift".into()),
            line: 1,
            byte_offset: 0,
        });
        assert!(has_kind(&block, &TokenKind::Keyword));
        assert!(
            has_kind(&block, &TokenKind::Identifier),
            "Swift's simple_identifier must classify as Identifier"
        );
        assert!(has_kind(&block, &TokenKind::Type));
        assert!(has_kind(&block, &TokenKind::String));
    }

    #[test]
    fn grammar_python() {
        let block = highlight_code(&RawCodeBlock {
            source: "def foo(x):\n    s = \"hello\"\n    return s\n".into(),
            language: Some("python".into()),
            line: 1,
            byte_offset: 0,
        });
        assert!(has_kind(&block, &TokenKind::Keyword));
        assert!(has_kind(&block, &TokenKind::Identifier));
        assert!(has_kind(&block, &TokenKind::String));
    }

    #[test]
    fn grammar_javascript() {
        let block = highlight_code(&RawCodeBlock {
            source: "function foo(x) { let s = \"hello\"; return s; }\nconst n = 42;".into(),
            language: Some("javascript".into()),
            line: 1,
            byte_offset: 0,
        });
        assert!(has_kind(&block, &TokenKind::Keyword));
        assert!(has_kind(&block, &TokenKind::Identifier));
        assert!(has_kind(&block, &TokenKind::String));
        assert!(has_kind(&block, &TokenKind::Number));
    }

    #[test]
    fn grammar_typescript() {
        let block = highlight_code(&RawCodeBlock {
            source:
                "function foo(x: number): string { return \"hi\"; }\ninterface Bar { val: string }"
                    .into(),
            language: Some("typescript".into()),
            line: 1,
            byte_offset: 0,
        });
        assert!(has_kind(&block, &TokenKind::Keyword));
        assert!(
            has_kind(&block, &TokenKind::Type),
            "TypeScript type_identifier must classify as Type"
        );
        assert!(has_kind(&block, &TokenKind::String));
        assert!(has_kind(&block, &TokenKind::Number));
    }

    #[test]
    fn grammar_json() {
        let block = highlight_code(&RawCodeBlock {
            source: r#"{"key": 42, "flag": true}"#.into(),
            language: Some("json".into()),
            line: 1,
            byte_offset: 0,
        });
        assert!(has_kind(&block, &TokenKind::String));
        assert!(has_kind(&block, &TokenKind::Number));
        assert!(has_kind(&block, &TokenKind::Keyword));
    }

    #[test]
    fn grammar_yaml() {
        let block = highlight_code(&RawCodeBlock {
            source: "key: value\nnumber: 42\nbool: true".into(),
            language: Some("yaml".into()),
            line: 1,
            byte_offset: 0,
        });
        assert!(has_kind(&block, &TokenKind::String));
        assert!(has_kind(&block, &TokenKind::Number));
        assert!(has_kind(&block, &TokenKind::Keyword));
    }

    #[test]
    fn grammar_bash() {
        let block = highlight_code(&RawCodeBlock {
            source: "if [ $foo = \"hello\" ]; then\n  echo $foo\nfi".into(),
            language: Some("bash".into()),
            line: 1,
            byte_offset: 0,
        });
        assert!(has_kind(&block, &TokenKind::Keyword));
        assert!(has_kind(&block, &TokenKind::Identifier));
        assert!(has_kind(&block, &TokenKind::String));
    }

    #[test]
    fn grammar_html() {
        let block = highlight_code(&RawCodeBlock {
            source: "<div class=\"box\"><p>Hello</p></div>".into(),
            language: Some("html".into()),
            line: 1,
            byte_offset: 0,
        });
        assert!(
            has_kind(&block, &TokenKind::Keyword),
            "HTML tag_name must classify as Keyword"
        );
        assert!(
            has_kind(&block, &TokenKind::Identifier),
            "HTML attribute_name must classify as Identifier"
        );
        assert!(has_kind(&block, &TokenKind::String));
    }

    #[test]
    fn grammar_css() {
        let block = highlight_code(&RawCodeBlock {
            source: ".box { color: red; }".into(),
            language: Some("css".into()),
            line: 1,
            byte_offset: 0,
        });
        assert!(has_kind(&block, &TokenKind::Identifier));
        assert!(has_kind(&block, &TokenKind::String));
    }

    #[test]
    fn grammar_go() {
        let block = highlight_code(&RawCodeBlock {
            source: "package main\nfunc foo(x int) string { s := \"hello\"; return s }".into(),
            language: Some("go".into()),
            line: 1,
            byte_offset: 0,
        });
        assert!(has_kind(&block, &TokenKind::Keyword));
        assert!(has_kind(&block, &TokenKind::Identifier));
        assert!(has_kind(&block, &TokenKind::Type));
        assert!(has_kind(&block, &TokenKind::String));
    }

    #[test]
    fn grammar_c() {
        let block = highlight_code(&RawCodeBlock {
            source: "int foo(int x) { char* s = \"hello\"; return 0; }".into(),
            language: Some("c".into()),
            line: 1,
            byte_offset: 0,
        });
        assert!(has_kind(&block, &TokenKind::Keyword));
        assert!(has_kind(&block, &TokenKind::Type));
        assert!(has_kind(&block, &TokenKind::String));
        assert!(has_kind(&block, &TokenKind::Number));
    }

    #[test]
    fn grammar_cpp() {
        let block = highlight_code(&RawCodeBlock {
            source: "class Bar { public: double val; };".into(),
            language: Some("cpp".into()),
            line: 1,
            byte_offset: 0,
        });
        assert!(has_kind(&block, &TokenKind::Keyword));
        assert!(has_kind(&block, &TokenKind::Type));
        assert!(has_kind(&block, &TokenKind::Identifier));
    }

    #[test]
    fn grammar_sql() {
        let block = highlight_code(&RawCodeBlock {
            source: "SELECT id, name FROM users WHERE age > 18;".into(),
            language: Some("sql".into()),
            line: 1,
            byte_offset: 0,
        });
        assert!(
            has_kind(&block, &TokenKind::Keyword),
            "SQL keyword_* nodes must classify as Keyword"
        );
        assert!(has_kind(&block, &TokenKind::Identifier));
        assert!(has_kind(&block, &TokenKind::Number));
    }

    #[test]
    fn grammar_markdown() {
        let block = highlight_code(&RawCodeBlock {
            source: "# Hello\n\n- item\n- item2".into(),
            language: Some("markdown".into()),
            line: 1,
            byte_offset: 0,
        });
        assert!(!block.tokens.is_empty());
    }

    /// Audit #248: tree-sitter-sequel is a third-party grammar (maintainer:
    /// derekstride, not the tree-sitter org). This test pins the `keyword_*`
    /// naming convention our `classify_sql` relies on — if the grammar
    /// changes its node-kind scheme, this test breaks and flags the issue.
    #[test]
    fn sql_grammar_keyword_prefix_convention() {
        let mut parser = Parser::new();
        parser
            .set_language(&tree_sitter_sequel::LANGUAGE.into())
            .expect("tree-sitter-sequel must be compatible with tree-sitter 0.26");
        let source = "SELECT id FROM users WHERE age > 18 ORDER BY name LIMIT 10;";
        let tree = parser.parse(source, None).expect("SQL parse must succeed");
        let mut keyword_nodes: Vec<String> = Vec::new();
        collect_keywords(tree.root_node(), &mut keyword_nodes);
        assert!(
            keyword_nodes.iter().all(|k| k.starts_with("keyword_")),
            "tree-sitter-sequel SQL keywords must use the keyword_* prefix convention; \
             got: {keyword_nodes:?}"
        );
        let expected = [
            "keyword_select",
            "keyword_from",
            "keyword_where",
            "keyword_order",
            "keyword_by",
            "keyword_limit",
        ];
        for kw in &expected {
            assert!(
                keyword_nodes.iter().any(|k| k == kw),
                "expected {kw} in SQL keyword nodes; got: {keyword_nodes:?}"
            );
        }
    }
}

fn collect_keywords(node: tree_sitter::Node, out: &mut Vec<String>) {
    let kind = node.kind();
    if kind.starts_with("keyword_") {
        out.push(kind.to_string());
    }
    let mut cursor = node.walk();
    for child in node.children(&mut cursor) {
        collect_keywords(child, out);
    }
}
