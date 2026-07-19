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

extension AnnouncementPosting {
    /// Post a typed accessibility event (W0.5-3 #719): the canonical
    /// text AND priority come from core (`slate_core::a11y` via the
    /// FFI) — trigger sites decide *when*, never *what it says*.
    func post(_ event: A11yEvent) {
        let rendered = a11yRender(event: event)
        post(rendered.text, priority: AnnouncementPriority(rendered.priority))
    }
}

extension AnnouncementPriority {
    /// Core → host priority bridge (the two levels map 1:1).
    init(_ priority: A11yPriority) {
        switch priority {
        case .medium: self = .medium
        case .high: self = .high
        }
    }
}

/// Production impl: forwards to the global AppKit-backed helper.
struct AppKitAnnouncementPoster: AnnouncementPosting {
    func post(_ message: String, priority: AnnouncementPriority) {
        postAccessibilityAnnouncement(message, priority: priority.nsPriority)
    }
}

/// Post a typed accessibility event through the global AppKit helper —
/// the event-first twin of `postAccessibilityAnnouncement(_:priority:)`
/// (which remains the platform PRIMITIVE this renders into; every
/// interaction-site caller posts events, and `A11yResidueCensusTests`
/// keeps non-poster string-primitive calls at zero).
func postAccessibilityAnnouncement(_ event: A11yEvent) {
    let rendered = a11yRender(event: event)
    postAccessibilityAnnouncement(
        rendered.text,
        priority: AnnouncementPriority(rendered.priority).nsPriority
    )
}
