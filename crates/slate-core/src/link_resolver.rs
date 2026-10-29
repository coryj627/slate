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
//! 2. **Basename** (`bar`, `bar.md`): every index entry whose
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

use std::hash::{BuildHasher, BuildHasherDefault, DefaultHasher};
use std::sync::OnceLock;

use crate::links::{LinkAnchor, looks_external_for_resolver};

/// Vault-relative paths the resolver can match against. Paths use
/// forward slashes regardless of platform.
pub trait VaultIndex {
    /// Iterator over every indexed vault-relative path. Implementors
    /// can choose any concrete iterator type as long as it yields
    /// `&str` slices.
    fn all_paths(&self) -> Box<dyn Iterator<Item = &str> + '_>;

    /// Offer `selector` the indexed paths its target could match (#1429):
    /// at least every path its probe ([`LinkCandidateSelector::probe`])
    /// names, and paths equal under lowercase in index order, which decides
    /// a tie between them. An extra path never changes the winner. The
    /// default offers every path, in index order; [`InMemoryVaultIndex`]
    /// looks its candidates up instead.
    fn offer_candidates(&self, selector: &mut LinkCandidateSelector) {
        for path in self.all_paths() {
            selector.offer(path);
        }
    }

    /// The indexed paths `probe` names (#1485): at least every path whose
    /// lowercased whole path ([`CandidateProbe::Paths`]) or final component
    /// ([`CandidateProbe::Names`]) is one of its keys, each once, in index
    /// order — so a caller offering them in turn keeps the first-offered
    /// tie that [`Self::all_paths`] gives. The default filters every path;
    /// [`InMemoryVaultIndex`] looks them up.
    fn candidate_paths(&self, probe: &CandidateProbe) -> Vec<&str> {
        match probe {
            CandidateProbe::None => Vec::new(),
            CandidateProbe::Paths(keys) => self
                .all_paths()
                .filter(|path| keys.contains(&path.to_lowercase()))
                .collect(),
            CandidateProbe::Names(keys) => self
                .all_paths()
                .filter(|path| keys.contains(&final_component(path).to_lowercase()))
                .collect(),
        }
    }
}

/// Trivial in-memory `VaultIndex` over an owned `Vec<String>`.
/// Useful in tests and as a snapshot view of a session's file list.
///
/// A resolution offers only the paths its target's probe names (#1429),
/// looked up in a [`CandidateTable`] built from the paths on the first
/// resolution, once per snapshot. Its winner is the one offering every path
/// in the snapshot's order would give, whatever that order is.
pub struct InMemoryVaultIndex {
    paths: Vec<String>,
    candidates: OnceLock<CandidateTable>,
}

impl InMemoryVaultIndex {
    pub fn new(paths: Vec<String>) -> Self {
        #[cfg(test)]
        SNAPSHOT_PATHS.with(|count| count.set(count.get() + paths.len()));
        Self {
            paths,
            candidates: OnceLock::new(),
        }
    }
}

/// [`InMemoryVaultIndex`]'s lookup by the keys a [`CandidateProbe`] names
/// (#1429): every path's position under the hash of its lowercased whole
/// path, and again under the hash of its lowercased final component —
/// each computed exactly as [`LinkCandidateSelector::offer`] compares —
/// sorted, so one key's candidates are one run, in index order. Two keys
/// that share a hash share a run: that adds only paths the selector
/// rejects, and keeps every key's own paths in index order.
struct CandidateTable {
    /// `(hash of path.to_lowercase(), position)`, sorted.
    by_path: Vec<(u64, usize)>,
    /// `(hash of final_component(path).to_lowercase(), position)`, sorted.
    by_name: Vec<(u64, usize)>,
}

impl CandidateTable {
    fn build(paths: &[String]) -> Self {
        #[cfg(test)]
        CANDIDATE_TABLES_BUILT.with(|count| count.set(count.get() + 1));
        let mut by_path = Vec::with_capacity(paths.len());
        let mut by_name = Vec::with_capacity(paths.len());
        for (position, path) in paths.iter().enumerate() {
            by_path.push((key_hash(&path.to_lowercase()), position));
            by_name.push((key_hash(&final_component(path).to_lowercase()), position));
        }
        // The pairs are distinct (each position once), so the order is
        // total: by hash, then by position — index order within a run.
        by_path.sort_unstable();
        by_name.sort_unstable();
        Self { by_path, by_name }
    }

    /// Offer `selector` the run of each key its probe names. The order
    /// across keys does not matter: an exact rank is its key's position in
    /// the probe, so only paths under one key can tie, and they arrive in
    /// index order within its run; a basename's rank (distance, then path)
    /// is total.
    fn offer(&self, paths: &[String], selector: &mut LinkCandidateSelector) {
        let (table, keys) = match selector.probe() {
            CandidateProbe::None => return,
            CandidateProbe::Paths(keys) => (&self.by_path, keys),
            CandidateProbe::Names(keys) => (&self.by_name, keys),
        };
        let mut hashes: Vec<u64> = keys.iter().map(|key| key_hash(key)).collect();
        hashes.sort_unstable();
        hashes.dedup();
        for hash in hashes {
            let start = table.partition_point(|&(entry, _)| entry < hash);
            for &(_, position) in table[start..]
                .iter()
                .take_while(|(entry, _)| *entry == hash)
            {
                selector.offer(&paths[position]);
            }
        }
    }

    /// The positions of every run `probe` names, each once and sorted
    /// (#1485): the union in index order, not in key-hash order. Two keys
    /// that share a hash add only paths a selector rejects.
    fn positions(&self, probe: &CandidateProbe) -> Vec<usize> {
        let (table, keys) = match probe {
            CandidateProbe::None => return Vec::new(),
            CandidateProbe::Paths(keys) => (&self.by_path, keys),
            CandidateProbe::Names(keys) => (&self.by_name, keys),
        };
        let mut positions = Vec::new();
        for key in keys {
            let hash = key_hash(key);
            let start = table.partition_point(|&(entry, _)| entry < hash);
            positions.extend(
                table[start..]
                    .iter()
                    .take_while(|(entry, _)| *entry == hash)
                    .map(|&(_, position)| position),
            );
        }
        positions.sort_unstable();
        positions.dedup();
        positions
    }
}

/// One fixed hasher for a [`CandidateTable`]'s keys and its probes, so a
/// table answers the same in every run.
fn key_hash(key: &str) -> u64 {
    BuildHasherDefault::<DefaultHasher>::default().hash_one(key)
}

#[cfg(test)]
thread_local! {
    /// #1295 test seam: paths materialized into an [`InMemoryVaultIndex`]
    /// on this thread — what a whole-`files` snapshot costs a caller.
    pub(crate) static SNAPSHOT_PATHS: std::cell::Cell<usize> =
        const { std::cell::Cell::new(0) };
    /// #1295 test seam: paths offered to a [`LinkCandidateSelector`] on
    /// this thread, matching or not — the rows a resolution visits.
    pub(crate) static PATHS_OFFERED: std::cell::Cell<usize> =
        const { std::cell::Cell::new(0) };
    /// #1429 test seam: [`CandidateTable`]s built on this thread — one per
    /// snapshot that resolves anything.
    pub(crate) static CANDIDATE_TABLES_BUILT: std::cell::Cell<usize> =
        const { std::cell::Cell::new(0) };
}

impl VaultIndex for InMemoryVaultIndex {
    fn all_paths(&self) -> Box<dyn Iterator<Item = &str> + '_> {
        Box::new(self.paths.iter().map(String::as_str))
    }

    fn offer_candidates(&self, selector: &mut LinkCandidateSelector) {
        self.candidates
            .get_or_init(|| CandidateTable::build(&self.paths))
            .offer(&self.paths, selector);
    }

    fn candidate_paths(&self, probe: &CandidateProbe) -> Vec<&str> {
        self.candidates
            .get_or_init(|| CandidateTable::build(&self.paths))
            .positions(probe)
            .into_iter()
            .map(|position| self.paths[position].as_str())
            .collect()
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

/// Format an indexed Markdown target as a complete resolver-correct wikilink.
///
/// A bare basename is used only when its Markdown stem is globally unique.
/// Duplicate stems use a vault-relative qualified target; a root-level target
/// is prefixed with `/` so it cannot fall back to basename resolution. Every
/// candidate is parsed by the production wikilink parser and resolved through
/// [`resolve_link`] before it is returned.
pub fn format_wikilink_for_path(target_path: &str, index: &dyn VaultIndex) -> Option<String> {
    let paths: Vec<&str> = index.all_paths().collect();
    let target_stem_path = markdown_stem_path(target_path)?;

    let target_folded = target_path.to_lowercase();
    if paths
        .iter()
        .filter(|path| path.to_lowercase() == target_folded)
        .count()
        != 1
    {
        return None;
    }

    let target_stem = final_component(target_stem_path);
    if target_stem.is_empty() {
        return None;
    }
    if target_stem
        .chars()
        .any(|c| matches!(c, ']' | '|' | '#' | '^'))
    {
        return None;
    }
    let target_stem_folded = target_stem.to_lowercase();
    let globally_unique_markdown_stem = paths
        .iter()
        .filter_map(|path| markdown_stem_path(path))
        .filter(|stem_path| final_component(stem_path).to_lowercase() == target_stem_folded)
        .count()
        == 1;

    let target_stem_path_folded = target_stem_path.to_lowercase();
    let markdown_flavor_collision = paths
        .iter()
        .filter_map(|path| markdown_stem_path(path))
        .filter(|stem_path| stem_path.to_lowercase() == target_stem_path_folded)
        .count()
        > 1;
    let retain_extension = target_stem.contains('.') || markdown_flavor_collision;
    let bare_target = if retain_extension {
        final_component(target_path)
    } else {
        target_stem
    };
    let bare_matches = collect_basename_matches(bare_target, index);
    let globally_unique_basename =
        globally_unique_markdown_stem && bare_matches.as_slice() == [target_path];

    let mut bodies = Vec::new();
    if globally_unique_basename {
        push_candidate_bodies(
            &mut bodies,
            target_stem,
            final_component(target_path),
            retain_extension,
        );
    }

    let qualified_stem = if target_stem_path.contains('/') {
        target_stem_path.to_string()
    } else {
        format!("/{target_stem_path}")
    };
    let qualified_full = if target_path.contains('/') {
        target_path.to_string()
    } else {
        format!("/{target_path}")
    };
    push_candidate_bodies(
        &mut bodies,
        &qualified_stem,
        &qualified_full,
        retain_extension,
    );
    if target_stem_path.contains('/') {
        push_candidate_bodies(
            &mut bodies,
            &format!("/{target_stem_path}"),
            &format!("/{target_path}"),
            retain_extension,
        );
    }

    bodies.into_iter().find_map(|body| {
        let candidate = format!("[[{body}]]");
        wikilink_round_trips_to(&candidate, target_path, index).then_some(candidate)
    })
}

fn push_candidate_bodies(
    candidates: &mut Vec<String>,
    without_extension: &str,
    with_extension: &str,
    retain_extension: bool,
) {
    if !retain_extension {
        candidates.push(without_extension.to_string());
    }
    if !candidates
        .iter()
        .any(|candidate| candidate == with_extension)
    {
        candidates.push(with_extension.to_string());
    }
}

fn wikilink_round_trips_to(candidate: &str, target_path: &str, index: &dyn VaultIndex) -> bool {
    let links = crate::links::extract_links(candidate);
    let [link] = links.as_slice() else {
        return false;
    };
    if link.kind != crate::links::LinkKind::Wikilink
        || link.span_start != 0
        || link.span_end != candidate.len()
        || link.display_text.is_some()
        || link.anchor.is_some()
        || link.is_embed
        || link.is_external
    {
        return false;
    }

    matches!(
        resolve_link(&link.target_raw, None, "", index),
        ResolvedLink::Resolved { target_path: resolved, anchor: None }
            if resolved == target_path
    )
}

fn markdown_stem_path(path: &str) -> Option<&str> {
    let dot = path.rfind('.')?;
    let extension = &path[dot + 1..];
    MD_EXTENSIONS
        .iter()
        .any(|candidate| extension.eq_ignore_ascii_case(candidate))
        .then_some(&path[..dot])
}

/// Resolve a single link target against the vault index.
pub fn resolve_link(
    target_raw: &str,
    anchor: Option<LinkAnchor>,
    source_path: &str,
    index: &dyn VaultIndex,
) -> ResolvedLink {
    // One rule for every caller (#1279): the index offers its candidates to
    // the same incremental selector a streaming caller feeds row by row, so
    // a resolution over a whole snapshot and one over streamed candidate
    // rows cannot disagree; a snapshot offers only its target's (#1429).
    let Ok(resolved) = resolve_link_through(target_raw, anchor, source_path, |mut selector| {
        index.offer_candidates(&mut selector);
        Ok::<_, std::convert::Infallible>(selector.into_winner())
    });
    resolved
}

/// [`resolve_link`] with the candidate paths supplied by `choose` (#1295):
/// the same trimming, the same empty and external short-circuits and the
/// same [`LinkCandidateSelector`], so a caller that offers only the rows a
/// target could match — a save reading them through the index's folded-name
/// lookup — answers exactly as a resolution over the whole index. `choose`
/// runs only for a target that can name an indexed path: it offers its
/// candidates to the selector it is handed and returns the winner, and its
/// error is the resolution's.
pub(crate) fn resolve_link_through<E>(
    target_raw: &str,
    anchor: Option<LinkAnchor>,
    source_path: &str,
    choose: impl FnOnce(LinkCandidateSelector) -> Result<Option<String>, E>,
) -> Result<ResolvedLink, E> {
    let trimmed = target_raw.trim();
    if trimmed.is_empty() {
        // Defensive: an empty target shouldn't have made it through
        // `links::extract_links`, but if a downstream caller hands us
        // one, treat as unresolved rather than panicking.
        return Ok(ResolvedLink::Unresolved {
            target_raw: target_raw.to_string(),
            anchor,
        });
    }
    if looks_external_for_resolver(trimmed) {
        return Ok(ResolvedLink::External);
    }
    let Some(selector) = LinkCandidateSelector::new(target_raw, source_path) else {
        return Ok(ResolvedLink::Unresolved {
            target_raw: target_raw.to_string(),
            anchor,
        });
    };
    Ok(match choose(selector)? {
        Some(target_path) => ResolvedLink::Resolved {
            target_path,
            anchor,
        },
        None => ResolvedLink::Unresolved {
            target_raw: target_raw.to_string(),
            anchor,
        },
    })
}

/// [`resolve_link`]'s choice among candidate paths, made one path at a time
/// (#1279), so a caller holding a database streams index rows through it and
/// keeps only the best candidate instead of materializing every match.
///
/// The target is normalized as the resolution rules describe: a leading
/// `/` or `./` is vault-rooted, and a rooted or qualified target (one that
/// contains `/`) matches EXACT paths only — the literal, then each implied
/// Markdown extension in order, the first offered path winning among equals.
/// A basename target matches every path whose final component equals it
/// (extension-implied or extension-allowed) and keeps the one nearest the
/// source by directory distance, then the alphabetically first path.
/// Comparison is full-Unicode lowercase without NFC; a caller may pre-filter
/// by a coarser fold, since offering extra paths never changes the answer.
pub struct LinkCandidateSelector {
    mode: SelectorMode,
    best: Option<(SelectorRank, String)>,
}

enum SelectorMode {
    /// The lowercased exact keys, in priority order.
    Exact { keys: Vec<String> },
    /// The lowercased file name and the source's directories.
    Basename {
        basename: String,
        has_extension: bool,
        source_dirs: Vec<String>,
    },
}

#[derive(Clone, PartialEq, Eq, PartialOrd, Ord)]
enum SelectorRank {
    /// The index of the exact key the path matched.
    Exact(usize),
    /// Directory distance from the source, then the path itself.
    Basename(usize, String),
}

impl LinkCandidateSelector {
    /// The selector for `target_raw` resolved from `source_path`, or `None`
    /// when the target can name nothing in the index (empty or external).
    pub fn new(target_raw: &str, source_path: &str) -> Option<Self> {
        let trimmed = target_raw.trim();
        if trimmed.is_empty() || looks_external_for_resolver(trimmed) {
            return None;
        }
        // Normalize the input: strip a leading `./` or `/` so
        // `./notes/foo.md` and `/notes/foo.md` both resolve the same as
        // `notes/foo.md`. Obsidian renders a leading `/` as vault-rooted;
        // we match that. Multiple `../` is out of scope at this layer —
        // wiki/markdown links pointing at parent directories of the vault
        // aren't supported by the indexer.
        //
        // `rooted` (a leading `/` or `./`) is VAULT-ROOTED EXACT — no
        // basename fallback. Before U2-3 the flag was stripped and lost, so
        // `/foo` on a ROOT-LEVEL file (no `/` left after stripping) fell
        // through to the basename scan and could tie-break to a DIFFERENT
        // deep file — which also made root files unpinnable by any authored
        // text (the U2-3 referential-stability census found this, seed 164).
        let rooted = trimmed.starts_with('/') || trimmed.starts_with("./");
        let normalized = trimmed
            .strip_prefix("./")
            .or_else(|| trimmed.strip_prefix('/'))
            .unwrap_or(trimmed);
        let lower = normalized.to_lowercase();
        let has_extension = has_extension(normalized);
        let mode = if rooted || normalized.contains('/') {
            let mut keys = vec![lower.clone()];
            if !has_extension {
                keys.extend(MD_EXTENSIONS.iter().map(|ext| format!("{lower}.{ext}")));
            }
            SelectorMode::Exact { keys }
        } else {
            SelectorMode::Basename {
                basename: lower,
                has_extension,
                source_dirs: dir_components(source_path)
                    .into_iter()
                    .map(str::to_string)
                    .collect(),
            }
        };
        Some(Self { mode, best: None })
    }

    /// The index rows this target could match, for a caller that fetches
    /// candidates instead of the whole index.
    pub fn probe(&self) -> CandidateProbe {
        match &self.mode {
            SelectorMode::Exact { keys } => CandidateProbe::Paths(keys.clone()),
            SelectorMode::Basename {
                basename,
                has_extension,
                ..
            } => {
                let mut keys = vec![basename.clone()];
                if !has_extension {
                    keys.extend(MD_EXTENSIONS.iter().map(|ext| format!("{basename}.{ext}")));
                }
                CandidateProbe::Names(keys)
            }
        }
    }

    /// Whether this target's probe names `path` (#1485): its lowercased
    /// whole path is an exact key, or its final component matches the
    /// basename — [`Self::offer`]'s own matching, without the ranking.
    pub fn probe_names(&self, path: &str) -> bool {
        match &self.mode {
            SelectorMode::Exact { keys } => keys.contains(&path.to_lowercase()),
            SelectorMode::Basename {
                basename,
                has_extension,
                ..
            } => basename_matches(
                &final_component(path).to_lowercase(),
                basename,
                *has_extension,
            ),
        }
    }

    /// Consider one indexed path; a path that cannot match is ignored.
    pub fn offer(&mut self, path: &str) {
        #[cfg(test)]
        PATHS_OFFERED.with(|count| count.set(count.get() + 1));
        let rank = match &self.mode {
            SelectorMode::Exact { keys } => {
                let lower = path.to_lowercase();
                match keys.iter().position(|key| *key == lower) {
                    Some(index) => SelectorRank::Exact(index),
                    None => return,
                }
            }
            SelectorMode::Basename {
                basename,
                has_extension,
                source_dirs,
            } => {
                let file = final_component(path).to_lowercase();
                if !basename_matches(&file, basename, *has_extension) {
                    return;
                }
                SelectorRank::Basename(directory_distance(source_dirs, path), path.to_string())
            }
        };
        // Strictly better only: among equals the first offered path stays,
        // as the whole-index scan's first hit (exact) or first minimum
        // (basename) did.
        if self.best.as_ref().is_none_or(|(best, _)| rank < *best) {
            self.best = Some((rank, path.to_string()));
        }
    }

    /// The winning path, if any offered path matched.
    pub fn into_winner(self) -> Option<String> {
        self.best.map(|(_, path)| path)
    }
}

/// The index rows [`resolve_link`] could match for a target (#1279), so a
/// caller holding a database can fetch just those instead of snapshotting
/// every path. Keys are lowercased the way the resolver compares; the
/// caller may fold further (a coarser fold only widens the set), and the
/// resolver's selector over the fetched candidates still decides.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum CandidateProbe {
    /// Nothing in the index can match: an empty or external target.
    None,
    /// A qualified or rooted target: the whole vault-relative paths it
    /// may name (the literal, then each implied Markdown extension).
    Paths(Vec<String>),
    /// A basename target: the file names it may match (the literal, then
    /// each implied Markdown extension).
    Names(Vec<String>),
}

/// The [`CandidateProbe`] for `target_raw`, normalized exactly as
/// [`resolve_link`] normalizes it.
pub fn candidate_probe(target_raw: &str) -> CandidateProbe {
    LinkCandidateSelector::new(target_raw, "")
        .map_or(CandidateProbe::None, |selector| selector.probe())
}

/// Gather all index entries whose final path component matches
/// `basename` (case-insensitive, Markdown-extension-aware).
fn collect_basename_matches<'a>(basename: &str, index: &'a dyn VaultIndex) -> Vec<&'a str> {
    let basename_lower = basename.to_lowercase();
    let basename_has_ext = has_extension(basename);
    index
        .all_paths()
        .filter(|path| {
            basename_matches(
                &final_component(path).to_lowercase(),
                &basename_lower,
                basename_has_ext,
            )
        })
        .collect()
}

/// A lowercased file name matches a lowercased basename exactly, or — for a
/// basename with no extension — as `basename.{md,markdown,mdown,mkd}`
/// (`[[foo]]` matches `foo.md`, `foo.markdown`, ...).
fn basename_matches(file_lower: &str, basename_lower: &str, basename_has_ext: bool) -> bool {
    file_lower == basename_lower
        || (!basename_has_ext
            && MD_EXTENSIONS.iter().any(|ext| {
                file_lower.len() == basename_lower.len() + 1 + ext.len()
                    && file_lower.starts_with(basename_lower)
                    && file_lower[basename_lower.len()..].starts_with('.')
                    && file_lower.ends_with(ext)
            }))
}

/// Distance between two locations measured by directory components.
/// Common prefix counts as 0; everything that differs adds 1 per
/// side. Files in the same directory have distance 0.
///
/// Comparison is case-insensitive to stay consistent with the rest
/// of the resolver — otherwise a vault on a case-insensitive
/// filesystem (HFS+, default APFS) could rank `Notes/foo.md` and
/// `notes/foo.md` as different directories and break the tiebreak.
fn directory_distance<S: AsRef<str>>(source_dirs: &[S], target_path: &str) -> usize {
    let target_dirs = dir_components(target_path);
    let common = source_dirs
        .iter()
        .zip(target_dirs.iter())
        .take_while(|(a, b)| a.as_ref().eq_ignore_ascii_case(b))
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
pub(crate) fn final_component(path: &str) -> &str {
    path.rsplit('/').next().unwrap_or(path)
}

/// `true` when the final path component contains a `.` — implies the
/// user already typed an extension and we shouldn't try Markdown-
/// extension variants.
fn has_extension(name: &str) -> bool {
    final_component(name).contains('.')
}

/// The resolver fixtures the equivalence censuses share (#1295): every
/// shape the rule tells apart, and the oracle they are checked against — a
/// selector offered every path in a whole-table snapshot's order. The save
/// lookup's census (`session/tests/save_link_lookup.rs`) runs them through
/// the index; a narrower snapshot offer (#1429) can run them in memory.
#[cfg(test)]
pub(crate) mod census_fixtures {
    use super::{LinkAnchor, ResolvedLink, resolve_link_through};

    /// Files any volume can hold: duplicate basenames at several depths,
    /// implied and explicit extensions, attachments, case, and Unicode
    /// (composed, decomposed, non-ASCII case, the Kelvin sign's fold).
    pub(crate) const INDEXED_PATHS: &[&str] = &[
        "alpha.md",
        "notes/beta.md",
        "gamma.md",
        "a/shared.md",
        "notes/shared.md",
        "z/shared.md",
        "notes/deep/shared.md",
        "img/pic.png",
        "pic.png",
        "docs/report.pdf",
        "other.markdown",
        "c.mdown",
        "d.mkd",
        "x.y.md",
        "foo.txt",
        "foo.txt.md",
        "space name.md",
        "\u{dc}ber.md",
        "folder \u{c4}/Stra\u{df}e.md",
        "e\u{301}cole.md",
        "\u{130}stanbul.md",
        "notes/\u{c6}ON.md",
        "Notes2/Mixed Case.MD",
        "Kelvin.md",
        "index.md",
        "a/index.md",
        "notes/index.md",
    ];

    /// Index rows a case-insensitive volume (APFS, NTFS) cannot hold side by
    /// side — case twins, composed and decomposed twins, a name spelled with
    /// the Kelvin sign — each pair listed in the reverse of path order, so a
    /// source written in this order and read in row order breaks a path tie
    /// differently from the snapshot.
    pub(crate) const INDEX_ONLY_TWINS: &[&str] = &[
        "twin/case.md",
        "Twin/Case.md",
        "Caf\u{e9}.md",
        "Cafe\u{301}.md",
        "dup.md",
        "DUP.md",
        "\u{212a}m.md",
    ];

    /// Targets: bare names, folder paths, vault-root and `./` paths,
    /// relative `../` paths, targets without an extension, attachments, the
    /// same name in many folders, case and Unicode forms, the twins, and
    /// targets that never reach a lookup (external, empty, padded).
    pub(crate) const TARGETS: &[&str] = &[
        "alpha",
        "Alpha",
        "ALPHA.md",
        "alpha.md",
        "beta",
        "gamma",
        "other",
        "OTHER.markdown",
        "c",
        "d",
        "x.y",
        "x.y.md",
        "foo",
        "foo.txt",
        "foo.txt.md",
        "space name",
        "space name.md",
        "mixed case",
        "Mixed Case.MD",
        "index",
        "ghost",
        "notes/beta",
        "Notes/Beta.md",
        "notes/beta.md",
        "./notes/beta",
        "/notes/beta",
        "/alpha",
        "/alpha.md",
        "./alpha",
        "/gamma",
        "a/index",
        "/index",
        "Notes2/mixed case",
        "notes/ghost",
        "shared",
        "SHARED.md",
        "a/shared",
        "/shared",
        "deep/shared",
        "notes/deep/shared",
        "../alpha",
        "../alpha.md",
        "notes/../alpha",
        "./../alpha",
        "pic.png",
        "img/pic.png",
        "PIC.PNG",
        "/pic.png",
        "report.pdf",
        "docs/report.pdf",
        "report",
        "\u{fc}ber",
        "\u{dc}BER",
        "u\u{308}ber",
        "e\u{301}cole",
        "\u{e9}cole",
        "stra\u{df}e",
        "STRASSE",
        "folder \u{e4}/stra\u{df}e",
        "Folder \u{c4}/Stra\u{df}e.md",
        "\u{130}stanbul",
        "i\u{307}stanbul",
        "istanbul",
        "\u{e6}on",
        "notes/\u{e6}on",
        "\u{212a}elvin",
        "kelvin",
        "twin/case",
        "Twin/Case",
        "TWIN/CASE.MD",
        "/twin/case",
        "case",
        "Case.md",
        "caf\u{e9}",
        "cafe\u{301}",
        "CAF\u{c9}",
        "dup",
        "DUP.md",
        "Dup",
        "km",
        "\u{212a}m",
        "https://example.com/a",
        "mailto:a@example.com",
        "#top",
        "//host/x",
        "C:\\notes\\alpha.md",
        "2024:report.md",
        "",
        "   ",
        " alpha ",
    ];

    /// Sources: the root and folders at several depths, one of them a case
    /// twin's (the basename tiebreak is by distance from the source).
    pub(crate) const SOURCES: &[&str] = &[
        "",
        "host.md",
        "notes/host.md",
        "notes/deep/host.md",
        "Twin/host.md",
        "a/b/c/host.md",
    ];

    /// `(target, source, winner)` cases a census must reach for its
    /// equivalence to mean anything: a path tie between case twins goes to
    /// the first in path order, composed and decomposed twins stay apart,
    /// the Kelvin sign folds, a basename three folders carry goes to the
    /// nearest, and `../` names nothing.
    pub(crate) const DECISIVE_CASES: &[(&str, &str, Option<&str>)] = &[
        ("twin/case", "", Some("Twin/Case.md")),
        ("dup", "", Some("DUP.md")),
        ("caf\u{e9}", "", Some("Caf\u{e9}.md")),
        ("cafe\u{301}", "", Some("Cafe\u{301}.md")),
        ("\u{212a}elvin", "", Some("Kelvin.md")),
        ("km", "", Some("\u{212a}m.md")),
        ("shared", "notes/deep/host.md", Some("notes/deep/shared.md")),
        ("../alpha", "notes/host.md", None),
    ];

    /// Every fixture path in a whole-table snapshot's order: path order.
    pub(crate) fn snapshot_paths() -> Vec<String> {
        let mut paths: Vec<String> = INDEXED_PATHS
            .iter()
            .chain(INDEX_ONLY_TWINS)
            .map(|path| path.to_string())
            .collect();
        paths.sort();
        paths
    }

    /// The oracle: the rule with every one of `paths` offered, in order —
    /// what a resolution over a whole-table snapshot answers.
    pub(crate) fn resolve_by_full_offer(
        target_raw: &str,
        anchor: Option<LinkAnchor>,
        source_path: &str,
        paths: &[String],
    ) -> ResolvedLink {
        let Ok(resolved) = resolve_link_through(target_raw, anchor, source_path, |mut selector| {
            for path in paths {
                selector.offer(path);
            }
            Ok::<_, std::convert::Infallible>(selector.into_winner())
        });
        resolved
    }

    /// The winner `resolved` names, if any.
    pub(crate) fn winner(resolved: &ResolvedLink) -> Option<&str> {
        match resolved {
            ResolvedLink::Resolved { target_path, .. } => Some(target_path),
            _ => None,
        }
    }
}

#[cfg(test)]
mod rooted_semantics_tests {
    use super::*;

    fn index(paths: &[&str]) -> InMemoryVaultIndex {
        InMemoryVaultIndex::new(paths.iter().map(|s| s.to_string()).collect())
    }

    #[test]
    fn leading_slash_is_vault_rooted_exact_for_root_files() {
        // Two n9s; the deep one wins the basename tie-break from a deep
        // source. `/n9` must pin the ROOT file regardless (U2-3 census,
        // seed 164 — pre-fix the slash was stripped and fell through to
        // the basename scan).
        let idx = index(&["n9.md", "dest2/n9.md", "dest2/src.md"]);
        assert_eq!(
            resolve_link("/n9", None, "dest2/src.md", &idx),
            ResolvedLink::Resolved {
                target_path: "n9.md".into(),
                anchor: None
            }
        );
        assert_eq!(
            resolve_link("/n9.md", None, "dest2/src.md", &idx),
            ResolvedLink::Resolved {
                target_path: "n9.md".into(),
                anchor: None
            }
        );
    }

    #[test]
    fn rooted_miss_is_unresolved_not_basename_fallback() {
        // `/ghost` names a missing ROOT file; a deep basename match must
        // NOT hijack it (vault-rooted means exactly that).
        let idx = index(&["deep/ghost.md", "src.md"]);
        assert_eq!(
            resolve_link("/ghost", None, "src.md", &idx),
            ResolvedLink::Unresolved {
                target_raw: "/ghost".into(),
                anchor: None
            }
        );
    }
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

    // --- Resolver-correct wikilink formatting (FL-04) ---

    #[test]
    fn wikilink_formatter_uses_stem_for_globally_unique_markdown_target() {
        let index = idx(&["Notes/Target.md", "Notes/Other.md"]);

        assert_eq!(
            format_wikilink_for_path("Notes/Target.md", &index),
            Some("[[Target]]".into())
        );
    }

    #[test]
    fn wikilink_formatter_qualifies_duplicate_stem_independent_of_source() {
        let index = idx(&["Notes/Target.md", "Archive/Target.md", "Sources/Index.md"]);

        assert_eq!(
            format_wikilink_for_path("Notes/Target.md", &index),
            Some("[[Notes/Target]]".into())
        );
    }

    #[test]
    fn wikilink_formatter_uses_rooted_fallback_for_scheme_like_qualified_path() {
        let index = idx(&["ab:folder/Target.md", "Other/Target.md"]);

        assert_eq!(
            format_wikilink_for_path("ab:folder/Target.md", &index),
            Some("[[/ab:folder/Target]]".into())
        );
    }

    #[test]
    fn wikilink_formatter_uses_rooted_fallback_for_leading_space_qualified_path() {
        let index = idx(&[" Folder/Target.md", "Other/Target.md"]);

        assert_eq!(
            format_wikilink_for_path(" Folder/Target.md", &index),
            Some("[[/ Folder/Target]]".into())
        );
    }

    #[test]
    fn wikilink_formatter_roots_root_level_duplicate() {
        let index = idx(&["Target.md", "Archive/Target.md"]);

        assert_eq!(
            format_wikilink_for_path("Target.md", &index),
            Some("[[/Target]]".into())
        );
    }

    #[test]
    fn wikilink_formatter_qualifies_non_markdown_basename_collision() {
        let index = idx(&["Target.md", "Archive/Target"]);

        assert_eq!(
            format_wikilink_for_path("Target.md", &index),
            Some("[[/Target]]".into())
        );
    }

    #[test]
    fn wikilink_formatter_retains_extension_for_markdown_flavor_collision() {
        let index = idx(&["Notes/Target.md", "Notes/Target.markdown"]);

        assert_eq!(
            format_wikilink_for_path("Notes/Target.md", &index),
            Some("[[Notes/Target.md]]".into())
        );
        assert_eq!(
            format_wikilink_for_path("Notes/Target.markdown", &index),
            Some("[[Notes/Target.markdown]]".into())
        );
    }

    #[test]
    fn wikilink_formatter_retains_extension_for_multi_dot_stem() {
        let index = idx(&["Notes/Target.v2.md"]);

        assert_eq!(
            format_wikilink_for_path("Notes/Target.v2.md", &index),
            Some("[[Target.v2.md]]".into())
        );
    }

    #[test]
    fn wikilink_formatter_preserves_indexed_case_and_unicode() {
        let index = idx(&["Research/Überblick.MD"]);

        assert_eq!(
            format_wikilink_for_path("Research/Überblick.MD", &index),
            Some("[[Überblick]]".into())
        );
    }

    #[test]
    fn wikilink_formatter_refuses_case_fold_equal_target_paths() {
        let index = idx(&["Notes/Target.md", "notes/target.MD"]);

        assert_eq!(format_wikilink_for_path("Notes/Target.md", &index), None);
        assert_eq!(format_wikilink_for_path("notes/target.MD", &index), None);
    }

    #[test]
    fn wikilink_formatter_refuses_parser_control_characters() {
        for path in [
            "Notes/closing]bracket.md",
            "Notes/pipe|name.md",
            "Notes/hash#name.md",
            "Notes/caret^name.md",
        ] {
            let index = idx(&[path]);
            assert_eq!(
                format_wikilink_for_path(path, &index),
                None,
                "parser-control path should fail closed: {path}"
            );
        }
    }

    #[test]
    fn wikilink_formatter_allows_parser_control_characters_only_in_parent_directories() {
        let cases = [
            ("Folder#Name/HashTarget.md", "[[HashTarget]]"),
            ("Folder^Name/CaretTarget.md", "[[CaretTarget]]"),
            ("Folder|Name/PipeTarget.md", "[[PipeTarget]]"),
            ("Folder]Name/BracketTarget.md", "[[BracketTarget]]"),
        ];
        let paths: Vec<&str> = cases.iter().map(|(path, _)| *path).collect();
        let index = idx(&paths);

        for (path, expected) in cases {
            assert_eq!(
                format_wikilink_for_path(path, &index),
                Some(expected.into()),
                "a safe globally unique bare target must not inherit parent punctuation: {path}"
            );
        }
    }

    #[test]
    fn wikilink_formatter_refuses_missing_and_non_markdown_targets() {
        let index = idx(&["Notes/Present.md", "Assets/Diagram.png"]);

        assert_eq!(format_wikilink_for_path("Notes/Missing.md", &index), None);
        assert_eq!(format_wikilink_for_path("Assets/Diagram.png", &index), None);
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

    /// #1295: the shared census fixtures mean what they claim over an
    /// in-memory snapshot in path order — every decisive case is reached,
    /// by `resolve_link` and by the full-offer oracle alike.
    #[test]
    fn the_census_fixtures_reach_their_decisive_cases() {
        use super::census_fixtures::{
            DECISIVE_CASES, resolve_by_full_offer, snapshot_paths, winner,
        };
        let paths = snapshot_paths();
        let index = InMemoryVaultIndex::new(paths.clone());
        for (target, source, expected) in DECISIVE_CASES {
            let resolved = resolve_link(target, None, source, &index);
            assert_eq!(winner(&resolved), *expected, "{target:?} from {source:?}");
            assert_eq!(
                resolve_by_full_offer(target, None, source, &paths),
                resolved,
                "{target:?} from {source:?}"
            );
        }
    }

    /// The paths of `paths` that `target_raw`'s probe names: those whose
    /// lowercased whole path (a path target) or final component (a basename)
    /// is one of its keys — the candidates and nothing else.
    fn probe_candidates(target_raw: &str, paths: &[String]) -> usize {
        match candidate_probe(target_raw) {
            CandidateProbe::None => 0,
            CandidateProbe::Paths(keys) => paths
                .iter()
                .filter(|path| keys.contains(&path.to_lowercase()))
                .count(),
            CandidateProbe::Names(keys) => paths
                .iter()
                .filter(|path| keys.contains(&final_component(path).to_lowercase()))
                .count(),
        }
    }

    /// #1429 (#1279's equivalence): a snapshot that looks its candidates up
    /// resolves every target as offering it every path does — over the
    /// shared fixtures (bare names, folder and vault-root paths, `../`,
    /// implied extensions, attachments, a name three folders carry, case and
    /// Unicode forms, and the index-only case, normalization and Kelvin-sign
    /// twins), from six sources, anchors carried through, with the paths in
    /// path order, reversed, and as written (each twin pair against path
    /// order). Each resolution offers exactly its probe's candidates, from a
    /// table built once per snapshot.
    #[test]
    fn candidate_lookup_resolves_like_the_full_offer() {
        use super::census_fixtures::{
            INDEX_ONLY_TWINS, INDEXED_PATHS, SOURCES, TARGETS, resolve_by_full_offer,
            snapshot_paths, winner,
        };
        use std::cell::Cell;

        let in_path_order = snapshot_paths();
        let reversed: Vec<String> = in_path_order.iter().rev().cloned().collect();
        let as_written: Vec<String> = INDEX_ONLY_TWINS
            .iter()
            .chain(INDEXED_PATHS)
            .map(|path| path.to_string())
            .collect();
        let orders = [&in_path_order, &reversed, &as_written];
        let anchors = [
            None,
            Some(LinkAnchor::Heading("Intro".into())),
            Some(LinkAnchor::Block("blk".into())),
        ];
        for order in orders {
            let index = InMemoryVaultIndex::new(order.clone());
            let built_before = CANDIDATE_TABLES_BUILT.with(Cell::get);
            for source in SOURCES {
                for (n, target) in TARGETS.iter().enumerate() {
                    let anchor = anchors[n % anchors.len()].clone();
                    let expected = resolve_by_full_offer(target, anchor.clone(), source, order);
                    let offered_before = PATHS_OFFERED.with(Cell::get);
                    let resolved = resolve_link(target, anchor, source, &index);
                    let offered = PATHS_OFFERED.with(Cell::get) - offered_before;
                    assert_eq!(resolved, expected, "{target:?} from {source:?}");
                    assert_eq!(
                        offered,
                        probe_candidates(target, order),
                        "{target:?} from {source:?}: the paths offered are not its candidates"
                    );
                }
            }
            assert_eq!(
                CANDIDATE_TABLES_BUILT.with(Cell::get) - built_before,
                1,
                "one candidate table per snapshot"
            );
        }

        // The orders differ where order decides: a case-twin tie goes to the
        // twin first in the snapshot, so a lookup that reordered a key's
        // paths could not pass the census above.
        let twin_winners: Vec<Option<String>> = orders
            .iter()
            .map(|order| {
                let index = InMemoryVaultIndex::new((*order).clone());
                winner(&resolve_link("twin/case", None, "", &index)).map(str::to_string)
            })
            .collect();
        assert_eq!(
            twin_winners,
            [
                Some("Twin/Case.md".to_string()),
                Some("twin/case.md".to_string()),
                Some("twin/case.md".to_string())
            ]
        );
    }

    /// #1429: a snapshot nothing resolves against builds no table.
    #[test]
    fn a_snapshot_that_resolves_nothing_builds_no_candidate_table() {
        use std::cell::Cell;
        let built_before = CANDIDATE_TABLES_BUILT.with(Cell::get);
        let index = idx(&["notes/foo.md", "notes/bar.md"]);
        assert_eq!(index.all_paths().count(), 2);
        assert!(matches!(
            resolve_link("https://example.com", None, "", &index),
            ResolvedLink::External
        ));
        assert_eq!(CANDIDATE_TABLES_BUILT.with(Cell::get), built_before);
    }

    /// #1485: a probe's candidates come back as one union in index order —
    /// two keys whose matches interleave stay interleaved, not grouped by
    /// key — from the table and from the default alike.
    #[test]
    fn candidate_paths_returns_the_union_in_index_order() {
        struct EveryPath(Vec<String>);
        impl VaultIndex for EveryPath {
            fn all_paths(&self) -> Box<dyn Iterator<Item = &str> + '_> {
                Box::new(self.0.iter().map(String::as_str))
            }
        }
        let paths: Vec<String> = [
            "a/foo.md",
            "b/bar.md",
            "c/foo.md",
            "d/other.md",
            "e/bar.md",
            "f/foo.md",
        ]
        .into_iter()
        .map(String::from)
        .collect();
        let names = CandidateProbe::Names(vec!["foo.md".into(), "bar.md".into()]);
        let exact = CandidateProbe::Paths(vec!["e/bar.md".into(), "a/foo.md".into()]);
        let table = InMemoryVaultIndex::new(paths.clone());
        let every = EveryPath(paths);
        for (index, label) in [
            (&table as &dyn VaultIndex, "the table"),
            (&every as &dyn VaultIndex, "the default"),
        ] {
            assert_eq!(
                index.candidate_paths(&names),
                ["a/foo.md", "b/bar.md", "c/foo.md", "e/bar.md", "f/foo.md"],
                "{label}: names"
            );
            assert_eq!(
                index.candidate_paths(&exact),
                ["a/foo.md", "e/bar.md"],
                "{label}: paths"
            );
            assert!(
                index.candidate_paths(&CandidateProbe::None).is_empty(),
                "{label}: none"
            );
        }
    }
}
