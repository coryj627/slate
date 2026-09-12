// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

use super::common::*;
use super::*;
use crate::PropertyValue;

#[test]
fn typed_key_cache_upgrade_reindexes_unchanged_files_and_never_guesses_old_identity() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"---\n1: old\n\"1\": twin\n---\nbody")
            .unwrap()
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let expected = session
        .get_file_metadata("note.md")
        .unwrap()
        .unwrap()
        .properties;
    let before = session.read_text("note.md").unwrap();
    // Freeze a real populated cache at schema 37, before identities existed.
    {
        let conn = session.conn.lock().unwrap();
        conn.execute_batch("ALTER TABLE properties DROP COLUMN key_identity; DELETE FROM schema_version WHERE version = 38;").unwrap();
    }
    drop(session);
    let reopened = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    let cached = reopened
        .get_file_metadata("note.md")
        .unwrap()
        .unwrap()
        .properties;
    assert_eq!(cached.len(), expected.len());
    for row in cached {
        assert!(
            reopened
                .set_property_by_identity(
                    "note.md",
                    &row.key_identity,
                    PropertyValue::Text("wrong".into()),
                    None
                )
                .is_err()
        );
    }
    reopened.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(
        reopened
            .get_file_metadata("note.md")
            .unwrap()
            .unwrap()
            .properties,
        expected
    );
    assert_eq!(reopened.read_text("note.md").unwrap(), before);
}

#[test]
fn literal_dotted_string_identity_does_not_edit_a_nested_path() {
    let source = "---\na:\n  b: nested\n\"a.b\": literal\n---\nbody";
    let properties = crate::extract_frontmatter(source).0;
    assert_ne!(properties[0].key_identity, properties[1].key_identity);
    assert!(
        crate::frontmatter::set_property_by_identity_in_source(
            source,
            &properties[0].key_identity,
            &PropertyValue::Text("wrong".into())
        )
        .is_err()
    );
    let after = crate::frontmatter::set_property_by_identity_in_source(
        source,
        &properties[1].key_identity,
        &PropertyValue::Text("edited".into()),
    )
    .unwrap();
    let properties_after = crate::extract_frontmatter(&after).0;
    assert_eq!(properties_after[0], properties[0]);
    assert_eq!(
        properties_after[1].value,
        PropertyValue::Text("edited".into())
    );
}

#[test]
fn typed_keys_set_delete_and_rename_without_touching_string_twins() {
    for (typed, name) in [
        ("1", "1"),
        ("true", "true"),
        ("null", "null"),
        ("1.5", "1.5"),
    ] {
        let original = format!("---\n{typed}: old\n\"{name}\": twin\n---\nbody\r\nπ\n");
        let (_tmp, session) = make_vault(|p| p.write_file("note.md", original.as_bytes()).unwrap());
        session.scan_initial(&CancelToken::new()).unwrap();
        let properties = session
            .get_file_metadata("note.md")
            .unwrap()
            .unwrap()
            .properties;
        assert_eq!(properties.len(), 2);
        assert_eq!(properties[0].key, properties[1].key);
        assert_ne!(properties[0].key_identity, properties[1].key_identity);
        let identity = &properties[0].key_identity;
        let hash = crate::vault::content_hash(original.as_bytes());
        let saved = session
            .set_property_by_identity(
                "note.md",
                identity,
                PropertyValue::Text("edited".into()),
                Some(&hash),
            )
            .unwrap();
        let source = session.read_text("note.md").unwrap();
        assert_eq!(
            crate::frontmatter::body_after_frontmatter(&source),
            "body\r\nπ\n"
        );
        let after = session
            .get_file_metadata("note.md")
            .unwrap()
            .unwrap()
            .properties;
        assert_eq!(after.len(), 2);
        assert_eq!(after[0].key_identity, *identity);
        assert_eq!(after[0].value, PropertyValue::Text("edited".into()));
        assert_eq!(after[1], properties[1]);

        // The exact identity travels through a real indexed rename preview/apply.
        let preview = session
            .rename_property_by_identity_across_vault(
                identity,
                "renamed",
                true,
                &CancelToken::new(),
            )
            .unwrap();
        assert_eq!(preview.affected.len(), 1);
        assert!(preview.failed.is_empty());
        assert_eq!(session.read_text("note.md").unwrap(), source);
        let apply = session
            .rename_property_by_identity_across_vault(
                identity,
                "renamed",
                false,
                &CancelToken::new(),
            )
            .unwrap();
        assert_eq!(apply.affected.len(), 1);
        assert_eq!(
            preview.affected[0].after_excerpt,
            apply.affected[0].after_excerpt
        );
        assert_eq!(
            session
                .get_file_metadata("note.md")
                .unwrap()
                .unwrap()
                .properties[0],
            properties[1]
        );

        // Reset and exercise actual deletion, including CAS and the string twin.
        session.save_text("note.md", &source, None).unwrap();
        session
            .delete_property_by_identity("note.md", identity, Some(&saved.new_content_hash))
            .unwrap();
        let remaining = session
            .get_file_metadata("note.md")
            .unwrap()
            .unwrap()
            .properties;
        assert_eq!(remaining, vec![properties[1].clone()]);
        assert_eq!(
            crate::frontmatter::body_after_frontmatter(&session.read_text("note.md").unwrap()),
            "body\r\nπ\n"
        );
    }
}

#[test]
fn typed_rename_can_convert_integer_to_same_spelling_string_and_preserves_null_value() {
    let (_tmp, session) =
        make_vault(|p| p.write_file("note.md", b"---\n1: null\n---\nbody").unwrap());
    session.scan_initial(&CancelToken::new()).unwrap();
    let identity = session
        .get_file_metadata("note.md")
        .unwrap()
        .unwrap()
        .properties[0]
        .key_identity
        .clone();
    let report = session
        .rename_property_by_identity_across_vault(&identity, "1", false, &CancelToken::new())
        .unwrap();
    assert_eq!(report.affected.len(), 1);
    let source = session.read_text("note.md").unwrap();
    let yaml =
        yaml_rust2::YamlLoader::load_from_str(&crate::split_note(&source).fm_source).unwrap();
    assert_eq!(yaml[0]["1"], yaml_rust2::Yaml::Null);
    assert_eq!(
        session
            .get_file_metadata("note.md")
            .unwrap()
            .unwrap()
            .properties[0]
            .key_identity,
        "1"
    );
}

#[test]
fn projected_rename_handles_unique_typed_keys_and_refuses_ambiguous_names() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("unique.md", b"---\ntrue: old\n---\nbody")
            .unwrap();
        p.write_file("ambiguous.md", b"---\ntrue: old\n\"true\": twin\n---\nbody")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let before = session.read_text("ambiguous.md").unwrap();
    let report = session
        .rename_property_across_vault("true", "renamed", false, &CancelToken::new())
        .unwrap();
    assert_eq!(report.affected.len(), 1);
    assert_eq!(report.failed.len(), 1);
    assert_eq!(session.read_text("ambiguous.md").unwrap(), before);
}

#[test]
fn typed_key_stale_hash_conflicts_and_last_delete_removes_shell() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"---\nnull: old\n---\nbody")
            .unwrap()
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let identity = session
        .get_file_metadata("note.md")
        .unwrap()
        .unwrap()
        .properties[0]
        .key_identity
        .clone();
    assert!(matches!(
        session.delete_property_by_identity("note.md", &identity, Some("stale")),
        Err(VaultError::WriteConflict { .. })
    ));
    assert!(session.read_text("note.md").unwrap().contains("null: old"));
    session
        .delete_property_by_identity("note.md", &identity, None)
        .unwrap();
    assert_eq!(session.read_text("note.md").unwrap(), "body");
}

#[test]
fn string_names_that_look_like_tokens_do_not_select_typed_keys() {
    let (_tmp, session) =
        make_vault(|p| p.write_file("note.md", b"---\n1: old\n---\nbody").unwrap());
    session.scan_initial(&CancelToken::new()).unwrap();
    let typed = session
        .get_file_metadata("note.md")
        .unwrap()
        .unwrap()
        .properties[0]
        .key_identity
        .clone();
    let literal = crate::string_property_key_identity(&typed);
    session
        .set_property_by_identity(
            "note.md",
            &literal,
            PropertyValue::Text("literal".into()),
            None,
        )
        .unwrap();
    let properties = session
        .get_file_metadata("note.md")
        .unwrap()
        .unwrap()
        .properties;
    assert_eq!(properties.len(), 2);
    assert_eq!(properties[0].value, PropertyValue::Text("old".into()));
    assert_eq!(properties[1].key, typed);
    assert_eq!(properties[1].key_identity, literal);
}

#[test]
fn forged_real_identity_cannot_inject_yaml() {
    let source = "---\n1.5: old\n---\nbody";
    let identity = crate::extract_frontmatter(source).0[0]
        .key_identity
        .replace("1.5", "1.5\\nother: injected");
    assert!(
        crate::frontmatter::set_property_by_identity_in_source(
            source,
            &identity,
            &PropertyValue::Text("new".into())
        )
        .is_err()
    );
    assert!(crate::frontmatter::delete_property_by_identity_in_source(source, &identity).is_err());
}

#[test]
fn typed_key_rename_reports_mapping_destination_as_collision() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"---\n1: old\ndest:\n  inner: safe\n---\nbody")
            .unwrap()
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let before = session.read_text("note.md").unwrap();
    let identity = session
        .get_file_metadata("note.md")
        .unwrap()
        .unwrap()
        .properties[0]
        .key_identity
        .clone();
    for preview in [true, false] {
        let report = session
            .rename_property_by_identity_across_vault(
                &identity,
                "dest",
                preview,
                &CancelToken::new(),
            )
            .unwrap();
        assert!(report.affected.is_empty());
        assert!(report.failed.is_empty());
        assert_eq!(report.skipped.len(), 1);
        assert_eq!(report.skipped[0].reason, RenameSkipReason::KeyCollision);
        assert_eq!(session.read_text("note.md").unwrap(), before);
    }
}

#[test]
fn typed_key_rename_preview_shows_the_selected_string_twin() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "note.md",
            b"---\n1: integer\ngap: one\nmore: two\n\"1\": string\n---\nbody",
        )
        .unwrap()
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let report = session
        .rename_property_by_identity_across_vault("1", "renamed", true, &CancelToken::new())
        .unwrap();
    assert_eq!(report.affected.len(), 1);
    assert!(report.affected[0].before_excerpt.contains("string"));
    assert!(!report.affected[0].before_excerpt.contains("integer"));
}
