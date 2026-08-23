// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

private enum CanvasConvertThreadProbe {
    nonisolated static func isMainThread() -> Bool {
        Thread.isMainThread
    }
}

private enum CanvasConvertToNoteOutcome: Sendable {
    case converted(CanvasApplyResult)
    case destinationExists
    case readFailed(String)
    case createFailed(String)
    case retargetFailed(String)

    var createdNote: Bool {
        switch self {
        case .converted, .retargetFailed:
            return true
        case .destinationExists, .readFailed, .createFailed:
            return false
        }
    }
}

/// Obsidian-parity authoring extras (Milestone T, #525) — all
/// keyboard-first: create-connected-card (the mind-mapping gesture),
/// duplicate (selection or marked set, one action), convert card →
/// vault note (U2-2 creation API), and `#heading` subpath open-to-
/// anchor. Edge-label editing shipped with #523; URL-card host titles
/// ship from the Rust model. **No live web embeds** — documented
/// divergence (t5 spec).
extension AppState {
    // MARK: Create connected card (⌃⌥⌘N)

    /// One command = one action: a new empty text card already
    /// connected FROM the selection, engine-placed (default below),
    /// landed in edit mode for immediate typing.
    func canvasCreateConnectedCard(direction: CanvasPlaceDirection = .below) {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc),
            let session = currentSession,
            let handle = doc.handle
        else { return }
        guard let origin = doc.selection.selected,
            let originRow = doc.outline.first(where: { $0.nodeId == origin }),
            let originNode = doc.scene.nodes.first(where: { $0.nodeId == origin })
        else {
            canvasAnnouncer.announce(.canvasStatus(note: .nothingSelected))
            return
        }
        let id = Self.newCanvasEntityID()
        do {
            let placement = try session.canvasPlaceNew(
                handle: handle, anchor: origin,
                width: 260, height: 140,
                directionHint: direction, exclude: [])
            // §W-G row C / CD-16: `canvas_auto_sides` is keyed by
            // RECTS, which is why this site could not have used an
            // id-keyed query — the card it connects to does not exist
            // in the model yet. The synthetic `CanvasSceneNode` that
            // used to be built here purely to satisfy the old
            // node-keyed helper is gone with it.
            let sides = canvasAutoSides(
                from: CanvasRect(
                    x: originNode.x, y: originNode.y,
                    width: originNode.width, height: originNode.height),
                to: CanvasRect(
                    x: placement.x, y: placement.y, width: 260, height: 140))
            let ok = canvasApply(
                CanvasAction(
                    name: "create connected card",
                    ops: [
                        .createNode(
                            id: id, content: .text(text: ""),
                            x: placement.x, y: placement.y,
                            width: 260, height: 140, color: nil),
                        .addEdge(
                            id: Self.newCanvasEntityID(),
                            fromNode: origin, fromSide: sides.from,
                            toNode: id, toSide: sides.to,
                            fromEnd: .none, toEnd: .arrow,
                            label: nil, color: nil),
                    ]),
                to: doc)
            guard ok else { return }
            canvasSelect(nodeId: id, in: doc, announce: false)
            canvasAnnouncer.announce(
                .canvasConnectedCardCreated(
                    relative: placement.relative, originTitle: originRow.title))
            // Lands in edit mode (the mind-mapping loop: create → type).
            canvasCardEditor = CanvasCardEditorRequest(
                nodeId: id, title: "Untitled", initialText: "")
        } catch {
            canvasAnnouncer.announce(
                .canvasActionFailed(
                    action: .createConnectedCard, detail: error.localizedDescription))
        }
    }

    /// Palette variant: choose the direction first.
    func canvasPromptConnectedDirection() {
        guard let doc = activeCanvasDocument, doc.selection.selected != nil else {
            canvasAnnouncer.announce(.canvasStatus(note: .nothingSelected))
            return
        }
        guard admitCanvasMutation(for: doc) else { return }
        presentCanvasPrompt(.connectedDirection)
    }

    // MARK: Duplicate (selection or marked set — ONE action)

    /// Duplicate the marked set (rigid unit) or the selected card.
    /// Groups expand to the members core's containment tree gives them
    /// (§W-G row D), so a duplicated frame keeps its cards. Engine
    /// set-placement preserves pairwise offsets; edges are not copied
    /// (cards duplicate, connections are authored intent).
    func canvasDuplicate() {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc),
            let session = currentSession,
            let handle = doc.handle
        else { return }
        let seed = canvasMovingSet(in: doc)
        guard !seed.isEmpty else {
            canvasAnnouncer.announce(.canvasStatus(note: .nothingSelected))
            return
        }
        let nodesById = Dictionary(
            uniqueKeysWithValues: doc.scene.nodes.map { ($0.nodeId, $0) })
        // §W-G row D: a picked group brings its members, and membership
        // is core's `GroupTree` (`canvas_children_of`, contract 0b-8)
        // rather than a centre-in-rect test written out again here.
        // Walked transitively, because a picked group's members include
        // the contents of the groups it contains. `children_of` answers
        // `[]` for a card, so the walk needs no kind test of its own.
        var members = Set(seed)
        var pending = seed
        while let id = pending.popLast() {
            let children =
                (try? session.canvasChildrenOf(handle: handle, groupId: id)) ?? []
            for child in children where !members.contains(child) {
                members.insert(child)
                pending.append(child)
            }
        }
        // Reading order, from the one projection (§W-G row F).
        let expanded = canvasInReadingOrder(Array(members), in: doc)
        do {
            let boxes = expanded.compactMap { id -> CanvasRect? in
                nodesById[id].map {
                    CanvasRect(x: $0.x, y: $0.y, width: $0.width, height: $0.height)
                }
            }
            let placement = try session.canvasPlaceSet(
                handle: handle, anchor: expanded.first, boxes: boxes,
                directionHint: nil, exclude: [])
            var ops: [CanvasOp] = []
            for (id, origin) in zip(expanded, placement.origins) {
                guard let node = nodesById[id] else { continue }
                if node.kind == "group" {
                    ops.append(
                        .createGroup(
                            id: Self.newCanvasEntityID(),
                            label: node.title,
                            x: origin.x, y: origin.y,
                            width: node.width, height: node.height,
                            color: node.color))
                } else {
                    let content: CanvasNodeContent
                    switch node.kind {
                    case "file", "image":
                        content = .file(
                            file: doc.target(of: id), subpath: node.subpath)
                    case "link":
                        content = .link(url: doc.target(of: id))
                    default:
                        let fetched = try? session.canvasNodeText(
                            handle: handle, nodeId: id)
                        content = .text(text: fetched ?? "")
                    }
                    ops.append(
                        .createNode(
                            id: Self.newCanvasEntityID(), content: content,
                            x: origin.x, y: origin.y,
                            width: node.width, height: node.height,
                            color: node.color))
                }
            }
            let single = expanded.count == 1
            let name =
                single
                ? "duplicate \"\(doc.outline.first { $0.nodeId == expanded[0] }?.title ?? "card")\""
                : "duplicate \(CountCopy.counted(expanded.count, "card", "cards"))"
            let ok = canvasApply(CanvasAction(name: name, ops: ops), to: doc)
            guard ok else { return }
            if single {
                let title =
                    doc.outline.first { $0.nodeId == expanded[0] }?.title ?? "card"
                canvasAnnouncer.announce(
                    .canvasCardPlaced(
                        verb: .duplicated, title: title,
                        relative: placement.relative))
            } else {
                canvasAnnouncer.announce(
                    .canvasBulkDuplicated(count: UInt32(clamping: expanded.count)))
            }
        } catch {
            canvasAnnouncer.announce(
                .canvasActionFailed(
                    action: .duplicate, detail: error.localizedDescription))
        }
    }

    // MARK: Convert card → note (U2-2 creation API)

    func canvasPromptConvertToNote() {
        guard let doc = activeCanvasDocument,
            let selected = doc.selection.selected,
            let row = doc.outline.first(where: { $0.nodeId == selected })
        else {
            canvasAnnouncer.announce(.canvasStatus(note: .nothingSelected))
            return
        }
        guard row.kind == "text" else {
            canvasAnnouncer.announce(.canvasStatus(note: .onlyTextCardsConvert))
            return
        }
        guard admitCanvasMutation(for: doc) else { return }
        guard admitStructuralMutationRequest() else { return }
        // Suggested path: the card's first-line title, slugged lightly.
        let stem = row.title
            .replacingOccurrences(of: "/", with: "-")
            .trimmingCharacters(in: .whitespacesAndNewlines)
        let suggested = (stem.isEmpty ? "Untitled" : stem) + ".md"
        presentCanvasPrompt(
            .convertToNote(nodeId: selected, suggested: suggested),
            draft: suggested)
    }

    /// Commit: create the note via the U2-2 save path (journaled file
    /// creation), then ONE canvas_apply retargets the card at it.
    /// Canvas undo restores the text card; the note file remains (the
    /// U2 convention — file ops have their own journal).
    @discardableResult
    func canvasConvertToNote(
        nodeId: String,
        path: String,
        nativeThreadObserver: (@Sendable (Bool) -> Void)? = nil
    ) -> Task<Void, Never>? {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc),
            let session = currentSession,
            let handle = doc.handle,
            let row = doc.outline.first(where: { $0.nodeId == nodeId })
        else { return nil }
        let cleanPath = path.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !cleanPath.isEmpty, cleanPath.lowercased().hasSuffix(".md") else {
            canvasAnnouncer.announce(.canvasBlocked(reason: .notePathMustEndInMd))
            return nil
        }
        // The `files` snapshot is a cheap early bail; it is NOT the
        // collision guard — a file present on disk but absent from the
        // (possibly stale/unscanned) list would slip through. The real
        // guard is the backend's create-if-absent contract below.
        guard !files.contains(where: { $0.path == cleanPath }) else {
            canvasAnnouncer.announce(
                .canvasBlocked(reason: .notePathExists(path: cleanPath, onDisk: false)))
            return nil
        }
        guard canvasTransient == nil else {
            canvasAnnouncer.announce(.canvasBlocked(reason: .modeBusy))
            return nil
        }
        guard admitStructuralMutationRequest() else { return nil }
        guard let recoveryReservation =
                admitStructuralRecoveryDestination(cleanPath),
            admitBatchTrashWrite(to: [cleanPath])
        else { return nil }

        let action = CanvasAction(
            name: "convert \"\(row.title)\" to note",
            ops: [
                .setNodeContent(
                    id: nodeId, content: .file(file: cleanPath, subpath: nil))
            ])
        let token = beginStructuralMutation(
            recoveryReservation: recoveryReservation)
        let refresher = structuralBatchRefreshRunner
        let task = Task { @MainActor [weak self] in
            let outcome = await Task.detached(priority: .userInitiated) {
                let text: String
                do {
                    nativeThreadObserver?(CanvasConvertThreadProbe.isMainThread())
                    guard
                        let fetched = try session.canvasNodeText(
                            handle: handle, nodeId: nodeId)
                    else {
                        return CanvasConvertToNoteOutcome.readFailed(
                            "The card text is unavailable.")
                    }
                    text = fetched
                } catch {
                    return CanvasConvertToNoteOutcome.readFailed(error.localizedDescription)
                }

                do {
                    nativeThreadObserver?(CanvasConvertThreadProbe.isMainThread())
                    // `expectedContentHash: ""` is the backend's exclusive
                    // create idiom. An on-disk collision fails instead of
                    // replacing content the file index hasn't seen yet.
                    _ = try session.saveText(
                        path: cleanPath, contents: text, expectedContentHash: "")
                } catch let error as VaultError {
                    switch error {
                    case .WriteConflict, .DestinationExists:
                        return CanvasConvertToNoteOutcome.destinationExists
                    default:
                        return CanvasConvertToNoteOutcome.createFailed(
                            error.localizedDescription)
                    }
                } catch {
                    return CanvasConvertToNoteOutcome.createFailed(
                        error.localizedDescription)
                }

                do {
                    nativeThreadObserver?(CanvasConvertThreadProbe.isMainThread())
                    return CanvasConvertToNoteOutcome.converted(
                        try session.canvasApply(handle: handle, action: action))
                } catch {
                    return CanvasConvertToNoteOutcome.retargetFailed(
                        error.localizedDescription)
                }
            }.value

            guard let self else { return }
            defer { self.endStructuralMutation(token) }
            guard self.ownsStructuralMutation(token, session: session) else { return }

            if outcome.createdNote {
                await refresher(self)
                guard self.ownsStructuralMutation(token, session: session) else { return }
                // The new note bypasses publishTreeMutation, so it is a
                // structural-history barrier even if the subsequent canvas
                // retarget failed after creation.
                self.clearStructuralUndoStacks()
            }

            switch outcome {
            case .converted(let result):
                doc.undoStack.append((name: action.name, inverse: result.inverse))
                doc.redoStack = []
                doc.reloadAfterMutation(session: session)
                self.noteUndoStacksChanged()
                self.canvasAnnouncer.announce(
                    .canvasConvertedToNote(path: cleanPath))
            case .destinationExists:
                self.canvasAnnouncer.announce(
                    .canvasBlocked(
                        reason: .notePathExists(path: cleanPath, onDisk: true)))
            case .readFailed(let message):
                self.canvasAnnouncer.announce(
                    .canvasBlocked(reason: .noteReadFailed(message: message)))
            case .createFailed(let message):
                self.canvasAnnouncer.announce(
                    .canvasBlocked(
                        reason: .noteCreateFailed(path: cleanPath, message: message)))
            case .retargetFailed(let message):
                self.canvasAnnouncer.announce(
                    .canvasBlocked(
                        reason: .noteRetargetFailed(path: cleanPath, message: message)))
            }
        }
        recordPendingStructuralTask(task)
        return task
    }

    // MARK: In-canvas filter (#373)

    /// ⌘F (canvas-scoped): reveal + focus the filter field.
    func canvasFocusFilter() {
        guard activeCanvasDocument != nil else { return }
        canvasFilterFocusToken += 1
    }

    /// Esc rung / palette: clear the filter and say what came back.
    func canvasClearFilter() {
        guard let doc = activeCanvasDocument else { return }
        guard doc.filterActive || !doc.filterText.isEmpty else { return }
        doc.filterText = ""
        canvasAnnouncer.announce(
            .canvasFilterCleared(total: UInt32(clamping: doc.outline.count)))
    }

    /// Debounced result count (t0 §1.5 — the announcer's filter
    /// category coalesces keystroke bursts).
    func canvasAnnounceFilterCount(doc: CanvasDocument) {
        guard doc.filterActive else { return }
        canvasAnnouncer.announce(
            .canvasFilterCount(
                matched: UInt32(clamping: doc.filteredOutline.count)))
    }

    // MARK: `#heading` subpath open-to-anchor (t5)

    /// Open a note and scroll to a heading once the load lands —
    /// the search-result activation pattern (await load, guard the
    /// selection didn't move, then route the anchor).
    func canvasOpenFileAtHeading(path: String, heading: String) {
        openFile(path, target: .currentTab)
        let pendingLoad = noteLoadTask
        let wanted = heading.trimmingCharacters(in: .whitespaces)
        Task { @MainActor [weak self] in
            if let pendingLoad { await pendingLoad.value }
            guard let self, self.selectedFilePath == path else { return }
            if let match = self.currentNoteHeadings.first(where: {
                $0.text.compare(wanted, options: [.caseInsensitive]) == .orderedSame
            }) {
                self.requestScrollToHeading(anchor: match.anchorId)
            } else {
                // Still the canvas activation's outcome — funnel rules
                // apply (DoD §H) even though the note is now frontmost.
                self.canvasAnnouncer.announce(
                    .canvasBlocked(
                        reason: .headingNotFound(
                            heading: wanted,
                            filename: (path as NSString).lastPathComponent)))
            }
        }
    }
}
