// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Foundation

// MARK: - CanvasVerbosity (FFI enum + Swift-side niceties)
//
// `CanvasVerbosity` is now the FFI-generated enum in
// `slate_uniffi.swift` (W6-1 PR 0a moved the canvas grammar to core,
// where verbosity is a PARAMETER on the two families whose template
// varies — contracts doc 0a-5). The host used to declare its own
// three-case copy; two same-named enums in one module do not compile,
// and the host copy had nothing left to do once core owned the
// matrix. This file adds the Swift-side niceties on top, exactly like
// `MathPrefs.swift` does for `MathVerbosity`:
// - `CaseIterable` for the Settings picker,
// - `title` for its labels,
// - `Codable` over a stable persistence tag for `PreferencesStore`.
//
// **Persistence-tag stability — DO NOT RENAME.** The literals in
// `persistenceTag` are written into users' UserDefaults through
// `PreferencesStore`; they are the same three strings the previous
// `String`-raw-valued host enum encoded, so stored prefs decode
// unchanged. Renaming one silently resets the user's choice.

extension CanvasVerbosity: Codable, CaseIterable {
    public static var allCases: [CanvasVerbosity] {
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
                debugDescription: "Unknown CanvasVerbosity: \(value)"
            )
        }
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.singleValueContainer()
        try container.encode(persistenceTag)
    }
}

/// Canvas announcement preferences (persisted blob).
struct CanvasPrefs: Codable, Equatable {
    var verbosity: CanvasVerbosity = .standard
}

/// The one announcement funnel for every canvas surface (#518, DoD §H).
///
/// Since W6-1 PR 0a the GRAMMAR is core's: callers hand over a typed
/// `CanvasA11yEvent` from `slate_core::a11y` and this class renders it
/// through the FFI, so the spoken text AND the priority both come from
/// the vocabulary — never composed here, never re-classified here.
/// What stays host-side is exactly what a pure render cannot own: the
/// ~200 ms same-class coalescing window (t0 §1.5 — a render has no
/// clock) and the persisted, live-switchable verbosity preference that
/// the two verbosity-varying families take as a parameter.
///
/// No canvas code calls `postAccessibilityAnnouncement` directly —
/// DoD §H, enforced by
/// `CanvasAnnouncerTests.testNoDirectAnnouncementsUnderCanvas`.
@MainActor
final class CanvasAnnouncer: ObservableObject {
    /// Live-switchable verbosity (§1.2); persisted by the owner and
    /// passed per event (core holds no "current verbosity").
    @Published var verbosity: CanvasVerbosity

    /// Event classes for coalescing (§1.5): same-class events within
    /// the window collapse to the latest. The MEMBERSHIP is pinned
    /// core-side in one doc comment on the canvas family so both hosts
    /// copy one list (contracts doc 0a-8); only the timing is ours.
    private enum EventClass: Hashable {
        case navigation
        case filter
    }

    private let post: (String, NSAccessibilityPriorityLevel) -> Void
    private let coalesceWindow: TimeInterval
    private var pending:
        [EventClass: (work: DispatchWorkItem, text: String, priority: NSAccessibilityPriorityLevel)] = [:]

    /// The seam is `(text, priority)` rather than an event because the
    /// coalescer holds a RENDERED line: the window's winner is decided
    /// after the render, and the loser is dropped without being spoken.
    /// The default posts through the poster layer — never the string
    /// primitive, which belongs to `AnnouncementPosting.swift` alone
    /// (`A11yResidueCensusTests.testNoInteractionSiteCallsTheStringPrimitiveDirectly`).
    init(
        verbosity: CanvasVerbosity = .standard,
        coalesceWindow: TimeInterval = 0.2,
        post: @escaping (String, NSAccessibilityPriorityLevel) -> Void = { text, priority in
            let level: AnnouncementPriority = priority == .high ? .high : .medium
            AppKitAnnouncementPoster().post(text, priority: level)
        }
    ) {
        self.verbosity = verbosity
        self.coalesceWindow = coalesceWindow
        self.post = post
    }

    /// The only announcement API canvas code may use.
    func announce(_ event: CanvasA11yEvent) {
        emit(.canvas(event: event), coalescing: Self.coalescingClass(of: event))
    }

    /// Relay a core event that is NOT canvas vocabulary — the shared
    /// data grid raises its own sort/filter events, and the canvas
    /// table must pass their rendered text AND priority through rather
    /// than re-wrapping them at a priority of its own choosing.
    func relay(_ event: A11yEvent) {
        emit(event, coalescing: nil)
    }

    private func emit(_ event: A11yEvent, coalescing eventClass: EventClass?) {
        let rendered = a11yRender(event: event)
        guard !rendered.text.isEmpty else { return }
        let priority = AnnouncementPriority(rendered.priority)
        if let eventClass {
            debounce(eventClass, text: rendered.text, priority: priority.nsPriority)
            return
        }
        // §1.5: an assertive event supersedes queued navigation
        // context — re-derivable by moving again — so both pending
        // classes are cancelled and DROPPED, never posted.
        if case .high = priority { flushAllPending() }
        post(rendered.text, priority.nsPriority)
    }

    /// The coalescing class of a canvas event, per the list pinned on
    /// the canvas family core-side (contracts doc 0a-8): `navigation`
    /// for movement and transient geometry, `filter` for the two
    /// filter events, immediate for everything else.
    private static func coalescingClass(of event: CanvasA11yEvent) -> EventClass? {
        switch event {
        case .canvasMovedTo, .canvasGroupEntered, .canvasGroupLeft,
            .canvasConnectionTraversed, .canvasMoveRelative, .canvasResizeGeometry:
            return .navigation
        case .canvasFilterCount, .canvasFilterCleared:
            return .filter
        default:
            return nil
        }
    }

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
