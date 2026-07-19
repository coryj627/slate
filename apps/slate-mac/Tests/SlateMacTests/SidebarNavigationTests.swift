// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation
import XCTest

@testable import SlateMac

/// FL3-3/FL3-4 (#660/#661): shortcut activation semantics, the
/// selection-history ring, collapse/expand commands, and Clear Recents.
@MainActor
final class SidebarNavigationTests: XCTestCase {
  private var root: URL!

  override func setUpWithError() throws {
    try super.setUpWithError()
    root = FileManager.default.temporaryDirectory
      .appendingPathComponent("slate-sidebar-nav-\(UUID().uuidString)")
    try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
  }

  override func tearDownWithError() throws {
    try? FileManager.default.removeItem(at: root)
    try super.tearDownWithError()
  }

  private func openVault(
    named name: String,
    files: [String] = [],
    folders: [String] = [],
    sidebarJSON: String? = nil
  ) throws -> (state: AppState, vault: URL) {
    let vault = root.appendingPathComponent(name)
    try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
    for folder in folders {
      try FileManager.default.createDirectory(
        at: vault.appendingPathComponent(folder), withIntermediateDirectories: true)
    }
    for path in files {
      let url = vault.appendingPathComponent(path)
      try FileManager.default.createDirectory(
        at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
      try "# \((path as NSString).lastPathComponent)".write(
        to: url, atomically: true, encoding: .utf8)
    }
    if let sidebarJSON {
      let slate = vault.appendingPathComponent(".slate", isDirectory: true)
      try FileManager.default.createDirectory(at: slate, withIntermediateDirectories: true)
      try sidebarJSON.write(
        to: slate.appendingPathComponent("sidebar.json"), atomically: true, encoding: .utf8)
    }
    let state = AppState(
      recentsStore: RecentVaultsStore(
        fileURL: root.appendingPathComponent("\(name)-recents.json")),
      externalOpener: { _ in true })
    state.openVault(at: vault)
    _ = try XCTUnwrap(state.currentSession).scanInitial(cancel: CancelToken())
    return (state, vault)
  }

  private func publish(_ state: AppState, _ paths: [String] = []) throws {
    _ = state.publishSidebarSelectionSnapshot(
      SidebarSelectionSnapshot(
        sessionIdentity: ObjectIdentifier(try XCTUnwrap(state.currentSession)),
        items: paths.map {
          SidebarSelectionItem(path: $0, isDirectory: false, isMarkdown: true)
        },
        focusedPath: paths.last,
        creationParent: ""))
  }

  // MARK: - History ring (FL3-4.2)

  func testHistoryRecordsDedupesTruncatesAndCaps() throws {
    let (state, _) = try openVault(named: "ring", folders: ["A", "B", "C"])
    state.recordSidebarSelectionForHistory(path: "A", isDirectory: true)
    state.recordSidebarSelectionForHistory(path: "A", isDirectory: true)
    state.recordSidebarSelectionForHistory(path: "B", isDirectory: true)
    XCTAssertEqual(state.sidebarSelectionHistory.map(\.path), ["A", "B"])
    XCTAssertEqual(state.sidebarSelectionHistoryIndex, 1)

    // Navigate back, then a NEW selection truncates the forward tail.
    try publish(state)
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarHistoryBack)
    XCTAssertEqual(state.sidebarSelectionHistoryIndex, 0)
    XCTAssertEqual(state.sidebarRevealRequest?.path, "A")
    XCTAssertEqual(state.sidebarRevealRequest?.isDirectory, true)
    // The navigated-to arrival equals the cursor — no push.
    state.recordSidebarSelectionForHistory(path: "A", isDirectory: true)
    XCTAssertEqual(state.sidebarSelectionHistory.map(\.path), ["A", "B"])
    state.recordSidebarSelectionForHistory(path: "C", isDirectory: true)
    XCTAssertEqual(state.sidebarSelectionHistory.map(\.path), ["A", "C"])
    XCTAssertEqual(state.sidebarSelectionHistoryIndex, 1)

    for index in 0..<80 {
      state.recordSidebarSelectionForHistory(
        path: "bulk-\(index)", isDirectory: true)
    }
    XCTAssertEqual(
      state.sidebarSelectionHistory.count,
      AppState.sidebarSelectionHistoryCap)
    XCTAssertEqual(state.sidebarSelectionHistory.last?.path, "bulk-79")
  }

  func testHistoryBackSkipsFilesThatNoLongerResolve() throws {
    let (state, _) = try openVault(named: "skip", folders: ["Kept"])
    // A file entry with no matching live scan row is skipped; the folder
    // entry behind it is the landing point.
    state.recordSidebarSelectionForHistory(path: "Kept", isDirectory: true)
    state.recordSidebarSelectionForHistory(path: "ghost.md", isDirectory: false)
    state.recordSidebarSelectionForHistory(path: "Kept2", isDirectory: true)
    try publish(state)
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarHistoryBack)
    XCTAssertEqual(
      state.sidebarRevealRequest?.path, "Kept",
      "the vanished file entry is skipped, not revealed")
    XCTAssertEqual(state.sidebarSelectionHistoryIndex, 0)
  }

  func testHistoryPurgesDeletedEntriesViaStructuralTransform() throws {
    let (state, _) = try openVault(named: "purge", folders: ["A", "B"])
    state.recordSidebarSelectionForHistory(path: "A", isDirectory: true)
    state.recordSidebarSelectionForHistory(path: "B", isDirectory: true)
    state.recordSidebarSelectionForHistory(path: "A/x.md", isDirectory: false)
    state.applySidebarPinsMutation(
      .delete(path: "A", parent: "", wasDirectory: true))
    XCTAssertEqual(
      state.sidebarSelectionHistory.map(\.path), ["B"],
      "the deleted folder and its descendant purge; the cursor stays coherent")
    XCTAssertEqual(state.sidebarSelectionHistoryIndex, 0)
  }

  func testHistoryEndsAnnounceTypedFailures() throws {
    let (state, _) = try openVault(named: "ends")
    try publish(state)
    XCTAssertThrowsError(
      try state.dispatchSidebarAction(id: SlateCommandID.sidebarHistoryBack))
    XCTAssertThrowsError(
      try state.dispatchSidebarAction(id: SlateCommandID.sidebarHistoryForward))
  }

  // MARK: - Collapse / Expand (FL3-4.1)

  func testCollapseAllKeepsAncestorsOfCurrentSelection() throws {
    let (state, _) = try openVault(
      named: "collapse", files: ["A/B/note.md"], folders: ["A", "A/B", "C"])
    state.treeExpandedDirPaths = ["A", "A/B", "C"]
    state.selectedFilePath = "A/B/note.md"
    try publish(state)
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarCollapseAll)
    XCTAssertEqual(
      state.treeExpandedDirPaths, ["A", "A/B"],
      "ancestors of the selection stay expanded; everything else collapses")
    XCTAssertEqual(state.lastMutationAnnouncement, "Collapsed all folders.")
  }

  func testExpandLoadedBumpsTheOneShotRequestAndAnnounces() throws {
    let (state, _) = try openVault(named: "expand")
    try publish(state)
    let before = state.sidebarExpandLoadedRequest
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarExpandLoaded)
    XCTAssertEqual(state.sidebarExpandLoadedRequest, before + 1)
    XCTAssertEqual(state.lastMutationAnnouncement, "Expanded loaded folders.")
  }

  // MARK: - Shortcuts activation (FL3-3.2)

  func testOpenShortcutFolderRevealsAndFileOpens() async throws {
    let (state, vault) = try openVault(
      named: "activate", files: ["Projects/note.md"], folders: ["Projects"],
      sidebarJSON: """
        {"version": 1,
         "shortcuts": [
           {"kind": "folder", "path": "Projects"},
           {"kind": "tag", "path": "reserved"},
           {"kind": "file", "path": "Projects/note.md"}]}
        """)
    _ = vault
    try publish(state)
    _ = try state.dispatchSidebarAction(
      id: SlateCommandID.sidebarOpenShortcut(1))
    XCTAssertEqual(state.sidebarRevealRequest?.path, "Projects")
    XCTAssertEqual(state.sidebarRevealRequest?.isDirectory, true)

    // Slot 2 is the FILE shortcut — the reserved tag entry is invisible.
    _ = try state.dispatchSidebarAction(
      id: SlateCommandID.sidebarOpenShortcut(2))
    await state.noteLoadTask?.value
    XCTAssertEqual(
      state.selectedFilePath, "Projects/note.md",
      "a file shortcut opens through the normal seam")
    XCTAssertEqual(
      state.fileRecents.first, "Projects/note.md",
      "the normal open seam records recency")

    XCTAssertThrowsError(
      try state.dispatchSidebarAction(
        id: SlateCommandID.sidebarOpenShortcut(3)),
      "an empty slot is a typed failure")
  }

  // MARK: - Add / Remove / Move dispatch (FL3-3.2)

  func testShortcutLifecycleDispatchPersistsAndPreservesReservedKinds()
    async throws
  {
    let (state, vault) = try openVault(
      named: "lifecycle", files: ["A/x.md"], folders: ["A"],
      sidebarJSON: """
        {"version": 1, "shortcuts": [{"kind": "tag", "path": "keep"}]}
        """)
    _ = state.publishSidebarSelectionSnapshot(
      SidebarSelectionSnapshot(
        sessionIdentity: ObjectIdentifier(try XCTUnwrap(state.currentSession)),
        items: [SidebarSelectionItem(path: "A", isDirectory: true, isMarkdown: false)],
        focusedPath: "A",
        creationParent: "A"))
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarAddShortcut)
    await state.sidebarOrganizationPersistTaskForTesting?.value
    XCTAssertEqual(
      state.sidebarOrganization.shortcuts,
      [SidebarShortcut(kind: .folder, path: "A")])
    XCTAssertThrowsError(
      try state.dispatchSidebarAction(id: SlateCommandID.sidebarAddShortcut),
      "adding twice is refused")

    let data = try Data(
      contentsOf: vault.appendingPathComponent(".slate/sidebar.json"))
    let json =
      try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
    let raw = try XCTUnwrap(json["shortcuts"] as? [[String: Any]])
    XCTAssertEqual(raw.count, 2)
    XCTAssertEqual(raw[0]["kind"] as? String, "tag", "reserved kinds survive")
    XCTAssertEqual(raw[1]["path"] as? String, "A")

    _ = try state.dispatchSidebarAction(
      id: SlateCommandID.sidebarRemoveShortcut)
    await state.sidebarOrganizationPersistTaskForTesting?.value
    XCTAssertTrue(state.sidebarOrganization.shortcuts.isEmpty)
    XCTAssertThrowsError(
      try state.dispatchSidebarAction(
        id: SlateCommandID.sidebarRemoveShortcut),
      "removing a non-member is refused")
  }

  func testMoveSidebarShortcutSwapsVisibleNeighborsAndPersists() async throws {
    let (state, vault) = try openVault(
      named: "move", files: ["a.md", "b.md"],
      sidebarJSON: """
        {"version": 1,
         "shortcuts": [
           {"kind": "file", "path": "a.md"},
           {"kind": "untagged", "path": ""},
           {"kind": "file", "path": "b.md"}]}
        """)
    state.moveSidebarShortcut(
      SidebarShortcut(kind: .file, path: "b.md"), delta: -1)
    await state.sidebarOrganizationPersistTaskForTesting?.value
    XCTAssertEqual(
      state.sidebarOrganization.shortcuts.map(\.path), ["b.md", "a.md"])
    let data = try Data(
      contentsOf: vault.appendingPathComponent(".slate/sidebar.json"))
    let json =
      try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
    let raw = try XCTUnwrap(json["shortcuts"] as? [[String: Any]])
    XCTAssertEqual(raw.map { $0["kind"] as? String }, ["file", "untagged", "file"])
    XCTAssertEqual(raw[0]["path"] as? String, "b.md")
    XCTAssertEqual(raw[2]["path"] as? String, "a.md")
  }

  // MARK: - Recents (FL3-3.3)

  func testClearRecentsEmptiesSharedHistoryAndAnnounces() throws {
    let (state, _) = try openVault(named: "clear", files: ["a.md"])
    state.recordFileOpen(path: "a.md")
    XCTAssertEqual(state.fileRecents, ["a.md"])
    try publish(state)
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarClearRecents)
    XCTAssertEqual(state.fileRecents, [])
    XCTAssertEqual(state.lastMutationAnnouncement, "Recents cleared.")
  }

  func testSidebarRecentsDisplayExcludesCurrentAndCapsAtTen() throws {
    let (state, _) = try openVault(named: "display")
    for index in (0..<15).reversed() {
      state.recordFileOpen(path: "n\(index).md")
    }
    state.selectedFilePath = "n0.md"
    let display = state.sidebarRecentsForDisplay
    XCTAssertEqual(display.count, 10)
    XCTAssertFalse(display.contains("n0.md"), "the current file is excluded")
    XCTAssertEqual(display.first, "n1.md")
  }
}
