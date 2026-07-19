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

  func testHistoryDiesWithTheVault() throws {
    // Review round: entries are vault-relative — a ring surviving an A→B
    // switch could resolve a same-path entry against the wrong vault.
    let (state, vaultA) = try openVault(named: "hist-a", folders: ["Shared"])
    state.recordSidebarSelectionForHistory(path: "Shared", isDirectory: true)
    XCTAssertEqual(state.sidebarSelectionHistory.count, 1)
    state.closeVault()
    XCTAssertTrue(state.sidebarSelectionHistory.isEmpty)
    XCTAssertEqual(state.sidebarSelectionHistoryIndex, -1)
    XCTAssertNil(state.sidebarRevealRequest)

    // Direct A→B switch clears too (no closeVault on that path).
    state.openVault(at: vaultA)
    _ = try XCTUnwrap(state.currentSession).scanInitial(cancel: CancelToken())
    state.recordSidebarSelectionForHistory(path: "Shared", isDirectory: true)
    let vaultB = root.appendingPathComponent("hist-b")
    try FileManager.default.createDirectory(
      at: vaultB.appendingPathComponent("Shared"),
      withIntermediateDirectories: true)
    state.openVault(at: vaultB)
    XCTAssertTrue(
      state.sidebarSelectionHistory.isEmpty,
      "a direct switch must not carry vault A's ring into vault B")
  }

  // MARK: - Collapse / Expand (FL3-4.1)

  func testCollapseAllBumpsTheLiveTreeRequestAndAnnounces() throws {
    // The mounted tree consumes the one-shot request (mirror writes alone
    // never reach the live view model — review round); the view model's
    // ancestor preservation is covered in SidebarOrganizationTreeTests.
    let (state, _) = try openVault(
      named: "collapse", files: ["A/B/note.md"], folders: ["A", "A/B", "C"])
    try publish(state)
    let before = state.sidebarCollapseAllRequest
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarCollapseAll)
    XCTAssertEqual(state.sidebarCollapseAllRequest, before + 1)
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

  func testFocusFilterDispatchesThroughTheRouterAndBumpsTheRequest() throws {
    // FL-09 regression: the router's navigation case must include
    // focusFilter — membership in `sidebarNavigationCommands` alone
    // doesn't route it, and a missed case throws at the menu/palette.
    let (state, _) = try openVault(named: "focusFilter")
    try publish(state)
    let before = state.sidebarFilterFocusRequest
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarFocusFilter)
    XCTAssertEqual(state.sidebarFilterFocusRequest, before + 1)
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

    // FL5-2: slot 2 is the TAG container — activation drives the
    // shared flat list with the tag query, field showing it.
    _ = try state.dispatchSidebarAction(
      id: SlateCommandID.sidebarOpenShortcut(2))
    XCTAssertEqual(state.sidebarFilterModel.fieldText, "#reserved")
    XCTAssertEqual(state.sidebarFilterModel.committedQuery, "#reserved")
    XCTAssertTrue(state.sidebarFilterModel.isActive)
    state.sidebarFilterModel.escapeInField()

    // Slot 3 is the FILE shortcut.
    _ = try state.dispatchSidebarAction(
      id: SlateCommandID.sidebarOpenShortcut(3))
    await state.noteLoadTask?.value
    XCTAssertEqual(
      state.selectedFilePath, "Projects/note.md",
      "a file shortcut opens through the normal seam")
    XCTAssertEqual(
      state.fileRecents.first, "Projects/note.md",
      "the normal open seam records recency")

    XCTAssertThrowsError(
      try state.dispatchSidebarAction(
        id: SlateCommandID.sidebarOpenShortcut(4)),
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
    // FL5-2: the tag entry is a first-class visible container now.
    XCTAssertEqual(
      state.sidebarOrganization.shortcuts,
      [
        SidebarShortcut(kind: .tag, path: "keep"),
        SidebarShortcut(kind: .folder, path: "A"),
      ])
    XCTAssertThrowsError(
      try state.dispatchSidebarAction(id: SlateCommandID.sidebarAddShortcut),
      "adding twice is refused")

    let data = try Data(
      contentsOf: vault.appendingPathComponent(".slate/sidebar.json"))
    let json =
      try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
    let raw = try XCTUnwrap(json["shortcuts"] as? [[String: Any]])
    XCTAssertEqual(raw.count, 2)
    XCTAssertEqual(raw[0]["kind"] as? String, "tag", "tag kinds survive in place")
    XCTAssertEqual(raw[1]["path"] as? String, "A")

    _ = try state.dispatchSidebarAction(
      id: SlateCommandID.sidebarRemoveShortcut)
    await state.sidebarOrganizationPersistTaskForTesting?.value
    XCTAssertEqual(
      state.sidebarOrganization.shortcuts,
      [SidebarShortcut(kind: .tag, path: "keep")],
      "removing the folder leaves the tag container")
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
    // FL5-2: the untagged entry is a visible row — b.md's nearest
    // neighbor — so they swap directly.
    XCTAssertEqual(
      state.sidebarOrganization.shortcuts.map(\.path), ["a.md", "b.md", ""])
    let data = try Data(
      contentsOf: vault.appendingPathComponent(".slate/sidebar.json"))
    let json =
      try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
    let raw = try XCTUnwrap(json["shortcuts"] as? [[String: Any]])
    XCTAssertEqual(raw.map { $0["kind"] as? String }, ["file", "file", "untagged"])
    XCTAssertEqual(raw[0]["path"] as? String, "a.md")
    XCTAssertEqual(raw[1]["path"] as? String, "b.md")
  }

  func testAddShortcutRefusesWhenRawEntriesAreAtTheCeiling() throws {
    // Review round: the shape guard caps the RAW array (reserved kinds
    // included) — acknowledgement must gate on the same count, not the
    // decoded file/folder subset.
    let reserved = (0..<199).map {
      #"{"kind": "tag", "path": "t\#($0)"}"#
    }.joined(separator: ",")
    let (state, _) = try openVault(
      named: "cap-raw", files: ["x.md"], folders: ["Y"],
      sidebarJSON: """
        {"version": 1,
         "shortcuts": [\(reserved), {"kind": "file", "path": "x.md"}]}
        """)
    XCTAssertEqual(state.sidebarOrganization.shortcutRawEntryCount, 200)
    _ = state.publishSidebarSelectionSnapshot(
      SidebarSelectionSnapshot(
        sessionIdentity: ObjectIdentifier(try XCTUnwrap(state.currentSession)),
        items: [
          SidebarSelectionItem(path: "Y", isDirectory: true, isMarkdown: false)
        ],
        focusedPath: "Y",
        creationParent: "Y"))
    XCTAssertThrowsError(
      try state.dispatchSidebarAction(id: SlateCommandID.sidebarAddShortcut)
    ) { error in
      XCTAssertTrue(
        String(describing: error).contains("Shortcut limit reached"),
        "unexpected refusal: \(error)")
    }
  }

  func testRapidAddsCannotBypassTheRawCeiling() async throws {
    // Codoki review: reflects keep the raw counter in lockstep, so a
    // second add issued BEFORE the first persist settles still preflights
    // against the incremented count.
    let reserved = (0..<198).map {
      #"{"kind": "tag", "path": "t\#($0)"}"#
    }.joined(separator: ",")
    let (state, _) = try openVault(
      named: "cap-race", files: ["x.md"], folders: ["Y", "Z"],
      sidebarJSON: """
        {"version": 1,
         "shortcuts": [\(reserved), {"kind": "file", "path": "x.md"}]}
        """)
    XCTAssertEqual(state.sidebarOrganization.shortcutRawEntryCount, 199)

    func select(_ folder: String) throws {
      _ = state.publishSidebarSelectionSnapshot(
        SidebarSelectionSnapshot(
          sessionIdentity: ObjectIdentifier(
            try XCTUnwrap(state.currentSession)),
          items: [
            SidebarSelectionItem(
              path: folder, isDirectory: true, isMarkdown: false)
          ],
          focusedPath: folder,
          creationParent: folder))
    }
    try select("Y")
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarAddShortcut)
    XCTAssertEqual(state.sidebarOrganization.shortcutRawEntryCount, 200)
    // No persist await: the second add must refuse against the LIVE count.
    try select("Z")
    XCTAssertThrowsError(
      try state.dispatchSidebarAction(id: SlateCommandID.sidebarAddShortcut)
    ) { error in
      XCTAssertTrue(
        String(describing: error).contains("Shortcut limit reached"),
        "unexpected: \(error)")
    }
    await state.sidebarOrganizationPersistTaskForTesting?.value

    // And a remove frees capacity immediately.
    try select("Y")
    _ = try state.dispatchSidebarAction(
      id: SlateCommandID.sidebarRemoveShortcut)
    XCTAssertEqual(state.sidebarOrganization.shortcutRawEntryCount, 199)
    try select("Z")
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarAddShortcut)
    await state.sidebarOrganizationPersistTaskForTesting?.value
    XCTAssertTrue(
      state.sidebarOrganization.shortcuts.contains(
        SidebarShortcut(kind: .folder, path: "Z")))
  }

  func testProductionRenameRespectsShortcutKindNamespaces() async throws {
    // Review round: single rename/move producers carry the node kind, so
    // a FILE rename never rewrites a same-path FOLDER shortcut.
    let (state, vault) = try openVault(
      named: "namespace", files: ["Notes"], folders: [],
      sidebarJSON: """
        {"version": 1,
         "shortcuts": [
           {"kind": "folder", "path": "Notes"},
           {"kind": "file", "path": "Notes"}]}
        """)
    _ = vault
    state.applySidebarPinsMutation(
      .rename(oldPath: "Notes", newPath: "Journal"), isDirectory: false)
    await state.sidebarOrganizationPersistTaskForTesting?.value
    XCTAssertEqual(
      state.sidebarOrganization.shortcuts,
      [
        SidebarShortcut(kind: .folder, path: "Notes"),
        SidebarShortcut(kind: .file, path: "Journal"),
      ],
      "the folder shortcut sharing the string is untouched")
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
