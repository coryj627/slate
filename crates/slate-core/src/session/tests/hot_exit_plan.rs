// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Plan 43 PR 29.1 (spec
//! `docs/plans/43_mac_backlog_delivery/specs/pr29_hot_exit_mac_half.md` §7):
//! core's hot-exit restore plan — which tab each kept entry comes back
//! in, how divergent peers on one note are reconciled, and that peers are
//! the entries whose paths name one file on disk (contract 32 I1), never
//! a case rule.

use super::*;
use crate::hot_exit::{
    HotExitCurrentClaim, HotExitEntry, HotExitPlannedTab, HotExitRedundantEntry, HotExitRestore,
    HotExitRestorePlan, HotExitSnapshot,
};
use std::collections::HashMap;
use std::path::Path;

fn session_at(root: &Path) -> VaultSession {
    let session = VaultSession::from_filesystem(root.to_path_buf()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    session
}

fn hash_of(root: &Path, physical: &str) -> String {
    crate::vault::content_hash(&std::fs::read(root.join(physical)).unwrap())
}

/// A kept entry; `minute` orders `written_at`.
fn entry(id: &str, path: &str, text: &str, baseline: Option<String>, minute: u32) -> HotExitEntry {
    HotExitEntry {
        schema: 1,
        entry_id: id.into(),
        incarnation: format!("inc-{id}"),
        generation: 1,
        path: path.into(),
        text: text.into(),
        baseline_hash: baseline,
        baseline_file_id: None,
        written_at: format!("2026-11-01T00:{minute:02}:00.000Z"),
    }
}

fn snapshot(entries: Vec<HotExitEntry>, claims: Vec<HotExitCurrentClaim>) -> HotExitSnapshot {
    HotExitSnapshot {
        epoch: 1,
        entries,
        claims,
    }
}

fn claim(id: &str, incarnation: &str) -> HotExitCurrentClaim {
    HotExitCurrentClaim {
        entry_id: id.into(),
        incarnation: incarnation.into(),
    }
}

fn ids(layout: &[&str]) -> Vec<String> {
    layout.iter().map(|id| id.to_string()).collect()
}

fn planned<'a>(tabs: &'a [HotExitPlannedTab], id: &str) -> &'a HotExitPlannedTab {
    tabs.iter()
        .find(|tab| tab.entry_id == id)
        .unwrap_or_else(|| panic!("no planned tab {id} in {tabs:?}"))
}

fn has(tabs: &[HotExitPlannedTab], id: &str) -> bool {
    tabs.iter().any(|tab| tab.entry_id == id)
}

#[test]
fn a_plan_restores_each_entry_into_the_layout_tab_with_its_id() {
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path();
    std::fs::write(root.join("a.md"), "# A\n").unwrap();
    std::fs::write(root.join("b.md"), "# B\n").unwrap();
    std::fs::write(root.join("c.md"), "# C\n").unwrap();
    let session = session_at(root);
    let snap = snapshot(
        vec![
            entry("t1", "a.md", "a edited", Some(hash_of(root, "a.md")), 1),
            entry("t3", "gone.md", "gone edited", Some("h".into()), 2),
            entry("t4", "c.md", "c edited", Some(hash_of(root, "c.md")), 3),
            entry("t2", "b.md", "b edited", Some(hash_of(root, "b.md")), 4),
        ],
        vec![claim("t1", "claim-t1")],
    );

    let plan = session.plan_hot_exit_restore(&snap, &ids(&["t1", "t3", "elsewhere"]));

    let t1 = planned(&plan.layout, "t1");
    assert_eq!(
        t1.decision,
        HotExitRestore::Restore {
            path: "a.md".into()
        }
    );
    assert_eq!((t1.path.as_str(), t1.text.as_str()), ("a.md", "a edited"));
    assert_eq!(t1.observed_claim.as_deref(), Some("claim-t1"));
    assert_eq!(t1.entry.incarnation, "inc-t1");
    let t3 = planned(&plan.layout, "t3");
    assert_eq!(
        t3.decision,
        HotExitRestore::Gone {
            path: "gone.md".into()
        }
    );
    assert_eq!(t3.observed_claim, None);
    // Unmatched entries are new tabs in `written_at` order; a layout id
    // with no entry plans nothing.
    let new: Vec<&str> = plan.new_tabs.iter().map(|t| t.entry_id.as_str()).collect();
    assert_eq!(new, ["t4", "t2"]);
    assert_eq!(plan.layout.len(), 2);
    assert!(plan.displaced.is_empty() && plan.redundant.is_empty());
    assert!(plan.orphan_claims.is_empty());
}

#[test]
fn divergent_peers_give_the_latest_text_to_every_tab_and_recover_the_rest() {
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path();
    std::fs::write(root.join("a.md"), "# A\n").unwrap();
    let session = session_at(root);
    let baseline = Some(hash_of(root, "a.md"));
    let snap = snapshot(
        vec![
            entry("p1", "a.md", "older", baseline.clone(), 1),
            entry("p2", "a.md", "newer", baseline, 2),
        ],
        vec![claim("p1", "claim-p1")],
    );

    let plan = session.plan_hot_exit_restore(&snap, &ids(&["p1", "p2"]));

    // The latest text goes to its own layout tab.
    let p2 = planned(&plan.layout, "p2");
    assert_eq!(p2.text, "newer");
    assert_eq!(
        p2.decision,
        HotExitRestore::Restore {
            path: "a.md".into()
        }
    );
    // The other distinct text becomes a recovered note keeping its id.
    let recovered = planned(&plan.new_tabs, "p1");
    assert_eq!(
        recovered.decision,
        HotExitRestore::Recovered {
            path: "a (recovered).md".into()
        }
    );
    assert_eq!(
        (recovered.path.as_str(), recovered.text.as_str()),
        ("a (recovered).md", "older")
    );
    assert_eq!(recovered.observed_claim.as_deref(), Some("claim-p1"));
    // The layout tab that held p1 is displaced: it shows the latest text.
    let displaced = planned(&plan.displaced, "p1");
    assert_eq!(displaced.text, "newer");
    assert_eq!(displaced.entry.entry_id, "p2");
    assert_eq!(displaced.path, "a.md");
    assert_eq!(displaced.observed_claim, None);
    assert!(!has(&plan.layout, "p1"));
    assert!(plan.redundant.is_empty());
}

#[test]
fn a_second_peer_with_the_same_text_is_redundant() {
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path();
    std::fs::write(root.join("a.md"), "# A\n").unwrap();
    let session = session_at(root);
    let baseline = Some(hash_of(root, "a.md"));
    let snap = snapshot(
        vec![
            entry("p1", "a.md", "older", baseline.clone(), 1),
            entry("p3", "a.md", "older", baseline.clone(), 2),
            entry("p2", "a.md", "newer", baseline, 3),
        ],
        vec![claim("p3", "claim-p3")],
    );

    let plan = session.plan_hot_exit_restore(&snap, &ids(&["p3"]));

    assert_eq!(planned(&plan.new_tabs, "p2").text, "newer");
    let recovered: Vec<&HotExitPlannedTab> = plan
        .new_tabs
        .iter()
        .filter(|tab| matches!(tab.decision, HotExitRestore::Recovered { .. }))
        .collect();
    assert_eq!(recovered.len(), 1, "one recovered note per distinct text");
    assert_eq!(recovered[0].entry_id, "p1");
    assert_eq!(
        plan.redundant,
        [HotExitRedundantEntry {
            entry_id: "p3".into(),
            incarnations: vec!["inc-p3".into(), "claim-p3".into()],
        }]
    );
    // p3's layout tab is displaced onto the latest text, never a note.
    assert_eq!(planned(&plan.displaced, "p3").text, "newer");
    assert!(!has(&plan.new_tabs, "p3"));
}

#[test]
fn a_recovered_name_never_lands_on_a_file_or_a_taken_name() {
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path();
    std::fs::create_dir(root.join("notes")).unwrap();
    std::fs::write(root.join("notes/a.md"), "# A\n").unwrap();
    std::fs::write(root.join("notes/a (recovered).md"), "a file").unwrap();
    let session = session_at(root);
    let baseline = Some(hash_of(root, "notes/a.md"));
    let snap = snapshot(
        vec![
            entry("p1", "notes/a.md", "older", baseline.clone(), 1),
            // Another kept note already holds the second name.
            entry("g", "notes/A (Recovered 2).md", "kept", None, 2),
            entry("p2", "notes/a.md", "newer", baseline, 3),
        ],
        vec![],
    );

    let plan = session.plan_hot_exit_restore(&snap, &ids(&[]));

    assert_eq!(
        planned(&plan.new_tabs, "p1").path,
        "notes/a (recovered 3).md",
        "past a file on disk and a name the plan took (compared without case)"
    );
}

#[test]
fn orphan_claims_are_claims_with_no_payload() {
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path();
    std::fs::write(root.join("a.md"), "# A\n").unwrap();
    let session = session_at(root);
    let snap = snapshot(
        vec![entry(
            "t1",
            "a.md",
            "edited",
            Some(hash_of(root, "a.md")),
            1,
        )],
        vec![claim("t1", "claim-t1"), claim("t9", "claim-t9")],
    );

    let plan = session.plan_hot_exit_restore(&snap, &ids(&["t1", "t9"]));

    assert_eq!(plan.orphan_claims, [claim("t9", "claim-t9")]);
    assert!(!has(&plan.layout, "t9") && !has(&plan.new_tabs, "t9"));
    assert_eq!(
        planned(&plan.layout, "t1").observed_claim.as_deref(),
        Some("claim-t1")
    );
}

#[test]
fn an_empty_snapshot_plans_nothing() {
    let tmp = tempfile::tempdir().unwrap();
    let session = session_at(tmp.path());
    assert_eq!(
        session.plan_hot_exit_restore(&snapshot(vec![], vec![]), &ids(&["t1"])),
        HotExitRestorePlan::default()
    );
}

// ---- peers are the entries whose paths name one file (contract 32 I1) ---

/// A provider whose spellings map to physical files and whose
/// `canonical_path` answers from a table, so the case facts behave the
/// same on a case-insensitive development volume and a case-sensitive CI
/// one.
struct SpellingProvider {
    inner: FsVaultProvider,
    physical: HashMap<String, String>,
    canonical: HashMap<String, Result<Option<String>, ()>>,
    /// `stat` of any path containing this fails with an error that is not
    /// "no such file": a volume that does not answer.
    refuse_stat_containing: Option<&'static str>,
}

impl SpellingProvider {
    fn map<'a>(&'a self, relative: &'a str) -> &'a str {
        self.physical
            .get(relative)
            .map(String::as_str)
            .unwrap_or(relative)
    }
}

impl crate::VaultProvider for SpellingProvider {
    fn list_dir(&self, relative: &str) -> Result<Vec<crate::DirEntry>, VaultError> {
        self.inner.list_dir(self.map(relative))
    }
    fn read_file(&self, relative: &str) -> Result<Vec<u8>, VaultError> {
        self.inner.read_file(self.map(relative))
    }
    fn write_file(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
        self.inner.write_file(self.map(relative), contents)
    }
    fn delete(&self, relative: &str) -> Result<(), VaultError> {
        self.inner.delete(self.map(relative))
    }
    fn rename(&self, from: &str, to: &str) -> Result<(), VaultError> {
        self.inner.rename(self.map(from), self.map(to))
    }
    fn create_dir(&self, relative: &str) -> Result<(), VaultError> {
        self.inner.create_dir(self.map(relative))
    }
    fn stat(&self, relative: &str) -> Result<crate::FileStat, VaultError> {
        if self
            .refuse_stat_containing
            .is_some_and(|needle| relative.contains(needle))
        {
            return Err(VaultError::Io(std::io::Error::other(
                "the volume did not answer",
            )));
        }
        self.inner.stat(self.map(relative))
    }
    fn canonical_path(&self, relative: &str) -> Result<Option<String>, VaultError> {
        match self.canonical.get(relative) {
            Some(Ok(answer)) => Ok(answer.clone()),
            Some(Err(())) => Err(VaultError::Io(std::io::Error::other(
                "the volume did not answer",
            ))),
            None => Ok(self
                .mutation_path_exists(relative)?
                .then(|| relative.to_string())),
        }
    }
    fn watch(
        &self,
        sink: Arc<dyn crate::FileEventSink>,
    ) -> Result<Option<crate::WatchHandle>, VaultError> {
        self.inner.watch(sink)
    }
}

fn spelling_session(
    root: &Path,
    physical: &[(&str, &str)],
    canonical: &[(&str, Result<Option<&str>, ()>)],
) -> VaultSession {
    spelling_session_refusing(root, physical, canonical, None)
}

fn spelling_session_refusing(
    root: &Path,
    physical: &[(&str, &str)],
    canonical: &[(&str, Result<Option<&str>, ()>)],
    refuse_stat_containing: Option<&'static str>,
) -> VaultSession {
    let provider = SpellingProvider {
        refuse_stat_containing,
        inner: FsVaultProvider::new(root.to_path_buf()),
        physical: physical
            .iter()
            .map(|(from, to)| (from.to_string(), to.to_string()))
            .collect(),
        canonical: canonical
            .iter()
            .map(|(path, answer)| {
                (
                    path.to_string(),
                    answer.map(|spelling| spelling.map(str::to_string)),
                )
            })
            .collect(),
    };
    VaultSession::open(
        Arc::new(provider) as Arc<dyn VaultProvider>,
        SessionConfig::new(root.join(".slate")),
    )
    .unwrap()
}

#[test]
fn peers_are_the_entries_whose_paths_name_one_file() {
    // A case-insensitive volume: `A.md` and `a.md` name one stored file.
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path();
    std::fs::write(root.join("a.md"), "# A\n").unwrap();
    let session = spelling_session(
        root,
        &[("A.md", "a.md")],
        &[("A.md", Ok(Some("a.md"))), ("a.md", Ok(Some("a.md")))],
    );
    let baseline = Some(hash_of(root, "a.md"));
    let snap = snapshot(
        vec![
            entry("e1", "A.md", "one", baseline.clone(), 1),
            entry("e2", "a.md", "two", baseline, 2),
        ],
        vec![],
    );

    let plan = session.plan_hot_exit_restore(&snap, &ids(&["e1", "e2"]));

    assert_eq!(planned(&plan.layout, "e2").text, "two");
    assert_eq!(
        planned(&plan.new_tabs, "e1").path,
        "A (recovered).md",
        "one peer group: the older text is recovered"
    );
    assert_eq!(planned(&plan.displaced, "e1").text, "two");
}

#[test]
fn distinct_files_differing_only_in_case_are_never_peers() {
    // A case-sensitive volume: `A.md` and `a.md` are two stored files.
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path();
    std::fs::write(root.join("upper.md"), "# Upper\n").unwrap();
    std::fs::write(root.join("lower.md"), "# Lower\n").unwrap();
    let session = spelling_session(root, &[("A.md", "upper.md"), ("a.md", "lower.md")], &[]);
    let snap = snapshot(
        vec![
            entry("e1", "A.md", "one", Some(hash_of(root, "upper.md")), 1),
            entry("e2", "a.md", "two", Some(hash_of(root, "lower.md")), 2),
        ],
        vec![],
    );

    let plan = session.plan_hot_exit_restore(&snap, &ids(&["e1", "e2"]));

    let e1 = planned(&plan.layout, "e1");
    assert_eq!((e1.path.as_str(), e1.text.as_str()), ("A.md", "one"));
    assert_eq!(
        e1.decision,
        HotExitRestore::Restore {
            path: "A.md".into()
        }
    );
    let e2 = planned(&plan.layout, "e2");
    assert_eq!((e2.path.as_str(), e2.text.as_str()), ("a.md", "two"));
    assert!(
        plan.new_tabs.is_empty(),
        "no recovered note: {:?}",
        plan.new_tabs
    );
    assert!(plan.displaced.is_empty() && plan.redundant.is_empty());
}

#[test]
fn an_unanswered_canonical_path_never_merges() {
    // `a.md`'s spelling cannot be read; `A.md` answers `a.md`. A fallback
    // to the requested path as the key would make them peers.
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path();
    std::fs::write(root.join("a.md"), "# A\n").unwrap();
    let session = spelling_session(
        root,
        &[("A.md", "a.md")],
        &[("a.md", Err(())), ("A.md", Ok(Some("a.md")))],
    );
    let baseline = Some(hash_of(root, "a.md"));
    let snap = snapshot(
        vec![
            entry("e1", "a.md", "one", baseline.clone(), 1),
            entry("e2", "A.md", "two", baseline, 2),
        ],
        vec![],
    );

    let plan = session.plan_hot_exit_restore(&snap, &ids(&["e1", "e2"]));

    assert_eq!(planned(&plan.layout, "e1").text, "one");
    assert_eq!(planned(&plan.layout, "e2").text, "two");
    assert!(plan.new_tabs.is_empty() && plan.displaced.is_empty());
}

/// Spec §7: a restored entry's whole text splits into the parts and
/// offsets `read_note_parts` gives for the same bytes, so a host's
/// restored buffer and its loaded buffer never disagree.
#[test]
fn split_note_offsets_are_read_note_parts_offsets() {
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path();
    let notes = [
        "Body only\n",
        "---\ntitle: Ghost\n---\nBody\n",
        "---\r\na: 1\r\n---\r\nline one\r\nline two",
        "\u{FEFF}---  \ntags: [x]\n---\n\n# Heading\n",
        "---\nunclosed: yes\nbody",
        "",
    ];
    for (index, text) in notes.iter().enumerate() {
        std::fs::write(root.join(format!("n{index}.md")), text).unwrap();
    }
    let session = session_at(root);
    for (index, text) in notes.iter().enumerate() {
        let read = session.read_note_parts(&format!("n{index}.md")).unwrap();
        let split = crate::split_note_with_offsets(text);
        assert_eq!(
            (
                split.fm_source.as_str(),
                split.body.as_str(),
                split.body_byte_offset,
                split.body_line_offset
            ),
            (
                read.fm_source.as_str(),
                read.body.as_str(),
                read.body_byte_offset,
                read.body_line_offset
            ),
            "{text:?}"
        );
    }
}

/// Peers whose texts are all the latest are one buffer: every tab keeps
/// its own place with that text; none is redundant or recovered.
#[test]
fn peers_sharing_the_latest_text_each_keep_their_tab() {
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path();
    std::fs::write(root.join("a.md"), "# A\n").unwrap();
    let session = session_at(root);
    let baseline = Some(hash_of(root, "a.md"));
    let snap = snapshot(
        vec![
            entry("p1", "a.md", "same", baseline.clone(), 1),
            entry("p2", "a.md", "same", baseline, 2),
        ],
        vec![],
    );

    let plan = session.plan_hot_exit_restore(&snap, &ids(&["p1"]));

    assert_eq!(planned(&plan.layout, "p1").text, "same");
    assert_eq!(planned(&plan.new_tabs, "p2").text, "same");
    assert_eq!(plan.new_tabs.len(), 1);
    assert!(plan.displaced.is_empty() && plan.redundant.is_empty());
}

/// Entries of one instant keep the snapshot's order among the new tabs,
/// whichever pass (a peer group or the rest) planned them.
#[test]
fn new_tabs_of_one_instant_keep_the_snapshots_order() {
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path();
    std::fs::write(root.join("a.md"), "# A\n").unwrap();
    let session = session_at(root);
    let baseline = Some(hash_of(root, "a.md"));
    let snap = snapshot(
        vec![
            entry("g", "gone.md", "gone", Some("h".into()), 5),
            entry("p1", "a.md", "same", baseline.clone(), 5),
            entry("p2", "a.md", "same", baseline, 5),
            entry("n", "new.md", "new", None, 5),
        ],
        vec![],
    );

    let plan = session.plan_hot_exit_restore(&snap, &ids(&["p1"]));

    let order: Vec<&str> = plan.new_tabs.iter().map(|t| t.entry_id.as_str()).collect();
    assert_eq!(order, ["g", "p2", "n"]);
}

/// A vault that never answers for a recovered name gets one made unique by
/// the entry's id, reduced to the store's id alphabet and 64 characters.
#[test]
fn a_vault_that_never_answers_names_the_recovered_note_by_its_entry_id() {
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path();
    std::fs::write(root.join("a.md"), "# A\n").unwrap();
    let session = spelling_session_refusing(root, &[], &[], Some("(recovered"));
    let baseline = Some(hash_of(root, "a.md"));
    let odd_id = format!("p1-\u{2713}/..\u{e9}_{}", "x".repeat(80));
    let snap = snapshot(
        vec![
            entry(&odd_id, "a.md", "older", baseline.clone(), 1),
            entry("p2", "a.md", "newer", baseline, 2),
        ],
        vec![],
    );

    let plan = session.plan_hot_exit_restore(&snap, &ids(&[]));

    let id: String = format!("p1-_{}", "x".repeat(80)).chars().take(64).collect();
    assert_eq!(
        planned(&plan.new_tabs, &odd_id).path,
        format!("a (recovered {id}).md")
    );
}
