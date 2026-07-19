// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W0-1 counter-candidate (#714, w0_spec rule 2b): a hand-written C-ABI
// shim over slate-core covering the spike's rule-1 probe surface, for
// csbindgen to turn into C# P/Invoke declarations.
//
// Everything uniffi generates for free is hand-rolled here, and its
// line count + unsafe surface is the spike's finding:
//   - handle ownership (Box::into_raw / from_raw pairs per object)
//   - UTF-8 (ptr,len) marshalling both directions + a Rust-owned buffer
//     type with an explicit free contract
//   - error mapping flattened to status codes + out-params (typed fields
//     survive only where a dedicated out-param was hand-plumbed)
//   - foreign callbacks as raw fn pointers + a context pointer whose
//     Send+Sync is *asserted*, not proven
//   - panic containment at every boundary fn (extern "C" unwind is UB)
//
// C#-side obligations the uniffi runtime otherwise owns: callbacks MUST
// catch every exception before returning into Rust, and every SlateBuf
// received must go back through slate_buf_free exactly once.

use std::ffi::c_void;
use std::panic::{catch_unwind, AssertUnwindSafe};
use std::sync::{Arc, Mutex};

use slate_core as core;

// ---------------------------------------------------------------------
// Status codes (VaultError flattened) + panic sentinel
// ---------------------------------------------------------------------

pub const SLATE_OK: i32 = 0;
pub const SLATE_ERR_IO: i32 = 1;
pub const SLATE_ERR_DB: i32 = 2;
pub const SLATE_ERR_INVALID_PATH: i32 = 3;
pub const SLATE_ERR_TRASH: i32 = 4;
pub const SLATE_ERR_CANCELLED: i32 = 5;
pub const SLATE_ERR_INVALID_UTF8: i32 = 6;
pub const SLATE_ERR_FILE_TOO_LARGE: i32 = 7;
pub const SLATE_ERR_INVALID_QUERY: i32 = 8;
pub const SLATE_ERR_UNSUPPORTED: i32 = 9;
pub const SLATE_ERR_INVALID_ARGUMENT: i32 = 10;
pub const SLATE_ERR_DESTINATION_EXISTS: i32 = 11;
pub const SLATE_ERR_WRITE_CONFLICT: i32 = 12;
pub const SLATE_ERR_HISTORY_UNAVAILABLE: i32 = 13;
pub const SLATE_ERR_MALFORMED_FRONTMATTER: i32 = 14;
pub const SLATE_ERR_BIB_SOURCE_UNREADABLE: i32 = 15;
pub const SLATE_ERR_CSL_STYLE_UNREADABLE: i32 = 16;
pub const SLATE_ERR_PREFS_UNREADABLE: i32 = 17;
pub const SLATE_ERR_COMMAND_UNKNOWN_ID: i32 = 100;
pub const SLATE_ERR_COMMAND_ACTION_FAILED: i32 = 101;
pub const SLATE_ERR_PANIC: i32 = -2;
pub const SLATE_ERR_BAD_ARG: i32 = -3;

fn vault_error_code(e: &core::VaultError) -> i32 {
    // Exhaustive: a new core variant is a compile error here, which is
    // this design's (manual) equivalent of uniffi's generated totality.
    match e {
        core::VaultError::Io(_) => SLATE_ERR_IO,
        core::VaultError::Db(_) => SLATE_ERR_DB,
        core::VaultError::InvalidPath { .. } => SLATE_ERR_INVALID_PATH,
        core::VaultError::Trash { .. } => SLATE_ERR_TRASH,
        core::VaultError::Cancelled => SLATE_ERR_CANCELLED,
        core::VaultError::InvalidUtf8 { .. } => SLATE_ERR_INVALID_UTF8,
        core::VaultError::FileTooLarge { .. } => SLATE_ERR_FILE_TOO_LARGE,
        core::VaultError::InvalidQuery { .. } => SLATE_ERR_INVALID_QUERY,
        core::VaultError::Unsupported { .. } => SLATE_ERR_UNSUPPORTED,
        core::VaultError::InvalidArgument { .. } => SLATE_ERR_INVALID_ARGUMENT,
        core::VaultError::DestinationExists { .. } => SLATE_ERR_DESTINATION_EXISTS,
        core::VaultError::WriteConflict { .. } => SLATE_ERR_WRITE_CONFLICT,
        core::VaultError::HistoryUnavailable { .. } => SLATE_ERR_HISTORY_UNAVAILABLE,
        core::VaultError::MalformedFrontmatter { .. } => SLATE_ERR_MALFORMED_FRONTMATTER,
        core::VaultError::BibSourceUnreadable { .. } => SLATE_ERR_BIB_SOURCE_UNREADABLE,
        core::VaultError::CslStyleUnreadable { .. } => SLATE_ERR_CSL_STYLE_UNREADABLE,
        core::VaultError::PrefsUnreadable { .. } => SLATE_ERR_PREFS_UNREADABLE,
    }
}

// ---------------------------------------------------------------------
// Rust-owned byte buffer with an explicit free contract
// ---------------------------------------------------------------------

/// Rust-allocated bytes handed across the boundary. `ptr` is owned by
/// Rust; the C# side must call `slate_buf_free` exactly once (a missed
/// call leaks, a double call corrupts the allocator).
#[repr(C)]
pub struct SlateBuf {
    pub ptr: *mut u8,
    pub len: usize,
}

impl SlateBuf {
    fn empty() -> Self {
        Self { ptr: std::ptr::null_mut(), len: 0 }
    }

    fn from_string(s: String) -> Self {
        let boxed = s.into_bytes().into_boxed_slice();
        let len = boxed.len();
        let ptr = Box::into_raw(boxed) as *mut u8;
        Self { ptr, len }
    }
}

/// Free a Rust-owned buffer previously returned by any shim call.
/// Null/empty buffers are tolerated.
#[no_mangle]
pub extern "C" fn slate_buf_free(buf: SlateBuf) {
    if !buf.ptr.is_null() && buf.len > 0 {
        unsafe {
            drop(Box::from_raw(std::slice::from_raw_parts_mut(buf.ptr, buf.len)));
        }
    }
}

/// Error out-param: `code` is a SLATE_ERR_* constant, `message` is the
/// Display rendering (must be freed by the caller when code != 0).
#[repr(C)]
pub struct SlateError {
    pub code: i32,
    pub message: SlateBuf,
}

unsafe fn fill_error(out: *mut SlateError, code: i32, message: String) {
    if !out.is_null() {
        unsafe {
            (*out).code = code;
            (*out).message = SlateBuf::from_string(message);
        }
    }
}

unsafe fn clear_error(out: *mut SlateError) {
    if !out.is_null() {
        unsafe {
            (*out).code = SLATE_OK;
            (*out).message = SlateBuf::empty();
        }
    }
}

/// Rebuild a &str from (ptr,len). Invalid UTF-8 or a null ptr with a
/// nonzero len is a caller contract violation surfaced as BAD_ARG.
unsafe fn str_arg<'a>(ptr: *const u8, len: usize) -> Result<&'a str, i32> {
    if len == 0 {
        return Ok("");
    }
    if ptr.is_null() {
        return Err(SLATE_ERR_BAD_ARG);
    }
    let bytes = unsafe { std::slice::from_raw_parts(ptr, len) };
    std::str::from_utf8(bytes).map_err(|_| SLATE_ERR_BAD_ARG)
}

/// C# context pointer smuggled through Send+Sync so listener structs can
/// hold it. The safety burden ("the GCHandle target tolerates calls from
/// any thread") moves to the C# author — uniffi proves the equivalent
/// with its generated handle map.
#[derive(Clone, Copy)]
struct CtxPtr(*mut c_void);
unsafe impl Send for CtxPtr {}
unsafe impl Sync for CtxPtr {}

/// Panic fence: extern "C" unwinding is UB, so every boundary fn runs
/// its body inside catch_unwind (what uniffi's scaffolding emits per
/// export automatically).
fn fenced<F: FnOnce() -> i32>(body: F) -> i32 {
    match catch_unwind(AssertUnwindSafe(body)) {
        Ok(code) => code,
        Err(_) => SLATE_ERR_PANIC,
    }
}

// ---------------------------------------------------------------------
// CancelToken
// ---------------------------------------------------------------------

pub struct ShimCancelToken {
    inner: core::CancelToken,
}

#[no_mangle]
pub extern "C" fn slate_cancel_new() -> *mut ShimCancelToken {
    Box::into_raw(Box::new(ShimCancelToken { inner: core::CancelToken::new() }))
}

#[no_mangle]
pub extern "C" fn slate_cancel_cancel(token: *mut ShimCancelToken) {
    if let Some(t) = unsafe { token.as_ref() } {
        t.inner.cancel();
    }
}

#[no_mangle]
pub extern "C" fn slate_cancel_is_cancelled(token: *mut ShimCancelToken) -> i32 {
    match unsafe { token.as_ref() } {
        Some(t) => t.inner.is_cancelled() as i32,
        None => 0,
    }
}

#[no_mangle]
pub extern "C" fn slate_cancel_free(token: *mut ShimCancelToken) {
    if !token.is_null() {
        drop(unsafe { Box::from_raw(token) });
    }
}

// ---------------------------------------------------------------------
// VaultSession: open/close, scan with progress, events, save/read
// ---------------------------------------------------------------------

pub struct ShimVaultSession {
    inner: core::VaultSession,
}

#[no_mangle]
pub extern "C" fn slate_vault_open(
    root_ptr: *const u8,
    root_len: usize,
    out_session: *mut *mut ShimVaultSession,
    err: *mut SlateError,
) -> i32 {
    fenced(|| {
        unsafe { clear_error(err) };
        if out_session.is_null() {
            return SLATE_ERR_BAD_ARG;
        }
        let root = match unsafe { str_arg(root_ptr, root_len) } {
            Ok(s) => s,
            Err(code) => return code,
        };
        match core::VaultSession::from_filesystem(std::path::PathBuf::from(root)) {
            Ok(inner) => {
                unsafe {
                    *out_session = Box::into_raw(Box::new(ShimVaultSession { inner }));
                }
                SLATE_OK
            }
            Err(e) => {
                let code = vault_error_code(&e);
                unsafe { fill_error(err, code, e.to_string()) };
                code
            }
        }
    })
}

#[no_mangle]
pub extern "C" fn slate_vault_close(session: *mut ShimVaultSession) {
    if !session.is_null() {
        drop(unsafe { Box::from_raw(session) });
    }
}

// Scan-progress events flattened onto one callback:
//   tag 1 Started        (a = total_files)
//   tag 2 FileIndexed    (str = path, a = indexed, b = total)
//   tag 3 Finished       (a = files_seen, b = files_indexed)
//   tag 4 Cancelled
//   tag 5 Failed         (str = message)
pub const SCAN_EVT_STARTED: u32 = 1;
pub const SCAN_EVT_FILE_INDEXED: u32 = 2;
pub const SCAN_EVT_FINISHED: u32 = 3;
pub const SCAN_EVT_CANCELLED: u32 = 4;
pub const SCAN_EVT_FAILED: u32 = 5;

pub type ScanProgressCallback = extern "C" fn(
    ctx: *mut c_void,
    tag: u32,
    str_ptr: *const u8,
    str_len: usize,
    a: u64,
    b: u64,
);

struct ShimProgressListener {
    cb: ScanProgressCallback,
    ctx: CtxPtr,
}

impl core::ScanProgressListener for ShimProgressListener {
    fn on_progress(&self, event: core::ScanProgress) {
        let (tag, s, a, b) = match event {
            core::ScanProgress::Started { total_files } => (SCAN_EVT_STARTED, None, total_files, 0),
            core::ScanProgress::FileIndexed { path, indexed, total } => {
                (SCAN_EVT_FILE_INDEXED, Some(path), indexed, total)
            }
            core::ScanProgress::Finished { report } => {
                (SCAN_EVT_FINISHED, None, report.files_seen, report.files_indexed)
            }
            core::ScanProgress::Cancelled => (SCAN_EVT_CANCELLED, None, 0, 0),
            core::ScanProgress::Failed { message } => (SCAN_EVT_FAILED, Some(message), 0, 0),
        };
        let (ptr, len) = match &s {
            Some(text) => (text.as_ptr(), text.len()),
            None => (std::ptr::null(), 0),
        };
        (self.cb)(self.ctx.0, tag, ptr, len, a, b);
    }
}

#[no_mangle]
pub extern "C" fn slate_vault_scan_with_progress(
    session: *mut ShimVaultSession,
    cancel: *mut ShimCancelToken,
    cb: ScanProgressCallback,
    ctx: *mut c_void,
    out_files_seen: *mut u64,
    out_files_indexed: *mut u64,
    err: *mut SlateError,
) -> i32 {
    fenced(|| {
        unsafe { clear_error(err) };
        let (Some(session), Some(cancel)) = (unsafe { session.as_ref() }, unsafe { cancel.as_ref() })
        else {
            return SLATE_ERR_BAD_ARG;
        };
        let listener: Arc<dyn core::ScanProgressListener> =
            Arc::new(ShimProgressListener { cb, ctx: CtxPtr(ctx) });
        match session.inner.scan_initial_with_progress(&cancel.inner, Some(listener)) {
            Ok(report) => {
                unsafe {
                    if !out_files_seen.is_null() {
                        *out_files_seen = report.files_seen;
                    }
                    if !out_files_indexed.is_null() {
                        *out_files_indexed = report.files_indexed;
                    }
                }
                SLATE_OK
            }
            Err(e) => {
                let code = vault_error_code(&e);
                unsafe { fill_error(err, code, e.to_string()) };
                code
            }
        }
    })
}

// Vault events: three separate fn pointers, matching the trait's three
// methods. file-change kinds and index phases are u32 discriminants.
pub type VaultErrorCallback = extern "C" fn(
    ctx: *mut c_void,
    code: u32,
    path_ptr: *const u8,
    path_len: usize,
    msg_ptr: *const u8,
    msg_len: usize,
);
pub type FileChangeCallback = extern "C" fn(
    ctx: *mut c_void,
    kind: u32,
    path_ptr: *const u8,
    path_len: usize,
    prev_ptr: *const u8,
    prev_len: usize,
);
pub type IndexPhaseCallback = extern "C" fn(ctx: *mut c_void, phase: u32, files_seen: u64);

struct ShimEventListener {
    on_error: VaultErrorCallback,
    on_file_change: FileChangeCallback,
    on_index_phase: IndexPhaseCallback,
    ctx: CtxPtr,
}

impl core::VaultEventListener for ShimEventListener {
    fn on_error(&self, code: core::EventErrorCode, path: String, message: String) {
        let code = match code {
            core::EventErrorCode::CompactionFailed => 1u32,
        };
        (self.on_error)(
            self.ctx.0,
            code,
            path.as_ptr(),
            path.len(),
            message.as_ptr(),
            message.len(),
        );
    }

    fn on_file_change(&self, event: core::FileChangeEvent) {
        let kind = match event.kind {
            core::FileChangeKind::Created => 1u32,
            core::FileChangeKind::Modified => 2,
            core::FileChangeKind::Deleted => 3,
            core::FileChangeKind::Renamed => 4,
        };
        let (prev_ptr, prev_len) = match &event.previous_path {
            Some(p) => (p.as_ptr(), p.len()),
            None => (std::ptr::null(), 0),
        };
        (self.on_file_change)(
            self.ctx.0,
            kind,
            event.path.as_ptr(),
            event.path.len(),
            prev_ptr,
            prev_len,
        );
    }

    fn on_index_phase(&self, phase: core::IndexPhase, files_seen: u64) {
        let phase = match phase {
            core::IndexPhase::ScanStarted => 1u32,
            core::IndexPhase::ReconcileStarted => 2,
            core::IndexPhase::ReconcileFinished => 3,
            core::IndexPhase::ScanFinished => 4,
        };
        (self.on_index_phase)(self.ctx.0, phase, files_seen);
    }
}

#[no_mangle]
pub extern "C" fn slate_vault_register_events(
    session: *mut ShimVaultSession,
    on_error: VaultErrorCallback,
    on_file_change: FileChangeCallback,
    on_index_phase: IndexPhaseCallback,
    ctx: *mut c_void,
) -> u64 {
    let Some(session) = (unsafe { session.as_ref() }) else {
        return 0;
    };
    let listener: Arc<dyn core::VaultEventListener> = Arc::new(ShimEventListener {
        on_error,
        on_file_change,
        on_index_phase,
        ctx: CtxPtr(ctx),
    });
    session.inner.register_event_listener(listener)
}

#[no_mangle]
pub extern "C" fn slate_vault_unregister_events(session: *mut ShimVaultSession, token: u64) {
    if let Some(session) = unsafe { session.as_ref() } {
        session.inner.unregister_event_listener(token);
    }
}

#[no_mangle]
pub extern "C" fn slate_vault_save_text(
    session: *mut ShimVaultSession,
    path_ptr: *const u8,
    path_len: usize,
    contents_ptr: *const u8,
    contents_len: usize,
    expected_hash_ptr: *const u8,
    expected_hash_len: usize, // 0 = unconditional save
    out_new_hash: *mut SlateBuf,
    out_new_mtime_ms: *mut i64,
    wc_current_hash: *mut SlateBuf, // WriteConflict only
    wc_current_mtime_ms: *mut i64,  // WriteConflict only
    err: *mut SlateError,
) -> i32 {
    fenced(|| {
        unsafe { clear_error(err) };
        let Some(session) = (unsafe { session.as_ref() }) else {
            return SLATE_ERR_BAD_ARG;
        };
        let (path, contents) = match (unsafe { str_arg(path_ptr, path_len) }, unsafe {
            str_arg(contents_ptr, contents_len)
        }) {
            (Ok(p), Ok(c)) => (p, c),
            _ => return SLATE_ERR_BAD_ARG,
        };
        let expected = if expected_hash_len == 0 {
            None
        } else {
            match unsafe { str_arg(expected_hash_ptr, expected_hash_len) } {
                Ok(h) => Some(h),
                Err(code) => return code,
            }
        };
        match session.inner.save_text(path, contents, expected) {
            Ok(report) => {
                unsafe {
                    if !out_new_hash.is_null() {
                        *out_new_hash = SlateBuf::from_string(report.new_content_hash);
                    }
                    if !out_new_mtime_ms.is_null() {
                        *out_new_mtime_ms = report.new_mtime_ms;
                    }
                }
                SLATE_OK
            }
            Err(e) => {
                // WriteConflict's typed fields survive only because these
                // two out-params were hand-plumbed; every other variant
                // collapses to (code, display-string).
                if let core::VaultError::WriteConflict {
                    current_content_hash,
                    current_mtime_ms,
                    ..
                } = &e
                {
                    unsafe {
                        if !wc_current_hash.is_null() {
                            *wc_current_hash = SlateBuf::from_string(current_content_hash.clone());
                        }
                        if !wc_current_mtime_ms.is_null() {
                            *wc_current_mtime_ms = *current_mtime_ms;
                        }
                    }
                }
                let code = vault_error_code(&e);
                unsafe { fill_error(err, code, e.to_string()) };
                code
            }
        }
    })
}

#[no_mangle]
pub extern "C" fn slate_vault_read_text(
    session: *mut ShimVaultSession,
    path_ptr: *const u8,
    path_len: usize,
    out_text: *mut SlateBuf,
    err: *mut SlateError,
) -> i32 {
    fenced(|| {
        unsafe { clear_error(err) };
        let Some(session) = (unsafe { session.as_ref() }) else {
            return SLATE_ERR_BAD_ARG;
        };
        let path = match unsafe { str_arg(path_ptr, path_len) } {
            Ok(p) => p,
            Err(code) => return code,
        };
        match session.inner.read_text(path) {
            Ok(text) => {
                unsafe {
                    if !out_text.is_null() {
                        *out_text = SlateBuf::from_string(text);
                    }
                }
                SLATE_OK
            }
            Err(e) => {
                let code = vault_error_code(&e);
                unsafe { fill_error(err, code, e.to_string()) };
                code
            }
        }
    })
}

// ---------------------------------------------------------------------
// DocumentBuffer (keystroke hot path)
// ---------------------------------------------------------------------

pub struct ShimDocBuffer {
    // Same locking rationale as slate-uniffi's DocumentBuffer: nothing
    // serializes concurrent boundary calls for us.
    inner: Mutex<core::doc_buffer::DocBufferState>,
}

#[repr(C)]
pub struct SlateSpan {
    pub start_byte: u32,
    pub end_byte: u32,
    /// Flattened [`core::editor_spans::EditorSpanKind`] discriminant.
    pub kind: u32,
    /// Variant payload: heading level for kind 1; 0 elsewhere. The
    /// nested `Code(TokenKind)` detail is *collapsed* here — carrying it
    /// faithfully means flattening a second enum by hand (uniffi lifts
    /// the whole nested shape for free).
    pub arg: u32,
}

fn flatten_span_kind(kind: &core::editor_spans::EditorSpanKind) -> (u32, u32) {
    use core::editor_spans::EditorSpanKind as K;
    match kind {
        K::Heading(level) => (1, *level as u32),
        K::Emphasis => (2, 0),
        K::Strong => (3, 0),
        K::Strikethrough => (4, 0),
        K::InlineCode => (5, 0),
        K::CodeFence => (6, 0),
        K::Link => (7, 0),
        K::Image => (8, 0),
        K::BlockQuote => (9, 0),
        K::Wikilink => (10, 0),
        K::Embed => (11, 0),
        K::Tag => (12, 0),
        K::Citation => (13, 0),
        K::Comment => (14, 0),
        K::Frontmatter => (15, 0),
        K::Code(_) => (16, 0),
    }
}

#[no_mangle]
pub extern "C" fn slate_doc_new(text_ptr: *const u8, text_len: usize) -> *mut ShimDocBuffer {
    let Ok(text) = (unsafe { str_arg(text_ptr, text_len) }) else {
        return std::ptr::null_mut();
    };
    Box::into_raw(Box::new(ShimDocBuffer {
        inner: Mutex::new(core::doc_buffer::DocBufferState::new(text)),
    }))
}

#[no_mangle]
pub extern "C" fn slate_doc_apply_edit(
    doc: *mut ShimDocBuffer,
    start_utf16: u32,
    old_len_utf16: u32,
    text_ptr: *const u8,
    text_len: usize,
) -> i32 {
    fenced(|| {
        let Some(doc) = (unsafe { doc.as_ref() }) else {
            return SLATE_ERR_BAD_ARG;
        };
        let Ok(text) = (unsafe { str_arg(text_ptr, text_len) }) else {
            return SLATE_ERR_BAD_ARG;
        };
        doc.inner
            .lock()
            .unwrap()
            .apply_edit(start_utf16 as usize, old_len_utf16 as usize, text);
        SLATE_OK
    })
}

#[no_mangle]
pub extern "C" fn slate_doc_reset(
    doc: *mut ShimDocBuffer,
    text_ptr: *const u8,
    text_len: usize,
) -> i32 {
    fenced(|| {
        let Some(doc) = (unsafe { doc.as_ref() }) else {
            return SLATE_ERR_BAD_ARG;
        };
        let Ok(text) = (unsafe { str_arg(text_ptr, text_len) }) else {
            return SLATE_ERR_BAD_ARG;
        };
        doc.inner.lock().unwrap().reset(text);
        SLATE_OK
    })
}

#[no_mangle]
pub extern "C" fn slate_doc_len_utf16(doc: *mut ShimDocBuffer) -> u32 {
    match unsafe { doc.as_ref() } {
        Some(d) => d.inner.lock().unwrap().len_utf16() as u32,
        None => 0,
    }
}

#[no_mangle]
pub extern "C" fn slate_doc_byte_to_utf16(doc: *mut ShimDocBuffer, byte: u32) -> u32 {
    match unsafe { doc.as_ref() } {
        Some(d) => d.inner.lock().unwrap().byte_to_utf16(byte as usize) as u32,
        None => 0,
    }
}

/// Windowed highlight. On success the span array is Rust-owned; free it
/// with `slate_spans_free(ptr, len)` exactly once.
#[no_mangle]
pub extern "C" fn slate_doc_highlight(
    doc: *mut ShimDocBuffer,
    dirty_start_utf16: u32,
    dirty_end_utf16: u32,
    out_applied_start: *mut u32,
    out_applied_end: *mut u32,
    out_spans: *mut *mut SlateSpan,
    out_span_count: *mut usize,
) -> i32 {
    fenced(|| {
        let Some(doc) = (unsafe { doc.as_ref() }) else {
            return SLATE_ERR_BAD_ARG;
        };
        if out_spans.is_null() || out_span_count.is_null() {
            return SLATE_ERR_BAD_ARG;
        }
        let snapshot = doc.inner.lock().unwrap().clone();
        let ranged =
            snapshot.highlight_in_range(dirty_start_utf16 as usize, dirty_end_utf16 as usize);
        let spans: Vec<SlateSpan> = ranged
            .spans
            .into_iter()
            .map(|s| {
                let (kind, arg) = flatten_span_kind(&s.kind);
                SlateSpan { start_byte: s.start_byte, end_byte: s.end_byte, kind, arg }
            })
            .collect();
        let count = spans.len();
        let ptr = Box::into_raw(spans.into_boxed_slice()) as *mut SlateSpan;
        unsafe {
            if !out_applied_start.is_null() {
                *out_applied_start = ranged.applied_range.start as u32;
            }
            if !out_applied_end.is_null() {
                *out_applied_end = ranged.applied_range.end as u32;
            }
            *out_spans = ptr;
            *out_span_count = count;
        }
        SLATE_OK
    })
}

#[no_mangle]
pub extern "C" fn slate_spans_free(spans: *mut SlateSpan, count: usize) {
    if !spans.is_null() && count > 0 {
        unsafe {
            drop(Box::from_raw(std::slice::from_raw_parts_mut(spans, count)));
        }
    }
}

#[no_mangle]
pub extern "C" fn slate_doc_free(doc: *mut ShimDocBuffer) {
    if !doc.is_null() {
        drop(unsafe { Box::from_raw(doc) });
    }
}

// ---------------------------------------------------------------------
// CommandRegistry + foreign CommandAction
// ---------------------------------------------------------------------

pub struct ShimCommandRegistry {
    inner: core::CommandRegistry,
}

/// Foreign action callback. Returns 0 for success; any nonzero return is
/// ActionFailed, with the failure message copied into `msg_out`
/// (capacity `msg_cap` bytes; write the used byte count to `msg_len`).
/// The fixed buffer sidesteps a cross-allocator ownership dance — a
/// deliberate contract simplification vs uniffi's lifted strings.
pub type CommandInvokeCallback = extern "C" fn(
    ctx: *mut c_void,
    msg_out: *mut u8,
    msg_cap: usize,
    msg_len: *mut usize,
) -> i32;

struct ShimCommandAction {
    cb: CommandInvokeCallback,
    ctx: CtxPtr,
}

const ACTION_MSG_CAP: usize = 1024;

impl core::CommandAction for ShimCommandAction {
    fn invoke(&self) -> Result<(), core::CommandError> {
        let mut buf = [0u8; ACTION_MSG_CAP];
        let mut used: usize = 0;
        let status = (self.cb)(self.ctx.0, buf.as_mut_ptr(), ACTION_MSG_CAP, &mut used);
        if status == 0 {
            return Ok(());
        }
        let used = used.min(ACTION_MSG_CAP);
        let message = String::from_utf8_lossy(&buf[..used]).into_owned();
        Err(core::CommandError::ActionFailed(message))
    }
}

#[no_mangle]
pub extern "C" fn slate_registry_new() -> *mut ShimCommandRegistry {
    Box::into_raw(Box::new(ShimCommandRegistry { inner: core::CommandRegistry::new() }))
}

fn command_section(section: u32) -> core::CommandSection {
    match section {
        0 => core::CommandSection::File,
        1 => core::CommandSection::Navigation,
        2 => core::CommandSection::View,
        3 => core::CommandSection::Vault,
        4 => core::CommandSection::Editor,
        5 => core::CommandSection::Tasks,
        6 => core::CommandSection::Settings,
        7 => core::CommandSection::Plugins,
        8 => core::CommandSection::Canvas,
        10 => core::CommandSection::Bases,
        11 => core::CommandSection::Graph,
        12 => core::CommandSection::Sidebar,
        _ => core::CommandSection::File,
    }
}

/// Returns 1 when the registration replaced an existing id, 0 for a
/// fresh registration, negative on bad args.
#[no_mangle]
pub extern "C" fn slate_registry_register(
    registry: *mut ShimCommandRegistry,
    id_ptr: *const u8,
    id_len: usize,
    label_ptr: *const u8,
    label_len: usize,
    section: u32,
    cb: CommandInvokeCallback,
    ctx: *mut c_void,
) -> i32 {
    fenced(|| {
        let Some(registry) = (unsafe { registry.as_ref() }) else {
            return SLATE_ERR_BAD_ARG;
        };
        let (id, label) = match (unsafe { str_arg(id_ptr, id_len) }, unsafe {
            str_arg(label_ptr, label_len)
        }) {
            (Ok(i), Ok(l)) => (i, l),
            _ => return SLATE_ERR_BAD_ARG,
        };
        let command = core::Command {
            id: id.to_owned(),
            label: label.to_owned(),
            accessibility_hint: None,
            hotkey_hint: None,
            section: command_section(section),
        };
        let replaced = registry
            .inner
            .register(command, Arc::new(ShimCommandAction { cb, ctx: CtxPtr(ctx) }));
        replaced as i32
    })
}

#[no_mangle]
pub extern "C" fn slate_registry_unregister(
    registry: *mut ShimCommandRegistry,
    id_ptr: *const u8,
    id_len: usize,
) -> i32 {
    fenced(|| {
        let Some(registry) = (unsafe { registry.as_ref() }) else {
            return SLATE_ERR_BAD_ARG;
        };
        let Ok(id) = (unsafe { str_arg(id_ptr, id_len) }) else {
            return SLATE_ERR_BAD_ARG;
        };
        registry.inner.unregister(id) as i32
    })
}

#[no_mangle]
pub extern "C" fn slate_registry_count(registry: *mut ShimCommandRegistry) -> usize {
    match unsafe { registry.as_ref() } {
        Some(r) => r.inner.list().len(),
        None => 0,
    }
}

#[no_mangle]
pub extern "C" fn slate_registry_invoke(
    registry: *mut ShimCommandRegistry,
    id_ptr: *const u8,
    id_len: usize,
    err: *mut SlateError,
) -> i32 {
    fenced(|| {
        unsafe { clear_error(err) };
        let Some(registry) = (unsafe { registry.as_ref() }) else {
            return SLATE_ERR_BAD_ARG;
        };
        let Ok(id) = (unsafe { str_arg(id_ptr, id_len) }) else {
            return SLATE_ERR_BAD_ARG;
        };
        match registry.inner.invoke_by_id(id) {
            Ok(()) => SLATE_OK,
            Err(core::CommandError::UnknownId(id)) => {
                unsafe { fill_error(err, SLATE_ERR_COMMAND_UNKNOWN_ID, id) };
                SLATE_ERR_COMMAND_UNKNOWN_ID
            }
            Err(core::CommandError::ActionFailed(message)) => {
                unsafe { fill_error(err, SLATE_ERR_COMMAND_ACTION_FAILED, message) };
                SLATE_ERR_COMMAND_ACTION_FAILED
            }
        }
    })
}

#[no_mangle]
pub extern "C" fn slate_registry_free(registry: *mut ShimCommandRegistry) {
    if !registry.is_null() {
        drop(unsafe { Box::from_raw(registry) });
    }
}
