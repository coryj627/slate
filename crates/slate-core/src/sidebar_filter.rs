// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! FL4-1 (#662): the sidebar filter's locked grammar and SQL plan.
//!
//! Query = whitespace-separated terms, all ANDed; `-` negates any term.
//! Malformed operator terms are typed [`FilterParseError`]s, never silent
//! name words. The planner emits ONE parameterized statement per page —
//! no SQL is ever built from user strings by concatenation; user text
//! travels exclusively through bound parameters.
//!
//! Determinism (spec rule 4): core reads no wall clock and no host time
//! zone. Date terms declare *requirements*; the host supplies one exact
//! half-open `[start_ms, end_ms)` UTC-instant window per requirement and
//! core validates the pairing before SQL. Result order is effective-name
//! ascending on the `slate_tree_sort_key` casefold (the ghost-key
//! convention — reused, not re-implemented), tie-broken by byte-ordered
//! path: a total order.

use crate::VaultError;

/// One malformed term with the reason the UI shows (spec: silent
/// fallback teaches users the grammar is broken).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct FilterParseError {
    pub term: String,
    pub reason: String,
}

impl FilterParseError {
    pub(crate) fn invalid_query(&self) -> VaultError {
        VaultError::InvalidQuery {
            message: format!("term '{}': {}", self.term, self.reason),
        }
    }
}

/// The named relative windows. Core knows only their NAMES — boundary
/// instants are host-supplied (spec rule 3).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum SidebarFilterNamedWindow {
    Today,
    Yesterday,
    Last7d,
    Last30d,
}

impl SidebarFilterNamedWindow {
    fn canonical(self) -> &'static str {
        match self {
            Self::Today => "@today",
            Self::Yesterday => "@yesterday",
            Self::Last7d => "@last7d",
            Self::Last30d => "@last30d",
        }
    }
}

/// One positive-form term.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum SidebarFilterTerm {
    /// Case-insensitive (casefold-convention) substring of the effective
    /// name (`display_name ?? stem`).
    Name(String),
    /// Normalized tag; matches the tag or any nested child (`#a` ⊇ `a/b`).
    Tag(String),
    /// A named relative modified-window.
    DateNamed(SidebarFilterNamedWindow),
    /// `@YYYY-MM-DD` — modified on that calendar day.
    DateLiteral(String),
    /// At least one open task.
    HasTask,
    /// File extension, case-insensitive.
    Ext(String),
    /// Vault-relative folder prefix.
    PathPrefix(String),
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SidebarFilterQueryTerm {
    pub negated: bool,
    pub term: SidebarFilterTerm,
}

impl SidebarFilterQueryTerm {
    /// The canonical requirement string for a date term, if any.
    fn date_requirement(&self) -> Option<String> {
        match &self.term {
            SidebarFilterTerm::DateNamed(named) => Some(named.canonical().to_string()),
            SidebarFilterTerm::DateLiteral(day) => Some(format!("@{day}")),
            _ => None,
        }
    }
}

/// One host-validated half-open UTC window for a required date term.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SidebarFilterDateWindow {
    pub term: String,
    pub start_ms: i64,
    pub end_ms: i64,
}

fn valid_gregorian_day(value: &str) -> bool {
    let bytes = value.as_bytes();
    if bytes.len() != 10
        || bytes[4] != b'-'
        || bytes[7] != b'-'
        || !bytes[..4].iter().all(u8::is_ascii_digit)
        || !bytes[5..7].iter().all(u8::is_ascii_digit)
        || !bytes[8..].iter().all(u8::is_ascii_digit)
    {
        return false;
    }
    let year: i32 = match value[..4].parse() {
        Ok(y) => y,
        Err(_) => return false,
    };
    let month: u32 = match value[5..7].parse() {
        Ok(m) => m,
        Err(_) => return false,
    };
    let day: u32 = match value[8..].parse() {
        Ok(d) => d,
        Err(_) => return false,
    };
    (1..=9999).contains(&year) && chrono::NaiveDate::from_ymd_opt(year, month, day).is_some()
}

/// Vault-relative folder validation shared by `path:` terms and
/// `scope_dir` (spec rule 5: traversal/escape scopes fail before SQL).
/// Returns the normalized prefix (no trailing slash) or a reason.
pub(crate) fn normalize_vault_relative_dir(raw: &str) -> Result<String, String> {
    let trimmed = raw.trim().trim_end_matches('/');
    if trimmed.is_empty() {
        return Err("a folder path is required".to_string());
    }
    if trimmed.starts_with('/') || trimmed.contains('\\') || trimmed.contains("//") {
        return Err("the path must be vault-relative".to_string());
    }
    if trimmed
        .split('/')
        .any(|component| component.is_empty() || component == "." || component == "..")
    {
        return Err("the path must stay inside the vault".to_string());
    }
    Ok(trimmed.to_string())
}

/// Parse the locked grammar. Empty/whitespace queries return an empty
/// term list — [`validate_scoped_request`] decides whether that is the
/// scoped-listing mode or an error.
pub fn parse_sidebar_filter(query: &str) -> Result<Vec<SidebarFilterQueryTerm>, FilterParseError> {
    let mut terms = Vec::new();
    for raw in query.split_whitespace() {
        let (negated, body) = match raw.strip_prefix('-') {
            Some(rest) => (true, rest),
            None => (false, raw),
        };
        if body.is_empty() {
            return Err(FilterParseError {
                term: raw.to_string(),
                reason: "a lone '-' negates nothing".to_string(),
            });
        }
        let term = if let Some(tag) = body.strip_prefix('#') {
            match crate::tags_db::normalize_tag(tag) {
                Some(tag_norm) => SidebarFilterTerm::Tag(tag_norm),
                None => {
                    return Err(FilterParseError {
                        term: raw.to_string(),
                        reason: "'#' needs a tag name".to_string(),
                    });
                }
            }
        } else if let Some(date) = body.strip_prefix('@') {
            match date {
                "today" => SidebarFilterTerm::DateNamed(SidebarFilterNamedWindow::Today),
                "yesterday" => SidebarFilterTerm::DateNamed(SidebarFilterNamedWindow::Yesterday),
                "last7d" => SidebarFilterTerm::DateNamed(SidebarFilterNamedWindow::Last7d),
                "last30d" => SidebarFilterTerm::DateNamed(SidebarFilterNamedWindow::Last30d),
                other if valid_gregorian_day(other) => {
                    SidebarFilterTerm::DateLiteral(other.to_string())
                }
                _ => {
                    return Err(FilterParseError {
                        term: raw.to_string(),
                        reason: "'@' takes today, yesterday, last7d, last30d, \
                                 or a YYYY-MM-DD date"
                            .to_string(),
                    });
                }
            }
        } else if let Some(rest) = body.strip_prefix("has:") {
            match rest {
                "task" => SidebarFilterTerm::HasTask,
                _ => {
                    return Err(FilterParseError {
                        term: raw.to_string(),
                        reason: "'has:' supports only has:task".to_string(),
                    });
                }
            }
        } else if let Some(rest) = body.strip_prefix("ext:") {
            let ext = rest.trim().trim_start_matches('.');
            if ext.is_empty() || ext.contains('/') {
                return Err(FilterParseError {
                    term: raw.to_string(),
                    reason: "'ext:' needs an extension like ext:pdf".to_string(),
                });
            }
            SidebarFilterTerm::Ext(ext.to_lowercase())
        } else if let Some(rest) = body.strip_prefix("path:") {
            match normalize_vault_relative_dir(rest) {
                Ok(prefix) => SidebarFilterTerm::PathPrefix(prefix),
                Err(reason) => {
                    return Err(FilterParseError {
                        term: raw.to_string(),
                        reason,
                    });
                }
            }
        } else {
            SidebarFilterTerm::Name(body.to_string())
        };
        terms.push(SidebarFilterQueryTerm { negated, term });
    }
    Ok(terms)
}

/// The canonical unique date-term requirements, first-occurrence order.
/// Negated date terms still require their window (the exclusion needs
/// the same boundaries).
pub fn sidebar_filter_date_requirements(terms: &[SidebarFilterQueryTerm]) -> Vec<String> {
    let mut seen = std::collections::HashSet::new();
    let mut requirements = Vec::new();
    for term in terms {
        if let Some(requirement) = term.date_requirement()
            && seen.insert(requirement.clone())
        {
            requirements.push(requirement);
        }
    }
    requirements
}

/// Validate the host-supplied windows against the requirements: exactly
/// one window per requirement, no extras, no duplicates, no reversed or
/// empty ranges, no unknown terms (spec rule 2).
pub fn validate_date_windows(
    terms: &[SidebarFilterQueryTerm],
    windows: &[SidebarFilterDateWindow],
) -> Result<std::collections::HashMap<String, (i64, i64)>, VaultError> {
    let requirements = sidebar_filter_date_requirements(terms);
    let required: std::collections::HashSet<&str> =
        requirements.iter().map(String::as_str).collect();
    let mut resolved = std::collections::HashMap::new();
    for window in windows {
        if !required.contains(window.term.as_str()) {
            return Err(VaultError::InvalidQuery {
                message: format!(
                    "date window '{}' matches no date term in the query",
                    window.term
                ),
            });
        }
        if window.start_ms >= window.end_ms {
            return Err(VaultError::InvalidQuery {
                message: format!(
                    "date window '{}' must be a non-empty half-open range",
                    window.term
                ),
            });
        }
        if resolved
            .insert(window.term.clone(), (window.start_ms, window.end_ms))
            .is_some()
        {
            return Err(VaultError::InvalidQuery {
                message: format!("duplicate date window '{}'", window.term),
            });
        }
    }
    for requirement in &requirements {
        if !resolved.contains_key(requirement) {
            return Err(VaultError::InvalidQuery {
                message: format!("missing date window '{requirement}'"),
            });
        }
    }
    Ok(resolved)
}

/// Escape `%`, `_`, and `\` for a `LIKE … ESCAPE '\'` pattern.
fn escape_like(value: &str) -> String {
    let mut escaped = String::with_capacity(value.len());
    for character in value.chars() {
        if matches!(character, '%' | '_' | '\\') {
            escaped.push('\\');
        }
        escaped.push(character);
    }
    escaped
}

/// The one-statement plan: WHERE fragments plus their bound parameters,
/// in placement order. `files_clauses` runs against the raw `files`
/// columns; `name_clauses` runs after the effective-name key exists.
pub(crate) struct SidebarFilterPlan {
    pub(crate) files_clauses: Vec<String>,
    pub(crate) files_params: Vec<rusqlite::types::Value>,
    pub(crate) name_clauses: Vec<String>,
    pub(crate) name_params: Vec<rusqlite::types::Value>,
}

/// FL5-2 (#665): the reserved Untagged scope — markdown files with
/// zero `file_tags` rows — as a hand-built plan through the SAME
/// execution pipeline as every filter query (ordering, title join,
/// cursors, summary; no second query path).
pub(crate) fn untagged_plan() -> SidebarFilterPlan {
    SidebarFilterPlan {
        files_clauses: vec![
            "f.is_markdown = 1".to_string(),
            "NOT EXISTS (SELECT 1 FROM file_tags t WHERE t.file_id = f.id)".to_string(),
        ],
        files_params: Vec::new(),
        name_clauses: Vec::new(),
        name_params: Vec::new(),
    }
}

pub(crate) fn plan(
    terms: &[SidebarFilterQueryTerm],
    scope_dir: Option<&str>,
    scope_tag: Option<&str>,
    windows: &std::collections::HashMap<String, (i64, i64)>,
) -> Result<SidebarFilterPlan, VaultError> {
    let mut plan = SidebarFilterPlan {
        files_clauses: Vec::new(),
        files_params: Vec::new(),
        name_clauses: Vec::new(),
        name_params: Vec::new(),
    };
    if let Some(scope) = scope_dir {
        let normalized =
            normalize_vault_relative_dir(scope).map_err(|reason| VaultError::InvalidQuery {
                message: format!("scope '{scope}': {reason}"),
            })?;
        // Binary subtree bounds (the `subtree_bounds` convention): LIKE
        // is ASCII-case-insensitive on this connection, and a scope must
        // never leak a case-distinct sibling directory (review round).
        plan.files_clauses
            .push("(f.path >= ? AND f.path < ?)".to_string());
        plan.files_params.push(format!("{normalized}/").into());
        plan.files_params.push(format!("{normalized}0").into());
    }
    if let Some(raw_tag) = scope_tag {
        // FL-15 red team (high): a tag container's scope rides OUT OF
        // BAND — never interpolated into query text. Frontmatter accepts
        // tags containing whitespace ("project alpha"), which the
        // whitespace-tokenized grammar would split into a tag term plus
        // a name term, silently mis-scoping every downstream batch
        // operation. The structural clause is byte-identical to a typed
        // `#tag` term's (exact or nested descendant).
        let Some(tag_norm) = crate::tags_db::normalize_tag(raw_tag) else {
            return Err(VaultError::InvalidQuery {
                message: format!("scope tag \"{raw_tag}\" is not a valid tag."),
            });
        };
        plan.files_clauses.push(
            "EXISTS (SELECT 1 FROM file_tags ft WHERE ft.file_id = f.id \
             AND (ft.tag_norm = ? OR ft.tag_norm LIKE ? ESCAPE '\\'))"
                .to_string(),
        );
        plan.files_params.push(tag_norm.clone().into());
        plan.files_params
            .push(format!("{}/%", escape_like(&tag_norm)).into());
    }
    for query_term in terms {
        let polarity = |clause: String| {
            if query_term.negated {
                format!("NOT ({clause})")
            } else {
                clause
            }
        };
        match &query_term.term {
            SidebarFilterTerm::Name(word) => {
                // Both sides fold through the SAME key the ordering uses;
                // the parameter folds Rust-side so SQL sees plain text.
                plan.name_clauses
                    .push(polarity("instr(k.sort_key, ?) > 0".to_string()));
                plan.name_params.push(crate::db::tree_sort_key(word).into());
            }
            SidebarFilterTerm::Tag(tag_norm) => {
                plan.files_clauses.push(polarity(
                    "EXISTS (SELECT 1 FROM file_tags ft WHERE ft.file_id = f.id \
                     AND (ft.tag_norm = ? OR ft.tag_norm LIKE ? ESCAPE '\\'))"
                        .to_string(),
                ));
                plan.files_params.push(tag_norm.clone().into());
                plan.files_params
                    .push(format!("{}/%", escape_like(tag_norm)).into());
            }
            SidebarFilterTerm::DateNamed(_) | SidebarFilterTerm::DateLiteral(_) => {
                let requirement = query_term
                    .date_requirement()
                    .expect("date terms always carry a requirement");
                let (start_ms, end_ms) = windows
                    .get(&requirement)
                    .copied()
                    .expect("windows validated against requirements");
                plan.files_clauses
                    .push(polarity("(f.mtime_ms >= ? AND f.mtime_ms < ?)".to_string()));
                plan.files_params.push(start_ms.into());
                plan.files_params.push(end_ms.into());
            }
            SidebarFilterTerm::HasTask => {
                plan.files_clauses.push(polarity(
                    "EXISTS (SELECT 1 FROM tasks t WHERE t.file_id = f.id \
                     AND t.completed = 0)"
                        .to_string(),
                ));
            }
            SidebarFilterTerm::Ext(ext) => {
                plan.files_clauses
                    .push(polarity("lower(COALESCE(f.extension, '')) = ?".to_string()));
                plan.files_params.push(ext.clone().into());
            }
            SidebarFilterTerm::PathPrefix(prefix) => {
                // Binary subtree bounds — see the scope_dir note above.
                plan.files_clauses
                    .push(polarity("(f.path >= ? AND f.path < ?)".to_string()));
                plan.files_params.push(format!("{prefix}/").into());
                plan.files_params.push(format!("{prefix}0").into());
            }
        }
    }
    Ok(plan)
}

/// Normative summary strings (spec rule 6). Grouped decimals.
pub fn sidebar_filter_audio_summary(
    total: u64,
    scope_dir: Option<&str>,
    scope_tag: Option<&str>,
) -> String {
    if total == 0 {
        return "No results.".to_string();
    }
    let counted = count_noun(total, "result", "results");
    if let Some(scope) = scope_dir {
        let folder = scope
            .trim_end_matches('/')
            .rsplit('/')
            .next()
            .unwrap_or(scope);
        return format!("{counted} in {folder}.");
    }
    if let Some(tag) = scope_tag {
        return format!("{counted} for #{tag}.");
    }
    format!("{counted}.")
}

/// A grouped count plus its English noun, taking the singular only at
/// exactly one — zero stays plural (`"0 tags."`). Announcement copy is
/// assembled from this so noun agreement has a single definition.
pub(crate) fn count_noun(value: u64, singular: &str, plural: &str) -> String {
    format!(
        "{} {}",
        group_thousands(value),
        noun(value, singular, plural)
    )
}

/// The bare noun for a count — the single definition of the agreement
/// rule. Callers that format the number themselves (locale decimals,
/// ungrouped byte counts) take this instead of [`count_noun`] so they
/// keep their own number formatting.
pub(crate) fn noun<'a>(value: u64, singular: &'a str, plural: &'a str) -> &'a str {
    if value == 1 { singular } else { plural }
}

pub(crate) fn group_thousands(value: u64) -> String {
    let digits = value.to_string();
    let mut grouped = String::with_capacity(digits.len() + digits.len() / 3);
    for (index, character) in digits.chars().enumerate() {
        if index != 0 && (digits.len() - index).is_multiple_of(3) {
            grouped.push(',');
        }
        grouped.push(character);
    }
    grouped
}

#[cfg(test)]
mod tests {
    use super::*;

    fn term(query: &str) -> SidebarFilterTerm {
        parse_sidebar_filter(query).unwrap().remove(0).term
    }

    #[test]
    fn parser_table() {
        assert_eq!(term("hello"), SidebarFilterTerm::Name("hello".into()));
        assert_eq!(term("#Project"), SidebarFilterTerm::Tag("project".into()));
        assert_eq!(
            term("@today"),
            SidebarFilterTerm::DateNamed(SidebarFilterNamedWindow::Today)
        );
        assert_eq!(
            term("@2026-07-19"),
            SidebarFilterTerm::DateLiteral("2026-07-19".into())
        );
        assert_eq!(term("has:task"), SidebarFilterTerm::HasTask);
        assert_eq!(term("ext:PDF"), SidebarFilterTerm::Ext("pdf".into()));
        assert_eq!(
            term("path:research/"),
            SidebarFilterTerm::PathPrefix("research".into())
        );
        let negated = parse_sidebar_filter("-#a").unwrap().remove(0);
        assert!(negated.negated);
        assert_eq!(negated.term, SidebarFilterTerm::Tag("a".into()));
        assert!(parse_sidebar_filter("").unwrap().is_empty());
        assert!(parse_sidebar_filter("   ").unwrap().is_empty());
    }

    #[test]
    fn malformed_terms_are_typed_errors() {
        for (query, needle) in [
            ("@notadate", "YYYY-MM-DD"),
            ("@2026-02-30", "YYYY-MM-DD"),
            ("has:xyzzy", "has:task"),
            ("#", "tag name"),
            ("ext:", "extension"),
            ("path:", "folder path"),
            ("path:../up", "inside the vault"),
            ("path:/abs", "vault-relative"),
            ("-", "negates nothing"),
        ] {
            let error = parse_sidebar_filter(query).unwrap_err();
            assert!(
                error.reason.contains(needle),
                "{query}: {} !~ {needle}",
                error.reason
            );
            assert_eq!(error.term, query);
        }
    }

    #[test]
    fn date_requirements_are_unique_first_occurrence_and_cover_negation() {
        let terms = parse_sidebar_filter("@today -@2026-01-02 @today @yesterday").unwrap();
        assert_eq!(
            sidebar_filter_date_requirements(&terms),
            vec!["@today", "@2026-01-02", "@yesterday"]
        );
    }

    #[test]
    fn window_validation_rejects_every_mismatch_class() {
        let terms = parse_sidebar_filter("@today").unwrap();
        let make = |term: &str, start: i64, end: i64| SidebarFilterDateWindow {
            term: term.into(),
            start_ms: start,
            end_ms: end,
        };
        assert!(validate_date_windows(&terms, &[]).is_err(), "missing");
        assert!(
            validate_date_windows(&terms, &[make("@today", 5, 5)]).is_err(),
            "empty range"
        );
        assert!(
            validate_date_windows(&terms, &[make("@today", 9, 1)]).is_err(),
            "reversed"
        );
        assert!(
            validate_date_windows(&terms, &[make("@today", 0, 10), make("@today", 0, 10)]).is_err(),
            "duplicate"
        );
        assert!(
            validate_date_windows(&terms, &[make("@today", 0, 10), make("@yesterday", 0, 10)])
                .is_err(),
            "extra/mismatched"
        );
        let resolved = validate_date_windows(&terms, &[make("@today", 0, 10)]).unwrap();
        assert_eq!(resolved["@today"], (0, 10));
    }

    #[test]
    fn audio_summary_shapes() {
        assert_eq!(sidebar_filter_audio_summary(0, None, None), "No results.");
        assert_eq!(sidebar_filter_audio_summary(1, None, None), "1 result.");
        assert_eq!(
            sidebar_filter_audio_summary(1234, None, None),
            "1,234 results."
        );
        assert_eq!(
            sidebar_filter_audio_summary(2, Some("research/papers"), None),
            "2 results in papers."
        );
        // Exactly one is singular in every shape; everything else is plural.
        assert_eq!(
            sidebar_filter_audio_summary(1, Some("research/papers"), None),
            "1 result in papers."
        );
        assert_eq!(
            sidebar_filter_audio_summary(1, None, Some("project alpha")),
            "1 result for #project alpha."
        );
        assert_eq!(
            sidebar_filter_audio_summary(2, None, Some("project alpha")),
            "2 results for #project alpha."
        );
    }

    #[test]
    fn like_escaping_neutralizes_wildcards() {
        assert_eq!(escape_like("a%b_c\\d"), "a\\%b\\_c\\\\d");
    }
}
