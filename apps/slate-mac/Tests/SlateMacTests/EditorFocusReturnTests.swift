// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// #1472: where keyboard focus lands after an activation in the editor.
///
/// These mount the real SwiftUI hierarchy — `WorkspaceView`, with the tab
/// strip's buttons and the note content pane — in a key window, focus the
/// editor, and drive the activation through the window as AppKit routes a
/// keypress. The 2026-10-17 field pass found that a link followed in the
/// current tab left keyboard focus on the tab strip's All tabs button,
/// and that closing the editor's citation popover did not return to the
/// editor.
@MainActor
final class EditorFocusReturnTests: XCTestCase {

    private var window: NSWindow!
    private var tempDirs: [URL] = []
    private let announcer = EditorActivationTests.RecordingAnnouncer()

    override func setUp() {
        super.setUp()
        window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 900, height: 640),
            styleMask: [.titled, .resizable],
            backing: .buffered,
            defer: false)
        window.isReleasedWhenClosed = false
    }

    override func tearDown() {
        window.orderOut(nil)
        window = nil
        for dir in tempDirs { try? FileManager.default.removeItem(at: dir) }
        tempDirs.removeAll()
        super.tearDown()
    }

    private static let source = """
        # Source

        See [[target]] here.

        A citation [@doe] here.

        """

    private static let target = """
        # Target

        Target body.

        ## Later

        More.

        """

    private func openSource() async throws -> AppState {
        let dir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-editor-focus-\(UUID().uuidString)")
        tempDirs.append(dir)
        let vault = dir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data(Self.source.utf8).write(to: vault.appendingPathComponent("source.md"))
        try Data(Self.target.utf8).write(to: vault.appendingPathComponent("target.md"))
        try Data("# Other\n\nOther body.\n".utf8).write(to: vault.appendingPathComponent("other.md"))
        let state = AppState(
            recentsStore: RecentVaultsStore(fileURL: dir.appendingPathComponent("recents.json")),
            preferencesStore: PreferencesStore(defaults: UserDefaults(suiteName: UUID().uuidString)!),
            announcer: announcer)
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "source.md"
        await state.noteLoadTask?.value
        await state.linksLoadTask?.value
        await state.citationsLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "source.md")
        return state
    }

    private func host(_ state: AppState) -> NSView {
        let hosting = NSHostingView(rootView: WorkspaceView().environmentObject(state))
        hosting.frame = NSRect(x: 0, y: 0, width: 900, height: 640)
        window.contentView = hosting
        window.makeKeyAndOrderFront(nil)
        return hosting
    }

    private static func editors(in view: NSView) -> [SlateEditorTextView] {
        if let editor = view as? SlateEditorTextView { return [editor] }
        return view.subviews.flatMap { editors(in: $0) }
    }

    /// Suspend (so the main actor's queued work and SwiftUI's updates run)
    /// until `condition` holds, or fail after `timeout`.
    private func waitUntil(
        _ what: String, timeout: Duration = .seconds(5), _ condition: () -> Bool
    ) async {
        let deadline = ContinuousClock.now.advanced(by: timeout)
        while !condition(), ContinuousClock.now < deadline {
            try? await Task.sleep(for: .milliseconds(10))
        }
        XCTAssertTrue(condition(), "timed out waiting for \(what)")
    }

    /// The editor in `hosting` whose text contains `needle`, once mounted.
    private func editor(in hosting: NSView, showing needle: String) async -> SlateEditorTextView? {
        var found: SlateEditorTextView?
        await waitUntil("an editor showing \(needle)") {
            found = Self.editors(in: hosting).first { $0.string.contains(needle) }
            return found != nil
        }
        return found
    }

    private func commandReturn() -> NSEvent {
        NSEvent.keyEvent(
            with: .keyDown, location: .zero, modifierFlags: .command, timestamp: 0,
            windowNumber: window.windowNumber, context: nil, characters: "\r",
            charactersIgnoringModifiers: "\r", isARepeat: false, keyCode: 36)!
    }

    /// ⌘↩ on a wikilink opens its note in the current tab, and keyboard
    /// focus is in the opened note's editor, with the caret at its start —
    /// as the template flow's caret park leaves it (`placeCursorAtByteOffset`).
    /// It is the same text view: the editor never left the window while the
    /// note loaded, so VoiceOver had no vanished element to recover from.
    /// An anchor scroll afterwards places its own caret.
    func testFollowingALinkInTheCurrentTabLeavesTheOpenedNotesEditorFocusedAtItsStart()
        async throws
    {
        let state = try await openSource()
        let hosting = host(state)
        let maybeSource = await editor(in: hosting, showing: "See [[target]]")
        let source = try XCTUnwrap(maybeSource)
        XCTAssertTrue(window.makeFirstResponder(source))
        let link = (source.string as NSString).range(of: "[[target]]")
        source.setSelectedRange(NSRange(location: link.location + 2, length: 0))

        XCTAssertTrue(window.performKeyEquivalent(with: commandReturn()))
        await waitUntil("the target to load") {
            state.loadedFilePath == "target.md" && !state.isLoadingNote
        }
        let maybeLanded = await editor(in: hosting, showing: "Target body.")
        let landed = try XCTUnwrap(maybeLanded)
        await waitUntil("the opened editor to hold keyboard focus") {
            self.window.firstResponder === landed
        }
        XCTAssertTrue(
            window.firstResponder === landed,
            "keyboard focus: \(String(describing: window.firstResponder))")
        XCTAssertEqual(landed.selectedRange(), NSRange(location: 0, length: 0))
        XCTAssertTrue(landed === source, "the editor stayed in the window through the load")
        await waitUntil("the claim to be spent") { state.editorFocusClaim == nil }

        let later = try XCTUnwrap(state.currentNoteHeadings.first { $0.text == "Later" })
        state.requestScrollToHeading(anchor: later.anchorId)
        let heading = (landed.string as NSString).range(of: "## Later").location
        await waitUntil("the anchor scroll's caret") { landed.selectedRange().location == heading }
        XCTAssertTrue(window.firstResponder === landed)
    }

    /// A link into a note already open in another tab switches to that tab
    /// (the selection funnel's tab switch), and keyboard focus is in its
    /// editor there too.
    func testFollowingALinkIntoANoteOpenInAnotherTabFocusesThatTabsEditor() async throws {
        let state = try await openSource()
        let sourceTab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        state.openFile("target.md", target: .newTab)
        await state.noteLoadTask?.value
        state.activateTab(sourceTab)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "source.md")
        let hosting = host(state)
        let maybeSource = await editor(in: hosting, showing: "See [[target]]")
        let source = try XCTUnwrap(maybeSource)
        XCTAssertTrue(window.makeFirstResponder(source))
        let link = (source.string as NSString).range(of: "[[target]]")
        source.setSelectedRange(NSRange(location: link.location + 2, length: 0))

        XCTAssertTrue(window.performKeyEquivalent(with: commandReturn()))
        await waitUntil("the target's tab") {
            state.loadedFilePath == "target.md"
                && state.workspace.model.activeGroup.activeTabID != sourceTab
        }
        let maybeLanded = await editor(in: hosting, showing: "Target body.")
        let landed = try XCTUnwrap(maybeLanded)
        await waitUntil("its editor to hold keyboard focus") {
            self.window.firstResponder === landed
        }
        await waitUntil("the claim to be spent") { state.editorFocusClaim == nil }
    }

    /// #1378: a search result opened into the current tab leaves keyboard
    /// focus in the opened note's editor, at the hit's line. The overlay's
    /// row leaves the window with the overlay, and before this nothing
    /// claimed focus after it.
    func testASearchResultOpenedIntoTheCurrentTabFocusesItsEditorAtTheHit() async throws {
        let state = try await openSource()
        let hosting = host(state)
        let maybeSource = await editor(in: hosting, showing: "See [[target]]")
        _ = try XCTUnwrap(maybeSource)
        window.makeFirstResponder(nil)  // the overlay held the keyboard
        state.searchQuery = "More"
        state.openSearchResult(QueryHit(path: "target.md", snippet: "\u{2}More\u{3}.", score: 0))

        let maybeLanded = await editor(in: hosting, showing: "Target body.")
        let landed = try XCTUnwrap(maybeLanded)
        await waitUntil("the hit's line scroll") { state.lastActivatedSearchResultPath == "target.md" }
        await waitUntil("the editor to hold keyboard focus") {
            self.window.firstResponder === landed
        }
        let hitLine = (landed.string as NSString).range(of: "More.").location
        await waitUntil("the caret at the hit's line") {
            landed.selectedRange().location == hitLine
        }
        await waitUntil("the claim to be spent") { state.editorFocusClaim == nil }
    }

    /// A claim belongs to the note it was made for: choosing another note
    /// before it lands ends it, and that note's editor claims nothing.
    func testAClaimEndsWhenAnotherNoteIsChosenBeforeItLands() async throws {
        let state = try await openSource()
        state.claimEditorFocus(for: "target.md", caretByte: 0, keepsEditorThroughLoad: true)
        state.selectedFilePath = "other.md"
        XCTAssertNil(state.editorFocusClaim)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "other.md")
        XCTAssertNil(state.editorFocusClaim)
    }

    /// The citation popover opened from the editor closes (Escape, Close)
    /// back into the editor, with the caret where it was.
    func testClosingTheEditorsCitationReturnsFocusToTheEditorAtTheCitation() async throws {
        let state = try await openSource()
        let hosting = host(state)
        let maybeSource = await editor(in: hosting, showing: "[@doe]")
        let source = try XCTUnwrap(maybeSource)
        XCTAssertTrue(window.makeFirstResponder(source))
        let citation = (source.string as NSString).range(of: "[@doe]")
        let caret = NSRange(location: citation.location + 2, length: 0)
        source.setSelectedRange(caret)

        XCTAssertTrue(window.performKeyEquivalent(with: commandReturn()))
        await waitUntil("the citation popover") { state.expandedCitation != nil }
        // The popover is a sheet that takes the keyboard; when it closes,
        // nothing in this window holds focus until something claims it.
        window.makeFirstResponder(nil)
        source.setSelectedRange(NSRange(location: 0, length: 0))

        XCTAssertTrue(state.closeExpandedCitation(), "what the sheet's Escape and Close run")

        await waitUntil("the editor to hold keyboard focus again") {
            self.window.firstResponder === source
        }
        XCTAssertEqual(source.selectedRange(), caret, "the caret is back at the citation")
    }
}
