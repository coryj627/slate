// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Foundation
import SwiftUI
import XCTest

@testable import SlateMac

/// #1438: an organization command re-sorts the Files tree's already-loaded
/// levels at once, whichever surface chose it, without reloading a level,
/// with the command's own announcement and nothing more, and the reader's
/// selection and keyboard focus stay on the same item.
///
/// The defect these facts pin: the live re-sort compared a level's raw
/// summary-revision entry, which is absent until a sort-relevant save lands
/// in that level, with a capture defaulted to 0. Every level that had never
/// seen such a save judged its own fresh result stale, queued itself again
/// and never published, so the worker re-sorted it over and over until the
/// next bind. Files: Refresh hid it, because a reload organizes a level
/// while it prepares it.
///
/// The view-model facts drive the production asynchronous loading (the
/// serial level worker and the live-reorganization pump) over a real vault
/// session, dispatch through the surface they name exactly as it does, and
/// hand the tree AppState's organization the way `FileTreeSidebar`'s
/// `.onChange(of: appState.sidebarOrganization)` does. The hosted fact
/// proves that wiring end to end, through the accessibility tree.
/// Nothing here trashes anything.
@MainActor
final class FileTreeLiveOrganizationTests: XCTestCase {
    private var root: URL!

    override func setUpWithError() throws {
        root = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-live-organization-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        if let root { try? FileManager.default.removeItem(at: root) }
    }

    // MARK: - Fixtures

    private final class RecordingAnnouncer: AnnouncementPosting, @unchecked Sendable {
        private(set) var messages: [String] = []

        func post(_ message: String, priority: AnnouncementPriority) {
            messages.append(message)
        }
    }

    /// Level fetches, counted on the worker that makes them, so a fact can
    /// prove an organization command re-sorted a level instead of reloading
    /// it.
    private final class LevelFetches: @unchecked Sendable {
        private let lock = NSLock()
        private var parents: [String] = []

        func record(_ parent: String) {
            lock.lock()
            parents.append(parent)
            lock.unlock()
        }

        var count: Int {
            lock.lock()
            defer { lock.unlock() }
            return parents.count
        }
    }

    private struct Note {
        let path: String
        /// An authored civil date: the created sort's first source.
        let created: String
        let modifiedSecondsAgo: TimeInterval
    }

    /// Every sort mode gives these its own order (names A, B, C; modified
    /// newest B, A, C; created newest A, C, B; and the reverses), so every
    /// command in the matrix visibly moves rows at both levels.
    private static let matrixNotes: [Note] = [
        Note(path: "alpha.md", created: "2026-09-01", modifiedSecondsAgo: 2 * 86_400),
        Note(path: "bravo.md", created: "2026-01-01", modifiedSecondsAgo: 3_600),
        Note(path: "charlie.md", created: "2026-06-01", modifiedSecondsAgo: 40 * 86_400),
        Note(path: "folder/delta.md", created: "2026-09-01", modifiedSecondsAgo: 2 * 86_400),
        Note(path: "folder/echo.md", created: "2026-01-01", modifiedSecondsAgo: 3_600),
        Note(
            path: "folder/foxtrot.md", created: "2026-06-01",
            modifiedSecondsAgo: 40 * 86_400),
    ]

    /// The vault #1438 was reported against, plus one more note so a sort
    /// moves rows on either side of the selected one.
    private static let issueNotes: [Note] = [
        Note(path: "note.md", created: "2026-03-01", modifiedSecondsAgo: 86_400),
        Note(path: "other.md", created: "2026-04-01", modifiedSecondsAgo: 7_200),
        Note(path: "zulu.md", created: "2026-05-01", modifiedSecondsAgo: 3_600),
    ]

    private func makeVault(_ name: String, notes: [Note]) throws -> URL {
        let vault = root.appendingPathComponent(name, isDirectory: true)
        let now = Date()
        for note in notes {
            let url = vault.appendingPathComponent(note.path)
            try FileManager.default.createDirectory(
                at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
            let stem = (url.lastPathComponent as NSString).deletingPathExtension
            try "---\ncreated: \(note.created)\n---\n# \(stem)\n".write(
                to: url, atomically: true, encoding: .utf8)
            try FileManager.default.setAttributes(
                [.modificationDate: now.addingTimeInterval(-note.modifiedSecondsAgo)],
                ofItemAtPath: url.path)
        }
        return vault
    }

    private func openVault(
        _ vault: URL, announcer: AnnouncementPosting = AppKitAnnouncementPoster()
    ) async throws -> AppState {
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: root.appendingPathComponent("\(vault.lastPathComponent)-recents.json")),
            externalOpener: { _ in true },
            announcer: announcer)
        state.openVault(at: vault)
        await state.scanTask?.value
        XCTAssertNotNil(state.currentSession, "the vault opened")
        return state
    }

    private func settle(
        _ tree: FileTreeViewModel, file: StaticString = #filePath, line: UInt = #line
    ) async {
        let settled = await tree.settleLevelLoadsForTesting()
        XCTAssertTrue(
            settled,
            "the tree's level work never settled: a live re-sort that queues itself "
                + "again never publishes",
            file: file, line: line)
    }

    /// The production asynchronous tree over the vault's own session, every
    /// level fetch counted, with `expanding` folders disclosed and loaded.
    private func loadedTree(
        for state: AppState,
        fetches: LevelFetches,
        expanding folders: [String] = [],
        file: StaticString = #filePath,
        line: UInt = #line
    ) async throws -> FileTreeViewModel {
        let session = try XCTUnwrap(state.currentSession, file: file, line: line)
        let tree = FileTreeViewModel()
        reflect(state, into: tree)
        tree.bindAsynchronouslyForTesting { parent, cursor, cancel in
            fetches.record(parent)
            return try session.listDirChildrenPage(
                parentPath: parent,
                paging: Paging(cursor: cursor, limit: FileTreeViewModel.levelPageLimit),
                cancel: cancel
            ).asTreeListing()
        }
        await settle(tree, file: file, line: line)
        for path in folders {
            let folder = try XCTUnwrap(
                tree.rootLevel.first { $0.path == path }, "the root lists \(path)",
                file: file, line: line)
            tree.expand(folder)
            await settle(tree, file: file, line: line)
        }
        return tree
    }

    /// What `FileTreeSidebar`'s `.onChange(of: appState.sidebarOrganization)`
    /// does: hand the tree AppState's current organization.
    private func reflect(_ state: AppState, into tree: FileTreeViewModel) {
        tree.applyOrganization(state.sidebarOrganizationContext())
    }

    private func select(
        _ path: String?, isDirectory: Bool = false, on state: AppState,
        file: StaticString = #filePath, line: UInt = #line
    ) throws {
        let session = try XCTUnwrap(state.currentSession, file: file, line: line)
        let items = path.map {
            [SidebarSelectionItem(path: $0, isDirectory: isDirectory, isMarkdown: !isDirectory)]
        } ?? []
        let parent = path.map {
            isDirectory ? $0 : ($0 as NSString).deletingLastPathComponent
        } ?? ""
        XCTAssertTrue(
            state.publishSidebarSelectionSnapshot(
                SidebarSelectionSnapshot(
                    sessionIdentity: ObjectIdentifier(session),
                    items: items,
                    focusedPath: path,
                    creationParent: parent)),
            "the selection published", file: file, line: line)
    }

    private enum Surface: String {
        case menuBar = "menu bar"
        case palette = "palette"
    }

    /// Chooses `id` the way the surface does: the menu bar runs its
    /// evaluation's frozen intent (`SidebarSortMenu`, the File menu's
    /// catalog groups); the palette runs the command by id.
    private func choose(
        _ id: String, from surface: Surface, on state: AppState,
        file: StaticString = #filePath, line: UInt = #line
    ) throws {
        switch surface {
        case .menuBar:
            let evaluation = try XCTUnwrap(
                state.sidebarActionProjection(surface: .menuBar).first { $0.id == id },
                "the menu bar lists \(id)", file: file, line: line)
            let intent = try XCTUnwrap(
                evaluation.intent,
                "\(id) is unavailable from the menu bar: \(evaluation.disabledReason ?? "")",
                file: file, line: line)
            _ = try state.dispatchSidebarAction(intent)
        case .palette:
            _ = try state.dispatchSidebarAction(id: id)
        }
    }

    /// The command's acknowledgement waits for its journal line and its
    /// write; the write's tail publishes disk truth, which the view hands
    /// the tree again (a no-op when nothing changed).
    private func persisted(
        _ state: AppState, tree: FileTreeViewModel,
        file: StaticString = #filePath, line: UInt = #line
    ) async {
        await state.sidebarOrganizationAcknowledgementTaskForTesting?.value
        await state.sidebarOrganizationPersistTaskForTesting?.value
        reflect(state, into: tree)
        await settle(tree, file: file, line: line)
    }

    // MARK: - Every organization command, live

    func testEveryOrganizationCommandFromTheMenuBarReSortsTheLoadedLevelsLive() async throws {
        try await assertEveryOrganizationCommandReSortsLive(from: .menuBar)
    }

    func testEveryOrganizationCommandFromThePaletteReSortsTheLoadedLevelsLive() async throws {
        try await assertEveryOrganizationCommandReSortsLive(from: .palette)
    }

    private func assertEveryOrganizationCommandReSortsLive(from surface: Surface) async throws {
        let announcer = RecordingAnnouncer()
        let state = try await openVault(
            makeVault(surface == .menuBar ? "menu-bar" : "palette", notes: Self.matrixNotes),
            announcer: announcer)
        let fetches = LevelFetches()
        let tree = try await loadedTree(for: state, fetches: fetches, expanding: ["folder"])
        let folderID = try XCTUnwrap(tree.rootLevel.first { $0.path == "folder" }?.nodeID)
        let folderLevel = { tree.children[folderID]?.map(\.path) ?? [] }
        XCTAssertEqual(
            tree.rootLevel.map(\.path), ["folder", "alpha.md", "bravo.md", "charlie.md"])
        XCTAssertEqual(
            folderLevel(), ["folder/delta.md", "folder/echo.md", "folder/foxtrot.md"])
        let loadedFetches = fetches.count
        var chosen: Set<String> = []

        func step(
            _ id: String,
            on target: String?,
            isDirectory: Bool = false,
            says sentence: String,
            root expectedRoot: [String],
            folder expectedFolder: [String],
            line: UInt = #line
        ) async throws {
            chosen.insert(id)
            try select(target, isDirectory: isDirectory, on: state, line: line)
            let spoken = announcer.messages.count
            try choose(id, from: surface, on: state, line: line)
            reflect(state, into: tree)
            await settle(tree, line: line)
            let context = "\(id) from the \(surface.rawValue)"
            XCTAssertEqual(
                tree.rootLevel.map(\.path), ["folder"] + expectedRoot,
                "\(context): the root level re-sorts at once", line: line)
            XCTAssertEqual(
                folderLevel(), expectedFolder,
                "\(context): the expanded folder re-sorts at once", line: line)
            await persisted(state, tree: tree, line: line)
            XCTAssertEqual(
                tree.rootLevel.map(\.path), ["folder"] + expectedRoot,
                "\(context): the saved organization keeps the order", line: line)
            XCTAssertEqual(folderLevel(), expectedFolder, context, line: line)
            XCTAssertEqual(
                fetches.count, loadedFetches,
                "\(context): a re-sort, never a reload of a level", line: line)
            XCTAssertEqual(
                Array(announcer.messages.dropFirst(spoken)), [sentence],
                "\(context): the command's own sentence, once; the re-sort says nothing",
                line: line)
            XCTAssertEqual(
                state.sidebarSelectionSnapshot?.focusedPath, target,
                "\(context): the reader's row is untouched", line: line)
        }

        // Pins: the folder's Pinned section appears, grows, shrinks and goes.
        try await step(
            SlateCommandID.sidebarPinNote, on: "folder/foxtrot.md", says: "Pinned.",
            root: ["alpha.md", "bravo.md", "charlie.md"],
            folder: ["folder/foxtrot.md", "folder/delta.md", "folder/echo.md"])
        XCTAssertEqual(tree.headerRow(before: .file(path: "folder/foxtrot.md"))?.kind, .pinned)
        XCTAssertEqual(tree.headerRow(before: .file(path: "folder/foxtrot.md"))?.fileCount, 1)
        try await step(
            SlateCommandID.sidebarPinNote, on: "folder/echo.md", says: "Pinned.",
            root: ["alpha.md", "bravo.md", "charlie.md"],
            folder: ["folder/foxtrot.md", "folder/echo.md", "folder/delta.md"])
        XCTAssertEqual(tree.headerRow(before: .file(path: "folder/foxtrot.md"))?.fileCount, 2)
        XCTAssertTrue(tree.isPinnedRow(.file(path: "folder/echo.md")))
        try await step(
            SlateCommandID.sidebarUnpinNote, on: "folder/foxtrot.md", says: "Unpinned.",
            root: ["alpha.md", "bravo.md", "charlie.md"],
            folder: ["folder/echo.md", "folder/delta.md", "folder/foxtrot.md"])
        XCTAssertEqual(tree.headerRow(before: .file(path: "folder/echo.md"))?.fileCount, 1)
        XCTAssertFalse(tree.isPinnedRow(.file(path: "folder/foxtrot.md")))
        try await step(
            SlateCommandID.sidebarUnpinAll, on: "folder", isDirectory: true,
            says: "Unpinned 1 note.",
            root: ["alpha.md", "bravo.md", "charlie.md"],
            folder: ["folder/delta.md", "folder/echo.md", "folder/foxtrot.md"])
        XCTAssertNil(tree.headerRow(before: .file(path: "folder/delta.md")))
        XCTAssertFalse(tree.isPinnedRow(.file(path: "folder/echo.md")))

        // Every sort mode, chosen for the vault from a root note: both
        // loaded levels follow.
        try await step(
            SlateCommandID.sidebarSortNameDesc, on: "alpha.md",
            says: "Sorted by name, Z to A.",
            root: ["charlie.md", "bravo.md", "alpha.md"],
            folder: ["folder/foxtrot.md", "folder/echo.md", "folder/delta.md"])
        try await step(
            SlateCommandID.sidebarSortModifiedDesc, on: "alpha.md",
            says: "Sorted by modified, newest first.",
            root: ["bravo.md", "alpha.md", "charlie.md"],
            folder: ["folder/echo.md", "folder/delta.md", "folder/foxtrot.md"])
        try await step(
            SlateCommandID.sidebarSortModifiedAsc, on: "alpha.md",
            says: "Sorted by modified, oldest first.",
            root: ["charlie.md", "alpha.md", "bravo.md"],
            folder: ["folder/foxtrot.md", "folder/delta.md", "folder/echo.md"])
        try await step(
            SlateCommandID.sidebarSortCreatedDesc, on: "alpha.md",
            says: "Sorted by created, newest first.",
            root: ["alpha.md", "charlie.md", "bravo.md"],
            folder: ["folder/delta.md", "folder/foxtrot.md", "folder/echo.md"])
        try await step(
            SlateCommandID.sidebarSortCreatedAsc, on: "alpha.md",
            says: "Sorted by created, oldest first.",
            root: ["bravo.md", "charlie.md", "alpha.md"],
            folder: ["folder/echo.md", "folder/foxtrot.md", "folder/delta.md"])

        // Date grouping forces the newest-first date sort and adds section
        // headers; turning it off restores the chosen sort, headers gone.
        try await step(
            SlateCommandID.sidebarToggleDateGrouping, on: "alpha.md",
            says: "Sorted by created, newest first, grouped by date.",
            root: ["alpha.md", "charlie.md", "bravo.md"],
            folder: ["folder/delta.md", "folder/foxtrot.md", "folder/echo.md"])
        XCTAssertEqual(tree.headerRow(before: .file(path: "alpha.md"))?.kind, .group)
        XCTAssertEqual(tree.headerRow(before: .file(path: "folder/delta.md"))?.kind, .group)
        try await step(
            SlateCommandID.sidebarToggleDateGrouping, on: "alpha.md",
            says: "Sorted by created, oldest first.",
            root: ["bravo.md", "charlie.md", "alpha.md"],
            folder: ["folder/echo.md", "folder/foxtrot.md", "folder/delta.md"])
        XCTAssertNil(tree.headerRow(before: .file(path: "bravo.md")))
        XCTAssertNil(tree.headerRow(before: .file(path: "folder/echo.md")))
        try await step(
            SlateCommandID.sidebarSortNameAsc, on: "alpha.md",
            says: "Sorted by name, A to Z.",
            root: ["alpha.md", "bravo.md", "charlie.md"],
            folder: ["folder/delta.md", "folder/echo.md", "folder/foxtrot.md"])

        // A folder's own sort, then back to the vault default: only that
        // level moves.
        try await step(
            SlateCommandID.sidebarSortNameDesc, on: "folder/delta.md",
            says: "Sorted by name, Z to A.",
            root: ["alpha.md", "bravo.md", "charlie.md"],
            folder: ["folder/foxtrot.md", "folder/echo.md", "folder/delta.md"])
        try await step(
            SlateCommandID.sidebarUseVaultDefaultSort, on: "folder/delta.md",
            says: "Sorted by name, A to Z.",
            root: ["alpha.md", "bravo.md", "charlie.md"],
            folder: ["folder/delta.md", "folder/echo.md", "folder/foxtrot.md"])

        // Shortcuts are organization commands that order nothing in the tree.
        try await step(
            SlateCommandID.sidebarAddShortcut, on: "alpha.md", says: "Added to Shortcuts.",
            root: ["alpha.md", "bravo.md", "charlie.md"],
            folder: ["folder/delta.md", "folder/echo.md", "folder/foxtrot.md"])
        try await step(
            SlateCommandID.sidebarRemoveShortcut, on: "alpha.md",
            says: "Removed from Shortcuts.",
            root: ["alpha.md", "bravo.md", "charlie.md"],
            folder: ["folder/delta.md", "folder/echo.md", "folder/foxtrot.md"])

        XCTAssertEqual(
            chosen, SlateCommandID.sidebarOrganizationCommands,
            "the matrix chooses every organization command in the catalog")
    }

    // MARK: - The Pinned section

    /// #1438's own report: a pin chosen from the menu bar shows the Pinned
    /// section at once, and the last unpin takes it away, with no reload.
    func testThePinnedSectionAppearsAndDisappearsLive() async throws {
        let state = try await openVault(makeVault("pinned", notes: Self.issueNotes))
        let fetches = LevelFetches()
        let tree = try await loadedTree(for: state, fetches: fetches)
        let loadedFetches = fetches.count
        XCTAssertEqual(tree.rootLevel.map(\.path), ["note.md", "other.md", "zulu.md"])
        XCTAssertNil(tree.headerRow(before: .file(path: "note.md")))

        func choosePin(_ id: String, on path: String, line: UInt = #line) async throws {
            try select(path, on: state, line: line)
            try choose(id, from: .menuBar, on: state, line: line)
            reflect(state, into: tree)
            await settle(tree, line: line)
        }

        try await choosePin(SlateCommandID.sidebarPinNote, on: "other.md")
        XCTAssertEqual(tree.rootLevel.map(\.path), ["other.md", "note.md", "zulu.md"])
        XCTAssertEqual(tree.headerRow(before: .file(path: "other.md"))?.kind, .pinned)
        XCTAssertEqual(tree.headerRow(before: .file(path: "other.md"))?.fileCount, 1)
        XCTAssertTrue(tree.isPinnedRow(.file(path: "other.md")))
        await persisted(state, tree: tree)

        try await choosePin(SlateCommandID.sidebarPinNote, on: "note.md")
        XCTAssertEqual(tree.rootLevel.map(\.path), ["other.md", "note.md", "zulu.md"])
        XCTAssertEqual(
            tree.headerRow(before: .file(path: "other.md"))?.fileCount, 2,
            "the section counts both pins at once")
        XCTAssertTrue(tree.isPinnedRow(.file(path: "note.md")))
        await persisted(state, tree: tree)

        try await choosePin(SlateCommandID.sidebarUnpinNote, on: "other.md")
        XCTAssertEqual(tree.rootLevel.map(\.path), ["note.md", "other.md", "zulu.md"])
        XCTAssertEqual(tree.headerRow(before: .file(path: "note.md"))?.fileCount, 1)
        XCTAssertNil(tree.headerRow(before: .file(path: "other.md")))
        XCTAssertFalse(tree.isPinnedRow(.file(path: "other.md")))
        await persisted(state, tree: tree)

        try await choosePin(SlateCommandID.sidebarUnpinNote, on: "note.md")
        XCTAssertEqual(tree.rootLevel.map(\.path), ["note.md", "other.md", "zulu.md"])
        XCTAssertNil(
            tree.headerRow(before: .file(path: "note.md")),
            "the last unpin takes the Pinned section away at once")
        XCTAssertFalse(tree.isPinnedRow(.file(path: "note.md")))
        await persisted(state, tree: tree)
        XCTAssertEqual(fetches.count, loadedFetches, "no level reloaded")
    }

    // MARK: - A collapsed level

    /// A cached level the reader collapsed re-sorts too, so expanding it
    /// shows the new order from its cache, without a fetch.
    func testACollapsedLevelReSortsWhenExpanded() async throws {
        let state = try await openVault(makeVault("collapsed", notes: Self.matrixNotes))
        let fetches = LevelFetches()
        let tree = try await loadedTree(for: state, fetches: fetches, expanding: ["folder"])
        let folder = try XCTUnwrap(tree.rootLevel.first { $0.path == "folder" })
        tree.collapse(folder)
        XCTAssertEqual(
            tree.visibleRows.map(\.path), ["folder", "alpha.md", "bravo.md", "charlie.md"])
        let loadedFetches = fetches.count

        try select("alpha.md", on: state)
        try choose(SlateCommandID.sidebarSortNameDesc, from: .menuBar, on: state)
        reflect(state, into: tree)
        await settle(tree)
        XCTAssertEqual(
            tree.visibleRows.map(\.path), ["folder", "charlie.md", "bravo.md", "alpha.md"])

        tree.expand(folder)
        await settle(tree)
        XCTAssertEqual(
            tree.visibleRows.map(\.path),
            [
                "folder", "folder/foxtrot.md", "folder/echo.md", "folder/delta.md",
                "charlie.md", "bravo.md", "alpha.md",
            ],
            "the collapsed level re-sorted in its cache")
        XCTAssertEqual(fetches.count, loadedFetches, "expanding reused the cache")
        await persisted(state, tree: tree)
    }

    // MARK: - The civil-day rollover

    /// The sidebar clock's midnight tick takes the same live re-sort as a
    /// command: under date grouping a loaded level re-buckets at once
    /// ("Today" becomes "Yesterday"), and a same-day tick changes nothing.
    /// The asynchronous twin of `SidebarOrganizationTreeTests`'
    /// `testSameDayTickDoesNotReorganizeButRolloverDoes`.
    func testAMidnightRolloverReBucketsTheLoadedLevelLive() async throws {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = try XCTUnwrap(TimeZone(identifier: "UTC"))
        let utc = calendar
        func instant(day: Int, hour: Int) throws -> Date {
            try XCTUnwrap(
                utc.date(from: DateComponents(year: 2026, month: 7, day: day, hour: hour)))
        }
        var prefs = SidebarOrganizationPrefs()
        prefs.vaultChoice = SidebarOrganizationChoice(
            sort: SidebarSortOption(field: .modified, direction: .desc),
            grouping: .dateBuckets)
        let grouped = prefs
        func context(_ now: Date) -> FileTreeViewModel.OrganizationContext {
            FileTreeViewModel.OrganizationContext(
                prefs: grouped, now: now, calendar: utc, locale: Locale(identifier: "en_US"))
        }
        let modifiedMs = Int64(try instant(day: 18, hour: 8).timeIntervalSince1970 * 1_000)
        let tree = FileTreeViewModel()
        defer { tree.cancelPendingLoads() }
        tree.applyOrganization(context(try instant(day: 18, hour: 9)))
        tree.bindAsynchronouslyForTesting { _, _, _ in
            DirListing(
                dirs: [],
                files: FileSummaryPage(
                    items: [
                        FileSummary(
                            path: "a.md", name: "a.md", mtimeMs: modifiedMs, sizeBytes: 0,
                            isMarkdown: true, displayName: nil, createdDate: nil,
                            createdMs: nil, wordCount: nil, preview: nil, taskTotal: 0,
                            taskOpen: 0)
                    ],
                    nextCursor: nil,
                    totalFiltered: 1))
        }
        await settle(tree)
        XCTAssertEqual(tree.headerRow(before: .file(path: "a.md"))?.label, "Today")

        tree.applyOrganization(context(try instant(day: 18, hour: 10)))
        await settle(tree)
        XCTAssertEqual(
            tree.headerRow(before: .file(path: "a.md"))?.label, "Today",
            "a same-day tick changes nothing")

        tree.applyOrganization(context(try instant(day: 19, hour: 0)))
        await settle(tree)
        XCTAssertEqual(
            tree.headerRow(before: .file(path: "a.md"))?.label, "Yesterday",
            "crossing local midnight re-buckets the loaded level at once")
    }

    // MARK: - The hosted tree under the keys

    /// The Windows twin is `FilesRegionLandingTests.PinNoteRebuildsTheTreeUnderTheKeysAndTheyStayOnTheRow`:
    /// a pin re-sorts the tree under the reader's row and the keys stay on
    /// the pinned note's row, opening nothing. Here through the real
    /// `FileTreeSidebar` and the accessibility client path: the rows an AT
    /// client reads reorder at once, with the Pinned header and the row's
    /// "pinned", and so does a sort chosen from the menu bar; the selected
    /// row stays `other` and the tree keeps the keys.
    func testPinNoteReSortsTheTreeUnderTheKeysAndTheyStayOnTheRow() async throws {
        let state = try await openVault(makeVault("hosted", notes: Self.issueNotes))
        let window = HostedAccessibilityTree.host(
            FileTreeSidebar().environmentObject(state), size: NSSize(width: 360, height: 560))
        defer { window.close() }
        NSApp.activate(ignoringOtherApps: true)

        let loaded = try await HostedAccessibilityTree.snapshot(of: window) { tree in
            Self.filesTreeRows(in: tree).map(\.label) == ["note", "other", "zulu"]
        }
        XCTAssertEqual(
            Self.filesTreeRows(in: loaded).map(\.label), ["note", "other", "zulu"], "\(loaded)")

        // The reader moves onto `other`, as an AT client does. Where the
        // runner's table does not take an AX selection, the editor's mirror
        // selects the row instead (a search open's route): the fact is about
        // where the selection stays, not how it arrived.
        try HostedAccessibilityTree.selectRow(
            in: window,
            table: { Self.rows(of: $0).contains { $0.label == "zulu" } },
            row: { Self.label(of: $0) == "other" })
        let selectedThroughAX = await eventually(timeout: .seconds(5)) {
            state.sidebarSelectionSnapshot?.focusedPath == "other.md"
        }
        if !selectedThroughAX {
            print("FileTreeLiveOrganizationTests: AX selection did not land; mirroring instead")
            state.selectedFilePath = "other.md"
        }
        try await waitUntil("the tree publishes the reader's row") {
            state.sidebarSelectionSnapshot?.focusedPath == "other.md"
        }
        let table = try XCTUnwrap(
            Self.filesTableView(in: window.contentView), "the Files tree's table view")
        if window.firstResponder !== table {
            // An inactive test runner may not hand the keys over through AX;
            // the fact is about where they stay, so give them directly.
            XCTAssertTrue(window.makeFirstResponder(table))
        }
        let keys = window.firstResponder
        XCTAssertTrue(
            keys === table, "the tree holds the keys before the pin: \(String(describing: keys))")
        let opened = state.selectedFilePath

        try choose(SlateCommandID.sidebarPinNote, from: .menuBar, on: state)
        let pinned = try await HostedAccessibilityTree.snapshot(of: window) { tree in
            Self.filesTreeRows(in: tree).map(\.label) == ["Pinned", "other", "note", "zulu"]
        }
        let pinnedRows = Self.filesTreeRows(in: pinned)
        XCTAssertEqual(pinnedRows.first?.value, "1 note", "\(pinned)")
        XCTAssertEqual(
            pinnedRows.first { $0.label == "other" }?.value?.hasSuffix("pinned"), true,
            "\(pinned)")
        XCTAssertEqual(
            pinnedRows.filter(\.selected).map(\.label), ["other"],
            "the selection stays on the pinned note's row:\n\(pinned)")
        XCTAssertTrue(window.firstResponder === keys, "the tree keeps the keys after the pin")
        XCTAssertEqual(state.sidebarSelectionSnapshot?.focusedPath, "other.md")
        XCTAssertEqual(state.selectedFilePath, opened, "the re-sort opens nothing")
        await state.sidebarOrganizationAcknowledgementTaskForTesting?.value
        await state.sidebarOrganizationPersistTaskForTesting?.value

        try choose(SlateCommandID.sidebarSortNameDesc, from: .menuBar, on: state)
        let sorted = try await HostedAccessibilityTree.snapshot(of: window) { tree in
            Self.filesTreeRows(in: tree).map(\.label) == ["Pinned", "other", "zulu", "note"]
        }
        XCTAssertEqual(
            Self.filesTreeRows(in: sorted).filter(\.selected).map(\.label), ["other"],
            "the selection stays on the same row after the sort:\n\(sorted)")
        XCTAssertTrue(
            sorted.subtree.contains { $0.name == "Files. Sorted by name, Z to A." },
            "the tree names its new order:\n\(sorted)")
        XCTAssertTrue(window.firstResponder === keys, "the tree keeps the keys after the sort")
        XCTAssertEqual(state.selectedFilePath, opened, "the re-sort opens nothing")
        await state.sidebarOrganizationAcknowledgementTaskForTesting?.value
        await state.sidebarOrganizationPersistTaskForTesting?.value
    }

    private struct TreeRow {
        let label: String
        let value: String?
        let selected: Bool
    }

    /// A row's label: the first accessible name in it (the combined file
    /// row's display name, a header's section name).
    private static func label(of row: AXElementSnapshot) -> String? {
        row.subtree.first { !($0.name ?? "").isEmpty }?.name
    }

    /// The table's rows in order: its own row children, or, where the client
    /// nests them deeper, every row beneath it.
    private static func rows(of table: AXElementSnapshot) -> [TreeRow] {
        let direct = table.children.filter { $0.role == "AXRow" }
        let rowElements =
            direct.isEmpty ? table.subtree.dropFirst().filter { $0.role == "AXRow" } : direct
        return rowElements.compactMap { row in
            guard let named = row.subtree.first(where: { !($0.name ?? "").isEmpty })
            else { return nil }
            return TreeRow(
                label: named.name ?? "",
                value: named.value ?? named.valueDescription,
                selected: row.selected || named.selected)
        }
    }

    /// The table view behind the Files tree: the one listing the vault's
    /// three notes.
    private static func filesTableView(in view: NSView?) -> NSTableView? {
        guard let view else { return nil }
        if let table = view as? NSTableView, table.numberOfRows == 3 { return table }
        for subview in view.subviews {
            if let found = filesTableView(in: subview) { return found }
        }
        return nil
    }

    /// The Files tree's rows in reading order: the table or outline listing
    /// `zulu`, which no other sidebar list shows in these facts.
    private static func filesTreeRows(in window: AXElementSnapshot) -> [TreeRow] {
        for table in window.subtree where table.role == "AXOutline" || table.role == "AXTable" {
            let found = rows(of: table)
            if found.contains(where: { $0.label == "zulu" }) { return found }
        }
        return []
    }

    /// Whether `condition` holds within `timeout`, yielding the main actor
    /// between reads.
    private func eventually(
        timeout: Duration, _ condition: () -> Bool
    ) async -> Bool {
        let deadline = ContinuousClock.now.advanced(by: timeout)
        while !condition() {
            guard ContinuousClock.now < deadline else { return false }
            try? await Task.sleep(for: .milliseconds(20))
        }
        return true
    }

    private func waitUntil(
        _ description: String,
        timeout: Duration = .seconds(10),
        file: StaticString = #filePath,
        line: UInt = #line,
        _ condition: () -> Bool
    ) async throws {
        guard await eventually(timeout: timeout, condition) else {
            XCTFail("timed out: \(description)", file: file, line: line)
            throw HostedAccessibilityTree.NotReady(description: description)
        }
    }
}
