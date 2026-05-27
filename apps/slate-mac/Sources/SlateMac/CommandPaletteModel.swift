// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Foundation

/// Testable view-model for the command palette (Milestone Q #315).
///
/// SwiftUI `@State` is tied to a hosted view's lifetime — instance
/// methods on the View struct that mutate `@State` are awkward to
/// test in isolation. Extracting the filter + selection logic into
/// an `ObservableObject` here lets `CommandPaletteViewTests` drive
/// arrow-key navigation, Enter dispatch, and ActionFailed handling
/// against pure model state without spinning up an
/// `NSHostingController` per test.
///
/// `CommandPaletteView` owns a `@StateObject` of this type and
/// binds the relevant `@Published` properties.
@MainActor
final class CommandPaletteModel: ObservableObject {

    /// Snapshot of the registry's commands. Set once in
    /// `loadCommands` (matches the view's `.onAppear` lifetime).
    @Published private(set) var commands: [Command] = []

    /// User's typed query. Bound two-way to the search field.
    @Published var query: String = ""

    /// Currently selected command id. Mutated by arrow keys, hover,
    /// and the on-query-change snap-to-first-result rule.
    @Published var selectedID: String? = nil

    /// Last announcement string the model wants posted via
    /// `postAccessibilityAnnouncement`. Stored separately from the
    /// view's own posting site so tests can assert on it without a
    /// running NSApp.
    @Published private(set) var pendingAnnouncement: String?

    /// Initial-load entry point — called from the view's
    /// `.onAppear`. Idempotent; calling twice with the same
    /// command list is a no-op except for resetting `selectedID`
    /// to the first row.
    func loadCommands(_ snapshot: [Command]) {
        commands = snapshot
        selectedID = filteredCommands.first?.id
    }

    /// Re-run the selection-snap rule when `query` changes. The
    /// view binds this to `.onChange(of: query)`.
    func handleQueryChange() {
        let f = filteredCommands
        if selectedID == nil || !f.contains(where: { $0.id == selectedID }) {
            selectedID = f.first?.id
        }
    }

    /// Filtered, ranked command list. Empty query keeps the
    /// registry's deterministic `(section, id)` order; non-empty
    /// query sorts by descending fuzzy score with id as a stable
    /// tiebreaker.
    var filteredCommands: [Command] {
        guard !query.isEmpty else { return commands }
        return commands
            .compactMap { command -> (Command, Int)? in
                let labelScore = Self.fuzzyScore(query: query, target: command.label)
                let hintScore = command.accessibilityHint
                    .flatMap { Self.fuzzyScore(query: query, target: $0) }
                let scores = [labelScore, hintScore].compactMap { $0 }
                guard let best = scores.max() else { return nil }
                return (command, best)
            }
            .sorted { lhs, rhs in
                if lhs.1 != rhs.1 { return lhs.1 > rhs.1 }
                return lhs.0.id < rhs.0.id
            }
            .map { $0.0 }
    }

    /// Move selection to the next filtered result, wrapping at
    /// the end. No-op when the filter is empty.
    func selectNext() {
        let filtered = filteredCommands
        guard !filtered.isEmpty else { return }
        let idx = filtered.firstIndex { $0.id == selectedID } ?? -1
        let next = (idx + 1) % filtered.count
        selectedID = filtered[next].id
    }

    /// Move selection to the previous filtered result, wrapping
    /// at the start. No-op when the filter is empty.
    func selectPrevious() {
        let filtered = filteredCommands
        guard !filtered.isEmpty else { return }
        let idx = filtered.firstIndex { $0.id == selectedID } ?? filtered.count
        let prev = (idx - 1 + filtered.count) % filtered.count
        selectedID = filtered[prev].id
    }

    /// Invoke the command currently selected via the supplied
    /// registry. Returns the side-effect outcome so the view can
    /// decide whether to dismiss.
    ///
    /// - `success`: action ran cleanly → caller dismisses palette.
    /// - `actionFailed(label, message)`: announce, stay open.
    /// - `unknownId(id)`: announce, stay open.
    /// - `noSelection`: no-op.
    @discardableResult
    func invokeSelected(via registry: CommandRegistry) -> InvocationOutcome {
        guard let id = selectedID,
              let command = filteredCommands.first(where: { $0.id == id })
        else {
            return .noSelection
        }
        return invoke(command, via: registry)
    }

    /// Invoke an explicit command. Used by row-tap callers; the
    /// outcome shape lets the view stay open on error per the
    /// #315 spec ("On invoke error, palette stays open and
    /// surfaces an assertive announcement").
    @discardableResult
    func invoke(_ command: Command, via registry: CommandRegistry) -> InvocationOutcome {
        do {
            try registry.invokeById(id: command.id)
            return .success
        } catch let CommandError.ActionFailed(message) {
            let announcement = "\(command.label) failed: \(message)"
            pendingAnnouncement = announcement
            return .actionFailed(label: command.label, message: message)
        } catch let CommandError.UnknownId(id) {
            let announcement = "Command not found: \(id)"
            pendingAnnouncement = announcement
            return .unknownId(id: id)
        } catch {
            // CommandError is the only declared throwing type from
            // the registry; this branch is defensive for future
            // additions to the FFI error surface.
            let announcement = "\(command.label) failed."
            pendingAnnouncement = announcement
            return .actionFailed(label: command.label, message: "")
        }
    }

    /// Reset the pending-announcement after the view has posted
    /// it (tests don't post; they read and clear).
    func clearPendingAnnouncement() {
        pendingAnnouncement = nil
    }

    // MARK: - Fuzzy matcher

    /// Subsequence-with-boost matcher. Returns `nil` if the query
    /// doesn't subsequence-match the target, otherwise an integer
    /// score (higher = better).
    ///
    /// Scoring:
    /// - +10 per matched character
    /// - +5 if the match lands at a word boundary (start of string
    ///   or after space / `.` / `-` / `:` / `_`)
    /// - +3 if the match is consecutive with the previous match
    /// - +50 if the query is a strict (case-insensitive) prefix
    ///   of the target
    ///
    /// All comparisons are case-insensitive.
    ///
    /// `nonisolated` because the function is pure — no actor state
    /// touched. Lets non-main-actor test contexts call it directly
    /// without `await`.
    nonisolated static func fuzzyScore(query: String, target: String) -> Int? {
        let q = Array(query.lowercased())
        let t = Array(target.lowercased())
        guard !q.isEmpty else { return 0 }

        var qi = 0
        var consecutive = 0
        var score = 0

        for (ti, ch) in t.enumerated() {
            guard qi < q.count else { break }
            if ch == q[qi] {
                score += 10
                let prev = ti > 0 ? t[ti - 1] : Character(" ")
                if ti == 0 || Self.wordBoundary.contains(prev) {
                    score += 5
                }
                if consecutive > 0 {
                    score += 3
                }
                consecutive += 1
                qi += 1
            } else {
                consecutive = 0
            }
        }

        guard qi == q.count else { return nil }
        if t.starts(with: q) {
            score += 50
        }
        return score
    }

    /// Word-boundary characters considered for the +5 bonus.
    /// Includes the punctuation a command label might reasonably
    /// use (`Slate: Save…` future plugin labels, snake_case ids
    /// scraped from `accessibilityHint`).
    private nonisolated static let wordBoundary: Set<Character> = [" ", ".", "-", ":", "_"]
}

/// Outcome of a single `invoke` call. The view branches on this:
/// `success` dismisses the palette; all other variants keep it
/// open while the announcement plays.
enum InvocationOutcome: Equatable {
    case success
    case actionFailed(label: String, message: String)
    case unknownId(id: String)
    case noSelection
}
