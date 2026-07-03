// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Parked per-tab document state (U1-2, #454).
///
/// AppState's single-note `@Published` fields ARE the active tab's document —
/// no load/save/conflict machinery moved (see the U1-2 architecture amendment
/// in `docs/plans/08_ui_parity/specs/u1_spec.md`). A `NoteDocument` holds the
/// state of an INACTIVE tab between activations:
///
///   tab switch = snapshot(outgoing fields → its NoteDocument)
///              ⊕ restore(incoming NoteDocument → fields, skip disk read)
///
/// `text` and `hasUnsavedChanges` are `@Published` so an unfocused pane
/// (U1-3) can render parked content live; `updateEditorText` mirrors edits
/// into same-path parked documents (copy-on-write assign, O(1)) so a
/// duplicated tab never shows stale bytes.
@MainActor
final class NoteDocument: ObservableObject, Identifiable {
    nonisolated let id: TabID
    let path: String

    @Published var text: String = ""
    @Published var hasUnsavedChanges: Bool = false
    var savedBaselineText: String = ""
    var contentHash: String?
    var saveError: String?
    var saveConflict: SaveConflict?
    /// False until the tab has been activated (and thus disk-loaded) once.
    /// Restore short-circuits the disk read only when true.
    var hasLoaded: Bool = false

    init(id: TabID, path: String) {
        self.id = id
        self.path = path
    }
}
