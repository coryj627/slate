// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Combine
import SwiftUI
import XCTest

@testable import SlateMac

/// A host swap of the editor's buffer (a reload from disk, a rescan's
/// in-place reload of a clean note) is a new baseline: Undo never replays
/// typing from before the swap into the new bytes (plan 43 PR 19, #1333).
/// These facts mount the real SwiftUI hierarchy, as the ⌘E integration
/// facts do, so SwiftUI itself runs `updateNSView` when the bound text
/// changes, exactly as a reload reaches the editor.
@MainActor
final class EditorReloadUndoTests: XCTestCase {

    private final class Buffer: ObservableObject {
        @Published var text: String
        init(_ text: String) { self.text = text }
    }

    private struct Host: View {
        @ObservedObject var buffer: Buffer
        let scroll = PassthroughSubject<String, Never>().eraseToAnyPublisher()
        let line = PassthroughSubject<Int, Never>().eraseToAnyPublisher()
        let offset = PassthroughSubject<Int, Never>().eraseToAnyPublisher()

        var body: some View {
            NoteEditorView(
                text: $buffer.text,
                headings: [],
                accessibilityLabel: "Test editor",
                onSave: {},
                scrollAnchorRequest: scroll,
                lineScrollRequest: line,
                cursorByteOffsetRequest: offset,
                previewEmbedAtCursor: nil
            )
        }
    }

    private var window: NSWindow!

    override func setUp() {
        super.setUp()
        window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 800, height: 600),
            styleMask: [.titled, .resizable],
            backing: .buffered,
            defer: false
        )
        window.isReleasedWhenClosed = false
    }

    override func tearDown() {
        window.orderOut(nil)
        window = nil
        super.tearDown()
    }

    private func mountEditor(_ buffer: Buffer) throws -> SlateEditorTextView {
        let hosting = NSHostingView(rootView: Host(buffer: buffer))
        hosting.frame = NSRect(x: 0, y: 0, width: 800, height: 600)
        window.contentView = hosting
        window.makeKeyAndOrderFront(nil)
        for _ in 0..<40 {
            if let textView = Self.findTextView(in: hosting) { return textView }
            RunLoop.main.run(until: Date(timeIntervalSinceNow: 0.05))
        }
        if let textView = Self.findTextView(in: hosting) { return textView }
        throw XCTSkip("SlateEditorTextView never materialized in the hosting hierarchy")
    }

    private static func findTextView(in view: NSView) -> SlateEditorTextView? {
        if let textView = view as? SlateEditorTextView { return textView }
        for sub in view.subviews {
            if let found = findTextView(in: sub) { return found }
        }
        return nil
    }

    /// Pump the run loop until `condition` holds (SwiftUI's update pass),
    /// or for two seconds.
    private func pump(until condition: () -> Bool) {
        for _ in 0..<40 where !condition() {
            RunLoop.main.run(until: Date(timeIntervalSinceNow: 0.05))
        }
    }

    /// Type through the text view itself, as a keystroke does, and close
    /// the undo group so the edit is one undoable step.
    private func type(_ string: String, at location: Int, into textView: SlateEditorTextView) {
        window.makeFirstResponder(textView)
        textView.setSelectedRange(NSRange(location: location, length: 0))
        textView.insertText(string, replacementRange: textView.selectedRange())
        textView.breakUndoCoalescing()
    }

    func testUndoAfterTheBufferIsReloadedNeverReplaysEarlierTypingIntoTheNewBytes() throws {
        let buffer = Buffer("hello world\n")
        let textView = try mountEditor(buffer)
        type(" there", at: 5, into: textView)
        XCTAssertEqual(buffer.text, "hello there world\n", "the typing reached the binding")
        XCTAssertEqual(textView.undoManager?.canUndo, true, "the typing is undoable")

        // The reload: new bytes from disk replace the buffer from outside.
        let fromDisk = "completely different bytes from disk\n"
        buffer.text = fromDisk
        pump { textView.string == fromDisk }
        XCTAssertEqual(textView.string, fromDisk, "the editor took the reloaded bytes")

        textView.undoManager?.undo()
        pump { buffer.text != fromDisk }
        XCTAssertEqual(
            textView.string, fromDisk,
            "Undo replayed typing from before the reload into the new bytes")
        XCTAssertEqual(buffer.text, fromDisk, "the binding still holds the reloaded bytes")
    }

    func testTypingAfterAReloadIsStillUndoable() throws {
        let buffer = Buffer("first\n")
        let textView = try mountEditor(buffer)
        type("x", at: 0, into: textView)

        let fromDisk = "second version\n"
        buffer.text = fromDisk
        pump { textView.string == fromDisk }

        type("y", at: 0, into: textView)
        XCTAssertEqual(textView.string, "ysecond version\n")
        XCTAssertEqual(buffer.text, "ysecond version\n")
        textView.undoManager?.undo()
        pump { buffer.text == fromDisk }
        XCTAssertEqual(textView.string, fromDisk, "the edit made after the reload undoes")
        XCTAssertEqual(buffer.text, fromDisk)
    }
}
