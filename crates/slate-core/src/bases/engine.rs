// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! SQLite-backed Bases query execution.
//!
//! N1-2 owns file-row planning and execution. Task rows and FTS composition
//! have their own follow-up issues, so this module fails those unsupported
//! surfaces loudly instead of approximating them.

use std::{cell::RefCell, collections::BTreeMap};

use chrono::{DateTime, NaiveDate, NaiveDateTime, TimeZone, Utc};
use rusqlite::{Connection, OptionalExtension, params, params_from_iter};

use super::{
    ColumnSelection, FilterNode, QuerySource, RowSource, SlateQuery,
    eval::{
        DateValue, EvalCtx, EvalError, FileFields, LinkValue, ResolvedFormulas, RowContext, Value,
        VaultLookup, WarningSink, eval,
    },
    expr::{BinaryOp, Callee, Expr, ExprKind, FileField, GlobalFn, Lit, MethodName, PropertyRef},
};
use crate::{CancelToken, VaultError, db::DbError};

#[derive(Debug, Clone, PartialEq)]
pub struct BasesResultSet {
    pub columns: Vec<ResultColumn>,
    pub rows: Vec<BasesRow>,
    pub total_count: usize,
    pub shown_count: usize,
    pub warnings: Vec<String>,
    pub error: Option<ViewError>,
    pub executed_at_ms: i64,
    pub cache_hit: bool,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ResultColumn {
    pub id: String,
    pub display_name: Option<String>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct BasesRow {
    pub path: String,
    pub cells: Vec<CellValue>,
}

#[derive(Debug, Clone, PartialEq)]
pub enum CellValue {
    Value(Value),
    Error(String),
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
    if query.row_source != RowSource::Files {
        return Ok(error_result(
            query,
            ctx,
            "source: tasks is owned by N1-4",
            "",
        ));
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
    let vault = SqlVaultLookup { conn };
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
        let batch = load_candidates(query, conn, ctx, page_size, offset)?;
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
                let eval_ctx = eval_ctx(
                    &candidate.row,
                    this.as_ref(),
                    &formulas.values,
                    ctx,
                    &vault,
                    &warnings,
                );
                match eval_filter(filters, &eval_ctx) {
                    Ok(true) => {}
                    Ok(false) => continue,
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
            rows.push(row_to_result(
                query,
                &candidate.row,
                &formulas,
                this.as_ref(),
                ctx,
                &vault,
                &warnings,
            ));
        }
        offset += page_size;
    }

    let total_count = rows.len();
    if let Some(limit) = query.limit {
        rows.truncate(limit as usize);
    }
    let mut result = BasesResultSet {
        columns: result_columns(query),
        shown_count: rows.len(),
        total_count,
        rows,
        warnings: warnings.messages(),
        error: None,
        executed_at_ms: ctx.now_ms,
        cache_hit: false,
    };
    if let (Some(cache), Some(key)) = (ctx.cache, cache_key) {
        cache.entries.borrow_mut().insert(key, result.clone());
    }
    result.cache_hit = false;
    Ok(result)
}

fn load_candidates(
    query: &SlateQuery,
    conn: &Connection,
    ctx: &EngineCtx<'_>,
    limit: usize,
    offset: usize,
) -> Result<Vec<Candidate>, VaultError> {
    let mut plan = SqlPlan::default();
    source_predicates(&query.source, ctx, &mut plan);
    if ctx.pushdown
        && let Some(filters) = &query.filters
    {
        for conjunct in top_level_and(filters) {
            if let Some(predicate) = pushdown_predicate(conjunct) {
                plan.clauses.push(predicate.clause);
                plan.params.extend(predicate.params);
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

    rows.into_iter()
        .map(|file| assemble_row(conn, &file).map(|row| Candidate { row }))
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

fn assemble_row(conn: &Connection, file: &IndexedFileRow) -> Result<RowContext, VaultError> {
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
    Ok(RowContext {
        file_path: file.path.clone(),
        file_fields,
        properties,
        task: None,
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
    row.map(|file| assemble_row(conn, &file)).transpose()
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

fn row_to_result(
    query: &SlateQuery,
    row: &RowContext,
    formulas: &FormulaEval,
    this: Option<&RowContext>,
    ctx: &EngineCtx<'_>,
    vault: &dyn VaultLookup,
    warnings: &WarningSink,
) -> BasesRow {
    let cells = query
        .columns
        .iter()
        .map(|column| eval_column(column, row, formulas, this, ctx, vault, warnings))
        .collect();
    BasesRow {
        path: row.file_path.clone(),
        cells,
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
        total_count: 0,
        shown_count: 0,
        warnings: Vec::new(),
        error: Some(ViewError {
            construct: construct.into(),
            row_path: row_path.to_string(),
        }),
        executed_at_ms: ctx.now_ms,
        cache_hit: false,
    }
}

fn cache_key(query: &SlateQuery, ctx: &EngineCtx<'_>) -> Option<CacheKey> {
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
}

impl VaultLookup for SqlVaultLookup<'_> {
    fn resolve_link(&self, target: &str) -> Option<String> {
        self.conn
            .query_row(
                "SELECT path FROM files WHERE path = ?1 OR name = ?1 LIMIT 1",
                params![target],
                |row| row.get::<_, String>(0),
            )
            .optional()
            .ok()
            .flatten()
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
