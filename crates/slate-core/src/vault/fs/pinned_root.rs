// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Structural renames beneath a pinned vault-root descriptor (#1449).
//!
//! A structural rename, move or undo used to name both ends by absolute
//! path. The kernel resolved every ancestor again at the call and followed
//! symlinks, so a vault folder swapped for a link after Slate saw it moved
//! files into or out of the vault (#1430: matrix §2, probe P1b). Here the
//! vault root is one open directory descriptor, and nothing beneath it is
//! followed:
//!
//! - **macOS:** `renameatx_np(root, from, root, to, RENAME_EXCL |
//!   RENAME_NOFOLLOW_ANY)` with both paths relative to the root. A symbolic
//!   link anywhere in either path fails the call with `ELOOP` (P1b).
//! - **Linux:** both parents are pinned with `openat2(RESOLVE_NO_SYMLINKS |
//!   RESOLVE_BENEATH)` from the root, then `renameat2(parent, leaf, parent,
//!   leaf, RENAME_NOREPLACE)` acts on those directories. Where `openat2` is
//!   missing (`ENOSYS` before Linux 5.6, `EPERM` under some seccomp filters)
//!   the parents are pinned by opening one component at a time with
//!   `O_NOFOLLOW` instead. Neither path ever falls back to an absolute-path
//!   rename.
//!
//! Both platforms check the ends with that same one-component-at-a-time walk
//! before renaming, and create a missing destination folder only through it
//! (`mkdirat` in the pinned parent). A link or a non-folder found by the walk
//! or reported by the kernel is a typed refusal ([`VaultError::InvalidPath`],
//! whose reason core renders for both hosts) that changes nothing.
//!
//! The root itself is the descriptor, not a component, so a vault whose root
//! is a symlink keeps working. This hardens the ancestors, not the leaf: the
//! final component is still a name, and binding its identity is an open
//! #1125 decision (#1430, Decision 2 option C).
//!
//! PR 22's design (#1441) gives the session an `FsRoot` that holds the root
//! descriptor for the session's lifetime. Until it lands, the provider opens
//! one [`PinnedRoot`] per rename; `FsRoot` adopts this type by handing it
//! the descriptor it already holds.

use std::ffi::{CStr, CString};
use std::os::unix::ffi::OsStrExt as _;
use std::path::{Component, Path, PathBuf};
use std::{fs, io};

use crate::VaultError;

use super::{create_directory_at, directory_open_flags, open_directory_at};

/// `RENAME_NOFOLLOW_ANY` from the macOS SDK's `<sys/stdio.h>` (`0x00000010`).
/// The `libc` crate (0.2.186) does not export it. With it, a symbolic link
/// met while resolving either path fails the rename with `ELOOP`; a final
/// component that is itself a link is still renamed, never followed. XNU's
/// `renameatx_np()` accepts it on every macOS Slate supports (15 and later;
/// #1430 matrix §3.1). `renameatx_np_honours_rename_nofollow_any` pins the
/// value against the running kernel.
#[cfg(target_os = "macos")]
pub(super) const RENAME_NOFOLLOW_ANY: libc::c_uint = 0x0000_0010;

/// The vault root, held open as one directory descriptor for a structural
/// rename. Everything beneath it is resolved without following a link.
#[derive(Debug)]
pub(super) struct PinnedRoot {
    dir: fs::File,
}

/// One end of a structural rename: the directory that holds the final
/// component, pinned beneath the root, and that component.
#[derive(Debug)]
pub(super) struct RenameEnd<'a> {
    /// The caller's vault-relative spelling, for the refusal.
    original: &'a str,
    /// The normalized root-relative path.
    relative: &'a Path,
    /// `relative` for the kernel: macOS renames from the root by it.
    #[cfg(target_os = "macos")]
    relative_c: CString,
    parent: fs::File,
    leaf: CString,
}

impl PinnedRoot {
    /// Open the vault root. The root itself is followed, so a vault whose
    /// root is a symlink keeps working: the descriptor is the root.
    pub(super) fn open(root: &Path) -> Result<Self, VaultError> {
        use std::os::unix::fs::OpenOptionsExt as _;

        let dir = fs::OpenOptions::new()
            .read(true)
            .custom_flags(directory_open_flags(false))
            .open(root)
            .map_err(VaultError::Io)?;
        Ok(Self { dir })
    }

    /// Pin the directory that holds `relative`'s final component. A missing
    /// folder is `VaultError::Io(NotFound)` unless `create_missing`, which
    /// makes it inside its pinned parent. A link or a non-folder on the way
    /// is the typed refusal.
    pub(super) fn end<'a>(
        &self,
        relative: &'a Path,
        original: &'a str,
        create_missing: bool,
    ) -> Result<RenameEnd<'a>, VaultError> {
        let leaf = relative
            .file_name()
            .ok_or_else(|| invalid(original, "the path has no final component"))?;
        let leaf = c_string(leaf.as_bytes(), original)?;
        #[cfg(target_os = "macos")]
        let relative_c = c_string(relative.as_os_str().as_bytes(), original)?;
        let parent = self.pin(parent_of(relative), original, create_missing)?;
        Ok(RenameEnd {
            original,
            relative,
            #[cfg(target_os = "macos")]
            relative_c,
            parent,
            leaf,
        })
    }

    /// The atomic no-replace rename: `EEXIST` if `to` is occupied, and never
    /// through a link among either end's ancestors.
    pub(super) fn rename_no_replace(&self, from: &RenameEnd, to: &RenameEnd) -> io::Result<()> {
        #[cfg(target_os = "macos")]
        {
            self.renameatx(from, to, libc::RENAME_EXCL | RENAME_NOFOLLOW_ANY)
        }
        #[cfg(target_os = "linux")]
        {
            renameat2_no_replace(from, to)
        }
        #[cfg(not(any(target_os = "macos", target_os = "linux")))]
        {
            let _ = (from, to);
            // Fail closed where no atomic no-replace primitive is wired: a
            // plain rename could replace a destination created after the
            // provider's check.
            Err(io::Error::new(
                io::ErrorKind::Unsupported,
                "atomic no-clobber rename is unsupported on this target",
            ))
        }
    }

    /// The #1077 IR-5 case, reached only when the provider proved `from` and
    /// `to` are one entry under two spellings (a case-only or
    /// normalization-only rename the volume reports as occupied): a rename
    /// without no-replace, still beneath the root and never through a link.
    pub(super) fn rename_same_entry(&self, from: &RenameEnd, to: &RenameEnd) -> io::Result<()> {
        #[cfg(target_os = "macos")]
        {
            self.renameatx(from, to, RENAME_NOFOLLOW_ANY)
        }
        #[cfg(not(target_os = "macos"))]
        {
            use std::os::fd::AsRawFd as _;
            // SAFETY: both parents are open directories and both leaves are
            // NUL-terminated single components that outlive the call.
            let result = unsafe {
                libc::renameat(
                    from.parent.as_raw_fd(),
                    from.leaf.as_ptr(),
                    to.parent.as_raw_fd(),
                    to.leaf.as_ptr(),
                )
            };
            os_result(result)
        }
    }

    /// Map a failed kernel rename. `ELOOP` or `ENOTDIR` means a folder in one
    /// of the paths became a link or a file after the ends were pinned: the
    /// same typed refusal, naming that folder when it still shows. Nothing
    /// was renamed, and nothing is retried another way.
    pub(super) fn rename_error(
        &self,
        error: io::Error,
        from: &RenameEnd,
        to: &RenameEnd,
    ) -> VaultError {
        if !is_substitution(&error) {
            return VaultError::Io(error);
        }
        for end in [from, to] {
            if let Err(refusal @ VaultError::InvalidPath { .. }) =
                self.walk(parent_of(end.relative), end.original, false)
            {
                return refusal;
            }
        }
        changed_during_rename(from.original)
    }

    #[cfg(target_os = "macos")]
    fn renameatx(&self, from: &RenameEnd, to: &RenameEnd, flags: libc::c_uint) -> io::Result<()> {
        use std::os::fd::AsRawFd as _;
        // SAFETY: the root is an open directory and both paths are
        // NUL-terminated and relative to it; all three outlive the call.
        let result = unsafe {
            libc::renameatx_np(
                self.dir.as_raw_fd(),
                from.relative_c.as_ptr(),
                self.dir.as_raw_fd(),
                to.relative_c.as_ptr(),
                flags,
            )
        };
        os_result(result)
    }

    /// The directory at `parent` beneath the root, reached without following
    /// a link.
    fn pin(
        &self,
        parent: &Path,
        original: &str,
        create_missing: bool,
    ) -> Result<fs::File, VaultError> {
        #[cfg(target_os = "linux")]
        {
            match openat2_beneath(&self.dir, parent, original)? {
                Ok(dir) => return Ok(dir),
                // A destination folder to create, or `openat2` unavailable:
                // the no-follow walk below.
                Err(error) if create_missing && error.kind() == io::ErrorKind::NotFound => {}
                Err(error) if openat2_unavailable(&error) => {}
                // Name the link or non-folder: the walk finds it.
                Err(error)
                    if is_substitution(&error) || error.raw_os_error() == Some(libc::EXDEV) =>
                {
                    return Err(match self.walk(parent, original, false) {
                        Err(refusal @ VaultError::InvalidPath { .. }) => refusal,
                        _ => changed_during_rename(original),
                    });
                }
                Err(error) => return Err(VaultError::Io(error)),
            }
        }
        self.walk(parent, original, create_missing)
    }

    /// Open `parent` one component at a time, each relative to the one
    /// before and with `O_NOFOLLOW`, creating a missing folder in place when
    /// `create_missing`. The same descent `PinnedMutationTarget` uses for
    /// creation; kept separate so the creation and Trash paths stay as they
    /// are until `FsRoot` (#1441) serves both.
    fn walk(
        &self,
        parent: &Path,
        original: &str,
        create_missing: bool,
    ) -> Result<fs::File, VaultError> {
        let mut dir = self.dir.try_clone().map_err(VaultError::Io)?;
        let mut prefix = PathBuf::new();
        for component in parent.components() {
            let Component::Normal(name) = component else {
                return Err(invalid(original, "the path is not vault-relative"));
            };
            prefix.push(name);
            let name = c_string(name.as_bytes(), original)?;
            let next = match open_directory_at(&dir, &name) {
                Ok(next) => next,
                Err(error) if create_missing && error.kind() == io::ErrorKind::NotFound => {
                    match create_directory_at(&dir, &name) {
                        Ok(()) => {}
                        Err(error) if error.kind() == io::ErrorKind::AlreadyExists => {}
                        Err(error) => return Err(VaultError::Io(error)),
                    }
                    open_directory_at(&dir, &name)
                        .map_err(|error| refusal_or_io(&dir, &name, &prefix, original, error))?
                }
                Err(error) => return Err(refusal_or_io(&dir, &name, &prefix, original, error)),
            };
            dir = next;
        }
        Ok(dir)
    }
}

impl RenameEnd<'_> {
    /// Whether an entry, of any kind and without following a final link,
    /// exists at this end now.
    pub(super) fn entry_exists(&self) -> io::Result<bool> {
        match lstat_mode(&self.parent, &self.leaf) {
            Ok(_) => Ok(true),
            Err(error) if error.kind() == io::ErrorKind::NotFound => Ok(false),
            Err(error) => Err(error),
        }
    }
}

fn parent_of(relative: &Path) -> &Path {
    relative.parent().unwrap_or_else(|| Path::new(""))
}

/// `ELOOP` (a link where a folder was expected, or `RENAME_NOFOLLOW_ANY` and
/// `RESOLVE_NO_SYMLINKS` meeting one) or `ENOTDIR` (`O_DIRECTORY` with
/// `O_NOFOLLOW` meeting a link or a file).
fn is_substitution(error: &io::Error) -> bool {
    matches!(
        error.raw_os_error(),
        Some(libc::ELOOP) | Some(libc::ENOTDIR)
    )
}

/// The typed refusal for the folder at `prefix`, or the IO error itself when
/// opening it failed for another reason.
fn refusal_or_io(
    dir: &fs::File,
    name: &CStr,
    prefix: &Path,
    original: &str,
    error: io::Error,
) -> VaultError {
    if !is_substitution(&error) {
        return VaultError::Io(error);
    }
    let folder = prefix.to_string_lossy();
    let reason = match lstat_mode(dir, name) {
        Ok(mode) if (mode & libc::S_IFMT) == libc::S_IFLNK => {
            format!(
                "\"{folder}\" is a symbolic link, not a folder in this vault, so nothing was moved"
            )
        }
        Ok(mode) if (mode & libc::S_IFMT) != libc::S_IFDIR => {
            format!("\"{folder}\" is not a folder, so nothing was moved")
        }
        // It changed back, or vanished, between the open and the look.
        _ => return changed_during_rename(original),
    };
    VaultError::InvalidPath {
        path: original.to_string(),
        reason,
    }
}

fn changed_during_rename(original: &str) -> VaultError {
    invalid(
        original,
        "a folder in this path changed into a symbolic link or a file during the move, so \
         nothing was moved",
    )
}

fn invalid(original: &str, reason: &str) -> VaultError {
    VaultError::InvalidPath {
        path: original.to_string(),
        reason: reason.to_string(),
    }
}

fn c_string(bytes: &[u8], original: &str) -> Result<CString, VaultError> {
    CString::new(bytes).map_err(|_| invalid(original, "the path contains an embedded NUL"))
}

fn lstat_mode(dir: &fs::File, name: &CStr) -> io::Result<libc::mode_t> {
    use std::os::fd::AsRawFd as _;
    let mut stat = std::mem::MaybeUninit::<libc::stat>::uninit();
    // SAFETY: `dir` is an open directory, `name` is NUL-terminated and
    // `stat` has the size and alignment fstatat writes. The leaf is not
    // followed.
    let result = unsafe {
        libc::fstatat(
            dir.as_raw_fd(),
            name.as_ptr(),
            stat.as_mut_ptr(),
            libc::AT_SYMLINK_NOFOLLOW,
        )
    };
    os_result(result)?;
    // SAFETY: a successful fstatat initialized `stat`.
    Ok(unsafe { stat.assume_init() }.st_mode)
}

fn os_result(result: libc::c_int) -> io::Result<()> {
    if result != 0 {
        return Err(io::Error::last_os_error());
    }
    Ok(())
}

/// `openat2(root, parent, O_PATH | O_DIRECTORY, RESOLVE_NO_SYMLINKS |
/// RESOLVE_BENEATH)`. The outer error is a path Slate cannot express; the
/// inner one is the kernel's answer for the caller to classify.
#[cfg(target_os = "linux")]
fn openat2_beneath(
    root: &fs::File,
    parent: &Path,
    original: &str,
) -> Result<io::Result<fs::File>, VaultError> {
    use std::os::fd::{AsRawFd as _, FromRawFd as _};

    let path = if parent.as_os_str().is_empty() {
        c".".to_owned()
    } else {
        c_string(parent.as_os_str().as_bytes(), original)?
    };
    // SAFETY: `open_how` is plain integers; all-zero is its documented
    // default (no mode, no resolve restrictions) before the fields are set.
    let mut how: libc::open_how = unsafe { std::mem::zeroed() };
    how.flags = (libc::O_PATH | libc::O_DIRECTORY | libc::O_CLOEXEC) as u64;
    how.resolve = libc::RESOLVE_NO_SYMLINKS | libc::RESOLVE_BENEATH;
    // SAFETY: the root is an open directory, `path` is NUL-terminated and
    // `how` is a live `open_how` whose size is passed with it.
    let descriptor = unsafe {
        libc::syscall(
            libc::SYS_openat2,
            root.as_raw_fd(),
            path.as_ptr(),
            &how as *const libc::open_how,
            std::mem::size_of::<libc::open_how>(),
        )
    };
    if descriptor < 0 {
        return Ok(Err(io::Error::last_os_error()));
    }
    // SAFETY: a successful openat2 returned a new descriptor we now own.
    Ok(Ok(unsafe {
        fs::File::from_raw_fd(descriptor as std::os::fd::RawFd)
    }))
}

/// `openat2` is not available to this process: the kernel predates it, or
/// a seccomp filter refuses unknown system calls.
#[cfg(target_os = "linux")]
fn openat2_unavailable(error: &io::Error) -> bool {
    matches!(error.raw_os_error(), Some(libc::ENOSYS) | Some(libc::EPERM))
}

#[cfg(all(target_os = "linux", target_env = "gnu"))]
fn renameat2_no_replace(from: &RenameEnd, to: &RenameEnd) -> io::Result<()> {
    use std::os::fd::AsRawFd as _;
    // SAFETY: both parents are open directories and both leaves are
    // NUL-terminated single components that outlive the call.
    let result = unsafe {
        libc::renameat2(
            from.parent.as_raw_fd(),
            from.leaf.as_ptr(),
            to.parent.as_raw_fd(),
            to.leaf.as_ptr(),
            libc::RENAME_NOREPLACE,
        )
    };
    os_result(result)
}

// `libc` has no `renameat2` wrapper on every non-GNU Linux target (notably
// musl); the system call is the same primitive.
#[cfg(all(target_os = "linux", not(target_env = "gnu")))]
fn renameat2_no_replace(from: &RenameEnd, to: &RenameEnd) -> io::Result<()> {
    use std::os::fd::AsRawFd as _;
    // SAFETY: as for the GNU wrapper; the system call takes the same
    // arguments.
    let result = unsafe {
        libc::syscall(
            libc::SYS_renameat2,
            from.parent.as_raw_fd(),
            from.leaf.as_ptr(),
            to.parent.as_raw_fd(),
            to.leaf.as_ptr(),
            libc::RENAME_NOREPLACE,
        )
    };
    if result != 0 {
        return Err(io::Error::last_os_error());
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    /// The local `RENAME_NOFOLLOW_ANY` is the SDK's value and the running
    /// kernel honours it: through a symlinked folder the root-relative
    /// rename fails `ELOOP` and moves nothing, while the same call with
    /// `RENAME_EXCL` alone follows the link (the pre-#1449 behaviour). A
    /// final component that is itself a link is still renamed.
    #[cfg(target_os = "macos")]
    #[test]
    fn renameatx_np_honours_rename_nofollow_any() {
        use std::os::fd::AsRawFd as _;

        assert_eq!(RENAME_NOFOLLOW_ANY, 0x0000_0010);
        let root = tempfile::tempdir().unwrap();
        let elsewhere = tempfile::tempdir().unwrap();
        std::fs::write(root.path().join("c.md"), b"c").unwrap();
        std::os::unix::fs::symlink(elsewhere.path(), root.path().join("sub")).unwrap();
        std::os::unix::fs::symlink("nowhere", root.path().join("dangling.md")).unwrap();
        let pinned = PinnedRoot::open(root.path()).unwrap();
        let rename = |from: &CStr, to: &CStr, flags| {
            // SAFETY: the root descriptor is open; both paths are
            // NUL-terminated literals relative to it.
            os_result(unsafe {
                libc::renameatx_np(
                    pinned.dir.as_raw_fd(),
                    from.as_ptr(),
                    pinned.dir.as_raw_fd(),
                    to.as_ptr(),
                    flags,
                )
            })
        };

        let refused = rename(
            c"c.md",
            c"sub/c.md",
            libc::RENAME_EXCL | RENAME_NOFOLLOW_ANY,
        )
        .unwrap_err();
        assert_eq!(refused.raw_os_error(), Some(libc::ELOOP), "{refused:?}");
        assert!(root.path().join("c.md").is_file());
        assert!(!elsewhere.path().join("c.md").exists());

        rename(c"c.md", c"sub/c.md", libc::RENAME_EXCL).unwrap();
        assert!(
            elsewhere.path().join("c.md").is_file(),
            "EXCL alone follows the link"
        );

        rename(
            c"dangling.md",
            c"moved.md",
            libc::RENAME_EXCL | RENAME_NOFOLLOW_ANY,
        )
        .unwrap();
        assert!(
            root.path()
                .join("moved.md")
                .symlink_metadata()
                .unwrap()
                .file_type()
                .is_symlink()
        );
    }

    /// The fallback pin for kernels without `openat2` refuses a symlinked
    /// folder and a file standing where a folder should be, naming each;
    /// `openat2` itself, where present, refuses the same link.
    #[cfg(target_os = "linux")]
    #[test]
    fn the_no_follow_walk_refuses_what_openat2_refuses() {
        let root = tempfile::tempdir().unwrap();
        let elsewhere = tempfile::tempdir().unwrap();
        std::fs::create_dir(root.path().join("real")).unwrap();
        std::fs::write(root.path().join("real/plain.md"), b"p").unwrap();
        std::os::unix::fs::symlink(elsewhere.path(), root.path().join("real/sub")).unwrap();
        let pinned = PinnedRoot::open(root.path()).unwrap();

        let link = pinned
            .walk(Path::new("real/sub/deeper"), "real/sub/deeper/x.md", true)
            .unwrap_err();
        assert!(
            matches!(&link, VaultError::InvalidPath { path, reason }
                if path == "real/sub/deeper/x.md"
                    && reason.contains("\"real/sub\" is a symbolic link")),
            "{link:?}"
        );
        assert!(!elsewhere.path().join("deeper").exists());
        let file = pinned
            .walk(Path::new("real/plain.md"), "real/plain.md/x.md", false)
            .unwrap_err();
        assert!(
            matches!(&file, VaultError::InvalidPath { reason, .. }
                if reason.contains("\"real/plain.md\" is not a folder")),
            "{file:?}"
        );
        pinned.walk(Path::new("real"), "real/x.md", false).unwrap();

        match openat2_beneath(&pinned.dir, Path::new("real/sub"), "real/sub/x.md").unwrap() {
            Err(error) if openat2_unavailable(&error) => {}
            Err(error) => assert_eq!(error.raw_os_error(), Some(libc::ELOOP), "{error:?}"),
            Ok(dir) => panic!("openat2 followed the link: {dir:?}"),
        }
    }
}
