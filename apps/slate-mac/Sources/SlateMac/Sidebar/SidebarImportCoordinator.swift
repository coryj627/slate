// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Foundation
import UniformTypeIdentifiers

enum SidebarImportDestinationNaming {
  static func reservationKey(_ name: String) -> String {
    name.lowercased()
  }

  static func candidateName(
    originalName: String,
    kind: SidebarImportSourceEntryKind,
    sequence: Int
  ) -> String {
    precondition(sequence >= 1)
    guard sequence > 1 else { return originalName }
    let suffix = " \(sequence)"
    guard kind == .regularFile else { return originalName + suffix }

    let path = originalName as NSString
    let pathExtension = path.pathExtension
    guard !pathExtension.isEmpty else { return originalName + suffix }
    return path.deletingPathExtension + suffix + "." + pathExtension
  }
}

struct SidebarImportDestinationCreators: Sendable {
  private let fileCreator: @Sendable (String, Data) throws -> Void
  private let directoryCreator: @Sendable (String) throws -> Void

  init(
    createFile: @escaping @Sendable (String, Data) throws -> Void,
    createDirectory: @escaping @Sendable (String) throws -> Void
  ) {
    fileCreator = createFile
    directoryCreator = createDirectory
  }

  func createFile(path: String, bytes: Data) throws {
    try fileCreator(path, bytes)
  }

  func createDirectory(path: String) throws {
    try directoryCreator(path)
  }

  static func production(session: VaultSession) -> Self {
    Self(
      createFile: { path, bytes in
        if let text = String(data: bytes, encoding: .utf8) {
          _ = try session.createExclusive(path: path, content: text)
        } else {
          _ = try session.createExclusiveBytes(path: path, bytes: bytes)
        }
      },
      createDirectory: { path in
        try session.createFolderExclusive(path: path)
      })
  }
}

enum SidebarImportItemKind: Equatable, Sendable {
  case directory
  case regularFile
  case rejected
}

struct SidebarImportEntryIdentity: Equatable, Sendable {
  let providerIndex: Int
  let sourceRootName: String
  let relativePath: String
  let kind: SidebarImportItemKind
}

enum SidebarImportFailureReason: Equatable, Sendable {
  case source(SidebarImportSourceFailureReason)
  case readFailed(String)
  case capacityExceeded
  case physicalOutcomeUnknown(candidatePath: String, underlying: String)
  case blockedByUnknownAncestor(candidatePath: String)
  case cancelled
}

struct SidebarImportFailure: Equatable, Sendable {
  let identity: SidebarImportEntryIdentity
  let reason: SidebarImportFailureReason
}

struct SidebarImportFailurePresentation: Equatable, Sendable {
  let failures: [SidebarImportFailure]
  let omittedCount: Int
}

struct SidebarImportTopLevelDestination: Equatable, Sendable {
  let providerIndex: Int
  let path: String
  let kind: SidebarImportSourceEntryKind
}

struct SidebarImportUnknownCandidate: Equatable, Sendable {
  let identity: SidebarImportEntryIdentity
  let candidatePath: String
  let underlying: String
}

struct SidebarImportProgressSnapshot: Equatable, Sendable {
  let completedTopLevel: Int
  let totalTopLevel: Int
}

struct SidebarImportReport: Equatable, Sendable {
  let verifiedTopLevelDestinations: [SidebarImportTopLevelDestination]
  let successfulFileCount: Int
  let successfulFolderCount: Int
  let bytesCopied: UInt64
  let failures: [SidebarImportFailure]
  let wasCancelled: Bool
  let progressSnapshots: [SidebarImportProgressSnapshot]

  init(
    verifiedTopLevelDestinations: [SidebarImportTopLevelDestination] = [],
    successfulFileCount: Int = 0,
    successfulFolderCount: Int = 0,
    bytesCopied: UInt64 = 0,
    failures: [SidebarImportFailure],
    wasCancelled: Bool,
    progressSnapshots: [SidebarImportProgressSnapshot] = []
  ) {
    self.verifiedTopLevelDestinations = verifiedTopLevelDestinations
    self.successfulFileCount = successfulFileCount
    self.successfulFolderCount = successfulFolderCount
    self.bytesCopied = bytesCopied
    self.failures = failures
    self.wasCancelled = wasCancelled
    self.progressSnapshots = progressSnapshots
  }

  var unknownCandidates: [SidebarImportUnknownCandidate] {
    failures.compactMap { failure in
      guard case .physicalOutcomeUnknown(let candidatePath, let underlying) = failure.reason
      else { return nil }
      return SidebarImportUnknownCandidate(
        identity: failure.identity,
        candidatePath: candidatePath,
        underlying: underlying)
    }
  }

  var requiresRescan: Bool {
    failures.contains { failure in
      if case .physicalOutcomeUnknown = failure.reason {
        return true
      }
      return false
    }
  }

  var cancelledEntries: [SidebarImportEntryIdentity] {
    failures.compactMap { failure in
      guard failure.reason == .cancelled else { return nil }
      return failure.identity
    }
  }

  var cancelledEntryCount: Int { cancelledEntries.count }

  func failurePresentation(limit: Int) -> SidebarImportFailurePresentation {
    let presentedCount = min(max(0, limit), failures.count)
    return SidebarImportFailurePresentation(
      failures: Array(failures.prefix(presentedCount)),
      omittedCount: failures.count - presentedCount)
  }
}

struct SidebarImportExternalRoot: Sendable {
  let providerIndex: Int
  let preparedSource: SidebarImportPreparedSource
}

struct SidebarImportCoordinatorHooks: Sendable {
  let willAttemptReadAdmission:
    (@Sendable (SidebarImportEntryIdentity) -> Void)?
  let didWaitForReadAdmission:
    (@Sendable (SidebarImportEntryIdentity) -> Void)?
  let didResolveReadAdmission:
    (@Sendable (SidebarImportEntryIdentity) -> Void)?
  let willAttemptCreate:
    (@Sendable (SidebarImportEntryIdentity, String) -> Void)?
  let didUpdateProgress:
    (@Sendable (SidebarImportProgressSnapshot) -> Void)?

  init(
    willAttemptReadAdmission:
      (@Sendable (SidebarImportEntryIdentity) -> Void)? = nil,
    didWaitForReadAdmission:
      (@Sendable (SidebarImportEntryIdentity) -> Void)? = nil,
    didResolveReadAdmission:
      (@Sendable (SidebarImportEntryIdentity) -> Void)? = nil,
    willAttemptCreate:
      (@Sendable (SidebarImportEntryIdentity, String) -> Void)? = nil,
    didUpdateProgress:
      (@Sendable (SidebarImportProgressSnapshot) -> Void)? = nil
  ) {
    self.willAttemptReadAdmission = willAttemptReadAdmission
    self.didWaitForReadAdmission = didWaitForReadAdmission
    self.didResolveReadAdmission = didResolveReadAdmission
    self.willAttemptCreate = willAttemptCreate
    self.didUpdateProgress = didUpdateProgress
  }
}

enum SidebarImportCoordinatorError: Error, Equatable {
  case mismatchedCancellationSignal
}

final class SidebarImportCoordinator: @unchecked Sendable {
  private struct WorkLocation: Hashable {
    let rootIndex: Int
    let itemIndex: Int
  }

  private final class ReadAdmissionSequencer: @unchecked Sendable {
    private let signal: SidebarImportEngineSignal
    private var nextOrdinal = 0

    init(signal: SidebarImportEngineSignal) {
      self.signal = signal
    }

    func perform<Value>(
      ordinal: Int,
      didWait: (() -> Void)?,
      _ admission: () throws -> Value
    ) throws -> Value {
      let condition = signal.condition
      condition.lock()
      precondition(ordinal >= nextOrdinal)
      var notifiedWait = false
      while ordinal != nextOrdinal {
        if signal.isCancelledWithConditionHeld {
          condition.unlock()
          throw SidebarImportByteSchedulerError.cancelled
        }
        if !notifiedWait, let didWait {
          notifiedWait = true
          condition.unlock()
          didWait()
          condition.lock()
          continue
        }
        condition.wait()
      }
      if signal.isCancelledWithConditionHeld {
        condition.unlock()
        throw SidebarImportByteSchedulerError.cancelled
      }
      condition.unlock()

      defer {
        condition.lock()
        precondition(nextOrdinal == ordinal)
        nextOrdinal += 1
        condition.broadcast()
        condition.unlock()
      }
      return try admission()
    }
  }

  private enum WorkItem {
    case entry(SidebarImportSourceEntry)
    case failure(SidebarImportSourceFailure)

    var traversalOrdinal: Int {
      switch self {
      case .entry(let entry): entry.traversalOrdinal
      case .failure(let failure): failure.traversalOrdinal
      }
    }
  }

  private enum FileCreatorAttempt {
    case succeeded(byteCount: UInt64)
    case destinationExists
    case physicalOutcomeUnknown(String)
    case cancelled
  }

  private enum ReadOutcome: @unchecked Sendable {
    case ready(SidebarImportReadyRead)
    case failed(Error)
  }

  private let signal: SidebarImportEngineSignal
  private let scheduler: SidebarImportByteScheduler
  private let creators: SidebarImportDestinationCreators
  private let hooks: SidebarImportCoordinatorHooks

  init(
    signal: SidebarImportEngineSignal,
    scheduler: SidebarImportByteScheduler,
    creators: SidebarImportDestinationCreators,
    hooks: SidebarImportCoordinatorHooks = SidebarImportCoordinatorHooks()
  ) {
    self.signal = signal
    self.scheduler = scheduler
    self.creators = creators
    self.hooks = hooks
  }

  func cancel() {
    signal.cancel()
  }

  func copy(
    roots: [SidebarImportExternalRoot],
    into destinationParent: String,
    reservingMoveBasenames: [String]
  ) async throws -> SidebarImportReport {
    try await withTaskCancellationHandler {
      try await performCopy(
        roots: roots,
        into: destinationParent,
        reservingMoveBasenames: reservingMoveBasenames)
    } onCancel: {
      signal.requestCancellation()
    }
  }

  private func performCopy(
    roots: [SidebarImportExternalRoot],
    into destinationParent: String,
    reservingMoveBasenames: [String]
  ) async throws -> SidebarImportReport {
    defer {
      for root in roots {
        root.preparedSource.close()
      }
    }
    guard scheduler.usesSignal(signal),
      roots.allSatisfy({ $0.preparedSource.usesSignal(signal) })
    else {
      throw SidebarImportCoordinatorError.mismatchedCancellationSignal
    }

    var topLevelDestinations: [SidebarImportTopLevelDestination] = []
    var successfulFileCount = 0
    var successfulFolderCount = 0
    var bytesCopied: UInt64 = 0
    var failures: [SidebarImportFailure] = []
    let initialProgress = SidebarImportProgressSnapshot(
      completedTopLevel: 0,
      totalTopLevel: roots.count)
    var progress = [initialProgress]
    hooks.didUpdateProgress?(initialProgress)
    var occupiedNamesByParent: [String: Set<String>] = [
      destinationParent: Set(
        reservingMoveBasenames.map(SidebarImportDestinationNaming.reservationKey))
    ]
    let orderedWorkItemsByRoot = roots.map { root in
      (
        root.preparedSource.manifest.entries.map { WorkItem.entry($0) }
          + root.preparedSource.manifest.failures.map { WorkItem.failure($0) }
      ).sorted { $0.traversalOrdinal < $1.traversalOrdinal }
    }
    var destinationDirectoriesByRoot = Array(
      repeating: [String: String](), count: roots.count)
    var unknownDirectoryCandidatesByRoot = Array(
      repeating: [String: String](), count: roots.count)
    var pendingReads: [WorkLocation: Task<ReadOutcome, Never>] = [:]
    let readAdmissionSequencer = ReadAdmissionSequencer(signal: signal)
    var nextReadAdmissionOrdinal = 0

    for (rootOrder, root) in roots.enumerated() {
      let orderedWorkItems = orderedWorkItemsByRoot[rootOrder]
      var workIndex = 0
      while workIndex < orderedWorkItems.count {
        let workLocation = WorkLocation(rootIndex: rootOrder, itemIndex: workIndex)
        startEligibleReads(
          at: workLocation,
          workItemsByRoot: orderedWorkItemsByRoot,
          roots: roots,
          destinationDirectoriesByRoot: destinationDirectoriesByRoot,
          admissionSequencer: readAdmissionSequencer,
          nextAdmissionOrdinal: &nextReadAdmissionOrdinal,
          pendingReads: &pendingReads)
        let workItem = orderedWorkItems[workIndex]
        switch workItem {
        case .failure(let sourceFailure):
          failures.append(
            SidebarImportFailure(
              identity: SidebarImportEntryIdentity(
                providerIndex: root.providerIndex,
                sourceRootName: root.preparedSource.sourceRootName,
                relativePath: sourceFailure.relativePath.display,
                kind: .rejected),
              reason: .source(sourceFailure.reason)))

        case .entry(let entry):
          let identity = Self.identity(for: entry, root: root)
          if signal.isCancelled {
            if let pendingRead = pendingReads.removeValue(forKey: workLocation),
              case .ready(let ready) = await pendingRead.value
            {
              ready.discard()
            }
            failures.append(
              SidebarImportFailure(
                identity: identity,
                reason: .cancelled))
            workIndex += 1
            continue
          }
          let isRoot = entry.relativePath.display.isEmpty
          let parentPath: String?
          if isRoot {
            parentPath = destinationParent
          } else {
            parentPath = destinationDirectoriesByRoot[rootOrder][
              entry.relativePath.parentDisplay]
          }
          guard let parentPath else {
            let unknownAncestor = entry.relativePath.directoryAncestorDisplays
              .compactMap { unknownDirectoryCandidatesByRoot[rootOrder][$0] }
              .first ?? ""
            failures.append(
              SidebarImportFailure(
                identity: identity,
                reason: .blockedByUnknownAncestor(candidatePath: unknownAncestor)))
            workIndex += 1
            continue
          }
          let originalName = entry.relativePath.leafName
            ?? root.preparedSource.sourceRootName

          switch entry.kind {
          case .directory:
            var sequence = 1
            directoryCandidates: while true {
              let candidatePath = Self.reserveNextCandidate(
                originalName: originalName,
                kind: .directory,
                parentPath: parentPath,
                sequence: &sequence,
                occupiedNamesByParent: &occupiedNamesByParent)
              hooks.willAttemptCreate?(identity, candidatePath)
              guard let operationLease = signal.beginOperation() else {
                failures.append(
                  SidebarImportFailure(
                    identity: identity,
                    reason: .cancelled))
                break directoryCandidates
              }
              let creatorResult: Result<Void, Error>
              do {
                try creators.createDirectory(path: candidatePath)
                creatorResult = .success(())
              } catch {
                creatorResult = .failure(error)
              }
              operationLease.release()
              switch creatorResult {
              case .success:
                successfulFolderCount += 1
                destinationDirectoriesByRoot[rootOrder][entry.relativePath.display] =
                  candidatePath
                if occupiedNamesByParent[candidatePath] == nil {
                  occupiedNamesByParent[candidatePath] = []
                }
                if isRoot {
                  topLevelDestinations.append(
                    SidebarImportTopLevelDestination(
                      providerIndex: root.providerIndex,
                      path: candidatePath,
                      kind: .directory))
                }
                break directoryCandidates
              case .failure(let error):
                if Self.isDestinationExists(error) {
                  if signal.isCancelled {
                    failures.append(
                      SidebarImportFailure(
                        identity: identity,
                        reason: .cancelled))
                    break directoryCandidates
                  }
                  continue directoryCandidates
                }
                failures.append(
                  SidebarImportFailure(
                    identity: identity,
                    reason: .physicalOutcomeUnknown(
                      candidatePath: candidatePath,
                      underlying: String(describing: error))))
                unknownDirectoryCandidatesByRoot[rootOrder][entry.relativePath.display] =
                  candidatePath
                break directoryCandidates
              }
            }

          case .regularFile:
            do {
              guard let pendingRead = pendingReads.removeValue(forKey: workLocation) else {
                preconditionFailure("eligible file read was not admitted")
              }
              let readOutcome = await pendingRead.value
              let ready: SidebarImportReadyRead
              switch readOutcome {
              case .ready(let value):
                ready = value
              case .failed(let error):
                throw error
              }
              var sequence = 1
              while true {
                let candidatePath = Self.reserveNextCandidate(
                  originalName: originalName,
                  kind: .regularFile,
                  parentPath: parentPath,
                  sequence: &sequence,
                  occupiedNamesByParent: &occupiedNamesByParent)
                var attempt: FileCreatorAttempt?
                try ready.withData { data in
                  hooks.willAttemptCreate?(identity, candidatePath)
                  guard let operationLease = signal.beginOperation() else {
                    attempt = .cancelled
                    return .discard
                  }
                  defer { operationLease.release() }
                  do {
                    try creators.createFile(path: candidatePath, bytes: data)
                    attempt = .succeeded(byteCount: UInt64(data.count))
                    return .publishSucceeded
                  } catch {
                    if Self.isDestinationExists(error) {
                      attempt = .destinationExists
                      return .retainForRetry
                    }
                    attempt = .physicalOutcomeUnknown(String(describing: error))
                    return .discard
                  }
                }
                switch attempt {
                case .succeeded(let createdByteCount):
                  successfulFileCount += 1
                  bytesCopied += createdByteCount
                  if isRoot {
                    topLevelDestinations.append(
                      SidebarImportTopLevelDestination(
                        providerIndex: root.providerIndex,
                        path: candidatePath,
                        kind: .regularFile))
                  }
                  break
                case .destinationExists:
                  if signal.isCancelled {
                    ready.discard()
                    failures.append(
                      SidebarImportFailure(
                        identity: identity,
                        reason: .cancelled))
                    break
                  }
                  continue
                case .physicalOutcomeUnknown(let underlying):
                  failures.append(
                    SidebarImportFailure(
                      identity: identity,
                      reason: .physicalOutcomeUnknown(
                        candidatePath: candidatePath,
                        underlying: underlying)))
                  break
                case .cancelled:
                  failures.append(
                    SidebarImportFailure(
                      identity: identity,
                      reason: .cancelled))
                  break
                case nil:
                  preconditionFailure("ready read did not produce a creator outcome")
                }
                break
              }
            } catch let error as SidebarImportByteSchedulerError {
              failures.append(
                SidebarImportFailure(
                  identity: identity,
                  reason: Self.failureReason(for: error)))
            } catch SidebarImportSourceWalkerError.cancelled {
              failures.append(
                SidebarImportFailure(
                  identity: identity,
                  reason: .cancelled))
            } catch {
              failures.append(
                SidebarImportFailure(
                  identity: identity,
                  reason: .readFailed(String(describing: error))))
            }
          }
        }
        workIndex += 1
      }
      let rootProgress = SidebarImportProgressSnapshot(
        completedTopLevel: rootOrder + 1,
        totalTopLevel: roots.count)
      progress.append(rootProgress)
      hooks.didUpdateProgress?(rootProgress)
    }

    return SidebarImportReport(
      verifiedTopLevelDestinations: topLevelDestinations,
      successfulFileCount: successfulFileCount,
      successfulFolderCount: successfulFolderCount,
      bytesCopied: bytesCopied,
      failures: failures,
      wasCancelled: signal.isCancelled,
      progressSnapshots: progress)
  }

  private static func identity(
    for entry: SidebarImportSourceEntry,
    root: SidebarImportExternalRoot
  ) -> SidebarImportEntryIdentity {
    SidebarImportEntryIdentity(
      providerIndex: root.providerIndex,
      sourceRootName: root.preparedSource.sourceRootName,
      relativePath: entry.relativePath.display,
      kind: entry.kind == .directory ? .directory : .regularFile)
  }

  private func startEligibleReads(
    at startLocation: WorkLocation,
    workItemsByRoot: [[WorkItem]],
    roots: [SidebarImportExternalRoot],
    destinationDirectoriesByRoot: [[String: String]],
    admissionSequencer: ReadAdmissionSequencer,
    nextAdmissionOrdinal: inout Int,
    pendingReads: inout [WorkLocation: Task<ReadOutcome, Never>]
  ) {
    guard !signal.isCancelled else { return }
    var rootIndex = startLocation.rootIndex
    while rootIndex < roots.count, pendingReads.count < 2 {
      let workItems = workItemsByRoot[rootIndex]
      var itemIndex = rootIndex == startLocation.rootIndex
        ? startLocation.itemIndex : 0
      while itemIndex < workItems.count, pendingReads.count < 2 {
        guard !signal.isCancelled else { return }
        let location = WorkLocation(rootIndex: rootIndex, itemIndex: itemIndex)
        switch workItems[itemIndex] {
        case .failure:
          itemIndex += 1
        case .entry(let entry):
          guard entry.kind == .regularFile else { return }
          let isRoot = entry.relativePath.display.isEmpty
          guard isRoot
            || destinationDirectoriesByRoot[rootIndex][entry.relativePath.parentDisplay]
              != nil
          else { return }
          if pendingReads[location] == nil {
            let preparedSource = roots[rootIndex].preparedSource
            let scheduler = scheduler
            let identity = Self.identity(for: entry, root: roots[rootIndex])
            let willAttemptAdmission = hooks.willAttemptReadAdmission
            let didWaitForAdmission = hooks.didWaitForReadAdmission
            let didResolveAdmission = hooks.didResolveReadAdmission
            let admissionOrdinal = nextAdmissionOrdinal
            nextAdmissionOrdinal += 1
            pendingReads[location] = Task.detached(priority: .userInitiated) {
              Self.read(
                entry: entry,
                preparedSource: preparedSource,
                scheduler: scheduler,
                identity: identity,
                willAttemptAdmission: willAttemptAdmission,
                didWaitForAdmission: didWaitForAdmission,
                didResolveAdmission: didResolveAdmission,
                admissionSequencer: admissionSequencer,
                admissionOrdinal: admissionOrdinal)
            }
          }
          itemIndex += 1
        }
      }
      rootIndex += 1
    }
  }

  private static func read(
    entry: SidebarImportSourceEntry,
    preparedSource: SidebarImportPreparedSource,
    scheduler: SidebarImportByteScheduler,
    identity: SidebarImportEntryIdentity,
    willAttemptAdmission:
      (@Sendable (SidebarImportEntryIdentity) -> Void)?,
    didWaitForAdmission:
      (@Sendable (SidebarImportEntryIdentity) -> Void)?,
    didResolveAdmission:
      (@Sendable (SidebarImportEntryIdentity) -> Void)?,
    admissionSequencer: ReadAdmissionSequencer,
    admissionOrdinal: Int
  ) -> ReadOutcome {
    willAttemptAdmission?(identity)
    do {
      let reservedRead = try admissionSequencer.perform(
        ordinal: admissionOrdinal,
        didWait: { didWaitForAdmission?(identity) }
      ) {
        try preparedSource.reserveRead(
          for: entry,
          scheduler: scheduler)
      }
      didResolveAdmission?(identity)
      return .ready(
        try reservedRead.readBytes())
    } catch {
      return .failed(error)
    }
  }

  private static func join(_ parent: String, _ child: String) -> String {
    parent.isEmpty ? child : parent + "/" + child
  }

  private static func reserveNextCandidate(
    originalName: String,
    kind: SidebarImportSourceEntryKind,
    parentPath: String,
    sequence: inout Int,
    occupiedNamesByParent: inout [String: Set<String>]
  ) -> String {
    while true {
      let candidateName = SidebarImportDestinationNaming.candidateName(
        originalName: originalName,
        kind: kind,
        sequence: sequence)
      sequence += 1
      let key = SidebarImportDestinationNaming.reservationKey(candidateName)
      if occupiedNamesByParent[parentPath, default: []].insert(key).inserted {
        return join(parentPath, candidateName)
      }
    }
  }

  private static func isDestinationExists(_ error: Error) -> Bool {
    guard let error = error as? VaultError else { return false }
    if case .DestinationExists = error {
      return true
    }
    return false
  }

  private static func failureReason(
    for error: SidebarImportByteSchedulerError
  ) -> SidebarImportFailureReason {
    switch error {
    case .capacityExceeded, .promotionRequiresExclusiveAccess:
      return .capacityExceeded
    case .cancelled:
      return .cancelled
    default:
      return .readFailed(String(describing: error))
    }
  }
}

protocol SidebarImportProviderLoading: AnyObject {
  var registeredTypeIdentifiers: [String] { get }

  func hasItemConformingToTypeIdentifier(_ typeIdentifier: String) -> Bool

  @discardableResult
  func loadDataRepresentation(
    forTypeIdentifier typeIdentifier: String,
    completionHandler: @escaping @Sendable (Data?, Error?) -> Void
  ) -> Progress
}

extension NSItemProvider: SidebarImportProviderLoading {}

enum SidebarImportProviderFailure: Error, Equatable, Sendable {
  case loadFailed(String)
  case missingData
  case invalidFileURL
  case cancelled
}

enum SidebarImportProviderOutcome: Equatable, Sendable {
  case url(URL)
  case failure(SidebarImportProviderFailure)
}

struct SidebarImportProviderSlot: Equatable, Sendable {
  let providerIndex: Int
  let outcome: SidebarImportProviderOutcome
}

final class SidebarImportProviderIntake: @unchecked Sendable {
  static let fileURLTypeIdentifier = UTType.fileURL.identifier

  private let providers: [(index: Int, provider: SidebarImportProviderLoading)]
  private let lock = NSLock()
  private var continuation: CheckedContinuation<[SidebarImportProviderSlot], Never>?
  private var slots: [SidebarImportProviderSlot?]
  private var retainedProgress: [Progress?]
  private var didStart = false
  private var didFinish = false
  private var isCancelled = false

  init(providers: [SidebarImportProviderLoading]) {
    self.providers = providers.enumerated().compactMap { index, provider in
      guard provider.hasItemConformingToTypeIdentifier(Self.fileURLTypeIdentifier) else {
        return nil
      }
      return (index, provider)
    }
    slots = Array(repeating: nil, count: self.providers.count)
    retainedProgress = Array(repeating: nil, count: self.providers.count)
  }

  func load() async -> [SidebarImportProviderSlot] {
    await withTaskCancellationHandler {
      await withCheckedContinuation { continuation in
        var immediateResult: [SidebarImportProviderSlot]?
        lock.lock()
        precondition(!didStart, "SidebarImportProviderIntake.load() may only be called once")
        didStart = true
        self.continuation = continuation
        if slots.allSatisfy({ $0 != nil }) {
          didFinish = true
          self.continuation = nil
          immediateResult = slots.compactMap { $0 }
        }
        lock.unlock()

        if let immediateResult {
          continuation.resume(returning: immediateResult)
          return
        }

        for (slotIndex, indexedProvider) in providers.enumerated() {
          lock.lock()
          let shouldLoad = slots[slotIndex] == nil
          lock.unlock()
          guard shouldLoad else { continue }

          let progress = indexedProvider.provider.loadDataRepresentation(
            forTypeIdentifier: Self.fileURLTypeIdentifier
          ) { [weak self] data, error in
            self?.complete(
              slotIndex: slotIndex,
              providerIndex: indexedProvider.index,
              data: data,
              error: error)
          }
          lock.lock()
          let slotIsTerminal = slots[slotIndex] != nil
          if !slotIsTerminal {
            retainedProgress[slotIndex] = progress
          }
          let cancelProgress = isCancelled && slotIsTerminal
          lock.unlock()
          if cancelProgress {
            progress.cancel()
          }
        }
      }
    } onCancel: {
      cancel()
    }
  }

  func cancel() {
    var progressToCancel: [Progress] = []
    var resume: CheckedContinuation<[SidebarImportProviderSlot], Never>?
    var result: [SidebarImportProviderSlot] = []
    lock.lock()
    guard !didFinish else {
      lock.unlock()
      return
    }
    isCancelled = true
    for slotIndex in slots.indices where slots[slotIndex] == nil {
      if let progress = retainedProgress[slotIndex] {
        progressToCancel.append(progress)
      }
      retainedProgress[slotIndex] = nil
      slots[slotIndex] = SidebarImportProviderSlot(
        providerIndex: providers[slotIndex].index,
        outcome: .failure(.cancelled))
    }
    if let continuation {
      didFinish = true
      resume = continuation
      self.continuation = nil
      result = slots.compactMap { $0 }
    }
    lock.unlock()

    for progress in progressToCancel {
      progress.cancel()
    }
    resume?.resume(returning: result)
  }

  private func complete(
    slotIndex: Int,
    providerIndex: Int,
    data: Data?,
    error: Error?
  ) {
    let outcome: SidebarImportProviderOutcome
    if let error {
      outcome = .failure(.loadFailed(error.localizedDescription))
    } else if let data,
      let url = URL(dataRepresentation: data, relativeTo: nil),
      url.isFileURL
    {
      outcome = .url(url)
    } else if data == nil {
      outcome = .failure(.missingData)
    } else {
      outcome = .failure(.invalidFileURL)
    }

    var resume: CheckedContinuation<[SidebarImportProviderSlot], Never>?
    var result: [SidebarImportProviderSlot] = []
    lock.lock()
    if !didFinish, slots[slotIndex] == nil {
      slots[slotIndex] = SidebarImportProviderSlot(
        providerIndex: providerIndex,
        outcome: outcome)
      retainedProgress[slotIndex] = nil
    }
    if !didFinish, slots.allSatisfy({ $0 != nil }), let continuation {
      didFinish = true
      resume = continuation
      self.continuation = nil
      result = slots.compactMap { $0 }
    }
    lock.unlock()
    resume?.resume(returning: result)
  }
}
