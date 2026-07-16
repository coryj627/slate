// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation
import XCTest

@testable import SlateMac

/// Pins the view-layer availability contract for keyboard-selected commands.
/// The registry guard remains independently covered because non-palette callers
/// can still reach it while a structural operation owns the mutation gate.
@MainActor
final class CommandPaletteBusyAnnouncementTests: XCTestCase {
    func testArrowSelectionAnnouncementIncludesExactUnavailableReason() {
        let command = structuralCommand()
        let reason = AppState.structuralMutationBusyReason

        XCTAssertEqual(
            CommandPaletteView.disabledReason(
                for: command, structuralMutationDisabledReason: reason),
            reason)
        XCTAssertEqual(
            CommandPaletteView.selectionAnnouncement(
                for: command, disabledReason: reason),
            "Selected: New Note. Unavailable: \(reason)")
    }

    func testReturnOnUnavailableSelectionAnnouncesWithoutInvokingAndRetainsSearchFocus() {
        let reason = AppState.structuralMutationBusyReason
        var focusEdges = 0
        var announcements: [String] = []
        var registryInvocations = 0

        let outcome = CommandPaletteView.invokeIfAvailable(
            disabledReason: reason,
            restoreSearchFocus: { focusEdges += 1 },
            announceUnavailable: { announcements.append($0) },
            invoke: {
                registryInvocations += 1
                return .success
            })

        XCTAssertNil(outcome)
        XCTAssertEqual(registryInvocations, 0, "Return must not reach the registry action")
        XCTAssertEqual(announcements, [reason], "VoiceOver hears the exact busy reason")
        XCTAssertEqual(focusEdges, 1, "the search field remains the keyboard owner")
    }

    func testEnabledSelectionStillInvokesNormallyAndRetainsSearchFocus() {
        var focusEdges = 0
        var announcements: [String] = []
        var registryInvocations = 0

        let outcome = CommandPaletteView.invokeIfAvailable(
            disabledReason: nil,
            restoreSearchFocus: { focusEdges += 1 },
            announceUnavailable: { announcements.append($0) },
            invoke: {
                registryInvocations += 1
                return .success
            })

        XCTAssertEqual(outcome, .success)
        XCTAssertEqual(registryInvocations, 1)
        XCTAssertTrue(announcements.isEmpty)
        XCTAssertEqual(focusEdges, 1)
    }

    func testBusyReasonAppliesOnlyToStructuralCommands() {
        let reason = AppState.structuralMutationBusyReason
        let nonstructural = Command(
            id: "test.find", label: "Find", accessibilityHint: nil,
            hotkeyHint: "⌘F", section: .editor)

        XCTAssertNil(
            CommandPaletteView.disabledReason(
                for: nonstructural, structuralMutationDisabledReason: reason))
        XCTAssertNil(
            CommandPaletteView.disabledReason(
                for: structuralCommand(), structuralMutationDisabledReason: nil))
    }

    func testBaseCommandsExposeTheSameAvailabilityReasonBeforeInvocation() {
        let interactionReason = AppState.baseDocumentReopeningDisabledReason
        XCTAssertEqual(
            SlateCommandID.baseInteractionCommands,
            [
                SlateCommandID.basesNextView,
                SlateCommandID.basesPreviousView,
                SlateCommandID.basesSortByColumn,
                SlateCommandID.basesSaveSortToView,
                SlateCommandID.basesQuickFilter,
            ])
        XCTAssertEqual(
            SlateCommandID.baseDefinitionEditingCommands,
            [SlateCommandID.basesEditViewFilters])
        for id in SlateCommandID.baseInteractionCommands {
            XCTAssertEqual(
                CommandPaletteView.disabledReason(
                    for: command(id: id),
                    structuralMutationDisabledReason: nil,
                    baseInteractionDisabledReason: interactionReason),
                interactionReason,
                "\(id) must be visibly unavailable with the shared Base reason")
        }

        let refreshReason = AppState.baseDocumentReopeningDisabledReason
        XCTAssertEqual(
            CommandPaletteView.disabledReason(
                for: command(id: SlateCommandID.basesRefresh),
                structuralMutationDisabledReason: nil,
                baseInteractionDisabledReason: interactionReason,
                baseRefreshDisabledReason: refreshReason),
            refreshReason)
        XCTAssertEqual(
            CommandPaletteView.disabledReason(
                for: command(id: SlateCommandID.basesEditViewFilters),
                structuralMutationDisabledReason: nil,
                baseInteractionDisabledReason: interactionReason,
                baseDefinitionEditingDisabledReason: interactionReason),
            interactionReason)
        XCTAssertNil(
            CommandPaletteView.disabledReason(
                for: command(id: SlateCommandID.basesEditViewFilters),
                structuralMutationDisabledReason: nil,
                baseInteractionDisabledReason: interactionReason,
                baseDefinitionEditingDisabledReason: nil),
            "saved-query Edit Filters must remain available")
        XCTAssertNil(
            CommandPaletteView.disabledReason(
                for: command(id: SlateCommandID.basesResultsPopover),
                structuralMutationDisabledReason: nil,
                baseInteractionDisabledReason: interactionReason,
                baseRefreshDisabledReason: refreshReason),
            "read-only Base inspection must remain available")
    }

    func testRegistryStillGuardsExternalStructuralCallers() {
        let model = CommandPaletteModel()
        let registry = CommandRegistry()
        let reason = AppState.structuralMutationBusyReason
        let action = InvocationProbe(failure: .ActionFailed(message: reason))
        let command = structuralCommand()
        _ = registry.register(command: command, action: action)
        model.loadCommands(registry.list())

        let outcome = model.invokeSelected(via: registry)

        XCTAssertEqual(
            outcome, .actionFailed(label: command.label, message: reason))
        XCTAssertEqual(model.pendingAnnouncement, reason)
        XCTAssertEqual(action.invocationCount, 1)
    }

    private func structuralCommand() -> Command {
        Command(
            id: SlateCommandID.newNote,
            label: "New Note",
            accessibilityHint: "Create an untitled note.",
            hotkeyHint: "⌘N",
            section: .file)
    }

    private func command(id: String) -> Command {
        Command(
            id: id,
            label: id,
            accessibilityHint: "Test Base command.",
            hotkeyHint: nil,
            section: .bases)
    }

    private final class InvocationProbe: CommandAction, @unchecked Sendable {
        private let lock = NSLock()
        private var count = 0
        private let failure: CommandError?

        init(failure: CommandError? = nil) {
            self.failure = failure
        }

        var invocationCount: Int {
            lock.lock()
            defer { lock.unlock() }
            return count
        }

        func invoke() throws {
            lock.lock()
            count += 1
            lock.unlock()
            if let failure { throw failure }
        }
    }
}
