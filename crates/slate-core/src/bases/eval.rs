// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Bases formula evaluator.
//!
//! N1-1 keeps evaluation pure: no SQLite handles, no filesystem access, and
//! no wall-clock reads. Host data enters through [`EvalCtx`] and [`VaultLookup`].
//!
//! v1 function status:
//!
//! - Evaluated: `if`, `date`, `duration`, `now`, `today`, `number`, `string`,
//!   `link`, `list`, `object`, `file`, `min`, `max`, `sum`, `average`,
//!   `escapeHTML`; the typed methods represented in `expr::MethodName`.
//! - Parse-only/render-as-text: `html`, `image`, `icon`.
//! - Excluded: `random`, which returns [`EvalError::Unsupported`].

use std::{
    cell::RefCell,
    cmp::Ordering,
    collections::{BTreeMap, BTreeSet},
    sync::OnceLock,
};

use chrono::{
    DateTime, Datelike, FixedOffset, Local, LocalResult, NaiveDate, NaiveDateTime, Offset,
    TimeZone, Timelike, Utc,
};
use regex::{Regex, RegexBuilder};
use thiserror::Error;

use super::expr::{
    BinaryOp, Callee, Expr, ExprKind, FileField, GlobalFn, ListExprKind, Lit, MethodName,
    PropertyRef, TaskField, UnaryOp,
};

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
const DQL_FLAT_DEPTH_LIMIT: usize = 256;
const REPEAT_OUTPUT_LIMIT: usize = 1_048_576;

pub type ResolvedFormulas = BTreeMap<String, Value>;

#[derive(Debug, Clone, Copy, Default, PartialEq)]
pub struct DqlDurationValue {
    pub years: f64,
    pub months: f64,
    pub weeks: f64,
    pub days: f64,
    pub hours: f64,
    pub minutes: f64,
    pub seconds: f64,
    pub milliseconds: f64,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
struct DurationParts {
    calendar_months: i64,
    fixed_ms: i64,
}

impl DqlDurationValue {
    const fn zero() -> Self {
        Self {
            years: 0.0,
            months: 0.0,
            weeks: 0.0,
            days: 0.0,
            hours: 0.0,
            minutes: 0.0,
            seconds: 0.0,
            milliseconds: 0.0,
        }
    }

    const fn fixed(fixed_ms: i64) -> Self {
        Self {
            milliseconds: fixed_ms as f64,
            ..Self::zero()
        }
    }

    fn negated(self) -> Self {
        self.scaled(-1.0)
    }

    fn added(self, rhs: Self) -> Self {
        Self {
            years: self.years + rhs.years,
            months: self.months + rhs.months,
            weeks: self.weeks + rhs.weeks,
            days: self.days + rhs.days,
            hours: self.hours + rhs.hours,
            minutes: self.minutes + rhs.minutes,
            seconds: self.seconds + rhs.seconds,
            milliseconds: self.milliseconds + rhs.milliseconds,
        }
    }

    fn subtracted(self, rhs: Self) -> Self {
        self.added(rhs.negated())
    }

    fn scaled(self, factor: f64) -> Self {
        Self {
            years: self.years * factor,
            months: self.months * factor,
            weeks: self.weeks * factor,
            days: self.days * factor,
            hours: self.hours * factor,
            minutes: self.minutes * factor,
            seconds: self.seconds * factor,
            milliseconds: self.milliseconds * factor,
        }
    }

    fn casual_milliseconds(self) -> f64 {
        self.years * 365.0 * 86_400_000.0
            + self.months * 30.0 * 86_400_000.0
            + self.weeks * 7.0 * 86_400_000.0
            + self.days * 86_400_000.0
            + self.hours * 3_600_000.0
            + self.minutes * 60_000.0
            + self.seconds * 1_000.0
            + self.milliseconds
    }

    fn normalized(self) -> Self {
        let total = self.casual_milliseconds();
        if total == 0.0 {
            return Self::zero();
        }
        let sign = total.signum();
        let mut remaining = total.abs();
        let mut take = |unit_ms: f64| {
            let count = (remaining / unit_ms).floor();
            remaining -= count * unit_ms;
            count * sign
        };
        let years = take(365.0 * 86_400_000.0);
        let months = take(30.0 * 86_400_000.0);
        let weeks = take(7.0 * 86_400_000.0);
        let days = take(86_400_000.0);
        let hours = take(3_600_000.0);
        let minutes = take(60_000.0);
        let seconds = take(1_000.0);
        Self {
            years,
            months,
            weeks,
            days,
            hours,
            minutes,
            seconds,
            milliseconds: remaining * sign,
        }
    }

    fn is_finite(self) -> bool {
        [
            self.years,
            self.months,
            self.weeks,
            self.days,
            self.hours,
            self.minutes,
            self.seconds,
            self.milliseconds,
        ]
        .into_iter()
        .all(f64::is_finite)
    }
}

impl DurationParts {
    const fn fixed(fixed_ms: i64) -> Self {
        Self {
            calendar_months: 0,
            fixed_ms,
        }
    }

    fn negated(self) -> Self {
        Self {
            calendar_months: self.calendar_months.saturating_neg(),
            fixed_ms: self.fixed_ms.saturating_neg(),
        }
    }
}

#[derive(Debug, Clone, PartialEq)]
pub enum Value {
    Null,
    Bool(bool),
    Number(f64),
    Text(String),
    Date(DateValue),
    DqlDate(DqlDateValue),
    Duration(i64),
    DqlDuration(DqlDurationValue),
    List(Vec<Value>),
    Object(BTreeMap<String, Value>),
    Link(LinkValue),
    File(FileHandleValue),
    Regex(String, String),
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord)]
pub struct DateValue {
    pub epoch_ms: i64,
    pub has_time: bool,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord)]
pub struct DqlDateValue {
    pub epoch_ms: i64,
    pub has_time: bool,
    pub offset_minutes: i32,
    pub is_local: bool,
}

#[derive(Debug, Clone, PartialEq, Eq, PartialOrd, Ord)]
pub struct LinkValue {
    pub target: String,
    pub display: Option<String>,
    pub resolved_path: Option<String>,
    pub subpath: Option<String>,
    pub link_type: String,
    pub embed: bool,
}

#[derive(Debug, Clone, PartialEq, Eq, PartialOrd, Ord)]
pub struct FileHandleValue {
    pub path: String,
}

#[derive(Debug, Clone, PartialEq)]
pub struct FileFields {
    pub name: String,
    pub basename: String,
    pub path: String,
    pub folder: String,
    pub ext: String,
    pub size: u64,
    pub properties: BTreeMap<String, Value>,
    pub tags: Vec<String>,
    pub aliases: Vec<String>,
    pub links: Vec<LinkValue>,
    pub backlinks: Vec<LinkValue>,
    pub embeds: Vec<LinkValue>,
    pub ctime: Option<DateValue>,
    pub mtime: Option<DateValue>,
    pub in_degree: u64,
    pub out_degree: u64,
}

impl FileFields {
    pub fn for_path(path: impl Into<String>) -> Self {
        let path = path.into();
        let (folder, name) = path
            .rsplit_once('/')
            .map(|(folder, name)| (folder.to_string(), name.to_string()))
            .unwrap_or_else(|| (String::new(), path.clone()));
        let (basename, ext) = name
            .rsplit_once('.')
            .map(|(base, ext)| (base.to_string(), ext.to_string()))
            .unwrap_or_else(|| (name.clone(), String::new()));
        Self {
            name,
            basename,
            path,
            folder,
            ext,
            size: 0,
            properties: BTreeMap::new(),
            tags: Vec::new(),
            aliases: Vec::new(),
            links: Vec::new(),
            backlinks: Vec::new(),
            embeds: Vec::new(),
            ctime: None,
            mtime: None,
            in_degree: 0,
            out_degree: 0,
        }
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct TaskRow {
    pub ordinal: u64,
    pub text: String,
    pub status: String,
    pub completed: bool,
    pub due: Option<DateValue>,
    pub scheduled: Option<DateValue>,
    pub priority: Option<f64>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct RowContext {
    pub file_path: String,
    pub file_fields: FileFields,
    pub properties: Vec<(String, Value)>,
    pub task: Option<TaskRow>,
}

pub trait VaultLookup {
    fn resolve_link(&self, _target: &str) -> Option<String> {
        None
    }

    fn file_matches(&self, _path: &str, _query: &str) -> Result<bool, EvalError> {
        Err(EvalError::FilterOnly {
            function: "file.matches".to_string(),
        })
    }

    /// O-6 (#544): ∃ a content-change event for `path` at or after
    /// `cutoff_ms`. Filter-position only, SQL-backed (the engine's
    /// SqlVaultLookup implements it; the default is the FilterOnly
    /// refusal, like `file_matches`).
    fn oplog_has_change_since(&self, _path: &str, _cutoff_ms: i64) -> Result<bool, EvalError> {
        Err(EvalError::FilterOnly {
            function: "oplog.has_change_since".to_string(),
        })
    }

    /// O-6 (#544): ∃ a property event for `key` (class 5 matches any
    /// key) at or after `cutoff_ms`.
    fn oplog_has_property_change(
        &self,
        _path: &str,
        _key: &str,
        _cutoff_ms: i64,
    ) -> Result<bool, EvalError> {
        Err(EvalError::FilterOnly {
            function: "oplog.has_property_change".to_string(),
        })
    }

    /// O-6 (#544): ∃ a content-change event at or after `cutoff_ms`
    /// whose deleted_text contains `pattern` ASCII-case-insensitively.
    fn oplog_deleted_content_matches(
        &self,
        _path: &str,
        _pattern: &str,
        _cutoff_ms: i64,
    ) -> Result<bool, EvalError> {
        Err(EvalError::FilterOnly {
            function: "oplog.deleted_content_matches".to_string(),
        })
    }

    fn row_for_path(&self, _path: &str) -> Option<RowContext> {
        None
    }

    fn links_for(&self, _path: &str) -> Vec<LinkValue> {
        Vec::new()
    }

    fn embeds_for(&self, _path: &str) -> Vec<LinkValue> {
        Vec::new()
    }

    fn outlinks_for(&self, path: &str) -> Vec<LinkValue> {
        self.links_for(path)
            .into_iter()
            .chain(self.embeds_for(path))
            .collect()
    }

    fn backlinks_for(&self, _path: &str) -> Vec<LinkValue> {
        Vec::new()
    }

    fn dql_tags_for(&self, _path: &str) -> Vec<String> {
        Vec::new()
    }

    fn dql_inline_fields_for(
        &self,
        _path: &str,
        _now_ms: i64,
    ) -> Result<(Vec<(String, Value)>, bool), EvalError> {
        Ok((Vec::new(), false))
    }
}

pub struct EvalCtx<'a> {
    pub file: &'a RowContext,
    pub this: Option<&'a RowContext>,
    pub formulas: &'a ResolvedFormulas,
    pub now_ms: i64,
    pub vault: &'a dyn VaultLookup,
    pub warnings: &'a WarningSink,
    pub filter_position: bool,
}

#[derive(Debug, Default)]
pub struct WarningSink {
    messages: RefCell<BTreeSet<String>>,
}

impl WarningSink {
    pub fn warn(&self, message: impl Into<String>) {
        self.messages.borrow_mut().insert(message.into());
    }

    pub fn messages(&self) -> Vec<String> {
        self.messages.borrow().iter().cloned().collect()
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Error)]
pub enum EvalError {
    #[error("unsupported expression: {reason}")]
    Unsupported { reason: String },
    #[error("{function} expected {expected}, got {got}")]
    InvalidArity {
        function: String,
        expected: String,
        got: usize,
    },
    #[error("{function}: {message}")]
    InvalidArgument { function: String, message: String },
    #[error("this is unavailable in this evaluation context")]
    NoThisContext,
    #[error("unsupported date format token {token}")]
    UnsupportedFormatToken { token: String },
    #[error("{function} is only valid in filter position")]
    FilterOnly { function: String },
    #[error("operation cancelled")]
    Cancelled,
}

#[derive(Default)]
struct Locals<'a> {
    value: Option<&'a Value>,
    index: Option<usize>,
    acc: Option<&'a Value>,
}

pub fn eval(expr: &Expr, ctx: &EvalCtx<'_>) -> Result<Value, EvalError> {
    eval_inner(expr, ctx, &Locals::default())
}

fn eval_inner(expr: &Expr, ctx: &EvalCtx<'_>, locals: &Locals<'_>) -> Result<Value, EvalError> {
    match &expr.kind {
        ExprKind::Lit(lit) => eval_lit(lit, ctx, locals),
        ExprKind::Prop(prop) => eval_prop(prop, ctx, locals),
        ExprKind::Index { base, index } => {
            let base = eval_inner(base, ctx, locals)?;
            let index = eval_inner(index, ctx, locals)?;
            index_value(&base, &index, ctx)
        }
        ExprKind::Field { base, name } => {
            let base = eval_inner(base, ctx, locals)?;
            field_value(&base, name, ctx)
        }
        ExprKind::Unary { op, rhs } => {
            let rhs = eval_inner(rhs, ctx, locals)?;
            eval_unary(*op, rhs, ctx)
        }
        ExprKind::Binary { op, lhs, rhs } => eval_binary(*op, lhs, rhs, ctx, locals),
        ExprKind::Call { callee, args } => eval_call(callee, args, ctx, locals),
        ExprKind::ListExpr {
            base,
            kind,
            body,
            init,
        } => eval_list_expr(base, *kind, body, init.as_deref(), ctx, locals),
        ExprKind::Unsupported { reason, .. } => Err(EvalError::Unsupported {
            reason: reason.clone(),
        }),
    }
}

fn eval_lit(lit: &Lit, ctx: &EvalCtx<'_>, locals: &Locals<'_>) -> Result<Value, EvalError> {
    Ok(match lit {
        Lit::String(value) => Value::Text(value.clone()),
        Lit::Number(value) if value.is_finite() => Value::Number(*value),
        Lit::Number(_) => {
            ctx.warnings
                .warn("non-finite numeric literal normalized to Null");
            Value::Null
        }
        Lit::Bool(value) => Value::Bool(*value),
        Lit::List(items) => Value::List(
            items
                .iter()
                .map(|item| eval_inner(item, ctx, locals))
                .collect::<Result<Vec<_>, _>>()?,
        ),
        Lit::Object(items) => {
            let mut out = BTreeMap::new();
            for (key, value) in items {
                out.insert(key.clone(), eval_inner(value, ctx, locals)?);
            }
            Value::Object(out)
        }
        Lit::Regex { pattern, flags } => Value::Regex(pattern.clone(), flags.clone()),
    })
}

fn eval_prop(
    prop: &PropertyRef,
    ctx: &EvalCtx<'_>,
    locals: &Locals<'_>,
) -> Result<Value, EvalError> {
    match prop {
        PropertyRef::Note(key) => Ok(row_property(ctx.file, key)),
        PropertyRef::File(field) => Ok(file_field_value(ctx.file, *field, ctx)),
        PropertyRef::Formula(key) => Ok(ctx.formulas.get(key).cloned().unwrap_or(Value::Null)),
        PropertyRef::This => {
            let this = ctx.this.ok_or(EvalError::NoThisContext)?;
            Ok(Value::File(FileHandleValue {
                path: this.file_path.clone(),
            }))
        }
        PropertyRef::ThisNote(key) => {
            let this = ctx.this.ok_or(EvalError::NoThisContext)?;
            Ok(row_property(this, key))
        }
        PropertyRef::ThisFile(field) => {
            let this = ctx.this.ok_or(EvalError::NoThisContext)?;
            Ok(file_field_value(this, *field, ctx))
        }
        PropertyRef::TaskField(field) => Ok(task_field_value(ctx.file, *field)),
        PropertyRef::ImplicitValue => locals
            .value
            .cloned()
            .ok_or_else(|| invalid_arg("list expression", "value used outside list expression")),
        PropertyRef::ImplicitIndex => locals
            .index
            .map(|index| Value::Number(index as f64))
            .ok_or_else(|| invalid_arg("list expression", "index used outside list expression")),
        PropertyRef::ImplicitAcc => locals
            .acc
            .cloned()
            .ok_or_else(|| invalid_arg("list expression", "acc used outside reduce expression")),
    }
}

fn eval_unary(op: UnaryOp, rhs: Value, ctx: &EvalCtx<'_>) -> Result<Value, EvalError> {
    match op {
        UnaryOp::Not => Ok(Value::Bool(!dql_or_native_truthy(&rhs))),
        UnaryOp::Neg => match coerce_number(&rhs) {
            Some(value) => Ok(number_or_null(-value, ctx)),
            None => {
                ctx.warnings
                    .warn("arithmetic on incompatible operand evaluated to Null");
                Ok(Value::Null)
            }
        },
    }
}

fn eval_binary(
    op: BinaryOp,
    lhs: &Expr,
    rhs: &Expr,
    ctx: &EvalCtx<'_>,
    locals: &Locals<'_>,
) -> Result<Value, EvalError> {
    if op == BinaryOp::And {
        let lhs = eval_inner(lhs, ctx, locals)?;
        if dql_object_value(&lhs, DQL_TRUTHY_OBJECT_KEY).is_some() {
            let rhs = eval_inner(rhs, ctx, locals)?;
            return Ok(Value::Bool(
                dql_or_native_truthy(&lhs) && dql_or_native_truthy(&rhs),
            ));
        }
        if !dql_or_native_truthy(&lhs) {
            return Ok(Value::Bool(false));
        }
        return Ok(Value::Bool(dql_or_native_truthy(&eval_inner(
            rhs, ctx, locals,
        )?)));
    }
    if op == BinaryOp::Or {
        let lhs = eval_inner(lhs, ctx, locals)?;
        if dql_object_value(&lhs, DQL_TRUTHY_OBJECT_KEY).is_some() {
            let rhs = eval_inner(rhs, ctx, locals)?;
            return Ok(Value::Bool(
                dql_or_native_truthy(&lhs) || dql_or_native_truthy(&rhs),
            ));
        }
        if dql_or_native_truthy(&lhs) {
            return Ok(Value::Bool(true));
        }
        return Ok(Value::Bool(dql_or_native_truthy(&eval_inner(
            rhs, ctx, locals,
        )?)));
    }

    let (lhs, lhs_literal_duration) = eval_binary_operand(lhs, ctx, locals)?;
    let (rhs, rhs_literal_duration) = eval_binary_operand(rhs, ctx, locals)?;
    match op {
        BinaryOp::Add => {
            if let Some(lhs) = dql_object_value(&lhs, DQL_ARITHMETIC_OBJECT_KEY) {
                return dql_add_values(lhs, &rhs, ctx);
            }
            if let (Value::Date(date), Some(duration)) = (&lhs, rhs_literal_duration) {
                return Ok(Value::Date(add_duration(*date, duration)));
            }
            if let (Some(duration), Value::Date(date)) = (lhs_literal_duration, &rhs) {
                return Ok(Value::Date(add_duration(*date, duration)));
            }
            add_values(lhs, rhs, ctx)
        }
        BinaryOp::Sub => {
            if let Some(lhs) = dql_object_value(&lhs, DQL_ARITHMETIC_OBJECT_KEY) {
                return dql_sub_values(lhs, &rhs, ctx);
            }
            if let (Value::Date(date), Some(duration)) = (&lhs, rhs_literal_duration) {
                return Ok(Value::Date(add_duration(*date, duration.negated())));
            }
            sub_values(lhs, rhs, ctx)
        }
        BinaryOp::Mul => mul_values(lhs, rhs, ctx),
        BinaryOp::Div => {
            if let Some(lhs) = dql_object_value(&lhs, DQL_ARITHMETIC_OBJECT_KEY) {
                dql_div_values(lhs, &rhs, ctx)
            } else {
                div_values(lhs, rhs, ctx)
            }
        }
        BinaryOp::Mod => {
            if let Some(lhs) = dql_object_value(&lhs, DQL_ARITHMETIC_OBJECT_KEY) {
                dql_mod_values(lhs, &rhs, ctx)
            } else {
                mod_values(lhs, rhs, ctx)
            }
        }
        BinaryOp::Eq => Ok(Value::Bool(
            dql_object_value(&lhs, DQL_EQUALITY_OBJECT_KEY)
                .map(|lhs| dql_typed_value_eq(lhs, &rhs))
                .unwrap_or_else(|| value_eq(&lhs, &rhs)),
        )),
        BinaryOp::Ne => Ok(Value::Bool(
            dql_object_value(&lhs, DQL_EQUALITY_OBJECT_KEY)
                .map(|lhs| !dql_typed_value_eq(lhs, &rhs))
                .unwrap_or_else(|| !value_eq(&lhs, &rhs)),
        )),
        BinaryOp::Gt | BinaryOp::Lt | BinaryOp::Gte | BinaryOp::Lte => {
            let ordering = if let Some(lhs) = dql_object_value(&lhs, DQL_ORDERING_OBJECT_KEY) {
                if matches!(lhs, Value::Null) != matches!(rhs, Value::Null) {
                    return Err(invalid_arg(
                        "DQL ordering",
                        "mixed null and non-null direct comparison is unsupported",
                    ));
                }
                dql_ordering_cmp(lhs, &rhs, "DQL ordering")?
            } else {
                let Some(ordering) = compare_values(&lhs, &rhs, ctx) else {
                    return Ok(Value::Bool(false));
                };
                ordering
            };
            let matched = match op {
                BinaryOp::Gt => ordering == Ordering::Greater,
                BinaryOp::Lt => ordering == Ordering::Less,
                BinaryOp::Gte => matches!(ordering, Ordering::Greater | Ordering::Equal),
                BinaryOp::Lte => matches!(ordering, Ordering::Less | Ordering::Equal),
                _ => unreachable!(),
            };
            Ok(Value::Bool(matched))
        }
        BinaryOp::And | BinaryOp::Or => unreachable!(),
    }
}

fn eval_binary_operand(
    expr: &Expr,
    ctx: &EvalCtx<'_>,
    locals: &Locals<'_>,
) -> Result<(Value, Option<DurationParts>), EvalError> {
    if let Some(duration) = literal_duration_parts(expr) {
        return Ok((
            Value::Duration(duration_parts_to_ms(duration)),
            Some(duration),
        ));
    }
    Ok((eval_inner(expr, ctx, locals)?, None))
}

fn literal_duration_parts(expr: &Expr) -> Option<DurationParts> {
    let ExprKind::Call {
        callee: Callee::Global(GlobalFn::Duration),
        args,
    } = &expr.kind
    else {
        return None;
    };
    let [arg] = args.as_slice() else {
        return None;
    };
    let ExprKind::Lit(Lit::String(text)) = &arg.kind else {
        return None;
    };
    parse_duration_parts(text)
}

fn add_values(lhs: Value, rhs: Value, ctx: &EvalCtx<'_>) -> Result<Value, EvalError> {
    match (&lhs, &rhs) {
        (Value::Date(date), rhs) if duration_arg(rhs).is_some() => Ok(Value::Date(add_duration(
            *date,
            duration_arg(rhs).expect("checked"),
        ))),
        (lhs, Value::Date(date)) if duration_arg(lhs).is_some() => Ok(Value::Date(add_duration(
            *date,
            duration_arg(lhs).expect("checked"),
        ))),
        _ if coerce_number(&lhs).zip(coerce_number(&rhs)).is_some() => {
            numeric_binary(&lhs, &rhs, ctx, |a, b| a + b)
        }
        _ if matches!(lhs, Value::Text(_)) || matches!(rhs, Value::Text(_)) => Ok(Value::Text(
            format!("{}{}", value_to_string(&lhs), value_to_string(&rhs)),
        )),
        _ => numeric_binary(&lhs, &rhs, ctx, |a, b| a + b),
    }
}

fn dql_add_values(lhs: &Value, rhs: &Value, _ctx: &EvalCtx<'_>) -> Result<Value, EvalError> {
    match (lhs, rhs) {
        (Value::Null, Value::Null)
        | (Value::Date(_), Value::Null)
        | (Value::Null, Value::Date(_))
        | (Value::DqlDate(_), Value::Null)
        | (Value::Null, Value::DqlDate(_)) => Ok(Value::Null),
        (Value::Number(lhs), Value::Number(rhs)) => Ok(Value::Number(lhs + rhs)),
        (Value::Text(lhs), rhs) => Ok(Value::Text(format!("{lhs}{}", dql_value_to_string(rhs)?))),
        (lhs, Value::Text(rhs)) => Ok(Value::Text(format!("{}{rhs}", dql_value_to_string(lhs)?))),
        (lhs, rhs) if dql_duration_arg(lhs).is_some() && dql_duration_arg(rhs).is_some() => {
            Ok(Value::DqlDuration(
                dql_duration_arg(lhs)
                    .expect("checked")
                    .added(dql_duration_arg(rhs).expect("checked"))
                    .normalized(),
            ))
        }
        (Value::Date(date), rhs) if dql_duration_arg(rhs).is_some() => {
            Ok(Value::DqlDate(add_dql_date_duration(
                dql_date_from_native(*date)?,
                dql_duration_arg(rhs).expect("checked"),
            )?))
        }
        (Value::DqlDate(date), rhs) if dql_duration_arg(rhs).is_some() => Ok(Value::DqlDate(
            add_dql_date_duration(*date, dql_duration_arg(rhs).expect("checked"))?,
        )),
        (lhs, Value::Date(date)) if dql_duration_arg(lhs).is_some() => {
            Ok(Value::DqlDate(add_dql_date_duration(
                dql_date_from_native(*date)?,
                dql_duration_arg(lhs).expect("checked"),
            )?))
        }
        (lhs, Value::DqlDate(date)) if dql_duration_arg(lhs).is_some() => Ok(Value::DqlDate(
            add_dql_date_duration(*date, dql_duration_arg(lhs).expect("checked"))?,
        )),
        (Value::List(lhs), Value::List(rhs)) => Ok(Value::List(
            lhs.iter().chain(rhs).cloned().collect::<Vec<_>>(),
        )),
        (Value::Object(lhs), Value::Object(rhs)) => {
            let mut merged = lhs.clone();
            merged.extend(rhs.iter().map(|(key, value)| (key.clone(), value.clone())));
            Ok(Value::Object(merged))
        }
        _ => Err(invalid_arg(
            "add",
            "DQL add received incompatible value types",
        )),
    }
}

fn dql_sub_values(lhs: &Value, rhs: &Value, _ctx: &EvalCtx<'_>) -> Result<Value, EvalError> {
    match (lhs, rhs) {
        (Value::Null, Value::Null)
        | (Value::Date(_), Value::Null)
        | (Value::Null, Value::Date(_))
        | (Value::DqlDate(_), Value::Null)
        | (Value::Null, Value::DqlDate(_)) => Ok(Value::Null),
        (Value::Number(lhs), Value::Number(rhs)) => Ok(Value::Number(lhs - rhs)),
        (lhs, rhs) if dql_duration_arg(lhs).is_some() && dql_duration_arg(rhs).is_some() => {
            Ok(Value::DqlDuration(
                dql_duration_arg(lhs)
                    .expect("checked")
                    .subtracted(dql_duration_arg(rhs).expect("checked"))
                    .normalized(),
            ))
        }
        (Value::Date(lhs), Value::Date(rhs)) => {
            Ok(Value::DqlDuration(dql_date_difference(*lhs, *rhs)?))
        }
        (Value::DqlDate(lhs), Value::DqlDate(rhs)) => {
            Ok(Value::DqlDuration(dql_date_difference_value(*lhs, *rhs)?))
        }
        (Value::DqlDate(lhs), Value::Date(rhs)) => Ok(Value::DqlDuration(
            dql_date_difference_value(*lhs, dql_date_from_native_with_provenance(*rhs, *lhs)?)?,
        )),
        (Value::Date(lhs), Value::DqlDate(rhs)) => Ok(Value::DqlDuration(
            dql_date_difference_value(dql_date_from_native_with_provenance(*lhs, *rhs)?, *rhs)?,
        )),
        (Value::Date(date), rhs) if dql_duration_arg(rhs).is_some() => {
            Ok(Value::DqlDate(add_dql_date_duration(
                dql_date_from_native(*date)?,
                dql_duration_arg(rhs).expect("checked").negated(),
            )?))
        }
        (Value::DqlDate(date), rhs) if dql_duration_arg(rhs).is_some() => Ok(Value::DqlDate(
            add_dql_date_duration(*date, dql_duration_arg(rhs).expect("checked").negated())?,
        )),
        _ => Err(invalid_arg(
            "subtract",
            "DQL subtract received incompatible value types",
        )),
    }
}

fn dql_div_values(lhs: &Value, rhs: &Value, _ctx: &EvalCtx<'_>) -> Result<Value, EvalError> {
    match (lhs, rhs) {
        (Value::Null, Value::Null) => Ok(Value::Null),
        (Value::Number(lhs), Value::Number(rhs)) => Ok(Value::Number(lhs / rhs)),
        (lhs, Value::Number(rhs)) if dql_duration_arg(lhs).is_some() && *rhs != 0.0 => {
            scale_dql_duration(dql_duration_arg(lhs).expect("checked"), 1.0 / rhs, "divide")
        }
        _ => Err(invalid_arg(
            "divide",
            "DQL divide received incompatible value types",
        )),
    }
}

fn scale_dql_duration(
    duration: DqlDurationValue,
    factor: f64,
    function: &str,
) -> Result<Value, EvalError> {
    let duration = duration.scaled(factor);
    if !factor.is_finite() || !duration.is_finite() {
        return Err(invalid_arg(
            function,
            "DQL duration scaling produced a non-finite duration",
        ));
    }
    Ok(Value::DqlDuration(duration.normalized()))
}

fn dql_mod_values(lhs: &Value, rhs: &Value, _ctx: &EvalCtx<'_>) -> Result<Value, EvalError> {
    match (lhs, rhs) {
        (Value::Null, Value::Null) => Ok(Value::Null),
        (Value::Number(lhs), Value::Number(rhs)) => Ok(Value::Number(lhs % rhs)),
        _ => Err(invalid_arg("modulo", "DQL modulo requires numeric values")),
    }
}

fn sub_values(lhs: Value, rhs: Value, ctx: &EvalCtx<'_>) -> Result<Value, EvalError> {
    match (&lhs, &rhs) {
        (Value::Date(date), rhs) if duration_arg(rhs).is_some() => Ok(Value::Date(add_duration(
            *date,
            duration_arg(rhs).expect("checked").negated(),
        ))),
        (Value::Date(lhs), Value::Date(rhs)) => {
            Ok(Value::Duration(lhs.epoch_ms.saturating_sub(rhs.epoch_ms)))
        }
        _ => numeric_binary(&lhs, &rhs, ctx, |a, b| a - b),
    }
}

fn mul_values(lhs: Value, rhs: Value, ctx: &EvalCtx<'_>) -> Result<Value, EvalError> {
    if let Some(value) = dql_object_value(&lhs, DQL_MULTIPLY_OBJECT_KEY) {
        return match (value, &rhs) {
            (Value::Null, Value::Null) => Ok(Value::Null),
            (Value::Text(text), Value::Number(times)) => repeat_text(
                "multiply",
                text,
                times.max(0.0),
                "DQL string repeat count must be finite and within Slate's safe output limit",
            ),
            (Value::Number(times), Value::Text(text)) => repeat_text(
                "multiply",
                text,
                times.max(0.0),
                "DQL string repeat count must be finite and within Slate's safe output limit",
            ),
            (Value::Number(lhs), Value::Number(rhs)) => Ok(Value::Number(lhs * rhs)),
            (duration, Value::Number(factor)) if dql_duration_arg(duration).is_some() => {
                scale_dql_duration(
                    dql_duration_arg(duration).expect("checked"),
                    *factor,
                    "multiply",
                )
            }
            (Value::Number(factor), duration) if dql_duration_arg(duration).is_some() => {
                scale_dql_duration(
                    dql_duration_arg(duration).expect("checked"),
                    *factor,
                    "multiply",
                )
            }
            _ => Err(invalid_arg(
                "multiply",
                "DQL multiply requires numbers, a string and a number, or a duration and a number",
            )),
        };
    }
    if let (Value::Text(text), Some(times)) = (&lhs, coerce_number(&rhs)) {
        return repeat_text(
            "multiply",
            text,
            times,
            "string repeat count must be finite and within Slate's safe output limit",
        );
    }
    if let (Some(times), Value::Text(text)) = (coerce_number(&lhs), &rhs) {
        return repeat_text(
            "multiply",
            text,
            times,
            "string repeat count must be finite and within Slate's safe output limit",
        );
    }
    numeric_binary(&lhs, &rhs, ctx, |a, b| a * b)
}

fn repeat_text(function: &str, text: &str, times: f64, error: &str) -> Result<Value, EvalError> {
    if !times.is_finite() || times < 0.0 || times > usize::MAX as f64 {
        return Err(invalid_arg(function, error));
    }
    let times = times.floor() as usize;
    if text
        .len()
        .checked_mul(times)
        .is_none_or(|length| length > REPEAT_OUTPUT_LIMIT)
    {
        return Err(invalid_arg(function, error));
    }
    Ok(Value::Text(text.repeat(times)))
}

fn div_values(lhs: Value, rhs: Value, ctx: &EvalCtx<'_>) -> Result<Value, EvalError> {
    let Some(a) = coerce_number(&lhs) else {
        return incompatible_arithmetic(ctx);
    };
    let Some(b) = coerce_number(&rhs) else {
        return incompatible_arithmetic(ctx);
    };
    number_or_null_with_warning(a / b, ctx)
}

fn mod_values(lhs: Value, rhs: Value, ctx: &EvalCtx<'_>) -> Result<Value, EvalError> {
    numeric_binary(&lhs, &rhs, ctx, |a, b| a % b)
}

fn numeric_binary(
    lhs: &Value,
    rhs: &Value,
    ctx: &EvalCtx<'_>,
    op: impl Fn(f64, f64) -> f64,
) -> Result<Value, EvalError> {
    let Some(a) = coerce_number(lhs) else {
        return incompatible_arithmetic(ctx);
    };
    let Some(b) = coerce_number(rhs) else {
        return incompatible_arithmetic(ctx);
    };
    number_or_null_with_warning(op(a, b), ctx)
}

fn incompatible_arithmetic(ctx: &EvalCtx<'_>) -> Result<Value, EvalError> {
    ctx.warnings
        .warn("arithmetic on incompatible operands evaluated to Null");
    Ok(Value::Null)
}

fn number_or_null_with_warning(value: f64, ctx: &EvalCtx<'_>) -> Result<Value, EvalError> {
    if value.is_nan() {
        ctx.warnings.warn("NaN normalized to Null");
        Ok(Value::Null)
    } else {
        Ok(Value::Number(value))
    }
}

fn number_or_null(value: f64, ctx: &EvalCtx<'_>) -> Value {
    if value.is_nan() {
        ctx.warnings.warn("NaN normalized to Null");
        Value::Null
    } else {
        Value::Number(value)
    }
}

fn eval_call(
    callee: &Callee,
    args: &[Expr],
    ctx: &EvalCtx<'_>,
    locals: &Locals<'_>,
) -> Result<Value, EvalError> {
    match callee {
        Callee::Global(GlobalFn::If) => eval_if(args, ctx, locals),
        Callee::Global(function) => eval_global(*function, args, ctx, locals),
        Callee::Method { receiver, name } => {
            let receiver = eval_inner(receiver, ctx, locals)?;
            let args = args
                .iter()
                .map(|arg| eval_inner(arg, ctx, locals))
                .collect::<Result<Vec<_>, _>>()?;
            eval_method(receiver, *name, &args, ctx)
        }
    }
}

fn eval_if(args: &[Expr], ctx: &EvalCtx<'_>, locals: &Locals<'_>) -> Result<Value, EvalError> {
    expect_arity("if", args.len(), 2, 3)?;
    let condition = eval_inner(&args[0], ctx, locals)?;
    if let Some(condition) = dql_object_value(&condition, DQL_CHOICE_OBJECT_KEY) {
        let then_value = eval_inner(&args[1], ctx, locals)?;
        let else_value = args
            .get(2)
            .map(|otherwise| eval_inner(otherwise, ctx, locals))
            .transpose()?
            .unwrap_or(Value::Null);
        return Ok(if dql_or_native_truthy(condition) {
            then_value
        } else {
            else_value
        });
    }
    if let Some(value) = dql_object_value(&condition, DQL_DEFAULT_OBJECT_KEY) {
        let fallback = eval_inner(&args[1], ctx, locals)?;
        if let Some(unused) = args.get(2) {
            eval_inner(unused, ctx, locals)?;
        }
        return Ok(if matches!(value, Value::Null) {
            fallback
        } else {
            value.clone()
        });
    }
    if condition.is_truthy() {
        eval_inner(&args[1], ctx, locals)
    } else if let Some(otherwise) = args.get(2) {
        eval_inner(otherwise, ctx, locals)
    } else {
        Ok(Value::Null)
    }
}

fn eval_global(
    function: GlobalFn,
    args: &[Expr],
    ctx: &EvalCtx<'_>,
    locals: &Locals<'_>,
) -> Result<Value, EvalError> {
    match function {
        GlobalFn::If => unreachable!("if is evaluated lazily before eval_global"),
        GlobalFn::Random => Err(EvalError::Unsupported {
            reason: "random is excluded from Bases v1".to_string(),
        }),
        GlobalFn::Now => {
            expect_arity("now", args.len(), 0, 0)?;
            Ok(Value::Date(DateValue {
                epoch_ms: ctx.now_ms,
                has_time: true,
            }))
        }
        GlobalFn::Today => {
            expect_arity("today", args.len(), 0, 0)?;
            Ok(Value::Date(today_value(ctx.now_ms)))
        }
        GlobalFn::Date => {
            let values = eval_args(args, ctx, locals)?;
            if let [value] = &values[..]
                && let Some(value) = dql_object_value(value, DQL_DATE_OBJECT_KEY)
            {
                return dql_date(value, ctx);
            }
            eval_date(&values, ctx)
        }
        GlobalFn::Duration => {
            let values = eval_args(args, ctx, locals)?;
            if let [value] = &values[..]
                && let Some(value) = dql_object_value(value, DQL_DURATION_OBJECT_KEY)
            {
                return dql_duration(value);
            }
            eval_duration(&values)
        }
        GlobalFn::Number => {
            let values = eval_args(args, ctx, locals)?;
            expect_arity("number", values.len(), 1, 1)?;
            let value = if let Some(value) = dql_object_value(&values[0], DQL_NUMBER_OBJECT_KEY) {
                dql_number(value)?
            } else {
                coerce_number(&values[0])
            };
            Ok(value.map(Value::Number).unwrap_or(Value::Null))
        }
        GlobalFn::String => {
            let values = eval_args(args, ctx, locals)?;
            expect_arity("string", values.len(), 1, 1)?;
            Ok(Value::Text(value_to_string(&values[0])))
        }
        GlobalFn::Link => {
            let values = eval_args(args, ctx, locals)?;
            expect_arity("link", values.len(), 1, 3)?;
            if let Some(value) = dql_object_value(&values[0], DQL_EMBED_OBJECT_KEY) {
                if matches!(value, Value::Null) || matches!(values.get(1), Some(Value::Null)) {
                    return Ok(Value::Null);
                }
                if let Some(embed) = values.get(1)
                    && !matches!(embed, Value::Bool(_))
                {
                    return Err(invalid_arg(
                        "embed",
                        "DQL embed's second argument must be boolean",
                    ));
                }
                return match value {
                    Value::Null => Ok(Value::Null),
                    Value::Link(link) => {
                        let mut link = link.clone();
                        link.embed = values
                            .get(1)
                            .and_then(|value| match value {
                                Value::Bool(embed) => Some(*embed),
                                _ => None,
                            })
                            .unwrap_or(true);
                        Ok(Value::Link(link))
                    }
                    _ => Err(invalid_arg("embed", "DQL embed expects a link value")),
                };
            }
            if let Some(value) = dql_object_value(&values[0], DQL_LINK_OBJECT_KEY) {
                return dql_link(value, values.get(1), values.get(2), ctx);
            }
            if values.len() == 3 {
                return Err(invalid_arg(
                    "link",
                    "native link expects at most two arguments",
                ));
            }
            let (target, resolved_path) = match &values[0] {
                Value::File(file) => (file.path.clone(), Some(file.path.clone())),
                value => {
                    let target = expect_text_like("link", value)?;
                    let resolved_path = ctx.vault.resolve_link(&target);
                    (target, resolved_path)
                }
            };
            let display = values.get(1).map(value_to_string);
            Ok(Value::Link(LinkValue {
                resolved_path,
                target,
                display,
                subpath: None,
                link_type: "file".to_string(),
                embed: false,
            }))
        }
        GlobalFn::List => {
            let values = eval_args(args, ctx, locals)?;
            normalize_list(values)
        }
        GlobalFn::Object => {
            let values = eval_args(args, ctx, locals)?;
            if matches!(values.first(), Some(Value::Text(key)) if key == DQL_OBJECT_CONSTRUCTOR_KEY)
            {
                return dql_object_constructor(&values[1..]);
            }
            eval_object_constructor(&values)
        }
        GlobalFn::File => {
            let values = eval_args(args, ctx, locals)?;
            expect_arity("file", values.len(), 1, 1)?;
            let path = file_path_from_value(&values[0])?;
            Ok(Value::File(FileHandleValue { path }))
        }
        GlobalFn::EscapeHtml => {
            let values = eval_args(args, ctx, locals)?;
            expect_arity("escapeHTML", values.len(), 1, 1)?;
            Ok(Value::Text(escape_html(&value_to_string(&values[0]))))
        }
        GlobalFn::Html | GlobalFn::Icon | GlobalFn::Image => {
            let values = eval_args(args, ctx, locals)?;
            let name = match function {
                GlobalFn::Html => "html",
                GlobalFn::Icon => "icon",
                GlobalFn::Image => "image",
                _ => unreachable!(),
            };
            expect_arity(name, values.len(), 1, 1)?;
            Ok(Value::Text(value_to_string(&values[0])))
        }
        GlobalFn::Min | GlobalFn::Max | GlobalFn::Sum | GlobalFn::Average => {
            let values = eval_args(args, ctx, locals)?;
            eval_aggregate(function, &values, ctx)
        }
    }
}

fn eval_args(
    args: &[Expr],
    ctx: &EvalCtx<'_>,
    locals: &Locals<'_>,
) -> Result<Vec<Value>, EvalError> {
    args.iter()
        .map(|arg| eval_inner(arg, ctx, locals))
        .collect::<Result<Vec<_>, _>>()
}

fn eval_method(
    receiver: Value,
    name: MethodName,
    args: &[Value],
    ctx: &EvalCtx<'_>,
) -> Result<Value, EvalError> {
    match name {
        MethodName::IsTruthy => {
            expect_arity("isTruthy", args.len(), 0, 0)?;
            Ok(Value::Bool(dql_or_native_truthy(&receiver)))
        }
        MethodName::IsType => {
            expect_arity("isType", args.len(), 1, 1)?;
            Ok(Value::Bool(
                receiver.is_type(&expect_text_like("isType", &args[0])?),
            ))
        }
        MethodName::ToString => {
            expect_arity("toString", args.len(), 0, 0)?;
            if let Some(Value::Text(path)) = dql_object_value(&receiver, DQL_FILE_NAME_OBJECT_KEY) {
                let name = path.rsplit('/').next().unwrap_or(path);
                return Ok(Value::Text(
                    name.strip_suffix(".md").unwrap_or(name).to_string(),
                ));
            }
            if let Some(value) = dql_object_value(&receiver, DQL_STRING_OBJECT_KEY) {
                return Ok(Value::Text(dql_value_to_string(value)?));
            }
            Ok(Value::Text(value_to_string(&receiver)))
        }
        MethodName::Date => {
            expect_arity("date", args.len(), 0, 0)?;
            if let Some(value) = dql_object_value(&receiver, DQL_STRIPTIME_OBJECT_KEY) {
                return match value {
                    Value::Null => Ok(Value::Null),
                    Value::Date(date) => Ok(Value::Date(strip_time(*date))),
                    Value::DqlDate(date) => Ok(Value::DqlDate(dql_strip_time(*date)?)),
                    _ => Err(invalid_arg(
                        "striptime",
                        "DQL striptime expects a date or null",
                    )),
                };
            }
            match receiver {
                Value::Date(date) => Ok(Value::Date(strip_time(date))),
                Value::DqlDate(date) => Ok(Value::DqlDate(dql_strip_time(date)?)),
                Value::Null => Ok(Value::Null),
                other => eval_date(&[other], ctx),
            }
        }
        MethodName::Format => {
            expect_arity("format", args.len(), 1, 1)?;
            let format = expect_text_like("format", &args[0])?;
            match receiver {
                Value::Date(date) => Ok(Value::Text(format_date(date, &format)?)),
                Value::DqlDate(date) => Ok(Value::Text(format_dql_date(date, &format)?)),
                _ => Ok(Value::Null),
            }
        }
        MethodName::Time => {
            expect_arity("time", args.len(), 0, 0)?;
            if let Value::Date(date) = receiver {
                let dt = date_time(date)?;
                Ok(Value::Text(dt.format("%H:%M:%S").to_string()))
            } else if let Value::DqlDate(date) = receiver {
                let dt = dql_date_time(date)?;
                Ok(Value::Text(dt.format("%H:%M:%S").to_string()))
            } else {
                Ok(Value::Null)
            }
        }
        MethodName::Relative => {
            expect_arity("relative", args.len(), 0, 0)?;
            if let Value::Date(date) = receiver {
                let delta = ctx.now_ms - date.epoch_ms;
                Ok(Value::Text(relative_text(delta)))
            } else if let Value::DqlDate(date) = receiver {
                let delta = ctx.now_ms - date.epoch_ms;
                Ok(Value::Text(relative_text(delta)))
            } else {
                Ok(Value::Null)
            }
        }
        MethodName::IsEmpty => {
            expect_arity("isEmpty", args.len(), 0, 0)?;
            Ok(Value::Bool(receiver.is_empty_value()))
        }
        MethodName::Contains => {
            expect_arity("contains", args.len(), 1, 1)?;
            let contains = dql_object_value(&receiver, DQL_CONTAINS_OBJECT_KEY)
                .map(|receiver| dql_contains_value(receiver, &args[0]))
                .unwrap_or_else(|| contains_value(&receiver, &args[0], false));
            Ok(Value::Bool(contains))
        }
        MethodName::ContainsAll => {
            expect_arity("containsAll", args.len(), 1, usize::MAX)?;
            let needles = match args {
                [Value::List(values)] => values.as_slice(),
                _ => args,
            };
            Ok(Value::Bool(
                needles
                    .iter()
                    .all(|needle| contains_value(&receiver, needle, false)),
            ))
        }
        MethodName::ContainsAny => {
            expect_arity("containsAny", args.len(), 1, usize::MAX)?;
            let needles = match args {
                [Value::List(values)] => values.as_slice(),
                _ => args,
            };
            Ok(Value::Bool(
                needles
                    .iter()
                    .any(|needle| contains_value(&receiver, needle, false)),
            ))
        }
        MethodName::StartsWith | MethodName::EndsWith
            if dql_object_value(&receiver, DQL_TEXT_METHOD_OBJECT_KEY).is_some() =>
        {
            let function = if name == MethodName::StartsWith {
                "startsWith"
            } else {
                "endsWith"
            };
            expect_arity(function, args.len(), 1, 1)?;
            let receiver = dql_object_value(&receiver, DQL_TEXT_METHOD_OBJECT_KEY)
                .expect("guard checked marker");
            match (receiver, &args[0]) {
                (Value::Null, _) | (_, Value::Null) => Ok(Value::Null),
                (Value::Text(receiver), Value::Text(argument)) => {
                    Ok(Value::Bool(if name == MethodName::StartsWith {
                        receiver.starts_with(argument)
                    } else {
                        receiver.ends_with(argument)
                    }))
                }
                _ => Err(invalid_arg(
                    function,
                    "DQL text function requires text arguments",
                )),
            }
        }
        MethodName::StartsWith => string_method(&receiver, "startsWith", args, 1, |s, args| {
            Value::Bool(s.starts_with(&value_to_string(&args[0])))
        }),
        MethodName::EndsWith => string_method(&receiver, "endsWith", args, 1, |s, args| {
            Value::Bool(s.ends_with(&value_to_string(&args[0])))
        }),
        MethodName::Lower if dql_object_value(&receiver, DQL_TEXT_METHOD_OBJECT_KEY).is_some() => {
            expect_arity("lower", args.len(), 0, 0)?;
            match dql_object_value(&receiver, DQL_TEXT_METHOD_OBJECT_KEY)
                .expect("guard checked marker")
            {
                Value::Null => Ok(Value::Null),
                Value::Text(text) if text.is_ascii() => Ok(Value::Text(text.to_ascii_lowercase())),
                Value::Text(_) => Err(invalid_arg(
                    "lower",
                    "DQL locale-aware lowercasing is unsupported for non-ASCII text",
                )),
                _ => Err(invalid_arg("lower", "DQL lower requires text")),
            }
        }
        MethodName::Lower => string_method(&receiver, "lower", args, 0, |s, _| {
            Value::Text(s.to_lowercase())
        }),
        MethodName::Title => string_method(&receiver, "title", args, 0, |s, _| {
            Value::Text(title_case(s))
        }),
        MethodName::Trim => string_method(&receiver, "trim", args, 0, |s, _| {
            Value::Text(s.trim().to_string())
        }),
        MethodName::Reverse => {
            expect_arity("reverse", args.len(), 0, 0)?;
            if let Some(value) = dql_object_value(&receiver, DQL_REVERSE_OBJECT_KEY) {
                return dql_reverse(value);
            }
            match receiver {
                Value::Text(text) => Ok(Value::Text(text.chars().rev().collect())),
                Value::List(mut values) => {
                    values.reverse();
                    Ok(Value::List(values))
                }
                _ => Ok(Value::Null),
            }
        }
        MethodName::Repeat => {
            expect_arity("repeat", args.len(), 1, 1)?;
            let text = match receiver {
                Value::Text(text) => text,
                Value::Null => return Ok(Value::Null),
                value => value_to_string(&value),
            };
            repeat_text(
                "repeat",
                &text,
                coerce_number(&args[0]).unwrap_or(0.0),
                "string repeat count must be finite and within Slate's safe output limit",
            )
        }
        MethodName::Slice => slice_value(receiver, args),
        MethodName::Split => split_value(receiver, args),
        MethodName::Replace => replace_value(receiver, args),
        MethodName::Abs => number_method(receiver, "abs", args, 0, |n, _| Value::Number(n.abs())),
        MethodName::Ceil if dql_object_value(&receiver, DQL_NUMBER_METHOD_OBJECT_KEY).is_some() => {
            dql_number_method(receiver, "ceil", args, |number| number.ceil())
        }
        MethodName::Ceil => {
            number_method(receiver, "ceil", args, 0, |n, _| Value::Number(n.ceil()))
        }
        MethodName::Floor
            if dql_object_value(&receiver, DQL_NUMBER_METHOD_OBJECT_KEY).is_some() =>
        {
            dql_number_method(receiver, "floor", args, |number| number.floor())
        }
        MethodName::Floor => {
            number_method(receiver, "floor", args, 0, |n, _| Value::Number(n.floor()))
        }
        MethodName::Trunc
            if dql_object_value(&receiver, DQL_NUMBER_METHOD_OBJECT_KEY).is_some() =>
        {
            dql_number_method(receiver, "trunc", args, |number| number.trunc())
        }
        MethodName::Trunc => {
            number_method(receiver, "trunc", args, 0, |n, _| Value::Number(n.trunc()))
        }
        MethodName::Round => {
            expect_arity("round", args.len(), 0, 1)?;
            let (number, dql) =
                if let Some(receiver) = dql_object_value(&receiver, DQL_NUMBER_METHOD_OBJECT_KEY) {
                    match receiver {
                        Value::Null => return Ok(Value::Null),
                        Value::Number(number) => (*number, true),
                        _ => return Err(invalid_arg("round", "DQL round requires a number")),
                    }
                } else {
                    let Some(number) = coerce_number(&receiver) else {
                        return Ok(Value::Null);
                    };
                    (number, false)
                };
            let precision = if dql {
                match args.first() {
                    None => 0.0,
                    Some(Value::Number(precision)) => *precision,
                    Some(Value::Null) => 0.0,
                    Some(_) => {
                        return Err(invalid_arg(
                            "round",
                            "DQL round precision requires a number",
                        ));
                    }
                }
            } else {
                args.first().and_then(coerce_number).unwrap_or(0.0)
            };
            Ok(Value::Number(javascript_round_with_precision(
                number, precision,
            )?))
        }
        MethodName::ToFixed => {
            expect_arity("toFixed", args.len(), 1, 1)?;
            let Some(number) = coerce_number(&receiver) else {
                return Ok(Value::Null);
            };
            let precision = coerce_number(&args[0]).unwrap_or(0.0).max(0.0) as usize;
            Ok(Value::Text(format!("{number:.precision$}")))
        }
        MethodName::Join => {
            expect_arity("join", args.len(), 1, 1)?;
            let dql_join = dql_object_value(&receiver, DQL_JOIN_OBJECT_KEY).is_some();
            let receiver = dql_object_value(&receiver, DQL_JOIN_OBJECT_KEY)
                .cloned()
                .unwrap_or(receiver);
            let sep = if dql_join {
                match &args[0] {
                    Value::Null => ", ".to_string(),
                    Value::Text(separator) => separator.clone(),
                    _ => return Err(invalid_arg("join", "DQL join separator requires text")),
                }
            } else {
                value_to_string(&args[0])
            };
            if dql_join && !matches!(receiver, Value::List(_)) {
                return dql_value_to_string(&receiver).map(Value::Text);
            }
            let Value::List(values) = receiver else {
                return Ok(Value::Null);
            };
            Ok(Value::Text(
                values
                    .iter()
                    .map(|value| {
                        if dql_join {
                            dql_nested_value_to_string(value)
                        } else {
                            Ok(value_to_string(value))
                        }
                    })
                    .collect::<Result<Vec<_>, _>>()?
                    .join(&sep),
            ))
        }
        MethodName::Flat => {
            expect_arity("flat", args.len(), 0, 1)?;
            let dql_flat = dql_object_value(&receiver, DQL_LIST_METHOD_OBJECT_KEY).is_some();
            let receiver = unwrap_dql_list_method(receiver, "flat")?;
            let Value::List(values) = receiver else {
                return Ok(Value::Null);
            };
            let depth = match args.first() {
                None => 1,
                Some(Value::Number(depth)) if dql_flat && depth.is_finite() => {
                    depth.max(0.0).trunc() as usize
                }
                Some(_) if dql_flat => {
                    return Err(invalid_arg(
                        "flat",
                        "DQL flat depth requires a finite number",
                    ));
                }
                Some(value) => coerce_number(value)
                    .filter(|depth| depth.is_finite())
                    .map(|depth| depth.max(0.0).trunc() as usize)
                    .ok_or_else(|| invalid_arg("flat", "expected a finite numeric depth"))?,
            };
            if depth > DQL_FLAT_DEPTH_LIMIT {
                return Err(invalid_arg(
                    "flat",
                    "depth exceeds Slate's safe conversion limit",
                ));
            }
            Ok(Value::List(flatten(values, depth)))
        }
        MethodName::Sort => {
            expect_arity("sort", args.len(), 0, 0)?;
            let dql_sort = dql_object_value(&receiver, DQL_SORT_OBJECT_KEY).is_some();
            let receiver = dql_object_value(&receiver, DQL_SORT_OBJECT_KEY)
                .cloned()
                .unwrap_or(receiver);
            let Value::List(mut values) = receiver else {
                return Ok(Value::Null);
            };
            if dql_sort {
                dql_sort_values(&mut values, "sort")?;
            } else {
                sort_values(&mut values, "sort")?;
            }
            Ok(Value::List(values))
        }
        MethodName::Unique => {
            expect_arity("unique", args.len(), 0, 0)?;
            let dql_unique = dql_object_value(&receiver, DQL_LIST_METHOD_OBJECT_KEY).is_some();
            let receiver = unwrap_dql_list_method(receiver, "unique")?;
            let Value::List(values) = receiver else {
                return Ok(Value::Null);
            };
            let mut values = values;
            if dql_unique {
                for index in 1..values.len() {
                    let mut cursor = index;
                    while cursor > 0
                        && dql_ordering_cmp(&values[cursor], &values[cursor - 1], "unique")?
                            == Ordering::Less
                    {
                        values.swap(cursor, cursor - 1);
                        cursor -= 1;
                    }
                }
            }
            let mut out = Vec::new();
            for value in values {
                if !out.iter().any(|seen| {
                    if dql_unique {
                        dql_typed_value_eq(seen, &value)
                    } else {
                        value_eq(seen, &value)
                    }
                }) {
                    out.push(value);
                }
            }
            Ok(Value::List(out))
        }
        MethodName::AsFile => {
            expect_arity("asFile", args.len(), 0, 0)?;
            let path = file_path_from_value(&receiver)?;
            Ok(Value::File(FileHandleValue { path }))
        }
        MethodName::LinksTo => {
            expect_arity("linksTo", args.len(), 1, 1)?;
            let source = file_path_from_value(&receiver)?;
            let target = file_path_from_value(&args[0])?;
            Ok(Value::Bool(
                ctx.vault
                    .links_for(&source)
                    .into_iter()
                    .chain(ctx.vault.embeds_for(&source))
                    .any(|link| {
                        link.resolved_path.as_deref() == Some(target.as_str())
                            || link.target == target
                    }),
            ))
        }
        MethodName::AsLink => {
            expect_arity("asLink", args.len(), 0, 1)?;
            let path = file_path_from_value(&receiver)?;
            let display = args.first().map(value_to_string);
            Ok(Value::Link(LinkValue {
                target: path.clone(),
                display,
                resolved_path: Some(path),
                subpath: None,
                link_type: "file".to_string(),
                embed: false,
            }))
        }
        MethodName::HasLink => {
            expect_arity("hasLink", args.len(), 1, 1)?;
            let path = file_path_from_value(&receiver)?;
            let target = file_path_from_value(&args[0])?;
            Ok(Value::Bool(
                ctx.vault
                    .links_for(&path)
                    .into_iter()
                    .chain(ctx.vault.embeds_for(&path))
                    .any(|link| {
                        link.resolved_path.as_deref() == Some(target.as_str())
                            || link.target == target
                    }),
            ))
        }
        MethodName::HasProperty => {
            expect_arity("hasProperty", args.len(), 1, 1)?;
            let row = row_from_value(&receiver, ctx);
            let key = expect_text_like("hasProperty", &args[0])?;
            Ok(Value::Bool(
                row.properties.iter().any(|(got, _)| got == &key)
                    || row.file_fields.properties.contains_key(&key),
            ))
        }
        MethodName::HasTag => {
            expect_arity("hasTag", args.len(), 1, usize::MAX)?;
            let row = row_from_value(&receiver, ctx);
            let tags = args
                .iter()
                .map(|arg| expect_text("hasTag", arg))
                .collect::<Result<Vec<_>, _>>()?;
            Ok(Value::Bool(tags.iter().any(|tag| {
                row.file_fields.tags.iter().any(|got| tag_matches(got, tag))
            })))
        }
        MethodName::InFolder => {
            expect_arity("inFolder", args.len(), 1, 1)?;
            let row = row_from_value(&receiver, ctx);
            let folder = value_to_string(&args[0]).trim_matches('/').to_string();
            Ok(Value::Bool(
                row.file_fields.folder == folder
                    || row
                        .file_fields
                        .folder
                        .strip_prefix(folder.as_str())
                        .is_some_and(|rest| rest.starts_with('/')),
            ))
        }
        MethodName::Keys => {
            expect_arity("keys", args.len(), 0, 0)?;
            let Value::Object(map) = receiver else {
                return Ok(Value::Null);
            };
            Ok(Value::List(map.keys().cloned().map(Value::Text).collect()))
        }
        MethodName::Values => {
            expect_arity("values", args.len(), 0, 0)?;
            if let Some(Value::Text(path)) = dql_object_value(&receiver, DQL_TAGS_OBJECT_KEY) {
                return dql_tag_values(&Value::List(
                    ctx.vault
                        .dql_tags_for(path)
                        .into_iter()
                        .map(Value::Text)
                        .collect(),
                ));
            }
            if let Some(Value::Text(path)) = dql_object_value(&receiver, DQL_ALIASES_OBJECT_KEY) {
                let row = row_for_path(ctx, path);
                return Ok(Value::List(dql_alias_values(&row)?));
            }
            if let Some(Value::Text(path)) = dql_object_value(&receiver, DQL_OUTLINKS_OBJECT_KEY) {
                return Ok(Value::List(
                    ctx.vault
                        .outlinks_for(path)
                        .into_iter()
                        .map(Value::Link)
                        .collect(),
                ));
            }
            if let Some(Value::Text(path)) = dql_object_value(&receiver, DQL_INLINKS_OBJECT_KEY) {
                let mut links = Vec::new();
                for link in ctx.vault.backlinks_for(path) {
                    if !links
                        .iter()
                        .any(|seen: &LinkValue| link_identity(seen) == link_identity(&link))
                    {
                        links.push(link);
                    }
                }
                return Ok(Value::List(links.into_iter().map(Value::Link).collect()));
            }
            let Value::Object(map) = receiver else {
                return Ok(Value::Null);
            };
            Ok(Value::List(map.values().cloned().collect()))
        }
        MethodName::Matches => {
            expect_arity("matches", args.len(), 1, 1)?;
            if let Value::File(file) = receiver {
                if !ctx.filter_position {
                    return Err(EvalError::FilterOnly {
                        function: "file.matches".to_string(),
                    });
                }
                let query = expect_text("file.matches", &args[0])?;
                if query.trim().is_empty() {
                    ctx.warnings
                        .warn("file.matches empty query matched no files");
                    return Ok(Value::Bool(false));
                }
                return ctx.vault.file_matches(&file.path, &query).map(Value::Bool);
            }
            let dql_regex = dql_object_value(&receiver, DQL_REGEX_OBJECT_KEY).is_some();
            let regex = match receiver {
                Value::Regex(pattern, flags) => build_regex(&pattern, &flags, "matches")?,
                value => {
                    let Some((regex, _)) = dql_regex_from_object(&value, "matches")? else {
                        return Ok(if dql_regex {
                            Value::Bool(false)
                        } else {
                            Value::Null
                        });
                    };
                    regex
                }
            };
            if dql_regex {
                return match &args[0] {
                    Value::Null => Ok(Value::Bool(false)),
                    Value::Text(text) => Ok(Value::Bool(regex.is_match(text))),
                    _ => Err(invalid_arg("matches", "DQL regex test requires text input")),
                };
            }
            Ok(Value::Bool(regex.is_match(&value_to_string(&args[0]))))
        }
        MethodName::OplogHasChangeSince => {
            expect_arity("oplog.has_change_since", args.len(), 1, 1)?;
            if !ctx.filter_position {
                return Err(EvalError::FilterOnly {
                    function: "oplog.has_change_since".to_string(),
                });
            }
            let duration = expect_text("oplog.has_change_since", &args[0])?;
            let window_ms =
                crate::bases::engine::parse_operator_duration(&duration).ok_or_else(|| {
                    invalid_arg(
                        "oplog.has_change_since",
                        "duration must match ^([1-9][0-9]*)(h|d|w)$ and fit the supported range (e.g. \"7d\")",
                    )
                })?;
            let cutoff_ms = ctx.now_ms.saturating_sub(window_ms);
            ctx.vault
                .oplog_has_change_since(&ctx.file.file_path, cutoff_ms)
                .map(Value::Bool)
        }
        MethodName::OplogHasPropertyChange => {
            expect_arity("oplog.has_property_change", args.len(), 2, 2)?;
            if !ctx.filter_position {
                return Err(EvalError::FilterOnly {
                    function: "oplog.has_property_change".to_string(),
                });
            }
            let key = expect_text("oplog.has_property_change", &args[0])?;
            let duration = expect_text("oplog.has_property_change", &args[1])?;
            let window_ms =
                crate::bases::engine::parse_operator_duration(&duration).ok_or_else(|| {
                    invalid_arg(
                        "oplog.has_property_change",
                        "duration must match ^([1-9][0-9]*)(h|d|w)$ and fit the supported range (e.g. \"7d\")",
                    )
                })?;
            let cutoff_ms = ctx.now_ms.saturating_sub(window_ms);
            ctx.vault
                .oplog_has_property_change(&ctx.file.file_path, &key, cutoff_ms)
                .map(Value::Bool)
        }
        MethodName::OplogDeletedContentMatches => {
            expect_arity("oplog.deleted_content_matches", args.len(), 2, 2)?;
            if !ctx.filter_position {
                return Err(EvalError::FilterOnly {
                    function: "oplog.deleted_content_matches".to_string(),
                });
            }
            let pattern = expect_text("oplog.deleted_content_matches", &args[0])?;
            let duration = expect_text("oplog.deleted_content_matches", &args[1])?;
            let window_ms =
                crate::bases::engine::parse_operator_duration(&duration).ok_or_else(|| {
                    invalid_arg(
                        "oplog.deleted_content_matches",
                        "duration must match ^([1-9][0-9]*)(h|d|w)$ and fit the supported range (e.g. \"7d\")",
                    )
                })?;
            let cutoff_ms = ctx.now_ms.saturating_sub(window_ms);
            ctx.vault
                .oplog_deleted_content_matches(&ctx.file.file_path, &pattern, cutoff_ms)
                .map(Value::Bool)
        }
    }
}

fn dql_tag_values(value: &Value) -> Result<Value, EvalError> {
    let Value::List(tags) = value else {
        return Err(invalid_arg("tags", "DQL file.tags requires a tag list"));
    };
    let mut out = Vec::new();
    for tag in tags {
        let Value::Text(tag) = tag else {
            return Err(invalid_arg("tags", "DQL file.tags contains a non-text tag"));
        };
        let mut current = tag.trim_start_matches('#');
        while !current.is_empty() {
            let value = Value::Text(format!("#{current}"));
            if !out.iter().any(|existing| existing == &value) {
                out.push(value);
            }
            current = current
                .rsplit_once('/')
                .map(|(parent, _)| parent)
                .unwrap_or("");
        }
    }
    Ok(Value::List(out))
}

fn dql_alias_values(row: &RowContext) -> Result<Vec<Value>, EvalError> {
    fn push_alias(alias: String, out: &mut Vec<Value>) {
        let alias = Value::Text(alias);
        if !out.iter().any(|seen| seen == &alias) {
            out.push(alias);
        }
    }

    fn push_value(value: &Value, out: &mut Vec<Value>) -> Result<(), EvalError> {
        match value {
            Value::List(values) => {
                for value in values {
                    let alias = match value {
                        Value::Null => "null".to_string(),
                        value => dql_value_to_string(value)?,
                    };
                    push_alias(alias.trim().to_string(), out);
                }
            }
            value if dql_truthy_value(value) => {
                let text = dql_value_to_string(value)?;
                for alias in text
                    .split(',')
                    .map(str::trim)
                    .filter(|alias| !alias.is_empty())
                {
                    push_alias(alias.to_string(), out);
                }
            }
            _ => {}
        }
        Ok(())
    }

    let mut out = Vec::new();
    for (key, value) in &row.properties {
        if key.eq_ignore_ascii_case("alias") || key.eq_ignore_ascii_case("aliases") {
            push_value(value, &mut out)?;
        }
    }
    Ok(out)
}

fn eval_list_expr(
    base: &Expr,
    kind: ListExprKind,
    body: &Expr,
    init: Option<&Expr>,
    ctx: &EvalCtx<'_>,
    locals: &Locals<'_>,
) -> Result<Value, EvalError> {
    let mut base = eval_inner(base, ctx, locals)?;
    let dql_function = match kind {
        ListExprKind::Filter => Some("filter"),
        ListExprKind::Map => Some("map"),
        ListExprKind::Reduce => None,
    };
    if let Some(function) = dql_function {
        base = unwrap_dql_list_method(base, function)?;
    }
    let Value::List(values) = base else {
        return Ok(Value::Null);
    };
    match kind {
        ListExprKind::Filter => {
            let mut out = Vec::new();
            for (index, value) in values.iter().enumerate() {
                let item_locals = Locals {
                    value: Some(value),
                    index: Some(index),
                    acc: None,
                };
                if eval_inner(body, ctx, &item_locals)?.is_truthy() {
                    out.push(value.clone());
                }
            }
            Ok(Value::List(out))
        }
        ListExprKind::Map => {
            let mut out = Vec::with_capacity(values.len());
            for (index, value) in values.iter().enumerate() {
                let item_locals = Locals {
                    value: Some(value),
                    index: Some(index),
                    acc: None,
                };
                out.push(eval_inner(body, ctx, &item_locals)?);
            }
            Ok(Value::List(out))
        }
        ListExprKind::Reduce => {
            let init = init.ok_or_else(|| invalid_arg("reduce", "missing initializer"))?;
            let mut acc = eval_inner(init, ctx, locals)?;
            for (index, value) in values.iter().enumerate() {
                let item_locals = Locals {
                    value: Some(value),
                    index: Some(index),
                    acc: Some(&acc),
                };
                acc = eval_inner(body, ctx, &item_locals)?;
            }
            Ok(acc)
        }
    }
}

fn eval_date(values: &[Value], ctx: &EvalCtx<'_>) -> Result<Value, EvalError> {
    expect_arity("date", values.len(), 1, 1)?;
    Ok(match &values[0] {
        Value::Date(value) => Value::Date(*value),
        Value::Number(ms) => Value::Date(DateValue {
            epoch_ms: *ms as i64,
            has_time: true,
        }),
        Value::Text(text) => match parse_date_text(text, ctx.now_ms) {
            Some(value) => Value::Date(value),
            None => Value::Null,
        },
        Value::Null => Value::Null,
        _ => return Err(invalid_arg("date", "expected text, number, date, or null")),
    })
}

fn dql_date(value: &Value, ctx: &EvalCtx<'_>) -> Result<Value, EvalError> {
    Ok(match value {
        Value::Null => Value::Null,
        Value::Date(value) => Value::DqlDate(dql_date_from_native(*value)?),
        Value::DqlDate(value) => Value::DqlDate(*value),
        Value::Text(text) => parse_dql_date_text(text, ctx.now_ms)?
            .map(Value::DqlDate)
            .unwrap_or(Value::Null),
        Value::Link(link) => {
            let date = match parse_dql_link_date(link)? {
                some @ Some(_) => some,
                None => dql_link_page_day(link, ctx)?,
            };
            date.map(Value::DqlDate).unwrap_or(Value::Null)
        }
        _ => {
            return Err(invalid_arg(
                "date",
                "DQL date expects text, a date, a link, or null",
            ));
        }
    })
}

pub(crate) fn dql_inline_date_value(text: &str, now_ms: i64) -> Result<Value, EvalError> {
    parse_dql_date_text(text, now_ms)?
        .map(Value::DqlDate)
        .ok_or_else(|| invalid_arg("DQL inline date", "stored inline date is not representable"))
}

fn eval_duration(values: &[Value]) -> Result<Value, EvalError> {
    expect_arity("duration", values.len(), 1, 1)?;
    Ok(match &values[0] {
        Value::Duration(value) => Value::Duration(*value),
        Value::Number(value) => Value::Duration(*value as i64),
        Value::Text(text) => Value::Duration(parse_duration_ms(text).ok_or_else(|| {
            invalid_arg("duration", format!("could not parse duration {text:?}"))
        })?),
        Value::Null => Value::Null,
        _ => {
            return Err(invalid_arg(
                "duration",
                "expected text, number, duration, or null",
            ));
        }
    })
}

fn dql_duration(value: &Value) -> Result<Value, EvalError> {
    Ok(match value {
        Value::Null => Value::Null,
        Value::Duration(value) => Value::DqlDuration(DqlDurationValue::fixed(*value)),
        Value::DqlDuration(value) => Value::DqlDuration(*value),
        Value::Text(text) => parse_dql_duration_parts(text)?
            .map(Value::DqlDuration)
            .unwrap_or(Value::Null),
        _ => {
            return Err(invalid_arg(
                "duration",
                "DQL duration expects text, a duration, or null",
            ));
        }
    })
}

pub(crate) fn dql_inline_duration_value(text: &str) -> Result<Value, EvalError> {
    parse_dql_duration_parts(text)?
        .map(Value::DqlDuration)
        .ok_or_else(|| {
            invalid_arg(
                "DQL inline duration",
                "stored inline duration is not representable",
            )
        })
}

fn dql_link(
    value: &Value,
    display: Option<&Value>,
    embed: Option<&Value>,
    ctx: &EvalCtx<'_>,
) -> Result<Value, EvalError> {
    if embed.is_some() {
        if !matches!(value, Value::Text(_)) {
            return Err(invalid_arg(
                "link",
                "DQL three-argument link path requires text",
            ));
        }
        if !matches!(display, Some(Value::Text(_))) {
            return Err(invalid_arg(
                "link",
                "DQL three-argument link display requires text",
            ));
        }
        if !matches!(embed, Some(Value::Bool(_))) {
            return Err(invalid_arg(
                "link",
                "DQL three-argument link embed requires a boolean",
            ));
        }
    }
    if matches!(value, Value::Null) {
        return Ok(Value::Null);
    }
    let display = match display {
        None | Some(Value::Null) => None,
        Some(Value::Text(display)) => Some(display.clone()),
        Some(_) => {
            return Err(invalid_arg(
                "link",
                "DQL link display requires text or null",
            ));
        }
    };
    let embed = match embed {
        None => None,
        Some(Value::Bool(embed)) => Some(*embed),
        Some(_) => return Err(invalid_arg("link", "DQL link embed requires a boolean")),
    };
    match value {
        Value::Link(link) => {
            let mut link = link.clone();
            if display.is_some() {
                link.display = display;
            }
            if let Some(embed) = embed {
                link.embed = embed;
            }
            Ok(Value::Link(link))
        }
        Value::Text(target) => {
            let (path, link_type, subpath) = if let Some((path, subpath)) = target.split_once('#') {
                if let Some(block) = subpath.strip_prefix('^') {
                    (path, "block", Some(block.to_string()))
                } else {
                    (path, "header", Some(normalize_header_for_link(subpath)))
                }
            } else {
                (target.as_str(), "file", None)
            };
            let resolved_path = ctx.vault.resolve_link(path);
            Ok(Value::Link(LinkValue {
                target: resolved_path.clone().unwrap_or_else(|| path.to_string()),
                resolved_path,
                display,
                subpath,
                link_type: link_type.to_string(),
                embed: embed.unwrap_or(false),
            }))
        }
        _ => Err(invalid_arg(
            "link",
            "DQL link expects text, a link, or null",
        )),
    }
}

fn normalize_list(mut values: Vec<Value>) -> Result<Value, EvalError> {
    expect_arity("list", values.len(), 1, 1)?;
    Ok(match values.pop().expect("arity checked") {
        value @ Value::List(_) => value,
        value => Value::List(vec![value]),
    })
}

fn eval_object_constructor(values: &[Value]) -> Result<Value, EvalError> {
    if values.len() == 1 && matches!(values[0], Value::Object(_)) {
        return Ok(values[0].clone());
    }
    if !values.len().is_multiple_of(2) {
        return Err(invalid_arg("object", "expected key/value pairs"));
    }
    let mut out = BTreeMap::new();
    for pair in values.chunks(2) {
        out.insert(expect_text_like("object", &pair[0])?, pair[1].clone());
    }
    Ok(Value::Object(out))
}

fn dql_object_constructor(values: &[Value]) -> Result<Value, EvalError> {
    if values.is_empty() {
        return Ok(Value::Object(BTreeMap::new()));
    }
    if !values.len().is_multiple_of(2) {
        return Err(invalid_arg("object", "expected DQL key/value pairs"));
    }
    let mut out = BTreeMap::new();
    for pair in values.chunks(2) {
        let Value::Text(key) = &pair[0] else {
            return Err(invalid_arg("object", "DQL object keys must be text"));
        };
        out.insert(key.clone(), pair[1].clone());
    }
    Ok(Value::Object(out))
}

fn eval_aggregate(
    function: GlobalFn,
    values: &[Value],
    ctx: &EvalCtx<'_>,
) -> Result<Value, EvalError> {
    let mut flattened = Vec::new();
    for value in values {
        if let Some(value) = dql_object_value(value, DQL_AGGREGATE_OBJECT_KEY) {
            let Value::List(items) = value else {
                return Err(invalid_arg(
                    aggregate_name(function),
                    "DQL aggregate requires a list",
                ));
            };
            if matches!(function, GlobalFn::Min | GlobalFn::Max) {
                return dql_min_max(items, function);
            }
            return dql_sum_average(items, function, ctx);
        }
        match value {
            Value::List(items) => flattened.extend(items.iter().cloned()),
            other => flattened.push(other.clone()),
        }
    }
    let mut numbers = Vec::new();
    for value in &flattened {
        if let Some(number) = coerce_number(value) {
            numbers.push(number);
        }
    }
    if numbers.is_empty() {
        return Ok(Value::Null);
    }
    let value = match function {
        GlobalFn::Min => numbers.into_iter().fold(f64::INFINITY, f64::min),
        GlobalFn::Max => numbers.into_iter().fold(f64::NEG_INFINITY, f64::max),
        GlobalFn::Sum => numbers.into_iter().sum(),
        GlobalFn::Average => numbers.iter().sum::<f64>() / numbers.len() as f64,
        _ => unreachable!(),
    };
    Ok(number_or_null(value, ctx))
}

fn aggregate_name(function: GlobalFn) -> &'static str {
    match function {
        GlobalFn::Min => "min",
        GlobalFn::Max => "max",
        GlobalFn::Sum => "sum",
        GlobalFn::Average => "average",
        _ => unreachable!("aggregate_name is only called for aggregate functions"),
    }
}

fn dql_min_max(items: &[Value], function: GlobalFn) -> Result<Value, EvalError> {
    let mut comparable = items.iter().filter(|value| !matches!(value, Value::Null));
    let Some(mut selected) = comparable.next() else {
        return Ok(Value::Null);
    };
    for candidate in comparable {
        let ordering = dql_ordering_cmp(selected, candidate, aggregate_name(function))?;
        let keep_selected = if function == GlobalFn::Min {
            matches!(ordering, Ordering::Less | Ordering::Equal)
        } else {
            ordering == Ordering::Greater
        };
        if !keep_selected {
            selected = candidate;
        }
    }
    Ok(selected.clone())
}

fn dql_sum_average(
    items: &[Value],
    function: GlobalFn,
    ctx: &EvalCtx<'_>,
) -> Result<Value, EvalError> {
    let Some(first) = items.first() else {
        return Ok(Value::Null);
    };
    let mut total = first.clone();
    for value in &items[1..] {
        if matches!(value, Value::Null) {
            continue;
        }
        total = dql_sum_add(total, value, ctx)?;
    }
    if function == GlobalFn::Sum {
        return Ok(total);
    }
    match total {
        Value::Null => Ok(Value::Null),
        Value::Number(total) => Ok(number_or_null(total / items.len() as f64, ctx)),
        Value::Duration(total) => Ok(Value::Duration(total / items.len() as i64)),
        Value::DqlDuration(total) => scale_dql_duration(total, 1.0 / items.len() as f64, "average"),
        _ => Err(invalid_arg(
            "average",
            "DQL average requires a numeric or duration sum",
        )),
    }
}

fn dql_sum_add(lhs: Value, rhs: &Value, ctx: &EvalCtx<'_>) -> Result<Value, EvalError> {
    dql_add_values(&lhs, rhs, ctx)
}

fn dql_object_value<'a>(value: &'a Value, key: &str) -> Option<&'a Value> {
    let Value::Object(values) = value else {
        return None;
    };
    values.get(key)
}

fn dql_or_native_truthy(value: &Value) -> bool {
    let Some(value) = dql_object_value(value, DQL_TRUTHY_OBJECT_KEY) else {
        return value.is_truthy();
    };
    dql_truthy_value(value)
}

fn dql_truthy_value(value: &Value) -> bool {
    match value {
        Value::Null => false,
        Value::Bool(value) => *value,
        Value::Number(value) => *value != 0.0,
        Value::Text(value) => !value.is_empty(),
        Value::List(value) => !value.is_empty(),
        Value::Object(value) => !value.is_empty(),
        Value::Date(value) => value.epoch_ms != 0,
        Value::DqlDate(value) => value.epoch_ms != 0,
        Value::Duration(value) => *value != 0,
        Value::DqlDuration(value) => value.casual_milliseconds() != 0.0,
        Value::Link(value) => !value.target.is_empty(),
        Value::File(value) => !value.path.is_empty(),
        Value::Regex(_, _) => true,
    }
}

fn dql_number(value: &Value) -> Result<Option<f64>, EvalError> {
    let text = match value {
        Value::Null => return Ok(None),
        Value::Number(number) => return Ok(Some(*number)),
        Value::Text(text) => text,
        _ => {
            return Err(invalid_arg(
                "number",
                "DQL number expects text, a number, or null",
            ));
        }
    };
    static NUMBER: OnceLock<Regex> = OnceLock::new();
    let regex = NUMBER.get_or_init(|| {
        Regex::new(r"-?[0-9]+(?:\.[0-9]+)?").expect("DQL number extraction regex is valid")
    });
    let Some(number) = regex.find(text) else {
        return Ok(None);
    };
    number
        .as_str()
        .parse::<f64>()
        .map(Some)
        .map_err(|_| invalid_arg("number", "DQL number could not parse the numeric substring"))
}

fn dql_length(value: &Value) -> Result<Value, EvalError> {
    match value {
        Value::Text(text) => Ok(Value::Number(text.encode_utf16().count() as f64)),
        Value::List(values) => Ok(Value::Number(values.len() as f64)),
        Value::Object(values) => Ok(Value::Number(values.len() as f64)),
        Value::Null => Ok(Value::Number(0.0)),
        _ => Err(invalid_arg(
            "length",
            "DQL length expects text, a list, an object, or null",
        )),
    }
}

fn dql_reverse(value: &Value) -> Result<Value, EvalError> {
    match value {
        Value::Text(text) => {
            if contains_non_bmp(text) {
                return Err(invalid_arg(
                    "reverse",
                    "DQL reverse cannot represent UTF-16 surrogate fragments",
                ));
            }
            Ok(Value::Text(text.chars().rev().collect()))
        }
        Value::List(values) => {
            let mut values = values.clone();
            values.reverse();
            Ok(Value::List(values))
        }
        value => Ok(value.clone()),
    }
}

fn unwrap_dql_list_method(receiver: Value, function: &str) -> Result<Value, EvalError> {
    let Some(value) = dql_object_value(&receiver, DQL_LIST_METHOD_OBJECT_KEY) else {
        return Ok(receiver);
    };
    match value {
        Value::List(_) | Value::Null => Ok(value.clone()),
        _ => Err(invalid_arg(
            function,
            format!("DQL {function} requires a list"),
        )),
    }
}

fn row_property(row: &RowContext, key: &str) -> Value {
    row.properties
        .iter()
        .rev()
        .find(|(got, _)| got == key)
        .map(|(_, value)| value.clone())
        .or_else(|| row.file_fields.properties.get(key).cloned())
        .unwrap_or(Value::Null)
}

fn dql_combined_properties(
    row: &RowContext,
    ctx: &EvalCtx<'_>,
) -> Result<Vec<(String, Value)>, EvalError> {
    let (inline, incomplete) = ctx
        .vault
        .dql_inline_fields_for(&row.file_path, ctx.now_ms)?;
    if incomplete {
        return Err(invalid_arg(
            "DQL property",
            format!("DQL inline-field index is incomplete for {}", row.file_path),
        ));
    }
    let mut properties = row.properties.clone();
    properties.extend(inline);
    Ok(properties)
}

fn dql_merge_property_values(values: impl IntoIterator<Item = Value>) -> Value {
    let mut matching = values.into_iter().collect::<Vec<_>>();
    match matching.len() {
        0 => Value::Null,
        1 => matching.pop().expect("one matching property"),
        _ => Value::List(matching),
    }
}

fn dql_row_property(row: &RowContext, key: &str, ctx: &EvalCtx<'_>) -> Result<Value, EvalError> {
    let properties = dql_combined_properties(row, ctx)?;
    let exact = properties
        .iter()
        .filter(|(got, _)| got == key)
        .map(|(_, value)| value.clone())
        .collect::<Vec<_>>();
    if !exact.is_empty() {
        return Ok(dql_merge_property_values(exact));
    }

    let canonical_key = dql_canonical_property_key(key)?;
    let mut matching = Vec::<Value>::new();
    for (got, value) in &properties {
        if dql_canonical_property_key(got)? == canonical_key {
            matching.push(value.clone());
        }
    }
    if matching.is_empty() {
        for (got, value) in &row.file_fields.properties {
            if got == key {
                return Ok(value.clone());
            }
            if dql_canonical_property_key(got)? == canonical_key {
                matching.push(value.clone());
            }
        }
    }
    Ok(dql_merge_property_values(matching))
}

fn dql_canonical_property_key(key: &str) -> Result<String, EvalError> {
    static LETTER: OnceLock<Regex> = OnceLock::new();
    static EMOJI: OnceLock<Regex> = OnceLock::new();
    static EMOJI_COMPONENT: OnceLock<Regex> = OnceLock::new();
    let letter = LETTER.get_or_init(|| {
        Regex::new(r"\A\p{Letter}\z").expect("Unicode Letter property is supported")
    });
    let emoji = EMOJI.get_or_init(|| {
        Regex::new(
            r"(?x)\A(?:
                \p{Regional_Indicator}{2}
              | [0-9\#\*]\u{FE0F}?\u{20E3}
              | (?:\p{Emoji_Presentation}|\p{Emoji}\u{FE0F})
                \p{Emoji_Modifier}?
                (?:\u{200D}(?:\p{Emoji_Presentation}|\p{Emoji}\u{FE0F})\p{Emoji_Modifier}?)*
            )",
        )
        .expect("Unicode Emoji properties are supported")
    });
    let emoji_component = EMOJI_COMPONENT.get_or_init(|| {
        Regex::new(r"\A(?:\p{Emoji}|\p{Emoji_Component})\z")
            .expect("Unicode Emoji component properties are supported")
    });

    let mut out = String::new();
    let mut in_whitespace = false;
    let mut pos = 0usize;
    while pos < key.len() {
        let rest = &key[pos..];
        if let Some(found) = emoji.find(rest) {
            out.push_str(found.as_str());
            pos += found.end();
            in_whitespace = false;
            continue;
        }
        let character = rest
            .chars()
            .next()
            .expect("position is a character boundary");
        pos += character.len_utf8();
        if character.is_whitespace() {
            if !in_whitespace {
                out.push('-');
                in_whitespace = true;
            }
            continue;
        }
        in_whitespace = false;
        if character.is_ascii_digit()
            || matches!(character, '_' | '-')
            || letter.is_match(&character.to_string())
        {
            out.extend(character.to_lowercase());
        } else if emoji_component.is_match(&character.to_string()) {
            return Err(invalid_arg(
                "DQL property",
                "property name contains an unsupported partial emoji sequence",
            ));
        }
    }
    Ok(out)
}

fn file_field_value(row: &RowContext, field: FileField, ctx: &EvalCtx<'_>) -> Value {
    match field {
        FileField::Name => Value::Text(row.file_fields.name.clone()),
        FileField::Basename => Value::Text(row.file_fields.basename.clone()),
        FileField::Path => Value::Text(row.file_fields.path.clone()),
        FileField::Folder => Value::Text(row.file_fields.folder.clone()),
        FileField::Ext => Value::Text(row.file_fields.ext.clone()),
        FileField::Size => Value::Number(row.file_fields.size as f64),
        FileField::Properties => {
            let mut properties = row.file_fields.properties.clone();
            for (key, value) in &row.properties {
                properties
                    .entry(key.clone())
                    .or_insert_with(|| value.clone());
            }
            Value::Object(properties)
        }
        FileField::Tags => Value::List(
            row.file_fields
                .tags
                .iter()
                .cloned()
                .map(Value::Text)
                .collect(),
        ),
        FileField::Aliases => Value::List(
            row.file_fields
                .aliases
                .iter()
                .cloned()
                .map(Value::Text)
                .collect(),
        ),
        FileField::Links => {
            let links = if row.file_fields.links.is_empty() {
                ctx.vault.links_for(&row.file_path)
            } else {
                row.file_fields.links.clone()
            };
            Value::List(links.into_iter().map(Value::Link).collect())
        }
        FileField::Backlinks => {
            let links = if row.file_fields.backlinks.is_empty() {
                ctx.vault.backlinks_for(&row.file_path)
            } else {
                row.file_fields.backlinks.clone()
            };
            Value::List(links.into_iter().map(Value::Link).collect())
        }
        FileField::Embeds => Value::List(
            row.file_fields
                .embeds
                .iter()
                .cloned()
                .map(Value::Link)
                .collect(),
        ),
        FileField::File => Value::File(FileHandleValue {
            path: row.file_path.clone(),
        }),
        FileField::Tasks => row
            .file_fields
            .properties
            .get("tasks")
            .cloned()
            .unwrap_or(Value::Null),
        FileField::Ctime => row
            .file_fields
            .ctime
            .map(Value::Date)
            .unwrap_or(Value::Null),
        FileField::Mtime => row
            .file_fields
            .mtime
            .map(Value::Date)
            .unwrap_or(Value::Null),
        FileField::InDegree => Value::Number(row.file_fields.in_degree as f64),
        FileField::OutDegree => Value::Number(row.file_fields.out_degree as f64),
    }
}

fn task_field_value(row: &RowContext, field: TaskField) -> Value {
    let Some(task) = row.task.as_ref() else {
        return Value::Null;
    };
    match field {
        TaskField::Text => Value::Text(task.text.clone()),
        TaskField::Status => Value::Text(task.status.clone()),
        TaskField::Completed => Value::Bool(task.completed),
        TaskField::Due => task.due.map(Value::Date).unwrap_or(Value::Null),
        TaskField::Scheduled => task.scheduled.map(Value::Date).unwrap_or(Value::Null),
        TaskField::Priority => task.priority.map(Value::Number).unwrap_or(Value::Null),
        TaskField::File => Value::File(FileHandleValue {
            path: row.file_path.clone(),
        }),
    }
}

fn index_value(base: &Value, index: &Value, ctx: &EvalCtx<'_>) -> Result<Value, EvalError> {
    Ok(match (base, index) {
        (Value::List(values), index) => coerce_number(index)
            .and_then(index_as_usize)
            .and_then(|idx| values.get(idx).cloned())
            .unwrap_or(Value::Null),
        (Value::Object(map), Value::Text(key)) if map.contains_key(DQL_ROW_PROPERTY_OBJECT_KEY) => {
            let row = if matches!(
                map.get(DQL_ROW_PROPERTY_OBJECT_KEY),
                Some(Value::Bool(true))
            ) {
                let Some(row) = ctx.this else {
                    return Ok(Value::Null);
                };
                row
            } else {
                ctx.file
            };
            return dql_row_property(row, key, ctx);
        }
        (Value::Object(map), index) => map
            .get(&value_to_string(index))
            .cloned()
            .unwrap_or(Value::Null),
        (Value::Text(text), index) => coerce_number(index)
            .and_then(index_as_usize)
            .and_then(|idx| text.chars().nth(idx))
            .map(|ch| Value::Text(ch.to_string()))
            .unwrap_or(Value::Null),
        (Value::File(file), index) => {
            let row = row_for_path(ctx, &file.path);
            row_property(&row, &value_to_string(index))
        }
        _ => Value::Null,
    })
}

fn index_as_usize(index: f64) -> Option<usize> {
    if !index.is_finite() || index < 0.0 || index > usize::MAX as f64 {
        return None;
    }
    Some(index.trunc() as usize)
}

fn field_value(base: &Value, name: &str, ctx: &EvalCtx<'_>) -> Result<Value, EvalError> {
    Ok(match base {
        Value::Object(map) if map.contains_key(DQL_DATA_ARRAY_OBJECT_KEY) => {
            let value = map
                .get(DQL_DATA_ARRAY_OBJECT_KEY)
                .expect("marker presence checked");
            if let Value::List(values) = value {
                let mut projected = Vec::new();
                for value in values {
                    match field_value(value, name, ctx)? {
                        Value::List(values) => projected.extend(values),
                        Value::Null => {}
                        value => projected.push(value),
                    }
                }
                Value::List(projected)
            } else {
                field_value(value, name, ctx)?
            }
        }
        Value::Object(map) if name == "length" && map.contains_key(DQL_LENGTH_OBJECT_KEY) => {
            dql_length(
                map.get(DQL_LENGTH_OBJECT_KEY)
                    .expect("marker presence checked"),
            )?
        }
        Value::Object(map) => map.get(name).cloned().unwrap_or(Value::Null),
        Value::List(values) if name == "length" => Value::Number(values.len() as f64),
        Value::Text(text) if name == "length" => Value::Number(text.chars().count() as f64),
        Value::Date(date) => date_field(*date, name)?,
        Value::DqlDate(date) => dql_date_field(*date, name)?,
        Value::File(file) => {
            let row = row_for_path(ctx, &file.path);
            file_field_by_name(name)
                .map(|field| file_field_value(&row, field, ctx))
                .unwrap_or_else(|| row_property(&row, name))
        }
        Value::Link(link) => match name {
            "display" => link.display.clone().map(Value::Text).unwrap_or(Value::Null),
            "path" | "target" => Value::Text(
                link.resolved_path
                    .clone()
                    .unwrap_or_else(|| link.target.clone()),
            ),
            _ => Value::Null,
        },
        _ => Value::Null,
    })
}

fn date_field(date: DateValue, name: &str) -> Result<Value, EvalError> {
    let dt = date_time(date)?;
    Ok(match name {
        "year" => Value::Number(dt.year() as f64),
        "month" => Value::Number(dt.month() as f64),
        "day" => Value::Number(dt.day() as f64),
        "hour" => Value::Number(dt.hour() as f64),
        "minute" => Value::Number(dt.minute() as f64),
        "second" => Value::Number(dt.second() as f64),
        "millisecond" => Value::Number(dt.timestamp_subsec_millis() as f64),
        _ => Value::Null,
    })
}

fn dql_date_field(date: DqlDateValue, name: &str) -> Result<Value, EvalError> {
    let dt = dql_date_time(date)?;
    Ok(match name {
        "year" => Value::Number(dt.year() as f64),
        "month" => Value::Number(dt.month() as f64),
        "day" => Value::Number(dt.day() as f64),
        "hour" => Value::Number(dt.hour() as f64),
        "minute" => Value::Number(dt.minute() as f64),
        "second" => Value::Number(dt.second() as f64),
        "millisecond" => Value::Number(dt.timestamp_subsec_millis() as f64),
        _ => Value::Null,
    })
}

fn string_method(
    receiver: &Value,
    name: &str,
    args: &[Value],
    arity: usize,
    f: impl Fn(&str, &[Value]) -> Value,
) -> Result<Value, EvalError> {
    expect_arity(name, args.len(), arity, arity)?;
    match receiver {
        Value::Text(text) => Ok(f(text, args)),
        Value::Null => Ok(Value::Null),
        other => Ok(f(&value_to_string(other), args)),
    }
}

fn split_value(receiver: Value, args: &[Value]) -> Result<Value, EvalError> {
    expect_arity("split", args.len(), 1, 2)?;
    let dql_split = dql_object_value(&args[0], DQL_REGEX_OBJECT_KEY).is_some();
    if dql_split
        && matches!(
            dql_object_value(&args[0], DQL_REGEX_OBJECT_KEY),
            Some(Value::Null)
        )
    {
        return Ok(Value::Null);
    }
    let text = match receiver {
        Value::Text(text) => text,
        Value::Null => return Ok(Value::Null),
        Value::List(_) if matches!(args[0], Value::Regex(_, _) | Value::Object(_)) => {
            return Err(invalid_arg("split", "DQL split expects scalar text"));
        }
        _ if dql_split => return Err(invalid_arg("split", "DQL split requires text input")),
        other => value_to_string(&other),
    };
    let limit = match args.get(1) {
        None => None,
        Some(Value::Null) if dql_split => return Ok(Value::Null),
        Some(Value::Number(limit)) => Some(javascript_uint32(*limit) as usize),
        Some(_) => return Err(invalid_arg("split", "expected numeric limit")),
    };
    let mut parts = match &args[0] {
        Value::Text(separator) if separator.is_empty() => text
            .chars()
            .map(|character| Value::Text(character.to_string()))
            .collect::<Vec<_>>(),
        Value::Text(separator) => text
            .split(separator)
            .map(|part| Value::Text(part.to_string()))
            .collect::<Vec<_>>(),
        Value::Regex(pattern, flags) => {
            regex_split_with_captures(&build_regex(pattern, flags, "split")?, &text)
        }
        value => {
            if let Some((regex, _)) = dql_regex_from_object(value, "split")? {
                if regex.is_match("") && contains_non_bmp(&text) {
                    return Err(invalid_arg(
                        "split",
                        "DQL empty-matching regex cannot advance safely through non-BMP text",
                    ));
                }
                regex_split_with_captures(&regex, &text)
            } else {
                return Err(invalid_arg(
                    "split",
                    "expected text or regular expression separator",
                ));
            }
        }
    };
    if let Some(limit) = limit {
        parts.truncate(limit);
    }
    Ok(Value::List(parts))
}

fn regex_split_with_captures(regex: &Regex, text: &str) -> Vec<Value> {
    if text.is_empty() && regex.is_match(text) {
        return Vec::new();
    }
    let mut parts = Vec::new();
    let mut previous_end = 0usize;
    let mut previous_match_was_nonempty = false;
    for captures in regex.captures_iter(text) {
        let Some(whole_match) = captures.get(0) else {
            continue;
        };
        if whole_match.is_empty() && (whole_match.start() == 0 || whole_match.start() == text.len())
        {
            previous_match_was_nonempty = false;
            continue;
        }
        if whole_match.is_empty()
            && previous_match_was_nonempty
            && whole_match.start() == previous_end
        {
            previous_match_was_nonempty = false;
            continue;
        }
        parts.push(Value::Text(
            text[previous_end..whole_match.start()].to_string(),
        ));
        for capture_index in 1..captures.len() {
            parts.push(
                captures
                    .get(capture_index)
                    .map(|capture| Value::Text(capture.as_str().to_string()))
                    .unwrap_or_else(|| Value::Text(String::new())),
            );
        }
        previous_end = whole_match.end();
        previous_match_was_nonempty = !whole_match.is_empty();
    }
    parts.push(Value::Text(text[previous_end..].to_string()));
    parts
}

fn javascript_uint32(value: f64) -> u32 {
    if !value.is_finite() || value == 0.0 {
        return 0;
    }
    value.trunc().rem_euclid(u32::MAX as f64 + 1.0) as u32
}

fn number_method(
    receiver: Value,
    name: &str,
    args: &[Value],
    arity: usize,
    f: impl Fn(f64, &[Value]) -> Value,
) -> Result<Value, EvalError> {
    expect_arity(name, args.len(), arity, arity)?;
    Ok(coerce_number(&receiver)
        .map(|number| f(number, args))
        .unwrap_or(Value::Null))
}

fn dql_number_method(
    receiver: Value,
    name: &str,
    args: &[Value],
    operation: impl FnOnce(f64) -> f64,
) -> Result<Value, EvalError> {
    expect_arity(name, args.len(), 0, 0)?;
    match dql_object_value(&receiver, DQL_NUMBER_METHOD_OBJECT_KEY)
        .expect("DQL number method caller checked marker")
    {
        Value::Null => Ok(Value::Null),
        Value::Number(number) => Ok(Value::Number(operation(*number))),
        _ => Err(invalid_arg(name, format!("DQL {name} requires a number"))),
    }
}

fn slice_value(receiver: Value, args: &[Value]) -> Result<Value, EvalError> {
    if let Some(value) = dql_object_value(&receiver, DQL_SUBSTRING_OBJECT_KEY) {
        expect_arity("slice", args.len(), 1, 2)?;
        return dql_substring(value, args);
    }
    if let Some(value) = dql_object_value(&receiver, DQL_LIST_METHOD_OBJECT_KEY) {
        expect_arity("slice", args.len(), 0, 2)?;
        if matches!(value, Value::Null) {
            return Ok(Value::Null);
        }
        let Value::List(values) = value else {
            return Err(invalid_arg("slice", "DQL slice requires a list"));
        };
        return Ok(Value::List(dql_list_slice(values, args)?));
    }
    expect_arity("slice", args.len(), 1, 2)?;
    let start = coerce_number(&args[0]).unwrap_or(0.0).max(0.0) as usize;
    let end = args
        .get(1)
        .and_then(coerce_number)
        .map(|n| n.max(0.0) as usize);
    Ok(match receiver {
        Value::Text(text) => {
            let chars: Vec<char> = text.chars().collect();
            let end = end.unwrap_or(chars.len()).min(chars.len());
            Value::Text(chars[start.min(end)..end].iter().collect())
        }
        Value::List(values) => {
            let end = end.unwrap_or(values.len()).min(values.len());
            Value::List(values[start.min(end)..end].to_vec())
        }
        _ => Value::Null,
    })
}

fn dql_list_slice(values: &[Value], args: &[Value]) -> Result<Vec<Value>, EvalError> {
    let indices = args
        .iter()
        .map(|value| match value {
            Value::Number(index) => Ok(*index),
            _ => Err(invalid_arg("slice", "DQL slice indices require numbers")),
        })
        .collect::<Result<Vec<_>, _>>()?;
    let start = indices
        .first()
        .map(|index| dql_slice_index(*index, values.len()))
        .unwrap_or(0);
    let end = args
        .get(1)
        .and_then(|_| indices.get(1))
        .map(|value| dql_slice_index(*value, values.len()))
        .unwrap_or(values.len());
    if end <= start {
        Ok(Vec::new())
    } else {
        Ok(values[start..end].to_vec())
    }
}

fn dql_slice_index(index: f64, length: usize) -> usize {
    if index.is_nan() {
        return 0;
    }
    if index == f64::INFINITY {
        return length;
    }
    if index == f64::NEG_INFINITY {
        return 0;
    }
    let index = index.trunc();
    if index < 0.0 {
        (length as f64 + index).max(0.0) as usize
    } else {
        index.min(length as f64) as usize
    }
}

fn dql_substring(value: &Value, args: &[Value]) -> Result<Value, EvalError> {
    if matches!(value, Value::Null) || args.iter().any(|value| matches!(value, Value::Null)) {
        return Ok(Value::Null);
    }
    let Value::Text(text) = value else {
        return Err(invalid_arg("substring", "DQL substring expects text"));
    };
    let indices = args
        .iter()
        .map(|value| match value {
            Value::Number(index) => Ok(*index),
            _ => Err(invalid_arg(
                "substring",
                "DQL substring indices require numbers",
            )),
        })
        .collect::<Result<Vec<_>, _>>()?;
    let utf16_len = text.encode_utf16().count();
    let mut start = dql_substring_index(indices[0], utf16_len);
    let mut end = indices
        .get(1)
        .map(|value| dql_substring_index(*value, utf16_len))
        .unwrap_or(utf16_len);
    if start > end {
        std::mem::swap(&mut start, &mut end);
    }
    let start = utf16_index_to_byte(text, start).ok_or_else(|| {
        invalid_arg(
            "substring",
            "DQL substring cannot represent a UTF-16 surrogate fragment",
        )
    })?;
    let end = utf16_index_to_byte(text, end).ok_or_else(|| {
        invalid_arg(
            "substring",
            "DQL substring cannot represent a UTF-16 surrogate fragment",
        )
    })?;
    Ok(Value::Text(text[start..end].to_string()))
}

fn dql_substring_index(index: f64, length: usize) -> usize {
    if index.is_nan() || index <= 0.0 {
        0
    } else if index.is_infinite() || index >= length as f64 {
        length
    } else {
        index.trunc() as usize
    }
}

fn utf16_index_to_byte(text: &str, wanted: usize) -> Option<usize> {
    if wanted == 0 {
        return Some(0);
    }
    let mut utf16_index = 0usize;
    for (byte_index, character) in text.char_indices() {
        if utf16_index == wanted {
            return Some(byte_index);
        }
        utf16_index += character.len_utf16();
        if utf16_index > wanted {
            return None;
        }
    }
    (utf16_index == wanted).then_some(text.len())
}

fn contains_non_bmp(text: &str) -> bool {
    text.chars().any(|character| character.len_utf16() > 1)
}

fn replace_value(receiver: Value, args: &[Value]) -> Result<Value, EvalError> {
    expect_arity("replace", args.len(), 2, 2)?;
    let dql_replace = dql_object_value(&args[0], DQL_LITERAL_REPLACE_OBJECT_KEY).is_some()
        || dql_object_value(&args[0], DQL_REGEX_OBJECT_KEY).is_some();
    if dql_replace
        && (matches!(
            dql_object_value(&args[0], DQL_LITERAL_REPLACE_OBJECT_KEY),
            Some(Value::Null)
        ) || matches!(
            dql_object_value(&args[0], DQL_REGEX_OBJECT_KEY),
            Some(Value::Null)
        ))
    {
        return Ok(Value::Null);
    }
    let (text, replacement) = if dql_replace {
        match (&receiver, &args[1]) {
            (Value::Null, _) | (_, Value::Null) => return Ok(Value::Null),
            (Value::Text(text), Value::Text(replacement)) => (text.clone(), replacement.clone()),
            _ => {
                return Err(invalid_arg(
                    "replace",
                    "DQL replace requires text input and replacement",
                ));
            }
        }
    } else {
        (value_to_string(&receiver), value_to_string(&args[1]))
    };
    match &args[0] {
        Value::Regex(pattern, flags) => {
            let regex = build_regex(pattern, flags, "replace")?;
            if flags.contains('g') {
                Ok(Value::Text(
                    regex.replace_all(&text, replacement.as_str()).to_string(),
                ))
            } else {
                Ok(Value::Text(
                    regex.replace(&text, replacement.as_str()).to_string(),
                ))
            }
        }
        pattern => {
            if let Some(pattern) = dql_object_value(pattern, DQL_LITERAL_REPLACE_OBJECT_KEY) {
                let pattern = match pattern {
                    Value::Null => return Ok(Value::Null),
                    Value::Text(pattern) => pattern,
                    _ => {
                        return Err(invalid_arg("replace", "DQL replace pattern requires text"));
                    }
                };
                return dql_literal_replace(&text, pattern, &replacement).map(Value::Text);
            }
            if matches!(
                dql_object_value(pattern, DQL_REGEX_OBJECT_KEY),
                Some(Value::Null)
            ) {
                return Ok(Value::Null);
            }
            if let Some((regex, mode)) = dql_regex_from_object(pattern, "replace")? {
                if mode == "global" {
                    if regex.is_match("") && contains_non_bmp(&text) {
                        return Err(invalid_arg(
                            "replace",
                            "DQL empty-matching regex cannot advance safely through non-BMP text",
                        ));
                    }
                    Ok(Value::Text(dql_regex_replace_all(
                        &regex,
                        &text,
                        &replacement,
                    )))
                } else {
                    Ok(Value::Text(dql_regex_replace_first(
                        &regex,
                        &text,
                        &replacement,
                    )))
                }
            } else {
                Ok(Value::Text(
                    text.replace(&value_to_string(pattern), &replacement),
                ))
            }
        }
    }
}

fn dql_literal_replace(text: &str, pattern: &str, replacement: &str) -> Result<String, EvalError> {
    if !pattern.is_empty() {
        return Ok(text.replace(pattern, replacement));
    }
    if contains_non_bmp(text) {
        return Err(invalid_arg(
            "replace",
            "DQL empty replacement pattern cannot split UTF-16 surrogate pairs safely",
        ));
    }
    let mut characters = text.chars();
    let Some(first) = characters.next() else {
        return Ok(String::new());
    };
    let mut out = String::new();
    out.push(first);
    for character in characters {
        out.push_str(replacement);
        out.push(character);
    }
    Ok(out)
}

fn javascript_round(number: f64) -> f64 {
    if !number.is_finite() || number == 0.0 {
        return number;
    }
    let floor = number.floor();
    let mut rounded = if number - floor < 0.5 {
        floor
    } else {
        floor + 1.0
    };
    if rounded == 0.0 && number.is_sign_negative() {
        rounded = -0.0;
    }
    rounded
}

fn javascript_round_with_precision(number: f64, precision: f64) -> Result<f64, EvalError> {
    if !precision.is_finite() {
        return Err(invalid_arg("round", "expected a finite precision"));
    }
    if precision <= 0.0 {
        return Ok(javascript_round(number));
    }
    let precision = precision.trunc();
    if !number.is_finite() || number.abs() >= 1e21 {
        return Ok(number);
    }
    if precision > 15.0 {
        return Err(invalid_arg(
            "round",
            "precision exceeds Slate's exact JavaScript-compatible limit",
        ));
    }
    let precision = precision as u32;
    let bits = number.abs().to_bits();
    let fraction = bits & ((1_u64 << 52) - 1);
    let exponent_bits = ((bits >> 52) & 0x7ff) as i32;
    let (significand, binary_exponent) = if exponent_bits == 0 {
        (fraction, -1074)
    } else {
        ((1_u64 << 52) | fraction, exponent_bits - 1023 - 52)
    };
    let numerator = u128::from(significand)
        .checked_mul(5_u128.pow(precision))
        .ok_or_else(|| invalid_arg("round", "precision is too large"))?;
    let binary_exponent = binary_exponent + precision as i32;
    let rounded = if binary_exponent >= 0 {
        numerator
            .checked_shl(binary_exponent as u32)
            .ok_or_else(|| invalid_arg("round", "value is too large to round exactly"))?
    } else {
        let shift = (-binary_exponent) as u32;
        if shift >= u128::BITS {
            0
        } else {
            let whole = numerator >> shift;
            let remainder = numerator & ((1_u128 << shift) - 1);
            let halfway = 1_u128 << (shift - 1);
            whole + u128::from(remainder >= halfway)
        }
    };

    let mut decimal = rounded.to_string();
    let precision = precision as usize;
    if decimal.len() <= precision {
        decimal.insert_str(0, &"0".repeat(precision + 1 - decimal.len()));
    }
    decimal.insert(decimal.len() - precision, '.');
    if number.is_sign_negative() {
        decimal.insert(0, '-');
    }
    decimal
        .parse::<f64>()
        .map_err(|_| invalid_arg("round", "rounded result is not representable"))
}

fn dql_regex_from_object(
    value: &Value,
    function: &str,
) -> Result<Option<(Regex, String)>, EvalError> {
    let Value::Object(values) = value else {
        return Ok(None);
    };
    let Some(pattern) = values.get(DQL_REGEX_OBJECT_KEY) else {
        return Ok(None);
    };
    let pattern = match pattern {
        Value::Null => return Ok(None),
        Value::Text(pattern) => pattern.clone(),
        _ => {
            return Err(invalid_arg(
                function,
                "DQL regex pattern requires text or null",
            ));
        }
    };
    let pattern = normalize_dql_regex_pattern(&pattern, function)?;
    let mode = values
        .get(DQL_REGEX_MODE_KEY)
        .map(|value| expect_text("DQL regex mode", value))
        .transpose()?
        .unwrap_or_else(|| "search".to_string());
    let pattern = if mode == "whole" && !pattern.starts_with('^') && !pattern.ends_with('$') {
        format!(r"\A(?:{pattern})\z")
    } else {
        pattern
    };
    Ok(Some((build_regex(&pattern, "", function)?, mode)))
}

fn normalize_dql_regex_pattern(pattern: &str, function: &str) -> Result<String, EvalError> {
    if !pattern.is_ascii() {
        return Err(unsupported_dql_regex(function));
    }

    let bytes = pattern.as_bytes();
    let mut out = String::with_capacity(pattern.len());
    let mut index = 0usize;
    let mut in_character_class = false;
    let mut first_in_character_class = false;
    while index < bytes.len() {
        let character = bytes[index];
        if character == b'\\' {
            let Some(&escaped) = bytes.get(index + 1) else {
                out.push('\\');
                break;
            };
            match escaped {
                b'w' => {
                    if in_character_class {
                        out.push_str("A-Za-z0-9_");
                    } else {
                        out.push_str("[A-Za-z0-9_]");
                    }
                }
                b'd' => {
                    if in_character_class {
                        out.push_str("0-9");
                    } else {
                        out.push_str("[0-9]");
                    }
                }
                b'W' | b'D' | b's' | b'S' | b'b' | b'B' | b'p' | b'P' | b'u' | b'k' | b'c'
                | b'A' | b'z' | b'Z' | b'G' => {
                    return Err(unsupported_dql_regex(function));
                }
                _ => {
                    out.push('\\');
                    out.push(char::from(escaped));
                }
            }
            if in_character_class {
                first_in_character_class = false;
            }
            index += 2;
            continue;
        }

        if in_character_class {
            if first_in_character_class && character == b'^' {
                return Err(unsupported_dql_regex(function));
            }
            if character == b'['
                || matches!(
                    (character, bytes.get(index + 1)),
                    (b'&', Some(b'&')) | (b'-', Some(b'-')) | (b'~', Some(b'~'))
                )
            {
                return Err(unsupported_dql_regex(function));
            }
            out.push(char::from(character));
            if character == b']' {
                in_character_class = false;
            }
            first_in_character_class = false;
        } else {
            match character {
                b'.' => return Err(unsupported_dql_regex(function)),
                b'[' => {
                    in_character_class = true;
                    first_in_character_class = true;
                    out.push('[');
                }
                b'(' if bytes.get(index + 1) == Some(&b'?')
                    && bytes
                        .get(index + 2)
                        .is_some_and(|next| next.is_ascii_alphabetic() || *next == b'-') =>
                {
                    return Err(unsupported_dql_regex(function));
                }
                _ => out.push(char::from(character)),
            }
        }
        index += 1;
    }
    Ok(out)
}

fn unsupported_dql_regex(function: &str) -> EvalError {
    invalid_arg(
        function,
        "DQL regex uses syntax outside Slate's JavaScript-compatible subset",
    )
}

fn dql_regex_replace_all(regex: &Regex, text: &str, replacement: &str) -> String {
    let mut out = String::new();
    let mut previous_end = 0usize;
    let has_named_captures = regex.capture_names().skip(1).any(|name| name.is_some());
    for captures in regex.captures_iter(text) {
        let Some(whole_match) = captures.get(0) else {
            continue;
        };
        out.push_str(&text[previous_end..whole_match.start()]);
        expand_dql_replacement(&mut out, replacement, &captures, text, has_named_captures);
        previous_end = whole_match.end();
    }
    out.push_str(&text[previous_end..]);
    out
}

fn dql_regex_replace_first(regex: &Regex, text: &str, replacement: &str) -> String {
    let Some(captures) = regex.captures(text) else {
        return text.to_string();
    };
    let Some(whole_match) = captures.get(0) else {
        return text.to_string();
    };
    let mut out = text[..whole_match.start()].to_string();
    let has_named_captures = regex.capture_names().skip(1).any(|name| name.is_some());
    expand_dql_replacement(&mut out, replacement, &captures, text, has_named_captures);
    out.push_str(&text[whole_match.end()..]);
    out
}

fn expand_dql_replacement(
    out: &mut String,
    replacement: &str,
    captures: &regex::Captures<'_>,
    text: &str,
    has_named_captures: bool,
) {
    let whole_match = captures.get(0).expect("capture zero always exists");
    let bytes = replacement.as_bytes();
    let mut pos = 0usize;
    while pos < bytes.len() {
        if bytes[pos] != b'$' || pos + 1 >= bytes.len() {
            let character = replacement[pos..]
                .chars()
                .next()
                .expect("position is at a character boundary");
            out.push(character);
            pos += character.len_utf8();
            continue;
        }
        match bytes[pos + 1] {
            b'$' => {
                out.push('$');
                pos += 2;
            }
            b'&' => {
                out.push_str(whole_match.as_str());
                pos += 2;
            }
            b'`' => {
                out.push_str(&text[..whole_match.start()]);
                pos += 2;
            }
            b'\'' => {
                out.push_str(&text[whole_match.end()..]);
                pos += 2;
            }
            b'<' => {
                let Some(end) = replacement[pos + 2..].find('>') else {
                    out.push('$');
                    pos += 1;
                    continue;
                };
                let name = &replacement[pos + 2..pos + 2 + end];
                if has_named_captures {
                    if let Some(capture) = captures.name(name) {
                        out.push_str(capture.as_str());
                    }
                } else {
                    out.push_str(&replacement[pos..pos + end + 3]);
                }
                pos += end + 3;
            }
            b'0' if bytes
                .get(pos + 2)
                .is_some_and(|digit| digit.is_ascii_digit() && *digit != b'0') =>
            {
                let index = usize::from(bytes[pos + 2] - b'0');
                if index < captures.len() {
                    if let Some(capture) = captures.get(index) {
                        out.push_str(capture.as_str());
                    }
                    pos += 3;
                } else {
                    out.push('$');
                    pos += 1;
                }
            }
            digit if digit.is_ascii_digit() && digit != b'0' => {
                let mut end = pos + 2;
                if bytes.get(end).is_some_and(u8::is_ascii_digit) {
                    end += 1;
                }
                let mut index = replacement[pos + 1..end]
                    .parse::<usize>()
                    .expect("capture index contains only digits");
                if index >= captures.len() && end > pos + 2 {
                    end -= 1;
                    index = replacement[pos + 1..end]
                        .parse::<usize>()
                        .expect("single capture index is a digit");
                }
                if index < captures.len() {
                    if let Some(capture) = captures.get(index) {
                        out.push_str(capture.as_str());
                    }
                    pos = end;
                } else {
                    out.push('$');
                    pos += 1;
                }
            }
            _ => {
                out.push('$');
                pos += 1;
            }
        }
    }
}

fn build_regex(pattern: &str, flags: &str, function: &str) -> Result<Regex, EvalError> {
    let mut builder = RegexBuilder::new(pattern);
    for flag in flags.chars() {
        match flag {
            'g' => {}
            'i' => {
                builder.case_insensitive(true);
            }
            'm' => {
                builder.multi_line(true);
            }
            's' => {
                builder.dot_matches_new_line(true);
            }
            other => {
                return Err(invalid_arg(
                    function,
                    format!("unsupported regex flag {other}"),
                ));
            }
        }
    }
    builder.build().map_err(|err| invalid_arg(function, err))
}

fn contains_value(haystack: &Value, needle: &Value, case_insensitive: bool) -> bool {
    match haystack {
        Value::List(items) => items.iter().any(|item| value_eq(item, needle)),
        Value::Text(text) => {
            let needle = value_to_string(needle);
            if case_insensitive {
                text.to_lowercase().contains(&needle.to_lowercase())
            } else {
                text.contains(&needle)
            }
        }
        Value::Object(map) => map.contains_key(&value_to_string(needle)),
        _ => false,
    }
}

fn dql_contains_value(haystack: &Value, needle: &Value) -> bool {
    match (haystack, needle) {
        (Value::List(items), _) => items.iter().any(|item| dql_contains_value(item, needle)),
        (Value::Text(text), Value::Text(needle)) => text.contains(needle),
        (Value::Object(values), Value::Text(key)) => values.contains_key(key),
        _ => dql_typed_value_eq(haystack, needle),
    }
}

fn flatten(values: Vec<Value>, depth: usize) -> Vec<Value> {
    if depth == 0 {
        return values;
    }
    let mut out = Vec::new();
    for value in values {
        match value {
            Value::List(items) => out.extend(flatten(items, depth - 1)),
            other => out.push(other),
        }
    }
    out
}

fn row_from_value(value: &Value, ctx: &EvalCtx<'_>) -> RowContext {
    match value {
        Value::File(file) => row_for_path(ctx, &file.path),
        Value::Link(link) => row_for_path(
            ctx,
            link.resolved_path
                .as_deref()
                .unwrap_or(link.target.as_str()),
        ),
        _ => ctx.file.clone(),
    }
}

fn row_for_path(ctx: &EvalCtx<'_>, path: &str) -> RowContext {
    if ctx.file.file_path == path {
        return ctx.file.clone();
    }
    if let Some(this) = ctx.this
        && this.file_path == path
    {
        return this.clone();
    }
    ctx.vault.row_for_path(path).unwrap_or_else(|| RowContext {
        file_path: path.to_string(),
        file_fields: FileFields::for_path(path),
        properties: Vec::new(),
        task: None,
    })
}

fn file_path_from_value(value: &Value) -> Result<String, EvalError> {
    match value {
        Value::File(file) => Ok(file.path.clone()),
        Value::Link(link) => Ok(link
            .resolved_path
            .clone()
            .unwrap_or_else(|| link.target.clone())),
        Value::Text(text) => Ok(text.clone()),
        other => Err(invalid_arg(
            "file/link method",
            format!("expected file, link, or text, got {}", other.type_name()),
        )),
    }
}

fn file_field_by_name(name: &str) -> Option<FileField> {
    Some(match name {
        "name" => FileField::Name,
        "basename" => FileField::Basename,
        "path" => FileField::Path,
        "folder" => FileField::Folder,
        "ext" => FileField::Ext,
        "size" => FileField::Size,
        "properties" => FileField::Properties,
        "tags" => FileField::Tags,
        "aliases" => FileField::Aliases,
        "links" => FileField::Links,
        "backlinks" => FileField::Backlinks,
        "embeds" => FileField::Embeds,
        "file" => FileField::File,
        "tasks" => FileField::Tasks,
        "ctime" => FileField::Ctime,
        "mtime" => FileField::Mtime,
        "inDegree" => FileField::InDegree,
        "outDegree" => FileField::OutDegree,
        _ => return None,
    })
}

fn value_eq(lhs: &Value, rhs: &Value) -> bool {
    match (lhs, rhs) {
        (Value::Null, Value::Null) => true,
        (Value::Null, _) | (_, Value::Null) => false,
        (Value::Bool(a), Value::Bool(b)) => a == b,
        (Value::Number(a), Value::Number(b)) => a == b,
        (Value::Text(a), Value::Text(b)) => a == b,
        (Value::Date(a), Value::Date(b)) => a.epoch_ms == b.epoch_ms,
        (Value::DqlDate(a), Value::DqlDate(b)) => dql_date_value_eq(*a, *b),
        (Value::DqlDate(a), Value::Date(b)) | (Value::Date(b), Value::DqlDate(a)) => {
            dql_mixed_date_cmp(*a, *b) == Ordering::Equal
        }
        (Value::Duration(a), Value::Duration(b)) => a == b,
        (Value::DqlDuration(a), Value::DqlDuration(b)) => a == b,
        (Value::DqlDuration(a), Value::Duration(b))
        | (Value::Duration(b), Value::DqlDuration(a)) => *a == DqlDurationValue::fixed(*b),
        (Value::List(a), Value::List(b)) => {
            a.len() == b.len() && a.iter().zip(b).all(|(a, b)| value_eq(a, b))
        }
        (Value::Object(a), Value::Object(b)) => {
            a.len() == b.len()
                && a.iter()
                    .all(|(key, value)| b.get(key).is_some_and(|other| value_eq(value, other)))
        }
        (Value::Link(a), Value::Link(b)) => link_identity(a) == link_identity(b),
        (Value::File(a), Value::File(b)) => a.path == b.path,
        (Value::Regex(a_pattern, a_flags), Value::Regex(b_pattern, b_flags)) => {
            a_pattern == b_pattern && a_flags == b_flags
        }
        (Value::Link(link), Value::File(file)) | (Value::File(file), Value::Link(link)) => {
            link_identity(link) == file.path
        }
        _ => coerce_number(lhs)
            .zip(coerce_number(rhs))
            .is_some_and(|(a, b)| a == b),
    }
}

fn dql_typed_value_eq(lhs: &Value, rhs: &Value) -> bool {
    match (lhs, rhs) {
        (Value::Null, Value::Null) => true,
        (Value::Bool(lhs), Value::Bool(rhs)) => lhs == rhs,
        (Value::Number(lhs), Value::Number(rhs)) => lhs == rhs,
        (Value::Text(lhs), Value::Text(rhs)) => lhs == rhs,
        (Value::Date(lhs), Value::Date(rhs)) => lhs.epoch_ms == rhs.epoch_ms,
        (Value::DqlDate(lhs), Value::DqlDate(rhs)) => dql_date_value_eq(*lhs, *rhs),
        (Value::DqlDate(lhs), Value::Date(rhs)) | (Value::Date(rhs), Value::DqlDate(lhs)) => {
            dql_mixed_date_cmp(*lhs, *rhs) == Ordering::Equal
        }
        (Value::Duration(lhs), Value::Duration(rhs)) => lhs == rhs,
        (Value::DqlDuration(lhs), Value::DqlDuration(rhs)) => lhs == rhs,
        (Value::DqlDuration(lhs), Value::Duration(rhs))
        | (Value::Duration(rhs), Value::DqlDuration(lhs)) => *lhs == DqlDurationValue::fixed(*rhs),
        (Value::List(lhs), Value::List(rhs)) => {
            lhs.len() == rhs.len()
                && lhs
                    .iter()
                    .zip(rhs)
                    .all(|(lhs, rhs)| dql_typed_value_eq(lhs, rhs))
        }
        (Value::Object(lhs), Value::Object(rhs)) => {
            lhs.len() == rhs.len()
                && lhs.iter().all(|(key, value)| {
                    rhs.get(key)
                        .is_some_and(|other| dql_typed_value_eq(value, other))
                })
        }
        (Value::Link(lhs), Value::Link(rhs)) => {
            link_identity(lhs) == link_identity(rhs)
                && lhs.link_type == rhs.link_type
                && lhs.subpath == rhs.subpath
        }
        (Value::File(lhs), Value::File(rhs)) => lhs.path == rhs.path,
        (Value::Regex(lhs_pattern, lhs_flags), Value::Regex(rhs_pattern, rhs_flags)) => {
            lhs_pattern == rhs_pattern && lhs_flags == rhs_flags
        }
        _ => false,
    }
}

fn dql_date_value_eq(lhs: DqlDateValue, rhs: DqlDateValue) -> bool {
    lhs.epoch_ms == rhs.epoch_ms
        && lhs.is_local == rhs.is_local
        && (lhs.is_local || lhs.offset_minutes == rhs.offset_minutes)
}

fn dql_number_cmp(lhs: f64, rhs: f64) -> Ordering {
    if lhs < rhs {
        Ordering::Less
    } else if lhs == rhs {
        Ordering::Equal
    } else {
        Ordering::Greater
    }
}

fn dql_ordering_cmp(lhs: &Value, rhs: &Value, function: &str) -> Result<Ordering, EvalError> {
    match (lhs, rhs) {
        (Value::Null, Value::Null) => return Ok(Ordering::Equal),
        (Value::Null, _) => return Ok(Ordering::Less),
        (_, Value::Null) => return Ok(Ordering::Greater),
        _ => {}
    }
    let type_order = dql_type_name(lhs).cmp(dql_type_name(rhs));
    if type_order != Ordering::Equal {
        return Ok(type_order);
    }
    match (lhs, rhs) {
        (Value::Bool(lhs), Value::Bool(rhs)) => Ok(lhs.cmp(rhs)),
        (Value::Number(lhs), Value::Number(rhs)) => Ok(dql_number_cmp(*lhs, *rhs)),
        (Value::Text(lhs), Value::Text(rhs)) => dql_locale_cmp(lhs, rhs, function),
        (Value::Date(lhs), Value::Date(rhs)) => Ok(lhs.epoch_ms.cmp(&rhs.epoch_ms)),
        (Value::DqlDate(lhs), Value::DqlDate(rhs)) => Ok(lhs.epoch_ms.cmp(&rhs.epoch_ms)),
        (Value::DqlDate(lhs), Value::Date(rhs)) => Ok(dql_mixed_date_cmp(*lhs, *rhs)),
        (Value::Date(lhs), Value::DqlDate(rhs)) => Ok(dql_mixed_date_cmp(*rhs, *lhs).reverse()),
        (Value::Duration(lhs), Value::Duration(rhs)) => Ok(lhs.cmp(rhs)),
        (Value::DqlDuration(lhs), Value::DqlDuration(rhs)) => {
            let ordering = dql_number_cmp(lhs.casual_milliseconds(), rhs.casual_milliseconds());
            Ok(if ordering == Ordering::Equal && lhs != rhs {
                Ordering::Greater
            } else {
                ordering
            })
        }
        (Value::DqlDuration(lhs), Value::Duration(rhs)) => {
            Ok(dql_number_cmp(lhs.casual_milliseconds(), *rhs as f64))
        }
        (Value::Duration(lhs), Value::DqlDuration(rhs)) => {
            Ok(dql_number_cmp(*lhs as f64, rhs.casual_milliseconds()))
        }
        (Value::List(lhs), Value::List(rhs)) => {
            for (lhs, rhs) in lhs.iter().zip(rhs) {
                let ordering = dql_ordering_cmp(lhs, rhs, function)?;
                if ordering != Ordering::Equal {
                    return Ok(ordering);
                }
            }
            Ok(lhs.len().cmp(&rhs.len()))
        }
        (Value::Object(lhs), Value::Object(rhs)) => {
            let lhs_keys = lhs.keys().collect::<Vec<_>>();
            let rhs_keys = rhs.keys().collect::<Vec<_>>();
            for (lhs_key, rhs_key) in lhs_keys.iter().zip(&rhs_keys) {
                let ordering = dql_locale_cmp(lhs_key, rhs_key, function)?;
                if ordering != Ordering::Equal {
                    return Ok(ordering);
                }
            }
            let key_count = lhs_keys.len().cmp(&rhs_keys.len());
            if key_count != Ordering::Equal {
                return Ok(key_count);
            }
            for key in lhs_keys {
                let ordering = dql_ordering_cmp(
                    lhs.get(key).expect("key collected from lhs object"),
                    rhs.get(key).expect("equal object keys exist in rhs object"),
                    function,
                )?;
                if ordering != Ordering::Equal {
                    return Ok(ordering);
                }
            }
            Ok(Ordering::Equal)
        }
        (Value::Link(lhs), Value::Link(rhs)) => {
            let path = dql_locale_cmp(link_identity(lhs), link_identity(rhs), function)?;
            if path != Ordering::Equal {
                return Ok(path);
            }
            let link_type = dql_locale_cmp(&lhs.link_type, &rhs.link_type, function)?;
            if link_type != Ordering::Equal {
                return Ok(link_type);
            }
            dql_optional_locale_cmp(lhs.subpath.as_deref(), rhs.subpath.as_deref(), function)
        }
        (Value::File(lhs), Value::File(rhs)) => dql_locale_cmp(&lhs.path, &rhs.path, function),
        (Value::Link(lhs), Value::File(rhs)) => {
            dql_locale_cmp(link_identity(lhs), &rhs.path, function)
        }
        (Value::File(lhs), Value::Link(rhs)) => {
            dql_locale_cmp(&lhs.path, link_identity(rhs), function)
        }
        (Value::Regex(lhs_pattern, lhs_flags), Value::Regex(rhs_pattern, rhs_flags)) => {
            let pattern = dql_locale_cmp(lhs_pattern, rhs_pattern, function)?;
            if pattern == Ordering::Equal {
                dql_locale_cmp(lhs_flags, rhs_flags, function)
            } else {
                Ok(pattern)
            }
        }
        _ => Ok(Ordering::Equal),
    }
}

pub(crate) fn dql_command_sort_value(value: &Value) -> Option<&Value> {
    dql_object_value(value, DQL_COMMAND_SORT_OBJECT_KEY)
}

pub(crate) fn compare_dql_command_sort_values(
    lhs: &Value,
    rhs: &Value,
) -> Result<Ordering, EvalError> {
    dql_ordering_cmp(lhs, rhs, "DQL SORT")
}

fn dql_locale_cmp(lhs: &str, rhs: &str, function: &str) -> Result<Ordering, EvalError> {
    let safe = |value: &str| {
        value
            .bytes()
            .all(|byte| byte.is_ascii_lowercase() || byte.is_ascii_digit())
    };
    if !safe(lhs) || !safe(rhs) {
        return Err(invalid_arg(
            function,
            "DQL locale collation is unsupported outside lowercase ASCII letters and digits",
        ));
    }
    Ok(lhs.cmp(rhs))
}

fn dql_optional_locale_cmp(
    lhs: Option<&str>,
    rhs: Option<&str>,
    function: &str,
) -> Result<Ordering, EvalError> {
    match (lhs, rhs) {
        (None, None) => Ok(Ordering::Equal),
        (None, Some(_)) => Ok(Ordering::Less),
        (Some(_), None) => Ok(Ordering::Greater),
        (Some(lhs), Some(rhs)) => dql_locale_cmp(lhs, rhs, function),
    }
}

fn dql_type_name(value: &Value) -> &'static str {
    match value {
        Value::List(_) => "array",
        Value::Bool(_) => "boolean",
        Value::Date(_) | Value::DqlDate(_) => "date",
        Value::Duration(_) | Value::DqlDuration(_) => "duration",
        Value::File(_) | Value::Link(_) => "link",
        Value::Null => "null",
        Value::Number(_) => "number",
        Value::Object(_) => "object",
        Value::Regex(_, _) => "regex",
        Value::Text(_) => "string",
    }
}

fn compare_values(lhs: &Value, rhs: &Value, ctx: &EvalCtx<'_>) -> Option<Ordering> {
    let ordering = match (lhs, rhs) {
        (Value::Null, _) | (_, Value::Null) => None,
        (Value::Date(a), Value::Date(b)) => Some(a.epoch_ms.cmp(&b.epoch_ms)),
        (Value::DqlDate(a), Value::DqlDate(b)) => Some(a.epoch_ms.cmp(&b.epoch_ms)),
        (Value::DqlDate(a), Value::Date(b)) => Some(dql_mixed_date_cmp(*a, *b)),
        (Value::Date(a), Value::DqlDate(b)) => Some(dql_mixed_date_cmp(*b, *a).reverse()),
        (Value::Text(a), Value::Text(b)) => numeric_order(lhs, rhs).or_else(|| Some(a.cmp(b))),
        (Value::Bool(a), Value::Bool(b)) => Some(a.cmp(b)),
        _ => numeric_order(lhs, rhs),
    };
    if let Some(ordering) = ordering {
        Some(ordering)
    } else {
        ctx.warnings
            .warn("ordering comparison on non-comparable values evaluated to false");
        None
    }
}

fn numeric_order(lhs: &Value, rhs: &Value) -> Option<Ordering> {
    coerce_number(lhs)
        .zip(coerce_number(rhs))
        .and_then(|(a, b)| a.partial_cmp(&b))
}

fn typed_value_cmp(lhs: &Value, rhs: &Value, function: &str) -> Result<Ordering, EvalError> {
    let ordering = match (lhs, rhs) {
        (Value::Null, Value::Null) => Some(Ordering::Equal),
        (Value::Bool(lhs), Value::Bool(rhs)) => Some(lhs.cmp(rhs)),
        (Value::Number(lhs), Value::Number(rhs)) => lhs.partial_cmp(rhs),
        (Value::Text(lhs), Value::Text(rhs)) => Some(lhs.cmp(rhs)),
        (Value::Date(lhs), Value::Date(rhs)) => Some(lhs.epoch_ms.cmp(&rhs.epoch_ms)),
        (Value::DqlDate(lhs), Value::DqlDate(rhs)) => Some(lhs.epoch_ms.cmp(&rhs.epoch_ms)),
        (Value::DqlDate(lhs), Value::Date(rhs)) => Some(dql_mixed_date_cmp(*lhs, *rhs)),
        (Value::Date(lhs), Value::DqlDate(rhs)) => Some(dql_mixed_date_cmp(*rhs, *lhs).reverse()),
        (Value::Duration(lhs), Value::Duration(rhs)) => Some(lhs.cmp(rhs)),
        (Value::DqlDuration(lhs), Value::DqlDuration(rhs)) => Some(
            lhs.casual_milliseconds()
                .total_cmp(&rhs.casual_milliseconds()),
        ),
        (Value::DqlDuration(lhs), Value::Duration(rhs)) => {
            Some(lhs.casual_milliseconds().total_cmp(&(*rhs as f64)))
        }
        (Value::Duration(lhs), Value::DqlDuration(rhs)) => {
            Some((*lhs as f64).total_cmp(&rhs.casual_milliseconds()))
        }
        _ => None,
    };
    ordering.ok_or_else(|| {
        invalid_arg(
            function,
            format!(
                "cannot compare {} and {} values",
                lhs.type_name(),
                rhs.type_name()
            ),
        )
    })
}

fn sort_values(values: &mut [Value], function: &str) -> Result<(), EvalError> {
    for index in 1..values.len() {
        let mut cursor = index;
        while cursor > 0
            && typed_value_cmp(&values[cursor], &values[cursor - 1], function)? == Ordering::Less
        {
            values.swap(cursor, cursor - 1);
            cursor -= 1;
        }
    }
    Ok(())
}

fn dql_sort_values(values: &mut [Value], function: &str) -> Result<(), EvalError> {
    for index in 1..values.len() {
        let mut cursor = index;
        while cursor > 0
            && dql_ordering_cmp(&values[cursor], &values[cursor - 1], function)? == Ordering::Less
        {
            values.swap(cursor, cursor - 1);
            cursor -= 1;
        }
    }
    Ok(())
}

pub(super) fn link_identity(link: &LinkValue) -> &str {
    link.resolved_path
        .as_deref()
        .unwrap_or(link.target.as_str())
}

fn coerce_number(value: &Value) -> Option<f64> {
    match value {
        Value::Number(value) => Some(*value),
        Value::Bool(value) => Some(if *value { 1.0 } else { 0.0 }),
        Value::Text(value) => value.trim().parse::<f64>().ok(),
        Value::Date(value) => Some(value.epoch_ms as f64),
        Value::Duration(value) => Some(*value as f64),
        _ => None,
    }
}

fn duration_arg(value: &Value) -> Option<DurationParts> {
    match value {
        Value::Duration(value) => Some(DurationParts::fixed(*value)),
        Value::Text(text) => parse_duration_parts(text),
        _ => None,
    }
}

fn dql_duration_arg(value: &Value) -> Option<DqlDurationValue> {
    match value {
        Value::Duration(value) => Some(DqlDurationValue::fixed(*value)),
        Value::DqlDuration(value) => Some(*value),
        _ => None,
    }
}

fn dql_value_to_string(value: &Value) -> Result<String, EvalError> {
    dql_value_to_string_inner(value, true)
}

fn dql_nested_value_to_string(value: &Value) -> Result<String, EvalError> {
    dql_value_to_string_inner(value, false)
}

fn dql_value_to_string_inner(value: &Value, top_level: bool) -> Result<String, EvalError> {
    Ok(match value {
        Value::Null => "\\-".to_string(),
        Value::Bool(value) => value.to_string(),
        Value::Number(value) => format_number(*value),
        Value::Text(value) => value.clone(),
        Value::List(values) => {
            let contents = values
                .iter()
                .map(dql_nested_value_to_string)
                .collect::<Result<Vec<_>, _>>()?
                .join(", ");
            if top_level {
                contents
            } else {
                format!("[{contents}]")
            }
        }
        Value::Object(values) => {
            if values.len() > 1 {
                return Err(invalid_arg(
                    "string",
                    "DQL string formatting of multi-key objects is unsupported because authored JavaScript key order is unavailable",
                ));
            }
            format!(
                "{{ {} }}",
                values
                    .iter()
                    .map(|(key, value)| {
                        Ok(format!("{key}: {}", dql_nested_value_to_string(value)?))
                    })
                    .collect::<Result<Vec<_>, EvalError>>()?
                    .join(", ")
            )
        }
        Value::Date(value) => dql_format_date(*value)?,
        Value::DqlDate(value) => dql_format_dql_date(*value)?,
        Value::Duration(value) => dql_format_duration(*value),
        Value::DqlDuration(value) => dql_format_duration_parts(*value),
        Value::Link(link) => dql_format_link(link),
        Value::File(_) | Value::Regex(_, _) => {
            return Err(invalid_arg(
                "string",
                "DQL string formatting for this value type is unsupported",
            ));
        }
    })
}

fn dql_format_date(value: DateValue) -> Result<String, EvalError> {
    let date = date_time(value)?;
    if date.hour() == 0 && date.minute() == 0 && date.second() == 0 {
        Ok(date.format("%B %d, %Y").to_string())
    } else {
        let hour = match date.hour() % 12 {
            0 => 12,
            hour => hour,
        };
        let period = if date.hour() < 12 { "AM" } else { "PM" };
        Ok(format!(
            "{hour}:{:02} {period} - {}",
            date.minute(),
            date.format("%B %d, %Y")
        ))
    }
}

fn dql_format_dql_date(value: DqlDateValue) -> Result<String, EvalError> {
    let date = dql_date_time(value)?;
    if date.hour() == 0 && date.minute() == 0 && date.second() == 0 {
        Ok(date.format("%B %d, %Y").to_string())
    } else {
        let hour = match date.hour() % 12 {
            0 => 12,
            hour => hour,
        };
        let period = if date.hour() < 12 { "AM" } else { "PM" };
        Ok(format!(
            "{hour}:{:02} {period} - {}",
            date.minute(),
            date.format("%B %d, %Y")
        ))
    }
}

fn dql_format_duration(value: i64) -> String {
    if value == 0 {
        return "0 milliseconds".to_string();
    }
    let negative = value.is_negative();
    let mut remaining = value.unsigned_abs();
    let units = [
        (86_400_000_u64, "day"),
        (3_600_000, "hour"),
        (60_000, "minute"),
        (1_000, "second"),
        (1, "millisecond"),
    ];
    let mut parts = Vec::new();
    for (milliseconds, name) in units {
        let count = remaining / milliseconds;
        remaining %= milliseconds;
        if count != 0 {
            let count = if negative && parts.is_empty() {
                format!("-{count}")
            } else {
                count.to_string()
            };
            parts.push(format!(
                "{count} {name}{}",
                if count == "1" || count == "-1" {
                    ""
                } else {
                    "s"
                }
            ));
        }
    }
    parts.join(", ")
}

pub(crate) fn dql_format_duration_parts(value: DqlDurationValue) -> String {
    let parts = [
        (value.years, "year"),
        (value.months, "month"),
        (value.weeks, "week"),
        (value.days, "day"),
        (value.hours, "hour"),
        (value.minutes, "minute"),
        (value.seconds, "second"),
        (value.milliseconds, "millisecond"),
    ]
    .into_iter()
    .filter(|(amount, _)| *amount != 0.0)
    .map(|(amount, unit)| {
        format!(
            "{} {unit}{}",
            format_number(amount),
            if amount.abs() == 1.0 { "" } else { "s" }
        )
    })
    .collect::<Vec<_>>();
    if parts.is_empty() {
        "0 milliseconds".to_string()
    } else {
        parts.join(", ")
    }
}

fn dql_format_link(link: &LinkValue) -> String {
    let display = link.display.clone().unwrap_or_else(|| {
        let file = link.target.rsplit('/').next().unwrap_or(&link.target);
        let mut display = file.strip_suffix(".md").unwrap_or(file).to_string();
        if let Some(subpath) = &link.subpath {
            display.push_str(" > ");
            display.push_str(subpath);
        }
        display
    });
    let subpath = match (&*link.link_type, &link.subpath) {
        ("header", Some(subpath)) => format!("#{}", subpath.replace('|', "\\|")),
        ("block", Some(subpath)) => format!("#^{}", subpath.replace('|', "\\|")),
        _ => String::new(),
    };
    let target = link.target.replace('|', "\\|");
    format!(
        "{}[[{}{subpath}|{display}]]",
        if link.embed { "!" } else { "" },
        target
    )
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

fn value_to_string(value: &Value) -> String {
    match value {
        Value::Null => String::new(),
        Value::Bool(value) => value.to_string(),
        Value::Number(value) => format_number(*value),
        Value::Text(value) => value.clone(),
        Value::Date(value) => format_date_default(*value),
        Value::DqlDate(value) => dql_date_display(*value),
        Value::Duration(value) => value.to_string(),
        Value::DqlDuration(value) => dql_format_duration_parts(*value),
        Value::List(values) => values
            .iter()
            .map(value_to_string)
            .collect::<Vec<_>>()
            .join(","),
        Value::Object(_) => "[object Object]".to_string(),
        Value::Link(link) => match &link.display {
            Some(display) => format!("[[{}|{}]]", link.target, display),
            None => format!("[[{}]]", link.target),
        },
        Value::File(file) => file.path.clone(),
        Value::Regex(pattern, flags) => format!("/{pattern}/{flags}"),
    }
}

fn format_number(value: f64) -> String {
    if value.is_nan() {
        "NaN".to_string()
    } else if value == f64::INFINITY {
        "Infinity".to_string()
    } else if value == f64::NEG_INFINITY {
        "-Infinity".to_string()
    } else if value == 0.0 {
        "0".to_string()
    } else if value.fract() == 0.0 {
        format!("{}", value as i64)
    } else {
        value.to_string()
    }
}

fn expect_text_like(function: &str, value: &Value) -> Result<String, EvalError> {
    match value {
        Value::Text(text) => Ok(text.clone()),
        Value::Number(_)
        | Value::Bool(_)
        | Value::Date(_)
        | Value::DqlDate(_)
        | Value::Duration(_)
        | Value::DqlDuration(_)
        | Value::Link(_) => Ok(value_to_string(value)),
        _ => Err(invalid_arg(function, "expected text-like value")),
    }
}

fn expect_text(function: &str, value: &Value) -> Result<String, EvalError> {
    match value {
        Value::Text(text) => Ok(text.clone()),
        _ => Err(invalid_arg(function, "expected text value")),
    }
}

fn expect_arity(
    function: impl Into<String>,
    got: usize,
    min: usize,
    max: usize,
) -> Result<(), EvalError> {
    if got >= min && got <= max {
        return Ok(());
    }
    let expected = if min == max {
        min.to_string()
    } else if max == usize::MAX {
        format!("at least {min}")
    } else {
        format!("{min}..={max}")
    };
    Err(EvalError::InvalidArity {
        function: function.into(),
        expected,
        got,
    })
}

fn invalid_arg(function: impl Into<String>, message: impl ToString) -> EvalError {
    EvalError::InvalidArgument {
        function: function.into(),
        message: message.to_string(),
    }
}

impl Value {
    pub fn is_truthy(&self) -> bool {
        match self {
            Value::Null => false,
            Value::Bool(value) => *value,
            Value::Number(value) => *value != 0.0 && !value.is_nan(),
            Value::Text(value) => !value.is_empty(),
            Value::List(value) => !value.is_empty(),
            Value::Object(value) => !value.is_empty(),
            Value::Date(_)
            | Value::DqlDate(_)
            | Value::Duration(_)
            | Value::DqlDuration(_)
            | Value::Link(_)
            | Value::File(_)
            | Value::Regex(_, _) => true,
        }
    }

    fn is_empty_value(&self) -> bool {
        match self {
            Value::Null => true,
            Value::Text(value) => value.is_empty(),
            Value::List(value) => value.is_empty(),
            Value::Object(value) => value.is_empty(),
            _ => false,
        }
    }

    fn type_name(&self) -> &'static str {
        match self {
            Value::Null => "null",
            Value::Bool(_) => "bool",
            Value::Number(_) => "number",
            Value::Text(_) => "string",
            Value::Date(_) => "date",
            Value::DqlDate(_) => "date",
            Value::Duration(_) => "duration",
            Value::DqlDuration(_) => "duration",
            Value::List(_) => "list",
            Value::Object(_) => "object",
            Value::Link(_) => "link",
            Value::File(_) => "file",
            Value::Regex(_, _) => "regex",
        }
    }

    fn is_type(&self, name: &str) -> bool {
        matches!(
            (self.type_name(), name),
            (got, expected) if got.eq_ignore_ascii_case(expected)
        ) || matches!(
            (self, name),
            (Value::Bool(_), "boolean")
                | (Value::Text(_), "text")
                | (Value::List(_), "array")
                | (Value::Regex(_, _), "regexp")
        )
    }
}

fn parse_date_text(text: &str, now_ms: i64) -> Option<DateValue> {
    match text {
        "now" => Some(DateValue {
            epoch_ms: now_ms,
            has_time: true,
        }),
        "today" => Some(today_value(now_ms)),
        "tomorrow" => Some(add_duration(
            today_value(now_ms),
            DurationParts::fixed(86_400_000),
        )),
        "yesterday" => Some(add_duration(
            today_value(now_ms),
            DurationParts::fixed(-86_400_000),
        )),
        _ => parse_iso_date(text),
    }
}

fn dql_date_at_epoch(epoch_ms: i64, has_time: bool) -> Result<DqlDateValue, EvalError> {
    let utc = DateTime::<Utc>::from_timestamp_millis(epoch_ms)
        .ok_or_else(|| invalid_arg("date", "timestamp is outside chrono range"))?;
    let local = utc.with_timezone(&Local);
    Ok(DqlDateValue {
        epoch_ms,
        has_time,
        offset_minutes: local.offset().fix().local_minus_utc() / 60,
        is_local: true,
    })
}

fn dql_date_from_native(date: DateValue) -> Result<DqlDateValue, EvalError> {
    if date.has_time {
        return dql_date_at_epoch(date.epoch_ms, true);
    }
    let authored_date = date_time(date)?.date_naive();
    let local_midnight = authored_date
        .and_hms_opt(0, 0, 0)
        .ok_or_else(|| invalid_arg("date", "native date-only value is outside chrono range"))?;
    dql_date_from_local(local_midnight, false)
}

fn dql_date_from_native_with_provenance(
    date: DateValue,
    provenance: DqlDateValue,
) -> Result<DqlDateValue, EvalError> {
    if date.has_time {
        return Ok(DqlDateValue {
            epoch_ms: date.epoch_ms,
            has_time: true,
            ..provenance
        });
    }
    let authored_date = date_time(date)?.date_naive();
    let local_midnight = authored_date
        .and_hms_opt(0, 0, 0)
        .ok_or_else(|| invalid_arg("date", "native date-only value is outside chrono range"))?;
    resolve_dql_local_datetime(
        DqlDateValue {
            has_time: false,
            ..provenance
        },
        local_midnight,
    )
}

fn dql_mixed_date_cmp(dql: DqlDateValue, native: DateValue) -> Ordering {
    if native.has_time {
        return dql.epoch_ms.cmp(&native.epoch_ms);
    }
    dql_date_from_native_with_provenance(native, dql)
        .map(|rebuilt| dql.epoch_ms.cmp(&rebuilt.epoch_ms))
        .unwrap_or_else(|_| dql.epoch_ms.cmp(&native.epoch_ms))
}

fn dql_date_from_local(naive: NaiveDateTime, has_time: bool) -> Result<DqlDateValue, EvalError> {
    let local = match Local.from_local_datetime(&naive) {
        LocalResult::Single(value) => value,
        LocalResult::Ambiguous(_, _) => {
            return Err(invalid_arg(
                "date",
                "DQL local datetime is ambiguous at a daylight-saving transition",
            ));
        }
        LocalResult::None => {
            return Err(invalid_arg(
                "date",
                "DQL local datetime does not exist at a daylight-saving transition",
            ));
        }
    };
    Ok(DqlDateValue {
        epoch_ms: local.timestamp_millis(),
        has_time,
        offset_minutes: local.offset().fix().local_minus_utc() / 60,
        is_local: true,
    })
}

fn dql_date_from_fixed(date: DateTime<FixedOffset>, has_time: bool) -> DqlDateValue {
    DqlDateValue {
        epoch_ms: date.timestamp_millis(),
        has_time,
        offset_minutes: date.offset().local_minus_utc() / 60,
        is_local: false,
    }
}

fn parse_dql_date_text(text: &str, now_ms: i64) -> Result<Option<DqlDateValue>, EvalError> {
    if matches!(
        text,
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
    ) {
        let now = DateTime::<Utc>::from_timestamp_millis(now_ms)
            .ok_or_else(|| invalid_arg("date", "timestamp is outside chrono range"))?
            .with_timezone(&Local);
        if text == "now" {
            return Ok(Some(DqlDateValue {
                epoch_ms: now_ms,
                has_time: true,
                offset_minutes: now.offset().fix().local_minus_utc() / 60,
                is_local: true,
            }));
        }
        let today = now.date_naive();
        let date = match text {
            "today" => today,
            "tomorrow" => today.succ_opt().expect("local today is not chrono max"),
            "yesterday" => today.pred_opt().expect("local today is not chrono min"),
            "sow" | "start-of-week" => today
                .checked_sub_days(chrono::Days::new(u64::from(
                    today.weekday().num_days_from_monday(),
                )))
                .expect("week start is in chrono range"),
            "eow" | "end-of-week" => today
                .checked_add_days(chrono::Days::new(u64::from(
                    6 - today.weekday().num_days_from_monday(),
                )))
                .expect("week end is in chrono range"),
            "som" | "start-of-month" => {
                NaiveDate::from_ymd_opt(today.year(), today.month(), 1).expect("valid month start")
            }
            "eom" | "end-of-month" => NaiveDate::from_ymd_opt(
                today.year(),
                today.month(),
                days_in_month(today.year(), today.month()).expect("valid month"),
            )
            .expect("valid month end"),
            "soy" | "start-of-year" => {
                NaiveDate::from_ymd_opt(today.year(), 1, 1).expect("valid year start")
            }
            "eoy" | "end-of-year" => {
                NaiveDate::from_ymd_opt(today.year(), 12, 31).expect("valid year end")
            }
            _ => unreachable!(),
        };
        let end_of_period = matches!(
            text,
            "eow" | "eom" | "eoy" | "end-of-week" | "end-of-month" | "end-of-year"
        );
        let local = if end_of_period {
            date.and_hms_milli_opt(23, 59, 59, 999)
                .expect("end of day is valid")
        } else {
            date.and_hms_milli_opt(0, 0, 0, 0)
                .expect("midnight is valid")
        };
        return dql_date_from_local(local, end_of_period).map(Some);
    }

    if text.contains(' ') {
        return Ok(None);
    }
    if !dql_fractional_seconds_have_exact_milliseconds(text) {
        return Ok(None);
    }
    if let Ok(date) = DateTime::parse_from_rfc3339(text) {
        return Ok(Some(dql_date_from_fixed(date, true)));
    }
    if let Some(value) = parse_dql_local_iso(text)? {
        return Ok(Some(value));
    }
    if looks_like_dql_date_shape(text) {
        return Err(invalid_arg(
            "date",
            "DQL date shape is valid upstream but unsupported by Slate",
        ));
    }
    Ok(None)
}

fn parse_dql_local_iso(text: &str) -> Result<Option<DqlDateValue>, EvalError> {
    if let Ok(month) = NaiveDate::parse_from_str(&format!("{text}-01"), "%Y-%m-%d") {
        return dql_date_from_local(
            month
                .and_hms_milli_opt(0, 0, 0, 0)
                .expect("midnight is valid"),
            false,
        )
        .map(Some);
    }
    if let Ok(date) = NaiveDate::parse_from_str(text, "%Y-%m-%d") {
        return dql_date_from_local(
            date.and_hms_milli_opt(0, 0, 0, 0)
                .expect("midnight is valid"),
            false,
        )
        .map(Some);
    }
    if text.len() == 13 {
        let expanded = format!("{text}:00");
        if let Ok(date) = NaiveDateTime::parse_from_str(&expanded, "%Y-%m-%dT%H:%M") {
            return dql_date_from_local(date, true).map(Some);
        }
    }
    for format in [
        "%Y-%m-%dT%H",
        "%Y-%m-%dT%H:%M",
        "%Y-%m-%dT%H:%M:%S",
        "%Y-%m-%dT%H:%M:%S%.f",
    ] {
        if let Ok(date) = NaiveDateTime::parse_from_str(text, format) {
            return dql_date_from_local(date, true).map(Some);
        }
    }
    Ok(None)
}

fn dql_fractional_seconds_have_exact_milliseconds(text: &str) -> bool {
    let Some(time_start) = text.find('T') else {
        return true;
    };
    let time = &text[time_start + 1..];
    let Some(dot) = time.find('.') else {
        return true;
    };
    time[dot + 1..]
        .bytes()
        .take_while(u8::is_ascii_digit)
        .count()
        == 3
}

fn looks_like_dql_date_shape(text: &str) -> bool {
    let bytes = text.as_bytes();
    bytes.len() >= 7
        && bytes
            .get(0..4)
            .is_some_and(|part| part.iter().all(u8::is_ascii_digit))
        && bytes.get(4) == Some(&b'-')
        && bytes
            .get(5..7)
            .is_some_and(|part| part.iter().all(u8::is_ascii_digit))
}

fn parse_dql_link_date(link: &LinkValue) -> Result<Option<DqlDateValue>, EvalError> {
    for candidate in [link.display.as_deref(), Some(link.target.as_str())]
        .into_iter()
        .flatten()
    {
        if let Some(date) = parse_dql_local_iso(candidate)? {
            return Ok(Some(date));
        }
    }
    Ok(None)
}

fn dql_link_page_day(
    link: &LinkValue,
    ctx: &EvalCtx<'_>,
) -> Result<Option<DqlDateValue>, EvalError> {
    let Some(path) = ctx
        .vault
        .resolve_link(&link.target)
        .or_else(|| link.resolved_path.clone())
    else {
        return Ok(None);
    };
    let Some(row) = ctx.vault.row_for_path(&path) else {
        return Ok(None);
    };
    let (inline, incomplete) = ctx.vault.dql_inline_fields_for(&path, ctx.now_ms)?;
    if incomplete {
        return Err(invalid_arg(
            "date",
            format!("DQL inline-field index is incomplete for {path}"),
        ));
    }

    let mut grouped = Vec::<(String, Vec<Value>)>::new();
    for (key, value) in row.properties.iter().chain(&inline) {
        if let Some((_, values)) = grouped.iter_mut().find(|(got, _)| got == key) {
            values.push(value.clone());
        } else {
            grouped.push((key.clone(), vec![value.clone()]));
        }
    }
    for (key, values) in grouped {
        if (key.eq_ignore_ascii_case("date") || key.eq_ignore_ascii_case("day"))
            && let Some(date) = dql_page_day_value(&dql_merge_property_values(values))?
        {
            return Ok(Some(date));
        }
    }

    if !row.file_fields.ext.eq_ignore_ascii_case("md") {
        return Ok(None);
    }
    let title = path.rsplit('/').next().unwrap_or(&path);
    let title = title
        .rsplit_once('.')
        .filter(|(_, extension)| extension.eq_ignore_ascii_case("md"))
        .map(|(title, _)| title)
        .unwrap_or(title);
    extract_dql_page_date(title)
}

fn dql_page_day_value(value: &Value) -> Result<Option<DqlDateValue>, EvalError> {
    match value {
        Value::Date(date) => dql_date_from_native(*date).map(Some),
        Value::DqlDate(date) => Ok(Some(*date)),
        Value::Link(link) => dql_page_day_link_date(link),
        Value::List(values) => match values.first() {
            Some(Value::Date(date)) => dql_date_from_native(*date).map(Some),
            Some(Value::DqlDate(date)) => Ok(Some(*date)),
            _ => Ok(None),
        },
        _ => Ok(None),
    }
}

fn dql_page_day_link_date(link: &LinkValue) -> Result<Option<DqlDateValue>, EvalError> {
    for candidate in [
        Some(link.target.as_str()),
        link.subpath.as_deref(),
        link.display.as_deref(),
    ]
    .into_iter()
    .flatten()
    {
        if let Some(date) = extract_dql_page_date(candidate)? {
            return Ok(Some(date));
        }
    }
    Ok(None)
}

fn extract_dql_page_date(text: &str) -> Result<Option<DqlDateValue>, EvalError> {
    let bytes = text.as_bytes();
    for start in 0..bytes.len() {
        if bytes.get(start..start + 10).is_some_and(|candidate| {
            candidate[0..4].iter().all(u8::is_ascii_digit)
                && candidate[4] == b'-'
                && candidate[5..7].iter().all(u8::is_ascii_digit)
                && candidate[7] == b'-'
                && candidate[8..10].iter().all(u8::is_ascii_digit)
        }) && let Ok(date) = NaiveDate::parse_from_str(&text[start..start + 10], "%Y-%m-%d")
        {
            return dql_date_from_local(
                date.and_hms_milli_opt(0, 0, 0, 0)
                    .expect("midnight is valid"),
                false,
            )
            .map(Some);
        }
        if bytes
            .get(start..start + 8)
            .is_some_and(|candidate| candidate.iter().all(u8::is_ascii_digit))
            && let Ok(date) = NaiveDate::parse_from_str(&text[start..start + 8], "%Y%m%d")
        {
            return dql_date_from_local(
                date.and_hms_milli_opt(0, 0, 0, 0)
                    .expect("midnight is valid"),
                false,
            )
            .map(Some);
        }
    }
    Ok(None)
}

fn parse_iso_date(text: &str) -> Option<DateValue> {
    if let Ok(date) = NaiveDate::parse_from_str(text, "%Y-%m-%d") {
        let dt = Utc.from_utc_datetime(&date.and_hms_milli_opt(0, 0, 0, 0)?);
        return Some(DateValue {
            epoch_ms: dt.timestamp_millis(),
            has_time: false,
        });
    }
    if let Ok(dt) = DateTime::parse_from_rfc3339(text) {
        return Some(DateValue {
            epoch_ms: dt.timestamp_millis(),
            has_time: true,
        });
    }
    if let Ok(dt) = NaiveDateTime::parse_from_str(text, "%Y-%m-%dT%H:%M:%S") {
        return Some(DateValue {
            epoch_ms: Utc.from_utc_datetime(&dt).timestamp_millis(),
            has_time: true,
        });
    }
    if let Ok(dt) = NaiveDateTime::parse_from_str(text, "%Y-%m-%d %H:%M:%S") {
        return Some(DateValue {
            epoch_ms: Utc.from_utc_datetime(&dt).timestamp_millis(),
            has_time: true,
        });
    }
    None
}

fn parse_duration_ms(text: &str) -> Option<i64> {
    parse_duration_parts(text).map(duration_parts_to_ms)
}

fn duration_parts_to_ms(parts: DurationParts) -> i64 {
    parts
        .fixed_ms
        .saturating_add(parts.calendar_months.saturating_mul(30 * 86_400_000))
}

fn parse_duration_parts(text: &str) -> Option<DurationParts> {
    let mut fixed_ms = 0i64;
    let mut calendar_months = 0i64;
    let mut pos = 0usize;
    let bytes = text.as_bytes();
    while pos < bytes.len() {
        while bytes.get(pos).is_some_and(|b| b.is_ascii_whitespace()) {
            pos += 1;
        }
        if pos >= bytes.len() {
            break;
        }
        let start = pos;
        while bytes
            .get(pos)
            .is_some_and(|b| b.is_ascii_digit() || *b == b'.')
        {
            pos += 1;
        }
        if start == pos {
            return None;
        }
        let number = text[start..pos].parse::<f64>().ok()?;
        while bytes.get(pos).is_some_and(|b| b.is_ascii_whitespace()) {
            pos += 1;
        }
        let unit_start = pos;
        while bytes.get(pos).is_some_and(|b| b.is_ascii_alphabetic()) {
            pos += 1;
        }
        let unit = &text[unit_start..pos];
        let factor = match unit {
            "ms" | "millisecond" | "milliseconds" => 1.0,
            "s" | "sec" | "secs" | "second" | "seconds" => 1_000.0,
            "m" | "min" | "mins" | "minute" | "minutes" => 60_000.0,
            "h" | "hr" | "hrs" | "hour" | "hours" => 3_600_000.0,
            "d" | "day" | "days" => 86_400_000.0,
            "w" | "week" | "weeks" => 7.0 * 86_400_000.0,
            "M" | "mo" | "month" | "months" => {
                calendar_months = calendar_months.saturating_add(number.round() as i64);
                continue;
            }
            "y" | "yr" | "year" | "years" => {
                calendar_months = calendar_months.saturating_add((number * 12.0).round() as i64);
                continue;
            }
            _ => return None,
        };
        fixed_ms = fixed_ms.saturating_add((number * factor) as i64);
    }
    Some(DurationParts {
        calendar_months,
        fixed_ms,
    })
}

fn parse_dql_duration_parts(text: &str) -> Result<Option<DqlDurationValue>, EvalError> {
    let text = text.trim();
    if text.is_empty() {
        return Ok(None);
    }
    let mut duration = DqlDurationValue::zero();
    let mut pos = 0usize;
    let bytes = text.as_bytes();
    let mut parsed = false;
    while pos < bytes.len() {
        let start = pos;
        if bytes.get(pos) == Some(&b'-') {
            pos += 1;
        }
        let integer_start = pos;
        while bytes.get(pos).is_some_and(u8::is_ascii_digit) {
            pos += 1;
        }
        if integer_start == pos {
            return Ok(None);
        }
        if bytes.get(pos) == Some(&b'.') {
            pos += 1;
            let fractional_start = pos;
            while bytes.get(pos).is_some_and(u8::is_ascii_digit) {
                pos += 1;
            }
            if fractional_start == pos {
                return Ok(None);
            }
        }
        let Ok(number) = text[start..pos].parse::<f64>() else {
            return Ok(None);
        };
        if !number.is_finite() {
            return Err(invalid_arg(
                "duration",
                "DQL duration component must be finite",
            ));
        }
        while bytes.get(pos).is_some_and(u8::is_ascii_whitespace) {
            pos += 1;
        }
        let unit_start = pos;
        while bytes.get(pos).is_some_and(u8::is_ascii_alphabetic) {
            pos += 1;
        }
        let unit = &text[unit_start..pos];
        match unit {
            "yr" | "yrs" | "year" | "years" => duration.years += number,
            "mo" | "mos" | "month" | "months" => duration.months += number,
            "w" | "wk" | "wks" | "week" | "weeks" => duration.weeks += number,
            "d" | "day" | "days" => duration.days += number,
            "h" | "hr" | "hrs" | "hour" | "hours" => duration.hours += number,
            "m" | "min" | "mins" | "minute" | "minutes" => duration.minutes += number,
            "s" | "sec" | "secs" | "second" | "seconds" => duration.seconds += number,
            _ => return Ok(None),
        }
        parsed = true;
        while bytes.get(pos).is_some_and(u8::is_ascii_whitespace) {
            pos += 1;
        }
        if pos == bytes.len() {
            break;
        }
        if bytes[pos] == b',' {
            pos += 1;
            while bytes.get(pos).is_some_and(u8::is_ascii_whitespace) {
                pos += 1;
            }
            if pos == bytes.len() || bytes[pos] == b',' {
                return Ok(None);
            }
        }
    }
    if !duration.is_finite() {
        return Err(invalid_arg(
            "duration",
            "DQL duration component total must be finite",
        ));
    }
    Ok(parsed.then_some(duration))
}

fn today_value(now_ms: i64) -> DateValue {
    let dt = DateTime::<Utc>::from_timestamp_millis(now_ms).expect("ctx.now_ms is in chrono range");
    let date = dt.date_naive();
    let midnight = Utc.from_utc_datetime(
        &date
            .and_hms_milli_opt(0, 0, 0, 0)
            .expect("midnight is valid"),
    );
    DateValue {
        epoch_ms: midnight.timestamp_millis(),
        has_time: false,
    }
}

fn strip_time(date: DateValue) -> DateValue {
    today_value(date.epoch_ms)
}

fn dql_strip_time(date: DqlDateValue) -> Result<DqlDateValue, EvalError> {
    let local = dql_date_time(date)?;
    resolve_dql_local_datetime(
        date,
        local
            .date_naive()
            .and_hms_milli_opt(0, 0, 0, 0)
            .expect("midnight is valid"),
    )
    .map(|mut value| {
        value.has_time = false;
        value
    })
}

fn add_duration(date: DateValue, duration: DurationParts) -> DateValue {
    let date = if duration.calendar_months == 0 {
        date
    } else {
        add_calendar_months(date, duration.calendar_months)
    };
    DateValue {
        epoch_ms: date.epoch_ms.saturating_add(duration.fixed_ms),
        has_time: date.has_time || duration.fixed_ms % 86_400_000 != 0,
    }
}

fn add_dql_date_duration(
    date: DqlDateValue,
    duration: DqlDurationValue,
) -> Result<DqlDateValue, EvalError> {
    let calendar_months = duration.years * 12.0 + duration.months;
    let calendar_days = duration.weeks * 7.0 + duration.days;
    let whole_days = calendar_days.trunc();
    let fixed_ms = (calendar_days - whole_days) * 86_400_000.0
        + duration.hours * 3_600_000.0
        + duration.minutes * 60_000.0
        + duration.seconds * 1_000.0
        + duration.milliseconds;
    if !calendar_months.is_finite()
        || !whole_days.is_finite()
        || !fixed_ms.is_finite()
        || calendar_months.fract() != 0.0
        || fixed_ms.fract() != 0.0
        || calendar_months < i64::MIN as f64
        || calendar_months > i64::MAX as f64
        || whole_days < i64::MIN as f64
        || whole_days > i64::MAX as f64
        || fixed_ms < i64::MIN as f64
        || fixed_ms > i64::MAX as f64
    {
        return Err(invalid_arg(
            "date arithmetic",
            "DQL date arithmetic cannot preserve this fractional duration exactly",
        ));
    }
    let local = dql_date_time(date)?.naive_local();
    let local = add_calendar_months_naive(local, calendar_months as i64)?
        .checked_add_signed(chrono::Duration::days(whole_days as i64))
        .ok_or_else(|| invalid_arg("date arithmetic", "DQL date arithmetic overflowed"))?;
    let calendar_date = resolve_dql_local_datetime(date, local)?;
    let epoch_ms = calendar_date.epoch_ms.saturating_add(fixed_ms as i64);
    let mut result = if date.is_local {
        dql_date_at_epoch(epoch_ms, date.has_time || fixed_ms != 0.0)?
    } else {
        DqlDateValue {
            epoch_ms,
            has_time: date.has_time || fixed_ms != 0.0,
            ..date
        }
    };
    result.has_time = date.has_time || fixed_ms % 86_400_000.0 != 0.0;
    Ok(result)
}

fn resolve_dql_local_datetime(
    provenance: DqlDateValue,
    naive: NaiveDateTime,
) -> Result<DqlDateValue, EvalError> {
    if provenance.is_local {
        return dql_date_from_local(naive, provenance.has_time);
    }
    let offset = FixedOffset::east_opt(provenance.offset_minutes.saturating_mul(60))
        .ok_or_else(|| invalid_arg("date", "DQL date offset is outside the supported range"))?;
    let fixed = offset
        .from_local_datetime(&naive)
        .single()
        .ok_or_else(|| invalid_arg("date", "DQL fixed-offset datetime could not be resolved"))?;
    Ok(dql_date_from_fixed(fixed, provenance.has_time))
}

fn add_calendar_months_naive(date: NaiveDateTime, months: i64) -> Result<NaiveDateTime, EvalError> {
    let month0 = i64::from(date.year())
        .checked_mul(12)
        .and_then(|year_month| year_month.checked_add(i64::from(date.month0())))
        .and_then(|current_month| current_month.checked_add(months))
        .ok_or_else(|| invalid_arg("date arithmetic", "DQL month arithmetic overflowed"))?;
    let year = i32::try_from(month0.div_euclid(12))
        .map_err(|_| invalid_arg("date arithmetic", "DQL month arithmetic overflowed"))?;
    let month = month0.rem_euclid(12) as u32 + 1;
    let day = date
        .day()
        .min(days_in_month(year, month).ok_or_else(|| invalid_arg("date", "invalid month"))?);
    NaiveDate::from_ymd_opt(year, month, day)
        .and_then(|value| {
            value.and_hms_nano_opt(date.hour(), date.minute(), date.second(), date.nanosecond())
        })
        .ok_or_else(|| invalid_arg("date arithmetic", "DQL month arithmetic overflowed"))
}

fn dql_date_difference_value(
    lhs: DqlDateValue,
    rhs: DqlDateValue,
) -> Result<DqlDurationValue, EvalError> {
    if lhs.epoch_ms == rhs.epoch_ms {
        return Ok(DqlDurationValue::zero());
    }
    if lhs.is_local != rhs.is_local || (!lhs.is_local && lhs.offset_minutes != rhs.offset_minutes) {
        return Err(invalid_arg(
            "date subtraction",
            "DQL dates with different zone provenance cannot be subtracted exactly",
        ));
    }

    let (later, earlier, sign) = if lhs.epoch_ms > rhs.epoch_ms {
        (lhs, rhs, 1.0)
    } else {
        (rhs, lhs, -1.0)
    };
    let later_dt = dql_date_time(later)?;
    let earlier_dt = dql_date_time(earlier)?;

    let mut years = i64::from(later_dt.year()) - i64::from(earlier_dt.year());
    while years > 0 && add_dql_calendar_months(earlier, years * 12)?.epoch_ms > later.epoch_ms {
        years -= 1;
    }
    while add_dql_calendar_months(earlier, (years + 1) * 12)?.epoch_ms <= later.epoch_ms {
        years += 1;
    }
    let after_years = add_dql_calendar_months(earlier, years * 12)?;
    let after_years_dt = dql_date_time(after_years)?;
    let mut months = (i64::from(later_dt.year()) - i64::from(after_years_dt.year())) * 12
        + i64::from(later_dt.month0())
        - i64::from(after_years_dt.month0());
    while months > 0 && add_dql_calendar_months(after_years, months)?.epoch_ms > later.epoch_ms {
        months -= 1;
    }
    while add_dql_calendar_months(after_years, months + 1)?.epoch_ms <= later.epoch_ms {
        months += 1;
    }
    let cursor = add_dql_calendar_months(after_years, months)?;
    let later_day = dql_date_time(later)?.date_naive();
    let cursor_day = dql_date_time(cursor)?.date_naive();
    let mut days = later_day.signed_duration_since(cursor_day).num_days();
    let mut after_days = add_dql_calendar_days(cursor, days)?;
    while days > 0 && after_days.epoch_ms > later.epoch_ms {
        days -= 1;
        after_days = add_dql_calendar_days(cursor, days)?;
    }
    loop {
        let next = add_dql_calendar_days(cursor, days + 1)?;
        if next.epoch_ms > later.epoch_ms {
            break;
        }
        days += 1;
        after_days = next;
    }

    let mut remaining = later.epoch_ms.saturating_sub(after_days.epoch_ms);
    let hours = remaining / 3_600_000;
    remaining %= 3_600_000;
    let minutes = remaining / 60_000;
    remaining %= 60_000;
    let seconds = remaining / 1_000;
    let milliseconds = remaining % 1_000;
    Ok(DqlDurationValue {
        years: years as f64 * sign,
        months: months as f64 * sign,
        weeks: 0.0,
        days: days as f64 * sign,
        hours: hours as f64 * sign,
        minutes: minutes as f64 * sign,
        seconds: seconds as f64 * sign,
        milliseconds: milliseconds as f64 * sign,
    })
}

fn add_dql_calendar_months(date: DqlDateValue, months: i64) -> Result<DqlDateValue, EvalError> {
    let local = add_calendar_months_naive(dql_date_time(date)?.naive_local(), months)?;
    resolve_dql_local_datetime(date, local)
}

fn add_dql_calendar_days(date: DqlDateValue, days: i64) -> Result<DqlDateValue, EvalError> {
    let local = dql_date_time(date)?
        .naive_local()
        .checked_add_signed(chrono::Duration::days(days))
        .ok_or_else(|| invalid_arg("date subtraction", "DQL date difference overflowed"))?;
    resolve_dql_local_datetime(date, local)
}

fn dql_date_difference(lhs: DateValue, rhs: DateValue) -> Result<DqlDurationValue, EvalError> {
    if lhs.epoch_ms == rhs.epoch_ms {
        return Ok(DqlDurationValue::zero());
    }
    let (later, earlier, sign) = if lhs.epoch_ms > rhs.epoch_ms {
        (lhs, rhs, 1.0)
    } else {
        (rhs, lhs, -1.0)
    };
    let later_dt = date_time(later)?;
    let earlier_dt = date_time(earlier)?;

    let mut years = i64::from(later_dt.year()) - i64::from(earlier_dt.year());
    while years > 0 && add_calendar_months(earlier, years * 12).epoch_ms > later.epoch_ms {
        years -= 1;
    }
    while add_calendar_months(earlier, (years + 1) * 12).epoch_ms <= later.epoch_ms {
        years += 1;
    }
    let after_years = add_calendar_months(earlier, years * 12);
    let after_years_dt = date_time(after_years)?;
    let mut months = (i64::from(later_dt.year()) - i64::from(after_years_dt.year())) * 12
        + i64::from(later_dt.month0())
        - i64::from(after_years_dt.month0());
    while months > 0 && add_calendar_months(after_years, months).epoch_ms > later.epoch_ms {
        months -= 1;
    }
    while add_calendar_months(after_years, months + 1).epoch_ms <= later.epoch_ms {
        months += 1;
    }
    let cursor = add_calendar_months(after_years, months);
    let mut remaining = later.epoch_ms.saturating_sub(cursor.epoch_ms);
    let days = remaining / 86_400_000;
    remaining %= 86_400_000;
    let hours = remaining / 3_600_000;
    remaining %= 3_600_000;
    let minutes = remaining / 60_000;
    remaining %= 60_000;
    let seconds = remaining / 1_000;
    let milliseconds = remaining % 1_000;
    Ok(DqlDurationValue {
        years: years as f64 * sign,
        months: months as f64 * sign,
        weeks: 0.0,
        days: days as f64 * sign,
        hours: hours as f64 * sign,
        minutes: minutes as f64 * sign,
        seconds: seconds as f64 * sign,
        milliseconds: milliseconds as f64 * sign,
    })
}

fn add_calendar_months(date: DateValue, months: i64) -> DateValue {
    let Ok(dt) = date_time(date) else {
        return date;
    };
    let Some(month0) = i64::from(dt.year())
        .checked_mul(12)
        .and_then(|year_month| year_month.checked_add(i64::from(dt.month0())))
        .and_then(|current_month| current_month.checked_add(months))
    else {
        return date;
    };
    let Ok(year) = i32::try_from(month0.div_euclid(12)) else {
        return date;
    };
    let month0 = month0.rem_euclid(12) as u32;
    let month = month0 + 1;
    let Some(last_day) = days_in_month(year, month) else {
        return date;
    };
    let day = dt.day().min(last_day);
    let Some(naive) = NaiveDate::from_ymd_opt(year, month, day).and_then(|d| {
        d.and_hms_milli_opt(
            dt.hour(),
            dt.minute(),
            dt.second(),
            dt.timestamp_subsec_millis(),
        )
    }) else {
        return date;
    };
    DateValue {
        epoch_ms: Utc.from_utc_datetime(&naive).timestamp_millis(),
        has_time: date.has_time,
    }
}

fn days_in_month(year: i32, month: u32) -> Option<u32> {
    let (next_year, next_month) = if month == 12 {
        (year.checked_add(1)?, 1)
    } else {
        (year, month + 1)
    };
    let first_next = NaiveDate::from_ymd_opt(next_year, next_month, 1)?;
    first_next.pred_opt().map(|date| date.day())
}

fn date_time(date: DateValue) -> Result<DateTime<Utc>, EvalError> {
    DateTime::<Utc>::from_timestamp_millis(date.epoch_ms)
        .ok_or_else(|| invalid_arg("date", "timestamp is outside chrono range"))
}

fn dql_date_time(date: DqlDateValue) -> Result<DateTime<FixedOffset>, EvalError> {
    let offset = FixedOffset::east_opt(date.offset_minutes.saturating_mul(60))
        .ok_or_else(|| invalid_arg("date", "DQL date offset is outside the supported range"))?;
    let utc = DateTime::<Utc>::from_timestamp_millis(date.epoch_ms)
        .ok_or_else(|| invalid_arg("date", "timestamp is outside chrono range"))?;
    Ok(utc.with_timezone(&offset))
}

pub(crate) fn dql_date_display(date: DqlDateValue) -> String {
    let Ok(dt) = dql_date_time(date) else {
        return String::new();
    };
    if date.has_time {
        dt.to_rfc3339_opts(chrono::SecondsFormat::Millis, false)
    } else {
        dt.format("%Y-%m-%d").to_string()
    }
}

fn format_date_default(date: DateValue) -> String {
    if let Ok(dt) = date_time(date) {
        if date.has_time {
            dt.format("%Y-%m-%dT%H:%M:%SZ").to_string()
        } else {
            dt.format("%Y-%m-%d").to_string()
        }
    } else {
        String::new()
    }
}

fn format_date(date: DateValue, format: &str) -> Result<String, EvalError> {
    let dt = date_time(date)?;
    let mut out = String::new();
    let mut pos = 0usize;
    while pos < format.len() {
        let rest = &format[pos..];
        let (token, value) = if rest.starts_with("YYYY") {
            ("YYYY", format!("{:04}", dt.year()))
        } else if rest.starts_with("MMM") {
            ("MMM", dt.format("%b").to_string())
        } else if rest.starts_with("ddd") {
            ("ddd", dt.format("%a").to_string())
        } else if rest.starts_with('E') {
            ("E", dt.weekday().number_from_monday().to_string())
        } else if rest.starts_with("MM") {
            ("MM", format!("{:02}", dt.month()))
        } else if rest.starts_with("DD") {
            ("DD", format!("{:02}", dt.day()))
        } else if rest.starts_with("HH") {
            ("HH", format!("{:02}", dt.hour()))
        } else if rest.starts_with("mm") {
            ("mm", format!("{:02}", dt.minute()))
        } else if rest.starts_with("ss") {
            ("ss", format!("{:02}", dt.second()))
        } else {
            let ch = rest.chars().next().expect("non-empty rest");
            if ch.is_ascii_alphabetic() {
                return Err(EvalError::UnsupportedFormatToken {
                    token: ch.to_string(),
                });
            }
            out.push(ch);
            pos += ch.len_utf8();
            continue;
        };
        out.push_str(&value);
        pos += token.len();
    }
    Ok(out)
}

fn format_dql_date(date: DqlDateValue, format: &str) -> Result<String, EvalError> {
    let dt = dql_date_time(date)?;
    let mut out = String::new();
    let mut pos = 0usize;
    while pos < format.len() {
        let rest = &format[pos..];
        let (token, value) = if rest.starts_with("YYYY") {
            ("YYYY", format!("{:04}", dt.year()))
        } else if rest.starts_with("MMM") {
            ("MMM", dt.format("%b").to_string())
        } else if rest.starts_with("ddd") {
            ("ddd", dt.format("%a").to_string())
        } else if rest.starts_with('E') {
            ("E", dt.weekday().number_from_monday().to_string())
        } else if rest.starts_with("MM") {
            ("MM", format!("{:02}", dt.month()))
        } else if rest.starts_with("DD") {
            ("DD", format!("{:02}", dt.day()))
        } else if rest.starts_with("HH") {
            ("HH", format!("{:02}", dt.hour()))
        } else if rest.starts_with("mm") {
            ("mm", format!("{:02}", dt.minute()))
        } else if rest.starts_with("ss") {
            ("ss", format!("{:02}", dt.second()))
        } else {
            let ch = rest.chars().next().expect("non-empty rest");
            if ch.is_ascii_alphabetic() {
                return Err(EvalError::UnsupportedFormatToken {
                    token: ch.to_string(),
                });
            }
            out.push(ch);
            pos += ch.len_utf8();
            continue;
        };
        out.push_str(&value);
        pos += token.len();
    }
    Ok(out)
}

fn relative_text(delta_ms: i64) -> String {
    let abs = delta_ms.abs();
    let (count, unit) = if abs >= 86_400_000 {
        (abs / 86_400_000, "day")
    } else if abs >= 3_600_000 {
        (abs / 3_600_000, "hour")
    } else if abs >= 60_000 {
        (abs / 60_000, "minute")
    } else {
        (abs / 1000, "second")
    };
    let plural = if count == 1 { "" } else { "s" };
    if delta_ms >= 0 {
        format!("{count} {unit}{plural} ago")
    } else {
        format!("in {count} {unit}{plural}")
    }
}

fn tag_matches(got: &str, wanted: &str) -> bool {
    let got = got.trim_start_matches('#');
    let wanted = wanted.trim_start_matches('#');
    got == wanted
        || got
            .strip_prefix(wanted)
            .is_some_and(|rest| rest.starts_with('/'))
}

fn title_case(input: &str) -> String {
    input
        .split_word_bounds_ascii()
        .into_iter()
        .map(|word| {
            let mut chars = word.chars();
            match chars.next() {
                Some(first) if first.is_alphabetic() => {
                    first.to_uppercase().collect::<String>() + &chars.as_str().to_lowercase()
                }
                _ => word.to_string(),
            }
        })
        .collect()
}

trait SplitWordsAscii {
    fn split_word_bounds_ascii(&self) -> Vec<&str>;
}

impl SplitWordsAscii for str {
    fn split_word_bounds_ascii(&self) -> Vec<&str> {
        let mut out = Vec::new();
        let mut start = 0usize;
        let mut in_word = false;
        for (idx, ch) in self.char_indices() {
            let word = ch.is_alphanumeric();
            if idx == 0 {
                in_word = word;
                continue;
            }
            if word != in_word {
                out.push(&self[start..idx]);
                start = idx;
                in_word = word;
            }
        }
        if start <= self.len() {
            out.push(&self[start..]);
        }
        out
    }
}

fn escape_html(input: &str) -> String {
    let mut out = String::new();
    for ch in input.chars() {
        match ch {
            '&' => out.push_str("&amp;"),
            '<' => out.push_str("&lt;"),
            '>' => out.push_str("&gt;"),
            '"' => out.push_str("&quot;"),
            '\'' => out.push_str("&#39;"),
            _ => out.push(ch),
        }
    }
    out
}
