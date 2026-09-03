// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Foundation

// MARK: - Generated-enum extensions (W6-2 PR 0a; contracts doc 0a-5, 0a-18)

// `GraphVerbosity` is core's since W6-2 PR 0a (`slate_core::a11y`). This
// extension gives the generated enum what a store and a settings picker
// need — the canvas extension's shape (`CanvasAnnouncer.swift`). Nothing
// persists the level YET: mac never did (P2-4's intention never landed),
// and 0b adds the `verbosity` key to `.slate/graph.json`'s schema; until
// then every announcer starts at `.standard`.
//
// **Persistence-tag stability — DO NOT RENAME.** The literals in
// `persistenceTag` are the strings 0b's store writes into the vault
// file; renaming one silently resets a user's choice.
extension GraphVerbosity: Codable, CaseIterable {
    public static var allCases: [GraphVerbosity] {
        [.terse, .standard, .verbose]
    }

    /// Settings picker label (t0 §1.2 level names).
    var title: String {
        switch self {
        case .terse: return "Terse"
        case .standard: return "Standard"
        case .verbose: return "Verbose"
        }
    }

    private var persistenceTag: String {
        switch self {
        case .terse: return "terse"
        case .standard: return "standard"
        case .verbose: return "verbose"
        }
    }

    public init(from decoder: Decoder) throws {
        let value = try decoder.singleValueContainer().decode(String.self)
        switch value {
        case "terse": self = .terse
        case "standard": self = .standard
        case "verbose": self = .verbose
        default:
            throw DecodingError.dataCorruptedError(
                in: try decoder.singleValueContainer(),
                debugDescription: "Unknown GraphVerbosity: \(value)"
            )
        }
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.singleValueContainer()
        try container.encode(persistenceTag)
    }
}

// The Graph tab's two projections are core's `GraphSurfaceMode` (W6-2 PR
// 0a) — the local `GraphTabMode` it replaces was a `String`-raw-valued
// enum, so the config store's `mode` key keeps the SAME two tags through
// this explicit mapping (P2-4 v1 schema, unchanged on disk).
//
// **Persistence-tag stability — DO NOT RENAME.**
extension GraphSurfaceMode: CaseIterable {
    public static var allCases: [GraphSurfaceMode] {
        [.table, .diagram]
    }

    var title: String {
        switch self {
        case .table: return "Table"
        case .diagram: return "Diagram"
        }
    }

    /// The `.slate/graph.json` `mode` tag.
    var persistenceTag: String {
        switch self {
        case .table: return "table"
        case .diagram: return "diagram"
        }
    }

    init?(persistenceTag: String) {
        switch persistenceTag {
        case "table": self = .table
        case "diagram": self = .diagram
        default: return nil
        }
    }
}

// MARK: - The announcer

/// The one announcement funnel for every graph surface (P1-1 #554; W6-2
/// PR 0a, #746).
///
/// Since W6-2 PR 0a the GRAMMAR is core's: callers hand over a typed
/// `GraphA11yEvent` from `slate_core::a11y` and this class renders it
/// through the FFI, so the spoken text AND the priority both come from
/// the vocabulary — never composed here, never re-classified here. What
/// stays host-side is exactly what a pure render cannot own: the ~200 ms
/// same-class coalescing window (a render has no clock), the filter
/// count's fire-time relevance gate, `cancelPending` on view departure,
/// and the live-switchable verbosity the row family takes as a parameter
/// (contracts doc 0a-9, 0aD-3). The class membership is pinned core-side
/// in the one "Coalescing class keys" list; slate-uniffi's
/// `the_mac_graph_coalescing_switch_matches_the_pinned_class_list` reads
/// the switch below against it.
///
/// No graph code calls `postAccessibilityAnnouncement` directly — DoD §H,
/// enforced by `GraphAnnouncerTests.testNoDirectAnnouncementsUnderGraph`.
@MainActor
final class GraphAnnouncer: ObservableObject {
    /// Live-switchable verbosity, passed per row event (core holds no
    /// "current verbosity"). Default-only until 0b's store lands.
    @Published var verbosity: GraphVerbosity

    /// Event classes for coalescing: same-class events within the window
    /// collapse to the latest; each class is independent, so a settled
    /// message can neither cancel nor be cancelled by a force value.
    private enum EventClass: Hashable {
        case navigation
        case filter
        case forceValue
        case settle
    }

    /// The production coalescing window (t0 §1.5's ~200 ms), test-visible
    /// so the pin is a number and not a stopwatch (contracts doc 0a-9).
    static let defaultCoalesceWindow: TimeInterval = 0.2

    private let post: (String, NSAccessibilityPriorityLevel) -> Void
    private let coalesceWindow: TimeInterval
    private var pending:
        [EventClass: (
            work: DispatchWorkItem, text: String, priority: NSAccessibilityPriorityLevel,
            gate: () -> Bool
        )] = [:]

    /// The seam is `(text, priority)` rather than an event because the
    /// coalescer holds a RENDERED line: the window's winner is decided
    /// after the render, and the loser is dropped without being spoken.
    /// The default posts through the poster layer — never the string
    /// primitive, never `.hostComposed` (the residue census dropped this
    /// class's site: 29 → 28).
    init(
        verbosity: GraphVerbosity = .standard,
        coalesceWindow: TimeInterval = GraphAnnouncer.defaultCoalesceWindow,
        post: @escaping (String, NSAccessibilityPriorityLevel) -> Void = { text, priority in
            let level: AnnouncementPriority = priority == .high ? .high : .medium
            AppKitAnnouncementPoster().post(text, priority: level)
        }
    ) {
        self.verbosity = verbosity
        self.coalesceWindow = coalesceWindow
        self.post = post
    }

    /// The only announcement API graph code may use.
    func announce(_ event: GraphA11yEvent) {
        emit(.graph(event: event), coalescing: Self.coalescingClass(of: event), gate: { true })
    }

    /// The ONE gated entry (contracts doc 0a-13, 0aD-3): the filter count
    /// carries a FIRE-TIME relevance gate — a count queued while the graph
    /// was focused is dropped if focus has since moved to another split
    /// pane (which leaves the graph mounted, so `onDisappear` never runs).
    /// Restricted to the filter class: no other event can acquire a host
    /// relevance gate.
    func announceFilterCount(shown: UInt32, total: UInt32, gate: @escaping () -> Bool) {
        let event: GraphA11yEvent = .graphFilterCount(shown: shown, total: total)
        emit(.graph(event: event), coalescing: Self.coalescingClass(of: event), gate: gate)
    }

    /// Relay a core event that is NOT graph vocabulary — the shared data
    /// grid raises its own sort/filter events, and the graph table must
    /// pass their rendered text AND priority through rather than
    /// re-wrapping them at a priority of its own choosing (0a-13, 0a-D2).
    func relay(_ event: A11yEvent) {
        emit(event, coalescing: nil, gate: { true })
    }

    /// The copy of core's class list (a11y.rs "Coalescing class keys",
    /// the graph rows). Parsed by slate-uniffi's tripwire: keep the shape.
    private static func coalescingClass(of event: GraphA11yEvent) -> EventClass? {
        switch event {
        case .graphRow: return .navigation
        case .graphFilterCount: return .filter
        case .graphForceValue: return .forceValue
        case .graphLayoutSettled: return .settle
        default: return nil
        }
    }

    private func emit(_ event: A11yEvent, coalescing eventClass: EventClass?, gate: @escaping () -> Bool) {
        let rendered = a11yRender(event: event)
        guard !rendered.text.isEmpty else { return }
        let priority = AnnouncementPriority(rendered.priority)
        if let eventClass {
            debounce(eventClass, text: rendered.text, priority: priority.nsPriority, gate: gate)
            return
        }
        // A High event — announced or relayed — supersedes every queued
        // line: navigation context is re-derivable by moving again, and a
        // stale count or value must not speak after the error (0a-9).
        if priority == .high {
            flushAllPending()
        }
        post(rendered.text, priority.nsPriority)
    }

    // MARK: Coalescing (mirrors CanvasAnnouncer)

    private func debounce(
        _ eventClass: EventClass, text: String, priority: NSAccessibilityPriorityLevel,
        gate: @escaping () -> Bool
    ) {
        pending[eventClass]?.work.cancel()
        // The class is `@MainActor` and every caller is therefore on the
        // main actor; this DispatchWorkItem is the one non-isolated closure
        // in the type and is dispatched ONLY to `DispatchQueue.main`, so
        // `assumeIsolated` makes that isolation explicit.
        let work = DispatchWorkItem { [weak self] in
            MainActor.assumeIsolated {
                guard let self else { return }
                guard let entry = self.pending.removeValue(forKey: eventClass) else { return }
                // Re-check relevance AT FIRE TIME.
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
    /// after the user has moved on.
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
