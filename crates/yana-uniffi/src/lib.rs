//! FFI bindings for `yana-core` via `uniffi-rs`.
//!
//! This crate wraps the pure-Rust `yana-core` API with uniffi annotations
//! so it can be called from Swift (Mac, iOS) and Kotlin (Android) without
//! hand-written FFI glue.
//!
//! Bootstrap stage: only the heading-extraction primitives are exposed.
//! The full FFI surface (`VaultProvider` trait via callback interfaces,
//! `VaultSession`, operation log, query engine, etc.) will land
//! incrementally per `docs/plans/05_locked_architecture_decisions.md`.

use yana_core as core;

uniffi::setup_scaffolding!();

/// A heading parsed from a Markdown document.
///
/// Mirrored from `yana_core::Heading` so that uniffi can derive its
/// foreign-language bindings without coupling the core API surface to
/// uniffi annotations.
#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct Heading {
    pub level: u8,
    pub text: String,
}

impl From<core::Heading> for Heading {
    fn from(h: core::Heading) -> Self {
        Heading {
            level: h.level,
            text: h.text,
        }
    }
}

/// Errors that may be returned across the FFI boundary.
///
/// Mirrors `yana_core::VaultError` with the inner sources flattened into
/// strings so the FFI surface stays simple. Each variant maps 1:1 to a
/// core error variant.
#[derive(Debug, thiserror::Error, uniffi::Error)]
pub enum VaultError {
    #[error("io error: {message}")]
    Io { message: String },

    #[error("database error: {message}")]
    Db { message: String },

    #[error("invalid vault-relative path {path:?}: {reason}")]
    InvalidPath { path: String, reason: String },

    #[error("trash operation failed: {message}")]
    Trash { message: String },

    #[error("operation cancelled")]
    Cancelled,
}

impl From<core::VaultError> for VaultError {
    fn from(e: core::VaultError) -> Self {
        match e {
            core::VaultError::Io(io) => VaultError::Io {
                message: io.to_string(),
            },
            core::VaultError::Db(db) => VaultError::Db {
                message: db.to_string(),
            },
            core::VaultError::InvalidPath { path, reason } => {
                VaultError::InvalidPath { path, reason }
            }
            core::VaultError::Trash { message } => VaultError::Trash { message },
            core::VaultError::Cancelled => VaultError::Cancelled,
        }
    }
}

/// Extract headings from a Markdown source string.
///
/// Exposed as `extractHeadings(source:)` in Swift and `extractHeadings(source)`
/// in Kotlin after binding generation.
#[uniffi::export]
pub fn extract_headings(source: String) -> Vec<Heading> {
    core::extract_headings(&source)
        .into_iter()
        .map(Heading::from)
        .collect()
}

/// Read a Markdown file from disk and return its headings.
///
/// The host platform supplies the absolute path. On sandboxed platforms
/// (iOS, Android) the path must come from a security-scoped resource the
/// host already has permission for. (Full vault-provider abstraction with
/// host-implemented file access lands in a later iteration.)
#[uniffi::export]
pub fn read_headings(path: String) -> Result<Vec<Heading>, VaultError> {
    core::read_headings(&path)
        .map(|hs| hs.into_iter().map(Heading::from).collect())
        .map_err(VaultError::from)
}

// =====================================================================
// VaultSession FFI surface (Milestone A subset)
// =====================================================================

use std::path::PathBuf;
use std::sync::Arc;

/// FFI-exposed vault session. Wraps `yana_core::VaultSession`.
///
/// Constructed via `VaultSession.openFilesystem(rootPath:)` on the
/// foreign side. Acquired sessions are reference-counted; releasing the
/// last reference closes the underlying SQLite cache.
#[derive(uniffi::Object)]
pub struct VaultSession {
    inner: core::VaultSession,
}

#[uniffi::export]
impl VaultSession {
    /// Open or create a vault rooted at `root_path` using the desktop
    /// filesystem-backed provider. The cache database lives at
    /// `<root_path>/.yana/cache.sqlite`.
    #[uniffi::constructor]
    pub fn open_filesystem(root_path: String) -> Result<Arc<Self>, VaultError> {
        let inner = core::VaultSession::from_filesystem(PathBuf::from(root_path))?;
        Ok(Arc::new(Self { inner }))
    }

    /// Walk the vault and index every file into the metadata cache.
    /// Synchronous; callers should dispatch off the UI thread.
    ///
    /// The supplied `cancel` token can be cancelled from another thread
    /// (typically the UI) to abort an in-progress scan. A pre-cancelled
    /// token returns `VaultError::Cancelled` without touching the cache.
    pub fn scan_initial(&self, cancel: Arc<CancelToken>) -> Result<ScanReport, VaultError> {
        let report = self.inner.scan_initial(&cancel.inner)?;
        Ok(report.into())
    }

    /// Return a page of indexed files matching `filter`.
    pub fn list_files(
        &self,
        filter: FileFilter,
        paging: Paging,
    ) -> Result<FileSummaryPage, VaultError> {
        let page = self.inner.list_files(filter.into(), paging.into())?;
        Ok(page.into())
    }
}

/// Cooperative cancellation token exposed to foreign callers.
///
/// Construct one with `CancelToken()`, hand it to a long-running call
/// like `scan_initial`, and call `cancel()` from another thread (or
/// dispatch queue) to abort. The token is reference-counted via
/// `Arc` so the foreground UI and the worker can share the same
/// instance without cloning state.
#[derive(uniffi::Object)]
pub struct CancelToken {
    inner: core::CancelToken,
}

#[uniffi::export]
impl CancelToken {
    #[uniffi::constructor]
    pub fn new() -> Arc<Self> {
        Arc::new(Self {
            inner: core::CancelToken::new(),
        })
    }

    /// Signal cancellation. Subsequent checks inside running operations
    /// (e.g. `scan_initial`) will return `VaultError::Cancelled`.
    pub fn cancel(&self) {
        self.inner.cancel();
    }

    /// Whether cancellation has been signalled. Useful for callers that
    /// want to short-circuit work before invoking an FFI call.
    pub fn is_cancelled(&self) -> bool {
        self.inner.is_cancelled()
    }
}

/// Filter passed to `list_files`.
#[derive(uniffi::Enum)]
pub enum FileFilter {
    All,
    MarkdownOnly,
}

impl From<FileFilter> for core::FileFilter {
    fn from(f: FileFilter) -> Self {
        match f {
            FileFilter::All => core::FileFilter::All,
            FileFilter::MarkdownOnly => core::FileFilter::MarkdownOnly,
        }
    }
}

/// Caller-supplied paging request.
#[derive(uniffi::Record)]
pub struct Paging {
    pub cursor: Option<String>,
    pub limit: u32,
}

impl From<Paging> for core::Paging {
    fn from(p: Paging) -> Self {
        Self {
            cursor: p.cursor,
            limit: p.limit,
        }
    }
}

/// Light-weight per-file row.
#[derive(uniffi::Record)]
pub struct FileSummary {
    pub path: String,
    pub name: String,
    pub mtime_ms: i64,
    pub size_bytes: u64,
    pub is_markdown: bool,
}

impl From<core::FileSummary> for FileSummary {
    fn from(s: core::FileSummary) -> Self {
        Self {
            path: s.path,
            name: s.name,
            mtime_ms: s.mtime_ms,
            size_bytes: s.size_bytes,
            is_markdown: s.is_markdown,
        }
    }
}

/// A page of `FileSummary`. uniffi doesn't take generics, so this is the
/// concrete instantiation of `Page<FileSummary>` for the FFI boundary.
#[derive(uniffi::Record)]
pub struct FileSummaryPage {
    pub items: Vec<FileSummary>,
    pub next_cursor: Option<String>,
    pub total_filtered: u64,
}

impl From<core::Page<core::FileSummary>> for FileSummaryPage {
    fn from(p: core::Page<core::FileSummary>) -> Self {
        Self {
            items: p.items.into_iter().map(Into::into).collect(),
            next_cursor: p.next_cursor,
            total_filtered: p.total_filtered,
        }
    }
}

/// Summary of a scan operation.
#[derive(uniffi::Record)]
pub struct ScanReport {
    pub files_seen: u64,
    pub files_indexed: u64,
    pub files_skipped: u64,
    pub bytes_processed: u64,
    pub errors: Vec<String>,
}

impl From<core::ScanReport> for ScanReport {
    fn from(r: core::ScanReport) -> Self {
        Self {
            files_seen: r.files_seen,
            files_indexed: r.files_indexed,
            files_skipped: r.files_skipped,
            bytes_processed: r.bytes_processed,
            errors: r.errors,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn extract_headings_passes_through_to_core() {
        let headings = extract_headings("# Foo\n## Bar".to_string());
        assert_eq!(headings.len(), 2);
        assert_eq!(headings[0].level, 1);
        assert_eq!(headings[0].text, "Foo");
        assert_eq!(headings[1].level, 2);
        assert_eq!(headings[1].text, "Bar");
    }

    #[test]
    fn read_headings_returns_io_error_for_missing_path() {
        let result = read_headings("/does/not/exist.md".to_string());
        match result {
            Err(VaultError::Io { message }) => {
                assert!(message.contains("No such file") || message.contains("not found"));
            }
            other => panic!("expected Io error, got {other:?}"),
        }
    }

    #[test]
    fn cancel_token_round_trips_through_scan_initial() {
        let tmp = tempfile::tempdir().expect("tempdir");
        std::fs::write(tmp.path().join("a.md"), b"# a").unwrap();

        let session = VaultSession::open_filesystem(tmp.path().to_string_lossy().into_owned())
            .expect("open vault");
        let cancel = CancelToken::new();
        cancel.cancel();
        assert!(cancel.is_cancelled());

        match session.scan_initial(cancel) {
            Err(VaultError::Cancelled) => {}
            Err(other) => panic!("expected Cancelled, got error {other:?}"),
            Ok(_) => panic!("expected Cancelled, scan returned Ok"),
        }
    }

    #[test]
    fn cancel_token_shared_state_visible_to_scan() {
        // Mirrors the host UI pattern: the caller keeps a strong
        // reference (e.g. on a view model) and hands a second reference
        // to the worker, then triggers cancel from the UI side. Both
        // sides see the same flag because uniffi gives back the same
        // Arc<CancelToken>.
        let tmp = tempfile::tempdir().expect("tempdir");
        std::fs::write(tmp.path().join("a.md"), b"# a").unwrap();
        let session = VaultSession::open_filesystem(tmp.path().to_string_lossy().into_owned())
            .expect("open vault");

        let cancel = CancelToken::new();
        let cancel_for_worker = Arc::clone(&cancel);
        cancel.cancel();

        match session.scan_initial(cancel_for_worker) {
            Err(VaultError::Cancelled) => {}
            Err(other) => panic!("expected Cancelled, got error {other:?}"),
            Ok(_) => panic!("expected Cancelled, scan returned Ok"),
        }
    }
}
