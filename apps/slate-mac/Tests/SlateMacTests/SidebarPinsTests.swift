// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation
import XCTest

@testable import SlateMac

/// FL3-2 (#659): the pure pinned-notes model — authored order, mutation-driven
/// integrity, and once-per-session prune bookkeeping. Persistence goes through
/// `SidebarOrganizationSchema` (covered in SidebarOrganizationTests); these
/// tests own the lifecycle rules.
final class SidebarPinsTests: XCTestCase {

  // MARK: - Authored order

  func testPinAppendsAndUnpinRemovesPreservingAuthoredOrder() {
    var pins = SidebarPins()
    pins.pin("Projects/b.md", inFolder: "Projects")
    pins.pin("Projects/a.md", inFolder: "Projects")
    pins.pin("Projects/c.md", inFolder: "Projects")
    XCTAssertEqual(
      pins.paths(forFolder: "Projects"),
      ["Projects/b.md", "Projects/a.md", "Projects/c.md"])

    pins.unpin("Projects/a.md", inFolder: "Projects")
    XCTAssertEqual(
      pins.paths(forFolder: "Projects"), ["Projects/b.md", "Projects/c.md"])
  }

  func testPinIsIdempotentAndDoesNotReorderAnExistingPin() {
    var pins = SidebarPins()
    pins.pin("f/a.md", inFolder: "f")
    pins.pin("f/b.md", inFolder: "f")
    pins.pin("f/a.md", inFolder: "f")
    XCTAssertEqual(pins.paths(forFolder: "f"), ["f/a.md", "f/b.md"])
  }

  func testUnpinAllClearsOnlyThatFolder() {
    var pins = SidebarPins()
    pins.pin("a/x.md", inFolder: "a")
    pins.pin("b/y.md", inFolder: "b")
    pins.unpinAll(inFolder: "a")
    XCTAssertEqual(pins.paths(forFolder: "a"), [])
    XCTAssertEqual(pins.paths(forFolder: "b"), ["b/y.md"])
    XCTAssertTrue(pins.isPinned("b/y.md", inFolder: "b"))
    XCTAssertFalse(pins.isPinned("a/x.md", inFolder: "a"))
  }

  // MARK: - Mutation-driven integrity

  func testRenameWithinFolderRetargetsThePinInPlace() {
    var pins = SidebarPins()
    pins.pin("f/old.md", inFolder: "f")
    pins.pin("f/keep.md", inFolder: "f")
    let changed = pins.applyRename(from: "f/old.md", to: "f/new.md")
    XCTAssertTrue(changed)
    XCTAssertEqual(pins.paths(forFolder: "f"), ["f/new.md", "f/keep.md"])
  }

  func testMoveToAnotherFolderDropsThePin() {
    // Pins are per-folder context (Navigator semantics): a note moved out of
    // the folder is no longer pinned anywhere.
    var pins = SidebarPins()
    pins.pin("f/note.md", inFolder: "f")
    let changed = pins.applyRename(from: "f/note.md", to: "g/note.md")
    XCTAssertTrue(changed)
    XCTAssertEqual(pins.paths(forFolder: "f"), [])
    XCTAssertEqual(pins.paths(forFolder: "g"), [])
  }

  func testFolderRenameRetargetsPinKeysAndMemberPaths() {
    // Renaming a folder moves both the folder key and every pinned path
    // under it, including pins in nested subfolders.
    var pins = SidebarPins()
    pins.pin("old/a.md", inFolder: "old")
    pins.pin("old/sub/b.md", inFolder: "old/sub")
    let changed = pins.applyFolderRename(from: "old", to: "new")
    XCTAssertTrue(changed)
    XCTAssertEqual(pins.paths(forFolder: "new"), ["new/a.md"])
    XCTAssertEqual(pins.paths(forFolder: "new/sub"), ["new/sub/b.md"])
    XCTAssertEqual(pins.paths(forFolder: "old"), [])
    // Prefix safety: "older" is not inside "old".
    var prefix = SidebarPins()
    prefix.pin("older/x.md", inFolder: "older")
    XCTAssertFalse(prefix.applyFolderRename(from: "old", to: "new"))
    XCTAssertEqual(prefix.paths(forFolder: "older"), ["older/x.md"])
  }

  func testDeleteDropsFilePinsAndFolderSubtreePins() {
    var pins = SidebarPins()
    pins.pin("f/a.md", inFolder: "f")
    pins.pin("gone/b.md", inFolder: "gone")
    pins.pin("gone/sub/c.md", inFolder: "gone/sub")

    XCTAssertTrue(pins.applyDelete(paths: ["f/a.md"], deletedFolders: []))
    XCTAssertEqual(pins.paths(forFolder: "f"), [])

    XCTAssertTrue(pins.applyDelete(paths: [], deletedFolders: ["gone"]))
    XCTAssertEqual(pins.paths(forFolder: "gone"), [])
    XCTAssertEqual(pins.paths(forFolder: "gone/sub"), [])
  }

  func testNoOpMutationsReportNoChange() {
    var pins = SidebarPins()
    pins.pin("f/a.md", inFolder: "f")
    XCTAssertFalse(pins.applyRename(from: "x/unrelated.md", to: "x/other.md"))
    XCTAssertFalse(pins.applyDelete(paths: ["x/unrelated.md"], deletedFolders: []))
    XCTAssertFalse(pins.applyFolderRename(from: "x", to: "y"))
    XCTAssertEqual(pins.paths(forFolder: "f"), ["f/a.md"])
  }

  // MARK: - Lazy prune bookkeeping

  func testPruneBookkeepingAllowsOneRewritePerFolderPerSession() {
    var ledger = SidebarPinPruneLedger()
    XCTAssertTrue(ledger.shouldPrune(folder: "f"))
    ledger.markPruned(folder: "f")
    XCTAssertFalse(ledger.shouldPrune(folder: "f"))
    XCTAssertTrue(ledger.shouldPrune(folder: "g"))
    ledger.reset()
    XCTAssertTrue(ledger.shouldPrune(folder: "f"))
  }
}
