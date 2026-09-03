// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

// The canonical node-action set (Milestone P, P2-5 #561, DoD §P-B) and
// the cross-projection node key are CORE'S since W6-2 PR 0b (contracts
// doc 0b-3, 0b-9): `GraphRowAction` is the generated enum, its titles and
// its kind eligibility come from `graph_row_action_title` /
// `graph_row_actions` — fetched ONCE per process into the statics below
// (design B: a per-node crossing is forbidden) — and a node's identity
// is `GraphNode.stableKey`. What remains here is the Swift sugar the
// projections read.

extension GraphRowAction: CaseIterable {
    /// Core's eligibility vectors per kind, fetched once (design B): the
    /// action and its title, in core's order.
    private static let specs: [GraphNodeKind: [GraphRowActionSpec]] = Dictionary(
        uniqueKeysWithValues: [GraphNodeKind.note, .attachment, .ghost].map { ($0, graphRowActions(kind: $0)) })

    /// The canonical order: the union of the per-kind vectors — the
    /// generated enum is not CaseIterable, and no case is listed here.
    public static var allCases: [GraphRowAction] {
        var seen: [GraphRowAction] = []
        for kind in [GraphNodeKind.note, .ghost] {
            for spec in specs[kind] ?? [] where !seen.contains(spec.action) {
                seen.append(spec.action)
            }
        }
        return seen
    }

    /// The user-facing label — the VoiceOver action name AND the
    /// context-menu title, core's string from the vectors.
    var title: String {
        for vector in Self.specs.values {
            if let spec = vector.first(where: { $0.action == self }) { return spec.title }
        }
        return ""
    }

    /// Whether a node of the given ghost-ness is ELIGIBLE for this action
    /// (core's rule: the four navigation actions need a real file; "Create
    /// note" applies only to a ghost). Whether it can run NOW is the host's
    /// admission (0bD-8).
    func applies(toGhost isGhost: Bool) -> Bool {
        Self.actions(forGhost: isGhost).contains(self)
    }

    /// The canonical, ordered action set a node of the given ghost-ness is
    /// eligible for — core's vector.
    static func actions(forGhost isGhost: Bool) -> [GraphRowAction] {
        (specs[isGhost ? .ghost : .note] ?? []).map(\.action)
    }
}
