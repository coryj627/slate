//! YAML frontmatter parser with type inference.
//!
//! A note's frontmatter is the YAML block bracketed by `---` lines at
//! the start of the file. We pull every leaf value out as a typed
//! `Property` so downstream storage (#54) and UI (#55) can render it
//! as a structured Properties Panel without re-doing the YAML walk.
//!
//! ## Detection
//!
//! Per the spec: a `---` line at byte 0, content until the next `---`
//! line at column 0. No leading whitespace allowed on either delimiter.
//! Files that don't start with `---` get an empty result without any
//! YAML parsing — fast path for the (very common) plain-Markdown case.
//!
//! ## YAML crate choice
//!
//! `yaml-rust2` over `serde_yaml`:
//!   - serde_yaml is officially deprecated as of 0.9.34.
//!   - yaml-rust2 is the maintained YAML 1.2 fork and surfaces the
//!     raw `Yaml` enum, which is what we want — we're not
//!     deserializing into known shapes; we're walking the parsed tree
//!     to classify leaves and flatten nested objects.
//!
//! ## Type inference
//!
//! Each leaf maps to one of `text` / `number` / `boolean` / `date` /
//! `datetime` / `wikilink`, or a `list` of those, or a `tag_list`
//! (Obsidian-flavored hashtag convention). Heuristics are bounded:
//! we don't second-guess yaml-rust2's existing scalar typing for
//! numbers / booleans / null — we only layer date / datetime /
//! wikilink / tag-list on top of strings it already classified as
//! text.
//!
//! ## Nested objects
//!
//! Flattened via dotted keys: `person.name`, `person.role.title`,
//! etc. Documented as a deliberate choice in the issue; revisit if
//! testers find it surprising.
//!
//! ## Malformed YAML
//!
//! The parser never returns an error to the caller. yaml-rust2's
//! `YamlLoader::load_from_str` is whole-document — any syntax
//! error in the frontmatter aborts the parse and we return an
//! empty `Vec<Property>` plus a single `PropertyParseWarning`
//! describing the failure. We do NOT do per-line shrink + retry;
//! a malformed line wipes the row's properties. Future-issue:
//! implement a real partial parser if testers report frequent
//! "lost everything on one typo" friction.
//!
//! ## Deep nesting
//!
//! `walk_value` is recursion-bounded by `MAX_WALK_DEPTH` so a
//! synced vault containing a pathological note (mappings nested
//! beyond reasonable structure) doesn't stack-overflow the
//! scanner. Past the depth we record a warning and stop
//! descending — the prefix that fit under the budget still
//! lands.

use std::ops::Range;

use yaml_rust2::yaml::Hash as YamlHash;
use yaml_rust2::{Yaml, YamlEmitter, YamlLoader};

/// Maximum nesting depth for `walk_value`. yaml-rust2 has a
/// parser-side guard but no runtime cap on the resulting tree, so a
/// pathologically deep mapping in a synced vault note could blow
/// the indexer's stack. 32 levels covers any realistic frontmatter
/// shape (Obsidian / Logseq / Bear style); past it we emit a
/// `PropertyParseWarning` and stop descending. The prefix that fit
/// under the budget still lands.
const MAX_WALK_DEPTH: usize = 32;

/// One parsed frontmatter key-value pair with its inferred type.
#[derive(Debug, Clone, PartialEq)]
pub struct Property {
    /// Dot-joined key path: `tags`, `person.name`, etc. Unicode keys
    /// pass through verbatim.
    pub key: String,
    pub value: PropertyValue,
}

/// Inferred type + value for a single property.
///
/// Variants are deliberately flat — no metadata (provenance, parse
/// site, etc.) — so the storage layer (#54) can persist them with a
/// single discriminator column.
#[derive(Debug, Clone, PartialEq)]
pub enum PropertyValue {
    Text(String),
    Integer(i64),
    Float(f64),
    Boolean(bool),
    /// `YYYY-MM-DD` strings get classified as dates rather than
    /// generic text. The original string is preserved verbatim so
    /// downstream formatters can re-emit it without timezone games.
    Date(String),
    /// ISO-8601 datetime strings (`YYYY-MM-DDTHH:MM:SS` + optional
    /// `Z` or `±HH:MM`). Preserved as text.
    Datetime(String),
    /// A YAML string of the form `[[target]]` (Obsidian convention
    /// for wikilinks inside frontmatter values). The inner target is
    /// stored; the brackets are stripped.
    Wikilink(String),
    /// Homogeneous list of leaf values. Mixed-type input collapses
    /// to a `List` of `Text` per the spec.
    List(Vec<PropertyValue>),
    /// List of bare tag strings. Produced when either:
    ///   - the key is exactly `tags` (Obsidian convention), or
    ///   - the value is a list whose entries are all `#`-prefixed
    ///     strings (we strip the `#` when storing each tag).
    TagList(Vec<String>),
}

/// A non-fatal warning produced while parsing frontmatter. Returned
/// alongside any properties that did parse so the caller can show the
/// user "we read this much of your frontmatter, here's what went
/// wrong with the rest."
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct PropertyParseWarning {
    /// Best-effort key path the warning belongs to. `None` when the
    /// failure happened before any keys could be associated (e.g. the
    /// YAML lexer aborted at line 1).
    pub key_path: Option<String>,
    pub message: String,
}

/// Find the frontmatter block bounds in `source`, returning the
/// byte range of the YAML body (between the two `---` lines).
///
/// `None` when the file doesn't start with a delimiter line; this is
/// the fast path for plain-Markdown notes and gets taken on the vast
/// majority of files in a typical vault.
pub fn frontmatter_range(source: &str) -> Option<Range<usize>> {
    // Strict: the leading delimiter must be at byte 0.
    let after_first = strip_opening_delimiter(source)?;
    let body_start = source.len() - after_first.len();
    // Look for the closing `---` at the start of a line.
    let mut search_from = 0usize;
    while let Some(rel) = after_first[search_from..].find("\n---") {
        let line_start = search_from + rel + 1; // past the `\n`
        let after_delim = &after_first[line_start + 3..];
        // The closing delimiter line must be exactly `---` followed
        // by EOL (with optional trailing whitespace).
        let line_end = after_delim
            .find('\n')
            .map(|n| line_start + 3 + n)
            .unwrap_or(after_first.len());
        let trailing = &after_first[line_start + 3..line_end];
        if trailing.trim().is_empty() {
            return Some(body_start..body_start + line_start);
        }
        search_from = line_start + 3;
    }
    None
}

/// Return the source slice past a YAML frontmatter block, if any —
/// i.e. the Markdown body the reader actually sees. When there's no
/// detectable frontmatter (no leading `---` line, or no matching
/// closing `---`), returns `source` unchanged.
///
/// Wrap-around helper for parsers that operate on the body and would
/// be confused by YAML's `---` delimiters (which pulldown-cmark
/// reads as Setext H2 underlines when they follow non-blank text —
/// issue #227). Uses `frontmatter_range` for detection so the
/// definition of "is this a frontmatter block" stays in one place.
pub fn body_after_frontmatter(source: &str) -> &str {
    let Some(range) = frontmatter_range(source) else {
        return source;
    };
    // `range.end` is the start of the closing `---` line. Skip the
    // `---` plus its line ending (CRLF or LF). If the file ended at
    // the closing delimiter with no trailing newline, return the
    // empty slice past it.
    let after_body = &source[range.end..];
    let Some(rest) = after_body.strip_prefix("---") else {
        return source;
    };
    rest.strip_prefix("\r\n")
        .or_else(|| rest.strip_prefix('\n'))
        .unwrap_or(rest)
}

/// Returns the source slice past the opening `---` line if and only
/// if the file starts with one (byte 0 must be `-`, or a UTF-8 BOM
/// followed by `-`).
///
/// The dashes must be followed by EOL, with optional trailing
/// whitespace tolerated on the line. The closing delimiter already
/// accepts the same shape via `trailing.trim().is_empty()`; symmetry
/// matters because files authored with `--- \n` at byte 0 previously
/// looked like "no frontmatter at all" while the same trailing-space
/// pattern on the closing delimiter was accepted (#93 item 4).
///
/// A leading UTF-8 BOM (`\u{FEFF}`) is tolerated and consumed before
/// the dash check — many editors default-save UTF-8-with-BOM, and
/// without this the writer would synthesize a duplicate frontmatter
/// block ahead of the BOM and silently shadow the user's original
/// (audit #173).
///
/// A bare `---` at EOF still isn't a valid opening (no body can
/// follow), so a missing newline returns `None`.
fn strip_opening_delimiter(source: &str) -> Option<&str> {
    let body_source = source.strip_prefix('\u{FEFF}').unwrap_or(source);
    let after_dashes = body_source.strip_prefix("---")?;
    let line_end = after_dashes.find('\n')?;
    let trailing = &after_dashes[..line_end];
    if !trailing.chars().all(char::is_whitespace) {
        return None;
    }
    Some(&after_dashes[line_end + 1..])
}

/// Parse the frontmatter block out of `source` and return typed
/// properties plus any non-fatal warnings.
///
/// Always returns: never panics, never errors. Bad YAML produces an
/// empty (or partial) properties vec + a warning entry.
pub fn extract_frontmatter(source: &str) -> (Vec<Property>, Vec<PropertyParseWarning>) {
    let range = match frontmatter_range(source) {
        Some(r) => r,
        None => return (Vec::new(), Vec::new()),
    };
    let yaml_src = &source[range];

    let docs = match YamlLoader::load_from_str(yaml_src) {
        Ok(d) => d,
        Err(e) => {
            return (
                Vec::new(),
                vec![PropertyParseWarning {
                    key_path: None,
                    message: format!("YAML parse error: {e}"),
                }],
            );
        }
    };

    let Some(root) = docs.into_iter().next() else {
        return (Vec::new(), Vec::new());
    };

    let mut props = Vec::new();
    let mut warnings = Vec::new();

    match root {
        Yaml::Hash(map) => {
            for (k, v) in map {
                let key = yaml_key_to_string(&k);
                walk_value(&key, &v, 0, &mut props, &mut warnings);
            }
        }
        Yaml::Null | Yaml::BadValue => {
            // Empty `---\n---` block is fine — no properties, no warning.
        }
        other => {
            warnings.push(PropertyParseWarning {
                key_path: None,
                message: format!(
                    "frontmatter root must be a YAML mapping; got {}",
                    yaml_type_name(&other)
                ),
            });
        }
    }

    (props, warnings)
}

/// Recursively walk one YAML value, emitting flat `Property` rows for
/// every leaf. Nested mappings get dotted-key flattening; lists get
/// classified as `List` or `TagList`.
///
/// `depth` is the current nesting level; root callers pass 0. Past
/// `MAX_WALK_DEPTH` we emit a warning and stop descending so a
/// pathologically nested note can't stack-overflow the scanner.
fn walk_value(
    key: &str,
    value: &Yaml,
    depth: usize,
    props: &mut Vec<Property>,
    warnings: &mut Vec<PropertyParseWarning>,
) {
    match value {
        Yaml::Hash(map) => {
            // Boundary guard sits on the parent (this Hash), not on
            // each child. Recursing into N children at depth+1 and
            // letting each one trip its own guard would emit N
            // warnings for a wide map at the cap — Codoki PR 95
            // callout. Emit one warning for the boundary key and
            // don't iterate.
            if depth >= MAX_WALK_DEPTH {
                warnings.push(PropertyParseWarning {
                    key_path: Some(key.to_string()),
                    message: format!(
                        "nesting exceeds the maximum depth of {MAX_WALK_DEPTH}; deeper keys are skipped"
                    ),
                });
                return;
            }
            for (k, v) in map {
                let sub_key = yaml_key_to_string(k);
                let full_key = format!("{key}.{sub_key}");
                walk_value(&full_key, v, depth + 1, props, warnings);
            }
        }
        Yaml::Array(items) => match classify_list(key, items) {
            Some(value) => props.push(Property {
                key: key.to_string(),
                value,
            }),
            None => warnings.push(PropertyParseWarning {
                key_path: Some(key.to_string()),
                message:
                    "list contains a nested mapping; flattening lists of objects is not supported"
                        .to_string(),
            }),
        },
        Yaml::Null => {
            // Explicit null in YAML — represent as empty text for
            // now. The properties panel can render this as "(empty)".
            props.push(Property {
                key: key.to_string(),
                value: PropertyValue::Text(String::new()),
            });
        }
        leaf => {
            if let Some(value) = classify_leaf(key, leaf) {
                props.push(Property {
                    key: key.to_string(),
                    value,
                });
            } else {
                warnings.push(PropertyParseWarning {
                    key_path: Some(key.to_string()),
                    message: format!("unsupported YAML value type {}", yaml_type_name(leaf)),
                });
            }
        }
    }
}

/// Classify a single YAML leaf — non-string scalars go through
/// yaml-rust2's existing typing; strings get layered date / datetime /
/// wikilink detection on top.
fn classify_leaf(key: &str, value: &Yaml) -> Option<PropertyValue> {
    match value {
        Yaml::Integer(i) => Some(PropertyValue::Integer(*i)),
        // yaml-rust2 marks scalars as Real when they look numeric but
        // aren't integers. Most of those parse cleanly as f64
        // (`3.14`, `1e-3`, etc.), but `.nan` / `.inf` / `-.inf` and
        // numerically-suspect forms can fail. Falling back to Text
        // keeps the property visible to the user instead of
        // surfacing an unsupported-type warning — Codoki's PR 81
        // callout.
        Yaml::Real(s) => Some(
            s.parse::<f64>()
                .map(PropertyValue::Float)
                .unwrap_or_else(|_| PropertyValue::Text(s.clone())),
        ),
        Yaml::Boolean(b) => Some(PropertyValue::Boolean(*b)),
        Yaml::String(s) => Some(classify_string(key, s)),
        // BadValue is yaml-rust2's "couldn't parse this scalar"
        // signal; treat as text so the user sees what was authored.
        Yaml::BadValue => Some(PropertyValue::Text(value_to_string(value))),
        // yaml-rust2 leaves YAML aliases (`*ref`) unresolved — it
        // hands us a `Yaml::Alias(anchor_id)` without expanding to
        // the anchor target. We don't keep the anchor table around
        // long enough to resolve it ourselves, so emit a visible
        // placeholder. Without this branch the key would route
        // through the walk_value default arm and get dropped behind
        // an "unsupported YAML value type alias" warning — meaning
        // a YAML feature the user explicitly wrote vanishes from
        // the properties panel.
        Yaml::Alias(_) => Some(PropertyValue::Text(
            "<YAML alias — not resolved>".to_string(),
        )),
        _ => None,
    }
}

/// Layered inference on top of a YAML string. Order matters:
/// wikilink → datetime → date → text fallback. (Datetime checked
/// before date because `2024-01-02T03:04:05` matches the date prefix
/// too.)
fn classify_string(key: &str, s: &str) -> PropertyValue {
    if let Some(target) = wikilink_target(s) {
        return PropertyValue::Wikilink(target);
    }
    if is_datetime(s) {
        return PropertyValue::Datetime(s.to_string());
    }
    if is_date(s) {
        return PropertyValue::Date(s.to_string());
    }
    let _ = key; // reserved for future per-key inference (e.g. URL on a `url:` key)
    PropertyValue::Text(s.to_string())
}

/// Returns the bracketed inner target if `s` matches `[[target]]`
/// exactly (no surrounding whitespace, no display text). YAML
/// strings holding wikilinks are an Obsidian convention; this
/// gives the storage layer a typed bridge into #50's links table
/// without re-parsing on the read path.
fn wikilink_target(s: &str) -> Option<String> {
    let inner = s.strip_prefix("[[")?.strip_suffix("]]")?;
    if inner.is_empty() || inner.contains("\n") {
        return None;
    }
    Some(inner.to_string())
}

/// Strict `YYYY-MM-DD` check: 4 digits + `-` + 2 digits + `-` + 2
/// digits, exactly 10 characters total. Doesn't validate that the
/// date is real (Feb 31 etc.) — that's calendar arithmetic that
/// neither the YAML spec nor V1's Properties Panel needs.
fn is_date(s: &str) -> bool {
    let bytes = s.as_bytes();
    bytes.len() == 10
        && bytes[..4].iter().all(|b| b.is_ascii_digit())
        && bytes[4] == b'-'
        && bytes[5..7].iter().all(|b| b.is_ascii_digit())
        && bytes[7] == b'-'
        && bytes[8..10].iter().all(|b| b.is_ascii_digit())
}

/// ISO 8601-ish datetime: a date prefix + `T` + `HH:MM:SS`, plus an
/// optional zone suffix (`Z` or `±HH:MM`).
fn is_datetime(s: &str) -> bool {
    let bytes = s.as_bytes();
    if bytes.len() < 19 {
        return false;
    }
    if !is_date(&s[..10]) {
        return false;
    }
    if bytes[10] != b'T' {
        return false;
    }
    if !(bytes[11..13].iter().all(|b| b.is_ascii_digit())
        && bytes[13] == b':'
        && bytes[14..16].iter().all(|b| b.is_ascii_digit())
        && bytes[16] == b':'
        && bytes[17..19].iter().all(|b| b.is_ascii_digit()))
    {
        return false;
    }
    let suffix = &s[19..];
    if suffix.is_empty() || suffix == "Z" {
        return true;
    }
    // ±HH:MM
    let bytes = suffix.as_bytes();
    bytes.len() == 6
        && (bytes[0] == b'+' || bytes[0] == b'-')
        && bytes[1..3].iter().all(|b| b.is_ascii_digit())
        && bytes[3] == b':'
        && bytes[4..6].iter().all(|b| b.is_ascii_digit())
}

/// Classify a YAML list. Returns `None` if it contains a nested
/// mapping (which we don't flatten — that would lose ordering).
fn classify_list(key: &str, items: &[Yaml]) -> Option<PropertyValue> {
    // `tags:` key with a list of bare strings → TagList regardless
    // of `#` prefix. The list values may include `#`-prefixed forms;
    // strip the prefix for storage so consumers don't have to.
    let key_is_tags = key.eq_ignore_ascii_case("tags");

    let mut child_values: Vec<PropertyValue> = Vec::with_capacity(items.len());
    let mut all_strings_hash_prefixed = true;
    let mut any_string = false;

    for item in items {
        if matches!(item, Yaml::Hash(_)) {
            return None;
        }
        let classified = match item {
            Yaml::Array(_) => {
                // Lists-of-lists: collapse the inner list to text to
                // keep the outer list homogeneous-classified-as-Text.
                PropertyValue::Text(value_to_string(item))
            }
            _ => match classify_leaf(key, item) {
                Some(v) => v,
                None => PropertyValue::Text(value_to_string(item)),
            },
        };
        if let PropertyValue::Text(s) = &classified {
            any_string = true;
            if !s.starts_with('#') {
                all_strings_hash_prefixed = false;
            }
        } else {
            all_strings_hash_prefixed = false;
        }
        child_values.push(classified);
    }

    if key_is_tags {
        let tags: Vec<String> = child_values
            .iter()
            .map(|v| match v {
                PropertyValue::Text(s) => s.strip_prefix('#').unwrap_or(s).to_string(),
                other => value_to_string_value(other),
            })
            .collect();
        return Some(PropertyValue::TagList(tags));
    }

    if any_string && all_strings_hash_prefixed {
        let tags: Vec<String> = child_values
            .iter()
            .map(|v| match v {
                PropertyValue::Text(s) => s.strip_prefix('#').unwrap_or(s).to_string(),
                _ => unreachable!("guarded by all_strings_hash_prefixed"),
            })
            .collect();
        return Some(PropertyValue::TagList(tags));
    }

    // Homogeneous? If yes, return the list as-is. If mixed types,
    // collapse to a list of Text per the spec.
    let homogeneous = is_homogeneous(&child_values);
    if homogeneous {
        Some(PropertyValue::List(child_values))
    } else {
        let texts: Vec<PropertyValue> = child_values
            .into_iter()
            .map(|v| PropertyValue::Text(value_to_string_value(&v)))
            .collect();
        Some(PropertyValue::List(texts))
    }
}

fn is_homogeneous(values: &[PropertyValue]) -> bool {
    let first = match values.first() {
        Some(v) => v,
        None => return true,
    };
    values
        .iter()
        .all(|v| std::mem::discriminant(v) == std::mem::discriminant(first))
}

fn yaml_key_to_string(k: &Yaml) -> String {
    match k {
        Yaml::String(s) => s.clone(),
        other => value_to_string(other),
    }
}

fn value_to_string(value: &Yaml) -> String {
    match value {
        Yaml::String(s) => s.clone(),
        Yaml::Integer(i) => i.to_string(),
        Yaml::Real(s) => s.clone(),
        Yaml::Boolean(b) => b.to_string(),
        Yaml::Null => String::new(),
        Yaml::BadValue => "<bad>".to_string(),
        Yaml::Array(_) | Yaml::Hash(_) | Yaml::Alias(_) => format!("{value:?}"),
    }
}

fn value_to_string_value(v: &PropertyValue) -> String {
    match v {
        PropertyValue::Text(s) => s.clone(),
        PropertyValue::Integer(i) => i.to_string(),
        PropertyValue::Float(f) => f.to_string(),
        PropertyValue::Boolean(b) => b.to_string(),
        PropertyValue::Date(s) => s.clone(),
        PropertyValue::Datetime(s) => s.clone(),
        PropertyValue::Wikilink(t) => format!("[[{t}]]"),
        PropertyValue::List(items) => {
            let parts: Vec<String> = items.iter().map(value_to_string_value).collect();
            parts.join(", ")
        }
        PropertyValue::TagList(tags) => tags
            .iter()
            .map(|t| format!("#{t}"))
            .collect::<Vec<_>>()
            .join(", "),
    }
}

fn yaml_type_name(value: &Yaml) -> &'static str {
    match value {
        Yaml::Real(_) => "real",
        Yaml::Integer(_) => "integer",
        Yaml::String(_) => "string",
        Yaml::Boolean(_) => "boolean",
        Yaml::Array(_) => "array",
        Yaml::Hash(_) => "hash",
        Yaml::Alias(_) => "alias",
        Yaml::Null => "null",
        Yaml::BadValue => "bad-value",
    }
}

// --- Edit primitives for set_property / delete_property ---------------

/// Outcome of a delete-property edit on a source string.
///
/// `Unchanged` covers the cases where the requested key isn't in the
/// frontmatter (or there's no frontmatter at all) — callers short-
/// circuit on this without writing to disk.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum FrontmatterEdit {
    Unchanged,
    Changed(String),
}

/// Errors that prevent us from editing the frontmatter cleanly.
///
/// `MalformedFrontmatter` covers source-side problems (broken YAML,
/// stacked delimiters, anchors we can't preserve). `InvalidPropertyValue`
/// covers caller-side problems (a `PropertyValue` whose shape can't be
/// emitted such that the read path will round-trip it back to the
/// same variant — non-finite floats, wikilinks containing `]]`, etc.).
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum FrontmatterEditError {
    MalformedFrontmatter(String),
    InvalidPropertyValue { reason: String },
}

impl std::fmt::Display for FrontmatterEditError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            FrontmatterEditError::MalformedFrontmatter(reason) => {
                write!(f, "frontmatter is malformed: {reason}")
            }
            FrontmatterEditError::InvalidPropertyValue { reason } => {
                write!(f, "invalid property value: {reason}")
            }
        }
    }
}

impl std::error::Error for FrontmatterEditError {}

/// Insert or replace `key` → `value` in the frontmatter block, returning
/// the rewritten source.
///
/// Behavior:
///   - If `source` has no frontmatter block, prepend one carrying just
///     this key. The body byte range is left untouched.
///   - If the block exists, the YAML hash is mutated via the
///     `LinkedHashMap::replace` semantics: existing keys keep their
///     position; brand-new keys append at the end.
///   - YAML comments inside the block are stripped by the emitter —
///     `yaml-rust2`'s emitter doesn't round-trip them. The body
///     (everything after the closing `---`) is byte-identical to the
///     input.
///   - A malformed YAML block returns `MalformedFrontmatter`; we don't
///     try to merge into broken YAML.
pub fn set_property_in_source(
    source: &str,
    key: &str,
    value: &PropertyValue,
) -> Result<String, FrontmatterEditError> {
    reject_dotted_key(key)?;
    let yaml_value = property_value_to_yaml(value)?;
    let yaml_key = Yaml::String(key.to_string());

    match frontmatter_range(source) {
        None => synthesize_block(source, yaml_key, yaml_value),
        Some(range) => {
            let yaml_src = &source[range.clone()];
            let mut hash = parse_hash(yaml_src)?;
            hash.replace(yaml_key, yaml_value);
            let new_yaml = emit_hash_body(&hash);
            Ok(replace_range(source, range, &new_yaml))
        }
    }
}

/// Remove `key` from the frontmatter block. Returns `Unchanged` if the
/// key (or the whole block) isn't present.
///
/// If removing `key` empties the frontmatter, the entire `---`-bracketed
/// block is removed too — no empty frontmatter shell left behind.
///
/// As with `set_property_in_source`, a malformed YAML block returns
/// `MalformedFrontmatter`. Comments inside the block are not preserved
/// when the rest of the YAML round-trips through the emitter.
pub fn delete_property_in_source(
    source: &str,
    key: &str,
) -> Result<FrontmatterEdit, FrontmatterEditError> {
    reject_dotted_key(key)?;
    let Some(range) = frontmatter_range(source) else {
        return Ok(FrontmatterEdit::Unchanged);
    };
    let yaml_src = &source[range.clone()];
    let mut hash = parse_hash(yaml_src)?;
    let yaml_key = Yaml::String(key.to_string());
    if hash.remove(&yaml_key).is_none() {
        return Ok(FrontmatterEdit::Unchanged);
    }
    if hash.is_empty() {
        // The block is now empty — strip the whole `---<eol>…---<eol>`
        // shell. `frontmatter_range` guarantees the opening delimiter
        // starts at byte 0, so the block runs `0..closing----<eol>`.
        // The closing `---` sits at `range.end..range.end + 3`; its
        // trailing newline (LF or CRLF) follows immediately, but is
        // not always present (a file ending mid-frontmatter has no
        // trailing newline at all).
        let mut block_end = range.end + 3;
        if source.as_bytes().get(block_end) == Some(&b'\r') {
            block_end += 1;
        }
        if source.as_bytes().get(block_end) == Some(&b'\n') {
            block_end += 1;
        }
        let mut out = String::with_capacity(source.len() - block_end);
        out.push_str(&source[block_end..]);
        return Ok(FrontmatterEdit::Changed(out));
    }
    let new_yaml = emit_hash_body(&hash);
    Ok(FrontmatterEdit::Changed(replace_range(
        source, range, &new_yaml,
    )))
}

/// Reject dotted keys at the API boundary.
///
/// The read path flattens nested mappings (`person:\n  name: X` →
/// `person.name`), so a UI that surfaces dotted keys to the user
/// would otherwise naturally pass them back to `set_property` /
/// `delete_property` / `rename_property_across_vault`. The write
/// path can't drill into nested mappings — it would create a
/// duplicate top-level key alongside the original (audit #179) —
/// so we refuse at the boundary and route the caller to a different
/// flow. (Today: no flow; users hand-edit the file. Future: a
/// dedicated "edit nested property" surface.)
fn reject_dotted_key(key: &str) -> Result<(), FrontmatterEditError> {
    if key.contains('.') {
        return Err(FrontmatterEditError::InvalidPropertyValue {
            reason: format!(
                "dotted keys (e.g. {key:?}) aren't supported by the write API; \
                 the read path's dotted-key flattening isn't symmetric with the \
                 writer, and editing a nested property requires a different surface"
            ),
        });
    }
    Ok(())
}

/// Convert a `PropertyValue` to the `Yaml` representation we want the
/// emitter to write. Choices here are what controls how a property
/// round-trips through `extract_frontmatter`:
///   - `Date` / `Datetime` / `Text` → quoted/plain string, classified
///     back to the same variant by `classify_string`.
///   - `Wikilink(t)` → emitted as `"[[t]]"` so the read path
///     recognises it as a wikilink again. Targets that would be
///     ambiguous on round-trip (containing `]]`, newline, or empty)
///     are rejected with `InvalidPropertyValue` (audit #176).
///   - `Float(f)` → emitted as a decimal-form `Real`. Non-finite
///     values (NaN, ±inf) are rejected because neither
///     `yaml-rust2`'s emitter nor Rust's `f64::parse` round-trip
///     them as floats; the value would silently demote to `Text` on
///     the next read (audit #175).
///   - `List` and `TagList` always emit block style. `TagList`
///     strips a leading `#` from each tag before re-prepending one,
///     so `TagList(["#foo"])` round-trips as `TagList(["foo"])`
///     instead of growing a `##` prefix on disk (audit #180).
fn property_value_to_yaml(value: &PropertyValue) -> Result<Yaml, FrontmatterEditError> {
    Ok(match value {
        PropertyValue::Text(s) => Yaml::String(s.clone()),
        PropertyValue::Integer(i) => Yaml::Integer(*i),
        PropertyValue::Float(f) => {
            if !f.is_finite() {
                return Err(FrontmatterEditError::InvalidPropertyValue {
                    reason: format!(
                        "non-finite float ({f}) can't be safely round-tripped through YAML"
                    ),
                });
            }
            // yaml-rust2's `Real` is the string form a YAML float
            // would be written as. `f64::to_string` matches Rust's
            // canonical decimal form, which the parser will accept
            // back as `Real` and the layered classifier converts to
            // `Float` again.
            Yaml::Real(f.to_string())
        }
        PropertyValue::Boolean(b) => Yaml::Boolean(*b),
        PropertyValue::Date(s) | PropertyValue::Datetime(s) => Yaml::String(s.clone()),
        PropertyValue::Wikilink(target) => {
            if target.is_empty() {
                return Err(FrontmatterEditError::InvalidPropertyValue {
                    reason: "wikilink target is empty".to_string(),
                });
            }
            if target.contains('\n') || target.contains('\r') {
                return Err(FrontmatterEditError::InvalidPropertyValue {
                    reason: "wikilink target contains a newline".to_string(),
                });
            }
            if target.contains("]]") {
                return Err(FrontmatterEditError::InvalidPropertyValue {
                    reason: "wikilink target contains `]]` which would produce ambiguous output"
                        .to_string(),
                });
            }
            Yaml::String(format!("[[{target}]]"))
        }
        PropertyValue::List(items) => Yaml::Array(
            items
                .iter()
                .map(property_value_to_yaml)
                .collect::<Result<Vec<_>, _>>()?,
        ),
        PropertyValue::TagList(tags) => Yaml::Array(
            tags.iter()
                .map(|t| {
                    let bare = t.strip_prefix('#').unwrap_or(t);
                    Yaml::String(format!("#{bare}"))
                })
                .collect(),
        ),
    })
}

fn parse_hash(yaml_src: &str) -> Result<YamlHash, FrontmatterEditError> {
    // Audit #181: anchors and aliases get expanded inline by
    // `YamlLoader` so we can't preserve them through the round-trip.
    // Refuse rather than silently rewriting `&b shared` / `*b` into
    // two literal copies. Detection walks the Parser event stream
    // (the AST drops the anchor info, but the parser exposes it).
    detect_anchors_or_aliases(yaml_src)?;

    let docs = YamlLoader::load_from_str(yaml_src).map_err(|e| {
        FrontmatterEditError::MalformedFrontmatter(rewrite_duplicate_key_message(e.to_string()))
    })?;
    match docs.into_iter().next() {
        Some(Yaml::Hash(h)) => Ok(h),
        // Empty `---\n---` is a valid starting point for edits — treat
        // it as an empty mapping so `set_property` can populate it.
        // But "empty" YAML that the source authored as comments-only
        // (`---\n# do not edit\n---`) would silently lose those
        // comments on round-trip; refuse if any non-whitespace text
        // remains in the source slice (audit #181).
        None | Some(Yaml::Null) | Some(Yaml::BadValue) => {
            if yaml_src.chars().all(char::is_whitespace) {
                Ok(YamlHash::new())
            } else {
                Err(FrontmatterEditError::MalformedFrontmatter(
                    "frontmatter block contains only comments; editing would silently \
                     drop them. Add a real key (or delete the block) before editing properties"
                        .to_string(),
                ))
            }
        }
        Some(other) => Err(FrontmatterEditError::MalformedFrontmatter(format!(
            "frontmatter root must be a YAML mapping; got {}",
            yaml_type_name(&other)
        ))),
    }
}

/// Walk the parser event stream looking for anchor IDs or aliases.
/// Returns `Err(MalformedFrontmatter)` on the first hit. yaml-rust2's
/// AST hides anchor info (aliases are expanded inline to their target
/// value), so a successful parse alone can't tell us whether the
/// source used them.
fn detect_anchors_or_aliases(yaml_src: &str) -> Result<(), FrontmatterEditError> {
    use yaml_rust2::parser::{Event, Parser};
    let mut parser = Parser::new(yaml_src.chars());
    loop {
        let (ev, _marker) = match parser.next_token() {
            Ok(pair) => pair,
            // A scan error here resurfaces as a more user-friendly
            // MalformedFrontmatter when `YamlLoader` runs over the
            // same input below; treat anchor detection as best-
            // effort and let the loader produce the message.
            Err(_) => return Ok(()),
        };
        match ev {
            Event::Alias(_) => {
                return Err(FrontmatterEditError::MalformedFrontmatter(
                    "frontmatter uses YAML aliases (`*ref`) which the editor can't \
                     preserve through the round-trip. Inline the alias values \
                     before editing properties"
                        .to_string(),
                ));
            }
            Event::Scalar(_, _, anchor, _)
            | Event::SequenceStart(anchor, _)
            | Event::MappingStart(anchor, _)
                if anchor != 0 =>
            {
                return Err(FrontmatterEditError::MalformedFrontmatter(
                    "frontmatter uses YAML anchors (`&name`) which the editor can't \
                     preserve through the round-trip. Inline the anchor values \
                     before editing properties"
                        .to_string(),
                ));
            }
            Event::StreamEnd => return Ok(()),
            _ => {}
        }
    }
}

/// Audit #182: yaml-rust2's duplicate-key parse error renders as
/// `String("title"): duplicated key in mapping at byte N line L col C`.
/// Rewrite it to surface the offending key in a form the UI can show
/// without re-parsing. Falls through with the raw text if the message
/// shape changes in a future yaml-rust2 release.
fn rewrite_duplicate_key_message(raw: String) -> String {
    let dup_marker = "duplicated key in mapping";
    if !raw.contains(dup_marker) {
        return format!("YAML parse error: {raw}");
    }
    let prefix = "String(\"";
    if let Some(key_start) = raw.find(prefix) {
        let after_quote = &raw[key_start + prefix.len()..];
        if let Some(end_quote) = after_quote.find("\")") {
            let key = &after_quote[..end_quote];
            return format!(
                "duplicate frontmatter key `{key}`. Remove the duplicate before \
                 editing properties through this API"
            );
        }
    }
    format!("YAML parse error: {raw}")
}

/// Emit a YAML hash as a frontmatter body — i.e. without the leading
/// `---\n` that `YamlEmitter::dump` always prepends, and with a single
/// trailing newline so the closing `---` line sits flush.
fn emit_hash_body(hash: &YamlHash) -> String {
    let mut raw = String::new();
    {
        let mut emitter = YamlEmitter::new(&mut raw);
        // `dump` is infallible for our inputs (no recursion, no IO);
        // the emitter only errors on `BadValue`, which we never
        // construct.
        let _ = emitter.dump(&Yaml::Hash(hash.clone()));
    }
    // Strip the leading `---\n` document marker.
    let stripped = raw.strip_prefix("---\n").unwrap_or(&raw);
    // Ensure a trailing newline so the next line (the closing `---`)
    // sits at column 0.
    if stripped.ends_with('\n') {
        stripped.to_string()
    } else {
        format!("{stripped}\n")
    }
}

fn synthesize_block(source: &str, key: Yaml, value: Yaml) -> Result<String, FrontmatterEditError> {
    // Walk past a UTF-8 BOM if present so the synthesized block lands
    // *after* the BOM, not before it — otherwise the next reader's
    // BOM-tolerant `strip_opening_delimiter` would still find the
    // synthesized block, but external tools that don't tolerate BOM
    // would see a stray BOM in what they call the body (#173).
    let (prefix, after_bom) = match source.strip_prefix('\u{FEFF}') {
        Some(rest) => ("\u{FEFF}", rest),
        None => ("", source),
    };

    // Guard against inputs that look like a half-formed frontmatter
    // block: a leading `---\n` (or `--- \n` etc.) followed by content
    // that `frontmatter_range` couldn't pair with a closing delimiter.
    // Synthesizing a fresh block ahead of those would stack `---`
    // lines and produce visibly broken Markdown (#177).
    if looks_like_unfinished_opening_delimiter(after_bom) {
        return Err(FrontmatterEditError::MalformedFrontmatter(
            "file appears to start with a frontmatter delimiter but no closing \
             `---` line was found; fix the YAML in this note before editing properties"
                .to_string(),
        ));
    }

    let mut hash = YamlHash::new();
    hash.insert(key, value);
    let body = emit_hash_body(&hash);
    // Prepend `---\n<body>---\n` to the source. Body already ends with
    // `\n`, so the closing `---\n` lines up cleanly.
    let mut out = String::with_capacity(source.len() + body.len() + 8);
    out.push_str(prefix);
    out.push_str("---\n");
    out.push_str(&body);
    out.push_str("---\n");
    out.push_str(after_bom);
    Ok(out)
}

/// `true` when `s` opens with a frontmatter-style `---<whitespace>*\n`
/// line. Mirrors the shape `strip_opening_delimiter` accepts so the
/// synthesize-block guard rejects exactly the inputs the
/// `frontmatter_range`-returning-None path would otherwise stack a
/// duplicate `---` ahead of.
fn looks_like_unfinished_opening_delimiter(s: &str) -> bool {
    let Some(after_dashes) = s.strip_prefix("---") else {
        return false;
    };
    let Some(line_end) = after_dashes.find('\n') else {
        return false;
    };
    after_dashes[..line_end].chars().all(char::is_whitespace)
}

fn replace_range(source: &str, range: Range<usize>, replacement: &str) -> String {
    let mut out = String::with_capacity(source.len() - range.len() + replacement.len());
    out.push_str(&source[..range.start]);
    out.push_str(replacement);
    out.push_str(&source[range.end..]);
    out
}

#[cfg(test)]
mod tests {
    use super::*;

    fn props(source: &str) -> Vec<Property> {
        extract_frontmatter(source).0
    }

    fn find(props: &[Property], key: &str) -> Option<PropertyValue> {
        props.iter().find(|p| p.key == key).map(|p| p.value.clone())
    }

    // --- Detection ---

    #[test]
    fn no_frontmatter_returns_empty() {
        let (p, w) = extract_frontmatter("# Heading\n\nbody only");
        assert!(p.is_empty());
        assert!(w.is_empty());
    }

    #[test]
    fn leading_whitespace_before_delim_disqualifies() {
        // The delimiter must be at byte 0. A blank line before `---`
        // means the file's actually plain Markdown that happens to
        // start with an `<hr>`.
        let (p, w) = extract_frontmatter(" ---\nkey: value\n---\n");
        assert!(p.is_empty());
        assert!(w.is_empty());
    }

    #[test]
    fn detects_empty_frontmatter_block() {
        let (p, w) = extract_frontmatter("---\n---\nbody\n");
        assert!(p.is_empty());
        assert!(w.is_empty());
    }

    #[test]
    fn detects_crlf_opening_delim() {
        let (p, _) = extract_frontmatter("---\r\nkey: value\r\n---\r\nbody");
        assert_eq!(
            find(&p, "key"),
            Some(PropertyValue::Text("value".to_string()))
        );
    }

    // --- Scalars ---

    #[test]
    fn parses_text_scalar() {
        let p = props("---\ntitle: My Note\n---\n");
        assert_eq!(
            find(&p, "title"),
            Some(PropertyValue::Text("My Note".to_string()))
        );
    }

    #[test]
    fn parses_integer_vs_float() {
        // Using 3.5 (not 3.14) to dodge clippy's approx_constant check
        // for pi — same coverage of float parsing without the lint
        // collision.
        let p = props("---\nint_val: 42\nfloat_val: 3.5\n---\n");
        assert_eq!(find(&p, "int_val"), Some(PropertyValue::Integer(42)));
        match find(&p, "float_val") {
            Some(PropertyValue::Float(f)) => assert!((f - 3.5).abs() < 1e-9),
            other => panic!("expected Float, got {other:?}"),
        }
    }

    #[test]
    fn parses_boolean() {
        let p = props("---\npublished: true\ndraft: false\n---\n");
        assert_eq!(find(&p, "published"), Some(PropertyValue::Boolean(true)));
        assert_eq!(find(&p, "draft"), Some(PropertyValue::Boolean(false)));
    }

    #[test]
    fn parses_date_and_datetime_distinctly() {
        let p = props(
            "---\ncreated: 2024-01-02\nupdated: 2024-01-02T03:04:05\nfixed: 2024-01-02T03:04:05Z\n---\n",
        );
        assert_eq!(
            find(&p, "created"),
            Some(PropertyValue::Date("2024-01-02".to_string()))
        );
        assert_eq!(
            find(&p, "updated"),
            Some(PropertyValue::Datetime("2024-01-02T03:04:05".to_string()))
        );
        assert_eq!(
            find(&p, "fixed"),
            Some(PropertyValue::Datetime("2024-01-02T03:04:05Z".to_string()))
        );
    }

    #[test]
    fn parses_datetime_with_offset() {
        let p = props("---\nat: \"2024-01-02T03:04:05+02:00\"\n---\n");
        assert_eq!(
            find(&p, "at"),
            Some(PropertyValue::Datetime(
                "2024-01-02T03:04:05+02:00".to_string()
            ))
        );
    }

    #[test]
    fn parses_embedded_wikilink() {
        let p = props("---\nrelated: \"[[Alpha]]\"\n---\n");
        assert_eq!(
            find(&p, "related"),
            Some(PropertyValue::Wikilink("Alpha".to_string()))
        );
    }

    // --- Lists ---

    #[test]
    fn parses_homogeneous_list_of_text() {
        let p = props("---\nauthors:\n  - alice\n  - bob\n---\n");
        match find(&p, "authors") {
            Some(PropertyValue::List(items)) => {
                assert_eq!(items.len(), 2);
                assert_eq!(items[0], PropertyValue::Text("alice".to_string()));
                assert_eq!(items[1], PropertyValue::Text("bob".to_string()));
            }
            other => panic!("expected List, got {other:?}"),
        }
    }

    #[test]
    fn mixed_type_list_collapses_to_list_of_text() {
        let p = props("---\nstuff:\n  - 1\n  - hello\n  - true\n---\n");
        match find(&p, "stuff") {
            Some(PropertyValue::List(items)) => {
                assert_eq!(items.len(), 3);
                assert!(matches!(items[0], PropertyValue::Text(_)));
                assert!(matches!(items[1], PropertyValue::Text(_)));
                assert!(matches!(items[2], PropertyValue::Text(_)));
            }
            other => panic!("expected List, got {other:?}"),
        }
    }

    #[test]
    fn tags_key_produces_tag_list_even_without_hash_prefix() {
        let p = props("---\ntags:\n  - alpha\n  - beta\n---\n");
        assert_eq!(
            find(&p, "tags"),
            Some(PropertyValue::TagList(vec![
                "alpha".to_string(),
                "beta".to_string()
            ]))
        );
    }

    #[test]
    fn tag_list_strips_hash_prefix() {
        let p = props("---\ntags:\n  - \"#alpha\"\n  - \"#beta\"\n---\n");
        assert_eq!(
            find(&p, "tags"),
            Some(PropertyValue::TagList(vec![
                "alpha".to_string(),
                "beta".to_string()
            ]))
        );
    }

    #[test]
    fn list_of_hash_prefixed_strings_under_other_key_becomes_tag_list() {
        let p = props("---\ntopics:\n  - \"#science\"\n  - \"#math\"\n---\n");
        assert_eq!(
            find(&p, "topics"),
            Some(PropertyValue::TagList(vec![
                "science".to_string(),
                "math".to_string()
            ]))
        );
    }

    // --- Nested + Unicode keys ---

    #[test]
    fn nested_object_flattens_to_dotted_keys() {
        let p = props("---\nperson:\n  name: Alice\n  role: author\n---\n");
        assert_eq!(
            find(&p, "person.name"),
            Some(PropertyValue::Text("Alice".to_string()))
        );
        assert_eq!(
            find(&p, "person.role"),
            Some(PropertyValue::Text("author".to_string()))
        );
        // No bare `person` key in the output — only the flattened leaves.
        assert!(find(&p, "person").is_none());
    }

    #[test]
    fn deeply_nested_object_flattens_recursively() {
        let p = props("---\na:\n  b:\n    c: deep\n---\n");
        assert_eq!(
            find(&p, "a.b.c"),
            Some(PropertyValue::Text("deep".to_string()))
        );
    }

    #[test]
    fn unicode_keys_preserved_verbatim() {
        let p = props("---\nプロジェクト: 値\n作者: 山田\n---\n");
        assert_eq!(
            find(&p, "プロジェクト"),
            Some(PropertyValue::Text("値".to_string()))
        );
        assert_eq!(
            find(&p, "作者"),
            Some(PropertyValue::Text("山田".to_string()))
        );
    }

    // --- Malformed YAML ---

    #[test]
    fn malformed_yaml_produces_warning_without_crashing() {
        // Tab indentation is invalid YAML and yaml-rust2 rejects it.
        // The contract documented at the top of this module is
        // whole-document parse: a syntax error anywhere aborts the
        // parse and yields empty props + a single warning. (We do
        // NOT do a per-line shrink + retry — that was the previous
        // doc comment's claim and it was wrong.)
        let (props, warnings) = extract_frontmatter("---\nkey:\n\t- value\n---\n");
        assert!(
            props.is_empty(),
            "malformed YAML must not yield partial props"
        );
        assert_eq!(warnings.len(), 1);
        assert!(
            warnings[0].message.contains("YAML parse error"),
            "expected a YAML parse error warning, got {:?}",
            warnings[0].message
        );
    }

    #[test]
    fn unbalanced_quote_yields_empty_props_with_warning() {
        // Second malformed-YAML shape: an unterminated string. Same
        // contract as the tab-indent case — empty props + warning,
        // no panic, no partial result.
        let (props, warnings) = extract_frontmatter("---\nkey: \"unterminated\n---\n");
        assert!(props.is_empty());
        assert!(!warnings.is_empty());
    }

    // --- Depth limit ---

    #[test]
    fn nesting_past_depth_limit_records_warning_and_stops_descending() {
        // Build a YAML mapping nested past MAX_WALK_DEPTH. The walk
        // must record a warning at the boundary and return rather
        // than recurse the rest of the way — synced vaults can
        // contain hostile or generated files with pathological
        // shapes, and a stack overflow during indexing would take
        // down the whole scanner.
        let depth = MAX_WALK_DEPTH + 3;
        let mut source = String::from("---\n");
        for i in 0..depth {
            source.push_str(&"  ".repeat(i));
            source.push_str(&format!("k{i}:\n"));
        }
        source.push_str(&"  ".repeat(depth));
        source.push_str("leaf: deep\n");
        source.push_str("---\n");
        let (_props, warnings) = extract_frontmatter(&source);
        let nesting: Vec<_> = warnings
            .iter()
            .filter(|w| w.message.contains("nesting exceeds"))
            .collect();
        assert_eq!(
            nesting.len(),
            1,
            "expected exactly one nesting-depth warning, got {nesting:?}"
        );
    }

    #[test]
    fn wide_map_at_depth_boundary_emits_one_warning_not_per_child() {
        // Regression for Codoki PR 95 medium: with the old guard
        // (top-level `depth > MAX_WALK_DEPTH` after the recursive
        // call), a wide map sitting AT the boundary would recurse
        // into each child at depth+1 and emit one warning per
        // child — flooding the warnings list for a fan-out shape.
        // The boundary guard now lives on the parent Hash and
        // emits exactly one warning regardless of fan-out.
        //
        // Build a chain of MAX_WALK_DEPTH+1 nested maps so the
        // deepest Hash lands at depth=MAX_WALK_DEPTH (=32), where
        // the new guard fires.
        let chain = MAX_WALK_DEPTH + 1;
        let mut source = String::from("---\n");
        for i in 0..chain {
            source.push_str(&"  ".repeat(i));
            source.push_str(&format!("k{i}:\n"));
        }
        // 10 wide children under the deepest Hash. Each would
        // have tripped its own warning under the old guard.
        for j in 0..10 {
            source.push_str(&"  ".repeat(chain));
            source.push_str(&format!("w{j}: x\n"));
        }
        source.push_str("---\n");
        let (_props, warnings) = extract_frontmatter(&source);
        let nesting: Vec<_> = warnings
            .iter()
            .filter(|w| w.message.contains("nesting exceeds"))
            .collect();
        assert_eq!(
            nesting.len(),
            1,
            "wide map at the boundary must emit one warning, not one per child; got {nesting:?}"
        );
    }

    // --- YAML aliases ---

    #[test]
    fn yaml_alias_key_survives_with_placeholder() {
        // yaml-rust2 hands us `Yaml::Alias(id)` for `*ref` rather
        // than expanding the anchor. Before this branch the key
        // dropped silently behind an "unsupported value type alias"
        // warning — a YAML feature the author wrote intentionally
        // shouldn't vanish from the properties panel. We emit a
        // visible placeholder instead.
        let (p, _) = extract_frontmatter("---\nbase: &b anchor-value\nref: *b\n---\n");
        // The anchored value parses normally.
        assert!(find(&p, "base").is_some());
        // The alias-bearing key survives. yaml-rust2 may or may not
        // resolve the alias internally — both outcomes are fine, the
        // invariant is "the key isn't dropped."
        assert!(
            find(&p, "ref").is_some(),
            "alias-bearing key was dropped from properties"
        );
    }

    #[test]
    fn root_array_produces_warning() {
        let (p, w) = extract_frontmatter("---\n- not a mapping\n- still not\n---\n");
        assert!(p.is_empty());
        assert_eq!(w.len(), 1);
        assert!(w[0].message.contains("mapping"));
    }

    // --- Range detection ---

    #[test]
    fn yaml_real_edge_values_fall_back_to_text() {
        // Codoki callout on PR 81: yaml-rust2 marks `.nan` / `.inf` /
        // `-.inf` as `Real` scalars that may not round-trip through
        // f64.parse cleanly. We treat them as Text so the property
        // stays visible rather than getting silently dropped behind
        // a warning.
        let p = props("---\nnan_val: .nan\ninf_val: .inf\nneg_inf: -.inf\n---\n");
        // We don't care whether yaml-rust2 routes these through
        // Real or Text — only that they end up in the props vec
        // with a known value, not as a warning.
        for key in ["nan_val", "inf_val", "neg_inf"] {
            assert!(
                find(&p, key).is_some(),
                "expected {key} to be classified, got nothing"
            );
        }
    }

    #[test]
    fn yaml_real_with_exponent_parses_as_float() {
        let p = props("---\nscientific: 1e-3\n---\n");
        match find(&p, "scientific") {
            Some(PropertyValue::Float(f)) => assert!((f - 0.001).abs() < 1e-12),
            other => panic!("expected Float, got {other:?}"),
        }
    }

    #[test]
    fn opening_delimiter_tolerates_trailing_whitespace() {
        // #93 item 4: the opening delimiter parse was strict
        // (`---\n` / `---\r\n` only) while the closing parse
        // accepted any-whitespace-then-newline via
        // `trailing.trim().is_empty()`. A file with `--- \n` at
        // byte 0 looked like "no frontmatter" while the same
        // trailing-space pattern on the close was accepted.
        // Both sides now share the same tolerance.
        let (p, _) = extract_frontmatter("--- \nkey: value\n---\n");
        assert_eq!(
            find(&p, "key"),
            Some(PropertyValue::Text("value".to_string()))
        );
        let (p, _) = extract_frontmatter("---\t\nkey: value\n---\n");
        assert_eq!(
            find(&p, "key"),
            Some(PropertyValue::Text("value".to_string()))
        );
        let (p, _) = extract_frontmatter("--- \r\nkey: value\r\n---\r\n");
        assert_eq!(
            find(&p, "key"),
            Some(PropertyValue::Text("value".to_string()))
        );
    }

    #[test]
    fn opening_delimiter_rejects_non_whitespace_trailing_chars() {
        // The tolerance is for whitespace only — `---xyz\n` is still
        // a regular markdown line, not a frontmatter opening.
        let (p, w) = extract_frontmatter("---xyz\nkey: value\n---\n");
        assert!(p.is_empty());
        assert!(w.is_empty());
    }

    #[test]
    fn bare_triple_dash_at_eof_is_not_a_frontmatter_opening() {
        // Documents the explicit rejection: `---` without a trailing
        // newline isn't a usable opening (no body can follow it).
        assert!(frontmatter_range("---").is_none());
        assert!(frontmatter_range("---abc").is_none());
    }

    #[test]
    fn frontmatter_range_returns_yaml_byte_range() {
        let src = "---\nkey: value\n---\nbody starts here";
        let range = frontmatter_range(src).unwrap();
        assert_eq!(&src[range], "key: value\n");
    }

    #[test]
    fn body_after_frontmatter_returns_post_block_slice() {
        let src = "---\nkey: value\n---\nbody text\n";
        assert_eq!(body_after_frontmatter(src), "body text\n");
    }

    #[test]
    fn body_after_frontmatter_handles_crlf_after_closing_delim() {
        let src = "---\r\nkey: value\r\n---\r\nbody text\r\n";
        // `frontmatter_range` is line-ending-tolerant only on the
        // closing-delimiter trim, so the CRLF after the closing
        // `---` is consumed by the trailing newline strip.
        assert_eq!(body_after_frontmatter(src), "body text\r\n");
    }

    #[test]
    fn body_after_frontmatter_passes_through_when_no_frontmatter() {
        let src = "# just a body\nno frontmatter here\n";
        assert_eq!(body_after_frontmatter(src), src);
    }

    #[test]
    fn body_after_frontmatter_passes_through_when_open_without_close() {
        // Mid-edit shape: opening `---` but no closing delimiter
        // anywhere — frontmatter_range returns None, so the body
        // helper is a no-op and we keep all the user's text visible.
        let src = "---\nstill writing\nno close yet\n";
        assert_eq!(body_after_frontmatter(src), src);
    }

    #[test]
    fn frontmatter_range_handles_missing_close() {
        // Opening delimiter but no close → treated as no frontmatter.
        // The whole file is body.
        let src = "---\nkey: value\nno close here";
        assert!(frontmatter_range(src).is_none());
    }

    // --- set_property_in_source / delete_property_in_source ---

    #[test]
    fn set_property_inserts_new_key_into_existing_frontmatter() {
        let src = "---\ntitle: Hello\n---\nbody\n";
        let out = set_property_in_source(src, "author", &PropertyValue::Text("Cory".to_string()))
            .unwrap();
        let p = props(&out);
        assert_eq!(
            find(&p, "title"),
            Some(PropertyValue::Text("Hello".to_string()))
        );
        assert_eq!(
            find(&p, "author"),
            Some(PropertyValue::Text("Cory".to_string()))
        );
    }

    #[test]
    fn set_property_appends_new_key_at_end_not_alphabetical() {
        let src = "---\nzebra: 1\nalpha: 2\n---\nbody\n";
        let out = set_property_in_source(src, "middle", &PropertyValue::Integer(99)).unwrap();
        // Read keys back in document order.
        let p = props(&out);
        let order: Vec<&str> = p.iter().map(|p| p.key.as_str()).collect();
        assert_eq!(order, vec!["zebra", "alpha", "middle"]);
    }

    #[test]
    fn set_property_updates_existing_key_in_place() {
        let src = "---\nzebra: 1\nalpha: 2\nomega: 3\n---\nbody\n";
        let out = set_property_in_source(src, "alpha", &PropertyValue::Integer(42)).unwrap();
        let p = props(&out);
        let order: Vec<&str> = p.iter().map(|p| p.key.as_str()).collect();
        // Order preserved — `alpha` didn't migrate to the end.
        assert_eq!(order, vec!["zebra", "alpha", "omega"]);
        assert_eq!(find(&p, "alpha"), Some(PropertyValue::Integer(42)));
    }

    #[test]
    fn set_property_synthesizes_block_when_no_frontmatter() {
        let src = "# Note\n\nBody text.\n";
        let out =
            set_property_in_source(src, "title", &PropertyValue::Text("Hi".to_string())).unwrap();
        assert!(out.starts_with("---\n"));
        assert!(out.ends_with("# Note\n\nBody text.\n"));
        let p = props(&out);
        assert_eq!(
            find(&p, "title"),
            Some(PropertyValue::Text("Hi".to_string()))
        );
    }

    #[test]
    fn set_property_keeps_body_byte_identical() {
        // The byte range after the closing `---\n` must round-trip
        // through `set_property_in_source` untouched. This is the
        // load-bearing guarantee: editing frontmatter never disturbs
        // note content.
        let src =
            "---\ntitle: Old\n---\n# Heading\n\nParagraph with **bold** and `code`.\n\n- list item\n";
        let body = &src["---\ntitle: Old\n---\n".len()..];
        let out =
            set_property_in_source(src, "title", &PropertyValue::Text("New".to_string())).unwrap();
        assert!(out.ends_with(body));
    }

    #[test]
    fn set_property_round_trips_each_value_variant() {
        let src = "---\nplaceholder: x\n---\nbody\n";
        let cases: Vec<(&str, PropertyValue)> = vec![
            ("text_k", PropertyValue::Text("hello world".to_string())),
            ("int_k", PropertyValue::Integer(-7)),
            ("float_k", PropertyValue::Float(3.5)),
            ("bool_k", PropertyValue::Boolean(true)),
            ("date_k", PropertyValue::Date("2026-05-24".to_string())),
            (
                "dt_k",
                PropertyValue::Datetime("2026-05-24T10:00:00Z".to_string()),
            ),
            ("wiki_k", PropertyValue::Wikilink("Note Title".to_string())),
            (
                "list_k",
                PropertyValue::List(vec![
                    PropertyValue::Text("a".to_string()),
                    PropertyValue::Text("b".to_string()),
                ]),
            ),
            (
                "tags",
                PropertyValue::TagList(vec!["foo".to_string(), "bar".to_string()]),
            ),
        ];
        let mut current = src.to_string();
        for (k, v) in &cases {
            current = set_property_in_source(&current, k, v).unwrap();
        }
        let p = props(&current);
        for (k, v) in &cases {
            assert_eq!(
                find(&p, k).as_ref(),
                Some(v),
                "round-trip failed for key {k}"
            );
        }
    }

    #[test]
    fn set_property_on_malformed_yaml_returns_error() {
        // Unterminated string — yaml-rust2 rejects the parse.
        let src = "---\ntitle: \"unterminated\n---\nbody\n";
        let err = set_property_in_source(src, "author", &PropertyValue::Text("x".to_string()))
            .unwrap_err();
        assert!(matches!(err, FrontmatterEditError::MalformedFrontmatter(_)));
    }

    #[test]
    fn delete_property_missing_key_returns_unchanged() {
        let src = "---\ntitle: Hi\n---\nbody\n";
        assert_eq!(
            delete_property_in_source(src, "author").unwrap(),
            FrontmatterEdit::Unchanged
        );
    }

    #[test]
    fn delete_property_no_frontmatter_returns_unchanged() {
        let src = "# Note\n\nBody.\n";
        assert_eq!(
            delete_property_in_source(src, "anything").unwrap(),
            FrontmatterEdit::Unchanged
        );
    }

    #[test]
    fn delete_property_removes_one_of_many_keys_in_place() {
        let src = "---\ntitle: Hi\nauthor: Cory\nyear: 2026\n---\nbody\n";
        let out = match delete_property_in_source(src, "author").unwrap() {
            FrontmatterEdit::Changed(s) => s,
            other => panic!("expected Changed, got {other:?}"),
        };
        let p = props(&out);
        let order: Vec<&str> = p.iter().map(|p| p.key.as_str()).collect();
        assert_eq!(order, vec!["title", "year"]);
        assert!(out.ends_with("body\n"));
    }

    #[test]
    fn delete_property_on_last_key_strips_whole_block() {
        let src = "---\ntitle: Hi\n---\nbody\n";
        let out = match delete_property_in_source(src, "title").unwrap() {
            FrontmatterEdit::Changed(s) => s,
            other => panic!("expected Changed, got {other:?}"),
        };
        // No `---` block left at all.
        assert_eq!(out, "body\n");
    }

    #[test]
    fn delete_property_on_empty_frontmatter_returns_unchanged() {
        // `---\n---\n` is technically a frontmatter block with zero
        // keys. Deleting any key from it is unchanged.
        let src = "---\n---\nbody\n";
        assert_eq!(
            delete_property_in_source(src, "anything").unwrap(),
            FrontmatterEdit::Unchanged
        );
    }

    #[test]
    fn delete_property_strips_block_with_crlf_line_endings() {
        // Same shape as the LF case but with Windows line endings on
        // the frontmatter delimiters. The `\r\n` after the closing
        // `---` must also be consumed.
        let src = "---\r\ntitle: Hi\r\n---\r\nbody\n";
        let out = match delete_property_in_source(src, "title").unwrap() {
            FrontmatterEdit::Changed(s) => s,
            other => panic!("expected Changed, got {other:?}"),
        };
        assert_eq!(out, "body\n");
    }

    #[test]
    fn delete_property_keeps_body_byte_identical() {
        let src = "---\ntitle: Hi\nauthor: Cory\n---\n# Heading\n\nParagraph.\n";
        let body = &src["---\ntitle: Hi\nauthor: Cory\n---\n".len()..];
        let out = match delete_property_in_source(src, "author").unwrap() {
            FrontmatterEdit::Changed(s) => s,
            other => panic!("expected Changed, got {other:?}"),
        };
        assert!(out.ends_with(body));
    }

    // --- Audit fixes -------------------------------------------------

    #[test]
    fn set_property_tolerates_leading_bom_and_edits_existing_block() {
        // Audit #173: a UTF-8 BOM ahead of `---` was making
        // frontmatter_range return None, so set_property synthesized
        // a duplicate block ahead of the BOM and silently shadowed
        // the original frontmatter. With BOM tolerance, the existing
        // block is detected and edited in place.
        let src = "\u{FEFF}---\ntitle: Original\n---\nbody\n";
        let out = set_property_in_source(src, "year", &PropertyValue::Integer(2026)).unwrap();
        let (props, _) = extract_frontmatter(&out);
        let keys: Vec<&str> = props.iter().map(|p| p.key.as_str()).collect();
        assert_eq!(keys, vec!["title", "year"]);
        // BOM is preserved at byte 0.
        assert!(out.starts_with('\u{FEFF}'));
        // Body bytes after the closing `---\n` are byte-identical.
        assert!(out.ends_with("body\n"));
    }

    #[test]
    fn set_property_synthesizes_after_bom_for_plain_markdown() {
        // BOM-prefixed file with no frontmatter still gets a
        // synthesized block — but the BOM stays at byte 0 so external
        // tools and BOM-tolerant readers agree on where the
        // frontmatter starts.
        let src = "\u{FEFF}# Note\n\nbody\n";
        let out = set_property_in_source(src, "title", &PropertyValue::Text("Hi".into())).unwrap();
        assert!(out.starts_with("\u{FEFF}---\n"));
        let (props, _) = extract_frontmatter(&out);
        let keys: Vec<&str> = props.iter().map(|p| p.key.as_str()).collect();
        assert_eq!(keys, vec!["title"]);
    }

    #[test]
    fn set_property_refuses_to_stack_delimiters_on_half_formed_frontmatter() {
        // Audit #177: source already starts with `---\n` but the
        // closing delimiter isn't where frontmatter_range expects it.
        // Synthesizing a fresh block would produce stacked `---`
        // lines and visibly broken Markdown — refuse instead.
        for src in [
            "---\nfoo bar\n",         // missing close
            "---\n---\nbody\n",       // empty-block-no-close shape
            "\u{FEFF}---\nfoo bar\n", // same with BOM
        ] {
            let err =
                set_property_in_source(src, "title", &PropertyValue::Text("X".into())).unwrap_err();
            assert!(
                matches!(err, FrontmatterEditError::MalformedFrontmatter(_)),
                "expected MalformedFrontmatter for {src:?}, got {err:?}"
            );
        }
    }

    #[test]
    fn delete_property_tolerates_leading_bom() {
        let src = "\u{FEFF}---\ntitle: Hi\nauthor: Cory\n---\nbody\n";
        let out = match delete_property_in_source(src, "author").unwrap() {
            FrontmatterEdit::Changed(s) => s,
            other => panic!("expected Changed, got {other:?}"),
        };
        assert!(out.starts_with('\u{FEFF}'));
        let (props, _) = extract_frontmatter(&out);
        let keys: Vec<&str> = props.iter().map(|p| p.key.as_str()).collect();
        assert_eq!(keys, vec!["title"]);
    }

    #[test]
    fn set_property_refuses_non_finite_floats() {
        // Audit #175: NaN/inf/-inf would round-trip from Float to
        // Text (yaml-rust2 produces Real(".nan") which Rust's
        // f64::parse rejects, falling back to PropertyValue::Text).
        // Refuse at emit time instead.
        let src = "---\np: x\n---\nbody\n";
        for f in [f64::NAN, f64::INFINITY, f64::NEG_INFINITY] {
            let err = set_property_in_source(src, "nv", &PropertyValue::Float(f)).unwrap_err();
            assert!(
                matches!(err, FrontmatterEditError::InvalidPropertyValue { .. }),
                "expected InvalidPropertyValue for {f}, got {err:?}"
            );
        }
        // A list containing one bad float is rejected as a whole —
        // the previous behavior demoted every element of the list to
        // Text on round-trip (audit #175 cascade).
        let bad_list = PropertyValue::List(vec![
            PropertyValue::Float(f64::NAN),
            PropertyValue::Float(1.25),
        ]);
        assert!(matches!(
            set_property_in_source(src, "vals", &bad_list).unwrap_err(),
            FrontmatterEditError::InvalidPropertyValue { .. }
        ));
    }

    #[test]
    fn set_property_refuses_invalid_wikilink_targets() {
        // Audit #176: empty / newline / `]]` targets either silently
        // demote on round-trip (empty, newline) or produce ambiguous
        // output (`]]`). All three rejected at emit time.
        let src = "---\np: x\n---\nbody\n";
        for target in ["", "a\nb", "evil]]injected", "trail\r"] {
            let err =
                set_property_in_source(src, "link", &PropertyValue::Wikilink(target.to_string()))
                    .unwrap_err();
            assert!(
                matches!(err, FrontmatterEditError::InvalidPropertyValue { .. }),
                "expected InvalidPropertyValue for target {target:?}, got {err:?}"
            );
        }
        // Valid target still round-trips.
        let out = set_property_in_source(
            src,
            "link",
            &PropertyValue::Wikilink("Plain Note".to_string()),
        )
        .unwrap();
        let (props, _) = extract_frontmatter(&out);
        let link = props.iter().find(|p| p.key == "link").unwrap();
        assert_eq!(
            link.value,
            PropertyValue::Wikilink("Plain Note".to_string())
        );
    }

    #[test]
    fn set_property_refuses_to_overwrite_anchors_and_aliases() {
        // Audit #181: yaml-rust2's loader expands anchors and
        // aliases inline. Round-tripping a frontmatter that uses
        // them would silently turn `&b shared` + `*b` into two
        // literal `shared` copies. Refuse instead.
        let src = "---\nbase: &b shared\nref: *b\nplain: keep\n---\nbody\n";
        let err = set_property_in_source(src, "year", &PropertyValue::Integer(2026)).unwrap_err();
        assert!(matches!(err, FrontmatterEditError::MalformedFrontmatter(_)));

        // Just an anchor, no alias use, still refused — round-trip
        // would still drop the anchor token from disk.
        let src = "---\nbase: &b shared\nplain: keep\n---\nbody\n";
        let err = set_property_in_source(src, "year", &PropertyValue::Integer(2026)).unwrap_err();
        assert!(matches!(err, FrontmatterEditError::MalformedFrontmatter(_)));
    }

    #[test]
    fn set_property_refuses_to_clobber_comments_only_block() {
        // Audit #181: a frontmatter consisting entirely of comments
        // parses to an empty hash. Without a guard, set_property
        // would synthesize a fresh block carrying just the new key
        // and silently lose every comment line.
        let src = "---\n# IMPORTANT: machine-managed\n# generated 2026-05-24\n---\nbody\n";
        let err =
            set_property_in_source(src, "title", &PropertyValue::Text("New".into())).unwrap_err();
        assert!(matches!(err, FrontmatterEditError::MalformedFrontmatter(_)));
    }

    #[test]
    fn set_property_duplicate_key_error_names_the_key() {
        // Audit #182: yaml-rust2's raw duplicate-key error is hard
        // to act on. Rewrite it to call out the offending key by
        // name so the UI can present it without re-parsing.
        let src = "---\ntitle: First\ntitle: Second\n---\nbody\n";
        let err = set_property_in_source(src, "year", &PropertyValue::Integer(2026)).unwrap_err();
        match err {
            FrontmatterEditError::MalformedFrontmatter(msg) => {
                assert!(
                    msg.contains("duplicate frontmatter key `title`"),
                    "expected duplicate-key message naming `title`, got {msg:?}"
                );
            }
            other => panic!("expected MalformedFrontmatter, got {other:?}"),
        }
    }

    #[test]
    fn set_property_rejects_dotted_keys() {
        // Audit #179: the reader flattens nested mappings to dotted
        // keys (`person:\n  name: X` → `person.name`). The writer
        // can't drill into the mapping, so a dotted key would create
        // a duplicate top-level entry. Refuse at the boundary.
        let src = "---\nperson:\n  name: Original\n---\nbody\n";
        let err = set_property_in_source(src, "person.name", &PropertyValue::Text("Y".into()))
            .unwrap_err();
        assert!(matches!(
            err,
            FrontmatterEditError::InvalidPropertyValue { .. }
        ));

        // Same rejection on a plain-flat file — the rule is at the
        // API boundary, not contingent on the source shape.
        let src = "---\ntitle: Hi\n---\nbody\n";
        let err = set_property_in_source(src, "a.b", &PropertyValue::Text("X".into())).unwrap_err();
        assert!(matches!(
            err,
            FrontmatterEditError::InvalidPropertyValue { .. }
        ));

        // delete_property symmetric.
        let err = delete_property_in_source(src, "a.b").unwrap_err();
        assert!(matches!(
            err,
            FrontmatterEditError::InvalidPropertyValue { .. }
        ));
    }

    #[test]
    fn set_property_taglist_strips_existing_hash_before_re_prefixing() {
        // Audit #180A: TagList(["#foo"]) was emitting as `- "##foo"`
        // on disk. Round-trip preserved the type but disk content
        // grew an extra `#` the user didn't author.
        let src = "---\np: x\n---\nbody\n";
        let out = set_property_in_source(
            src,
            "tags",
            &PropertyValue::TagList(vec!["#leading".to_string()]),
        )
        .unwrap();
        // The on-disk form has a single `#` prefix.
        assert!(
            out.contains("- \"#leading\"") || out.contains("- '#leading'"),
            "expected single-# emit, got {out:?}"
        );
        assert!(!out.contains("##"), "got double-# in {out:?}");
        // Round-trips as the same tag value.
        let (props, _) = extract_frontmatter(&out);
        let tags = props.iter().find(|p| p.key == "tags").unwrap();
        assert_eq!(
            tags.value,
            PropertyValue::TagList(vec!["leading".to_string()])
        );
    }
}
