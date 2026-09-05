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
    let description = structured_description(&raw.source, raw.dialect);
    let (svg, status) = match raw.dialect {
        DiagramDialect::Mermaid => render_mermaid_with_validation(&raw.source),
    };
    DiagramBlock {
        source: raw.source.clone(),
        dialect: raw.dialect,
        svg,
        png_fallback: None,
        structured_description: description,
        render_status: status,
        line: raw.line,
        byte_offset: raw.byte_offset,
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

/// The per-call budget set, injectable for tests.
#[derive(Debug, Clone, Copy)]
struct DiagramBudgets {
    max_rendered: usize,
    max_source_bytes: usize,
    max_svg_bytes: usize,
    max_retained_svg_bytes: usize,
}

impl Default for DiagramBudgets {
    fn default() -> Self {
        Self {
            max_rendered: MAX_RENDERED_DIAGRAMS_PER_NOTE,
            max_source_bytes: MAX_DIAGRAM_SOURCE_BYTES,
            max_svg_bytes: MAX_DIAGRAM_SVG_BYTES,
            max_retained_svg_bytes: MAX_RETAINED_SVG_BYTES_PER_NOTE,
        }
    }
}

/// Render every extracted block under the per-note budgets — the
/// bounded entry `VaultSession::get_diagram_blocks` uses.
pub fn render_diagram_blocks(raws: &[RawDiagramBlock]) -> Vec<DiagramBlock> {
    render_diagram_blocks_bounded(raws, DiagramBudgets::default())
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
    let mut retained: usize = 0;
    raws.iter()
        .enumerate()
        .map(|(index, raw)| {
            if raw.source.len() > budgets.max_source_bytes {
                return budget_degraded(raw, "diagram source exceeds the render budget");
            }
            if index >= budgets.max_rendered {
                return budget_degraded(raw, "diagram count exceeds the per-note render budget");
            }
            let mut block = render_diagram(raw);
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
        .collect()
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
}

use std::sync::atomic::{AtomicBool, Ordering};

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
fn render_mermaid_with_validation(source: &str) -> (Option<Vec<u8>>, DiagramRenderStatus) {
    let trimmed = source.trim();
    if trimmed.is_empty() {
        return (
            None,
            DiagramRenderStatus::RenderFailed {
                message: "empty diagram source".into(),
            },
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
        );
    }
    match try_render_mermaid(source) {
        Ok(svg) => (Some(svg), DiagramRenderStatus::Ok),
        Err(RenderError::Unsupported(reason)) => {
            (None, DiagramRenderStatus::UnsupportedDialect { reason })
        }
        Err(RenderError::Failed(message)) => (None, DiagramRenderStatus::RenderFailed { message }),
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
fn try_render_mermaid(source: &str) -> Result<Vec<u8>, RenderError> {
    if RENDERER_POISONED.load(Ordering::Relaxed) {
        return Err(RenderError::Failed(
            "mermaid renderer is unavailable for the rest of this session \
             (a previous render caused a panic that poisoned the renderer's \
             internal state)"
                .into(),
        ));
    }

    use std::panic::AssertUnwindSafe;
    let result = std::panic::catch_unwind(AssertUnwindSafe(|| mermaid_renderer_render(source)));
    match result {
        Ok(Ok(svg)) => Ok(svg.into_bytes()),
        Ok(Err(msg)) => {
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

/// Thin shim around the actual mermaid-rs-renderer call. The
/// upstream returns an `anyhow::Error` which we don't depend on
/// directly — stringify here so the rest of the module deals with
/// plain `String` messages.
fn mermaid_renderer_render(source: &str) -> Result<String, String> {
    mermaid_rs_renderer::render(source).map_err(|e| e.to_string())
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
        let result = try_render_mermaid("flowchart LR\nA --> B\n");
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
}
