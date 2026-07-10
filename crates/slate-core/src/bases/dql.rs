// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Dataview DQL block-query parser for Bases migration.
//!
//! Slate reads Dataview block queries and converts the supported subset into
//! the same [`SlateQuery`] shape as `.base` files. It never authors DQL back
//! to disk. Unsupported DQL constructs become explicit `Expr::Unsupported`
//! nodes plus [`DqlWarning`] entries so renderers can fail loud instead of
//! quietly changing query membership.
//!
//! Pinned DQL-to-Bases mapping highlights:
//!
//! | DQL construct | Slate target | Status |
//! |---|---|---|
//! | `TABLE [WITHOUT ID] a AS "A"` | table view, `formula.dql_column_N` columns | supported |
//! | `LIST [WITHOUT ID] a` | list view plus optional secondary formula column | supported |
//! | `TASK` | `row_source: Tasks`, list view | supported |
//! | `CALENDAR`, `GROUP BY`, `FLATTEN` | `Unsupported` | fail-loud |
//! | `#tag`, `"folder"`, `[[note]]` | `file.hasTag`, `file.inFolder`, `file.hasLink` filters | supported |
//! | `outgoing([[note]])` | `QuerySource::Linked { depth: 1 }` when standalone | supported |
//! | `file.cday`, `file.mday`, `file.link`, `file.inlinks`, `file.outlinks` | `file.ctime.date()`, `file.mtime.date()`, `link(file.path)`, `file.backlinks`, `file.links` | supported |
//! | `file.etags`, `file.lists`, `file.frontmatter`, `file.day`, `file.starred` | `Unsupported` | fail-loud |
//! | TASK `completed`, `checked` | `task.status == "x"`, `task.status != " "` | supported |
//! | TASK `created`, `completion`, `start`, `fullyCompleted`, `children`, `section` | `Unsupported` | fail-loud |

use super::expr::{Callee, Expr, ExprKind, Lit, MethodName, Span, parse_expr};
use super::{ColumnSelection, FilterNode, QuerySource, RowSource, SlateQuery, SortKey, ViewSpec};

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct DqlWarning {
    pub kind: DqlWarningKind,
    pub message: String,
    pub span: Span,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum DqlWarningKind {
    ParseProblem,
    UnsupportedConstruct,
    InvalidCommand,
    InvalidExpression,
}

#[derive(Debug, Clone, Copy)]
struct DqlLine<'a> {
    text: &'a str,
    start: usize,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum CommandKind {
    Where,
    Sort,
    GroupBy,
    Flatten,
    Limit,
    Unknown,
}

#[derive(Debug, Clone)]
struct DqlColumn {
    expression: String,
    alias: Option<String>,
    span: Span,
}

#[derive(Debug, Clone)]
struct SourceParse {
    source: Option<QuerySource>,
    filter: Option<FilterNode>,
}

const DQL_REGEX_TOKEN_STEM: &str = "\u{f8ff}slate-dql-regex:";
const DQL_REGEX_PLACEHOLDER_SUFFIX: char = '\u{f8fe}';

struct TranslatedExpr {
    source: String,
    regex_token: String,
}

/// Pick a deterministic token absent from the authored expression so only
/// placeholders synthesized during this translation can carry its prefix.
fn dql_regex_token(source: &str) -> String {
    let mut id = 0usize;
    loop {
        let token = format!("{DQL_REGEX_TOKEN_STEM}{id}:");
        if !source.contains(&token) {
            return token;
        }
        id = id
            .checked_add(1)
            .expect("DQL regex placeholder namespace exhausted");
    }
}

/// Parse a Dataview block query into a `SlateQuery`.
///
/// This is total: malformed or unsupported input returns a query containing
/// an explicit unsupported expression and at least one warning.
pub fn parse_dql(source: &str) -> (SlateQuery, Vec<DqlWarning>) {
    let lines = dql_lines(source);
    let mut warnings = Vec::new();
    let mut query = empty_query();
    let Some(header) = lines.first().copied() else {
        push_warning(
            &mut warnings,
            DqlWarningKind::ParseProblem,
            "missing DQL query type",
            span(0, source.len()),
        );
        add_unsupported_filter(
            &mut query,
            source,
            "missing DQL query type",
            span(0, source.len()),
        );
        return (query, warnings);
    };

    parse_query_type(header, &mut query, &mut warnings);

    let mut filters = Vec::new();
    if let Some(existing) = query.filters.take() {
        filters.push(existing);
    }
    let mut command_sequence = Vec::new();
    let mut from_seen = false;
    let mut saw_data_command = false;

    for line in lines.iter().skip(1).copied() {
        if let Some(rest) = strip_keyword(line.text, "FROM") {
            if from_seen {
                push_warning(
                    &mut warnings,
                    DqlWarningKind::InvalidCommand,
                    "DQL query contains more than one FROM command",
                    line_span(line),
                );
                filters.push(unsupported_filter(
                    line.text,
                    "repeated FROM command",
                    line_span(line),
                ));
                continue;
            }
            if saw_data_command {
                push_warning(
                    &mut warnings,
                    DqlWarningKind::InvalidCommand,
                    "FROM must appear immediately after the query type",
                    line_span(line),
                );
                filters.push(unsupported_filter(
                    line.text,
                    "FROM after data commands",
                    line_span(line),
                ));
                continue;
            }
            from_seen = true;
            let parsed = parse_source(
                rest.trim(),
                line.start + line.text.len() - rest.len(),
                &mut warnings,
            );
            query.source = parsed.source.unwrap_or(QuerySource::All);
            if let Some(filter) = parsed.filter {
                filters.push(filter);
            }
            continue;
        }

        saw_data_command = true;
        if let Some(rest) = strip_keyword(line.text, "WHERE") {
            command_sequence.push(CommandKind::Where);
            filters.push(FilterNode::Stmt(convert_expr_or_unsupported(
                rest.trim(),
                query.row_source == RowSource::Tasks,
                line.start + line.text.len() - rest.len(),
                &mut warnings,
            )));
        } else if let Some(rest) = strip_keyword(line.text, "SORT") {
            command_sequence.push(CommandKind::Sort);
            parse_sort(
                rest.trim(),
                line,
                query.row_source == RowSource::Tasks,
                &mut query,
                &mut warnings,
            );
        } else if let Some(rest) = strip_keyword(line.text, "LIMIT") {
            command_sequence.push(CommandKind::Limit);
            if let Some(filter) = parse_limit(rest.trim(), line, &mut query, &mut warnings) {
                filters.push(filter);
            }
        } else if let Some(rest) = strip_keyword(line.text, "GROUP BY") {
            command_sequence.push(CommandKind::GroupBy);
            push_warning(
                &mut warnings,
                DqlWarningKind::UnsupportedConstruct,
                "DQL GROUP BY changes row membership via rows aggregation",
                line_span(line),
            );
            filters.push(unsupported_filter(
                rest.trim(),
                "rows aggregation",
                line_span(line),
            ));
        } else if let Some(rest) = strip_keyword(line.text, "FLATTEN") {
            command_sequence.push(CommandKind::Flatten);
            push_warning(
                &mut warnings,
                DqlWarningKind::UnsupportedConstruct,
                "DQL FLATTEN changes row membership",
                line_span(line),
            );
            filters.push(unsupported_filter(rest.trim(), "flatten", line_span(line)));
        } else {
            command_sequence.push(CommandKind::Unknown);
            push_warning(
                &mut warnings,
                DqlWarningKind::InvalidCommand,
                "unknown DQL data command",
                line_span(line),
            );
            filters.push(unsupported_filter(
                line.text,
                "unknown DQL data command",
                line_span(line),
            ));
        }
    }

    if !pipeline_order_is_safe(&command_sequence) {
        push_warning(
            &mut warnings,
            DqlWarningKind::UnsupportedConstruct,
            "DQL command order is order-dependent",
            span(0, source.len()),
        );
        filters.push(unsupported_filter(
            source,
            "order-dependent commands",
            span(0, source.len()),
        ));
    }

    query.filters = combine_filters(filters);
    (query, warnings)
}

fn empty_query() -> SlateQuery {
    SlateQuery {
        source: QuerySource::All,
        row_source: RowSource::Files,
        filters: None,
        formulas: Vec::new(),
        custom_summaries: Vec::new(),
        group_by: None,
        sort: Vec::new(),
        columns: Vec::new(),
        summaries: Vec::new(),
        limit: None,
        view: ViewSpec::Table {
            fallback_from: None,
        },
    }
}

fn dql_lines(source: &str) -> Vec<DqlLine<'_>> {
    let mut lines = Vec::new();
    let mut offset = 0usize;
    for raw in source.split_inclusive('\n') {
        let without_newline = raw.strip_suffix('\n').unwrap_or(raw);
        let text = without_newline.trim();
        if !text.is_empty() {
            let leading = without_newline.len() - without_newline.trim_start().len();
            lines.push(DqlLine {
                text,
                start: offset + leading,
            });
        }
        offset += raw.len();
    }
    if !source.ends_with('\n') {
        return lines;
    }
    lines
}

fn parse_query_type(line: DqlLine<'_>, query: &mut SlateQuery, warnings: &mut Vec<DqlWarning>) {
    if let Some(rest) = strip_keyword(line.text, "TABLE") {
        query.view = ViewSpec::Table {
            fallback_from: None,
        };
        parse_table_header(rest.trim(), line, query, warnings);
    } else if let Some(rest) = strip_keyword(line.text, "LIST") {
        query.view = ViewSpec::List {
            fallback_from: None,
        };
        parse_list_header(rest.trim(), line, query, warnings);
    } else if let Some(rest) = strip_keyword(line.text, "TASK") {
        query.row_source = RowSource::Tasks;
        query.view = ViewSpec::List {
            fallback_from: None,
        };
        if !rest.trim().is_empty() {
            push_warning(
                warnings,
                DqlWarningKind::InvalidCommand,
                "TASK queries do not accept header expressions",
                line_span(line),
            );
            query.filters = Some(unsupported_filter(
                rest.trim(),
                "TASK header expressions",
                line_span(line),
            ));
        }
    } else if let Some(rest) = strip_keyword(line.text, "CALENDAR") {
        query.view = ViewSpec::Table {
            fallback_from: None,
        };
        push_warning(
            warnings,
            DqlWarningKind::UnsupportedConstruct,
            "DQL CALENDAR has no Slate v1 view",
            line_span(line),
        );
        query.filters = Some(unsupported_filter(
            rest.trim(),
            "calendar view",
            line_span(line),
        ));
    } else {
        push_warning(
            warnings,
            DqlWarningKind::ParseProblem,
            "unknown DQL query type",
            line_span(line),
        );
        query.filters = Some(unsupported_filter(
            line.text,
            "unknown DQL query type",
            line_span(line),
        ));
    }
}

fn parse_table_header(
    rest: &str,
    line: DqlLine<'_>,
    query: &mut SlateQuery,
    warnings: &mut Vec<DqlWarning>,
) {
    let (without_id, columns_source) = strip_without_id(rest);
    if !without_id {
        query.columns.push(ColumnSelection {
            id: "file.file".to_string(),
            display_name: None,
        });
    }
    for (idx, column) in parse_columns(columns_source, line.start + line.text.len() - rest.len())
        .into_iter()
        .enumerate()
    {
        let name = format!("dql_column_{}", idx + 1);
        let expr = convert_expr_or_unsupported(
            &column.expression,
            false,
            column.span.start as usize,
            warnings,
        );
        query.formulas.push((name.clone(), expr));
        query.columns.push(ColumnSelection {
            id: format!("formula.{name}"),
            display_name: column.alias,
        });
    }
}

fn parse_list_header(
    rest: &str,
    line: DqlLine<'_>,
    query: &mut SlateQuery,
    warnings: &mut Vec<DqlWarning>,
) {
    let (without_id, value_source) = strip_without_id(rest);
    if !without_id {
        query.columns.push(ColumnSelection {
            id: "file.file".to_string(),
            display_name: None,
        });
    }
    let values = split_top_level(value_source, ',');
    if let Some(first) = values.first().filter(|part| !part.trim().is_empty()) {
        let name = "dql_list_value".to_string();
        let start = line.start + line.text.find(first.trim()).unwrap_or(0);
        let expr = convert_expr_or_unsupported(first.trim(), false, start, warnings);
        query.formulas.push((name.clone(), expr));
        query.columns.push(ColumnSelection {
            id: format!("formula.{name}"),
            display_name: None,
        });
    }
    if values.len() > 1 {
        push_warning(
            warnings,
            DqlWarningKind::InvalidCommand,
            "LIST accepts at most one expression",
            line_span(line),
        );
        query.filters = Some(unsupported_filter(
            value_source,
            "LIST with multiple expressions",
            line_span(line),
        ));
    }
}

fn parse_columns(source: &str, offset: usize) -> Vec<DqlColumn> {
    split_top_level(source, ',')
        .into_iter()
        .filter_map(|raw| {
            let expression = raw.trim();
            if expression.is_empty() {
                return None;
            }
            let local_start = raw.find(expression).unwrap_or(0);
            let (expression, alias) = split_alias(expression);
            let start = offset + local_start;
            Some(DqlColumn {
                span: span(start, start + expression.len()),
                expression: expression.to_string(),
                alias,
            })
        })
        .collect()
}

fn split_alias(source: &str) -> (&str, Option<String>) {
    let Some(idx) = find_top_level_keyword(source, "AS") else {
        return (source.trim(), None);
    };
    let expr = source[..idx].trim();
    let alias_src = source[idx + 2..].trim();
    let alias = parse_quoted(alias_src).unwrap_or_else(|| alias_src.to_string());
    (expr, Some(alias))
}

fn strip_without_id(source: &str) -> (bool, &str) {
    let Some(rest) = strip_keyword(source.trim_start(), "WITHOUT ID") else {
        return (false, source.trim());
    };
    (true, rest.trim())
}

fn parse_sort(
    source: &str,
    line: DqlLine<'_>,
    task_context: bool,
    query: &mut SlateQuery,
    warnings: &mut Vec<DqlWarning>,
) {
    for raw in split_top_level(source, ',') {
        let part = raw.trim();
        if part.is_empty() {
            continue;
        }
        let (expr_source, ascending) = split_sort_direction(part);
        let start = line.start + line.text.find(expr_source.trim()).unwrap_or(0);
        query.sort.push(SortKey {
            expr: convert_expr_or_unsupported(expr_source.trim(), task_context, start, warnings),
            ascending,
        });
    }
}

fn split_sort_direction(source: &str) -> (&str, bool) {
    let trimmed = source.trim();
    for direction in ["DESCENDING", "ASCENDING", "DESC", "ASC"] {
        if let Some(prefix) = strip_suffix_keyword(trimmed, direction) {
            return (prefix.trim(), !direction.starts_with("DESC"));
        }
    }
    (trimmed, true)
}

fn parse_limit(
    source: &str,
    line: DqlLine<'_>,
    query: &mut SlateQuery,
    warnings: &mut Vec<DqlWarning>,
) -> Option<FilterNode> {
    match source.trim().parse::<u64>() {
        Ok(limit) => {
            query.limit = Some(limit);
            None
        }
        Err(_) => {
            push_warning(
                warnings,
                DqlWarningKind::InvalidCommand,
                "LIMIT must be an unsigned integer",
                line_span(line),
            );
            Some(unsupported_filter(source, "invalid LIMIT", line_span(line)))
        }
    }
}

fn parse_source(source: &str, offset: usize, warnings: &mut Vec<DqlWarning>) -> SourceParse {
    if let Some(link) =
        parse_standalone_outgoing(source).filter(|link| !link.is_empty() && link != "#")
    {
        return SourceParse {
            source: Some(QuerySource::Linked {
                from_path: link,
                depth: 1,
            }),
            filter: None,
        };
    }

    let mut parser = SourceParser {
        source,
        bytes: source.as_bytes(),
        pos: 0,
        offset,
    };
    match parser.parse_or() {
        Some(filter) if parser.finished() => SourceParse {
            source: None,
            filter: Some(filter),
        },
        _ => {
            let source_span = span(offset, offset + source.len());
            push_warning(
                warnings,
                DqlWarningKind::InvalidExpression,
                format!("invalid FROM source: {source}"),
                source_span,
            );
            SourceParse {
                source: None,
                filter: Some(unsupported_filter(
                    source,
                    "invalid FROM source",
                    source_span,
                )),
            }
        }
    }
}

struct SourceParser<'a> {
    source: &'a str,
    bytes: &'a [u8],
    pos: usize,
    offset: usize,
}

impl<'a> SourceParser<'a> {
    fn parse_or(&mut self) -> Option<FilterNode> {
        let mut nodes = vec![self.parse_and()?];
        loop {
            self.skip_ws();
            if !self.consume_word("or") {
                break;
            }
            nodes.push(self.parse_and()?);
        }
        Some(if nodes.len() == 1 {
            nodes.remove(0)
        } else {
            FilterNode::Or(nodes)
        })
    }

    fn parse_and(&mut self) -> Option<FilterNode> {
        let mut nodes = vec![self.parse_unary()?];
        loop {
            self.skip_ws();
            if !self.consume_word("and") {
                break;
            }
            nodes.push(self.parse_unary()?);
        }
        Some(if nodes.len() == 1 {
            nodes.remove(0)
        } else {
            FilterNode::And(nodes)
        })
    }

    fn parse_unary(&mut self) -> Option<FilterNode> {
        self.skip_ws();
        if self.consume_byte(b'!') || self.consume_byte(b'-') {
            return Some(FilterNode::Not(vec![self.parse_unary()?]));
        }
        if self.consume_byte(b'(') {
            let node = self.parse_or()?;
            self.skip_ws();
            if !self.consume_byte(b')') {
                return None;
            }
            return Some(node);
        }
        self.parse_atom()
    }

    fn parse_atom(&mut self) -> Option<FilterNode> {
        self.skip_ws();
        let start = self.pos;
        if self.peek_byte() == Some(b'#') {
            self.pos += 1;
            while self
                .peek_byte()
                .is_some_and(|b| !b.is_ascii_whitespace() && !matches!(b, b')' | b'('))
            {
                self.pos += 1;
            }
            let tag = &self.source[start + 1..self.pos];
            if tag.is_empty() {
                return None;
            }
            return Some(expr_filter(
                &format!("file.hasTag({})", quote_expr_string(tag)),
                self.offset + start,
                self.pos - start,
            ));
        }
        if self.peek_byte() == Some(b'"') {
            let quoted = self.read_quoted()?;
            let expr = if quoted.ends_with(".md") {
                format!("file.path == {}", quote_expr_string(&quoted))
            } else {
                format!("file.inFolder({})", quote_expr_string(&quoted))
            };
            return Some(expr_filter(&expr, self.offset + start, self.pos - start));
        }
        if self.starts_with("[[") {
            let link = self.read_wikilink()?;
            let expr = if link.is_empty() || link == "#" {
                "file.hasLink(this)".to_string()
            } else {
                format!("file.hasLink({})", quote_expr_string(&link))
            };
            return Some(expr_filter(&expr, self.offset + start, self.pos - start));
        }
        if self.consume_word("outgoing") {
            self.skip_ws();
            if !self.consume_byte(b'(') {
                return None;
            }
            self.skip_ws();
            let link = self.read_wikilink()?;
            self.skip_ws();
            if !self.consume_byte(b')') {
                return None;
            }
            let target = if link.is_empty() || link == "#" {
                "this".to_string()
            } else {
                quote_expr_string(&link)
            };
            let expr = format!("link({target}).linksTo(file.file)");
            return Some(expr_filter(&expr, self.offset + start, self.pos - start));
        }
        None
    }

    fn skip_ws(&mut self) {
        while self.peek_byte().is_some_and(|b| b.is_ascii_whitespace()) {
            self.pos += 1;
        }
    }

    fn consume_word(&mut self, word: &str) -> bool {
        self.skip_ws();
        let end = self.pos + word.len();
        if end > self.source.len() {
            return false;
        }
        let candidate = &self.source[self.pos..end];
        if !candidate.eq_ignore_ascii_case(word) {
            return false;
        }
        if self
            .bytes
            .get(end)
            .is_some_and(|b| b.is_ascii_alphanumeric() || *b == b'_')
        {
            return false;
        }
        self.pos = end;
        true
    }

    fn consume_byte(&mut self, byte: u8) -> bool {
        self.skip_ws();
        if self.peek_byte() == Some(byte) {
            self.pos += 1;
            true
        } else {
            false
        }
    }

    fn read_quoted(&mut self) -> Option<String> {
        if self.peek_byte()? != b'"' {
            return None;
        }
        self.pos += 1;
        let mut value = String::new();
        while self.pos < self.source.len() {
            let ch = self.source[self.pos..].chars().next()?;
            self.pos += ch.len_utf8();
            if ch == '"' {
                return Some(value);
            }
            if ch == '\\' {
                let next = self.source[self.pos..].chars().next()?;
                self.pos += next.len_utf8();
                value.push(next);
            } else {
                value.push(ch);
            }
        }
        None
    }

    fn read_wikilink(&mut self) -> Option<String> {
        if !self.starts_with("[[") {
            return None;
        }
        self.pos += 2;
        let start = self.pos;
        let end_rel = self.source[start..].find("]]")?;
        let end = start + end_rel;
        let link = self.source[start..end].trim().to_string();
        self.pos = end + 2;
        Some(link)
    }

    fn starts_with(&self, needle: &str) -> bool {
        self.source[self.pos..].starts_with(needle)
    }

    fn peek_byte(&self) -> Option<u8> {
        self.bytes.get(self.pos).copied()
    }

    fn finished(&mut self) -> bool {
        self.skip_ws();
        self.pos == self.source.len()
    }
}

fn parse_standalone_outgoing(source: &str) -> Option<String> {
    let trimmed = source.trim();
    if !starts_with_keyword(trimmed, "outgoing") {
        return None;
    }
    let open = trimmed.find('(')?;
    let close = trimmed.rfind(')')?;
    if !trimmed[close + 1..].trim().is_empty() {
        return None;
    }
    let inner = trimmed[open + 1..close].trim();
    if !inner.starts_with("[[") || !inner.ends_with("]]") {
        return None;
    }
    let link = inner[2..inner.len() - 2].trim();
    Some(link.to_string())
}

fn pipeline_order_is_safe(commands: &[CommandKind]) -> bool {
    let mut phase = 0u8;
    let mut saw_sort = false;
    let mut saw_limit = false;
    for command in commands {
        match command {
            CommandKind::Where if phase == 0 => {}
            CommandKind::Where => return false,
            CommandKind::Sort if phase <= 1 && !saw_sort => {
                phase = 1;
                saw_sort = true;
            }
            CommandKind::Limit if phase <= 2 && !saw_limit => {
                phase = 2;
                saw_limit = true;
            }
            CommandKind::GroupBy | CommandKind::Flatten | CommandKind::Unknown => return false,
            CommandKind::Sort | CommandKind::Limit => return false,
        }
    }
    true
}

fn convert_expr_or_unsupported(
    source: &str,
    task_context: bool,
    offset: usize,
    warnings: &mut Vec<DqlWarning>,
) -> Expr {
    match translate_expr(source, task_context) {
        Ok(translated) => match parse_expr(&translated.source) {
            Ok(mut expr) => {
                restore_dql_regex_literals(&mut expr, &translated.regex_token);
                let mut reasons = Vec::new();
                collect_unsupported_reasons(&expr, &mut reasons);
                reasons.sort();
                reasons.dedup();
                for reason in reasons {
                    push_warning(
                        warnings,
                        DqlWarningKind::UnsupportedConstruct,
                        reason,
                        span(offset, offset + source.len()),
                    );
                }
                expr
            }
            Err(err) => {
                push_warning(
                    warnings,
                    DqlWarningKind::InvalidExpression,
                    format!("DQL expression did not convert: {}", err.message),
                    span(offset, offset + source.len()),
                );
                unsupported_expr(
                    source,
                    "invalid converted expression",
                    span(offset, offset + source.len()),
                )
            }
        },
        Err(reason) => {
            push_warning(
                warnings,
                DqlWarningKind::UnsupportedConstruct,
                reason.clone(),
                span(offset, offset + source.len()),
            );
            unsupported_expr(source, &reason, span(offset, offset + source.len()))
        }
    }
}

fn collect_unsupported_reasons(expr: &Expr, reasons: &mut Vec<String>) {
    match &expr.kind {
        ExprKind::Unsupported { reason, .. } => reasons.push(reason.clone()),
        ExprKind::Unary { rhs, .. } => collect_unsupported_reasons(rhs, reasons),
        ExprKind::Binary { lhs, rhs, .. } => {
            collect_unsupported_reasons(lhs, reasons);
            collect_unsupported_reasons(rhs, reasons);
        }
        ExprKind::Call { args, .. } => {
            for arg in args {
                collect_unsupported_reasons(arg, reasons);
            }
        }
        ExprKind::Index { base, index } => {
            collect_unsupported_reasons(base, reasons);
            collect_unsupported_reasons(index, reasons);
        }
        ExprKind::Field { base, .. } => collect_unsupported_reasons(base, reasons),
        ExprKind::ListExpr {
            base, body, init, ..
        } => {
            collect_unsupported_reasons(base, reasons);
            collect_unsupported_reasons(body, reasons);
            if let Some(init) = init {
                collect_unsupported_reasons(init, reasons);
            }
        }
        ExprKind::Lit(_) | ExprKind::Prop(_) => {}
    }
}

fn restore_dql_regex_literals(expr: &mut Expr, regex_token: &str) {
    match &mut expr.kind {
        ExprKind::Call { callee, args } => {
            match callee {
                Callee::Method {
                    receiver,
                    name: MethodName::Matches,
                } => {
                    let pattern = match &receiver.kind {
                        ExprKind::Lit(Lit::String(placeholder)) => placeholder
                            .strip_prefix(regex_token)
                            .and_then(|placeholder| {
                                placeholder.strip_suffix(DQL_REGEX_PLACEHOLDER_SUFFIX)
                            })
                            .map(str::to_string),
                        _ => None,
                    };
                    if let Some(pattern) = pattern {
                        receiver.kind = ExprKind::Lit(Lit::Regex {
                            pattern,
                            flags: String::new(),
                        });
                    } else {
                        restore_dql_regex_literals(receiver, regex_token);
                    }
                }
                Callee::Method { receiver, .. } => {
                    restore_dql_regex_literals(receiver, regex_token)
                }
                Callee::Global(_) => {}
            }
            args.iter_mut()
                .for_each(|arg| restore_dql_regex_literals(arg, regex_token));
        }
        ExprKind::Lit(Lit::List(items)) => items
            .iter_mut()
            .for_each(|item| restore_dql_regex_literals(item, regex_token)),
        ExprKind::Lit(Lit::Object(items)) => items.iter_mut().for_each(|(_, value)| {
            restore_dql_regex_literals(value, regex_token);
        }),
        ExprKind::Index { base, index } => {
            restore_dql_regex_literals(base, regex_token);
            restore_dql_regex_literals(index, regex_token);
        }
        ExprKind::Field { base, .. } => restore_dql_regex_literals(base, regex_token),
        ExprKind::Unary { rhs, .. } => restore_dql_regex_literals(rhs, regex_token),
        ExprKind::Binary { lhs, rhs, .. } => {
            restore_dql_regex_literals(lhs, regex_token);
            restore_dql_regex_literals(rhs, regex_token);
        }
        ExprKind::ListExpr {
            base, body, init, ..
        } => {
            restore_dql_regex_literals(base, regex_token);
            restore_dql_regex_literals(body, regex_token);
            if let Some(init) = init {
                restore_dql_regex_literals(init, regex_token);
            }
        }
        ExprKind::Lit(_) | ExprKind::Prop(_) | ExprKind::Unsupported { .. } => {}
    }
}

fn translate_expr(source: &str, task_context: bool) -> Result<TranslatedExpr, String> {
    let regex_token = dql_regex_token(source);
    let source = translate_expr_with_token(source, task_context, &regex_token)?;
    Ok(TranslatedExpr {
        source,
        regex_token,
    })
}

fn translate_expr_with_token(
    source: &str,
    task_context: bool,
    regex_token: &str,
) -> Result<String, String> {
    check_unsupported_fields(source, task_context)?;
    let rewritten = rewrite_typeof_comparisons(source)?;
    let rewritten = rewrite_function_calls(&rewritten, regex_token)?;
    let rewritten = rewrite_string_repeat(&rewritten);
    let rewritten = rewrite_special_fields(&rewritten, task_context);
    let rewritten = rewrite_boolean_words_and_equality(&rewritten);
    Ok(rewritten)
}

fn check_unsupported_fields(source: &str, task_context: bool) -> Result<(), String> {
    if contains_bare_word_ascii_ci(source, "null") {
        return Err("DQL null literal is unsupported; guard with typeof".to_string());
    }
    for field in [
        "file.etags",
        "file.lists",
        "file.frontmatter",
        "file.day",
        "file.starred",
    ] {
        if contains_field_ascii_ci(source, field) {
            return Err(format!("unsupported DQL field {field}"));
        }
    }
    if task_context {
        for field in [
            "created",
            "completion",
            "start",
            "fullyCompleted",
            "children",
            "section",
            "visual",
            "line",
            "lineCount",
            "path",
            "tags",
            "outlinks",
            "link",
            "parent",
            "annotated",
            "task",
            "blockId",
        ] {
            if contains_bare_task_field_ascii_ci(source, field) {
                return Err(format!("unsupported DQL task field {field}"));
            }
        }
    }
    Ok(())
}

fn rewrite_function_calls(source: &str, regex_token: &str) -> Result<String, String> {
    let mut out = String::new();
    let bytes = source.as_bytes();
    let mut pos = 0usize;
    while pos < bytes.len() {
        let b = bytes[pos];
        if b == b'"' || b == b'\'' {
            if let Some((value, consumed)) = parse_quoted_with_len(&source[pos..]) {
                out.push_str(&quote_expr_string(&value));
                pos += consumed;
            } else {
                pos = copy_quoted(source, pos, &mut out);
            }
            continue;
        }
        if is_ident_start(b) {
            let start = pos;
            pos += 1;
            while bytes
                .get(pos)
                .is_some_and(|b| b.is_ascii_alphanumeric() || *b == b'_')
            {
                pos += 1;
            }
            let name = &source[start..pos];
            let mut probe = pos;
            while bytes.get(probe).is_some_and(|b| b.is_ascii_whitespace()) {
                probe += 1;
            }
            if bytes.get(probe) == Some(&b'(') {
                let Some(close) = find_matching_paren(source, probe) else {
                    return Err(format!("unterminated function call {name}"));
                };
                let args_src = &source[probe + 1..close];
                let args = split_top_level(args_src, ',')
                    .into_iter()
                    .map(|arg| rewrite_function_calls(arg.trim(), regex_token))
                    .collect::<Result<Vec<_>, _>>()?;
                out.push_str(&map_function_call(name, args_src, &args, regex_token)?);
                pos = close + 1;
            } else {
                out.push_str(name);
            }
            continue;
        }
        out.push(b as char);
        pos += 1;
    }
    Ok(out)
}

fn rewrite_typeof_comparisons(source: &str) -> Result<String, String> {
    let mut out = String::new();
    let bytes = source.as_bytes();
    let mut pos = 0usize;
    while pos < bytes.len() {
        let b = bytes[pos];
        if b == b'"' || b == b'\'' {
            pos = copy_quoted(source, pos, &mut out);
            continue;
        }
        if keyword_at(source, pos, "typeof") {
            let after_name = pos + "typeof".len();
            let mut open = after_name;
            while bytes.get(open).is_some_and(|b| b.is_ascii_whitespace()) {
                open += 1;
            }
            if bytes.get(open) != Some(&b'(') {
                out.push_str("typeof");
                pos = after_name;
                continue;
            }
            let Some(close) = find_matching_paren(source, open) else {
                return Err("unterminated typeof call".to_string());
            };
            let mut op_start = close + 1;
            while bytes.get(op_start).is_some_and(|b| b.is_ascii_whitespace()) {
                op_start += 1;
            }
            let (negated, after_op) =
                if source.as_bytes().get(op_start..op_start + 2) == Some(b"!=") {
                    (true, op_start + 2)
                } else if source.as_bytes().get(op_start..op_start + 2) == Some(b"==") {
                    (false, op_start + 2)
                } else if bytes.get(op_start) == Some(&b'=') {
                    (false, op_start + 1)
                } else {
                    out.push_str("typeof");
                    pos = after_name;
                    continue;
                };
            let mut value_start = after_op;
            while bytes
                .get(value_start)
                .is_some_and(|b| b.is_ascii_whitespace())
            {
                value_start += 1;
            }
            let value = parse_quoted_with_len(&source[value_start..])
                .ok_or_else(|| "typeof comparison must use a string type name".to_string())?;
            let arg = source[open + 1..close].trim();
            if negated {
                out.push_str(&format!(
                    "!(({}).isType({}))",
                    arg,
                    quote_expr_string(&value.0)
                ));
            } else {
                out.push_str(&format!(
                    "({}).isType({})",
                    arg,
                    quote_expr_string(&value.0)
                ));
            }
            pos = value_start + value.1;
            continue;
        }
        out.push(b as char);
        pos += 1;
    }
    Ok(out)
}

fn map_function_call(
    name: &str,
    original_args: &str,
    args: &[String],
    regex_token: &str,
) -> Result<String, String> {
    let lower = name.to_ascii_lowercase();
    let one = |method: &str| -> Result<String, String> {
        require_arg_count(name, args, 1)?;
        Ok(format!("({}).{method}()", args[0]))
    };
    let method = |method: &str| -> Result<String, String> {
        require_arg_count_at_least(name, args, 1)?;
        let receiver = &args[0];
        let rest = args[1..].join(", ");
        Ok(if rest.is_empty() {
            format!("({receiver}).{method}()")
        } else {
            format!("({receiver}).{method}({rest})")
        })
    };
    match lower.as_str() {
        "date" => {
            require_arg_count(name, args, 1)?;
            let arg = original_args.trim();
            Ok(match arg.to_ascii_lowercase().as_str() {
                "today" => "today()".to_string(),
                "now" => "now()".to_string(),
                "tomorrow" => "(today() + duration(\"1d\"))".to_string(),
                "yesterday" => "(today() - duration(\"1d\"))".to_string(),
                "sow" => start_of_week_expr(),
                "eow" => end_of_week_expr(),
                "som" => start_of_month_expr(),
                "eom" => end_of_month_expr(),
                "soy" => start_of_year_expr(),
                "eoy" => end_of_year_expr(),
                raw if looks_like_iso_date(raw) => format!("date({})", quote_expr_string(raw)),
                _ => format!("date({})", args[0]),
            })
        }
        "dur" => {
            require_arg_count(name, args, 1)?;
            let arg = original_args.trim();
            if is_quoted(arg) {
                Ok(format!("duration({})", args[0]))
            } else {
                Ok(format!("duration({})", quote_expr_string(arg)))
            }
        }
        "number" | "link" | "list" | "min" | "max" | "sum" | "average" | "object" => {
            Ok(format!("{name}({})", args.join(", ")))
        }
        "array" => Ok(format!("list({})", args.join(", "))),
        "embed" => Ok(format!("link({})", args.join(", "))),
        "string" => {
            require_arg_count(name, args, 1)?;
            Ok(format!("({}).toString()", args[0]))
        }
        "contains" => method("contains"),
        "lower" => one("lower"),
        "replace" => method("replace"),
        "join" => method("join"),
        "length" => {
            require_arg_count(name, args, 1)?;
            Ok(format!("({}).length", args[0]))
        }
        "sort" => one("sort"),
        "reverse" => one("reverse"),
        "unique" => one("unique"),
        "flat" => method("flat"),
        "slice" | "substring" => method("slice"),
        "filter" => map_lambda_list_expr(name, original_args, args, "filter", regex_token),
        "map" => map_lambda_list_expr(name, original_args, args, "map", regex_token),
        "startswith" => method("startsWith"),
        "endswith" => method("endsWith"),
        "round" => method("round"),
        "floor" => one("floor"),
        "ceil" => one("ceil"),
        "trunc" => one("trunc"),
        "regextest" | "regexmatch" => {
            require_arg_count(name, args, 2)?;
            let raw_args = split_top_level(original_args, ',');
            let raw_pattern = raw_args
                .first()
                .map(|arg| arg.trim())
                .ok_or_else(|| format!("{name} requires a literal regex pattern"))?;
            let (pattern, _) = parse_quoted_with_len(raw_pattern)
                .filter(|(_, consumed)| raw_pattern[*consumed..].trim().is_empty())
                .ok_or_else(|| format!("{name} requires a literal regex pattern"))?;
            let placeholder = format!("{regex_token}{pattern}{DQL_REGEX_PLACEHOLDER_SUFFIX}");
            Ok(format!(
                "({}).matches({})",
                quote_expr_string(&placeholder),
                args[1]
            ))
        }
        "regexreplace" => method("replace"),
        "split" => method("split"),
        "striptime" => one("date"),
        "choice" => {
            require_arg_count(name, args, 3)?;
            Ok(format!("if({}, {}, {})", args[0], args[1], args[2]))
        }
        "default" | "ldefault" => {
            require_arg_count(name, args, 2)?;
            Ok(format!(
                "if(({}).isEmpty(), {}, {})",
                args[0], args[1], args[0]
            ))
        }
        "typeof" => Err("typeof only maps in boolean isType rewrites".to_string()),
        "upper" | "truncate" | "padleft" | "padright" | "containsword" | "econtains"
        | "icontains" | "dateformat" | "durationformat" | "currencyformat" | "localtime"
        | "hash" | "meta" | "minby" | "maxby" | "product" | "reduce" | "extract" | "firstvalue"
        | "nonnull" | "display" | "elink" | "all" | "any" | "none" => {
            Err(format!("unsupported DQL function {name}"))
        }
        _ => Ok(format!("{name}({})", args.join(", "))),
    }
}

fn map_lambda_list_expr(
    name: &str,
    original_args: &str,
    args: &[String],
    method: &str,
    regex_token: &str,
) -> Result<String, String> {
    require_arg_count(name, args, 2)?;
    let raw_args = split_top_level(original_args, ',');
    let lambda = raw_args
        .get(1)
        .ok_or_else(|| format!("{name} requires a lambda"))?
        .trim();
    let Some((param, body)) = split_single_arg_lambda(lambda) else {
        return Err(format!("unsupported DQL lambda in {name}"));
    };
    let body = replace_word(body, param, "value");
    let body = translate_expr_with_token(&body, false, regex_token)?;
    Ok(format!("({}).{method}({body})", args[0]))
}

fn start_of_week_expr() -> String {
    "(today() - duration(((number(today().format(\"E\")) - 1).toString() + \"d\")))".to_string()
}

fn end_of_week_expr() -> String {
    "(today() + duration(((7 - number(today().format(\"E\"))).toString() + \"d\")))".to_string()
}

fn start_of_month_expr() -> String {
    "(today() - duration(((today().day - 1).toString() + \"d\")))".to_string()
}

fn end_of_month_expr() -> String {
    format!("({} + \"1M\" - \"1d\")", start_of_month_expr())
}

fn start_of_year_expr() -> String {
    format!(
        "({} - duration(((today().month - 1).toString() + \"M\")))",
        start_of_month_expr()
    )
}

fn end_of_year_expr() -> String {
    format!("({} + \"1y\" - \"1d\")", start_of_year_expr())
}

fn rewrite_string_repeat(source: &str) -> String {
    let Some(star) = find_top_level_operator(source, '*') else {
        return source.to_string();
    };
    let lhs = source[..star].trim();
    let rhs = source[star + 1..].trim();
    if is_quoted(lhs) {
        format!("({lhs}).repeat({rhs})")
    } else {
        source.to_string()
    }
}

fn split_single_arg_lambda(source: &str) -> Option<(&str, &str)> {
    let arrow = source.find("=>")?;
    let param = source[..arrow]
        .trim()
        .trim_start_matches('(')
        .trim_end_matches(')')
        .trim();
    if param.is_empty() || param.contains(',') {
        return None;
    }
    Some((param, source[arrow + 2..].trim()))
}

fn rewrite_special_fields(source: &str, task_context: bool) -> String {
    let mut out = replace_outside_strings(source, "file.cday", "file.ctime.date()");
    out = replace_outside_strings(&out, "file.mday", "file.mtime.date()");
    out = replace_outside_strings(&out, "file.link", "link(file.path)");
    out = replace_outside_strings(&out, "file.inlinks", "file.backlinks");
    out = replace_outside_strings(&out, "file.outlinks", "file.links");
    if task_context {
        for (from, to) in [
            ("completed", "(task.status == \"x\")"),
            ("checked", "(task.status != \" \")"),
            ("text", "task.text"),
            ("status", "task.status"),
            ("due", "task.due"),
            ("scheduled", "task.scheduled"),
        ] {
            out = replace_bare_word(&out, from, to);
        }
    }
    out
}

fn rewrite_boolean_words_and_equality(source: &str) -> String {
    let mut out = String::new();
    let bytes = source.as_bytes();
    let mut pos = 0usize;
    while pos < bytes.len() {
        let b = bytes[pos];
        if b == b'"' || b == b'\'' {
            pos = copy_quoted(source, pos, &mut out);
            continue;
        }
        if is_ident_start(b) {
            let start = pos;
            pos += 1;
            while bytes
                .get(pos)
                .is_some_and(|b| b.is_ascii_alphanumeric() || *b == b'_')
            {
                pos += 1;
            }
            let word = &source[start..pos];
            if word.eq_ignore_ascii_case("and") {
                out.push_str("&&");
            } else if word.eq_ignore_ascii_case("or") {
                out.push_str("||");
            } else {
                out.push_str(word);
            }
            continue;
        }
        if b == b'=' {
            if bytes.get(pos + 1) == Some(&b'=') {
                out.push_str("==");
                pos += 2;
            } else if pos > 0 && matches!(bytes[pos - 1], b'!' | b'<' | b'>') {
                out.push('=');
                pos += 1;
            } else {
                out.push_str("==");
                pos += 1;
            }
            continue;
        }
        out.push(b as char);
        pos += 1;
    }
    out
}

fn expr_filter(source: &str, offset: usize, len: usize) -> FilterNode {
    match parse_expr(source) {
        Ok(expr) => FilterNode::Stmt(expr),
        Err(_) => FilterNode::Stmt(unsupported_expr(
            source,
            "invalid source expression",
            span(offset, offset + len),
        )),
    }
}

fn unsupported_filter(raw: &str, reason: &str, span: Span) -> FilterNode {
    FilterNode::Stmt(unsupported_expr(raw, reason, span))
}

fn add_unsupported_filter(query: &mut SlateQuery, raw: &str, reason: &str, span: Span) {
    let mut filters = Vec::new();
    if let Some(filter) = query.filters.take() {
        filters.push(filter);
    }
    filters.push(unsupported_filter(raw, reason, span));
    query.filters = combine_filters(filters);
}

fn unsupported_expr(raw: &str, reason: &str, span: Span) -> Expr {
    Expr {
        span,
        kind: ExprKind::Unsupported {
            raw: raw.to_string(),
            reason: reason.to_string(),
        },
    }
}

fn combine_filters(mut filters: Vec<FilterNode>) -> Option<FilterNode> {
    filters.retain(|_| true);
    match filters.len() {
        0 => None,
        1 => filters.pop(),
        _ => Some(FilterNode::And(filters)),
    }
}

fn split_top_level(source: &str, delimiter: char) -> Vec<&str> {
    let mut parts = Vec::new();
    let mut start = 0usize;
    let mut depth = 0i32;
    let mut quote = None;
    let bytes = source.as_bytes();
    let mut pos = 0usize;
    while pos < bytes.len() {
        let ch = bytes[pos] as char;
        if let Some(q) = quote {
            if bytes[pos] == b'\\' {
                pos += 2;
                continue;
            }
            if ch == q {
                quote = None;
            }
            pos += 1;
            continue;
        }
        match ch {
            '"' | '\'' => quote = Some(ch),
            '(' | '[' | '{' => depth += 1,
            ')' | ']' | '}' => depth -= 1,
            _ if ch == delimiter && depth == 0 => {
                parts.push(&source[start..pos]);
                start = pos + ch.len_utf8();
            }
            _ => {}
        }
        pos += 1;
    }
    parts.push(&source[start..]);
    parts
}

fn find_top_level_keyword(source: &str, keyword: &str) -> Option<usize> {
    let bytes = source.as_bytes();
    let mut depth = 0i32;
    let mut quote = None;
    let mut pos = 0usize;
    while pos < bytes.len() {
        let ch = bytes[pos] as char;
        if let Some(q) = quote {
            if bytes[pos] == b'\\' {
                pos += 2;
                continue;
            }
            if ch == q {
                quote = None;
            }
            pos += 1;
            continue;
        }
        match ch {
            '"' | '\'' => quote = Some(ch),
            '(' | '[' | '{' => depth += 1,
            ')' | ']' | '}' => depth -= 1,
            _ if depth == 0 && keyword_at(source, pos, keyword) => return Some(pos),
            _ => {}
        }
        pos += 1;
    }
    None
}

fn find_top_level_operator(source: &str, operator: char) -> Option<usize> {
    let bytes = source.as_bytes();
    let mut depth = 0i32;
    let mut quote = None;
    let mut pos = 0usize;
    while pos < bytes.len() {
        let ch = source[pos..].chars().next()?;
        if let Some(q) = quote {
            if ch == '\\' {
                pos += ch.len_utf8();
                if pos < bytes.len() {
                    let escaped = source[pos..].chars().next()?;
                    pos += escaped.len_utf8();
                }
                continue;
            }
            if ch == q {
                quote = None;
            }
            pos += ch.len_utf8();
            continue;
        }
        match ch {
            '"' | '\'' => quote = Some(ch),
            '(' | '[' | '{' => depth += 1,
            ')' | ']' | '}' => depth -= 1,
            _ if depth == 0 && ch == operator => return Some(pos),
            _ => {}
        }
        pos += ch.len_utf8();
    }
    None
}

fn strip_keyword<'a>(source: &'a str, keyword: &str) -> Option<&'a str> {
    if !starts_with_keyword(source, keyword) {
        return None;
    }
    Some(source[keyword.len()..].trim_start())
}

fn strip_suffix_keyword<'a>(source: &'a str, keyword: &str) -> Option<&'a str> {
    let trimmed = source.trim_end();
    if trimmed.len() < keyword.len() {
        return None;
    }
    let start = trimmed.len() - keyword.len();
    if !keyword_at(trimmed, start, keyword) {
        return None;
    }
    Some(&trimmed[..start])
}

fn starts_with_keyword(source: &str, keyword: &str) -> bool {
    keyword_at(source, 0, keyword)
}

fn keyword_at(source: &str, pos: usize, keyword: &str) -> bool {
    let end = pos + keyword.len();
    if end > source.len() {
        return false;
    }
    if !source.as_bytes()[pos..end].eq_ignore_ascii_case(keyword.as_bytes()) {
        return false;
    }
    let before_ok = pos == 0
        || !source.as_bytes()[pos - 1].is_ascii_alphanumeric()
            && source.as_bytes()[pos - 1] != b'_';
    let after_ok = end == source.len()
        || !source.as_bytes()[end].is_ascii_alphanumeric() && source.as_bytes()[end] != b'_';
    before_ok && after_ok
}

fn find_matching_paren(source: &str, open: usize) -> Option<usize> {
    let bytes = source.as_bytes();
    let mut depth = 0i32;
    let mut quote = None;
    let mut pos = open;
    while pos < bytes.len() {
        let ch = bytes[pos] as char;
        if let Some(q) = quote {
            if bytes[pos] == b'\\' {
                pos += 2;
                continue;
            }
            if ch == q {
                quote = None;
            }
            pos += 1;
            continue;
        }
        match ch {
            '"' | '\'' => quote = Some(ch),
            '(' => depth += 1,
            ')' => {
                depth -= 1;
                if depth == 0 {
                    return Some(pos);
                }
            }
            _ => {}
        }
        pos += 1;
    }
    None
}

fn copy_quoted(source: &str, start: usize, out: &mut String) -> usize {
    let bytes = source.as_bytes();
    let quote = bytes[start];
    let mut pos = start;
    out.push(quote as char);
    pos += 1;
    while pos < bytes.len() {
        let ch = source[pos..].chars().next().expect("pos is in bounds");
        out.push(ch);
        pos += ch.len_utf8();
        if ch == '\\' && pos < bytes.len() {
            let escaped = source[pos..].chars().next().expect("pos is in bounds");
            out.push(escaped);
            pos += escaped.len_utf8();
            continue;
        }
        if ch == quote as char {
            break;
        }
    }
    pos
}

fn replace_outside_strings(source: &str, from: &str, to: &str) -> String {
    let mut out = String::new();
    let bytes = source.as_bytes();
    let mut pos = 0usize;
    while pos < bytes.len() {
        let b = bytes[pos];
        if b == b'"' || b == b'\'' {
            pos = copy_quoted(source, pos, &mut out);
            continue;
        }
        if source
            .as_bytes()
            .get(pos..pos + from.len())
            .is_some_and(|candidate| candidate == from.as_bytes())
        {
            out.push_str(to);
            pos += from.len();
        } else {
            out.push(b as char);
            pos += 1;
        }
    }
    out
}

fn replace_word(source: &str, from: &str, to: &str) -> String {
    replace_word_with(source, from, to, |_, _| true)
}

fn replace_bare_word(source: &str, from: &str, to: &str) -> String {
    replace_word_with(source, from, to, |source, pos| {
        pos == 0 || source.as_bytes().get(pos - 1) != Some(&b'.')
    })
}

fn replace_word_with(
    source: &str,
    from: &str,
    to: &str,
    allow: impl Fn(&str, usize) -> bool,
) -> String {
    let mut out = String::new();
    let bytes = source.as_bytes();
    let mut pos = 0usize;
    while pos < bytes.len() {
        let b = bytes[pos];
        if b == b'"' || b == b'\'' {
            pos = copy_quoted(source, pos, &mut out);
            continue;
        }
        if keyword_at(source, pos, from) && allow(source, pos) {
            out.push_str(to);
            pos += from.len();
        } else {
            out.push(b as char);
            pos += 1;
        }
    }
    out
}

fn contains_field_ascii_ci(source: &str, field: &str) -> bool {
    let bytes = source.as_bytes();
    let field_bytes = field.as_bytes();
    let mut pos = 0usize;
    while pos < bytes.len() {
        if matches!(bytes[pos], b'"' | b'\'') {
            pos = skip_quoted(source, pos);
            continue;
        }
        let end = pos + field_bytes.len();
        if end <= bytes.len()
            && bytes[pos..end].eq_ignore_ascii_case(field_bytes)
            && field_start_boundary(bytes, pos)
            && field_end_boundary(bytes, end)
        {
            return true;
        }
        pos += 1;
    }
    false
}

fn contains_bare_word_ascii_ci(source: &str, word: &str) -> bool {
    contains_bare_word_ascii_ci_with(source, word, |_, _| true)
}

fn contains_bare_task_field_ascii_ci(source: &str, field: &str) -> bool {
    contains_bare_word_ascii_ci_with(source, field, |source, idx| {
        !is_followed_by_call(source, idx + field.len())
    })
}

fn contains_bare_word_ascii_ci_with(
    source: &str,
    word: &str,
    allow: impl Fn(&str, usize) -> bool,
) -> bool {
    let bytes = source.as_bytes();
    let mut pos = 0usize;
    while pos < bytes.len() {
        if matches!(bytes[pos], b'"' | b'\'') {
            pos = skip_quoted(source, pos);
            continue;
        }
        if keyword_at(source, pos, word)
            && (pos == 0 || source.as_bytes().get(pos - 1) != Some(&b'.'))
            && source
                .as_bytes()
                .get(pos + word.len())
                .is_none_or(|b| *b != b'.')
            && allow(source, pos)
        {
            return true;
        }
        pos += 1;
    }
    false
}

fn field_start_boundary(bytes: &[u8], pos: usize) -> bool {
    pos == 0 || !is_ident_continue(bytes[pos - 1]) && bytes[pos - 1] != b'.'
}

fn field_end_boundary(bytes: &[u8], end: usize) -> bool {
    end == bytes.len() || !is_ident_continue(bytes[end])
}

fn is_followed_by_call(source: &str, end: usize) -> bool {
    let bytes = source.as_bytes();
    let mut pos = end;
    while bytes.get(pos).is_some_and(|b| b.is_ascii_whitespace()) {
        pos += 1;
    }
    bytes.get(pos) == Some(&b'(')
}

fn skip_quoted(source: &str, start: usize) -> usize {
    let bytes = source.as_bytes();
    let quote = bytes[start];
    let mut pos = start + 1;
    while pos < bytes.len() {
        let ch = source[pos..].chars().next().expect("pos is in bounds");
        pos += ch.len_utf8();
        if ch == '\\' && pos < bytes.len() {
            let escaped = source[pos..].chars().next().expect("pos is in bounds");
            pos += escaped.len_utf8();
            continue;
        }
        if ch == quote as char {
            break;
        }
    }
    pos
}

fn parse_quoted(source: &str) -> Option<String> {
    let trimmed = source.trim();
    parse_quoted_with_len(trimmed).map(|(value, _)| value)
}

fn parse_quoted_with_len(source: &str) -> Option<(String, usize)> {
    let trimmed = source.trim_start();
    if trimmed.len() < 2 {
        return None;
    }
    let quote = trimmed.as_bytes()[0];
    if !matches!(quote, b'"' | b'\'') {
        return None;
    }
    let mut value = String::new();
    let mut pos = 1usize;
    while pos < trimmed.len() {
        let ch = trimmed[pos..].chars().next()?;
        pos += ch.len_utf8();
        if ch == quote as char {
            let leading = source.len() - trimmed.len();
            return Some((value, leading + pos));
        }
        if ch == '\\' {
            let escaped = trimmed[pos..].chars().next()?;
            pos += escaped.len_utf8();
            match escaped {
                'n' => value.push('\n'),
                'r' => value.push('\r'),
                't' => value.push('\t'),
                '"' => value.push('"'),
                '\'' => value.push('\''),
                '\\' => value.push('\\'),
                other => {
                    value.push('\\');
                    value.push(other);
                }
            }
        } else {
            value.push(ch);
        }
    }
    None
}

fn quote_expr_string(value: &str) -> String {
    format!("\"{}\"", value.replace('\\', "\\\\").replace('"', "\\\""))
}

fn looks_like_iso_date(source: &str) -> bool {
    let bytes = source.as_bytes();
    bytes.len() == 10
        && bytes[0..4].iter().all(u8::is_ascii_digit)
        && bytes[4] == b'-'
        && bytes[5..7].iter().all(u8::is_ascii_digit)
        && bytes[7] == b'-'
        && bytes[8..10].iter().all(u8::is_ascii_digit)
}

fn is_quoted(source: &str) -> bool {
    matches!(source.as_bytes().first(), Some(b'"' | b'\''))
}

fn require_arg_count(name: &str, args: &[String], expected: usize) -> Result<(), String> {
    if args.len() == expected {
        Ok(())
    } else {
        Err(format!("{name} expects {expected} argument(s)"))
    }
}

fn require_arg_count_at_least(name: &str, args: &[String], expected: usize) -> Result<(), String> {
    if args.len() >= expected {
        Ok(())
    } else {
        Err(format!("{name} expects at least {expected} argument(s)"))
    }
}

fn is_ident_start(b: u8) -> bool {
    b.is_ascii_alphabetic() || b == b'_'
}

fn is_ident_continue(b: u8) -> bool {
    b.is_ascii_alphanumeric() || b == b'_'
}

fn line_span(line: DqlLine<'_>) -> Span {
    span(line.start, line.start + line.text.len())
}

fn span(start: usize, end: usize) -> Span {
    Span {
        start: start as u32,
        end: end as u32,
    }
}

fn push_warning(
    warnings: &mut Vec<DqlWarning>,
    kind: DqlWarningKind,
    message: impl Into<String>,
    span: Span,
) {
    warnings.push(DqlWarning {
        kind,
        message: message.into(),
        span,
    });
}
