//! FFI bindings for `yana-core` via `uniffi-rs`.
//!
//! This crate wraps the pure-Rust `yana-core` API with uniffi annotations
//! so it can be called from Swift (Mac, iOS) and Kotlin (Android) without
//! hand-written FFI glue.
//!
//! Bootstrap stage: only the heading-extraction primitives are exposed.
//! The full FFI surface (`VaultProvider` trait via callback interfaces,
//! `VaultSession`, operation log, query engine, etc.) will land
//! incrementally per `docs/plans/05_locked_architecture_decisions.md`.

use yana_core as core;

uniffi::setup_scaffolding!();

/// A heading parsed from a Markdown document.
///
/// Mirrored from `yana_core::Heading` so that uniffi can derive its
/// foreign-language bindings without coupling the core API surface to
/// uniffi annotations.
#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct Heading {
    pub level: u8,
    pub text: String,
}

impl From<core::Heading> for Heading {
    fn from(h: core::Heading) -> Self {
        Heading {
            level: h.level,
            text: h.text,
        }
    }
}

/// Errors that may be returned across the FFI boundary.
///
/// Mirrors `yana_core::VaultError` with the inner sources flattened into
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
}
