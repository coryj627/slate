// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Diagram pipeline for Milestone K (#219).
//!
//! Walks a Markdown source for fenced `mermaid` blocks. For each
//! block, attempts to render to SVG via `mermaid-rs-renderer` and
//! generates a structured natural-language description for AT (the
//! sighted-user fallback is the rendered SVG; the AT description is
//! what makes the diagram intelligible at all).
//!
//! The renderer is "early-stage but viable" per the locked
//! architecture decisions (`05` §2.4); failure surfaces as a typed
//! `DiagramRenderStatus::RenderFailed` plus the source text so AT
//! users at least hear the raw Mermaid syntax. We never panic on
//! malformed source.

use std::time::{Duration, Instant};

/// Which diagramming dialect a block uses.
///
/// V1 supports Mermaid only. PlantUML / D2 / Graphviz are V1.x.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum DiagramDialect {
    Mermaid,
}

/// Render outcome.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum DiagramRenderStatus {
    /// `svg` is populated and renders correctly.
    Ok,
    /// The dialect is one we understand but this specific diagram
    /// kind isn't yet supported by the renderer. `reason` gives the
    /// renderer's explanation for surfacing to the user.
    UnsupportedDialect { reason: String },
    /// The renderer threw an error mid-render. Source is preserved
    /// so AT users can still read the raw text.
    RenderFailed { message: String },
}

/// Raw fenced ` ```mermaid ` block before rendering.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RawDiagramBlock {
    pub source: String,
    pub dialect: DiagramDialect,
    /// 1-based line number of the fence opener.
    pub line: u32,
    /// Byte offset of the fence opener in the host source.
    pub byte_offset: u32,
}

/// Rendered diagram block.
///
/// `structured_description` is non-empty even on render failure so
/// AT users always get something to read.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct DiagramBlock {
    pub source: String,
    pub dialect: DiagramDialect,
    pub svg: Option<Vec<u8>>,
    /// Reserved for future PNG fallback path (V1.x). Always `None`
    /// in the current build since the Mac SVG path is well-supported.
    pub png_fallback: Option<Vec<u8>>,
    pub structured_description: String,
    pub render_status: DiagramRenderStatus,
    pub line: u32,
    pub byte_offset: u32,
}

/// Walk `source` and return every Mermaid block in document order.
///
/// Recognises fenced code blocks whose language tag is exactly
/// `mermaid` (case-insensitive). Other code blocks fall through to
/// the code pipeline.
pub fn extract_diagram_blocks(source: &str) -> Vec<RawDiagramBlock> {
    use pulldown_cmark::{CodeBlockKind, Event, Options, Parser as MdParser, Tag, TagEnd};

    let mut out: Vec<RawDiagramBlock> = Vec::new();
    let mut in_mermaid = false;
    let mut current_buffer = String::new();
    let mut current_start: Option<usize> = None;
    // #387: O(n) incremental line numbering — mermaid block starts arrive
    // in document order, so count newlines once over the source.
    let mut lines = crate::line_index::LineTracker::new(source);

    let parser = MdParser::new_ext(source, Options::ENABLE_STRIKETHROUGH).into_offset_iter();
    for (event, range) in parser {
        match event {
            Event::Start(Tag::CodeBlock(CodeBlockKind::Fenced(tag)))
                if tag.trim().eq_ignore_ascii_case("mermaid") =>
            {
                in_mermaid = true;
                current_buffer.clear();
                current_start = Some(range.start);
            }
            Event::End(TagEnd::CodeBlock) if in_mermaid => {
                let start = current_start.take().unwrap_or(0);
                out.push(RawDiagramBlock {
                    source: std::mem::take(&mut current_buffer),
                    dialect: DiagramDialect::Mermaid,
                    line: lines.line_at(start),
                    byte_offset: start as u32,
                });
                in_mermaid = false;
            }
            Event::Text(s) if in_mermaid => {
                current_buffer.push_str(&s);
            }
            _ => {}
        }
    }
    out
}

/// Render a raw block to its full `DiagramBlock` shape.
///
/// SVG rendering is via `mermaid-rs-renderer`; failures surface as
/// `RenderFailed` (never as panics). The structured description is
/// always populated, derived from the source itself even on render
/// failure — that's the AT-facing contract.
pub fn render_diagram(raw: &RawDiagramBlock) -> DiagramBlock {
    render_diagram_within(raw, DiagramBudgets::default())
}

/// [`render_diagram`] under `budgets`: the per-diagram time budget and the
/// abandoned-render cap bound the render itself.
fn render_diagram_within(raw: &RawDiagramBlock, budgets: DiagramBudgets) -> DiagramBlock {
    render_diagram_attempt(raw, budgets, None).block
}

/// One render's block, and whether its outcome is transient: a render
/// refused while earlier renders run on says nothing about the note, so the
/// session does not cache it (#1580).
struct DiagramAttempt {
    block: DiagramBlock,
    transient: bool,
}

/// The render under `budgets` and, inside a note, before the note's render
/// deadline.
fn render_diagram_attempt(
    raw: &RawDiagramBlock,
    budgets: DiagramBudgets,
    note_deadline: Option<Instant>,
) -> DiagramAttempt {
    let description = structured_description(&raw.source, raw.dialect);
    let (svg, status, transient) = match raw.dialect {
        DiagramDialect::Mermaid => {
            render_mermaid_with_validation(&raw.source, budgets, note_deadline)
        }
    };
    DiagramAttempt {
        block: DiagramBlock {
            source: raw.source.clone(),
            dialect: raw.dialect,
            svg,
            png_fallback: None,
            structured_description: description,
            render_status: status,
            line: raw.line,
            byte_offset: raw.byte_offset,
        },
        transient,
    }
}

/// Per-note render budget (W3-3, the W3-2 round-5 precedent applied
/// at design time): reading projections cap at 2,000 blocks, so
/// diagrams past that count can never all be displayed — rendering
/// them through mermaid-rs-renderer is unbounded work a dense
/// adversarial note can weaponize. Over-budget blocks keep source,
/// position, and a real structured description with a typed
/// `RenderFailed` status, so hosts render source-in-range fallbacks —
/// never silently absent.
pub const MAX_RENDERED_DIAGRAMS_PER_NOTE: usize = 2_000;

/// Per-diagram source cap, checked BEFORE the renderer runs. Real
/// Mermaid sources are hundreds of bytes; no legitimate authored
/// diagram approaches 64 KiB.
pub const MAX_DIAGRAM_SOURCE_BYTES: usize = 64 * 1024;

/// Per-diagram OUTPUT cap, checked after the renderer returns
/// (round 1: the source cap does not bound the EXPANSION — a small
/// source can legally render a large SVG, and 2,000 of them would
/// cross the FFI as gigabytes). Real Mermaid SVGs are tens of
/// kilobytes; over-cap output is dropped and the block degrades to
/// the typed shape with description and source intact.
pub const MAX_DIAGRAM_SVG_BYTES: usize = 2 * 1024 * 1024;

/// Aggregate retained-SVG cap per call: the total artifact payload a
/// single `get_diagram_blocks` may marshal. Once drained, later
/// blocks keep description and source but carry no SVG.
pub const MAX_RETAINED_SVG_BYTES_PER_NOTE: usize = 8 * 1024 * 1024;

/// Longest one diagram's render is waited on (#1580, XD3). The render
/// is super-linear in its input with mermaid-rs-renderer 0.3.1 (release,
/// Linux, the issue's measurements): a 500-step chain takes about 1 s,
/// 1,000 steps 3.6 s, 2,000 steps 7.6 s and a 64 KiB chain 17 s. An
/// authored diagram stays far under it; a note built to stall the
/// reading view cannot hold a refresh past it.
pub const MAX_RENDER_TIME_PER_DIAGRAM: Duration = Duration::from_secs(5);

/// Longest one note's renders are waited on in all: four diagrams at the
/// per-diagram budget. A render's wait is cut to what is left of it, and
/// past it the remaining diagrams degrade without rendering.
pub const MAX_RENDER_TIME_PER_NOTE: Duration = Duration::from_secs(20);

/// Most renders abandoned past their budget that may still run in the
/// background. An abandoned render keeps its thread, a core and its
/// memory until it ends, and it shares mermaid-rs-renderer's
/// process-global text measurer (locked per measurement) with every
/// render after it; while this many are still running, a new render is
/// refused instead of adding to them. The check precedes the spawn, so
/// callers rendering at the same moment can each start one more.
pub const MAX_ABANDONED_RENDERS: usize = 2;

/// Most `url(` references one attribute value or `<style>` declaration
/// of a rendered diagram may carry. Mermaid style values (`style`,
/// `classDef`, `linkStyle`) reach the SVG verbatim and every host's SVG
/// decoder parses them. A paint takes one reference and a fallback, so
/// no diagram needs more.
const MAX_STYLE_URL_REFERENCES: usize = 8;

/// Deepest bracket nesting one attribute value or `<style>` block of a
/// rendered diagram may carry, for the same reason.
const MAX_STYLE_VALUE_NESTING: usize = 16;

/// The per-call budget set, injectable for tests.
#[derive(Debug, Clone, Copy)]
struct DiagramBudgets {
    max_rendered: usize,
    max_source_bytes: usize,
    max_svg_bytes: usize,
    max_retained_svg_bytes: usize,
    max_render_time_per_diagram: Duration,
    max_render_time_per_note: Duration,
    max_abandoned_renders: usize,
}

impl Default for DiagramBudgets {
    fn default() -> Self {
        Self {
            max_rendered: MAX_RENDERED_DIAGRAMS_PER_NOTE,
            max_source_bytes: MAX_DIAGRAM_SOURCE_BYTES,
            max_svg_bytes: MAX_DIAGRAM_SVG_BYTES,
            max_retained_svg_bytes: MAX_RETAINED_SVG_BYTES_PER_NOTE,
            max_render_time_per_diagram: MAX_RENDER_TIME_PER_DIAGRAM,
            max_render_time_per_note: MAX_RENDER_TIME_PER_NOTE,
            max_abandoned_renders: MAX_ABANDONED_RENDERS,
        }
    }
}

/// The sentence a block shows when a time budget stopped its render
/// (#1580): `what` is `"diagram"` (this render ran past its budget),
/// `"note"` (the note's diagrams used theirs), `"busy"` (earlier renders
/// are still running past theirs, so this one was not started),
/// `"image"` (an SVG preview's decode ran past its budget, #1581) or
/// `"image-busy"` (earlier decodes are still running past theirs, so this
/// one was not started). Both hosts show it verbatim as the failed state's
/// reason. An unknown `what` reads as `"diagram"`.
pub fn render_time_exceeded_message(what: &str, seconds: u64) -> String {
    let unit = if seconds == 1 { "second" } else { "seconds" };
    match what {
        "note" => format!(
            "Diagram not drawn: this note's diagrams used their {seconds}-second render budget. \
             The source follows."
        ),
        "busy" => format!(
            "Diagram not drawn: earlier diagrams are still rendering past their \
             {seconds}-second budget. The source follows."
        ),
        "image" => format!("Image not shown: decoding took longer than {seconds} {unit}."),
        "image-busy" => format!(
            "Image not shown: earlier images are still decoding past their \
             {seconds}-second budget."
        ),
        _ => format!(
            "Diagram not drawn: rendering took longer than {seconds} {unit}. The source follows."
        ),
    }
}

/// A budget in the whole seconds its sentence names, rounded up so a
/// budget under a second is never called zero.
fn budget_seconds(budget: Duration) -> u64 {
    budget.as_secs() + u64::from(budget.subsec_nanos() > 0)
}

/// Render every extracted block under the per-note budgets — the
/// bounded entry `VaultSession::get_diagram_blocks` uses.
pub fn render_diagram_blocks(raws: &[RawDiagramBlock]) -> Vec<DiagramBlock> {
    render_diagram_blocks_bounded(raws, DiagramBudgets::default())
}

/// A note's rendered blocks, and whether any outcome among them is
/// transient (a render refused while earlier renders ran on, #1580).
pub(crate) struct RenderedDiagrams {
    pub(crate) blocks: Vec<DiagramBlock>,
    pub(crate) transient: bool,
}

/// [`render_diagram_blocks`] for `VaultSession::get_diagram_blocks`, which
/// caches a note's blocks only when nothing among them is transient.
pub(crate) fn render_diagram_blocks_for_cache(raws: &[RawDiagramBlock]) -> RenderedDiagrams {
    render_diagram_blocks_reporting(raws, DiagramBudgets::default())
}

/// Budget mechanism, injectable so the caps are testable without
/// pushing thousands of renders (or megabytes of output) through the
/// renderer. A pathological single render still allocates inside
/// `mermaid-rs-renderer` for its own call — that library exposes no
/// bounded sink — but nothing over-cap is RETAINED or marshalled.
fn render_diagram_blocks_bounded(
    raws: &[RawDiagramBlock],
    budgets: DiagramBudgets,
) -> Vec<DiagramBlock> {
    render_diagram_blocks_reporting(raws, budgets).blocks
}

fn render_diagram_blocks_reporting(
    raws: &[RawDiagramBlock],
    budgets: DiagramBudgets,
) -> RenderedDiagrams {
    let mut retained: usize = 0;
    let mut transient = false;
    // #1580: no render of this note is waited on past its deadline. Each
    // render's wait is the per-diagram budget or what is left of the
    // note's, whichever is shorter, and a render due past the deadline is
    // not started.
    let note_deadline = Instant::now().checked_add(budgets.max_render_time_per_note);
    let blocks = raws
        .iter()
        .enumerate()
        .map(|(index, raw)| {
            if raw.source.len() > budgets.max_source_bytes {
                return budget_degraded(raw, "diagram source exceeds the render budget");
            }
            if index >= budgets.max_rendered {
                return budget_degraded(raw, "diagram count exceeds the per-note render budget");
            }
            let attempt = render_diagram_attempt(raw, budgets, note_deadline);
            transient |= attempt.transient;
            let mut block = attempt.block;
            if let Some(svg) = &block.svg {
                if svg.len() > budgets.max_svg_bytes {
                    return budget_degraded(raw, "diagram output exceeds the render budget");
                }
                if retained + svg.len() > budgets.max_retained_svg_bytes {
                    block.svg = None;
                    block.render_status = DiagramRenderStatus::RenderFailed {
                        message: "diagram output exceeds the per-note retention budget".into(),
                    };
                    return block;
                }
                retained += svg.len();
            }
            block
        })
        .collect();
    RenderedDiagrams { blocks, transient }
}

/// The budget-degradation shape: a typed `RenderFailed` with the
/// description still computed from source (bounded by the Unknown
/// dump cap), so the host fallback contract stays uniform.
fn budget_degraded(raw: &RawDiagramBlock, message: &str) -> DiagramBlock {
    DiagramBlock {
        source: raw.source.clone(),
        dialect: raw.dialect,
        svg: None,
        png_fallback: None,
        structured_description: structured_description(&raw.source, raw.dialect),
        render_status: DiagramRenderStatus::RenderFailed {
            message: message.into(),
        },
        line: raw.line,
        byte_offset: raw.byte_offset,
    }
}

/// AT-facing description for a Mermaid source.
///
/// Reads the source's first non-blank line to classify the diagram
/// kind and the body to count steps / nodes. Doesn't aim for full
/// fidelity (that's a V1.x effort, possibly an upstream contribution
/// to mermaid-rs-renderer per the issue scope note) — the goal is
/// "AT user knows what they're looking at" instead of "image."
pub fn structured_description(source: &str, dialect: DiagramDialect) -> String {
    match dialect {
        DiagramDialect::Mermaid => mermaid_structured_description(source),
    }
}

fn mermaid_structured_description(source: &str) -> String {
    let trimmed = source.trim();
    if trimmed.is_empty() {
        return "Mermaid diagram, empty source.".into();
    }
    // Skip %% directive/comment lines when classifying — the SAME
    // skip render validation applies (Codoki PR #245). Without it a
    // theme-directive diagram rendered fine (status Ok) while its
    // description degraded to the raw source dump, and the body count
    // charged the kind line (W3-3 catch).
    let kind_line_index = trimmed
        .lines()
        .position(|l| !l.trim().is_empty() && !l.trim().starts_with("%%"));
    let kind = match kind_line_index {
        Some(index) => classify_mermaid_kind(
            &trimmed
                .lines()
                .nth(index)
                .unwrap_or("")
                .trim()
                .to_ascii_lowercase(),
        ),
        None => MermaidKind::Unknown,
    };
    let count = trimmed
        .lines()
        .skip(kind_line_index.map_or(0, |index| index + 1))
        .map(str::trim)
        .filter(|l| !l.is_empty() && !l.starts_with("%%"))
        .count();
    match kind {
        MermaidKind::Flowchart => format!(
            "Flowchart with {count} {}.",
            if count == 1 { "step" } else { "steps" }
        ),
        MermaidKind::SequenceDiagram => format!(
            "Sequence diagram with {count} {}.",
            if count == 1 {
                "interaction"
            } else {
                "interactions"
            }
        ),
        MermaidKind::ClassDiagram => format!(
            "Class diagram with {count} {}.",
            if count == 1 {
                "declaration"
            } else {
                "declarations"
            }
        ),
        MermaidKind::StateDiagram => format!(
            "State diagram with {count} {}.",
            if count == 1 {
                "transition"
            } else {
                "transitions"
            }
        ),
        MermaidKind::EntityRelationshipDiagram => format!(
            "Entity-relationship diagram with {count} {}.",
            if count == 1 { "entity" } else { "entities" }
        ),
        MermaidKind::Unknown => {
            // Bounded dump (W3-3): the description crosses the FFI
            // into every AT read as the accessible name; embedding an
            // unbounded source is the defect class the render budgets
            // close. 160 chars keeps "what is this fence" readable.
            const DUMP_CAP: usize = 160;
            let mut dump: String = trimmed.chars().take(DUMP_CAP).collect();
            if trimmed.chars().count() > DUMP_CAP {
                dump.push('…');
            }
            format!("Mermaid diagram, source:\n{dump}")
        }
    }
}

#[derive(Debug, Clone, Copy)]
enum MermaidKind {
    Flowchart,
    SequenceDiagram,
    ClassDiagram,
    StateDiagram,
    EntityRelationshipDiagram,
    Unknown,
}

fn classify_mermaid_kind(first_line_lower: &str) -> MermaidKind {
    if first_line_lower.starts_with("flowchart") || first_line_lower.starts_with("graph") {
        return MermaidKind::Flowchart;
    }
    if first_line_lower.starts_with("sequencediagram") {
        return MermaidKind::SequenceDiagram;
    }
    if first_line_lower.starts_with("classdiagram") {
        return MermaidKind::ClassDiagram;
    }
    if first_line_lower.starts_with("statediagram") {
        return MermaidKind::StateDiagram;
    }
    if first_line_lower.starts_with("erdiagram") {
        return MermaidKind::EntityRelationshipDiagram;
    }
    MermaidKind::Unknown
}

#[derive(Debug)]
enum RenderError {
    Unsupported(String),
    Failed(String),
    /// Refused while earlier renders ran on past their budget: transient.
    Busy(String),
}

use std::sync::atomic::{AtomicBool, AtomicU8, AtomicUsize, Ordering};

/// Renders abandoned past their budget that are still running (#1580).
static ABANDONED_RENDERS: AtomicUsize = AtomicUsize::new(0);

/// Set after the renderer panics, indicating the process-global
/// `TEXT_MEASURER` mutex inside `mermaid-rs-renderer` is poisoned.
/// All subsequent renders will fail, so we short-circuit with a
/// clear message instead of letting them produce opaque errors.
static RENDERER_POISONED: AtomicBool = AtomicBool::new(false);

/// Wrap the renderer call with structural validation of the input
/// (audit #245 M1). `mermaid-rs-renderer` 0.2 returns `Ok(svg)` for
/// any input — including garbage like `@@@ random @@@` — producing
/// small-but-meaningless SVGs. We pre-check that the source's first
/// non-blank line classifies as a known Mermaid diagram kind; if
/// not, route to `UnsupportedDialect` immediately so the user
/// hears "diagram couldn't render" instead of seeing a fake
/// rectangle.
fn render_mermaid_with_validation(
    source: &str,
    budgets: DiagramBudgets,
    note_deadline: Option<Instant>,
) -> (Option<Vec<u8>>, DiagramRenderStatus, bool) {
    // The render stack (`MERMAID_RENDER_STACK_BYTES`) is sized for capped
    // sources, so the cap holds here too, not only in the per-note budget.
    if source.len() > MAX_DIAGRAM_SOURCE_BYTES {
        return (
            None,
            DiagramRenderStatus::RenderFailed {
                message: "diagram source exceeds the render budget".into(),
            },
            false,
        );
    }
    let trimmed = source.trim();
    if trimmed.is_empty() {
        return (
            None,
            DiagramRenderStatus::RenderFailed {
                message: "empty diagram source".into(),
            },
            false,
        );
    }
    // Skip Mermaid directive / comment lines (e.g.
    // `%%{ init: {'theme': 'dark'} }%%` config blocks at the top
    // of a diagram, or `%% standalone comments`) when looking for
    // the kind-declaring first line — Codoki PR #245 catch. Without
    // this skip, every theme-using diagram in the wild reported as
    // UnsupportedDialect.
    let first_line = trimmed
        .lines()
        .map(str::trim)
        .find(|l| !l.is_empty() && !l.starts_with("%%"))
        .unwrap_or("")
        .to_ascii_lowercase();
    if matches!(classify_mermaid_kind(&first_line), MermaidKind::Unknown) {
        return (
            None,
            DiagramRenderStatus::UnsupportedDialect {
                reason: format!("unrecognized Mermaid diagram type (first line: {first_line:?})"),
            },
            false,
        );
    }
    match try_render_mermaid(source, budgets, note_deadline) {
        Ok(svg) => (Some(svg), DiagramRenderStatus::Ok, false),
        Err(RenderError::Unsupported(reason)) => (
            None,
            DiagramRenderStatus::UnsupportedDialect { reason },
            false,
        ),
        Err(RenderError::Failed(message)) => {
            (None, DiagramRenderStatus::RenderFailed { message }, false)
        }
        Err(RenderError::Busy(message)) => {
            (None, DiagramRenderStatus::RenderFailed { message }, true)
        }
    }
}

/// Render a Mermaid source string to SVG bytes.
///
/// Wraps the renderer in `catch_unwind` because early-stage Mermaid
/// renderers have a history of panicking on edge-case input; the
/// pipeline's contract is that bad input becomes a typed
/// `RenderFailed`, not a crashed scanner thread.
///
/// Audit #246: the renderer holds a process-global `Mutex` internally.
/// If a panic occurs while the lock is held, the mutex is poisoned for
/// the rest of the process. We track this via `RENDERER_POISONED` and
/// short-circuit all future renders with a clear message.
fn try_render_mermaid(
    source: &str,
    budgets: DiagramBudgets,
    note_deadline: Option<Instant>,
) -> Result<Vec<u8>, RenderError> {
    if RENDERER_POISONED.load(Ordering::Relaxed) {
        return Err(RenderError::Failed(
            "mermaid renderer is unavailable for the rest of this session \
             (a previous render caused a panic that poisoned the renderer's \
             internal state)"
                .into(),
        ));
    }

    use std::panic::AssertUnwindSafe;
    let result = std::panic::catch_unwind(AssertUnwindSafe(|| {
        mermaid_renderer_render(source, budgets, note_deadline)
    }));
    match result {
        Ok(Ok(svg)) => check_rendered_style_values(&svg)
            .map(|()| svg.into_bytes())
            .map_err(RenderError::Failed),
        // Our own outcome (a budget, the cap, the thread): its message is
        // final, never classified as the renderer's.
        Ok(Err(RendererError::Bounded(message))) => Err(RenderError::Failed(message)),
        Ok(Err(RendererError::Busy(message))) => Err(RenderError::Busy(message)),
        Ok(Err(RendererError::Upstream(msg))) => {
            let lower = msg.to_ascii_lowercase();
            if lower.contains("poison") {
                RENDERER_POISONED.store(true, Ordering::Relaxed);
                Err(RenderError::Failed(
                    "mermaid renderer's internal state was poisoned by a previous \
                     render failure; rendering is unavailable for the rest of this \
                     session"
                        .into(),
                ))
            } else if lower.contains("unsupported") || lower.contains("not implemented") {
                Err(RenderError::Unsupported(msg))
            } else {
                Err(RenderError::Failed(msg))
            }
        }
        Err(panic) => {
            RENDERER_POISONED.store(true, Ordering::Relaxed);
            Err(RenderError::Failed(format!(
                "mermaid renderer panicked: {panic:?}"
            )))
        }
    }
}

/// Stack reserved for each render's own thread. The renderer recurses
/// in proportion to its input, and a host calls in on whatever thread
/// it has: a Swift concurrency worker has 512 KiB. The deepest sources
/// measured under `MAX_DIAGRAM_SOURCE_BYTES` need under 16 MiB with
/// mermaid-rs-renderer 0.3.1. The reservation is address space; pages
/// are committed only as a render touches them.
const MERMAID_RENDER_STACK_BYTES: usize = 256 * 1024 * 1024;

/// How a render ended without an SVG.
#[derive(Debug)]
enum RendererError {
    /// The renderer's own error, stringified: `try_render_mermaid`
    /// classifies it.
    Upstream(String),
    /// Ours: a budget stopped the render, or its thread could not run.
    /// The message is final.
    Bounded(String),
    /// Ours: refused at the abandoned-render cap. Final, and transient.
    Busy(String),
}

/// A render for the facts to stand in for mermaid-rs-renderer: it runs on
/// the render thread, through the channel and the counters, as the real
/// one does. Only renders the setting thread asks for take it, so a test
/// elsewhere in the process renders for real.
#[cfg(test)]
type RenderSeam = (
    std::thread::ThreadId,
    std::sync::Arc<dyn Fn(&str) -> Result<String, String> + Send + Sync>,
);

#[cfg(test)]
static RENDER_SEAM: std::sync::Mutex<Option<RenderSeam>> = std::sync::Mutex::new(None);

/// A render thread's state, shared with the call that waits on it.
const RENDER_RUNNING: u8 = 0;
const RENDER_FINISHED: u8 = 1;
const RENDER_ABANDONED: u8 = 2;

/// Held by a render thread for its whole run, panic included: when the
/// thread ends, a render its caller abandoned releases its slot in
/// `ABANDONED_RENDERS`.
struct RenderSlot(std::sync::Arc<AtomicU8>);

impl Drop for RenderSlot {
    fn drop(&mut self) {
        if self.0.swap(RENDER_FINISHED, Ordering::AcqRel) == RENDER_ABANDONED {
            ABANDONED_RENDERS.fetch_sub(1, Ordering::SeqCst);
        }
    }
}

/// What a render thread sends: the renderer's result, or the payload of
/// its panic.
type RenderThreadOutcome = std::thread::Result<Result<String, String>>;

/// Thin shim around the actual mermaid-rs-renderer call, run on a
/// thread of its own with `MERMAID_RENDER_STACK_BYTES` of stack. The
/// upstream returns an `anyhow::Error` which we don't depend on
/// directly — stringify here so the rest of the module deals with
/// plain `String` messages.
///
/// #1580: the render is waited on for at most the per-diagram budget, or
/// what is left of the note's when that is shorter. Past it the call
/// returns the sentence of the budget that ran out and the thread is
/// abandoned: it runs on, holding a slot in `ABANDONED_RENDERS` until it
/// ends. A render due past the note's deadline, or asked for while
/// `max_abandoned_renders` are still running, is not started.
fn mermaid_renderer_render(
    source: &str,
    budgets: DiagramBudgets,
    note_deadline: Option<Instant>,
) -> Result<String, RendererError> {
    let per_diagram = budgets.max_render_time_per_diagram;
    let note_sentence =
        || render_time_exceeded_message("note", budget_seconds(budgets.max_render_time_per_note));
    let (wait, timed_out) =
        match note_deadline.map(|deadline| deadline.saturating_duration_since(Instant::now())) {
            Some(left) if left.is_zero() => return Err(RendererError::Bounded(note_sentence())),
            Some(left) if left < per_diagram => (left, note_sentence()),
            _ => (
                per_diagram,
                render_time_exceeded_message("diagram", budget_seconds(per_diagram)),
            ),
        };
    if ABANDONED_RENDERS.load(Ordering::SeqCst) >= budgets.max_abandoned_renders {
        return Err(RendererError::Busy(render_time_exceeded_message(
            "busy",
            budget_seconds(per_diagram),
        )));
    }
    let source = source.to_owned();
    #[cfg(test)]
    let seam = RENDER_SEAM
        .lock()
        .unwrap_or_else(std::sync::PoisonError::into_inner)
        .as_ref()
        .filter(|(owner, _)| *owner == std::thread::current().id())
        .map(|(_, seam)| std::sync::Arc::clone(seam));
    let state = std::sync::Arc::new(AtomicU8::new(RENDER_RUNNING));
    let slot = RenderSlot(std::sync::Arc::clone(&state));
    let (sender, receiver) = std::sync::mpsc::sync_channel::<RenderThreadOutcome>(1);
    std::thread::Builder::new()
        .name("slate-diagram-render".into())
        .stack_size(MERMAID_RENDER_STACK_BYTES)
        .spawn(move || {
            // Dropped last, after the send: a caller that sees the render
            // finished finds its outcome already sent.
            let _slot = slot;
            let outcome = std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
                #[cfg(test)]
                if let Some(seam) = &seam {
                    return seam(&source);
                }
                mermaid_rs_renderer::render(&source).map_err(|e| e.to_string())
            }));
            if outcome.is_err() {
                // An abandoned render's panic has no caller to raise it: the
                // poisoning rule (`RENDERER_POISONED`) applies to it as to a
                // waited-on render's.
                RENDERER_POISONED.store(true, Ordering::SeqCst);
            }
            // A caller that abandoned the render has dropped the receiver.
            let _ = sender.send(outcome);
        })
        .map_err(|e| {
            RendererError::Bounded(format!("could not start the diagram renderer: {e}"))
        })?;
    let received = match receiver.recv_timeout(wait) {
        Err(std::sync::mpsc::RecvTimeoutError::Timeout) => {
            // The slot is taken before the render is marked abandoned, so the
            // thread can never release it first.
            ABANDONED_RENDERS.fetch_add(1, Ordering::SeqCst);
            if state
                .compare_exchange(
                    RENDER_RUNNING,
                    RENDER_ABANDONED,
                    Ordering::AcqRel,
                    Ordering::Acquire,
                )
                .is_ok()
            {
                return Err(RendererError::Bounded(timed_out));
            }
            // It finished as the budget ran out: its outcome is already sent.
            ABANDONED_RENDERS.fetch_sub(1, Ordering::SeqCst);
            receiver.recv().ok()
        }
        Err(std::sync::mpsc::RecvTimeoutError::Disconnected) => None,
        Ok(outcome) => Some(outcome),
    };
    match received {
        Some(Ok(result)) => result.map_err(RendererError::Upstream),
        // Raised again here so `try_render_mermaid`'s `catch_unwind` treats a
        // panic on the render thread exactly as one on the caller's.
        Some(Err(panic)) => std::panic::resume_unwind(panic),
        None => Err(RendererError::Bounded(
            "the diagram renderer stopped without a result".into(),
        )),
    }
}

/// Checks every attribute value and every `<style>` declaration of a
/// rendered SVG against `MAX_STYLE_URL_REFERENCES` and
/// `MAX_STYLE_VALUE_NESTING`. The renderer XML-escapes what it writes,
/// so a quoted value never holds its own quote character, and text
/// content (which no decoder parses as paint or CSS) is skipped.
fn check_rendered_style_values(svg: &str) -> Result<(), String> {
    #[derive(Clone, Copy)]
    enum State {
        Text,
        Tag { style: bool },
        Value { quote: u8, style: bool },
        Style,
    }
    let bytes = svg.as_bytes();
    let mut state = State::Text;
    let mut urls = 0;
    let mut depth = 0;
    for (index, &byte) in bytes.iter().enumerate() {
        state = match state {
            State::Text if byte == b'<' => State::Tag {
                style: is_style_open_tag(&bytes[index + 1..]),
            },
            State::Text => State::Text,
            State::Tag { style } => match byte {
                b'"' | b'\'' => {
                    urls = 0;
                    depth = 0;
                    State::Value { quote: byte, style }
                }
                b'>' if style && bytes[index - 1] != b'/' => {
                    urls = 0;
                    depth = 0;
                    State::Style
                }
                b'>' => State::Text,
                _ => State::Tag { style },
            },
            State::Value { quote, style } if byte == quote => State::Tag { style },
            State::Value { .. } => {
                count_style_byte(&bytes[index..], &mut urls, &mut depth)?;
                state
            }
            // Only `</style>` can end the stylesheet: its text is escaped.
            State::Style if byte == b'<' => State::Tag { style: false },
            State::Style => {
                if matches!(byte, b';' | b'{' | b'}') {
                    urls = 0;
                }
                count_style_byte(&bytes[index..], &mut urls, &mut depth)?;
                State::Style
            }
        };
    }
    Ok(())
}

fn is_style_open_tag(after_lt: &[u8]) -> bool {
    after_lt.len() > 5
        && after_lt[..5].eq_ignore_ascii_case(b"style")
        && matches!(after_lt[5], b'>' | b' ' | b'\t' | b'\r' | b'\n' | b'/')
}

/// Counts the byte at the head of `rest` against the two caps.
fn count_style_byte(rest: &[u8], urls: &mut usize, depth: &mut usize) -> Result<(), String> {
    match rest[0] {
        b'(' | b'[' | b'{' => {
            *depth += 1;
            if *depth > MAX_STYLE_VALUE_NESTING {
                return Err(format!(
                    "a style value in this diagram nests brackets more than \
                     {MAX_STYLE_VALUE_NESTING} deep"
                ));
            }
        }
        b')' | b']' | b'}' => *depth = depth.saturating_sub(1),
        b'u' | b'U' if rest.len() >= 4 && rest[..4].eq_ignore_ascii_case(b"url(") => {
            *urls += 1;
            if *urls > MAX_STYLE_URL_REFERENCES {
                return Err(format!(
                    "a style value in this diagram has more than \
                     {MAX_STYLE_URL_REFERENCES} url() references"
                ));
            }
        }
        _ => {}
    }
    Ok(())
}

// --- Tests -------------------------------------------------------------

#[cfg(test)]
fn reset_renderer_poisoned() {
    RENDERER_POISONED.store(false, Ordering::Relaxed);
}

#[cfg(test)]
mod tests {
    use super::*;

    /// Tests that force the process-global poison flag must not overlap with
    /// successful render tests. Production only moves the flag from false to
    /// true; the test-only reset seam is what makes parallel test execution a
    /// potential race.
    static RENDERER_TEST_LOCK: std::sync::Mutex<()> = std::sync::Mutex::new(());

    struct RendererTestGuard {
        _lock: std::sync::MutexGuard<'static, ()>,
    }

    impl Drop for RendererTestGuard {
        fn drop(&mut self) {
            reset_renderer_poisoned();
        }
    }

    fn renderer_test_guard() -> RendererTestGuard {
        let lock = RENDERER_TEST_LOCK
            .lock()
            .unwrap_or_else(|poisoned| poisoned.into_inner());
        reset_renderer_poisoned();
        RendererTestGuard { _lock: lock }
    }

    #[test]
    fn extracts_single_mermaid_block() {
        let src = "intro\n\n```mermaid\nflowchart LR\nA --> B\n```\n\nafter";
        let blocks = extract_diagram_blocks(src);
        assert_eq!(blocks.len(), 1);
        assert!(blocks[0].source.contains("flowchart"));
        assert_eq!(blocks[0].dialect, DiagramDialect::Mermaid);
    }

    #[test]
    fn other_fenced_blocks_are_not_diagram_blocks() {
        let src = "```rust\nfn foo() {}\n```";
        let blocks = extract_diagram_blocks(src);
        assert!(blocks.is_empty());
    }

    #[test]
    fn structured_description_for_flowchart() {
        let src = "flowchart LR\nA --> B\nB --> C\n";
        let desc = structured_description(src, DiagramDialect::Mermaid);
        assert!(desc.starts_with("Flowchart with"), "got {desc:?}");
        assert!(desc.contains("2"));
    }

    #[test]
    fn structured_description_for_sequence_diagram() {
        let src = "sequenceDiagram\nA->>B: Hi\nB->>A: Hello\n";
        let desc = structured_description(src, DiagramDialect::Mermaid);
        assert!(desc.starts_with("Sequence diagram"), "got {desc:?}");
    }

    #[test]
    fn structured_description_for_class_diagram() {
        let src = "classDiagram\nclass Animal\nclass Dog\n";
        let desc = structured_description(src, DiagramDialect::Mermaid);
        assert!(desc.starts_with("Class diagram"), "got {desc:?}");
    }

    #[test]
    fn structured_description_for_state_diagram() {
        let src = "stateDiagram\n[*] --> Idle\nIdle --> Running\n";
        let desc = structured_description(src, DiagramDialect::Mermaid);
        assert!(desc.starts_with("State diagram"), "got {desc:?}");
    }

    #[test]
    fn structured_description_for_er_diagram() {
        let src = "erDiagram\nCUSTOMER ||--o{ ORDER : places\n";
        let desc = structured_description(src, DiagramDialect::Mermaid);
        assert!(
            desc.starts_with("Entity-relationship diagram"),
            "got {desc:?}"
        );
    }

    #[test]
    fn structured_description_for_unknown_kind_falls_back_to_source() {
        let src = "weirdDiagram\nstuff\n";
        let desc = structured_description(src, DiagramDialect::Mermaid);
        assert!(desc.starts_with("Mermaid diagram, source:"), "got {desc:?}");
        assert!(desc.contains("weirdDiagram"));
    }

    #[test]
    fn structured_description_for_empty_source() {
        let desc = structured_description("", DiagramDialect::Mermaid);
        assert_eq!(desc, "Mermaid diagram, empty source.");
    }

    /// W3-3 catch: render validation skips `%%` directive lines when
    /// classifying, but the description generator did not — a
    /// theme-directive diagram rendered with status Ok while its
    /// AT-facing description degraded to the raw source dump, and the
    /// body count charged the kind line. Both walks now share the
    /// same skip.
    #[test]
    fn structured_description_skips_directive_lines_like_render_validation() {
        let src = "%%{ init: { 'theme': 'dark' } }%%\nflowchart LR\nA --> B\nB --> C\n";
        let desc = structured_description(src, DiagramDialect::Mermaid);
        assert_eq!(desc, "Flowchart with 2 steps.");
    }

    /// The Unknown-kind dump is bounded: the description is the
    /// accessible NAME on both hosts, and an unbounded source embed
    /// is the defect class the render budgets close.
    #[test]
    fn structured_description_unknown_dump_is_bounded() {
        let src = format!("weirdDiagram\n{}", "x".repeat(10_000));
        let desc = structured_description(&src, DiagramDialect::Mermaid);
        assert!(
            desc.starts_with("Mermaid diagram, source:"),
            "got prefix {:?}",
            &desc[..40]
        );
        assert!(desc.ends_with('…'), "over-cap dump must show truncation");
        assert!(
            desc.chars().count() < 220,
            "got {} chars",
            desc.chars().count()
        );
    }

    /// W3-3 render budgets (the W3-2 round-5 precedent applied at
    /// design time): blocks past the count cap degrade to a typed
    /// RenderFailed with source, position, and description intact —
    /// never dropped, never rendered. Injectable budget keeps the
    /// test off the real renderer; the production cap is pinned.
    #[test]
    fn render_budget_degrades_past_the_diagram_count_cap() {
        let _guard = renderer_test_guard();
        let raws: Vec<RawDiagramBlock> = (0..4)
            .map(|i| RawDiagramBlock {
                source: "flowchart LR\nA --> B".to_string(),
                dialect: DiagramDialect::Mermaid,
                line: i as u32 + 1,
                byte_offset: i as u32 * 32,
            })
            .collect();
        let blocks = render_diagram_blocks_bounded(
            &raws,
            DiagramBudgets {
                max_rendered: 2,
                ..DiagramBudgets::default()
            },
        );
        assert_eq!(blocks.len(), 4, "over-budget blocks degrade, never drop");
        assert!(matches!(blocks[1].render_status, DiagramRenderStatus::Ok));
        assert!(blocks[1].svg.is_some());
        for block in &blocks[2..] {
            assert!(matches!(
                block.render_status,
                DiagramRenderStatus::RenderFailed { .. }
            ));
            assert!(block.svg.is_none());
            assert_eq!(block.structured_description, "Flowchart with 1 step.");
            assert_eq!(block.source, "flowchart LR\nA --> B");
        }
        assert_eq!(blocks[3].byte_offset, 96);
        assert_eq!(MAX_RENDERED_DIAGRAMS_PER_NOTE, 2_000);
    }

    /// Round 1 [high]: the source cap does not bound the EXPANSION —
    /// per-SVG output and aggregate retention caps stop unbounded
    /// artifact bytes from being retained or marshalled. Injectable
    /// budgets keep the fixture small; production values are pinned.
    #[test]
    fn render_budget_bounds_svg_output_and_retention() {
        let _guard = renderer_test_guard();
        let raws: Vec<RawDiagramBlock> = (0..3)
            .map(|i| RawDiagramBlock {
                source: "flowchart LR\nA --> B".to_string(),
                dialect: DiagramDialect::Mermaid,
                line: i as u32 + 1,
                byte_offset: i as u32 * 32,
            })
            .collect();
        let probe = render_diagram(&raws[0]);
        let svg_len = probe.svg.as_ref().expect("probe renders").len();

        // Per-SVG cap below the real output: every block degrades to
        // the typed shape and nothing is retained.
        let capped = render_diagram_blocks_bounded(
            &raws,
            DiagramBudgets {
                max_svg_bytes: svg_len - 1,
                ..DiagramBudgets::default()
            },
        );
        for block in &capped {
            assert!(block.svg.is_none());
            assert!(matches!(
                block.render_status,
                DiagramRenderStatus::RenderFailed { .. }
            ));
            assert_eq!(block.structured_description, "Flowchart with 1 step.");
        }

        // Aggregate retention sized for exactly two: the third keeps
        // description and source but marshals no SVG.
        let retained = render_diagram_blocks_bounded(
            &raws,
            DiagramBudgets {
                max_retained_svg_bytes: (svg_len * 2) + (svg_len / 2),
                ..DiagramBudgets::default()
            },
        );
        assert!(retained[0].svg.is_some());
        assert!(retained[1].svg.is_some());
        assert!(retained[2].svg.is_none());
        assert!(matches!(
            retained[2].render_status,
            DiagramRenderStatus::RenderFailed { .. }
        ));
        assert_eq!(retained[2].structured_description, "Flowchart with 1 step.");
        assert_eq!(retained[2].source, "flowchart LR\nA --> B");

        // Production caps stay coherent: one SVG can never exceed the
        // note-wide retention pool. (assert_eq keeps clippy's
        // assertions-on-constants lint quiet — these are deliberate
        // constant pins.)
        assert_eq!(MAX_DIAGRAM_SVG_BYTES, 2 * 1024 * 1024);
        assert_eq!(MAX_RETAINED_SVG_BYTES_PER_NOTE, 8 * 1024 * 1024);
        assert_eq!(
            MAX_RETAINED_SVG_BYTES_PER_NOTE / MAX_DIAGRAM_SVG_BYTES,
            4,
            "one SVG must never exceed the note-wide retention pool"
        );
    }

    /// An oversized source degrades BEFORE the renderer runs — and
    /// only that block: neighbors render normally.
    #[test]
    fn render_budget_degrades_oversized_diagrams_individually() {
        let _guard = renderer_test_guard();
        let oversized = RawDiagramBlock {
            source: format!("flowchart LR\n{}", "A --> B\n".repeat(9_000)),
            dialect: DiagramDialect::Mermaid,
            line: 1,
            byte_offset: 0,
        };
        assert!(oversized.source.len() > MAX_DIAGRAM_SOURCE_BYTES);
        let small = RawDiagramBlock {
            source: "flowchart LR\nA --> B".to_string(),
            dialect: DiagramDialect::Mermaid,
            line: 100,
            byte_offset: 80_000,
        };
        let blocks = render_diagram_blocks(&[oversized, small]);
        assert!(matches!(
            blocks[0].render_status,
            DiagramRenderStatus::RenderFailed { .. }
        ));
        assert!(blocks[0].svg.is_none());
        assert!(matches!(blocks[1].render_status, DiagramRenderStatus::Ok));
        assert!(blocks[1].svg.is_some(), "neighbor must render normally");
    }

    #[test]
    fn render_diagram_populates_description_even_on_malformed_source() {
        let raw = RawDiagramBlock {
            source: "@@@ garbage @@@".to_string(),
            dialect: DiagramDialect::Mermaid,
            line: 1,
            byte_offset: 0,
        };
        let block = render_diagram(&raw);
        // Whichever way the renderer reacted, the structured
        // description has to be non-empty so AT users have content.
        assert!(!block.structured_description.is_empty());
    }

    /// Audit #245 M1: mermaid-rs-renderer 0.2 accepts ANY input
    /// (returns `Ok(svg)` even for garbage). Pre-validate that the
    /// source's first non-blank line classifies as a known Mermaid
    /// kind before accepting the renderer's output — otherwise route
    /// to `UnsupportedDialect`.
    #[test]
    fn render_garbage_routes_to_unsupported_dialect_not_ok() {
        let raw = RawDiagramBlock {
            source: "@@@ garbage @@@".to_string(),
            dialect: DiagramDialect::Mermaid,
            line: 1,
            byte_offset: 0,
        };
        let block = render_diagram(&raw);
        match block.render_status {
            DiagramRenderStatus::UnsupportedDialect { .. } => {}
            other => panic!("expected UnsupportedDialect for unknown first-line; got {other:?}"),
        }
        assert!(block.svg.is_none(), "garbage input must not produce SVG");
    }

    #[test]
    fn render_empty_source_routes_to_render_failed() {
        let raw = RawDiagramBlock {
            source: "".to_string(),
            dialect: DiagramDialect::Mermaid,
            line: 1,
            byte_offset: 0,
        };
        let block = render_diagram(&raw);
        match block.render_status {
            DiagramRenderStatus::RenderFailed { .. } => {}
            other => panic!("expected RenderFailed for empty source; got {other:?}"),
        }
    }

    #[test]
    fn render_valid_flowchart_yields_ok_status() {
        let _guard = renderer_test_guard();
        let raw = RawDiagramBlock {
            source: "flowchart LR\nA --> B\n".to_string(),
            dialect: DiagramDialect::Mermaid,
            line: 1,
            byte_offset: 0,
        };
        let block = render_diagram(&raw);
        match block.render_status {
            DiagramRenderStatus::Ok => {}
            other => panic!("expected Ok for valid flowchart; got {other:?}"),
        }
        assert!(block.svg.is_some());
    }

    /// Codoki polish on M1: Mermaid theme/init directives at the
    /// top of a diagram look like `%%{ init: {...} }%%`. The
    /// validation must skip them and find the actual kind-
    /// declaring line below — otherwise every themed diagram
    /// in the wild reports as UnsupportedDialect.
    #[test]
    fn render_skips_init_directive_when_classifying() {
        let _guard = renderer_test_guard();
        let raw = RawDiagramBlock {
            source: "%%{ init: { 'theme': 'dark' } }%%\nflowchart LR\nA --> B\n".to_string(),
            dialect: DiagramDialect::Mermaid,
            line: 1,
            byte_offset: 0,
        };
        let block = render_diagram(&raw);
        match block.render_status {
            DiagramRenderStatus::Ok => {}
            other => panic!("themed flowchart should classify as Ok; got {other:?}"),
        }
    }

    #[test]
    fn render_skips_comment_lines_when_classifying() {
        let _guard = renderer_test_guard();
        let raw = RawDiagramBlock {
            source: "%% top-level comment\n%% another\nclassDiagram\nclass Foo\n".to_string(),
            dialect: DiagramDialect::Mermaid,
            line: 1,
            byte_offset: 0,
        };
        let block = render_diagram(&raw);
        match block.render_status {
            DiagramRenderStatus::Ok => {}
            other => {
                panic!("leading comment-only lines should not block kind detection; got {other:?}")
            }
        }
    }

    /// Audit #246: when the renderer's internal mutex is poisoned,
    /// subsequent renders short-circuit with a clear message instead
    /// of producing opaque PoisonError failures.
    #[test]
    fn poisoned_renderer_short_circuits_with_clear_message() {
        let _guard = renderer_test_guard();
        RENDERER_POISONED.store(true, Ordering::Relaxed);
        let result = try_render_mermaid("flowchart LR\nA --> B\n", DiagramBudgets::default(), None);
        match result {
            Err(RenderError::Failed(msg)) => {
                assert!(
                    msg.contains("unavailable for the rest of this session"),
                    "expected clear poison message; got {msg:?}"
                );
            }
            other => panic!("expected RenderError::Failed for poisoned renderer; got {other:?}"),
        }
    }

    /// Audit #246: the poisoned flag integrates through
    /// `render_diagram` — a valid flowchart still gets a structured
    /// description even when the renderer is poisoned.
    #[test]
    fn poisoned_renderer_still_produces_structured_description() {
        let _guard = renderer_test_guard();
        RENDERER_POISONED.store(true, Ordering::Relaxed);
        let raw = RawDiagramBlock {
            source: "flowchart LR\nA --> B\nB --> C\n".to_string(),
            dialect: DiagramDialect::Mermaid,
            line: 1,
            byte_offset: 0,
        };
        let block = render_diagram(&raw);
        assert!(
            !block.structured_description.is_empty(),
            "AT users must still get a description even when rendering is poisoned"
        );
        assert!(block.svg.is_none());
        match block.render_status {
            DiagramRenderStatus::RenderFailed { ref message } => {
                assert!(message.contains("unavailable"), "got {message:?}");
            }
            other => panic!("expected RenderFailed; got {other:?}"),
        }
    }

    /// Audit #246: if the library itself returns a PoisonError (rather
    /// than panicking), we detect it and set the poisoned flag.
    #[test]
    fn poison_error_in_library_response_sets_flag() {
        let _guard = renderer_test_guard();
        assert!(!RENDERER_POISONED.load(Ordering::Relaxed));
        let lower = "mutex poisonerror: poisoned".to_ascii_lowercase();
        assert!(lower.contains("poison"));
    }

    fn mermaid(source: String) -> RawDiagramBlock {
        RawDiagramBlock {
            source,
            dialect: DiagramDialect::Mermaid,
            line: 1,
            byte_offset: 0,
        }
    }

    /// The renderer recurses in proportion to its input: in
    /// mermaid-rs-renderer 0.3.1 a 300-step chain overflows a 64 KiB
    /// stack. A host calls in on whatever thread it has, so the render
    /// must run on a stack of its own.
    #[test]
    fn render_runs_on_its_own_stack_whatever_the_caller_has() {
        let _guard = renderer_test_guard();
        let source: String = std::iter::once("flowchart TD\n".to_string())
            .chain((0..300).map(|i| format!("n{i}-->n{}\n", i + 1)))
            .collect();
        let raw = mermaid(source);
        // A generous time budget: this fact is about the stack, and an
        // unoptimised build renders slowly.
        let block = std::thread::Builder::new()
            .stack_size(64 * 1024)
            .spawn(move || render_diagram_within(&raw, budgets_with(Duration::from_secs(120))))
            .expect("spawn a caller with a 64 KiB stack")
            .join()
            .expect("the caller finished");
        assert_eq!(block.render_status, DiagramRenderStatus::Ok);
        assert!(block.svg.is_some());
    }

    /// The source cap holds inside the render itself, not only in the
    /// per-note budget: the render stack is sized for capped sources.
    #[test]
    fn render_diagram_refuses_a_source_over_the_cap_itself() {
        let _guard = renderer_test_guard();
        let mut source = String::from("flowchart TD\nA --> B\n");
        while source.len() <= MAX_DIAGRAM_SOURCE_BYTES {
            source.push_str("%% padding past the source cap\n");
        }
        let block = render_diagram(&mermaid(source));
        assert!(block.svg.is_none());
        assert_eq!(
            block.render_status,
            DiagramRenderStatus::RenderFailed {
                message: "diagram source exceeds the render budget".into()
            }
        );
    }

    /// `style`, `classDef` and `linkStyle` values reach the SVG
    /// verbatim. One past the cap is refused, with a message that says
    /// why; the cap itself still renders.
    #[test]
    fn render_refuses_a_style_value_past_the_url_reference_cap() {
        let _guard = renderer_test_guard();
        let templates = [
            "flowchart TD\nA\nstyle A fill:{value}\n",
            "flowchart TD\nA:::hot\nclassDef hot fill:{value}\n",
            "flowchart TD\nA-->B\nlinkStyle 0 stroke:{value}\n",
        ];
        for template in templates {
            let at_cap = template.replace("{value}", &"url(#a)".repeat(MAX_STYLE_URL_REFERENCES));
            let block = render_diagram(&mermaid(at_cap));
            assert_eq!(block.render_status, DiagramRenderStatus::Ok, "{template}");

            let over = template.replace("{value}", &"url(#a)".repeat(MAX_STYLE_URL_REFERENCES + 1));
            let block = render_diagram(&mermaid(over));
            assert!(block.svg.is_none(), "{template}");
            assert_eq!(
                block.render_status,
                DiagramRenderStatus::RenderFailed {
                    message: format!(
                        "a style value in this diagram has more than \
                         {MAX_STYLE_URL_REFERENCES} url() references"
                    )
                },
                "{template}"
            );
        }
    }

    #[test]
    fn render_refuses_a_style_value_nested_past_the_cap() {
        let _guard = renderer_test_guard();
        // `fill` reaches the SVG verbatim up to its first comma (Mermaid
        // separates style properties with commas); `stroke-width` is
        // parsed into a number and never would.
        let nested = |depth: usize| {
            format!(
                "flowchart TD\nA\nstyle A fill:{}red{}\n",
                "calc(".repeat(depth),
                ")".repeat(depth)
            )
        };
        let block = render_diagram(&mermaid(nested(MAX_STYLE_VALUE_NESTING)));
        assert_eq!(block.render_status, DiagramRenderStatus::Ok);

        let block = render_diagram(&mermaid(nested(MAX_STYLE_VALUE_NESTING + 1)));
        assert!(block.svg.is_none());
        assert_eq!(
            block.render_status,
            DiagramRenderStatus::RenderFailed {
                message: format!(
                    "a style value in this diagram nests brackets more than \
                     {MAX_STYLE_VALUE_NESTING} deep"
                )
            }
        );
    }

    /// The check reads attribute values and `<style>` declarations, the
    /// places a decoder parses as paint or CSS. Text content is not one
    /// of them, and each declaration is counted on its own.
    #[test]
    fn style_value_check_reads_values_and_declarations_not_text() {
        let passes =
            |body: String| check_rendered_style_values(&format!("<svg>{body}</svg>")).is_ok();
        let urls = |n: usize| "url(#a)".repeat(n);
        let brackets = |n: usize| format!("{}{}", "(".repeat(n), ")".repeat(n));
        let over = MAX_STYLE_URL_REFERENCES + 1;

        assert!(!passes(format!("<rect fill=\"{}\"/>", urls(over))));
        assert!(!passes(format!("<rect fill='{}'/>", urls(over))));
        assert!(!passes(format!("<style>a{{fill:{}}}</style>", urls(over))));
        // Each declaration counts on its own.
        let declarations = vec![format!("fill:{}", urls(MAX_STYLE_URL_REFERENCES)); 4].join(";");
        assert!(passes(format!("<style>a{{{declarations}}}</style>")));

        assert!(passes(format!(
            "<rect fill=\"{}\"/>",
            brackets(MAX_STYLE_VALUE_NESTING)
        )));
        assert!(!passes(format!(
            "<rect fill=\"{}\"/>",
            brackets(MAX_STYLE_VALUE_NESTING + 1)
        )));
        // Blocks nest like brackets.
        let blocks = format!(
            "{}a{{fill:red}}{}",
            "@media all{".repeat(MAX_STYLE_VALUE_NESTING),
            "}".repeat(MAX_STYLE_VALUE_NESTING)
        );
        assert!(!passes(format!("<style>{blocks}</style>")));

        // Text content is neither.
        assert!(passes(format!(
            "<text>{}{}</text>",
            urls(over * 10),
            brackets(100)
        )));
    }

    // --- Render time budgets (#1580) ---------------------------------------

    /// The render seam for one fact, cleared when the fact ends.
    struct SeamGuard;

    impl Drop for SeamGuard {
        fn drop(&mut self) {
            *RENDER_SEAM
                .lock()
                .unwrap_or_else(std::sync::PoisonError::into_inner) = None;
        }
    }

    /// Sets the seam for renders this thread asks for.
    fn render_seam(
        seam: impl Fn(&str) -> Result<String, String> + Send + Sync + 'static,
    ) -> SeamGuard {
        *RENDER_SEAM
            .lock()
            .unwrap_or_else(std::sync::PoisonError::into_inner) =
            Some((std::thread::current().id(), std::sync::Arc::new(seam)));
        SeamGuard
    }

    /// Opens a parked seam's gate when the fact ends, so a failed assertion
    /// cannot leave its renders parked, and abandoned, for every later fact.
    struct GateGuard(std::sync::Arc<AtomicBool>);

    impl Drop for GateGuard {
        fn drop(&mut self) {
            self.0.store(true, Ordering::SeqCst);
        }
    }

    /// Waits up to 2 s for `count` to reach `target`.
    fn reaches(count: &AtomicUsize, target: usize) -> bool {
        let deadline = Instant::now() + Duration::from_secs(2);
        while count.load(Ordering::SeqCst) < target {
            if Instant::now() >= deadline {
                return false;
            }
            std::thread::sleep(Duration::from_millis(5));
        }
        true
    }

    /// What a seam render returns: a small SVG with no style values.
    const SEAM_SVG: &str = "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>";

    fn flowchart() -> RawDiagramBlock {
        mermaid("flowchart LR\nA --> B".to_string())
    }

    fn budgets_with(per_diagram: Duration) -> DiagramBudgets {
        DiagramBudgets {
            max_render_time_per_diagram: per_diagram,
            ..DiagramBudgets::default()
        }
    }

    /// Every abandoned render finished and released its slot, within 2 s.
    fn abandoned_renders_settle() -> bool {
        let deadline = Instant::now() + Duration::from_secs(2);
        while ABANDONED_RENDERS.load(Ordering::SeqCst) != 0 {
            if Instant::now() >= deadline {
                return false;
            }
            std::thread::sleep(Duration::from_millis(5));
        }
        true
    }

    /// #1580: a render past the per-diagram budget stops being waited on.
    /// The block degrades with core's sentence while the render runs on,
    /// abandoned, and the render releases its slot when it ends.
    #[test]
    fn a_render_past_the_per_diagram_budget_degrades_with_the_budget_sentence() {
        let _guard = renderer_test_guard();
        let _seam = render_seam(|_| {
            std::thread::sleep(Duration::from_secs(1));
            Ok(SEAM_SVG.to_string())
        });
        let started = Instant::now();
        let block = render_diagram_within(&flowchart(), budgets_with(Duration::from_millis(100)));
        let waited = started.elapsed();
        assert!(
            waited < Duration::from_millis(800),
            "the render was waited on for {waited:?}"
        );
        assert!(block.svg.is_none());
        assert_eq!(
            block.render_status,
            DiagramRenderStatus::RenderFailed {
                message: render_time_exceeded_message("diagram", 1)
            }
        );
        assert_eq!(block.structured_description, "Flowchart with 1 step.");
        assert_eq!(block.source, "flowchart LR\nA --> B");
        assert_eq!(
            ABANDONED_RENDERS.load(Ordering::SeqCst),
            1,
            "the render runs on, abandoned"
        );
        assert!(
            abandoned_renders_settle(),
            "the abandoned render never released its slot"
        );
    }

    #[test]
    fn a_render_within_the_budget_is_unchanged() {
        let _guard = renderer_test_guard();
        let _seam = render_seam(|_| Ok(SEAM_SVG.to_string()));
        let block = render_diagram_within(&flowchart(), budgets_with(Duration::from_secs(5)));
        assert_eq!(block.render_status, DiagramRenderStatus::Ok);
        assert_eq!(block.svg.as_deref(), Some(SEAM_SVG.as_bytes()));
        assert_eq!(ABANDONED_RENDERS.load(Ordering::SeqCst), 0);
    }

    /// Once a note's renders have used its budget, the remaining diagrams
    /// degrade with the note's sentence and are never handed to the renderer.
    #[test]
    fn a_note_past_its_render_budget_degrades_the_rest_without_rendering() {
        let _guard = renderer_test_guard();
        let calls = std::sync::Arc::new(AtomicUsize::new(0));
        let counted = std::sync::Arc::clone(&calls);
        let _seam = render_seam(move |_| {
            counted.fetch_add(1, Ordering::SeqCst);
            std::thread::sleep(Duration::from_millis(400));
            Ok(SEAM_SVG.to_string())
        });
        let raws = vec![flowchart(); 3];
        let blocks = render_diagram_blocks_bounded(
            &raws,
            DiagramBudgets {
                max_render_time_per_note: Duration::from_millis(600),
                ..DiagramBudgets::default()
            },
        );
        let note = DiagramRenderStatus::RenderFailed {
            message: render_time_exceeded_message("note", 1),
        };
        assert_eq!(blocks.len(), 3, "a degraded block is never dropped");
        assert_eq!(blocks[0].render_status, DiagramRenderStatus::Ok);
        // The second render starts before the deadline and is waited on
        // only for what is left of it; the third is due past it.
        assert_eq!(blocks[1].render_status, note);
        assert_eq!(blocks[2].render_status, note);
        assert!(blocks[2].svg.is_none());
        assert_eq!(blocks[2].structured_description, "Flowchart with 1 step.");
        assert_eq!(
            calls.load(Ordering::SeqCst),
            2,
            "a block past the note's deadline was handed to the renderer"
        );
        assert!(abandoned_renders_settle());
    }

    /// A render that starts close to the note's deadline is waited on only
    /// for what is left of it, and says so with the note's sentence: no
    /// note holds a refresh past its budget.
    #[test]
    fn a_render_near_the_note_deadline_waits_only_for_what_is_left() {
        let _guard = renderer_test_guard();
        let _seam = render_seam(|_| {
            std::thread::sleep(Duration::from_secs(1));
            Ok(SEAM_SVG.to_string())
        });
        let started = Instant::now();
        let blocks = render_diagram_blocks_bounded(
            &[flowchart()],
            DiagramBudgets {
                max_render_time_per_note: Duration::from_millis(150),
                ..DiagramBudgets::default()
            },
        );
        let waited = started.elapsed();
        assert!(
            waited < Duration::from_millis(800),
            "the note's render was waited on for {waited:?}"
        );
        assert_eq!(
            blocks[0].render_status,
            DiagramRenderStatus::RenderFailed {
                message: render_time_exceeded_message("note", 1)
            }
        );
        assert!(abandoned_renders_settle());
    }

    /// Past its deadline a note's unsupported or empty block keeps its own
    /// reason: the deadline is checked where a render would start.
    #[test]
    fn a_block_past_the_note_deadline_keeps_a_reason_of_its_own() {
        let _guard = renderer_test_guard();
        let blocks = render_diagram_blocks_bounded(
            &[
                mermaid("weirdDiagram\nstuff".to_string()),
                mermaid(String::new()),
            ],
            DiagramBudgets {
                max_render_time_per_note: Duration::ZERO,
                ..DiagramBudgets::default()
            },
        );
        assert!(matches!(
            blocks[0].render_status,
            DiagramRenderStatus::UnsupportedDialect { .. }
        ));
        assert_eq!(
            blocks[1].render_status,
            DiagramRenderStatus::RenderFailed {
                message: "empty diagram source".into()
            }
        );
    }

    /// While `max_abandoned_renders` abandoned renders are still running, a
    /// new render is refused without starting; once they end, rendering
    /// works again.
    #[test]
    fn abandoned_renders_are_capped_and_new_renders_are_refused_while_the_cap_holds() {
        let _guard = renderer_test_guard();
        let released = std::sync::Arc::new(AtomicBool::new(false));
        let calls = std::sync::Arc::new(AtomicUsize::new(0));
        let (gate, counted) = (
            std::sync::Arc::clone(&released),
            std::sync::Arc::clone(&calls),
        );
        let _seam = render_seam(move |_| {
            counted.fetch_add(1, Ordering::SeqCst);
            while !gate.load(Ordering::SeqCst) {
                std::thread::sleep(Duration::from_millis(5));
            }
            Ok(SEAM_SVG.to_string())
        });
        let _gate = GateGuard(std::sync::Arc::clone(&released));
        let budgets = budgets_with(Duration::from_millis(50));
        let timed_out = DiagramRenderStatus::RenderFailed {
            message: render_time_exceeded_message("diagram", 1),
        };
        for _ in 0..MAX_ABANDONED_RENDERS {
            assert_eq!(
                render_diagram_within(&flowchart(), budgets).render_status,
                timed_out
            );
        }
        assert_eq!(
            ABANDONED_RENDERS.load(Ordering::SeqCst),
            MAX_ABANDONED_RENDERS
        );
        assert!(
            reaches(&calls, MAX_ABANDONED_RENDERS),
            "the abandoned renders never started"
        );

        let refused = render_diagram_blocks_reporting(&[flowchart()], budgets);
        assert_eq!(
            refused.blocks[0].render_status,
            DiagramRenderStatus::RenderFailed {
                message: render_time_exceeded_message("busy", 1)
            }
        );
        assert!(refused.transient, "a refusal at the cap must not be cached");
        assert_eq!(
            calls.load(Ordering::SeqCst),
            MAX_ABANDONED_RENDERS,
            "a render was started past the cap"
        );

        released.store(true, Ordering::SeqCst);
        assert!(
            abandoned_renders_settle(),
            "the abandoned renders never released their slots"
        );
        let after =
            render_diagram_blocks_reporting(&[flowchart()], budgets_with(Duration::from_secs(5)));
        assert_eq!(after.blocks[0].render_status, DiagramRenderStatus::Ok);
        assert!(!after.transient);
    }

    /// The session caches a note's blocks, but not a refusal at the cap: once
    /// the earlier renders end, the same note renders.
    #[test]
    fn a_refusal_at_the_cap_is_not_cached_by_the_session() {
        let _guard = renderer_test_guard();
        let released = std::sync::Arc::new(AtomicBool::new(false));
        let calls = std::sync::Arc::new(AtomicUsize::new(0));
        let (gate, counted) = (
            std::sync::Arc::clone(&released),
            std::sync::Arc::clone(&calls),
        );
        let _seam = render_seam(move |_| {
            counted.fetch_add(1, Ordering::SeqCst);
            while !gate.load(Ordering::SeqCst) {
                std::thread::sleep(Duration::from_millis(5));
            }
            Ok(SEAM_SVG.to_string())
        });
        let _gate = GateGuard(std::sync::Arc::clone(&released));
        for _ in 0..MAX_ABANDONED_RENDERS {
            let _ = render_diagram_within(&flowchart(), budgets_with(Duration::from_millis(50)));
        }
        assert!(reaches(&calls, MAX_ABANDONED_RENDERS));

        let tmp = tempfile::tempdir().unwrap();
        std::fs::write(
            tmp.path().join("note.md"),
            "```mermaid\nflowchart LR\nA --> B\n```\n",
        )
        .unwrap();
        let session =
            crate::session::VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
        session
            .scan_initial(&crate::session::CancelToken::new())
            .unwrap();
        let busy = DiagramRenderStatus::RenderFailed {
            message: render_time_exceeded_message(
                "busy",
                budget_seconds(MAX_RENDER_TIME_PER_DIAGRAM),
            ),
        };
        assert_eq!(
            session.get_diagram_blocks("note.md").unwrap()[0].render_status,
            busy
        );

        released.store(true, Ordering::SeqCst);
        assert!(abandoned_renders_settle());
        assert_eq!(
            session.get_diagram_blocks("note.md").unwrap()[0].render_status,
            DiagramRenderStatus::Ok,
            "the refusal was cached"
        );
    }

    /// An abandoned render that panics releases its slot all the same, and
    /// its panic poisons the renderer as a waited-on render's does.
    #[test]
    fn an_abandoned_render_that_panics_still_releases_its_slot() {
        let _guard = renderer_test_guard();
        let _seam = render_seam(|_| {
            std::thread::sleep(Duration::from_secs(1));
            panic!("slate-1580: an abandoned render panics");
        });
        let block = render_diagram_within(&flowchart(), budgets_with(Duration::from_millis(50)));
        assert_eq!(
            block.render_status,
            DiagramRenderStatus::RenderFailed {
                message: render_time_exceeded_message("diagram", 1)
            }
        );
        assert!(
            abandoned_renders_settle(),
            "the panicking render never released its slot"
        );
        assert!(RENDERER_POISONED.load(Ordering::SeqCst));
    }

    /// The sentences both hosts show verbatim as the failed state's reason.
    #[test]
    fn render_time_exceeded_message_has_its_four_forms() {
        assert_eq!(
            render_time_exceeded_message("diagram", 5),
            "Diagram not drawn: rendering took longer than 5 seconds. The source follows."
        );
        assert_eq!(
            render_time_exceeded_message("note", 20),
            "Diagram not drawn: this note's diagrams used their 20-second render budget. \
             The source follows."
        );
        assert_eq!(
            render_time_exceeded_message("busy", 5),
            "Diagram not drawn: earlier diagrams are still rendering past their 5-second \
             budget. The source follows."
        );
        assert_eq!(
            render_time_exceeded_message("image", 3),
            "Image not shown: decoding took longer than 3 seconds."
        );
        assert_eq!(
            render_time_exceeded_message("image-busy", 5),
            "Image not shown: earlier images are still decoding past their 5-second budget."
        );
        assert_eq!(
            render_time_exceeded_message("diagram", 1),
            "Diagram not drawn: rendering took longer than 1 second. The source follows."
        );
        assert_eq!(
            render_time_exceeded_message("something else", 5),
            render_time_exceeded_message("diagram", 5)
        );
        assert_eq!(budget_seconds(Duration::from_millis(100)), 1);
        assert_eq!(budget_seconds(Duration::from_secs(5)), 5);
    }

    /// XD3's settled values, and the defaults every render uses.
    #[test]
    fn the_render_time_budgets_are_the_settled_values() {
        assert_eq!(MAX_RENDER_TIME_PER_DIAGRAM, Duration::from_secs(5));
        assert_eq!(MAX_RENDER_TIME_PER_NOTE, Duration::from_secs(20));
        assert_eq!(MAX_ABANDONED_RENDERS, 2);
        let budgets = DiagramBudgets::default();
        assert_eq!(
            budgets.max_render_time_per_diagram,
            MAX_RENDER_TIME_PER_DIAGRAM
        );
        assert_eq!(budgets.max_render_time_per_note, MAX_RENDER_TIME_PER_NOTE);
        assert_eq!(budgets.max_abandoned_renders, MAX_ABANDONED_RENDERS);
    }

    /// `BENCHMARKS.md`'s "diagram render budget" sample: a 500-step chain
    /// through the real renderer, which must stay under half the
    /// per-diagram budget. Run in release:
    /// `cargo test --release -p slate-core --lib -- --ignored --nocapture
    /// diagram::tests::render_budget_sample`.
    #[test]
    #[ignore = "a release-build timing sample, recorded in BENCHMARKS.md"]
    #[allow(
        clippy::disallowed_macros,
        reason = "the sample prints its own timing to the test's stdout; no note text"
    )]
    fn render_budget_sample_a_500_step_chain() {
        let _guard = renderer_test_guard();
        let source: String = std::iter::once("flowchart TD\n".to_string())
            .chain((0..500).map(|i| format!("n{i}-->n{}\n", i + 1)))
            .collect();
        let raw = mermaid(source);
        let mut samples: Vec<Duration> = (0..5)
            .map(|_| {
                let started = Instant::now();
                let block = render_diagram(&raw);
                assert_eq!(block.render_status, DiagramRenderStatus::Ok);
                started.elapsed()
            })
            .collect();
        samples.sort();
        let median = samples[samples.len() / 2];
        println!("diagram render budget sample: 500-step chain, median {median:?} of {samples:?}");
        assert!(
            median < MAX_RENDER_TIME_PER_DIAGRAM / 2,
            "a 500-step chain took {median:?}, over half the per-diagram budget"
        );
    }
}
