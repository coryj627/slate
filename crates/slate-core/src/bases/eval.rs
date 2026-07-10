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
};

use chrono::{DateTime, Datelike, NaiveDate, NaiveDateTime, TimeZone, Timelike, Utc};
use regex::{Regex, RegexBuilder};
use thiserror::Error;

use super::expr::{
    BinaryOp, Callee, Expr, ExprKind, FileField, GlobalFn, ListExprKind, Lit, MethodName,
    PropertyRef, TaskField, UnaryOp,
};

pub type ResolvedFormulas = BTreeMap<String, Value>;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
struct DurationParts {
    calendar_months: i64,
    fixed_ms: i64,
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
    Duration(i64),
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

#[derive(Debug, Clone, PartialEq, Eq, PartialOrd, Ord)]
pub struct LinkValue {
    pub target: String,
    pub display: Option<String>,
    pub resolved_path: Option<String>,
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

    fn row_for_path(&self, _path: &str) -> Option<RowContext> {
        None
    }

    fn links_for(&self, _path: &str) -> Vec<LinkValue> {
        Vec::new()
    }

    fn embeds_for(&self, _path: &str) -> Vec<LinkValue> {
        Vec::new()
    }

    fn backlinks_for(&self, _path: &str) -> Vec<LinkValue> {
        Vec::new()
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
            Ok(index_value(&base, &index, ctx))
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
        UnaryOp::Not => Ok(Value::Bool(!rhs.is_truthy())),
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
        if !lhs.is_truthy() {
            return Ok(Value::Bool(false));
        }
        return Ok(Value::Bool(eval_inner(rhs, ctx, locals)?.is_truthy()));
    }
    if op == BinaryOp::Or {
        let lhs = eval_inner(lhs, ctx, locals)?;
        if lhs.is_truthy() {
            return Ok(Value::Bool(true));
        }
        return Ok(Value::Bool(eval_inner(rhs, ctx, locals)?.is_truthy()));
    }

    let (lhs, lhs_literal_duration) = eval_binary_operand(lhs, ctx, locals)?;
    let (rhs, rhs_literal_duration) = eval_binary_operand(rhs, ctx, locals)?;
    match op {
        BinaryOp::Add => {
            if let (Value::Date(date), Some(duration)) = (&lhs, rhs_literal_duration) {
                return Ok(Value::Date(add_duration(*date, duration)));
            }
            if let (Some(duration), Value::Date(date)) = (lhs_literal_duration, &rhs) {
                return Ok(Value::Date(add_duration(*date, duration)));
            }
            add_values(lhs, rhs, ctx)
        }
        BinaryOp::Sub => {
            if let (Value::Date(date), Some(duration)) = (&lhs, rhs_literal_duration) {
                return Ok(Value::Date(add_duration(*date, duration.negated())));
            }
            sub_values(lhs, rhs, ctx)
        }
        BinaryOp::Mul => mul_values(lhs, rhs, ctx),
        BinaryOp::Div => div_values(lhs, rhs, ctx),
        BinaryOp::Mod => mod_values(lhs, rhs, ctx),
        BinaryOp::Eq => Ok(Value::Bool(value_eq(&lhs, &rhs))),
        BinaryOp::Ne => Ok(Value::Bool(!value_eq(&lhs, &rhs))),
        BinaryOp::Gt | BinaryOp::Lt | BinaryOp::Gte | BinaryOp::Lte => {
            let Some(ordering) = compare_values(&lhs, &rhs, ctx) else {
                return Ok(Value::Bool(false));
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
    if let (Value::Text(text), Some(times)) = (&lhs, coerce_number(&rhs)) {
        return Ok(Value::Text(text.repeat(times.max(0.0).floor() as usize)));
    }
    if let (Some(times), Value::Text(text)) = (coerce_number(&lhs), &rhs) {
        return Ok(Value::Text(text.repeat(times.max(0.0).floor() as usize)));
    }
    numeric_binary(&lhs, &rhs, ctx, |a, b| a * b)
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
    if eval_inner(&args[0], ctx, locals)?.is_truthy() {
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
            eval_date(&values, ctx)
        }
        GlobalFn::Duration => {
            let values = eval_args(args, ctx, locals)?;
            eval_duration(&values)
        }
        GlobalFn::Number => {
            let values = eval_args(args, ctx, locals)?;
            expect_arity("number", values.len(), 1, 1)?;
            Ok(coerce_number(&values[0])
                .map(Value::Number)
                .unwrap_or(Value::Null))
        }
        GlobalFn::String => {
            let values = eval_args(args, ctx, locals)?;
            expect_arity("string", values.len(), 1, 1)?;
            Ok(Value::Text(value_to_string(&values[0])))
        }
        GlobalFn::Link => {
            let values = eval_args(args, ctx, locals)?;
            expect_arity("link", values.len(), 1, 2)?;
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
            }))
        }
        GlobalFn::List => {
            let values = eval_args(args, ctx, locals)?;
            normalize_list(values)
        }
        GlobalFn::Object => {
            let values = eval_args(args, ctx, locals)?;
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
            Ok(Value::Bool(receiver.is_truthy()))
        }
        MethodName::IsType => {
            expect_arity("isType", args.len(), 1, 1)?;
            Ok(Value::Bool(
                receiver.is_type(&expect_text_like("isType", &args[0])?),
            ))
        }
        MethodName::ToString => {
            expect_arity("toString", args.len(), 0, 0)?;
            Ok(Value::Text(value_to_string(&receiver)))
        }
        MethodName::Date => {
            expect_arity("date", args.len(), 0, 0)?;
            match receiver {
                Value::Date(date) => Ok(Value::Date(strip_time(date))),
                Value::Null => Ok(Value::Null),
                other => eval_date(&[other], ctx),
            }
        }
        MethodName::Format => {
            expect_arity("format", args.len(), 1, 1)?;
            let Value::Date(date) = receiver else {
                return Ok(Value::Null);
            };
            Ok(Value::Text(format_date(
                date,
                &expect_text_like("format", &args[0])?,
            )?))
        }
        MethodName::Time => {
            expect_arity("time", args.len(), 0, 0)?;
            if let Value::Date(date) = receiver {
                let dt = date_time(date)?;
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
            Ok(Value::Bool(contains_value(&receiver, &args[0], false)))
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
        MethodName::StartsWith => string_method(&receiver, "startsWith", args, 1, |s, args| {
            Value::Bool(s.starts_with(&value_to_string(&args[0])))
        }),
        MethodName::EndsWith => string_method(&receiver, "endsWith", args, 1, |s, args| {
            Value::Bool(s.ends_with(&value_to_string(&args[0])))
        }),
        MethodName::Lower => string_method(&receiver, "lower", args, 0, |s, _| {
            Value::Text(s.to_lowercase())
        }),
        MethodName::Title => string_method(&receiver, "title", args, 0, |s, _| {
            Value::Text(title_case(s))
        }),
        MethodName::Trim => string_method(&receiver, "trim", args, 0, |s, _| {
            Value::Text(s.trim().to_string())
        }),
        MethodName::Reverse => match receiver {
            Value::Text(text) => {
                expect_arity("reverse", args.len(), 0, 0)?;
                Ok(Value::Text(text.chars().rev().collect()))
            }
            Value::List(mut values) => {
                expect_arity("reverse", args.len(), 0, 0)?;
                values.reverse();
                Ok(Value::List(values))
            }
            _ => Ok(Value::Null),
        },
        MethodName::Repeat => string_method(&receiver, "repeat", args, 1, |s, args| {
            let n = coerce_number(&args[0]).unwrap_or(0.0).max(0.0).floor() as usize;
            Value::Text(s.repeat(n))
        }),
        MethodName::Slice => slice_value(receiver, args),
        MethodName::Split => split_value(receiver, args),
        MethodName::Replace => replace_value(receiver, args),
        MethodName::Abs => number_method(receiver, "abs", args, 0, |n, _| Value::Number(n.abs())),
        MethodName::Ceil => {
            number_method(receiver, "ceil", args, 0, |n, _| Value::Number(n.ceil()))
        }
        MethodName::Floor => {
            number_method(receiver, "floor", args, 0, |n, _| Value::Number(n.floor()))
        }
        MethodName::Trunc => {
            number_method(receiver, "trunc", args, 0, |n, _| Value::Number(n.trunc()))
        }
        MethodName::Round => {
            expect_arity("round", args.len(), 0, 1)?;
            let Some(number) = coerce_number(&receiver) else {
                return Ok(Value::Null);
            };
            let digits = args.first().and_then(coerce_number).unwrap_or(0.0) as i32;
            let factor = 10_f64.powi(digits);
            Ok(Value::Number((number * factor).round() / factor))
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
            let Value::List(values) = receiver else {
                return Ok(Value::Null);
            };
            let sep = value_to_string(&args[0]);
            Ok(Value::Text(
                values
                    .iter()
                    .map(value_to_string)
                    .collect::<Vec<_>>()
                    .join(&sep),
            ))
        }
        MethodName::Flat => {
            expect_arity("flat", args.len(), 0, 0)?;
            let Value::List(values) = receiver else {
                return Ok(Value::Null);
            };
            Ok(Value::List(flatten(values, 1)))
        }
        MethodName::Sort => {
            expect_arity("sort", args.len(), 0, 0)?;
            let Value::List(mut values) = receiver else {
                return Ok(Value::Null);
            };
            values.sort_by(value_total_cmp);
            Ok(Value::List(values))
        }
        MethodName::Unique => {
            expect_arity("unique", args.len(), 0, 0)?;
            let Value::List(values) = receiver else {
                return Ok(Value::Null);
            };
            let mut seen = BTreeSet::new();
            let mut out = Vec::new();
            for value in values {
                if seen.insert(value_key(&value)) {
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
            }))
        }
        MethodName::HasLink => {
            expect_arity("hasLink", args.len(), 1, 1)?;
            let path = file_path_from_value(&receiver)?;
            let target = file_path_from_value(&args[0])?;
            Ok(Value::Bool(ctx.vault.links_for(&path).iter().any(|link| {
                link.resolved_path.as_deref() == Some(target.as_str()) || link.target == target
            })))
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
            let Value::Regex(pattern, flags) = receiver else {
                return Ok(Value::Null);
            };
            let regex = build_regex(&pattern, &flags, "matches")?;
            Ok(Value::Bool(regex.is_match(&value_to_string(&args[0]))))
        }
    }
}

fn eval_list_expr(
    base: &Expr,
    kind: ListExprKind,
    body: &Expr,
    init: Option<&Expr>,
    ctx: &EvalCtx<'_>,
    locals: &Locals<'_>,
) -> Result<Value, EvalError> {
    let base = eval_inner(base, ctx, locals)?;
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

fn eval_aggregate(
    function: GlobalFn,
    values: &[Value],
    ctx: &EvalCtx<'_>,
) -> Result<Value, EvalError> {
    let mut flattened = Vec::new();
    for value in values {
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

fn row_property(row: &RowContext, key: &str) -> Value {
    row.properties
        .iter()
        .rev()
        .find(|(got, _)| got == key)
        .map(|(_, value)| value.clone())
        .or_else(|| row.file_fields.properties.get(key).cloned())
        .unwrap_or(Value::Null)
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

fn index_value(base: &Value, index: &Value, ctx: &EvalCtx<'_>) -> Value {
    match (base, index) {
        (Value::List(values), index) => coerce_number(index)
            .and_then(index_as_usize)
            .and_then(|idx| values.get(idx).cloned())
            .unwrap_or(Value::Null),
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
    }
}

fn index_as_usize(index: f64) -> Option<usize> {
    if !index.is_finite() || index < 0.0 || index > usize::MAX as f64 {
        return None;
    }
    Some(index.trunc() as usize)
}

fn field_value(base: &Value, name: &str, ctx: &EvalCtx<'_>) -> Result<Value, EvalError> {
    Ok(match base {
        Value::Object(map) => map.get(name).cloned().unwrap_or(Value::Null),
        Value::List(values) if name == "length" => Value::Number(values.len() as f64),
        Value::Text(text) if name == "length" => Value::Number(text.chars().count() as f64),
        Value::Date(date) => date_field(*date, name)?,
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
    let text = match receiver {
        Value::Text(text) => text,
        Value::Null => return Ok(Value::Null),
        other => value_to_string(&other),
    };
    let limit = match args.get(1) {
        None => None,
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
        Value::Regex(pattern, flags) => build_regex(pattern, flags, "split")?
            .split(&text)
            .map(|part| Value::Text(part.to_string()))
            .collect::<Vec<_>>(),
        _ => {
            return Err(invalid_arg(
                "split",
                "expected text or regular expression separator",
            ));
        }
    };
    if let Some(limit) = limit {
        parts.truncate(limit);
    }
    Ok(Value::List(parts))
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

fn slice_value(receiver: Value, args: &[Value]) -> Result<Value, EvalError> {
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

fn replace_value(receiver: Value, args: &[Value]) -> Result<Value, EvalError> {
    expect_arity("replace", args.len(), 2, 2)?;
    let text = value_to_string(&receiver);
    let replacement = value_to_string(&args[1]);
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
        pattern => Ok(Value::Text(
            text.replace(&value_to_string(pattern), &replacement),
        )),
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
        (Value::Duration(a), Value::Duration(b)) => a == b,
        (Value::Link(a), Value::Link(b)) => link_identity(a) == link_identity(b),
        (Value::File(a), Value::File(b)) => a.path == b.path,
        (Value::Link(link), Value::File(file)) | (Value::File(file), Value::Link(link)) => {
            link_identity(link) == file.path
        }
        _ => coerce_number(lhs)
            .zip(coerce_number(rhs))
            .is_some_and(|(a, b)| a == b),
    }
}

fn compare_values(lhs: &Value, rhs: &Value, ctx: &EvalCtx<'_>) -> Option<Ordering> {
    let ordering = match (lhs, rhs) {
        (Value::Null, _) | (_, Value::Null) => None,
        (Value::Date(a), Value::Date(b)) => Some(a.epoch_ms.cmp(&b.epoch_ms)),
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

fn value_total_cmp(lhs: &Value, rhs: &Value) -> Ordering {
    value_key(lhs).cmp(&value_key(rhs))
}

fn value_key(value: &Value) -> String {
    format!("{}:{}", value.type_name(), value_to_string(value))
}

fn link_identity(link: &LinkValue) -> &str {
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

fn value_to_string(value: &Value) -> String {
    match value {
        Value::Null => String::new(),
        Value::Bool(value) => value.to_string(),
        Value::Number(value) => format_number(*value),
        Value::Text(value) => value.clone(),
        Value::Date(value) => format_date_default(*value),
        Value::Duration(value) => value.to_string(),
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
    if value.is_finite() && value.fract() == 0.0 {
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
        | Value::Duration(_)
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
            | Value::Duration(_)
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
            Value::Duration(_) => "duration",
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
