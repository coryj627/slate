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
    func openCanvasFile(
        _ path: String,
        target: OpenTarget,
        advancesSidebarSelectionRevision: Bool = true
    ) {
        if let reason = propertyEditNavigationDisabledReason {
            postMutationAnnouncement(reason)
            return
        }
        if advancesSidebarSelectionRevision {
            recordExplicitSidebarNavigationIntent()
        }
        switch target {
        case .currentTab:
            if let existing = workspace.activeGroupTab(forPath: path) {
                activateTab(existing.id)
                return
            }
            guard admitCurrentTabReplacementForPropertyRecovery() else { return }
            parkOutgoingNoteBuffer()
            if workspace.activeTab != nil {
                let replacedItem = workspace.activeTab?.item
                workspace.replaceActiveItem(.canvas(path: path))
                releaseCanvasDocumentIfUnreferenced(replacedItem)
                releaseBaseDocumentIfUnreferenced(replacedItem)
                releaseDashboardDocumentIfUnreferenced(replacedItem)
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
                openCanvasFile(
                    path,
                    target: .newTab,
                    advancesSidebarSelectionRevision: false)
                return
            }
            openCanvasFile(
                path,
                target: .currentTab,
                advancesSidebarSelectionRevision: false)
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
            canvasDocuments[path]?.consumePreparedActivationIfNeeded()
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
        let skipSynchronousLoad = doc.shouldSkipSynchronousActivationLoad()
        if doc.handle == nil,
            !isBatchTrashPathQuarantined(path),
            let session = currentSession
        {
            if doc.hasPendingRetargetPreparation {
                scheduleCanvasRetargetPreparationIfNeeded(
                    document: doc, path: path, session: session)
            } else if !skipSynchronousLoad {
                canvasNewFileNativeExecutionObserverForTesting?(
                    CanvasNewFileNativeExecutionEvent(
                        phase: .activationLoad,
                        ranOnMainThread: CanvasNewFileThreadProbe.isMainThread()))
                doc.load(session: session)
            }
        }
        if selectedFilePath != path {
            selectedFilePath = path
        }
    }

    /// Park the active markdown buffer through the standard snapshot
    /// (no-op when nothing markdown is loaded — guards inside).
    func parkOutgoingNoteBuffer() {
        parkPropertiesSourceDraftForTransition()
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
        // New Canvas keeps its per-path object registry-owned while native
        // preparation/tree refresh suspends. Dropping it here would orphan the
        // prepared handle and make the eventual landing synchronously reload a
        // different object on the main actor.
        guard !doc.hasPreparedLoadReservation else { return }
        if let session = currentSession {
            doc.close(session: session)
        }
        canvasDocuments[path] = nil
        dropCanvasModeState(for: path)
    }

    /// Release a cancelled New Canvas reservation, and remove its placeholder
    /// when no tab still owns it. Vault switches have already cleared the
    /// registry, so identity checks keep stale tasks from touching the new one.
    func abandonCanvasPreparedReplacement(
        _ document: CanvasDocument,
        path: String,
        session: VaultSession
    ) {
        guard currentSession === session,
            canvasDocuments[path] === document
        else { return }
        document.abandonPreparedReplacement()
        let stillOpen = workspace.model.allTabs.contains {
            $0.item == .canvas(path: path)
        }
        if !stillOpen {
            canvasDocuments[path] = nil
            dropCanvasModeState(for: path)
        }
    }

    /// Red-team #521 (2): a released/invalidated document must take its
    /// mode controller and any transient with it — a phantom mode
    /// otherwise M7-blocks the reopened canvas and leaks the transient.
    func dropCanvasModeState(for path: String) {
        if canvasModeControllers[path]?.active != nil {
            canvasTransient = nil
        }
        canvasModeControllers[path] = nil
    }

    /// Deleted-from-disk canvas: retain the mounted document identity and land
    /// it directly in the unavailable state. Dropping it here lets
    /// `activeCanvasDocument` create a fresh `.loading` object which this
    /// already-mounted view never loads, leaving an infinite Opening state and
    /// briefly re-enabling prompt/picker writes.
    func invalidateCanvasDocument(path: String) {
        guard let doc = canvasDocuments[path] else { return }
        doc.markMovedToTrash(session: currentSession)
        dropCanvasModeState(for: path)
    }

    /// Rekey a physically moved canvas without replacing its shared Swift
    /// document, selection, viewport, mode, or undo stacks. The path-bound
    /// native handle is closed and reopened at the landed path.
    func rekeyCanvasDocument(oldPath: String, newPath: String) {
        guard !BaseExactIdentity.matches(oldPath, newPath) else { return }
        let prepare = workspace.model.groupsInOrder.contains { group in
            guard case .canvas(let path)? = group.activeTab?.item else { return false }
            return BaseExactIdentity.matches(path, newPath)
        }
        if let plan = detachCanvasDocumentForRetarget(
            oldPath: oldPath, newPath: newPath, prepare: prepare)
        {
            scheduleNativeDocumentRetargets([plan])
        }
    }

    /// Synchronous Swift-only half of Canvas retargeting. The returned value is
    /// safe to execute off-main and owns the detached old handle.
    func detachCanvasDocumentForRetarget(
        oldPath: String,
        newPath: String,
        prepare: Bool
    ) -> NativeDocumentRetargetPlan? {
        guard !BaseExactIdentity.matches(oldPath, newPath) else { return nil }
        guard let document = canvasDocuments.removeValue(forKey: oldPath) else {
            if let controller = canvasModeControllers.removeValue(forKey: oldPath) {
                canvasModeControllers[newPath] = controller
            }
            return nil
        }

        let reservation = document.beginBatchRetarget(to: newPath)
        let collided = canvasDocuments[newPath].map { $0 !== document } ?? false
        if !collided {
            canvasDocuments[newPath] = document
        }
        if let controller = canvasModeControllers.removeValue(forKey: oldPath),
            canvasModeControllers[newPath] == nil
        {
            canvasModeControllers[newPath] = controller
        }

        let shouldPrepare = !collided && prepare
            && document.claimRetargetPreparation() == reservation.generation
        return .canvas(
            path: newPath,
            generation: reservation.generation,
            replacedHandle: reservation.replacedHandle,
            prepare: shouldPrepare)
    }

    func scheduleCanvasRetargetPreparationIfNeeded(
        document: CanvasDocument,
        path: String,
        session: VaultSession
    ) {
        guard currentSession === session,
            canvasDocuments[path] === document,
            let generation = document.claimRetargetPreparation()
        else { return }
        scheduleNativeDocumentRetargets(
            [
                .canvas(
                    path: path,
                    generation: generation,
                    replacedHandle: nil,
                    prepare: true)
            ],
            session: session)
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
            // #373: the navigator's "filtered" cue — Where-am-I always
            // discloses an active filter and its result count.
            var full = text
            if doc.filterActive {
                full +=
                    " Filter active: \(doc.filteredOutline.count) of "
                    + "\(CountCopy.counted(doc.outline.count, "card", "cards")) "
                    + CountCopy.verb(doc.filteredOutline.count, "matches", "match") + "."
            }
            canvasWhereAmIReadback = full
            canvasAnnouncer.announce(.status(full))
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

    /// Outcome-unknown Trash paths keep their last safe snapshot visible, but
    /// every authoring surface must describe that snapshot as read-only. This
    /// single capability feeds views, command availability, prompts, keyboard
    /// actions, and the final native-write backstop.
    static let canvasOpeningMutationDisabledReason =
        "This canvas is still opening. Wait for it to finish before making changes."
    static let canvasReopeningMutationDisabledReason =
        "This canvas is reopening. Wait for it to finish before making changes."
    static let canvasRetargetFailedMutationDisabledReason =
        "This canvas could not be reopened. Choose Retry before making changes."
    static let canvasUnavailableMutationDisabledReason =
        "This canvas is no longer available. Copy any draft before closing."
    static let canvasReadOnlyMutationDisabledReason =
        "This canvas is read-only because it could not be opened safely."

    func canvasMutationDisabledReason(for document: CanvasDocument) -> String? {
        switch batchTrashPathCapability(for: document.path) {
        case .writable:
            break
        case .readOnly(let reason), .invalid(let reason):
            return reason
        }

        if document.handle != nil { return nil }
        if document.hasPendingRetargetPreparation {
            if case .retargetFailed = document.state {
                return Self.canvasRetargetFailedMutationDisabledReason
            }
            return Self.canvasReopeningMutationDisabledReason
        }
        switch document.state {
        case .loading:
            return Self.canvasOpeningMutationDisabledReason
        case .retargetFailed:
            return Self.canvasRetargetFailedMutationDisabledReason
        case .failed:
            return Self.canvasUnavailableMutationDisabledReason
        case .degraded:
            return Self.canvasReadOnlyMutationDisabledReason
        case .ready:
            return Self.canvasUnavailableMutationDisabledReason
        }
    }

    /// Unlike the navigation-facing `activeCanvasDocument`, recovery UI must
    /// retain access to a failed or retargeting document so it can explain why
    /// authoring is unavailable and offer Retry without creating a new object.
    var activeCanvasRecoveryDocument: CanvasDocument? {
        guard let tab = workspace.activeTab,
            case .canvas(let path) = tab.item
        else { return nil }
        return canvasDocuments[path]
    }

    var activeCanvasMutationDisabledReason: String? {
        guard let tab = workspace.activeTab, case .canvas = tab.item else {
            return nil
        }
        guard let document = activeCanvasRecoveryDocument else {
            return Self.canvasUnavailableMutationDisabledReason
        }
        return canvasMutationDisabledReason(for: document)
    }

    /// A card draft can outlive the native Canvas document when reconciliation
    /// proves the file absent. Keep the editor useful for selection/copy/close,
    /// but never re-enable Done merely because the unknown ledger cleared.
    static let canvasCardEditorUnavailableReason =
        "This canvas is no longer available. Copy your draft before closing the editor."

    var activeCanvasCardEditorDisabledReason: String? {
        guard canvasCardEditor != nil else { return nil }
        guard let document = activeCanvasRecoveryDocument else {
            return Self.canvasCardEditorUnavailableReason
        }
        if let reason = canvasMutationDisabledReason(for: document) {
            return reason
        }
        guard document.handle != nil else {
            return Self.canvasCardEditorUnavailableReason
        }
        return nil
    }

    /// Defensive admission for every Canvas writer. Disableable controls use
    /// `activeCanvasMutationDisabledReason`; direct/palette/keyboard callers
    /// still pass here and announce the exact same recovery instruction.
    @discardableResult
    func admitCanvasMutation(for document: CanvasDocument) -> Bool {
        guard let reason = canvasMutationDisabledReason(for: document) else {
            return true
        }
        postMutationAnnouncement(reason)
        return false
    }

    /// Prompt commits are synchronous Canvas actions. Keep the sheet and its
    /// AppState-owned draft unless the action actually appended one undo step;
    /// this covers native conflicts, invalid prompt input, missing handles, and
    /// the open-prompt → outcome-unknown quarantine race with one contract.
    @discardableResult
    func commitCanvasPromptMutation(_ action: () -> Void) -> Bool {
        guard let document = activeCanvasRecoveryDocument else {
            postMutationAnnouncement(Self.canvasUnavailableMutationDisabledReason)
            return false
        }
        guard admitCanvasMutation(for: document) else { return false }
        let undoCount = document.undoStack.count
        action()
        return document.undoStack.count == undoCount + 1
    }

    /// A picker is itself user input (query, highlight, target choice). Keep it
    /// mounted until the downstream mutation or prompt transition succeeds;
    /// quarantine and native failures must not discard that context.
    @discardableResult
    func commitCanvasCardPickerSelection(
        in document: CanvasDocument,
        _ action: () -> Void
    ) -> Bool {
        guard admitCanvasMutation(for: document) else { return false }
        let undoCount = document.undoStack.count
        let promptBefore = canvasPrompt
        action()
        let succeeded = document.undoStack.count == undoCount + 1
            || canvasPrompt != promptBefore
        if succeeded {
            canvasCardPicker = nil
        }
        return succeeded
    }

    /// Sheet-local recovery route: modal presentation obscures the window
    /// banner, so outcome-unknown card drafts need their own Check Again.
    @discardableResult
    func retryCanvasCardEditorReconciliation() -> Task<Void, Never>? {
        retryBatchTrashUnknownReconciliation()
    }

    /// Modal Canvas sheets obscure the window-level recovery banner. Surface
    /// the same action in-sheet without dismissing the prompt or picker.
    func canvasRecoveryActionLabel(for document: CanvasDocument) -> String? {
        if isBatchTrashPathQuarantined(document.path) {
            return BatchTrashCopy.checkAgainLabel
        }
        if document.hasPendingRetargetPreparation,
            !document.isRetargetPreparationInFlight
        {
            return "Retry"
        }
        return nil
    }

    func canvasRecoveryActionHint(for document: CanvasDocument) -> String? {
        if isBatchTrashPathQuarantined(document.path) {
            return BatchTrashCopy.checkAgainHint
        }
        if document.hasPendingRetargetPreparation,
            !document.isRetargetPreparationInFlight
        {
            return "Attempts to reopen the canvas at its current path."
        }
        return nil
    }

    @discardableResult
    func retryCanvasRecovery(for document: CanvasDocument) -> Task<Void, Never>? {
        if isBatchTrashPathQuarantined(document.path) {
            return retryBatchTrashUnknownReconciliation()
        }
        guard document.hasPendingRetargetPreparation,
            let session = currentSession,
            canvasDocuments[document.path] === document
        else { return nil }
        scheduleCanvasRetargetPreparationIfNeeded(
            document: document,
            path: document.path,
            session: session)
        return nativeDocumentRetargetTask
    }

    func dismissCanvasCardEditor() {
        canvasCardEditor = nil
    }

    func dismissCanvasPrompt() {
        canvasPrompt = nil
        canvasPromptDraft = ""
    }

    func presentCanvasPrompt(_ prompt: CanvasPrompt, draft: String = "") {
        canvasPromptDraft = draft
        canvasPrompt = prompt
    }

    /// The one mutation entry point every canvas verb uses: applies the
    /// action (one write, one journal entry), pushes the inverse onto
    /// the document's undo stack, clears redo. Errors surface through
    /// the funnel (conflicts assertively, t0 §5) and return false.
    @discardableResult
    func canvasApply(_ action: CanvasAction, to doc: CanvasDocument) -> Bool {
        guard admitCanvasMutation(for: doc) else { return false }
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
            canvasApplyObserverForTesting?(action)
            let result = try session.canvasApply(handle: handle, action: action)
            doc.undoStack.append((name: action.name, inverse: result.inverse))
            doc.redoStack = []
            doc.reloadAfterMutation(session: session)
            // #867: the stacks are plain vars (never @Published — views
            // don't render them), so the Undo/Redo menu titles need an
            // explicit pulse from this funnel.
            noteUndoStacksChanged()
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
        guard admitCanvasMutation(for: doc) else { return }
        guard canvasTransient == nil else {
            canvasAnnouncer.announce(
                .error("A move or resize is in progress. Return to place it or Escape to cancel first."))
            return
        }
        guard let session = currentSession, let handle = doc.handle else { return }
        guard let entry = doc.undoStack.popLast() else {
            canvasAnnouncer.announce(.status("Nothing to undo."))
            return
        }
        do {
            canvasApplyObserverForTesting?(entry.inverse)
            let result = try session.canvasApply(handle: handle, action: entry.inverse)
            doc.redoStack.append((name: entry.name, inverse: result.inverse))
            doc.reloadAfterMutation(session: session)
            noteUndoStacksChanged()  // #867 menu-title pulse
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
        guard admitCanvasMutation(for: doc) else { return }
        guard canvasTransient == nil else {
            canvasAnnouncer.announce(
                .error("A move or resize is in progress. Return to place it or Escape to cancel first."))
            return
        }
        guard let session = currentSession, let handle = doc.handle else { return }
        guard let entry = doc.redoStack.popLast() else {
            canvasAnnouncer.announce(.status("Nothing to redo."))
            return
        }
        do {
            canvasApplyObserverForTesting?(entry.inverse)
            let result = try session.canvasApply(handle: handle, action: entry.inverse)
            doc.undoStack.append((name: entry.name, inverse: result.inverse))
            doc.reloadAfterMutation(session: session)
            noteUndoStacksChanged()  // #867 menu-title pulse
            canvasAnnouncer.announce(.confirmation(CanvasAnnouncer.redidText(actionName: entry.name)))
        } catch {
            doc.redoStack.append(entry)
            canvasAnnouncer.announce(
                .error("Redo blocked: the canvas changed on disk. Reload it and try again."))
        }
    }

    /// The responder-chain seam (#372): ⌘Z drives the canvas stack when
    /// a canvas surface owns focus, the standard responder chain
    /// otherwise (NSTextView editors keep their NSUndoManager).
    var undoTargetsCanvas: Bool {
        guard activeCanvasDocument != nil else { return false }
        // #867 red-team (BROKEN): the card-editor / prompt / picker
        // sheets put their own editors in first-responder position
        // while the canvas TAB stays active — ⌘Z must drive the
        // sheet's responder chain (an NSTextView carries its own
        // NSUndoManager), and the Edit menu must advertise ITS verbs,
        // never the canvas stack's. These sheets are AppState
        // @Published, so setting/clearing one publishes → the bridge
        // re-renders `.commands` → the title, enablement, AND ⌘Z
        // routing flip together (never a title/action split), and the
        // gate is observable headless.
        //
        // The BROADER focus problem — a Settings field or other
        // auxiliary key window owning keyboard focus while a canvas
        // tab is active — is #372's route-by-active-tab-not-focus
        // behavior (byte-for-byte unchanged by this PR) and can't be
        // closed here without a PUBLISHED first-responder signal: a
        // `NSApp.keyWindow.firstResponder` read is unpublished, so
        // the render-time title would bake one undo domain while the
        // press-time action re-evaluates the other. Deferred to the
        // #372 focus-routing refinement; PR5 fixes only the
        // publishable modal-sheet case.
        if canvasCardEditor != nil || canvasPrompt != nil || canvasCardPicker != nil {
            return false
        }
        return true
    }
}
