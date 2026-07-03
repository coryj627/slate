// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! U2-3 part 2 (#461): link integrity on move/rename — the session-level
//! RED-TEAM censuses over the real mutation + rewrite pipeline. The pure
//! planner has its own census (`link_rewrite.rs`); these exercise the
//! wiring: tx1 index/link-column updates, per-file rewrites through the
//! standard save path, journal capture, and byte-exact undo.
//!
//! Scale: default runs finish in minutes; `SLATE_CENSUS_FULL=1` (release)
//! is the pre-push confirmation scale, recorded in the PR.

fn census_scale() -> u64 {
    if std::env::var("SLATE_CENSUS_FULL").as_deref() == Ok("1") {
        120
    } else {
        40
    }
}

struct SplitMix64(u64);
impl SplitMix64 {
    fn next(&mut self) -> u64 {
        self.0 = self.0.wrapping_add(0x9E37_79B9_7F4A_7C15);
        let mut z = self.0;
        z = (z ^ (z >> 30)).wrapping_mul(0xBF58_476D_1CE4_E5B9);
        z = (z ^ (z >> 27)).wrapping_mul(0x94D0_49BB_1331_11EB);
        z ^ (z >> 31)
    }
    fn below(&mut self, n: usize) -> usize {
        (self.next() % n.max(1) as u64) as usize
    }
}

/// A vault with a deliberately ambiguous link graph: five link forms,
/// basename collisions, unresolved links, links from/to every directory.
fn linked_vault(rng: &mut SplitMix64) -> (tempfile::TempDir, crate::VaultSession) {
    let dir = tempfile::tempdir().expect("tempdir");
    let root = dir.path();
    let dirs = ["", "a", "a/b", "c", "notes"];
    let stems = ["n0", "n1", "n2", "shared", "shared"]; // duplicate stem on purpose
    let mut paths: Vec<String> = Vec::new();
    for (i, d) in dirs.iter().enumerate() {
        for (j, stem) in stems.iter().enumerate() {
            if (i + j) % 2 == 0 {
                let path = if d.is_empty() {
                    format!("{stem}{i}.md")
                } else {
                    format!("{d}/{stem}{j}.md")
                };
                if !paths.contains(&path) {
                    paths.push(path);
                }
            }
        }
    }
    let all = paths.clone();
    for path in &paths {
        let mut body = format!("# {path}\n");
        for _ in 0..(rng.below(4) + 1) {
            let target = &all[rng.below(all.len())];
            let stem = target.rsplit('/').next().unwrap().trim_end_matches(".md");
            match rng.below(7) {
                0 => body.push_str(&format!("w [[{stem}]]\n")),
                1 => body.push_str(&format!("q [[{}]]\n", target.trim_end_matches(".md"))),
                2 => body.push_str(&format!("a [[{stem}|alias]]\n")),
                3 => body.push_str(&format!("h [[{stem}#Top]]\n")),
                4 => body.push_str(&format!("e ![[{stem}]]\n")),
                5 => body.push_str(&format!("m [t]({target})\n")),
                _ => body.push_str("u [[totally-unresolved]]\n"),
            }
        }
        let full = root.join(path);
        std::fs::create_dir_all(full.parent().unwrap()).unwrap();
        std::fs::write(full, body).unwrap();
    }
    let session = crate::VaultSession::from_filesystem(root.to_path_buf()).expect("open");
    session
        .scan_initial(&crate::CancelToken::new())
        .expect("scan");
    (dir, session)
}

fn vault_hashes(root: &std::path::Path) -> std::collections::BTreeMap<String, String> {
    let mut out = std::collections::BTreeMap::new();
    let mut stack = vec![root.to_path_buf()];
    while let Some(dir) = stack.pop() {
        for entry in std::fs::read_dir(&dir).unwrap() {
            let entry = entry.unwrap();
            let name = entry.file_name().to_string_lossy().into_owned();
            if name.starts_with('.') {
                continue;
            }
            let path = entry.path();
            if path.is_dir() {
                stack.push(path);
            } else {
                let rel = path
                    .strip_prefix(root)
                    .unwrap()
                    .to_string_lossy()
                    .replace('\\', "/");
                let bytes = std::fs::read(&path).unwrap();
                out.insert(rel, crate::vault::content_hash(&bytes));
            }
        }
    }
    out
}

/// (source_path, ordinal) → resolved target path, computed from the FILES
/// ON DISK via the production extractor + resolver (independent of the
/// links table, so the census cross-checks the pipeline, not itself).
fn resolution_map(root: &std::path::Path) -> std::collections::BTreeMap<(String, usize), String> {
    let hashes = vault_hashes(root);
    let index = crate::InMemoryVaultIndex::new(hashes.keys().cloned().collect());
    let mut out = std::collections::BTreeMap::new();
    for path in hashes.keys() {
        if !path.ends_with(".md") {
            continue;
        }
        let text = std::fs::read_to_string(root.join(path)).unwrap();
        for (ordinal, link) in crate::links::extract_links(&text).into_iter().enumerate() {
            if link.is_external {
                continue;
            }
            if let crate::link_resolver::ResolvedLink::Resolved { target_path, .. } =
                crate::link_resolver::resolve_link(
                    &link.target_raw,
                    link.anchor.clone(),
                    path,
                    &index,
                )
            {
                out.insert((path.clone(), ordinal), target_path);
            }
        }
    }
    out
}

/// One random structural mutation through the session; returns the report
/// (accepted ops only) with the op description for failure messages.
fn random_move(
    session: &crate::VaultSession,
    rng: &mut SplitMix64,
    step: usize,
) -> Option<(String, crate::structural::StructuralReport)> {
    let files: Vec<String> = {
        let conn = session.conn.lock().unwrap();
        let mut stmt = conn
            .prepare("SELECT path FROM files ORDER BY path")
            .unwrap();
        stmt.query_map([], |row| row.get::<_, String>(0))
            .unwrap()
            .collect::<Result<Vec<_>, _>>()
            .unwrap()
    };
    let dirs: Vec<String> = {
        let conn = session.conn.lock().unwrap();
        let mut stmt = conn.prepare("SELECT path FROM dirs ORDER BY path").unwrap();
        stmt.query_map([], |row| row.get::<_, String>(0))
            .unwrap()
            .collect::<Result<Vec<_>, _>>()
            .unwrap()
    };
    match rng.below(4) {
        0 if !files.is_empty() => {
            let from = &files[rng.below(files.len())];
            let name = format!("moved{step}.md");
            session
                .rename_file(from, &name)
                .ok()
                .map(|r| (format!("rename_file {from} -> {name}"), r))
        }
        1 if !files.is_empty() && !dirs.is_empty() => {
            let from = &files[rng.below(files.len())];
            let parent = &dirs[rng.below(dirs.len())];
            session
                .move_file(from, parent)
                .ok()
                .map(|r| (format!("move_file {from} -> {parent}/"), r))
        }
        2 if !dirs.is_empty() => {
            let target = &dirs[rng.below(dirs.len())];
            let name = format!("dir{step}");
            session
                .rename_folder(target, &name)
                .ok()
                .map(|r| (format!("rename_folder {target} -> {name}"), r))
        }
        _ if !dirs.is_empty() => {
            let target = &dirs[rng.below(dirs.len())];
            let parent = &dirs[rng.below(dirs.len())];
            session
                .move_folder(target, parent)
                .ok()
                .map(|r| (format!("move_folder {target} -> {parent}/"), r))
        }
        _ => None,
    }
}

/// RED-TEAM census: referential stability through the real pipeline.
/// After every accepted mutation: every link that resolved to file F
/// before still resolves to F (F tracked through the move mapping); files
/// outside the rewrite set are byte-identical.
#[test]
fn census_link_graph_referential_stability_session() {
    let seeds = census_scale();
    for seed in 0..seeds {
        let mut rng = SplitMix64(seed.wrapping_mul(0x00C0_FFEE).wrapping_add(3));
        let (dir, session) = linked_vault(&mut rng);
        for step in 0..12 {
            let before_resolution = resolution_map(dir.path());
            let before_hashes = vault_hashes(dir.path());
            let Some((desc, report)) = random_move(&session, &mut rng, step) else {
                continue;
            };
            let mapping: std::collections::HashMap<&str, &str> = report
                .moved
                .iter()
                .map(|(old, new)| (old.as_str(), new.as_str()))
                .collect();
            let after_resolution = resolution_map(dir.path());
            for ((src, ordinal), old_target) in &before_resolution {
                let new_src = mapping.get(src.as_str()).copied().unwrap_or(src);
                let expected = mapping
                    .get(old_target.as_str())
                    .copied()
                    .unwrap_or(old_target);
                let got = after_resolution.get(&(new_src.to_string(), *ordinal));
                assert_eq!(
                    got.map(String::as_str),
                    Some(expected),
                    "seed {seed} step {step} ({desc}): link {ordinal} in {src} \
                     resolved to {old_target} before; now {got:?} (expected {expected})"
                );
            }
            // Byte discipline: only moved files (path change, same bytes)
            // and rewritten files may differ.
            let rewritten: std::collections::BTreeSet<&str> =
                report.rewritten.iter().map(|r| r.path.as_str()).collect();
            let after_hashes = vault_hashes(dir.path());
            for (path, hash) in &before_hashes {
                let new_path = mapping.get(path.as_str()).copied().unwrap_or(path);
                let Some(after) = after_hashes.get(new_path) else {
                    panic!("seed {seed} step {step} ({desc}): {new_path} vanished");
                };
                if !rewritten.contains(new_path) {
                    assert_eq!(
                        after, hash,
                        "seed {seed} step {step} ({desc}): {new_path} changed \
                         without being reported as rewritten"
                    );
                }
            }
            // Failures must be surfaced, never silent: in this census no
            // external writer races us, so failures indicate a bug.
            assert!(
                report.failed.is_empty(),
                "seed {seed} step {step} ({desc}): unexpected rewrite failures {:?}",
                report.failed
            );
        }
    }
}

/// Round-trip: move ⊕ undo restores every byte in the vault — including
/// rewritten link text (the plan's 'move then undo restores byte-identical
/// link text' acceptance).
#[test]
fn census_move_undo_restores_bytes() {
    let seeds = census_scale();
    for seed in 0..seeds {
        let mut rng = SplitMix64(seed.wrapping_mul(0x5EED_CAFE).wrapping_add(7));
        let (dir, session) = linked_vault(&mut rng);
        for step in 0..6 {
            let before = vault_hashes(dir.path());
            let Some((desc, report)) = random_move(&session, &mut rng, step) else {
                continue;
            };
            session
                .undo_structural(report.op_id)
                .unwrap_or_else(|e| panic!("seed {seed} step {step} ({desc}) undo: {e:?}"));
            assert_eq!(
                vault_hashes(dir.path()),
                before,
                "seed {seed} step {step} ({desc}): undo not byte-identical"
            );
        }
    }
}

/// The spec's tie-break fixture, end to end through the session: moving a
/// source nearer another same-stem file must pin its basename link to the
/// ORIGINAL target — and the links table must agree with the text.
#[test]
fn tie_break_flip_pinned_end_to_end() {
    let dir = tempfile::tempdir().expect("tempdir");
    let root = dir.path();
    for (path, body) in [
        ("a/src.md", "L [[note]]\n"),
        ("a/note.md", "# a-note\n"),
        ("b/note.md", "# b-note\n"),
        ("b/keep.md", "# keep\n"),
    ] {
        let full = root.join(path);
        std::fs::create_dir_all(full.parent().unwrap()).unwrap();
        std::fs::write(full, body).unwrap();
    }
    let session = crate::VaultSession::from_filesystem(root.to_path_buf()).expect("open");
    session
        .scan_initial(&crate::CancelToken::new())
        .expect("scan");

    let report = session.move_file("a/src.md", "b").expect("move");
    assert_eq!(
        report.rewritten.len(),
        1,
        "the basename link must be pinned: {report:?}"
    );
    let text = std::fs::read_to_string(root.join("b/src.md")).unwrap();
    assert_eq!(text, "L [[a/note]]\n", "pinned to the original target");
    // And the index agrees.
    let links = session.outgoing_links("b/src.md").expect("links");
    assert_eq!(links.len(), 1);
    assert_eq!(links[0].target_path.as_deref(), Some("a/note.md"));
}
