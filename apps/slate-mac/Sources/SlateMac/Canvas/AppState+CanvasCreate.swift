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

/// An open text-card edit session (sheet presented by the container).
struct CanvasCardEditorRequest: Identifiable, Equatable {
    let nodeId: String
    let title: String
    let initialText: String
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
            let fetched = try? session.canvasNodeText(handle: handle, nodeId: nodeId)
        else {
            canvasAnnouncer.announce(.canvasBlocked(reason: .cardTextUnreadable))
            return
        }
        canvasCardEditor = CanvasCardEditorRequest(
            nodeId: nodeId, title: row.title, initialText: fetched)
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

    /// Media files: everything in the vault that isn't a note/canvas.
    /// Listed fresh (the sidebar list is markdown+canvas only). Filter
    /// PER PAGE (Codoki #622): on a large vault, accumulating every
    /// path before filtering spikes peak memory for a set the UI
    /// truncates to 200 anyway.
    private var canvasMediaPaths: [String] {
        guard let session = currentSession else { return [] }
        var media: [String] = []
        var cursor: String? = nil
        repeat {
            guard
                let page = try? session.listFiles(
                    filter: .all, paging: Paging(cursor: cursor, limit: 1_000))
            else { break }
            media.append(
                contentsOf: page.items.map(\.path).filter {
                    let lower = $0.lowercased()
                    return !lower.hasSuffix(".md") && !lower.hasSuffix(".markdown")
                        && !lower.hasSuffix(".canvas")
                })
            cursor = page.nextCursor
        } while cursor != nil
        return media
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

    func canvasOpenAddMedia() {
        guard let document = activeCanvasDocument,
            admitCanvasMutation(for: document)
        else { return }
        let media = canvasMediaPaths
        guard !media.isEmpty else {
            canvasAnnouncer.announce(.canvasStatus(note: .noMediaInVault))
            return
        }
        presentCanvasPrompt(.addMedia(files: media))
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
        var candidates = canvasNotePaths
        candidates.append(contentsOf: canvasMediaPaths)
        guard !candidates.isEmpty else {
            canvasAnnouncer.announce(.canvasStatus(note: .noFilesToPointAt))
            return
        }
        presentCanvasPrompt(
            .locate(nodeId: selected, title: row.title, files: candidates))
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
