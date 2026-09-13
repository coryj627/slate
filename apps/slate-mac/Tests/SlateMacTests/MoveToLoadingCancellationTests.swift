// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest
@testable import SlateMac

@MainActor
final class MoveToLoadingCancellationTests: XCTestCase {
    func testDismissalRejectsFoldersReturnedByAnAlreadyRunningLoader() async {
        let ready = XCTestExpectation(description: "destination loader suspended")
        let finished = XCTestExpectation(description: "cancelled presentation loader finished")
        var suspended: CheckedContinuation<[String], Never>?
        var result: [String]?
        let task = Task { @MainActor in
            result = await MoveToFolderSheet.loadFoldersForPresentation {
                await withCheckedContinuation { continuation in
                    // If readiness timed out before this task ran, cleanup has
                    // already cancelled it. Never park a late continuation.
                    guard !Task.isCancelled else {
                        continuation.resume(returning: ["late destination"])
                        return
                    }
                    suspended = continuation
                    ready.fulfill()
                }
            }
            finished.fulfill()
        }
        func releaseLoader() {
            let continuation = suspended
            suspended = nil
            continuation?.resume(returning: ["late destination"])
        }
        defer {
            task.cancel()
            releaseLoader()
        }
        await fulfillment(of: [ready], timeout: 5)
        task.cancel()
        releaseLoader()
        await fulfillment(of: [finished], timeout: 5)
        XCTAssertNil(result)
    }

    func testCurrentPresentationRetainsTheCompleteFolderResult() async {
        let result = await MoveToFolderSheet.loadFoldersForPresentation {
            ["first", "second/nested"]
        }
        XCTAssertEqual(result, ["first", "second/nested"])
    }
}
