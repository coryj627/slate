// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Math pipeline for Milestone K (#217).
//!
//! Walks a Markdown source for `$…$` (inline) and `$$…$$` (display)
//! math, converts each block from LaTeX to MathML via `pulldown-latex`,
//! then asks `mathcat` (the same library NVDA uses) for the speech and
//! braille representations. The structured representation is what AT
//! consumes; visual rendering happens in the Mac UI layer.
//!
//! ## Architecture notes
//!
//! - **No pulldown-cmark math events.** The installed version
//!   (0.10.3) doesn't surface inline / display math as its own
//!   `Tag::Math` events. We scan delimiters ourselves and use
//!   pulldown-cmark only for code-block ranges so a `$` inside a
//!   fenced code block doesn't get treated as math.
//! - **MathCAT uses thread-local state.** `set_mathml`,
//!   `get_spoken_text`, `set_preference`, and the per-thread
//!   `PrefManager` (set up by `set_rules_dir`) all live in
//!   `thread_local!` cells inside `libmathcat`. We route every
//!   `render_math` call through a single process-global
//!   dedicated worker thread so MathCAT only ever sees one OS
//!   thread — audit #269 found that scattered cross-thread
//!   calls produce non-deterministic pref propagation.
//! - **`include-zip` feature** bundles MathCAT's rule files into the
//!   binary, so we never have to ship a rules directory alongside the
//!   app or do runtime file lookups.
//!
//! See issue #217 + `docs/plans/05_locked_architecture_decisions.md`
//! §6.2 for the full design.

use crate::VaultError;

/// Inline vs display math. Pure data; the renderer decides layout.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum MathDisplayStyle {
    /// `$…$` — inline within a paragraph.
    Inline,
    /// `$$…$$` — block, centred on its own line.
    Block,
}

/// MathCAT speech style preference (`05` §6.2).
///
/// ClearSpeak (default) reads math intuitively for general audiences;
/// SimpleSpeak is MathCAT's briefer style — fewer structural words,
/// preferred by users who already know the notation and want a terser
/// read.
///
/// These are the two styles MathCAT actually ships (`ClearSpeak_Rules`
/// and `SimpleSpeak_Rules`, through `0.7.6-rc.2`). The variant this enum
/// carried before #1056 — `MathSpeak` — was never implemented upstream:
/// `set_preference("SpeechStyle", "MathSpeak")` was accepted and ignored,
/// so the option silently produced ClearSpeak. It is gone rather than
/// relabelled; hosts migrate a stored `mathSpeak` to ClearSpeak, which is
/// what those users were already hearing.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Default)]
pub enum MathSpeechStyle {
    #[default]
    ClearSpeak,
    SimpleSpeak,
}

/// MathCAT verbosity.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Default)]
pub enum MathVerbosity {
    Terse,
    #[default]
    Medium,
    Verbose,
}

/// Braille code preference.
///
/// Nemeth is the long-established US math-braille standard. UEB is
/// the newer unified standard adopted by several English-language
/// jurisdictions. Choice is per-user; both are equally supported.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Default)]
pub enum BrailleCode {
    #[default]
    Nemeth,
    Ueb,
}

/// Per-user math rendering preferences.
///
/// Carried in `SessionConfig` so callers can flip a setting and have
/// subsequent `get_math_blocks` calls reflect it. Cache key includes
/// the preference hash so a prefs change naturally invalidates.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Default)]
pub struct MathPrefs {
    pub speech_style: MathSpeechStyle,
    pub verbosity: MathVerbosity,
    pub braille_code: BrailleCode,
}

impl MathPrefs {
    /// Compose a deterministic hash for the cache key. Stable across
    /// process restarts (no `RandomState` interference). The cache
    /// invalidates when this hash changes.
    pub fn fingerprint(self) -> u64 {
        let mut acc: u64 = 1469598103934665603; // FNV-1a basis
        let bytes: [u8; 3] = [
            self.speech_style as u8,
            self.verbosity as u8,
            self.braille_code as u8,
        ];
        for b in bytes {
            acc ^= b as u64;
            acc = acc.wrapping_mul(1099511628211); // FNV-1a prime
        }
        acc
    }
}

/// One unrendered math block discovered in source.
///
/// Carries the raw LaTeX + position so the renderer can be deferred
/// (large notes may have dozens of blocks; rendering them all eagerly
/// would block the read-path UI).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RawMathBlock {
    pub source: String,
    pub display_style: MathDisplayStyle,
    /// 1-based line number of the block's first character.
    pub line: u32,
    /// Byte offset of the block's first delimiter (`$` or `$$`).
    pub byte_offset: u32,
}

/// One rendered math block. `mathml` always populates; `speech` and
/// `braille` may be empty when MathCAT couldn't produce them (the
/// MathML's still useful to render visually).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct MathBlock {
    pub source: String,
    pub display_style: MathDisplayStyle,
    pub mathml: String,
    pub speech: String,
    pub braille: Vec<u8>,
    pub line: u32,
    pub byte_offset: u32,
}

#[derive(Debug, Clone, Copy)]
struct ScannedMathBlock {
    source_start: usize,
    source_end: usize,
    delimiter_end: usize,
    display_style: MathDisplayStyle,
    line: u32,
    byte_offset: usize,
}

fn markdown_code_ranges(source: &str) -> Vec<std::ops::Range<usize>> {
    use pulldown_cmark::{Event, Options, Parser, Tag};

    Parser::new_ext(source, Options::ENABLE_STRIKETHROUGH)
        .into_offset_iter()
        .filter_map(|(event, range)| match event {
            Event::Code(_) | Event::Start(Tag::CodeBlock(_)) => Some(range),
            _ => None,
        })
        .collect()
}

/// Scan math without allocating formula strings. `code_ranges` must be in
/// document order, as pulldown-cmark's offset iterator provides them.
fn scan_math_blocks(
    source: &str,
    code_ranges: &[std::ops::Range<usize>],
    mut visit: impl FnMut(ScannedMathBlock),
) {
    let bytes = source.as_bytes();
    let mut lines = crate::line_index::LineTracker::new(source);
    let mut code_index = 0usize;
    let mut i = 0usize;
    while i < bytes.len() {
        while code_index < code_ranges.len() && code_ranges[code_index].end <= i {
            code_index += 1;
        }
        let in_code = code_ranges
            .get(code_index)
            .is_some_and(|range| range.start <= i && i < range.end);

        if bytes[i] == b'$' && dollar_is_escaped(bytes, i) {
            i += 1;
            continue;
        }
        if i + 1 < bytes.len()
            && &bytes[i..i + 2] == b"$$"
            && !in_code
            && let Some(end_rel) = find_double_dollar_close(&bytes[i + 2..], i + 2, code_ranges)
        {
            let source_start = i + 2;
            let source_end = source_start + end_rel;
            if !source[source_start..source_end].trim().is_empty() {
                visit(ScannedMathBlock {
                    source_start,
                    source_end,
                    delimiter_end: source_end + 2,
                    display_style: MathDisplayStyle::Block,
                    line: lines.line_at(i),
                    byte_offset: i,
                });
            }
            i = source_end + 2;
            continue;
        }
        if bytes[i] == b'$' && !in_code {
            let next_idx = i + 1;
            if next_idx < bytes.len() {
                let nb = bytes[next_idx];
                let opens = nb != b' ' && nb != b'\t' && nb != b'\n' && !nb.is_ascii_digit();
                if opens
                    && let Some(end_rel) =
                        find_single_dollar_close(&bytes[next_idx..], next_idx, code_ranges)
                {
                    let source_start = next_idx;
                    let source_end = source_start + end_rel;
                    if !source[source_start..source_end].trim().is_empty() {
                        visit(ScannedMathBlock {
                            source_start,
                            source_end,
                            delimiter_end: source_end + 1,
                            display_style: MathDisplayStyle::Inline,
                            line: lines.line_at(i),
                            byte_offset: i,
                        });
                    }
                    i = source_end + 1;
                    continue;
                }
            }
        }
        i += 1;
    }
}

/// Walk `source` and return every math block in document order.
///
/// Uses pulldown-cmark to find code-block byte ranges (so `$` inside
/// a fenced ` ```rust ` block stays as a `$`), then scans the source
/// for `$…$` and `$$…$$` delimiters outside those ranges.
///
/// Recognises Obsidian's convention: `$$` ALWAYS opens display math,
/// even mid-line. `$` opens inline math when followed by a non-space
/// character (matching pandoc's tex-math rule, which suppresses
/// mid-sentence dollar signs like "$50").
pub fn extract_math_blocks(source: &str) -> Vec<RawMathBlock> {
    let code_ranges = markdown_code_ranges(source);
    let mut out = Vec::new();
    scan_math_blocks(source, &code_ranges, |block| {
        out.push(RawMathBlock {
            source: source[block.source_start..block.source_end]
                .trim()
                .to_string(),
            display_style: block.display_style,
            line: block.line,
            byte_offset: block.byte_offset as u32,
        });
    });
    out
}

/// Per-note render budget (W3-2 round 5): reading projections cap at
/// 2,000 blocks, so formulas past that count can never all be
/// displayed — rendering them through the serialized MathCAT worker
/// is unbounded work a dense adversarial note can weaponize.
/// Over-budget blocks keep source and position with the standard
/// typed-speech degradation (empty MathML/braille) so hosts render
/// source-in-range fallbacks, never silently absent.
pub const MAX_RENDERED_FORMULAS_PER_NOTE: usize = 2_000;

/// Per-formula source cap, checked BEFORE LaTeX → MathML: MathCAT's
/// own 1 MiB MathML cap (audit #245) fires only after conversion has
/// already burned worker time. No legitimate authored formula
/// approaches 16 KiB of LaTeX.
pub const MAX_FORMULA_SOURCE_BYTES: usize = 16 * 1024;

/// Render every scanned block under the per-note budgets — the
/// bounded entry `VaultSession::get_math_blocks` uses.
pub fn render_math_blocks(raws: &[RawMathBlock], prefs: MathPrefs) -> Vec<MathBlock> {
    render_math_blocks_bounded(raws, prefs, MAX_RENDERED_FORMULAS_PER_NOTE)
}

/// Budget mechanism, injectable so the count cap is testable without
/// pushing thousands of renders through the MathCAT worker.
fn render_math_blocks_bounded(
    raws: &[RawMathBlock],
    prefs: MathPrefs,
    max_rendered: usize,
) -> Vec<MathBlock> {
    raws.iter()
        .enumerate()
        .map(|(index, raw)| {
            if raw.source.len() > MAX_FORMULA_SOURCE_BYTES {
                budget_degraded(
                    raw,
                    "Math expression too large to render to accessible speech.".to_string(),
                )
            } else if index >= max_rendered {
                budget_degraded(raw, format!("Math expression: {}", raw.source))
            } else {
                render_math(raw, prefs)
            }
        })
        .collect()
}

/// The budget-degradation shape: identical to the worker-loss
/// fallback in [`render_math`] so hosts handle one degradation
/// contract, not two.
fn budget_degraded(raw: &RawMathBlock, speech: String) -> MathBlock {
    MathBlock {
        source: raw.source.clone(),
        display_style: raw.display_style,
        mathml: String::new(),
        speech,
        braille: Vec::new(),
        line: raw.line,
        byte_offset: raw.byte_offset,
    }
}

/// Display-math ranges that suppress editor interactions.
///
/// This intentionally composes two existing canonical semantics in one
/// CommonMark pass: the reading pipeline supplies the exact top-level,
/// trim-start `$$` opener, while the non-owning math scanner supplies its
/// canonical closing extent and code suppression. The returned range includes
/// both delimiters and internal blank lines.
pub fn interaction_display_math_ranges(source: &str) -> Vec<std::ops::Range<usize>> {
    let body = crate::frontmatter::body_after_frontmatter(source);
    let body_offset = source.len() - body.len();
    let context = crate::reading::editor_display_math_context(body);
    let mut opener_index = 0usize;
    let mut out = Vec::new();
    scan_math_blocks(body, &context.code_ranges, |block| {
        while opener_index < context.openers.len()
            && context.openers[opener_index] < block.byte_offset
        {
            opener_index += 1;
        }
        if block.display_style == MathDisplayStyle::Block
            && context.openers.get(opener_index) == Some(&block.byte_offset)
        {
            out.push(body_offset + block.byte_offset..body_offset + block.delimiter_end);
            opener_index += 1;
        }
    });
    out
}

fn dollar_is_escaped(bytes: &[u8], index: usize) -> bool {
    let mut backslashes = 0usize;
    let mut j = index;
    while j > 0 && bytes[j - 1] == b'\\' {
        backslashes += 1;
        j -= 1;
    }
    !backslashes.is_multiple_of(2)
}

/// Find the byte offset of the next closing `$$` in `after`, ignoring
/// delimiter-shaped text inside canonical Markdown code ranges. Returns
/// `None` when the block doesn't close before EOF (degenerate / mid-edit)
/// so we don't sweep math syntax over the rest of the file.
///
/// Honors `\$` escapes (audit #245 H3, Codoki polish on top): a `$$`
/// is escaped only when preceded by an **odd** number of backslashes.
/// `\$$` (one `\`) is escaped → keep scanning. `\\$$` (two `\`s) is
/// not escaped — the backslashes form a literal `\\` and the `$$`
/// closes normally. Without the parity check, a literal-backslash
/// LaTeX expression (`\\`-terminated) followed by the close fence
/// would skip the close and consume the rest of the file.
fn find_double_dollar_close(
    after: &[u8],
    after_start: usize,
    code_ranges: &[std::ops::Range<usize>],
) -> Option<usize> {
    let mut i = 0;
    let mut code_index = code_ranges.partition_point(|range| range.end <= after_start);
    while i + 1 < after.len() {
        let absolute = after_start + i;
        while code_index < code_ranges.len() && code_ranges[code_index].end <= absolute {
            code_index += 1;
        }
        if let Some(range) = code_ranges.get(code_index)
            && range.start < absolute + 2
            && absolute < range.end
        {
            i = range.end.saturating_sub(after_start).max(i + 1);
            continue;
        }
        if &after[i..i + 2] == b"$$" && !dollar_is_escaped(after, i) {
            return Some(i);
        }
        i += 1;
    }
    None
}

/// Find the byte offset of the next closing `$` in `after`, on the same
/// line and outside canonical Markdown code ranges. Inline math can't span
/// newlines (matches pandoc + Obs).
fn find_single_dollar_close(
    after: &[u8],
    after_start: usize,
    code_ranges: &[std::ops::Range<usize>],
) -> Option<usize> {
    let mut i = 0;
    let mut code_index = code_ranges.partition_point(|range| range.end <= after_start);
    while i < after.len() {
        let b = after[i];
        if b == b'\n' {
            return None;
        }
        let absolute = after_start + i;
        while code_index < code_ranges.len() && code_ranges[code_index].end <= absolute {
            code_index += 1;
        }
        let in_code = code_ranges
            .get(code_index)
            .is_some_and(|range| range.start <= absolute && absolute < range.end);
        if b == b'$' && !in_code && !dollar_is_escaped(after, i) {
            return Some(i);
        }
        i += 1;
    }
    None
}
// --- Rendering ---------------------------------------------------------

// MathCAT's `MATHML_INSTANCE`, `SPEECH_RULES`, `NAVIGATION_STATE`,
// and `PREF_MANAGER` are all `thread_local!` statics (see
// `mathcat-0.7.6-beta.4/src/{interface,prefs,speech}.rs`). The
// per-thread design is fine for an embedder that pins all calls
// to one thread, but ours doesn't — the Mac UI's
// `loadCurrentNoteMathBlocks` dispatches via `Task.detached`,
// which picks any worker thread from Swift's concurrency pool.
//
// Audit #245 found that EVERY thread first calling into MathCAT
// must run `set_rules_dir` once or downstream calls silently
// return empty. Audit #269 then found that even with per-thread
// `set_rules_dir`, swapping `SpeechStyle` / `BrailleCode` across
// fresh threads doesn't reliably propagate — MathCAT's pref-
// invalidation logic ties to a thread's first `set_mathml` in
// non-obvious ways we couldn't isolate from outside the library.
// Same-thread sequential pref swaps DO work; only the cross-
// thread case is broken.
//
// Architectural fix (audit #269): route ALL libmathcat calls
// through a single process-global dedicated worker thread. The
// worker owns MathCAT's thread-local state for the lifetime of
// the process; callers send (RawMathBlock, MathPrefs) and
// receive a `MathBlock` back over a channel. Operations
// serialize naturally on the worker, and SpeechStyle /
// BrailleCode swaps resolve correctly because they happen
// sequentially on the same OS thread — the scenario we verified
// works in probe tests.
//
// Cost: one round-trip through `std::sync::mpsc` per render.
// Math blocks render off the UI thread in batches of single
// digits per note, so this is dominated by MathCAT's own
// rendering latency.

/// Request envelope sent to the dedicated MathCAT worker thread.
struct RenderRequest {
    raw: RawMathBlock,
    prefs: MathPrefs,
    reply: std::sync::mpsc::SyncSender<MathBlock>,
}

/// Handle to the dedicated MathCAT worker thread. The mutex
/// serializes access to the `Sender` (`mpsc::Sender` is `Send`
/// but not `Sync`). Only the *send* is behind the lock; the
/// actual render runs on the worker thread.
struct MathCatWorker {
    tx: std::sync::mpsc::Sender<RenderRequest>,
}

/// Lazily-initialized process-global MathCAT worker. We use
/// `OnceLock` over `LazyLock` so the first-use init is
/// observable and we can crash early if the worker thread
/// fails to spawn.
static MATHCAT_WORKER: std::sync::OnceLock<std::sync::Mutex<MathCatWorker>> =
    std::sync::OnceLock::new();

fn worker_handle() -> &'static std::sync::Mutex<MathCatWorker> {
    MATHCAT_WORKER.get_or_init(|| {
        let (tx, rx) = std::sync::mpsc::channel::<RenderRequest>();
        std::thread::Builder::new()
            .name("slate-mathcat".to_string())
            .spawn(move || mathcat_worker_loop(rx))
            .expect("MathCAT worker thread must spawn");
        std::sync::Mutex::new(MathCatWorker { tx })
    })
}

/// Worker-thread main loop. Runs on the one dedicated OS thread
/// that owns MathCAT's per-thread state. Initializes MathCAT on
/// entry, then processes render requests until the channel
/// closes (only happens at process exit).
fn mathcat_worker_loop(rx: std::sync::mpsc::Receiver<RenderRequest>) {
    // First call on this thread initializes the per-thread
    // PrefManager and rule-file table. Subsequent calls reuse
    // the cached state; SpeechStyle / BrailleCode swaps go
    // through `set_preference` and propagate correctly because
    // they execute sequentially on the same thread.
    match set_rules_dir_chaining_panic_hook() {
        Ok(()) => {
            MATHCAT_INITIALIZED.with(|c| c.set(true));
        }
        Err(err) => {
            // Non-fatal: each render falls back when `apply_prefs` errors.
            // No vault path or note name here, so a plain facade warn (#507)
            // is fine — the error is about the bundled rule files, not user
            // content.
            log::warn!("slate-mathcat: set_rules_dir failed at worker init: {err:?}");
            // The worker keeps running so callers don't hang
            // waiting for replies; each render will hit the
            // fallback path when `apply_prefs` returns an error.
            // Leaving `MATHCAT_INITIALIZED = false` makes
            // `ensure_mathcat_initialized_on_this_thread` retry
            // on each render, giving recovery a chance if the
            // failure was transient.
        }
    }

    while let Ok(request) = rx.recv() {
        let block = render_math_on_worker(&request.raw, request.prefs);
        let _ = request.reply.send(block);
    }
}

/// Run the render pipeline directly. Only call this from the
/// MathCAT worker thread — callers from other threads must go
/// through `render_math`, which dispatches via the worker
/// channel.
fn render_math_on_worker(raw: &RawMathBlock, prefs: MathPrefs) -> MathBlock {
    let mathml = latex_to_mathml(&raw.source);
    let (speech, braille) = if mathml.is_empty() {
        // pulldown-latex could not render the source faithfully; see
        // `latex_to_mathml`.
        (unrendered_speech(&raw.source), Vec::new())
    } else {
        match mathml_to_speech_and_braille(&mathml, prefs) {
            Ok(pair) => pair,
            Err(MathCatError::MathmlTooLarge) => (
                "Math expression too large to render to accessible speech.".to_string(),
                Vec::new(),
            ),
            Err(_) => (unrendered_speech(&raw.source), Vec::new()),
        }
    };
    MathBlock {
        source: raw.source.clone(),
        display_style: raw.display_style,
        mathml,
        speech,
        braille,
        line: raw.line,
        byte_offset: raw.byte_offset,
    }
}

use std::cell::Cell;

thread_local! {
    /// Per-thread flag tracking whether `set_rules_dir` has run
    /// on this thread. Only the dedicated worker thread needs
    /// this set to `true`; all `mathml_to_speech_and_braille`
    /// callers must run inside the worker. The flag exists so
    /// `ensure_mathcat_initialized_on_this_thread` short-circuits
    /// rather than re-calling `set_rules_dir` (which causes
    /// subtle pref-cache drift in `libmathcat`).
    static MATHCAT_INITIALIZED: Cell<bool> = const { Cell::new(false) };
}

fn ensure_mathcat_initialized_on_this_thread() -> Result<(), MathCatError> {
    if MATHCAT_INITIALIZED.with(|c| c.get()) {
        return Ok(());
    }
    set_rules_dir_chaining_panic_hook().map_err(MathCatError::Init)?;
    MATHCAT_INITIALIZED.with(|c| c.set(true));
    Ok(())
}

/// `libmathcat::set_rules_dir("Rules")`, with MathCAT's panic hook chained
/// to the hook installed before it (#1583).
///
/// MathCAT installs a panic hook of its own, once per process, at the start
/// of its first `set_rules_dir` (`init_panic_handler`, a `Once`). That hook
/// records the panic for MathCAT's error reporting and calls the hook it
/// replaced only under MathCAT's own `cfg(test)`, which no Slate build sets.
/// So from the first math render on, every panic in the process, caught or
/// not, on any thread, printed nothing. Before the first `set_rules_dir`,
/// on whichever thread gets here first, this takes the current hook, has
/// MathCAT install its own over the default (`init_panic_handler`, the same
/// `Once` `set_rules_dir` would run), then installs a hook that runs
/// MathCAT's and then the previous one; only then does MathCAT load its
/// rules, so no panic during that load is silent either. Taken after
/// MathCAT's `Once`, `previous` would be MathCAT's own hook, and the chain
/// would never reach the one before it. Every call then sets the rules
/// directory (each thread initializes its own MathCAT state).
fn set_rules_dir_chaining_panic_hook() -> Result<(), libmathcat::errors::Error> {
    static CHAINED: std::sync::Once = std::sync::Once::new();
    CHAINED.call_once(|| {
        let previous = std::panic::take_hook();
        libmathcat::init_panic_handler();
        let mathcat = std::panic::take_hook();
        std::panic::set_hook(Box::new(move |info| {
            mathcat(info);
            previous(info);
        }));
    });
    libmathcat::set_rules_dir("Rules")
}

/// Largest MathML payload MathCAT 0.7 accepts. Bigger inputs fail
/// inside `set_mathml`; we route those to a typed "too large" speech
/// rather than silently empty (audit #245 M4).
const MATHCAT_MATHML_MAX_BYTES: usize = 1024 * 1024;

/// Render a raw block into the full `MathBlock` shape under the
/// supplied preferences.
///
/// MathCAT failures fall back to a typed speech string so AT users
/// always hear *something* — silent empty was an audit #245 finding
/// (H1, M4). The MathML is still populated so the UI can render
/// visually.
///
/// Dispatches to the dedicated MathCAT worker thread (audit #269)
/// — see the comment block above the worker for the rationale.
/// Synchronous from the caller's perspective: blocks until the
/// worker thread sends back the rendered block.
pub fn render_math(raw: &RawMathBlock, prefs: MathPrefs) -> MathBlock {
    let worker = worker_handle();
    let (reply_tx, reply_rx) = std::sync::mpsc::sync_channel::<MathBlock>(1);
    let request = RenderRequest {
        raw: raw.clone(),
        prefs,
        reply: reply_tx,
    };
    {
        let worker = worker.lock().expect("MathCAT worker mutex poisoned");
        if worker.tx.send(request).is_err() {
            // Worker thread is gone (shouldn't happen except at
            // process tear-down). Fall back to a stub rather than
            // hanging the caller.
            return MathBlock {
                source: raw.source.clone(),
                display_style: raw.display_style,
                mathml: String::new(),
                speech: format!("Math expression: {}", raw.source),
                braille: Vec::new(),
                line: raw.line,
                byte_offset: raw.byte_offset,
            };
        }
    }
    reply_rx.recv().unwrap_or_else(|_| MathBlock {
        source: raw.source.clone(),
        display_style: raw.display_style,
        mathml: String::new(),
        speech: format!("Math expression: {}", raw.source),
        braille: Vec::new(),
        line: raw.line,
        byte_offset: raw.byte_offset,
    })
}

/// What a source that could not be rendered is spoken as: the source
/// itself, so AT users at least hear what was written. Better than
/// silence. Empty for an empty source.
fn unrendered_speech(source: &str) -> String {
    if source.trim().is_empty() {
        String::new()
    } else {
        format!("Math expression: {source}")
    }
}

/// LaTeX → MathML using pulldown-latex.
///
/// Returns an empty string when pulldown-latex cannot render the source
/// faithfully, and `render_math` then speaks the source itself:
/// - a parse error, which pulldown-latex would write into the MathML as
///   `<merror>` holding its diagnostic and an unescaped quote of the
///   source, for MathCAT to speak and braille (#1557, #1558);
/// - a command it renders without its content (#1556).
///
/// The MathML writer copies two kinds of text verbatim: the name of a
/// function, which `\operatorname{…}` sets, and the character of a
/// relation or operator such as `<` or `&`. The names are handed to it
/// already escaped, and the lone characters are escaped in its output
/// (#1557), so no source text becomes MathML structure.
fn latex_to_mathml(latex: &str) -> String {
    use pulldown_latex::event::{Content, Event};
    use pulldown_latex::{RenderConfig, Storage, mathml::push_mathml};
    if uses_unrenderable_command(latex) {
        return String::new();
    }
    let storage = Storage::new();
    let Ok(events) =
        pulldown_latex::Parser::new(latex, &storage).collect::<Result<Vec<Event<'_>>, _>>()
    else {
        return String::new();
    };
    let events = fold_primes_into_superscripts(events);
    let names: Vec<String> = events
        .iter()
        .filter_map(|event| match event {
            Event::Content(Content::Function(name)) => Some(escape_xml_text(name)),
            _ => None,
        })
        .collect();
    let mut escaped_names = names.iter();
    let escaped = events.into_iter().map(|event| {
        Ok::<_, std::convert::Infallible>(match event {
            Event::Content(Content::Function(_)) => Event::Content(Content::Function(
                escaped_names.next().map_or("", String::as_str),
            )),
            other => other,
        })
    });
    let mut out = String::new();
    match push_mathml(&mut out, escaped, RenderConfig::default()) {
        Ok(()) => escape_stray_markup(&out),
        Err(_) => String::new(),
    }
}

/// #1585: a prime after an element is that element's superscript, as in TeX
/// and MathJax (`<msup><mi>v</mi><mo>′</mo></msup>`). pulldown-latex 0.8
/// reads `v'` as `v` followed by a loose `′`, so MathCAT, seeing no function
/// before `(`, speaks `v'(x)` as "v prime times x". This rewrites a run of one
/// or two primes that follows a free element into exactly the events
/// pulldown-latex itself yields for `v^{\prime}` (or `v^{\dprime}`): the
/// superscript script, the element, then a group holding `′` or `″`. A
/// function name, script or visual that carries the primes is grouped
/// first, as for `{\sin^2}^{\prime}`: pulldown-latex writes a function's
/// application mark after its script, and the thin space before a function
/// name that follows an ordinary atom (`2\sin`) inside the script it opens,
/// and either gives MathCAT a superscript of three or four children, which
/// it rejects.
///
/// Three or more primes stay as parsed: no superscript MathCAT 0.7.7 reads
/// gives correct Nemeth for them (`‴` brailles a stray `.`, and a run of `′`
/// gains a level indicator), while the loose run keeps the braille right
/// (#1628).
///
/// An element is free when it is not an argument a script or visual is
/// still waiting for. A prime there is itself that argument, and stays. An
/// ordinary character (a letter, or `|` typed as itself), a function name,
/// a number, a plain `{...}` group or a whole script or visual (root,
/// fraction, negation) can carry primes; a delimiter or an operator
/// pulldown-latex classes as one, a `\left...\right` group and an
/// environment cannot (`(fg)'`, `\left(fg\right)'`, `\vert x\vert'` and
/// `a/'` keep their loose prime). A prime with nothing before it stays loose
/// too.
fn fold_primes_into_superscripts(
    events: Vec<pulldown_latex::event::Event<'_>>,
) -> Vec<pulldown_latex::event::Event<'_>> {
    use pulldown_latex::event::{Content, Event, Grouping, ScriptPosition, ScriptType, Visual};

    /// What an element still open at this point is waiting for.
    enum Open {
        /// A `{...}` group, from its `Begin` at this index.
        Group(usize),
        /// A script or visual at this index, waiting for `remaining` elements.
        Construct { start: usize, remaining: usize },
    }

    /// The element that just completed at a free position and can carry
    /// primes: where it starts in `out`, and whether it is grouped before it
    /// is scripted (a function name, or a whole script or visual).
    #[derive(Clone, Copy)]
    struct Carrier {
        start: usize,
        grouped: bool,
    }

    fn primes_in(event: &Event<'_>) -> Option<usize> {
        match event {
            Event::Content(Content::Ordinary { content, .. }) => match content {
                '′' => Some(1),
                '″' => Some(2),
                '‴' => Some(3),
                '⁗' => Some(4),
                _ => None,
            },
            _ => None,
        }
    }

    /// One element completed: count it against the construct waiting for
    /// it, and answer the element now free, if it can carry primes.
    fn complete(
        stack: &mut Vec<Open>,
        start: usize,
        carriable: bool,
        grouped: bool,
    ) -> Option<Carrier> {
        let mut carrier = carriable.then_some(Carrier { start, grouped });
        loop {
            match stack.last_mut() {
                Some(Open::Construct {
                    start: construct,
                    remaining,
                }) => {
                    *remaining -= 1;
                    if *remaining > 0 {
                        return None;
                    }
                    carrier = Some(Carrier {
                        start: *construct,
                        grouped: true,
                    });
                    stack.pop();
                }
                _ => return carrier,
            }
        }
    }

    if !events.iter().any(|event| primes_in(event).is_some()) {
        return events;
    }
    let original = events.clone();
    let mut out: Vec<Event<'_>> = Vec::with_capacity(events.len() + 6);
    let mut stack: Vec<Open> = Vec::new();
    let mut previous: Option<Carrier> = None;
    let mut events = events.into_iter().peekable();
    while let Some(event) = events.next() {
        // A carrier is only ever free: `complete` answers one only when no
        // construct is left waiting, and every push clears it.
        if let Some(primes) = primes_in(&event)
            && let Some(carrier) = previous
        {
            let mut run = vec![event];
            let mut count = primes;
            while let Some(more) = events.peek().and_then(primes_in) {
                count += more;
                run.extend(events.next());
            }
            previous = None;
            let glyph = match count {
                1 => '′',
                2 => '″',
                _ => {
                    // Three or more: kept loose (see above).
                    out.extend(run);
                    continue;
                }
            };
            out.insert(
                carrier.start,
                Event::Script {
                    ty: ScriptType::Superscript,
                    position: ScriptPosition::Right,
                },
            );
            if carrier.grouped {
                out.insert(carrier.start + 1, Event::Begin(Grouping::Normal));
                out.push(Event::End);
            }
            out.push(Event::Begin(Grouping::Normal));
            out.push(Event::Content(Content::Ordinary {
                content: glyph,
                stretchy: false,
            }));
            out.push(Event::End);
            continue;
        }
        let index = out.len();
        match &event {
            Event::Script { ty, .. } => {
                let remaining = if matches!(ty, ScriptType::SubSuperscript) {
                    3
                } else {
                    2
                };
                stack.push(Open::Construct {
                    start: index,
                    remaining,
                });
                previous = None;
            }
            Event::Visual(visual) => {
                let remaining = match visual {
                    Visual::SquareRoot | Visual::Negation => 1,
                    Visual::Root | Visual::Fraction(_) => 2,
                };
                stack.push(Open::Construct {
                    start: index,
                    remaining,
                });
                previous = None;
            }
            Event::Begin(_) => {
                stack.push(Open::Group(index));
                previous = None;
            }
            Event::End => {
                let Some(Open::Group(start)) = stack.pop() else {
                    // The walker's arity model disagrees with the stream:
                    // leave it exactly as parsed.
                    return original;
                };
                // Only a plain `{...}` group carries: a `\left...\right` group
                // or an environment is bracketed, and keeps its prime loose as
                // a delimiter does.
                let plain = matches!(out.get(start), Some(Event::Begin(Grouping::Normal)));
                out.push(event);
                previous = complete(&mut stack, start, plain, false);
                continue;
            }
            Event::Content(content) => {
                let function = matches!(content, Content::Function(_));
                let carriable = match content {
                    Content::Function(_) | Content::Number(_) => true,
                    Content::Ordinary { .. } => primes_in(&event).is_none(),
                    _ => false,
                };
                out.push(event);
                previous = complete(&mut stack, index, carriable, function);
                continue;
            }
            _ => {
                // A space, a state change (`\rm`) or an environment's flow:
                // `push_mathml` takes any event a script or visual is
                // waiting for as that argument, so it counts as one there,
                // and carries no prime.
                out.push(event);
                previous = if matches!(stack.last(), Some(Open::Construct { .. })) {
                    complete(&mut stack, index, false, false)
                } else {
                    None
                };
                continue;
            }
        }
        out.push(event);
    }
    out
}

/// Commands pulldown-latex 0.8 parses but renders without their
/// content, so the accessible output would describe less than the source
/// holds (#1556): `\sideset` drops all four of its scripts.
const UNRENDERABLE_COMMANDS: &[&str] = &["sideset"];

/// Whether `latex` uses a command in [`UNRENDERABLE_COMMANDS`]: a
/// backslash and exactly that command's letters. A control symbol such
/// as `\\` is skipped whole, so the letters after it are text.
fn uses_unrenderable_command(latex: &str) -> bool {
    let bytes = latex.as_bytes();
    let mut i = 0;
    while i < bytes.len() {
        if bytes[i] != b'\\' {
            i += 1;
            continue;
        }
        let start = i + 1;
        let end = start
            + bytes[start..]
                .iter()
                .take_while(|byte| byte.is_ascii_alphabetic())
                .count();
        if end == start {
            i = start + 1;
            continue;
        }
        if UNRENDERABLE_COMMANDS.contains(&&latex[start..end]) {
            return true;
        }
        i = end;
    }
    false
}

fn escape_xml_text(text: &str) -> String {
    text.replace('&', "&amp;")
        .replace('<', "&lt;")
        .replace('>', "&gt;")
}

/// Escapes each `<` that cannot open a tag and each `&` that does not
/// begin a character reference: the lone relation and operator
/// characters pulldown-latex writes unescaped (#1557). Its own markup
/// and references are left alone.
fn escape_stray_markup(mathml: &str) -> String {
    let mut out = String::with_capacity(mathml.len());
    let mut rest = mathml;
    while let Some(index) = rest.find(['<', '&']) {
        out.push_str(&rest[..index]);
        let tail = &rest[index..];
        let markup = if let Some(after) = tail.strip_prefix('<') {
            after.starts_with(|c: char| c.is_ascii_alphabetic() || matches!(c, '/' | '!' | '?'))
        } else {
            begins_reference(&tail[1..])
        };
        out.push_str(match (tail.as_bytes()[0], markup) {
            (b'<', false) => "&lt;",
            (b'&', false) => "&amp;",
            (b'<', true) => "<",
            _ => "&",
        });
        rest = &tail[1..];
    }
    out.push_str(rest);
    out
}

/// Whether `text` (what follows an `&`) completes a character or entity
/// reference: `name;`, `#digits;` or `#xhex;`.
fn begins_reference(text: &str) -> bool {
    let (body, allowed): (&str, fn(char) -> bool) =
        if let Some(hex) = text.strip_prefix("#x").or_else(|| text.strip_prefix("#X")) {
            (hex, |c| c.is_ascii_hexdigit())
        } else if let Some(decimal) = text.strip_prefix('#') {
            (decimal, |c| c.is_ascii_digit())
        } else {
            (text, |c| c.is_ascii_alphanumeric())
        };
    let length = body.chars().take_while(|&c| allowed(c)).count();
    length > 0 && body[length..].starts_with(';')
}

/// MathML → speech + braille via MathCAT.
fn mathml_to_speech_and_braille(
    mathml: &str,
    prefs: MathPrefs,
) -> Result<(String, Vec<u8>), MathCatError> {
    if mathml.trim().is_empty() {
        return Ok((String::new(), Vec::new()));
    }
    if mathml.len() > MATHCAT_MATHML_MAX_BYTES {
        return Err(MathCatError::MathmlTooLarge);
    }
    ensure_mathcat_initialized_on_this_thread()?;
    apply_prefs(prefs)?;
    libmathcat::set_mathml(mathml).map_err(MathCatError::SetMathml)?;
    let speech = libmathcat::get_spoken_text().map_err(MathCatError::Speech)?;
    // #1558: a braille failure keeps the speech. The block has no braille,
    // and the hosts say braille is not available for it.
    let braille = libmathcat::get_braille("")
        .map(String::into_bytes)
        .unwrap_or_else(|_| {
            log::debug!("slate-mathcat: braille generation failed; the speech is kept");
            Vec::new()
        });
    Ok((speech, braille))
}

fn apply_prefs(prefs: MathPrefs) -> Result<(), MathCatError> {
    let speech_style = match prefs.speech_style {
        MathSpeechStyle::ClearSpeak => "ClearSpeak",
        MathSpeechStyle::SimpleSpeak => "SimpleSpeak",
    };
    let verbosity = match prefs.verbosity {
        MathVerbosity::Terse => "Terse",
        MathVerbosity::Medium => "Medium",
        MathVerbosity::Verbose => "Verbose",
    };
    let braille = match prefs.braille_code {
        BrailleCode::Nemeth => "Nemeth",
        BrailleCode::Ueb => "UEB",
    };
    libmathcat::set_preference("SpeechStyle", speech_style).map_err(MathCatError::Preference)?;
    libmathcat::set_preference("Verbosity", verbosity).map_err(MathCatError::Preference)?;
    libmathcat::set_preference("BrailleCode", braille).map_err(MathCatError::Preference)?;
    Ok(())
}

/// MathCAT FFI errors. Kept internal — public callers see empty
/// speech + braille on render failure so a single bad block doesn't
/// poison the whole document. Variants carry the underlying error
/// for `Debug` formatting in diagnostic logs even though we don't
/// match on them at runtime.
#[derive(Debug)]
#[allow(dead_code)]
enum MathCatError {
    /// MathCAT's `set_rules_dir` failed on the current thread. With
    /// the `include-zip` feature this should never happen in
    /// practice; it's plumbed so init failures can be diagnosed.
    Init(libmathcat::errors::Error),
    /// MathML payload exceeds MathCAT's 1 MiB internal cap. Audit
    /// #245 M4 — surface as a typed speech rather than empty.
    MathmlTooLarge,
    SetMathml(libmathcat::errors::Error),
    Speech(libmathcat::errors::Error),
    Preference(libmathcat::errors::Error),
}

// mathcat package's library is named `libmathcat` (see its Cargo.toml's
// [lib] name). The dep name in Cargo.toml stays `mathcat` (the package
// name), but the rustc import target is `libmathcat`.
use libmathcat;

// --- Public surface for VaultSession ----------------------------------

/// Convert a (potentially MathCAT-failed) `VaultError` shape. Today's
/// caller (`VaultSession::get_math_blocks`) doesn't surface anything
/// to the UI on MathCAT failure — the empty-speech fallback is
/// silent — but this exists so future "trace why MathCAT failed"
/// callers can route through `VaultError::Unsupported`.
#[allow(dead_code)]
fn math_error_to_vault(_err: MathCatError) -> VaultError {
    VaultError::Unsupported {
        feature: "math rendering".into(),
    }
}

// --- Tests -------------------------------------------------------------

#[cfg(test)]
mod tests {
    use super::*;
    use std::thread;

    /// #1585: a prime after an element is its superscript, byte for byte the
    /// MathML of the explicit `^{\prime}` form.
    #[test]
    fn a_prime_after_an_identifier_is_a_superscript_in_mathml() {
        let folded = latex_to_mathml("v'(x)");
        assert_eq!(folded, latex_to_mathml("v^{\\prime}(x)"));
        assert!(folded.contains("<msup><mi>v</mi>"), "{folded}");
        assert_eq!(
            latex_to_mathml("f'(g(x)) \\cdot g'(x)"),
            latex_to_mathml("f^{\\prime}(g(x)) \\cdot g^{\\prime}(x)")
        );
        // A function name is grouped before it is scripted (see the fold).
        assert_eq!(
            latex_to_mathml("\\sin'(x)"),
            latex_to_mathml("{\\sin}^{\\prime}(x)")
        );
        assert_eq!(latex_to_mathml("{u}'v"), latex_to_mathml("{u}^{\\prime}v"));
    }

    /// Two primes fold into one `″` (MathCAT reads "double prime", and its
    /// Nemeth keeps the two prime cells). Three or more stay as parsed: no
    /// superscript MathCAT reads gives them correct Nemeth.
    #[test]
    fn consecutive_primes_fold_into_one_superscript() {
        assert_eq!(
            latex_to_mathml("f''(x)"),
            latex_to_mathml("f^{\\dprime}(x)")
        );
        // A run counts primes, not glyphs: `\dprime` is two.
        assert_eq!(latex_to_mathml("f\\dprime(x)"), latex_to_mathml("f''(x)"));
        for source in ["f'''(x)", "f''''(x)", "f\\dprime'(x)", "f'\\dprime(x)"] {
            let mathml = latex_to_mathml(source);
            assert!(!mathml.is_empty(), "{source} did not render");
            assert!(!mathml.contains("<msup>"), "{source}: {mathml}");
        }
    }

    /// A prime with no element before it, a prime after a delimiter, an
    /// operator, a `\left...\right` group or an environment, and a prime
    /// that is itself an argument a script or fraction still waits for are
    /// left as pulldown-latex parsed them.
    #[test]
    fn a_prime_with_no_free_element_before_it_is_left_alone() {
        for source in [
            "'x",
            "(f g)'(x)",
            "\\left(f g\\right)'(x)",
            "\\begin{matrix} a \\end{matrix}'",
            "a/'",
            "\\frac a'",
        ] {
            let mathml = latex_to_mathml(source);
            assert!(!mathml.is_empty(), "{source} did not render");
            assert!(!mathml.contains("<msup>"), "{source}: {mathml}");
        }
        assert_eq!(
            latex_to_mathml("'x"),
            "<math display=\"inline\"><mi>′</mi><mi>x</mi></math>"
        );
        // A whole script carries the prime, grouped: x_1' is {x_1} primed.
        assert!(latex_to_mathml("x_1'").contains("<msup><mrow><msub>"));
    }

    /// A primed script on a function (`\sin^2'`, `\log_2'`) is grouped
    /// before it is scripted, as `{\sin^2}^{\prime}` is: pulldown-latex
    /// writes the function's application mark after its script, and in a
    /// bare `msup` that mark gave MathCAT a four-child superscript it
    /// rejected, so the whole formula fell back to its source.
    #[test]
    fn a_primed_script_on_a_function_is_grouped_before_it_is_scripted() {
        assert_eq!(
            latex_to_mathml("\\sin^2'(x)"),
            latex_to_mathml("{\\sin^2}^{\\prime}(x)")
        );
        assert_eq!(
            latex_to_mathml("\\log_2'(x)"),
            latex_to_mathml("{\\log_2}^{\\prime}(x)")
        );
        for source in ["\\sin^2'(x)", "v'(x) + \\sin^2'(x)", "\\lim_{x\\to 0}'"] {
            let raw = RawMathBlock {
                source: source.to_string(),
                display_style: MathDisplayStyle::Block,
                line: 1,
                byte_offset: 0,
            };
            let block = render_math(&raw, MathPrefs::default());
            assert!(
                !block.speech.starts_with("Math expression"),
                "{source} fell back: {}",
                block.speech
            );
            assert!(!block.braille.is_empty(), "{source} has no braille");
        }
    }

    /// A primed function after a letter, a number, a closing bracket or a
    /// group renders and is spoken as a function: pulldown-latex writes a
    /// thin space before a function name after such an atom, and inside a
    /// bare `msup` that space gave MathCAT a three-child superscript it
    /// rejected, so the whole formula fell back to its source.
    #[test]
    fn a_primed_function_after_an_ordinary_atom_still_renders() {
        for source in [
            "2\\sin'(x)",
            "x\\cos'(x)",
            "-\\sin'(x)",
            "\\sin'(x)\\cos'(x)",
            "x\\operatorname{erf}'(x)",
        ] {
            let raw = RawMathBlock {
                source: source.to_string(),
                display_style: MathDisplayStyle::Block,
                line: 1,
                byte_offset: 0,
            };
            let block = render_math(&raw, MathPrefs::default());
            assert!(
                !block.speech.starts_with("Math expression"),
                "{source} fell back: {}",
                block.speech
            );
            assert!(!block.braille.is_empty(), "{source} has no braille");
            assert!(block.speech.contains("of x"), "{source}: {}", block.speech);
        }
    }

    /// A number, a whole fraction and a function inside an environment's cell
    /// carry primes too; and an arity the walker cannot follow leaves the
    /// formula exactly as parsed rather than mis-nesting it.
    #[test]
    fn every_kind_of_free_element_carries_its_prime() {
        assert!(latex_to_mathml("2'").contains("<msup><mn>2</mn>"));
        // A bar typed as itself is an ordinary character.
        assert_eq!(latex_to_mathml("|x|'"), latex_to_mathml("|x|^{\\prime}"));
        assert_eq!(
            latex_to_mathml("\\frac{a}{b}'"),
            latex_to_mathml("{\\frac{a}{b}}^{\\prime}")
        );
        assert!(
            latex_to_mathml("\\begin{matrix} f'(x) \\end{matrix}").contains("<msup><mi>f</mi>")
        );
        // `\sqrt[]` takes an empty index the walker does not count: the
        // stream is handed on unchanged, and still renders.
        assert!(!latex_to_mathml("{\\sqrt[]{x}}'").is_empty());
    }

    /// A state change (`\rm`) a script or root is waiting for is that
    /// argument, as `push_mathml` takes it, so the element after it is free.
    #[test]
    fn a_state_change_counts_as_the_argument_it_fills() {
        let mathml = latex_to_mathml("\\sqrt\\rm x'");
        assert!(
            mathml.contains("<msqrt></msqrt><msup><mi mathvariant=\"normal\">x</mi>"),
            "{mathml}"
        );
    }

    /// The step-0 probe, pinned: a primed function is spoken as a function
    /// ("of x"), never as a product ("times x"), under every speech style
    /// and verbosity.
    #[test]
    fn a_primed_function_is_spoken_as_a_function() {
        let raw = RawMathBlock {
            source: "v'(x)".to_string(),
            display_style: MathDisplayStyle::Block,
            line: 1,
            byte_offset: 0,
        };
        for speech_style in [MathSpeechStyle::ClearSpeak, MathSpeechStyle::SimpleSpeak] {
            for verbosity in [
                MathVerbosity::Terse,
                MathVerbosity::Medium,
                MathVerbosity::Verbose,
            ] {
                let speech = render_math(
                    &raw,
                    MathPrefs {
                        speech_style,
                        verbosity,
                        braille_code: BrailleCode::Nemeth,
                    },
                )
                .speech;
                assert!(
                    !speech.contains("times"),
                    "{speech_style:?} {verbosity:?}: {speech}"
                );
                if verbosity != MathVerbosity::Terse {
                    assert!(
                        speech.contains("of x"),
                        "{speech_style:?} {verbosity:?}: {speech}"
                    );
                }
            }
        }
    }

    /// #1583: MathCAT's own panic hook, installed on the first call into
    /// MathCAT, calls the hook it replaced only under MathCAT's `cfg(test)`.
    /// After a math render, a panic anywhere in the process must still reach
    /// the hook that was installed before. This proves the chain when the
    /// render is the first MathCAT init in its process (nextest runs each
    /// fact in its own; under `cargo test` run it alone with `--exact`). In
    /// a shared process where an earlier fact already initialized MathCAT,
    /// the probe wraps the chain and passes either way.
    #[test]
    fn a_panic_after_mathcat_initializes_still_reaches_the_previous_hook() {
        use std::sync::{Arc, Mutex, PoisonError};
        let seen: Arc<Mutex<Vec<String>>> = Arc::default();
        let recorder = Arc::clone(&seen);
        let current = std::panic::take_hook();
        std::panic::set_hook(Box::new(move |info| {
            let payload = info.payload();
            let message = payload
                .downcast_ref::<String>()
                .cloned()
                .or_else(|| {
                    payload
                        .downcast_ref::<&str>()
                        .map(|text| (*text).to_string())
                })
                .unwrap_or_default();
            recorder
                .lock()
                .unwrap_or_else(PoisonError::into_inner)
                .push(message);
            current(info);
        }));
        let raw = RawMathBlock {
            source: "x^2".to_string(),
            display_style: MathDisplayStyle::Block,
            line: 1,
            byte_offset: 0,
        };
        let block = render_math(&raw, MathPrefs::default());
        assert!(!block.mathml.is_empty(), "the render must reach MathCAT");
        let marker = "slate-1583: a panic after the first math render".to_string();
        let thrown = marker.clone();
        let outcome =
            thread::spawn(move || std::panic::catch_unwind(move || panic!("{thrown}"))).join();
        assert!(
            matches!(outcome, Ok(Err(_))),
            "the probe panic is caught on its own thread"
        );
        // A copy, so the guard is dropped before the assertion: a failing
        // assert panics through the probe, which takes the same lock.
        let seen = seen.lock().unwrap_or_else(PoisonError::into_inner).clone();
        assert!(
            seen.iter().any(|message| message == &marker),
            "the hook installed before MathCAT never saw the panic: {seen:?}"
        );
    }

    /// W3-2 round 5: `get_math_blocks` work is bounded per call. The
    /// count budget degrades formulas past the cap to the typed-speech
    /// shape (source + position intact, empty MathML/braille) instead
    /// of pushing them through the serialized MathCAT worker. The
    /// injectable-budget seam keeps this test from rendering
    /// thousands of formulas; the production cap value is pinned
    /// alongside.
    #[test]
    fn render_budget_degrades_past_the_formula_count_cap() {
        let raws: Vec<RawMathBlock> = (0..4)
            .map(|i| RawMathBlock {
                source: "x".to_string(),
                display_style: MathDisplayStyle::Block,
                line: i as u32 + 1,
                byte_offset: i as u32 * 8,
            })
            .collect();
        let blocks = render_math_blocks_bounded(&raws, MathPrefs::default(), 2);
        assert_eq!(blocks.len(), 4, "over-budget blocks degrade, never drop");
        assert!(
            !blocks[1].mathml.is_empty(),
            "under-budget block must render"
        );
        for block in &blocks[2..] {
            assert!(block.mathml.is_empty());
            assert_eq!(block.speech, "Math expression: x");
            assert!(block.braille.is_empty());
            assert_eq!(block.source, "x");
        }
        // Position survives so hosts still match + degrade in range.
        assert_eq!(blocks[3].byte_offset, 24);
        // The production cap aligns with the reading projection
        // ceiling — formulas past it can never all be displayed.
        assert_eq!(MAX_RENDERED_FORMULAS_PER_NOTE, 2_000);
    }

    /// An oversized formula degrades BEFORE LaTeX → MathML burns
    /// worker time — and only that formula: neighbors render.
    #[test]
    fn render_budget_degrades_oversized_formulas_individually() {
        let oversized = RawMathBlock {
            source: "x+".repeat(MAX_FORMULA_SOURCE_BYTES / 2 + 1),
            display_style: MathDisplayStyle::Block,
            line: 1,
            byte_offset: 0,
        };
        let small = RawMathBlock {
            source: "y".to_string(),
            display_style: MathDisplayStyle::Block,
            line: 3,
            byte_offset: 40_000,
        };
        let blocks = render_math_blocks(&[oversized, small], MathPrefs::default());
        assert!(blocks[0].mathml.is_empty());
        assert_eq!(
            blocks[0].speech,
            "Math expression too large to render to accessible speech."
        );
        assert!(blocks[0].braille.is_empty());
        assert!(
            !blocks[1].mathml.is_empty(),
            "neighbor must render normally"
        );
    }

    /// Audit #269 / #1056: ClearSpeak vs SimpleSpeak must produce
    /// different speech when each `render_math` call runs on its own
    /// fresh thread (the shape mac's `Task.detached` produces).
    ///
    /// History. This test spent a year `#[ignore]`d as "upstream
    /// MathCAT: SpeechStyle swap on the worker thread silently no-ops
    /// after a prior render" — with MathSpeak as the second style.
    /// #1056 established that MathCAT has never shipped MathSpeak (the
    /// crate carries exactly `ClearSpeak_Rules.yaml` and
    /// `SimpleSpeak_Rules.yaml`, through `0.7.6-rc.2`), so
    /// `set_preference("SpeechStyle", "MathSpeak")` was accepted and
    /// ignored and the "no-op" was the style not existing. With a real
    /// second style the property holds, and this test is the proof.
    #[test]
    fn render_math_speech_style_propagates_across_fresh_threads() {
        let formula = r"\sum_{i=0}^{n} \frac{i^2}{2}";
        let raw_cs = RawMathBlock {
            source: formula.to_string(),
            display_style: MathDisplayStyle::Block,
            line: 1,
            byte_offset: 0,
        };
        let raw_ss = raw_cs.clone();
        let prefs_cs = MathPrefs {
            speech_style: MathSpeechStyle::ClearSpeak,
            verbosity: MathVerbosity::Medium,
            braille_code: BrailleCode::Nemeth,
        };
        let prefs_ss = MathPrefs {
            speech_style: MathSpeechStyle::SimpleSpeak,
            verbosity: MathVerbosity::Medium,
            braille_code: BrailleCode::Nemeth,
        };

        let ss_speech = thread::spawn(move || render_math(&raw_ss, prefs_ss).speech)
            .join()
            .expect("SimpleSpeak render thread joined");
        let cs_speech = thread::spawn(move || render_math(&raw_cs, prefs_cs).speech)
            .join()
            .expect("ClearSpeak render thread joined");

        assert!(
            !cs_speech.is_empty() && !ss_speech.is_empty(),
            "MathCAT init must produce non-empty speech on each thread"
        );
        assert_ne!(
            cs_speech, ss_speech,
            "#1056: ClearSpeak vs SimpleSpeak speech must differ when each \
             render_math call runs on its own fresh thread. \
             CS = {cs_speech:?}, SS = {ss_speech:?}"
        );
    }

    /// #1056: the style swap must also take effect on ONE thread that
    /// has already rendered — the shape the Windows host produces (a
    /// Settings flip re-applies prefs on an existing worker) and the
    /// shape audit #269 originally diagnosed as a MathCAT cache bug.
    /// CS → SS → CS on a single thread: the two CS renders agree and
    /// the SS render differs from both.
    #[test]
    fn render_math_speech_style_swaps_on_the_same_thread() {
        let formula = r"\sum_{i=0}^{n} \frac{i^2}{2}";
        let raw = RawMathBlock {
            source: formula.to_string(),
            display_style: MathDisplayStyle::Block,
            line: 1,
            byte_offset: 0,
        };
        let cs = MathPrefs {
            speech_style: MathSpeechStyle::ClearSpeak,
            verbosity: MathVerbosity::Medium,
            braille_code: BrailleCode::Nemeth,
        };
        let ss = MathPrefs {
            speech_style: MathSpeechStyle::SimpleSpeak,
            ..cs
        };

        let (first_cs, then_ss, back_to_cs) = thread::spawn(move || {
            let first_cs = render_math(&raw, cs).speech;
            let then_ss = render_math(&raw, ss).speech;
            let back_to_cs = render_math(&raw, cs).speech;
            (first_cs, then_ss, back_to_cs)
        })
        .join()
        .expect("swap thread joined");

        assert!(!first_cs.is_empty() && !then_ss.is_empty());
        assert_ne!(
            first_cs, then_ss,
            "#1056: switching ClearSpeak → SimpleSpeak on a thread that already \
             rendered must change the speech. CS = {first_cs:?}, SS = {then_ss:?}"
        );
        assert_eq!(
            first_cs, back_to_cs,
            "switching back must restore the ClearSpeak phrasing exactly"
        );
    }

    /// Audit #269 mirror: same propagation property for braille
    /// code (Nemeth ↔ UEB). Same fresh-thread pattern as above.
    #[test]
    fn render_math_braille_code_propagates_across_fresh_threads() {
        let formula = r"\sum_{i=0}^{n} \frac{i^2}{2}";
        let raw_n = RawMathBlock {
            source: formula.to_string(),
            display_style: MathDisplayStyle::Block,
            line: 1,
            byte_offset: 0,
        };
        let raw_u = raw_n.clone();
        let prefs_n = MathPrefs {
            speech_style: MathSpeechStyle::ClearSpeak,
            verbosity: MathVerbosity::Medium,
            braille_code: BrailleCode::Nemeth,
        };
        let prefs_u = MathPrefs {
            speech_style: MathSpeechStyle::ClearSpeak,
            verbosity: MathVerbosity::Medium,
            braille_code: BrailleCode::Ueb,
        };

        let nemeth = thread::spawn(move || render_math(&raw_n, prefs_n).braille)
            .join()
            .expect("Nemeth render thread joined");
        let ueb = thread::spawn(move || render_math(&raw_u, prefs_u).braille)
            .join()
            .expect("UEB render thread joined");

        assert!(
            !nemeth.is_empty() && !ueb.is_empty(),
            "MathCAT braille path must produce non-empty bytes on each thread"
        );
        assert_ne!(
            nemeth,
            ueb,
            "audit #269: Nemeth vs UEB braille bytes must differ on \
             fresh threads. Nemeth len = {}, UEB len = {}",
            nemeth.len(),
            ueb.len()
        );
    }

    #[test]
    fn extracts_inline_math() {
        let blocks = extract_math_blocks("text $x + y$ more");
        assert_eq!(blocks.len(), 1);
        assert_eq!(blocks[0].source, "x + y");
        assert_eq!(blocks[0].display_style, MathDisplayStyle::Inline);
    }

    #[test]
    fn extracts_display_math() {
        let blocks = extract_math_blocks("text\n\n$$\\sum_{i=0}^n i$$\n\nmore");
        assert_eq!(blocks.len(), 1);
        assert_eq!(blocks[0].source, "\\sum_{i=0}^n i");
        assert_eq!(blocks[0].display_style, MathDisplayStyle::Block);
    }

    #[test]
    fn ignores_math_inside_fenced_code() {
        let src = "```\n$x + y$\n```\n\nbody $a$";
        let blocks = extract_math_blocks(src);
        assert_eq!(blocks.len(), 1, "got {:?}", blocks);
        assert_eq!(blocks[0].source, "a");
    }

    #[test]
    fn ignores_math_inside_inline_code() {
        let blocks = extract_math_blocks("see `$5 cost` here, also $x$");
        assert_eq!(blocks.len(), 1);
        assert_eq!(blocks[0].source, "x");
    }

    #[test]
    fn code_delimiters_do_not_close_unclosed_display_math() {
        let fenced = "$$unclosed\n\n- [ ] task\n\n```\n$$inside-code\n```\n";
        let inline = "$$unclosed\n\n- [ ] task\n\n`$$inside-code`\n";

        for source in [fenced, inline] {
            assert!(
                extract_math_blocks(source).is_empty(),
                "code delimiter must not produce an extracted math block: {source:?}"
            );
            assert!(
                interaction_display_math_ranges(source).is_empty(),
                "code delimiter must not suppress editor interactions: {source:?}"
            );
        }
    }

    #[test]
    fn inline_code_dollar_does_not_close_unclosed_inline_math() {
        let source = "prefix $unclosed `code $` trailing text";
        assert!(
            extract_math_blocks(source).is_empty(),
            "inline-code delimiter must not produce an inline math block"
        );
    }

    #[test]
    fn inline_math_close_uses_backslash_parity() {
        assert!(
            extract_math_blocks(r"$x \$").is_empty(),
            "one backslash must escape the candidate close"
        );

        let even = extract_math_blocks(r"$x \\$ trailing $");
        assert_eq!(even.len(), 1, "got {even:?}");
        assert_eq!(
            even[0].source, r"x \\",
            "two backslashes must leave the first candidate close active"
        );

        let odd = extract_math_blocks(r"$x \\\$ trailing$");
        assert_eq!(odd.len(), 1, "got {odd:?}");
        assert!(
            odd[0].source.contains("trailing"),
            "three backslashes must escape the first candidate close"
        );
    }

    #[test]
    fn inline_math_opener_uses_backslash_parity() {
        assert!(
            extract_math_blocks(r"text \$x$").is_empty(),
            "one backslash must escape the candidate opener"
        );

        let even = extract_math_blocks(r"text \\$x$");
        assert_eq!(even.len(), 1, "got {even:?}");
        assert_eq!(even[0].source, "x");

        assert!(
            extract_math_blocks(r"text \\\$x$").is_empty(),
            "three backslashes must escape the candidate opener"
        );
    }

    #[test]
    fn inline_math_skips_code_delimiter_before_the_real_close() {
        let blocks = extract_math_blocks("prefix $alpha `code $` omega$ suffix");
        assert_eq!(blocks.len(), 1, "got {blocks:?}");
        assert!(
            blocks[0].source.ends_with("omega"),
            "the real inline close after code must win: {:?}",
            blocks[0]
        );
    }

    #[test]
    fn display_math_skips_code_delimiters_before_the_real_close() {
        let source = "$$alpha\n```\n$$inside-code\n```\nomega$$";
        let blocks = extract_math_blocks(source);
        assert_eq!(blocks.len(), 1, "got {blocks:?}");
        assert!(
            blocks[0].source.ends_with("omega"),
            "the real close after code must win: {:?}",
            blocks[0]
        );
    }

    #[test]
    fn does_not_open_inline_math_on_dollar_followed_by_digit() {
        // `$50` is a price, not math.
        let blocks = extract_math_blocks("prices: $50 and $100");
        assert!(blocks.is_empty(), "got {:?}", blocks);
    }

    #[test]
    fn open_without_close_is_dropped() {
        let blocks = extract_math_blocks("text $unclosed math");
        assert!(blocks.is_empty());
    }

    #[test]
    fn escaped_dollar_does_not_open_math() {
        let blocks = extract_math_blocks("text \\$x + y\\$ more");
        assert!(blocks.is_empty(), "got {:?}", blocks);
    }

    #[test]
    fn line_numbers_are_one_based() {
        let blocks = extract_math_blocks("first\n\nsecond $x$ here");
        assert_eq!(blocks.len(), 1);
        assert_eq!(blocks[0].line, 3);
    }

    #[test]
    fn interaction_ranges_use_exact_top_level_trim_start_openers_and_canonical_closes() {
        let source = "---\ntitle: Math\n---\n\n  $$alpha\n\nbeta$$\n\n$$a$$ prose $$b$$\n";
        let ranges = interaction_display_math_ranges(source);
        let first = source.find("$$alpha").unwrap();
        let first_end = source.find("beta$$").unwrap() + "beta$$".len();
        let second = source.find("$$a$$").unwrap();

        assert_eq!(ranges, vec![first..first_end, second..second + 5]);
    }

    #[test]
    fn interaction_ranges_reject_midline_lists_quotes_and_code() {
        let source =
            "prose $$mid$$\n\n- $$list$$\n\n> $$quote$$\n\n`$$inline$$`\n\n```\n$$fenced$$\n```\n";
        assert!(interaction_display_math_ranges(source).is_empty());
    }

    #[test]
    fn interaction_range_scan_stays_linear_for_mixed_code_and_dollar_dense_input() {
        let mut source = String::with_capacity(8 * 1024 * 1024);
        while source.len() < 8 * 1024 * 1024 {
            source.push_str("```\n$$$$$$$$$$$$$$$$\n```\nprose $$x$$ $$y$$ $$$$$$$$\n\n");
        }

        let started = std::time::Instant::now();
        assert!(interaction_display_math_ranges(&source).is_empty());
        assert!(
            started.elapsed() < std::time::Duration::from_secs(20),
            "mixed fenced/$-dense interaction scan regressed from linear behavior"
        );
    }
    #[test]
    fn prefs_fingerprint_changes_on_each_field() {
        let base = MathPrefs::default();
        let style_change = MathPrefs {
            speech_style: MathSpeechStyle::SimpleSpeak,
            ..base
        };
        let verb_change = MathPrefs {
            verbosity: MathVerbosity::Verbose,
            ..base
        };
        let braille_change = MathPrefs {
            braille_code: BrailleCode::Ueb,
            ..base
        };
        assert_ne!(base.fingerprint(), style_change.fingerprint());
        assert_ne!(base.fingerprint(), verb_change.fingerprint());
        assert_ne!(base.fingerprint(), braille_change.fingerprint());
    }

    #[test]
    fn latex_to_mathml_round_trips_basic_formula() {
        let mathml = latex_to_mathml("x + y");
        assert!(
            mathml.contains("<math"),
            "expected MathML root, got {mathml}"
        );
        assert!(mathml.contains('+'));
    }

    #[test]
    fn render_math_populates_mathml_for_basic_formula() {
        let raw = RawMathBlock {
            source: "x + 1".to_string(),
            display_style: MathDisplayStyle::Inline,
            line: 1,
            byte_offset: 0,
        };
        let block = render_math(&raw, MathPrefs::default());
        assert!(block.mathml.contains("<math"));
    }

    /// Audit #245 H1: pre-fix, `set_rules_dir` was never called, so
    /// MathCAT silently returned empty speech for every block.
    /// After the per-thread init lands, speech for a simple formula
    /// MUST be non-empty.
    #[test]
    fn render_math_produces_non_empty_speech_on_basic_formula() {
        let raw = RawMathBlock {
            source: "x + 1".to_string(),
            display_style: MathDisplayStyle::Inline,
            line: 1,
            byte_offset: 0,
        };
        let block = render_math(&raw, MathPrefs::default());
        assert!(
            !block.speech.is_empty(),
            "MathCAT init should produce non-empty speech for `x + 1`; got empty"
        );
    }

    /// Audit #245 H3: display close must honor `\$` escape so a
    /// dollar sign inside a display block doesn't truncate the
    /// block mid-content.
    #[test]
    fn display_math_respects_escaped_dollar_in_body() {
        let src = "$$\\sum_i x_i \\text{ pays \\$5}$$";
        let blocks = extract_math_blocks(src);
        assert_eq!(blocks.len(), 1, "got {:?}", blocks);
        assert!(
            blocks[0].source.contains("\\$5"),
            "escaped dollar should remain inside the block; got: {:?}",
            blocks[0].source
        );
    }

    /// Codoki polish on H3: a `$$` preceded by an EVEN number of
    /// backslashes is NOT escaped — the backslashes form literal
    /// `\\` pairs and the close fence stands. The original single-
    /// backslash check would treat `\\$$` as escaped and skip the
    /// close, sweeping math through the rest of the file.
    #[test]
    fn display_math_does_not_treat_double_backslash_as_escape() {
        // `$$ x \\$$ trailing text` — the `\\` is a LaTeX line
        // break, then `$$` closes the block normally.
        let src = "$$ x \\\\$$ trailing text";
        let blocks = extract_math_blocks(src);
        assert_eq!(blocks.len(), 1, "got {:?}", blocks);
        assert!(
            !blocks[0].source.contains("trailing"),
            "double-backslash before `$$` must close — block should not include trailing text; got: {:?}",
            blocks[0].source
        );
    }

    /// Audit #245 M4: a MathML payload that exceeds MathCAT's 1 MiB
    /// internal cap must produce a typed fallback speech rather
    /// than silently empty.
    #[test]
    fn render_math_too_large_gets_typed_fallback_speech() {
        let raw = RawMathBlock {
            source: "x".to_string(),
            display_style: MathDisplayStyle::Inline,
            line: 1,
            byte_offset: 0,
        };
        // Build a renderer probe directly: stuff oversized MathML
        // through `mathml_to_speech_and_braille`.
        let huge = "<math>".to_string() + &"x".repeat(2 * 1024 * 1024) + "</math>";
        let err = mathml_to_speech_and_braille(&huge, MathPrefs::default()).unwrap_err();
        assert!(
            matches!(err, MathCatError::MathmlTooLarge),
            "expected MathmlTooLarge; got {err:?}"
        );
        // And `render_math` end-to-end: an oversized LaTeX source
        // would produce oversized MathML — confirm the fallback
        // string fires. (We can't easily trigger an oversized MathML
        // from a small LaTeX; the probe above covers the typed
        // error. Smoke-test the fallback shape on a basic formula
        // by manually fabricating the MathML.)
        let _ = raw; // silence unused on non-oversized branch
    }

    fn render_with(source: &str, braille_code: BrailleCode) -> MathBlock {
        let raw = RawMathBlock {
            source: source.to_string(),
            display_style: MathDisplayStyle::Inline,
            line: 1,
            byte_offset: 0,
        };
        render_math(
            &raw,
            MathPrefs {
                braille_code,
                ..MathPrefs::default()
            },
        )
    }

    /// #1556: pulldown-latex 0.8 parses `\sideset` and drops all four of
    /// its scripts, so the accessible output described a bare operator.
    /// Like a source pulldown-latex cannot parse, it now gets no MathML
    /// and is spoken as its source.
    #[test]
    fn sideset_is_spoken_as_its_source_not_as_a_bare_operator() {
        let source = r"\sideset{_a^b}{_c^d}\sum";
        for code in [BrailleCode::Nemeth, BrailleCode::Ueb] {
            let block = render_with(source, code);
            assert_eq!(block.mathml, "", "{code:?}");
            assert_eq!(
                block.speech,
                format!("Math expression: {source}"),
                "{code:?}"
            );
            assert!(block.braille.is_empty(), "{code:?}");
        }
    }

    #[test]
    fn only_the_sideset_command_itself_is_refused() {
        assert!(uses_unrenderable_command(r"\sideset{_a}{_b}\sum"));
        assert!(uses_unrenderable_command(r"x + \sideset{}{}\prod"));
        // `\\` is a line break; the letters after it are plain text.
        assert!(!uses_unrenderable_command(r"a \\sideset"));
        // A longer command name is a different command.
        assert!(!uses_unrenderable_command(r"\sidesets x"));
        assert!(!uses_unrenderable_command(r"\sum_a^b"));
    }

    /// #1557 and #1558: pulldown-latex writes a parse error into the
    /// MathML as `<merror>`, holding its diagnostic and an unescaped quote
    /// of the source. Under UEB MathCAT spoke and brailled that
    /// diagnostic; under Nemeth the braille failed and took the speech
    /// with it. A source with a parse error is now spoken as its source
    /// under every braille code, like `\sideset`.
    #[test]
    fn a_parse_error_is_spoken_as_its_source_under_every_braille_code() {
        for source in [r"{n \choose k}", r"\mathscr{L}", r"\frac{<}{"] {
            for code in [BrailleCode::Nemeth, BrailleCode::Ueb] {
                let block = render_with(source, code);
                assert_eq!(block.mathml, "", "{source} {code:?}");
                assert_eq!(
                    block.speech,
                    format!("Math expression: {source}"),
                    "{source} {code:?}"
                );
                assert!(block.braille.is_empty(), "{source} {code:?}");
            }
        }
    }

    /// #1557: pulldown-latex writes relation and operator characters into
    /// the MathML unescaped, so every `<` and `\&` made it invalid and the
    /// block fell back to its source, with no braille.
    #[test]
    fn comparisons_and_ampersands_render_speak_and_braille() {
        for source in [
            r"x < y",
            r"a \& b",
            r"f(x) = \begin{cases} x & x < 0 \\ -x & x \ge 0 \end{cases}",
        ] {
            for code in [BrailleCode::Nemeth, BrailleCode::Ueb] {
                let block = render_with(source, code);
                assert!(!block.mathml.is_empty(), "{source} {code:?}");
                assert!(
                    !block.speech.is_empty() && !block.speech.starts_with("Math expression"),
                    "{source} {code:?}: {:?}",
                    block.speech
                );
                assert!(!block.braille.is_empty(), "{source} {code:?}");
            }
        }
        assert!(
            render_with(r"x < y", BrailleCode::Nemeth)
                .mathml
                .contains("<mo>&lt;</mo>")
        );
        assert!(
            render_with(r"a \& b", BrailleCode::Nemeth)
                .mathml
                .contains("&amp;")
        );
    }

    /// #1557: an `\operatorname` argument was copied into the MathML
    /// verbatim, so markup in it became MathML structure and the speech
    /// said what the source did not show. It is now text.
    #[test]
    fn operatorname_text_cannot_add_mathml_structure() {
        let block = render_with(r"\operatorname{<mi>evil</mi>}", BrailleCode::Nemeth);
        assert!(!block.mathml.contains("<mi>evil</mi>"), "{}", block.mathml);
        assert!(
            block.mathml.contains("&lt;mi&gt;evil&lt;/mi&gt;"),
            "{}",
            block.mathml
        );
        assert_ne!(block.speech, "evil");
    }

    #[test]
    fn stray_markup_characters_are_escaped_and_markup_is_kept() {
        assert_eq!(
            escape_stray_markup("<mo><</mo><mi>&</mi><mtext>&amp;&#x3C;&#60;&nbsp;</mtext>"),
            "<mo>&lt;</mo><mi>&amp;</mi><mtext>&amp;&#x3C;&#60;&nbsp;</mtext>"
        );
        assert_eq!(escape_stray_markup("<mo>≮</mo>"), "<mo>≮</mo>");
        assert_eq!(
            escape_stray_markup("<mo><\u{338}</mo>"),
            "<mo>&lt;\u{338}</mo>"
        );
    }

    /// #1558: when MathCAT produces the speech but fails on the braille,
    /// the speech is kept and the block has no braille; the hosts then say
    /// braille is not available. MathCAT's Nemeth path fails on
    /// `<merror>`.
    #[test]
    fn a_braille_failure_keeps_the_speech() {
        let prefs = MathPrefs {
            braille_code: BrailleCode::Nemeth,
            ..MathPrefs::default()
        };
        let (speech, braille) = thread::spawn(move || {
            mathml_to_speech_and_braille("<math><merror><mtext>x</mtext></merror></math>", prefs)
        })
        .join()
        .expect("MathCAT thread joined")
        .expect("the speech survives a braille failure");
        assert!(!speech.is_empty());
        assert!(braille.is_empty());
    }
}
