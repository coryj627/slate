// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Foundation

/// Graph verbosity levels (mirrors `CanvasVerbosity`, t0 §1.2). Persisted
/// alongside the other graph settings once `.slate/graph.json` lands
/// (P2-4); default `.standard` until then.
enum GraphVerbosity: String, Codable, CaseIterable {
    case terse
    case standard
    case verbose

    var title: String {
        switch self {
        case .terse: return "Terse"
        case .standard: return "Standard"
        case .verbose: return "Verbose"
        }
    }
}

/// A graph node reference for row-focus phrasing (P1-1 VoiceOver copy).
/// Fields come straight from a backend `GraphNode`; the announcer never
/// re-derives labels or counts.
struct GraphRowRef: Equatable {
    var label: String
    var linksIn: UInt32
    var linksOut: UInt32
    /// True for `NodeKind.ghost` (unresolved target).
    var isGhost: Bool
    /// Ghosts only: inbound reference count (spec phrases ghosts by
    /// references, not links-in/out).
    var references: UInt32
    /// True when the focused relationship is an embed (`![[…]]`).
    var isEmbed: Bool
}

/// Everything graph UI code may say out loud (P1 VoiceOver copy; P2
/// extends). No graph code calls `postAccessibilityAnnouncement`
/// directly — enforced by
/// `GraphAnnouncerTests.testNoDirectAnnouncementsUnderGraph`.
enum GraphEvent: Equatable {
    /// Focus moved to a connections/table row (§P1-1 row copy).
    /// Navigation-class → coalesced so a held arrow announces the
    /// resting row.
    case rowFocused(GraphRowRef)
    /// A pre-rendered backend `audioSummary` (neighborhood or snapshot)
    /// surfaced on depth change or initial load. Posted immediately.
    case summary(String)
    /// Re-rooting the connections panel on a node: `"Connections:
    /// {label}"`. The authoritative neighborhood summary follows from
    /// the load's own `.summary` (spec: label first, then summary).
    case reRooted(label: String)
    /// A count/preset announcement (`"{k} orphaned notes."` etc.).
    case status(String)
    /// Errors: assertive.
    case error(String)
}

/// The one announcement funnel for every graph surface (mirrors
/// `CanvasAnnouncer` / #518): assembles P1 copy per verbosity, coalesces
/// rapid navigation events (~200 ms, final state wins), and posts with
/// the right priority (navigation/status polite; errors assertive).
@MainActor
final class GraphAnnouncer: ObservableObject {
    @Published var verbosity: GraphVerbosity

    private enum EventClass: Hashable {
        case navigation
    }

    private let post: (String, NSAccessibilityPriorityLevel) -> Void
    private let coalesceWindow: TimeInterval
    private var pending:
        [EventClass: (work: DispatchWorkItem, text: String, priority: NSAccessibilityPriorityLevel)] = [:]

    init(
        verbosity: GraphVerbosity = .standard,
        coalesceWindow: TimeInterval = 0.2,
        post: @escaping (String, NSAccessibilityPriorityLevel) -> Void = {
            postAccessibilityAnnouncement($0, priority: $1)
        }
    ) {
        self.verbosity = verbosity
        self.coalesceWindow = coalesceWindow
        self.post = post
    }

    /// The only announcement API graph code may use.
    func announce(_ event: GraphEvent) {
        let text = phrase(event)
        guard !text.isEmpty else { return }
        switch event {
        case .rowFocused:
            debounce(.navigation, text: text, priority: .medium)
        case .error:
            flushAllPending()
            post(text, .high)
        case .summary, .reRooted, .status:
            post(text, .medium)
        }
    }

    // MARK: Grammar assembly (§P1-1 row copy)

    /// Row-focus copy (spec §P1-1, normative — strings VERBATIM):
    /// `"{label}, {n} links in, {m} links out"`; ghosts:
    /// `"{label}, unresolved, {n} references"`; embeds append `", embed"`.
    /// The spec's templates keep "links"/"references" plural regardless
    /// of count, so we substitute the count and never re-pluralize
    /// (review round 1 finding 5). Terse collapses to the label.
    func rowPhrase(_ ref: GraphRowRef) -> String {
        if verbosity == .terse {
            return ref.label
        }
        var text: String
        if ref.isGhost {
            text = "\(ref.label), unresolved, \(ref.references) references"
        } else {
            text = "\(ref.label), \(ref.linksIn) links in, \(ref.linksOut) links out"
        }
        if ref.isEmbed { text += ", embed" }
        return text
    }

    private func phrase(_ event: GraphEvent) -> String {
        switch event {
        case .rowFocused(let ref):
            return rowPhrase(ref)
        case .summary(let text), .status(let text), .error(let text):
            return text
        case .reRooted(let label):
            return "Connections: \(label)"
        }
    }

    // MARK: Coalescing (mirrors CanvasAnnouncer)

    private func debounce(
        _ eventClass: EventClass, text: String, priority: NSAccessibilityPriorityLevel
    ) {
        pending[eventClass]?.work.cancel()
        let work = DispatchWorkItem { [weak self] in
            guard let self else { return }
            guard let entry = self.pending.removeValue(forKey: eventClass) else { return }
            self.post(entry.text, entry.priority)
        }
        pending[eventClass] = (work, text, priority)
        DispatchQueue.main.asyncAfter(deadline: .now() + coalesceWindow, execute: work)
    }

    private func flushAllPending() {
        for (_, entry) in pending { entry.work.cancel() }
        pending = [:]
    }

    /// Test hook: emit pending debounced posts NOW.
    func flushForTests() {
        let items = pending
        pending = [:]
        for (_, entry) in items {
            entry.work.cancel()
            post(entry.text, entry.priority)
        }
    }
}
