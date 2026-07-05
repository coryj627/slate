// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `VaultSession` tests — set_property, delete_property, rename_property_across_vault, files_with_property.
//!
//! Extracted from `crates/slate-core/src/session.rs` as part of #272.

#![allow(clippy::too_many_lines)]

use super::common::*;
use super::*;

#[test]
fn files_with_property_matches_atomic_value_case_insensitively() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/match.md", b"---\nauthor: Alice\n---\nbody")
            .unwrap();
        p.write_file("notes/other.md", b"---\nauthor: Bob\n---\nbody")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let page = session
        .files_with_property("author", "alice", Paging::first(100))
        .unwrap();
    let paths: Vec<&str> = page.items.iter().map(|f| f.path.as_str()).collect();
    assert_eq!(paths, vec!["notes/match.md"]);
    assert_eq!(page.total_filtered, 1);
}

#[test]
fn files_with_property_matches_inside_tag_list() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/a.md", b"---\ntags:\n  - alpha\n  - beta\n---\n")
            .unwrap();
        p.write_file("notes/b.md", b"---\ntags:\n  - gamma\n---\n")
            .unwrap();
        p.write_file("notes/c.md", b"---\ntags:\n  - beta\n---\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let page = session
        .files_with_property("tags", "beta", Paging::first(100))
        .unwrap();
    let mut paths: Vec<&str> = page.items.iter().map(|f| f.path.as_str()).collect();
    paths.sort();
    assert_eq!(paths, vec!["notes/a.md", "notes/c.md"]);
}

#[test]
fn rescan_with_changed_frontmatter_rewrites_properties() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("notes/note.md", b"---\nstatus: draft\n---\nbody")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let md = session.get_file_metadata("notes/note.md").unwrap().unwrap();
    assert_eq!(md.properties[0].key, "status");

    // Change the property + body so the scanner picks it up via
    // the content-hash fast-path miss.
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider
        .write_file(
            "notes/note.md",
            b"---\nstatus: published\n---\nbody changed",
        )
        .unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();

    let md = session.get_file_metadata("notes/note.md").unwrap().unwrap();
    assert_eq!(md.properties.len(), 1);
    // Value changed from draft → published. We don't care about
    // the exact internal representation, just that the new value
    // is reflected.
    match &md.properties[0].value {
        crate::PropertyValue::Text(s) => assert_eq!(s, "published"),
        other => panic!("expected Text, got {other:?}"),
    }
}

#[test]
fn fast_path_does_not_rewrite_properties() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/note.md", b"---\ntitle: Stable\n---\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let before = session.get_file_metadata("notes/note.md").unwrap().unwrap();
    assert_eq!(before.properties.len(), 1);

    // Second scan with no file changes: fast path skips per-file
    // work, so the properties row stays exactly as it was.
    session.scan_initial(&CancelToken::new()).unwrap();
    let after = session.get_file_metadata("notes/note.md").unwrap().unwrap();
    assert_eq!(after.properties, before.properties);
}

#[test]
fn files_with_property_matches_boolean_value() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/published.md", b"---\npublished: true\n---\nbody")
            .unwrap();
        p.write_file("notes/draft.md", b"---\npublished: false\n---\nbody")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let page = session
        .files_with_property("published", "true", Paging::first(100))
        .unwrap();
    assert_eq!(
        page.items
            .iter()
            .map(|f| f.path.as_str())
            .collect::<Vec<_>>(),
        vec!["notes/published.md"]
    );
    let page = session
        .files_with_property("published", "false", Paging::first(100))
        .unwrap();
    assert_eq!(
        page.items
            .iter()
            .map(|f| f.path.as_str())
            .collect::<Vec<_>>(),
        vec!["notes/draft.md"]
    );
}

#[test]
fn files_with_property_matches_numeric_value() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/p1.md", b"---\npriority: 1\n---\n")
            .unwrap();
        p.write_file("notes/p2.md", b"---\npriority: 2\n---\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let page = session
        .files_with_property("priority", "1", Paging::first(100))
        .unwrap();
    assert_eq!(
        page.items
            .iter()
            .map(|f| f.path.as_str())
            .collect::<Vec<_>>(),
        vec!["notes/p1.md"]
    );
}

#[test]
fn files_with_property_pagination_dedupes_multi_match_files() {
    // Regression for the Codoki PR-82 callout: a single file
    // with multiple list-element matches under the same key must
    // be counted exactly once across the paged results, with
    // cursor-driven pagination yielding every file once with no
    // duplicates.
    let (_tmp, session) = make_vault(|p| {
        for letter in ["a", "b", "c", "d", "e"] {
            p.write_file(
                &format!("notes/{}.md", letter),
                // Same tag appears twice in the list so each
                // file produces two json_each rows; DISTINCT
                // must collapse them.
                b"---\ntags:\n  - common\n  - common-alias\n  - common\n---\n",
            )
            .unwrap();
        }
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let mut seen: Vec<String> = Vec::new();
    let mut cursor: Option<String> = None;
    loop {
        let paging = match &cursor {
            Some(c) => Paging::after(c.clone(), 2),
            None => Paging::first(2),
        };
        let page = session
            .files_with_property("tags", "common", paging)
            .unwrap();
        for f in &page.items {
            seen.push(f.path.clone());
        }
        cursor = page.next_cursor;
        if cursor.is_none() {
            break;
        }
    }
    let expected: Vec<String> = ["a", "b", "c", "d", "e"]
        .iter()
        .map(|s| format!("notes/{}.md", s))
        .collect();
    assert_eq!(seen, expected, "paging dropped or duplicated rows");
}

// --- list_property_keys / files_with_property_key (M-5, #536) --------

#[test]
fn list_property_keys_returns_distinct_keys_sorted_with_file_counts() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"---\nauthor: Alice\nstatus: draft\n---\n")
            .unwrap();
        p.write_file("b.md", b"---\nauthor: Bob\n---\n").unwrap();
        p.write_file("c.md", b"---\nstatus: done\ntags:\n  - x\n---\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let keys = session.list_property_keys().unwrap();
    // Key-sorted: author, status, tags.
    let pairs: Vec<(&str, u64)> = keys
        .iter()
        .map(|k| (k.key.as_str(), k.file_count))
        .collect();
    assert_eq!(
        pairs,
        vec![("author", 2), ("status", 2), ("tags", 1)],
        "keys must be DISTINCT, key-sorted, with distinct-file counts"
    );
}

#[test]
fn list_property_keys_counts_each_file_once_per_key() {
    // A file carrying the same key across two dotted rows (person.name
    // + person) must still count once for each distinct key.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("one.md", b"---\nperson:\n  name: A\n  email: a@x\n---\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let keys = session.list_property_keys().unwrap();
    // Every distinct dotted key is present exactly once, each on one
    // file.
    assert!(keys.iter().all(|k| k.file_count == 1));
    // No duplicate keys in the output.
    let mut names: Vec<&str> = keys.iter().map(|k| k.key.as_str()).collect();
    let dedup_len = {
        let mut n = names.clone();
        n.dedup();
        n.len()
    };
    names.sort();
    assert_eq!(names.len(), dedup_len, "keys must be distinct");
}

#[test]
fn list_property_keys_empty_when_no_frontmatter() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("plain.md", b"# no frontmatter here\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    assert!(session.list_property_keys().unwrap().is_empty());
}

#[test]
fn files_with_property_key_matches_any_value() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"---\nstatus: draft\n---\n").unwrap();
        p.write_file("b.md", b"---\nstatus: done\n---\n").unwrap();
        p.write_file("c.md", b"---\nauthor: X\n---\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let page = session
        .files_with_property_key("status", Paging::first(100))
        .unwrap();
    let mut paths: Vec<&str> = page.items.iter().map(|f| f.path.as_str()).collect();
    paths.sort();
    // Both `draft` and `done` — the value is irrelevant, only the key.
    assert_eq!(paths, vec!["a.md", "b.md"]);
    assert_eq!(page.total_filtered, 2);
}

#[test]
fn files_with_property_key_matches_list_valued_key() {
    // A key whose value is a list (`tags`) still counts the file — the
    // parent `properties` row carries the key even though elements live
    // in `properties_list_values`.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"---\ntags:\n  - x\n  - y\n---\n")
            .unwrap();
        p.write_file("b.md", b"---\ntitle: T\n---\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let page = session
        .files_with_property_key("tags", Paging::first(100))
        .unwrap();
    let paths: Vec<&str> = page.items.iter().map(|f| f.path.as_str()).collect();
    assert_eq!(paths, vec!["a.md"]);
}

#[test]
fn files_with_property_key_missing_key_is_empty_not_error() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"---\nstatus: draft\n---\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let page = session
        .files_with_property_key("nonexistent", Paging::first(100))
        .unwrap();
    assert!(page.items.is_empty());
    assert_eq!(page.total_filtered, 0);
    assert!(page.next_cursor.is_none());
}

#[test]
fn files_with_property_key_pagination_drains_each_file_once() {
    // The CLI drains this via next_cursor; a file carrying the key on
    // two dotted rows must appear exactly once, and paging must cover
    // every match with no duplicate or dropped rows.
    let (_tmp, session) = make_vault(|p| {
        for letter in ["a", "b", "c", "d", "e"] {
            p.write_file(
                &format!("{letter}.md"),
                // Two dotted rows under `meta` → two `properties` rows
                // with key `meta.x` / `meta.y`; we query `meta.x`, which
                // is on exactly one row per file, but DISTINCT protects
                // against any JOIN fan-out.
                b"---\nmeta:\n  x: 1\n  y: 2\n---\n",
            )
            .unwrap();
        }
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let mut seen: Vec<String> = Vec::new();
    let mut cursor: Option<String> = None;
    loop {
        let paging = match &cursor {
            Some(c) => Paging::after(c.clone(), 2),
            None => Paging::first(2),
        };
        let page = session.files_with_property_key("meta.x", paging).unwrap();
        for f in &page.items {
            seen.push(f.path.clone());
        }
        cursor = page.next_cursor;
        if cursor.is_none() {
            break;
        }
    }
    let expected: Vec<String> = ["a", "b", "c", "d", "e"]
        .iter()
        .map(|s| format!("{s}.md"))
        .collect();
    assert_eq!(seen, expected, "paging dropped or duplicated rows");
}

#[test]
fn files_without_frontmatter_have_empty_properties() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/plain.md", b"# heading only\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let md = session
        .get_file_metadata("notes/plain.md")
        .unwrap()
        .unwrap();
    assert!(md.properties.is_empty());
}

#[test]
fn set_property_adds_new_key_and_reindexes() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"---\ntitle: Hi\n---\nbody\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let report = session
        .set_property(
            "note.md",
            "author",
            crate::PropertyValue::Text("Cory".to_string()),
            None,
        )
        .unwrap();
    assert!(!report.new_content_hash.is_empty());

    // Both keys land in the index after reparse.
    let bundle = session
        .note_load_bundle("note.md", Paging::first(50))
        .unwrap();
    let keys: Vec<&str> = bundle.properties.iter().map(|p| p.key.as_str()).collect();
    assert_eq!(keys, vec!["title", "author"]);

    // Body byte-equal on disk after the edit.
    let raw = session.read_text("note.md").unwrap();
    assert!(raw.ends_with("body\n"));
}

#[test]
fn set_property_updates_existing_key_without_reordering() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"---\nalpha: 1\nbeta: 2\ngamma: 3\n---\nbody\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    session
        .set_property("note.md", "beta", crate::PropertyValue::Integer(42), None)
        .unwrap();

    let bundle = session
        .note_load_bundle("note.md", Paging::first(50))
        .unwrap();
    let keys: Vec<&str> = bundle.properties.iter().map(|p| p.key.as_str()).collect();
    assert_eq!(
        keys,
        vec!["alpha", "beta", "gamma"],
        "existing key must keep its position"
    );
    let beta_val = bundle
        .properties
        .iter()
        .find(|p| p.key == "beta")
        .map(|p| &p.value);
    assert_eq!(beta_val, Some(&crate::PropertyValue::Integer(42)));
}

#[test]
fn set_property_synthesizes_frontmatter_when_none_exists() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"# Note\n\nBody.\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    session
        .set_property(
            "note.md",
            "title",
            crate::PropertyValue::Text("Hi".to_string()),
            None,
        )
        .unwrap();

    let raw = session.read_text("note.md").unwrap();
    assert!(raw.starts_with("---\n"));
    assert!(raw.ends_with("# Note\n\nBody.\n"));
    let bundle = session
        .note_load_bundle("note.md", Paging::first(50))
        .unwrap();
    assert_eq!(bundle.properties.len(), 1);
    assert_eq!(bundle.properties[0].key, "title");
}

#[test]
fn set_property_returns_write_conflict_on_stale_hash() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"---\ntitle: Hi\n---\nbody\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let bogus_hash = "0".repeat(64);
    let err = session
        .set_property(
            "note.md",
            "author",
            crate::PropertyValue::Text("X".to_string()),
            Some(&bogus_hash),
        )
        .unwrap_err();
    match err {
        VaultError::WriteConflict {
            expected_content_hash,
            ..
        } => assert_eq!(expected_content_hash, bogus_hash),
        other => panic!("expected WriteConflict, got {other:?}"),
    }
}

#[test]
fn set_property_returns_malformed_frontmatter_when_yaml_broken() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"---\ntitle: \"unterminated\n---\nbody\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let err = session
        .set_property(
            "note.md",
            "author",
            crate::PropertyValue::Text("X".to_string()),
            None,
        )
        .unwrap_err();
    match err {
        VaultError::MalformedFrontmatter { path, .. } => assert_eq!(path, "note.md"),
        other => panic!("expected MalformedFrontmatter, got {other:?}"),
    }
    // File on disk is untouched.
    let raw = session.read_text("note.md").unwrap();
    assert!(raw.starts_with("---\ntitle: \"unterminated"));
}

#[test]
fn delete_property_removes_one_key_and_reindexes() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"---\ntitle: Hi\nauthor: Cory\n---\nbody\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    session.delete_property("note.md", "author", None).unwrap();

    let bundle = session
        .note_load_bundle("note.md", Paging::first(50))
        .unwrap();
    let keys: Vec<&str> = bundle.properties.iter().map(|p| p.key.as_str()).collect();
    assert_eq!(keys, vec!["title"]);
}

#[test]
fn delete_property_on_last_key_strips_block_entirely() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"---\ntitle: Hi\n---\nbody\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    session.delete_property("note.md", "title", None).unwrap();

    let raw = session.read_text("note.md").unwrap();
    assert_eq!(raw, "body\n", "the whole --- block must be gone");
    let bundle = session
        .note_load_bundle("note.md", Paging::first(50))
        .unwrap();
    assert!(bundle.properties.is_empty());
}

#[test]
fn delete_property_missing_key_short_circuits_without_write() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"---\ntitle: Hi\n---\nbody\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let before_oplog = session.read_oplog("note.md").unwrap();

    let report = session
        .delete_property("note.md", "nonexistent", None)
        .unwrap();
    // Hash matches the cached one — file untouched.
    let after = session
        .get_file_metadata("note.md")
        .unwrap()
        .unwrap()
        .content_hash;
    assert_eq!(report.new_content_hash, after);

    // No op-log entry was appended for a no-op.
    let after_oplog = session.read_oplog("note.md").unwrap();
    assert_eq!(after_oplog.len(), before_oplog.len());
}

#[test]
fn delete_property_short_circuit_hash_matches_bytes_we_read() {
    // Audit #174: the no-op short-circuit used to do a fresh
    // disk read for the SaveReport's hash, which could race
    // against the `read_text` that preceded it. The fix is to
    // hash the bytes we actually read. Verify with a provider
    // that mutates the file between the two reads — the
    // SaveReport's hash must still match the original bytes,
    // not the mutated ones.
    let tmp = tempfile::tempdir().unwrap();
    let setup = FsVaultProvider::new(tmp.path().to_path_buf());
    setup
        .write_file("note.md", b"---\ntitle: T\n---\nbody A\n")
        .unwrap();
    let raced_inner = FsVaultProvider::new(tmp.path().to_path_buf());
    let provider = Arc::new(RaceOnFirstReadProvider {
        inner: raced_inner,
        race_path: "note.md".to_string(),
        post_read_bytes: b"---\ntitle: T\n---\nbody MUTATED\n".to_vec(),
        raced: std::sync::atomic::AtomicBool::new(false),
    });
    let config = SessionConfig::new(tmp.path().join(".slate"));
    let session = VaultSession::open(provider, config).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();

    // Delete a key that isn't there → hits the short-circuit.
    // expected_content_hash = None on purpose to follow the
    // CLI/scripted path the audit calls out.
    let report = session
        .delete_property("note.md", "nonexistent", None)
        .unwrap();

    // The SaveReport's hash should equal the hash of the bytes
    // read_text observed (the original "body A\n" version),
    // NOT the mutated bytes on disk.
    let expected_hash = crate::vault::content_hash(b"---\ntitle: T\n---\nbody A\n");
    assert_eq!(report.new_content_hash, expected_hash);
    assert_eq!(
        report.new_size_bytes,
        "---\ntitle: T\n---\nbody A\n".len() as u64
    );
}

#[test]
fn delete_property_missing_key_still_validates_hash() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"---\ntitle: Hi\n---\nbody\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let bogus_hash = "0".repeat(64);
    let err = session
        .delete_property("note.md", "nonexistent", Some(&bogus_hash))
        .unwrap_err();
    assert!(matches!(err, VaultError::WriteConflict { .. }));
}

#[test]
fn rename_property_dry_run_matches_apply() {
    // Three files: two carry the old key, one doesn't. Dry-run
    // then apply on the same session — the affected/skipped sets
    // should match.
    let setup = |p: &FsVaultProvider| {
        p.write_file("a.md", b"---\nauthor: Cory\n---\nbody A\n")
            .unwrap();
        p.write_file("b.md", b"---\ntitle: B\nauthor: Cory\n---\nbody B\n")
            .unwrap();
        p.write_file("c.md", b"---\ntitle: C\n---\nbody C\n")
            .unwrap();
    };

    let (_tmp1, dry_session) = make_vault(setup);
    dry_session.scan_initial(&CancelToken::new()).unwrap();
    let dry = dry_session
        .rename_property_across_vault("author", "by", true, &CancelToken::new())
        .unwrap();
    assert!(dry.failed.is_empty());
    assert_eq!(dry.affected.len(), 2);
    assert!(dry.affected.iter().all(|a| !a.applied));
    let mut dry_paths: Vec<&str> = dry.affected.iter().map(|a| a.path.as_str()).collect();
    dry_paths.sort();
    assert_eq!(dry_paths, vec!["a.md", "b.md"]);

    let (_tmp2, apply_session) = make_vault(setup);
    apply_session.scan_initial(&CancelToken::new()).unwrap();
    let apply = apply_session
        .rename_property_across_vault("author", "by", false, &CancelToken::new())
        .unwrap();
    assert!(apply.failed.is_empty());
    assert_eq!(apply.affected.len(), 2);
    assert!(apply.affected.iter().all(|a| a.applied));
    let mut apply_paths: Vec<&str> = apply.affected.iter().map(|a| a.path.as_str()).collect();
    apply_paths.sort();
    assert_eq!(apply_paths, dry_paths);

    // Verify on disk: a.md + b.md now have `by`, not `author`.
    let a = apply_session.read_text("a.md").unwrap();
    assert!(a.contains("by:") && !a.contains("author:"));
    let b = apply_session.read_text("b.md").unwrap();
    assert!(b.contains("by:") && !b.contains("author:"));
    // c.md is untouched.
    let c = apply_session.read_text("c.md").unwrap();
    assert_eq!(c, "---\ntitle: C\n---\nbody C\n");
}

#[test]
fn rename_property_skips_files_with_key_collision() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "collide.md",
            b"---\nauthor: Cory\nby: Existing\n---\nbody\n",
        )
        .unwrap();
        p.write_file("clean.md", b"---\nauthor: Cory\n---\nbody\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let report = session
        .rename_property_across_vault("author", "by", false, &CancelToken::new())
        .unwrap();
    assert_eq!(report.affected.len(), 1);
    assert_eq!(report.affected[0].path, "clean.md");
    assert_eq!(report.skipped.len(), 1);
    assert_eq!(report.skipped[0].path, "collide.md");
    assert_eq!(report.skipped[0].reason, RenameSkipReason::KeyCollision);

    // collide.md preserved as-is.
    let raw = session.read_text("collide.md").unwrap();
    assert_eq!(raw, "---\nauthor: Cory\nby: Existing\n---\nbody\n");
}

#[test]
fn rename_property_skips_tags_boundary_crossing_with_list_value() {
    // Audit #180B. Two scenarios:
    //   - `tags` → `labels` with a tag list → would lose `#`
    //     prefixes on round-trip (reader's `tags`-keyname
    //     classification doesn't apply under `labels`).
    //   - `authors` → `tags` with a plain string list → would
    //     gain `TagList` classification under `tags`.
    // Both refused; scalar-valued renames across the same
    // boundary are unaffected.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"---\ntags:\n  - foo\n  - bar\n---\nbody\n")
            .unwrap();
        p.write_file("b.md", b"---\nauthors:\n  - alice\n  - bob\n---\nbody\n")
            .unwrap();
        p.write_file("c.md", b"---\ntags: scalar-not-list\n---\nbody\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    // Case 1: tags → labels with list value, skipped.
    let report = session
        .rename_property_across_vault("tags", "labels", false, &CancelToken::new())
        .unwrap();
    // a.md (list-valued tags) skipped; c.md (scalar-valued tags)
    // applied.
    let skipped_paths: Vec<&str> = report
        .skipped
        .iter()
        .filter(|s| s.reason == RenameSkipReason::TagsKeyTypeDrift)
        .map(|s| s.path.as_str())
        .collect();
    assert_eq!(skipped_paths, vec!["a.md"]);
    let applied_paths: Vec<&str> = report
        .affected
        .iter()
        .filter(|a| a.applied)
        .map(|a| a.path.as_str())
        .collect();
    assert!(applied_paths.contains(&"c.md"));
    // a.md's disk content untouched.
    let raw = session.read_text("a.md").unwrap();
    assert_eq!(raw, "---\ntags:\n  - foo\n  - bar\n---\nbody\n");

    // Case 2: authors → tags with list value, skipped.
    let report = session
        .rename_property_across_vault("authors", "tags", false, &CancelToken::new())
        .unwrap();
    let skipped_paths: Vec<&str> = report
        .skipped
        .iter()
        .filter(|s| s.reason == RenameSkipReason::TagsKeyTypeDrift)
        .map(|s| s.path.as_str())
        .collect();
    assert_eq!(skipped_paths, vec!["b.md"]);
    let raw = session.read_text("b.md").unwrap();
    assert_eq!(raw, "---\nauthors:\n  - alice\n  - bob\n---\nbody\n");
}

struct RaceOnFirstReadProvider {
    inner: FsVaultProvider,
    race_path: String,
    /// Bytes the inner file gets rewritten to on the first read of
    /// `race_path`. Picked so the file still carries the old key
    /// (so it isn't `NoSuchKey`-skipped) but has a different
    /// content hash than the bytes the rename's `read_text` saw.
    post_read_bytes: Vec<u8>,
    raced: std::sync::atomic::AtomicBool,
}

impl crate::VaultProvider for RaceOnFirstReadProvider {
    fn list_dir(&self, relative: &str) -> Result<Vec<crate::DirEntry>, VaultError> {
        self.inner.list_dir(relative)
    }
    fn read_file(&self, relative: &str) -> Result<Vec<u8>, VaultError> {
        self.inner.read_file(relative)
    }
    fn read_file_with_cap(&self, relative: &str, max_bytes: u64) -> Result<Vec<u8>, VaultError> {
        let bytes = self.inner.read_file_with_cap(relative, max_bytes)?;
        if relative == self.race_path && !self.raced.swap(true, std::sync::atomic::Ordering::SeqCst)
        {
            // Mutate the underlying file so the next read in
            // save_text's hash check sees different bytes than
            // the ones the rename's read_text just captured.
            self.inner
                .write_file(relative, &self.post_read_bytes)
                .unwrap();
        }
        Ok(bytes)
    }
    fn write_file(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
        self.inner.write_file(relative, contents)
    }
    fn delete(&self, relative: &str) -> Result<(), VaultError> {
        self.inner.delete(relative)
    }
    fn rename(&self, from: &str, to: &str) -> Result<(), VaultError> {
        self.inner.rename(from, to)
    }
    fn create_dir(&self, relative: &str) -> Result<(), VaultError> {
        self.inner.create_dir(relative)
    }
    fn stat(&self, relative: &str) -> Result<crate::FileStat, VaultError> {
        self.inner.stat(relative)
    }
    fn watch(
        &self,
        sink: Arc<dyn crate::FileEventSink>,
    ) -> Result<Option<crate::WatchHandle>, VaultError> {
        self.inner.watch(sink)
    }
}

#[test]
fn rename_property_records_write_conflict_when_file_changes_mid_save() {
    // Use a provider that flips `b.md` on disk between the
    // rename's per-file read and the save's hash check. That's
    // the exact shape of "external writer modified the file
    // between read and save" the issue spec calls out.
    let tmp = tempfile::tempdir().unwrap();
    let setup_provider = FsVaultProvider::new(tmp.path().to_path_buf());
    setup_provider
        .write_file("a.md", b"---\nauthor: Cory\n---\nbody A\n")
        .unwrap();
    setup_provider
        .write_file("b.md", b"---\nauthor: Cory\n---\nbody B\n")
        .unwrap();

    let raced_inner = FsVaultProvider::new(tmp.path().to_path_buf());
    let provider = Arc::new(RaceOnFirstReadProvider {
        inner: raced_inner,
        race_path: "b.md".to_string(),
        // Same key still present (so the file isn't NoSuchKey-
        // skipped after the swap if the rename re-read), but
        // body text differs.
        post_read_bytes: b"---\nauthor: Cory\n---\nbody B EXTERNALLY MUTATED\n".to_vec(),
        raced: std::sync::atomic::AtomicBool::new(false),
    });
    let config = SessionConfig::new(tmp.path().join(".slate"));
    let session = VaultSession::open(provider, config).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();

    let report = session
        .rename_property_across_vault("author", "by", false, &CancelToken::new())
        .unwrap();

    // a.md applied cleanly; b.md raced and failed.
    let applied_paths: Vec<&str> = report
        .affected
        .iter()
        .filter(|a| a.applied)
        .map(|a| a.path.as_str())
        .collect();
    assert_eq!(applied_paths, vec!["a.md"]);
    let failed_paths: Vec<&str> = report.failed.iter().map(|f| f.path.as_str()).collect();
    assert_eq!(failed_paths, vec!["b.md"]);
    assert_eq!(report.failed[0].kind, RenameFailureKind::WriteConflict);

    // a.md on disk reflects the rename; b.md keeps the externally-
    // mutated body untouched by the rename.
    let a = session.read_text("a.md").unwrap();
    assert!(a.contains("by:") && !a.contains("author:"));
    let b = session.read_text("b.md").unwrap();
    assert!(
        b.contains("EXTERNALLY MUTATED") && b.contains("author:"),
        "raced file must retain the external writer's content, got {b:?}"
    );
}

#[test]
fn rename_property_cancellation_stops_subsequent_files() {
    let (_tmp, session) = make_vault(|p| {
        for i in 0..5 {
            p.write_file(
                &format!("note{i}.md"),
                format!("---\nauthor: A{i}\n---\nbody {i}\n").as_bytes(),
            )
            .unwrap();
        }
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let cancel = CancelToken::new();
    cancel.cancel();
    let report = session
        .rename_property_across_vault("author", "by", false, &cancel)
        .unwrap();
    // Pre-cancelled: every file lands in failed with Cancelled.
    assert!(report.affected.is_empty());
    assert_eq!(report.failed.len(), 5);
    assert!(
        report
            .failed
            .iter()
            .all(|f| f.kind == RenameFailureKind::Cancelled)
    );
    // Nothing was written.
    for i in 0..5 {
        let raw = session.read_text(&format!("note{i}.md")).unwrap();
        assert!(raw.contains("author:"), "note{i} must be untouched");
    }
}

#[test]
fn rename_property_preserves_list_values() {
    // Rename a list-valued key that doesn't cross the `tags`
    // boundary so the elements survive the round-trip cleanly.
    // (The `tags`-boundary crossing case is now a deliberate
    // skip per audit #180B — see
    // `rename_property_skips_tags_boundary_crossing_with_list_value`.)
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "note.md",
            b"---\ncategories:\n  - foo\n  - bar\n---\nbody\n",
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let report = session
        .rename_property_across_vault("categories", "topics", false, &CancelToken::new())
        .unwrap();
    assert_eq!(report.affected.len(), 1);

    let bundle = session
        .note_load_bundle("note.md", Paging::first(50))
        .unwrap();
    let topics = bundle.properties.iter().find(|p| p.key == "topics");
    match topics.map(|p| &p.value) {
        Some(crate::PropertyValue::List(items)) if items.len() == 2 => {}
        other => panic!("expected list value for `topics`, got {other:?}"),
    }
}

#[test]
fn rename_property_rejects_identical_keys() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"---\nauthor: Cory\n---\nbody\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let err = session
        .rename_property_across_vault("author", "author", false, &CancelToken::new())
        .unwrap_err();
    assert!(matches!(err, VaultError::InvalidArgument { .. }));
}

#[test]
fn rename_property_excerpt_ignores_block_scalar_continuations() {
    // Audit #178: the previous excerpt logic matched any line
    // whose first non-whitespace token started with `<key>:`,
    // so a continuation line inside a `|` block scalar that
    // happened to read `des: …` would steal the excerpt from
    // the real top-level `des:` key.
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "note.md",
            b"---\ntext: |\n  des: this is a continuation, not a key\nrealkey: hi\ndes: real value\n---\nbody\n",
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let report = session
        .rename_property_across_vault("des", "y", true, &CancelToken::new())
        .unwrap();
    assert_eq!(report.affected.len(), 1);
    let aff = &report.affected[0];
    // The excerpt must contain the real `des: real value` line,
    // not the block-scalar continuation.
    assert!(
        aff.before_excerpt.contains("des: real value"),
        "before_excerpt should include the real key line; got {:?}",
        aff.before_excerpt
    );
    // And the after_excerpt should include the renamed key
    // somewhere — yaml-rust2 quotes `y` (a YAML 1.1 boolean
    // alias) on emit, so the line reads `"y": real value`.
    assert!(
        aff.after_excerpt.contains("real value")
            && (aff.after_excerpt.contains("y:") || aff.after_excerpt.contains("\"y\":")),
        "after_excerpt should include the renamed key; got {:?}",
        aff.after_excerpt
    );
}

#[test]
fn rename_property_rejects_dotted_keys() {
    let (_tmp, session) = make_vault(|_| {});
    let err = session
        .rename_property_across_vault("person.name", "name", false, &CancelToken::new())
        .unwrap_err();
    assert!(matches!(err, VaultError::InvalidArgument { .. }));
    let err = session
        .rename_property_across_vault("name", "person.name", false, &CancelToken::new())
        .unwrap_err();
    assert!(matches!(err, VaultError::InvalidArgument { .. }));
}

#[test]
fn rename_property_rejects_empty_keys() {
    let (_tmp, session) = make_vault(|_| {});
    let err = session
        .rename_property_across_vault("", "x", false, &CancelToken::new())
        .unwrap_err();
    assert!(matches!(err, VaultError::InvalidArgument { .. }));
    let err = session
        .rename_property_across_vault("x", "", false, &CancelToken::new())
        .unwrap_err();
    assert!(matches!(err, VaultError::InvalidArgument { .. }));
}
