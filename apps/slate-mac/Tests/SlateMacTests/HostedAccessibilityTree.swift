// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import ApplicationServices
import SwiftUI
import XCTest

/// One element of a hosted view's accessibility tree as an
/// assistive-technology client reads it: the AXUIElement attributes that
/// VoiceOver and Accessibility Inspector read, copied into values so a
/// fact can assert on them after the walk.
struct AXElementSnapshot: CustomStringConvertible {
    let role: String?
    /// `AXSubrole`: what refines the role for VoiceOver, e.g. a list's
    /// `AXContentList`, which VoiceOver speaks as "content list".
    let subrole: String?
    let title: String?
    /// `AXDescription`: the accessible name `.accessibilityLabel` sets.
    let name: String?
    /// `AXValue` as text: a static text's string, a control's value.
    let value: String?
    /// `AXValueDescription`: where SwiftUI publishes `.accessibilityValue`
    /// on an element with no value of its own, such as a group.
    let valueDescription: String?
    /// `AXSelected`: a table or outline row the client reads as selected.
    let selected: Bool
    let children: [AXElementSnapshot]

    /// This element and every descendant, depth first.
    var subtree: [AXElementSnapshot] { [self] + children.flatMap(\.subtree) }

    func elements(role: String) -> [AXElementSnapshot] {
        subtree.filter { $0.role == role }
    }

    /// The static texts' strings, in reading order.
    var staticTexts: [String] {
        elements(role: "AXStaticText").compactMap(\.value)
    }

    var description: String { lines(depth: 0).joined(separator: "\n") }

    private func lines(depth: Int) -> [String] {
        let fields = [
            role ?? "?",
            subrole.map { "subrole=\($0)" },
            title.map { "title=\($0)" },
            name.map { "name=\($0)" },
            value.map { "value=\($0)" },
            valueDescription.map { "valueDescription=\($0)" },
            selected ? "selected" : nil,
        ].compactMap { $0 }
        return [String(repeating: "  ", count: depth) + fields.joined(separator: " ")]
            + children.flatMap { $0.lines(depth: depth + 1) }
    }
}

/// Hosts a SwiftUI view in a real window and reads its accessibility tree
/// through the AXUIElement API, the client path VoiceOver and
/// Accessibility Inspector use. XCTest proves attributes, not speech
/// (plan 43 §4 rule 6): these facts pin what an AT client is handed.
///
/// Every client call stays on the main actor. A request this process makes
/// of itself is answered synchronously on the calling thread, and SwiftUI
/// refreshes its accessibility nodes while answering; off the main thread
/// that refresh can trap in `MainActor.assumeIsolated` once the tree has
/// changed (reading the sync panel after expanding a disclosure did).
@MainActor
enum HostedAccessibilityTree {
    struct NotReady: Error, CustomStringConvertible {
        let description: String
    }

    /// XCTest never enters `NSApplication.run()`, so until something calls
    /// `finishLaunching()` the worker has no AppKit accessibility server and
    /// every client request answers kAXErrorNotImplemented. The activation
    /// policy is deliberately left alone: TrashProgressStripTests asserts its
    /// own switch to `.accessory`, which would read false had this run first
    /// in the same worker. A second `finishLaunching()` there is harmless.
    private static let launched: Void = {
        NSApplication.shared.finishLaunching()
    }()

    /// A titled window whose content is `view`, ordered front, and tagged
    /// with a unique accessibility identifier so the client walk reads this
    /// window and no other (a `.navigationTitle` renames the window, so the
    /// title cannot identify it).
    static func host<V: View>(
        _ view: V, size: NSSize = NSSize(width: 560, height: 420)
    ) -> NSWindow {
        _ = launched
        let window = NSWindow(
            contentRect: NSRect(origin: .zero, size: size),
            styleMask: [.titled],
            backing: .buffered,
            defer: false)
        window.isReleasedWhenClosed = false
        window.contentView = NSHostingView(rootView: view)
        window.setAccessibilityIdentifier("hosted-ax-\(UUID().uuidString)")
        window.makeKeyAndOrderFront(nil)
        window.contentView?.layoutSubtreeIfNeeded()
        return window
    }

    /// The window's tree as a client reads it, once `ready` holds. SwiftUI
    /// builds its accessibility tree lazily and publishes it on a later
    /// rendering pass, so this re-reads the tree, yielding the main actor
    /// between reads, until the state the fact needs is there, rather than
    /// sleeping for a fixed time.
    ///
    /// A timeout is recorded through `XCTFail` (with the last tree read)
    /// before the throw: once a SwiftUI view has been hosted, this XCTest
    /// build reports any error that escapes the test as an internal
    /// framework error ("InvalidTransition"), not as the one thrown.
    static func snapshot(
        of window: NSWindow,
        timeout: Duration = .seconds(30),
        file: StaticString = #filePath,
        line: UInt = #line,
        until ready: (AXElementSnapshot) -> Bool
    ) async throws -> AXElementSnapshot {
        let deadline = ContinuousClock.now.advanced(by: timeout)
        var last: AXElementSnapshot?
        repeat {
            window.contentView?.layoutSubtreeIfNeeded()
            last = clientWindow(of: window).map { tree(of: $0) }
            if let last, ready(last) { return last }
            try await Task.sleep(for: .milliseconds(20))
        } while ContinuousClock.now < deadline
        let failure = NotReady(
            description: "the hosted tree never reached the awaited state; last client read:\n"
                + (last?.description ?? "(no window answered)"))
        XCTFail(failure.description, file: file, line: line)
        throw failure
    }

    /// Sends AXPress, the action VoiceOver's VO-Space sends, to the first
    /// element `match` accepts. Fails only when nothing matches: AppKit's
    /// segmented control answers its segments' AXPress with
    /// kAXErrorFailure even as it selects the segment, so a fact judges a
    /// press by its effect on the next snapshot, not by the return code.
    static func press(
        in window: NSWindow,
        file: StaticString = #filePath,
        line: UInt = #line,
        where match: (AXElementSnapshot) -> Bool
    ) throws {
        let root = clientWindow(of: window)
        var pending = root.map { [$0] } ?? []
        var budget = 2_000
        while !pending.isEmpty, budget > 0 {
            budget -= 1
            let element = pending.removeFirst()
            if match(shallow(element)) {
                _ = AXUIElementPerformAction(element, kAXPressAction as CFString)
                return
            }
            pending += (attribute(element, kAXChildrenAttribute) as? [AXUIElement]) ?? []
        }
        let failure = NotReady(
            description: "no element in the hosted tree accepts the press; client read:\n"
                + (root.map { tree(of: $0).description } ?? "(no window answered)"))
        XCTFail(failure.description, file: file, line: line)
        throw failure
    }

    /// Moves a reader onto a row the way an assistive-technology client
    /// does: keyboard focus to the first table or outline `table` accepts
    /// (`AXFocused`), then its first row `row` accepts as the selection
    /// (`AXSelectedRows`). Fails only when no row matches; a fact judges the
    /// move by its effect, as `press` does.
    static func selectRow(
        in window: NSWindow,
        file: StaticString = #filePath,
        line: UInt = #line,
        table: (AXElementSnapshot) -> Bool,
        row: (AXElementSnapshot) -> Bool
    ) throws {
        let root = clientWindow(of: window)
        var pending = root.map { [$0] } ?? []
        var budget = 2_000
        while !pending.isEmpty, budget > 0 {
            budget -= 1
            let element = pending.removeFirst()
            let role = text(attribute(element, kAXRoleAttribute))
            if role == "AXOutline" || role == "AXTable", table(tree(of: element)) {
                let rows = (attribute(element, kAXRowsAttribute) as? [AXUIElement]) ?? []
                if let match = rows.first(where: { row(tree(of: $0)) }) {
                    _ = AXUIElementSetAttributeValue(
                        element, kAXFocusedAttribute as CFString, kCFBooleanTrue)
                    _ = AXUIElementSetAttributeValue(
                        element, kAXSelectedRowsAttribute as CFString, [match] as CFArray)
                    return
                }
            }
            pending += (attribute(element, kAXChildrenAttribute) as? [AXUIElement]) ?? []
        }
        let failure = NotReady(
            description: "no table row in the hosted tree accepts the selection; client read:\n"
                + (root.map { tree(of: $0).description } ?? "(no window answered)"))
        XCTFail(failure.description, file: file, line: line)
        throw failure
    }

    // MARK: - Client reads

    private static func clientWindow(of window: NSWindow) -> AXUIElement? {
        let identifier = window.accessibilityIdentifier()
        let application = AXUIElementCreateApplication(ProcessInfo.processInfo.processIdentifier)
        guard let windows = attribute(application, kAXWindowsAttribute) as? [AXUIElement] else {
            return nil
        }
        return windows.first { text(attribute($0, kAXIdentifierAttribute)) == identifier }
    }

    private static func tree(of root: AXUIElement) -> AXElementSnapshot {
        var budget = 2_000
        return tree(of: root, depth: 0, budget: &budget)
    }

    private static func tree(
        of element: AXUIElement, depth: Int, budget: inout Int
    ) -> AXElementSnapshot {
        budget -= 1
        var children: [AXElementSnapshot] = []
        if depth < 40, let raw = attribute(element, kAXChildrenAttribute) as? [AXUIElement] {
            for child in raw where budget > 0 {
                children.append(tree(of: child, depth: depth + 1, budget: &budget))
            }
        }
        return shallow(element, children: children)
    }

    private static func shallow(
        _ element: AXUIElement, children: [AXElementSnapshot] = []
    ) -> AXElementSnapshot {
        AXElementSnapshot(
            role: text(attribute(element, kAXRoleAttribute)),
            subrole: text(attribute(element, kAXSubroleAttribute)),
            title: text(attribute(element, kAXTitleAttribute)),
            name: text(attribute(element, kAXDescriptionAttribute)),
            value: text(attribute(element, kAXValueAttribute)),
            valueDescription: text(attribute(element, "AXValueDescription")),
            selected: (attribute(element, kAXSelectedAttribute) as? NSNumber)?.boolValue
                ?? false,
            children: children)
    }

    private static func attribute(_ element: AXUIElement, _ name: String) -> CFTypeRef? {
        var value: CFTypeRef?
        guard AXUIElementCopyAttributeValue(element, name as CFString, &value) == .success else {
            return nil
        }
        return value
    }

    /// Strings and numbers as text; element references and geometry as nil.
    private static func text(_ value: CFTypeRef?) -> String? {
        switch value {
        case let string as String: return string
        case let number as NSNumber: return number.stringValue
        default: return nil
        }
    }
}
