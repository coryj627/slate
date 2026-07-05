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
        dropCanvasModeState(for: path)
    }

    /// Red-team #521 (2): a released/invalidated document must take its
    /// mode controller and any transient with it — a phantom mode
    /// otherwise M7-blocks the reopened canvas and leaks the transient.
    private func dropCanvasModeState(for path: String) {
        if canvasModeControllers[path]?.active != nil {
            canvasTransient = nil
        }
        canvasModeControllers[path] = nil
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
        dropCanvasModeState(for: path)
    }

    /// Vault close: release every canvas handle.
    func releaseAllCanvasDocuments() {
        if let session = currentSession {
            for doc in canvasDocuments.values {
                doc.close(session: session)
            }
        }
        canvasDocuments = [:]
        canvasModeControllers = [:]
        canvasTransient = nil
    }

    /// Palette command action: switch the active canvas tab's surface
    /// (Show Outline / Show Table / Show Visual, `CommandSection.canvas`).
    /// No-op when the active tab isn't a canvas — the palette rows stay
    /// registered (R1: commands are always reachable; acting on a
    /// non-canvas tab does nothing rather than erroring).
    func showCanvasSurface(_ surface: CanvasSurface) {
        guard let tab = workspace.activeTab, case .canvas = tab.item else { return }
        workspace.setCanvasSurface(surface, for: tab.id)
        canvasAnnouncer.announce(.status("Canvas \(surface.title.lowercased()) view."))
    }

    /// ⌃⌘I (t0 §1.4, #518): one pull-based verbose readback of the
    /// selected card's full context — announced AND rendered in a
    /// focusable transient panel so braille users read it at leisure.
    func canvasWhereAmI() {
        guard let tab = workspace.activeTab, case .canvas(let path) = tab.item else { return }
        let doc = canvasDocument(for: path)
        guard let handle = doc.handle, let session = currentSession else { return }
        guard case .ready = doc.state else {
            canvasAnnouncer.announce(.status("Canvas is not readable."))
            return
        }
        // Fall back to the first card in reading order when nothing is
        // selected yet (fresh landing) — "where am I" always answers.
        guard let nodeId = doc.selection.selected ?? doc.outline.first?.nodeId else {
            canvasAnnouncer.announce(.status("Canvas is empty."))
            return
        }
        do {
            let ctx = try session.canvasWhereAmI(handle: handle, nodeId: nodeId)
            let text = canvasAnnouncer.whereAmIText(
                ctx,
                marked: doc.selection.marked.contains(nodeId),
                activeMode: nil,  // mode stack lands with #364 (Wave 3)
                filterSummary: nil)  // filter lands with #373 (Wave 5)
            canvasWhereAmIReadback = text
            canvasAnnouncer.announce(.status(text))
        } catch {
            canvasAnnouncer.announce(.error("Where am I failed: \(error.localizedDescription)"))
        }
    }

    /// Persist a live verbosity change (#518 setting).
    func setCanvasVerbosity(_ verbosity: CanvasVerbosity) {
        canvasAnnouncer.verbosity = verbosity
        preferencesStore.saveCanvasPrefs(CanvasPrefs(verbosity: verbosity))
    }

    // MARK: Mutations + undo (#372)

    /// The one mutation entry point every canvas verb uses: applies the
    /// action (one write, one journal entry), pushes the inverse onto
    /// the document's undo stack, clears redo. Errors surface through
    /// the funnel (conflicts assertively, t0 §5) and return false.
    @discardableResult
    func canvasApply(_ action: CanvasAction, to doc: CanvasDocument) -> Bool {
        // Red-team #521 (1): a mutation while a spatial mode holds a
        // transient would invalidate the mode's start snapshot — a
        // later Return would silently clobber this change with
        // entry-time absolute rects. The mode's OWN commit clears
        // `canvasTransient` before calling here, so this guard only
        // stops out-of-band verbs (palette/menu) mid-mode.
        guard canvasTransient == nil else {
            canvasAnnouncer.announce(
                .error("A move or resize is in progress. Return to place it or Escape to cancel first."))
            return false
        }
        guard let session = currentSession, let handle = doc.handle else { return false }
        do {
            let result = try session.canvasApply(handle: handle, action: action)
            doc.undoStack.append((name: action.name, inverse: result.inverse))
            doc.redoStack = []
            doc.reloadAfterMutation(session: session)
            return true
        } catch let error as VaultError {
            if case .WriteConflict = error {
                canvasAnnouncer.announce(
                    .error(
                        "The canvas changed on disk. Reload it to continue — your action was not applied."
                    ))
            } else {
                canvasAnnouncer.announce(.error("Canvas action failed: \(error.localizedDescription)"))
            }
            return false
        } catch {
            canvasAnnouncer.announce(.error("Canvas action failed: \(error.localizedDescription)"))
            return false
        }
    }

    /// ⌘Z on a canvas surface: apply the top inverse; its own inverse
    /// becomes the redo entry. "Undid: ⟨name⟩" per t0 §1.3.
    func canvasUndo() {
        guard let doc = activeCanvasDocument else { return }
        guard canvasTransient == nil else {
            canvasAnnouncer.announce(
                .error("A move or resize is in progress. Return to place it or Escape to cancel first."))
            return
        }
        guard let entry = doc.undoStack.popLast() else {
            canvasAnnouncer.announce(.status("Nothing to undo."))
            return
        }
        guard let session = currentSession, let handle = doc.handle else { return }
        do {
            let result = try session.canvasApply(handle: handle, action: entry.inverse)
            doc.redoStack.append((name: entry.name, inverse: result.inverse))
            doc.reloadAfterMutation(session: session)
            canvasAnnouncer.announce(.confirmation(CanvasAnnouncer.undidText(actionName: entry.name)))
        } catch {
            // Stale undo after an external change: conflict surfaces,
            // the entry stays poppable after the user reloads (t3).
            doc.undoStack.append(entry)
            canvasAnnouncer.announce(
                .error("Undo blocked: the canvas changed on disk. Reload it and try again."))
        }
    }

    /// ⇧⌘Z symmetric to `canvasUndo`.
    func canvasRedo() {
        guard let doc = activeCanvasDocument else { return }
        guard canvasTransient == nil else {
            canvasAnnouncer.announce(
                .error("A move or resize is in progress. Return to place it or Escape to cancel first."))
            return
        }
        guard let entry = doc.redoStack.popLast() else {
            canvasAnnouncer.announce(.status("Nothing to redo."))
            return
        }
        guard let session = currentSession, let handle = doc.handle else { return }
        do {
            let result = try session.canvasApply(handle: handle, action: entry.inverse)
            doc.undoStack.append((name: entry.name, inverse: result.inverse))
            doc.reloadAfterMutation(session: session)
            canvasAnnouncer.announce(.confirmation(CanvasAnnouncer.redidText(actionName: entry.name)))
        } catch {
            doc.redoStack.append(entry)
            canvasAnnouncer.announce(
                .error("Redo blocked: the canvas changed on disk. Reload it and try again."))
        }
    }

    /// The responder-chain seam (#372): ⌘Z drives the canvas stack when
    /// a canvas surface owns focus, the standard responder chain
    /// otherwise (NSTextView editors keep their NSUndoManager). Wave 4's
    /// inline text-card editor refines this with a first-responder
    /// text-view check.
    var undoTargetsCanvas: Bool {
        guard activeCanvasDocument != nil else { return false }
        // Inside the inline editor (Wave 4) the editor's undo wins;
        // today the only canvas text surface is the read-only detail.
        return true
    }
}
