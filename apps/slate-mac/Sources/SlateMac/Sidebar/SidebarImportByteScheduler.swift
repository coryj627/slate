// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

enum SidebarImportByteSchedulerError: Error, Equatable {
  case cancelled
  case capacityExceeded(limitBytes: UInt64)
  case promotionRequiresExclusiveAccess
  case invalidReservation
}

struct SidebarImportByteSchedulerSnapshot: Equatable, Sendable {
  let committedBytes: UInt64
  let tentativeBytes: UInt64
  let normalResidentBytes: UInt64
  let normalPlannedBytes: UInt64
  let oversizedResidentBytes: UInt64
  let activeNormalPermits: Int
  let hasExclusivePermit: Bool
  let activeReadingPermits: Int
  let activeReadyPermits: Int
  let waitingRequests: Int
  let highWaterTentativeBytes: UInt64
  let highWaterNormalResidentBytes: UInt64
  let highWaterNormalPlannedBytes: UInt64
  let highWaterOversizedResidentBytes: UInt64
  let highWaterActivePermits: Int
  let highWaterWaitingRequests: Int
}

final class SidebarImportReadyRead: @unchecked Sendable {
  private let lock = NSLock()
  private var bytes: Data?
  private var reservation: SidebarImportByteReservation?

  fileprivate init(data: Data, reservation: SidebarImportByteReservation) {
    bytes = data
    self.reservation = reservation
  }

  deinit {
    discard()
  }

  var data: Data {
    lock.lock()
    defer { lock.unlock() }
    precondition(bytes != nil, "ready-read bytes are no longer available")
    return bytes!
  }

  func publishSucceeded() {
    finish(commit: true)
  }

  func discard() {
    finish(commit: false)
  }

  private func finish(commit: Bool) {
    lock.lock()
    let ownedReservation = reservation
    reservation = nil
    bytes = nil
    lock.unlock()

    if commit {
      ownedReservation?.publishSucceeded()
    } else {
      ownedReservation?.discard()
    }
  }
}

final class SidebarImportByteReservation: @unchecked Sendable {
  private let scheduler: SidebarImportByteScheduler
  fileprivate let identifier: UInt64

  fileprivate init(scheduler: SidebarImportByteScheduler, identifier: UInt64) {
    self.scheduler = scheduler
    self.identifier = identifier
  }

  deinit {
    discard()
  }

  func resize(toByteCount byteCount: UInt64) throws {
    try scheduler.resize(identifier: identifier, toByteCount: byteCount)
  }

  func makeReady(data: Data) throws -> SidebarImportReadyRead {
    try scheduler.markReady(identifier: identifier, actualByteCount: UInt64(data.count))
    return SidebarImportReadyRead(data: data, reservation: self)
  }

  fileprivate func publishSucceeded() {
    scheduler.finish(identifier: identifier, commit: true)
  }

  func discard() {
    scheduler.finish(identifier: identifier, commit: false)
  }
}

final class SidebarImportByteScheduler: @unchecked Sendable {
  static let productionTotalLimitBytes: UInt64 = 1_073_741_824
  static let productionNormalResidentLimitBytes: UInt64 = 128 * 1_024 * 1_024

  private enum Lane {
    case normal
    case oversized
  }

  private enum Phase {
    case admitted
    case reading
    case ready
  }

  private struct Permit {
    var byteCount: UInt64
    var plannedByteCount: UInt64
    var lane: Lane
    var phase: Phase
  }

  private struct Waiter {
    let identifier: UInt64
    let lane: Lane
  }

  private let condition = NSCondition()
  private let totalLimitBytes: UInt64
  private let normalResidentLimitBytes: UInt64
  private let cancellation: SidebarImportSourceCancellationToken
  private var nextIdentifier: UInt64 = 1
  private var permits: [UInt64: Permit] = [:]
  private var waiters: [Waiter] = []
  private var committedBytes: UInt64 = 0
  private var tentativeBytes: UInt64 = 0
  private var normalResidentBytes: UInt64 = 0
  private var normalPlannedBytes: UInt64 = 0
  private var oversizedResidentBytes: UInt64 = 0
  private var highWaterTentativeBytes: UInt64 = 0
  private var highWaterNormalResidentBytes: UInt64 = 0
  private var highWaterNormalPlannedBytes: UInt64 = 0
  private var highWaterOversizedResidentBytes: UInt64 = 0
  private var highWaterActivePermits = 0
  private var highWaterWaitingRequests = 0

  init(
    totalLimitBytes: UInt64 = productionTotalLimitBytes,
    normalResidentLimitBytes: UInt64 = productionNormalResidentLimitBytes,
    cancellation: SidebarImportSourceCancellationToken
  ) {
    self.totalLimitBytes = totalLimitBytes
    self.normalResidentLimitBytes = normalResidentLimitBytes
    self.cancellation = cancellation
  }

  func cancel() {
    condition.lock()
    cancellation.cancel()
    condition.broadcast()
    condition.unlock()
  }

  func snapshot() -> SidebarImportByteSchedulerSnapshot {
    condition.lock()
    defer { condition.unlock() }
    return SidebarImportByteSchedulerSnapshot(
      committedBytes: committedBytes,
      tentativeBytes: tentativeBytes,
      normalResidentBytes: normalResidentBytes,
      normalPlannedBytes: normalPlannedBytes,
      oversizedResidentBytes: oversizedResidentBytes,
      activeNormalPermits: permits.values.reduce(into: 0) { count, permit in
        if permit.lane == .normal { count += 1 }
      },
      hasExclusivePermit: permits.values.contains { $0.lane == .oversized },
      activeReadingPermits: permits.values.reduce(into: 0) { count, permit in
        if permit.phase == .reading { count += 1 }
      },
      activeReadyPermits: permits.values.reduce(into: 0) { count, permit in
        if permit.phase == .ready { count += 1 }
      },
      waitingRequests: waiters.count,
      highWaterTentativeBytes: highWaterTentativeBytes,
      highWaterNormalResidentBytes: highWaterNormalResidentBytes,
      highWaterNormalPlannedBytes: highWaterNormalPlannedBytes,
      highWaterOversizedResidentBytes: highWaterOversizedResidentBytes,
      highWaterActivePermits: highWaterActivePermits,
      highWaterWaitingRequests: highWaterWaitingRequests)
  }

  func reserve(advisoryByteCount: UInt64) throws
    -> SidebarImportByteReservation
  {
    condition.lock()
    defer { condition.unlock() }
    try throwIfCancelledLocked()

    let identifier = nextIdentifier
    nextIdentifier &+= 1
    let lane: Lane = advisoryByteCount <= normalResidentLimitBytes ? .normal : .oversized
    if !waiters.isEmpty || !canEnterLaneLocked(lane, advisoryByteCount: advisoryByteCount) {
      waiters.append(Waiter(identifier: identifier, lane: lane))
      highWaterWaitingRequests = max(highWaterWaitingRequests, waiters.count)
      do {
        while waiters.first?.identifier != identifier
          || !canEnterLaneLocked(lane, advisoryByteCount: advisoryByteCount)
        {
          _ = condition.wait(until: Date(timeIntervalSinceNow: 0.01))
          try throwIfCancelledLocked()
        }
        precondition(waiters.removeFirst().identifier == identifier)
      } catch {
        waiters.removeAll { $0.identifier == identifier }
        condition.broadcast()
        throw error
      }
    }

    let plannedByteCount = lane == .normal ? advisoryByteCount : 0
    permits[identifier] = Permit(
      byteCount: 0,
      plannedByteCount: plannedByteCount,
      lane: lane,
      phase: .admitted)
    normalPlannedBytes += plannedByteCount
    recordHighWaterLocked()
    condition.broadcast()
    return SidebarImportByteReservation(scheduler: self, identifier: identifier)
  }

  func checkCancellation() throws {
    condition.lock()
    defer { condition.unlock() }
    try throwIfCancelledLocked()
  }

  fileprivate func resize(identifier: UInt64, toByteCount newByteCount: UInt64) throws {
    condition.lock()
    defer { condition.unlock() }
    try throwIfCancelledLocked()
    guard var permit = permits[identifier], permit.phase != .ready else {
      throw SidebarImportByteSchedulerError.invalidReservation
    }

    if permit.phase == .admitted {
      guard canAddToTotalLocked(newByteCount) else {
        throw SidebarImportByteSchedulerError.capacityExceeded(limitBytes: totalLimitBytes)
      }
      if newByteCount <= normalResidentLimitBytes {
        let previousPlanned = permit.lane == .normal ? permit.plannedByteCount : 0
        let otherPlanned = normalPlannedBytes - previousPlanned
        guard otherPlanned <= normalResidentLimitBytes,
          newByteCount <= normalResidentLimitBytes - otherPlanned,
          newByteCount <= normalResidentLimitBytes - normalResidentBytes
        else {
          throw SidebarImportByteSchedulerError.capacityExceeded(
            limitBytes: normalResidentLimitBytes)
        }
        normalPlannedBytes = otherPlanned + newByteCount
        permit.lane = .normal
        permit.plannedByteCount = newByteCount
        normalResidentBytes += newByteCount
      } else {
        guard permits.count == 1 else {
          throw SidebarImportByteSchedulerError.promotionRequiresExclusiveAccess
        }
        if permit.lane == .normal {
          normalPlannedBytes -= permit.plannedByteCount
        }
        permit.lane = .oversized
        permit.plannedByteCount = 0
        oversizedResidentBytes += newByteCount
      }
      tentativeBytes += newByteCount
      permit.byteCount = newByteCount
      permit.phase = .reading
      permits[identifier] = permit
      recordHighWaterLocked()
      condition.broadcast()
      return
    }

    guard newByteCount != permit.byteCount else { return }

    if newByteCount < permit.byteCount {
      let released = permit.byteCount - newByteCount
      tentativeBytes -= released
      if permit.lane == .normal {
        normalResidentBytes -= released
        normalPlannedBytes -= released
        permit.plannedByteCount = newByteCount
      } else {
        oversizedResidentBytes -= released
      }
      permit.byteCount = newByteCount
      permits[identifier] = permit
      condition.broadcast()
      return
    }

    let growth = newByteCount - permit.byteCount
    guard canAddToTotalLocked(growth) else {
      throw SidebarImportByteSchedulerError.capacityExceeded(limitBytes: totalLimitBytes)
    }
    if permit.lane == .normal, newByteCount > normalResidentLimitBytes {
      guard permits.count == 1 else {
        throw SidebarImportByteSchedulerError.promotionRequiresExclusiveAccess
      }
      normalResidentBytes -= permit.byteCount
      oversizedResidentBytes += newByteCount
      normalPlannedBytes -= permit.plannedByteCount
      permit.lane = .oversized
      permit.plannedByteCount = 0
    } else if permit.lane == .normal {
      guard normalResidentBytes <= normalResidentLimitBytes,
        normalPlannedBytes <= normalResidentLimitBytes,
        normalResidentLimitBytes - normalResidentBytes >= growth,
        normalResidentLimitBytes - normalPlannedBytes >= growth
      else {
        throw SidebarImportByteSchedulerError.capacityExceeded(
          limitBytes: normalResidentLimitBytes)
      }
      normalResidentBytes += growth
      normalPlannedBytes += growth
      permit.plannedByteCount = newByteCount
    } else {
      oversizedResidentBytes += growth
    }
    tentativeBytes += growth
    permit.byteCount = newByteCount
    permits[identifier] = permit
    recordHighWaterLocked()
  }

  fileprivate func markReady(identifier: UInt64, actualByteCount: UInt64) throws {
    try resize(identifier: identifier, toByteCount: actualByteCount)
    condition.lock()
    defer { condition.unlock() }
    try throwIfCancelledLocked()
    guard var permit = permits[identifier], permit.phase == .reading else {
      throw SidebarImportByteSchedulerError.invalidReservation
    }
    permit.phase = .ready
    permits[identifier] = permit
  }

  fileprivate func finish(identifier: UInt64, commit: Bool) {
    condition.lock()
    guard let permit = permits.removeValue(forKey: identifier) else {
      condition.unlock()
      return
    }
    tentativeBytes -= permit.byteCount
    switch permit.lane {
    case .normal:
      normalResidentBytes -= permit.byteCount
      normalPlannedBytes -= permit.plannedByteCount
    case .oversized:
      oversizedResidentBytes -= permit.byteCount
    }
    if commit {
      let (newCommitted, overflow) = committedBytes.addingReportingOverflow(permit.byteCount)
      precondition(!overflow && newCommitted <= totalLimitBytes)
      committedBytes = newCommitted
    }
    condition.broadcast()
    condition.unlock()
  }

  private func canEnterLaneLocked(_ lane: Lane, advisoryByteCount: UInt64) -> Bool {
    switch lane {
    case .normal:
      guard !permits.values.contains(where: { $0.lane == .oversized }) else { return false }
      let normalCount = permits.values.reduce(into: 0) { count, permit in
        if permit.lane == .normal { count += 1 }
      }
      guard normalCount < 2 else { return false }
      guard normalPlannedBytes <= normalResidentLimitBytes else { return false }
      return advisoryByteCount <= normalResidentLimitBytes - normalPlannedBytes
    case .oversized:
      return permits.isEmpty
    }
  }

  private func canAddToTotalLocked(_ byteCount: UInt64) -> Bool {
    let (used, usedOverflow) = committedBytes.addingReportingOverflow(tentativeBytes)
    guard !usedOverflow else { return false }
    let (requested, requestedOverflow) = used.addingReportingOverflow(byteCount)
    return !requestedOverflow && requested <= totalLimitBytes
  }

  private func throwIfCancelledLocked() throws {
    if cancellation.isCancelled || Task.isCancelled {
      throw SidebarImportByteSchedulerError.cancelled
    }
  }

  private func recordHighWaterLocked() {
    highWaterTentativeBytes = max(highWaterTentativeBytes, tentativeBytes)
    highWaterNormalResidentBytes = max(highWaterNormalResidentBytes, normalResidentBytes)
    highWaterNormalPlannedBytes = max(highWaterNormalPlannedBytes, normalPlannedBytes)
    highWaterOversizedResidentBytes = max(
      highWaterOversizedResidentBytes,
      oversizedResidentBytes)
    highWaterActivePermits = max(highWaterActivePermits, permits.count)
  }

}
