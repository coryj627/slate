//! Core engine for YANA, an accessibility-first knowledge workspace.
//!
//! The full API surface is documented in
//! `docs/plans/05_locked_architecture_decisions.md`. This crate is currently
//! mid-bootstrap, building Milestone A: SQLite-backed metadata index (`db`
//! module) and the vault filesystem abstraction (`vault` module) plus the
//! pre-existing heading extraction used by the FFI smoke tests.

pub mod db;
pub mod link_resolver;
pub mod links;
pub mod links_db;
pub mod session;
pub mod vault;

pub use link_resolver::{resolve_link, InMemoryVaultIndex, ResolvedLink, VaultIndex};
pub use links::{extract_links, LinkAnchor, LinkKind, ParsedLink};
pub use links_db::{Backlink, OutgoingLink, UnresolvedLink};

pub use session::{
    CancelToken, FileFilter, FileMetadata, FileSummary, Page, Paging, ScanProgress,
    ScanProgressListener, ScanReport, SessionConfig, VaultSession,
};
pub use vault::{
    content_hash, DirEntry, EntryKind, FileEvent, FileEventSink, FileStat, FsVaultProvider,
    VaultProvider, WatchHandle,
};

use std::path::Path;
use thiserror::Error;

/// Errors produced by the YANA core library's vault-facing API.
#[derive(Debug, Error)]
pub enum VaultError {
    #[error("io error: {0}")]
    Io(#[from] std::io::Error),

    #[error("database error: {0}")]
    Db(#[from] db::DbError),

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
}

// Convenience: bare rusqlite errors flow through the `Db` variant so
// `?` works inside session code that calls SQLite directly.
impl From<rusqlite::Error> for VaultError {
    fn from(e: rusqlite::Error) -> Self {
        VaultError::Db(db::DbError::from(e))
    }
}

/// A heading parsed from a Markdown document.
///
/// `ordinal` is the heading's 0-based index within its source file.
/// `anchor_id` is a deterministic slug derived from `text`, deduped
/// within the file so each heading has a unique identifier the UI
/// (and future deep-link URLs) can target.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Heading {
    pub level: u8,
    pub text: String,
    pub ordinal: u32,
    pub anchor_id: String,
}

/// Read a Markdown file from disk and return its headings in document order.
pub fn read_headings(path: impl AsRef<Path>) -> Result<Vec<Heading>, VaultError> {
    let source = std::fs::read_to_string(path)?;
    Ok(extract_headings(&source))
}

/// Extract headings from a Markdown source string in document order.
///
/// Assigns each heading an `ordinal` (0-based index) and a per-file-
/// unique `anchor_id` slug. Anchor IDs are case-folded ASCII with
/// non-alphanumerics collapsed to `-`; duplicates within a file get a
/// `-2`, `-3` … suffix; an empty slug (heading composed entirely of
/// punctuation, say) falls back to `section`.
///
/// # Examples
///
/// ```
/// use yana_core::extract_headings;
///
/// let headings = extract_headings("# Hello\n\n## World");
/// assert_eq!(headings.len(), 2);
/// assert_eq!(headings[0].text, "Hello");
/// assert_eq!(headings[0].anchor_id, "hello");
/// assert_eq!(headings[1].level, 2);
/// ```
pub fn extract_headings(source: &str) -> Vec<Heading> {
    use pulldown_cmark::{Event, Parser, Tag, TagEnd};

    let parser = Parser::new(source);
    let mut raw: Vec<(u8, String)> = Vec::new();
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
                    raw.push((level, std::mem::take(&mut current_text)));
                }
            }
            Event::Text(s) | Event::Code(s) if current_level.is_some() => {
                current_text.push_str(&s);
            }
            _ => {}
        }
    }

    let mut seen: std::collections::HashMap<String, u32> = std::collections::HashMap::new();
    raw.into_iter()
        .enumerate()
        .map(|(idx, (level, text))| {
            let base = slugify(&text);
            let counter = seen.entry(base.clone()).or_insert(0);
            *counter += 1;
            let anchor_id = if *counter == 1 {
                base
            } else {
                format!("{base}-{counter}")
            };
            Heading {
                level,
                text,
                ordinal: idx as u32,
                anchor_id,
            }
        })
        .collect()
}

/// Slugify heading text into an ASCII-only anchor id.
///
/// Lowercase ASCII alphanumerics pass through unchanged; everything
/// else (whitespace, punctuation, non-ASCII) collapses to single `-`
/// runs. Leading / trailing dashes are stripped. An empty result (a
/// heading composed entirely of non-alphanumeric content) falls back
/// to `section` so every heading still gets a usable anchor.
fn slugify(text: &str) -> String {
    let mut out = String::with_capacity(text.len());
    let mut prev_dash = true; // suppress leading dash
    for c in text.chars() {
        if c.is_ascii_alphanumeric() {
            for lc in c.to_lowercase() {
                out.push(lc);
            }
            prev_dash = false;
        } else if !prev_dash {
            out.push('-');
            prev_dash = true;
        }
    }
    while out.ends_with('-') {
        out.pop();
    }
    if out.is_empty() {
        "section".to_string()
    } else {
        out
    }
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

    fn h(level: u8, text: &str, ordinal: u32, anchor: &str) -> Heading {
        Heading {
            level,
            text: text.into(),
            ordinal,
            anchor_id: anchor.into(),
        }
    }

    #[test]
    fn extracts_simple_headings() {
        let source = "# First\n\n## Second\n\n### Third\n";
        let headings = extract_headings(source);
        assert_eq!(
            headings,
            vec![
                h(1, "First", 0, "first"),
                h(2, "Second", 1, "second"),
                h(3, "Third", 2, "third"),
            ]
        );
    }

    #[test]
    fn ignores_non_heading_content() {
        let source = "Some text.\n\n# Heading\n\nMore text.\n";
        let headings = extract_headings(source);
        assert_eq!(headings, vec![h(1, "Heading", 0, "heading")]);
    }

    #[test]
    fn includes_inline_code_text_in_headings() {
        let source = "# Use `cargo test`\n";
        let headings = extract_headings(source);
        assert_eq!(headings, vec![h(1, "Use cargo test", 0, "use-cargo-test")]);
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
        assert_eq!(headings[0].ordinal, 0);
        assert_eq!(headings[5].ordinal, 5);
    }

    #[test]
    fn duplicate_heading_text_gets_unique_anchor_ids() {
        let source = "# Notes\n\n# Notes\n\n## Notes\n";
        let headings = extract_headings(source);
        let anchors: Vec<&str> = headings.iter().map(|h| h.anchor_id.as_str()).collect();
        assert_eq!(anchors, vec!["notes", "notes-2", "notes-3"]);
    }

    #[test]
    fn punctuation_only_heading_falls_back_to_section_slug() {
        let source = "# !!!\n\n# ???\n";
        let headings = extract_headings(source);
        assert_eq!(headings[0].anchor_id, "section");
        assert_eq!(headings[1].anchor_id, "section-2");
    }

    #[test]
    fn slug_collapses_punctuation_and_strips_edges() {
        let source = "# Hello,  World!\n";
        let headings = extract_headings(source);
        assert_eq!(headings[0].anchor_id, "hello-world");
    }

    #[test]
    fn slug_lowercases_and_replaces_non_ascii() {
        let source = "# Café Résumé 2026\n";
        let headings = extract_headings(source);
        // Non-ASCII collapses to dashes; the year stays. Slug is
        // deterministic; that's all we promise here.
        assert_eq!(headings[0].anchor_id, "caf-r-sum-2026");
    }
}
