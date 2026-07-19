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

    // MARK: - Section grouping (#316; core-owned since W0.5-1 #717)

    /// Renderable grouping of the filtered commands, computed by
    /// `slate-core`'s palette module through the FFI. Ranking, section
    /// layout and titles, Recent blending, and the within-Sidebar
    /// catalog placement all live core-side so the mac and Windows
    /// palettes render identically from identical inputs — this model
    /// only forwards its snapshot state.
    var sections: [PaletteSection] {
        paletteSections(
            commands: commands,
            query: query,
            recentIds: recentIDs,
            sidebarPinnedOrder: Self.sidebarPinnedOrder
        )
    }

    /// Flat list of commands in display (section-flattened) order.
    /// Feeds arrow-nav so the visual flow and selection cycle
    /// match exactly.
    var displayOrder: [Command] {
        sections.flatMap { $0.commands }
    }

    /// The sidebar catalog's task-oriented id order, handed to core's
    /// layout as data. The catalog itself — capability gating, undo
    /// behavior, invocation — stays a host concern; core only places
    /// the ids it is given.
    private static var sidebarPinnedOrder: [String] {
        SidebarActionCatalog.actions.map(\.id)
    }

    // MARK: - Filtering

    /// Commands matching the current query in global ranked order —
    /// descending score with id as the stable tiebreaker, the same
    /// contract as before #717. The score is core-computed and carried
    /// on each row; the host only orders by it (display stays the
    /// section-grouped `sections`). Empty query returns the snapshot
    /// unchanged. Feeds the filter-count announcement.
    var filteredCommands: [Command] {
        guard !query.isEmpty else { return commands }
        return sections
            .flatMap { $0.rows }
            .sorted { lhs, rhs in
                if lhs.score != rhs.score { return lhs.score > rhs.score }
                return lhs.command.id < rhs.command.id
            }
            .map(\.command)
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

/// Display affordances for the core-computed `PaletteSection` record
/// (`slate_core::palette` via the FFI; W0.5-1 #717). `kind == nil` is
/// the synthetic "Recent" section; everything else maps 1:1 to a
/// `CommandSection`.
extension PaletteSection: Identifiable {
    /// Stable identifier independent of the display title — a
    /// future plugin section titled "Recent" can't collide with
    /// the synthetic Recent section, and a localisation pass on
    /// `title` (V2 per #264) doesn't change `id`.
    public var id: String {
        if let kind {
            return "kind.\(kind)"
        }
        return "recent"
    }

    /// Flat command list for rendering. `rows` additionally carry the
    /// matched label byte ranges for per-platform bolding; the mac view
    /// doesn't render bolding yet, so it reads the plain commands.
    var commands: [Command] {
        rows.map(\.command)
    }
}
