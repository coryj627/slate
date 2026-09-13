// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest
@testable import SlateMac

@MainActor
final class MoveToLoadingCancellationTests: XCTestCase {
    func testDismissalRejectsFoldersReturnedByAnAlreadyRunningLoader() async {
        var suspended: CheckedContinuation<[String], Never>?
        let task = Task {
            await MoveToFolderSheet.loadFoldersForPresentation {
                await withCheckedContinuation { suspended = $0 }
            }
        }
        while suspended == nil { await Task.yield() }
        task.cancel()
        suspended?.resume(returning: ["late destination"])
        let result = await task.value
        XCTAssertNil(result)
    }

    func testCurrentPresentationRetainsTheCompleteFolderResult() async {
        let result = await MoveToFolderSheet.loadFoldersForPresentation {
            ["first", "second/nested"]
        }
        XCTAssertEqual(result, ["first", "second/nested"])
    }
}
