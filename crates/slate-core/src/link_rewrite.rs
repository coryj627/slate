// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Link-integrity rewriting for file/folder moves (U2-3, #461).
//!
//! ## The invariant: referential stability
//!
//! After any move/rename, every link that resolved to file F before the
//! mutation still resolves to F — with byte-minimal edits. Links that were
//! unresolved before may heal (their target arrived at the name) via the
//! links-table re-resolution pass; they get **no text edit**. No other byte
//! of any file changes.
//!
//! ## Why this is subtler than "rewrite links to moved files"
//! (gap_analysis.md G11)
//!
//! - Basename wikilinks to a moved file usually still resolve — rewriting
//!   them would churn text needlessly.
//! - Moving a SOURCE can silently re-target its own basename wikilinks:
//!   the resolver breaks basename ties by directory distance, and the
//!   distances just changed.
//! - Relative Markdown links break when their source moves even though
//!   their target didn't.
//!
//! So the planner asks, per link: "what did you resolve to before?" and
//! "what do you resolve to now?" — and only when those differ does it
//! rewrite, to the minimal form that pins the original target.
//!
//! ## Byte discipline
//!
//! Rewrites splice ONLY the target segment inside the link's span — never
//! reform the whole link — so aliases, anchors, embed prefixes, spacing,
//! and angle-bracket wrapping survive byte-exact. The census asserts edited
//! files differ from the originals only inside planned spans.
//!
//! Pure module: no IO, no session state. The U2-2 mutation transaction
//! drives it (collect → plan → apply via `save_text` → journal), and the
//! per-file op-log makes the whole operation byte-identically undoable.

use std::collections::HashMap;

use crate::link_resolver::{ResolvedLink, VaultIndex, resolve_link};
use crate::links::{LinkKind, ParsedLink, extract_links};

/// Old-path → new-path mapping for every file whose path changes in one
/// structural mutation (a single file move/rename, or every file under a
/// moved/renamed folder).
#[derive(Debug, Clone, Default)]
pub struct MoveMapping {
    forward: HashMap<String, String>,
    reverse: HashMap<String, String>,
}

impl MoveMapping {
    pub fn new(pairs: impl IntoIterator<Item = (String, String)>) -> Self {
        let mut forward = HashMap::new();
        let mut reverse = HashMap::new();
        for (old, new) in pairs {
            reverse.insert(new.clone(), old.clone());
            forward.insert(old, new);
        }
        Self { forward, reverse }
    }

    pub fn is_empty(&self) -> bool {
        self.forward.is_empty()
    }

    pub fn new_path_of<'a>(&'a self, old: &'a str) -> &'a str {
        self.forward.get(old).map(String::as_str).unwrap_or(old)
    }

    pub fn old_path_of<'a>(&'a self, new: &'a str) -> &'a str {
        self.reverse.get(new).map(String::as_str).unwrap_or(new)
    }

    pub fn old_paths(&self) -> impl Iterator<Item = &str> {
        self.forward.keys().map(String::as_str)
    }
}

/// A `VaultIndex` view of the PRE-move world, derived from the post-move
/// index by reverse-applying the mapping. Lets the planner re-run the
/// production resolver against yesterday's paths without snapshotting the
/// whole index.
struct PreMoveIndex<'a> {
    post: &'a dyn VaultIndex,
    mapping: &'a MoveMapping,
}

impl VaultIndex for PreMoveIndex<'_> {
    fn all_paths(&self) -> Box<dyn Iterator<Item = &str> + '_> {
        Box::new(
            self.post
                .all_paths()
                .map(|path| self.mapping.old_path_of(path)),
        )
    }
}

/// One byte-range replacement inside a source file. Offsets are into the
/// ORIGINAL text; apply in descending `start` order.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct LinkEdit {
    pub start: usize,
    pub end: usize,
    pub replacement: String,
}

/// Plan every text edit `source_text` needs so its links keep resolving to
/// the same files after the move described by `mapping`.
///
/// - `post_source_path`: where this file lives AFTER the move (for moved
///   sources, the new path).
/// - `post_index`: the vault index AFTER the move (paths already updated).
///
/// Returns edits in DESCENDING start order, ready to splice.
pub fn plan_rewrites_for_source(
    post_source_path: &str,
    source_text: &str,
    mapping: &MoveMapping,
    post_index: &dyn VaultIndex,
) -> Vec<LinkEdit> {
    if mapping.is_empty() {
        return Vec::new();
    }
    let pre_index = PreMoveIndex {
        post: post_index,
        mapping,
    };
    let pre_source_path = mapping.old_path_of(post_source_path);

    let mut edits = Vec::new();
    for link in extract_links(source_text) {
        if link.is_external {
            continue;
        }
        // What did this link mean BEFORE the move?
        let pre = resolve_link(
            &link.target_raw,
            link.anchor.clone(),
            pre_source_path,
            &pre_index,
        );
        let pre_target_old_path = match pre {
            ResolvedLink::Resolved { target_path, .. } => target_path,
            // Unresolved before the move: no meaning to preserve. If the
            // move parked a file at this name, the links-table
            // re-resolution pass heals the ROW; the text is already what
            // the user wrote for that file. Never edit.
            ResolvedLink::Unresolved { .. } | ResolvedLink::External => continue,
        };
        let pre_target_post_path = mapping.new_path_of(&pre_target_old_path).to_string();

        // What does the authored text mean AFTER the move?
        let post = resolve_link(
            &link.target_raw,
            link.anchor.clone(),
            post_source_path,
            post_index,
        );
        if let ResolvedLink::Resolved { target_path, .. } = &post
            && *target_path == pre_target_post_path
        {
            // Same file — the authored text survives the move. (The links
            // table's target_path column is updated by the mutation
            // transaction; no text edit.)
            continue;
        }

        // The authored text now dangles or points at a DIFFERENT file:
        // pin the original target with a minimal rewrite.
        if let Some(edit) = pin_edit(
            source_text,
            &link,
            post_source_path,
            &pre_target_post_path,
            post_index,
        ) {
            edits.push(edit);
        }
    }
    edits.sort_by_key(|edit| std::cmp::Reverse(edit.start));
    edits
}

/// Apply descending-ordered edits to `text`.
pub fn apply_edits(text: &str, edits: &[LinkEdit]) -> String {
    let mut out = text.to_string();
    for edit in edits {
        debug_assert!(edit.start <= edit.end && edit.end <= out.len());
        out.replace_range(edit.start..edit.end, &edit.replacement);
    }
    out
}

/// Build the pinning edit for one link: replace exactly the target-segment
/// bytes inside the link's span.
fn pin_edit(
    source_text: &str,
    link: &ParsedLink,
    post_source_path: &str,
    pinned_post_path: &str,
    post_index: &dyn VaultIndex,
) -> Option<LinkEdit> {
    let span = source_text.get(link.span_start..link.span_end)?;
    let (seg_start, seg_end) = target_segment(span, link)?;
    // The minimal target text that VERIFIABLY resolves to the pinned file.
    // Every candidate is checked against the production resolver — never
    // assumed. The leading-`/` forms are load-bearing for ROOT-level pins:
    // a root file's path has no `/`, so the bare path is a BASENAME to the
    // resolver (tie-breakable away); the resolver strips a leading `/` as
    // vault-rooted, which is the exact form (census-found, seed 164).
    let pinned = pinned_target_text(pinned_post_path, post_source_path, link, post_index)?;
    let replacement = match link.kind {
        LinkKind::Wikilink => pinned,
        LinkKind::Markdown => {
            // The resolver reads destination bytes verbatim (no percent-
            // decoding); angle brackets preserve authored wrapping and are
            // REQUIRED when the pin contains whitespace (a bare CommonMark
            // destination ends at the first space).
            let was_wrapped = span[seg_start..seg_end].starts_with('<');
            if was_wrapped || pinned.chars().any(char::is_whitespace) {
                format!("<{pinned}>")
            } else {
                pinned
            }
        }
    };
    Some(LinkEdit {
        start: link.span_start + seg_start,
        end: link.span_start + seg_end,
        replacement,
    })
}

/// Shortest target text that resolves to `pinned_post_path` from
/// `post_source_path`, verified via the production resolver. Candidate
/// order: extensionless path, full path, then the vault-rooted `/` forms
/// (exact for root-level files whose bare path is basename-ambiguous).
/// None when no form pins (cannot happen for an indexed file — the `/`
/// form is exact by construction — but stay total rather than assert).
fn pinned_target_text(
    pinned_post_path: &str,
    post_source_path: &str,
    link: &ParsedLink,
    post_index: &dyn VaultIndex,
) -> Option<String> {
    let without_ext = strip_md_extension(pinned_post_path);
    let mut candidates: Vec<String> = Vec::with_capacity(4);
    // Preference by kind: wikilinks conventionally omit the extension;
    // markdown destinations conventionally carry it. Both fall through to
    // the other form and then the vault-rooted `/` forms.
    match link.kind {
        LinkKind::Wikilink => {
            if without_ext != pinned_post_path {
                candidates.push(without_ext.to_string());
            }
            candidates.push(pinned_post_path.to_string());
            if without_ext != pinned_post_path {
                candidates.push(format!("/{without_ext}"));
            }
            candidates.push(format!("/{pinned_post_path}"));
        }
        LinkKind::Markdown => {
            candidates.push(pinned_post_path.to_string());
            candidates.push(format!("/{pinned_post_path}"));
        }
    }
    candidates.into_iter().find(|candidate| {
        matches!(
            resolve_link(candidate, link.anchor.clone(), post_source_path, post_index),
            ResolvedLink::Resolved { ref target_path, .. }
                if target_path == pinned_post_path
        )
    })
}

/// Byte range of the TARGET segment within the link's span text.
///
/// Wikilink span: `!?[[  target  (#|^)anchor? (|alias)? ]]` — the target
/// runs from after `[[` to the first `#`, `^`, `|`, or `]]`. Whitespace
/// padding belongs to the segment only if the resolver would see it;
/// `target_raw` is the authored form, so locate it exactly and replace
/// just those bytes (padding survives).
///
/// Markdown span: `!?[text](dest ...)` — the destination starts after the
/// `](` (skipping leading whitespace) and runs to the closing `)` or the
/// first whitespace (title separator), honoring `<...>` wrapping.
fn target_segment(span: &str, link: &ParsedLink) -> Option<(usize, usize)> {
    match link.kind {
        LinkKind::Wikilink => {
            let open = span.find("[[")? + 2;
            let rest = &span[open..];
            let rel_end = rest
                .char_indices()
                .find(|(i, c)| matches!(c, '#' | '^' | '|') || rest[*i..].starts_with("]]"))
                .map(|(i, _)| i)
                .unwrap_or(rest.len());
            let segment = &rest[..rel_end];
            // Locate the authored target within the segment (there may be
            // padding); fall back to the whole segment when trimming was
            // the only difference.
            let inner_start = segment
                .find(link.target_raw.as_str())
                .unwrap_or_else(|| segment.len() - segment.trim_start().len());
            let inner_len = if segment[inner_start..].starts_with(link.target_raw.as_str()) {
                link.target_raw.len()
            } else {
                segment.trim().len()
            };
            Some((open + inner_start, open + inner_start + inner_len))
        }
        LinkKind::Markdown => {
            let paren = span.find("](")? + 2;
            let rest = &span[paren..];
            let lead_ws = rest.len() - rest.trim_start().len();
            let body = &rest[lead_ws..];
            let (start_off, end_off) = if body.starts_with('<') {
                let close = body.find('>')?;
                (0, close + 1)
            } else {
                let end = body
                    .char_indices()
                    .find(|(_, c)| c.is_whitespace() || *c == ')')
                    .map(|(i, _)| i)
                    .unwrap_or(body.len());
                (0, end)
            };
            Some((paren + lead_ws + start_off, paren + lead_ws + end_off))
        }
    }
}

fn strip_md_extension(path: &str) -> &str {
    for ext in ["md", "markdown", "mdown", "mkd"] {
        if let Some(stem) = path.strip_suffix(ext)
            && let Some(stem) = stem.strip_suffix('.')
        {
            return stem;
        }
    }
    path
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::link_resolver::InMemoryVaultIndex;
    use std::collections::BTreeMap;

    fn index(paths: &[&str]) -> InMemoryVaultIndex {
        InMemoryVaultIndex::new(paths.iter().map(|s| s.to_string()).collect())
    }

    fn moved(pairs: &[(&str, &str)]) -> MoveMapping {
        MoveMapping::new(pairs.iter().map(|(a, b)| (a.to_string(), b.to_string())))
    }

    fn plan(
        source_path: &str,
        text: &str,
        mapping: &MoveMapping,
        post_paths: &[&str],
    ) -> Vec<LinkEdit> {
        plan_rewrites_for_source(source_path, text, mapping, &index(post_paths))
    }

    // --- The spec's fixture matrix (byte-exact assertions) ---

    #[test]
    fn basename_wikilink_survives_target_move_untouched() {
        // note.md moves deeper; [[note]] still uniquely resolves → no edit.
        let mapping = moved(&[("note.md", "archive/note.md")]);
        let edits = plan(
            "index.md",
            "See [[note]] for details.\n",
            &mapping,
            &["index.md", "archive/note.md"],
        );
        assert!(edits.is_empty(), "basename link must not churn: {edits:?}");
    }

    #[test]
    fn folder_qualified_wikilink_rewrites_on_target_move() {
        let mapping = moved(&[("notes/a.md", "archive/a.md")]);
        let text = "See [[notes/a]] here.\n";
        let edits = plan("index.md", text, &mapping, &["index.md", "archive/a.md"]);
        assert_eq!(edits.len(), 1);
        assert_eq!(apply_edits(text, &edits), "See [[archive/a]] here.\n");
    }

    #[test]
    fn alias_anchor_and_embed_survive_rewrite_byte_exact() {
        let mapping = moved(&[("notes/a.md", "deep/dir/a.md")]);
        let text = "Body ![[notes/a#Heading One|the alias]] tail.\n";
        let edits = plan("index.md", text, &mapping, &["index.md", "deep/dir/a.md"]);
        assert_eq!(
            apply_edits(text, &edits),
            "Body ![[deep/dir/a#Heading One|the alias]] tail.\n",
            "embed prefix, anchor, and alias preserved; only the target changed"
        );
    }

    #[test]
    fn block_anchor_preserved() {
        let mapping = moved(&[("n/x.md", "m/x2.md")]);
        let text = "Ref [[n/x^blockid]].\n";
        let edits = plan("index.md", text, &mapping, &["index.md", "m/x2.md"]);
        assert_eq!(apply_edits(text, &edits), "Ref [[m/x2^blockid]].\n");
    }

    #[test]
    fn markdown_basename_link_survives_source_move_untouched() {
        // Slate's resolver is vault-rooted/basename — never source-
        // relative — so moving the SOURCE cannot dangle a basename
        // destination. No churn. (The u2_spec's original "recompute
        // relative path" line assumed CommonMark semantics; corrected —
        // see the spec's U2-3 amendment.)
        let mapping = moved(&[("notes/index.md", "archive/deep/index.md")]);
        let text = "See [target](target.md) here.\n";
        let edits = plan(
            "archive/deep/index.md",
            text,
            &mapping,
            &["archive/deep/index.md", "notes/target.md"],
        );
        assert!(edits.is_empty(), "{edits:?}");
    }

    #[test]
    fn markdown_link_to_moved_target_recomputed() {
        let mapping = moved(&[("notes/target.md", "elsewhere/target.md")]);
        let text = "See [t](notes/target.md).\n";
        let edits = plan(
            "index.md",
            text,
            &mapping,
            &["index.md", "elsewhere/target.md"],
        );
        assert_eq!(apply_edits(text, &edits), "See [t](elsewhere/target.md).\n");
    }

    #[test]
    fn markdown_angle_bracket_target_keeps_wrapping() {
        let mapping = moved(&[("notes/a file.md", "arch ive/a file.md")]);
        let text = "See [t](<notes/a file.md>).\n";
        let edits = plan(
            "index.md",
            text,
            &mapping,
            &["index.md", "arch ive/a file.md"],
        );
        assert_eq!(
            apply_edits(text, &edits),
            "See [t](<arch ive/a file.md>).\n"
        );
    }

    #[test]
    fn markdown_qualified_dest_moving_into_spaced_dir_gets_angle_wrapping() {
        // The dest was qualified (so the move dangles it) and the new path
        // contains a space: a bare destination would end at the space, so
        // the pin wraps in angle brackets (the resolver reads raw bytes —
        // %20 would dangle).
        let mapping = moved(&[("notes/b.md", "sub dir/b.md")]);
        let text = "See [t](notes/b.md).\n";
        let edits = plan("index.md", text, &mapping, &["index.md", "sub dir/b.md"]);
        assert_eq!(apply_edits(text, &edits), "See [t](<sub dir/b.md>).\n");
    }

    #[test]
    fn tie_break_flip_is_pinned_by_qualification() {
        // Two note.md candidates; the SOURCE moves nearer the other one.
        // Its basename link must be pinned to the ORIGINAL target.
        let mapping = moved(&[("a/src.md", "b/src.md")]);
        let text = "Link [[note]].\n";
        let edits = plan(
            "b/src.md",
            text,
            &mapping,
            &["b/src.md", "a/note.md", "b/note.md"],
        );
        assert_eq!(
            apply_edits(text, &edits),
            "Link [[a/note]].\n",
            "resolution would have flipped a/note.md → b/note.md; pin the original"
        );
    }

    #[test]
    fn unresolved_link_gets_no_edit_even_when_it_would_heal() {
        // [[ghost]] didn't resolve before; after the move ghost.md exists.
        // Healing is the re-resolution pass's job — text is untouched.
        let mapping = moved(&[("elsewhere/ghost.md", "ghost.md")]);
        let text = "Link [[ghost]].\n";
        let edits = plan("index.md", text, &mapping, &["index.md", "ghost.md"]);
        assert!(edits.is_empty());
    }

    #[test]
    fn self_link_within_moved_file_stays_stable() {
        let mapping = moved(&[("a/self.md", "b/self.md")]);
        let text = "Me: [[self]].\n";
        let edits = plan("b/self.md", text, &mapping, &["b/self.md", "other.md"]);
        assert!(edits.is_empty(), "unique basename still resolves to itself");
    }

    #[test]
    fn link_inside_code_fence_never_rewritten() {
        let mapping = moved(&[("notes/a.md", "arch/a.md")]);
        let text = "```\n[[notes/a]]\n```\nAnd `[[notes/a]]` inline.\n";
        let edits = plan("index.md", text, &mapping, &["index.md", "arch/a.md"]);
        assert!(
            edits.is_empty(),
            "extract_links excludes code ranges end-to-end"
        );
    }

    #[test]
    fn wikilink_with_extension_rewrites_keeping_extension_form() {
        let mapping = moved(&[("n/a.md", "m/a.md")]);
        let text = "X [[n/a.md]].\n";
        let edits = plan("index.md", text, &mapping, &["index.md", "m/a.md"]);
        // Minimal pin: extensionless resolves uniquely, so the rewrite may
        // drop the extension — but the ORIGINAL byte range is exactly the
        // target, so verify the result resolves and nothing else changed.
        let out = apply_edits(text, &edits);
        assert!(out == "X [[m/a]].\n" || out == "X [[m/a.md]].\n", "{out}");
    }

    #[test]
    fn ambiguous_new_basename_pins_with_extension_when_needed() {
        // Pinned path collides with a same-stem file of another md flavor:
        // extensionless can't pin → full path with extension.
        let mapping = moved(&[("x/t.md", "y/t.md")]);
        let text = "L [[x/t]].\n";
        let edits = plan(
            "index.md",
            text,
            &mapping,
            &["index.md", "y/t.md", "y/t.markdown"],
        );
        // "y/t" (exact-then-extension rule) resolves to y/t.md by priority
        // order, which IS the pinned file — so extensionless is fine here.
        assert_eq!(apply_edits(text, &edits), "L [[y/t]].\n");
    }

    // --- Referential-stability census (random link graphs × random moves) ---

    struct SplitMix64(u64);
    impl SplitMix64 {
        fn next(&mut self) -> u64 {
            self.0 = self.0.wrapping_add(0x9E37_79B9_7F4A_7C15);
            let mut z = self.0;
            z = (z ^ (z >> 30)).wrapping_mul(0xBF58_476D_1CE4_E5B9);
            z = (z ^ (z >> 27)).wrapping_mul(0x94D0_49BB_1331_11EB);
            z ^ (z >> 31)
        }
        fn pick<'a, T>(&mut self, xs: &'a [T]) -> &'a T {
            &xs[(self.next() % xs.len() as u64) as usize]
        }
    }

    /// Build a synthetic vault: files across nested dirs, each with a body
    /// of random links (wikilink basename / qualified / alias / anchor /
    /// embed / markdown relative / markdown vault-rooted / unresolved).
    fn synth_vault(rng: &mut SplitMix64, n_files: usize) -> BTreeMap<String, String> {
        let dirs = ["", "a", "a/b", "c", "c/d/e", "notes", "arch ive"];
        let mut paths: Vec<String> = Vec::new();
        for i in 0..n_files {
            let dir = rng.pick(&dirs);
            // Deliberate basename collisions: stem pool smaller than file
            // count so ambiguity is common.
            let stem = format!("n{}", rng.next() % (n_files as u64 / 2 + 2));
            let path = if dir.is_empty() {
                format!("{stem}.md")
            } else {
                format!("{dir}/{stem}.md")
            };
            if !paths.contains(&path) {
                paths.push(path);
            } else {
                paths.push(format!("u{i}.md"));
            }
        }
        let path_vec = paths.clone();
        let mut vault = BTreeMap::new();
        for path in paths {
            let mut body = format!("# {path}\n");
            for _ in 0..(rng.next() % 6) {
                let target = rng.pick(&path_vec);
                let stem = target.rsplit('/').next().unwrap().trim_end_matches(".md");
                match rng.next() % 8 {
                    0 => body.push_str(&format!("l [[{stem}]]\n")),
                    1 => body.push_str(&format!("l [[{}]]\n", target.trim_end_matches(".md"))),
                    2 => body.push_str(&format!("l [[{stem}|alias {stem}]]\n")),
                    3 => body.push_str(&format!("l [[{stem}#Sec]]\n")),
                    4 => body.push_str(&format!("e ![[{stem}]]\n")),
                    5 => {
                        // Vault-rooted qualified destination; wrap when the
                        // path carries spaces (bare dests end at whitespace).
                        if target.contains(' ') {
                            body.push_str(&format!("m [t](<{target}>)\n"));
                        } else {
                            body.push_str(&format!("m [t]({target})\n"));
                        }
                    }
                    6 => body.push_str(&format!("m [t](<{target}>)\n")),
                    _ => body.push_str("l [[completely-unresolved-ghost]]\n"),
                }
            }
            vault.insert(path, body);
        }
        vault
    }

    /// Resolution map: link ordinal → resolved pre-path, for every file.
    fn resolution_map(vault: &BTreeMap<String, String>) -> BTreeMap<String, Vec<Option<String>>> {
        let idx = InMemoryVaultIndex::new(vault.keys().cloned().collect());
        vault
            .iter()
            .map(|(path, text)| {
                let res = extract_links(text)
                    .into_iter()
                    .map(|l| {
                        if l.is_external {
                            return None;
                        }
                        match resolve_link(&l.target_raw, l.anchor.clone(), path, &idx) {
                            ResolvedLink::Resolved { target_path, .. } => Some(target_path),
                            _ => None,
                        }
                    })
                    .collect();
                (path.clone(), res)
            })
            .collect()
    }

    #[test]
    fn census_referential_stability_over_random_moves() {
        for seed in 0..400u64 {
            let mut rng = SplitMix64(seed.wrapping_mul(0x5D1E_C0DE).wrapping_add(11));
            let vault = synth_vault(&mut rng, 14 + (seed % 10) as usize);
            let pre_resolution = resolution_map(&vault);

            // Random mutation: move a folder prefix or 1–3 single files.
            let paths: Vec<String> = vault.keys().cloned().collect();
            let mut pairs: Vec<(String, String)> = Vec::new();
            if rng.next().is_multiple_of(2) {
                let prefixes = ["a", "c/d", "notes", "arch ive", "a/b"];
                let from = rng.pick(&prefixes).to_string();
                let to = format!("moved{}", rng.next() % 5);
                for p in &paths {
                    if p.starts_with(&format!("{from}/")) {
                        pairs.push((p.clone(), format!("{to}/{}", &p[from.len() + 1..])));
                    }
                }
            } else {
                for _ in 0..=(rng.next() % 3) {
                    let victim = rng.pick(&paths).clone();
                    if pairs.iter().any(|(o, _)| *o == victim) {
                        continue;
                    }
                    let new = format!(
                        "dest{}/{}",
                        rng.next() % 4,
                        victim.rsplit('/').next().unwrap()
                    );
                    if !paths.contains(&new) {
                        pairs.push((victim, new));
                    }
                }
            }
            // Drop destination collisions — with unmoved files AND among
            // the moved set itself (U2-2 rejects both as DestinationExists;
            // an unfiltered dupe would collapse two files into one post-
            // vault entry and invalidate the fixture).
            let mut used_dests: std::collections::HashSet<String> =
                std::collections::HashSet::new();
            let mapping = MoveMapping::new(
                pairs
                    .into_iter()
                    .filter(|(_, n)| !vault.contains_key(n) && used_dests.insert(n.clone())),
            );
            if mapping.is_empty() {
                continue;
            }

            // Post-move world.
            let post_vault: BTreeMap<String, String> = vault
                .iter()
                .map(|(p, t)| (mapping.new_path_of(p).to_string(), t.clone()))
                .collect();
            let post_index = InMemoryVaultIndex::new(post_vault.keys().cloned().collect());

            // Plan + apply per file; then verify the invariant.
            for (post_path, text) in &post_vault {
                let edits = plan_rewrites_for_source(post_path, text, &mapping, &post_index);
                let rewritten = apply_edits(text, &edits);
                if edits.is_empty() {
                    assert_eq!(&rewritten, text, "no-edit file must be byte-identical");
                }
                let pre_path = mapping.old_path_of(post_path).to_string();
                let pre_res = &pre_resolution[&pre_path];
                let post_links = extract_links(&rewritten);
                assert_eq!(
                    post_links.len(),
                    pre_res.len(),
                    "seed {seed}: rewrite changed link count in {post_path}\n{rewritten}"
                );
                for (i, link) in post_links.iter().enumerate() {
                    let Some(expected_old) = &pre_res[i] else {
                        continue;
                    };
                    let expected_new = mapping.new_path_of(expected_old);
                    match resolve_link(
                        &link.target_raw,
                        link.anchor.clone(),
                        post_path,
                        &post_index,
                    ) {
                        ResolvedLink::Resolved { target_path, .. } => assert_eq!(
                            target_path, expected_new,
                            "seed {seed}: {post_path} link {i} drifted \
                             ({expected_old} → expected {expected_new})\n{rewritten}"
                        ),
                        other => panic!(
                            "seed {seed}: {post_path} link {i} dangles post-rewrite \
                             ({expected_old} → {expected_new}): {other:?}\n{rewritten}"
                        ),
                    }
                }
            }
        }
    }
}
