// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// #368 part 2 — the remaining umbrella verbs: the REAL text-card
/// editor (interview decision 7: the post-U3 editing component in a
/// sheet; **Esc commits** — the t0 M8 embedded-editor carve-out, never
/// M2 semantics), creation for every card kind (Add Note / Add Media /
/// Add Link Card, R5), Locate… repointing for missing file targets
/// (t0 §5), and Remove from Group (R22).
///
/// Every commit is ONE `canvas_apply` action → one announcement → one
/// undo step, through the t4 pipeline like all canvas mutations.

/// Test seam for the canvas media listing (plan 43 PR 12, #1324): awaited
/// off the main actor before each `list_files` page, with the page's index
/// and the listing's token.
typealias CanvasMediaListingPageGate = @Sendable (_ page: Int, _ cancel: CancelToken) async -> Void

/// Test seam for the canvas media listing: an error returned for a page
/// stands in for that `list_files` page failing in core.
typealias CanvasMediaListingPageFault = @Sendable (_ page: Int) -> Error?

/// What a canvas media listing answers its picker (plan 43 PR 12, #1324,
/// #1437): the complete listed set, or why the listing failed. A cancelled
/// listing answers nothing.
enum CanvasMediaListingOutcome {
    case listed([String])
    case failed(detail: String)
}

/// An open text-card edit session (sheet presented by the container).
struct CanvasCardEditorRequest: Identifiable, Equatable {
    let nodeId: String
    let title: String
    let initialText: String
    /// The basis the text was read at — one locked core read pairs
    /// them (W6-1 §E TE-0: `canvas_editor_seed`), so a commit can know
    /// the exact revision this draft grew from.
    let basis: String
    var inspectionOnly = false
    var id: String { nodeId }
}

extension AppState {
    // MARK: Text-card editor

    /// Open the editor for the selected card (palette / context menu).
    func canvasEditCard() {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc),
            let selected = doc.selection.selected
        else {
            canvasAnnouncer.announce(.canvasStatus(note: .nothingSelected))
            return
        }
        canvasEditCard(nodeId: selected)
    }

    /// Recovered text remains inspectable without requesting a writable editor
    /// seed. This request stays read-only even after a successful file repair.
    func canvasInspectCard(nodeId: String) {
        guard let doc = activeCanvasDocument else { return }
        guard doc.disposition == .recoveredReadOnly else {
            canvasEditCard(nodeId: nodeId)
            return
        }
        guard canvasCardEditor == nil,
            let row = doc.outline.first(where: { $0.nodeId == nodeId }), row.kind == "text",
            let context = canvasReadContext(for: doc)
        else { return }
        do {
            guard
                let text = try context.session.canvasNodeText(
                    handle: context.handle, nodeId: nodeId)
            else { return }
            canvasCardEditor = CanvasCardEditorRequest(
                nodeId: nodeId, title: row.title, initialText: text,
                basis: doc.contentHash ?? "", inspectionOnly: true)
        } catch {
            canvasAnnouncer.announce(.canvasBlocked(reason: .cardTextUnreadable))
        }
    }

    /// Open the editor for a specific text card (activation path).
    func canvasEditCard(nodeId: String) {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc),
            let row = doc.outline.first(where: { $0.nodeId == nodeId })
        else { return }
        guard row.kind == "text" else {
            canvasAnnouncer.announce(.canvasStatus(note: .notATextCard))
            return
        }
        guard let session = currentSession, let handle = doc.handle,
            let seed = try? session.canvasEditorSeed(handle: handle, nodeId: nodeId)
        else {
            canvasAnnouncer.announce(.canvasBlocked(reason: .cardTextUnreadable))
            return
        }
        canvasCardEditor = CanvasCardEditorRequest(
            nodeId: nodeId, title: row.title,
            initialText: seed.text, basis: seed.contentHash)
    }

    /// Commit path (Esc / Done / ⌘S): one `canvas_apply` when the text
    /// changed, silence-with-status when it didn't. NSTextView's own
    /// allowsUndo covers keystroke undo *inside* the session; after
    /// commit, ⌘Z on the canvas restores the previous text as one step.
    ///
    /// Data-safety (adversarial review): the editor sheet holds the
    /// ONLY copy of `newText`, so it is dismissed only on a successful
    /// apply or a true no-op — NOT when `canvasApply` fails (e.g. a
    /// WriteConflict: the .canvas changed on disk). On failure the
    /// sheet stays open with the draft so the user can retry/reload
    /// rather than silently losing the edit.
    @discardableResult
    func canvasCommitCardEdit(nodeId: String, newText: String) -> Bool {
        guard let editor = canvasCardEditor, editor.nodeId == nodeId else {
            return false
        }
        guard let doc = activeCanvasDocument else {
            announceCanvasRefusal(.canvas(.cardEditorUnavailable))
            return false
        }
        if let refusal = activeCanvasCardEditorRefusal {
            announceCanvasRefusal(refusal)
            return false
        }
        guard newText != editor.initialText else {
            canvasAnnouncer.announce(.canvasStatus(note: .noChanges))
            dismissCanvasCardEditor()
            return true
        }
        let ok = canvasApply(
            CanvasAction(
                name: "edit \"\(editor.title)\"",
                ops: [.setNodeContent(id: nodeId, content: .text(text: newText))]),
            to: doc)
        // Apply failed (the funnel already surfaced why) — keep the
        // editor open so the draft is not lost.
        guard ok else { return false }
        dismissCanvasCardEditor()
        canvasAnnouncer.announce(.canvasCardUpdated(title: editor.title))
        return true
    }

    // MARK: Creation, all card kinds (R5)

    /// Markdown notes in the vault (quick-open's list, minus canvases).
    private var canvasNotePaths: [String] {
        files.map(\.path).filter {
            let lower = $0.lowercased()
            return lower.hasSuffix(".md") || lower.hasSuffix(".markdown")
        }
    }

    // MARK: The media listing (plan 43 PR 12, #1324)

    /// Media files: everything in the vault that isn't a note/canvas.
    /// Listed fresh (the sidebar list is markdown+canvas only). Filter
    /// PER PAGE (Codoki #622): on a large vault, accumulating every
    /// path before filtering spikes peak memory for a set the UI
    /// truncates to 200 anyway.
    ///
    /// Runs off the main actor (locked 05 §4, principle 2) under the ONE
    /// token of its listing: a cancelled token ends it `.Cancelled` at its
    /// next page. Any other page failure ends it with that error, so a
    /// partial set is never presented as the vault's (contract 34 TG2-3 and
    /// IG2-20, Windows' arm; #1437 closes IG2-37's mac half).
    nonisolated static func listCanvasMediaPaths(
        session: VaultSession,
        cancel: CancelToken,
        pageLimit: UInt32,
        pageGate: CanvasMediaListingPageGate?,
        pageFault: CanvasMediaListingPageFault? = nil
    ) async throws -> [String] {
        var media: [String] = []
        var cursor: String? = nil
        var page = 0
        repeat {
            await pageGate?(page, cancel)
            if let fault = pageFault?(page) { throw fault }
            let listed = try session.listFiles(
                filter: .all, paging: Paging(cursor: cursor, limit: pageLimit),
                cancel: cancel)
            media.append(
                contentsOf: listed.items.map(\.path).filter {
                    let lower = $0.lowercased()
                    return !lower.hasSuffix(".md") && !lower.hasSuffix(".markdown")
                        && !lower.hasSuffix(".canvas")
                })
            cursor = listed.nextCursor
            page += 1
        } while cursor != nil
        return media
    }

    /// Present `loadingPrompt` — the picker on its loading row — and list the
    /// vault's media for it off the main actor, then hand `land` the outcome
    /// on the main actor: the complete list, or why a page failed. The listing is the picker's: one token, cancelled
    /// when the picker closes or is replaced (`canvasPrompt`'s observer),
    /// when a newer listing starts (here) and when the vault closes
    /// (`releaseAllCanvasDocuments`). A cancelled or superseded listing, or
    /// one whose canvas is no longer the active one, publishes nothing.
    private func startCanvasMediaListing(
        for document: CanvasDocument,
        presenting loadingPrompt: CanvasPrompt,
        land: @escaping (AppState, CanvasMediaListingOutcome) -> Void
    ) {
        cancelCanvasMediaListing()
        guard let session = currentSession else {
            land(self, .listed([]))
            return
        }
        canvasMediaListingGeneration &+= 1
        let generation = canvasMediaListingGeneration
        let cancel = CancelToken()
        canvasMediaListingCancelToken = cancel
        canvasMediaListingPromptID = loadingPrompt.id
        presentCanvasPrompt(loadingPrompt)
        let pageLimit = canvasMediaListingPageLimit
        let pageGate = canvasMediaListingPageGateForTesting
        let pageFault = canvasMediaListingPageFaultForTesting
        canvasMediaListingTask = Task { @MainActor [weak self, weak document] in
            let outcome: CanvasMediaListingOutcome?
            do {
                outcome = .listed(
                    try await withTaskCancellationHandler {
                        try await Task.detached(priority: .userInitiated) {
                            try await Self.listCanvasMediaPaths(
                                session: session, cancel: cancel,
                                pageLimit: pageLimit, pageGate: pageGate,
                                pageFault: pageFault)
                        }.value
                    } onCancel: {
                        cancel.cancel()
                    })
            } catch VaultError.Cancelled {
                outcome = nil
            } catch is CancellationError {
                outcome = nil
            } catch let error as VaultError {
                outcome = .failed(detail: Self.humanReadableVaultError(error))
            } catch {
                outcome = .failed(detail: error.localizedDescription)
            }
            guard let self, self.canvasMediaListingGeneration == generation else { return }
            self.canvasMediaListingTask = nil
            self.canvasMediaListingCancelToken = nil
            self.canvasMediaListingPromptID = nil
            guard let outcome, !cancel.isCancelled(), self.currentSession === session,
                let document, self.activeCanvasDocument === document,
                self.canvasPrompt?.id == loadingPrompt.id
            else {
                // The canvas this picker served is gone: take the loading
                // picker down with it rather than leave it waiting.
                if self.canvasPrompt?.id == loadingPrompt.id { self.dismissCanvasPrompt() }
                return
            }
            land(self, outcome)
        }
    }

    /// End the media listing in flight, if any: cancel its token and task
    /// and drop its answer.
    func cancelCanvasMediaListing() {
        canvasMediaListingGeneration &+= 1
        canvasMediaListingCancelToken?.cancel()
        canvasMediaListingCancelToken = nil
        canvasMediaListingTask?.cancel()
        canvasMediaListingTask = nil
        canvasMediaListingPromptID = nil
    }

    /// The vault is closing: end the listing, and with it the picker still
    /// waiting on it, so nothing is left behind.
    func endCanvasMediaListingForVaultTransition() {
        guard let listing = canvasMediaListingPromptID else { return }
        cancelCanvasMediaListing()
        if canvasPrompt?.id == listing { dismissCanvasPrompt() }
    }

    func canvasOpenAddNote() {
        guard let document = activeCanvasDocument,
            admitCanvasMutation(for: document)
        else { return }
        let notes = canvasNotePaths
        guard !notes.isEmpty else {
            canvasAnnouncer.announce(.canvasStatus(note: .noNotesInVault))
            return
        }
        presentCanvasPrompt(.addNote(files: notes))
    }

    /// The picker opens at once on its loading row while the media listing
    /// runs off the main actor (plan 43 PR 12, #1324); an empty vault still
    /// refuses, once the listing has answered.
    func canvasOpenAddMedia() {
        guard let document = activeCanvasDocument,
            admitCanvasMutation(for: document)
        else { return }
        startCanvasMediaListing(for: document, presenting: .addMedia(files: nil)) {
            state, outcome in
            switch outcome {
            case .failed(let detail):
                // #1437 (contract 34 IG2-37, Windows' TG2-3 arm): a failed
                // page presents nothing and says why.
                state.dismissCanvasPrompt()
                state.canvasAnnouncer.announce(
                    .canvasActionFailed(action: .create, detail: detail))
            case .listed(let media):
                guard !media.isEmpty else {
                    state.dismissCanvasPrompt()
                    state.canvasAnnouncer.announce(.canvasStatus(note: .noMediaInVault))
                    return
                }
                state.canvasPrompt = .addMedia(files: media)
            }
        }
    }

    func canvasOpenAddLink() {
        guard let document = activeCanvasDocument,
            admitCanvasMutation(for: document)
        else { return }
        presentCanvasPrompt(.addLink)
    }

    /// Create a file card (note or media) placed by the #517 engine
    /// adjacent to the selection, announced with the relation.
    func canvasAddFileCard(path: String) {
        canvasCreatePlaced(
            content: .file(file: path, subpath: nil),
            kind: "file",
            title: (path as NSString).lastPathComponent,
            actionName: "add file card")
    }

    /// Create a link card from a pasted/typed URL.
    func canvasAddLinkCard(url: String) {
        let trimmed = url.trimmingCharacters(in: .whitespacesAndNewlines)
        guard let parsed = URL(string: trimmed), parsed.scheme != nil else {
            canvasAnnouncer.announce(.canvasBlocked(reason: .notAUrl))
            return
        }
        canvasCreatePlaced(
            content: .link(url: trimmed),
            kind: "link",
            title: parsed.host ?? trimmed,
            actionName: "add link card")
    }

    /// Shared placed-creation path: engine placement (never UI math),
    /// selection follows, creation announced with the relative phrase.
    private func canvasCreatePlaced(
        content: CanvasNodeContent, kind: String, title: String, actionName: String
    ) {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc),
            let session = currentSession,
            let handle = doc.handle
        else { return }
        let id = canvasNewId()
        let geometry = Self.canvasGeometry
        do {
            let placement = try session.canvasPlaceNew(
                handle: handle,
                anchor: doc.selection.selected,
                width: geometry.defaultCardW, height: geometry.defaultCardH,
                directionHint: nil, exclude: [])
            let ok = canvasApply(
                CanvasAction(
                    name: actionName,
                    ops: [
                        .createNode(
                            id: id, content: content,
                            x: placement.x, y: placement.y,
                            width: geometry.defaultCardW, height: geometry.defaultCardH,
                            color: nil)
                    ]),
                to: doc)
            guard ok else { return }
            canvasSelect(nodeId: id, in: doc, announce: false)
            canvasAnnouncer.announce(
                .canvasCreated(
                    kindLabel: kind, title: title, relative: placement.relative))
        } catch {
            canvasAnnouncer.announce(
                .canvasActionFailed(
                    action: .create, detail: error.localizedDescription))
        }
    }

    // MARK: Locate… (t0 §5 — repoint a file card)

    /// Open the repoint picker for the selected file card. Offered on
    /// every file card (a "missing" test against the note list would
    /// false-positive on media targets); it's the same picker either way.
    func canvasOpenLocate() {
        guard let doc = activeCanvasDocument,
            let selected = doc.selection.selected,
            let row = doc.outline.first(where: { $0.nodeId == selected })
        else {
            canvasAnnouncer.announce(.canvasStatus(note: .nothingSelected))
            return
        }
        guard admitCanvasMutation(for: doc) else { return }
        guard row.kind == "file" || row.kind == "image" else {
            canvasAnnouncer.announce(.canvasStatus(note: .notAFileCard))
            return
        }
        // Notes come from the sidebar's list; media from the listing, which
        // runs off the main actor while the picker shows its loading row
        // (plan 43 PR 12, #1324).
        let notes = canvasNotePaths
        let title = row.title
        startCanvasMediaListing(
            for: doc, presenting: .locate(nodeId: selected, title: title, files: nil)
        ) { state, outcome in
            switch outcome {
            case .failed(let detail):
                // #1437, as for Add Media: Locate's verb arm is CanvasAction.
                state.dismissCanvasPrompt()
                state.canvasAnnouncer.announce(
                    .canvasActionFailed(action: .canvasAction, detail: detail))
            case .listed(let media):
                let candidates = notes + media
                guard !candidates.isEmpty else {
                    state.dismissCanvasPrompt()
                    state.canvasAnnouncer.announce(.canvasStatus(note: .noFilesToPointAt))
                    return
                }
                state.canvasPrompt = .locate(nodeId: selected, title: title, files: candidates)
            }
        }
    }

    /// Repoint a file card at a new vault path — one action, one undo.
    func canvasLocate(nodeId: String, path: String) {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc),
            let row = doc.outline.first(where: { $0.nodeId == nodeId })
        else { return }
        let ok = canvasApply(
            CanvasAction(
                name: "repoint \"\(row.title)\"",
                ops: [.setNodeContent(id: nodeId, content: .file(file: path, subpath: nil))]),
            to: doc)
        guard ok else { return }
        canvasAnnouncer.announce(
            .canvasCardRetargeted(title: row.title, path: path))
    }

    // MARK: Remove from Group (R22)

    /// The voice-friendly un-reparent: engine placement adjacent to the
    /// enclosing group's frame (outside it), zero coordinates.
    func canvasRemoveFromGroup() {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc),
            let session = currentSession,
            let handle = doc.handle,
            let selected = doc.selection.selected,
            let row = doc.outline.first(where: { $0.nodeId == selected }),
            let node = doc.scene.nodes.first(where: { $0.nodeId == selected })
        else {
            canvasAnnouncer.announce(.canvasStatus(note: .nothingSelected))
            return
        }
        guard !row.groupPath.isEmpty else {
            canvasAnnouncer.announce(
                .canvasStatus(note: .notInAGroup(title: row.title)))
            return
        }
        // §W-G row D: the enclosing group is core's `canvas_parent_of`
        // (contract 0b-8) — the same `GroupTree` the outline's depth
        // and group path already come from, so this verb and the rows
        // it moves can no longer disagree about who contains what. The
        // strict-centre / smallest-area filter that lived here is
        // gone, and with it mac's equal-area tie-break: core keeps the
        // LATER document order (CD-18), observable only for exactly
        // coincident group rects.
        // `do`/`catch`, not `try?`: SE-0230 flattens `try?` on an
        // optional-returning call, so `try?` here would report a THROWN
        // query as "not in a group" — a fact the query never returned.
        // Same collapse as the one codex found at exit-group, in a verb
        // where it was previously recorded as an unreachable exclusion;
        // removing it is cheaper than keeping the exclusion true.
        let parentId: String?
        do {
            parentId = try session.canvasParentOf(handle: handle, nodeId: selected)
        } catch {
            canvasAnnouncer.announce(.canvasStatus(note: .nothingSelected))
            return
        }
        guard let parentId,
            let parent = doc.scene.nodes.first(where: { $0.nodeId == parentId })
        else {
            canvasAnnouncer.announce(
                .canvasStatus(note: .notInAGroup(title: row.title)))
            return
        }
        do {
            let placement = try session.canvasPlaceNew(
                handle: handle, anchor: parent.nodeId,
                width: node.width, height: node.height,
                directionHint: nil, exclude: [selected])
            let ok = canvasApply(
                CanvasAction(
                    name: "remove \"\(row.title)\" from \"\(parent.title)\"",
                    ops: [
                        .updateNodeGeometry(
                            id: selected, x: placement.x, y: placement.y,
                            width: node.width, height: node.height)
                    ]),
                to: doc)
            guard ok else { return }
            canvasAnnouncer.announce(.canvasRemovedFromGroup(label: parent.title))
        } catch {
            canvasAnnouncer.announce(
                .canvasActionFailed(
                    action: .removeFromGroup, detail: error.localizedDescription))
        }
    }
}
