// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Bridges scanner-thread `ScanProgressListener.onProgress` calls
/// across to a foreground closure.
///
/// uniffi's generated `ScanProgressListener` is a class-bound protocol
/// invoked from whatever thread `scan_initial_with_progress` is
/// running on — i.e. the detached `Task.detached(priority:
/// .userInitiated)` we use in `AppState.loadFiles`. The protocol
/// docs explicitly warn against blocking inside `onProgress`, so this
/// adapter holds an `@Sendable` closure that the caller is expected
/// to bounce back onto whatever isolation they need (typically the
/// main actor, where the published-property fan-out happens).
///
/// Why a separate type instead of letting `AppState` conform:
/// `AppState` is `@MainActor`-isolated, but `onProgress` is called
/// from the scanner thread. Conformance would require either dropping
/// the actor annotation or littering the call site with
/// `Task { @MainActor in ... }` indirection inside the protocol
/// method, which is exactly what this adapter centralizes.
final class ScanProgressAdapter: ScanProgressListener, @unchecked Sendable {
    private let onEvent: @Sendable (ScanProgress) -> Void

    init(onEvent: @escaping @Sendable (ScanProgress) -> Void) {
        self.onEvent = onEvent
    }

    func onProgress(event: ScanProgress) {
        onEvent(event)
    }
}
