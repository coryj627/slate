// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// FL3-3 (#660): vault-local shortcuts schema — decode, exact-op
/// mutations against the RAW authored array, reserved-kind preservation,
/// and structural-transform integrity.
///
/// #1332 (plan 43 PR 25, owner decision D6, 2026-10-16: converge to the
/// Windows slot model): a shortcut is a SLOT, not a target. One note in
/// two slots is two rows, each named apart, through a restart, a rename
/// and a move, and removing a slot removes that slot and names it. The
/// slot facts below are the twins of Windows' `SharedNameTests`
/// (`OneNoteInTwoShortcutSlotsIsTwoItems`,
/// `TwoShortcutSlotsOnOneNoteSurviveARestartARenameAndAMove`).
final class SidebarShortcutsTests: XCTestCase {
  private var root: URL!

  override func setUpWithError() throws {
    try super.setUpWithError()
    root = FileManager.default.temporaryDirectory
      .appendingPathComponent("slate-sidebar-shortcuts-\(UUID().uuidString)")
    try FileManager.default.createDirectory(
      at: root, withIntermediateDirectories: true)
  }

  override func tearDownWithError() throws {
    try? FileManager.default.removeItem(at: root)
    try super.tearDownWithError()
  }

  private func entry(_ kind: String, _ path: String) -> [String: Any] {
    ["kind": kind, "path": path]
  }

  // MARK: - Vault fixtures (the slot facts run through AppState)

  private func makeVault(
    named name: String, files: [String], folders: [String] = [],
    sidebarJSON: String
  ) throws -> URL {
    let vault = root.appendingPathComponent(name)
    for folder in folders {
      try FileManager.default.createDirectory(
        at: vault.appendingPathComponent(folder),
        withIntermediateDirectories: true)
    }
    for path in files {
      let url = vault.appendingPathComponent(path)
      try FileManager.default.createDirectory(
        at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
      try "plain body\n".write(to: url, atomically: true, encoding: .utf8)
    }
    let slate = vault.appendingPathComponent(".slate", isDirectory: true)
    try FileManager.default.createDirectory(
      at: slate, withIntermediateDirectories: true)
    try sidebarJSON.write(
      to: slate.appendingPathComponent("sidebar.json"), atomically: true,
      encoding: .utf8)
    return vault
  }

  /// One app launch on the vault: a fresh AppState reads the settings
  /// file the way a restart does.
  @MainActor
  private func launch(_ vault: URL) async throws -> AppState {
    let state = AppState(
      recentsStore: RecentVaultsStore(
        fileURL: root.appendingPathComponent("recents-\(UUID().uuidString).json")),
      externalOpener: { _ in true })
    state.openVault(at: vault)
    await state.scanTask?.value
    XCTAssertNil(state.sidebarVaultPrefsNotice, "the settings file is adopted")
    return state
  }

  private func rawShortcuts(_ vault: URL) throws -> [[String: Any]] {
    let data = try Data(
      contentsOf: vault.appendingPathComponent(".slate/sidebar.json"))
    let json = try XCTUnwrap(
      JSONSerialization.jsonObject(with: data) as? [String: Any])
    return try XCTUnwrap(json["shortcuts"] as? [[String: Any]])
  }

  @MainActor
  private func slotPaths(_ state: AppState) -> [String] {
    state.sidebarOrganization.shortcuts.map(\.path)
  }

  @MainActor
  private func spokenNames(_ state: AppState) -> [String] {
    SidebarShortcutRow.rows(for: state.sidebarOrganization.shortcuts)
      .map(\.spokenName)
  }

  // MARK: - D6: every authored slot is a row (#1332)

  /// Twin of Windows' `OneNoteInTwoShortcutSlotsIsTwoItems`: two slots
  /// on one note are two rows — two identities, two names, the names
  /// Windows' hosted list gives them — and removing the second leaves
  /// the first.
  @MainActor
  func testOneNoteInTwoShortcutSlotsIsTwoItems() async throws {
    let vault = try makeVault(
      named: "shortcut-occurrences", files: ["note.md"],
      sidebarJSON: """
        {"version": 1,
         "shortcuts": [
           {"kind": "file", "path": "note.md", "color": "red"},
           {"kind": "file", "path": "note.md", "color": "blue"}]}
        """)
    let state = try await launch(vault)
    let note = SidebarShortcut(kind: .file, path: "note.md")
    XCTAssertEqual(state.sidebarOrganization.shortcuts, [note, note])

    // Two items: each row is its slot, so a ForEach keyed by row id
    // never folds the two into one.
    let rows = SidebarShortcutRow.rows(for: state.sidebarOrganization.shortcuts)
    XCTAssertEqual(rows.map(\.id), [1, 2])
    XCTAssertEqual(rows.map(\.shortcut), [note, note])
    XCTAssertEqual(
      rows.map(\.spokenName),
      [
        "note.md, file shortcut, note.md, shortcut 1",
        "note.md, file shortcut, note.md, shortcut 2",
      ])

    try state.removeSidebarShortcutDirect(slot: 2)
    await state.sidebarOrganizationPersistTaskForTesting?.value
    XCTAssertEqual(state.sidebarOrganization.shortcuts, [note])
    let raw = try rawShortcuts(vault)
    XCTAssertEqual(raw.count, 1)
    XCTAssertEqual(
      raw.first?["color"] as? String, "red", "the first slot is the one left")
    XCTAssertEqual(spokenNames(state), ["note.md, file shortcut"])
  }

  /// Twin of Windows' `TwoShortcutSlotsOnOneNoteSurviveARestartARenameAndAMove`:
  /// every slot survives a restart, a rename and a move, in order, so no
  /// later ⌃-number binding shifts.
  @MainActor
  func testTwoShortcutSlotsOnOneNoteSurviveARestartARenameAndAMove()
    async throws
  {
    let vault = try makeVault(
      named: "shortcut-slots-persist", files: ["b.md", "other.md"],
      folders: ["sub"],
      sidebarJSON: """
        {"version": 1,
         "shortcuts": [
           {"kind": "file", "path": "b.md"},
           {"kind": "file", "path": "b.md"},
           {"kind": "file", "path": "other.md"}]}
        """)
    let first = try await launch(vault)
    XCTAssertEqual(slotPaths(first), ["b.md", "b.md", "other.md"])
    _ = first.closeVault()

    // A restart reads every slot back, in order — slot 3 still opens
    // other.md.
    let state = try await launch(vault)
    XCTAssertEqual(slotPaths(state), ["b.md", "b.md", "other.md"])
    _ = state.publishSidebarSelectionSnapshot(
      SidebarSelectionSnapshot(
        sessionIdentity: ObjectIdentifier(try XCTUnwrap(state.currentSession)),
        items: [],
        focusedPath: nil,
        creationParent: ""))
    _ = try state.dispatchSidebarAction(
      id: SlateCommandID.sidebarOpenShortcut(3))
    await state.noteLoadTask?.value
    XCTAssertEqual(state.selectedFilePath, "other.md")

    // A rename carries both slots, in place, in memory and on disk.
    await state.renameEntry(path: "b.md", isDirectory: false, to: "c.md")?.value
    XCTAssertEqual(
      slotPaths(state), ["c.md", "c.md", "other.md"],
      "the published slots follow the rename at once")
    await state.sidebarOrganizationPersistTaskForTesting?.value
    XCTAssertEqual(slotPaths(state), ["c.md", "c.md", "other.md"])
    XCTAssertEqual(
      try rawShortcuts(vault).map { $0["path"] as? String },
      ["c.md", "c.md", "other.md"])

    // ...and so does a move.
    await state.moveEntry(path: "c.md", isDirectory: false, to: "sub")?.value
    await state.sidebarOrganizationPersistTaskForTesting?.value
    XCTAssertTrue(
      FileManager.default.fileExists(
        atPath: vault.appendingPathComponent("sub/c.md").path))
    XCTAssertEqual(slotPaths(state), ["sub/c.md", "sub/c.md", "other.md"])
    XCTAssertEqual(
      try rawShortcuts(vault).map { $0["path"] as? String },
      ["sub/c.md", "sub/c.md", "other.md"])
    _ = state.closeVault()

    let restarted = try await launch(vault)
    XCTAssertEqual(slotPaths(restarted), ["sub/c.md", "sub/c.md", "other.md"])
  }

  /// AR-58's mac half: removing a slot removes THAT slot — not the first
  /// slot with its target, not every slot with it — and the announcement
  /// names it as its row was named before it left (after the removal the
  /// first slot reads bare, so a name composed afterwards would be the
  /// wrong one).
  @MainActor
  func testRemovingTheSecondSlotRemovesTheSecondAndNamesIt() async throws {
    let vault = try makeVault(
      named: "shortcut-remove-second", files: ["note.md", "other.md"],
      sidebarJSON: """
        {"version": 1,
         "shortcuts": [
           {"kind": "file", "path": "note.md", "color": "red"},
           {"kind": "file", "path": "note.md", "color": "blue"},
           {"kind": "file", "path": "other.md", "color": "green"}]}
        """)
    let state = try await launch(vault)
    XCTAssertEqual(
      spokenNames(state),
      [
        "note.md, file shortcut, note.md, shortcut 1",
        "note.md, file shortcut, note.md, shortcut 2",
        "other.md, file shortcut",
      ])

    try state.removeSidebarShortcutDirect(slot: 2)
    // PR 23 (#944): spoken once its journal line is durable.
    await state.sidebarOrganizationAcknowledgementTaskForTesting?.value
    XCTAssertEqual(
      state.lastMutationAnnouncement,
      "Removed from Shortcuts: note.md, file shortcut, note.md, shortcut 2.")
    XCTAssertEqual(slotPaths(state), ["note.md", "other.md"])
    await state.sidebarOrganizationPersistTaskForTesting?.value
    XCTAssertEqual(slotPaths(state), ["note.md", "other.md"])
    XCTAssertEqual(
      try rawShortcuts(vault).map { $0["color"] as? String },
      ["red", "green"],
      "the second slot left; the first and third kept their place and keys")
    XCTAssertEqual(
      spokenNames(state), ["note.md, file shortcut", "other.md, file shortcut"])

    // A slot that no longer exists is refused with a reason, never a
    // silent no-op, and the file is untouched.
    XCTAssertThrowsError(try state.removeSidebarShortcutDirect(slot: 3)) {
      error in
      XCTAssertEqual(error.sidebarActionAnnouncement, "No shortcut 3.")
    }
  }

  /// Moves are by slot too: moving the SECOND of two slots on one note
  /// moves the second, where a target-keyed move found the first and
  /// swapped two equal entries — nothing moved.
  @MainActor
  func testMovingTheSecondSlotMovesTheSecond() async throws {
    let vault = try makeVault(
      named: "shortcut-move-second", files: ["b.md", "other.md"],
      sidebarJSON: """
        {"version": 1,
         "shortcuts": [
           {"kind": "file", "path": "b.md", "color": "red"},
           {"kind": "file", "path": "b.md", "color": "blue"},
           {"kind": "file", "path": "other.md", "color": "green"}]}
        """)
    let state = try await launch(vault)
    state.moveSidebarShortcut(slot: 2, delta: 1)
    XCTAssertEqual(slotPaths(state), ["b.md", "other.md", "b.md"])
    await state.sidebarOrganizationPersistTaskForTesting?.value
    XCTAssertEqual(slotPaths(state), ["b.md", "other.md", "b.md"])
    XCTAssertEqual(
      try rawShortcuts(vault).map { $0["color"] as? String },
      ["red", "green", "blue"])
  }

  /// R-4's flattened-list rule over the shortcut rows, the shortcuts half
  /// of Windows' `FlatFileListsTellASharedFileNameApartByPath`: two notes
  /// that share a name (or only read alike — case is not spoken) are told
  /// apart by path, a name nothing shares stays bare, and removing one of
  /// the pair re-reads the other bare.
  func testShortcutNamesTellASharedFileNameApartByPath() {
    let shared = [
      SidebarShortcut(kind: .file, path: "A/note.md"),
      SidebarShortcut(kind: .file, path: "B/note.md"),
      SidebarShortcut(kind: .file, path: "notes-extra.md"),
    ]
    XCTAssertEqual(
      SidebarShortcutRow.rows(for: shared).map(\.spokenName),
      [
        "note.md, file shortcut, A/note.md",
        "note.md, file shortcut, B/note.md",
        "notes-extra.md, file shortcut",
      ])
    XCTAssertEqual(
      SidebarShortcutRow.rows(for: [shared[0], shared[2]]).map(\.spokenName),
      ["note.md, file shortcut", "notes-extra.md, file shortcut"])
    XCTAssertEqual(
      SidebarShortcutRow.rows(for: [
        SidebarShortcut(kind: .file, path: "A/Note.md"),
        SidebarShortcut(kind: .file, path: "B/note.md"),
      ]).map(\.spokenName),
      ["Note.md, file shortcut, A/Note.md", "note.md, file shortcut, B/note.md"])
    // Container kinds carry no path to tell slots apart: two slots on one
    // tag take the ordinal straight away.
    XCTAssertEqual(
      SidebarShortcutRow.rows(for: [
        SidebarShortcut(kind: .tag, path: "research"),
        SidebarShortcut(kind: .folder, path: "Projects/Sub"),
        SidebarShortcut(kind: .tag, path: "research"),
      ]).map(\.spokenName),
      [
        "#research, tag shortcut, shortcut 1",
        "Sub, folder shortcut",
        "#research, tag shortcut, shortcut 3",
      ])
  }

  func testDecodeKeepsEveryKnownKindIncludingTagContainersAndEverySlot() {
    // FL5-2: tag/untagged graduated from reserved-opaque to first-class
    // container kinds — they decode as visible rows now.
    // D6 (#1332): a repeated entry is a second SLOT, kept in place — the
    // old first-occurrence-wins collapse is gone (the decode cap bounds
    // repetition instead; see SidebarOrganizationTests).
    let root: [String: Any] = [
      "shortcuts": [
        entry("file", "Projects/a.md"),
        entry("tag", "research"),
        entry("folder", "Projects"),
        entry("file", "Projects/a.md"),
        entry("untagged", ""),
        ["kind": "file"],
      ]
    ]
    XCTAssertEqual(
      SidebarOrganizationSchema.decodeShortcuts(root: root),
      [
        SidebarShortcut(kind: .file, path: "Projects/a.md"),
        SidebarShortcut(kind: .tag, path: "research"),
        SidebarShortcut(kind: .folder, path: "Projects"),
        SidebarShortcut(kind: .file, path: "Projects/a.md"),
        SidebarShortcut(kind: .untagged, path: ""),
      ])
  }

  func testTransformsNeverRewriteOrDropTagNamespaceShortcuts() {
    // FL5-2: a tag whose TEXT collides with a file path lives in a
    // disjoint namespace — folder renames and deletes must not touch it.
    var transform = SidebarStructuralTransform()
    transform.renames = [
      SidebarStructuralTransform.Rename(
        oldPath: "projects", newPath: "work", isDirectory: true)
    ]
    transform.deletedFolders = ["archive"]
    var shortcuts = [
      SidebarShortcut(kind: .tag, path: "projects/reading"),
      SidebarShortcut(kind: .tag, path: "archive/old"),
      SidebarShortcut(kind: .untagged, path: ""),
      SidebarShortcut(kind: .folder, path: "projects/sub"),
    ]
    XCTAssertTrue(transform.apply(to: &shortcuts))
    XCTAssertEqual(
      shortcuts,
      [
        SidebarShortcut(kind: .tag, path: "projects/reading"),
        SidebarShortcut(kind: .tag, path: "archive/old"),
        SidebarShortcut(kind: .untagged, path: ""),
        SidebarShortcut(kind: .folder, path: "work/sub"),
      ])
  }

  func testShapeGuardAcceptsReservedKindsAndRejectsMalformedEntries() {
    XCTAssertTrue(
      SidebarOrganizationSchema.knownSectionShapesAreValid(root: [
        "shortcuts": [
          entry("file", "a.md"), entry("tag", "x"), entry("untagged", ""),
        ]
      ]))
    XCTAssertFalse(
      SidebarOrganizationSchema.knownSectionShapesAreValid(root: [
        "shortcuts": ["not an object"]
      ]))
    XCTAssertFalse(
      SidebarOrganizationSchema.knownSectionShapesAreValid(root: [
        "shortcuts": [["kind": "file"]]
      ]))
    XCTAssertFalse(
      SidebarOrganizationSchema.knownSectionShapesAreValid(root: [
        "shortcuts": "nope"
      ]))
    XCTAssertFalse(
      SidebarOrganizationSchema.knownSectionShapesAreValid(root: [
        "shortcuts": Array(
          repeating: entry("file", "a.md"),
          count: SidebarOrganizationSchema.maxShortcuts + 1)
      ]))
    XCTAssertFalse(
      SidebarOrganizationSchema.knownSectionShapesAreValid(root: [
        "shortcuts": [
          entry("file", String(repeating: "a", count: 4_097))
        ]
      ]))
    XCTAssertTrue(
      SidebarOrganizationSchema.knownSectionShapesAreValid(root: [
        "shortcuts": Array(
          repeating: entry("file", "a.md"),
          count: SidebarOrganizationSchema.maxShortcuts)
      ]))
  }

  func testAddRemoveMutateOnlyTheirEntryAndPreserveEverythingElse() {
    var root: [String: Any] = [
      "version": 1,
      "future": ["x": true],
      "shortcuts": [
        entry("tag", "research"),
        ["kind": "file", "path": "Projects/a.md", "color": "red"],
      ],
    ]
    SidebarOrganizationSchema.addShortcut(
      &root, kind: "folder", path: "Projects")
    SidebarOrganizationSchema.addShortcut(
      &root, kind: "folder", path: "Projects")

    var raw = root["shortcuts"] as? [Any]
    XCTAssertEqual(raw?.count, 3)
    XCTAssertEqual((raw?[0] as? [String: Any])?["kind"] as? String, "tag")
    XCTAssertEqual(
      (raw?[1] as? [String: Any])?["color"] as? String, "red",
      "unknown entry keys survive untouched")
    XCTAssertEqual((raw?[2] as? [String: Any])?["path"] as? String, "Projects")
    XCTAssertEqual((root["future"] as? [String: Any])?["x"] as? Bool, true)

    SidebarOrganizationSchema.removeShortcut(
      &root, kind: "file", path: "Projects/a.md")
    raw = root["shortcuts"] as? [Any]
    XCTAssertEqual(raw?.count, 2)
    XCTAssertEqual((raw?[0] as? [String: Any])?["kind"] as? String, "tag")
    XCTAssertEqual((raw?[1] as? [String: Any])?["kind"] as? String, "folder")
  }

  func testMoveSwapsVisibleNeighborsAcrossReservedEntriesAndClampsAtEdges() {
    var root: [String: Any] = [
      "shortcuts": [
        entry("file", "a.md"),
        entry("tag", "research"),
        entry("file", "b.md"),
        entry("folder", "F"),
      ]
    ]
    // FL5-2: tag entries are visible rows now — b.md's nearest visible
    // neighbor IS the tag entry, and they swap directly.
    SidebarOrganizationSchema.moveShortcut(
      &root, kind: "file", path: "b.md", delta: -1)
    var paths = (root["shortcuts"] as? [Any])?.compactMap {
      ($0 as? [String: Any])?["path"] as? String
    }
    XCTAssertEqual(paths, ["a.md", "b.md", "research", "F"])

    // A second move-up swaps with a.md; the top edge then clamps.
    SidebarOrganizationSchema.moveShortcut(
      &root, kind: "file", path: "b.md", delta: -1)
    paths = (root["shortcuts"] as? [Any])?.compactMap {
      ($0 as? [String: Any])?["path"] as? String
    }
    XCTAssertEqual(paths, ["b.md", "a.md", "research", "F"])
    SidebarOrganizationSchema.moveShortcut(
      &root, kind: "file", path: "b.md", delta: -1)
    paths = (root["shortcuts"] as? [Any])?.compactMap {
      ($0 as? [String: Any])?["path"] as? String
    }
    XCTAssertEqual(paths, ["b.md", "a.md", "research", "F"])

    // Bottom edge clamps.
    SidebarOrganizationSchema.moveShortcut(
      &root, kind: "folder", path: "F", delta: 1)
    paths = (root["shortcuts"] as? [Any])?.compactMap {
      ($0 as? [String: Any])?["path"] as? String
    }
    XCTAssertEqual(paths, ["b.md", "a.md", "research", "F"])
  }

  func testTransformRetargetsRenamesRespectingKindNamespaces() {
    var transform = SidebarStructuralTransform()
    transform.renames.append(
      .init(oldPath: "Projects", newPath: "Archive", isDirectory: nil))
    var root: [String: Any] = [
      "shortcuts": [
        entry("folder", "Projects"),
        entry("file", "Projects/a.md"),
        entry("folder", "Projects/Sub"),
        entry("tag", "Projects"),
        entry("file", "Other/b.md"),
      ]
    ]
    transform.applyRaw(to: &root)
    let raw = (root["shortcuts"] as? [Any])?.compactMap { $0 as? [String: Any] }
    XCTAssertEqual(raw?[0]["path"] as? String, "Archive")
    XCTAssertEqual(raw?[1]["path"] as? String, "Archive/a.md")
    XCTAssertEqual(raw?[2]["path"] as? String, "Archive/Sub")
    XCTAssertEqual(
      raw?[3]["path"] as? String, "Projects",
      "reserved kinds are untouched by path transforms")
    XCTAssertEqual(raw?[4]["path"] as? String, "Other/b.md")
  }

  func testTransformKindSpecificExactRename() {
    // A FILE rename must not retarget a folder shortcut sharing the string.
    var transform = SidebarStructuralTransform()
    transform.renames.append(
      .init(oldPath: "Notes", newPath: "Journal", isDirectory: false))
    var shortcuts = [
      SidebarShortcut(kind: .folder, path: "Notes"),
      SidebarShortcut(kind: .file, path: "Notes"),
    ]
    transform.apply(to: &shortcuts)
    XCTAssertEqual(
      shortcuts,
      [
        SidebarShortcut(kind: .folder, path: "Notes"),
        SidebarShortcut(kind: .file, path: "Journal"),
      ])
  }

  func testTransformDeletesRemoveTargetsAndDescendants() {
    var transform = SidebarStructuralTransform()
    transform.deletedFolders.append("Projects")
    transform.deletedFiles.append("Other/gone.md")
    var shortcuts = [
      SidebarShortcut(kind: .folder, path: "Projects"),
      SidebarShortcut(kind: .file, path: "Projects/a.md"),
      SidebarShortcut(kind: .folder, path: "Projects/Sub"),
      SidebarShortcut(kind: .file, path: "Other/gone.md"),
      SidebarShortcut(kind: .file, path: "Other/kept.md"),
    ]
    transform.apply(to: &shortcuts)
    XCTAssertEqual(
      shortcuts, [SidebarShortcut(kind: .file, path: "Other/kept.md")])
  }

  func testTransformConvergenceKeepsEverySlotInOrder() {
    // D6 (#1332): a rename that lands one slot on another slot's target
    // keeps both, in place — the old source-wins drop silently deleted a
    // slot and shifted every later ⌃-number binding down one.
    var transform = SidebarStructuralTransform()
    transform.renames.append(
      .init(oldPath: "A/x.md", newPath: "B/x.md", isDirectory: false))
    var root: [String: Any] = [
      "shortcuts": [
        ["kind": "file", "path": "A/x.md", "color": "red"],
        ["kind": "file", "path": "B/x.md", "color": "blue"],
        entry("file", "C/y.md"),
      ]
    ]
    transform.applyRaw(to: &root)
    let raw = (root["shortcuts"] as? [Any])?.compactMap { $0 as? [String: Any] }
    XCTAssertEqual(raw?.map { $0["path"] as? String }, ["B/x.md", "B/x.md", "C/y.md"])
    XCTAssertEqual(
      raw?.map { $0["color"] as? String }, ["red", "blue", nil],
      "each slot keeps its own unknown keys")

    var shortcuts = [
      SidebarShortcut(kind: .file, path: "A/x.md"),
      SidebarShortcut(kind: .file, path: "B/x.md"),
      SidebarShortcut(kind: .file, path: "C/y.md"),
    ]
    XCTAssertTrue(transform.apply(to: &shortcuts))
    XCTAssertEqual(
      shortcuts,
      [
        SidebarShortcut(kind: .file, path: "B/x.md"),
        SidebarShortcut(kind: .file, path: "B/x.md"),
        SidebarShortcut(kind: .file, path: "C/y.md"),
      ])
  }
  func testShortcutPresentationPerKind() {
    // Review round: container kinds carry their own presentation — an
    // empty-path untagged entry must never render as a blank file row.
    let file = SidebarSectionsView.shortcutPresentation(
      for: SidebarShortcut(kind: .file, path: "Projects/Note.md"))
    XCTAssertEqual(file.label, "Note")
    XCTAssertEqual(file.value, "file shortcut")
    let folder = SidebarSectionsView.shortcutPresentation(
      for: SidebarShortcut(kind: .folder, path: "Projects/Sub"))
    XCTAssertEqual(folder.label, "Sub")
    XCTAssertEqual(folder.value, "folder shortcut")
    let tag = SidebarSectionsView.shortcutPresentation(
      for: SidebarShortcut(kind: .tag, path: "projects/reading"))
    XCTAssertEqual(tag.label, "#projects/reading")
    XCTAssertEqual(tag.value, "tag shortcut")
    XCTAssertEqual(tag.hint, "Filters the file list to this tag.")
    XCTAssertEqual(tag.symbol, .tag)
    let untagged = SidebarSectionsView.shortcutPresentation(
      for: SidebarShortcut(kind: .untagged, path: ""))
    XCTAssertEqual(untagged.label, "Untagged")
    XCTAssertEqual(untagged.value, "untagged shortcut")
    XCTAssertEqual(untagged.hint, "Shows notes with no tags.")
  }

}
