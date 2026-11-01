// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Plan 44 WB-18 (#1481): a fact that panics while holding the fault guard
//! fails alone. The guard's mutex cannot be poisoned for the facts after it,
//! and every fault-seam variable the holder set is gone when it unwinds — so
//! one real failure never reads as twenty-four.

use super::common::{
    FaultGuard, fault_guard, fault_mutex_is_poisoned, lock_fault_mutex_without_clearing,
};

/// A name outside the guard's own list, so no acquisition — by this fact or
/// any running beside it — can clear it: only the holder's `Drop` can.
const PROBE: &str = "SLATE_TEST_FAULT_GUARD_PROBE";

#[test]
fn a_panicking_holder_leaves_no_variable_it_set() {
    let holder = std::thread::spawn(|| {
        let mut env = fault_guard();
        env.set(PROBE, "1");
        panic!("the holder fails while it holds the guard");
    });
    assert!(holder.join().is_err(), "premise: the holder did not panic");

    assert_eq!(
        std::env::var_os(PROBE),
        None,
        "the unwound holder left {PROBE} set"
    );
}

#[test]
fn a_panicking_holder_leaves_the_fault_mutex_usable() {
    let holder = std::thread::spawn(|| {
        let _env = fault_guard();
        panic!("the holder fails while it holds the guard");
    });
    assert!(holder.join().is_err(), "premise: the holder did not panic");
    assert!(
        fault_mutex_is_poisoned(),
        "premise: the unwind did not poison the fault mutex"
    );

    // Taking the guard again returns one, instead of panicking on the
    // poisoned lock as every guarded fact after a failure once did.
    let _env = fault_guard();
}

#[test]
fn the_fault_guard_clears_every_fault_variable_on_acquisition() {
    // Written out here, not read from the guard's own list, so a name
    // dropped from that list fails this fact.
    const EVERY_FAULT_VARIABLE: [&str; 19] = [
        "SLATE_TEST_FAULT_AFTER_WRITE",
        "SLATE_TEST_FAULT_PRE_WRITE",
        "SLATE_TEST_FAULT_REINDEX",
        "SLATE_TEST_FAULT_REINDEX_DB",
        "SLATE_TEST_FAULT_INTENT_CLEAR",
        "SLATE_TEST_FAULT_PLANT_MARKERS",
        "SLATE_TEST_FAULT_RECOVERY_FINALIZE",
        "SLATE_TEST_INTENT_ABANDON_ZERO_FOR",
        "SLATE_TEST_COMMIT_AFTER_INTENT",
        "SLATE_TEST_SWEEP_AFTER_INTENT",
        "SLATE_TEST_RESOLVE_BEFORE_QUARANTINE",
        "SLATE_TEST_SECOND_WRITER_BEFORE_QUARANTINE",
        "SLATE_TEST_STALL_ORPHAN_REPAIR",
        "SLATE_TEST_TORN_FILE_TASKS",
        "SLATE_TEST_TORN_NOTE_READ",
        "SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED",
        "SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE",
        "SLATE_TEST_ROOT_FILE_ID_INFO_FAILS",
        "SLATE_TEST_ROOT_FILE_INDEX_COLLIDES",
    ];
    let lock = lock_fault_mutex_without_clearing();
    for name in EVERY_FAULT_VARIABLE {
        // SAFETY: the fault mutex is held. The value is a substring no
        // fixture path contains, so no unguarded fact's trigger fires.
        unsafe { std::env::set_var(name, "wb18-no-fixture-path") };
    }

    let _env = FaultGuard::clearing(lock);

    let left: Vec<&str> = EVERY_FAULT_VARIABLE
        .into_iter()
        .filter(|name| std::env::var_os(name).is_some())
        .collect();
    assert!(left.is_empty(), "taking the guard left {left:?} set");
}

/// The census: a fault-seam variable is set only through the guard. The
/// watcher's own switch is no fault seam, and its fact sets it under its
/// own lock. It reads every source under `src`, so an inline test module
/// beside the code is held to the same rule. Its ceiling: it reads literal
/// names only.
#[test]
fn no_test_sets_a_fault_variable_outside_the_guard() {
    let root = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("src");
    let pattern = regex::Regex::new(r#"set_var\(\s*"SLATE_TEST_\w+""#).unwrap();
    let mut found = Vec::new();
    let mut watcher_switches = 0;
    let mut sources = 0;
    let mut folders = vec![root.clone()];
    while let Some(folder) = folders.pop() {
        for entry in std::fs::read_dir(&folder).unwrap() {
            let path = entry.unwrap().path();
            if path.is_dir() {
                folders.push(path);
                continue;
            }
            if path.extension().and_then(|e| e.to_str()) != Some("rs") {
                continue;
            }
            sources += 1;
            let name = path
                .strip_prefix(&root)
                .unwrap()
                .to_string_lossy()
                .replace('\\', "/");
            if name == "session/tests/common.rs" {
                continue;
            }
            let text = std::fs::read_to_string(&path).unwrap();
            for hit in pattern.find_iter(&text) {
                if name == "session/tests/watch.rs"
                    && hit.as_str().contains("SLATE_TEST_DISABLE_VAULT_WATCHER")
                {
                    watcher_switches += 1;
                    continue;
                }
                let line = text[..hit.start()].matches('\n').count() + 1;
                found.push(format!("{name}:{line}: {}", hit.as_str()));
            }
        }
    }
    assert!(
        sources > 100,
        "the census found {sources} sources under src — the walk is broken"
    );
    assert_eq!(
        watcher_switches, 2,
        "the watcher switch's fact no longer sets its variable twice — update the census"
    );
    assert!(
        found.is_empty(),
        "fault-seam variables set outside the fault guard (use `fault_guard()` and `set`):\n  {}",
        found.join("\n  ")
    );
}
