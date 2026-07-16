// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI

/// Complete, fail-closed description of a sidebar row drag. Each selected
/// vault entry becomes its own public pasteboard item so Finder and other apps
/// receive the whole selection. Only the initiating (leader) item also carries
/// Slate's ordered private envelope for a single in-app batch move.
///
/// `NSPasteboardItem` doesn't provide NSItemProvider's `.ownProcess` visibility,
/// so the leader's custom UTI is only a transport hint, never proof of origin.
/// Its envelope carries a one-shot opaque capability registered in this Slate
/// process and bound to the originating vault/session. Every item still carries
/// its public absolute file URL for Finder and other-app interoperability.
struct FileTreeRowDragDescriptor {
    let fileURLs: [URL]
    let directoryFlags: [Bool]
    let leaderIndex: Int
    let privatePayload: Data
    var leaderImage: NSImage?

    init?(
        fileURLs: [URL],
        directoryFlags: [Bool],
        leaderIndex: Int,
        privatePayload: Data,
        leaderImage: NSImage? = nil
    ) {
        guard !fileURLs.isEmpty,
            fileURLs.count == directoryFlags.count,
            fileURLs.indices.contains(leaderIndex),
            fileURLs.allSatisfy(\.isFileURL),
            !privatePayload.isEmpty
        else { return nil }
        self.fileURLs = fileURLs
        self.directoryFlags = directoryFlags
        self.leaderIndex = leaderIndex
        self.privatePayload = privatePayload
        self.leaderImage = leaderImage
    }

    func makePasteboardItems(privateType: String) -> [NSPasteboardItem] {
        let customType = NSPasteboard.PasteboardType(privateType)
        return fileURLs.enumerated().map { index, url in
            let item = NSPasteboardItem()
            item.setData(url.dataRepresentation, forType: .fileURL)
            item.setString(url.absoluteString, forType: .URL)
            if index == leaderIndex {
                item.setData(privatePayload, forType: customType)
            }
            return item
        }
    }

    func makeDraggingItems(privateType: String, at point: NSPoint) -> [NSDraggingItem] {
        makePasteboardItems(privateType: privateType).enumerated().map { index, writer in
            let item = NSDraggingItem(pasteboardWriter: writer)
            let image: NSImage = {
                if index == leaderIndex, let leaderImage { return leaderImage }
                let symbol = directoryFlags[index] ? "folder" : "doc.text"
                return NSImage(systemSymbolName: symbol, accessibilityDescription: nil)
                    ?? NSImage(size: NSSize(width: 24, height: 24))
            }()
            let sourceSize = image.size
            let width = max(24, min(sourceSize.width, 240))
            let height = max(24, min(sourceSize.height, 120))
            item.setDraggingFrame(
                NSRect(
                    x: point.x - width / 2,
                    y: point.y - height / 2,
                    width: width,
                    height: height),
                contents: image)
            return item
        }
    }
}

enum FileTreeRowDragGestureOutcome: Equatable {
    case none
    case click(NSEvent.ModifierFlags)
}

/// Small deterministic state machine shared by the AppKit bridge and tests.
/// AppKit's standard three-point threshold distinguishes a click from a drag;
/// crossing it begins at most one session and suppresses the eventual click.
struct FileTreeRowDragGestureState {
    private enum Phase {
        case idle
        case pending(origin: CGPoint, modifiers: NSEvent.ModifierFlags)
        case dragging
    }

    private var phase: Phase = .idle
    private static let thresholdSquared: CGFloat = 9

    var isTracking: Bool {
        if case .idle = phase { return false }
        return true
    }

    mutating func mouseDown(at location: CGPoint, modifiers: NSEvent.ModifierFlags) {
        phase = .pending(origin: location, modifiers: modifiers)
    }

    /// Returns true exactly once, when the pointer first reaches three points.
    mutating func mouseDragged(to location: CGPoint) -> Bool {
        guard case .pending(let origin, _) = phase else { return false }
        let dx = location.x - origin.x
        let dy = location.y - origin.y
        guard dx * dx + dy * dy >= Self.thresholdSquared else { return false }
        phase = .dragging
        return true
    }

    mutating func mouseUp(inside: Bool) -> FileTreeRowDragGestureOutcome {
        defer { phase = .idle }
        guard case .pending(_, let modifiers) = phase, inside else { return .none }
        return .click(modifiers)
    }

    mutating func cancel() {
        phase = .idle
    }
}

/// A non-hit-testing AppKit bridge mounted behind each SwiftUI List row. A
/// window-local left-mouse monitor owns the down/drag/up sequence so initiating
/// a multi-row drag doesn't collapse SwiftUI's selection before the descriptor
/// is captured. Right-click, Control-click, context menus, keyboard handling,
/// incoming `.onDrop`, and hover targeting continue through SwiftUI unchanged.
struct FileTreeRowDragSource: NSViewRepresentable {
    var makeDescriptor: () -> FileTreeRowDragDescriptor?
    var onClick: (NSEvent.ModifierFlags) -> Void
    var onDragEnded: () -> Void

    static func operation(for context: NSDraggingContext) -> NSDragOperation {
        context == .withinApplication ? .move : .copy
    }

    func makeCoordinator() -> Coordinator {
        Coordinator(
            makeDescriptor: makeDescriptor,
            onClick: onClick,
            onDragEnded: onDragEnded)
    }

    func makeNSView(context: Context) -> TrackingView {
        let view = TrackingView()
        view.coordinator = context.coordinator
        context.coordinator.attach(to: view)
        return view
    }

    func updateNSView(_ nsView: TrackingView, context: Context) {
        context.coordinator.makeDescriptor = makeDescriptor
        context.coordinator.onClick = onClick
        context.coordinator.onDragEnded = onDragEnded
        context.coordinator.attach(to: nsView)
    }

    static func dismantleNSView(_ nsView: TrackingView, coordinator: Coordinator) {
        coordinator.detach()
        nsView.coordinator = nil
    }

    class TrackingView: NSView {
        weak var coordinator: Coordinator?

        override func hitTest(_ point: NSPoint) -> NSView? {
            nil
        }

        override func viewDidMoveToWindow() {
            super.viewDidMoveToWindow()
            coordinator?.viewWindowDidChange()
        }
    }

    final class Coordinator: NSObject, NSDraggingSource {
        var makeDescriptor: () -> FileTreeRowDragDescriptor?
        var onClick: (NSEvent.ModifierFlags) -> Void
        var onDragEnded: () -> Void

        private weak var view: TrackingView?
        private var monitor: Any?
        private var gesture = FileTreeRowDragGestureState()
        private var mouseDownEvent: NSEvent?

        init(
            makeDescriptor: @escaping () -> FileTreeRowDragDescriptor?,
            onClick: @escaping (NSEvent.ModifierFlags) -> Void,
            onDragEnded: @escaping () -> Void
        ) {
            self.makeDescriptor = makeDescriptor
            self.onClick = onClick
            self.onDragEnded = onDragEnded
        }

        deinit {
            if let monitor { NSEvent.removeMonitor(monitor) }
        }

        func attach(to view: TrackingView) {
            guard self.view !== view else { return }
            self.view = view
            installMonitor()
        }

        func detach() {
            if let monitor { NSEvent.removeMonitor(monitor) }
            monitor = nil
            view = nil
            mouseDownEvent = nil
            gesture.cancel()
        }

        func viewWindowDidChange() {
            installMonitor()
        }

        private func installMonitor() {
            if let monitor { NSEvent.removeMonitor(monitor) }
            monitor = nil
            guard let view, let window = view.window else { return }
            monitor = NSEvent.addLocalMonitorForEvents(
                matching: [.leftMouseDown, .leftMouseDragged, .leftMouseUp]
            ) { [weak self, weak view, weak window] event in
                guard let self, let view, let window,
                    event.window === window
                else { return event }
                return self.handle(event, in: view)
            }
        }

        private func handle(_ event: NSEvent, in view: TrackingView) -> NSEvent? {
            let location = view.convert(event.locationInWindow, from: nil)
            let inside = view.bounds.contains(location)
            switch event.type {
            case .leftMouseDown:
                // macOS treats Control-click as a secondary click; don't steal
                // it from SwiftUI's context-menu recognizer.
                guard inside, !event.modifierFlags.contains(.control) else { return event }
                gesture.mouseDown(at: location, modifiers: event.modifierFlags)
                mouseDownEvent = event
                return nil
            case .leftMouseDragged:
                guard gesture.isTracking else { return event }
                if gesture.mouseDragged(to: location) {
                    beginDrag(in: view)
                }
                return nil
            case .leftMouseUp:
                guard gesture.isTracking else { return event }
                let outcome = gesture.mouseUp(inside: inside)
                mouseDownEvent = nil
                if case .click(let modifiers) = outcome {
                    onClick(modifiers)
                }
                return nil
            default:
                return event
            }
        }

        private func beginDrag(in view: TrackingView) {
            guard let event = mouseDownEvent,
                let descriptor = makeDescriptor()
            else {
                mouseDownEvent = nil
                gesture.cancel()
                return
            }
            let point = view.convert(event.locationInWindow, from: nil)
            let items = descriptor.makeDraggingItems(
                privateType: FileTreeSidebar.nodeUTType,
                at: point)
            guard !items.isEmpty else {
                mouseDownEvent = nil
                gesture.cancel()
                return
            }
            let session = view.beginDraggingSession(
                with: items,
                event: event,
                source: self)
            session.draggingLeaderIndex = descriptor.leaderIndex
            session.draggingFormation = .pile
            session.animatesToStartingPositionsOnCancelOrFail = true
        }

        func draggingSession(
            _ session: NSDraggingSession,
            sourceOperationMaskFor context: NSDraggingContext
        ) -> NSDragOperation {
            FileTreeRowDragSource.operation(for: context)
        }

        func ignoreModifierKeys(for session: NSDraggingSession) -> Bool {
            true
        }

        func draggingSession(
            _ session: NSDraggingSession,
            endedAt screenPoint: NSPoint,
            operation: NSDragOperation
        ) {
            mouseDownEvent = nil
            gesture.cancel()
            onDragEnded()
        }
    }
}
