// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! #1455: a test never moves its fixtures into the Trash of whoever runs it,
//! a developer's `~/.Trash` or a CI runner's Trash or Recycle Bin.
//!
//! - In a test build every `FsVaultProvider::new` gets its own `ScratchTrash`
//!   (`trash_handoff::default_trash`), so `VaultSession::from_filesystem`,
//!   the session facts' `make_vault` and every wrapper around a real provider
//!   (`FaultInjectingProvider`, `CancellingProvider`, …) trash into a scratch
//!   bin with no change of their own.
//! - The real system Trash is reached only by the gated real-Trash facts in
//!   `trash_handoff_tests.rs`, through `real_system_trash_provider`, and in a
//!   test build it refuses any path that is not on their scratch volume.
//! - Test code outside this library (the integration tests, benches, and the
//!   CLI's and the FFI's own tests) links the shipping default, the system
//!   Trash, so none of it may trash before routing its process into a
//!   scratch bin (`route_trash_to_scratch_bin_for_tests`).
//!
//! The hosts' suites keep the same rule with their own censuses:
//! `ScratchTrashCensusTests` on mac and `ScratchTrashCensus` on Windows.

use std::collections::BTreeMap;
use std::path::{Path, PathBuf};

use super::{FsVaultProvider, VaultProvider};
use crate::{CancelToken, VaultSession};

/// The gated real-Trash set (`trash_handoff_tests.rs`): the only facts that
/// hand their items to the real system Trash, each on a scratch volume.
const GATED_REAL_TRASH_FACTS: [&str; 5] = [
    // macOS, ignored: need SLATE_TRASH_PROBE_VOLUME, an attached scratch
    // APFS image whose Trash is its own `.Trashes/<uid>`.
    "real_trash_on_a_scratch_volume_lands_in_its_trashes_with_put_back_records",
    "real_trash_from_many_processes_at_once_loses_nothing",
    // Its child processes, which act only with the parent's variables.
    "real_trash_race_child",
    // Linux, every run: a child whose XDG_DATA_HOME, and so whose home
    // Trash, is inside a scratch directory.
    "real_trash_on_linux_lands_in_a_scratch_home_trash",
    "real_trash_linux_child",
];

/// The only helper that builds a provider on the real system Trash.
const REAL_TRASH_HELPER: &str = "real_system_trash_provider";

fn crate_root() -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR"))
}

/// Every `.rs` file beneath `dir`, sorted.
fn rust_sources(dir: &Path) -> Vec<PathBuf> {
    let mut found = Vec::new();
    let mut pending = vec![dir.to_path_buf()];
    while let Some(next) = pending.pop() {
        let Ok(entries) = std::fs::read_dir(&next) else {
            continue;
        };
        for entry in entries {
            let path = entry.unwrap().path();
            if path.is_dir() {
                pending.push(path);
            } else if path.extension().is_some_and(|extension| extension == "rs") {
                found.push(path);
            }
        }
    }
    found.sort();
    found
}

/// `path` relative to `base`, with forward slashes.
fn relative(path: &Path, base: &Path) -> String {
    path.strip_prefix(base)
        .unwrap_or(path)
        .to_string_lossy()
        .replace('\\', "/")
}

/// The source's lines that are code: comment lines (`//`, `///`, `//!`) are
/// dropped, so prose about a name is not a use of it.
fn code_lines(source: &str) -> impl Iterator<Item = (usize, &str)> {
    source
        .lines()
        .enumerate()
        .filter(|(_, line)| !line.trim_start().starts_with("//"))
}

/// The name of the function whose body holds line `index`: the nearest `fn`
/// declaration above it.
fn enclosing_fn(lines: &[&str], index: usize) -> Option<String> {
    lines[..=index].iter().rev().find_map(|line| {
        let declaration = line.trim_start();
        let rest = ["fn ", "pub fn ", "pub(crate) fn ", "pub(super) fn "]
            .iter()
            .find_map(|prefix| declaration.strip_prefix(prefix))?;
        Some(
            rest.chars()
                .take_while(|c| c.is_alphanumeric() || *c == '_')
                .collect(),
        )
    })
}

/// `name` left the vault, and a `ScratchTrash` was handed its path beneath
/// the vault: the deletion went to a scratch bin, not to a system Trash.
fn assert_trashed_into_a_scratch_bin(vault: &Path, name: &str) {
    assert!(
        vault.join(name).symlink_metadata().is_err(),
        "{name} is still in the vault"
    );
    let vault = std::fs::canonicalize(vault).unwrap();
    let handed: Vec<PathBuf> = super::trash_handoff::scratch_handoffs()
        .into_iter()
        .filter(|path| path.file_name().is_some_and(|leaf| leaf == name))
        .collect();
    assert!(
        handed.iter().any(|path| path.starts_with(&vault)),
        "{name} was never handed to a scratch Trash bin: {handed:?}"
    );
}

/// The ways core's tests build a filesystem provider or session all trash
/// into a scratch bin: `FsVaultProvider::new` (which every wrapper around a
/// real provider holds) and `VaultSession::from_filesystem` (which
/// `make_vault` and the session facts use).
#[test]
fn every_provider_and_session_a_test_builds_trashes_into_a_scratch_bin() {
    let tmp = tempfile::tempdir().unwrap();
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider
        .write_file("census-1455-provider.md", b"provider")
        .unwrap();
    provider.delete("census-1455-provider.md").unwrap();
    assert_trashed_into_a_scratch_bin(tmp.path(), "census-1455-provider.md");

    provider
        .write_file("census-1455-session.md", b"session")
        .unwrap();
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    let cancel = CancelToken::new();
    session.scan_initial(&cancel).unwrap();
    session.delete_file("census-1455-session.md").unwrap();
    assert_trashed_into_a_scratch_bin(tmp.path(), "census-1455-session.md");
}

/// In a test build the system Trash refuses a path off the gated facts'
/// scratch volume before it calls the platform. The path is one that does
/// not exist, so even without the refusal nothing could move.
#[test]
fn the_system_trash_refuses_a_test_path_off_the_scratch_volume() {
    use super::trash_handoff::{PlatformTrash, SystemTrash};

    let path = crate_root().join("census-1455-never-created.md");
    assert!(path.symlink_metadata().is_err());
    let refusal = std::panic::catch_unwind(|| PlatformTrash.trash(&path))
        .expect_err("the system Trash took a test path that is not on a scratch volume");
    let message = refusal
        .downcast_ref::<String>()
        .cloned()
        .unwrap_or_default();
    assert!(message.contains("#1455"), "{message}");
}

/// Only `trash_handoff` (which owns `PlatformTrash` and picks each provider's
/// default Trash) and the gated facts' one helper name the platform Trash.
/// Before #1455, `FsVaultProvider::new` named it, so every test's provider
/// trashed into the Trash of whoever ran the suite.
#[test]
fn only_trash_handoff_and_the_gated_helper_name_the_platform_trash() {
    let src = crate_root().join("src");
    let files = rust_sources(&src);
    assert!(files.len() > 100, "the walk must see the whole crate");
    let mut namers: BTreeMap<String, Vec<usize>> = BTreeMap::new();
    for file in &files {
        let name = relative(file, &src);
        // This census spells the name it looks for.
        if name == "vault/test_trash_census.rs" {
            continue;
        }
        let source = std::fs::read_to_string(file).unwrap();
        for (index, line) in code_lines(&source) {
            if line.contains("PlatformTrash") {
                namers.entry(name.clone()).or_default().push(index + 1);
            }
        }
    }
    let helper_lines = namers
        .remove("vault/trash_handoff_tests.rs")
        .unwrap_or_default();
    namers.remove("vault/trash_handoff.rs");
    assert!(
        namers.is_empty(),
        "only trash_handoff picks the system Trash (#1455); a test that needs the real \
         Trash is a gated fact on a scratch volume and uses {REAL_TRASH_HELPER}. Named at \
         (file: lines): {namers:?}"
    );
    assert_eq!(
        helper_lines.len(),
        1,
        "trash_handoff_tests.rs names PlatformTrash only in {REAL_TRASH_HELPER}: lines \
         {helper_lines:?}"
    );
    let tests = std::fs::read_to_string(src.join("vault/trash_handoff_tests.rs")).unwrap();
    let lines: Vec<&str> = tests.lines().collect();
    assert_eq!(
        enclosing_fn(&lines, helper_lines[0] - 1).as_deref(),
        Some(REAL_TRASH_HELPER)
    );
}

/// The gated helper is called from the gated facts alone, and every gated
/// fact still exists, so the set can neither grow nor rot unseen.
#[test]
fn only_the_gated_facts_build_a_provider_on_the_real_trash() {
    let src = crate_root().join("src");
    let mut callers = Vec::new();
    for file in rust_sources(&src) {
        let name = relative(&file, &src);
        if name == "vault/test_trash_census.rs" {
            continue;
        }
        let source = std::fs::read_to_string(&file).unwrap();
        let lines: Vec<&str> = source.lines().collect();
        for (index, line) in code_lines(&source) {
            let calls = line.contains(&format!("{REAL_TRASH_HELPER}("));
            if calls && !line.contains(&format!("fn {REAL_TRASH_HELPER}(")) {
                callers.push((name.clone(), enclosing_fn(&lines, index)));
            }
        }
    }
    let strays: Vec<_> = callers
        .iter()
        .filter(|(file, caller)| {
            file != "vault/trash_handoff_tests.rs"
                || !caller
                    .as_deref()
                    .is_some_and(|caller| GATED_REAL_TRASH_FACTS.contains(&caller))
        })
        .collect();
    assert!(
        strays.is_empty(),
        "{REAL_TRASH_HELPER} hands deletions to the real system Trash; only the gated \
         real-Trash facts may call it (#1455): {strays:?}"
    );
    let tests = std::fs::read_to_string(src.join("vault/trash_handoff_tests.rs")).unwrap();
    for fact in GATED_REAL_TRASH_FACTS {
        assert!(
            tests.contains(&format!("fn {fact}(")),
            "the gated real-Trash fact {fact} is gone; update GATED_REAL_TRASH_FACTS"
        );
    }
    assert!(
        callers.len() >= 3,
        "the walk must see the gated facts' calls: {callers:?}"
    );
}

/// Test code outside this library links the shipping default, the system
/// Trash: the integration tests and benches, and the CLI's and the FFI's own
/// tests. None of them may trash through a session or provider (a Trash
/// endpoint, a provider's `delete`, or undoing a `create_folder`) unless it
/// first routes its process into a scratch bin. None does today.
#[test]
fn tests_outside_core_never_trash_without_a_scratch_bin() {
    let crates = crate_root().join("..");
    // The session's Trash endpoints (each `_staged`/`_cancellable` variant
    // included) and a provider's own `delete`.
    let trash_call =
        regex::Regex::new(r"\.(delete_file|delete_folder|batch_trash)\w*\s*\(|\.delete\s*\(")
            .unwrap();
    // A provider wrapper forwarding `delete` to the provider it wraps is not
    // a test trashing anything.
    let forwarding = regex::Regex::new(r"self\.(0|inner)\.delete\s*\(").unwrap();
    // `undo_structural` trashes only to undo a `create_folder` (the empty
    // folder goes to the Trash); undoing a move or a rename moves it back.
    let undo = regex::Regex::new(r"\.undo_structural\s*\(").unwrap();
    let creates_folder = regex::Regex::new(r"\.create_folder\s*\(").unwrap();
    // Whole files that are test code, and source trees whose test module
    // (`#[cfg(test)] mod tests`, conventionally last) is.
    let whole = ["slate-core/tests", "slate-core/benches", "slate-cli/tests"];
    let test_modules = ["slate-cli/src", "slate-uniffi/src"];
    let mut scanned = 0;
    let mut unrouted = Vec::new();
    for (tree, whole_file) in whole
        .iter()
        .map(|tree| (tree, true))
        .chain(test_modules.iter().map(|tree| (tree, false)))
    {
        for file in rust_sources(&crates.join(tree)) {
            let source = std::fs::read_to_string(&file).unwrap();
            let test_code = if whole_file {
                source.as_str()
            } else {
                match source.find("#[cfg(test)]\nmod tests") {
                    Some(start) => &source[start..],
                    None => continue,
                }
            };
            scanned += 1;
            let calls = |pattern: &regex::Regex| {
                code_lines(test_code).any(|(_, line)| pattern.is_match(line))
            };
            let trashes = code_lines(test_code)
                .any(|(_, line)| trash_call.is_match(line) && !forwarding.is_match(line))
                || (calls(&undo) && calls(&creates_folder));
            if trashes && !test_code.contains("route_trash_to_scratch_bin_for_tests") {
                unrouted.push(relative(&file, &crates));
            }
        }
    }
    assert!(
        scanned >= 15,
        "the walk must see the test code: {scanned} files"
    );
    assert!(
        unrouted.is_empty(),
        "these tests trash through the shipping default, the system Trash of whoever runs \
         them; route the process first with route_trash_to_scratch_bin_for_tests (#1455): \
         {unrouted:?}"
    );
}
