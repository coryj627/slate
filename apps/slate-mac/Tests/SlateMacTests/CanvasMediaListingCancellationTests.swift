// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// Plan 43 PR 12 (#1324; locked 05 §4): the canvas vault-file picker's media
/// listing (Add Media… and Locate File…) was a synchronous computed property
/// on the main actor, paging `list_files` with a fresh token per page — the
/// sheet froze for the walk and nothing could stop it. It is now an operation
/// off the main actor with ONE token, owned by the picker it feeds: closing
/// or replacing the picker, a newer listing and the vault closing each cancel
/// it, and a cancelled listing stops at its next page and publishes nothing.
/// Meanwhile the picker shows its loading row (`files == nil`), so the wait
/// is never silent; an empty answer still refuses as it did.
@MainActor
final class CanvasMediaListingCancellationTests: XCTestCase {
    private var tempDir: URL!
    private var posted: [String] = []

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-canvas-media-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    /// A text card and a file card, so Locate has a card to repoint.
    private static let canvas = """
        {"nodes":[
        {"id":"t","type":"text","text":"Alpha","x":0,"y":0,"width":200,"height":100},
        {"id":"f","type":"file","file":"notes/first.md","x":300,"y":0,"width":200,"height":100}
        ],"edges":[]}
        """

    private func makeState(media: [String], notes: [String] = ["notes/first.md"])
        async throws -> AppState
    {
        let vault = tempDir.appendingPathComponent("vault-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        for path in notes + media {
            let url = vault.appendingPathComponent(path)
            try FileManager.default.createDirectory(
                at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
            try Data("# \(path)\n".utf8).write(to: url)
        }
        try Data(Self.canvas.utf8).write(to: vault.appendingPathComponent("board.canvas"))
        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        state.openFile("board.canvas", target: .currentTab)
        XCTAssertNotNil(state.activeCanvasDocument, "the canvas opened")
        posted = []
        state.canvasAnnouncer = CanvasAnnouncer(verbosity: .standard, coalesceWindow: 60) {
            text, _ in self.posted.append(text)
        }
        return state
    }

    /// Parks the first listing to reach `page` until released; every other
    /// visit passes. Records which listing (by token) visited which page.
    private actor PageGate {
        private let parkPage: Int
        private var visits: [(token: ObjectIdentifier, page: Int, onMain: Bool)] = []
        private var parkedToken: ObjectIdentifier?
        private var parked: CheckedContinuation<Void, Never>?
        private var parkWaiters: [CheckedContinuation<Void, Never>] = []
        private var released = false

        init(parkAtPage page: Int) { parkPage = page }

        func visit(page: Int, token: CancelToken, onMain: Bool) async {
            let id = ObjectIdentifier(token)
            visits.append((id, page, onMain))
            guard !released, page == parkPage, parkedToken == nil else { return }
            parkedToken = id
            for waiter in parkWaiters { waiter.resume() }
            parkWaiters = []
            await withCheckedContinuation { parked = $0 }
        }

        func waitUntilParked() async {
            guard parkedToken == nil else { return }
            await withCheckedContinuation { parkWaiters.append($0) }
        }

        func release() {
            released = true
            parked?.resume()
            parked = nil
        }

        func pages(of token: CancelToken) -> [Int] {
            visits.filter { $0.token == ObjectIdentifier(token) }.map(\.page)
        }

        var anyOnMain: Bool { visits.contains { $0.onMain } }
        var visitCount: Int { visits.count }
    }

    private nonisolated static func onMainThread() -> Bool { Thread.isMainThread }

    private func install(_ gate: PageGate, on state: AppState) {
        state.canvasMediaListingPageGateForTesting = { page, token in
            await gate.visit(page: page, token: token, onMain: Self.onMainThread())
        }
    }

    // MARK: - The owner's teardown cancels the listing

    /// The spec's per-owner fact for the canvas media listing: the picker
    /// opens on its loading row, and closing it — the sheet's own binding
    /// sets the prompt to nil — cancels the listing's token. The listing then
    /// stops and publishes nothing: no picker comes back, nothing is spoken.
    func testCancellingTheOwnerCancelsItsListingToken() async throws {
        let state = try await makeState(media: ["media/a.png", "media/b.mp3"])
        let gate = PageGate(parkAtPage: 0)
        install(gate, on: state)

        state.canvasOpenAddMedia()
        await gate.waitUntilParked()
        guard case .addMedia(let loading)? = state.canvasPrompt else {
            return XCTFail("Add Media opens its picker at once, got \(String(describing: state.canvasPrompt))")
        }
        XCTAssertNil(loading, "the picker shows its loading row while the listing runs")
        let token = try XCTUnwrap(state.canvasMediaListingCancelToken)
        let listing = try XCTUnwrap(state.canvasMediaListingTask)
        XCTAssertFalse(token.isCancelled())

        state.canvasPrompt = nil
        XCTAssertTrue(token.isCancelled(), "closing the picker cancels its listing's token")
        await gate.release()
        await listing.value
        state.canvasAnnouncer.flushForTests()

        XCTAssertNil(state.canvasPrompt, "a cancelled listing presents nothing")
        XCTAssertEqual(posted, [], "and says nothing")
        XCTAssertNil(state.canvasMediaListingCancelToken)
        XCTAssertNil(state.canvasMediaListingTask)
    }

    /// A page loop observes its token: a newer listing cancels the older one
    /// while it is between pages; the older one's next `list_files` page
    /// returns `.Cancelled`, it requests no page after that, and it publishes
    /// nothing — the picker shows the newer listing's complete set, never the
    /// older one's partial one.
    func testAPageLoopObservesItsTokenAndStopsWithoutAPartialPublish() async throws {
        let media = (1...5).map { "media/clip-\($0).png" }
        let state = try await makeState(media: media)
        state.canvasMediaListingPageLimit = 2
        let gate = PageGate(parkAtPage: 1)
        install(gate, on: state)

        state.canvasOpenAddMedia()
        await gate.waitUntilParked()
        let older = try XCTUnwrap(state.canvasMediaListingCancelToken)
        let olderListing = try XCTUnwrap(state.canvasMediaListingTask)

        state.canvasOpenAddMedia()
        XCTAssertTrue(older.isCancelled(), "a newer listing cancels the older one's token")
        let newer = try XCTUnwrap(state.canvasMediaListingCancelToken)
        let newerListing = try XCTUnwrap(state.canvasMediaListingTask)
        XCTAssertFalse(newer === older, "one token per listing")

        await gate.release()
        await olderListing.value
        await newerListing.value

        let olderPages = await gate.pages(of: older)
        XCTAssertEqual(olderPages, [0, 1], "the cancelled loop requests no page after its token")
        guard case .addMedia(let files)? = state.canvasPrompt else {
            return XCTFail("the newer listing presents its picker")
        }
        XCTAssertEqual(files, media, "the complete set, never the older listing's partial one")
        let anyOnMain = await gate.anyOnMain
        XCTAssertFalse(anyOnMain, "locked 05 §4: the listing pages off the main actor")
    }

    /// Closing the vault mid-listing cancels it and leaves no picker behind.
    func testClosingTheVaultMidListingLeavesNothingBehind() async throws {
        let state = try await makeState(media: ["media/a.png"])
        let gate = PageGate(parkAtPage: 0)
        install(gate, on: state)
        state.canvasOpenAddMedia()
        await gate.waitUntilParked()
        let token = try XCTUnwrap(state.canvasMediaListingCancelToken)
        let listing = try XCTUnwrap(state.canvasMediaListingTask)

        XCTAssertTrue(state.closeVault())
        XCTAssertTrue(token.isCancelled(), "closing the vault cancels the listing")
        XCTAssertNil(state.canvasPrompt, "no loading picker survives the vault")
        await gate.release()
        await listing.value
        state.canvasAnnouncer.flushForTests()
        XCTAssertNil(state.canvasPrompt)
        XCTAssertEqual(posted, [])
    }

    // MARK: - What the picker shows once the listing answers

    func testAddMediaListsTheVaultsMediaAfterItsLoadingRow() async throws {
        let state = try await makeState(media: ["media/a.png", "media/b.mp3", "docs/c.pdf"])
        state.canvasOpenAddMedia()
        let listing = try XCTUnwrap(state.canvasMediaListingTask)
        await listing.value
        guard case .addMedia(let files)? = state.canvasPrompt else {
            return XCTFail("the picker stays open with its list")
        }
        XCTAssertEqual(
            files.map(Set.init), Set(["docs/c.pdf", "media/a.png", "media/b.mp3"]),
            "every non-note, non-canvas file, as before (#1369 owns the media gate)")
        XCTAssertNil(state.canvasMediaListingCancelToken, "a finished listing holds no token")
    }

    /// The filter typed while the list loads survives its arrival.
    func testTheFilterTypedWhileLoadingSurvivesTheListsArrival() async throws {
        let state = try await makeState(media: ["media/a.png", "media/b.mp3"])
        let gate = PageGate(parkAtPage: 0)
        install(gate, on: state)
        state.canvasOpenAddMedia()
        await gate.waitUntilParked()
        state.canvasPromptDraft = "b.mp"
        let listing = try XCTUnwrap(state.canvasMediaListingTask)
        await gate.release()
        await listing.value
        guard case .addMedia(let files)? = state.canvasPrompt else {
            return XCTFail("the picker stays open with its list")
        }
        XCTAssertEqual(files?.count, 2)
        XCTAssertEqual(state.canvasPromptDraft, "b.mp")
    }

    /// An empty answer still refuses as Add Media always has: the picker
    /// closes and core's sentence is spoken.
    func testAnEmptyVaultStillRefusesAddMediaOnceTheListingAnswers() async throws {
        let state = try await makeState(media: [])
        state.canvasOpenAddMedia()
        let listing = try XCTUnwrap(state.canvasMediaListingTask)
        await listing.value
        state.canvasAnnouncer.flushForTests()
        XCTAssertNil(state.canvasPrompt, "no media: no picker")
        XCTAssertEqual(
            posted, [a11yRender(event: .canvas(event: .canvasStatus(note: .noMediaInVault))).text])
    }

    /// The presented sheet keeps the prompt it was presented with, and
    /// `CanvasPrompt` compares by identity, so the list that lands in place
    /// must replace the loading row from the owner's current value — the
    /// runtime check found the sheet left on its loading row otherwise.
    func testTheLandedListReplacesTheLoadingRowInThePresentedSheet() async throws {
        let state = try await makeState(media: ["media/a.png"])
        state.canvasPrompt = .addMedia(files: nil)
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 480, height: 520),
            styleMask: [.titled], backing: .buffered, defer: false)
        window.isReleasedWhenClosed = false
        defer { window.orderOut(nil) }
        let host = NSHostingView(
            rootView: CanvasPromptSheet(prompt: .addMedia(files: nil)).environmentObject(state))
        window.contentView = host
        window.makeKeyAndOrderFront(nil)
        host.layoutSubtreeIfNeeded()
        func find<T: NSView>(_ type: T.Type, in view: NSView) -> T? {
            if let match = view as? T { return match }
            for child in view.subviews {
                if let match = find(type, in: child) { return match }
            }
            return nil
        }
        XCTAssertNotNil(find(NSProgressIndicator.self, in: host), "the loading row shows first")
        XCTAssertNil(find(NSTableView.self, in: host))

        state.canvasPrompt = .addMedia(files: ["media/a.png"])
        let deadline = Date().addingTimeInterval(5)
        while find(NSTableView.self, in: host) == nil, Date() < deadline {
            try await Task.sleep(nanoseconds: 20_000_000)
            host.layoutSubtreeIfNeeded()
        }
        XCTAssertNotNil(find(NSTableView.self, in: host), "the landed list replaces the loading row")
        XCTAssertNil(find(NSProgressIndicator.self, in: host))
    }

    func testLocateListsNotesAndMediaAfterItsLoadingRow() async throws {
        let state = try await makeState(media: ["media/a.png"], notes: ["notes/first.md", "notes/second.md"])
        let doc = try XCTUnwrap(state.activeCanvasDocument)
        state.canvasSelect(nodeId: "f", in: doc, announce: false)
        let gate = PageGate(parkAtPage: 0)
        install(gate, on: state)

        state.canvasOpenLocate()
        await gate.waitUntilParked()
        guard case .locate(let nodeId, _, let loading)? = state.canvasPrompt else {
            return XCTFail("Locate opens its picker at once")
        }
        XCTAssertEqual(nodeId, "f")
        XCTAssertNil(loading, "the picker shows its loading row while the listing runs")
        let listing = try XCTUnwrap(state.canvasMediaListingTask)
        await gate.release()
        await listing.value

        guard case .locate(_, _, let files)? = state.canvasPrompt else {
            return XCTFail("the picker stays open with its list")
        }
        XCTAssertEqual(files, ["notes/first.md", "notes/second.md", "media/a.png"])
    }
}
