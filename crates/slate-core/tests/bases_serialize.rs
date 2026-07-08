// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

use slate_core::bases::{BaseEdit, SerializeError, parse_base, serialize_base};

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
    ]
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

    assert!(edited.contains("  ppu: \"price / 100\"\n"));
    assert!(edited.starts_with("# Obsidian-authored shape"));
    assert_eq!(
        edited.replace("  ppu: \"price / 100\"\n", "  ppu: 'price / pages'\n"),
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

    assert!(edited.contains("    displayName: \"State\"\n"));
    assert_eq!(
        edited.replace("    displayName: \"State\"\n", "    displayName: Status\n"),
        source
    );
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

    assert!(edited.contains("      note.status: \"Count\"\n"));
    assert_eq!(
        edited.replace(
            "      note.status: \"Count\"\n",
            "      note.status: Unique\n"
        ),
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
