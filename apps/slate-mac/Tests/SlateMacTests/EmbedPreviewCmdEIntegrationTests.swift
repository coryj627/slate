// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Combine
import SwiftUI
import XCTest

@testable import SlateMac

/// Full-stack integration tests for the Cmd+E embed-preview path
/// ([#412](https://github.com/coryj627/slate/issues/412)).
///
/// The 2026-06-10 VO feature test found Cmd+E always announcing
/// "No embed at cursor." with the caret verifiably inside an
/// `![[…]]` embed. The existing coordinator-level unit test passes,
/// so these tests mount the REAL SwiftUI hierarchy — `NoteEditorView`
/// inside an `NSHostingView` inside a key-capable `NSWindow`, letting
/// SwiftUI run `makeNSView`/`updateNSView` itself — position the
/// caret, and send a synthetic ⌘E key equivalent through the window,
/// exactly as AppKit would route a real keypress.
@MainActor
final class EmbedPreviewCmdEIntegrationTests: XCTestCase {

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

    /// Mount a NoteEditorView bound to `text` and pump the run loop
    /// until the underlying SlateEditorTextView materializes.
    private func mountEditor(
        text: Binding<String>,
        previewEmbedAtCursor: @escaping (String, Int) -> Void
    ) throws -> SlateEditorTextView {
        let editor = NoteEditorView(
            text: text,
            headings: [],
            accessibilityLabel: "Test editor",
            onSave: {},
            scrollAnchorRequest: PassthroughSubject<String, Never>().eraseToAnyPublisher(),
            lineScrollRequest: PassthroughSubject<Int, Never>().eraseToAnyPublisher(),
            cursorByteOffsetRequest: PassthroughSubject<Int, Never>().eraseToAnyPublisher(),
            previewEmbedAtCursor: previewEmbedAtCursor
        )
        let hosting = NSHostingView(rootView: editor)
        hosting.frame = NSRect(x: 0, y: 0, width: 800, height: 600)
        window.contentView = hosting
        window.makeKeyAndOrderFront(nil)

        // Let SwiftUI materialize the NSViewRepresentable. A few
        // run-loop turns are enough; bail out with a clear failure
        // if the text view never appears.
        for _ in 0..<40 {
            if let tv = Self.findTextView(in: hosting) { return tv }
            RunLoop.main.run(until: Date(timeIntervalSinceNow: 0.05))
        }
        if let tv = Self.findTextView(in: hosting) { return tv }
        throw XCTSkip("SlateEditorTextView never materialized in the hosting hierarchy")
    }

    private static func findTextView(in view: NSView) -> SlateEditorTextView? {
        if let tv = view as? SlateEditorTextView { return tv }
        for sub in view.subviews {
            if let found = findTextView(in: sub) { return found }
        }
        return nil
    }

    private func cmdEEvent(for window: NSWindow) -> NSEvent {
        NSEvent.keyEvent(
            with: .keyDown,
            location: .zero,
            modifierFlags: [.command],
            timestamp: ProcessInfo.processInfo.systemUptime,
            windowNumber: window.windowNumber,
            context: nil,
            characters: "e",
            charactersIgnoringModifiers: "e",
            isARepeat: false,
            keyCode: 14
        )!
    }

    /// The live failure shape from the VO test: caret inside
    /// `![[Whipped cream]]`, ⌘E sent to the window. The preview
    /// callback must fire with the embed target — not fall through
    /// to "No embed at cursor."
    func testCmdEThroughWindowFindsEmbedUnderCaret() throws {
        let content = "intro line\nsee ![[Whipped cream]] for the topping\nmore\n"
        var bound = content
        var captured: (target: String, line: Int)?
        let binding = Binding<String>(
            get: { bound }, set: { bound = $0 }
        )
        let textView = try mountEditor(text: binding) { target, line in
            captured = (target, line)
        }

        // Caret inside the embed target (offset of "Whipped" + 2).
        let ns = content as NSString
        let inside = ns.range(of: "Whipped").location + 2
        window.makeFirstResponder(textView)
        textView.setSelectedRange(NSRange(location: inside, length: 0))

        let handled = window.performKeyEquivalent(with: cmdEEvent(for: window))

        XCTAssertTrue(handled, "the editor must swallow ⌘E (audit #208)")
        XCTAssertEqual(
            captured?.target, "Whipped cream",
            "⌘E with the caret inside ![[Whipped cream]] must open its preview, not report 'No embed at cursor' (#412)"
        )
        XCTAssertEqual(captured?.line, 2, "source line is 1-based")
    }

    /// Maximum-fidelity reproduction of the VO test's exact J-test
    /// procedure: real multi-byte note content (em-dashes, ½, °F
    /// before the embeds), caret parked via the ACCESSIBILITY API
    /// (`setAccessibilitySelectedTextRange`, what the harness's
    /// AXSelectedTextRange write reaches) WITHOUT making the text
    /// view first responder (the harness never click-focused the
    /// editor for this test), then ⌘E through the window.
    func testCmdEAfterAXCaretParkOnUnfocusedEditorWithMultibyteContent() throws {
        let content = """
            # Apple pie

            The serving suggestion — whipped cream — uses ½ cup sugar at 425°F…

            The block-level preview of the same step:

            ![[Whipped cream#^method-step-2]]

            And the full recipe embedded for reference:

            ![[Whipped cream]]
            """
        var bound = content
        var captured: (target: String, line: Int)?
        let binding = Binding<String>(get: { bound }, set: { bound = $0 })
        let textView = try mountEditor(text: binding) { target, line in
            captured = (target, line)
        }

        // Focus deliberately NOT on the text view (live test parked
        // the caret by AX write only). Park inside the final
        // `![[Whipped cream]]` via the accessibility setter.
        let ns = content as NSString
        let embedStart = ns.range(of: "![[Whipped cream]]").location
        textView.setAccessibilitySelectedTextRange(
            NSRange(location: embedStart + 5, length: 0)
        )

        let handled = window.performKeyEquivalent(with: cmdEEvent(for: window))

        XCTAssertTrue(handled, "⌘E must reach the editor even when it is not first responder")
        XCTAssertEqual(
            captured?.target, "Whipped cream",
            "AX-parked caret inside ![[Whipped cream]] + ⌘E must open the preview (#412 live shape)"
        )
    }

    /// Same path with the caret on a markdown image line — #412
    /// also asks for an explicit scope decision. Wikilink embeds are
    /// the Cmd+E surface today (`findEditorEmbedSpans` documents the
    /// markdown-image gap); this test pins the announced fallback
    /// rather than a crash or a bogus match.
    func testCmdEOnMarkdownImageLineFallsThroughWithoutCrash() throws {
        let content = "![alt text](attachments/pie.svg)\n![[Real embed]]\n"
        var bound = content
        var captured: (target: String, line: Int)?
        let binding = Binding<String>(get: { bound }, set: { bound = $0 })
        let textView = try mountEditor(text: binding) { target, line in
            captured = (target, line)
        }

        let ns = content as NSString
        let onImage = ns.range(of: "alt text").location + 1
        window.makeFirstResponder(textView)
        textView.setSelectedRange(NSRange(location: onImage, length: 0))

        let handled = window.performKeyEquivalent(with: cmdEEvent(for: window))

        XCTAssertTrue(handled, "⌘E is always swallowed by the editor")
        XCTAssertNil(
            captured,
            "markdown images are out of Cmd+E scope today — must fall through to the announcement, not match"
        )
    }
}
