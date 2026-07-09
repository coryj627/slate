// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

use std::collections::BTreeMap;

use proptest::prelude::*;
use slate_core::bases::eval::{
    DateValue, EvalCtx, EvalError, FileFields, LinkValue, ResolvedFormulas, RowContext, Value,
    VaultLookup, WarningSink, eval,
};
use slate_core::bases::expr::parse_expr;

#[test]
fn evaluates_file_note_formula_and_this_properties() {
    let mut vault = TestVault::default();
    vault
        .resolved
        .insert("Dashboard".into(), "Dashboard.md".into());
    let warnings = WarningSink::default();
    let file = fixture_row("Projects/Alpha.md");
    let this = fixture_row("Dashboard.md");
    let formulas = BTreeMap::from([("score".to_string(), Value::Number(42.0))]);
    let ctx = ctx(&file, Some(&this), &formulas, &vault, &warnings);

    assert_eq!(
        eval_src("file.name", &ctx).unwrap(),
        Value::Text("Alpha.md".into())
    );
    assert_eq!(eval_src("rating", &ctx).unwrap(), Value::Number(4.5));
    assert_eq!(
        eval_src("file.properties.rating", &ctx).unwrap(),
        Value::Number(4.5)
    );
    assert_eq!(
        eval_src("formula.score", &ctx).unwrap(),
        Value::Number(42.0)
    );
    assert_eq!(
        eval_src("this.file.name", &ctx).unwrap(),
        Value::Text("Dashboard.md".into())
    );
    assert_eq!(
        eval_src("link(\"Dashboard\") == this", &ctx).unwrap(),
        Value::Bool(true)
    );
    assert_eq!(eval_src("missing", &ctx).unwrap(), Value::Null);
}

#[test]
fn data_mismatches_warn_without_structural_errors() {
    let vault = TestVault::default();
    let warnings = WarningSink::default();
    let file = fixture_row("Projects/Alpha.md");
    let formulas = ResolvedFormulas::default();
    let ctx = ctx(&file, None, &formulas, &vault, &warnings);

    assert_eq!(eval_src("missing > 2", &ctx).unwrap(), Value::Bool(false));
    assert_eq!(eval_src("missing < 2", &ctx).unwrap(), Value::Bool(false));
    assert_eq!(eval_src("\"10\" > \"2\"", &ctx).unwrap(), Value::Bool(true));
    assert_number(eval_src("\"2.5\" + 1", &ctx).unwrap(), 3.5);
    assert_eq!(
        eval_src("\"x\" + 3", &ctx).unwrap(),
        Value::Text("x3".into())
    );
    assert_eq!(
        eval_src("1 / 0", &ctx).unwrap(),
        Value::Number(f64::INFINITY)
    );
    assert_eq!(eval_src("0 / 0", &ctx).unwrap(), Value::Null);
    assert!(
        warnings
            .messages()
            .iter()
            .any(|message| message.contains("ordering comparison"))
    );
    assert!(
        warnings
            .messages()
            .iter()
            .any(|message| message.contains("NaN normalized to Null"))
    );
}

#[test]
fn structural_problems_fail_loud() {
    let vault = TestVault::default();
    let warnings = WarningSink::default();
    let file = fixture_row("Projects/Alpha.md");
    let formulas = ResolvedFormulas::default();
    let ctx = ctx(&file, None, &formulas, &vault, &warnings);

    assert!(matches!(
        eval_src("unknownFn(1)", &ctx),
        Err(EvalError::Unsupported { .. })
    ));
    assert!(matches!(
        eval_src("random()", &ctx),
        Err(EvalError::Unsupported { .. })
    ));
    assert!(matches!(
        eval_src("this.file.name", &ctx),
        Err(EvalError::NoThisContext)
    ));
    assert!(matches!(
        eval_src("file.hasTag(5)", &ctx),
        Err(EvalError::InvalidArgument { .. })
    ));
    assert!(matches!(
        eval_src("today().format(\"E\")", &ctx),
        Err(EvalError::UnsupportedFormatToken { .. })
    ));
}

#[test]
fn two_argument_if_returns_the_true_branch() {
    assert_eq!(value("if(true, 7)"), Value::Number(7.0));
}

#[test]
fn two_argument_if_defaults_the_false_branch_to_null() {
    assert_eq!(value("if(false, 7)"), Value::Null);
}

#[test]
fn if_rejects_too_few_arguments() {
    assert_eq!(error_message("if(true)"), "if expected 2..=3, got 1");
}

#[test]
fn if_rejects_too_many_arguments() {
    assert_eq!(
        error_message("if(true, 1, 2, 3)"),
        "if expected 2..=3, got 4"
    );
}

#[test]
fn list_wraps_a_scalar() {
    assert_eq!(value("list(1)"), Value::List(vec![Value::Number(1.0)]));
}

#[test]
fn list_returns_an_existing_list_unchanged() {
    assert_eq!(
        value("list([1, 2])"),
        Value::List(vec![Value::Number(1.0), Value::Number(2.0)])
    );
}

#[test]
fn list_rejects_no_arguments() {
    assert_eq!(error_message("list()"), "list expected 1, got 0");
}

#[test]
fn list_rejects_multiple_arguments() {
    assert_eq!(error_message("list(1, 2)"), "list expected 1, got 2");
}

#[test]
fn string_contains_all_accepts_variadic_needles() {
    assert_eq!(
        value("\"abc\".containsAll(\"a\", \"c\")"),
        Value::Bool(true)
    );
}

#[test]
fn list_contains_all_accepts_variadic_needles() {
    assert_eq!(value("[1, 2, 3].containsAll(1, 3)"), Value::Bool(true));
}

#[test]
fn string_contains_all_expands_a_single_list_of_matching_needles() {
    assert_eq!(value(r#""abc".containsAll(["a", "c"])"#), Value::Bool(true));
}

#[test]
fn string_contains_all_expands_a_single_list_with_a_missing_needle() {
    assert_eq!(
        value(r#""abc".containsAll(["a", "z"])"#),
        Value::Bool(false)
    );
}

#[test]
fn list_contains_all_expands_a_single_list_of_matching_needles() {
    assert_eq!(value("[1, 2, 3].containsAll([1, 3])"), Value::Bool(true));
}

#[test]
fn list_contains_all_expands_a_single_list_with_a_missing_needle() {
    assert_eq!(value("[1, 2, 3].containsAll([1, 4])"), Value::Bool(false));
}

#[test]
fn string_contains_any_accepts_variadic_needles() {
    assert_eq!(
        value("\"abc\".containsAny(\"x\", \"c\")"),
        Value::Bool(true)
    );
}

#[test]
fn list_contains_any_accepts_variadic_needles() {
    assert_eq!(value("[1, 2, 3].containsAny(4, 2)"), Value::Bool(true));
}

#[test]
fn string_contains_any_expands_a_single_list_with_a_matching_needle() {
    assert_eq!(value(r#""abc".containsAny(["z", "b"])"#), Value::Bool(true));
}

#[test]
fn string_contains_any_expands_a_single_list_without_a_matching_needle() {
    assert_eq!(
        value(r#""abc".containsAny(["x", "z"])"#),
        Value::Bool(false)
    );
}

#[test]
fn list_contains_any_expands_a_single_list_with_a_matching_needle() {
    assert_eq!(value("[1, 2, 3].containsAny([4, 2])"), Value::Bool(true));
}

#[test]
fn list_contains_any_expands_a_single_list_without_a_matching_needle() {
    assert_eq!(value("[1, 2, 3].containsAny([4, 5])"), Value::Bool(false));
}

#[test]
fn contains_all_rejects_no_needles() {
    assert_eq!(
        error_message("\"abc\".containsAll()"),
        "containsAll expected at least 1, got 0"
    );
}

#[test]
fn contains_any_rejects_no_needles() {
    assert_eq!(
        error_message("\"abc\".containsAny()"),
        "containsAny expected at least 1, got 0"
    );
}

#[test]
fn split_accepts_a_text_separator() {
    assert_eq!(value("\"a,b,c\".split(\",\")"), text_list(&["a", "b", "c"]));
}

#[test]
fn split_limit_retains_the_first_n_elements() {
    assert_eq!(value("\"a,b,c\".split(\",\", 2)"), text_list(&["a", "b"]));
}

#[test]
fn split_accepts_a_regular_expression_separator() {
    assert_eq!(
        value(r#""a, b,c".split(/,\s*/)"#),
        text_list(&["a", "b", "c"])
    );
}

#[test]
fn split_zero_limit_returns_an_empty_list() {
    assert_eq!(value("\"a,b,c\".split(\",\", 0)"), text_list(&[]));
}

#[test]
fn split_negative_limit_uses_javascript_uint32_behavior() {
    assert_eq!(
        value("\"a,b,c\".split(\",\", -1)"),
        text_list(&["a", "b", "c"])
    );
}

#[test]
fn split_rejects_a_non_numeric_limit() {
    assert_eq!(
        error_message("\"a,b,c\".split(\",\", \"two\")"),
        "split: expected numeric limit"
    );
}

#[test]
fn split_rejects_a_non_text_or_regex_separator() {
    assert_eq!(
        error_message("\"a,b,c\".split(1)"),
        "split: expected text or regular expression separator"
    );
}

#[test]
fn split_rejects_no_arguments() {
    assert_eq!(
        error_message("\"a,b,c\".split()"),
        "split expected 1..=2, got 0"
    );
}

#[test]
fn split_rejects_too_many_arguments() {
    assert_eq!(
        error_message("\"a,b,c\".split(\",\", 1, 2)"),
        "split expected 1..=2, got 3"
    );
}

#[test]
fn date_parses_the_documented_space_separated_datetime() {
    assert_eq!(
        value("date(\"2026-07-08 15:04:05\").format(\"YYYY-MM-DD HH:mm:ss\")"),
        Value::Text("2026-07-08 15:04:05".into())
    );
}

#[test]
fn mixed_calendar_and_fixed_duration_clamps_month_before_adding_day() {
    assert_eq!(
        value("(date(\"2026-01-31\") + \"1M 1d\").format(\"YYYY-MM-DD\")"),
        Value::Text("2026-03-01".into())
    );
}

#[test]
fn out_of_range_calendar_duration_is_total() {
    assert_eq!(
        value(
            "(date(\"2026-01-31\") + \"999999999999999999999999999999999999M\").format(\"YYYY-MM-DD\")"
        ),
        Value::Text("2026-01-31".into())
    );
}

#[test]
fn date_time_returns_an_hh_mm_ss_string() {
    assert_eq!(
        value("date(\"2026-07-08T15:04:05\").time()"),
        Value::Text("15:04:05".into())
    );
}

#[test]
fn date_subtraction_saturates_positive_overflow() {
    assert_eq!(
        date_difference(i64::MAX, i64::MIN),
        Value::Duration(i64::MAX)
    );
}

#[test]
fn date_subtraction_saturates_negative_overflow() {
    assert_eq!(
        date_difference(i64::MIN, i64::MAX),
        Value::Duration(i64::MIN)
    );
}

#[test]
fn date_subtraction_preserves_ordinary_differences() {
    assert_eq!(date_difference(12_345, 2_345), Value::Duration(10_000));
}

#[test]
fn file_rejects_no_arguments() {
    assert_eq!(error_message("file()"), "file expected 1, got 0");
}

#[test]
fn file_rejects_multiple_arguments() {
    assert_eq!(
        error_message("file(\"a.md\", \"b.md\")"),
        "file expected 1, got 2"
    );
}

#[test]
fn html_rejects_no_arguments() {
    assert_eq!(error_message("html()"), "html expected 1, got 0");
}

#[test]
fn html_rejects_multiple_arguments() {
    assert_eq!(
        error_message("html(\"a\", \"b\")"),
        "html expected 1, got 2"
    );
}

#[test]
fn image_rejects_no_arguments() {
    assert_eq!(error_message("image()"), "image expected 1, got 0");
}

#[test]
fn image_rejects_multiple_arguments() {
    assert_eq!(
        error_message("image(\"a\", \"b\")"),
        "image expected 1, got 2"
    );
}

#[test]
fn icon_rejects_no_arguments() {
    assert_eq!(error_message("icon()"), "icon expected 1, got 0");
}

#[test]
fn icon_rejects_multiple_arguments() {
    assert_eq!(
        error_message("icon(\"a\", \"b\")"),
        "icon expected 1, got 2"
    );
}

#[test]
fn join_rejects_no_arguments() {
    assert_eq!(error_message("[1, 2].join()"), "join expected 1, got 0");
}

#[test]
fn join_rejects_multiple_arguments() {
    assert_eq!(
        error_message("[1, 2].join(\",\", \";\")"),
        "join expected 1, got 2"
    );
}

#[test]
fn flat_rejects_arguments() {
    assert_eq!(
        error_message("[[1], [2]].flat(1)"),
        "flat expected 0, got 1"
    );
}

#[test]
fn evaluates_pinned_global_functions() {
    let vault = TestVault::default();
    let warnings = WarningSink::default();
    let file = fixture_row("Projects/Alpha.md");
    let formulas = ResolvedFormulas::default();
    let ctx = ctx(&file, None, &formulas, &vault, &warnings);

    assert_eq!(
        eval_src("if(false, 1, 2)", &ctx).unwrap(),
        Value::Number(2.0)
    );
    assert_eq!(
        eval_src("today().format(\"YYYY-MM-DD\")", &ctx).unwrap(),
        Value::Text("2026-07-08".into())
    );
    assert_eq!(
        eval_src("now().format(\"HH:mm:ss\")", &ctx).unwrap(),
        Value::Text("15:04:05".into())
    );
    assert_eq!(
        eval_src("date(\"2026-01-31\") + duration(\"1M\")", &ctx).unwrap(),
        eval_src("date(\"2026-02-28\")", &ctx).unwrap()
    );
    assert_eq!(
        eval_src("date(\"2026-01-31\") + \"1M\"", &ctx).unwrap(),
        eval_src("date(\"2026-02-28\")", &ctx).unwrap()
    );
    assert_eq!(
        eval_src(
            "(date(\"2026-07-01\") + duration(\"1M\") - duration(\"1d\")).format(\"YYYY-MM-DD\")",
            &ctx
        )
        .unwrap(),
        Value::Text("2026-07-31".into())
    );
    assert_eq!(
        eval_src(
            "(date(\"2026-07-01\") + \"1M\" - \"1d\").format(\"YYYY-MM-DD\")",
            &ctx
        )
        .unwrap(),
        Value::Text("2026-07-31".into())
    );
    assert_eq!(eval_src("number(true)", &ctx).unwrap(), Value::Number(1.0));
    assert_eq!(
        eval_src("string(42)", &ctx).unwrap(),
        Value::Text("42".into())
    );
    assert_eq!(
        eval_src("escapeHTML(\"<b>&\")", &ctx).unwrap(),
        Value::Text("&lt;b&gt;&amp;".into())
    );
    assert_eq!(
        eval_src("html(\"<b>x</b>\")", &ctx).unwrap(),
        Value::Text("<b>x</b>".into())
    );
    assert_eq!(
        eval_src("list([1, 2, 3]).length", &ctx).unwrap(),
        Value::Number(3.0)
    );
    assert_eq!(eval_src("min(3, 2, 5)", &ctx).unwrap(), Value::Number(2.0));
    assert_eq!(eval_src("max(3, 2, 5)", &ctx).unwrap(), Value::Number(5.0));
    assert_eq!(
        eval_src("sum([1, 2, 3])", &ctx).unwrap(),
        Value::Number(6.0)
    );
    assert_eq!(
        eval_src("average([1, 2, 3])", &ctx).unwrap(),
        Value::Number(2.0)
    );
    assert_eq!(
        eval_src("object(\"a\", 1, \"b\", 2).keys().sort().join(\",\")", &ctx).unwrap(),
        Value::Text("a,b".into())
    );
}

#[test]
fn evaluates_methods_and_list_expressions() {
    let vault = TestVault::default();
    let warnings = WarningSink::default();
    let file = fixture_row("Projects/Alpha.md");
    let formulas = ResolvedFormulas::default();
    let ctx = ctx(&file, None, &formulas, &vault, &warnings);

    assert_eq!(
        eval_src("\"  Hello \".trim().lower().contains(\"ell\")", &ctx).unwrap(),
        Value::Bool(true)
    );
    assert_eq!(
        eval_src("\"ab\".repeat(3)", &ctx).unwrap(),
        Value::Text("ababab".into())
    );
    assert_eq!(
        eval_src("\"a1 a2\".replace(/a(\\d)/g, \"b$1\")", &ctx).unwrap(),
        Value::Text("b1 b2".into())
    );
    assert_eq!(
        eval_src("\"FOO\".replace(rx_i, \"bar\")", &ctx).unwrap(),
        Value::Text("bar".into())
    );
    assert_eq!(
        eval_src(
            "[1, 2, 3].filter(value > 1).map(value * 2).join(\"|\")",
            &ctx
        )
        .unwrap(),
        Value::Text("4|6".into())
    );
    assert_eq!(
        eval_src("[1, 2, 3].reduce(acc + value, 0)", &ctx).unwrap(),
        Value::Number(6.0)
    );
    assert_eq!(
        eval_src("/a2/.matches(\"a2\")", &ctx).unwrap(),
        Value::Bool(true)
    );
    assert_eq!(
        eval_src("rx_i.matches(\"FOO\")", &ctx).unwrap(),
        Value::Bool(true)
    );
    assert_eq!(
        eval_src("rx_m.matches(\"a\\nb\")", &ctx).unwrap(),
        Value::Bool(true)
    );
    assert_eq!(
        eval_src("rx_s.matches(\"a\\nb\")", &ctx).unwrap(),
        Value::Bool(true)
    );
    assert_eq!(eval_src("[1, 2][-1]", &ctx).unwrap(), Value::Null);
    assert_eq!(eval_src("\"ab\"[-1]", &ctx).unwrap(), Value::Null);
    assert_eq!(eval_src("[1][9999999999999]", &ctx).unwrap(), Value::Null);
}

#[test]
fn file_and_link_methods_use_vault_lookup() {
    let mut vault = TestVault::default();
    vault
        .resolved
        .insert("Beta".into(), "Projects/Beta.md".into());
    vault.links.insert(
        "Projects/Alpha.md".into(),
        vec![LinkValue {
            target: "Beta".into(),
            display: None,
            resolved_path: Some("Projects/Beta.md".into()),
        }],
    );
    let warnings = WarningSink::default();
    let file = fixture_row("Projects/Alpha.md");
    let formulas = ResolvedFormulas::default();
    let ctx = ctx(&file, None, &formulas, &vault, &warnings);

    assert_eq!(
        eval_src("file.hasTag(\"project\")", &ctx).unwrap(),
        Value::Bool(true)
    );
    assert_eq!(
        eval_src("file.inFolder(\"Projects\")", &ctx).unwrap(),
        Value::Bool(true)
    );
    assert_eq!(
        eval_src("file.asLink().linksTo(link(\"Beta\"))", &ctx).unwrap(),
        Value::Bool(true)
    );
    assert_eq!(
        eval_src("file.asLink(\"Alpha\").toString()", &ctx).unwrap(),
        Value::Text("[[Projects/Alpha.md|Alpha]]".into())
    );
}

proptest! {
    #[test]
    fn eval_is_total_and_deterministic_for_generated_supported_exprs(source in eval_expr_source()) {
        let vault = TestVault::default();
        let file = fixture_row("Projects/Alpha.md");
        let formulas = ResolvedFormulas::default();
        let expr = parse_expr(&source).expect(&source);

        let warnings_a = WarningSink::default();
        let ctx_a = ctx(&file, None, &formulas, &vault, &warnings_a);
        let warnings_b = WarningSink::default();
        let ctx_b = ctx(&file, None, &formulas, &vault, &warnings_b);

        prop_assert_eq!(eval(&expr, &ctx_a), eval(&expr, &ctx_b));
    }
}

fn eval_src(source: &str, ctx: &EvalCtx<'_>) -> Result<Value, EvalError> {
    let expr = parse_expr(source).expect(source);
    eval(&expr, ctx)
}

fn value(source: &str) -> Value {
    let vault = TestVault::default();
    let warnings = WarningSink::default();
    let file = fixture_row("Projects/Alpha.md");
    let formulas = ResolvedFormulas::default();
    eval_src(source, &ctx(&file, None, &formulas, &vault, &warnings)).unwrap()
}

fn date_difference(lhs_ms: i64, rhs_ms: i64) -> Value {
    let vault = TestVault::default();
    let warnings = WarningSink::default();
    let file = fixture_row("Projects/Alpha.md");
    let formulas = ResolvedFormulas::from([
        (
            "lhs".to_string(),
            Value::Date(DateValue {
                epoch_ms: lhs_ms,
                has_time: true,
            }),
        ),
        (
            "rhs".to_string(),
            Value::Date(DateValue {
                epoch_ms: rhs_ms,
                has_time: true,
            }),
        ),
    ]);
    eval_src(
        "formula.lhs - formula.rhs",
        &ctx(&file, None, &formulas, &vault, &warnings),
    )
    .unwrap()
}

fn error_message(source: &str) -> String {
    let vault = TestVault::default();
    let warnings = WarningSink::default();
    let file = fixture_row("Projects/Alpha.md");
    let formulas = ResolvedFormulas::default();
    eval_src(source, &ctx(&file, None, &formulas, &vault, &warnings))
        .unwrap_err()
        .to_string()
}

fn text_list(values: &[&str]) -> Value {
    Value::List(
        values
            .iter()
            .map(|value| Value::Text((*value).to_string()))
            .collect(),
    )
}

fn eval_expr_source() -> impl Strategy<Value = String> {
    let leaf = prop_oneof![
        Just("rating".to_string()),
        Just("missing".to_string()),
        Just("file.size".to_string()),
        Just("file.name".to_string()),
        Just("true".to_string()),
        Just("false".to_string()),
        (-20i32..20).prop_map(|n| n.to_string()),
        "[0-9a-z]{0,8}".prop_map(|s| format!("{s:?}")),
    ];

    leaf.prop_recursive(3, 24, 3, |inner| {
        prop_oneof![
            (inner.clone(), inner.clone()).prop_map(|(a, b)| format!("({a} + {b})")),
            (inner.clone(), inner.clone()).prop_map(|(a, b)| format!("({a} > {b})")),
            (inner.clone(), inner.clone()).prop_map(|(a, b)| format!("({a} == {b})")),
            inner.clone().prop_map(|a| format!("if({a}, 1, 0)")),
            inner.clone().prop_map(|a| format!("string({a})")),
            inner.prop_map(|a| format!("list({a}).join(\",\")")),
        ]
    })
}

fn ctx<'a>(
    file: &'a RowContext,
    this: Option<&'a RowContext>,
    formulas: &'a ResolvedFormulas,
    vault: &'a dyn VaultLookup,
    warnings: &'a WarningSink,
) -> EvalCtx<'a> {
    EvalCtx {
        file,
        this,
        formulas,
        now_ms: 1_783_523_045_000,
        vault,
        warnings,
        filter_position: false,
    }
}

fn fixture_row(path: &str) -> RowContext {
    let mut file_fields = FileFields::for_path(path);
    file_fields.size = 123;
    file_fields.ctime = Some(DateValue {
        epoch_ms: 1_700_000_000_000,
        has_time: true,
    });
    file_fields.mtime = Some(DateValue {
        epoch_ms: 1_783_523_045_000,
        has_time: true,
    });
    file_fields.tags = vec!["project".into(), "project/rust".into()];
    file_fields.aliases = vec!["A".into()];
    file_fields.in_degree = 2;
    file_fields.out_degree = 1;
    RowContext {
        file_path: path.to_string(),
        file_fields,
        properties: vec![
            ("rating".into(), Value::Number(4.5)),
            ("status".into(), Value::Text("Active".into())),
            (
                "tags".into(),
                Value::List(vec![Value::Text("project".into())]),
            ),
            ("rx_i".into(), Value::Regex("foo".into(), "i".into())),
            ("rx_m".into(), Value::Regex("^b$".into(), "m".into())),
            ("rx_s".into(), Value::Regex("a.b".into(), "s".into())),
        ],
        task: None,
    }
}

fn assert_number(value: Value, expected: f64) {
    let Value::Number(actual) = value else {
        panic!("expected number, got {value:?}");
    };
    assert!((actual - expected).abs() < 1e-9, "{actual} != {expected}");
}

#[derive(Default)]
struct TestVault {
    resolved: BTreeMap<String, String>,
    links: BTreeMap<String, Vec<LinkValue>>,
}

impl VaultLookup for TestVault {
    fn resolve_link(&self, target: &str) -> Option<String> {
        self.resolved.get(target).cloned()
    }

    fn links_for(&self, path: &str) -> Vec<LinkValue> {
        self.links.get(path).cloned().unwrap_or_default()
    }
}
