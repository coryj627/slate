// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

@MainActor
final class TrashProgressStripTests: XCTestCase {
    func testHostedPreparingCancelPressDisablesRepeatedCancellation() async throws {
        let model = ProgressFixture(phase: .preparing)
        let cancelled = expectation(description: "Cancel Trash invokes its callback")
        let repeated = expectation(description: "Stopping must suppress repeated cancellation")
        repeated.isInverted = true
        let hosted = await host(model: model) {
            model.cancellationCount += 1
            model.progress.phase = .stopping
            if model.cancellationCount == 1 { cancelled.fulfill() }
            else { repeated.fulfill() }
        }
        defer { hosted.window.close() }

        let button = try await cancelButton(in: hosted.view, enabled: true)
        // AXPress is the real hosted control's click action; SwiftUI need not
        // implement its Button as an NSButton in the NSView subtree.
        XCTAssertTrue(button.accessibilityPerformPress())
        await fulfillment(of: [cancelled], timeout: 30)
        XCTAssertEqual(model.cancellationCount, 1)

        let stoppingButton = try await cancelButton(in: hosted.view, enabled: false)
        _ = stoppingButton.accessibilityPerformPress()
        _ = hosted.window.performKeyEquivalent(with: try commandPeriod(in: hosted.window))
        await fulfillment(of: [repeated], timeout: 0.5)
        XCTAssertEqual(model.cancellationCount, 1)
        XCTAssertTrue(hosted.window.firstResponder === hosted.focusProbe)
    }

    func testHostedExecutingCommandPeriodDisablesRepeatedCancellation() async throws {
        let model = ProgressFixture(phase: .executing)
        let cancelled = expectation(description: "Command-period invokes Cancel Trash")
        let repeated = expectation(description: "Stopping must suppress repeated cancellation")
        repeated.isInverted = true
        let hosted = await host(model: model) {
            model.cancellationCount += 1
            model.progress.phase = .stopping
            if model.cancellationCount == 1 { cancelled.fulfill() }
            else { repeated.fulfill() }
        }
        defer { hosted.window.close() }
        _ = try await cancelButton(in: hosted.view, enabled: true)

        XCTAssertTrue(hosted.window.performKeyEquivalent(with: try commandPeriod(in: hosted.window)))
        await fulfillment(of: [cancelled], timeout: 30)
        XCTAssertEqual(model.cancellationCount, 1)

        let stoppingButton = try await cancelButton(in: hosted.view, enabled: false)
        _ = hosted.window.performKeyEquivalent(with: try commandPeriod(in: hosted.window))
        _ = stoppingButton.accessibilityPerformPress()
        await fulfillment(of: [repeated], timeout: 0.5)
        XCTAssertEqual(model.cancellationCount, 1)
        XCTAssertTrue(hosted.window.firstResponder === hosted.focusProbe)
    }

    private final class ProgressFixture: ObservableObject {
        @Published var progress: AppState.TrashProgress
        var cancellationCount = 0

        init(phase: AppState.TrashWorkPhase) {
            progress = AppState.TrashProgress(id: UUID(), phase: phase)
        }
    }

    private struct LiveStrip: View {
        @ObservedObject var model: ProgressFixture
        let onCancel: () -> Void

        var body: some View {
            TrashProgressStrip(progress: model.progress, onCancel: onCancel)
        }
    }

    private func host(
        model: ProgressFixture,
        onCancel: @escaping () -> Void
    ) async -> (view: NSHostingView<AnyView>, window: NSWindow, focusProbe: FocusProbeView) {
        // Each --parallel XCTest worker must initialize AppKit independently.
        _ = NSApplication.shared
        let appeared = expectation(description: "hosted Trash strip appeared")
        let view = NSHostingView(rootView: AnyView(
            LiveStrip(model: model, onCancel: onCancel)
                .onAppear { appeared.fulfill() }))
        view.frame = NSRect(x: 0, y: 0, width: 440, height: 96)
        let content = NSView(frame: NSRect(x: 0, y: 0, width: 540, height: 96))
        let focusProbe = FocusProbeView(frame: NSRect(x: 460, y: 32, width: 60, height: 24))
        content.addSubview(view)
        content.addSubview(focusProbe)
        let window = NSWindow(
            contentRect: content.frame,
            styleMask: [.titled],
            backing: .buffered,
            defer: false)
        window.isReleasedWhenClosed = false
        window.contentView = content
        window.makeKeyAndOrderFront(nil)
        XCTAssertTrue(window.makeFirstResponder(focusProbe))
        await fulfillment(of: [appeared], timeout: 30)
        view.layoutSubtreeIfNeeded()
        return (view, window, focusProbe)
    }

    private func cancelButton(in view: NSView, enabled: Bool) async throws -> any NSAccessibilityProtocol {
        // SwiftUI publishes its accessibility tree on a later rendering pass.
        // Wait for the actual control state, not a fixed render delay.
        let deadline = ContinuousClock.now.advanced(by: .seconds(30))
        repeat {
            view.layoutSubtreeIfNeeded()
            var visited = Set<ObjectIdentifier>()
            if let button = firstAccessibilityButton(in: view, visited: &visited),
                button.isAccessibilityEnabled() == enabled {
                let names = [button.accessibilityLabel(), button.accessibilityTitle()].compactMap { $0 }
                XCTAssertTrue(names.contains(String(localized: "Cancel Trash")))
                return button
            }
            try await Task.sleep(for: .milliseconds(20))
        } while ContinuousClock.now < deadline
        XCTFail("Hosted Cancel Trash never became \(enabled ? "enabled" : "disabled")")
        throw HostedControlError.buttonNotReady
    }

    private func firstAccessibilityButton(
        in root: Any,
        visited: inout Set<ObjectIdentifier>
    ) -> (any NSAccessibilityProtocol)? {
        guard let element = root as? any NSAccessibilityProtocol,
            visited.insert(ObjectIdentifier(element)).inserted else { return nil }
        if element.accessibilityRole() == .button { return element }
        for child in element.accessibilityChildren() ?? [] {
            if let button = firstAccessibilityButton(in: child, visited: &visited) { return button }
        }
        return nil
    }

    private func commandPeriod(in window: NSWindow) throws -> NSEvent {
        try XCTUnwrap(NSEvent.keyEvent(
            with: .keyDown,
            location: .zero,
            modifierFlags: [.command],
            timestamp: ProcessInfo.processInfo.systemUptime,
            windowNumber: window.windowNumber,
            context: nil,
            characters: ".",
            charactersIgnoringModifiers: ".",
            isARepeat: false,
            keyCode: 47))
    }

    private final class FocusProbeView: NSView {
        override var acceptsFirstResponder: Bool { true }
    }

    private enum HostedControlError: Error { case buttonNotReady }
}
