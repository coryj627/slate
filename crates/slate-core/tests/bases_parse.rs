// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

use slate_core::bases::expr::{BinaryOp, ExprKind, FileField, PropertyRef};
use slate_core::bases::{
    BaseWarningKind, FilterNode, RowSource, SummaryRef, ViewSpec, ViewType, parse_base, view_query,
};

#[test]
fn parses_brief_example_and_derives_view_query() {
    let source = include_str!("fixtures/bases/brief_example.base");
    let (base, warnings) = parse_base(source);

    assert_eq!(base.raw, source);
    assert!(
        warnings.is_empty(),
        "brief example should parse without warnings: {warnings:#?}"
    );
    let FilterNode::Or(base_filters) = base.filters.as_ref().expect("base filters") else {
        panic!("expected top-level OR filters, got {:#?}", base.filters);
    };
    assert_eq!(base_filters.len(), 3);
    assert!(
        base.spans
            .top_level
            .iter()
            .any(|r| r.name == "filters" && r.region.text.starts_with("filters:\n  or:"))
    );
    assert!(
        base.spans.formulas.iter().any(
            |r| r.name == "ppu" && r.region.text.contains(r#"ppu: "(price / age).toFixed(2)""#)
        )
    );
    assert!(base.spans.filters.len() >= 6);
    assert_eq!(base.spans.views.len(), 1);
    assert!(
        base.spans.views[0]
            .entry
            .text
            .contains(r#"name: "My table""#)
    );
    assert!(
        base.spans.views[0]
            .keys
            .iter()
            .any(|r| r.name == "groupBy" && r.region.text.contains("direction: DESC"))
    );
    assert_eq!(
        base.formulas
            .iter()
            .map(|(name, _)| name.as_str())
            .collect::<Vec<_>>(),
        ["formatted_price", "ppu"]
    );
    assert_eq!(
        base.properties
            .iter()
            .map(|(id, config)| (id.as_str(), config.display_name.as_deref()))
            .collect::<Vec<_>>(),
        [
            ("status", Some("Status")),
            ("formula.formatted_price", Some("Price")),
            ("file.ext", Some("Extension")),
        ]
    );

    let view = &base.views[0];
    assert_eq!(view.view_type, ViewType::Table);
    assert_eq!(view.name, "My table");
    assert_eq!(view.limit, Some(10));
    assert!(matches!(
        view.group_by,
        Some(ref group) if group.property == PropertyRef::Note("age".to_string()) && !group.ascending
    ));
    assert_eq!(
        view.order,
        [
            "file.name",
            "file.ext",
            "note.age",
            "formula.ppu",
            "formula.formatted_price",
        ]
    );

    let query = view_query(&base, 0);
    assert_eq!(query.row_source, RowSource::Files);
    assert_eq!(query.limit, Some(10));
    assert!(matches!(
        query.view,
        ViewSpec::Table {
            fallback_from: None
        }
    ));
    assert!(matches!(query.filters, Some(FilterNode::And(ref nodes)) if nodes.len() == 2));
    assert_eq!(
        query
            .columns
            .iter()
            .map(|column| (column.id.as_str(), column.display_name.as_deref()))
            .collect::<Vec<_>>(),
        [
            ("file.name", None),
            ("file.ext", Some("Extension")),
            ("note.age", None),
            ("formula.ppu", None),
            ("formula.formatted_price", Some("Price")),
        ]
    );
    assert_eq!(
        query.summaries,
        vec![(
            "formula.ppu".to_string(),
            SummaryRef::Builtin("Average".to_string())
        )]
    );
}

#[test]
fn preserves_unknown_keys_plugin_views_and_slate_state() {
    let source = r#"customTop:
  keep: me
properties:
  status:
    displayName: Status
    width: 120
views:
  - type: cards
    name: Cards
    rowHeight: 44
    source: mystery
    slate:
      selected: true
"#;
    let (base, warnings) = parse_base(source);

    assert_eq!(base.raw, source);
    assert!(
        base.preserved
            .regions
            .iter()
            .any(|r| r.text.contains("customTop"))
    );
    let view = &base.views[0];
    assert_eq!(view.view_type, ViewType::Cards);
    assert!(
        view.preserved
            .regions
            .iter()
            .any(|r| r.text.contains("rowHeight"))
    );
    assert!(
        base.properties[0]
            .1
            .preserved
            .regions
            .iter()
            .any(|r| r.text.contains("width"))
    );
    assert_eq!(view.source, RowSource::Files);
    assert_eq!(
        view.slate_state
            .as_ref()
            .and_then(|v| v["selected"].as_bool()),
        Some(true)
    );
    assert!(
        warnings
            .iter()
            .any(|w| w.kind == BaseWarningKind::InvalidViewSource)
    );

    let query = view_query(&base, 0);
    assert!(matches!(
        query.view,
        ViewSpec::Table {
            fallback_from: Some(ViewType::Cards)
        }
    ));
}

#[test]
fn filter_mapping_with_extra_keys_is_preserved_as_unsupported() {
    let source = r#"filters:
  and:
    - 'status != "done"' # keep this authored shape
  plugin: "keep"
views:
  - type: table
    name: Table
"#;
    let (base, warnings) = parse_base(source);

    let Some(FilterNode::Stmt(expr)) = base.filters else {
        panic!("expected unsupported statement");
    };
    let ExprKind::Unsupported { raw, .. } = expr.kind else {
        panic!("expected unsupported filter");
    };
    assert_eq!(
        raw,
        "filters:\n  and:\n    - 'status != \"done\"' # keep this authored shape\n  plugin: \"keep\"\n"
    );
    assert!(
        warnings
            .iter()
            .any(|w| w.kind == BaseWarningKind::InvalidFilter)
    );
}

#[test]
fn invalid_source_and_group_direction_warn_while_defaulting() {
    let source = r#"views:
  - type: table
    name: Table
    source: 42
    groupBy:
      property: note.status
      direction: DSEC
"#;
    let (base, warnings) = parse_base(source);

    assert_eq!(base.views[0].source, RowSource::Files);
    assert!(matches!(
        base.views[0].group_by,
        Some(ref group) if group.ascending
    ));
    assert!(
        warnings
            .iter()
            .any(|w| w.kind == BaseWarningKind::InvalidViewSource)
    );
    assert!(
        warnings
            .iter()
            .any(|w| w.kind == BaseWarningKind::InvalidGroupBy)
    );
}

#[test]
fn non_string_formula_preserves_authored_yaml_region() {
    let source = r#"formulas:
  ppu:
    nested: "keep"
views:
  - type: table
    name: Table
"#;
    let (base, warnings) = parse_base(source);

    let ExprKind::Unsupported { raw, .. } = &base.formulas[0].1.kind else {
        panic!("expected unsupported non-string formula");
    };
    assert_eq!(raw, "  ppu:\n    nested: \"keep\"\n");
    assert!(
        warnings
            .iter()
            .any(|w| w.kind == BaseWarningKind::InvalidExpression)
    );
}

#[test]
fn non_mapping_roots_degrade_but_preserve_raw() {
    let source = "- not\n- a mapping\n";
    let (base, warnings) = parse_base(source);

    assert_eq!(base.raw, source);
    assert!(base.views.is_empty());
    assert!(base.filters.is_none());
    assert!(
        warnings
            .iter()
            .any(|w| w.kind == BaseWarningKind::ParseFailed)
    );
}

#[test]
fn recursive_filters_preserve_not_semantics_and_warn_on_invalid_entries() {
    let source = r#"filters:
  not:
    - status
    - 42
views:
  - type: table
    name: Table
"#;
    let (base, warnings) = parse_base(source);

    let FilterNode::Not(nodes) = base.filters.expect("filters") else {
        panic!("expected NOT filter");
    };
    assert_eq!(nodes.len(), 2);
    assert!(matches!(nodes[0], FilterNode::Stmt(_)));
    assert!(matches!(
        nodes[1],
        FilterNode::Stmt(ref expr) if matches!(expr.kind, ExprKind::Unsupported { .. })
    ));
    assert!(
        warnings
            .iter()
            .any(|w| w.kind == BaseWarningKind::InvalidFilter)
    );
}

#[test]
fn view_query_derives_sort_from_slate_view_state() {
    let source = r#"views:
  - type: table
    name: Sorted
    limit: 1
    slate:
      sort:
        - property: rating
          direction: DESC
        - expr: file.name
          ascending: true
      order:
        - file.name
        - rating
"#;
    let (base, warnings) = parse_base(source);

    assert!(warnings.is_empty(), "{warnings:#?}");
    let query = view_query(&base, 0);
    assert_eq!(query.sort.len(), 2);
    assert!(!query.sort[0].ascending);
    assert!(query.sort[1].ascending);
    assert!(matches!(
        query.sort[0].expr.kind,
        ExprKind::Prop(PropertyRef::Note(ref name)) if name == "rating"
    ));
    assert!(matches!(
        query.sort[1].expr.kind,
        ExprKind::Prop(PropertyRef::File(FileField::Name))
    ));
}

#[test]
fn view_query_ignores_malformed_slate_sort_entries() {
    let source = r#"views:
  - type: table
    name: Sorted
    slate:
      sort:
        - property: rating
          direction: SIDEWAYS
        - expr: file.name
        - property: status
          direction: ASC
"#;
    let (base, warnings) = parse_base(source);

    assert!(warnings.is_empty(), "{warnings:#?}");
    let query = view_query(&base, 0);
    assert_eq!(query.sort.len(), 1);
    assert!(query.sort[0].ascending);
    assert!(matches!(
        query.sort[0].expr.kind,
        ExprKind::Prop(PropertyRef::Note(ref name)) if name == "status"
    ));
}

#[test]
fn circular_formula_references_become_unsupported() {
    let source = r#"formulas:
  a: "formula.b + 1"
  b: "formula.a + 1"
views:
  - type: table
    name: Table
"#;
    let (base, warnings) = parse_base(source);

    assert!(
        warnings
            .iter()
            .any(|w| w.kind == BaseWarningKind::CircularFormula)
    );
    for (name, expr) in base.formulas {
        assert!(
            matches!(expr.kind, ExprKind::Unsupported { ref reason, .. } if reason.contains("circular")),
            "formula {name} should be unsupported after cycle detection: {expr:#?}"
        );
    }
}

#[test]
fn missing_duplicate_view_names_and_tasks_source_warn() {
    let source = r#"views:
  - type: list
    source: tasks
  - type: table
    name: View 1
"#;
    let (base, warnings) = parse_base(source);

    assert_eq!(base.views[0].name, "View 1");
    assert_eq!(base.views[0].source, RowSource::Tasks);
    assert_eq!(base.views[1].name, "View 1");
    assert!(
        warnings
            .iter()
            .any(|w| w.kind == BaseWarningKind::MissingViewName)
    );
    assert!(
        warnings
            .iter()
            .any(|w| w.kind == BaseWarningKind::DuplicateViewName)
    );

    let query = view_query(&base, 0);
    assert_eq!(query.row_source, RowSource::Tasks);
    assert!(matches!(
        query.view,
        ViewSpec::List {
            fallback_from: None
        }
    ));
    assert_eq!(
        query
            .columns
            .iter()
            .map(|column| column.id.as_str())
            .collect::<Vec<_>>(),
        vec!["task.text", "task.status", "task.due", "task.file"]
    );
}

#[test]
fn view_query_combines_base_and_view_filters_with_and() {
    let source = r#"filters: "file.ext == \"md\""
views:
  - type: table
    name: Table
    filters: "status != \"done\""
"#;
    let (base, warnings) = parse_base(source);
    assert!(warnings.is_empty(), "{warnings:#?}");

    let query = view_query(&base, 0);
    let Some(FilterNode::And(nodes)) = query.filters else {
        panic!("expected combined AND filters, got {query:#?}");
    };
    assert_eq!(nodes.len(), 2);
    for node in nodes {
        let FilterNode::Stmt(expr) = node else {
            panic!("expected statement filters");
        };
        assert!(matches!(
            expr.kind,
            ExprKind::Binary {
                op: BinaryOp::Eq | BinaryOp::Ne,
                ..
            }
        ));
    }
}

#[test]
fn group_by_file_property_parses_closed_field_set() {
    let source = r#"views:
  - type: table
    name: Table
    groupBy:
      property: file.folder
"#;
    let (base, warnings) = parse_base(source);
    assert!(warnings.is_empty(), "{warnings:#?}");

    assert!(matches!(
        base.views[0].group_by,
        Some(ref group)
            if group.property == PropertyRef::File(FileField::Folder) && group.ascending
    ));
}
