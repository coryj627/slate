//! Template discovery, metadata extraction, and safe rendering.
//!
//! Templates are plain Markdown files. They become useful through
//! `{{...}}` markers that resolve at create-from-template time. The
//! rules — locked by `docs/plans/05_locked_architecture_decisions.md`
//! §8.2 ("Tier 1: Configuration-based extensions") — are deliberately
//! tighter than Obsidian's Templater plugin:
//!
//! - No code execution, no expression language, no `if` / `for`.
//! - Variables resolve from a fixed allowlist (`{{date}}`, `{{time}}`,
//!   `{{date:FMT}}`, `{{time:FMT}}`, `{{title}}`, `{{vault}}`,
//!   `{{cursor}}`, `{{prompt:Label}}`).
//! - Anything else is left as-is. A typo can never blow up the render.
//!
//! Two public entry points cover the H-milestone work:
//!
//! * [`extract_template_metadata`] — surfaces the prompts the UI must
//!   ask the user about, *in declaration order*, deduped, with stable
//!   `key`s the render path can look up.
//! * [`render_template_source`] — substitutes the allowlist against a
//!   caller-supplied [`TemplateContext`], records the resulting
//!   `{{cursor}}` byte offset, and never errors.
//!
//! `list_templates` lives on [`crate::VaultSession`] because it needs
//! the provider to enumerate the templates directory; everything else
//! is pure-source.

use std::collections::{HashMap, HashSet};

use chrono::format::{strftime::StrftimeItems, Item};
use chrono::{DateTime, Utc};

use crate::frontmatter::{extract_frontmatter, frontmatter_range, PropertyValue};

// --- Types ---------------------------------------------------------

/// A single prompt extracted from a template source by
/// [`extract_template_metadata`].
///
/// `label` is the raw text the template author wrote between
/// `{{prompt:` and `}}`. `key` is a deterministic slug derived from
/// `label` (lowercase ASCII + non-alnum runs collapsed to `_`, deduped
/// across the template with `_2`, `_3`, …). The Mac UI labels each
/// `TextField` with `label` and stuffs the resulting string into
/// `TemplateContext::prompt_values` keyed by `key`.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct TemplatePrompt {
    pub key: String,
    pub label: String,
}

/// Bundle of everything the UI needs to know up front about a template,
/// before it starts rendering. V1.H ships with `prompts` only; the type
/// is a struct (not a bare `Vec<TemplatePrompt>`) so the FFI surface
/// stays additive when we eventually carry "default cursor position",
/// "suggested file name", etc.
#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub struct TemplateMetadata {
    pub prompts: Vec<TemplatePrompt>,
}

/// A row in the picker: which templates exist and what they're called.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct TemplateSummary {
    /// Vault-relative path, e.g. `"Templates/Daily.md"`.
    pub path: String,
    /// File stem, e.g. `"Daily"`.
    pub name: String,
    /// Either the frontmatter `description:` field or the first non-blank
    /// non-frontmatter line, truncated to 120 chars. `None` when neither
    /// source produced any text.
    pub description: Option<String>,
}

/// All variable values supplied at render time.
///
/// Construct one per `render_template_source` call. `prompt_values` is
/// keyed by [`TemplatePrompt::key`] — the UI walks the
/// [`TemplateMetadata::prompts`] returned by
/// [`extract_template_metadata`] and stuffs each user-supplied response
/// into this map by that key.
#[derive(Debug, Clone)]
pub struct TemplateContext {
    /// Reference time (Unix epoch millis) for `{{date}}` / `{{time}}` /
    /// their `:FMT` variants. Always treated as UTC.
    pub now_ms: i64,
    /// Substituted for `{{title}}`. The new-note title.
    pub title: String,
    /// Substituted for `{{vault}}`. The vault root's basename.
    pub vault_name: String,
    /// Prompt responses keyed by [`TemplatePrompt::key`]. A missing key
    /// leaves the corresponding `{{prompt:Label}}` marker as literal
    /// text (matching the behavior for any other unknown variable).
    pub prompt_values: HashMap<String, String>,
}

impl TemplateContext {
    /// Build a context from common scalars with no prompt responses.
    /// Convenient for tests and the "no prompts in this template" UI path.
    pub fn new(now_ms: i64, title: impl Into<String>, vault_name: impl Into<String>) -> Self {
        Self {
            now_ms,
            title: title.into(),
            vault_name: vault_name.into(),
            prompt_values: HashMap::new(),
        }
    }

    /// Fluent builder for tests: `ctx.with_prompt("topic", "Q1 review")`.
    pub fn with_prompt(mut self, key: impl Into<String>, value: impl Into<String>) -> Self {
        self.prompt_values.insert(key.into(), value.into());
        self
    }
}

/// Result of substituting variables into a template source.
///
/// `cursor_byte_offset` is the byte offset *inside `body`* where the
/// (first, only) `{{cursor}}` marker stood. `None` means the template
/// didn't contain `{{cursor}}`. Indexed in bytes, not chars, so the
/// editor can scan to that point with `body.as_bytes()[offset]` and
/// see the character that follows.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RenderedTemplate {
    pub body: String,
    pub cursor_byte_offset: Option<usize>,
}

// --- Public API ----------------------------------------------------

/// Scan `source` for every `{{prompt:Label}}` marker and return the
/// distinct labels in declaration order.
///
/// Same label appearing twice resolves to one entry. Two *different*
/// labels whose slugs collide (e.g. `Topic` and `topic`) keep both
/// entries; the second one gets a `_2` suffix on its `key`.
///
/// Malformed markers (`{{prompt:}}` with empty label, `{{prompt` with
/// no closing `}}`, `{{prompt}}` without a colon) are silently
/// dropped — the rest of the source parses normally.
pub fn extract_template_metadata(source: &str) -> TemplateMetadata {
    let labels = scan_prompt_labels(source);
    let mut seen: HashSet<String> = HashSet::new();
    let mut counters: HashMap<String, u32> = HashMap::new();
    let mut prompts = Vec::new();
    for label in labels {
        if !seen.insert(label.clone()) {
            continue;
        }
        let base = slug(&label);
        let counter = counters.entry(base.clone()).or_insert(0);
        *counter += 1;
        let key = if *counter == 1 {
            base
        } else {
            format!("{base}_{counter}")
        };
        prompts.push(TemplatePrompt { key, label });
    }
    TemplateMetadata { prompts }
}

/// Render `source` against `context`, substituting every allowlisted
/// variable and leaving everything else literal.
///
/// Always succeeds. An invalid chrono format string (`{{date:%Q}}`),
/// an unknown variable (`{{foo}}`), or a prompt whose `key` isn't in
/// `context.prompt_values` all fall through unchanged. The op-log /
/// reparse path downstream sees identical-or-cleaner text than the
/// source.
///
/// `{{cursor}}` is substituted with the empty string; its byte offset
/// in the rendered `body` is captured in
/// [`RenderedTemplate::cursor_byte_offset`]. A second `{{cursor}}` is
/// also substituted (so the user never sees a literal `{{cursor}}` in
/// their freshly created note) but only the first one's offset wins.
pub fn render_template_source(source: &str, context: &TemplateContext) -> RenderedTemplate {
    // Pre-compute the label→key map by running the same dedup logic
    // the UI used when it asked the user for prompt values. We can't
    // just use `slug` directly: two labels that slug to the same base
    // need to look up under the *suffixed* key.
    let metadata = extract_template_metadata(source);
    let label_to_key: HashMap<&str, &str> = metadata
        .prompts
        .iter()
        .map(|p| (p.label.as_str(), p.key.as_str()))
        .collect();

    let mut out = String::with_capacity(source.len());
    let mut cursor_offset: Option<usize> = None;
    let bytes = source.as_bytes();
    let mut i = 0;
    while i < source.len() {
        // Look for `{{...}}` at this position.
        if i + 1 < source.len() && bytes[i] == b'{' && bytes[i + 1] == b'{' {
            if let Some(rel) = source[i + 2..].find("}}") {
                let body = &source[i + 2..i + 2 + rel];
                let end = i + 2 + rel + 2;
                match substitute(body, context, &label_to_key) {
                    Some(Substitution::Text(s)) => out.push_str(&s),
                    Some(Substitution::Cursor) => {
                        if cursor_offset.is_none() {
                            cursor_offset = Some(out.len());
                        }
                        // Substituted with empty string either way.
                    }
                    None => {
                        // Unknown — preserve the original marker.
                        out.push_str(&source[i..end]);
                    }
                }
                i = end;
                continue;
            }
        }
        // No `{{` match at this position. Advance one char.
        let c = source[i..].chars().next().expect("loop invariant");
        out.push(c);
        i += c.len_utf8();
    }
    RenderedTemplate {
        body: out,
        cursor_byte_offset: cursor_offset,
    }
}

// --- Internal helpers ----------------------------------------------

enum Substitution {
    /// Emit `String` and continue.
    Text(String),
    /// Emit empty string, but record the byte offset for the editor.
    Cursor,
}

fn substitute(
    body: &str,
    ctx: &TemplateContext,
    labels: &HashMap<&str, &str>,
) -> Option<Substitution> {
    match body {
        "date" => format_chrono(ctx.now_ms, "%Y-%m-%d").map(Substitution::Text),
        "time" => format_chrono(ctx.now_ms, "%H:%M").map(Substitution::Text),
        "title" => Some(Substitution::Text(ctx.title.clone())),
        "vault" => Some(Substitution::Text(ctx.vault_name.clone())),
        "cursor" => Some(Substitution::Cursor),
        _ if body.starts_with("date:") => {
            let fmt = &body["date:".len()..];
            if fmt.is_empty() {
                return None;
            }
            format_chrono(ctx.now_ms, fmt).map(Substitution::Text)
        }
        _ if body.starts_with("time:") => {
            let fmt = &body["time:".len()..];
            if fmt.is_empty() {
                return None;
            }
            format_chrono(ctx.now_ms, fmt).map(Substitution::Text)
        }
        _ if body.starts_with("prompt:") => {
            let label = &body["prompt:".len()..];
            if label.is_empty() {
                return None;
            }
            let key = labels.get(label)?;
            let value = ctx.prompt_values.get(*key)?;
            Some(Substitution::Text(value.clone()))
        }
        _ => None,
    }
}

/// Run an `strftime`-style format string against a millisecond
/// timestamp, returning `None` if the timestamp is out of range or the
/// format string contains an unrecognized specifier. Returning `None`
/// (rather than substituting an error string into the body) keeps the
/// caller's "unknown markers stay literal" invariant consistent.
fn format_chrono(now_ms: i64, fmt: &str) -> Option<String> {
    let dt: DateTime<Utc> = DateTime::<Utc>::from_timestamp_millis(now_ms)?;
    let items: Vec<Item<'_>> = StrftimeItems::new(fmt).collect();
    if items.iter().any(|item| matches!(item, Item::Error)) {
        return None;
    }
    Some(dt.format_with_items(items.iter()).to_string())
}

/// Walk the source and yield every `{{prompt:Label}}` label in
/// declaration order, including duplicates. Dedup happens in
/// `extract_template_metadata`.
fn scan_prompt_labels(source: &str) -> Vec<String> {
    let mut out = Vec::new();
    let bytes = source.as_bytes();
    let mut i = 0;
    while i < source.len() {
        if i + 1 < source.len() && bytes[i] == b'{' && bytes[i + 1] == b'{' {
            if let Some(rel) = source[i + 2..].find("}}") {
                let body = &source[i + 2..i + 2 + rel];
                let end = i + 2 + rel + 2;
                if let Some(label) = body.strip_prefix("prompt:") {
                    if !label.is_empty() {
                        out.push(label.to_string());
                    }
                }
                i = end;
                continue;
            }
        }
        let c = source[i..].chars().next().expect("loop invariant");
        i += c.len_utf8();
    }
    out
}

/// Slugify a prompt label into a deterministic snake_case key.
///
/// Lowercase ASCII alphanumerics pass through unchanged; everything
/// else (whitespace, punctuation, non-ASCII characters) collapses to
/// single `_` runs. Leading / trailing underscores are stripped. An
/// empty result (label composed entirely of non-alphanumeric content)
/// falls back to `prompt` so every label still yields a usable key.
fn slug(label: &str) -> String {
    let mut out = String::with_capacity(label.len());
    let mut prev_underscore = true; // suppress leading underscore
    for c in label.chars() {
        if c.is_ascii_alphanumeric() {
            for lc in c.to_lowercase() {
                out.push(lc);
            }
            prev_underscore = false;
        } else if !prev_underscore {
            out.push('_');
            prev_underscore = true;
        }
    }
    while out.ends_with('_') {
        out.pop();
    }
    if out.is_empty() {
        "prompt".to_string()
    } else {
        out
    }
}

/// Pick the description for a [`TemplateSummary`]: prefer a frontmatter
/// `description:` text value, fall back to the first non-blank line
/// after the frontmatter (or from the top, if there's no frontmatter).
/// Trimmed and truncated to 120 chars in both cases.
///
/// Exposed at crate scope so the session's `list_templates` path can
/// call it without re-implementing the lookup.
pub(crate) fn description_from_source(source: &str) -> Option<String> {
    let (props, _warnings) = extract_frontmatter(source);
    for p in &props {
        if p.key.eq_ignore_ascii_case("description") {
            if let PropertyValue::Text(text) = &p.value {
                let trimmed = text.trim();
                if !trimmed.is_empty() {
                    return Some(truncate_chars(trimmed, 120));
                }
            }
        }
    }
    let body_start = source_after_frontmatter(source);
    for line in source[body_start..].lines() {
        let trimmed = line.trim();
        if !trimmed.is_empty() {
            return Some(truncate_chars(trimmed, 120));
        }
    }
    None
}

/// Byte offset of the first character after the closing `---` line.
/// `0` when there's no frontmatter.
fn source_after_frontmatter(source: &str) -> usize {
    let Some(range) = frontmatter_range(source) else {
        return 0;
    };
    let after_body = &source[range.end..];
    match after_body.find('\n') {
        Some(nl) => range.end + nl + 1,
        None => source.len(),
    }
}

fn truncate_chars(s: &str, max_chars: usize) -> String {
    if s.chars().count() <= max_chars {
        s.to_string()
    } else {
        s.chars().take(max_chars).collect()
    }
}

// --- Tests ---------------------------------------------------------

#[cfg(test)]
mod tests {
    use super::*;

    fn prompts(source: &str) -> Vec<(String, String)> {
        extract_template_metadata(source)
            .prompts
            .into_iter()
            .map(|p| (p.key, p.label))
            .collect()
    }

    // 2023-11-14T22:13:20 UTC — picked because every chrono format
    // specifier we test against (`%Y`, `%m`, `%d`, `%H`, `%M`, `%S`,
    // `%A`, `%B`) yields a multi-digit / non-trivial value so a bug
    // that emits "0" or the wrong field can't pass by accident.
    const FIXED_NOW_MS: i64 = 1_700_000_000_000;

    fn ctx() -> TemplateContext {
        TemplateContext::new(FIXED_NOW_MS, "Daily note", "My Vault")
    }

    // -------- extract_template_metadata --------

    #[test]
    fn no_prompts_yields_empty_metadata() {
        assert_eq!(
            extract_template_metadata("just text and {{date}} and {{title}}"),
            TemplateMetadata::default()
        );
    }

    #[test]
    fn three_prompts_returned_in_declaration_order() {
        let source =
            "Hi {{prompt:Topic}}, attendees: {{prompt:Attendees}}. Notes for {{prompt:Date}}.";
        assert_eq!(
            prompts(source),
            vec![
                ("topic".into(), "Topic".into()),
                ("attendees".into(), "Attendees".into()),
                ("date".into(), "Date".into()),
            ]
        );
    }

    #[test]
    fn same_label_twice_yields_one_entry() {
        let source = "First: {{prompt:Topic}} ... again: {{prompt:Topic}}.";
        assert_eq!(prompts(source), vec![("topic".into(), "Topic".into())]);
    }

    #[test]
    fn different_labels_colliding_on_slug_get_suffixed_keys() {
        let source = "{{prompt:Topic}} {{prompt:topic}} {{prompt:TOPIC}}";
        assert_eq!(
            prompts(source),
            vec![
                ("topic".into(), "Topic".into()),
                ("topic_2".into(), "topic".into()),
                ("topic_3".into(), "TOPIC".into()),
            ]
        );
    }

    #[test]
    fn malformed_marker_does_not_break_neighbors() {
        // `{{prompt:}}` is malformed; `{{prompt` never closes.
        let source = "{{prompt:Before}} {{prompt:}} {{prompt:After}}";
        assert_eq!(
            prompts(source),
            vec![
                ("before".into(), "Before".into()),
                ("after".into(), "After".into()),
            ]
        );
    }

    #[test]
    fn slugifies_messy_labels_into_clean_snake_case_keys() {
        let source = "{{prompt:Project Name}} {{prompt:What's the goal?}} {{prompt:café 2026}}";
        assert_eq!(
            prompts(source),
            vec![
                ("project_name".into(), "Project Name".into()),
                ("what_s_the_goal".into(), "What's the goal?".into()),
                ("caf_2026".into(), "café 2026".into()),
            ]
        );
    }

    #[test]
    fn punctuation_only_label_falls_back_to_prompt_with_dedup() {
        let source = "{{prompt:???}} {{prompt:!!!}}";
        assert_eq!(
            prompts(source),
            vec![
                ("prompt".into(), "???".into()),
                ("prompt_2".into(), "!!!".into()),
            ]
        );
    }

    // -------- render_template_source --------

    #[test]
    fn date_uses_yyyy_mm_dd_for_known_timestamp() {
        let r = render_template_source("d={{date}}", &ctx());
        assert_eq!(r.body, "d=2023-11-14");
        assert_eq!(r.cursor_byte_offset, None);
    }

    #[test]
    fn time_uses_hh_mm_for_known_timestamp() {
        let r = render_template_source("t={{time}}", &ctx());
        assert_eq!(r.body, "t=22:13");
    }

    #[test]
    fn date_format_string_is_honored_per_call() {
        let r = render_template_source(
            "iso: {{date:%Y/%m/%d}} | long: {{date:%A, %B %d %Y}}",
            &ctx(),
        );
        assert_eq!(r.body, "iso: 2023/11/14 | long: Tuesday, November 14 2023");
    }

    #[test]
    fn time_format_string_is_honored() {
        let r = render_template_source("at {{time:%H:%M:%S UTC}}", &ctx());
        assert_eq!(r.body, "at 22:13:20 UTC");
    }

    #[test]
    fn title_and_vault_substitute_from_context() {
        let r = render_template_source("# {{title}} in {{vault}}", &ctx());
        assert_eq!(r.body, "# Daily note in My Vault");
    }

    #[test]
    fn unknown_variable_survives_verbatim() {
        let r = render_template_source("# {{title}} {{foo}} {{bar:baz}}", &ctx());
        assert_eq!(r.body, "# Daily note {{foo}} {{bar:baz}}");
    }

    #[test]
    fn invalid_date_format_string_survives_verbatim() {
        // `%Q` is not a chrono specifier — substitution falls through
        // to literal so the user can see what went wrong.
        let r = render_template_source("oops {{date:%Q}}", &ctx());
        assert_eq!(r.body, "oops {{date:%Q}}");
    }

    #[test]
    fn prompt_value_substitutes_by_key() {
        let mut ctx = ctx();
        ctx.prompt_values.insert("topic".into(), "Q2 review".into());
        ctx.prompt_values
            .insert("attendees".into(), "Cory, Pat".into());
        let r = render_template_source(
            "# Meeting: {{prompt:Topic}}\nAttendees: {{prompt:Attendees}}",
            &ctx,
        );
        assert_eq!(r.body, "# Meeting: Q2 review\nAttendees: Cory, Pat");
    }

    #[test]
    fn missing_prompt_value_leaves_marker_literal() {
        let r = render_template_source("Hi {{prompt:Topic}}!", &ctx());
        assert_eq!(r.body, "Hi {{prompt:Topic}}!");
    }

    #[test]
    fn cursor_records_byte_offset_and_substitutes_empty() {
        let r = render_template_source("before {{cursor}} after", &ctx());
        assert_eq!(r.body, "before  after");
        let offset = r.cursor_byte_offset.expect("cursor offset present");
        assert_eq!(offset, "before ".len());
        // The byte immediately following the cursor offset is the
        // next character ("space"), per the test in issue #118.
        assert_eq!(r.body.as_bytes()[offset], b' ');
    }

    #[test]
    fn cursor_offset_is_byte_accurate_after_multibyte_substitution() {
        // The `{{title}}` substitution writes "Café" (5 bytes). The
        // cursor must point to the byte directly after that, not the
        // char count.
        let mut ctx = ctx();
        ctx.title = "Café".into();
        let r = render_template_source("{{title}}{{cursor}}!", &ctx);
        assert_eq!(r.body, "Café!");
        let offset = r.cursor_byte_offset.unwrap();
        assert_eq!(offset, "Café".len());
        assert_eq!(r.body.as_bytes()[offset], b'!');
    }

    #[test]
    fn two_cursors_first_wins_both_substitute_empty() {
        let r = render_template_source("a{{cursor}}b{{cursor}}c", &ctx());
        assert_eq!(r.body, "abc");
        assert_eq!(r.cursor_byte_offset, Some(1));
    }

    #[test]
    fn malformed_prompt_marker_is_left_literal_on_render() {
        let r = render_template_source("a {{prompt:}} b {{prompt}} c", &ctx());
        // `{{prompt:}}` has empty label → unknown variable → literal.
        // `{{prompt}}` has no colon → unknown variable → literal.
        assert_eq!(r.body, "a {{prompt:}} b {{prompt}} c");
    }

    #[test]
    fn unclosed_marker_is_left_literal() {
        let r = render_template_source("oops {{title don't close", &ctx());
        assert_eq!(r.body, "oops {{title don't close");
    }

    #[test]
    fn empty_template_renders_empty() {
        let r = render_template_source("", &ctx());
        assert_eq!(r.body, "");
        assert_eq!(r.cursor_byte_offset, None);
    }

    // -------- description_from_source --------

    #[test]
    fn description_prefers_frontmatter_field() {
        let source = "---\ndescription: Daily-note template\n---\n# {{title}}\n\nBody.";
        assert_eq!(
            description_from_source(source).as_deref(),
            Some("Daily-note template")
        );
    }

    #[test]
    fn description_falls_back_to_first_nonblank_line() {
        let source = "\n\n# Daily Note\n\nMore body.";
        assert_eq!(
            description_from_source(source).as_deref(),
            Some("# Daily Note")
        );
    }

    #[test]
    fn description_skips_frontmatter_block_when_no_description_field() {
        let source = "---\ntags: [template]\n---\n\nFirst real line here.";
        assert_eq!(
            description_from_source(source).as_deref(),
            Some("First real line here.")
        );
    }

    #[test]
    fn description_truncates_to_120_chars() {
        let long = "a".repeat(200);
        let source = format!("---\ndescription: {long}\n---\nbody");
        let d = description_from_source(&source).unwrap();
        assert_eq!(d.chars().count(), 120);
        assert!(d.starts_with("aaaa"));
    }

    #[test]
    fn description_returns_none_for_truly_blank_template() {
        assert_eq!(description_from_source(""), None);
        assert_eq!(description_from_source("\n\n   \n\t\n"), None);
    }
}
