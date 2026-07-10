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
//! | `file.etags`, `file.lists`, `file.frontmatter`, `file.day`, `file.starred`, `file.tasks` | `Unsupported` | fail-loud |
//! | Bases-only `file.basename/properties/links/backlinks/embeds/file/inDegree/outDegree` (also under `this.file`) | `Unsupported`; never silently broaden DQL | fail-loud |
//! | TASK `completed`, `checked` | `task.completed` (`x` or `X`), non-empty/non-space `task.status` | supported |
//! | TASK `created`, `completion`, `start`, `fullyCompleted`, `children`, `section` | `Unsupported` | fail-loud |
//! | `contains`, `lower`, `replace`, `join`, `length` | corresponding method (`join` defaults to `", "`) | supported |
//! | `sort`, `reverse`, `unique`, `flat`, `slice`, `filter`, `map` | corresponding list operation; `flat` uses JS depth coercion with a 256 safety cap | supported |
//! | `sum`, `average`, `min`, `max` | Bases aggregate with exactly one list-shaped argument | supported |
//! | `startswith`, `endswith`, `round`, `trunc`, `floor`, `ceil` | corresponding method | supported |
//! | `regextest`, `regexmatch`, `regexreplace`, `split` | expression-valued regexes; whole-string match, JS replacement tokens, capture-splicing split | supported |
//! | `substring`, `striptime`, `choice`, `default`, boolean `typeof` comparison | `slice`, `date`, `if`, null-only `if`, `isType` | supported |
//! | `number`, `string`, one-argument `date`, `dur`, `link`, `object`, variadic `list`/`array`, `embed` | corresponding constructor (`dur` -> `duration`, `embed` -> `link`) | supported |
//! | Dataview-declared vectorized positions | conditional/list-map expansion, zipping multiple lists to the shortest | supported |
//! | list-valued first argument to `split` | `Unsupported` (`split` is not vectorized in Dataview) | fail-loud |
//! | two-argument `date(text, luxonFormat)`, `ldefault`, standalone `typeof` | `Unsupported` until parity semantics exist | fail-loud |
//! | `upper`, `truncate`, `padleft`, `padright`, `containsword`, `econtains`, `icontains` | `Unsupported` | fail-loud |
//! | `dateformat`, `durationformat`, `currencyformat`, `localtime`, `hash`, `meta` | `Unsupported` | fail-loud |
//! | `minby`, `maxby`, `product`, `reduce`, `extract`, `firstvalue`, `nonnull`, `display`, `elink`, predicate `all`/`any`/`none` | `Unsupported` | fail-loud |
//! | any function name outside the pinned DQL inventory | `Unsupported`; native Bases globals are not admitted by accident | fail-loud |
//! | expression `[[wikilink]]` / `[[wikilink]].field` | `Unsupported` until link-literal indexing has a parity-safe target | fail-loud |

use super::expr::{
    Callee, Expr, ExprKind, FileField, GlobalFn, Lit, MethodName, PropertyRef, Span, parse_expr,
};
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

fn subslice_offset(parent: &str, child: &str) -> usize {
    let parent_start = parent.as_ptr() as usize;
    let child_start = child.as_ptr() as usize;
    debug_assert!(child_start >= parent_start);
    debug_assert!(child_start + child.len() <= parent_start + parent.len());
    child_start - parent_start
}

fn line_slice_offset(line: DqlLine<'_>, slice: &str) -> usize {
    line.start + subslice_offset(line.text, slice)
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
const DQL_REGEX_OBJECT_KEY: &str = "\u{f8ff}slate.dql.regex";
const DQL_REGEX_MODE_KEY: &str = "\u{f8ff}slate.dql.regex.mode";
const DQL_AGGREGATE_OBJECT_KEY: &str = "\u{f8ff}slate.dql.list-aggregate";
const DQL_NUMBER_OBJECT_KEY: &str = "\u{f8ff}slate.dql.number";
const DQL_LENGTH_OBJECT_KEY: &str = "\u{f8ff}slate.dql.length";
const DQL_LITERAL_REPLACE_OBJECT_KEY: &str = "\u{f8ff}slate.dql.literal-replace";
const DQL_SUBSTRING_OBJECT_KEY: &str = "\u{f8ff}slate.dql.substring";
const DQL_LIST_METHOD_OBJECT_KEY: &str = "\u{f8ff}slate.dql.list-method";
const DQL_JOIN_OBJECT_KEY: &str = "\u{f8ff}slate.dql.join";
const DQL_SORT_OBJECT_KEY: &str = "\u{f8ff}slate.dql.sort";
const DQL_COMMAND_SORT_OBJECT_KEY: &str = "\u{f8ff}slate.dql.command-sort";
const DQL_CONTAINS_OBJECT_KEY: &str = "\u{f8ff}slate.dql.contains";
const DQL_REVERSE_OBJECT_KEY: &str = "\u{f8ff}slate.dql.reverse";
const DQL_MULTIPLY_OBJECT_KEY: &str = "\u{f8ff}slate.dql.multiply";
const DQL_EQUALITY_OBJECT_KEY: &str = "\u{f8ff}slate.dql.equality";
const DQL_ARITHMETIC_OBJECT_KEY: &str = "\u{f8ff}slate.dql.arithmetic";
const DQL_TEXT_METHOD_OBJECT_KEY: &str = "\u{f8ff}slate.dql.text-method";
const DQL_NUMBER_METHOD_OBJECT_KEY: &str = "\u{f8ff}slate.dql.number-method";
const DQL_ORDERING_OBJECT_KEY: &str = "\u{f8ff}slate.dql.ordering";
const DQL_TAGS_OBJECT_KEY: &str = "\u{f8ff}slate.dql.tags";
const DQL_STRING_OBJECT_KEY: &str = "\u{f8ff}slate.dql.string";
const DQL_TRUTHY_OBJECT_KEY: &str = "\u{f8ff}slate.dql.truthy";
const DQL_CHOICE_OBJECT_KEY: &str = "\u{f8ff}slate.dql.choice";
const DQL_DEFAULT_OBJECT_KEY: &str = "\u{f8ff}slate.dql.default";
const DQL_EMBED_OBJECT_KEY: &str = "\u{f8ff}slate.dql.embed";
const DQL_ROW_PROPERTY_OBJECT_KEY: &str = "\u{f8ff}slate.dql.row-property";
const DQL_DATE_OBJECT_KEY: &str = "\u{f8ff}slate.dql.date";
const DQL_DURATION_OBJECT_KEY: &str = "\u{f8ff}slate.dql.duration";
const DQL_LINK_OBJECT_KEY: &str = "\u{f8ff}slate.dql.link";
const DQL_OBJECT_CONSTRUCTOR_KEY: &str = "\u{f8ff}slate.dql.object";
const DQL_FILE_NAME_OBJECT_KEY: &str = "\u{f8ff}slate.dql.file-name";
const DQL_OUTLINKS_OBJECT_KEY: &str = "\u{f8ff}slate.dql.outlinks";
const DQL_INLINKS_OBJECT_KEY: &str = "\u{f8ff}slate.dql.inlinks";
const DQL_STRIPTIME_OBJECT_KEY: &str = "\u{f8ff}slate.dql.striptime";
const DQL_ALIASES_OBJECT_KEY: &str = "\u{f8ff}slate.dql.aliases";
const DQL_DATA_ARRAY_OBJECT_KEY: &str = "\u{f8ff}slate.dql.data-array";
const DQL_EXPANSION_DEPTH_LIMIT: usize = 64;
const DQL_EXPANSION_SIZE_LIMIT: usize = 65_536;

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

fn unique_internal_identifier(source: &str, stem: &str) -> String {
    let mut id = 0usize;
    loop {
        let token = format!("{stem}_{id}");
        if !source.contains(&token) {
            return token;
        }
        id = id
            .checked_add(1)
            .expect("DQL internal identifier namespace exhausted");
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
            let source = rest.trim();
            let parsed = parse_source(source, line_slice_offset(line, source), &mut warnings);
            query.source = parsed.source.unwrap_or(QuerySource::All);
            if let Some(filter) = parsed.filter {
                filters.push(filter);
            }
            continue;
        }

        saw_data_command = true;
        if let Some(rest) = strip_keyword(line.text, "WHERE") {
            command_sequence.push(CommandKind::Where);
            let source = rest.trim();
            let start = line_slice_offset(line, source);
            let expr = convert_expr_or_unsupported(
                source,
                query.row_source == RowSource::Tasks,
                start,
                &mut warnings,
            );
            filters.push(FilterNode::Stmt(dql_truthy_call_expr(
                expr,
                span(start, start + source.len()),
            )));
        } else if let Some(rest) = strip_keyword(line.text, "SORT") {
            command_sequence.push(CommandKind::Sort);
            let source = rest.trim();
            parse_sort(
                source,
                line_slice_offset(line, source),
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
    let columns_offset = line_slice_offset(line, columns_source);
    for (idx, column) in parse_columns(columns_source, columns_offset)
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
        let expression = first.trim();
        let start = line_slice_offset(line, expression);
        let expr = convert_expr_or_unsupported(expression, false, start, warnings);
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
            let local_start = subslice_offset(source, raw) + subslice_offset(raw, expression);
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
    offset: usize,
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
        let expression = expr_source.trim();
        let start = offset + subslice_offset(source, raw) + subslice_offset(raw, expression);
        let expr = convert_expr_or_unsupported(expression, task_context, start, warnings);
        let expr_span = expr.span;
        query.sort.push(SortKey {
            expr: dql_marker_expr(DQL_COMMAND_SORT_OBJECT_KEY, expr, expr_span),
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
    match parser.parse_binary() {
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
    fn parse_binary(&mut self) -> Option<FilterNode> {
        // Dataview's source grammar gives `and` and `or` the same precedence
        // and folds them from left to right. Parentheses are the only way to
        // override that ordering.
        let mut node = self.parse_unary()?;
        loop {
            self.skip_ws();
            let op = if self.consume_word("and") {
                Some(true)
            } else if self.consume_word("or") {
                Some(false)
            } else {
                None
            };
            let Some(and) = op else {
                break;
            };
            let rhs = self.parse_unary()?;
            node = if and {
                FilterNode::And(vec![node, rhs])
            } else {
                FilterNode::Or(vec![node, rhs])
            };
        }
        Some(node)
    }

    fn parse_unary(&mut self) -> Option<FilterNode> {
        self.skip_ws();
        if self.consume_byte(b'!') || self.consume_byte(b'-') {
            return Some(FilterNode::Not(vec![self.parse_unary()?]));
        }
        if self.consume_byte(b'(') {
            let node = self.parse_binary()?;
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
        let link = dql_wikilink_path(self.source[start..end].trim());
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
    Some(dql_wikilink_path(inner[2..inner.len() - 2].trim()))
}

fn dql_wikilink_path(link: &str) -> String {
    let separator = link
        .char_indices()
        .find(|(index, ch)| {
            *ch == '|'
                && link[..*index]
                    .chars()
                    .rev()
                    .take_while(|character| *character == '\\')
                    .count()
                    .is_multiple_of(2)
        })
        .map(|(index, _)| index);
    let path = separator.map(|index| &link[..index]).unwrap_or(link);
    path.split_once('#')
        .map(|(path, _)| path)
        .unwrap_or(path)
        .trim()
        .replace("\\|", "|")
        .to_string()
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
                rewrite_dql_binary_provenance(&mut expr);
                expr.span = span(offset, offset + source.len());
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
        ExprKind::Unary { op, rhs } => {
            if *op == super::expr::UnaryOp::Neg
                && !matches!(rhs.kind, ExprKind::Lit(Lit::Number(_)))
            {
                reasons.push("DQL unary minus is supported only for numeric literals".to_string());
            }
            collect_unsupported_reasons(rhs, reasons);
        }
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
                        receiver.kind = ExprKind::Lit(Lit::Object(vec![
                            (
                                DQL_REGEX_OBJECT_KEY.to_string(),
                                Expr {
                                    kind: ExprKind::Lit(Lit::String(pattern)),
                                    span: receiver.span,
                                },
                            ),
                            (
                                DQL_REGEX_MODE_KEY.to_string(),
                                Expr {
                                    kind: ExprKind::Lit(Lit::String("search".to_string())),
                                    span: receiver.span,
                                },
                            ),
                        ]));
                    } else {
                        restore_dql_regex_literals(receiver, regex_token);
                    }
                }
                Callee::Method { receiver, name } => {
                    restore_dql_regex_literals(receiver, regex_token);
                    if matches!(*name, MethodName::Replace | MethodName::Split)
                        && let Some(pattern) = args.first_mut()
                        && let ExprKind::Lit(Lit::String(placeholder)) = &pattern.kind
                        && let Some(value) = placeholder
                            .strip_prefix(regex_token)
                            .and_then(|value| value.strip_suffix(DQL_REGEX_PLACEHOLDER_SUFFIX))
                    {
                        let value = value.to_string();
                        pattern.kind = if *name == MethodName::Split {
                            ExprKind::Lit(Lit::Object(vec![
                                (
                                    DQL_REGEX_OBJECT_KEY.to_string(),
                                    Expr {
                                        kind: ExprKind::Lit(Lit::String(value)),
                                        span: pattern.span,
                                    },
                                ),
                                (
                                    DQL_REGEX_MODE_KEY.to_string(),
                                    Expr {
                                        kind: ExprKind::Lit(Lit::String("split".to_string())),
                                        span: pattern.span,
                                    },
                                ),
                            ]))
                        } else {
                            ExprKind::Lit(Lit::Regex {
                                pattern: value,
                                flags: "g".to_string(),
                            })
                        };
                    }
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
    if contains_single_quoted_string(source) {
        return Err("DQL strings must use double quotes".to_string());
    }
    if contains_unsupported_unary_minus(source) {
        return Err("DQL unary minus is supported only for numeric literals".to_string());
    }
    if contains_dql_wikilink_expression(source) {
        return Err("DQL wikilink expression literals are unsupported".to_string());
    }
    let rewritten = rewrite_dql_row_access(source)?;
    let rewritten = rewrite_hyphenated_identifiers(&rewritten);
    let rewritten = rewrite_dql_file_index_access(&rewritten)?;
    let typeof_token = unique_internal_identifier(&rewritten, "__slate_dql_typeof");
    let rewritten = rewrite_typeof_comparisons(&rewritten, &typeof_token)?;
    let rewritten = rewrite_function_calls(&rewritten, regex_token, &typeof_token)?;
    check_unsupported_fields(&rewritten, task_context)?;
    let rewritten = rewrite_special_fields(&rewritten, task_context);
    let rewritten = rewrite_boolean_words_and_equality(&rewritten);
    Ok(rewritten)
}

fn rewrite_dql_row_access(source: &str) -> Result<String, String> {
    let bytes = source.as_bytes();
    let mut out = String::with_capacity(source.len());
    let mut pos = 0usize;
    while pos < bytes.len() {
        if matches!(bytes[pos], b'"' | b'\'') {
            pos = copy_quoted(source, pos, &mut out);
            continue;
        }
        if bytes[pos] == b'/' && dql_slash_starts_regex(source, pos) {
            let start = pos;
            pos += 1;
            while pos < bytes.len() {
                if bytes[pos] == b'\\' {
                    pos = (pos + 2).min(bytes.len());
                } else if bytes[pos] == b'/' {
                    pos += 1;
                    while bytes.get(pos).is_some_and(u8::is_ascii_alphabetic) {
                        pos += 1;
                    }
                    break;
                } else {
                    pos += 1;
                }
            }
            out.push_str(&source[start..pos]);
            continue;
        }
        if !keyword_at(source, pos, "row")
            || source.as_bytes().get(pos.wrapping_sub(1)) == Some(&b'.')
        {
            let character = source[pos..]
                .chars()
                .next()
                .expect("position is at a character boundary");
            out.push(character);
            pos += character.len_utf8();
            continue;
        }

        let after = pos + 3;
        if bytes.get(after) == Some(&b'[') {
            let close = find_matching_bracket(source, after)
                .ok_or_else(|| "unterminated DQL row bracket access".to_string())?;
            let key_source = source[after + 1..close].trim();
            let Some((key, consumed)) = parse_quoted_with_len(key_source) else {
                return Err("dynamic DQL row bracket access is unsupported".to_string());
            };
            if !key_source[consumed..].trim().is_empty() {
                return Err("dynamic DQL row bracket access is unsupported".to_string());
            }
            out.push_str("note[");
            out.push_str(&quote_expr_string(&key));
            out.push(']');
            pos = close + 1;
            continue;
        }
        if bytes.get(after) == Some(&b'.') {
            let key_start = after + 1;
            if bytes
                .get(key_start)
                .is_some_and(|byte| is_ident_start(*byte))
            {
                let mut end = key_start + 1;
                while bytes.get(end).is_some_and(|byte| {
                    byte.is_ascii_alphanumeric() || matches!(*byte, b'_' | b'-')
                }) {
                    end += 1;
                }
                out.push_str("note[");
                out.push_str(&quote_expr_string(&source[key_start..end]));
                out.push(']');
                pos = end;
                continue;
            }
        }

        out.push_str("row");
        pos = after;
    }
    Ok(out)
}

fn rewrite_hyphenated_identifiers(source: &str) -> String {
    let bytes = source.as_bytes();
    let mut out = String::with_capacity(source.len());
    let mut pos = 0usize;
    while pos < bytes.len() {
        if matches!(bytes[pos], b'"' | b'\'') {
            pos = copy_quoted(source, pos, &mut out);
            continue;
        }
        if bytes[pos] == b'/' && dql_slash_starts_regex(source, pos) {
            let start = pos;
            pos += 1;
            while pos < bytes.len() {
                if bytes[pos] == b'\\' {
                    pos = (pos + 2).min(bytes.len());
                } else if bytes[pos] == b'/' {
                    pos += 1;
                    while bytes.get(pos).is_some_and(u8::is_ascii_alphabetic) {
                        pos += 1;
                    }
                    break;
                } else {
                    pos += 1;
                }
            }
            out.push_str(&source[start..pos]);
            continue;
        }
        if !is_ident_start(bytes[pos]) {
            let character = source[pos..]
                .chars()
                .next()
                .expect("position is at a character boundary");
            out.push(character);
            pos += character.len_utf8();
            continue;
        }

        let start = pos;
        pos += 1;
        let mut hyphenated = false;
        while bytes
            .get(pos)
            .is_some_and(|byte| byte.is_ascii_alphanumeric() || matches!(*byte, b'_' | b'-'))
        {
            hyphenated |= bytes[pos] == b'-';
            pos += 1;
        }
        let end = pos;
        if !hyphenated {
            out.push_str(&source[start..end]);
            continue;
        }

        let key = &source[start..end];
        if is_object_key_position(source, end) {
            out.push_str(&quote_expr_string(key));
        } else if start > 0 && bytes[start - 1] == b'.' {
            if out.ends_with('.') {
                out.pop();
            }
            out.push('[');
            out.push_str(&quote_expr_string(key));
            out.push(']');
        } else {
            out.push_str("note[");
            out.push_str(&quote_expr_string(key));
            out.push(']');
        }
        pos = end;
    }
    out
}

fn rewrite_dql_file_index_access(source: &str) -> Result<String, String> {
    const FIELDS: &[&str] = &[
        "name",
        "path",
        "folder",
        "ext",
        "size",
        "ctime",
        "cday",
        "mtime",
        "mday",
        "tags",
        "etags",
        "inlinks",
        "outlinks",
        "link",
        "aliases",
        "lists",
        "frontmatter",
        "day",
        "starred",
        "tasks",
    ];

    let bytes = source.as_bytes();
    let mut out = String::with_capacity(source.len());
    let mut pos = 0usize;
    while pos < bytes.len() {
        if matches!(bytes[pos], b'"' | b'\'') {
            pos = copy_quoted(source, pos, &mut out);
            continue;
        }
        if bytes[pos] == b'/' && dql_slash_starts_regex(source, pos) {
            let start = pos;
            pos += 1;
            while pos < bytes.len() {
                if bytes[pos] == b'\\' {
                    pos = (pos + 2).min(bytes.len());
                } else if bytes[pos] == b'/' {
                    pos += 1;
                    while bytes.get(pos).is_some_and(u8::is_ascii_alphabetic) {
                        pos += 1;
                    }
                    break;
                } else {
                    pos += 1;
                }
            }
            out.push_str(&source[start..pos]);
            continue;
        }

        let candidate = if keyword_at(source, pos, "this")
            && (pos == 0 || source.as_bytes().get(pos - 1) != Some(&b'.'))
            && source.as_bytes().get(pos + 4) == Some(&b'.')
            && keyword_at(source, pos + 5, "file")
        {
            Some(("this.file", pos + 9))
        } else if keyword_at(source, pos, "file")
            && (pos == 0 || source.as_bytes().get(pos - 1) != Some(&b'.'))
        {
            Some(("file", pos + 4))
        } else {
            None
        };
        let Some((prefix, after_prefix)) = candidate else {
            let character = source[pos..]
                .chars()
                .next()
                .expect("position is at a character boundary");
            out.push(character);
            pos += character.len_utf8();
            continue;
        };
        let mut open = after_prefix;
        while bytes.get(open).is_some_and(u8::is_ascii_whitespace) {
            open += 1;
        }
        if bytes.get(open) != Some(&b'[') {
            out.push_str(prefix);
            pos = after_prefix;
            continue;
        }
        let close = find_matching_bracket(source, open)
            .ok_or_else(|| "unterminated DQL file bracket access".to_string())?;
        let key_source = source[open + 1..close].trim();
        let Some((key, consumed)) = parse_quoted_with_len(key_source) else {
            return Err("dynamic DQL file bracket access is unsupported".to_string());
        };
        if !key_source[consumed..].trim().is_empty() {
            return Err("dynamic DQL file bracket access is unsupported".to_string());
        }
        let canonical = FIELDS
            .iter()
            .find(|field| field.eq_ignore_ascii_case(&key))
            .ok_or_else(|| format!("unsupported DQL field {prefix}.{key}"))?;
        out.push_str(prefix);
        out.push('.');
        out.push_str(canonical);
        pos = close + 1;
    }
    Ok(out)
}

fn contains_dql_wikilink_expression(source: &str) -> bool {
    let bytes = source.as_bytes();
    let mut pos = 0usize;
    while pos < bytes.len() {
        if matches!(bytes[pos], b'"' | b'\'') {
            pos = skip_quoted(source, pos);
            continue;
        }
        if bytes[pos] == b'/' && dql_slash_starts_regex(source, pos) {
            pos += 1;
            while pos < bytes.len() {
                if bytes[pos] == b'\\' {
                    pos = (pos + 2).min(bytes.len());
                    continue;
                }
                if bytes[pos] == b'/' {
                    pos += 1;
                    while bytes.get(pos).is_some_and(u8::is_ascii_alphabetic) {
                        pos += 1;
                    }
                    break;
                }
                pos += 1;
            }
            continue;
        }
        if bytes.get(pos..pos + 2) == Some(b"[[") && dql_double_bracket_starts_wikilink(source, pos)
        {
            return true;
        }
        pos += 1;
    }
    false
}

fn dql_double_bracket_starts_wikilink(source: &str, start: usize) -> bool {
    let bytes = source.as_bytes();
    let mut inner = start + 2;
    while let Some(byte) = bytes.get(inner) {
        if *byte == b'[' {
            return false;
        }
        if *byte == b']' {
            return bytes.get(inner + 1) == Some(&b']');
        }
        inner += 1;
    }
    false
}

fn dql_slash_starts_regex(source: &str, slash: usize) -> bool {
    let prefix = source[..slash].trim_end();
    let Some(previous) = prefix.as_bytes().last().copied() else {
        return true;
    };
    if matches!(
        previous,
        b'(' | b'['
            | b'{'
            | b','
            | b'='
            | b'!'
            | b'?'
            | b':'
            | b'&'
            | b'|'
            | b'+'
            | b'-'
            | b'*'
            | b'%'
            | b'<'
            | b'>'
    ) {
        return true;
    }
    prefix
        .split(|character: char| !character.is_ascii_alphanumeric() && character != '_')
        .next_back()
        .is_some_and(|word| word.eq_ignore_ascii_case("and") || word.eq_ignore_ascii_case("or"))
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
        "file.tasks",
    ] {
        if contains_field_ascii_ci(source, field) {
            return Err(format!("unsupported DQL field {field}"));
        }
    }
    for field in [
        "file.basename",
        "file.properties",
        "file.links",
        "file.backlinks",
        "file.embeds",
        "file.file",
        "file.inDegree",
        "file.outDegree",
    ] {
        if contains_field_ascii_ci(source, field) {
            return Err(format!("unsupported DQL field {field}"));
        }
    }
    for field in [
        "this.file.etags",
        "this.file.lists",
        "this.file.frontmatter",
        "this.file.day",
        "this.file.starred",
        "this.file.tasks",
    ] {
        if contains_field_ascii_ci(source, field) {
            return Err(format!("unsupported DQL field {field}"));
        }
    }
    for field in [
        "this.file.basename",
        "this.file.properties",
        "this.file.links",
        "this.file.backlinks",
        "this.file.embeds",
        "this.file.file",
        "this.file.inDegree",
        "this.file.outDegree",
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

fn rewrite_function_calls(
    source: &str,
    regex_token: &str,
    typeof_token: &str,
) -> Result<String, String> {
    rewrite_function_calls_inner(source, regex_token, typeof_token, 0)
}

fn rewrite_function_calls_inner(
    source: &str,
    regex_token: &str,
    typeof_token: &str,
    depth: usize,
) -> Result<String, String> {
    if depth > DQL_EXPANSION_DEPTH_LIMIT || source.len() > DQL_EXPANSION_SIZE_LIMIT {
        return Err("DQL expression expansion exceeds Slate's safe conversion limit".to_string());
    }
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
            if out.len() > DQL_EXPANSION_SIZE_LIMIT {
                return Err(
                    "DQL expression expansion exceeds Slate's safe conversion limit".to_string(),
                );
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
            if name.eq_ignore_ascii_case("and") || name.eq_ignore_ascii_case("or") {
                out.push_str(name);
                continue;
            }
            if bytes.get(probe) == Some(&b'(') {
                let Some(close) = find_matching_paren(source, probe) else {
                    return Err(format!("unterminated function call {name}"));
                };
                let args_src = &source[probe + 1..close];
                let args = split_top_level(args_src, ',')
                    .into_iter()
                    .map(|arg| {
                        rewrite_function_calls_inner(
                            arg.trim(),
                            regex_token,
                            typeof_token,
                            depth + 1,
                        )
                    })
                    .collect::<Result<Vec<_>, _>>()?;
                let is_method = source[..start]
                    .trim_end()
                    .as_bytes()
                    .last()
                    .is_some_and(|byte| *byte == b'.');
                if is_method {
                    return Err("DQL method-call syntax is unsupported".to_string());
                } else if name == typeof_token {
                    require_arg_count("typeof", &args, 2)?;
                    out.push_str(&format!("({}).isType({})", args[0], args[1]));
                } else {
                    let mapped = map_function_call(name, args_src, &args, regex_token)?;
                    if mapped.len() > DQL_EXPANSION_SIZE_LIMIT {
                        return Err(
                            "DQL expression expansion exceeds Slate's safe conversion limit"
                                .to_string(),
                        );
                    }
                    out.push_str(&mapped);
                }
                pos = close + 1;
            } else {
                let is_member = source[..start]
                    .trim_end()
                    .as_bytes()
                    .last()
                    .is_some_and(|byte| *byte == b'.');
                if matches!(name, "value" | "index")
                    && !is_member
                    && !is_object_key_position(source, pos)
                {
                    out.push_str("note[");
                    out.push_str(&quote_expr_string(name));
                    out.push(']');
                } else {
                    out.push_str(name);
                }
            }
            if out.len() > DQL_EXPANSION_SIZE_LIMIT {
                return Err(
                    "DQL expression expansion exceeds Slate's safe conversion limit".to_string(),
                );
            }
            continue;
        }
        out.push(b as char);
        pos += 1;
        if out.len() > DQL_EXPANSION_SIZE_LIMIT {
            return Err(
                "DQL expression expansion exceeds Slate's safe conversion limit".to_string(),
            );
        }
    }
    if out.len() > DQL_EXPANSION_SIZE_LIMIT {
        Err("DQL expression expansion exceeds Slate's safe conversion limit".to_string())
    } else {
        Ok(out)
    }
}

fn rewrite_typeof_comparisons(source: &str, typeof_token: &str) -> Result<String, String> {
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
                    "!{typeof_token}({}, {})",
                    arg,
                    quote_expr_string(&value.0)
                ));
            } else {
                out.push_str(&format!(
                    "{typeof_token}({}, {})",
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
    if name != lower {
        return Err("DQL function names are case-sensitive lowercase".to_string());
    }
    match lower.as_str() {
        "date" => {
            if args.len() == 2 {
                return Err("DQL date(text, luxonFormat) is unsupported".to_string());
            }
            require_arg_count(name, args, 1)?;
            let arg = original_args.trim();
            let shorthand_arg = arg
                .strip_prefix("note[\"")
                .and_then(|value| value.strip_suffix("\"]"))
                .unwrap_or(arg);
            Ok(match shorthand_arg {
                shorthand @ ("today" | "now" | "tomorrow" | "yesterday" | "sow" | "eow" | "som"
                | "eom" | "soy" | "eoy" | "start-of-week" | "end-of-week"
                | "start-of-month" | "end-of-month" | "start-of-year"
                | "end-of-year") => format!(
                    "date({})",
                    dql_marker_object_expr(DQL_DATE_OBJECT_KEY, &quote_expr_string(shorthand))
                ),
                raw if looks_like_iso_date(raw) => format!(
                    "date({})",
                    dql_marker_object_expr(DQL_DATE_OBJECT_KEY, &quote_expr_string(raw))
                ),
                _ => map_vectorized_function(args, &[0], &|values| {
                    format!(
                        "date({})",
                        dql_marker_object_expr(DQL_DATE_OBJECT_KEY, &values[0])
                    )
                }),
            })
        }
        "dur" => {
            require_arg_count(name, args, 1)?;
            let arg = original_args.trim();
            if !is_quoted(arg) && parse_expr(arg).is_err() {
                Ok(format!(
                    "duration({})",
                    dql_marker_object_expr(DQL_DURATION_OBJECT_KEY, &quote_expr_string(arg))
                ))
            } else {
                Ok(map_vectorized_function(args, &[0], &|values| {
                    format!(
                        "duration({})",
                        dql_marker_object_expr(DQL_DURATION_OBJECT_KEY, &values[0])
                    )
                }))
            }
        }
        "number" => {
            require_arg_count(name, args, 1)?;
            Ok(map_vectorized_function(args, &[0], &|values| {
                format!(
                    "number({})",
                    dql_marker_object_expr(DQL_NUMBER_OBJECT_KEY, &values[0])
                )
            }))
        }
        "link" => {
            if !(1..=3).contains(&args.len()) {
                return Err(format!("{name} expects 1, 2, or 3 arguments"));
            }
            let scalar = |values: &[String]| {
                let path = dql_marker_object_expr(DQL_LINK_OBJECT_KEY, &values[0]);
                match values {
                    [_, display, embed] => format!("link({path}, {display}, {embed})"),
                    [_, display] => format!("link({path}, {display})"),
                    _ => format!("link({path})"),
                }
            };
            if args.len() == 3 {
                if args
                    .iter()
                    .any(|arg| dql_expr_list_shape(arg) == DqlListShape::List)
                {
                    return Err("DQL three-argument link requires scalar arguments".to_string());
                }
                Ok(scalar(args))
            } else {
                let positions = (0..args.len()).collect::<Vec<_>>();
                Ok(map_vectorized_function(args, &positions, &scalar))
            }
        }
        "list" => Ok(format!("[{}]", args.join(", "))),
        "object" => Ok(if args.is_empty() {
            format!("object({})", quote_expr_string(DQL_OBJECT_CONSTRUCTOR_KEY))
        } else {
            format!(
                "object({}, {})",
                quote_expr_string(DQL_OBJECT_CONSTRUCTOR_KEY),
                args.join(", ")
            )
        }),
        "min" | "max" | "sum" | "average" => {
            if args.len() != 1 {
                return Err(format!(
                    "DQL {name} requires exactly one list-shaped argument"
                ));
            }
            match dql_expr_list_shape(&args[0]) {
                DqlListShape::NonList => Err(format!(
                    "DQL {name} requires exactly one list-shaped argument"
                )),
                DqlListShape::List | DqlListShape::Unknown => Ok(format!(
                    "{name}({})",
                    dql_marker_object_expr(DQL_AGGREGATE_OBJECT_KEY, &args[0])
                )),
            }
        }
        "array" => Ok(format!("[{}]", args.join(", "))),
        "embed" => {
            if !(1..=2).contains(&args.len()) {
                return Err(format!("{name} expects 1 or 2 arguments"));
            }
            let positions = (0..args.len()).collect::<Vec<_>>();
            Ok(map_vectorized_function(args, &positions, &|values| {
                let link = dql_marker_object_expr(DQL_EMBED_OBJECT_KEY, &values[0]);
                if let Some(embed) = values.get(1) {
                    format!("link({link}, {embed})")
                } else {
                    format!("link({link})")
                }
            }))
        }
        "string" => {
            require_arg_count(name, args, 1)?;
            Ok(format!(
                "({}).toString()",
                dql_marker_object_expr(DQL_STRING_OBJECT_KEY, &args[0])
            ))
        }
        "contains" => {
            require_arg_count(name, args, 2)?;
            Ok(map_vectorized_function(args, &[1], &|values| {
                format!(
                    "({}).contains({})",
                    dql_marker_object_expr(DQL_CONTAINS_OBJECT_KEY, &values[0]),
                    values[1]
                )
            }))
        }
        "lower" => {
            require_arg_count(name, args, 1)?;
            Ok(map_vectorized_function(args, &[0], &|values| {
                format!(
                    "({}).lower()",
                    dql_marker_object_expr(DQL_TEXT_METHOD_OBJECT_KEY, &values[0])
                )
            }))
        }
        "replace" => {
            require_arg_count(name, args, 3)?;
            Ok(map_vectorized_function(args, &[0, 1, 2], &|values| {
                format!(
                    "({}).replace({}, {})",
                    values[0],
                    dql_marker_object_expr(DQL_LITERAL_REPLACE_OBJECT_KEY, &values[1]),
                    values[2]
                )
            }))
        }
        "join" => {
            require_arg_count_at_least(name, args, 1)?;
            if args.len() > 2 {
                return Err(format!("{name} expects 1 or 2 arguments"));
            }
            let separator = args
                .get(1)
                .cloned()
                .unwrap_or_else(|| quote_expr_string(", "));
            let receiver = dql_marker_object_expr(DQL_JOIN_OBJECT_KEY, &args[0]);
            let mapped_args = vec![receiver, separator];
            Ok(map_vectorized_function(&mapped_args, &[1], &|values| {
                format!("({}).join({})", values[0], values[1])
            }))
        }
        "length" => {
            require_arg_count(name, args, 1)?;
            Ok(format!(
                "({}).length",
                dql_marker_object_expr(DQL_LENGTH_OBJECT_KEY, &args[0])
            ))
        }
        "sort" => {
            require_arg_count(name, args, 1)?;
            Ok(format!(
                "if(({}).isType(\"list\"), ({}).sort(), {})",
                args[0],
                dql_marker_object_expr(DQL_SORT_OBJECT_KEY, &args[0]),
                args[0]
            ))
        }
        "reverse" => {
            require_arg_count(name, args, 1)?;
            Ok(format!(
                "if(({}).isType(\"list\") || ({}).isType(\"string\"), ({}).reverse(), {})",
                args[0],
                args[0],
                dql_marker_object_expr(DQL_REVERSE_OBJECT_KEY, &args[0]),
                args[0]
            ))
        }
        "unique" => {
            require_arg_count(name, args, 1)?;
            let receiver = dql_list_method_receiver(name, &args[0])?;
            Ok(format!("({receiver}).unique()"))
        }
        "flat" => {
            require_arg_count_at_least(name, args, 1)?;
            if args.len() > 2 {
                return Err(format!("{name} expects 1 or 2 arguments"));
            }
            let receiver = dql_list_method_receiver(name, &args[0])?;
            if args.len() == 1 {
                return Ok(format!("({receiver}).flat()"));
            }
            let raw_args = split_top_level(original_args, ',');
            let raw_depth = raw_args.get(1).map(|arg| arg.trim()).unwrap_or_default();
            if let Ok(depth) = raw_depth.parse::<f64>() {
                if !depth.is_finite() {
                    return Err("DQL flat depth must be a finite number".to_string());
                }
                let depth = depth.max(0.0).trunc() as usize;
                if depth > 256 {
                    return Err("DQL flat depth exceeds Slate's safe conversion limit".to_string());
                }
                Ok(format!("({receiver}).flat({depth})"))
            } else {
                Ok(format!("({receiver}).flat({})", args[1]))
            }
        }
        "slice" => {
            if !(1..=3).contains(&args.len()) {
                return Err(format!("{name} expects 1, 2, or 3 arguments"));
            }
            let receiver = dql_list_method_receiver(name, &args[0])?;
            Ok(format!("({receiver}).slice({})", args[1..].join(", ")))
        }
        "substring" => {
            if !(2..=3).contains(&args.len()) {
                return Err(format!("{name} expects 2 or 3 arguments"));
            }
            let positions = (0..args.len()).collect::<Vec<_>>();
            Ok(map_vectorized_function(args, &positions, &|values| {
                format!(
                    "({}).slice({})",
                    dql_marker_object_expr(DQL_SUBSTRING_OBJECT_KEY, &values[0]),
                    values[1..].join(", ")
                )
            }))
        }
        "filter" => map_lambda_list_expr(name, original_args, args, "filter", regex_token),
        "map" => map_lambda_list_expr(name, original_args, args, "map", regex_token),
        "startswith" | "endswith" => {
            require_arg_count(name, args, 2)?;
            let target = if lower == "startswith" {
                "startsWith"
            } else {
                "endsWith"
            };
            Ok(map_vectorized_function(args, &[0, 1], &|values| {
                format!(
                    "({}).{target}({})",
                    dql_marker_object_expr(DQL_TEXT_METHOD_OBJECT_KEY, &values[0]),
                    values[1]
                )
            }))
        }
        "round" => {
            if !(1..=2).contains(&args.len()) {
                return Err(format!("{name} expects 1 or 2 arguments"));
            }
            Ok(map_vectorized_function(args, &[0], &|values| {
                format!(
                    "({}).round({})",
                    dql_marker_object_expr(DQL_NUMBER_METHOD_OBJECT_KEY, &values[0]),
                    values[1..].join(", ")
                )
            }))
        }
        "floor" | "ceil" | "trunc" => {
            require_arg_count(name, args, 1)?;
            Ok(map_vectorized_function(args, &[0], &|values| {
                format!(
                    "({}).{lower}()",
                    dql_marker_object_expr(DQL_NUMBER_METHOD_OBJECT_KEY, &values[0])
                )
            }))
        }
        "regextest" | "regexmatch" => {
            require_arg_count(name, args, 2)?;
            let raw_args = split_top_level(original_args, ',');
            let raw_pattern = raw_args.first().map(|arg| arg.trim()).unwrap_or_default();
            if let Some((pattern, _)) = parse_quoted_with_len(raw_pattern)
                .filter(|(_, consumed)| raw_pattern[*consumed..].trim().is_empty())
            {
                let pattern = normalize_dql_regex_pattern(name, &pattern)?;
                let mode = if lower == "regexmatch" {
                    "whole"
                } else {
                    "search"
                };
                // Keep the authored JavaScript-compatible pattern in the DQL
                // marker. Whole-string anchoring is applied only after runtime
                // normalization, so our synthesized Rust anchors are never
                // mistaken for unsupported authored syntax.
                let receiver = dql_regex_object_expr(&quote_expr_string(&pattern), mode);
                let mapped_args = vec![receiver, args[1].clone()];
                Ok(map_vectorized_function(&mapped_args, &[1], &|values| {
                    format!("({}).matches({})", values[0], values[1])
                }))
            } else {
                let mode = if lower == "regexmatch" {
                    "whole"
                } else {
                    "search"
                };
                Ok(map_vectorized_function(args, &[0, 1], &|values| {
                    format!(
                        "({}).matches({})",
                        dql_regex_object_expr(&values[0], mode),
                        values[1]
                    )
                }))
            }
        }
        "regexreplace" => {
            require_arg_count(name, args, 3)?;
            let raw_args = split_top_level(original_args, ',');
            let raw_pattern = raw_args.get(1).map(|arg| arg.trim()).unwrap_or_default();
            let mut mapped_args = args.to_vec();
            if let Some((pattern, _)) = parse_quoted_with_len(raw_pattern)
                .filter(|(_, consumed)| raw_pattern[*consumed..].trim().is_empty())
            {
                mapped_args[1] = quote_expr_string(&normalize_dql_regex_pattern(name, &pattern)?);
            }
            Ok(map_vectorized_function(
                &mapped_args,
                &[0, 1, 2],
                &|values| {
                    format!(
                        "({}).replace({}, {})",
                        values[0],
                        dql_regex_object_expr(&values[1], "global"),
                        values[2]
                    )
                },
            ))
        }
        "split" => {
            if !(2..=3).contains(&args.len()) {
                return Err(format!("{name} expects 2 or 3 arguments"));
            }
            if dql_expr_list_shape(&args[0]) == DqlListShape::List {
                return Err("DQL split expects scalar text".to_string());
            }
            let raw_args = split_top_level(original_args, ',');
            let raw_pattern = raw_args.get(1).map(|arg| arg.trim()).unwrap_or_default();
            let pattern = if let Some((pattern, _)) = parse_quoted_with_len(raw_pattern)
                .filter(|(_, consumed)| raw_pattern[*consumed..].trim().is_empty())
            {
                let pattern = normalize_dql_regex_pattern(name, &pattern)?;
                let placeholder = format!("{regex_token}{pattern}{DQL_REGEX_PLACEHOLDER_SUFFIX}");
                quote_expr_string(&placeholder)
            } else {
                dql_regex_object_expr(&args[1], "split")
            };
            let limit = args
                .get(2)
                .map(|value| format!(", {value}"))
                .unwrap_or_default();
            Ok(format!("({}).split({pattern}{limit})", args[0]))
        }
        "striptime" => {
            require_arg_count(name, args, 1)?;
            Ok(map_vectorized_function(args, &[0], &|values| {
                format!(
                    "({}).date()",
                    dql_marker_object_expr(DQL_STRIPTIME_OBJECT_KEY, &values[0])
                )
            }))
        }
        "choice" => {
            require_arg_count(name, args, 3)?;
            Ok(map_vectorized_function(args, &[0], &|values| {
                format!(
                    "if({}, {}, {})",
                    dql_marker_object_expr(
                        DQL_CHOICE_OBJECT_KEY,
                        &dql_marker_object_expr(DQL_TRUTHY_OBJECT_KEY, &values[0])
                    ),
                    values[1],
                    values[2]
                )
            }))
        }
        "default" => {
            require_arg_count(name, args, 2)?;
            Ok(map_vectorized_function(args, &[0, 1], &|values| {
                format!(
                    "if({}, {}, false)",
                    dql_marker_object_expr(DQL_DEFAULT_OBJECT_KEY, &values[0]),
                    values[1]
                )
            }))
        }
        "typeof" => Err("typeof only maps in boolean isType rewrites".to_string()),
        "upper" | "truncate" | "padleft" | "padright" | "containsword" | "econtains"
        | "icontains" | "dateformat" | "durationformat" | "currencyformat" | "localtime"
        | "hash" | "meta" | "minby" | "maxby" | "product" | "reduce" | "extract" | "firstvalue"
        | "nonnull" | "display" | "elink" | "ldefault" | "all" | "any" | "none" => {
            Err(format!("unsupported DQL function {name}"))
        }
        _ => Err(format!("unsupported DQL function {name}")),
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
enum DqlListShape {
    List,
    NonList,
    Unknown,
}

fn dql_expr_list_shape(source: &str) -> DqlListShape {
    parse_expr(source)
        .map(|expr| expr_list_shape(&expr))
        .unwrap_or(DqlListShape::Unknown)
}

fn expr_list_shape(expr: &Expr) -> DqlListShape {
    match &expr.kind {
        ExprKind::Lit(Lit::List(_)) | ExprKind::ListExpr { .. } => DqlListShape::List,
        ExprKind::Lit(_) => DqlListShape::NonList,
        ExprKind::Prop(PropertyRef::File(
            FileField::Tags
            | FileField::Aliases
            | FileField::Links
            | FileField::Backlinks
            | FileField::Embeds,
        )) => DqlListShape::List,
        ExprKind::Prop(PropertyRef::ThisFile(
            FileField::Tags
            | FileField::Aliases
            | FileField::Links
            | FileField::Backlinks
            | FileField::Embeds,
        )) => DqlListShape::List,
        ExprKind::Prop(
            PropertyRef::File(_) | PropertyRef::ThisFile(_) | PropertyRef::TaskField(_),
        ) => DqlListShape::NonList,
        ExprKind::Call {
            callee: Callee::Global(GlobalFn::If),
            args,
        } if args.len() == 3 => match (expr_list_shape(&args[1]), expr_list_shape(&args[2])) {
            (DqlListShape::List, DqlListShape::List) => DqlListShape::List,
            (DqlListShape::NonList, DqlListShape::NonList) => DqlListShape::NonList,
            _ => DqlListShape::Unknown,
        },
        ExprKind::Call {
            callee: Callee::Global(GlobalFn::List),
            ..
        }
        | ExprKind::Call {
            callee:
                Callee::Method {
                    name:
                        MethodName::Split | MethodName::Flat | MethodName::Sort | MethodName::Unique,
                    ..
                },
            ..
        } => DqlListShape::List,
        ExprKind::Call {
            callee:
                Callee::Global(
                    GlobalFn::Date
                    | GlobalFn::Duration
                    | GlobalFn::File
                    | GlobalFn::Link
                    | GlobalFn::Max
                    | GlobalFn::Min
                    | GlobalFn::Now
                    | GlobalFn::Number
                    | GlobalFn::Object
                    | GlobalFn::String
                    | GlobalFn::Sum
                    | GlobalFn::Average
                    | GlobalFn::Today,
                ),
            ..
        } => DqlListShape::NonList,
        ExprKind::Call {
            callee:
                Callee::Method {
                    name:
                        MethodName::IsTruthy
                        | MethodName::IsType
                        | MethodName::ToString
                        | MethodName::Date
                        | MethodName::Format
                        | MethodName::Time
                        | MethodName::Relative
                        | MethodName::IsEmpty
                        | MethodName::Contains
                        | MethodName::ContainsAll
                        | MethodName::ContainsAny
                        | MethodName::StartsWith
                        | MethodName::EndsWith
                        | MethodName::Lower
                        | MethodName::Title
                        | MethodName::Trim
                        | MethodName::Repeat
                        | MethodName::Abs
                        | MethodName::Ceil
                        | MethodName::Floor
                        | MethodName::Trunc
                        | MethodName::Round
                        | MethodName::ToFixed
                        | MethodName::Join
                        | MethodName::Matches,
                    ..
                },
            ..
        } => DqlListShape::NonList,
        ExprKind::Field { name, .. } if name == "length" => DqlListShape::NonList,
        ExprKind::Unary { .. } | ExprKind::Binary { .. } => DqlListShape::NonList,
        _ => DqlListShape::Unknown,
    }
}

fn map_vectorized_function(
    args: &[String],
    vector_positions: &[usize],
    scalar: &impl Fn(&[String]) -> String,
) -> String {
    build_vectorized_shape_branches(args, vector_positions, 0, &[], scalar)
}

fn build_vectorized_shape_branches(
    args: &[String],
    vector_positions: &[usize],
    cursor: usize,
    list_positions: &[usize],
    scalar: &impl Fn(&[String]) -> String,
) -> String {
    if cursor == vector_positions.len() {
        return build_vectorized_list_branch(args, list_positions, scalar);
    }
    let position = vector_positions[cursor];
    match dql_expr_list_shape(&args[position]) {
        DqlListShape::List => {
            let mut lists = list_positions.to_vec();
            lists.push(position);
            build_vectorized_shape_branches(args, vector_positions, cursor + 1, &lists, scalar)
        }
        DqlListShape::NonList => build_vectorized_shape_branches(
            args,
            vector_positions,
            cursor + 1,
            list_positions,
            scalar,
        ),
        DqlListShape::Unknown => {
            let mut lists = list_positions.to_vec();
            lists.push(position);
            let list_branch =
                build_vectorized_shape_branches(args, vector_positions, cursor + 1, &lists, scalar);
            let scalar_branch = build_vectorized_shape_branches(
                args,
                vector_positions,
                cursor + 1,
                list_positions,
                scalar,
            );
            format!(
                "if(({}).isType(\"list\"), {list_branch}, {scalar_branch})",
                args[position]
            )
        }
    }
}

fn build_vectorized_list_branch(
    args: &[String],
    list_positions: &[usize],
    scalar: &impl Fn(&[String]) -> String,
) -> String {
    if list_positions.is_empty() {
        return scalar(args);
    }
    if let Some(driver) = list_positions
        .iter()
        .map(|position| dql_expr_known_list_len(&args[*position]).map(|len| (*position, len)))
        .collect::<Option<Vec<_>>>()
        .and_then(|lengths| {
            lengths
                .into_iter()
                .min_by_key(|(position, len)| (*len, *position))
                .map(|(position, _)| position)
        })
    {
        let mut mapped_args = args.to_vec();
        for position in list_positions {
            mapped_args[*position] = if *position == driver {
                "value".to_string()
            } else {
                format!("({})[index]", args[*position])
            };
        }
        return format!("({}).map({})", args[driver], scalar(&mapped_args));
    }
    let candidates = list_positions
        .iter()
        .map(|driver| {
            let mut mapped_args = args.to_vec();
            for position in list_positions {
                mapped_args[*position] = if position == driver {
                    "value".to_string()
                } else {
                    format!("({})[index]", args[*position])
                };
            }
            format!("({}).map({})", args[*driver], scalar(&mapped_args))
        })
        .collect::<Vec<_>>();
    let mut selected = candidates
        .last()
        .cloned()
        .expect("list positions are non-empty");
    for (index, driver) in list_positions.iter().enumerate().rev().skip(1) {
        let shortest = list_positions
            .iter()
            .filter(|other| *other != driver)
            .map(|other| format!("({}).length <= ({}).length", args[*driver], args[*other]))
            .collect::<Vec<_>>()
            .join(" && ");
        selected = format!("if({shortest}, {}, {selected})", candidates[index]);
    }
    selected
}

fn dql_expr_known_list_len(source: &str) -> Option<usize> {
    let expr = parse_expr(source).ok()?;
    let ExprKind::Lit(Lit::List(items)) = expr.kind else {
        return None;
    };
    Some(items.len())
}

fn dql_regex_object_expr(pattern: &str, mode: &str) -> String {
    format!(
        "object({}, {pattern}, {}, {})",
        quote_expr_string(DQL_REGEX_OBJECT_KEY),
        quote_expr_string(DQL_REGEX_MODE_KEY),
        quote_expr_string(mode)
    )
}

fn dql_marker_object_expr(key: &str, value: &str) -> String {
    format!("object({}, {value})", quote_expr_string(key))
}

fn dql_list_method_receiver(name: &str, source: &str) -> Result<String, String> {
    match dql_expr_list_shape(source) {
        DqlListShape::NonList => Err(format!("DQL {name} requires a list-shaped argument")),
        DqlListShape::List | DqlListShape::Unknown => {
            Ok(dql_marker_object_expr(DQL_LIST_METHOD_OBJECT_KEY, source))
        }
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
    if contains_unquoted_arrow(body) {
        return Err("nested DQL lambdas are unsupported".to_string());
    }
    let lambda_token = unique_internal_identifier(body, "__slate_dql_lambda_value");
    let body = replace_bare_word(body, param, &lambda_token);
    let body = translate_expr_with_token(&body, false, regex_token)?;
    if parse_expr(&body)
        .map(|expr| expr_contains_capturing_list_expr(&expr, &lambda_token))
        .unwrap_or(false)
    {
        return Err("vectorized DQL functions inside lambdas are unsupported".to_string());
    }
    let body = replace_word(&body, &lambda_token, "value");
    let receiver = dql_list_method_receiver(name, &args[0])?;
    Ok(format!("({receiver}).{method}({body})"))
}

fn expr_contains_capturing_list_expr(expr: &Expr, property: &str) -> bool {
    match &expr.kind {
        ExprKind::ListExpr {
            base, body, init, ..
        } => {
            expr_references_note_property(body, property)
                || init
                    .as_deref()
                    .is_some_and(|init| expr_references_note_property(init, property))
                || expr_contains_capturing_list_expr(base, property)
                || expr_contains_capturing_list_expr(body, property)
                || init
                    .as_deref()
                    .is_some_and(|init| expr_contains_capturing_list_expr(init, property))
        }
        ExprKind::Call { callee, args } => {
            let receiver_has_list = match callee {
                Callee::Method { receiver, .. } => {
                    expr_contains_capturing_list_expr(receiver, property)
                }
                Callee::Global(_) => false,
            };
            receiver_has_list
                || args
                    .iter()
                    .any(|arg| expr_contains_capturing_list_expr(arg, property))
        }
        ExprKind::Lit(Lit::List(items)) => items
            .iter()
            .any(|item| expr_contains_capturing_list_expr(item, property)),
        ExprKind::Lit(Lit::Object(items)) => items
            .iter()
            .any(|(_, value)| expr_contains_capturing_list_expr(value, property)),
        ExprKind::Index { base, index }
        | ExprKind::Binary {
            lhs: base,
            rhs: index,
            ..
        } => {
            expr_contains_capturing_list_expr(base, property)
                || expr_contains_capturing_list_expr(index, property)
        }
        ExprKind::Field { base, .. } | ExprKind::Unary { rhs: base, .. } => {
            expr_contains_capturing_list_expr(base, property)
        }
        ExprKind::Lit(_) | ExprKind::Prop(_) | ExprKind::Unsupported { .. } => false,
    }
}

fn expr_references_note_property(expr: &Expr, property: &str) -> bool {
    match &expr.kind {
        ExprKind::Prop(PropertyRef::Note(name)) => name == property,
        ExprKind::Call { callee, args } => {
            let receiver_matches = match callee {
                Callee::Method { receiver, .. } => {
                    expr_references_note_property(receiver, property)
                }
                Callee::Global(_) => false,
            };
            receiver_matches
                || args
                    .iter()
                    .any(|arg| expr_references_note_property(arg, property))
        }
        ExprKind::Lit(Lit::List(items)) => items
            .iter()
            .any(|item| expr_references_note_property(item, property)),
        ExprKind::Lit(Lit::Object(items)) => items
            .iter()
            .any(|(_, value)| expr_references_note_property(value, property)),
        ExprKind::Index { base, index }
        | ExprKind::Binary {
            lhs: base,
            rhs: index,
            ..
        } => {
            expr_references_note_property(base, property)
                || expr_references_note_property(index, property)
        }
        ExprKind::Field { base, .. } | ExprKind::Unary { rhs: base, .. } => {
            expr_references_note_property(base, property)
        }
        ExprKind::ListExpr {
            base, body, init, ..
        } => {
            expr_references_note_property(base, property)
                || expr_references_note_property(body, property)
                || init
                    .as_deref()
                    .is_some_and(|init| expr_references_note_property(init, property))
        }
        ExprKind::Lit(_) | ExprKind::Prop(_) | ExprKind::Unsupported { .. } => false,
    }
}

fn rewrite_dql_binary_provenance(expr: &mut Expr) {
    let property = match &expr.kind {
        ExprKind::Prop(PropertyRef::Note(key)) => Some((false, key.clone())),
        ExprKind::Prop(PropertyRef::ThisNote(key)) => Some((true, key.clone())),
        ExprKind::Index { base, index }
            if matches!(base.kind, ExprKind::Prop(PropertyRef::This)) =>
        {
            match &index.kind {
                ExprKind::Lit(Lit::String(key)) => Some((true, key.clone())),
                _ => None,
            }
        }
        _ => None,
    };
    if let Some((this, key)) = property {
        let span = expr.span;
        expr.kind = ExprKind::Index {
            base: Box::new(Expr {
                kind: ExprKind::Lit(Lit::Object(vec![(
                    DQL_ROW_PROPERTY_OBJECT_KEY.to_string(),
                    Expr {
                        kind: ExprKind::Lit(Lit::Bool(this)),
                        span,
                    },
                )])),
                span,
            }),
            index: Box::new(Expr {
                kind: ExprKind::Lit(Lit::String(key)),
                span,
            }),
        };
        return;
    }

    match &mut expr.kind {
        ExprKind::Call { callee, args } => {
            if let Callee::Method { receiver, .. } = callee {
                rewrite_dql_binary_provenance(receiver);
            }
            args.iter_mut().for_each(rewrite_dql_binary_provenance);
        }
        ExprKind::Lit(Lit::List(items)) => items.iter_mut().for_each(rewrite_dql_binary_provenance),
        ExprKind::Lit(Lit::Object(items)) => items
            .iter_mut()
            .for_each(|(_, value)| rewrite_dql_binary_provenance(value)),
        ExprKind::Index { base, index } => {
            rewrite_dql_binary_provenance(base);
            rewrite_dql_binary_provenance(index);
        }
        ExprKind::Field { base, .. } => {
            rewrite_dql_binary_provenance(base);
            let span = base.span;
            let original = std::mem::replace(
                base.as_mut(),
                Expr {
                    kind: ExprKind::Lit(Lit::Bool(false)),
                    span,
                },
            );
            **base = dql_marker_expr(DQL_DATA_ARRAY_OBJECT_KEY, original, span);
        }
        ExprKind::Unary { rhs, .. } => rewrite_dql_binary_provenance(rhs),
        ExprKind::Binary { lhs, rhs, .. } => {
            rewrite_dql_binary_provenance(lhs);
            rewrite_dql_binary_provenance(rhs);
        }
        ExprKind::ListExpr {
            base,
            kind,
            body,
            init,
        } => {
            rewrite_dql_binary_provenance(base);
            rewrite_dql_binary_provenance(body);
            if let Some(init) = init {
                rewrite_dql_binary_provenance(init);
            }
            if *kind == super::expr::ListExprKind::Filter {
                let span = body.span;
                let original = std::mem::replace(
                    body.as_mut(),
                    Expr {
                        kind: ExprKind::Lit(Lit::Bool(false)),
                        span,
                    },
                );
                **body = dql_truthy_call_expr(original, span);
            }
        }
        ExprKind::Lit(_) | ExprKind::Prop(_) | ExprKind::Unsupported { .. } => {}
    }

    if let ExprKind::Unary {
        op: super::expr::UnaryOp::Not,
        rhs,
    } = &mut expr.kind
    {
        let span = rhs.span;
        let original = std::mem::replace(
            rhs.as_mut(),
            Expr {
                kind: ExprKind::Lit(Lit::Bool(false)),
                span,
            },
        );
        **rhs = dql_marker_expr(DQL_TRUTHY_OBJECT_KEY, original, span);
        return;
    }

    let span = expr.span;
    let kind = std::mem::replace(&mut expr.kind, ExprKind::Lit(Lit::Bool(false)));
    let ExprKind::Binary { op, lhs, rhs } = kind else {
        expr.kind = kind;
        return;
    };
    if matches!(op, super::expr::BinaryOp::And | super::expr::BinaryOp::Or) {
        expr.kind = ExprKind::Binary {
            op,
            lhs: Box::new(dql_marker_expr(DQL_TRUTHY_OBJECT_KEY, *lhs, span)),
            rhs: Box::new(dql_marker_expr(DQL_TRUTHY_OBJECT_KEY, *rhs, span)),
        };
        return;
    }
    let marker = match op {
        super::expr::BinaryOp::Mul => DQL_MULTIPLY_OBJECT_KEY,
        super::expr::BinaryOp::Eq | super::expr::BinaryOp::Ne => DQL_EQUALITY_OBJECT_KEY,
        super::expr::BinaryOp::Add
        | super::expr::BinaryOp::Sub
        | super::expr::BinaryOp::Div
        | super::expr::BinaryOp::Mod => DQL_ARITHMETIC_OBJECT_KEY,
        super::expr::BinaryOp::Gt
        | super::expr::BinaryOp::Gte
        | super::expr::BinaryOp::Lt
        | super::expr::BinaryOp::Lte => DQL_ORDERING_OBJECT_KEY,
        _ => {
            expr.kind = ExprKind::Binary { op, lhs, rhs };
            return;
        }
    };
    expr.kind = ExprKind::Binary {
        op,
        lhs: Box::new(dql_marker_expr(marker, *lhs, span)),
        rhs,
    };
}

fn dql_marker_expr(key: &str, value: Expr, span: Span) -> Expr {
    Expr {
        kind: ExprKind::Lit(Lit::Object(vec![(key.to_string(), value)])),
        span,
    }
}

fn dql_truthy_call_expr(value: Expr, span: Span) -> Expr {
    Expr {
        kind: ExprKind::Call {
            callee: Callee::Method {
                receiver: Box::new(dql_marker_expr(DQL_TRUTHY_OBJECT_KEY, value, span)),
                name: MethodName::IsTruthy,
            },
            args: Vec::new(),
        },
        span,
    }
}

fn split_single_arg_lambda(source: &str) -> Option<(&str, &str)> {
    let arrow = source.find("=>")?;
    let raw_param = source[..arrow].trim();
    if !raw_param.starts_with('(') || !raw_param.ends_with(')') {
        return None;
    }
    let param = raw_param[1..raw_param.len() - 1].trim();
    if param.is_empty() || param.contains(',') {
        return None;
    }
    Some((param, source[arrow + 2..].trim()))
}

fn rewrite_special_fields(source: &str, task_context: bool) -> String {
    let mut out = replace_field_outside_strings(
        source,
        "this.file.name",
        &format!(
            "object({}, this.file.path).toString()",
            quote_expr_string(DQL_FILE_NAME_OBJECT_KEY)
        ),
    );
    out = replace_field_outside_strings(&out, "this.file.cday", "this.file.ctime.date()");
    out = replace_field_outside_strings(&out, "this.file.mday", "this.file.mtime.date()");
    out = replace_field_outside_strings(&out, "this.file.link", "link(this.file.path)");
    out = replace_field_outside_strings(
        &out,
        "this.file.inlinks",
        &format!(
            "object({}, this.file.path).values()",
            quote_expr_string(DQL_INLINKS_OBJECT_KEY)
        ),
    );
    out = replace_field_outside_strings(
        &out,
        "this.file.outlinks",
        &format!(
            "object({}, this.file.path).values()",
            quote_expr_string(DQL_OUTLINKS_OBJECT_KEY)
        ),
    );
    out = replace_field_outside_strings(
        &out,
        "this.file.tags",
        &format!(
            "object({}, this.file.path).values()",
            quote_expr_string(DQL_TAGS_OBJECT_KEY)
        ),
    );
    out = replace_field_outside_strings(
        &out,
        "this.file.aliases",
        &format!(
            "object({}, this.file.path).values()",
            quote_expr_string(DQL_ALIASES_OBJECT_KEY)
        ),
    );
    out = replace_field_outside_strings(
        &out,
        "file.name",
        &format!(
            "object({}, file.path).toString()",
            quote_expr_string(DQL_FILE_NAME_OBJECT_KEY)
        ),
    );
    out = replace_field_outside_strings(&out, "file.cday", "file.ctime.date()");
    out = replace_field_outside_strings(&out, "file.mday", "file.mtime.date()");
    out = replace_field_outside_strings(&out, "file.link", "link(file.path)");
    out = replace_field_outside_strings(
        &out,
        "file.inlinks",
        &format!(
            "object({}, file.path).values()",
            quote_expr_string(DQL_INLINKS_OBJECT_KEY)
        ),
    );
    out = replace_field_outside_strings(
        &out,
        "file.outlinks",
        &format!(
            "object({}, file.path).values()",
            quote_expr_string(DQL_OUTLINKS_OBJECT_KEY)
        ),
    );
    out = replace_field_outside_strings(
        &out,
        "file.tags",
        &format!(
            "object({}, file.path).values()",
            quote_expr_string(DQL_TAGS_OBJECT_KEY)
        ),
    );
    out = replace_field_outside_strings(
        &out,
        "file.aliases",
        &format!(
            "object({}, file.path).values()",
            quote_expr_string(DQL_ALIASES_OBJECT_KEY)
        ),
    );
    if task_context {
        for (from, to) in [
            ("completed", "task.completed"),
            (
                "checked",
                "((task.status != \"\") AND (task.status != \" \"))",
            ),
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

fn find_matching_bracket(source: &str, open: usize) -> Option<usize> {
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
            '[' => depth += 1,
            ']' => {
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

fn replace_field_outside_strings(source: &str, from: &str, to: &str) -> String {
    let mut out = String::new();
    let bytes = source.as_bytes();
    let mut pos = 0usize;
    while pos < bytes.len() {
        let b = bytes[pos];
        if b == b'"' || b == b'\'' {
            pos = copy_quoted(source, pos, &mut out);
            continue;
        }
        let end = pos + from.len();
        if source
            .as_bytes()
            .get(pos..end)
            .is_some_and(|candidate| candidate == from.as_bytes())
            && field_start_boundary(bytes, pos)
            && field_end_boundary(bytes, end)
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
        (pos == 0 || source.as_bytes().get(pos - 1) != Some(&b'.'))
            && !is_object_key_position(source, pos + from.len())
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
            && !is_object_key_position(source, idx + field.len())
    })
}

fn is_object_key_position(source: &str, end: usize) -> bool {
    let bytes = source.as_bytes();
    let mut pos = end;
    while bytes
        .get(pos)
        .is_some_and(|byte| byte.is_ascii_whitespace())
    {
        pos += 1;
    }
    bytes.get(pos) == Some(&b':')
}

fn contains_unquoted_arrow(source: &str) -> bool {
    let bytes = source.as_bytes();
    let mut pos = 0usize;
    while pos < bytes.len() {
        if matches!(bytes[pos], b'"' | b'\'') {
            pos = skip_quoted(source, pos);
            continue;
        }
        if bytes.get(pos..pos + 2) == Some(b"=>") {
            return true;
        }
        pos += 1;
    }
    false
}

fn normalize_dql_regex_pattern(name: &str, pattern: &str) -> Result<String, String> {
    let unsupported = || {
        format!(
            "DQL {name} regex uses unsupported JavaScript syntax in Slate's JavaScript-compatible subset"
        )
    };
    if !pattern.is_ascii() {
        return Err(unsupported());
    }
    let bytes = pattern.as_bytes();
    let mut out = String::with_capacity(pattern.len());
    let mut pos = 0usize;
    let mut in_class = false;
    while pos < bytes.len() {
        match bytes[pos] {
            b'\\' => {
                let Some(escaped) = bytes.get(pos + 1).copied() else {
                    return Err(unsupported());
                };
                match escaped {
                    b'w' if in_class => out.push_str("A-Za-z0-9_"),
                    b'w' => out.push_str("[A-Za-z0-9_]"),
                    b'd' if in_class => out.push_str("0-9"),
                    b'd' => out.push_str("[0-9]"),
                    b'W' | b'D' | b's' | b'S' | b'b' | b'B' | b'p' | b'P' | b'u' | b'k' | b'c'
                    | b'A' | b'z' | b'Z' | b'G' => {
                        return Err(unsupported());
                    }
                    other => {
                        out.push('\\');
                        out.push(other as char);
                    }
                }
                pos += 2;
            }
            b'[' if !in_class => {
                if bytes.get(pos + 1) == Some(&b'^') {
                    return Err(unsupported());
                }
                in_class = true;
                out.push('[');
                pos += 1;
            }
            b'[' if in_class => return Err(unsupported()),
            b']' if in_class => {
                in_class = false;
                out.push(']');
                pos += 1;
            }
            b'&' | b'-' | b'~' if in_class && bytes.get(pos + 1) == Some(&bytes[pos]) => {
                return Err(unsupported());
            }
            b'(' if bytes.get(pos + 1) == Some(&b'?') => {
                let suffix = &pattern[pos + 2..];
                if suffix.starts_with("P<")
                    || suffix.starts_with('-')
                    || suffix
                        .as_bytes()
                        .first()
                        .is_some_and(u8::is_ascii_alphabetic)
                {
                    return Err(unsupported());
                }
                out.push('(');
                pos += 1;
            }
            b'.' if !in_class => return Err(unsupported()),
            byte => {
                out.push(byte as char);
                pos += 1;
            }
        }
    }
    regex::Regex::new(&out)
        .map(|_| out)
        .map_err(|_| unsupported())
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
    bytes.len() >= 7
        && bytes[0..4].iter().all(u8::is_ascii_digit)
        && bytes[4] == b'-'
        && bytes[5..7].iter().all(u8::is_ascii_digit)
}

fn is_quoted(source: &str) -> bool {
    matches!(source.as_bytes().first(), Some(b'"' | b'\''))
}

fn contains_single_quoted_string(source: &str) -> bool {
    let bytes = source.as_bytes();
    let mut position = 0usize;
    while position < bytes.len() {
        if bytes[position] == b'"' {
            position += 1;
            while position < bytes.len() {
                if bytes[position] == b'\\' {
                    position = (position + 2).min(bytes.len());
                } else if bytes[position] == b'"' {
                    position += 1;
                    break;
                } else {
                    position += 1;
                }
            }
            continue;
        }
        if bytes[position] == b'/' && dql_slash_starts_regex(source, position) {
            position += 1;
            while position < bytes.len() {
                if bytes[position] == b'\\' {
                    position = (position + 2).min(bytes.len());
                } else if bytes[position] == b'/' {
                    position += 1;
                    break;
                } else {
                    position += 1;
                }
            }
            continue;
        }
        if bytes[position] == b'\'' {
            return true;
        }
        position += 1;
    }
    false
}

fn contains_unsupported_unary_minus(source: &str) -> bool {
    let bytes = source.as_bytes();
    let mut position = 0usize;
    while position < bytes.len() {
        if matches!(bytes[position], b'"' | b'\'') {
            let quote = bytes[position];
            position += 1;
            while position < bytes.len() {
                if bytes[position] == b'\\' {
                    position = (position + 2).min(bytes.len());
                } else if bytes[position] == quote {
                    position += 1;
                    break;
                } else {
                    position += 1;
                }
            }
            continue;
        }
        if bytes[position] == b'/' && dql_slash_starts_regex(source, position) {
            position += 1;
            while position < bytes.len() {
                if bytes[position] == b'\\' {
                    position = (position + 2).min(bytes.len());
                } else if bytes[position] == b'/' {
                    position += 1;
                    break;
                } else {
                    position += 1;
                }
            }
            continue;
        }
        if bytes[position] == b'-' {
            let is_identifier_byte =
                |byte: u8| byte.is_ascii_alphanumeric() || matches!(byte, b'_' | b'-');
            if position > 0
                && position + 1 < bytes.len()
                && is_identifier_byte(bytes[position - 1])
                && is_identifier_byte(bytes[position + 1])
            {
                position += 1;
                continue;
            }
            let previous = source[..position]
                .bytes()
                .rev()
                .find(|byte| !byte.is_ascii_whitespace());
            let unary = previous.is_none_or(|byte| {
                matches!(
                    byte,
                    b'(' | b'['
                        | b'{'
                        | b','
                        | b':'
                        | b'='
                        | b'!'
                        | b'<'
                        | b'>'
                        | b'&'
                        | b'|'
                        | b'+'
                        | b'-'
                        | b'*'
                        | b'/'
                        | b'%'
                )
            });
            if unary {
                let next = bytes[position + 1..]
                    .iter()
                    .copied()
                    .find(|byte| !byte.is_ascii_whitespace());
                if !next.is_some_and(|byte| byte.is_ascii_digit()) {
                    return true;
                }
            }
        }
        position += 1;
    }
    false
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
