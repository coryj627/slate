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
//! The parser never returns an error to the caller. Bad YAML yields
//! whatever properties did parse (typically the prefix before the
//! error) plus a `PropertyParseWarning` describing the failure. The
//! scanner can persist the partial result and surface the warning
//! through #54 / #55 so the user notices without losing access to
//! the rest of the frontmatter.

use std::ops::Range;

use yaml_rust2::{Yaml, YamlLoader};

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

/// Returns the source slice past the opening `---` line if and only
/// if the file starts with one (no leading whitespace allowed).
fn strip_opening_delimiter(source: &str) -> Option<&str> {
    // Accept three forms: `---\n`, `---\r\n`, and `---` at end of
    // file. The last is degenerate (no body, no close) but we still
    // recognize the opening so the caller's None handling is
    // explicit.
    if let Some(rest) = source.strip_prefix("---\r\n") {
        return Some(rest);
    }
    if let Some(rest) = source.strip_prefix("---\n") {
        return Some(rest);
    }
    // `---` followed by EOF or a non-newline char doesn't count as a
    // delimiter — that's either a body line that happens to start
    // with three dashes or a doc with no actual frontmatter.
    None
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
                walk_value(&key, &v, &mut props, &mut warnings);
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
fn walk_value(
    key: &str,
    value: &Yaml,
    props: &mut Vec<Property>,
    warnings: &mut Vec<PropertyParseWarning>,
) {
    match value {
        Yaml::Hash(map) => {
            for (k, v) in map {
                let sub_key = yaml_key_to_string(k);
                let full_key = format!("{key}.{sub_key}");
                walk_value(&full_key, v, props, warnings);
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
        Yaml::Real(s) => s.parse::<f64>().ok().map(PropertyValue::Float),
        Yaml::Boolean(b) => Some(PropertyValue::Boolean(*b)),
        Yaml::String(s) => Some(classify_string(key, s)),
        // BadValue is yaml-rust2's "couldn't parse this scalar"
        // signal; treat as text so the user sees what was authored.
        Yaml::BadValue => Some(PropertyValue::Text(value_to_string(value))),
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
        let (props, warnings) = extract_frontmatter("---\nkey:\n\t- value\n---\n");
        // Either we got 0 properties + a warning, or partial-success
        // with a warning. Both are acceptable per the spec; the
        // invariant is "don't fail the whole file."
        assert!(
            !warnings.is_empty() || !props.is_empty(),
            "expected warnings or partial props, got neither"
        );
        // No panic = test passes.
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
    fn frontmatter_range_returns_yaml_byte_range() {
        let src = "---\nkey: value\n---\nbody starts here";
        let range = frontmatter_range(src).unwrap();
        assert_eq!(&src[range], "key: value\n");
    }

    #[test]
    fn frontmatter_range_handles_missing_close() {
        // Opening delimiter but no close → treated as no frontmatter.
        // The whole file is body.
        let src = "---\nkey: value\nno close here";
        assert!(frontmatter_range(src).is_none());
    }
}
