// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

use std::collections::BTreeSet;

use chrono::{DateTime, Local, TimeZone, Utc};
use rusqlite::{Connection, params};
use serde::Deserialize;
use slate_core::bases::dql::{DqlWarningKind, parse_dql};
use slate_core::bases::engine::{CellValue, EngineCtx, execute};
use slate_core::bases::eval::{DqlDateValue, DqlDurationValue, Value};
use slate_core::bases::expr::{BinaryOp, Callee, Expr, ExprKind, Lit, PropertyRef, TaskField};
use slate_core::bases::{
    FilterNode, QuerySource, RowSource, SlateQuery, ViewSpec, parse_base, view_query,
};
use slate_core::db::migrate;
use slate_core::{CancelToken, VaultSession};

const OUTGOING_DQL: &str = include_str!("fixtures/dql/outgoing.dql");
const FUNCTIONS_DQL: &str = include_str!("fixtures/dql/functions.dql");
const CLOSURE_CORPUS_JSON: &str = include_str!("fixtures/dql/closure_corpus.json");
const FIELD_CORPUS_JSON: &str = include_str!("fixtures/dql/field_corpus.json");
const FUNCTION_CORPUS_JSON: &str = include_str!("fixtures/dql/function_corpus.json");
const DQL_CENSUS_SEED: u64 = 0x4e5f_4451_4c5f_7631;

const EXPECTED_DQL_GOLDEN_COVERAGE: &[&str] = &[
    "command.flatten.unsupported",
    "command.from.late.unsupported",
    "command.from.repeated.unsupported",
    "command.group_by.unsupported",
    "command.limit",
    "command.sort.asc",
    "command.sort.ascending",
    "command.sort.desc",
    "command.sort.descending",
    "command.sort.multi_key",
    "command.where",
    "command.where.repeated",
    "expression.boolean.and",
    "expression.boolean.or",
    "expression.date.eom",
    "expression.date.eow",
    "expression.date.eoy",
    "expression.date.iso",
    "expression.date.now",
    "expression.date.som",
    "expression.date.sow",
    "expression.date.soy",
    "expression.date.today",
    "expression.date.tomorrow",
    "expression.date.yesterday",
    "expression.duration.unit_aliases",
    "expression.field.bare",
    "expression.field.file_dynamic_index.unsupported",
    "expression.field.file_literal_index",
    "expression.field.file_literal_index.unsupported_field",
    "expression.field.file_special_literal_index",
    "expression.field.index",
    "expression.field.index.vector_hygiene",
    "expression.field.keyword_index",
    "expression.field.link_index.unsupported",
    "expression.field.member",
    "expression.field.member_this_file_collision",
    "expression.field.this_file_dynamic_index.unsupported",
    "expression.field.this_file_literal_index",
    "expression.field.value.vector_hygiene",
    "expression.lambda.filter",
    "expression.lambda.free_index_property",
    "expression.lambda.free_value_property",
    "expression.lambda.map",
    "expression.lambda.member_name_hygiene",
    "expression.lambda.multi_arg.unsupported",
    "expression.lambda.nested.unsupported",
    "expression.lambda.object_key_hygiene",
    "expression.lambda.task_local.annotated",
    "expression.lambda.task_local.block_id",
    "expression.lambda.task_local.checked",
    "expression.lambda.task_local.children",
    "expression.lambda.task_local.completed",
    "expression.lambda.task_local.completion",
    "expression.lambda.task_local.created",
    "expression.lambda.task_local.due",
    "expression.lambda.task_local.fully_completed",
    "expression.lambda.task_local.line",
    "expression.lambda.task_local.line_count",
    "expression.lambda.task_local.link",
    "expression.lambda.task_local.outlinks",
    "expression.lambda.task_local.parent",
    "expression.lambda.task_local.path",
    "expression.lambda.task_local.scheduled",
    "expression.lambda.task_local.section",
    "expression.lambda.task_local.start",
    "expression.lambda.task_local.status",
    "expression.lambda.task_local.tags",
    "expression.lambda.task_local.task",
    "expression.lambda.task_local.text",
    "expression.lambda.task_local.visual",
    "expression.lambda.vectorized_capture.unsupported",
    "expression.literal.boolean",
    "expression.literal.compact_nested_list",
    "expression.literal.list",
    "expression.literal.null.unsupported",
    "expression.literal.number",
    "expression.literal.object",
    "expression.literal.string",
    "expression.literal.wikilink.compact_flat.unsupported",
    "expression.literal.wikilink.numeric.unsupported",
    "expression.literal.wikilink.unsupported",
    "expression.method_call.unsupported",
    "expression.null_delta.unsupported",
    "expression.numeric_multiply.dispatch",
    "expression.object.task_field_keys",
    "expression.operator.add",
    "expression.operator.div",
    "expression.operator.eq",
    "expression.operator.gt",
    "expression.operator.gte",
    "expression.operator.lt",
    "expression.operator.lte",
    "expression.operator.mod",
    "expression.operator.mul",
    "expression.operator.mul.linear_rewrite",
    "expression.operator.mul.precedence",
    "expression.operator.ne",
    "expression.operator.sub",
    "expression.prefix_not",
    "expression.string_concat",
    "expression.string_repeat",
    "expression.string_repeat.computed",
    "expression.string_repeat.field",
    "expression.string_repeat.left_precedence",
    "expression.string_repeat.right_precedence",
    "field.file.aliases",
    "field.file.backlinks.unsupported",
    "field.file.basename.unsupported",
    "field.file.cday",
    "field.file.ctime",
    "field.file.day.unsupported",
    "field.file.embeds.unsupported",
    "field.file.etags.unsupported",
    "field.file.ext",
    "field.file.file.unsupported",
    "field.file.folder",
    "field.file.frontmatter.unsupported",
    "field.file.in_degree.unsupported",
    "field.file.inlinks",
    "field.file.link",
    "field.file.links.unsupported",
    "field.file.lists.unsupported",
    "field.file.mday",
    "field.file.mtime",
    "field.file.name",
    "field.file.out_degree.unsupported",
    "field.file.outlinks",
    "field.file.path",
    "field.file.properties.unsupported",
    "field.file.size",
    "field.file.starred.unsupported",
    "field.file.tags",
    "field.file.tasks.unsupported",
    "field.task.annotated.unsupported",
    "field.task.block_id.unsupported",
    "field.task.checked.nonempty_nonspace",
    "field.task.children.unsupported",
    "field.task.completed.x_or_uppercase",
    "field.task.completion.unsupported",
    "field.task.created.unsupported",
    "field.task.due",
    "field.task.fully_completed.unsupported",
    "field.task.line.unsupported",
    "field.task.line_count.unsupported",
    "field.task.link.unsupported",
    "field.task.outlinks.unsupported",
    "field.task.parent.unsupported",
    "field.task.path.unsupported",
    "field.task.scheduled",
    "field.task.section.unsupported",
    "field.task.start.unsupported",
    "field.task.status",
    "field.task.tags.unsupported",
    "field.task.task.unsupported",
    "field.task.text",
    "field.task.visual.unsupported",
    "field.this.file.aliases",
    "field.this.file.backlinks.unsupported",
    "field.this.file.basename.unsupported",
    "field.this.file.cday",
    "field.this.file.ctime",
    "field.this.file.day.unsupported",
    "field.this.file.embeds.unsupported",
    "field.this.file.etags.unsupported",
    "field.this.file.ext",
    "field.this.file.file.unsupported",
    "field.this.file.folder",
    "field.this.file.frontmatter.unsupported",
    "field.this.file.in_degree.unsupported",
    "field.this.file.inlinks",
    "field.this.file.link",
    "field.this.file.links.unsupported",
    "field.this.file.lists.unsupported",
    "field.this.file.mday",
    "field.this.file.mtime",
    "field.this.file.name",
    "field.this.file.out_degree.unsupported",
    "field.this.file.outlinks",
    "field.this.file.path",
    "field.this.file.properties.unsupported",
    "field.this.file.size",
    "field.this.file.starred.unsupported",
    "field.this.file.tags",
    "field.this.file.tasks.unsupported",
    "field.this.note",
    "function.aggregate.choice_list",
    "function.aggregate.dynamic_list_shape",
    "function.aggregate.dynamic_scalar_guard",
    "function.aggregate.empty_list",
    "function.aggregate.this_file_list",
    "function.aggregate.this_file_scalar.unsupported",
    "function.all.predicate.unsupported",
    "function.any.predicate.unsupported",
    "function.array",
    "function.array.variadic",
    "function.average.list",
    "function.average.non_list.unsupported",
    "function.ceil",
    "function.choice",
    "function.contains",
    "function.contains.vectorized_nested_needle",
    "function.contains.object_equality",
    "function.contains.recursive_list",
    "function.contains.scalar_equality",
    "function.containsword.unsupported",
    "function.currencyformat.unsupported",
    "function.date.luxon_format.unsupported",
    "function.date.one_arg",
    "function.dateformat.unsupported",
    "function.default",
    "function.display.unsupported",
    "function.dur",
    "function.durationformat.unsupported",
    "function.econtains.unsupported",
    "function.elink.unsupported",
    "function.embed",
    "function.endswith",
    "function.extract.unsupported",
    "function.filter",
    "function.filter.dynamic_list_shape",
    "function.filter.dynamic_scalar_guard",
    "function.filter.non_list.unsupported",
    "function.firstvalue.unsupported",
    "function.flat.default_depth",
    "function.flat.dynamic_depth",
    "function.flat.dynamic_list_shape",
    "function.flat.dynamic_scalar_guard",
    "function.flat.excessive_depth.unsupported",
    "function.flat.explicit_depth",
    "function.flat.fractional_depth",
    "function.flat.negative_depth",
    "function.flat.non_list.unsupported",
    "function.floor",
    "function.hash.unsupported",
    "function.icontains.unsupported",
    "function.if.unsupported",
    "function.join.default_delimiter",
    "function.join.dynamic_list_shape",
    "function.join.scalar",
    "function.join.explicit_delimiter",
    "function.ldefault.unsupported",
    "function.length",
    "function.length.dynamic_scalar_guard",
    "function.length.null",
    "function.length.object",
    "function.length.utf16",
    "function.link.display",
    "function.link.path",
    "function.list",
    "function.list.nested",
    "function.list.variadic",
    "function.localtime.unsupported",
    "function.lower",
    "function.map",
    "function.map.dynamic_list_shape",
    "function.map.dynamic_scalar_guard",
    "function.map.non_list.unsupported",
    "function.max.date",
    "function.max.list",
    "function.max.non_list.unsupported",
    "function.max.null_elision",
    "function.max.text",
    "function.max.typed_numeric",
    "function.maxby.unsupported",
    "function.meta.unsupported",
    "function.min.all_null",
    "function.min.date",
    "function.min.list",
    "function.min.non_list.unsupported",
    "function.min.null_elision",
    "function.min.text",
    "function.min.typed_numeric",
    "function.minby.unsupported",
    "function.none.predicate.unsupported",
    "function.nonnull.unsupported",
    "function.number",
    "function.number.exponent_prefix",
    "function.number.leading_dot",
    "function.number.substring_extract",
    "function.object",
    "function.padleft.unsupported",
    "function.padright.unsupported",
    "function.product.unsupported",
    "function.reduce.unsupported",
    "function.regex.ascii_digit_class",
    "function.regex.ascii_word_class",
    "function.regex.class_set_operation.unsupported",
    "function.regex.digit_class_unicode_nonmatch",
    "function.regex.dot_utf16.unsupported",
    "function.regex.dynamic_subset_guard",
    "function.regex.inline_flags.unsupported",
    "function.regex.javascript_only_syntax.unsupported",
    "function.regex.non_ascii_pattern.unsupported",
    "function.regex.rust_anchor.unsupported",
    "function.regex.rust_named_group.unsupported",
    "function.regex.word_class_unicode_nonmatch",
    "function.regexmatch.dynamic_pattern",
    "function.regexmatch.whole_string",
    "function.regexreplace.dynamic_pattern",
    "function.regexreplace.global_captures",
    "function.regexreplace.replacement.leading_zero_capture",
    "function.regexreplace.replacement.named_capture",
    "function.regexreplace.replacement.named_literal",
    "function.regexreplace.replacement.whole_match",
    "function.regexreplace.zero_width_utf16_guard",
    "function.regextest",
    "function.regextest.dynamic_pattern",
    "function.replace.empty_pattern",
    "function.replace.literal_all",
    "function.reverse",
    "function.reverse.scalar_identity",
    "function.reverse.utf16_guard",
    "function.round.default_digits",
    "function.round.explicit_digits",
    "function.round.negative_half_tie",
    "function.round.nonpositive_precision",
    "function.round.positive_precision",
    "function.slice",
    "function.slice.dynamic_list_shape",
    "function.slice.dynamic_scalar_guard",
    "function.slice.negative_end",
    "function.slice.negative_start",
    "function.slice.non_list.unsupported",
    "function.sort",
    "function.sort.scalar_identity",
    "function.sort.typed_numeric",
    "function.split.dynamic_pattern",
    "function.split.empty_regex.bmp_unicode",
    "function.split.empty_text_empty_regex",
    "function.split.limit",
    "function.split.list_input.unsupported",
    "function.split.regex_capture_groups",
    "function.split.regex_no_capture",
    "function.split.unmatched_capture",
    "function.split.zero_width",
    "function.split.zero_width_utf16_guard",
    "function.startswith",
    "function.string",
    "function.striptime",
    "function.substring",
    "function.substring.dynamic_scalar_guard",
    "function.substring.reversed_bounds",
    "function.sum.list",
    "function.sum.non_list.unsupported",
    "function.trunc.toward_zero",
    "function.truncate.unsupported",
    "function.typeof.boolean_eq",
    "function.typeof.boolean_ne",
    "function.typeof.value.unsupported",
    "function.unique",
    "function.unique.dynamic_list_shape",
    "function.unique.dynamic_scalar_guard",
    "function.unique.non_list.unsupported",
    "function.unique.structural",
    "function.unknown.unsupported",
    "function.upper.unsupported",
    "function.vectorize.ceil.arg0",
    "function.vectorize.choice.arg0",
    "function.vectorize.contains.arg1",
    "function.vectorize.date.arg0",
    "function.vectorize.default.arg0_arg1",
    "function.vectorize.dur.arg0",
    "function.vectorize.embed.arg0",
    "function.vectorize.endswith.arg0_arg1",
    "function.vectorize.expansion_limit.unsupported",
    "function.vectorize.floor.arg0",
    "function.vectorize.join.separator",
    "function.vectorize.link.arg0",
    "function.vectorize.link.arg0_arg1",
    "function.vectorize.lower.arg0",
    "function.vectorize.number.arg0",
    "function.vectorize.regexmatch.arg0_arg1",
    "function.vectorize.regexreplace.arg0_arg1_arg2",
    "function.vectorize.regextest.arg0_arg1",
    "function.vectorize.replace.arg0_arg1_arg2",
    "function.vectorize.round.arg0",
    "function.vectorize.startswith.arg0_arg1",
    "function.vectorize.striptime.arg0",
    "function.vectorize.substring.arg0_arg1_arg2",
    "function.vectorize.trunc.arg0",
    "idiom.felker.schedule_table",
    "idiom.felker.status_table",
    "idiom.reading_duration_math",
    "pipeline.limit_before_sort.unsupported",
    "pipeline.repeated_limit.unsupported",
    "pipeline.repeated_sort.unsupported",
    "pipeline.safe",
    "pipeline.where_after_limit.unsupported",
    "pipeline.where_after_sort.unsupported",
    "query.calendar.unsupported",
    "query.list.secondary",
    "query.list.with_id",
    "query.list.without_id",
    "query.list.without_id.duplicate_token_span",
    "query.table.alias",
    "query.table.column_order",
    "query.table.with_id",
    "query.table.without_id",
    "query.task",
    "source.and",
    "source.boolean.case_insensitive",
    "source.folder",
    "source.folder.recursive",
    "source.function.unsupported",
    "source.incoming_link",
    "source.negation.bang",
    "source.negation.dash",
    "source.or",
    "source.outgoing_link.query_source",
    "source.outgoing_link.serialized_filter",
    "source.parentheses",
    "source.path",
    "source.path.folder_wins",
    "source.tag",
    "source.tag.subtags",
    "source.this_anchor.incoming",
    "source.this_anchor.outgoing",
    "source.this_empty.incoming",
    "source.this_empty.outgoing",
    "span.from.trimmed_expression",
    "span.sort.header_collision",
    "span.sort.repeated_key",
    "span.table.duplicate_column",
    "span.table.second_column",
    "span.where.trimmed_expression",
];

#[test]
fn table_without_id_maps_columns_sources_where_sort_and_limit() {
    let (query, warnings) = parse_dql(
        r##"TABLE WITHOUT ID file.name AS "Name", lower(status) AS "Status"
FROM #project and -"Archive"
WHERE file.mtime >= date(today) AND contains(file.tags, "#project")
SORT file.mtime DESCENDING, file.name ASC
LIMIT 25
"##,
    );

    assert_eq!(warnings, []);
    assert!(matches!(
        query.view,
        ViewSpec::Table {
            fallback_from: None
        }
    ));
    assert_eq!(
        query
            .columns
            .iter()
            .map(|column| (column.id.as_str(), column.display_name.as_deref()))
            .collect::<Vec<_>>(),
        [
            ("formula.dql_column_1", Some("Name")),
            ("formula.dql_column_2", Some("Status")),
        ]
    );
    assert_eq!(query.formulas.len(), 2);
    assert_eq!(query.sort.len(), 2);
    assert!(!query.sort[0].ascending);
    assert!(query.sort[1].ascending);
    assert_eq!(query.limit, Some(25));
    assert!(matches!(query.filters, Some(FilterNode::And(ref nodes)) if nodes.len() == 2));
}

#[test]
fn table_and_list_default_id_prepend_file_column() {
    let (table, table_warnings) = parse_dql("TABLE file.name\n");
    let (list, list_warnings) = parse_dql("LIST file.name\n");

    assert_eq!(table_warnings, []);
    assert_eq!(list_warnings, []);
    assert_eq!(table.columns[0].id, "file.file");
    assert_eq!(table.columns[1].id, "formula.dql_column_1");
    assert_eq!(list.columns[0].id, "file.file");
    assert_eq!(list.columns[1].id, "formula.dql_list_value");
}

#[test]
fn task_queries_use_tasks_source_and_dataview_completed_semantics() {
    let (query, warnings) = parse_dql(
        r#"TASK
WHERE !completed AND due >= date(today)
SORT status ASC
"#,
    );

    assert_eq!(warnings, []);
    assert_eq!(query.row_source, RowSource::Tasks);
    assert!(matches!(
        query.view,
        ViewSpec::List {
            fallback_from: None
        }
    ));
    assert!(filter_contains_task_completed(
        query.filters.as_ref().expect("task filters")
    ));
    assert!(matches!(
        dql_sort_expr(&query.sort[0].expr).kind,
        ExprKind::Prop(PropertyRef::TaskField(TaskField::Status))
    ));
}

#[test]
fn dql_completed_and_checked_include_uppercase_x() {
    let conn = dql_fixture_conn();
    let completed = execute_dql(&conn, "TASK\nWHERE completed\n", None);
    let checked = execute_dql(&conn, "TASK\nWHERE checked\n", None);

    assert_eq!(
        completed.rows.len(),
        2,
        "lowercase and uppercase x are completed"
    );
    assert_eq!(checked.rows.len(), 3, "x, X, and / are all checked");
    assert!(
        checked
            .rows
            .iter()
            .all(|row| row.task_ordinal != Some(1) || row.path == "Hub.md"),
        "an empty checkbox status is not checked"
    );
    assert_eq!(completed.error, None);
    assert_eq!(checked.error, None);
}

#[test]
fn from_sources_support_outgoing_and_boolean_negation() {
    let (linked, linked_warnings) = parse_dql("LIST\nFROM outgoing([[Hub]])\n");
    let (filtered, filtered_warnings) = parse_dql("LIST\nFROM [[Inbox]] or !#done\n");
    let (nested, nested_warnings) = parse_dql("LIST\nFROM (#project and [[Hub]])\n");
    let invalid_source = "LIST\nFROM #tag trailing\n";
    let (invalid, invalid_warnings) = parse_dql(invalid_source);

    assert_eq!(linked_warnings, []);
    assert_eq!(
        linked.source,
        QuerySource::Linked {
            from_path: "Hub".to_string(),
            depth: 1,
        }
    );
    assert!(linked.filters.is_none());
    assert_eq!(filtered_warnings, []);
    assert!(matches!(filtered.filters, Some(FilterNode::Or(ref nodes)) if nodes.len() == 2));
    assert_eq!(nested_warnings, []);
    assert!(matches!(nested.filters, Some(FilterNode::And(ref nodes)) if nodes.len() == 2));
    assert!(filter_has_unsupported_reason(
        invalid.filters.as_ref().expect("invalid source filter"),
        "invalid FROM source"
    ));
    assert_eq!(invalid_warnings.len(), 1);
    assert_eq!(invalid_warnings[0].kind, DqlWarningKind::InvalidExpression);
    assert!(invalid_warnings[0].message.contains("invalid FROM source"));
    assert!(invalid_warnings[0].message.contains("#tag trailing"));
    assert_eq!(invalid_warnings[0].span.start, 10);
    assert_eq!(invalid_warnings[0].span.end, 23);

    let (unknown, unknown_warnings) = parse_dql("LIST\nFROM mystery(\"x\")\n");
    assert!(filter_has_unsupported_reason(
        unknown.filters.as_ref().expect("unknown source filter"),
        "invalid FROM source"
    ));
    assert_eq!(unknown_warnings.len(), 1);
    assert_eq!(unknown_warnings[0].kind, DqlWarningKind::InvalidExpression);
    assert!(unknown_warnings[0].message.contains("mystery(\"x\")"));
    assert_eq!(unknown_warnings[0].span.start, 10);
    assert_eq!(unknown_warnings[0].span.end, 22);

    let (empty_tag, empty_tag_warnings) = parse_dql("LIST\nFROM #\n");
    assert!(filter_has_unsupported_reason(
        empty_tag.filters.as_ref().expect("empty tag source filter"),
        "invalid FROM source"
    ));
    assert_eq!(empty_tag_warnings.len(), 1);
    assert!(
        empty_tag_warnings[0]
            .message
            .contains("invalid FROM source")
    );
}

#[test]
fn dql_explicit_outgoing_resolves_extensionless_wikilink_membership() {
    let conn = dql_fixture_conn();
    let result = execute_dql(&conn, OUTGOING_DQL, None);

    assert_eq!(row_paths(&result), ["Target.md"]);
    assert_eq!(result.error, None);
}

#[test]
fn dql_explicit_outgoing_resolves_extensionless_target_from_host_context() {
    let conn = dql_fixture_conn();
    let result = execute_dql(&conn, OUTGOING_DQL, Some("Notes/View.base"));

    assert_eq!(row_paths(&result), ["Notes/Target.md"]);
    assert_eq!(result.error, None);
}

#[test]
fn dql_dynamic_outgoing_uses_this_file_membership() {
    let conn = dql_fixture_conn();
    let result = execute_dql(&conn, "LIST\nFROM outgoing([[]])\n", Some("Hub.md"));

    assert_eq!(row_paths(&result), ["Target.md"]);
    assert_eq!(result.error, None);
}

#[test]
fn dql_outgoing_embed_membership_matches_saved_base_filter() {
    let conn = dql_embed_fixture_conn();
    let live = execute_dql(
        &conn,
        "LIST WITHOUT ID file.path\nFROM outgoing([[Hub]])\n",
        None,
    );

    assert_eq!(row_paths(&live), ["Target.md"]);
    assert_eq!(live.error, None);

    let (base, warnings) = parse_base(
        r#"filters: 'link("Hub").linksTo(file.file)'
views:
  - type: list
    name: Outgoing
    order:
      - file.path
"#,
    );
    assert_eq!(warnings, []);
    let saved = execute(
        &view_query(&base, 0),
        &conn,
        &EngineCtx::default(),
        &CancelToken::new(),
    )
    .expect("execute saved Base equivalent");

    assert_eq!(row_paths(&saved), ["Target.md"]);
    assert_eq!(saved.error, None);
}

#[test]
fn dql_incoming_source_includes_embed_and_ordinary_link_controls() {
    let conn = dql_embed_fixture_conn();
    let live = execute_dql(
        &conn,
        "LIST WITHOUT ID file.path\nFROM [[Target.md]]\n",
        None,
    );

    assert_eq!(row_paths(&live), ["Hub.md", "Other.md"]);
    assert_eq!(live.error, None);

    let (base, warnings) = parse_base(
        r#"filters: 'file.hasLink("Target.md")'
views:
  - type: list
    name: Incoming
    order:
      - file.path
"#,
    );
    assert_eq!(warnings, []);
    let saved = execute(
        &view_query(&base, 0),
        &conn,
        &EngineCtx::default(),
        &CancelToken::new(),
    )
    .expect("execute durable incoming Base equivalent");

    assert_eq!(row_paths(&saved), ["Hub.md", "Other.md"]);
    assert_eq!(saved.error, None);
}

#[test]
fn dql_file_link_fields_recombine_embeds_and_deduplicate_incoming_pages() {
    let conn = dql_embed_fixture_conn();
    conn.execute(
        "INSERT INTO links (
            source_file_id, ordinal, target_path, target_raw, target_anchor,
            kind, is_embed, is_external, snippet, span_start, span_end
         ) VALUES (1, 1, 'Other.md', 'Other', NULL, 'wikilink', 0, 0, '', 12, 21)",
        [],
    )
    .expect("insert ordinary outgoing control after embed");
    conn.execute(
        "INSERT INTO links (
            source_file_id, ordinal, target_path, target_raw, target_anchor,
            kind, is_embed, is_external, snippet, span_start, span_end
         ) VALUES (1, 2, 'Target.md', 'Target', NULL, 'wikilink', 0, 0, '', 22, 32)",
        [],
    )
    .expect("insert repeated incoming control");

    let outgoing = execute_dql(
        &conn,
        "TABLE WITHOUT ID file.outlinks AS \"Links\"\nWHERE file.path = \"Hub.md\"\n",
        None,
    );
    assert!(matches!(first_value(&outgoing, 0), Value::List(values)
        if matches!(&values[..], [Value::Link(first), Value::Link(second)]
            if first.target == "Target" && first.embed
                && second.target == "Other" && !second.embed)));

    let incoming = execute_dql(
        &conn,
        "TABLE WITHOUT ID file.inlinks AS \"Links\"\nWHERE file.path = \"Target.md\"\n",
        None,
    );
    assert!(matches!(first_value(&incoming, 0), Value::List(values)
        if matches!(&values[..], [Value::Link(first), Value::Link(second)]
            if first.target == "Hub.md" && second.target == "Other.md")));

    let this_fields = execute_dql(
        &conn,
        "TABLE WITHOUT ID this.file.outlinks AS \"Out\", this.file.inlinks AS \"In\"\nWHERE file.path = \"Other.md\"\n",
        Some("Hub.md"),
    );
    assert_eq!(first_value(&this_fields, 0), first_value(&outgoing, 0));
    assert!(matches!(first_value(&this_fields, 1), Value::List(values) if values.is_empty()));

    let (base, warnings) = parse_base(
        r#"views:
  - type: table
    name: Degree
    order:
      - file.inDegree
"#,
    );
    assert_eq!(warnings, []);
    let native = execute(
        &view_query(&base, 0),
        &conn,
        &EngineCtx::default(),
        &CancelToken::new(),
    )
    .expect("execute native repeated-link degree control");
    let target = native
        .rows
        .iter()
        .find(|row| row.path == "Target.md")
        .expect("target native row");
    assert!(matches!(
        target.cells.first(),
        Some(CellValue::Value(Value::Number(3.0)))
    ));
}

#[test]
fn dql_command_sort_uses_dql_null_ordering() {
    let conn = dql_fixture_conn();
    let result = execute_dql(
        &conn,
        "TABLE WITHOUT ID file.path AS \"Path\"\nSORT priority ASC\n",
        None,
    );

    assert_eq!(result.error, None);
    assert_eq!(
        result.rows.last().map(|row| row.path.as_str()),
        Some("Hub.md"),
        "DQL nulls sort before the one numeric priority value"
    );
}

#[test]
fn dql_command_sort_totalizes_mixed_finite_and_nan_keys() {
    let conn = dql_fixture_conn();
    let result = execute_dql(
        &conn,
        r#"TABLE WITHOUT ID file.path AS "Path"
WHERE file.path = "123.md" OR file.path = "Hub.md" OR file.path = "Target.md"
SORT choice(file.path = "123.md", 0 / 0, 1) ASC
"#,
        None,
    );

    assert_eq!(result.error, None);
    assert_eq!(
        row_paths(&result),
        ["Hub.md", "Target.md", "123.md"],
        "finite keys must use their representable DQL order and NaN must use the deterministic total fallback"
    );
}

#[test]
fn dql_command_sort_totalizes_equal_casual_durations_by_structure() {
    let conn = dql_fixture_conn();
    let result = execute_dql(
        &conn,
        r#"TABLE WITHOUT ID file.path AS "Path"
WHERE file.path = "123.md" OR file.path = "Hub.md"
SORT choice(file.path = "123.md", dur("1month"), dur("30days")) ASC
"#,
        None,
    );

    assert_eq!(result.error, None);
    assert_eq!(
        row_paths(&result),
        ["Hub.md", "123.md"],
        "equal casual milliseconds with distinct duration structure need one stable order"
    );
}

#[test]
fn dql_command_sort_totalizes_inconsistent_nested_values() {
    let conn = dql_fixture_conn();
    let result = execute_dql(
        &conn,
        r#"TABLE WITHOUT ID file.path AS "Path"
WHERE file.path = "123.md" OR file.path = "Hub.md"
SORT choice(file.path = "123.md", [0 / 0], [1]) ASC
"#,
        None,
    );

    assert_eq!(result.error, None);
    assert_eq!(
        row_paths(&result),
        ["Hub.md", "123.md"],
        "nested DQL keys must inherit the same deterministic total fallback"
    );
}

#[test]
fn dql_command_sort_fails_loud_when_locale_collation_is_unrepresentable() {
    let conn = dql_fixture_conn();
    let result = execute_dql(
        &conn,
        "TABLE WITHOUT ID file.path AS \"Path\"\nSORT file.path ASC\n",
        None,
    );

    let error = result.error.expect("DQL SORT must fail the view loudly");
    assert!(
        error.construct.contains("DQL SORT") && error.construct.contains("locale collation"),
        "unexpected DQL SORT failure: {error:?}"
    );
    assert!(result.rows.is_empty());
}

#[test]
fn dql_command_sort_validates_unrepresentable_keys_even_for_one_row() {
    let conn = dql_fixture_conn();
    let result = execute_dql(
        &conn,
        "TABLE WITHOUT ID file.path AS \"Path\"\nWHERE file.path = \"123.md\"\nSORT file.path ASC\n",
        None,
    );

    assert_fail_loud(
        &result,
        "DQL locale collation",
        "single-row DQL command sort",
    );
}

#[test]
fn dql_file_tags_expand_parents_once() {
    let result = execute_dql(
        &dql_fixture_conn(),
        "TABLE WITHOUT ID file.tags AS \"Tags\", contains(file.tags, \"#Project\") AS \"Parent\", contains(file.tags, \"#project\") AS \"Lower\"\nWHERE file.path = \"Hub.md\"\n",
        None,
    );
    assert_eq!(
        first_value(&result, 0),
        &Value::List(vec![
            Value::Text("#Project/SubTag".into()),
            Value::Text("#Project".into()),
        ])
    );
    assert_eq!(first_value(&result, 1), &Value::Bool(true));
    assert_eq!(first_value(&result, 2), &Value::Bool(false));
}

#[test]
fn dql_regextest_converts_literal_pattern_and_evaluates() {
    let conn = dql_fixture_conn();
    let (query, warnings) = parse_dql(FUNCTIONS_DQL);
    assert_eq!(warnings, []);
    let ExprKind::Call {
        callee: Callee::Method { receiver, .. },
        ..
    } = &query.formulas[0].1.kind
    else {
        panic!("regextest should convert to a regex method call");
    };
    let ExprKind::Call {
        callee: Callee::Global(slate_core::bases::expr::GlobalFn::Object),
        args,
    } = &receiver.kind
    else {
        panic!("regextest should preserve its DQL regex marker: {receiver:?}");
    };
    assert!(matches!(
        &args[..],
        [
            Expr { kind: ExprKind::Lit(Lit::String(key)), .. },
            Expr { kind: ExprKind::Lit(Lit::String(pattern)), .. },
            Expr { kind: ExprKind::Lit(Lit::String(mode_key)), .. },
            Expr { kind: ExprKind::Lit(Lit::String(mode)), .. },
        ] if key == "\u{f8ff}slate.dql.regex"
            && pattern == "^foo"
            && mode_key == "\u{f8ff}slate.dql.regex.mode"
            && mode == "search"
    ));

    let result = execute_dql(&conn, FUNCTIONS_DQL, None);
    assert_eq!(first_value(&result, 0), &Value::Bool(true));
    assert_eq!(result.error, None);
}

#[test]
fn dql_regexreplace_uses_regex_pattern_semantics() {
    let source = r##"TABLE WITHOUT ID regexreplace("a1b22c", "\d+", "#") AS "Global", regexreplace("Doe, Jane", "(\w+), (\w+)", "$2 $1") AS "Captures", regexreplace("price 12", "(\d+)", "$$$1") AS "Dollar", regexreplace("a1b", "\d", "[$&]") AS "Whole match", regexreplace("a", "a", "$<x>") AS "Named literal", regexreplace("a", "(a)", "$01") AS "Leading zero", regexreplace("a", "(?<x>a)", "$<x>") AS "Named capture"
"##;
    let (query, warnings) = parse_dql(source);
    let conn = dql_fixture_conn();
    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute converted regexreplace query");

    assert_eq!(warnings, []);
    assert_eq!(first_value(&result, 0), &Value::Text("a#b#c".to_string()));
    assert_eq!(
        first_value(&result, 1),
        &Value::Text("Jane Doe".to_string())
    );
    assert_eq!(
        first_value(&result, 2),
        &Value::Text("price $12".to_string())
    );
    assert_eq!(first_value(&result, 3), &Value::Text("a[1]b".to_string()));
    assert_eq!(first_value(&result, 4), &Value::Text("$<x>".to_string()));
    assert_eq!(first_value(&result, 5), &Value::Text("a".to_string()));
    assert_eq!(first_value(&result, 6), &Value::Text("a".to_string()));
    assert_eq!(result.error, None);
}

#[test]
fn dql_regexmatch_requires_the_whole_string_while_regextest_does_not() {
    let source = r#"TABLE WITHOUT ID regextest("foo", "foobar") AS "Test", regexmatch("foo", "foobar") AS "Substring", regexmatch("foo", "foo") AS "Full", regexmatch("^foo$", "foo") AS "Anchored", regexmatch("foo|bar", "foobar") AS "Alternation", regexmatch("(foo|bar)", "bar") AS "Grouping", regexmatch("\d+", "123") AS "Escaped full", regexmatch("\d+", "x123") AS "Escaped substring"
"#;
    let (query, warnings) = parse_dql(source);
    let conn = dql_fixture_conn();
    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute converted regex query");

    assert_eq!(warnings, []);
    assert_eq!(first_value(&result, 0), &Value::Bool(true));
    assert_eq!(first_value(&result, 1), &Value::Bool(false));
    assert_eq!(first_value(&result, 2), &Value::Bool(true));
    assert_eq!(first_value(&result, 3), &Value::Bool(true));
    assert_eq!(first_value(&result, 4), &Value::Bool(false));
    assert_eq!(first_value(&result, 5), &Value::Bool(true));
    assert_eq!(first_value(&result, 6), &Value::Bool(true));
    assert_eq!(first_value(&result, 7), &Value::Bool(false));
    assert_eq!(result.error, None);
}

#[test]
fn dql_join_defaults_delimiter_and_flat_honors_explicit_depth() {
    let source = r#"TABLE WITHOUT ID join(["a", "b"]) AS "Default join", join(["a", "b"], "|") AS "Explicit join", flat([ [ [1] ], [ [2] ] ]) AS "Default flat", flat([ [ [1] ], [ [2] ] ], 0) AS "Zero flat", flat([ [ [1] ], [ [2] ] ], 1) AS "One flat", flat([ [ [1] ], [ [2] ] ], 2) AS "Deep flat"
"#;
    let (query, warnings) = parse_dql(source);
    let conn = dql_fixture_conn();
    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute converted join and flat query");

    assert_eq!(warnings, []);
    assert_eq!(first_value(&result, 0), &Value::Text("a, b".to_string()));
    assert_eq!(first_value(&result, 1), &Value::Text("a|b".to_string()));
    assert_eq!(
        first_value(&result, 2),
        &Value::List(vec![
            Value::List(vec![Value::Number(1.0)]),
            Value::List(vec![Value::Number(2.0)]),
        ])
    );
    assert_eq!(
        first_value(&result, 3),
        &Value::List(vec![
            Value::List(vec![Value::List(vec![Value::Number(1.0)])]),
            Value::List(vec![Value::List(vec![Value::Number(2.0)])]),
        ])
    );
    assert_eq!(
        first_value(&result, 4),
        &Value::List(vec![
            Value::List(vec![Value::Number(1.0)]),
            Value::List(vec![Value::Number(2.0)]),
        ])
    );
    assert_eq!(
        first_value(&result, 5),
        &Value::List(vec![Value::Number(1.0), Value::Number(2.0)])
    );
    assert_eq!(result.error, None);
}

#[test]
fn dql_flat_depth_uses_javascript_coercion_and_accepts_expressions() {
    let source = r#"TABLE WITHOUT ID flat([ [ [1] ] ], -1) AS "Negative", flat([ [ [1] ] ], 1.5) AS "Fractional", flat([ [ [1] ] ], file.size) AS "Dynamic"
WHERE file.path = "123.md"
"#;
    let (query, warnings) = parse_dql(source);
    let conn = dql_fixture_conn();
    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute DQL flat depth coercions");

    assert_eq!(warnings, []);
    assert_eq!(
        first_value(&result, 0),
        &Value::List(vec![Value::List(vec![Value::List(vec![Value::Number(
            1.0
        )])])])
    );
    assert_eq!(
        first_value(&result, 1),
        &Value::List(vec![Value::List(vec![Value::Number(1.0)])])
    );
    assert_eq!(
        first_value(&result, 2),
        &Value::List(vec![Value::Number(1.0)])
    );
    assert_eq!(result.error, None);
}

#[test]
fn dql_split_uses_regex_delimiters_and_splices_capture_groups() {
    let source = r#"TABLE WITHOUT ID split("a1b22c", "\d+") AS "No captures", split("a1b22c", "(\d+)") AS "One capture", split("a1b", "(\d)([a-z])") AS "Multiple captures", split("ab", "(x)?b") AS "Unmatched capture", split("ab", "") AS "Zero width", split("a1b2c", "(\d)", 3) AS "Limited"
"#;
    let (query, warnings) = parse_dql(source);
    let conn = dql_fixture_conn();
    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute converted regex split query");

    assert_eq!(warnings, []);
    assert_eq!(
        first_value(&result, 0),
        &Value::List(vec![
            Value::Text("a".to_string()),
            Value::Text("b".to_string()),
            Value::Text("c".to_string()),
        ])
    );
    assert_eq!(
        first_value(&result, 1),
        &Value::List(vec![
            Value::Text("a".to_string()),
            Value::Text("1".to_string()),
            Value::Text("b".to_string()),
            Value::Text("22".to_string()),
            Value::Text("c".to_string()),
        ])
    );
    assert_eq!(
        first_value(&result, 2),
        &Value::List(vec![
            Value::Text("a".to_string()),
            Value::Text("1".to_string()),
            Value::Text("b".to_string()),
            Value::Text(String::new()),
        ])
    );
    assert_eq!(
        first_value(&result, 3),
        &Value::List(vec![
            Value::Text("a".to_string()),
            Value::Text(String::new()),
            Value::Text(String::new()),
        ])
    );
    assert_eq!(
        first_value(&result, 4),
        &Value::List(vec![
            Value::Text("a".to_string()),
            Value::Text("b".to_string()),
        ])
    );
    assert_eq!(
        first_value(&result, 5),
        &Value::List(vec![
            Value::Text("a".to_string()),
            Value::Text("1".to_string()),
            Value::Text("b".to_string()),
        ])
    );
    assert_eq!(result.error, None);
}

#[test]
fn dql_file_tasks_fails_loud_instead_of_exposing_slate_count_object() {
    let (query, warnings) = parse_dql("TABLE WITHOUT ID file.tasks AS \"Tasks\"\n");

    assert!(warnings.iter().any(|warning| {
        warning.kind == DqlWarningKind::UnsupportedConstruct
            && warning.message == "unsupported DQL field file.tasks"
    }));
    assert!(matches!(
        query.formulas[0].1.kind,
        ExprKind::Unsupported { ref reason, .. } if reason == "unsupported DQL field file.tasks"
    ));
}

#[test]
fn dql_unsupported_this_file_fields_fail_loud() {
    for field in ["etags", "lists", "frontmatter", "day", "starred", "tasks"] {
        let source = format!("TABLE WITHOUT ID this.file.{field} AS \"Value\"\n");
        let (query, warnings) = parse_dql(&source);
        let reason = format!("unsupported DQL field this.file.{field}");

        assert!(warnings.iter().any(|warning| {
            warning.kind == DqlWarningKind::UnsupportedConstruct && warning.message == reason
        }));
        assert!(matches!(
            query.formulas[0].1.kind,
            ExprKind::Unsupported { reason: ref got, .. } if got == &reason
        ));
    }
}

#[test]
fn dql_this_file_special_fields_rewrite_and_execute() {
    let source = r#"TABLE WITHOUT ID this.file.cday AS "Created day", this.file.mday AS "Modified day", this.file.link AS "Link", this.file.inlinks AS "Incoming", this.file.outlinks AS "Outgoing"
WHERE file.path = "123.md"
"#;
    let (query, warnings) = parse_dql(source);
    let conn = dql_fixture_conn();
    let result = execute(
        &query,
        &conn,
        &EngineCtx {
            this_path: Some("123.md".to_string()),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("execute converted this.file special fields");

    assert_eq!(warnings, []);
    assert_eq!(
        first_value(&result, 0),
        &Value::Date(slate_core::bases::eval::DateValue {
            epoch_ms: 1_767_225_600_000,
            has_time: false,
        })
    );
    assert_eq!(
        first_value(&result, 1),
        &Value::Date(slate_core::bases::eval::DateValue {
            epoch_ms: 1_767_312_000_000,
            has_time: false,
        })
    );
    assert!(matches!(first_value(&result, 2), Value::Link(link) if link.target == "123.md"));
    assert_eq!(first_value(&result, 3), &Value::List(Vec::new()));
    assert_eq!(first_value(&result, 4), &Value::List(Vec::new()));
}

#[test]
fn dql_this_file_ordinary_fields_map_exactly() {
    let source = r#"TABLE WITHOUT ID this.file.name AS "Name", this.file.path AS "Path", this.file.folder AS "Folder", this.file.ext AS "Ext", this.file.size AS "Size", this.file.ctime AS "Ctime", this.file.mtime AS "Mtime", this.file.tags AS "Tags", this.file.aliases AS "Aliases"
WHERE file.path = "123.md"
"#;
    let (query, warnings) = parse_dql(source);
    let result = execute(
        &query,
        &dql_fixture_conn(),
        &EngineCtx {
            this_path: Some("123.md".into()),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("execute ordinary this.file fields");

    assert_eq!(warnings, []);
    assert_eq!(first_value(&result, 0), &Value::Text("123".into()));
    assert_eq!(first_value(&result, 1), &Value::Text("123.md".into()));
    assert_eq!(first_value(&result, 2), &Value::Text(String::new()));
    assert_eq!(first_value(&result, 3), &Value::Text("md".into()));
    assert_eq!(first_value(&result, 4), &Value::Number(123.0));
    assert!(matches!(first_value(&result, 5), Value::Date(_)));
    assert!(matches!(first_value(&result, 6), Value::Date(_)));
    assert_eq!(
        first_value(&result, 7),
        &Value::List(vec![
            Value::Text("#Reading".into()),
            Value::Text("#A/B".into()),
            Value::Text("#A".into()),
        ])
    );
    assert_eq!(
        first_value(&result, 8),
        &Value::List(vec![
            Value::Text("One".into()),
            Value::Text("Two".into()),
            Value::Text("A,B".into()),
            Value::Text("0".into()),
            Value::Text("false".into()),
            Value::Text(String::new()),
            Value::Text("Three".into()),
        ])
    );
}

#[test]
fn dql_file_aliases_use_frontmatter_only() {
    let conn = dql_fixture_conn();
    conn.execute(
        r#"UPDATE properties
           SET value_text = '["One","Two","A,B",0,false,"",null]',
               value_text_norm = '["one","two","a,b",0,false,"",null]'
           WHERE file_id = 4 AND ordinal = 1"#,
        [],
    )
    .expect("add null list alias control");
    conn.execute(
        "INSERT INTO files (
            id, path, name, extension, size_bytes, mtime_ms, ctime_ms,
            content_hash, parser_version, indexed_at_ms, is_markdown
         ) VALUES (31, 'ScalarAlias.md', 'ScalarAlias.md', 'md', 0, 0, 0,
                   'scalar-alias', 1, 0, 1)",
        [],
    )
    .expect("insert scalar alias control file");
    conn.execute(
        r#"INSERT INTO properties (
            file_id, ordinal, key, value_kind, value_text, value_text_norm
         ) VALUES (31, 0, 'aliases', 'text', '"Solo,, Pair, "', 'solo,, pair, ')"#,
        [],
    )
    .expect("insert scalar alias control");

    let result = execute_dql(
        &conn,
        r#"TABLE WITHOUT ID file.aliases AS "Aliases", this.file.aliases AS "This aliases"
WHERE file.path = "123.md"
"#,
        Some("123.md"),
    );
    let expected = Value::List(vec![
        Value::Text("One".into()),
        Value::Text("Two".into()),
        Value::Text("A,B".into()),
        Value::Text("0".into()),
        Value::Text("false".into()),
        Value::Text(String::new()),
        Value::Text("null".into()),
        Value::Text("Three".into()),
    ]);

    assert_eq!(first_value(&result, 0), &expected);
    assert_eq!(first_value(&result, 1), &expected);
    assert_eq!(result.error, None);

    let scalar = execute_dql(
        &conn,
        "TABLE WITHOUT ID file.aliases AS \"Aliases\"\nWHERE file.path = \"ScalarAlias.md\"\n",
        None,
    );
    assert_eq!(
        first_value(&scalar, 0),
        &Value::List(vec![Value::Text("Solo".into()), Value::Text("Pair".into()),])
    );
}

#[test]
fn dql_file_literal_brackets_map_like_fields_and_dynamic_keys_fail_loud() {
    let source = r#"TABLE WITHOUT ID file["name"] AS "Name", file["link"] AS "Link", this.file["name"] AS "This name"
WHERE file.path = "123.md"
"#;
    let (query, warnings) = parse_dql(source);
    let result = execute(
        &query,
        &dql_fixture_conn(),
        &EngineCtx {
            this_path: Some("123.md".into()),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("execute literal file bracket mappings");
    assert_eq!(warnings, []);
    assert_eq!(first_value(&result, 0), &Value::Text("123".into()));
    assert!(matches!(first_value(&result, 1), Value::Link(link) if link.target == "123.md"));
    assert_eq!(first_value(&result, 2), &Value::Text("123".into()));

    let collision = execute_dql(
        &dql_fixture_conn(),
        "TABLE WITHOUT ID {this: {file: {day: \"ok\"}}}.this.file[\"day\"] AS \"Value\"\nWHERE file.path = \"123.md\"\n",
        None,
    );
    assert_eq!(first_value(&collision, 0), &Value::Text("ok".into()));

    for source in [
        "TABLE WITHOUT ID file[key] AS \"Dynamic\"\n",
        "TABLE WITHOUT ID this.file[key] AS \"Dynamic\"\n",
        "TABLE WITHOUT ID file[\"day\"] AS \"Unsupported\"\n",
    ] {
        let (query, warnings) = parse_dql(source);
        assert!(
            warnings
                .iter()
                .any(|warning| warning.kind == DqlWarningKind::UnsupportedConstruct)
        );
        assert!(matches!(
            query.formulas[0].1.kind,
            ExprKind::Unsupported { .. }
        ));
    }
}

#[test]
fn dql_hyphenated_and_row_keyword_properties_preserve_authored_lookup() {
    let source = r#"TABLE WITHOUT ID total-cost AS "Hyphen", my-field AS "Sanitized", phase-2 AS "Digit", a--b AS "Repeated", myfield AS "Punctuation", total - cost AS "Subtract", reading-status AS "Status", row["where"] AS "Row index", row.where AS "Row member", this.my-field AS "This", this["my-field"] AS "This bracket", {reading-status: 1}["reading-status"] AS "Object key"
WHERE file.path = "123.md"
"#;
    let result = execute_dql(&dql_fixture_conn(), source, Some("123.md"));

    assert_eq!(first_value(&result, 0), &Value::Number(99.0));
    assert_eq!(
        first_value(&result, 1),
        &Value::List(vec![Value::Number(88.0), Value::Number(55.0)])
    );
    assert_eq!(first_value(&result, 2), &Value::Number(22.0));
    assert_eq!(first_value(&result, 3), &Value::Number(33.0));
    assert_eq!(
        first_value(&result, 4),
        &Value::List(vec![Value::Number(44.0), Value::Number(66.0)])
    );
    assert_eq!(first_value(&result, 5), &Value::Number(7.0));
    assert_eq!(first_value(&result, 6), &Value::Text("done".into()));
    assert_eq!(first_value(&result, 7), &Value::Text("keyword".into()));
    assert_eq!(first_value(&result, 8), &Value::Text("keyword".into()));
    assert_eq!(
        first_value(&result, 9),
        &Value::List(vec![Value::Number(88.0), Value::Number(55.0)])
    );
    assert_eq!(first_value(&result, 10), first_value(&result, 9));
    assert_eq!(first_value(&result, 11), &Value::Number(1.0));
    assert_eq!(result.error, None);

    let exact = execute_dql(
        &dql_fixture_conn(),
        r#"TABLE WITHOUT ID row["My Field"] AS "Row exact", this["My Field"] AS "This exact", my-field AS "Bare canonical"
WHERE file.path = "Hub.md"
"#,
        Some("Hub.md"),
    );
    assert_eq!(first_value(&exact, 0), &Value::Number(1.0));
    assert_eq!(first_value(&exact, 1), &Value::Number(1.0));
    assert_eq!(first_value(&exact, 2), &Value::Number(2.0));

    let (query, warnings) = parse_dql("TABLE WITHOUT ID row[key] AS \"Dynamic\"\n");
    assert!(warnings.iter().any(|warning| {
        warning.kind == DqlWarningKind::UnsupportedConstruct
            && warning.message.contains("dynamic DQL row bracket access")
    }));
    assert!(matches!(
        query.formulas[0].1.kind,
        ExprKind::Unsupported { .. }
    ));
}

#[test]
fn dql_inline_fields_merge_with_frontmatter_and_fail_loud_when_incomplete() {
    let conn = dql_fixture_conn();
    conn.execute(
        "INSERT INTO properties (
            file_id, ordinal, key, value_kind, value_text, value_text_norm
         )
         VALUES (4, 24, 'x', 'list', '[1,2]', '[1,2]')",
        [],
    )
    .expect("insert repeated list-valued frontmatter property");
    conn.execute(
        "INSERT INTO dql_inline_fields (file_id, ordinal, key, value_json)
         VALUES (4, 6, 'x', '{\"kind\":\"number\",\"value\":3.0}')",
        [],
    )
    .expect("insert repeated scalar inline property");

    let repeated = execute_dql(
        &conn,
        "TABLE WITHOUT ID x AS \"Repeated\"\nWHERE file.path = \"123.md\"\n",
        None,
    );
    assert_eq!(
        first_value(&repeated, 0),
        &Value::List(vec![
            Value::List(vec![Value::Number(1.0), Value::Number(2.0)]),
            Value::Number(3.0),
        ])
    );

    let result = execute_dql(
        &conn,
        r#"TABLE WITHOUT ID inline-field AS "Merged", exact-key AS "Exact", inline-date AS "Date", inline-duration AS "Duration", inline-link AS "Link"
WHERE file.path = "123.md"
"#,
        None,
    );
    assert_eq!(
        first_value(&result, 0),
        &Value::List(vec![
            Value::Number(1.0),
            Value::Text("list".into()),
            Value::Text("page".into()),
        ])
    );
    assert_eq!(first_value(&result, 1), &Value::Number(9.0));
    assert!(matches!(first_value(&result, 2), Value::DqlDate(_)));
    assert_eq!(
        first_value(&result, 3),
        &Value::DqlDuration(DqlDurationValue {
            days: 1.0,
            ..DqlDurationValue::default()
        })
    );
    assert!(matches!(
        first_value(&result, 4),
        Value::Link(link)
            if link.target == "Target.md"
                && link.resolved_path.as_deref() == Some("Target.md")
    ));

    let membership = execute_dql(
        &conn,
        "LIST WITHOUT ID file.path\nWHERE contains(inline-field, \"page\")\n",
        None,
    );
    assert_eq!(row_paths(&membership), ["123.md"]);

    conn.execute(
        "UPDATE dql_inline_field_state SET incomplete = 1 WHERE file_id = 3",
        [],
    )
    .expect("mark inline fixture incomplete");
    let incomplete = execute_dql(
        &conn,
        "TABLE WITHOUT ID inline-field AS \"Value\"\nWHERE file.path = \"Other.md\"\n",
        None,
    );
    assert_fail_loud(
        &incomplete,
        "inline-field index is incomplete",
        "incomplete inline index",
    );
}

#[test]
fn dql_inline_links_resolve_from_the_owning_row_without_changing_query_link_context() {
    let conn = dql_fixture_conn();
    for (id, path, extension, is_markdown) in [
        (10_i64, "A/Owner.md", "md", 1_i64),
        (11, "A/Target.md", "md", 1),
        (12, "B/Owner.md", "md", 1),
        (13, "B/Target.md", "md", 1),
        (14, "B/View.base", "base", 0),
    ] {
        conn.execute(
            "INSERT INTO files (
                id, path, name, extension, size_bytes, mtime_ms, ctime_ms,
                content_hash, parser_version, indexed_at_ms, is_markdown
             )
             VALUES (?1, ?2, ?2, ?3, 0, 0, 0, ?4, 1, 0, ?5)",
            params![
                id,
                path,
                extension,
                format!("inline-owner-{id}"),
                is_markdown
            ],
        )
        .expect("insert owner-relative inline-link fixture file");
        conn.execute(
            "INSERT INTO dql_inline_field_state (file_id, incomplete) VALUES (?1, 0)",
            params![id],
        )
        .expect("mark owner-relative inline-link projection complete");
    }
    for file_id in [10_i64, 12_i64] {
        conn.execute(
            "INSERT INTO dql_inline_fields (file_id, ordinal, key, value_json)
             VALUES (
                ?1,
                0,
                'sibling',
                '{\"kind\":\"link\",\"value\":{\"target\":\"Target\",\"display\":null,\"embed\":false,\"link_type\":\"file\",\"subpath\":null}}'
             )",
            params![file_id],
        )
        .expect("insert owner-relative inline link");
    }

    let result = execute_dql(
        &conn,
        r#"TABLE WITHOUT ID sibling AS "Stored", link("Target") AS "Query"
WHERE file.path = "A/Owner.md" OR file.path = "B/Owner.md"
"#,
        Some("B/View.base"),
    );
    assert_eq!(row_paths(&result), ["A/Owner.md", "B/Owner.md"]);

    for (row, expected_stored) in result.rows.iter().zip(["A/Target.md", "B/Target.md"]) {
        assert!(matches!(
            &row.cells[0],
            CellValue::Value(Value::Link(link))
                if link.target == expected_stored
                    && link.resolved_path.as_deref() == Some(expected_stored)
        ));
        assert!(matches!(
            &row.cells[1],
            CellValue::Value(Value::Link(link))
                if link.target == "B/Target.md"
                    && link.resolved_path.as_deref() == Some("B/Target.md")
        ));
    }
}

#[test]
fn dql_file_name_is_markdown_title_and_preserves_other_extensions() {
    let conn = dql_fixture_conn();
    for (path, expected) in [
        ("123.md", "123"),
        ("Deep/Nested Note.md", "Nested Note"),
        ("Assets/Board.canvas", "Board.canvas"),
    ] {
        let source =
            format!("TABLE WITHOUT ID file.name AS \"Name\"\nWHERE file.path = {path:?}\n");
        let result = execute_dql(&conn, &source, None);
        assert_eq!(first_value(&result, 0), &Value::Text(expected.into()));

        let result = execute_dql(
            &conn,
            "TABLE WITHOUT ID this.file.name AS \"Name\"\nWHERE file.path = \"123.md\"\n",
            Some(path),
        );
        assert_eq!(first_value(&result, 0), &Value::Text(expected.into()));
    }
}

#[test]
fn dql_from_wikilinks_ignore_display_and_subpath() {
    let conn = dql_fixture_conn();
    for source in [
        "LIST WITHOUT ID file.path\nFROM [[Target.md|Alias]]\n",
        "LIST WITHOUT ID file.path\nFROM [[Target.md#Heading|Alias]]\n",
    ] {
        let result = execute_dql(&conn, source, None);
        assert_eq!(row_paths(&result), ["Hub.md"]);
    }

    for source in [
        "LIST WITHOUT ID file.path\nFROM outgoing([[Hub.md|Alias]])\n",
        "LIST WITHOUT ID file.path\nFROM outgoing([[Hub.md#Heading]])\n",
    ] {
        let result = execute_dql(&conn, source, None);
        assert_eq!(row_paths(&result), ["Target.md"]);
    }
}

#[test]
fn dql_from_boolean_operators_are_left_associative_without_parentheses() {
    let conn = dql_fixture_conn();
    let left = execute_dql(
        &conn,
        "LIST WITHOUT ID file.path\nFROM #project or #reading and \"Archive\"\n",
        None,
    );
    assert!(left.rows.is_empty());

    let grouped = execute_dql(
        &conn,
        "LIST WITHOUT ID file.path\nFROM #project or (#reading and \"Archive\")\n",
        None,
    );
    assert_eq!(row_paths(&grouped), ["Hub.md"]);

    let membership = execute_dql(
        &conn,
        "LIST WITHOUT ID file.path\nFROM #project and \"Archive\" or [[Target.md]]\n",
        None,
    );
    assert_eq!(row_paths(&membership), ["Hub.md"]);
}

#[test]
fn dql_truthiness_covers_top_level_boolean_choice_and_filter_positions() {
    let conn = dql_fixture_conn();
    let source = r#"TABLE WITHOUT ID choice(dur("0s"), "yes", "no") AS "Duration", choice(date("1970-01-02"), "yes", "no") AS "Date", choice(link(""), "yes", "no") AS "Link", !dur("0s") AS "Not", dur("0s") OR true AS "Or", filter([dur("0s"), dur("1s")], (item) => item) AS "Filter", choice(0 / 0, "yes", "no") AS "NaN"
WHERE file.path = "123.md"
"#;
    let result = execute_dql(&conn, source, None);
    assert_eq!(first_value(&result, 0), &Value::Text("no".into()));
    assert_eq!(first_value(&result, 1), &Value::Text("yes".into()));
    assert_eq!(first_value(&result, 2), &Value::Text("no".into()));
    assert_eq!(first_value(&result, 3), &Value::Bool(true));
    assert_eq!(first_value(&result, 4), &Value::Bool(true));
    assert_eq!(
        first_value(&result, 5),
        &Value::List(vec![Value::DqlDuration(DqlDurationValue {
            seconds: 1.0,
            ..DqlDurationValue::default()
        })])
    );
    assert_eq!(first_value(&result, 6), &Value::Text("yes".into()));

    for predicate in ["dur(\"0s\")", "link(\"\")"] {
        let source = format!("LIST WITHOUT ID file.path\nWHERE {predicate}\n");
        let result = execute_dql(&conn, &source, None);
        assert!(
            result.rows.is_empty(),
            "predicate should be DQL-false: {predicate}"
        );
    }
    let date = execute_dql(
        &conn,
        "LIST WITHOUT ID file.path\nWHERE date(\"1970-01-02\")\n",
        None,
    );
    assert!(!date.rows.is_empty(), "a non-epoch DQL date is truthy");
    let nan = execute_dql(&conn, "LIST WITHOUT ID file.path\nWHERE 0 / 0\n", None);
    assert!(!nan.rows.is_empty(), "Dataview treats NaN as truthy");

    let eager_values = execute_dql(
        &conn,
        r#"TABLE WITHOUT ID true AND true AS "And", false OR true AS "Or", choice(false, "a", "b") AS "Choice", default(missing, "x") AS "Null default", default("x", "y") AS "Value default"
WHERE file.path = "123.md"
"#,
        None,
    );
    assert_eq!(first_value(&eager_values, 0), &Value::Bool(true));
    assert_eq!(first_value(&eager_values, 1), &Value::Bool(true));
    assert_eq!(first_value(&eager_values, 2), &Value::Text("b".into()));
    assert_eq!(first_value(&eager_values, 3), &Value::Text("x".into()));
    assert_eq!(first_value(&eager_values, 4), &Value::Text("x".into()));

    for expression in [
        "false AND flat(1)",
        "choice(true, \"ok\", flat(1))",
        "default(\"x\", flat(1))",
    ] {
        let source =
            format!("TABLE WITHOUT ID {expression} AS \"Value\"\nWHERE file.path = \"123.md\"\n");
        let (query, warnings) = parse_dql(&source);
        assert!(
            !warnings.is_empty(),
            "invalid eager branch should be rejected during conversion: {expression}"
        );
        let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
            .expect("execute fail-loud eager branch");
        assert_fail_loud(&result, "flat", expression);
    }
}

#[test]
fn dql_exact_runtime_semantics_cover_constructors_operators_and_nulls() {
    const CHILD: &str = "SLATE_DQL_EXACT_RUNTIME_TZ_CHILD";
    if std::env::var(CHILD).as_deref() != Ok("1") {
        let status = std::process::Command::new(
            std::env::current_exe().expect("locate DQL exact-runtime test binary"),
        )
        .arg("dql_exact_runtime_semantics_cover_constructors_operators_and_nulls")
        .arg("--exact")
        .arg("--nocapture")
        .env("TZ", "America/New_York")
        .env(CHILD, "1")
        .status()
        .expect("run DQL exact-runtime test in America/New_York");
        assert!(status.success(), "DQL exact-runtime timezone child failed");
        return;
    }

    let conn = dql_fixture_conn();
    let source = r#"TABLE WITHOUT ID number(42) AS "Number", link(link("Target.md")) AS "Link identity", date(link("2026-07-10", "2020-01-01")) AS "Link date", embed(link("Target.md")) AS "Embed", embed(missing, 42) AS "Null embed left", embed(link("Target.md"), missing) AS "Null embed right", slice([1, 2]) AS "Slice", sum([5]) AS "Sum", average([5]) AS "Average", sum([missing]) AS "Sum only null", regextest(missing, "x") AS "Regex null", regexreplace("x", missing, "y") AS "Replace null", split("x", missing) AS "Split null", string([1, [2, 3]]) AS "Nested string", string(date("2026-07-10")) AS "Date string", string(dur("1d")) AS "Duration string", string(link("Target.md")) AS "Link string", join([1, [2, 3], {a: 1}], missing) AS "Join", "done: " + true AS "Concat bool", "values: " + [1, 2] AS "Concat list", "x" * -2 AS "Negative right", -2 * "x" AS "Negative left", dur("1d") * 2 AS "Duration multiply", missing + missing AS "Null add", date("2026-07-10") - missing AS "Date null", striptime(date("2026-07-10T12:00:00")) AS "Strip", replace(true, missing, true) AS "Literal null precedence", regexreplace(true, missing, true) AS "Regex null precedence", dur("-1day, 2hrs") AS "Negative duration", dur("1yrs 2mos 3wks") AS "Aliases", dur("1ms") AS "Invalid ms", dur("1M") AS "Invalid M", dur("1y") AS "Invalid y", dur(1day) AS "Raw"
WHERE file.path = "123.md"
"#;
    let result = execute_dql(&conn, source, None);
    assert_eq!(result.error, None);
    let expected = vec![
        Value::Number(42.0),
        first_value(&result, 1).clone(),
        Value::DqlDate(DqlDateValue {
            epoch_ms: 1_577_854_800_000,
            has_time: false,
            offset_minutes: -300,
            is_local: true,
        }),
        first_value(&result, 3).clone(),
        Value::Null,
        Value::Null,
        Value::List(vec![Value::Number(1.0), Value::Number(2.0)]),
        Value::Number(5.0),
        Value::Number(5.0),
        Value::Null,
        Value::Bool(false),
        Value::Null,
        Value::Null,
        Value::Text("1, [2, 3]".into()),
        Value::Text("July 10, 2026".into()),
        Value::Text("1 day".into()),
        Value::Text("[[Target.md|Target]]".into()),
        Value::Text("1, [2, 3], { a: 1 }".into()),
        Value::Text("done: true".into()),
        Value::Text("values: 1, 2".into()),
        Value::Text(String::new()),
        Value::Text(String::new()),
        Value::DqlDuration(DqlDurationValue {
            days: 2.0,
            ..DqlDurationValue::default()
        }),
        Value::Null,
        Value::Null,
        Value::DqlDate(DqlDateValue {
            epoch_ms: 1_783_656_000_000,
            has_time: false,
            offset_minutes: -240,
            is_local: true,
        }),
        Value::Null,
        Value::Null,
        Value::DqlDuration(DqlDurationValue {
            days: -1.0,
            hours: 2.0,
            ..DqlDurationValue::default()
        }),
        Value::DqlDuration(DqlDurationValue {
            years: 1.0,
            months: 2.0,
            weeks: 3.0,
            ..DqlDurationValue::default()
        }),
        Value::Null,
        Value::Null,
        Value::Null,
        Value::DqlDuration(DqlDurationValue {
            days: 1.0,
            ..DqlDurationValue::default()
        }),
    ];
    for (column, expected) in expected.iter().enumerate() {
        assert_eq!(first_value(&result, column), expected, "column {column}");
    }
    assert!(matches!(first_value(&result, 1), Value::Link(link) if link.target == "Target.md"));
    assert!(matches!(first_value(&result, 3), Value::Link(link) if link.target == "Target.md"));

    for (expression, error) in [
        ("number(true)", "number"),
        ("date(1)", "date"),
        ("dur(1)", "duration"),
        ("object(1, \"x\")", "object"),
        ("object({a: 1})", "object"),
        ("link(\"Target.md\", true)", "link"),
        ("embed(\"Target.md\")", "embed"),
        ("flat([ [1] ], true)", "flat"),
        ("slice([1, 2], true)", "slice"),
        ("striptime(86400000)", "striptime"),
    ] {
        let source =
            format!("TABLE WITHOUT ID {expression} AS \"Value\"\nWHERE file.path = \"123.md\"\n");
        let result = execute_dql(&conn, &source, None);
        assert_fail_loud(&result, error, expression);
    }
}

#[test]
fn dql_nonfinite_numbers_use_javascript_string_spelling() {
    let result = execute_dql(
        &dql_fixture_conn(),
        r#"TABLE WITHOUT ID string(1 / 0) AS "Positive", string(-1 / 0) AS "Negative", string(0 / 0) AS "NaN", string(-0) AS "Zero", "x" + (1 / 0) AS "Concat"
WHERE file.path = "123.md"
"#,
        None,
    );
    for (column, expected) in [
        (0, "Infinity"),
        (1, "-Infinity"),
        (2, "NaN"),
        (3, "0"),
        (4, "xInfinity"),
    ] {
        assert_eq!(
            first_value(&result, column),
            &Value::Text(expected.to_string()),
            "column {column}"
        );
    }
}

#[test]
fn dql_durations_preserve_units_grammar_truth_and_calendar_arithmetic() {
    let result = execute_dql(
        &dql_fixture_conn(),
        r#"TABLE WITHOUT ID dur("1week") = dur("7days") AS "Week identity", dur("1year") = dur("12months") AS "Year identity", string(dur("1week")) AS "Week string", string(dur("1year")) AS "Year string", length(unique([dur("1week"), dur("7days")])) AS "Unique", contains([dur("1week")], dur("7days")) AS "Contains", choice(dur("1month,-30days"), "yes", "no") AS "Canceled truth", date("2026-01-31") + dur("1month") = date("2026-02-28") AS "Month clamp", string(date("2026-02-28") - date("2026-01-31")) AS "Month diff", string(date("2027-01-31") - date("2026-01-31")) AS "Year diff", string(dur("0.1year")) AS "Fraction", (dur("24hours") + dur("0seconds")) = dur("1day") AS "Normalized equality", string(dur("24hours") + dur("0seconds")) AS "Normalized string"
WHERE file.path = "123.md"
"#,
        None,
    );
    let expected = [
        Value::Bool(false),
        Value::Bool(false),
        Value::Text("1 week".into()),
        Value::Text("1 year".into()),
        Value::Number(2.0),
        Value::Bool(false),
        Value::Text("no".into()),
        Value::Bool(true),
        Value::Text("1 month".into()),
        Value::Text("1 year".into()),
        Value::Text("0.1 years".into()),
        Value::Bool(true),
        Value::Text("1 day".into()),
    ];
    for (column, expected) in expected.iter().enumerate() {
        assert_eq!(first_value(&result, column), expected, "column {column}");
    }

    let invalid = execute_dql(
        &dql_fixture_conn(),
        r#"TABLE WITHOUT ID dur(",1d") AS "Leading", dur("1d,") AS "Trailing", dur("1d,,2h") AS "Repeated", dur("-.5d") AS "Leading decimal", dur("1.d") AS "Trailing decimal", dur("1ms") AS "Short milliseconds", dur("1 millisecond") AS "Milliseconds"
WHERE file.path = "123.md"
"#,
        None,
    );
    for column in 0..7 {
        assert_eq!(
            first_value(&invalid, column),
            &Value::Null,
            "column {column}"
        );
    }
}

#[test]
fn dql_ordering_is_total_with_pinned_locale_and_object_limits() {
    let conn = dql_fixture_conn();
    let result = execute_dql(
        &conn,
        r#"TABLE WITHOUT ID sort([1, "a", missing]) AS "Mixed", [1] < [2] AS "Lists", {a: 1} < {a: 2} AS "Objects", 1 < "a" AS "Types", (0 / 0) > (0 / 0) AS "NaN", length(unique([0 / 0, 0 / 0])) AS "NaN unique", {b: 1, a: 2} < {a: 1, b: 3} AS "Multi object"
WHERE file.path = "123.md"
"#,
        None,
    );
    assert_eq!(
        first_value(&result, 0),
        &Value::List(vec![
            Value::Null,
            Value::Number(1.0),
            Value::Text("a".into()),
        ])
    );
    assert_eq!(first_value(&result, 1), &Value::Bool(true));
    assert_eq!(first_value(&result, 2), &Value::Bool(true));
    assert_eq!(first_value(&result, 3), &Value::Bool(true));
    assert_eq!(first_value(&result, 4), &Value::Bool(true));
    assert_eq!(first_value(&result, 5), &Value::Number(2.0));
    assert_eq!(first_value(&result, 6), &Value::Bool(false));

    for (expression, message) in [
        ("\"A\" < \"b\"", "locale collation"),
        ("\"é\" < \"z\"", "locale collation"),
        ("sort([\"a\", \"B\"])", "locale collation"),
        ("{A: 1} < {b: 1}", "locale collation"),
        ("object(\"é\", 1) < object(\"é\", 2)", "locale collation"),
        ("string({b: 1, a: 2})", "multi-key objects"),
    ] {
        let source =
            format!("TABLE WITHOUT ID {expression} AS \"Value\"\nWHERE file.path = \"123.md\"\n");
        assert_fail_loud(&execute_dql(&conn, &source, None), message, expression);
    }
}

#[test]
fn dql_aggregate_and_list_null_scalar_overloads_match_dataview() {
    let conn = dql_fixture_conn();
    let result = execute_dql(
        &conn,
        r#"TABLE WITHOUT ID join(42) AS "Scalar join", join(42, "-") AS "Scalar separator", flat(missing) AS "Flat", unique(missing) AS "Unique", slice(missing) AS "Slice", map(missing, (x) => x) AS "Map", filter(missing, (x) => x) AS "Filter", min([dur("1week"), dur("7days")]) = dur("7days") AS "Min", max([dur("1week"), dur("7days")]) = dur("1week") AS "Max", dur("1week") > dur("7days") AS "Forward tie", dur("7days") > dur("1week") AS "Reverse tie"
WHERE file.path = "123.md"
"#,
        None,
    );
    assert_eq!(first_value(&result, 0), &Value::Text("42".into()));
    assert_eq!(first_value(&result, 1), &Value::Text("42".into()));
    for column in 2..7 {
        assert_eq!(
            first_value(&result, column),
            &Value::Null,
            "column {column}"
        );
    }
    for column in 7..11 {
        assert_eq!(
            first_value(&result, column),
            &Value::Bool(true),
            "column {column}"
        );
    }
}

#[test]
fn dql_grammar_regex_lower_and_dataarray_projection_are_pinned() {
    let conn = dql_fixture_conn();
    let result = execute_dql(
        &conn,
        r#"TABLE WITHOUT ID regexmatch("^foo", "foobar") AS "Leading", regexmatch("foo$", "xfoo") AS "Trailing", regexmatch("foo", "xfoo") AS "Whole", lower("ABC") AS "Lower", [{a: 1}, {a: [2, 3]}].a AS "Projection", "x" * -2 AS "Negative literal"
WHERE file.path = "123.md"
"#,
        None,
    );
    assert_eq!(first_value(&result, 0), &Value::Bool(true));
    assert_eq!(first_value(&result, 1), &Value::Bool(true));
    assert_eq!(first_value(&result, 2), &Value::Bool(false));
    assert_eq!(first_value(&result, 3), &Value::Text("abc".into()));
    assert_eq!(
        first_value(&result, 4),
        &Value::List(vec![
            Value::Number(1.0),
            Value::Number(2.0),
            Value::Number(3.0),
        ])
    );
    assert_eq!(first_value(&result, 5), &Value::Text(String::new()));

    let sparse_projection = execute_dql(
        &conn,
        r#"TABLE WITHOUT ID sum([{a: 1}, {}].a) AS "Missing", [{a: [1, 2]}, {a: [3]}].a AS "Flatten", [{a: missing}, {a: 2}].a AS "Explicit null"
WHERE file.path = "123.md"
"#,
        None,
    );
    assert_eq!(first_value(&sparse_projection, 0), &Value::Number(1.0));
    assert_eq!(
        first_value(&sparse_projection, 1),
        &Value::List(vec![
            Value::Number(1.0),
            Value::Number(2.0),
            Value::Number(3.0),
        ])
    );
    assert_eq!(
        first_value(&sparse_projection, 2),
        &Value::List(vec![Value::Number(2.0)])
    );

    for (expression, reason) in [
        ("LOWER(\"A\")", "case-sensitive lowercase"),
        ("SUM([1])", "case-sensitive lowercase"),
        ("'x'", "double quotes"),
        ("-rating", "unary minus"),
        ("map([1], x => x)", "lambda"),
        ("lower(\"İ\")", "locale-aware lowercasing"),
    ] {
        let source =
            format!("TABLE WITHOUT ID {expression} AS \"Value\"\nWHERE file.path = \"123.md\"\n");
        let (query, warnings) = parse_dql(&source);
        let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
            .expect("execute grammar fail-loud control");
        assert!(
            !warnings.is_empty()
                || result.rows[0]
                    .cells
                    .iter()
                    .any(|cell| matches!(cell, CellValue::Error(_))),
            "grammar control must fail loud: {expression}"
        );
        assert_fail_loud(&result, reason, expression);
    }
}

#[test]
fn dql_dates_preserve_local_or_explicit_zone_and_exact_parser_shapes() {
    let conn = dql_fixture_conn();
    let result = execute_dql(
        &conn,
        r#"TABLE WITHOUT ID string(date("2026-07")) AS "Month", date("2026-07-10T12").hour AS "Hour", date("2026-07-10T12:34").minute AS "Minute", string(date("2026-07-10T12:00:00-04:00")) AS "Offset", date(link("today")) AS "Link shorthand", string(date(link("2026-07-10"))) AS "Link date", date("2026-07-10 12:00:00") AS "Space", date(choice(true, "sow", "eow")) = date(sow) AS "Dynamic shorthand", date(choice(true, "start-of-week", "end-of-week")) = date(start-of-week) AS "Long shorthand", string(date("2026-03-07T12") + dur("1day")) AS "DST calendar day"
WHERE file.path = "123.md"
"#,
        None,
    );
    let aliases = execute_dql(
        &conn,
        r#"TABLE WITHOUT ID string(date(choice(true, "start-of-week", "end-of-week"))) AS "Dynamic", string(date(start-of-week)) AS "Literal"
WHERE file.path = "123.md"
"#,
        None,
    );
    assert_eq!(first_value(&aliases, 0), first_value(&aliases, 1));
    let period_ends = execute_dql(
        &conn,
        r#"TABLE WITHOUT ID date(eow).hour AS "Week hour", date(eow).minute AS "Week minute", date(eow).second AS "Week second", date(eow).millisecond AS "Week millisecond", date(eom).hour AS "Month hour", date(eom).minute AS "Month minute", date(eom).second AS "Month second", date(eom).millisecond AS "Month millisecond", date(eoy).hour AS "Year hour", date(eoy).minute AS "Year minute", date(eoy).second AS "Year second", date(eoy).millisecond AS "Year millisecond"
WHERE file.path = "123.md"
"#,
        None,
    );
    for group in 0..3 {
        let offset = group * 4;
        assert_eq!(first_value(&period_ends, offset), &Value::Number(23.0));
        assert_eq!(first_value(&period_ends, offset + 1), &Value::Number(59.0));
        assert_eq!(first_value(&period_ends, offset + 2), &Value::Number(59.0));
        assert_eq!(first_value(&period_ends, offset + 3), &Value::Number(999.0));
    }
    assert_eq!(
        first_value(&result, 0),
        &Value::Text("July 01, 2026".into())
    );
    assert_eq!(first_value(&result, 1), &Value::Number(12.0));
    assert_eq!(first_value(&result, 2), &Value::Number(34.0));
    assert_eq!(
        first_value(&result, 3),
        &Value::Text("12:00 PM - July 10, 2026".into())
    );
    assert_eq!(first_value(&result, 4), &Value::Null);
    assert_eq!(
        first_value(&result, 5),
        &Value::Text("July 10, 2026".into())
    );
    assert_eq!(first_value(&result, 6), &Value::Null);
    assert_eq!(first_value(&result, 7), &Value::Bool(true));
    assert_eq!(first_value(&result, 8), &Value::Bool(true));
    assert_eq!(
        first_value(&result, 9),
        &Value::Text("12:00 PM - March 08, 2026".into())
    );
    assert!(matches!(first_value(&result, 3), Value::Text(_)));

    let offset = execute_dql(
        &conn,
        "TABLE WITHOUT ID date(\"2026-07-10T12:00:00-04:00\") AS \"Value\"\nWHERE file.path = \"123.md\"\n",
        None,
    );
    assert!(matches!(
        first_value(&offset, 0),
        Value::DqlDate(DqlDateValue {
            offset_minutes: -240,
            is_local: false,
            ..
        })
    ));

    let named = execute_dql(
        &conn,
        "TABLE WITHOUT ID date(\"2026-07-10T12:00:00[America/New_York]\") AS \"Value\"\nWHERE file.path = \"123.md\"\n",
        None,
    );
    assert_fail_loud(&named, "date shape", "named-zone date");

    let fractions = execute_dql(
        &conn,
        r#"TABLE WITHOUT ID date("2026-07-10T12:00:00.123") AS "Local valid", date("2026-07-10T12:00:00.123Z") AS "Zoned valid", date("2026-07-10T12:00:00.1") AS "Local short", date("2026-07-10T12:00:00.12") AS "Local short two", date("2026-07-10T12:00:00.1234") AS "Local long", date("2026-07-10T12:00:00.1Z") AS "Zoned short", date("2026-07-10T12:00:00.1234+01:00") AS "Zoned long"
WHERE file.path = "123.md"
"#,
        None,
    );
    assert!(matches!(first_value(&fractions, 0), Value::DqlDate(_)));
    assert!(matches!(first_value(&fractions, 1), Value::DqlDate(_)));
    for column in 2..7 {
        assert_eq!(
            first_value(&fractions, column),
            &Value::Null,
            "fractional-second column {column}"
        );
    }

    let uppercase_shorthands = execute_dql(
        &conn,
        r#"TABLE WITHOUT ID date(TODAY) AS "Literal", date(choice(true, "TODAY", "today")) AS "Dynamic"
WHERE file.path = "123.md"
"#,
        None,
    );
    assert_eq!(first_value(&uppercase_shorthands, 0), &Value::Null);
    assert_eq!(first_value(&uppercase_shorthands, 1), &Value::Null);
}

#[test]
fn dql_date_link_falls_back_to_query_resolved_page_day() {
    let conn = dql_fixture_conn();
    for (id, path, extension, is_markdown) in [
        (20_i64, "A/Owner.md", "md", 1_i64),
        (21, "A/Target.md", "md", 1),
        (22, "B/Target.md", "md", 1),
        (23, "B/View.base", "base", 0),
        (24, "B/Inline.md", "md", 1),
        (25, "B/Meeting 2033-04-05.md", "md", 1),
        (26, "B/DisplayField.md", "md", 1),
        (27, "B/SubpathField.md", "md", 1),
        (28, "B/PathField.md", "md", 1),
        (29, "B/ListSecond.md", "md", 1),
        (30, "B/LaterKey.md", "md", 1),
        (31, "B/DuplicateDay.md", "md", 1),
        (32, "B/FrontInlineDay.md", "md", 1),
    ] {
        conn.execute(
            "INSERT INTO files (
                id, path, name, extension, size_bytes, mtime_ms, ctime_ms,
                content_hash, parser_version, indexed_at_ms, is_markdown
             )
             VALUES (?1, ?2, ?2, ?3, 0, 0, 0, ?4, 1, 0, ?5)",
            params![id, path, extension, format!("date-link-{id}"), is_markdown],
        )
        .expect("insert date(Link) fallback fixture file");
        conn.execute(
            "INSERT INTO dql_inline_field_state (file_id, incomplete) VALUES (?1, 0)",
            params![id],
        )
        .expect("mark date(Link) inline projection complete");
    }

    for (file_id, ordinal, key, kind, value, normalized) in [
        (
            21_i64,
            0_i64,
            "date",
            "date",
            r#""2030-01-02""#,
            "1893542400000",
        ),
        (22, 0, "DaTe", "date", r#""2031-02-03""#, "1927843200000"),
        (28, 0, "DAY", "wikilink", r#""2036-07-08""#, "2036-07-08"),
        (30, 0, "date", "text", r#""not a date""#, "not a date"),
        (30, 1, "day", "date", r#""2037-08-09""#, "2133388800000"),
        (32, 0, "day", "text", r#""ignored""#, "ignored"),
    ] {
        conn.execute(
            "INSERT INTO properties (
                file_id, ordinal, key, value_kind, value_text, value_text_norm
             )
             VALUES (?1, ?2, ?3, ?4, ?5, ?6)",
            params![file_id, ordinal, key, kind, value, normalized],
        )
        .expect("insert date(Link) frontmatter field");
    }

    for (file_id, ordinal, key, value_json) in [
        (
            22_i64,
            0_i64,
            "day",
            r#"{"kind":"date","value":"2099-12-31"}"#,
        ),
        (
            24,
            0,
            "dAy",
            r#"{"kind":"list","value":[{"kind":"date","value":"2032-03-04"},{"kind":"text","value":"ignored"},{"kind":"date","value":"2032-03-05"}]}"#,
        ),
        (
            26,
            0,
            "DATE",
            r#"{"kind":"link","value":{"target":"Undated","display":"2034-05-06","embed":false,"link_type":"file","subpath":null}}"#,
        ),
        (
            27,
            0,
            "day",
            r#"{"kind":"link","value":{"target":"Undated","display":null,"embed":false,"link_type":"header","subpath":"2035-06-07"}}"#,
        ),
        (
            29,
            0,
            "day",
            r#"{"kind":"list","value":[{"kind":"text","value":"ignored"},{"kind":"date","value":"2038-09-10"}]}"#,
        ),
        (31, 0, "day", r#"{"kind":"text","value":"ignored"}"#),
        (31, 1, "day", r#"{"kind":"date","value":"2038-09-10"}"#),
        (32, 0, "day", r#"{"kind":"date","value":"2039-10-11"}"#),
    ] {
        conn.execute(
            "INSERT INTO dql_inline_fields (file_id, ordinal, key, value_json)
             VALUES (?1, ?2, ?3, ?4)",
            params![file_id, ordinal, key, value_json],
        )
        .expect("insert date(Link) scanner inline field");
    }
    for (ordinal, target_path, target_raw) in [
        (0_i64, "A/Target.md", "Target"),
        (1, "B/Meeting 2033-04-05.md", "DatedAlias"),
    ] {
        conn.execute(
            "INSERT INTO links (
                source_file_id, ordinal, target_path, target_raw, target_anchor,
                kind, is_embed, is_external, snippet, span_start, span_end
             )
             VALUES (20, ?1, ?2, ?3, NULL, 'wikilink', 0, 0, '', 0, 10)",
            params![ordinal, target_path, target_raw],
        )
        .expect("insert raw date(Link) fixture link");
    }

    let result = execute_dql(
        &conn,
        r#"TABLE WITHOUT ID string(date(file.outlinks[0])) AS "Query context", string(date(link("Inline"))) AS "Inline list", string(date(file.outlinks[1])) AS "Dated title", string(date(link("DisplayField"))) AS "Field display", string(date(link("SubpathField"))) AS "Field subpath", string(date(link("PathField"))) AS "Field path", string(date(link("Target", "2040-01-02"))) AS "Direct display", string(date(link("2041-02-03", "not-a-date"))) AS "Direct path", string(date(link("LaterKey"))) AS "Later key"
WHERE file.path = "A/Owner.md"
"#,
        Some("B/View.base"),
    );
    for (column, expected) in [
        (0, "February 03, 2031"),
        (1, "March 04, 2032"),
        (2, "April 05, 2033"),
        (3, "May 06, 2034"),
        (4, "June 07, 2035"),
        (5, "July 08, 2036"),
        (6, "January 02, 2040"),
        (7, "February 03, 2041"),
        (8, "August 09, 2037"),
    ] {
        assert_eq!(
            first_value(&result, column),
            &Value::Text(expected.into()),
            "date(Link) column {column}"
        );
    }

    let direct_negative_controls = execute_dql(
        &conn,
        r#"TABLE WITHOUT ID date(link("ListSecond")) AS "Second list element", date(link("Missing/2042-03-04.md")) AS "Folder basename", date(link("Undated#2043-04-05")) AS "Direct subpath", date(link("DuplicateDay")) AS "Duplicate inline key", date(link("FrontInlineDay")) AS "Frontmatter inline key"
WHERE file.path = "A/Owner.md"
"#,
        Some("B/View.base"),
    );
    for column in 0..5 {
        assert_eq!(
            first_value(&direct_negative_controls, column),
            &Value::Null,
            "date(Link) negative control column {column}"
        );
    }

    conn.execute(
        "UPDATE dql_inline_field_state SET incomplete = 1 WHERE file_id = 24",
        [],
    )
    .expect("mark date(Link) target inline projection incomplete");
    let incomplete = execute_dql(
        &conn,
        "TABLE WITHOUT ID date(link(\"Inline\")) AS \"Date\"\nWHERE file.path = \"A/Owner.md\"\n",
        Some("B/View.base"),
    );
    assert_fail_loud(
        &incomplete,
        "inline-field index is incomplete",
        "date(Link) incomplete target page",
    );
}

#[test]
fn dql_date_difference_uses_calendar_days_across_dst() {
    const CHILD: &str = "SLATE_DQL_DST_DIFF_CHILD";
    if std::env::var(CHILD).as_deref() != Ok("1") {
        let status = std::process::Command::new(
            std::env::current_exe().expect("locate DQL date-difference test binary"),
        )
        .arg("dql_date_difference_uses_calendar_days_across_dst")
        .arg("--exact")
        .arg("--nocapture")
        .env("TZ", "America/New_York")
        .env(CHILD, "1")
        .status()
        .expect("run DQL date-difference test in America/New_York");
        assert!(status.success(), "DQL DST date-difference child failed");
        return;
    }

    let conn = dql_fixture_conn();
    let result = execute_dql(
        &conn,
        r#"TABLE WITHOUT ID date("2026-03-08T12") - date("2026-03-07T12") AS "Spring", date("2026-11-01T12") - date("2026-10-31T12") AS "Fall"
WHERE file.path = "123.md"
"#,
        None,
    );
    let expected = Value::DqlDuration(DqlDurationValue {
        days: 1.0,
        ..DqlDurationValue::default()
    });
    assert_eq!(first_value(&result, 0), &expected);
    assert_eq!(first_value(&result, 1), &expected);

    let mixed = execute_dql(
        &conn,
        r#"TABLE WITHOUT ID date("2026-07-10T12:00:00-04:00") - date("2026-07-10T12:00:00Z") AS "Mixed"
WHERE file.path = "123.md"
"#,
        None,
    );
    assert_fail_loud(
        &mixed,
        "different zone provenance",
        "mixed fixed-offset DQL date subtraction",
    );
}

#[test]
fn dql_date_equality_preserves_zone_provenance() {
    const CHILD: &str = "SLATE_DQL_DATE_EQ_CHILD";
    if std::env::var(CHILD).as_deref() != Ok("1") {
        let status = std::process::Command::new(
            std::env::current_exe().expect("locate DQL date-equality test binary"),
        )
        .arg("dql_date_equality_preserves_zone_provenance")
        .arg("--exact")
        .arg("--nocapture")
        .env("TZ", "America/New_York")
        .env(CHILD, "1")
        .status()
        .expect("run DQL date-equality test in America/New_York");
        assert!(status.success(), "DQL date-equality child failed");
        return;
    }

    let result = execute_dql(
        &dql_fixture_conn(),
        r#"TABLE WITHOUT ID date("2026-07-10T12:00:00-04:00") = date("2026-07-10T16:00:00Z") AS "Fixed zones", date("2026-07-10T12") = date("2026-07-10T12:00:00-04:00") AS "Local fixed", date("2026-07-10T12") = date("2026-07-10T12") AS "Same local", date("2026-07-10T12:00:00-04:00") = date("2026-07-10T12:00:00-04:00") AS "Same fixed"
WHERE file.path = "123.md"
"#,
        None,
    );
    assert_eq!(first_value(&result, 0), &Value::Bool(false));
    assert_eq!(first_value(&result, 1), &Value::Bool(false));
    assert_eq!(first_value(&result, 2), &Value::Bool(true));
    assert_eq!(first_value(&result, 3), &Value::Bool(true));
}

#[test]
fn dql_links_preserve_embed_subpath_resolution_and_markdown_escaping() {
    let conn = dql_fixture_conn();
    let result = execute_dql(
        &conn,
        r#"TABLE WITHOUT ID string(link("Target")) AS "Resolved", link("Target", "Shown", true) AS "Embed", link("Target", "Shown", true) = link("Target.md", "Other", false) AS "Embed equality", link(["Target", "Hub"], ["T", "H"]) AS "Vector", string(link("A|B")) AS "Pipe", string(link("A|B#^block|id")) AS "Block pipe", string(link("Target#Hello,   world!")) AS "Header"
WHERE file.path = "123.md"
"#,
        None,
    );
    assert_eq!(
        first_value(&result, 0),
        &Value::Text("[[Target.md|Target]]".into())
    );
    assert!(matches!(
        first_value(&result, 1),
        Value::Link(link)
            if link.target == "Target.md"
                && link.resolved_path.as_deref() == Some("Target.md")
                && link.display.as_deref() == Some("Shown")
                && link.embed
    ));
    assert_eq!(first_value(&result, 2), &Value::Bool(true));
    assert!(matches!(
        first_value(&result, 3),
        Value::List(values)
            if values.len() == 2
                && values.iter().all(|value| matches!(value, Value::Link(link) if !link.embed))
    ));
    assert_eq!(
        first_value(&result, 4),
        &Value::Text("[[A\\|B|A|B]]".into())
    );
    assert_eq!(
        first_value(&result, 5),
        &Value::Text("[[A\\|B#^block\\|id|A|B > block|id]]".into())
    );
    assert_eq!(
        first_value(&result, 6),
        &Value::Text("[[Target.md#Hello world|Target > Hello world]]".into())
    );

    for (expression, context) in [
        (
            r#"link(["Target", "Hub"], "Shown", true)"#,
            "three-argument link path list",
        ),
        (
            r#"link("Target", ["T", "H"], true)"#,
            "three-argument link display list",
        ),
    ] {
        let source =
            format!("TABLE WITHOUT ID {expression} AS \"Link\"\nWHERE file.path = \"123.md\"\n");
        let (query, warnings) = parse_dql(&source);
        assert!(warnings.iter().any(|warning| {
            warning.kind == DqlWarningKind::UnsupportedConstruct
                && warning
                    .message
                    .contains("three-argument link requires scalar")
        }));
        let unsupported = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
            .expect("execute unsupported three-argument link query");
        assert_fail_loud(&unsupported, "three-argument link requires scalar", context);
    }

    conn.execute(
        "INSERT INTO links (
            source_file_id, ordinal, target_path, target_raw, target_anchor,
            kind, is_embed, is_external, snippet, span_start, span_end
         ) VALUES (1, 9, 'Target.md', 'Target', 'h:Hello,   world!', 'wikilink', 0, 0, '', 0, 10)",
        [],
    )
    .expect("insert normalized heading outlink");
    let outlinks = execute_dql(
        &conn,
        "TABLE WITHOUT ID file.outlinks AS \"Links\"\nWHERE file.path = \"Hub.md\"\n",
        None,
    );
    assert!(matches!(
        first_value(&outlinks, 0),
        Value::List(values)
            if matches!(values.as_slice(), [Value::Link(link)]
                if link.target == "Target"
                    && link.link_type == "file"
                    && link.subpath.is_none())
    ));

    let (source, warnings) = parse_dql("LIST\nFROM [[A\\|B|shown]]\n");
    assert_eq!(warnings, []);
    assert!(matches!(
        source.filters,
        Some(FilterNode::Stmt(Expr {
            kind: ExprKind::Call { ref args, .. },
            ..
        })) if matches!(
            args.first().map(|arg| &arg.kind),
            Some(ExprKind::Lit(Lit::String(path))) if path == "A|B"
        )
    ));
}

#[test]
fn dql_link_equality_uses_normalized_identity_and_ignores_presentation() {
    let result = execute_dql(
        &dql_fixture_conn(),
        r#"TABLE WITHOUT ID link("Target", "One", true) = link("Target.md", "Two", false) AS "Presentation", link("Target#Hello,   world!", "One", true) = link("Target.md#Hello world", "Two", false) AS "Normalized", link("Target#Hello world") = link("Target#^Hello world") AS "Type", link("Target#Hello world") = link("Target#Other") AS "Subpath"
WHERE file.path = "123.md"
"#,
        None,
    );

    assert_eq!(first_value(&result, 0), &Value::Bool(true));
    assert_eq!(first_value(&result, 1), &Value::Bool(true));
    assert_eq!(first_value(&result, 2), &Value::Bool(false));
    assert_eq!(first_value(&result, 3), &Value::Bool(false));
}

#[test]
fn dql_three_argument_link_requires_string_string_boolean_at_runtime() {
    let conn = dql_fixture_conn();
    let control = execute_dql(
        &conn,
        "TABLE WITHOUT ID link(\"Target\", \"Shown\", true) AS \"Link\"\nWHERE file.path = \"123.md\"\n",
        None,
    );
    assert!(matches!(
        first_value(&control, 0),
        Value::Link(link)
            if link.target == "Target.md"
                && link.display.as_deref() == Some("Shown")
                && link.embed
    ));

    for (expression, expected_error, context) in [
        (
            r#"link(missing, "Shown", true)"#,
            "three-argument link path requires text",
            "null three-argument link path",
        ),
        (
            r#"link(link("Target"), "Shown", true)"#,
            "three-argument link path requires text",
            "link-valued three-argument link path",
        ),
        (
            r#"link("Target", missing, true)"#,
            "three-argument link display requires text",
            "null three-argument link display",
        ),
        (
            r#"link("Target", "Shown", missing)"#,
            "three-argument link embed requires a boolean",
            "null three-argument link embed",
        ),
    ] {
        let source =
            format!("TABLE WITHOUT ID {expression} AS \"Link\"\nWHERE file.path = \"123.md\"\n");
        let result = execute_dql(&conn, &source, None);
        assert_fail_loud(&result, expected_error, context);
    }
}

#[test]
fn dql_task_priority_remains_page_metadata_not_task_priority() {
    let result = execute_dql(&dql_fixture_conn(), "TASK\nWHERE priority = 99\n", None);
    assert_eq!(
        result
            .rows
            .iter()
            .map(|row| row.task_ordinal)
            .collect::<Vec<_>>(),
        [Some(0), Some(1), Some(2)]
    );
}

#[test]
fn dql_task_lambda_locals_do_not_collide_with_task_field_names() {
    for parameter in [
        "text",
        "status",
        "completed",
        "checked",
        "due",
        "scheduled",
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
        let source = format!("TASK\nWHERE length(map([1], ({parameter}) => {parameter})) = 1\n");
        let (query, warnings) = parse_dql(&source);

        assert_eq!(warnings, [], "lambda parameter {parameter:?} collided");
        assert!(!filter_has_any_unsupported(
            query.filters.as_ref().expect("lambda filter")
        ));
    }
}

#[test]
fn dql_wikilink_detection_does_not_capture_nested_lists_strings_or_regexes() {
    let source = r#"TABLE WITHOUT ID [ [1] ] AS "Nested", "[[Target]]" AS "String", /\[\[Target\]\]/ AS "Regex"
"#;
    let (query, warnings) = parse_dql(source);

    assert!(
        warnings
            .iter()
            .all(|warning| warning.message != "DQL wikilink expression literals are unsupported")
    );
    assert!(matches!(
        query.formulas[0].1.kind,
        ExprKind::Lit(Lit::List(ref outer))
            if matches!(outer[0].kind, ExprKind::Lit(Lit::List(_)))
    ));
    assert!(matches!(
        query.formulas[1].1.kind,
        ExprKind::Lit(Lit::String(ref value)) if value == "[[Target]]"
    ));
    assert!(matches!(
        query.formulas[2].1.kind,
        ExprKind::Lit(Lit::Regex { .. })
    ));
}

#[test]
fn dql_compact_nested_lists_are_not_wikilinks() {
    let source = "TABLE WITHOUT ID flat([[file.name], [file.path]], 1) AS \"Flat\"\nWHERE file.path = \"123.md\"\n";
    let (query, warnings) = parse_dql(source);
    let conn = dql_fixture_conn();
    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute compact nested-list DQL");

    assert_eq!(warnings, []);
    assert_eq!(
        first_value(&result, 0),
        &Value::List(vec![
            Value::Text("123".to_string()),
            Value::Text("123.md".to_string()),
        ])
    );
}

#[test]
fn dql_double_brackets_follow_dataview_link_precedence() {
    for source in [
        "TABLE WITHOUT ID [[123]] AS \"Link\"\n",
        "TABLE WITHOUT ID flat([[file.name]], 1) AS \"Link\"\n",
        "TABLE WITHOUT ID flat([[[1]], [[2]]], 2) AS \"Links\"\n",
    ] {
        let (query, warnings) = parse_dql(source);

        assert!(warnings.iter().any(|warning| {
            warning.kind == DqlWarningKind::UnsupportedConstruct
                && warning.message == "DQL wikilink expression literals are unsupported"
        }));
        assert!(matches!(
            query.formulas[0].1.kind,
            ExprKind::Unsupported { ref reason, .. }
                if reason == "DQL wikilink expression literals are unsupported"
        ));
    }
}

#[test]
fn dql_table_without_id_warning_span_starts_at_expression() {
    let source = "TABLE WITHOUT ID [[123]] AS \"Link\"\n";
    let (_, warnings) = parse_dql(source);
    let warning = warnings
        .iter()
        .find(|warning| warning.message == "DQL wikilink expression literals are unsupported")
        .expect("wikilink conversion warning");

    assert_eq!(warning.span.start, source.find("[[123]]").unwrap() as u32);
    assert_eq!(warning.span.end, source.find("[[123]]").unwrap() as u32 + 7);
}

#[test]
fn dql_table_without_id_duplicate_id_token_uses_trailing_expression() {
    let (query, warnings) = parse_dql("TABLE WITHOUT ID ID\n");

    assert_eq!(warnings, []);
    assert!(matches!(
        query.formulas[0].1.kind,
        ExprKind::Index { ref index, .. }
            if matches!(index.kind, ExprKind::Lit(Lit::String(ref name)) if name == "ID")
    ));
}

#[test]
fn dql_list_without_id_duplicate_id_token_uses_trailing_expression_span() {
    let source = "LIST WITHOUT ID ID\n";
    let (query, warnings) = parse_dql(source);

    assert_eq!(warnings, []);
    assert_eq!(
        query.formulas[0].1.span.start as usize,
        source.rfind("ID").unwrap()
    );
    assert_eq!(
        query.formulas[0].1.span.end as usize,
        source.rfind("ID").unwrap() + 2
    );
}

#[test]
fn dql_expression_spans_track_trimmed_where_from_sort_and_later_columns() {
    let where_source = "TABLE WITHOUT ID file.name\nWHERE   file.day\n";
    let (_, warnings) = parse_dql(where_source);
    let warning = warnings
        .iter()
        .find(|warning| warning.message == "unsupported DQL field file.day")
        .expect("WHERE field warning");
    let start = where_source.find("file.day").unwrap();
    assert_eq!(
        (warning.span.start as usize, warning.span.end as usize),
        (start, start + 8)
    );

    let from_source = "TABLE WITHOUT ID file.name\nFROM   madeup()\n";
    let (_, warnings) = parse_dql(from_source);
    let warning = warnings
        .iter()
        .find(|warning| warning.message.starts_with("invalid FROM source"))
        .expect("FROM warning");
    let start = from_source.find("madeup()").unwrap();
    assert_eq!(
        (warning.span.start as usize, warning.span.end as usize),
        (start, start + 8)
    );

    let column_source =
        "TABLE WITHOUT ID file.name AS \"Ok\", file.day AS \"Bad\", file.day AS \"Again\"\n";
    let (_, warnings) = parse_dql(column_source);
    let starts = warnings
        .iter()
        .filter(|warning| warning.message == "unsupported DQL field file.day")
        .map(|warning| warning.span.start as usize)
        .collect::<Vec<_>>();
    assert_eq!(
        starts,
        column_source
            .match_indices("file.day")
            .map(|(idx, _)| idx)
            .collect::<Vec<_>>()
    );

    let sort_source = "TABLE WITHOUT ID file.name\nSORT SORT, file.day, file.day\n";
    let (query, warnings) = parse_dql(sort_source);
    assert_eq!(
        query.sort[0].expr.span.start as usize,
        sort_source.rfind("SORT,").unwrap()
    );
    let starts = warnings
        .iter()
        .filter(|warning| warning.message == "unsupported DQL field file.day")
        .map(|warning| warning.span.start as usize)
        .collect::<Vec<_>>();
    assert_eq!(
        starts,
        sort_source
            .match_indices("file.day")
            .map(|(idx, _)| idx)
            .collect::<Vec<_>>()
    );
}

#[test]
fn dql_supported_scalar_functions_vectorize_over_lists() {
    let source = r#"TABLE WITHOUT ID lower(["A", "B"]) AS "Lower", round([1.2, 2.7]) AS "Round", regexmatch("a+", ["aa", "b"]) AS "Regex"
WHERE file.path = "123.md"
"#;
    let (query, warnings) = parse_dql(source);
    let conn = dql_fixture_conn();
    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute vectorized DQL functions");

    assert_eq!(warnings, []);
    assert_eq!(
        first_value(&result, 0),
        &Value::List(vec![
            Value::Text("a".to_string()),
            Value::Text("b".to_string()),
        ])
    );
    assert_eq!(
        first_value(&result, 1),
        &Value::List(vec![Value::Number(1.0), Value::Number(3.0)])
    );
    assert_eq!(
        first_value(&result, 2),
        &Value::List(vec![Value::Bool(true), Value::Bool(false)])
    );
}

#[test]
fn dql_vectorized_direct_map_matrix_uses_shortest_list() {
    let source = r#"TABLE WITHOUT ID number(["1", "2"]) AS "Number", link(["Target.md", "Hub.md"]) AS "Link", contains("slate", ["lat", "zzz"]) AS "Contains", startswith(["slate", "base"], ["sl", "x", "ignored"]) AS "Starts", replace(["aba", "aca"], "a", ["x", "y"]) AS "Replace", floor([1.9, -1.1]) AS "Floor", ceil([1.1, -1.9]) AS "Ceil", trunc([1.9, -1.9]) AS "Trunc", regextest("a+", ["aa", "b"]) AS "Test", regexreplace(["a1", "b2"], "\\d", ["x", "y"]) AS "Regex replace", striptime([date(2026-07-10), date(2026-07-11)]) AS "Strip", choice([true, false], "yes", "no") AS "Choice", default(["", "x"], ["a", "b", "ignored"]) AS "Default", substring(["abcd", "wxyz"], [1, 2], 3) AS "Substring", date(["2026-07-10", "2026-07-11"]) AS "Date", dur(["1 day", "2 days"]) AS "Duration", link(["Target.md", "Hub.md"], ["Target", "Hub"]) AS "Display link", endswith(["slate", "base"], ["ate", "x"]) AS "Ends", join(["a", "b"], [",", "|"]) AS "Join", regextest(["a+", "b+"], ["aa", "x"]) AS "Pattern test"
WHERE file.path = "123.md"
"#;
    let (query, warnings) = parse_dql(source);
    let conn = dql_fixture_conn();
    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute vectorized DQL direct-map matrix");

    assert_eq!(warnings, []);
    assert_eq!(result.error, None);
    assert_eq!(
        first_value(&result, 0),
        &Value::List(vec![Value::Number(1.0), Value::Number(2.0)])
    );
    assert!(
        matches!(first_value(&result, 1), Value::List(values) if values.len() == 2 && values.iter().all(|value| matches!(value, Value::Link(_))))
    );
    assert_eq!(
        first_value(&result, 2),
        &Value::List(vec![Value::Bool(true), Value::Bool(false)])
    );
    assert_eq!(
        first_value(&result, 3),
        &Value::List(vec![Value::Bool(true), Value::Bool(false)])
    );
    assert_eq!(
        first_value(&result, 4),
        &Value::List(vec![Value::Text("xbx".into()), Value::Text("ycy".into())])
    );
    assert_eq!(
        first_value(&result, 5),
        &Value::List(vec![Value::Number(1.0), Value::Number(-2.0)])
    );
    assert_eq!(
        first_value(&result, 6),
        &Value::List(vec![Value::Number(2.0), Value::Number(-1.0)])
    );
    assert_eq!(
        first_value(&result, 7),
        &Value::List(vec![Value::Number(1.0), Value::Number(-1.0)])
    );
    assert_eq!(
        first_value(&result, 8),
        &Value::List(vec![Value::Bool(true), Value::Bool(false)])
    );
    assert_eq!(
        first_value(&result, 9),
        &Value::List(vec![Value::Text("ax".into()), Value::Text("by".into())])
    );
    assert!(
        matches!(first_value(&result, 10), Value::List(values) if values.len() == 2 && values.iter().all(|value| matches!(value, Value::DqlDate(date) if !date.has_time)))
    );
    assert_eq!(
        first_value(&result, 11),
        &Value::List(vec![Value::Text("yes".into()), Value::Text("no".into())])
    );
    assert_eq!(
        first_value(&result, 12),
        &Value::List(vec![Value::Text("".into()), Value::Text("x".into())])
    );
    assert_eq!(
        first_value(&result, 13),
        &Value::List(vec![Value::Text("bc".into()), Value::Text("y".into())])
    );
    assert!(matches!(
        first_value(&result, 14),
        Value::List(values)
            if matches!(
                values.as_slice(),
                [Value::DqlDate(first), Value::DqlDate(second)]
                    if !first.has_time
                        && !second.has_time
                        && first.is_local
                        && second.is_local
                        && second.epoch_ms - first.epoch_ms == 86_400_000
            )
    ));
    assert_eq!(
        first_value(&result, 15),
        &Value::List(vec![
            Value::DqlDuration(DqlDurationValue {
                days: 1.0,
                ..DqlDurationValue::default()
            }),
            Value::DqlDuration(DqlDurationValue {
                days: 2.0,
                ..DqlDurationValue::default()
            })
        ])
    );
    assert!(
        matches!(first_value(&result, 16), Value::List(values) if matches!(&values[..], [Value::Link(first), Value::Link(second)] if first.display.as_deref() == Some("Target") && second.display.as_deref() == Some("Hub")))
    );
    assert_eq!(
        first_value(&result, 17),
        &Value::List(vec![Value::Bool(true), Value::Bool(false)])
    );
    assert_eq!(
        first_value(&result, 18),
        &Value::List(vec![Value::Text("a,b".into()), Value::Text("a|b".into())])
    );
    assert_eq!(
        first_value(&result, 19),
        &Value::List(vec![Value::Bool(true), Value::Bool(false)])
    );
}

#[test]
fn dql_nested_lambdas_fail_loud_instead_of_rebinding_outer_value() {
    let source = "TABLE WITHOUT ID map([1], (x) => map([2], (y) => x + y)) AS \"Nested\"\n";
    let (query, warnings) = parse_dql(source);

    assert!(warnings.iter().any(|warning| {
        warning.kind == DqlWarningKind::UnsupportedConstruct
            && warning.message == "nested DQL lambdas are unsupported"
    }));
    assert!(matches!(
        query.formulas[0].1.kind,
        ExprKind::Unsupported { ref reason, .. } if reason == "nested DQL lambdas are unsupported"
    ));
}

#[test]
fn dql_generated_vectorization_inside_lambda_fails_loud_instead_of_capturing_outer_value() {
    for source in [
        "TABLE WITHOUT ID map([\"1\"], (x) => contains(x, [\"1\", \"2\"])) AS \"Value\"\n",
        "TABLE WITHOUT ID map([\"ab\"], (x) => startswith(x, [\"a\", \"z\"])) AS \"Value\"\n",
    ] {
        let (query, warnings) = parse_dql(source);

        assert!(warnings.iter().any(|warning| {
            warning.message == "vectorized DQL functions inside lambdas are unsupported"
        }));
        assert!(matches!(
            query.formulas[0].1.kind,
            ExprKind::Unsupported { .. }
        ));
    }
}

#[test]
fn dql_lambda_substitution_preserves_member_names_and_object_keys() {
    let source = r#"TABLE WITHOUT ID map([{x: 1}], (x) => x.x) AS "Member", filter([{x: 1}], (x) => x.x = 1) AS "Filter", map([1], (x) => {x: x}) AS "Object", map([{name: "a"}], (name) => name.name) AS "Named"
WHERE file.path = "123.md"
"#;
    let (query, warnings) = parse_dql(source);
    let conn = dql_fixture_conn();
    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute scope-aware DQL lambda substitution");

    assert_eq!(warnings, []);
    assert_eq!(
        first_value(&result, 0),
        &Value::List(vec![Value::Number(1.0)])
    );
    assert!(
        matches!(first_value(&result, 1), Value::List(values) if values.len() == 1 && matches!(&values[0], Value::Object(map) if map.get("x") == Some(&Value::Number(1.0))))
    );
    assert!(
        matches!(first_value(&result, 2), Value::List(values) if matches!(&values[..], [Value::Object(map)] if map.get("x") == Some(&Value::Number(1.0)) && !map.contains_key("value")))
    );
    assert_eq!(
        first_value(&result, 3),
        &Value::List(vec![Value::Text("a".into())])
    );
}

#[test]
fn dql_authored_value_and_index_properties_do_not_collide_with_list_bindings() {
    let source = r#"TABLE WITHOUT ID contains(value, ["x", "z"]) AS "Contains", choice([true, false], value, "n") AS "Choice", map([1, 2], (x) => x + index) AS "Index", map(["a", "b"], (x) => x + value) AS "Value"
WHERE file.path = "123.md"
"#;
    let (query, warnings) = parse_dql(source);
    let result = execute(
        &query,
        &dql_fixture_conn(),
        &EngineCtx::default(),
        &CancelToken::new(),
    )
    .expect("execute DQL authored value/index properties");

    assert_eq!(warnings, []);
    assert_eq!(
        first_value(&result, 0),
        &Value::List(vec![Value::Bool(true), Value::Bool(false)])
    );
    assert_eq!(
        first_value(&result, 1),
        &Value::List(vec![Value::Text("x".into()), Value::Text("n".into())])
    );
    assert_eq!(
        first_value(&result, 2),
        &Value::List(vec![Value::Number(11.0), Value::Number(12.0)])
    );
    assert_eq!(
        first_value(&result, 3),
        &Value::List(vec![Value::Text("ax".into()), Value::Text("bx".into())])
    );
}

#[test]
fn dql_task_object_keys_are_not_rewritten_as_implicit_fields() {
    let source = "TASK\nWHERE {status: 1, line: 2}[\"status\"] = 1\n";
    let (query, warnings) = parse_dql(source);
    let conn = dql_fixture_conn();
    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute TASK object literal query");

    assert_eq!(warnings, []);
    assert_eq!(result.error, None);
    assert_eq!(result.rows.len(), 5);
}

#[test]
fn dql_bases_only_file_fields_fail_loud() {
    for prefix in ["file", "this.file"] {
        for field in [
            "basename",
            "properties",
            "links",
            "backlinks",
            "embeds",
            "file",
            "inDegree",
            "outDegree",
        ] {
            let dql_field = format!("{prefix}.{field}");
            let source = format!("TABLE WITHOUT ID {dql_field} AS \"Value\"\n");
            let (query, warnings) = parse_dql(&source);
            let reason = format!("unsupported DQL field {dql_field}");

            assert!(warnings.iter().any(|warning| {
                warning.kind == DqlWarningKind::UnsupportedConstruct && warning.message == reason
            }));
            assert!(matches!(
                query.formulas[0].1.kind,
                ExprKind::Unsupported { reason: ref got, .. } if got == &reason
            ));
        }
    }
}

#[test]
fn dql_unknown_functions_fail_loud_instead_of_falling_through_to_bases() {
    for function in [
        "if",
        "now",
        "today",
        "duration",
        "file",
        "escapehtml",
        "html",
        "icon",
        "image",
        "random",
        "madeup",
    ] {
        let source = format!("TABLE WITHOUT ID {function}(\"x\") AS \"Value\"\n");
        let (query, warnings) = parse_dql(&source);
        let reason = format!("unsupported DQL function {function}");

        assert!(warnings.iter().any(|warning| {
            warning.kind == DqlWarningKind::UnsupportedConstruct && warning.message == reason
        }));
        assert!(matches!(
            query.formulas[0].1.kind,
            ExprKind::Unsupported { reason: ref got, .. } if got == &reason
        ));
    }
}

#[test]
fn dql_authored_method_calls_fail_loud_but_typeof_rewrite_still_works() {
    let (query, warnings) = parse_dql(
        "TABLE WITHOUT ID file.name.title() AS \"Method\", typeof(file.name) = \"string\" AS \"Type\"\n",
    );

    assert!(warnings.iter().any(|warning| {
        warning.kind == DqlWarningKind::UnsupportedConstruct
            && warning.message == "DQL method-call syntax is unsupported"
    }));
    assert!(matches!(
        query.formulas[0].1.kind,
        ExprKind::Unsupported { ref reason, .. }
            if reason == "DQL method-call syntax is unsupported"
    ));
    assert!(!matches!(
        query.formulas[1].1.kind,
        ExprKind::Unsupported { .. }
    ));
}

#[test]
fn dql_javascript_only_regex_syntax_fails_during_conversion() {
    for source in [
        "TABLE WITHOUT ID regextest(\"(?=b)\", \"ab\") AS \"Regex\"\n",
        "TABLE WITHOUT ID regexreplace(\"ab\", \"(?=b)\", \"x\") AS \"Regex\"\n",
        "TABLE WITHOUT ID split(\"ab\", \"(?=b)\") AS \"Regex\"\n",
    ] {
        let (query, warnings) = parse_dql(source);

        assert!(warnings.iter().any(|warning| {
            warning.kind == DqlWarningKind::UnsupportedConstruct
                && warning
                    .message
                    .contains("regex uses unsupported JavaScript syntax")
        }));
        assert!(matches!(
            query.formulas[0].1.kind,
            ExprKind::Unsupported { .. }
        ));
    }
}

#[test]
fn dql_regex_uses_javascript_ascii_character_classes_and_rejects_rust_only_syntax() {
    let source = r#"TABLE WITHOUT ID regextest("\w", "é") AS "Word", regexmatch("\w", "é") AS "Match", regexreplace("é", "\w", "x") AS "Replace", split("é", "\w") AS "Split", regextest("\d", "١") AS "Digit"
"#;
    let result = execute_dql(&dql_fixture_conn(), source, None);
    assert_eq!(first_value(&result, 0), &Value::Bool(false));
    assert_eq!(first_value(&result, 1), &Value::Bool(false));
    assert_eq!(first_value(&result, 2), &Value::Text("é".into()));
    assert_eq!(
        first_value(&result, 3),
        &Value::List(vec![Value::Text("é".into())])
    );
    assert_eq!(first_value(&result, 4), &Value::Bool(false));

    for pattern in [r".", r"\Afoo", r"(?P<x>a)", r"(?i)a", r"[a&&b]"] {
        let source = format!(
            "TABLE WITHOUT ID regextest({}, \"foo\") AS \"Regex\"\n",
            serde_json::to_string(pattern).unwrap()
        );
        let (query, warnings) = parse_dql(&source);
        assert!(
            warnings.iter().any(|warning| {
                warning.kind == DqlWarningKind::UnsupportedConstruct
                    && warning.message.contains("JavaScript-compatible subset")
            }),
            "pattern {pattern:?}"
        );
        assert!(matches!(
            query.formulas[0].1.kind,
            ExprKind::Unsupported { .. }
        ));
    }
}

#[test]
fn dql_dynamic_rust_only_and_unrepresentable_utf16_regex_cases_fail_loud() {
    for source in [
        r#"TABLE WITHOUT ID regextest("\A" + "foo", "foo") AS "Regex"
"#,
        r#"TABLE WITHOUT ID regextest("(?i)" + "a", "A") AS "Regex"
"#,
        r#"TABLE WITHOUT ID regexreplace("😀", "", "-") AS "Regex"
"#,
        r#"TABLE WITHOUT ID regexreplace("😀", "a*", "-") AS "Regex"
"#,
        r#"TABLE WITHOUT ID split("😀", "a*") AS "Regex"
"#,
    ] {
        let (query, warnings) = parse_dql(source);
        let result = execute(
            &query,
            &dql_fixture_conn(),
            &EngineCtx::default(),
            &CancelToken::new(),
        )
        .expect("execute guarded DQL regex");
        assert_eq!(warnings, []);
        assert!(matches!(result.rows[0].cells[0], CellValue::Error(_)));
    }
}

#[test]
fn dql_unmapped_date_format_and_ldefault_overloads_fail_loud() {
    for (source, reason) in [
        (
            "TABLE WITHOUT ID date(\"07/10/2026\", \"MM/dd/yyyy\") AS \"Date\"\n",
            "DQL date(text, luxonFormat) is unsupported",
        ),
        (
            "TABLE WITHOUT ID ldefault(\"\", \"fallback\") AS \"Default\"\n",
            "unsupported DQL function ldefault",
        ),
    ] {
        let (query, warnings) = parse_dql(source);

        assert!(warnings.iter().any(|warning| {
            warning.kind == DqlWarningKind::UnsupportedConstruct && warning.message == reason
        }));
        assert!(matches!(
            query.formulas[0].1.kind,
            ExprKind::Unsupported { reason: ref got, .. } if got == reason
        ));
    }
}

#[test]
fn dql_aggregates_reject_variadic_non_list_shape() {
    for function in ["sum", "average", "min", "max"] {
        for arguments in ["1", "1, 2"] {
            let source = format!("TABLE WITHOUT ID {function}({arguments}) AS \"Value\"\n");
            let (query, warnings) = parse_dql(&source);
            let reason = format!("DQL {function} requires exactly one list-shaped argument");

            assert!(warnings.iter().any(|warning| {
                warning.kind == DqlWarningKind::UnsupportedConstruct && warning.message == reason
            }));
            assert!(matches!(
                query.formulas[0].1.kind,
                ExprKind::Unsupported { reason: ref got, .. } if got == &reason
            ));
        }
    }
}

#[test]
fn dql_aggregates_execute_list_and_empty_list_shapes() {
    let source = r#"TABLE WITHOUT ID sum([1, 2]) AS "Sum", average([1, 2]) AS "Average", min([1, 2]) AS "Min", max([1, 2]) AS "Max", sum([]) AS "Empty sum", average([]) AS "Empty average", min([]) AS "Empty min", max([]) AS "Empty max"
"#;
    let (query, warnings) = parse_dql(source);
    let conn = dql_fixture_conn();
    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute converted aggregate query");

    assert_eq!(warnings, []);
    assert_eq!(first_value(&result, 0), &Value::Number(3.0));
    assert_eq!(first_value(&result, 1), &Value::Number(1.5));
    assert_eq!(first_value(&result, 2), &Value::Number(1.0));
    assert_eq!(first_value(&result, 3), &Value::Number(2.0));
    for column in 4..8 {
        assert_eq!(first_value(&result, column), &Value::Null);
    }
    assert_eq!(result.error, None);
}

#[test]
fn dql_aggregate_accepts_list_shaped_choice_result() {
    let source = r#"TABLE WITHOUT ID sum(choice(true, [1, 2], [3])) AS "Sum"
WHERE file.path = "123.md"
"#;
    let (query, warnings) = parse_dql(source);
    let conn = dql_fixture_conn();
    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute aggregate over list-shaped choice");

    assert_eq!(warnings, []);
    assert_eq!(first_value(&result, 0), &Value::Number(3.0));
}

#[test]
fn dql_dynamic_aggregates_accept_lists_and_fail_loud_on_scalars() {
    let list_source = r#"TABLE WITHOUT ID sum(scores) AS "Sum", average(scores) AS "Average", min(scores) AS "Min", max(scores) AS "Max"
WHERE file.path = "123.md"
"#;
    let (query, warnings) = parse_dql(list_source);
    let result = execute(
        &query,
        &dql_fixture_conn(),
        &EngineCtx::default(),
        &CancelToken::new(),
    )
    .expect("execute dynamic list aggregates");
    assert_eq!(warnings, []);
    assert_eq!(first_value(&result, 0), &Value::Number(3.0));
    assert_eq!(first_value(&result, 1), &Value::Number(1.5));
    assert_eq!(first_value(&result, 2), &Value::Number(1.0));
    assert_eq!(first_value(&result, 3), &Value::Number(2.0));

    for function in ["sum", "average", "min", "max"] {
        let source = format!(
            "TABLE WITHOUT ID {function}(rating) AS \"Value\"\nWHERE file.path = \"123.md\"\n"
        );
        let (query, warnings) = parse_dql(&source);
        let result = execute(
            &query,
            &dql_fixture_conn(),
            &EngineCtx::default(),
            &CancelToken::new(),
        )
        .expect("execute guarded dynamic scalar aggregate");
        assert_eq!(warnings, []);
        assert!(matches!(
            result.rows[0].cells[0],
            CellValue::Error(ref error) if error.contains("DQL aggregate requires a list")
        ));
    }
}

#[test]
fn dql_min_max_preserve_string_and_date_values() {
    let source = r#"TABLE WITHOUT ID min(["b", "a"]) AS "Min text", max(["b", "a"]) AS "Max text", min([date("2026-02-01"), date("2026-01-01")]) AS "Min date", max([date("2026-02-01"), date("2026-01-01")]) AS "Max date", date("2026-01-01") AS "Expected min", date("2026-02-01") AS "Expected max"
WHERE file.path = "123.md"
"#;
    let (query, warnings) = parse_dql(source);
    let result = execute(
        &query,
        &dql_fixture_conn(),
        &EngineCtx::default(),
        &CancelToken::new(),
    )
    .expect("execute generic DQL min/max");
    assert_eq!(warnings, []);
    assert_eq!(first_value(&result, 0), &Value::Text("a".into()));
    assert_eq!(first_value(&result, 1), &Value::Text("b".into()));
    assert!(
        matches!(first_value(&result, 2), Value::DqlDate(date) if !date.has_time && date.is_local)
    );
    assert!(
        matches!(first_value(&result, 3), Value::DqlDate(date) if !date.has_time && date.is_local)
    );
    assert_eq!(first_value(&result, 2), first_value(&result, 4));
    assert_eq!(first_value(&result, 3), first_value(&result, 5));
}

#[test]
fn dql_sum_preserves_typed_addition_and_average_rejects_mixed_totals() {
    let supported = r#"TABLE WITHOUT ID sum([1, "2"]) AS "Numeric text", sum([1, "x"]) AS "Text"
WHERE file.path = "123.md"
"#;
    let result = execute_dql(&dql_fixture_conn(), supported, None);
    assert_eq!(first_value(&result, 0), &Value::Text("12".into()));
    assert_eq!(first_value(&result, 1), &Value::Text("1x".into()));

    let null_tail = execute_dql(
        &dql_fixture_conn(),
        r#"TABLE WITHOUT ID sum([5, missing]) AS "Sum", average([5, missing]) AS "Average"
WHERE file.path = "123.md"
"#,
        None,
    );
    assert_eq!(first_value(&null_tail, 0), &Value::Number(5.0));
    assert_eq!(first_value(&null_tail, 1), &Value::Number(2.5));

    for source in [
        "TABLE WITHOUT ID average([1, \"2\"]) AS \"Value\"\nWHERE file.path = \"123.md\"\n",
        "TABLE WITHOUT ID sum([missing, 5]) AS \"Value\"\nWHERE file.path = \"123.md\"\n",
    ] {
        let (query, warnings) = parse_dql(source);
        let result = execute(
            &query,
            &dql_fixture_conn(),
            &EngineCtx::default(),
            &CancelToken::new(),
        )
        .expect("execute guarded DQL aggregate");
        assert_eq!(warnings, []);
        assert!(matches!(result.rows[0].cells[0], CellValue::Error(_)));
    }
}

#[test]
fn dql_contains_recurses_through_nested_lists() {
    let source = r#"TABLE WITHOUT ID contains([1], 1) AS "Direct", contains([ [1] ], 1) AS "Nested", contains([ [ [1] ] ], 1) AS "Deep"
WHERE file.path = "123.md"
"#;
    let result = execute_dql(&dql_fixture_conn(), source, None);
    for column in 0..3 {
        assert_eq!(first_value(&result, column), &Value::Bool(true));
    }
}

#[test]
fn dql_sort_unique_and_slice_use_typed_structural_javascript_semantics() {
    let source = r#"TABLE WITHOUT ID sort([2, 10]) AS "Sort", unique([ ["a, b"], ["a", "b"] ]) AS "Unique", slice([1, 2, 3], -1) AS "Tail", slice([1, 2, 3], 0, -1) AS "Drop last", min([2, 10]) AS "Min", max([2, 10]) AS "Max", unique([1, "1", true]) AS "Mixed", unique([{x: 1}, {x: "1"}]) AS "Nested mixed"
WHERE file.path = "123.md"
"#;
    let result = execute_dql(&dql_fixture_conn(), source, None);
    assert_eq!(
        first_value(&result, 0),
        &Value::List(vec![Value::Number(2.0), Value::Number(10.0)])
    );
    assert!(matches!(
        result.rows[0].cells[1],
        CellValue::Error(ref error) if error.contains("locale collation")
    ));
    assert_eq!(
        first_value(&result, 2),
        &Value::List(vec![Value::Number(3.0)])
    );
    assert_eq!(
        first_value(&result, 3),
        &Value::List(vec![Value::Number(1.0), Value::Number(2.0)])
    );
    assert_eq!(first_value(&result, 4), &Value::Number(2.0));
    assert_eq!(first_value(&result, 5), &Value::Number(10.0));
    assert!(matches!(first_value(&result, 6), Value::List(values) if values.len() == 3));
    assert!(matches!(first_value(&result, 7), Value::List(values) if values.len() == 2));
}

#[test]
fn dql_direct_map_edge_semantics_match_dataview() {
    let source = r#"TABLE WITHOUT ID round(-1.5) AS "Round", round(-1.25, 1) AS "Precision", round(123.4, -1) AS "Negative precision", number("abc -12.5 xyz") AS "Embedded", number(".5") AS "Leading dot", number("1e2") AS "Exponent", length("😀") AS "UTF16", length({a: 1, b: 2}) AS "Object length", length(missing) AS "Null length", replace("ab", "", "-") AS "Empty replace", substring("abcd", 3, 1) AS "Swap"
WHERE file.path = "123.md"
"#;
    let (query, warnings) = parse_dql(source);
    let result = execute(
        &query,
        &dql_fixture_conn(),
        &EngineCtx::default(),
        &CancelToken::new(),
    )
    .expect("execute direct-map parity edges");

    assert_eq!(warnings, []);
    for (column, expected) in [
        (0, Value::Number(-1.0)),
        (1, Value::Number(-1.3)),
        (2, Value::Number(123.0)),
        (3, Value::Number(-12.5)),
        (4, Value::Number(5.0)),
        (5, Value::Number(1.0)),
        (6, Value::Number(2.0)),
        (7, Value::Number(2.0)),
        (8, Value::Number(0.0)),
        (9, Value::Text("a-b".into())),
        (10, Value::Text("bc".into())),
    ] {
        assert_eq!(first_value(&result, column), &expected, "column {column}");
    }
}

#[test]
fn dql_string_repeat_dispatches_computed_strings_without_breaking_numeric_multiply() {
    let source = r#"TABLE WITHOUT ID file.name * 2 AS "Field", lower("A") * 3 AS "Computed", 6 * 7 AS "Number", 1 + 2 * 3 AS "Precedence", "a" * 2 + "b" AS "Repeat left", "a" + "b" * 2 AS "Repeat right"
WHERE file.path = "123.md"
"#;
    let result = execute_dql(&dql_fixture_conn(), source, None);
    assert_eq!(first_value(&result, 0), &Value::Text("123123".into()));
    assert_eq!(first_value(&result, 1), &Value::Text("aaa".into()));
    assert_eq!(first_value(&result, 2), &Value::Number(42.0));
    assert_eq!(first_value(&result, 3), &Value::Number(7.0));
    assert_eq!(first_value(&result, 4), &Value::Text("aab".into()));
    assert_eq!(first_value(&result, 5), &Value::Text("abb".into()));
}

#[test]
fn dql_multiply_rewrite_stays_linear_and_nested_vector_expansion_fails_loud() {
    let multiply = (0..256).map(|_| "1").collect::<Vec<_>>().join(" * ");
    let source = format!("TABLE WITHOUT ID {multiply} AS \"Value\"\n");
    let (query, warnings) = parse_dql(&source);
    assert_eq!(warnings, []);
    assert!(
        dql_expr_semantic_signature(&query.formulas[0].1).len() < 100_000,
        "DQL multiply rewrite grew superlinearly"
    );

    let mut nested = "value".to_string();
    for _ in 0..100 {
        nested = format!("lower({nested})");
    }
    let source = format!("TABLE WITHOUT ID {nested} AS \"Value\"\n");
    let (query, warnings) = parse_dql(&source);
    assert!(warnings.iter().any(|warning| {
        warning.message == "DQL expression expansion exceeds Slate's safe conversion limit"
    }));
    assert!(matches!(
        query.formulas[0].1.kind,
        ExprKind::Unsupported { .. }
    ));
}

#[test]
fn dql_list_only_functions_guard_dynamic_scalar_shapes() {
    let source = r#"TABLE WITHOUT ID join(scores, ",") AS "Join", flat(scores) AS "Flat", unique(scores) AS "Unique", slice(scores, 0, 1) AS "Slice", map(scores, (x) => x + 1) AS "Map", filter(scores, (x) => x > 1) AS "Filter", sort(rating) AS "Sort scalar", reverse(rating) AS "Reverse scalar", reverse("abc") AS "Reverse text", join(rating, ",") AS "Bad join", flat(rating) AS "Bad flat", unique(rating) AS "Bad unique", slice(rating, 0, 1) AS "Bad slice", map(rating, (x) => x) AS "Bad map", filter(rating, (x) => true) AS "Bad filter"
WHERE file.path = "123.md"
"#;
    let (query, warnings) = parse_dql(source);
    let result = execute(
        &query,
        &dql_fixture_conn(),
        &EngineCtx::default(),
        &CancelToken::new(),
    )
    .expect("execute guarded DQL list surface");

    assert_eq!(warnings, []);
    assert_eq!(first_value(&result, 0), &Value::Text("1,2".into()));
    assert_eq!(
        first_value(&result, 1),
        &Value::List(vec![Value::Number(1.0), Value::Number(2.0)])
    );
    assert_eq!(first_value(&result, 6), &Value::Number(4.5));
    assert_eq!(first_value(&result, 7), &Value::Number(4.5));
    assert_eq!(first_value(&result, 8), &Value::Text("cba".into()));
    assert_eq!(first_value(&result, 9), &Value::Text("4.5".into()));
    for column in 10..15 {
        assert!(
            matches!(
                result.rows[0].cells[column],
                CellValue::Error(ref error) if error.contains("requires a list")
            ),
            "column {column}: {:?}",
            result.rows[0].cells[column]
        );
    }

    for source in [
        "TABLE WITHOUT ID unique(1) AS \"Value\"\n",
        "TABLE WITHOUT ID slice(1, 0, 1) AS \"Value\"\n",
        "TABLE WITHOUT ID map(1, (x) => x) AS \"Value\"\n",
        "TABLE WITHOUT ID filter(1, (x) => true) AS \"Value\"\n",
        "TABLE WITHOUT ID flat(1) AS \"Value\"\n",
    ] {
        let (query, warnings) = parse_dql(source);
        assert!(
            warnings
                .iter()
                .any(|warning| warning.kind == DqlWarningKind::UnsupportedConstruct)
        );
        assert!(matches!(
            query.formulas[0].1.kind,
            ExprKind::Unsupported { .. }
        ));
    }
}

#[test]
fn dql_reverse_fails_loud_when_utf16_reversal_is_not_representable() {
    let (query, warnings) = parse_dql(
        "TABLE WITHOUT ID reverse(\"😀a\") AS \"Reverse\"\nWHERE file.path = \"123.md\"\n",
    );
    let result = execute(
        &query,
        &dql_fixture_conn(),
        &EngineCtx::default(),
        &CancelToken::new(),
    )
    .expect("execute guarded DQL reverse");
    assert_eq!(warnings, []);
    assert!(matches!(result.rows[0].cells[0], CellValue::Error(_)));
}

#[test]
fn dql_aggregates_classify_this_file_scalar_and_list_fields() {
    let (scalar_query, scalar_warnings) =
        parse_dql("TABLE WITHOUT ID sum(this.file.size) AS \"Sum\"\n");
    assert!(
        scalar_warnings.iter().any(|warning| {
            warning.message == "DQL sum requires exactly one list-shaped argument"
        })
    );
    assert!(matches!(
        scalar_query.formulas[0].1.kind,
        ExprKind::Unsupported { .. }
    ));

    let source = r#"TABLE WITHOUT ID sum(this.file.tags) AS "Sum"
WHERE file.path = "123.md"
"#;
    let (list_query, list_warnings) = parse_dql(source);
    assert_eq!(list_warnings, []);
    assert!(!matches!(
        list_query.formulas[0].1.kind,
        ExprKind::Unsupported { .. }
    ));
}

#[test]
fn dql_list_and_array_constructors_are_variadic_and_preserve_nested_lists() {
    let source = r#"TABLE WITHOUT ID list(1, 2) AS "List", array("a", "b") AS "Array", list([1, 2]) AS "Nested"
WHERE file.path = "123.md"
"#;
    let (query, warnings) = parse_dql(source);
    let conn = dql_fixture_conn();
    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute variadic DQL list constructors");

    assert_eq!(warnings, []);
    assert_eq!(
        first_value(&result, 0),
        &Value::List(vec![Value::Number(1.0), Value::Number(2.0)])
    );
    assert_eq!(
        first_value(&result, 1),
        &Value::List(vec![Value::Text("a".into()), Value::Text("b".into())])
    );
    assert_eq!(
        first_value(&result, 2),
        &Value::List(vec![Value::List(vec![
            Value::Number(1.0),
            Value::Number(2.0),
        ])])
    );
}

#[test]
fn dql_split_empty_text_by_empty_regex_returns_empty_list() {
    let source = r#"TABLE WITHOUT ID split("", "") AS "Parts"
WHERE file.path = "123.md"
"#;
    let (query, warnings) = parse_dql(source);
    let conn = dql_fixture_conn();
    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute empty regex split");

    assert_eq!(warnings, []);
    assert_eq!(first_value(&result, 0), &Value::List(Vec::new()));
}

#[test]
fn dql_split_list_input_fails_loud_because_dataview_does_not_vectorize_split() {
    let source = "TABLE WITHOUT ID split([\"a-b\", \"c-d\"], \"-\") AS \"Parts\"\n";
    let (query, warnings) = parse_dql(source);

    assert!(warnings.iter().any(|warning| {
        warning.kind == DqlWarningKind::UnsupportedConstruct
            && warning.message == "DQL split expects scalar text"
    }));
    assert!(matches!(
        query.formulas[0].1.kind,
        ExprKind::Unsupported { ref reason, .. } if reason == "DQL split expects scalar text"
    ));
}

#[test]
fn dql_user_marker_shaped_string_is_not_promoted_to_regex() {
    let old_marker = format!("{}slate-dql-regex:^foo{}", '\u{f8ff}', '\u{f8fe}');
    let source = format!(
        "TABLE WITHOUT ID \"{old_marker}\" AS \"Authored\", regextest(\"^foo\", \"foobar\") AS \"Synthesized\"\n"
    );
    let (query, warnings) = parse_dql(&source);

    assert_eq!(warnings, []);
    assert!(matches!(
        &query.formulas[0].1.kind,
        ExprKind::Lit(Lit::String(value)) if value == &old_marker
    ));
    assert_eq!(regex_pattern(&query.formulas[1].1), "^foo");

    let conn = dql_fixture_conn();
    let result = execute_dql(&conn, &source, None);
    assert_eq!(first_value(&result, 0), &Value::Text(old_marker));
    assert_eq!(first_value(&result, 1), &Value::Bool(true));
    assert_eq!(result.error, None);
}

#[test]
fn dql_regex_literals_preserve_escapes_in_ast_and_execution() {
    let source = r#"TABLE WITHOUT ID regextest("\d+", "123") AS "Digits", regextest("a/b", "a/b") AS "Slash", regextest("a\"b", "a\"b") AS "Quote", regextest("\\\\", "\\") AS "Backslash", regextest("\n", "\n") AS "Newline", regextest("\r", "\r") AS "Carriage return", regextest("\t", "\t") AS "Tab"
"#;
    let (query, warnings) = parse_dql(source);

    assert_eq!(warnings, []);
    assert_eq!(
        query
            .formulas
            .iter()
            .map(|(_, expr)| regex_pattern(expr))
            .collect::<Vec<_>>(),
        [r"[0-9]+", "a/b", "a\"b", r"\\", "\n", "\r", "\t"]
    );

    let conn = dql_fixture_conn();
    let values = execute_dql(&conn, source, None);
    for column in 0..query.formulas.len() {
        assert_eq!(first_value(&values, column), &Value::Bool(true));
    }

    let membership = execute_dql(
        &conn,
        r#"LIST WITHOUT ID file.path
WHERE regextest("\d+", file.name)
"#,
        None,
    );
    assert_eq!(row_paths(&membership), ["123.md"]);
    assert_eq!(membership.error, None);
}

#[test]
fn dql_regex_functions_accept_expression_valued_patterns() {
    let source = r#"TABLE WITHOUT ID regextest(choice(true, "\d+", "x"), "123") AS "Test", regexmatch(choice(true, "\d+", "x"), "123") AS "Match", regexreplace("a1", choice(true, "\d+", "x"), "x") AS "Replace", split("a1b", choice(true, "\d+", "x")) AS "Split"
WHERE file.path = "123.md"
"#;
    let (query, warnings) = parse_dql(source);
    let conn = dql_fixture_conn();
    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute dynamic DQL regex patterns");

    assert_eq!(warnings, []);
    assert_eq!(first_value(&result, 0), &Value::Bool(true));
    assert_eq!(first_value(&result, 1), &Value::Bool(true));
    assert_eq!(first_value(&result, 2), &Value::Text("ax".to_string()));
    assert_eq!(
        first_value(&result, 3),
        &Value::List(vec![Value::Text("a".into()), Value::Text("b".into())])
    );
    assert_eq!(result.error, None);
}

#[test]
fn dql_negative_truncates_toward_zero() {
    let conn = dql_fixture_conn();
    let result = execute_dql(&conn, FUNCTIONS_DQL, None);

    assert_eq!(first_value(&result, 1), &Value::Number(-1.0));
    assert_eq!(result.error, None);
}

#[test]
fn unsafe_pipeline_orders_and_rows_commands_fail_loud() {
    let (ordered, ordered_warnings) = parse_dql(
        r#"TABLE file.name
LIMIT 5
SORT file.name
"#,
    );
    let (grouped, grouped_warnings) = parse_dql("TABLE file.name\nGROUP BY status\n");
    let (_flattened, flattened_warnings) = parse_dql("TABLE file.name\nFLATTEN tags\n");

    assert!(ordered_warnings.iter().any(|warning| {
        warning.kind == DqlWarningKind::UnsupportedConstruct
            && warning.message.contains("order-dependent")
    }));
    assert!(filter_has_unsupported_reason(
        ordered.filters.as_ref().expect("ordered filters"),
        "order-dependent commands"
    ));
    assert!(
        grouped_warnings
            .iter()
            .any(|warning| warning.message.contains("rows aggregation"))
    );
    assert!(filter_has_unsupported_reason(
        grouped.filters.as_ref().expect("grouped filters"),
        "rows aggregation"
    ));
    assert!(
        flattened_warnings
            .iter()
            .any(|warning| warning.message.contains("FLATTEN"))
    );
}

#[test]
fn invalid_limit_and_parse_expr_unsupported_nodes_warn_and_fail_loud() {
    let (invalid_limit, limit_warnings) = parse_dql("TABLE file.name\nLIMIT nope\n");
    let (unknown_field, field_warnings) = parse_dql("TABLE file.magic\n");

    assert!(limit_warnings.iter().any(|warning| {
        warning.kind == DqlWarningKind::InvalidCommand && warning.message.contains("LIMIT")
    }));
    assert!(filter_has_unsupported_reason(
        invalid_limit
            .filters
            .as_ref()
            .expect("invalid limit unsupported filter"),
        "invalid LIMIT"
    ));
    assert!(field_warnings.iter().any(|warning| {
        warning.kind == DqlWarningKind::UnsupportedConstruct
            && warning.message.contains("unknown file field magic")
    }));
    assert!(matches!(
        unknown_field.formulas[0].1.kind,
        ExprKind::Unsupported { .. }
    ));
}

#[test]
fn unsupported_fields_and_functions_become_unsupported_expressions() {
    let (query, warnings) = parse_dql(
        r#"TABLE file.etags AS "Explicit tags"
WHERE upper(file.name) = "X"
"#,
    );

    assert!(warnings.iter().any(|warning| {
        warning.kind == DqlWarningKind::UnsupportedConstruct
            && warning.message.contains("file.etags")
    }));
    assert!(warnings.iter().any(|warning| {
        warning.kind == DqlWarningKind::UnsupportedConstruct && warning.message.contains("upper")
    }));
    assert!(matches!(
        query.formulas[0].1.kind,
        ExprKind::Unsupported { .. }
    ));
    assert!(filter_has_unsupported_reason(
        query.filters.as_ref().expect("unsupported where"),
        "unsupported DQL function upper"
    ));
}

#[test]
fn supported_field_and_lambda_rewrites_compile_to_supported_exprs() {
    let (query, warnings) = parse_dql(
        r#"TABLE file.cday AS "Created day", file.link AS "Link", map(file.tags, (t) => lower(t)) AS "Tags"
WHERE file.mday = date(2026-07-08)
"#,
    );

    assert_eq!(warnings, []);
    assert_eq!(query.formulas.len(), 3);
    assert!(
        query
            .formulas
            .iter()
            .all(|(_, expr)| !matches!(expr.kind, ExprKind::Unsupported { .. }))
    );
    assert!(!filter_has_any_unsupported(
        query.filters.as_ref().expect("date filter")
    ));
}

#[test]
fn date_null_repeat_aliases_and_typeof_follow_dql_mapping_rules() {
    let (dates, date_warnings) = parse_dql(
        r#"TABLE file.aliases AS "Aliases", "x" * 3 AS "Repeat"
WHERE typeof(due) = "date" AND due <= date(tomorrow)
"#,
    );
    let (periods, period_warnings) = parse_dql(
        r#"TABLE file.name
WHERE file.mtime >= date(sow) AND file.mtime <= date(eoy)
"#,
    );
    let (null_query, null_warnings) = parse_dql("TABLE file.name\nWHERE null <= date(today)\n");

    assert_eq!(date_warnings, []);
    assert_eq!(dates.formulas.len(), 2);
    assert!(
        dates
            .formulas
            .iter()
            .all(|(_, expr)| !matches!(expr.kind, ExprKind::Unsupported { .. }))
    );
    assert!(!filter_has_any_unsupported(
        dates.filters.as_ref().expect("date filter")
    ));
    assert_eq!(period_warnings, []);
    assert!(!filter_has_any_unsupported(
        periods.filters.as_ref().expect("period filter")
    ));
    assert!(null_warnings.iter().any(|warning| {
        warning.kind == DqlWarningKind::UnsupportedConstruct
            && warning.message.contains("null literal")
    }));
    assert!(filter_has_unsupported_reason(
        null_query.filters.as_ref().expect("null filter"),
        "DQL null literal is unsupported; guard with typeof"
    ));
}

#[test]
fn week_shorthands_execute_with_monday_through_sunday_boundaries() {
    let local_time = |month, day, hour| {
        Local
            .with_ymd_and_hms(2026, month, day, hour, 0, 0)
            .single()
            .expect("unambiguous local fixture time")
            .timestamp_millis()
    };
    let previous_sunday_ms = local_time(7, 5, 0);
    let monday_ms = local_time(7, 6, 0);
    let wednesday_ms = local_time(7, 8, 12);
    let sunday_ms = local_time(7, 12, 23).saturating_add(3_599_999);
    let next_monday_ms = local_time(7, 13, 0);

    let conn = dql_fixture_conn();
    for (path, mtime_ms) in [
        ("Hub.md", monday_ms),
        ("Target.md", sunday_ms),
        ("Other.md", previous_sunday_ms),
        ("123.md", next_monday_ms),
    ] {
        conn.execute(
            "UPDATE files SET mtime_ms = ?1 WHERE path = ?2",
            params![mtime_ms, path],
        )
        .expect("set week-boundary fixture mtime");
    }

    let (query, warnings) = parse_dql(
        "TABLE WITHOUT ID file.path\nWHERE file.mtime >= date(sow) AND file.mtime <= date(eow)\n",
    );
    let result = execute(
        &query,
        &conn,
        &EngineCtx {
            now_ms: wednesday_ms,
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("execute DQL week shorthand query");

    assert_eq!(warnings, []);
    assert_eq!(result.error, None);
    assert_eq!(row_paths(&result), ["Hub.md", "Target.md"]);
}

#[test]
fn unsupported_task_fields_are_not_silent_note_properties() {
    let (query, warnings) = parse_dql("TASK\nWHERE line > 0\n");

    assert!(warnings.iter().any(|warning| {
        warning.kind == DqlWarningKind::UnsupportedConstruct
            && warning.message.contains("task field line")
    }));
    assert!(filter_has_unsupported_reason(
        query.filters.as_ref().expect("task field filter"),
        "unsupported DQL task field line"
    ));
}

#[test]
fn supported_constructor_and_aggregate_functions_compile() {
    let (query, warnings) = parse_dql(
        r#"TABLE sum([file.size]) AS "Total", average([file.size]) AS "Average", string(file.name) AS "Name", array(file.name) AS "Names", embed(link(file.path)) AS "Embed"
"#,
    );

    assert_eq!(warnings, []);
    assert_eq!(query.formulas.len(), 5);
    assert!(
        query
            .formulas
            .iter()
            .all(|(_, expr)| !matches!(expr.kind, ExprKind::Unsupported { .. }))
    );
}

#[test]
fn unsupported_field_detection_respects_token_boundaries() {
    let (_file_query, file_warnings) = parse_dql("TABLE file.daylight\n");
    let (task_query, task_warnings) = parse_dql(
        r#"TASK
WHERE contains(link("Note"), file.path)
"#,
    );

    assert!(
        !file_warnings
            .iter()
            .any(|warning| warning.message.contains("unsupported DQL field file.day"))
    );
    assert_eq!(task_warnings, []);
    assert!(!filter_has_any_unsupported(
        task_query.filters.as_ref().expect("task link filter")
    ));
}

#[test]
fn parse_dql_is_total_and_deterministic_for_arbitrary_text() {
    let cases = [
        "",
        "nonsense",
        "TABLE ((((",
        "TASK\nWHERE created <= date(today)",
        "TABLE \"cafe\" AS \"Cafe\"\nFROM [[Café Note]]",
        "TABLE map(rows, (a, b) => a)",
        "CALENDAR file.day\nSORT file.name",
        "LIST WITHOUT ID contains(file.tags, \"x\")",
    ];

    for case in cases {
        let first = parse_dql(case);
        let second = parse_dql(case);
        assert_eq!(
            first, second,
            "parse_dql should be deterministic for {case:?}"
        );
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "snake_case")]
enum GoldenDisposition {
    Supported,
    Unsupported,
    RuntimeFailLoud,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "snake_case")]
enum GoldenSpanClass {
    Query,
    Line,
    Expression,
}

#[derive(Debug, Deserialize)]
struct GoldenDqlCase {
    name: String,
    operation: String,
    disposition: GoldenDisposition,
    #[serde(default)]
    coverage: Vec<String>,
    source: String,
    #[serde(default)]
    this_path: Option<String>,
    #[serde(default)]
    expected_semantics: Option<String>,
    #[serde(default)]
    expected_base: Option<String>,
    #[serde(default)]
    warning_kind: Option<String>,
    #[serde(default)]
    warning_message: Option<String>,
    #[serde(default)]
    warning_span_class: Option<GoldenSpanClass>,
    #[serde(default)]
    error_contains: Option<String>,
    #[serde(default)]
    expected_rows: Option<Vec<String>>,
    #[serde(default)]
    expected_cells: Option<Vec<Vec<String>>>,
}

#[derive(Debug)]
enum GeneratedExpectation {
    Supported {
        rows: Vec<String>,
        cells: Vec<Vec<String>>,
    },
    FailLoud {
        warning_kind: DqlWarningKind,
        warning_contains: String,
        error_contains: String,
    },
}

#[derive(Debug)]
struct GeneratedDqlCase {
    operation: &'static str,
    source: String,
    this_path: Option<String>,
    expectation: GeneratedExpectation,
}

#[test]
fn census_bases_dql_golden_corpus_executes_or_fails_loud() {
    const GOLDEN_CHILD: &str = "SLATE_DQL_GOLDEN_TZ_CHILD";
    if std::env::var(GOLDEN_CHILD).as_deref() != Ok("1") {
        let status = std::process::Command::new(
            std::env::current_exe().expect("locate DQL golden test binary"),
        )
        .arg("census_bases_dql_golden_corpus_executes_or_fails_loud")
        .arg("--exact")
        .arg("--nocapture")
        .env("TZ", "America/New_York")
        .env(GOLDEN_CHILD, "1")
        .status()
        .expect("run DQL golden corpus in its pinned local timezone");
        assert!(
            status.success(),
            "DQL golden corpus failed in pinned America/New_York timezone"
        );
        return;
    }

    let mut cases = Vec::<GoldenDqlCase>::new();
    for (name, source) in [
        ("closure", CLOSURE_CORPUS_JSON),
        ("field", FIELD_CORPUS_JSON),
        ("function", FUNCTION_CORPUS_JSON),
    ] {
        cases.extend(
            serde_json::from_str::<Vec<GoldenDqlCase>>(source)
                .unwrap_or_else(|error| panic!("parse checked-in DQL {name} corpus: {error}")),
        );
    }
    assert!(
        cases
            .iter()
            .any(|case| case.disposition == GoldenDisposition::Supported),
        "golden corpus must pin supported DQL"
    );
    assert!(
        cases
            .iter()
            .any(|case| case.disposition == GoldenDisposition::Unsupported),
        "golden corpus must pin unsupported DQL"
    );
    let disposition_counts = (
        cases
            .iter()
            .filter(|case| case.disposition == GoldenDisposition::Supported)
            .count(),
        cases
            .iter()
            .filter(|case| case.disposition == GoldenDisposition::Unsupported)
            .count(),
        cases
            .iter()
            .filter(|case| case.disposition == GoldenDisposition::RuntimeFailLoud)
            .count(),
    );
    assert_eq!(
        disposition_counts,
        (45, 117, 8),
        "checked-in DQL disposition census changed; review and bless the inventory explicitly"
    );
    let case_names = cases
        .iter()
        .map(|case| case.name.as_str())
        .collect::<BTreeSet<_>>();
    assert_eq!(
        case_names.len(),
        cases.len(),
        "golden corpus case names must be unique"
    );

    let conn = dql_fixture_conn();
    let mut missing_semantics = Vec::new();
    let mut missing_results = Vec::new();
    for case in &cases {
        let context = format!(
            "golden_case={} operation={} source={:?}",
            case.name, case.operation, case.source
        );
        let (query, warnings) = parse_dql(&case.source);
        let result = execute(
            &query,
            &conn,
            &EngineCtx {
                this_path: case.this_path.clone(),
                ..EngineCtx::default()
            },
            &CancelToken::new(),
        )
        .unwrap_or_else(|error| panic!("{context}: execute failed: {error}"));

        match case.disposition {
            GoldenDisposition::Supported => {
                assert_eq!(warnings, [], "{context}: unexpected conversion warnings");
                assert!(
                    result.rows.iter().all(|row| {
                        row.cells
                            .iter()
                            .all(|cell| !matches!(cell, CellValue::Error(_)))
                    }),
                    "{context}: supported case rendered an Error cell: {result:?}"
                );
                let actual_semantics = dql_query_semantic_signature(&query);
                if let Some(expected_semantics) = case.expected_semantics.as_deref() {
                    assert_eq!(
                        actual_semantics, expected_semantics,
                        "{context}: converted SlateQuery semantics changed"
                    );
                } else {
                    missing_semantics.push(format!("{}: {}", case.name, actual_semantics));
                }
                if let Some(expected_base) = case.expected_base.as_deref() {
                    let temporary_vault = tempfile::tempdir()
                        .unwrap_or_else(|error| panic!("{context}: create temp vault: {error}"));
                    let session =
                        VaultSession::from_filesystem(temporary_vault.path().to_path_buf())
                            .unwrap_or_else(|error| panic!("{context}: open temp vault: {error}"));
                    let actual_base = session
                        .dql_as_base(&case.source)
                        .unwrap_or_else(|error| panic!("{context}: convert DQL to Base: {error}"));
                    assert_eq!(
                        actual_base, expected_base,
                        "{context}: save-as-.base semantics changed"
                    );
                }
                assert_eq!(result.error, None, "{context}: execution failed loud");
                let actual_rows = result
                    .rows
                    .iter()
                    .map(|row| {
                        format!(
                            "{}#{}",
                            row.path,
                            row.task_ordinal
                                .map(|ordinal| ordinal.to_string())
                                .unwrap_or_else(|| "-".to_string())
                        )
                    })
                    .collect::<Vec<_>>();
                let actual_cells = result
                    .rows
                    .iter()
                    .map(|row| row.cells.iter().map(cell_signature).collect::<Vec<_>>())
                    .collect::<Vec<_>>();
                if let (Some(expected_rows), Some(expected_cells)) =
                    (&case.expected_rows, &case.expected_cells)
                {
                    assert_result_rows_and_cells(&result, expected_rows, expected_cells, &context);
                } else {
                    missing_results.push(format!(
                        "{}: rows={:?}; cells={:?}",
                        case.name, actual_rows, actual_cells
                    ));
                }
            }
            GoldenDisposition::Unsupported => {
                let expected_kind = case
                    .warning_kind
                    .as_deref()
                    .unwrap_or_else(|| panic!("{context}: unsupported case lacks warning_kind"));
                let expected_warning = case
                    .warning_message
                    .as_deref()
                    .unwrap_or_else(|| panic!("{context}: unsupported case lacks warning_message"));
                let expected_span = case.warning_span_class.unwrap_or_else(|| {
                    panic!("{context}: unsupported case lacks warning_span_class")
                });
                let matching_warning = warnings.iter().find(|warning| {
                        dql_warning_kind_name(warning.kind) == expected_kind
                            && warning.message == expected_warning
                    }).unwrap_or_else(|| panic!(
                        "{context}: expected exact {expected_kind} warning {expected_warning:?}, got {warnings:?}"
                    ));
                assert_eq!(
                    dql_warning_span_class(&case.source, matching_warning.span),
                    expected_span,
                    "{context}: warning span class changed"
                );
                let expected_error = case
                    .error_contains
                    .as_deref()
                    .unwrap_or_else(|| panic!("{context}: unsupported case lacks error_contains"));
                assert_fail_loud(&result, expected_error, &context);
            }
            GoldenDisposition::RuntimeFailLoud => {
                assert_eq!(warnings, [], "{context}: unexpected conversion warnings");
                let actual_semantics = dql_query_semantic_signature(&query);
                if let Some(expected_semantics) = case.expected_semantics.as_deref() {
                    assert_eq!(
                        actual_semantics, expected_semantics,
                        "{context}: converted SlateQuery semantics changed"
                    );
                } else {
                    missing_semantics.push(format!("{}: {}", case.name, actual_semantics));
                }
                let actual_rows = result
                    .rows
                    .iter()
                    .map(|row| {
                        format!(
                            "{}#{}",
                            row.path,
                            row.task_ordinal
                                .map(|ordinal| ordinal.to_string())
                                .unwrap_or_else(|| "-".to_string())
                        )
                    })
                    .collect::<Vec<_>>();
                let actual_cells = result
                    .rows
                    .iter()
                    .map(|row| row.cells.iter().map(cell_signature).collect::<Vec<_>>())
                    .collect::<Vec<_>>();
                if let (Some(expected_rows), Some(expected_cells)) =
                    (&case.expected_rows, &case.expected_cells)
                {
                    assert_result_rows_and_cells(&result, expected_rows, expected_cells, &context);
                } else {
                    missing_results.push(format!(
                        "{}: rows={:?}; cells={:?}",
                        case.name, actual_rows, actual_cells
                    ));
                }
                let expected_error = case.error_contains.as_deref().unwrap_or_else(|| {
                    panic!("{context}: runtime fail-loud case lacks error_contains")
                });
                assert_fail_loud(&result, expected_error, &context);
            }
        }
    }
    let actual_coverage = cases
        .iter()
        .flat_map(|case| case.coverage.iter().map(String::as_str))
        .collect::<BTreeSet<_>>();
    let coverage_tag_count = cases.iter().map(|case| case.coverage.len()).sum::<usize>();
    assert_eq!(
        actual_coverage.len(),
        coverage_tag_count,
        "each DQL mapping tag must belong to exactly one auditable golden case"
    );
    let expected_coverage = EXPECTED_DQL_GOLDEN_COVERAGE
        .iter()
        .copied()
        .collect::<BTreeSet<_>>();
    assert_eq!(
        actual_coverage, expected_coverage,
        "checked-in DQL golden coverage must exactly match the N0-5 mapping inventory"
    );
    assert!(
        missing_results.is_empty(),
        "supported DQL golden cases must pin exact rows and cells:\n{}",
        missing_results.join("\n")
    );
    assert!(
        missing_semantics.is_empty(),
        "supported DQL golden cases must pin exact SlateQuery semantics:\n{}",
        missing_semantics.join("\n")
    );
}

#[test]
fn census_bases_dql_generated_statements_execute_or_fail_loud() {
    let case_count = if std::env::var("SLATE_CENSUS_FULL").as_deref() == Ok("1") {
        4_096
    } else {
        256
    };
    let conn = dql_fixture_conn();
    let mut covered_operations = BTreeSet::new();
    let mut covered_sources = BTreeSet::new();

    for case_index in 0..case_count {
        let case = generated_dql_case(DQL_CENSUS_SEED, case_index);
        covered_operations.insert(case.operation);
        covered_sources.insert(case.source.clone());
        let context = format!(
            "seed={DQL_CENSUS_SEED:#018x} case_index={case_index} operation={} source={:?}",
            case.operation, case.source
        );
        assert_generated_dql_case(&conn, &case, &context);
    }

    assert_eq!(
        covered_operations,
        BTreeSet::from([
            "constructors",
            "group_by",
            "malformed",
            "outgoing",
            "regex_escapes",
            "tasks",
            "trunc",
            "unsupported",
        ]),
        "seed={DQL_CENSUS_SEED:#018x}: generated census lost an operation family"
    );
    assert_eq!(
        covered_sources.len(),
        case_count,
        "seed={DQL_CENSUS_SEED:#018x}: generated census repeated a statement"
    );
}

fn generated_dql_case(seed: u64, case_index: usize) -> GeneratedDqlCase {
    let word = census_word(seed, case_index as u64);
    match case_index % 8 {
        0 => GeneratedDqlCase {
            operation: "outgoing",
            source: format!(
                "TABLE WITHOUT ID file.path AS \"Path {case_index}\"\nFROM outgoing([[Hub]])\nLIMIT 1\n"
            ),
            this_path: None,
            expectation: GeneratedExpectation::Supported {
                rows: vec!["Target.md#-".to_string()],
                cells: vec![vec!["text:Target.md".to_string()]],
            },
        },
        1 => {
            let (pattern, candidate) = match word % 3 {
                0 => (r#""\d+""#, format!("case{case_index}123")),
                1 => (r#""a/b""#, format!("case{case_index}a/b")),
                _ => (r#""\w+[ ]+\w+""#, format!("case{case_index} second")),
            };
            GeneratedDqlCase {
                operation: "regex_escapes",
                source: format!(
                    "TABLE WITHOUT ID regextest({pattern}, \"{candidate}\") AS \"Match {case_index}\"\nWHERE file.path = \"123.md\"\n"
                ),
                this_path: None,
                expectation: GeneratedExpectation::Supported {
                    rows: vec!["123.md#-".to_string()],
                    cells: vec![vec!["bool:true".to_string()]],
                },
            }
        }
        2 => {
            let whole = 1 + (word % 97) as i64;
            let tenth = 1 + ((word >> 8) % 9) as i64;
            let negative = word & 1 == 1;
            let literal = if negative {
                format!("-{whole}.{tenth}")
            } else {
                format!("{whole}.{tenth}")
            };
            let truncated = if negative { -whole } else { whole };
            GeneratedDqlCase {
                operation: "trunc",
                source: format!(
                    "TABLE WITHOUT ID trunc({literal}) AS \"Truncated {case_index}\"\nWHERE file.path = \"123.md\"\n"
                ),
                this_path: None,
                expectation: GeneratedExpectation::Supported {
                    rows: vec!["123.md#-".to_string()],
                    cells: vec![vec![format!("number:{truncated}")]],
                },
            }
        }
        3 => {
            let number = 1 + word % 999;
            let token = format!("case-{case_index}-{number}");
            GeneratedDqlCase {
                operation: "constructors",
                source: format!(
                    "TABLE WITHOUT ID string({number}) AS \"String\", array(\"{token}\") AS \"Array\", object(\"key\", \"{token}\")[\"key\"] AS \"Object\", link(file.path) AS \"Link\"\nWHERE file.path = \"123.md\"\n"
                ),
                this_path: None,
                expectation: GeneratedExpectation::Supported {
                    rows: vec!["123.md#-".to_string()],
                    cells: vec![vec![
                        format!("text:{number}"),
                        format!("list:[text:{token}]"),
                        format!("text:{token}"),
                        "link:123.md|-|123.md|-|file|false".to_string(),
                    ]],
                },
            }
        }
        4 => {
            let completed = word & 1 == 0;
            let predicate = if completed {
                format!("completed AND text != \"never-{case_index}\"")
            } else {
                format!("!completed AND text != \"never-{case_index}\"")
            };
            let rows = if completed {
                vec!["Hub.md#1".to_string(), "Hub.md#2".to_string()]
            } else {
                vec![
                    "Hub.md#0".to_string(),
                    "Target.md#0".to_string(),
                    "Target.md#1".to_string(),
                ]
            };
            GeneratedDqlCase {
                operation: "tasks",
                source: format!("TASK\nWHERE {predicate}\nSORT due ASC\n"),
                this_path: None,
                expectation: GeneratedExpectation::Supported {
                    cells: vec![Vec::new(); rows.len()],
                    rows,
                },
            }
        }
        5 => match word % 3 {
            0 => GeneratedDqlCase {
                operation: "malformed",
                source: format!("TABLE file.name\nLIMIT nope{case_index}\n"),
                this_path: None,
                expectation: GeneratedExpectation::FailLoud {
                    warning_kind: DqlWarningKind::InvalidCommand,
                    warning_contains: "LIMIT must be an unsigned integer".to_string(),
                    error_contains: "invalid LIMIT".to_string(),
                },
            },
            1 => GeneratedDqlCase {
                operation: "malformed",
                source: format!("TABLE WITHOUT ID regextest(\"x\", \"case-{case_index}\"\n"),
                this_path: None,
                expectation: GeneratedExpectation::FailLoud {
                    warning_kind: DqlWarningKind::UnsupportedConstruct,
                    warning_contains: "unterminated function call regextest".to_string(),
                    error_contains: "unterminated function call regextest".to_string(),
                },
            },
            _ => GeneratedDqlCase {
                operation: "malformed",
                source: format!("LIST file.name, \"case-{case_index}\"\n"),
                this_path: None,
                expectation: GeneratedExpectation::FailLoud {
                    warning_kind: DqlWarningKind::InvalidCommand,
                    warning_contains: "LIST accepts at most one expression".to_string(),
                    error_contains: "LIST with multiple expressions".to_string(),
                },
            },
        },
        6 => match word % 3 {
            0 => GeneratedDqlCase {
                operation: "unsupported",
                source: format!("TABLE WITHOUT ID upper(file.name) AS \"Upper {case_index}\"\n"),
                this_path: None,
                expectation: GeneratedExpectation::FailLoud {
                    warning_kind: DqlWarningKind::UnsupportedConstruct,
                    warning_contains: "unsupported DQL function upper".to_string(),
                    error_contains: "unsupported DQL function upper".to_string(),
                },
            },
            1 => GeneratedDqlCase {
                operation: "unsupported",
                source: format!("TABLE WITHOUT ID file.etags AS \"Tags {case_index}\"\n"),
                this_path: None,
                expectation: GeneratedExpectation::FailLoud {
                    warning_kind: DqlWarningKind::UnsupportedConstruct,
                    warning_contains: "unsupported DQL field file.etags".to_string(),
                    error_contains: "unsupported DQL field file.etags".to_string(),
                },
            },
            _ => GeneratedDqlCase {
                operation: "unsupported",
                source: format!("TASK\nWHERE line > {case_index}\n"),
                this_path: None,
                expectation: GeneratedExpectation::FailLoud {
                    warning_kind: DqlWarningKind::UnsupportedConstruct,
                    warning_contains: "unsupported DQL task field line".to_string(),
                    error_contains: "unsupported DQL task field line".to_string(),
                },
            },
        },
        _ => GeneratedDqlCase {
            operation: "group_by",
            source: format!("TABLE file.name AS \"Name {case_index}\"\nGROUP BY file.folder\n"),
            this_path: None,
            expectation: GeneratedExpectation::FailLoud {
                warning_kind: DqlWarningKind::UnsupportedConstruct,
                warning_contains: "GROUP BY changes row membership".to_string(),
                error_contains: "rows aggregation".to_string(),
            },
        },
    }
}

fn census_word(seed: u64, case_index: u64) -> u64 {
    let mut value = seed ^ case_index.wrapping_mul(0x9e37_79b9_7f4a_7c15);
    value = (value ^ (value >> 30)).wrapping_mul(0xbf58_476d_1ce4_e5b9);
    value = (value ^ (value >> 27)).wrapping_mul(0x94d0_49bb_1331_11eb);
    value ^ (value >> 31)
}

fn assert_generated_dql_case(conn: &Connection, case: &GeneratedDqlCase, context: &str) {
    let (query, warnings) = parse_dql(&case.source);
    let result = execute(
        &query,
        conn,
        &EngineCtx {
            this_path: case.this_path.clone(),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .unwrap_or_else(|error| panic!("{context}: execute failed: {error}"));

    match &case.expectation {
        GeneratedExpectation::Supported { rows, cells } => {
            assert_eq!(warnings, [], "{context}: unexpected conversion warnings");
            assert_eq!(result.error, None, "{context}: execution failed loud");
            assert_result_rows_and_cells(&result, rows, cells, context);
        }
        GeneratedExpectation::FailLoud {
            warning_kind,
            warning_contains,
            error_contains,
        } => {
            assert!(
                warnings.iter().any(|warning| {
                    warning.kind == *warning_kind
                        && warning.message.contains(warning_contains.as_str())
                }),
                "{context}: expected {warning_kind:?} warning containing {warning_contains:?}, got {warnings:?}"
            );
            assert_fail_loud(&result, error_contains, context);
        }
    }
}

fn assert_result_rows_and_cells(
    result: &slate_core::bases::engine::BasesResultSet,
    expected_rows: &[String],
    expected_cells: &[Vec<String>],
    context: &str,
) {
    let actual_rows = result
        .rows
        .iter()
        .map(|row| {
            format!(
                "{}#{}",
                row.path,
                row.task_ordinal
                    .map(|ordinal| ordinal.to_string())
                    .unwrap_or_else(|| "-".to_string())
            )
        })
        .collect::<Vec<_>>();
    let actual_cells = result
        .rows
        .iter()
        .map(|row| row.cells.iter().map(cell_signature).collect::<Vec<_>>())
        .collect::<Vec<_>>();
    assert_eq!(actual_rows, expected_rows, "{context}: row mismatch");
    assert_eq!(actual_cells, expected_cells, "{context}: cell mismatch");
}

fn assert_fail_loud(
    result: &slate_core::bases::engine::BasesResultSet,
    expected_error: &str,
    context: &str,
) {
    if let Some(error) = &result.error {
        assert!(
            error.construct.contains(expected_error),
            "{context}: expected fail-loud construct containing {expected_error:?}, got {error:?}"
        );
        assert!(
            result.rows.is_empty(),
            "{context}: result-level failure must not leak partial rows"
        );
        return;
    }

    assert!(
        !result.rows.is_empty()
            && result.rows.iter().all(|row| {
                !row.cells.is_empty()
                    && row.cells.iter().all(|cell| {
                        matches!(cell, CellValue::Error(error) if error.contains(expected_error))
                    })
            }),
        "{context}: expected every rendered cell to name {expected_error:?}, got {result:?}"
    );
}

fn dql_warning_kind_name(kind: DqlWarningKind) -> &'static str {
    match kind {
        DqlWarningKind::ParseProblem => "parse_problem",
        DqlWarningKind::UnsupportedConstruct => "unsupported_construct",
        DqlWarningKind::InvalidCommand => "invalid_command",
        DqlWarningKind::InvalidExpression => "invalid_expression",
    }
}

fn dql_warning_span_class(
    source: &str,
    warning_span: slate_core::bases::expr::Span,
) -> GoldenSpanClass {
    if warning_span.start == 0 && warning_span.end as usize == source.len() {
        return GoldenSpanClass::Query;
    }

    let mut offset = 0usize;
    for raw in source.split_inclusive('\n') {
        let line = raw.strip_suffix('\n').unwrap_or(raw);
        let leading = line.len() - line.trim_start().len();
        let trimmed = line.trim();
        let start = offset + leading;
        let end = start + trimmed.len();
        if warning_span.start as usize == start && warning_span.end as usize == end {
            return GoldenSpanClass::Line;
        }
        offset += raw.len();
    }
    GoldenSpanClass::Expression
}

fn dql_query_semantic_signature(query: &SlateQuery) -> String {
    let filters = query
        .filters
        .as_ref()
        .map(dql_filter_semantic_signature)
        .unwrap_or_else(|| "-".to_string());
    let formulas = query
        .formulas
        .iter()
        .map(|(name, expr)| format!("{name}={}", dql_expr_semantic_signature(expr)))
        .collect::<Vec<_>>()
        .join(",");
    let custom_summaries = query
        .custom_summaries
        .iter()
        .map(|(name, expr)| format!("{name}={}", dql_expr_semantic_signature(expr)))
        .collect::<Vec<_>>()
        .join(",");
    let sort = query
        .sort
        .iter()
        .map(|key| {
            format!(
                "{}:{}",
                dql_expr_semantic_signature(dql_sort_expr(&key.expr)),
                if key.ascending { "asc" } else { "desc" }
            )
        })
        .collect::<Vec<_>>()
        .join(",");
    let columns = query
        .columns
        .iter()
        .map(|column| {
            format!(
                "{}:{}",
                column.id,
                column.display_name.as_deref().unwrap_or("-")
            )
        })
        .collect::<Vec<_>>()
        .join(",");

    format!(
        "source={:?};rows={:?};view={:?};filters={filters};formulas=[{formulas}];custom=[{custom_summaries}];group={:?};sort=[{sort}];columns=[{columns}];summaries={:?};limit={:?}",
        query.source, query.row_source, query.view, query.group_by, query.summaries, query.limit
    )
}

fn dql_sort_expr(expr: &Expr) -> &Expr {
    let ExprKind::Lit(Lit::Object(entries)) = &expr.kind else {
        return expr;
    };
    let [(key, value)] = entries.as_slice() else {
        return expr;
    };
    if key == "\u{f8ff}slate.dql.command-sort" {
        value
    } else {
        expr
    }
}

fn dql_filter_semantic_signature(filter: &FilterNode) -> String {
    match filter {
        FilterNode::Stmt(expr) => dql_expr_semantic_signature(expr),
        FilterNode::And(nodes) => {
            let signatures = nodes
                .iter()
                .map(dql_filter_semantic_signature)
                .collect::<Vec<_>>();
            if signatures.len() > 1
                && signatures
                    .iter()
                    .skip(1)
                    .all(|signature| signature == &signatures[0])
            {
                format!("and_repeat({},{})", signatures.len(), signatures[0])
            } else {
                format!("and({})", signatures.join(","))
            }
        }
        FilterNode::Or(nodes) => format!(
            "or({})",
            nodes
                .iter()
                .map(dql_filter_semantic_signature)
                .collect::<Vec<_>>()
                .join(",")
        ),
        FilterNode::Not(nodes) => format!(
            "not({})",
            nodes
                .iter()
                .map(dql_filter_semantic_signature)
                .collect::<Vec<_>>()
                .join(",")
        ),
    }
}

fn dql_expr_semantic_signature(expr: &Expr) -> String {
    match &expr.kind {
        ExprKind::Lit(lit) => match lit {
            Lit::String(value) => serde_json::to_string(value).expect("serialize string literal"),
            Lit::Number(value) => format!("{value:?}"),
            Lit::Bool(value) => value.to_string(),
            Lit::List(values) => format!(
                "[{}]",
                values
                    .iter()
                    .map(dql_expr_semantic_signature)
                    .collect::<Vec<_>>()
                    .join(",")
            ),
            Lit::Object(values) => format!(
                "{{{}}}",
                values
                    .iter()
                    .map(|(key, value)| format!(
                        "{}:{}",
                        serde_json::to_string(key).expect("serialize object key"),
                        dql_expr_semantic_signature(value)
                    ))
                    .collect::<Vec<_>>()
                    .join(",")
            ),
            Lit::Regex { pattern, flags } => format!("/{pattern}/{flags}"),
        },
        ExprKind::Prop(property) => format!("prop({property:?})"),
        ExprKind::Index { base, index } => format!(
            "index({},{})",
            dql_expr_semantic_signature(base),
            dql_expr_semantic_signature(index)
        ),
        ExprKind::Field { base, name } => format!(
            "field({},{})",
            dql_expr_semantic_signature(base),
            serde_json::to_string(name).expect("serialize field name")
        ),
        ExprKind::Unary { op, rhs } => {
            format!("unary({op:?},{})", dql_expr_semantic_signature(rhs))
        }
        ExprKind::Binary { op, lhs, rhs } => format!(
            "binary({op:?},{},{})",
            dql_expr_semantic_signature(lhs),
            dql_expr_semantic_signature(rhs)
        ),
        ExprKind::Call { callee, args } => {
            let callee = match callee {
                Callee::Global(function) => format!("global({function:?})"),
                Callee::Method { receiver, name } => {
                    format!("method({},{name:?})", dql_expr_semantic_signature(receiver))
                }
            };
            format!(
                "call({callee},[{}])",
                args.iter()
                    .map(dql_expr_semantic_signature)
                    .collect::<Vec<_>>()
                    .join(",")
            )
        }
        ExprKind::ListExpr {
            base,
            kind,
            body,
            init,
        } => format!(
            "list_expr({kind:?},{},{},{})",
            dql_expr_semantic_signature(base),
            dql_expr_semantic_signature(body),
            init.as_deref()
                .map(dql_expr_semantic_signature)
                .unwrap_or_else(|| "-".to_string())
        ),
        ExprKind::Unsupported { raw, reason } => format!(
            "unsupported({},{})",
            serde_json::to_string(raw).expect("serialize unsupported source"),
            serde_json::to_string(reason).expect("serialize unsupported reason")
        ),
    }
}

fn cell_signature(cell: &CellValue) -> String {
    match cell {
        CellValue::Value(value) => value_signature(value),
        CellValue::Error(error) => format!("error:{error}"),
    }
}

fn value_signature(value: &Value) -> String {
    match value {
        Value::Null => "null".to_string(),
        Value::Bool(value) => format!("bool:{value}"),
        Value::Number(value) => format!("number:{value}"),
        Value::Text(value) => format!("text:{value}"),
        Value::Date(value) => format!("date:{}:{}", value.epoch_ms, value.has_time),
        Value::DqlDate(value) => {
            let wall_epoch_ms = value
                .epoch_ms
                .saturating_add(i64::from(value.offset_minutes).saturating_mul(60_000));
            let wall = DateTime::<Utc>::from_timestamp_millis(wall_epoch_ms)
                .map(|date| {
                    if value.has_time {
                        date.format("%Y-%m-%dT%H:%M:%S%.3f").to_string()
                    } else {
                        date.format("%Y-%m-%d").to_string()
                    }
                })
                .unwrap_or_else(|| "invalid".to_string());
            let provenance = if value.is_local {
                "local".to_string()
            } else {
                format!("offset={}", value.offset_minutes)
            };
            format!("dql-date:{wall}:{}:{provenance}", value.has_time)
        }
        Value::Duration(value) => format!("duration:{value}"),
        Value::DqlDuration(value) => format!(
            "dql-duration:{:?}:{:?}:{:?}:{:?}:{:?}:{:?}:{:?}:{:?}",
            value.years,
            value.months,
            value.weeks,
            value.days,
            value.hours,
            value.minutes,
            value.seconds,
            value.milliseconds
        ),
        Value::List(values) => format!(
            "list:[{}]",
            values
                .iter()
                .map(value_signature)
                .collect::<Vec<_>>()
                .join(",")
        ),
        Value::Object(values) => format!(
            "object:{{{}}}",
            values
                .iter()
                .map(|(key, value)| format!("{key}={}", value_signature(value)))
                .collect::<Vec<_>>()
                .join(",")
        ),
        Value::Link(value) => format!(
            "link:{}|{}|{}|{}|{}|{}",
            value.target,
            value.display.as_deref().unwrap_or("-"),
            value.resolved_path.as_deref().unwrap_or("-"),
            value.subpath.as_deref().unwrap_or("-"),
            value.link_type,
            value.embed
        ),
        Value::File(value) => format!("file:{}", value.path),
        Value::Regex(pattern, flags) => format!("regex:{pattern}/{flags}"),
    }
}

fn dql_fixture_conn() -> Connection {
    let mut conn = Connection::open_in_memory().expect("open in-memory database");
    migrate(&mut conn).expect("migrate schema");
    for (id, path) in [
        (1_i64, "Hub.md"),
        (2, "Target.md"),
        (3, "Other.md"),
        (4, "123.md"),
        (5, "Notes/Hub.md"),
        (6, "Notes/Target.md"),
        (7, "Notes/View.base"),
        (8, "Deep/Nested Note.md"),
        (9, "Assets/Board.canvas"),
    ] {
        conn.execute(
            "INSERT INTO files (
                id, path, name, extension, size_bytes, mtime_ms, ctime_ms,
                content_hash, parser_version, indexed_at_ms, is_markdown
             )
             VALUES (?1, ?2, ?2, 'md', 0, 0, 0, ?3, 1, 0, 1)",
            params![id, path, format!("hash-{id}")],
        )
        .expect("insert DQL fixture file");
    }
    conn.execute(
        "INSERT INTO dql_inline_field_state (file_id, incomplete)
         SELECT id, 0 FROM files",
        [],
    )
    .expect("mark DQL inline fixture projections complete");
    conn.execute(
        "UPDATE files
         SET size_bytes = 123, ctime_ms = 1767225600000, mtime_ms = 1767312000000
         WHERE id = 4",
        [],
    )
    .expect("enrich DQL file-field fixture");
    for (file_id, tag) in [
        (1_i64, "project"),
        (1, "project/subtag"),
        (4, "reading"),
        (4, "a/b"),
    ] {
        conn.execute(
            "INSERT INTO file_tags (file_id, tag_norm) VALUES (?1, ?2)",
            params![file_id, tag],
        )
        .expect("insert DQL fixture tag");
    }
    for (file_id, ordinal, tag) in [
        (1_i64, 0_i64, "Project/SubTag"),
        (1, 1, "Project"),
        (4, 0, "Reading"),
        (4, 1, "A/B"),
    ] {
        conn.execute(
            "INSERT INTO dql_file_tags (file_id, ordinal, tag_raw) VALUES (?1, ?2, ?3)",
            params![file_id, ordinal, tag],
        )
        .expect("insert raw ordered DQL fixture tag");
    }
    for (file_id, ordinal, key, kind, value, normalized) in [
        (1_i64, 0_i64, "status", "text", r#""active""#, "active"),
        (4, 0, "status", "text", r#""reading""#, "reading"),
        (
            4,
            1,
            "aliases",
            "list",
            r#"["One","Two","A,B",0,false,""]"#,
            r#"["one","two","a,b",0,false,""]"#,
        ),
        (
            4,
            2,
            "scheduled",
            "date",
            r#""2026-07-10""#,
            "1783641600000",
        ),
        (4, 3, "pages", "number", "120", "120"),
        (4, 4, "minutesPerPage", "number", "3", "3"),
        (4, 5, "rating", "number", "4.5", "4.5"),
        (4, 6, "scores", "list", "[1,2]", "[1,2]"),
        (4, 7, "value", "text", r#""x""#, "x"),
        (4, 8, "index", "number", "10", "10"),
        (4, 9, "key", "text", r#""name""#, "name"),
        (4, 10, "total-cost", "number", "99", "99"),
        (4, 11, "total", "number", "10", "10"),
        (4, 12, "cost", "number", "3", "3"),
        (4, 13, "reading-status", "text", r#""done""#, "done"),
        (4, 14, "where", "text", r#""keyword""#, "keyword"),
        (4, 15, "My Field", "number", "88", "88"),
        (4, 16, "Phase 2", "number", "22", "22"),
        (4, 17, "A--B", "number", "33", "33"),
        (4, 18, "My.Field", "number", "44", "44"),
        (4, 19, "My  Field", "number", "55", "55"),
        (4, 20, "Alias", "text", r#""Three, Two""#, "three, two"),
        (4, 21, "My·Field", "number", "66", "66"),
        (4, 22, "Inline Field", "number", "1", "1"),
        (4, 23, "exact-key", "number", "9", "9"),
        (1, 1, "priority", "number", "99", "99"),
        (1, 2, "My Field", "number", "1", "1"),
        (1, 3, "my-field", "number", "2", "2"),
        (1, 4, "My  Field", "number", "3", "3"),
    ] {
        conn.execute(
            "INSERT INTO properties (file_id, ordinal, key, value_kind, value_text, value_text_norm)
             VALUES (?1, ?2, ?3, ?4, ?5, ?6)",
            params![file_id, ordinal, key, kind, value, normalized],
        )
        .expect("insert DQL fixture property");
    }
    for (ordinal, key, value_json) in [
        (0_i64, "Inline  Field", r#"{"kind":"text","value":"list"}"#),
        (1, "Inline Field", r#"{"kind":"text","value":"page"}"#),
        (2, "Exact Key", r#"{"kind":"number","value":10.0}"#),
        (3, "Inline Date", r#"{"kind":"date","value":"2026-07-10"}"#),
        (
            4,
            "Inline Duration",
            r#"{"kind":"duration","value":"1day"}"#,
        ),
        (
            5,
            "Inline Link",
            r#"{"kind":"link","value":{"target":"Target","display":null,"embed":false,"link_type":"file","subpath":null}}"#,
        ),
        (24, "aliases", r#"{"kind":"text","value":"Phantom"}"#),
    ] {
        conn.execute(
            "INSERT INTO dql_inline_fields (file_id, ordinal, key, value_json)
             VALUES (4, ?1, ?2, ?3)",
            params![ordinal, key, value_json],
        )
        .expect("insert DQL inline-field fixture");
    }
    conn.execute(
        "INSERT INTO links (
            source_file_id, ordinal, target_path, target_raw, target_anchor,
            kind, is_embed, is_external, snippet, span_start, span_end
         )
         VALUES (1, 0, 'Target.md', 'Target', NULL, 'wikilink', 0, 0, '', 0, 10)",
        [],
    )
    .expect("insert outgoing DQL fixture link");
    conn.execute(
        "INSERT INTO links (
            source_file_id, ordinal, target_path, target_raw, target_anchor,
            kind, is_embed, is_external, snippet, span_start, span_end
         )
         VALUES (5, 0, 'Notes/Target.md', 'Target', NULL, 'wikilink', 0, 0, '', 0, 10)",
        [],
    )
    .expect("insert contextual outgoing DQL fixture link");
    conn.execute(
        "INSERT INTO links (
            source_file_id, ordinal, target_path, target_raw, target_anchor,
            kind, is_embed, is_external, snippet, span_start, span_end
         )
         VALUES (3, 0, 'Hub.md', 'Hub', NULL, 'wikilink', 0, 0, '', 0, 8)",
        [],
    )
    .expect("insert current-file incoming DQL fixture link");
    for (file_id, ordinal, text, status, completed, due_ms, scheduled_ms, priority) in [
        (
            1_i64,
            0_i64,
            "Open task",
            " ",
            false,
            86_400_000_i64,
            172_800_000_i64,
            3_i64,
        ),
        (1, 1, "Done task", "x", true, 86_400_000, 172_800_000, 1),
        (
            1,
            2,
            "Uppercase task",
            "X",
            true,
            86_400_000,
            172_800_000,
            1,
        ),
        (
            2,
            0,
            "Waiting task",
            "/",
            false,
            259_200_000,
            345_600_000,
            2,
        ),
        (2, 1, "Empty task", "", false, 259_200_000, 345_600_000, 2),
    ] {
        conn.execute(
            "INSERT INTO tasks (
                file_id, ordinal, text, status_char, completed, due_ms, scheduled_ms,
                priority, recurrence, line, byte_offset
             )
             VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, NULL, ?9, ?10)",
            params![
                file_id,
                ordinal,
                text,
                status,
                completed,
                due_ms,
                scheduled_ms,
                priority,
                10 + ordinal,
                100 + ordinal,
            ],
        )
        .expect("insert DQL fixture task");
    }
    conn
}

fn dql_embed_fixture_conn() -> Connection {
    let mut conn = Connection::open_in_memory().expect("open in-memory database");
    migrate(&mut conn).expect("migrate schema");
    for (id, path) in [(1_i64, "Hub.md"), (2, "Target.md"), (3, "Other.md")] {
        conn.execute(
            "INSERT INTO files (
                id, path, name, extension, size_bytes, mtime_ms, ctime_ms,
                content_hash, parser_version, indexed_at_ms, is_markdown
             )
             VALUES (?1, ?2, ?2, 'md', 0, 0, 0, ?3, 1, 0, 1)",
            params![id, path, format!("embed-hash-{id}")],
        )
        .expect("insert embed DQL fixture file");
    }
    conn.execute(
        "INSERT INTO links (
            source_file_id, ordinal, target_path, target_raw, target_anchor,
            kind, is_embed, is_external, snippet, span_start, span_end
         )
         VALUES (1, 0, 'Target.md', 'Target', NULL, 'wikilink', 1, 0, '', 0, 11)",
        [],
    )
    .expect("insert embed-only DQL fixture link");
    conn.execute(
        "INSERT INTO links (
            source_file_id, ordinal, target_path, target_raw, target_anchor,
            kind, is_embed, is_external, snippet, span_start, span_end
         )
         VALUES (3, 0, 'Target.md', 'Target', NULL, 'wikilink', 0, 0, '', 0, 10)",
        [],
    )
    .expect("insert ordinary incoming DQL control link");
    conn
}

fn execute_dql(
    conn: &Connection,
    source: &str,
    this_path: Option<&str>,
) -> slate_core::bases::engine::BasesResultSet {
    let (query, warnings) = parse_dql(source);
    assert_eq!(warnings, [], "fixture should convert without loss");
    execute(
        &query,
        conn,
        &EngineCtx {
            this_path: this_path.map(str::to_string),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("execute converted DQL")
}

fn row_paths(result: &slate_core::bases::engine::BasesResultSet) -> Vec<&str> {
    result.rows.iter().map(|row| row.path.as_str()).collect()
}

fn first_value(result: &slate_core::bases::engine::BasesResultSet, column: usize) -> &Value {
    let Some(CellValue::Value(value)) = result.rows[0].cells.get(column) else {
        panic!("expected first-row value in column {column}: {result:?}");
    };
    value
}

fn regex_pattern(expr: &Expr) -> &str {
    let ExprKind::Call {
        callee: Callee::Method { receiver, .. },
        ..
    } = &expr.kind
    else {
        panic!("regextest should convert to a regex method call: {expr:?}");
    };
    match &receiver.kind {
        ExprKind::Lit(Lit::Regex { pattern, flags }) => {
            assert!(flags.is_empty());
            pattern
        }
        ExprKind::Call {
            callee: Callee::Global(slate_core::bases::expr::GlobalFn::Object),
            args,
        } => match &args[..] {
            [
                Expr {
                    kind: ExprKind::Lit(Lit::String(key)),
                    ..
                },
                Expr {
                    kind: ExprKind::Lit(Lit::String(pattern)),
                    ..
                },
                ..,
            ] if key == "\u{f8ff}slate.dql.regex" => pattern,
            _ => panic!("regextest receiver has malformed DQL regex marker: {receiver:?}"),
        },
        _ => panic!("regextest receiver should preserve its DQL regex marker: {receiver:?}"),
    }
}

fn filter_contains_task_completed(filter: &FilterNode) -> bool {
    match filter {
        FilterNode::Stmt(expr) => expr_contains_task_completed(expr),
        FilterNode::And(nodes) | FilterNode::Or(nodes) | FilterNode::Not(nodes) => {
            nodes.iter().any(filter_contains_task_completed)
        }
    }
}

fn expr_contains_task_completed(expr: &Expr) -> bool {
    match &expr.kind {
        ExprKind::Prop(PropertyRef::TaskField(TaskField::Completed)) => true,
        ExprKind::Binary { op, lhs, rhs } if *op == BinaryOp::Eq => {
            let lhs = match &lhs.kind {
                ExprKind::Lit(Lit::Object(values)) => values
                    .iter()
                    .find(|(key, _)| key == "\u{f8ff}slate.dql.equality")
                    .map(|(_, value)| value)
                    .unwrap_or(lhs),
                _ => lhs,
            };
            matches!(
                lhs.kind,
                ExprKind::Prop(PropertyRef::TaskField(TaskField::Status))
            ) && matches!(rhs.kind, ExprKind::Lit(slate_core::bases::expr::Lit::String(ref s)) if s == "x")
        }
        ExprKind::Unary { rhs, .. } => expr_contains_task_completed(rhs),
        ExprKind::Binary { lhs, rhs, .. } => {
            expr_contains_task_completed(lhs) || expr_contains_task_completed(rhs)
        }
        ExprKind::Call { callee, args } => {
            matches!(callee, Callee::Method { receiver, .. } if expr_contains_task_completed(receiver))
                || args.iter().any(expr_contains_task_completed)
        }
        ExprKind::Index { base, index } => {
            expr_contains_task_completed(base) || expr_contains_task_completed(index)
        }
        ExprKind::Field { base, .. } => expr_contains_task_completed(base),
        ExprKind::ListExpr {
            base, body, init, ..
        } => {
            expr_contains_task_completed(base)
                || expr_contains_task_completed(body)
                || init.as_deref().is_some_and(expr_contains_task_completed)
        }
        ExprKind::Lit(Lit::List(values)) => values.iter().any(expr_contains_task_completed),
        ExprKind::Lit(Lit::Object(values)) => values
            .iter()
            .any(|(_, value)| expr_contains_task_completed(value)),
        ExprKind::Lit(_) | ExprKind::Prop(_) | ExprKind::Unsupported { .. } => false,
    }
}

fn filter_has_unsupported_reason(filter: &FilterNode, reason: &str) -> bool {
    match filter {
        FilterNode::Stmt(expr) => expr_has_unsupported_reason(expr, reason),
        FilterNode::And(nodes) | FilterNode::Or(nodes) | FilterNode::Not(nodes) => nodes
            .iter()
            .any(|node| filter_has_unsupported_reason(node, reason)),
    }
}

fn filter_has_any_unsupported(filter: &FilterNode) -> bool {
    match filter {
        FilterNode::Stmt(expr) => expr_has_any_unsupported(expr),
        FilterNode::And(nodes) | FilterNode::Or(nodes) | FilterNode::Not(nodes) => {
            nodes.iter().any(filter_has_any_unsupported)
        }
    }
}

fn expr_has_unsupported_reason(expr: &Expr, reason: &str) -> bool {
    match &expr.kind {
        ExprKind::Unsupported { reason: got, .. } => got == reason,
        ExprKind::Unary { rhs, .. } => expr_has_unsupported_reason(rhs, reason),
        ExprKind::Binary { lhs, rhs, .. } => {
            expr_has_unsupported_reason(lhs, reason) || expr_has_unsupported_reason(rhs, reason)
        }
        ExprKind::Call { callee, args } => {
            matches!(callee, Callee::Method { receiver, .. } if expr_has_unsupported_reason(receiver, reason))
                || args
                    .iter()
                    .any(|arg| expr_has_unsupported_reason(arg, reason))
        }
        ExprKind::Index { base, index } => {
            expr_has_unsupported_reason(base, reason) || expr_has_unsupported_reason(index, reason)
        }
        ExprKind::Field { base, .. } => expr_has_unsupported_reason(base, reason),
        ExprKind::ListExpr {
            base, body, init, ..
        } => {
            expr_has_unsupported_reason(base, reason)
                || expr_has_unsupported_reason(body, reason)
                || init
                    .as_deref()
                    .is_some_and(|expr| expr_has_unsupported_reason(expr, reason))
        }
        ExprKind::Lit(Lit::List(values)) => values
            .iter()
            .any(|value| expr_has_unsupported_reason(value, reason)),
        ExprKind::Lit(Lit::Object(values)) => values
            .iter()
            .any(|(_, value)| expr_has_unsupported_reason(value, reason)),
        ExprKind::Lit(_) | ExprKind::Prop(_) => false,
    }
}

fn expr_has_any_unsupported(expr: &Expr) -> bool {
    match &expr.kind {
        ExprKind::Unsupported { .. } => true,
        ExprKind::Unary { rhs, .. } => expr_has_any_unsupported(rhs),
        ExprKind::Binary { lhs, rhs, .. } => {
            expr_has_any_unsupported(lhs) || expr_has_any_unsupported(rhs)
        }
        ExprKind::Call { callee, args } => {
            matches!(callee, Callee::Method { receiver, .. } if expr_has_any_unsupported(receiver))
                || args.iter().any(expr_has_any_unsupported)
        }
        ExprKind::Index { base, index } => {
            expr_has_any_unsupported(base) || expr_has_any_unsupported(index)
        }
        ExprKind::Field { base, .. } => expr_has_any_unsupported(base),
        ExprKind::ListExpr {
            base, body, init, ..
        } => {
            expr_has_any_unsupported(base)
                || expr_has_any_unsupported(body)
                || init.as_deref().is_some_and(expr_has_any_unsupported)
        }
        ExprKind::Lit(Lit::List(values)) => values.iter().any(expr_has_any_unsupported),
        ExprKind::Lit(Lit::Object(values)) => values
            .iter()
            .any(|(_, value)| expr_has_any_unsupported(value)),
        ExprKind::Lit(_) | ExprKind::Prop(_) => false,
    }
}
