// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// One entry in the welcome screen's "Recent Vaults" list.
///
/// Persisted to disk in JSON. The `path` is the absolute filesystem
/// path the user picked; `displayName` is the folder's last path
/// component (cached at add-time so we don't re-derive it during list
/// rendering). `lastOpenedMs` is Unix epoch milliseconds — used for
/// "last opened <relative date>" labeling and for LRU sort on add.
public struct RecentVault: Codable, Equatable, Identifiable {
    public let path: String
    public let displayName: String
    public let lastOpenedMs: Int64

    public var id: String { path }

    public init(path: String, displayName: String, lastOpenedMs: Int64) {
        self.path = path
        self.displayName = displayName
        self.lastOpenedMs = lastOpenedMs
    }

    /// Build from a vault URL and the current wall clock.
    public init(url: URL, now: Date = Date()) {
        self.init(
            path: url.path,
            displayName: url.lastPathComponent,
            lastOpenedMs: Int64(now.timeIntervalSince1970 * 1000)
        )
    }
}
