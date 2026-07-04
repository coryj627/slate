// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Foundation

/// Canvas verbosity levels (t0 §1.2 matrix). Persisted via
/// `PreferencesStore`; live-switchable from Settings.
enum CanvasVerbosity: String, Codable, CaseIterable {
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

/// Canvas announcement preferences (persisted blob).
struct CanvasPrefs: Codable, Equatable {
    var verbosity: CanvasVerbosity = .standard
}

/// A card reference for phrasing (t0 §1.1): backend-derived kind word
/// + display title. The announcer never re-derives titles or geometry —
/// every payload arrives from backend rows/summaries.
struct CanvasCardRef: Equatable {
    /// "text" | "file" | "image" | "link" | "group" (backend kind_label).
    var kind: String
    var title: String

    /// `⟨Type⟩ card "title"`; groups phrase as `Group "label"`.
    var phrase: String {
        kind == "group" ? "Group \"\(title)\"" : "\(kind.capitalized) card \"\(title)\""
    }
}

/// Everything canvas code may say out loud (t0 §1 grammars). No canvas
/// code calls `postAccessibilityAnnouncement` directly — DoD §H,
/// enforced by `CanvasAnnouncerTests.testNoDirectAnnouncementsUnderCanvas`.
enum CanvasEvent: Equatable {
    /// Selection moved to a card (§1.2 verbosity matrix).
    case movedTo(
        card: CanvasCardRef, ordinal: UInt32, total: UInt32,
        container: String?, connectionCount: UInt32,
        colorName: String?, marked: Bool)
    case groupEntered(label: String, cardCount: Int)
    case groupLeft(label: String)
    /// Following a connection (§1.2 direction phrases). `towardOther`
    /// is true when traversing from the current card to `other`.
    case connectionTraversed(
        direction: CanvasEdgeDirection, other: CanvasCardRef,
        label: String?, towardOther: Bool)
    /// `⟨Verb past⟩ ⟨object⟩ ⟨relative detail⟩` (§1.3), pre-assembled
    /// by the typed builders below — never ad-hoc caller prose.
    case confirmation(String)
    /// A destructive confirmation: carries the undo hint at standard+
    /// verbosity (§1.3).
    case destructiveConfirmation(String)
    /// Exactly one summary for an action over N marked cards (§1.5).
    case bulk(String)
    /// Mode lifecycle strings (t0 §2 — #364/#521/#523 consume).
    case mode(String)
    /// Filter narration (#373 consumes; coalesced).
    case filter(String)
    /// Errors and conflicts: assertive (§1.5 priority rule).
    case error(String)
    /// Container-level statements (surface switches, load summaries).
    case status(String)
}

/// The one announcement funnel for every canvas surface (#518, DoD §H):
/// assembles t0 §1 grammar strings per verbosity, coalesces rapid
/// same-class events (~200 ms, final state wins), and posts with the
/// right priority (navigation polite; errors assertive).
@MainActor
final class CanvasAnnouncer: ObservableObject {
    /// Live-switchable verbosity (§1.2); persisted by the owner.
    @Published var verbosity: CanvasVerbosity

    /// Event classes for coalescing (§1.5): same-class events within
    /// the window collapse to the latest.
    private enum EventClass: Hashable {
        case navigation
        case filter
    }

    private let post: (String, NSAccessibilityPriorityLevel) -> Void
    private let coalesceWindow: TimeInterval
    private var pending:
        [EventClass: (work: DispatchWorkItem, text: String, priority: NSAccessibilityPriorityLevel)] = [:]

    init(
        verbosity: CanvasVerbosity = .standard,
        coalesceWindow: TimeInterval = 0.2,
        post: @escaping (String, NSAccessibilityPriorityLevel) -> Void = {
            postAccessibilityAnnouncement($0, priority: $1)
        }
    ) {
        self.verbosity = verbosity
        self.coalesceWindow = coalesceWindow
        self.post = post
    }

    /// The only announcement API canvas code may use.
    func announce(_ event: CanvasEvent) {
        let text = phrase(event)
        guard !text.isEmpty else { return }
        switch event {
        case .movedTo, .groupEntered, .groupLeft, .connectionTraversed:
            debounce(.navigation, text: text, priority: .medium)
        case .filter:
            debounce(.filter, text: text, priority: .medium)
        case .error:
            flushAllPending()
            post(text, .high)
        case .confirmation, .destructiveConfirmation, .bulk, .mode, .status:
            post(text, .medium)
        }
    }

    /// t0 §1.4: the ⌃⌘I readback — always verbose-grade regardless of
    /// the setting. Mark/mode/filter context is UI-owned and merged
    /// here (pull-based; the panel shows the same string).
    func whereAmIText(
        _ ctx: CanvasWhereAmI, marked: Bool,
        activeMode: String?, filterSummary: String?
    ) -> String {
        let card = CanvasCardRef(kind: ctx.kind, title: ctx.title)
        var parts: [String] = [card.phrase]
        parts.append(
            ctx.groupPath.isEmpty
                ? "at canvas level" : "in \(ctx.groupPath.joined(separator: " › "))")
        parts.append("\(ctx.ordinalN) of \(ctx.totalM)")
        parts.append(
            "\(ctx.connectionCount) connection\(ctx.connectionCount == 1 ? "" : "s") (\(ctx.inCount) in, \(ctx.outCount) out)"
        )
        if let color = ctx.colorName { parts.append(color) }
        if marked { parts.append("marked") }
        if let activeMode { parts.append(activeMode) }
        if let filterSummary { parts.append(filterSummary) }
        return parts.joined(separator: ", ")
    }

    // MARK: Grammar assembly (t0 §1.1–§1.3)

    private func phrase(_ event: CanvasEvent) -> String {
        switch event {
        case .movedTo(let card, let n, let m, let container, let connections, let color, let marked):
            switch verbosity {
            case .terse:
                return card.title
            case .standard:
                return "\(card.phrase), \(n) of \(m) in \(container ?? "canvas")"
            case .verbose:
                var text = "\(card.phrase), \(n) of \(m) in \(container ?? "canvas")"
                text += ", \(connections) connection\(connections == 1 ? "" : "s")"
                if let color { text += ", \(color)" }
                if marked { text += ", marked" }
                return text
            }
        case .groupEntered(let label, let count):
            return "Entering group \"\(label)\", \(count) card\(count == 1 ? "" : "s")"
        case .groupLeft(let label):
            return "Leaving group \"\(label)\""
        case .connectionTraversed(let direction, let other, let label, let towardOther):
            let phrase: String
            switch direction {
            case .outgoing: phrase = towardOther ? "Connects to" : "Connected from"
            case .incoming: phrase = towardOther ? "Connected from" : "Connects to"
            case .bidirectional, .undirected: phrase = "Linked with"
            }
            var text = "\(phrase) \(other.phrase)"
            if let label { text += ", labelled \"\(label)\"" }
            return text
        case .confirmation(let text), .bulk(let text), .mode(let text),
            .filter(let text), .error(let text), .status(let text):
            return text
        case .destructiveConfirmation(let text):
            // The undo hint rides at standard+ (§1.3); terse users
            // asked for minimum chrome.
            return verbosity == .terse ? text : "\(text) — ⌘Z to undo"
        }
    }

    // MARK: Typed confirmation builders (§1.3 patterns)

    static func createdText(card: CanvasCardRef, relative: CanvasRelativeDesc) -> String {
        "Created \(card.phrase.prefix(1).lowercased() + card.phrase.dropFirst()) \(relativePhrase(relative))"
    }

    static func relativePhrase(_ relative: CanvasRelativeDesc) -> String {
        switch relative {
        case .below(let anchor): return "below \"\(anchor)\""
        case .rightOf(let anchor): return "right of \"\(anchor)\""
        case .above(let anchor): return "above \"\(anchor)\""
        case .leftOf(let anchor): return "left of \"\(anchor)\""
        case .atOrigin: return "at the canvas origin"
        }
    }

    static func undidText(actionName: String) -> String { "Undid: \(actionName)" }
    static func redidText(actionName: String) -> String { "Redid: \(actionName)" }

    // MARK: Coalescing (§1.5)

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

    /// Errors must not be preceded by a stale queued navigation line
    /// (§1.5: the error supersedes; navigation context is re-derivable
    /// by moving again).
    private func flushAllPending() {
        for (_, entry) in pending { entry.work.cancel() }
        pending = [:]
    }

    /// Test hook: emit pending debounced posts NOW (deterministic tests
    /// without wall-clock waits).
    func flushForTests() {
        let items = pending
        pending = [:]
        for (_, entry) in items {
            entry.work.cancel()
            post(entry.text, entry.priority)
        }
    }
}
