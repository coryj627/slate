// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `slate links <vault-path> <note-path>` (M-5, #536).
//!
//! Existence check **first** (m_spec §M-5): `get_file_metadata(path)` →
//! `None` → exit 1 `no such note: <path>`. `note_load_bundle` itself
//! returns empty results for an unknown path, so without this guard an
//! empty result would be ambiguous — "isolated note" vs "typo'd path".
//! The check disambiguates: after it passes, empty blocks always mean
//! "isolated note", never "wrong path".
//!
//! Then `note_load_bundle` gives the first page of backlinks plus every
//! outgoing link; backlinks are drained to exhaustion via `backlinks`'
//! `next_cursor` (the CLI is the sanctioned drain consumer).
//!
//! `data` shape (the `slate.cli.v1` stability contract — field names
//! pinned to the shipped `Backlink` / `OutgoingLink` models):
//! ```json
//! { "path": String,
//!   "backlinks": [{ "source_path": String, "snippet": String }],
//!   "outgoing": [{ "target": String, "resolved_path": String|null,
//!                  "kind": "wikilink"|"markdown", "embed": bool,
//!                  "external": bool, "unresolved": bool }] }
//! ```
//! Backlink fields mirror `Backlink` (`source_path` + non-optional
//! `snippet`); outgoing mirrors `OutgoingLink` (`kind` is only
//! `wikilink`|`markdown`; `embed`/`external`/`unresolved` are orthogonal
//! flags). These names are part of the v1 contract.

use slate_core::session::{CancelToken, Paging};
use slate_core::{Backlink, OutgoingLink};

use crate::output::{CommandOutput, tsv_row};
use crate::session::{CliError, map_vault_error, open_and_scan};

/// Backlink page size for the drain loop.
const BACKLINK_PAGE_SIZE: u32 = 500;

/// Run `slate links`. `note_path` is vault-relative.
pub fn run(
    raw_path: &std::path::Path,
    note_path: &str,
    cancel: &CancelToken,
) -> Result<(String, CommandOutput), CliError> {
    let (session, abs_path) = open_and_scan(raw_path, cancel)?;

    // Existence check FIRST — empty output must mean "isolated note",
    // never "typo" (m_spec §M-5).
    if session
        .get_file_metadata(note_path)
        .map_err(map_vault_error)?
        .is_none()
    {
        return Err(CliError::NoSuchNote {
            path: note_path.to_string(),
        });
    }

    // One bundle fetch for outgoing links + the first backlink page,
    // then drain the remaining backlink pages.
    let bundle = session
        .note_load_bundle(note_path, Paging::first(BACKLINK_PAGE_SIZE))
        .map_err(map_vault_error)?;

    let mut backlinks: Vec<Backlink> = bundle.backlinks.items;
    let mut cursor = bundle.backlinks.next_cursor;
    while let Some(c) = cursor.take() {
        let page = session
            .backlinks(note_path, Paging::after(c, BACKLINK_PAGE_SIZE))
            .map_err(map_vault_error)?;
        backlinks.extend(page.items);
        cursor = page.next_cursor;
    }

    let outgoing = bundle.outgoing_links;

    let data = serde_json::json!({
        "path": note_path,
        "backlinks": backlinks.iter().map(backlink_json).collect::<Vec<_>>(),
        "outgoing": outgoing.iter().map(outgoing_json).collect::<Vec<_>>(),
    });
    let human = render_human(note_path, &backlinks, &outgoing);
    let tsv = render_tsv(&backlinks, &outgoing);

    Ok((
        abs_path,
        CommandOutput {
            data,
            human,
            tsv,
            human_verbatim: false,
        },
    ))
}

// --- json shaping ----------------------------------------------------

/// Backlink json — only the two contract fields (`source_path`,
/// `snippet`), not the internal `ordinal`/`kind`/`is_embed`. The v1
/// shape is deliberately the minimal backlink pair.
fn backlink_json(b: &Backlink) -> serde_json::Value {
    serde_json::json!({
        "source_path": b.source_path,
        "snippet": b.snippet,
    })
}

/// Outgoing json — the contract fields, mapping the `OutgoingLink`
/// model: `target` ← `target_raw`, `resolved_path` ← `target_path`
/// (null when unresolved / external), and the three orthogonal flags.
fn outgoing_json(o: &OutgoingLink) -> serde_json::Value {
    serde_json::json!({
        "target": o.target_raw,
        "resolved_path": o.target_path,
        "kind": o.kind,
        "embed": o.is_embed,
        "external": o.is_external,
        "unresolved": o.is_unresolved,
    })
}

// --- human format ----------------------------------------------------

/// Human format (m_spec §M-5): `Backlinks (N):` block then
/// `Outgoing links (M):` block, one entry per line. Unresolved outgoing
/// links get a `→ unresolved` suffix; embeds get an `(embed)` suffix.
fn render_human(path: &str, backlinks: &[Backlink], outgoing: &[OutgoingLink]) -> String {
    let mut lines = vec![format!("Links for {path}"), String::new()];

    lines.push(format!("Backlinks ({}):", backlinks.len()));
    for b in backlinks {
        lines.push(format!("  {}", b.source_path));
    }

    lines.push(String::new());
    lines.push(format!("Outgoing links ({}):", outgoing.len()));
    for o in outgoing {
        // Prefer the resolved path when we have one; otherwise show
        // what the author typed.
        let shown = o.target_path.as_deref().unwrap_or(&o.target_raw);
        let mut line = format!("  {shown}");
        if o.is_embed {
            line.push_str(" (embed)");
        }
        if o.is_unresolved {
            line.push_str(" → unresolved");
        }
        lines.push(line);
    }

    lines.join("\n")
}

// --- tsv format ------------------------------------------------------

/// TSV format (m_spec §M-5): header
/// `direction path kind embed external unresolved`. Backlink rows have
/// `direction=in`, `path` = the linking file, remaining columns empty.
/// Outgoing rows have `direction=out`, `path` = the target.
fn render_tsv(backlinks: &[Backlink], outgoing: &[OutgoingLink]) -> String {
    let mut rows = vec![tsv_row([
        "direction",
        "path",
        "kind",
        "embed",
        "external",
        "unresolved",
    ])];
    for b in backlinks {
        rows.push(tsv_row(["in", b.source_path.as_str(), "", "", "", ""]));
    }
    for o in outgoing {
        // `path` = the target: the resolved path when known, else the
        // authored target string (so an unresolved/external row still
        // carries something identifying).
        let path = o.target_path.as_deref().unwrap_or(&o.target_raw);
        rows.push(tsv_row([
            "out",
            path,
            o.kind.as_str(),
            bool_cell(o.is_embed),
            bool_cell(o.is_external),
            bool_cell(o.is_unresolved),
        ]));
    }
    rows.join("\n")
}

/// TSV boolean cell: `true` / `false` (lowercase, script-friendly).
fn bool_cell(b: bool) -> &'static str {
    if b { "true" } else { "false" }
}
