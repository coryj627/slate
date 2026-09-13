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
        // Standalone expectations are only registered when awaited below. A
        // discovery error must not also produce unwaited-expectation failures.
        let cancelled = XCTestExpectation(description: "Cancel Trash invokes its callback")
        let repeated = XCTestExpectation(description: "Stopping must suppress repeated cancellation")
        repeated.isInverted = true
        let hosted = await host(model: model) {
            model.cancellationCount += 1
            model.progress.phase = .stopping
            if model.cancellationCount == 1 { cancelled.fulfill() }
            else { repeated.fulfill() }
        }
        defer { hosted.window.close() }

        let button = try await cancelButton(in: hosted.window, enabled: true)
        // AXPress is the real hosted control's click action; SwiftUI need not
        // implement its Button as an NSButton in the NSView subtree.
        XCTAssertTrue(button.accessibilityPerformPress())
        await fulfillment(of: [cancelled], timeout: 30)
        XCTAssertEqual(model.cancellationCount, 1)

        let stoppingButton = try await cancelButton(in: hosted.window, enabled: false)
        _ = stoppingButton.accessibilityPerformPress()
        _ = hosted.window.performKeyEquivalent(with: try commandPeriod(in: hosted.window))
        await fulfillment(of: [repeated], timeout: 0.5)
        XCTAssertEqual(model.cancellationCount, 1)
        XCTAssertTrue(hosted.window.firstResponder === hosted.focusProbe)
    }

    func testHostedExecutingCommandPeriodDisablesRepeatedCancellation() async throws {
        let model = ProgressFixture(phase: .executing)
        let cancelled = XCTestExpectation(description: "Command-period invokes Cancel Trash")
        let repeated = XCTestExpectation(description: "Stopping must suppress repeated cancellation")
        repeated.isInverted = true
        let hosted = await host(model: model) {
            model.cancellationCount += 1
            model.progress.phase = .stopping
            if model.cancellationCount == 1 { cancelled.fulfill() }
            else { repeated.fulfill() }
        }
        defer { hosted.window.close() }
        _ = try await cancelButton(in: hosted.window, enabled: true)

        XCTAssertTrue(hosted.window.performKeyEquivalent(with: try commandPeriod(in: hosted.window)))
        await fulfillment(of: [cancelled], timeout: 30)
        XCTAssertEqual(model.cancellationCount, 1)

        let stoppingButton = try await cancelButton(in: hosted.window, enabled: false)
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

    private func cancelButton(in window: NSWindow, enabled: Bool) async throws -> AccessibilityButton {
        // SwiftUI publishes its accessibility tree on a later rendering pass.
        // Wait for the actual control state, not a fixed render delay.
        let deadline = ContinuousClock.now.advanced(by: .seconds(30))
        var snapshot: [String] = []
        repeat {
            window.contentView?.layoutSubtreeIfNeeded()
            var visited = Set<ObjectIdentifier>()
            snapshot = []
            if let button = findCancelButton(
                in: window, enabled: enabled, visited: &visited, snapshot: &snapshot) {
                return button
            }
            try await Task.sleep(for: .milliseconds(20))
        } while ContinuousClock.now < deadline
        throw HostedControlError(description:
            "Hosted Cancel Trash never became \(enabled ? "enabled" : "disabled"); "
            + "window visible=\(window.isVisible), key=\(window.isKeyWindow), "
            + "content frame=\(String(describing: window.contentView?.frame)). "
            + "Accessibility/view snapshot (at most 20 nodes):\n"
            + snapshot.joined(separator: "\n"))
    }

    private func findCancelButton(
        in root: Any,
        enabled: Bool,
        visited: inout Set<ObjectIdentifier>,
        snapshot: inout [String]
    ) -> AccessibilityButton? {
        let element = root as AnyObject
        guard visited.count < 200,
            visited.insert(ObjectIdentifier(element)).inserted else { return nil }

        // AppKit explicitly supports accessibility methods without formal
        // protocol conformance. SwiftUI navigation children only promise the
        // minimal element protocol, so a full-protocol cast cannot gate a walk.
        // These optional Objective-C calls dispatch public accessibility APIs.
        let role = element.accessibilityRole?()
        let names = [element.accessibilityLabel?(), element.accessibilityTitle?()].compactMap { $0 }
        let isEnabled = element.isAccessibilityEnabled?()
        let children = accessibilityArray(
            from: element, selector: #selector(NSAccessibilityProtocol.accessibilityChildren))
        let navigationChildren = accessibilityArray(
            from: element, selector: #selector(NSAccessibilityProtocol.accessibilityChildrenInNavigationOrder))
        let subviews: [NSView]
        if let view = element as? NSView {
            subviews = view.subviews
        } else if let window = element as? NSWindow, let content = window.contentView {
            subviews = [content]
        } else {
            subviews = []
        }

        if snapshot.count < 20 {
            let typeName = String(String(reflecting: type(of: element)).prefix(96))
            let name = String(names.joined(separator: " / ").prefix(80))
            snapshot.append(
                "\(typeName) role=\(role?.rawValue ?? "nil") name=\(name) "
                + "enabled=\(String(describing: isEnabled)) "
                + "protocols=\(element is any NSAccessibilityProtocol)/"
                + "\(element is any NSAccessibilityElementProtocol)/\(element is any NSAccessibilityButton) "
                + "children=\(children.count)/\(navigationChildren.count)/\(subviews.count)")
        }
        if role == .button, names.contains(String(localized: "Cancel Trash")), isEnabled == enabled {
            return AccessibilityButton(element: element)
        }

        // Visit both platform accessibility edges and real native view edges;
        // SwiftUI can expose either proxies or view-backed controls. Identity
        // tracking deduplicates overlaps and prevents cycles through wrappers.
        for child in children + navigationChildren + subviews.map({ $0 as Any }) {
            if let button = findCancelButton(
                in: child, enabled: enabled, visited: &visited, snapshot: &snapshot) { return button }
        }
        return nil
    }

    private func accessibilityArray(from element: AnyObject, selector: Selector) -> [Any] {
        // Native navigation arrays can contain NSAccessibilityReparentingCellProxy.
        // Calling the Swift-imported typed getter traps while bridging that
        // proxy, before a subsequent `as [Any]` can help. Invoke the same public
        // Objective-C getter and preserve its raw NSArray elements instead.
        guard let object = element as? any NSObjectProtocol,
            object.responds(to: selector),
            let array = object.perform(selector)?.takeUnretainedValue() as? NSArray
        else { return [] }
        return (0..<array.count).map { array.object(at: $0) }
    }

    @MainActor
    private struct AccessibilityButton {
        let element: AnyObject

        func accessibilityPerformPress() -> Bool {
            element.accessibilityPerformPress?() ?? false
        }
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

    private struct HostedControlError: Error, CustomStringConvertible {
        let description: String
    }
}
