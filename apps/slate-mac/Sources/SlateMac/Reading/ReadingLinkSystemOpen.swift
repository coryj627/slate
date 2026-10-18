// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import ObjectiveC

/// VoiceOver's activation of a reading-view link reaches `ReadingLinkRouter`,
/// never LaunchServices (#1421).
///
/// **Two triggers.** Every reading leaf is a selectable SwiftUI `Text`
/// (`.textSelection(.enabled)`). In this app SwiftUI renders that selection
/// layer as an AppKit text field (the SwiftPM binary records SDK 15, and
/// SwiftUI keeps its AppKit-backed selectable text for it), and the field's
/// accessibility vends each link run as AppKit's text-link element: role
/// link, the run's URL, and a press action. A mouse click on the run goes
/// through the field's link-click path, which SwiftUI sends to the
/// `\.openURL` action, `ReadingLinkRouter.route(_:)`. VoiceOver's VO-Space
/// presses the element instead, and AppKit's press opens the element's URL
/// with `NSWorkspace.open(_:configuration:completionHandler:)` itself — the
/// #1421 spike's stack is `-[NSAccessibilityElement accessibilityPerformPress]`
/// calling `-[NSWorkspace openURL:configuration:completionHandler:]`, no
/// delegate and no environment in between — so `slate-wiki://…` went to
/// LaunchServices ("There is no application set to open the URL …"), the
/// note never opened, and the alert took VoiceOver's focus.
///
/// **The guard.** The system open is the one place the press can be caught.
/// `install()` wraps NSWorkspace's two URL-open entry points once per
/// process. A URL on one of the router's schemes (`isSlateURL`) is handed to
/// the router of the reading view on screen (`register`) and never reaches
/// the system. Every other URL — the same press on an http(s) or mailto
/// link, and every other caller in the process — goes on to NSWorkspace
/// unchanged, so external links open in the browser exactly as before. The
/// router decides what a slate URL does, as it does for a click: the two
/// triggers cannot diverge.
///
/// **Not chosen** (evidence in the #1421 PR): a link element of our own,
/// whose press calls the router — the elements are AppKit's, inside
/// SwiftUI's private selection field, so owning them means replacing
/// SwiftUI's selectable text in every leaf; an accessibility action per
/// link — VO-Space on the link itself would still reach LaunchServices; and
/// registering the schemes in Info.plist with a GetURL handler —
/// LaunchServices hands the URL to whichever registered app it picks (every
/// worktree build shares one bundle id), the schemes become reachable from
/// any other app, and in the spike two registered ad-hoc-signed copies still
/// got "There is no application set to open the URL".
@MainActor
enum ReadingLinkSystemOpen {

    /// One reading view's claim on the opens the guard catches, from mount
    /// to unmount.
    struct Registration: Hashable {
        fileprivate let id = UUID()
    }

    /// Mounted reading views, oldest first. Only the active pane mounts a
    /// reading view; while a remount briefly overlaps two, in whichever
    /// order SwiftUI mounts and unmounts them, the newest still mounted
    /// receives a caught open.
    private static var routes: [(registration: Registration, route: (URL) -> Void)] = []

    #if DEBUG
    /// Test seam: a URL the guard hands on to the system is given to this
    /// instead, so a fact can see what would reach NSWorkspace without
    /// opening the browser. Nil in the app.
    static var systemOpenForTesting: ((URL) -> Void)?
    #endif

    /// Wrap NSWorkspace's URL opens. Idempotent: the exchange runs once per
    /// process, however many reading views mount (a second exchange would
    /// undo the first).
    static func install() {
        _ = exchanged
    }

    private static let exchanged: Void = {
        exchange(
            #selector(NSWorkspace.open(_:) as (NSWorkspace) -> (URL) -> Bool),
            with: #selector(NSWorkspace.slateReadingGuard_open(_:)))
        exchange(
            #selector(NSWorkspace.open(_:configuration:completionHandler:)),
            with: #selector(NSWorkspace.slateReadingGuard_open(_:configuration:completionHandler:)))
    }()

    private static func exchange(_ original: Selector, with replacement: Selector) {
        guard let system = class_getInstanceMethod(NSWorkspace.self, original),
            let guarded = class_getInstanceMethod(NSWorkspace.self, replacement)
        else {
            NSLog("ReadingLinkSystemOpen: NSWorkspace has no %@; that entry point is not guarded.",
                NSStringFromSelector(original))
            return
        }
        method_exchangeImplementations(system, guarded)
    }

    /// Route the opens the guard catches to `route` while the reading view
    /// that owns it is mounted. Installs the guard on first use.
    static func register(_ route: @escaping (URL) -> Void) -> Registration {
        install()
        let registration = Registration()
        routes.append((registration, route))
        return registration
    }

    static func unregister(_ registration: Registration) {
        routes.removeAll { $0.registration == registration }
    }

    /// What the wrapped entry points ask first: true when `url` is the
    /// guard's to deliver (it never reaches the system), false when it goes
    /// on to NSWorkspace.
    nonisolated static func intercepts(_ url: URL) -> Bool {
        let isSlate = ReadingLinkRouter.isSlateURL(url)
        guard Thread.isMainThread else {
            guard isSlate else { return false }
            Task { @MainActor in deliver(url) }
            return true
        }
        return MainActor.assumeIsolated {
            if isSlate {
                deliver(url)
                return true
            }
            #if DEBUG
            if let capture = systemOpenForTesting {
                capture(url)
                return true
            }
            #endif
            return false
        }
    }

    private static func deliver(_ url: URL) {
        guard let route = routes.last?.route else {
            // Unreachable from the UI: a slate URL exists only as a run of
            // a mounted reading view. Dropped, and never the system's.
            NSLog("ReadingLinkSystemOpen: a %@ open arrived with no reading view mounted; dropped.",
                url.scheme ?? "")
            return
        }
        route(url)
    }
}

extension NSWorkspace {
    /// After `ReadingLinkSystemOpen.install()` this implementation answers
    /// `openURL:`, and calling it by this name runs the system's.
    @objc(slateReadingGuard_openURL:)
    fileprivate dynamic func slateReadingGuard_open(_ url: URL) -> Bool {
        if ReadingLinkSystemOpen.intercepts(url) { return true }
        return slateReadingGuard_open(url)
    }

    /// After `ReadingLinkSystemOpen.install()` this implementation answers
    /// `openURL:configuration:completionHandler:` — the call AppKit's
    /// text-link press makes — and calling it by this name runs the
    /// system's.
    @objc(slateReadingGuard_openURL:configuration:completionHandler:)
    fileprivate dynamic func slateReadingGuard_open(
        _ url: URL, configuration: NSWorkspace.OpenConfiguration,
        completionHandler: ((NSRunningApplication?, (any Error)?) -> Void)?
    ) {
        if ReadingLinkSystemOpen.intercepts(url) {
            // Handled in-process: no application was opened, nothing failed.
            completionHandler?(nil, nil)
            return
        }
        slateReadingGuard_open(url, configuration: configuration, completionHandler: completionHandler)
    }
}
