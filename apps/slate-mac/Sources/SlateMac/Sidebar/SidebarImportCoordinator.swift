// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Foundation
import UniformTypeIdentifiers

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
          retainedProgress[slotIndex] = progress
          let cancelProgress = isCancelled && slots[slotIndex] != nil
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
    if let data,
      let url = URL(dataRepresentation: data, relativeTo: nil),
      url.isFileURL
    {
      outcome = .url(url)
    } else if let error {
      outcome = .failure(.loadFailed(error.localizedDescription))
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
