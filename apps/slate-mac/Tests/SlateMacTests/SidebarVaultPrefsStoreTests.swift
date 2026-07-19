// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Darwin
import Foundation
import XCTest

@testable import SlateMac

/// FL1-2 (#654): `.slate/sidebar.json` is a bounded, versioned,
/// forward-safe generic store. FL-02 intentionally authors no feature
/// sections; later milestones add their own keys through `update`.
final class SidebarVaultPrefsStoreTests: XCTestCase {
  private final class RecordingAnnouncer: AnnouncementPosting, @unchecked Sendable {
    private(set) var posts: [(message: String, priority: AnnouncementPriority)] = []

    func post(_ message: String, priority: AnnouncementPriority) {
      posts.append((message, priority))
    }
  }

  private final class FirstCreateRaceOpener: @unchecked Sendable {
    private let lock = NSLock()
    private var attempts = 0

    var attemptCount: Int {
      lock.withLock { attempts }
    }

    func openSidebarFile(in directoryFD: Int32) -> Int32 {
      let attempt = lock.withLock {
        attempts += 1
        return attempts
      }
      if attempt == 1 {
        let renameResult = ".sidebar-prepared".withCString { sourceName in
          "sidebar.json".withCString { destinationName in
            renameat(directoryFD, sourceName, directoryFD, destinationName)
          }
        }
        guard renameResult == 0 else { return -1 }
        errno = ENOENT
        return -1
      }
      return "sidebar.json".withCString { name in
        openat(
          directoryFD,
          name,
          O_RDONLY | O_CLOEXEC | O_NONBLOCK | O_NOFOLLOW
        )
      }
    }
  }

  private final class DirectorySynchronizerSpy: @unchecked Sendable {
    private let lock = NSLock()
    private let shouldFail: Bool
    private var callCountStorage = 0
    private var sawRenamedRegularFileStorage = false

    init(shouldFail: Bool = false) {
      self.shouldFail = shouldFail
    }

    var callCount: Int {
      lock.withLock { callCountStorage }
    }

    var sawRenamedRegularFile: Bool {
      lock.withLock { sawRenamedRegularFileStorage }
    }

    func synchronize(_ directoryFD: Int32) -> Int32 {
      var metadata = stat()
      let result = "sidebar.json".withCString { name in
        fstatat(directoryFD, name, &metadata, AT_SYMLINK_NOFOLLOW)
      }
      let sawRenamedRegularFile =
        result == 0 && metadata.st_mode & S_IFMT == S_IFREG
      lock.withLock {
        callCountStorage += 1
        sawRenamedRegularFileStorage =
          sawRenamedRegularFileStorage || sawRenamedRegularFile
      }
      guard shouldFail else { return 0 }
      errno = EIO
      return -1
    }
  }

  private var vault: URL!

  override func setUpWithError() throws {
    try super.setUpWithError()
    vault = FileManager.default.temporaryDirectory
      .appendingPathComponent("slate-sidebar-prefs-\(UUID().uuidString)", isDirectory: true)
    try FileManager.default.createDirectory(
      at: vault,
      withIntermediateDirectories: true
    )
  }

  override func tearDownWithError() throws {
    try? FileManager.default.removeItem(at: vault)
    try super.tearDownWithError()
  }

  func testMissingFileReturnsDefaultsWithoutNoticeOrCreatingAnything() {
    let store = makeStore()

    let result = store.read()

    XCTAssertEqual(result.root["version"] as? Int, SidebarVaultPrefsStore.currentVersion)
    XCTAssertEqual(Set(result.root.keys), ["version"])
    XCTAssertNil(result.notice)
    XCTAssertFalse(result.isReadOnly)
    XCTAssertFalse(FileManager.default.fileExists(atPath: store.fileURL.path))
    XCTAssertFalse(
      FileManager.default.fileExists(
        atPath: vault.appendingPathComponent(".slate", isDirectory: true).path),
      "a read must not create the settings directory or a lock file"
    )
  }

  func testReadRetriesWhenFirstAtomicCreateWinsAfterInitialENOENT() throws {
    let slateDirectory = vault.appendingPathComponent(".slate", isDirectory: true)
    try FileManager.default.createDirectory(
      at: slateDirectory,
      withIntermediateDirectories: true
    )
    try Data(#"{"version":1,"race":"won"}"#.utf8).write(
      to: slateDirectory.appendingPathComponent(".sidebar-prepared")
    )
    let raceOpener = FirstCreateRaceOpener()
    let store = SidebarVaultPrefsStore(
      vaultRoot: vault,
      sidebarFileOpener: { raceOpener.openSidebarFile(in: $0) }
    )

    let result = store.read()

    XCTAssertNil(result.notice)
    XCTAssertEqual(result.root["race"] as? String, "won")
    XCTAssertEqual(raceOpener.attemptCount, 2)
  }

  func testMaximumReadIsSixteenMebibytes() {
    XCTAssertEqual(SidebarVaultPrefsStore.maxReadBytes, 2 * 1_024 * 1_024)
  }

  func testUpdateCreatesOnlyTheVersionAndCallerKeysWithDeterministicJSON() throws {
    let store = makeStore()

    try store.update { root in
      root["zeta"] = ["enabled": true]
      root["alpha"] = "first"
    }

    let result = store.read()
    XCTAssertNil(result.notice)
    XCTAssertFalse(result.isReadOnly)
    XCTAssertEqual(result.root["version"] as? Int, SidebarVaultPrefsStore.currentVersion)
    XCTAssertEqual(result.root["alpha"] as? String, "first")
    XCTAssertEqual(
      (result.root["zeta"] as? [String: Any])?["enabled"] as? Bool,
      true
    )
    XCTAssertEqual(Set(result.root.keys), ["alpha", "version", "zeta"])

    let text = try String(contentsOf: store.fileURL, encoding: .utf8)
    let alpha = try XCTUnwrap(text.range(of: #""alpha""#)?.lowerBound)
    let version = try XCTUnwrap(text.range(of: #""version""#)?.lowerBound)
    let zeta = try XCTUnwrap(text.range(of: #""zeta""#)?.lowerBound)
    XCTAssertLessThan(alpha, version)
    XCTAssertLessThan(version, zeta)
  }

  func testAtomicReplacementSynchronizesPinnedDirectoryAfterRename() throws {
    let synchronizer = DirectorySynchronizerSpy()
    let store = makeStore(directorySynchronizer: { synchronizer.synchronize($0) })

    try store.update { root in
      root["durable"] = true
    }

    XCTAssertEqual(synchronizer.callCount, 1)
    XCTAssertTrue(
      synchronizer.sawRenamedRegularFile,
      "the directory sync must happen after sidebar.json is atomically renamed into place")
    XCTAssertEqual(store.read().root["durable"] as? Bool, true)
  }

  func testUpdateRejectsAMismatchedRootIdentityOnItsOwnDescriptor() throws {
    // FL-06 round-19: identity verifies via fstat on the exact descriptor
    // the locked write resolves through — a mismatch refuses the write and
    // leaves the file untouched.
    let store = makeStore()
    try store.update { root in
      root["original"] = true
    }
    let before = try Data(contentsOf: store.fileURL)

    let wrongIdentity = SidebarVaultPrefsStore.RootIdentity(
      device: 0xDEAD, inode: 0xBEEF)
    XCTAssertThrowsError(
      try store.update(expectedRootIdentity: wrongIdentity) { root in
        root["intruder"] = true
      }
    ) { error in
      guard case SidebarVaultPrefsStoreError.vaultReplaced = error else {
        return XCTFail("expected vaultReplaced, got \(error)")
      }
    }
    XCTAssertEqual(try Data(contentsOf: store.fileURL), before)

    // The correct identity passes.
    var info = stat()
    XCTAssertEqual(vault.path.withCString { stat($0, &info) }, 0)
    let rightIdentity = SidebarVaultPrefsStore.RootIdentity(
      device: UInt64(info.st_dev), inode: UInt64(info.st_ino))
    try store.update(expectedRootIdentity: rightIdentity) { root in
      root["allowed"] = true
    }
    XCTAssertEqual(store.read().root["allowed"] as? Bool, true)
  }

  func testIdentityBoundReadRefusesAMismatchedRoot() throws {
    // FL-06 round-23: post-admission re-reads must prove the root is still
    // the admitted vault; a mismatch returns nil instead of the newcomer's
    // content (or defaults that would clobber published state).
    let store = makeStore()
    try store.update { root in
      root["ours"] = true
    }
    let wrongIdentity = SidebarVaultPrefsStore.RootIdentity(
      device: 0xDEAD, inode: 0xBEEF)
    XCTAssertNil(store.read(expectedRootIdentity: wrongIdentity))

    var info = stat()
    XCTAssertEqual(vault.path.withCString { stat($0, &info) }, 0)
    let rightIdentity = SidebarVaultPrefsStore.RootIdentity(
      device: UInt64(info.st_dev), inode: UInt64(info.st_ino))
    XCTAssertEqual(
      store.read(expectedRootIdentity: rightIdentity)?.root["ours"] as? Bool,
      true)
  }

  func testAdmissionReadPairsContentWithItsRootIdentity() throws {
    // FL-06 round-24: admission returns content and root identity from ONE
    // descriptor resolution — after a same-path swap the pair is the
    // newcomer's content WITH the newcomer's identity, never a mix.
    let store = makeStore()
    try store.update { root in
      root["ours"] = true
    }
    var info = stat()
    XCTAssertEqual(vault.path.withCString { stat($0, &info) }, 0)
    let original = SidebarVaultPrefsStore.RootIdentity(
      device: UInt64(info.st_dev), inode: UInt64(info.st_ino))

    let first = store.readForAdmission()
    XCTAssertEqual(first.result.root["ours"] as? Bool, true)
    XCTAssertEqual(first.rootIdentity, original)

    // Same-path replacement.
    let aside = vault.deletingLastPathComponent()
      .appendingPathComponent("admission-moved-\(UUID().uuidString)")
    try FileManager.default.moveItem(at: vault, to: aside)
    defer { try? FileManager.default.removeItem(at: aside) }
    let slate = vault.appendingPathComponent(".slate", isDirectory: true)
    try FileManager.default.createDirectory(
      at: slate, withIntermediateDirectories: true)
    try Data(#"{"version": 1, "theirs": true}"#.utf8)
      .write(to: slate.appendingPathComponent("sidebar.json"))

    let second = store.readForAdmission()
    XCTAssertEqual(second.result.root["theirs"] as? Bool, true)
    XCTAssertNil(second.result.root["ours"])
    XCTAssertNotEqual(second.rootIdentity, original)
    var replacementInfo = stat()
    XCTAssertEqual(vault.path.withCString { stat($0, &replacementInfo) }, 0)
    XCTAssertEqual(
      second.rootIdentity,
      SidebarVaultPrefsStore.RootIdentity(
        device: UInt64(replacementInfo.st_dev),
        inode: UInt64(replacementInfo.st_ino)))
  }

  func testIdentityBoundReadTreatsAMissingSlateChildAsDefaults() throws {
    // FL-06 round-24: once the root descriptor matches the admitted
    // identity, a missing `.slate` child is the writable default state —
    // deleting the whole broken directory is a repair, not a replacement.
    let store = makeStore()
    try store.update { root in
      root["ours"] = true
    }
    var info = stat()
    XCTAssertEqual(vault.path.withCString { stat($0, &info) }, 0)
    let identity = SidebarVaultPrefsStore.RootIdentity(
      device: UInt64(info.st_dev), inode: UInt64(info.st_ino))

    try FileManager.default.removeItem(
      at: vault.appendingPathComponent(".slate"))
    let result = try XCTUnwrap(store.read(expectedRootIdentity: identity))
    XCTAssertNil(result.notice)
    XCTAssertNil(result.root["ours"])

    // A vanished root stays fail-closed.
    try FileManager.default.removeItem(at: vault)
    XCTAssertNil(store.read(expectedRootIdentity: identity))
  }

  func testNoOpUpdateSkipsThePhysicalReplacement() throws {
    let synchronizer = DirectorySynchronizerSpy()
    let store = makeStore(directorySynchronizer: { synchronizer.synchronize($0) })
    try store.update { root in
      root["value"] = "same"
    }
    XCTAssertEqual(synchronizer.callCount, 1)

    let before = try Data(contentsOf: store.fileURL)
    try store.update { _ in
      // No change at all.
    }
    try store.update { root in
      root["value"] = "same"  // identical value: still a no-op
    }
    XCTAssertEqual(
      synchronizer.callCount, 1,
      "an unchanged root performs no physical replacement")
    XCTAssertEqual(try Data(contentsOf: store.fileURL), before)
  }

  func testDirectorySyncFailureIsReportedAfterAtomicReplacement() throws {
    let synchronizer = DirectorySynchronizerSpy(shouldFail: true)
    let store = makeStore(directorySynchronizer: { synchronizer.synchronize($0) })

    XCTAssertThrowsError(
      try store.update { root in
        root["writtenBeforeSyncFailure"] = true
      }
    ) { error in
      guard
        case SidebarVaultPrefsStoreError.replacedButUnsynced(let reason) = error
      else {
        return XCTFail("expected replacedButUnsynced, got \(error)")
      }
      XCTAssertTrue(reason.contains("directory"))
      XCTAssertTrue(reason.contains("synchronized"))
    }
    XCTAssertEqual(synchronizer.callCount, 1)
    XCTAssertTrue(synchronizer.sawRenamedRegularFile)
    XCTAssertEqual(store.read().root["writtenBeforeSyncFailure"] as? Bool, true)
  }

  func testUpdatePreservesUnknownTopLevelAndNestedKeys() throws {
    let store = makeStore()
    try writeRaw(
      #"{"version":1,"future":{"nested":{"keep":true}},"untouched":[1,2,3]}"#
    )

    try store.update { root in
      root["laterMilestone"] = ["value": "new"]
    }

    let root = try rawObject(at: store.fileURL)
    let future = root["future"] as? [String: Any]
    let nested = future?["nested"] as? [String: Any]
    XCTAssertEqual(nested?["keep"] as? Bool, true)
    XCTAssertEqual(root["untouched"] as? [Int], [1, 2, 3])
    XCTAssertEqual(
      (root["laterMilestone"] as? [String: Any])?["value"] as? String,
      "new"
    )
  }

  func testMalformedInputUsesDefaultsWithSpecificReadOnlyNoticeAndStaysUntouched() throws {
    let store = makeStore()
    let original = Data("not valid json {{{".utf8)
    try writeRaw(original)

    let result = store.read()

    assertDefaults(result)
    guard case .malformed = result.notice else {
      return XCTFail("expected malformed notice, got \(String(describing: result.notice))")
    }
    XCTAssertTrue(result.notice?.localizedDescription.contains("malformed") == true)
    XCTAssertTrue(
      result.notice?.localizedDescription.contains("Slate won’t change this file") == true)
    XCTAssertTrue(result.notice?.localizedDescription.contains("choose Retry") == true)
    assertReadOnlyUpdate(store, expectedNotice: result.notice)
    XCTAssertEqual(try Data(contentsOf: store.fileURL), original)
  }

  func testNonObjectInputIsMalformedAndReadOnly() throws {
    let store = makeStore()
    try writeRaw(#"["valid JSON", "but not an object"]"#)

    let result = store.read()

    assertDefaults(result)
    guard case .malformed = result.notice else {
      return XCTFail("expected malformed notice, got \(String(describing: result.notice))")
    }
  }

  func testOversizedInputUsesDefaultsWithSpecificReadOnlyNoticeAndStaysUntouched() throws {
    let store = makeStore()
    let original = Data(
      repeating: Character("x").asciiValue!,
      count: SidebarVaultPrefsStore.maxReadBytes + 1
    )
    try writeRaw(original)

    let result = store.read()

    assertDefaults(result)
    guard case .oversized(let limitBytes) = result.notice else {
      return XCTFail("expected oversized notice, got \(String(describing: result.notice))")
    }
    XCTAssertEqual(limitBytes, 2 * 1_024 * 1_024)
    XCTAssertTrue(result.notice?.localizedDescription.contains("2 MiB") == true)
    XCTAssertTrue(
      result.notice?.localizedDescription.contains("Slate won’t change this file") == true)
    assertReadOnlyUpdate(store, expectedNotice: result.notice)
    XCTAssertEqual(
      try FileManager.default.attributesOfItem(atPath: store.fileURL.path)[.size] as? Int,
      original.count
    )
  }

  func testUnreadableInputUsesDefaultsWithSpecificReadOnlyNoticeAndStaysUntouched() throws {
    let store = makeStore()
    try FileManager.default.createDirectory(
      at: store.fileURL,
      withIntermediateDirectories: true
    )

    let result = store.read()

    assertDefaults(result)
    guard case .unreadable = result.notice else {
      return XCTFail("expected unreadable notice, got \(String(describing: result.notice))")
    }
    XCTAssertTrue(result.notice?.localizedDescription.contains("could not be read") == true)
    XCTAssertTrue(
      result.notice?.localizedDescription.contains("Slate won’t change this file") == true)
    assertReadOnlyUpdate(store, expectedNotice: result.notice)
    var isDirectory: ObjCBool = false
    XCTAssertTrue(
      FileManager.default.fileExists(atPath: store.fileURL.path, isDirectory: &isDirectory)
    )
    XCTAssertTrue(isDirectory.boolValue)
  }

  func testBrokenSymlinkIsUnreadableAndIsNeverReplacedAsThoughItWereMissing() throws {
    let store = makeStore()
    try FileManager.default.createDirectory(
      at: store.fileURL.deletingLastPathComponent(),
      withIntermediateDirectories: true
    )
    let missingTarget = vault.appendingPathComponent("missing-sidebar-target.json")
    try FileManager.default.createSymbolicLink(
      at: store.fileURL,
      withDestinationURL: missingTarget
    )

    let result = store.read()

    assertDefaults(result)
    guard case .unreadable = result.notice else {
      return XCTFail("expected unreadable notice, got \(String(describing: result.notice))")
    }
    assertReadOnlyUpdate(store, expectedNotice: result.notice)
    let destination = try FileManager.default.destinationOfSymbolicLink(
      atPath: store.fileURL.path
    )
    XCTAssertEqual(destination, missingTarget.path)
    XCTAssertFalse(FileManager.default.fileExists(atPath: missingTarget.path))
  }

  func testSymlinkedSlateDirectoryIsUnreadableAndDoesNotReadOutsideVault() throws {
    let external = FileManager.default.temporaryDirectory
      .appendingPathComponent("slate-sidebar-external-\(UUID().uuidString)", isDirectory: true)
    try FileManager.default.createDirectory(at: external, withIntermediateDirectories: true)
    defer { try? FileManager.default.removeItem(at: external) }
    let externalFile = external.appendingPathComponent("sidebar.json")
    let original = Data(#"{"version":1,"outside":"must not be read"}"#.utf8)
    try original.write(to: externalFile)
    try FileManager.default.createSymbolicLink(
      at: vault.appendingPathComponent(".slate", isDirectory: true),
      withDestinationURL: external
    )

    let result = makeStore().read()

    assertDefaults(result)
    guard case .unreadable = result.notice else {
      return XCTFail("expected unreadable notice, got \(String(describing: result.notice))")
    }
    XCTAssertNil(result.root["outside"], "a symlinked parent must not escape the vault")
    XCTAssertEqual(try Data(contentsOf: externalFile), original)
  }

  func testUpdateThroughSymlinkedSlateDirectoryDoesNotTouchExternalFileLockOrTemp() throws {
    let external = FileManager.default.temporaryDirectory
      .appendingPathComponent("slate-sidebar-external-\(UUID().uuidString)", isDirectory: true)
    try FileManager.default.createDirectory(at: external, withIntermediateDirectories: true)
    defer { try? FileManager.default.removeItem(at: external) }
    let externalFile = external.appendingPathComponent("sidebar.json")
    let original = Data(#"{"version":1,"outside":"must survive"}"#.utf8)
    try original.write(to: externalFile)
    let namesBefore = try Set(
      FileManager.default.contentsOfDirectory(atPath: external.path)
    )
    try FileManager.default.createSymbolicLink(
      at: vault.appendingPathComponent(".slate", isDirectory: true),
      withDestinationURL: external
    )

    XCTAssertThrowsError(
      try makeStore().update { root in
        root["escaped"] = true
      }
    ) { error in
      guard case SidebarVaultPrefsStoreError.writeFailed = error else {
        return XCTFail("expected writeFailed, got \(error)")
      }
    }

    XCTAssertEqual(try Data(contentsOf: externalFile), original)
    XCTAssertEqual(
      try Set(FileManager.default.contentsOfDirectory(atPath: external.path)),
      namesBefore,
      "the refused update must not create an external lock or temporary file"
    )
    XCTAssertFalse(
      FileManager.default.fileExists(
        atPath: external.appendingPathComponent("sidebar.json.lock").path)
    )
  }

  func testNewerVersionUsesDefaultsWithSpecificReadOnlyNoticeAndStaysByteIdentical() throws {
    let store = makeStore()
    let original = Data(
      #"{"version":999,"future":{"meaning":"must survive"}}"#.utf8
    )
    try writeRaw(original)

    let result = store.read()

    assertDefaults(result)
    guard case .newerVersion(let found, let supported) = result.notice else {
      return XCTFail("expected newer-version notice, got \(String(describing: result.notice))")
    }
    XCTAssertEqual(found, 999)
    XCTAssertEqual(supported, SidebarVaultPrefsStore.currentVersion)
    XCTAssertTrue(result.notice?.localizedDescription.contains("newer version") == true)
    XCTAssertTrue(
      result.notice?.localizedDescription.contains("Slate won’t change this file") == true)
    assertReadOnlyUpdate(store, expectedNotice: result.notice)
    XCTAssertEqual(try Data(contentsOf: store.fileURL), original)
  }

  func testInvalidVersionShapeIsMalformedAndCannotBeOverwritten() throws {
    let store = makeStore()
    let original = Data(#"{"version":"one","future":true}"#.utf8)
    try writeRaw(original)

    let result = store.read()

    assertDefaults(result)
    guard case .malformed = result.notice else {
      return XCTFail("expected malformed notice, got \(String(describing: result.notice))")
    }
    assertReadOnlyUpdate(store, expectedNotice: result.notice)
    XCTAssertEqual(try Data(contentsOf: store.fileURL), original)
  }

  func testOutOfRangeVersionIsMalformedWithoutIntegerOverflow() throws {
    let store = makeStore()
    let original = Data(#"{"version":999999999999999999999999999999}"#.utf8)
    try writeRaw(original)

    let result = store.read()

    assertDefaults(result)
    guard case .malformed = result.notice else {
      return XCTFail("expected malformed notice, got \(String(describing: result.notice))")
    }
    assertReadOnlyUpdate(store, expectedNotice: result.notice)
    XCTAssertEqual(try Data(contentsOf: store.fileURL), original)
  }

  func testInvalidMutationDoesNotReplaceExistingFile() throws {
    let store = makeStore()
    let original = Data(#"{"version":1,"precious":"keep"}"#.utf8)
    try writeRaw(original)

    XCTAssertThrowsError(
      try store.update { root in
        root["notJSON"] = Date()
      }
    ) { error in
      guard case SidebarVaultPrefsStoreError.encodingFailed = error else {
        return XCTFail("expected encodingFailed, got \(error)")
      }
    }
    XCTAssertEqual(try Data(contentsOf: store.fileURL), original)
  }

  func testUpdateRefusesToCreateAFileItCouldNotReadBackWithinTheBound() {
    let store = makeStore()
    let tooLarge = String(
      repeating: "x",
      count: SidebarVaultPrefsStore.maxReadBytes
    )

    XCTAssertThrowsError(
      try store.update { root in
        root["tooLarge"] = tooLarge
      }
    ) { error in
      guard case SidebarVaultPrefsStoreError.encodingFailed = error else {
        return XCTFail("expected encodingFailed, got \(error)")
      }
    }
    XCTAssertFalse(FileManager.default.fileExists(atPath: store.fileURL.path))
  }

  func testTwoInterleavedWritersCannotLoseEitherKey() throws {
    let storeA = makeStore()
    let storeB = makeStore()
    let firstWriterInsideMutation = DispatchSemaphore(value: 0)
    let releaseFirstWriter = DispatchSemaphore(value: 0)
    let group = DispatchGroup()
    let errorLock = NSLock()
    var errors: [Error] = []

    func record(_ error: Error) {
      errorLock.lock()
      errors.append(error)
      errorLock.unlock()
    }

    group.enter()
    DispatchQueue.global(qos: .userInitiated).async {
      defer { group.leave() }
      do {
        try storeA.update { root in
          firstWriterInsideMutation.signal()
          _ = releaseFirstWriter.wait(timeout: .now() + 5)
          root["writerA"] = "kept"
        }
      } catch {
        record(error)
      }
    }

    XCTAssertEqual(firstWriterInsideMutation.wait(timeout: .now() + 5), .success)
    group.enter()
    DispatchQueue.global(qos: .userInitiated).async {
      defer { group.leave() }
      do {
        try storeB.update { root in
          root["writerB"] = "kept"
        }
      } catch {
        record(error)
      }
    }

    // Without a lock around the whole read-mutate-write cycle, writer B
    // reads the same empty root and is then overwritten by writer A.
    Thread.sleep(forTimeInterval: 0.05)
    releaseFirstWriter.signal()
    XCTAssertEqual(group.wait(timeout: .now() + 10), .success)
    XCTAssertTrue(errors.isEmpty, "unexpected writer errors: \(errors)")

    let result = storeA.read()
    XCTAssertNil(result.notice)
    XCTAssertEqual(result.root["writerA"] as? String, "kept")
    XCTAssertEqual(result.root["writerB"] as? String, "kept")
  }

  // MARK: - AppState lifecycle and visible recovery

  @MainActor
  func testAppStateOwnsMalformedVaultStoreAndClearsItOnClose() throws {
    let original = Data("not valid json".utf8)
    try writeRaw(original)
    let (state, suite) = makeAppState()
    defer {
      state.closeVault()
      UserDefaults(suiteName: suite)?.removePersistentDomain(forName: suite)
    }

    state.openVault(at: vault)

    XCTAssertEqual(state.sidebarVaultPrefsStore?.fileURL, makeStore().fileURL)
    guard case .malformed = state.sidebarVaultPrefsNotice else {
      return XCTFail(
        "expected AppState to publish the malformed notice, got "
          + "\(String(describing: state.sidebarVaultPrefsNotice))")
    }
    XCTAssertEqual(try Data(contentsOf: makeStore().fileURL), original)

    state.closeVault()
    XCTAssertNil(state.sidebarVaultPrefsStore)
    XCTAssertNil(state.sidebarVaultPrefsNotice)
  }

  @MainActor
  func testDirectVaultSwitchReplacesTheStoreAndDoesNotCarryNotice() throws {
    try writeRaw("not valid json")
    let cleanVault = FileManager.default.temporaryDirectory
      .appendingPathComponent("slate-sidebar-clean-\(UUID().uuidString)")
    try FileManager.default.createDirectory(
      at: cleanVault,
      withIntermediateDirectories: true)
    let (state, suite) = makeAppState()
    defer {
      state.closeVault()
      try? FileManager.default.removeItem(at: cleanVault)
      UserDefaults(suiteName: suite)?.removePersistentDomain(forName: suite)
    }

    state.openVault(at: vault)
    XCTAssertNotNil(state.sidebarVaultPrefsNotice)

    state.openVault(at: cleanVault)

    XCTAssertEqual(
      state.sidebarVaultPrefsStore?.fileURL,
      SidebarVaultPrefsStore(vaultRoot: cleanVault).fileURL)
    XCTAssertNil(state.sidebarVaultPrefsNotice)
    XCTAssertFalse(
      FileManager.default.fileExists(
        atPath: SidebarVaultPrefsStore(vaultRoot: cleanVault).fileURL.path),
      "opening a vault with no authored fields must not create sidebar.json")
  }

  @MainActor
  func testDirectVaultSwitchAnnouncesTheNewVaultAndItsRecoveryNoticeOnce() throws {
    try writeRaw("not valid json")
    let cleanVault = FileManager.default.temporaryDirectory
      .appendingPathComponent("slate-sidebar-clean-\(UUID().uuidString)")
    try FileManager.default.createDirectory(
      at: cleanVault,
      withIntermediateDirectories: true)
    let announcer = RecordingAnnouncer()
    let (state, suite) = makeAppState(announcer: announcer)
    defer {
      state.closeVault()
      try? FileManager.default.removeItem(at: cleanVault)
      UserDefaults(suiteName: suite)?.removePersistentDomain(forName: suite)
    }

    state.openVault(at: cleanVault)
    XCTAssertTrue(
      announcer.posts.isEmpty,
      "the initial mount owns its own on-appear announcement")

    state.openVault(at: vault)

    XCTAssertEqual(announcer.posts.count, 1)
    XCTAssertEqual(announcer.posts.first?.priority, .medium)
    XCTAssertTrue(announcer.posts.first?.message.contains(vault.lastPathComponent) == true)
    XCTAssertTrue(
      announcer.posts.first?.message.contains("malformed") == true,
      "the one direct-switch announcement must include the typed recovery notice")
  }

  @MainActor
  func testCleanRecentVaultSwitchAnnouncesEvenWhenCloseAndOpenAreCoalesced() throws {
    try writeRaw("not valid json")
    let cleanVault = FileManager.default.temporaryDirectory
      .appendingPathComponent("slate-sidebar-clean-\(UUID().uuidString)")
    try FileManager.default.createDirectory(
      at: cleanVault,
      withIntermediateDirectories: true)
    let announcer = RecordingAnnouncer()
    let (state, suite) = makeAppState(announcer: announcer)
    defer {
      state.closeVault()
      try? FileManager.default.removeItem(at: cleanVault)
      UserDefaults(suiteName: suite)?.removePersistentDomain(forName: suite)
    }

    state.openVault(at: cleanVault)
    state.switchToRecent(RecentVault(url: vault))

    XCTAssertEqual(state.currentVaultURL?.standardizedFileURL, vault.standardizedFileURL)
    XCTAssertEqual(announcer.posts.count, 1)
    XCTAssertTrue(announcer.posts.first?.message.contains("malformed") == true)
  }

  @MainActor
  func testRepairThenRetryClearsNoticeAndAnnouncesSuccess() async throws {
    try writeRaw("not valid json")
    let announcer = RecordingAnnouncer()
    let (state, suite) = makeAppState(announcer: announcer)
    defer {
      state.closeVault()
      UserDefaults(suiteName: suite)?.removePersistentDomain(forName: suite)
    }
    state.openVault(at: vault)
    XCTAssertNotNil(state.sidebarVaultPrefsNotice)

    try writeRaw(#"{"version":1}"#)
    let retry = try XCTUnwrap(state.retrySidebarVaultPreferences())
    await retry.value

    XCTAssertNil(state.sidebarVaultPrefsNotice)
    XCTAssertEqual(announcer.posts.last?.priority, .medium)
    XCTAssertEqual(announcer.posts.last?.message, "Sidebar settings reloaded.")
  }

  @MainActor
  func testRapidRetryIsCoalescedUntilTheActiveReadCompletes() async throws {
    try writeRaw("not valid json")
    let (state, suite) = makeAppState()
    defer {
      state.closeVault()
      UserDefaults(suiteName: suite)?.removePersistentDomain(forName: suite)
    }
    state.openVault(at: vault)

    let firstRetry = try XCTUnwrap(state.retrySidebarVaultPreferences())
    XCTAssertNil(
      state.retrySidebarVaultPreferences(),
      "a second activation must reuse the active read instead of starting another"
    )

    await firstRetry.value

    let laterRetry = try XCTUnwrap(state.retrySidebarVaultPreferences())
    await laterRetry.value
  }

  func testSidebarRendersThePublishedNoticeAndOpenAnnouncementIncludesIt() throws {
    let sources = try appSources()
    XCTAssertTrue(
      sources.fileTree.contains("if let notice = appState.sidebarVaultPrefsNotice"))
    XCTAssertTrue(
      sources.fileTree.contains(
        "sidebarPreferencesNotice(notice.localizedDescription)"))
    // Round-26: the journal-recovery banner shares the notice surface.
    XCTAssertTrue(
      sources.fileTree.contains(
        "appState.sidebarOrganizationJournalRecoveryPending"))
    XCTAssertTrue(sources.fileTree.contains("appState.retrySidebarVaultPreferences()"))
    XCTAssertTrue(
      sources.fileTree.contains(".disabled(appState.isRetryingSidebarVaultPreferences)"))
    XCTAssertTrue(
      sources.mainSplit.contains("let sidebarNotice = appState.sidebarVaultPrefsNotice"))
    // W0.5-3: the notice rides the typed event as a parameter; core's
    // VaultOpened template appends it to the announcement.
    XCTAssertTrue(sources.mainSplit.contains("sidebarNotice: sidebarNotice"))
  }

  // MARK: - Helpers

  @MainActor
  private func makeAppState(
    announcer: AnnouncementPosting = AppKitAnnouncementPoster()
  ) -> (AppState, String) {
    let suite = "slate.sidebar-app-state.\(UUID().uuidString)"
    let defaults = UserDefaults(suiteName: suite)!
    let recents = FileManager.default.temporaryDirectory
      .appendingPathComponent("\(suite).recents.json")
    let state = AppState(
      recentsStore: RecentVaultsStore(fileURL: recents),
      externalOpener: { _ in true },
      preferencesStore: PreferencesStore(defaults: defaults),
      announcer: announcer)
    return (state, suite)
  }

  private func appSources() throws -> (fileTree: String, mainSplit: String) {
    var cursor = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
    for _ in 0..<8 {
      let sources = cursor.appendingPathComponent("Sources/SlateMac")
      let fileTree = sources.appendingPathComponent("FileTreeSidebar.swift")
      let mainSplit = sources.appendingPathComponent("MainSplitView.swift")
      if FileManager.default.fileExists(atPath: fileTree.path),
        FileManager.default.fileExists(atPath: mainSplit.path)
      {
        return (
          try String(contentsOf: fileTree, encoding: .utf8),
          try String(contentsOf: mainSplit, encoding: .utf8)
        )
      }
      cursor = cursor.deletingLastPathComponent()
    }
    throw XCTSkip("SlateMac sources not found relative to the test file")
  }

  private func makeStore() -> SidebarVaultPrefsStore {
    SidebarVaultPrefsStore(vaultRoot: vault)
  }

  private func makeStore(
    directorySynchronizer: @escaping @Sendable (Int32) -> Int32
  ) -> SidebarVaultPrefsStore {
    SidebarVaultPrefsStore(
      vaultRoot: vault,
      sidebarFileOpener: { directoryFD in
        "sidebar.json".withCString { name in
          openat(
            directoryFD,
            name,
            O_RDONLY | O_CLOEXEC | O_NONBLOCK | O_NOFOLLOW
          )
        }
      },
      directorySynchronizer: directorySynchronizer)
  }

  private func writeRaw(_ string: String) throws {
    try writeRaw(Data(string.utf8))
  }

  private func writeRaw(_ data: Data) throws {
    let url = makeStore().fileURL
    try FileManager.default.createDirectory(
      at: url.deletingLastPathComponent(),
      withIntermediateDirectories: true
    )
    try data.write(to: url)
  }

  private func rawObject(at url: URL) throws -> [String: Any] {
    try XCTUnwrap(
      JSONSerialization.jsonObject(with: Data(contentsOf: url)) as? [String: Any]
    )
  }

  private func assertDefaults(
    _ result: SidebarVaultPrefsReadResult,
    file: StaticString = #filePath,
    line: UInt = #line
  ) {
    XCTAssertEqual(
      result.root["version"] as? Int,
      SidebarVaultPrefsStore.currentVersion,
      file: file,
      line: line
    )
    XCTAssertEqual(Set(result.root.keys), ["version"], file: file, line: line)
    XCTAssertNotNil(result.notice, file: file, line: line)
    XCTAssertTrue(result.isReadOnly, file: file, line: line)
  }

  private func assertReadOnlyUpdate(
    _ store: SidebarVaultPrefsStore,
    expectedNotice: SidebarVaultPrefsNotice?,
    file: StaticString = #filePath,
    line: UInt = #line
  ) {
    XCTAssertThrowsError(
      try store.update { root in
        root["mustNotBeWritten"] = true
      },
      file: file,
      line: line
    ) { error in
      guard case SidebarVaultPrefsStoreError.readOnly(let notice) = error else {
        return XCTFail("expected readOnly, got \(error)", file: file, line: line)
      }
      XCTAssertEqual(notice, expectedNotice, file: file, line: line)
    }
  }
}
