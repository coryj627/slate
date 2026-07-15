// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

@MainActor
private final class OpenSelectedKeyProbeLog: ObservableObject {
    @Published var entries: [String] = []
    @Published var monitorEnabled = true
    @Published var monitorRenaming = false
    @Published var openMarker = "open-selected"
}

@MainActor
private final class KeyDownRecorderView: NSView {
    private(set) var receivedKeyDownCount = 0

    override var acceptsFirstResponder: Bool { true }

    override func keyDown(with event: NSEvent) {
        receivedKeyDownCount += 1
    }
}

@MainActor
private final class HostedKeyWindow: NSWindow {
    var reportsKeyWindow = false

    override var isKeyWindow: Bool { reportsKeyWindow || super.isKeyWindow }
}

@MainActor
private final class HostedWindowRouting {
    var applicationIsActive = true
    weak var keyWindow: NSWindow?
}

@MainActor
private struct OpenSelectedKeyDeliveryProbe: View {
    @ObservedObject var log: OpenSelectedKeyProbeLog
    let routing: HostedWindowRouting
    let isRenaming: Bool
    let shouldFocus: Bool
    @FocusState private var focused: Bool
    @State private var selection: Int? = 0

    var body: some View {
        List(selection: $selection) {
            Text("First").tag(0)
            Text("Second").tag(1)
        }
        .focused($focused)
        // Match the shipped modifier-selection handlers under the scoped monitor.
        .onMoveCommand { direction in
            log.entries.append("move:\(direction)")
        }
        .onKeyPress(keys: [.upArrow, .downArrow]) { press in
            guard let action = FileTreeSidebar.selectionKeyAction(
                key: press.key,
                modifiers: press.modifiers,
                fileTreeFocused: focused,
                isRenaming: false)
            else {
                log.entries.append("key:ignored")
                return .ignored
            }
            switch action {
            case .extend(.up): log.entries.append("key:extend-up")
            case .extend(.down): log.entries.append("key:extend-down")
            case .selectAll: log.entries.append("key:select-all")
            case .openSelected: log.entries.append("key:open-selected")
            }
            return .handled
        }
        .background {
            TreeOpenSelectedKeyMonitor(
                enabled: shouldFocus && log.monitorEnabled,
                isRenaming: isRenaming || log.monitorRenaming,
                applicationIsActive: { routing.applicationIsActive },
                currentKeyWindow: { routing.keyWindow },
                openSelected: { [marker = log.openMarker] in
                    log.entries.append(marker)
                })
                .frame(width: 0, height: 0)
        }
        .onAppear {
            focused = shouldFocus
        }
    }
}

@MainActor
final class FileTreeKeyboardOpenDragTests: XCTestCase {
    private func fileRow(_ path: String) -> FileTreeSidebar.RowID {
        .node(.file(path: path))
    }

    private func sidebarSource() throws -> String {
        let url = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Sources/SlateMac/FileTreeSidebar.swift")
        return try String(contentsOf: url, encoding: .utf8)
    }

    private func downArrowEvent(
        for window: NSWindow,
        modifiers: NSEvent.ModifierFlags,
        isRepeat: Bool = false
    ) -> NSEvent {
        NSEvent.keyEvent(
            with: .keyDown,
            location: .zero,
            modifierFlags: modifiers,
            timestamp: ProcessInfo.processInfo.systemUptime,
            windowNumber: window.windowNumber,
            context: nil,
            characters: "\u{F701}",
            charactersIgnoringModifiers: "\u{F701}",
            isARepeat: isRepeat,
            keyCode: 125)!
    }

    private func pumpRunLoop(_ interval: TimeInterval) {
        RunLoop.main.run(until: Date(timeIntervalSinceNow: interval))
    }

    func testOpenSelectionProjectsOnlyVisibleFilesInFlattenedOrder() {
        let a = fileRow("a.md")
        let hidden = fileRow("hidden.md")
        let folder: FileTreeSidebar.RowID = .node(.dir(99))
        let b = fileRow("b.md")
        let model = FileTreeSidebar.SelectionModel(
            focused: b,
            selected: [b, hidden, folder, a],
            selectionPathSnapshots: [
                b: "b.md", hidden: "hidden.md", folder: "folder", a: "a.md",
            ],
            rangeAnchor: a,
            rangeAnchorPathSnapshot: "a.md")
        let visibleRows = [
            FileTreeSidebar.SelectionRow(identity: a, path: "a.md", isDirectory: false),
            FileTreeSidebar.SelectionRow(
                identity: folder, path: "folder", isDirectory: true),
            FileTreeSidebar.SelectionRow(identity: b, path: "b.md", isDirectory: false),
        ]

        XCTAssertEqual(
            FileTreeSidebar.openSelectedPaths(from: model, visibleRows: visibleRows),
            ["a.md", "b.md"])
    }

    func testOpenSelectionKeepsVisibleOrderButOpensFocusedSelectedFileLast() throws {
        let a = fileRow("a.md")
        let b = fileRow("b.md")
        let c = fileRow("c.md")
        let rows = [a, b, c].enumerated().map { index, id in
            FileTreeSidebar.SelectionRow(
                identity: id,
                path: ["a.md", "b.md", "c.md"][index],
                isDirectory: false)
        }
        let model = FileTreeSidebar.SelectionModel(
            focused: a,
            selected: [a, b, c],
            selectionPathSnapshots: [a: "a.md", b: "b.md", c: "c.md"],
            rangeAnchor: b,
            rangeAnchorPathSnapshot: "b.md")

        let batch = FileTreeSidebar.openSelectionBatch(from: model, visibleRows: rows)
        XCTAssertEqual(batch.paths, ["a.md", "b.md", "c.md"])
        XCTAssertEqual(batch.focusedPath, "a.md")
        XCTAssertEqual(batch.executionPaths, ["b.md", "c.md", "a.md"])

        let session = NSObject()
        let tenRows = (1...10).map { index in
            FileTreeSidebar.SelectionRow(
                identity: fileRow("note-\(index).md"),
                path: "note-\(index).md",
                isDirectory: false)
        }
        let focused = tenRows[2]
        let tenModel = FileTreeSidebar.SelectionModel(
            focused: focused.identity,
            selected: Set(tenRows.map(\.identity)),
            selectionPathSnapshots: Dictionary(
                uniqueKeysWithValues: tenRows.map { ($0.identity, $0.path) }),
            rangeAnchor: tenRows[0].identity,
            rangeAnchorPathSnapshot: tenRows[0].path)
        let captured = FileTreeSidebar.openSelectionBatch(
            from: tenModel, visibleRows: tenRows)
        guard case let .confirm(request) = FileTreeSidebar.openSelectionDisposition(
            batch: captured,
            sessionIdentity: ObjectIdentifier(session))
        else { return XCTFail("ten files must stage") }
        XCTAssertEqual(
            request.paths,
            (1...10).map { "note-\($0).md" },
            "the staged capture remains in flattened visible order")
        XCTAssertEqual(request.focusedPath, "note-3.md")

        let changedModel = FileTreeSidebar.SelectionModel(
            focused: tenRows[9].identity,
            selected: [tenRows[9].identity],
            selectionPathSnapshots: [tenRows[9].identity: tenRows[9].path],
            rangeAnchor: tenRows[9].identity,
            rangeAnchorPathSnapshot: tenRows[9].path)
        XCTAssertEqual(
            FileTreeSidebar.openSelectionBatch(from: changedModel, visibleRows: tenRows)
                .focusedPath,
            "note-10.md",
            "prove the live selection changed after staging")
        XCTAssertEqual(
            FileTreeSidebar.resolvedOpenPaths(
                request,
                confirmed: true,
                currentSessionIdentity: ObjectIdentifier(session)),
            ["note-1.md", "note-2.md", "note-4.md", "note-5.md", "note-6.md",
             "note-7.md", "note-8.md", "note-9.md", "note-10.md", "note-3.md"],
            "confirmation uses the staged focus, not the later live selection")
    }

    func testKeyboardSelectionActionIsFocusRenameAndModifierGated() {
        XCTAssertNil(
            FileTreeSidebar.selectionKeyAction(
                key: .downArrow, modifiers: [.shift],
                fileTreeFocused: false, isRenaming: false))
        XCTAssertNil(
            FileTreeSidebar.selectionKeyAction(
                key: .downArrow, modifiers: [.shift],
                fileTreeFocused: true, isRenaming: true))
        XCTAssertEqual(
            FileTreeSidebar.selectionKeyAction(
                key: .downArrow, modifiers: [.shift, .capsLock],
                fileTreeFocused: true, isRenaming: false),
            .extend(.down))
        XCTAssertNil(
            FileTreeSidebar.selectionKeyAction(
                key: .downArrow, modifiers: [.shift, .option],
                fileTreeFocused: true, isRenaming: false))
        XCTAssertNil(
            FileTreeSidebar.selectionKeyAction(
                key: .downArrow, modifiers: [.shift, .control],
                fileTreeFocused: true, isRenaming: false))
    }

    func testKeyboardSelectionActionRecognizesSelectAllAndOpenShortcuts() {
        XCTAssertEqual(
            FileTreeSidebar.selectionKeyAction(
                key: "a", modifiers: [.command, .capsLock],
                fileTreeFocused: true, isRenaming: false),
            .selectAll)
        XCTAssertEqual(
            FileTreeSidebar.selectionKeyAction(
                key: .return, modifiers: [.capsLock],
                fileTreeFocused: true, isRenaming: false),
            .openSelected)
        XCTAssertNil(
            FileTreeSidebar.selectionKeyAction(
                key: .downArrow, modifiers: [.command, .capsLock],
                fileTreeFocused: true, isRenaming: false))
        XCTAssertNil(
            FileTreeSidebar.selectionKeyAction(
                key: "a", modifiers: [.command, .option],
                fileTreeFocused: true, isRenaming: false))
    }

    func testOpenSelectedKeyDispositionIsExactFocusRenameAndRepeatGated() {
        let exact: NSEvent.ModifierFlags = [.command, .function, .numericPad]
        XCTAssertEqual(
            TreeOpenSelectedKey.disposition(
                keyCode: 125, modifierFlags: exact, isRepeat: false,
                fileTreeFocused: true, isRenaming: false),
            .open)
        XCTAssertEqual(
            TreeOpenSelectedKey.disposition(
                keyCode: 125, modifierFlags: exact.union(.capsLock), isRepeat: false,
                fileTreeFocused: true, isRenaming: false),
            .open)
        XCTAssertEqual(
            TreeOpenSelectedKey.disposition(
                keyCode: 125, modifierFlags: exact, isRepeat: true,
                fileTreeFocused: true, isRenaming: false),
            .suppressRepeat)
        for modifiers in [
            exact.union(.option), exact.union(.control), exact.union(.shift), [],
        ] {
            XCTAssertEqual(
                TreeOpenSelectedKey.disposition(
                    keyCode: 125, modifierFlags: modifiers, isRepeat: false,
                    fileTreeFocused: true, isRenaming: false),
                .passThrough)
        }
        XCTAssertEqual(
            TreeOpenSelectedKey.disposition(
                keyCode: 126, modifierFlags: exact, isRepeat: false,
                fileTreeFocused: true, isRenaming: false),
            .passThrough)
        XCTAssertEqual(
            TreeOpenSelectedKey.disposition(
                keyCode: 125, modifierFlags: exact, isRepeat: false,
                fileTreeFocused: false, isRenaming: false),
            .passThrough)
        XCTAssertEqual(
            TreeOpenSelectedKey.disposition(
                keyCode: 125, modifierFlags: exact, isRepeat: false,
                fileTreeFocused: true, isRenaming: true),
            .passThrough)
    }

    func testOpenSelectedKeyWindowScopeRequiresEveryActiveOwnerGate() {
        let owner = HostedKeyWindow(
            contentRect: .zero, styleMask: [], backing: .buffered, defer: false)
        let other = HostedKeyWindow(
            contentRect: .zero, styleMask: [], backing: .buffered, defer: false)
        owner.reportsKeyWindow = true

        XCTAssertTrue(
            TreeOpenSelectedKey.eventBelongsToActiveOwner(
                eventWindow: owner,
                ownerWindow: owner,
                applicationIsActive: true,
                keyWindow: owner))
        XCTAssertFalse(
            TreeOpenSelectedKey.eventBelongsToActiveOwner(
                eventWindow: owner,
                ownerWindow: owner,
                applicationIsActive: false,
                keyWindow: owner),
            "an inactive application must pass through")

        owner.reportsKeyWindow = false
        XCTAssertFalse(
            TreeOpenSelectedKey.eventBelongsToActiveOwner(
                eventWindow: owner,
                ownerWindow: owner,
                applicationIsActive: true,
                keyWindow: owner),
            "a non-key owner must pass through")
        owner.reportsKeyWindow = true

        XCTAssertFalse(
            TreeOpenSelectedKey.eventBelongsToActiveOwner(
                eventWindow: owner,
                ownerWindow: owner,
                applicationIsActive: true,
                keyWindow: other),
            "the current application key window must be the owner")
        XCTAssertFalse(
            TreeOpenSelectedKey.eventBelongsToActiveOwner(
                eventWindow: other,
                ownerWindow: owner,
                applicationIsActive: true,
                keyWindow: owner),
            "the event itself must belong to the owner window")
        XCTAssertFalse(
            TreeOpenSelectedKey.eventBelongsToActiveOwner(
                eventWindow: owner,
                ownerWindow: nil,
                applicationIsActive: true,
                keyWindow: owner),
            "a detached monitor view has no owner")
    }

    func testHostedScopedMonitorIsOwningWindowBoundAndPreservesArrowHandling() {
        let log = OpenSelectedKeyProbeLog()
        let routing = HostedWindowRouting()
        let host = NSHostingView(
            rootView: OpenSelectedKeyDeliveryProbe(
                log: log, routing: routing, isRenaming: false, shouldFocus: true))
        host.frame = NSRect(x: 0, y: 0, width: 360, height: 240)
        let window = HostedKeyWindow(
            contentRect: host.frame,
            styleMask: [.titled],
            backing: .buffered,
            defer: false)
        window.isReleasedWhenClosed = false
        window.contentView = host
        window.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
        pumpRunLoop(0.5)

        let otherResponder = KeyDownRecorderView(
            frame: NSRect(x: 0, y: 0, width: 180, height: 100))
        let otherWindow = NSWindow(
            contentRect: otherResponder.frame,
            styleMask: [.titled],
            backing: .buffered,
            defer: false)
        otherWindow.isReleasedWhenClosed = false
        otherWindow.contentView = otherResponder
        otherWindow.makeKeyAndOrderFront(nil)
        otherWindow.makeFirstResponder(otherResponder)
        routing.keyWindow = otherWindow
        pumpRunLoop(0.2)

        log.entries.removeAll()
        NSApp.sendEvent(downArrowEvent(for: otherWindow, modifiers: [.command]))
        pumpRunLoop(0.1)
        XCTAssertEqual(
            log.entries,
            [],
            "Command-Down in another key window must not open the hidden tree")
        XCTAssertEqual(
            otherResponder.receivedKeyDownCount,
            1,
            "the owning-window mismatch must pass the event to its real responder")

        otherWindow.orderOut(nil)
        otherWindow.contentView = nil
        otherWindow.close()
        window.makeKeyAndOrderFront(nil)
        pumpRunLoop(0.2)

        let sheetResponder = KeyDownRecorderView(
            frame: NSRect(x: 0, y: 0, width: 220, height: 120))
        let sheetWindow = NSWindow(
            contentRect: sheetResponder.frame,
            styleMask: [.titled],
            backing: .buffered,
            defer: false)
        sheetWindow.isReleasedWhenClosed = false
        sheetWindow.contentView = sheetResponder
        window.beginSheet(sheetWindow)
        sheetWindow.makeFirstResponder(sheetResponder)
        routing.keyWindow = sheetWindow
        pumpRunLoop(0.2)

        log.entries.removeAll()
        NSApp.sendEvent(downArrowEvent(for: sheetWindow, modifiers: [.command]))
        pumpRunLoop(0.1)
        XCTAssertEqual(
            log.entries,
            [],
            "Command-Down in an attached sheet must not open its parent tree")
        XCTAssertEqual(
            sheetResponder.receivedKeyDownCount,
            1,
            "a sheet event must pass through to the sheet responder")

        window.endSheet(sheetWindow)
        sheetWindow.orderOut(nil)
        sheetWindow.contentView = nil
        sheetWindow.close()
        window.makeKeyAndOrderFront(nil)
        window.reportsKeyWindow = true
        routing.keyWindow = window
        pumpRunLoop(0.2)
        XCTAssertTrue(window.isKeyWindow, "the reactivated tree owner must report key")

        log.entries.removeAll()
        NSApp.sendEvent(downArrowEvent(for: window, modifiers: [.command]))
        pumpRunLoop(0.2)

        XCTAssertEqual(
            log.entries,
            ["open-selected"],
            "the scoped monitor must route exact Command-Down before SwiftUI erases modifiers")

        NSApp.sendEvent(
            downArrowEvent(for: window, modifiers: [.command], isRepeat: true))
        pumpRunLoop(0.2)
        XCTAssertEqual(
            log.entries,
            ["open-selected"],
            "a held Command-Down must not reopen the selection")

        log.openMarker = "updated-open"
        pumpRunLoop(0.1)
        log.entries.removeAll()
        NSApp.sendEvent(downArrowEvent(for: window, modifiers: [.command]))
        pumpRunLoop(0.1)
        XCTAssertEqual(
            log.entries,
            ["updated-open"],
            "updateNSView must replace the live open callback")
        log.openMarker = "open-selected"

        log.monitorRenaming = true
        pumpRunLoop(0.1)
        log.entries.removeAll()
        NSApp.sendEvent(downArrowEvent(for: window, modifiers: [.command]))
        pumpRunLoop(0.1)
        XCTAssertFalse(
            log.entries.contains("open-selected"),
            "updateNSView must pass through while rename is live")
        log.monitorRenaming = false

        log.monitorEnabled = false
        pumpRunLoop(0.1)
        log.entries.removeAll()
        NSApp.sendEvent(downArrowEvent(for: window, modifiers: [.command]))
        pumpRunLoop(0.1)
        XCTAssertFalse(
            log.entries.contains("open-selected"),
            "updateNSView must pass through after tree focus is disabled")
        log.monitorEnabled = true
        pumpRunLoop(0.1)

        for modifiers in [
            NSEvent.ModifierFlags.command.union(.option),
            .command.union(.control),
            .command.union(.shift),
        ] {
            log.entries.removeAll()
            NSApp.sendEvent(downArrowEvent(for: window, modifiers: modifiers))
            pumpRunLoop(0.1)
            XCTAssertFalse(
                log.entries.contains("open-selected"),
                "extra modifier must pass through: \(modifiers)")
        }

        log.entries.removeAll()
        NSApp.sendEvent(downArrowEvent(for: window, modifiers: [.shift]))
        pumpRunLoop(0.1)
        XCTAssertEqual(log.entries, ["key:extend-down"])

        log.entries.removeAll()
        NSApp.sendEvent(downArrowEvent(for: window, modifiers: []))
        pumpRunLoop(0.1)
        XCTAssertFalse(log.entries.contains("open-selected"))

        window.orderOut(nil)
        window.contentView = nil
        window.close()
        pumpRunLoop(0.2)
    }

    func testOpenSelectedKeyMonitorDismantleStopsInterception() {
        let log = OpenSelectedKeyProbeLog()
        let routing = HostedWindowRouting()
        let host = NSHostingView(
            rootView: OpenSelectedKeyDeliveryProbe(
                log: log, routing: routing, isRenaming: false, shouldFocus: true))
        host.frame = NSRect(x: 0, y: 0, width: 240, height: 120)
        let window = HostedKeyWindow(
            contentRect: host.frame,
            styleMask: [.titled],
            backing: .buffered,
            defer: false)
        window.isReleasedWhenClosed = false
        window.contentView = host
        window.makeKeyAndOrderFront(nil)
        window.reportsKeyWindow = true
        routing.keyWindow = window
        pumpRunLoop(0.3)

        log.entries.removeAll()
        NSApp.sendEvent(downArrowEvent(for: window, modifiers: [.command]))
        pumpRunLoop(0.1)
        XCTAssertEqual(log.entries, ["open-selected"])

        let replacement = KeyDownRecorderView(frame: host.frame)
        window.contentView = replacement
        window.makeFirstResponder(replacement)
        pumpRunLoop(0.2)
        log.entries.removeAll()
        NSApp.sendEvent(downArrowEvent(for: window, modifiers: [.command]))
        pumpRunLoop(0.1)
        XCTAssertEqual(log.entries, [], "dismantle must remove the hosted monitor")
        XCTAssertEqual(
            replacement.receivedKeyDownCount,
            1,
            "the event must reach the replacement responder after dismantle")

        window.orderOut(nil)
        window.contentView = nil
        window.close()
        pumpRunLoop(0.1)
    }

    func testKeyboardExtensionMutatesModelBeforeMirroringAndConsumesBoundary() throws {
        let a = fileRow("a.md")
        let b = fileRow("b.md")
        let rows = [
            FileTreeSidebar.SelectionRow(identity: a, path: "a.md", isDirectory: false),
            FileTreeSidebar.SelectionRow(identity: b, path: "b.md", isDirectory: false),
        ]
        let model = FileTreeSidebar.SelectionModel(
            focused: a,
            selected: [a],
            selectionPathSnapshots: [a: "a.md"],
            rangeAnchor: a,
            rangeAnchorPathSnapshot: "a.md")

        let moved = FileTreeSidebar.keyboardSelectionOutcome(
            action: .extend(.down), model: model,
            currentListSelection: a, visibleRows: rows)
        XCTAssertEqual(moved.model.focused, b)
        XCTAssertEqual(moved.model.selected, [a, b])
        XCTAssertEqual(moved.listSelection, b)
        XCTAssertTrue(moved.shouldMirrorListSelection)
        XCTAssertEqual(moved.visibleSelectedCount, 2)

        let boundary = FileTreeSidebar.keyboardSelectionOutcome(
            action: .extend(.down), model: moved.model,
            currentListSelection: b, visibleRows: rows)
        XCTAssertTrue(boundary.handled, "an unchanged list boundary is consumed")
        XCTAssertFalse(boundary.changed)
        XCTAssertFalse(
            boundary.shouldMirrorListSelection,
            "a same-value write must not arm one-shot open suppression")

        let shrunk = FileTreeSidebar.keyboardSelectionOutcome(
            action: .extend(.up), model: moved.model,
            currentListSelection: b, visibleRows: rows)
        XCTAssertEqual(shrunk.model.selected, [a])
        XCTAssertEqual(shrunk.visibleSelectedCount, 1)
        XCTAssertTrue(shrunk.shouldMirrorListSelection)
    }

    func testCommandASelectsOnlySuppliedVisibleRows() {
        let a = fileRow("a.md")
        let b = fileRow("b.md")
        let hidden = fileRow("hidden.md")
        let rows = [
            FileTreeSidebar.SelectionRow(identity: a, path: "a.md", isDirectory: false),
            FileTreeSidebar.SelectionRow(identity: b, path: "b.md", isDirectory: false),
        ]
        let model = FileTreeSidebar.SelectionModel(
            focused: a,
            selected: [a, hidden],
            selectionPathSnapshots: [a: "a.md", hidden: "hidden.md"],
            rangeAnchor: a,
            rangeAnchorPathSnapshot: "a.md")

        let outcome = FileTreeSidebar.keyboardSelectionOutcome(
            action: .selectAll, model: model,
            currentListSelection: a, visibleRows: rows)

        XCTAssertEqual(outcome.model.selected, [a, b])
        XCTAssertEqual(outcome.model.focused, a)
        XCTAssertEqual(outcome.visibleSelectedCount, 2)
        XCTAssertFalse(outcome.shouldMirrorListSelection)
    }

    func testTreeListWiresKeyboardSelectionDispatch() throws {
        let source = try sidebarSource()
        XCTAssertTrue(source.contains(".onKeyPress(keys: [.upArrow, .downArrow])"))
        XCTAssertTrue(source.contains(".onKeyPress(\"a\""))
        XCTAssertTrue(source.contains("handleMoveCommand(direction)"))
        XCTAssertTrue(
            source.contains(".onKeyPress(keys: [.space, .return], phases: .down)"),
            "Return must not create a held-key open storm")
        XCTAssertTrue(source.contains("handleSelectionKeyAction("))
        XCTAssertTrue(source.contains("requestOpenSelected()"))
        XCTAssertTrue(source.contains("pendingOpenSelection?.title"))
        XCTAssertTrue(source.contains("TreeOpenSelectedKeyMonitor("))
        XCTAssertTrue(source.contains("enabled: fileTreeFocused"))
        XCTAssertTrue(source.contains("isRenaming: appState.renamingNode != nil"))
        XCTAssertFalse(source.contains("openSelectedKeyDownMonitor"))
        XCTAssertFalse(source.contains("installOpenSelectedKeyDownMonitor"))
        XCTAssertEqual(
            source.components(separatedBy: ".onDrag { dragItem(for: node) } preview: {")
                .count - 1,
            2,
            "file and folder rows must both render the shared drag preview")
        XCTAssertTrue(source.contains("dragPreview(for: node)"))
        let previewStart = try XCTUnwrap(source.range(of: "private func dragPreview("))
        let previewEnd = try XCTUnwrap(
            source.range(
                of: "static func makeDragProvider(nodePath:",
                range: previewStart.upperBound..<source.endIndex))
        let previewSource = source[previewStart.lowerBound..<previewEnd.lowerBound]
        XCTAssertTrue(previewSource.contains("if count > 1"))
        XCTAssertTrue(previewSource.contains("Tokens.ColorRole.accentFill"))
        XCTAssertTrue(
            previewSource.contains(".accessibilityHidden(true)"),
            "the visual count badge must not duplicate selection state for VoiceOver")
        XCTAssertTrue(
            source.contains(
                ".onChange(of: appState.currentSession.map(ObjectIdentifier.init))"),
            "same-URL session replacement must dismiss a staged open request")

        let dropStart = try XCTUnwrap(source.range(of: "private func handleDrop("))
        let dropEnd = try XCTUnwrap(
            source.range(
                of: "private func handleFileURLDrop(",
                range: dropStart.upperBound..<source.endIndex))
        let dropSource = source[dropStart.lowerBound..<dropEnd.lowerBound]
        XCTAssertTrue(dropSource.contains("Self.preferredDropProvider(in: providers)"))
        XCTAssertTrue(dropSource.contains("Self.decodeDragPayload(data)"))
        XCTAssertTrue(dropSource.contains("appState.moveTreeSelection("))
        XCTAssertFalse(dropSource.contains("appState.dragSourceNode"))
    }

    func testOpenSelectionStagesAtTenWithSpecificCopy() throws {
        let session = NSObject()
        let nine = (1...9).map { "note-\($0).md" }
        let nineBatch = FileTreeSidebar.OpenSelectionBatch(
            paths: nine, focusedPath: nine[2])
        XCTAssertEqual(
            FileTreeSidebar.openSelectionDisposition(
                batch: nineBatch, sessionIdentity: ObjectIdentifier(session)),
            .direct(nineBatch))

        let ten = (1...10).map { "note-\($0).md" }
        let tenBatch = FileTreeSidebar.OpenSelectionBatch(
            paths: ten, focusedPath: ten[2])
        let disposition = FileTreeSidebar.openSelectionDisposition(
            batch: tenBatch, sessionIdentity: ObjectIdentifier(session))
        guard case let .confirm(request) = disposition else {
            return XCTFail("10 selected files must stage one confirmation")
        }
        XCTAssertEqual(request.paths, ten)
        XCTAssertEqual(request.focusedPath, ten[2])
        XCTAssertEqual(request.title, "Open 10 Files?")
        XCTAssertEqual(request.message, "This will open each selected file in a tab.")
    }

    func testOpenSelectionCancelAndStaleSessionOpenNothing() throws {
        let originalSession = NSObject()
        let replacementSession = NSObject()
        let paths = (1...10).map { "note-\($0).md" }
        let batch = FileTreeSidebar.OpenSelectionBatch(
            paths: paths, focusedPath: paths[2])
        let disposition = FileTreeSidebar.openSelectionDisposition(
            batch: batch, sessionIdentity: ObjectIdentifier(originalSession))
        guard case let .confirm(request) = disposition else {
            return XCTFail("expected a staged request")
        }

        XCTAssertEqual(
            FileTreeSidebar.resolvedOpenPaths(
                request, confirmed: false,
                currentSessionIdentity: ObjectIdentifier(originalSession)),
            [], "Cancel opens nothing")
        XCTAssertEqual(
            FileTreeSidebar.resolvedOpenPaths(
                request, confirmed: true,
                currentSessionIdentity: ObjectIdentifier(replacementSession)),
            [], "a request from the previous vault is stale")
        XCTAssertEqual(
            FileTreeSidebar.resolvedOpenPaths(
                request, confirmed: true,
                currentSessionIdentity: ObjectIdentifier(originalSession)),
            [paths[0], paths[1]] + Array(paths[3...]) + [paths[2]],
            "confirmation uses the paths and focus captured before selection can change")
    }

    func testSelectedDragOriginProjectsWholeVisibleSelectionWithoutMutation() {
        let folder: FileTreeSidebar.RowID = .node(.dir(7))
        let child = fileRow("folder/child.md")
        let other = fileRow("other.md")
        let rows = [
            FileTreeSidebar.SelectionRow(
                identity: folder, path: "folder", isDirectory: true),
            FileTreeSidebar.SelectionRow(
                identity: child, path: "folder/child.md", isDirectory: false),
            FileTreeSidebar.SelectionRow(
                identity: other, path: "other.md", isDirectory: false),
        ]
        let model = FileTreeSidebar.SelectionModel(
            focused: child,
            selected: [other, folder, child],
            selectionPathSnapshots: [
                folder: "folder", child: "folder/child.md", other: "other.md",
            ],
            rangeAnchor: folder,
            rangeAnchorPathSnapshot: "folder")
        let before = model

        let items = FileTreeSidebar.dragItems(
            for: rows[1], from: model, visibleRows: rows)

        XCTAssertEqual(items.map(\.path), ["folder", "folder/child.md", "other.md"])
        XCTAssertEqual(items.map(\.isDirectory), [true, false, false])
        XCTAssertEqual(model, before, "drag start must not mutate selection state")
    }

    func testDragPreviewCountMatchesProjectedPayloadForSelectedAndUnselectedOrigins() {
        let folder: FileTreeSidebar.RowID = .node(.dir(7))
        let child = fileRow("folder/child.md")
        let other = fileRow("other.md")
        let unselected = fileRow("unselected.md")
        let rows = [
            FileTreeSidebar.SelectionRow(
                identity: folder, path: "folder", isDirectory: true),
            FileTreeSidebar.SelectionRow(
                identity: child, path: "folder/child.md", isDirectory: false),
            FileTreeSidebar.SelectionRow(
                identity: other, path: "other.md", isDirectory: false),
            FileTreeSidebar.SelectionRow(
                identity: unselected, path: "unselected.md", isDirectory: false),
        ]
        let model = FileTreeSidebar.SelectionModel(
            focused: child,
            selected: [other, folder, child],
            selectionPathSnapshots: [
                folder: "folder", child: "folder/child.md", other: "other.md",
            ],
            rangeAnchor: folder,
            rangeAnchorPathSnapshot: "folder")

        XCTAssertEqual(
            FileTreeSidebar.dragPreviewCount(
                for: rows[0], from: model, visibleRows: rows),
            3)
        XCTAssertEqual(
            FileTreeSidebar.dragPreviewCount(
                for: rows[3], from: model, visibleRows: rows),
            1)
    }

    func testUnselectedOrReusedIDDragOriginProjectsOnlyLiveOrigin() {
        let selected = fileRow("selected.md")
        let unselected = fileRow("unselected.md")
        let reused: FileTreeSidebar.RowID = .node(.dir(42))
        let rows = [
            FileTreeSidebar.SelectionRow(
                identity: selected, path: "selected.md", isDirectory: false),
            FileTreeSidebar.SelectionRow(
                identity: unselected, path: "unselected.md", isDirectory: false),
            FileTreeSidebar.SelectionRow(
                identity: reused, path: "replacement", isDirectory: true),
        ]
        let model = FileTreeSidebar.SelectionModel(
            focused: selected,
            selected: [selected, reused],
            selectionPathSnapshots: [selected: "selected.md", reused: "old-folder"],
            rangeAnchor: selected,
            rangeAnchorPathSnapshot: "selected.md")

        XCTAssertEqual(
            FileTreeSidebar.dragItems(
                for: rows[1], from: model, visibleRows: rows).map(\.path),
            ["unselected.md"])
        XCTAssertEqual(
            FileTreeSidebar.dragItems(
                for: rows[2], from: model, visibleRows: rows).map(\.path),
            ["replacement"],
            "a reused directory ID with a path mismatch is not selected")
    }
}
