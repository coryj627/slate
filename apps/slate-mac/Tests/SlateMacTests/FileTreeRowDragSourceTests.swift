// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import XCTest

@testable import SlateMac

@MainActor
final class FileTreeRowDragSourceTests: XCTestCase {
    /// `NSDraggingSession()` has no CoreDrag handle under XCTest; retaining the
    /// recording subclass for the process lifetime avoids AppKit disposing an
    /// invalid test-only handle after the assertion completes.
    private static var retainedSessions: [NSDraggingSession] = []

    private static func source(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Sources/SlateMac")
        return try String(
            contentsOf: root.appendingPathComponent(relativePath),
            encoding: .utf8)
    }

    private func file(_ path: String) -> FileTreeSidebar.RowID {
        .node(.file(path: path))
    }

    func testDescriptorCreatesOnePublicPasteboardItemPerSelectedURLAndOnePrivateLeader()
        throws
    {
        let a = file("a.md")
        let folderID = NodeID.dir(42)
        let folder = FileTreeSidebar.RowID.node(folderID)
        let b = file("nested/b.md")
        let rows = [
            FileTreeSidebar.SelectionRow(
                identity: a, path: "a.md", isDirectory: false),
            FileTreeSidebar.SelectionRow(
                identity: folder, path: "folder", isDirectory: true),
            FileTreeSidebar.SelectionRow(
                identity: b, path: "nested/b.md", isDirectory: false),
        ]
        let model = FileTreeSidebar.SelectionModel(
            focused: b,
            selected: [folder, b, a],
            selectionPathSnapshots: [
                a: "a.md", folder: "folder", b: "nested/b.md",
            ],
            rangeAnchor: a,
            rangeAnchorPathSnapshot: "a.md")

        let descriptor = try XCTUnwrap(
            FileTreeSidebar.makeRowDragDescriptor(
                origin: rows[2],
                from: model,
                visibleRows: rows,
                vaultURL: URL(fileURLWithPath: "/Vaults/demo")))
        XCTAssertEqual(
            descriptor.fileURLs.map(\.path),
            [
                "/Vaults/demo/a.md", "/Vaults/demo/folder",
                "/Vaults/demo/nested/b.md",
            ])
        XCTAssertEqual(descriptor.directoryFlags, [false, true, false])
        XCTAssertEqual(descriptor.leaderIndex, 2, "the initiating row leads the pile")

        let items = descriptor.makePasteboardItems(
            privateType: FileTreeSidebar.nodeUTType)
        XCTAssertEqual(items.count, 3)
        let privateType = NSPasteboard.PasteboardType(FileTreeSidebar.nodeUTType)
        for item in items {
            XCTAssertTrue(item.types.contains(.fileURL))
            XCTAssertTrue(item.types.contains(.URL))
        }
        XCTAssertEqual(
            items.filter { $0.types.contains(privateType) }.count,
            1,
            "only the leader carries the ordered private envelope")
        let payload = try XCTUnwrap(items[2].data(forType: privateType))
        XCTAssertEqual(
            FileTreeSidebar.decodeDragPayload(payload)?.map(\.path),
            ["a.md", "folder", "nested/b.md"])
        let object = try XCTUnwrap(
            JSONSerialization.jsonObject(with: payload) as? [String: Any])
        XCTAssertEqual(object["preferredFocusPath"] as? String, "nested/b.md")

        XCTAssertNil(
            FileTreeSidebar.makeRowDragDescriptor(
                origin: rows[2], from: model, visibleRows: rows, vaultURL: nil),
            "a multi-item bridge fails closed instead of emitting partial URL-only items")
    }

    func testLiveSessionUsesInitiatingCAsLeaderForRichPreviewAndPrivateEnvelope()
        throws
    {
        let a = file("a.md")
        let b = file("b.md")
        let c = file("c.md")
        let rows = [
            FileTreeSidebar.SelectionRow(identity: a, path: "a.md", isDirectory: false),
            FileTreeSidebar.SelectionRow(identity: b, path: "b.md", isDirectory: false),
            FileTreeSidebar.SelectionRow(identity: c, path: "c.md", isDirectory: false),
        ]
        let model = FileTreeSidebar.SelectionModel(
            focused: c,
            selected: [a, b, c],
            selectionPathSnapshots: [a: "a.md", b: "b.md", c: "c.md"],
            rangeAnchor: a,
            rangeAnchorPathSnapshot: "a.md")
        var descriptor = try XCTUnwrap(
            FileTreeSidebar.makeRowDragDescriptor(
                origin: rows[2],
                from: model,
                visibleRows: rows,
                vaultURL: URL(fileURLWithPath: "/Vaults/demo")))
        descriptor.leaderImage = NSImage(size: NSSize(width: 96, height: 42))

        let session = DragSessionSpy()
        Self.retainedSessions.append(session)
        let view = DragSessionSpyView(
            frame: NSRect(x: 0, y: 0, width: 120, height: 40),
            session: session)
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 120, height: 40),
            styleMask: .borderless,
            backing: .buffered,
            defer: false)
        window.contentView = view
        let coordinator = FileTreeRowDragSource.Coordinator(
            makeDescriptor: { descriptor },
            onClick: { _ in XCTFail("crossing the threshold must not click") },
            onDragEnded: {})
        view.coordinator = coordinator
        coordinator.attach(to: view)
        defer {
            coordinator.detach()
            window.contentView = nil
        }

        NSApp.sendEvent(
            try mouseEvent(
                .leftMouseDown, in: window, location: NSPoint(x: 10, y: 10)))
        NSApp.sendEvent(
            try mouseEvent(
                .leftMouseDragged, in: window, location: NSPoint(x: 13, y: 10)))

        let items = try XCTUnwrap(view.capturedItems)
        XCTAssertEqual(items.count, 3)
        XCTAssertEqual(session.draggingLeaderIndex, 2, "C must lead the live AppKit pile")
        XCTAssertEqual(
            items.map(\.draggingFrame.size),
            [
                NSSize(width: 24, height: 24),
                NSSize(width: 24, height: 24),
                NSSize(width: 96, height: 42),
            ],
            "only C uses the rendered three-item count preview")

        let privateType = NSPasteboard.PasteboardType(FileTreeSidebar.nodeUTType)
        let writers = try items.map {
            try XCTUnwrap($0.item as? NSPasteboardItem)
        }
        XCTAssertEqual(
            writers.indices.filter { writers[$0].types.contains(privateType) },
            [2],
            "the live leader alone carries the private batch envelope")
        let payload = try XCTUnwrap(writers[2].data(forType: privateType))
        XCTAssertEqual(
            FileTreeSidebar.decodeDragPayload(payload)?.map(\.path),
            ["a.md", "b.md", "c.md"])
        let object = try XCTUnwrap(
            JSONSerialization.jsonObject(with: payload) as? [String: Any])
        XCTAssertEqual(object["preferredFocusPath"] as? String, "c.md")
    }

    func testGestureStatePreservesClickModifiersAndBeginsOneDragAtThreePoints() {
        var state = FileTreeRowDragGestureState()
        state.mouseDown(at: .zero, modifiers: [.command])
        XCTAssertFalse(state.mouseDragged(to: CGPoint(x: 2.9, y: 0)))
        XCTAssertTrue(state.mouseDragged(to: CGPoint(x: 3, y: 0)))
        XCTAssertFalse(state.mouseDragged(to: CGPoint(x: 20, y: 0)))
        XCTAssertEqual(state.mouseUp(inside: true), .none, "a drag never also clicks")

        state.mouseDown(at: CGPoint(x: 5, y: 5), modifiers: [.shift])
        XCTAssertEqual(state.mouseUp(inside: true), .click([.shift]))

        state.mouseDown(at: CGPoint(x: 5, y: 5), modifiers: [])
        XCTAssertEqual(state.mouseUp(inside: false), .none, "outside release does not click")
    }

    func testDragOperationIsMoveInsideAndCopyOutside() {
        XCTAssertEqual(
            FileTreeRowDragSource.operation(for: .withinApplication), .move)
        XCTAssertEqual(
            FileTreeRowDragSource.operation(for: .outsideApplication), .copy)
    }

    func testSidebarRowsMountAppKitBridgeAndRetireSwiftUIDragGesture() throws {
        let sidebar = try Self.source("FileTreeSidebar.swift")
        let bridge = try Self.source("Sidebar/FileTreeRowDragSource.swift")

        XCTAssertEqual(
            sidebar.components(separatedBy: "FileTreeRowDragSource(").count - 1,
            2,
            "file and folder rows each mount the full-row bridge")
        XCTAssertFalse(
            sidebar.contains(".onDrag { dragItem(for: node) }"),
            "SwiftUI's single-provider drag recognizer must no longer collapse selection")
        XCTAssertFalse(
            sidebar.contains(".onTapGesture {\n                let click"),
            "the bridge owns click-versus-drag instead of racing a second tap recognizer")
        XCTAssertTrue(sidebar.contains("makeDescriptor: { dragDescriptor(for: node) }"))
        XCTAssertTrue(sidebar.contains("fileTreeFocused = true"))
        XCTAssertTrue(sidebar.contains("Self.selectionClick(from: modifiers)"))
        XCTAssertTrue(sidebar.contains("onDragEnded: { endDragSession(dropDestination: nil) }"))

        XCTAssertTrue(
            bridge.contains(
                "matching: [.leftMouseDown, .leftMouseDragged, .leftMouseUp]"))
        XCTAssertTrue(
            bridge.contains("!event.modifierFlags.contains(.control)"),
            "Control-click must continue to SwiftUI's context menu")
        XCTAssertTrue(bridge.contains("view.beginDraggingSession("))
        XCTAssertTrue(bridge.contains("session.draggingFormation = .pile"))
        XCTAssertTrue(bridge.contains("session.animatesToStartingPositionsOnCancelOrFail = true"))
    }

    func testDefaultAccessibilityActivationUsesFrozenOpenWithoutChangingPlainPointer() throws {
        let source = try Self.source("FileTreeSidebar.swift")

        func compactBody(from start: String, to end: String) throws -> String {
            let startRange = try XCTUnwrap(source.range(of: start), start)
            let tail = source[startRange.lowerBound...]
            let endRange = try XCTUnwrap(tail.range(of: end), end)
            return tail[..<endRange.lowerBound]
                .split(whereSeparator: { $0.isWhitespace })
                .joined(separator: " ")
        }

        let folder = try compactBody(
            from: "private func folderRow(", to: "private func fileRow(")
        XCTAssertTrue(
            folder.contains(
                "let activate = { fileTreeFocused = true applyPlainSelection(.node(node.nodeID)) tree.toggle(node) }"),
            "folder activation must focus, plain-select, then toggle disclosure")
        XCTAssertTrue(
            folder.contains("if click == .plain { activate() }"),
            "plain pointer activation must use the shared folder closure")
        XCTAssertTrue(
            folder.contains(".accessibilityAction(.default) { activate() }"),
            "AXPress/VoiceOver-Space must use that same folder closure")
        XCTAssertTrue(
            folder.contains(
                ".accessibilityAction(named: Text(isExpanded ? \"Collapse\" : \"Expand\")) { tree.toggle(node) }"),
            "the explicit disclosure rotor action remains independently available")

        let file = try compactBody(
            from: "private func fileRow(", to: "// MARK: - Inline rename")
        XCTAssertTrue(
            file.contains(
                "let activate = { fileTreeFocused = true applyPlainSelection(.node(node.nodeID)) }"),
            "file activation must focus and plain-select so the existing selection observer opens it")
        XCTAssertTrue(
            file.contains("if click == .plain { activate() }"),
            "plain pointer activation must use the shared file closure")
        XCTAssertTrue(
            file.contains(
                "openEvaluation: voiceOverProjection?.openEvaluation"),
            "VoiceOver Open must derive from the same frozen catalog projection")
        XCTAssertTrue(
            file.contains("FileRowOpenAccessibilityModifier("),
            "the file row must attach its conditional shared-catalog Open action")
        XCTAssertTrue(
            file.contains("_ = try appState.dispatchSidebarAction(openIntent)"),
            "AXPress/VoiceOver-Space must dispatch the frozen Open intent directly")

        let modifier = try compactBody(
            from: "private struct FileRowOpenAccessibilityModifier",
            to: "static func invokeSidebarKeyboardAction")
        XCTAssertTrue(
            modifier.contains(
                ".accessibilityAction(.default) { dispatch(openIntent) }"),
            "the conditional default action must dispatch exactly its retained intent")
    }

    private func mouseEvent(
        _ type: NSEvent.EventType,
        in window: NSWindow,
        location: NSPoint
    ) throws -> NSEvent {
        try XCTUnwrap(
            NSEvent.mouseEvent(
                with: type,
                location: location,
                modifierFlags: [],
                timestamp: 0,
                windowNumber: window.windowNumber,
                context: nil,
                eventNumber: 1,
                clickCount: 1,
                pressure: 1))
    }

    private final class DragSessionSpyView: FileTreeRowDragSource.TrackingView {
        let session: NSDraggingSession
        private(set) var capturedItems: [NSDraggingItem]?

        init(frame: NSRect, session: NSDraggingSession) {
            self.session = session
            super.init(frame: frame)
        }

        required init?(coder: NSCoder) {
            fatalError("init(coder:) has not been implemented")
        }

        override func beginDraggingSession(
            with items: [NSDraggingItem],
            event: NSEvent,
            source: NSDraggingSource
        ) -> NSDraggingSession {
            capturedItems = items
            return session
        }
    }

    private final class DragSessionSpy: NSDraggingSession {
        private var storedLeaderIndex = -1
        private var storedFormation: NSDraggingFormation = .none
        private var storedAnimatesToStart = false

        override var draggingLeaderIndex: Int {
            get { storedLeaderIndex }
            set { storedLeaderIndex = newValue }
        }

        override var draggingFormation: NSDraggingFormation {
            get { storedFormation }
            set { storedFormation = newValue }
        }

        override var animatesToStartingPositionsOnCancelOrFail: Bool {
            get { storedAnimatesToStart }
            set { storedAnimatesToStart = newValue }
        }
    }
}
