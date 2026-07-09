// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! SQLite-backed Bases query execution.
//!
//! N1-2 owns file-row planning and execution. N1-4 adds task rows and
//! file-level task aggregates. FTS composition has its own follow-up issue, so
//! this module still fails unsupported search surfaces loudly instead of
//! approximating them.

use std::{
    cell::{Cell, RefCell},
    cmp::Ordering,
    collections::{BTreeMap, BTreeSet},
    ops::Range,
};

use chrono::{DateTime, NaiveDate, NaiveDateTime, TimeZone, Utc};
use rusqlite::{Connection, OptionalExtension, params, params_from_iter};

use super::{
    ColumnSelection, FilterNode, QuerySource, RowSource, SlateQuery, SummaryRef,
    eval::{
        DateValue, EvalCtx, EvalError, FileFields, LinkValue, ResolvedFormulas, RowContext,
        TaskRow, Value, VaultLookup, WarningSink, eval,
    },
    expr::{
        BinaryOp, Callee, Expr, ExprKind, FileField, GlobalFn, Lit, MethodName, PropertyRef,
        TaskField, UnaryOp,
    },
};
use crate::{
    CancelToken, VaultError,
    db::DbError,
    search_db::{SearchScope, full_text_search},
};

#[derive(Debug, Clone, PartialEq)]
pub struct BasesResultSet {
    pub columns: Vec<ResultColumn>,
    pub rows: Vec<BasesRow>,
    pub groups: Vec<ResultGroup>,
    pub summaries: Vec<BasesSummaryCell>,
    pub total_count: usize,
    pub shown_count: usize,
    pub warnings: Vec<String>,
    pub error: Option<ViewError>,
    pub executed_at_ms: i64,
    pub cache_hit: bool,
    pub audio_summary: String,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ResultColumn {
    pub id: String,
    pub display_name: Option<String>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct BasesRow {
    pub path: String,
    pub task_ordinal: Option<u64>,
    pub cells: Vec<CellValue>,
    pub audio_description: String,
}

#[derive(Debug, Clone, PartialEq)]
pub enum CellValue {
    Value(Value),
    Error(String),
}

#[derive(Debug, Clone, PartialEq)]
pub struct ResultGroup {
    pub key: Value,
    pub label: String,
    pub rows: Range<usize>,
    pub summaries: Vec<BasesSummaryCell>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct BasesSummaryCell {
    pub column_id: String,
    pub summary: String,
    pub value: CellValue,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ViewError {
    pub construct: String,
    pub row_path: String,
}

#[derive(Debug, Clone)]
pub struct EngineCtx<'a> {
    pub now_ms: i64,
    pub generation: u64,
    pub page_size: usize,
    pub this_path: Option<String>,
    pub cache: Option<&'a BasesQueryCache>,
    pub quick_filter: Option<&'a str>,
    pub pushdown: bool,
}

impl Default for EngineCtx<'_> {
    fn default() -> Self {
        Self {
            now_ms: 0,
            generation: 0,
            page_size: 512,
            this_path: None,
            cache: None,
            quick_filter: None,
            pushdown: true,
        }
    }
}

#[derive(Debug, Default)]
pub struct BasesQueryCache {
    entries: RefCell<BTreeMap<CacheKey, BasesResultSet>>,
}

#[derive(Debug, Clone, PartialEq, Eq, PartialOrd, Ord)]
struct CacheKey {
    query_hash: String,
    generation: u64,
    this_path: Option<String>,
    today_key: Option<i64>,
}

#[derive(Debug, Clone)]
struct Candidate {
    row: RowContext,
}

struct CandidateLoadDeps<'a> {
    fts: &'a FtsMatchCache,
    warnings: &'a WarningSink,
    cancel: &'a CancelToken,
}

#[derive(Debug)]
enum CandidateLoadError {
    Vault(VaultError),
    View(String),
}

impl From<VaultError> for CandidateLoadError {
    fn from(error: VaultError) -> Self {
        Self::Vault(error)
    }
}

impl From<DbError> for CandidateLoadError {
    fn from(error: DbError) -> Self {
        Self::Vault(error.into())
    }
}

#[derive(Debug, Clone)]
struct MaterializedRow {
    path: String,
    ordinal: Option<u64>,
    cells: Vec<CellValue>,
    summary_values: BTreeMap<String, CellValue>,
    sort_values: Vec<Value>,
    group_key: Option<Value>,
}

#[derive(Debug, Clone)]
struct GroupSlice {
    key: Value,
    label: String,
    full_range: Range<usize>,
}

#[derive(Debug, Clone)]
struct IndexedFileRow {
    id: i64,
    path: String,
    name: String,
    ext: String,
    size: u64,
    mtime_ms: i64,
    ctime_ms: i64,
}

#[derive(Debug, Clone)]
struct IndexedTaskRow {
    ordinal: u64,
    text: String,
    status: String,
    completed: bool,
    due_ms: Option<i64>,
    scheduled_ms: Option<i64>,
    priority: Option<f64>,
}

#[derive(Debug, Clone, Copy, Default)]
struct TaskCounts {
    total: u64,
    completed: u64,
}

#[derive(Debug, Default)]
struct FtsMatchCache {
    paths: RefCell<BTreeMap<String, BTreeSet<String>>>,
    sql_queries: RefCell<BTreeSet<String>>,
    temp_table_ready: Cell<bool>,
}

impl FtsMatchCache {
    fn paths(
        &self,
        conn: &Connection,
        query: &str,
        cancel: &CancelToken,
    ) -> Result<BTreeSet<String>, VaultError> {
        let query = query.trim().to_string();
        if let Some(paths) = self.paths.borrow().get(&query).cloned() {
            return Ok(paths);
        }
        let result = full_text_search(conn, &query, &SearchScope::Vault, cancel)?;
        let paths = result
            .rows
            .into_iter()
            .map(|hit| hit.path)
            .collect::<BTreeSet<_>>();
        self.paths.borrow_mut().insert(query, paths.clone());
        Ok(paths)
    }

    fn ensure_sql_membership(
        &self,
        conn: &Connection,
        query: &str,
        cancel: &CancelToken,
    ) -> Result<(), VaultError> {
        let query = query.trim().to_string();
        if self.sql_queries.borrow().contains(&query) {
            return Ok(());
        }
        let paths = self.paths(conn, &query, cancel)?;
        self.ensure_temp_table(conn)?;
        let tx = conn.unchecked_transaction()?;
        {
            let mut insert = tx.prepare_cached(
                "INSERT OR IGNORE INTO temp.slate_bases_fts_matches (query, path)
                 VALUES (?1, ?2)",
            )?;
            for path in &paths {
                if cancel.is_cancelled() {
                    return Err(VaultError::Cancelled);
                }
                insert.execute(params![&query, path])?;
            }
        }
        tx.commit()?;
        self.sql_queries.borrow_mut().insert(query);
        Ok(())
    }

    fn ensure_temp_table(&self, conn: &Connection) -> Result<(), VaultError> {
        if self.temp_table_ready.get() {
            return Ok(());
        }
        // Connection-scoped temp storage is just the SQL-visible form of this
        // execute run's FTS memo. Clear once on first use so rows never persist
        // across query executions as a semantic cache.
        conn.execute_batch(
            "CREATE TEMP TABLE IF NOT EXISTS slate_bases_fts_matches (
                query TEXT NOT NULL,
                path TEXT NOT NULL,
                PRIMARY KEY (query, path)
             ) WITHOUT ROWID;
             DELETE FROM temp.slate_bases_fts_matches;",
        )?;
        self.temp_table_ready.set(true);
        Ok(())
    }
}

#[derive(Debug, Clone, Default)]
struct FormulaEval {
    values: ResolvedFormulas,
    errors: BTreeMap<String, EvalError>,
}

#[derive(Debug, Clone, Default)]
struct SqlPlan {
    clauses: Vec<String>,
    params: Vec<String>,
}

pub fn execute(
    query: &SlateQuery,
    conn: &Connection,
    ctx: &EngineCtx<'_>,
    cancel: &CancelToken,
) -> Result<BasesResultSet, VaultError> {
    if cancel.is_cancelled() {
        return Err(VaultError::Cancelled);
    }
    if let QuerySource::Unsupported(reason) = &query.source {
        return Ok(error_result(query, ctx, reason, ""));
    }
    if let QuerySource::Linked { depth, .. } = &query.source
        && *depth != 1
    {
        return Ok(error_result(
            query,
            ctx,
            format!("linked source depth {depth} is owned by a later Bases issue"),
            "",
        ));
    }

    let cache_key = cache_key(query, ctx);
    if let (Some(cache), Some(key)) = (ctx.cache, cache_key.as_ref())
        && let Some(mut cached) = cache.entries.borrow().get(key).cloned()
    {
        cached.cache_hit = true;
        return Ok(cached);
    }

    let warnings = WarningSink::default();
    let fts = FtsMatchCache::default();
    let vault = SqlVaultLookup {
        conn,
        fts: &fts,
        cancel,
        source_path: ctx.this_path.as_deref().unwrap_or_default(),
        link_index: RefCell::new(None),
        link_resolutions: RefCell::new(BTreeMap::new()),
    };
    let mut resolved_query = None;
    if let QuerySource::Linked { from_path, depth } = &query.source
        && authored_link_path_is_extensionless(from_path)
        && let Some(resolved_path) = vault.resolve_link(from_path)
    {
        let mut canonical = query.clone();
        canonical.source = QuerySource::Linked {
            from_path: resolved_path,
            depth: *depth,
        };
        resolved_query = Some(canonical);
    }
    let query = resolved_query.as_ref().unwrap_or(query);
    let load_deps = CandidateLoadDeps {
        fts: &fts,
        warnings: &warnings,
        cancel,
    };
    let this = ctx
        .this_path
        .as_deref()
        .and_then(|path| assemble_row_for_path(conn, path).transpose())
        .transpose()?;

    let mut rows = Vec::new();
    let mut offset = 0usize;
    let page_size = ctx.page_size.max(1);
    loop {
        if cancel.is_cancelled() {
            return Err(VaultError::Cancelled);
        }
        let batch = match load_candidates(query, conn, ctx, page_size, offset, &load_deps) {
            Ok(batch) => batch,
            Err(CandidateLoadError::Vault(error)) => return Err(error),
            Err(CandidateLoadError::View(error)) => {
                return Ok(error_result(query, ctx, error, ""));
            }
        };
        if batch.is_empty() {
            break;
        }
        for candidate in batch {
            if cancel.is_cancelled() {
                return Err(VaultError::Cancelled);
            }
            let formulas =
                eval_formulas(query, &candidate.row, this.as_ref(), ctx, &vault, &warnings);
            if let Some(filters) = &query.filters {
                if let Some((name, error)) = first_formula_error(filters, &formulas.errors) {
                    return Ok(error_result(
                        query,
                        ctx,
                        format!("formula.{name}: {error}"),
                        &candidate.row.file_path,
                    ));
                }
                let mut eval_ctx = eval_ctx(
                    &candidate.row,
                    this.as_ref(),
                    &formulas.values,
                    ctx,
                    &vault,
                    &warnings,
                );
                eval_ctx.filter_position = true;
                match eval_filter_after_pushdown(filters, &eval_ctx, ctx.pushdown) {
                    Ok(true) => {}
                    Ok(false) => continue,
                    Err(EvalError::Cancelled) => return Err(VaultError::Cancelled),
                    Err(error) => {
                        return Ok(error_result(
                            query,
                            ctx,
                            error.to_string(),
                            &candidate.row.file_path,
                        ));
                    }
                }
            }
            match materialize_row(
                query,
                &candidate.row,
                &formulas,
                this.as_ref(),
                ctx,
                &vault,
                &warnings,
            ) {
                Ok(row) => rows.push(row),
                Err(error) => {
                    return Ok(error_result(query, ctx, error, &candidate.row.file_path));
                }
            }
        }
        offset += page_size;
    }

    apply_quick_filter(&mut rows, ctx.quick_filter);
    sort_rows(query, &mut rows);
    let group_slices = group_rows(query, &mut rows);
    let total_count = rows.len();
    let mut result_warnings = warnings.messages();
    let summaries = match compute_summaries(
        query,
        &rows,
        0..rows.len(),
        ctx,
        &vault,
        &warnings,
        &mut result_warnings,
    ) {
        Ok(summaries) => summaries,
        Err(error) => return Ok(error_result(query, ctx, error, "")),
    };
    let group_summaries = match group_slices
        .iter()
        .map(|group| {
            compute_summaries(
                query,
                &rows,
                group.full_range.clone(),
                ctx,
                &vault,
                &warnings,
                &mut result_warnings,
            )
        })
        .collect::<Result<Vec<_>, _>>()
    {
        Ok(summaries) => summaries,
        Err(error) => return Ok(error_result(query, ctx, error, "")),
    };
    let shown_count = query
        .limit
        .map(|limit| (limit as usize).min(total_count))
        .unwrap_or(total_count);
    let audio_summary = audio_summary(query, total_count, shown_count, &group_slices);
    let groups = visible_groups(group_slices, group_summaries, shown_count);
    let columns = result_columns(query);
    let rows = rows
        .iter()
        .take(shown_count)
        .map(|row| row_to_result(row, &columns))
        .collect::<Vec<_>>();
    let mut result = BasesResultSet {
        columns,
        shown_count,
        total_count,
        rows,
        groups,
        summaries,
        warnings: result_warnings,
        error: None,
        executed_at_ms: ctx.now_ms,
        cache_hit: false,
        audio_summary,
    };
    if let (Some(cache), Some(key)) = (ctx.cache, cache_key) {
        cache.entries.borrow_mut().insert(key, result.clone());
    }
    result.cache_hit = false;
    Ok(result)
}

fn authored_link_path_is_extensionless(path: &str) -> bool {
    !path.rsplit('/').next().unwrap_or(path).contains('.')
}

fn load_candidates(
    query: &SlateQuery,
    conn: &Connection,
    ctx: &EngineCtx<'_>,
    limit: usize,
    offset: usize,
    deps: &CandidateLoadDeps<'_>,
) -> Result<Vec<Candidate>, CandidateLoadError> {
    match query.row_source {
        RowSource::Files => load_file_candidates(query, conn, ctx, limit, offset, deps),
        RowSource::Tasks => load_task_candidates(query, conn, ctx, limit, offset, deps),
    }
}

fn load_file_candidates(
    query: &SlateQuery,
    conn: &Connection,
    ctx: &EngineCtx<'_>,
    limit: usize,
    offset: usize,
    deps: &CandidateLoadDeps<'_>,
) -> Result<Vec<Candidate>, CandidateLoadError> {
    let mut plan = SqlPlan::default();
    source_predicates(&query.source, ctx, &mut plan);
    if ctx.pushdown
        && let Some(filters) = &query.filters
    {
        for conjunct in top_level_and(filters) {
            if let Some(predicate) = pushdown_predicate(conjunct) {
                plan.clauses.push(predicate.clause);
                plan.params.extend(predicate.params);
            } else if let Some(predicate) =
                fts_pushdown_predicate(conjunct, conn, deps.fts, deps.warnings, deps.cancel)?
            {
                plan.clauses.push(predicate.sql.clause);
                plan.params.extend(predicate.sql.params);
            }
        }
    }

    let mut sql =
        "SELECT id, path, name, extension, size_bytes, mtime_ms, ctime_ms FROM files".to_string();
    if !plan.clauses.is_empty() {
        sql.push_str(" WHERE ");
        sql.push_str(&plan.clauses.join(" AND "));
    }
    sql.push_str(" ORDER BY path ASC LIMIT ? OFFSET ?");
    let limit_s = limit.to_string();
    let offset_s = offset.to_string();
    plan.params.push(limit_s);
    plan.params.push(offset_s);

    let mut stmt = conn.prepare(&sql).map_err(DbError::from)?;
    let rows = stmt
        .query_map(params_from_iter(plan.params.iter()), |row| {
            let id: i64 = row.get(0)?;
            let path: String = row.get(1)?;
            let name: String = row.get(2)?;
            let ext: Option<String> = row.get(3)?;
            let size: u64 = row.get::<_, i64>(4)?.max(0) as u64;
            let mtime_ms: i64 = row.get(5)?;
            let ctime_ms: i64 = row.get(6)?;
            Ok(IndexedFileRow {
                id,
                path,
                name,
                ext: ext.unwrap_or_default(),
                size,
                mtime_ms,
                ctime_ms,
            })
        })
        .map_err(DbError::from)?
        .collect::<Result<Vec<_>, _>>()
        .map_err(DbError::from)?;

    let file_ids = rows.iter().map(|file| file.id).collect::<Vec<_>>();
    let counts = load_task_counts(conn, &file_ids)?;
    rows.into_iter()
        .map(|file| {
            let task_counts = counts.get(&file.id).copied().unwrap_or_default();
            assemble_row(conn, &file, task_counts, None)
                .map(|row| Candidate { row })
                .map_err(CandidateLoadError::from)
        })
        .collect()
}

fn load_task_candidates(
    query: &SlateQuery,
    conn: &Connection,
    ctx: &EngineCtx<'_>,
    limit: usize,
    offset: usize,
    deps: &CandidateLoadDeps<'_>,
) -> Result<Vec<Candidate>, CandidateLoadError> {
    let mut plan = SqlPlan::default();
    source_predicates(&query.source, ctx, &mut plan);
    if ctx.pushdown
        && let Some(filters) = &query.filters
    {
        for conjunct in top_level_and(filters) {
            if let Some(predicate) =
                pushdown_predicate(conjunct).or_else(|| task_pushdown_predicate(conjunct))
            {
                plan.clauses.push(predicate.clause);
                plan.params.extend(predicate.params);
            } else if let Some(predicate) =
                fts_pushdown_predicate(conjunct, conn, deps.fts, deps.warnings, deps.cancel)?
            {
                plan.clauses.push(predicate.sql.clause);
                plan.params.extend(predicate.sql.params);
            }
        }
    }

    let mut sql = "SELECT
            f.id, f.path, f.name, f.extension, f.size_bytes, f.mtime_ms, f.ctime_ms,
            t.ordinal, t.text, t.status_char, t.completed, t.due_ms, t.scheduled_ms, t.priority
         FROM tasks t
         JOIN files f ON f.id = t.file_id"
        .to_string();
    if !plan.clauses.is_empty() {
        sql.push_str(" WHERE ");
        sql.push_str(&plan.clauses.join(" AND "));
    }
    sql.push_str(" ORDER BY f.path ASC, t.ordinal ASC LIMIT ? OFFSET ?");
    plan.params.push(limit.to_string());
    plan.params.push(offset.to_string());

    let mut stmt = conn.prepare(&sql).map_err(DbError::from)?;
    let rows = stmt
        .query_map(params_from_iter(plan.params.iter()), |row| {
            let file = IndexedFileRow {
                id: row.get::<_, i64>(0)?,
                path: row.get::<_, String>(1)?,
                name: row.get::<_, String>(2)?,
                ext: row.get::<_, Option<String>>(3)?.unwrap_or_default(),
                size: row.get::<_, i64>(4)?.max(0) as u64,
                mtime_ms: row.get::<_, i64>(5)?,
                ctime_ms: row.get::<_, i64>(6)?,
            };
            let priority = row.get::<_, Option<i64>>(13)?.map(|value| value as f64);
            let task = IndexedTaskRow {
                ordinal: row.get::<_, i64>(7)?.max(0) as u64,
                text: row.get(8)?,
                status: row.get(9)?,
                completed: row.get::<_, i64>(10)? != 0,
                due_ms: row.get(11)?,
                scheduled_ms: row.get(12)?,
                priority,
            };
            Ok((file, task))
        })
        .map_err(DbError::from)?
        .collect::<Result<Vec<_>, _>>()
        .map_err(DbError::from)?;

    let file_ids = rows.iter().map(|(file, _)| file.id).collect::<Vec<_>>();
    let counts = load_task_counts(conn, &file_ids)?;
    rows.into_iter()
        .map(|(file, task)| {
            let task_counts = counts.get(&file.id).copied().unwrap_or_default();
            assemble_row(conn, &file, task_counts, Some(task))
                .map(|row| Candidate { row })
                .map_err(CandidateLoadError::from)
        })
        .collect()
}

fn source_predicates(source: &QuerySource, ctx: &EngineCtx<'_>, plan: &mut SqlPlan) {
    match source {
        QuerySource::All | QuerySource::Unsupported(_) => {}
        QuerySource::Folder(folder) => push_folder_clause(plan, folder),
        QuerySource::Tag(tag) => push_tag_clause(plan, tag),
        QuerySource::Recent { days } => {
            let cutoff = ctx.now_ms.saturating_sub(*days as i64 * 86_400_000);
            plan.clauses.push("mtime_ms >= ?".to_string());
            plan.params.push(cutoff.to_string());
        }
        QuerySource::Linked { from_path, depth } if *depth == 1 => {
            plan.clauses.push(
                "path IN (
                    SELECT l.target_path
                    FROM links l
                    JOIN files src ON src.id = l.source_file_id
                    WHERE src.path = ? AND l.target_path IS NOT NULL
                )"
                .to_string(),
            );
            plan.params.push(from_path.clone());
        }
        QuerySource::Linked { .. } => {}
    }
}

#[derive(Debug, Clone)]
struct SqlPredicate {
    clause: String,
    params: Vec<String>,
}

#[derive(Debug, Clone)]
struct FtsSqlPredicate {
    sql: SqlPredicate,
}

fn pushdown_predicate(node: &FilterNode) -> Option<SqlPredicate> {
    let FilterNode::Stmt(expr) = node else {
        return None;
    };
    match &expr.kind {
        ExprKind::Binary { op, lhs, rhs } => pushdown_binary(*op, lhs, rhs),
        ExprKind::Call {
            callee: Callee::Method { receiver, name },
            args,
        } => pushdown_method(receiver, *name, args),
        _ => None,
    }
}

fn fts_pushdown_predicate(
    node: &FilterNode,
    conn: &Connection,
    fts: &FtsMatchCache,
    warnings: &WarningSink,
    cancel: &CancelToken,
) -> Result<Option<FtsSqlPredicate>, CandidateLoadError> {
    let FilterNode::Stmt(expr) = node else {
        return Ok(None);
    };
    let Some(query) = file_matches_query(expr) else {
        return Ok(None);
    };
    let query = query.trim();
    if query.is_empty() {
        warnings.warn("file.matches empty query matched no files");
        return Ok(Some(FtsSqlPredicate {
            sql: false_predicate(),
        }));
    }
    match fts.ensure_sql_membership(conn, query, cancel) {
        Ok(()) => {}
        Err(VaultError::Cancelled) => return Err(CandidateLoadError::Vault(VaultError::Cancelled)),
        Err(VaultError::InvalidQuery { message }) => {
            return Err(CandidateLoadError::View(format!(
                "file.matches: invalid search query: {message}"
            )));
        }
        Err(error) => return Err(CandidateLoadError::Vault(error)),
    }
    Ok(Some(FtsSqlPredicate {
        sql: file_matches_sql_predicate(query),
    }))
}

fn file_matches_query(expr: &Expr) -> Option<String> {
    let ExprKind::Call {
        callee: Callee::Method { receiver, name },
        args,
    } = &expr.kind
    else {
        return None;
    };
    if *name != MethodName::Matches || args.len() != 1 {
        return None;
    }
    if !matches!(
        receiver.kind,
        ExprKind::Prop(PropertyRef::File(FileField::File))
    ) {
        return None;
    }
    literal_text(args.first()?)
}

fn false_predicate() -> SqlPredicate {
    SqlPredicate {
        clause: "0 = 1".to_string(),
        params: Vec::new(),
    }
}

fn file_matches_sql_predicate(query: &str) -> SqlPredicate {
    SqlPredicate {
        clause: "path IN (
            SELECT path
            FROM temp.slate_bases_fts_matches
            WHERE query = ?
        )"
        .to_string(),
        params: vec![query.to_string()],
    }
}

fn pushdown_binary(op: BinaryOp, lhs: &Expr, rhs: &Expr) -> Option<SqlPredicate> {
    match (&lhs.kind, op) {
        (ExprKind::Prop(PropertyRef::File(FileField::Ext)), BinaryOp::Eq) => Some(SqlPredicate {
            clause: "extension = ?".to_string(),
            params: vec![nonnumeric_literal_text(rhs)?],
        }),
        (ExprKind::Prop(PropertyRef::File(FileField::Name)), BinaryOp::Eq) => Some(SqlPredicate {
            clause: "name = ?".to_string(),
            params: vec![nonnumeric_literal_text(rhs)?],
        }),
        (ExprKind::Prop(PropertyRef::File(FileField::Path)), BinaryOp::Eq) => Some(SqlPredicate {
            clause: "path = ?".to_string(),
            params: vec![nonnumeric_literal_text(rhs)?],
        }),
        (
            ExprKind::Prop(PropertyRef::File(FileField::Size)),
            op @ (BinaryOp::Gt | BinaryOp::Gte | BinaryOp::Lt | BinaryOp::Lte | BinaryOp::Eq),
        ) => comparison_clause("size_bytes", op, numeric_literal_string(rhs)?),
        (
            ExprKind::Prop(PropertyRef::File(FileField::Mtime)),
            op @ (BinaryOp::Gt | BinaryOp::Gte | BinaryOp::Lt | BinaryOp::Lte | BinaryOp::Eq),
        ) => comparison_clause("mtime_ms", op, numeric_literal_string(rhs)?),
        (
            ExprKind::Prop(PropertyRef::File(FileField::Ctime)),
            op @ (BinaryOp::Gt | BinaryOp::Gte | BinaryOp::Lt | BinaryOp::Lte | BinaryOp::Eq),
        ) => comparison_clause("ctime_ms", op, numeric_literal_string(rhs)?),
        (ExprKind::Prop(PropertyRef::Note(key)), BinaryOp::Eq) => {
            let lit = nonnumeric_literal_text(rhs)?;
            Some(SqlPredicate {
                clause:
                    "id IN (SELECT file_id FROM properties WHERE key = ? AND value_text_norm = ?)"
                        .to_string(),
                params: vec![key.clone(), lit.to_lowercase()],
            })
        }
        _ => None,
    }
}

fn pushdown_method(receiver: &Expr, name: MethodName, args: &[Expr]) -> Option<SqlPredicate> {
    match (&receiver.kind, name) {
        (ExprKind::Prop(PropertyRef::File(FileField::File)), MethodName::InFolder) => {
            if args.len() != 1 {
                return None;
            }
            let folder = literal_string(args.first()?)?;
            let mut plan = SqlPlan::default();
            push_folder_clause(&mut plan, &folder);
            Some(SqlPredicate {
                clause: plan.clauses.pop()?,
                params: plan.params,
            })
        }
        (ExprKind::Prop(PropertyRef::File(FileField::File)), MethodName::HasTag) => {
            let tags = args.iter().map(literal_text).collect::<Option<Vec<_>>>()?;
            tag_predicate(&tags)
        }
        (ExprKind::Prop(PropertyRef::File(FileField::Name)), MethodName::StartsWith) => {
            if args.len() != 1 {
                return None;
            }
            let prefix = literal_string(args.first()?)?;
            Some(SqlPredicate {
                clause: "substr(name, 1, length(?)) = ?".to_string(),
                params: vec![prefix.clone(), prefix],
            })
        }
        (ExprKind::Prop(PropertyRef::File(FileField::Path)), MethodName::StartsWith) => {
            if args.len() != 1 {
                return None;
            }
            let prefix = literal_string(args.first()?)?;
            Some(SqlPredicate {
                clause: "substr(path, 1, length(?)) = ?".to_string(),
                params: vec![prefix.clone(), prefix],
            })
        }
        (ExprKind::Prop(PropertyRef::Note(key)), MethodName::Contains) => {
            if args.len() != 1 {
                return None;
            }
            let lit = nonnumeric_literal_text(args.first()?)?;
            Some(SqlPredicate {
                clause: "id IN (
                    SELECT file_id FROM properties_list_values WHERE key = ? AND value_norm = ?
                    UNION
                    SELECT file_id FROM properties
                    WHERE key = ? AND value_kind = 'text' AND instr(json_extract(value_text, '$'), ?) > 0
                )"
                .to_string(),
                params: vec![key.clone(), lit.to_lowercase(), key.clone(), lit],
            })
        }
        _ => None,
    }
}

fn task_pushdown_predicate(node: &FilterNode) -> Option<SqlPredicate> {
    let FilterNode::Stmt(expr) = node else {
        return None;
    };
    match &expr.kind {
        ExprKind::Prop(PropertyRef::TaskField(TaskField::Completed)) => Some(SqlPredicate {
            clause: "t.completed != 0".to_string(),
            params: Vec::new(),
        }),
        ExprKind::Unary {
            op: UnaryOp::Not,
            rhs,
        } if matches!(
            rhs.kind,
            ExprKind::Prop(PropertyRef::TaskField(TaskField::Completed))
        ) =>
        {
            Some(SqlPredicate {
                clause: "t.completed = 0".to_string(),
                params: Vec::new(),
            })
        }
        ExprKind::Binary { op, lhs, rhs } => task_pushdown_binary(*op, lhs, rhs),
        ExprKind::Call {
            callee: Callee::Method { receiver, name },
            args,
        } => task_file_method_pushdown(receiver, *name, args),
        _ => None,
    }
}

fn task_file_method_pushdown(
    receiver: &Expr,
    name: MethodName,
    args: &[Expr],
) -> Option<SqlPredicate> {
    match (&receiver.kind, name) {
        (ExprKind::Prop(PropertyRef::TaskField(TaskField::File)), MethodName::InFolder) => {
            if args.len() != 1 {
                return None;
            }
            let folder = literal_string(args.first()?)?;
            let mut plan = SqlPlan::default();
            push_folder_clause(&mut plan, &folder);
            Some(SqlPredicate {
                clause: plan.clauses.pop()?,
                params: plan.params,
            })
        }
        (ExprKind::Prop(PropertyRef::TaskField(TaskField::File)), MethodName::HasTag) => {
            let tags = args.iter().map(literal_text).collect::<Option<Vec<_>>>()?;
            tag_predicate(&tags)
        }
        _ => None,
    }
}

fn task_pushdown_binary(op: BinaryOp, lhs: &Expr, rhs: &Expr) -> Option<SqlPredicate> {
    if let ExprKind::Prop(PropertyRef::TaskField(field)) = &lhs.kind {
        return task_field_pushdown(*field, op, rhs);
    }
    if let ExprKind::Prop(PropertyRef::TaskField(field)) = &rhs.kind {
        return task_field_pushdown(*field, reverse_binary_op(op)?, lhs);
    }
    None
}

fn reverse_binary_op(op: BinaryOp) -> Option<BinaryOp> {
    Some(match op {
        BinaryOp::Eq => BinaryOp::Eq,
        BinaryOp::Ne => BinaryOp::Ne,
        BinaryOp::Gt => BinaryOp::Lt,
        BinaryOp::Gte => BinaryOp::Lte,
        BinaryOp::Lt => BinaryOp::Gt,
        BinaryOp::Lte => BinaryOp::Gte,
        _ => return None,
    })
}

fn task_field_pushdown(field: TaskField, op: BinaryOp, value: &Expr) -> Option<SqlPredicate> {
    match field {
        TaskField::Completed => {
            task_comparison_clause("t.completed", op, bool_literal(value)?, false)
        }
        TaskField::Status => {
            task_comparison_clause("t.status_char", op, literal_text(value)?, false)
        }
        TaskField::Priority => {
            task_comparison_clause("t.priority", op, numeric_literal_string(value)?, true)
        }
        TaskField::Due => {
            task_comparison_clause("t.due_ms", op, date_literal_ms_string(value)?, true)
        }
        TaskField::Scheduled => {
            task_comparison_clause("t.scheduled_ms", op, date_literal_ms_string(value)?, true)
        }
        TaskField::Text | TaskField::File => None,
    }
}

fn task_comparison_clause(
    column: &str,
    op: BinaryOp,
    value: String,
    nullable: bool,
) -> Option<SqlPredicate> {
    let sql_op = match op {
        BinaryOp::Eq => "=",
        BinaryOp::Ne => "<>",
        BinaryOp::Gt => ">",
        BinaryOp::Gte => ">=",
        BinaryOp::Lt => "<",
        BinaryOp::Lte => "<=",
        _ => return None,
    };
    if nullable && op == BinaryOp::Ne {
        return Some(SqlPredicate {
            clause: format!("({column} {sql_op} ? OR {column} IS NULL)"),
            params: vec![value],
        });
    }
    Some(SqlPredicate {
        clause: format!("{column} {sql_op} ?"),
        params: vec![value],
    })
}

fn comparison_clause(column: &str, op: BinaryOp, value: String) -> Option<SqlPredicate> {
    let sql_op = match op {
        BinaryOp::Eq => "=",
        BinaryOp::Gt => ">",
        BinaryOp::Gte => ">=",
        BinaryOp::Lt => "<",
        BinaryOp::Lte => "<=",
        _ => return None,
    };
    Some(SqlPredicate {
        clause: format!("{column} {sql_op} ?"),
        params: vec![value],
    })
}

fn push_folder_clause(plan: &mut SqlPlan, folder: &str) {
    let folder = folder.trim_matches('/');
    if folder.is_empty() {
        plan.clauses.push("instr(path, '/') = 0".to_string());
        return;
    }
    let prefix = format!("{folder}/");
    plan.clauses
        .push("(path = ? OR substr(path, 1, length(?)) = ?)".to_string());
    plan.params.push(folder.to_string());
    plan.params.push(prefix.clone());
    plan.params.push(prefix);
}

fn push_tag_clause(plan: &mut SqlPlan, tag: &str) {
    if let Some(predicate) = tag_predicate(&[tag.to_string()]) {
        plan.clauses.push(predicate.clause);
        plan.params.extend(predicate.params);
    }
}

fn escape_like(value: &str) -> String {
    value
        .replace('\\', "\\\\")
        .replace('%', "\\%")
        .replace('_', "\\_")
}

fn top_level_and(node: &FilterNode) -> Vec<&FilterNode> {
    match node {
        FilterNode::And(nodes) => nodes.iter().collect(),
        other => vec![other],
    }
}

fn literal_string(expr: &Expr) -> Option<String> {
    match &expr.kind {
        ExprKind::Lit(Lit::String(value)) => Some(value.clone()),
        ExprKind::Lit(Lit::Number(value)) => Some(value.to_string()),
        ExprKind::Lit(Lit::Bool(value)) => Some(value.to_string()),
        _ => None,
    }
}

fn literal_text(expr: &Expr) -> Option<String> {
    match &expr.kind {
        ExprKind::Lit(Lit::String(value)) => Some(value.clone()),
        _ => None,
    }
}

fn nonnumeric_literal_text(expr: &Expr) -> Option<String> {
    let value = literal_text(expr)?;
    value.trim().parse::<f64>().is_err().then_some(value)
}

fn numeric_literal_string(expr: &Expr) -> Option<String> {
    let value = match &expr.kind {
        ExprKind::Lit(Lit::Number(value)) => *value,
        ExprKind::Lit(Lit::String(value)) => value.trim().parse().ok()?,
        _ => return None,
    };
    value.is_finite().then(|| value.to_string())
}

fn bool_literal(expr: &Expr) -> Option<String> {
    match &expr.kind {
        ExprKind::Lit(Lit::Bool(value)) => Some(if *value { "1" } else { "0" }.to_string()),
        ExprKind::Lit(Lit::Number(value)) => bool_number_literal(*value),
        ExprKind::Lit(Lit::String(value)) => value
            .trim()
            .parse::<f64>()
            .ok()
            .and_then(bool_number_literal),
        _ => None,
    }
}

fn bool_number_literal(value: f64) -> Option<String> {
    if value == 0.0 {
        Some("0".to_string())
    } else if value == 1.0 {
        Some("1".to_string())
    } else {
        None
    }
}

fn date_literal_ms_string(expr: &Expr) -> Option<String> {
    match &expr.kind {
        ExprKind::Lit(Lit::Number(_)) => numeric_literal_string(expr),
        ExprKind::Lit(Lit::String(value)) => parse_date_value(value)
            .or_else(|| parse_datetime_value(value))
            .map(|date| date.epoch_ms.to_string()),
        ExprKind::Call {
            callee: Callee::Global(GlobalFn::Date),
            args,
        } if args.len() == 1 => date_literal_ms_string(&args[0]),
        _ => None,
    }
}

fn tag_predicate(tags: &[String]) -> Option<SqlPredicate> {
    if tags.is_empty() {
        return None;
    }
    let mut clauses = Vec::with_capacity(tags.len());
    let mut params = Vec::with_capacity(tags.len() * 2);
    for tag in tags {
        let tag = tag.trim_start_matches('#').to_lowercase();
        clauses.push("(tag_norm = ? OR tag_norm LIKE ? ESCAPE '\\')".to_string());
        params.push(tag.clone());
        params.push(format!("{}/%", escape_like(&tag)));
    }
    Some(SqlPredicate {
        clause: format!(
            "id IN (
                SELECT file_id FROM file_tags
                WHERE {}
            )",
            clauses.join(" OR ")
        ),
        params,
    })
}

fn assemble_row(
    conn: &Connection,
    file: &IndexedFileRow,
    task_counts: TaskCounts,
    task: Option<IndexedTaskRow>,
) -> Result<RowContext, VaultError> {
    let mut file_fields = FileFields::for_path(&file.path);
    file_fields.name = file.name.clone();
    file_fields.ext = file.ext.clone();
    file_fields.size = file.size;
    file_fields.mtime = Some(DateValue {
        epoch_ms: file.mtime_ms,
        has_time: true,
    });
    file_fields.ctime = Some(DateValue {
        epoch_ms: file.ctime_ms,
        has_time: true,
    });
    file_fields.tags = load_tags(conn, file.id)?;
    file_fields.links = load_links(conn, file.id)?;
    file_fields.backlinks = load_backlinks(conn, &file.path)?;
    file_fields.out_degree = file_fields.links.len() as u64;
    file_fields.in_degree = file_fields.backlinks.len() as u64;
    let properties = load_properties(conn, file.id)?;
    for (key, value) in &properties {
        file_fields.properties.insert(key.clone(), value.clone());
    }
    file_fields
        .properties
        .insert("tasks".to_string(), task_counts_value(task_counts));
    let task = task.map(|task| TaskRow {
        ordinal: task.ordinal,
        text: task.text,
        status: task.status,
        completed: task.completed,
        due: task_date(task.due_ms),
        scheduled: task_date(task.scheduled_ms),
        priority: task.priority,
    });
    Ok(RowContext {
        file_path: file.path.clone(),
        file_fields,
        properties,
        task,
    })
}

fn assemble_row_for_path(conn: &Connection, path: &str) -> Result<Option<RowContext>, VaultError> {
    let row = conn
        .query_row(
            "SELECT id, path, name, extension, size_bytes, mtime_ms, ctime_ms FROM files WHERE path = ?1",
            params![path],
            |row| {
                Ok(IndexedFileRow {
                    id: row.get::<_, i64>(0)?,
                    path: row.get::<_, String>(1)?,
                    name: row.get::<_, String>(2)?,
                    ext: row.get::<_, Option<String>>(3)?.unwrap_or_default(),
                    size: row.get::<_, i64>(4)?.max(0) as u64,
                    mtime_ms: row.get::<_, i64>(5)?,
                    ctime_ms: row.get::<_, i64>(6)?,
                })
            },
        )
        .optional()
        .map_err(DbError::from)?;
    row.map(|file| {
        let counts = load_task_counts(conn, &[file.id])?;
        let task_counts = counts.get(&file.id).copied().unwrap_or_default();
        assemble_row(conn, &file, task_counts, None)
    })
    .transpose()
}

fn task_counts_value(counts: TaskCounts) -> Value {
    Value::Object(BTreeMap::from([
        ("total".to_string(), Value::Number(counts.total as f64)),
        (
            "completed".to_string(),
            Value::Number(counts.completed as f64),
        ),
    ]))
}

fn task_date(epoch_ms: Option<i64>) -> Option<DateValue> {
    epoch_ms.map(|epoch_ms| DateValue {
        epoch_ms,
        has_time: false,
    })
}

fn load_task_counts(
    conn: &Connection,
    file_ids: &[i64],
) -> Result<BTreeMap<i64, TaskCounts>, VaultError> {
    if file_ids.is_empty() {
        return Ok(BTreeMap::new());
    }
    let mut ids = file_ids.to_vec();
    ids.sort_unstable();
    ids.dedup();
    let placeholders = std::iter::repeat_n("?", ids.len())
        .collect::<Vec<_>>()
        .join(", ");
    let sql = format!(
        "SELECT file_id, COUNT(*), COALESCE(SUM(CASE WHEN completed != 0 THEN 1 ELSE 0 END), 0)
         FROM tasks
         WHERE file_id IN ({placeholders})
         GROUP BY file_id"
    );
    let mut stmt = conn.prepare(&sql).map_err(DbError::from)?;
    stmt.query_map(params_from_iter(ids.iter()), |row| {
        Ok((
            row.get::<_, i64>(0)?,
            TaskCounts {
                total: row.get::<_, i64>(1)?.max(0) as u64,
                completed: row.get::<_, i64>(2)?.max(0) as u64,
            },
        ))
    })
    .map_err(DbError::from)?
    .collect::<Result<BTreeMap<_, _>, _>>()
    .map_err(|err| DbError::from(err).into())
}

fn load_properties(conn: &Connection, file_id: i64) -> Result<Vec<(String, Value)>, VaultError> {
    let mut stmt = conn
        .prepare(
            "SELECT key, value_kind, value_text
             FROM properties
             WHERE file_id = ?1
             ORDER BY ordinal",
        )
        .map_err(DbError::from)?;
    stmt.query_map(params![file_id], |row| {
        Ok((
            row.get::<_, String>(0)?,
            row.get::<_, String>(1)?,
            row.get::<_, String>(2)?,
        ))
    })
    .map_err(DbError::from)?
    .map(|row| {
        let (key, kind, value_text) = row.map_err(DbError::from)?;
        Ok((key, decode_property_value(&kind, &value_text)))
    })
    .collect()
}

fn decode_property_value(kind: &str, value_text: &str) -> Value {
    let json: serde_json::Value =
        serde_json::from_str(value_text).unwrap_or(serde_json::Value::Null);
    match kind {
        "text" => json
            .as_str()
            .map(|s| Value::Text(s.to_string()))
            .unwrap_or(Value::Null),
        "number" => json.as_f64().map(Value::Number).unwrap_or(Value::Null),
        "boolean" => json.as_bool().map(Value::Bool).unwrap_or(Value::Null),
        "date" => json
            .as_str()
            .and_then(parse_date_value)
            .map(Value::Date)
            .unwrap_or(Value::Null),
        "datetime" => json
            .as_str()
            .and_then(parse_datetime_value)
            .map(Value::Date)
            .unwrap_or(Value::Null),
        "wikilink" => json
            .as_str()
            .map(|target| {
                Value::Link(LinkValue {
                    target: target.to_string(),
                    display: None,
                    resolved_path: None,
                })
            })
            .unwrap_or(Value::Null),
        "list" | "tag_list" => Value::List(
            json.as_array()
                .map(|items| items.iter().map(json_to_value).collect())
                .unwrap_or_default(),
        ),
        _ => Value::Null,
    }
}

fn json_to_value(value: &serde_json::Value) -> Value {
    match value {
        serde_json::Value::Null => Value::Null,
        serde_json::Value::Bool(value) => Value::Bool(*value),
        serde_json::Value::Number(value) => {
            value.as_f64().map(Value::Number).unwrap_or(Value::Null)
        }
        serde_json::Value::String(value) => Value::Text(value.clone()),
        serde_json::Value::Array(items) => Value::List(items.iter().map(json_to_value).collect()),
        serde_json::Value::Object(map) => Value::Object(
            map.iter()
                .map(|(key, value)| (key.clone(), json_to_value(value)))
                .collect(),
        ),
    }
}

fn parse_date_value(text: &str) -> Option<DateValue> {
    let date = NaiveDate::parse_from_str(text, "%Y-%m-%d").ok()?;
    let dt = Utc.from_utc_datetime(&date.and_hms_milli_opt(0, 0, 0, 0)?);
    Some(DateValue {
        epoch_ms: dt.timestamp_millis(),
        has_time: false,
    })
}

fn parse_datetime_value(text: &str) -> Option<DateValue> {
    if let Ok(dt) = DateTime::parse_from_rfc3339(text) {
        return Some(DateValue {
            epoch_ms: dt.timestamp_millis(),
            has_time: true,
        });
    }
    let dt = NaiveDateTime::parse_from_str(text, "%Y-%m-%dT%H:%M:%S").ok()?;
    Some(DateValue {
        epoch_ms: Utc.from_utc_datetime(&dt).timestamp_millis(),
        has_time: true,
    })
}

fn load_tags(conn: &Connection, file_id: i64) -> Result<Vec<String>, VaultError> {
    let mut stmt = conn
        .prepare("SELECT tag_norm FROM file_tags WHERE file_id = ?1 ORDER BY tag_norm")
        .map_err(DbError::from)?;
    stmt.query_map(params![file_id], |row| row.get::<_, String>(0))
        .map_err(DbError::from)?
        .collect::<Result<Vec<_>, _>>()
        .map_err(|err| DbError::from(err).into())
}

fn load_links(conn: &Connection, file_id: i64) -> Result<Vec<LinkValue>, VaultError> {
    let mut stmt = conn
        .prepare(
            "SELECT target_raw, target_path FROM links WHERE source_file_id = ?1 ORDER BY ordinal",
        )
        .map_err(DbError::from)?;
    stmt.query_map(params![file_id], |row| {
        let target: String = row.get(0)?;
        let resolved_path: Option<String> = row.get(1)?;
        Ok(LinkValue {
            target,
            display: None,
            resolved_path,
        })
    })
    .map_err(DbError::from)?
    .collect::<Result<Vec<_>, _>>()
    .map_err(|err| DbError::from(err).into())
}

fn load_backlinks(conn: &Connection, path: &str) -> Result<Vec<LinkValue>, VaultError> {
    let mut stmt = conn
        .prepare(
            "SELECT src.path
             FROM links l
             JOIN files src ON src.id = l.source_file_id
             WHERE l.target_path = ?1
             ORDER BY src.path",
        )
        .map_err(DbError::from)?;
    stmt.query_map(params![path], |row| {
        let source: String = row.get(0)?;
        Ok(LinkValue {
            target: source.clone(),
            display: None,
            resolved_path: Some(source),
        })
    })
    .map_err(DbError::from)?
    .collect::<Result<Vec<_>, _>>()
    .map_err(|err| DbError::from(err).into())
}

fn eval_formulas(
    query: &SlateQuery,
    row: &RowContext,
    this: Option<&RowContext>,
    ctx: &EngineCtx<'_>,
    vault: &dyn VaultLookup,
    warnings: &WarningSink,
) -> FormulaEval {
    let mut state = FormulaEval::default();
    for (name, expr) in &query.formulas {
        let eval_ctx = eval_ctx(row, this, &state.values, ctx, vault, warnings);
        match eval(expr, &eval_ctx) {
            Ok(value) => {
                state.values.insert(name.clone(), value);
            }
            Err(error) => {
                state.errors.insert(name.clone(), error);
            }
        }
    }
    state
}

fn eval_filter(node: &FilterNode, ctx: &EvalCtx<'_>) -> Result<bool, EvalError> {
    match node {
        FilterNode::Stmt(expr) => Ok(eval(expr, ctx)?.is_truthy()),
        FilterNode::And(nodes) => {
            for node in nodes {
                if !eval_filter(node, ctx)? {
                    return Ok(false);
                }
            }
            Ok(true)
        }
        FilterNode::Or(nodes) => {
            for node in nodes {
                if eval_filter(node, ctx)? {
                    return Ok(true);
                }
            }
            Ok(false)
        }
        FilterNode::Not(nodes) => {
            for node in nodes {
                if eval_filter(node, ctx)? {
                    return Ok(false);
                }
            }
            Ok(true)
        }
    }
}

fn eval_filter_after_pushdown(
    node: &FilterNode,
    ctx: &EvalCtx<'_>,
    pushdown: bool,
) -> Result<bool, EvalError> {
    if !pushdown {
        return eval_filter(node, ctx);
    }
    match node {
        FilterNode::Stmt(expr) if file_matches_query(expr).is_some() => Ok(true),
        FilterNode::And(nodes) => {
            for node in nodes {
                if matches!(node, FilterNode::Stmt(expr) if file_matches_query(expr).is_some()) {
                    continue;
                }
                if !eval_filter(node, ctx)? {
                    return Ok(false);
                }
            }
            Ok(true)
        }
        _ => eval_filter(node, ctx),
    }
}

fn materialize_row(
    query: &SlateQuery,
    row: &RowContext,
    formulas: &FormulaEval,
    this: Option<&RowContext>,
    ctx: &EngineCtx<'_>,
    vault: &dyn VaultLookup,
    warnings: &WarningSink,
) -> Result<MaterializedRow, String> {
    let eval_ctx = eval_ctx(row, this, &formulas.values, ctx, vault, warnings);
    let mut sort_values = Vec::with_capacity(query.sort.len());
    for sort in &query.sort {
        if let Some((name, error)) = first_formula_error_expr(&sort.expr, &formulas.errors) {
            return Err(format!("sort formula.{name}: {error}"));
        }
        sort_values.push(eval(&sort.expr, &eval_ctx).map_err(|error| format!("sort: {error}"))?);
    }
    let group_key = if let Some(group_by) = &query.group_by {
        let expr = property_expr(&group_by.property);
        if let Some((name, error)) = first_formula_error_expr(&expr, &formulas.errors) {
            return Err(format!("groupBy formula.{name}: {error}"));
        }
        Some(eval(&expr, &eval_ctx).map_err(|error| format!("groupBy: {error}"))?)
    } else {
        None
    };
    let cells = query
        .columns
        .iter()
        .map(|column| eval_column(column, row, formulas, this, ctx, vault, warnings))
        .collect::<Vec<_>>();
    let mut summary_values = BTreeMap::new();
    for (index, column) in query.columns.iter().enumerate() {
        summary_values.insert(column.id.clone(), cells[index].clone());
    }
    for (column_id, _) in &query.summaries {
        if !summary_values.contains_key(column_id) {
            let column = ColumnSelection {
                id: column_id.clone(),
                display_name: None,
            };
            summary_values.insert(
                column_id.clone(),
                eval_column(&column, row, formulas, this, ctx, vault, warnings),
            );
        }
    }
    Ok(MaterializedRow {
        path: row.file_path.clone(),
        ordinal: row.task.as_ref().map(|task| task.ordinal),
        cells,
        summary_values,
        sort_values,
        group_key,
    })
}

fn row_to_result(row: &MaterializedRow, columns: &[ResultColumn]) -> BasesRow {
    let audio_description = row_audio_description(row, columns);
    BasesRow {
        path: row.path.clone(),
        task_ordinal: row.ordinal,
        cells: row.cells.clone(),
        audio_description,
    }
}

fn property_expr(property: &PropertyRef) -> Expr {
    Expr {
        span: super::expr::Span { start: 0, end: 0 },
        kind: ExprKind::Prop(property.clone()),
    }
}

fn sort_rows(query: &SlateQuery, rows: &mut [MaterializedRow]) {
    rows.sort_by(|lhs, rhs| compare_rows(query, lhs, rhs));
}

fn compare_rows(query: &SlateQuery, lhs: &MaterializedRow, rhs: &MaterializedRow) -> Ordering {
    for (index, sort) in query.sort.iter().enumerate() {
        let ordering = compare_sort_key(
            &lhs.sort_values[index],
            &rhs.sort_values[index],
            sort.ascending,
        );
        if ordering != Ordering::Equal {
            return ordering;
        }
    }
    match lhs.path.cmp(&rhs.path) {
        Ordering::Equal => lhs.ordinal.cmp(&rhs.ordinal),
        ordering => ordering,
    }
}

fn compare_sort_key(lhs: &Value, rhs: &Value, ascending: bool) -> Ordering {
    match (matches!(lhs, Value::Null), matches!(rhs, Value::Null)) {
        (true, true) => Ordering::Equal,
        (true, false) => Ordering::Greater,
        (false, true) => Ordering::Less,
        (false, false) => {
            let ordering = compare_non_null_values(lhs, rhs);
            if ascending {
                ordering
            } else {
                ordering.reverse()
            }
        }
    }
}

fn compare_non_null_values(lhs: &Value, rhs: &Value) -> Ordering {
    let lhs_rank = value_type_rank(lhs);
    let rhs_rank = value_type_rank(rhs);
    if lhs_rank != rhs_rank {
        return lhs_rank.cmp(&rhs_rank);
    }
    match (lhs, rhs) {
        (Value::Bool(lhs), Value::Bool(rhs)) => lhs.cmp(rhs),
        (Value::Number(lhs), Value::Number(rhs)) => lhs.partial_cmp(rhs).unwrap_or(Ordering::Equal),
        (Value::Date(lhs), Value::Date(rhs)) => lhs.epoch_ms.cmp(&rhs.epoch_ms),
        (Value::Text(lhs), Value::Text(rhs)) => lhs.to_lowercase().cmp(&rhs.to_lowercase()),
        _ => value_key(lhs).cmp(&value_key(rhs)),
    }
}

fn value_type_rank(value: &Value) -> u8 {
    match value {
        Value::Null => 9,
        Value::Bool(_) => 0,
        Value::Number(_) => 1,
        Value::Date(_) => 2,
        Value::Duration(_) => 3,
        Value::Text(_) => 4,
        Value::Link(_) => 5,
        Value::File(_) => 6,
        Value::Regex(_, _) => 7,
        Value::List(_) => 8,
        Value::Object(_) => 9,
    }
}

fn apply_quick_filter(rows: &mut Vec<MaterializedRow>, quick_filter: Option<&str>) {
    let Some(needle) = quick_filter
        .map(str::trim)
        .filter(|filter| !filter.is_empty())
    else {
        return;
    };
    let needle = value_text_norm(needle);
    rows.retain(|row| {
        row.cells
            .iter()
            .any(|cell| cell_text_norm(cell).contains(&needle))
    });
}

fn cell_text_norm(cell: &CellValue) -> String {
    match cell {
        CellValue::Value(value) => value_text_norm(&value_display(value)),
        CellValue::Error(error) => value_text_norm(error),
    }
}

fn value_text_norm(value: &str) -> String {
    use unicode_normalization::{UnicodeNormalization, char::is_combining_mark};

    value
        .nfd()
        .filter(|c| !is_combining_mark(*c))
        .flat_map(char::to_lowercase)
        .collect()
}

fn group_rows(query: &SlateQuery, rows: &mut Vec<MaterializedRow>) -> Vec<GroupSlice> {
    let Some(group_by) = &query.group_by else {
        return Vec::new();
    };
    let mut buckets: Vec<(Value, Vec<MaterializedRow>)> = Vec::new();
    for row in rows.drain(..) {
        let key = row.group_key.clone().unwrap_or(Value::Null);
        if let Some((_, group_rows)) = buckets.iter_mut().find(|(got, _)| *got == key) {
            group_rows.push(row);
        } else {
            buckets.push((key, vec![row]));
        }
    }
    buckets.sort_by(|(lhs, _), (rhs, _)| compare_sort_key(lhs, rhs, group_by.ascending));
    let mut groups = Vec::with_capacity(buckets.len());
    let mut start = 0usize;
    let property = property_ref_label(&group_by.property);
    for (key, mut bucket_rows) in buckets {
        let end = start + bucket_rows.len();
        groups.push(GroupSlice {
            label: group_label(&property, &key),
            key,
            full_range: start..end,
        });
        rows.append(&mut bucket_rows);
        start = end;
    }
    groups
}

fn visible_groups(
    groups: Vec<GroupSlice>,
    summaries: Vec<Vec<BasesSummaryCell>>,
    shown_count: usize,
) -> Vec<ResultGroup> {
    groups
        .into_iter()
        .zip(summaries)
        .filter_map(|(group, summaries)| {
            let start = group.full_range.start.min(shown_count);
            let end = group.full_range.end.min(shown_count);
            (start < end).then_some(ResultGroup {
                key: group.key,
                label: group.label,
                rows: start..end,
                summaries,
            })
        })
        .collect()
}

#[derive(Debug, Clone)]
enum SummaryKind {
    Count,
    Filled,
    Empty,
    Unique,
    Min,
    Max,
    Sum,
    Average,
    Earliest,
    Latest,
    Checked,
    Unchecked,
    Custom(String),
}

#[derive(Clone, Copy)]
struct SummaryEvalDeps<'a, 'ctx> {
    ctx: &'a EngineCtx<'ctx>,
    vault: &'a dyn VaultLookup,
    warnings: &'a WarningSink,
}

fn compute_summaries(
    query: &SlateQuery,
    rows: &[MaterializedRow],
    range: Range<usize>,
    ctx: &EngineCtx<'_>,
    vault: &dyn VaultLookup,
    warnings: &WarningSink,
    result_warnings: &mut Vec<String>,
) -> Result<Vec<BasesSummaryCell>, String> {
    query
        .summaries
        .iter()
        .map(|(column_id, summary)| {
            let (summary_name, kind) = summary_kind(summary)?;
            compute_summary(
                query,
                rows,
                range.clone(),
                column_id,
                &summary_name,
                kind,
                ctx,
                vault,
                warnings,
                result_warnings,
            )
        })
        .collect()
}

fn summary_kind(summary: &SummaryRef) -> Result<(String, SummaryKind), String> {
    match summary {
        SummaryRef::Builtin(name) => {
            let normalized = name.to_ascii_lowercase();
            let kind = match normalized.as_str() {
                "count" => SummaryKind::Count,
                "filled" => SummaryKind::Filled,
                "empty" => SummaryKind::Empty,
                "unique" => SummaryKind::Unique,
                "min" => SummaryKind::Min,
                "max" => SummaryKind::Max,
                "sum" => SummaryKind::Sum,
                "average" => SummaryKind::Average,
                "earliest" => SummaryKind::Earliest,
                "latest" => SummaryKind::Latest,
                "checked" => SummaryKind::Checked,
                "unchecked" => SummaryKind::Unchecked,
                "median" | "stddev" | "range" => {
                    return Err(format!("summary.{name}: unsupported in Bases v1"));
                }
                _ => return Err(format!("summary.{name}: unknown summary")),
            };
            Ok((normalized, kind))
        }
        SummaryRef::Custom(name) => Ok((name.clone(), SummaryKind::Custom(name.clone()))),
    }
}

#[allow(clippy::too_many_arguments)]
fn compute_summary(
    query: &SlateQuery,
    rows: &[MaterializedRow],
    range: Range<usize>,
    column_id: &str,
    summary_name: &str,
    kind: SummaryKind,
    ctx: &EngineCtx<'_>,
    vault: &dyn VaultLookup,
    warnings: &WarningSink,
    result_warnings: &mut Vec<String>,
) -> Result<BasesSummaryCell, String> {
    let value = match kind {
        SummaryKind::Count => CellValue::Value(Value::Number(range.len() as f64)),
        SummaryKind::Filled => CellValue::Value(Value::Number(
            range
                .clone()
                .filter(|index| {
                    summary_cell_value(&rows[*index], column_id)
                        .is_some_and(|value| !is_empty_summary_value(value))
                })
                .count() as f64,
        )),
        SummaryKind::Empty => CellValue::Value(Value::Number(
            range
                .clone()
                .filter(|index| {
                    summary_cell_value(&rows[*index], column_id).is_none_or(is_empty_summary_value)
                })
                .count() as f64,
        )),
        SummaryKind::Unique => {
            let mut seen = BTreeMap::new();
            for index in range.clone() {
                if let Some(value) = summary_cell_value(&rows[index], column_id)
                    && !is_empty_summary_value(value)
                {
                    seen.insert(value_key(value), ());
                }
            }
            CellValue::Value(Value::Number(seen.len() as f64))
        }
        SummaryKind::Min => numeric_summary(
            rows,
            range,
            column_id,
            summary_name,
            result_warnings,
            |values| values.iter().copied().fold(f64::INFINITY, f64::min),
        ),
        SummaryKind::Max => numeric_summary(
            rows,
            range,
            column_id,
            summary_name,
            result_warnings,
            |values| values.iter().copied().fold(f64::NEG_INFINITY, f64::max),
        ),
        SummaryKind::Sum => numeric_summary(
            rows,
            range,
            column_id,
            summary_name,
            result_warnings,
            |values| values.iter().sum(),
        ),
        SummaryKind::Average => numeric_summary(
            rows,
            range,
            column_id,
            summary_name,
            result_warnings,
            |values| values.iter().sum::<f64>() / values.len() as f64,
        ),
        SummaryKind::Earliest => {
            date_summary(rows, range, column_id, summary_name, result_warnings, true)
        }
        SummaryKind::Latest => {
            date_summary(rows, range, column_id, summary_name, result_warnings, false)
        }
        SummaryKind::Checked => {
            boolean_summary(rows, range, column_id, summary_name, result_warnings, true)
        }
        SummaryKind::Unchecked => {
            boolean_summary(rows, range, column_id, summary_name, result_warnings, false)
        }
        SummaryKind::Custom(name) => custom_summary(
            query,
            rows,
            range,
            column_id,
            &name,
            SummaryEvalDeps {
                ctx,
                vault,
                warnings,
            },
        )?,
    };
    Ok(BasesSummaryCell {
        column_id: column_id.to_string(),
        summary: summary_name.to_string(),
        value,
    })
}

fn summary_cell_value<'a>(row: &'a MaterializedRow, column_id: &str) -> Option<&'a Value> {
    match row.summary_values.get(column_id) {
        Some(CellValue::Value(value)) => Some(value),
        Some(CellValue::Error(_)) | None => None,
    }
}

fn is_empty_summary_value(value: &Value) -> bool {
    match value {
        Value::Null => true,
        Value::Text(value) => value.is_empty(),
        Value::List(values) => values.is_empty(),
        Value::Object(values) => values.is_empty(),
        _ => false,
    }
}

fn numeric_summary(
    rows: &[MaterializedRow],
    range: Range<usize>,
    column_id: &str,
    summary_name: &str,
    warnings: &mut Vec<String>,
    aggregate: impl FnOnce(&[f64]) -> f64,
) -> CellValue {
    let mut values = Vec::new();
    for index in range {
        match summary_cell_value(&rows[index], column_id) {
            Some(Value::Number(value)) => values.push(*value),
            Some(value) if !is_empty_summary_value(value) => {
                return inapplicable_summary(column_id, summary_name, warnings);
            }
            _ => {}
        }
    }
    if values.is_empty() {
        return inapplicable_summary(column_id, summary_name, warnings);
    }
    CellValue::Value(Value::Number(aggregate(&values)))
}

fn date_summary(
    rows: &[MaterializedRow],
    range: Range<usize>,
    column_id: &str,
    summary_name: &str,
    warnings: &mut Vec<String>,
    earliest: bool,
) -> CellValue {
    let mut values = Vec::new();
    for index in range {
        match summary_cell_value(&rows[index], column_id) {
            Some(Value::Date(value)) => values.push(*value),
            Some(value) if !is_empty_summary_value(value) => {
                return inapplicable_summary(column_id, summary_name, warnings);
            }
            _ => {}
        }
    }
    let value = if earliest {
        values.into_iter().min_by_key(|date| date.epoch_ms)
    } else {
        values.into_iter().max_by_key(|date| date.epoch_ms)
    };
    let Some(value) = value else {
        return inapplicable_summary(column_id, summary_name, warnings);
    };
    CellValue::Value(Value::Date(value))
}

fn boolean_summary(
    rows: &[MaterializedRow],
    range: Range<usize>,
    column_id: &str,
    summary_name: &str,
    warnings: &mut Vec<String>,
    target: bool,
) -> CellValue {
    let mut count = 0usize;
    for index in range {
        match summary_cell_value(&rows[index], column_id) {
            Some(Value::Bool(value)) if *value == target => count += 1,
            Some(Value::Bool(_)) => {}
            Some(value) if !is_empty_summary_value(value) => {
                return inapplicable_summary(column_id, summary_name, warnings);
            }
            _ => {}
        }
    }
    CellValue::Value(Value::Number(count as f64))
}

fn inapplicable_summary(
    column_id: &str,
    summary_name: &str,
    warnings: &mut Vec<String>,
) -> CellValue {
    warnings.push(format!(
        "summary {summary_name} is not applicable to {column_id}"
    ));
    CellValue::Value(Value::Text("—".to_string()))
}

fn custom_summary(
    query: &SlateQuery,
    rows: &[MaterializedRow],
    range: Range<usize>,
    column_id: &str,
    name: &str,
    deps: SummaryEvalDeps<'_, '_>,
) -> Result<CellValue, String> {
    let Some((_, expr)) = query
        .custom_summaries
        .iter()
        .find(|(summary_name, _)| summary_name == name)
    else {
        return Err(format!("summary.{name}: missing custom summary formula"));
    };
    let values = range
        .map(|index| {
            summary_cell_value(&rows[index], column_id)
                .cloned()
                .unwrap_or(Value::Null)
        })
        .collect::<Vec<_>>();
    let mut file_fields = FileFields::for_path("summary");
    file_fields
        .properties
        .insert("values".to_string(), Value::List(values.clone()));
    let row = RowContext {
        file_path: "summary".to_string(),
        file_fields,
        properties: vec![("values".to_string(), Value::List(values))],
        task: None,
    };
    let formulas = ResolvedFormulas::new();
    let eval_ctx = EvalCtx {
        file: &row,
        this: None,
        formulas: &formulas,
        now_ms: deps.ctx.now_ms,
        vault: deps.vault,
        warnings: deps.warnings,
        filter_position: false,
    };
    eval(expr, &eval_ctx)
        .map(CellValue::Value)
        .map_err(|error| format!("summary.{name}: {error}"))
}

fn value_key(value: &Value) -> String {
    match value {
        Value::Null => "null".to_string(),
        Value::Bool(value) => format!("bool:{value}"),
        Value::Number(value) => format!("number:{value:?}"),
        Value::Date(value) => format!("date:{}:{}", value.epoch_ms, value.has_time),
        Value::Duration(value) => format!("duration:{value}"),
        Value::Text(value) => format!("text:{value}"),
        Value::Link(value) => format!("link:{}:{:?}", value.target, value.resolved_path),
        Value::File(value) => format!("file:{}", value.path),
        Value::Regex(pattern, flags) => format!("regex:{pattern}/{flags}"),
        Value::List(values) => format!(
            "list:[{}]",
            values.iter().map(value_key).collect::<Vec<_>>().join(",")
        ),
        Value::Object(values) => format!(
            "object:{{{}}}",
            values
                .iter()
                .map(|(key, value)| format!("{key}:{}", value_key(value)))
                .collect::<Vec<_>>()
                .join(",")
        ),
    }
}

fn row_audio_description(row: &MaterializedRow, columns: &[ResultColumn]) -> String {
    let Some((first, rest)) = row.cells.split_first() else {
        return row.path.clone();
    };
    let mut out = cell_audio_value(first).unwrap_or_else(|| row.path.clone());
    for (index, cell) in rest.iter().enumerate() {
        if let Some(value) = cell_audio_value(cell) {
            let label = columns
                .get(index + 1)
                .map(column_label)
                .unwrap_or_else(|| format!("Column {}", index + 2));
            out.push_str(". ");
            out.push_str(&label);
            out.push_str(": ");
            out.push_str(&value);
        }
    }
    if out.is_empty() {
        row.path.clone()
    } else {
        out
    }
}

fn cell_audio_value(cell: &CellValue) -> Option<String> {
    match cell {
        CellValue::Value(value) if is_empty_summary_value(value) => None,
        CellValue::Value(value) => Some(value_display(value)),
        CellValue::Error(error) => Some(format!("Error: {error}")),
    }
}

fn audio_summary(
    query: &SlateQuery,
    total_count: usize,
    shown_count: usize,
    groups: &[GroupSlice],
) -> String {
    if total_count == 0 {
        return "No results.".to_string();
    }
    let noun = match query.row_source {
        RowSource::Tasks if total_count == 1 => "task",
        RowSource::Tasks => "tasks",
        RowSource::Files if total_count == 1 => "note",
        RowSource::Files => "notes",
    };
    let mut out = format!("{total_count} {noun}");
    if let Some(group_by) = &query.group_by {
        let property = property_ref_label(&group_by.property);
        let parts = groups
            .iter()
            .map(|group| format!("{} {}", group.label, group.full_range.len()))
            .collect::<Vec<_>>();
        out.push_str(", grouped by ");
        out.push_str(&property);
        if !parts.is_empty() {
            out.push_str(": ");
            out.push_str(&parts.join(", "));
        }
    }
    if query
        .limit
        .is_some_and(|limit| shown_count < total_count && limit as usize == shown_count)
    {
        out.push_str(&format!(", limited to {shown_count}"));
    }
    out.push('.');
    if !query.sort.is_empty() {
        out.push(' ');
        out.push_str("Sorted by ");
        out.push_str(&sort_description(query));
        out.push('.');
    }
    out
}

fn sort_description(query: &SlateQuery) -> String {
    query
        .sort
        .iter()
        .map(|sort| {
            let direction = if sort.ascending {
                "ascending"
            } else {
                "descending"
            };
            format!("{} {direction}", expr_label(&sort.expr))
        })
        .collect::<Vec<_>>()
        .join(", ")
}

fn column_label(column: &ResultColumn) -> String {
    column
        .display_name
        .clone()
        .unwrap_or_else(|| column.id.clone())
}

fn group_label(property: &str, value: &Value) -> String {
    if matches!(value, Value::Null) {
        format!("No {property}")
    } else {
        value_display(value)
    }
}

fn expr_label(expr: &Expr) -> String {
    match &expr.kind {
        ExprKind::Prop(property) => property_ref_label(property),
        _ => "expression".to_string(),
    }
}

fn property_ref_label(property: &PropertyRef) -> String {
    match property {
        PropertyRef::Note(name) => name.clone(),
        PropertyRef::Formula(name) => format!("formula.{name}"),
        PropertyRef::File(field) => format!("file.{}", file_field_label(*field)),
        PropertyRef::This => "this".to_string(),
        PropertyRef::ThisNote(name) => format!("this.{name}"),
        PropertyRef::ThisFile(field) => format!("this.file.{}", file_field_label(*field)),
        PropertyRef::TaskField(field) => format!("task.{}", task_field_label(*field)),
        PropertyRef::ImplicitValue => "value".to_string(),
        PropertyRef::ImplicitIndex => "index".to_string(),
        PropertyRef::ImplicitAcc => "acc".to_string(),
    }
}

fn file_field_label(field: FileField) -> &'static str {
    match field {
        FileField::Name => "name",
        FileField::Basename => "basename",
        FileField::Path => "path",
        FileField::Folder => "folder",
        FileField::Ext => "ext",
        FileField::Size => "size",
        FileField::Properties => "properties",
        FileField::Tags => "tags",
        FileField::Aliases => "aliases",
        FileField::Links => "links",
        FileField::Backlinks => "backlinks",
        FileField::Embeds => "embeds",
        FileField::File => "file",
        FileField::Tasks => "tasks",
        FileField::Ctime => "ctime",
        FileField::Mtime => "mtime",
        FileField::InDegree => "inDegree",
        FileField::OutDegree => "outDegree",
    }
}

fn task_field_label(field: TaskField) -> &'static str {
    match field {
        TaskField::Text => "text",
        TaskField::Status => "status",
        TaskField::Completed => "completed",
        TaskField::Due => "due",
        TaskField::Scheduled => "scheduled",
        TaskField::Priority => "priority",
        TaskField::File => "file",
    }
}

pub(crate) fn value_display(value: &Value) -> String {
    match value {
        Value::Null => String::new(),
        Value::Bool(value) => value.to_string(),
        Value::Number(value) => {
            if value.fract() == 0.0 {
                format!("{value:.0}")
            } else {
                value.to_string()
            }
        }
        Value::Date(value) => Utc
            .timestamp_millis_opt(value.epoch_ms)
            .single()
            .map(|datetime| {
                if value.has_time {
                    datetime.format("%Y-%m-%d %H:%M").to_string()
                } else {
                    datetime.format("%Y-%m-%d").to_string()
                }
            })
            .unwrap_or_else(|| value.epoch_ms.to_string()),
        Value::Duration(value) => value.to_string(),
        Value::Text(value) => value.clone(),
        Value::Link(value) => value.display.clone().unwrap_or_else(|| {
            value
                .resolved_path
                .clone()
                .unwrap_or_else(|| value.target.clone())
        }),
        Value::File(value) => value.path.clone(),
        Value::Regex(pattern, flags) => {
            if flags.is_empty() {
                format!("/{pattern}/")
            } else {
                format!("/{pattern}/{flags}")
            }
        }
        Value::List(values) => values
            .iter()
            .map(value_display)
            .collect::<Vec<_>>()
            .join(", "),
        Value::Object(values) => values
            .iter()
            .map(|(key, value)| format!("{key}: {}", value_display(value)))
            .collect::<Vec<_>>()
            .join(", "),
    }
}

fn eval_column(
    column: &ColumnSelection,
    row: &RowContext,
    formulas: &FormulaEval,
    this: Option<&RowContext>,
    ctx: &EngineCtx<'_>,
    vault: &dyn VaultLookup,
    warnings: &WarningSink,
) -> CellValue {
    if let Some(name) = column.id.strip_prefix("formula.") {
        if let Some(error) = formulas.errors.get(name) {
            return CellValue::Error(error.to_string());
        }
        return CellValue::Value(formulas.values.get(name).cloned().unwrap_or(Value::Null));
    }
    let expr = column_expr(&column.id);
    let eval_ctx = eval_ctx(row, this, &formulas.values, ctx, vault, warnings);
    match eval(&expr, &eval_ctx) {
        Ok(value) => CellValue::Value(value),
        Err(error) => CellValue::Error(error.to_string()),
    }
}

fn column_expr(id: &str) -> Expr {
    super::expr::parse_expr(id).unwrap_or_else(|_| Expr {
        span: super::expr::Span { start: 0, end: 0 },
        kind: ExprKind::Prop(PropertyRef::Note(id.to_string())),
    })
}

fn first_formula_error<'a>(
    node: &FilterNode,
    errors: &'a BTreeMap<String, EvalError>,
) -> Option<(&'a str, &'a EvalError)> {
    match node {
        FilterNode::Stmt(expr) => first_formula_error_expr(expr, errors),
        FilterNode::And(nodes) | FilterNode::Or(nodes) | FilterNode::Not(nodes) => nodes
            .iter()
            .find_map(|node| first_formula_error(node, errors)),
    }
}

fn first_formula_error_expr<'a>(
    expr: &Expr,
    errors: &'a BTreeMap<String, EvalError>,
) -> Option<(&'a str, &'a EvalError)> {
    match &expr.kind {
        ExprKind::Prop(PropertyRef::Formula(name)) => errors
            .get_key_value(name)
            .map(|(name, error)| (name.as_str(), error)),
        ExprKind::Index { base, index }
        | ExprKind::Binary {
            lhs: base,
            rhs: index,
            ..
        } => first_formula_error_expr(base, errors)
            .or_else(|| first_formula_error_expr(index, errors)),
        ExprKind::Field { base, .. } | ExprKind::Unary { rhs: base, .. } => {
            first_formula_error_expr(base, errors)
        }
        ExprKind::Call { callee, args } => {
            let callee_error = match callee {
                Callee::Method { receiver, .. } => first_formula_error_expr(receiver, errors),
                Callee::Global(_) => None,
            };
            callee_error.or_else(|| {
                args.iter()
                    .find_map(|arg| first_formula_error_expr(arg, errors))
            })
        }
        ExprKind::ListExpr {
            base, body, init, ..
        } => first_formula_error_expr(base, errors)
            .or_else(|| first_formula_error_expr(body, errors))
            .or_else(|| {
                init.as_deref()
                    .and_then(|expr| first_formula_error_expr(expr, errors))
            }),
        ExprKind::Lit(Lit::List(items)) => items
            .iter()
            .find_map(|item| first_formula_error_expr(item, errors)),
        ExprKind::Lit(Lit::Object(items)) => items
            .iter()
            .find_map(|(_, value)| first_formula_error_expr(value, errors)),
        ExprKind::Lit(_) | ExprKind::Prop(_) | ExprKind::Unsupported { .. } => None,
    }
}

fn eval_ctx<'a>(
    row: &'a RowContext,
    this: Option<&'a RowContext>,
    formulas: &'a ResolvedFormulas,
    ctx: &EngineCtx<'_>,
    vault: &'a dyn VaultLookup,
    warnings: &'a WarningSink,
) -> EvalCtx<'a> {
    EvalCtx {
        file: row,
        this,
        formulas,
        now_ms: ctx.now_ms,
        vault,
        warnings,
        filter_position: false,
    }
}

fn result_columns(query: &SlateQuery) -> Vec<ResultColumn> {
    query
        .columns
        .iter()
        .map(|column| ResultColumn {
            id: column.id.clone(),
            display_name: column.display_name.clone(),
        })
        .collect()
}

fn error_result(
    query: &SlateQuery,
    ctx: &EngineCtx<'_>,
    construct: impl Into<String>,
    row_path: &str,
) -> BasesResultSet {
    BasesResultSet {
        columns: result_columns(query),
        rows: Vec::new(),
        groups: Vec::new(),
        summaries: Vec::new(),
        total_count: 0,
        shown_count: 0,
        warnings: Vec::new(),
        error: Some(ViewError {
            construct: construct.into(),
            row_path: row_path.to_string(),
        }),
        executed_at_ms: ctx.now_ms,
        cache_hit: false,
        audio_summary: "No results.".to_string(),
    }
}

fn cache_key(query: &SlateQuery, ctx: &EngineCtx<'_>) -> Option<CacheKey> {
    if ctx
        .quick_filter
        .is_some_and(|filter| !filter.trim().is_empty())
    {
        return None;
    }
    if query_mentions_global(query, GlobalFn::Now) {
        return None;
    }
    let bytes = serde_json::to_vec(query).ok()?;
    let today_key =
        query_mentions_global(query, GlobalFn::Today).then_some(ctx.now_ms / 86_400_000);
    Some(CacheKey {
        query_hash: blake3::hash(&bytes).to_hex().to_string(),
        generation: ctx.generation,
        this_path: ctx.this_path.clone(),
        today_key,
    })
}

fn query_mentions_global(query: &SlateQuery, needle: GlobalFn) -> bool {
    query
        .filters
        .as_ref()
        .is_some_and(|filter| filter_mentions_global(filter, needle))
        || query
            .formulas
            .iter()
            .any(|(_, expr)| expr_mentions_global(expr, needle))
        || query
            .sort
            .iter()
            .any(|sort| expr_mentions_global(&sort.expr, needle))
        || query
            .columns
            .iter()
            .any(|column| expr_mentions_global(&column_expr(&column.id), needle))
}

fn filter_mentions_global(filter: &FilterNode, needle: GlobalFn) -> bool {
    match filter {
        FilterNode::Stmt(expr) => expr_mentions_global(expr, needle),
        FilterNode::And(nodes) | FilterNode::Or(nodes) | FilterNode::Not(nodes) => nodes
            .iter()
            .any(|node| filter_mentions_global(node, needle)),
    }
}

fn expr_mentions_global(expr: &Expr, needle: GlobalFn) -> bool {
    match &expr.kind {
        ExprKind::Call { callee, args } => {
            matches!(callee, Callee::Global(function) if *function == needle)
                || matches!(callee, Callee::Method { receiver, .. } if expr_mentions_global(receiver, needle))
                || args.iter().any(|arg| expr_mentions_global(arg, needle))
        }
        ExprKind::Index { base, index }
        | ExprKind::Binary {
            lhs: base,
            rhs: index,
            ..
        } => expr_mentions_global(base, needle) || expr_mentions_global(index, needle),
        ExprKind::Field { base, .. } | ExprKind::Unary { rhs: base, .. } => {
            expr_mentions_global(base, needle)
        }
        ExprKind::ListExpr {
            base, body, init, ..
        } => {
            expr_mentions_global(base, needle)
                || expr_mentions_global(body, needle)
                || init
                    .as_deref()
                    .is_some_and(|expr| expr_mentions_global(expr, needle))
        }
        ExprKind::Lit(Lit::List(items)) => {
            items.iter().any(|item| expr_mentions_global(item, needle))
        }
        ExprKind::Lit(Lit::Object(items)) => items
            .iter()
            .any(|(_, value)| expr_mentions_global(value, needle)),
        ExprKind::Lit(_) | ExprKind::Prop(_) | ExprKind::Unsupported { .. } => false,
    }
}

struct SqlVaultLookup<'a> {
    conn: &'a Connection,
    fts: &'a FtsMatchCache,
    cancel: &'a CancelToken,
    source_path: &'a str,
    link_index: RefCell<Option<crate::InMemoryVaultIndex>>,
    link_resolutions: RefCell<BTreeMap<String, Option<String>>>,
}

impl VaultLookup for SqlVaultLookup<'_> {
    fn resolve_link(&self, target: &str) -> Option<String> {
        if let Some(resolved) = self.link_resolutions.borrow().get(target) {
            return resolved.clone();
        }
        if self.link_index.borrow().is_none() {
            let mut stmt = self
                .conn
                .prepare("SELECT path FROM files ORDER BY path")
                .ok()?;
            let paths = stmt
                .query_map([], |row| row.get::<_, String>(0))
                .ok()?
                .collect::<Result<Vec<_>, _>>()
                .ok()?;
            *self.link_index.borrow_mut() = Some(crate::InMemoryVaultIndex::new(paths));
        }
        let index = self.link_index.borrow();
        let resolved = match crate::resolve_link(target, None, self.source_path, index.as_ref()?) {
            crate::ResolvedLink::Resolved { target_path, .. } => Some(target_path),
            crate::ResolvedLink::Unresolved { .. } | crate::ResolvedLink::External => None,
        };
        self.link_resolutions
            .borrow_mut()
            .insert(target.to_string(), resolved.clone());
        resolved
    }

    fn file_matches(&self, path: &str, query: &str) -> Result<bool, EvalError> {
        match self.fts.paths(self.conn, query, self.cancel) {
            Ok(paths) => Ok(paths.contains(path)),
            Err(VaultError::Cancelled) => Err(EvalError::Cancelled),
            Err(error) => Err(EvalError::InvalidArgument {
                function: "file.matches".to_string(),
                message: error.to_string(),
            }),
        }
    }

    fn row_for_path(&self, path: &str) -> Option<RowContext> {
        assemble_row_for_path(self.conn, path).ok().flatten()
    }

    fn links_for(&self, path: &str) -> Vec<LinkValue> {
        self.conn
            .query_row(
                "SELECT id FROM files WHERE path = ?1",
                params![path],
                |row| row.get::<_, i64>(0),
            )
            .optional()
            .ok()
            .flatten()
            .and_then(|id| load_links(self.conn, id).ok())
            .unwrap_or_default()
    }

    fn backlinks_for(&self, path: &str) -> Vec<LinkValue> {
        load_backlinks(self.conn, path).unwrap_or_default()
    }
}
