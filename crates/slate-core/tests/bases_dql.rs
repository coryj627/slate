// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

use slate_core::bases::dql::{DqlWarningKind, parse_dql};
use slate_core::bases::expr::{BinaryOp, Expr, ExprKind, PropertyRef, TaskField};
use slate_core::bases::{FilterNode, QuerySource, RowSource, ViewSpec};

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
    assert!(filter_contains_task_status_eq_x(
        query.filters.as_ref().expect("task filters")
    ));
    assert!(matches!(
        query.sort[0].expr.kind,
        ExprKind::Prop(PropertyRef::TaskField(TaskField::Status))
    ));
}

#[test]
fn from_sources_support_outgoing_and_boolean_negation() {
    let (linked, linked_warnings) = parse_dql("LIST\nFROM outgoing([[Hub]])\n");
    let (filtered, filtered_warnings) = parse_dql("LIST\nFROM [[Inbox]] or !#done\n");
    let (nested, nested_warnings) = parse_dql("LIST\nFROM (#project and [[Hub]])\n");
    let (invalid, _invalid_warnings) = parse_dql("LIST\nFROM #tag trailing\n");

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
        r#"TABLE sum(file.size) AS "Total", average(file.size) AS "Average", string(file.name) AS "Name", array(file.name) AS "Names", embed(file.path) AS "Embed"
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
WHERE link("Note").linksTo(file.file)
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

fn filter_contains_task_status_eq_x(filter: &FilterNode) -> bool {
    match filter {
        FilterNode::Stmt(expr) => expr_contains_task_status_eq_x(expr),
        FilterNode::And(nodes) | FilterNode::Or(nodes) | FilterNode::Not(nodes) => {
            nodes.iter().any(filter_contains_task_status_eq_x)
        }
    }
}

fn expr_contains_task_status_eq_x(expr: &Expr) -> bool {
    match &expr.kind {
        ExprKind::Binary { op, lhs, rhs } if *op == BinaryOp::Eq => {
            matches!(
                lhs.kind,
                ExprKind::Prop(PropertyRef::TaskField(TaskField::Status))
            ) && matches!(rhs.kind, ExprKind::Lit(slate_core::bases::expr::Lit::String(ref s)) if s == "x")
        }
        ExprKind::Unary { rhs, .. } => expr_contains_task_status_eq_x(rhs),
        ExprKind::Binary { lhs, rhs, .. } => {
            expr_contains_task_status_eq_x(lhs) || expr_contains_task_status_eq_x(rhs)
        }
        ExprKind::Call { args, .. } => args.iter().any(expr_contains_task_status_eq_x),
        ExprKind::Index { base, index } => {
            expr_contains_task_status_eq_x(base) || expr_contains_task_status_eq_x(index)
        }
        ExprKind::Field { base, .. } => expr_contains_task_status_eq_x(base),
        ExprKind::ListExpr {
            base, body, init, ..
        } => {
            expr_contains_task_status_eq_x(base)
                || expr_contains_task_status_eq_x(body)
                || init.as_deref().is_some_and(expr_contains_task_status_eq_x)
        }
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
        ExprKind::Call { args, .. } => args
            .iter()
            .any(|arg| expr_has_unsupported_reason(arg, reason)),
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
        ExprKind::Call { args, .. } => args.iter().any(expr_has_any_unsupported),
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
        ExprKind::Lit(_) | ExprKind::Prop(_) => false,
    }
}
