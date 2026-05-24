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
}

impl From<core::Heading> for Heading {
    fn from(h: core::Heading) -> Self {
        Heading {
            level: h.level,
            text: h.text,
            ordinal: h.ordinal,
            anchor_id: h.anchor_id,
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
            core::VaultError::WriteConflict {
                current_content_hash,
                expected_content_hash,
                current_mtime_ms,
            } => VaultError::WriteConflict {
                current_content_hash,
                expected_content_hash,
                current_mtime_ms,
            },
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

/// FFI-exposed vault session. Wraps `slate_core::VaultSession`.
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
    /// `<root_path>/.slate/cache.sqlite`.
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

    /// Save UTF-8 text to a vault path, refresh the index, and append
    /// a `WholeFileReplace` entry to the file's op-log.
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
    /// Returns an empty list — never an error — when the vault has no
    /// templates folder configured, or when the folder vanished after
    /// session open. Only `.md` files are included; results are sorted
    /// alphabetically by name (case-insensitive).
    pub fn list_templates(&self) -> Result<Vec<TemplateSummary>, VaultError> {
        Ok(self
            .inner
            .list_templates()?
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

/// Scope of a `full_text_search` call. `File` and `Tag` are
/// reserved for later milestones; today they return
/// `VaultError::Cancelled`.
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

/// Kind of operation recorded in an op-log entry. V1.F only ships
/// `WholeFileReplace`.
#[derive(Debug, uniffi::Enum)]
pub enum OpKind {
    WholeFileReplace,
}

impl From<core::OpKind> for OpKind {
    fn from(k: core::OpKind) -> Self {
        match k {
            core::OpKind::WholeFileReplace => OpKind::WholeFileReplace,
        }
    }
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

        let templates = session.list_templates().expect("list_templates");
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
