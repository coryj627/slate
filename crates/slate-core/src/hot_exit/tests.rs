// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Plan 44 WB-29a: the hot-exit store's facts (the spec's Tests table).
//! Every fact asserts on the files on disk as well as through the API.

use super::*;
use std::cell::RefCell;
use std::collections::HashMap;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::{Condvar, Mutex as StdMutex};

thread_local! {
    /// A stop the store's own `inject` hook honours (a step with no I/O
    /// between it and the one before).
    pub(super) static INJECTED_STOP: RefCell<Option<HotExitStep>> = const { RefCell::new(None) };
}

/// The real I/O, recording every step and able to fail or hold one.
#[derive(Default)]
struct Recording {
    log: StdMutex<Vec<(HotExitStep, String)>>,
    /// Fails the n-th occurrence (0-based) of a step.
    fail: StdMutex<Option<(HotExitStep, usize)>>,
    counts: StdMutex<HashMap<HotExitStepKey, usize>>,
    /// Holds the first `SyncFile` of a staged entry until released.
    hold_staged_sync: AtomicBool,
    held: StdMutex<bool>,
    release: Condvar,
    holding: StdMutex<bool>,
    holding_cv: Condvar,
}

type HotExitStepKey = u8;

fn step_key(step: HotExitStep) -> HotExitStepKey {
    step as u8
}

impl Recording {
    fn step(&self, step: HotExitStep, path: &Path) -> io::Result<()> {
        let name = path
            .file_name()
            .and_then(|n| n.to_str())
            .unwrap_or_default()
            .to_string();
        self.log.lock().unwrap().push((step, name));
        let mut counts = self.counts.lock().unwrap();
        let n = counts.entry(step_key(step)).or_insert(0);
        let index = *n;
        *n += 1;
        if let Some((fail_step, at)) = *self.fail.lock().unwrap()
            && fail_step == step
            && at == index
        {
            return Err(io::Error::other("injected"));
        }
        Ok(())
    }

    fn fail_nth(&self, step: HotExitStep, n: usize) {
        *self.counts.lock().unwrap() = HashMap::new();
        *self.fail.lock().unwrap() = Some((step, n));
    }

    fn heal(&self) {
        *self.fail.lock().unwrap() = None;
    }

    fn steps(&self) -> Vec<(HotExitStep, String)> {
        self.log.lock().unwrap().clone()
    }

    fn clear(&self) {
        self.log.lock().unwrap().clear();
    }

    /// Fails rather than hangs when the write never reaches its staged sync.
    fn wait_until_holding(&self) {
        let holding = self.holding.lock().unwrap();
        let (_holding, wait) = self
            .holding_cv
            .wait_timeout_while(holding, Duration::from_secs(30), |holding| !*holding)
            .unwrap();
        assert!(!wait.timed_out(), "the write never reached its staged sync");
    }

    fn release_hold(&self) {
        *self.held.lock().unwrap() = true;
        self.release.notify_all();
    }
}

impl HotExitFs for Recording {
    fn write_new(&self, path: &Path, bytes: &[u8]) -> io::Result<()> {
        self.step(HotExitStep::WriteFile, path)?;
        RealHotExitFs.write_new(path, bytes)
    }

    fn sync_file(&self, path: &Path) -> io::Result<()> {
        let staged_entry = path
            .file_name()
            .and_then(|n| n.to_str())
            .is_some_and(|n| n.contains(".json.tmp-"));
        if staged_entry && self.hold_staged_sync.swap(false, Ordering::SeqCst) {
            *self.holding.lock().unwrap() = true;
            self.holding_cv.notify_all();
            let mut held = self.held.lock().unwrap();
            while !*held {
                held = self.release.wait(held).unwrap();
            }
        }
        self.step(HotExitStep::SyncFile, path)?;
        RealHotExitFs.sync_file(path)
    }

    fn rename(&self, from: &Path, to: &Path) -> io::Result<()> {
        self.step(HotExitStep::Rename, to)?;
        RealHotExitFs.rename(from, to)
    }

    fn sync_dir(&self, dir: &Path) -> io::Result<()> {
        self.step(HotExitStep::SyncDir, dir)?;
        RealHotExitFs.sync_dir(dir)
    }

    fn remove(&self, path: &Path) -> io::Result<()> {
        self.step(HotExitStep::Remove, path)?;
        RealHotExitFs.remove(path)
    }

    fn create_dir(&self, path: &Path) -> io::Result<()> {
        self.step(HotExitStep::CreateDir, path)?;
        RealHotExitFs.create_dir(path)
    }

    fn read(&self, path: &Path) -> io::Result<Vec<u8>> {
        RealHotExitFs.read(path)
    }

    fn list(&self, dir: &Path) -> io::Result<Vec<String>> {
        RealHotExitFs.list(dir)
    }

    fn modified(&self, path: &Path) -> io::Result<SystemTime> {
        RealHotExitFs.modified(path)
    }

    fn size(&self, path: &Path) -> io::Result<u64> {
        RealHotExitFs.size(path)
    }
}

struct Fixture {
    dir: tempfile::TempDir,
    fs: Arc<Recording>,
    clock: Arc<StdMutex<SystemTime>>,
    store: HotExitStore,
}

impl Fixture {
    fn new() -> Self {
        let dir = tempfile::tempdir().unwrap();
        let fs = Arc::new(Recording::default());
        let clock = Arc::new(StdMutex::new(SystemTime::now()));
        let store = Self::store_on(dir.path(), &fs, &clock);
        Self {
            dir,
            fs,
            clock,
            store,
        }
    }

    fn store_on(
        root: &Path,
        fs: &Arc<Recording>,
        clock: &Arc<StdMutex<SystemTime>>,
    ) -> HotExitStore {
        let clock = Arc::clone(clock);
        HotExitStore::open_with(
            root,
            Arc::clone(fs) as Arc<dyn HotExitFs>,
            Arc::new(move || *clock.lock().unwrap()),
        )
        .unwrap()
    }

    /// A second store on the same root, as a relaunch or another process.
    fn reopen(&self) -> HotExitStore {
        Self::store_on(self.dir.path(), &self.fs, &self.clock)
    }

    fn advance(&self, by: Duration) {
        let mut now = self.clock.lock().unwrap();
        *now += by;
    }

    fn vault_dir(&self, key: &HotExitVaultKey) -> PathBuf {
        self.store.vault_dir(key)
    }

    fn entry_file(&self, key: &HotExitVaultKey, id: &str) -> PathBuf {
        self.vault_dir(key)
            .join(ENTRIES_DIR)
            .join(format!("{id}{ENTRY_EXT}"))
    }

    fn on_disk(&self, key: &HotExitVaultKey, id: &str) -> Option<HotExitEntry> {
        std::fs::read(self.entry_file(key, id))
            .ok()
            .and_then(|bytes| serde_json::from_slice(&bytes).ok())
    }

    fn files_in(&self, key: &HotExitVaultKey, sub: &str) -> Vec<String> {
        RealHotExitFs
            .list(&self.vault_dir(key).join(sub))
            .unwrap_or_default()
    }
}

fn key(text: &str) -> HotExitVaultKey {
    HotExitVaultKey { text: text.into() }
}

fn entry(text: &str) -> HotExitEntryWrite {
    HotExitEntryWrite {
        path: "notes/a.md".into(),
        text: text.into(),
        baseline_hash: Some("h0".into()),
        baseline_file_id: Some("f0".into()),
    }
}

fn claim(store: &HotExitStore, key: &HotExitVaultKey, id: &str) -> HotExitClaim {
    let epoch = store.current_epoch(key).unwrap();
    match store.claim_entry(key, epoch, id, None).unwrap() {
        HotExitClaimOutcome::Claimed(claim) => claim,
        other => panic!("claim refused: {other:?}"),
    }
}

fn durable(outcome: Result<HotExitWriteOutcome, HotExitError>) -> u64 {
    match outcome {
        Ok(HotExitWriteOutcome::Durable { generation }) => generation,
        other => panic!("not durable: {other:?}"),
    }
}

#[test]
fn a_durable_write_is_read_back_after_the_store_reopens() {
    let f = Fixture::new();
    let k = key("vault-a");
    let c = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &c, 1, &entry("hello")));
    let snapshot = f.reopen().read_buffers(&k).unwrap();
    assert_eq!(snapshot.entries.len(), 1);
    assert_eq!(snapshot.entries[0].text, "hello");
    assert_eq!(snapshot.entries[0].baseline_file_id.as_deref(), Some("f0"));
    assert_eq!(f.on_disk(&k, "tab1").unwrap().text, "hello");
}

#[test]
fn durable_means_synced_then_renamed_then_synced() {
    let f = Fixture::new();
    let k = key("vault-a");
    let c = claim(&f.store, &k, "tab1");
    f.fs.clear();
    durable(f.store.write_buffer(&k, &c, 1, &entry("x")));
    let steps: Vec<HotExitStep> = f.fs.steps().into_iter().map(|(step, _)| step).collect();
    assert_eq!(
        steps,
        [
            HotExitStep::WriteFile,
            HotExitStep::SyncFile,
            HotExitStep::Rename,
            HotExitStep::SyncDir
        ]
    );
    // A claim and the epoch use the same sequence.
    f.fs.clear();
    let _ = claim(&f.store, &k, "tab2");
    let steps: Vec<HotExitStep> = f.fs.steps().into_iter().map(|(step, _)| step).collect();
    assert_eq!(
        steps,
        [
            HotExitStep::WriteFile,
            HotExitStep::SyncFile,
            HotExitStep::Rename,
            HotExitStep::SyncDir
        ]
    );
    f.fs.clear();
    f.store.discard_vault(&k).unwrap();
    let steps: Vec<HotExitStep> = f.fs.steps().into_iter().map(|(step, _)| step).collect();
    assert_eq!(
        &steps[..4],
        [
            HotExitStep::WriteFile,
            HotExitStep::SyncFile,
            HotExitStep::Rename,
            HotExitStep::SyncDir
        ],
        "the epoch is durable before any delete"
    );
    assert!(steps[4..].contains(&HotExitStep::Remove));
}

#[test]
fn a_failed_sync_is_never_reported_durable() {
    for (step, n) in [
        (HotExitStep::SyncFile, 0),
        (HotExitStep::Rename, 0),
        (HotExitStep::SyncDir, 0),
    ] {
        let f = Fixture::new();
        let k = key("vault-a");
        let c = claim(&f.store, &k, "tab1");
        durable(f.store.write_buffer(&k, &c, 1, &entry("old")));
        f.fs.fail_nth(step, n);
        let outcome = f.store.write_buffer(&k, &c, 2, &entry("new"));
        assert!(
            outcome.is_err(),
            "{step:?} failed but the write said {outcome:?}"
        );
        f.fs.heal();
        if step != HotExitStep::SyncDir {
            assert_eq!(f.on_disk(&k, "tab1").unwrap().text, "old", "{step:?}");
        }
        assert!(
            f.files_in(&k, ENTRIES_DIR)
                .iter()
                .all(|n| !n.contains(TEMP_MARK)),
            "{step:?} left its temp file"
        );
    }
}

#[test]
fn two_tabs_on_one_note_are_two_entries_and_one_release_keeps_the_other() {
    let f = Fixture::new();
    let k = key("vault-a");
    let a = claim(&f.store, &k, "tabA");
    let b = claim(&f.store, &k, "tabB");
    durable(f.store.write_buffer(&k, &a, 1, &entry("from A")));
    durable(f.store.write_buffer(&k, &b, 1, &entry("from B")));
    f.store.release_entry(&k, &a).unwrap();
    let snapshot = f.store.read_buffers(&k).unwrap();
    assert_eq!(snapshot.entries.len(), 1);
    assert_eq!(snapshot.entries[0].text, "from B");
    assert!(f.on_disk(&k, "tabA").is_none());
}

#[test]
fn an_older_generation_never_replaces_a_newer_one() {
    let f = Fixture::new();
    let k = key("vault-a");
    let c = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &c, 5, &entry("five")));
    for older in [5, 4] {
        assert_eq!(
            f.store
                .write_buffer(&k, &c, older, &entry("stale"))
                .unwrap(),
            HotExitWriteOutcome::Superseded(HotExitSupersession::Newer {
                stored_generation: 5
            })
        );
    }
    assert_eq!(f.on_disk(&k, "tab1").unwrap().text, "five");
}

#[test]
fn newer_is_never_vouched_for_without_durability() {
    let f = Fixture::new();
    let k = key("vault-a");
    let c = claim(&f.store, &k, "tab1");
    // N+1 renames, then its directory sync fails.
    f.fs.fail_nth(HotExitStep::SyncDir, 0);
    assert!(f.store.write_buffer(&k, &c, 2, &entry("two")).is_err());
    // N then arrives: the store re-syncs the stored entry before Newer; with
    // the directory sync still failing, N is an error, never Newer.
    f.fs.fail_nth(HotExitStep::SyncDir, 0);
    assert!(f.store.write_buffer(&k, &c, 1, &entry("one")).is_err());
    // Healed, the re-sync succeeds and Newer is returned.
    f.fs.heal();
    f.fs.clear();
    assert_eq!(
        f.store.write_buffer(&k, &c, 1, &entry("one")).unwrap(),
        HotExitWriteOutcome::Superseded(HotExitSupersession::Newer {
            stored_generation: 2
        })
    );
    let steps: Vec<HotExitStep> = f.fs.steps().into_iter().map(|(step, _)| step).collect();
    assert!(
        steps
            .windows(2)
            .any(|w| w == [HotExitStep::SyncFile, HotExitStep::SyncDir]),
        "Newer re-synced the stored entry: {steps:?}"
    );
}

#[test]
fn a_superseded_write_says_why() {
    let f = Fixture::new();
    let k = key("vault-a");
    let a = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &a, 3, &entry("a3")));
    // Newer.
    assert!(matches!(
        f.store.write_buffer(&k, &a, 2, &entry("a2")).unwrap(),
        HotExitWriteOutcome::Superseded(HotExitSupersession::Newer { .. })
    ));
    // Claim.
    let b = claim(&f.store, &k, "tab1");
    assert_eq!(
        f.store.write_buffer(&k, &a, 9, &entry("a9")).unwrap(),
        HotExitWriteOutcome::Superseded(HotExitSupersession::Claim)
    );
    // Epoch.
    let next = f.store.discard_vault(&k).unwrap();
    assert_eq!(
        f.store.write_buffer(&k, &b, 1, &entry("b1")).unwrap(),
        HotExitWriteOutcome::Superseded(HotExitSupersession::Epoch { current: next })
    );
}

#[test]
fn a_newer_claim_invalidates_every_older_writer() {
    let f = Fixture::new();
    let k = key("vault-a");
    let a = claim(&f.store, &k, "tab1");
    let b = claim(&f.store, &k, "tab1");
    assert_eq!(
        f.store.write_buffer(&k, &a, 1, &entry("A")).unwrap(),
        HotExitWriteOutcome::Superseded(HotExitSupersession::Claim)
    );
    durable(f.store.write_buffer(&k, &b, 1, &entry("B")));
    assert_eq!(
        f.store.write_buffer(&k, &a, 2, &entry("A2")).unwrap(),
        HotExitWriteOutcome::Superseded(HotExitSupersession::Claim)
    );
    assert_eq!(f.on_disk(&k, "tab1").unwrap().text, "B");
}

#[test]
fn a_release_invalidates_its_own_late_writes() {
    let f = Fixture::new();
    let k = key("vault-a");
    let c = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &c, 1, &entry("one")));
    f.store.release_entry(&k, &c).unwrap();
    assert_eq!(
        f.store.write_buffer(&k, &c, 2, &entry("late")).unwrap(),
        HotExitWriteOutcome::Superseded(HotExitSupersession::Claim)
    );
    assert!(f.on_disk(&k, "tab1").is_none());
    assert!(f.files_in(&k, CLAIMS_DIR).is_empty());
}

#[test]
fn a_release_retires_its_payload_after_the_next_claim() {
    let f = Fixture::new();
    let k = key("vault-a");
    let c = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &c, 1, &entry("discarded")));
    // The tab's next dirty lifetime claims before the release lands.
    let _c2 = claim(&f.store, &k, "tab1");
    f.store.release_entry(&k, &c).unwrap();
    assert!(
        f.on_disk(&k, "tab1").is_none(),
        "the discarded payload stayed"
    );
    // Killed before C2 publishes: nothing restores.
    assert!(f.reopen().read_buffers(&k).unwrap().entries.is_empty());
}

#[test]
fn a_stale_release_never_deletes_a_newer_entry() {
    let f = Fixture::new();
    let k = key("vault-a");
    let a = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &a, 1, &entry("A")));
    let b = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &b, 1, &entry("B")));
    f.store.release_entry(&k, &a).unwrap();
    assert_eq!(f.on_disk(&k, "tab1").unwrap().text, "B");
    // Past a vault discard and a new claim too.
    f.store.discard_vault(&k).unwrap();
    let c = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &c, 1, &entry("C")));
    f.store.release_entry(&k, &b).unwrap();
    assert_eq!(f.on_disk(&k, "tab1").unwrap().text, "C");
}

#[test]
fn a_release_retires_the_text_its_claim_adopted() {
    for release_after_next_claim in [false, true] {
        let f = Fixture::new();
        let k = key("vault-a");
        let a = claim(&f.store, &k, "tab1");
        durable(f.store.write_buffer(&k, &a, 1, &entry("restored")));
        // Restore: the tab claims B adopting A's incarnation.
        let epoch = f.store.current_epoch(&k).unwrap();
        let HotExitClaimOutcome::Claimed(b) = f
            .store
            .claim_entry(&k, epoch, "tab1", Some(&a.incarnation))
            .unwrap()
        else {
            panic!("claim refused");
        };
        if release_after_next_claim {
            let _c = claim(&f.store, &k, "tab1");
            f.store.release_entry(&k, &b).unwrap();
        } else {
            f.store.release_entry(&k, &b).unwrap();
            let _c = claim(&f.store, &k, "tab1");
        }
        // Killed before C publishes: nothing restores.
        assert!(
            f.reopen().read_buffers(&k).unwrap().entries.is_empty(),
            "released-after-next-claim = {release_after_next_claim}"
        );
        assert!(f.on_disk(&k, "tab1").is_none());
    }
}

#[test]
fn claiming_without_releasing_keeps_the_adopted_text() {
    let f = Fixture::new();
    let k = key("vault-a");
    let a = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &a, 1, &entry("restored")));
    let epoch = f.store.current_epoch(&k).unwrap();
    let _b = f
        .store
        .claim_entry(&k, epoch, "tab1", Some(&a.incarnation))
        .unwrap();
    let snapshot = f.reopen().read_buffers(&k).unwrap();
    assert_eq!(snapshot.entries.len(), 1);
    assert_eq!(snapshot.entries[0].text, "restored");
}

#[test]
fn a_stale_adopters_release_keeps_a_successors_payload() {
    let f = Fixture::new();
    let k = key("vault-a");
    let a = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &a, 1, &entry("A")));
    let epoch = f.store.current_epoch(&k).unwrap();
    let HotExitClaimOutcome::Claimed(b) = f
        .store
        .claim_entry(&k, epoch, "tab1", Some(&a.incarnation))
        .unwrap()
    else {
        panic!("claim refused");
    };
    let c = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &c, 1, &entry("C")));
    f.store.release_entry(&k, &b).unwrap();
    assert_eq!(f.on_disk(&k, "tab1").unwrap().text, "C");
}

#[test]
fn a_vault_discard_supersedes_every_earlier_claim_write_and_release() {
    let f = Fixture::new();
    let k = key("vault-a");
    let c = claim(&f.store, &k, "tab1");
    let old_epoch = c.epoch;
    durable(f.store.write_buffer(&k, &c, 1, &entry("kept")));
    let next = f.store.discard_vault(&k).unwrap();
    assert!(next > old_epoch);
    // A claim issued with the old epoch.
    assert_eq!(
        f.store.claim_entry(&k, old_epoch, "tab2", None).unwrap(),
        HotExitClaimOutcome::Superseded(HotExitSupersession::Epoch { current: next })
    );
    // A write issued before, landing after.
    assert_eq!(
        f.store.write_buffer(&k, &c, 2, &entry("late")).unwrap(),
        HotExitWriteOutcome::Superseded(HotExitSupersession::Epoch { current: next })
    );
    f.store.release_entry(&k, &c).unwrap();
    assert!(f.files_in(&k, ENTRIES_DIR).is_empty());
    assert!(f.files_in(&k, CLAIMS_DIR).is_empty());
    assert!(f.store.read_buffers(&k).unwrap().entries.is_empty());
}

#[test]
fn a_discard_cut_short_after_its_epoch_hides_every_older_entry() {
    let f = Fixture::new();
    let k = key("vault-a");
    let c = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &c, 1, &entry("discarded")));
    INJECTED_STOP.with(|cell| *cell.borrow_mut() = Some(HotExitStep::Remove));
    let cut_short = f.store.discard_vault(&k);
    INJECTED_STOP.with(|cell| *cell.borrow_mut() = None);
    assert!(cut_short.is_err());
    assert!(
        f.on_disk(&k, "tab1").is_some(),
        "premise: the delete never ran"
    );
    assert!(f.reopen().read_buffers(&k).unwrap().entries.is_empty());
    assert!(
        f.on_disk(&k, "tab1").is_none(),
        "the read deleted the older entry"
    );
}

#[test]
fn a_stalled_payload_write_never_blocks_a_discard() {
    let f = Arc::new(Fixture::new());
    let k = key("vault-a");
    let c = claim(&f.store, &k, "tab1");
    f.fs.hold_staged_sync.store(true, Ordering::SeqCst);
    let writer = {
        let f = Arc::clone(&f);
        let k = k.clone();
        let c = c.clone();
        std::thread::spawn(move || f.store.write_buffer(&k, &c, 1, &entry("held")))
    };
    f.fs.wait_until_holding();
    let started = std::time::Instant::now();
    f.store.discard_vault(&k).unwrap();
    assert!(
        started.elapsed() < Duration::from_secs(5),
        "the discard waited"
    );
    f.fs.release_hold();
    assert!(matches!(
        writer.join().unwrap().unwrap(),
        HotExitWriteOutcome::Superseded(HotExitSupersession::Epoch { .. })
    ));
    assert!(f.on_disk(&k, "tab1").is_none());
}

#[test]
fn a_lost_epoch_fails_closed() {
    let f = Fixture::new();
    let k = key("vault-a");
    let c = claim(&f.store, &k, "tab1");
    f.store.discard_vault(&k).unwrap();
    std::fs::remove_file(f.vault_dir(&k).join(EPOCH_FILE)).unwrap();
    assert!(matches!(
        f.store.read_buffers(&k),
        Err(HotExitError::Damaged)
    ));
    assert!(matches!(
        f.store.claim_entry(&k, 0, "tab2", None),
        Err(HotExitError::Damaged)
    ));
    assert!(matches!(
        f.store.write_buffer(&k, &c, 1, &entry("older")),
        Err(HotExitError::Damaged)
    ));
    assert!(f.files_in(&k, ENTRIES_DIR).is_empty());
    // Close Vault's Discard All re-anchors it above anything it can have
    // held.
    let anchored = f.store.discard_vault(&k).unwrap();
    assert!(anchored > 1);
    assert!(f.store.read_buffers(&k).unwrap().entries.is_empty());
}

#[test]
fn a_write_or_claim_over_a_cap_is_refused_and_changes_nothing() {
    let f = Fixture::new();
    let k = key("vault-a");
    let c = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &c, 1, &entry("small")));
    let big = "x".repeat(MAX_ENTRY_TEXT_BYTES + 1);
    assert_eq!(
        f.store.write_buffer(&k, &c, 2, &entry(&big)).unwrap(),
        HotExitWriteOutcome::Refused(HotExitRefusal::EntryTooLarge)
    );
    assert_eq!(f.on_disk(&k, "tab1").unwrap().text, "small");

    // 256 entries.
    for i in 1..MAX_ENTRIES_PER_VAULT {
        let id = format!("t{i}");
        let ci = claim(&f.store, &k, &id);
        durable(f.store.write_buffer(&k, &ci, 1, &entry("e")));
    }
    let over = claim(&f.store, &k, "over");
    assert_eq!(
        f.store.write_buffer(&k, &over, 1, &entry("e")).unwrap(),
        HotExitWriteOutcome::Refused(HotExitRefusal::VaultFull)
    );
    assert!(f.on_disk(&k, "over").is_none());
    // A replacement of an existing entry still fits.
    durable(f.store.write_buffer(&k, &c, 3, &entry("still fits")));

    // 64 MiB.
    let g = Fixture::new();
    let kb = key("vault-b");
    let chunk = "y".repeat(MAX_ENTRY_TEXT_BYTES - 1024);
    let per = (MAX_BYTES_PER_VAULT / (MAX_ENTRY_TEXT_BYTES as u64)) as usize;
    for i in 0..per - 1 {
        let ci = claim(&g.store, &kb, &format!("b{i}"));
        durable(g.store.write_buffer(&kb, &ci, 1, &entry(&chunk)));
    }
    let last = claim(&g.store, &kb, "last");
    let two_more = "z".repeat(MAX_ENTRY_TEXT_BYTES);
    let _ = g.store.write_buffer(&kb, &last, 1, &entry(&two_more));
    let ci = claim(&g.store, &kb, "past");
    assert_eq!(
        g.store
            .write_buffer(&kb, &ci, 1, &entry(&two_more))
            .unwrap(),
        HotExitWriteOutcome::Refused(HotExitRefusal::VaultFull)
    );
    assert!(g.on_disk(&kb, "past").is_none());
}

#[test]
fn the_vault_cap_holds_under_racing_first_claims() {
    let f = Arc::new(Fixture::new());
    for i in 0..MAX_VAULTS - 1 {
        let _ = claim(&f.store, &key(&format!("v{i}")), "t");
    }
    let threads: Vec<_> = (0..4)
        .map(|i| {
            let f = Arc::clone(&f);
            std::thread::spawn(move || {
                f.store
                    .claim_entry(&key(&format!("racer{i}")), 0, "t", None)
                    .unwrap()
            })
        })
        .collect();
    let outcomes: Vec<_> = threads.into_iter().map(|t| t.join().unwrap()).collect();
    let claimed = outcomes
        .iter()
        .filter(|o| matches!(o, HotExitClaimOutcome::Claimed(_)))
        .count();
    assert_eq!(claimed, 1, "{outcomes:?}");
    assert!(outcomes.contains(&HotExitClaimOutcome::Refused(HotExitRefusal::TooManyVaults)));
    let vaults = RealHotExitFs.list(&f.store.root.join(VAULTS_DIR)).unwrap();
    assert_eq!(
        vaults.iter().filter(|n| !n.contains(TEMP_MARK)).count(),
        MAX_VAULTS
    );
}

#[test]
fn nothing_unread_is_ever_deleted() {
    let f = Fixture::new();
    let k = key("vault-a");
    let c = claim(&f.store, &k, "tab1");
    let mut odd = entry("kept a year");
    odd.path = "../outside.md".into();
    durable(f.store.write_buffer(&k, &c, 1, &odd));
    f.advance(Duration::from_secs(365 * 24 * 60 * 60));
    let reopened = f.reopen();
    let snapshot = reopened.read_buffers(&k).unwrap();
    assert_eq!(snapshot.entries.len(), 1);
    assert_eq!(snapshot.entries[0].path, "../outside.md");
    assert!(f.on_disk(&k, "tab1").is_some());
}

#[test]
fn a_cut_short_write_keeps_the_previous_entry() {
    let f = Fixture::new();
    let k = key("vault-a");
    let c = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &c, 1, &entry("previous")));
    // A temp file a dead process left before its rename.
    let temp = f
        .vault_dir(&k)
        .join(ENTRIES_DIR)
        .join(format!("tab1{ENTRY_EXT}{TEMP_MARK}dead"));
    std::fs::write(&temp, b"half").unwrap();
    assert_eq!(
        f.store.read_buffers(&k).unwrap().entries[0].text,
        "previous"
    );
    assert!(temp.exists(), "a young temp may be a live write's");
    f.advance(ORPHAN_TEMP_AGE + Duration::from_secs(1));
    assert_eq!(
        f.store.read_buffers(&k).unwrap().entries[0].text,
        "previous"
    );
    assert!(!temp.exists(), "an hour-old temp is an orphan");
}

#[test]
fn two_vault_keys_never_read_each_others_entries() {
    let f = Fixture::new();
    let a = key("win:0000000000000001-00000000000000020000000000000003-00000000000000aa");
    let b = key("win:0000000000000001-00000000000000020000000000000003-00000000000000ab");
    let ca = claim(&f.store, &a, "tab1");
    durable(f.store.write_buffer(&a, &ca, 1, &entry("A's")));
    assert!(f.store.read_buffers(&b).unwrap().entries.is_empty());
    let cb = claim(&f.store, &b, "tab1");
    durable(f.store.write_buffer(&b, &cb, 1, &entry("B's")));
    assert_eq!(f.store.read_buffers(&a).unwrap().entries[0].text, "A's");
    assert_ne!(f.vault_dir(&a), f.vault_dir(&b));
}

#[test]
fn an_invalid_entry_id_is_refused() {
    let f = Fixture::new();
    let k = key("vault-a");
    for bad in [
        "",
        "a/b",
        "..",
        "a.b",
        "tab one",
        &"x".repeat(MAX_ENTRY_ID_LEN + 1),
    ] {
        assert!(
            matches!(
                f.store.claim_entry(&k, 0, bad, None),
                Err(HotExitError::InvalidEntryId)
            ),
            "{bad:?}"
        );
    }
    assert!(!f.vault_dir(&k).exists(), "a refused id wrote nothing");
    let _ = claim(&f.store, &k, &"x".repeat(MAX_ENTRY_ID_LEN));
}

/// The child half of `the_vault_lock_excludes_another_process`: run only
/// when that fact re-invokes this test binary.
#[test]
fn hot_exit_child_holds_the_vault_lock() {
    let Some(root) = std::env::var_os("SLATE_HOT_EXIT_CHILD_ROOT") else {
        return;
    };
    let root = PathBuf::from(root);
    let store = HotExitStore::open(&root).unwrap();
    let k = key("vault-shared");
    let _guard = store.lock_vault(&k).unwrap();
    std::fs::write(root.join("child-holding"), b"").unwrap();
    while !root.join("parent-done-waiting").exists() {
        std::thread::sleep(Duration::from_millis(20));
    }
    std::thread::sleep(Duration::from_millis(300));
    std::fs::write(root.join("child-releasing"), b"").unwrap();
}

#[test]
fn the_vault_lock_excludes_another_process() {
    let dir = tempfile::tempdir().unwrap();
    let store = HotExitStore::open(dir.path()).unwrap();
    let k = key("vault-shared");
    let _ = claim(&store, &k, "tab1");
    let mut child = std::process::Command::new(std::env::current_exe().unwrap())
        .args([
            "--exact",
            "hot_exit::tests::hot_exit_child_holds_the_vault_lock",
            "--nocapture",
        ])
        .env("SLATE_HOT_EXIT_CHILD_ROOT", dir.path())
        .spawn()
        .unwrap();
    let deadline = std::time::Instant::now() + Duration::from_secs(60);
    while !dir.path().join("child-holding").exists() {
        assert!(
            std::time::Instant::now() < deadline,
            "the child never took the lock"
        );
        std::thread::sleep(Duration::from_millis(20));
    }
    std::fs::write(dir.path().join("parent-done-waiting"), b"").unwrap();
    // The parent's claim must wait for the child's lock to go.
    let outcome = store.claim_entry(&k, 0, "tab2", None).unwrap();
    assert!(
        dir.path().join("child-releasing").exists(),
        "the parent's claim ran while the child held the vault lock"
    );
    assert!(matches!(outcome, HotExitClaimOutcome::Claimed(_)));
    assert!(child.wait().unwrap().success());
}

#[test]
fn two_processes_restoring_one_tab_never_both_count_as_kept() {
    // Two stores on one root stand for two processes; the claim files and
    // the file lock are what they share.
    let f = Fixture::new();
    let other = f.reopen();
    let k = key("vault-a");
    let mine = claim(&f.store, &k, "tab1");
    let theirs = claim(&other, &k, "tab1");
    durable(other.write_buffer(&k, &theirs, 1, &entry("theirs")));
    assert_eq!(
        f.store.write_buffer(&k, &mine, 1, &entry("mine")).unwrap(),
        HotExitWriteOutcome::Superseded(HotExitSupersession::Claim)
    );
    // And the reverse order.
    let mine = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &mine, 1, &entry("mine")));
    assert_eq!(
        other
            .write_buffer(&k, &theirs, 2, &entry("theirs 2"))
            .unwrap(),
        HotExitWriteOutcome::Superseded(HotExitSupersession::Claim)
    );
    assert_eq!(f.on_disk(&k, "tab1").unwrap().text, "mine");
}
