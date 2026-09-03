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
    // Filter-count narration is NOT an event: it needs a FIRE-TIME
    // relevance gate (a coalesced count must not speak after focus leaves
    // the graph), which an Equatable event can't carry — see
    // `announceFilterCount(_:gate:)`.
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
        case filter
        /// A force slider's "control value" during a drag — coalesces to
        /// the resting value (P2-4 finding 8).
        case forceValue
        /// The one "settled" state posted after the layout converges
        /// (separate class from `.forceValue` so the settled message can't
        /// cancel — or be cancelled by — the value message; P2-4 finding 8).
        case settle
    }

    private let post: (String, NSAccessibilityPriorityLevel) -> Void
    private let coalesceWindow: TimeInterval
    private var pending:
        [EventClass: (
            work: DispatchWorkItem, text: String, priority: NSAccessibilityPriorityLevel,
            gate: () -> Bool
        )] = [:]

    init(
        verbosity: GraphVerbosity = .standard,
        coalesceWindow: TimeInterval = 0.2,
        post: @escaping (String, NSAccessibilityPriorityLevel) -> Void = {
            // W0.5-3 residue: graph announcer engine
            postAccessibilityAnnouncement(
                .hostComposed(text: $0, priority: $1 == .high ? .high : .medium))
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

    /// Coalesced filter-count narration (`"{k} of {n} shown"`) with a
    /// FIRE-TIME relevance gate. Per-keystroke filtering coalesces to the
    /// resting count (round 1 finding 7), and `gate` is re-evaluated when
    /// the debounce actually fires — so a count queued while the graph
    /// was focused is dropped if focus has since moved to another split
    /// pane (which leaves the graph mounted, so `onDisappear` never runs
    /// — round 3 finding 2).
    func announceFilterCount(_ text: String, gate: @escaping () -> Bool) {
        guard !text.isEmpty else { return }
        debounce(.filter, text: text, priority: .medium, gate: gate)
    }

    /// A force slider's changed "control value" (e.g. "Repel force 70
    /// percent"), coalesced so a drag posts once at its resting value
    /// rather than per tick (P2-4 finding 8).
    func announceForceValue(_ text: String) {
        guard !text.isEmpty else { return }
        debounce(.forceValue, text: text, priority: .medium)
    }

    /// The layout's final "settled" state, posted (coalesced) once the
    /// re-heated layout converges (P2-4 finding 8).
    func announceSettle(_ text: String) {
        guard !text.isEmpty else { return }
        debounce(.settle, text: text, priority: .medium)
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
        _ eventClass: EventClass, text: String, priority: NSAccessibilityPriorityLevel,
        gate: @escaping () -> Bool = { true }
    ) {
        pending[eventClass]?.work.cancel()
        // The class is `@MainActor` and every caller is therefore on the
        // main actor (the compiler enforces this — a `@MainActor` method
        // can't be invoked synchronously off-main), so `pending` is only
        // ever mutated on the main thread. This DispatchWorkItem is the
        // one non-isolated closure in the type: it is dispatched ONLY to
        // `DispatchQueue.main` (the main actor's executor), so
        // `assumeIsolated` makes that main-actor isolation explicit and
        // strict-concurrency-safe rather than implicit (Codoki flagged
        // the implicit form as a data-race risk; the AccessibleDataGrid
        // custom-action handlers use the same idiom).
        let work = DispatchWorkItem { [weak self] in
            MainActor.assumeIsolated {
                guard let self else { return }
                guard let entry = self.pending.removeValue(forKey: eventClass) else { return }
                // Re-check relevance AT FIRE TIME (round 3 finding 2): a
                // count queued while the graph was focused is dropped if
                // the gate no longer holds.
                guard entry.gate() else { return }
                self.post(entry.text, entry.priority)
            }
        }
        pending[eventClass] = (work, text, priority, gate)
        DispatchQueue.main.asyncAfter(deadline: .now() + coalesceWindow, execute: work)
    }

    private func flushAllPending() {
        for (_, entry) in pending { entry.work.cancel() }
        pending = [:]
    }

    /// Drop any queued (debounced) announcements WITHOUT posting them —
    /// called when the graph view disappears or a vault opens/closes, so
    /// a coalesced count scheduled while the graph was active can't fire
    /// after the user has moved on (round 2 finding 8).
    func cancelPending() {
        flushAllPending()
    }

    /// Test hook: emit pending debounced posts NOW, honoring each entry's
    /// fire-time gate (so gating is observable in tests, matching prod).
    func flushForTests() {
        let items = pending
        pending = [:]
        for (_, entry) in items {
            entry.work.cancel()
            guard entry.gate() else { continue }
            post(entry.text, entry.priority)
        }
    }
}
