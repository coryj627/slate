// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// The canonical node-action set shared by ALL graph projections — Table
/// rows, Diagram nodes, and the Connections leaf (Milestone P, P2-5 #561,
/// DoD §P-B "action parity"). Each projection builds its context menu /
/// VoiceOver custom actions from this ONE enum, so the three can never
/// silently drift apart (the parity drift test asserts exactly this).
///
/// `pin`/`unpin` is DELIBERATELY absent — it is diagram-only (a visual
/// layout affordance with no meaning in the Table/Connections lists) and is
/// exempted from parity with that rationale.
enum GraphRowAction: String, CaseIterable {
    case open
    case openInNewTab
    case showConnections
    case reveal
    case createNote

    /// The user-facing label — the VoiceOver action name AND the
    /// context-menu title, identical across every projection so the spoken
    /// action set matches byte-for-byte.
    var title: String {
        switch self {
        case .open: return "Open"
        case .openInNewTab: return "Open in New Tab"
        case .showConnections: return "Show connections"
        case .reveal: return "Reveal in File Tree"
        case .createNote: return "Create note"
        }
    }

    /// Whether this action applies to a node of the given ghost-ness: the
    /// four navigation actions need a real file; "Create note" applies only
    /// to a ghost (an unresolved target). No projection ever offers an
    /// action that would silently do nothing.
    func applies(toGhost isGhost: Bool) -> Bool {
        self == .createNote ? isGhost : !isGhost
    }

    /// The canonical, ordered action set a node of the given ghost-ness must
    /// expose — the exact list every projection is checked against.
    static func actions(forGhost isGhost: Bool) -> [GraphRowAction] {
        allCases.filter { $0.applies(toGhost: isGhost) }
    }
}

/// The cross-projection-stable identity for a graph node (Milestone P,
/// P2-5 #561). A raw backend `UInt64` id is only stable WITHIN one graph
/// generation (a rebuild reassigns ids — `graph.rs`), so it cannot be the
/// shared selection key. Instead every projection keys a node the way the
/// P1-2 Table already did: the vault path under `"p:"` for a real node, and
/// the percent-encoded folded label under `"g:"` for a ghost (no path).
/// Two disjoint namespaces, byte-stable, legible for ASCII labels.
enum GraphNodeKey {
    static func make(path: String?, label: String) -> String {
        if let path {
            return "p:\(path)"
        }
        // The backend keys ghosts on the raw UTF-8 bytes of the folded
        // target (no Unicode NFC), so percent-encode to keep byte-distinct
        // labels distinct while ASCII stays readable ("missing note" →
        // "missing%20note"). Fold with the INVARIANT (en_US_POSIX) locale,
        // not bare `lowercased()`, so the key is byte-stable regardless of
        // the user's system locale (e.g. Turkish "I"→"ı") — the Table and
        // Diagram must derive the SAME key for the same label on every
        // machine (Codoki review).
        let folded = label.lowercased(with: Locale(identifier: "en_US_POSIX"))
        let encoded = folded.addingPercentEncoding(withAllowedCharacters: .alphanumerics) ?? folded
        return "g:\(encoded)"
    }

    static func make(for node: GraphNode) -> String {
        make(path: node.path, label: node.label)
    }
}
