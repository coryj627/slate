//! Desktop `VaultProvider` implementation backed by `std::fs`.
//!
//! Used on Mac and Windows. iOS and Android supply their own host-side
//! providers that route through their security-scoped resource APIs.
//!
//! See `super` (the `vault` module's `mod.rs`) for the cross-cutting
//! decisions: path safety rules, symlink handling, atomic-write
//! semantics, delete-via-trash policy.

use std::io::Write;
use std::path::{Component, Path, PathBuf};
use std::sync::Arc;
use std::time::UNIX_EPOCH;
use std::{fs, io};

use crate::VaultError;

use super::provider::{DirEntry, EntryKind, FileEventSink, FileStat, VaultProvider, WatchHandle};

/// A vault provider backed by an on-disk directory.
#[derive(Debug)]
pub struct FsVaultProvider {
    root: PathBuf,
}

impl FsVaultProvider {
    /// Wrap an on-disk directory as a vault provider.
    ///
    /// The directory does not need to exist yet; subsequent operations
    /// will fail with `VaultError::Io` for non-existent paths. This is
    /// intentional — the provider doesn't enforce vault-existence
    /// semantics, the calling `VaultSession::open` does.
    pub fn new(root: PathBuf) -> Self {
        Self { root }
    }

    /// Read the file at `relative` and return its blake3 hex digest.
    /// Convenience for the indexer; not part of the `VaultProvider`
    /// trait so it can stay desktop-specific.
    pub fn content_hash_for(&self, relative: &str) -> Result<String, VaultError> {
        let bytes = self.read_file(relative)?;
        Ok(content_hash(&bytes))
    }

    fn resolve(&self, relative: &str) -> Result<PathBuf, VaultError> {
        resolve_relative(&self.root, relative)
    }

    /// Like `resolve`, but rejects paths that resolve to the vault root.
    ///
    /// Mutator entry points (`write_file`, `delete`, `rename`) use this
    /// so that `""` and `"."` cannot trash, overwrite, or move the
    /// vault directory itself. Read-side operations keep using `resolve`
    /// because listing/stating the root is well-defined.
    fn resolve_for_mutation(&self, relative: &str) -> Result<PathBuf, VaultError> {
        let path = self.resolve(relative)?;
        if path == self.root {
            return Err(VaultError::InvalidPath {
                path: relative.to_string(),
                reason: "operation would target the vault root".into(),
            });
        }
        Ok(path)
    }
}

/// Compute the blake3 hex digest of a byte slice. Used for vault file
/// content-hash columns, cache-keying, and conflict detection.
pub fn content_hash(bytes: &[u8]) -> String {
    blake3::hash(bytes).to_hex().to_string()
}

/// Normalize a vault-relative path string against a root directory.
///
/// Returns `VaultError::InvalidPath` if the input is absolute, contains
/// any `..` component, or contains a Windows path prefix. The `.`
/// component is allowed and stripped (so `""`, `"."`, and `"./foo"` all
/// resolve cleanly).
fn resolve_relative(root: &Path, relative: &str) -> Result<PathBuf, VaultError> {
    let rel = Path::new(relative);
    if rel.is_absolute() {
        return Err(VaultError::InvalidPath {
            path: relative.to_string(),
            reason: "absolute paths are not allowed; vault-relative only".into(),
        });
    }

    let mut normalized = PathBuf::new();
    for component in rel.components() {
        match component {
            Component::Normal(s) => normalized.push(s),
            Component::CurDir => {}
            Component::ParentDir => {
                return Err(VaultError::InvalidPath {
                    path: relative.to_string(),
                    reason: "parent-directory references (..) are not allowed".into(),
                });
            }
            Component::RootDir | Component::Prefix(_) => {
                return Err(VaultError::InvalidPath {
                    path: relative.to_string(),
                    reason: "absolute paths and platform prefixes are not allowed".into(),
                });
            }
        }
    }

    Ok(root.join(normalized))
}

impl VaultProvider for FsVaultProvider {
    fn list_dir(&self, relative: &str) -> Result<Vec<DirEntry>, VaultError> {
        let path = self.resolve(relative)?;
        let read = fs::read_dir(&path)?;

        let mut entries: Vec<DirEntry> = Vec::new();
        for result in read {
            let entry = result?;
            // `file_type` is cheap on most platforms (uses the readdir
            // d_type field) and does NOT follow symlinks — exactly what
            // we want for reporting EntryKind::Symlink as itself.
            let file_type = entry.file_type()?;
            let kind = if file_type.is_dir() {
                EntryKind::Directory
            } else if file_type.is_symlink() {
                EntryKind::Symlink
            } else {
                EntryKind::File
            };

            let name = entry.file_name().to_string_lossy().into_owned();
            entries.push(DirEntry { name, kind });
        }

        // Stable, deterministic ordering for tests and UI predictability.
        entries.sort_by(|a, b| a.name.cmp(&b.name));
        Ok(entries)
    }

    fn read_file(&self, relative: &str) -> Result<Vec<u8>, VaultError> {
        let path = self.resolve(relative)?;
        Ok(fs::read(&path)?)
    }

    fn write_file(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
        let path = self.resolve_for_mutation(relative)?;
        atomic_write(&path, contents)?;
        Ok(())
    }

    fn delete(&self, relative: &str) -> Result<(), VaultError> {
        let path = self.resolve_for_mutation(relative)?;
        // `symlink_metadata` uses lstat and does not follow symlinks, so
        // a broken symlink (a real directory entry whose target is gone)
        // still passes this guard. `path.exists()` would have hidden it.
        // Propagate the raw io::Error so permission-denied (etc.)
        // doesn't get masked as NotFound.
        let meta = path.symlink_metadata().map_err(VaultError::Io)?;

        // Broken symlinks: some `trash` backends reject dangling links
        // (macOS routes through Finder, which refuses). The target file
        // is already gone so there's nothing recoverable to send to the
        // trash anyway — unlink the dangling entry directly.
        if meta.file_type().is_symlink() && !path.exists() {
            return fs::remove_file(&path).map_err(VaultError::Io);
        }

        match trash::delete(&path) {
            Ok(()) => Ok(()),
            Err(e) => Err(VaultError::Trash {
                message: e.to_string(),
            }),
        }
    }

    fn rename(&self, from: &str, to: &str) -> Result<(), VaultError> {
        let from_path = self.resolve_for_mutation(from)?;
        let to_path = self.resolve_for_mutation(to)?;
        // Refuse early if the source is missing so we don't leave behind
        // freshly-minted destination parent directories on a failed move.
        // `symlink_metadata` (lstat) checks the directory entry itself,
        // so a broken symlink — still a real entry that `rename(2)` will
        // happily move — is not mistakenly classified as NotFound, and
        // other io errors (permission denied, etc.) propagate untouched.
        match from_path.symlink_metadata() {
            Ok(_) => {}
            Err(e) if e.kind() == io::ErrorKind::NotFound => {
                return Err(VaultError::Io(io::Error::new(
                    io::ErrorKind::NotFound,
                    format!("rename source does not exist: {from}"),
                )));
            }
            Err(e) => return Err(VaultError::Io(e)),
        }
        if let Some(parent) = to_path.parent() {
            fs::create_dir_all(parent)?;
        }
        fs::rename(&from_path, &to_path)?;
        Ok(())
    }

    fn stat(&self, relative: &str) -> Result<FileStat, VaultError> {
        let path = self.resolve(relative)?;
        // `metadata()` follows symlinks; the indexer wants the target's
        // mtime/size when stating a symlink, so this matches the
        // documented semantics in `super::mod`.
        let meta = fs::metadata(&path)?;
        let kind = if meta.is_dir() {
            EntryKind::Directory
        } else if meta.file_type().is_symlink() {
            EntryKind::Symlink
        } else {
            EntryKind::File
        };
        let mtime_ms = meta
            .modified()
            .ok()
            .and_then(|t| t.duration_since(UNIX_EPOCH).ok())
            .map(|d| d.as_millis() as i64)
            .unwrap_or(0);
        Ok(FileStat {
            size_bytes: meta.len(),
            mtime_ms,
            kind,
        })
    }

    fn watch(&self, _sink: Arc<dyn FileEventSink>) -> Result<Option<WatchHandle>, VaultError> {
        // Real watcher is a follow-up issue. For Milestone A, returning
        // None tells the session to fall back to refresh-on-foreground
        // semantics, which works for the tester build.
        Ok(None)
    }
}

/// Atomically replace the contents of `path` by writing to a temp file
/// in the same directory and renaming it into place.
fn atomic_write(path: &Path, contents: &[u8]) -> io::Result<()> {
    let parent = path.parent().ok_or_else(|| {
        io::Error::new(
            io::ErrorKind::InvalidInput,
            "target path has no parent directory",
        )
    })?;
    fs::create_dir_all(parent)?;

    // `tempfile::NamedTempFile::new_in` places the temp file alongside
    // the target so the rename is atomic on the same filesystem.
    let mut temp = tempfile::NamedTempFile::new_in(parent)?;
    temp.write_all(contents)?;
    temp.flush()?;
    temp.as_file().sync_data()?;
    temp.persist(path).map_err(|e| e.error)?;
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    fn vault() -> (tempfile::TempDir, FsVaultProvider) {
        let tmp = tempfile::tempdir().expect("tempdir");
        let provider = FsVaultProvider::new(tmp.path().to_path_buf());
        (tmp, provider)
    }

    #[test]
    fn content_hash_is_deterministic() {
        let h1 = content_hash(b"hello world");
        let h2 = content_hash(b"hello world");
        assert_eq!(h1, h2);
        assert_eq!(h1.len(), 64); // blake3 hex = 64 chars
    }

    #[test]
    fn content_hash_distinguishes_inputs() {
        assert_ne!(content_hash(b"a"), content_hash(b"b"));
        assert_ne!(content_hash(b""), content_hash(b" "));
    }

    #[test]
    fn write_then_read_round_trip() {
        let (_tmp, p) = vault();
        p.write_file("notes/foo.md", b"# hello").unwrap();
        let bytes = p.read_file("notes/foo.md").unwrap();
        assert_eq!(bytes, b"# hello");
    }

    #[test]
    fn write_overwrites_existing_file() {
        let (_tmp, p) = vault();
        p.write_file("notes/foo.md", b"first").unwrap();
        p.write_file("notes/foo.md", b"second").unwrap();
        let bytes = p.read_file("notes/foo.md").unwrap();
        assert_eq!(bytes, b"second");
    }

    #[test]
    fn write_creates_intermediate_directories() {
        let (_tmp, p) = vault();
        p.write_file("a/b/c/d.md", b"deep").unwrap();
        let bytes = p.read_file("a/b/c/d.md").unwrap();
        assert_eq!(bytes, b"deep");
    }

    #[test]
    fn list_dir_returns_alphabetical_entries() {
        let (_tmp, p) = vault();
        p.write_file("zebra.md", b"").unwrap();
        p.write_file("apple.md", b"").unwrap();
        p.write_file("mango.md", b"").unwrap();

        let entries = p.list_dir("").unwrap();
        let names: Vec<&str> = entries.iter().map(|e| e.name.as_str()).collect();
        assert_eq!(names, vec!["apple.md", "mango.md", "zebra.md"]);
        for e in &entries {
            assert_eq!(e.kind, EntryKind::File);
        }
    }

    #[test]
    fn list_dir_distinguishes_files_and_directories() {
        let (_tmp, p) = vault();
        p.write_file("note.md", b"").unwrap();
        p.write_file("subfolder/inner.md", b"").unwrap();

        let entries = p.list_dir("").unwrap();
        let map: std::collections::HashMap<_, _> =
            entries.iter().map(|e| (e.name.as_str(), e.kind)).collect();
        assert_eq!(map.get("note.md"), Some(&EntryKind::File));
        assert_eq!(map.get("subfolder"), Some(&EntryKind::Directory));
    }

    #[test]
    fn stat_reports_size_and_kind() {
        let (_tmp, p) = vault();
        p.write_file("foo.md", b"hello world").unwrap();
        let stat = p.stat("foo.md").unwrap();
        assert_eq!(stat.size_bytes, 11);
        assert_eq!(stat.kind, EntryKind::File);
        assert!(stat.mtime_ms > 0);
    }

    #[test]
    fn stat_missing_file_returns_io_not_found() {
        let (_tmp, p) = vault();
        let err = p.stat("never.md").unwrap_err();
        match err {
            VaultError::Io(io) => assert_eq!(io.kind(), io::ErrorKind::NotFound),
            other => panic!("expected Io NotFound, got {other:?}"),
        }
    }

    #[test]
    fn read_missing_file_returns_io_not_found() {
        let (_tmp, p) = vault();
        let err = p.read_file("missing.md").unwrap_err();
        match err {
            VaultError::Io(io) => assert_eq!(io.kind(), io::ErrorKind::NotFound),
            other => panic!("expected Io NotFound, got {other:?}"),
        }
    }

    #[test]
    fn rename_moves_file() {
        let (_tmp, p) = vault();
        p.write_file("old.md", b"content").unwrap();
        p.rename("old.md", "new.md").unwrap();
        assert!(p.read_file("old.md").is_err());
        let bytes = p.read_file("new.md").unwrap();
        assert_eq!(bytes, b"content");
    }

    #[test]
    fn rename_into_new_directory_creates_path() {
        let (_tmp, p) = vault();
        p.write_file("old.md", b"content").unwrap();
        p.rename("old.md", "archive/2026/old.md").unwrap();
        let bytes = p.read_file("archive/2026/old.md").unwrap();
        assert_eq!(bytes, b"content");
    }

    #[test]
    fn delete_removes_the_file() {
        let (_tmp, p) = vault();
        p.write_file("doomed.md", b"").unwrap();
        p.delete("doomed.md").unwrap();
        let err = p.read_file("doomed.md").unwrap_err();
        match err {
            VaultError::Io(io) => assert_eq!(io.kind(), io::ErrorKind::NotFound),
            other => panic!("expected Io NotFound, got {other:?}"),
        }
    }

    #[test]
    fn delete_missing_path_errors() {
        let (_tmp, p) = vault();
        let err = p.delete("never.md").unwrap_err();
        match err {
            VaultError::Io(io) => assert_eq!(io.kind(), io::ErrorKind::NotFound),
            other => panic!("expected Io NotFound, got {other:?}"),
        }
    }

    #[test]
    fn absolute_path_is_rejected() {
        let (_tmp, p) = vault();
        let err = p.read_file("/etc/passwd").unwrap_err();
        assert!(matches!(err, VaultError::InvalidPath { .. }));
    }

    #[test]
    fn parent_directory_traversal_is_rejected() {
        let (_tmp, p) = vault();
        let err = p.read_file("../../escape.md").unwrap_err();
        assert!(matches!(err, VaultError::InvalidPath { .. }));
    }

    #[test]
    fn current_directory_component_is_stripped() {
        let (_tmp, p) = vault();
        p.write_file("./inside.md", b"ok").unwrap();
        let bytes = p.read_file("inside.md").unwrap();
        assert_eq!(bytes, b"ok");
    }

    #[test]
    fn empty_relative_lists_root() {
        let (_tmp, p) = vault();
        p.write_file("a.md", b"").unwrap();
        p.write_file("b.md", b"").unwrap();
        let entries = p.list_dir("").unwrap();
        assert_eq!(entries.len(), 2);
    }

    #[test]
    fn dot_relative_lists_root() {
        let (_tmp, p) = vault();
        p.write_file("a.md", b"").unwrap();
        let entries = p.list_dir(".").unwrap();
        assert_eq!(entries.len(), 1);
        assert_eq!(entries[0].name, "a.md");
    }

    #[test]
    fn content_hash_for_reads_file() {
        let (_tmp, p) = vault();
        p.write_file("hash-me.md", b"abc").unwrap();
        let h = p.content_hash_for("hash-me.md").unwrap();
        assert_eq!(h, content_hash(b"abc"));
    }

    #[test]
    fn watch_returns_none_in_milestone_a() {
        let (_tmp, p) = vault();
        struct NullSink;
        impl FileEventSink for NullSink {
            fn on_event(&self, _event: super::super::provider::FileEvent) {}
        }
        let sink: Arc<dyn FileEventSink> = Arc::new(NullSink);
        let result = p.watch(sink).unwrap();
        assert!(result.is_none());
    }

    #[cfg(unix)]
    #[test]
    fn list_dir_reports_symlink_as_symlink() {
        let (tmp, p) = vault();
        p.write_file("target.md", b"target").unwrap();
        std::os::unix::fs::symlink(tmp.path().join("target.md"), tmp.path().join("link.md"))
            .unwrap();

        let entries = p.list_dir("").unwrap();
        let map: std::collections::HashMap<_, _> =
            entries.iter().map(|e| (e.name.as_str(), e.kind)).collect();
        assert_eq!(map.get("target.md"), Some(&EntryKind::File));
        assert_eq!(map.get("link.md"), Some(&EntryKind::Symlink));
    }

    #[test]
    fn mutators_refuse_vault_root() {
        let (tmp, p) = vault();
        p.write_file("survivor.md", b"hi").unwrap();

        for relative in ["", "."] {
            let err = p.delete(relative).unwrap_err();
            assert!(
                matches!(err, VaultError::InvalidPath { .. }),
                "delete({relative:?}) should reject vault root, got {err:?}"
            );
            let err = p.write_file(relative, b"x").unwrap_err();
            assert!(
                matches!(err, VaultError::InvalidPath { .. }),
                "write_file({relative:?}) should reject vault root, got {err:?}"
            );
            let err = p.rename(relative, "elsewhere.md").unwrap_err();
            assert!(
                matches!(err, VaultError::InvalidPath { .. }),
                "rename(from={relative:?}) should reject vault root, got {err:?}"
            );
            let err = p.rename("survivor.md", relative).unwrap_err();
            assert!(
                matches!(err, VaultError::InvalidPath { .. }),
                "rename(to={relative:?}) should reject vault root, got {err:?}"
            );
        }

        // The vault directory and its sole file are untouched.
        assert!(tmp.path().is_dir());
        assert_eq!(p.read_file("survivor.md").unwrap(), b"hi");
    }

    #[test]
    fn rename_missing_source_does_not_create_destination_parents() {
        let (_tmp, p) = vault();
        let err = p
            .rename("nope.md", "archive/2026/old.md")
            .expect_err("missing source should error");
        match err {
            VaultError::Io(io) => assert_eq!(io.kind(), io::ErrorKind::NotFound),
            other => panic!("expected Io NotFound, got {other:?}"),
        }
        // The dest tree must not exist — that's the regression we're guarding.
        let listing = p.list_dir("").unwrap();
        assert!(
            listing.is_empty(),
            "vault root should be empty, found {listing:?}"
        );
    }

    #[cfg(unix)]
    #[test]
    fn rename_moves_broken_symlink() {
        let (tmp, p) = vault();
        std::os::unix::fs::symlink(
            tmp.path().join("does-not-exist.md"),
            tmp.path().join("broken.md"),
        )
        .unwrap();

        p.rename("broken.md", "still-broken.md")
            .expect("rename should move the dangling entry");

        assert!(tmp.path().join("broken.md").symlink_metadata().is_err());
        let moved = tmp
            .path()
            .join("still-broken.md")
            .symlink_metadata()
            .expect("renamed symlink should be a real entry at the destination");
        assert!(moved.file_type().is_symlink());
    }

    #[cfg(unix)]
    #[test]
    fn delete_removes_broken_symlink() {
        let (tmp, p) = vault();
        std::os::unix::fs::symlink(
            tmp.path().join("does-not-exist.md"),
            tmp.path().join("broken.md"),
        )
        .unwrap();
        // Sanity: the broken link is visible to list_dir but `exists()`
        // would have hidden it.
        assert!(!tmp.path().join("broken.md").exists());
        assert!(tmp.path().join("broken.md").symlink_metadata().is_ok());

        p.delete("broken.md").expect("delete should succeed");
        assert!(tmp.path().join("broken.md").symlink_metadata().is_err());
    }
}
