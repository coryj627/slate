// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

use slate_core::bases::{BaseEdit, BaseWarningKind, SerializeError, parse_base, serialize_base};

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

    assert_eq!(edited, "views:\n");
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
