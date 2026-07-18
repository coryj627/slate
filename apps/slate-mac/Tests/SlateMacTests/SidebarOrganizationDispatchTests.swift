// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation
import XCTest

@testable import SlateMac

/// FL-06 AppState funnel: sort/group/pin commands dispatch through the shared
/// catalog, mutate one published organization state, persist atomically to
/// `.slate/sidebar.json` with unknown keys preserved, announce the effective
/// result, stay disabled with a reason when the prefs file is unsafe, and keep
/// pins consistent across structural mutations and the lazy stale prune.
@MainActor
final class SidebarOrganizationDispatchTests: XCTestCase {
  private var root: URL!

  private final class RecordingAnnouncer: AnnouncementPosting, @unchecked Sendable {
    private(set) var posts: [(message: String, priority: AnnouncementPriority)] = []

    func post(_ message: String, priority: AnnouncementPriority) {
      posts.append((message, priority))
    }

    var messages: [String] { posts.map(\.message) }
  }

  override func setUpWithError() throws {
    root = FileManager.default.temporaryDirectory
      .appendingPathComponent("slate-sidebar-organization-\(UUID().uuidString)")
    try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
  }

  override func tearDownWithError() throws {
    try? FileManager.default.removeItem(at: root)
  }

  private func openVault(
    named name: String,
    files: [String] = [],
    folders: [String] = [],
    sidebarJSON: String? = nil,
    announcer: AnnouncementPosting = AppKitAnnouncementPoster()
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
      externalOpener: { _ in true },
      announcer: announcer)
    state.openVault(at: vault)
    let session = try XCTUnwrap(state.currentSession)
    _ = try session.scanInitial(cancel: CancelToken())
    return (state, vault)
  }

  private func snapshot(
    on state: AppState,
    _ items: [SidebarSelectionItem],
    focusedPath: String? = nil,
    creationParent: String = ""
  ) throws -> SidebarSelectionSnapshot {
    SidebarSelectionSnapshot(
      sessionIdentity: ObjectIdentifier(try XCTUnwrap(state.currentSession)),
      items: items,
      focusedPath: focusedPath,
      creationParent: creationParent)
  }

  private func item(_ path: String, directory: Bool = false) -> SidebarSelectionItem {
    SidebarSelectionItem(path: path, isDirectory: directory, isMarkdown: !directory)
  }

  private func awaitPersist(_ state: AppState) async {
    await state.sidebarOrganizationPersistTaskForTesting?.value
  }

  private func sidebarJSON(at vault: URL) throws -> [String: Any] {
    let data = try Data(
      contentsOf: vault.appendingPathComponent(".slate/sidebar.json"))
    return try XCTUnwrap(
      JSONSerialization.jsonObject(with: data) as? [String: Any])
  }

  private func publish(
    _ state: AppState,
    _ items: [SidebarSelectionItem],
    focusedPath: String? = nil,
    creationParent: String = ""
  ) throws {
    _ = state.publishSidebarSelectionSnapshot(
      try snapshot(
        on: state, items, focusedPath: focusedPath, creationParent: creationParent))
  }

  // MARK: - Sort and grouping dispatch

  func testSortCommandOnVaultContainerPersistsAndAnnounces() async throws {
    let announcer = RecordingAnnouncer()
    let (state, vault) = try openVault(
      named: "sort-vault", files: ["a.md"], announcer: announcer)
    try publish(state, [])

    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortModifiedDesc)

    XCTAssertEqual(
      state.sidebarOrganization.prefs.vaultChoice.sort,
      SidebarSortOption(field: .modified, direction: .desc))
    XCTAssertTrue(announcer.messages.contains("Sorted by modified, newest first."))

    await awaitPersist(state)
    let json = try sidebarJSON(at: vault)
    XCTAssertEqual(
      json["sort"] as? [String: String],
      ["field": "modified", "direction": "desc"])
  }

  func testSortCommandOnFolderContainerWritesOnlyTheOverride() async throws {
    let (state, vault) = try openVault(
      named: "sort-folder", files: ["Projects/a.md"], folders: ["Projects"])
    try publish(
      state, [item("Projects", directory: true)],
      focusedPath: "Projects", creationParent: "Projects")

    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortCreatedAsc)

    XCTAssertEqual(
      state.sidebarOrganization.prefs.folderOverrides["Projects"]?.sort,
      SidebarSortOption(field: .created, direction: .asc))
    XCTAssertEqual(state.sidebarOrganization.prefs.vaultChoice, .defaults)

    await awaitPersist(state)
    let json = try sidebarJSON(at: vault)
    let overrides = try XCTUnwrap(json["folderOverrides"] as? [String: Any])
    let projects = try XCTUnwrap(overrides["Projects"] as? [String: Any])
    XCTAssertEqual(
      projects["sort"] as? [String: String],
      ["field": "created", "direction": "asc"])
    XCTAssertNil(json["sort"])
  }

  func testToggleDateGroupingFlipsTheTargetContainer() async throws {
    let announcer = RecordingAnnouncer()
    let (state, _) = try openVault(
      named: "grouping", files: ["a.md"], announcer: announcer)
    try publish(state, [])

    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarToggleDateGrouping)
    XCTAssertEqual(state.sidebarOrganization.prefs.vaultChoice.grouping, .dateBuckets)
    XCTAssertTrue(
      announcer.messages.contains(
        "Sorted by modified, newest first, grouped by date."))

    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarToggleDateGrouping)
    XCTAssertEqual(state.sidebarOrganization.prefs.vaultChoice.grouping, .none)
    await awaitPersist(state)
  }

  func testUseVaultDefaultClearsTheOverrideAndHasHonestDisabledReasons() async throws {
    let (state, _) = try openVault(
      named: "use-default", files: ["Projects/a.md"], folders: ["Projects"])

    // Root container: nothing to clear, deterministic reason.
    try publish(state, [])
    let rootEvaluation = state.sidebarActionProjection(surface: .menuBar)
      .first { $0.id == SlateCommandID.sidebarUseVaultDefaultSort }
    XCTAssertEqual(
      rootEvaluation?.disabledReason, "The vault default applies here.")

    // Folder without an override: still disabled, different honest reason.
    try publish(
      state, [item("Projects", directory: true)],
      focusedPath: "Projects", creationParent: "Projects")
    let cleanEvaluation = state.sidebarActionProjection(surface: .menuBar)
      .first { $0.id == SlateCommandID.sidebarUseVaultDefaultSort }
    XCTAssertEqual(
      cleanEvaluation?.disabledReason, "This folder uses the vault default.")

    // With an override the action enables and clears it.
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortNameDesc)
    XCTAssertNotNil(state.sidebarOrganization.prefs.folderOverrides["Projects"])
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarUseVaultDefaultSort)
    XCTAssertNil(state.sidebarOrganization.prefs.folderOverrides["Projects"])
    await awaitPersist(state)
  }

  // MARK: - Pins

  func testPinUnpinLifecycleWithStateDependentAvailability() async throws {
    let announcer = RecordingAnnouncer()
    let (state, vault) = try openVault(
      named: "pins", files: ["Projects/note.md"], folders: ["Projects"],
      announcer: announcer)
    try publish(
      state, [item("Projects/note.md")],
      focusedPath: "Projects/note.md", creationParent: "Projects")

    // Unpin is unavailable before any pin exists.
    let beforeUnpin = state.sidebarActionProjection(surface: .menuBar)
      .first { $0.id == SlateCommandID.sidebarUnpinNote }
    XCTAssertEqual(beforeUnpin?.disabledReason, "This note isn't pinned.")

    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarPinNote)
    XCTAssertTrue(
      state.sidebarOrganization.pins.isPinned(
        "Projects/note.md", inFolder: "Projects"))
    XCTAssertTrue(announcer.messages.contains("Pinned."))

    // Pin is now the unavailable direction; the context menu omits it.
    let afterPin = state.sidebarActionProjection(surface: .menuBar)
      .first { $0.id == SlateCommandID.sidebarPinNote }
    XCTAssertEqual(afterPin?.disabledReason, "This note is already pinned.")
    XCTAssertFalse(
      state.sidebarActionProjection(surface: .contextMenu)
        .contains { $0.id == SlateCommandID.sidebarPinNote })
    XCTAssertTrue(
      state.sidebarActionProjection(surface: .contextMenu)
        .contains { $0.id == SlateCommandID.sidebarUnpinNote })

    await awaitPersist(state)
    let json = try sidebarJSON(at: vault)
    let pins = try XCTUnwrap(json["pins"] as? [String: Any])
    XCTAssertEqual(pins["Projects"] as? [String], ["Projects/note.md"])

    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarUnpinNote)
    XCTAssertFalse(
      state.sidebarOrganization.pins.isPinned(
        "Projects/note.md", inFolder: "Projects"))
    XCTAssertTrue(announcer.messages.contains("Unpinned."))
    await awaitPersist(state)
  }

  func testUnpinAllRequiresAFolderWithPins() async throws {
    let announcer = RecordingAnnouncer()
    let (state, _) = try openVault(
      named: "unpin-all", files: ["Projects/a.md", "Projects/b.md"],
      folders: ["Projects"], announcer: announcer)

    try publish(
      state, [item("Projects", directory: true)],
      focusedPath: "Projects", creationParent: "Projects")
    let empty = state.sidebarActionProjection(surface: .menuBar)
      .first { $0.id == SlateCommandID.sidebarUnpinAll }
    XCTAssertEqual(empty?.disabledReason, "No pinned notes in this folder.")

    try publish(
      state, [item("Projects/a.md")],
      focusedPath: "Projects/a.md", creationParent: "Projects")
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarPinNote)
    try publish(
      state, [item("Projects/b.md")],
      focusedPath: "Projects/b.md", creationParent: "Projects")
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarPinNote)

    try publish(
      state, [item("Projects", directory: true)],
      focusedPath: "Projects", creationParent: "Projects")
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarUnpinAll)
    XCTAssertEqual(
      state.sidebarOrganization.pins.paths(forFolder: "Projects"), [])
    XCTAssertTrue(announcer.messages.contains("Unpinned 2 notes."))
    await awaitPersist(state)
  }

  // MARK: - Read-only prefs

  func testOrganizationActionsAreDisabledWhenPrefsFileIsUnsafe() throws {
    let (state, _) = try openVault(
      named: "read-only", files: ["a.md"], sidebarJSON: "{not json")
    XCTAssertNotNil(state.sidebarVaultPrefsNotice)
    try publish(state, [])

    let evaluation = state.sidebarActionProjection(surface: .menuBar)
      .first { $0.id == SlateCommandID.sidebarSortNameAsc }
    let reason = try XCTUnwrap(evaluation?.disabledReason)
    XCTAssertTrue(reason.contains("read-only"), reason)
    XCTAssertThrowsError(
      try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortNameAsc))
  }

  // MARK: - Startup read-back

  func testExistingSidebarJSONPopulatesOrganizationOnVaultOpen() throws {
    let json = """
      {
        "version": 1,
        "sort": {"field": "created", "direction": "desc"},
        "grouping": "dateBuckets",
        "pins": {"": ["a.md"]},
        "future-key": {"keep": true}
      }
      """
    let (state, _) = try openVault(
      named: "read-back", files: ["a.md"], sidebarJSON: json)
    XCTAssertEqual(
      state.sidebarOrganization.prefs.vaultChoice,
      SidebarOrganizationChoice(
        sort: SidebarSortOption(field: .created, direction: .desc),
        grouping: .dateBuckets))
    XCTAssertEqual(state.sidebarOrganization.pins.paths(forFolder: ""), ["a.md"])
  }

  func testUnknownSiblingKeysSurviveAnOrganizationWrite() async throws {
    let json = """
      {"version": 1, "future-key": {"keep": true}}
      """
    let (state, vault) = try openVault(
      named: "unknown-keys", files: ["a.md"], sidebarJSON: json)
    try publish(state, [])
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortNameDesc)
    await awaitPersist(state)
    let written = try sidebarJSON(at: vault)
    XCTAssertEqual(written["future-key"] as? [String: Bool], ["keep": true])
    XCTAssertEqual(
      written["sort"] as? [String: String],
      ["field": "name", "direction": "desc"])
  }

  // MARK: - Structural-mutation pin integrity

  func testStructuralMutationsKeepPinsConsistentAndPersist() async throws {
    let (state, vault) = try openVault(
      named: "mutations",
      files: ["Projects/keep.md", "Projects/old.md", "Projects/gone.md"],
      folders: ["Projects"])
    for path in ["Projects/keep.md", "Projects/old.md", "Projects/gone.md"] {
      try publish(
        state, [item(path)], focusedPath: path, creationParent: "Projects")
      _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarPinNote)
    }

    // Rename within the folder retargets; move out and delete drop.
    state.applySidebarPinsMutation(
      .rename(oldPath: "Projects/old.md", newPath: "Projects/renamed.md"))
    state.applySidebarPinsMutation(
      .delete(path: "Projects/gone.md", parent: "Projects", wasDirectory: false))
    XCTAssertEqual(
      state.sidebarOrganization.pins.paths(forFolder: "Projects"),
      ["Projects/keep.md", "Projects/renamed.md"])

    // A folder rename retargets keys and members together.
    state.applySidebarPinsMutation(
      .rename(oldPath: "Projects", newPath: "Archive"))
    XCTAssertEqual(
      state.sidebarOrganization.pins.paths(forFolder: "Archive"),
      ["Archive/keep.md", "Archive/renamed.md"])
    XCTAssertEqual(state.sidebarOrganization.pins.paths(forFolder: "Projects"), [])

    // Batch trash drops its exact items.
    state.applySidebarPinsMutation(
      .batchTrash(trashed: [
        StructuralBatchItem(path: "Archive/keep.md", isDirectory: false)
      ]))
    XCTAssertEqual(
      state.sidebarOrganization.pins.paths(forFolder: "Archive"),
      ["Archive/renamed.md"])

    await awaitPersist(state)
    let json = try sidebarJSON(at: vault)
    let pins = try XCTUnwrap(json["pins"] as? [String: Any])
    XCTAssertEqual(pins["Archive"] as? [String], ["Archive/renamed.md"])
    XCTAssertNil(pins["Projects"])
  }

  // MARK: - Red-team regressions (adversarial review round 1)

  func testQueuedPersistFromAClosedVaultNeverTouchesTheFile() async throws {
    // Finding 1: a persist queued under an old session must revalidate its
    // write generation on the main actor BEFORE disk I/O. Closing the vault
    // immediately after dispatch — before the queued task has run — must
    // leave the file untouched by that stale write.
    let (state, vault) = try openVault(named: "stale-write", files: ["a.md"])
    try publish(state, [])
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortModifiedDesc)
    let staleTask = state.sidebarOrganizationPersistTaskForTesting
    state.closeVault()
    await staleTask?.value

    let sidebarURL = vault.appendingPathComponent(".slate/sidebar.json")
    if FileManager.default.fileExists(atPath: sidebarURL.path) {
      let json = try sidebarJSON(at: vault)
      XCTAssertNil(
        json["sort"],
        "a stale queued write must not land after vault teardown")
    }
    // And the reset state is defaults, not the rolled-back old vault's data.
    XCTAssertEqual(state.sidebarOrganization, AppState.SidebarOrganizationState())
  }

  func testFailedPersistPublishesTheNewlyDetectedReadOnlyNotice() async throws {
    // Finding 3: when a write fails because the file BECAME unsafe after
    // vault open (synced-in corruption), the recovery must publish the
    // notice so the banner appears and the command set disables.
    let announcer = RecordingAnnouncer()
    let (state, vault) = try openVault(
      named: "became-unsafe", files: ["a.md"], announcer: announcer)
    XCTAssertNil(state.sidebarVaultPrefsNotice)

    // Corrupt the file out-of-band, as a sync client would.
    let slate = vault.appendingPathComponent(".slate", isDirectory: true)
    try FileManager.default.createDirectory(at: slate, withIntermediateDirectories: true)
    try "{not json".write(
      to: slate.appendingPathComponent("sidebar.json"), atomically: true,
      encoding: .utf8)

    try publish(state, [])
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortModifiedDesc)
    await awaitPersist(state)

    XCTAssertEqual(state.sidebarVaultPrefsNotice, .malformed)
    XCTAssertEqual(state.sidebarOrganization, AppState.SidebarOrganizationState())
    XCTAssertTrue(
      announcer.messages.contains {
        $0.hasPrefix("Sidebar organization could not be saved.")
      })
    let evaluation = state.sidebarActionProjection(surface: .menuBar)
      .first { $0.id == SlateCommandID.sidebarSortNameAsc }
    XCTAssertNotNil(evaluation?.disabledReason)
    XCTAssertThrowsError(
      try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortNameAsc))
  }

  func testGroupedContainerOffersOnlyTheNewestFirstDateSorts() async throws {
    // Finding 2: while a container groups by date, name and oldest-first
    // options are inert (normalization would hide them) — they disable with
    // one deterministic reason and dispatch rejects them; the two
    // newest-first date sorts stay live and switch the grouped field.
    let (state, _) = try openVault(named: "grouped-radio", files: ["a.md"])
    try publish(state, [])
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarToggleDateGrouping)

    let inert = "Date grouping sorts newest first. Turn off Group by Date to use this order."
    let projection = state.sidebarActionProjection(surface: .menuBar)
    for id in [
      SlateCommandID.sidebarSortNameAsc, SlateCommandID.sidebarSortNameDesc,
      SlateCommandID.sidebarSortCreatedAsc, SlateCommandID.sidebarSortModifiedAsc,
    ] {
      XCTAssertEqual(
        projection.first { $0.id == id }?.disabledReason, inert, id)
    }
    for id in [
      SlateCommandID.sidebarSortCreatedDesc, SlateCommandID.sidebarSortModifiedDesc,
    ] {
      XCTAssertNil(projection.first { $0.id == id }?.disabledReason, id)
    }
    XCTAssertThrowsError(
      try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortNameAsc))

    // Switching the grouped date field works without touching grouping.
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortCreatedDesc)
    XCTAssertEqual(
      state.sidebarOrganization.prefs.vaultChoice,
      SidebarOrganizationChoice(
        sort: SidebarSortOption(field: .created, direction: .desc),
        grouping: .dateBuckets))

    // Turning grouping off restores the full radio set.
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarToggleDateGrouping)
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortNameAsc)
    XCTAssertEqual(
      state.sidebarOrganization.prefs.vaultChoice.sort,
      SidebarSortOption(field: .name, direction: .asc))
    await awaitPersist(state)
  }

  func testMenuTargetChoiceFollowsTheSelectedContainersOverride() async throws {
    // Finding 4: the AX summary and radio state derive from the SELECTED
    // container's effective (normalized) choice, not the vault default.
    let (state, _) = try openVault(
      named: "summary-target", files: ["Projects/a.md"], folders: ["Projects"])
    try publish(
      state, [item("Projects", directory: true)],
      focusedPath: "Projects", creationParent: "Projects")
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortModifiedDesc)

    XCTAssertEqual(
      state.sidebarOrganizationMenuTargetChoice,
      SidebarOrganizationChoice(
        sort: SidebarSortOption(field: .modified, direction: .desc),
        grouping: .none))
    XCTAssertEqual(
      FileTreeSidebar.treeAccessibilitySummary(
        for: state.sidebarOrganizationMenuTargetChoice),
      "Files. Sorted by modified, newest first.")

    // Deselecting (vault container) reports the untouched vault default.
    try publish(state, [])
    XCTAssertEqual(state.sidebarOrganizationMenuTargetChoice, .defaults)
    XCTAssertNil(
      FileTreeSidebar.treeAccessibilitySummary(
        for: state.sidebarOrganizationMenuTargetChoice))
    await awaitPersist(state)
  }

  // MARK: - Lazy stale prune

  func testStalePruneRewritesAtMostOncePerFolderPerSession() async throws {
    let (state, vault) = try openVault(
      named: "prune", files: ["Projects/real.md"], folders: ["Projects"],
      sidebarJSON: """
        {"version": 1,
         "pins": {"Projects": ["Projects/ghost.md", "Projects/real.md"]}}
        """)
    XCTAssertEqual(
      state.sidebarOrganization.pins.paths(forFolder: "Projects"),
      ["Projects/ghost.md", "Projects/real.md"])

    state.pruneStaleSidebarPins(
      forFolder: "Projects", stale: ["Projects/ghost.md"])
    XCTAssertEqual(
      state.sidebarOrganization.pins.paths(forFolder: "Projects"),
      ["Projects/real.md"])
    await awaitPersist(state)
    let json = try sidebarJSON(at: vault)
    let pins = try XCTUnwrap(json["pins"] as? [String: Any])
    XCTAssertEqual(pins["Projects"] as? [String], ["Projects/real.md"])

    // The ledger blocks a second rewrite for the same folder this session,
    // even if a stale report somehow repeats.
    state.pruneStaleSidebarPins(
      forFolder: "Projects", stale: ["Projects/real.md"])
    XCTAssertEqual(
      state.sidebarOrganization.pins.paths(forFolder: "Projects"),
      ["Projects/real.md"])
  }
}
