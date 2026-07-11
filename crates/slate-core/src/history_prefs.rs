// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `.slate/prefs.json` `history` section (O-5 #543) — the retention
//! window the compaction fold honors.
//!
//! This module is the crate's FIRST prefs.json WRITER (the citations
//! section is written by the Mac app's `PrefsJsonStore`). The writer
//! discipline mirrors that store exactly: read the existing root
//! object, replace ONLY the `history` key, keep every other top-level
//! key byte-meaningful (forward compatibility both directions), and
//! publish atomically via temp-file + rename. A prefs.json that
//! exists but doesn't parse is NEVER clobbered — the write fails with
//! the same typed error the readers use.

use std::path::Path;

use crate::VaultError;

/// Cross-process, cross-language write serialization (adversarial
/// review): every prefs.json mutation — this module AND the Mac app's
/// `PrefsJsonStore` (bibliography section) — takes an exclusive
/// `flock` on the sidecar `prefs.json.lock` for the whole
/// read→merge→write→rename cycle. Atomic rename alone prevents torn
/// JSON but NOT lost updates: two read-modify-write cycles that both
/// read the old file drop whichever section renamed first.
struct PrefsLock {
    _file: std::fs::File,
}

impl PrefsLock {
    fn acquire(prefs_path: &Path) -> std::io::Result<Self> {
        let lock_path = prefs_path.with_extension("json.lock");
        if let Some(parent) = lock_path.parent() {
            std::fs::create_dir_all(parent)?;
        }
        let file = std::fs::OpenOptions::new()
            .create(true)
            .truncate(false)
            .write(true)
            .open(&lock_path)?;
        // Blocking exclusive lock; released on close (Drop).
        let rc = unsafe { libc::flock(std::os::fd::AsRawFd::as_raw_fd(&file), libc::LOCK_EX) };
        if rc != 0 {
            return Err(std::io::Error::last_os_error());
        }
        Ok(Self { _file: file })
    }
}

/// The `history` section of `.slate/prefs.json`.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct HistoryPrefs {
    /// Op-log retention window in days. The compaction fold discards
    /// versions older than this (o_spec §O-2). UI offers 30/90/180/365;
    /// the core accepts any non-zero value.
    pub retention_days: u32,
}

impl Default for HistoryPrefs {
    fn default() -> Self {
        // Matches `SessionConfig::oplog_retention_days`.
        Self { retention_days: 90 }
    }
}

/// Read the `history` section from the prefs file at `prefs_path`
/// (`<vault>/.slate/prefs.json`). `Ok(None)` when the file or the
/// section is absent — the caller keeps its configured default.
/// A file that exists but doesn't parse, or a `history` section with
/// an invalid `retention_days`, is a typed error (the prefs.json
/// policy: config must fail loudly, not half-apply).
pub fn read_history_prefs(prefs_path: &Path) -> Result<Option<HistoryPrefs>, VaultError> {
    let contents = match std::fs::read_to_string(prefs_path) {
        Ok(contents) => contents,
        Err(e) if e.kind() == std::io::ErrorKind::NotFound => return Ok(None),
        Err(e) => {
            return Err(VaultError::PrefsUnreadable {
                path: prefs_path.display().to_string(),
                reason: e.to_string(),
            });
        }
    };
    let root: serde_json::Value =
        serde_json::from_str(&contents).map_err(|e| VaultError::PrefsUnreadable {
            path: prefs_path.display().to_string(),
            reason: format!("invalid JSON: {e}"),
        })?;
    let Some(history) = root.get("history") else {
        return Ok(None);
    };
    let retention_days = history
        .get("retention_days")
        .and_then(serde_json::Value::as_u64)
        .filter(|d| (1..=u64::from(u32::MAX)).contains(d))
        .ok_or_else(|| VaultError::PrefsUnreadable {
            path: prefs_path.display().to_string(),
            reason: "history.retention_days must be a positive integer".into(),
        })?;
    Ok(Some(HistoryPrefs {
        retention_days: retention_days as u32,
    }))
}

/// Write (or replace) the `history` section at `prefs_path`,
/// preserving every other top-level key. Atomic temp-file + rename;
/// the parent directory is created if missing (a fresh vault may not
/// have written `.slate/prefs.json` yet).
pub fn write_history_prefs(prefs_path: &Path, prefs: &HistoryPrefs) -> Result<(), VaultError> {
    // Exclusive cross-writer lock for the whole read-modify-write.
    let _lock = PrefsLock::acquire(prefs_path).map_err(|e| VaultError::PrefsUnreadable {
        path: prefs_path.display().to_string(),
        reason: format!("prefs lock unavailable: {e}"),
    })?;
    let mut root: serde_json::Value = match std::fs::read_to_string(prefs_path) {
        Ok(contents) => {
            serde_json::from_str(&contents).map_err(|e| VaultError::PrefsUnreadable {
                path: prefs_path.display().to_string(),
                reason: format!("refusing to overwrite unparseable prefs.json (invalid JSON: {e})"),
            })?
        }
        Err(e) if e.kind() == std::io::ErrorKind::NotFound => {
            serde_json::Value::Object(serde_json::Map::new())
        }
        Err(e) => {
            return Err(VaultError::PrefsUnreadable {
                path: prefs_path.display().to_string(),
                reason: e.to_string(),
            });
        }
    };
    let Some(object) = root.as_object_mut() else {
        return Err(VaultError::PrefsUnreadable {
            path: prefs_path.display().to_string(),
            reason: "prefs.json root is not a JSON object".into(),
        });
    };
    object.insert(
        "history".to_string(),
        serde_json::json!({ "retention_days": prefs.retention_days }),
    );

    let serialized = serde_json::to_string_pretty(&root).map_err(|e| VaultError::Trash {
        message: format!("prefs serialization failed: {e}"),
    })?;
    if let Some(parent) = prefs_path.parent() {
        std::fs::create_dir_all(parent)?;
    }
    // Unique temp name: concurrent writers (already serialized by the
    // lock, but belt-and-braces for lock-bypassing readers/tools)
    // never share a staging file.
    static TEMP_COUNTER: std::sync::atomic::AtomicU64 = std::sync::atomic::AtomicU64::new(0);
    let tmp = prefs_path.with_extension(format!(
        "json.tmp.{}.{}",
        std::process::id(),
        TEMP_COUNTER.fetch_add(1, std::sync::atomic::Ordering::Relaxed)
    ));
    std::fs::write(&tmp, serialized.as_bytes())?;
    std::fs::rename(&tmp, prefs_path)?;
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    fn prefs_path(dir: &tempfile::TempDir) -> std::path::PathBuf {
        dir.path().join(".slate").join("prefs.json")
    }

    #[test]
    fn roundtrip_preserves_unknown_keys_and_sections() {
        let tmp = tempfile::tempdir().unwrap();
        let path = prefs_path(&tmp);
        std::fs::create_dir_all(path.parent().unwrap()).unwrap();
        std::fs::write(
            &path,
            r#"{
  "bibliography": { "sources": [{ "path": "refs.bib", "format": "bibtex" }] },
  "future_section": { "anything": [1, 2, 3] }
}"#,
        )
        .unwrap();

        write_history_prefs(
            &path,
            &HistoryPrefs {
                retention_days: 180,
            },
        )
        .unwrap();

        let root: serde_json::Value =
            serde_json::from_str(&std::fs::read_to_string(&path).unwrap()).unwrap();
        assert_eq!(root["history"]["retention_days"], 180, "section written");
        assert_eq!(
            root["bibliography"]["sources"][0]["path"], "refs.bib",
            "sibling sections preserved"
        );
        assert_eq!(
            root["future_section"]["anything"][2], 3,
            "unknown keys preserved"
        );

        assert_eq!(
            read_history_prefs(&path).unwrap(),
            Some(HistoryPrefs {
                retention_days: 180
            })
        );

        // Overwrite updates in place.
        write_history_prefs(&path, &HistoryPrefs { retention_days: 30 }).unwrap();
        assert_eq!(
            read_history_prefs(&path).unwrap(),
            Some(HistoryPrefs { retention_days: 30 })
        );
    }

    #[test]
    fn missing_file_and_missing_section_read_as_none() {
        let tmp = tempfile::tempdir().unwrap();
        let path = prefs_path(&tmp);
        assert_eq!(read_history_prefs(&path).unwrap(), None, "no file");

        std::fs::create_dir_all(path.parent().unwrap()).unwrap();
        std::fs::write(&path, r#"{ "bibliography": {} }"#).unwrap();
        assert_eq!(read_history_prefs(&path).unwrap(), None, "no history key");

        // Writing into a fresh vault creates the directory + file.
        let tmp2 = tempfile::tempdir().unwrap();
        let path2 = prefs_path(&tmp2);
        write_history_prefs(&path2, &HistoryPrefs::default()).unwrap();
        assert_eq!(
            read_history_prefs(&path2).unwrap(),
            Some(HistoryPrefs { retention_days: 90 })
        );
    }

    #[test]
    fn corrupt_or_invalid_prefs_are_typed_errors_and_never_clobbered() {
        let tmp = tempfile::tempdir().unwrap();
        let path = prefs_path(&tmp);
        std::fs::create_dir_all(path.parent().unwrap()).unwrap();
        std::fs::write(&path, "{ not json").unwrap();

        assert!(matches!(
            read_history_prefs(&path),
            Err(VaultError::PrefsUnreadable { .. })
        ));
        assert!(matches!(
            write_history_prefs(&path, &HistoryPrefs::default()),
            Err(VaultError::PrefsUnreadable { .. })
        ));
        assert_eq!(
            std::fs::read_to_string(&path).unwrap(),
            "{ not json",
            "the unparseable file is untouched"
        );

        // Invalid retention values are typed errors, not silent defaults.
        std::fs::write(&path, r#"{ "history": { "retention_days": 0 } }"#).unwrap();
        assert!(matches!(
            read_history_prefs(&path),
            Err(VaultError::PrefsUnreadable { .. })
        ));
        std::fs::write(&path, r#"{ "history": { "retention_days": "soon" } }"#).unwrap();
        assert!(matches!(
            read_history_prefs(&path),
            Err(VaultError::PrefsUnreadable { .. })
        ));
    }
}
