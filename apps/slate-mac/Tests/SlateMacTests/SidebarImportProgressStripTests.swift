// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
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

    func testHostedStripExposesLiveProgressAndSingleDeliveryCancel() throws {
        let progress = SidebarImportProgressModel(
            admittedProviderCount: 3,
            completedProviderCount: 1)
        var cancellationCount = 0
        let hosted = host(
            SidebarImportProgressStrip(
                progress: progress,
                onCancel: { cancellationCount += 1 }))
        defer { hosted.window.close() }
        XCTAssertTrue(hosted.window.firstResponder === hosted.focusProbe)

        let progressIndicator = try XCTUnwrap(
            firstSubview(
                of: NSProgressIndicator.self,
                identifiedBy: "sidebar.import.progress",
                in: hosted.host))
        XCTAssertEqual(progressIndicator.style, .bar)
        XCTAssertFalse(progressIndicator.isIndeterminate)
        XCTAssertEqual(progressIndicator.minValue, 0)
        XCTAssertEqual(progressIndicator.maxValue, 3)
        XCTAssertEqual(progressIndicator.doubleValue, 1)
        XCTAssertEqual(progressIndicator.accessibilityRole(), .progressIndicator)
        XCTAssertEqual(progressIndicator.accessibilityLabel(), "Import progress")
        XCTAssertEqual(progressIndicator.accessibilityValueDescription(), "1 of 3")

        let cancelButton = try XCTUnwrap(
            firstSubview(
                of: NSButton.self,
                identifiedBy: "sidebar.import.cancel",
                in: hosted.host))
        XCTAssertEqual(cancelButton.title, "Cancel")
        XCTAssertEqual(cancelButton.accessibilityRole(), .button)
        XCTAssertEqual(cancelButton.accessibilityLabel(), "Cancel")
        XCTAssertEqual(
            cancelButton.accessibilityHelp(),
            SidebarImportProgressStrip.cancelAccessibilityHint)
        XCTAssertEqual(
            cancelButton.toolTip,
            SidebarImportProgressStrip.cancelAccessibilityHint)
        XCTAssertEqual(cancelButton.keyEquivalent, "\u{1b}")
        XCTAssertTrue(cancelButton.keyEquivalentModifierMask.isEmpty)
        XCTAssertTrue(cancelButton.isEnabled)
        XCTAssertGreaterThan(progressIndicator.frame.width, 0)
        XCTAssertGreaterThan(cancelButton.frame.width, 0)
        XCTAssertGreaterThanOrEqual(cancelButton.frame.height, 20)
        XCTAssertLessThan(progressIndicator.frame.maxX, cancelButton.frame.minX)

        let escapeEvent = try XCTUnwrap(NSEvent.keyEvent(
            with: .keyDown,
            location: .zero,
            modifierFlags: [],
            timestamp: ProcessInfo.processInfo.systemUptime,
            windowNumber: hosted.window.windowNumber,
            context: nil,
            characters: "\u{1b}",
            charactersIgnoringModifiers: "\u{1b}",
            isARepeat: false,
            keyCode: 53))
        XCTAssertTrue(cancelButton.performKeyEquivalent(with: escapeEvent))
        pumpRunLoop()
        XCTAssertEqual(cancellationCount, 1)
        XCTAssertTrue(hosted.window.firstResponder === hosted.focusProbe)

        let disabledCancelButton = try XCTUnwrap(
            firstSubview(
                of: NSButton.self,
                identifiedBy: "sidebar.import.cancel",
                in: hosted.host))
        XCTAssertFalse(disabledCancelButton.isEnabled)
        disabledCancelButton.performClick(nil)
        XCTAssertEqual(cancellationCount, 1)

        progress.recordCompletedProviderCount(2)
        progress.setPhase(.moving)
        progress.setCancellationAvailability(false)
        pumpRunLoop()
        XCTAssertTrue(hosted.window.firstResponder === hosted.focusProbe)

        let updatedProgress = try XCTUnwrap(
            firstSubview(
                of: NSProgressIndicator.self,
                identifiedBy: "sidebar.import.progress",
                in: hosted.host))
        XCTAssertEqual(updatedProgress.maxValue, 3)
        XCTAssertEqual(updatedProgress.doubleValue, 2)
        XCTAssertEqual(updatedProgress.accessibilityValueDescription(), "2 of 3")
        let updatedCancelButton = try XCTUnwrap(
            firstSubview(
                of: NSButton.self,
                identifiedBy: "sidebar.import.cancel",
                in: hosted.host))
        XCTAssertFalse(updatedCancelButton.isEnabled)
        XCTAssertEqual(
            updatedCancelButton.accessibilityHelp(),
            SidebarImportProgressStrip.cancellationHint(
                phase: .moving,
                available: false))
    }

    private func host<V: View>(
        _ view: V
    ) -> (host: NSHostingView<V>, window: NSWindow, focusProbe: FocusProbeView) {
        let frame = NSRect(x: 0, y: 0, width: 440, height: 96)
        let host = NSHostingView(rootView: view)
        host.frame = NSRect(x: 0, y: 0, width: 320, height: 96)
        let focusProbe = FocusProbeView(frame: NSRect(x: 340, y: 32, width: 80, height: 24))
        let contentView = NSView(frame: frame)
        contentView.addSubview(host)
        contentView.addSubview(focusProbe)
        let window = NSWindow(
            contentRect: frame,
            styleMask: [.titled],
            backing: .buffered,
            defer: false)
        window.isReleasedWhenClosed = false
        window.contentView = contentView
        window.makeKeyAndOrderFront(nil)
        window.makeFirstResponder(focusProbe)
        host.layoutSubtreeIfNeeded()
        pumpRunLoop()
        return (host, window, focusProbe)
    }

    private func pumpRunLoop() {
        RunLoop.main.run(until: Date(timeIntervalSinceNow: 0.1))
    }

    private func firstSubview<T: NSView>(
        of type: T.Type,
        identifiedBy identifier: String,
        in root: NSView
    ) -> T? {
        if let match = root as? T,
            match.accessibilityIdentifier() == identifier
        {
            return match
        }
        for child in root.subviews {
            if let match = firstSubview(
                of: type,
                identifiedBy: identifier,
                in: child)
            {
                return match
            }
        }
        return nil
    }

    private final class FocusProbeView: NSView {
        override var acceptsFirstResponder: Bool { true }
    }
}
