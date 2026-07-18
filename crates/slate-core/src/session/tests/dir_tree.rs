// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `VaultSession` tests — the directory-tree API (`list_dir_children`)
//! and its scan integration (`dirs` table upsert + prune) (#459, U2-1).
//!
//! The two censuses (`census_dir_tree_matches_filesystem`,
//! `census_dir_ids_stable_across_rescans`) follow the project's
//! adversarial-census methodology: plain `#[test]` functions driven by a
//! deterministic seeded PRNG, printing the failing seed so any failure
//! replays. NOT proptest — these run in the normal suite every time.

#![allow(clippy::too_many_lines)]

use std::collections::{BTreeMap, BTreeSet};
use std::path::Path;

use super::common::*;
use super::*;

// --- helpers -------------------------------------------------------------

/// Create an empty directory on disk (the provider only writes files;
/// empty folders need a direct mkdir so we can prove they get `dirs`
/// rows).
fn mkdir(root: &Path, rel: &str) {
    std::fs::create_dir_all(root.join(rel)).unwrap();
}

/// Collect just the child-directory names of a listing, in returned order.
fn dir_names(listing: &DirListing) -> Vec<String> {
    listing.dirs.iter().map(|d| d.name.clone()).collect()
}

/// Collect just the child-file names of a listing page, in returned order.
fn file_names(listing: &DirListing) -> Vec<String> {
    listing.files.items.iter().map(|f| f.name.clone()).collect()
}

// --- unit: root listing --------------------------------------------------

#[test]
fn root_listing_dirs_then_files_sorted() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("Zebra.md", b"# z").unwrap();
        p.write_file("apple.md", b"# a").unwrap();
        p.write_file("notes/inner.md", b"# i").unwrap();
    });
    mkdir(tmp.path(), "Beta");
    mkdir(tmp.path(), "alpha");
    session.scan_initial(&CancelToken::new()).unwrap();

    let listing = session.list_dir_children("", Paging::first(100)).unwrap();

    // Directories first, case-insensitive alphabetical: alpha, Beta, notes.
    assert_eq!(dir_names(&listing), vec!["alpha", "Beta", "notes"]);
    // Files after, case-insensitive: apple.md, Zebra.md.
    assert_eq!(file_names(&listing), vec!["apple.md", "Zebra.md"]);
    assert_eq!(listing.files.total_filtered, 2);
    assert_eq!(listing.files.next_cursor, None);
}

#[test]
fn root_child_counts_are_immediate_only() {
    let (tmp, session) = make_vault(|p| {
        // notes/ has two files + one subdir directly under it.
        p.write_file("notes/a.md", b"a").unwrap();
        p.write_file("notes/b.md", b"b").unwrap();
        p.write_file("notes/deep/c.md", b"c").unwrap();
    });
    mkdir(tmp.path(), "notes/emptysub");
    session.scan_initial(&CancelToken::new()).unwrap();

    let listing = session.list_dir_children("", Paging::first(100)).unwrap();
    let notes = listing
        .dirs
        .iter()
        .find(|d| d.name == "notes")
        .expect("notes dir present");
    // Immediate child dirs of notes: deep, emptysub => 2. Immediate child
    // files of notes: a.md, b.md => 2 (c.md is a grandchild, excluded).
    assert_eq!(notes.child_dir_count, 2);
    assert_eq!(notes.child_file_count, 2);
    assert_eq!(notes.path, "notes");
}

#[test]
fn root_listing_counts_nested_files_without_enriching_their_summaries() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/nested.md", b"# nested\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    // This value cannot be decoded into FileSummary.word_count's checked u32.
    // A root listing has no direct file rows, so it must only count this nested
    // file and must not run the expensive summary projection for it.
    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "UPDATE file_meta
             SET word_count = 4294967296
             WHERE file_id = (SELECT id FROM files WHERE path = 'notes/nested.md')",
            [],
        )
        .unwrap();
    }

    let root = session.list_dir_children("", Paging::first(100)).unwrap();
    assert!(root.files.items.is_empty());
    assert_eq!(root.files.total_filtered, 0);
    let notes = root.dirs.iter().find(|dir| dir.path == "notes").unwrap();
    assert_eq!(notes.child_file_count, 1);

    let nested_error = session
        .list_dir_children("notes", Paging::first(100))
        .expect_err("listing the nested level must exercise checked summary decoding");
    assert!(nested_error.to_string().contains("out of range"));
}

// --- unit: nested listing ------------------------------------------------

#[test]
fn nested_listing_lists_only_that_level() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("notes/one.md", b"1").unwrap();
        p.write_file("notes/two.md", b"2").unwrap();
        p.write_file("notes/sub/three.md", b"3").unwrap();
        p.write_file("other/x.md", b"x").unwrap();
    });
    mkdir(tmp.path(), "notes/emptydir");
    session.scan_initial(&CancelToken::new()).unwrap();

    let listing = session
        .list_dir_children("notes", Paging::first(100))
        .unwrap();
    assert_eq!(dir_names(&listing), vec!["emptydir", "sub"]);
    assert_eq!(file_names(&listing), vec!["one.md", "two.md"]);
    // Path form is the full vault-relative path, not just the name.
    assert_eq!(
        listing.dirs.iter().find(|d| d.name == "sub").unwrap().path,
        "notes/sub"
    );
}

#[test]
fn nested_listing_of_leaf_dir_is_empty() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/sub/only.md", b"o").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let listing = session
        .list_dir_children("notes/sub", Paging::first(100))
        .unwrap();
    assert!(listing.dirs.is_empty());
    assert_eq!(file_names(&listing), vec!["only.md"]);
}

// --- unit: unicode NFC/NFD sort determinism ------------------------------

#[test]
fn unicode_nfc_nfd_names_sort_deterministically() {
    // The spec's sort key is `name.to_lowercase()` on the NFC form —
    // ordinary code-point order on that key, NOT locale-aware collation.
    // Two invariants matter and both are asserted:
    //
    //   1. NFC/NFD fold: "é" precomposed (U+00E9) and decomposed
    //      ("e" + U+0301) name the same letter; NFC-normalizing both keys
    //      collapses them to the same U+00E9 prefix, so `éclair` and
    //      `étude` sort adjacently (by their 2nd letter, c < t) regardless
    //      of the raw byte order the filesystem hands back.
    //   2. Determinism: the order is byte-stable across rescans.
    //
    // Because the key is code-point order, a Latin-1 accented letter
    // (U+00E9 = 233) sorts *after* ASCII 'z' (0x7A = 122) — deliberately,
    // to match the spec's literal key rather than a collator the backend
    // doesn't have. That ordering is the point of the assertion below.
    let nfc = "\u{00e9}clair.md"; // éclair (precomposed)
    let nfd = "e\u{0301}tude.md"; // étude (decomposed)
    let (_tmp, session) = make_vault(|p| {
        p.write_file(nfc, b"1").unwrap();
        p.write_file(nfd, b"2").unwrap();
        p.write_file("zed.md", b"3").unwrap();
        p.write_file("Apple.md", b"4").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let listing = session.list_dir_children("", Paging::first(100)).unwrap();
    let names = file_names(&listing);
    // apple (0x61) < zed (0x7A) < é… (0xE9); the accented pair is adjacent
    // and in fold order (éclair before étude).
    assert_eq!(names, vec!["Apple.md", "zed.md", nfc, nfd]);

    // Determinism: a second identical scan + query yields the same order.
    session.scan_initial(&CancelToken::new()).unwrap();
    let again = session.list_dir_children("", Paging::first(100)).unwrap();
    assert_eq!(file_names(&again), names);
}

// --- unit: dot-dir exclusion ---------------------------------------------

#[test]
fn dot_dirs_excluded_from_tree() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("real.md", b"r").unwrap();
        p.write_file(".obsidian/workspace.json", b"{}").unwrap();
        p.write_file(".hidden/note.md", b"h").unwrap();
    });
    mkdir(tmp.path(), ".git");
    session.scan_initial(&CancelToken::new()).unwrap();

    let listing = session.list_dir_children("", Paging::first(100)).unwrap();
    // No dot-prefixed directory appears.
    assert!(
        listing.dirs.is_empty(),
        "dot dirs must not appear: {:?}",
        dir_names(&listing)
    );
    assert_eq!(file_names(&listing), vec!["real.md"]);
}

// --- unit: empty dir inclusion -------------------------------------------

#[test]
fn empty_dir_included_in_tree() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("keep.md", b"k").unwrap();
    });
    mkdir(tmp.path(), "EmptyFolder");
    mkdir(tmp.path(), "nested/alsoempty");
    session.scan_initial(&CancelToken::new()).unwrap();

    let root = session.list_dir_children("", Paging::first(100)).unwrap();
    assert_eq!(dir_names(&root), vec!["EmptyFolder", "nested"]);
    let empty = root.dirs.iter().find(|d| d.name == "EmptyFolder").unwrap();
    assert_eq!(empty.child_dir_count, 0);
    assert_eq!(empty.child_file_count, 0);

    let nested = session
        .list_dir_children("nested", Paging::first(100))
        .unwrap();
    assert_eq!(dir_names(&nested), vec!["alsoempty"]);
    assert!(file_names(&nested).is_empty());
}

// --- unit: paging --------------------------------------------------------

#[test]
fn file_paging_round_trips_case_insensitive_order() {
    let (_tmp, session) = make_vault(|p| {
        // Ten files whose case-insensitive order differs from binary order
        // (mix of upper/lower first letters).
        for (i, c) in ["b", "A", "d", "C", "f", "E", "h", "G", "j", "I"]
            .iter()
            .enumerate()
        {
            p.write_file(&format!("{c}{i}.md"), b"x").unwrap();
        }
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    // Expected full case-insensitive order.
    let full = session.list_dir_children("", Paging::first(100)).unwrap();
    let expected = file_names(&full);
    assert_eq!(expected.len(), 10);
    assert_eq!(full.files.total_filtered, 10);

    // Page in threes, following next_cursor, and reassemble.
    let mut collected: Vec<String> = Vec::new();
    let mut cursor: Option<String> = None;
    loop {
        let paging = match cursor.take() {
            Some(c) => Paging::after(c, 3),
            None => Paging::first(3),
        };
        let page = session.list_dir_children("", paging).unwrap();
        assert_eq!(page.files.total_filtered, 10);
        collected.extend(file_names(&page));
        match page.files.next_cursor {
            Some(c) => cursor = Some(c),
            None => break,
        }
    }
    assert_eq!(collected, expected, "paged order must equal full order");
}

#[test]
fn unicode_tree_order_survives_page_boundaries() {
    let nfc = "éclair.md";
    let nfd = "e\u{0301}tude.md";
    let (_tmp, session) = make_vault(|p| {
        for name in [nfd, "Zed.md", nfc, "apple.md", "Beta.md"] {
            p.write_file(name, b"# note\n").unwrap();
        }
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let mut names = Vec::new();
    let mut cursor = None;
    loop {
        let listing = session
            .list_dir_children(
                "",
                Paging {
                    cursor: cursor.take(),
                    limit: 2,
                },
            )
            .unwrap();
        assert_eq!(listing.files.total_filtered, 5);
        names.extend(file_names(&listing));
        match listing.files.next_cursor {
            Some(next) => cursor = Some(next),
            None => break,
        }
    }
    assert_eq!(names, ["apple.md", "Beta.md", "Zed.md", nfc, nfd]);
}

#[test]
fn equal_tree_sort_keys_use_binary_path_tiebreak_across_pages() {
    let (_tmp, session) = make_vault(|provider| {
        provider.write_file("a-path.md", b"# a\n").unwrap();
        provider.write_file("z-path.md", b"# z\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    {
        // A default case-insensitive APFS temp volume cannot hold two actual
        // names that differ only by case. Change only the cached display-name
        // column so both rows have the same NFC+lowercase key while their
        // distinct real paths exercise the contract's binary tiebreak.
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "UPDATE files SET name = 'Case.md' WHERE path = 'z-path.md'",
            [],
        )
        .unwrap();
        conn.execute(
            "UPDATE files SET name = 'case.md' WHERE path = 'a-path.md'",
            [],
        )
        .unwrap();
    }

    let first = session.list_dir_children("", Paging::first(1)).unwrap();
    assert_eq!(
        first
            .files
            .items
            .iter()
            .map(|file| file.path.as_str())
            .collect::<Vec<_>>(),
        ["a-path.md"]
    );
    assert_eq!(first.files.next_cursor.as_deref(), Some("1"));

    let second = session
        .list_dir_children("", Paging::after("1".to_string(), 1))
        .unwrap();
    assert_eq!(
        second
            .files
            .items
            .iter()
            .map(|file| file.path.as_str())
            .collect::<Vec<_>>(),
        ["z-path.md"]
    );
    assert_eq!(second.files.next_cursor, None);
}

#[test]
fn directory_page_enriches_only_limit_plus_one_candidates() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("alpha.md", b"# alpha\n").unwrap();
        p.write_file("beta.md", b"# beta\n").unwrap();
        p.write_file("zulu.md", b"# zulu\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "UPDATE file_meta
             SET word_count = 4294967296
             WHERE file_id = (SELECT id FROM files WHERE path = 'zulu.md')",
            [],
        )
        .unwrap();
    }

    // The first one-row page may fetch one lookahead candidate, but zulu is
    // beyond both and must not enter the expensive enrichment joins yet.
    let first = session.list_dir_children("", Paging::first(1)).unwrap();
    assert_eq!(file_names(&first), ["alpha.md"]);
    assert_eq!(first.files.next_cursor.as_deref(), Some("1"));
    assert_eq!(first.files.total_filtered, 3);

    // Requesting the page that actually includes zulu still exercises the
    // checked summary decoder and surfaces the corrupt value.
    let error = session
        .list_dir_children("", Paging::after("2".to_string(), 1))
        .expect_err("the corrupt candidate must fail only on its own page");
    assert!(error.to_string().contains("out of range"));
}

#[test]
fn root_parent_path_and_dot_are_equivalent() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"a").unwrap();
        p.write_file("dir/b.md", b"b").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let empty = session.list_dir_children("", Paging::first(100)).unwrap();
    let dot = session.list_dir_children(".", Paging::first(100)).unwrap();
    assert_eq!(dir_names(&empty), dir_names(&dot));
    assert_eq!(file_names(&empty), file_names(&dot));
}

#[test]
fn trailing_slash_and_dot_slash_normalize() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/x.md", b"x").unwrap();
        p.write_file("notes/sub/y.md", b"y").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let plain = session
        .list_dir_children("notes", Paging::first(100))
        .unwrap();
    let dotslash = session
        .list_dir_children("./notes", Paging::first(100))
        .unwrap();
    assert_eq!(file_names(&plain), file_names(&dotslash));
    assert_eq!(dir_names(&plain), dir_names(&dotslash));
}

// --- unit: path safety ---------------------------------------------------

#[test]
fn invalid_parent_paths_rejected() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"a").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    // Parent traversal is rejected.
    assert!(matches!(
        session.list_dir_children("../escape", Paging::first(10)),
        Err(VaultError::InvalidPath { .. })
    ));
    assert!(matches!(
        session.list_dir_children("notes/../..", Paging::first(10)),
        Err(VaultError::InvalidPath { .. })
    ));
    // Absolute path is rejected.
    assert!(matches!(
        session.list_dir_children("/etc", Paging::first(10)),
        Err(VaultError::InvalidPath { .. })
    ));
}

#[test]
fn nonexistent_parent_returns_empty_listing() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"a").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    // A valid-but-absent path is not an error; it's an empty level.
    let listing = session
        .list_dir_children("does/not/exist", Paging::first(10))
        .unwrap();
    assert!(listing.dirs.is_empty());
    assert!(listing.files.items.is_empty());
    assert_eq!(listing.files.total_filtered, 0);
}

// --- unit: bracket/star/question-mark names (GLOB hazards) ---------------

#[test]
fn glob_hazard_names_do_not_break_range_scan() {
    // `[`, `*`, `?` are legal in vault filenames and would be wildcards
    // under GLOB/LIKE; the range-scan must treat them literally.
    let (tmp, session) = make_vault(|p| {
        p.write_file("weird[1]/inside.md", b"i").unwrap();
        p.write_file("weird[1]/nested/deep.md", b"d").unwrap();
        p.write_file("star*dir/s.md", b"s").unwrap();
        p.write_file("q?dir/q.md", b"q").unwrap();
        // A sibling that would be matched by `weird[1]` glob but must NOT
        // be conflated: `weird1` (glob char class [1] matches '1').
        p.write_file("weird1/other.md", b"o").unwrap();
    });
    mkdir(tmp.path(), "weird[1]/emptybracket");
    session.scan_initial(&CancelToken::new()).unwrap();

    let root = session.list_dir_children("", Paging::first(100)).unwrap();
    // All four bracket/star/question/plain dirs present, distinct.
    let mut names = dir_names(&root);
    names.sort();
    assert_eq!(names, vec!["q?dir", "star*dir", "weird1", "weird[1]"]);

    // Listing the bracket dir returns exactly its immediate children, not
    // `weird1`'s.
    let bracket = session
        .list_dir_children("weird[1]", Paging::first(100))
        .unwrap();
    assert_eq!(dir_names(&bracket), vec!["emptybracket", "nested"]);
    assert_eq!(file_names(&bracket), vec!["inside.md"]);

    // And its counts are immediate-only.
    let bracket_node = root.dirs.iter().find(|d| d.name == "weird[1]").unwrap();
    assert_eq!(bracket_node.child_dir_count, 2); // nested, emptybracket
    assert_eq!(bracket_node.child_file_count, 1); // inside.md
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
    /// Uniform-ish in `[0, n)`; `n` small so modulo bias is negligible for
    /// a census.
    fn below(&mut self, n: usize) -> usize {
        (self.next_u64() % n as u64) as usize
    }
    fn chance(&mut self, numerator: u32, denominator: u32) -> bool {
        (self.next_u64() % denominator as u64) < numerator as u64
    }
}

/// The alphabet the random-vault generator draws segment names from —
/// spaces, unicode (NFC + NFD spellings of the same letters), and the
/// GLOB-hazard characters `[`, `*`, `?` that a LIKE/GLOB query would
/// mis-handle. Names are combined so collisions across NFC/NFD forms are
/// deliberately possible.
const NAME_ATOMS: &[&str] = &[
    "alpha",
    "Beta",
    "gamma note",
    "\u{00e9}",  // é precomposed
    "e\u{0301}", // é decomposed (same rendered glyph)
    "star*",
    "br[ack]",
    "q?mark",
    "Zed",
    "mix ED",
];

/// Build a random vault on disk under `root`. Returns the set of expected
/// (relative) directory paths and file paths, matching the scanner's
/// dot-exclusion rule (the generator never emits dot-prefixed names).
///
/// `depth`/`width` bound the tree; empty directories are emitted so the
/// "empty dir gets a row" contract is exercised.
fn generate_random_vault(
    root: &Path,
    rng: &mut SplitMix64,
    max_depth: usize,
    max_width: usize,
) -> (BTreeSet<String>, BTreeSet<String>) {
    let mut dirs: BTreeSet<String> = BTreeSet::new();
    let mut files: BTreeSet<String> = BTreeSet::new();
    // Always create the root itself on disk.
    std::fs::create_dir_all(root).unwrap();
    generate_into(root, "", rng, max_depth, max_width, &mut dirs, &mut files);
    (dirs, files)
}

#[allow(clippy::too_many_arguments)]
fn generate_into(
    root: &Path,
    parent_rel: &str,
    rng: &mut SplitMix64,
    depth_left: usize,
    max_width: usize,
    dirs: &mut BTreeSet<String>,
    files: &mut BTreeSet<String>,
) {
    // Unique child names at this level (a set so we don't fight the OS's
    // case-insensitive-but-case-preserving behavior in the same folder;
    // APFS is case-insensitive by default, so two names that differ only
    // by case/normal form would collide on disk — dedupe on the fold key).
    let mut used_keys: BTreeSet<String> = BTreeSet::new();
    let child_count = rng.below(max_width + 1);
    for _ in 0..child_count {
        // Assemble a 1–2 atom name.
        let mut name = NAME_ATOMS[rng.below(NAME_ATOMS.len())].to_string();
        if rng.chance(1, 3) {
            name.push_str(NAME_ATOMS[rng.below(NAME_ATOMS.len())]);
        }
        // Fold key mirrors the OS's collision surface: NFC + lowercase.
        let key = {
            use unicode_normalization::UnicodeNormalization;
            name.nfc().collect::<String>().to_lowercase()
        };
        if !used_keys.insert(key) {
            continue; // would collide on a case-insensitive volume
        }
        let rel = if parent_rel.is_empty() {
            name.clone()
        } else {
            format!("{parent_rel}/{name}")
        };
        // Decide: file or directory.
        if depth_left > 0 && rng.chance(1, 2) {
            // Directory (possibly empty).
            std::fs::create_dir_all(root.join(&rel)).unwrap();
            dirs.insert(rel.clone());
            generate_into(root, &rel, rng, depth_left - 1, max_width, dirs, files);
        } else {
            // File. Ensure the parent exists (it always does here).
            let fname = format!("{rel}.md");
            std::fs::write(root.join(&fname), b"# note\n").unwrap();
            files.insert(fname);
        }
    }
}

/// Walk `list_dir_children` recursively from the root, collecting every
/// directory path and file path the API reports. Also cross-checks that
/// each directory's reported child counts equal what the recursion sees.
fn walk_api(session: &VaultSession) -> (BTreeSet<String>, BTreeSet<String>) {
    let mut dirs = BTreeSet::new();
    let mut files = BTreeSet::new();
    walk_api_level(session, "", &mut dirs, &mut files);
    (dirs, files)
}

fn walk_api_level(
    session: &VaultSession,
    parent: &str,
    dirs: &mut BTreeSet<String>,
    files: &mut BTreeSet<String>,
) {
    // Page through files so paging is exercised inside the census too.
    let mut cursor: Option<String> = None;
    let mut listing_dirs: Vec<DirNodeSummary> = Vec::new();
    let mut first = true;
    loop {
        let paging = match cursor.take() {
            Some(c) => Paging::after(c, 3),
            None => Paging::first(3),
        };
        let listing = session.list_dir_children(parent, paging).unwrap();
        if first {
            listing_dirs = listing.dirs.clone();
            first = false;
        }
        for f in &listing.files.items {
            assert!(
                files.insert(f.path.clone()),
                "duplicate file path {:?}",
                f.path
            );
        }
        match listing.files.next_cursor {
            Some(c) => cursor = Some(c),
            None => break,
        }
    }
    for d in &listing_dirs {
        assert!(
            dirs.insert(d.path.clone()),
            "duplicate dir path {:?}",
            d.path
        );
    }
    // Recurse and, on the way, assert each child dir's counts match the
    // level we get when we descend into it.
    for d in &listing_dirs {
        let sub = session
            .list_dir_children(&d.path, Paging::first(1_000_000))
            .unwrap();
        assert_eq!(
            d.child_dir_count as usize,
            sub.dirs.len(),
            "child_dir_count mismatch for {:?}",
            d.path
        );
        assert_eq!(
            d.child_file_count as u64, sub.files.total_filtered,
            "child_file_count mismatch for {:?}",
            d.path
        );
        walk_api_level(session, &d.path, dirs, files);
    }
}

// --- census: tree matches filesystem -------------------------------------
//
// 500 random vaults (depth <= 6, width <= 12) with unicode / spaces /
// bracket / star / question-mark names, plus the exhaustive small shapes
// (every tree with <= 4 dirs). After scanning, the recursive
// `list_dir_children` walk must equal a direct filesystem walk: identical
// directory sets, identical file sets, identical counts.
//
// The 500 random seeds are split across four `#[test]` chunks so the test
// runner can schedule them in parallel: as one test fn this census ran
// 549s single-threaded on CI (run 29649179445) and was the entire test
// lane's wall-clock tail. The chunks cover exactly the same seeds with
// the same RNG construction — `census_dir_tree_seed_chunks_are_contiguous`
// machine-checks that the union is precisely 0..500, so a future edit
// can't silently drop coverage.

const DIR_TREE_SEED_CHUNKS: [(u64, u64); 4] = [(0, 125), (125, 250), (250, 375), (375, 500)];

#[test]
fn census_dir_tree_seed_chunks_are_contiguous() {
    assert_eq!(DIR_TREE_SEED_CHUNKS[0].0, 0);
    assert_eq!(DIR_TREE_SEED_CHUNKS[DIR_TREE_SEED_CHUNKS.len() - 1].1, 500);
    for pair in DIR_TREE_SEED_CHUNKS.windows(2) {
        assert_eq!(
            pair[0].1, pair[1].0,
            "seed chunks must be contiguous: {pair:?}"
        );
    }
}

#[test]
fn census_dir_tree_matches_filesystem_seeds_000_125() {
    dir_tree_matches_filesystem_for_seeds(DIR_TREE_SEED_CHUNKS[0]);
}

#[test]
fn census_dir_tree_matches_filesystem_seeds_125_250() {
    dir_tree_matches_filesystem_for_seeds(DIR_TREE_SEED_CHUNKS[1]);
}

#[test]
fn census_dir_tree_matches_filesystem_seeds_250_375() {
    dir_tree_matches_filesystem_for_seeds(DIR_TREE_SEED_CHUNKS[2]);
}

#[test]
fn census_dir_tree_matches_filesystem_seeds_375_500() {
    dir_tree_matches_filesystem_for_seeds(DIR_TREE_SEED_CHUNKS[3]);
}

fn dir_tree_matches_filesystem_for_seeds((lo, hi): (u64, u64)) {
    for seed in lo..hi {
        let tmp = tempfile::tempdir().unwrap();
        let mut rng = SplitMix64::new(seed);
        let (fs_dirs, fs_files) = generate_random_vault(tmp.path(), &mut rng, 6, 12);

        let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
        session.scan_initial(&CancelToken::new()).unwrap();

        let (api_dirs, api_files) = walk_api(&session);
        assert_eq!(
            api_dirs,
            fs_dirs,
            "seed {seed}: dir set mismatch\n  api-only: {:?}\n  fs-only: {:?}",
            api_dirs.difference(&fs_dirs).collect::<Vec<_>>(),
            fs_dirs.difference(&api_dirs).collect::<Vec<_>>()
        );
        assert_eq!(
            api_files,
            fs_files,
            "seed {seed}: file set mismatch\n  api-only: {:?}\n  fs-only: {:?}",
            api_files.difference(&fs_files).collect::<Vec<_>>(),
            fs_files.difference(&api_files).collect::<Vec<_>>()
        );
    }
}

// Exhaustive small shapes: every rooted tree with <= 4 directories.
// We enumerate parent-pointer forests over dir slots 1..=n where each
// slot's parent is any earlier slot or the root (slot 0). Each shape
// is realized on disk (each dir also gets one marker file) and checked.
#[test]
fn census_dir_tree_matches_filesystem_exhaustive_shapes() {
    for n in 0..=4usize {
        exhaustive_shapes(n, &mut |parents: &[usize]| {
            let tmp = tempfile::tempdir().unwrap();
            let mut fs_dirs: BTreeSet<String> = BTreeSet::new();
            let mut fs_files: BTreeSet<String> = BTreeSet::new();
            // Build paths for slots 1..=n. Slot 0 is the vault root.
            let mut path_of: Vec<String> = vec![String::new()]; // index 0 = root
            for (i, &parent) in parents.iter().enumerate() {
                let slot = i + 1;
                let name = format!("d{slot}");
                let rel = if parent == 0 {
                    name
                } else {
                    format!("{}/{}", path_of[parent], name)
                };
                std::fs::create_dir_all(tmp.path().join(&rel)).unwrap();
                fs_dirs.insert(rel.clone());
                // One marker file per dir so file-walk paths are exercised.
                let fpath = format!("{rel}/m.md");
                std::fs::write(tmp.path().join(&fpath), b"m").unwrap();
                fs_files.insert(fpath);
                path_of.push(rel);
            }
            // Also a root-level file so the root level is never empty.
            std::fs::write(tmp.path().join("root.md"), b"r").unwrap();
            fs_files.insert("root.md".to_string());

            let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
            session.scan_initial(&CancelToken::new()).unwrap();
            let (api_dirs, api_files) = walk_api(&session);
            assert_eq!(
                api_dirs, fs_dirs,
                "exhaustive n={n} parents={parents:?}: dir mismatch"
            );
            assert_eq!(
                api_files, fs_files,
                "exhaustive n={n} parents={parents:?}: file mismatch"
            );
        });
    }
}

/// Enumerate every parent-pointer forest over `n` directory slots, where
/// slot `i` (0-based, representing dir `i+1`) has parent in `0..=i` (0 =
/// root). This covers every distinct rooted tree shape with `n` dirs
/// (labelled — a superset of unlabelled shapes, which is fine: more
/// coverage, not less). Calls `f` with each parent vector.
fn exhaustive_shapes(n: usize, f: &mut impl FnMut(&[usize])) {
    let mut parents = vec![0usize; n];
    fn rec(i: usize, n: usize, parents: &mut Vec<usize>, f: &mut impl FnMut(&[usize])) {
        if i == n {
            f(parents);
            return;
        }
        for p in 0..=i {
            parents[i] = p;
            rec(i + 1, n, parents, f);
        }
    }
    if n == 0 {
        f(&[]);
    } else {
        rec(0, n, &mut parents, f);
    }
}

// --- census: dir ids stable across rescans -------------------------------

#[test]
fn census_dir_ids_stable_across_rescans() {
    // Scan, record every dir's (path -> id), then perturb the vault with
    // *unrelated* file churn (touch / add / remove files that don't move a
    // directory), rescan x3, and assert every surviving directory path
    // keeps its id. A newly created directory naturally gets a fresh id;
    // we only assert stability for paths present before and after.
    const SEEDS: u64 = 120;
    for seed in 0..SEEDS {
        let tmp = tempfile::tempdir().unwrap();
        let mut rng = SplitMix64::new(seed.wrapping_mul(0x1000_0001).wrapping_add(7));
        let (_fs_dirs, fs_files) = generate_random_vault(tmp.path(), &mut rng, 5, 8);

        let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
        session.scan_initial(&CancelToken::new()).unwrap();
        let baseline = dir_id_map(&session);

        let mut files: Vec<String> = fs_files.into_iter().collect();
        for round in 0..3 {
            // Touch an existing file (content change, same path).
            if !files.is_empty() {
                let idx = rng.below(files.len());
                let f = files[idx].clone();
                std::fs::write(tmp.path().join(&f), format!("changed {round}\n").as_bytes())
                    .unwrap();
            }
            // Add a new unrelated file in an existing directory (root or an
            // existing dir path).
            {
                let dir_choices: Vec<String> = baseline.keys().cloned().collect();
                let target_dir = if dir_choices.is_empty() || rng.chance(1, 3) {
                    String::new()
                } else {
                    dir_choices[rng.below(dir_choices.len())].clone()
                };
                let new_rel = if target_dir.is_empty() {
                    format!("added_{seed}_{round}.md")
                } else {
                    format!("{target_dir}/added_{seed}_{round}.md")
                };
                std::fs::write(tmp.path().join(&new_rel), b"new\n").unwrap();
                files.push(new_rel);
            }
            // Remove an unrelated file (never removes a directory).
            if files.len() > 1 {
                let idx = rng.below(files.len());
                let f = files.remove(idx);
                let _ = std::fs::remove_file(tmp.path().join(&f));
            }

            session.scan_initial(&CancelToken::new()).unwrap();
            let after = dir_id_map(&session);
            for (path, id) in &baseline {
                if let Some(after_id) = after.get(path) {
                    assert_eq!(
                        id, after_id,
                        "seed {seed} round {round}: dir {path:?} id changed {id} -> {after_id}"
                    );
                }
            }
        }
    }
}

/// Read `dirs` as a `path -> id` map straight from the DB (the census
/// asserts referential stability directly on the table the API reads).
fn dir_id_map(session: &VaultSession) -> BTreeMap<String, i64> {
    let conn = session.conn.lock().unwrap();
    let mut stmt = conn.prepare("SELECT path, id FROM dirs").unwrap();
    let rows = stmt
        .query_map([], |row| {
            Ok((row.get::<_, String>(0)?, row.get::<_, i64>(1)?))
        })
        .unwrap();
    rows.map(|r| r.unwrap()).collect()
}

// --- perf guard ----------------------------------------------------------

#[test]
fn perf_guard_root_listing_under_100ms_on_10k_files() {
    // Match the Criterion gate's difficult shape: 10k metadata-rich direct
    // root children. The budget in the spec is 10ms; assert 100ms (10x
    // headroom) so a loaded CI box still passes while a real regression
    // (accidental O(vault^2) or a per-file query) trips.
    let tmp = tempfile::tempdir().unwrap();
    for i in 0..10_000usize {
        std::fs::write(
            tmp.path().join(format!("note-{i:08}.md")),
            format!(
                "---\ntitle: Metadata note {i}\ncreated: 2026-07-14\n---\n# Note {i}\n\nPreview words for metadata-rich listing {i}.\n\n- [ ] open task {i}\n"
            ),
        )
        .unwrap();
    }

    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();

    let start = std::time::Instant::now();
    let listing = session.list_dir_children("", Paging::first(200)).unwrap();
    let elapsed = start.elapsed();

    assert!(listing.dirs.is_empty());
    assert_eq!(listing.files.total_filtered, 10_000);
    assert_eq!(listing.files.items.len(), 200);
    assert_eq!(listing.files.next_cursor.as_deref(), Some("200"));
    assert!(
        elapsed < std::time::Duration::from_millis(100),
        "root list_dir_children took {elapsed:?}, over the 100ms guard (10x the 10ms budget)"
    );
}
