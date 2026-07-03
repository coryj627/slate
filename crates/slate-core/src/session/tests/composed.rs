// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `VaultSession` tests — the body-only source buffer + composed save
//! (`read_note_parts`, `save_composed`, `set_frontmatter_source`) and
//! the pure `split_note`/`compose_note` round-trip (#469, U3-5).
//!
//! Two censuses follow the project's adversarial-census methodology:
//! plain `#[test]` functions driven by a deterministic seeded PRNG
//! (splitmix64), printing the failing seed so any failure replays. NOT
//! proptest — these run in the normal suite every time.
//!
//! - `census_split_compose_round_trip` — 100k random documents (random
//!   frontmatter presence, CRLF mix, BOM, unclosed fences, `---` inside
//!   code blocks and mid-body, unicode) must satisfy
//!   `split_note(s).compose() == s` byte-exact.
//! - `census_widget_body_edit_interleave` — random interleavings of
//!   {set_property, delete_property, set_frontmatter_source,
//!   save_composed(body-edit)} against an in-memory reference model
//!   mutated by the SAME pure functions: on-disk bytes == reference
//!   after every op, and the serial content-hash chain never conflicts.

#![allow(clippy::too_many_lines)]

use super::common::*;
use super::*;

// --- unit: read_note_parts ------------------------------------------------

#[test]
fn read_note_parts_splits_frontmatter_and_body() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "note.md",
            b"---\ntitle: Hi\nauthor: Cory\n---\n# Heading\n\nBody.\n",
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let bundle = session.read_note_parts("note.md").unwrap();
    assert_eq!(bundle.fm_source, "title: Hi\nauthor: Cory\n");
    assert_eq!(bundle.body, "# Heading\n\nBody.\n");
    // Whole-file hash — the same value save_composed conflict-checks.
    assert_eq!(
        bundle.content_hash,
        crate::vault::content_hash(b"---\ntitle: Hi\nauthor: Cory\n---\n# Heading\n\nBody.\n")
    );
    assert!(bundle.mtime_ms > 0);
}

#[test]
fn read_note_parts_empty_fm_for_plain_markdown() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"# Just a body\n\nNo frontmatter.\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let bundle = session.read_note_parts("note.md").unwrap();
    assert_eq!(bundle.fm_source, "");
    assert_eq!(bundle.body, "# Just a body\n\nNo frontmatter.\n");
}

// --- unit: save_composed --------------------------------------------------

#[test]
fn save_composed_writes_canonical_frontmatter_plus_body() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"---\ntitle: Old\n---\nold body\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let hash = session.read_note_parts("note.md").unwrap().content_hash;
    let report = session
        .save_composed("note.md", "title: New\n", "new body\n", Some(hash))
        .unwrap();
    assert!(!report.new_content_hash.is_empty());

    let raw = session.read_text("note.md").unwrap();
    assert_eq!(raw, "---\ntitle: New\n---\nnew body\n");
}

#[test]
fn save_composed_empty_fm_writes_body_only() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"---\ntitle: X\n---\nbody\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let hash = session.read_note_parts("note.md").unwrap().content_hash;
    session
        .save_composed("note.md", "", "just body\n", Some(hash))
        .unwrap();

    let raw = session.read_text("note.md").unwrap();
    assert_eq!(raw, "just body\n", "empty fm elides the delimiters");
}

#[test]
fn save_composed_detects_write_conflict_on_stale_hash() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"---\ntitle: X\n---\nbody\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    // A hash that doesn't match the current file → conflict, nothing written.
    let stale = crate::vault::content_hash(b"something else entirely");
    let err = session
        .save_composed("note.md", "title: Y\n", "body2\n", Some(stale))
        .unwrap_err();
    assert!(matches!(err, VaultError::WriteConflict { .. }));

    // File is untouched.
    let raw = session.read_text("note.md").unwrap();
    assert_eq!(raw, "---\ntitle: X\n---\nbody\n");
}

#[test]
fn save_composed_body_edit_round_trips_via_read_note_parts() {
    // Simulate the editor: read parts, edit only the body, save, re-read.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"---\ntags:\n  - a\n  - b\n---\nline one\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let before = session.read_note_parts("note.md").unwrap();
    let new_body = format!("{}line two\n", before.body);
    let report = session
        .save_composed(
            "note.md",
            &before.fm_source,
            &new_body,
            Some(before.content_hash),
        )
        .unwrap();

    let after = session.read_note_parts("note.md").unwrap();
    assert_eq!(after.fm_source, before.fm_source, "frontmatter untouched");
    assert_eq!(after.body, "line one\nline two\n");
    // The report's hash is the new on-disk whole-file hash — the handoff
    // the next save conflict-checks against.
    assert_eq!(after.content_hash, report.new_content_hash);
}

// --- unit: set_frontmatter_source -----------------------------------------

#[test]
fn set_frontmatter_source_replaces_fm_preserves_body() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"---\ntitle: Old\n---\n# Body\n\nkeep me\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let hash = session.read_note_parts("note.md").unwrap().content_hash;
    session
        .set_frontmatter_source("note.md", "title: New\nauthor: Cory\n", Some(hash))
        .unwrap();

    let bundle = session.read_note_parts("note.md").unwrap();
    assert_eq!(bundle.fm_source, "title: New\nauthor: Cory\n");
    assert_eq!(
        bundle.body, "# Body\n\nkeep me\n",
        "body preserved verbatim"
    );
}

#[test]
fn set_frontmatter_source_stores_comments_verbatim() {
    // Unlike set_property, the source is stored as-authored — comments
    // survive (they're the source of truth, not a re-emitted view).
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"---\ntitle: X\n---\nbody\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let hash = session.read_note_parts("note.md").unwrap().content_hash;
    session
        .set_frontmatter_source("note.md", "# machine-managed\ntitle: Y\n", Some(hash))
        .unwrap();

    let raw = session.read_text("note.md").unwrap();
    assert!(
        raw.contains("# machine-managed"),
        "comment survived: {raw:?}"
    );
}

#[test]
fn set_frontmatter_source_empty_removes_block() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"---\ntitle: X\n---\nbody\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let hash = session.read_note_parts("note.md").unwrap().content_hash;
    session
        .set_frontmatter_source("note.md", "", Some(hash))
        .unwrap();

    let raw = session.read_text("note.md").unwrap();
    assert_eq!(raw, "body\n", "empty fm source drops the whole block");
}

#[test]
fn set_frontmatter_source_rejects_malformed_yaml_writes_nothing() {
    let original = b"---\ntitle: X\n---\nbody\n";
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", original).unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let hash = session.read_note_parts("note.md").unwrap().content_hash;
    let err = session
        .set_frontmatter_source("note.md", "key: \"unterminated\n", Some(hash))
        .unwrap_err();
    match err {
        VaultError::MalformedFrontmatter { reason, .. } => {
            assert!(
                reason.contains("line") && reason.contains("column"),
                "expected line/column in the message, got {reason:?}"
            );
        }
        other => panic!("expected MalformedFrontmatter, got {other:?}"),
    }

    // Non-destructive: the file on disk is byte-identical to before.
    let raw = session.read_text("note.md").unwrap();
    assert_eq!(raw.as_bytes(), original);
}

#[test]
fn set_frontmatter_source_rejects_non_mapping_writes_nothing() {
    let original = b"---\ntitle: X\n---\nbody\n";
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", original).unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let hash = session.read_note_parts("note.md").unwrap().content_hash;
    let err = session
        .set_frontmatter_source("note.md", "- a\n- b\n", Some(hash))
        .unwrap_err();
    assert!(matches!(err, VaultError::MalformedFrontmatter { .. }));
    assert_eq!(session.read_text("note.md").unwrap().as_bytes(), original);
}

// =========================================================================
// Deterministic PRNG for the censuses (splitmix64). Self-contained so the
// suite needs no `rand` dependency and every failure replays from its seed.
// =========================================================================

struct SplitMix64(u64);

impl SplitMix64 {
    fn new(seed: u64) -> Self {
        Self(seed)
    }
    fn next_u64(&mut self) -> u64 {
        self.0 = self.0.wrapping_add(0x9E37_79B9_7F4A_7C15);
        let mut z = self.0;
        z = (z ^ (z >> 30)).wrapping_mul(0xBF58_476D_1CE4_E5B9);
        z = (z ^ (z >> 27)).wrapping_mul(0x94D0_49BB_1331_11EB);
        z ^ (z >> 31)
    }
    /// Uniform-ish in `[0, n)`; `n` small so modulo bias is negligible.
    fn below(&mut self, n: usize) -> usize {
        (self.next_u64() % n as u64) as usize
    }
    fn chance(&mut self, numerator: u32, denominator: u32) -> bool {
        (self.next_u64() % denominator as u64) < numerator as u64
    }
}

// --- census: split/compose round-trip -------------------------------------

/// Line-ending flavors the generator mixes so CRLF / LF / bare-CR paths
/// through the boundary are exercised.
const EOLS: &[&str] = &["\n", "\r\n", "\r"];

/// Atoms the random-document generator draws lines from — unicode,
/// fence markers, `---` (both as a delimiter candidate and a body
/// thematic break), and YAML-ish key/value shapes. Deliberately hostile:
/// a naive splitter that scans for any `---` line will trip on these.
const LINE_ATOMS: &[&str] = &[
    "key: value",
    "title: Hello World",
    "tags:",
    "  - alpha",
    "  - \u{00e9}", // é precomposed
    "# Heading",
    "",
    "```rust",
    "let x = 5;",
    "```",
    "---", // thematic break OR delimiter, depending on position
    "prose with \u{1F600} emoji and `inline code`",
    "> a blockquote",
    "|table|cell|",
    "  indented continuation",
    "author: \"Cory\"",
];

/// Build a random document: optional frontmatter block (with a random
/// opener/closer delimiter shape) followed by a random body, joined with
/// randomly-chosen line endings. Returns the assembled string.
fn random_document(rng: &mut SplitMix64) -> String {
    let mut out = String::new();

    // Optional leading BOM (rare — most files don't have one).
    if rng.chance(1, 10) {
        out.push('\u{FEFF}');
    }

    // ~2/3 of documents carry a frontmatter block; the rest are plain.
    let has_frontmatter = rng.chance(2, 3);
    if has_frontmatter {
        // Opening delimiter: `---` + optional trailing whitespace + EOL.
        out.push_str("---");
        if rng.chance(1, 4) {
            out.push_str([" ", "\t", "  "][rng.below(3)]);
        }
        out.push_str(EOLS[rng.below(EOLS.len())]);

        // 0..=4 frontmatter lines that do NOT contain a bare `---`
        // (so we don't accidentally close the block mid-generation with
        // a shape the reader would treat as the real close).
        let fm_lines = rng.below(5);
        for _ in 0..fm_lines {
            let mut atom = LINE_ATOMS[rng.below(LINE_ATOMS.len())];
            if atom == "---" {
                atom = "key: value"; // never a delimiter inside the fm
            }
            out.push_str(atom);
            out.push_str(EOLS[rng.below(EOLS.len())]);
        }

        // Closing delimiter: `---` + optional trailing whitespace, then
        // either an EOL or (rarely) EOF with no newline.
        out.push_str("---");
        if rng.chance(1, 4) {
            out.push_str([" ", "\t"][rng.below(2)]);
        }
        let close_at_eof = rng.chance(1, 6);
        if !close_at_eof {
            out.push_str(EOLS[rng.below(EOLS.len())]);
        } else {
            // File ends exactly at the closing delimiter — no body.
            return out;
        }
    }

    // Body: 0..=8 arbitrary lines (these MAY include `---` thematic
    // breaks and unclosed code fences — the split must not be fooled).
    let body_lines = rng.below(9);
    for _ in 0..body_lines {
        out.push_str(LINE_ATOMS[rng.below(LINE_ATOMS.len())]);
        out.push_str(EOLS[rng.below(EOLS.len())]);
    }
    // Sometimes the final line has no trailing newline.
    if rng.chance(1, 3) {
        out.push_str("tail no newline");
    }

    out
}

#[test]
fn census_split_compose_round_trip() {
    // 100k random documents: the split → reconstruct round-trip must be
    // byte-exact, and the split's body must agree with the reader's
    // `body_after_frontmatter`. Plus the exhaustive delimiter fixtures
    // (also asserted directly in `frontmatter.rs`, re-run here so this
    // census is self-standing as the release guarantee).
    const DOCS: u64 = 100_000;
    for seed in 0..DOCS {
        let mut rng = SplitMix64::new(seed.wrapping_mul(0x2545_F491_4F6C_DD1D).wrapping_add(1));
        let doc = random_document(&mut rng);
        let parts = crate::split_note(&doc);
        assert_eq!(
            parts.compose(),
            doc,
            "seed {seed}: split→compose not byte-exact\n  doc   = {doc:?}\n  parts = {parts:?}"
        );
        assert_eq!(
            parts.body,
            crate::frontmatter::body_after_frontmatter(&doc),
            "seed {seed}: split body != body_after_frontmatter for {doc:?}"
        );
        // fm_source is either empty or exactly the bytes between the
        // delimiters — never contains a `---` delimiter line of its own.
        if parts.fm_source.is_empty() {
            assert_eq!(
                parts.body, doc,
                "seed {seed}: empty fm must leave body == whole doc"
            );
        }
    }

    // Exhaustive delimiter edge-case fixtures (the spec's named set).
    let fixtures = [
        "",
        "---\n---\nbody\n",                // empty fm block
        "---\nkey: value\n---\n",          // fm-only file
        "---\nkey: value\n---\nbody",      // no trailing newline
        "---\na: 1\n---",                  // `---` at EOF, no newline
        "intro\n---\nkey: v\n---\nrest\n", // frontmatter-like block mid-file
        "\u{FEFF}---\nk: v\n---\nb\n",     // BOM + fm
        "---\r\nk: v\r\n---\r\nb\r\n",     // all CRLF
        "--- \nk: v\n--- \nb\n",           // trailing ws on delimiters
    ];
    for doc in fixtures {
        let parts = crate::split_note(doc);
        assert_eq!(
            parts.compose(),
            doc,
            "fixture round-trip failed for {doc:?}"
        );
        assert_eq!(parts.body, crate::frontmatter::body_after_frontmatter(doc));
    }
}

// --- census: widget/body edit interleave ----------------------------------

/// The reference model: a plain in-memory string mutated by exactly the
/// same pure functions the session APIs route through. The on-disk file
/// must equal this string after every operation.
struct RefModel {
    source: String,
}

impl RefModel {
    /// Apply the same edit `set_property` performs, or leave the model
    /// unchanged and report the error (so we can assert the session
    /// errors identically). Returns `Ok(())` on a successful edit.
    fn set_property(&mut self, key: &str, value: &crate::PropertyValue) -> Result<(), ()> {
        match crate::frontmatter::set_property_in_source(&self.source, key, value) {
            Ok(next) => {
                self.source = next;
                Ok(())
            }
            Err(_) => Err(()),
        }
    }

    fn delete_property(&mut self, key: &str) -> Result<(), ()> {
        match crate::frontmatter::delete_property_in_source(&self.source, key) {
            Ok(crate::frontmatter::FrontmatterEdit::Changed(next)) => {
                self.source = next;
                Ok(())
            }
            // Unchanged is a no-op success (session short-circuits too).
            Ok(crate::frontmatter::FrontmatterEdit::Unchanged) => Ok(()),
            Err(_) => Err(()),
        }
    }

    fn set_frontmatter_source(&mut self, fm: &str) -> Result<(), ()> {
        match crate::validate_frontmatter_source(fm) {
            Ok(()) => {
                let body = crate::split_note(&self.source).body;
                self.source = crate::compose_note(fm, &body);
                Ok(())
            }
            Err(_) => Err(()),
        }
    }

    fn save_composed_body_edit(&mut self, new_body: &str) {
        let fm = crate::split_note(&self.source).fm_source;
        self.source = crate::compose_note(&fm, new_body);
    }
}

#[test]
fn census_widget_body_edit_interleave() {
    // For each seed: seed a note, then apply a random serial sequence of
    // {set_property, delete_property, set_frontmatter_source,
    // save_composed(body-edit)}, threading the whole-file content hash
    // from each SaveReport into the next call. After EVERY op the on-disk
    // bytes must equal the reference model, and the serial hash chain must
    // never produce a WriteConflict (proving the handoff contract the
    // Swift side relies on).
    const SEEDS: u64 = 2_000;
    const OPS_PER_SEED: usize = 40;

    // A small pool of property keys/values the ops draw from.
    let keys = ["title", "author", "year", "tags", "status"];
    let string_vals = ["Hello", "Cory", "draft", "published", "\u{00e9}dition"];

    // Frontmatter sources for set_frontmatter_source: a mix of valid
    // mappings, empty (drop block), and malformed (must be rejected,
    // model + disk both unchanged).
    let fm_sources: &[&str] = &[
        "title: A\n",
        "title: B\nauthor: C\n",
        "",                      // drops the block
        "# comment only\n",      // valid empty mapping
        "key: \"unterminated\n", // malformed → rejected
        "- not\n- a map\n",      // non-mapping → rejected
        "tags:\n  - x\n  - y\n",
    ];

    for seed in 0..SEEDS {
        let mut rng = SplitMix64::new(seed.wrapping_mul(0x1000_0001).wrapping_add(7));

        // Seed the initial note: ~half start with frontmatter.
        let initial = if rng.chance(1, 2) {
            "---\ntitle: Seed\n---\ninitial body\n".to_string()
        } else {
            "initial body only\n".to_string()
        };

        let (_tmp, session) = make_vault(|p| {
            p.write_file("note.md", initial.as_bytes()).unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();

        let mut model = RefModel {
            source: initial.clone(),
        };
        // Thread the whole-file hash through the chain.
        let mut hash = session.read_note_parts("note.md").unwrap().content_hash;
        assert_eq!(
            hash,
            crate::vault::content_hash(model.source.as_bytes()),
            "seed {seed}: initial hash disagreement"
        );

        for op in 0..OPS_PER_SEED {
            let choice = rng.below(4);
            let result = match choice {
                0 => {
                    // set_property with a random key + string value.
                    let key = keys[rng.below(keys.len())];
                    let val = crate::PropertyValue::Text(
                        string_vals[rng.below(string_vals.len())].to_string(),
                    );
                    let model_ok = model.set_property(key, &val).is_ok();
                    let disk = session.set_property("note.md", key, val, Some(hash.as_str()));
                    check_agreement(seed, op, "set_property", model_ok, &disk, &model.source);
                    disk
                }
                1 => {
                    // delete_property.
                    let key = keys[rng.below(keys.len())];
                    let model_ok = model.delete_property(key).is_ok();
                    let disk = session.delete_property("note.md", key, Some(hash.as_str()));
                    check_agreement(seed, op, "delete_property", model_ok, &disk, &model.source);
                    disk
                }
                2 => {
                    // set_frontmatter_source (valid, empty, or malformed).
                    let fm = fm_sources[rng.below(fm_sources.len())];
                    let model_ok = model.set_frontmatter_source(fm).is_ok();
                    let disk = session.set_frontmatter_source("note.md", fm, Some(hash.clone()));
                    check_agreement(
                        seed,
                        op,
                        "set_frontmatter_source",
                        model_ok,
                        &disk,
                        &model.source,
                    );
                    disk
                }
                _ => {
                    // save_composed with a body edit (fm from current parts).
                    let new_body = format!("edited body @op{op}\nsecond line\n");
                    model.save_composed_body_edit(&new_body);
                    let fm = session.read_note_parts("note.md").unwrap().fm_source;
                    let disk = session.save_composed("note.md", &fm, &new_body, Some(hash.clone()));
                    // A body-edit compose always succeeds (valid fm by
                    // construction — it came from the current parts).
                    assert!(
                        disk.is_ok(),
                        "seed {seed} op {op}: save_composed body-edit failed: {:?}",
                        disk.err()
                    );
                    disk
                }
            };

            // On success, advance the hash chain from the SaveReport. On a
            // rejected op (malformed fm), the file is unchanged, so the
            // current on-disk hash still equals `hash` — no advance needed.
            if let Ok(report) = result {
                hash = report.new_content_hash;
            }

            // The invariant, checked after EVERY op regardless of outcome:
            // on-disk bytes == reference model.
            let on_disk = session.read_text("note.md").unwrap();
            assert_eq!(
                on_disk, model.source,
                "seed {seed} op {op} ({choice}): on-disk bytes diverged from reference model"
            );
            // And the threaded hash matches the on-disk bytes (the serial
            // chain never drifted — the contract the Swift side relies on).
            assert_eq!(
                hash,
                crate::vault::content_hash(on_disk.as_bytes()),
                "seed {seed} op {op}: threaded hash != on-disk hash (chain drift)"
            );
        }
    }
}

/// Assert the session outcome agrees with the reference model on
/// success-vs-error, and that a `WriteConflict` never appears in the
/// serial chain (a conflict here would mean the hash handoff is broken).
fn check_agreement(
    seed: u64,
    op: usize,
    label: &str,
    model_ok: bool,
    disk: &Result<SaveReport, VaultError>,
    model_source: &str,
) {
    match (model_ok, disk) {
        (true, Ok(_)) | (false, Err(_)) => {}
        (true, Err(e)) => panic!(
            "seed {seed} op {op} ({label}): model succeeded but session errored: {e:?}\n  model = {model_source:?}"
        ),
        (false, Ok(_)) => panic!(
            "seed {seed} op {op} ({label}): model rejected but session accepted\n  model = {model_source:?}"
        ),
    }
    if let Err(VaultError::WriteConflict { .. }) = disk {
        panic!(
            "seed {seed} op {op} ({label}): serial chain produced a WriteConflict — hash handoff broken"
        );
    }
}
