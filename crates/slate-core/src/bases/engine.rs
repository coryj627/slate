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
    collections::{BTreeMap, BTreeSet, VecDeque},
    ops::Range,
};

use chrono::{DateTime, NaiveDate, NaiveDateTime, TimeZone, Utc};
use rusqlite::{Connection, OptionalExtension, params, params_from_iter};

use super::{
    ColumnSelection, FilterNode, QuerySource, RowSource, SlateQuery, SortKey, SummaryRef,
    eval::{
        DateValue, EvalCtx, EvalError, FileFields, LinkValue, ResolvedFormulas, RowContext,
        TaskRow, Value, VaultLookup, WarningSink, compare_dql_command_sort_values,
        dql_command_sort_value, eval, link_identity,
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
    pub unfiltered_shown_count: usize,
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
    recency: RefCell<VecDeque<CacheKey>>,
}

const BASES_QUERY_CACHE_CAPACITY: usize = 16;

#[derive(Debug, Clone, PartialEq, Eq, PartialOrd, Ord)]
struct CacheKey {
    query_hash: String,
    generation: u64,
    this_path: Option<String>,
    today_key: Option<i64>,
}

impl BasesQueryCache {
    #[cfg(test)]
    pub(crate) fn test_entry_generations(&self) -> Vec<u64> {
        self.entries
            .borrow()
            .keys()
            .map(|key| key.generation)
            .collect()
    }

    fn get(&self, key: &CacheKey) -> Option<BasesResultSet> {
        self.evict_stale_generations(key.generation);
        let result = self.entries.borrow().get(key).cloned()?;
        self.touch(key);
        Some(result)
    }

    fn insert(&self, key: CacheKey, result: BasesResultSet) {
        self.evict_stale_generations(key.generation);
        self.entries.borrow_mut().insert(key.clone(), result);
        self.touch(&key);
        while self.entries.borrow().len() > BASES_QUERY_CACHE_CAPACITY {
            let Some(oldest) = self.recency.borrow_mut().pop_front() else {
                break;
            };
            self.entries.borrow_mut().remove(&oldest);
        }
    }

    fn evict_stale_generations(&self, generation: u64) {
        self.entries
            .borrow_mut()
            .retain(|key, _| key.generation == generation);
        self.recency
            .borrow_mut()
            .retain(|key| key.generation == generation);
    }

    fn touch(&self, key: &CacheKey) {
        let mut recency = self.recency.borrow_mut();
        if let Some(index) = recency.iter().position(|candidate| candidate == key) {
            recency.remove(index);
        }
        recency.push_back(key.clone());
    }
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
struct FormulaPlan {
    dependencies: Vec<BTreeSet<String>>,
    circular: BTreeSet<String>,
    unresolvable: BTreeSet<usize>,
    order: Vec<usize>,
}

impl FormulaPlan {
    fn new(query: &SlateQuery) -> Self {
        let known_names = query
            .formulas
            .iter()
            .map(|(name, _)| name.clone())
            .collect::<BTreeSet<_>>();
        let dependencies = query
            .formulas
            .iter()
            .map(|(_, expr)| {
                let mut dependencies = BTreeSet::new();
                collect_formula_dependencies(expr, &mut dependencies);
                dependencies.retain(|name| known_names.contains(name));
                dependencies
            })
            .collect::<Vec<_>>();
        let dependency_graph = query
            .formulas
            .iter()
            .zip(&dependencies)
            .map(|((name, _), dependencies)| (name.clone(), dependencies.clone()))
            .collect::<BTreeMap<_, _>>();
        let circular = known_names
            .iter()
            .filter(|name| formula_reaches(name, name, &dependency_graph, &mut BTreeSet::new()))
            .cloned()
            .collect::<BTreeSet<_>>();

        let mut resolved = circular.clone();
        let mut pending = query
            .formulas
            .iter()
            .map(|(name, _)| !circular.contains(name))
            .collect::<Vec<_>>();
        let mut order = query
            .formulas
            .iter()
            .enumerate()
            .filter_map(|(index, (name, _))| circular.contains(name).then_some(index))
            .collect::<Vec<_>>();
        let mut unresolvable = BTreeSet::new();
        while pending.iter().any(|pending| *pending) {
            let mut progressed = false;
            for (index, (name, _)) in query.formulas.iter().enumerate() {
                if pending[index]
                    && dependencies[index]
                        .iter()
                        .all(|dependency| resolved.contains(dependency))
                {
                    order.push(index);
                    resolved.insert(name.clone());
                    pending[index] = false;
                    progressed = true;
                }
            }
            if !progressed {
                order.extend(
                    pending
                        .iter_mut()
                        .enumerate()
                        .filter_map(|(index, pending)| {
                            if *pending {
                                *pending = false;
                                unresolvable.insert(index);
                                Some(index)
                            } else {
                                None
                            }
                        }),
                );
                break;
            }
        }

        Self {
            dependencies,
            circular,
            unresolvable,
            order,
        }
    }
}

#[derive(Debug, Clone, Default)]
struct SqlPlan {
    clauses: Vec<String>,
    params: Vec<String>,
}

#[cfg(test)]
struct TestMaterializationProbe {
    after_rows: usize,
    reached: std::sync::mpsc::Sender<usize>,
    release: std::sync::mpsc::Receiver<()>,
}

#[cfg(test)]
std::thread_local! {
    static TEST_MATERIALIZATION_PROBE: RefCell<Option<TestMaterializationProbe>> =
        const { RefCell::new(None) };
}

#[cfg(test)]
fn install_test_materialization_probe(probe: TestMaterializationProbe) {
    TEST_MATERIALIZATION_PROBE.with(|slot| {
        let previous = slot.replace(Some(probe));
        assert!(
            previous.is_none(),
            "materialization probe already installed"
        );
    });
}

#[cfg(test)]
fn park_on_test_materialization_probe(materialized_rows: usize) {
    let probe = TEST_MATERIALIZATION_PROBE.with(|slot| {
        let mut slot = slot.borrow_mut();
        if slot
            .as_ref()
            .is_some_and(|probe| probe.after_rows == materialized_rows)
        {
            slot.take()
        } else {
            None
        }
    });
    if let Some(probe) = probe {
        probe
            .reached
            .send(materialized_rows)
            .expect("cancellation test receives materialization signal");
        probe
            .release
            .recv()
            .expect("cancellation test releases engine worker");
    }
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

    if let Some(cache) = ctx.cache {
        cache.evict_stale_generations(ctx.generation);
    }

    let cache_key = cache_key(query, ctx);
    if let (Some(cache), Some(key)) = (ctx.cache, cache_key.as_ref())
        && let Some(mut cached) = cache.get(key)
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
    let formula_plan = FormulaPlan::new(query);
    let load_deps = CandidateLoadDeps {
        fts: &fts,
        warnings: &warnings,
        cancel,
    };
    let this = ctx
        .this_path
        .as_deref()
        .and_then(|path| assemble_row_for_path(conn, path, &vault).transpose())
        .transpose()?;

    let mut rows = Vec::new();
    let mut offset = 0usize;
    let page_size = ctx.page_size.max(1);
    loop {
        if cancel.is_cancelled() {
            return Err(VaultError::Cancelled);
        }
        let batch = match load_candidates(query, conn, ctx, page_size, offset, &load_deps, &vault) {
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
            let formulas = eval_formulas(
                query,
                &formula_plan,
                &candidate.row,
                this.as_ref(),
                ctx,
                &vault,
                &warnings,
            );
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
                Ok(row) => {
                    rows.push(row);
                    #[cfg(test)]
                    park_on_test_materialization_probe(rows.len());
                }
                Err(error) => {
                    return Ok(error_result(query, ctx, error, &candidate.row.file_path));
                }
            }
        }
        offset += page_size;
    }

    let unfiltered_total_count = rows.len();
    apply_quick_filter(&mut rows, ctx.quick_filter);
    if let Err(error) = sort_rows(query, &mut rows) {
        return Ok(error_result(query, ctx, format!("sort: {error}"), ""));
    }
    let group_slices = group_rows(query, &mut rows);
    let total_count = rows.len();
    let mut result_warnings = warnings.messages();
    let summaries = compute_summaries(
        query,
        &rows,
        0..rows.len(),
        ctx,
        &vault,
        &warnings,
        &mut result_warnings,
    );
    let group_summaries = group_slices
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
        .collect::<Vec<_>>();
    let shown_count = query
        .limit
        .map(|limit| (limit as usize).min(total_count))
        .unwrap_or(total_count);
    let unfiltered_shown_count = query
        .limit
        .map(|limit| (limit as usize).min(unfiltered_total_count))
        .unwrap_or(unfiltered_total_count);
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
        unfiltered_shown_count,
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
        cache.insert(key, result.clone());
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
    vault: &SqlVaultLookup<'_>,
) -> Result<Vec<Candidate>, CandidateLoadError> {
    match query.row_source {
        RowSource::Files => load_file_candidates(query, conn, ctx, limit, offset, deps, vault),
        RowSource::Tasks => load_task_candidates(query, conn, ctx, limit, offset, deps, vault),
    }
}

fn load_file_candidates(
    query: &SlateQuery,
    conn: &Connection,
    ctx: &EngineCtx<'_>,
    limit: usize,
    offset: usize,
    deps: &CandidateLoadDeps<'_>,
    vault: &SqlVaultLookup<'_>,
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
            } else if let Some(result) = oplog_pushdown_predicate(conjunct, ctx.now_ms) {
                let predicate = result.map_err(CandidateLoadError::View)?;
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
            assemble_row(conn, &file, task_counts, None, vault)
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
    vault: &SqlVaultLookup<'_>,
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
            } else if let Some(result) = oplog_pushdown_predicate(conjunct, ctx.now_ms) {
                let predicate = result.map_err(CandidateLoadError::View)?;
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
            assemble_row(conn, &file, task_counts, Some(task), vault)
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
            let lookback_ms = i128::from(*days).saturating_mul(86_400_000);
            let cutoff = i128::from(ctx.now_ms).saturating_sub(lookback_ms);
            let cutoff = i64::try_from(cutoff).unwrap_or(i64::MIN);
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

/// The O-6 (#544) operator duration grammar, pinned by the spec:
/// `^([1-9][0-9]*)(h|d|w)$` — a deliberate strict subset of the
/// expression language's richer `duration()` grammar (documented
/// divergence; months/years are calendar-dependent and excluded).
/// Returns the window in milliseconds.
pub(crate) fn parse_operator_duration(text: &str) -> Option<i64> {
    let unit = text.chars().last()?;
    let digits = &text[..text.len() - unit.len_utf8()];
    if digits.is_empty() || digits.starts_with('0') || !digits.bytes().all(|b| b.is_ascii_digit()) {
        return None;
    }
    let count: i64 = digits.parse().ok()?;
    let unit_ms: i64 = match unit {
        'h' => 60 * 60 * 1000,
        'd' => 24 * 60 * 60 * 1000,
        'w' => 7 * 24 * 60 * 60 * 1000,
        _ => return None,
    };
    count.checked_mul(unit_ms)
}

/// Escape `pattern` for a SQLite LIKE with `ESCAPE '\'`.
fn like_escape(pattern: &str) -> String {
    let mut out = String::with_capacity(pattern.len());
    for c in pattern.chars() {
        if matches!(c, '%' | '_' | '\\') {
            out.push('\\');
        }
        out.push(c);
    }
    out
}

/// Which O-6 temporal operator (with literal args) an expression is.
enum OplogOperator {
    HasChangeSince { duration: String },
    HasPropertyChange { key: String, duration: String },
    DeletedContentMatches { pattern: String, duration: String },
    CreatedSince { duration: String },
    UntouchedFor { duration: String },
}

/// Recognize an `oplog.*` operator call with LITERAL args (the
/// pushdown-able shape — non-literal args fall to row-by-row eval).
fn oplog_operator_call(expr: &Expr) -> Option<OplogOperator> {
    let ExprKind::Call {
        callee: Callee::Method { name, .. },
        args,
    } = &expr.kind
    else {
        return None;
    };
    // Exact arity or no pushdown (adversarial round 3): extra args
    // must fall through to row-eval, whose arity check rejects them —
    // otherwise the identical wrong expression works at a top-level
    // AND but errors inside an OR.
    match name {
        MethodName::OplogHasChangeSince if args.len() == 1 => Some(OplogOperator::HasChangeSince {
            duration: literal_text(args.first()?)?,
        }),
        MethodName::OplogHasPropertyChange if args.len() == 2 => {
            Some(OplogOperator::HasPropertyChange {
                key: literal_text(args.first()?)?,
                duration: literal_text(args.get(1)?)?,
            })
        }
        MethodName::OplogDeletedContentMatches if args.len() == 2 => {
            Some(OplogOperator::DeletedContentMatches {
                pattern: literal_text(args.first()?)?,
                duration: literal_text(args.get(1)?)?,
            })
        }
        MethodName::OplogCreatedSince if args.len() == 1 => Some(OplogOperator::CreatedSince {
            duration: literal_text(args.first()?)?,
        }),
        MethodName::OplogUntouchedFor if args.len() == 1 => Some(OplogOperator::UntouchedFor {
            duration: literal_text(args.first()?)?,
        }),
        _ => None,
    }
}

/// SQL lowering for one recognized operator (O-6 #544): membership
/// subqueries over `oplog_events`, the tag/property-predicate
/// convention — unqualified `id` binds to `files` on both the file and
/// task candidate paths. `Err` = an operator with an invalid duration
/// (surfaced as an in-band view error, the `file.matches` precedent).
fn oplog_pushdown_predicate(
    node: &FilterNode,
    now_ms: i64,
) -> Option<Result<SqlPredicate, String>> {
    let FilterNode::Stmt(expr) = node else {
        return None;
    };
    let operator = oplog_operator_call(expr)?;
    let lower = |duration: &str, name: &str| -> Result<i64, String> {
        parse_operator_duration(duration).map(|w| now_ms.saturating_sub(w)).ok_or_else(|| {
            format!(
                "{name}: duration {duration:?} must match ^([1-9][0-9]*)(h|d|w)$ and fit the supported range (e.g. \"7d\")"
            )
        })
    };
    Some(match operator {
        OplogOperator::HasChangeSince { duration } => lower(&duration, "oplog.has_change_since")
            .map(|cutoff| SqlPredicate {
                clause: "id IN (
                    SELECT file_id FROM oplog_events
                    WHERE event_class = 1 AND ts_ms >= ?
                )"
                .to_string(),
                params: vec![cutoff.to_string()],
            }),
        OplogOperator::HasPropertyChange { key, duration } => {
            lower(&duration, "oplog.has_property_change").map(|cutoff| SqlPredicate {
                clause: "id IN (
                    SELECT file_id FROM oplog_events
                    WHERE event_class IN (2, 3, 5)
                      AND (property_key = ? OR event_class = 5)
                      AND ts_ms >= ?
                )"
                .to_string(),
                params: vec![key, cutoff.to_string()],
            })
        }
        OplogOperator::DeletedContentMatches { pattern, duration } => {
            lower(&duration, "oplog.deleted_content_matches").map(|cutoff| SqlPredicate {
                clause: "id IN (
                    SELECT file_id FROM oplog_events
                    WHERE event_class = 1 AND ts_ms >= ?
                      AND deleted_text LIKE '%' || ? || '%' ESCAPE '\\'
                )"
                .to_string(),
                params: vec![cutoff.to_string(), like_escape(&pattern)],
            })
        }
        // #801: filesystem birth time — compaction/rebuild-stable
        // (event rows shift with retention folds; birth doesn't).
        // birthtime 0 = unknown ⇒ never matches (documented).
        OplogOperator::CreatedSince { duration } => {
            lower(&duration, "oplog.created_since").map(|cutoff| SqlPredicate {
                clause: "(birthtime_ms > 0 AND birthtime_ms >= ?)".to_string(),
                params: vec![cutoff.to_string()],
            })
        }
        // #801: untouched by BOTH signals — mtime (external writes)
        // and class-1 events (Slate saves). A never-logged, old-mtime
        // file is vacuously untouched (documented).
        OplogOperator::UntouchedFor { duration } => {
            lower(&duration, "oplog.untouched_for").map(|cutoff| SqlPredicate {
                clause: "(mtime_ms < ? AND id NOT IN (
                    SELECT file_id FROM oplog_events
                    WHERE event_class = 1 AND ts_ms >= ?
                ))"
                .to_string(),
                params: vec![cutoff.to_string(), cutoff.to_string()],
            })
        }
    })
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
            nullable_task_comparison_clause("t.priority", op, numeric_literal_string(value)?)
        }
        TaskField::Due => {
            nullable_task_comparison_clause("t.due_ms", op, date_literal_ms_string(value)?)
        }
        TaskField::Scheduled => {
            nullable_task_comparison_clause("t.scheduled_ms", op, date_literal_ms_string(value)?)
        }
        TaskField::Text | TaskField::File => None,
    }
}

fn nullable_task_comparison_clause(
    column: &str,
    op: BinaryOp,
    value: String,
) -> Option<SqlPredicate> {
    // Ordering a missing task field is false *and emits a view warning*.
    // SQL could reproduce the membership but would discard the Null rows
    // before Rust can preserve that observable warning, so only the
    // warning-free equality family is safe to push down.
    if matches!(op, BinaryOp::Eq | BinaryOp::Ne) {
        task_comparison_clause(column, op, value, true)
    } else {
        None
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
    vault: &SqlVaultLookup<'_>,
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
    let outgoing = load_outgoing_links(conn, file.id)?;
    file_fields.links = outgoing.links;
    file_fields.embeds = outgoing.embeds;
    file_fields.backlinks = load_backlinks(conn, &file.path)?;
    file_fields.out_degree = (file_fields.links.len() + file_fields.embeds.len()) as u64;
    file_fields.in_degree = file_fields.backlinks.len() as u64;
    let mut properties = load_properties(conn, file.id)?;
    for (_, value) in &mut properties {
        resolve_property_links(value, &file.path, vault);
    }
    for (key, value) in &properties {
        file_fields.properties.insert(key.clone(), value.clone());
    }
    file_fields.aliases = match file_fields.properties.get("aliases") {
        Some(Value::Text(alias)) => vec![alias.clone()],
        Some(Value::List(aliases)) => aliases
            .iter()
            .filter_map(|alias| match alias {
                Value::Text(alias) => Some(alias.clone()),
                _ => None,
            })
            .collect(),
        _ => Vec::new(),
    };
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

fn assemble_row_for_path(
    conn: &Connection,
    path: &str,
    vault: &SqlVaultLookup<'_>,
) -> Result<Option<RowContext>, VaultError> {
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
        assemble_row(conn, &file, task_counts, None, vault)
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
        .prepare_cached(
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
            .map(|target| property_wikilink_to_bases_value(target.to_string()))
            .unwrap_or(Value::Null),
        "list" | "tag_list" => Value::List(
            json.as_array()
                .map(|items| items.iter().map(json_to_value).collect())
                .unwrap_or_default(),
        ),
        _ => Value::Null,
    }
}

fn property_value_to_bases_value(value: crate::frontmatter::PropertyValue) -> Value {
    use crate::frontmatter::PropertyValue;

    match value {
        PropertyValue::Text(value) => Value::Text(value),
        PropertyValue::Integer(value) => Value::Number(value as f64),
        PropertyValue::Float(value) => Value::Number(value),
        PropertyValue::Boolean(value) => Value::Bool(value),
        PropertyValue::Date(value) => parse_date_value(&value)
            .map(Value::Date)
            .unwrap_or(Value::Null),
        PropertyValue::Datetime(value) => parse_datetime_value(&value)
            .map(Value::Date)
            .unwrap_or(Value::Null),
        PropertyValue::Wikilink(target) => property_wikilink_to_bases_value(target),
        PropertyValue::List(values) => Value::List(
            values
                .into_iter()
                .map(property_value_to_bases_value)
                .collect(),
        ),
        PropertyValue::TagList(values) => {
            Value::List(values.into_iter().map(Value::Text).collect())
        }
    }
}

fn property_wikilink_to_bases_value(authored: String) -> Value {
    let source = format!("[[{authored}]]");
    let mut parsed = crate::extract_links(&source);
    if parsed.len() == 1 {
        let link = parsed.pop().expect("length checked");
        if link.kind == crate::LinkKind::Wikilink
            && link.span_start == 0
            && link.span_end == source.len()
            && !link.is_embed
        {
            let (link_type, subpath) = match link.anchor {
                Some(crate::LinkAnchor::Heading(header)) => {
                    ("header", Some(normalize_header_for_link(&header)))
                }
                Some(crate::LinkAnchor::Block(block)) => ("block", Some(block)),
                None => ("file", None),
            };
            return Value::Link(LinkValue {
                target: link.target_raw,
                display: link.display_text,
                resolved_path: None,
                subpath,
                link_type: link_type.to_string(),
                embed: false,
            });
        }
    }

    // Preserve the previous behavior for malformed cached rows. Valid
    // frontmatter wikilinks take the shared scanner path above.
    Value::Link(LinkValue {
        target: authored,
        display: None,
        resolved_path: None,
        subpath: None,
        link_type: "file".to_string(),
        embed: false,
    })
}

fn json_to_value(value: &serde_json::Value) -> Value {
    if let Some(value) = crate::properties_db::tagged_list_element_to_property_value(value) {
        return property_value_to_bases_value(value);
    }
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

fn resolve_property_links(value: &mut Value, owner_path: &str, vault: &SqlVaultLookup<'_>) {
    match value {
        Value::Link(link) => {
            if link.resolved_path.is_none() {
                link.resolved_path = vault.resolve_link_from(&link.target, owner_path);
            }
        }
        Value::List(values) => {
            for value in values {
                resolve_property_links(value, owner_path, vault);
            }
        }
        Value::Object(values) => {
            for value in values.values_mut() {
                resolve_property_links(value, owner_path, vault);
            }
        }
        Value::Null
        | Value::Bool(_)
        | Value::Number(_)
        | Value::Text(_)
        | Value::Date(_)
        | Value::DqlDate(_)
        | Value::Duration(_)
        | Value::DqlDuration(_)
        | Value::File(_)
        | Value::Regex(_, _) => {}
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
        .prepare_cached("SELECT tag_norm FROM file_tags WHERE file_id = ?1 ORDER BY tag_norm")
        .map_err(DbError::from)?;
    stmt.query_map(params![file_id], |row| row.get::<_, String>(0))
        .map_err(DbError::from)?
        .collect::<Result<Vec<_>, _>>()
        .map_err(|err| DbError::from(err).into())
}

#[derive(Debug, Default)]
struct OutgoingLinks {
    all: Vec<LinkValue>,
    links: Vec<LinkValue>,
    embeds: Vec<LinkValue>,
}

fn load_outgoing_links(conn: &Connection, file_id: i64) -> Result<OutgoingLinks, VaultError> {
    let mut stmt = conn
        .prepare_cached(
            "SELECT target_raw, target_path, target_anchor, is_embed, display_text
             FROM links
             WHERE source_file_id = ?1
               AND is_external = 0
               AND (target_anchor IS NULL OR target_anchor LIKE 'h:%' OR target_anchor LIKE 'b:%')
             ORDER BY ordinal",
        )
        .map_err(DbError::from)?;
    let rows = stmt.query_map(params![file_id], |row| {
        let target: String = row.get(0)?;
        let resolved_path: Option<String> = row.get(1)?;
        let anchor: Option<String> = row.get(2)?;
        let is_embed = row.get::<_, i64>(3)? != 0;
        let display: Option<String> = row.get(4)?;
        let (link_type, subpath) = match anchor.as_deref() {
            Some(anchor) if anchor.starts_with("h:") => {
                ("header", Some(normalize_header_for_link(&anchor[2..])))
            }
            Some(anchor) if anchor.starts_with("b:") => ("block", Some(anchor[2..].to_string())),
            Some(_) => unreachable!("SQL filters unknown serialized anchor prefixes"),
            None => ("file", None),
        };
        Ok((
            LinkValue {
                target,
                display,
                resolved_path,
                subpath,
                link_type: link_type.to_string(),
                embed: is_embed,
            },
            is_embed,
        ))
    })?;
    let mut outgoing = OutgoingLinks::default();
    let mut seen_pages = BTreeSet::new();
    for row in rows {
        let (link, is_embed) = row.map_err(DbError::from)?;
        let page_identity = link
            .resolved_path
            .as_deref()
            .unwrap_or(link.target.as_str())
            .to_string();
        if seen_pages.insert(page_identity) {
            outgoing.all.push(link.clone());
        }
        if is_embed {
            outgoing.embeds.push(link);
        } else {
            outgoing.links.push(link);
        }
    }
    Ok(outgoing)
}

fn normalize_header_for_link(header: &str) -> String {
    let mut normalized = String::new();
    let mut pending_space = false;
    for ch in header.chars() {
        if ch.is_alphanumeric() || matches!(ch, '_' | '-') || !ch.is_ascii() && !ch.is_whitespace()
        {
            if pending_space && !normalized.is_empty() {
                normalized.push(' ');
            }
            pending_space = false;
            normalized.push(ch);
        } else {
            pending_space = true;
        }
    }
    normalized
}

fn load_backlinks(conn: &Connection, path: &str) -> Result<Vec<LinkValue>, VaultError> {
    let mut stmt = conn
        .prepare_cached(
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
            subpath: None,
            link_type: "file".to_string(),
            embed: false,
        })
    })
    .map_err(DbError::from)?
    .collect::<Result<Vec<_>, _>>()
    .map_err(|err| DbError::from(err).into())
}

fn eval_formulas(
    query: &SlateQuery,
    plan: &FormulaPlan,
    row: &RowContext,
    this: Option<&RowContext>,
    ctx: &EngineCtx<'_>,
    vault: &dyn VaultLookup,
    warnings: &WarningSink,
) -> FormulaEval {
    let mut state = FormulaEval::default();
    for index in &plan.order {
        let (name, expr) = &query.formulas[*index];
        if plan.circular.contains(name) {
            state.errors.insert(
                name.clone(),
                EvalError::Unsupported {
                    reason: format!("formula.{name} participates in a circular formula reference"),
                },
            );
            continue;
        }
        if plan.unresolvable.contains(index) {
            state.errors.insert(
                name.clone(),
                EvalError::Unsupported {
                    reason: "unresolvable formula dependency".to_string(),
                },
            );
            continue;
        }

        if let Some((dependency, error)) = plan.dependencies[*index].iter().find_map(|dependency| {
            state
                .errors
                .get(dependency)
                .map(|error| (dependency, error))
        }) {
            state.errors.insert(
                name.clone(),
                EvalError::Unsupported {
                    reason: format!("formula.{dependency}: {error}"),
                },
            );
        } else {
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
    }
    state
}

fn formula_reaches(
    current: &str,
    target: &str,
    graph: &BTreeMap<String, BTreeSet<String>>,
    seen: &mut BTreeSet<String>,
) -> bool {
    let Some(dependencies) = graph.get(current) else {
        return false;
    };
    dependencies.iter().any(|dependency| {
        dependency == target
            || seen.insert(dependency.clone()) && formula_reaches(dependency, target, graph, seen)
    })
}

fn collect_formula_dependencies(expr: &Expr, dependencies: &mut BTreeSet<String>) {
    match &expr.kind {
        ExprKind::Prop(PropertyRef::Formula(name)) => {
            dependencies.insert(name.clone());
        }
        ExprKind::Index { base, index }
        | ExprKind::Binary {
            lhs: base,
            rhs: index,
            ..
        } => {
            collect_formula_dependencies(base, dependencies);
            collect_formula_dependencies(index, dependencies);
        }
        ExprKind::Field { base, .. } | ExprKind::Unary { rhs: base, .. } => {
            collect_formula_dependencies(base, dependencies);
        }
        ExprKind::Call { callee, args } => {
            if let Callee::Method { receiver, .. } = callee {
                collect_formula_dependencies(receiver, dependencies);
            }
            for arg in args {
                collect_formula_dependencies(arg, dependencies);
            }
        }
        ExprKind::ListExpr {
            base, body, init, ..
        } => {
            collect_formula_dependencies(base, dependencies);
            collect_formula_dependencies(body, dependencies);
            if let Some(init) = init {
                collect_formula_dependencies(init, dependencies);
            }
        }
        ExprKind::Lit(Lit::List(items)) => {
            for item in items {
                collect_formula_dependencies(item, dependencies);
            }
        }
        ExprKind::Lit(Lit::Object(items)) => {
            for (_, value) in items {
                collect_formula_dependencies(value, dependencies);
            }
        }
        ExprKind::Lit(_) | ExprKind::Prop(_) | ExprKind::Unsupported { .. } => {}
    }
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
    // A conjunct is SQL-handled when it is a file.matches or a
    // literal-arg oplog operator (both pushed above); their Rust
    // re-evaluation is skipped. Non-literal oplog args never push
    // down, so they still eval row-by-row here.
    let sql_handled = |expr: &Expr| -> bool {
        file_matches_query(expr).is_some() || oplog_operator_call(expr).is_some()
    };
    match node {
        FilterNode::Stmt(expr) if sql_handled(expr) => Ok(true),
        FilterNode::And(nodes) => {
            for node in nodes {
                if matches!(node, FilterNode::Stmt(expr) if sql_handled(expr)) {
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

fn sort_rows(query: &SlateQuery, rows: &mut [MaterializedRow]) -> Result<(), EvalError> {
    for row in rows.iter() {
        for value in &row.sort_values {
            if let Some(value) = dql_command_sort_value(value) {
                compare_dql_command_sort_values(value, value)?;
            }
        }
    }

    let mut first_error = None;
    rows.sort_by(|lhs, rhs| {
        if first_error.is_some() {
            return Ordering::Equal;
        }
        match compare_rows(query, lhs, rhs) {
            Ok(ordering) => ordering,
            Err(error) => {
                first_error = Some(error);
                Ordering::Equal
            }
        }
    });
    if let Some(error) = first_error {
        Err(error)
    } else {
        Ok(())
    }
}

fn compare_rows(
    query: &SlateQuery,
    lhs: &MaterializedRow,
    rhs: &MaterializedRow,
) -> Result<Ordering, EvalError> {
    for (index, sort) in query.sort.iter().enumerate() {
        let ordering = compare_row_sort_value(
            &lhs.sort_values[index],
            &rhs.sort_values[index],
            sort.ascending,
        )?;
        if ordering != Ordering::Equal {
            return Ok(ordering);
        }
    }
    Ok(match lhs.path.cmp(&rhs.path) {
        Ordering::Equal => lhs.ordinal.cmp(&rhs.ordinal),
        ordering => ordering,
    })
}

fn compare_row_sort_value(
    lhs: &Value,
    rhs: &Value,
    ascending: bool,
) -> Result<Ordering, EvalError> {
    let ordering = match (dql_command_sort_value(lhs), dql_command_sort_value(rhs)) {
        (Some(lhs), Some(rhs)) => compare_dql_command_sort_values_total(lhs, rhs)?,
        (None, None) => return Ok(compare_sort_key(lhs, rhs, ascending)),
        _ => {
            return Err(EvalError::InvalidArgument {
                function: "DQL SORT".to_string(),
                message: "inconsistent command-sort provenance".to_string(),
            });
        }
    };
    Ok(if ascending {
        ordering
    } else {
        ordering.reverse()
    })
}

fn compare_dql_command_sort_values_total(lhs: &Value, rhs: &Value) -> Result<Ordering, EvalError> {
    let lhs_reflexive = compare_dql_command_sort_values(lhs, lhs)?;
    let rhs_reflexive = compare_dql_command_sort_values(rhs, rhs)?;
    let forward = compare_dql_command_sort_values(lhs, rhs)?;
    let reverse = compare_dql_command_sort_values(rhs, lhs)?;
    if lhs_reflexive == Ordering::Equal
        && rhs_reflexive == Ordering::Equal
        && forward == reverse.reverse()
    {
        return Ok(forward);
    }

    // Dataview's expression comparator deliberately exposes source behavior
    // such as NaN > NaN and distinct duration structures with equal casual
    // milliseconds comparing greater in both directions. Keep those expression
    // semantics intact, but never pass a non-total comparator to Rust's sort.
    // Native Bases already has a deterministic total value order, so use it
    // only for the inconsistent pair.
    Ok(compare_sort_key(lhs, rhs, true))
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
        (Value::Number(lhs), Value::Number(rhs)) => {
            numeric_order_key(*lhs).cmp(&numeric_order_key(*rhs))
        }
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
        Value::Date(_) | Value::DqlDate(_) => 2,
        Value::Duration(_) | Value::DqlDuration(_) => 3,
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
) -> Vec<BasesSummaryCell> {
    query
        .summaries
        .iter()
        .map(|(column_id, summary)| {
            let fallback_name = match summary {
                SummaryRef::Builtin(name) | SummaryRef::Custom(name) => name.clone(),
            };
            let (summary_name, kind) = match summary_kind(summary) {
                Ok(summary) => summary,
                Err(error) => return summary_error_cell(column_id, &fallback_name, error),
            };
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
            .unwrap_or_else(|error| summary_error_cell(column_id, &summary_name, error))
        })
        .collect()
}

fn summary_error_cell(column_id: &str, summary_name: &str, error: String) -> BasesSummaryCell {
    BasesSummaryCell {
        column_id: column_id.to_string(),
        summary: summary_name.to_string(),
        value: CellValue::Error(error),
    }
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
        Value::DqlDate(value) => format!(
            "dql-date:{}:{}:{}:{}",
            value.epoch_ms, value.has_time, value.offset_minutes, value.is_local
        ),
        Value::Duration(value) => format!("duration:{value}"),
        Value::DqlDuration(value) => {
            format!(
                "dql-duration:{:?}:{:?}:{:?}:{:?}:{:?}:{:?}:{:?}:{:?}",
                value.years,
                value.months,
                value.weeks,
                value.days,
                value.hours,
                value.minutes,
                value.seconds,
                value.milliseconds
            )
        }
        Value::Text(value) => format!("text:{value}"),
        Value::Link(value) => format!("link:{}", link_identity(value)),
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

/// ASCII key whose lexicographic order matches the native ascending value
/// comparator. UI-only grids use it without reimplementing Rust value rules.
pub(crate) fn value_sort_key(value: &Value) -> String {
    match value {
        Value::Null => "ff".to_string(),
        Value::Bool(false) => "00:0".to_string(),
        Value::Bool(true) => "00:1".to_string(),
        Value::Number(value) => fixed_width_u64_sort_key("01:", numeric_order_key(*value)),
        Value::Date(value) => {
            let ordered = (value.epoch_ms as u64) ^ (1_u64 << 63);
            fixed_width_u64_sort_key("02:00:", ordered)
        }
        Value::DqlDate(_) => hex_sort_key("02:01:", &value_key(value)),
        Value::Duration(_) | Value::DqlDuration(_) => hex_sort_key("03:", &value_key(value)),
        Value::Text(value) => lowercase_text_sort_key(value),
        Value::Link(_) => hex_sort_key("05:", &value_key(value)),
        Value::File(_) => hex_sort_key("06:", &value_key(value)),
        Value::Regex(_, _) => hex_sort_key("07:", &value_key(value)),
        Value::List(_) => hex_sort_key("08:", &value_key(value)),
        Value::Object(_) => hex_sort_key("09:", &value_key(value)),
    }
}

/// Total numeric order shared by native row sorting and mirrored UI keys:
/// -Infinity < finite values < +Infinity < NaN. Signed zeroes compare equal,
/// and all NaN signs/payloads collapse to one canonical position.
fn numeric_order_key(value: f64) -> u64 {
    const CANONICAL_NAN_BITS: u64 = 0x7ff8_0000_0000_0000;

    let bits = if value.is_nan() {
        CANONICAL_NAN_BITS
    } else if value == 0.0 {
        0.0_f64.to_bits()
    } else {
        value.to_bits()
    };
    if bits & (1_u64 << 63) == 0 {
        bits ^ (1_u64 << 63)
    } else {
        !bits
    }
}

fn fixed_width_u64_sort_key(prefix: &str, value: u64) -> String {
    const HEX: &[u8; 16] = b"0123456789abcdef";
    let mut encoded = String::with_capacity(prefix.len() + 16);
    encoded.push_str(prefix);
    for shift in (0..64).step_by(4).rev() {
        encoded.push(HEX[((value >> shift) & 0x0f) as usize] as char);
    }
    encoded
}

fn hex_sort_key(prefix: &str, value: &str) -> String {
    let mut encoded = String::with_capacity(prefix.len() + value.len() * 2);
    encoded.push_str(prefix);
    push_sort_key_hex(&mut encoded, value.as_bytes());
    encoded
}

fn lowercase_text_sort_key(value: &str) -> String {
    let mut encoded = String::with_capacity(3 + value.len() * 2);
    encoded.push_str("04:");
    let mut utf8 = [0_u8; 4];
    for ch in value.chars().flat_map(char::to_lowercase) {
        push_sort_key_hex(&mut encoded, ch.encode_utf8(&mut utf8).as_bytes());
    }
    encoded
}

fn push_sort_key_hex(encoded: &mut String, value: &[u8]) {
    const HEX: &[u8; 16] = b"0123456789abcdef";
    for &byte in value {
        encoded.push(HEX[(byte >> 4) as usize] as char);
        encoded.push(HEX[(byte & 0x0f) as usize] as char);
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
        Value::DqlDate(value) => crate::bases::eval::dql_date_display(*value),
        Value::Duration(value) => value.to_string(),
        Value::DqlDuration(value) => crate::bases::eval::dql_format_duration_parts(*value),
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
    // ColumnSelection IDs are property identifiers. Calls are the one explicit
    // expression-shaped compatibility surface (`now()`, `date(when)`, method
    // calls); every other non-namespaced string is a literal note-property ID.
    // Native `sort[].expr` is parsed separately and is not constrained here.
    if let Ok(expr) = super::expr::parse_expr(id)
        && matches!(expr.kind, ExprKind::Call { .. })
    {
        return expr;
    }
    super::property_id_expr(id).unwrap_or_else(|| Expr {
        span: super::expr::Span { start: 0, end: 0 },
        kind: ExprKind::Prop(PropertyRef::Note(id.to_string())),
    })
}

pub(crate) fn sort_key_for_column_id(id: &str, ascending: bool) -> SortKey {
    SortKey {
        expr: column_expr(id),
        ascending,
    }
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
        unfiltered_shown_count: 0,
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
    if query_mentions_global(query, GlobalFn::Now)
        || query_mentions_dql_date_shorthand(query)
        || query_mentions_oplog_operator(query)
    {
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
    query_matches_expr(query, &|expr| {
        matches!(
            &expr.kind,
            ExprKind::Call {
                callee: Callee::Global(function),
                ..
            } if *function == needle
        )
    })
}

/// O-6 (#544): temporal operators are wall-clock-dependent — their
/// truth changes with time, not vault generation — so queries carrying
/// them are never cached (the `now()` carve-out precedent).
fn query_mentions_oplog_operator(query: &SlateQuery) -> bool {
    query_matches_expr(query, &|expr| {
        matches!(
            &expr.kind,
            ExprKind::Call {
                callee: Callee::Method { name, .. },
                ..
            } if matches!(
                name,
                MethodName::OplogHasChangeSince
                    | MethodName::OplogHasPropertyChange
                    | MethodName::OplogDeletedContentMatches
                    | MethodName::OplogCreatedSince
                    | MethodName::OplogUntouchedFor
            )
        )
    })
}

fn query_mentions_dql_date_shorthand(query: &SlateQuery) -> bool {
    query_matches_expr(query, &expr_is_dql_date_shorthand)
}

fn query_matches_expr(query: &SlateQuery, predicate: &impl Fn(&Expr) -> bool) -> bool {
    matches!(query.source, QuerySource::Recent { .. })
        || query
            .filters
            .as_ref()
            .is_some_and(|filter| filter_matches_expr(filter, predicate))
        || query
            .formulas
            .iter()
            .any(|(_, expr)| expr_or_descendant_matches(expr, predicate))
        || query
            .custom_summaries
            .iter()
            .any(|(_, expr)| expr_or_descendant_matches(expr, predicate))
        || query
            .sort
            .iter()
            .any(|sort| expr_or_descendant_matches(&sort.expr, predicate))
        || query
            .columns
            .iter()
            .any(|column| expr_or_descendant_matches(&column_expr(&column.id), predicate))
        || query
            .summaries
            .iter()
            .any(|(column_id, _)| expr_or_descendant_matches(&column_expr(column_id), predicate))
}

fn filter_matches_expr(filter: &FilterNode, predicate: &impl Fn(&Expr) -> bool) -> bool {
    match filter {
        FilterNode::Stmt(expr) => expr_or_descendant_matches(expr, predicate),
        FilterNode::And(nodes) | FilterNode::Or(nodes) | FilterNode::Not(nodes) => nodes
            .iter()
            .any(|node| filter_matches_expr(node, predicate)),
    }
}

fn expr_or_descendant_matches(expr: &Expr, predicate: &impl Fn(&Expr) -> bool) -> bool {
    if predicate(expr) {
        return true;
    }
    match &expr.kind {
        ExprKind::Call { callee, args } => {
            matches!(callee, Callee::Method { receiver, .. } if expr_or_descendant_matches(receiver, predicate))
                || args
                    .iter()
                    .any(|arg| expr_or_descendant_matches(arg, predicate))
        }
        ExprKind::Index { base, index }
        | ExprKind::Binary {
            lhs: base,
            rhs: index,
            ..
        } => {
            expr_or_descendant_matches(base, predicate)
                || expr_or_descendant_matches(index, predicate)
        }
        ExprKind::Field { base, .. } | ExprKind::Unary { rhs: base, .. } => {
            expr_or_descendant_matches(base, predicate)
        }
        ExprKind::ListExpr {
            base, body, init, ..
        } => {
            expr_or_descendant_matches(base, predicate)
                || expr_or_descendant_matches(body, predicate)
                || init
                    .as_deref()
                    .is_some_and(|expr| expr_or_descendant_matches(expr, predicate))
        }
        ExprKind::Lit(Lit::List(items)) => items
            .iter()
            .any(|item| expr_or_descendant_matches(item, predicate)),
        ExprKind::Lit(Lit::Object(items)) => items
            .iter()
            .any(|(_, value)| expr_or_descendant_matches(value, predicate)),
        ExprKind::Lit(_) | ExprKind::Prop(_) | ExprKind::Unsupported { .. } => false,
    }
}

fn expr_is_dql_date_shorthand(expr: &Expr) -> bool {
    const DQL_DATE_OBJECT_KEY: &str = "\u{f8ff}slate.dql.date";

    let ExprKind::Call {
        callee: Callee::Global(GlobalFn::Date),
        args,
    } = &expr.kind
    else {
        return false;
    };
    let [
        Expr {
            kind:
                ExprKind::Call {
                    callee: Callee::Global(GlobalFn::Object),
                    args: marker_args,
                },
            ..
        },
    ] = args.as_slice()
    else {
        return false;
    };
    let [
        Expr {
            kind: ExprKind::Lit(Lit::String(marker)),
            ..
        },
        payload,
    ] = marker_args.as_slice()
    else {
        return false;
    };
    if marker != DQL_DATE_OBJECT_KEY {
        return false;
    }
    let ExprKind::Lit(Lit::String(value)) = &payload.kind else {
        // Dynamic DQL date inputs can evaluate to a shorthand at runtime.
        return true;
    };
    matches!(
        value.as_str(),
        "now"
            | "today"
            | "tomorrow"
            | "yesterday"
            | "sow"
            | "eow"
            | "som"
            | "eom"
            | "soy"
            | "eoy"
            | "start-of-week"
            | "end-of-week"
            | "start-of-month"
            | "end-of-month"
            | "start-of-year"
            | "end-of-year"
    )
}

struct SqlVaultLookup<'a> {
    conn: &'a Connection,
    fts: &'a FtsMatchCache,
    cancel: &'a CancelToken,
    source_path: &'a str,
    link_index: RefCell<Option<crate::InMemoryVaultIndex>>,
    link_resolutions: RefCell<BTreeMap<(String, String), Option<String>>>,
}

impl SqlVaultLookup<'_> {
    /// One EXISTS-shaped probe for the O-6 temporal-operator eval
    /// fallback (OR/NOT filter positions, where SQL pushdown can't
    /// fire).
    fn exists_query(
        &self,
        sql: &str,
        params: impl rusqlite::Params,
        function: &str,
    ) -> Result<bool, EvalError> {
        use rusqlite::OptionalExtension as _;
        self.conn
            .query_row(sql, params, |_| Ok(()))
            .optional()
            .map(|row| row.is_some())
            .map_err(|error| EvalError::InvalidArgument {
                function: function.to_string(),
                message: error.to_string(),
            })
    }

    fn dql_inline_value(
        &self,
        value: &crate::dql_inline_fields_db::DqlInlineValue,
        owner_path: &str,
        now_ms: i64,
    ) -> Result<Value, EvalError> {
        use crate::dql_inline_fields_db::{DqlInlineLinkType, DqlInlineValue};
        match value {
            DqlInlineValue::Null => Ok(Value::Null),
            DqlInlineValue::Boolean(value) => Ok(Value::Bool(*value)),
            DqlInlineValue::Number(value) => Ok(Value::Number(*value)),
            DqlInlineValue::Text(value) | DqlInlineValue::Tag(value) => {
                Ok(Value::Text(value.clone()))
            }
            DqlInlineValue::Date(value) => crate::bases::eval::dql_inline_date_value(value, now_ms),
            DqlInlineValue::Duration(value) => crate::bases::eval::dql_inline_duration_value(value),
            DqlInlineValue::Link(link) => {
                let resolved_path = self.resolve_link_from(&link.target, owner_path);
                Ok(Value::Link(LinkValue {
                    target: resolved_path.clone().unwrap_or_else(|| link.target.clone()),
                    display: link.display.clone(),
                    resolved_path,
                    subpath: link.subpath.clone(),
                    link_type: match link.link_type {
                        DqlInlineLinkType::File => "file",
                        DqlInlineLinkType::Header => "header",
                        DqlInlineLinkType::Block => "block",
                    }
                    .to_string(),
                    embed: link.embed,
                }))
            }
            DqlInlineValue::List(values) => values
                .iter()
                .map(|value| self.dql_inline_value(value, owner_path, now_ms))
                .collect::<Result<Vec<_>, _>>()
                .map(Value::List),
        }
    }

    fn resolve_link_from(&self, target: &str, source_path: &str) -> Option<String> {
        let cache_key = (source_path.to_string(), target.to_string());
        if let Some(resolved) = self.link_resolutions.borrow().get(&cache_key) {
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
        let resolved = match crate::resolve_link(target, None, source_path, index.as_ref()?) {
            crate::ResolvedLink::Resolved { target_path, .. } => Some(target_path),
            crate::ResolvedLink::Unresolved { .. } | crate::ResolvedLink::External => None,
        };
        self.link_resolutions
            .borrow_mut()
            .insert(cache_key, resolved.clone());
        resolved
    }
}

impl VaultLookup for SqlVaultLookup<'_> {
    fn resolve_link(&self, target: &str) -> Option<String> {
        self.resolve_link_from(target, self.source_path)
    }

    fn oplog_has_change_since(&self, path: &str, cutoff_ms: i64) -> Result<bool, EvalError> {
        self.exists_query(
            "SELECT 1 FROM oplog_events e JOIN files f ON f.id = e.file_id
             WHERE f.path = ?1 AND e.event_class = 1 AND e.ts_ms >= ?2 LIMIT 1",
            rusqlite::params![path, cutoff_ms],
            "oplog.has_change_since",
        )
    }

    fn oplog_has_property_change(
        &self,
        path: &str,
        key: &str,
        cutoff_ms: i64,
    ) -> Result<bool, EvalError> {
        self.exists_query(
            "SELECT 1 FROM oplog_events e JOIN files f ON f.id = e.file_id
             WHERE f.path = ?1 AND e.event_class IN (2, 3, 5)
               AND (e.property_key = ?2 OR e.event_class = 5)
               AND e.ts_ms >= ?3 LIMIT 1",
            rusqlite::params![path, key, cutoff_ms],
            "oplog.has_property_change",
        )
    }

    fn oplog_deleted_content_matches(
        &self,
        path: &str,
        pattern: &str,
        cutoff_ms: i64,
    ) -> Result<bool, EvalError> {
        self.exists_query(
            "SELECT 1 FROM oplog_events e JOIN files f ON f.id = e.file_id
             WHERE f.path = ?1 AND e.event_class = 1 AND e.ts_ms >= ?2
               AND e.deleted_text LIKE '%' || ?3 || '%' ESCAPE '\\' LIMIT 1",
            rusqlite::params![path, cutoff_ms, like_escape(pattern)],
            "oplog.deleted_content_matches",
        )
    }

    fn oplog_created_since(&self, path: &str, cutoff_ms: i64) -> Result<bool, EvalError> {
        self.exists_query(
            "SELECT 1 FROM files
             WHERE path = ?1 AND birthtime_ms > 0 AND birthtime_ms >= ?2 LIMIT 1",
            rusqlite::params![path, cutoff_ms],
            "oplog.created_since",
        )
    }

    fn oplog_untouched_for(&self, path: &str, cutoff_ms: i64) -> Result<bool, EvalError> {
        self.exists_query(
            "SELECT 1 FROM files f
             WHERE f.path = ?1 AND f.mtime_ms < ?2
               AND NOT EXISTS (
                   SELECT 1 FROM oplog_events e
                   WHERE e.file_id = f.id AND e.event_class = 1 AND e.ts_ms >= ?2
               ) LIMIT 1",
            rusqlite::params![path, cutoff_ms],
            "oplog.untouched_for",
        )
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
        assemble_row_for_path(self.conn, path, self).ok().flatten()
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
            .and_then(|id| load_outgoing_links(self.conn, id).ok())
            .map(|outgoing| outgoing.links)
            .unwrap_or_default()
    }

    fn embeds_for(&self, path: &str) -> Vec<LinkValue> {
        self.conn
            .query_row(
                "SELECT id FROM files WHERE path = ?1",
                params![path],
                |row| row.get::<_, i64>(0),
            )
            .optional()
            .ok()
            .flatten()
            .and_then(|id| load_outgoing_links(self.conn, id).ok())
            .map(|outgoing| outgoing.embeds)
            .unwrap_or_default()
    }

    fn outlinks_for(&self, path: &str) -> Vec<LinkValue> {
        self.conn
            .query_row(
                "SELECT id FROM files WHERE path = ?1",
                params![path],
                |row| row.get::<_, i64>(0),
            )
            .optional()
            .ok()
            .flatten()
            .and_then(|id| load_outgoing_links(self.conn, id).ok())
            .map(|outgoing| outgoing.all)
            .unwrap_or_default()
    }

    fn backlinks_for(&self, path: &str) -> Vec<LinkValue> {
        load_backlinks(self.conn, path).unwrap_or_default()
    }

    fn dql_tags_for(&self, path: &str) -> Vec<String> {
        crate::tags_db::load_dql_tags_for_path(self.conn, path).unwrap_or_default()
    }

    fn dql_inline_fields_for(
        &self,
        path: &str,
        now_ms: i64,
    ) -> Result<(Vec<(String, Value)>, bool), EvalError> {
        let projection = crate::dql_inline_fields_db::load_dql_inline_fields_for_path(
            self.conn, path,
        )
        .map_err(|error| EvalError::InvalidArgument {
            function: "DQL inline fields".to_string(),
            message: error.to_string(),
        })?;
        let fields = projection
            .fields
            .iter()
            .map(|field| {
                self.dql_inline_value(&field.value, path, now_ms)
                    .map(|value| (field.key.clone(), value))
            })
            .collect::<Result<Vec<_>, _>>()?;
        Ok((fields, projection.incomplete))
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::{
        sync::{Mutex, mpsc},
        time::{Duration, Instant},
    };

    static CANCELLATION_TEST_SERIAL: Mutex<()> = Mutex::new(());
    const CANCELLATION_PROBE_ROW: usize = 512;
    const CANCELLATION_VAULT_ROWS: usize = 10_000;

    fn filter(source: &str) -> FilterNode {
        FilterNode::Stmt(
            super::super::expr::parse_expr(source).unwrap_or_else(|error| {
                panic!("planner census expression must parse: {source}: {error}")
            }),
        )
    }

    #[test]
    fn census_bases_planner_selects_every_active_pushdown_family() {
        let file_families = [
            ("extension equality", r#"file.ext == "md""#),
            ("name equality", r#"file.name == "note.md""#),
            ("path equality", r#"file.path == "Notes/note.md""#),
            ("size comparison", "file.size >= 42"),
            ("mtime comparison", "file.mtime < 1234"),
            ("ctime comparison", "file.ctime == 5678"),
            ("note property equality", r#"status == "active""#),
            ("folder membership", r#"file.inFolder("Notes")"#),
            ("tag membership", r#"file.hasTag("project")"#),
            ("name prefix", r#"file.name.startsWith("note")"#),
            ("path prefix", r#"file.path.startsWith("Notes/")"#),
            ("note list/text membership", r#"tags.contains("project")"#),
        ];
        for (family, source) in file_families {
            let predicate = pushdown_predicate(&filter(source))
                .unwrap_or_else(|| panic!("file optimizer did not select {family} for {source}"));
            assert!(
                !predicate.clause.trim().is_empty(),
                "file optimizer selected an empty predicate for {family}: {source}"
            );
        }

        let task_families = [
            ("completed truthiness", "task.completed"),
            ("completed negation", "!task.completed"),
            ("completed comparison", "task.completed == false"),
            ("status comparison", r#"task.status == " ""#),
            ("priority equality", "task.priority == 3"),
            ("due equality", r#"task.due == date("2027-01-16")"#),
            (
                "scheduled equality",
                r#"task.scheduled == date("2027-01-15")"#,
            ),
            (
                "scheduled inequality",
                r#"task.scheduled != date("2027-01-15")"#,
            ),
            (
                "task file folder membership",
                r#"task.file.inFolder("Notes")"#,
            ),
            ("task file tag membership", r#"task.file.hasTag("project")"#),
        ];
        for (family, source) in task_families {
            let predicate = task_pushdown_predicate(&filter(source))
                .unwrap_or_else(|| panic!("task optimizer did not select {family} for {source}"));
            assert!(
                !predicate.clause.trim().is_empty(),
                "task optimizer selected an empty predicate for {family}: {source}"
            );
        }

        let source_families = [
            ("folder source", QuerySource::Folder("Notes".to_string())),
            ("tag source", QuerySource::Tag("project".to_string())),
            ("recent source", QuerySource::Recent { days: 7 }),
            (
                "linked depth-one source",
                QuerySource::Linked {
                    from_path: "Index.md".to_string(),
                    depth: 1,
                },
            ),
        ];
        for (family, source) in source_families {
            let mut plan = SqlPlan::default();
            source_predicates(
                &source,
                &EngineCtx {
                    now_ms: 1_800_000_000_000,
                    ..EngineCtx::default()
                },
                &mut plan,
            );
            assert!(
                !plan.clauses.is_empty(),
                "source optimizer did not select {family}: {source:?}"
            );
        }

        let mut conn = Connection::open_in_memory().expect("open FTS planner database");
        crate::db::migrate(&mut conn).expect("migrate FTS planner database");
        let fts = FtsMatchCache::default();
        let warnings = WarningSink::default();
        let cancel = CancelToken::new();
        let predicate = fts_pushdown_predicate(
            &filter(r#"file.matches("needle")"#),
            &conn,
            &fts,
            &warnings,
            &cancel,
        )
        .expect("FTS planner must not fail")
        .expect("FTS optimizer must select file.matches");
        assert!(
            predicate.sql.clause.contains("slate_bases_fts_matches"),
            "FTS optimizer selected the wrong SQL predicate: {:?}",
            predicate.sql
        );
        assert_eq!(predicate.sql.params, ["needle"]);
    }

    fn cancellation_query() -> SlateQuery {
        SlateQuery {
            source: QuerySource::All,
            row_source: RowSource::Files,
            filters: None,
            formulas: Vec::new(),
            custom_summaries: Vec::new(),
            group_by: None,
            sort: Vec::new(),
            columns: vec![ColumnSelection {
                id: "file.name".to_string(),
                display_name: None,
            }],
            summaries: Vec::new(),
            limit: None,
            view: super::super::ViewSpec::Table {
                fallback_from: None,
            },
        }
    }

    fn cancellation_vault() -> Connection {
        let mut conn = Connection::open_in_memory().expect("open cancellation database");
        crate::db::migrate(&mut conn).expect("migrate cancellation database");
        {
            let tx = conn.transaction().expect("begin cancellation seed");
            {
                let mut insert = tx
                    .prepare(
                        "INSERT INTO files (
                            id, path, name, extension, size_bytes, mtime_ms, ctime_ms,
                            content_hash, parser_version, indexed_at_ms, is_markdown
                         ) VALUES (?1, ?2, ?3, 'md', ?4, ?5, ?5, ?6, 1, ?5, 1)",
                    )
                    .expect("prepare cancellation seed");
                for index in 0..CANCELLATION_VAULT_ROWS {
                    insert
                        .execute(params![
                            index as i64 + 1,
                            format!("Notes/note-{index:05}.md"),
                            format!("note-{index:05}.md"),
                            index as i64 + 1,
                            index as i64,
                            format!("cancellation-hash-{index}")
                        ])
                        .expect("insert cancellation row");
                }
            }
            tx.commit().expect("commit cancellation seed");
        }
        conn
    }

    fn empty_cache_vault() -> Connection {
        let mut conn = Connection::open_in_memory().expect("open cache database");
        crate::db::migrate(&mut conn).expect("migrate cache database");
        conn
    }

    #[test]
    fn mirrored_value_sort_keys_match_the_native_engine_comparator() {
        use super::super::eval::{DqlDateValue, DqlDurationValue, FileHandleValue};

        let link = |target: &str, display: Option<&str>| {
            Value::Link(LinkValue {
                target: target.to_string(),
                display: display.map(str::to_string),
                resolved_path: Some(format!("Resolved/{target}.md")),
                subpath: None,
                link_type: "file".to_string(),
                embed: false,
            })
        };
        let values = vec![
            Value::Bool(false),
            Value::Bool(true),
            Value::Number(f64::NEG_INFINITY),
            Value::Number(-10.0),
            Value::Number(-0.0),
            Value::Number(0.0),
            Value::Number(2.0),
            Value::Number(10.0),
            Value::Number(f64::INFINITY),
            Value::Number(f64::NAN),
            Value::Number(f64::from_bits(0xfff8_0000_0000_0001)),
            Value::Date(DateValue {
                epoch_ms: -1,
                has_time: false,
            }),
            Value::Date(DateValue {
                epoch_ms: 1,
                has_time: true,
            }),
            Value::DqlDate(DqlDateValue {
                epoch_ms: -1,
                has_time: false,
                offset_minutes: 0,
                is_local: true,
            }),
            Value::DqlDate(DqlDateValue {
                epoch_ms: 1,
                has_time: true,
                offset_minutes: -300,
                is_local: false,
            }),
            Value::Duration(-2),
            Value::Duration(10),
            Value::DqlDuration(DqlDurationValue::default()),
            Value::DqlDuration(DqlDurationValue {
                days: 1.0,
                ..DqlDurationValue::default()
            }),
            Value::Text("Alpha".to_string()),
            Value::Text("alpha".to_string()),
            Value::Text("beta".to_string()),
            link("Alpha", Some("First display")),
            link("Alpha", Some("Different display")),
            link("Beta", None),
            Value::File(FileHandleValue {
                path: "Alpha.md".to_string(),
            }),
            Value::Regex("a+".to_string(), "i".to_string()),
            Value::List(vec![Value::Number(2.0), Value::Number(10.0)]),
            Value::List(vec![Value::Number(2.0), Value::Number(2.0)]),
            Value::Object(BTreeMap::from([(
                "key".to_string(),
                Value::Text("value".to_string()),
            )])),
        ];

        for lhs in &values {
            for rhs in &values {
                assert_eq!(
                    value_sort_key(lhs).cmp(&value_sort_key(rhs)),
                    compare_non_null_values(lhs, rhs),
                    "sort-key mismatch for lhs={lhs:?}, rhs={rhs:?}"
                );
            }
        }

        let mut with_null = values;
        with_null.push(Value::Null);
        for lhs in &with_null {
            for rhs in &with_null {
                assert_eq!(
                    value_sort_key(lhs).cmp(&value_sort_key(rhs)),
                    compare_sort_key(lhs, rhs, true),
                    "null-aware sort-key mismatch for lhs={lhs:?}, rhs={rhs:?}"
                );
            }
        }
    }

    #[test]
    fn resolved_equivalent_links_share_native_semantic_keys() {
        let link = |target: &str| {
            Value::Link(LinkValue {
                target: target.to_string(),
                display: None,
                resolved_path: Some("Notes/Target.md".to_string()),
                subpath: None,
                link_type: "file".to_string(),
                embed: false,
            })
        };
        let authored = link("Target");
        let canonical = link("Notes/Target.md");

        assert_eq!(
            compare_non_null_values(&authored, &canonical),
            Ordering::Equal,
            "authored and canonical targets resolving to one file must sort equally"
        );
        assert_eq!(
            value_sort_key(&authored),
            value_sort_key(&canonical),
            "mirrored sort keys must use the same resolved-first identity"
        );
        let unique_keys = [authored, canonical]
            .iter()
            .map(value_key)
            .collect::<BTreeSet<_>>();
        assert_eq!(
            unique_keys.len(),
            1,
            "resolved-equivalent links must count as one unique value"
        );
    }

    #[test]
    fn numeric_sort_order_is_total_for_non_finite_values_and_signed_zero() {
        let number = Value::Number;
        let ordered = [
            f64::NEG_INFINITY,
            -1.0,
            -0.0,
            0.0,
            1.0,
            f64::INFINITY,
            f64::NAN,
        ];

        for pair in ordered.windows(2) {
            let expected = if pair == [-0.0, 0.0] {
                Ordering::Equal
            } else {
                Ordering::Less
            };
            assert_eq!(
                compare_non_null_values(&number(pair[0]), &number(pair[1])),
                expected,
                "unexpected numeric order for {:?} and {:?}",
                pair[0],
                pair[1]
            );
            assert_eq!(
                value_sort_key(&number(pair[0])).cmp(&value_sort_key(&number(pair[1]))),
                expected,
                "sort-key order disagrees for {:?} and {:?}",
                pair[0],
                pair[1]
            );
        }

        let negative_payload_nan = f64::from_bits(0xfff8_0000_0000_0001);
        assert_eq!(
            compare_non_null_values(&number(f64::NAN), &number(negative_payload_nan)),
            Ordering::Equal,
            "all NaN payloads and signs share one canonical order position"
        );
        assert_eq!(
            value_sort_key(&number(f64::NAN)),
            value_sort_key(&number(negative_payload_nan)),
            "all NaN payloads and signs share one canonical sort key"
        );
    }

    #[test]
    fn decorated_frontmatter_wikilinks_keep_shared_parser_metadata() {
        let scalar_json = serde_json::to_string("Target#Project target!|Project lead").unwrap();
        let Value::Link(scalar) = decode_property_value("wikilink", &scalar_json) else {
            panic!("scalar wikilink must decode as a link");
        };
        assert_eq!(scalar.target, "Target");
        assert_eq!(scalar.display.as_deref(), Some("Project lead"));
        assert_eq!(scalar.subpath.as_deref(), Some("Project target"));
        assert_eq!(scalar.link_type, "header");

        let tagged_list_json = serde_json::json!([{
            "\u{f8ff}slate.property-kind": "wikilink",
            "value": "Target#^project-block|Project reference"
        }])
        .to_string();
        let Value::List(values) = decode_property_value("list", &tagged_list_json) else {
            panic!("wikilink list must decode as a list");
        };
        let [Value::Link(list_link)] = values.as_slice() else {
            panic!("tagged wikilink list element must decode as a link");
        };
        assert_eq!(list_link.target, "Target");
        assert_eq!(list_link.display.as_deref(), Some("Project reference"));
        assert_eq!(list_link.subpath.as_deref(), Some("project-block"));
        assert_eq!(list_link.link_type, "block");
    }

    #[test]
    fn query_cache_discards_every_stale_generation() {
        let conn = empty_cache_vault();
        let query = cancellation_query();
        let cache = BasesQueryCache::default();

        for generation in 0..=3 {
            let result = execute(
                &query,
                &conn,
                &EngineCtx {
                    generation,
                    cache: Some(&cache),
                    ..EngineCtx::default()
                },
                &CancelToken::new(),
            )
            .expect("execute cache generation");
            assert!(!result.cache_hit, "a new generation cannot hit stale state");
        }

        let entries = cache.entries.borrow();
        assert_eq!(entries.len(), 1, "all stale generations must be evicted");
        assert!(entries.keys().all(|key| key.generation == 3));
        drop(entries);

        execute(
            &query,
            &conn,
            &EngineCtx {
                generation: 4,
                cache: Some(&cache),
                quick_filter: Some("uncached"),
                ..EngineCtx::default()
            },
            &CancelToken::new(),
        )
        .expect("execute uncacheable current generation");
        assert!(
            cache.entries.borrow().is_empty(),
            "an uncacheable execution must still evict every stale generation"
        );
    }

    #[test]
    fn query_cache_is_a_bounded_lru_within_one_generation() {
        const EXPECTED_CAPACITY: usize = 16;

        let conn = empty_cache_vault();
        let query = cancellation_query();
        let cache = BasesQueryCache::default();
        let execute_for = |this_path: &str| {
            execute(
                &query,
                &conn,
                &EngineCtx {
                    generation: 7,
                    this_path: Some(this_path.to_string()),
                    cache: Some(&cache),
                    ..EngineCtx::default()
                },
                &CancelToken::new(),
            )
            .expect("execute cache variant")
        };

        for index in 0..EXPECTED_CAPACITY {
            assert!(!execute_for(&format!("Context-{index}.md")).cache_hit);
        }
        assert!(
            execute_for("Context-0.md").cache_hit,
            "refresh oldest entry"
        );
        assert!(
            !execute_for("Context-16.md").cache_hit,
            "insert one beyond capacity"
        );

        assert_eq!(cache.entries.borrow().len(), EXPECTED_CAPACITY);
        assert!(
            execute_for("Context-0.md").cache_hit,
            "recently used entry must survive"
        );
        assert!(
            !execute_for("Context-1.md").cache_hit,
            "least-recently-used entry must be evicted"
        );
        assert_eq!(cache.entries.borrow().len(), EXPECTED_CAPACITY);
    }

    #[test]
    fn census_bases_cancellation_under_load_returns_within_100ms() {
        let _serial = CANCELLATION_TEST_SERIAL
            .lock()
            .expect("serialize cancellation probe test");
        let conn = cancellation_vault();
        let query = cancellation_query();
        let cancel = CancelToken::new();
        let worker_cancel = cancel.clone();
        let (reached_tx, reached) = mpsc::channel();
        let (release, release_rx) = mpsc::channel();
        let (result_tx, result_rx) = mpsc::channel();

        let worker = std::thread::spawn(move || {
            install_test_materialization_probe(TestMaterializationProbe {
                after_rows: CANCELLATION_PROBE_ROW,
                reached: reached_tx,
                release: release_rx,
            });
            let result = execute(&query, &conn, &EngineCtx::default(), &worker_cancel);
            result_tx.send(result).expect("send engine result");
        });

        let materialized = reached
            .recv_timeout(Duration::from_secs(5))
            .expect("engine must park after real row materialization");
        assert_eq!(materialized, CANCELLATION_PROBE_ROW);
        let cancelled_at = Instant::now();
        cancel.cancel();
        release.send(()).expect("release engine worker");
        let result = result_rx
            .recv_timeout(Duration::from_millis(100))
            .unwrap_or_else(|error| {
                panic!(
                    "cancelled engine must finish within 100ms; waited {:?}: {error}",
                    cancelled_at.elapsed()
                )
            });
        assert!(
            matches!(result, Err(VaultError::Cancelled)),
            "expected exact VaultError::Cancelled, got {result:?}"
        );
        worker.join().expect("join cancellation worker");
    }
}
