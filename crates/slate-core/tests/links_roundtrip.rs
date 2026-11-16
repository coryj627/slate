// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Property tests for the markdown → SQL → query round-trip of the
//! links table.
//!
//! The integration tests in `session.rs` already exercise the scan
//! path on hand-crafted vaults. This file fuzzes the
//! `extract_links` → `resolve_link` → `replace_links_for_file` →
//! `outgoing_links` / `backlinks` pipeline with randomly-shaped
//! inputs so we catch off-by-one / ordinal / dedup regressions that
//! a static fixture would miss.
//!
//! Each `proptest!` block opens a fresh tempdir and a fresh
//! `VaultSession`, so cases are independent.

use std::collections::HashSet;

use proptest::prelude::*;
use slate_core::{
    CancelToken, FsVaultProvider, Paging, VaultProvider, VaultSession, extract_links,
};

/// One link to include in a generated markdown body. Mirrors the three
/// classes of links the production resolver actually distinguishes:
/// internal-resolved, internal-unresolved, and external.
#[derive(Debug, Clone)]
enum LinkSpec {
    /// Wikilink whose target file is materialized in the vault before
    /// the scan, so it resolves to a real `target_path`.
    Resolvable(String),
    /// Wikilink whose target name is *not* present in the vault,
    /// producing an unresolved row.
    Unresolvable(String),
    /// `[label](https://…)` URL — external by construction.
    External,
}

/// The stems Windows reserves for devices, as the note-name pattern can
/// spell them. `FsVaultProvider::write_file` refuses `aux.md` and the rest
/// on Windows, and it is right to (#1963).
const WINDOWS_RESERVED_DEVICE_NAMES: [&str; 24] = [
    "con", "prn", "aux", "nul", "com0", "com1", "com2", "com3", "com4", "com5", "com6", "com7",
    "com8", "com9", "lpt0", "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9",
];

/// Strategy for one note name, which the properties below write as
/// `{name}.md`. Names are short lowercase ascii so the generated paths
/// stay vault-relative-friendly and the test stays fast (no time spent on
/// weird Unicode normalization). A reserved device name is never drawn, on
/// any platform, so the cases are the same everywhere.
fn note_name() -> impl Strategy<Value = String> {
    "[a-z][a-z0-9]{0,5}".prop_filter("a Windows reserved device name", |name| {
        !WINDOWS_RESERVED_DEVICE_NAMES.contains(&name.as_str())
    })
}

/// Strategy for one link spec.
fn link_spec_strategy() -> impl Strategy<Value = LinkSpec> {
    prop_oneof![
        note_name().prop_map(LinkSpec::Resolvable),
        note_name().prop_map(LinkSpec::Unresolvable),
        Just(LinkSpec::External),
    ]
}

/// #1963: `backlinks_contain_every_other_source` once drew `aux` and failed
/// on Windows, where the provider refuses to write `aux.md`. No property in
/// this file is about that refusal, so the generator never draws a reserved
/// device name, on any platform. The run is seeded: the bare pattern draws
/// several of them within these cases.
#[test]
fn a_generated_note_name_is_never_a_windows_reserved_device_name() {
    use proptest::strategy::ValueTree;
    use proptest::test_runner::TestRunner;

    let mut runner = TestRunner::deterministic();
    let strategy = note_name();
    let mut reserved: Vec<String> = Vec::new();
    for _ in 0..300_000 {
        let name = strategy.new_tree(&mut runner).unwrap().current();
        if WINDOWS_RESERVED_DEVICE_NAMES.contains(&name.as_str()) {
            reserved.push(name);
        }
    }
    assert!(
        reserved.is_empty(),
        "the generator drew reserved device names: {reserved:?}"
    );
}

/// Build a markdown body that contains exactly one link per
/// non-empty paragraph, in the order specs were given. Each
/// paragraph is prefixed with a unique index so pulldown-cmark
/// can't collapse them.
fn body_from_specs(specs: &[LinkSpec]) -> String {
    let mut body = String::new();
    for (i, spec) in specs.iter().enumerate() {
        let line = match spec {
            LinkSpec::Resolvable(name) | LinkSpec::Unresolvable(name) => {
                format!("Para {i}: see [[{name}]].\n\n")
            }
            LinkSpec::External => {
                format!("Para {i}: see [example](https://example.com/{i}).\n\n")
            }
        };
        body.push_str(&line);
    }
    body
}

/// Materialize every `Resolvable` target as an empty stub file in the
/// provider, deduplicated by name. Files we don't materialize stay
/// unresolved — the resolver will write `target_path = NULL`.
fn write_resolvable_targets(provider: &FsVaultProvider, specs: &[LinkSpec]) {
    let mut written: HashSet<String> = HashSet::new();
    for spec in specs {
        if let LinkSpec::Resolvable(name) = spec
            && written.insert(name.clone())
        {
            provider
                .write_file(&format!("{name}.md"), b"# stub\n")
                .unwrap();
        }
    }
}

proptest! {
    // Each case spins up a tempdir + SQLite cache, so keep the case
    // count small. 32 catches generator-level shape bugs without
    // blowing CI wall time on the default 256.
    #![proptest_config(ProptestConfig::with_cases(32))]

    /// For any markdown body with N parsed links, after a full scan
    /// the `outgoing_links` query returns exactly N rows in
    /// document order (ordinal 0 .. N-1), and each row's
    /// `is_external` flag mirrors what `extract_links` saw.
    #[test]
    fn outgoing_links_round_trip(
        specs in prop::collection::vec(link_spec_strategy(), 1..=8),
    ) {
        let body = body_from_specs(&specs);
        let parsed = extract_links(&body);

        let tmp = tempfile::tempdir().unwrap();
        let provider = FsVaultProvider::new(tmp.path().to_path_buf());
        write_resolvable_targets(&provider, &specs);
        provider.write_file("src.md", body.as_bytes()).unwrap();

        let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
        session.scan_initial(&CancelToken::new()).unwrap();

        let outgoing = session.outgoing_links("src.md").unwrap();

        prop_assert_eq!(
            outgoing.len(),
            parsed.len(),
            "outgoing_links count ({}) should match extract_links count ({})",
            outgoing.len(),
            parsed.len()
        );

        for (i, row) in outgoing.iter().enumerate() {
            prop_assert_eq!(
                row.ordinal as usize,
                i,
                "ordinals should be contiguous from 0; row {} has ordinal {}",
                i,
                row.ordinal
            );
        }

        for (row, p) in outgoing.iter().zip(parsed.iter()) {
            prop_assert_eq!(
                row.is_external,
                p.is_external,
                "is_external mismatch for ordinal {}: stored={}, parsed={}",
                row.ordinal,
                row.is_external,
                p.is_external
            );
        }
    }

    /// Re-scanning the same on-disk vault produces an
    /// outgoing_links list byte-identical to the first scan's
    /// output. Catches DELETE/INSERT ordering regressions or
    /// nondeterminism in the resolver.
    #[test]
    fn rescan_does_not_change_outgoing_links(
        specs in prop::collection::vec(link_spec_strategy(), 1..=5),
    ) {
        let body = body_from_specs(&specs);

        let tmp = tempfile::tempdir().unwrap();
        let provider = FsVaultProvider::new(tmp.path().to_path_buf());
        write_resolvable_targets(&provider, &specs);
        provider.write_file("src.md", body.as_bytes()).unwrap();

        let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
        session.scan_initial(&CancelToken::new()).unwrap();
        let first = session.outgoing_links("src.md").unwrap();

        session.scan_initial(&CancelToken::new()).unwrap();
        let second = session.outgoing_links("src.md").unwrap();

        prop_assert_eq!(first, second);
    }
}

proptest! {
    // Backlinks symmetry requires N-by-N writes; cap N tighter and
    // run fewer cases so wall time stays bounded.
    #![proptest_config(ProptestConfig::with_cases(16))]

    /// In a vault where every file links to every other file, every
    /// file's backlinks must contain every other file. Verifies the
    /// resolver + write path + read path agree on "internal link"
    /// across multiple files.
    #[test]
    fn backlinks_contain_every_other_source(
        names in prop::collection::hash_set(note_name(), 2..=4),
    ) {
        // hash_set strategy already dedups; assume the lower bound.
        prop_assume!(names.len() >= 2);
        let names: Vec<String> = names.into_iter().collect();

        let tmp = tempfile::tempdir().unwrap();
        let provider = FsVaultProvider::new(tmp.path().to_path_buf());

        // Each file links to every other file via a plain wikilink.
        for from in &names {
            let body: String = names
                .iter()
                .filter(|t| *t != from)
                .enumerate()
                .map(|(i, t)| format!("Line {i}: [[{t}]]\n\n"))
                .collect();
            provider
                .write_file(&format!("{from}.md"), body.as_bytes())
                .unwrap();
        }

        let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
        session.scan_initial(&CancelToken::new()).unwrap();

        for target in &names {
            let target_path = format!("{target}.md");
            let page = session
                .backlinks(&target_path, Paging::first(100))
                .unwrap();
            let sources: HashSet<String> =
                page.items.iter().map(|b| b.source_path.clone()).collect();

            for source in &names {
                if source == target {
                    continue;
                }
                let source_path = format!("{source}.md");
                prop_assert!(
                    sources.contains(&source_path),
                    "{source_path} should appear in {target_path}'s backlinks; got {sources:?}",
                );
            }
        }
    }
}
