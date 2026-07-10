// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

use std::collections::BTreeSet;

use rusqlite::{Connection, params};
use serde::Deserialize;
use slate_core::CancelToken;
use slate_core::bases::dql::{DqlWarningKind, parse_dql};
use slate_core::bases::engine::{CellValue, EngineCtx, execute};
use slate_core::bases::eval::Value;
use slate_core::bases::expr::{BinaryOp, Callee, Expr, ExprKind, Lit, PropertyRef, TaskField};
use slate_core::bases::{FilterNode, QuerySource, RowSource, ViewSpec, parse_base, view_query};
use slate_core::db::migrate;

const OUTGOING_DQL: &str = include_str!("fixtures/dql/outgoing.dql");
const FUNCTIONS_DQL: &str = include_str!("fixtures/dql/functions.dql");
const CLOSURE_CORPUS_JSON: &str = include_str!("fixtures/dql/closure_corpus.json");
const DQL_CENSUS_SEED: u64 = 0x4e5f_4451_4c5f_7631;

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
    assert!(matches!(
        receiver.kind,
        ExprKind::Lit(Lit::Regex {
            ref pattern,
            ref flags,
        }) if pattern == "^foo" && flags.is_empty()
    ));

    let result = execute_dql(&conn, FUNCTIONS_DQL, None);
    assert_eq!(first_value(&result, 0), &Value::Bool(true));
    assert_eq!(result.error, None);
}

#[test]
fn dql_user_marker_shaped_string_is_not_promoted_to_regex() {
    let old_marker = format!("{}slate-dql-regex:^foo{}", '\u{f8ff}', '\u{f8fe}');
    let source = format!(
        "TABLE WITHOUT ID \"{old_marker}\".matches(\"foobar\") AS \"Authored\", regextest(\"^foo\", \"foobar\") AS \"Synthesized\"\n"
    );
    let (query, warnings) = parse_dql(&source);

    assert_eq!(warnings, []);
    let ExprKind::Call {
        callee: Callee::Method { receiver, .. },
        ..
    } = &query.formulas[0].1.kind
    else {
        panic!("authored marker should remain a string method receiver");
    };
    assert!(matches!(
        &receiver.kind,
        ExprKind::Lit(Lit::String(value)) if value == &old_marker
    ));
    assert_eq!(regex_pattern(&query.formulas[1].1), "^foo");

    let conn = dql_fixture_conn();
    let result = execute_dql(&conn, &source, None);
    assert_eq!(first_value(&result, 0), &Value::Null);
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
        [r"\d+", "a/b", "a\"b", r"\\", "\n", "\r", "\t"]
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
fn dql_nonliteral_regex_pattern_fails_loudly() {
    let (query, warnings) =
        parse_dql("TABLE WITHOUT ID regextest(pattern, file.name) AS \"Match\"\n");

    assert!(warnings.iter().any(|warning| {
        warning.kind == DqlWarningKind::UnsupportedConstruct
            && warning.message.contains("literal regex pattern")
    }));
    assert!(matches!(
        query.formulas[0].1.kind,
        ExprKind::Unsupported { .. }
    ));
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
    const PREVIOUS_SUNDAY_MS: i64 = 1_783_209_600_000;
    const MONDAY_MS: i64 = 1_783_296_000_000;
    const WEDNESDAY_MS: i64 = 1_783_468_800_000;
    const SUNDAY_MS: i64 = 1_783_814_400_000;
    const NEXT_MONDAY_MS: i64 = 1_783_900_800_000;

    let conn = dql_fixture_conn();
    for (path, mtime_ms) in [
        ("Hub.md", MONDAY_MS),
        ("Target.md", SUNDAY_MS),
        ("Other.md", PREVIOUS_SUNDAY_MS),
        ("123.md", NEXT_MONDAY_MS),
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
            now_ms: WEDNESDAY_MS,
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

#[derive(Debug, Clone, Copy, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "snake_case")]
enum GoldenDisposition {
    Supported,
    Unsupported,
}

#[derive(Debug, Deserialize)]
struct GoldenDqlCase {
    name: String,
    operation: String,
    disposition: GoldenDisposition,
    source: String,
    #[serde(default)]
    this_path: Option<String>,
    #[serde(default)]
    warning_kind: Option<String>,
    #[serde(default)]
    warning_contains: Option<String>,
    #[serde(default)]
    error_contains: Option<String>,
    #[serde(default)]
    expected_rows: Vec<String>,
    #[serde(default)]
    expected_cells: Vec<Vec<String>>,
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
    let cases: Vec<GoldenDqlCase> =
        serde_json::from_str(CLOSURE_CORPUS_JSON).expect("parse checked-in DQL closure corpus");
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

    let conn = dql_fixture_conn();
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
                assert_eq!(result.error, None, "{context}: execution failed loud");
                assert_result_rows_and_cells(
                    &result,
                    &case.expected_rows,
                    &case.expected_cells,
                    &context,
                );
            }
            GoldenDisposition::Unsupported => {
                let expected_kind = case
                    .warning_kind
                    .as_deref()
                    .unwrap_or_else(|| panic!("{context}: unsupported case lacks warning_kind"));
                let expected_warning = case.warning_contains.as_deref().unwrap_or_else(|| {
                    panic!("{context}: unsupported case lacks warning_contains")
                });
                assert!(
                    warnings.iter().any(|warning| {
                        dql_warning_kind_name(warning.kind) == expected_kind
                            && warning.message.contains(expected_warning)
                    }),
                    "{context}: expected {expected_kind} warning containing {expected_warning:?}, got {warnings:?}"
                );
                let expected_error = case
                    .error_contains
                    .as_deref()
                    .unwrap_or_else(|| panic!("{context}: unsupported case lacks error_contains"));
                assert_fail_loud(&result, expected_error, &context);
            }
        }
    }
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
                _ => (r#""\w+\s+\w+""#, format!("case{case_index} second")),
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
                        "link:123.md|-|123.md".to_string(),
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
                vec!["Hub.md#1".to_string()]
            } else {
                vec!["Hub.md#0".to_string(), "Target.md#0".to_string()]
            };
            GeneratedDqlCase {
                operation: "tasks",
                source: format!("TASK\nWHERE {predicate}\nSORT text ASC\n"),
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
        Value::Duration(value) => format!("duration:{value}"),
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
            "link:{}|{}|{}",
            value.target,
            value.display.as_deref().unwrap_or("-"),
            value.resolved_path.as_deref().unwrap_or("-")
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
    for (file_id, ordinal, text, status, completed, priority) in [
        (1_i64, 0_i64, "Open task", " ", false, 3_i64),
        (1, 1, "Done task", "x", true, 1),
        (2, 0, "Waiting task", "/", false, 2),
    ] {
        conn.execute(
            "INSERT INTO tasks (
                file_id, ordinal, text, status_char, completed, due_ms, scheduled_ms,
                priority, recurrence, line, byte_offset
             )
             VALUES (?1, ?2, ?3, ?4, ?5, NULL, NULL, ?6, NULL, ?7, ?8)",
            params![
                file_id,
                ordinal,
                text,
                status,
                completed,
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
    let ExprKind::Lit(Lit::Regex { pattern, flags }) = &receiver.kind else {
        panic!("regextest receiver should be a regex literal: {receiver:?}");
    };
    assert!(flags.is_empty());
    pattern
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
