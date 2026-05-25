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
                    line: line_of_offset(source, start),
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
    let first_line = trimmed
        .lines()
        .find(|l| !l.trim().is_empty())
        .unwrap_or("")
        .trim()
        .to_ascii_lowercase();
    let kind = classify_mermaid_kind(&first_line);
    let body_lines: Vec<&str> = trimmed
        .lines()
        .skip(1)
        .map(str::trim)
        .filter(|l| !l.is_empty() && !l.starts_with("%%"))
        .collect();
    let count = body_lines.len();
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
        MermaidKind::Unknown => format!("Mermaid diagram, source:\n{}", trimmed),
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
    let first_line = trimmed
        .lines()
        .find(|l| !l.trim().is_empty())
        .unwrap_or("")
        .trim()
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
fn try_render_mermaid(source: &str) -> Result<Vec<u8>, RenderError> {
    use std::panic::AssertUnwindSafe;
    let result = std::panic::catch_unwind(AssertUnwindSafe(|| mermaid_renderer_render(source)));
    match result {
        Ok(Ok(svg)) => Ok(svg.into_bytes()),
        Ok(Err(msg)) => {
            // Heuristic: messages mentioning unsupported dialect /
            // diagram type get routed to `Unsupported`; otherwise
            // it's a real render failure.
            if msg.to_ascii_lowercase().contains("unsupported")
                || msg.to_ascii_lowercase().contains("not implemented")
            {
                Err(RenderError::Unsupported(msg))
            } else {
                Err(RenderError::Failed(msg))
            }
        }
        Err(panic) => Err(RenderError::Failed(format!(
            "mermaid renderer panicked: {panic:?}"
        ))),
    }
}

/// Thin shim around the actual mermaid-rs-renderer call. The
/// upstream returns an `anyhow::Error` which we don't depend on
/// directly — stringify here so the rest of the module deals with
/// plain `String` messages.
fn mermaid_renderer_render(source: &str) -> Result<String, String> {
    mermaid_rs_renderer::render(source).map_err(|e| e.to_string())
}

fn line_of_offset(source: &str, off: usize) -> u32 {
    1 + source[..off.min(source.len())]
        .bytes()
        .filter(|&b| b == b'\n')
        .count() as u32
}

// --- Tests -------------------------------------------------------------

#[cfg(test)]
mod tests {
    use super::*;

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
}
