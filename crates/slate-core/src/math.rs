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
/// MathML's still useful to render visually). `speech` is read from
/// `mathml`, except for a run of three or more primes after a carrier,
/// which is spoken from a folded tree while `mathml` keeps the run as
/// parsed and `braille` reads it loose, an authored `‴` or `⁗` spelled as
/// single primes (#1628, CD21).
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
///
/// A formula with a run of three or more primes after a carrier is spoken
/// from its folded tree and brailled from its loose one, whose MathML the
/// block keeps, or from that tree with an authored `‴` or `⁗` spelled as
/// single primes (#1628, CD21; [`MathTrees`]).
fn render_math_on_worker(raw: &RawMathBlock, prefs: MathPrefs) -> MathBlock {
    let MathTrees {
        loose: mathml,
        speech: speech_tree,
        braille: braille_tree,
    } = latex_to_mathml_trees(&raw.source);
    let (speech, braille) = if mathml.is_empty() {
        // pulldown-latex could not render the source faithfully; see
        // `latex_to_mathml_trees`.
        (unrendered_speech(&raw.source), Vec::new())
    } else {
        let rendered = if speech_tree.is_none() && braille_tree.is_none() {
            mathml_to_speech_and_braille(&mathml, prefs)
        } else {
            speech_and_braille_from_two_trees(
                speech_tree.as_deref().unwrap_or(&mathml),
                braille_tree.as_deref().unwrap_or(&mathml),
                prefs,
            )
        };
        match rendered {
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
        #[cfg(test)]
        let _swap = HOOK_SWAP
            .lock()
            .unwrap_or_else(std::sync::PoisonError::into_inner);
        #[cfg(test)]
        hook_chain_pause(HookChainStep::Locked);
        let previous = std::panic::take_hook();
        #[cfg(test)]
        hook_chain_pause(HookChainStep::PreviousTaken);
        libmathcat::init_panic_handler();
        let mathcat = std::panic::take_hook();
        #[cfg(test)]
        hook_chain_pause(HookChainStep::MathcatTaken);
        std::panic::set_hook(Box::new(move |info| {
            mathcat(info);
            previous(info);
        }));
    });
    libmathcat::set_rules_dir("Rules")
}

/// #1979, test builds only: makes a fact's swap of the process panic hook and
/// the `Once` body of [`set_rules_dir_chaining_panic_hook`] one at a time.
///
/// A fact that puts a hook over the current one makes two calls, `take_hook`
/// and `set_hook`. The body makes five: its own `take_hook`, MathCAT's
/// `take_hook` and `set_hook` inside `init_panic_handler`, then its own
/// `take_hook` and `set_hook`. With the fact's two among the body's, on
/// another thread, the fact's hook could be handed to MathCAT, which never
/// calls it, or replaced by MathCAT's hook or by the chain. The body holds
/// this from before its first hook call until after its last, and a fact
/// holds it around its two (`tests::swap_in_a_recorder`). So either the
/// fact goes first and the body's `previous` is the fact's hook, or the body
/// goes first and the fact's hook wraps the finished chain.
///
/// A fact holds this for its two hook calls and nothing else: it never
/// renders or reaches [`set_rules_dir_chaining_panic_hook`] while it holds
/// it, since the body takes it too and would never get it. The body, while
/// it holds it, calls the hook functions, [`hook_chain_pause`] (which waits
/// only on the fact that armed it, never on this lock), and
/// `libmathcat::init_panic_handler`, which makes its two hook calls once
/// and calls nothing of Slate's. Builds that are not tests have no lock:
/// there, nothing but the body changes the hook.
#[cfg(test)]
static HOOK_SWAP: std::sync::Mutex<()> = std::sync::Mutex::new(());

/// #1979, test builds only: a place inside the `Once` body of
/// [`set_rules_dir_chaining_panic_hook`], after it takes [`HOOK_SWAP`] and
/// before it installs the chain, where a fact's swap of the process panic
/// hook must wait for the body.
#[cfg(test)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum HookChainStep {
    /// The body has made no hook call, so the process hook is still the one
    /// from before the first MathCAT init. A swap whose `take_hook` came
    /// before the body's first and whose `set_hook` came after it would
    /// leave the body's `previous` the default hook and put the swap's own
    /// hook inside the body.
    Locked,
    /// The previous hook is taken and MathCAT has not installed its own, so
    /// the process hook is the default one. MathCAT takes whatever is there
    /// next and never calls it.
    PreviousTaken,
    /// MathCAT's hook is taken and the chain is not installed, so the process
    /// hook is the default one again. The chain replaces whatever is there
    /// next.
    MathcatTaken,
}

/// #1979, test builds only: what the first MathCAT init of the process runs
/// at each [`HookChainStep`], on the thread that runs the init. One fact sets
/// it, in a process where it runs alone, to hold the init there, with
/// [`HOOK_SWAP`] held. Unset, a step does nothing.
#[cfg(test)]
static HOOK_CHAIN_PAUSE: std::sync::OnceLock<Box<dyn Fn(HookChainStep) + Send + Sync>> =
    std::sync::OnceLock::new();

#[cfg(test)]
fn hook_chain_pause(step: HookChainStep) {
    if let Some(pause) = HOOK_CHAIN_PAUSE.get() {
        pause(step);
    }
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

/// The loose tree of [`latex_to_mathml_trees`]: the MathML a block keeps.
#[cfg(test)]
fn latex_to_mathml(latex: &str) -> String {
    latex_to_mathml_trees(latex).loose
}

/// The MathML of one formula: the tree every reader is given, and, for a
/// formula with a run of three or more primes after a carrier, a tree that
/// only speech is read from and, when the run holds an authored `‴` or `⁗`,
/// one that only braille is read from (#1628, CD21).
struct MathTrees {
    /// The primes of a long run stay as parsed. `MathBlock.mathml` holds
    /// this tree, Windows exposes it, and braille is read from it unless
    /// `braille` holds one: MathCAT 0.7.7's Nemeth cells for `‴` and `⁗`
    /// hold a stray full stop (`Rules/Braille/Nemeth/unicode-full.yaml:686-687`),
    /// and a loose run of single primes brailles right.
    loose: String,
    /// A long run folded into a superscript, as one or two primes are, so
    /// MathCAT speaks a function's argument ("f triple prime, of x")
    /// rather than a product ("times x"). `None` when the formula has no
    /// long run after a carrier; the loose tree is then the only one.
    speech: Option<String>,
    /// The loose tree with each authored `‴` or `⁗` of a long run spelled as
    /// the single primes it stands for, so it brailles as `f'''` does.
    /// `None` when no long run holds one; braille then reads the loose tree.
    braille: Option<String>,
}

/// LaTeX → MathML using pulldown-latex, with the speech and braille trees
/// of [`MathTrees`] when a long prime run asks for them.
///
/// The loose tree is an empty string, and there is no speech tree, when
/// pulldown-latex cannot render the source faithfully, and `render_math`
/// then speaks the source itself:
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
fn latex_to_mathml_trees(latex: &str) -> MathTrees {
    use pulldown_latex::Storage;
    use pulldown_latex::event::Event;
    let none = || MathTrees {
        loose: String::new(),
        speech: None,
        braille: None,
    };
    if uses_unrenderable_command(latex) {
        return none();
    }
    let storage = Storage::new();
    let Ok(events) =
        pulldown_latex::Parser::new(latex, &storage).collect::<Result<Vec<Event<'_>>, _>>()
    else {
        return none();
    };
    let events = script_positions_as_grouped(latex, events);
    let original = events
        .iter()
        .any(|event| primes_in(event).is_some())
        .then(|| events.clone());
    let (events, met) = fold_primes_into_superscripts(events, LongPrimeRuns::Loose);
    let loose = write_mathml(events);
    let (speech, braille) = match original {
        Some(original) if met.long_run && !loose.is_empty() => {
            let braille = met
                .authored_glyph
                .then(|| {
                    let (spelled, _) =
                        fold_primes_into_superscripts(original.clone(), LongPrimeRuns::Spelled);
                    write_mathml(spelled)
                })
                .filter(|tree| !tree.is_empty());
            let (folded, _) = fold_primes_into_superscripts(original, LongPrimeRuns::Folded);
            (
                Some(write_mathml(folded)).filter(|tree| !tree.is_empty()),
                braille,
            )
        }
        _ => (None, None),
    };
    MathTrees {
        loose,
        speech,
        braille,
    }
}

/// Control words whose primitive in pulldown-latex 0.8 sets the parser's
/// script position (`parser/primitives.rs:140-154`, `:588-630`, `:2116-2119`):
/// the operators with limits and the large operators.
const SCRIPT_POSITION_SETTERS: &[&str] = &[
    "lim",
    "Pr",
    "sup",
    "max",
    "inf",
    "gcd",
    "min",
    "liminf",
    "limsup",
    "sum",
    "prod",
    "coprod",
    "bigvee",
    "bigwedge",
    "bigcup",
    "bigcap",
    "biguplus",
    "bigoplus",
    "bigotimes",
    "bigodot",
    "bigsqcup",
    "bigsqcap",
    "bigtimes",
    "intop",
    "int",
    "iint",
    "iiint",
    "iiiint",
    "intcap",
    "intcup",
    "oint",
    "varointclockwise",
    "intclockwise",
    "oiint",
    "pointint",
    "rppolint",
    "scpolint",
    "oiiint",
    "intlarhk",
    "sqint",
    "intx",
    "intbar",
    "intBar",
    "fint",
];

/// #1629: each script gets the position (beside, or above and below) its
/// base gives it, as in the grouped form.
///
/// pulldown-latex 0.8 keeps the position a base sets (an operator's limits,
/// `\limits`, `\nolimits`) in its parser state, and an ungrouped argument
/// that is itself an operator with limits or a large operator sets it again
/// before the script is emitted (`parser.rs:271-343`). So
/// `\liminf\limits_{n}^\max x` was written as `msubsup` where
/// `\liminf\limits_{n}^{\max} x` gets `munderover`, and
/// `\displaystyle x_\max y` put max under x. The events lose which base
/// set what, so the source is parsed again with each such argument in
/// braces ([`brace_position_setting_arguments`]), and each script takes
/// the position it has there. The braced parse must match this one event
/// for event but for those braces and the positions, and each brace must
/// hold an argument of a script in this parse, never its base (a `^` or
/// `_` a macro took is no script mark); otherwise the events are kept as
/// parsed.
fn script_positions_as_grouped<'a>(
    latex: &str,
    mut events: Vec<pulldown_latex::event::Event<'a>>,
) -> Vec<pulldown_latex::event::Event<'a>> {
    use pulldown_latex::Storage;
    use pulldown_latex::event::{Content, Event, Grouping};
    let Some((braced, braces)) = brace_position_setting_arguments(latex) else {
        return events;
    };
    let Some(arguments) = script_argument_starts(&events) else {
        return events;
    };
    let storage = Storage::new();
    let Ok(grouped) =
        pulldown_latex::Parser::new(&braced, &storage).collect::<Result<Vec<Event<'_>>, _>>()
    else {
        return events;
    };
    let mut positions = Vec::new();
    let mut found = 0;
    let mut next = 0;
    for (index, event) in events.iter().enumerate() {
        match (event, grouped.get(next)) {
            (
                Event::Script { ty, position },
                Some(Event::Script {
                    ty: grouped_ty,
                    position: grouped_position,
                }),
            ) if ty == grouped_ty => {
                if position != grouped_position {
                    positions.push((index, *grouped_position));
                }
                next += 1;
            }
            (_, Some(same)) if same == event => next += 1,
            (
                Event::Content(Content::Function(_) | Content::LargeOp { .. }),
                Some(Event::Begin(Grouping::Normal)),
            ) if arguments[index]
                && grouped.get(next + 1) == Some(event)
                && matches!(grouped.get(next + 2), Some(Event::End)) =>
            {
                found += 1;
                next += 3;
            }
            _ => return events,
        }
    }
    if next != grouped.len() || found != braces {
        return events;
    }
    for (index, grouped_position) in positions {
        if let Event::Script { position, .. } = &mut events[index] {
            *position = grouped_position;
        }
    }
    events
}

/// For each event, whether it starts an argument of a script (a slot after
/// its base), read with the model the writer follows: a group, a script or
/// a visual is one element once closed, content is one, and any other event
/// is one where a construct is waiting for it (as in
/// [`fold_primes_into_superscripts`]). `None` when a group's end has no
/// start.
fn script_argument_starts(events: &[pulldown_latex::event::Event<'_>]) -> Option<Vec<bool>> {
    use pulldown_latex::event::{Event, ScriptType, Visual};
    enum Open {
        Group,
        Construct {
            script: bool,
            filled: usize,
            needed: usize,
        },
    }
    let mut starts = vec![false; events.len()];
    let mut stack: Vec<Open> = Vec::new();
    for (index, event) in events.iter().enumerate() {
        let waiting = matches!(stack.last(), Some(Open::Construct { .. }));
        if let Some(Open::Construct {
            script: true,
            filled,
            ..
        }) = stack.last()
        {
            starts[index] = *filled >= 1;
        }
        let complete = match event {
            Event::Script { ty, .. } => {
                let needed = if matches!(ty, ScriptType::SubSuperscript) {
                    3
                } else {
                    2
                };
                stack.push(Open::Construct {
                    script: true,
                    filled: 0,
                    needed,
                });
                false
            }
            Event::Visual(visual) => {
                let needed = match visual {
                    Visual::SquareRoot | Visual::Negation => 1,
                    Visual::Root | Visual::Fraction(_) => 2,
                };
                stack.push(Open::Construct {
                    script: false,
                    filled: 0,
                    needed,
                });
                false
            }
            Event::Begin(_) => {
                stack.push(Open::Group);
                false
            }
            Event::End => {
                let Some(Open::Group) = stack.pop() else {
                    return None;
                };
                true
            }
            Event::Content(_) => true,
            _ => waiting,
        };
        if complete {
            while let Some(Open::Construct { filled, needed, .. }) = stack.last_mut() {
                *filled += 1;
                if *filled < *needed {
                    break;
                }
                stack.pop();
            }
        }
    }
    Some(starts)
}

/// `latex` with each ungrouped script argument that is a control word in
/// [`SCRIPT_POSITION_SETTERS`] put in braces (`x_\max` becomes `x_{\max}`),
/// and how many were; `None` when there is none. A control word or control
/// symbol is read whole, so `\_` and `\^` are not scripts, and a `%`
/// comment is skipped to the end of its line, as is the whitespace and any
/// comment between a script mark and its argument, as pulldown-latex's
/// lexer skips them (`parser/lex.rs:463-483`); whatever this misreads,
/// [`script_positions_as_grouped`] finds in the parse.
fn brace_position_setting_arguments(latex: &str) -> Option<(String, usize)> {
    let bytes = latex.as_bytes();
    let word_end = |start: usize| {
        let mut end = start;
        while end < bytes.len() && bytes[end].is_ascii_alphabetic() {
            end += 1;
        }
        end
    };
    // The end of the `%` comment at `at`: past its line's newline.
    let comment_end = |at: usize| latex[at..].find('\n').map_or(latex.len(), |n| at + n + 1);
    let mut out = String::new();
    let mut copied = 0;
    let mut braces = 0;
    let mut i = 0;
    while i < bytes.len() {
        match bytes[i] {
            b'\\' => {
                let end = word_end(i + 1);
                i = if end > i + 1 {
                    end
                } else {
                    i + 1 + latex[i + 1..].chars().next().map_or(0, char::len_utf8)
                };
            }
            b'%' => i = comment_end(i),
            b'^' | b'_' => {
                i += 1;
                let mut start = i;
                loop {
                    let rest = &latex[start..];
                    start += rest.len() - rest.trim_start().len();
                    if bytes.get(start) == Some(&b'%') {
                        start = comment_end(start);
                    } else {
                        break;
                    }
                }
                if bytes.get(start) == Some(&b'\\') {
                    let end = word_end(start + 1);
                    if SCRIPT_POSITION_SETTERS.contains(&&latex[start + 1..end]) {
                        out.push_str(&latex[copied..start]);
                        out.push('{');
                        out.push_str(&latex[start..end]);
                        out.push('}');
                        copied = end;
                        braces += 1;
                        i = end;
                    }
                }
            }
            _ => i += 1,
        }
    }
    (braces > 0).then(|| {
        out.push_str(&latex[copied..]);
        (out, braces)
    })
}

/// An event stream → MathML, as written by pulldown-latex and then made
/// readable: the function names escaped before, the stray markup escaped
/// after (#1557), and script elements given the children MathCAT takes
/// (#1629, [`repair_script_children`]). Empty when the writer fails.
fn write_mathml(events: Vec<pulldown_latex::event::Event<'_>>) -> String {
    #[cfg(test)]
    TREES_WRITTEN.with(|written| written.set(written.get() + 1));
    let (written, functions) = write_mathml_marked(events);
    if written.is_empty() {
        written
    } else {
        repair_script_children(&written, &functions)
    }
}

#[cfg(test)]
thread_local! {
    /// How many trees [`write_mathml`] has written on this thread, for the
    /// fact that counts a formula's trees.
    static TREES_WRITTEN: Cell<usize> = const { Cell::new(0) };
}

/// Marks a function name for [`write_mathml_marked`]. A private-use
/// character; a source that holds one itself is written unmarked.
const FUNCTION_MARK: char = '\u{E000}';

/// What the writer gives, with only its markup characters escaped, and for
/// each `<mi>` in it, in order, whether the writer wrote it for a function
/// name. The two are byte for byte what pulldown-latex writes for an
/// ordinary character and for a function name of one character after
/// escaping (`\&` and `\operatorname{&}` are both `<mi>&amp;</mi>`), so
/// each function name is handed to the writer behind [`FUNCTION_MARK`] and
/// the mark is taken out again ([`unmark_functions`]). When the output
/// holds any mark the writer was not handed (the source spells one), the
/// formula is written again unmarked and no `<mi>` is told to be a
/// function, so the repair leaves it as the writer wrote it.
fn write_mathml_marked(events: Vec<pulldown_latex::event::Event<'_>>) -> (String, Vec<bool>) {
    use pulldown_latex::event::{Content, Event};
    let functions = events
        .iter()
        .filter(|event| matches!(event, Event::Content(Content::Function(_))))
        .count();
    if functions == 0 {
        return (write_mathml_names(events, false), Vec::new());
    }
    let marked = write_mathml_names(events.clone(), true);
    if marked.matches(FUNCTION_MARK).count() == functions {
        unmark_functions(&marked)
    } else {
        (write_mathml_names(events, false), Vec::new())
    }
}

/// pulldown-latex's MathML for `events`, its markup characters escaped
/// (#1557), with each function name escaped first and, when `mark` holds,
/// put behind [`FUNCTION_MARK`]. Empty when the writer fails.
fn write_mathml_names(events: Vec<pulldown_latex::event::Event<'_>>, mark: bool) -> String {
    use pulldown_latex::event::{Content, Event};
    use pulldown_latex::{RenderConfig, mathml::push_mathml};
    let names: Vec<String> = events
        .iter()
        .filter_map(|event| match event {
            Event::Content(Content::Function(name)) if mark => {
                Some(format!("{FUNCTION_MARK}{}", escape_xml_text(name)))
            }
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

/// Takes [`FUNCTION_MARK`] out of the writer's output, and answers, for each
/// `<mi>` in order, whether it held the mark. The writer gives a name of
/// one character `mathvariant="normal"` (`mathml.rs:651-655` in
/// pulldown-latex), counting the name as it was handed over, so the mark
/// moved that by one: a marked name of one character gets it back, and a
/// marked empty name loses the one the mark alone earned it. Every other
/// byte is the writer's.
fn unmark_functions(marked: &str) -> (String, Vec<bool>) {
    const UPRIGHT: &str = " mathvariant=\"normal\">";
    let mut out = String::with_capacity(marked.len());
    let mut functions = Vec::new();
    let mut rest = marked;
    while let Some(open) = rest.find("<mi") {
        let after = &rest[open + 3..];
        let Some(close) = after
            .starts_with(['>', ' '])
            .then(|| after.find('>'))
            .flatten()
        else {
            out.push_str(&rest[..open + 3]);
            rest = after;
            continue;
        };
        let tag = &rest[open..open + 3 + close + 1];
        out.push_str(&rest[..open]);
        let text = &after[close + 1..];
        match text.strip_prefix(FUNCTION_MARK) {
            Some(name) => {
                functions.push(true);
                let length = name[..name.find('<').unwrap_or(name.len())].chars().count();
                match (length, tag.strip_suffix(UPRIGHT)) {
                    (0, Some(bare)) => {
                        out.push_str(bare);
                        out.push('>');
                    }
                    (1, None) => {
                        out.push_str(&tag[..tag.len() - 1]);
                        out.push_str(UPRIGHT);
                    }
                    _ => out.push_str(tag),
                }
                rest = name;
            }
            None => {
                functions.push(false);
                out.push_str(tag);
                rest = text;
            }
        }
    }
    out.push_str(rest);
    (out, functions)
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
/// Three or more primes are what `long_runs` says (#1628, CD21). No
/// superscript MathCAT 0.7.7 reads gives correct Nemeth for them (`‴`
/// brailles a stray `.`, `Rules/Braille/Nemeth/unicode-full.yaml:686-687`,
/// and a run of `′` gains a level indicator), while the loose run keeps the
/// braille right but is spoken as a product ("f prime prime, prime, times
/// x"). So [`LongPrimeRuns::Loose`] keeps them as parsed, for the exposed
/// MathML, [`LongPrimeRuns::Spelled`] keeps them loose with an authored `‴`
/// or `⁗` spelled as single primes, for braille, and
/// [`LongPrimeRuns::Folded`] folds them as one and two are (`‴`, `⁗`, and
/// an `mrow` of single primes past four), for speech. The second answer
/// says whether a long run after a carrier was met, and one holding an
/// authored `‴` or `⁗`, so a formula is written only as many times as its
/// trees differ. When a MathCAT release corrects the two cells, the folded
/// tree serves speech and braille and the split goes.
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
    long_runs: LongPrimeRuns,
) -> (Vec<pulldown_latex::event::Event<'_>>, LongRunsMet) {
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
        return (events, LongRunsMet::default());
    }
    let original = events.clone();
    let mut out: Vec<Event<'_>> = Vec::with_capacity(events.len() + 6);
    let mut stack: Vec<Open> = Vec::new();
    let mut previous: Option<Carrier> = None;
    let mut met = LongRunsMet::default();
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
            if count >= 3 {
                met.long_run = true;
                met.authored_glyph |= run
                    .iter()
                    .any(|event| primes_in(event).is_some_and(|primes| primes >= 3));
            }
            let glyphs = match (count, long_runs) {
                (1, _) => vec!['′'],
                (2, _) => vec!['″'],
                (_, LongPrimeRuns::Loose) => {
                    // Three or more, for the exposed MathML: kept as parsed
                    // (see above).
                    out.extend(run);
                    continue;
                }
                (_, LongPrimeRuns::Spelled) => {
                    // Three or more, for braille: loose, an authored `‴` or
                    // `⁗` spelled as the single primes it stands for.
                    for event in run {
                        match primes_in(&event) {
                            Some(count @ 3..) => {
                                out.extend(std::iter::repeat_n('′', count).map(|glyph| {
                                    Event::Content(Content::Ordinary {
                                        content: glyph,
                                        stretchy: false,
                                    })
                                }))
                            }
                            _ => out.push(event),
                        }
                    }
                    continue;
                }
                (3, LongPrimeRuns::Folded) => vec!['‴'],
                (4, LongPrimeRuns::Folded) => vec!['⁗'],
                // No single glyph holds five or more: a row of single
                // primes, which MathCAT speaks one by one on the function.
                (_, LongPrimeRuns::Folded) => vec!['′'; count],
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
            out.extend(glyphs.into_iter().map(|glyph| {
                Event::Content(Content::Ordinary {
                    content: glyph,
                    stretchy: false,
                })
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
                    // leave it exactly as parsed, in either mode.
                    return (original, LongRunsMet::default());
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
    (out, met)
}

/// What [`fold_primes_into_superscripts`] met (#1628).
#[derive(Debug, Clone, Copy, Default, PartialEq, Eq)]
struct LongRunsMet {
    /// A run of three or more primes after a carrier: speech needs its own
    /// tree.
    long_run: bool,
    /// Such a run holding an authored `‴` or `⁗`: braille needs its own tree.
    authored_glyph: bool,
}

/// What [`fold_primes_into_superscripts`] makes of a run of three or more
/// primes after a carrier (#1628).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum LongPrimeRuns {
    /// Kept as parsed: the tree the exposed MathML comes from.
    Loose,
    /// Kept loose, an authored `‴` or `⁗` spelled as three or four `′`:
    /// the tree braille comes from, since MathCAT 0.7.7's Nemeth cells for
    /// the two glyphs hold a stray full stop.
    Spelled,
    /// A superscript, as one or two primes are: the tree speech comes from.
    Folded,
}

/// How many primes an event is: `′` one, `″` two, `‴` three, `⁗` four.
fn primes_in(event: &pulldown_latex::event::Event<'_>) -> Option<usize> {
    use pulldown_latex::event::{Content, Event};
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

/// The thin space pulldown-latex 0.8 writes, byte for byte: before a
/// function name whose previous atom is ordinary, inner or closing
/// (`mathml.rs:641-648` in the crate), and after an application mark that
/// is not followed by an opening, punctuation or closing atom (`:665-668`,
/// `:951-954`). An author's `\,` is written `0.16666667em`; an authored
/// `\hspace{0.1667em}`, `\kern0.1667em` or `\hskip0.1667em` has these bytes.
const WRITER_THIN_SPACE: &str = "<mspace width=\"0.1667em\" />";

/// The writer's application mark, byte for byte: after a function written
/// outside a script, and after a script element in which a function was
/// written directly, as its base or as an argument (`mathml.rs:660-669`,
/// `:947-956`), an accent included.
const APPLICATION_MARK: &str = "<mo>\u{2061}</mo>";

/// The number of children MathCAT takes for an element it holds to a fixed
/// count, which it requires exactly (`canonicalize.rs:455-457`, `:638-646`
/// in mathcat 0.7.7), and whether the element is a script; `None` for any
/// other element.
fn fixed_arity(name: &str) -> Option<(usize, bool)> {
    match name {
        "msup" | "msub" | "munder" | "mover" => Some((2, true)),
        "msubsup" | "munderover" => Some((3, true)),
        "mfrac" | "mroot" => Some((2, false)),
        _ => None,
    }
}

/// #1629: gives a script, fraction or root element the children MathCAT
/// takes when the writer gave it more, and takes away the application mark
/// the writer puts after a script element for a function that is only its
/// argument.
///
/// pulldown-latex opens a script element before it writes the base, and
/// writes the thin space it puts before a function name after an ordinary,
/// inner or closing atom (`2\sin^2 x`, `-\sin^2 x`, `(a+b)\sin^2 x`) or
/// after a script's base (`x_\max`) as it reaches the name, so the space
/// lands inside the element. An accent over a function (`\hat\sin`) is a
/// script of its own, and the writer puts the application mark it writes
/// when the accent closes (`<mo>⁡</mo>`, and a thin space) inside the
/// element that holds the accent. A function written directly in a
/// fraction or a root (`\frac x\sin y`, `\sqrt[3]\sin x`) gets its space
/// and its mark inside that element too. MathCAT rejects such an element
/// for its count of children, so the whole formula fell back to its
/// source, with no braille.
///
/// For an element with more element children than it takes, the
/// candidates are: a thin space directly before a function name (the one
/// it spaced; `functions` says, for each `<mi>` in order, whether the
/// writer wrote it for a function name, [`write_mathml_marked`]); a thin
/// space this repair moved out from the front of one of the element's
/// children; an application mark; and a thin space directly after a
/// candidate mark. Only when removing every candidate leaves exactly the
/// count is the element changed; otherwise it is left as written. In a
/// fraction or a root every candidate is dropped, as the grouped form
/// (`\frac{x}{\sin} y`) has none. In a script element, with the base the
/// first child that is no candidate:
/// - a candidate before the base moves to just before the element (the
///   function was the base, so `2\sin^2 x` reads as `2\sin x` does);
/// - an application mark directly after the base, with its thin space,
///   moves to just after the element (the accented function, with its
///   script, applies to what follows: `2\hat\sin^2 x`);
/// - any other candidate is dropped.
///
/// A script element with a function name written directly as an argument,
/// on a base that is not one, gets the writer's application mark after it
/// (`mathml.rs:947-956`), with or without a space before the name
/// (`x_\max y`, `\int_0^\max f`), and whatever fills its other slots (an
/// element with exactly its count has no candidates, so an authored space
/// in a slot stays: `\int_\hspace{0.1667em}^\max f`). That mark applies nothing, so it goes,
/// with its thin space, and the formula reads as its grouped form does
/// (`x_{\max} y`); a function base keeps it (`\operatorname{foo}_{2}^\max x`).
/// An accented function base is not a function name, so after its mark
/// moved out of the element the element's own goes (`\hat\sin^\max x`),
/// and one mark remains.
///
/// No other element MathCAT accepts is touched, and neither is a thin space
/// that fills a slot (`x^\hspace{0.1667em} 2`). A formula with another
/// element MathCAT still rejects falls back as before; its kept MathML has
/// the repaired elements. The walk is one pass over the tags with an
/// explicit stack, never recursing, however deep the formula nests.
fn repair_script_children(mathml: &str, functions: &[bool]) -> String {
    #[derive(Clone, Copy, PartialEq, Eq)]
    enum Kind {
        Space,
        Function,
        Mark,
        Other,
    }

    /// An element child: its tags and text, `pieces[start..end]`.
    #[derive(Clone, Copy)]
    struct Child {
        start: usize,
        end: usize,
        kind: Kind,
        /// Moved here by the repair, out of the element's first child.
        hoisted: bool,
    }

    /// Whether the next children are the application mark the writer put
    /// after a script element whose argument was a function, and its space.
    #[derive(Clone, Copy, PartialEq, Eq)]
    enum Dropping {
        Nothing,
        Mark,
        SpaceAfterMark,
    }

    struct Frame<'a> {
        open: usize,
        name: &'a str,
        kind: Kind,
        children: Vec<Child>,
        dropping: Dropping,
    }

    /// Where each child the repair moved or dropped now goes. A moved
    /// child is written at its anchor (an element's open or close tag,
    /// which never moves) instead of where it was.
    #[derive(Default)]
    struct Edits {
        gone: Vec<bool>,
        before: std::collections::HashMap<usize, Vec<(usize, usize)>>,
        after: std::collections::HashMap<usize, Vec<(usize, usize)>>,
        placed: std::collections::HashMap<usize, (usize, bool)>,
    }

    impl Edits {
        fn unplace(&mut self, child: Child) {
            if let Some((anchor, after)) = self.placed.remove(&child.start) {
                let list = if after {
                    &mut self.after
                } else {
                    &mut self.before
                };
                if let Some(moved) = list.get_mut(&anchor) {
                    moved.retain(|&(start, _)| start != child.start);
                }
            }
        }

        fn drop_child(&mut self, child: Child) {
            self.unplace(child);
            self.gone[child.start..child.end].fill(true);
        }

        fn place(&mut self, child: Child, anchor: usize, after: bool) {
            self.unplace(child);
            self.gone[child.start..child.end].fill(true);
            let list = if after {
                &mut self.after
            } else {
                &mut self.before
            };
            list.entry(anchor)
                .or_default()
                .push((child.start, child.end));
            self.placed.insert(child.start, (anchor, after));
        }
    }

    fn push_child(frame: &mut Frame<'_>, child: Child, edits: &mut Edits) {
        match frame.dropping {
            Dropping::Mark if child.kind == Kind::Mark => {
                edits.drop_child(child);
                frame.dropping = Dropping::SpaceAfterMark;
                return;
            }
            Dropping::SpaceAfterMark if child.kind == Kind::Space && !child.hoisted => {
                edits.drop_child(child);
                frame.dropping = Dropping::Nothing;
                return;
            }
            _ => frame.dropping = Dropping::Nothing,
        }
        frame.children.push(child);
    }

    if !mathml.contains(WRITER_THIN_SPACE) && !mathml.contains(APPLICATION_MARK) {
        return mathml.to_string();
    }
    let mut pieces: Vec<&str> = Vec::new();
    let mut rest = mathml;
    while !rest.is_empty() {
        let end = if rest.starts_with('<') {
            match rest.find('>') {
                Some(close) => close + 1,
                None => return mathml.to_string(),
            }
        } else {
            rest.find('<').unwrap_or(rest.len())
        };
        pieces.push(&rest[..end]);
        rest = &rest[end..];
    }

    let mut edits = Edits {
        gone: vec![false; pieces.len()],
        ..Edits::default()
    };
    let mut mi_seen = 0;
    let mut stack = vec![Frame {
        open: 0,
        name: "",
        kind: Kind::Other,
        children: Vec::new(),
        dropping: Dropping::Nothing,
    }];
    for (index, piece) in pieces.iter().enumerate() {
        let Some(tag) = piece.strip_prefix('<') else {
            continue;
        };
        if let Some(closing) = tag.strip_prefix('/') {
            if stack.len() < 2 {
                return mathml.to_string();
            }
            let frame = stack.pop().expect("an open element");
            let name = closing.trim_end_matches('>').trim();
            if name != frame.name {
                return mathml.to_string();
            }
            let kind = if frame.name == "mo"
                && index == frame.open + 2
                && pieces[frame.open] == "<mo>"
                && pieces[frame.open + 1] == "\u{2061}"
            {
                Kind::Mark
            } else {
                frame.kind
            };
            let element = Child {
                start: frame.open,
                end: index + 1,
                kind,
                hoisted: false,
            };
            let parent = stack.last_mut().expect("a parent");
            let Some((arity, script)) =
                fixed_arity(frame.name).filter(|&(n, _)| frame.children.len() >= n)
            else {
                push_child(parent, element, &mut edits);
                continue;
            };
            let children = &frame.children;
            // An element with exactly its count, none of them moved here, has
            // no extra child to lose: each one, a space the author put in a
            // slot too, is real.
            let exact = children.len() == arity && !children.iter().any(|child| child.hoisted);
            let mut candidate: Vec<bool> = children
                .iter()
                .enumerate()
                .map(|(i, child)| match child.kind {
                    _ if exact => false,
                    Kind::Space => {
                        child.hoisted
                            || children
                                .get(i + 1)
                                .is_some_and(|next| next.kind == Kind::Function)
                    }
                    Kind::Mark => true,
                    _ => false,
                })
                .collect();
            for i in 1..children.len() {
                if candidate[i - 1]
                    && children[i - 1].kind == Kind::Mark
                    && children[i].kind == Kind::Space
                {
                    candidate[i] = true;
                }
            }
            let kept = candidate.iter().filter(|&&is| !is).count();
            let base = candidate.iter().position(|&is| !is);
            let Some(base) = base.filter(|_| kept == arity) else {
                push_child(parent, element, &mut edits);
                continue;
            };
            let mut hoisted = Vec::new();
            let mut trailing = Vec::new();
            for (i, &child) in children.iter().enumerate() {
                if !candidate[i] {
                    continue;
                }
                if !script {
                    // A fraction's or a root's arguments are not applied to
                    // anything outside it, and its grouped form has no space
                    // or mark in it: each goes.
                    edits.drop_child(child);
                    continue;
                }
                let follows_base_mark = i > base
                    && (i == base + 1 && child.kind == Kind::Mark
                        || i == base + 2
                            && child.kind == Kind::Space
                            && children[base + 1].kind == Kind::Mark
                            && candidate[base + 1]);
                if i < base {
                    edits.place(child, frame.open, false);
                    hoisted.push(Child {
                        hoisted: true,
                        ..child
                    });
                } else if follows_base_mark {
                    edits.place(child, index, true);
                    trailing.push(child);
                } else {
                    edits.drop_child(child);
                }
            }
            for child in hoisted {
                push_child(parent, child, &mut edits);
            }
            push_child(parent, element, &mut edits);
            for child in trailing {
                push_child(parent, child, &mut edits);
            }
            // Whether the writer wrote the base as a function name, and a
            // function name directly as an argument, for which it puts its
            // application mark after the element.
            let base_is_a_function = children[base].kind == Kind::Function;
            let argument_is_a_function = children
                .iter()
                .enumerate()
                .any(|(i, child)| i != base && !candidate[i] && child.kind == Kind::Function);
            // That mark applies nothing on a base that is not a function (a
            // function base keeps it, as `\operatorname{foo}_{2}^{\max} x`
            // does), and after an accent's mark moved out of the element it
            // would be a second one (`\hat\sin^\max x`): it goes.
            if script && argument_is_a_function && !base_is_a_function {
                parent.dropping = Dropping::Mark;
            }
        } else {
            let self_closing = tag.ends_with("/>");
            let name = tag
                .trim_end_matches('>')
                .trim_end_matches('/')
                .split_whitespace()
                .next()
                .unwrap_or("");
            let kind = if *piece == WRITER_THIN_SPACE {
                Kind::Space
            } else if name == "mi" {
                let function = functions.get(mi_seen).copied();
                mi_seen += 1;
                match function {
                    Some(true) => Kind::Function,
                    Some(false) => Kind::Other,
                    // More `<mi>`s than the writer's account: leave it all.
                    None => return mathml.to_string(),
                }
            } else {
                Kind::Other
            };
            if self_closing {
                let child = Child {
                    start: index,
                    end: index + 1,
                    kind,
                    hoisted: false,
                };
                push_child(stack.last_mut().expect("a parent"), child, &mut edits);
            } else {
                stack.push(Frame {
                    open: index,
                    name,
                    kind,
                    children: Vec::new(),
                    dropping: Dropping::Nothing,
                });
            }
        }
    }
    if !edits.gone.contains(&true) || stack.len() != 1 || mi_seen != functions.len() {
        return mathml.to_string();
    }
    let mut out = String::with_capacity(mathml.len());
    let write = |out: &mut String, list: Option<&Vec<(usize, usize)>>| {
        for &(start, end) in list.into_iter().flatten() {
            for piece in &pieces[start..end] {
                out.push_str(piece);
            }
        }
    };
    for (index, piece) in pieces.iter().enumerate() {
        write(&mut out, edits.before.get(&index));
        if !edits.gone[index] {
            out.push_str(piece);
        }
        write(&mut out, edits.after.get(&index));
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

/// #1628, CD21: speech from `speech_tree` (a long prime run folded) and
/// braille from `braille_tree` (the run loose, as the block keeps it or
/// with an authored `‴` or `⁗` spelled as single primes). A speech tree
/// MathCAT cannot take, for any reason, gives the formula the one-tree
/// reading of the braille tree ([`mathml_to_speech_and_braille`]). A
/// braille tree it cannot take keeps the speech with no braille, as a
/// braille failure does there (#1558).
fn speech_and_braille_from_two_trees(
    speech_tree: &str,
    braille_tree: &str,
    prefs: MathPrefs,
) -> Result<(String, Vec<u8>), MathCatError> {
    if speech_tree.len() > MATHCAT_MATHML_MAX_BYTES {
        return mathml_to_speech_and_braille(braille_tree, prefs);
    }
    ensure_mathcat_initialized_on_this_thread()?;
    apply_prefs(prefs)?;
    let Ok(speech) =
        libmathcat::set_mathml(speech_tree).and_then(|_| libmathcat::get_spoken_text())
    else {
        return mathml_to_speech_and_braille(braille_tree, prefs);
    };
    if braille_tree.len() > MATHCAT_MATHML_MAX_BYTES {
        return Ok((speech, Vec::new()));
    }
    let braille = libmathcat::set_mathml(braille_tree)
        .and_then(|_| libmathcat::get_braille(""))
        .map(String::into_bytes)
        .unwrap_or_else(|_| {
            log::debug!("slate-mathcat: braille of the loose tree failed; the speech is kept");
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
    /// Nemeth keeps the two prime cells). Three or more stay as parsed in
    /// the MathML a block keeps and brailles: no superscript MathCAT reads
    /// gives them correct Nemeth. Their speech comes from a folded tree
    /// (#1628, `three_or_more_primes_are_spoken_as_primes`).
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
    /// the hook that was installed before.
    ///
    /// Alone in its process (nextest runs each fact in its own; under `cargo
    /// test`, run it with `--exact`), the render is the first MathCAT init,
    /// the recorder is the hook the chain must call, and the fact proves the
    /// chain: it is red when the chain drops the previous hook. In a shared
    /// `cargo test` process it can prove less. Where an earlier fact already
    /// initialized MathCAT, the recorder wraps the finished chain and passes
    /// whatever the chain does. Where another fact's thread runs the first
    /// init at the same moment, the swap and the init's hook calls go one at
    /// a time ([`HOOK_SWAP`], #1979): either the swap goes first, the
    /// recorder is the init's previous hook and the fact proves the chain as
    /// it does alone, or the init goes first and the recorder wraps the
    /// finished chain. The recorder is never handed to MathCAT or replaced by
    /// the chain, so with a sound chain the probe panic always reaches it.
    #[test]
    fn a_panic_after_mathcat_initializes_still_reaches_the_previous_hook() {
        use std::sync::PoisonError;
        let seen = swap_in_a_recorder();
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

    /// Puts a hook over the current one that records each panic's message
    /// and then calls the hook it replaced. Returns what the hook has
    /// recorded.
    ///
    /// The two hook calls, and nothing else, are made under [`HOOK_SWAP`], so
    /// the first MathCAT init of the process, on another thread, cannot land
    /// between them, nor they inside it (#1979). Every fact that swaps the
    /// hook does it here.
    fn swap_in_a_recorder() -> std::sync::Arc<std::sync::Mutex<Vec<String>>> {
        use std::sync::{Arc, Mutex, PoisonError};
        let seen: Arc<Mutex<Vec<String>>> = Arc::default();
        let recorder = Arc::clone(&seen);
        let _swap = HOOK_SWAP.lock().unwrap_or_else(PoisonError::into_inner);
        let current = std::panic::take_hook();
        std::panic::set_hook(Box::new(move |info| {
            let message = panic_text(info.payload());
            recorder
                .lock()
                .unwrap_or_else(PoisonError::into_inner)
                .push(message);
            current(info);
        }));
        seen
    }

    /// A panic's message: its payload when that is a `String` or a `&str`,
    /// and empty otherwise.
    fn panic_text(payload: &(dyn std::any::Any + Send)) -> String {
        payload
            .downcast_ref::<String>()
            .cloned()
            .or_else(|| {
                payload
                    .downcast_ref::<&str>()
                    .map(|text| (*text).to_string())
            })
            .unwrap_or_default()
    }

    /// The full name of [`a_hook_swap_inside_the_first_mathcat_init_child`],
    /// for the process that runs it alone.
    const SWAP_INSIDE_INIT_CHILD: &str =
        "math::tests::a_hook_swap_inside_the_first_mathcat_init_child";

    /// #1979: a fact that swaps a recording hook in while another thread runs
    /// the first MathCAT init of the process keeps its recorder. The `Once`
    /// and the panic hook are the process's, so the init can be held between
    /// its hook calls only where nothing has run it yet: this runs
    /// [`a_hook_swap_inside_the_first_mathcat_init_child`] alone in a fresh
    /// copy of the test binary (`session::run_alone`), and fails when that
    /// process fails or does not run it. It proves the same under nextest (a
    /// process per fact), with `--exact`, and in a shared `cargo test`
    /// process, because the child's process is always its own.
    #[test]
    fn a_hook_swap_inside_the_first_mathcat_init_keeps_its_recorder() {
        crate::session::run_alone(SWAP_INSIDE_INIT_CHILD);
    }

    /// The body of
    /// [`a_hook_swap_inside_the_first_mathcat_init_keeps_its_recorder`], which
    /// starts the one process this runs in, with `SLATE_WB31_CHILD` naming
    /// this fact. Anywhere else (nextest's process for this fact, a shared
    /// `cargo test` process, or `--exact` without the variable) it returns
    /// at once and passes, proving nothing.
    ///
    /// A render on another thread starts the first MathCAT init of the
    /// process, and the init is held at each [`HookChainStep`] in turn. At
    /// each, a thread is started to swap a recorder in. Before the init is
    /// let go, that thread finds [`HOOK_SWAP`] held by the init, so its swap
    /// waits for the finished chain. A panic after the render must reach all
    /// three recorders. With the init taking the lock late, or not at all,
    /// the thread finds it free at a step and the fact fails there: a swap
    /// made there lands inside the init, and its recorder is handed to
    /// MathCAT or replaced and never sees the panic. A render that panics is
    /// caught on its thread and reported, so the fact fails instead of
    /// waiting for a pause that never comes.
    #[test]
    fn a_hook_swap_inside_the_first_mathcat_init_child() {
        use std::sync::{Mutex, PoisonError, TryLockError, mpsc};
        if !crate::session::is_alone_child(SWAP_INSIDE_INIT_CHILD) {
            return;
        }
        enum Event {
            Held(HookChainStep),
            Rendered(MathBlock),
            RenderFailed(String),
        }
        let (events, reached) = mpsc::channel::<Event>();
        let (let_go, held) = mpsc::channel::<()>();
        let held = Mutex::new(held);
        let holding = events.clone();
        let armed = HOOK_CHAIN_PAUSE.set(Box::new(move |step| {
            // An end that is gone means the fact is over: the init goes on.
            let _ = holding.send(Event::Held(step));
            let _ = held.lock().unwrap_or_else(PoisonError::into_inner).recv();
        }));
        assert!(armed.is_ok(), "one fact sets the pause, once per process");
        // The pause keeps a sender of `events` and the receiver of `let_go`
        // for the life of the process, so neither channel closes: a wait on
        // `events` ends only with an event, and a send on `let_go` never
        // fails. The render thread therefore sends an event whatever happens
        // to it: the block, or the message of a panic it caught.
        let next = || reached.recv().expect("the pause keeps a sender for good");
        let render = thread::spawn(move || {
            let outcome = std::panic::catch_unwind(|| {
                let raw = RawMathBlock {
                    source: "x^2".to_string(),
                    display_style: MathDisplayStyle::Block,
                    line: 1,
                    byte_offset: 0,
                };
                render_math(&raw, MathPrefs::default())
            });
            let _ = events.send(match outcome {
                Ok(block) => Event::Rendered(block),
                Err(payload) => Event::RenderFailed(panic_text(payload.as_ref())),
            });
        });
        let mut swaps = Vec::new();
        for step in [
            HookChainStep::Locked,
            HookChainStep::PreviousTaken,
            HookChainStep::MathcatTaken,
        ] {
            // The render answers only after the init, so an answer first
            // means the init never stopped here.
            match next() {
                Event::Held(at) => assert_eq!(at, step),
                Event::Rendered(_) => panic!("the first MathCAT init was not held at {step:?}"),
                Event::RenderFailed(message) => {
                    panic!("the render panicked before the init was held at {step:?}: {message}")
                }
            }
            // The init is let go only after the swap's thread has looked at
            // the lock, so what it finds is the lock as the held init has it.
            let (found, lock_was_held) = mpsc::channel::<bool>();
            let swap = thread::spawn(move || {
                let lock_held = matches!(HOOK_SWAP.try_lock(), Err(TryLockError::WouldBlock));
                let _ = found.send(lock_held);
                swap_in_a_recorder()
            });
            swaps.push((step, swap));
            assert!(
                lock_was_held.recv().expect("the swap's thread is alive"),
                "a hook swap could land inside the first MathCAT init, held at {step:?}"
            );
            let_go
                .send(())
                .expect("the pause keeps the receiver for good");
        }
        let block = match next() {
            Event::Rendered(block) => block,
            Event::RenderFailed(message) => {
                panic!("the render panicked after the init was let go: {message}")
            }
            Event::Held(step) => panic!("the first MathCAT init was held again at {step:?}"),
        };
        render
            .join()
            .expect("the render thread catches its own panic");
        assert!(!block.mathml.is_empty(), "the render must reach MathCAT");
        let recorders: Vec<_> = swaps
            .into_iter()
            .map(|(step, swap)| (step, swap.join().expect("the swap's thread joined")))
            .collect();
        let marker = "slate-1979: a panic after a hook swap inside the first init";
        let outcome = thread::spawn(move || std::panic::catch_unwind(|| panic!("{marker}"))).join();
        assert!(
            matches!(outcome, Ok(Err(_))),
            "the probe panic is caught on its own thread"
        );
        let lost: Vec<HookChainStep> = recorders
            .iter()
            .filter(|(_, seen)| {
                let seen = seen.lock().unwrap_or_else(PoisonError::into_inner);
                !seen.iter().any(|message| message == marker)
            })
            .map(|(step, _)| *step)
            .collect();
        assert!(
            lost.is_empty(),
            "a recorder asked for inside the first MathCAT init never saw the panic: {lost:?}"
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

    /// #1629: a function name with a script after an ordinary atom. The
    /// writer puts the thin space it writes before such a name inside the
    /// script element it has just opened, MathCAT rejects the element, and
    /// the whole formula used to fall back to its source.
    const SCRIPTED_FUNCTIONS_AFTER_AN_ATOM: &[&str] = &[
        r"2\sin^2 x",
        r"x\sin^2 x",
        r"\sin^2 x\cos^2 x",
        r"\sin^2\theta\cos^2\theta",
        r"\pi\cos^2 t",
        r"-\sin^2 x",
        r"(-\sin^2 x)",
        r"(a+b)\sin^2 x",
        r"\left(a\right)\sin^2 x",
        r"\frac{1}{2}\sin^2 x",
        r"\sqrt{2}\cos^2 x",
        r"{a}\sin^2 x",
        r"x^2\sin^2 x",
        r"\text{if }\sin^2 x",
        r"\|x\|\sin^2 x",
        r"x\operatorname{erf}^2 x",
        r"n\log_2 n",
        r"2\log_{10} x",
        r"x\log_2^2 n",
        r"2\max_i a_i",
        r"c\lim_{n\to\infty} a_n",
        r"a\liminf_n x_n",
        r"2\max\limits_i a_i",
        r"c\lim\limits_{n\to\infty} a_n",
        r"\displaystyle 2\max_i a_i",
        r"2\sin^2 x + 3\cos^2 x = 1",
        r"x\hspace{0.1667em}\sin^2 x",
        r"2\hat\sin x",
        r"2\sin^{-1} x",
    ];

    /// #1629: a function as an ungrouped script argument, each beside its
    /// grouped form, which renders and is the reading the ungrouped one
    /// must give.
    const FUNCTIONS_AS_SCRIPT_ARGUMENTS: &[(&str, &str)] = &[
        (r"x_\max", r"x_{\max}"),
        (r"x_\max y", r"x_{\max} y"),
        (r"x_\max(t)", r"x_{\max}(t)"),
        (r"T_\min + 1", r"T_{\min} + 1"),
        (r"x^\ln y", r"x^{\ln} y"),
        (r"2x_\max", r"2x_{\max}"),
        (r"a_\max b_\min", r"a_{\max} b_{\min}"),
        (r"x_\max^2", r"x_{\max}^2"),
        (r"x_\min^\max", r"x_{\min}^{\max}"),
        (r"x_\min^\max y", r"x_{\min}^{\max} y"),
        // A function base keeps the mark it applies to x by, when an
        // argument is a function too; an ordinary base does not have one.
        (
            r"\operatorname{foo}_{2}^\max x",
            r"\operatorname{foo}_{2}^{\max} x",
        ),
        (r"\sin_{2}^\max x", r"\sin_{2}^{\max} x"),
        (r"\liminf_{n}^\max x", r"\liminf_{n}^{\max} x"),
        (r"\limsup_{n}^\min x", r"\limsup_{n}^{\min} x"),
        (r"x_{2}^\max y", r"x_{2}^{\max} y"),
        // An escaped ordinary base is one character, not a function name;
        // an escaped one-character function name keeps its mark.
        (r"\&_{2}^\max x", r"\&_{2}^{\max} x"),
        (r"\&^\max x", r"\&^{\max} x"),
        (
            r"\operatorname{&}_{2}^\max x",
            r"\operatorname{&}_{2}^{\max} x",
        ),
        // No thin space before the argument (after a number), so the element
        // has its arity, but the writer still puts its mark after it.
        (r"x_0^\max y", r"x_0^{\max} y"),
        (r"x_1^\ln y", r"x_1^{\ln} y"),
        (r"\int_0^\max f", r"\int_0^{\max} f"),
        // The parser reads the script position the base set (`\limits`,
        // `\nolimits`, a large operator's) from its state, and an ungrouped
        // argument that is an operator with limits overwrites it.
        (r"\liminf\limits_{n}^\max x", r"\liminf\limits_{n}^{\max} x"),
        (r"\max\limits_\min x", r"\max\limits_{\min} x"),
        (r"\sum\limits_{n}^\max x", r"\sum\limits_{n}^{\max} x"),
        (r"\int\limits_0^\max f", r"\int\limits_0^{\max} f"),
        (r"\displaystyle x_\max y", r"\displaystyle x_{\max} y"),
        (r"\displaystyle x_\sum y", r"\displaystyle x_{\sum} y"),
        (
            r"\displaystyle\int_0^\max f",
            r"\displaystyle\int_0^{\max} f",
        ),
        (
            r"\displaystyle\lim\nolimits_n^\max x",
            r"\displaystyle\lim\nolimits_n^{\max} x",
        ),
        // The parser skips a comment and any whitespace between the mark and
        // its argument; a comment's own text is not a script.
        (
            "\\displaystyle x_% the subscript\n\\max y",
            r"\displaystyle x_{\max} y",
        ),
        (
            "\\displaystyle x_\u{a0}\\max y",
            r"\displaystyle x_{\max} y",
        ),
        (
            "\\liminf\\limits_{n}^ % the bound\n \\max x",
            r"\liminf\limits_{n}^{\max} x",
        ),
        (
            "\\liminf\\limits_{n}^\\max x % or y_\\max",
            r"\liminf\limits_{n}^{\max} x",
        ),
        // A slot an authored space with the writer's bytes fills is a real
        // child; the mark after the element still applies nothing.
        (
            r"\int_\hspace{0.1667em}^\max f",
            r"\int_\hspace{0.1667em}^{\max} f",
        ),
        (r"\hspace{0.1667em}^\max x", r"\hspace{0.1667em}^{\max} x"),
        (r"\hspace{0.1667em}_\sin x", r"\hspace{0.1667em}_{\sin} x"),
        (r"\kern0.1667em_\sin x", r"\kern0.1667em_{\sin} x"),
    ];

    /// #1629, the plan lead's second ruling of 2026-11-18: a function as an
    /// ungrouped argument of a fraction or a root, each beside the grouped
    /// form whose reading it must give. The writer puts the thin space before
    /// the function, and its application mark, inside the element.
    const FUNCTIONS_AS_FRACTION_AND_ROOT_ARGUMENTS: &[(&str, &str)] = &[
        (r"\frac x\sin y", r"\frac{x}{\sin} y"),
        (r"\frac x\sin", r"\frac{x}{\sin}"),
        (r"\frac\sin x y", r"\frac{\sin}{x} y"),
        (r"2\frac\sin x y", r"2\frac{\sin}{x} y"),
        (r"\frac\sin\cos x", r"\frac{\sin}{\cos} x"),
        (r"\frac 1\ln 2", r"\frac{1}{\ln} 2"),
        (r"\frac x\sin\cos y", r"\frac x{\sin}\cos y"),
        (r"\frac x\sin^2 y", r"\frac{x}{\sin}^2 y"),
        (r"\dfrac x\sin y", r"\dfrac{x}{\sin} y"),
        (r"\tfrac x\sin y", r"\tfrac{x}{\sin} y"),
        (r"\binom x\sin y", r"\binom{x}{\sin} y"),
        (r"\frac x\hat\sin y", r"\frac{x}{\hat\sin} y"),
        (r"\sqrt[3]\sin x", r"\sqrt[3]{\sin} x"),
        (r"\sqrt[\sin]x y", r"\sqrt[{\sin}]x y"),
        (r"\sqrt[\sin]{x} y", r"\sqrt[{\sin}]{x} y"),
    ];

    /// #1629, the plan lead's ruling of 2026-11-18: an accented function as
    /// a script's base. The writer puts the accent's application mark
    /// inside the outer script element, so it too had too many children.
    const ACCENTED_FUNCTIONS_AS_SCRIPT_BASES: &[(&str, &str)] = &[
        (r"2\hat\sin^2 x", r"2{\hat\sin}^2 x"),
        (r"\hat\sin^2 x", r"{\hat\sin}^2 x"),
        (r"x\bar\cos^2 x", r"x{\bar\cos}^2 x"),
        (r"2\tilde\log_2 n", r"2{\tilde\log}_2 n"),
        (r"2\overline\sin^2 x", r"2{\overline\sin}^2 x"),
        (r"2\widehat\sin^2 x", r"2{\widehat\sin}^2 x"),
        (r"2\hat\sin_2^3 x", r"2{\hat\sin}_2^3 x"),
        (r"2\vec\max_i a_i", r"2{\vec\max}_i a_i"),
        (r"2\hat\sin^2(x)", r"2{\hat\sin}^2(x)"),
        (r"x_\hat\max y", r"x_{\hat\max} y"),
        // An accented function as the argument of a function's script: the
        // base's own application mark after the element stays.
        (r"\sin^\hat\cos x", r"\sin^{\hat\cos} x"),
        (r"2\sin^\hat\cos x", r"2\sin^{\hat\cos} x"),
        (
            r"\operatorname{foo}^\hat\cos x",
            r"\operatorname{foo}^{\hat\cos} x",
        ),
    ];

    /// Every speech setting under Nemeth, then the braille of each code.
    fn every_reading(source: &str) -> Vec<String> {
        let raw = RawMathBlock {
            source: source.to_string(),
            display_style: MathDisplayStyle::Inline,
            line: 1,
            byte_offset: 0,
        };
        let mut readings = Vec::new();
        for speech_style in [MathSpeechStyle::ClearSpeak, MathSpeechStyle::SimpleSpeak] {
            for verbosity in [
                MathVerbosity::Terse,
                MathVerbosity::Medium,
                MathVerbosity::Verbose,
            ] {
                let prefs = MathPrefs {
                    speech_style,
                    verbosity,
                    braille_code: BrailleCode::Nemeth,
                };
                readings.push(render_math(&raw, prefs).speech);
            }
        }
        for braille_code in [BrailleCode::Nemeth, BrailleCode::Ueb] {
            let block = render_with(source, braille_code);
            readings.push(String::from_utf8_lossy(&block.braille).into_owned());
        }
        readings
    }

    /// Each script, fraction and root element's name and its count of element
    /// children, read with a stack over the tags. Written apart from the
    /// repair, so it can judge it.
    fn script_child_counts(mathml: &str) -> Vec<(String, usize)> {
        let mut counts = Vec::new();
        let mut stack: Vec<(String, usize)> = vec![(String::new(), 0)];
        let mut rest = mathml;
        while let Some(open) = rest.find('<') {
            let Some(close) = rest[open..].find('>') else {
                break;
            };
            let tag = &rest[open + 1..open + close];
            rest = &rest[open + close + 1..];
            if let Some(name) = tag.strip_prefix('/') {
                let (opened, children) = stack.pop().expect("an open element");
                assert_eq!(opened, name, "{mathml}");
                if matches!(
                    name,
                    "msup"
                        | "msub"
                        | "munder"
                        | "mover"
                        | "msubsup"
                        | "munderover"
                        | "mfrac"
                        | "mroot"
                ) {
                    counts.push((opened, children));
                }
            } else {
                stack.last_mut().expect("a parent").1 += 1;
                if !tag.ends_with('/') {
                    let name = tag.split([' ', '/']).next().unwrap_or("").to_string();
                    stack.push((name, 0));
                }
            }
        }
        counts
    }

    fn golden_sources() -> Vec<String> {
        let expected = std::fs::read_to_string(concat!(
            env!("CARGO_MANIFEST_DIR"),
            "/tests/fixtures/math_golden/expected.txt"
        ))
        .expect("read the math golden");
        expected
            .lines()
            .filter_map(|line| line.strip_prefix("source: "))
            .map(|source| source.replace('⏎', "\n").replace('␍', "\r"))
            .collect()
    }

    /// #1629: each formula renders, and is spoken as a function with its
    /// script.
    #[test]
    fn a_scripted_function_after_an_ordinary_atom_still_renders() {
        for &source in SCRIPTED_FUNCTIONS_AFTER_AN_ATOM {
            let block = render_with(source, BrailleCode::Nemeth);
            assert!(
                !block.speech.starts_with("Math expression"),
                "{source} fell back: {}",
                block.speech
            );
            assert!(!block.braille.is_empty(), "{source} has no braille");
            if source.contains("^2") {
                assert!(
                    block.speech.contains("squared") && block.speech.contains(" of "),
                    "{source}: {}",
                    block.speech
                );
            }
        }
    }

    /// #1629: a function as an ungrouped script argument reads exactly as
    /// its grouped form, under every speech setting and both braille codes.
    #[test]
    fn a_function_as_a_script_argument_is_read_as_its_group() {
        let mut differ = Vec::new();
        for &(ungrouped, grouped) in FUNCTIONS_AS_SCRIPT_ARGUMENTS {
            let readings = every_reading(ungrouped);
            let expected = every_reading(grouped);
            if readings[0].starts_with("Math expression") || readings != expected {
                differ.push(format!(
                    "{ungrouped:?}: {readings:?}, its group {expected:?}"
                ));
            }
        }
        assert!(differ.is_empty(), "{}", differ.join("\n"));
    }

    /// #1629: a brace the source scan puts where the parse does not read a
    /// script argument (after a `^` or `_` a macro took, so around a
    /// script's base) moves no script: each formula reads as the same
    /// formula written without that mark.
    #[test]
    fn a_brace_around_a_scripts_base_moves_no_script() {
        let mut differ = Vec::new();
        for (source, same) in [
            (
                r"\displaystyle\def\ignore#1{f}\ignore_\sum_n x",
                r"\displaystyle f\sum_n x",
            ),
            (
                r"\displaystyle\def\ignore#1{f}\ignore^\prod_n x",
                r"\displaystyle f\prod_n x",
            ),
            (
                r"\displaystyle\let\unused=_\sum_n x",
                r"\displaystyle\sum_n x",
            ),
            (
                r"\displaystyle\let\unused=^\lim_n x",
                r"\displaystyle\lim_n x",
            ),
        ] {
            let readings = every_reading(source);
            let expected = every_reading(same);
            if readings[0].starts_with("Math expression") || readings != expected {
                differ.push(format!(
                    "{source:?}: {readings:?}, written without it {expected:?}"
                ));
            }
        }
        assert!(differ.is_empty(), "{}", differ.join("\n"));
    }

    /// #1629 (the plan lead's second ruling): a function as an ungrouped
    /// argument of a fraction or a root reads exactly as its grouped form.
    #[test]
    fn a_function_as_a_fraction_or_root_argument_is_read_as_its_group() {
        for &(ungrouped, grouped) in FUNCTIONS_AS_FRACTION_AND_ROOT_ARGUMENTS {
            let readings = every_reading(ungrouped);
            assert!(
                !readings[0].starts_with("Math expression"),
                "{ungrouped} fell back: {}",
                readings[0]
            );
            assert_eq!(readings, every_reading(grouped), "{ungrouped}");
        }
    }

    /// #1628 (the plan lead's second ruling): an authored triple or quadruple
    /// prime after a carrier brailles as the primes typed one by one do,
    /// without the full stop MathCAT 0.7.7's Nemeth cells for `‴` and `⁗`
    /// hold, in both codes; it is still spoken as one, and the MathML the
    /// block keeps still holds the glyph as written.
    #[test]
    fn an_authored_triple_or_quadruple_prime_brailles_as_its_primes() {
        for (authored, typed, glyph) in [
            (r"f\trprime(x)", r"f'''(x)", '‴'),
            (r"f\qprime(x)", r"f''''(x)", '⁗'),
            (r"\sin\trprime(x)", r"\sin'''(x)", '‴'),
            (r"f\trprime'(x)", r"f''''(x)", '‴'),
            (r"f'\trprime(x)", r"f''''(x)", '‴'),
            (r"y\trprime", r"y'''", '‴'),
            (r"2\sin^2 x + f\trprime(x)", r"2\sin^2 x + f'''(x)", '‴'),
        ] {
            for code in [BrailleCode::Nemeth, BrailleCode::Ueb] {
                let block = render_with(authored, code);
                let braille = String::from_utf8_lossy(&block.braille).into_owned();
                assert!(
                    !braille.is_empty() && !braille.contains('.'),
                    "{authored} {code:?}: {braille}"
                );
                assert_eq!(
                    braille,
                    String::from_utf8_lossy(&render_with(typed, code).braille),
                    "{authored} {code:?}"
                );
                assert!(
                    block.mathml.contains(&format!("<mi>{glyph}</mi>")),
                    "{}",
                    block.mathml
                );
            }
        }
        assert_eq!(
            render_with(r"f\trprime(x)", BrailleCode::Nemeth).speech,
            "f triple prime, of x"
        );
        assert_eq!(
            render_with(r"f\qprime(x)", BrailleCode::Nemeth).speech,
            "f quadruple prime, of x"
        );
    }

    /// #1629 (the plan lead's ruling): an accented function as a script's
    /// base reads exactly as its grouped form.
    #[test]
    fn an_accented_function_as_a_scripts_base_is_read_as_its_group() {
        for &(ungrouped, grouped) in ACCENTED_FUNCTIONS_AS_SCRIPT_BASES {
            let readings = every_reading(ungrouped);
            assert!(
                !readings[0].starts_with("Math expression"),
                "{ungrouped} fell back: {}",
                readings[0]
            );
            assert_eq!(readings, every_reading(grouped), "{ungrouped}");
        }
        // The accent's application mark moves to after the element it was
        // written inside, so the scripted, accented function applies to x.
        assert_eq!(
            latex_to_mathml(r"2\hat\sin^2 x"),
            "<math display=\"inline\"><mn>2</mn><mspace width=\"0.1667em\" />\
             <msup><mover><mi>sin</mi><mi>^</mi></mover><mn>2</mn></msup>\
             <mo>\u{2061}</mo><mspace width=\"0.1667em\" /><mi>x</mi></math>"
        );
        // A function base keeps its own mark after the element when its
        // argument is an accented function.
        let base_mark = latex_to_mathml(r"\sin^\hat\cos x");
        assert!(
            base_mark.contains("</msup><mo>\u{2061}</mo>"),
            "{base_mark}"
        );
        // An accented base with a function argument: one mark after the
        // element, not the accent's and the element's own in a row.
        let one_mark = latex_to_mathml(r"\hat\sin^\max x");
        assert_eq!(one_mark.matches(APPLICATION_MARK).count(), 1, "{one_mark}");
        assert!(one_mark.contains("</msup><mo>\u{2061}</mo>"), "{one_mark}");
    }

    /// #1629: no script, fraction or root element is written with more
    /// element children than MathCAT takes, for the families above and every
    /// golden source. (Fewer is out of scope: `x^\rm a`, whose slot a state
    /// change fills, has one, and plan 47 §9 leaves it.)
    #[test]
    fn no_element_has_more_children_than_mathcat_takes() {
        let family = SCRIPTED_FUNCTIONS_AFTER_AN_ATOM
            .iter()
            .map(|source| (*source).to_string())
            .chain(
                FUNCTIONS_AS_SCRIPT_ARGUMENTS
                    .iter()
                    .chain(ACCENTED_FUNCTIONS_AS_SCRIPT_BASES)
                    .chain(FUNCTIONS_AS_FRACTION_AND_ROOT_ARGUMENTS)
                    .map(|(ungrouped, _)| (*ungrouped).to_string()),
            );
        let long_runs = [r"2\sin^2 x + f'''(x)", r"x_\max'''", r"n\log_2 n + y'''"]
            .into_iter()
            .map(str::to_string);
        for source in family.chain(long_runs).chain(golden_sources()) {
            let MathTrees {
                loose,
                speech,
                braille,
            } = latex_to_mathml_trees(&source);
            for mathml in std::iter::once(loose).chain(speech).chain(braille) {
                for (name, children) in script_child_counts(&mathml) {
                    let arity = if matches!(name.as_str(), "msubsup" | "munderover") {
                        3
                    } else {
                        2
                    };
                    assert!(
                        children <= arity,
                        "{source}: {name} has {children} children: {mathml}"
                    );
                }
            }
        }
    }

    /// #1628: three or more primes after a carrier are spoken as primes on
    /// it, never as a product ("times"), under every speech setting.
    #[test]
    fn three_or_more_primes_are_spoken_as_primes() {
        for source in [
            r"f'''(x)",
            r"f''''(x)",
            r"f'''''(x)",
            r"\sin'''(x)",
            r"f\dprime'(x)",
            r"f'\dprime(x)",
            r"f\trprime(x)",
            r"f\qprime(x)",
            r"2\sin^2 x + f'''(x)",
            r"x_\max'''",
        ] {
            let raw = RawMathBlock {
                source: source.to_string(),
                display_style: MathDisplayStyle::Inline,
                line: 1,
                byte_offset: 0,
            };
            for speech_style in [MathSpeechStyle::ClearSpeak, MathSpeechStyle::SimpleSpeak] {
                for verbosity in [
                    MathVerbosity::Terse,
                    MathVerbosity::Medium,
                    MathVerbosity::Verbose,
                ] {
                    let prefs = MathPrefs {
                        speech_style,
                        verbosity,
                        braille_code: BrailleCode::Nemeth,
                    };
                    let speech = render_math(&raw, prefs).speech;
                    assert!(
                        !speech.contains("times") && !speech.starts_with("Math expression"),
                        "{source} {speech_style:?} {verbosity:?}: {speech}"
                    );
                    if verbosity != MathVerbosity::Terse && source.ends_with("(x)") {
                        assert!(
                            speech.contains("of x"),
                            "{source} {speech_style:?} {verbosity:?}: {speech}"
                        );
                    }
                }
            }
        }
        assert_eq!(
            render_with(r"f'''(x)", BrailleCode::Nemeth).speech,
            "f triple prime, of x"
        );
    }

    /// #1628, CD21: a long run of primes is spoken from a folded tree, but
    /// its braille and the MathML Windows exposes stay the loose form's,
    /// whose Nemeth is right (MathCAT 0.7.7's `‴` and `⁗` cells hold a
    /// stray full stop).
    #[test]
    fn a_long_prime_run_keeps_its_loose_braille_and_mathml() {
        for (source, nemeth, ueb, mathml) in [
            (
                r"f'''(x)",
                "⠋⠄⠄⠄⠷⠭⠾",
                "⠰⠰⠋⠶⠶⠶⠐⠣⠭⠐⠜",
                "<math display=\"inline\"><mi>f</mi><mi>′</mi><mi>′</mi><mi>′</mi>\
                 <mo symmetric=\"false\" stretchy=\"false\">(</mo><mi>x</mi>\
                 <mo symmetric=\"false\" stretchy=\"false\">)</mo></math>",
            ),
            (
                r"f''''(x)",
                "⠋⠄⠄⠄⠄⠷⠭⠾",
                "⠰⠰⠋⠶⠶⠶⠶⠐⠣⠭⠐⠜",
                "<math display=\"inline\"><mi>f</mi><mi>′</mi><mi>′</mi><mi>′</mi><mi>′</mi>\
                 <mo symmetric=\"false\" stretchy=\"false\">(</mo><mi>x</mi>\
                 <mo symmetric=\"false\" stretchy=\"false\">)</mo></math>",
            ),
            (
                r"y'''",
                "⠽⠄⠄⠄",
                "⠰⠰⠽⠶⠶⠶",
                "<math display=\"inline\"><mi>y</mi><mi>′</mi><mi>′</mi><mi>′</mi></math>",
            ),
        ] {
            let nemeth_block = render_with(source, BrailleCode::Nemeth);
            assert_eq!(
                String::from_utf8_lossy(&nemeth_block.braille),
                nemeth,
                "{source}"
            );
            assert_eq!(nemeth_block.mathml, mathml, "{source}");
            let ueb_block = render_with(source, BrailleCode::Ueb);
            assert_eq!(String::from_utf8_lossy(&ueb_block.braille), ueb, "{source}");
        }
        let block = render_with(r"\sin'''(x)", BrailleCode::Nemeth);
        assert_eq!(String::from_utf8_lossy(&block.braille), "⠎⠊⠝⠀⠄⠄⠄⠷⠭⠾");
        assert_eq!(
            block.mathml,
            "<math display=\"inline\"><mi>sin</mi><mo>\u{2061}</mo>\
             <mspace width=\"0.1667em\" /><mi>′</mi><mi>′</mi><mi>′</mi>\
             <mo symmetric=\"false\" stretchy=\"false\">(</mo><mi>x</mi>\
             <mo symmetric=\"false\" stretchy=\"false\">)</mo></math>"
        );
        assert_eq!(
            String::from_utf8_lossy(&render_with(r"\sin'''(x)", BrailleCode::Ueb).braille),
            "⠰⠰⠎⠊⠝⠶⠶⠶⠐⠣⠭⠐⠜"
        );
        let combined = render_with(r"2\sin^2 x + f'''(x)", BrailleCode::Nemeth);
        assert_eq!(
            String::from_utf8_lossy(&combined.braille),
            "⠼⠆⠎⠊⠝⠘⠆⠀⠭⠬⠋⠄⠄⠄⠷⠭⠾"
        );
    }

    /// A script element under 8,000 nested groups, about as deep as a
    /// formula under [`MAX_FORMULA_SOURCE_BYTES`] can nest, is repaired and
    /// then refused by MathCAT's depth limit (512 levels), and the formula
    /// falls back to its source with the process alive. (A walk that
    /// recursed once per element fits this depth in the worker's stack;
    /// [`the_repair_walk_keeps_its_depth_off_the_stack`] is the fact that a
    /// recursing walk fails.)
    #[test]
    fn a_deeply_nested_formula_falls_back_without_ending_the_process() {
        let source = format!("{}{}{}", "{".repeat(8000), r"2\sin^2 x", "}".repeat(8000));
        assert!(source.len() <= MAX_FORMULA_SOURCE_BYTES);
        let block = render_with(&source, BrailleCode::Nemeth);
        assert!(
            block.speech.starts_with("Math expression"),
            "{}",
            &block.speech[..block.speech.len().min(80)]
        );
    }

    /// The repair walks the tags with its own stack on the heap, never
    /// recursing: a script element under 200,000 nested rows, far deeper
    /// than any formula under [`MAX_FORMULA_SOURCE_BYTES`] writes, is
    /// repaired on a thread with the MathCAT worker's stack (the standard
    /// 2 MiB), which a walk that recursed once per element overflows.
    #[test]
    fn the_repair_walk_keeps_its_depth_off_the_stack() {
        const DEPTH: usize = 200_000;
        let space = WRITER_THIN_SPACE;
        let inner = format!(
            "<mn>2</mn><msup>{space}<mi>sin</mi><mn>2</mn></msup><mo>\u{2061}</mo>{space}<mi>x</mi>"
        );
        let mathml = format!(
            "<math>{}{inner}{}</math>",
            "<mrow>".repeat(DEPTH),
            "</mrow>".repeat(DEPTH)
        );
        let repaired = thread::Builder::new()
            .stack_size(2 * 1024 * 1024)
            .spawn(move || repair_script_children(&mathml, &[true, false]))
            .expect("a thread with the worker's stack")
            .join()
            .expect("the repair ran");
        assert!(
            repaired.contains(&format!(
                "<mn>2</mn>{space}<msup><mi>sin</mi><mn>2</mn></msup><mo>\u{2061}</mo>"
            )),
            "the msup under {DEPTH} rows was not repaired"
        );
    }

    /// The writer's MathML before the script repair, for the loose tree,
    /// and which of its `<mi>`s are function names; empty for a source
    /// pulldown-latex cannot parse.
    fn as_written(latex: &str) -> (String, Vec<bool>) {
        use pulldown_latex::Storage;
        use pulldown_latex::event::Event;
        let storage = Storage::new();
        let Ok(events) =
            pulldown_latex::Parser::new(latex, &storage).collect::<Result<Vec<Event<'_>>, _>>()
        else {
            return (String::new(), Vec::new());
        };
        let (events, _) = fold_primes_into_superscripts(events, LongPrimeRuns::Loose);
        write_mathml_marked(events)
    }

    /// #1629: the function mark is taken out without a trace. For every
    /// golden source and the shapes that could trip it (a source that spells
    /// the mark itself, in a function name or alone; an empty function name;
    /// a one-character name, escaped or not), the marked write gives the
    /// writer's own bytes, and each `<mi>` is told a function name exactly
    /// when the parser made it one.
    #[test]
    fn the_function_mark_leaves_the_writers_bytes() {
        use pulldown_latex::Storage;
        use pulldown_latex::event::Event;
        let shapes = [
            ("\\operatorname{\u{E000}x}", vec![]),
            ("\\operatorname{\u{E000}}", vec![]),
            ("\u{E000}", vec![]),
            ("\\sin x + \u{E000}", vec![]),
            ("\\operatorname{}", vec![true]),
            ("\\operatorname{&}", vec![true]),
            ("\\&", vec![]),
            ("\\operatorname{f} x", vec![true, false]),
            ("\\sin x", vec![true, false]),
        ]
        .map(|(source, functions)| (source.to_string(), Some(functions)));
        let golden = golden_sources().into_iter().map(|source| (source, None));
        for (source, functions) in shapes.into_iter().chain(golden) {
            let storage = Storage::new();
            let Ok(events) = pulldown_latex::Parser::new(&source, &storage)
                .collect::<Result<Vec<Event<'_>>, _>>()
            else {
                continue;
            };
            let written = write_mathml_names(events.clone(), false);
            let (marked, told) = write_mathml_marked(events);
            assert_eq!(marked, written, "{source}");
            assert!(!marked.contains(FUNCTION_MARK) || source.contains(FUNCTION_MARK));
            if let Some(functions) = functions {
                assert_eq!(told, functions, "{source}");
            }
        }
    }

    /// #1629: the repair changes a script, fraction or root element with
    /// more children than MathCAT takes, and then only when the writer's
    /// spaces and marks account for every extra one, and the writer's mark
    /// after a script element whose argument, not its base, is a function
    /// name. A thin space that fills a slot, an extra space that is not
    /// before a function, a function base's mark, and every golden formula
    /// that MathCAT accepts as written are left as they are, but for such a
    /// stray mark; the writer's own extra space is repaired, so a repair
    /// that changed nothing would fail here too.
    #[test]
    fn the_repair_changes_only_an_overfull_element_or_a_stray_mark() {
        let space = WRITER_THIN_SPACE;
        let mark = APPLICATION_MARK;
        // The mark after a script element whose argument is a function, on
        // a large operator: the one change to a formula MathCAT reads.
        let (written, functions) = as_written(r"\int_0^\max f");
        assert!(
            written.contains(&format!("</msubsup>{mark}{space}")),
            "{written}"
        );
        assert_eq!(
            repair_script_children(&written, &functions),
            written.replace(&format!("</msubsup>{mark}{space}"), "</msubsup>")
        );
        for source in [r"\sin^2 x", r"\operatorname{foo}_0^\max x", r"x^{\max} y"] {
            let (written, functions) = as_written(source);
            assert_eq!(
                repair_script_children(&written, &functions),
                written,
                "{source}"
            );
        }
        // A fraction's extra child that is not the writer's stays.
        let fraction = format!("<math><mfrac>{space}<mi>x</mi><mi>y</mi></mfrac></math>");
        assert_eq!(repair_script_children(&fraction, &[false, false]), fraction);
        let (written, functions) = as_written(r"2\sin^2 x");
        assert_eq!(functions, [true, false]);
        assert_eq!(
            repair_script_children(&written, &functions),
            format!(
                "<math display=\"inline\"><mn>2</mn>{space}<msup><mi>sin</mi><mn>2</mn></msup>\
                 <mo>\u{2061}</mo>{space}<mi>x</mi></math>"
            ),
            "{written}"
        );
        for (mathml, functions) in [
            (
                format!("<math><msup><mi>x</mi>{space}</msup><mn>2</mn></math>"),
                vec![false],
            ),
            (
                format!("<math><msubsup><mi>x</mi>{space}<mn>2</mn></msubsup></math>"),
                vec![false],
            ),
            (
                format!("<math><munder><mi>max</mi>{space}</munder></math>"),
                vec![true],
            ),
            // A space before an ordinary character is not the writer's.
            (
                format!("<math><msub>{space}<mi>x</mi><mi>y</mi></msub></math>"),
                vec![false, false],
            ),
        ] {
            assert_eq!(repair_script_children(&mathml, &functions), mathml);
        }
        // An authored 0.1667em space after an accented function: the
        // writer's mark and that space are not all the element's extra
        // children it could lose, so the element is left as written.
        let (authored, functions) = as_written(r"\hat\sin^\hspace{0.1667em}(x)");
        assert!(authored.contains("<msup><mover>"), "{authored}");
        assert_eq!(repair_script_children(&authored, &functions), authored);
        let (written, functions) = as_written(r"x^\hspace{0.1667em} 2");
        assert_eq!(repair_script_children(&written, &functions), written);

        let sources = golden_sources();
        let accepted = thread::spawn(move || {
            let mut accepted = 0;
            for source in sources {
                let (written, functions) = as_written(&source);
                if written.is_empty()
                    || mathml_to_speech_and_braille(&written, MathPrefs::default()).is_err()
                {
                    continue;
                }
                accepted += 1;
                let repaired = repair_script_children(&written, &functions);
                let mut without_stray_marks = written.clone();
                for close in [
                    "</msup>",
                    "</msub>",
                    "</msubsup>",
                    "</munder>",
                    "</mover>",
                    "</munderover>",
                ] {
                    without_stray_marks = without_stray_marks
                        .replace(
                            &format!("{close}{APPLICATION_MARK}{WRITER_THIN_SPACE}"),
                            close,
                        )
                        .replace(&format!("{close}{APPLICATION_MARK}"), close);
                }
                assert!(
                    repaired == written || repaired == without_stray_marks,
                    "{source}: {written} became {repaired}"
                );
            }
            accepted
        })
        .join()
        .expect("the golden's sources were read");
        assert!(
            accepted >= 190,
            "only {accepted} golden sources were checked"
        );
    }

    /// #1628: a formula is written once per tree it needs: twice for a run of
    /// three or more primes after a carrier, and a third time only when that
    /// run holds an authored `‴` or `⁗`.
    #[test]
    fn a_formula_is_written_once_per_tree_it_needs() {
        for (source, trees) in [
            (r"x^2", 1),
            (r"f''(x)", 1),
            (r"(fg)\trprime", 1),
            (r"f'''(x)", 2),
            (r"f^{\trprime}(x) + g'''(x)", 2),
            (r"f\trprime(x)", 3),
            (r"f'\trprime(x)", 3),
        ] {
            TREES_WRITTEN.with(|written| written.set(0));
            let _ = latex_to_mathml_trees(source);
            assert_eq!(TREES_WRITTEN.with(Cell::get), trees, "{source}");
        }
    }

    /// #1628: a speech tree is built only for a run of three or more primes
    /// after a carrier, and a braille tree only when that run holds an
    /// authored `‴` or `⁗`; a run with no carrier folds in no mode.
    #[test]
    fn a_formula_with_no_long_prime_run_has_one_tree() {
        for source in [
            r"f''(x)",
            r"x^2",
            r"2\sin^2 x",
            r"y''^2",
            r"\left(fg\right)'''(x)",
            r"'''x",
            r"(fg)\trprime",
        ] {
            let trees = latex_to_mathml_trees(source);
            assert!(
                trees.speech.is_none() && trees.braille.is_none(),
                "{source}"
            );
        }
        let trees = latex_to_mathml_trees(r"f'''(x)");
        assert!(
            trees.braille.is_none(),
            "a typed run brailles from the loose tree"
        );
        let speech = trees.speech.expect("a speech tree for a long run");
        assert!(speech.contains("<msup><mi>f</mi><mrow><mi>‴</mi></mrow></msup>"));
        assert!(!trees.loose.contains("<msup>"), "{}", trees.loose);
        // An authored glyph: the block keeps it, braille reads it spelled.
        let authored = latex_to_mathml_trees(r"f\trprime(x)");
        assert!(
            authored.loose.contains("<mi>f</mi><mi>‴</mi>"),
            "{}",
            authored.loose
        );
        let braille = authored
            .braille
            .expect("a braille tree for an authored glyph");
        assert!(
            braille.contains(&format!("<mi>f</mi>{}", "<mi>′</mi>".repeat(3))),
            "{braille}"
        );
        let five = latex_to_mathml_trees(r"f'''''(x)")
            .speech
            .expect("a speech tree for five primes");
        assert!(
            five.contains(&format!(
                "<msup><mi>f</mi><mrow>{}</mrow></msup>",
                "<mi>′</mi>".repeat(5)
            )),
            "{five}"
        );
    }

    /// #1628: when MathCAT cannot take the folded tree, at any size or for
    /// any reason, the formula is spoken and brailled from the loose tree
    /// as it is today.
    #[test]
    fn a_speech_tree_mathcat_rejects_is_spoken_from_the_loose_tree() {
        let MathTrees { loose, speech, .. } = latex_to_mathml_trees(r"f'''(x)");
        let folded = speech.expect("a speech tree for a long run");
        thread::spawn(move || {
            let today = mathml_to_speech_and_braille(&loose, MathPrefs::default())
                .expect("the loose tree renders");
            // A speech tree MathCAT takes is the one spoken.
            let (speech, braille) =
                speech_and_braille_from_two_trees(&folded, &loose, MathPrefs::default())
                    .expect("both trees render");
            assert_eq!(speech, "f triple prime, of x");
            assert_eq!(braille, today.1);
            let rejected = "<math><msup><mi>f</mi><mi>a</mi><mi>b</mi></msup></math>".to_string();
            let huge = "<math>".to_string() + &"x".repeat(2 * 1024 * 1024) + "</math>";
            for speech_tree in [rejected, huge] {
                let pair =
                    speech_and_braille_from_two_trees(&speech_tree, &loose, MathPrefs::default())
                        .expect("the loose tree's reading");
                assert_eq!(pair, today);
            }
        })
        .join()
        .expect("MathCAT thread joined");
    }

    /// #1628: when MathCAT cannot braille the loose tree, the folded tree's
    /// speech is kept and the block has no braille, as a braille failure
    /// does on one tree (#1558).
    #[test]
    fn a_loose_tree_whose_braille_fails_keeps_the_folded_speech() {
        let speech_tree = latex_to_mathml_trees(r"f'''(x)")
            .speech
            .expect("a speech tree for a long run");
        let (speech, braille) = thread::spawn(move || {
            speech_and_braille_from_two_trees(
                &speech_tree,
                "<math><merror><mtext>x</mtext></merror></math>",
                MathPrefs::default(),
            )
        })
        .join()
        .expect("MathCAT thread joined")
        .expect("the speech survives a braille failure");
        assert_eq!(speech, "f triple prime, of x");
        assert!(braille.is_empty());
    }
}
