//! Core engine for YANA, an accessibility-first knowledge workspace.
//!
//! This is the initial bootstrap crate. The shape of the API will evolve
//! significantly as the architecture from
//! `docs/plans/05_locked_architecture_decisions.md` is implemented. For now,
//! this crate validates the Rust toolchain and `pulldown-cmark` integration
//! with the simplest possible useful operation: extract headings from a
//! Markdown file.

use std::path::Path;
use thiserror::Error;

/// Errors produced by the YANA core library.
#[derive(Debug, Error)]
pub enum YanaError {
    #[error("io error reading vault file: {0}")]
    Io(#[from] std::io::Error),
}

/// A heading parsed from a Markdown document.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Heading {
    pub level: u8,
    pub text: String,
}

/// Read a Markdown file from disk and return its headings in document order.
pub fn read_headings(path: impl AsRef<Path>) -> Result<Vec<Heading>, YanaError> {
    let source = std::fs::read_to_string(path)?;
    Ok(extract_headings(&source))
}

/// Extract headings from a Markdown source string in document order.
///
/// # Examples
///
/// ```
/// use yana_core::extract_headings;
///
/// let headings = extract_headings("# Hello\n\n## World");
/// assert_eq!(headings.len(), 2);
/// assert_eq!(headings[0].text, "Hello");
/// assert_eq!(headings[1].level, 2);
/// ```
pub fn extract_headings(source: &str) -> Vec<Heading> {
    use pulldown_cmark::{Event, Parser, Tag, TagEnd};

    let parser = Parser::new(source);
    let mut headings = Vec::new();
    let mut current_level: Option<u8> = None;
    let mut current_text = String::new();

    for event in parser {
        match event {
            Event::Start(Tag::Heading { level, .. }) => {
                current_level = Some(heading_level_to_u8(level));
                current_text.clear();
            }
            Event::End(TagEnd::Heading(_)) => {
                if let Some(level) = current_level.take() {
                    headings.push(Heading {
                        level,
                        text: std::mem::take(&mut current_text),
                    });
                }
            }
            Event::Text(s) | Event::Code(s) if current_level.is_some() => {
                current_text.push_str(&s);
            }
            _ => {}
        }
    }

    headings
}

fn heading_level_to_u8(level: pulldown_cmark::HeadingLevel) -> u8 {
    use pulldown_cmark::HeadingLevel::*;
    match level {
        H1 => 1,
        H2 => 2,
        H3 => 3,
        H4 => 4,
        H5 => 5,
        H6 => 6,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn extracts_simple_headings() {
        let source = "# First\n\n## Second\n\n### Third\n";
        let headings = extract_headings(source);
        assert_eq!(
            headings,
            vec![
                Heading {
                    level: 1,
                    text: "First".into()
                },
                Heading {
                    level: 2,
                    text: "Second".into()
                },
                Heading {
                    level: 3,
                    text: "Third".into()
                },
            ]
        );
    }

    #[test]
    fn ignores_non_heading_content() {
        let source = "Some text.\n\n# Heading\n\nMore text.\n";
        let headings = extract_headings(source);
        assert_eq!(
            headings,
            vec![Heading {
                level: 1,
                text: "Heading".into()
            }]
        );
    }

    #[test]
    fn includes_inline_code_text_in_headings() {
        let source = "# Use `cargo test`\n";
        let headings = extract_headings(source);
        assert_eq!(
            headings,
            vec![Heading {
                level: 1,
                text: "Use cargo test".into()
            }]
        );
    }

    #[test]
    fn handles_empty_input() {
        assert_eq!(extract_headings(""), Vec::<Heading>::new());
    }

    #[test]
    fn handles_all_heading_levels() {
        let source = "# h1\n## h2\n### h3\n#### h4\n##### h5\n###### h6\n";
        let headings = extract_headings(source);
        assert_eq!(headings.len(), 6);
        assert_eq!(headings[0].level, 1);
        assert_eq!(headings[5].level, 6);
    }
}
