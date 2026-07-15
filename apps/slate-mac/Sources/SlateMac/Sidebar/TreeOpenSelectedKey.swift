// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI

/// Exact, tree-scoped Command-Down routing. SwiftUI translates this chord into
/// `onMoveCommand(.down)` after discarding its modifiers, so the mounted tree
/// observes the physical keyDown first and consumes only this one command.
enum TreeOpenSelectedKey {
    enum Disposition: Equatable {
        case passThrough
        case suppressRepeat
        case open
    }

    static func disposition(
        keyCode: UInt16,
        modifierFlags: NSEvent.ModifierFlags,
        isRepeat: Bool,
        fileTreeFocused: Bool,
        isRenaming: Bool
    ) -> Disposition {
        guard fileTreeFocused, !isRenaming, keyCode == 125 else { return .passThrough }
        let meaningfulModifiers = modifierFlags
            .intersection(.deviceIndependentFlagsMask)
            .subtracting([.capsLock, .function, .numericPad])
        guard meaningfulModifiers == [.command] else { return .passThrough }
        return isRepeat ? .suppressRepeat : .open
    }

    static func eventBelongsToActiveOwner(
        eventWindow: NSWindow?,
        ownerWindow: NSWindow?,
        applicationIsActive: Bool,
        keyWindow: NSWindow?
    ) -> Bool {
        guard let ownerWindow else { return false }
        return applicationIsActive
            && ownerWindow.isKeyWindow
            && keyWindow === ownerWindow
            && eventWindow === ownerWindow
    }

}

/// A zero-size bridge that owns the Command-Down monitor for exactly the window
/// containing this view. `updateNSView` refreshes all live SwiftUI state and the
/// callback; no environment/window value is captured at construction time.
struct TreeOpenSelectedKeyMonitor: NSViewRepresentable {
    let enabled: Bool
    let isRenaming: Bool
    let applicationIsActive: () -> Bool
    let currentKeyWindow: () -> NSWindow?
    let openSelected: () -> Void

    init(
        enabled: Bool,
        isRenaming: Bool,
        applicationIsActive: @escaping () -> Bool = { NSApp.isActive },
        currentKeyWindow: @escaping () -> NSWindow? = { NSApp.keyWindow },
        openSelected: @escaping () -> Void
    ) {
        self.enabled = enabled
        self.isRenaming = isRenaming
        self.applicationIsActive = applicationIsActive
        self.currentKeyWindow = currentKeyWindow
        self.openSelected = openSelected
    }

    func makeNSView(context: Context) -> MonitorView {
        let view = MonitorView()
        view.update(
            enabled: enabled,
            isRenaming: isRenaming,
            applicationIsActive: applicationIsActive,
            currentKeyWindow: currentKeyWindow,
            openSelected: openSelected)
        return view
    }

    func updateNSView(_ view: MonitorView, context: Context) {
        view.update(
            enabled: enabled,
            isRenaming: isRenaming,
            applicationIsActive: applicationIsActive,
            currentKeyWindow: currentKeyWindow,
            openSelected: openSelected)
    }

    static func dismantleNSView(_ view: MonitorView, coordinator: ()) {
        view.stopMonitoring()
    }

    final class MonitorView: NSView {
        private var monitor: Any?
        private var enabled = false
        private var isRenaming = false
        private var applicationIsActive: () -> Bool = { false }
        private var currentKeyWindow: () -> NSWindow? = { nil }
        private var openSelected: () -> Void = {}

        override var intrinsicContentSize: NSSize { .zero }

        func update(
            enabled: Bool,
            isRenaming: Bool,
            applicationIsActive: @escaping () -> Bool,
            currentKeyWindow: @escaping () -> NSWindow?,
            openSelected: @escaping () -> Void
        ) {
            self.enabled = enabled
            self.isRenaming = isRenaming
            self.applicationIsActive = applicationIsActive
            self.currentKeyWindow = currentKeyWindow
            self.openSelected = openSelected
        }

        override func viewDidMoveToWindow() {
            super.viewDidMoveToWindow()
            if window == nil {
                stopMonitoring()
            } else {
                startMonitoringIfNeeded()
            }
        }

        func stopMonitoring() {
            guard let monitor else { return }
            NSEvent.removeMonitor(monitor)
            self.monitor = nil
        }

        private func startMonitoringIfNeeded() {
            guard monitor == nil else { return }
            monitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) {
                [weak self] event in
                guard let self else { return event }
                let ownerWindow = self.window
                guard TreeOpenSelectedKey.eventBelongsToActiveOwner(
                    eventWindow: event.window,
                    ownerWindow: ownerWindow,
                    applicationIsActive: self.applicationIsActive(),
                    keyWindow: self.currentKeyWindow())
                else { return event }

                // Never intercept an input method's marked-text interaction.
                if let editor = ownerWindow?.firstResponder as? NSTextView,
                    editor.hasMarkedText()
                {
                    return event
                }
                switch TreeOpenSelectedKey.disposition(
                    keyCode: event.keyCode,
                    modifierFlags: event.modifierFlags,
                    isRepeat: event.isARepeat,
                    fileTreeFocused: self.enabled,
                    isRenaming: self.isRenaming)
                {
                case .passThrough:
                    return event
                case .suppressRepeat:
                    return nil
                case .open:
                    self.openSelected()
                    return nil
                }
            }
        }

        deinit {
            if let monitor {
                NSEvent.removeMonitor(monitor)
            }
        }
    }
}
