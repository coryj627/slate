// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Structural mutations' shared types (U2-2, #460): the report every
//! mutation returns, the failure taxonomy U2-3's rewriter feeds, and the
//! journal payload persisted in `structural_ops` (migration 017).
//!
//! The journal is a SQLite table, not a second binary format: rows are
//! regenerable-adjacent metadata (the *content* history lives in the
//! per-file op-logs, which is what makes `undo_structural` byte-exact),
//! and SQLite gives us MAX(id) ordering and transactional append for free.

use serde_json::{Value, json};

/// What a structural mutation did. `rewritten`/`failed` are produced by the
/// U2-3 link rewriter — empty until that integration lands; the shape ships
/// with U2-2 so the API is stable.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct StructuralReport {
    /// Journal row id — the handle `undo_structural` takes.
    pub op_id: i64,
    /// Every path that changed, `(old, new)`, files only (a folder move
    /// lists each contained file; the folder itself is implied).
    pub moved: Vec<(String, String)>,
    /// Files whose link text was rewritten (U2-3).
    pub rewritten: Vec<RewriteOutcome>,
    /// Files whose rewrite could not be applied (U2-3); the mutation
    /// itself still stands — per-file failures are reported, not silent,
    /// and never abort the move (the rename-property discipline).
    pub failed: Vec<RewriteFailure>,
}

/// One successfully rewritten file. Hashes anchor `undo_structural`'s
/// byte-exact restore through the per-file op-log.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RewriteOutcome {
    /// Post-move vault-relative path.
    pub path: String,
    pub hash_before: String,
    pub hash_after: String,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RewriteFailure {
    pub path: String,
    pub kind: RewriteFailureKind,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum RewriteFailureKind {
    /// The file changed externally between planning and apply.
    WriteConflict,
    MalformedFrontmatter,
    Cancelled,
    Other(String),
}

/// The mutation kinds journaled in `structural_ops.kind`. String-stable:
/// these are persisted; never rename a discriminant.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum StructuralOpKind {
    CreateFolder,
    RenameFolder,
    MoveFolder,
    DeleteFolder,
    RenameFile,
    MoveFile,
    DeleteFile,
}

impl StructuralOpKind {
    pub fn as_str(self) -> &'static str {
        match self {
            Self::CreateFolder => "create_folder",
            Self::RenameFolder => "rename_folder",
            Self::MoveFolder => "move_folder",
            Self::DeleteFolder => "delete_folder",
            Self::RenameFile => "rename_file",
            Self::MoveFile => "move_file",
            Self::DeleteFile => "delete_file",
        }
    }

    pub fn parse(s: &str) -> Option<Self> {
        Some(match s {
            "create_folder" => Self::CreateFolder,
            "rename_folder" => Self::RenameFolder,
            "move_folder" => Self::MoveFolder,
            "delete_folder" => Self::DeleteFolder,
            "rename_file" => Self::RenameFile,
            "move_file" => Self::MoveFile,
            "delete_file" => Self::DeleteFile,
            _ => return None,
        })
    }

    /// Deletes are journaled for auditability but not undoable through
    /// `undo_structural` (the bytes live in the system trash; a
    /// restore-from-trash API is a recorded follow-up).
    pub fn undoable(self) -> bool {
        !matches!(self, Self::DeleteFolder | Self::DeleteFile)
    }
}

/// The JSON payload persisted in `structural_ops.payload`. Hand-rolled
/// through `serde_json::Value` (the crate's existing serde_json-Value-only
/// style — no derive dependency); additive evolution only, unknown fields
/// ignored on read.
#[derive(Debug, Clone, PartialEq, Eq, Default)]
pub struct StructuralOpPayload {
    /// The op's primary subject: source path (folder or file).
    pub from: String,
    /// The op's destination path; equals `from` for create/delete.
    pub to: String,
    /// Per-file `(old, new)` path pairs (folder ops enumerate contents).
    pub moved: Vec<(String, String)>,
    /// U2-3's applied rewrites, for byte-exact undo.
    pub rewrites: Vec<RewriteOutcome>,
    /// `DeleteFile` only (O-1 #539): the deleted file's op-log name
    /// stem, captured before its `files` row went. The durable
    /// stem↔path association O-3's deleted-file recovery joins on.
    /// `None` for every other op kind and for pre-O-1 journal rows.
    pub oplog_name: Option<String>,
}

impl StructuralOpPayload {
    pub fn to_json(&self) -> String {
        let mut payload = json!({
            "from": self.from,
            "to": self.to,
            "moved": self.moved.iter().map(|(a, b)| json!([a, b])).collect::<Vec<_>>(),
            "rewrites": self.rewrites.iter().map(|r| json!({
                "path": r.path,
                "hash_before": r.hash_before,
                "hash_after": r.hash_after,
            })).collect::<Vec<_>>(),
        });
        if let Some(name) = &self.oplog_name {
            payload["oplog_name"] = json!(name);
        }
        payload.to_string()
    }

    /// None on any shape violation — a corrupt journal row renders that op
    /// non-undoable with a clear error rather than a panic.
    pub fn from_json(payload: &str) -> Option<Self> {
        let value: Value = serde_json::from_str(payload).ok()?;
        let obj = value.as_object()?;
        let str_field = |key: &str| -> Option<String> {
            obj.get(key).and_then(Value::as_str).map(str::to_string)
        };
        let moved = obj
            .get("moved")
            .and_then(Value::as_array)
            .map(|pairs| {
                pairs
                    .iter()
                    .filter_map(|pair| {
                        let pair = pair.as_array()?;
                        Some((
                            pair.first()?.as_str()?.to_string(),
                            pair.get(1)?.as_str()?.to_string(),
                        ))
                    })
                    .collect()
            })
            .unwrap_or_default();
        let rewrites = obj
            .get("rewrites")
            .and_then(Value::as_array)
            .map(|rows| {
                rows.iter()
                    .filter_map(|row| {
                        let row = row.as_object()?;
                        Some(RewriteOutcome {
                            path: row.get("path")?.as_str()?.to_string(),
                            hash_before: row.get("hash_before")?.as_str()?.to_string(),
                            hash_after: row.get("hash_after")?.as_str()?.to_string(),
                        })
                    })
                    .collect()
            })
            .unwrap_or_default();
        Some(Self {
            from: str_field("from")?,
            to: str_field("to")?,
            moved,
            rewrites,
            oplog_name: str_field("oplog_name"),
        })
    }
}
