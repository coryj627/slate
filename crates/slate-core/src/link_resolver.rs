// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Resolves parsed link targets against a vault's file index.
//!
//! Takes a `target_raw` string (the part before `|`/`#`/`^` already
//! stripped by `links::extract_links`), the source file's
//! vault-relative path (for distance-based tiebreaks), and a
//! `VaultIndex` snapshot, and returns one of:
//!
//! - `ResolvedLink::Resolved { target_path, anchor }` — a vault-
//!   relative path that exists in the index.
//! - `ResolvedLink::Unresolved { target_raw, anchor }` — the link
//!   is internal but no file in the vault matches. #50's links table
//!   carries this through so the UI can render unresolved links
//!   distinctly (per #52).
//! - `ResolvedLink::External` — the target is a URL / `mailto:` /
//!   in-document anchor, identified the same way `links::looks_external`
//!   does. These don't get a backlink row.
//!
//! ## Resolution rules
//!
//! Applied in order; first match wins:
//!
//! 1. **Folder-qualified** (`foo/bar.md`, `foo/bar`): the input
//!    contains a `/`. We treat it as a full vault-relative path and
//!    look for an exact match, trying the literal string first and
//!    then the same string with each candidate Markdown extension
//!    appended.
//! 2. **Basename** (`bar`, `bar.md`): scan every index entry whose
//!    final path component case-insensitively equals the target's
//!    basename (with the same `.md` / `.markdown` / `.mdown` / `.mkd`
//!    extension-implied / extension-allowed rules).
//! 3. **Multi-match tiebreak**: when more than one basename match,
//!    pick the smallest source-to-target directory distance; on
//!    distance ties, pick the alphabetically first relative path. This
//!    gives stable cross-platform behaviour regardless of how the
//!    vault was walked.
//! 4. **No match** → `Unresolved`.
//!
//! ## Why a trait
//!
//! The real index lives in SQLite (`slate_core::session`), but the
//! resolver doesn't need a database — it just needs a list of paths.
//! Keeping the dependency at the trait boundary lets us write hermetic
//! tests against an in-memory vec without spinning up a session.

use crate::links::{looks_external_for_resolver, LinkAnchor};

/// Vault-relative paths the resolver can match against. Paths use
/// forward slashes regardless of platform.
pub trait VaultIndex {
    /// Iterator over every indexed vault-relative path. Implementors
    /// can choose any concrete iterator type as long as it yields
    /// `&str` slices.
    fn all_paths(&self) -> Box<dyn Iterator<Item = &str> + '_>;
}

/// Trivial in-memory `VaultIndex` over an owned `Vec<String>`.
/// Useful in tests and as a snapshot view of a session's file list.
pub struct InMemoryVaultIndex {
    paths: Vec<String>,
}

impl InMemoryVaultIndex {
    pub fn new(paths: Vec<String>) -> Self {
        Self { paths }
    }
}

impl VaultIndex for InMemoryVaultIndex {
    fn all_paths(&self) -> Box<dyn Iterator<Item = &str> + '_> {
        Box::new(self.paths.iter().map(String::as_str))
    }
}

/// Result of resolving a `ParsedLink::target_raw` against the index.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum ResolvedLink {
    /// Target matched an indexed file. `target_path` is vault-relative
    /// with forward slashes; `anchor` carries through unchanged from
    /// the input (verifying the heading/block exists is out-of-scope
    /// per the issue).
    Resolved {
        target_path: String,
        anchor: Option<LinkAnchor>,
    },
    /// No file in the index matches. We keep `target_raw` (and the
    /// anchor) so #50's links table can store an unresolved row and
    /// re-resolve it after a future scan.
    Unresolved {
        target_raw: String,
        anchor: Option<LinkAnchor>,
    },
    /// The link points outside the vault (URL, `mailto:`, in-document
    /// fragment). Backlinks table excludes these.
    External,
}

/// Markdown-style extensions we treat as "the same kind of file" for
/// extension-implied resolution. Iteration order is the priority
/// order: a `[[foo]]` link with `foo.md` AND `foo.markdown` in the
/// vault resolves to `foo.md`.
const MD_EXTENSIONS: &[&str] = &["md", "markdown", "mdown", "mkd"];

/// Resolve a single link target against the vault index.
pub fn resolve_link(
    target_raw: &str,
    anchor: Option<LinkAnchor>,
    source_path: &str,
    index: &dyn VaultIndex,
) -> ResolvedLink {
    let trimmed = target_raw.trim();
    if trimmed.is_empty() {
        // Defensive: an empty target shouldn't have made it through
        // `links::extract_links`, but if a downstream caller hands us
        // one, treat as unresolved rather than panicking.
        return ResolvedLink::Unresolved {
            target_raw: target_raw.to_string(),
            anchor,
        };
    }
    if looks_external_for_resolver(trimmed) {
        return ResolvedLink::External;
    }

    // Normalize the input: strip a leading `./` or `/` so
    // `./notes/foo.md` and `/notes/foo.md` both resolve the same as
    // `notes/foo.md`. Obsidian renders a leading `/` as vault-rooted;
    // we match that. Multiple `../` is out of scope at this layer —
    // wiki/markdown links pointing at parent directories of the vault
    // aren't supported by the indexer.
    let normalized = trimmed
        .strip_prefix("./")
        .or_else(|| trimmed.strip_prefix('/'))
        .unwrap_or(trimmed);
    // Folder-qualified path: contains `/`. Try exact (literal),
    // then add each Markdown extension if the input has no extension.
    if normalized.contains('/') {
        if let Some(hit) = find_exact(normalized, index) {
            return ResolvedLink::Resolved {
                target_path: hit,
                anchor,
            };
        }
        return ResolvedLink::Unresolved {
            target_raw: target_raw.to_string(),
            anchor,
        };
    }

    // Basename-only: scan the index for files whose final path
    // component matches case-insensitively.
    let matches: Vec<&str> = collect_basename_matches(normalized, index);
    match matches.len() {
        0 => ResolvedLink::Unresolved {
            target_raw: target_raw.to_string(),
            anchor,
        },
        1 => ResolvedLink::Resolved {
            target_path: matches[0].to_string(),
            anchor,
        },
        _ => {
            let winner = tiebreak(&matches, source_path);
            ResolvedLink::Resolved {
                target_path: winner.to_string(),
                anchor,
            }
        }
    }
}

/// Try the literal target first; if no hit, retry with each Markdown
/// extension appended (only when the input has no extension).
fn find_exact(target: &str, index: &dyn VaultIndex) -> Option<String> {
    let target_lower = target.to_lowercase();
    if let Some(hit) = index.all_paths().find(|p| p.to_lowercase() == target_lower) {
        return Some(hit.to_string());
    }
    if !has_extension(target) {
        for ext in MD_EXTENSIONS {
            let candidate = format!("{}.{}", target_lower, ext);
            if let Some(hit) = index.all_paths().find(|p| p.to_lowercase() == candidate) {
                return Some(hit.to_string());
            }
        }
    }
    None
}

/// Gather all index entries whose final path component matches
/// `basename` (case-insensitive, Markdown-extension-aware).
fn collect_basename_matches<'a>(basename: &str, index: &'a dyn VaultIndex) -> Vec<&'a str> {
    let basename_lower = basename.to_lowercase();
    let basename_has_ext = has_extension(basename);
    let mut hits = Vec::new();
    for path in index.all_paths() {
        let file = final_component(path).to_lowercase();
        if file == basename_lower {
            hits.push(path);
            continue;
        }
        if !basename_has_ext {
            // Implied-extension match: `[[foo]]` matches `foo.md`,
            // `foo.markdown`, etc.
            for ext in MD_EXTENSIONS {
                let candidate = format!("{}.{}", basename_lower, ext);
                if file == candidate {
                    hits.push(path);
                    break;
                }
            }
        }
    }
    hits
}

/// Resolve a tie among multiple basename matches by:
///   1. shortest directory-distance between `source_path` and the
///      candidate, then
///   2. alphabetical order of the candidate paths.
fn tiebreak<'a>(candidates: &[&'a str], source_path: &str) -> &'a str {
    debug_assert!(
        candidates.len() >= 2,
        "tiebreak called with {} candidate(s); caller should short-circuit",
        candidates.len()
    );
    let source_dirs = dir_components(source_path);
    candidates
        .iter()
        .copied()
        .min_by(|a, b| {
            let da = directory_distance(&source_dirs, a);
            let db = directory_distance(&source_dirs, b);
            da.cmp(&db).then_with(|| a.cmp(b))
        })
        .expect("non-empty by debug_assert")
}

/// Distance between two locations measured by directory components.
/// Common prefix counts as 0; everything that differs adds 1 per
/// side. Files in the same directory have distance 0.
///
/// Comparison is case-insensitive to stay consistent with the rest
/// of the resolver — otherwise a vault on a case-insensitive
/// filesystem (HFS+, default APFS) could rank `Notes/foo.md` and
/// `notes/foo.md` as different directories and break the tiebreak.
fn directory_distance(source_dirs: &[&str], target_path: &str) -> usize {
    let target_dirs = dir_components(target_path);
    let common = source_dirs
        .iter()
        .zip(target_dirs.iter())
        .take_while(|(a, b)| a.eq_ignore_ascii_case(b))
        .count();
    (source_dirs.len() - common) + (target_dirs.len() - common)
}

/// Directory components of `path`, with the final filename dropped.
/// For `notes/journal/today.md` this returns `["notes", "journal"]`.
fn dir_components(path: &str) -> Vec<&str> {
    let trimmed = path.trim_start_matches('/');
    let parts: Vec<&str> = trimmed.split('/').collect();
    if parts.len() <= 1 {
        Vec::new()
    } else {
        parts[..parts.len() - 1].to_vec()
    }
}

/// Last `/`-delimited component of `path` (the file name).
fn final_component(path: &str) -> &str {
    path.rsplit('/').next().unwrap_or(path)
}

/// `true` when the final path component contains a `.` — implies the
/// user already typed an extension and we shouldn't try Markdown-
/// extension variants.
fn has_extension(name: &str) -> bool {
    final_component(name).contains('.')
}

#[cfg(test)]
mod tests {
    use super::*;

    fn idx(paths: &[&str]) -> InMemoryVaultIndex {
        InMemoryVaultIndex::new(paths.iter().map(|s| s.to_string()).collect())
    }

    fn resolved_path(r: &ResolvedLink) -> &str {
        match r {
            ResolvedLink::Resolved { target_path, .. } => target_path,
            other => panic!("expected Resolved, got {:?}", other),
        }
    }

    // --- Folder-qualified ---

    #[test]
    fn folder_qualified_exact_match() {
        let index = idx(&["notes/foo.md", "archive/foo.md"]);
        let r = resolve_link("notes/foo.md", None, "notes/index.md", &index);
        assert_eq!(resolved_path(&r), "notes/foo.md");
    }

    #[test]
    fn folder_qualified_without_extension_implies_md() {
        let index = idx(&["notes/foo.md"]);
        let r = resolve_link("notes/foo", None, "notes/index.md", &index);
        assert_eq!(resolved_path(&r), "notes/foo.md");
    }

    #[test]
    fn folder_qualified_case_insensitive() {
        let index = idx(&["Notes/Foo.md"]);
        let r = resolve_link("notes/foo", None, "Notes/index.md", &index);
        assert_eq!(resolved_path(&r), "Notes/Foo.md");
    }

    #[test]
    fn folder_qualified_unresolved_when_missing() {
        let index = idx(&["notes/foo.md"]);
        let r = resolve_link("notes/bar", None, "notes/index.md", &index);
        assert!(matches!(r, ResolvedLink::Unresolved { .. }), "got {:?}", r);
    }

    #[test]
    fn folder_qualified_with_dot_slash_prefix() {
        let index = idx(&["notes/foo.md"]);
        let r = resolve_link("./notes/foo.md", None, "notes/index.md", &index);
        assert_eq!(resolved_path(&r), "notes/foo.md");
    }

    // --- Basename ---

    #[test]
    fn basename_match_single_hit() {
        let index = idx(&["notes/foo.md", "notes/bar.md"]);
        let r = resolve_link("foo", None, "notes/index.md", &index);
        assert_eq!(resolved_path(&r), "notes/foo.md");
    }

    #[test]
    fn basename_match_with_extension() {
        let index = idx(&["notes/foo.md"]);
        let r = resolve_link("foo.md", None, "notes/index.md", &index);
        assert_eq!(resolved_path(&r), "notes/foo.md");
    }

    #[test]
    fn basename_case_insensitive() {
        let index = idx(&["notes/FOO.md"]);
        let r = resolve_link("foo", None, "notes/index.md", &index);
        assert_eq!(resolved_path(&r), "notes/FOO.md");
    }

    #[test]
    fn basename_alternate_markdown_extensions_match() {
        let index = idx(&["notes/a.markdown", "notes/b.mdown", "notes/c.mkd"]);
        for (name, expected) in &[
            ("a", "notes/a.markdown"),
            ("b", "notes/b.mdown"),
            ("c", "notes/c.mkd"),
        ] {
            let r = resolve_link(name, None, "notes/index.md", &index);
            assert_eq!(resolved_path(&r), *expected, "for [[{}]]", name);
        }
    }

    #[test]
    fn basename_explicit_extension_does_not_imply_md() {
        // `[[foo.txt]]` should NOT match `notes/foo.txt.md` — the
        // user typed an extension and we honor it literally.
        let index = idx(&["notes/foo.txt", "notes/foo.txt.md"]);
        let r = resolve_link("foo.txt", None, "notes/index.md", &index);
        assert_eq!(resolved_path(&r), "notes/foo.txt");
    }

    #[test]
    fn basename_unresolved_when_truly_missing() {
        let index = idx(&["notes/bar.md"]);
        let r = resolve_link("foo", None, "notes/index.md", &index);
        assert!(matches!(r, ResolvedLink::Unresolved { .. }), "got {:?}", r);
    }

    // --- Tiebreak ---

    #[test]
    fn tiebreak_prefers_shortest_distance_from_source_dir() {
        // The exemplar from the acceptance criteria.
        let index = idx(&["notes/foo.md", "archive/foo.md"]);
        let r = resolve_link("foo", None, "notes/journal/today.md", &index);
        assert_eq!(resolved_path(&r), "notes/foo.md");
    }

    #[test]
    fn tiebreak_same_distance_falls_back_to_alphabetical() {
        // Both candidates equidistant from source (each one dir away).
        let index = idx(&["alpha/foo.md", "beta/foo.md"]);
        let r = resolve_link("foo", None, "gamma/source.md", &index);
        assert_eq!(resolved_path(&r), "alpha/foo.md");
    }

    #[test]
    fn tiebreak_same_dir_as_source_wins() {
        let index = idx(&["other/foo.md", "notes/foo.md"]);
        let r = resolve_link("foo", None, "notes/index.md", &index);
        assert_eq!(resolved_path(&r), "notes/foo.md");
    }

    // --- External / anchor passthrough ---

    #[test]
    fn external_url_short_circuits() {
        let index = idx(&["notes/foo.md"]);
        let r = resolve_link("https://example.com", None, "notes/index.md", &index);
        assert!(matches!(r, ResolvedLink::External), "got {:?}", r);
    }

    #[test]
    fn fragment_only_is_external() {
        let index = idx(&["notes/foo.md"]);
        let r = resolve_link("#intro", None, "notes/index.md", &index);
        assert!(matches!(r, ResolvedLink::External), "got {:?}", r);
    }

    #[test]
    fn anchor_passes_through_to_resolved() {
        let index = idx(&["notes/foo.md"]);
        let anchor = Some(LinkAnchor::Heading("Intro".to_string()));
        let r = resolve_link("foo", anchor.clone(), "notes/index.md", &index);
        match r {
            ResolvedLink::Resolved {
                target_path,
                anchor: ra,
            } => {
                assert_eq!(target_path, "notes/foo.md");
                assert_eq!(ra, anchor);
            }
            other => panic!("expected Resolved with anchor, got {:?}", other),
        }
    }

    #[test]
    fn anchor_passes_through_to_unresolved() {
        let index = idx(&["notes/bar.md"]);
        let anchor = Some(LinkAnchor::Block("blk".to_string()));
        let r = resolve_link("missing", anchor.clone(), "notes/index.md", &index);
        match r {
            ResolvedLink::Unresolved {
                target_raw,
                anchor: ra,
            } => {
                assert_eq!(target_raw, "missing");
                assert_eq!(ra, anchor);
            }
            other => panic!("expected Unresolved with anchor, got {:?}", other),
        }
    }

    #[test]
    fn empty_target_is_unresolved() {
        let index = idx(&["notes/foo.md"]);
        let r = resolve_link("", None, "notes/index.md", &index);
        assert!(matches!(r, ResolvedLink::Unresolved { .. }), "got {:?}", r);
    }

    #[test]
    fn leading_slash_is_normalized_to_vault_root() {
        // Obsidian treats `/notes/foo.md` as vault-relative from the
        // root; we match that by stripping the leading slash before
        // resolving.
        let index = idx(&["notes/foo.md"]);
        let r = resolve_link("/notes/foo.md", None, "elsewhere/source.md", &index);
        assert_eq!(resolved_path(&r), "notes/foo.md");
    }

    #[test]
    fn leading_slash_with_implied_extension() {
        let index = idx(&["notes/foo.md"]);
        let r = resolve_link("/notes/foo", None, "elsewhere/source.md", &index);
        assert_eq!(resolved_path(&r), "notes/foo.md");
    }

    #[test]
    fn tiebreak_directory_comparison_is_case_insensitive() {
        // Case-insensitive filesystems (HFS+, default APFS) can record
        // mixed casing for the same directory; the resolver should
        // treat `Notes` and `notes` as the same parent for distance.
        let index = idx(&["Notes/foo.md", "archive/foo.md"]);
        let r = resolve_link("foo", None, "notes/journal/today.md", &index);
        assert_eq!(resolved_path(&r), "Notes/foo.md");
    }
}
