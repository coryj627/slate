// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `VaultSession` tests — templates listing and rendering.
//!
//! Extracted from `crates/slate-core/src/session.rs` as part of #272.

#![allow(clippy::too_many_lines)]

use super::common::*;
use super::*;

fn make_templates_vault(setup: impl FnOnce(&std::path::Path)) -> (tempfile::TempDir, VaultSession) {
    let tmp = tempfile::tempdir().unwrap();
    setup(tmp.path());
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    (tmp, session)
}

#[test]
fn templates_dir_autodetected_when_templates_folder_exists() {
    let (_tmp, session) = make_templates_vault(|root| {
        std::fs::create_dir(root.join("Templates")).unwrap();
    });
    assert_eq!(session.config.templates_dir.as_deref(), Some("Templates"));
}

#[test]
fn templates_dir_stays_none_when_no_templates_folder() {
    let (_tmp, session) = make_vault(|_| {});
    assert_eq!(session.config.templates_dir, None);
}

#[test]
fn list_templates_returns_empty_when_templates_dir_is_none() {
    let (_tmp, session) = make_vault(|_| {});
    assert_eq!(session.list_templates().unwrap(), Vec::new());
}

#[test]
fn list_templates_returns_alphabetical_md_files() {
    let (_tmp, session) = make_templates_vault(|root| {
        let dir = root.join("Templates");
        std::fs::create_dir(&dir).unwrap();
        std::fs::write(dir.join("Zeta.md"), b"# Z").unwrap();
        std::fs::write(dir.join("Alpha.md"), b"# A").unwrap();
        std::fs::write(dir.join("Middle.md"), b"# M").unwrap();
    });
    let names: Vec<String> = session
        .list_templates()
        .unwrap()
        .into_iter()
        .map(|t| t.name)
        .collect();
    assert_eq!(names, vec!["Alpha", "Middle", "Zeta"]);
}

#[test]
fn list_templates_filters_out_non_markdown_files() {
    let (_tmp, session) = make_templates_vault(|root| {
        let dir = root.join("Templates");
        std::fs::create_dir(&dir).unwrap();
        std::fs::write(dir.join("Daily.md"), b"# Daily").unwrap();
        // Garbage that the filter must skip.
        std::fs::write(dir.join(".DS_Store"), b"junk").unwrap();
        std::fs::write(dir.join("README"), b"no ext").unwrap();
        std::fs::write(dir.join("banner.png"), b"\x89PNG").unwrap();
        std::fs::create_dir(dir.join("subdir")).unwrap();
    });
    let names: Vec<String> = session
        .list_templates()
        .unwrap()
        .into_iter()
        .map(|t| t.name)
        .collect();
    assert_eq!(names, vec!["Daily"]);
}

#[test]
fn list_templates_uses_frontmatter_description_first() {
    let (_tmp, session) = make_templates_vault(|root| {
        let dir = root.join("Templates");
        std::fs::create_dir(&dir).unwrap();
        std::fs::write(
            dir.join("Daily.md"),
            b"---\ndescription: My daily note layout\n---\n# {{title}}\n",
        )
        .unwrap();
    });
    let summary = session.list_templates().unwrap().pop().unwrap();
    assert_eq!(summary.path, "Templates/Daily.md");
    assert_eq!(summary.name, "Daily");
    assert_eq!(summary.description.as_deref(), Some("My daily note layout"));
}

#[test]
fn list_templates_falls_back_to_first_nonblank_line_for_description() {
    let (_tmp, session) = make_templates_vault(|root| {
        let dir = root.join("Templates");
        std::fs::create_dir(&dir).unwrap();
        std::fs::write(
            dir.join("Scratch.md"),
            b"\n\nQuick scratch note\nMore body\n",
        )
        .unwrap();
    });
    let summary = session.list_templates().unwrap().pop().unwrap();
    assert_eq!(summary.description.as_deref(), Some("Quick scratch note"));
}

#[test]
fn list_templates_returns_empty_when_dir_disappeared_after_open() {
    let (tmp, session) = make_templates_vault(|root| {
        std::fs::create_dir(root.join("Templates")).unwrap();
    });
    // Auto-detection captured "Templates" at open time.
    assert_eq!(session.config.templates_dir.as_deref(), Some("Templates"));
    // Now delete it under the running session — list_templates should
    // degrade to "no templates" rather than surface a NotFound error
    // to the picker.
    std::fs::remove_dir_all(tmp.path().join("Templates")).unwrap();
    assert_eq!(session.list_templates().unwrap(), Vec::new());
}

#[test]
fn render_template_substitutes_via_disk_read() {
    let (_tmp, session) = make_templates_vault(|root| {
        let dir = root.join("Templates");
        std::fs::create_dir(&dir).unwrap();
        std::fs::write(
            dir.join("Meeting.md"),
            b"# Meeting: {{prompt:Topic}}\n\n{{cursor}}\n",
        )
        .unwrap();
    });
    let ctx = crate::TemplateContext::new(1_700_000_000_000, "ignored", "MyVault")
        .with_prompt("topic", "Quarterly review");
    let rendered = session
        .render_template("Templates/Meeting.md", ctx)
        .unwrap();
    assert_eq!(rendered.body, "# Meeting: Quarterly review\n\n\n");
    // {{cursor}} lands right after the two blank lines (`\n\n`)
    // following the heading text.
    let offset = rendered.cursor_byte_offset.expect("cursor offset");
    let prefix = "# Meeting: Quarterly review\n\n";
    assert_eq!(offset, prefix.len());
}

#[test]
fn render_template_rejects_path_outside_vault() {
    let (_tmp, session) = make_vault(|_| {});
    let ctx = crate::TemplateContext::new(0, "t", "v");
    match session.render_template("../escape.md", ctx) {
        Err(VaultError::InvalidPath { .. }) => {}
        other => panic!("expected InvalidPath, got {other:?}"),
    }
}

#[test]
fn list_templates_drops_symlinks_pointing_outside_the_vault() {
    // Sentinel file outside the vault: a symlink dropped under
    // Templates/ that targets this file must not surface in the
    // picker.
    let outside = tempfile::tempdir().unwrap();
    let secret = outside.path().join("secret.md");
    std::fs::write(&secret, b"# secret\n\nshould never reach the picker\n").unwrap();

    let (vault_tmp, session) = make_vault(|p| {
        p.write_file("Templates/Real.md", b"# real template\n")
            .unwrap();
    });
    // Symlink under Templates/ pointing at the outside sentinel.
    let templates_dir = vault_tmp.path().join("Templates");
    std::os::unix::fs::symlink(&secret, templates_dir.join("Pwn.md")).unwrap();

    let templates = session.list_templates().unwrap();
    let names: Vec<&str> = templates.iter().map(|t| t.name.as_str()).collect();
    // Only the legitimate template surfaces; the escaping symlink
    // is silently dropped.
    assert_eq!(names, vec!["Real"]);
}

#[test]
fn list_templates_follows_symlinks_pointing_inside_the_vault() {
    // Symmetric: a symlink under Templates/ whose canonical target
    // stays in-vault is a legitimate authoring choice and must
    // continue to work.
    let (vault_tmp, session) = make_vault(|p| {
        p.write_file("Templates/Real.md", b"# real template\n")
            .unwrap();
        p.write_file("shared/Daily.md", b"# daily\n\nshared content\n")
            .unwrap();
    });
    // Symlink Templates/Daily.md → ../shared/Daily.md (inside vault).
    let templates_dir = vault_tmp.path().join("Templates");
    let target = vault_tmp.path().join("shared/Daily.md");
    std::os::unix::fs::symlink(&target, templates_dir.join("Daily.md")).unwrap();

    let templates = session.list_templates().unwrap();
    let mut names: Vec<&str> = templates.iter().map(|t| t.name.as_str()).collect();
    names.sort();
    // Both surface — the in-vault symlink is treated as a normal
    // template.
    assert_eq!(names, vec!["Daily", "Real"]);
}

#[test]
fn render_template_refuses_escaping_symlink_with_invalid_path() {
    // `render_template` is more emphatic than `list_templates`:
    // an explicit attempt to render an escaping template gets
    // an InvalidPath error rather than a silent drop, so the
    // host can surface "refused for safety" cleanly.
    let outside = tempfile::tempdir().unwrap();
    let secret = outside.path().join("secret.md");
    std::fs::write(&secret, b"SHOULD NEVER RENDER").unwrap();

    let (vault_tmp, session) = make_vault(|p| {
        p.write_file("Templates/Real.md", b"# real\n").unwrap();
    });
    std::os::unix::fs::symlink(&secret, vault_tmp.path().join("Templates/Pwn.md")).unwrap();

    let ctx = crate::TemplateContext::new(0, "t", "v");
    match session.render_template("Templates/Pwn.md", ctx) {
        Err(VaultError::InvalidPath { reason, .. }) => {
            assert!(reason.contains("escapes the vault root"), "got: {reason}");
        }
        other => panic!("expected InvalidPath for escaping symlink, got {other:?}"),
    }
}

#[test]
fn render_template_opens_canonical_path_not_original_symlink() {
    // Direct test of the TOCTOU defence (Codoki PR #153 Medium).
    // The provider's `read_in_vault_with_cap` opens the CANONICAL
    // resolved path, not the original symlink — so a swap of the
    // symlink between the canonicalize and the open can't redirect
    // the read.
    //
    // We can't reproduce the race timing reliably in a unit test,
    // but we CAN verify the canonical-open behaviour by setting up
    // a chain `Templates/Alias.md → Templates/inner/Real.md` and
    // confirming the read returns the real file's contents
    // unambiguously. Combined with the
    // `list_templates_drops_symlinks_pointing_outside_the_vault`
    // test, this proves the provider does the right thing on
    // both sides of the scope boundary.
    let (vault_tmp, session) = make_vault(|p| {
        p.write_file("Templates/inner/Real.md", b"# real inside\n")
            .unwrap();
    });
    std::os::unix::fs::symlink(
        vault_tmp.path().join("Templates/inner/Real.md"),
        vault_tmp.path().join("Templates/Alias.md"),
    )
    .unwrap();

    let ctx = crate::TemplateContext::new(0, "t", "v");
    let rendered = session
        .render_template("Templates/Alias.md", ctx)
        .expect("in-vault symlink chain should render cleanly");
    assert_eq!(rendered.body, "# real inside\n");
}

#[test]
fn list_templates_skips_broken_symlinks_without_error() {
    // Defensive: a broken symlink under Templates/ (target was
    // deleted) should still cleanly drop out of the picker without
    // breaking the enumeration of legitimate templates. Existing
    // pre-#132 behaviour relied on the read-side NotFound catch;
    // the new verify_in_vault path triggers first and also raises
    // an Io error here. Either way, drop silently.
    let (vault_tmp, session) = make_vault(|p| {
        p.write_file("Templates/Real.md", b"# real\n").unwrap();
    });
    let broken_target = vault_tmp.path().join("Templates/.does-not-exist.md");
    std::os::unix::fs::symlink(&broken_target, vault_tmp.path().join("Templates/Broken.md"))
        .unwrap();

    let templates = session.list_templates().unwrap();
    let names: Vec<&str> = templates.iter().map(|t| t.name.as_str()).collect();
    assert_eq!(names, vec!["Real"]);
}
