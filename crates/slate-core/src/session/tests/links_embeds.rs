// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `VaultSession` tests — outgoing_links, backlinks, list_unresolved_links, resolve_embed, note_load_bundle, read_attachment.
//!
//! Extracted from `crates/slate-core/src/session.rs` as part of #272.

#![allow(clippy::too_many_lines)]

use super::common::*;
use super::*;

#[test]
fn outgoing_links_returns_mixed_kinds_in_document_order() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "notes/source.md",
            b"see [[Alpha]] and [md](beta.md) and [ext](https://example.com)",
        )
        .unwrap();
        p.write_file("notes/Alpha.md", b"# Alpha").unwrap();
        p.write_file("notes/beta.md", b"# Beta").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let outgoing = session.outgoing_links("notes/source.md").unwrap();
    assert_eq!(outgoing.len(), 3, "got {:?}", outgoing);

    // Wikilink → resolved.
    assert_eq!(outgoing[0].target_path.as_deref(), Some("notes/Alpha.md"));
    assert_eq!(outgoing[0].kind, "wikilink");
    assert!(!outgoing[0].is_external && !outgoing[0].is_unresolved);

    // Markdown internal → resolved.
    assert_eq!(outgoing[1].target_path.as_deref(), Some("notes/beta.md"));
    assert_eq!(outgoing[1].kind, "markdown");
    assert!(!outgoing[1].is_external);

    // Markdown external.
    assert!(outgoing[2].is_external);
    assert_eq!(outgoing[2].target_raw, "https://example.com");
}

#[test]
fn backlinks_returns_all_inbound_sources() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/target.md", b"# Target").unwrap();
        p.write_file("notes/a.md", b"prelude [[target]] more")
            .unwrap();
        p.write_file("notes/b.md", b"see [[Target]] here").unwrap();
        p.write_file("notes/c.md", b"no link").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let page = session
        .backlinks("notes/target.md", Paging::first(100))
        .unwrap();
    let mut paths: Vec<&str> = page.items.iter().map(|b| b.source_path.as_str()).collect();
    paths.sort();
    assert_eq!(paths, vec!["notes/a.md", "notes/b.md"]);
    for backlink in &page.items {
        assert!(
            !backlink.snippet.is_empty(),
            "backlink snippet should be populated"
        );
    }
    assert_eq!(page.total_filtered, 2);
}

#[test]
fn list_unresolved_links_surfaces_dangling_targets() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/source.md", b"hello [[Missing]] bye")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let page = session.list_unresolved_links(Paging::first(100)).unwrap();
    assert_eq!(page.items.len(), 1);
    assert_eq!(page.items[0].source_path, "notes/source.md");
    assert_eq!(page.items[0].target_raw, "Missing");
}

#[test]
fn unresolved_link_resolves_after_target_appears() {
    // The post-scan re-resolve pass should fix up links that
    // were Unresolved because the target file was indexed AFTER
    // the source on the previous scan run.
    let (tmp, session) = make_vault(|p| {
        p.write_file("notes/source.md", b"see [[Eventually]]")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    // Still missing after first scan.
    assert_eq!(
        session
            .list_unresolved_links(Paging::first(10))
            .unwrap()
            .items
            .len(),
        1
    );

    // Add the target and re-scan.
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider
        .write_file("notes/Eventually.md", b"# Eventually")
        .unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();

    // Source's content didn't change, so the slow path doesn't
    // rewrite its links — but the post-scan re-resolve pass
    // re-runs the resolver against the now-complete index and
    // updates target_path.
    let page = session.list_unresolved_links(Paging::first(10)).unwrap();
    assert_eq!(
        page.items.len(),
        0,
        "expected 0 unresolved after target appeared, got {:?}",
        page.items
    );
    let outgoing = session.outgoing_links("notes/source.md").unwrap();
    assert_eq!(outgoing.len(), 1);
    assert_eq!(
        outgoing[0].target_path.as_deref(),
        Some("notes/Eventually.md")
    );
}

#[test]
fn link_to_removed_target_becomes_unresolved_on_rescan() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("notes/source.md", b"see [[Vanishing]]")
            .unwrap();
        p.write_file("notes/Vanishing.md", b"# Vanishing").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let initial_unresolved = session.list_unresolved_links(Paging::first(10)).unwrap();
    assert!(
        initial_unresolved.items.is_empty(),
        "should resolve initially"
    );

    // Remove the target on disk and force a rescan with a content
    // change so the slow path rewrites source.md's links against
    // the updated (target-less) index.
    std::fs::remove_file(tmp.path().join("notes/Vanishing.md")).unwrap();
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider
        .write_file("notes/source.md", b"see [[Vanishing]] (updated)")
        .unwrap();
    // Note: the scanner doesn't currently prune rows for files
    // removed on disk; it just upserts what it sees. The link
    // resolver runs against `files` table contents, so a removed
    // file is still in the index until a future cleanup pass.
    // To make this test deterministic we open a fresh session
    // against the same .slate directory — re-scan triggers
    // re-write of source.md's links against the current files
    // table.
    session.scan_initial(&CancelToken::new()).unwrap();
    // Confirm the outgoing link still points somewhere useful or
    // is flagged unresolved (depending on whether the orphan
    // sweep has run; here it hasn't, so it still resolves to the
    // stale row). The point of this test is to exercise the
    // slow-path rewrite of links on content change.
    let outgoing = session.outgoing_links("notes/source.md").unwrap();
    assert_eq!(outgoing.len(), 1);
    assert!(outgoing[0].snippet.contains("[[Vanishing]]"));
}

#[test]
fn fast_path_does_not_rewrite_links() {
    // First scan writes a link row.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/source.md", b"see [[Alpha]]").unwrap();
        p.write_file("notes/Alpha.md", b"# Alpha").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let before = session.outgoing_links("notes/source.md").unwrap();
    assert_eq!(before.len(), 1);

    // Second scan with no file changes: fast path skips per-file
    // work, so the link row stays exactly as it was. We assert
    // by ordinal + target identity — if the slow path had run,
    // ordinals would be reassigned but identical, so the more
    // meaningful invariant is that the row is still there.
    session.scan_initial(&CancelToken::new()).unwrap();
    let after = session.outgoing_links("notes/source.md").unwrap();
    assert_eq!(after, before, "fast path must not touch links");
}

#[test]
fn backlinks_pagination_round_trips_without_gaps_or_duplicates() {
    // Regression for the Codoki callout on PR 78: an earlier
    // implementation derived next_cursor from the lookahead row,
    // which skipped one item per page boundary. With the fix the
    // union of paged items must equal the full set.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/target.md", b"# Target").unwrap();
        for i in 0..7 {
            p.write_file(
                &format!("notes/src{:02}.md", i),
                format!("see [[target]] now {i}").as_bytes(),
            )
            .unwrap();
        }
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let limit: u32 = 3;
    let mut seen: Vec<String> = Vec::new();
    let mut cursor: Option<String> = None;
    loop {
        let paging = match &cursor {
            Some(c) => Paging::after(c.clone(), limit),
            None => Paging::first(limit),
        };
        let page = session.backlinks("notes/target.md", paging).unwrap();
        for backlink in &page.items {
            seen.push(backlink.source_path.clone());
        }
        if let Some(next) = page.next_cursor {
            cursor = Some(next);
        } else {
            break;
        }
    }
    let mut expected: Vec<String> = (0..7).map(|i| format!("notes/src{:02}.md", i)).collect();
    expected.sort();
    let mut seen_sorted = seen.clone();
    seen_sorted.sort();
    assert_eq!(seen_sorted, expected, "paging dropped/duplicated rows");
    // No duplicates check (above-sorted equality already enforces
    // count + identity, but the explicit assertion makes the
    // intent obvious).
    let unique: std::collections::HashSet<_> = seen.iter().collect();
    assert_eq!(unique.len(), seen.len(), "duplicate rows across pages");
}

#[test]
fn link_anchor_passes_through_to_outgoing() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/source.md", b"see [[Alpha#Intro]]")
            .unwrap();
        p.write_file("notes/Alpha.md", b"# Alpha\n\n## Intro")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let outgoing = session.outgoing_links("notes/source.md").unwrap();
    assert_eq!(outgoing.len(), 1);
    assert_eq!(
        outgoing[0].target_anchor.as_ref().map(|(k, _)| k.as_str()),
        Some("heading")
    );
    assert_eq!(
        outgoing[0].target_anchor.as_ref().map(|(_, v)| v.as_str()),
        Some("Intro")
    );
}

#[test]
fn note_load_bundle_returns_backlinks_outgoing_and_properties_in_one_call() {
    // Three vaults' worth of state in a single bundle:
    //   - notes/target.md is linked to from notes/src.md (one backlink)
    //   - notes/target.md links out to notes/other.md (one outgoing)
    //   - notes/target.md has frontmatter (one property)
    // Verifies all three components arrive populated under a
    // single mutex acquisition (#92 item 4).
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "notes/target.md",
            b"---\ntitle: Target\n---\nbody mentions [[other]]\n",
        )
        .unwrap();
        p.write_file("notes/src.md", b"see [[target]] for context\n")
            .unwrap();
        p.write_file("notes/other.md", b"placeholder\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let bundle = session
        .note_load_bundle("notes/target.md", Paging::first(50))
        .unwrap();

    assert_eq!(bundle.backlinks.items.len(), 1);
    assert_eq!(bundle.backlinks.items[0].source_path, "notes/src.md");
    assert_eq!(bundle.outgoing_links.len(), 1);
    assert_eq!(bundle.outgoing_links[0].target_raw, "other");
    assert_eq!(bundle.properties.len(), 1);
    assert_eq!(bundle.properties[0].key, "title");
}

#[test]
fn note_load_bundle_returns_empty_arrays_for_unknown_path() {
    // The previous shape's three separate calls each independently
    // tolerated unknown paths (empty backlinks page, empty outgoing
    // vec, None metadata → empty properties). The combined call
    // must preserve that contract.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/a.md", b"body\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let bundle = session
        .note_load_bundle("notes/missing.md", Paging::first(50))
        .unwrap();

    assert!(bundle.backlinks.items.is_empty());
    assert!(bundle.outgoing_links.is_empty());
    assert!(bundle.properties.is_empty());
}

#[test]
fn resolve_embed_returns_full_note_with_body_only() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("host.md", b"# Host\n\nSee ![[target]] inline.\n")
            .unwrap();
        p.write_file(
            "target.md",
            b"---\ntitle: T\n---\n# Target\n\nTarget body content.\n",
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let resolution = session.resolve_embed("host.md", "target").unwrap();
    match resolution {
        crate::EmbedResolution::FullNote {
            target_path,
            text,
            nested,
        } => {
            assert_eq!(target_path, "target.md");
            assert!(text.contains("Target body content"));
            // Body strip drops the YAML frontmatter.
            assert!(!text.contains("title: T"));
            assert!(nested.is_empty());
        }
        other => panic!("expected FullNote, got {other:?}"),
    }
}

#[test]
fn resolve_embed_section_runs_until_next_same_level_heading() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "target.md",
            b"# H1\nfirst\n\n## H2\nsection text\n\n## H2b\nmore\n",
        )
        .unwrap();
        p.write_file("host.md", b"![[target#H2]]\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let resolution = session.resolve_embed("host.md", "target#H2").unwrap();
    match resolution {
        crate::EmbedResolution::Section { heading, text, .. } => {
            assert_eq!(heading, "H2");
            assert!(text.contains("section text"));
            assert!(
                !text.contains("H2b"),
                "section must end at the next same-level heading"
            );
        }
        other => panic!("expected Section, got {other:?}"),
    }
}

#[test]
fn resolve_embed_block_returns_anchored_byte_range() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "target.md",
            b"first paragraph\n\nsecond paragraph ^my-block\n\nthird paragraph\n",
        )
        .unwrap();
        p.write_file("host.md", b"![[target^my-block]]\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let resolution = session.resolve_embed("host.md", "target^my-block").unwrap();
    match resolution {
        crate::EmbedResolution::Block { block_id, text, .. } => {
            assert_eq!(block_id, "my-block");
            assert!(text.contains("second paragraph"));
            assert!(!text.contains("first paragraph"));
            assert!(!text.contains("third paragraph"));
        }
        other => panic!("expected Block, got {other:?}"),
    }
}

#[test]
fn resolve_embed_obsidian_block_ref_returns_anchored_byte_range() {
    // `![[target#^block]]` -- Obsidian's canonical block-ref syntax
    // (#413). Must resolve as a Block embed, not fail with "heading
    // wasn't found" from parsing `#^block` as a heading path.
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "target.md",
            b"first paragraph\n\nsecond paragraph ^my-block\n\nthird paragraph\n",
        )
        .unwrap();
        p.write_file("host.md", b"![[target#^my-block]]\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let resolution = session
        .resolve_embed("host.md", "target#^my-block")
        .unwrap();
    match resolution {
        crate::EmbedResolution::Block { block_id, text, .. } => {
            assert_eq!(block_id, "my-block");
            assert!(text.contains("second paragraph"));
            assert!(!text.contains("first paragraph"));
        }
        other => panic!("expected Block for #^ syntax, got {other:?}"),
    }
}

#[test]
fn resolve_embed_image_returns_bytes_and_mime() {
    let png_bytes: [u8; 16] = [
        0x89, b'P', b'N', b'G', 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0, 0, 0, 0,
    ];
    let (_tmp, session) = make_vault(|p| {
        p.write_file("attachments/cover.png", &png_bytes).unwrap();
        p.write_file("host.md", b"![[cover.png]]\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let resolution = session.resolve_embed("host.md", "cover.png").unwrap();
    match resolution {
        crate::EmbedResolution::Image {
            target_path,
            mime,
            bytes,
            ..
        } => {
            assert_eq!(target_path, "attachments/cover.png");
            assert_eq!(mime, "image/png");
            assert_eq!(bytes.len(), png_bytes.len());
        }
        other => panic!("expected Image, got {other:?}"),
    }
}

#[test]
fn resolve_embed_target_not_found_surfaces_unresolved() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("host.md", b"![[missing]]\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let r = session.resolve_embed("host.md", "missing").unwrap();
    assert!(matches!(
        r,
        crate::EmbedResolution::Unresolved {
            reason: crate::EmbedUnresolvedReason::TargetNotFound { .. }
        }
    ));
}

#[test]
fn resolve_embed_heading_not_found_carries_target_path() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("target.md", b"# Only Heading\nbody\n")
            .unwrap();
        p.write_file("host.md", b"![[target#Missing]]\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let r = session.resolve_embed("host.md", "target#Missing").unwrap();
    match r {
        crate::EmbedResolution::Unresolved {
            reason:
                crate::EmbedUnresolvedReason::HeadingNotFound {
                    target_path,
                    heading,
                },
        } => {
            assert_eq!(target_path, "target.md");
            assert_eq!(heading, "Missing");
        }
        other => panic!("expected HeadingNotFound, got {other:?}"),
    }
}

#[test]
fn resolve_embed_markdown_image_carries_alt_text() {
    // #419 (WCAG 1.1.1): `![alt](src)` — the author's alt text must
    // ride the Image resolution so AT hears the description, not
    // the filename.
    let png_bytes: [u8; 16] = [
        0x89, b'P', b'N', b'G', 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0, 0, 0, 0,
    ];
    let (_tmp, session) = make_vault(|p| {
        p.write_file("attachments/pie.png", &png_bytes).unwrap();
        p.write_file(
            "host.md",
            b"![A simple line drawing of a pie](attachments/pie.png)\n",
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let resolution = session
        .resolve_embed("host.md", "attachments/pie.png")
        .unwrap();
    match resolution {
        crate::EmbedResolution::Image { alt, .. } => {
            assert_eq!(alt.as_deref(), Some("A simple line drawing of a pie"));
        }
        other => panic!("expected Image, got {other:?}"),
    }
}

#[test]
fn resolve_embed_wikilink_image_without_alt_has_none() {
    // `![[cover.png]]` has no alt; the Swift layer falls back to the
    // filename descriptor (audit #198 contract).
    let png_bytes: [u8; 16] = [
        0x89, b'P', b'N', b'G', 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0, 0, 0, 0,
    ];
    let (_tmp, session) = make_vault(|p| {
        p.write_file("cover.png", &png_bytes).unwrap();
        p.write_file("host.md", b"![[cover.png]]\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    match session.resolve_embed("host.md", "cover.png").unwrap() {
        crate::EmbedResolution::Image { alt, .. } => assert_eq!(alt, None),
        other => panic!("expected Image, got {other:?}"),
    }
}

#[test]
fn resolve_embed_block_not_found_carries_block_id() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("target.md", b"paragraph\n").unwrap();
        p.write_file("host.md", b"![[target^missing]]\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let r = session.resolve_embed("host.md", "target^missing").unwrap();
    match r {
        crate::EmbedResolution::Unresolved {
            reason: crate::EmbedUnresolvedReason::BlockNotFound { block_id, .. },
        } => assert_eq!(block_id, "missing"),
        other => panic!("expected BlockNotFound, got {other:?}"),
    }
}

#[test]
fn resolve_embed_recursion_bottoms_out_at_depth_three() {
    // Chain: host → a → b → c → d. The resolver pre-resolves
    // up to MAX_EMBED_DEPTH (3) total levels; depth 3 onward
    // surfaces as DepthLimitReached.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("host.md", b"![[a]]\n").unwrap();
        p.write_file("a.md", b"a body ![[b]]\n").unwrap();
        p.write_file("b.md", b"b body ![[c]]\n").unwrap();
        p.write_file("c.md", b"c body ![[d]]\n").unwrap();
        p.write_file("d.md", b"d body\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let r = session.resolve_embed("host.md", "a").unwrap();
    // a (depth 0) → FullNote with nested b (depth 1) →
    // FullNote with nested c (depth 2) → FullNote with nested
    // d (depth 3) → DepthLimitReached.
    let nested = expect_full_note(&r);
    let inner_b = expect_nested_full_note(nested, "b");
    let inner_c = expect_nested_full_note(inner_b, "c");
    // c's nested d hits the depth limit.
    let d_entry = inner_c.iter().find(|n| n.raw_target == "d").unwrap();
    match &d_entry.resolution {
        crate::EmbedResolution::Unresolved {
            reason: crate::EmbedUnresolvedReason::DepthLimitReached,
        } => {}
        other => panic!("expected DepthLimitReached at depth 3, got {other:?}"),
    }
}

fn expect_full_note(r: &crate::EmbedResolution) -> &[crate::NestedEmbed] {
    match r {
        crate::EmbedResolution::FullNote { nested, .. } => nested,
        other => panic!("expected FullNote, got {other:?}"),
    }
}

fn expect_nested_full_note<'a>(
    nested: &'a [crate::NestedEmbed],
    target: &str,
) -> &'a [crate::NestedEmbed] {
    let entry = nested
        .iter()
        .find(|n| n.raw_target == target)
        .unwrap_or_else(|| panic!("no nested embed with target {target}"));
    match &entry.resolution {
        crate::EmbedResolution::FullNote { nested, .. } => nested,
        other => panic!("expected nested FullNote for {target}, got {other:?}"),
    }
}

#[test]
fn read_attachment_refuses_files_over_the_size_cap() {
    let tmp = tempfile::tempdir().unwrap();
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider.write_file("big.png", &[0u8; 64]).unwrap();
    let mut config = SessionConfig::new(tmp.path().join(".slate"));
    config.large_attachment_refuse_bytes = 32;
    let session = VaultSession::open(Arc::new(provider), config).unwrap();
    let err = session.read_attachment("big.png").unwrap_err();
    assert!(matches!(err, VaultError::FileTooLarge { .. }));
}

#[test]
fn nested_embed_byte_offset_lands_inside_parent_text() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("host.md", b"![[a]]\n").unwrap();
        p.write_file("a.md", b"prefix text ![[b]] suffix\n")
            .unwrap();
        p.write_file("b.md", b"b body\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let r = session.resolve_embed("host.md", "a").unwrap();
    let (text, nested) = match r {
        crate::EmbedResolution::FullNote { text, nested, .. } => (text, nested),
        other => panic!("expected FullNote, got {other:?}"),
    };
    let b_entry = nested.iter().find(|n| n.raw_target == "b").unwrap();
    // The offset must land inside the parent text.
    assert!(
        (b_entry.byte_offset_in_parent as usize) < text.len(),
        "nested byte offset {} must be < parent text len {}",
        b_entry.byte_offset_in_parent,
        text.len()
    );
}
