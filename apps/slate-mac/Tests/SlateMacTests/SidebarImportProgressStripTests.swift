// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation
import XCTest

@testable import SlateMac

@MainActor
final class SidebarImportProgressStripTests: XCTestCase {
    func testAdmissionCapturesFixedDenominatorAndCompletionIsMonotone() {
        let progress = SidebarImportProgressModel(admittedProviderCount: 3)

        XCTAssertEqual(progress.totalProviderCount, 3)
        XCTAssertEqual(progress.completedProviderCount, 0)
        XCTAssertEqual(progress.accessibilityValue, "0 of 3")
        XCTAssertTrue(progress.hasRemainingProviders)
        XCTAssertTrue(progress.canRequestCancellation)
        XCTAssertEqual(progress.phase, .importing)

        progress.recordCompletedProviderCount(1)
        progress.recordCompletedProviderCount(0)
        XCTAssertEqual(progress.completedProviderCount, 1)
        XCTAssertEqual(progress.totalProviderCount, 3)
        XCTAssertEqual(progress.accessibilityValue, "1 of 3")

        progress.recordCompletedProviderCount(9)
        XCTAssertEqual(progress.completedProviderCount, 3)
        XCTAssertEqual(progress.totalProviderCount, 3)
        XCTAssertEqual(progress.accessibilityValue, "3 of 3")
        XCTAssertFalse(progress.hasRemainingProviders)
    }

    func testCancellationGateInvokesCallbackExactlyOnce() {
        var gate = SidebarImportCancellationGate()
        var invocationCount = 0

        for _ in 0..<4 {
            gate.request { invocationCount += 1 }
        }

        XCTAssertTrue(gate.isCancellationRequested)
        XCTAssertEqual(invocationCount, 1)
    }

    func testProgressAndCancellationCopyIsTruthful() {
        XCTAssertEqual(
            SidebarImportProgressStrip.title(
                phase: .importing, hasRemainingProviders: true),
            "Importing…")
        XCTAssertEqual(
            SidebarImportProgressStrip.title(
                phase: .importing, hasRemainingProviders: false),
            "Finishing import…")
        XCTAssertEqual(
            SidebarImportProgressStrip.title(
                phase: .cancelling, hasRemainingProviders: true),
            "Cancelling import…")
        XCTAssertEqual(
            SidebarImportProgressStrip.title(
                phase: .moving, hasRemainingProviders: true),
            "Moving items…")
        XCTAssertEqual(
            SidebarImportProgressStrip.title(
                phase: .finishing, hasRemainingProviders: true),
            "Finishing import…")
        XCTAssertEqual(
            SidebarImportProgressStrip.cancelAccessibilityHint,
            "Stops remaining imports. Completed copies remain in the vault.")
        XCTAssertEqual(
            SidebarImportProgressStrip.cancellationHint(
                phase: .cancelling, available: false),
            "Cancellation requested. Completed copies remain.")
        XCTAssertEqual(
            SidebarImportProgressStrip.cancellationHint(
                phase: .moving, available: false),
            "The in-vault move is already being applied and can’t be cancelled.")
        XCTAssertEqual(
            SidebarImportProgressStrip.noImportInProgressHint,
            "No import is in progress.")

        let copy = SidebarImportProgressStrip.cancelAccessibilityHint.lowercased()
        XCTAssertTrue(copy.contains("completed copies remain"))
        for forbidden in ["rollback", "rolled back", "removed", "undo"] {
            XCTAssertFalse(copy.contains(forbidden))
        }
    }

    func testViewUsesNativeDeterminateProgressAndStandardCancellationCommands()
        throws
    {
        let source = try progressStripSource()
        XCTAssertTrue(source.contains("ProgressView("))
        XCTAssertTrue(source.contains("ProgressView()"))
        XCTAssertTrue(source.contains(".controlSize(.mini)"))
        XCTAssertTrue(source.contains(".accessibilityHidden(true)"))
        XCTAssertTrue(source.contains("total: Double(progress.totalProviderCount)"))
        XCTAssertTrue(source.contains("Button(\"Cancel\")"))
        XCTAssertTrue(source.contains(".keyboardShortcut(.cancelAction)"))
        XCTAssertTrue(source.contains(".onExitCommand"))
        XCTAssertTrue(source.contains("guard progress.canRequestCancellation else { return }"))
        XCTAssertTrue(source.contains("!progress.canRequestCancellation"))
        XCTAssertTrue(source.contains("Tokens.Spacing"))
        XCTAssertTrue(source.contains("Tokens.Typography.caption"))
        XCTAssertTrue(source.contains(".controlSize(.small)"))
        XCTAssertTrue(source.contains(".accessibilityValue(progress.accessibilityValue)"))

        for focusStealer in [
            "NSEvent.addLocalMonitorForEvents", ".focused(", ".focusable(",
            "withAnimation", ".animation(", "KeyboardShortcut(\".\"",
        ] {
            XCTAssertFalse(
                source.contains(focusStealer),
                "progress strip must not add focus interception or custom motion: \(focusStealer)")
        }
    }

    func testFileMenuOwnsFocusIndependentCommandPeriodCancellation() throws {
        let source = try appSource("SlateMacApp.swift")
        guard let start = source.range(of: "Button(\"Cancel Import\")") else {
            return XCTFail("File menu needs a discoverable Cancel Import item")
        }
        let tail = String(source[start.lowerBound...].prefix(1_200))
        XCTAssertTrue(tail.contains("appState.requestImportBatchCancellation()"))
        XCTAssertTrue(tail.contains(".keyboardShortcut(\".\", modifiers: [.command])"))
        XCTAssertTrue(tail.contains("cancelImportDisabledReason != nil"))
        XCTAssertTrue(source.contains("appState.importCancellationDisabledReason"))
        XCTAssertTrue(source.contains("SidebarImportProgressStrip.cancelAccessibilityHint"))
    }

    private func progressStripSource() throws -> String {
        let sourceURL = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Sources/SlateMac/Sidebar/SidebarImportProgressStrip.swift")
        return try String(contentsOf: sourceURL, encoding: .utf8)
    }

    private func appSource(_ relativePath: String) throws -> String {
        let sourceURL = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Sources/SlateMac")
            .appendingPathComponent(relativePath)
        return try String(contentsOf: sourceURL, encoding: .utf8)
    }
}
