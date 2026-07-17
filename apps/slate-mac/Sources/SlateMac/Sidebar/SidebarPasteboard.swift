// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit

/// The single pasteboard boundary for Sidebar copy actions. AppState owns the
/// write so every action surface shares one success/failure and announcement
/// contract; tests inject a deterministic recorder instead of touching the
/// user's clipboard.
protocol SidebarPasteboardWriting {
    func clearContents()
    @discardableResult func setString(_ value: String) -> Bool
}

struct AppKitSidebarPasteboard: SidebarPasteboardWriting {
    func clearContents() {
        NSPasteboard.general.clearContents()
    }

    @discardableResult
    func setString(_ value: String) -> Bool {
        NSPasteboard.general.setString(value, forType: .string)
    }
}
