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
        let geometry = Self.canvasGeometry
        do {
            let placement = try session.canvasPlaceNew(
                handle: handle,
                anchor: doc.selection.selected,
                width: geometry.defaultCardW, height: geometry.defaultCardH,
                directionHint: nil, exclude: [])
            let ok = canvasApply(
                CanvasAction(
                    name: "create card",
                    ops: [
                        .createNode(
                            id: id, content: .text(text: ""),
                            x: placement.x, y: placement.y,
                            width: geometry.defaultCardW, height: geometry.defaultCardH,
                            color: nil)
                    ]),
                to: doc)
            guard ok else { return }
            canvasSelect(nodeId: id, in: doc, announce: false)
            canvasAnnouncer.announce(
                .canvasCreated(
                    kindLabel: "text", title: "Untitled",
                    relative: placement.relative))
            // G22: a new text card lands in edit mode.
            canvasCardEditor = CanvasCardEditorRequest(
                nodeId: id, title: "Untitled", initialText: "")
        } catch {
            canvasAnnouncer.announce(
                .canvasActionFailed(
                    action: .newCard, detail: error.localizedDescription))
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
        let geometry = Self.canvasGeometry
        do {
            let placement = try session.canvasPlaceNew(
                handle: handle,
                anchor: doc.selection.selected,
                width: geometry.defaultGroupW, height: geometry.defaultGroupH,
                directionHint: nil, exclude: [])
            let ok = canvasApply(
                CanvasAction(
                    name: "create group",
                    ops: [
                        .createGroup(
                            id: id, label: label.isEmpty ? nil : label,
                            x: placement.x, y: placement.y,
                            width: geometry.defaultGroupW, height: geometry.defaultGroupH,
                            color: nil)
                    ]),
                to: doc)
            guard ok else { return }
            canvasSelect(nodeId: id, in: doc, announce: false)
            // One template for cards and groups: core's `CanvasCreated`
            // group rendering is byte-identical to the string this site
            // hand-rolled (contracts doc, "Verified during
            // implementation").
            canvasAnnouncer.announce(
                .canvasCreated(
                    kindLabel: "group", title: label.isEmpty ? "Untitled" : label,
                    relative: placement.relative))
        } catch {
            canvasAnnouncer.announce(
                .canvasActionFailed(
                    action: .newGroup, detail: error.localizedDescription))
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
                        .canvasActionFailed(
                            action: .newCanvas, detail: error.localizedDescription))
                    return
                }
                break
            }

            guard let name = createdName else {
                let error = VaultError.Io(
                    message: "could not find a free canvas name after 200 attempts")
                self.canvasAnnouncer.announce(
                    .canvasActionFailed(
                        action: .newCanvas, detail: error.localizedDescription))
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
                .canvasFileCreated(name: (name as NSString).deletingPathExtension))
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
        let op: CanvasOp =
            row.kind == "group" ? .ungroup(id: selected) : .deleteNode(id: selected)
        let name = row.kind == "group" ? "ungroup \"\(row.title)\"" : "delete \"\(row.title)\""
        let ok = canvasApply(CanvasAction(name: name, ops: [op]), to: doc)
        guard ok else { return }
        doc.selection.selected = nil
        canvasAnnouncer.announce(
            .canvasDeleted(
                target: row.kind == "group"
                    ? .group(label: row.title)
                    : .card(kindLabel: row.kind, title: row.title),
                verbosity: canvasAnnouncer.verbosity,
                undoChord: Self.canvasUndoChord))
    }

    /// Set the selected card's color (named preset or nil to clear),
    /// announced with the color NAME (t0 §1.1; #370 verifies contrast).
    func canvasSetColor(preset: Int?) {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc),
            let selected = doc.selection.selected,
            let row = doc.outline.first(where: { $0.nodeId == selected })
        else { return }
        let ok = canvasApply(
            CanvasAction(
                name: "set color of \"\(row.title)\"",
                ops: [.setNodeColor(id: selected, color: preset.map(String.init))]),
            to: doc)
        guard ok else { return }
        // The colour goes over TYPED; core names it through
        // `canvas::color_name` (contracts doc 0a-11). The preset-name
        // dictionary that used to live here is gone.
        canvasAnnouncer.announce(
            .canvasColorSet(title: row.title, color: Self.canvasColor(preset: preset)))
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
            canvasAnnouncer.announce(.canvasStatus(note: .notAGroup))
            return
        }
        let ok = canvasApply(
            CanvasAction(
                name: "rename group \"\(row.title)\"",
                ops: [.renameGroup(id: selected, label: label.isEmpty ? nil : label)]),
            to: doc)
        guard ok else { return }
        canvasAnnouncer.announce(
            .canvasRenamedGroup(label: label.isEmpty ? "Untitled" : label))
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
            canvasAnnouncer.announce(.canvasStatus(note: .notAGroup))
            return
        }
        guard admitCanvasMutation(for: doc) else { return }
        presentCanvasPrompt(.renameGroup(current: row.title), draft: row.title)
    }

    func canvasPromptMoveIntoGroup() {
        guard let doc = activeCanvasDocument,
            doc.selection.selected != nil
        else {
            canvasAnnouncer.announce(.canvasStatus(note: .nothingSelected))
            return
        }
        let groups = doc.outline.filter { $0.kind == "group" }
            .map { (id: $0.nodeId, title: $0.title) }
        guard !groups.isEmpty else {
            canvasAnnouncer.announce(.canvasStatus(note: .noGroups))
            return
        }
        guard admitCanvasMutation(for: doc) else { return }
        presentCanvasPrompt(.moveIntoGroup(groups: groups))
    }

    func canvasPromptSetColor() {
        guard let doc = activeCanvasDocument, doc.selection.selected != nil else {
            canvasAnnouncer.announce(.canvasStatus(note: .nothingSelected))
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
            // §W-G rows D + H: placing inside a group is core's
            // `canvas_place_inside_group` (contract 0b-12), and three
            // things die with the block that stood here.
            //
            // 1. The first-child anchor was TITLE-keyed
            //    (`groupPath.last == group.title`) — the last live copy
            //    of the repeated-label miscount Codoki #613 flagged.
            // 2. Anchoring on that child delegated to plain
            //    `place_new`, which is not clipped to the group: a full
            //    group pushed the card OUT and containment silently
            //    un-parented it. Core's lattice never leaves the frame.
            // 3. The `(x + 20, y + 40)` inset was `GRID_STEP` and
            //    `2 × GRID_STEP` written as literals.
            //
            // The outcome is typed, so "the group is full" is answered
            // instead of guessed (CD-21). `TooSmall` is the group that
            // cannot hold one slot at all; its point is core's inset,
            // deliberately unchecked for overlap, so the shipped
            // refusal is preserved by checking it here.
            let placement = try session.canvasPlaceInsideGroup(
                handle: handle, groupId: groupId,
                width: node.width, height: node.height, exclude: [selected])
            let target: (x: Double, y: Double)
            switch placement {
            case .placed(let x, let y):
                target = (x, y)
            case .tooSmall(let x, let y):
                let overlaps = try session.canvasCheckOverlap(
                    handle: handle,
                    rect: CanvasRect(
                        x: x, y: y, width: node.width, height: node.height),
                    exclude: [selected])
                guard overlaps.isEmpty else {
                    canvasAnnouncer.announce(
                        .canvasBlocked(reason: .noFreeSpaceInGroup(label: group.title)))
                    return
                }
                target = (x, y)
            case .full:
                canvasAnnouncer.announce(
                    .canvasBlocked(reason: .noFreeSpaceInGroup(label: group.title)))
                return
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
            canvasAnnouncer.announce(.canvasMovedIntoGroup(label: group.title))
        } catch {
            canvasAnnouncer.announce(
                .canvasActionFailed(
                    action: .moveIntoGroup, detail: error.localizedDescription))
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
            canvasAnnouncer.announce(.canvasStatus(note: .nothingSelected))
            return
        }
        canvasCardPicker = CanvasCardPickerRequest(purpose: purpose)
    }

    /// Project a set of ids onto the canvas reading order — core's
    /// `canvas_order_nodes` (§W-G row F, contract 0b-10). Unknown ids
    /// drop silently (a mark left over from an external write is not
    /// fatal), duplicates collapse to one reading-order position, and
    /// an empty input gives an empty output. Without a live handle
    /// there is no reading order to project onto, and every caller is
    /// behind `admitCanvasMutation`, which refuses in exactly that
    /// case.
    func canvasInReadingOrder(_ ids: [String], in doc: CanvasDocument) -> [String] {
        guard let session = currentSession, let handle = doc.handle else { return [] }
        return (try? session.canvasOrderNodes(handle: handle, ids: ids)) ?? []
    }

    /// The ids that move for a structural placement: the marked set
    /// when marks exist (rigid unit, #524 semantics), else the
    /// selected card. The selection fallback is host state (§2 row F
    /// is Tier 3 there); only the projection is core's.
    func canvasMovingSet(in doc: CanvasDocument) -> [String] {
        if !doc.selection.marked.isEmpty {
            return canvasInReadingOrder(Array(doc.selection.marked), in: doc)
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
            canvasAnnouncer.announce(.canvasStatus(note: .pickOutsideMovingSet))
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
                    .canvasCardPlaced(
                        verb: .moved, title: row?.title ?? id,
                        relative: placement.relative))
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
                    CanvasAction(
                        name: "move \(CountCopy.counted(moving.count, "card", "cards"))",
                        ops: ops), to: doc)
                guard ok else { return }
                canvasAnnouncer.announce(
                    .canvasBulkMoved(
                        count: UInt32(clamping: moving.count),
                        relative: placement.relative))
            }
        } catch {
            canvasAnnouncer.announce(
                .canvasActionFailed(
                    action: .placement, detail: error.localizedDescription))
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
                canvasAnnouncer.announce(.canvasBlocked(reason: .alignWouldOverlap))
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
                .canvasCardAligned(
                    title: row?.title ?? selected,
                    targetTitle: targetRow?.title ?? target))
        } catch {
            canvasAnnouncer.announce(
                .canvasActionFailed(action: .align, detail: error.localizedDescription))
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
            canvasAnnouncer.announce(.canvasStatus(note: .noConnections))
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
            canvasAnnouncer.announce(.canvasStatus(note: .noConnections))
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
            canvasAnnouncer.announce(.canvasStatus(note: .nothingSelected))
            return
        }
        let marking = !doc.selection.marked.contains(selected)
        if marking {
            doc.selection.marked.insert(selected)
        } else {
            doc.selection.marked.remove(selected)
        }
        canvasAnnouncer.announce(
            .canvasMarkToggled(
                marked: marking, title: row.title,
                count: UInt32(clamping: doc.selection.marked.count)))
    }

    func canvasClearMarks() {
        guard let doc = activeCanvasDocument else { return }
        let count = doc.selection.marked.count
        doc.selection.marked = []
        canvasAnnouncer.announce(.canvasMarksCleared(count: UInt32(clamping: count)))
    }

    /// The marks list (t0 §3: the pull-based counterpart to mark
    /// announcements) — a focusable panel with Unmark + Jump per row.
    func canvasShowMarksList() {
        guard let doc = activeCanvasDocument else { return }
        guard !doc.selection.marked.isEmpty else {
            canvasAnnouncer.announce(.canvasStatus(note: .noMarks))
            return
        }
        presentCanvasPrompt(.marksList)
    }

    /// Marked ids in reading order (deterministic everywhere) — the
    /// same core projection `canvasMovingSet` uses, so the two cannot
    /// disagree about what "in order" means.
    func canvasMarkedInOrder(_ doc: CanvasDocument) -> [String] {
        canvasInReadingOrder(Array(doc.selection.marked), in: doc)
    }

    /// Bulk delete: one action, one undo, one summary.
    func canvasDeleteMarked() {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc)
        else { return }
        let marked = canvasMarkedInOrder(doc)
        guard !marked.isEmpty else {
            canvasAnnouncer.announce(.canvasStatus(note: .noMarks))
            return
        }
        let ops = marked.map { CanvasOp.deleteNode(id: $0) }
        let ok = canvasApply(
            CanvasAction(
                name: "delete \(CountCopy.counted(marked.count, "card", "cards"))",
                ops: ops), to: doc)
        guard ok else { return }
        doc.selection.marked = []
        if doc.selection.selected.map(marked.contains) == true {
            doc.selection.selected = nil
        }
        canvasAnnouncer.announce(
            .canvasDeleted(
                target: .cards(count: UInt32(clamping: marked.count)),
                verbosity: canvasAnnouncer.verbosity,
                undoChord: Self.canvasUndoChord))
    }

    /// Bulk color: one action, one summary.
    func canvasColorMarked(preset: Int?) {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc)
        else { return }
        let marked = canvasMarkedInOrder(doc)
        guard !marked.isEmpty else {
            canvasAnnouncer.announce(.canvasStatus(note: .noMarks))
            return
        }
        let ops = marked.map {
            CanvasOp.setNodeColor(id: $0, color: preset.map(String.init))
        }
        let ok = canvasApply(
            CanvasAction(
                name: "color \(CountCopy.counted(marked.count, "card", "cards"))",
                ops: ops), to: doc)
        guard ok else { return }
        canvasAnnouncer.announce(
            .canvasBulkColorSet(
                count: UInt32(clamping: marked.count),
                color: Self.canvasColor(preset: preset)))
    }

    /// Group the marked set: one group sized to the set's padded
    /// bounds — geometric containment (t1 rule 1) does the parenting.
    func canvasGroupMarked(label: String) {
        guard let doc = activeCanvasDocument,
            admitCanvasMutation(for: doc),
            let session = currentSession,
            let handle = doc.handle
        else { return }
        let marked = canvasMarkedInOrder(doc)
        guard marked.count >= 1 else {
            canvasAnnouncer.announce(.canvasStatus(note: .noMarks))
            return
        }
        // §W-G row H: the padded bounding box is core's
        // (`canvas_group_rect_around`, contract 0b-11). The literal
        // `pad = 40` that stood here IS `placement::DEFAULT_GAP` — the
        // fold and the number both go. `nil` (no member resolves) is
        // mac's `guard minX.isFinite else { return }` silent no-op,
        // typed now (CD-24); what a host SAYS there is PR G's call, not
        // this migration's, so the silence is preserved deliberately.
        let frameLookup = try? session.canvasGroupRectAround(handle: handle, members: marked)
        guard let frame = frameLookup ?? nil else { return }
        let ok = canvasApply(
            CanvasAction(
                name: "group \(CountCopy.counted(marked.count, "card", "cards"))",
                ops: [
                    .createGroup(
                        id: Self.newCanvasEntityID(),
                        label: label.isEmpty ? nil : label,
                        x: frame.x, y: frame.y,
                        width: frame.width, height: frame.height,
                        color: nil)
                ]),
            to: doc)
        guard ok else { return }
        doc.selection.marked = []
        canvasAnnouncer.announce(
            .canvasGrouped(
                count: UInt32(clamping: marked.count),
                label: label.isEmpty ? "Untitled" : label))
    }

    func canvasPromptGroupMarked() {
        guard let doc = activeCanvasDocument, !doc.selection.marked.isEmpty else {
            canvasAnnouncer.announce(.canvasStatus(note: .noMarks))
            return
        }
        guard admitCanvasMutation(for: doc) else { return }
        presentCanvasPrompt(.groupMarked)
    }
}
