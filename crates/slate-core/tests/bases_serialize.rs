// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

use std::collections::BTreeSet;

use slate_core::bases::{
    BaseEdit, BaseWarningKind, FilterNode, SerializeError, SummaryRef,
    expr::parse_expr as parse_base_expr, parse_base, serialize_base,
};

fn corpus() -> Vec<(&'static str, &'static str)> {
    vec![
        (
            "brief_example",
            include_str!("fixtures/bases/brief_example.base"),
        ),
        (
            "comments_key_order",
            include_str!("fixtures/bases/comments_key_order.base"),
        ),
        (
            "filter_only",
            include_str!("fixtures/bases/filter_only.base"),
        ),
        (
            "plugin_view_state",
            include_str!("fixtures/bases/plugin_view_state.base"),
        ),
        (
            "every_documented_key",
            include_str!("fixtures/bases/every_documented_key.base"),
        ),
        (
            "quoted_unknown_keys",
            include_str!("fixtures/bases/quoted_unknown_keys.base"),
        ),
        (
            "tasks_and_comments",
            include_str!("fixtures/bases/tasks_and_comments.base"),
        ),
        (
            "edit_adversarial",
            include_str!("fixtures/bases/edit_adversarial.base"),
        ),
        (
            "obsidian_basic_app_capture",
            include_str!("fixtures/bases/obsidian/obsidian-basic.base"),
        ),
        (
            "obsidian_formulas_app_capture",
            include_str!("fixtures/bases/obsidian/obsidian-formulas.base"),
        ),
    ]
}

fn edit(source: &str, edit: BaseEdit) -> String {
    let (base, warnings) = parse_base(source);
    assert!(
        warnings
            .iter()
            .all(|warning| warning.kind != BaseWarningKind::ParseFailed),
        "edit source should parse: {warnings:#?}"
    );
    serialize_base(&base, &[edit]).expect("edit should serialize")
}

fn parse_preserved_root_flow_edit(source: &str) -> slate_core::bases::BaseFile {
    assert!(
        source.starts_with('{') && source.ends_with('}'),
        "edit must preserve flow-style root syntax: {source}"
    );
    assert!(
        source.contains("plugin: keep"),
        "edit changed or removed the unrelated plugin entry: {source}"
    );
    let (base, warnings) = parse_base(source);
    assert!(
        warnings
            .iter()
            .all(|warning| warning.kind != BaseWarningKind::ParseFailed),
        "root flow edit must reparse: {warnings:#?}\n{source}"
    );
    base
}

#[test]
fn edits_indented_and_flow_collections_without_invalid_yaml() {
    let four_space = "views:\n    - type: table\n      name: 'Old' # keep\n";
    let renamed = edit(
        four_space,
        BaseEdit::RenameView {
            view: 0,
            name: "New".into(),
        },
    );
    assert!(
        parse_base(&renamed)
            .1
            .iter()
            .all(|warning| warning.kind != BaseWarningKind::ParseFailed)
    );
    assert!(renamed.contains("name: 'New' # keep"));

    let formulas = edit(
        "formulas: {}\nviews: []\n",
        BaseEdit::SetFormula {
            name: "score".into(),
            expression: "1 + 1".into(),
        },
    );
    assert_eq!(parse_base(&formulas).0.formulas.len(), 1);
    let with_view = edit(
        &formulas,
        BaseEdit::AddView {
            yaml: "type: table\nname: \"Main\"".into(),
        },
    );
    assert_eq!(parse_base(&with_view).0.views.len(), 1);
}

#[test]
fn rename_view_inserts_name_at_authored_item_indent() {
    let source = "views:\n    - type: table\n";
    let changed = edit(
        source,
        BaseEdit::RenameView {
            view: 0,
            name: "New".into(),
        },
    );
    let (parsed, warnings) = parse_base(&changed);

    assert!(
        warnings
            .iter()
            .all(|warning| warning.kind != BaseWarningKind::ParseFailed)
    );
    assert_eq!(parsed.views[0].name, "New");
    assert_eq!(changed, "views:\n    - type: table\n      name: \"New\"\n");
}

#[test]
fn set_display_name_stays_inside_four_space_indented_property() {
    let source = "properties:\n    status:\n        type: text\nviews: []\n";
    let changed = edit(
        source,
        BaseEdit::SetDisplayName {
            property: "status".into(),
            display_name: Some("State".into()),
        },
    );
    let (parsed, warnings) = parse_base(&changed);

    assert!(
        warnings
            .iter()
            .all(|warning| warning.kind != BaseWarningKind::ParseFailed)
    );
    assert_eq!(
        parsed
            .properties
            .iter()
            .find(|(property, _)| property == "status")
            .and_then(|(_, config)| config.display_name.as_deref()),
        Some("State")
    );
    assert!(
        !parsed
            .properties
            .iter()
            .any(|(property, _)| property == "displayName")
    );
    assert_eq!(
        changed,
        "properties:\n    status:\n        type: text\n        displayName: \"State\"\nviews: []\n"
    );
}

#[test]
fn scalar_splice_preserves_comment_quote_and_final_newline() {
    let source = "views:\n  - type: table # keep-inline\n    name: 'Old'";
    let changed = edit(
        source,
        BaseEdit::RenameView {
            view: 0,
            name: "New".into(),
        },
    );
    assert!(changed.contains("name: 'New'"));
    assert!(changed.contains("# keep-inline"));
    assert!(!changed.ends_with('\n'));
}

#[test]
fn unicode_marker_offsets_are_safe_before_and_inside_edits() {
    let before = "emoji: 😀\nviews:\n  - type: table\n    name: 'Old'\n";
    let changed = edit(
        before,
        BaseEdit::RenameView {
            view: 0,
            name: "New".into(),
        },
    );
    let (parsed, warnings) = parse_base(&changed);
    assert!(
        warnings
            .iter()
            .all(|warning| warning.kind != BaseWarningKind::ParseFailed)
    );
    assert_eq!(parsed.views[0].name, "New");
    assert_eq!(
        changed,
        "emoji: 😀\nviews:\n  - type: table\n    name: 'New'\n"
    );

    let inside = "views:\n  - type: table\n    name: 'Old 😀 Name' # keep\n";
    let changed = edit(
        inside,
        BaseEdit::RenameView {
            view: 0,
            name: "Néw 😀".into(),
        },
    );
    let (parsed, warnings) = parse_base(&changed);
    assert!(
        warnings
            .iter()
            .all(|warning| warning.kind != BaseWarningKind::ParseFailed)
    );
    assert_eq!(parsed.views[0].name, "Néw 😀");
    assert_eq!(
        changed,
        "views:\n  - type: table\n    name: 'Néw 😀' # keep\n"
    );
}

#[test]
fn unicode_block_scalar_before_edit_uses_marker_line_and_column_fallback() {
    let block = "α".repeat(100);
    let source = format!("description: |\n  {block}\nviews:\n  - type: table\n    name: 'Old'\n");
    let changed = edit(
        &source,
        BaseEdit::RenameView {
            view: 0,
            name: "New".into(),
        },
    );
    let (parsed, warnings) = parse_base(&changed);
    assert!(
        warnings
            .iter()
            .all(|warning| warning.kind != BaseWarningKind::ParseFailed)
    );
    assert_eq!(parsed.views[0].name, "New");
    assert_eq!(
        changed,
        format!("description: |\n  {block}\nviews:\n  - type: table\n    name: 'New'\n")
    );
}

#[test]
fn adds_to_nonempty_flow_formula_mapping() {
    let formulas = edit(
        "formulas: {old: '1'}\nviews: []\n",
        BaseEdit::SetFormula {
            name: "score".into(),
            expression: "1 + 1".into(),
        },
    );
    let (parsed, warnings) = parse_base(&formulas);
    assert!(
        warnings
            .iter()
            .all(|warning| warning.kind != BaseWarningKind::ParseFailed)
    );
    assert_eq!(parsed.formulas.len(), 2);
    assert!(formulas.contains("old: '1'"));
}

#[test]
fn adds_to_nonempty_flow_view_sequence() {
    let views = edit(
        "views: [{type: table, name: Old}] # keep\nunknown: 😀\n",
        BaseEdit::AddView {
            yaml: "type: list\nname: New".into(),
        },
    );
    let (parsed, warnings) = parse_base(&views);
    assert!(
        warnings
            .iter()
            .all(|warning| warning.kind != BaseWarningKind::ParseFailed)
    );
    assert_eq!(parsed.views.len(), 2);
    assert_eq!(parsed.views[1].name, "New");
    assert!(views.contains("{type: table, name: Old}"));
    assert!(views.ends_with(" # keep\nunknown: 😀\n"));
}

#[test]
fn root_flow_set_formula_inserts_missing_key() {
    let changed = edit(
        "{views: [], plugin: keep}",
        BaseEdit::SetFormula {
            name: "score".into(),
            expression: "1 + 1".into(),
        },
    );
    let parsed = parse_preserved_root_flow_edit(&changed);

    assert_eq!(
        parsed.formulas,
        vec![(
            "score".to_string(),
            parse_base_expr("1 + 1").expect("formula expectation must parse"),
        )]
    );
    assert!(parsed.views.is_empty());
}

#[test]
fn root_flow_set_top_level_filters_inserts_missing_key() {
    let changed = edit(
        "{views: [], plugin: keep}",
        BaseEdit::SetTopLevelFilters {
            yaml: "filters: \"status == 'active'\"".into(),
        },
    );
    let parsed = parse_preserved_root_flow_edit(&changed);

    assert_eq!(
        parsed.filters,
        Some(FilterNode::Stmt(
            parse_base_expr("status == 'active'").expect("filter expectation must parse")
        ))
    );
    assert!(parsed.views.is_empty());
}

#[test]
fn root_flow_add_view_expands_empty_views() {
    let changed = edit(
        "{views: [], plugin: keep}",
        BaseEdit::AddView {
            yaml: "type: table\nname: Main".into(),
        },
    );
    let parsed = parse_preserved_root_flow_edit(&changed);

    assert_eq!(parsed.views.len(), 1);
    assert_eq!(
        parsed.views[0].view_type,
        slate_core::bases::ViewType::Table
    );
    assert_eq!(parsed.views[0].name, "Main");
}

#[test]
fn root_flow_add_view_inserts_missing_views_key() {
    let changed = edit(
        "{plugin: keep}",
        BaseEdit::AddView {
            yaml: "type: list\nname: Main".into(),
        },
    );
    let parsed = parse_preserved_root_flow_edit(&changed);

    assert_eq!(parsed.views.len(), 1);
    assert_eq!(parsed.views[0].view_type, slate_core::bases::ViewType::List);
    assert_eq!(parsed.views[0].name, "Main");
}

#[test]
fn root_flow_trailing_comma_accepts_set_formula() {
    let changed = edit(
        "{plugin: keep,}",
        BaseEdit::SetFormula {
            name: "score".into(),
            expression: "1 + 1".into(),
        },
    );
    let parsed = parse_preserved_root_flow_edit(&changed);

    assert_eq!(parsed.formulas[0].0, "score");
}

#[test]
fn root_flow_trailing_comma_accepts_top_level_filters() {
    let changed = edit(
        "{plugin: keep,}",
        BaseEdit::SetTopLevelFilters {
            yaml: "filters: \"status == 'active'\"".into(),
        },
    );
    let parsed = parse_preserved_root_flow_edit(&changed);

    assert!(parsed.filters.is_some());
}

#[test]
fn root_flow_trailing_comma_accepts_add_view() {
    let changed = edit(
        "{plugin: keep,}",
        BaseEdit::AddView {
            yaml: "type: table\nname: Main".into(),
        },
    );
    let parsed = parse_preserved_root_flow_edit(&changed);

    assert_eq!(parsed.views.len(), 1);
    assert_eq!(parsed.views[0].name, "Main");
}

#[test]
fn nonempty_flow_formula_trailing_comma_accepts_append_in_block_and_root() {
    for source in [
        "formulas: {old: '1',}\nviews: []\n",
        "{formulas: {old: '1',}, plugin: keep}",
    ] {
        let changed = edit(
            source,
            BaseEdit::SetFormula {
                name: "new".into(),
                expression: "2".into(),
            },
        );
        let (parsed, warnings) = parse_base(&changed);
        assert!(
            warnings
                .iter()
                .all(|warning| warning.kind != BaseWarningKind::ParseFailed),
            "flow formula append must reparse: {warnings:#?}\n{changed}"
        );
        assert_eq!(parsed.formulas.len(), 2);
        assert!(changed.contains("old: '1'"));
        if source.starts_with('{') {
            assert!(changed.contains("plugin: keep"));
        }
    }
}

#[test]
fn nonempty_flow_views_trailing_comma_accepts_append_in_block_and_root() {
    for source in [
        "views: [{type: table, name: Old},]\n",
        "{views: [{type: table, name: Old},], plugin: keep}",
    ] {
        let changed = edit(
            source,
            BaseEdit::AddView {
                yaml: "type: table\nname: New".into(),
            },
        );
        let (parsed, warnings) = parse_base(&changed);
        assert!(
            warnings
                .iter()
                .all(|warning| warning.kind != BaseWarningKind::ParseFailed),
            "flow view append must reparse: {warnings:#?}\n{changed}"
        );
        assert_eq!(parsed.views.len(), 2);
        assert_eq!(parsed.views[0].name, "Old");
        assert_eq!(parsed.views[1].name, "New");
        if source.starts_with('{') {
            assert!(changed.contains("plugin: keep"));
        }
    }
}

#[test]
fn multiline_block_flow_formula_trailing_comment_accepts_append() {
    let source = "formulas: {old: '1', # tail\n}\nviews: []\n";
    let changed = edit(
        source,
        BaseEdit::SetFormula {
            name: "new".into(),
            expression: "2".into(),
        },
    );
    let (parsed, warnings) = parse_base(&changed);

    assert!(
        warnings
            .iter()
            .all(|warning| warning.kind != BaseWarningKind::ParseFailed),
        "multiline flow formula append must reparse: {warnings:#?}\n{changed}"
    );
    assert_eq!(parsed.formulas.len(), 2);
    assert!(changed.contains("# tail\n"));
}

#[test]
fn multiline_block_flow_views_trailing_comment_accepts_append() {
    let source = "views: [{type: table, name: Old}, # tail\n]\n";
    let changed = edit(
        source,
        BaseEdit::AddView {
            yaml: "type: table\nname: New".into(),
        },
    );
    let (parsed, warnings) = parse_base(&changed);

    assert!(
        warnings
            .iter()
            .all(|warning| warning.kind != BaseWarningKind::ParseFailed),
        "multiline flow view append must reparse: {warnings:#?}\n{changed}"
    );
    assert_eq!(parsed.views.len(), 2);
    assert_eq!(parsed.views[1].name, "New");
    assert!(changed.contains("# tail\n"));
}

#[test]
fn removes_from_nonempty_flow_collections_without_corrupting_delimiters() {
    let removed = edit(
        "views: [{type: table, name: Old}, {type: list, name: Keep}] # keep\nunknown: 😀\n",
        BaseEdit::RemoveView { view: 0 },
    );
    let (parsed, warnings) = parse_base(&removed);
    assert!(
        warnings
            .iter()
            .all(|warning| warning.kind != BaseWarningKind::ParseFailed)
    );
    assert_eq!(parsed.views.len(), 1);
    assert_eq!(parsed.views[0].name, "Keep");
    assert!(removed.contains("{type: list, name: Keep}"));
    assert!(removed.ends_with(" # keep\nunknown: 😀\n"));

    let removed = edit(
        "formulas: {old: '1', keep: '2'}\nviews: []\n",
        BaseEdit::RemoveFormula { name: "old".into() },
    );
    let (parsed, warnings) = parse_base(&removed);
    assert!(
        warnings
            .iter()
            .all(|warning| warning.kind != BaseWarningKind::ParseFailed)
    );
    assert_eq!(parsed.formulas.len(), 1);
    assert!(removed.contains("keep: '2'"));
}

#[test]
fn census_bases_serializer_existing_flow_structured_view_keys_accept_multiline_replacements() {
    let source = concat!(
        "views: [{ type: table, name: \"Main\", filters: { and: [\"old\"] }, ",
        "groupBy: { property: old, direction: ASC }, order: [old], ",
        "pluginKey: \"keep-flow-existing\" }]"
    );
    let cases = [
        (
            BaseEdit::SetViewFilters {
                view: 0,
                yaml: concat!(
                    "filters:\n",
                    "  and:\n",
                    "    - \"status == 'active'\"\n",
                    "    - \"file.ext == 'md'\""
                )
                .to_string(),
            },
            concat!(
                "views: [{ type: table, name: \"Main\", ",
                "\"filters\": {\"and\": [\"status == 'active'\", ",
                "\"file.ext == 'md'\"]}, ",
                "groupBy: { property: old, direction: ASC }, order: [old], ",
                "pluginKey: \"keep-flow-existing\" }]"
            ),
        ),
        (
            BaseEdit::SetViewKey {
                view: 0,
                key: "groupBy".to_string(),
                value: "groupBy:\n  property: status\n  direction: DESC".to_string(),
            },
            concat!(
                "views: [{ type: table, name: \"Main\", filters: { and: [\"old\"] }, ",
                "\"groupBy\": {\"property\": \"status\", \"direction\": \"DESC\"}, ",
                "order: [old], pluginKey: \"keep-flow-existing\" }]"
            ),
        ),
        (
            BaseEdit::SetViewKey {
                view: 0,
                key: "order".to_string(),
                value: "order:\n  - status\n  - file.name".to_string(),
            },
            concat!(
                "views: [{ type: table, name: \"Main\", filters: { and: [\"old\"] }, ",
                "groupBy: { property: old, direction: ASC }, ",
                "\"order\": [\"status\", \"file.name\"], ",
                "pluginKey: \"keep-flow-existing\" }]"
            ),
        ),
    ];

    for (operation, expected) in cases {
        let changed = edit(source, operation);
        assert_eq!(changed, expected);
        assert!(changed.contains("pluginKey: \"keep-flow-existing\""));
        assert!(!changed.ends_with('\n'));
        let (_, warnings) = parse_base(&changed);
        assert!(
            warnings.is_empty(),
            "flow replacement warnings: {warnings:#?}"
        );
    }
}

#[test]
fn census_bases_serializer_removing_only_block_children_collapses_parent_mapping() {
    let formula_source = concat!(
        "unknownSentinel: \"keep-formula\"\n",
        "formulas:\n",
        "  only: \"1 + 1\"\n",
        "views: []\n"
    );
    let formula_changed = edit(
        formula_source,
        BaseEdit::RemoveFormula {
            name: "only".to_string(),
        },
    );
    assert_eq!(
        formula_changed,
        concat!(
            "unknownSentinel: \"keep-formula\"\n",
            "formulas: {}\n",
            "views: []\n"
        )
    );
    let (formula_base, formula_warnings) = parse_base(&formula_changed);
    assert!(formula_warnings.is_empty(), "{formula_warnings:#?}");
    assert!(formula_base.formulas.is_empty());

    let summary_source = concat!(
        "views:\n",
        "  - type: table\n",
        "    name: Main\n",
        "    summaries:\n",
        "      status: Count\n",
        "    pluginKey: \"keep-summary\"\n"
    );
    let summary_changed = edit(
        summary_source,
        BaseEdit::SetSummaryAssignment {
            view: 0,
            property: "status".to_string(),
            summary: None,
        },
    );
    assert_eq!(
        summary_changed,
        concat!(
            "views:\n",
            "  - type: table\n",
            "    name: Main\n",
            "    summaries: {}\n",
            "    pluginKey: \"keep-summary\"\n"
        )
    );
    let (summary_base, summary_warnings) = parse_base(&summary_changed);
    assert!(summary_warnings.is_empty(), "{summary_warnings:#?}");
    assert!(summary_base.views[0].summaries.is_empty());
    assert!(summary_changed.contains("pluginKey: \"keep-summary\""));

    let property_source = concat!(
        "unknownSentinel: \"keep-property\"\n",
        "properties:\n",
        "  status:\n",
        "    displayName: State\n",
        "views: []\n"
    );
    let property_changed = edit(
        property_source,
        BaseEdit::SetDisplayName {
            property: "status".to_string(),
            display_name: None,
        },
    );
    assert_eq!(
        property_changed,
        concat!(
            "unknownSentinel: \"keep-property\"\n",
            "properties:\n",
            "  status: {}\n",
            "views: []\n"
        )
    );
    let (property_base, property_warnings) = parse_base(&property_changed);
    assert!(property_warnings.is_empty(), "{property_warnings:#?}");
    assert_eq!(property_base.properties[0].0, "status");
    assert_eq!(property_base.properties[0].1.display_name, None);
}

#[test]
fn census_bases_serializer_dependent_batches_reparse_between_edits() {
    let formula_source = concat!(
        "unknownSentinel: \"keep-formula-batch\"\n",
        "formulas:\n",
        "  old: \"1\"\n",
        "  newer: \"2\""
    );
    let (formula_base, warnings) = parse_base(formula_source);
    assert!(warnings.is_empty(), "{warnings:#?}");
    let formulas_removed = serialize_base(
        &formula_base,
        &[
            BaseEdit::RemoveFormula {
                name: "old".to_string(),
            },
            BaseEdit::RemoveFormula {
                name: "newer".to_string(),
            },
        ],
    )
    .expect("all-formula batch must serialize");
    assert_eq!(
        formulas_removed,
        "unknownSentinel: \"keep-formula-batch\"\nformulas: {}"
    );
    assert!(!formulas_removed.ends_with('\n'));
    let (formula_base, warnings) = parse_base(&formulas_removed);
    assert!(warnings.is_empty(), "{warnings:#?}");
    assert!(formula_base.formulas.is_empty());

    let summary_source = concat!(
        "views:\n",
        "  - type: table\n",
        "    name: Main\n",
        "    summaries:\n",
        "      old: Count\n",
        "      newer: Count"
    );
    let (summary_base, warnings) = parse_base(summary_source);
    assert!(warnings.is_empty(), "{warnings:#?}");
    let summaries_removed = serialize_base(
        &summary_base,
        &[
            BaseEdit::SetSummaryAssignment {
                view: 0,
                property: "old".to_string(),
                summary: None,
            },
            BaseEdit::SetSummaryAssignment {
                view: 0,
                property: "newer".to_string(),
                summary: None,
            },
        ],
    )
    .expect("all-summary batch must serialize");
    assert_eq!(
        summaries_removed,
        concat!(
            "views:\n",
            "  - type: table\n",
            "    name: Main\n",
            "    summaries: {}"
        )
    );
    assert!(!summaries_removed.ends_with('\n'));
    let (summary_base, warnings) = parse_base(&summaries_removed);
    assert!(warnings.is_empty(), "{warnings:#?}");
    assert!(summary_base.views[0].summaries.is_empty());

    let mixed_source = "formulas:\n  old: \"1\"\nviews: []\n";
    let (mixed_base, warnings) = parse_base(mixed_source);
    assert!(warnings.is_empty(), "{warnings:#?}");
    let mixed_changed = serialize_base(
        &mixed_base,
        &[
            BaseEdit::RemoveFormula {
                name: "old".to_string(),
            },
            BaseEdit::SetFormula {
                name: "new".to_string(),
                expression: "2 + 2".to_string(),
            },
        ],
    )
    .expect("dependent remove-then-set batch must serialize");
    assert_eq!(mixed_changed, "formulas:\n  new: \"2 + 2\"\nviews: []\n");
    let (mixed_base, warnings) = parse_base(&mixed_changed);
    assert!(warnings.is_empty(), "{warnings:#?}");
    assert_eq!(mixed_base.formulas.len(), 1);
    assert_eq!(mixed_base.formulas[0].0, "new");
}

#[test]
fn census_bases_serializer_parent_collapse_preserves_comments_and_unknown_bytes() {
    let formula_source = concat!(
        "unknownSentinel: \"keep-formula-comments\"\n",
        "formulas: # KEEP_FORMULAS\n",
        "  # KEEP_FORMULA_INTERSTITIAL\n",
        "  only: \"1\"\n",
        "views: []\n"
    );
    let formula_changed = edit(
        formula_source,
        BaseEdit::RemoveFormula {
            name: "only".to_string(),
        },
    );
    assert_eq!(
        formula_changed,
        concat!(
            "unknownSentinel: \"keep-formula-comments\"\n",
            "formulas: {} # KEEP_FORMULAS\n",
            "  # KEEP_FORMULA_INTERSTITIAL\n",
            "views: []\n"
        )
    );

    let property_source = concat!(
        "unknownSentinel: \"keep-property-comments\"\n",
        "properties:\n",
        "  status: # KEEP_PROPERTY\n",
        "    # KEEP_PROPERTY_INTERSTITIAL\n",
        "    displayName: State\n",
        "views: []\n"
    );
    let property_changed = edit(
        property_source,
        BaseEdit::SetDisplayName {
            property: "status".to_string(),
            display_name: None,
        },
    );
    assert_eq!(
        property_changed,
        concat!(
            "unknownSentinel: \"keep-property-comments\"\n",
            "properties:\n",
            "  status: {} # KEEP_PROPERTY\n",
            "    # KEEP_PROPERTY_INTERSTITIAL\n",
            "views: []\n"
        )
    );

    let summary_source = concat!(
        "views:\n",
        "  - type: table\n",
        "    name: Main\n",
        "    summaries: # KEEP_SUMMARIES\n",
        "      # KEEP_SUMMARY_INTERSTITIAL\n",
        "      status: Count\n",
        "    pluginKey: \"keep-summary-unknown\"\n"
    );
    let summary_changed = edit(
        summary_source,
        BaseEdit::SetSummaryAssignment {
            view: 0,
            property: "status".to_string(),
            summary: None,
        },
    );
    assert_eq!(
        summary_changed,
        concat!(
            "views:\n",
            "  - type: table\n",
            "    name: Main\n",
            "    summaries: {} # KEEP_SUMMARIES\n",
            "      # KEEP_SUMMARY_INTERSTITIAL\n",
            "    pluginKey: \"keep-summary-unknown\"\n"
        )
    );

    for changed in [formula_changed, property_changed, summary_changed] {
        let (_, warnings) = parse_base(&changed);
        assert!(warnings.is_empty(), "collapse warnings: {warnings:#?}");
    }
}

#[test]
fn census_bases_serializer_removing_all_block_views_collapses_parent_sequence() {
    let source = concat!(
        "unknownSentinel: \"keep-views\"\n",
        "views: # KEEP_VIEWS\n",
        "  # KEEP_VIEW_INTERSTITIAL\n",
        "  - type: table\n",
        "    name: First\n",
        "  - type: list\n",
        "    name: Second"
    );
    let (base, warnings) = parse_base(source);
    assert!(warnings.is_empty(), "{warnings:#?}");
    let changed = serialize_base(
        &base,
        &[
            BaseEdit::RemoveView { view: 1 },
            BaseEdit::RemoveView { view: 0 },
        ],
    )
    .expect("all-view batch must serialize");
    assert_eq!(
        changed,
        concat!(
            "unknownSentinel: \"keep-views\"\n",
            "views: [] # KEEP_VIEWS\n",
            "  # KEEP_VIEW_INTERSTITIAL"
        )
    );
    assert!(!changed.ends_with('\n'));
    let (base, warnings) = parse_base(&changed);
    assert!(warnings.is_empty(), "{warnings:#?}");
    assert!(base.views.is_empty());
}

#[test]
fn census_bases_serializer_block_removals_at_eof_preserve_no_final_newline_policy() {
    let cases = [
        (
            "RemoveViewKey",
            "views:\n  - type: table\n    name: Main\n    limit: 1",
            BaseEdit::RemoveViewKey {
                view: 0,
                key: "limit".to_string(),
            },
            "views:\n  - type: table\n    name: Main",
        ),
        (
            "RemoveFormula",
            "formulas:\n  keep: \"1\"\n  drop: \"2\"",
            BaseEdit::RemoveFormula {
                name: "drop".to_string(),
            },
            "formulas:\n  keep: \"1\"",
        ),
        (
            "SetDisplayName(None)",
            "properties:\n  status:\n    type: text\n    displayName: State",
            BaseEdit::SetDisplayName {
                property: "status".to_string(),
                display_name: None,
            },
            "properties:\n  status:\n    type: text",
        ),
        (
            "SetSummaryAssignment(None)",
            concat!(
                "views:\n",
                "  - type: table\n",
                "    name: Main\n",
                "    summaries:\n",
                "      keep: Count\n",
                "      drop: Count"
            ),
            BaseEdit::SetSummaryAssignment {
                view: 0,
                property: "drop".to_string(),
                summary: None,
            },
            concat!(
                "views:\n",
                "  - type: table\n",
                "    name: Main\n",
                "    summaries:\n",
                "      keep: Count"
            ),
        ),
        (
            "SetSlateState(None)",
            concat!(
                "views:\n",
                "  - type: table\n",
                "    name: Main\n",
                "    slate:\n",
                "      density: compact"
            ),
            BaseEdit::SetSlateState {
                view: 0,
                yaml: None,
            },
            "views:\n  - type: table\n    name: Main",
        ),
    ];

    for (name, source, operation, expected) in cases {
        let changed = edit(source, operation.clone());
        assert_eq!(changed, expected, "{name}");
        assert!(!changed.ends_with('\n'), "{name} added a final newline");
        let (_, warnings) = parse_base(&changed);
        assert!(warnings.is_empty(), "{name} warnings: {warnings:#?}");

        let crlf_source = source.replace('\n', "\r\n");
        let crlf_expected = expected.replace('\n', "\r\n");
        let crlf_changed = edit(&crlf_source, operation);
        assert_eq!(crlf_changed, crlf_expected, "{name} CRLF");
        assert!(
            !crlf_changed.ends_with(['\r', '\n']),
            "{name} CRLF added a final newline"
        );
        assert!(
            !crlf_changed.replace("\r\n", "").contains('\n'),
            "{name} CRLF introduced a lone LF"
        );
        let (_, crlf_warnings) = parse_base(&crlf_changed);
        assert!(
            crlf_warnings.is_empty(),
            "{name} CRLF warnings: {crlf_warnings:#?}"
        );
    }

    let adjacent_source = concat!(
        "views:\n",
        "  - type: table\n",
        "    name: Main\n",
        "    filters: \"status\"\n",
        "    limit: 1"
    );
    let (adjacent_base, warnings) = parse_base(adjacent_source);
    assert!(warnings.is_empty(), "{warnings:#?}");
    let adjacent_changed = serialize_base(
        &adjacent_base,
        &[
            BaseEdit::RemoveViewKey {
                view: 0,
                key: "filters".to_string(),
            },
            BaseEdit::RemoveViewKey {
                view: 0,
                key: "limit".to_string(),
            },
        ],
    )
    .expect("adjacent EOF removals must coalesce safely");
    assert_eq!(adjacent_changed, "views:\n  - type: table\n    name: Main");
}

#[test]
fn scalar_splice_replaces_complete_multiline_tokens_with_crlf() {
    let quoted =
        "views:\r\n  - type: table\r\n    name: 'Old\r\n      Name' # keep\r\n    limit: 5\r\n";
    let changed = edit(
        quoted,
        BaseEdit::RenameView {
            view: 0,
            name: "New".into(),
        },
    );
    let (parsed, warnings) = parse_base(&changed);
    assert!(
        warnings
            .iter()
            .all(|warning| warning.kind != BaseWarningKind::ParseFailed)
    );
    assert_eq!(parsed.views[0].name, "New");
    assert_eq!(
        changed,
        "views:\r\n  - type: table\r\n    name: 'New' # keep\r\n    limit: 5\r\n"
    );
    assert!(!changed.replace("\r\n", "").contains('\n'));

    let plain = "views:\n  - type: table\n    name: Old\n      Name\n    limit: 5\n";
    let changed = edit(
        plain,
        BaseEdit::RenameView {
            view: 0,
            name: "New".into(),
        },
    );
    let (parsed, warnings) = parse_base(&changed);
    assert!(
        warnings
            .iter()
            .all(|warning| warning.kind != BaseWarningKind::ParseFailed)
    );
    assert_eq!(parsed.views[0].name, "New");
    assert_eq!(
        changed,
        "views:\n  - type: table\n    name: New\n    limit: 5\n"
    );

    let flow_plain = "formulas: {calc: old\r\n  continued, keep: '1'}\r\nviews: []\r\n";
    let changed = edit(
        flow_plain,
        BaseEdit::SetFormula {
            name: "calc".into(),
            expression: "new".into(),
        },
    );
    let (_, warnings) = parse_base(&changed);
    assert!(
        warnings
            .iter()
            .all(|warning| warning.kind != BaseWarningKind::ParseFailed)
    );
    assert_eq!(changed, "formulas: {calc: new, keep: '1'}\r\nviews: []\r\n");
}

#[test]
fn successful_edits_are_always_reparsable() {
    let (base, warnings) = parse_base("views: []\n");
    assert!(
        warnings
            .iter()
            .all(|warning| warning.kind != BaseWarningKind::ParseFailed)
    );

    let error = serialize_base(
        &base,
        &[BaseEdit::AddView {
            yaml: "type: [unterminated".into(),
        }],
    )
    .expect_err("an edit that emits invalid YAML must not report success");
    assert!(matches!(error, SerializeError::InvalidEdit { .. }));
}

#[test]
fn untouched_corpus_serializes_byte_equal() {
    for (name, source) in corpus() {
        let (base, warnings) = parse_base(source);
        assert!(
            warnings
                .iter()
                .all(|w| !matches!(w.kind, slate_core::bases::BaseWarningKind::ParseFailed)),
            "{name} should load without root failure: {warnings:#?}"
        );

        let serialized = serialize_base(&base, &[]).expect("untouched serialize succeeds");
        assert_eq!(serialized, source, "{name} must be byte-equal");
    }
}

#[test]
fn rename_view_rewrites_only_name_line() {
    let source = include_str!("fixtures/bases/every_documented_key.base");
    let (base, _) = parse_base(source);

    let edited = serialize_base(
        &base,
        &[BaseEdit::RenameView {
            view: 0,
            name: "Renamed".to_string(),
        }],
    )
    .expect("rename serializes");

    assert!(edited.contains("    name: \"Renamed\"\n"));
    assert!(edited.contains("filters:\n  and:\n    - file.hasTag(\"project\")\n"));
    assert_eq!(
        edited.replace("    name: \"Renamed\"\n", "    name: \"Table\"\n"),
        source
    );
}

#[test]
fn set_existing_formula_preserves_surrounding_yaml() {
    let source = include_str!("fixtures/bases/comments_key_order.base");
    let (base, _) = parse_base(source);

    let edited = serialize_base(
        &base,
        &[BaseEdit::SetFormula {
            name: "ppu".to_string(),
            expression: "price / 100".to_string(),
        }],
    )
    .expect("formula serializes");

    assert!(edited.contains("  ppu: 'price / 100'\n"));
    assert!(edited.starts_with("# Obsidian-authored shape"));
    assert_eq!(
        edited.replace("  ppu: 'price / 100'\n", "  ppu: 'price / pages'\n"),
        source
    );
}

#[test]
fn remove_formula_deletes_only_that_entry() {
    let source = include_str!("fixtures/bases/brief_example.base");
    let (base, _) = parse_base(source);

    let edited = serialize_base(
        &base,
        &[BaseEdit::RemoveFormula {
            name: "formatted_price".to_string(),
        }],
    )
    .expect("remove serializes");

    assert!(!edited.contains("  formatted_price:"));
    assert!(edited.contains("  ppu: \"(price / age).toFixed(2)\"\n"));
}

#[test]
fn set_view_filters_rewrites_filter_block_only() {
    let source = include_str!("fixtures/bases/every_documented_key.base");
    let (base, _) = parse_base(source);

    let edited = serialize_base(
        &base,
        &[BaseEdit::SetViewFilters {
            view: 0,
            yaml: "filters: \"status != \\\"done\\\"\"".to_string(),
        }],
    )
    .expect("view filter serializes");

    assert!(edited.contains("    filters: \"status != \\\"done\\\"\"\n"));
    assert!(edited.contains("    order:\n      - file.name\n"));
}

#[test]
fn remove_view_key_deletes_existing_view_key_only() {
    let source = "views:\n  - type: table\n    filters: \"status != \\\"done\\\"\"\n    groupBy:\n      property: status\n      direction: DESC\n    order:\n      - file.name\n";
    let (base, _) = parse_base(source);

    let edited = serialize_base(
        &base,
        &[
            BaseEdit::RemoveViewKey {
                view: 0,
                key: "filters".to_string(),
            },
            BaseEdit::RemoveViewKey {
                view: 0,
                key: "groupBy".to_string(),
            },
        ],
    )
    .expect("view key removals serialize");

    assert!(!edited.contains("filters:"));
    assert!(!edited.contains("groupBy:"));
    assert!(edited.contains("    order:\n      - file.name\n"));
}

#[test]
fn set_view_key_preserves_view_list_marker() {
    let source = include_str!("fixtures/bases/every_documented_key.base");
    let (base, _) = parse_base(source);

    let edited = serialize_base(
        &base,
        &[BaseEdit::SetViewKey {
            view: 0,
            key: "type".to_string(),
            value: "list".to_string(),
        }],
    )
    .expect("view key serializes");

    assert!(edited.contains("  - type: list\n"));
    assert_eq!(
        edited.replace("  - type: list\n", "  - type: table\n"),
        source
    );
}

#[test]
fn set_view_key_preserves_interstitial_comments() {
    let source = "views:\n  - type: table\n    # keep this comment\n    name: \"Table\"\n";
    let (base, _) = parse_base(source);

    let edited = serialize_base(
        &base,
        &[BaseEdit::SetViewKey {
            view: 0,
            key: "type".to_string(),
            value: "list".to_string(),
        }],
    )
    .expect("view key serializes");

    assert_eq!(
        edited,
        "views:\n  - type: list\n    # keep this comment\n    name: \"Table\"\n"
    );
}

#[test]
fn set_view_key_rejects_keys_outside_closed_set() {
    let source = include_str!("fixtures/bases/every_documented_key.base");
    let (base, _) = parse_base(source);

    let err = serialize_base(
        &base,
        &[BaseEdit::SetViewKey {
            view: 0,
            key: "filters".to_string(),
            value: "status".to_string(),
        }],
    )
    .expect_err("filters has a dedicated edit");

    assert!(matches!(err, SerializeError::InvalidEdit { .. }));
}

#[test]
fn set_top_level_filters_rewrites_only_filter_region() {
    let source = include_str!("fixtures/bases/every_documented_key.base");
    let (base, _) = parse_base(source);

    let edited = serialize_base(
        &base,
        &[BaseEdit::SetTopLevelFilters {
            yaml: "filters: \"file.ext == \\\"md\\\"\"".to_string(),
        }],
    )
    .expect("top-level filters serialize");

    assert!(edited.starts_with("filters: \"file.ext == \\\"md\\\"\"\n"));
    assert!(edited.contains("views:\n  - type: table\n"));
}

#[test]
fn set_display_name_rewrites_property_child_only() {
    let source = include_str!("fixtures/bases/brief_example.base");
    let (base, _) = parse_base(source);

    let edited = serialize_base(
        &base,
        &[BaseEdit::SetDisplayName {
            property: "status".to_string(),
            display_name: Some("State".to_string()),
        }],
    )
    .expect("display name serializes");

    assert!(edited.contains("    displayName: State\n"));
    assert_eq!(
        edited.replace("    displayName: State\n", "    displayName: Status\n"),
        source
    );
}

#[test]
fn set_display_name_inserts_missing_properties_section_before_views() {
    let source = "views:\n  - type: table\n    order:\n      - file.name\n";
    let (base, _) = parse_base(source);

    let edited = serialize_base(
        &base,
        &[BaseEdit::SetDisplayName {
            property: "file.name".to_string(),
            display_name: Some("Title".to_string()),
        }],
    )
    .expect("display name insertion serializes");

    assert!(edited.starts_with("properties:\n  file.name:\n    displayName: \"Title\"\n"));
    assert!(edited.contains("views:\n  - type: table\n"));
}

#[test]
fn set_summary_assignment_rewrites_assignment_only() {
    let source = include_str!("fixtures/bases/every_documented_key.base");
    let (base, _) = parse_base(source);

    let edited = serialize_base(
        &base,
        &[BaseEdit::SetSummaryAssignment {
            view: 0,
            property: "note.status".to_string(),
            summary: Some("Count".to_string()),
        }],
    )
    .expect("summary assignment serializes");

    assert!(edited.contains("      note.status: Count\n"));
    assert_eq!(
        edited.replace("      note.status: Count\n", "      note.status: Unique\n"),
        source
    );
}

#[test]
fn set_slate_state_rewrites_state_block_only() {
    let source = include_str!("fixtures/bases/every_documented_key.base");
    let (base, _) = parse_base(source);

    let edited = serialize_base(
        &base,
        &[BaseEdit::SetSlateState {
            view: 0,
            yaml: Some("slate:\n  density: cozy".to_string()),
        }],
    )
    .expect("slate state serializes");

    assert!(edited.contains("    slate:\n      density: cozy\n"));
    assert_eq!(
        edited.replace(
            "    slate:\n      density: cozy\n",
            "    slate:\n      density: compact\n"
        ),
        source
    );
}

fn set_slate_sort_edit(view: usize, yaml: Option<&str>) -> BaseEdit {
    serde_json::from_value(serde_json::json!({
        "SetSlateSort": {
            "view": view,
            "yaml": yaml,
        }
    }))
    .expect("SetSlateSort must be part of the closed BaseEdit API")
}

#[test]
fn set_slate_sort_rewrites_only_sort_in_block_state() {
    let source = "views:\n  - type: table\n    name: Main\n    slate:\n      density: compact # keep density\n      sort:\n        - expr: old\n          direction: asc\n      pluginState:\n        nested: keep\n      listMarker: dash\n      secondaryProperties:\n        - alpha\n      # keep trailing state comment\n";
    let (base, warnings) = parse_base(source);
    assert!(warnings.is_empty(), "{warnings:#?}");

    let changed = serialize_base(
        &base,
        &[set_slate_sort_edit(
            0,
            Some("- expr: new\n  direction: desc"),
        )],
    )
    .expect("sort edit serializes");

    assert_eq!(
        changed,
        source.replace(
            "      sort:\n        - expr: old\n          direction: asc\n",
            "      sort:\n        - expr: new\n          direction: desc\n"
        )
    );
}

#[test]
fn set_slate_sort_preserves_flow_state_and_clear_preserves_other_state() {
    let source = "views: [{type: table, name: Main, slate: {density: compact, sort: [{expr: old, direction: asc}], pluginState: {nested: keep}, listMarker: dash}}] # keep tail\n";
    let (base, warnings) = parse_base(source);
    assert!(warnings.is_empty(), "{warnings:#?}");

    let changed = serialize_base(
        &base,
        &[set_slate_sort_edit(
            0,
            Some("- expr: new\n  direction: desc"),
        )],
    )
    .expect("flow sort edit serializes");
    assert_eq!(
        changed,
        "views: [{type: table, name: Main, slate: {density: compact, \"sort\": [{\"expr\": \"new\", \"direction\": \"desc\"}], pluginState: {nested: keep}, listMarker: dash}}] # keep tail\n"
    );

    let (changed_base, warnings) = parse_base(&changed);
    assert!(warnings.is_empty(), "{warnings:#?}");
    let cleared = serialize_base(&changed_base, &[set_slate_sort_edit(0, None)])
        .expect("flow sort clear serializes");
    assert_eq!(
        cleared,
        "views: [{type: table, name: Main, slate: {density: compact, pluginState: {nested: keep}, listMarker: dash}}] # keep tail\n"
    );
}

#[test]
fn set_slate_sort_inserts_and_clears_without_rewriting_unrelated_state() {
    let source = "views:\n  - type: table\n    name: Main\n    slate:\n      density: compact\n      pluginState: keep # comment\n";
    let (base, warnings) = parse_base(source);
    assert!(warnings.is_empty(), "{warnings:#?}");

    let changed = serialize_base(
        &base,
        &[set_slate_sort_edit(
            0,
            Some("- expr: file.name\n  direction: asc"),
        )],
    )
    .expect("sort insertion serializes");
    assert_eq!(
        changed,
        "views:\n  - type: table\n    name: Main\n    slate:\n      density: compact\n      pluginState: keep # comment\n      sort:\n        - expr: file.name\n          direction: asc\n"
    );

    let (changed_base, warnings) = parse_base(&changed);
    assert!(warnings.is_empty(), "{warnings:#?}");
    let cleared = serialize_base(&changed_base, &[set_slate_sort_edit(0, None)])
        .expect("sort removal serializes");
    assert_eq!(cleared, source);
}

#[test]
fn clear_only_slate_sort_removes_the_now_empty_slate_parent() {
    let block = "views:\n  - type: table\n    name: Main\n    # before slate\n    slate:\n      sort:\n        - expr: file.name\n          direction: asc\n    # after slate\n    order: [file.name]\n";
    let (base, warnings) = parse_base(block);
    assert!(warnings.is_empty(), "{warnings:#?}");
    let cleared =
        serialize_base(&base, &[set_slate_sort_edit(0, None)]).expect("only block sort clears");
    assert_eq!(
        cleared,
        "views:\n  - type: table\n    name: Main\n    # before slate\n    # after slate\n    order: [file.name]\n"
    );

    let flow = "views: [{type: table, name: Main, slate: {sort: [{expr: file.name, direction: asc}]}, order: [file.name]}]\n";
    let (base, warnings) = parse_base(flow);
    assert!(warnings.is_empty(), "{warnings:#?}");
    let cleared =
        serialize_base(&base, &[set_slate_sort_edit(0, None)]).expect("only flow sort clears");
    assert_eq!(
        cleared,
        "views: [{type: table, name: Main, order: [file.name]}]\n"
    );
}

#[test]
fn clear_final_flow_slate_sort_preserves_preceding_comment_and_crlf_trailing_comma() {
    let source = concat!(
        "views: [{type: table, name: Main, slate: {\r\n",
        "  pluginState: keep, # KEEP_PLUGIN\r\n",
        "  sort: [{expr: file.name, direction: asc}],\r\n",
        "}}]\r\n",
    );
    let (base, warnings) = parse_base(source);
    assert!(warnings.is_empty(), "{warnings:#?}");

    let cleared = serialize_base(&base, &[set_slate_sort_edit(0, None)])
        .expect("final flow sort clears without consuming prior comments");

    assert_eq!(
        cleared,
        concat!(
            "views: [{type: table, name: Main, slate: {\r\n",
            "  pluginState: keep, # KEEP_PLUGIN\r\n",
            "}}]\r\n",
        )
    );
    assert!(cleared.contains("# KEEP_PLUGIN"));
    assert!(!cleared.replace("\r\n", "").contains('\n'));

    let no_trailing_comma = concat!(
        "views: [{type: table, name: Main, slate: {\n",
        "  pluginState: keep, # KEEP_PLUGIN\n",
        "  sort: [{expr: file.name, direction: asc}]\n",
        "}}]\n",
    );
    let (base, warnings) = parse_base(no_trailing_comma);
    assert!(warnings.is_empty(), "{warnings:#?}");
    let cleared = serialize_base(&base, &[set_slate_sort_edit(0, None)])
        .expect("final flow sort without trailing comma clears");
    assert_eq!(
        cleared,
        concat!(
            "views: [{type: table, name: Main, slate: {\n",
            "  pluginState: keep # KEEP_PLUGIN\n",
            "}}]\n",
        )
    );
}

#[test]
fn remove_final_flow_formula_preserves_preceding_comment_with_and_without_trailing_comma() {
    let crlf = concat!(
        "formulas: {\r\n",
        "  keep: \"1\", # KEEP_FORMULA\r\n",
        "  remove: \"2\",\r\n",
        "}\r\n",
        "views: []\r\n",
    );
    let (base, warnings) = parse_base(crlf);
    assert!(warnings.is_empty(), "{warnings:#?}");
    let changed = serialize_base(
        &base,
        &[BaseEdit::RemoveFormula {
            name: "remove".to_string(),
        }],
    )
    .expect("final CRLF flow formula removal serializes");
    assert_eq!(
        changed,
        concat!(
            "formulas: {\r\n",
            "  keep: \"1\", # KEEP_FORMULA\r\n",
            "}\r\n",
            "views: []\r\n",
        )
    );

    let lf = concat!(
        "formulas: {\n",
        "  keep: \"1\", # KEEP_FORMULA\n",
        "  remove: \"2\"\n",
        "}\n",
        "views: []\n",
    );
    let (base, warnings) = parse_base(lf);
    assert!(warnings.is_empty(), "{warnings:#?}");
    let changed = serialize_base(
        &base,
        &[BaseEdit::RemoveFormula {
            name: "remove".to_string(),
        }],
    )
    .expect("final LF flow formula removal serializes");
    assert_eq!(
        changed,
        concat!(
            "formulas: {\n",
            "  keep: \"1\" # KEEP_FORMULA\n",
            "}\n",
            "views: []\n",
        )
    );
}

#[test]
fn remove_final_flow_view_preserves_preceding_comment_with_and_without_trailing_comma() {
    let crlf = concat!(
        "views: [\r\n",
        "  {type: table, name: Keep}, # KEEP_VIEW\r\n",
        "  {type: list, name: Remove},\r\n",
        "]\r\n",
    );
    let (base, warnings) = parse_base(crlf);
    assert!(warnings.is_empty(), "{warnings:#?}");
    let changed = serialize_base(&base, &[BaseEdit::RemoveView { view: 1 }])
        .expect("final CRLF flow view removal serializes");
    assert_eq!(
        changed,
        concat!(
            "views: [\r\n",
            "  {type: table, name: Keep}, # KEEP_VIEW\r\n",
            "]\r\n",
        )
    );

    let lf = concat!(
        "views: [\n",
        "  {type: table, name: Keep}, # KEEP_VIEW\n",
        "  {type: list, name: Remove}\n",
        "]\n",
    );
    let (base, warnings) = parse_base(lf);
    assert!(warnings.is_empty(), "{warnings:#?}");
    let changed = serialize_base(&base, &[BaseEdit::RemoveView { view: 1 }])
        .expect("final LF flow view removal serializes");
    assert_eq!(
        changed,
        concat!(
            "views: [\n",
            "  {type: table, name: Keep} # KEEP_VIEW\n",
            "]\n",
        )
    );
}

#[test]
fn remove_sole_flow_items_consumes_legal_trailing_comma_and_preserves_suffix_trivia() {
    let formula = "formulas: {remove: \"1\",} # KEEP_SOLE\r\nviews: []\r\n";
    let (base, warnings) = parse_base(formula);
    assert!(warnings.is_empty(), "{warnings:#?}");
    let changed = serialize_base(
        &base,
        &[BaseEdit::RemoveFormula {
            name: "remove".to_string(),
        }],
    )
    .expect("sole flow formula removal serializes");
    assert_eq!(changed, "formulas: {} # KEEP_SOLE\r\nviews: []\r\n");
    assert!(parse_base(&changed).1.is_empty());

    let view = "views: [{type: table, name: Remove},] # KEEP_SOLE\n";
    let (base, warnings) = parse_base(view);
    assert!(warnings.is_empty(), "{warnings:#?}");
    let changed = serialize_base(&base, &[BaseEdit::RemoveView { view: 0 }])
        .expect("sole flow view removal serializes");
    assert_eq!(changed, "views: [] # KEEP_SOLE\n");
    assert!(parse_base(&changed).1.is_empty());

    let multiline_formula = concat!(
        "formulas: {\r\n",
        "  # KEEP_PREFIX\r\n",
        "  remove: \"1\"\r\n",
        "} # KEEP_SUFFIX\r\n",
        "views: []\r\n",
    );
    let (base, warnings) = parse_base(multiline_formula);
    assert!(warnings.is_empty(), "{warnings:#?}");
    let changed = serialize_base(
        &base,
        &[BaseEdit::RemoveFormula {
            name: "remove".to_string(),
        }],
    )
    .expect("sole multiline flow formula removal serializes");
    assert_eq!(
        changed,
        concat!(
            "formulas: { # KEEP_PREFIX\r\n",
            "  } # KEEP_SUFFIX\r\n",
            "views: []\r\n",
        )
    );
    assert!(parse_base(&changed).1.is_empty());

    let multiline_view = concat!(
        "views: [\n",
        "  # KEEP_PREFIX\n",
        "  {type: table, name: Remove},\n",
        "] # KEEP_SUFFIX\n",
    );
    let (base, warnings) = parse_base(multiline_view);
    assert!(warnings.is_empty(), "{warnings:#?}");
    let changed = serialize_base(&base, &[BaseEdit::RemoveView { view: 0 }])
        .expect("sole multiline flow view removal serializes");
    assert_eq!(
        changed,
        concat!("views: [ # KEEP_PREFIX\n", "  ] # KEEP_SUFFIX\n")
    );
    assert!(parse_base(&changed).1.is_empty());
}

#[test]
fn remove_sole_flow_suffix_comments_normalize_empty_close_indentation() {
    for (newline, close_indent) in [("\n", ""), ("\n", "  "), ("\r\n", ""), ("\r\n", "  ")] {
        let context = format!("newline={newline:?} close_indent={close_indent:?}");
        let formula = format!(
            "formulas: {{remove: \"2\", # KEEP_SOLE{newline}{close_indent}}}{newline}views: []{newline}"
        );
        let (base, warnings) = parse_base(&formula);
        assert!(
            warnings.is_empty(),
            "formula source warnings ({context}): {warnings:#?}"
        );
        let changed = serialize_base(
            &base,
            &[BaseEdit::RemoveFormula {
                name: "remove".to_string(),
            }],
        )
        .unwrap_or_else(|error| panic!("formula removal failed ({context}): {error}"));
        assert_eq!(
            changed,
            format!("formulas: {{ # KEEP_SOLE{newline}  }}{newline}views: []{newline}"),
            "{context}"
        );
        assert!(
            parse_base(&changed).1.is_empty(),
            "formula result did not reparse ({context}):\n{changed}"
        );

        let view = format!(
            "views: [{{type: table, name: Remove}}, # KEEP_SOLE{newline}{close_indent}]{newline}"
        );
        let (base, warnings) = parse_base(&view);
        assert!(
            warnings.is_empty(),
            "view source warnings ({context}): {warnings:#?}"
        );
        let changed = serialize_base(&base, &[BaseEdit::RemoveView { view: 0 }])
            .unwrap_or_else(|error| panic!("view removal failed ({context}): {error}"));
        assert_eq!(
            changed,
            format!("views: [ # KEEP_SOLE{newline}  ]{newline}"),
            "{context}"
        );
        assert!(
            parse_base(&changed).1.is_empty(),
            "view result did not reparse ({context}):\n{changed}"
        );
    }
}

#[test]
fn remove_nonfinal_flow_item_preserves_standalone_comment_before_next_item() {
    let source = concat!(
        "formulas: {\n",
        "  remove: \"1\",\n",
        "  # KEEP_NEXT\n",
        "  keep: \"2\"\n",
        "}\n",
        "views: []\n",
    );
    let (base, warnings) = parse_base(source);
    assert!(warnings.is_empty(), "{warnings:#?}");

    let changed = serialize_base(
        &base,
        &[BaseEdit::RemoveFormula {
            name: "remove".to_string(),
        }],
    )
    .expect("non-final flow formula removal serializes");

    assert_eq!(
        changed,
        concat!(
            "formulas: {\n",
            "  # KEEP_NEXT\n",
            "  keep: \"2\"\n",
            "}\n",
            "views: []\n",
        )
    );
    assert!(parse_base(&changed).1.is_empty());
}

#[test]
fn flow_removal_matrix_preserves_trivia_and_reparses_for_every_position() {
    #[derive(Clone, Copy, Debug)]
    enum Collection {
        Formulas,
        Views,
    }
    #[derive(Clone, Copy, Debug)]
    enum CommentPlacement {
        Prefix,
        Suffix,
    }
    let positions = [
        ("first", 3usize, 0usize),
        ("middle", 3, 1),
        ("final", 3, 2),
        ("sole", 1, 0),
    ];

    for collection in [Collection::Formulas, Collection::Views] {
        for (position, count, remove) in positions {
            for trailing_comma in [false, true] {
                for comment_placement in [CommentPlacement::Prefix, CommentPlacement::Suffix] {
                    for newline in ["\n", "\r\n"] {
                        let mut source = match collection {
                            Collection::Formulas => "formulas: {\n".to_string(),
                            Collection::Views => "views: [\n".to_string(),
                        };
                        for index in 0..count {
                            let comma = if index + 1 < count || trailing_comma {
                                ","
                            } else {
                                ""
                            };
                            let item = match collection {
                                Collection::Formulas => format!("f{index}: \"{index}\""),
                                Collection::Views => {
                                    format!("{{type: table, name: V{index}}}")
                                }
                            };
                            match comment_placement {
                                CommentPlacement::Prefix => {
                                    source.push_str(&format!("  # KEEP_{index}\n"));
                                    source.push_str(&format!("  {item}{comma}\n"));
                                }
                                CommentPlacement::Suffix => {
                                    source.push_str(&format!("  {item}{comma} # KEEP_{index}\n"));
                                }
                            }
                        }
                        source.push_str(match collection {
                            Collection::Formulas => "} # KEEP_SUFFIX\nviews: []\n",
                            Collection::Views => "] # KEEP_SUFFIX\n",
                        });
                        if newline == "\r\n" {
                            source = source.replace('\n', "\r\n");
                        }
                        let context = format!(
                            "collection={collection:?} position={position} trailing={trailing_comma} comments={comment_placement:?} newline={newline:?}"
                        );
                        let (base, warnings) = parse_base(&source);
                        assert!(
                            warnings.is_empty(),
                            "source warnings ({context}): {warnings:#?}"
                        );
                        let operation = match collection {
                            Collection::Formulas => BaseEdit::RemoveFormula {
                                name: format!("f{remove}"),
                            },
                            Collection::Views => BaseEdit::RemoveView { view: remove },
                        };

                        let changed = serialize_base(&base, &[operation])
                            .unwrap_or_else(|error| panic!("removal failed ({context}): {error}"));

                        for index in 0..count {
                            assert!(
                                changed.contains(&format!("# KEEP_{index}")),
                                "lost comment KEEP_{index} ({context}):\n{changed}"
                            );
                        }
                        assert!(changed.contains("# KEEP_SUFFIX"), "{context}:\n{changed}");
                        if newline == "\r\n" {
                            assert!(
                                !changed.replace("\r\n", "").contains('\n'),
                                "introduced lone LF ({context}):\n{changed:?}"
                            );
                        } else {
                            assert!(!changed.contains('\r'), "introduced CR ({context})");
                        }
                        let (reparsed, warnings) = parse_base(&changed);
                        assert!(
                            warnings.is_empty(),
                            "result warnings ({context}): {warnings:#?}\n{changed}"
                        );
                        match collection {
                            Collection::Formulas => {
                                let names = reparsed
                                    .formulas
                                    .iter()
                                    .map(|(name, _)| name.as_str())
                                    .collect::<Vec<_>>();
                                for index in 0..count {
                                    assert_eq!(
                                        names.contains(&format!("f{index}").as_str()),
                                        index != remove,
                                        "wrong formulas ({context}): {names:?}"
                                    );
                                }
                            }
                            Collection::Views => {
                                let names = reparsed
                                    .views
                                    .iter()
                                    .map(|view| view.name.as_str())
                                    .collect::<Vec<_>>();
                                for index in 0..count {
                                    assert_eq!(
                                        names.contains(&format!("V{index}").as_str()),
                                        index != remove,
                                        "wrong views ({context}): {names:?}"
                                    );
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

#[test]
fn add_view_rejects_scalar_fragment_atomically() {
    let source = "views:\n  - type: table\n    name: Existing\n";
    let (base, warnings) = parse_base(source);
    assert!(warnings.is_empty(), "{warnings:#?}");

    let error = serialize_base(
        &base,
        &[BaseEdit::AddView {
            yaml: "scalar-view".to_string(),
        }],
    )
    .expect_err("AddView must reject non-mapping fragments");

    assert!(matches!(error, SerializeError::InvalidEdit { .. }));
    assert_eq!(base.raw, source);
}

#[test]
fn add_view_creates_canonical_views_section() {
    let source = include_str!("fixtures/bases/filter_only.base");
    let (base, _) = parse_base(source);

    let edited = serialize_base(
        &base,
        &[BaseEdit::AddView {
            yaml: "type: table\nname: \"Added\"".to_string(),
        }],
    )
    .expect("add view serializes");

    assert_eq!(
        edited,
        "filters: \"file.ext == \\\"md\\\"\"\nviews:\n  - type: table\n    name: \"Added\"\n"
    );
}

#[test]
fn remove_view_deletes_only_view_entry() {
    let source = include_str!("fixtures/bases/plugin_view_state.base");
    let (base, _) = parse_base(source);

    let edited =
        serialize_base(&base, &[BaseEdit::RemoveView { view: 0 }]).expect("remove view serializes");

    assert_eq!(edited, "views: []\n");
}

#[test]
fn edit_inside_preserved_unknown_view_state_is_rejected() {
    let source = include_str!("fixtures/bases/plugin_view_state.base");
    let (base, _) = parse_base(source);

    let err = serialize_base(
        &base,
        &[BaseEdit::SetViewKey {
            view: 0,
            key: "columnWidths".to_string(),
            value: "columnWidths: {}".to_string(),
        }],
    )
    .expect_err("unknown state edits must not clobber preserved yaml");

    assert!(matches!(err, SerializeError::WouldClobber { .. }));
}

#[test]
fn edit_inside_quoted_preserved_view_key_is_rejected() {
    let source = include_str!("fixtures/bases/quoted_unknown_keys.base");
    let (base, _) = parse_base(source);

    let err = serialize_base(
        &base,
        &[BaseEdit::SetViewKey {
            view: 0,
            key: "row height".to_string(),
            value: "row height: 44".to_string(),
        }],
    )
    .expect_err("quoted unknown keys must remain preserved");

    assert!(matches!(err, SerializeError::WouldClobber { .. }));
}

#[test]
fn edit_inside_quoted_colon_preserved_view_key_is_rejected() {
    let source = "views:\n  - type: table\n    name: \"Table\"\n    \"plugin:key\": 1\n";
    let (base, _) = parse_base(source);

    let err = serialize_base(
        &base,
        &[BaseEdit::SetViewKey {
            view: 0,
            key: "plugin:key".to_string(),
            value: "2".to_string(),
        }],
    )
    .expect_err("quoted preserved key with a colon must not be duplicated");

    assert!(matches!(err, SerializeError::WouldClobber { .. }));
}

#[test]
fn census_bases_roundtrip() {
    for (name, source) in corpus() {
        let (base, warnings) = parse_base(source);
        assert!(
            warnings
                .iter()
                .all(|w| !matches!(w.kind, slate_core::bases::BaseWarningKind::ParseFailed)),
            "{name} should parse for census: {warnings:#?}"
        );
        assert_eq!(
            serialize_base(&base, &[]).expect("untouched census serialize"),
            source,
            "{name} untouched census round-trip must be byte-equal"
        );

        let edited = serialize_base(
            &base,
            &[BaseEdit::SetTopLevelFilters {
                yaml: "filters: \"file.name\"".to_string(),
            }],
        )
        .expect("top-level filter edit should serialize");
        assert!(
            edited.contains("filters: \"file.name\"\n"),
            "{name} edited output should contain the replacement filter"
        );
        assert!(
            parse_base(&edited)
                .1
                .iter()
                .all(|w| !matches!(w.kind, slate_core::bases::BaseWarningKind::ParseFailed)),
            "{name} edited output should remain parseable"
        );
    }
}

const SERIALIZER_CENSUS_SEED: u64 = 0x5A17_EA5E_C0DE_0001;

#[derive(Debug)]
struct GeneratedSerializerCase {
    seed: u64,
    source: String,
    unknown_sentinel: String,
    view_unknown_sentinel: String,
    edit: BaseEdit,
    expected: GeneratedSerializerExpectation,
    crlf: bool,
    final_newline: bool,
    four_space_indent: bool,
    flow_collection: bool,
    double_quoted: bool,
    plugin_view: bool,
    multiline_scalar: bool,
    comment: bool,
    unknown_key: bool,
}

#[derive(Debug)]
enum GeneratedSerializerExpectation {
    Success(GeneratedSerializerSemantic),
    Refusal(GeneratedSerializerError),
}

#[derive(Debug)]
enum GeneratedSerializerSemantic {
    ViewLimit(u64),
    AddedView {
        expected_count: usize,
        expected_name: String,
    },
    RemovedView {
        expected_count: usize,
        expected_remaining_name: String,
    },
    RenamedView {
        expected_source: String,
        expected_name: String,
    },
    RemovedViewLimit,
    ViewFilters(String),
    TopLevelFilters(String),
    FormulaPresent {
        name: String,
        expression: String,
    },
    FormulaAbsent(String),
    DisplayName {
        property: String,
        expected_name: String,
    },
    SummaryAssignment {
        property: String,
        expected_summary: String,
    },
    SlateDensity(String),
    SlateSort(String),
}

#[derive(Debug)]
enum GeneratedSerializerError {
    WouldClobber,
}

fn base_edit_discriminator(edit: &BaseEdit) -> &'static str {
    match edit {
        BaseEdit::SetViewKey { .. } => "SetViewKey",
        BaseEdit::AddView { .. } => "AddView",
        BaseEdit::RemoveView { .. } => "RemoveView",
        BaseEdit::RenameView { .. } => "RenameView",
        BaseEdit::RemoveViewKey { .. } => "RemoveViewKey",
        BaseEdit::SetViewFilters { .. } => "SetViewFilters",
        BaseEdit::SetTopLevelFilters { .. } => "SetTopLevelFilters",
        BaseEdit::SetFormula { .. } => "SetFormula",
        BaseEdit::RemoveFormula { .. } => "RemoveFormula",
        BaseEdit::SetDisplayName { .. } => "SetDisplayName",
        BaseEdit::SetSummaryAssignment { .. } => "SetSummaryAssignment",
        BaseEdit::SetSlateState { .. } => "SetSlateState",
        BaseEdit::SetSlateSort { .. } => "SetSlateSort",
    }
}

fn serializer_census_case_count() -> usize {
    if std::env::var("SLATE_CENSUS_FULL").as_deref() == Ok("1") {
        2_048
    } else {
        128
    }
}

fn serializer_case_seed(index: usize) -> u64 {
    let mut value =
        SERIALIZER_CENSUS_SEED.wrapping_add((index as u64).wrapping_mul(0x9E37_79B9_7F4A_7C15));
    value = (value ^ (value >> 30)).wrapping_mul(0xBF58_476D_1CE4_E5B9);
    value = (value ^ (value >> 27)).wrapping_mul(0x94D0_49BB_1331_11EB);
    value ^ (value >> 31)
}

fn generated_serializer_case(index: usize) -> GeneratedSerializerCase {
    let seed = serializer_case_seed(index);
    let crlf = index & 1 != 0;
    let final_newline = index & 2 != 0;
    let four_space_indent = index & 4 != 0;
    let flow_collection = index & 8 != 0;
    let double_quoted = index & 16 != 0;
    let plugin_view = index % 3 == 2;
    let indent = if four_space_indent { "    " } else { "  " };
    let child_indent = format!("{indent}  ");
    let grandchild_indent = format!("{child_indent}  ");
    let old_name = format!("Old-é-{index}-{seed:016x}");
    let new_name = format!("Renamed-é-{index}-{seed:016x}");
    let quote = if double_quoted { '"' } else { '\'' };
    let old_token = format!("{quote}{old_name}{quote}");
    let new_token = format!("{quote}{new_name}{quote}");
    let view_type = if plugin_view { "plugin-grid" } else { "table" };
    let multiline_scalar = !crlf && !flow_collection && final_newline;
    let comment = crlf || flow_collection;
    let unknown_key = true;
    let unknown_sentinel_lf = format!("unknownSentinel: \"keep-{seed:016x}\"\n");
    let view_unknown_sentinel = format!("pluginKey: \"keep-view-{seed:016x}\"");

    let mut source = if flow_collection {
        format!(
            "{unknown_sentinel_lf}\
formulas: {{ score: \"1 + 1\", removeMe: \"2 + 2\" }}\n\
properties: {{ status: {{ type: text, displayName: \"Old State\" }} }}\n\
summaries: {{ total: \"values.length\" }}\n\
filters: \"file.ext == 'md'\"\n\
views: [{{ type: {view_type}, name: {old_token}, limit: 7, filters: \"file.name\", order: [file.name], summaries: {{ status: total }}, source: files, slate: {{ density: compact }}, pluginKey: \"keep-view-{seed:016x}\" }}, {{ type: list, name: \"Secondary-{index}\" }}] # preserve flow comment\n"
        )
    } else {
        let description = if multiline_scalar {
            format!("description: |\n{indent}αααααααα\n")
        } else {
            String::new()
        };
        let comment = if comment { " # preserve comment" } else { "" };
        format!(
            "{unknown_sentinel_lf}{description}\
formulas:\n\
{indent}score: \"1 + 1\"\n\
{indent}removeMe: \"2 + 2\"\n\
properties:\n\
{indent}status:\n\
{child_indent}type: text\n\
{child_indent}displayName: \"Old State\"\n\
summaries:\n\
{indent}total: \"values.length\"\n\
filters: \"file.ext == 'md'\"\n\
views:\n\
{indent}- type: {view_type}\n\
{child_indent}name: {old_token}{comment}\n\
{child_indent}limit: 7\n\
{child_indent}filters: \"file.name\"\n\
{child_indent}order:\n\
{grandchild_indent}- file.name\n\
{child_indent}summaries:\n\
{grandchild_indent}status: total\n\
{child_indent}source: files\n\
{child_indent}slate:\n\
{grandchild_indent}density: compact\n\
{child_indent}pluginKey: \"keep-view-{seed:016x}\"\n\
{indent}- type: list\n\
{child_indent}name: \"Secondary-{index}\"\n"
        )
    };

    if !final_newline {
        assert_eq!(
            source.pop(),
            Some('\n'),
            "generated source lacks removable final newline index={index} seed={seed:#018x}"
        );
    }
    if crlf {
        source = source.replace('\n', "\r\n");
    }
    let unknown_sentinel = if crlf {
        unknown_sentinel_lf.replace('\n', "\r\n")
    } else {
        unknown_sentinel_lf
    };

    let (edit, expected) = match index % 14 {
        0 => (
            BaseEdit::SetViewKey {
                view: 0,
                key: "limit".to_string(),
                value: "19".to_string(),
            },
            GeneratedSerializerExpectation::Success(GeneratedSerializerSemantic::ViewLimit(19)),
        ),
        1 => {
            let expected_name = format!("Added-é-{seed:016x}");
            (
                BaseEdit::AddView {
                    yaml: format!("type: table\nname: \"{expected_name}\""),
                },
                GeneratedSerializerExpectation::Success(GeneratedSerializerSemantic::AddedView {
                    expected_count: 3,
                    expected_name,
                }),
            )
        }
        2 => (
            BaseEdit::RemoveView { view: 1 },
            GeneratedSerializerExpectation::Success(GeneratedSerializerSemantic::RemovedView {
                expected_count: 1,
                expected_remaining_name: old_name.clone(),
            }),
        ),
        3 => {
            let expected_source = source.replacen(&old_token, &new_token, 1);
            (
                BaseEdit::RenameView {
                    view: 0,
                    name: new_name.clone(),
                },
                GeneratedSerializerExpectation::Success(GeneratedSerializerSemantic::RenamedView {
                    expected_source,
                    expected_name: new_name,
                }),
            )
        }
        4 => (
            BaseEdit::RemoveViewKey {
                view: 0,
                key: "limit".to_string(),
            },
            GeneratedSerializerExpectation::Success(GeneratedSerializerSemantic::RemovedViewLimit),
        ),
        5 => (
            BaseEdit::SetViewFilters {
                view: 0,
                yaml: "filters: \"status == 'active'\"".to_string(),
            },
            GeneratedSerializerExpectation::Success(GeneratedSerializerSemantic::ViewFilters(
                "status == 'active'".to_string(),
            )),
        ),
        6 => (
            BaseEdit::SetTopLevelFilters {
                yaml: "filters: \"file.name == 'note.md'\"".to_string(),
            },
            GeneratedSerializerExpectation::Success(GeneratedSerializerSemantic::TopLevelFilters(
                "file.name == 'note.md'".to_string(),
            )),
        ),
        7 => {
            let name = format!("newScore{index}");
            (
                BaseEdit::SetFormula {
                    name: name.clone(),
                    expression: "40 + 2".to_string(),
                },
                GeneratedSerializerExpectation::Success(
                    GeneratedSerializerSemantic::FormulaPresent {
                        name,
                        expression: "40 + 2".to_string(),
                    },
                ),
            )
        }
        8 => (
            BaseEdit::RemoveFormula {
                name: "removeMe".to_string(),
            },
            GeneratedSerializerExpectation::Success(GeneratedSerializerSemantic::FormulaAbsent(
                "removeMe".to_string(),
            )),
        ),
        9 => {
            let expected_name = format!("State-é-{seed:016x}");
            (
                BaseEdit::SetDisplayName {
                    property: "status".to_string(),
                    display_name: Some(expected_name.clone()),
                },
                GeneratedSerializerExpectation::Success(GeneratedSerializerSemantic::DisplayName {
                    property: "status".to_string(),
                    expected_name,
                }),
            )
        }
        10 => (
            BaseEdit::SetSummaryAssignment {
                view: 0,
                property: "status".to_string(),
                summary: Some("Count".to_string()),
            },
            GeneratedSerializerExpectation::Success(
                GeneratedSerializerSemantic::SummaryAssignment {
                    property: "status".to_string(),
                    expected_summary: "Count".to_string(),
                },
            ),
        ),
        11 => (
            BaseEdit::SetSlateState {
                view: 0,
                yaml: Some("slate:\n  density: cozy".to_string()),
            },
            GeneratedSerializerExpectation::Success(GeneratedSerializerSemantic::SlateDensity(
                "cozy".to_string(),
            )),
        ),
        12 => (
            BaseEdit::SetSlateSort {
                view: 0,
                yaml: Some("- expr: status\n  direction: desc".to_string()),
            },
            GeneratedSerializerExpectation::Success(GeneratedSerializerSemantic::SlateSort(
                "status".to_string(),
            )),
        ),
        13 => (
            BaseEdit::SetViewKey {
                view: 0,
                key: "pluginKey".to_string(),
                value: "changed".to_string(),
            },
            GeneratedSerializerExpectation::Refusal(GeneratedSerializerError::WouldClobber),
        ),
        _ => unreachable!(),
    };

    GeneratedSerializerCase {
        seed,
        source,
        unknown_sentinel,
        view_unknown_sentinel,
        edit,
        expected,
        crlf,
        final_newline,
        four_space_indent,
        flow_collection,
        double_quoted,
        plugin_view,
        multiline_scalar,
        comment,
        unknown_key,
    }
}

fn assert_generated_semantic(
    reparsed: &slate_core::bases::BaseFile,
    semantic: &GeneratedSerializerSemantic,
    edited: &str,
    context: &str,
) {
    match semantic {
        GeneratedSerializerSemantic::ViewLimit(expected) => {
            assert_eq!(reparsed.views[0].limit, Some(*expected), "{context}");
        }
        GeneratedSerializerSemantic::AddedView {
            expected_count,
            expected_name,
        } => {
            assert_eq!(reparsed.views.len(), *expected_count, "{context}");
            assert_eq!(
                reparsed.views.last().map(|view| view.name.as_str()),
                Some(expected_name.as_str()),
                "{context}"
            );
        }
        GeneratedSerializerSemantic::RemovedView {
            expected_count,
            expected_remaining_name,
        } => {
            assert_eq!(reparsed.views.len(), *expected_count, "{context}");
            assert_eq!(
                reparsed.views.first().map(|view| view.name.as_str()),
                Some(expected_remaining_name.as_str()),
                "remove-view deleted the wrong authored view ({context})"
            );
        }
        GeneratedSerializerSemantic::RenamedView {
            expected_source,
            expected_name,
        } => {
            assert_eq!(
                edited, expected_source,
                "rename changed bytes outside the requested scalar ({context})"
            );
            assert_eq!(reparsed.views[0].name, *expected_name, "{context}");
        }
        GeneratedSerializerSemantic::RemovedViewLimit => {
            assert_eq!(reparsed.views[0].limit, None, "{context}");
        }
        GeneratedSerializerSemantic::ViewFilters(expected) => {
            let expected = FilterNode::Stmt(
                parse_base_expr(expected).expect("generated view filter expectation must parse"),
            );
            assert_eq!(
                reparsed.views[0].filters.as_ref(),
                Some(&expected),
                "{context}"
            );
        }
        GeneratedSerializerSemantic::TopLevelFilters(expected) => {
            let expected = FilterNode::Stmt(
                parse_base_expr(expected)
                    .expect("generated top-level filter expectation must parse"),
            );
            assert_eq!(reparsed.filters.as_ref(), Some(&expected), "{context}");
        }
        GeneratedSerializerSemantic::FormulaPresent { name, expression } => {
            let expected =
                parse_base_expr(expression).expect("generated formula expectation must parse");
            assert_eq!(
                reparsed
                    .formulas
                    .iter()
                    .find(|(candidate, _)| candidate == name)
                    .map(|(_, expression)| expression),
                Some(&expected),
                "set-formula did not preserve the exact requested AST ({context})"
            );
        }
        GeneratedSerializerSemantic::FormulaAbsent(expected_name) => {
            assert!(
                reparsed
                    .formulas
                    .iter()
                    .all(|(name, _)| name != expected_name),
                "{context}"
            );
        }
        GeneratedSerializerSemantic::DisplayName {
            property,
            expected_name,
        } => {
            assert_eq!(
                reparsed
                    .properties
                    .iter()
                    .find(|(name, _)| name == property)
                    .and_then(|(_, config)| config.display_name.as_deref()),
                Some(expected_name.as_str()),
                "{context}"
            );
        }
        GeneratedSerializerSemantic::SummaryAssignment {
            property,
            expected_summary,
        } => {
            assert!(
                reparsed.views[0].summaries.iter().any(|(name, summary)| {
                    name == property
                        && matches!(summary, SummaryRef::Builtin(value) if value == expected_summary)
                }),
                "{context}"
            );
        }
        GeneratedSerializerSemantic::SlateDensity(expected) => {
            assert_eq!(
                reparsed.views[0]
                    .slate_state
                    .as_ref()
                    .and_then(|state| state.get("density"))
                    .and_then(serde_json::Value::as_str),
                Some(expected.as_str()),
                "{context}"
            );
        }
        GeneratedSerializerSemantic::SlateSort(expected) => {
            let state = reparsed.views[0]
                .slate_state
                .as_ref()
                .expect("generated SetSlateSort must retain slate state");
            assert_eq!(
                state
                    .get("sort")
                    .and_then(serde_json::Value::as_array)
                    .and_then(|sort| sort.first())
                    .and_then(|sort| sort.get("expr"))
                    .and_then(serde_json::Value::as_str),
                Some(expected.as_str()),
                "{context}"
            );
            assert_eq!(
                state.get("density").and_then(serde_json::Value::as_str),
                Some("compact"),
                "SetSlateSort changed unrelated slate state ({context})"
            );
        }
    }
}

fn parse_preserved_flow_edit(source: &str) -> slate_core::bases::BaseFile {
    assert!(
        source.contains("unknownSentinel: \"keep-flow\""),
        "flow edit changed or removed the top-level unknown sentinel: {source}"
    );
    assert!(
        source.contains("pluginKey: \"keep-view-flow\""),
        "flow edit changed or removed the per-view plugin sentinel: {source}"
    );
    assert!(
        !source.ends_with('\n'),
        "flow edit changed the authored no-final-newline policy: {source}"
    );
    let (base, warnings) = parse_base(source);
    assert!(
        warnings
            .iter()
            .all(|warning| warning.kind != BaseWarningKind::ParseFailed),
        "flow edit must reparse: {warnings:#?}\n{source}"
    );
    base
}

#[test]
fn census_bases_serializer_flow_missing_key_insertions_and_removals() {
    let source = concat!(
        "unknownSentinel: \"keep-flow\"\n",
        "properties: { status: { type: text } }\n",
        "views: [{ type: table, name: \"Main\", slate: { density: compact }, ",
        "pluginKey: \"keep-view-flow\" }]"
    );

    let display_name = edit(
        source,
        BaseEdit::SetDisplayName {
            property: "newStatus".to_string(),
            display_name: Some("New State".to_string()),
        },
    );
    let display_base = parse_preserved_flow_edit(&display_name);
    assert_eq!(
        display_base
            .properties
            .iter()
            .find(|(property, _)| property == "newStatus")
            .and_then(|(_, config)| config.display_name.as_deref()),
        Some("New State")
    );
    let display_removed = edit(
        &display_name,
        BaseEdit::SetDisplayName {
            property: "newStatus".to_string(),
            display_name: None,
        },
    );
    let display_removed_base = parse_preserved_flow_edit(&display_removed);
    assert_eq!(
        display_removed_base
            .properties
            .iter()
            .find(|(property, _)| property == "newStatus")
            .and_then(|(_, config)| config.display_name.as_deref()),
        None
    );

    let filters = edit(
        source,
        BaseEdit::SetViewFilters {
            view: 0,
            yaml: "filters: \"status == 'active'\"".to_string(),
        },
    );
    let filters_base = parse_preserved_flow_edit(&filters);
    assert_eq!(
        filters_base.views[0].filters,
        Some(FilterNode::Stmt(
            parse_base_expr("status == 'active'").expect("flow filter expectation")
        ))
    );
    let filters_removed = edit(
        &filters,
        BaseEdit::RemoveViewKey {
            view: 0,
            key: "filters".to_string(),
        },
    );
    assert!(
        parse_preserved_flow_edit(&filters_removed).views[0]
            .filters
            .is_none()
    );

    let limit = edit(
        source,
        BaseEdit::SetViewKey {
            view: 0,
            key: "limit".to_string(),
            value: "17".to_string(),
        },
    );
    assert_eq!(parse_preserved_flow_edit(&limit).views[0].limit, Some(17));
    let limit_removed = edit(
        &limit,
        BaseEdit::RemoveViewKey {
            view: 0,
            key: "limit".to_string(),
        },
    );
    assert_eq!(
        parse_preserved_flow_edit(&limit_removed).views[0].limit,
        None
    );

    let summary = edit(
        source,
        BaseEdit::SetSummaryAssignment {
            view: 0,
            property: "status".to_string(),
            summary: Some("Count".to_string()),
        },
    );
    let summary_base = parse_preserved_flow_edit(&summary);
    assert!(
        summary_base.views[0]
            .summaries
            .iter()
            .any(|(property, value)| {
                property == "status"
                    && matches!(value, SummaryRef::Builtin(name) if name == "Count")
            })
    );
    let summary_removed = edit(
        &summary,
        BaseEdit::SetSummaryAssignment {
            view: 0,
            property: "status".to_string(),
            summary: None,
        },
    );
    assert!(
        parse_preserved_flow_edit(&summary_removed).views[0]
            .summaries
            .is_empty()
    );

    let slate_removed = edit(
        source,
        BaseEdit::SetSlateState {
            view: 0,
            yaml: None,
        },
    );
    assert!(
        parse_preserved_flow_edit(&slate_removed).views[0]
            .slate_state
            .is_none()
    );
    let slate_restored = edit(
        &slate_removed,
        BaseEdit::SetSlateState {
            view: 0,
            yaml: Some("slate:\n  density: cozy".to_string()),
        },
    );
    assert_eq!(
        parse_preserved_flow_edit(&slate_restored).views[0]
            .slate_state
            .as_ref()
            .and_then(|state| state.get("density"))
            .and_then(serde_json::Value::as_str),
        Some("cozy")
    );
}

#[test]
fn census_bases_serializer_generated_edits() {
    let cases = serializer_census_case_count();
    let mut edit_discriminators = BTreeSet::new();
    let mut saw_crlf = false;
    let mut saw_lf = false;
    let mut saw_final_newline = false;
    let mut saw_no_final_newline = false;
    let mut saw_two_space_indent = false;
    let mut saw_four_space_indent = false;
    let mut saw_block = false;
    let mut saw_flow = false;
    let mut saw_single_quote = false;
    let mut saw_double_quote = false;
    let mut saw_plugin_view = false;
    let mut saw_multiline_scalar = false;
    let mut saw_comment = false;
    let mut saw_unknown_key = false;
    let mut saw_refusal = false;

    for index in 0..cases {
        let case = generated_serializer_case(index);
        let context = format!(
            "index={index} seed={:#018x} operation={:?}\nsource:\n{}",
            case.seed, case.edit, case.source
        );
        saw_crlf |= case.crlf;
        saw_lf |= !case.crlf;
        saw_final_newline |= case.final_newline;
        saw_no_final_newline |= !case.final_newline;
        saw_four_space_indent |= case.four_space_indent;
        saw_two_space_indent |= !case.four_space_indent;
        saw_flow |= case.flow_collection;
        saw_block |= !case.flow_collection;
        saw_double_quote |= case.double_quoted;
        saw_single_quote |= !case.double_quoted;
        saw_plugin_view |= case.plugin_view;
        saw_multiline_scalar |= case.multiline_scalar;
        saw_comment |= case.comment;
        saw_unknown_key |= case.unknown_key;
        edit_discriminators.insert(base_edit_discriminator(&case.edit));

        let (base, warnings) = parse_base(&case.source);
        assert!(
            warnings
                .iter()
                .all(|warning| warning.kind != BaseWarningKind::ParseFailed),
            "generated source must parse ({context}): {warnings:#?}\n{}",
            case.source
        );
        assert_eq!(
            serialize_base(&base, &[]).unwrap_or_else(|error| {
                panic!("untouched generated source refused ({context}): {error}")
            }),
            case.source,
            "untouched generated bytes changed ({context})"
        );

        let first = serialize_base(&base, std::slice::from_ref(&case.edit));
        let second = serialize_base(&base, std::slice::from_ref(&case.edit));
        assert_eq!(first, second, "serializer is nondeterministic ({context})");
        match &case.expected {
            GeneratedSerializerExpectation::Success(semantic) => {
                assert_eq!(
                    base.spans.views.len(),
                    base.views.len(),
                    "successful generated edit source lacks its view span ({context})\n{}",
                    case.source
                );
                let edited = first.unwrap_or_else(|error| {
                    panic!("generated edit unexpectedly refused ({context}): {error}")
                });
                assert!(
                    edited.contains(&case.unknown_sentinel),
                    "successful edit changed or removed exact unknown sentinel bytes ({context})\n{edited}"
                );
                assert!(
                    edited.contains(&case.view_unknown_sentinel),
                    "successful edit changed or removed exact pluginKey bytes ({context})\n{edited}"
                );
                assert_eq!(
                    edited.ends_with('\n'),
                    case.final_newline,
                    "successful edit changed final-newline policy ({context})"
                );
                if case.crlf {
                    assert!(
                        !edited.replace("\r\n", "").contains('\n'),
                        "successful edit introduced a lone LF into CRLF source ({context})"
                    );
                } else {
                    assert!(
                        !edited.contains('\r'),
                        "successful edit introduced CR bytes into LF source ({context})"
                    );
                }
                let (reparsed, warnings) = parse_base(&edited);
                assert!(
                    warnings
                        .iter()
                        .all(|warning| warning.kind != BaseWarningKind::ParseFailed),
                    "edited source must reparse ({context}): {warnings:#?}\n{edited}"
                );
                assert_generated_semantic(&reparsed, semantic, &edited, &context);
            }
            GeneratedSerializerExpectation::Refusal(expected_error) => {
                saw_refusal = true;
                let error = first.expect_err(&format!(
                    "generated refusal case unexpectedly serialized ({context})"
                ));
                match expected_error {
                    GeneratedSerializerError::WouldClobber => assert!(
                        matches!(error, SerializeError::WouldClobber { .. }),
                        "expected exact SerializeError::WouldClobber ({context}), got {error:?}"
                    ),
                }
            }
        }
    }

    assert!(
        saw_crlf
            && saw_lf
            && saw_final_newline
            && saw_no_final_newline
            && saw_two_space_indent
            && saw_four_space_indent
            && saw_block
            && saw_flow
            && saw_single_quote
            && saw_double_quote
            && saw_plugin_view
            && saw_multiline_scalar
            && saw_comment
            && saw_unknown_key
            && saw_refusal,
        "serializer generator did not cover every required dimension"
    );
    assert_eq!(
        edit_discriminators,
        BTreeSet::from([
            "AddView",
            "RemoveFormula",
            "RemoveView",
            "RemoveViewKey",
            "RenameView",
            "SetDisplayName",
            "SetFormula",
            "SetSlateSort",
            "SetSlateState",
            "SetSummaryAssignment",
            "SetTopLevelFilters",
            "SetViewFilters",
            "SetViewKey",
        ]),
        "generated serializer census must cover every public BaseEdit discriminator"
    );
}
