// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Foundation

/// AppKit wrapper around `NSOpenPanel` configured for vault selection.
///
/// Directory-mode only — files are not selectable. Returns `nil` when
/// the user cancels (Esc or Cancel button), in which case app state
/// must not change.
enum VaultPicker {
    static func pick() -> URL? {
        let panel = NSOpenPanel()
        panel.canChooseFiles = false
        panel.canChooseDirectories = true
        panel.allowsMultipleSelection = false
        panel.title = "Open vault"
        panel.message = "Choose a folder of Markdown files to open as a vault."
        panel.prompt = "Open"
        guard panel.runModal() == .OK else { return nil }
        return panel.url
    }
}
