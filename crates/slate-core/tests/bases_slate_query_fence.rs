// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

use slate_core::bases::{
    SlateQueryFenceClassification, SlateQueryFenceError, classify_slate_query_fence,
};

#[test]
fn classifies_block_mapping_reference_with_inline_comments() {
    let source = r#"
query: Saved Notes # references a saved query by name
view: Main # renderer override
"#;

    assert_eq!(
        classify_slate_query_fence(source),
        Ok(SlateQueryFenceClassification {
            query: Some("Saved Notes".to_string()),
            view: Some("Main".to_string()),
        })
    );
}

#[test]
fn classifies_flow_mapping_and_decodes_quoted_scalars() {
    let source = r#"{ query: "Saved\nNotes \u263A", view: 'Editor''s list' }"#;

    assert_eq!(
        classify_slate_query_fence(source),
        Ok(SlateQueryFenceClassification {
            query: Some("Saved\nNotes ☺".to_string()),
            view: Some("Editor's list".to_string()),
        })
    );
}

#[test]
fn classifies_folded_and_literal_block_scalars() {
    let source = r#"
query: >-
  Saved
  Notes
view: |-
  List
  view
"#;

    assert_eq!(
        classify_slate_query_fence(source),
        Ok(SlateQueryFenceClassification {
            query: Some("Saved Notes".to_string()),
            view: Some("List\nview".to_string()),
        })
    );
}

#[test]
fn root_without_top_level_query_is_inline_even_with_nested_query_or_view() {
    let source = r#"
view: ignored-in-inline-mode
views:
  - type: table
    name: Main
    query: nested-does-not-select-reference-mode
"#;

    assert_eq!(
        classify_slate_query_fence(source),
        Ok(SlateQueryFenceClassification {
            query: None,
            view: None,
        })
    );
}

#[test]
fn accepts_non_string_yaml_scalars_without_losing_reference_mode() {
    assert_eq!(
        classify_slate_query_fence("{ query: 42, view: true }"),
        Ok(SlateQueryFenceClassification {
            query: Some("42".to_string()),
            view: Some("true".to_string()),
        })
    );
}

#[test]
fn non_scalar_query_fails_loud_instead_of_becoming_inline() {
    assert_eq!(
        classify_slate_query_fence("query: [Saved, Notes]\n"),
        Err(SlateQueryFenceError::NonScalarQuery {
            actual: "array".to_string(),
        })
    );
}

#[test]
fn non_scalar_view_fails_loud_in_reference_mode() {
    assert_eq!(
        classify_slate_query_fence("query: Saved Notes\nview: { type: table }\n"),
        Err(SlateQueryFenceError::NonScalarView {
            actual: "hash".to_string(),
        })
    );
}

#[test]
fn empty_or_null_query_fails_loud() {
    assert_eq!(
        classify_slate_query_fence("query: ''\n"),
        Err(SlateQueryFenceError::EmptyQuery)
    );
    assert_eq!(
        classify_slate_query_fence("query: null\n"),
        Err(SlateQueryFenceError::InvalidQueryScalar {
            actual: "null".to_string(),
        })
    );
}

#[test]
fn malformed_yaml_and_multiple_documents_are_typed_errors() {
    assert!(matches!(
        classify_slate_query_fence("query: [unterminated\n"),
        Err(SlateQueryFenceError::InvalidYaml { .. })
    ));
    assert_eq!(
        classify_slate_query_fence("query: Saved\n---\nviews: []\n"),
        Err(SlateQueryFenceError::MultipleDocuments { count: 2 })
    );
}
