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

  // MARK: - Red-team regressions (adversarial review round 2)

  func testStructuralReplayPreservesAConcurrentWritersPins() async throws {
    // Round-2 finding 1: the disk replay applies the exact mutation against
    // the decoded on-disk state. Another writer's pins — added after this
    // AppState loaded — must survive an unrelated structural replay here.
    let (state, vault) = try openVault(
      named: "interleaved", files: ["Projects/note.md"], folders: ["Projects"])
    try publish(
      state, [item("Projects/note.md")],
      focusedPath: "Projects/note.md", creationParent: "Projects")
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarPinNote)
    await awaitPersist(state)

    // A second window/process pins a note in another folder.
    let otherWriter = SidebarVaultPrefsStore(vaultRoot: vault)
    try otherWriter.update { root in
      SidebarOrganizationSchema.setPins(
        &root, folder: "Elsewhere", paths: ["Elsewhere/x.md"])
    }

    state.applySidebarPinsMutation(
      .rename(oldPath: "Projects/note.md", newPath: "Projects/renamed.md"))
    await awaitPersist(state)

    let json = try sidebarJSON(at: vault)
    let pins = try XCTUnwrap(json["pins"] as? [String: Any])
    XCTAssertEqual(
      pins["Elsewhere"] as? [String], ["Elsewhere/x.md"],
      "an unrelated concurrent pin must never be clobbered by a stale snapshot")
    XCTAssertEqual(pins["Projects"] as? [String], ["Projects/renamed.md"])
  }

  func testUseVaultDefaultSortPreservesAGroupingOverride() async throws {
    // Round-2 finding 2: the command's label promises sort only.
    let (state, vault) = try openVault(
      named: "sort-only-clear", files: ["Projects/a.md"], folders: ["Projects"])
    try publish(
      state, [item("Projects", directory: true)],
      focusedPath: "Projects", creationParent: "Projects")
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarToggleDateGrouping)

    // A grouping-only override leaves nothing for the command to clear.
    let groupingOnly = state.sidebarActionProjection(surface: .menuBar)
      .first { $0.id == SlateCommandID.sidebarUseVaultDefaultSort }
    XCTAssertEqual(
      groupingOnly?.disabledReason, "This folder uses the vault default.")

    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortCreatedDesc)
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarUseVaultDefaultSort)

    let override = state.sidebarOrganization.prefs.folderOverrides["Projects"]
    XCTAssertNil(override?.sort)
    XCTAssertEqual(
      override?.grouping, .dateBuckets,
      "clearing the sort override must not erase the grouping override")

    await awaitPersist(state)
    let json = try sidebarJSON(at: vault)
    let overrides = try XCTUnwrap(json["folderOverrides"] as? [String: Any])
    let projects = try XCTUnwrap(overrides["Projects"] as? [String: Any])
    XCTAssertNil(projects["sort"])
    XCTAssertEqual(projects["grouping"] as? String, "dateBuckets")
  }

  func testFolderRenameRetargetsOverridesWithUnknownKeysAndDeleteDropsThem() async throws {
    // Round-2 finding 3: path-keyed overrides follow their folder through
    // renames (carrying unknown inner keys) and drop on deletion.
    let (state, vault) = try openVault(
      named: "override-replay", files: ["Projects/a.md"], folders: ["Projects"],
      sidebarJSON: """
        {"version": 1,
         "folderOverrides": {
           "Projects": {
             "sort": {"field": "modified", "direction": "desc"},
             "future-inner": "keep"
           },
           "Projects/sub": {"grouping": "dateBuckets"}
         }}
        """)
    XCTAssertEqual(
      state.sidebarOrganization.prefs.folderOverrides["Projects"]?.sort?.field,
      .modified)

    state.applySidebarPinsMutation(
      .rename(oldPath: "Projects", newPath: "Archive"))
    XCTAssertEqual(
      state.sidebarOrganization.prefs.folderOverrides["Archive"]?.sort?.field,
      .modified)
    XCTAssertEqual(
      state.sidebarOrganization.prefs.folderOverrides["Archive/sub"]?.grouping,
      .dateBuckets)
    XCTAssertNil(state.sidebarOrganization.prefs.folderOverrides["Projects"])

    await awaitPersist(state)
    var json = try sidebarJSON(at: vault)
    var overrides = try XCTUnwrap(json["folderOverrides"] as? [String: Any])
    let archive = try XCTUnwrap(overrides["Archive"] as? [String: Any])
    XCTAssertEqual(
      archive["future-inner"] as? String, "keep",
      "unknown inner keys ride along with the rekeyed entry")
    XCTAssertNotNil(overrides["Archive/sub"])
    XCTAssertNil(overrides["Projects"])

    state.applySidebarPinsMutation(
      .delete(path: "Archive", parent: "", wasDirectory: true))
    XCTAssertNil(state.sidebarOrganization.prefs.folderOverrides["Archive"])
    XCTAssertNil(state.sidebarOrganization.prefs.folderOverrides["Archive/sub"])
    await awaitPersist(state)
    json = try sidebarJSON(at: vault)
    overrides = (json["folderOverrides"] as? [String: Any]) ?? [:]
    XCTAssertNil(overrides["Archive"])
    XCTAssertNil(overrides["Archive/sub"])
  }

  // MARK: - Red-team regressions (adversarial review round 3)

  func testStructuralReplayCarriesDiskOnlyOverridesAndPins() async throws {
    // Round-3 finding 1: an override made only of unknown keys is invisible
    // to the decoded in-memory state, but a folder rename must still rekey
    // its raw entry on disk; likewise a pin another writer added after load.
    let (state, vault) = try openVault(
      named: "disk-only-replay", files: ["Projects/a.md"], folders: ["Projects"],
      sidebarJSON: """
        {"version": 1,
         "folderOverrides": {"Projects": {"future-only": true}}}
        """)
    XCTAssertTrue(state.sidebarOrganization.prefs.folderOverrides.isEmpty)

    // A concurrent writer pins a path under the folder after this AppState
    // loaded; the rename replay must retarget it too.
    let otherWriter = SidebarVaultPrefsStore(vaultRoot: vault)
    try otherWriter.update { root in
      SidebarOrganizationSchema.setPins(
        &root, folder: "Projects", paths: ["Projects/a.md"])
    }

    state.applySidebarPinsMutation(
      .rename(oldPath: "Projects", newPath: "Archive"))
    await awaitPersist(state)

    let json = try sidebarJSON(at: vault)
    let overrides = try XCTUnwrap(json["folderOverrides"] as? [String: Any])
    XCTAssertEqual(
      (overrides["Archive"] as? [String: Any])?["future-only"] as? Bool, true,
      "an unknown-only override entry follows its folder through a rename")
    XCTAssertNil(overrides["Projects"])
    let pins = try XCTUnwrap(json["pins"] as? [String: Any])
    XCTAssertEqual(pins["Archive"] as? [String], ["Archive/a.md"])
    XCTAssertNil(pins["Projects"])
  }

  func testContextRowReasonsNeverInheritTheSelectedRowsPinState() async throws {
    // Round-3 finding 2: right-clicking an unpinned row while a pinned row
    // is the published selection must offer Pin (not hide both directions),
    // and vice versa.
    let (state, _) = try openVault(
      named: "row-reasons",
      files: ["Projects/pinned.md", "Projects/plain.md"], folders: ["Projects"])
    try publish(
      state, [item("Projects/pinned.md")],
      focusedPath: "Projects/pinned.md", creationParent: "Projects")
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarPinNote)

    // The published-selection reasons say "already pinned" for Pin. A
    // different row's reasons must fully replace the organization set.
    let publishedReasons = state.sidebarActionDisabledReasons
    XCTAssertEqual(
      publishedReasons[SlateCommandID.sidebarPinNote], "This note is already pinned.")

    let rowReasons = state.sidebarOrganizationActionReasons(
      target: item("Projects/plain.md"))
    XCTAssertNil(rowReasons[SlateCommandID.sidebarPinNote])
    XCTAssertEqual(
      rowReasons[SlateCommandID.sidebarUnpinNote], "This note isn't pinned.")
    await awaitPersist(state)
  }

  func testMidChainPersistFailureConvergesToDiskTruthAfterTheTail() async throws {
    // Round-3 finding 3: with writes A (fails) and B (succeeds) queued, the
    // failed mid-chain write must not roll back B's optimistic state; the
    // tail converges published state on authoritative disk truth and
    // surfaces A's failure exactly once.
    let announcer = RecordingAnnouncer()
    let (state, vault) = try openVault(
      named: "mid-chain", files: ["a.md"], announcer: announcer)
    try publish(state, [])

    // A: an apply whose payload JSON cannot encode — the write itself fails
    // while the file stays untouched.
    state.enqueueSidebarOrganizationWriteForTesting { root in
      root["boom"] = NSObject()
    }
    // B: a real command, queued behind A.
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortModifiedDesc)
    await awaitPersist(state)

    XCTAssertEqual(
      state.sidebarOrganization.prefs.vaultChoice.sort,
      SidebarSortOption(field: .modified, direction: .desc),
      "the successful later write must survive the earlier failure")
    let json = try sidebarJSON(at: vault)
    XCTAssertEqual(
      json["sort"] as? [String: String],
      ["field": "modified", "direction": "desc"])
    XCTAssertEqual(
      announcer.messages.filter {
        $0.hasPrefix("Sidebar organization could not be saved.")
      }.count,
      1,
      "the mid-chain failure surfaces exactly once, from the tail")
  }

  func testSameFolderTwoWriterPinsBothSurvive() async throws {
    // Round-3 finding 4: pinning replays the exact op against decoded disk
    // state, so a second writer's pin in the SAME folder survives.
    let (state, vault) = try openVault(
      named: "two-writer-pin",
      files: ["Projects/mine.md", "Projects/theirs.md"], folders: ["Projects"])

    let otherWriter = SidebarVaultPrefsStore(vaultRoot: vault)
    try otherWriter.update { root in
      SidebarOrganizationSchema.setPins(
        &root, folder: "Projects", paths: ["Projects/theirs.md"])
    }

    try publish(
      state, [item("Projects/mine.md")],
      focusedPath: "Projects/mine.md", creationParent: "Projects")
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarPinNote)
    await awaitPersist(state)

    let json = try sidebarJSON(at: vault)
    let pins = try XCTUnwrap(json["pins"] as? [String: Any])
    XCTAssertEqual(
      Set(try XCTUnwrap(pins["Projects"] as? [String])),
      ["Projects/theirs.md", "Projects/mine.md"],
      "both writers' pins survive the locked exact-op replay")
  }

  func testGroupingToggleLeavesAConcurrentSortWriteIntact() async throws {
    // Round-3 finding 5: the grouping toggle writes only the grouping key —
    // a sort another writer changed after load must not be restored.
    let (state, vault) = try openVault(named: "axis-isolation", files: ["a.md"])
    try publish(state, [])
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortModifiedDesc)
    await awaitPersist(state)

    let otherWriter = SidebarVaultPrefsStore(vaultRoot: vault)
    try otherWriter.update { root in
      SidebarOrganizationSchema.setVaultSort(
        &root, SidebarSortOption(field: .created, direction: .desc))
    }

    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarToggleDateGrouping)
    await awaitPersist(state)

    let json = try sidebarJSON(at: vault)
    XCTAssertEqual(
      json["sort"] as? [String: String],
      ["field": "created", "direction": "desc"],
      "a concurrent sort write survives the grouping toggle")
    XCTAssertEqual(json["grouping"] as? String, "dateBuckets")
  }

  // MARK: - Red-team regressions (adversarial review round 4)

  func testSuccessfulWritePublishesMergedDiskTruthToLiveState() async throws {
    // Round-4 finding 1: when the locked update merges another writer's
    // change, the tail publishes decoded post-write disk truth — the live
    // tree and AX state see the merge, not just this window's optimistic
    // reflect.
    let (state, _) = try openVault(named: "merge-publish", files: ["a.md"])
    try publish(state, [])
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortModifiedDesc)
    await awaitPersist(state)

    let otherWriter = SidebarVaultPrefsStore(
      vaultRoot: try XCTUnwrap(state.currentVaultURL))
    try otherWriter.update { root in
      SidebarOrganizationSchema.setVaultSort(
        &root, SidebarSortOption(field: .created, direction: .desc))
    }

    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarToggleDateGrouping)
    await awaitPersist(state)

    XCTAssertEqual(
      state.sidebarOrganization.prefs.vaultChoice,
      SidebarOrganizationChoice(
        sort: SidebarSortOption(field: .created, direction: .desc),
        grouping: .dateBuckets),
      "published state must adopt the merged on-disk sort, not restore the optimistic one")

    // Same for pins: a second writer's same-folder pin becomes visible.
    let (pinState, pinVault) = try openVault(
      named: "merge-publish-pins",
      files: ["Projects/mine.md", "Projects/theirs.md"], folders: ["Projects"])
    let pinWriter = SidebarVaultPrefsStore(vaultRoot: pinVault)
    try pinWriter.update { root in
      SidebarOrganizationSchema.setPins(
        &root, folder: "Projects", paths: ["Projects/theirs.md"])
    }
    try publish(
      pinState, [item("Projects/mine.md")],
      focusedPath: "Projects/mine.md", creationParent: "Projects")
    _ = try pinState.dispatchSidebarAction(id: SlateCommandID.sidebarPinNote)
    await awaitPersist(pinState)
    XCTAssertEqual(
      Set(pinState.sidebarOrganization.pins.paths(forFolder: "Projects")),
      ["Projects/theirs.md", "Projects/mine.md"],
      "live pin state includes the merged concurrent pin")
  }

  func testStructuralTransformsDuringReadOnlyReplayOnRetry() async throws {
    // Round-4 finding 2: a rename made while sidebar.json is read-only must
    // not orphan the file's pins/overrides — Retry replays the journaled
    // transform under the lock before republishing.
    let announcer = RecordingAnnouncer()
    let (state, vault) = try openVault(
      named: "journal-replay",
      files: ["Projects/note.md"], folders: ["Projects"],
      sidebarJSON: """
        {"version": 1,
         "pins": {"Projects": ["Projects/note.md"]},
         "folderOverrides": {"Projects": {"grouping": "dateBuckets"}}}
        """,
      announcer: announcer)
    XCTAssertTrue(
      state.sidebarOrganization.pins.isPinned(
        "Projects/note.md", inFolder: "Projects"))

    // The file becomes unsafe (synced-in corruption); a failed write
    // publishes the notice.
    let slate = vault.appendingPathComponent(".slate", isDirectory: true)
    let sidebarURL = slate.appendingPathComponent("sidebar.json")
    let originalContent = try Data(contentsOf: sidebarURL)
    try "{not json".write(to: sidebarURL, atomically: true, encoding: .utf8)
    try publish(state, [])
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortModifiedDesc)
    await awaitPersist(state)
    XCTAssertEqual(state.sidebarVaultPrefsNotice, .malformed)

    // A structural rename happens during the outage: journaled, not lost.
    state.applySidebarPinsMutation(
      .rename(oldPath: "Projects", newPath: "Archive"))
    XCTAssertEqual(state.sidebarStructuralTransformJournal.count, 1)

    // The user repairs the file (restoring the pre-outage content) and
    // retries: the journal replays before the republish.
    try originalContent.write(to: sidebarURL)
    await state.retrySidebarVaultPreferences()?.value

    XCTAssertNil(state.sidebarVaultPrefsNotice)
    XCTAssertTrue(state.sidebarStructuralTransformJournal.isEmpty)
    XCTAssertTrue(
      state.sidebarOrganization.pins.isPinned(
        "Archive/note.md", inFolder: "Archive"),
      "the outage rename retargets the repaired file's pins")
    XCTAssertEqual(
      state.sidebarOrganization.prefs.folderOverrides["Archive"]?.grouping,
      .dateBuckets)
    XCTAssertNil(state.sidebarOrganization.prefs.folderOverrides["Projects"])

    let json = try sidebarJSON(at: vault)
    let pins = try XCTUnwrap(json["pins"] as? [String: Any])
    XCTAssertEqual(pins["Archive"] as? [String], ["Archive/note.md"])
    XCTAssertNil(pins["Projects"])
    XCTAssertTrue(announcer.messages.contains("Sidebar settings reloaded."))
  }

  // MARK: - Red-team regressions (adversarial review round 5)

  func testRetryReplayPreservesTransformsAppendedMidPass() async throws {
    // Round-5 finding 1: an append that lands while a replay pass is in
    // flight must survive the pass's acknowledgement and replay in a
    // follow-up pass before the notice clears.
    let (state, vault) = try openVault(
      named: "journal-suffix",
      files: ["Projects/note.md", "Other/keep.md"],
      folders: ["Projects", "Other"],
      sidebarJSON: """
        {"version": 1,
         "pins": {
           "Projects": ["Projects/note.md"],
           "Other": ["Other/keep.md"]}}
        """)
    let slate = vault.appendingPathComponent(".slate", isDirectory: true)
    let sidebarURL = slate.appendingPathComponent("sidebar.json")
    let originalContent = try Data(contentsOf: sidebarURL)
    try "{not json".write(to: sidebarURL, atomically: true, encoding: .utf8)
    try publish(state, [])
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortModifiedDesc)
    await awaitPersist(state)
    XCTAssertEqual(state.sidebarVaultPrefsNotice, .malformed)

    // First outage transform, journaled before Retry.
    state.applySidebarPinsMutation(
      .rename(oldPath: "Projects", newPath: "Archive"))
    XCTAssertEqual(state.sidebarStructuralTransformJournal.count, 1)
    try originalContent.write(to: sidebarURL)

    // Second transform lands mid-pass, via the deterministic interleave
    // seam (the notice is still active at that instant).
    state.sidebarStructuralJournalReplayInterleaveHookForTesting = {
      state.applySidebarPinsMutation(
        .rename(oldPath: "Other/keep.md", newPath: "Other/kept.md"))
    }
    await state.retrySidebarVaultPreferences()?.value

    XCTAssertNil(state.sidebarVaultPrefsNotice)
    XCTAssertTrue(state.sidebarStructuralTransformJournal.isEmpty)
    XCTAssertTrue(
      state.sidebarOrganization.pins.isPinned(
        "Archive/note.md", inFolder: "Archive"))
    XCTAssertTrue(
      state.sidebarOrganization.pins.isPinned("Other/kept.md", inFolder: "Other"),
      "the mid-pass append replays in a follow-up pass, not silently cleared")
    let json = try sidebarJSON(at: vault)
    let pins = try XCTUnwrap(json["pins"] as? [String: Any])
    XCTAssertEqual(pins["Archive"] as? [String], ["Archive/note.md"])
    XCTAssertEqual(pins["Other"] as? [String], ["Other/kept.md"])
  }

  func testInvalidKnownSectionShapeEntersReadOnlyRecovery() throws {
    // Round-5 finding 3: `pins` as an array is parseable JSON but not this
    // build's schema — it must enter recovery, not be silently replaced by
    // the next write.
    let (state, _) = try openVault(
      named: "shape-recovery", files: ["a.md"],
      sidebarJSON: """
        {"version": 1, "pins": ["future-data"]}
        """)
    XCTAssertEqual(state.sidebarVaultPrefsNotice, .malformed)
    XCTAssertEqual(state.sidebarOrganization, AppState.SidebarOrganizationState())
    try publish(state, [])
    XCTAssertThrowsError(
      try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortNameAsc))
  }

  // MARK: - Red-team regressions (adversarial review round 6)

  func testFirstStructuralTransformAfterSilentCorruptionSurvivesForRetry() async throws {
    // Round-6 finding 1: when a rename is the FIRST operation after the
    // file silently became malformed (no prior failed write, so no notice
    // yet), the failed replay must retain the journaled transform for
    // Retry instead of losing it with the rollback.
    let (state, vault) = try openVault(
      named: "silent-corruption",
      files: ["Projects/note.md"], folders: ["Projects"],
      sidebarJSON: """
        {"version": 1, "pins": {"Projects": ["Projects/note.md"]}}
        """)
    XCTAssertNil(state.sidebarVaultPrefsNotice)

    let sidebarURL = vault.appendingPathComponent(".slate/sidebar.json")
    let originalContent = try Data(contentsOf: sidebarURL)
    try "{not json".write(to: sidebarURL, atomically: true, encoding: .utf8)

    // Notice is still nil here — the failure below is what detects it.
    state.applySidebarPinsMutation(
      .rename(oldPath: "Projects", newPath: "Archive"))
    await awaitPersist(state)
    XCTAssertEqual(state.sidebarVaultPrefsNotice, .malformed)
    XCTAssertEqual(
      state.sidebarStructuralTransformJournal.count, 1,
      "the un-committed transform stays journaled for Retry")

    try originalContent.write(to: sidebarURL)
    await state.retrySidebarVaultPreferences()?.value
    XCTAssertNil(state.sidebarVaultPrefsNotice)
    XCTAssertTrue(state.sidebarStructuralTransformJournal.isEmpty)
    let json = try sidebarJSON(at: vault)
    let pins = try XCTUnwrap(json["pins"] as? [String: Any])
    XCTAssertEqual(pins["Archive"] as? [String], ["Archive/note.md"])
    XCTAssertNil(pins["Projects"])
  }

  func testLockedWriteRefusesAConcurrentlyReshapedFile() async throws {
    // Round-6 finding 2: a cooperating writer replaces `pins` with an array
    // AFTER open-time validation; the next locked write must fail and enter
    // recovery instead of clobbering that data.
    let (state, vault) = try openVault(named: "reshaped", files: ["a.md"])
    try publish(state, [])

    let sidebarURL = vault.appendingPathComponent(".slate/sidebar.json")
    let slate = vault.appendingPathComponent(".slate", isDirectory: true)
    try FileManager.default.createDirectory(
      at: slate, withIntermediateDirectories: true)
    try """
      {"version": 1, "pins": ["future-data"]}
      """.write(to: sidebarURL, atomically: true, encoding: .utf8)

    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortModifiedDesc)
    await awaitPersist(state)

    XCTAssertEqual(state.sidebarVaultPrefsNotice, .malformed)
    let json = try sidebarJSON(at: vault)
    XCTAssertEqual(
      json["pins"] as? [String], ["future-data"],
      "the unrecognized section survives untouched")
    XCTAssertNil(json["sort"], "the refused write left no partial data")
  }

  func testGroupingToggleReturnsToVaultInheritance() async throws {
    // Round-6 finding 3: toggling a folder back to the vault's current
    // grouping clears the override (inheritance restored) instead of
    // pinning an explicit copy.
    let (state, vault) = try openVault(
      named: "grouping-inherit", files: ["Projects/a.md"], folders: ["Projects"])
    try publish(
      state, [item("Projects", directory: true)],
      focusedPath: "Projects", creationParent: "Projects")

    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarToggleDateGrouping)
    XCTAssertEqual(
      state.sidebarOrganization.prefs.folderOverrides["Projects"]?.grouping,
      .dateBuckets)
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarToggleDateGrouping)
    XCTAssertNil(
      state.sidebarOrganization.prefs.folderOverrides["Projects"],
      "matching the vault default removes the override entirely")
    await awaitPersist(state)
    let cleared = try sidebarJSON(at: vault)
    XCTAssertNil((cleared["folderOverrides"] as? [String: Any])?["Projects"])

    // The folder now follows a later vault-wide change.
    try publish(state, [])
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarToggleDateGrouping)
    XCTAssertEqual(
      state.sidebarOrganization.prefs
        .effectiveChoice(forFolder: "Projects").grouping,
      .dateBuckets)

    // Asymmetry: turning the folder OFF while the vault is ON is a real
    // difference and stays explicit.
    try publish(
      state, [item("Projects", directory: true)],
      focusedPath: "Projects", creationParent: "Projects")
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarToggleDateGrouping)
    XCTAssertEqual(
      state.sidebarOrganization.prefs.folderOverrides["Projects"]?.grouping,
      SidebarGroupingOption.none)
    await awaitPersist(state)
  }

  // MARK: - Red-team regressions (adversarial review round 7)

  func testNestedInvalidPinShapesEnterRecoveryAtOpen() throws {
    // Round-7 finding 1: a pin list that is not purely strings would be
    // truncated by decoding and destroyed by the next exact-op rewrite —
    // it must enter recovery instead.
    let (state, _) = try openVault(
      named: "nested-shape", files: ["a.md"],
      sidebarJSON: """
        {"version": 1,
         "pins": {"Projects": ["Projects/kept.md", {"future": true}]}}
        """)
    XCTAssertEqual(state.sidebarVaultPrefsNotice, .malformed)
    XCTAssertEqual(state.sidebarOrganization, AppState.SidebarOrganizationState())
  }

  func testConcurrentGroupingWriteRejectsAStaleInertSortUnderTheLock() async throws {
    // Round-7 finding 2: another writer enables grouping on disk after this
    // process's pre-dispatch guard read stale state. The locked recheck
    // refuses the inert Name sort, announces the rule, and converges
    // published state on the merged disk truth.
    let announcer = RecordingAnnouncer()
    let (state, vault) = try openVault(
      named: "grouped-race", files: ["a.md"], announcer: announcer)
    try publish(state, [])

    let otherWriter = SidebarVaultPrefsStore(vaultRoot: vault)
    try otherWriter.update { root in
      SidebarOrganizationSchema.setVaultGrouping(&root, .dateBuckets)
    }

    // Pre-dispatch guard sees stale grouping == none and admits the intent.
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortNameAsc)
    await awaitPersist(state)

    let json = try sidebarJSON(at: vault)
    XCTAssertNil(json["sort"], "the inert sort must not be written")
    XCTAssertEqual(json["grouping"] as? String, "dateBuckets")
    XCTAssertEqual(
      state.sidebarOrganization.prefs.vaultChoice.grouping, .dateBuckets,
      "published state converges on the merged disk truth")
    XCTAssertEqual(state.sidebarOrganization.prefs.vaultChoice.sort, .defaults)
    XCTAssertTrue(
      announcer.messages.contains(AppState.sidebarGroupedSortInertReason))
    XCTAssertFalse(
      announcer.messages.contains {
        $0.hasPrefix("Sidebar organization could not be saved.")
      },
      "a semantic refusal is not reported as a save failure")
  }

  func testVaultGroupingChangeBeforeFolderToggleKeepsTheUsersIntent() async throws {
    // Round-7 finding 3: folder explicitly groups by date; another writer
    // flips the VAULT to dateBuckets; the user toggles the folder OFF. The
    // under-lock decision must write an explicit `none` override (a real
    // difference from the new vault value), not clear to inheritance and
    // silently leave grouping ON.
    let (state, vault) = try openVault(
      named: "toggle-race", files: ["Projects/a.md"], folders: ["Projects"])
    try publish(
      state, [item("Projects", directory: true)],
      focusedPath: "Projects", creationParent: "Projects")
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarToggleDateGrouping)
    await awaitPersist(state)

    let otherWriter = SidebarVaultPrefsStore(vaultRoot: vault)
    try otherWriter.update { root in
      SidebarOrganizationSchema.setVaultGrouping(&root, .dateBuckets)
    }

    // Stale in-memory vault grouping is .none, so the naive decision would
    // be "clear to inheritance" — which would leave the folder grouped.
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarToggleDateGrouping)
    await awaitPersist(state)

    let json = try sidebarJSON(at: vault)
    let overrides = try XCTUnwrap(json["folderOverrides"] as? [String: Any])
    let projects = try XCTUnwrap(overrides["Projects"] as? [String: Any])
    XCTAssertEqual(
      projects["grouping"] as? String, "none",
      "the user's OFF intent persists as an explicit override against the new vault value")
    XCTAssertEqual(
      state.sidebarOrganization.prefs
        .effectiveChoice(forFolder: "Projects").grouping,
      SidebarGroupingOption.none)
  }

  // MARK: - Red-team regressions (adversarial review round 8)

  func testReadableWriteFailureDrainsWithTheNextPersistOfAnyKind() async throws {
    // Round-8 finding 1: a structural replay that fails while the file
    // stays READABLE (lock unavailable) publishes no notice, so the
    // notice-gated Retry never runs — the journaled transform must commit
    // with the next persist of any kind.
    let (state, vault) = try openVault(
      named: "readable-failure",
      files: ["Projects/note.md"], folders: ["Projects"],
      sidebarJSON: """
        {"version": 1, "pins": {"Projects": ["Projects/note.md"]}}
        """)

    // A DIRECTORY at the lock path makes every locked write fail while
    // read() stays clean.
    let lockURL = vault.appendingPathComponent(".slate/sidebar.json.lock")
    try FileManager.default.createDirectory(
      at: lockURL, withIntermediateDirectories: true)

    state.applySidebarPinsMutation(
      .rename(oldPath: "Projects", newPath: "Archive"))
    await awaitPersist(state)
    XCTAssertNil(state.sidebarVaultPrefsNotice, "the file is still readable")
    XCTAssertEqual(
      state.sidebarStructuralTransformJournal.count, 1,
      "the transform survives the readable failure")

    // Unblock the lock; ANY later persist drains the backlog first.
    try FileManager.default.removeItem(at: lockURL)
    try publish(state, [])
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortModifiedDesc)
    await awaitPersist(state)

    XCTAssertTrue(state.sidebarStructuralTransformJournal.isEmpty)
    let json = try sidebarJSON(at: vault)
    let pins = try XCTUnwrap(json["pins"] as? [String: Any])
    XCTAssertEqual(pins["Archive"] as? [String], ["Archive/note.md"])
    XCTAssertNil(pins["Projects"])
    XCTAssertEqual(
      json["sort"] as? [String: String],
      ["field": "modified", "direction": "desc"])
    XCTAssertTrue(
      state.sidebarOrganization.pins.isPinned(
        "Archive/note.md", inFolder: "Archive"))
  }

  func testRetryAgainstAnUnchangedShapeInvalidFilePublishesDefaults() async throws {
    // Round-8 finding 2: a shape-invalid file carrying an otherwise-valid
    // sort must not be partially salvaged by Retry — a standing notice
    // publishes defaults.
    let (state, _) = try openVault(
      named: "no-salvage", files: ["a.md"],
      sidebarJSON: """
        {"version": 1,
         "sort": {"field": "modified", "direction": "desc"},
         "pins": ["future-data"]}
        """)
    XCTAssertEqual(state.sidebarVaultPrefsNotice, .malformed)
    XCTAssertEqual(state.sidebarOrganization, AppState.SidebarOrganizationState())

    await state.retrySidebarVaultPreferences()?.value
    XCTAssertEqual(state.sidebarVaultPrefsNotice, .malformed)
    XCTAssertEqual(
      state.sidebarOrganization, AppState.SidebarOrganizationState(),
      "Retry must not salvage the valid sort beside the invalid pins section")
  }

  // MARK: - Red-team regressions (adversarial review round 9)

  func testStructuralTransformsReplayAtMostOnceAcrossQueuedPersists() async throws {
    // Round-9 finding 1: two persists enqueued back-to-back both snapshot
    // the same pending transform; the successor must re-filter against the
    // live journal after the predecessor commits and never replay it again.
    let (state, vault) = try openVault(
      named: "at-most-once",
      files: ["Projects/note.md"], folders: ["Projects"],
      sidebarJSON: """
        {"version": 1, "pins": {"Projects": ["Projects/note.md"]}}
        """)
    try publish(state, [])

    // Enqueue the structural persist and a second persist synchronously,
    // before either has run — both capture the same backlog snapshot.
    state.applySidebarPinsMutation(
      .rename(oldPath: "Projects", newPath: "Archive"))
    let transformID = try XCTUnwrap(
      state.sidebarStructuralTransformJournal.first?.id)
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortModifiedDesc)
    await awaitPersist(state)

    XCTAssertEqual(
      state.sidebarStructuralReplayCountsForTesting[transformID], 1,
      "the committed transform replays exactly once across the queued chain")
    XCTAssertTrue(state.sidebarStructuralTransformJournal.isEmpty)
    let json = try sidebarJSON(at: vault)
    let pins = try XCTUnwrap(json["pins"] as? [String: Any])
    XCTAssertEqual(pins["Archive"] as? [String], ["Archive/note.md"])
    XCTAssertEqual(
      json["sort"] as? [String: String],
      ["field": "modified", "direction": "desc"])
  }

  // MARK: - Red-team regressions (adversarial review round 10)

  func testReplacedButUnsyncedCommitAcknowledgesTheTransform() async throws {
    // Round-10 finding 1: the atomic rename landed but the directory fsync
    // failed. The transform's content IS in the file — it must acknowledge
    // (never re-replay onto recreated paths) and only a durability warning
    // is announced. A recreated source folder with fresh pins then survives
    // the next persist untouched.
    let announcer = RecordingAnnouncer()
    final class FailOnceBox: @unchecked Sendable {
      private let lock = NSLock()
      private var failed = false
      func shouldFail() -> Bool {
        lock.lock()
        defer { lock.unlock() }
        if failed { return false }
        failed = true
        return true
      }
    }
    let failOnce = FailOnceBox()

    let vault = root.appendingPathComponent("unsynced-commit")
    try FileManager.default.createDirectory(
      at: vault.appendingPathComponent("Projects"),
      withIntermediateDirectories: true)
    try "# note".write(
      to: vault.appendingPathComponent("Projects/note.md"),
      atomically: true, encoding: .utf8)
    let slate = vault.appendingPathComponent(".slate", isDirectory: true)
    try FileManager.default.createDirectory(at: slate, withIntermediateDirectories: true)
    try """
      {"version": 1, "pins": {"Projects": ["Projects/note.md"]}}
      """.write(
        to: slate.appendingPathComponent("sidebar.json"),
        atomically: true, encoding: .utf8)

    let state = AppState(
      recentsStore: RecentVaultsStore(
        fileURL: root.appendingPathComponent("unsynced-recents.json")),
      externalOpener: { _ in true },
      announcer: announcer)
    state.sidebarVaultPrefsStoreFactoryForTesting = { vaultRoot in
      SidebarVaultPrefsStore(
        vaultRoot: vaultRoot,
        sidebarFileOpener: { directoryFD in
          "sidebar.json".withCString { name in
            openat(
              directoryFD, name,
              O_RDONLY | O_CLOEXEC | O_NONBLOCK | O_NOFOLLOW)
          }
        },
        directorySynchronizer: { fd in
          failOnce.shouldFail() ? -1 : fsync(fd)
        })
    }
    state.openVault(at: vault)
    _ = try XCTUnwrap(state.currentSession).scanInitial(cancel: CancelToken())

    state.applySidebarPinsMutation(
      .rename(oldPath: "Projects", newPath: "Archive"))
    await awaitPersist(state)

    XCTAssertTrue(
      state.sidebarStructuralTransformJournal.isEmpty,
      "a committed-but-unsynced transform acknowledges — it must not stay pending")
    XCTAssertTrue(
      state.sidebarOrganization.pins.isPinned(
        "Archive/note.md", inFolder: "Archive"))
    XCTAssertTrue(
      announcer.messages.contains {
        $0.hasPrefix("Sidebar settings were saved, but")
      })

    // Another process recreates Projects with fresh pins; the next persist
    // must not re-apply the old rename to them.
    let otherWriter = SidebarVaultPrefsStore(vaultRoot: vault)
    try otherWriter.update { root in
      SidebarOrganizationSchema.setPins(
        &root, folder: "Projects", paths: ["Projects/fresh.md"])
    }
    try publish(state, [])
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortModifiedDesc)
    await awaitPersist(state)
    let json = try sidebarJSON(at: vault)
    let pins = try XCTUnwrap(json["pins"] as? [String: Any])
    XCTAssertEqual(
      pins["Projects"] as? [String], ["Projects/fresh.md"],
      "the recreated folder's fresh pins survive — no ghost re-replay")
    XCTAssertEqual(pins["Archive"] as? [String], ["Archive/note.md"])
  }

  func testMergedWriteAnnouncesTheCorrectedEffectiveChoice() async throws {
    // Round-10 finding 2: the optimistic announcement described the stale
    // sort; after the locked update merges another writer's sort, the tail
    // must speak the corrected effective truth once.
    let announcer = RecordingAnnouncer()
    let (state, vault) = try openVault(
      named: "announce-correction", files: ["a.md"], announcer: announcer)
    try publish(state, [])
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortModifiedDesc)
    await awaitPersist(state)

    let otherWriter = SidebarVaultPrefsStore(vaultRoot: vault)
    try otherWriter.update { root in
      SidebarOrganizationSchema.setVaultSort(
        &root, SidebarSortOption(field: .created, direction: .desc))
    }

    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarToggleDateGrouping)
    await awaitPersist(state)

    XCTAssertTrue(
      announcer.messages.contains(
        "Sorted by modified, newest first, grouped by date."),
      "the optimistic announcement fires immediately")
    XCTAssertTrue(
      announcer.messages.contains(
        "Sorted by created, newest first, grouped by date."),
      "the merged effective truth is corrected after the locked update")
  }

  // MARK: - Red-team regressions (adversarial review round 11)

  func testRetryHonorsACommittedButUnsyncedReplayPass() async throws {
    // Round-11 finding 1: a Retry replay pass whose rename landed but whose
    // directory fsync failed is COMMITTED — acknowledge the pass, publish,
    // and announce only durability; never leave it journaled to ghost-replay
    // onto recreated paths.
    let announcer = RecordingAnnouncer()
    final class FailOnceBox: @unchecked Sendable {
      private let lock = NSLock()
      private var armed = false
      func arm() {
        lock.lock()
        armed = true
        lock.unlock()
      }
      func shouldFail() -> Bool {
        lock.lock()
        defer { lock.unlock() }
        if armed {
          armed = false
          return true
        }
        return false
      }
    }
    let failOnce = FailOnceBox()

    let vault = root.appendingPathComponent("retry-unsynced")
    try FileManager.default.createDirectory(
      at: vault.appendingPathComponent("Projects"),
      withIntermediateDirectories: true)
    try "# note".write(
      to: vault.appendingPathComponent("Projects/note.md"),
      atomically: true, encoding: .utf8)
    let slate = vault.appendingPathComponent(".slate", isDirectory: true)
    try FileManager.default.createDirectory(at: slate, withIntermediateDirectories: true)
    let sidebarURL = slate.appendingPathComponent("sidebar.json")
    let validContent = """
      {"version": 1, "pins": {"Projects": ["Projects/note.md"]}}
      """
    try validContent.write(to: sidebarURL, atomically: true, encoding: .utf8)

    let state = AppState(
      recentsStore: RecentVaultsStore(
        fileURL: root.appendingPathComponent("retry-unsynced-recents.json")),
      externalOpener: { _ in true },
      announcer: announcer)
    state.sidebarVaultPrefsStoreFactoryForTesting = { vaultRoot in
      SidebarVaultPrefsStore(
        vaultRoot: vaultRoot,
        sidebarFileOpener: { directoryFD in
          "sidebar.json".withCString { name in
            openat(
              directoryFD, name,
              O_RDONLY | O_CLOEXEC | O_NONBLOCK | O_NOFOLLOW)
          }
        },
        directorySynchronizer: { fd in
          failOnce.shouldFail() ? -1 : fsync(fd)
        })
    }
    state.openVault(at: vault)
    _ = try XCTUnwrap(state.currentSession).scanInitial(cancel: CancelToken())

    // Outage: corrupt, journal a rename, repair.
    try "{not json".write(to: sidebarURL, atomically: true, encoding: .utf8)
    state.applySidebarPinsMutation(
      .rename(oldPath: "Projects", newPath: "Archive"))
    await awaitPersist(state)
    XCTAssertEqual(state.sidebarVaultPrefsNotice, .malformed)
    XCTAssertEqual(state.sidebarStructuralTransformJournal.count, 1)
    try validContent.write(to: sidebarURL, atomically: true, encoding: .utf8)

    // The retry replay pass itself hits the fsync failure.
    failOnce.arm()
    await state.retrySidebarVaultPreferences()?.value

    XCTAssertNil(state.sidebarVaultPrefsNotice)
    XCTAssertTrue(
      state.sidebarStructuralTransformJournal.isEmpty,
      "a committed-but-unsynced pass acknowledges instead of staying journaled")
    XCTAssertTrue(
      state.sidebarOrganization.pins.isPinned(
        "Archive/note.md", inFolder: "Archive"))
    XCTAssertTrue(
      announcer.messages.contains {
        $0.hasPrefix("Sidebar settings were saved, but")
      })

    // A recreated source folder's fresh pins survive the next persist.
    let otherWriter = SidebarVaultPrefsStore(vaultRoot: vault)
    try otherWriter.update { root in
      SidebarOrganizationSchema.setPins(
        &root, folder: "Projects", paths: ["Projects/fresh.md"])
    }
    try publish(state, [])
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortModifiedDesc)
    await awaitPersist(state)
    let json = try sidebarJSON(at: vault)
    let pins = try XCTUnwrap(json["pins"] as? [String: Any])
    XCTAssertEqual(pins["Projects"] as? [String], ["Projects/fresh.md"])
    XCTAssertEqual(pins["Archive"] as? [String], ["Archive/note.md"])
  }

  func testNonTailMergedWriteStillGetsItsCorrectionAtTheTail() async throws {
    // Round-11 finding 2: the grouping write is NOT the chain tail (a pin
    // follows immediately), yet its announcement-verification record must
    // survive to the tail, which corrects it against final disk truth.
    let announcer = RecordingAnnouncer()
    let (state, vault) = try openVault(
      named: "non-tail-correction", files: ["Projects/note.md"],
      folders: ["Projects"], announcer: announcer)

    let otherWriter = SidebarVaultPrefsStore(vaultRoot: vault)
    try otherWriter.update { root in
      SidebarOrganizationSchema.setVaultSort(
        &root, SidebarSortOption(field: .created, direction: .desc))
    }

    try publish(state, [])
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarToggleDateGrouping)
    // Queue a second, unrelated persist before the first runs: the toggle
    // commit becomes non-tail.
    try publish(
      state, [item("Projects/note.md")],
      focusedPath: "Projects/note.md", creationParent: "Projects")
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarPinNote)
    await awaitPersist(state)

    XCTAssertTrue(
      announcer.messages.contains(
        "Sorted by created, newest first, grouped by date."),
      "the non-tail merge's correction is carried to and spoken at the tail")
    XCTAssertEqual(
      state.sidebarOrganization.prefs.vaultChoice,
      SidebarOrganizationChoice(
        sort: SidebarSortOption(field: .created, direction: .desc),
        grouping: .dateBuckets))
  }

  func testPruneRetainsAPinWhoseFileReappeared() async throws {
    // Round-11 finding 3: the stale snapshot came from an earlier listing;
    // if the file exists again by prune time (sync ordering), the pin is
    // retained and the folder's once-per-session slot is NOT consumed.
    let (state, vault) = try openVault(
      named: "prune-revalidate", files: ["Projects/real.md"],
      folders: ["Projects"],
      sidebarJSON: """
        {"version": 1,
         "pins": {"Projects": [
           "Projects/ghost.md", "Projects/real.md", "Projects/gone.md"]}}
        """)

    // ghost.md re-appears before the prune runs.
    try "# back".write(
      to: vault.appendingPathComponent("Projects/ghost.md"),
      atomically: true, encoding: .utf8)
    state.pruneStaleSidebarPins(
      forFolder: "Projects", stale: ["Projects/ghost.md"])
    await awaitPersist(state)
    XCTAssertEqual(
      state.sidebarOrganization.pins.paths(forFolder: "Projects"),
      ["Projects/ghost.md", "Projects/real.md", "Projects/gone.md"],
      "a re-appeared file is never pruned")

    // The slot was not consumed (and the first prune has settled): a
    // genuinely missing path still prunes on the next report.
    state.pruneStaleSidebarPins(
      forFolder: "Projects", stale: ["Projects/gone.md"])
    await awaitPersist(state)
    XCTAssertEqual(
      state.sidebarOrganization.pins.paths(forFolder: "Projects"),
      ["Projects/ghost.md", "Projects/real.md"])
    let json = try sidebarJSON(at: vault)
    let pins = try XCTUnwrap(json["pins"] as? [String: Any])
    XCTAssertEqual(
      pins["Projects"] as? [String],
      ["Projects/ghost.md", "Projects/real.md"])
  }

  // MARK: - Red-team regressions (adversarial review round 12)

  func testAdversariallyDuplicatedStaleListsPruneBoundedAndOffMain() async throws {
    // Round-12 finding 1: thousands of duplicated candidates collapse to
    // one bounded, deduplicated locked pass — and decode dedupes the
    // authored list itself.
    let ghost = "Projects/ghost.md"
    let duplicated = Array(repeating: ghost, count: 5_000) + ["Projects/real.md"]
    let pinsJSON = duplicated.map { "\"\($0)\"" }.joined(separator: ",")
    let (state, vault) = try openVault(
      named: "dup-prune", files: ["Projects/real.md"], folders: ["Projects"],
      sidebarJSON: """
        {"version": 1, "pins": {"Projects": [\(pinsJSON)]}}
        """)
    // Decode collapsed the duplicates (first occurrence wins).
    XCTAssertEqual(
      state.sidebarOrganization.pins.paths(forFolder: "Projects"),
      [ghost, "Projects/real.md"])

    state.pruneStaleSidebarPins(
      forFolder: "Projects",
      stale: Array(repeating: ghost, count: 5_000))
    await awaitPersist(state)
    XCTAssertEqual(
      state.sidebarOrganization.pins.paths(forFolder: "Projects"),
      ["Projects/real.md"])
    let json = try sidebarJSON(at: vault)
    let pins = try XCTUnwrap(json["pins"] as? [String: Any])
    XCTAssertEqual(pins["Projects"] as? [String], ["Projects/real.md"])
  }

  func testPruneWriteFailureDoesNotConsumeTheSlot() async throws {
    // Round-12 finding 2: the once-per-session slot is consumed only by a
    // COMMITTED mutation that removed a pin — a failed write retries later.
    let (state, vault) = try openVault(
      named: "prune-write-failure", files: ["Projects/real.md"],
      folders: ["Projects"],
      sidebarJSON: """
        {"version": 1,
         "pins": {"Projects": ["Projects/ghost.md", "Projects/real.md"]}}
        """)
    let lockURL = vault.appendingPathComponent(".slate/sidebar.json.lock")
    try FileManager.default.createDirectory(
      at: lockURL, withIntermediateDirectories: true)
    state.pruneStaleSidebarPins(
      forFolder: "Projects", stale: ["Projects/ghost.md"])
    await awaitPersist(state)
    XCTAssertEqual(
      state.sidebarOrganization.pins.paths(forFolder: "Projects"),
      ["Projects/ghost.md", "Projects/real.md"],
      "the failed prune changed nothing")

    try FileManager.default.removeItem(at: lockURL)
    state.pruneStaleSidebarPins(
      forFolder: "Projects", stale: ["Projects/ghost.md"])
    await awaitPersist(state)
    XCTAssertEqual(
      state.sidebarOrganization.pins.paths(forFolder: "Projects"),
      ["Projects/real.md"],
      "the slot survived the failure, so the retry prunes")
  }

  func testFailedChainDoesNotReplayAStaleSortCorrectionLater() async throws {
    // Round-12 finding 3: a failed sort's verification record must not leak
    // into a later unrelated commit and speak a delayed sort announcement.
    let announcer = RecordingAnnouncer()
    let (state, vault) = try openVault(
      named: "record-leak", files: ["Projects/note.md"], folders: ["Projects"],
      announcer: announcer)
    let lockURL = vault.appendingPathComponent(".slate/sidebar.json.lock")
    try FileManager.default.createDirectory(
      at: lockURL, withIntermediateDirectories: true)
    try publish(state, [])
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortModifiedDesc)
    await awaitPersist(state)
    try FileManager.default.removeItem(at: lockURL)
    let sortMessagesBefore = announcer.messages.filter {
      $0.hasPrefix("Sorted by")
    }.count

    try publish(
      state, [item("Projects/note.md")],
      focusedPath: "Projects/note.md", creationParent: "Projects")
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarPinNote)
    await awaitPersist(state)
    let sortMessagesAfter = announcer.messages.filter {
      $0.hasPrefix("Sorted by")
    }.count
    XCTAssertEqual(
      sortMessagesAfter, sortMessagesBefore,
      "the pin's tail must not replay the failed sort's stale correction")
  }

  // MARK: - Red-team regressions (adversarial review round 14)

  func testStaleRevalidationIsExactOnCaseInsensitiveFilesystems() async throws {
    // Round-14 finding 2: on the default case-insensitive volume a stat for
    // OLD.md succeeds via old.md. The byte-exact entry check prunes the
    // wrong-case pin, retains a same-name directory, and retains everything
    // under an unreadable parent.
    let (state, vault) = try openVault(
      named: "exact-revalidate", files: ["Projects/old.md"],
      folders: ["Projects", "Projects/dir-entry"],
      sidebarJSON: """
        {"version": 1,
         "pins": {"Projects": [
           "Projects/OLD.md", "Projects/dir-entry", "Projects/old.md"]}}
        """)

    state.pruneStaleSidebarPins(
      forFolder: "Projects",
      stale: ["Projects/OLD.md", "Projects/dir-entry", "Projects/old.md"])
    await awaitPersist(state)

    XCTAssertEqual(
      state.sidebarOrganization.pins.paths(forFolder: "Projects"),
      ["Projects/dir-entry", "Projects/old.md"],
      "the wrong-case pin prunes; an exact-name directory entry and the real file retain")
    let json = try sidebarJSON(at: vault)
    let pins = try XCTUnwrap(json["pins"] as? [String: Any])
    XCTAssertEqual(
      pins["Projects"] as? [String],
      ["Projects/dir-entry", "Projects/old.md"])
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
    await awaitPersist(state)
    XCTAssertEqual(
      state.sidebarOrganization.pins.paths(forFolder: "Projects"),
      ["Projects/real.md"])
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
