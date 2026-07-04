// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Canvas tab lifecycle (Milestone T, #369): the canvas arm of the
/// single navigation funnel, the per-path `CanvasDocument` registry
/// (t2: one document per open path, shared across panes), and the
/// surface-switch command actions.
extension AppState {
    /// The document for `path`, creating it if needed. Creation does
    /// not load — activation loads lazily, so restored sessions with
    /// parked canvas tabs cost nothing until visited.
    func canvasDocument(for path: String) -> CanvasDocument {
        if let existing = canvasDocuments[path] { return existing }
        let doc = CanvasDocument(path: path)
        canvasDocuments[path] = doc
        return doc
    }

    /// The canvas arm of `openFile` (single navigation entry point).
    func openCanvasFile(_ path: String, target: OpenTarget) {
        switch target {
        case .currentTab:
            if let existing = workspace.activeGroupTab(forPath: path) {
                activateTab(existing.id)
                return
            }
            parkOutgoingNoteBuffer()
            if workspace.activeTab != nil {
                workspace.replaceActiveItem(.canvas(path: path))
                if let id = workspace.model.activeGroup.activeTabID {
                    activateTab(id)
                }
            } else {
                let id = workspace.openTab(.canvas(path: path))
                activateTab(id)
            }
        case .newTab:
            if let existing = workspace.activeGroupTab(forPath: path) {
                activateTab(existing.id)
                return
            }
            parkOutgoingNoteBuffer()
            let id = workspace.openTab(.canvas(path: path))
            activateTab(id)
        case .newSplit(let axis):
            let paneCount = workspace.model.groupsInOrder.count
            splitActivePane(axis: axis)
            if workspace.model.groupsInOrder.count == paneCount {
                openCanvasFile(path, target: .newTab)
                return
            }
            openCanvasFile(path, target: .currentTab)
        }
    }

    /// Canvas half of the tab-switch funnel: park the outgoing note
    /// buffer, select the tab, load the shared document (first visit
    /// only), and mirror the sidebar highlight. Canvas tabs carry no
    /// note-editor state, so the note fields clear rather than park.
    func activateCanvasTab(_ id: TabID, path: String) {
        // Same-tab re-activation is a no-op once the document is live —
        // mirrors the markdown branch's early return (Codoki #608:
        // avoids re-clearing collections and main-thread churn).
        if id == workspace.model.activeGroup.activeTabID,
            selectedFilePath == path,
            canvasDocuments[path]?.handle != nil
        {
            return
        }
        workspace.markEditorRegionActive()
        if let pending = pendingTabCloseAfterSave, pending != id {
            pendingTabCloseAfterSave = nil
        }
        isActivatingTab = true
        defer { isActivatingTab = false }
        parkOutgoingNoteBuffer()
        cancelNoteScopedWork()
        clearActiveNoteFields()
        workspace.select(id)
        clearTransitionSensitiveCollections()
        let doc = canvasDocument(for: path)
        if doc.handle == nil, let session = currentSession {
            doc.load(session: session)
        }
        if selectedFilePath != path {
            selectedFilePath = path
        }
    }

    /// Park the active markdown buffer through the standard snapshot
    /// (no-op when nothing markdown is loaded — guards inside).
    func parkOutgoingNoteBuffer() {
        workspace.snapshotActiveTab(
            text: currentNoteText, baseline: savedBaselineText,
            contentHash: currentNoteContentHash,
            hasUnsavedChanges: hasUnsavedChanges,
            saveError: saveError, saveConflict: currentSaveConflict,
            loadedFilePath: loadedFilePath,
            fmSource: currentNoteFMSource,
            bodyByteOffset: bodyByteOffset, bodyLineOffset: bodyLineOffset)
    }

    /// Marks clear (and the FFI handle releases) when the last tab for
    /// a canvas path closes (t2 multi-pane scoping).
    func releaseCanvasDocumentIfUnreferenced(_ item: EditorItem?) {
        guard case .canvas(let path) = item else { return }
        let stillOpen = workspace.model.allTabs.contains { $0.item == .canvas(path: path) }
        guard !stillOpen, let doc = canvasDocuments[path] else { return }
        if let session = currentSession {
            doc.close(session: session)
        }
        canvasDocuments[path] = nil
    }

    /// Deleted-from-disk canvas: drop the document so the next
    /// activation re-reads and lands in the error state (mirrors the
    /// parked-note invalidation contract).
    func invalidateCanvasDocument(path: String) {
        guard let doc = canvasDocuments[path] else { return }
        if let session = currentSession {
            doc.close(session: session)
        }
        canvasDocuments[path] = nil
    }

    /// Vault close: release every canvas handle.
    func releaseAllCanvasDocuments() {
        if let session = currentSession {
            for doc in canvasDocuments.values {
                doc.close(session: session)
            }
        }
        canvasDocuments = [:]
    }

    /// Palette command action: switch the active canvas tab's surface
    /// (Show Outline / Show Table / Show Visual, `CommandSection.canvas`).
    /// No-op when the active tab isn't a canvas — the palette rows stay
    /// registered (R1: commands are always reachable; acting on a
    /// non-canvas tab does nothing rather than erroring).
    func showCanvasSurface(_ surface: CanvasSurface) {
        guard let tab = workspace.activeTab, case .canvas = tab.item else { return }
        workspace.setCanvasSurface(surface, for: tab.id)
        postAccessibilityAnnouncement(
            "Canvas \(surface.title.lowercased()) view.", priority: .medium)
    }
}
