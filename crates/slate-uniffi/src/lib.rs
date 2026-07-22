// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! FFI bindings for `slate-core` via `uniffi-rs`.
//!
//! This crate wraps the pure-Rust `slate-core` API with uniffi annotations
//! so it can be called from Swift (Mac, iOS) and Kotlin (Android) without
//! hand-written FFI glue.
//!
//! Bootstrap stage: only the heading-extraction primitives are exposed.
//! The full FFI surface (`VaultProvider` trait via callback interfaces,
//! `VaultSession`, operation log, query engine, etc.) will land
//! incrementally per `docs/plans/05_locked_architecture_decisions.md`.

use slate_core as core;

uniffi::setup_scaffolding!();

/// A heading parsed from a Markdown document.
///
/// Mirrored from `slate_core::Heading` so that uniffi can derive its
/// foreign-language bindings without coupling the core API surface to
/// uniffi annotations.
#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct Heading {
    pub level: u8,
    pub text: String,
    pub ordinal: u32,
    pub anchor_id: String,
    /// Byte offset of the heading's start in the original source
    /// (#431) — lets the UI scroll by position instead of searching
    /// for rendered text that may not match the raw buffer.
    pub byte_offset: u32,
}

impl From<core::Heading> for Heading {
    fn from(h: core::Heading) -> Self {
        Heading {
            level: h.level,
            text: h.text,
            ordinal: h.ordinal,
            anchor_id: h.anchor_id,
            byte_offset: h.byte_offset,
        }
    }
}

/// Errors that may be returned across the FFI boundary.
///
/// Mirrors `slate_core::VaultError` with the inner sources flattened into
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

    #[error("file at {path:?} is not valid UTF-8")]
    InvalidUtf8 { path: String },

    #[error("file at {path:?} is {size} bytes, larger than the configured refuse threshold")]
    FileTooLarge { path: String, size: u64 },

    /// User-supplied query string didn't parse as FTS5 syntax.
    /// Surfaced by `full_text_search` so the UI can render a
    /// "bad query" message without conflating it with a corrupt
    /// cache (which arrives as `Db`).
    #[error("invalid search query: {message}")]
    InvalidQuery { message: String },

    /// Distinct from `Cancelled` so retry logic can stop looping
    /// and so logs separate "user pressed Esc" from "feature not
    /// landed yet" (#93 item 2). Used today for the reserved
    /// `SearchScope::File` and `SearchScope::Tag` paths.
    #[error("operation not supported yet: {feature}")]
    Unsupported { feature: String },

    /// Caller passed an argument that doesn't fit the current vault
    /// state — e.g. a `toggle_task_status` with an out-of-range
    /// ordinal or a multi-character status string. The file is left
    /// untouched.
    #[error("invalid argument: {message}")]
    InvalidArgument { message: String },
    #[error("destination already exists: {path}")]
    DestinationExists { path: String },

    /// Save failed because the on-disk file no longer matches the
    /// `expected_content_hash` the caller supplied. Surfaces the
    /// current state so the host can drive a "Keep mine / Reload
    /// from disk" resolution UI.
    #[error(
        "write conflict: file has been modified since it was read \
         (expected hash {expected_content_hash:?}, current hash {current_content_hash:?})"
    )]
    WriteConflict {
        current_content_hash: String,
        expected_content_hash: String,
        current_mtime_ms: i64,
    },

    /// A version operation refused to serve bytes whose hash doesn't
    /// match the requested version (O-3 #541) — history is corrupt or
    /// inconsistent and the operation failed closed.
    #[error("history for {path:?} is unavailable: {reason}")]
    HistoryUnavailable { path: String, reason: String },

    /// `set_property` / `delete_property` / `rename_property_across_vault`
    /// refused to merge the requested edit into a YAML block that
    /// doesn't parse. The user's broken YAML is left on disk.
    #[error("frontmatter at {path:?} is malformed: {reason}")]
    MalformedFrontmatter { path: String, reason: String },

    /// Bibliography source configured in `.slate/prefs.json` couldn't
    /// be opened (missing, permission denied, IO error). Distinct
    /// from a successful load with parse warnings.
    #[error("bibliography source {path:?} is unreadable: {reason}")]
    BibSourceUnreadable { path: String, reason: String },

    /// CSL style file couldn't be opened OR parsed. Both share the
    /// same UI response ("this style isn't usable") so they collapse
    /// to one FFI variant.
    #[error("CSL style {path:?} is unreadable: {reason}")]
    CslStyleUnreadable { path: String, reason: String },

    /// `.slate/prefs.json` exists but can't be opened OR its JSON
    /// doesn't parse. A missing file is NOT an error.
    #[error("preferences file {path:?} is unreadable: {reason}")]
    PrefsUnreadable { path: String, reason: String },
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
            core::VaultError::InvalidUtf8 { path } => VaultError::InvalidUtf8 { path },
            core::VaultError::FileTooLarge { path, size } => {
                VaultError::FileTooLarge { path, size }
            }
            core::VaultError::InvalidQuery { message } => VaultError::InvalidQuery { message },
            core::VaultError::Unsupported { feature } => VaultError::Unsupported { feature },
            core::VaultError::InvalidArgument { message } => {
                VaultError::InvalidArgument { message }
            }
            core::VaultError::DestinationExists { path } => VaultError::DestinationExists { path },
            core::VaultError::WriteConflict {
                current_content_hash,
                expected_content_hash,
                current_mtime_ms,
            } => VaultError::WriteConflict {
                current_content_hash,
                expected_content_hash,
                current_mtime_ms,
            },
            core::VaultError::HistoryUnavailable { path, reason } => {
                VaultError::HistoryUnavailable { path, reason }
            }
            core::VaultError::MalformedFrontmatter { path, reason } => {
                VaultError::MalformedFrontmatter { path, reason }
            }
            core::VaultError::BibSourceUnreadable { path, reason } => {
                VaultError::BibSourceUnreadable { path, reason }
            }
            core::VaultError::CslStyleUnreadable { path, reason } => {
                VaultError::CslStyleUnreadable { path, reason }
            }
            core::VaultError::PrefsUnreadable { path, reason } => {
                VaultError::PrefsUnreadable { path, reason }
            }
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

/// Parse one canonical frontmatter source string into the same property wire
/// records returned by indexed metadata queries. This is intentionally pure:
/// callers that have just read `NoteParts` can refresh conflict UI from those
/// authoritative bytes without rescanning the vault or trusting a stale index.
#[uniffi::export]
pub fn parse_frontmatter_properties(fm_source: String) -> Vec<Property> {
    let source = core::compose_note(&fm_source, "");
    core::extract_frontmatter(&source)
        .0
        .into_iter()
        .map(Into::into)
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
// Host logging sink (#507)
// =====================================================================

mod host_logging;

/// Install a minimal stderr sink for slate-core's `log` facade diagnostics.
///
/// slate-core routes its non-fatal, degradation-only diagnostics through the
/// [`log`] facade (`log::warn!` / `log::debug!`), which is a **no-op unless a
/// host installs a `log::Log` sink** (#507). This is that sink for the desktop
/// host: it writes each record to `stderr`, at `warn` level by default, or
/// `debug` level when `verbose` is `true`.
///
/// The privacy split is deliberate. slate-core keeps vault-relative paths and
/// note names off `warn`-level messages and emits them only on `debug` lines,
/// so a default (`verbose: false`) install never surfaces a note title in host
/// stderr / macOS unified logging. A developer who opts into `verbose: true`
/// (e.g. a debug build) gets the paths back.
///
/// **Idempotent.** `log::set_logger` can only succeed once per process; a
/// second call — or a race with another installer — is swallowed, and the
/// `verbose` argument of the first successful call wins. Safe to call
/// unconditionally at app startup.
///
/// An `os_log` bridge (routing through the macOS unified logging system with
/// its own subsystem/category and privacy qualifiers instead of raw stderr) is
/// explicitly deferred; see #507. Raw stderr is enough for the current
/// bring-up and keeps the sink dependency-free.
///
/// Exposed as `initHostLogging(verbose:)` in Swift.
#[uniffi::export]
pub fn init_host_logging(verbose: bool) {
    host_logging::init(verbose);
}

/// Census support (w0_spec §W0-3 item 2, #715): deterministically raise
/// any `VaultError` arm so foreign-binding censuses can prove every
/// discriminant's mapping end-to-end — native raise → typed foreign
/// exception with structured fields — instead of trusting generated-code
/// inspection. Field values are fixed by contract; censuses assert them
/// exactly. An unknown arm name returns `Ok(())`, keeping the function
/// inert outside census use. Not a product surface.
#[uniffi::export]
pub fn census_synthesize_vault_error(arm: String) -> Result<(), VaultError> {
    Err(match arm.as_str() {
        "Io" => VaultError::Io {
            message: "census io".into(),
        },
        "Db" => VaultError::Db {
            message: "census db".into(),
        },
        "InvalidPath" => VaultError::InvalidPath {
            path: "census/path.md".into(),
            reason: "census reason".into(),
        },
        "Trash" => VaultError::Trash {
            message: "census trash".into(),
        },
        "Cancelled" => VaultError::Cancelled,
        "InvalidUtf8" => VaultError::InvalidUtf8 {
            path: "census/utf8.md".into(),
        },
        "FileTooLarge" => VaultError::FileTooLarge {
            path: "census/large.md".into(),
            size: 42,
        },
        "InvalidQuery" => VaultError::InvalidQuery {
            message: "census query".into(),
        },
        "Unsupported" => VaultError::Unsupported {
            feature: "census feature".into(),
        },
        "InvalidArgument" => VaultError::InvalidArgument {
            message: "census argument".into(),
        },
        "DestinationExists" => VaultError::DestinationExists {
            path: "census/dest.md".into(),
        },
        "WriteConflict" => VaultError::WriteConflict {
            current_content_hash: "census-current".into(),
            expected_content_hash: "census-expected".into(),
            current_mtime_ms: 42,
        },
        "HistoryUnavailable" => VaultError::HistoryUnavailable {
            path: "census/history.md".into(),
            reason: "census reason".into(),
        },
        "MalformedFrontmatter" => VaultError::MalformedFrontmatter {
            path: "census/frontmatter.md".into(),
            reason: "census reason".into(),
        },
        "BibSourceUnreadable" => VaultError::BibSourceUnreadable {
            path: "census/bib.json".into(),
            reason: "census reason".into(),
        },
        "CslStyleUnreadable" => VaultError::CslStyleUnreadable {
            path: "census/style.csl".into(),
            reason: "census reason".into(),
        },
        "PrefsUnreadable" => VaultError::PrefsUnreadable {
            path: "census/prefs.json".into(),
            reason: "census reason".into(),
        },
        _ => return Ok(()),
    })
}

/// Census live-object counters (w0_spec §W0-3 item 2, #715): every
/// `uniffi::Object` carries an RAII [`census_live::Marker`] that counts
/// it live from construction until its `Drop` — i.e. until the last
/// foreign reference is released and the native allocation actually
/// freed. [`census_live_object_counts`] exposes the counters so binding
/// censuses can assert native release (a collected foreign wrapper with
/// a broken finalizer would leave the counter high), not merely managed
/// collection. Not a product surface.
mod census_live {
    use std::sync::atomic::{AtomicI64, Ordering};

    pub static SESSIONS: AtomicI64 = AtomicI64::new(0);
    pub static BUFFERS: AtomicI64 = AtomicI64::new(0);
    pub static CANCEL_TOKENS: AtomicI64 = AtomicI64::new(0);
    pub static REGISTRIES: AtomicI64 = AtomicI64::new(0);
    pub static LAYOUT_SESSIONS: AtomicI64 = AtomicI64::new(0);

    /// RAII marker: one live native object of its kind.
    pub struct Marker(&'static AtomicI64);

    impl Marker {
        pub fn count(counter: &'static AtomicI64) -> Self {
            counter.fetch_add(1, Ordering::AcqRel);
            Marker(counter)
        }
    }

    impl Drop for Marker {
        fn drop(&mut self) {
            self.0.fetch_sub(1, Ordering::AcqRel);
        }
    }
}

/// Live native object counts per bound `uniffi::Object` type.
#[derive(uniffi::Record)]
pub struct LiveObjectCounts {
    pub sessions: i64,
    pub buffers: i64,
    pub cancel_tokens: i64,
    pub registries: i64,
    pub layout_sessions: i64,
}

/// Snapshot the census live-object counters (see [`census_live`]).
#[uniffi::export]
pub fn census_live_object_counts() -> LiveObjectCounts {
    use std::sync::atomic::Ordering;
    LiveObjectCounts {
        sessions: census_live::SESSIONS.load(Ordering::Acquire),
        buffers: census_live::BUFFERS.load(Ordering::Acquire),
        cancel_tokens: census_live::CANCEL_TOKENS.load(Ordering::Acquire),
        registries: census_live::REGISTRIES.load(Ordering::Acquire),
        layout_sessions: census_live::LAYOUT_SESSIONS.load(Ordering::Acquire),
    }
}

// =====================================================================
// VaultSession FFI surface (Milestone A subset)
// =====================================================================

use std::path::PathBuf;
use std::sync::Arc;

/// Allowlist for `toggle_task_status`'s `new_status_char` argument.
///
/// **Printable ASCII (0x20..=0x7E)** minus `[` and `]` (would
/// unbalance the bracket pair) and any whitespace control codes
/// that aren't space. Tabs / newlines / carriage returns would
/// split the task line into two and corrupt the on-disk file —
/// the file would re-parse with the task gone (see red-team L4
/// probe results: `"\n"` rewrites `- [\n] body` which the line
/// scanner then loses entirely).
///
/// The space character (0x20) is explicitly allowed because it's
/// the canonical "unchecked" status. The remaining excluded ASCII
/// — control chars (0x00..=0x1F, 0x7F) — aren't 0x20..=0x7E so
/// they're already rejected by the range check.
fn is_allowed_status_char(c: char) -> bool {
    let b = c as u32;
    (0x20..=0x7E).contains(&b) && c != '[' && c != ']' && c != '\t' // 0x09 — already outside the range, but the
    // intent here is to be explicit about WHY
    // it's rejected so a future widening of the
    // range doesn't accidentally re-admit it.
}

/// Physical `(device, inode)` identity of the vault root as observed
/// by one metadata call inside a session's own open (FL-06 round-27).
/// Hosts compare their per-surface root observations against this
/// anchor so a path swapped A→B→A around the open cannot bind surfaces
/// to different vaults. Nil when the platform cannot observe one;
/// hosts fail closed.
#[derive(uniffi::Record)]
pub struct VaultRootIdentity {
    pub device: u64,
    pub inode: u64,
}

/// FL4-1 (#662): one host-validated half-open UTC window for a required
/// date term.
#[derive(uniffi::Record)]
pub struct SidebarFilterDateWindow {
    pub term: String,
    pub start_ms: i64,
    pub end_ms: i64,
}

/// FL4-1 result page: shared file summaries plus the normative
/// pre-rendered VoiceOver summary.
#[derive(uniffi::Record)]
pub struct SidebarFilterPage {
    pub files: Vec<FileSummary>,
    pub next_cursor: Option<String>,
    pub total: u64,
    pub audio_summary: String,
}

/// FL5-1 (#664): one pre-order entry of the nested tag tree. The core
/// tree is recursive; the FFI carries a flattened pre-order projection
/// (records cannot self-reference across the bindings) — `depth`
/// reconstructs the hierarchy exactly, and pre-order matches the
/// rendered outline order.
#[derive(uniffi::Record)]
pub struct TagTreeEntry {
    /// Display segment, e.g. `reading`.
    pub segment: String,
    /// Normalized full tag, e.g. `projects/reading`.
    pub full: String,
    /// Distinct files with this tag OR any descendant.
    pub file_count: u32,
    /// Distinct files with exactly this tag.
    pub direct_count: u32,
    /// 0 for roots; parent is the nearest prior entry with depth-1.
    pub depth: u32,
}

/// FL5-1 tag tree: flattened entries plus untagged count and the
/// normative pre-rendered summary.
#[derive(uniffi::Record)]
pub struct TagTree {
    pub entries: Vec<TagTreeEntry>,
    pub untagged_count: u32,
    pub audio_summary: String,
}

/// FL5-3b (#666): one tag carried by a file selection, with its
/// distinct-file count — the Remove Tag editor's choice row.
#[derive(uniffi::Record)]
pub struct TagCount {
    pub tag: String,
    pub file_count: u32,
}

/// FL5-3a (refs #666): one refused file in a batch tag edit.
#[derive(uniffi::Record)]
pub struct SkippedFile {
    pub path: String,
    pub reason: String,
}

/// FL5-3a batch tag-edit report: `inline_remainder` counts processed
/// files still carrying the tag inline after a remove (adds report 0).
#[derive(uniffi::Record)]
pub struct TagEditReport {
    pub changed: u32,
    pub skipped: Vec<SkippedFile>,
    pub inline_remainder: u32,
    pub audio_summary: String,
}

impl From<core::TagEditReport> for TagEditReport {
    fn from(report: core::TagEditReport) -> Self {
        TagEditReport {
            changed: report.changed,
            skipped: report
                .skipped
                .into_iter()
                .map(|skip| SkippedFile {
                    path: skip.path,
                    reason: skip.reason,
                })
                .collect(),
            inline_remainder: report.inline_remainder,
            audio_summary: report.audio_summary,
        }
    }
}

fn flatten_tag_tree(tree: core::TagTree) -> TagTree {
    fn walk(node: core::TagTreeNode, depth: u32, out: &mut Vec<TagTreeEntry>) {
        out.push(TagTreeEntry {
            segment: node.segment,
            full: node.full,
            file_count: node.file_count,
            direct_count: node.direct_count,
            depth,
        });
        for child in node.children {
            walk(child, depth + 1, out);
        }
    }
    let mut entries = Vec::new();
    for root in tree.roots {
        walk(root, 0, &mut entries);
    }
    TagTree {
        entries,
        untagged_count: tree.untagged_count,
        audio_summary: tree.audio_summary,
    }
}

/// FFI-exposed vault session. Wraps `slate_core::VaultSession`.
///
/// Constructed via `VaultSession.openFilesystem(rootPath:)` on the
/// foreign side. Acquired sessions are reference-counted; releasing the
/// last reference closes the underlying SQLite cache.
#[derive(uniffi::Object)]
pub struct VaultSession {
    inner: core::VaultSession,
    _census: census_live::Marker,
}

#[uniffi::export]
impl VaultSession {
    /// Open or create a vault rooted at `root_path` using the desktop
    /// filesystem-backed provider. The cache database lives at
    /// `<root_path>/.slate/cache.sqlite`.
    #[uniffi::constructor]
    pub fn open_filesystem(root_path: String) -> Result<Arc<Self>, VaultError> {
        let inner = core::VaultSession::from_filesystem(PathBuf::from(root_path))?;
        Ok(Arc::new(Self {
            inner,
            _census: census_live::Marker::count(&census_live::SESSIONS),
        }))
    }

    /// The physical root identity observed inside this session's open,
    /// or nil when the platform cannot observe one. Hosts compare their
    /// own per-surface root observations (e.g. the sidebar preference
    /// store's descriptor-bound fstat) against this anchor and refuse
    /// to run surfaces that cannot prove they share it.
    pub fn root_identity(&self) -> Option<VaultRootIdentity> {
        self.inner.root_identity().map(|id| VaultRootIdentity {
            device: id.device,
            inode: id.inode,
        })
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

    /// FL4-1 (#662): the canonical unique date-term requirements for a
    /// sidebar filter query, first-occurrence order. Parse errors carry
    /// the offending term through `VaultError::InvalidQuery`.
    pub fn sidebar_filter_date_requirements(
        &self,
        query: String,
    ) -> Result<Vec<String>, VaultError> {
        self.inner
            .sidebar_filter_date_requirements(&query)
            .map_err(Into::into)
    }

    /// FL4-1 (#662): one deterministic parameterized filter/scoped-listing
    /// statement per page. Empty queries are valid only with a normalized
    /// vault-contained `scope_dir`; date terms need exactly one validated
    /// half-open UTC window each.
    pub fn filter_files(
        &self,
        query: String,
        scope_dir: Option<String>,
        scope_tag: Option<String>,
        date_windows: Vec<SidebarFilterDateWindow>,
        paging: Paging,
    ) -> Result<SidebarFilterPage, VaultError> {
        let windows: Vec<core::SidebarFilterDateWindow> = date_windows
            .into_iter()
            .map(|window| core::SidebarFilterDateWindow {
                term: window.term,
                start_ms: window.start_ms,
                end_ms: window.end_ms,
            })
            .collect();
        let page = self.inner.filter_files(
            &query,
            scope_dir.as_deref(),
            scope_tag.as_deref(),
            &windows,
            paging.into(),
        )?;
        Ok(SidebarFilterPage {
            files: page.files.into_iter().map(Into::into).collect(),
            next_cursor: page.next_cursor,
            total: page.total,
            audio_summary: page.audio_summary,
        })
    }

    /// FL5-1 (#664): the nested tag tree over the honest tag dimension
    /// (inline + frontmatter), flattened pre-order for the bindings.
    pub fn tag_tree(&self) -> Result<TagTree, VaultError> {
        Ok(flatten_tag_tree(self.inner.tag_tree()?))
    }

    /// FL5-2 (#665): the reserved Untagged scope through the one shared
    /// filter execution pipeline (identical ordering, pagination, and
    /// summary semantics).
    pub fn untagged_files(&self, paging: Paging) -> Result<SidebarFilterPage, VaultError> {
        let page = self.inner.untagged_files(paging.into())?;
        Ok(SidebarFilterPage {
            files: page.files.into_iter().map(Into::into).collect(),
            next_cursor: page.next_cursor,
            total: page.total,
            audio_summary: page.audio_summary,
        })
    }

    /// FL5-3b (#666): the distinct tags carried by the given files with
    /// distinct-file counts, alphabetical.
    pub fn tags_for_files(&self, paths: Vec<String>) -> Result<Vec<TagCount>, VaultError> {
        Ok(self
            .inner
            .tags_for_files(paths)?
            .into_iter()
            .map(|count| TagCount {
                tag: count.tag,
                file_count: count.file_count,
            })
            .collect())
    }

    /// FL5-3a (refs #666): add a tag to each file's frontmatter
    /// `tags:` list through the shared serializer; conflict-checked per
    /// file, one report for the whole batch.
    pub fn add_tag_to_files(
        &self,
        paths: Vec<String>,
        tag: String,
    ) -> Result<TagEditReport, VaultError> {
        Ok(self.inner.add_tag_to_files(paths, tag)?.into())
    }

    /// FL5-3a (refs #666): remove a tag (normalized compare) from each
    /// file's frontmatter `tags:` list only; inline occurrences are
    /// counted into `inline_remainder`, never edited.
    pub fn remove_tag_from_files(
        &self,
        paths: Vec<String>,
        tag: String,
    ) -> Result<TagEditReport, VaultError> {
        Ok(self.inner.remove_tag_from_files(paths, tag)?.into())
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

    /// Return one indexed file's enriched sidebar summary by exact
    /// vault-relative path, or `None` when the file is not indexed.
    pub fn get_file_summary(&self, path: String) -> Result<Option<FileSummary>, VaultError> {
        Ok(self.inner.get_file_summary(&path)?.map(Into::into))
    }

    /// Return a complete resolver-correct wikilink for a live indexed
    /// Markdown file, or `None` when the target cannot round-trip safely.
    pub fn wikilink_for_path(&self, path: String) -> Result<Option<String>, VaultError> {
        Ok(self.inner.wikilink_for_path(&path)?)
    }

    pub fn list_tags(&self) -> Result<Vec<String>, VaultError> {
        Ok(self.inner.list_tags()?)
    }

    /// List one level of the file tree: `parent_path`'s child directories
    /// (each with immediate child-dir / child-file counts) then a page of
    /// its child files. `parent_path = ""` lists the root. Directories
    /// come first, then files, each sorted case-insensitively (#459).
    /// U2-2 (#460): structural mutations. Semantics in
    /// `docs/plans/08_ui_parity/specs/u2_spec.md` §U2-2.
    pub fn create_folder(&self, path: String) -> Result<StructuralReport, VaultError> {
        Ok(self.inner.create_folder(&path)?.into())
    }

    /// Create one import destination directory without merging an occupant or
    /// adding a standalone structural-history entry.
    pub fn create_folder_exclusive(&self, path: String) -> Result<(), VaultError> {
        Ok(self.inner.create_folder_exclusive(&path)?)
    }

    pub fn rename_folder(
        &self,
        path: String,
        new_name: String,
    ) -> Result<StructuralReport, VaultError> {
        Ok(self.inner.rename_folder(&path, &new_name)?.into())
    }

    /// FL6-1 (#667): rename a folder AND its `<Folder>/<Folder>.md`
    /// note as one core compound operation — complete preflight,
    /// rollback on second-step failure (reported honestly; the pair is
    /// not crash-atomic), one merged report mapping the note
    /// original → final.
    pub fn rename_folder_with_note(
        &self,
        path: String,
        new_name: String,
    ) -> Result<StructuralReport, VaultError> {
        Ok(self.inner.rename_folder_with_note(&path, &new_name)?.into())
    }

    pub fn move_folder(
        &self,
        path: String,
        new_parent: String,
    ) -> Result<StructuralReport, VaultError> {
        Ok(self.inner.move_folder(&path, &new_parent)?.into())
    }

    pub fn delete_folder(&self, path: String) -> Result<(), VaultError> {
        Ok(self.inner.delete_folder(&path)?)
    }

    pub fn rename_file(
        &self,
        path: String,
        new_name: String,
    ) -> Result<StructuralReport, VaultError> {
        Ok(self.inner.rename_file(&path, &new_name)?.into())
    }

    pub fn move_file(
        &self,
        path: String,
        new_parent: String,
    ) -> Result<StructuralReport, VaultError> {
        Ok(self.inner.move_file(&path, &new_parent)?.into())
    }

    pub fn delete_file(&self, path: String) -> Result<(), VaultError> {
        Ok(self.inner.delete_file(&path)?)
    }

    pub fn undo_structural(&self, op_id: i64) -> Result<StructuralReport, VaultError> {
        Ok(self.inner.undo_structural(op_id)?.into())
    }

    pub fn batch_move(&self, request: BatchMoveRequest) -> Result<BatchMoveReport, VaultError> {
        Ok(self.inner.batch_move(request.into())?.into())
    }

    pub fn batch_trash(&self, request: BatchTrashRequest) -> Result<BatchTrashReport, VaultError> {
        Ok(self.inner.batch_trash(request.into())?.into())
    }

    pub fn undo_batch_move(&self, op_id: i64) -> Result<BatchMoveReport, VaultError> {
        Ok(self.inner.undo_batch_move(op_id)?.into())
    }

    pub fn list_dir_children(
        &self,
        parent_path: String,
        paging: Paging,
    ) -> Result<DirListing, VaultError> {
        let listing = self.inner.list_dir_children(&parent_path, paging.into())?;
        Ok(listing.into())
    }

    /// Fetch full per-file metadata (basic columns + headings).
    ///
    /// Returns `nil` if the path isn't in the index yet — call
    /// `scan_initial` first, or pass a path the scanner has visited.
    pub fn get_file_metadata(&self, path: String) -> Result<Option<FileMetadata>, VaultError> {
        Ok(self.inner.get_file_metadata(&path)?.map(Into::into))
    }

    /// Read the given vault file's bytes as UTF-8 text.
    ///
    /// Refuses files larger than the configured large-file refuse
    /// threshold with `FileTooLarge` (no IO on the file itself), and
    /// surfaces non-UTF-8 content as `InvalidUtf8` rather than
    /// silently substituting replacement characters.
    pub fn read_text(&self, path: String) -> Result<String, VaultError> {
        Ok(self.inner.read_text(&path)?)
    }

    /// Save UTF-8 text to a vault path, refresh the index, and append a
    /// fine-grained `EditBatch` (or a `WholeFileReplace` snapshot) to the
    /// file's op-log (#378).
    ///
    /// Pass `expected_content_hash = Some(hash)` to detect external
    /// changes between read and save: if the on-disk file no longer
    /// matches `hash`, the call returns `WriteConflict` and leaves the
    /// file untouched so the UI can drive a "Keep mine / Reload from
    /// disk" resolution. Pass `None` for an unconditional save (the
    /// CLI path).
    pub fn save_text(
        &self,
        path: String,
        contents: String,
        expected_content_hash: Option<String>,
    ) -> Result<SaveReport, VaultError> {
        let report = self
            .inner
            .save_text(&path, &contents, expected_content_hash.as_deref())?;
        Ok(report.into())
    }

    /// Read a note split into `{ fm_source, body }` plus the whole-file
    /// content hash and mtime — the U3 tab-open call (#469, U3-5).
    ///
    /// The body-only editor buffer is populated from `body`; the
    /// properties widget from `fm_source`. The hash is over the WHOLE
    /// file, so a later `save_composed` conflict-detects an external edit
    /// to either half. One read, one hash.
    pub fn read_note_parts(&self, path: String) -> Result<NotePartsBundle, VaultError> {
        Ok(self.inner.read_note_parts(&path)?.into())
    }

    /// Compose `fm_source ⊕ body` and save through the same machinery as
    /// `save_text` — conflict detection, atomic write, index refresh,
    /// op-log append (#469, U3-5).
    ///
    /// The body-only editor's save: the widget hands the current
    /// frontmatter source, the editor hands the body.
    /// `expected_content_hash` is the whole-file hash from
    /// `read_note_parts` (or a prior `SaveReport`), so an external edit to
    /// either half since the read is caught as `WriteConflict`. Empty
    /// `fm_source` writes the body with no `---` block.
    pub fn save_composed(
        &self,
        path: String,
        fm_source: String,
        body: String,
        expected_content_hash: Option<String>,
    ) -> Result<SaveReport, VaultError> {
        let report = self
            .inner
            .save_composed(&path, &fm_source, &body, expected_content_hash)?;
        Ok(report.into())
    }

    /// Replace a note's frontmatter source wholesale — the U3-4
    /// show-source YAML commit path.
    ///
    /// `fm_source` must be empty or parse as a YAML mapping, else
    /// `MalformedFrontmatter` is returned with a line/column message and
    /// nothing is written (the UI keeps the user's draft). On success the
    /// current body is read fresh and recomposed with the new
    /// frontmatter, so an in-flight body edit isn't clobbered. Unlike
    /// `set_property`, the frontmatter is stored verbatim — comments and
    /// formatting survive.
    pub fn set_frontmatter_source(
        &self,
        path: String,
        fm_source: String,
        expected_content_hash: Option<String>,
    ) -> Result<SaveReport, VaultError> {
        let report = self
            .inner
            .set_frontmatter_source(&path, &fm_source, expected_content_hash)?;
        Ok(report.into())
    }

    /// Return every well-formed op-log entry recorded for `path`.
    ///
    /// Empty result if the path isn't indexed yet or has never been
    /// saved. A torn trailing entry is silently dropped and the
    /// well-formed prefix is returned.
    pub fn read_oplog(&self, path: String) -> Result<Vec<OpLogEntry>, VaultError> {
        Ok(self
            .inner
            .read_oplog(&path)?
            .into_iter()
            .map(OpLogEntry::from)
            .collect())
    }

    /// Walk + index the vault while emitting incremental progress
    /// events to the supplied listener. The listener always sees
    /// `Started`, one `FileIndexed` per file, and exactly one
    /// terminal event (`Finished` or `Cancelled`).
    pub fn scan_initial_with_progress(
        &self,
        cancel: Arc<CancelToken>,
        listener: Arc<dyn ScanProgressListener>,
    ) -> Result<ScanReport, VaultError> {
        let adapter: Arc<dyn core::ScanProgressListener> =
            Arc::new(ScanProgressListenerAdapter { foreign: listener });
        let report = self
            .inner
            .scan_initial_with_progress(&cancel.inner, Some(adapter))?;
        Ok(report.into())
    }

    /// Register a session-event listener (O-2 #540). Returns an opaque
    /// token for `unregister_event_listener`. Events arrive on
    /// background worker threads — marshal to the main actor inside
    /// the listener.
    pub fn register_event_listener(&self, listener: Arc<dyn VaultEventListener>) -> u64 {
        let adapter: Arc<dyn core::VaultEventListener> =
            Arc::new(VaultEventListenerAdapter { foreign: listener });
        self.inner.register_event_listener(adapter)
    }

    /// Remove a previously registered session-event listener. Unknown
    /// tokens are a no-op.
    pub fn unregister_event_listener(&self, token: u64) {
        self.inner.unregister_event_listener(token);
    }

    /// The live history prefs (O-5 #543) — always what the session is
    /// actually enforcing.
    pub fn history_prefs(&self) -> HistoryPrefs {
        self.inner.history_prefs().into()
    }

    /// Persist (`.slate/prefs.json`, unknown keys preserved) AND
    /// live-apply history prefs (O-5 #543). `retention_days == 0` and
    /// an unparseable existing prefs file are typed errors; on any
    /// error the running session keeps its previous setting.
    pub fn set_history_prefs(&self, prefs: HistoryPrefs) -> Result<(), VaultError> {
        Ok(self.inner.set_history_prefs(prefs.into())?)
    }

    /// Page through a file's version history, newest first (O-3
    /// #541). A compaction between pages invalidates the cursor with
    /// `InvalidArgument("history changed, restart paging")` — reload
    /// page one.
    pub fn list_versions(
        &self,
        path: String,
        paging: Paging,
    ) -> Result<VersionSummaryPage, VaultError> {
        Ok(self.inner.list_versions(&path, paging.into())?.into())
    }

    /// The exact bytes of one version, integrity-verified — wrong
    /// bytes are never served (`HistoryUnavailable` instead).
    pub fn version_content(
        &self,
        path: String,
        version_hash: String,
    ) -> Result<String, VaultError> {
        Ok(self.inner.version_content(&path, &version_hash)?)
    }

    /// Restore a (verified) version through the standard save
    /// machinery — conflict-detected, atomic, itself versioned.
    /// History is never rewritten.
    pub fn restore_version(
        &self,
        path: String,
        version_hash: String,
        expected_content_hash: Option<String>,
    ) -> Result<SaveReport, VaultError> {
        Ok(self
            .inner
            .restore_version(&path, &version_hash, expected_content_hash.as_deref())?
            .into())
    }

    /// The recoverable deleted files (newest per path, journal
    /// timestamps where known, `deleted_at_ms` descending).
    pub fn list_deleted_files(&self, paging: Paging) -> Result<DeletedFilePage, VaultError> {
        Ok(self.inner.list_deleted_files(paging.into())?.into())
    }

    /// Recover a deleted file to its pre-deletion bytes; the recovered
    /// file keeps its history. Occupied destination →
    /// `DestinationExists`, nothing written.
    pub fn recover_deleted_file(&self, path: String) -> Result<SaveReport, VaultError> {
        Ok(self.inner.recover_deleted_file(&path)?.into())
    }

    /// Create-if-absent write: existing destination (on disk or in the
    /// index) → `DestinationExists`; else the standard save machinery.
    /// Restore As… (#795): recover a deleted file's tail content to
    /// a CALLER-CHOSEN destination (no-clobber; the remnant log
    /// re-binds to the new path, history following the file).
    pub fn recover_deleted_file_as(
        &self,
        path: String,
        destination: String,
    ) -> Result<SaveReport, VaultError> {
        Ok(self
            .inner
            .recover_deleted_file_as(&path, &destination)?
            .into())
    }

    pub fn create_exclusive(
        &self,
        path: String,
        content: String,
    ) -> Result<SaveReport, VaultError> {
        Ok(self.inner.create_exclusive(&path, &content)?.into())
    }

    /// Create-if-absent BYTES write (#910): the binary / non-UTF-8 sibling
    /// of `create_exclusive`. Same no-clobber contract — an occupied
    /// destination (on disk or in the case-insensitive index) is
    /// `DestinationExists`, nothing written — so a host's file-drop import
    /// funnel can copy an image / PDF / arbitrary binary byte-for-byte
    /// with the identical collision surface the text path uses.
    pub fn create_exclusive_bytes(
        &self,
        path: String,
        bytes: Vec<u8>,
    ) -> Result<SaveReport, VaultError> {
        Ok(self.inner.create_exclusive_bytes(&path, &bytes)?.into())
    }

    /// The byte ceiling above which a write is refused (`FileTooLarge`).
    /// A host reads this to PRE-CHECK a dropped file's size and refuse an
    /// oversized import gracefully BEFORE reading it into memory and
    /// lowering it across the FFI — where a >2 GiB `Data`/`String` would
    /// otherwise trap in the generated `Int32(count)` converter (#910).
    pub fn large_file_refuse_bytes(&self) -> u64 {
        self.inner.large_file_refuse_bytes()
    }

    /// Structured diff between two (verified) versions (O-4 #542) —
    /// named operations in document order, never a side-by-side dump.
    pub fn diff_versions(
        &self,
        path: String,
        from_hash: String,
        to_hash: String,
    ) -> Result<StructuredDiff, VaultError> {
        Ok(self
            .inner
            .diff_versions(&path, &from_hash, &to_hash)?
            .into())
    }

    /// What changed since the last recorded open. Hosts MUST call this
    /// BEFORE `mark_opened` (the pinned funnel order — marking first
    /// always reports Unchanged).
    pub fn changes_since_last_open(&self, path: String) -> Result<ChangesSinceOpen, VaultError> {
        Ok(self.inner.changes_since_last_open(&path)?.into())
    }

    /// Record "opened now, at this content" into `open_marks`.
    pub fn mark_opened(&self, path: String) -> Result<(), VaultError> {
        self.inner.mark_opened(&path)?;
        Ok(())
    }

    /// All outgoing links from `path` in document order, including
    /// resolved (internal-and-found), unresolved (internal-and-missing),
    /// and external links. UI uses `kind` + `is_external` +
    /// `is_unresolved` to render each in its own style.
    pub fn outgoing_links(&self, path: String) -> Result<Vec<OutgoingLink>, VaultError> {
        Ok(self
            .inner
            .outgoing_links(&path)?
            .into_iter()
            .map(Into::into)
            .collect())
    }

    /// Paged inbound-link query: every file that links TO `path`,
    /// with a cached ±60-char snippet. External links never appear
    /// here.
    pub fn backlinks(&self, path: String, paging: Paging) -> Result<BacklinkPage, VaultError> {
        let page = self.inner.backlinks(&path, paging.into())?;
        Ok(page.into())
    }

    /// Bundle of backlinks + outgoing links + properties for `path`,
    /// fetched under a single mutex acquisition. The host UI's note-
    /// load handler should prefer this over three separate calls
    /// (#92 item 4) — same total work, one contiguous lock-hold
    /// instead of three races against the scanner transaction.
    pub fn note_load_bundle(
        &self,
        path: String,
        backlinks_paging: Paging,
    ) -> Result<NoteLoadBundle, VaultError> {
        let bundle = self
            .inner
            .note_load_bundle(&path, backlinks_paging.into())?;
        Ok(bundle.into())
    }

    /// Paged vault-wide audit of unresolved internal links.
    pub fn list_unresolved_links(&self, paging: Paging) -> Result<UnresolvedLinkPage, VaultError> {
        let page = self.inner.list_unresolved_links(paging.into())?;
        Ok(page.into())
    }

    /// Filtered whole-graph projection (Milestone P #552). Sync —
    /// the host dispatches off-main per the AppState pattern.
    pub fn graph_snapshot(&self, filter: GraphFilter) -> Result<GraphSnapshot, VaultError> {
        Ok(self.inner.graph_snapshot(filter.into())?.into())
    }

    /// Depth-limited (1..=3, clamped) undirected neighborhood of one
    /// note (#552). The filter applies before traversal.
    pub fn graph_neighborhood(
        &self,
        path: String,
        depth: u32,
        filter: GraphFilter,
    ) -> Result<GraphNeighborhood, VaultError> {
        Ok(self
            .inner
            .graph_neighborhood(&path, depth, filter.into())?
            .into())
    }

    /// Cheap graph-change probe (#552): bumps once per applied
    /// mutation batch; 0 until the first graph query builds the
    /// index. The refresh contract: re-query on VaultEventListener
    /// file-change / scan-finished events, refresh surfaces on change.
    pub fn graph_generation(&self) -> u64 {
        self.inner.graph_generation()
    }

    /// Snapshot the current graph under `filter` and seed a
    /// [`LayoutSession`] (#558). The session keeps the force-directed
    /// layout state pointer-side; only flat position buffers cross the
    /// FFI. All of its methods are off-main-callable — the host drives
    /// ticks from a background task. Builds the graph index on first use.
    pub fn start_graph_layout(
        self: Arc<Self>,
        filter: GraphFilter,
        forces: LayoutForces,
        config: LayoutConfig,
    ) -> Result<Arc<LayoutSession>, VaultError> {
        let core_filter: core::graph::GraphFilter = filter.into();
        let core_config: core::graph_layout::LayoutConfig = config.into();
        let (engine, topology) =
            self.inner
                .start_layout(core_filter, forces.into(), core_config)?;
        let state = LayoutState::new(engine, topology, core_config.max_iterations);
        Ok(Arc::new(LayoutSession {
            session: Arc::clone(&self),
            filter: core_filter,
            state: std::sync::Mutex::new(state),
            _census: census_live::Marker::count(&census_live::LAYOUT_SESSIONS),
        }))
    }

    /// Paged list of files whose frontmatter contains property `key`
    /// with a value matching `value` (case-insensitive). For list /
    /// tag_list properties, each element is searched independently.
    pub fn files_with_property(
        &self,
        key: String,
        value: String,
        paging: Paging,
    ) -> Result<FileSummaryPage, VaultError> {
        let page = self
            .inner
            .files_with_property(&key, &value, paging.into())?;
        Ok(page.into())
    }

    /// Every distinct frontmatter property key in the vault, key-sorted,
    /// each with the count of files that carry it and the sorted distinct
    /// value kinds observed for it (m_spec §M-5). For the app's future
    /// property browser.
    pub fn list_property_keys(&self) -> Result<Vec<PropertyKeySummary>, VaultError> {
        let keys = self.inner.list_property_keys()?;
        Ok(keys.into_iter().map(Into::into).collect())
    }

    /// Paged list of files carrying property `key` with any value
    /// (m_spec §M-5) — the key-only companion to `files_with_property`.
    pub fn files_with_property_key(
        &self,
        key: String,
        paging: Paging,
    ) -> Result<FileSummaryPage, VaultError> {
        let page = self.inner.files_with_property_key(&key, paging.into())?;
        Ok(page.into())
    }

    /// Full-text search. Cancellable via the supplied `cancel`
    /// token. Reserved scopes (`File`, `Tag`) return
    /// `VaultError::Cancelled` until those code paths land.
    pub fn full_text_search(
        &self,
        query: String,
        scope: SearchScope,
        cancel: Arc<CancelToken>,
    ) -> Result<QueryResultSet, VaultError> {
        let scope: core::SearchScope = scope.into();
        let result = self.inner.full_text_search(&query, &scope, &cancel.inner)?;
        Ok(result.into())
    }

    /// Enumerate templates under the vault's templates folder
    /// (defaults to `Templates/`, configurable via `SessionConfig`).
    ///
    /// Returns an empty list when the selected templates folder is absent or
    /// vanished after session open. Returns `Cancelled` when the supplied token
    /// is signalled. Only `.md` files are included; results are sorted
    /// alphabetically by name (case-insensitive).
    pub fn list_templates(
        &self,
        cancel: Arc<CancelToken>,
    ) -> Result<Vec<TemplateSummary>, VaultError> {
        Ok(self
            .inner
            .list_templates(&cancel.inner)?
            .into_iter()
            .map(TemplateSummary::from)
            .collect())
    }

    /// Every task parsed from `path`, in document order. Empty result
    /// when the file isn't indexed yet or has no tasks. Used by the
    /// Mac per-file Tasks panel.
    pub fn tasks_for_file(&self, path: String) -> Result<Vec<TaskItem>, VaultError> {
        Ok(self
            .inner
            .tasks_for_file(&path)?
            .into_iter()
            .map(TaskItem::from)
            .collect())
    }

    /// Paged vault-wide task query. Used by the Mac TasksReviewView
    /// to render filtered overdue / today / soon views without
    /// loading every task into memory.
    pub fn tasks_in_vault(
        &self,
        filter: TaskFilter,
        paging: Paging,
    ) -> Result<TaskWithLocationPage, VaultError> {
        let page = self.inner.tasks_in_vault(filter.into(), paging.into())?;
        Ok(page.into())
    }

    /// Replace one task's `[X]` status character in place, routed
    /// through `save_text` so the index, op-log, and conflict
    /// detection stay consistent with editor saves.
    ///
    /// `new_status_char` must be **exactly one printable ASCII
    /// character**, excluding `[`, `]`, `\n`, `\r`, `\t`. This is
    /// narrower than "any Unicode scalar" by design:
    ///
    /// - `\n` / `\r` would split the task line in two and corrupt
    ///   the file (re-parse would lose the task entirely).
    /// - `[` / `]` would unbalance the `[X]` bracket pair and
    ///   confuse downstream parsing.
    /// - Non-ASCII / control characters either don't render as a
    ///   task in any consumer or break the GFM checkbox convention.
    ///
    /// The Mac UI today only emits `' '` / `'x'` / `'/'` / `'-'`,
    /// well inside the allowlist. Scripted callers and tester
    /// explorations get a clean `InvalidArgument` instead of
    /// silent file corruption (#147 / red-team L4).
    pub fn toggle_task_status(
        &self,
        path: String,
        ordinal: u32,
        new_status_char: String,
        expected_content_hash: Option<String>,
    ) -> Result<SaveReport, VaultError> {
        let mut chars = new_status_char.chars();
        let c = chars.next().ok_or(VaultError::InvalidArgument {
            message:
                "new_status_char must be exactly one printable ASCII character (got empty string)"
                    .to_string(),
        })?;
        if chars.next().is_some() {
            return Err(VaultError::InvalidArgument {
                message: format!(
                    "new_status_char must be exactly one printable ASCII character (got {new_status_char:?} — multiple scalars / grapheme cluster)"
                ),
            });
        }
        if !is_allowed_status_char(c) {
            return Err(VaultError::InvalidArgument {
                message: format!(
                    "new_status_char {c:?} is not allowed — must be printable ASCII (0x20..=0x7E), excluding `[`, `]`, `\\n`, `\\r`, `\\t`"
                ),
            });
        }
        let report =
            self.inner
                .toggle_task_status(&path, ordinal, c, expected_content_hash.as_deref())?;
        Ok(report.into())
    }

    /// Insert or replace a YAML frontmatter property. Routes through
    /// the same atomic-write + reindex + op-log pipeline as
    /// `save_text` so the host UI can reuse the conflict dialog and
    /// op-log surface without special-casing.
    ///
    /// Existing keys keep their position in the frontmatter block; a
    /// brand-new key appends at the end. The body of the note is
    /// byte-identical to its pre-edit state.
    pub fn set_property(
        &self,
        path: String,
        key: String,
        value: PropertyValue,
        expected_content_hash: Option<String>,
    ) -> Result<SaveReport, VaultError> {
        let report =
            self.inner
                .set_property(&path, &key, value.into(), expected_content_hash.as_deref())?;
        Ok(report.into())
    }

    /// Remove a YAML frontmatter property. When the deletion empties
    /// the frontmatter, the `---` shell is removed too.
    ///
    /// When the key isn't present (or the file has no frontmatter),
    /// the call short-circuits: no write, no op-log entry — but
    /// `expected_content_hash` is still validated so callers don't
    /// silently miss a stale-read race.
    pub fn delete_property(
        &self,
        path: String,
        key: String,
        expected_content_hash: Option<String>,
    ) -> Result<SaveReport, VaultError> {
        let report = self
            .inner
            .delete_property(&path, &key, expected_content_hash.as_deref())?;
        Ok(report.into())
    }

    /// Rename a YAML frontmatter property across every file in the
    /// vault that currently carries `old_key`. `dry_run = true`
    /// returns the per-file diff without writing; `dry_run = false`
    /// iterates per-file with atomic save_text calls.
    ///
    /// Per-file `WriteConflict` from external mid-rename modification
    /// becomes a `RenameFailed` entry; the rest of the vault still
    /// processes.
    pub fn rename_property_across_vault(
        &self,
        old_key: String,
        new_key: String,
        dry_run: bool,
        cancel: Arc<CancelToken>,
    ) -> Result<RenameReport, VaultError> {
        let report =
            self.inner
                .rename_property_across_vault(&old_key, &new_key, dry_run, &cancel.inner)?;
        Ok(report.into())
    }

    /// Resolve one `![[…]]` embed into the text or bytes the UI
    /// needs to render. Note targets recurse up to depth 3; deeper
    /// embeds surface as `Unresolved { DepthLimitReached }`.
    pub fn resolve_embed(
        &self,
        host_path: String,
        target: String,
        alt: Option<String>,
    ) -> Result<EmbedResolution, VaultError> {
        // #433: `alt` is the authored display text of the link being
        // resolved (image embeds: the alt text). Threading it from
        // the caller's OutgoingLink replaces the per-image host
        // re-read #419 shipped with.
        let resolution = self.inner.resolve_embed(&host_path, &target, alt)?;
        Ok(resolution.into())
    }

    /// Read a binary attachment from the vault. Used by the read-
    /// pane image preview + future "open original" / copy flows.
    /// Returns the raw bytes alongside an inferred MIME type.
    pub fn read_attachment(&self, path: String) -> Result<AttachmentBytes, VaultError> {
        let attachment = self.inner.read_attachment(&path)?;
        Ok(attachment.into())
    }

    /// Render the template at `template_path` against `context`. The
    /// host reads `body` and parks the editor's cursor at
    /// `cursor_byte_offset` if it's `Some(_)`.
    ///
    /// Variable allowlist (per `docs/plans/05` §8.2): `{{date}}`,
    /// `{{date:FMT}}`, `{{time}}`, `{{time:FMT}}`, `{{title}}`,
    /// `{{vault}}`, `{{cursor}}`, `{{prompt:Label}}`. Anything else
    /// (including unknown chrono format specifiers) survives verbatim
    /// in the output, so a typo can never blow up the create-from-
    /// template flow.
    pub fn render_template(
        &self,
        template_path: String,
        context: TemplateContext,
    ) -> Result<RenderedTemplate, VaultError> {
        let rendered = self.inner.render_template(&template_path, context.into())?;
        Ok(rendered.into())
    }

    // --- Milestone K content pipelines (#217 / #218 / #219) -------

    /// Extract + render math blocks in `path` via MathCAT. Honors
    /// the session's `math_prefs`.
    pub fn get_math_blocks(&self, path: String) -> Result<Vec<MathBlock>, VaultError> {
        Ok(self
            .inner
            .get_math_blocks(&path)?
            .into_iter()
            .map(Into::into)
            .collect())
    }

    /// Extract + highlight code blocks in `path` via tree-sitter.
    /// Unknown languages fall back to a single `Other` token.
    pub fn get_syntax_tokens(&self, path: String) -> Result<Vec<CodeBlock>, VaultError> {
        Ok(self
            .inner
            .get_syntax_tokens(&path)?
            .into_iter()
            .map(Into::into)
            .collect())
    }

    /// Extract + render Mermaid diagrams in `path`. Render failures
    /// surface as typed status; structured description is always
    /// populated.
    pub fn get_diagram_blocks(&self, path: String) -> Result<Vec<DiagramBlock>, VaultError> {
        Ok(self
            .inner
            .get_diagram_blocks(&path)?
            .into_iter()
            .map(Into::into)
            .collect())
    }

    /// Ordered whole-document block segmentation for the reading view
    /// (U3-1, #465). Reads `path`, returns each top-level block in
    /// document order with whole-source byte offsets + the exact slice.
    /// The pure `reading_blocks_source` free function is the live-buffer
    /// variant (U3-2) — this one is the initial disk read.
    pub fn reading_blocks(&self, path: String) -> Result<Vec<ReadingBlock>, VaultError> {
        Ok(self
            .inner
            .reading_blocks(&path)?
            .into_iter()
            .map(Into::into)
            .collect())
    }

    /// Swap the session's math preferences at runtime. Settings UI
    /// (#224) drives this when the user changes a Picker — the
    /// next `get_math_blocks` call renders with the new prefs.
    /// Audit #259 — the missing FFI surface that left Settings
    /// changes UI-only.
    pub fn set_math_prefs(&self, prefs: MathPrefs) -> Result<(), VaultError> {
        self.inner.set_math_prefs(prefs.into())?;
        Ok(())
    }

    // --- Milestone L citations + bibliography (#278) -------------

    /// Replace the active bibliography sources, reload entries, and
    /// bump the renderer's `BibIndex` version so any cached renders
    /// are invalidated implicitly.
    /// The session's effective citations prefs (#411): the merged
    /// view across `.slate/prefs.json` and the vault-root
    /// `slate.json`, exactly as `from_filesystem` resolved them.
    /// Passive data — pushing sources into the bibliography index
    /// still happens via `set_bibliography_sources`.
    pub fn citations_prefs(&self) -> CitationsPrefs {
        let p = &self.inner.config().citations_prefs;
        CitationsPrefs {
            sources: p.sources.iter().cloned().map(Into::into).collect(),
            default_style: p.default_style.clone(),
            additional_styles: p.additional_styles.clone(),
        }
    }

    pub fn set_bibliography_sources(
        &self,
        sources: Vec<BibliographySource>,
    ) -> Result<Vec<BibLoadWarning>, VaultError> {
        let core_sources: Vec<core::BibliographySource> =
            sources.into_iter().map(Into::into).collect();
        let warnings = self.inner.set_bibliography_sources(core_sources)?;
        Ok(warnings.into_iter().map(Into::into).collect())
    }

    pub fn get_bibliography_entries(&self) -> Result<Vec<BibEntry>, VaultError> {
        Ok(self
            .inner
            .get_bibliography_entries()?
            .into_iter()
            .map(Into::into)
            .collect())
    }

    pub fn get_bibliography_entry(&self, key: String) -> Result<Option<BibEntry>, VaultError> {
        Ok(self.inner.get_bibliography_entry(&key)?.map(Into::into))
    }

    pub fn search_bibliography(&self, query: String) -> Result<Vec<BibEntry>, VaultError> {
        Ok(self
            .inner
            .search_bibliography(&query)?
            .into_iter()
            .map(Into::into)
            .collect())
    }

    pub fn list_files_citing(&self, citation_key: String) -> Result<Vec<String>, VaultError> {
        Ok(self
            .inner
            .list_files_citing(&citation_key)?
            .into_iter()
            .map(|f| f.path)
            .collect())
    }

    pub fn list_unresolved_citations(&self) -> Result<Vec<UnresolvedCitation>, VaultError> {
        Ok(self
            .inner
            .list_unresolved_citations()?
            .into_iter()
            .map(|(path, key)| UnresolvedCitation { path, key })
            .collect())
    }

    pub fn list_citations_in_file(
        &self,
        path: String,
    ) -> Result<Vec<CitationReference>, VaultError> {
        Ok(self
            .inner
            .list_citations_in_file(&path)?
            .into_iter()
            .map(Into::into)
            .collect())
    }

    pub fn render_citation(
        &self,
        reference: CitationReference,
        style_id: String,
    ) -> Result<RenderedCitation, VaultError> {
        let core_ref: core::CitationReference = reference.into();
        let rendered = self.inner.render_citation(&core_ref, &style_id)?;
        Ok(rendered.into())
    }

    pub fn list_csl_styles(&self) -> Result<Vec<CslStyleInfo>, VaultError> {
        Ok(self
            .inner
            .list_csl_styles()?
            .into_iter()
            .map(Into::into)
            .collect())
    }

    /// Detect external sync systems managing this vault (M-1, #532).
    ///
    /// Filesystem-probe based (sync markers never reach the index).
    /// Sessions without a filesystem root return a report with
    /// `supported == false` rather than an error. Synchronous and
    /// cheap; dispatch off the UI thread like any FFI call.
    pub fn detect_sync(&self) -> Result<SyncDetectionReport, VaultError> {
        Ok(self.inner.detect_sync()?.into())
    }

    /// Read the LiveSync plugin's config, credential-free (M-2, #533).
    ///
    /// Same no-fs-root rule as `detect_sync`: `NotPresent`, never an
    /// error. Only the six allow-listed fields are ever read out of
    /// the plugin's JSON.
    pub fn livesync_config(&self) -> Result<LiveSyncConfigStatus, VaultError> {
        Ok(self.inner.livesync_config()?.into())
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
    _census: census_live::Marker,
}

#[uniffi::export]
impl CancelToken {
    #[uniffi::constructor]
    pub fn new() -> Arc<Self> {
        Arc::new(Self {
            inner: core::CancelToken::new(),
            _census: census_live::Marker::count(&census_live::CANCEL_TOKENS),
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

// --- Milestone M sync detection (M-1, #532) ---------------------------

/// Mirrors `slate_core::sync_detect::SyncProviderKind`.
#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum SyncProviderKind {
    LiveSync,
    ICloudDrive,
    Dropbox,
    OneDrive,
    GoogleDrive,
    Git,
    Syncthing,
}

impl From<core::sync_detect::SyncProviderKind> for SyncProviderKind {
    fn from(k: core::sync_detect::SyncProviderKind) -> Self {
        use core::sync_detect::SyncProviderKind as K;
        match k {
            K::LiveSync => Self::LiveSync,
            K::ICloudDrive => Self::ICloudDrive,
            K::Dropbox => Self::Dropbox,
            K::OneDrive => Self::OneDrive,
            K::GoogleDrive => Self::GoogleDrive,
            K::Git => Self::Git,
            K::Syncthing => Self::Syncthing,
        }
    }
}

/// Mirrors `slate_core::sync_detect::RiskLevel`.
#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum RiskLevel {
    Low,
    Medium,
    High,
}

impl From<core::sync_detect::RiskLevel> for RiskLevel {
    fn from(r: core::sync_detect::RiskLevel) -> Self {
        use core::sync_detect::RiskLevel as R;
        match r {
            R::Low => Self::Low,
            R::Medium => Self::Medium,
            R::High => Self::High,
        }
    }
}

/// Mirrors `slate_core::sync_detect::DetectedSyncProvider`, plus the
/// pre-rendered `display_name` — uniffi enums can't carry methods, and
/// the normative display-name table must stay in core (single source
/// for the M-3 row labels and CLI output).
#[derive(Debug, Clone, uniffi::Record)]
pub struct DetectedSyncProvider {
    pub kind: SyncProviderKind,
    pub display_name: String,
    /// Vault-relative when the marker is inside the vault; absolute
    /// when it is an ancestor/location signal.
    pub evidence_paths: Vec<String>,
    pub risk_level: RiskLevel,
    /// Full recommendation sentence(s) — exact user-facing copy.
    pub recommendation: String,
}

impl From<core::sync_detect::DetectedSyncProvider> for DetectedSyncProvider {
    fn from(p: core::sync_detect::DetectedSyncProvider) -> Self {
        Self {
            display_name: p.kind.display_name().to_string(),
            kind: p.kind.into(),
            evidence_paths: p.evidence_paths,
            risk_level: p.risk_level.into(),
            recommendation: p.recommendation,
        }
    }
}

/// Mirrors `slate_core::sync_detect::SyncDetectionReport`.
#[derive(Debug, Clone, uniffi::Record)]
pub struct SyncDetectionReport {
    /// Detector-table order, deterministic.
    pub providers: Vec<DetectedSyncProvider>,
    /// `Some(copy)` when ≥ 2 providers with risk ≥ Medium are detected.
    pub multi_sync_warning: Option<String>,
    /// Pre-rendered VoiceOver summary.
    pub audio_summary: String,
    /// `false` when the session has no filesystem root: detection
    /// unsupported, `providers` empty.
    pub supported: bool,
}

impl From<core::sync_detect::SyncDetectionReport> for SyncDetectionReport {
    fn from(r: core::sync_detect::SyncDetectionReport) -> Self {
        Self {
            providers: r.providers.into_iter().map(Into::into).collect(),
            multi_sync_warning: r.multi_sync_warning,
            audio_summary: r.audio_summary,
            supported: r.supported,
        }
    }
}

/// Mirrors `slate_core::sync_detect::LiveSyncConfig` (M-2, #533) —
/// the credential-free allow-listed subset of the plugin's data.json.
#[derive(Debug, Clone, uniffi::Record)]
pub struct LiveSyncConfig {
    /// Host (+ optional port) only — never userinfo/path/query/fragment.
    pub server_host: Option<String>,
    pub database: Option<String>,
    pub live_sync_enabled: Option<bool>,
    pub sync_on_save: Option<bool>,
    pub sync_on_start: Option<bool>,
    pub end_to_end_encryption: Option<bool>,
}

impl From<core::sync_detect::LiveSyncConfig> for LiveSyncConfig {
    fn from(c: core::sync_detect::LiveSyncConfig) -> Self {
        Self {
            server_host: c.server_host,
            database: c.database,
            live_sync_enabled: c.live_sync_enabled,
            sync_on_save: c.sync_on_save,
            sync_on_start: c.sync_on_start,
            end_to_end_encryption: c.end_to_end_encryption,
        }
    }
}

/// Mirrors `slate_core::sync_detect::LiveSyncConfigStatus`.
#[derive(Debug, Clone, uniffi::Enum)]
pub enum LiveSyncConfigStatus {
    NotPresent,
    Parsed { config: LiveSyncConfig },
    Malformed { reason: String },
}

impl From<core::sync_detect::LiveSyncConfigStatus> for LiveSyncConfigStatus {
    fn from(s: core::sync_detect::LiveSyncConfigStatus) -> Self {
        use core::sync_detect::LiveSyncConfigStatus as S;
        match s {
            S::NotPresent => Self::NotPresent,
            S::Parsed(config) => Self::Parsed {
                config: config.into(),
            },
            S::Malformed { reason } => Self::Malformed { reason },
        }
    }
}

/// Filter passed to `list_files`.
#[derive(uniffi::Enum)]
pub enum FileFilter {
    All,
    MarkdownOnly,
    /// Markdown notes plus `.canvas` files (Milestone T, #361) — the
    /// openable-document set for quick open / the file tree (#369).
    MarkdownAndCanvas,
    /// Markdown notes plus `.canvas` and `.base` files (Milestone N, #702).
    OpenableDocuments,
}

impl From<FileFilter> for core::FileFilter {
    fn from(f: FileFilter) -> Self {
        match f {
            FileFilter::All => core::FileFilter::All,
            FileFilter::MarkdownOnly => core::FileFilter::MarkdownOnly,
            FileFilter::MarkdownAndCanvas => core::FileFilter::MarkdownAndCanvas,
            FileFilter::OpenableDocuments => core::FileFilter::OpenableDocuments,
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
    pub display_name: Option<String>,
    pub created_date: Option<String>,
    pub created_ms: Option<i64>,
    pub word_count: Option<u32>,
    pub preview: Option<String>,
    pub task_total: u32,
    pub task_open: u32,
}

/// One frontmatter property as exposed across the FFI boundary.
///
/// `kind` is one of `"text"`, `"number"`, `"boolean"`, `"date"`,
/// `"datetime"`, `"wikilink"`, `"list"`, `"tag_list"`. `value_json`
/// is the JSON-encoded value the storage layer round-trips through
/// SQLite — atomic kinds get the literal form (`"foo"`, `42`, `true`,
/// `"2024-01-02"`), list / tag_list get JSON arrays. The Swift /
/// Kotlin side decodes via the platform's JSON parser, which keeps
/// the FFI surface trivially-derived and avoids re-implementing
/// recursive enums across uniffi.
#[derive(uniffi::Record)]
pub struct Property {
    pub key: String,
    pub kind: String,
    pub value_json: String,
}

impl From<core::Property> for Property {
    fn from(p: core::Property) -> Self {
        let (kind, value_json) = encode_property(&p.value);
        Self {
            key: p.key,
            kind: kind.to_string(),
            value_json,
        }
    }
}

/// Discriminated `PropertyValue` for the write-side API
/// (`set_property`, `rename_property_across_vault`).
///
/// Read-side (`get_file_metadata`, `note_load_bundle`, etc.) keeps
/// returning the `(kind, value_json)` encoding on `Property` — the
/// two surfaces aren't unified because callers reading bulk metadata
/// don't pay the cost of a tagged union per row, while callers
/// writing one edit at a time get a clean discriminated value they
/// can pattern-match instead of dispatching on a kind string.
#[derive(Debug, Clone, PartialEq, uniffi::Enum)]
pub enum PropertyValue {
    Text { value: String },
    Integer { value: i64 },
    Float { value: f64 },
    Boolean { value: bool },
    Date { value: String },
    Datetime { value: String },
    Wikilink { target: String },
    List { items: Vec<PropertyValue> },
    TagList { tags: Vec<String> },
}

impl From<PropertyValue> for core::PropertyValue {
    fn from(v: PropertyValue) -> Self {
        match v {
            PropertyValue::Text { value } => core::PropertyValue::Text(value),
            PropertyValue::Integer { value } => core::PropertyValue::Integer(value),
            PropertyValue::Float { value } => core::PropertyValue::Float(value),
            PropertyValue::Boolean { value } => core::PropertyValue::Boolean(value),
            PropertyValue::Date { value } => core::PropertyValue::Date(value),
            PropertyValue::Datetime { value } => core::PropertyValue::Datetime(value),
            PropertyValue::Wikilink { target } => core::PropertyValue::Wikilink(target),
            PropertyValue::List { items } => {
                core::PropertyValue::List(items.into_iter().map(Into::into).collect())
            }
            PropertyValue::TagList { tags } => core::PropertyValue::TagList(tags),
        }
    }
}

/// FFI-side encoder. Mirrors the SQLite encoding in
/// `properties_db::serialize_value` so a property loaded from the
/// DB and a property freshly parsed at boundary-crossing time
/// produce identical wire-format strings.
fn encode_property(value: &core::PropertyValue) -> (&'static str, String) {
    use core::PropertyValue;
    use serde_json::Value as J;
    let v: J = match value {
        PropertyValue::Text(s) => J::String(s.clone()),
        PropertyValue::Integer(i) => J::from(*i),
        PropertyValue::Float(f) => {
            if f.is_finite() {
                J::from(*f)
            } else {
                J::String(f.to_string())
            }
        }
        PropertyValue::Boolean(b) => J::Bool(*b),
        PropertyValue::Date(s) | PropertyValue::Datetime(s) | PropertyValue::Wikilink(s) => {
            J::String(s.clone())
        }
        PropertyValue::List(items) => J::Array(items.iter().map(encode_inner).collect()),
        PropertyValue::TagList(tags) => J::Array(tags.iter().cloned().map(J::String).collect()),
    };
    let kind = match value {
        PropertyValue::Text(_) => "text",
        PropertyValue::Integer(_) | PropertyValue::Float(_) => "number",
        PropertyValue::Boolean(_) => "boolean",
        PropertyValue::Date(_) => "date",
        PropertyValue::Datetime(_) => "datetime",
        PropertyValue::Wikilink(_) => "wikilink",
        PropertyValue::List(_) => "list",
        PropertyValue::TagList(_) => "tag_list",
    };
    (kind, v.to_string())
}

fn encode_inner(value: &core::PropertyValue) -> serde_json::Value {
    use core::PropertyValue;
    use serde_json::Value as J;
    match value {
        PropertyValue::Text(s) => J::String(s.clone()),
        PropertyValue::Integer(i) => J::from(*i),
        PropertyValue::Float(f) => {
            if f.is_finite() {
                J::from(*f)
            } else {
                J::String(f.to_string())
            }
        }
        PropertyValue::Boolean(b) => J::Bool(*b),
        PropertyValue::Date(s) | PropertyValue::Datetime(s) | PropertyValue::Wikilink(s) => {
            J::String(s.clone())
        }
        PropertyValue::List(items) => J::Array(items.iter().map(encode_inner).collect()),
        PropertyValue::TagList(tags) => J::Array(tags.iter().cloned().map(J::String).collect()),
    }
}

/// Full per-file metadata returned by `get_file_metadata`.
#[derive(uniffi::Record)]
pub struct FileMetadata {
    pub path: String,
    pub name: String,
    pub mtime_ms: i64,
    pub size_bytes: u64,
    pub is_markdown: bool,
    pub content_hash: String,
    pub headings: Vec<Heading>,
    pub properties: Vec<Property>,
}

impl From<core::FileMetadata> for FileMetadata {
    fn from(m: core::FileMetadata) -> Self {
        Self {
            path: m.path,
            name: m.name,
            mtime_ms: m.mtime_ms,
            size_bytes: m.size_bytes,
            is_markdown: m.is_markdown,
            content_hash: m.content_hash,
            headings: m.headings.into_iter().map(Into::into).collect(),
            properties: m.properties.into_iter().map(Into::into).collect(),
        }
    }
}

impl From<core::FileSummary> for FileSummary {
    fn from(s: core::FileSummary) -> Self {
        Self {
            path: s.path,
            name: s.name,
            mtime_ms: s.mtime_ms,
            size_bytes: s.size_bytes,
            is_markdown: s.is_markdown,
            display_name: s.display_name,
            created_date: s.created_date,
            created_ms: s.created_ms,
            word_count: s.word_count,
            preview: s.preview,
            task_total: s.task_total,
            task_open: s.task_open,
        }
    }
}

/// One distinct property key, the count of files that carry it, and its
/// sorted distinct value kinds (m_spec §M-5). Mirrors
/// `core::PropertyKeySummary` across the FFI boundary for the app's
/// future property browser.
#[derive(uniffi::Record)]
pub struct PropertyKeySummary {
    pub key: String,
    pub file_count: u64,
    pub value_kinds: Vec<String>,
}

impl From<core::PropertyKeySummary> for PropertyKeySummary {
    fn from(s: core::PropertyKeySummary) -> Self {
        Self {
            key: s.key,
            file_count: s.file_count,
            value_kinds: s.value_kinds,
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

/// One child directory row in a [`DirListing`] (#459). `child_dir_count`
/// / `child_file_count` are the immediate (non-recursive) child counts so
/// the UI can announce a collapsed folder's item count without a second
/// fetch. `id` is stable across rescans and serves as the tree node id.
#[derive(uniffi::Record)]
pub struct DirNodeSummary {
    pub id: i64,
    pub path: String,
    pub name: String,
    pub child_dir_count: u32,
    pub child_file_count: u32,
    /// FL6-1 (#667): this folder contains `<name>/<name>.md` (exact
    /// stem, byte compare — the one folder-note convention).
    pub has_folder_note: bool,
}

/// U2-2 (#460): structural-mutation report mirrors.
#[derive(uniffi::Record)]
pub struct StructuralReport {
    pub op_id: i64,
    /// FL6-1: ordered ids that fully reverse this report (newest
    /// first) — a compound folder+note rename journals two rows and
    /// `op_id` alone cannot reverse it. Single ops carry `[op_id]`.
    pub undo_op_ids: Vec<i64>,
    pub moved: Vec<MovedPath>,
    pub rewritten: Vec<RewriteOutcome>,
    pub failed: Vec<RewriteFailure>,
}

/// Tuple structs don't cross uniffi; `(old, new)` becomes a record.
#[derive(uniffi::Record)]
pub struct MovedPath {
    pub old_path: String,
    pub new_path: String,
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct RewriteOutcome {
    pub path: String,
    pub hash_before: String,
    pub hash_after: String,
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct RewriteFailure {
    pub path: String,
    pub kind: RewriteFailureKind,
}

/// Flattened (uniffi enums carry no per-variant payload here; the detail
/// string rides alongside).
#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct RewriteFailureKind {
    pub kind: String,
    pub detail: String,
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct StructuralBatchItem {
    pub path: String,
    pub is_directory: bool,
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct BatchMoveRequest {
    pub items: Vec<StructuralBatchItem>,
    pub new_parent: String,
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct BatchTrashRequest {
    pub items: Vec<StructuralBatchItem>,
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct BatchPathChange {
    pub old_path: String,
    pub new_path: String,
    pub is_directory: bool,
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct BatchSkippedItem {
    pub item: StructuralBatchItem,
    pub reason: BatchSkipReason,
    pub detail: String,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum BatchSkipReason {
    Duplicate,
    CoveredBySelectedFolder,
    AlreadyInDestination,
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct BatchItemFailure {
    pub item: Option<StructuralBatchItem>,
    pub stage: BatchFailureStage,
    pub message: String,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum BatchFailureStage {
    Preflight,
    Move,
    Index,
    LinkRewrite,
    LinkRewriteRestore,
    Journal,
    Rollback,
    Trash,
    Reconciliation,
    RecoveryBarrier,
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct StructuralBatchEnvelope {
    pub planned: Vec<StructuralBatchItem>,
    pub skipped: Vec<BatchSkippedItem>,
    pub preflight_failures: Vec<BatchItemFailure>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum BatchMoveState {
    Rejected,
    NoOp,
    Succeeded,
    RolledBack,
    RollbackIncomplete,
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct BatchMoveReport {
    pub envelope: StructuralBatchEnvelope,
    pub state: BatchMoveState,
    pub op_id: Option<i64>,
    pub standing: Vec<BatchPathChange>,
    pub rolled_back: Vec<BatchPathChange>,
    pub failure: Option<BatchItemFailure>,
    pub rollback_failures: Vec<BatchItemFailure>,
    pub rewritten: Vec<RewriteOutcome>,
    pub rewrite_failures: Vec<RewriteFailure>,
    pub requires_rescan: bool,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum BatchTrashState {
    Rejected,
    NoOp,
    Succeeded,
    Partial,
    Failed,
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct BatchTrashRemainder {
    pub item: StructuralBatchItem,
    pub failure: BatchItemFailure,
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct BatchTrashReport {
    pub envelope: StructuralBatchEnvelope,
    pub state: BatchTrashState,
    pub op_id: Option<i64>,
    pub trashed: Vec<StructuralBatchItem>,
    pub untrashed: Vec<BatchTrashRemainder>,
    pub unknown: Vec<BatchTrashRemainder>,
    pub bookkeeping_failures: Vec<BatchItemFailure>,
    pub requires_rescan: bool,
}

impl From<core::structural::StructuralReport> for StructuralReport {
    fn from(r: core::structural::StructuralReport) -> Self {
        Self {
            op_id: r.op_id,
            undo_op_ids: r.undo_op_ids,
            moved: r
                .moved
                .into_iter()
                .map(|(old_path, new_path)| MovedPath { old_path, new_path })
                .collect(),
            rewritten: r.rewritten.into_iter().map(Into::into).collect(),
            failed: r.failed.into_iter().map(Into::into).collect(),
        }
    }
}

impl From<core::structural::RewriteOutcome> for RewriteOutcome {
    fn from(r: core::structural::RewriteOutcome) -> Self {
        Self {
            path: r.path,
            hash_before: r.hash_before,
            hash_after: r.hash_after,
        }
    }
}

impl From<core::structural::RewriteFailure> for RewriteFailure {
    fn from(f: core::structural::RewriteFailure) -> Self {
        let (kind, detail) = match f.kind {
            core::structural::RewriteFailureKind::WriteConflict => {
                ("write_conflict".to_string(), String::new())
            }
            core::structural::RewriteFailureKind::MalformedFrontmatter => {
                ("malformed_frontmatter".to_string(), String::new())
            }
            core::structural::RewriteFailureKind::Cancelled => {
                ("cancelled".to_string(), String::new())
            }
            core::structural::RewriteFailureKind::Other(detail) => ("other".to_string(), detail),
        };
        Self {
            path: f.path,
            kind: RewriteFailureKind { kind, detail },
        }
    }
}

impl From<StructuralBatchItem> for core::structural_batch::StructuralBatchItem {
    fn from(item: StructuralBatchItem) -> Self {
        Self {
            path: item.path,
            is_directory: item.is_directory,
        }
    }
}

impl From<core::structural_batch::StructuralBatchItem> for StructuralBatchItem {
    fn from(item: core::structural_batch::StructuralBatchItem) -> Self {
        Self {
            path: item.path,
            is_directory: item.is_directory,
        }
    }
}

impl From<BatchMoveRequest> for core::structural_batch::BatchMoveRequest {
    fn from(request: BatchMoveRequest) -> Self {
        Self {
            items: request.items.into_iter().map(Into::into).collect(),
            new_parent: request.new_parent,
        }
    }
}

impl From<BatchTrashRequest> for core::structural_batch::BatchTrashRequest {
    fn from(request: BatchTrashRequest) -> Self {
        Self {
            items: request.items.into_iter().map(Into::into).collect(),
        }
    }
}

impl From<core::structural_batch::BatchPathChange> for BatchPathChange {
    fn from(change: core::structural_batch::BatchPathChange) -> Self {
        Self {
            old_path: change.old_path,
            new_path: change.new_path,
            is_directory: change.is_directory,
        }
    }
}

impl From<core::structural_batch::BatchSkippedItem> for BatchSkippedItem {
    fn from(skipped: core::structural_batch::BatchSkippedItem) -> Self {
        Self {
            item: skipped.item.into(),
            reason: skipped.reason.into(),
            detail: skipped.detail,
        }
    }
}

impl From<core::structural_batch::BatchSkipReason> for BatchSkipReason {
    fn from(reason: core::structural_batch::BatchSkipReason) -> Self {
        use core::structural_batch::BatchSkipReason as C;
        match reason {
            C::Duplicate => Self::Duplicate,
            C::CoveredBySelectedFolder => Self::CoveredBySelectedFolder,
            C::AlreadyInDestination => Self::AlreadyInDestination,
        }
    }
}

impl From<core::structural_batch::BatchItemFailure> for BatchItemFailure {
    fn from(failure: core::structural_batch::BatchItemFailure) -> Self {
        Self {
            item: failure.item.map(Into::into),
            stage: failure.stage.into(),
            message: failure.message,
        }
    }
}

impl From<core::structural_batch::BatchFailureStage> for BatchFailureStage {
    fn from(stage: core::structural_batch::BatchFailureStage) -> Self {
        use core::structural_batch::BatchFailureStage as C;
        match stage {
            C::Preflight => Self::Preflight,
            C::Move => Self::Move,
            C::Index => Self::Index,
            C::LinkRewrite => Self::LinkRewrite,
            C::LinkRewriteRestore => Self::LinkRewriteRestore,
            C::Journal => Self::Journal,
            C::Rollback => Self::Rollback,
            C::Trash => Self::Trash,
            C::Reconciliation => Self::Reconciliation,
            C::RecoveryBarrier => Self::RecoveryBarrier,
        }
    }
}

impl From<core::structural_batch::StructuralBatchEnvelope> for StructuralBatchEnvelope {
    fn from(envelope: core::structural_batch::StructuralBatchEnvelope) -> Self {
        Self {
            planned: envelope.planned.into_iter().map(Into::into).collect(),
            skipped: envelope.skipped.into_iter().map(Into::into).collect(),
            preflight_failures: envelope
                .preflight_failures
                .into_iter()
                .map(Into::into)
                .collect(),
        }
    }
}

impl From<core::structural_batch::BatchMoveState> for BatchMoveState {
    fn from(state: core::structural_batch::BatchMoveState) -> Self {
        use core::structural_batch::BatchMoveState as C;
        match state {
            C::Rejected => Self::Rejected,
            C::NoOp => Self::NoOp,
            C::Succeeded => Self::Succeeded,
            C::RolledBack => Self::RolledBack,
            C::RollbackIncomplete => Self::RollbackIncomplete,
        }
    }
}

impl From<core::structural_batch::BatchMoveReport> for BatchMoveReport {
    fn from(report: core::structural_batch::BatchMoveReport) -> Self {
        Self {
            envelope: report.envelope.into(),
            state: report.state.into(),
            op_id: report.op_id,
            standing: report.standing.into_iter().map(Into::into).collect(),
            rolled_back: report.rolled_back.into_iter().map(Into::into).collect(),
            failure: report.failure.map(Into::into),
            rollback_failures: report
                .rollback_failures
                .into_iter()
                .map(Into::into)
                .collect(),
            rewritten: report.rewritten.into_iter().map(Into::into).collect(),
            rewrite_failures: report
                .rewrite_failures
                .into_iter()
                .map(Into::into)
                .collect(),
            requires_rescan: report.requires_rescan,
        }
    }
}

impl From<core::structural_batch::BatchTrashState> for BatchTrashState {
    fn from(state: core::structural_batch::BatchTrashState) -> Self {
        use core::structural_batch::BatchTrashState as C;
        match state {
            C::Rejected => Self::Rejected,
            C::NoOp => Self::NoOp,
            C::Succeeded => Self::Succeeded,
            C::Partial => Self::Partial,
            C::Failed => Self::Failed,
        }
    }
}

impl From<core::structural_batch::BatchTrashRemainder> for BatchTrashRemainder {
    fn from(remainder: core::structural_batch::BatchTrashRemainder) -> Self {
        Self {
            item: remainder.item.into(),
            failure: remainder.failure.into(),
        }
    }
}

impl From<core::structural_batch::BatchTrashReport> for BatchTrashReport {
    fn from(report: core::structural_batch::BatchTrashReport) -> Self {
        Self {
            envelope: report.envelope.into(),
            state: report.state.into(),
            op_id: report.op_id,
            trashed: report.trashed.into_iter().map(Into::into).collect(),
            untrashed: report.untrashed.into_iter().map(Into::into).collect(),
            unknown: report.unknown.into_iter().map(Into::into).collect(),
            bookkeeping_failures: report
                .bookkeeping_failures
                .into_iter()
                .map(Into::into)
                .collect(),
            requires_rescan: report.requires_rescan,
        }
    }
}

impl From<core::DirNodeSummary> for DirNodeSummary {
    fn from(d: core::DirNodeSummary) -> Self {
        Self {
            id: d.id,
            path: d.path,
            name: d.name,
            child_dir_count: d.child_dir_count,
            child_file_count: d.child_file_count,
            has_folder_note: d.has_folder_note,
        }
    }
}

/// One level of the file tree: the child directories of a parent (already
/// sorted, dirs-first) followed by a page of its child files (#459).
#[derive(uniffi::Record)]
pub struct DirListing {
    pub dirs: Vec<DirNodeSummary>,
    pub files: FileSummaryPage,
}

impl From<core::DirListing> for DirListing {
    fn from(l: core::DirListing) -> Self {
        Self {
            dirs: l.dirs.into_iter().map(Into::into).collect(),
            files: l.files.into(),
        }
    }
}

/// Anchor suffix on a wikilink target as exposed across FFI.
///
/// Kept simple (kind + text) so foreign callers don't have to model a
/// tagged-union — the kind string is one of `"heading"` or `"block"`.
#[derive(uniffi::Record)]
pub struct LinkAnchor {
    pub kind: String,
    pub text: String,
}

/// Single outgoing link from a source file, as returned by
/// `outgoing_links`.
#[derive(uniffi::Record)]
pub struct OutgoingLink {
    pub target_path: Option<String>,
    pub target_raw: String,
    pub target_anchor: Option<LinkAnchor>,
    pub kind: String,
    pub is_embed: bool,
    pub is_external: bool,
    pub is_unresolved: bool,
    pub snippet: String,
    pub ordinal: u32,
    /// Authored display text (`![alt](src)` → the alt; `[[t|d]]` → d).
    pub display_text: Option<String>,
}

impl From<core::OutgoingLink> for OutgoingLink {
    fn from(l: core::OutgoingLink) -> Self {
        Self {
            target_path: l.target_path,
            target_raw: l.target_raw,
            target_anchor: l
                .target_anchor
                .map(|(kind, text)| LinkAnchor { kind, text }),
            kind: l.kind,
            is_embed: l.is_embed,
            is_external: l.is_external,
            is_unresolved: l.is_unresolved,
            snippet: l.snippet,
            ordinal: l.ordinal,
            display_text: l.display_text,
        }
    }
}

/// One backlink — a file that links TO the queried path.
#[derive(uniffi::Record)]
pub struct Backlink {
    pub source_path: String,
    pub snippet: String,
    pub ordinal: u32,
    pub kind: String,
    pub is_embed: bool,
}

impl From<core::Backlink> for Backlink {
    fn from(b: core::Backlink) -> Self {
        Self {
            source_path: b.source_path,
            snippet: b.snippet,
            ordinal: b.ordinal,
            kind: b.kind,
            is_embed: b.is_embed,
        }
    }
}

/// Paged backlinks result.
#[derive(uniffi::Record)]
pub struct BacklinkPage {
    pub items: Vec<Backlink>,
    pub next_cursor: Option<String>,
    pub total_filtered: u64,
}

impl From<core::Page<core::Backlink>> for BacklinkPage {
    fn from(p: core::Page<core::Backlink>) -> Self {
        Self {
            items: p.items.into_iter().map(Into::into).collect(),
            next_cursor: p.next_cursor,
            total_filtered: p.total_filtered,
        }
    }
}

/// A note split into frontmatter source + body plus the whole-file
/// content hash and mtime — the U3 tab-open payload (#469, U3-5).
/// Mirrors `slate_core::NotePartsBundle`. The Swift side maps this to
/// `NoteDocument.fmSource` / `bodyText`.
#[derive(uniffi::Record)]
pub struct NotePartsBundle {
    pub fm_source: String,
    pub body: String,
    pub content_hash: String,
    pub mtime_ms: i64,
    /// Body start in the whole file, UTF-8 bytes (U3-3 offset rebase).
    pub body_byte_offset: u64,
    /// Newlines before the body start (file line → body line delta).
    pub body_line_offset: u32,
}

impl From<core::NotePartsBundle> for NotePartsBundle {
    fn from(b: core::NotePartsBundle) -> Self {
        Self {
            fm_source: b.fm_source,
            body: b.body,
            content_hash: b.content_hash,
            mtime_ms: b.mtime_ms,
            body_byte_offset: b.body_byte_offset,
            body_line_offset: b.body_line_offset,
        }
    }
}

/// Combined backlinks + outgoing-links + properties bundle for a
/// single note, fetched under one mutex acquisition by
/// `VaultSession::note_load_bundle` (#92 item 4).
#[derive(uniffi::Record)]
pub struct NoteLoadBundle {
    pub backlinks: BacklinkPage,
    pub outgoing_links: Vec<OutgoingLink>,
    pub properties: Vec<Property>,
}

impl From<core::NoteLoadBundle> for NoteLoadBundle {
    fn from(b: core::NoteLoadBundle) -> Self {
        Self {
            backlinks: b.backlinks.into(),
            outgoing_links: b.outgoing_links.into_iter().map(Into::into).collect(),
            properties: b.properties.into_iter().map(Into::into).collect(),
        }
    }
}

/// One row in the vault-wide unresolved-links audit.
#[derive(uniffi::Record)]
pub struct UnresolvedLink {
    pub source_path: String,
    pub target_raw: String,
    pub ordinal: u32,
    pub snippet: String,
}

impl From<core::UnresolvedLink> for UnresolvedLink {
    fn from(u: core::UnresolvedLink) -> Self {
        Self {
            source_path: u.source_path,
            target_raw: u.target_raw,
            ordinal: u.ordinal,
            snippet: u.snippet,
        }
    }
}

/// Paged unresolved-link audit result.
#[derive(uniffi::Record)]
pub struct UnresolvedLinkPage {
    pub items: Vec<UnresolvedLink>,
    pub next_cursor: Option<String>,
    pub total_filtered: u64,
}

impl From<core::Page<core::UnresolvedLink>> for UnresolvedLinkPage {
    fn from(p: core::Page<core::UnresolvedLink>) -> Self {
        Self {
            items: p.items.into_iter().map(Into::into).collect(),
            next_cursor: p.next_cursor,
            total_filtered: p.total_filtered,
        }
    }
}

/// Scope of a `full_text_search` call. `Vault`, `Folder`, and `Tag`
/// are live; `File` (single-file find-in-page) is reserved and
/// returns `VaultError::Unsupported`. `Tag` also lists all tagged
/// files on an empty query — see `slate_core::SearchScope::Tag`.
#[derive(uniffi::Enum)]
pub enum SearchScope {
    Vault,
    Folder { path: String },
    File { path: String },
    Tag { name: String },
}

impl From<SearchScope> for core::SearchScope {
    fn from(s: SearchScope) -> Self {
        match s {
            SearchScope::Vault => core::SearchScope::Vault,
            SearchScope::Folder { path } => core::SearchScope::Folder(path),
            SearchScope::File { path } => core::SearchScope::File(path),
            SearchScope::Tag { name } => core::SearchScope::Tag(name),
        }
    }
}

/// One full-text-search hit.
///
/// No `line_number` field — the line is derived UI-side at result-
/// activation time so we don't pull `body_text` through SQLite for
/// every hit at search time. See `slate_core::search_db` module
/// docs (#92 item 1).
#[derive(uniffi::Record)]
pub struct QueryHit {
    pub path: String,
    /// Snippet of ±60 chars around the match. STX (`\u{0002}`) and
    /// ETX (`\u{0003}`) wrap the matched tokens — the host UI
    /// replaces those with attributed-string emphasis.
    pub snippet: String,
    pub score: f64,
}

impl From<core::QueryHit> for QueryHit {
    fn from(h: core::QueryHit) -> Self {
        Self {
            path: h.path,
            snippet: h.snippet,
            score: h.score,
        }
    }
}

/// Result set returned by `full_text_search`.
#[derive(uniffi::Record)]
pub struct QueryResultSet {
    pub rows: Vec<QueryHit>,
    pub summary: String,
}

impl From<core::QueryResultSet> for QueryResultSet {
    fn from(r: core::QueryResultSet) -> Self {
        Self {
            rows: r.rows.into_iter().map(Into::into).collect(),
            summary: r.summary,
        }
    }
}

/// Incremental scan progress events emitted to a `ScanProgressListener`.
///
/// Mirrors `slate_core::ScanProgress`. Listeners always observe
/// `Started` first, one `FileIndexed` per visited file, and exactly
/// one terminal event (`Finished` or `Cancelled`).
#[derive(uniffi::Enum)]
pub enum ScanProgress {
    Started {
        total_files: u64,
    },
    FileIndexed {
        path: String,
        indexed: u64,
        total: u64,
    },
    Finished {
        report: ScanReport,
    },
    Cancelled,
    Failed {
        message: String,
    },
}

impl From<core::ScanProgress> for ScanProgress {
    fn from(p: core::ScanProgress) -> Self {
        match p {
            core::ScanProgress::Started { total_files } => ScanProgress::Started { total_files },
            core::ScanProgress::FileIndexed {
                path,
                indexed,
                total,
            } => ScanProgress::FileIndexed {
                path,
                indexed,
                total,
            },
            core::ScanProgress::Finished { report } => ScanProgress::Finished {
                report: report.into(),
            },
            core::ScanProgress::Cancelled => ScanProgress::Cancelled,
            core::ScanProgress::Failed { message } => ScanProgress::Failed { message },
        }
    }
}

/// Foreign-implementable listener for scan progress events.
///
/// On the Swift side this becomes a `protocol ScanProgressListener`
/// the host can implement on a class. Methods are invoked from the
/// scanner's thread; implementations must be cheap and non-blocking
/// (marshal back to the main actor asynchronously rather than block
/// inside `onProgress`).
#[uniffi::export(with_foreign)]
pub trait ScanProgressListener: Send + Sync {
    fn on_progress(&self, event: ScanProgress);
}

/// Bridges core::ScanProgressListener calls (in Rust) into the
/// foreign-implemented uniffi ScanProgressListener (which the Swift
/// app provides). Each event is converted from the core enum to the
/// FFI enum before forwarding.
struct ScanProgressListenerAdapter {
    foreign: Arc<dyn ScanProgressListener>,
}

impl core::ScanProgressListener for ScanProgressListenerAdapter {
    fn on_progress(&self, event: core::ScanProgress) {
        self.foreign.on_progress(event.into());
    }
}

/// What went wrong in a session-event error (O-2 #540). Mirrors
/// `slate_core::EventErrorCode`; additive-only — hosts must tolerate
/// unknown codes.
#[derive(Debug, Clone, uniffi::Enum)]
pub enum EventErrorCode {
    CompactionFailed,
}

impl From<core::EventErrorCode> for EventErrorCode {
    fn from(c: core::EventErrorCode) -> Self {
        match c {
            core::EventErrorCode::CompactionFailed => EventErrorCode::CompactionFailed,
        }
    }
}

/// One Slate-originated file mutation (#802). Mirrors
/// `slate_core::FileChangeEvent`.
#[derive(Debug, Clone, uniffi::Record)]
pub struct FileChangeEvent {
    pub kind: FileChangeKind,
    /// Vault-relative. For `Renamed`, the NEW path.
    pub path: String,
    /// `Renamed` only: the path moved away from.
    pub previous_path: Option<String>,
}

/// What happened to the file (#802). Additive-only — hosts must
/// tolerate unknown kinds (the `EventErrorCode` convention).
#[derive(Debug, Clone, uniffi::Enum)]
pub enum FileChangeKind {
    Created,
    Modified,
    Deleted,
    Renamed,
}

// --- Graph surface (Milestone P #552) ----------------------------------

/// Node kind in the link graph. Mirrors `slate_core::graph::NodeKind`.
#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum GraphNodeKind {
    Note,
    Attachment,
    Ghost,
}

impl From<core::graph::NodeKind> for GraphNodeKind {
    fn from(k: core::graph::NodeKind) -> Self {
        match k {
            core::graph::NodeKind::Note => GraphNodeKind::Note,
            core::graph::NodeKind::Attachment => GraphNodeKind::Attachment,
            core::graph::NodeKind::Ghost => GraphNodeKind::Ghost,
        }
    }
}

/// Edge kind in the link graph. Mirrors `slate_core::graph::EdgeKind`.
#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum GraphEdgeKind {
    Link,
    Embed,
}

impl From<core::graph::EdgeKind> for GraphEdgeKind {
    fn from(k: core::graph::EdgeKind) -> Self {
        match k {
            core::graph::EdgeKind::Link => GraphEdgeKind::Link,
            core::graph::EdgeKind::Embed => GraphEdgeKind::Embed,
        }
    }
}

/// One graph node with its metrics (#552). `id` is stable while the
/// snapshot's `generation` is unchanged — a generation change may
/// reassign ids, so re-fetch instead of caching ids across
/// generations; never stable across sessions. Mirrors
/// `slate_core::graph::GraphNode`.
#[derive(Debug, Clone, uniffi::Record)]
pub struct GraphNode {
    pub id: u64,
    /// `None` for ghosts.
    pub path: Option<String>,
    pub label: String,
    pub kind: GraphNodeKind,
    pub in_links: u32,
    pub out_links: u32,
    pub in_embeds: u32,
    pub out_embeds: u32,
    pub component: u32,
    pub is_orphan: bool,
    pub pagerank: f64,
    /// `files.mtime_ms`; `None` for ghosts.
    pub modified_ms: Option<i64>,
}

impl From<core::graph::GraphNode> for GraphNode {
    fn from(n: core::graph::GraphNode) -> Self {
        GraphNode {
            id: n.id,
            path: n.path,
            label: n.label,
            kind: n.kind.into(),
            in_links: n.in_links,
            out_links: n.out_links,
            in_embeds: n.in_embeds,
            out_embeds: n.out_embeds,
            component: n.component,
            is_orphan: n.is_orphan,
            pagerank: n.pagerank,
            modified_ms: n.modified_ms,
        }
    }
}

/// One collapsed edge (#552): parallel references share an edge with
/// a reference count. Mirrors `slate_core::graph::GraphEdge`.
#[derive(Debug, Clone, uniffi::Record)]
pub struct GraphEdge {
    pub source_id: u64,
    pub target_id: u64,
    pub kind: GraphEdgeKind,
    pub count: u32,
}

impl From<core::graph::GraphEdge> for GraphEdge {
    fn from(e: core::graph::GraphEdge) -> Self {
        GraphEdge {
            source_id: e.source_id,
            target_id: e.target_id,
            kind: e.kind.into(),
            count: e.count,
        }
    }
}

/// Projection filter (#552). Defaults: attachments off, ghosts on,
/// all notes. Mirrors `slate_core::graph::GraphFilter`.
#[derive(Debug, Clone, Copy, uniffi::Record)]
pub struct GraphFilter {
    pub include_attachments: bool,
    pub include_ghosts: bool,
    pub orphans_only: bool,
}

impl From<GraphFilter> for core::graph::GraphFilter {
    fn from(f: GraphFilter) -> Self {
        core::graph::GraphFilter {
            include_attachments: f.include_attachments,
            include_ghosts: f.include_ghosts,
            orphans_only: f.orphans_only,
        }
    }
}

/// Whole-graph projection under a filter (#552). Node order is
/// key-sorted; edge order is (source, target, kind) — deterministic.
#[derive(Debug, Clone, uniffi::Record)]
pub struct GraphSnapshot {
    pub nodes: Vec<GraphNode>,
    pub edges: Vec<GraphEdge>,
    pub generation: u64,
    /// Pre-rendered VoiceOver summary, e.g. `"247 notes, 1,032 links.
    /// 12 orphans, 3 unresolved targets."` (format normative in
    /// p0_spec §P0-3).
    pub audio_summary: String,
}

impl From<core::graph::GraphSnapshot> for GraphSnapshot {
    fn from(s: core::graph::GraphSnapshot) -> Self {
        GraphSnapshot {
            nodes: s.nodes.into_iter().map(Into::into).collect(),
            edges: s.edges.into_iter().map(Into::into).collect(),
            generation: s.generation,
            audio_summary: s.audio_summary,
        }
    }
}

/// Depth-limited neighborhood of one note (#552).
#[derive(Debug, Clone, uniffi::Record)]
pub struct GraphNeighborhood {
    pub center_id: u64,
    pub depth: u32,
    pub nodes: Vec<GraphNode>,
    pub edges: Vec<GraphEdge>,
    pub audio_summary: String,
}

impl From<core::graph::GraphNeighborhood> for GraphNeighborhood {
    fn from(n: core::graph::GraphNeighborhood) -> Self {
        GraphNeighborhood {
            center_id: n.center_id,
            depth: n.depth,
            nodes: n.nodes.into_iter().map(Into::into).collect(),
            edges: n.edges.into_iter().map(Into::into).collect(),
            audio_summary: n.audio_summary,
        }
    }
}

// --- Layout surface (Milestone P #558) ---------------------------------

/// The four Obsidian-parity force sliders (each `0.0..=1.0`, default
/// `0.5`), mapped 1:1 to `slate_core::graph_layout::LayoutForces`. The
/// mapping to physical constants is normative in p2_spec §P2-1.
#[derive(Debug, Clone, Copy, uniffi::Record)]
pub struct LayoutForces {
    /// Gravity toward the origin (frames disconnected components).
    #[uniffi(default = 0.5)]
    pub center: f32,
    /// Pairwise repulsion strength.
    #[uniffi(default = 0.5)]
    pub repel: f32,
    /// Per-edge attraction strength.
    #[uniffi(default = 0.5)]
    pub link: f32,
    /// Ideal edge length (maps to `k`).
    #[uniffi(default = 0.5)]
    pub link_distance: f32,
}

/// A non-finite slider value from the foreign side would poison the
/// whole force pass (NaN `link_distance` ⇒ NaN `k` ⇒ every position NaN,
/// breaking P2-1's no-NaN/Inf invariant), so we defensively fold NaN/±∞
/// back to the `0.5` default at the boundary. Finite out-of-range values
/// are left for the kernel's own `clamp(0.0, 1.0)`.
fn finite_or_default(v: f32) -> f32 {
    if v.is_finite() { v } else { 0.5 }
}

impl From<LayoutForces> for core::graph_layout::LayoutForces {
    fn from(f: LayoutForces) -> Self {
        core::graph_layout::LayoutForces {
            center: finite_or_default(f.center),
            repel: finite_or_default(f.repel),
            link: finite_or_default(f.link),
            link_distance: finite_or_default(f.link_distance),
        }
    }
}

/// Solve budgets and the deterministic jitter seed, mirroring
/// `slate_core::graph_layout::LayoutConfig`. Defaults match the kernel:
/// seed 0, 300 cold iterations, 60 warm.
#[derive(Debug, Clone, Copy, uniffi::Record)]
pub struct LayoutConfig {
    /// Same seed ⇒ same layout (jitter derivation only).
    #[uniffi(default = 0)]
    pub seed: u64,
    /// Cold-solve iteration budget; also the per-call ceiling on extra
    /// iterations `run_to_convergence` will spend.
    #[uniffi(default = 300)]
    pub max_iterations: u32,
    /// Warm-start budget after a `refresh` re-seats nodes.
    #[uniffi(default = 60)]
    pub warm_iterations: u32,
}

impl From<LayoutConfig> for core::graph_layout::LayoutConfig {
    fn from(c: LayoutConfig) -> Self {
        core::graph_layout::LayoutConfig {
            seed: c.seed,
            max_iterations: c.max_iterations,
            warm_iterations: c.warm_iterations,
        }
    }
}

/// One position frame from a [`LayoutSession`] (#558). `positions` is
/// interleaved `x0,y0,x1,y1…` in `node_ids()` order — length is exactly
/// `2 × node count`; `f32` at the boundary, `f64` inside the kernel.
/// `generation` tags the graph the positions' ids belong to; a change
/// means `node_ids()`/`edges()` must be re-fetched (a `refresh` reported
/// topology churn).
#[derive(Debug, Clone, uniffi::Record)]
pub struct LayoutFrame {
    pub positions: Vec<f32>,
    pub iteration: u32,
    pub converged: bool,
    pub generation: u64,
}

/// A running force-directed layout over one filtered graph projection
/// (#558). The heavy state — positions, velocities, quadtree — lives
/// here on the Rust side; the FFI hands back only flat `LayoutFrame`
/// buffers. Every method is internally synchronized (one `Mutex`) and
/// safe to call from any thread, so the host can drive `tick`s from a
/// background task while the UI thread reads the last frame.
#[derive(uniffi::Object)]
pub struct LayoutSession {
    /// Kept alive so `refresh` can re-read the live graph under the core
    /// session's lock order (conn → graph).
    session: Arc<VaultSession>,
    /// The projection this layout is bound to; re-applied on `refresh`.
    filter: core::graph::GraphFilter,
    state: std::sync::Mutex<LayoutState>,
    _census: census_live::Marker,
}

/// Mutable layout state behind the session's mutex. `ids`/`edges`/
/// `generation` are the last-synced topology; `id_to_slot` maps a
/// backend node id to its position slot for `pin_node`/`unpin_node`.
struct LayoutState {
    engine: core::graph_layout::LayoutEngine,
    ids: Vec<u64>,
    id_to_slot: std::collections::HashMap<u64, usize>,
    edges: Vec<GraphEdge>,
    /// Full node metadata in `ids` order, computed atomically with the
    /// topology under one graph lock (#559) — so the diagram's labels
    /// can never come from a different generation than its ids.
    nodes: Vec<GraphNode>,
    generation: u64,
    /// Result of the most recent step; reset to `false` whenever the
    /// layout is perturbed (forces changed, node pinned, topology
    /// re-synced) so a frame never claims convergence it hasn't earned.
    converged: bool,
    /// Per-call ceiling on extra iterations `run_to_convergence` spends
    /// (the config's cold budget) — bounds work whether the engine is
    /// cold (iteration 0) or warm (iteration already high after a
    /// refresh).
    max_iterations: u32,
    /// Test-only seam: when set, `run_to_convergence` fires a cancel
    /// exactly once, AFTER a top-of-loop cancel check has already passed,
    /// so a test can deterministically exercise the "a cancel that arrives
    /// mid-flight still lets at most one ≤10 chunk finish" bound without
    /// racing an unsynchronized thread.
    #[cfg(test)]
    cancel_after_next_check: bool,
}

impl LayoutState {
    fn new(
        engine: core::graph_layout::LayoutEngine,
        topology: core::graph_layout::LayoutTopology,
        max_iterations: u32,
    ) -> Self {
        let mut state = LayoutState {
            engine,
            ids: Vec::new(),
            id_to_slot: std::collections::HashMap::new(),
            edges: Vec::new(),
            nodes: Vec::new(),
            generation: 0,
            converged: false,
            max_iterations,
            #[cfg(test)]
            cancel_after_next_check: false,
        };
        state.apply_topology(topology);
        state
    }

    fn apply_topology(&mut self, topology: core::graph_layout::LayoutTopology) {
        self.id_to_slot = topology
            .ids
            .iter()
            .enumerate()
            .map(|(slot, &id)| (id, slot))
            .collect();
        self.ids = topology.ids;
        self.edges = topology.edges.into_iter().map(GraphEdge::from).collect();
        self.nodes = topology.nodes.into_iter().map(GraphNode::from).collect();
        self.generation = topology.generation;
    }

    fn frame(&self) -> LayoutFrame {
        let mut positions = Vec::with_capacity(self.engine.node_count() * 2);
        for &[x, y] in self.engine.positions() {
            positions.push(x as f32);
            positions.push(y as f32);
        }
        LayoutFrame {
            positions,
            iteration: self.engine.iteration(),
            converged: self.converged,
            generation: self.generation,
        }
    }
}

#[uniffi::export]
impl LayoutSession {
    /// Backend node ids in position order — `node_ids()[i]` names the
    /// node whose coordinates are `positions[2i], positions[2i+1]` in
    /// every frame. Fetch once and cache; re-fetch only after a
    /// `refresh` reports topology change (the frame's `generation`
    /// moved).
    pub fn node_ids(&self) -> Vec<u64> {
        self.state.lock().expect("layout state mutex").ids.clone()
    }

    /// The collapsed edges of this projection (deterministic order).
    /// Same caching contract as [`Self::node_ids`].
    pub fn edges(&self) -> Vec<GraphEdge> {
        self.state.lock().expect("layout state mutex").edges.clone()
    }

    /// Full node metadata (labels, link counts, kind, path, metrics) in
    /// `node_ids()` order — computed ATOMICALLY with the topology under
    /// one graph lock at build/refresh, so the host never has to pair it
    /// with a separately-fetched snapshot that could belong to a
    /// different generation (#559). Same fetch-once/re-fetch-on-change
    /// contract as [`Self::node_ids`].
    pub fn node_metadata(&self) -> Vec<GraphNode> {
        self.state.lock().expect("layout state mutex").nodes.clone()
    }

    /// The graph generation the current `node_ids()`/`edges()`/
    /// `node_metadata()` belong to. The host tags its model with this so
    /// it can drop stale frames after a `refresh` reassigns ids.
    pub fn generation(&self) -> u64 {
        self.state.lock().expect("layout state mutex").generation
    }

    /// Advance the simulation `iterations` steps and return the new
    /// frame. The host's interactive cadence is `tick(20)` per display
    /// frame while sliders are engaged or a settle animation runs,
    /// stopping once `converged`.
    pub fn tick(&self, iterations: u32) -> LayoutFrame {
        let mut state = self.state.lock().expect("layout state mutex");
        let report = state.engine.step(iterations);
        state.converged = report.converged;
        state.frame()
    }

    /// Run until the deterministic convergence predicate holds, `cancel`
    /// fires, or the per-call iteration ceiling is reached — checking
    /// `cancel` every 10 iterations. This is the Reduce-Motion path: one
    /// settled frame instead of an animated drift.
    pub fn run_to_convergence(&self, cancel: Arc<CancelToken>) -> LayoutFrame {
        let mut state = self.state.lock().expect("layout state mutex");
        let start = state.engine.iteration();
        let cap = state.max_iterations;
        loop {
            if cancel.inner.is_cancelled() {
                break;
            }
            // Test seam: simulate a cancel that lands the instant AFTER the
            // check above passed. The in-flight chunk must still complete
            // (≤10), then the next top-of-loop check stops the run.
            #[cfg(test)]
            if state.cancel_after_next_check {
                state.cancel_after_next_check = false;
                cancel.inner.cancel();
            }
            // Size the chunk to the remaining budget FIRST, so the total
            // never overshoots `cap` (a `cap` not divisible by 10 — or 0,
            // or 1 — must still be honored exactly), while never stepping
            // more than 10 between cancel checks.
            let elapsed = state.engine.iteration().saturating_sub(start);
            let remaining = cap.saturating_sub(elapsed);
            if remaining == 0 {
                break;
            }
            let report = state.engine.step(remaining.min(10));
            state.converged = report.converged;
            if report.converged {
                break;
            }
        }
        state.frame()
    }

    /// Retune the forces live and re-heat to the warm temperature (the
    /// slider-drag path). The next `tick`s settle into the new field.
    pub fn set_forces(&self, forces: LayoutForces) {
        let mut state = self.state.lock().expect("layout state mutex");
        state.engine.set_forces(forces.into());
        state.converged = false;
    }

    /// Pin `id` at `(x, y)`: it stops accumulating displacement but
    /// still repels its neighbors. Unknown ids (e.g. filtered out, or
    /// from a stale generation) are ignored, as are non-finite
    /// coordinates — pinning to NaN/∞ would plant that value directly in
    /// the next frame and contaminate neighbors through the force pass.
    pub fn pin_node(&self, id: u64, x: f32, y: f32) {
        if !x.is_finite() || !y.is_finite() {
            return;
        }
        let mut state = self.state.lock().expect("layout state mutex");
        if let Some(&slot) = state.id_to_slot.get(&id) {
            state.engine.pin(slot, f64::from(x), f64::from(y));
            state.converged = false;
        }
    }

    /// Release a previously pinned `id`. Unknown ids are ignored.
    pub fn unpin_node(&self, id: u64) {
        let mut state = self.state.lock().expect("layout state mutex");
        if let Some(&slot) = state.id_to_slot.get(&id) {
            state.engine.unpin(slot);
            state.converged = false;
        }
    }

    /// Re-sync with the live `GraphIndex`. Returns `None` when the graph
    /// generation is unchanged (a cheap probe — no work done), otherwise
    /// carries surviving nodes' positions over, seats newcomers, re-heats
    /// the changed neighborhood, and returns the post-`warm_update`
    /// frame. On any change the caller MUST re-fetch `node_ids()` /
    /// `edges()`: the returned frame's `generation` moved and ids may
    /// have been reassigned.
    pub fn refresh(&self) -> Result<Option<LayoutFrame>, VaultError> {
        let mut state = self.state.lock().expect("layout state mutex");
        let last_generation = state.generation;
        match self
            .session
            .inner
            .refresh_layout(&mut state.engine, self.filter, last_generation)?
        {
            None => Ok(None),
            Some((topology, _warm)) => {
                state.apply_topology(topology);
                state.converged = false;
                Ok(Some(state.frame()))
            }
        }
    }
}

#[cfg(test)]
impl LayoutSession {
    /// Arm the one-shot cancel seam (see `LayoutState::cancel_after_next_check`).
    fn arm_cancel_after_next_check(&self) {
        self.state
            .lock()
            .expect("layout state mutex")
            .cancel_after_next_check = true;
    }
}

impl From<core::FileChangeEvent> for FileChangeEvent {
    fn from(e: core::FileChangeEvent) -> Self {
        Self {
            kind: match e.kind {
                core::FileChangeKind::Created => FileChangeKind::Created,
                core::FileChangeKind::Modified => FileChangeKind::Modified,
                core::FileChangeKind::Deleted => FileChangeKind::Deleted,
                core::FileChangeKind::Renamed => FileChangeKind::Renamed,
            },
            path: e.path,
            previous_path: e.previous_path,
        }
    }
}

/// Index lifecycle phases (#802). Additive-only.
#[derive(Debug, Clone, uniffi::Enum)]
pub enum IndexPhase {
    ScanStarted,
    ReconcileStarted,
    ReconcileFinished,
    ScanFinished,
}

impl From<core::IndexPhase> for IndexPhase {
    fn from(p: core::IndexPhase) -> Self {
        match p {
            core::IndexPhase::ScanStarted => IndexPhase::ScanStarted,
            core::IndexPhase::ReconcileStarted => IndexPhase::ReconcileStarted,
            core::IndexPhase::ReconcileFinished => IndexPhase::ReconcileFinished,
            core::IndexPhase::ScanFinished => IndexPhase::ScanFinished,
        }
    }
}

/// Session events: errors (O-2 #540) plus, since #802, file-change and
/// index-phase deliveries. Invoked on a background worker thread — and
/// the #802 methods may arrive with session locks held — so
/// implementations must be cheap and non-blocking, marshal to their
/// main actor themselves, and NEVER call session APIs synchronously
/// from a callback (the `ScanProgressListener` contract). `message` is
/// user-facing copy and may name the vault path.
///
/// The filesystem watcher is a stub: file-change events cover Slate's
/// own write paths; external edits surface at the next scan.
#[uniffi::export(with_foreign)]
pub trait VaultEventListener: Send + Sync {
    fn on_error(&self, code: EventErrorCode, path: String, message: String);
    fn on_file_change(&self, event: FileChangeEvent);
    fn on_index_phase(&self, phase: IndexPhase, files_seen: u64);
}

/// Bridges core::VaultEventListener calls into the foreign-implemented
/// uniffi trait, converting payloads at the boundary.
struct VaultEventListenerAdapter {
    foreign: Arc<dyn VaultEventListener>,
}

impl core::VaultEventListener for VaultEventListenerAdapter {
    fn on_error(&self, code: core::EventErrorCode, path: String, message: String) {
        self.foreign.on_error(code.into(), path, message);
    }
    fn on_file_change(&self, event: core::FileChangeEvent) {
        self.foreign.on_file_change(event.into());
    }
    fn on_index_phase(&self, phase: core::IndexPhase, files_seen: u64) {
        self.foreign.on_index_phase(phase.into(), files_seen);
    }
}

/// Result of a successful `save_text`. Mirrors
/// `slate_core::SaveReport`.
#[derive(Debug, uniffi::Record)]
pub struct SaveReport {
    pub new_content_hash: String,
    pub new_size_bytes: u64,
    pub new_mtime_ms: i64,
}

impl From<core::SaveReport> for SaveReport {
    fn from(r: core::SaveReport) -> Self {
        Self {
            new_content_hash: r.new_content_hash,
            new_size_bytes: r.new_size_bytes,
            new_mtime_ms: r.new_mtime_ms,
        }
    }
}

/// Outcome of a `rename_property_across_vault` call. Mirrors
/// `slate_core::RenameReport`.
#[derive(Debug, Clone, uniffi::Record)]
pub struct RenameReport {
    pub affected: Vec<RenameAffected>,
    pub skipped: Vec<RenameSkipped>,
    pub failed: Vec<RenameFailed>,
}

impl From<core::RenameReport> for RenameReport {
    fn from(r: core::RenameReport) -> Self {
        Self {
            affected: r.affected.into_iter().map(Into::into).collect(),
            skipped: r.skipped.into_iter().map(Into::into).collect(),
            failed: r.failed.into_iter().map(Into::into).collect(),
        }
    }
}

#[derive(Debug, Clone, uniffi::Record)]
pub struct RenameAffected {
    pub path: String,
    pub before_excerpt: String,
    pub after_excerpt: String,
    /// `false` for dry-run results, `true` for successful applies.
    pub applied: bool,
    pub new_content_hash: Option<String>,
}

impl From<core::RenameAffected> for RenameAffected {
    fn from(a: core::RenameAffected) -> Self {
        Self {
            path: a.path,
            before_excerpt: a.before_excerpt,
            after_excerpt: a.after_excerpt,
            applied: a.applied,
            new_content_hash: a.new_content_hash,
        }
    }
}

#[derive(Debug, Clone, uniffi::Record)]
pub struct RenameSkipped {
    pub path: String,
    pub reason: RenameSkipReason,
}

impl From<core::RenameSkipped> for RenameSkipped {
    fn from(s: core::RenameSkipped) -> Self {
        Self {
            path: s.path,
            reason: s.reason.into(),
        }
    }
}

#[derive(Debug, Clone, uniffi::Enum)]
pub enum RenameSkipReason {
    NoSuchKey,
    KeyCollision,
    /// Rename would cross the `tags` key boundary with a list-shaped
    /// value, which would flip the type discriminator between `List`
    /// and `TagList` on round-trip. UI can offer a manual-edit
    /// fallback. Audit #180.
    TagsKeyTypeDrift,
}

impl From<core::RenameSkipReason> for RenameSkipReason {
    fn from(r: core::RenameSkipReason) -> Self {
        match r {
            core::RenameSkipReason::NoSuchKey => RenameSkipReason::NoSuchKey,
            core::RenameSkipReason::KeyCollision => RenameSkipReason::KeyCollision,
            core::RenameSkipReason::TagsKeyTypeDrift => RenameSkipReason::TagsKeyTypeDrift,
        }
    }
}

#[derive(Debug, Clone, uniffi::Record)]
pub struct RenameFailed {
    pub path: String,
    pub kind: RenameFailureKind,
    pub message: String,
}

impl From<core::RenameFailed> for RenameFailed {
    fn from(f: core::RenameFailed) -> Self {
        Self {
            path: f.path,
            kind: f.kind.into(),
            message: f.message,
        }
    }
}

/// Coarse classification of a per-file rename failure. The full
/// error text is in `RenameFailed::message`; this enum lets the UI
/// route to specific recovery flows (e.g. the conflict dialog)
/// without pattern-matching on display strings.
#[derive(Debug, Clone, uniffi::Enum)]
pub enum RenameFailureKind {
    WriteConflict,
    MalformedFrontmatter,
    Cancelled,
    Other,
}

impl From<core::RenameFailureKind> for RenameFailureKind {
    fn from(k: core::RenameFailureKind) -> Self {
        match k {
            core::RenameFailureKind::WriteConflict => RenameFailureKind::WriteConflict,
            core::RenameFailureKind::MalformedFrontmatter => {
                RenameFailureKind::MalformedFrontmatter
            }
            core::RenameFailureKind::Cancelled => RenameFailureKind::Cancelled,
            core::RenameFailureKind::Other => RenameFailureKind::Other,
        }
    }
}

// --- Embed resolution (Milestone J / #185) ---------------------------

/// Raw bytes returned by `read_attachment`. Mirrors
/// `slate_core::AttachmentBytes`. `bytes` crosses FFI as `Data`
/// (Swift) / `ByteArray` (Kotlin); UI uses it directly for image
/// rendering.
#[derive(Debug, Clone, uniffi::Record)]
pub struct AttachmentBytes {
    pub bytes: Vec<u8>,
    pub mime: String,
}

impl From<core::AttachmentBytes> for AttachmentBytes {
    fn from(a: core::AttachmentBytes) -> Self {
        Self {
            bytes: a.bytes,
            mime: a.mime,
        }
    }
}

/// FFI mirror of `slate_core::EmbedResolution`. Variants carry the
/// same data the resolver produces — including the pre-resolved
/// `nested` tree on `FullNote` / `Section` so the UI never needs
/// to recurse manually. Recursive via `NestedEmbed`, the same
/// pattern `PropertyValue::List` validated for UniFFI.
#[derive(Debug, Clone, uniffi::Enum)]
pub enum EmbedResolution {
    FullNote {
        target_path: String,
        text: String,
        nested: Vec<NestedEmbed>,
    },
    Section {
        target_path: String,
        heading: String,
        text: String,
        nested: Vec<NestedEmbed>,
    },
    Block {
        target_path: String,
        block_id: String,
        text: String,
    },
    Image {
        target_path: String,
        bytes: Vec<u8>,
        mime: String,
        alt: Option<String>,
    },
    Unresolved {
        reason: EmbedUnresolvedReason,
    },
}

impl From<core::EmbedResolution> for EmbedResolution {
    fn from(r: core::EmbedResolution) -> Self {
        match r {
            core::EmbedResolution::FullNote {
                target_path,
                text,
                nested,
            } => EmbedResolution::FullNote {
                target_path,
                text,
                nested: nested.into_iter().map(Into::into).collect(),
            },
            core::EmbedResolution::Section {
                target_path,
                heading,
                text,
                nested,
            } => EmbedResolution::Section {
                target_path,
                heading,
                text,
                nested: nested.into_iter().map(Into::into).collect(),
            },
            core::EmbedResolution::Block {
                target_path,
                block_id,
                text,
            } => EmbedResolution::Block {
                target_path,
                block_id,
                text,
            },
            core::EmbedResolution::Image {
                target_path,
                bytes,
                mime,
                alt,
            } => EmbedResolution::Image {
                target_path,
                bytes,
                mime,
                alt,
            },
            core::EmbedResolution::Unresolved { reason } => EmbedResolution::Unresolved {
                reason: reason.into(),
            },
        }
    }
}

#[derive(Debug, Clone, uniffi::Record)]
pub struct NestedEmbed {
    pub raw_target: String,
    pub byte_offset_in_parent: u32,
    pub resolution: EmbedResolution,
}

impl From<core::NestedEmbed> for NestedEmbed {
    fn from(n: core::NestedEmbed) -> Self {
        Self {
            raw_target: n.raw_target,
            byte_offset_in_parent: n.byte_offset_in_parent,
            resolution: n.resolution.into(),
        }
    }
}

#[derive(Debug, Clone, uniffi::Enum)]
pub enum EmbedUnresolvedReason {
    TargetNotFound {
        target: String,
    },
    HeadingNotFound {
        target_path: String,
        heading: String,
    },
    BlockNotFound {
        target_path: String,
        block_id: String,
    },
    DepthLimitReached,
    ReadError {
        message: String,
    },
}

impl From<core::EmbedUnresolvedReason> for EmbedUnresolvedReason {
    fn from(r: core::EmbedUnresolvedReason) -> Self {
        match r {
            core::EmbedUnresolvedReason::TargetNotFound { target } => {
                EmbedUnresolvedReason::TargetNotFound { target }
            }
            core::EmbedUnresolvedReason::HeadingNotFound {
                target_path,
                heading,
            } => EmbedUnresolvedReason::HeadingNotFound {
                target_path,
                heading,
            },
            core::EmbedUnresolvedReason::BlockNotFound {
                target_path,
                block_id,
            } => EmbedUnresolvedReason::BlockNotFound {
                target_path,
                block_id,
            },
            core::EmbedUnresolvedReason::DepthLimitReached => {
                EmbedUnresolvedReason::DepthLimitReached
            }
            core::EmbedUnresolvedReason::ReadError { message } => {
                EmbedUnresolvedReason::ReadError { message }
            }
        }
    }
}

/// Kind of operation recorded in an op-log entry (#378, #372, O-1 #539).
///
/// `WholeFileReplace`'s `payload_bytes` is the full file; `EditBatch`'s
/// is the encoded fine-grained Insert/Delete/Replace op-vector for one
/// save; `Annotated` wraps one of those plus the save's semantic
/// annotations. Hosts decode `EditBatch` payloads with
/// [`decode_edit_batch_ops`] and `Annotated` payloads with
/// [`decode_annotated_payload`] (O-1's per-op accessors). `CanvasApply`
/// payloads (the JSON `{name, action, inverse}` record) remain opaque.
#[derive(Debug, Clone, uniffi::Enum)]
pub enum OpKind {
    WholeFileReplace,
    EditBatch,
    /// One committed canvas action (Milestone T #372): payload is the
    /// JSON `{name, action, inverse}` record. Opaque to hosts.
    CanvasApply,
    /// An annotated wrapper around a single inner snapshot/batch entry
    /// (O-1 #539) — one atomic entry per save, intent attached. Decode
    /// with [`decode_annotated_payload`].
    Annotated,
}

impl From<core::OpKind> for OpKind {
    fn from(k: core::OpKind) -> Self {
        match k {
            core::OpKind::WholeFileReplace => OpKind::WholeFileReplace,
            core::OpKind::EditBatch => OpKind::EditBatch,
            core::OpKind::CanvasApply => OpKind::CanvasApply,
            core::OpKind::Annotated => OpKind::Annotated,
        }
    }
}

/// One fine-grained edit within an `EditBatch` payload. Mirrors
/// `slate_core::EditOp`; offsets are UTF-8 **byte** offsets in the
/// OLD-content space (usize → u64 for the FFI).
#[derive(Debug, Clone, uniffi::Enum)]
pub enum EditOp {
    Insert { pos: u64, text: String },
    Delete { start: u64, end: u64 },
    Replace { start: u64, end: u64, text: String },
}

impl From<core::EditOp> for EditOp {
    fn from(op: core::EditOp) -> Self {
        match op {
            core::EditOp::Insert { pos, text } => EditOp::Insert {
                pos: pos as u64,
                text,
            },
            core::EditOp::Delete { start, end } => EditOp::Delete {
                start: start as u64,
                end: end as u64,
            },
            core::EditOp::Replace { start, end, text } => EditOp::Replace {
                start: start as u64,
                end: end as u64,
                text,
            },
        }
    }
}

/// Semantic intent recorded alongside a save (O-1 #539). Mirrors
/// `slate_core::OpAnnotation`; `new_status` is a one-scalar String
/// (uniffi has no char primitive — the `TaskItem::status` precedent).
#[derive(Debug, Clone, uniffi::Enum)]
pub enum OpAnnotation {
    SetProperty { key: String, value_json: String },
    RemoveProperty { key: String },
    ToggleTask { ordinal: u32, new_status: String },
    FrontmatterReplace,
    PathChanged { from: String, to: String },
}

impl From<core::OpAnnotation> for OpAnnotation {
    fn from(a: core::OpAnnotation) -> Self {
        match a {
            core::OpAnnotation::SetProperty { key, value_json } => {
                OpAnnotation::SetProperty { key, value_json }
            }
            core::OpAnnotation::RemoveProperty { key } => OpAnnotation::RemoveProperty { key },
            core::OpAnnotation::ToggleTask {
                ordinal,
                new_status,
            } => OpAnnotation::ToggleTask {
                ordinal,
                new_status: new_status.to_string(),
            },
            core::OpAnnotation::FrontmatterReplace => OpAnnotation::FrontmatterReplace,
            core::OpAnnotation::PathChanged { from, to } => OpAnnotation::PathChanged { from, to },
        }
    }
}

/// A decoded `Annotated` payload: the wrapped inner entry plus its
/// annotations. `inner_payload` is interpreted per `inner_kind`
/// exactly like a bare entry's payload (a wrapped `EditBatch` feeds
/// [`decode_edit_batch_ops`]).
/// Operation classes for structured diffs (O-4 #542). Mirrors
/// `slate_core::DiffOpClass`.
#[derive(Debug, Clone, uniffi::Enum)]
pub enum DiffOpClass {
    HeadingAdded,
    HeadingRemoved,
    HeadingEdited,
    PropertySet,
    PropertyRemoved,
    ParagraphAdded,
    ParagraphRemoved,
    ParagraphEdited,
    ListItemAdded,
    ListItemRemoved,
    ListItemEdited,
    TaskStatusChanged,
    CodeBlockEdited,
    MathBlockEdited,
    DiagramEdited,
    TableEdited,
    Other,
}

impl From<core::DiffOpClass> for DiffOpClass {
    fn from(k: core::DiffOpClass) -> Self {
        use core::DiffOpClass as C;
        match k {
            C::HeadingAdded => DiffOpClass::HeadingAdded,
            C::HeadingRemoved => DiffOpClass::HeadingRemoved,
            C::HeadingEdited => DiffOpClass::HeadingEdited,
            C::PropertySet => DiffOpClass::PropertySet,
            C::PropertyRemoved => DiffOpClass::PropertyRemoved,
            C::ParagraphAdded => DiffOpClass::ParagraphAdded,
            C::ParagraphRemoved => DiffOpClass::ParagraphRemoved,
            C::ParagraphEdited => DiffOpClass::ParagraphEdited,
            C::ListItemAdded => DiffOpClass::ListItemAdded,
            C::ListItemRemoved => DiffOpClass::ListItemRemoved,
            C::ListItemEdited => DiffOpClass::ListItemEdited,
            C::TaskStatusChanged => DiffOpClass::TaskStatusChanged,
            C::CodeBlockEdited => DiffOpClass::CodeBlockEdited,
            C::MathBlockEdited => DiffOpClass::MathBlockEdited,
            C::DiagramEdited => DiffOpClass::DiagramEdited,
            C::TableEdited => DiffOpClass::TableEdited,
            C::Other => DiffOpClass::Other,
        }
    }
}

/// One named diff operation (O-4 #542). Mirrors
/// `slate_core::DiffOperation`.
#[derive(Debug, Clone, uniffi::Record)]
pub struct DiffOperation {
    pub kind: DiffOpClass,
    pub line: u32,
    pub line_end: u32,
    pub semantic_description: String,
    pub detail: Option<String>,
}

impl From<core::DiffOperation> for DiffOperation {
    fn from(op: core::DiffOperation) -> Self {
        Self {
            kind: op.kind.into(),
            line: op.line,
            line_end: op.line_end,
            semantic_description: op.semantic_description,
            detail: op.detail,
        }
    }
}

/// One structured diff (O-4 #542). Mirrors `slate_core::StructuredDiff`.
#[derive(Debug, Clone, uniffi::Record)]
pub struct StructuredDiff {
    pub file_path: String,
    pub from_hash: String,
    pub to_hash: String,
    pub operations: Vec<DiffOperation>,
    pub audio_summary: String,
}

impl From<core::StructuredDiff> for StructuredDiff {
    fn from(d: core::StructuredDiff) -> Self {
        Self {
            file_path: d.file_path,
            from_hash: d.from_hash,
            to_hash: d.to_hash,
            operations: d.operations.into_iter().map(Into::into).collect(),
            audio_summary: d.audio_summary,
        }
    }
}

/// The changes-since-last-open verdict (O-4 #542). Mirrors
/// `slate_core::ChangesSinceOpen`.
#[derive(Debug, Clone, uniffi::Enum)]
pub enum ChangesSinceOpen {
    NoBaseline,
    Unchanged,
    Diff { diff: StructuredDiff },
    BaselineCompacted,
}

impl From<core::ChangesSinceOpen> for ChangesSinceOpen {
    fn from(c: core::ChangesSinceOpen) -> Self {
        match c {
            core::ChangesSinceOpen::NoBaseline => ChangesSinceOpen::NoBaseline,
            core::ChangesSinceOpen::Unchanged => ChangesSinceOpen::Unchanged,
            core::ChangesSinceOpen::Diff(diff) => ChangesSinceOpen::Diff { diff: diff.into() },
            core::ChangesSinceOpen::BaselineCompacted => ChangesSinceOpen::BaselineCompacted,
        }
    }
}

#[derive(Debug, uniffi::Record)]
pub struct AnnotatedPayload {
    pub inner_kind: OpKind,
    pub inner_payload: Vec<u8>,
    pub annotations: Vec<OpAnnotation>,
}

/// Decode an `EditBatch` payload into its typed ops (O-1 #539's
/// per-op accessor — `EditBatch` payloads are no longer host-opaque).
/// A malformed/truncated payload is `InvalidArgument`, never a panic.
#[uniffi::export]
pub fn decode_edit_batch_ops(payload: Vec<u8>) -> Result<Vec<EditOp>, VaultError> {
    core::decode_edit_batch(&payload)
        .map(|ops| ops.into_iter().map(EditOp::from).collect())
        .map_err(|message| VaultError::InvalidArgument { message })
}

/// One annotation on a version row (O-3 #541). Mirrors
/// `slate_core::OpAnnotationSummary`.
#[derive(Debug, Clone, uniffi::Record)]
pub struct OpAnnotationSummary {
    pub kind: String,
    pub display: String,
}

impl From<core::OpAnnotationSummary> for OpAnnotationSummary {
    fn from(a: core::OpAnnotationSummary) -> Self {
        Self {
            kind: a.kind,
            display: a.display,
        }
    }
}

/// One version-history row (O-3 #541). Mirrors
/// `slate_core::VersionSummary` — `position_from_tail` is the ROW
/// identity (hashes repeat across A→B→A histories);
/// `content_hash_after` is the CONTENT identity used for
/// `version_content`/`restore_version`.
#[derive(Debug, Clone, uniffi::Record)]
pub struct VersionSummary {
    pub position_from_tail: u32,
    pub content_hash_after: String,
    pub timestamp_ms: i64,
    pub op_kind: OpKind,
    pub op_count: u32,
    pub byte_delta: i64,
    pub annotations: Vec<OpAnnotationSummary>,
    pub is_marker: bool,
    pub audio_fragment: String,
}

impl From<core::VersionSummary> for VersionSummary {
    fn from(v: core::VersionSummary) -> Self {
        Self {
            position_from_tail: v.position_from_tail,
            content_hash_after: v.content_hash_after,
            timestamp_ms: v.timestamp_ms,
            op_kind: v.op_kind.into(),
            op_count: v.op_count,
            byte_delta: v.byte_delta,
            annotations: v.annotations.into_iter().map(Into::into).collect(),
            is_marker: v.is_marker,
            audio_fragment: v.audio_fragment,
        }
    }
}

/// The `.slate/prefs.json` `history` section (O-5 #543). Mirrors
/// `slate_core::HistoryPrefs`.
#[derive(Debug, Clone, Copy, uniffi::Record)]
pub struct HistoryPrefs {
    /// Op-log retention window in days (UI offers 30/90/180/365;
    /// any non-zero value is accepted).
    pub retention_days: u32,
}

impl From<core::HistoryPrefs> for HistoryPrefs {
    fn from(p: core::HistoryPrefs) -> Self {
        Self {
            retention_days: p.retention_days,
        }
    }
}

impl From<HistoryPrefs> for core::HistoryPrefs {
    fn from(p: HistoryPrefs) -> Self {
        Self {
            retention_days: p.retention_days,
        }
    }
}

/// One page of version history (uniffi doesn't take generics — the
/// concrete `Page<VersionSummary>` instantiation).
#[derive(Debug, uniffi::Record)]
pub struct VersionSummaryPage {
    pub items: Vec<VersionSummary>,
    pub next_cursor: Option<String>,
    pub total_filtered: u64,
}

impl From<core::Page<core::VersionSummary>> for VersionSummaryPage {
    fn from(p: core::Page<core::VersionSummary>) -> Self {
        Self {
            items: p.items.into_iter().map(Into::into).collect(),
            next_cursor: p.next_cursor,
            total_filtered: p.total_filtered,
        }
    }
}

/// One recoverable deleted file (O-3 #541). Mirrors
/// `slate_core::DeletedFileEntry`.
#[derive(Debug, Clone, uniffi::Record)]
pub struct DeletedFileEntry {
    pub path: String,
    pub deleted_at_ms: Option<i64>,
    pub recoverable: bool,
    pub size_bytes: Option<u64>,
}

impl From<core::DeletedFileEntry> for DeletedFileEntry {
    fn from(e: core::DeletedFileEntry) -> Self {
        Self {
            path: e.path,
            deleted_at_ms: e.deleted_at_ms,
            recoverable: e.recoverable,
            size_bytes: e.size_bytes,
        }
    }
}

/// One page of deleted files (concrete `Page<DeletedFileEntry>`).
#[derive(Debug, uniffi::Record)]
pub struct DeletedFilePage {
    pub items: Vec<DeletedFileEntry>,
    pub next_cursor: Option<String>,
    pub total_filtered: u64,
}

impl From<core::Page<core::DeletedFileEntry>> for DeletedFilePage {
    fn from(p: core::Page<core::DeletedFileEntry>) -> Self {
        Self {
            items: p.items.into_iter().map(Into::into).collect(),
            next_cursor: p.next_cursor,
            total_filtered: p.total_filtered,
        }
    }
}

/// Decode an `Annotated` payload into its inner entry + annotations
/// (O-1 #539). A malformed/truncated payload is `InvalidArgument`,
/// never a panic; unknown annotation tags were already skipped by the
/// decoder (forward-extensible vocabulary).
#[uniffi::export]
pub fn decode_annotated_payload(payload: Vec<u8>) -> Result<AnnotatedPayload, VaultError> {
    core::decode_annotated(&payload)
        .map(
            |(inner_kind, inner_payload, annotations)| AnnotatedPayload {
                inner_kind: inner_kind.into(),
                inner_payload,
                annotations: annotations.into_iter().map(OpAnnotation::from).collect(),
            },
        )
        .map_err(|message| VaultError::InvalidArgument { message })
}

/// One recorded op-log entry. Mirrors `slate_core::OpLogEntry`.
#[derive(Debug, uniffi::Record)]
pub struct OpLogEntry {
    pub timestamp_ms: i64,
    pub user_actor_id: String,
    pub op_kind: OpKind,
    pub content_hash_before: String,
    pub content_hash_after: String,
    pub payload_bytes: Vec<u8>,
}

impl From<core::OpLogEntry> for OpLogEntry {
    fn from(e: core::OpLogEntry) -> Self {
        Self {
            timestamp_ms: e.timestamp_ms,
            user_actor_id: e.user_actor_id,
            op_kind: e.op_kind.into(),
            content_hash_before: e.content_hash_before,
            content_hash_after: e.content_hash_after,
            payload_bytes: e.payload_bytes,
        }
    }
}

// =====================================================================
// Tasks FFI surface (Milestone G)
// =====================================================================

/// One parsed Markdown task. Mirrors `slate_core::TaskItem` with the
/// status char encoded as a String (uniffi has no char primitive).
#[derive(Debug, Clone, uniffi::Record)]
pub struct TaskItem {
    pub ordinal: u32,
    pub text: String,
    /// Raw status character between `[` and `]` (e.g. `" "`, `"x"`,
    /// `"/"`). Always exactly one Unicode scalar; modelled as a
    /// String so foreign languages without a `char` type don't have
    /// to invent one.
    pub status_char: String,
    pub completed: bool,
    pub due_ms: Option<i64>,
    pub scheduled_ms: Option<i64>,
    pub priority: Option<i32>,
    pub recurrence: Option<String>,
    /// 1-based line number in the source.
    pub line: u32,
    /// Byte offset of the task's line start.
    pub byte_offset: u32,
}

impl From<core::TaskItem> for TaskItem {
    fn from(t: core::TaskItem) -> Self {
        Self {
            ordinal: t.ordinal,
            text: t.text,
            status_char: t.status_char.to_string(),
            completed: t.completed,
            due_ms: t.due_ms,
            scheduled_ms: t.scheduled_ms,
            priority: t.priority,
            recurrence: t.recurrence,
            line: t.line,
            byte_offset: t.byte_offset,
        }
    }
}

/// Task plus the file it lives in, for the vault-wide review view.
#[derive(Debug, Clone, uniffi::Record)]
pub struct TaskWithLocation {
    pub task: TaskItem,
    pub path: String,
    pub file_name: String,
}

impl From<core::TaskWithLocation> for TaskWithLocation {
    fn from(t: core::TaskWithLocation) -> Self {
        Self {
            task: t.task.into(),
            path: t.path,
            file_name: t.file_name,
        }
    }
}

/// Filter shape for `tasks_in_vault`. `None` fields mean "no
/// restriction on this axis."
#[derive(Debug, Clone, uniffi::Record)]
pub struct TaskFilter {
    /// `None` = both open and done; `Some(false)` = only open;
    /// `Some(true)` = only done.
    pub completed: Option<bool>,
    /// Inclusive lower bound for `due_ms`.
    pub due_from_ms: Option<i64>,
    /// Exclusive upper bound for `due_ms`.
    pub due_to_ms: Option<i64>,
    /// Tasks with priority `>= this` (NULL priorities are excluded
    /// when this is `Some`).
    pub priority_at_least: Option<i32>,
}

impl From<TaskFilter> for core::TaskFilter {
    fn from(f: TaskFilter) -> Self {
        Self {
            completed: f.completed,
            due_from_ms: f.due_from_ms,
            due_to_ms: f.due_to_ms,
            priority_at_least: f.priority_at_least,
        }
    }
}

/// Paged result of `tasks_in_vault`. uniffi can't generate generic
/// `Page<T>`, so this is the concrete instantiation.
#[derive(Debug, Clone, uniffi::Record)]
pub struct TaskWithLocationPage {
    pub items: Vec<TaskWithLocation>,
    pub next_cursor: Option<String>,
    pub total_filtered: u64,
}

impl From<core::Page<core::TaskWithLocation>> for TaskWithLocationPage {
    fn from(p: core::Page<core::TaskWithLocation>) -> Self {
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

// =====================================================================
// Templates FFI surface (Milestone H)
// =====================================================================

use std::collections::HashMap;

/// Row in the template picker — what `list_templates` returns.
#[derive(Debug, Clone, uniffi::Record)]
pub struct TemplateSummary {
    /// Vault-relative path, e.g. `"Templates/Daily.md"`.
    pub path: String,
    /// File stem, e.g. `"Daily"`.
    pub name: String,
    /// Picker subtitle: frontmatter `description:`, else first non-blank
    /// non-frontmatter line, trimmed and truncated to 120 chars. `nil`
    /// when neither source produced any text.
    pub description: Option<String>,
}

impl From<core::TemplateSummary> for TemplateSummary {
    fn from(t: core::TemplateSummary) -> Self {
        Self {
            path: t.path,
            name: t.name,
            description: t.description,
        }
    }
}

/// A single prompt extracted from a template by
/// `extract_template_metadata`. The picker labels its text field with
/// `label` and uses `key` to stuff the user's response back into
/// `TemplateContext::prompt_values`.
#[derive(Debug, Clone, uniffi::Record)]
pub struct TemplatePrompt {
    pub key: String,
    pub label: String,
}

impl From<core::TemplatePrompt> for TemplatePrompt {
    fn from(p: core::TemplatePrompt) -> Self {
        Self {
            key: p.key,
            label: p.label,
        }
    }
}

/// Everything the UI needs to know up front about a template, before
/// it starts rendering. V1.H ships with `prompts` only; the struct
/// shape leaves room for additive fields without breaking foreign
/// callers.
#[derive(Debug, Clone, uniffi::Record)]
pub struct TemplateMetadata {
    pub prompts: Vec<TemplatePrompt>,
}

impl From<core::TemplateMetadata> for TemplateMetadata {
    fn from(m: core::TemplateMetadata) -> Self {
        Self {
            prompts: m.prompts.into_iter().map(TemplatePrompt::from).collect(),
        }
    }
}

/// Variable values supplied to `render_template`. Construct one per
/// render call. `prompt_values` is keyed by [`TemplatePrompt::key`],
/// not the raw label — the same dedup logic
/// `extract_template_metadata` ran is what produced those keys.
#[derive(Debug, Clone, uniffi::Record)]
pub struct TemplateContext {
    /// Reference time (Unix epoch millis) used for `{{date}}` /
    /// `{{time}}` and their `:FMT` variants. Always interpreted as UTC.
    pub now_ms: i64,
    /// Substituted for `{{title}}` — the new note's title.
    pub title: String,
    /// Substituted for `{{vault}}` — the vault root's basename.
    pub vault_name: String,
    /// Prompt responses keyed by [`TemplatePrompt::key`]. A missing key
    /// leaves the corresponding `{{prompt:Label}}` marker literal.
    pub prompt_values: HashMap<String, String>,
}

impl From<TemplateContext> for core::TemplateContext {
    fn from(c: TemplateContext) -> Self {
        Self {
            now_ms: c.now_ms,
            title: c.title,
            vault_name: c.vault_name,
            prompt_values: c.prompt_values,
        }
    }
}

/// Result of rendering a template.
#[derive(Debug, Clone, uniffi::Record)]
pub struct RenderedTemplate {
    /// Rendered template body, with allowlisted variables substituted
    /// and `{{cursor}}` markers stripped (their byte position is
    /// captured in `cursor_byte_offset`).
    pub body: String,
    /// Byte offset inside `body` where the editor should park the
    /// cursor. `nil` when the template carried no `{{cursor}}` marker.
    /// Indexed in bytes so the host can scan with byte-precise APIs;
    /// the offset always falls on a UTF-8 char boundary.
    pub cursor_byte_offset: Option<u64>,
}

impl From<core::RenderedTemplate> for RenderedTemplate {
    fn from(r: core::RenderedTemplate) -> Self {
        Self {
            body: r.body,
            cursor_byte_offset: r.cursor_byte_offset.map(|n| n as u64),
        }
    }
}

/// Extract prompt metadata from a template source.
///
/// The Mac UI's create-from-template flow is: read the template source
/// (via [`VaultSession::read_text`]), call this to learn which
/// `{{prompt:Label}}` markers to ask the user about, collect the
/// responses, then call [`VaultSession::render_template`] with a
/// [`TemplateContext`] carrying those responses keyed by
/// [`TemplatePrompt::key`].
///
/// Exposed as `extractTemplateMetadata(source:)` in Swift.
#[uniffi::export]
pub fn extract_template_metadata(source: String) -> TemplateMetadata {
    core::extract_template_metadata(&source).into()
}

// --- Milestone K content pipelines (#217 / #218 / #219) ---------------

// Math pipeline mirror.

#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum MathDisplayStyle {
    Inline,
    Block,
}

impl From<core::math::MathDisplayStyle> for MathDisplayStyle {
    fn from(v: core::math::MathDisplayStyle) -> Self {
        match v {
            core::math::MathDisplayStyle::Inline => MathDisplayStyle::Inline,
            core::math::MathDisplayStyle::Block => MathDisplayStyle::Block,
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum MathSpeechStyle {
    ClearSpeak,
    MathSpeak,
}

impl From<MathSpeechStyle> for core::math::MathSpeechStyle {
    fn from(v: MathSpeechStyle) -> Self {
        match v {
            MathSpeechStyle::ClearSpeak => core::math::MathSpeechStyle::ClearSpeak,
            MathSpeechStyle::MathSpeak => core::math::MathSpeechStyle::MathSpeak,
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum MathVerbosity {
    Terse,
    Medium,
    Verbose,
}

impl From<MathVerbosity> for core::math::MathVerbosity {
    fn from(v: MathVerbosity) -> Self {
        match v {
            MathVerbosity::Terse => core::math::MathVerbosity::Terse,
            MathVerbosity::Medium => core::math::MathVerbosity::Medium,
            MathVerbosity::Verbose => core::math::MathVerbosity::Verbose,
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum BrailleCode {
    Nemeth,
    Ueb,
}

impl From<BrailleCode> for core::math::BrailleCode {
    fn from(v: BrailleCode) -> Self {
        match v {
            BrailleCode::Nemeth => core::math::BrailleCode::Nemeth,
            BrailleCode::Ueb => core::math::BrailleCode::Ueb,
        }
    }
}

/// FFI mirror of `slate_core::math::MathPrefs`. Settings panel
/// (#224) drives this; `VaultSession::set_math_prefs` consumes it.
/// Audit #259.
#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Record)]
pub struct MathPrefs {
    pub speech_style: MathSpeechStyle,
    pub verbosity: MathVerbosity,
    pub braille_code: BrailleCode,
}

impl From<MathPrefs> for core::math::MathPrefs {
    fn from(p: MathPrefs) -> Self {
        core::math::MathPrefs {
            speech_style: p.speech_style.into(),
            verbosity: p.verbosity.into(),
            braille_code: p.braille_code.into(),
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct MathBlock {
    pub source: String,
    pub display_style: MathDisplayStyle,
    pub mathml: String,
    pub speech: String,
    pub braille: Vec<u8>,
    pub line: u32,
    pub byte_offset: u32,
}

impl From<core::math::MathBlock> for MathBlock {
    fn from(b: core::math::MathBlock) -> Self {
        Self {
            source: b.source,
            display_style: b.display_style.into(),
            mathml: b.mathml,
            speech: b.speech,
            braille: b.braille,
            line: b.line,
            byte_offset: b.byte_offset,
        }
    }
}

// Code pipeline mirror.

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Enum)]
pub enum TokenKind {
    Keyword,
    String,
    Number,
    Comment,
    Identifier,
    Type,
    Function,
    Operator,
    Punctuation,
    Other { label: String },
}

impl From<core::code::TokenKind> for TokenKind {
    fn from(k: core::code::TokenKind) -> Self {
        match k {
            core::code::TokenKind::Keyword => TokenKind::Keyword,
            core::code::TokenKind::String => TokenKind::String,
            core::code::TokenKind::Number => TokenKind::Number,
            core::code::TokenKind::Comment => TokenKind::Comment,
            core::code::TokenKind::Identifier => TokenKind::Identifier,
            core::code::TokenKind::Type => TokenKind::Type,
            core::code::TokenKind::Function => TokenKind::Function,
            core::code::TokenKind::Operator => TokenKind::Operator,
            core::code::TokenKind::Punctuation => TokenKind::Punctuation,
            core::code::TokenKind::Other(s) => TokenKind::Other { label: s },
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct SyntaxToken {
    pub start_byte: u32,
    pub end_byte: u32,
    pub kind: TokenKind,
}

impl From<core::code::SyntaxToken> for SyntaxToken {
    fn from(t: core::code::SyntaxToken) -> Self {
        Self {
            start_byte: t.start_byte,
            end_byte: t.end_byte,
            kind: t.kind.into(),
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum SemanticKind {
    Function,
    Type,
    Variable,
}

impl From<core::code::SemanticKind> for SemanticKind {
    fn from(k: core::code::SemanticKind) -> Self {
        match k {
            core::code::SemanticKind::Function => SemanticKind::Function,
            core::code::SemanticKind::Type => SemanticKind::Type,
            core::code::SemanticKind::Variable => SemanticKind::Variable,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct SemanticSpan {
    pub start_byte: u32,
    pub end_byte: u32,
    pub kind: SemanticKind,
    pub name: String,
}

impl From<core::code::SemanticSpan> for SemanticSpan {
    fn from(s: core::code::SemanticSpan) -> Self {
        Self {
            start_byte: s.start_byte,
            end_byte: s.end_byte,
            kind: s.kind.into(),
            name: s.name,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct CodeBlock {
    pub source: String,
    pub language: Option<String>,
    pub tokens: Vec<SyntaxToken>,
    pub semantic_spans: Vec<SemanticSpan>,
    pub line: u32,
    pub byte_offset: u32,
}

impl From<core::code::CodeBlock> for CodeBlock {
    fn from(b: core::code::CodeBlock) -> Self {
        Self {
            source: b.source,
            language: b.language,
            tokens: b.tokens.into_iter().map(Into::into).collect(),
            semantic_spans: b.semantic_spans.into_iter().map(Into::into).collect(),
            line: b.line,
            byte_offset: b.byte_offset,
        }
    }
}

// Editor syntax-span mirror (#377).

/// Classifies one editor highlight span. Payload variants carry the
/// heading level / inner code token; the rest are unit variants.
#[derive(Debug, Clone, PartialEq, Eq, uniffi::Enum)]
pub enum EditorSpanKind {
    Heading { level: u8 },
    Emphasis,
    Strong,
    Strikethrough,
    InlineCode,
    CodeFence,
    Link,
    Image,
    BlockQuote,
    Wikilink,
    Embed,
    Tag,
    Citation,
    Comment,
    Frontmatter,
    Code { token: TokenKind },
}

impl From<core::editor_spans::EditorSpanKind> for EditorSpanKind {
    fn from(k: core::editor_spans::EditorSpanKind) -> Self {
        use core::editor_spans::EditorSpanKind as K;
        match k {
            K::Heading(level) => EditorSpanKind::Heading { level },
            K::Emphasis => EditorSpanKind::Emphasis,
            K::Strong => EditorSpanKind::Strong,
            K::Strikethrough => EditorSpanKind::Strikethrough,
            K::InlineCode => EditorSpanKind::InlineCode,
            K::CodeFence => EditorSpanKind::CodeFence,
            K::Link => EditorSpanKind::Link,
            K::Image => EditorSpanKind::Image,
            K::BlockQuote => EditorSpanKind::BlockQuote,
            K::Wikilink => EditorSpanKind::Wikilink,
            K::Embed => EditorSpanKind::Embed,
            K::Tag => EditorSpanKind::Tag,
            K::Citation => EditorSpanKind::Citation,
            K::Comment => EditorSpanKind::Comment,
            K::Frontmatter => EditorSpanKind::Frontmatter,
            K::Code(token) => EditorSpanKind::Code {
                token: token.into(),
            },
        }
    }
}

/// One editor highlight span over the note's UTF-8 byte offsets.
#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct EditorSpan {
    pub start_byte: u32,
    pub end_byte: u32,
    pub kind: EditorSpanKind,
}

impl From<core::editor_spans::EditorSpan> for EditorSpan {
    fn from(s: core::editor_spans::EditorSpan) -> Self {
        Self {
            start_byte: s.start_byte,
            end_byte: s.end_byte,
            kind: s.kind.into(),
        }
    }
}

/// Compute the canonical editor highlight spans for a Markdown source
/// (#377, `05` §1.1/§1.2). Pure — no vault/session needed; the editor
/// calls this off the main thread (debounced) and stamps the spans as
/// temporary attributes. Offsets are UTF-8 byte offsets into `text`;
/// `Code` tokens may nest inside their `CodeFence` span. A ranged /
/// incremental variant is tracked as #379.
#[uniffi::export]
pub fn editor_highlight_spans(text: String) -> Vec<EditorSpan> {
    core::editor_spans::highlight_spans(&text)
        .into_iter()
        .map(Into::into)
        .collect()
}

/// Highlight spans recomputed for a window around an edit (#379, `05`
/// §1.1/§1.2). `spans` authoritatively cover **all** of
/// `[applied_start, applied_end)` in whole-document UTF-8 byte offsets —
/// the caller must remove its temporary attributes over that range before
/// re-adding these. When the window can't be parsed equivalently in
/// isolation (frontmatter / a straddled fence or `%%` comment / a `---`
/// at the window head), the core falls back to a whole-document parse and
/// signals it by returning `applied_start == 0 && applied_end ==
/// text.len()` with the full span set, so the consumer's apply path stays
/// uniform. `dirty_*` are UTF-8 byte offsets into the **post-edit** `text`
/// and are clamped + char-boundary-snapped by the core.
#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct RangedHighlight {
    pub applied_start: u32,
    pub applied_end: u32,
    pub spans: Vec<EditorSpan>,
}

#[uniffi::export]
pub fn editor_highlight_spans_in_range(
    text: String,
    dirty_start: u32,
    dirty_end: u32,
) -> RangedHighlight {
    let ranged = core::editor_spans::highlight_spans_in_range(
        &text,
        dirty_start as usize..dirty_end as usize,
    );
    RangedHighlight {
        applied_start: ranged.applied_range.start as u32,
        applied_end: ranged.applied_range.end as u32,
        spans: ranged.spans.into_iter().map(Into::into).collect(),
    }
}

/// Stateful editor document buffer (#404). Holds the note's text as a rope
/// across edits so the macOS editor feeds **edit deltas** (not the whole
/// string) per keystroke and gets O(log n) UTF-16 ↔ byte conversions + a
/// windowed highlight without re-marshalling the document. Wraps
/// [`core::doc_buffer::DocBufferState`] in a `Mutex`: uniffi does **not**
/// serialize `&self` Object calls, so the lock is what makes a concurrent
/// `apply_edit` (main thread) and `highlight_in_range` (background Task) safe.
/// `highlight_in_range` clones the rope (O(1) — `ropey` shares chunks via
/// `Arc`) under a short lock, then parses the snapshot lock-free, preserving
/// the editor's immutable-snapshot semantics.
#[derive(uniffi::Object)]
pub struct DocumentBuffer {
    inner: std::sync::Mutex<core::doc_buffer::DocBufferState>,
    _census: census_live::Marker,
}

#[uniffi::export]
impl DocumentBuffer {
    /// Build from the full document text (initial load / note switch).
    #[uniffi::constructor]
    pub fn new(text: String) -> Arc<Self> {
        Arc::new(Self {
            inner: std::sync::Mutex::new(core::doc_buffer::DocBufferState::new(&text)),
            _census: census_live::Marker::count(&census_live::BUFFERS),
        })
    }

    /// Apply one UTF-16 edit delta (AppKit `editedRange` + `changeInLength`):
    /// replace `old_len_utf16` units at `start_utf16` with `new_text`.
    pub fn apply_edit(&self, start_utf16: u32, old_len_utf16: u32, new_text: String) {
        self.inner.lock().unwrap().apply_edit(
            start_utf16 as usize,
            old_len_utf16 as usize,
            &new_text,
        );
    }

    /// Replace the whole document (reload / programmatic `string =` swap) —
    /// keeps the buffer in lockstep when the host can't express a single delta.
    pub fn reset(&self, text: String) {
        self.inner.lock().unwrap().reset(&text);
    }

    /// Document length in UTF-16 code units — the host's cheap drift guard: a
    /// mismatch with the text view's length means a delta was missed, so the
    /// host re-`reset`s and falls back to a whole-document highlight.
    pub fn len_utf16(&self) -> u32 {
        self.inner.lock().unwrap().len_utf16() as u32
    }

    /// Convert a whole-document UTF-8 byte offset to a UTF-16 offset on the
    /// live rope (O(log n)) — the host maps an `applied_range` back to UTF-16.
    pub fn byte_to_utf16(&self, byte: u32) -> u32 {
        self.inner.lock().unwrap().byte_to_utf16(byte as usize) as u32
    }

    /// Windowed highlight around a dirty range (UTF-16 in). Snapshots the rope
    /// under a short lock, then parses lock-free. Returns whole-document UTF-8
    /// byte offsets in the same [`RangedHighlight`] shape as the stateless
    /// `editor_highlight_spans_in_range`, with the same fallback contract
    /// (`applied_start == 0 && applied_end == len` ⇒ whole-document parse).
    pub fn highlight_in_range(
        &self,
        dirty_start_utf16: u32,
        dirty_end_utf16: u32,
    ) -> RangedHighlight {
        let snapshot = self.inner.lock().unwrap().clone();
        let ranged =
            snapshot.highlight_in_range(dirty_start_utf16 as usize, dirty_end_utf16 as usize);
        RangedHighlight {
            applied_start: ranged.applied_range.start as u32,
            applied_end: ranged.applied_range.end as u32,
            spans: ranged.spans.into_iter().map(Into::into).collect(),
        }
    }
}

// Editor text-buffer conversions (#378, `05` §7.1).
//
// Stateless wrappers over the canonical rope `TextBuffer`. They build a
// rope from `text`, convert, and return the UTF-16 / line integer the
// host needs — replacing the Mac app's hand-rolled O(n) `String` walks
// (`scrollToLine`, `placeCursorAtByteOffset`, `oneBasedLineForUTF16Offset`)
// with one O(log n) definition shared with the rest of the backend.
// These run at human-action cadence (jump-to-line, cursor placement),
// not per keystroke, so rebuilding the rope per call is well under a
// frame even at the large-file ceiling. A stateful `DocumentBuffer`
// holding the rope across edits is the later step (#378 PR 2; it also
// subsumes the per-keystroke conversion path).

/// UTF-16 code-unit offset into `text` → 1-based line number. Past-the-
/// end offsets clamp to the last line. Backs the Cmd+E line cue.
#[uniffi::export]
pub fn text_utf16_to_line(text: String, utf16_offset: u32) -> u32 {
    core::TextBuffer::from_str(&text).utf16_to_line(utf16_offset as usize) as u32
}

/// 1-based line number → UTF-16 code-unit offset of that line's first
/// character (the `NSRange.location` a "jump to line" scroll needs). A
/// line past the end parks at the buffer end; a line `< 1` clamps to
/// line 1. Backs `scrollToLine`.
#[uniffi::export]
pub fn text_line_to_utf16(text: String, one_based_line: u32) -> u32 {
    let buffer = core::TextBuffer::from_str(&text);
    buffer.byte_to_utf16(buffer.line_to_byte(one_based_line as usize)) as u32
}

/// UTF-8 byte offset into `text` → UTF-16 code-unit offset (the
/// `NSRange.location` for parking the caret at e.g. a template's
/// `{{cursor}}`). Past-the-end clamps to the buffer length. Backs
/// `placeCursorAtByteOffset`.
#[uniffi::export]
pub fn text_byte_to_utf16(text: String, byte_offset: u32) -> u32 {
    core::TextBuffer::from_str(&text).byte_to_utf16(byte_offset as usize) as u32
}

/// UTF-16 code-unit offset into `text` → UTF-8 byte offset (the inverse
/// of [`text_byte_to_utf16`]). The ranged highlighter (#379 PR 2) needs
/// it to turn NSTextView's UTF-16 edited range into the byte `dirty`
/// range `editor_highlight_spans_in_range` expects. Past-the-end clamps
/// to the buffer length; an offset that lands on the trailing half of a
/// surrogate pair snaps to the character boundary (see
/// `TextBuffer::utf16_to_byte`).
#[uniffi::export]
pub fn text_utf16_to_byte(text: String, utf16_offset: u32) -> u32 {
    core::TextBuffer::from_str(&text).utf16_to_byte(utf16_offset as usize) as u32
}

// Diagram pipeline mirror.

#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum DiagramDialect {
    Mermaid,
}

impl From<core::diagram::DiagramDialect> for DiagramDialect {
    fn from(d: core::diagram::DiagramDialect) -> Self {
        match d {
            core::diagram::DiagramDialect::Mermaid => DiagramDialect::Mermaid,
        }
    }
}

/// Markdown link/image spans for a source slice — the CommonMark
/// structure `editor_highlight_spans` intentionally omits (the editor
/// never colours links; see `highlight_spans`' doc). The reading-mode
/// inline mapper (U3-1, #465) uses these to keep Slate-token splicing
/// out of markdown-link syntax it doesn't own: `[t](#intro)`'s
/// destination also classifies as a tag, and splicing inside the link
/// would corrupt the destination the native markdown parse is about to
/// consume. Same authority as everywhere else — `pulldown-cmark`'s
/// event stream via `editor_spans::markdown_spans`; wikilinks are NOT
/// in this set (the highlight classifier already carries those).
#[uniffi::export]
pub fn markdown_link_spans(text: String) -> Vec<EditorSpan> {
    core::editor_spans::markdown_spans(&text)
        .into_iter()
        .filter(|s| {
            matches!(
                s.kind,
                core::editor_spans::EditorSpanKind::Link
                    | core::editor_spans::EditorSpanKind::Image
            )
        })
        .map(Into::into)
        .collect()
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Enum)]
pub enum DiagramRenderStatus {
    Ok,
    UnsupportedDialect { reason: String },
    RenderFailed { message: String },
}

impl From<core::diagram::DiagramRenderStatus> for DiagramRenderStatus {
    fn from(s: core::diagram::DiagramRenderStatus) -> Self {
        match s {
            core::diagram::DiagramRenderStatus::Ok => DiagramRenderStatus::Ok,
            core::diagram::DiagramRenderStatus::UnsupportedDialect { reason } => {
                DiagramRenderStatus::UnsupportedDialect { reason }
            }
            core::diagram::DiagramRenderStatus::RenderFailed { message } => {
                DiagramRenderStatus::RenderFailed { message }
            }
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct DiagramBlock {
    pub source: String,
    pub dialect: DiagramDialect,
    pub svg: Option<Vec<u8>>,
    pub png_fallback: Option<Vec<u8>>,
    pub structured_description: String,
    pub render_status: DiagramRenderStatus,
    pub line: u32,
    pub byte_offset: u32,
}

impl From<core::diagram::DiagramBlock> for DiagramBlock {
    fn from(b: core::diagram::DiagramBlock) -> Self {
        Self {
            source: b.source,
            dialect: b.dialect.into(),
            svg: b.svg,
            png_fallback: b.png_fallback,
            structured_description: b.structured_description,
            render_status: b.render_status.into(),
            line: b.line,
            byte_offset: b.byte_offset,
        }
    }
}

// --- Reading-view block segmentation mirror (U3-1, #465) --------------

/// FFI mirror of [`core::reading::ReadingBlockKind`]. Payload variants
/// flatten to named fields per the uniffi enum-mirror convention (same
/// shape as `EditorSpanKind` / `DiagramRenderStatus`). `task` is the
/// list-item status char as a `String` — uniffi has no `char` type, and
/// a 1-char string is what Swift wants for the checkbox glyph anyway;
/// `None` (not a task) maps to an absent optional.
#[derive(Debug, Clone, PartialEq, Eq, uniffi::Enum)]
pub enum ReadingBlockKind {
    Heading {
        level: u8,
    },
    Paragraph,
    ListItem {
        depth: u8,
        ordered: bool,
        task: Option<String>,
    },
    BlockQuote {
        depth: u8,
    },
    CodeFence {
        language: String,
        interior: String,
    },
    MathBlock,
    Diagram {
        dialect: String,
        interior: String,
    },
    Table,
    ThematicBreak,
    Html,
}

impl From<core::reading::ReadingBlockKind> for ReadingBlockKind {
    fn from(k: core::reading::ReadingBlockKind) -> Self {
        use core::reading::ReadingBlockKind as K;
        match k {
            K::Heading { level } => ReadingBlockKind::Heading { level },
            K::Paragraph => ReadingBlockKind::Paragraph,
            K::ListItem {
                depth,
                ordered,
                task,
            } => ReadingBlockKind::ListItem {
                depth,
                ordered,
                task: task.map(|c| c.to_string()),
            },
            K::BlockQuote { depth } => ReadingBlockKind::BlockQuote { depth },
            K::CodeFence { language, interior } => {
                ReadingBlockKind::CodeFence { language, interior }
            }
            K::MathBlock => ReadingBlockKind::MathBlock,
            K::Diagram { dialect, interior } => ReadingBlockKind::Diagram { dialect, interior },
            K::Table => ReadingBlockKind::Table,
            K::ThematicBreak => ReadingBlockKind::ThematicBreak,
            K::Html => ReadingBlockKind::Html,
        }
    }
}

/// One ordered reading block. `byte_start`/`byte_end` index the **whole
/// source** (frontmatter offset included); `source` is the exact slice.
#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct ReadingBlock {
    pub kind: ReadingBlockKind,
    pub byte_start: u64,
    pub byte_end: u64,
    pub source: String,
}

impl From<core::reading::ReadingBlock> for ReadingBlock {
    fn from(b: core::reading::ReadingBlock) -> Self {
        Self {
            kind: b.kind.into(),
            byte_start: b.byte_start,
            byte_end: b.byte_end,
            source: b.source,
        }
    }
}

/// Segment `source` into ordered reading blocks — pure, no IO (U3-2
/// live-buffer entry point). Reading mode renders the editor's in-memory
/// body directly, so unsaved edits are visible without a disk round-trip.
#[uniffi::export]
pub fn reading_blocks_source(source: String) -> Vec<ReadingBlock> {
    core::reading::reading_blocks_source(&source)
        .into_iter()
        .map(Into::into)
        .collect()
}

/// FFI mirror of [`core::reading::ReadingTableCells`] (#510). `rows` is a
/// list of rows, each a list of cell strings — uniffi carries `Vec<Vec<String>>`
/// directly, so no row wrapper record is needed. Every row equals `header.len()`
/// (Rust normalizes ragged rows), so the Swift grid indexes cells safely.
#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct ReadingTableCells {
    pub header: Vec<String>,
    pub rows: Vec<Vec<String>>,
}

impl From<core::reading::ReadingTableCells> for ReadingTableCells {
    fn from(c: core::reading::ReadingTableCells) -> Self {
        Self {
            header: c.header,
            rows: c.rows,
        }
    }
}

/// Segment a GFM table's `source` slice into header + body cells — pure, no
/// IO (#510). Input is exactly what [`ReadingBlock::source`] carries for a
/// `Table` block; `None` when the slice is not a table (the Swift side falls
/// back to the raw-source block). Cells are the flattened inline text.
#[uniffi::export]
pub fn reading_table_cells(source: String) -> Option<ReadingTableCells> {
    core::reading::reading_table_cells(&source).map(Into::into)
}

// =====================================================================
// Milestone L citations + bibliography (#278) — FFI mirror
// =====================================================================

#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum BibFormat {
    BibTeX,
    BibLaTeX,
    CslJson,
}

impl From<BibFormat> for core::BibFormat {
    fn from(f: BibFormat) -> Self {
        match f {
            BibFormat::BibTeX => core::BibFormat::BibTeX,
            BibFormat::BibLaTeX => core::BibFormat::BibLaTeX,
            BibFormat::CslJson => core::BibFormat::CslJson,
        }
    }
}

impl From<core::BibFormat> for BibFormat {
    fn from(f: core::BibFormat) -> Self {
        match f {
            core::BibFormat::BibTeX => BibFormat::BibTeX,
            core::BibFormat::BibLaTeX => BibFormat::BibLaTeX,
            core::BibFormat::CslJson => BibFormat::CslJson,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct BibliographySource {
    pub path: String,
    pub format: BibFormat,
    pub watch: bool,
}

impl From<BibliographySource> for core::BibliographySource {
    fn from(s: BibliographySource) -> Self {
        Self {
            path: s.path,
            format: s.format.into(),
            watch: s.watch,
        }
    }
}

impl From<core::BibliographySource> for BibliographySource {
    fn from(s: core::BibliographySource) -> Self {
        Self {
            path: s.path,
            format: s.format.into(),
            watch: s.watch,
        }
    }
}

/// Effective citation preferences for the open vault, merged across
/// both config surfaces (#411): `.slate/prefs.json` where it speaks,
/// the vault-root `slate.json` otherwise. Exposed so the app can
/// seed its bibliography state from the vault-shipped config at
/// open time without re-implementing the precedence rules.
#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct CitationsPrefs {
    pub sources: Vec<BibliographySource>,
    pub default_style: Option<String>,
    pub additional_styles: Vec<String>,
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct Author {
    pub family: String,
    pub given: Option<String>,
}

impl From<core::Author> for Author {
    fn from(a: core::Author) -> Self {
        Self {
            family: a.family,
            given: a.given,
        }
    }
}

impl From<Author> for core::Author {
    fn from(a: Author) -> Self {
        Self {
            family: a.family,
            given: a.given,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct BibEntry {
    pub key: String,
    pub item_type: String,
    pub title: String,
    pub authors: Vec<Author>,
    pub year: Option<i32>,
    pub journal: Option<String>,
    pub doi: Option<String>,
    pub url: Option<String>,
    pub publisher: Option<String>,
    pub abstract_text: Option<String>,
    pub raw_csl_json: String,
}

impl From<core::BibEntry> for BibEntry {
    fn from(e: core::BibEntry) -> Self {
        Self {
            key: e.key,
            item_type: e.item_type,
            title: e.title,
            authors: e.authors.into_iter().map(Into::into).collect(),
            year: e.year,
            journal: e.journal,
            doi: e.doi,
            url: e.url,
            publisher: e.publisher,
            abstract_text: e.abstract_text,
            raw_csl_json: e.raw_csl_json,
        }
    }
}

impl From<BibEntry> for core::BibEntry {
    fn from(e: BibEntry) -> Self {
        Self {
            key: e.key,
            item_type: e.item_type,
            title: e.title,
            authors: e.authors.into_iter().map(Into::into).collect(),
            year: e.year,
            journal: e.journal,
            doi: e.doi,
            url: e.url,
            publisher: e.publisher,
            abstract_text: e.abstract_text,
            raw_csl_json: e.raw_csl_json,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct BibLoadWarning {
    pub source_path: String,
    pub message: String,
}

impl From<core::BibLoadWarning> for BibLoadWarning {
    fn from(w: core::BibLoadWarning) -> Self {
        Self {
            source_path: w.source_path,
            message: w.message,
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum CitationMode {
    Bracketed,
    InText,
    SuppressAuthor,
}

impl From<core::CitationMode> for CitationMode {
    fn from(m: core::CitationMode) -> Self {
        match m {
            core::CitationMode::Bracketed => CitationMode::Bracketed,
            core::CitationMode::InText => CitationMode::InText,
            core::CitationMode::SuppressAuthor => CitationMode::SuppressAuthor,
        }
    }
}

impl From<CitationMode> for core::CitationMode {
    fn from(m: CitationMode) -> Self {
        match m {
            CitationMode::Bracketed => core::CitationMode::Bracketed,
            CitationMode::InText => core::CitationMode::InText,
            CitationMode::SuppressAuthor => core::CitationMode::SuppressAuthor,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct Locator {
    pub label: String,
    /// The locator value (core's `locator` field). Named `value` here because
    /// generators that PascalCase record fields (C#) may not emit a member
    /// named identically to its enclosing type (`Locator.Locator`, CS0542).
    pub value: String,
}

impl From<core::Locator> for Locator {
    fn from(l: core::Locator) -> Self {
        Self {
            label: l.label,
            value: l.locator,
        }
    }
}

impl From<Locator> for core::Locator {
    fn from(l: Locator) -> Self {
        Self {
            label: l.label,
            locator: l.value,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct CitedItem {
    pub key: String,
    pub locator: Option<Locator>,
    pub prefix: Option<String>,
    pub suffix: Option<String>,
    pub mode: CitationMode,
}

impl From<core::CitedItem> for CitedItem {
    fn from(i: core::CitedItem) -> Self {
        Self {
            key: i.key,
            locator: i.locator.map(Into::into),
            prefix: i.prefix,
            suffix: i.suffix,
            mode: i.mode.into(),
        }
    }
}

impl From<CitedItem> for core::CitedItem {
    fn from(i: CitedItem) -> Self {
        Self {
            key: i.key,
            locator: i.locator.map(Into::into),
            prefix: i.prefix,
            suffix: i.suffix,
            mode: i.mode.into(),
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct CitationReference {
    pub raw: String,
    pub citations: Vec<CitedItem>,
    pub byte_offset: u32,
    pub line: u32,
}

impl From<core::CitationReference> for CitationReference {
    fn from(r: core::CitationReference) -> Self {
        Self {
            raw: r.raw,
            citations: r.citations.into_iter().map(Into::into).collect(),
            byte_offset: r.byte_offset,
            line: r.line,
        }
    }
}

impl From<CitationReference> for core::CitationReference {
    fn from(r: CitationReference) -> Self {
        Self {
            raw: r.raw,
            citations: r.citations.into_iter().map(Into::into).collect(),
            byte_offset: r.byte_offset,
            line: r.line,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct RenderedCitation {
    pub raw: String,
    pub visual_text: String,
    pub speech_text: String,
    pub bib_entry: Option<BibEntry>,
    pub style_id: String,
}

impl From<core::RenderedCitation> for RenderedCitation {
    fn from(r: core::RenderedCitation) -> Self {
        Self {
            raw: r.raw,
            visual_text: r.visual_text,
            speech_text: r.speech_text,
            bib_entry: r.bib_entry.map(Into::into),
            style_id: r.style_id,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct CslStyleInfo {
    pub id: String,
    pub path: String,
    pub title: String,
}

impl From<core::CslStyleInfo> for CslStyleInfo {
    fn from(s: core::CslStyleInfo) -> Self {
        Self {
            id: s.id,
            path: s.path,
            title: s.title,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct UnresolvedCitation {
    pub path: String,
    pub key: String,
}

// =====================================================================
// Command palette registry FFI surface (Milestone Q, issue #312)
// =====================================================================

/// Maximum byte length for `ActionFailed.message` returned by a
/// foreign action. Foreign callers — Swift menu items in #314, and
/// V1.x plugin commands — supply this message; without a cap a
/// hostile or buggy implementation could return megabytes that flow
/// into `os_log`, the SwiftUI `Text` views that render error
/// alerts, and VoiceOver. 1 KiB is generous for a real error
/// message and orders of magnitude smaller than any plausible abuse
/// payload. Truncation lands at a UTF-8 boundary with a "(truncated)"
/// suffix so the result is still a valid Rust `String`.
const MAX_ACTION_ERROR_MSG_LEN: usize = 1024;

/// Top-level grouping for commands shown in the palette. Mirrors
/// `slate_core::CommandSection` 1:1; declared in palette render
/// order. New section requires a deliberate edit on both sides.
#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum CommandSection {
    File,
    Navigation,
    View,
    Vault,
    Editor,
    Tasks,
    Settings,
    Plugins,
    /// Canvas commands (Milestone T, #369).
    Canvas,
    /// Bases commands (Milestone N, #702).
    Bases,
    /// Graph commands (Milestone P, P1-3 #556).
    Graph,
    /// File-sidebar actions (Milestone FL, FL-04).
    Sidebar,
}

impl From<core::CommandSection> for CommandSection {
    fn from(s: core::CommandSection) -> Self {
        match s {
            core::CommandSection::File => Self::File,
            core::CommandSection::Navigation => Self::Navigation,
            core::CommandSection::View => Self::View,
            core::CommandSection::Vault => Self::Vault,
            core::CommandSection::Editor => Self::Editor,
            core::CommandSection::Tasks => Self::Tasks,
            core::CommandSection::Settings => Self::Settings,
            core::CommandSection::Plugins => Self::Plugins,
            core::CommandSection::Canvas => Self::Canvas,
            core::CommandSection::Bases => Self::Bases,
            core::CommandSection::Graph => Self::Graph,
            core::CommandSection::Sidebar => Self::Sidebar,
        }
    }
}

impl From<CommandSection> for core::CommandSection {
    fn from(s: CommandSection) -> Self {
        match s {
            CommandSection::File => Self::File,
            CommandSection::Navigation => Self::Navigation,
            CommandSection::View => Self::View,
            CommandSection::Vault => Self::Vault,
            CommandSection::Editor => Self::Editor,
            CommandSection::Tasks => Self::Tasks,
            CommandSection::Settings => Self::Settings,
            CommandSection::Plugins => Self::Plugins,
            CommandSection::Canvas => Self::Canvas,
            CommandSection::Bases => Self::Bases,
            CommandSection::Graph => Self::Graph,
            CommandSection::Sidebar => Self::Sidebar,
        }
    }
}

/// Metadata for a registered command. Mirrors `slate_core::Command`.
/// The action implementation lives behind a callback interface
/// ([`CommandAction`]) so the foreign side (Swift / Kotlin / etc.)
/// can supply the actual handler at register time.
#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct Command {
    pub id: String,
    pub label: String,
    pub accessibility_hint: Option<String>,
    pub hotkey_hint: Option<String>,
    pub section: CommandSection,
}

impl From<core::Command> for Command {
    fn from(c: core::Command) -> Self {
        Self {
            id: c.id,
            label: c.label,
            accessibility_hint: c.accessibility_hint,
            hotkey_hint: c.hotkey_hint,
            section: c.section.into(),
        }
    }
}

impl From<Command> for core::Command {
    fn from(c: Command) -> Self {
        Self {
            id: c.id,
            label: c.label,
            accessibility_hint: c.accessibility_hint,
            hotkey_hint: c.hotkey_hint,
            section: c.section.into(),
        }
    }
}

/// FFI-exposed command registry errors. Mirrors
/// `slate_core::CommandError`; struct-variant shape matches the rest
/// of this crate's error surface so generated Swift enums stay
/// readable.
#[derive(Debug, thiserror::Error, uniffi::Error, PartialEq, Eq)]
pub enum CommandError {
    #[error("unknown command id: {id}")]
    UnknownId { id: String },
    /// Action returned an error. The `message` is **foreign-
    /// controlled** — supplied by a Swift menu handler or a V1.x
    /// plugin command — and is truncated by `ForeignActionAdapter::
    /// invoke` to `MAX_ACTION_ERROR_MSG_LEN` bytes. Renderers must
    /// treat it as plain text (not Markdown / `AttributedString`)
    /// to avoid injection from a hostile plugin.
    #[error("command action failed: {message}")]
    ActionFailed { message: String },
}

impl From<core::CommandError> for CommandError {
    fn from(e: core::CommandError) -> Self {
        match e {
            core::CommandError::UnknownId(id) => Self::UnknownId { id },
            core::CommandError::ActionFailed(message) => Self::ActionFailed { message },
        }
    }
}

/// Foreign-implemented action for a registered command.
///
/// First **fallible** callback interface in `slate-uniffi` — the
/// non-fallible `ScanProgressListener` (line ~1262 above) is the
/// existing precedent; this trait adds `Result`-typed error
/// propagation through [`CommandError`].
///
/// ## Untrusted boundary
///
/// Foreign callers — Swift menu wiring in #314, Kotlin equivalent
/// later, and V1.x plugin commands — supply both the action and the
/// `CommandError::ActionFailed { message }` returned by it. The
/// message is **untrusted**: [`ForeignActionAdapter::invoke`]
/// truncates it to `MAX_ACTION_ERROR_MSG_LEN` bytes so a hostile or
/// buggy implementation can't flood logs or `Text` views. Renderers
/// must treat the message as plain text (not Markdown /
/// `AttributedString`).
///
/// ## Sendable contract
///
/// The Rust trait is `Send + Sync`. Foreign implementations MUST
/// satisfy the same contract: on Swift, mark the implementing type
/// `Sendable` (or `@unchecked Sendable` with a lock guarding any
/// mutable state — see `ScanProgressAdapter` for the project
/// precedent). The compiler-side check on the Swift side is faith-
/// based; getting it wrong shows up as data races inside the
/// callback, not as a build error.
#[uniffi::export(with_foreign)]
pub trait CommandAction: Send + Sync {
    fn invoke(&self) -> Result<(), CommandError>;
}

/// Bridges a foreign `Arc<dyn CommandAction>` (uniffi) into a
/// `slate_core::CommandAction` so the pure-Rust registry can hold
/// foreign actions uniformly with native ones.
///
/// Truncates `ActionFailed::message` at the trust boundary; see
/// [`MAX_ACTION_ERROR_MSG_LEN`] for the rationale.
struct ForeignActionAdapter {
    foreign: Arc<dyn CommandAction>,
}

impl core::CommandAction for ForeignActionAdapter {
    fn invoke(&self) -> Result<(), core::CommandError> {
        self.foreign.invoke().map_err(|e| match e {
            CommandError::UnknownId { id } => core::CommandError::UnknownId(id),
            CommandError::ActionFailed { message } => {
                core::CommandError::ActionFailed(truncate_action_message(message))
            }
        })
    }
}

/// Truncate a foreign-supplied `ActionFailed.message` at a UTF-8
/// boundary so the result is a valid Rust `String`. Appends a
/// human-readable "(truncated)" marker when truncation occurs so
/// downstream renderers / log readers can tell the difference
/// between a deliberately terse message and a clipped one.
fn truncate_action_message(mut message: String) -> String {
    if message.len() <= MAX_ACTION_ERROR_MSG_LEN {
        return message;
    }
    let mut end = MAX_ACTION_ERROR_MSG_LEN;
    while end > 0 && !message.is_char_boundary(end) {
        end -= 1;
    }
    message.truncate(end);
    message.push_str("… (truncated)");
    message
}

/// FFI-exposed command registry. Wraps `slate_core::CommandRegistry`.
///
/// Construct with `CommandRegistry()` on the foreign side. The
/// registry is reference-counted and `Send + Sync`; the host can
/// hold a single shared instance for the app's lifetime.
#[derive(uniffi::Object)]
pub struct CommandRegistry {
    inner: core::CommandRegistry,
    _census: census_live::Marker,
}

#[uniffi::export]
impl CommandRegistry {
    #[uniffi::constructor]
    pub fn new() -> Arc<Self> {
        Arc::new(Self {
            inner: core::CommandRegistry::new(),
            _census: census_live::Marker::count(&census_live::REGISTRIES),
        })
    }

    /// Register a command with the foreign-implemented action.
    /// Returns `true` when the call replaced an existing entry
    /// with the same id, `false` for a fresh registration.
    ///
    /// Replace-semantics are deliberate (plugin hot-reload), but
    /// silent override of a core `slate.*` id by a plugin would be
    /// a privilege-escalation footgun — the menu bridge (#314) and
    /// any future plugin loader MUST check the return value and
    /// reject conflicts at the registration site.
    #[must_use = "register replaces existing entries silently; check the return value if uniqueness matters"]
    pub fn register(&self, command: Command, action: Arc<dyn CommandAction>) -> bool {
        self.inner.register(
            command.into(),
            Arc::new(ForeignActionAdapter { foreign: action }),
        )
    }

    /// Remove a registered command. Returns `true` when an entry existed.
    pub fn unregister(&self, id: String) -> bool {
        self.inner.unregister(&id)
    }

    /// Return every registered command's metadata, sorted by
    /// `(section, id)` for deterministic palette rendering.
    pub fn list(&self) -> Vec<Command> {
        self.inner.list().into_iter().map(Into::into).collect()
    }

    /// Return the metadata for a single command, or `nil` if no
    /// command is registered under `id`.
    pub fn find_by_id(&self, id: String) -> Option<Command> {
        self.inner.find_by_id(&id).map(Into::into)
    }

    /// Invoke the action for `id`. Returns `UnknownId` if no
    /// command is registered, or `ActionFailed` if the action's
    /// `invoke` returned an error.
    pub fn invoke_by_id(&self, id: String) -> Result<(), CommandError> {
        self.inner.invoke_by_id(&id).map_err(Into::into)
    }
}

// ---------------------------------------------------------------------------
// Command-palette ranking + recents policy (W0.5-1, #717): thin mirrors of
// `slate_core::palette`. Pure functions over an explicit command snapshot —
// the palette ranks the list it OPENED with (and host tests inject synthetic
// snapshots), so these deliberately don't read the live registry.

/// One matched byte range inside a command label, for host-side bolding.
/// Half-open `[start_byte, end_byte)` over the label's UTF-8 bytes.
#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Record)]
pub struct MatchSpan {
    pub start_byte: u32,
    pub end_byte: u32,
}

impl From<core::palette::MatchSpan> for MatchSpan {
    fn from(s: core::palette::MatchSpan) -> Self {
        Self {
            start_byte: s.start_byte,
            end_byte: s.end_byte,
        }
    }
}

/// One ranked palette row: the command, the label ranges the query
/// matched (empty on an empty query or a hint-only match), and the
/// winning fuzzy score (0 on an empty query) — hosts derive the global
/// "strongest match overall" order from it without re-scoring.
#[derive(Debug, Clone, PartialEq, uniffi::Record)]
pub struct PaletteRow {
    pub command: Command,
    pub label_match_spans: Vec<MatchSpan>,
    pub score: i32,
}

impl From<core::palette::PaletteRow> for PaletteRow {
    fn from(r: core::palette::PaletteRow) -> Self {
        Self {
            command: r.command.into(),
            label_match_spans: r.label_match_spans.into_iter().map(Into::into).collect(),
            score: r.score,
        }
    }
}

/// One renderable palette section. `kind == None` is the synthetic Recent
/// section; the `title` strings are canonical copy both hosts render
/// verbatim.
#[derive(Debug, Clone, PartialEq, uniffi::Record)]
pub struct PaletteSection {
    pub title: String,
    pub kind: Option<CommandSection>,
    pub rows: Vec<PaletteRow>,
}

impl From<core::palette::PaletteSection> for PaletteSection {
    fn from(s: core::palette::PaletteSection) -> Self {
        Self {
            title: s.title,
            kind: s.kind.map(Into::into),
            rows: s.rows.into_iter().map(Into::into).collect(),
        }
    }
}

/// Rank and group a command snapshot for palette rendering — ranking,
/// section layout, Recent blending, and within-Sidebar catalog placement
/// all live core-side (`slate_core::palette::palette_sections`).
/// `sidebar_pinned_order` is the host's sidebar-catalog id order (data,
/// not policy); pass an empty list to keep registry order.
#[uniffi::export]
pub fn palette_sections(
    commands: Vec<Command>,
    query: String,
    recent_ids: Vec<String>,
    sidebar_pinned_order: Vec<String>,
) -> Vec<PaletteSection> {
    let commands: Vec<core::Command> = commands.into_iter().map(Into::into).collect();
    core::palette::palette_sections(&commands, &query, &recent_ids, &sidebar_pinned_order)
        .into_iter()
        .map(Into::into)
        .collect()
}

/// Decode a recents file's bytes into the normalized id list (malformed,
/// oversized, or non-array input → empty; first-seen dedupe; capped).
/// Hosts own the file path + atomic I/O; core owns the format.
#[uniffi::export]
pub fn palette_recents_decode(bytes: Vec<u8>) -> Vec<String> {
    core::palette::recents_decode(&bytes)
}

/// Encode a recents id list in the on-disk format (v1: pretty JSON array,
/// byte-compatible with what the mac store has always written).
#[uniffi::export]
pub fn palette_recents_encode(ids: Vec<String>) -> Vec<u8> {
    core::palette::recents_encode(&ids)
}

/// LRU add: moves `id` to the front, dedupes, caps.
#[uniffi::export]
pub fn palette_recents_add(ids: Vec<String>, id: String) -> Vec<String> {
    core::palette::recents_add(&ids, &id)
}

/// Remove every occurrence of `id` (no-op when absent).
#[uniffi::export]
pub fn palette_recents_remove(ids: Vec<String>, id: String) -> Vec<String> {
    core::palette::recents_remove(&ids, &id)
}

// ---------------------------------------------------------------------------
// Quick-switcher ranking + recency blending (W0.5-2, #718): thin mirrors of
// `slate_core::switcher`. Same shape as the palette calls — pure functions
// over an explicit file snapshot; the shared base matcher is
// `slate_core::palette::fuzzy_score` (the one-ranking-engine decision).

/// One rankable file, as the host's file list provides it. `path` is
/// vault-relative; `name` is the display name WITH extension, as
/// `FileSummary` carries it.
#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct SwitcherFile {
    pub path: String,
    pub name: String,
}

impl From<SwitcherFile> for core::switcher::SwitcherFile {
    fn from(f: SwitcherFile) -> Self {
        Self {
            path: f.path,
            name: f.name,
        }
    }
}

/// One ranked switcher row: the file, its canonical extension-stripped
/// display label, the winning blended score (0 on an empty query), and
/// the matched byte ranges inside `display_name` for host bolding
/// (empty when only the full name or the path matched).
#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct SwitcherRow {
    pub path: String,
    pub name: String,
    pub display_name: String,
    pub score: i32,
    pub display_name_match_spans: Vec<MatchSpan>,
}

impl From<core::switcher::SwitcherRow> for SwitcherRow {
    fn from(r: core::switcher::SwitcherRow) -> Self {
        Self {
            path: r.path,
            name: r.name,
            display_name: r.display_name,
            score: r.score,
            display_name_match_spans: r
                .display_name_match_spans
                .into_iter()
                .map(Into::into)
                .collect(),
        }
    }
}

/// Rank a file snapshot for the quick switcher — the name-over-path
/// score bias and the recency-blended orderings all live core-side
/// (`slate_core::switcher::switcher_rank`). Empty query returns the
/// still-present recents first (pruned, recency order) then the rest in
/// incoming order; non-empty returns matches sorted by descending score
/// with recency-then-path tiebreaks. The display cap on rendered rows
/// stays a host/view concern — the full list's length is what
/// result-count announcements report.
#[uniffi::export]
pub fn switcher_rank(
    files: Vec<SwitcherFile>,
    query: String,
    recent_paths: Vec<String>,
) -> Vec<SwitcherRow> {
    let files: Vec<core::switcher::SwitcherFile> = files.into_iter().map(Into::into).collect();
    core::switcher::switcher_rank(&files, &query, &recent_paths)
        .into_iter()
        .map(Into::into)
        .collect()
}

/// Canonical extension-stripped display label for a file name (the
/// switcher row label; only a trailing `.md`/`.markdown` is removed,
/// case-insensitively).
#[uniffi::export]
pub fn switcher_display_name(name: String) -> String {
    core::switcher::display_name(&name)
}

// ---------------------------------------------------------------------------
// Canonical a11y-event vocabulary (W0.5-3, #719): thin mirror of
// `slate_core::a11y`. Hosts CONSTRUCT events and call `a11y_render`; the
// text + priority come back canonical, so both hosts speak identically
// (§W-D). The mirror is intentionally one-directional — events never flow
// core → host.

/// How urgently a host should speak an announcement. `High` interrupts
/// current speech (assertive); `Medium` queues politely.
#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum A11yPriority {
    Medium,
    High,
}

impl From<A11yPriority> for core::a11y::A11yPriority {
    fn from(p: A11yPriority) -> Self {
        match p {
            A11yPriority::Medium => core::a11y::A11yPriority::Medium,
            A11yPriority::High => core::a11y::A11yPriority::High,
        }
    }
}

impl From<core::a11y::A11yPriority> for A11yPriority {
    fn from(p: core::a11y::A11yPriority) -> Self {
        match p {
            core::a11y::A11yPriority::Medium => A11yPriority::Medium,
            core::a11y::A11yPriority::High => A11yPriority::High,
        }
    }
}

/// One announcement, as data — 1:1 mirror of `slate_core::a11y::A11yEvent`
/// (see that module for per-variant docs, the copy rules, and the
/// `HostComposed` residue contract).
#[derive(Debug, Clone, PartialEq, Eq, uniffi::Enum)]
pub enum A11yEvent {
    FilesRegionFocused,
    LeafPanelShown {
        title: String,
    },
    EditorPaneFocused {
        ordinal: u32,
        total: u32,
        title: String,
        prefix: String,
    },
    TabFocused {
        prefix: String,
        filename: String,
        index: u32,
        count: u32,
    },
    TabClosed {
        closed_title: String,
        successor: Option<String>,
    },
    NoSplitPanesToResize,
    PaneResized {
        percent: u32,
    },
    GraphOpensSinglePane,
    RightPaneShown,
    RightPaneHidden,
    HistoryPanelShown,
    ReopenTargetMissing {
        filename: String,
    },
    ReopenedFile {
        filename: String,
    },
    ReopenedNamed {
        name: String,
    },
    ReopenedGraph,
    VaultOpened {
        vault_title: String,
        sidebar_notice: String,
    },
    RemovedRecentVault {
        display_name: String,
    },
    WelcomeShown {
        recent_vault_count: u32,
    },
    CommandPaletteNeedsVault,
    SearchNeedsVault,
    SearchResultOpened {
        filename: String,
        line: u32,
        snippet: String,
    },
    ExternalLinkUnsupported {
        target: String,
    },
    ExternalLinkOpened,
    ExternalLinkFailed {
        target: String,
    },
    LinkUnresolved {
        target: String,
    },
    HelpOpened,
    HelpFailed,
    InternalNavigated {
        kind: String,
        filename: String,
    },
    CitationNotLoaded,
    NoResolvedEmbedAtCursor,
    NoEmbedAtCursor,
    HeadingNotFound,
    HeadingScrollFailed {
        heading: String,
    },
    ScrolledToHeading {
        heading: String,
    },
    ScrolledToLine {
        filename: String,
        line: u32,
    },
    OpenedAtLine {
        filename: String,
        line: u32,
    },
    OpenedFile {
        filename: String,
    },
    ShowingNote {
        display_name: String,
    },
    TaskToggleUnsaved {
        filename: String,
    },
    TaskToggleConflict {
        filename: String,
    },
    TasksReviewShown {
        filter_name: String,
    },
    TasksFilterSet {
        filter_name: String,
    },
    NoteSaved {
        filename: String,
    },
    SaveConflict {
        filename: String,
    },
    RestoredVersionFrom {
        formatted_date: String,
    },
    RestoredFile {
        filename: String,
    },
    RestoredFileAs {
        source_name: String,
        filename: String,
    },
    PrintNeedsNote,
    PrintDialogOpened {
        name: String,
    },
    BatchCheckStarted {
        formatted_count: String,
        action_name: String,
    },
    SelectionCopied,
    SidebarSettingsStillDefaults {
        detail: String,
    },
    SidebarSettingsReloadedStaleRefs,
    SidebarSettingsReloaded,
    VaultClosed,
    VaultClosedAllSaved,
    VaultClosedChangesDiscarded,
    PropertiesUpdated,
    PropertyChanged {
        key: String,
        deleted: bool,
    },
    PropertyEditConflict {
        filename: String,
    },
    PropertiesSourceRejected {
        reason: String,
    },
    PropertyEditFailed {
        detail: String,
    },
    PropertiesReloaded,
    PropertiesReloadedBodyChanged,
    NoteChangedAgain {
        detail: Option<String>,
    },
    PropertiesReloadFailed {
        reason: String,
    },
    PropertyRetainedCopied,
    PropertyRecoveryUnverified {
        display_name: String,
    },
    PropertyRetainedDiscarded,
    PropertyRetainedReapplyFailed {
        detail: Option<String>,
    },
    PropertyReloadStillFailed {
        reason: String,
    },
    PropertyLoadCurrentFailed {
        reason: String,
    },
    AddPropertySheetShown,
    SourceChangesDiscarded,
    BulkRenameSheetShown,
    RenameReloadFailed {
        detail: Option<String>,
    },
    RenameFailed {
        detail: String,
    },
    RenameSummary {
        applied: bool,
        renamed: u32,
        skipped: u32,
        failed: u32,
    },
    DuplicateFilesOnly,
    MathSpeechStyle {
        name: String,
    },
    MathVerbosity {
        name: String,
    },
    MathBrailleCode {
        name: String,
    },
    CodePreambleVerbosity {
        name: String,
    },
    EditorTextSize {
        percent: u32,
    },
    SpellCheckToggled {
        enabled: bool,
    },
    CitationStyleChanged {
        title: String,
    },
    CitationsCount {
        count: u32,
    },
    OutlineCount {
        count: u32,
    },
    FileListCount {
        count: u32,
    },
    ItemsSelected {
        count: u32,
    },
    NoItemsSelected,
    TreeFolderSelected {
        name: String,
    },
    RowSelected {
        name: String,
    },
    SwitcherRecentCount {
        count: u32,
    },
    SwitcherNoMatches {
        query: String,
    },
    SwitcherMatchCount {
        count: u32,
        query: String,
    },
    PaletteCommandSelected {
        label: String,
        disabled_reason: Option<String>,
    },
    RecentSearchFocused {
        query: String,
    },
    QuickSwitcherCount {
        count: u32,
        query: Option<String>,
    },
    BaseViewMode {
        mode: String,
    },
    BaseViewSwitcher {
        view_count: u32,
    },
    BasesNewQueryBuilder,
    BasesEditingFilters {
        view_name: String,
    },
    BasesFiltersOpenFailed {
        detail: String,
    },
    BasesPreviewFailed {
        detail: String,
    },
    BasesBuilderSaved,
    BasesViewSaveFailed {
        detail: String,
    },
    BasesSavedQueryNameNeeded,
    BasesSavedQueryCreated {
        name: String,
    },
    BasesSavedQueryCreateFailed {
        detail: String,
    },
    BasesSavedQueryUpdated {
        name: String,
    },
    BasesSavedQueryUpdateFailed {
        detail: String,
    },
    BasesViewSelected {
        name: String,
    },
    BasesSortSaveFailed {
        detail: String,
    },
    BaseRefreshed,
    DataviewConversionFailed {
        detail: String,
    },
    CitationInsertUnavailable,
    CitationWalkThrough,
    CodeCopied,
    HostComposed {
        text: String,
        priority: A11yPriority,
    },
}

impl From<A11yEvent> for core::a11y::A11yEvent {
    fn from(e: A11yEvent) -> Self {
        use A11yEvent as F;
        use core::a11y::A11yEvent as C;
        match e {
            F::FilesRegionFocused => C::FilesRegionFocused,
            F::LeafPanelShown { title } => C::LeafPanelShown { title },
            F::EditorPaneFocused {
                ordinal,
                total,
                title,
                prefix,
            } => C::EditorPaneFocused {
                ordinal,
                total,
                title,
                prefix,
            },
            F::TabFocused {
                prefix,
                filename,
                index,
                count,
            } => C::TabFocused {
                prefix,
                filename,
                index,
                count,
            },
            F::TabClosed {
                closed_title,
                successor,
            } => C::TabClosed {
                closed_title,
                successor,
            },
            F::NoSplitPanesToResize => C::NoSplitPanesToResize,
            F::PaneResized { percent } => C::PaneResized { percent },
            F::GraphOpensSinglePane => C::GraphOpensSinglePane,
            F::RightPaneShown => C::RightPaneShown,
            F::RightPaneHidden => C::RightPaneHidden,
            F::HistoryPanelShown => C::HistoryPanelShown,
            F::ReopenTargetMissing { filename } => C::ReopenTargetMissing { filename },
            F::ReopenedFile { filename } => C::ReopenedFile { filename },
            F::ReopenedNamed { name } => C::ReopenedNamed { name },
            F::ReopenedGraph => C::ReopenedGraph,
            F::VaultOpened {
                vault_title,
                sidebar_notice,
            } => C::VaultOpened {
                vault_title,
                sidebar_notice,
            },
            F::RemovedRecentVault { display_name } => C::RemovedRecentVault { display_name },
            F::WelcomeShown { recent_vault_count } => C::WelcomeShown { recent_vault_count },
            F::CommandPaletteNeedsVault => C::CommandPaletteNeedsVault,
            F::SearchNeedsVault => C::SearchNeedsVault,
            F::SearchResultOpened {
                filename,
                line,
                snippet,
            } => C::SearchResultOpened {
                filename,
                line,
                snippet,
            },
            F::ExternalLinkUnsupported { target } => C::ExternalLinkUnsupported { target },
            F::ExternalLinkOpened => C::ExternalLinkOpened,
            F::ExternalLinkFailed { target } => C::ExternalLinkFailed { target },
            F::LinkUnresolved { target } => C::LinkUnresolved { target },
            F::HelpOpened => C::HelpOpened,
            F::HelpFailed => C::HelpFailed,
            F::InternalNavigated { kind, filename } => C::InternalNavigated { kind, filename },
            F::CitationNotLoaded => C::CitationNotLoaded,
            F::NoResolvedEmbedAtCursor => C::NoResolvedEmbedAtCursor,
            F::NoEmbedAtCursor => C::NoEmbedAtCursor,
            F::HeadingNotFound => C::HeadingNotFound,
            F::HeadingScrollFailed { heading } => C::HeadingScrollFailed { heading },
            F::ScrolledToHeading { heading } => C::ScrolledToHeading { heading },
            F::ScrolledToLine { filename, line } => C::ScrolledToLine { filename, line },
            F::OpenedAtLine { filename, line } => C::OpenedAtLine { filename, line },
            F::OpenedFile { filename } => C::OpenedFile { filename },
            F::ShowingNote { display_name } => C::ShowingNote { display_name },
            F::TaskToggleUnsaved { filename } => C::TaskToggleUnsaved { filename },
            F::TaskToggleConflict { filename } => C::TaskToggleConflict { filename },
            F::TasksReviewShown { filter_name } => C::TasksReviewShown { filter_name },
            F::TasksFilterSet { filter_name } => C::TasksFilterSet { filter_name },
            F::NoteSaved { filename } => C::NoteSaved { filename },
            F::SaveConflict { filename } => C::SaveConflict { filename },
            F::RestoredVersionFrom { formatted_date } => C::RestoredVersionFrom { formatted_date },
            F::RestoredFile { filename } => C::RestoredFile { filename },
            F::RestoredFileAs {
                source_name,
                filename,
            } => C::RestoredFileAs {
                source_name,
                filename,
            },
            F::PrintNeedsNote => C::PrintNeedsNote,
            F::PrintDialogOpened { name } => C::PrintDialogOpened { name },
            F::BatchCheckStarted {
                formatted_count,
                action_name,
            } => C::BatchCheckStarted {
                formatted_count,
                action_name,
            },
            F::SelectionCopied => C::SelectionCopied,
            F::SidebarSettingsStillDefaults { detail } => {
                C::SidebarSettingsStillDefaults { detail }
            }
            F::SidebarSettingsReloadedStaleRefs => C::SidebarSettingsReloadedStaleRefs,
            F::SidebarSettingsReloaded => C::SidebarSettingsReloaded,
            F::VaultClosed => C::VaultClosed,
            F::VaultClosedAllSaved => C::VaultClosedAllSaved,
            F::VaultClosedChangesDiscarded => C::VaultClosedChangesDiscarded,
            F::PropertiesUpdated => C::PropertiesUpdated,
            F::PropertyChanged { key, deleted } => C::PropertyChanged { key, deleted },
            F::PropertyEditConflict { filename } => C::PropertyEditConflict { filename },
            F::PropertiesSourceRejected { reason } => C::PropertiesSourceRejected { reason },
            F::PropertyEditFailed { detail } => C::PropertyEditFailed { detail },
            F::PropertiesReloaded => C::PropertiesReloaded,
            F::PropertiesReloadedBodyChanged => C::PropertiesReloadedBodyChanged,
            F::NoteChangedAgain { detail } => C::NoteChangedAgain { detail },
            F::PropertiesReloadFailed { reason } => C::PropertiesReloadFailed { reason },
            F::PropertyRetainedCopied => C::PropertyRetainedCopied,
            F::PropertyRecoveryUnverified { display_name } => {
                C::PropertyRecoveryUnverified { display_name }
            }
            F::PropertyRetainedDiscarded => C::PropertyRetainedDiscarded,
            F::PropertyRetainedReapplyFailed { detail } => {
                C::PropertyRetainedReapplyFailed { detail }
            }
            F::PropertyReloadStillFailed { reason } => C::PropertyReloadStillFailed { reason },
            F::PropertyLoadCurrentFailed { reason } => C::PropertyLoadCurrentFailed { reason },
            F::AddPropertySheetShown => C::AddPropertySheetShown,
            F::SourceChangesDiscarded => C::SourceChangesDiscarded,
            F::BulkRenameSheetShown => C::BulkRenameSheetShown,
            F::RenameReloadFailed { detail } => C::RenameReloadFailed { detail },
            F::RenameFailed { detail } => C::RenameFailed { detail },
            F::RenameSummary {
                applied,
                renamed,
                skipped,
                failed,
            } => C::RenameSummary {
                applied,
                renamed,
                skipped,
                failed,
            },
            F::DuplicateFilesOnly => C::DuplicateFilesOnly,
            F::MathSpeechStyle { name } => C::MathSpeechStyle { name },
            F::MathVerbosity { name } => C::MathVerbosity { name },
            F::MathBrailleCode { name } => C::MathBrailleCode { name },
            F::CodePreambleVerbosity { name } => C::CodePreambleVerbosity { name },
            F::EditorTextSize { percent } => C::EditorTextSize { percent },
            F::SpellCheckToggled { enabled } => C::SpellCheckToggled { enabled },
            F::CitationStyleChanged { title } => C::CitationStyleChanged { title },
            F::CitationsCount { count } => C::CitationsCount { count },
            F::OutlineCount { count } => C::OutlineCount { count },
            F::FileListCount { count } => C::FileListCount { count },
            F::ItemsSelected { count } => C::ItemsSelected { count },
            F::NoItemsSelected => C::NoItemsSelected,
            F::TreeFolderSelected { name } => C::TreeFolderSelected { name },
            F::RowSelected { name } => C::RowSelected { name },
            F::SwitcherRecentCount { count } => C::SwitcherRecentCount { count },
            F::SwitcherNoMatches { query } => C::SwitcherNoMatches { query },
            F::SwitcherMatchCount { count, query } => C::SwitcherMatchCount { count, query },
            F::PaletteCommandSelected {
                label,
                disabled_reason,
            } => C::PaletteCommandSelected {
                label,
                disabled_reason,
            },
            F::RecentSearchFocused { query } => C::RecentSearchFocused { query },
            F::QuickSwitcherCount { count, query } => C::QuickSwitcherCount { count, query },
            F::BaseViewMode { mode } => C::BaseViewMode { mode },
            F::BaseViewSwitcher { view_count } => C::BaseViewSwitcher { view_count },
            F::BasesNewQueryBuilder => C::BasesNewQueryBuilder,
            F::BasesEditingFilters { view_name } => C::BasesEditingFilters { view_name },
            F::BasesFiltersOpenFailed { detail } => C::BasesFiltersOpenFailed { detail },
            F::BasesPreviewFailed { detail } => C::BasesPreviewFailed { detail },
            F::BasesBuilderSaved => C::BasesBuilderSaved,
            F::BasesViewSaveFailed { detail } => C::BasesViewSaveFailed { detail },
            F::BasesSavedQueryNameNeeded => C::BasesSavedQueryNameNeeded,
            F::BasesSavedQueryCreated { name } => C::BasesSavedQueryCreated { name },
            F::BasesSavedQueryCreateFailed { detail } => C::BasesSavedQueryCreateFailed { detail },
            F::BasesSavedQueryUpdated { name } => C::BasesSavedQueryUpdated { name },
            F::BasesSavedQueryUpdateFailed { detail } => C::BasesSavedQueryUpdateFailed { detail },
            F::BasesViewSelected { name } => C::BasesViewSelected { name },
            F::BasesSortSaveFailed { detail } => C::BasesSortSaveFailed { detail },
            F::BaseRefreshed => C::BaseRefreshed,
            F::DataviewConversionFailed { detail } => C::DataviewConversionFailed { detail },
            F::CitationInsertUnavailable => C::CitationInsertUnavailable,
            F::CitationWalkThrough => C::CitationWalkThrough,
            F::CodeCopied => C::CodeCopied,
            F::HostComposed { text, priority } => C::HostComposed {
                text,
                priority: priority.into(),
            },
        }
    }
}

/// A rendered announcement: canonical spoken text + urgency, ready for
/// the platform notifier verbatim.
#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct RenderedAnnouncement {
    pub text: String,
    pub priority: A11yPriority,
}

/// Render an accessibility event to its canonical spoken form
/// (`slate_core::a11y` owns every template and priority — hosts post
/// the result verbatim and never compose announcement copy).
#[uniffi::export]
pub fn a11y_render(event: A11yEvent) -> RenderedAnnouncement {
    let event: core::a11y::A11yEvent = event.into();
    RenderedAnnouncement {
        text: event.render(),
        priority: event.priority().into(),
    }
}

/// Core Debug identity of the event — the exact string the corpus artifact
/// pins in its `event` field. Host censuses assert this to prove they
/// constructed the SAME semantic event (variant + parameters), not merely
/// one that happens to render identical text (e.g. `OpenedFile` vs an
/// `InternalNavigated { kind: "Opened", .. }` both say "Opened notes.md.").
#[uniffi::export]
pub fn a11y_event_identity(event: A11yEvent) -> String {
    let event: core::a11y::A11yEvent = event.into();
    format!("{event:?}")
}

// ---------------------------------------------------------------------------
// Bases (Milestone N, #699): 1:1 mirrors of the handle-based session API.
// No logic here -- every method delegates to core and converts shapes.

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct BaseFileSummary {
    pub path: String,
    pub name: String,
    pub view_count: u32,
    pub warning_count: u32,
    pub degraded: bool,
    pub indexed_at_ms: i64,
}

impl From<core::BaseFileSummary> for BaseFileSummary {
    fn from(s: core::BaseFileSummary) -> Self {
        BaseFileSummary {
            path: s.path,
            name: s.name,
            view_count: s.view_count,
            warning_count: s.warning_count,
            degraded: s.degraded,
            indexed_at_ms: s.indexed_at_ms,
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum BaseViewStatus {
    Executable,
    Fallback,
    Error,
}

impl From<core::BaseViewStatus> for BaseViewStatus {
    fn from(s: core::BaseViewStatus) -> Self {
        match s {
            core::BaseViewStatus::Executable => BaseViewStatus::Executable,
            core::BaseViewStatus::Fallback => BaseViewStatus::Fallback,
            core::BaseViewStatus::Error => BaseViewStatus::Error,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct BaseViewSummary {
    pub name: String,
    pub view_type: String,
    pub source: String,
    pub status: BaseViewStatus,
    /// JSON-encoded `slate.*` view state from the `.base` file. `None` means
    /// the view has no Slate state; callers should not treat an empty string as
    /// an equivalent sentinel.
    pub slate_state_json: Option<String>,
}

impl From<core::BaseViewSummary> for BaseViewSummary {
    fn from(v: core::BaseViewSummary) -> Self {
        BaseViewSummary {
            name: v.name,
            view_type: v.view_type,
            source: v.source,
            status: v.status.into(),
            slate_state_json: v.slate_state_json,
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum ColumnRole {
    Primary,
    Identifier,
    Metadata,
    Metric,
    Action,
}

impl From<core::ColumnRole> for ColumnRole {
    fn from(r: core::ColumnRole) -> Self {
        match r {
            core::ColumnRole::Primary => ColumnRole::Primary,
            core::ColumnRole::Identifier => ColumnRole::Identifier,
            core::ColumnRole::Metadata => ColumnRole::Metadata,
            core::ColumnRole::Metric => ColumnRole::Metric,
            core::ColumnRole::Action => ColumnRole::Action,
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum ExportFormat {
    Csv,
    Markdown,
}

impl From<ExportFormat> for core::ExportFormat {
    fn from(f: ExportFormat) -> Self {
        match f {
            ExportFormat::Csv => core::ExportFormat::Csv,
            ExportFormat::Markdown => core::ExportFormat::Markdown,
        }
    }
}

#[derive(Debug, Clone, PartialEq, uniffi::Record)]
pub struct BasesColumn {
    pub id: String,
    pub label: String,
    pub value_kind: String,
    pub role: ColumnRole,
}

impl From<core::BasesColumn> for BasesColumn {
    fn from(c: core::BasesColumn) -> Self {
        BasesColumn {
            id: c.id,
            label: c.label,
            value_kind: c.value_kind,
            role: c.role.into(),
        }
    }
}

#[derive(Debug, Clone, PartialEq, uniffi::Record)]
pub struct BasesValue {
    pub raw_kind: String,
    pub sort_key: String,
    pub display: String,
    pub text: Option<String>,
    pub number: Option<f64>,
    pub bool_value: Option<bool>,
    pub date_epoch_ms: Option<i64>,
    pub date_has_time: bool,
    pub link_target: Option<String>,
    pub link_display: Option<String>,
    pub list: Vec<String>,
    pub error: Option<String>,
}

impl From<core::BasesValue> for BasesValue {
    fn from(v: core::BasesValue) -> Self {
        BasesValue {
            raw_kind: v.raw_kind,
            sort_key: v.sort_key,
            display: v.display,
            text: v.text,
            number: v.number,
            bool_value: v.bool_value,
            date_epoch_ms: v.date_epoch_ms,
            date_has_time: v.date_has_time,
            link_target: v.link_target,
            link_display: v.link_display,
            list: v.list,
            error: v.error,
        }
    }
}

#[derive(Debug, Clone, PartialEq, uniffi::Record)]
pub struct BasesRow {
    pub file_path: String,
    pub task_ordinal: Option<u64>,
    pub values: Vec<BasesValue>,
    pub audio_description: String,
}

impl From<core::BasesRow> for BasesRow {
    fn from(r: core::BasesRow) -> Self {
        BasesRow {
            file_path: r.file_path,
            task_ordinal: r.task_ordinal,
            values: r.values.into_iter().map(Into::into).collect(),
            audio_description: r.audio_description,
        }
    }
}

#[derive(Debug, Clone, PartialEq, uniffi::Record)]
pub struct BasesSummaryCell {
    pub column_id: String,
    pub summary: String,
    pub value: BasesValue,
}

impl From<core::BasesSummaryCell> for BasesSummaryCell {
    fn from(s: core::BasesSummaryCell) -> Self {
        BasesSummaryCell {
            column_id: s.column_id,
            summary: s.summary,
            value: s.value.into(),
        }
    }
}

#[derive(Debug, Clone, PartialEq, uniffi::Record)]
pub struct BasesGroup {
    pub label: String,
    pub row_start: u64,
    pub row_count: u64,
    pub summaries: Vec<BasesSummaryCell>,
}

impl From<core::BasesGroup> for BasesGroup {
    fn from(g: core::BasesGroup) -> Self {
        BasesGroup {
            label: g.label,
            row_start: g.row_start,
            row_count: g.row_count,
            summaries: g.summaries.into_iter().map(Into::into).collect(),
        }
    }
}

#[derive(Debug, Clone, PartialEq, uniffi::Record)]
pub struct BasesResultSet {
    pub columns: Vec<BasesColumn>,
    pub rows: Vec<BasesRow>,
    pub groups: Vec<BasesGroup>,
    pub summaries: Vec<BasesSummaryCell>,
    pub total_count: u64,
    pub shown_count: u64,
    pub unfiltered_shown_count: u64,
    pub executed_at_ms: i64,
    pub warnings: Vec<String>,
    pub view_error: Option<String>,
    pub audio_summary: String,
}

impl From<core::BasesResultSet> for BasesResultSet {
    fn from(r: core::BasesResultSet) -> Self {
        BasesResultSet {
            columns: r.columns.into_iter().map(Into::into).collect(),
            rows: r.rows.into_iter().map(Into::into).collect(),
            groups: r.groups.into_iter().map(Into::into).collect(),
            summaries: r.summaries.into_iter().map(Into::into).collect(),
            total_count: r.total_count,
            shown_count: r.shown_count,
            unfiltered_shown_count: r.unfiltered_shown_count,
            executed_at_ms: r.executed_at_ms,
            warnings: r.warnings,
            view_error: r.view_error,
            audio_summary: r.audio_summary,
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum SavedQuerySourceSyntax {
    Builder,
    Base,
    Dql,
}

impl From<core::SavedQuerySourceSyntax> for SavedQuerySourceSyntax {
    fn from(s: core::SavedQuerySourceSyntax) -> Self {
        match s {
            core::SavedQuerySourceSyntax::Builder => SavedQuerySourceSyntax::Builder,
            core::SavedQuerySourceSyntax::Base => SavedQuerySourceSyntax::Base,
            core::SavedQuerySourceSyntax::Dql => SavedQuerySourceSyntax::Dql,
        }
    }
}

impl From<SavedQuerySourceSyntax> for core::SavedQuerySourceSyntax {
    fn from(s: SavedQuerySourceSyntax) -> Self {
        match s {
            SavedQuerySourceSyntax::Builder => core::SavedQuerySourceSyntax::Builder,
            SavedQuerySourceSyntax::Base => core::SavedQuerySourceSyntax::Base,
            SavedQuerySourceSyntax::Dql => core::SavedQuerySourceSyntax::Dql,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct SavedQuerySummary {
    pub id: String,
    pub name: String,
    pub description: Option<String>,
    pub source_syntax: SavedQuerySourceSyntax,
    pub created_at_ms: i64,
    pub modified_at_ms: i64,
    pub warning: Option<String>,
}

impl From<core::SavedQuerySummary> for SavedQuerySummary {
    fn from(s: core::SavedQuerySummary) -> Self {
        SavedQuerySummary {
            id: s.id,
            name: s.name,
            description: s.description,
            source_syntax: s.source_syntax.into(),
            created_at_ms: s.created_at_ms,
            modified_at_ms: s.modified_at_ms,
            warning: s.warning,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct SavedQuery {
    pub id: String,
    pub name: String,
    pub description: Option<String>,
    pub query_json: String,
    pub source_syntax: SavedQuerySourceSyntax,
    pub created_at_ms: i64,
    pub modified_at_ms: i64,
    pub warning: Option<String>,
}

impl From<core::SavedQuery> for SavedQuery {
    fn from(s: core::SavedQuery) -> Self {
        SavedQuery {
            id: s.id,
            name: s.name,
            description: s.description,
            query_json: s.query_json,
            source_syntax: s.source_syntax.into(),
            created_at_ms: s.created_at_ms,
            modified_at_ms: s.modified_at_ms,
            warning: s.warning,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct DashboardSection {
    pub saved_query_id: String,
    pub heading_override: Option<String>,
    pub view_override: Option<String>,
}

impl From<DashboardSection> for core::DashboardSection {
    fn from(s: DashboardSection) -> Self {
        core::DashboardSection {
            saved_query_id: s.saved_query_id,
            heading_override: s.heading_override,
            view_override: s.view_override,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct DashboardSectionStatus {
    pub saved_query_id: String,
    pub saved_query_name: Option<String>,
    pub heading_override: Option<String>,
    pub view_override: Option<String>,
    pub missing: bool,
}

impl From<core::DashboardSectionStatus> for DashboardSectionStatus {
    fn from(s: core::DashboardSectionStatus) -> Self {
        DashboardSectionStatus {
            saved_query_id: s.saved_query_id,
            saved_query_name: s.saved_query_name,
            heading_override: s.heading_override,
            view_override: s.view_override,
            missing: s.missing,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct DashboardSummary {
    pub id: String,
    pub name: String,
    pub section_count: u32,
    pub created_at_ms: i64,
    pub modified_at_ms: i64,
}

impl From<core::DashboardSummary> for DashboardSummary {
    fn from(s: core::DashboardSummary) -> Self {
        DashboardSummary {
            id: s.id,
            name: s.name,
            section_count: s.section_count,
            created_at_ms: s.created_at_ms,
            modified_at_ms: s.modified_at_ms,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct Dashboard {
    pub id: String,
    pub name: String,
    pub sections: Vec<DashboardSectionStatus>,
    pub created_at_ms: i64,
    pub modified_at_ms: i64,
}

impl From<core::Dashboard> for Dashboard {
    fn from(d: core::Dashboard) -> Self {
        Dashboard {
            id: d.id,
            name: d.name,
            sections: d.sections.into_iter().map(Into::into).collect(),
            created_at_ms: d.created_at_ms,
            modified_at_ms: d.modified_at_ms,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct BaseExpressionValidation {
    pub valid: bool,
    pub expr_json: Option<String>,
    pub message: Option<String>,
    pub span_start: u32,
    pub span_end: u32,
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct SlateQueryFenceClassification {
    pub query: Option<String>,
    pub view: Option<String>,
}

impl From<core::bases::SlateQueryFenceClassification> for SlateQueryFenceClassification {
    fn from(value: core::bases::SlateQueryFenceClassification) -> Self {
        Self {
            query: value.query,
            view: value.view,
        }
    }
}

/// Decode the routing fields of a dual-mode `slate-query` fence with the
/// same full YAML parser used by Core's Bases implementation.
#[uniffi::export]
pub fn classify_slate_query_fence(
    source: String,
) -> Result<SlateQueryFenceClassification, VaultError> {
    core::bases::classify_slate_query_fence(&source)
        .map(Into::into)
        .map_err(|error| VaultError::InvalidQuery {
            message: error.to_string(),
        })
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Enum)]
pub enum BaseEdit {
    SetViewKey {
        view: u32,
        key: String,
        value: String,
    },
    AddView {
        yaml: String,
    },
    RemoveView {
        view: u32,
    },
    RenameView {
        view: u32,
        name: String,
    },
    RemoveViewKey {
        view: u32,
        key: String,
    },
    SetViewFilters {
        view: u32,
        yaml: String,
    },
    SetTopLevelFilters {
        yaml: String,
    },
    SetFormula {
        name: String,
        expression: String,
    },
    RemoveFormula {
        name: String,
    },
    SetDisplayName {
        property: String,
        display_name: Option<String>,
    },
    SetSummaryAssignment {
        view: u32,
        property: String,
        summary: Option<String>,
    },
    SetSlateState {
        view: u32,
        yaml: Option<String>,
    },
    SetSlateSort {
        view: u32,
        yaml: Option<String>,
    },
}

impl From<BaseEdit> for core::bases::BaseEdit {
    fn from(e: BaseEdit) -> Self {
        match e {
            BaseEdit::SetViewKey { view, key, value } => core::bases::BaseEdit::SetViewKey {
                view: view as usize,
                key,
                value,
            },
            BaseEdit::AddView { yaml } => core::bases::BaseEdit::AddView { yaml },
            BaseEdit::RemoveView { view } => core::bases::BaseEdit::RemoveView {
                view: view as usize,
            },
            BaseEdit::RenameView { view, name } => core::bases::BaseEdit::RenameView {
                view: view as usize,
                name,
            },
            BaseEdit::RemoveViewKey { view, key } => core::bases::BaseEdit::RemoveViewKey {
                view: view as usize,
                key,
            },
            BaseEdit::SetViewFilters { view, yaml } => core::bases::BaseEdit::SetViewFilters {
                view: view as usize,
                yaml,
            },
            BaseEdit::SetTopLevelFilters { yaml } => {
                core::bases::BaseEdit::SetTopLevelFilters { yaml }
            }
            BaseEdit::SetFormula { name, expression } => {
                core::bases::BaseEdit::SetFormula { name, expression }
            }
            BaseEdit::RemoveFormula { name } => core::bases::BaseEdit::RemoveFormula { name },
            BaseEdit::SetDisplayName {
                property,
                display_name,
            } => core::bases::BaseEdit::SetDisplayName {
                property,
                display_name,
            },
            BaseEdit::SetSummaryAssignment {
                view,
                property,
                summary,
            } => core::bases::BaseEdit::SetSummaryAssignment {
                view: view as usize,
                property,
                summary,
            },
            BaseEdit::SetSlateState { view, yaml } => core::bases::BaseEdit::SetSlateState {
                view: view as usize,
                yaml,
            },
            BaseEdit::SetSlateSort { view, yaml } => core::bases::BaseEdit::SetSlateSort {
                view: view as usize,
                yaml,
            },
        }
    }
}

#[uniffi::export]
impl VaultSession {
    pub fn bases_list(&self) -> Result<Vec<BaseFileSummary>, VaultError> {
        Ok(self
            .inner
            .bases_list()?
            .into_iter()
            .map(Into::into)
            .collect())
    }

    pub fn open_base(&self, path: String) -> Result<u64, VaultError> {
        Ok(self.inner.open_base(&path)?)
    }

    pub fn open_base_inline(
        &self,
        source: String,
        this_path: Option<String>,
    ) -> Result<u64, VaultError> {
        Ok(self.inner.open_base_inline(&source, this_path)?)
    }

    pub fn close_base(&self, handle: u64) {
        self.inner.close_base(handle);
    }

    pub fn base_views(&self, handle: u64) -> Result<Vec<BaseViewSummary>, VaultError> {
        Ok(self
            .inner
            .base_views(handle)?
            .into_iter()
            .map(Into::into)
            .collect())
    }

    pub fn base_view_query_json(&self, handle: u64, view: u32) -> Result<String, VaultError> {
        Ok(self.inner.base_view_query_json(handle, view)?)
    }

    pub fn base_view_edit_query_json(&self, handle: u64, view: u32) -> Result<String, VaultError> {
        Ok(self.inner.base_view_edit_query_json(handle, view)?)
    }

    pub fn validate_base_expression(&self, source: String) -> BaseExpressionValidation {
        match core::bases::expr::parse_expr(&source) {
            Ok(expr) => match serde_json::to_string(&expr) {
                Ok(expr_json) => BaseExpressionValidation {
                    valid: true,
                    expr_json: Some(expr_json),
                    message: None,
                    span_start: expr.span.start,
                    span_end: expr.span.end,
                },
                Err(err) => BaseExpressionValidation {
                    valid: false,
                    expr_json: None,
                    message: Some(format!("could not encode expression: {err}")),
                    span_start: 0,
                    span_end: source.len() as u32,
                },
            },
            Err(err) => BaseExpressionValidation {
                valid: false,
                expr_json: None,
                message: Some(err.message),
                span_start: err.span.start,
                span_end: err.span.end,
            },
        }
    }

    pub fn base_execute(
        &self,
        handle: u64,
        view: u32,
        this_path: Option<String>,
        quick_filter: Option<String>,
        cancel: Arc<CancelToken>,
    ) -> Result<BasesResultSet, VaultError> {
        Ok(self
            .inner
            .base_execute(handle, view, this_path, quick_filter, &cancel.inner)?
            .into())
    }

    pub fn base_set_transient_sort(
        &self,
        handle: u64,
        view: u32,
        column_id: Option<String>,
        ascending: bool,
    ) -> Result<(), VaultError> {
        Ok(self
            .inner
            .base_set_transient_sort(handle, view, column_id, ascending)?)
    }

    pub fn open_query(
        &self,
        query_json: String,
        this_path: Option<String>,
    ) -> Result<u64, VaultError> {
        Ok(self.inner.open_query(&query_json, this_path)?)
    }

    pub fn open_saved_query(&self, id: String) -> Result<u64, VaultError> {
        Ok(self.inner.open_saved_query(&id)?)
    }

    pub fn save_query(
        &self,
        name: String,
        description: Option<String>,
        query_json: String,
        source_syntax: SavedQuerySourceSyntax,
    ) -> Result<String, VaultError> {
        Ok(self.inner.save_query(
            &name,
            description.as_deref(),
            &query_json,
            source_syntax.into(),
        )?)
    }

    pub fn list_saved_queries(&self) -> Result<Vec<SavedQuerySummary>, VaultError> {
        Ok(self
            .inner
            .list_saved_queries()?
            .into_iter()
            .map(Into::into)
            .collect())
    }

    pub fn get_saved_query(&self, id: String) -> Result<SavedQuery, VaultError> {
        Ok(self.inner.get_saved_query(&id)?.into())
    }

    pub fn rename_saved_query(&self, id: String, name: String) -> Result<(), VaultError> {
        Ok(self.inner.rename_saved_query(&id, &name)?)
    }

    pub fn update_saved_query(
        &self,
        id: String,
        description: Option<String>,
        query_json: String,
        source_syntax: SavedQuerySourceSyntax,
    ) -> Result<(), VaultError> {
        Ok(self.inner.update_saved_query(
            &id,
            description.as_deref(),
            &query_json,
            source_syntax.into(),
        )?)
    }

    pub fn delete_saved_query(&self, id: String) -> Result<(), VaultError> {
        Ok(self.inner.delete_saved_query(&id)?)
    }

    pub fn export_saved_query_as_base(&self, id: String, path: String) -> Result<(), VaultError> {
        Ok(self.inner.export_saved_query_as_base(&id, &path)?)
    }

    pub fn save_dashboard(
        &self,
        name: String,
        sections: Vec<DashboardSection>,
    ) -> Result<String, VaultError> {
        Ok(self
            .inner
            .save_dashboard(&name, sections.into_iter().map(Into::into).collect())?)
    }

    pub fn list_dashboards(&self) -> Result<Vec<DashboardSummary>, VaultError> {
        Ok(self
            .inner
            .list_dashboards()?
            .into_iter()
            .map(Into::into)
            .collect())
    }

    pub fn get_dashboard(&self, id: String) -> Result<Dashboard, VaultError> {
        Ok(self.inner.get_dashboard(&id)?.into())
    }

    pub fn update_dashboard(
        &self,
        id: String,
        name: String,
        sections: Vec<DashboardSection>,
    ) -> Result<(), VaultError> {
        Ok(self.inner.update_dashboard(
            &id,
            &name,
            sections.into_iter().map(Into::into).collect(),
        )?)
    }

    pub fn rename_dashboard(&self, id: String, name: String) -> Result<(), VaultError> {
        Ok(self.inner.rename_dashboard(&id, &name)?)
    }

    pub fn update_dashboard_sections(
        &self,
        id: String,
        sections: Vec<DashboardSection>,
    ) -> Result<(), VaultError> {
        Ok(self
            .inner
            .update_dashboard_sections(&id, sections.into_iter().map(Into::into).collect())?)
    }

    pub fn delete_dashboard(&self, id: String) -> Result<(), VaultError> {
        Ok(self.inner.delete_dashboard(&id)?)
    }

    pub fn open_dql(&self, source: String, this_path: Option<String>) -> Result<u64, VaultError> {
        Ok(self.inner.open_dql(&source, this_path)?)
    }

    pub fn base_apply_edit(&self, handle: u64, edit: BaseEdit) -> Result<(), VaultError> {
        Ok(self.inner.base_apply_edit(handle, edit.into())?)
    }

    pub fn base_apply_edits(&self, handle: u64, edits: Vec<BaseEdit>) -> Result<(), VaultError> {
        Ok(self
            .inner
            .base_apply_edits(handle, edits.into_iter().map(Into::into).collect())?)
    }

    pub fn save_query_as_base(&self, query_json: String, path: String) -> Result<(), VaultError> {
        Ok(self.inner.save_query_as_base(&query_json, &path)?)
    }

    pub fn dql_as_base(&self, source: String) -> Result<String, VaultError> {
        Ok(self.inner.dql_as_base(&source)?)
    }

    pub fn base_export(
        &self,
        handle: u64,
        view: u32,
        format: ExportFormat,
        quick_filter: Option<String>,
    ) -> Result<String, VaultError> {
        Ok(self
            .inner
            .base_export(handle, view, format.into(), quick_filter)?)
    }
}

// ---------------------------------------------------------------------------
// Canvas (Milestone T, #361): 1:1 mirrors of the handle-based read API.
// No logic here — every method delegates to core and converts shapes.

/// One outline row (depth-first flattening of the canvas model).
#[derive(Debug, Clone, PartialEq, uniffi::Record)]
pub struct CanvasOutlineRow {
    pub node_id: String,
    pub depth: u32,
    /// "text" | "file" | "image" | "link" | "group" (t0 §1.1 type word).
    pub kind: String,
    pub title: String,
    pub group_path: Vec<String>,
    pub ordinal_n: u32,
    pub total_m: u32,
    pub connection_count: u32,
    pub color_name: Option<String>,
}

impl From<core::CanvasOutlineRow> for CanvasOutlineRow {
    fn from(r: core::CanvasOutlineRow) -> Self {
        CanvasOutlineRow {
            node_id: r.node_id,
            depth: r.depth,
            kind: r.kind,
            title: r.title,
            group_path: r.group_path,
            ordinal_n: r.ordinal_n,
            total_m: r.total_m,
            connection_count: r.connection_count,
            color_name: r.color_name,
        }
    }
}

/// One table row (flat, sortable view — #363).
#[derive(Debug, Clone, PartialEq, uniffi::Record)]
pub struct CanvasTableRow {
    pub node_id: String,
    pub kind: String,
    pub title: String,
    pub group_path: Vec<String>,
    pub target: String,
    pub connection_count: u32,
    pub color_name: Option<String>,
}

impl From<core::CanvasTableRow> for CanvasTableRow {
    fn from(r: core::CanvasTableRow) -> Self {
        CanvasTableRow {
            node_id: r.node_id,
            kind: r.kind,
            title: r.title,
            group_path: r.group_path,
            target: r.target,
            connection_count: r.connection_count,
            color_name: r.color_name,
        }
    }
}

/// Direction of a connection from the queried node's perspective
/// (t0 §1.2: "connects to" / "connected from" / "linked with").
#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum CanvasEdgeDirection {
    Outgoing,
    Incoming,
    Bidirectional,
    Undirected,
}

impl From<core::canvas::model::EdgeDirection> for CanvasEdgeDirection {
    fn from(d: core::canvas::model::EdgeDirection) -> Self {
        use core::canvas::model::EdgeDirection as D;
        match d {
            D::Outgoing => CanvasEdgeDirection::Outgoing,
            D::Incoming => CanvasEdgeDirection::Incoming,
            D::Bidirectional => CanvasEdgeDirection::Bidirectional,
            D::Undirected => CanvasEdgeDirection::Undirected,
        }
    }
}

/// Which side of a node a connection attaches to.
#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum CanvasSide {
    Top,
    Right,
    Bottom,
    Left,
}

impl From<core::canvas::Side> for CanvasSide {
    fn from(s: core::canvas::Side) -> Self {
        use core::canvas::Side as S;
        match s {
            S::Top => CanvasSide::Top,
            S::Right => CanvasSide::Right,
            S::Bottom => CanvasSide::Bottom,
            S::Left => CanvasSide::Left,
        }
    }
}

/// One adjacency entry with the raw phrasing data (#518 consumes).
#[derive(Debug, Clone, PartialEq, uniffi::Record)]
pub struct CanvasNeighbor {
    pub edge_id: String,
    pub other_node: String,
    pub other_title: String,
    pub direction: CanvasEdgeDirection,
    pub self_side: Option<CanvasSide>,
    pub label: Option<String>,
    pub self_is_from: bool,
}

impl From<core::CanvasNeighbor> for CanvasNeighbor {
    fn from(n: core::CanvasNeighbor) -> Self {
        CanvasNeighbor {
            edge_id: n.edge_id,
            other_node: n.other_node,
            other_title: n.other_title,
            direction: n.direction.into(),
            self_side: n.self_side.map(Into::into),
            label: n.label,
            self_is_from: n.self_is_from,
        }
    }
}

/// The ⌃⌘I "Where am I?" readback context (t0 §1.4).
#[derive(Debug, Clone, PartialEq, uniffi::Record)]
pub struct CanvasWhereAmI {
    pub node_id: String,
    pub title: String,
    pub kind: String,
    pub group_path: Vec<String>,
    pub ordinal_n: u32,
    pub total_m: u32,
    pub connection_count: u32,
    pub in_count: u32,
    pub out_count: u32,
    pub color_name: Option<String>,
}

impl From<core::CanvasWhereAmI> for CanvasWhereAmI {
    fn from(w: core::CanvasWhereAmI) -> Self {
        CanvasWhereAmI {
            node_id: w.node_id,
            title: w.title,
            kind: w.kind,
            group_path: w.group_path,
            ordinal_n: w.ordinal_n,
            total_m: w.total_m,
            connection_count: w.connection_count,
            in_count: w.in_count,
            out_count: w.out_count,
            color_name: w.color_name,
        }
    }
}

/// Load-warning classification for t0 §5 phrasing.
#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum CanvasLoadWarningKind {
    ParseFailed,
    SkippedEntry,
    DanglingEdge,
    IgnoredValue,
}

impl From<core::CanvasLoadWarningKind> for CanvasLoadWarningKind {
    fn from(k: core::CanvasLoadWarningKind) -> Self {
        use core::CanvasLoadWarningKind as K;
        match k {
            K::ParseFailed => CanvasLoadWarningKind::ParseFailed,
            K::SkippedEntry => CanvasLoadWarningKind::SkippedEntry,
            K::DanglingEdge => CanvasLoadWarningKind::DanglingEdge,
            K::IgnoredValue => CanvasLoadWarningKind::IgnoredValue,
        }
    }
}

#[derive(Debug, Clone, PartialEq, uniffi::Record)]
pub struct CanvasLoadWarning {
    pub kind: CanvasLoadWarningKind,
    pub detail: String,
}

impl From<core::CanvasLoadWarning> for CanvasLoadWarning {
    fn from(w: core::CanvasLoadWarning) -> Self {
        CanvasLoadWarning {
            kind: w.kind.into(),
            detail: w.detail,
        }
    }
}

/// Result of `open_canvas`. A `degraded` canvas is read-only (t0 §5).
#[derive(Debug, Clone, PartialEq, uniffi::Record)]
pub struct CanvasOpenInfo {
    pub handle: u64,
    pub node_count: u32,
    pub edge_count: u32,
    pub degraded: bool,
    pub warnings: Vec<CanvasLoadWarning>,
}

impl From<core::CanvasOpenInfo> for CanvasOpenInfo {
    fn from(i: core::CanvasOpenInfo) -> Self {
        CanvasOpenInfo {
            handle: i.handle,
            node_count: i.node_count,
            edge_count: i.edge_count,
            degraded: i.degraded,
            warnings: i.warnings.into_iter().map(Into::into).collect(),
        }
    }
}

/// Placement direction preference / hint (#517).
#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum CanvasPlaceDirection {
    Below,
    RightOf,
    Above,
    LeftOf,
}

impl From<CanvasPlaceDirection> for core::canvas::placement::PlaceDirection {
    fn from(d: CanvasPlaceDirection) -> Self {
        use core::canvas::placement::PlaceDirection as P;
        match d {
            CanvasPlaceDirection::Below => P::Below,
            CanvasPlaceDirection::RightOf => P::RightOf,
            CanvasPlaceDirection::Above => P::Above,
            CanvasPlaceDirection::LeftOf => P::LeftOf,
        }
    }
}

/// Typed relative-position description; `anchor_title` is empty for
/// `AtOrigin`. Phrasing stays UI-side (#518 grammar tables).
#[derive(Debug, Clone, PartialEq, uniffi::Enum)]
pub enum CanvasRelativeDesc {
    Below { anchor_title: String },
    RightOf { anchor_title: String },
    Above { anchor_title: String },
    LeftOf { anchor_title: String },
    AtOrigin,
}

impl From<core::canvas::placement::RelativeDesc> for CanvasRelativeDesc {
    fn from(r: core::canvas::placement::RelativeDesc) -> Self {
        use core::canvas::placement::RelativeDesc as R;
        match r {
            R::Below(t) => CanvasRelativeDesc::Below { anchor_title: t },
            R::RightOf(t) => CanvasRelativeDesc::RightOf { anchor_title: t },
            R::Above(t) => CanvasRelativeDesc::Above { anchor_title: t },
            R::LeftOf(t) => CanvasRelativeDesc::LeftOf { anchor_title: t },
            R::AtOrigin => CanvasRelativeDesc::AtOrigin,
        }
    }
}

/// Geometry argument for placement / overlap queries.
#[derive(Debug, Clone, Copy, PartialEq, uniffi::Record)]
pub struct CanvasRect {
    pub x: f64,
    pub y: f64,
    pub width: f64,
    pub height: f64,
}

impl From<CanvasRect> for core::CanvasRectArg {
    fn from(r: CanvasRect) -> Self {
        core::CanvasRectArg {
            x: r.x,
            y: r.y,
            width: r.width,
            height: r.height,
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, uniffi::Record)]
pub struct CanvasPoint {
    pub x: f64,
    pub y: f64,
}

/// A computed placement for one new card (#517).
#[derive(Debug, Clone, PartialEq, uniffi::Record)]
pub struct CanvasPlacement {
    pub x: f64,
    pub y: f64,
    pub relative: CanvasRelativeDesc,
}

impl From<core::CanvasPlacement> for CanvasPlacement {
    fn from(p: core::CanvasPlacement) -> Self {
        CanvasPlacement {
            x: p.x,
            y: p.y,
            relative: p.relative.into(),
        }
    }
}

/// A computed rigid-set placement (pairwise offsets preserved).
#[derive(Debug, Clone, PartialEq, uniffi::Record)]
pub struct CanvasSetPlacement {
    pub origins: Vec<CanvasPoint>,
    pub relative: CanvasRelativeDesc,
}

impl From<core::CanvasSetPlacement> for CanvasSetPlacement {
    fn from(p: core::CanvasSetPlacement) -> Self {
        CanvasSetPlacement {
            origins: p
                .origins
                .into_iter()
                .map(|(x, y)| CanvasPoint { x, y })
                .collect(),
            relative: p.relative.into(),
        }
    }
}

/// Connection end decoration (spec defaults: from = none, to = arrow).
#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum CanvasEndStyle {
    None,
    Arrow,
}

impl From<CanvasEndStyle> for core::canvas::EndStyle {
    fn from(e: CanvasEndStyle) -> Self {
        match e {
            CanvasEndStyle::None => core::canvas::EndStyle::None,
            CanvasEndStyle::Arrow => core::canvas::EndStyle::Arrow,
        }
    }
}

impl From<core::canvas::EndStyle> for CanvasEndStyle {
    fn from(e: core::canvas::EndStyle) -> Self {
        match e {
            core::canvas::EndStyle::None => CanvasEndStyle::None,
            core::canvas::EndStyle::Arrow => CanvasEndStyle::Arrow,
        }
    }
}

impl From<CanvasSide> for core::canvas::Side {
    fn from(s: CanvasSide) -> Self {
        match s {
            CanvasSide::Top => core::canvas::Side::Top,
            CanvasSide::Right => core::canvas::Side::Right,
            CanvasSide::Bottom => core::canvas::Side::Bottom,
            CanvasSide::Left => core::canvas::Side::Left,
        }
    }
}

/// New-card payload for create/set-content ops.
#[derive(Debug, Clone, PartialEq, uniffi::Enum)]
pub enum CanvasNodeContent {
    Text {
        text: String,
    },
    File {
        file: String,
        subpath: Option<String>,
    },
    Link {
        url: String,
    },
}

impl From<CanvasNodeContent> for core::canvas::apply::CanvasNodeContent {
    fn from(c: CanvasNodeContent) -> Self {
        use core::canvas::apply::CanvasNodeContent as C;
        match c {
            CanvasNodeContent::Text { text } => C::Text { text },
            CanvasNodeContent::File { file, subpath } => C::File { file, subpath },
            CanvasNodeContent::Link { url } => C::Link { url },
        }
    }
}

impl From<core::canvas::apply::CanvasNodeContent> for CanvasNodeContent {
    fn from(c: core::canvas::apply::CanvasNodeContent) -> Self {
        use core::canvas::apply::CanvasNodeContent as C;
        match c {
            C::Text { text } => CanvasNodeContent::Text { text },
            C::File { file, subpath } => CanvasNodeContent::File { file, subpath },
            C::Link { url } => CanvasNodeContent::Link { url },
        }
    }
}

/// One primitive canvas mutation (t1 op set). The `Restore*` variants
/// are undo-only payloads produced by the engine — the UI passes them
/// back verbatim inside an inverse action, never constructs them.
#[derive(Debug, Clone, PartialEq, uniffi::Enum)]
pub enum CanvasOp {
    CreateNode {
        id: String,
        content: CanvasNodeContent,
        x: f64,
        y: f64,
        width: f64,
        height: f64,
        color: Option<String>,
    },
    CreateGroup {
        id: String,
        label: Option<String>,
        x: f64,
        y: f64,
        width: f64,
        height: f64,
        color: Option<String>,
    },
    UpdateNodeGeometry {
        id: String,
        x: f64,
        y: f64,
        width: f64,
        height: f64,
    },
    SetNodeColor {
        id: String,
        color: Option<String>,
    },
    SetNodeContent {
        id: String,
        content: CanvasNodeContent,
    },
    DeleteNode {
        id: String,
    },
    AddEdge {
        id: String,
        from_node: String,
        from_side: Option<CanvasSide>,
        to_node: String,
        to_side: Option<CanvasSide>,
        from_end: CanvasEndStyle,
        to_end: CanvasEndStyle,
        label: Option<String>,
        color: Option<String>,
    },
    UpdateEdge {
        id: String,
        from_side: Option<CanvasSide>,
        to_side: Option<CanvasSide>,
        from_end: CanvasEndStyle,
        to_end: CanvasEndStyle,
        label: Option<String>,
        color: Option<String>,
    },
    DeleteEdge {
        id: String,
    },
    RenameGroup {
        id: String,
        label: Option<String>,
    },
    Ungroup {
        id: String,
    },
    RestoreNode {
        node_json: String,
        position: u32,
    },
    RestoreEdge {
        edge_json: String,
        position: u32,
    },
    RestoreNodeInPlace {
        node_json: String,
    },
    RestoreEdgeInPlace {
        edge_json: String,
    },
}

impl From<CanvasOp> for core::canvas::apply::CanvasOp {
    fn from(op: CanvasOp) -> Self {
        use core::canvas::apply::CanvasOp as O;
        match op {
            CanvasOp::CreateNode {
                id,
                content,
                x,
                y,
                width,
                height,
                color,
            } => O::CreateNode {
                id,
                content: content.into(),
                x,
                y,
                width,
                height,
                color,
            },
            CanvasOp::CreateGroup {
                id,
                label,
                x,
                y,
                width,
                height,
                color,
            } => O::CreateGroup {
                id,
                label,
                x,
                y,
                width,
                height,
                color,
            },
            CanvasOp::UpdateNodeGeometry {
                id,
                x,
                y,
                width,
                height,
            } => O::UpdateNodeGeometry {
                id,
                x,
                y,
                width,
                height,
            },
            CanvasOp::SetNodeColor { id, color } => O::SetNodeColor { id, color },
            CanvasOp::SetNodeContent { id, content } => O::SetNodeContent {
                id,
                content: content.into(),
            },
            CanvasOp::DeleteNode { id } => O::DeleteNode { id },
            CanvasOp::AddEdge {
                id,
                from_node,
                from_side,
                to_node,
                to_side,
                from_end,
                to_end,
                label,
                color,
            } => O::AddEdge {
                id,
                from_node,
                from_side: from_side.map(Into::into),
                to_node,
                to_side: to_side.map(Into::into),
                from_end: from_end.into(),
                to_end: to_end.into(),
                label,
                color,
            },
            CanvasOp::UpdateEdge {
                id,
                from_side,
                to_side,
                from_end,
                to_end,
                label,
                color,
            } => O::UpdateEdge {
                id,
                from_side: from_side.map(Into::into),
                to_side: to_side.map(Into::into),
                from_end: from_end.into(),
                to_end: to_end.into(),
                label,
                color,
            },
            CanvasOp::DeleteEdge { id } => O::DeleteEdge { id },
            CanvasOp::RenameGroup { id, label } => O::RenameGroup { id, label },
            CanvasOp::Ungroup { id } => O::Ungroup { id },
            CanvasOp::RestoreNode {
                node_json,
                position,
            } => O::RestoreNode {
                node_json,
                position,
            },
            CanvasOp::RestoreEdge {
                edge_json,
                position,
            } => O::RestoreEdge {
                edge_json,
                position,
            },
            CanvasOp::RestoreNodeInPlace { node_json } => O::RestoreNodeInPlace { node_json },
            CanvasOp::RestoreEdgeInPlace { edge_json } => O::RestoreEdgeInPlace { edge_json },
        }
    }
}

impl From<core::canvas::apply::CanvasOp> for CanvasOp {
    fn from(op: core::canvas::apply::CanvasOp) -> Self {
        use core::canvas::apply::CanvasOp as O;
        match op {
            O::CreateNode {
                id,
                content,
                x,
                y,
                width,
                height,
                color,
            } => CanvasOp::CreateNode {
                id,
                content: content.into(),
                x,
                y,
                width,
                height,
                color,
            },
            O::CreateGroup {
                id,
                label,
                x,
                y,
                width,
                height,
                color,
            } => CanvasOp::CreateGroup {
                id,
                label,
                x,
                y,
                width,
                height,
                color,
            },
            O::UpdateNodeGeometry {
                id,
                x,
                y,
                width,
                height,
            } => CanvasOp::UpdateNodeGeometry {
                id,
                x,
                y,
                width,
                height,
            },
            O::SetNodeColor { id, color } => CanvasOp::SetNodeColor { id, color },
            O::SetNodeContent { id, content } => CanvasOp::SetNodeContent {
                id,
                content: content.into(),
            },
            O::DeleteNode { id } => CanvasOp::DeleteNode { id },
            O::AddEdge {
                id,
                from_node,
                from_side,
                to_node,
                to_side,
                from_end,
                to_end,
                label,
                color,
            } => CanvasOp::AddEdge {
                id,
                from_node,
                from_side: from_side.map(Into::into),
                to_node,
                to_side: to_side.map(Into::into),
                from_end: from_end.into(),
                to_end: to_end.into(),
                label,
                color,
            },
            O::UpdateEdge {
                id,
                from_side,
                to_side,
                from_end,
                to_end,
                label,
                color,
            } => CanvasOp::UpdateEdge {
                id,
                from_side: from_side.map(Into::into),
                to_side: to_side.map(Into::into),
                from_end: from_end.into(),
                to_end: to_end.into(),
                label,
                color,
            },
            O::DeleteEdge { id } => CanvasOp::DeleteEdge { id },
            O::RenameGroup { id, label } => CanvasOp::RenameGroup { id, label },
            O::Ungroup { id } => CanvasOp::Ungroup { id },
            O::RestoreNode {
                node_json,
                position,
            } => CanvasOp::RestoreNode {
                node_json,
                position,
            },
            O::RestoreEdge {
                edge_json,
                position,
            } => CanvasOp::RestoreEdge {
                edge_json,
                position,
            },
            O::RestoreNodeInPlace { node_json } => CanvasOp::RestoreNodeInPlace { node_json },
            O::RestoreEdgeInPlace { edge_json } => CanvasOp::RestoreEdgeInPlace { edge_json },
        }
    }
}

/// A named, undoable batch of ops — one committed user action.
#[derive(Debug, Clone, PartialEq, uniffi::Record)]
pub struct CanvasAction {
    pub name: String,
    pub ops: Vec<CanvasOp>,
}

impl From<CanvasAction> for core::canvas::apply::CanvasAction {
    fn from(a: CanvasAction) -> Self {
        core::canvas::apply::CanvasAction {
            name: a.name,
            ops: a.ops.into_iter().map(Into::into).collect(),
        }
    }
}

impl From<core::canvas::apply::CanvasAction> for CanvasAction {
    fn from(a: core::canvas::apply::CanvasAction) -> Self {
        CanvasAction {
            name: a.name,
            ops: a.ops.into_iter().map(Into::into).collect(),
        }
    }
}

/// Result of `canvas_apply`: post-write hash + the inverse action for
/// the session-scoped undo stack (#372).
#[derive(Debug, Clone, PartialEq, uniffi::Record)]
pub struct CanvasApplyResult {
    pub new_content_hash: String,
    pub inverse: CanvasAction,
}

impl From<core::CanvasApplyResult> for CanvasApplyResult {
    fn from(r: core::CanvasApplyResult) -> Self {
        CanvasApplyResult {
            new_content_hash: r.new_content_hash,
            inverse: r.inverse.into(),
        }
    }
}

/// One node's render geometry (visual renderer, #367).
#[derive(Debug, Clone, PartialEq, uniffi::Record)]
pub struct CanvasSceneNode {
    pub node_id: String,
    pub kind: String,
    pub title: String,
    pub x: f64,
    pub y: f64,
    pub width: f64,
    pub height: f64,
    pub color: Option<String>,
    pub color_name: Option<String>,
    pub subpath: Option<String>,
}

impl From<core::CanvasSceneNode> for CanvasSceneNode {
    fn from(n: core::CanvasSceneNode) -> Self {
        CanvasSceneNode {
            node_id: n.node_id,
            kind: n.kind,
            title: n.title,
            x: n.x,
            y: n.y,
            width: n.width,
            height: n.height,
            color: n.color,
            color_name: n.color_name,
            subpath: n.subpath,
        }
    }
}

/// One connection's render data (visual renderer, #367).
#[derive(Debug, Clone, PartialEq, uniffi::Record)]
pub struct CanvasSceneEdge {
    pub edge_id: String,
    pub from_node: String,
    pub from_side: Option<CanvasSide>,
    pub to_node: String,
    pub to_side: Option<CanvasSide>,
    pub from_arrow: bool,
    pub to_arrow: bool,
    pub label: Option<String>,
    pub color: Option<String>,
}

impl From<core::CanvasSceneEdge> for CanvasSceneEdge {
    fn from(e: core::CanvasSceneEdge) -> Self {
        CanvasSceneEdge {
            edge_id: e.edge_id,
            from_node: e.from_node,
            from_side: e.from_side.map(Into::into),
            to_node: e.to_node,
            to_side: e.to_side.map(Into::into),
            from_arrow: e.from_arrow,
            to_arrow: e.to_arrow,
            label: e.label,
            color: e.color,
        }
    }
}

/// The full render scene for one canvas.
#[derive(Debug, Clone, PartialEq, uniffi::Record)]
pub struct CanvasScene {
    pub nodes: Vec<CanvasSceneNode>,
    pub edges: Vec<CanvasSceneEdge>,
}

#[uniffi::export]
impl VaultSession {
    /// The render scene (geometry in document order + edges) for the
    /// visual surface (#367).
    pub fn canvas_scene(&self, handle: u64) -> Result<CanvasScene, VaultError> {
        let (nodes, edges) = self.inner.canvas_scene(handle)?;
        Ok(CanvasScene {
            nodes: nodes.into_iter().map(Into::into).collect(),
            edges: edges.into_iter().map(Into::into).collect(),
        })
    }
}

#[uniffi::export]
impl VaultSession {
    /// Apply one committed user action (one write, one undo step);
    /// returns the inverse action for the undo stack.
    pub fn canvas_apply(
        &self,
        handle: u64,
        action: CanvasAction,
    ) -> Result<CanvasApplyResult, VaultError> {
        Ok(self.inner.canvas_apply(handle, action.into())?.into())
    }
}

#[uniffi::export]
impl VaultSession {
    /// Open a `.canvas` file: tolerant parse + model derivation +
    /// index refresh; returns the handle every other canvas call takes.
    pub fn open_canvas(&self, path: String) -> Result<CanvasOpenInfo, VaultError> {
        Ok(self.inner.open_canvas(&path)?.into())
    }

    /// Release a canvas handle (idempotent).
    pub fn close_canvas(&self, handle: u64) {
        self.inner.close_canvas(handle);
    }

    /// Depth-first outline rows in reading order (#362).
    pub fn canvas_outline(&self, handle: u64) -> Result<Vec<CanvasOutlineRow>, VaultError> {
        Ok(self
            .inner
            .canvas_outline(handle)?
            .into_iter()
            .map(Into::into)
            .collect())
    }

    /// Flat table rows in reading order (#363).
    pub fn canvas_table_rows(&self, handle: u64) -> Result<Vec<CanvasTableRow>, VaultError> {
        Ok(self
            .inner
            .canvas_table_rows(handle)?
            .into_iter()
            .map(Into::into)
            .collect())
    }

    /// A node's connections with directional phrase data (#364/#518).
    pub fn canvas_neighbors(
        &self,
        handle: u64,
        node_id: String,
    ) -> Result<Vec<CanvasNeighbor>, VaultError> {
        Ok(self
            .inner
            .canvas_neighbors(handle, &node_id)?
            .into_iter()
            .map(Into::into)
            .collect())
    }

    /// The ⌃⌘I readback context for one node (#518).
    pub fn canvas_where_am_i(
        &self,
        handle: u64,
        node_id: String,
    ) -> Result<CanvasWhereAmI, VaultError> {
        Ok(self.inner.canvas_where_am_i(handle, &node_id)?.into())
    }

    /// Non-overlapping grid-aligned position for a new card (#517).
    pub fn canvas_place_new(
        &self,
        handle: u64,
        anchor: Option<String>,
        width: f64,
        height: f64,
        direction_hint: Option<CanvasPlaceDirection>,
        exclude: Vec<String>,
    ) -> Result<CanvasPlacement, VaultError> {
        Ok(self
            .inner
            .canvas_place_new(
                handle,
                anchor,
                width,
                height,
                direction_hint.map(Into::into),
                exclude,
            )?
            .into())
    }

    /// Rigid-set placement: one origin per box, offsets preserved.
    pub fn canvas_place_set(
        &self,
        handle: u64,
        anchor: Option<String>,
        boxes: Vec<CanvasRect>,
        direction_hint: Option<CanvasPlaceDirection>,
        exclude: Vec<String>,
    ) -> Result<CanvasSetPlacement, VaultError> {
        Ok(self
            .inner
            .canvas_place_set(
                handle,
                anchor,
                boxes.into_iter().map(Into::into).collect(),
                direction_hint.map(Into::into),
                exclude,
            )?
            .into())
    }

    /// A text card's markdown content (t2 §#362 interim detail panel);
    /// `None` for non-text cards.
    pub fn canvas_node_text(
        &self,
        handle: u64,
        node_id: String,
    ) -> Result<Option<String>, VaultError> {
        Ok(self.inner.canvas_node_text(handle, &node_id)?)
    }

    /// Node ids overlapping `rect` (cards only) — #521 overlap warnings.
    pub fn canvas_check_overlap(
        &self,
        handle: u64,
        rect: CanvasRect,
        exclude: Vec<String>,
    ) -> Result<Vec<String>, VaultError> {
        Ok(self
            .inner
            .canvas_check_overlap(handle, rect.into(), exclude)?)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn create_folder_exclusive_ffi_wrapper_creates_the_directory() {
        let tmp = tempfile::tempdir().expect("tempdir");
        let session = VaultSession::open_filesystem(tmp.path().to_string_lossy().into_owned())
            .expect("open vault");

        session
            .create_folder_exclusive("imported".into())
            .expect("exclusive folder create");

        assert!(tmp.path().join("imported").is_dir());
    }

    #[test]
    fn parse_frontmatter_properties_uses_authoritative_source_bytes() {
        let properties = parse_frontmatter_properties(
            "title: External\ncount: 2\ntags: [one, two]\n".to_string(),
        );
        assert_eq!(properties.len(), 3);
        assert_eq!(properties[0].key, "title");
        assert_eq!(properties[0].kind, "text");
        assert_eq!(properties[0].value_json, "\"External\"");
        assert_eq!(properties[1].key, "count");
        assert_eq!(properties[1].kind, "number");
        assert_eq!(properties[1].value_json, "2");
        assert_eq!(properties[2].key, "tags");
        assert_eq!(properties[2].kind, "tag_list");
        assert_eq!(properties[2].value_json, "[\"one\",\"two\"]");
    }

    #[test]
    fn a11y_event_identity_is_the_corpus_artifact_event_string() {
        assert_eq!(
            a11y_event_identity(A11yEvent::OpenedAtLine {
                filename: "notes.md".into(),
                line: 40,
            }),
            "OpenedAtLine { filename: \"notes.md\", line: 40 }"
        );
        assert_eq!(
            a11y_event_identity(A11yEvent::NoItemsSelected),
            "NoItemsSelected"
        );
        // Same rendered text, different identity — the census must be able
        // to tell these apart (that is this accessor's whole job).
        let file = a11y_event_identity(A11yEvent::OpenedFile {
            filename: "notes.md".into(),
        });
        let nav = a11y_event_identity(A11yEvent::InternalNavigated {
            kind: "Opened".into(),
            filename: "notes.md".into(),
        });
        assert_ne!(file, nav);
    }

    fn ffi_batch_item(path: &str, is_directory: bool) -> StructuralBatchItem {
        StructuralBatchItem {
            path: path.to_string(),
            is_directory,
        }
    }

    fn core_batch_item(
        path: &str,
        is_directory: bool,
    ) -> core::structural_batch::StructuralBatchItem {
        core::structural_batch::StructuralBatchItem {
            path: path.to_string(),
            is_directory,
        }
    }

    fn core_batch_failure(
        item: Option<core::structural_batch::StructuralBatchItem>,
        stage: core::structural_batch::BatchFailureStage,
        message: &str,
    ) -> core::structural_batch::BatchItemFailure {
        core::structural_batch::BatchItemFailure {
            item,
            stage,
            message: message.to_string(),
        }
    }

    fn rich_core_batch_envelope() -> core::structural_batch::StructuralBatchEnvelope {
        core::structural_batch::StructuralBatchEnvelope {
            planned: vec![
                core_batch_item("planned-z", true),
                core_batch_item("planned-a.md", false),
            ],
            skipped: vec![
                core::structural_batch::BatchSkippedItem {
                    item: core_batch_item("skipped-z.md", false),
                    reason: core::structural_batch::BatchSkipReason::Duplicate,
                    detail: "skip-z".into(),
                },
                core::structural_batch::BatchSkippedItem {
                    item: core_batch_item("skipped-a", true),
                    reason: core::structural_batch::BatchSkipReason::CoveredBySelectedFolder,
                    detail: "skip-a".into(),
                },
            ],
            preflight_failures: vec![
                core_batch_failure(
                    None,
                    core::structural_batch::BatchFailureStage::Preflight,
                    "preflight-global-z",
                ),
                core_batch_failure(
                    Some(core_batch_item("preflight-a.md", false)),
                    core::structural_batch::BatchFailureStage::RecoveryBarrier,
                    "preflight-item-a",
                ),
            ],
        }
    }

    #[test]
    fn batch_move_request_conversion_preserves_utf8_order_and_kind() {
        let request = BatchMoveRequest {
            items: vec![
                ffi_batch_item("β.md", false),
                ffi_batch_item("Empty Folder", true),
                ffi_batch_item("a/猫.md", false),
            ],
            new_parent: "目的地/β".into(),
        };

        let converted: core::structural_batch::BatchMoveRequest = request.into();

        assert_eq!(
            converted.items,
            vec![
                core_batch_item("β.md", false),
                core_batch_item("Empty Folder", true),
                core_batch_item("a/猫.md", false),
            ]
        );
        assert_eq!(converted.new_parent, "目的地/β");
    }

    #[test]
    fn batch_item_conversion_is_bidirectional() {
        let ffi_item = ffi_batch_item("Empty Folder", true);
        let core_item: core::structural_batch::StructuralBatchItem = ffi_item.clone().into();
        assert_eq!(core_item, core_batch_item("Empty Folder", true));

        let round_trip: StructuralBatchItem = core_item.into();
        assert_eq!(round_trip, ffi_item);
    }

    #[test]
    fn batch_enum_conversions_are_exhaustive_including_no_op() {
        use slate_core::structural_batch as c;

        for (source, expected) in [
            (c::BatchSkipReason::Duplicate, BatchSkipReason::Duplicate),
            (
                c::BatchSkipReason::CoveredBySelectedFolder,
                BatchSkipReason::CoveredBySelectedFolder,
            ),
            (
                c::BatchSkipReason::AlreadyInDestination,
                BatchSkipReason::AlreadyInDestination,
            ),
        ] {
            assert_eq!(BatchSkipReason::from(source), expected);
        }

        for (source, expected) in [
            (
                c::BatchFailureStage::Preflight,
                BatchFailureStage::Preflight,
            ),
            (c::BatchFailureStage::Move, BatchFailureStage::Move),
            (c::BatchFailureStage::Index, BatchFailureStage::Index),
            (
                c::BatchFailureStage::LinkRewrite,
                BatchFailureStage::LinkRewrite,
            ),
            (
                c::BatchFailureStage::LinkRewriteRestore,
                BatchFailureStage::LinkRewriteRestore,
            ),
            (c::BatchFailureStage::Journal, BatchFailureStage::Journal),
            (c::BatchFailureStage::Rollback, BatchFailureStage::Rollback),
            (c::BatchFailureStage::Trash, BatchFailureStage::Trash),
            (
                c::BatchFailureStage::Reconciliation,
                BatchFailureStage::Reconciliation,
            ),
            (
                c::BatchFailureStage::RecoveryBarrier,
                BatchFailureStage::RecoveryBarrier,
            ),
        ] {
            assert_eq!(BatchFailureStage::from(source), expected);
        }

        for (source, expected) in [
            (c::BatchMoveState::Rejected, BatchMoveState::Rejected),
            (c::BatchMoveState::NoOp, BatchMoveState::NoOp),
            (c::BatchMoveState::Succeeded, BatchMoveState::Succeeded),
            (c::BatchMoveState::RolledBack, BatchMoveState::RolledBack),
            (
                c::BatchMoveState::RollbackIncomplete,
                BatchMoveState::RollbackIncomplete,
            ),
        ] {
            assert_eq!(BatchMoveState::from(source), expected);
        }

        for (source, expected) in [
            (c::BatchTrashState::Rejected, BatchTrashState::Rejected),
            (c::BatchTrashState::NoOp, BatchTrashState::NoOp),
            (c::BatchTrashState::Succeeded, BatchTrashState::Succeeded),
            (c::BatchTrashState::Partial, BatchTrashState::Partial),
            (c::BatchTrashState::Failed, BatchTrashState::Failed),
        ] {
            assert_eq!(BatchTrashState::from(source), expected);
        }
    }

    #[test]
    fn batch_failure_conversion_preserves_global_none_and_item_some() {
        let global = BatchItemFailure::from(core_batch_failure(
            None,
            core::structural_batch::BatchFailureStage::RecoveryBarrier,
            "global recovery failure",
        ));
        assert_eq!(global.item, None);
        assert_eq!(global.stage, BatchFailureStage::RecoveryBarrier);
        assert_eq!(global.message, "global recovery failure");

        let item = BatchItemFailure::from(core_batch_failure(
            Some(core_batch_item("Empty Folder", true)),
            core::structural_batch::BatchFailureStage::Trash,
            "item failure",
        ));
        assert_eq!(item.item, Some(ffi_batch_item("Empty Folder", true)));
        assert_eq!(item.stage, BatchFailureStage::Trash);
        assert_eq!(item.message, "item failure");
    }

    #[test]
    fn batch_move_report_conversion_preserves_order_state_and_optional_op_id() {
        use slate_core::structural_batch as c;

        for (source_state, expected_state, op_id) in [
            (c::BatchMoveState::Rejected, BatchMoveState::Rejected, None),
            (c::BatchMoveState::NoOp, BatchMoveState::NoOp, None),
            (
                c::BatchMoveState::Succeeded,
                BatchMoveState::Succeeded,
                Some(i64::MAX),
            ),
            (
                c::BatchMoveState::RolledBack,
                BatchMoveState::RolledBack,
                None,
            ),
            (
                c::BatchMoveState::RollbackIncomplete,
                BatchMoveState::RollbackIncomplete,
                Some(i64::MAX),
            ),
        ] {
            let converted = BatchMoveReport::from(c::BatchMoveReport {
                envelope: rich_core_batch_envelope(),
                state: source_state,
                op_id,
                standing: vec![
                    c::BatchPathChange {
                        old_path: "standing-z".into(),
                        new_path: "destination-z".into(),
                        is_directory: true,
                    },
                    c::BatchPathChange {
                        old_path: "standing-a.md".into(),
                        new_path: "destination-a.md".into(),
                        is_directory: false,
                    },
                ],
                rolled_back: vec![
                    c::BatchPathChange {
                        old_path: "rolled-z.md".into(),
                        new_path: "restored-z.md".into(),
                        is_directory: false,
                    },
                    c::BatchPathChange {
                        old_path: "rolled-a".into(),
                        new_path: "restored-a".into(),
                        is_directory: true,
                    },
                ],
                failure: Some(core_batch_failure(
                    None,
                    c::BatchFailureStage::LinkRewrite,
                    "forward-global",
                )),
                rollback_failures: vec![
                    core_batch_failure(
                        Some(core_batch_item("rollback-z", true)),
                        c::BatchFailureStage::Rollback,
                        "rollback-z",
                    ),
                    core_batch_failure(
                        Some(core_batch_item("rollback-a.md", false)),
                        c::BatchFailureStage::LinkRewriteRestore,
                        "rollback-a",
                    ),
                ],
                rewritten: vec![
                    core::structural::RewriteOutcome {
                        path: "rewritten-z.md".into(),
                        hash_before: "before-z".into(),
                        hash_after: "after-z".into(),
                    },
                    core::structural::RewriteOutcome {
                        path: "rewritten-a.md".into(),
                        hash_before: "before-a".into(),
                        hash_after: "after-a".into(),
                    },
                ],
                rewrite_failures: vec![
                    core::structural::RewriteFailure {
                        path: "rewrite-failure-z.md".into(),
                        kind: core::structural::RewriteFailureKind::Other("z-detail".into()),
                    },
                    core::structural::RewriteFailure {
                        path: "rewrite-failure-a.md".into(),
                        kind: core::structural::RewriteFailureKind::Cancelled,
                    },
                ],
                requires_rescan: true,
            });

            assert_eq!(converted.state, expected_state);
            assert_eq!(converted.op_id, op_id);
            assert_eq!(
                converted
                    .envelope
                    .planned
                    .iter()
                    .map(|item| item.path.as_str())
                    .collect::<Vec<_>>(),
                vec!["planned-z", "planned-a.md"]
            );
            assert_eq!(
                converted
                    .envelope
                    .skipped
                    .iter()
                    .map(|skip| (skip.item.path.as_str(), skip.detail.as_str()))
                    .collect::<Vec<_>>(),
                vec![("skipped-z.md", "skip-z"), ("skipped-a", "skip-a")]
            );
            assert_eq!(
                converted
                    .envelope
                    .preflight_failures
                    .iter()
                    .map(|failure| failure.message.as_str())
                    .collect::<Vec<_>>(),
                vec!["preflight-global-z", "preflight-item-a"]
            );
            assert_eq!(
                converted
                    .standing
                    .iter()
                    .map(|change| change.old_path.as_str())
                    .collect::<Vec<_>>(),
                vec!["standing-z", "standing-a.md"]
            );
            assert_eq!(
                converted
                    .rolled_back
                    .iter()
                    .map(|change| change.old_path.as_str())
                    .collect::<Vec<_>>(),
                vec!["rolled-z.md", "rolled-a"]
            );
            assert_eq!(converted.failure.as_ref().unwrap().item, None);
            assert_eq!(
                converted
                    .rollback_failures
                    .iter()
                    .map(|failure| failure.message.as_str())
                    .collect::<Vec<_>>(),
                vec!["rollback-z", "rollback-a"]
            );
            assert_eq!(
                converted
                    .rewritten
                    .iter()
                    .map(|outcome| outcome.path.as_str())
                    .collect::<Vec<_>>(),
                vec!["rewritten-z.md", "rewritten-a.md"]
            );
            assert_eq!(
                converted
                    .rewrite_failures
                    .iter()
                    .map(|failure| failure.path.as_str())
                    .collect::<Vec<_>>(),
                vec!["rewrite-failure-z.md", "rewrite-failure-a.md"]
            );
            assert!(converted.requires_rescan);
        }
    }

    #[test]
    fn batch_trash_report_conversion_keeps_all_outcome_buckets_distinct() {
        use slate_core::structural_batch as c;

        for (source_state, expected_state, op_id) in [
            (
                c::BatchTrashState::Rejected,
                BatchTrashState::Rejected,
                None,
            ),
            (c::BatchTrashState::NoOp, BatchTrashState::NoOp, None),
            (
                c::BatchTrashState::Succeeded,
                BatchTrashState::Succeeded,
                Some(i64::MAX),
            ),
            (
                c::BatchTrashState::Partial,
                BatchTrashState::Partial,
                Some(i64::MAX),
            ),
            (c::BatchTrashState::Failed, BatchTrashState::Failed, None),
        ] {
            let converted = BatchTrashReport::from(c::BatchTrashReport {
                envelope: rich_core_batch_envelope(),
                state: source_state,
                op_id,
                trashed: vec![
                    core_batch_item("trashed-z", true),
                    core_batch_item("trashed-a.md", false),
                ],
                untrashed: vec![
                    c::BatchTrashRemainder {
                        item: core_batch_item("untrashed-z.md", false),
                        failure: core_batch_failure(
                            Some(core_batch_item("untrashed-z.md", false)),
                            c::BatchFailureStage::Trash,
                            "untrashed-z-failure",
                        ),
                    },
                    c::BatchTrashRemainder {
                        item: core_batch_item("untrashed-a", true),
                        failure: core_batch_failure(
                            None,
                            c::BatchFailureStage::Reconciliation,
                            "untrashed-a-global-failure",
                        ),
                    },
                ],
                unknown: vec![c::BatchTrashRemainder {
                    item: core_batch_item("unknown.md", false),
                    failure: core_batch_failure(
                        Some(core_batch_item("unknown.md", false)),
                        c::BatchFailureStage::Reconciliation,
                        "physical Trash verification failed",
                    ),
                }],
                bookkeeping_failures: vec![
                    core_batch_failure(None, c::BatchFailureStage::Journal, "bookkeeping-z"),
                    core_batch_failure(
                        Some(core_batch_item("bookkeeping-a.md", false)),
                        c::BatchFailureStage::Index,
                        "bookkeeping-a",
                    ),
                ],
                requires_rescan: true,
            });

            assert_eq!(converted.state, expected_state);
            assert_eq!(converted.op_id, op_id);
            assert_eq!(
                converted
                    .envelope
                    .planned
                    .iter()
                    .map(|item| item.path.as_str())
                    .collect::<Vec<_>>(),
                vec!["planned-z", "planned-a.md"]
            );
            assert_eq!(
                converted
                    .trashed
                    .iter()
                    .map(|item| item.path.as_str())
                    .collect::<Vec<_>>(),
                vec!["trashed-z", "trashed-a.md"]
            );
            assert_eq!(
                converted
                    .untrashed
                    .iter()
                    .map(|remainder| {
                        (
                            remainder.item.path.as_str(),
                            remainder.failure.message.as_str(),
                            remainder
                                .failure
                                .item
                                .as_ref()
                                .map(|item| item.path.as_str()),
                        )
                    })
                    .collect::<Vec<_>>(),
                vec![
                    (
                        "untrashed-z.md",
                        "untrashed-z-failure",
                        Some("untrashed-z.md")
                    ),
                    ("untrashed-a", "untrashed-a-global-failure", None),
                ]
            );
            assert_eq!(
                converted
                    .unknown
                    .iter()
                    .map(|remainder| {
                        (
                            remainder.item.path.as_str(),
                            remainder.failure.message.as_str(),
                            remainder.failure.stage,
                        )
                    })
                    .collect::<Vec<_>>(),
                vec![(
                    "unknown.md",
                    "physical Trash verification failed",
                    BatchFailureStage::Reconciliation,
                )]
            );
            assert_eq!(
                converted
                    .bookkeeping_failures
                    .iter()
                    .map(|failure| failure.message.as_str())
                    .collect::<Vec<_>>(),
                vec!["bookkeeping-z", "bookkeeping-a"]
            );
            assert!(converted.requires_rescan);
        }
    }

    #[test]
    fn batch_ffi_wrapper_move_and_dedicated_undo_return_batch_reports() {
        let tmp = tempfile::tempdir().expect("tempdir");
        std::fs::create_dir_all(tmp.path().join("left")).unwrap();
        std::fs::create_dir_all(tmp.path().join("right/Empty Folder")).unwrap();
        std::fs::create_dir_all(tmp.path().join("destination")).unwrap();
        std::fs::write(tmp.path().join("left/a.md"), "# A\n").unwrap();
        let session = VaultSession::open_filesystem(tmp.path().to_string_lossy().into_owned())
            .expect("open vault");
        session.scan_initial(CancelToken::new()).unwrap();

        let moved = session
            .batch_move(BatchMoveRequest {
                items: vec![
                    ffi_batch_item("right/Empty Folder", true),
                    ffi_batch_item("left/a.md", false),
                ],
                new_parent: "destination".into(),
            })
            .expect("batch rejection/recovery states are report data");

        assert_eq!(moved.state, BatchMoveState::Succeeded);
        let op_id = moved.op_id.expect("successful batch move has a journal id");
        assert_eq!(
            moved
                .standing
                .iter()
                .map(|change| {
                    (
                        change.old_path.as_str(),
                        change.new_path.as_str(),
                        change.is_directory,
                    )
                })
                .collect::<Vec<_>>(),
            vec![
                ("left/a.md", "destination/a.md", false),
                ("right/Empty Folder", "destination/Empty Folder", true),
            ]
        );
        assert!(tmp.path().join("destination/a.md").is_file());
        assert!(tmp.path().join("destination/Empty Folder").is_dir());

        let undone = session
            .undo_batch_move(op_id)
            .expect("dedicated batch undo returns a batch report");
        assert_eq!(undone.state, BatchMoveState::Succeeded);
        assert!(undone.op_id.is_some());
        assert_eq!(
            undone
                .standing
                .iter()
                .map(|change| (change.old_path.as_str(), change.new_path.as_str()))
                .collect::<Vec<_>>(),
            vec![
                ("destination/Empty Folder", "right/Empty Folder"),
                ("destination/a.md", "left/a.md"),
            ]
        );
        assert!(tmp.path().join("left/a.md").is_file());
        assert!(tmp.path().join("right/Empty Folder").is_dir());
        assert!(!tmp.path().join("destination/a.md").exists());
        assert!(!tmp.path().join("destination/Empty Folder").exists());
    }

    #[test]
    fn batch_ffi_rejection_is_report_data() {
        let tmp = tempfile::tempdir().expect("tempdir");
        let session = VaultSession::open_filesystem(tmp.path().to_string_lossy().into_owned())
            .expect("open vault");

        let report = session
            .batch_move(BatchMoveRequest {
                items: Vec::new(),
                new_parent: String::new(),
            })
            .expect("an expected request rejection must not be a thrown VaultError");

        assert_eq!(report.state, BatchMoveState::Rejected);
        assert_eq!(report.op_id, None);
        assert_eq!(report.envelope.preflight_failures.len(), 1);
        assert_eq!(report.envelope.preflight_failures[0].item, None);
    }

    #[test]
    fn file_summary_conversion_preserves_enrichment() {
        let converted = FileSummary::from(core::FileSummary {
            path: "notes/a.md".into(),
            name: "a.md".into(),
            mtime_ms: 17,
            size_bytes: 23,
            is_markdown: true,
            display_name: Some("Authored".into()),
            created_date: Some("2024-02-29".into()),
            created_ms: Some(1_700_000_000_000),
            word_count: Some(42),
            preview: Some("Preview".into()),
            task_total: 3,
            task_open: 2,
        });

        assert_eq!(converted.path, "notes/a.md");
        assert_eq!(converted.name, "a.md");
        assert_eq!(converted.mtime_ms, 17);
        assert_eq!(converted.size_bytes, 23);
        assert!(converted.is_markdown);
        assert_eq!(converted.display_name.as_deref(), Some("Authored"));
        assert_eq!(converted.created_date.as_deref(), Some("2024-02-29"));
        assert_eq!(converted.created_ms, Some(1_700_000_000_000));
        assert_eq!(converted.word_count, Some(42));
        assert_eq!(converted.preview.as_deref(), Some("Preview"));
        assert_eq!((converted.task_total, converted.task_open), (3, 2));
    }

    #[test]
    fn file_summary_lookup_drives_over_ffi_wrapper() {
        let tmp = tempfile::tempdir().expect("tempdir");
        std::fs::write(
            tmp.path().join("target.md"),
            "---\ntitle: Target\n---\nFresh preview.\n- [ ] open\n",
        )
        .unwrap();
        let session = VaultSession::open_filesystem(tmp.path().to_string_lossy().into_owned())
            .expect("open vault");
        session.scan_initial(CancelToken::new()).unwrap();

        let summary = session
            .get_file_summary("target.md".into())
            .unwrap()
            .unwrap();
        assert_eq!(summary.path, "target.md");
        assert_eq!(summary.display_name.as_deref(), Some("Target"));
        assert!(
            summary
                .preview
                .as_deref()
                .unwrap()
                .contains("Fresh preview")
        );
        assert_eq!((summary.task_total, summary.task_open), (1, 1));
    }

    // ---------------------------------------------------------------
    // text_* conversions (#378) — rope-backed offset/line FFI smoke.
    // ---------------------------------------------------------------

    #[test]
    fn text_conversions_handle_multibyte_and_lines() {
        // "a😀\n中b": a(1B/1u16) 😀(4B/2u16) \n(1B/1u16) 中(3B/1u16) b.
        // bytes a=0 😀=1..5 \n=5 中=6..9 b=9 ; len 10 bytes, 6 utf16, 2 lines.
        let text = "a😀\n中b".to_string();
        // byte 6 (中) → utf16 4 (a=0, 😀=1..3, \n=3, 中=4).
        assert_eq!(text_byte_to_utf16(text.clone(), 6), 4);
        // utf16 4 sits on line 2 (after the \n).
        assert_eq!(text_utf16_to_line(text.clone(), 4), 2);
        // line 2's first char (中) is at utf16 4; line 1 at utf16 0.
        assert_eq!(text_line_to_utf16(text.clone(), 2), 4);
        assert_eq!(text_line_to_utf16(text.clone(), 1), 0);
        // A line past EOF parks at the buffer end (utf16 len = 6).
        assert_eq!(text_line_to_utf16(text, 99), 6);
    }

    #[test]
    fn text_utf16_to_byte_inverts_byte_to_utf16_and_clamps() {
        // Same "a😀\n中b" fixture: 10 bytes, 6 utf16 units.
        let text = "a😀\n中b".to_string();
        // utf16 4 (中) → byte 6; the inverse of text_byte_to_utf16(6)==4.
        assert_eq!(text_utf16_to_byte(text.clone(), 4), 6);
        assert_eq!(text_utf16_to_byte(text.clone(), 0), 0);
        // Round-trips at every code-unit boundary.
        for byte in [0u32, 1, 5, 6, 9, 10] {
            let u16 = text_byte_to_utf16(text.clone(), byte);
            assert_eq!(text_utf16_to_byte(text.clone(), u16), byte);
        }
        // Past-the-end clamps to the byte length (10).
        assert_eq!(text_utf16_to_byte(text, 99), 10);
    }

    // ---------------------------------------------------------------
    // editor_highlight_spans_in_range (#379) — ranged-highlight FFI:
    // a window that maps to a doc-space sub-range, and the whole-doc
    // fallback sentinel. The exhaustive correctness proptest lives in
    // slate-core; here we only smoke the FFI plumbing + sentinel.
    // ---------------------------------------------------------------

    #[test]
    fn editor_highlight_spans_in_range_windows_a_middle_paragraph() {
        let text = "alpha para\n\nbeta has **bold**\n\ngamma para\n".to_string();
        let dirty = text.find("bold").unwrap() as u32;
        let ranged = editor_highlight_spans_in_range(text.clone(), dirty, dirty);

        // A blank-bounded middle paragraph: the window neither starts at
        // byte 0 nor runs to EOF, so this is not the fallback sentinel.
        assert!(ranged.applied_start > 0);
        assert!((ranged.applied_end as usize) < text.len());

        // The ranged spans are exactly the whole-document spans that fall
        // inside the applied window (offsets already in document space).
        let whole = editor_highlight_spans(text);
        let expected: Vec<_> = whole
            .into_iter()
            .filter(|s| s.start_byte >= ranged.applied_start && s.end_byte <= ranged.applied_end)
            .collect();
        assert_eq!(ranged.spans, expected);
        assert!(
            ranged
                .spans
                .iter()
                .any(|s| s.kind == EditorSpanKind::Strong),
            "the windowed **bold** must carry a Strong span"
        );
    }

    #[test]
    fn editor_highlight_spans_in_range_falls_back_inside_frontmatter() {
        let text = "---\ntitle: x\n---\n\nbody text\n".to_string();
        let dirty = text.find("title").unwrap() as u32;
        let ranged = editor_highlight_spans_in_range(text.clone(), dirty, dirty);

        // Editing inside the top-of-document frontmatter can't be windowed
        // (the composed extractors re-derive it from byte 0), so the core
        // signals fallback: applied == 0..len with the whole-doc spans.
        assert_eq!(ranged.applied_start, 0);
        assert_eq!(ranged.applied_end as usize, text.len());
        assert_eq!(ranged.spans, editor_highlight_spans(text));
    }

    // ---------------------------------------------------------------
    // DocumentBuffer (#404) — stateful buffer FFI smoke: a fed delta
    // updates the length + windows, the stateful highlight matches the
    // stateless free function, and reset re-syncs to a fresh buffer.
    // ---------------------------------------------------------------

    #[test]
    fn document_buffer_apply_edit_updates_length_and_windows() {
        let initial = "alpha para\n\nbeta para\n\ngamma para\n";
        let buf = DocumentBuffer::new(initial.to_string());
        assert_eq!(buf.len_utf16(), initial.encode_utf16().count() as u32);

        // Insert into the middle paragraph (ASCII ⇒ UTF-16 == byte offsets).
        let at = "alpha para\n\nbeta".encode_utf16().count() as u32;
        buf.apply_edit(at, 0, " EDIT".to_string());
        let expected = "alpha para\n\nbeta EDIT para\n\ngamma para\n";
        assert_eq!(buf.len_utf16(), expected.encode_utf16().count() as u32);

        // A blank-bounded middle-paragraph edit windows — not the fallback
        // sentinel (which always reports applied_start == 0).
        let ranged = buf.highlight_in_range(at, at + 5);
        assert!(ranged.applied_start > 0);
        assert!((ranged.applied_end as usize) < expected.len());
    }

    #[test]
    fn document_buffer_highlight_matches_the_stateless_path() {
        // ASCII ⇒ UTF-16 offsets equal byte offsets, so the same dirty
        // position feeds the stateful buffer (UTF-16 in) and the stateless
        // free function (bytes in); the results must be identical.
        let text = "alpha para\n\nbeta has **bold**\n\ngamma para\n".to_string();
        let dirty = text.find("bold").unwrap() as u32;
        let buf = DocumentBuffer::new(text.clone());
        assert_eq!(
            buf.highlight_in_range(dirty, dirty),
            editor_highlight_spans_in_range(text, dirty, dirty)
        );
    }

    #[test]
    fn document_buffer_reset_matches_a_fresh_buffer() {
        let buf = DocumentBuffer::new("stale\n\ncontents\n".to_string());
        buf.apply_edit(0, 5, "x".to_string()); // mutate so reset must override
        let fresh_text = "# New\n\nReset body with **bold**.\n";
        buf.reset(fresh_text.to_string());
        let fresh = DocumentBuffer::new(fresh_text.to_string());
        assert_eq!(buf.len_utf16(), fresh.len_utf16());
        let n = fresh_text.encode_utf16().count() as u32;
        assert_eq!(buf.highlight_in_range(0, n), fresh.highlight_in_range(0, n));
    }

    // ---------------------------------------------------------------
    // truncate_action_message — trust-boundary truncation for the
    // foreign-controlled CommandError::ActionFailed.message field.
    // ---------------------------------------------------------------

    #[test]
    fn truncate_action_message_passes_short_messages_through() {
        let msg = "ordinary error message".to_string();
        let original = msg.clone();
        assert_eq!(truncate_action_message(msg), original);
    }

    #[test]
    fn truncate_action_message_truncates_at_cap_with_marker() {
        let big = "a".repeat(MAX_ACTION_ERROR_MSG_LEN * 4);
        let out = truncate_action_message(big);
        // The truncated body is <= the cap, plus the marker suffix
        // which is allowed to push the total slightly past.
        assert!(out.starts_with(&"a".repeat(MAX_ACTION_ERROR_MSG_LEN)));
        assert!(out.ends_with("(truncated)"));
        // Hard upper bound — the marker is ~14 ASCII bytes; anything
        // wildly larger means truncation regressed.
        assert!(out.len() < MAX_ACTION_ERROR_MSG_LEN + 32);
    }

    #[test]
    fn truncate_action_message_respects_utf8_boundaries() {
        // Build a string that would split a 4-byte codepoint right
        // at MAX_ACTION_ERROR_MSG_LEN if the truncation were
        // byte-naïve.
        let mut s = "x".repeat(MAX_ACTION_ERROR_MSG_LEN - 2);
        // 4-byte codepoint (U+1F389 PARTY POPPER) straddling the cap.
        s.push('🎉');
        s.push_str(&"y".repeat(64));
        let out = truncate_action_message(s);
        // Result must be valid UTF-8 (Rust String invariant — if
        // truncate had split the codepoint we'd have panicked on
        // the String::truncate call, but the assertion is the
        // contract).
        assert!(out.is_char_boundary(out.find('…').unwrap_or(out.len())));
        assert!(out.ends_with("(truncated)"));
    }

    #[test]
    fn foreign_action_adapter_truncates_action_failed_message() {
        struct HostileAction;
        impl CommandAction for HostileAction {
            fn invoke(&self) -> Result<(), CommandError> {
                Err(CommandError::ActionFailed {
                    message: "x".repeat(MAX_ACTION_ERROR_MSG_LEN * 8),
                })
            }
        }

        let reg = CommandRegistry::new();
        let cmd = Command {
            id: "test.hostile".into(),
            label: "hostile".into(),
            accessibility_hint: None,
            hotkey_hint: None,
            section: CommandSection::Plugins,
        };
        let replaced = reg.register(cmd, Arc::new(HostileAction));
        assert!(!replaced);

        let err = reg.invoke_by_id("test.hostile".into()).unwrap_err();
        let CommandError::ActionFailed { message } = err else {
            panic!("expected ActionFailed");
        };
        assert!(message.len() < MAX_ACTION_ERROR_MSG_LEN + 32);
        assert!(message.ends_with("(truncated)"));
    }

    #[test]
    fn slate_query_fence_classifier_crosses_ffi_boundary() {
        let classified =
            classify_slate_query_fence(r#"{query: "Saved Notes", view: 'Main'}"#.to_string())
                .unwrap();
        assert_eq!(classified.query.as_deref(), Some("Saved Notes"));
        assert_eq!(classified.view.as_deref(), Some("Main"));

        let error = classify_slate_query_fence("query: [not, scalar]".to_string()).unwrap_err();
        assert!(matches!(error, VaultError::InvalidQuery { .. }));
    }

    #[test]
    fn bases_api_drives_over_the_ffi_wrapper() {
        let tmp = tempfile::tempdir().unwrap();
        std::fs::create_dir(tmp.path().join("Queries")).unwrap();
        std::fs::create_dir(tmp.path().join("Notes")).unwrap();
        std::fs::write(
            tmp.path().join("Queries/Reading.base"),
            r#"views:
  - type: table
    name: Reading
    filters: "file.inFolder(\"Notes\")"
    order:
      - file.name
      - status
"#,
        )
        .unwrap();
        std::fs::write(
            tmp.path().join("Notes/Alpha.md"),
            "---\nstatus: active\n---\n# Alpha\n",
        )
        .unwrap();
        std::fs::write(
            tmp.path().join("Notes/Beta.md"),
            "---\nstatus: done\n---\n# Beta\n",
        )
        .unwrap();

        let session = VaultSession::open_filesystem(tmp.path().to_string_lossy().into_owned())
            .expect("open vault");
        session.scan_initial(CancelToken::new()).unwrap();

        let summaries = session.bases_list().unwrap();
        assert_eq!(summaries[0].path, "Queries/Reading.base");

        let handle = session.open_base("Queries/Reading.base".into()).unwrap();
        let views = session.base_views(handle).unwrap();
        assert_eq!(views[0].status, BaseViewStatus::Executable);

        let result = session
            .base_execute(handle, 0, None, Some("done".into()), CancelToken::new())
            .unwrap();
        assert_eq!(result.total_count, 1);
        assert_eq!(result.unfiltered_shown_count, 2);
        assert_eq!(result.rows[0].values[0].display, "Beta.md");
        assert!(
            !result.rows[0].values[0].sort_key.is_empty(),
            "the Core sort key must cross the UniFFI record mirror"
        );

        let csv = session
            .base_export(handle, 0, ExportFormat::Csv, Some("done".into()))
            .unwrap();
        assert_eq!(csv, "file.name,status\r\nBeta.md,done\r\n");

        session
            .base_set_transient_sort(handle, 0, Some("status".into()), false)
            .unwrap();
        let sorted = session
            .base_execute(handle, 0, None, None, CancelToken::new())
            .unwrap();
        assert_eq!(
            sorted
                .rows
                .iter()
                .map(|row| row.file_path.as_str())
                .collect::<Vec<_>>(),
            ["Notes/Beta.md", "Notes/Alpha.md"]
        );
        session
            .base_set_transient_sort(handle, 0, None, false)
            .unwrap();

        let (base, warnings) = core::bases::parse_base(
            r#"views:
  - type: table
    name: Reading
    filters: "file.inFolder(\"Notes\")"
    order:
      - file.name
      - status
"#,
        );
        assert!(warnings.is_empty(), "{warnings:?}");
        let query_json = serde_json::to_string(&core::bases::view_query(&base, 0)).unwrap();
        let saved_id = session
            .save_query(
                "Saved reading".into(),
                Some("From FFI".into()),
                query_json,
                SavedQuerySourceSyntax::Builder,
            )
            .unwrap();
        let saved = session.get_saved_query(saved_id.clone()).unwrap();
        assert_eq!(saved.name, "Saved reading");
        assert_eq!(saved.description.as_deref(), Some("From FFI"));
        assert!(saved.warning.is_none());

        let saved_handle = session.open_saved_query(saved_id.clone()).unwrap();
        assert_eq!(
            session
                .base_execute(
                    saved_handle,
                    0,
                    None,
                    Some("active".into()),
                    CancelToken::new()
                )
                .unwrap()
                .rows[0]
                .file_path,
            "Notes/Alpha.md"
        );

        session
            .export_saved_query_as_base(saved_id.clone(), "Queries/Saved.base".into())
            .unwrap();
        assert!(
            session
                .bases_list()
                .unwrap()
                .iter()
                .any(|summary| summary.path == "Queries/Saved.base")
        );

        let dashboard_id = session
            .save_dashboard(
                "Reading dashboard".into(),
                vec![DashboardSection {
                    saved_query_id: saved_id.clone(),
                    heading_override: Some("Active".into()),
                    view_override: None,
                }],
            )
            .unwrap();
        let dashboard = session.get_dashboard(dashboard_id).unwrap();
        assert_eq!(
            dashboard.sections[0].saved_query_name.as_deref(),
            Some("Saved reading")
        );
        assert!(!dashboard.sections[0].missing);

        session
            .update_dashboard(
                dashboard.id.clone(),
                "Updated dashboard".into(),
                vec![DashboardSection {
                    saved_query_id: saved_id.clone(),
                    heading_override: Some("Updated heading".into()),
                    view_override: Some("Reading".into()),
                }],
            )
            .unwrap();
        let updated_dashboard = session.get_dashboard(dashboard.id).unwrap();
        assert_eq!(updated_dashboard.name, "Updated dashboard");
        assert_eq!(
            updated_dashboard.sections[0].heading_override.as_deref(),
            Some("Updated heading")
        );
        assert_eq!(
            updated_dashboard.sections[0].view_override.as_deref(),
            Some("Reading")
        );

        session
            .base_apply_edit(
                handle,
                BaseEdit::RenameView {
                    view: 0,
                    name: "Renamed".into(),
                },
            )
            .unwrap();
        assert_eq!(session.base_views(handle).unwrap()[0].name, "Renamed");
        session
            .base_apply_edits(
                handle,
                vec![
                    BaseEdit::RenameView {
                        view: 0,
                        name: "Batch draft".into(),
                    },
                    BaseEdit::RenameView {
                        view: 0,
                        name: "Batched".into(),
                    },
                ],
            )
            .unwrap();
        assert_eq!(session.base_views(handle).unwrap()[0].name, "Batched");
        session
            .base_apply_edit(
                handle,
                BaseEdit::SetSlateSort {
                    view: 0,
                    yaml: Some("- expr: status\n  direction: desc".into()),
                },
            )
            .unwrap();
        let saved_base = std::fs::read_to_string(tmp.path().join("Queries/Reading.base")).unwrap();
        assert!(saved_base.contains("    slate:\n      sort:\n        - expr: status"));

        let converted = session
            .dql_as_base("TABLE WITHOUT ID file.name AS \"Name\"\nFROM \"Notes\"\n".into())
            .unwrap();
        let inline = session.open_base_inline(converted, None).unwrap();
        assert_eq!(
            session
                .base_execute(inline, 0, None, None, CancelToken::new())
                .unwrap()
                .columns[0]
                .label,
            "Name"
        );
    }

    #[test]
    fn command_section_round_trips_through_core() {
        for sec in [
            CommandSection::File,
            CommandSection::Navigation,
            CommandSection::View,
            CommandSection::Vault,
            CommandSection::Editor,
            CommandSection::Tasks,
            CommandSection::Settings,
            CommandSection::Plugins,
            CommandSection::Canvas,
            CommandSection::Bases,
            CommandSection::Graph,
            CommandSection::Sidebar,
        ] {
            let core: core::CommandSection = sec.into();
            let back: CommandSection = core.into();
            assert_eq!(sec, back);
        }
    }

    #[test]
    fn save_query_as_base_preserves_create_only_errors_through_ffi() {
        let tmp = tempfile::tempdir().expect("tempdir");
        std::fs::create_dir_all(tmp.path().join("Queries")).unwrap();
        std::fs::write(
            tmp.path().join("Queries/Source.base"),
            b"views:\n  - type: table\n    name: Source\n    order:\n      - file.name\n",
        )
        .unwrap();
        std::fs::write(
            tmp.path().join("Queries/Occupied.base"),
            b"external occupant\n",
        )
        .unwrap();
        let session = VaultSession::open_filesystem(tmp.path().to_string_lossy().into_owned())
            .expect("open vault");
        session.scan_initial(CancelToken::new()).unwrap();
        let handle = session.open_base("Queries/Source.base".into()).unwrap();
        let query_json = session.base_view_query_json(handle, 0).unwrap();

        assert!(matches!(
            session.save_query_as_base(query_json.clone(), "Queries/Wrong.md".into()),
            Err(VaultError::InvalidArgument { .. })
        ));
        assert!(matches!(
            session.save_query_as_base(query_json, "Queries/Occupied.base".into()),
            Err(VaultError::DestinationExists { .. })
        ));
        assert_eq!(
            std::fs::read(tmp.path().join("Queries/Occupied.base")).unwrap(),
            b"external occupant\n"
        );
    }

    /// The core `CommandSection` discriminants are a wire contract (the
    /// palette sorts by them). Graph is 11, and 9 stays reserved for
    /// Excalidraw (Milestone XD) — never reused (P1-3 #556).
    #[test]
    fn command_section_discriminants_are_pinned() {
        assert_eq!(core::CommandSection::Canvas as u8, 8);
        assert_eq!(core::CommandSection::Bases as u8, 10);
        assert_eq!(core::CommandSection::Graph as u8, 11);
        assert_eq!(core::CommandSection::Sidebar as u8, 12);
        // 9 is the Excalidraw reservation: no variant claims it.
        for sec in [
            core::CommandSection::Canvas,
            core::CommandSection::Bases,
            core::CommandSection::Graph,
            core::CommandSection::Sidebar,
        ] {
            assert_ne!(sec as u8, 9, "discriminant 9 is reserved for Excalidraw");
        }
    }

    #[test]
    fn wikilink_for_path_round_trips_through_ffi() {
        let tmp = tempfile::tempdir().expect("tempdir");
        std::fs::create_dir_all(tmp.path().join("Notes")).unwrap();
        std::fs::write(tmp.path().join("Notes/Target.md"), b"# Target").unwrap();
        let session = VaultSession::open_filesystem(tmp.path().to_string_lossy().into_owned())
            .expect("open vault");
        session.scan_initial(CancelToken::new()).unwrap();

        assert_eq!(
            session.wikilink_for_path("Notes/Target.md".into()).unwrap(),
            Some("[[Target]]".into())
        );
    }

    #[test]
    fn registry_register_returns_replaced_flag() {
        struct NoOp;
        impl CommandAction for NoOp {
            fn invoke(&self) -> Result<(), CommandError> {
                Ok(())
            }
        }
        let reg = CommandRegistry::new();
        let cmd = Command {
            id: "test.dup".into(),
            label: "dup".into(),
            accessibility_hint: None,
            hotkey_hint: None,
            section: CommandSection::Plugins,
        };
        let first = reg.register(cmd.clone(), Arc::new(NoOp));
        let second = reg.register(cmd, Arc::new(NoOp));
        assert!(!first, "first registration is not a replacement");
        assert!(second, "second registration must signal replacement");
    }

    #[test]
    fn palette_sections_ranks_and_blends_through_ffi_shapes() {
        let commands = vec![
            Command {
                id: "test.save".into(),
                label: "Save".into(),
                accessibility_hint: None,
                hotkey_hint: None,
                section: CommandSection::File,
            },
            Command {
                id: "test.scatter".into(),
                label: "Slate Add Various Everythings".into(),
                accessibility_hint: None,
                hotkey_hint: None,
                section: CommandSection::File,
            },
        ];

        // Ranked: prefix match first, label spans carried for bolding.
        let ranked = palette_sections(commands.clone(), "save".into(), vec![], vec![]);
        assert_eq!(ranked.len(), 1);
        assert_eq!(ranked[0].kind, Some(CommandSection::File));
        assert_eq!(ranked[0].rows[0].command.id, "test.save");
        assert_eq!(
            ranked[0].rows[0].label_match_spans,
            vec![MatchSpan {
                start_byte: 0,
                end_byte: 4
            }]
        );

        // Empty query: Recent section synthesized, shown ids excluded
        // from their native section.
        let recents = palette_recents_add(vec![], "test.save".into());
        let sections = palette_sections(commands, String::new(), recents, vec![]);
        assert_eq!(sections[0].title, "Recent");
        assert_eq!(sections[0].kind, None);
        assert_eq!(sections[0].rows[0].command.id, "test.save");
        assert_eq!(sections[1].rows[0].command.id, "test.scatter");

        // Recents byte format round-trips through the FFI mirrors.
        let bytes = palette_recents_encode(vec!["a".into(), "b".into()]);
        assert_eq!(palette_recents_decode(bytes), vec!["a", "b"]);
        assert!(palette_recents_remove(vec!["a".into(), "b".into()], "a".into()) == vec!["b"]);
    }

    #[test]
    fn switcher_rank_blends_recency_through_ffi_shapes() {
        let files = vec![
            SwitcherFile {
                path: "alpha/note.md".into(),
                name: "note.md".into(),
            },
            SwitcherFile {
                path: "beta/note.md".into(),
                name: "note.md".into(),
            },
            SwitcherFile {
                path: "diagram.png".into(),
                name: "diagram.png".into(),
            },
        ];

        // Empty query: still-present recents first, rest in incoming order.
        let rows = switcher_rank(files.clone(), String::new(), vec!["beta/note.md".into()]);
        assert_eq!(rows[0].path, "beta/note.md");
        assert_eq!(rows.len(), 3);
        assert_eq!(rows[0].display_name, "note");
        assert_eq!(rows[2].display_name, "diagram.png");

        // Ranked: identical scores tie-break by recency, spans carried
        // for the display-name match.
        let rows = switcher_rank(files, "note".into(), vec!["beta/note.md".into()]);
        assert_eq!(rows.len(), 2);
        assert_eq!(rows[0].path, "beta/note.md");
        assert_eq!(
            rows[0].display_name_match_spans,
            vec![MatchSpan {
                start_byte: 0,
                end_byte: 4
            }]
        );
        assert!(rows[0].score > 0);
        assert_eq!(
            switcher_display_name("2026.01.notes.md".into()),
            "2026.01.notes"
        );
    }

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
                // Message text is OS strerror: unix says "No such file",
                // Windows says "The system cannot find the file/path".
                assert!(
                    message.contains("No such file")
                        || message.contains("not found")
                        || message.contains("cannot find"),
                    "unexpected io message: {message}"
                );
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

    #[test]
    fn list_property_keys_carries_sorted_kinds_through_ffi_conversion() {
        let tmp = tempfile::tempdir().expect("tempdir");
        std::fs::write(tmp.path().join("a.md"), "---\nmixed: 42\n---\n").unwrap();
        std::fs::write(tmp.path().join("b.md"), "---\nmixed: true\n---\n").unwrap();
        std::fs::write(tmp.path().join("c.md"), "---\nmixed: 7\n---\n").unwrap();
        let session = VaultSession::open_filesystem(tmp.path().to_string_lossy().into_owned())
            .expect("open vault");
        session.scan_initial(CancelToken::new()).unwrap();

        let keys = session.list_property_keys().unwrap();
        let mixed = keys.iter().find(|summary| summary.key == "mixed").unwrap();
        assert_eq!(mixed.file_count, 3);
        assert_eq!(mixed.value_kinds, vec!["boolean", "number"]);
    }

    #[test]
    fn save_text_returns_save_report_through_ffi() {
        let tmp = tempfile::tempdir().expect("tempdir");
        let session = VaultSession::open_filesystem(tmp.path().to_string_lossy().into_owned())
            .expect("open vault");

        let report = session
            .save_text("note.md".into(), "# Hi\n".into(), None)
            .expect("save_text should succeed");
        assert_eq!(report.new_size_bytes, "# Hi\n".len() as u64);
        assert!(!report.new_content_hash.is_empty());
        assert!(report.new_mtime_ms > 0);
    }

    #[test]
    fn write_conflict_round_trips_through_ffi() {
        // Mac UI calls save_text with an expected hash; another writer
        // changed the file underneath; FFI must surface the typed
        // WriteConflict so the host can drive a resolution UI.
        let tmp = tempfile::tempdir().expect("tempdir");
        let session = VaultSession::open_filesystem(tmp.path().to_string_lossy().into_owned())
            .expect("open vault");
        session
            .save_text("note.md".into(), "v1".into(), None)
            .unwrap();
        // External write directly to disk, behind the session's back.
        std::fs::write(tmp.path().join("note.md"), b"external").unwrap();

        let stale = slate_core::content_hash(b"v1");
        match session.save_text("note.md".into(), "v2".into(), Some(stale.clone())) {
            Err(VaultError::WriteConflict {
                current_content_hash,
                expected_content_hash,
                current_mtime_ms,
            }) => {
                assert_eq!(current_content_hash, slate_core::content_hash(b"external"));
                assert_eq!(expected_content_hash, stale);
                assert!(current_mtime_ms > 0);
            }
            other => panic!("expected WriteConflict, got {other:?}"),
        }
    }

    #[test]
    fn tasks_for_file_round_trips_through_ffi() {
        let tmp = tempfile::tempdir().expect("tempdir");
        std::fs::write(
            tmp.path().join("n.md"),
            "- [ ] open\n- [x] done 📅 2026-06-01\n",
        )
        .unwrap();
        let session = VaultSession::open_filesystem(tmp.path().to_string_lossy().into_owned())
            .expect("open vault");
        session.scan_initial(CancelToken::new()).unwrap();
        let tasks = session.tasks_for_file("n.md".into()).unwrap();
        assert_eq!(tasks.len(), 2);
        assert_eq!(tasks[0].status_char, " ");
        assert_eq!(tasks[1].status_char, "x");
        assert!(tasks[1].completed);
        assert!(tasks[1].due_ms.is_some());
    }

    #[test]
    fn toggle_task_status_round_trips_through_ffi() {
        let tmp = tempfile::tempdir().expect("tempdir");
        std::fs::write(tmp.path().join("n.md"), "- [ ] thing\n").unwrap();
        let session = VaultSession::open_filesystem(tmp.path().to_string_lossy().into_owned())
            .expect("open vault");
        session.scan_initial(CancelToken::new()).unwrap();

        let report = session
            .toggle_task_status("n.md".into(), 0, "x".into(), None)
            .expect("toggle ok");
        assert!(!report.new_content_hash.is_empty());

        let after = std::fs::read_to_string(tmp.path().join("n.md")).unwrap();
        assert_eq!(after, "- [x] thing\n");
    }

    #[test]
    fn toggle_task_status_multi_char_status_string_returns_invalid_argument() {
        let tmp = tempfile::tempdir().expect("tempdir");
        std::fs::write(tmp.path().join("n.md"), "- [ ] thing\n").unwrap();
        let session = VaultSession::open_filesystem(tmp.path().to_string_lossy().into_owned())
            .expect("open vault");
        session.scan_initial(CancelToken::new()).unwrap();

        match session.toggle_task_status("n.md".into(), 0, "xy".into(), None) {
            Err(VaultError::InvalidArgument { message }) => {
                assert!(
                    message.contains("printable ASCII"),
                    "expected printable-ASCII message; got: {message}"
                );
            }
            other => panic!("expected InvalidArgument, got {other:?}"),
        }
    }

    // --- #147: tighter status-char allowlist on the FFI ---
    //
    // The previous shape accepted any single Unicode scalar, so a
    // caller passing "\n" / "[" / "🇺🇸" / "\u{200D}" could either
    // corrupt the file outright (newline splits the task line) or
    // produce a task that no renderer recognises. The Mac UI never
    // exercises these — it hardcodes ' ' / 'x' / '/' / '-' — but
    // scripted callers and tester explorations get a clean error
    // instead of silent on-disk damage.

    #[test]
    fn toggle_task_status_rejects_newline_status_char() {
        let tmp = tempfile::tempdir().expect("tempdir");
        std::fs::write(tmp.path().join("n.md"), "- [ ] thing\n").unwrap();
        let session = VaultSession::open_filesystem(tmp.path().to_string_lossy().into_owned())
            .expect("open vault");
        session.scan_initial(CancelToken::new()).unwrap();

        match session.toggle_task_status("n.md".into(), 0, "\n".into(), None) {
            Err(VaultError::InvalidArgument { message }) => {
                assert!(
                    message.contains("printable ASCII"),
                    "expected allowlist rejection; got: {message}"
                );
            }
            other => panic!("expected InvalidArgument for newline, got {other:?}"),
        }
        // File untouched — the rejection must happen before any IO.
        let on_disk = std::fs::read_to_string(tmp.path().join("n.md")).unwrap();
        assert_eq!(on_disk, "- [ ] thing\n");
    }

    #[test]
    fn toggle_task_status_rejects_bracket_status_chars() {
        let tmp = tempfile::tempdir().expect("tempdir");
        std::fs::write(tmp.path().join("n.md"), "- [ ] thing\n").unwrap();
        let session = VaultSession::open_filesystem(tmp.path().to_string_lossy().into_owned())
            .expect("open vault");
        session.scan_initial(CancelToken::new()).unwrap();

        for bad in ["[", "]"] {
            match session.toggle_task_status("n.md".into(), 0, bad.into(), None) {
                Err(VaultError::InvalidArgument { .. }) => {}
                other => panic!("expected InvalidArgument for {bad:?}, got {other:?}"),
            }
        }
    }

    #[test]
    fn toggle_task_status_rejects_non_ascii_status_char() {
        let tmp = tempfile::tempdir().expect("tempdir");
        std::fs::write(tmp.path().join("n.md"), "- [ ] thing\n").unwrap();
        let session = VaultSession::open_filesystem(tmp.path().to_string_lossy().into_owned())
            .expect("open vault");
        session.scan_initial(CancelToken::new()).unwrap();

        for bad in ["✓", "é", "\u{200D}"] {
            match session.toggle_task_status("n.md".into(), 0, bad.into(), None) {
                Err(VaultError::InvalidArgument { .. }) => {}
                other => panic!("expected InvalidArgument for {bad:?}, got {other:?}"),
            }
        }
    }

    #[test]
    fn toggle_task_status_accepts_the_common_status_set() {
        // Document the canonical accepted set so the contract is
        // visible in tests, not just doc comments. Each call must
        // succeed; the resulting status_char is what we asked for.
        let tmp = tempfile::tempdir().expect("tempdir");
        std::fs::write(tmp.path().join("n.md"), "- [ ] thing\n").unwrap();
        let session = VaultSession::open_filesystem(tmp.path().to_string_lossy().into_owned())
            .expect("open vault");
        session.scan_initial(CancelToken::new()).unwrap();

        for ch in [" ", "x", "X", "/", "-", "!", "?"] {
            session
                .toggle_task_status("n.md".into(), 0, ch.into(), None)
                .unwrap_or_else(|e| {
                    panic!("expected {ch:?} to be accepted by the allowlist; got {e:?}")
                });
        }
    }

    #[test]
    fn read_oplog_round_trips_entries_through_ffi() {
        let tmp = tempfile::tempdir().expect("tempdir");
        let session = VaultSession::open_filesystem(tmp.path().to_string_lossy().into_owned())
            .expect("open vault");
        session
            .save_text("note.md".into(), "v1".into(), None)
            .unwrap();
        session
            .save_text("note.md".into(), "v2".into(), None)
            .unwrap();

        let entries = session.read_oplog("note.md".into()).unwrap();
        assert_eq!(entries.len(), 2);
        assert!(matches!(entries[0].op_kind, OpKind::WholeFileReplace));
        assert_eq!(entries[0].payload_bytes, b"v1");
        assert_eq!(entries[1].payload_bytes, b"v2");
    }

    #[test]
    fn read_oplog_surfaces_edit_batch_kind_through_ffi() {
        // A small edit in a larger note logs a fine-grained EditBatch; the
        // new kind must cross the FFI (#378).
        let tmp = tempfile::tempdir().expect("tempdir");
        let session = VaultSession::open_filesystem(tmp.path().to_string_lossy().into_owned())
            .expect("open vault");
        let v1 = "# Note\n\nFirst paragraph line here.\nSecond paragraph line here.\n\
                  Third paragraph line here.\nFourth line here.\n";
        let r1 = session
            .save_text("note.md".into(), v1.into(), None)
            .unwrap();
        let v2 = v1.replace("Second paragraph line here.", "Second line was CHANGED.");
        session
            .save_text("note.md".into(), v2, Some(r1.new_content_hash))
            .unwrap();

        let entries = session.read_oplog("note.md".into()).unwrap();
        assert_eq!(entries.len(), 2);
        assert!(matches!(entries[0].op_kind, OpKind::WholeFileReplace));
        assert!(
            matches!(entries[1].op_kind, OpKind::EditBatch),
            "a fine-grained edit must surface as EditBatch across the FFI"
        );
    }

    #[test]
    fn extract_template_metadata_round_trips_through_ffi() {
        let meta = extract_template_metadata(
            "# {{title}}\n\nTopic: {{prompt:Topic}}\nAgain: {{prompt:Topic}}\n".to_string(),
        );
        assert_eq!(meta.prompts.len(), 1);
        assert_eq!(meta.prompts[0].key, "topic");
        assert_eq!(meta.prompts[0].label, "Topic");
    }

    #[test]
    fn list_templates_and_render_template_round_trip_through_ffi() {
        let tmp = tempfile::tempdir().expect("tempdir");
        std::fs::create_dir(tmp.path().join("Templates")).unwrap();
        // Note: no frontmatter on the template body itself — the
        // create-from-template flow renders the source verbatim, so
        // anything in the template (frontmatter included) lands in
        // the new note. The picker's description comes from the
        // separate `description:` lookup the picker test below covers.
        std::fs::write(
            tmp.path().join("Templates/Meeting.md"),
            b"# Meeting: {{prompt:Topic}}\n\n{{cursor}}\n",
        )
        .unwrap();
        std::fs::write(
            tmp.path().join("Templates/Daily.md"),
            b"---\ndescription: Daily-note layout\n---\n",
        )
        .unwrap();
        let session = VaultSession::open_filesystem(tmp.path().to_string_lossy().into_owned())
            .expect("open vault");

        let cancelled = CancelToken::new();
        cancelled.cancel();
        assert!(matches!(
            session.list_templates(cancelled),
            Err(VaultError::Cancelled)
        ));

        let templates = session
            .list_templates(CancelToken::new())
            .expect("list_templates");
        assert_eq!(templates.len(), 2);
        assert_eq!(templates[0].path, "Templates/Daily.md");
        assert_eq!(templates[0].name, "Daily");
        assert_eq!(
            templates[0].description.as_deref(),
            Some("Daily-note layout")
        );
        assert_eq!(templates[1].name, "Meeting");

        let mut prompt_values = HashMap::new();
        prompt_values.insert("topic".to_string(), "Q1 sync".to_string());
        let ctx = TemplateContext {
            now_ms: 1_700_000_000_000,
            title: "ignored".into(),
            vault_name: "MyVault".into(),
            prompt_values,
        };
        let rendered = session
            .render_template("Templates/Meeting.md".into(), ctx)
            .expect("render");
        assert_eq!(rendered.body, "# Meeting: Q1 sync\n\n\n");
        assert_eq!(
            rendered.cursor_byte_offset,
            Some("# Meeting: Q1 sync\n\n".len() as u64)
        );
    }
}

#[cfg(test)]
mod canvas_mirror_tests {
    //! Type-mirror parity (#361): every canvas FFI shape converts from
    //! its core counterpart without loss, and the full read API is
    //! drivable through the FFI wrapper against a real vault.

    use super::*;

    #[test]
    fn enum_mirrors_are_total() {
        use slate_core::canvas::model::EdgeDirection as D;
        for (c, f) in [
            (D::Outgoing, CanvasEdgeDirection::Outgoing),
            (D::Incoming, CanvasEdgeDirection::Incoming),
            (D::Bidirectional, CanvasEdgeDirection::Bidirectional),
            (D::Undirected, CanvasEdgeDirection::Undirected),
        ] {
            assert_eq!(CanvasEdgeDirection::from(c), f);
        }
        use slate_core::canvas::Side as S;
        for (c, f) in [
            (S::Top, CanvasSide::Top),
            (S::Right, CanvasSide::Right),
            (S::Bottom, CanvasSide::Bottom),
            (S::Left, CanvasSide::Left),
        ] {
            assert_eq!(CanvasSide::from(c), f);
        }
        use slate_core::canvas::placement::RelativeDesc as R;
        assert_eq!(
            CanvasRelativeDesc::from(R::Below("A".into())),
            CanvasRelativeDesc::Below {
                anchor_title: "A".into()
            }
        );
        assert_eq!(
            CanvasRelativeDesc::from(R::AtOrigin),
            CanvasRelativeDesc::AtOrigin
        );
    }

    #[test]
    fn read_api_drives_over_the_ffi_wrapper() {
        let tmp = tempfile::tempdir().unwrap();
        std::fs::write(
            tmp.path().join("b.canvas"),
            include_str!("../../slate-core/tests/fixtures/canvas/sample.canvas"),
        )
        .unwrap();
        let session = VaultSession::open_filesystem(tmp.path().to_string_lossy().into_owned())
            .expect("open vault");

        let info = session.open_canvas("b.canvas".into()).expect("open canvas");
        assert!(!info.degraded);
        assert_eq!(info.node_count, 9);

        let outline = session.canvas_outline(info.handle).unwrap();
        assert_eq!(outline.len(), 9);
        assert_eq!(outline[0].node_id, "grp-research");

        let rows = session.canvas_table_rows(info.handle).unwrap();
        assert_eq!(rows.len(), 9);

        let neighbors = session
            .canvas_neighbors(info.handle, "card-question".into())
            .unwrap();
        assert_eq!(neighbors.len(), 3);

        let ctx = session
            .canvas_where_am_i(info.handle, "card-question".into())
            .unwrap();
        assert_eq!(ctx.title, "Core question");

        let p = session
            .canvas_place_new(
                info.handle,
                Some("card-loose".into()),
                260.0,
                140.0,
                Some(CanvasPlaceDirection::RightOf),
                Vec::new(),
            )
            .unwrap();
        assert!(matches!(p.relative, CanvasRelativeDesc::RightOf { .. }));

        let sp = session
            .canvas_place_set(
                info.handle,
                Some("card-loose".into()),
                vec![CanvasRect {
                    x: 0.0,
                    y: 0.0,
                    width: 100.0,
                    height: 50.0,
                }],
                None,
                Vec::new(),
            )
            .unwrap();
        assert_eq!(sp.origins.len(), 1);

        let overlaps = session
            .canvas_check_overlap(
                info.handle,
                CanvasRect {
                    x: 0.0,
                    y: 0.0,
                    width: 10.0,
                    height: 10.0,
                },
                vec!["card-question".into()],
            )
            .unwrap();
        assert!(overlaps.is_empty());

        session.close_canvas(info.handle);
        assert!(session.canvas_outline(info.handle).is_err());
    }

    // --- Layout session FFI (Milestone P #558) -------------------------

    const LAYOUT_FILTER: GraphFilter = GraphFilter {
        include_attachments: false,
        include_ghosts: true,
        orphans_only: false,
    };
    const LAYOUT_FORCES: LayoutForces = LayoutForces {
        center: 0.5,
        repel: 0.5,
        link: 0.5,
        link_distance: 0.5,
    };
    const LAYOUT_CONFIG: LayoutConfig = LayoutConfig {
        seed: 0,
        max_iterations: 300,
        warm_iterations: 60,
    };

    /// A tiny fixed link graph: a → b, a → c, b → c, plus an orphan d.
    fn seed_layout_vault(dir: &std::path::Path) {
        std::fs::write(dir.join("a.md"), "# A\n[[b]] and [[c]]\n").unwrap();
        std::fs::write(dir.join("b.md"), "# B\n[[c]]\n").unwrap();
        std::fs::write(dir.join("c.md"), "# C\n").unwrap();
        std::fs::write(dir.join("d.md"), "# D, an orphan\n").unwrap();
    }

    fn open_layout_vault(dir: &std::path::Path) -> Arc<VaultSession> {
        seed_layout_vault(dir);
        let session =
            VaultSession::open_filesystem(dir.to_string_lossy().into_owned()).expect("open vault");
        session.scan_initial(CancelToken::new()).unwrap();
        session
    }

    #[test]
    fn layout_frame_is_2n_f32s_and_locks_to_the_snapshot_order() {
        let tmp = tempfile::tempdir().unwrap();
        let session = open_layout_vault(tmp.path());
        let layout = session
            .clone()
            .start_graph_layout(LAYOUT_FILTER, LAYOUT_FORCES, LAYOUT_CONFIG)
            .unwrap();

        let ids = layout.node_ids();
        let snapshot = session.graph_snapshot(LAYOUT_FILTER).unwrap();
        let snapshot_ids: Vec<u64> = snapshot.nodes.iter().map(|n| n.id).collect();
        // The layout's node order IS the P0-3 snapshot's key-sorted order —
        // so positions[2i], positions[2i+1] name node_ids()[i] == snapshot
        // row i. This is the id↔position order lock.
        assert_eq!(ids, snapshot_ids);
        assert_eq!(layout.edges().len(), snapshot.edges.len());

        let frame = layout.tick(1);
        assert_eq!(frame.positions.len(), ids.len() * 2, "frame is exactly 2×n");
        assert_eq!(frame.generation, snapshot.generation);
        assert!(frame.positions.iter().all(|c| c.is_finite()));
    }

    #[test]
    fn layout_is_bit_identical_across_two_sessions() {
        let t1 = tempfile::tempdir().unwrap();
        let t2 = tempfile::tempdir().unwrap();
        let s1 = open_layout_vault(t1.path());
        let s2 = open_layout_vault(t2.path());
        let l1 = s1
            .start_graph_layout(LAYOUT_FILTER, LAYOUT_FORCES, LAYOUT_CONFIG)
            .unwrap();
        let l2 = s2
            .start_graph_layout(LAYOUT_FILTER, LAYOUT_FORCES, LAYOUT_CONFIG)
            .unwrap();

        // Same content, same insertion order ⇒ same ids and, on a given
        // platform, bit-identical frames after the same budget (DoD §P-C
        // reaching through the FFI).
        assert_eq!(l1.node_ids(), l2.node_ids());
        let f1 = l1.tick(120);
        let f2 = l2.tick(120);
        assert_eq!(f1.iteration, f2.iteration);
        assert_eq!(f1.positions, f2.positions);
    }

    #[test]
    fn run_to_convergence_honors_cancel() {
        let tmp = tempfile::tempdir().unwrap();
        let session = open_layout_vault(tmp.path());

        // (a) Pre-cancelled from a COLD start: the loop checks before the
        // first step, so zero iterations run.
        let layout = session
            .clone()
            .start_graph_layout(LAYOUT_FILTER, LAYOUT_FORCES, LAYOUT_CONFIG)
            .unwrap();
        let token = CancelToken::new();
        token.cancel();
        assert_eq!(layout.run_to_convergence(token).iteration, 0);

        // (b) Pre-cancelled from a WARM start ⇒ zero ADDITIONAL iterations,
        // proving the top-of-loop cancel gate holds at any iteration count
        // (a concrete instance of the "≤10 iterations after cancel" bound;
        // the loop never steps more than 10 between cancel checks).
        let layout = session
            .clone()
            .start_graph_layout(LAYOUT_FILTER, LAYOUT_FORCES, LAYOUT_CONFIG)
            .unwrap();
        let warm = layout.tick(37);
        let token = CancelToken::new();
        token.cancel();
        assert_eq!(layout.run_to_convergence(token).iteration, warm.iteration);

        // (c) Cancelled DURING a live solve from another thread: with a
        // huge ceiling the run would never stop on its own, so returning
        // far below the cap proves the in-loop cancel check interrupts it.
        let cfg = LayoutConfig {
            seed: 0,
            max_iterations: 100_000,
            warm_iterations: 60,
        };
        let layout = session
            .start_graph_layout(LAYOUT_FILTER, LAYOUT_FORCES, cfg)
            .unwrap();
        let token = CancelToken::new();
        let canceller = {
            let t = token.clone();
            std::thread::spawn(move || t.cancel())
        };
        let frame = layout.run_to_convergence(token);
        canceller.join().unwrap();
        assert!(
            frame.iteration < cfg.max_iterations,
            "a live cancel interrupts the solve well before the ceiling"
        );
    }

    #[test]
    fn run_to_convergence_ceiling_is_exact_for_awkward_caps() {
        // The 4-node graph never reaches the tight convergence tolerance,
        // so the per-call iteration ceiling is exactly what bounds each
        // run — proving the loop honors caps that aren't multiples of 10
        // (and 0), never overshooting by up to a chunk.
        let tmp = tempfile::tempdir().unwrap();
        let session = open_layout_vault(tmp.path());
        for cap in [0u32, 1, 10, 11] {
            let cfg = LayoutConfig {
                seed: 0,
                max_iterations: cap,
                warm_iterations: 60,
            };
            let layout = session
                .clone()
                .start_graph_layout(LAYOUT_FILTER, LAYOUT_FORCES, cfg)
                .unwrap();
            let frame = layout.run_to_convergence(CancelToken::new());
            assert_eq!(frame.iteration, cap, "cold cap {cap} honored exactly");
        }

        // The ceiling is per-call ADDITIONAL work, exact from a warm start
        // too: a second run adds exactly `cap` more, never overshooting.
        let cfg = LayoutConfig {
            seed: 0,
            max_iterations: 11,
            warm_iterations: 60,
        };
        let layout = session
            .start_graph_layout(LAYOUT_FILTER, LAYOUT_FORCES, cfg)
            .unwrap();
        assert_eq!(layout.run_to_convergence(CancelToken::new()).iteration, 11);
        assert_eq!(layout.run_to_convergence(CancelToken::new()).iteration, 22);
    }

    #[test]
    fn run_to_convergence_reports_convergence_on_a_settling_graph() {
        // One isolated node seeds AT the origin (golden-angle radius 0)
        // with zero net force, so the convergence predicate holds on the
        // very first step — proving `converged` is wired through the FFI.
        let tmp = tempfile::tempdir().unwrap();
        std::fs::write(tmp.path().join("solo.md"), "# Solo\n").unwrap();
        let session =
            VaultSession::open_filesystem(tmp.path().to_string_lossy().into_owned()).unwrap();
        session.scan_initial(CancelToken::new()).unwrap();
        let layout = session
            .start_graph_layout(LAYOUT_FILTER, LAYOUT_FORCES, LAYOUT_CONFIG)
            .unwrap();

        let settled = layout.run_to_convergence(CancelToken::new());
        assert!(settled.converged);
        assert!(settled.iteration <= LAYOUT_CONFIG.max_iterations);
    }

    #[test]
    fn pin_node_holds_a_slot_fixed_until_unpinned() {
        let tmp = tempfile::tempdir().unwrap();
        let session = open_layout_vault(tmp.path());
        let layout = session
            .start_graph_layout(LAYOUT_FILTER, LAYOUT_FORCES, LAYOUT_CONFIG)
            .unwrap();
        let target = layout.node_ids()[0];

        layout.pin_node(target, 42.0, -7.0);
        let pinned = layout.tick(80);
        assert_eq!((pinned.positions[0], pinned.positions[1]), (42.0, -7.0));

        layout.unpin_node(target);
        let released = layout.tick(80);
        assert!(
            (released.positions[0], released.positions[1]) != (42.0, -7.0),
            "an unpinned node is free to move under the forces"
        );
    }

    #[test]
    fn refresh_is_a_noop_until_the_graph_changes_then_reflects_churn() {
        let tmp = tempfile::tempdir().unwrap();
        let session = open_layout_vault(tmp.path());
        let layout = session
            .clone()
            .start_graph_layout(LAYOUT_FILTER, LAYOUT_FORCES, LAYOUT_CONFIG)
            .unwrap();

        let ids_before = layout.node_ids();
        let gen_before = session.graph_generation();
        // No change ⇒ cheap None probe, ids untouched.
        assert!(layout.refresh().unwrap().is_none());
        assert_eq!(layout.node_ids(), ids_before);

        // Add a new note linking an existing one — the hooked write bumps
        // the graph generation.
        session
            .save_text("e.md".into(), "# E\n[[a]]\n".into(), None)
            .unwrap();
        let gen_after = session.graph_generation();
        assert!(gen_after > gen_before);

        let frame = layout
            .refresh()
            .unwrap()
            .expect("generation moved ⇒ a fresh frame");
        assert_eq!(frame.generation, gen_after);

        // The topology re-fetch reflects the new node, still in snapshot
        // order (order lock preserved across churn), and the frame is 2×n.
        let ids_after = layout.node_ids();
        assert_eq!(ids_after.len(), ids_before.len() + 1);
        assert_eq!(frame.positions.len(), ids_after.len() * 2);
        let snapshot_ids: Vec<u64> = session
            .graph_snapshot(LAYOUT_FILTER)
            .unwrap()
            .nodes
            .iter()
            .map(|n| n.id)
            .collect();
        assert_eq!(ids_after, snapshot_ids);
    }

    /// Map each node's path to its `(x, y)` in `frame`, joining the frame's
    /// position slots (in `ids` order) to paths via the current snapshot.
    /// Ghost nodes (no path) are skipped.
    fn coords_by_path(
        session: &VaultSession,
        ids: &[u64],
        frame: &LayoutFrame,
    ) -> std::collections::HashMap<String, (f32, f32)> {
        let snapshot = session.graph_snapshot(LAYOUT_FILTER).unwrap();
        let id_to_path: std::collections::HashMap<u64, String> = snapshot
            .nodes
            .iter()
            .filter_map(|n| n.path.clone().map(|p| (n.id, p)))
            .collect();
        ids.iter()
            .enumerate()
            .filter_map(|(i, id)| {
                id_to_path.get(id).map(|p| {
                    (
                        p.clone(),
                        (frame.positions[2 * i], frame.positions[2 * i + 1]),
                    )
                })
            })
            .collect()
    }

    #[test]
    fn forces_and_pins_reject_non_finite_inputs() {
        let tmp = tempfile::tempdir().unwrap();
        let session = open_layout_vault(tmp.path());
        let nan = f32::NAN;
        let inf = f32::INFINITY;

        // Non-finite force sliders are folded to the default at the FFI
        // boundary, so cold positions stay finite — P2-1's no-NaN/Inf
        // invariant, defended against foreign input (a NaN link_distance
        // would otherwise make k, and every position, NaN).
        let poison = LayoutForces {
            center: nan,
            repel: inf,
            link: -inf,
            link_distance: nan,
        };
        let layout = session
            .clone()
            .start_graph_layout(LAYOUT_FILTER, poison, LAYOUT_CONFIG)
            .unwrap();
        assert!(layout.tick(40).positions.iter().all(|c| c.is_finite()));

        layout.set_forces(poison);
        assert!(layout.tick(40).positions.iter().all(|c| c.is_finite()));

        // A non-finite pin is ignored — otherwise it would plant NaN/∞ in
        // the very next frame and contaminate neighbors through the forces.
        let id = layout.node_ids()[0];
        layout.pin_node(id, nan, inf);
        assert!(layout.tick(20).positions.iter().all(|c| c.is_finite()));

        // A finite pin still takes effect (the guard rejects only non-finite).
        layout.pin_node(id, 5.0, 5.0);
        let pinned = layout.tick(20);
        assert_eq!((pinned.positions[0], pinned.positions[1]), (5.0, 5.0));
    }

    #[test]
    fn refresh_carries_survivor_positions_by_key_across_slot_churn() {
        let tmp = tempfile::tempdir().unwrap();
        let session = open_layout_vault(tmp.path());
        let layout = session
            .clone()
            .start_graph_layout(LAYOUT_FILTER, LAYOUT_FORCES, LAYOUT_CONFIG)
            .unwrap();

        // Move positions off the deterministic spiral so a wrongly-keyed
        // carry-over would be detectable.
        let before_frame = layout.tick(60);
        let before = coords_by_path(&session, &layout.node_ids(), &before_frame);

        // Add a note whose path sorts BEFORE every existing one, shifting
        // every survivor's key-sorted slot by +1. If positions were carried
        // by slot (the bug), survivors would inherit a neighbor's
        // coordinates; carried by KEY (correct), each keeps its own.
        session
            .save_text("0_new.md".into(), "# New\n[[a]]\n".into(), None)
            .unwrap();
        let after_frame = layout
            .refresh()
            .unwrap()
            .expect("added node bumps the generation");
        let after_ids = layout.node_ids();
        assert_eq!(after_ids.len(), before.len() + 1);
        let after = coords_by_path(&session, &after_ids, &after_frame);

        for note in ["a.md", "b.md", "c.md", "d.md"] {
            assert_eq!(
                before.get(note),
                after.get(note),
                "{note} kept its coordinates under its NEW slot (carry by key, not slot)"
            );
        }

        // The rebuilt id_to_slot is correct: pinning a survivor by its NEW
        // id fixes the RIGHT position slot.
        let snapshot = session.graph_snapshot(LAYOUT_FILTER).unwrap();
        let c_id = snapshot
            .nodes
            .iter()
            .find(|n| n.path.as_deref() == Some("c.md"))
            .unwrap()
            .id;
        let c_slot = after_ids.iter().position(|&id| id == c_id).unwrap();
        layout.pin_node(c_id, 123.0, -45.0);
        let pinned = layout.tick(50);
        assert_eq!(
            (
                pinned.positions[2 * c_slot],
                pinned.positions[2 * c_slot + 1]
            ),
            (123.0, -45.0),
            "pin landed on c's rebuilt slot"
        );
    }

    /// Map each note path to its current backend id via the snapshot.
    fn id_by_path(session: &VaultSession) -> std::collections::HashMap<String, u64> {
        session
            .graph_snapshot(LAYOUT_FILTER)
            .unwrap()
            .nodes
            .iter()
            .filter_map(|n| n.path.clone().map(|p| (p, n.id)))
            .collect()
    }

    #[test]
    fn refresh_after_node_removal_rekeys_survivors_by_key() {
        let tmp = tempfile::tempdir().unwrap();
        let session = open_layout_vault(tmp.path()); // a,b,c,d
        let layout = session
            .clone()
            .start_graph_layout(LAYOUT_FILTER, LAYOUT_FORCES, LAYOUT_CONFIG)
            .unwrap();

        let before_frame = layout.tick(60);
        let before_ids = layout.node_ids();
        let before_coords = coords_by_path(&session, &before_ids, &before_frame);
        let ids_before = id_by_path(&session);

        // Delete `a.md` — the source-only node at backend index 0 that
        // nobody links to (so its removal creates no dangling ghost) — then
        // reconcile it out of the cache and force a rebuild. Removing the
        // FIRST index compacts every survivor's StableGraph index down by
        // one, so b/c/d all get REASSIGNED backend ids. This is the case
        // the previous test couldn't reach (its ids never changed).
        std::fs::remove_file(tmp.path().join("a.md")).unwrap();
        session.scan_initial(CancelToken::new()).unwrap();
        session.inner.graph_drop_for_bench();

        let after_frame = layout
            .refresh()
            .unwrap()
            .expect("removal + rebuild moves the generation");
        let after_ids = layout.node_ids();

        // a is gone; b/c/d survive, still in fresh-snapshot order.
        let snapshot_ids: Vec<u64> = session
            .graph_snapshot(LAYOUT_FILTER)
            .unwrap()
            .nodes
            .iter()
            .map(|n| n.id)
            .collect();
        assert_eq!(after_ids, snapshot_ids);
        assert_eq!(after_ids.len(), before_ids.len() - 1);

        // The hole-compacting rebuild reassigned survivor ids — so this
        // genuinely exercises re-sync BY KEY, not stale-id reuse.
        let ids_after = id_by_path(&session);
        assert!(
            ["b.md", "c.md", "d.md"]
                .iter()
                .any(|p| ids_before.get(*p) != ids_after.get(*p)),
            "a hole-compacting rebuild must reassign at least one survivor id"
        );

        // Positions stayed associated by KEY across the rekey; a's are gone.
        let after_coords = coords_by_path(&session, &after_ids, &after_frame);
        for note in ["b.md", "c.md", "d.md"] {
            assert_eq!(
                before_coords.get(note),
                after_coords.get(note),
                "{note} kept its coordinates across removal + rekey"
            );
        }
        assert!(!after_coords.contains_key("a.md"));

        // Pinning a survivor by its NEW id lands on its new slot (id_to_slot
        // rebuilt against the reassigned ids).
        let c_id = *ids_after.get("c.md").unwrap();
        let c_slot = after_ids.iter().position(|&id| id == c_id).unwrap();
        layout.pin_node(c_id, 88.0, -88.0);
        let pinned = layout.tick(40);
        assert_eq!(
            (
                pinned.positions[2 * c_slot],
                pinned.positions[2 * c_slot + 1]
            ),
            (88.0, -88.0),
            "pin landed on c's rebuilt slot"
        );
    }

    #[test]
    fn run_to_convergence_runs_at_most_one_chunk_after_a_mid_flight_cancel() {
        // The normative "≤10 iterations after cancel" bound: a cancel that
        // arrives AFTER a top-of-loop check has passed must let the
        // in-flight chunk finish (≤10) and then stop at the next check. The
        // test seam fires the cancel at exactly that point, deterministically
        // (no unsynchronized thread that might cancel before the run starts).
        let tmp = tempfile::tempdir().unwrap();
        let session = open_layout_vault(tmp.path());
        let cfg = LayoutConfig {
            seed: 0,
            max_iterations: 1000,
            warm_iterations: 60,
        };
        let layout = session
            .start_graph_layout(LAYOUT_FILTER, LAYOUT_FORCES, cfg)
            .unwrap();
        layout.arm_cancel_after_next_check();
        let frame = layout.run_to_convergence(CancelToken::new());
        // The in-flight chunk ran (>0) and nothing beyond it did (≤10). The
        // 4-node graph never converges, so only the cancel could stop it.
        assert!(
            frame.iteration > 0 && frame.iteration <= 10,
            "at most one ≤10 chunk runs after a mid-flight cancel (got {})",
            frame.iteration
        );
    }
}
