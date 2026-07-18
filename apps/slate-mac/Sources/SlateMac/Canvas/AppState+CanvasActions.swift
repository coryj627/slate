// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Canvas authoring verbs (Milestone T, #368) — every action is a
/// `CommandSection.canvas` registry command (rule R1), routes through
/// the one mutation pipeline (`canvasApply`: one write, one journal
/// entry, one undo step), places through the backend engine (#517 —
/// never UI math), and announces through the #518 funnel with the
/// t0 §1.3 grammar.
/// A pending text-input prompt for a canvas verb (M6: sheets are the
/// visible-control path; Voice Control and Switch Control drive them).
enum CanvasPrompt: Identifiable, Equatable {
    case newGroup
    case renameGroup(current: String)
    case moveIntoGroup(groups: [(id: String, title: String)])
    case setColor
    /// #523 optional label step after a Connect To… pick.
    case connectLabel(targetId: String, targetTitle: String)
    /// #523: choose one of the selected card's connections to act on.
    case pickConnection(choices: [(edgeId: String, label: String)], toDelete: Bool)
    /// #523: label + direction editor for one connection.
    case editConnection(edgeId: String, currentLabel: String)
    /// #524: the focusable marks list (Unmark + Jump per row).
    case marksList
    /// #524: label prompt for Group Marked Cards.
    case groupMarked
    /// #368 R5: vault-note picker for Add Note to Canvas….
    case addNote(files: [String])
    /// #368 R5: vault-media picker for Add Media….
    case addMedia(files: [String])
    /// #368 R5: URL prompt for Add Link Card.
    case addLink
    /// #368 t0 §5: repoint a file card at a new vault path.
    case locate(nodeId: String, title: String, files: [String])
    /// #525: direction chooser for Create Connected Card.
    case connectedDirection
    /// #525: path prompt for Convert Card to Note.
    case convertToNote(nodeId: String, suggested: String)

    var id: String {
        switch self {
        case .newGroup: return "newGroup"
        case .renameGroup: return "renameGroup"
        case .moveIntoGroup: return "moveIntoGroup"
        case .setColor: return "setColor"
        case .connectLabel: return "connectLabel"
        case .pickConnection: return "pickConnection"
        case .editConnection: return "editConnection"
        case .marksList: return "marksList"
        case .groupMarked: return "groupMarked"
        case .addNote: return "addNote"
        case .addMedia: return "addMedia"
        case .addLink: return "addLink"
        case .locate: return "locate"
        case .connectedDirection: return "connectedDirection"
        case .convertToNote: return "convertToNote"
        }
    }

    static func == (lhs: CanvasPrompt, rhs: CanvasPrompt) -> Bool { lhs.id == rhs.id }
}

extension AppState {
    /// Stable, collision-free node/edge ids (JSON Canvas convention:
    /// 16 hex chars).
    static func newCanvasEntityID() -> String {
        String(UUID().uuidString.replacingOccurrences(of: "-", with: "").prefix(16))
            .lowercased()
    }

    /// New Card (⌥⌘N): text card auto-placed adjacent to the selection
    /// (interview decision 1), announced relatively, selected, and
    /// landed in edit mode (G22) via the #368 card editor.
    func canvasNewCard() {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc),
            let session = currentSession,
            let handle = doc.handle
        else { return }
        let id = Self.newCanvasEntityID()
        do {
            let placement = try session.canvasPlaceNew(
                handle: handle,
                anchor: doc.selection.selected,
                width: 260, height: 140,
                directionHint: nil, exclude: [])
            let ok = canvasApply(
                CanvasAction(
                    name: "create card",
                    ops: [
                        .createNode(
                            id: id, content: .text(text: ""),
                            x: placement.x, y: placement.y,
                            width: 260, height: 140, color: nil)
                    ]),
                to: doc)
            guard ok else { return }
            canvasSelect(nodeId: id, in: doc, announce: false)
            canvasAnnouncer.announce(
                .confirmation(
                    CanvasAnnouncer.createdText(
                        card: CanvasCardRef(kind: "text", title: "Untitled"),
                        relative: placement.relative)))
            // G22: a new text card lands in edit mode.
            canvasCardEditor = CanvasCardEditorRequest(
                nodeId: id, title: "Untitled", initialText: "")
        } catch {
            canvasAnnouncer.announce(.error("New card failed: \(error.localizedDescription)"))
        }
    }

    /// New Group: prompts ride the UI (container sheet); this is the
    /// commit path. Placed via the engine like any creation.
    func canvasNewGroup(label: String) {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc),
            let session = currentSession,
            let handle = doc.handle
        else { return }
        let id = Self.newCanvasEntityID()
        do {
            let placement = try session.canvasPlaceNew(
                handle: handle,
                anchor: doc.selection.selected,
                width: 400, height: 300,
                directionHint: nil, exclude: [])
            let ok = canvasApply(
                CanvasAction(
                    name: "create group",
                    ops: [
                        .createGroup(
                            id: id, label: label.isEmpty ? nil : label,
                            x: placement.x, y: placement.y,
                            width: 400, height: 300, color: nil)
                    ]),
                to: doc)
            guard ok else { return }
            canvasSelect(nodeId: id, in: doc, announce: false)
            canvasAnnouncer.announce(
                .confirmation(
                    "Created group \"\(label.isEmpty ? "Untitled" : label)\" "
                        + CanvasAnnouncer.relativePhrase(placement.relative)))
        } catch {
            canvasAnnouncer.announce(.error("New group failed: \(error.localizedDescription)"))
        }
    }

    /// New Canvas (file-level, CommandSection.file): creates
    /// `name.canvas` beside the tree selection via the U2-2 create API
    /// and opens it — closes the "can't start from empty" gap (G22).
    @discardableResult
    func canvasNewCanvasFile() -> Task<Void, Never>? {
        guard let session = currentSession else { return nil }
        guard admitStructuralMutationRequest() else { return nil }
        let candidatePaths = (0..<200).map { attempt in
            attempt == 0
                ? "Untitled Canvas.canvas"
                : "Untitled Canvas \(attempt + 1).canvas"
        }
        let writableCandidatePaths = candidatePaths.filter {
            batchTrashPathCapability(for: $0) == .writable
        }
        guard !writableCandidatePaths.isEmpty else {
            _ = admitBatchTrashWrite(to: candidatePaths)
            return nil
        }

        let token = beginStructuralMutation()
        let refresher = structuralBatchRefreshRunner
        let nativeObserver = canvasNewFileNativeExecutionObserverForTesting
        let preloader = canvasNewFilePreloadRunner
        let task = Task { @MainActor [weak self] in
            guard let self else { return }
            defer { self.endStructuralMutation(token) }
            var createdName: String?
            // Each exact candidate is admitted and reserved on the main actor
            // before its exclusive native create. If a raced physical file
            // occupies that candidate, the next candidate gets its own fresh
            // admission/reservation; user-owned recovery is never treated as
            // a suffix collision and silently skipped.
            for name in writableCandidatePaths {
                guard !Task.isCancelled,
                    self.ownsStructuralMutation(token, session: session)
                else { return }
                guard let recoveryReservation =
                        self.admitStructuralRecoveryDestination(name),
                    self.admitBatchTrashWrite(to: [name]),
                    self.installStructuralRecoveryReservation(
                        recoveryReservation, token: token)
                else { return }

                let create: Result<Void, VaultError> = await Task.detached(
                    priority: .userInitiated
                ) {
                    do {
                        nativeObserver?(
                            CanvasNewFileNativeExecutionEvent(
                                phase: .create,
                                ranOnMainThread: CanvasNewFileThreadProbe.isMainThread()))
                        _ = try session.createExclusive(path: name, content: "{}\n")
                        return .success(())
                    } catch let error as VaultError {
                        return .failure(error)
                    } catch {
                        return .failure(.Io(message: error.localizedDescription))
                    }
                }.value
                guard !Task.isCancelled,
                    self.ownsStructuralMutation(token, session: session)
                else { return }

                switch create {
                case .success:
                    createdName = name
                case .failure(.DestinationExists):
                    continue
                case .failure(let error):
                    self.canvasAnnouncer.announce(
                        .error("New canvas failed: \(error.localizedDescription)"))
                    return
                }
                break
            }

            guard let name = createdName else {
                let error = VaultError.Io(
                    message: "could not find a free canvas name after 200 attempts")
                self.canvasAnnouncer.announce(
                    .error("New canvas failed: \(error.localizedDescription)"))
                return
            }

            // Reserve (and reuse) the per-path object before the slower
            // native open/outline/table/scene preparation begins. An
            // existing missing-file tab can be activated throughout that
            // suspension without falling back to main-actor native work.
            let document = self.canvasDocument(for: name)
            let replacedHandle = document.beginPreparedReplacement()
            if let replacedHandle {
                await Task.detached(priority: .utility) {
                    CanvasPreparedLoader.closeReplaced(
                        handle: replacedHandle,
                        session: session,
                        observer: nativeObserver)
                }.value
            }
            guard !Task.isCancelled,
                self.ownsStructuralMutation(token, session: session)
            else {
                self.abandonCanvasPreparedReplacement(
                    document, path: name, session: session)
                return
            }

            let prepared = await Task.detached(priority: .userInitiated) {
                preloader(session, name, nativeObserver)
            }.value
            guard !Task.isCancelled,
                self.ownsStructuralMutation(token, session: session)
            else {
                await Task.detached(priority: .utility) {
                    CanvasPreparedLoader.release(
                        prepared,
                        session: session,
                        observer: nativeObserver)
                }.value
                self.abandonCanvasPreparedReplacement(
                    document, path: name, session: session)
                return
            }

            await refresher(self)
            guard !Task.isCancelled,
                self.ownsStructuralMutation(token, session: session)
            else {
                await Task.detached(priority: .utility) {
                    CanvasPreparedLoader.release(
                        prepared,
                        session: session,
                        observer: nativeObserver)
                }.value
                self.abandonCanvasPreparedReplacement(
                    document, path: name, session: session)
                return
            }
            // #871 Codex round 2: a non-undoable structural create that
            // bypasses `publishTreeMutation` — clear the structural undo
            // history (barrier) so no stale inverse targets this new path.
            self.clearStructuralUndoStacks()
            self.dropCanvasModeState(for: name)
            document.applyPreparedLoad(prepared)
            // New documents get their own tab. Replacing the current tab
            // would destroy the only owner of an unsaved Markdown buffer;
            // it could also synchronously release a native editor object.
            self.openFile(
                name,
                target: .newTab,
                advancesSidebarSelectionRevision: false)
            self.canvasAnnouncer.announce(
                .confirmation(
                    "Created canvas \"\((name as NSString).deletingPathExtension)\"."))
        }
        recordPendingStructuralTask(task)
        return task
    }

    /// Delete the selected card/group. Group delete keeps children
    /// (they're contained geometrically); the destructive confirmation
    /// carries the undo hint at standard+ verbosity (t0 §1.3).
    func canvasDeleteSelection() {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc),
            let selected = doc.selection.selected,
            let row = doc.outline.first(where: { $0.nodeId == selected })
        else { return }
        let ref = CanvasCardRef(kind: row.kind, title: row.title)
        let op: CanvasOp =
            row.kind == "group" ? .ungroup(id: selected) : .deleteNode(id: selected)
        let name = row.kind == "group" ? "ungroup \"\(row.title)\"" : "delete \"\(row.title)\""
        let ok = canvasApply(CanvasAction(name: name, ops: [op]), to: doc)
        guard ok else { return }
        doc.selection.selected = nil
        canvasAnnouncer.announce(
            .destructiveConfirmation(
                row.kind == "group"
                    ? "Ungrouped \(ref.phrase) — cards kept"
                    : "Deleted \(ref.phrase)"))
    }

    /// Set the selected card's color (named preset or nil to clear),
    /// announced with the color NAME (t0 §1.1; #370 verifies contrast).
    func canvasSetColor(preset: Int?) {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc),
            let selected = doc.selection.selected,
            let row = doc.outline.first(where: { $0.nodeId == selected })
        else { return }
        let names = [1: "red", 2: "orange", 3: "yellow", 4: "green", 5: "cyan", 6: "purple"]
        let name = preset.flatMap { names[$0] } ?? "no color"
        let ok = canvasApply(
            CanvasAction(
                name: "set color of \"\(row.title)\"",
                ops: [.setNodeColor(id: selected, color: preset.map(String.init))]),
            to: doc)
        guard ok else { return }
        canvasAnnouncer.announce(
            .confirmation("Set \"\(row.title)\" to \(name)."))
    }

    /// Rename the selected group's label — the skeleton of the reading
    /// order (t4).
    func canvasRenameGroup(to label: String) {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc),
            let selected = doc.selection.selected,
            let row = doc.outline.first(where: { $0.nodeId == selected }),
            row.kind == "group"
        else {
            canvasAnnouncer.announce(.status("Not a group."))
            return
        }
        let ok = canvasApply(
            CanvasAction(
                name: "rename group \"\(row.title)\"",
                ops: [.renameGroup(id: selected, label: label.isEmpty ? nil : label)]),
            to: doc)
        guard ok else { return }
        canvasAnnouncer.announce(
            .confirmation("Renamed group to \"\(label.isEmpty ? "Untitled" : label)\"."))
    }

    // MARK: Prompt openers (the container renders the sheets)

    func canvasPromptNewGroup() {
        guard let document = activeCanvasDocument,
            admitCanvasMutation(for: document)
        else { return }
        presentCanvasPrompt(.newGroup)
    }

    func canvasPromptRenameGroup() {
        guard let doc = activeCanvasDocument,
            let selected = doc.selection.selected,
            let row = doc.outline.first(where: { $0.nodeId == selected }),
            row.kind == "group"
        else {
            canvasAnnouncer.announce(.status("Not a group."))
            return
        }
        guard admitCanvasMutation(for: doc) else { return }
        presentCanvasPrompt(.renameGroup(current: row.title), draft: row.title)
    }

    func canvasPromptMoveIntoGroup() {
        guard let doc = activeCanvasDocument,
            doc.selection.selected != nil
        else {
            canvasAnnouncer.announce(.status("Nothing selected."))
            return
        }
        let groups = doc.outline.filter { $0.kind == "group" }
            .map { (id: $0.nodeId, title: $0.title) }
        guard !groups.isEmpty else {
            canvasAnnouncer.announce(.status("This canvas has no groups."))
            return
        }
        guard admitCanvasMutation(for: doc) else { return }
        presentCanvasPrompt(.moveIntoGroup(groups: groups))
    }

    func canvasPromptSetColor() {
        guard let doc = activeCanvasDocument, doc.selection.selected != nil else {
            canvasAnnouncer.announce(.status("Nothing selected."))
            return
        }
        guard admitCanvasMutation(for: doc) else { return }
        presentCanvasPrompt(.setColor)
    }

    /// Move the selected card into a group by name — the voice-friendly
    /// reparent (R22): zero coordinates for the user, engine placement
    /// inside the target's bounds.
    func canvasMoveIntoGroup(groupId: String) {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc),
            let session = currentSession,
            let handle = doc.handle,
            let selected = doc.selection.selected,
            let row = doc.outline.first(where: { $0.nodeId == selected }),
            let group = doc.scene.nodes.first(where: { $0.nodeId == groupId }),
            let node = doc.scene.nodes.first(where: { $0.nodeId == selected })
        else { return }
        do {
            // Place inside the group's bounds: anchor on the group's
            // first child if any, else land at the group's padded
            // top-left (still engine-checked for overlap).
            let firstChild = doc.outline.first {
                $0.groupPath.last == group.title && $0.nodeId != selected
            }
            let target: (x: Double, y: Double)
            if let firstChild {
                let placement = try session.canvasPlaceNew(
                    handle: handle, anchor: firstChild.nodeId,
                    width: node.width, height: node.height,
                    directionHint: nil, exclude: [selected])
                target = (placement.x, placement.y)
            } else {
                let overlaps = try session.canvasCheckOverlap(
                    handle: handle,
                    rect: CanvasRect(
                        x: group.x + 20, y: group.y + 40,
                        width: node.width, height: node.height),
                    exclude: [selected])
                guard overlaps.isEmpty else {
                    canvasAnnouncer.announce(.error("No free space inside \"\(group.title)\"."))
                    return
                }
                target = (group.x + 20, group.y + 40)
            }
            let ok = canvasApply(
                CanvasAction(
                    name: "move \"\(row.title)\" into \"\(group.title)\"",
                    ops: [
                        .updateNodeGeometry(
                            id: selected, x: target.x, y: target.y,
                            width: node.width, height: node.height)
                    ]),
                to: doc)
            guard ok else { return }
            canvasAnnouncer.announce(.confirmation("Moved into group \"\(group.title)\"."))
        } catch {
            canvasAnnouncer.announce(.error("Move failed: \(error.localizedDescription)"))
        }
    }
}


/// Structural placement commands (#522): spatial arrangement with zero
/// coordinates — pick a reference card, the engine computes the slot
/// (never UI math), the announcement names the relation.
extension AppState {
    func canvasOpenCardPicker(_ purpose: CanvasCardPickerPurpose) {
        guard let doc = activeCanvasDocument else { return }
        guard admitCanvasMutation(for: doc) else { return }
        guard doc.selection.selected != nil else {
            canvasAnnouncer.announce(.status("Nothing selected."))
            return
        }
        canvasCardPicker = CanvasCardPickerRequest(purpose: purpose)
    }

    /// The ids that move for a structural placement: the marked set
    /// when marks exist (rigid unit, #524 semantics), else the
    /// selected card.
    func canvasMovingSet(in doc: CanvasDocument) -> [String] {
        if !doc.selection.marked.isEmpty {
            // Reading order keeps the op list deterministic.
            return doc.outline.map(\.nodeId).filter { doc.selection.marked.contains($0) }
        }
        return doc.selection.selected.map { [$0] } ?? []
    }

    /// "Place ⟨direction⟩ ⟨target⟩": engine placement with the moving
    /// card/set excluded from collision, one action, one undo, one
    /// announcement.
    func canvasPlaceRelative(target: String, direction: CanvasPlaceDirection) {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc),
            let session = currentSession,
            let handle = doc.handle
        else { return }
        let moving = canvasMovingSet(in: doc)
        guard !moving.isEmpty, !moving.contains(target) else {
            canvasAnnouncer.announce(.status("Pick a card outside the moving set."))
            return
        }
        let nodesById = Dictionary(
            uniqueKeysWithValues: doc.scene.nodes.map { ($0.nodeId, $0) })
        do {
            if moving.count == 1, let id = moving.first, let node = nodesById[id] {
                let placement = try session.canvasPlaceNew(
                    handle: handle, anchor: target,
                    width: node.width, height: node.height,
                    directionHint: direction, exclude: moving)
                let row = doc.outline.first { $0.nodeId == id }
                let ok = canvasApply(
                    CanvasAction(
                        name: "move \"\(row?.title ?? id)\"",
                        ops: [
                            .updateNodeGeometry(
                                id: id, x: placement.x, y: placement.y,
                                width: node.width, height: node.height)
                        ]),
                    to: doc)
                guard ok else { return }
                canvasAnnouncer.announce(
                    .confirmation(
                        "Moved \"\(row?.title ?? id)\" "
                            + CanvasAnnouncer.relativePhrase(placement.relative) + "."))
            } else {
                // Rigid unit: pairwise offsets preserved by the engine.
                let boxes = moving.compactMap { id -> CanvasRect? in
                    nodesById[id].map {
                        CanvasRect(x: $0.x, y: $0.y, width: $0.width, height: $0.height)
                    }
                }
                let placement = try session.canvasPlaceSet(
                    handle: handle, anchor: target, boxes: boxes,
                    directionHint: direction, exclude: moving)
                var ops: [CanvasOp] = []
                for (id, origin) in zip(moving, placement.origins) {
                    guard let node = nodesById[id] else { continue }
                    ops.append(
                        .updateNodeGeometry(
                            id: id, x: origin.x, y: origin.y,
                            width: node.width, height: node.height))
                }
                let ok = canvasApply(
                    CanvasAction(name: "move \(moving.count) cards", ops: ops), to: doc)
                guard ok else { return }
                canvasAnnouncer.announce(
                    .bulk(
                        "Moved \(moving.count) cards "
                            + CanvasAnnouncer.relativePhrase(placement.relative) + "."))
            }
        } catch {
            canvasAnnouncer.announce(.error("Placement failed: \(error.localizedDescription)"))
        }
    }

    /// "Align with ⟨target⟩": top edges align (same reading row); the
    /// engine's overlap check gates it — a collision is announced,
    /// never silently stacked (G20 spirit).
    func canvasAlignWith(target: String) {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc),
            let session = currentSession,
            let handle = doc.handle,
            let selected = doc.selection.selected,
            let node = doc.scene.nodes.first(where: { $0.nodeId == selected }),
            let targetNode = doc.scene.nodes.first(where: { $0.nodeId == target })
        else { return }
        do {
            let overlaps = try session.canvasCheckOverlap(
                handle: handle,
                rect: CanvasRect(
                    x: node.x, y: targetNode.y, width: node.width, height: node.height),
                exclude: [selected])
            guard overlaps.isEmpty else {
                canvasAnnouncer.announce(
                    .error("Aligning would overlap another card — not moved."))
                return
            }
            let row = doc.outline.first { $0.nodeId == selected }
            let targetRow = doc.outline.first { $0.nodeId == target }
            let ok = canvasApply(
                CanvasAction(
                    name: "align \"\(row?.title ?? selected)\"",
                    ops: [
                        .updateNodeGeometry(
                            id: selected, x: node.x, y: targetNode.y,
                            width: node.width, height: node.height)
                    ]),
                to: doc)
            guard ok else { return }
            canvasAnnouncer.announce(
                .confirmation(
                    "Aligned \"\(row?.title ?? selected)\" with \"\(targetRow?.title ?? target)\"."
                ))
        } catch {
            canvasAnnouncer.announce(.error("Align failed: \(error.localizedDescription)"))
        }
    }

    /// Route a completed pick to its verb.
    func canvasHandleCardPick(_ purpose: CanvasCardPickerPurpose, target: String) {
        switch purpose {
        case .placeBelow: canvasPlaceRelative(target: target, direction: .below)
        case .placeRightOf: canvasPlaceRelative(target: target, direction: .rightOf)
        case .placeAbove: canvasPlaceRelative(target: target, direction: .above)
        case .placeLeftOf: canvasPlaceRelative(target: target, direction: .leftOf)
        case .alignWith: canvasAlignWith(target: target)
        case .connectTo:
            // Optional label step (#523) before the edge commits.
            guard let doc = activeCanvasDocument,
                let row = doc.outline.first(where: { $0.nodeId == target })
            else { return }
            guard admitCanvasMutation(for: doc) else { return }
            presentCanvasPrompt(
                .connectLabel(targetId: target, targetTitle: row.title))
        }
    }

    /// Palette entries for existing connections (#523).
    func canvasPromptDeleteConnection() {
        guard let document = activeCanvasDocument,
            admitCanvasMutation(for: document)
        else { return }
        let choices = canvasConnectionChoices()
        guard !choices.isEmpty else {
            canvasAnnouncer.announce(.status("The selected card has no connections."))
            return
        }
        if choices.count == 1 {
            canvasDeleteConnection(edgeId: choices[0].edgeId)
        } else {
            presentCanvasPrompt(.pickConnection(choices: choices, toDelete: true))
        }
    }

    func canvasPromptEditConnection() {
        guard let document = activeCanvasDocument,
            admitCanvasMutation(for: document)
        else { return }
        let choices = canvasConnectionChoices()
        guard !choices.isEmpty else {
            canvasAnnouncer.announce(.status("The selected card has no connections."))
            return
        }
        if choices.count == 1 {
            canvasOpenConnectionEditor(edgeId: choices[0].edgeId)
        } else {
            presentCanvasPrompt(.pickConnection(choices: choices, toDelete: false))
        }
    }

    func canvasOpenConnectionEditor(edgeId: String) {
        guard let doc = activeCanvasDocument,
            let edge = doc.scene.edges.first(where: { $0.edgeId == edgeId })
        else { return }
        guard admitCanvasMutation(for: doc) else { return }
        presentCanvasPrompt(
            .editConnection(edgeId: edgeId, currentLabel: edge.label ?? ""),
            draft: edge.label ?? "")
    }
}


/// Mark-then-act multi-select (Milestone T, #524 — interview decision
/// 4: no shift-range selection). `CanvasSelection.marked` is the store
/// (per document, shared across panes, cleared when the last tab
/// closes); arrows move selection and NEVER mutate marks. Bulk actions
/// batch into ONE CanvasAction — one write, one undo, one summary.
extension AppState {
    /// ⌃⌘M on whichever surface has focus.
    func canvasToggleMark() {
        guard let doc = activeCanvasDocument,
            let selected = doc.selection.selected,
            let row = doc.outline.first(where: { $0.nodeId == selected })
        else {
            canvasAnnouncer.announce(.status("Nothing selected."))
            return
        }
        if doc.selection.marked.contains(selected) {
            doc.selection.marked.remove(selected)
            canvasAnnouncer.announce(
                .status("Unmarked \"\(row.title)\". \(doc.selection.marked.count) marked."))
        } else {
            doc.selection.marked.insert(selected)
            canvasAnnouncer.announce(
                .status("Marked \"\(row.title)\". \(doc.selection.marked.count) marked."))
        }
    }

    func canvasClearMarks() {
        guard let doc = activeCanvasDocument else { return }
        let count = doc.selection.marked.count
        doc.selection.marked = []
        canvasAnnouncer.announce(
            .status(count == 0 ? "No marks." : "Cleared \(count) marks."))
    }

    /// The marks list (t0 §3: the pull-based counterpart to mark
    /// announcements) — a focusable panel with Unmark + Jump per row.
    func canvasShowMarksList() {
        guard let doc = activeCanvasDocument else { return }
        guard !doc.selection.marked.isEmpty else {
            canvasAnnouncer.announce(.status("No marks."))
            return
        }
        presentCanvasPrompt(.marksList)
    }

    /// Marked ids in reading order (deterministic everywhere).
    func canvasMarkedInOrder(_ doc: CanvasDocument) -> [String] {
        doc.outline.map(\.nodeId).filter { doc.selection.marked.contains($0) }
    }

    /// Bulk delete: one action, one undo, one summary.
    func canvasDeleteMarked() {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc)
        else { return }
        let marked = canvasMarkedInOrder(doc)
        guard !marked.isEmpty else {
            canvasAnnouncer.announce(.status("No marks."))
            return
        }
        let ops = marked.map { CanvasOp.deleteNode(id: $0) }
        let ok = canvasApply(
            CanvasAction(name: "delete \(marked.count) cards", ops: ops), to: doc)
        guard ok else { return }
        doc.selection.marked = []
        if doc.selection.selected.map(marked.contains) == true {
            doc.selection.selected = nil
        }
        canvasAnnouncer.announce(
            .destructiveConfirmation("Deleted \(marked.count) cards"))
    }

    /// Bulk color: one action, one summary.
    func canvasColorMarked(preset: Int?) {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc)
        else { return }
        let marked = canvasMarkedInOrder(doc)
        guard !marked.isEmpty else {
            canvasAnnouncer.announce(.status("No marks."))
            return
        }
        let names = [1: "red", 2: "orange", 3: "yellow", 4: "green", 5: "cyan", 6: "purple"]
        let name = preset.flatMap { names[$0] } ?? "no color"
        let ops = marked.map {
            CanvasOp.setNodeColor(id: $0, color: preset.map(String.init))
        }
        let ok = canvasApply(
            CanvasAction(name: "color \(marked.count) cards", ops: ops), to: doc)
        guard ok else { return }
        canvasAnnouncer.announce(.bulk("Set \(marked.count) cards to \(name)."))
    }

    /// Group the marked set: one group sized to the set's padded
    /// bounds — geometric containment (t1 rule 1) does the parenting.
    func canvasGroupMarked(label: String) {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc)
        else { return }
        let marked = canvasMarkedInOrder(doc)
        guard marked.count >= 1 else {
            canvasAnnouncer.announce(.status("No marks."))
            return
        }
        var minX = Double.infinity, minY = Double.infinity
        var maxX = -Double.infinity, maxY = -Double.infinity
        for id in marked {
            guard let node = doc.scene.nodes.first(where: { $0.nodeId == id }) else { continue }
            minX = min(minX, node.x)
            minY = min(minY, node.y)
            maxX = max(maxX, node.x + node.width)
            maxY = max(maxY, node.y + node.height)
        }
        guard minX.isFinite else { return }
        let pad = 40.0
        let ok = canvasApply(
            CanvasAction(
                name: "group \(marked.count) cards",
                ops: [
                    .createGroup(
                        id: Self.newCanvasEntityID(),
                        label: label.isEmpty ? nil : label,
                        x: minX - pad, y: minY - pad,
                        width: (maxX - minX) + pad * 2,
                        height: (maxY - minY) + pad * 2,
                        color: nil)
                ]),
            to: doc)
        guard ok else { return }
        doc.selection.marked = []
        canvasAnnouncer.announce(
            .bulk(
                "Grouped \(marked.count) cards into \"\(label.isEmpty ? "Untitled" : label)\"."
            ))
    }

    func canvasPromptGroupMarked() {
        guard let doc = activeCanvasDocument, !doc.selection.marked.isEmpty else {
            canvasAnnouncer.announce(.status("No marks."))
            return
        }
        guard admitCanvasMutation(for: doc) else { return }
        presentCanvasPrompt(.groupMarked)
    }
}
