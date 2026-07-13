// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit

/// The persisted graph-tab configuration (Milestone P, P2-4 #560) —
/// filters, colour groups, display knobs, forces, the last mode, and the
/// Connections depth that P1-1 owned. Serialized to `.slate/graph.json`
/// v1 (single-writer: only the Mac app writes it, so no lock — a separate
/// file from `prefs.json` precisely to avoid contending on its flock).
struct GraphConfig: Equatable {
    static let version = 1

    var filters: GraphFilterConfig
    var groups: [GraphGroup]
    var display: GraphDisplay
    var forces: GraphForcesConfig
    var mode: GraphTabMode
    /// Migrated from P1-1's Connections depth setting (clamped 1…3).
    var connectionsDepth: Int

    static let `default` = GraphConfig(
        filters: .default,
        groups: [],
        display: .default,
        forces: .default,
        mode: .table,
        connectionsDepth: 1)

    /// The group that colours a node with `label`: FIRST-match-wins over
    /// the ordered list (Obsidian parity). A group's query is a
    /// case/diacritic-insensitive label substring; an empty query never
    /// matches (so a blank rule can't swallow every node).
    func matchingGroup(for label: String) -> GraphGroup? {
        groups.first { group in
            let q = group.query.trimmingCharacters(in: .whitespaces)
            guard !q.isEmpty else { return false }
            return label.range(of: q, options: [.caseInsensitive, .diacriticInsensitive]) != nil
        }
    }
}

/// The graph's effective filter — the backend toggles (shared with the
/// Table's `GraphFilter`) plus the client-side name query. The SAME
/// predicate the Table applies, so both projections show one node set
/// (spec §P2-4 "single source of truth").
struct GraphFilterConfig: Equatable {
    var includeAttachments: Bool
    var includeGhosts: Bool
    var orphansOnly: Bool
    /// Client-side label substring (case/diacritic-insensitive); empty = all.
    var nameQuery: String

    static let `default` = GraphFilterConfig(
        includeAttachments: false, includeGhosts: true, orphansOnly: false, nameQuery: "")

    /// The backend projection filter (drops the client-only name query).
    var backend: GraphFilter {
        GraphFilter(
            includeAttachments: includeAttachments, includeGhosts: includeGhosts,
            orphansOnly: orphansOnly)
    }

    /// The ONE predicate both Table and Diagram apply to a node — the
    /// single source of truth (spec §P2-4). Backend-kind is already
    /// applied by the projection; this is the client-side name needle.
    func matches(label: String) -> Bool {
        let needle = nameQuery.trimmingCharacters(in: .whitespaces)
        guard !needle.isEmpty else { return true }
        return label.range(of: needle, options: [.caseInsensitive, .diacriticInsensitive]) != nil
    }
}

/// Display knobs (spec §P2-4). Multipliers are 0.5…2.0 with a 1.0
/// default; text-fade is the zoom below which labels hide.
struct GraphDisplay: Equatable {
    var arrows: Bool
    var textFadeZoom: Double
    var nodeSizeMultiplier: Double
    var linkThickness: Double

    static let `default` = GraphDisplay(
        arrows: false, textFadeZoom: 0.55, nodeSizeMultiplier: 1.0, linkThickness: 1.0)
}

/// The four Obsidian-parity force sliders (0…1), mirrored into the
/// kernel's `LayoutForces`.
struct GraphForcesConfig: Equatable {
    var center: Double
    var repel: Double
    var link: Double
    var linkDistance: Double

    static let `default` = GraphForcesConfig(center: 0.5, repel: 0.5, link: 0.5, linkDistance: 0.5)

    var layoutForces: LayoutForces {
        LayoutForces(
            center: Float(center), repel: Float(repel), link: Float(link),
            linkDistance: Float(linkDistance))
    }
}

/// One colour-group rule: a label substring query → a palette colour +
/// ring style. First match wins (Obsidian parity). The ring style is a
/// SECOND channel so colour is never the sole signal (WCAG 1.4.1).
struct GraphGroup: Equatable {
    var query: String
    var colorToken: GraphColorToken
    var ringStyle: GraphRingStyle
}

/// An 8-slot colour palette, each slot a system-managed colour that
/// clears APCA in both appearances (asserted in tests). Raw values are
/// the stable tokens persisted in `graph.json`.
enum GraphColorToken: String, CaseIterable {
    case red, orange, yellow, green, teal, blue, purple, pink

    var color: NSColor {
        switch self {
        case .red: return .systemRed
        case .orange: return .systemOrange
        case .yellow: return .systemYellow
        case .green: return .systemGreen
        case .teal: return .systemTeal
        case .blue: return .systemBlue
        case .purple: return .systemPurple
        case .pink: return .systemPink
        }
    }

    var title: String { rawValue.capitalized }
}

/// The ring styles that carry group membership without relying on colour
/// (spec §P2-4). Cycled automatically as groups are added.
enum GraphRingStyle: String, CaseIterable {
    case solid, dashed, double, dotted

    /// The `CAShapeLayer.lineDashPattern` for this style (nil = solid);
    /// `double` is rendered as a thicker ring by the caller.
    var dashPattern: [NSNumber]? {
        switch self {
        case .solid, .double: return nil
        case .dashed: return [4, 2]
        case .dotted: return [1, 2]
        }
    }
}
