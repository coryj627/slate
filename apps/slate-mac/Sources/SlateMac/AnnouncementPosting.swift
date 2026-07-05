// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit

/// Priority a VoiceOver announcement is posted at. `.high` interrupts
/// current speech (assertive); `.medium` queues politely — mirroring
/// the two `NSAccessibilityPriorityLevel`s Slate actually uses.
enum AnnouncementPriority {
    case medium
    case high

    var nsPriority: NSAccessibilityPriorityLevel {
        switch self {
        case .medium: return .medium
        case .high: return .high
        }
    }
}

/// Testability seam for accessibility announcements (M-3, #534;
/// normative shape in m_spec §M-3, shared with O-5 — whichever PR
/// lands first creates exactly this seam, the other reuses it).
///
/// The global `postAccessibilityAnnouncement` early-returns when
/// `NSApp == nil` (unit-test runners have no app instance), which
/// makes it un-spyable in tests. AppState takes an init-injected
/// `AnnouncementPosting` instead; announcement-gate tests assert
/// against a recording fake while the app wires the default wrapper.
protocol AnnouncementPosting {
    func post(_ message: String, priority: AnnouncementPriority)
}

/// Production impl: forwards to the global AppKit-backed helper.
struct AppKitAnnouncementPoster: AnnouncementPosting {
    func post(_ message: String, priority: AnnouncementPriority) {
        postAccessibilityAnnouncement(message, priority: priority.nsPriority)
    }
}
