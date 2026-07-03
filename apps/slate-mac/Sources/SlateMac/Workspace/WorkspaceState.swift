// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// The observable owner of the `WorkspaceModel` (Milestone U1).
///
/// Views never mutate the model directly — every mutation funnels through
/// methods here so invariants and (from U1-2 on) per-tab document lifecycle
/// have a single choke point.
///
/// U1-4 scope: the model mirrors `AppState.selectedFilePath` — one group,
/// at most one tab — and drives no rendering decisions yet. `NoteContentView`
/// remains the single source of the center column's visual states, so the
/// migration is strictly behavior-preserving. U1-2 inverts the relationship
/// (tabs own documents; the mirror becomes the real state).
@MainActor
final class WorkspaceState: ObservableObject {

    @Published private(set) var model = WorkspaceModel()

    /// Mirror today's single-selection world into the model: a selected path
    /// is one `.markdown` tab in the (sole) group; deselection empties the
    /// workspace. Called from `AppState.handleSelectionChange` AFTER the
    /// dirty-navigation gate, so a parked (rolled-back) selection never
    /// reaches the model.
    func mirrorSingleSelection(_ path: String?) {
        if let path {
            model.replaceActiveTabItem(.markdown(path: path))
        } else if let activeTab = model.activeGroup.activeTabID {
            model.closeTab(activeTab)
        }
        assert(model.validate().isEmpty, "workspace invariants: \(model.validate())")
    }
}
