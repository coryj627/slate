// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

use rusqlite::{Connection, params};
use slate_core::{
    CancelToken, VaultError,
    bases::{
        ColumnSelection, FilterNode, GroupBy, QuerySource, RowSource, SlateQuery, SortKey,
        SummaryRef, ViewSpec,
        engine::{BasesQueryCache, BasesSummaryCell, CellValue, EngineCtx, execute},
        eval::{LinkValue, Value},
        expr::{Expr, PropertyRef, parse_expr},
    },
    db::migrate,
};

fn migrated_conn() -> Connection {
    let mut conn = Connection::open_in_memory().expect("open in-memory database");
    migrate(&mut conn).expect("migrate schema");
    conn
}

fn seed_index(conn: &Connection) {
    insert_file(conn, 1, "Projects/Alpha.md", "Alpha.md", "md", 100, 1_000);
    insert_file(conn, 2, "Projects/Beta.md", "Beta.md", "md", 200, 2_000);
    insert_file(conn, 3, "Notes/Gamma.md", "Gamma.md", "md", 300, 3_000);

    insert_text_property(conn, 1, 0, "status", "active");
    insert_number_property(conn, 1, 1, "rating", 4.5);
    insert_list_property(conn, 1, 2, "tags", &["project"]);
    insert_text_property(conn, 1, 3, "code", "01");
    insert_list_property(conn, 1, 4, "nums", &["01"]);
    insert_tag(conn, 1, "project");
    insert_tag(conn, 1, "project/rust");

    insert_text_property(conn, 2, 0, "status", "done");
    insert_number_property(conn, 2, 1, "rating", 2.0);
    insert_list_property(conn, 2, 2, "tags", &["archive"]);
    insert_tag(conn, 2, "archive");

    insert_text_property(conn, 3, 0, "status", "active");
    insert_number_property(conn, 3, 1, "rating", 5.0);
    insert_list_property(conn, 3, 2, "tags", &["project"]);
    insert_tag(conn, 3, "project");

    conn.execute(
        "INSERT INTO links (
            source_file_id, ordinal, target_path, target_raw, target_anchor,
            kind, is_embed, is_external, snippet, span_start, span_end
        )
        VALUES (?1, 0, ?2, ?3, NULL, 'wikilink', 0, 0, '', 0, 9)",
        params![1_i64, "Notes/Gamma.md", "Gamma"],
    )
    .expect("insert link");
}

fn insert_file(
    conn: &Connection,
    id: i64,
    path: &str,
    name: &str,
    extension: &str,
    size_bytes: i64,
    mtime_ms: i64,
) {
    conn.execute(
        "INSERT INTO files (
            id, path, name, extension, size_bytes, mtime_ms, ctime_ms,
            content_hash, parser_version, indexed_at_ms, is_markdown
        )
        VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, 1, ?9, 1)",
        params![
            id,
            path,
            name,
            extension,
            size_bytes,
            mtime_ms,
            mtime_ms + 10_000,
            format!("hash-{id}"),
            mtime_ms
        ],
    )
    .expect("insert file");
}

fn update_body_text(conn: &Connection, file_id: i64, body: &str) {
    conn.execute(
        "UPDATE files SET body_text = ?1 WHERE id = ?2",
        params![body, file_id],
    )
    .expect("update body text");
}

fn insert_text_property(conn: &Connection, file_id: i64, ordinal: i64, key: &str, value: &str) {
    conn.execute(
        "INSERT INTO properties (file_id, ordinal, key, value_kind, value_text, value_text_norm)
         VALUES (?1, ?2, ?3, 'text', ?4, ?5)",
        params![
            file_id,
            ordinal,
            key,
            format!("{value:?}"),
            value.to_lowercase()
        ],
    )
    .expect("insert text property");
}

fn insert_number_property(conn: &Connection, file_id: i64, ordinal: i64, key: &str, value: f64) {
    conn.execute(
        "INSERT INTO properties (file_id, ordinal, key, value_kind, value_text, value_text_norm)
         VALUES (?1, ?2, ?3, 'number', ?4, ?4)",
        params![file_id, ordinal, key, value.to_string()],
    )
    .expect("insert number property");
}

fn insert_bool_property(conn: &Connection, file_id: i64, ordinal: i64, key: &str, value: bool) {
    conn.execute(
        "INSERT INTO properties (file_id, ordinal, key, value_kind, value_text, value_text_norm)
         VALUES (?1, ?2, ?3, 'boolean', ?4, ?4)",
        params![file_id, ordinal, key, value.to_string()],
    )
    .expect("insert bool property");
}

fn insert_date_property(conn: &Connection, file_id: i64, ordinal: i64, key: &str, value: &str) {
    conn.execute(
        "INSERT INTO properties (file_id, ordinal, key, value_kind, value_text, value_text_norm)
         VALUES (?1, ?2, ?3, 'date', ?4, ?5)",
        params![file_id, ordinal, key, format!("{value:?}"), value],
    )
    .expect("insert date property");
}

fn insert_list_property(conn: &Connection, file_id: i64, ordinal: i64, key: &str, values: &[&str]) {
    let value_text = format!(
        "[{}]",
        values
            .iter()
            .map(|value| format!("{value:?}"))
            .collect::<Vec<_>>()
            .join(",")
    );
    conn.execute(
        "INSERT INTO properties (file_id, ordinal, key, value_kind, value_text, value_text_norm)
         VALUES (?1, ?2, ?3, 'tag_list', ?4, '')",
        params![file_id, ordinal, key, value_text],
    )
    .expect("insert list property");
    for value in values {
        conn.execute(
            "INSERT INTO properties_list_values (file_id, key, value_norm) VALUES (?1, ?2, ?3)",
            params![file_id, key, value.to_lowercase()],
        )
        .expect("insert list value");
    }
}

fn insert_tag(conn: &Connection, file_id: i64, tag: &str) {
    conn.execute(
        "INSERT INTO file_tags (file_id, tag_norm) VALUES (?1, ?2)",
        params![file_id, tag],
    )
    .expect("insert tag");
}

#[allow(clippy::too_many_arguments)]
fn insert_task(
    conn: &Connection,
    file_id: i64,
    ordinal: i64,
    text: &str,
    status_char: &str,
    completed: bool,
    due_ms: Option<i64>,
    scheduled_ms: Option<i64>,
    priority: Option<i64>,
) {
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
            status_char,
            completed,
            due_ms,
            scheduled_ms,
            priority,
            10 + ordinal,
            100 + ordinal
        ],
    )
    .expect("insert task");
}

fn query() -> SlateQuery {
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

fn column(id: &str) -> ColumnSelection {
    ColumnSelection {
        id: id.to_string(),
        display_name: None,
    }
}

fn expr(source: &str) -> Expr {
    parse_expr(source).expect(source)
}

fn stmt(source: &str) -> FilterNode {
    FilterNode::Stmt(expr(source))
}

fn summary_number(summary: &BasesSummaryCell) -> f64 {
    match &summary.value {
        CellValue::Value(Value::Number(value)) => *value,
        other => panic!("expected numeric summary, got {other:?}"),
    }
}

fn summary_cell<'a>(
    summaries: &'a [BasesSummaryCell],
    column_id: &str,
    summary: &str,
) -> &'a BasesSummaryCell {
    summaries
        .iter()
        .find(|cell| cell.column_id == column_id && cell.summary == summary)
        .unwrap_or_else(|| panic!("missing {column_id}/{summary} summary in {summaries:?}"))
}

#[test]
fn pushdown_and_interpreter_return_same_rows() {
    let conn = migrated_conn();
    seed_index(&conn);
    let mut query = query();
    query.filters = Some(FilterNode::And(vec![
        stmt(r#"file.inFolder("Projects")"#),
        stmt(r#"file.hasTag("missing", "project")"#),
        stmt(r#"file.ext == "md""#),
        stmt(r#"file.name.startsWith("Alpha")"#),
        stmt(r#"status == "active""#),
        stmt(r#"tags.contains("project")"#),
    ]));
    query.formulas = vec![("score".to_string(), expr("rating + 1"))];
    query.columns = vec![
        column("file.name"),
        column("status"),
        column("formula.score"),
    ];

    let cancel = CancelToken::new();
    let pushdown = execute(
        &query,
        &conn,
        &EngineCtx {
            now_ms: 10_000,
            page_size: 1,
            pushdown: true,
            ..EngineCtx::default()
        },
        &cancel,
    )
    .expect("execute with pushdown");
    let interpreted = execute(
        &query,
        &conn,
        &EngineCtx {
            now_ms: 10_000,
            page_size: 1,
            pushdown: false,
            ..EngineCtx::default()
        },
        &cancel,
    )
    .expect("execute without pushdown");

    assert_eq!(pushdown.error, None);
    assert_eq!(pushdown.rows, interpreted.rows);
    assert_eq!(pushdown.total_count, 1);
    assert_eq!(pushdown.shown_count, 1);
    assert_eq!(pushdown.rows[0].path, "Projects/Alpha.md");
    assert_eq!(
        pushdown.rows[0].cells,
        vec![
            CellValue::Value(Value::Text("Alpha.md".to_string())),
            CellValue::Value(Value::Text("active".to_string())),
            CellValue::Value(Value::Number(5.5)),
        ]
    );
}

#[test]
fn sort_limit_summaries_and_audio_use_post_filter_pre_limit_rows() {
    let conn = migrated_conn();
    seed_index(&conn);
    let mut query = query();
    query.sort = vec![SortKey {
        expr: expr("rating"),
        ascending: false,
    }];
    query.limit = Some(2);
    query.columns = vec![column("file.name"), column("rating")];
    query.summaries = vec![(
        "rating".to_string(),
        SummaryRef::Builtin("average".to_string()),
    )];

    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute sorted query");

    assert_eq!(result.error, None);
    assert_eq!(result.total_count, 3);
    assert_eq!(result.shown_count, 2);
    assert_eq!(result.unfiltered_shown_count, 2);
    assert_eq!(
        result
            .rows
            .iter()
            .map(|row| row.path.as_str())
            .collect::<Vec<_>>(),
        vec!["Notes/Gamma.md", "Projects/Alpha.md"]
    );
    assert_eq!(
        result.audio_summary,
        "3 notes, limited to 2. Sorted by rating descending."
    );
    assert_eq!(result.rows[0].audio_description, "Gamma.md. rating: 5");
    assert!((summary_number(&result.summaries[0]) - (11.5 / 3.0)).abs() < f64::EPSILON);
}

#[test]
fn quick_filter_preserves_unfiltered_shown_count_with_and_without_limit() {
    let conn = migrated_conn();
    seed_index(&conn);
    let mut query = query();
    query.columns = vec![column("file.name"), column("status")];
    query.summaries = vec![(
        "status".to_string(),
        SummaryRef::Builtin("count".to_string()),
    )];

    let unlimited = execute(
        &query,
        &conn,
        &EngineCtx {
            quick_filter: Some("Gamma"),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("execute unlimited quick filter");

    assert_eq!(unlimited.total_count, 1);
    assert_eq!(unlimited.shown_count, 1);
    assert_eq!(unlimited.unfiltered_shown_count, 3);
    assert_eq!(unlimited.rows[0].path, "Notes/Gamma.md");
    assert_eq!(summary_number(&unlimited.summaries[0]), 1.0);
    assert_eq!(unlimited.audio_summary, "1 note.");

    query.limit = Some(2);
    let limited = execute(
        &query,
        &conn,
        &EngineCtx {
            quick_filter: Some("Gamma"),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("execute limited quick filter");

    assert_eq!(limited.total_count, 1);
    assert_eq!(limited.shown_count, 1);
    assert_eq!(limited.unfiltered_shown_count, 2);
    assert_eq!(limited.rows[0].path, "Notes/Gamma.md");
    assert_eq!(summary_number(&limited.summaries[0]), 1.0);
    assert_eq!(limited.audio_summary, "1 note.");
}

#[test]
fn multi_key_sort_uses_later_keys_and_path_tiebreak() {
    let conn = migrated_conn();
    seed_index(&conn);
    let mut query = query();
    query.sort = vec![
        SortKey {
            expr: expr("status"),
            ascending: true,
        },
        SortKey {
            expr: expr("rating"),
            ascending: false,
        },
    ];
    query.columns = vec![column("status"), column("rating")];

    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute multi-key sort query");

    assert_eq!(result.error, None);
    assert_eq!(
        result
            .rows
            .iter()
            .map(|row| row.path.as_str())
            .collect::<Vec<_>>(),
        vec!["Notes/Gamma.md", "Projects/Alpha.md", "Projects/Beta.md"]
    );
}

#[test]
fn typed_sort_orders_numbers_dates_nulls_and_uses_stable_path_ties() {
    let conn = migrated_conn();
    seed_index(&conn);
    insert_file(&conn, 4, "Notes/Null.md", "Null.md", "md", 400, 4_000);
    insert_number_property(&conn, 1, 5, "score", 10.0);
    insert_number_property(&conn, 2, 3, "score", 2.0);
    insert_number_property(&conn, 3, 3, "score", 10.0);
    insert_date_property(&conn, 1, 6, "due", "2026-03-01");
    insert_date_property(&conn, 2, 4, "due", "2026-02-01");
    insert_date_property(&conn, 3, 4, "due", "2026-03-01");
    let mut query = query();
    query.columns = vec![column("file.name"), column("score"), column("due")];

    let cases = [
        (
            "score",
            true,
            vec![
                "Projects/Beta.md",
                "Notes/Gamma.md",
                "Projects/Alpha.md",
                "Notes/Null.md",
            ],
        ),
        (
            "score",
            false,
            vec![
                "Notes/Gamma.md",
                "Projects/Alpha.md",
                "Projects/Beta.md",
                "Notes/Null.md",
            ],
        ),
        (
            "due",
            true,
            vec![
                "Projects/Beta.md",
                "Notes/Gamma.md",
                "Projects/Alpha.md",
                "Notes/Null.md",
            ],
        ),
        (
            "due",
            false,
            vec![
                "Notes/Gamma.md",
                "Projects/Alpha.md",
                "Projects/Beta.md",
                "Notes/Null.md",
            ],
        ),
    ];

    for (column_id, ascending, expected) in cases {
        query.sort = vec![SortKey {
            expr: expr(column_id),
            ascending,
        }];
        let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
            .expect("execute typed sort");
        assert_eq!(
            result
                .rows
                .iter()
                .map(|row| row.path.as_str())
                .collect::<Vec<_>>(),
            expected,
            "sort {column_id} ascending={ascending}"
        );
    }
}

#[test]
fn text_sort_uses_unicode_lowercase_normalization() {
    let conn = migrated_conn();
    seed_index(&conn);
    insert_text_property(&conn, 1, 5, "category", "Zebra");
    insert_text_property(&conn, 2, 3, "category", "apple");
    insert_text_property(&conn, 3, 3, "category", "middle");
    let mut query = query();
    query.sort = vec![SortKey {
        expr: expr("category"),
        ascending: true,
    }];
    query.columns = vec![column("category")];

    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute normalized text sort query");

    assert_eq!(result.error, None);
    assert_eq!(
        result
            .rows
            .iter()
            .map(|row| row.path.as_str())
            .collect::<Vec<_>>(),
        vec!["Projects/Beta.md", "Notes/Gamma.md", "Projects/Alpha.md"]
    );
}

#[test]
fn group_by_orders_groups_keeps_sorted_rows_and_computes_group_summaries() {
    let conn = migrated_conn();
    seed_index(&conn);
    let mut query = query();
    query.group_by = Some(GroupBy {
        property: PropertyRef::Note("status".to_string()),
        ascending: true,
    });
    query.sort = vec![SortKey {
        expr: expr("rating"),
        ascending: false,
    }];
    query.columns = vec![column("status"), column("file.name"), column("rating")];
    query.summaries = vec![
        ("rating".to_string(), SummaryRef::Builtin("sum".to_string())),
        (
            "status".to_string(),
            SummaryRef::Builtin("count".to_string()),
        ),
    ];

    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute grouped query");

    assert_eq!(result.error, None);
    assert_eq!(
        result
            .rows
            .iter()
            .map(|row| row.path.as_str())
            .collect::<Vec<_>>(),
        vec!["Notes/Gamma.md", "Projects/Alpha.md", "Projects/Beta.md"]
    );
    assert_eq!(result.groups.len(), 2);
    assert_eq!(result.groups[0].label, "active");
    assert_eq!(result.groups[0].rows, 0..2);
    assert_eq!(result.groups[1].label, "done");
    assert_eq!(result.groups[1].rows, 2..3);
    assert_eq!(
        result.audio_summary,
        "3 notes, grouped by status: active 2, done 1. Sorted by rating descending."
    );
    assert!((summary_number(&result.summaries[0]) - 11.5).abs() < f64::EPSILON);
    assert!((summary_number(&result.groups[0].summaries[0]) - 9.5).abs() < f64::EPSILON);
    assert!((summary_number(&result.groups[1].summaries[0]) - 2.0).abs() < f64::EPSILON);
}

#[test]
fn grouped_audio_reports_pre_limit_group_counts() {
    let conn = migrated_conn();
    seed_index(&conn);
    let mut query = query();
    query.group_by = Some(GroupBy {
        property: PropertyRef::Note("status".to_string()),
        ascending: true,
    });
    query.sort = vec![SortKey {
        expr: expr("rating"),
        ascending: false,
    }];
    query.limit = Some(1);
    query.columns = vec![column("status"), column("file.name")];

    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute limited grouped query");

    assert_eq!(result.error, None);
    assert_eq!(result.shown_count, 1);
    assert_eq!(result.groups.len(), 1);
    assert_eq!(result.groups[0].label, "active");
    assert_eq!(result.groups[0].rows, 0..1);
    assert_eq!(
        result.audio_summary,
        "3 notes, grouped by status: active 2, done 1, limited to 1. Sorted by rating descending."
    );
}

#[test]
fn group_by_places_null_group_last() {
    let conn = migrated_conn();
    seed_index(&conn);
    let mut query = query();
    query.group_by = Some(GroupBy {
        property: PropertyRef::Note("code".to_string()),
        ascending: true,
    });
    query.sort = vec![SortKey {
        expr: expr("file.name"),
        ascending: true,
    }];
    query.columns = vec![column("file.name"), column("code")];

    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute null group query");

    assert_eq!(result.error, None);
    assert_eq!(
        result
            .groups
            .iter()
            .map(|group| group.label.as_str())
            .collect::<Vec<_>>(),
        vec!["01", "No code"]
    );
    assert_eq!(
        result
            .rows
            .iter()
            .map(|row| row.path.as_str())
            .collect::<Vec<_>>(),
        vec!["Projects/Alpha.md", "Projects/Beta.md", "Notes/Gamma.md"]
    );
}

#[test]
fn summaries_cover_defaults_dates_and_checked_aliases() {
    let conn = migrated_conn();
    seed_index(&conn);
    insert_bool_property(&conn, 1, 5, "done_flag", true);
    insert_bool_property(&conn, 2, 3, "done_flag", false);
    insert_date_property(&conn, 1, 6, "due", "2026-03-01");
    insert_date_property(&conn, 2, 4, "due", "2026-02-01");
    let mut query = query();
    query.columns = vec![
        column("status"),
        column("code"),
        column("rating"),
        column("due"),
        column("done_flag"),
    ];
    query.summaries = vec![
        (
            "status".to_string(),
            SummaryRef::Builtin("count".to_string()),
        ),
        (
            "status".to_string(),
            SummaryRef::Builtin("filled".to_string()),
        ),
        ("code".to_string(), SummaryRef::Builtin("empty".to_string())),
        (
            "status".to_string(),
            SummaryRef::Builtin("unique".to_string()),
        ),
        ("rating".to_string(), SummaryRef::Builtin("min".to_string())),
        ("rating".to_string(), SummaryRef::Builtin("max".to_string())),
        ("rating".to_string(), SummaryRef::Builtin("sum".to_string())),
        (
            "rating".to_string(),
            SummaryRef::Builtin("Average".to_string()),
        ),
        (
            "due".to_string(),
            SummaryRef::Builtin("earliest".to_string()),
        ),
        ("due".to_string(), SummaryRef::Builtin("latest".to_string())),
        (
            "done_flag".to_string(),
            SummaryRef::Builtin("checked".to_string()),
        ),
        (
            "done_flag".to_string(),
            SummaryRef::Builtin("unchecked".to_string()),
        ),
    ];

    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute summary query");

    assert_eq!(result.error, None);
    assert_eq!(
        summary_number(summary_cell(&result.summaries, "status", "count")),
        3.0
    );
    assert_eq!(
        summary_number(summary_cell(&result.summaries, "status", "filled")),
        3.0
    );
    assert_eq!(
        summary_number(summary_cell(&result.summaries, "code", "empty")),
        2.0
    );
    assert_eq!(
        summary_number(summary_cell(&result.summaries, "status", "unique")),
        2.0
    );
    assert_eq!(
        summary_number(summary_cell(&result.summaries, "rating", "min")),
        2.0
    );
    assert_eq!(
        summary_number(summary_cell(&result.summaries, "rating", "max")),
        5.0
    );
    assert_eq!(
        summary_number(summary_cell(&result.summaries, "rating", "sum")),
        11.5
    );
    assert!(
        (summary_number(summary_cell(&result.summaries, "rating", "average")) - (11.5 / 3.0)).abs()
            < f64::EPSILON
    );
    assert!(matches!(
        &summary_cell(&result.summaries, "due", "earliest").value,
        CellValue::Value(Value::Date(value)) if value.epoch_ms == 1_769_904_000_000 && !value.has_time
    ));
    assert!(matches!(
        &summary_cell(&result.summaries, "due", "latest").value,
        CellValue::Value(Value::Date(value)) if value.epoch_ms == 1_772_323_200_000 && !value.has_time
    ));
    assert_eq!(
        summary_number(summary_cell(&result.summaries, "done_flag", "checked")),
        1.0
    );
    assert_eq!(
        summary_number(summary_cell(&result.summaries, "done_flag", "unchecked")),
        1.0
    );
}

#[test]
fn inapplicable_and_unsupported_summaries_surface_correctly() {
    let conn = migrated_conn();
    seed_index(&conn);
    let mut query = query();
    query.columns = vec![column("status")];
    query.summaries = vec![("status".to_string(), SummaryRef::Builtin("sum".to_string()))];

    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute inapplicable summary query");

    assert_eq!(result.error, None);
    assert!(
        result
            .warnings
            .iter()
            .any(|warning| warning.contains("not applicable"))
    );
    assert!(matches!(
        &result.summaries[0].value,
        CellValue::Value(Value::Text(value)) if value == "—"
    ));

    query.summaries = vec![(
        "status".to_string(),
        SummaryRef::Builtin("checked".to_string()),
    )];
    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute inapplicable checked summary query");

    assert_eq!(result.error, None);
    assert!(
        result
            .warnings
            .iter()
            .any(|warning| warning.contains("not applicable"))
    );
    assert!(matches!(
        &result.summaries[0].value,
        CellValue::Value(Value::Text(value)) if value == "—"
    ));

    query.summaries = vec![(
        "status".to_string(),
        SummaryRef::Builtin("median".to_string()),
    )];
    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute unsupported summary query");
    let error = result.error.expect("unsupported summary fails loud");
    assert!(error.construct.contains("summary.median"), "{error:?}");
}

#[test]
fn custom_summary_formulas_use_values_binding() {
    let conn = migrated_conn();
    seed_index(&conn);
    let mut query = query();
    query.columns = vec![column("rating")];
    query.custom_summaries = vec![("filledCount".to_string(), expr("values.length"))];
    query.summaries = vec![(
        "rating".to_string(),
        SummaryRef::Custom("filledCount".to_string()),
    )];

    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute custom summary query");

    assert_eq!(result.error, None);
    assert_eq!(summary_number(&result.summaries[0]), 3.0);
}

#[test]
fn custom_summary_unsupported_methods_fail_loud() {
    let conn = migrated_conn();
    seed_index(&conn);
    let mut query = query();
    query.columns = vec![column("rating")];
    query.custom_summaries = vec![("bad".to_string(), expr("values.mean()"))];
    query.summaries = vec![("rating".to_string(), SummaryRef::Custom("bad".to_string()))];

    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute custom summary query");

    let error = result.error.expect("unsupported custom summary fails loud");
    assert!(error.construct.contains("summary.bad"), "{error:?}");
    assert!(error.construct.contains("mean"), "{error:?}");
}

#[test]
fn pushdown_preserves_note_coercion_and_text_contains_semantics() {
    let conn = migrated_conn();
    seed_index(&conn);

    for filter in ["code == 1", "nums.contains(1)", r#"code.contains("0")"#] {
        let mut query = query();
        query.filters = Some(stmt(filter));
        query.columns = vec![column("file.name")];
        let cancel = CancelToken::new();
        let pushdown = execute(
            &query,
            &conn,
            &EngineCtx {
                pushdown: true,
                ..EngineCtx::default()
            },
            &cancel,
        )
        .expect(filter);
        let interpreted = execute(
            &query,
            &conn,
            &EngineCtx {
                pushdown: false,
                ..EngineCtx::default()
            },
            &cancel,
        )
        .expect(filter);

        assert_eq!(pushdown.error, None, "{filter}");
        assert_eq!(pushdown.rows, interpreted.rows, "{filter}");
        assert_eq!(pushdown.rows.len(), 1, "{filter}");
        assert_eq!(pushdown.rows[0].path, "Projects/Alpha.md", "{filter}");
    }
}

#[test]
fn pushed_methods_still_fail_loud_on_invalid_arity() {
    let conn = migrated_conn();
    seed_index(&conn);

    for filter in [
        r#"file.name.startsWith("ZZZ", "extra")"#,
        r#"code.contains("missing", "extra")"#,
    ] {
        let mut query = query();
        query.filters = Some(stmt(filter));
        query.columns = vec![column("file.name")];

        let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
            .expect("execute invalid arity filter");

        let error = result.error.expect(filter);
        assert!(error.construct.contains("expected 1, got 2"), "{error:?}");
        assert!(result.rows.is_empty(), "{filter}");
    }
}

#[test]
fn ctime_columns_and_filters_are_assembled_from_files() {
    let conn = migrated_conn();
    seed_index(&conn);
    let mut query = query();
    query.filters = Some(stmt("file.ctime > 11500"));
    query.columns = vec![column("file.ctime")];

    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute ctime query");

    assert_eq!(result.error, None);
    assert_eq!(
        result
            .rows
            .iter()
            .map(|row| row.path.as_str())
            .collect::<Vec<_>>(),
        vec!["Notes/Gamma.md", "Projects/Beta.md"]
    );
    assert!(matches!(
        &result.rows[0].cells[0],
        CellValue::Value(Value::Date(date)) if date.epoch_ms == 13_000
    ));
}

#[test]
fn pushdown_preserves_file_field_numeric_coercions() {
    let conn = migrated_conn();
    seed_index(&conn);

    for filter in ["file.ctime > true", "file.mtime > true", "file.size > true"] {
        let mut query = query();
        query.filters = Some(stmt(filter));
        query.columns = vec![column("file.name")];
        let cancel = CancelToken::new();

        let pushdown = execute(
            &query,
            &conn,
            &EngineCtx {
                pushdown: true,
                ..EngineCtx::default()
            },
            &cancel,
        )
        .expect(filter);
        let interpreted = execute(
            &query,
            &conn,
            &EngineCtx {
                pushdown: false,
                ..EngineCtx::default()
            },
            &cancel,
        )
        .expect(filter);

        assert_eq!(pushdown.error, None, "{filter}");
        assert_eq!(pushdown.rows, interpreted.rows, "{filter}");
        assert_eq!(pushdown.rows.len(), 3, "{filter}");
    }
}

#[test]
fn filter_eval_errors_fail_the_view() {
    let conn = migrated_conn();
    seed_index(&conn);
    let mut query = query();
    query.filters = Some(stmt("file.hasTag(5)"));
    query.columns = vec![column("file.name")];

    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute invalid filter");

    let error = result.error.expect("filter error surfaces as view error");
    assert!(error.construct.contains("hasTag"), "{:?}", error);
    assert!(error.row_path.ends_with(".md"), "{:?}", error);
    assert!(result.rows.is_empty());
}

#[test]
fn column_formula_errors_poison_cells_without_failing_view() {
    let conn = migrated_conn();
    seed_index(&conn);
    let mut query = query();
    query.formulas = vec![("bad".to_string(), expr("random()"))];
    query.columns = vec![column("formula.bad"), column("file.name")];

    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute formula error query");

    assert_eq!(result.error, None);
    assert_eq!(result.rows.len(), 3);
    assert!(matches!(
        &result.rows[0].cells[0],
        CellValue::Error(error) if error.contains("random")
    ));
    assert!(matches!(
        &result.rows[0].cells[1],
        CellValue::Value(Value::Text(name)) if name.ends_with(".md")
    ));
}

#[test]
fn aliases_are_loaded_from_the_indexed_property() {
    let conn = migrated_conn();
    seed_index(&conn);
    insert_list_property(&conn, 2, 3, "aliases", &["B", "Beta alias"]);

    let mut query = query();
    query.source = QuerySource::Folder("Projects".to_string());
    query.columns = vec![column("file.aliases")];

    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute aliases query");
    let beta = result
        .rows
        .iter()
        .find(|row| row.path == "Projects/Beta.md")
        .expect("Beta result row");
    assert_eq!(
        beta.cells,
        vec![CellValue::Value(Value::List(vec![
            Value::Text("B".to_string()),
            Value::Text("Beta alias".to_string()),
        ]))]
    );
}

#[test]
fn links_and_embeds_are_complete_and_partitioned() {
    let conn = migrated_conn();
    seed_index(&conn);
    conn.execute(
        "INSERT INTO links (
            source_file_id, ordinal, target_path, target_raw, target_anchor,
            kind, is_embed, is_external, snippet, span_start, span_end
         )
         VALUES (2, 0, 'Notes/Target.md', 'Target', NULL, 'wikilink', 1, 0, '', 10, 19)",
        [],
    )
    .expect("insert embed");

    let mut query = query();
    query.source = QuerySource::Folder("Projects".to_string());
    query.columns = vec![
        column("file.links"),
        column("file.embeds"),
        column("file.outDegree"),
    ];

    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute aliases and embeds query");
    let alpha = result
        .rows
        .iter()
        .find(|row| row.path == "Projects/Alpha.md")
        .expect("Alpha result row");
    assert_eq!(
        alpha.cells,
        vec![
            CellValue::Value(Value::List(vec![Value::Link(LinkValue {
                target: "Gamma".to_string(),
                display: None,
                resolved_path: Some("Notes/Gamma.md".to_string()),
            })])),
            CellValue::Value(Value::List(vec![])),
            CellValue::Value(Value::Number(1.0)),
        ]
    );
    let beta = result
        .rows
        .iter()
        .find(|row| row.path == "Projects/Beta.md")
        .expect("Beta result row");
    assert_eq!(
        beta.cells,
        vec![
            CellValue::Value(Value::List(vec![])),
            CellValue::Value(Value::List(vec![Value::Link(LinkValue {
                target: "Target".to_string(),
                display: None,
                resolved_path: Some("Notes/Target.md".to_string()),
            })])),
            CellValue::Value(Value::Number(1.0)),
        ]
    );
}

#[test]
fn temporal_sources_and_custom_summaries_never_cache() {
    const DAY_MS: i64 = 86_400_000;

    let conn = migrated_conn();
    seed_index(&conn);
    let cache = BasesQueryCache::default();

    let mut recent = query();
    recent.source = QuerySource::Recent { days: 1 };
    recent.columns = vec![column("file.path")];
    let before_cutoff_transition = execute(
        &recent,
        &conn,
        &EngineCtx {
            now_ms: DAY_MS + 3_000,
            generation: 7,
            cache: Some(&cache),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("execute recent query before cutoff transition");
    let after_cutoff_transition = execute(
        &recent,
        &conn,
        &EngineCtx {
            now_ms: DAY_MS + 3_001,
            generation: 7,
            cache: Some(&cache),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("execute recent query after cutoff transition");
    assert_eq!(
        before_cutoff_transition
            .rows
            .iter()
            .map(|row| row.path.as_str())
            .collect::<Vec<_>>(),
        vec!["Notes/Gamma.md"]
    );
    assert!(after_cutoff_transition.rows.is_empty());
    assert!(!before_cutoff_transition.cache_hit);
    assert!(!after_cutoff_transition.cache_hit);

    let mut summary = query();
    summary.columns = vec![column("status")];
    summary.custom_summaries = vec![("clock".to_string(), expr("now()"))];
    summary.summaries = vec![(
        "status".to_string(),
        SummaryRef::Custom("clock".to_string()),
    )];
    let first_summary = execute(
        &summary,
        &conn,
        &EngineCtx {
            now_ms: 100,
            generation: 7,
            cache: Some(&cache),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("execute first temporal custom summary");
    let second_summary = execute(
        &summary,
        &conn,
        &EngineCtx {
            now_ms: 200,
            generation: 7,
            cache: Some(&cache),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("execute second temporal custom summary");
    assert!(matches!(
        &first_summary.summaries[0].value,
        CellValue::Value(Value::Date(date)) if date.epoch_ms == 100
    ));
    assert!(matches!(
        &second_summary.summaries[0].value,
        CellValue::Value(Value::Date(date)) if date.epoch_ms == 200
    ));
    assert!(!first_summary.cache_hit);
    assert!(!second_summary.cache_hit);
}

#[test]
fn hidden_now_summary_column_disables_cache() {
    let conn = migrated_conn();
    seed_index(&conn);
    let cache = BasesQueryCache::default();

    let mut now_summary = query();
    now_summary.columns = vec![column("file.path")];
    now_summary.summaries = vec![(
        "now()".to_string(),
        SummaryRef::Builtin("earliest".to_string()),
    )];
    let first_now = execute(
        &now_summary,
        &conn,
        &EngineCtx {
            now_ms: 100,
            generation: 7,
            cache: Some(&cache),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("execute first hidden now summary");
    let second_now = execute(
        &now_summary,
        &conn,
        &EngineCtx {
            now_ms: 200,
            generation: 7,
            cache: Some(&cache),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("execute second hidden now summary");
    assert!(matches!(
        &first_now.summaries[0].value,
        CellValue::Value(Value::Date(date)) if date.epoch_ms == 100
    ));
    assert!(matches!(
        &second_now.summaries[0].value,
        CellValue::Value(Value::Date(date)) if date.epoch_ms == 200
    ));
    assert!(!first_now.cache_hit);
    assert!(!second_now.cache_hit);
}

#[test]
fn hidden_today_summary_column_invalidates_cache_across_days() {
    const DAY_MS: i64 = 86_400_000;

    let conn = migrated_conn();
    seed_index(&conn);
    let cache = BasesQueryCache::default();

    let mut today_summary = query();
    today_summary.columns = vec![column("file.path")];
    today_summary.summaries = vec![(
        "today()".to_string(),
        SummaryRef::Builtin("earliest".to_string()),
    )];
    let first_today = execute(
        &today_summary,
        &conn,
        &EngineCtx {
            now_ms: DAY_MS * 10 + 100,
            generation: 7,
            cache: Some(&cache),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("execute first hidden today summary");
    let same_day_today = execute(
        &today_summary,
        &conn,
        &EngineCtx {
            now_ms: DAY_MS * 10 + 200,
            generation: 7,
            cache: Some(&cache),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("execute same-day hidden today summary");
    let next_day_today = execute(
        &today_summary,
        &conn,
        &EngineCtx {
            now_ms: DAY_MS * 11,
            generation: 7,
            cache: Some(&cache),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("execute next-day hidden today summary");
    assert!(matches!(
        &first_today.summaries[0].value,
        CellValue::Value(Value::Date(date)) if date.epoch_ms == DAY_MS * 10
    ));
    assert!(same_day_today.cache_hit);
    assert!(matches!(
        &next_day_today.summaries[0].value,
        CellValue::Value(Value::Date(date)) if date.epoch_ms == DAY_MS * 11
    ));
    assert!(!next_day_today.cache_hit);
}

#[test]
fn cache_keys_stable_queries_by_generation_and_today_queries_by_day() {
    let conn = migrated_conn();
    seed_index(&conn);
    let cache = BasesQueryCache::default();
    let mut stable_query = query();
    stable_query.columns = vec![column("file.name")];

    let first = execute(
        &stable_query,
        &conn,
        &EngineCtx {
            now_ms: 1_000,
            generation: 7,
            cache: Some(&cache),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("first stable query");
    let second = execute(
        &stable_query,
        &conn,
        &EngineCtx {
            now_ms: 2_000,
            generation: 7,
            cache: Some(&cache),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("second stable query");
    assert!(!first.cache_hit);
    assert!(second.cache_hit);
    assert_eq!(second.executed_at_ms, 1_000);

    let mut now_query = query();
    now_query.formulas = vec![("clock".to_string(), expr("now()"))];
    now_query.columns = vec![column("formula.clock")];
    let first_now = execute(
        &now_query,
        &conn,
        &EngineCtx {
            now_ms: 3_000,
            generation: 7,
            cache: Some(&cache),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("first now query");
    let second_now = execute(
        &now_query,
        &conn,
        &EngineCtx {
            now_ms: 4_000,
            generation: 7,
            cache: Some(&cache),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("second now query");
    assert!(!first_now.cache_hit);
    assert!(!second_now.cache_hit);

    let mut now_column_query = query();
    now_column_query.columns = vec![column("now()")];
    let first_now_column = execute(
        &now_column_query,
        &conn,
        &EngineCtx {
            now_ms: 5_000,
            generation: 7,
            cache: Some(&cache),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("first now column query");
    let second_now_column = execute(
        &now_column_query,
        &conn,
        &EngineCtx {
            now_ms: 6_000,
            generation: 7,
            cache: Some(&cache),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("second now column query");
    assert!(!first_now_column.cache_hit);
    assert!(!second_now_column.cache_hit);

    let mut today_query = query();
    today_query.formulas = vec![("day".to_string(), expr("today()"))];
    today_query.columns = vec![column("formula.day")];
    let same_day_first = execute(
        &today_query,
        &conn,
        &EngineCtx {
            now_ms: 86_400_000 * 10 + 1_000,
            generation: 7,
            cache: Some(&cache),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("first today query");
    let same_day_second = execute(
        &today_query,
        &conn,
        &EngineCtx {
            now_ms: 86_400_000 * 10 + 2_000,
            generation: 7,
            cache: Some(&cache),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("same day today query");
    let next_day = execute(
        &today_query,
        &conn,
        &EngineCtx {
            now_ms: 86_400_000 * 11,
            generation: 7,
            cache: Some(&cache),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("next day today query");
    assert!(!same_day_first.cache_hit);
    assert!(same_day_second.cache_hit);
    assert!(!next_day.cache_hit);

    let quick_first = execute(
        &stable_query,
        &conn,
        &EngineCtx {
            now_ms: 7_000,
            generation: 7,
            cache: Some(&cache),
            quick_filter: Some("Alpha"),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("first quick-filtered query");
    let quick_second = execute(
        &stable_query,
        &conn,
        &EngineCtx {
            now_ms: 8_000,
            generation: 7,
            cache: Some(&cache),
            quick_filter: Some("Alpha"),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("second quick-filtered query");
    assert_eq!(quick_first.rows.len(), 1);
    assert_eq!(quick_second.rows.len(), 1);
    assert!(!quick_first.cache_hit);
    assert!(!quick_second.cache_hit);
    assert_eq!(quick_second.executed_at_ms, 8_000);
}

#[test]
fn cancellation_is_checked_before_querying() {
    let conn = migrated_conn();
    seed_index(&conn);
    let cancel = CancelToken::new();
    cancel.cancel();

    let err = execute(&query(), &conn, &EngineCtx::default(), &cancel).expect_err("cancelled");
    assert!(matches!(err, VaultError::Cancelled));
}

#[test]
fn linked_source_depth_one_limits_candidates_and_later_depths_fail_loudly() {
    let conn = migrated_conn();
    seed_index(&conn);
    let mut linked = query();
    linked.source = QuerySource::Linked {
        from_path: "Projects/Alpha.md".to_string(),
        depth: 1,
    };
    linked.columns = vec![column("file.path")];

    let result = execute(&linked, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute linked source");

    assert_eq!(result.error, None);
    assert_eq!(result.rows.len(), 1);
    assert_eq!(result.rows[0].path, "Notes/Gamma.md");

    linked.source = QuerySource::Linked {
        from_path: "Projects/Alpha.md".to_string(),
        depth: 2,
    };
    let result = execute(&linked, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute unsupported linked depth");

    let error = result.error.expect("unsupported depth is a view error");
    assert!(error.construct.contains("depth 2"), "{:?}", error);
    assert!(result.rows.is_empty());
}

#[test]
fn linked_source_preserves_exact_public_path_over_contextual_collision() {
    let conn = migrated_conn();
    for (id, path) in [
        (1_i64, "Hub.md"),
        (2, "Notes/Hub.md"),
        (3, "RootTarget.md"),
        (4, "Notes/Target.md"),
        (5, "Notes/View.base"),
    ] {
        let (name, extension) = path.rsplit_once('.').expect("fixture extension");
        insert_file(&conn, id, path, name, extension, 0, 0);
    }
    for (source_file_id, target_path) in [(1_i64, "RootTarget.md"), (2_i64, "Notes/Target.md")] {
        conn.execute(
            "INSERT INTO links (
                source_file_id, ordinal, target_path, target_raw, target_anchor,
                kind, is_embed, is_external, snippet, span_start, span_end
             )
             VALUES (?1, 0, ?2, ?2, NULL, 'wikilink', 0, 0, '', 0, 10)",
            params![source_file_id, target_path],
        )
        .expect("insert colliding linked-source fixture");
    }

    let mut linked = query();
    linked.source = QuerySource::Linked {
        from_path: "Hub.md".to_string(),
        depth: 1,
    };
    linked.columns = vec![column("file.path")];

    let result = execute(
        &linked,
        &conn,
        &EngineCtx {
            this_path: Some("Notes/View.base".to_string()),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("execute exact canonical linked source");

    assert_eq!(result.error, None);
    assert_eq!(
        result
            .rows
            .iter()
            .map(|row| row.path.as_str())
            .collect::<Vec<_>>(),
        ["RootTarget.md"]
    );

    conn.execute("DELETE FROM files WHERE path = 'Hub.md'", [])
        .expect("remove exact canonical linked source");
    let missing = execute(
        &linked,
        &conn,
        &EngineCtx {
            this_path: Some("Notes/View.base".to_string()),
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("execute missing canonical linked source");

    assert_eq!(missing.error, None);
    assert!(
        missing.rows.is_empty(),
        "missing canonical Hub.md must not retarget to Notes/Hub.md: {:?}",
        missing.rows
    );
}

#[test]
fn folder_source_is_case_sensitive_and_handles_root_folder() {
    let conn = migrated_conn();
    seed_index(&conn);
    let mut folder = query();
    folder.source = QuerySource::Folder("projects".to_string());
    folder.columns = vec![column("file.path")];

    let result = execute(&folder, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute case-mismatched folder source");

    assert_eq!(result.error, None);
    assert!(result.rows.is_empty());

    insert_file(&conn, 4, "Root.md", "Root.md", "md", 50, 4_000);
    folder.source = QuerySource::Folder(String::new());
    let result = execute(&folder, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute root folder source");

    assert_eq!(result.error, None);
    assert_eq!(
        result
            .rows
            .iter()
            .map(|row| row.path.as_str())
            .collect::<Vec<_>>(),
        vec!["Root.md"]
    );
}

#[test]
fn file_task_aggregates_are_available_to_file_rows() {
    let conn = migrated_conn();
    seed_index(&conn);
    insert_task(
        &conn,
        1,
        0,
        "Ship N1",
        " ",
        false,
        Some(1_800_000_000_000),
        None,
        Some(2),
    );
    insert_task(&conn, 1, 1, "Write notes", "x", true, None, None, None);
    insert_task(&conn, 2, 0, "Archive", " ", false, None, None, None);

    let mut query = query();
    query.filters = Some(stmt("file.tasks.completed > 0"));
    query.columns = vec![
        column("file.name"),
        column("file.tasks.total"),
        column("file.tasks.completed"),
    ];
    query.summaries = vec![(
        "file.tasks.total".to_string(),
        SummaryRef::Builtin("sum".to_string()),
    )];

    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute task aggregate query");

    assert_eq!(result.error, None);
    assert_eq!(result.rows.len(), 1);
    assert_eq!(result.rows[0].path, "Projects/Alpha.md");
    assert_eq!(
        result.rows[0].cells,
        vec![
            CellValue::Value(Value::Text("Alpha.md".to_string())),
            CellValue::Value(Value::Number(2.0)),
            CellValue::Value(Value::Number(1.0)),
        ]
    );
    assert_eq!(summary_number(&result.summaries[0]), 2.0);
}

#[test]
fn tasks_source_materializes_task_fields_and_owner_file_values() {
    let conn = migrated_conn();
    seed_index(&conn);
    insert_task(
        &conn,
        1,
        0,
        "Do high value thing",
        "/",
        false,
        Some(1_800_000_000_000),
        Some(1_799_913_600_000),
        Some(5),
    );
    insert_task(&conn, 1, 1, "Already done", "x", true, None, None, Some(1));
    insert_task(
        &conn,
        2,
        0,
        "Archived work",
        " ",
        false,
        None,
        None,
        Some(9),
    );
    insert_task(&conn, 3, 0, "Follow up", " ", false, None, None, Some(3));

    let mut query = query();
    query.row_source = RowSource::Tasks;
    query.filters = Some(FilterNode::And(vec![
        stmt(r#"task.file.hasTag("project")"#),
        stmt("!task.completed"),
    ]));
    query.sort = vec![SortKey {
        expr: expr("task.priority"),
        ascending: false,
    }];
    query.columns = vec![
        column("task.text"),
        column("task.status"),
        column("task.completed"),
        column("task.due"),
        column("task.scheduled"),
        column("task.priority"),
        column("task.file.name"),
        column("status"),
    ];

    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute tasks source query");

    assert_eq!(result.error, None);
    assert_eq!(result.total_count, 2);
    assert_eq!(
        result
            .rows
            .iter()
            .map(|row| (row.path.as_str(), row.audio_description.as_str()))
            .collect::<Vec<_>>(),
        vec![
            (
                "Projects/Alpha.md",
                "Do high value thing. task.status: /. task.completed: false. task.due: 2027-01-15. task.scheduled: 2027-01-14. task.priority: 5. task.file.name: Alpha.md. status: active"
            ),
            (
                "Notes/Gamma.md",
                "Follow up. task.status:  . task.completed: false. task.priority: 3. task.file.name: Gamma.md. status: active"
            ),
        ]
    );
    assert_eq!(
        result.rows[0].cells,
        vec![
            CellValue::Value(Value::Text("Do high value thing".to_string())),
            CellValue::Value(Value::Text("/".to_string())),
            CellValue::Value(Value::Bool(false)),
            CellValue::Value(Value::Date(slate_core::bases::eval::DateValue {
                epoch_ms: 1_800_000_000_000,
                has_time: false,
            })),
            CellValue::Value(Value::Date(slate_core::bases::eval::DateValue {
                epoch_ms: 1_799_913_600_000,
                has_time: false,
            })),
            CellValue::Value(Value::Number(5.0)),
            CellValue::Value(Value::Text("Alpha.md".to_string())),
            CellValue::Value(Value::Text("active".to_string())),
        ]
    );
    assert_eq!(
        result.audio_summary,
        "2 tasks. Sorted by task.priority descending."
    );
}

#[test]
fn tasks_source_defaults_to_file_path_then_task_ordinal() {
    let conn = migrated_conn();
    seed_index(&conn);
    insert_task(&conn, 2, 0, "Beta zero", " ", false, None, None, None);
    insert_task(&conn, 1, 1, "Alpha one", " ", false, None, None, None);
    insert_task(&conn, 1, 0, "Alpha zero", " ", false, None, None, None);
    insert_task(&conn, 3, 0, "Gamma zero", " ", false, None, None, None);

    let mut query = query();
    query.row_source = RowSource::Tasks;
    query.columns = vec![column("task.text")];

    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute unsorted tasks query");

    assert_eq!(result.error, None);
    assert_eq!(
        result
            .rows
            .iter()
            .map(|row| (row.path.as_str(), &row.cells[0]))
            .collect::<Vec<_>>(),
        vec![
            (
                "Notes/Gamma.md",
                &CellValue::Value(Value::Text("Gamma zero".to_string()))
            ),
            (
                "Projects/Alpha.md",
                &CellValue::Value(Value::Text("Alpha zero".to_string()))
            ),
            (
                "Projects/Alpha.md",
                &CellValue::Value(Value::Text("Alpha one".to_string()))
            ),
            (
                "Projects/Beta.md",
                &CellValue::Value(Value::Text("Beta zero".to_string()))
            ),
        ]
    );
}

#[test]
fn task_field_pushdown_matches_interpreter() {
    let conn = migrated_conn();
    seed_index(&conn);
    insert_task(
        &conn,
        1,
        0,
        "Qualifies",
        " ",
        false,
        Some(1_800_000_000_000),
        Some(1_799_913_600_000),
        Some(5),
    );
    insert_task(
        &conn,
        1,
        1,
        "Too early",
        " ",
        false,
        Some(1_799_827_200_000),
        Some(1_799_913_600_000),
        Some(5),
    );
    insert_task(
        &conn,
        2,
        0,
        "Too low priority",
        " ",
        false,
        Some(1_800_000_000_000),
        Some(1_799_913_600_000),
        Some(1),
    );
    insert_task(
        &conn,
        3,
        0,
        "Completed",
        "x",
        true,
        Some(1_800_000_000_000),
        Some(1_799_913_600_000),
        Some(5),
    );

    let mut query = query();
    query.row_source = RowSource::Tasks;
    query.filters = Some(FilterNode::And(vec![
        stmt("task.completed == false"),
        stmt("task.priority >= 3"),
        stmt(r#"task.due >= date("2027-01-15")"#),
        stmt(r#"task.scheduled < date("2027-01-15")"#),
    ]));
    query.columns = vec![column("task.text")];

    let cancel = CancelToken::new();
    let pushdown = execute(
        &query,
        &conn,
        &EngineCtx {
            pushdown: true,
            ..EngineCtx::default()
        },
        &cancel,
    )
    .expect("execute task pushdown query");
    let interpreted = execute(
        &query,
        &conn,
        &EngineCtx {
            pushdown: false,
            ..EngineCtx::default()
        },
        &cancel,
    )
    .expect("execute interpreted task query");

    assert_eq!(pushdown.error, None);
    assert_eq!(pushdown.rows, interpreted.rows);
    assert_eq!(pushdown.rows.len(), 1);
    assert_eq!(
        pushdown.rows[0].cells,
        vec![CellValue::Value(Value::Text("Qualifies".to_string()))]
    );
}

#[test]
fn task_pushdown_inequality_matches_interpreter_for_nulls_and_typed_literals() {
    let conn = migrated_conn();
    seed_index(&conn);
    insert_task(&conn, 1, 0, "Null fields", " ", false, None, None, None);
    insert_task(
        &conn,
        1,
        1,
        "Exact values",
        " ",
        false,
        Some(1_799_971_200_000),
        Some(1_799_884_800_000),
        Some(3),
    );
    insert_task(
        &conn,
        3,
        0,
        "Different values",
        "x",
        true,
        Some(1_800_057_600_000),
        Some(1_799_971_200_000),
        Some(4),
    );

    for (filter, expected) in [
        (
            "task.priority != 3",
            vec!["Different values", "Null fields"],
        ),
        (
            r#"task.due != date("2027-01-15")"#,
            vec!["Different values", "Null fields"],
        ),
        (
            r#"task.scheduled != date("2027-01-14")"#,
            vec!["Different values", "Null fields"],
        ),
        (
            r#"task.completed != "false""#,
            vec!["Different values", "Null fields", "Exact values"],
        ),
    ] {
        let mut query = query();
        query.row_source = RowSource::Tasks;
        query.filters = Some(stmt(filter));
        query.columns = vec![column("task.text")];
        let cancel = CancelToken::new();

        let pushdown = execute(
            &query,
            &conn,
            &EngineCtx {
                pushdown: true,
                ..EngineCtx::default()
            },
            &cancel,
        )
        .expect(filter);
        let interpreted = execute(
            &query,
            &conn,
            &EngineCtx {
                pushdown: false,
                ..EngineCtx::default()
            },
            &cancel,
        )
        .expect(filter);

        assert_eq!(pushdown.error, None, "{filter}");
        assert_eq!(pushdown.rows, interpreted.rows, "{filter}");
        assert_eq!(
            pushdown
                .rows
                .iter()
                .map(|row| match &row.cells[0] {
                    CellValue::Value(Value::Text(text)) => text.as_str(),
                    other => panic!("expected task text cell for {filter}, got {other:?}"),
                })
                .collect::<Vec<_>>(),
            expected,
            "{filter}"
        );
    }
}

#[test]
fn file_matches_composes_with_structured_filters() {
    let conn = migrated_conn();
    seed_index(&conn);
    update_body_text(&conn, 1, "The roadmap lives in Alpha.");
    update_body_text(&conn, 2, "Archived roadmap notes.");
    update_body_text(&conn, 3, "Gamma has no matching term.");

    let mut query = query();
    query.filters = Some(FilterNode::And(vec![
        stmt(r#"file.inFolder("Projects")"#),
        stmt(r#"file.matches("roadmap")"#),
        stmt(r#"status == "active""#),
    ]));
    query.columns = vec![column("file.path")];

    let cancel = CancelToken::new();
    let pushdown = execute(
        &query,
        &conn,
        &EngineCtx {
            pushdown: true,
            ..EngineCtx::default()
        },
        &cancel,
    )
    .expect("execute FTS pushdown query");
    let interpreted = execute(
        &query,
        &conn,
        &EngineCtx {
            pushdown: false,
            ..EngineCtx::default()
        },
        &cancel,
    )
    .expect("execute interpreted FTS query");

    assert_eq!(pushdown.error, None);
    assert_eq!(pushdown.rows, interpreted.rows);
    assert_eq!(pushdown.total_count, 1);
    assert_eq!(pushdown.rows[0].path, "Projects/Alpha.md");
}

#[test]
fn file_matches_pushdown_materializes_membership_for_paged_query() {
    let conn = migrated_conn();
    seed_index(&conn);
    update_body_text(&conn, 1, "roadmap alpha");
    update_body_text(&conn, 2, "roadmap beta");
    update_body_text(&conn, 3, "roadmap gamma");

    let mut query = query();
    query.filters = Some(stmt(r#"file.matches("roadmap")"#));
    query.columns = vec![column("file.path")];

    let cancel = CancelToken::new();
    let pushdown = execute(
        &query,
        &conn,
        &EngineCtx {
            page_size: 1,
            pushdown: true,
            ..EngineCtx::default()
        },
        &cancel,
    )
    .expect("execute paged FTS pushdown query");
    let interpreted = execute(
        &query,
        &conn,
        &EngineCtx {
            page_size: 1,
            pushdown: false,
            ..EngineCtx::default()
        },
        &cancel,
    )
    .expect("execute paged interpreted FTS query");

    assert_eq!(pushdown.error, None);
    assert_eq!(pushdown.rows, interpreted.rows);
    assert_eq!(pushdown.total_count, 3);

    let mut stmt = conn
        .prepare(
            "SELECT path
             FROM temp.slate_bases_fts_matches
             WHERE query = 'roadmap'
             ORDER BY path",
        )
        .expect("prepare FTS membership inspection");
    let paths = stmt
        .query_map([], |row| row.get::<_, String>(0))
        .expect("query FTS membership")
        .collect::<Result<Vec<_>, _>>()
        .expect("collect FTS membership");
    assert_eq!(
        paths,
        vec![
            "Notes/Gamma.md".to_string(),
            "Projects/Alpha.md".to_string(),
            "Projects/Beta.md".to_string()
        ]
    );
}

#[test]
fn file_matches_temp_membership_clears_between_executions() {
    let conn = migrated_conn();
    seed_index(&conn);
    update_body_text(&conn, 1, "alpha token");
    update_body_text(&conn, 2, "beta token");
    update_body_text(&conn, 3, "alpha token");

    let mut first_query = query();
    first_query.filters = Some(stmt(r#"file.matches("alpha")"#));
    first_query.columns = vec![column("file.path")];

    let mut second_query = query();
    second_query.filters = Some(stmt(r#"file.matches("beta")"#));
    second_query.columns = vec![column("file.path")];

    let ctx = EngineCtx {
        pushdown: true,
        ..EngineCtx::default()
    };
    let first =
        execute(&first_query, &conn, &ctx, &CancelToken::new()).expect("execute first FTS query");
    let second =
        execute(&second_query, &conn, &ctx, &CancelToken::new()).expect("execute second FTS query");

    assert_eq!(first.error, None);
    assert_eq!(first.total_count, 2);
    assert_eq!(second.error, None);
    assert_eq!(second.total_count, 1);
    assert_eq!(second.rows[0].path, "Projects/Beta.md");

    let temp_queries = conn
        .prepare(
            "SELECT DISTINCT query
             FROM temp.slate_bases_fts_matches
             ORDER BY query",
        )
        .expect("prepare temp query inspection")
        .query_map([], |row| row.get::<_, String>(0))
        .expect("query temp membership")
        .collect::<Result<Vec<_>, _>>()
        .expect("collect temp membership");
    assert_eq!(temp_queries, vec!["beta".to_string()]);
}

#[test]
fn file_matches_multiple_and_clauses_intersect() {
    let conn = migrated_conn();
    seed_index(&conn);
    update_body_text(&conn, 1, "alpha beta");
    update_body_text(&conn, 2, "alpha only");
    update_body_text(&conn, 3, "beta only");

    let mut query = query();
    query.filters = Some(FilterNode::And(vec![
        stmt(r#"file.matches("alpha")"#),
        stmt(r#"file.matches("beta")"#),
    ]));
    query.columns = vec![column("file.path")];

    let cancel = CancelToken::new();
    let pushdown = execute(
        &query,
        &conn,
        &EngineCtx {
            pushdown: true,
            ..EngineCtx::default()
        },
        &cancel,
    )
    .expect("execute multi-FTS pushdown query");
    let interpreted = execute(
        &query,
        &conn,
        &EngineCtx {
            pushdown: false,
            ..EngineCtx::default()
        },
        &cancel,
    )
    .expect("execute multi-FTS interpreted query");

    assert_eq!(pushdown.error, None);
    assert_eq!(pushdown.rows, interpreted.rows);
    assert_eq!(pushdown.total_count, 1);
    assert_eq!(pushdown.rows[0].path, "Projects/Alpha.md");
}

#[test]
fn file_matches_nested_or_composes_with_top_level_pushdown() {
    let conn = migrated_conn();
    seed_index(&conn);
    update_body_text(&conn, 1, "roadmap alpha");
    update_body_text(&conn, 2, "roadmap beta");
    update_body_text(&conn, 3, "alpha outside the plan");

    let mut query = query();
    query.filters = Some(FilterNode::And(vec![
        stmt(r#"file.matches("roadmap")"#),
        FilterNode::Or(vec![
            stmt(r#"file.matches("alpha")"#),
            stmt(r#"status == "done""#),
        ]),
    ]));
    query.columns = vec![column("file.path")];

    let cancel = CancelToken::new();
    let pushdown = execute(
        &query,
        &conn,
        &EngineCtx {
            pushdown: true,
            ..EngineCtx::default()
        },
        &cancel,
    )
    .expect("execute mixed FTS pushdown query");
    let interpreted = execute(
        &query,
        &conn,
        &EngineCtx {
            pushdown: false,
            ..EngineCtx::default()
        },
        &cancel,
    )
    .expect("execute mixed FTS interpreted query");

    assert_eq!(pushdown.error, None);
    assert_eq!(pushdown.rows, interpreted.rows);
    assert_eq!(
        pushdown
            .rows
            .iter()
            .map(|row| row.path.as_str())
            .collect::<Vec<_>>(),
        vec!["Projects/Alpha.md", "Projects/Beta.md"]
    );
}

#[test]
fn file_matches_evaluates_against_owner_file_in_task_views() {
    let conn = migrated_conn();
    seed_index(&conn);
    update_body_text(&conn, 1, "Alpha mentions roadmap.");
    update_body_text(&conn, 2, "Beta does not.");
    update_body_text(&conn, 3, "Gamma does not.");
    insert_task(&conn, 1, 0, "Alpha task", " ", false, None, None, Some(1));
    insert_task(&conn, 2, 0, "Beta task", " ", false, None, None, Some(9));
    insert_task(&conn, 3, 0, "Gamma task", " ", false, None, None, Some(1));

    let mut query = query();
    query.row_source = RowSource::Tasks;
    query.filters = Some(FilterNode::Or(vec![
        stmt(r#"file.matches("roadmap")"#),
        stmt("task.priority == 9"),
    ]));
    query.columns = vec![column("task.text")];

    let cancel = CancelToken::new();
    let pushdown = execute(
        &query,
        &conn,
        &EngineCtx {
            pushdown: true,
            ..EngineCtx::default()
        },
        &cancel,
    )
    .expect("execute task FTS query");
    let interpreted = execute(
        &query,
        &conn,
        &EngineCtx {
            pushdown: false,
            ..EngineCtx::default()
        },
        &cancel,
    )
    .expect("execute interpreted task FTS query");

    assert_eq!(pushdown.error, None);
    assert_eq!(pushdown.rows, interpreted.rows);
    assert_eq!(
        pushdown
            .rows
            .iter()
            .map(|row| match &row.cells[0] {
                CellValue::Value(Value::Text(text)) => text.as_str(),
                other => panic!("expected task text, got {other:?}"),
            })
            .collect::<Vec<_>>(),
        vec!["Alpha task", "Beta task"]
    );
}

#[test]
fn file_matches_empty_query_warns_and_matches_nothing() {
    let conn = migrated_conn();
    seed_index(&conn);
    update_body_text(&conn, 1, "roadmap");

    let mut query = query();
    query.filters = Some(stmt(r#"file.matches("   ")"#));
    query.columns = vec![column("file.path")];

    let result = execute(
        &query,
        &conn,
        &EngineCtx {
            pushdown: true,
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("execute empty FTS query");

    assert_eq!(result.error, None);
    assert!(result.rows.is_empty());
    assert!(
        result
            .warnings
            .iter()
            .any(|warning| warning.contains("file.matches empty query")),
        "{:?}",
        result.warnings
    );
}

#[test]
fn file_matches_invalid_query_fails_view_with_search_error() {
    let conn = migrated_conn();
    seed_index(&conn);
    update_body_text(&conn, 1, "roadmap");

    let mut query = query();
    query.filters = Some(stmt(r#"file.matches("unbalanced\"")"#));
    query.columns = vec![column("file.path")];

    let result = execute(
        &query,
        &conn,
        &EngineCtx {
            pushdown: true,
            ..EngineCtx::default()
        },
        &CancelToken::new(),
    )
    .expect("execute invalid FTS query");

    let error = result.error.expect("invalid FTS query should fail view");
    assert!(
        error.construct.contains("file.matches")
            && error.construct.contains("invalid search query")
            && error.construct.contains("unbalanced"),
        "{error:?}"
    );
}

#[test]
fn file_matches_outside_filter_position_fails_loud() {
    let conn = migrated_conn();
    seed_index(&conn);
    update_body_text(&conn, 1, "roadmap");

    let mut query = query();
    query.columns = vec![column(r#"file.matches("roadmap")"#)];

    let result = execute(&query, &conn, &EngineCtx::default(), &CancelToken::new())
        .expect("execute file.matches column query");

    assert_eq!(result.error, None);
    assert!(matches!(
        &result.rows[0].cells[0],
        CellValue::Error(error) if error.contains("file.matches") && error.contains("filter position")
    ));
}
