// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

use rusqlite::{Connection, params};
use slate_core::{
    CancelToken, VaultError,
    bases::{
        ColumnSelection, FilterNode, QuerySource, RowSource, SlateQuery, ViewSpec,
        engine::{BasesQueryCache, CellValue, EngineCtx, execute},
        eval::Value,
        expr::{Expr, parse_expr},
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

fn query() -> SlateQuery {
    SlateQuery {
        source: QuerySource::All,
        row_source: RowSource::Files,
        filters: None,
        formulas: Vec::new(),
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
