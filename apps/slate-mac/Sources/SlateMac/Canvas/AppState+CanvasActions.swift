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

    var id: String {
        switch self {
        case .newGroup: return "newGroup"
        case .renameGroup: return "renameGroup"
        case .moveIntoGroup: return "moveIntoGroup"
        case .setColor: return "setColor"
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
    /// (interview decision 1), announced relatively, selected.
    /// The Wave-4 editor lands the card in edit mode; until #368's
    /// editor slice merges, selection + announcement are the landing.
    func canvasNewCard() {
        guard let doc = activeCanvasDocument, let session = currentSession,
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
        } catch {
            canvasAnnouncer.announce(.error("New card failed: \(error.localizedDescription)"))
        }
    }

    /// New Group: prompts ride the UI (container sheet); this is the
    /// commit path. Placed via the engine like any creation.
    func canvasNewGroup(label: String) {
        guard let doc = activeCanvasDocument, let session = currentSession,
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
    func canvasNewCanvasFile() {
        guard let session = currentSession else { return }
        let base = "Untitled Canvas"
        var name = "\(base).canvas"
        var counter = 2
        // Root-level creation; collision-avoid like New Note.
        while (try? session.readText(path: name)) != nil {
            name = "\(base) \(counter).canvas"
            counter += 1
        }
        do {
            _ = try session.saveText(
                path: name, contents: "{}\n", expectedContentHash: nil)
            openFile(name, target: .currentTab)
            canvasAnnouncer.announce(
                .confirmation("Created canvas \"\((name as NSString).deletingPathExtension)\"."))
        } catch {
            canvasAnnouncer.announce(.error("New canvas failed: \(error.localizedDescription)"))
        }
    }

    /// Delete the selected card/group. Group delete keeps children
    /// (they're contained geometrically); the destructive confirmation
    /// carries the undo hint at standard+ verbosity (t0 §1.3).
    func canvasDeleteSelection() {
        guard let doc = activeCanvasDocument,
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
        guard activeCanvasDocument != nil else { return }
        canvasPrompt = .newGroup
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
        canvasPrompt = .renameGroup(current: row.title)
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
        canvasPrompt = .moveIntoGroup(groups: groups)
    }

    func canvasPromptSetColor() {
        guard let doc = activeCanvasDocument, doc.selection.selected != nil else {
            canvasAnnouncer.announce(.status("Nothing selected."))
            return
        }
        canvasPrompt = .setColor
    }

    /// Move the selected card into a group by name — the voice-friendly
    /// reparent (R22): zero coordinates for the user, engine placement
    /// inside the target's bounds.
    func canvasMoveIntoGroup(groupId: String) {
        guard let doc = activeCanvasDocument, let session = currentSession,
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
