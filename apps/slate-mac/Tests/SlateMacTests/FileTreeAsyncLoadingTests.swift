// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation
import XCTest

@testable import SlateMac

private enum FileTreeAsyncTestTimeout: Error {
    case providerGate
}

private func waitForFileTreeGate(
    _ gate: DispatchSemaphore,
    timeout: TimeInterval = 20
) throws {
    guard gate.wait(timeout: .now() + timeout) == .success else {
        throw FileTreeAsyncTestTimeout.providerGate
    }
}

@MainActor
final class FileTreeAsyncLoadingTests: XCTestCase {
    private final class ConcurrentProbe: @unchecked Sendable {
        private let lock = NSLock()
        private var _calls: [String] = []
        private var _active = 0
        private var _maximum = 0
        private var _mainThreadEntries = 0

        func entered(_ path: String) {
            lock.lock()
            _calls.append(path)
            _active += 1
            _maximum = max(_maximum, _active)
            if Thread.isMainThread {
                _mainThreadEntries += 1
            }
            lock.unlock()
        }

        func left() {
            lock.lock()
            _active -= 1
            lock.unlock()
        }

        var snapshot: (calls: [String], maximum: Int, mainThreadEntries: Int) {
            lock.lock()
            defer { lock.unlock() }
            return (_calls, _maximum, _mainThreadEntries)
        }
    }

    private final class PreparationProbe: @unchecked Sendable {
        private let lock = NSLock()
        private var events: [FileTreePreparationEvent] = []

        func record(_ event: FileTreePreparationEvent) {
            lock.lock()
            events.append(event)
            lock.unlock()
        }

        func reset() {
            lock.lock()
            events = []
            lock.unlock()
        }

        var snapshot: [FileTreePreparationEvent] {
            lock.lock()
            defer { lock.unlock() }
            return events
        }
    }

    nonisolated private func dir(
        _ id: Int64,
        _ path: String,
        dirCount: Int = 0,
        fileCount: Int = 0
    ) -> DirNodeSummary {
        DirNodeSummary(
            id: id,
            path: path,
            name: (path as NSString).lastPathComponent,
            childDirCount: UInt32(dirCount),
            childFileCount: UInt32(fileCount),
            hasFolderNote: false)
    }

    nonisolated private func file(
        _ path: String,
        mtimeMs: Int64 = 0,
        displayName: String? = nil
    ) -> FileSummary {
        FileSummary(
            path: path,
            name: (path as NSString).lastPathComponent,
            mtimeMs: mtimeMs,
            sizeBytes: 0,
            isMarkdown: true,
            displayName: displayName,
            createdDate: nil,
            createdMs: nil,
            wordCount: nil,
            preview: nil,
            taskTotal: 0,
            taskOpen: 0)
    }

    nonisolated private func listing(
        dirs: [DirNodeSummary] = [],
        files: [FileSummary] = [],
        nextCursor: String? = nil
    ) -> DirListing {
        DirListing(
            dirs: dirs,
            files: FileSummaryPage(
                items: files,
                nextCursor: nextCursor,
                totalFiltered: UInt64(files.count)))
    }

    private func waitForCompletion(
        of task: Task<Void, Never>,
        description: String
    ) async {
        let completed = expectation(description: description)
        Task {
            await task.value
            completed.fulfill()
        }
        await fulfillment(of: [completed], timeout: 5)
    }

    private func assertSettles(
        _ vm: FileTreeViewModel,
        timeoutNanoseconds: UInt64 = 5_000_000_000,
        file: StaticString = #filePath,
        line: UInt = #line
    ) async {
        let settled = await vm.settleLevelLoadsForTesting(
            timeoutNanoseconds: timeoutNanoseconds)
        XCTAssertTrue(settled, file: file, line: line)
    }

    private func assertRetirementsSettle(
        _ worker: FileTreeLevelWorker
    ) async {
        let settled = expectation(description: "retirement queue settled")
        Task {
            await worker.settleRetirementsForTesting()
            settled.fulfill()
        }
        await fulfillment(of: [settled], timeout: 5)
    }

    func testBlockedRootReturnsPromptlyAndRebindPublishesOnlyNewestLevel()
        async throws
    {
        let entered = expectation(description: "old root provider entered")
        let release = DispatchSemaphore(value: 0)
        let probe = ConcurrentProbe()
        let stale = listing(files: [file("stale.md")])
        let newest = listing(files: [file("newest.md")])
        let vm = FileTreeViewModel()
        defer {
            vm.cancelPendingLoads()
            release.signal()
        }

        vm.bindAsynchronouslyForTesting { parent, _, _ in
            probe.entered(parent)
            defer { probe.left() }
            entered.fulfill()
            try waitForFileTreeGate(release)
            return stale
        }
        XCTAssertEqual(
            vm.fetchState[FileTreeViewModel.rootFetchKey],
            .loading,
            "loading publishes before page-one provider entry")
        XCTAssertTrue(vm.rootLevel.isEmpty)
        let staleTask = try XCTUnwrap(
            vm.levelDrainTasksForTesting[FileTreeViewModel.rootFetchKey])
        await fulfillment(of: [entered], timeout: 5)

        vm.bindAsynchronouslyForTesting { parent, _, _ in
            probe.entered(parent)
            defer { probe.left() }
            return newest
        }
        release.signal()
        await waitForCompletion(of: staleTask, description: "stale root finished")
        await assertSettles(vm)

        XCTAssertEqual(vm.rootLevel.map(\.path), ["newest.md"])
        XCTAssertNil(vm.fetchState[FileTreeViewModel.rootFetchKey])
        XCTAssertEqual(probe.snapshot.mainThreadEntries, 0)
    }

    func testBlockedChildCollapseCancelsAndReexpandRetries() async throws {
        let entered = expectation(description: "child provider entered")
        let cancelled = expectation(description: "child native token cancelled")
        let forceRelease = DispatchSemaphore(value: 0)
        let probe = ConcurrentProbe()
        let root = listing(dirs: [dir(1, "notes", fileCount: 1)])
        let recovered = listing(files: [file("notes/recovered.md")])
        let lock = NSLock()
        var childCallCount = 0
        let vm = FileTreeViewModel()
        defer {
            vm.cancelPendingLoads()
            forceRelease.signal()
        }

        vm.bindAsynchronouslyForTesting { parent, _, cancel in
            probe.entered(parent)
            defer { probe.left() }
            guard parent == "notes" else { return root }
            lock.lock()
            childCallCount += 1
            let call = childCallCount
            lock.unlock()
            if call == 1 {
                entered.fulfill()
                while !cancel.isCancelled() {
                    if forceRelease.wait(timeout: .now() + 0.01) == .success {
                        throw CancellationError()
                    }
                }
                cancelled.fulfill()
                throw VaultError.Cancelled
            }
            return recovered
        }
        await assertSettles(vm)
        let notes = try XCTUnwrap(vm.rootLevel.first)

        vm.expand(notes)
        XCTAssertEqual(vm.fetchState[notes.nodeID], .loading)
        XCTAssertNil(vm.children[notes.nodeID])
        let cancelledTask = try XCTUnwrap(
            vm.levelDrainTasksForTesting[notes.nodeID])
        await fulfillment(of: [entered], timeout: 5)

        vm.collapse(notes)
        await fulfillment(of: [cancelled], timeout: 5)
        forceRelease.signal()
        await waitForCompletion(
            of: cancelledTask, description: "cancelled child finished")
        XCTAssertFalse(vm.expanded.contains(notes.nodeID))
        XCTAssertNil(vm.fetchState[notes.nodeID])
        XCTAssertNil(vm.children[notes.nodeID])

        vm.expand(notes)
        await assertSettles(vm)
        XCTAssertEqual(
            vm.children[notes.nodeID]?.map(\.path),
            ["notes/recovered.md"])
        XCTAssertNil(vm.fetchState[notes.nodeID])
        XCTAssertEqual(probe.snapshot.maximum, 1)
        XCTAssertEqual(probe.snapshot.mainThreadEntries, 0)
    }

    func testActiveFailurePublishesRetryAndRetryClearsIt() async {
        let failed = expectation(description: "first root failed")
        let lock = NSLock()
        var callCount = 0
        let recovered = listing(files: [file("recovered.md")])
        let vm = FileTreeViewModel()
        defer { vm.cancelPendingLoads() }

        vm.bindAsynchronouslyForTesting { _, _, _ in
            lock.lock()
            callCount += 1
            let call = callCount
            lock.unlock()
            if call == 1 {
                failed.fulfill()
                throw VaultError.Db(message: "simulated")
            }
            return recovered
        }
        await fulfillment(of: [failed], timeout: 5)
        await assertSettles(vm)

        guard case .failed(let message) =
            vm.fetchState[FileTreeViewModel.rootFetchKey]
        else {
            return XCTFail("active provider failure must expose Retry")
        }
        XCTAssertEqual(message, "Couldn't load this folder.")
        XCTAssertTrue(vm.rootLevel.isEmpty)

        vm.retryRootLoad()
        XCTAssertEqual(
            vm.fetchState[FileTreeViewModel.rootFetchKey],
            .loading)
        await assertSettles(vm)
        XCTAssertEqual(vm.rootLevel.map(\.path), ["recovered.md"])
        XCTAssertNil(vm.fetchState[FileTreeViewModel.rootFetchKey])
    }

    func testContinuationFailureKeepsPreparedPrefixAndRetryRefetches()
        async
    {
        final class FailureBox: @unchecked Sendable {
            private let lock = NSLock()
            private var failing = true
            private var rootStarts = 0

            func startedRoot() {
                lock.lock()
                rootStarts += 1
                lock.unlock()
            }

            func heal() {
                lock.lock()
                failing = false
                lock.unlock()
            }

            var snapshot: (failing: Bool, rootStarts: Int) {
                lock.lock()
                defer { lock.unlock() }
                return (failing, rootStarts)
            }
        }

        let box = FailureBox()
        let vm = FileTreeViewModel()
        defer { vm.cancelPendingLoads() }
        vm.bindAsynchronouslyForTesting { [self] _, cursor, _ in
            if cursor == nil {
                box.startedRoot()
                return listing(
                    files: [file("page-one.md")],
                    nextCursor: "page-2")
            }
            if box.snapshot.failing {
                throw VaultError.Db(message: "page two boom")
            }
            return listing(files: [file("page-two.md")])
        }

        await assertSettles(vm)
        XCTAssertEqual(vm.rootLevel.map(\.path), ["page-one.md"])
        guard case .failed(let message) =
            vm.fetchState[FileTreeViewModel.rootFetchKey]
        else {
            return XCTFail("a failed continuation must expose Retry")
        }
        XCTAssertEqual(message, "Couldn't load this folder.")
        XCTAssertFalse(message.contains("page two boom"))
        XCTAssertEqual(box.snapshot.rootStarts, 1)

        box.heal()
        vm.retryRootLoad()
        await assertSettles(vm)
        XCTAssertEqual(
            vm.rootLevel.map(\.path),
            ["page-one.md", "page-two.md"])
        XCTAssertNil(vm.fetchState[FileTreeViewModel.rootFetchKey])
        XCTAssertEqual(box.snapshot.rootStarts, 2)
    }

    func testChildContinuationFailureKeepsPreparedPrefixAndRetryRefetches()
        async throws
    {
        final class FailureBox: @unchecked Sendable {
            private let lock = NSLock()
            private var failing = true
            private var childStarts = 0

            func startedChild() {
                lock.lock()
                childStarts += 1
                lock.unlock()
            }

            func heal() {
                lock.lock()
                failing = false
                lock.unlock()
            }

            var snapshot: (failing: Bool, childStarts: Int) {
                lock.lock()
                defer { lock.unlock() }
                return (failing, childStarts)
            }
        }

        let box = FailureBox()
        let vm = FileTreeViewModel()
        defer { vm.cancelPendingLoads() }
        vm.bindAsynchronouslyForTesting { [self] parent, cursor, _ in
            guard parent == "notes" else {
                return listing(
                    dirs: [dir(1, "notes", fileCount: 2)])
            }
            if cursor == nil {
                box.startedChild()
                return listing(
                    files: [file("notes/page-one.md")],
                    nextCursor: "page-2")
            }
            if box.snapshot.failing {
                throw VaultError.Db(message: "child page two boom")
            }
            return listing(files: [file("notes/page-two.md")])
        }

        await assertSettles(vm)
        let notes = try XCTUnwrap(vm.rootLevel.first)
        vm.expand(notes)
        await assertSettles(vm)
        XCTAssertEqual(
            vm.children[notes.nodeID]?.map(\.path),
            ["notes/page-one.md"])
        guard case .failed(let message) = vm.fetchState[notes.nodeID] else {
            return XCTFail("a failed child continuation must expose Retry")
        }
        XCTAssertEqual(message, "Couldn't load this folder.")
        XCTAssertFalse(message.contains("child page two boom"))
        XCTAssertEqual(box.snapshot.childStarts, 1)

        box.heal()
        vm.loadChildren(of: notes)
        await assertSettles(vm)
        XCTAssertEqual(
            vm.children[notes.nodeID]?.map(\.path),
            ["notes/page-one.md", "notes/page-two.md"])
        XCTAssertNil(vm.fetchState[notes.nodeID])
        XCTAssertEqual(box.snapshot.childStarts, 2)
    }

    func testCollapsedPartialChildRetryDoesNotInheritSameIDCache()
        async throws
    {
        final class RetryBox: @unchecked Sendable {
            private let lock = NSLock()
            private var failing = true
            private var replacementChildCalls = 0

            func heal() {
                lock.lock()
                failing = false
                lock.unlock()
            }

            func calledReplacementChild() {
                lock.lock()
                replacementChildCalls += 1
                lock.unlock()
            }

            var snapshot: (failing: Bool, replacementChildCalls: Int) {
                lock.lock()
                defer { lock.unlock() }
                return (failing, replacementChildCalls)
            }
        }

        let box = RetryBox()
        let vm = FileTreeViewModel()
        defer { vm.cancelPendingLoads() }
        vm.bindAsynchronouslyForTesting { [self] parent, cursor, _ in
            switch parent {
            case "":
                return listing(
                    dirs: [dir(1, "notes", dirCount: 1)])
            case "notes":
                if box.snapshot.failing {
                    if cursor == nil {
                        return listing(
                            dirs: [dir(2, "notes/old", fileCount: 1)],
                            nextCursor: "page-2")
                    }
                    throw VaultError.Db(message: "child page two failed")
                }
                return listing(
                    dirs: [dir(2, "notes/new", fileCount: 1)])
            case "notes/old":
                return listing(files: [file("notes/old/stale.md")])
            case "notes/new":
                box.calledReplacementChild()
                return listing(files: [file("notes/new/fresh.md")])
            default:
                return listing()
            }
        }

        await assertSettles(vm)
        let notes = try XCTUnwrap(vm.rootLevel.first)
        vm.expand(notes)
        await assertSettles(vm)
        let old = try XCTUnwrap(vm.children[notes.nodeID]?.first)
        vm.expand(old)
        await assertSettles(vm)
        XCTAssertEqual(
            vm.visibleRows.map(\.path),
            ["notes", "notes/old", "notes/old/stale.md"])

        vm.collapse(notes)
        XCTAssertFalse(vm.expanded.contains(notes.nodeID))
        XCTAssertTrue(vm.expanded.contains(old.nodeID))
        box.heal()
        vm.expand(notes)
        await assertSettles(vm)

        XCTAssertEqual(
            vm.children[notes.nodeID]?.map(\.path),
            ["notes/new"])
        XCTAssertFalse(vm.expanded.contains(old.nodeID))
        XCTAssertNil(vm.children[old.nodeID])
        XCTAssertEqual(
            vm.visibleRows.map(\.path),
            ["notes", "notes/new"])
        XCTAssertEqual(box.snapshot.replacementChildCalls, 0)
        XCTAssertNil(vm.fetchState[notes.nodeID])
    }

    func testPartialChildRetryBatchesFiftyThousandShallowRows()
        async throws
    {
        final class FailureBox: @unchecked Sendable {
            private let lock = NSLock()
            private var failing = true
            func heal() { lock.withLock { failing = false } }
            var snapshot: Bool { lock.withLock { failing } }
        }

        let box = FailureBox()
        let shallowCount = FileTreeViewModel.levelTotalSafetyCap - 1
        let shallow = (0..<shallowCount).map {
            dir(Int64($0 + 2), "notes/folder-\($0)")
        }
        let vm = FileTreeViewModel()
        defer { vm.cancelPendingLoads() }
        vm.bindAsynchronouslyForTesting { parent, cursor, _ in
            switch parent {
            case "":
                return self.listing(
                    dirs: [self.dir(1, "notes", dirCount: shallowCount)])
            case "notes":
                if box.snapshot {
                    if cursor == nil {
                        return self.listing(
                            dirs: shallow, nextCursor: "page-2")
                    }
                    throw VaultError.Db(message: "continuation failed")
                }
                return self.listing()
            default:
                return self.listing()
            }
        }
        await assertSettles(vm)
        let notes = try XCTUnwrap(vm.rootLevel.first)
        vm.expand(notes)
        await assertSettles(vm, timeoutNanoseconds: 20_000_000_000)
        guard case .failed = vm.fetchState[notes.nodeID] else {
            return XCTFail("partial child level must expose Retry")
        }

        let revisionBeforeRetry = vm.presentationRevision
        box.heal()
        vm.loadChildren(of: notes)
        XCTAssertEqual(
            vm.presentationRevision, revisionBeforeRetry + 1,
            "reset publishes the owning level once before async replacement")
        XCTAssertEqual(
            vm.descendantResetCandidateVisitCountForTesting, shallowCount)
        XCTAssertEqual(vm.descendantResetBatchPublicationCountForTesting, 0)
        XCTAssertEqual(vm.descendantResetRetirementHandoffCountForTesting, 0)
        await assertSettles(vm, timeoutNanoseconds: 20_000_000_000)
        XCTAssertEqual(vm.children[notes.nodeID]?.count, 0)
        XCTAssertNil(vm.fetchState[notes.nodeID])
    }

    func testPartialChildRetryBatchesManyCachedDescendantLevels()
        async throws
    {
        final class FailureBox: @unchecked Sendable {
            private let lock = NSLock()
            private var failing = true
            func heal() { lock.withLock { failing = false } }
            var snapshot: Bool { lock.withLock { failing } }
        }

        let box = FailureBox()
        let childCount = 128
        let shallow = (0..<childCount).map {
            dir(Int64($0 + 2), "notes/child-\($0)", fileCount: 1)
        }
        let vm = FileTreeViewModel()
        defer { vm.cancelPendingLoads() }
        vm.bindAsynchronouslyForTesting { parent, cursor, _ in
            switch parent {
            case "":
                return self.listing(
                    dirs: [self.dir(1, "notes", dirCount: childCount)])
            case "notes":
                if box.snapshot {
                    if cursor == nil {
                        return self.listing(
                            dirs: shallow, nextCursor: "page-2")
                    }
                    throw VaultError.Db(message: "continuation failed")
                }
                return self.listing()
            default:
                return self.listing(
                    files: [self.file("\(parent)/cached.md")])
            }
        }
        await assertSettles(vm)
        let notes = try XCTUnwrap(vm.rootLevel.first)
        vm.expand(notes)
        await assertSettles(vm)
        let oldChildren = try XCTUnwrap(vm.children[notes.nodeID])
        for child in oldChildren { vm.expand(child) }
        await assertSettles(vm, timeoutNanoseconds: 20_000_000_000)
        XCTAssertEqual(
            oldChildren.filter { vm.children[$0.nodeID] != nil }.count,
            childCount)

        let revisionBeforeRetry = vm.presentationRevision
        box.heal()
        vm.loadChildren(of: notes)
        XCTAssertEqual(vm.presentationRevision, revisionBeforeRetry + 2)
        XCTAssertEqual(
            vm.descendantResetCandidateVisitCountForTesting, childCount)
        XCTAssertEqual(vm.descendantResetBatchPublicationCountForTesting, 1)
        XCTAssertEqual(vm.descendantResetRetirementHandoffCountForTesting, 1)
        XCTAssertTrue(oldChildren.allSatisfy { vm.children[$0.nodeID] == nil })
        await assertSettles(vm)
        XCTAssertEqual(vm.children[notes.nodeID]?.count, 0)
        XCTAssertNil(vm.fetchState[notes.nodeID])
    }

    func testTargetedPartialRootKeepsLateCacheButHonorsCollapseAll()
        async throws
    {
        final class RefreshBox: @unchecked Sendable {
            private let lock = NSLock()
            private var refreshing = false
            private var failing = true

            func beginRefresh() {
                lock.lock()
                refreshing = true
                lock.unlock()
            }

            func heal() {
                lock.lock()
                failing = false
                lock.unlock()
            }

            var snapshot: (refreshing: Bool, failing: Bool) {
                lock.lock()
                defer { lock.unlock() }
                return (refreshing, failing)
            }
        }

        let box = RefreshBox()
        let vm = FileTreeViewModel()
        defer { vm.cancelPendingLoads() }
        vm.bindAsynchronouslyForTesting { [self] parent, cursor, _ in
            if parent == "late" {
                return listing(files: [file("late/cached.md")])
            }
            let state = box.snapshot
            guard state.refreshing else {
                return listing(
                    dirs: [
                        dir(1, "early"),
                        dir(2, "late", fileCount: 1)
                    ])
            }
            if cursor == nil {
                return listing(
                    dirs: [dir(1, "early")],
                    nextCursor: "page-2")
            }
            if state.failing {
                throw VaultError.Db(message: "late page unavailable")
            }
            return listing(dirs: [dir(2, "late", fileCount: 1)])
        }

        await assertSettles(vm)
        let late = try XCTUnwrap(vm.rootLevel.last)
        vm.expand(late)
        await assertSettles(vm)
        XCTAssertEqual(
            vm.children[late.nodeID]?.map(\.path),
            ["late/cached.md"])

        box.beginRefresh()
        vm.rootLevelInvalidation()
        await assertSettles(vm)
        XCTAssertEqual(vm.rootLevel.map(\.path), ["early"])
        XCTAssertTrue(vm.expanded.contains(late.nodeID))
        XCTAssertEqual(
            vm.children[late.nodeID]?.map(\.path),
            ["late/cached.md"])
        XCTAssertEqual(
            vm.fetchState[FileTreeViewModel.rootFetchKey],
            .failed(message: "Couldn't load this folder."))
        XCTAssertTrue(vm.collapseAllPreservingAncestors(ofPath: nil))
        XCTAssertFalse(vm.expanded.contains(late.nodeID))
        XCTAssertTrue(vm.expansionRecency.isEmpty)

        box.heal()
        vm.retryRootLoad()
        await assertSettles(vm)
        XCTAssertEqual(vm.rootLevel.map(\.path), ["early", "late"])
        XCTAssertFalse(vm.expanded.contains(late.nodeID))
        XCTAssertEqual(
            vm.children[late.nodeID]?.map(\.path),
            ["late/cached.md"])
        XCTAssertEqual(
            vm.visibleRows.map(\.path),
            ["early", "late"])
        XCTAssertNil(vm.fetchState[FileTreeViewModel.rootFetchKey])
    }

    func testRootRetrySameIDPathReplacementDoesNotInheritPartialCache()
        async throws
    {
        final class RetryBox: @unchecked Sendable {
            private let lock = NSLock()
            private var failing = true
            private var replacementChildCalls = 0

            func heal() {
                lock.lock()
                failing = false
                lock.unlock()
            }

            func calledReplacementChild() {
                lock.lock()
                replacementChildCalls += 1
                lock.unlock()
            }

            var snapshot: (failing: Bool, replacementChildCalls: Int) {
                lock.lock()
                defer { lock.unlock() }
                return (failing, replacementChildCalls)
            }
        }

        let box = RetryBox()
        let vm = FileTreeViewModel()
        defer { vm.cancelPendingLoads() }
        vm.bindAsynchronouslyForTesting { [self] parent, cursor, _ in
            switch parent {
            case "a":
                return listing(files: [file("a/stale.md")])
            case "b":
                box.calledReplacementChild()
                return listing(files: [file("b/fresh.md")])
            default:
                if box.snapshot.failing {
                    if cursor == nil {
                        return listing(
                            dirs: [dir(1, "a", fileCount: 1)],
                            nextCursor: "page-2")
                    }
                    throw VaultError.Db(message: "page two failed")
                }
                return listing(
                    dirs: [dir(1, "b", fileCount: 1)])
            }
        }

        await assertSettles(vm)
        let oldA = try XCTUnwrap(vm.rootLevel.first)
        vm.expand(oldA)
        await assertSettles(vm)
        XCTAssertEqual(
            vm.visibleRows.map(\.path),
            ["a", "a/stale.md"])

        box.heal()
        vm.retryRootLoad()
        await assertSettles(vm)
        XCTAssertEqual(vm.rootLevel.map(\.path), ["b"])
        XCTAssertFalse(vm.expanded.contains(oldA.nodeID))
        XCTAssertNil(vm.children[oldA.nodeID])
        XCTAssertEqual(vm.visibleRows.map(\.path), ["b"])
        XCTAssertEqual(box.snapshot.replacementChildCalls, 0)
        XCTAssertNil(vm.fetchState[FileTreeViewModel.rootFetchKey])
    }

    func testSiblingLoadsSerializeAndCollapsedQueueNeverEntersProvider()
        async throws
    {
        let firstChildEntered = expectation(description: "first child entered")
        let releaseFirstChild = DispatchSemaphore(value: 0)
        let probe = ConcurrentProbe()
        let root = listing(
            dirs: [dir(1, "a", fileCount: 1), dir(2, "b", fileCount: 1)])
        let aChildren = listing(files: [file("a/one.md")])
        let bChildren = listing(files: [file("b/two.md")])
        let empty = listing()
        let vm = FileTreeViewModel()
        defer {
            vm.cancelPendingLoads()
            releaseFirstChild.signal()
        }

        vm.bindAsynchronouslyForTesting { parent, _, _ in
            probe.entered(parent)
            defer { probe.left() }
            switch parent {
            case "":
                return root
            case "a":
                firstChildEntered.fulfill()
                try waitForFileTreeGate(releaseFirstChild)
                return aChildren
            case "b":
                return bChildren
            default:
                return empty
            }
        }
        await assertSettles(vm)
        let a = try XCTUnwrap(vm.rootLevel.first { $0.path == "a" })
        let b = try XCTUnwrap(vm.rootLevel.first { $0.path == "b" })

        vm.expand(a)
        let aTask = try XCTUnwrap(vm.levelDrainTasksForTesting[a.nodeID])
        await fulfillment(of: [firstChildEntered], timeout: 5)
        vm.expand(b)
        let cancelledBTask = try XCTUnwrap(
            vm.levelDrainTasksForTesting[b.nodeID])
        vm.collapse(b)
        releaseFirstChild.signal()
        await waitForCompletion(of: aTask, description: "first child finished")
        await waitForCompletion(
            of: cancelledBTask, description: "queued child cancelled")
        await assertSettles(vm)

        XCTAssertEqual(
            probe.snapshot.calls,
            ["", "a"],
            "a collapsed queued request must be rejected before provider entry")
        XCTAssertEqual(probe.snapshot.maximum, 1)
        XCTAssertNil(vm.children[b.nodeID])
        XCTAssertNil(vm.fetchState[b.nodeID])

        vm.expand(b)
        await assertSettles(vm)
        XCTAssertEqual(vm.children[b.nodeID]?.map(\.path), ["b/two.md"])
        XCTAssertEqual(probe.snapshot.calls, ["", "a", "b"])
        XCTAssertEqual(probe.snapshot.maximum, 1)
    }

    func testRestoredExpansionChainLoadsSeriallyOffMain() async {
        let probe = ConcurrentProbe()
        let root = listing(dirs: [dir(1, "a", dirCount: 1)])
        let child = listing(dirs: [dir(2, "a/b", fileCount: 1)])
        let grandchild = listing(files: [file("a/b/note.md")])
        let empty = listing()
        let vm = FileTreeViewModel()
        defer { vm.cancelPendingLoads() }

        vm.bindAsynchronouslyForTesting(
            cancellablePagedFetcher: { parent, _, _ in
                probe.entered(parent)
                defer { probe.left() }
                Thread.sleep(forTimeInterval: 0.01)
                switch parent {
                case "": return root
                case "a": return child
                case "a/b": return grandchild
                default: return empty
                }
            },
            restoringExpandedDirPaths: ["a", "a/b"])

        XCTAssertEqual(
            vm.fetchState[FileTreeViewModel.rootFetchKey],
            .loading)
        XCTAssertTrue(vm.rootLevel.isEmpty)
        await assertSettles(vm)

        XCTAssertEqual(probe.snapshot.calls, ["", "a", "a/b"])
        XCTAssertEqual(probe.snapshot.maximum, 1)
        XCTAssertEqual(probe.snapshot.mainThreadEntries, 0)
        XCTAssertEqual(
            vm.visibleRows.map(\.path),
            ["a", "a/b", "a/b/note.md"])
        XCTAssertTrue(vm.expanded.contains(.dir(1)))
        XCTAssertTrue(vm.expanded.contains(.dir(2)))
    }

    func testAsyncRootRenameKeepsRemappedExpansionAndReloadsChildren()
        async throws
    {
        let refreshEntered = expectation(description: "renamed root refresh entered")
        let releaseRefresh = DispatchSemaphore(value: 0)
        let lock = NSLock()
        var rootCalls = 0
        var bCalls = 0
        let vm = FileTreeViewModel()
        defer {
            vm.cancelPendingLoads()
            releaseRefresh.signal()
        }

        vm.bindAsynchronouslyForTesting { parent, _, _ in
            switch parent {
            case "":
                lock.lock()
                rootCalls += 1
                let call = rootCalls
                lock.unlock()
                if call == 1 {
                    return self.listing(
                        dirs: [self.dir(1, "a", fileCount: 1)])
                }
                refreshEntered.fulfill()
                try waitForFileTreeGate(releaseRefresh)
                return self.listing(
                    dirs: [self.dir(1, "b", fileCount: 1)])
            case "a":
                return self.listing(files: [self.file("a/old.md")])
            case "b":
                lock.lock()
                bCalls += 1
                lock.unlock()
                return self.listing(files: [self.file("b/new.md")])
            default:
                return self.listing()
            }
        }
        await assertSettles(vm)
        let oldA = try XCTUnwrap(vm.rootLevel.first)
        vm.expand(oldA)
        await assertSettles(vm)
        XCTAssertEqual(vm.children[oldA.nodeID]?.map(\.path), ["a/old.md"])

        vm.rootLevelInvalidation()
        vm.remapExpansion(fromPrefix: "a", to: "b")
        await fulfillment(of: [refreshEntered], timeout: 5)
        releaseRefresh.signal()
        await assertSettles(vm)

        XCTAssertEqual(vm.rootLevel.map(\.path), ["b"])
        XCTAssertEqual(vm.expansionRecency, ["b"])
        XCTAssertFalse(vm.pendingExpandedPaths.contains("a"))
        XCTAssertTrue(vm.expanded.contains(.dir(1)))
        XCTAssertEqual(vm.children[.dir(1)]?.map(\.path), ["b/new.md"])
        XCTAssertFalse(vm.visibleRows.map(\.path).contains("a/old.md"))
        let completedBCalls = lock.withLock { bCalls }
        XCTAssertEqual(
            completedBCalls, 1,
            "reconciliation and outer publication must schedule b only once")
    }

    func testCollapsedRootDisclosureIsBlockedDuringSameIDReplacement()
        async throws
    {
        let replacementEntered = expectation(
            description: "same-id root replacement entered")
        let releaseReplacement = DispatchSemaphore(value: 0)
        let lock = NSLock()
        var rootCalls = 0
        var childParents: [String] = []
        let vm = FileTreeViewModel()
        defer {
            vm.cancelPendingLoads()
            releaseReplacement.signal()
        }

        vm.bindAsynchronouslyForTesting { parent, _, _ in
            if parent.isEmpty {
                let call = lock.withLock {
                    rootCalls += 1
                    return rootCalls
                }
                if call == 1 {
                    return self.listing(dirs: [self.dir(1, "a")])
                }
                replacementEntered.fulfill()
                try waitForFileTreeGate(releaseReplacement)
                return self.listing(dirs: [self.dir(1, "b")])
            }
            lock.withLock { childParents.append(parent) }
            return self.listing()
        }
        await assertSettles(vm)
        let predecessor = try XCTUnwrap(vm.rootLevel.first)

        vm.rootLevelInvalidation()
        vm.remapExpansion(fromPrefix: "a", to: "b")
        await fulfillment(of: [replacementEntered], timeout: 5)
        XCTAssertFalse(vm.expandLoadedLevels())
        vm.expand(predecessor)

        XCTAssertFalse(vm.expanded.contains(predecessor.nodeID))
        XCTAssertTrue(vm.expansionRecency.isEmpty)
        XCTAssertNil(vm.fetchState[predecessor.nodeID])
        releaseReplacement.signal()
        await assertSettles(vm)

        XCTAssertEqual(vm.rootLevel.map(\.path), ["b"])
        XCTAssertFalse(vm.expanded.contains(.dir(1)))
        XCTAssertTrue(vm.pendingExpandedPaths.isEmpty)
        XCTAssertTrue(vm.expansionRecency.isEmpty)
        XCTAssertNil(vm.fetchState[.dir(1)])
        XCTAssertTrue(lock.withLock { childParents }.isEmpty)
    }

    func testExpandedRootCollapseIsBlockedDuringSameIDReplacement()
        async throws
    {
        let replacementEntered = expectation(
            description: "expanded same-id root replacement entered")
        let releaseReplacement = DispatchSemaphore(value: 0)
        let lock = NSLock()
        var rootCalls = 0
        var childParents: [String] = []
        let vm = FileTreeViewModel()
        defer {
            vm.cancelPendingLoads()
            releaseReplacement.signal()
        }

        vm.bindAsynchronouslyForTesting { parent, _, _ in
            if parent.isEmpty {
                let call = lock.withLock {
                    rootCalls += 1
                    return rootCalls
                }
                if call == 1 {
                    return self.listing(dirs: [self.dir(1, "a")])
                }
                replacementEntered.fulfill()
                try waitForFileTreeGate(releaseReplacement)
                return self.listing(dirs: [self.dir(1, "b")])
            }
            lock.withLock { childParents.append(parent) }
            if parent == "a" {
                return self.listing(dirs: [
                    self.dir(2, "a/x"), self.dir(3, "a/y"),
                ])
            }
            if parent == "b" {
                return self.listing(dirs: [
                    self.dir(2, "b/x"), self.dir(3, "b/y"),
                ])
            }
            return self.listing()
        }
        await assertSettles(vm)
        let predecessor = try XCTUnwrap(vm.rootLevel.first)
        vm.expand(predecessor)
        await assertSettles(vm)
        let oldX = try XCTUnwrap(
            vm.children[predecessor.nodeID]?.first { $0.path == "a/x" })
        let oldY = try XCTUnwrap(
            vm.children[predecessor.nodeID]?.first { $0.path == "a/y" })
        vm.expand(oldX)
        await assertSettles(vm)

        vm.rootLevelInvalidation()
        vm.remapExpansion(fromPrefix: "a", to: "b")
        await fulfillment(of: [replacementEntered], timeout: 5)
        XCTAssertFalse(
            vm.collapseAllPreservingAncestors(ofPath: "a/x/file.md"))
        vm.collapse(oldX)
        vm.collapse(predecessor)
        vm.expand(oldY)

        XCTAssertTrue(vm.expanded.contains(predecessor.nodeID))
        XCTAssertTrue(vm.expanded.contains(oldX.nodeID))
        XCTAssertFalse(vm.expanded.contains(oldY.nodeID))
        XCTAssertEqual(vm.expansionRecency, ["b", "b/x"])
        XCTAssertEqual(
            vm.children[predecessor.nodeID]?.map(\.path), ["a/x", "a/y"])
        releaseReplacement.signal()
        await assertSettles(vm)

        XCTAssertEqual(vm.rootLevel.map(\.path), ["b"])
        XCTAssertTrue(vm.expanded.contains(.dir(1)))
        XCTAssertTrue(vm.expanded.contains(.dir(2)))
        XCTAssertFalse(vm.expanded.contains(.dir(3)))
        XCTAssertEqual(vm.expansionRecency, ["b", "b/x"])
        XCTAssertEqual(
            lock.withLock { childParents }, ["a", "a/x", "b", "b/x"])
    }

    func testRemovingStaleOldPathPreservesRemappedRunningReplacement()
        async throws
    {
        let oldChildEntered = expectation(description: "old a child entered")
        let replacementEntered = expectation(description: "replacement b entered")
        let releaseOldChild = DispatchSemaphore(value: 0)
        let releaseReplacement = DispatchSemaphore(value: 0)
        let lock = NSLock()
        var rootCalls = 0
        var childParents: [String] = []
        let vm = FileTreeViewModel()
        defer {
            vm.cancelPendingLoads()
            releaseOldChild.signal()
            releaseReplacement.signal()
        }

        vm.bindAsynchronouslyForTesting { parent, _, _ in
            if parent.isEmpty {
                let call = lock.withLock {
                    rootCalls += 1
                    return rootCalls
                }
                if call == 1 {
                    return self.listing(dirs: [self.dir(1, "a")])
                }
                replacementEntered.fulfill()
                try waitForFileTreeGate(releaseReplacement)
                return self.listing(dirs: [self.dir(1, "b")])
            }
            lock.withLock { childParents.append(parent) }
            if parent == "a" {
                oldChildEntered.fulfill()
                try waitForFileTreeGate(releaseOldChild)
                return self.listing(files: [self.file("a/stale.md")])
            }
            return self.listing(files: [self.file("b/fresh.md")])
        }
        await assertSettles(vm)
        let a = try XCTUnwrap(vm.rootLevel.first)
        vm.expand(a)
        await fulfillment(of: [oldChildEntered], timeout: 5)

        vm.rootLevelInvalidation()
        vm.remapExpansion(fromPrefix: "a", to: "b")
        releaseOldChild.signal()
        await fulfillment(of: [replacementEntered], timeout: 5)
        vm.removeExpansion(underPrefix: "a")

        XCTAssertNil(vm.children[a.nodeID])
        XCTAssertNil(vm.fetchState[a.nodeID])
        XCTAssertTrue(vm.expanded.contains(a.nodeID))
        XCTAssertEqual(vm.expansionRecency, ["b"])
        XCTAssertEqual(vm.expandedDirPaths, ["b"])
        releaseReplacement.signal()
        await assertSettles(vm)

        XCTAssertEqual(vm.rootLevel.map(\.path), ["b"])
        XCTAssertEqual(vm.children[.dir(1)]?.map(\.path), ["b/fresh.md"])
        XCTAssertFalse(vm.visibleRows.map(\.path).contains("a/stale.md"))
        XCTAssertEqual(lock.withLock { childParents }, ["a", "b"])
    }

    func testDeleteRemappedSameIDRevokesBlockedPredecessorOwner()
        async
    {
        let oldChildEntered = expectation(
            description: "old owner entered before remap delete")
        let oldChildCancelled = expectation(
            description: "old owner cancelled by deleting remapped path")
        let deletionRootEntered = expectation(
            description: "root deletion progressed after old owner cancel")
        let forceRelease = DispatchSemaphore(value: 0)
        let lock = NSLock()
        var rootCalls = 0
        let vm = FileTreeViewModel()
        defer {
            vm.cancelPendingLoads()
            forceRelease.signal()
        }

        vm.bindAsynchronouslyForTesting { parent, _, cancel in
            if parent.isEmpty {
                let call = lock.withLock {
                    rootCalls += 1
                    return rootCalls
                }
                guard call == 1 else {
                    if call == 2 { deletionRootEntered.fulfill() }
                    return self.listing()
                }
                return self.listing(dirs: [self.dir(1, "a")])
            }
            oldChildEntered.fulfill()
            while !cancel.isCancelled() {
                if forceRelease.wait(timeout: .now() + 0.01) == .success {
                    throw CancellationError()
                }
            }
            oldChildCancelled.fulfill()
            throw VaultError.Cancelled
        }
        await assertSettles(vm)
        let a = vm.rootLevel[0]
        vm.expand(a)
        await fulfillment(of: [oldChildEntered], timeout: 5)

        vm.rootLevelInvalidation()
        vm.remapExpansion(fromPrefix: "a", to: "b")
        vm.rootLevelInvalidation()
        vm.removeExpansion(underPrefix: "b")

        await fulfillment(
            of: [oldChildCancelled, deletionRootEntered], timeout: 5)
        await assertSettles(vm)

        XCTAssertTrue(vm.rootLevel.isEmpty)
        XCTAssertTrue(vm.expanded.isEmpty)
        XCTAssertTrue(vm.pendingExpandedPaths.isEmpty)
        XCTAssertTrue(vm.expansionRecency.isEmpty)
        XCTAssertNil(vm.children[.dir(1)])
        XCTAssertNil(vm.fetchState[.dir(1)])
    }

    func testAsyncRootDeleteDoesNotReintroduceExpansionTombstone()
        async throws
    {
        let refreshEntered = expectation(description: "deleted root refresh entered")
        let releaseRefresh = DispatchSemaphore(value: 0)
        let lock = NSLock()
        var rootCalls = 0
        let vm = FileTreeViewModel()
        defer {
            vm.cancelPendingLoads()
            releaseRefresh.signal()
        }

        vm.bindAsynchronouslyForTesting { parent, _, _ in
            if parent == "" {
                lock.lock()
                rootCalls += 1
                let call = rootCalls
                lock.unlock()
                if call == 1 {
                    return self.listing(
                        dirs: [self.dir(1, "a", fileCount: 1)])
                }
                refreshEntered.fulfill()
                try waitForFileTreeGate(releaseRefresh)
                return self.listing()
            }
            return self.listing(files: [self.file("a/old.md")])
        }
        await assertSettles(vm)
        let oldA = try XCTUnwrap(vm.rootLevel.first)
        vm.expand(oldA)
        await assertSettles(vm)

        vm.rootLevelInvalidation()
        vm.removeExpansion(underPrefix: "a")
        await fulfillment(of: [refreshEntered], timeout: 5)
        releaseRefresh.signal()
        await assertSettles(vm)

        XCTAssertTrue(vm.rootLevel.isEmpty)
        XCTAssertTrue(vm.expansionRecency.isEmpty)
        XCTAssertFalse(vm.pendingExpandedPaths.contains("a"))
        XCTAssertFalse(vm.expanded.contains(.dir(1)))
        XCTAssertNil(vm.children[.dir(1)])
        XCTAssertNil(vm.fetchState[.dir(1)])
    }

    func testBlockedDeleteThenSamePathRecreateCannotInheritCachedOwner()
        async throws
    {
        let deleteRefreshEntered = expectation(
            description: "blocked delete refresh entered")
        let releaseDeleteRefresh = DispatchSemaphore(value: 0)
        let lock = NSLock()
        var rootCalls = 0
        var childCalls = 0
        let vm = FileTreeViewModel()
        defer {
            vm.cancelPendingLoads()
            releaseDeleteRefresh.signal()
        }

        vm.bindAsynchronouslyForTesting { parent, _, _ in
            if parent == "" {
                lock.lock()
                rootCalls += 1
                let call = rootCalls
                lock.unlock()
                if call == 2 {
                    deleteRefreshEntered.fulfill()
                    try waitForFileTreeGate(releaseDeleteRefresh)
                    return self.listing()
                }
                return self.listing(
                    dirs: [self.dir(1, "a", fileCount: 1)])
            }
            lock.lock()
            childCalls += 1
            lock.unlock()
            return self.listing(files: [self.file("a/old.md")])
        }
        await assertSettles(vm)
        let original = try XCTUnwrap(vm.rootLevel.first)
        vm.expand(original)
        await assertSettles(vm)
        vm.collapse(original)
        XCTAssertEqual(vm.children[.dir(1)]?.map(\.path), ["a/old.md"])

        vm.rootLevelInvalidation()
        await fulfillment(of: [deleteRefreshEntered], timeout: 5)
        vm.removeExpansion(underPrefix: "a")
        XCTAssertNil(
            vm.children[.dir(1)],
            "authoritative deletion clears even a collapsed cached owner")

        // The recreated folder uses both the predecessor path and recycled
        // SQLite id while the deletion refresh is still blocked. Superseding
        // that refresh must not reconnect either old disclosure or children.
        vm.rootLevelInvalidation()
        releaseDeleteRefresh.signal()
        await assertSettles(vm)

        XCTAssertEqual(vm.rootLevel.map(\.path), ["a"])
        XCTAssertFalse(vm.expanded.contains(.dir(1)))
        XCTAssertNil(vm.children[.dir(1)])
        XCTAssertEqual(vm.visibleRows.map(\.path), ["a"])
        XCTAssertFalse(vm.visibleRows.map(\.path).contains("a/old.md"))
        let (completedRootCalls, completedChildCalls) = lock.withLock {
            (rootCalls, childCalls)
        }
        XCTAssertEqual(completedRootCalls, 3)
        XCTAssertEqual(completedChildCalls, 1)
    }

    func testTargetedRootFailureClearsReusedIDCacheBeforeRetry() async throws {
        let lock = NSLock()
        var rootCalls = 0
        let vm = FileTreeViewModel()
        defer { vm.cancelPendingLoads() }

        vm.bindAsynchronouslyForTesting { parent, _, _ in
            if parent == "" {
                lock.lock()
                rootCalls += 1
                let call = rootCalls
                lock.unlock()
                switch call {
                case 1:
                    return self.listing(
                        dirs: [self.dir(1, "a", fileCount: 1)])
                case 2:
                    throw VaultError.Db(message: "absolute/private/provider")
                default:
                    return self.listing(
                        dirs: [self.dir(1, "b", fileCount: 1)])
                }
            }
            return self.listing(files: [self.file("a/stale.md")])
        }
        await assertSettles(vm)
        let oldA = try XCTUnwrap(vm.rootLevel.first)
        vm.expand(oldA)
        await assertSettles(vm)
        XCTAssertNotNil(vm.children[.dir(1)])

        vm.rootLevelInvalidation()
        vm.removeExpansion(underPrefix: "a")
        await assertSettles(vm)
        XCTAssertTrue(vm.rootLevel.isEmpty)
        XCTAssertNil(vm.children[.dir(1)])
        XCTAssertFalse(vm.expanded.contains(.dir(1)))
        XCTAssertEqual(
            vm.fetchState[FileTreeViewModel.rootFetchKey],
            .failed(message: "Couldn't load this folder."))

        vm.retryRootLoad()
        await assertSettles(vm)
        XCTAssertEqual(vm.rootLevel.map(\.path), ["b"])
        XCTAssertFalse(vm.expanded.contains(.dir(1)))
        XCTAssertNil(vm.children[.dir(1)])
        XCTAssertEqual(vm.visibleRows.map(\.path), ["b"])
    }

    func testSaveDuringVisibleRootRefreshRepreparesSortAndGroup() async {
        let providerEntered = expectation(
            description: "replacement root provider entered")
        let releaseProvider = DispatchSemaphore(value: 0)
        let lock = NSLock()
        var rootCalls = 0
        let now = Date(timeIntervalSince1970: 1_768_953_600)
        let today = Int64(now.timeIntervalSince1970 * 1_000)
        let yesterday = today - 86_400_000
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = TimeZone(secondsFromGMT: 0)!
        var prefs = SidebarOrganizationPrefs()
        prefs.vaultChoice = SidebarOrganizationChoice(
            sort: SidebarSortOption(field: .modified, direction: .desc),
            grouping: .dateBuckets)
        let vm = FileTreeViewModel()
        defer {
            vm.cancelPendingLoads()
            releaseProvider.signal()
        }
        vm.applyOrganization(
            FileTreeViewModel.OrganizationContext(
                prefs: prefs,
                now: now,
                calendar: calendar,
                locale: Locale(identifier: "en_US")))

        vm.bindAsynchronouslyForTesting { _, _, _ in
            lock.lock()
            rootCalls += 1
            let call = rootCalls
            lock.unlock()
            let snapshot = self.listing(files: [
                self.file("a.md", mtimeMs: yesterday - 1),
                self.file("b.md", mtimeMs: yesterday),
            ])
            guard call > 1 else { return snapshot }
            providerEntered.fulfill()
            try waitForFileTreeGate(releaseProvider)
            return snapshot
        }
        await assertSettles(vm)
        XCTAssertEqual(vm.rootLevel.map(\.path), ["b.md", "a.md"])

        vm.rootLevelInvalidation()
        await fulfillment(of: [providerEntered], timeout: 5)
        XCTAssertTrue(
            vm.replaceFileSummary(self.file("a.md", mtimeMs: today)),
            "the visible predecessor updates while its replacement is blocked")
        releaseProvider.signal()
        await assertSettles(vm)

        XCTAssertEqual(vm.rootLevel.map(\.path), ["a.md", "b.md"])
        XCTAssertEqual(vm.fileSummary(forPath: "a.md")?.mtimeMs, today)
        XCTAssertEqual(
            vm.headerRow(before: .file(path: "a.md"))?.label, "Today")
        XCTAssertEqual(
            vm.headerRow(before: .file(path: "b.md"))?.label, "Yesterday")
    }

    func testActiveModifiedSaveReorganizesOffMainAfterTheKeyedRowUpdate()
        async
    {
        var prefs = SidebarOrganizationPrefs()
        prefs.vaultChoice = SidebarOrganizationChoice(
            sort: SidebarSortOption(field: .modified, direction: .desc),
            grouping: .none)
        let vm = FileTreeViewModel()
        defer { vm.cancelPendingLoads() }
        vm.applyOrganization(
            FileTreeViewModel.OrganizationContext(prefs: prefs))
        vm.bindAsynchronouslyForTesting { _, _, _ in
            self.listing(files: [
                self.file("a.md", mtimeMs: 1),
                self.file("b.md", mtimeMs: 2),
            ])
        }
        await assertSettles(vm)
        XCTAssertEqual(vm.rootLevel.map(\.path), ["b.md", "a.md"])

        XCTAssertTrue(
            vm.replaceFileSummary(self.file("a.md", mtimeMs: 3)))
        XCTAssertEqual(
            vm.rootLevel.map(\.path),
            ["b.md", "a.md"],
            "the MainActor call returns before whole-level organization lands")
        XCTAssertEqual(vm.liveReorganizationRowVisitCountForTesting, 0)

        await assertSettles(vm)
        XCTAssertEqual(vm.rootLevel.map(\.path), ["a.md", "b.md"])
        XCTAssertGreaterThanOrEqual(
            vm.liveReorganizationRowVisitCountForTesting, 4)
        XCTAssertEqual(vm.levelReorganizeCountForTesting, 1)
    }

    func testAcceptedNoOpReorganizationEphemeraRetiresOffMain() async {
        let initialRetired = expectation(
            description: "initial prepared ephemera retired")
        let acceptedInputRetired = expectation(
            description: "accepted reorganization input retired")
        let acceptedOutputRetired = expectation(
            description: "accepted no-op output retired")
        let lock = NSLock()
        var retirements: [Bool] = []
        let worker = FileTreeLevelWorker(
            retirementHook: FileTreeRetirementHook { wasMain in
                let count = lock.withLock {
                    retirements.append(wasMain)
                    return retirements.count
                }
                switch count {
                case 1: initialRetired.fulfill()
                case 2: acceptedInputRetired.fulfill()
                case 3: acceptedOutputRetired.fulfill()
                default: break
                }
            })
        var prefs = SidebarOrganizationPrefs()
        prefs.vaultChoice = SidebarOrganizationChoice(
            sort: SidebarSortOption(field: .modified, direction: .desc),
            grouping: .dateBuckets)
        let vm = FileTreeViewModel(levelWorker: worker)
        defer { vm.cancelPendingLoads() }
        vm.applyOrganization(
            FileTreeViewModel.OrganizationContext(prefs: prefs))
        vm.bindAsynchronouslyForTesting { _, _, _ in
            self.listing(
                dirs: [self.dir(1, "folder")],
                files: [self.file("only.md", mtimeMs: 1)])
        }
        await assertSettles(vm)
        await fulfillment(of: [initialRetired], timeout: 5)

        XCTAssertTrue(
            vm.replaceFileSummary(self.file("only.md", mtimeMs: 2)))
        await assertSettles(vm)
        await fulfillment(
            of: [acceptedInputRetired, acceptedOutputRetired], timeout: 5)
        await assertRetirementsSettle(worker)

        XCTAssertEqual(vm.rootLevel.map(\.path), ["folder", "only.md"])
        XCTAssertEqual(vm.levelReorganizeCountForTesting, 0)
        XCTAssertEqual(lock.withLock { retirements }, [false, false, false])
    }

    func testObsoleteLevelStorageRetiresOffMain() async {
        let initialEphemeraRetired = expectation(
            description: "initial prepared ephemera retired")
        let obsoleteLevelRetired = expectation(
            description: "obsolete published level retired")
        let lock = NSLock()
        var retirementWasOnMain: Bool?
        var retirementCount = 0
        let worker = FileTreeLevelWorker(
            retirementHook: FileTreeRetirementHook { wasMain in
                let count = lock.withLock {
                    retirementCount += 1
                    if retirementCount == 2 {
                        retirementWasOnMain = wasMain
                    }
                    return retirementCount
                }
                if count == 1 {
                    initialEphemeraRetired.fulfill()
                } else if count == 2 {
                    obsoleteLevelRetired.fulfill()
                }
            })
        let vm = FileTreeViewModel(levelWorker: worker)
        defer { vm.cancelPendingLoads() }

        vm.bindAsynchronouslyForTesting { _, _, _ in
            self.listing(
                dirs: [self.dir(1, "folder")],
                files: [self.file("a.md")])
        }
        await assertSettles(vm)
        await fulfillment(of: [initialEphemeraRetired], timeout: 5)
        vm.rootLevelInvalidation()
        await assertSettles(vm)
        await fulfillment(of: [obsoleteLevelRetired], timeout: 5)

        let completedOnMain = lock.withLock { retirementWasOnMain }
        XCTAssertEqual(completedOnMain, false)
    }

    func testLargeTargetedRootSnapshotsRetireOffMainAfterPartialAndComplete()
        async
    {
        final class RootPhaseBox: @unchecked Sendable {
            private let lock = NSLock()
            private var phase = 0

            func beginPartial() { lock.withLock { phase = 1 } }
            func heal() { lock.withLock { phase = 2 } }
            var snapshot: Int { lock.withLock { phase } }
        }

        let roots = (0..<256).map {
            dir(Int64($0 + 1), "folder-\($0)")
        }
        let box = RootPhaseBox()
        let lock = NSLock()
        var retirements: [Bool] = []
        let worker = FileTreeLevelWorker(
            retirementHook: FileTreeRetirementHook { wasMain in
                lock.withLock { retirements.append(wasMain) }
            })
        let vm = FileTreeViewModel(levelWorker: worker)
        defer { vm.cancelPendingLoads() }
        vm.bindAsynchronouslyForTesting { parent, cursor, _ in
            guard parent.isEmpty else { return self.listing() }
            if box.snapshot == 1 {
                if cursor == nil {
                    return self.listing(
                        dirs: [roots[0]], nextCursor: "page-2")
                }
                throw VaultError.Db(message: "late roots unavailable")
            }
            return self.listing(dirs: roots)
        }
        await assertSettles(vm)
        for root in vm.rootLevel { vm.expand(root) }
        await assertSettles(vm, timeoutNanoseconds: 20_000_000_000)
        await assertRetirementsSettle(worker)
        lock.withLock { retirements = [] }

        box.beginPartial()
        vm.rootLevelInvalidation()
        await assertSettles(vm)
        await assertRetirementsSettle(worker)
        XCTAssertEqual(lock.withLock { retirements }, [false, false])
        XCTAssertEqual(vm.rootLevel.count, 1)
        guard case .failed = vm.fetchState[FileTreeViewModel.rootFetchKey]
        else { return XCTFail("partial targeted root must expose Retry") }

        lock.withLock { retirements = [] }
        box.heal()
        vm.retryRootLoad()
        await assertSettles(vm)
        await assertRetirementsSettle(worker)
        XCTAssertEqual(lock.withLock { retirements }, [false, false, false])
        XCTAssertEqual(vm.rootLevel.count, roots.count)
        XCTAssertNil(vm.fetchState[FileTreeViewModel.rootFetchKey])
    }

    func testPreparedSuccessRevokedBeforePublicationRetiresOffMain()
        async throws
    {
        let prepared = expectation(description: "stale result prepared")
        let releasePrepared = DispatchSemaphore(value: 0)
        let retired = expectation(description: "stale result retired")
        let lock = NSLock()
        var retirementWasOnMain: Bool?
        var completedHookCount = 0
        let worker = FileTreeLevelWorker(
            preparationHook: FileTreePreparationHook { event, _ in
                guard case let .completed(parentPath) = event,
                    parentPath.isEmpty
                else { return }
                let isFirst = lock.withLock {
                    completedHookCount += 1
                    return completedHookCount == 1
                }
                guard isFirst else { return }
                prepared.fulfill()
                _ = releasePrepared.wait(timeout: .now() + 20)
            },
            retirementHook: FileTreeRetirementHook { wasMain in
                lock.withLock { retirementWasOnMain = wasMain }
                retired.fulfill()
            })
        let vm = FileTreeViewModel(levelWorker: worker)
        defer {
            vm.cancelPendingLoads()
            releasePrepared.signal()
        }

        vm.bindAsynchronouslyForTesting { _, _, _ in
            self.listing(files: [self.file("stale.md")])
        }
        let staleTask = try XCTUnwrap(
            vm.levelDrainTasksForTesting[FileTreeViewModel.rootFetchKey])
        await fulfillment(of: [prepared], timeout: 5)
        vm.bindAsynchronouslyForTesting { _, _, _ in
            self.listing(files: [self.file("newest.md")])
        }
        releasePrepared.signal()
        await waitForCompletion(
            of: staleTask, description: "revoked prepared success")
        await assertSettles(vm)
        await fulfillment(of: [retired], timeout: 5)

        XCTAssertEqual(vm.rootLevel.map(\.path), ["newest.md"])
        XCTAssertEqual(lock.withLock { retirementWasOnMain }, false)
    }

    func testCancellationDuringPreparationSuppressesPublication() async throws {
        let preparationEntered = expectation(description: "preparation entered")
        let releasePreparation = DispatchSemaphore(value: 0)
        let worker = FileTreeLevelWorker(
            preparationHook: FileTreePreparationHook { _, _ in
                preparationEntered.fulfill()
                _ = releasePreparation.wait(timeout: .now() + 20)
            })
        let vm = FileTreeViewModel(levelWorker: worker)
        defer {
            vm.cancelPendingLoads()
            releasePreparation.signal()
        }

        vm.bindAsynchronouslyForTesting { _, _, _ in
            self.listing(files: [self.file("never-published.md")])
        }
        let task = try XCTUnwrap(
            vm.levelDrainTasksForTesting[FileTreeViewModel.rootFetchKey])
        await fulfillment(of: [preparationEntered], timeout: 5)
        vm.cancelPendingLoads()
        releasePreparation.signal()
        await waitForCompletion(
            of: task, description: "cancelled preparation finished")

        XCTAssertTrue(vm.rootLevel.isEmpty)
        XCTAssertNil(vm.fetchState[FileTreeViewModel.rootFetchKey])
        XCTAssertNil(vm.fileSummary(forPath: "never-published.md"))
    }

    func testSummaryOverlayRepreparesOnlyItsOwningQueuedLevel() async throws {
        let aEntered = expectation(description: "a provider entered")
        let releaseA = DispatchSemaphore(value: 0)
        let preparation = PreparationProbe()
        let worker = FileTreeLevelWorker(
            preparationHook: FileTreePreparationHook { event, _ in
                preparation.record(event)
            })
        let vm = FileTreeViewModel(levelWorker: worker)
        defer {
            vm.cancelPendingLoads()
            releaseA.signal()
        }

        vm.bindAsynchronouslyForTesting { parent, _, _ in
            switch parent {
            case "":
                return self.listing(dirs: [
                    self.dir(1, "a", fileCount: 1),
                    self.dir(2, "b", fileCount: 1),
                ])
            case "a":
                aEntered.fulfill()
                try waitForFileTreeGate(releaseA)
                return self.listing(files: [
                    self.file("a/new.md", mtimeMs: 1)
                ])
            case "b":
                return self.listing(files: [
                    self.file("b/other.md", mtimeMs: 1)
                ])
            default:
                return self.listing()
            }
        }
        await assertSettles(vm)
        preparation.reset()
        let a = try XCTUnwrap(vm.rootLevel.first { $0.path == "a" })
        let b = try XCTUnwrap(vm.rootLevel.first { $0.path == "b" })

        vm.expand(a)
        await fulfillment(of: [aEntered], timeout: 5)
        vm.expand(b)
        XCTAssertFalse(
            vm.replaceFileSummary(self.file("a/new.md", mtimeMs: 2)))
        releaseA.signal()
        await assertSettles(vm)

        let overlayParents = preparation.snapshot.compactMap { event -> String? in
            guard case let .overlay(parentPath) = event else { return nil }
            return parentPath
        }
        XCTAssertEqual(overlayParents, ["a"])
        XCTAssertEqual(vm.fileSummary(forPath: "a/new.md")?.mtimeMs, 2)
        XCTAssertEqual(vm.children[b.nodeID]?.map(\.path), ["b/other.md"])
    }

    func testOverlayOverflowRefetchesInsteadOfPublishingPartialTruth() async {
        let replacementEntered = expectation(
            description: "overflow replacement entered")
        let releaseReplacement = DispatchSemaphore(value: 0)
        let lock = NSLock()
        var rootCalls = 0
        let old = (0...FileTreeViewModel.pendingSummaryOverlayCap).map {
            self.file(String(format: "f%04d.md", $0), mtimeMs: 1)
        }
        let newest = old.map { self.file($0.path, mtimeMs: 2) }
        let vm = FileTreeViewModel()
        defer {
            vm.cancelPendingLoads()
            releaseReplacement.signal()
        }

        vm.bindAsynchronouslyForTesting { _, _, _ in
            lock.lock()
            rootCalls += 1
            let call = rootCalls
            lock.unlock()
            if call == 2 {
                replacementEntered.fulfill()
                try waitForFileTreeGate(releaseReplacement)
            }
            return self.listing(files: call >= 3 ? newest : old)
        }
        await assertSettles(vm)

        vm.rootLevelInvalidation()
        await fulfillment(of: [replacementEntered], timeout: 5)
        XCTAssertEqual(vm.replaceFileSummaries(newest), newest.count)
        releaseReplacement.signal()
        await assertSettles(vm)

        let completedRootCalls = lock.withLock { rootCalls }
        XCTAssertEqual(completedRootCalls, 3)
        XCTAssertEqual(vm.rootLevel.count, newest.count)
        XCTAssertTrue(
            newest.allSatisfy {
                vm.fileSummary(forPath: $0.path)?.mtimeMs == 2
            },
            "no row beyond the overlay cap may regress to the stale snapshot")
    }

    func testTargetedRootReconciliationVisitsOwnedDirsNotFiftyThousandRows()
        async throws
    {
        let files = (0..<49_999).map {
            self.file(String(format: "note-%05d.md", $0))
        }
        let root = listing(
            dirs: [dir(1, "owned")],
            files: files)
        let vm = FileTreeViewModel()
        defer { vm.cancelPendingLoads() }

        vm.bindAsynchronouslyForTesting { parent, _, _ in
            parent.isEmpty ? root : self.listing()
        }
        await assertSettles(vm, timeoutNanoseconds: 20_000_000_000)
        let owned = try XCTUnwrap(vm.rootLevel.first)
        vm.expand(owned)
        await assertSettles(vm)
        vm.collapse(owned)

        vm.rootLevelInvalidation()
        await assertSettles(vm, timeoutNanoseconds: 20_000_000_000)

        XCTAssertEqual(vm.rootLevel.count, 50_000)
        XCTAssertEqual(
            vm.targetedRootReconcileVisitCountForTesting,
            1,
            "file-heavy roots reconcile only stateful predecessor directories")
    }

    func testExpandLoadedLevelsAdmitsOnlyOneChildWorkerAtATime()
        async throws
    {
        let firstChildEntered = expectation(description: "first child entered")
        let secondChildEntered = expectation(description: "second child entered")
        let releaseFirstChild = DispatchSemaphore(value: 0)
        let releaseSecondChild = DispatchSemaphore(value: 0)
        let lock = NSLock()
        var childParents: [String] = []
        var didComplete = false
        let root = listing(
            dirs: (0..<1_000).map {
                self.dir(Int64($0 + 1), String(format: "d%04d", $0))
            })
        let vm = FileTreeViewModel()
        defer {
            vm.cancelPendingLoads()
            releaseFirstChild.signal()
            releaseSecondChild.signal()
        }

        vm.bindAsynchronouslyForTesting { parent, _, _ in
            guard !parent.isEmpty else { return root }
            lock.lock()
            childParents.append(parent)
            let first = childParents.count == 1
            let second = childParents.count == 2
            lock.unlock()
            if first {
                firstChildEntered.fulfill()
                try waitForFileTreeGate(releaseFirstChild)
            } else if second {
                secondChildEntered.fulfill()
                try waitForFileTreeGate(releaseSecondChild)
            }
            return self.listing()
        }
        await assertSettles(vm)
        vm.expandLoadedLevels { didComplete = true }
        await fulfillment(of: [firstChildEntered], timeout: 5)

        XCTAssertEqual(
            vm.expanded.count,
            1,
            "unadmitted folders remain truthfully collapsed without a fake "
                + "expanded accessibility state or missing loading row")
        XCTAssertEqual(vm.levelDrainTasksForTesting.count, 1)
        XCTAssertEqual(vm.maximumLevelDrainTaskCountForTesting, 1)
        XCTAssertFalse(didComplete)
        releaseFirstChild.signal()
        await fulfillment(of: [secondChildEntered], timeout: 5)
        XCTAssertEqual(
            vm.expanded.count,
            2,
            "the next folder discloses only when its load is admitted")
        XCTAssertEqual(vm.levelDrainTasksForTesting.count, 1)
        XCTAssertEqual(vm.maximumLevelDrainTaskCountForTesting, 1)
        XCTAssertFalse(didComplete)
        let admitted = try XCTUnwrap(vm.levelDrainTasksForTesting.values.first)
        vm.cancelPendingLoads()
        releaseSecondChild.signal()
        await waitForCompletion(
            of: admitted, description: "cancelled expand-loaded child")
        await Task.yield()

        let completedParents = lock.withLock { childParents }
        XCTAssertEqual(completedParents, ["d0000", "d0001"])
        XCTAssertFalse(
            didComplete,
            "a cancelled prefix must never announce whole-command completion")
        XCTAssertNil(vm.expandLoadedTaskForTesting)
        XCTAssertTrue(vm.levelDrainTasksForTesting.isEmpty)
    }

    func testExpandLoadedEmptyLevelsDoNotRescanGlobalExpandedSet() async {
        let completed = expectation(description: "all empty levels expanded")
        let lock = NSLock()
        var childCalls = 0
        let vm = FileTreeViewModel()
        defer { vm.cancelPendingLoads() }

        vm.bindAsynchronouslyForTesting { parent, _, _ in
            guard !parent.isEmpty else {
                return self.listing(
                    dirs: (0..<1_000).map {
                        self.dir(
                            Int64($0 + 1), String(format: "d%04d", $0))
                    })
            }
            lock.withLock { childCalls += 1 }
            return self.listing()
        }
        await assertSettles(vm)

        vm.expandLoadedLevels { completed.fulfill() }
        await fulfillment(of: [completed], timeout: 20)
        await assertSettles(vm, timeoutNanoseconds: 20_000_000_000)

        XCTAssertEqual(lock.withLock { childCalls }, 1_000)
        XCTAssertEqual(vm.expanded.count, 1_000)
        XCTAssertEqual(
            vm.materializationCandidateVisitCountForTesting, 0,
            "empty child publications must not scan the global expanded set")
    }

    func testManualCollapseRevokesBlockedExpandLoadedSnapshot() async throws {
        let firstEntered = expectation(description: "first bulk child entered")
        let releaseFirst = DispatchSemaphore(value: 0)
        let lock = NSLock()
        var childParents: [String] = []
        var didComplete = false
        let vm = FileTreeViewModel()
        defer {
            vm.cancelPendingLoads()
            releaseFirst.signal()
        }

        vm.bindAsynchronouslyForTesting { parent, _, _ in
            guard !parent.isEmpty else {
                return self.listing(dirs: [
                    self.dir(1, "a"), self.dir(2, "b"),
                ])
            }
            let isFirst = lock.withLock {
                childParents.append(parent)
                return childParents.count == 1
            }
            if isFirst {
                firstEntered.fulfill()
                try waitForFileTreeGate(releaseFirst)
            }
            return self.listing()
        }
        await assertSettles(vm)
        let b = try XCTUnwrap(vm.rootLevel.first { $0.path == "b" })

        vm.expandLoadedLevels { didComplete = true }
        let pump = try XCTUnwrap(vm.expandLoadedTaskForTesting)
        await fulfillment(of: [firstEntered], timeout: 5)
        let firstLoad = try XCTUnwrap(vm.levelDrainTasksForTesting[.dir(1)])
        vm.collapse(b)
        releaseFirst.signal()
        await waitForCompletion(of: firstLoad, description: "first bulk load")
        await waitForCompletion(of: pump, description: "revoked bulk pump")

        XCTAssertEqual(lock.withLock { childParents }, ["a"])
        XCTAssertFalse(vm.expanded.contains(b.nodeID))
        XCTAssertFalse(didComplete)
    }

    func testRestoredThousandSiblingLevelsMaterializeOneAtATime()
        async throws
    {
        let firstEntered = expectation(description: "first restored child entered")
        let unrelatedEntered = expectation(
            description: "unrelated restored child survived invalidation")
        let followingEntered = expectation(
            description: "provider-ordered restored sibling followed")
        let finalEntered = expectation(
            description: "final restored sibling survived invalidation")
        let releaseFirst = DispatchSemaphore(value: 0)
        let lock = NSLock()
        var childParents: [String] = []
        let paths = (0..<1_000).map { String(format: "d%04d", $0) }
        let vm = FileTreeViewModel()
        defer {
            vm.cancelPendingLoads()
            releaseFirst.signal()
        }

        vm.bindAsynchronouslyForTesting(
            cancellablePagedFetcher: { parent, _, _ in
                guard !parent.isEmpty else {
                    return self.listing(
                        dirs: paths.enumerated().map {
                            self.dir(Int64($0.offset + 1), $0.element)
                        })
                }
                let isFirst = lock.withLock {
                    childParents.append(parent)
                    return childParents.count == 1
                }
                if isFirst {
                    firstEntered.fulfill()
                    try waitForFileTreeGate(releaseFirst)
                } else if parent == "d0001" {
                    unrelatedEntered.fulfill()
                } else if parent == "d0002" {
                    followingEntered.fulfill()
                } else if parent == "d0999" {
                    finalEntered.fulfill()
                }
                return self.listing()
            },
            restoringExpandedDirPaths: paths)
        await fulfillment(of: [firstEntered], timeout: 5)

        XCTAssertEqual(lock.withLock { childParents }, ["d0000"])
        XCTAssertEqual(
            vm.expanded.count, 1,
            "only the admitted restored row may expose expanded disclosure")
        XCTAssertEqual(vm.pendingExpandedPaths.count, 999)
        XCTAssertTrue(vm.expanded.contains(.dir(1)))
        XCTAssertFalse(vm.expanded.contains(.dir(2)))
        XCTAssertEqual(vm.fetchState[.dir(1)], .loading)
        XCTAssertNil(vm.fetchState[.dir(2)])
        XCTAssertEqual(vm.levelDrainTasksForTesting.count, 1)
        XCTAssertEqual(vm.maximumLevelDrainTaskCountForTesting, 1)
        let firstLoad = try XCTUnwrap(vm.levelDrainTasksForTesting[.dir(1)])
        // A targeted refresh may add its own explicit request, but it must not
        // discard the pump's 998 unrelated pending siblings.
        vm.treeInvalidation(parent: .dir(1_000))
        releaseFirst.signal()
        await waitForCompletion(of: firstLoad, description: "restored first load")
        await fulfillment(
            of: [unrelatedEntered, followingEntered, finalEntered], timeout: 20)
        vm.cancelPendingLoads()

        let admittedParents = lock.withLock { childParents }
        XCTAssertEqual(admittedParents.first, "d0000")
        XCTAssertTrue(admittedParents.contains("d0999"))
        XCTAssertLessThan(
            try XCTUnwrap(admittedParents.firstIndex(of: "d0001")),
            try XCTUnwrap(admittedParents.firstIndex(of: "d0002")))
    }

    func testCollapsedRestoredAncestorReenqueuesDormantCachedDescendants()
        async throws
    {
        let firstXEntered = expectation(description: "first x child entered")
        let bEntered = expectation(description: "dormant b child reentered")
        let releaseFirstX = DispatchSemaphore(value: 0)
        let lock = NSLock()
        var xCalls = 0
        let vm = FileTreeViewModel()
        defer {
            vm.cancelPendingLoads()
            releaseFirstX.signal()
        }

        vm.bindAsynchronouslyForTesting(
            cancellablePagedFetcher: { parent, _, _ in
                switch parent {
                case "":
                    return self.listing(
                        dirs: [self.dir(1, "a", dirCount: 2)])
                case "a":
                    return self.listing(dirs: [
                        self.dir(2, "a/x"), self.dir(3, "a/b"),
                    ])
                case "a/x":
                    let call = lock.withLock {
                        xCalls += 1
                        return xCalls
                    }
                    if call == 1 {
                        firstXEntered.fulfill()
                        try waitForFileTreeGate(releaseFirstX)
                    }
                    return self.listing()
                case "a/b":
                    bEntered.fulfill()
                    return self.listing()
                default:
                    return self.listing()
                }
            },
            restoringExpandedDirPaths: ["a", "a/x", "a/b"])
        await fulfillment(of: [firstXEntered], timeout: 5)

        let a = try XCTUnwrap(vm.rootLevel.first)
        XCTAssertTrue(vm.expanded.contains(.dir(1)))
        XCTAssertTrue(vm.expanded.contains(.dir(2)))
        XCTAssertFalse(vm.expanded.contains(.dir(3)))
        XCTAssertTrue(vm.pendingExpandedPaths.contains("a/b"))
        let firstXLoad = try XCTUnwrap(
            vm.levelDrainTasksForTesting[.dir(2)])

        vm.collapse(a)
        releaseFirstX.signal()
        await waitForCompletion(
            of: firstXLoad, description: "cancelled first x child")
        await assertSettles(vm)

        XCTAssertFalse(vm.expanded.contains(.dir(1)))
        XCTAssertFalse(vm.expanded.contains(.dir(3)))
        XCTAssertTrue(vm.pendingExpandedPaths.contains("a/b"))
        XCTAssertNil(vm.children[.dir(3)])
        XCTAssertTrue(vm.pendingExpandedPaths.contains("a/x"))
        XCTAssertFalse(vm.expanded.contains(.dir(2)))

        vm.expand(a)
        XCTAssertFalse(
            vm.expanded.contains(.dir(2)),
            "a canceled descendant remains collapsed until readmission")
        XCTAssertNil(vm.fetchState[.dir(2)])
        await fulfillment(of: [bEntered], timeout: 5)
        await assertSettles(vm)

        XCTAssertTrue(vm.expanded.contains(.dir(3)))
        XCTAssertFalse(vm.pendingExpandedPaths.contains("a/b"))
        XCTAssertNotNil(vm.children[.dir(3)])
        XCTAssertEqual(lock.withLock { xCalls }, 2)
    }

    func testCollapseOfQueuedRestoredRowSurvivesOwningLevelReplacement()
        async throws
    {
        let firstEntered = expectation(description: "first restored row entered")
        let releaseFirst = DispatchSemaphore(value: 0)
        let lock = NSLock()
        var childParents: [String] = []
        let vm = FileTreeViewModel()
        defer {
            vm.cancelPendingLoads()
            releaseFirst.signal()
        }

        vm.bindAsynchronouslyForTesting(
            cancellablePagedFetcher: { parent, _, _ in
                guard !parent.isEmpty else {
                    return self.listing(dirs: [
                        self.dir(1, "d0000"), self.dir(2, "d0001"),
                    ])
                }
                let childCall = lock.withLock {
                    childParents.append(parent)
                    return childParents.count
                }
                if parent == "d0000", childCall == 1 {
                    firstEntered.fulfill()
                    try waitForFileTreeGate(releaseFirst)
                }
                return self.listing()
            },
            restoringExpandedDirPaths: ["d0000", "d0001"])
        await fulfillment(of: [firstEntered], timeout: 5)

        let queued = try XCTUnwrap(
            vm.rootLevel.first { $0.path == "d0001" })
        XCTAssertFalse(vm.expanded.contains(queued.nodeID))
        XCTAssertTrue(vm.pendingExpandedPaths.contains(queued.path))
        vm.collapse(queued)
        vm.rootLevelInvalidation()
        releaseFirst.signal()
        await assertSettles(vm)

        XCTAssertFalse(vm.pendingExpandedPaths.contains(queued.path))
        XCTAssertFalse(vm.expansionRecency.contains(queued.path))
        XCTAssertFalse(vm.expanded.contains(queued.nodeID))
        XCTAssertFalse(lock.withLock { childParents }.contains(queued.path))
    }

    func testCollapseAllClearsDormantRestorationAcrossRootReplacement()
        async throws
    {
        let aEntered = expectation(description: "restored a child entered")
        let releaseA = DispatchSemaphore(value: 0)
        let lock = NSLock()
        var aCalls = 0
        var childParents: [String] = []
        let vm = FileTreeViewModel()
        defer {
            vm.cancelPendingLoads()
            releaseA.signal()
        }

        vm.bindAsynchronouslyForTesting(
            cancellablePagedFetcher: { parent, _, _ in
                if parent.isEmpty {
                    return self.listing(
                        dirs: [self.dir(1, "a", dirCount: 1)])
                }
                let call = lock.withLock {
                    childParents.append(parent)
                    if parent == "a" { aCalls += 1 }
                    return aCalls
                }
                if parent == "a", call == 1 {
                    aEntered.fulfill()
                    try waitForFileTreeGate(releaseA)
                }
                if parent == "a" {
                    return self.listing(dirs: [self.dir(2, "a/b")])
                }
                return self.listing()
            },
            restoringExpandedDirPaths: ["a", "a/b"])
        await fulfillment(of: [aEntered], timeout: 5)

        vm.collapseAllPreservingAncestors(ofPath: nil)
        vm.rootLevelInvalidation()
        releaseA.signal()
        await assertSettles(vm)

        XCTAssertTrue(vm.pendingExpandedPaths.isEmpty)
        XCTAssertTrue(vm.expansionRecency.isEmpty)
        let a = try XCTUnwrap(vm.rootLevel.first)
        vm.expand(a)
        await assertSettles(vm)

        let b = try XCTUnwrap(vm.children[a.nodeID]?.first)
        XCTAssertFalse(vm.expanded.contains(b.nodeID))
        XCTAssertFalse(vm.pendingExpandedPaths.contains(b.path))
        XCTAssertFalse(lock.withLock { childParents }.contains("a/b"))
    }

    func testOwningLevelReplacementCannotLoseReenqueuedSameIDMaterialization()
        async throws
    {
        let oldEntered = expectation(description: "old owner child entered")
        let replacementEntered = expectation(
            description: "replacement same-id child entered")
        let releaseOld = DispatchSemaphore(value: 0)
        let lock = NSLock()
        var rootCalls = 0
        var childParents: [String] = []
        let vm = FileTreeViewModel()
        defer {
            vm.cancelPendingLoads()
            releaseOld.signal()
        }

        vm.bindAsynchronouslyForTesting(
            cancellablePagedFetcher: { parent, _, _ in
                if parent.isEmpty {
                    let call = lock.withLock {
                        rootCalls += 1
                        return rootCalls
                    }
                    return self.listing(dirs: call == 1 ? [
                        self.dir(1, "a"), self.dir(2, "b"),
                    ] : [
                        self.dir(1, "a"), self.dir(2, "c"),
                    ])
                }
                let childCall = lock.withLock {
                    childParents.append(parent)
                    return childParents.count
                }
                if parent == "a", childCall == 1 {
                    oldEntered.fulfill()
                    try waitForFileTreeGate(releaseOld)
                } else if parent == "c" {
                    replacementEntered.fulfill()
                }
                return self.listing()
            },
            restoringExpandedDirPaths: ["a", "b"])
        await fulfillment(of: [oldEntered], timeout: 5)

        vm.remapExpansion(fromPrefix: "b", to: "c")
        vm.rootLevelInvalidation()
        await Task.yield()
        releaseOld.signal()
        await fulfillment(of: [replacementEntered], timeout: 5)
        await assertSettles(vm)
        vm.cancelPendingLoads()

        XCTAssertEqual(vm.rootLevel.map(\.path), ["a", "c"])
        XCTAssertTrue(vm.expanded.contains(.dir(2)))
        XCTAssertTrue(vm.children[.dir(2)]?.isEmpty == true)
        XCTAssertFalse(lock.withLock { childParents }.contains("b"))
    }

    func testExpandLoadedCompletionFiresAfterDeterministicFullDrain() async {
        let completed = expectation(description: "bulk expansion completed")
        let aEntered = expectation(description: "last child worker entered")
        let releaseA = DispatchSemaphore(value: 0)
        let lock = NSLock()
        var childParents: [String] = []
        let vm = FileTreeViewModel()
        let tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("expand-mirror-\(UUID().uuidString)")
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents.json")),
            externalOpener: { _ in true })
        defer {
            vm.cancelPendingLoads()
            releaseA.signal()
            try? FileManager.default.removeItem(at: tempDir)
        }

        vm.bindAsynchronouslyForTesting { parent, _, _ in
            guard !parent.isEmpty else {
                return self.listing(dirs: [
                    self.dir(1, "a"), self.dir(2, "b"),
                ])
            }
            lock.withLock { childParents.append(parent) }
            if parent == "a" {
                aEntered.fulfill()
                try waitForFileTreeGate(releaseA)
            }
            return self.listing()
        }
        await assertSettles(vm)
        let b = vm.rootLevel[1]
        vm.expand(b)
        await assertSettles(vm)
        FileTreeSidebar.mirrorExpandedPaths(tree: vm, appState: state)
        XCTAssertEqual(state.treeExpandedDirPaths, ["b"])
        lock.withLock { childParents = [] }
        XCTAssertTrue(
            FileTreeSidebar.expandLoadedAndMirror(
                tree: vm, appState: state
            ) {
                completed.fulfill()
            })
        await fulfillment(of: [aEntered], timeout: 5)
        FileTreeSidebar.mirrorExpandedPaths(tree: vm, appState: state)
        XCTAssertEqual(
            state.treeExpandedDirPaths, ["b", "a"],
            "the normal expanded-id observer can publish the interim order")
        releaseA.signal()
        await fulfillment(of: [completed], timeout: 5)
        await assertSettles(vm)

        XCTAssertEqual(lock.withLock { childParents }, ["a"])
        XCTAssertEqual(vm.expanded.count, 2)
        XCTAssertEqual(vm.expansionRecency, ["a", "b"])
        XCTAssertEqual(
            state.treeExpandedDirPaths, ["a", "b"],
            "command completion must mirror the final deterministic recency")
        XCTAssertEqual(vm.maximumLevelDrainTaskCountForTesting, 1)
    }

    func testAsyncPathParityForPaginationPinsAndFolderNotes() async throws {
        var pins = SidebarPins()
        pins.pin("b.md", inFolder: "")
        let vm = FileTreeViewModel()
        defer { vm.cancelPendingLoads() }
        vm.applyOrganization(
            FileTreeViewModel.OrganizationContext(pins: pins))

        vm.bindAsynchronouslyForTesting { parent, cursor, _ in
            switch (parent, cursor) {
            case ("", nil):
                return self.listing(
                    dirs: [self.dir(1, "folder", fileCount: 2)],
                    files: [self.file("a.md")],
                    nextCursor: "next")
            case ("", "next"):
                return self.listing(files: [self.file("b.md")])
            case ("folder", nil):
                return self.listing(files: [
                    self.file("folder/folder.md"),
                    self.file("folder/visible.md"),
                ])
            default:
                return self.listing()
            }
        }
        await assertSettles(vm)
        XCTAssertEqual(vm.rootLevel.map(\.path), ["folder", "b.md", "a.md"])
        XCTAssertTrue(vm.isPinnedRow(.file(path: "b.md")))
        XCTAssertEqual(
            vm.headerRow(before: .file(path: "b.md"))?.kind, .pinned)
        XCTAssertNil(vm.incompleteLevelMessage(for: FileTreeViewModel.rootFetchKey))

        let folder = try XCTUnwrap(vm.rootLevel.first)
        vm.expand(folder)
        await assertSettles(vm)
        XCTAssertEqual(
            vm.children[folder.nodeID]?.map(\.path),
            ["folder/visible.md"],
            "a represented folder note must not duplicate the directory row")
    }
}
