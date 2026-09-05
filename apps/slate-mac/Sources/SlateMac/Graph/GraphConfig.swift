// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit

// The persisted graph-tab configuration (Milestone P, P2-4 #560) is
// CORE'S schema since W6-2 PR 0b (contracts doc 0b-12): `GraphConfig`,
// `GraphFilterConfig`, `GraphGroup`, `GraphDisplay`, `GraphForcesConfig`,
// `GraphColorToken` and `GraphRingStyle` are the generated types, and the
// defaults, clamps, version rule, unknown-key preservation and group rules
// live in `slate_core::graph_config`. What remains here is the Swift sugar
// the views read — and the colours, which are a rendering.

extension GraphConfig {
    static let `default` = graphConfigDefault()

    /// The group that colours a node with `label`: FIRST-match-wins over
    /// the ordered list, core's rule (the diagram reads the topology
    /// entry's `group` instead — one crossing per rebuild, design B).
    func matchingGroup(for label: String) -> GraphGroup? {
        graphConfigMatchingGroup(config: self, label: label).map { groups[Int($0)] }
    }
}

extension GraphFilterConfig {
    static let `default` = GraphConfig.default.filters

    /// The backend projection filter (drops the client-only name query).
    var backend: GraphFilter {
        GraphFilter(
            includeAttachments: includeAttachments, includeGhosts: includeGhosts,
            orphansOnly: orphansOnly)
    }
}

extension GraphDisplay {
    static let `default` = GraphConfig.default.display
}

extension GraphForcesConfig {
    static let `default` = GraphConfig.default.forces

    var layoutForces: LayoutForces {
        LayoutForces(
            center: Float(center), repel: Float(repel), link: Float(link),
            linkDistance: Float(linkDistance))
    }
}

/// The eight-slot palette: the tokens and titles are core's (T71); each
/// slot's COLOUR is a rendering and stays here (asserted against APCA in
/// tests).
extension GraphColorToken: CaseIterable {
    /// Core's ordered palette (T71), fetched once (design B): the picker
    /// and `allCases` are built from the vector, never from a literal.
    static let specs: [GraphColorTokenSpec] = graphColorTokens()

    public static var allCases: [GraphColorToken] { specs.map(\.token) }

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

    var title: String { Self.specs.first { $0.token == self }?.title ?? "" }
}

/// The ring styles that carry group membership without relying on colour
/// (spec §P2-4): the tokens and titles are core's (T72); the dash pattern
/// is a rendering.
extension GraphRingStyle: CaseIterable {
    /// Core's ordered ring styles, fetched once (design B).
    static let specs: [GraphRingStyleSpec] = graphRingStyles()

    public static var allCases: [GraphRingStyle] { specs.map(\.style) }

    /// The `CAShapeLayer.lineDashPattern` for this style (nil = solid);
    /// `double` is rendered as a thicker ring by the caller.
    var dashPattern: [NSNumber]? {
        switch self {
        case .solid, .double: return nil
        case .dashed: return [4, 2]
        case .dotted: return [1, 2]
        }
    }

    var title: String { Self.specs.first { $0.style == self }?.title ?? "" }
}
