//! Markdown task-list extraction.
//!
//! Parses `- [ ] thing` / `- [x] thing` / `- [/] thing` lines out of
//! a Markdown source string into structured `TaskItem` records, plus
//! the optional Tasks-plugin emoji metadata (📅 due, ⏳ scheduled,
//! 🔼/⏫/🔽/⏬ priority, 🔁 recurrence).
//!
//! ## Scope
//!
//! - Bullet markers: `-`, `*`, `+`.
//! - Status char: the single character between `[` and `]`. We don't
//!   restrict the set — `[ ]`, `[x]`, `[X]`, `[/]`, `[-]` and any
//!   single-character status authored by a project-specific Tasks
//!   plugin all round-trip. `completed` is derived (`x` / `X`).
//! - Indentation: any amount of leading whitespace, so nested list-
//!   item tasks parse the same as top-level ones.
//! - Code blocks: fenced (```` ``` ```` / ` ~~~ `) blocks are skipped
//!   so a task syntax pasted into a code example doesn't show up in
//!   the Tasks panel.
//!
//! ## Metadata block
//!
//! Once the first marker emoji appears on a task line, everything
//! from there to end-of-line is the metadata block. Markers are
//! split out and parsed; each marker consumes everything up to the
//! next marker (or EOL) as its payload. Malformed payloads (e.g.
//! `📅 not-a-date`) leave their field NULL without failing the
//! parse — the spec calls for the task to still land in the index
//! when only some of its emoji metadata is well-formed.
//!
//! ## What's NOT extracted
//!
//! - Lines like `- foo` (no checkbox), `- [` (unclosed),
//!   `- []x` (no space after `]`), `- [ab]` (multi-char status).
//! - Lines inside fenced code blocks.
//! - The Dataview inline syntax (`[due:: 2026-06-01]`) — deferred to
//!   V1.x per the Milestone G plan.

use chrono::NaiveDate;

/// One task parsed from a Markdown source.
///
/// `ordinal` is the 0-based document-order index, stable across
/// saves for a given parser version. The Mac UI uses it as the
/// stable identifier for toggle-from-panel without holding line
/// numbers that shift under edits.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct TaskItem {
    pub ordinal: u32,
    pub text: String,
    pub status_char: char,
    pub completed: bool,
    pub due_ms: Option<i64>,
    pub scheduled_ms: Option<i64>,
    pub priority: Option<i32>,
    pub recurrence: Option<String>,
    /// 1-based line number where the task is anchored.
    pub line: u32,
    /// Byte offset of the start of the task's line in the source.
    pub byte_offset: u32,
}

/// Extract tasks from a Markdown source string in document order.
///
/// Returns an empty Vec for sources with no task lines (including
/// the empty string). The following ranges are excluded from the
/// task-line scan so structurally-non-task content doesn't pollute
/// the Tasks panel:
///
/// - **Fenced code blocks** (```` ``` ```` / `~~~`) — a task-list
///   syntax pasted into a tutorial isn't a task.
/// - **HTML blocks** (`<div>` / `<details>` / etc.) — task-shaped
///   lines inside raw HTML are presentational, not actionable.
/// - **YAML frontmatter** (between leading `---` delimiters) —
///   YAML list elements that happen to look like `- [ ] x` are
///   structured data, not tasks; they're already picked up by the
///   properties pipeline.
pub fn extract_tasks(source: &str) -> Vec<TaskItem> {
    // Cheap prefilter: zero-task documents — by far the common case
    // in casual-user vaults — short-circuit before the pulldown-cmark
    // walk + line-by-line scan. The full pipeline costs ~50 µs on a
    // 50 KB no-task doc; the prefilter is one or two byte-substring
    // scans (str::contains is SIMD-accelerated). At 50 k files in a
    // cold scan that's the difference between "imperceptible" and
    // "noticeable lag" (#144 / red-team M3).
    if !might_contain_task_line(source) {
        return Vec::new();
    }

    let excluded = excluded_byte_ranges(source);
    let mut out = Vec::new();
    let mut ordinal: u32 = 0;
    let mut byte_offset: u32 = 0;
    for (line_idx, line) in source.split_inclusive('\n').enumerate() {
        // `split_inclusive` keeps the trailing `\n`; strip it before
        // matching so end-of-line state machines don't have to
        // special-case the terminator.
        let trimmed_end = strip_one_trailing_newline(line);
        let line_start = byte_offset;
        byte_offset = byte_offset.saturating_add(line.len() as u32);

        if byte_in_excluded_range(line_start as usize, &excluded) {
            continue;
        }
        let Some(parsed) = parse_task_line(trimmed_end) else {
            continue;
        };
        let (status_char, body) = parsed;
        let (text, due_ms, scheduled_ms, priority, recurrence) = split_text_and_metadata(body);
        out.push(TaskItem {
            ordinal,
            text,
            status_char,
            completed: status_char == 'x' || status_char == 'X',
            due_ms,
            scheduled_ms,
            priority,
            recurrence,
            line: (line_idx as u32) + 1,
            byte_offset: line_start,
        });
        ordinal += 1;
    }
    out
}

/// Byte-level prefilter for `extract_tasks` — returns `false` only
/// when the source provably has no task line, so it's safe to skip
/// the full pipeline.
///
/// **No false negatives.** Every task line, after optional leading
/// whitespace, starts with a bullet (`-`, `*`, `+`) followed by at
/// least one space or tab and then `[`. Looking for that 3-byte
/// fingerprint anywhere in the source catches every real task; any
/// task that DOESN'T match would also fail `parse_task_line` and
/// wasn't going to land as a TaskItem anyway.
///
/// **False positives are tolerated and cheap.** Prose containing
/// `"- ["`, `"* ["`, or a markdown link list like `"+ [link](url)"`
/// triggers the full parse, which correctly produces zero tasks.
/// The slow path is only paid by docs that look like they might
/// have tasks.
fn might_contain_task_line(source: &str) -> bool {
    // Spaces are by far the dominant separator; tab variants are
    // rare but legal under `parse_task_line` (which calls
    // `trim_start_matches([' ', '\t'])`), so we check them too —
    // a false negative would silently drop the user's tasks.
    const SIGNALS: &[&str] = &["- [", "* [", "+ [", "-\t[", "*\t[", "+\t["];
    SIGNALS.iter().any(|s| source.contains(s))
}

fn strip_one_trailing_newline(s: &str) -> &str {
    if let Some(stripped) = s.strip_suffix('\n') {
        stripped.strip_suffix('\r').unwrap_or(stripped)
    } else {
        s
    }
}

// --- Task-line shape ---

/// Returns `Some((status_char, body))` if `line` matches the task
/// pattern `^\s*[-*+] \[(.)\](?: .*)?$`. The trailing space after
/// `]` is required when text follows but tolerated as missing when
/// the line ends right after `]` — i.e. `- [x]` with no body parses
/// as a task with empty text. The body is what follows the `]`,
/// still untrimmed so metadata splitting can see the exact bytes.
fn parse_task_line(line: &str) -> Option<(char, &str)> {
    // Skip leading whitespace. We accept any horizontal whitespace
    // (spaces, tabs) so nested list-item tasks indented under their
    // parent parse the same as top-level ones.
    let trimmed = line.trim_start();
    let mut chars = trimmed.char_indices();
    let (_, bullet) = chars.next()?;
    if !matches!(bullet, '-' | '*' | '+') {
        return None;
    }
    // Require at least one space after the bullet — `-foo` is not a
    // list item in CommonMark, and `-[ ]` is not a task even though
    // some editors render it as one.
    let after_bullet_space = chars.next()?;
    if after_bullet_space.1 != ' ' && after_bullet_space.1 != '\t' {
        return None;
    }
    let after_bullet = &trimmed[after_bullet_space.0 + after_bullet_space.1.len_utf8()..];
    let after_bullet = after_bullet.trim_start_matches([' ', '\t']);

    // `[X] body` — exactly one char between brackets, then a single
    // space (CommonMark/GFM convention; pulldown-cmark's TaskList
    // extension demands the same).
    let after_open = after_bullet.strip_prefix('[')?;
    let mut status_chars = after_open.chars();
    let status_char = status_chars.next()?;
    let rest = &after_open[status_char.len_utf8()..];
    let after_close = rest.strip_prefix(']')?;
    // Require ` ` (single space) after `]`. A bare `- [x]` with no
    // text and no trailing space is treated as a task with empty
    // text — the issue's acceptance criteria don't carve that out
    // explicitly but it's a common authoring state, and Obsidian
    // treats it as a task.
    let body = if let Some(stripped) = after_close.strip_prefix(' ') {
        stripped
    } else if after_close.is_empty() {
        after_close
    } else {
        // `- [x]extra` (no space) → not a task. Matches GFM.
        return None;
    };
    Some((status_char, body))
}

// --- Metadata block ---

const MARK_DUE: char = '📅';
const MARK_SCHEDULED: char = '⏳';
const MARK_PRIORITY_HIGHEST: char = '⏫';
const MARK_PRIORITY_HIGH: char = '🔼';
const MARK_PRIORITY_LOW: char = '🔽';
const MARK_PRIORITY_LOWEST: char = '⏬';
const MARK_RECURRENCE: char = '🔁';

fn is_marker(c: char) -> bool {
    matches!(
        c,
        MARK_DUE
            | MARK_SCHEDULED
            | MARK_PRIORITY_HIGHEST
            | MARK_PRIORITY_HIGH
            | MARK_PRIORITY_LOW
            | MARK_PRIORITY_LOWEST
            | MARK_RECURRENCE
    )
}

/// Split a task body into its (text, due, scheduled, priority,
/// recurrence) components. The metadata block starts at the first
/// marker character on the line; everything before is text.
///
/// Returns trimmed `text`. Markers with malformed payloads leave
/// their field `None`; the line is still indexed.
fn split_text_and_metadata(
    body: &str,
) -> (
    String,
    Option<i64>,
    Option<i64>,
    Option<i32>,
    Option<String>,
) {
    let (text_part, meta_part) = match body.char_indices().find(|(_, c)| is_marker(*c)) {
        Some((idx, _)) => (&body[..idx], &body[idx..]),
        None => (body, ""),
    };

    let mut due_ms: Option<i64> = None;
    let mut scheduled_ms: Option<i64> = None;
    let mut priority: Option<i32> = None;
    let mut recurrence: Option<String> = None;

    let mut remaining = meta_part;
    while !remaining.is_empty() {
        let mut chars = remaining.char_indices();
        let Some((_, marker)) = chars.next() else {
            break;
        };
        let after_marker_idx = marker.len_utf8();
        let after_marker = &remaining[after_marker_idx..];
        // Payload is everything up to the next marker (or EOL),
        // with edge whitespace and emoji variation selectors
        // (U+FE0F, "VS-16") stripped. macOS and iOS keyboards emit
        // the colour-presentation form of an emoji as `<base> +
        // U+FE0F`; without the strip, a user typing `📅\u{FE0F}
        // 2026-06-01` (which is what those keyboards produce when
        // the emoji picker has VS-16 locked) would have the date
        // silently dropped because chrono can't parse a leading
        // VS-16 (#138 / red-team H3).
        let payload_end = after_marker
            .char_indices()
            .find(|(_, c)| is_marker(*c))
            .map(|(i, _)| i)
            .unwrap_or(after_marker.len());
        let payload = trim_marker_payload(&after_marker[..payload_end]);
        // First marker wins per axis — `is_none()` guards keep
        // duplicate markers (e.g. two 📅 entries on the same line)
        // from silently overwriting earlier well-formed payloads.
        match marker {
            MARK_DUE if due_ms.is_none() => due_ms = parse_date_to_ms(payload),
            MARK_SCHEDULED if scheduled_ms.is_none() => scheduled_ms = parse_date_to_ms(payload),
            MARK_PRIORITY_HIGHEST if priority.is_none() => priority = Some(2),
            MARK_PRIORITY_HIGH if priority.is_none() => priority = Some(1),
            MARK_PRIORITY_LOW if priority.is_none() => priority = Some(-1),
            MARK_PRIORITY_LOWEST if priority.is_none() => priority = Some(-2),
            MARK_RECURRENCE if recurrence.is_none() && !payload.is_empty() => {
                recurrence = Some(payload.to_string())
            }
            _ => {}
        }
        remaining = &after_marker[payload_end..];
    }

    (
        text_part.trim().to_string(),
        due_ms,
        scheduled_ms,
        priority,
        recurrence,
    )
}

/// Strip ASCII/Unicode whitespace AND emoji variation selectors
/// (U+FE0F) from both edges of `s`. The marker payload extraction
/// uses this in place of `str::trim` so a base-emoji + VS-16
/// pairing — what Apple keyboards emit when the user picks a
/// colour-presentation emoji from the system picker — doesn't
/// leak the variation-selector codepoint into the chrono /
/// recurrence parsers downstream.
fn trim_marker_payload(s: &str) -> &str {
    s.trim_matches(|c: char| c.is_whitespace() || c == '\u{FE0F}')
}

fn parse_date_to_ms(s: &str) -> Option<i64> {
    // Reject anything with embedded whitespace before chrono sees it
    // so `📅 2026-05-23 extra` doesn't parse — chrono's
    // `NaiveDate::parse_from_str` ignores trailing input.
    if s.is_empty() || s.split_whitespace().count() != 1 {
        return None;
    }
    let date = NaiveDate::parse_from_str(s, "%Y-%m-%d").ok()?;
    // Midnight UTC of the parsed date.
    let dt = date.and_hms_opt(0, 0, 0)?.and_utc();
    Some(dt.timestamp_millis())
}

// --- Range exclusion ---

/// Byte ranges in `source` that the task-line scanner should skip.
///
/// Covers three families of "looks like a task line but isn't":
///
/// 1. **Fenced code blocks** — pulldown-cmark `Tag::CodeBlock`.
///    Indented code blocks are *not* collected because that would
///    conflict with the legitimate nested-task-under-list pattern.
/// 2. **HTML blocks** — pulldown-cmark `Tag::HtmlBlock`. Catches
///    `<div>`, `<details>`, and the rest. Task-shaped lines inside
///    raw HTML are presentational, not actionable (#137 / red-team
///    H2).
/// 3. **YAML frontmatter** — leading `--- … ---` envelope. The
///    properties pipeline already extracts YAML list elements that
///    look like `- [ ] x` as structured data; surfacing them again
///    in the Tasks panel double-counts the same row (#136 /
///    red-team H1).
fn excluded_byte_ranges(source: &str) -> Vec<(usize, usize)> {
    use pulldown_cmark::{Event, Parser, Tag, TagEnd};
    let mut ranges = Vec::new();

    // (3) Frontmatter — cheap to check; covers the most common
    // false-positive shape.
    if let Some(fm) = crate::frontmatter::frontmatter_range(source) {
        ranges.push((fm.start, fm.end));
    }

    // (1) + (2) Code blocks and HTML blocks via pulldown-cmark's
    // offset iterator. One pass, two tag families collected.
    let parser = Parser::new(source).into_offset_iter();
    let mut code_stack: Vec<usize> = Vec::new();
    let mut html_stack: Vec<usize> = Vec::new();
    for (event, range) in parser {
        match event {
            Event::Start(Tag::CodeBlock(_)) => code_stack.push(range.start),
            Event::End(TagEnd::CodeBlock) => {
                if let Some(start) = code_stack.pop() {
                    ranges.push((start, range.end));
                }
            }
            Event::Start(Tag::HtmlBlock) => html_stack.push(range.start),
            Event::End(TagEnd::HtmlBlock) => {
                if let Some(start) = html_stack.pop() {
                    ranges.push((start, range.end));
                }
            }
            _ => {}
        }
    }
    ranges.sort_by_key(|r| r.0);
    coalesce_ranges(ranges)
}

/// Merge overlapping or adjacent ranges. Behaviour-preserving for
/// `byte_in_excluded_range` (any byte that was in some range still
/// is) but trims the per-line membership-check cost on documents
/// where pulldown-cmark and the frontmatter-range helper produce
/// adjacent or nested ranges — e.g. a fenced code block immediately
/// followed by an HTML block, or any case where multiple emitters
/// touch the same span (Codoki PR #141 Medium).
///
/// Assumes `ranges` is already sorted by start. Returns a new vec
/// because the merge mutates in-place into an output buffer rather
/// than working over the input.
fn coalesce_ranges(ranges: Vec<(usize, usize)>) -> Vec<(usize, usize)> {
    let mut out: Vec<(usize, usize)> = Vec::with_capacity(ranges.len());
    for (start, end) in ranges {
        match out.last_mut() {
            // Adjacent (`start == prev_end`) or overlapping
            // (`start < prev_end`) merges into the previous range.
            // Treat `start == prev_end` as adjacent so two
            // back-to-back blocks collapse to one.
            Some(prev) if start <= prev.1 => {
                if end > prev.1 {
                    prev.1 = end;
                }
            }
            _ => out.push((start, end)),
        }
    }
    out
}

fn byte_in_excluded_range(byte: usize, ranges: &[(usize, usize)]) -> bool {
    ranges.iter().any(|(s, e)| byte >= *s && byte < *e)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn ms_for(y: i32, m: u32, d: u32) -> i64 {
        NaiveDate::from_ymd_opt(y, m, d)
            .unwrap()
            .and_hms_opt(0, 0, 0)
            .unwrap()
            .and_utc()
            .timestamp_millis()
    }

    // --- #108: line shape ---

    #[test]
    fn extracts_open_and_done_tasks() {
        let src = "- [ ] open\n- [x] done\n- [X] also done\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks.len(), 3);
        assert_eq!(tasks[0].status_char, ' ');
        assert!(!tasks[0].completed);
        assert_eq!(tasks[0].text, "open");
        assert_eq!(tasks[1].status_char, 'x');
        assert!(tasks[1].completed);
        assert_eq!(tasks[2].status_char, 'X');
        assert!(tasks[2].completed);
    }

    #[test]
    fn extracts_in_progress_and_cancelled_status_chars() {
        // `[/]` (in progress) and `[-]` (cancelled) are common Tasks-
        // plugin extensions. Status char is stored raw; only x/X
        // mark completed.
        let src = "- [/] doing\n- [-] dropped\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks.len(), 2);
        assert_eq!(tasks[0].status_char, '/');
        assert!(!tasks[0].completed);
        assert_eq!(tasks[1].status_char, '-');
        assert!(!tasks[1].completed);
    }

    #[test]
    fn extracts_with_all_bullet_markers() {
        let src = "- [ ] dash\n* [ ] star\n+ [ ] plus\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks.len(), 3);
        assert_eq!(tasks[0].text, "dash");
        assert_eq!(tasks[1].text, "star");
        assert_eq!(tasks[2].text, "plus");
    }

    #[test]
    fn extracts_indented_nested_tasks_with_correct_line_and_offsets() {
        let src = "- [ ] parent\n  - [ ] child\n    - [ ] grandchild\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks.len(), 3);
        assert_eq!(tasks[0].line, 1);
        assert_eq!(tasks[0].byte_offset, 0);
        assert_eq!(tasks[1].line, 2);
        // Line 1 is "- [ ] parent\n" → 13 bytes; child starts at 13.
        assert_eq!(tasks[1].byte_offset, 13);
        assert_eq!(tasks[2].line, 3);
        // Line 2 is "  - [ ] child\n" → 14 bytes; total 27.
        assert_eq!(tasks[2].byte_offset, 27);
    }

    #[test]
    fn preserves_inline_code_and_wikilinks_in_task_text() {
        let src = "- [ ] run `cargo test` and link to [[Notes#H]]\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks.len(), 1);
        assert_eq!(tasks[0].text, "run `cargo test` and link to [[Notes#H]]");
    }

    #[test]
    fn ignores_lines_that_arent_tasks() {
        // Non-task patterns from the issue acceptance criteria.
        let src = "- foo\n- [\n- []x\n- [ab] no\nplain text\n";
        let tasks = extract_tasks(src);
        assert!(tasks.is_empty(), "got: {tasks:?}");
    }

    #[test]
    fn ignores_tasks_inside_fenced_code_blocks() {
        let src = "Doc:\n```\n- [ ] not a task\n```\n- [ ] real task\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks.len(), 1);
        assert_eq!(tasks[0].text, "real task");
    }

    // --- #136: frontmatter task-shape false positives ---

    #[test]
    fn ignores_task_shaped_lines_inside_yaml_frontmatter() {
        // YAML list elements that happen to look like markdown
        // tasks are structured data, not actionable items. The
        // properties pipeline already indexes them; including
        // them in the Tasks panel would double-count.
        let src = "---\ntitle: x\nitems:\n  - [ ] task-in-yaml\n---\n\n# Body\n\n- [ ] real task\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks.len(), 1, "got: {tasks:?}");
        assert_eq!(tasks[0].text, "real task");
    }

    #[test]
    fn body_task_line_and_byte_offset_correct_after_frontmatter_skip() {
        // Confirm the line/byte_offset accounting still tracks the
        // source-document position, not a frontmatter-stripped one.
        let src = "---\ntitle: x\n---\n- [ ] body task\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks.len(), 1);
        assert_eq!(tasks[0].text, "body task");
        assert_eq!(tasks[0].line, 4);
        // "---\n" (4) + "title: x\n" (9) + "---\n" (4) = 17
        assert_eq!(tasks[0].byte_offset, 17);
    }

    #[test]
    fn no_frontmatter_still_extracts_every_body_task() {
        let src = "# Body\n\n- [ ] one\n- [ ] two\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks.len(), 2);
    }

    // --- #137: HTML-block task-shape false positives ---

    #[test]
    fn ignores_task_shaped_lines_inside_html_blocks() {
        let src = "<div>\n- [ ] inside html\n</div>\n\n- [ ] real task\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks.len(), 1, "got: {tasks:?}");
        assert_eq!(tasks[0].text, "real task");
    }

    #[test]
    fn ignores_task_shaped_lines_inside_details_blocks() {
        // <details> is a common Obsidian collapse pattern; tasks
        // inside should not leak into the panel either.
        let src =
            "<details>\n<summary>Old TODO</summary>\n- [ ] historical task\n</details>\n\n- [ ] active task\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks.len(), 1, "got: {tasks:?}");
        assert_eq!(tasks[0].text, "active task");
    }

    // --- Range coalescing (Codoki PR #141 Medium follow-up) ---

    #[test]
    fn coalesce_ranges_merges_overlapping_runs() {
        // Three overlapping ranges collapse to one, two disjoint
        // ranges stay separate.
        let merged = coalesce_ranges(vec![(0, 5), (3, 7), (6, 10), (20, 25)]);
        assert_eq!(merged, vec![(0, 10), (20, 25)]);
    }

    #[test]
    fn coalesce_ranges_merges_adjacent_ranges() {
        // (0..5) and (5..10) share the boundary at 5; collapse to
        // one. Catches the "two back-to-back blocks" case Codoki
        // flagged.
        let merged = coalesce_ranges(vec![(0, 5), (5, 10)]);
        assert_eq!(merged, vec![(0, 10)]);
    }

    #[test]
    fn coalesce_ranges_leaves_disjoint_ranges_alone() {
        let merged = coalesce_ranges(vec![(0, 5), (10, 15), (20, 25)]);
        assert_eq!(merged, vec![(0, 5), (10, 15), (20, 25)]);
    }

    #[test]
    fn coalesce_ranges_handles_nested_range_inside_larger() {
        // (0..100) fully contains (10..20); the merge should keep
        // the outer extent.
        let merged = coalesce_ranges(vec![(0, 100), (10, 20)]);
        assert_eq!(merged, vec![(0, 100)]);
    }

    #[test]
    fn coalesce_ranges_handles_empty_input() {
        let merged: Vec<(usize, usize)> = coalesce_ranges(vec![]);
        assert!(merged.is_empty());
    }

    #[test]
    fn excluded_ranges_with_adjacent_html_and_fenced_block_preserve_real_task() {
        // HTML block + blank line + fenced code block + blank line +
        // real task. Whether or not the two excluded ranges actually
        // coalesce (depends on whether pulldown-cmark emits them
        // back-to-back or with a 1-byte gap on the blank line), the
        // observable behaviour must be identical: both blocks are
        // excluded, the real task survives. The coalesce step itself
        // is covered by the `coalesce_ranges_*` unit tests above.
        let src = "<div>\n- [ ] in html\n</div>\n\n```\n- [ ] in code\n```\n\n- [ ] real\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks.len(), 1, "got: {tasks:?}");
        assert_eq!(tasks[0].text, "real");
    }

    #[test]
    fn empty_input_yields_no_tasks() {
        assert!(extract_tasks("").is_empty());
    }

    // --- #144: zero-task fast path ---

    #[test]
    fn might_contain_task_line_recognises_all_bullet_marker_combos() {
        // Spaces.
        assert!(might_contain_task_line("- [ ] foo"));
        assert!(might_contain_task_line("* [ ] foo"));
        assert!(might_contain_task_line("+ [ ] foo"));
        // Tabs.
        assert!(might_contain_task_line("-\t[x] foo"));
        assert!(might_contain_task_line("*\t[ ] foo"));
        assert!(might_contain_task_line("+\t[ ] foo"));
        // Indented forms still trigger — the substring appears
        // somewhere in the source even with leading whitespace.
        assert!(might_contain_task_line("  - [ ] indented"));
        assert!(might_contain_task_line("\t\t- [ ] deeply indented"));
    }

    #[test]
    fn might_contain_task_line_rejects_clearly_taskless_docs() {
        assert!(!might_contain_task_line(""));
        assert!(!might_contain_task_line("# heading only\n"));
        assert!(!might_contain_task_line(
            "plain paragraph with no markers\n"
        ));
        // Markdown link is `[text](url)` — no bullet+space+bracket
        // sequence.
        assert!(!might_contain_task_line("see [docs](http://example.com)\n"));
        // Bullet list without checkboxes.
        assert!(!might_contain_task_line("- item one\n- item two\n"));
    }

    #[test]
    fn might_contain_task_line_tolerates_false_positives_safely() {
        // Prose contains `"- ["` literally — prefilter returns true
        // and the full `extract_tasks` correctly returns zero rows.
        // This proves the prefilter is conservative: it never lies
        // about a doc having no tasks.
        let prose = "Compare: foo - [bar](url) vs baz.\n";
        assert!(might_contain_task_line(prose));
        assert!(extract_tasks(prose).is_empty());
    }

    #[test]
    fn extract_tasks_fast_path_yields_empty_for_realistic_no_task_doc() {
        // Mimics the shape of `generate_vault`'s synthetic body:
        // frontmatter, headings, paragraphs, occasional code fence —
        // no task-shaped lines. Must produce zero TaskItems via the
        // fast path (validated by behaviour, not by instrumentation).
        let src = "---\ntitle: x\ntags: [a, b]\n---\n\
                   \n# Heading\n\
                   \nLorem ipsum [link](url) dolor sit amet.\n\
                   \n## Subheading\n\
                   \nMore paragraph content here. No tasks at all.\n\
                   \n```rust\nfn hello() { println!(\"x\"); }\n```\n";
        assert!(extract_tasks(src).is_empty());
    }

    #[test]
    fn extract_tasks_fast_path_does_not_false_negative_on_task_in_busy_doc() {
        // Regression guard: the prefilter must NEVER skip a doc
        // that actually has a task. Doc contains a frontmatter
        // block, headings, paragraphs, a markdown link, AND a real
        // task. The task must still extract.
        let src = "---\ntitle: x\ntags: [a, b]\n---\n\
                   \n# Heading\n\
                   \nSome paragraph with a [link](url).\n\
                   \n- [ ] actual task\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks.len(), 1);
        assert_eq!(tasks[0].text, "actual task");
    }

    #[test]
    fn ordinals_are_document_order_and_dense() {
        let src = "- [ ] a\n- [ ] b\n- [ ] c\n";
        let tasks = extract_tasks(src);
        let ordinals: Vec<u32> = tasks.iter().map(|t| t.ordinal).collect();
        assert_eq!(ordinals, vec![0, 1, 2]);
    }

    #[test]
    fn empty_text_task_round_trips() {
        // `- [x]` with nothing after — common transient state in
        // editors. Status is preserved; text is empty.
        let src = "- [x]\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks.len(), 1);
        assert_eq!(tasks[0].text, "");
        assert_eq!(tasks[0].status_char, 'x');
    }

    // --- #109: emoji metadata ---

    #[test]
    fn parses_due_date() {
        let src = "- [ ] file taxes 📅 2026-04-15\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks[0].text, "file taxes");
        assert_eq!(tasks[0].due_ms, Some(ms_for(2026, 4, 15)));
    }

    #[test]
    fn parses_scheduled_date() {
        let src = "- [ ] start ⏳ 2026-05-01\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks[0].scheduled_ms, Some(ms_for(2026, 5, 1)));
    }

    #[test]
    fn parses_all_priorities() {
        let cases = [
            ("- [ ] highest ⏫\n", 2),
            ("- [ ] high 🔼\n", 1),
            ("- [ ] low 🔽\n", -1),
            ("- [ ] lowest ⏬\n", -2),
        ];
        for (src, expected) in cases {
            let tasks = extract_tasks(src);
            assert_eq!(tasks[0].priority, Some(expected), "src: {src:?}");
        }
    }

    #[test]
    fn absent_priority_is_none() {
        let src = "- [ ] no priority\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks[0].priority, None);
    }

    #[test]
    fn parses_recurrence_raw() {
        let src = "- [ ] take vitamins 🔁 every day\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks[0].recurrence.as_deref(), Some("every day"));
    }

    #[test]
    fn parses_all_markers_mixed_order() {
        let src = "- [ ] standup ⏫ 🔁 every weekday 📅 2026-06-01 ⏳ 2026-05-25\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks[0].text, "standup");
        assert_eq!(tasks[0].priority, Some(2));
        assert_eq!(tasks[0].recurrence.as_deref(), Some("every weekday"));
        assert_eq!(tasks[0].due_ms, Some(ms_for(2026, 6, 1)));
        assert_eq!(tasks[0].scheduled_ms, Some(ms_for(2026, 5, 25)));
    }

    #[test]
    fn malformed_date_silently_drops_field() {
        let src = "- [ ] thing 📅 not-a-date ⏫\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks.len(), 1, "task should still be indexed");
        assert_eq!(tasks[0].text, "thing");
        assert_eq!(tasks[0].due_ms, None);
        assert_eq!(tasks[0].priority, Some(2));
    }

    #[test]
    fn trailing_whitespace_after_metadata_is_tolerated() {
        let src = "- [ ] thing 📅 2026-06-01   \n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks[0].due_ms, Some(ms_for(2026, 6, 1)));
    }

    // --- #138: emoji variation selector (VS-16, U+FE0F) handling ---
    //
    // macOS and iOS keyboards emit `<base emoji> + U+FE0F` for the
    // colour-presentation form of an emoji. Without the strip in
    // `trim_marker_payload`, that variation selector landed at the
    // start of the payload string and made chrono / recurrence
    // parsing fail silently — the line was still indexed but the
    // field stayed `None`.

    #[test]
    fn vs16_after_due_marker_does_not_drop_date() {
        let src = "- [ ] thing 📅\u{FE0F} 2026-06-01\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks[0].text, "thing");
        assert_eq!(tasks[0].due_ms, Some(ms_for(2026, 6, 1)));
    }

    #[test]
    fn vs16_after_scheduled_marker_does_not_drop_date() {
        let src = "- [ ] thing ⏳\u{FE0F} 2026-05-25\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks[0].scheduled_ms, Some(ms_for(2026, 5, 25)));
    }

    #[test]
    fn vs16_after_priority_marker_resolves_priority_and_doesnt_corrupt_next_marker_search() {
        // ⏫ has no payload, but VS-16 must not bleed into the
        // payload-end search and confuse a later marker on the
        // same line.
        let src = "- [ ] thing ⏫\u{FE0F} 📅 2026-06-01\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks[0].priority, Some(2));
        assert_eq!(tasks[0].due_ms, Some(ms_for(2026, 6, 1)));
    }

    #[test]
    fn vs16_after_recurrence_marker_does_not_pollute_payload() {
        let src = "- [ ] thing 🔁\u{FE0F} every week\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks[0].recurrence.as_deref(), Some("every week"));
    }

    #[test]
    fn vs16_trailing_payload_is_also_stripped() {
        // Less common in the wild but worth defending — a date
        // followed by VS-16 must still parse.
        let src = "- [ ] thing 📅 2026-06-01\u{FE0F}\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks[0].due_ms, Some(ms_for(2026, 6, 1)));
    }

    #[test]
    fn marker_characters_inside_task_text_become_metadata_block() {
        // First marker on the line opens the metadata block. The
        // word "highest" after ⏫ is not a marker, so it lands as
        // priority-without-payload (priority still resolves to 2),
        // while everything before ⏫ stays as text. This is the
        // pragmatic interpretation — the user wrote a marker char,
        // we treat it as one.
        let src = "- [ ] meeting ⏫ standup\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks[0].text, "meeting");
        assert_eq!(tasks[0].priority, Some(2));
    }

    // --- Metadata-block guarantees beyond the spec ---

    #[test]
    fn duplicate_due_marker_keeps_first() {
        // First marker wins so later writes don't silently overwrite
        // an earlier-authored date. (Not strictly required by the
        // spec, but the safe default.)
        let src = "- [ ] thing 📅 2026-01-01 📅 2026-02-02\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks[0].due_ms, Some(ms_for(2026, 1, 1)));
    }

    #[test]
    fn malformed_date_with_embedded_space_is_dropped() {
        let src = "- [ ] thing 📅 2026 -05-23\n";
        let tasks = extract_tasks(src);
        assert_eq!(tasks[0].due_ms, None);
    }
}
