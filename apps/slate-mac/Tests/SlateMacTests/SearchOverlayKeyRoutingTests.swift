// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import XCTest

@testable import SlateMac

/// #1113: the vault-search overlay's keyboard contract, converged on the
/// Windows overlay (`MainWindow.Search.cs` `HandleSearchOverlayKey` and
/// `RestoreFocusAfterSearch`; contract 29 SD-1 and SD-2; plan 41 D2).
///
/// The key decisions are a pure function (`SearchOverlay.route`), pinned
/// here without a live event loop, the palette's `shouldPassThroughArrow`
/// shape. The focus restore is AppState's, driven through a real AppKit
/// window whose two text views stand in for where the user was (the
/// editor) and the overlay's field.
@MainActor
final class SearchOverlayKeyRoutingTests: XCTestCase {
    private typealias Route = SearchOverlay.KeyRoute

    private static let down = SearchOverlay.downArrowKeyCode
    private static let up = SearchOverlay.upArrowKeyCode
    private static let returnKey = SearchOverlay.returnKeyCode
    private static let keypadEnter = SearchOverlay.keypadEnterKeyCode

    /// AppKit sets these on every arrow key event itself; a bare arrow
    /// from the hardware is never an empty modifier set.
    private static let arrowNoise: NSEvent.ModifierFlags = [.function, .numericPad]

    private func context(
        focus: SearchOverlay.FocusTarget?,
        rows: SearchOverlay.RowList,
        rowsTakeFocus: Bool = true,
        isInOverlayWindow: Bool = true,
        isCommandPaletteOpen: Bool = false,
        hasMarkedText: Bool = false
    ) -> SearchOverlay.KeyContext {
        SearchOverlay.KeyContext(
            focus: focus,
            rows: rows,
            rowsTakeFocus: rowsTakeFocus,
            isInOverlayWindow: isInOverlayWindow,
            isCommandPaletteOpen: isCommandPaletteOpen,
            hasMarkedText: hasMarkedText)
    }

    private func route(
        _ keyCode: UInt16,
        _ modifiers: NSEvent.ModifierFlags = [],
        focus: SearchOverlay.FocusTarget?,
        rows: SearchOverlay.RowList,
        rowsTakeFocus: Bool = true,
        isInOverlayWindow: Bool = true,
        isCommandPaletteOpen: Bool = false,
        hasMarkedText: Bool = false
    ) -> Route {
        SearchOverlay.route(
            keyCode: keyCode,
            modifierFlags: modifiers,
            in: context(
                focus: focus, rows: rows, rowsTakeFocus: rowsTakeFocus,
                isInOverlayWindow: isInOverlayWindow,
                isCommandPaletteOpen: isCommandPaletteOpen,
                hasMarkedText: hasMarkedText))
    }

    // MARK: - Arrows (SD-1)

    func testDownArrowMovesFocusFromTheFieldToTheFirstResult() {
        XCTAssertEqual(
            route(Self.down, focus: .field, rows: .results(count: 3)),
            .moveFocus(.result(0)),
            "Down on the field moves keyboard focus to the first result row")
        XCTAssertEqual(
            route(Self.down, Self.arrowNoise, focus: .field, rows: .results(count: 3)),
            .moveFocus(.result(0)),
            "the function and numeric-pad flags AppKit puts on every arrow are not modifiers")
        XCTAssertEqual(
            route(Self.down, [.capsLock], focus: .field, rows: .results(count: 1)),
            .moveFocus(.result(0)),
            "Caps Lock is not a modifier either")
    }

    func testArrowsMoveBetweenResultsAndUpReturnsToTheField() {
        let rows = SearchOverlay.RowList.results(count: 3)
        XCTAssertEqual(route(Self.down, focus: .result(0), rows: rows), .moveFocus(.result(1)))
        XCTAssertEqual(route(Self.down, focus: .result(1), rows: rows), .moveFocus(.result(2)))
        XCTAssertEqual(
            route(Self.down, focus: .result(2), rows: rows), .hold,
            "Down on the last row keeps focus there, consumed (the list does not wrap: "
                + "Up from the first row is the way back to the field)")
        XCTAssertEqual(route(Self.up, focus: .result(2), rows: rows), .moveFocus(.result(1)))
        XCTAssertEqual(route(Self.up, focus: .result(1), rows: rows), .moveFocus(.result(0)))
        XCTAssertEqual(
            route(Self.up, Self.arrowNoise, focus: .result(0), rows: rows), .moveFocus(.field),
            "Up from the first row returns focus to the field")
        XCTAssertEqual(
            route(Self.up, Self.arrowNoise, focus: .field, rows: rows), .passThrough,
            "Up on the field keeps the field's own caret movement")
    }

    /// The states that show no rows: searching and an error (`.none`), a
    /// search that found nothing, and the idle state with no recent
    /// searches. The idle state's recent rows are a list too; they have
    /// their own fact below (`testArrowsWalkTheRecentSearchesInTheIdleState`).
    func testArrowsAreIgnoredOutsideTheResultsState() {
        let rowless: [SearchOverlay.RowList] = [.none, .results(count: 0), .recents(count: 0)]
        for rows in rowless {
            for focus in [SearchOverlay.FocusTarget.field, nil] {
                for key in [Self.down, Self.up] {
                    XCTAssertEqual(
                        route(key, Self.arrowNoise, focus: focus, rows: rows), .passThrough,
                        "key \(key) on \(String(describing: focus)) with \(rows) must pass through")
                }
            }
        }
        XCTAssertEqual(
            route(Self.down, focus: .result(1), rows: .none), .passThrough,
            "a row focus left over from a list that is gone (now searching) moves nothing")
        XCTAssertEqual(
            route(Self.down, focus: .recent(0), rows: .results(count: 2)), .passThrough,
            "a recent-row focus does not walk the results list")
    }

    func testControlOptionArrowPassesThroughForQuickNav() {
        let rows = SearchOverlay.RowList.results(count: 3)
        let quickNav: NSEvent.ModifierFlags = [.control, .option]
        for focus in [SearchOverlay.FocusTarget.field, .result(0), .result(1)] {
            for key in [Self.down, Self.up] {
                XCTAssertEqual(
                    route(key, quickNav.union(Self.arrowNoise), focus: focus, rows: rows),
                    .passThrough,
                    "Control-Option-arrow (VoiceOver) MUST pass through on \(focus)")
            }
        }
        for modifiers: NSEvent.ModifierFlags in [.shift, .command, .option, .control] {
            XCTAssertEqual(
                route(Self.down, modifiers.union(Self.arrowNoise), focus: .field, rows: rows),
                .passThrough,
                "a modified arrow (\(modifiers.rawValue)) keeps its text-field meaning")
        }
    }

    /// Design 1's idle arm: the recent searches are focusable rows today
    /// (`.recent(index)`, #876), so they take the same walk.
    func testArrowsWalkTheRecentSearchesInTheIdleState() {
        let rows = SearchOverlay.RowList.recents(count: 2)
        XCTAssertEqual(route(Self.down, focus: .field, rows: rows), .moveFocus(.recent(0)))
        XCTAssertEqual(route(Self.down, focus: .recent(0), rows: rows), .moveFocus(.recent(1)))
        XCTAssertEqual(route(Self.down, focus: .recent(1), rows: rows), .hold)
        XCTAssertEqual(route(Self.up, focus: .recent(1), rows: rows), .moveFocus(.recent(0)))
        XCTAssertEqual(route(Self.up, focus: .recent(0), rows: rows), .moveFocus(.field))
        XCTAssertEqual(
            route(Self.down, focus: .result(0), rows: rows), .passThrough,
            "a result-row focus does not walk the recents")
    }

    /// The arrows move keyboard focus, so they reach the rows exactly
    /// when Tab does. A plain SwiftUI button takes keyboard focus only
    /// with Keyboard navigation (Full Keyboard Access) on, measured while
    /// writing this fact; with it off the arrows stay the field's.
    func testArrowsPassThroughWhenRowsCannotTakeFocus() {
        for rows in [SearchOverlay.RowList.results(count: 3), .recents(count: 2)] {
            XCTAssertEqual(
                route(Self.down, focus: .field, rows: rows, rowsTakeFocus: false),
                .passThrough)
        }
    }

    // MARK: - Return and ⌘Return (D2)

    func testCommandReturnOnAFocusedRowOpensInANewTab() async throws {
        // The monitor claims ⌘Return on a focused result row (D2)…
        for key in [Self.returnKey, Self.keypadEnter] {
            XCTAssertEqual(
                route(key, .command, focus: .result(0), rows: .results(count: 2)),
                .openResult(0))
        }
        XCTAssertEqual(
            route(Self.keypadEnter, [.command, .numericPad, .function], focus: .result(1),
                rows: .results(count: 2)),
            .openResult(1))

        // …and opens it through `openSearchResult`, whose target comes from
        // `openTargetFromCurrentEvent()`: while the monitor runs, the ⌘Return
        // keyDown is NSApp.currentEvent.
        let (state, vault) = try await openVault(files: [
            "a.md": "# A\n\nnothing to see\n",
            "b.md": "# B\n\nfindme target text\n",
        ])
        defer { try? FileManager.default.removeItem(at: vault.deletingLastPathComponent()) }
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value
        let hits = try await search(state, for: "findme")
        let hit = try XCTUnwrap(hits.first)
        XCTAssertEqual(hit.path, "b.md")

        try makeCurrentKeyEvent(keyCode: Self.returnKey, modifiers: .command)
        XCTAssertEqual(state.openTargetFromCurrentEvent(), .newTab)
        state.openSearchResult(hit)
        await state.noteLoadTask?.value

        XCTAssertFalse(state.isSearchOpen, "activation closes the overlay")
        XCTAssertEqual(
            state.workspace.model.allTabs.map(\.item),
            [.markdown(path: "a.md"), .markdown(path: "b.md")],
            "⌘Return opens the row in a new tab and keeps the note the user was in")
        XCTAssertEqual(
            state.workspace.model.activeGroup.activeTab?.item, .markdown(path: "b.md"))
    }

    func testCommandReturnOnTheFieldPassesThrough() {
        for key in [Self.returnKey, Self.keypadEnter] {
            for focus in [SearchOverlay.FocusTarget.field, nil] {
                XCTAssertEqual(
                    route(key, .command, focus: focus, rows: .results(count: 3)),
                    .passThrough,
                    "⌘Return anywhere but a row is not the overlay's (D2)")
                XCTAssertEqual(
                    route(key, .command, focus: focus, rows: .recents(count: 2)),
                    .passThrough)
            }
        }
    }

    func testPlainReturnOnTheFieldStillActivatesTheTopResult() async throws {
        // The monitor leaves Return on the field to the field (its onSubmit)…
        for key in [Self.returnKey, Self.keypadEnter] {
            XCTAssertEqual(
                route(key, focus: .field, rows: .results(count: 2)), .passThrough)
        }
        // …whose submit opens the top-ranked result.
        let (state, vault) = try await openVault(files: [
            "a.md": "# A\n\nfindme once\n",
            "b.md": "# B\n\nfindme findme findme findme\n",
        ])
        defer { try? FileManager.default.removeItem(at: vault.deletingLastPathComponent()) }
        let hits = try await search(state, for: "findme")
        XCTAssertEqual(hits.count, 2)
        let top = try XCTUnwrap(hits.first)

        try makeCurrentKeyEvent(keyCode: Self.returnKey, modifiers: [])
        state.openTopSearchResult()
        await state.noteLoadTask?.value

        XCTAssertFalse(state.isSearchOpen)
        XCTAssertEqual(state.selectedFilePath, top.path, "the top result opened")
        XCTAssertEqual(state.workspace.model.allTabs.count, 1, "in the current tab")
    }

    /// The #422 F-E2 path is unchanged: bare Return (or keypad Enter) on a
    /// focused result row opens it, and on a recent row re-runs it.
    func testReturnOnAFocusedRowStillOpensIt() {
        XCTAssertEqual(
            route(Self.returnKey, focus: .result(2), rows: .results(count: 3)), .openResult(2))
        XCTAssertEqual(
            route(Self.keypadEnter, [.numericPad, .function], focus: .result(0),
                rows: .results(count: 3)),
            .openResult(0),
            "keypad Enter carries the numeric-pad flag; it is not a modifier")
        XCTAssertEqual(
            route(Self.returnKey, [.capsLock], focus: .recent(1), rows: .recents(count: 2)),
            .runRecent(1))
        XCTAssertEqual(
            route(Self.returnKey, focus: .result(3), rows: .results(count: 3)), .passThrough,
            "a row index past the list opens nothing")
    }

    /// D2 covers "a result or recent row": ⌘Return on a recent row is
    /// claimed too, and does what Return does there (a recent search has
    /// no tab to open into).
    func testCommandReturnOnARecentRowRunsTheSearch() {
        XCTAssertEqual(
            route(Self.returnKey, .command, focus: .recent(0), rows: .recents(count: 1)),
            .runRecent(0))
    }

    func testOtherModifiedReturnsPassThrough() {
        for modifiers: NSEvent.ModifierFlags in [
            .shift, .option, .control, [.command, .shift], [.command, .option],
        ] {
            XCTAssertEqual(
                route(Self.returnKey, modifiers, focus: .result(0), rows: .results(count: 1)),
                .passThrough,
                "only bare Return or exactly ⌘Return is claimed (\(modifiers.rawValue))")
        }
    }

    // MARK: - Guards: input method, palette, other windows

    func testAnInputMethodComposingOwnsTheKeys() {
        let rows = SearchOverlay.RowList.results(count: 2)
        XCTAssertEqual(
            route(Self.down, focus: .field, rows: rows, hasMarkedText: true), .passThrough)
        XCTAssertEqual(
            route(Self.returnKey, focus: .result(0), rows: rows, hasMarkedText: true),
            .passThrough)
    }

    /// Red-team F2, kept: the parent window keeps its first responder
    /// while the palette sheet is up, so the overlay's focus can still
    /// read as a row.
    func testKeysPassThroughWhileThePaletteIsOpen() {
        let rows = SearchOverlay.RowList.results(count: 2)
        XCTAssertEqual(
            route(Self.down, focus: .field, rows: rows, isCommandPaletteOpen: true),
            .passThrough)
        XCTAssertEqual(
            route(Self.returnKey, focus: .result(0), rows: rows, isCommandPaletteOpen: true),
            .passThrough)
        XCTAssertEqual(
            route(Self.returnKey, .command, focus: .result(0), rows: rows,
                isCommandPaletteOpen: true),
            .passThrough)
    }

    /// The monitor is app-wide: a key typed in a sheet over the overlay
    /// (Quick Open, a dialog) or in another window is not the overlay's,
    /// even though the overlay's focus still reads as the field or a row.
    func testKeysFromAnotherWindowPassThrough() {
        let rows = SearchOverlay.RowList.results(count: 2)
        XCTAssertEqual(
            route(Self.down, focus: .field, rows: rows, isInOverlayWindow: false),
            .passThrough)
        XCTAssertEqual(
            route(Self.returnKey, focus: .result(0), rows: rows, isInOverlayWindow: false),
            .passThrough)
    }

    func testOtherKeysPassThrough() {
        for keyCode: UInt16 in [0 /* a */, 48 /* Tab */, 49 /* Space */, 53 /* Esc */,
            123 /* ← */, 124 /* → */, 115 /* Home */, 119 /* End */]
        {
            XCTAssertEqual(
                route(keyCode, focus: .result(0), rows: .results(count: 2)), .passThrough)
        }
    }

    // MARK: - Focus restore (SD-2)

    func testEscapeRestoresTheFirstResponderCapturedAtOpen() async throws {
        let (state, _) = makeState()
        let host = FocusHost()
        defer { host.close() }
        state.searchOverlayKeyWindowProvider = { host.window }
        XCTAssertTrue(host.window.makeFirstResponder(host.editor))

        state.toggleSearchOverlay()
        XCTAssertTrue(state.isSearchOpen)
        // The overlay's field takes focus once it is laid out.
        XCTAssertTrue(host.window.makeFirstResponder(host.overlayField))

        // Esc: the close button's shortcut.
        state.dismissSearchOverlay()
        XCTAssertFalse(state.isSearchOpen)
        XCTAssertTrue(
            host.window.firstResponder === host.overlayField,
            "the restore waits for the overlay to leave the view tree")
        host.removeOverlay()
        await nextMainQueueTurn()

        XCTAssertTrue(
            host.window.firstResponder === host.editor,
            "Esc gives focus back to the element that had it when the overlay opened")
    }

    /// The ⇧⌘F toggle closing an open overlay is the same user dismissal.
    func testTheToggleClosingTheOverlayRestoresFocusToo() async throws {
        let (state, _) = makeState()
        let host = FocusHost()
        defer { host.close() }
        state.searchOverlayKeyWindowProvider = { host.window }
        XCTAssertTrue(host.window.makeFirstResponder(host.editor))

        state.toggleSearchOverlay()
        XCTAssertTrue(host.window.makeFirstResponder(host.overlayField))
        state.toggleSearchOverlay()
        host.removeOverlay()
        await nextMainQueueTurn()

        XCTAssertTrue(host.window.firstResponder === host.editor)
    }

    func testActivationDoesNotRestoreFocus() async throws {
        let (state, vault) = try await openVault(files: ["a.md": "# A\n\nfindme here\n"])
        defer { try? FileManager.default.removeItem(at: vault.deletingLastPathComponent()) }
        let host = FocusHost()
        defer { host.close() }
        state.searchOverlayKeyWindowProvider = { host.window }
        XCTAssertTrue(host.window.makeFirstResponder(host.editor))

        let hits = try await search(state, for: "findme")
        let hit = try XCTUnwrap(hits.first)
        XCTAssertTrue(host.window.makeFirstResponder(host.overlayField))
        try makeCurrentKeyEvent(keyCode: Self.returnKey, modifiers: [])
        state.openSearchResult(hit)
        XCTAssertFalse(state.isSearchOpen)
        host.removeOverlay()
        await nextMainQueueTurn()
        await state.noteLoadTask?.value
        await nextMainQueueTurn()

        XCTAssertFalse(
            host.window.firstResponder === host.editor,
            "opening a hit never hands focus back to where the user was (SD-2)")
    }

    func testRestoreIsSkippedWhenThePaletteSupersededTheOverlay() async throws {
        let (state, _) = makeState()
        let host = FocusHost()
        defer { host.close() }
        state.searchOverlayKeyWindowProvider = { host.window }
        XCTAssertTrue(host.window.makeFirstResponder(host.editor))

        state.toggleSearchOverlay()
        XCTAssertTrue(host.window.makeFirstResponder(host.overlayField))
        // The palette over the overlay runs Search Vault, which closes the
        // overlay, and then dismisses itself in the same synchronous flow.
        state.isCommandPaletteOpen = true
        state.toggleSearchOverlay()
        XCTAssertFalse(state.isSearchOpen)
        state.isCommandPaletteOpen = false
        host.removeOverlay()
        await nextMainQueueTurn()

        XCTAssertFalse(
            host.window.firstResponder === host.editor,
            "the palette owned the moment the overlay closed: no restore")
    }

    func testRestoreIsSkippedWhenASheetOwnsTheWindow() async throws {
        let (state, _) = makeState()
        let host = FocusHost()
        defer { host.close() }
        state.searchOverlayKeyWindowProvider = { host.window }
        XCTAssertTrue(host.window.makeFirstResponder(host.editor))

        state.toggleSearchOverlay()
        XCTAssertTrue(host.window.makeFirstResponder(host.overlayField))
        let sheet = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 200, height: 100),
            styleMask: [.titled], backing: .buffered, defer: false)
        sheet.isReleasedWhenClosed = false
        host.window.beginSheet(sheet, completionHandler: nil)
        defer { host.window.endSheet(sheet) }
        await waitUntil { host.window.attachedSheet === sheet }

        state.dismissSearchOverlay()
        host.removeOverlay()
        await nextMainQueueTurn()

        XCTAssertFalse(host.window.firstResponder === host.editor)
    }

    /// Fall back to no restore rather than focusing something else.
    func testRestoreIsSkippedWhenTheCapturedElementLeftTheWindow() async throws {
        let (state, _) = makeState()
        let host = FocusHost()
        defer { host.close() }
        state.searchOverlayKeyWindowProvider = { host.window }
        XCTAssertTrue(host.window.makeFirstResponder(host.editor))

        state.toggleSearchOverlay()
        XCTAssertTrue(host.window.makeFirstResponder(host.overlayField))
        host.editor.removeFromSuperview()
        state.dismissSearchOverlay()
        host.removeOverlay()
        await nextMainQueueTurn()

        XCTAssertTrue(
            host.window.firstResponder === host.window,
            "a gone element is not replaced with another (focus stays on the window)")
    }

    /// A palette-invoked Search Vault opens while the palette's sheet is
    /// the key window. The overlay is laid out in the window the sheet is
    /// attached to, which keeps its first responder while the sheet is up:
    /// that is the element Esc gives focus back to.
    func testCaptureResolvesASheetToTheWindowItIsAttachedTo() async throws {
        let (state, _) = makeState()
        let host = FocusHost()
        defer { host.close() }
        XCTAssertTrue(host.window.makeFirstResponder(host.editor))
        let palette = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 200, height: 100),
            styleMask: [.titled], backing: .buffered, defer: false)
        palette.isReleasedWhenClosed = false
        let paletteField = NSTextView(frame: palette.contentLayoutRect)
        palette.contentView?.addSubview(paletteField)
        host.window.beginSheet(palette, completionHandler: nil)
        await waitUntil { palette.sheetParent === host.window }
        XCTAssertTrue(palette.makeFirstResponder(paletteField))
        XCTAssertTrue(
            host.window.firstResponder === host.editor,
            "the window under a sheet keeps its first responder")
        state.searchOverlayKeyWindowProvider = { palette }

        state.toggleSearchOverlay()
        host.window.endSheet(palette)
        XCTAssertTrue(host.window.makeFirstResponder(host.overlayField))
        state.dismissSearchOverlay()
        host.removeOverlay()
        await nextMainQueueTurn()

        XCTAssertTrue(host.window.firstResponder === host.editor)
    }

    // MARK: - Fixtures

    private static let hostApplication: NSApplication = {
        let application = NSApplication.shared
        _ = application.setActivationPolicy(.accessory)
        return application
    }()

    override func tearDown() async throws {
        // Leave NSApp.currentEvent a plain key so no later fact reads a ⌘
        // that this one made current.
        if NSApp != nil {
            try? makeCurrentKeyEvent(keyCode: 0, modifiers: [])
        }
        try await super.tearDown()
    }

    private func makeState() -> (AppState, URL) {
        _ = Self.hostApplication
        let dir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-search-keys-\(UUID().uuidString)")
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        let store = RecentVaultsStore(fileURL: dir.appendingPathComponent("recents.json"))
        return (AppState(recentsStore: store, externalOpener: { _ in true }), dir)
    }

    private func openVault(files: [String: String]) async throws -> (AppState, URL) {
        let (state, dir) = makeState()
        let vault = dir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        for (name, body) in files {
            try Data(body.utf8).write(to: vault.appendingPathComponent(name))
        }
        state.openVault(at: vault)
        await state.scanTask?.value
        return (state, vault)
    }

    /// Open the overlay, type `query`, and wait for the results panel.
    private func search(_ state: AppState, for query: String) async throws -> [QueryHit] {
        if !state.isSearchOpen { state.toggleSearchOverlay() }
        state.searchQuery = query
        state.bumpSearchQuery()
        for _ in 0..<100 {
            if case .results(let rows, _) = state.searchState, !rows.isEmpty { return rows }
            try await Task.sleep(nanoseconds: 50_000_000)
        }
        XCTFail("search for \(query) never published results")
        return []
    }

    /// Make a key event `NSApp.currentEvent`, the way the run loop does
    /// before a local monitor sees it: post it, then dequeue it.
    private func makeCurrentKeyEvent(keyCode: UInt16, modifiers: NSEvent.ModifierFlags) throws {
        _ = Self.hostApplication
        let characters = keyCode == Self.returnKey ? "\r" : "a"
        let event = try XCTUnwrap(
            NSEvent.keyEvent(
                with: .keyDown, location: .zero, modifierFlags: modifiers,
                timestamp: ProcessInfo.processInfo.systemUptime, windowNumber: 0,
                context: nil, characters: characters,
                charactersIgnoringModifiers: characters, isARepeat: false,
                keyCode: keyCode))
        NSApp.postEvent(event, atStart: true)
        let dequeued = NSApp.nextEvent(
            matching: .keyDown, until: Date(timeIntervalSinceNow: 5), inMode: .default,
            dequeue: true)
        XCTAssertEqual(dequeued?.keyCode, keyCode)
        XCTAssertEqual(NSApp.currentEvent?.keyCode, keyCode)
    }

    private func waitUntil(
        _ condition: () -> Bool, file: StaticString = #filePath, line: UInt = #line
    ) async {
        for _ in 0..<200 where !condition() {
            RunLoop.main.run(until: Date(timeIntervalSinceNow: 0.01))
        }
        XCTAssertTrue(condition(), "condition never held", file: file, line: line)
    }

    /// Wait until the main queue has run everything enqueued before now,
    /// the restore's `DispatchQueue.main.async` included.
    private func nextMainQueueTurn() async {
        let turn = expectation(description: "main queue turn")
        DispatchQueue.main.async { turn.fulfill() }
        await fulfillment(of: [turn], timeout: 5)
    }

    /// A window with two text views: where the user was (the editor) and
    /// the overlay's field, removed the way SwiftUI removes the overlay.
    private final class FocusHost {
        let window: NSWindow
        let editor: NSTextView
        let overlayField: NSTextView

        init() {
            _ = SearchOverlayKeyRoutingTests.hostApplication
            let content = NSView(frame: NSRect(x: 0, y: 0, width: 400, height: 200))
            editor = NSTextView(frame: NSRect(x: 0, y: 80, width: 400, height: 120))
            overlayField = NSTextView(frame: NSRect(x: 0, y: 0, width: 400, height: 40))
            content.addSubview(editor)
            content.addSubview(overlayField)
            window = NSWindow(
                contentRect: content.frame, styleMask: [.titled], backing: .buffered,
                defer: false)
            window.isReleasedWhenClosed = false
            window.contentView = content
        }

        func removeOverlay() { overlayField.removeFromSuperview() }

        func close() {
            window.contentView = nil
            window.close()
        }
    }
}
