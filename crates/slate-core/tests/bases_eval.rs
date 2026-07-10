// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

use std::collections::BTreeMap;

use proptest::prelude::*;
use slate_core::bases::eval::{
    DateValue, DqlDateValue, EvalCtx, EvalError, FileFields, LinkValue, ResolvedFormulas,
    RowContext, Value, VaultLookup, WarningSink, eval,
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
        eval_src("today().format(\"Q\")", &ctx),
        Err(EvalError::UnsupportedFormatToken { .. })
    ));
}

#[test]
fn date_format_supports_iso_weekdays() {
    assert_eq!(
        value(r#"date("2026-07-06").format("E")"#),
        Value::Text("1".to_string())
    );
    assert_eq!(
        value(r#"date("2026-07-08").format("E")"#),
        Value::Text("3".to_string())
    );
    assert_eq!(
        value(r#"date("2026-07-12").format("E")"#),
        Value::Text("7".to_string())
    );
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
fn mixed_calendar_and_fixed_duration_literals_clamp_before_adding_day() {
    assert_eq!(
        value("(date(\"2026-01-31\") + \"1M 1d\").format(\"YYYY-MM-DD\")"),
        Value::Text("2026-03-01".into())
    );
    assert_eq!(
        value("(date(\"2026-01-31\") + duration(\"1M 1d\")).format(\"YYYY-MM-DD\")"),
        Value::Text("2026-03-01".into())
    );
    assert_eq!(value("duration(\"1M 1d\")"), Value::Duration(2_678_400_000));
}

#[test]
fn duration_values_outside_literal_date_boundary_are_fixed_milliseconds() {
    assert_eq!(value("duration(\"1M\")"), Value::Duration(2_592_000_000));
    assert_eq!(
        value(
            "(date(\"2026-01-31\") + if(true, duration(\"1M\"), duration(\"0d\"))).format(\"YYYY-MM-DD\")"
        ),
        Value::Text("2026-03-02".into())
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
fn flat_rejects_more_than_one_depth_argument() {
    assert_eq!(
        error_message("[[1], [2]].flat(1, 2)"),
        "flat expected 0..=1, got 2"
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
fn dql_runtime_markers_preserve_javascript_semantics() {
    let vault = TestVault::default();
    let warnings = WarningSink::default();
    let file = fixture_row("Projects/Alpha.md");
    let formulas = ResolvedFormulas::default();
    let ctx = ctx(&file, None, &formulas, &vault, &warnings);

    assert_eq!(
        eval_src(
            r#"number(object("slate.dql.number", "abc -12.5 xyz"))"#,
            &ctx,
        )
        .unwrap(),
        Value::Number(-12.5)
    );
    assert_eq!(
        eval_src(r#"object("slate.dql.length", "😀").length"#, &ctx).unwrap(),
        Value::Number(2.0)
    );
    assert_eq!(
        eval_src(
            r#"object("slate.dql.length", object("a", 1, "b", 2)).length"#,
            &ctx,
        )
        .unwrap(),
        Value::Number(2.0)
    );
    assert_eq!(
        eval_src(r#"object("slate.dql.length", null).length"#, &ctx).unwrap(),
        Value::Number(0.0)
    );
    assert_eq!(
        eval_src(
            r#""ab".replace(object("slate.dql.literal-replace", ""), "-")"#,
            &ctx,
        )
        .unwrap(),
        Value::Text("a-b".into())
    );
    assert_eq!(
        eval_src(
            r#"object("slate.dql.substring", "abcd").slice(3, 1)"#,
            &ctx,
        )
        .unwrap(),
        Value::Text("bc".into())
    );
    assert_eq!(
        eval_src("(-1.5).round()", &ctx).unwrap(),
        Value::Number(-1.0)
    );
    assert_eq!(
        eval_src("(-1.25).round(1)", &ctx).unwrap(),
        Value::Number(-1.3)
    );
    assert_eq!(
        eval_src("123.4.round(-1)", &ctx).unwrap(),
        Value::Number(123.0)
    );
    assert_eq!(eval_src("2.55.round(1)", &ctx).unwrap(), Value::Number(2.5));
    assert_eq!(
        eval_src(r#"number(object("slate.dql.number", ".5"))"#, &ctx).unwrap(),
        Value::Number(5.0)
    );
    assert_eq!(
        eval_src(r#"number(object("slate.dql.number", "1e2"))"#, &ctx,).unwrap(),
        Value::Number(1.0)
    );

    assert_eq!(
        eval_src(r#"sum(object("slate.dql.list-aggregate", [1, 2]))"#, &ctx,).unwrap(),
        Value::Number(3.0)
    );
    assert_eq!(
        eval_src(
            r#"min(object("slate.dql.list-aggregate", ["b", "a"]))"#,
            &ctx,
        )
        .unwrap(),
        Value::Text("a".into())
    );
    assert_eq!(
        eval_src(r#"min(object("slate.dql.list-aggregate", [2, 10]))"#, &ctx,).unwrap(),
        Value::Number(2.0)
    );
    assert_eq!(
        eval_src(r#"max(object("slate.dql.list-aggregate", [2, 10]))"#, &ctx,).unwrap(),
        Value::Number(10.0)
    );
    assert_eq!(
        eval_src(
            r#"min(object("slate.dql.list-aggregate", [null, 5]))"#,
            &ctx,
        )
        .unwrap(),
        Value::Number(5.0)
    );
    assert_eq!(
        eval_src(
            r#"max(object("slate.dql.list-aggregate", [5, null]))"#,
            &ctx,
        )
        .unwrap(),
        Value::Number(5.0)
    );
    assert_eq!(
        eval_src(r#"min(object("slate.dql.list-aggregate", [null]))"#, &ctx,).unwrap(),
        Value::Null
    );
    assert_eq!(
        eval_src(
            r#"max(object("slate.dql.list-aggregate", [date("2026-07-01"), date("2026-07-02")]))"#,
            &ctx,
        )
        .unwrap(),
        eval_src(r#"date("2026-07-02")"#, &ctx).unwrap()
    );
    assert!(
        eval_src(r#"sum(object("slate.dql.list-aggregate", 1))"#, &ctx,)
            .unwrap_err()
            .to_string()
            .contains("DQL aggregate requires a list")
    );
    assert_eq!(
        eval_src(r#"object("slate.dql.join", [1, 2]).join(",")"#, &ctx,).unwrap(),
        Value::Text("1,2".into())
    );
    assert!(
        eval_src(r#"object("slate.dql.list-method", 1).flat()"#, &ctx,)
            .unwrap_err()
            .to_string()
            .contains("DQL flat requires a list")
    );
    assert_eq!(
        eval_src(
            r#"object("slate.dql.list-method", [1, 2]).filter(value > 1)"#,
            &ctx,
        )
        .unwrap(),
        Value::List(vec![Value::Number(2.0)])
    );
    assert!(
        eval_src(r#"object("slate.dql.list-method", 1).map(value)"#, &ctx,)
            .unwrap_err()
            .to_string()
            .contains("DQL map requires a list")
    );
    assert_eq!(
        eval_src(
            r#"object("slate.dql.list-method", [2, 1, 2]).unique()"#,
            &ctx,
        )
        .unwrap(),
        Value::List(vec![Value::Number(1.0), Value::Number(2.0)])
    );
    assert!(
        eval_src(
            r#"object("slate.dql.list-method", "abc").slice(0, 1)"#,
            &ctx,
        )
        .unwrap_err()
        .to_string()
        .contains("DQL slice requires a list")
    );
    assert_eq!(
        eval_src(
            r#"object("slate.dql.list-method", [1, 2, 3]).slice(-1)"#,
            &ctx,
        )
        .unwrap(),
        Value::List(vec![Value::Number(3.0)])
    );
    assert_eq!(
        eval_src(
            r#"object("slate.dql.list-method", [1, 2, 3]).slice(0, -1)"#,
            &ctx,
        )
        .unwrap(),
        Value::List(vec![Value::Number(1.0), Value::Number(2.0)])
    );
    assert_eq!(
        eval_src("[2, 10].sort()", &ctx).unwrap(),
        Value::List(vec![Value::Number(2.0), Value::Number(10.0)])
    );
    assert_eq!(
        eval_src(r#"[["a, b"], ["a", "b"]].unique()"#, &ctx).unwrap(),
        Value::List(vec![
            Value::List(vec![Value::Text("a, b".into())]),
            Value::List(vec![Value::Text("a".into()), Value::Text("b".into()),]),
        ])
    );
    assert_eq!(
        eval_src(
            r#"object("slate.dql.contains", [[[1]]]).contains(1)"#,
            &ctx,
        )
        .unwrap(),
        Value::Bool(true)
    );
    assert_eq!(
        eval_src(r#"object("slate.dql.contains", 1).contains(1)"#, &ctx,).unwrap(),
        Value::Bool(true)
    );
    assert_eq!(
        eval_src(
            r#"object("slate.dql.contains", object("a", 1)).contains(object("a", 1))"#,
            &ctx,
        )
        .unwrap(),
        Value::Bool(true)
    );
    assert_eq!(
        eval_src(
            r#"object("slate.dql.contains", [[1]]).contains([1])"#,
            &ctx,
        )
        .unwrap(),
        Value::Bool(false)
    );
    assert_eq!(
        eval_src("[[[1]]].contains(1)", &ctx).unwrap(),
        Value::Bool(false)
    );
    assert_eq!(
        eval_src(r#"object("slate.dql.reverse", "aé").reverse()"#, &ctx,).unwrap(),
        Value::Text("éa".into())
    );
    assert_eq!(
        eval_src(r#"object("slate.dql.reverse", 7).reverse()"#, &ctx,).unwrap(),
        Value::Number(7.0)
    );
    assert!(
        eval_src(r#"object("slate.dql.reverse", "😀").reverse()"#, &ctx,)
            .unwrap_err()
            .to_string()
            .contains("UTF-16 surrogate")
    );
}

#[test]
fn dql_date_only_bridge_preserves_calendar_days_in_non_utc_provenance() {
    let vault = TestVault::default();
    let warnings = WarningSink::default();
    let file = fixture_row("Projects/Alpha.md");
    let native_date = Value::Date(DateValue {
        epoch_ms: 1_783_641_600_000,
        has_time: false,
    });
    let native_datetime = Value::Date(DateValue {
        epoch_ms: 1_783_641_600_000,
        has_time: true,
    });
    let dql_date = Value::DqlDate(DqlDateValue {
        epoch_ms: 1_783_656_000_000,
        has_time: false,
        offset_minutes: -240,
        is_local: false,
    });
    let dql_next = Value::DqlDate(DqlDateValue {
        epoch_ms: 1_783_742_400_000,
        has_time: false,
        offset_minutes: -240,
        is_local: false,
    });
    let formulas = ResolvedFormulas::from([
        ("native_date".to_string(), native_date.clone()),
        ("native_datetime".to_string(), native_datetime),
        ("dql_date".to_string(), dql_date.clone()),
        ("dql_next".to_string(), dql_next.clone()),
    ]);
    let ctx = ctx(&file, None, &formulas, &vault, &warnings);

    assert_eq!(
        eval_src(
            r#"object("slate.dql.equality", formula.native_date) == formula.dql_date"#,
            &ctx,
        )
        .unwrap(),
        Value::Bool(true)
    );
    assert_eq!(
        eval_src(
            r#"object("slate.dql.ordering", formula.native_date) >= formula.dql_date"#,
            &ctx,
        )
        .unwrap(),
        Value::Bool(true)
    );
    assert_eq!(
        eval_src(
            r#"object("slate.dql.ordering", formula.native_datetime) < formula.dql_date"#,
            &ctx,
        )
        .unwrap(),
        Value::Bool(true),
        "date-times remain instant-based"
    );
    assert_eq!(
        eval_src(
            r#"object("slate.dql.sort", [formula.dql_next, formula.native_date, formula.dql_date]).sort()"#,
            &ctx,
        )
        .unwrap(),
        Value::List(vec![native_date, dql_date, dql_next])
    );

    let converted = eval_src(
        r#"date(object("slate.dql.date", formula.native_date))"#,
        &ctx,
    )
    .unwrap();
    let Value::DqlDate(converted) = converted else {
        panic!("date(native date-only) must produce a DQL date")
    };
    let local_coordinate_ms =
        converted.epoch_ms + i64::from(converted.offset_minutes).saturating_mul(60_000);
    assert!(
        (1_783_641_600_000..1_783_728_000_000).contains(&local_coordinate_ms),
        "coercion must retain the authored 2026-07-10 calendar day"
    );
    assert!(!converted.has_time);
}

#[test]
fn dql_regex_markers_use_javascript_replacement_and_character_classes() {
    let vault = TestVault::default();
    let warnings = WarningSink::default();
    let file = fixture_row("Projects/Alpha.md");
    let formulas = ResolvedFormulas::default();
    let ctx = ctx(&file, None, &formulas, &vault, &warnings);

    let regex = |pattern: &str, mode: &str| {
        format!("object(\"slate.dql.regex\", {pattern:?}, \"slate.dql.regex.mode\", {mode:?})")
    };

    assert_eq!(
        eval_src(
            &format!(r#""a".replace({}, "$<x>")"#, regex("a", "global")),
            &ctx,
        )
        .unwrap(),
        Value::Text("$<x>".into())
    );
    assert_eq!(
        eval_src(
            &format!(r#""a".replace({}, "$<x>")"#, regex("(?<x>a)", "global")),
            &ctx,
        )
        .unwrap(),
        Value::Text("a".into())
    );
    assert_eq!(
        eval_src(
            &format!(r#""a".replace({}, "$01")"#, regex("(a)", "global")),
            &ctx,
        )
        .unwrap(),
        Value::Text("a".into())
    );
    assert_eq!(
        eval_src(
            &format!(r#""a".replace({}, "$0")"#, regex("a", "global")),
            &ctx,
        )
        .unwrap(),
        Value::Text("$0".into())
    );
    assert_eq!(
        eval_src(&format!(r#"{}.matches("é")"#, regex(r"\w", "search")), &ctx).unwrap(),
        Value::Bool(false)
    );
    assert_eq!(
        eval_src(&format!(r#"{}.matches("a")"#, regex(r"\w", "search")), &ctx).unwrap(),
        Value::Bool(true)
    );
    assert_eq!(
        eval_src(&format!(r#"{}.matches("١")"#, regex(r"\d", "search")), &ctx).unwrap(),
        Value::Bool(false)
    );
    for pattern in [
        r"\W", r"\s", r"\p{L}", r"\Afoo\z", r"\G", r"\u0061", ".", "é", "[^a]", "(?P<x>a)",
        "(?i)a", "(?R)a", "(?-i:a)", "[a&&b]", "[[a]]",
    ] {
        assert!(matches!(
            eval_src(
                &format!(r#"{}.matches("a")"#, regex(pattern, "search")),
                &ctx,
            ),
            Err(EvalError::InvalidArgument { .. })
        ));
    }
    assert!(matches!(
        eval_src(&format!(r#""😀".split({})"#, regex("", "search")), &ctx),
        Err(EvalError::InvalidArgument { .. })
    ));
    assert_eq!(
        eval_src(&format!(r#""é".split({})"#, regex("", "search")), &ctx).unwrap(),
        Value::List(vec![Value::Text("é".into())])
    );
    assert_eq!(
        eval_src(&format!(r#""ab".split({})"#, regex("a*", "search")), &ctx).unwrap(),
        Value::List(vec![Value::Text(String::new()), Value::Text("b".into())])
    );
    assert!(matches!(
        eval_src(&format!(r#""😀".split({})"#, regex("a*", "search")), &ctx),
        Err(EvalError::InvalidArgument { .. })
    ));
    assert!(matches!(
        eval_src(
            &format!(r#""😀".replace({}, "x")"#, regex("a*", "global")),
            &ctx,
        ),
        Err(EvalError::InvalidArgument { .. })
    ));
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
            subpath: None,
            link_type: "file".into(),
            embed: false,
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
