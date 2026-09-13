// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

/// Expected filesystem identity for an inverse. IDs can eventually be reused
/// after deletion; a successful check and rename operate on the same live object.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct StructuralIdentity {
    pub entry: String,
    /// Indexed folder note that a compound rename will also rename, if present.
    pub folder_note: Option<String>,
}
