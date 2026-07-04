// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// The three switchable canvas surfaces (t2 shared architecture). The
/// keyboard navigator (#364) is deliberately NOT a fourth surface — it
/// is the command layer hosted by all of them.
///
/// Raw values are the persistence strings in `workspace.json`
/// (`activeCanvasSurface`, #369): additive, optional, unknown values
/// restore as `.outline` — the structured-first default landing.
enum CanvasSurface: String, CaseIterable {
    case outline
    case table
    case visual

    /// Human-visible name (palette command labels, switcher).
    var title: String {
        switch self {
        case .outline: return "Outline"
        case .table: return "Table"
        case .visual: return "Visual"
        }
    }
}

/// The single source of truth every canvas surface binds to (t2):
/// mutating this object is the only way selection changes — surfaces
/// never hold local selection state. One instance per
/// `CanvasDocument`, so panes showing the same canvas share selection
/// and marks (the U1 NoteDocument registry pattern).
@MainActor
final class CanvasSelection: ObservableObject {
    /// The selected card/connection anchor (a node id), if any.
    @Published var selected: String?
    /// Marked set for mark-then-act bulk operations (#524 populates;
    /// the slot ships with the container so AX values can expose it
    /// from day one — t0 §3 inspectability).
    @Published var marked: Set<String> = []
}
