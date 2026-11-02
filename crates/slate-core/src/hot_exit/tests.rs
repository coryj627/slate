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
            .join(format!("{}{ENTRY_EXT}", stem(id)))
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

/// A claim at the vault's current epoch with a new incarnation.
fn try_claim(
    store: &HotExitStore,
    key: &HotExitVaultKey,
    id: &str,
    expected: Option<&str>,
) -> HotExitClaimOutcome {
    let epoch = store.current_epoch(key).unwrap();
    store
        .claim_entry(
            key,
            epoch,
            id,
            expected,
            &new_hot_exit_incarnation().unwrap(),
        )
        .unwrap()
}

fn claimed(outcome: HotExitClaimOutcome) -> HotExitClaim {
    match outcome {
        HotExitClaimOutcome::Claimed(claim) => claim,
        other => panic!("claim refused: {other:?}"),
    }
}

/// A first claim: the entry has no current claim.
fn claim(store: &HotExitStore, key: &HotExitVaultKey, id: &str) -> HotExitClaim {
    claimed(try_claim(store, key, id, None))
}

/// A claim taking over the current claim `expected`.
fn claim_over(
    store: &HotExitStore,
    key: &HotExitVaultKey,
    id: &str,
    expected: &str,
) -> HotExitClaim {
    claimed(try_claim(store, key, id, Some(expected)))
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
    // A claim and the epoch use the same sequence, after the claim has made
    // the vault's own name durable (its creator may not have, §2) and, with
    // no current claim, `entries/` durable (WB-29 §7: no orphaned payload).
    f.fs.clear();
    let _ = claim(&f.store, &k, "tab2");
    let recorded = f.fs.steps();
    let steps: Vec<HotExitStep> = recorded.iter().map(|(step, _)| *step).collect();
    assert_eq!(
        steps,
        [
            HotExitStep::SyncDir,
            HotExitStep::SyncDir,
            HotExitStep::WriteFile,
            HotExitStep::SyncFile,
            HotExitStep::Rename,
            HotExitStep::SyncDir
        ]
    );
    assert_eq!(recorded[0].1, VAULTS_DIR);
    assert_eq!(recorded[1].1, ENTRIES_DIR);
    f.fs.clear();
    f.store.discard_vault(&k).unwrap();
    let steps: Vec<HotExitStep> = f.fs.steps().into_iter().map(|(step, _)| step).collect();
    assert_eq!(
        &steps[..5],
        [
            HotExitStep::SyncDir,
            HotExitStep::WriteFile,
            HotExitStep::SyncFile,
            HotExitStep::Rename,
            HotExitStep::SyncDir
        ],
        "the epoch is durable before any delete"
    );
    assert!(steps[5..].contains(&HotExitStep::Remove));
}

/// Opening the store makes its whole directory chain durable, up to the
/// device root, so no acknowledged write lives under an unsynced name.
#[test]
fn opening_the_store_syncs_its_directory_chain() {
    let dir = tempfile::tempdir().unwrap();
    let fs = Arc::new(Recording::default());
    let _store = HotExitStore::open_with(
        dir.path(),
        Arc::clone(&fs) as Arc<dyn HotExitFs>,
        Arc::new(SystemTime::now),
    )
    .unwrap();
    // The log keeps each path's last component.
    let synced: Vec<String> = fs
        .steps()
        .into_iter()
        .filter(|(step, _)| *step == HotExitStep::SyncDir)
        .map(|(_, name)| name)
        .collect();
    let device = dir.path().file_name().unwrap().to_str().unwrap();
    for chain in [VAULTS_DIR, VERSION_DIR, STORE_DIR, device] {
        assert!(
            synced.iter().any(|name| name == chain),
            "{chain} not synced: {synced:?}"
        );
    }
    // Every open syncs the whole chain, whoever created it: a second
    // opener never takes "already there" as "already durable".
    let before = synced.len();
    let _again = HotExitStore::open_with(
        dir.path(),
        Arc::clone(&fs) as Arc<dyn HotExitFs>,
        Arc::new(SystemTime::now),
    )
    .unwrap();
    let again = fs
        .steps()
        .into_iter()
        .filter(|(step, _)| *step == HotExitStep::SyncDir)
        .count();
    assert_eq!(again - before, before, "a second open synced less");
}

/// The store creates nothing above its own directory: a device root that
/// is missing, or relative, is refused (its own durability is the host's),
/// and nothing is made.
#[test]
fn the_device_root_must_be_an_existing_absolute_folder() {
    let dir = tempfile::tempdir().unwrap();
    let missing = dir.path().join("new-parent").join("new-device");
    assert!(matches!(
        HotExitStore::open(&missing),
        Err(HotExitError::Io { .. })
    ));
    assert!(!dir.path().join("new-parent").exists());
    assert!(matches!(
        HotExitStore::open(Path::new("relative-device")),
        Err(HotExitError::Io { .. })
    ));
    assert!(!Path::new("relative-device").exists());
}

/// No epoch follows u64::MAX: a counter or claim at the maximum is damage,
/// refused before anything changes, never a panic across the FFI or a wrap
/// back below the claims it must pass.
#[test]
fn a_maximal_epoch_is_refused_before_any_change() {
    let f = Fixture::new();
    let k = key("vault-a");
    let c = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &c, 1, &entry("kept")));
    std::fs::write(
        f.vault_dir(&k).join(EPOCH_FILE),
        u64::MAX.to_string().as_bytes(),
    )
    .unwrap();
    assert!(matches!(
        f.store.discard_vault(&k),
        Err(HotExitError::Damaged)
    ));
    assert_eq!(f.on_disk(&k, "tab1").unwrap().text, "kept");

    std::fs::write(f.vault_dir(&k).join(EPOCH_FILE), b"0").unwrap();
    std::fs::write(
        f.vault_dir(&k).join(CLAIMS_DIR).join(stem("tab1")),
        format!(
            r#"{{"incarnation":"{}","epoch":{}}}"#,
            c.incarnation,
            u64::MAX
        ),
    )
    .unwrap();
    assert!(matches!(
        f.store.discard_vault(&k),
        Err(HotExitError::Damaged)
    ));
    assert_eq!(f.on_disk(&k, "tab1").unwrap().text, "kept");

    // A lost epoch with a maximal claim cannot be re-anchored either.
    std::fs::remove_file(f.vault_dir(&k).join(EPOCH_FILE)).unwrap();
    assert!(matches!(
        f.store.discard_vault(&k),
        Err(HotExitError::Damaged)
    ));
    assert_eq!(f.on_disk(&k, "tab1").unwrap().text, "kept");
}

/// A release reports done only once its removals are durable, including a
/// retry that finds them already made (its first try's sync failed).
#[test]
fn a_release_that_finds_nothing_left_still_syncs() {
    let f = Fixture::new();
    let k = key("vault-a");
    let c = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &c, 1, &entry("x")));
    f.store.release_entry(&k, &c).unwrap();
    f.fs.clear();
    f.store.release_entry(&k, &c).unwrap();
    let synced: Vec<String> =
        f.fs.steps()
            .into_iter()
            .filter(|(step, _)| *step == HotExitStep::SyncDir)
            .map(|(_, name)| name)
            .collect();
    assert_eq!(synced, [CLAIMS_DIR, ENTRIES_DIR]);
}

/// A claim naming a later epoch than the vault's proves the epoch damaged
/// (a counter gone backwards), not its entry stale: the read fails closed
/// and the text stays; Discard All then moves past every claimed epoch.
#[test]
fn a_claim_from_a_later_epoch_keeps_its_entry() {
    let f = Fixture::new();
    let k = key("vault-a");
    f.store.discard_vault(&k).unwrap();
    let c = claim(&f.store, &k, "tab1");
    assert!(c.epoch >= 1);
    durable(f.store.write_buffer(&k, &c, 1, &entry("unread")));
    std::fs::write(f.vault_dir(&k).join(EPOCH_FILE), b"0").unwrap();
    assert!(matches!(
        f.store.read_buffers(&k),
        Err(HotExitError::Damaged)
    ));
    assert_eq!(f.on_disk(&k, "tab1").unwrap().text, "unread");
    assert!(f.files_in(&k, CLAIMS_DIR).contains(&stem("tab1")));
    let next = f.store.discard_vault(&k).unwrap();
    assert!(
        next > c.epoch,
        "{next} must pass the claimed epoch {}",
        c.epoch
    );
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
    // The stored entry itself, not only the staged write's temp, is synced
    // and then its directory, before Newer is vouched for.
    let stored = format!("{}{ENTRY_EXT}", stem("tab1"));
    let steps = f.fs.steps();
    assert!(
        steps
            .windows(2)
            .any(|w| w[0] == (HotExitStep::SyncFile, stored.clone())
                && w[1].0 == HotExitStep::SyncDir),
        "Newer re-synced the stored entry: {steps:?}"
    );
}

/// NTFS and APFS compare names without case, yet `TabA` and `taba` are two
/// valid, different entries: one must never replace the other's text.
#[test]
fn entry_ids_differing_only_by_case_are_two_entries() {
    let f = Fixture::new();
    let k = key("vault-a");
    let upper = claim(&f.store, &k, "TabA");
    durable(f.store.write_buffer(&k, &upper, 1, &entry("upper")));
    let lower = claim(&f.store, &k, "taba");
    durable(f.store.write_buffer(&k, &lower, 1, &entry("lower")));
    let mut texts: Vec<String> = f
        .store
        .read_buffers(&k)
        .unwrap()
        .entries
        .into_iter()
        .map(|entry| entry.text)
        .collect();
    texts.sort();
    assert_eq!(texts, ["lower", "upper"]);
    f.store.release_entry(&k, &lower).unwrap();
    assert_eq!(f.store.read_buffers(&k).unwrap().entries[0].text, "upper");
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
    let b = claim_over(&f.store, &k, "tab1", &a.incarnation);
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
    let b = claim_over(&f.store, &k, "tab1", &a.incarnation);
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
    let _c2 = claim_over(&f.store, &k, "tab1", &c.incarnation);
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
    let b = claim_over(&f.store, &k, "tab1", &a.incarnation);
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
        let b = claim_over(&f.store, &k, "tab1", &a.incarnation);
        assert_eq!(b.adopted, std::slice::from_ref(&a.incarnation));
        if release_after_next_claim {
            let _c = claim_over(&f.store, &k, "tab1", &b.incarnation);
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
    let _b = claim_over(&f.store, &k, "tab1", &a.incarnation);
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
    let b = claim_over(&f.store, &k, "tab1", &a.incarnation);
    let c = claim_over(&f.store, &k, "tab1", &b.incarnation);
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
        f.store
            .claim_entry(
                &k,
                old_epoch,
                "tab2",
                None,
                &new_hot_exit_incarnation().unwrap()
            )
            .unwrap(),
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
    // The discard runs apart and is awaited with a limit: if staging ever
    // moved under the vault lock it would wait on the held writer, and the
    // fact must fail (releasing the writer) rather than hang.
    let (done, discarded) = std::sync::mpsc::channel();
    {
        let f = Arc::clone(&f);
        let k = k.clone();
        std::thread::spawn(move || {
            let _ = done.send(f.store.discard_vault(&k));
        });
    }
    let outcome = discarded.recv_timeout(Duration::from_secs(5));
    f.fs.release_hold();
    outcome
        .expect("the discard waited on a stalled payload write")
        .unwrap();
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
        f.store
            .claim_entry(&k, 0, "tab2", None, &new_hot_exit_incarnation().unwrap()),
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
                    .claim_entry(
                        &key(&format!("racer{i}")),
                        0,
                        "t",
                        None,
                        &new_hot_exit_incarnation().unwrap(),
                    )
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
                f.store
                    .claim_entry(&k, 0, bad, None, &new_hot_exit_incarnation().unwrap()),
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
    // Held until the parent has seen its claim blocked, however slow the
    // machine: no timer decides when the lock goes.
    let deadline = std::time::Instant::now() + Duration::from_secs(120);
    while !root.join("parent-saw-blocked").exists() {
        assert!(
            std::time::Instant::now() < deadline,
            "the parent never answered"
        );
        std::thread::sleep(Duration::from_millis(20));
    }
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
    // The parent's claim must wait for the child's lock to go.
    let parent = std::thread::spawn({
        let root = dir.path().to_path_buf();
        let k = k.clone();
        move || {
            HotExitStore::open(&root).unwrap().claim_entry(
                &k,
                0,
                "tab2",
                None,
                &new_hot_exit_incarnation().unwrap(),
            )
        }
    });
    std::thread::sleep(Duration::from_millis(500));
    assert!(
        !parent.is_finished(),
        "the parent's claim ran while the child held the vault lock"
    );
    std::fs::write(dir.path().join("parent-saw-blocked"), b"").unwrap();
    let outcome = parent.join().unwrap().unwrap();
    assert!(dir.path().join("child-releasing").exists());
    assert!(matches!(outcome, HotExitClaimOutcome::Claimed(_)));
    assert!(child.wait().unwrap().success());
    drop(store);
}

/// The store-wide lock is a file lock too: a vault's first claim waits
/// while another handle (another process) holds `store.lock`, so two
/// processes never both pass the vault cap (§1, §8).
#[test]
fn a_first_claim_waits_for_the_store_lock_held_elsewhere() {
    let f = Fixture::new();
    let held = f
        .store
        .open_lock(&f.store.root.join(STORE_LOCK_FILE))
        .unwrap();
    held.lock().unwrap();
    let store = f.reopen();
    let first = std::thread::spawn(move || {
        let k = key("vault-new");
        store.claim_entry(&k, 0, "tab1", None, &new_hot_exit_incarnation().unwrap())
    });
    std::thread::sleep(Duration::from_millis(500));
    assert!(
        !first.is_finished(),
        "a first claim created its vault while the store lock was held"
    );
    held.unlock().unwrap();
    assert!(matches!(
        first.join().unwrap().unwrap(),
        HotExitClaimOutcome::Claimed(_)
    ));
}

/// Discard All on a vault whose first claim may still be creating it is
/// still a fence: a claim issued at epoch 0 before the discard meets the
/// new epoch (§6).
#[test]
fn a_discard_before_the_vault_exists_still_fences_the_first_claim() {
    let f = Fixture::new();
    let k = key("vault-new");
    let next = f.store.discard_vault(&k).unwrap();
    assert!(next >= 1);
    assert!(matches!(
        f.store
            .claim_entry(&k, 0, "tab1", None, &new_hot_exit_incarnation().unwrap())
            .unwrap(),
        HotExitClaimOutcome::Superseded(HotExitSupersession::Epoch { current }) if current == next
    ));
}

/// An entry is never deleted on a guess: an unreadable claim or a missing
/// claims directory makes the read fail closed, and the text stays for a
/// later read or an explicit discard (§3).
#[test]
fn an_unreadable_claim_keeps_its_entry_unread() {
    let f = Fixture::new();
    let k = key("vault-a");
    let c = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &c, 1, &entry("keep me")));
    let claim_file = f.vault_dir(&k).join(CLAIMS_DIR).join(stem("tab1"));
    std::fs::write(&claim_file, b"{ not json").unwrap();
    assert!(matches!(
        f.store.read_buffers(&k),
        Err(HotExitError::Damaged)
    ));
    assert_eq!(f.on_disk(&k, "tab1").unwrap().text, "keep me");

    std::fs::remove_dir_all(f.vault_dir(&k).join(CLAIMS_DIR)).unwrap();
    assert!(matches!(
        f.store.read_buffers(&k),
        Err(HotExitError::Damaged)
    ));
    assert_eq!(f.on_disk(&k, "tab1").unwrap().text, "keep me");

    // Discard All still clears the damaged vault and leaves it usable.
    f.store.discard_vault(&k).unwrap();
    assert!(f.store.read_buffers(&k).unwrap().entries.is_empty());
    assert!(f.on_disk(&k, "tab1").is_none());
    let c = claim(&f.store, &k, "tab2");
    durable(f.store.write_buffer(&k, &c, 1, &entry("again")));
}

/// A claim or epoch temp is published under the vault lock, so a read
/// (which holds it) removes any it finds; a staging vault directory goes
/// when the next vault is created under the store lock.
#[test]
fn dead_temps_beside_claims_the_epoch_and_vaults_are_cleaned() {
    let f = Fixture::new();
    let k = key("vault-a");
    let _ = claim(&f.store, &k, "tab1");
    let vault = f.vault_dir(&k);
    let claim_temp = vault.join(CLAIMS_DIR).join(format!("tab9{TEMP_MARK}dead"));
    let epoch_temp = vault.join(format!("{EPOCH_FILE}{TEMP_MARK}dead"));
    std::fs::write(&claim_temp, b"{}").unwrap();
    std::fs::write(&epoch_temp, b"7").unwrap();
    f.store.read_buffers(&k).unwrap();
    assert!(!claim_temp.exists());
    assert!(!epoch_temp.exists());

    let staging = f
        .store
        .root
        .join(VAULTS_DIR)
        .join(format!("abc{TEMP_MARK}dead"));
    std::fs::create_dir_all(staging.join(CLAIMS_DIR)).unwrap();
    let _ = claim(&f.store, &key("vault-b"), "tab1");
    assert!(!staging.exists());
}

/// The publishing rename reaches past MAX_PATH: a store under a deep root
/// still writes (Windows hands MoveFileExW the verbatim form).
#[cfg(windows)]
#[test]
fn a_store_under_a_long_path_still_writes() {
    let dir = tempfile::tempdir().unwrap();
    let deep = dir.path().join("d".repeat(120)).join("e".repeat(120));
    std::fs::create_dir_all(&deep).unwrap();
    let store = HotExitStore::open(&deep).unwrap();
    let k = key("vault-a");
    let c = claim(&store, &k, "tab1");
    assert!(
        store.vault_dir(&k).join(ENTRIES_DIR).as_os_str().len() > 260,
        "the fact must exercise a path past MAX_PATH"
    );
    durable(store.write_buffer(&k, &c, 1, &entry("deep")));
    assert_eq!(store.read_buffers(&k).unwrap().entries[0].text, "deep");
}

#[test]
fn two_processes_restoring_one_tab_never_both_count_as_kept() {
    // Two stores on one root stand for two processes; the claim files and
    // the file lock are what they share. Both restore the same entry, each
    // expecting the claim it read: only the first takes it over.
    for mine_first in [true, false] {
        let f = Fixture::new();
        let other = f.reopen();
        let k = key("vault-a");
        let a = claim(&f.store, &k, "tab1");
        durable(f.store.write_buffer(&k, &a, 1, &entry("restored")));
        let (first, second) = if mine_first {
            (&f.store, &other)
        } else {
            (&other, &f.store)
        };
        let winner = claim_over(first, &k, "tab1", &a.incarnation);
        assert_eq!(
            try_claim(second, &k, "tab1", Some(&a.incarnation)),
            HotExitClaimOutcome::Superseded(HotExitSupersession::Claim),
            "mine first = {mine_first}"
        );
        durable(first.write_buffer(&k, &winner, 1, &entry("winner")));
        assert_eq!(f.on_disk(&k, "tab1").unwrap().text, "winner");
    }
}

// ---- WB-29 §7: the compare-and-swap claim, retire_entry, the snapshot's
// ---- current claims and the vault hold.

/// A claim replaces only the current claim it names: one expecting none,
/// or the wrong one, over a live claim changes nothing on disk.
#[test]
fn a_claim_replaces_only_the_claim_it_expects() {
    let f = Fixture::new();
    let k = key("vault-a");
    let a = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &a, 1, &entry("A")));
    let claim_file = f.vault_dir(&k).join(CLAIMS_DIR).join(stem("tab1"));
    let before = std::fs::read(&claim_file).unwrap();
    let wrong = new_hot_exit_incarnation().unwrap();
    for expected in [None, Some(wrong.as_str())] {
        assert_eq!(
            try_claim(&f.store, &k, "tab1", expected),
            HotExitClaimOutcome::Superseded(HotExitSupersession::Claim),
            "expected {expected:?}"
        );
        assert_eq!(std::fs::read(&claim_file).unwrap(), before);
        assert_eq!(f.on_disk(&k, "tab1").unwrap().text, "A");
    }
    // A's own write still lands: nothing displaced it.
    durable(f.store.write_buffer(&k, &a, 2, &entry("A2")));
    let b = claim_over(&f.store, &k, "tab1", &a.incarnation);
    assert_eq!(b.adopted, std::slice::from_ref(&a.incarnation));
    assert_eq!(
        f.store.write_buffer(&k, &a, 3, &entry("A3")).unwrap(),
        HotExitWriteOutcome::Superseded(HotExitSupersession::Claim)
    );
}

/// With no current claim, an expected one is refused before anything is
/// removed or created: a cut-short discard's orphan stays for the read to
/// judge, and a vault with no directory gets none.
#[test]
fn an_expected_claim_over_no_current_claim_changes_nothing() {
    let f = Fixture::new();
    let k = key("vault-a");
    let a = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &a, 1, &entry("A")));
    INJECTED_STOP.with(|cell| *cell.borrow_mut() = Some(HotExitStep::Remove));
    assert!(f.store.discard_vault(&k).is_err());
    INJECTED_STOP.with(|cell| *cell.borrow_mut() = None);
    assert_eq!(
        try_claim(&f.store, &k, "tab1", Some(&a.incarnation)),
        HotExitClaimOutcome::Superseded(HotExitSupersession::Claim)
    );
    assert_eq!(
        f.on_disk(&k, "tab1").unwrap().text,
        "A",
        "the orphan was removed by a refused claim"
    );
    let fresh = key("vault-never-claimed");
    assert_eq!(
        try_claim(&f.store, &fresh, "tab1", Some(&a.incarnation)),
        HotExitClaimOutcome::Superseded(HotExitSupersession::Claim)
    );
    assert!(!f.vault_dir(&fresh).exists());
}

/// Codex (PR 1): a claim with a stale epoch on a vault with no directory is
/// refused before creating it: no directory, no vault cap spent.
#[test]
fn a_stale_epoch_claim_on_a_fresh_vault_creates_nothing() {
    let f = Fixture::new();
    for i in 0..(MAX_VAULTS + 1) {
        let fresh = key(&format!("never-claimed-{i}"));
        assert_eq!(
            f.store
                .claim_entry(
                    &fresh,
                    1,
                    "tab1",
                    None,
                    &new_hot_exit_incarnation().unwrap()
                )
                .unwrap(),
            HotExitClaimOutcome::Superseded(HotExitSupersession::Epoch { current: 0 })
        );
        assert!(!f.vault_dir(&fresh).exists());
    }
    // Every vault cap slot is still free: a real first claim lands.
    let _ = claim(&f.store, &key("really-claimed"), "tab1");
}

/// Codex (PR 1): contenders that overlap, each expecting the same claim: the
/// comparison runs under the vault lock, so exactly one takes it over. Each
/// has its own store and lock handle, as a process does.
#[test]
fn overlapping_claims_expecting_one_claim_have_one_winner() {
    let f = Fixture::new();
    let k = key("vault-a");
    let a = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &a, 1, &entry("restored")));
    // The test holds the vault lock until both contenders have read whatever
    // they read before taking it.
    let held = f.store.lock_vault(&k).unwrap();
    let contenders: Vec<_> = (0..2)
        .map(|_| {
            let store = f.reopen();
            let k = k.clone();
            let expected = a.incarnation.clone();
            std::thread::spawn(move || {
                store.claim_entry(
                    &k,
                    0,
                    "tab1",
                    Some(&expected),
                    &new_hot_exit_incarnation().unwrap(),
                )
            })
        })
        .collect();
    std::thread::sleep(Duration::from_millis(500));
    assert!(
        contenders.iter().all(|c| !c.is_finished()),
        "a contender finished while the vault lock was held"
    );
    drop(held);
    let outcomes: Vec<_> = contenders
        .into_iter()
        .map(|c| c.join().unwrap().unwrap())
        .collect();
    let winners = outcomes
        .iter()
        .filter(|o| matches!(o, HotExitClaimOutcome::Claimed(_)))
        .count();
    assert_eq!(winners, 1, "{outcomes:?}");
    assert!(
        outcomes.contains(&HotExitClaimOutcome::Superseded(HotExitSupersession::Claim)),
        "{outcomes:?}"
    );
}

/// Codex (PR 1): every sync of a claim's retry and of a retirement is an
/// error when it fails, never `Claimed` or done, and the healed retry lands.
#[test]
fn a_failed_sync_is_never_answered_claimed_or_retired() {
    let f = Fixture::new();
    let k = key("vault-a");
    let a = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &a, 1, &entry("A")));
    let incarnation = new_hot_exit_incarnation().unwrap();
    let retry =
        |store: &HotExitStore| store.claim_entry(&k, 0, "tab1", Some(&a.incarnation), &incarnation);
    assert!(matches!(
        retry(&f.store).unwrap(),
        HotExitClaimOutcome::Claimed(_)
    ));
    // The retry re-syncs the claim file, then `claims/` (`vaults/` is 0).
    f.fs.fail_nth(HotExitStep::SyncFile, 0);
    assert!(retry(&f.store).is_err(), "a failed file sync said Claimed");
    f.fs.fail_nth(HotExitStep::SyncDir, 1);
    assert!(
        retry(&f.store).is_err(),
        "a failed claims sync said Claimed"
    );
    f.fs.heal();
    assert!(matches!(
        retry(&f.store).unwrap(),
        HotExitClaimOutcome::Claimed(_)
    ));
    // A retirement's two directory syncs, with the records removed or not.
    let listed = [incarnation.clone(), a.incarnation.clone()];
    for failing in [0, 1] {
        f.fs.fail_nth(HotExitStep::SyncDir, failing);
        assert!(
            f.store.retire_entry(&k, "tab1", &listed).is_err(),
            "sync {failing} failed but the retirement said done"
        );
        f.fs.heal();
    }
    f.store.retire_entry(&k, "tab1", &listed).unwrap();
    assert!(f.files_in(&k, CLAIMS_DIR).is_empty());
    assert!(f.on_disk(&k, "tab1").is_none());
}

/// The red team's round-4 schedule: an adopter that never wrote leaves a
/// payload under a claim of another incarnation. The next adopter expects
/// the observed claim, and its `adopted` lists both, so its release
/// retires the restored text.
#[test]
fn adopted_lists_the_payload_an_earlier_adopter_never_wrote_over() {
    let f = Fixture::new();
    let k = key("vault-a");
    let a = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &a, 1, &entry("restored")));
    let b = claim_over(&f.store, &k, "tab1", &a.incarnation);
    // Crashed before B wrote: a new process reads A's payload under B.
    let snapshot = f.reopen().read_buffers(&k).unwrap();
    assert_eq!(snapshot.entries.len(), 1);
    assert_eq!(snapshot.entries[0].incarnation, a.incarnation);
    assert_eq!(
        snapshot.claims,
        [HotExitCurrentClaim {
            entry_id: "tab1".into(),
            incarnation: b.incarnation.clone()
        }]
    );
    // Expecting the payload's incarnation is refused; the observed claim's
    // is taken over.
    assert_eq!(
        try_claim(&f.store, &k, "tab1", Some(&a.incarnation)),
        HotExitClaimOutcome::Superseded(HotExitSupersession::Claim)
    );
    let c = claim_over(&f.store, &k, "tab1", &b.incarnation);
    assert_eq!(c.adopted, [b.incarnation.clone(), a.incarnation.clone()]);
    f.store.release_entry(&k, &c).unwrap();
    let after = f.reopen().read_buffers(&k).unwrap();
    assert!(after.entries.is_empty());
    assert!(after.claims.is_empty());
}

/// The snapshot lists every current claim: one with no payload, and one
/// over another incarnation's payload; a released claim is gone.
#[test]
fn the_snapshot_lists_every_current_claim() {
    let f = Fixture::new();
    let k = key("vault-a");
    let orphan = claim(&f.store, &k, "tab1");
    let y = claim(&f.store, &k, "tab2");
    durable(f.store.write_buffer(&k, &y, 1, &entry("Y")));
    let z = claim_over(&f.store, &k, "tab2", &y.incarnation);
    let gone = claim(&f.store, &k, "tab3");
    f.store.release_entry(&k, &gone).unwrap();
    let snapshot = f.reopen().read_buffers(&k).unwrap();
    assert_eq!(
        snapshot.claims,
        [
            HotExitCurrentClaim {
                entry_id: "tab1".into(),
                incarnation: orphan.incarnation.clone()
            },
            HotExitCurrentClaim {
                entry_id: "tab2".into(),
                incarnation: z.incarnation.clone()
            },
        ]
    );
    assert_eq!(snapshot.entries.len(), 1);
    assert_eq!(snapshot.entries[0].incarnation, y.incarnation);
    // The orphan is adoptable by its tab and retirable with no claim.
    let t = claim_over(&f.store, &k, "tab1", &orphan.incarnation);
    assert_eq!(t.adopted, std::slice::from_ref(&orphan.incarnation));
}

/// A claim retried with its own incarnation after it reached the disk
/// answers `Claimed` again, and only once its claim is durable: with the
/// directory sync after its rename failing, the retry re-syncs first.
#[test]
fn a_retried_claim_is_idempotent_and_durable_first() {
    let f = Fixture::new();
    let k = key("vault-a");
    let a = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &a, 1, &entry("restored")));
    let epoch = f.store.current_epoch(&k).unwrap();
    let incarnation = new_hot_exit_incarnation().unwrap();
    // The claim renames into place, then its directory sync fails.
    let rename_then_sync_fails = {
        f.fs.clear();
        f.fs.fail_nth(HotExitStep::SyncDir, 1);
        f.store
            .claim_entry(&k, epoch, "tab1", Some(&a.incarnation), &incarnation)
    };
    assert!(rename_then_sync_fails.is_err());
    f.fs.heal();
    assert!(
        f.fs.steps()
            .iter()
            .any(|(step, name)| *step == HotExitStep::Rename && *name == stem("tab1")),
        "premise: the claim reached the disk"
    );
    f.fs.clear();
    let retried = f
        .store
        .claim_entry(&k, epoch, "tab1", Some(&a.incarnation), &incarnation)
        .unwrap();
    let HotExitClaimOutcome::Claimed(b) = retried else {
        panic!("the retry was {retried:?}");
    };
    assert_eq!(b.incarnation, incarnation);
    assert_eq!(b.adopted, std::slice::from_ref(&a.incarnation));
    let steps = f.fs.steps();
    assert!(
        steps.contains(&(HotExitStep::SyncFile, stem("tab1")))
            && steps.contains(&(HotExitStep::SyncDir, CLAIMS_DIR.to_string())),
        "the retry vouched for an unsynced claim: {steps:?}"
    );
    // With the re-sync still failing (vaults/ is synced first), the retry
    // is an error, never Claimed.
    f.fs.fail_nth(HotExitStep::SyncDir, 1);
    assert!(
        f.store
            .claim_entry(&k, epoch, "tab1", Some(&a.incarnation), &incarnation)
            .is_err()
    );
    f.fs.heal();
}

/// Codex round 4: a discard cut short after its epoch leaves an older
/// epoch's claim and payload; a new claim there removes that payload
/// (durably) before it publishes, so a crash before its first write
/// restores nothing the discard retired.
#[test]
fn a_claim_with_no_current_claim_removes_an_orphaned_payload_first() {
    let f = Fixture::new();
    let k = key("vault-a");
    let c = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &c, 1, &entry("discarded")));
    INJECTED_STOP.with(|cell| *cell.borrow_mut() = Some(HotExitStep::Remove));
    assert!(f.store.discard_vault(&k).is_err());
    INJECTED_STOP.with(|cell| *cell.borrow_mut() = None);
    assert!(
        f.on_disk(&k, "tab1").is_some(),
        "premise: the delete never ran"
    );
    f.fs.clear();
    let fresh = claim(&f.store, &k, "tab1");
    assert!(fresh.adopted.is_empty());
    assert!(f.on_disk(&k, "tab1").is_none());
    let steps = f.fs.steps();
    let removed = steps
        .iter()
        .position(|s| *s == (HotExitStep::Remove, format!("{}{ENTRY_EXT}", stem("tab1"))))
        .expect("the orphaned payload was removed");
    let synced = steps
        .iter()
        .position(|s| *s == (HotExitStep::SyncDir, ENTRIES_DIR.to_string()))
        .expect("entries/ was synced");
    let published = steps
        .iter()
        .position(|s| *s == (HotExitStep::Rename, stem("tab1")))
        .expect("the claim was published");
    assert!(removed < synced && synced < published, "{steps:?}");
    // Crashed before its first write: nothing restores.
    assert!(f.reopen().read_buffers(&k).unwrap().entries.is_empty());
}

/// Even with nothing to remove, a claim over no current claim syncs
/// `entries/`: a retry after a failed sync must still make an earlier
/// removal durable.
#[test]
fn a_claim_with_no_current_claim_syncs_entries_even_with_nothing_to_remove() {
    let f = Fixture::new();
    let k = key("vault-a");
    let _ = claim(&f.store, &k, "tab0");
    f.fs.clear();
    let _ = claim(&f.store, &k, "tab1");
    assert!(
        f.fs.steps()
            .contains(&(HotExitStep::SyncDir, ENTRIES_DIR.to_string())),
        "{:?}",
        f.fs.steps()
    );
}

/// A current claim naming a later epoch than the vault's is damage to a
/// claim, as to a read: never treated as absent, nothing removed.
#[test]
fn a_later_epoch_claim_is_damage_to_a_claim() {
    let f = Fixture::new();
    let k = key("vault-a");
    f.store.discard_vault(&k).unwrap();
    let c = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &c, 1, &entry("unread")));
    std::fs::write(f.vault_dir(&k).join(EPOCH_FILE), b"0").unwrap();
    assert!(matches!(
        f.store
            .claim_entry(&k, 0, "tab1", None, &new_hot_exit_incarnation().unwrap()),
        Err(HotExitError::Damaged)
    ));
    assert_eq!(f.on_disk(&k, "tab1").unwrap().text, "unread");
}

#[test]
fn an_invalid_incarnation_is_refused() {
    let f = Fixture::new();
    let k = key("vault-a");
    for bad in [
        "",
        "ABCDEF0123456789ABCDEF0123456789",
        "short",
        &"g".repeat(32),
    ] {
        assert!(
            matches!(
                f.store.claim_entry(&k, 0, "tab1", None, bad),
                Err(HotExitError::InvalidIncarnation)
            ),
            "{bad:?}"
        );
    }
    let made = new_hot_exit_incarnation().unwrap();
    assert_eq!(made.len(), 32);
    assert!(validate_incarnation(&made).is_ok());
}

/// A retirement removes only the incarnations it lists, needs no claim,
/// checks no epoch, and syncs both directories every time.
#[test]
fn retire_entry_touches_only_listed_incarnations() {
    let f = Fixture::new();
    let k = key("vault-a");
    let a = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &a, 1, &entry("A")));
    let b = claim_over(&f.store, &k, "tab1", &a.incarnation);
    // An unlisted incarnation: nothing goes.
    let stranger = new_hot_exit_incarnation().unwrap();
    f.store.retire_entry(&k, "tab1", &[stranger]).unwrap();
    assert_eq!(f.on_disk(&k, "tab1").unwrap().text, "A");
    assert!(f.files_in(&k, CLAIMS_DIR).contains(&stem("tab1")));
    // The payload's incarnation only: the payload goes, B's claim stays.
    f.store
        .retire_entry(&k, "tab1", std::slice::from_ref(&a.incarnation))
        .unwrap();
    assert!(f.on_disk(&k, "tab1").is_none());
    assert!(f.files_in(&k, CLAIMS_DIR).contains(&stem("tab1")));
    // Across an epoch: a discard cut short after its epoch leaves B's
    // older-epoch claim, and the retirement removes it all the same.
    INJECTED_STOP.with(|cell| *cell.borrow_mut() = Some(HotExitStep::Remove));
    assert!(f.store.discard_vault(&k).is_err());
    INJECTED_STOP.with(|cell| *cell.borrow_mut() = None);
    assert!(
        f.files_in(&k, CLAIMS_DIR).contains(&stem("tab1")),
        "premise: the discard left B's claim"
    );
    f.store
        .retire_entry(&k, "tab1", std::slice::from_ref(&b.incarnation))
        .unwrap();
    assert!(f.files_in(&k, CLAIMS_DIR).is_empty());
    // Twice more: each call syncs both directories, removing nothing.
    for _ in 0..2 {
        f.fs.clear();
        f.store
            .retire_entry(&k, "tab1", std::slice::from_ref(&b.incarnation))
            .unwrap();
        let synced: Vec<String> =
            f.fs.steps()
                .into_iter()
                .filter(|(step, _)| *step == HotExitStep::SyncDir)
                .map(|(_, name)| name)
                .collect();
        assert_eq!(synced, [CLAIMS_DIR, ENTRIES_DIR]);
    }
    assert!(f.files_in(&k, CLAIMS_DIR).is_empty());
}

/// A stale adopting claim after a successor wrote never displaces it.
#[test]
fn a_stale_adopting_claim_never_displaces_a_successor() {
    let f = Fixture::new();
    let k = key("vault-a");
    let a = claim(&f.store, &k, "tab1");
    durable(f.store.write_buffer(&k, &a, 1, &entry("restored")));
    let b = claim_over(&f.store, &k, "tab1", &a.incarnation);
    durable(f.store.write_buffer(&k, &b, 1, &entry("successor")));
    assert_eq!(
        try_claim(&f.store, &k, "tab1", Some(&a.incarnation)),
        HotExitClaimOutcome::Superseded(HotExitSupersession::Claim)
    );
    let snapshot = f.reopen().read_buffers(&k).unwrap();
    assert_eq!(snapshot.entries[0].text, "successor");
}

/// The hold excludes a second store on the same root, and is freed when
/// its holder lets go.
#[test]
fn the_vault_hold_excludes_another_store_and_frees_on_release() {
    let f = Fixture::new();
    let other = f.reopen();
    let k = key("vault-a");
    let hold = f.store.hold_vault(&k).unwrap();
    assert!(matches!(
        other.hold_vault(&k),
        Err(HotExitError::HeldElsewhere)
    ));
    // Another vault is not held.
    let _b = other.hold_vault(&key("vault-b")).unwrap();
    hold.release();
    let again = other.hold_vault(&k).unwrap();
    drop(again);
    let _third = f.store.hold_vault(&k).unwrap();
}

/// The child half of `a_vault_hold_dies_with_its_process`: run only when
/// that fact re-invokes this test binary. It takes the hold and exits
/// without releasing it.
#[test]
fn hot_exit_child_takes_the_vault_hold_and_dies() {
    let Some(root) = std::env::var_os("SLATE_HOT_EXIT_HOLD_CHILD_ROOT") else {
        return;
    };
    let root = PathBuf::from(root);
    let store = HotExitStore::open(&root).unwrap();
    let hold = store.hold_vault(&key("vault-shared")).unwrap();
    std::fs::write(root.join("child-holding"), b"").unwrap();
    let deadline = std::time::Instant::now() + Duration::from_secs(120);
    while !root.join("parent-saw-held").exists() {
        assert!(
            std::time::Instant::now() < deadline,
            "the parent never answered"
        );
        std::thread::sleep(Duration::from_millis(20));
    }
    // Exit with the hold still taken: only the OS lets it go.
    std::mem::forget(hold);
    std::process::exit(0);
}

#[test]
fn a_vault_hold_dies_with_its_process() {
    let dir = tempfile::tempdir().unwrap();
    let store = HotExitStore::open(dir.path()).unwrap();
    let k = key("vault-shared");
    let mut child = std::process::Command::new(std::env::current_exe().unwrap())
        .args([
            "--exact",
            "hot_exit::tests::hot_exit_child_takes_the_vault_hold_and_dies",
            "--nocapture",
        ])
        .env("SLATE_HOT_EXIT_HOLD_CHILD_ROOT", dir.path())
        .spawn()
        .unwrap();
    let deadline = std::time::Instant::now() + Duration::from_secs(60);
    while !dir.path().join("child-holding").exists() {
        assert!(
            std::time::Instant::now() < deadline,
            "the child never took the hold"
        );
        std::thread::sleep(Duration::from_millis(20));
    }
    assert!(matches!(
        store.hold_vault(&k),
        Err(HotExitError::HeldElsewhere)
    ));
    std::fs::write(dir.path().join("parent-saw-held"), b"").unwrap();
    assert!(child.wait().unwrap().success());
    let _hold = store.hold_vault(&k).unwrap();
}
