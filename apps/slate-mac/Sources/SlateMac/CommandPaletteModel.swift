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
    /// running NSApp. Used for command-invocation feedback
    /// (ActionFailed / UnknownId).
    @Published private(set) var pendingAnnouncement: String?

    /// Filter-change announcement string (#316). The view watches
    /// this and posts assertively whenever the user's typing
    /// changes the result count. Separate from `pendingAnnouncement`
    /// so per-keystroke filter feedback doesn't collide with one-
    /// shot invocation outcomes.
    @Published private(set) var filterAnnouncement: String?

    /// Recent command ids in most-recent-first order. Set on
    /// `loadCommands` from `AppState.commandPaletteRecents`. The
    /// model uses these to populate the Recent section when query
    /// is empty (#316).
    @Published private(set) var recentIDs: [String] = []

    /// Initial-load entry point — called from the view's
    /// `.onAppear`. Idempotent; calling twice resets `selectedID`
    /// to the first row of the new display order.
    func loadCommands(_ snapshot: [Command], recents: [String] = []) {
        commands = snapshot
        recentIDs = recents
        selectedID = displayOrder.first?.id
    }

    /// Re-run the selection-snap rule when `query` changes, and
    /// refresh the filter-change announcement. The view binds this
    /// to `.onChange(of: query)`.
    func handleQueryChange() {
        let order = displayOrder
        if selectedID == nil || !order.contains(where: { $0.id == selectedID }) {
            selectedID = order.first?.id
        }

        // Don't announce on initial open (empty query → palette
        // just rendered the full list, which the user can see).
        // Announce on every non-empty filter change.
        if query.isEmpty {
            filterAnnouncement = nil
        } else {
            let count = filteredCommands.count
            filterAnnouncement = count == 0
                ? "No commands match \"\(query)\""
                : "\(count) command\(count == 1 ? "" : "s") matching \"\(query)\""
        }
    }

    /// Clear the filter-change announcement after the view has
    /// posted it.
    func clearFilterAnnouncement() {
        filterAnnouncement = nil
    }

    // MARK: - Section grouping (#316)

    /// Renderable grouping of the filtered commands. Drives the
    /// SwiftUI `Section` hierarchy in `CommandPaletteView`.
    ///
    /// **Empty query**: Recent section at top (most-recent-first,
    /// commands also in their native section are EXCLUDED from
    /// the native sections to avoid duplicate rows), followed by
    /// the CommandSection enums in declared order.
    ///
    /// **Non-empty query**: fuzzy-filter results grouped by their
    /// native section in declared order; no Recent section
    /// (filter results are what the user asked for, not history).
    var sections: [PaletteSection] {
        let filtered = filteredCommands

        if query.isEmpty {
            var result: [PaletteSection] = []

            // Recent section first — preserves invocation order.
            // Only includes recents that still exist in the
            // registry (a command id may have been removed across
            // app updates; we skip gracefully).
            let recentSet = Set(recentIDs)
            let recentCommands = recentIDs.compactMap { id in
                commands.first(where: { $0.id == id })
            }
            if !recentCommands.isEmpty {
                result.append(PaletteSection(
                    title: "Recent",
                    kind: nil,
                    commands: recentCommands
                ))
            }

            // Native sections — exclude commands already shown in
            // Recent to keep the flat displayOrder de-duped.
            let nativeOnly = filtered.filter { !recentSet.contains($0.id) }
            let byType = Dictionary(grouping: nativeOnly, by: { $0.section })
            for sec in Self.sectionOrder {
                if let cmds = byType[sec], !cmds.isEmpty {
                    result.append(PaletteSection(
                        title: Self.title(for: sec),
                        kind: sec,
                        commands: cmds
                    ))
                }
            }
            return result
        } else {
            // With a query: group fuzzy-matched commands by their
            // native section, in declared order. No Recent.
            let byType = Dictionary(grouping: filtered, by: { $0.section })
            return Self.sectionOrder.compactMap { sec -> PaletteSection? in
                guard let cmds = byType[sec] else { return nil }
                return PaletteSection(
                    title: Self.title(for: sec),
                    kind: sec,
                    commands: cmds
                )
            }
        }
    }

    /// Flat list of commands in display (section-flattened) order.
    /// Feeds arrow-nav so the visual flow and selection cycle
    /// match exactly.
    var displayOrder: [Command] {
        sections.flatMap { $0.commands }
    }

    /// Declared section order for the palette. Tracks
    /// `CommandSection`'s `repr(u8)` order on the Rust side, but
    /// stays explicit here so a Rust-side reorder doesn't silently
    /// change the palette layout.
    private nonisolated static let sectionOrder: [CommandSection] = [
        .file, .navigation, .view, .vault, .editor, .canvas, .bases, .graph, .sidebar, .tasks,
        .settings, .plugins,
    ]

    /// Human-readable header for a section. Plain en-US strings
    /// in V1; localisation lands in V2 per #264.
    nonisolated static func title(for section: CommandSection) -> String {
        switch section {
        case .file: return "File"
        case .navigation: return "Navigation"
        case .view: return "View"
        case .vault: return "Vault"
        case .editor: return "Editor"
        case .tasks: return "Tasks"
        case .settings: return "Settings"
        case .plugins: return "Plugins"
        case .canvas: return "Canvas"
        case .bases: return "Bases"
        case .graph: return "Graph"
        case .sidebar: return "Sidebar"
        }
    }

    // MARK: - Filtering

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

    /// Move selection to the next visible row, wrapping at the
    /// end. Operates on `displayOrder` (sectioned-and-deduped) so
    /// arrow nav matches what the user sees.
    func selectNext() {
        let order = displayOrder
        guard !order.isEmpty else { return }
        let idx = order.firstIndex { $0.id == selectedID } ?? -1
        let next = (idx + 1) % order.count
        selectedID = order[next].id
    }

    /// Move selection to the previous visible row, wrapping at
    /// the start. Operates on `displayOrder`.
    func selectPrevious() {
        let order = displayOrder
        guard !order.isEmpty else { return }
        let idx = order.firstIndex { $0.id == selectedID } ?? order.count
        let prev = (idx - 1 + order.count) % order.count
        selectedID = order[prev].id
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
              let command = displayOrder.first(where: { $0.id == id })
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
            // Structural busy is an availability rejection, not an operation
            // failure. The row already exposes this exact reason; announce it
            // verbatim so VoiceOver does not hear a misleading second prefix.
            let announcement =
                message == AppState.structuralMutationBusyReason
                ? message
                : "\(command.label) failed: \(message)"
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

/// One renderable section in the palette. `kind == nil` is the
/// synthetic "Recent" section; everything else maps 1:1 to a
/// `CommandSection`.
struct PaletteSection: Identifiable, Equatable {
    let title: String
    let kind: CommandSection?
    let commands: [Command]

    /// Stable identifier independent of the display title — a
    /// future plugin section titled "Recent" can't collide with
    /// the synthetic Recent section, and a localisation pass on
    /// `title` (V2 per #264) doesn't change `id`.
    var id: String {
        if let kind {
            return "kind.\(kind)"
        }
        return "recent"
    }
}
