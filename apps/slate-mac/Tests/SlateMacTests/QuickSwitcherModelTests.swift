// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Dispatch
import XCTest

@testable import SlateMac

/// Quick Open model coverage: canonical core ordering, bounded publication,
/// selection, announcements, and the background generation owner.
final class QuickSwitcherModelTests: XCTestCase {

    private final class LockedBool: @unchecked Sendable {
        private let lock = NSLock()
        private var storage = false

        var value: Bool {
            lock.lock()
            defer { lock.unlock() }
            return storage
        }

        func set(_ value: Bool) {
            lock.lock()
            storage = value
            lock.unlock()
        }
    }

    private final class ConcurrencyProbe: @unchecked Sendable {
        private let lock = NSLock()
        private var active = 0
        private var maximum = 0

        var maximumObserved: Int {
            lock.lock()
            defer { lock.unlock() }
            return maximum
        }

        func enter() {
            lock.lock()
            active += 1
            maximum = max(maximum, active)
            lock.unlock()
        }

        func leave() {
            lock.lock()
            active -= 1
            lock.unlock()
        }
    }

    private func candidate(
        _ path: String,
        _ name: String
    ) -> QuickSwitcherModel.CandidateFile {
        QuickSwitcherModel.CandidateFile(path: path, name: name)
    }

    private func candidates(
        _ pairs: [(String, String)]
    ) -> [QuickSwitcherModel.CandidateFile] {
        pairs.map { candidate($0.0, $0.1) }
    }

    @MainActor
    private func makeModel() -> QuickSwitcherModel {
        QuickSwitcherModel(debounceNanoseconds: 0)
    }

    // MARK: - Ordering

    @MainActor
    func testEmptyQueryOrdersRecentsFirstThenIncomingOrder() async {
        let model = makeModel()
        model.load(
            files: candidates([
                ("a.md", "a.md"), ("b.md", "b.md"),
                ("c.md", "c.md"), ("d.md", "d.md"),
            ]),
            recents: ["c.md", "a.md"])
        await model.settleRanking()

        XCTAssertEqual(
            model.displayOrder.map(\.path),
            ["c.md", "a.md", "b.md", "d.md"])
    }

    @MainActor
    func testEmptyQueryPrunesRecentsMissingFromFiles() async {
        let model = makeModel()
        model.load(
            files: candidates([("a.md", "a.md"), ("b.md", "b.md")]),
            recents: ["gone.md", "b.md"])
        await model.settleRanking()

        XCTAssertEqual(model.displayOrder.map(\.path), ["b.md", "a.md"])
    }

    @MainActor
    func testNonEmptyQuerySortsByScoreThenRecencyTiebreak() async {
        let model = makeModel()
        model.load(
            files: candidates([
                ("alpha/note.md", "note.md"),
                ("beta/note.md", "note.md"),
            ]),
            recents: ["beta/note.md"])
        await model.settleRanking()
        model.query = "note"
        await model.settleRanking()

        XCTAssertEqual(
            model.displayOrder.map(\.path),
            ["beta/note.md", "alpha/note.md"])
    }

    @MainActor
    func testRecencyDoesNotBeatAMateriallyBetterFuzzyScore() async {
        let model = makeModel()
        model.load(
            files: candidates([
                ("notes.md", "notes.md"),
                ("archive/meeting-notes.md", "meeting-notes.md"),
            ]),
            recents: ["archive/meeting-notes.md"])
        await model.settleRanking()
        model.query = "notes"
        await model.settleRanking()

        XCTAssertEqual(model.displayOrder.first?.path, "notes.md")
    }

    @MainActor
    func testNonEmptyQueryExcludesNonMatches() async {
        let model = makeModel()
        model.load(
            files: candidates([("foo.md", "foo.md"), ("bar.md", "bar.md")]),
            recents: [])
        await model.settleRanking()
        model.query = "foo"
        await model.settleRanking()

        XCTAssertEqual(model.displayOrder.map(\.path), ["foo.md"])
    }

    // MARK: - Selection

    @MainActor
    func testLoadSelectsFirstRow() async {
        let model = makeModel()
        model.load(
            files: candidates([("a.md", "a.md"), ("b.md", "b.md")]),
            recents: [])
        await model.settleRanking()
        XCTAssertEqual(model.selectedID, "a.md")
    }

    @MainActor
    func testQueryChangeSnapsSelectionToFirstMatch() async {
        let model = makeModel()
        model.load(
            files: candidates([("a.md", "a.md"), ("b.md", "b.md")]),
            recents: [])
        await model.settleRanking()
        model.selectedID = "b.md"
        model.query = "a"
        await model.settleRanking()
        XCTAssertEqual(model.selectedID, "a.md")
    }

    @MainActor
    func testQueryChangeRetainsSelectionWhenItRemainsRanked() async {
        let model = makeModel()
        model.load(
            files: candidates([
                ("alpha/note.md", "note.md"), ("beta/note.md", "note.md"),
            ]),
            recents: [])
        await model.settleRanking()
        model.selectedID = "beta/note.md"

        model.query = "note"
        XCTAssertEqual(model.selectedID, "beta/note.md")
        XCTAssertNil(model.selectedRow)
        await model.settleRanking()

        XCTAssertEqual(model.displayOrder.first?.id, "alpha/note.md")
        XCTAssertEqual(model.selectedID, "beta/note.md")
    }

    @MainActor
    func testQueryChangeWithNoMatchesNilsSelection() async {
        let model = makeModel()
        model.load(files: candidates([("a.md", "a.md")]), recents: [])
        await model.settleRanking()
        model.query = "zzz"
        await model.settleRanking()
        XCTAssertNil(model.selectedID)
    }

    @MainActor
    func testSelectNextWrapsAtEnd() async {
        let model = makeModel()
        model.load(
            files: candidates([
                ("a.md", "a.md"), ("b.md", "b.md"), ("c.md", "c.md"),
            ]),
            recents: [])
        await model.settleRanking()
        model.selectNext(); XCTAssertEqual(model.selectedID, "b.md")
        model.selectNext(); XCTAssertEqual(model.selectedID, "c.md")
        model.selectNext(); XCTAssertEqual(model.selectedID, "a.md")
    }

    @MainActor
    func testSelectPreviousWrapsAtStart() async {
        let model = makeModel()
        model.load(
            files: candidates([
                ("a.md", "a.md"), ("b.md", "b.md"), ("c.md", "c.md"),
            ]),
            recents: [])
        await model.settleRanking()
        model.selectPrevious()
        XCTAssertEqual(model.selectedID, "c.md")
    }

    @MainActor
    func testSelectedRowResolvesToDisplayedRow() async {
        let model = makeModel()
        model.load(
            files: candidates([("a.md", "a.md"), ("b.md", "b.md")]),
            recents: [])
        await model.settleRanking()
        model.selectedID = "b.md"
        XCTAssertEqual(model.selectedRow?.path, "b.md")
    }

    // MARK: - Bounded background publication

    @MainActor
    func testDefaultModelsShareProcessScopedWorker() {
        let first = makeModel()
        let second = makeModel()

        XCTAssertTrue(first.rankingWorkerForTesting === second.rankingWorkerForTesting)
    }

    @MainActor
    func testDisplayOrderCapsButAnnouncementReportsTotal() async {
        let model = makeModel()
        let many = (0..<(QuickSwitcherModel.displayCap + 20)).map {
            ("note\($0).md", "note\($0).md")
        }
        model.load(files: candidates(many), recents: [])
        await model.settleRanking()
        model.query = "note"
        await model.settleRanking()

        XCTAssertEqual(model.displayOrder.count, QuickSwitcherModel.displayCap)
        XCTAssertEqual(model.matches.count, QuickSwitcherModel.displayCap)
        XCTAssertEqual(
            model.resultAnnouncement,
            .switcherMatchCount(
                count: UInt32(QuickSwitcherModel.displayCap + 20),
                query: "note"
            ))
    }

    @MainActor
    func testRankingRunsOffMainAndPublishesAfterTheWorkerCompletes() async {
        let entered = DispatchSemaphore(value: 0)
        let release = DispatchSemaphore(value: 0)
        let ranOnMain = LockedBool()
        let worker = QuickSwitcherModel.RankingWorker {
            files, _, _, _ in
            ranOnMain.set(Thread.isMainThread)
            entered.signal()
            _ = release.wait(timeout: .now() + 15)
            let file = files[0]
            return QuickSwitcherModel.RankingPage(
                rows: [
                    QuickSwitcherModel.FileRow(
                        path: file.path,
                        name: file.name,
                        displayName: "A"
                    )
                ],
                total: 1
            )
        }
        let model = QuickSwitcherModel(debounceNanoseconds: 0, rankingWorker: worker)

        model.load(files: candidates([("a.md", "a.md")]), recents: [])
        let enteredInTime = await Task.detached {
            entered.wait(timeout: .now() + 10) == .success
        }.value
        XCTAssertTrue(enteredInTime)
        XCTAssertFalse(ranOnMain.value)
        XCTAssertTrue(model.isRanking)
        XCTAssertTrue(model.displayOrder.isEmpty)

        release.signal()
        await model.settleRanking()
        XCTAssertFalse(model.isRanking)
        XCTAssertEqual(model.displayOrder.map(\.path), ["a.md"])
    }

    @MainActor
    func testSupersededWorkerCannotPublishAfterANewerQuery() async {
        let oldEntered = DispatchSemaphore(value: 0)
        let releaseOld = DispatchSemaphore(value: 0)
        let oldFinished = DispatchSemaphore(value: 0)
        let concurrency = ConcurrencyProbe()
        let worker = QuickSwitcherModel.RankingWorker {
            files, query, _, _ in
            concurrency.enter()
            defer { concurrency.leave() }
            if query == "old" {
                oldEntered.signal()
                _ = releaseOld.wait(timeout: .now() + 15)
                oldFinished.signal()
            }
            let path = query == "new" ? "new.md" : files[0].path
            return QuickSwitcherModel.RankingPage(
                rows: [
                    QuickSwitcherModel.FileRow(
                        path: path,
                        name: path,
                        displayName: query.isEmpty ? "Old" : query
                    )
                ],
                total: 1
            )
        }
        let model = QuickSwitcherModel(debounceNanoseconds: 0, rankingWorker: worker)
        model.load(
            files: candidates([("old.md", "old.md"), ("new.md", "new.md")]),
            recents: [])
        await model.settleRanking()

        model.query = "old"
        let oldEnteredInTime = await Task.detached {
            oldEntered.wait(timeout: .now() + 10) == .success
        }.value
        XCTAssertTrue(oldEnteredInTime)
        model.query = "new"
        XCTAssertTrue(model.displayOrder.isEmpty)
        XCTAssertNil(model.resultAnnouncement)

        releaseOld.signal()
        let oldFinishedInTime = await Task.detached {
            oldFinished.wait(timeout: .now() + 10) == .success
        }.value
        XCTAssertTrue(oldFinishedInTime)
        await model.settleSupersededRanking()
        await model.settleRanking()
        XCTAssertEqual(model.displayOrder.map(\.path), ["new.md"])
        XCTAssertEqual(model.rankingPublicationCountForTesting, 2)
        XCTAssertEqual(concurrency.maximumObserved, 1)
    }

    @MainActor
    func testSharedWorkerSerializesRanksAcrossModelLifetimes() async {
        let firstEntered = DispatchSemaphore(value: 0)
        let releaseFirst = DispatchSemaphore(value: 0)
        let secondRequested = DispatchSemaphore(value: 0)
        let concurrency = ConcurrencyProbe()
        let worker = QuickSwitcherModel.RankingWorker { files, _, _, _ in
            concurrency.enter()
            defer { concurrency.leave() }
            if files[0].path == "first.md" {
                firstEntered.signal()
                _ = releaseFirst.wait(timeout: .now() + 15)
            }
            let file = files[0]
            return QuickSwitcherModel.RankingPage(
                rows: [
                    QuickSwitcherModel.FileRow(
                        path: file.path, name: file.name, displayName: file.name)
                ],
                total: 1)
        }
        let first = QuickSwitcherModel(debounceNanoseconds: 0, rankingWorker: worker)
        let second = QuickSwitcherModel(
            debounceNanoseconds: 0,
            rankingWorker: worker,
            rankingRequestObserverForTesting: { secondRequested.signal() })

        first.load(files: candidates([("first.md", "first.md")]), recents: [])
        let enteredInTime = await Task.detached {
            firstEntered.wait(timeout: .now() + 10) == .success
        }.value
        XCTAssertTrue(enteredInTime)
        first.cancel()
        second.load(files: candidates([("second.md", "second.md")]), recents: [])
        let secondRequestedInTime = await Task.detached {
            secondRequested.wait(timeout: .now() + 10) == .success
        }.value
        XCTAssertTrue(secondRequestedInTime)
        XCTAssertEqual(concurrency.maximumObserved, 1)

        releaseFirst.signal()
        await first.settleSupersededRanking()
        await second.settleRanking()

        XCTAssertEqual(concurrency.maximumObserved, 1)
        XCTAssertEqual(first.rankingPublicationCountForTesting, 0)
        XCTAssertEqual(second.displayOrder.map(\.path), ["second.md"])
        XCTAssertEqual(second.rankingPublicationCountForTesting, 1)
    }

    @MainActor
    func testCancellationDuringNativeRankPreventsLatePublication() async {
        let entered = DispatchSemaphore(value: 0)
        let release = DispatchSemaphore(value: 0)
        let worker = QuickSwitcherModel.RankingWorker { files, _, _, _ in
            entered.signal()
            _ = release.wait(timeout: .now() + 15)
            let file = files[0]
            return QuickSwitcherModel.RankingPage(
                rows: [
                    QuickSwitcherModel.FileRow(
                        path: file.path, name: file.name, displayName: file.name)
                ],
                total: 1)
        }
        let model = QuickSwitcherModel(debounceNanoseconds: 0, rankingWorker: worker)
        model.load(files: candidates([("a.md", "a.md")]), recents: [])
        model.announceInitialCount()
        let enteredInTime = await Task.detached {
            entered.wait(timeout: .now() + 10) == .success
        }.value
        XCTAssertTrue(enteredInTime)

        model.cancel()
        XCTAssertNil(model.resultAnnouncement)
        XCTAssertFalse(model.isRanking)
        XCTAssertTrue(model.displayOrder.isEmpty)
        release.signal()
        await model.settleSupersededRanking()

        XCTAssertNil(model.resultAnnouncement)
        XCTAssertTrue(model.displayOrder.isEmpty)
        XCTAssertEqual(model.rankingPublicationCountForTesting, 0)
    }

    // MARK: - Result-count announcements

    @MainActor
    func testInitialAnnouncementWaitsForTheOpeningRank() async {
        let model = makeModel()
        model.load(
            files: candidates([("a.md", "a.md"), ("b.md", "b.md")]),
            recents: [])
        model.announceInitialCount()
        XCTAssertNil(model.resultAnnouncement)
        await model.settleRanking()
        XCTAssertEqual(model.resultAnnouncement, .switcherRecentCount(count: 2))
        XCTAssertEqual(renderedText(model), "2 recent files")
    }

    @MainActor
    func testSupersessionAndDismissalClearPendingAnnouncements() async {
        let model = makeModel()
        model.load(files: candidates([("a.md", "a.md")]), recents: [])
        model.announceInitialCount()
        await model.settleRanking()
        XCTAssertNotNil(model.resultAnnouncement)
        let publications = model.rankingPublicationCountForTesting

        model.query = "a"
        XCTAssertNil(model.resultAnnouncement)
        model.cancel()
        await model.settleSupersededRanking()

        XCTAssertNil(model.resultAnnouncement)
        XCTAssertFalse(model.isRanking)
        XCTAssertTrue(model.displayOrder.isEmpty)
        XCTAssertEqual(model.rankingPublicationCountForTesting, publications)
    }

    @MainActor
    func testAnnouncementZeroRecents() async {
        let model = makeModel()
        model.load(files: [], recents: [])
        model.announceInitialCount()
        await model.settleRanking()
        XCTAssertEqual(model.resultAnnouncement, .switcherRecentCount(count: 0))
        XCTAssertEqual(renderedText(model), "0 recent files")
    }

    @MainActor
    func testAnnouncementSingularRecent() async {
        let model = makeModel()
        model.load(files: candidates([("a.md", "a.md")]), recents: [])
        model.announceInitialCount()
        await model.settleRanking()
        XCTAssertEqual(model.resultAnnouncement, .switcherRecentCount(count: 1))
        XCTAssertEqual(renderedText(model), "1 recent file")
    }

    @MainActor
    func testAnnouncementReportsMatchCount() async {
        let model = makeModel()
        model.load(
            files: candidates([("foo.md", "foo.md"), ("food.md", "food.md")]),
            recents: [])
        await model.settleRanking()
        model.query = "foo"
        await model.settleRanking()
        XCTAssertEqual(model.resultAnnouncement, .switcherMatchCount(count: 2, query: "foo"))
        XCTAssertEqual(renderedText(model), "2 files matching \"foo\"")
    }

    @MainActor
    func testAnnouncementSingularMatch() async {
        let model = makeModel()
        model.load(
            files: candidates([("foo.md", "foo.md"), ("bar.md", "bar.md")]),
            recents: [])
        await model.settleRanking()
        model.query = "foo"
        await model.settleRanking()
        XCTAssertEqual(model.resultAnnouncement, .switcherMatchCount(count: 1, query: "foo"))
        XCTAssertEqual(renderedText(model), "1 file matching \"foo\"")
    }

    @MainActor
    func testAnnouncementNoMatches() async {
        let model = makeModel()
        model.load(files: candidates([("foo.md", "foo.md")]), recents: [])
        await model.settleRanking()
        model.query = "zzz"
        await model.settleRanking()
        XCTAssertEqual(model.resultAnnouncement, .switcherNoMatches(query: "zzz"))
        XCTAssertEqual(renderedText(model), "No files matching \"zzz\"")
    }

    @MainActor
    func testClearAnnouncementResetsToNil() async {
        let model = makeModel()
        model.load(files: candidates([("a.md", "a.md")]), recents: [])
        model.announceInitialCount()
        await model.settleRanking()
        XCTAssertNotNil(model.resultAnnouncement)
        model.clearAnnouncement()
        XCTAssertNil(model.resultAnnouncement)
    }

    @MainActor
    private func renderedText(_ model: QuickSwitcherModel) -> String? {
        model.resultAnnouncement.map { a11yRender(event: $0).text }
    }
}
