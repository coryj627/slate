// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

use slate_core::bases::expr::{
    BinaryOp, Callee, ExprKind, FileField, GlobalFn, ListExprKind, Lit, MethodName, PropertyRef,
    UnaryOp, parse_expr,
};

#[test]
fn parses_precedence_and_verbatim_spans() {
    let source = "price + 2 * file.size >= 10 && !done";
    let expr = parse_expr(source).expect("expression parses");

    assert_eq!(
        &source[expr.span.start as usize..expr.span.end as usize],
        source
    );
    let ExprKind::Binary {
        op: BinaryOp::And,
        lhs,
        rhs,
    } = &expr.kind
    else {
        panic!("expected top-level &&, got {expr:#?}");
    };

    assert!(matches!(
        rhs.kind,
        ExprKind::Unary {
            op: UnaryOp::Not,
            ..
        }
    ));

    let ExprKind::Binary {
        op: BinaryOp::Gte,
        lhs: comparison_lhs,
        ..
    } = &lhs.kind
    else {
        panic!("expected >= below &&, got {lhs:#?}");
    };

    let ExprKind::Binary {
        op: BinaryOp::Add,
        rhs: add_rhs,
        ..
    } = &comparison_lhs.kind
    else {
        panic!("expected + below >=, got {comparison_lhs:#?}");
    };

    assert!(matches!(
        add_rhs.kind,
        ExprKind::Binary {
            op: BinaryOp::Mul,
            ..
        }
    ));
}

#[test]
fn parenthesized_expression_span_includes_delimiters() {
    let source = "a * (b + c)";
    let expr = parse_expr(source).expect("grouped expression parses");

    assert_eq!(
        &source[expr.span.start as usize..expr.span.end as usize],
        source
    );
}

#[test]
fn parses_subtraction_as_operator_not_hyphenated_identifier() {
    let expr = parse_expr("a-b").expect("subtraction parses");

    assert!(matches!(
        expr.kind,
        ExprKind::Binary {
            op: BinaryOp::Sub,
            ..
        }
    ));
}

#[test]
fn parses_namespaces_calls_and_this_file_fields() {
    let expr =
        parse_expr(r#"file.hasTag("project") && formula.ppu > 5 && this.file.path == file.path"#)
            .expect("expression parses");

    let ExprKind::Binary {
        op: BinaryOp::And,
        lhs,
        rhs,
    } = &expr.kind
    else {
        panic!("expected right-associative && chain, got {expr:#?}");
    };

    assert!(matches!(
        lhs.kind,
        ExprKind::Binary {
            op: BinaryOp::And,
            ..
        }
    ));

    let ExprKind::Binary {
        op: BinaryOp::Eq,
        lhs: eq_lhs,
        rhs: eq_rhs,
    } = &rhs.kind
    else {
        panic!("expected equality on this.file.path, got {rhs:#?}");
    };

    assert!(matches!(
        eq_lhs.kind,
        ExprKind::Prop(PropertyRef::ThisFile(FileField::Path))
    ));
    assert!(matches!(
        eq_rhs.kind,
        ExprKind::Prop(PropertyRef::File(FileField::Path))
    ));

    let backlinks = parse_expr("file.hasLink(this.file)").expect("this.file file handle parses");
    let ExprKind::Call { args, .. } = &backlinks.kind else {
        panic!("expected hasLink call, got {backlinks:#?}");
    };
    assert!(matches!(args[0].kind, ExprKind::Prop(PropertyRef::This)));
}

#[test]
fn parses_list_expression_implicit_bindings_before_note_properties() {
    let expr = parse_expr(
        r#"list(tags).filter(value.contains("a")).map(value.lower()).reduce(acc + value, "")"#,
    )
    .expect("expression parses");

    let ExprKind::ListExpr {
        kind: ListExprKind::Reduce,
        body,
        init: Some(init),
        ..
    } = &expr.kind
    else {
        panic!("expected reduce list expression, got {expr:#?}");
    };

    assert!(matches!(init.kind, ExprKind::Lit(Lit::String(_))));
    let ExprKind::Binary {
        op: BinaryOp::Add,
        lhs,
        rhs,
    } = &body.kind
    else {
        panic!("expected acc + value reduce body, got {body:#?}");
    };
    assert!(matches!(lhs.kind, ExprKind::Prop(PropertyRef::ImplicitAcc)));
    assert!(matches!(
        rhs.kind,
        ExprKind::Prop(PropertyRef::ImplicitValue)
    ));
}

#[test]
fn parses_unknown_function_as_unsupported_not_parse_error() {
    let expr = parse_expr("mystery(price)").expect("unknown function is preserved");

    let ExprKind::Unsupported { raw, reason } = expr.kind else {
        panic!("expected unsupported node, got {expr:#?}");
    };

    assert_eq!(raw, "mystery(price)");
    assert!(reason.contains("unknown function"));
}

#[test]
fn distinguishes_regex_literals_from_division() {
    let regex = parse_expr("name.replace(/a/g, \"b\")").expect("regex literal parses");
    let division = parse_expr("a / b / c").expect("division parses");

    let ExprKind::Call {
        callee: Callee::Method { name, .. },
        args,
    } = regex.kind
    else {
        panic!("expected replace method call, got {regex:#?}");
    };
    assert_eq!(name, MethodName::Replace);
    assert!(matches!(args[0].kind, ExprKind::Lit(Lit::Regex { .. })));

    assert!(matches!(
        division.kind,
        ExprKind::Binary {
            op: BinaryOp::Div,
            ..
        }
    ));
}

#[test]
fn string_and_regex_literals_preserve_unicode() {
    let string_expr = parse_expr(r#""café""#).expect("unicode string parses");
    assert!(matches!(
        string_expr.kind,
        ExprKind::Lit(Lit::String(ref value)) if value == "café"
    ));

    let regex_expr = parse_expr(r#"name.matches(/café/)"#).expect("unicode regex parses");
    let ExprKind::Call { args, .. } = regex_expr.kind else {
        panic!("expected regex method call, got {regex_expr:#?}");
    };
    assert!(matches!(
        args[0].kind,
        ExprKind::Lit(Lit::Regex { ref pattern, .. }) if pattern == "café"
    ));
}

#[test]
fn unsupported_regex_flags_preserve_raw_literal() {
    let expr = parse_expr("name.matches(/abc/i)").expect("unsupported regex flag preserves");

    let ExprKind::Call { args, .. } = expr.kind else {
        panic!("expected regex method call, got {expr:#?}");
    };
    let ExprKind::Unsupported { raw, reason } = &args[0].kind else {
        panic!("expected unsupported regex argument, got {:#?}", args[0]);
    };
    assert_eq!(raw, "/abc/i");
    assert!(reason.contains("unsupported regex flag"));
}

#[test]
fn recognizes_every_pinned_global_function_name() {
    for (name, expected) in [
        ("date", GlobalFn::Date),
        ("duration", GlobalFn::Duration),
        ("escapeHTML", GlobalFn::EscapeHtml),
        ("file", GlobalFn::File),
        ("html", GlobalFn::Html),
        ("icon", GlobalFn::Icon),
        ("if", GlobalFn::If),
        ("image", GlobalFn::Image),
        ("link", GlobalFn::Link),
        ("list", GlobalFn::List),
        ("max", GlobalFn::Max),
        ("min", GlobalFn::Min),
        ("now", GlobalFn::Now),
        ("number", GlobalFn::Number),
        ("object", GlobalFn::Object),
        ("random", GlobalFn::Random),
        ("string", GlobalFn::String),
        ("sum", GlobalFn::Sum),
        ("average", GlobalFn::Average),
        ("today", GlobalFn::Today),
    ] {
        let expr = parse_expr(&format!("{name}()")).expect("known global parses");
        let ExprKind::Call {
            callee: Callee::Global(actual),
            ..
        } = expr.kind
        else {
            panic!("expected global call for {name}, got {expr:#?}");
        };
        assert_eq!(actual, expected);
    }
}

#[test]
fn parses_bracketed_properties_degree_fields_and_computed_fields() {
    let expr =
        parse_expr(
            r#"note["price"] + file.inDegree + file.outDegree + file.aliases + date("2024-12-01").year"#,
        )
        .expect("expression parses");

    let ExprKind::Binary {
        op: BinaryOp::Add,
        rhs: date_field,
        ..
    } = &expr.kind
    else {
        panic!("expected add chain, got {expr:#?}");
    };
    assert!(matches!(
        date_field.kind,
        ExprKind::Field { ref name, .. } if name == "year"
    ));

    let note = parse_expr(r#"note["price"]"#).expect("bracket property parses");
    assert!(matches!(
        note.kind,
        ExprKind::Prop(PropertyRef::Note(ref key)) if key == "price"
    ));

    let in_degree = parse_expr("file.inDegree").expect("degree parses");
    assert!(matches!(
        in_degree.kind,
        ExprKind::Prop(PropertyRef::File(FileField::InDegree))
    ));
}

#[test]
fn parses_object_list_index_literals() {
    let expr = parse_expr(r#"{"a": [1, true, "x"]}.a[0]"#).expect("object/list/index parses");

    let ExprKind::Index { base, index } = &expr.kind else {
        panic!("expected top-level index, got {expr:#?}");
    };
    assert!(matches!(index.kind, ExprKind::Lit(Lit::Number(0.0))));
    assert!(matches!(
        base.kind,
        ExprKind::Field { ref name, .. } if name == "a"
    ));
}

#[test]
fn unknown_method_call_is_preserved_as_unsupported() {
    let expr = parse_expr("title.mystery()").expect("unknown method is preserved");

    let ExprKind::Unsupported { raw, reason } = expr.kind else {
        panic!("expected unsupported node, got {expr:#?}");
    };
    assert_eq!(raw, "title.mystery()");
    assert!(reason.contains("unknown method"));
}

#[test]
fn unknown_file_field_is_preserved_as_unsupported() {
    let expr = parse_expr("file.magic").expect("unknown field is preserved");

    let ExprKind::Unsupported { raw, reason } = expr.kind else {
        panic!("expected unsupported node, got {expr:#?}");
    };
    assert_eq!(raw, "file.magic");
    assert!(reason.contains("unknown file field"));
}

#[test]
fn unknown_file_method_call_is_preserved_as_unsupported_call() {
    let expr = parse_expr("file.magic()").expect("unknown file method is preserved");

    let ExprKind::Unsupported { raw, reason } = expr.kind else {
        panic!("expected unsupported node, got {expr:#?}");
    };
    assert_eq!(raw, "file.magic()");
    assert!(reason.contains("unknown method"));
}
