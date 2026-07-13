// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

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
        guard let doc = activeCanvasDocument, let session = currentSession,
            let handle = doc.handle
        else { return }
        guard let origin = doc.selection.selected,
            let originRow = doc.outline.first(where: { $0.nodeId == origin }),
            let originNode = doc.scene.nodes.first(where: { $0.nodeId == origin })
        else {
            canvasAnnouncer.announce(.status("Nothing selected."))
            return
        }
        let id = Self.newCanvasEntityID()
        do {
            let placement = try session.canvasPlaceNew(
                handle: handle, anchor: origin,
                width: 260, height: 140,
                directionHint: direction, exclude: [])
            let newNode = CanvasSceneNode(
                nodeId: id, kind: "text", title: "Untitled",
                x: placement.x, y: placement.y, width: 260, height: 140,
                color: nil, colorName: nil, subpath: nil)
            let sides = Self.canvasAutoSides(from: originNode, to: newNode)
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
                .confirmation(
                    "Created connected card "
                        + CanvasAnnouncer.relativePhrase(placement.relative)
                        + " — connected from \"\(originRow.title)\"."))
            // Lands in edit mode (the mind-mapping loop: create → type).
            canvasCardEditor = CanvasCardEditorRequest(
                nodeId: id, title: "Untitled", initialText: "")
        } catch {
            canvasAnnouncer.announce(
                .error("Create connected card failed: \(error.localizedDescription)"))
        }
    }

    /// Palette variant: choose the direction first.
    func canvasPromptConnectedDirection() {
        guard let doc = activeCanvasDocument, doc.selection.selected != nil else {
            canvasAnnouncer.announce(.status("Nothing selected."))
            return
        }
        canvasPrompt = .connectedDirection
    }

    // MARK: Duplicate (selection or marked set — ONE action)

    /// Duplicate the marked set (rigid unit) or the selected card.
    /// Groups expand to their geometric members (t1 strict-center
    /// containment), so a duplicated frame keeps its cards. Engine
    /// set-placement preserves pairwise offsets; edges are not copied
    /// (cards duplicate, connections are authored intent).
    func canvasDuplicate() {
        guard let doc = activeCanvasDocument, let session = currentSession,
            let handle = doc.handle
        else { return }
        let seed = canvasMovingSet(in: doc)
        guard !seed.isEmpty else {
            canvasAnnouncer.announce(.status("Nothing selected."))
            return
        }
        let nodesById = Dictionary(
            uniqueKeysWithValues: doc.scene.nodes.map { ($0.nodeId, $0) })
        // Expand groups to members by strict-center containment.
        var expanded: [String] = []
        var included = Set<String>()
        for id in doc.outline.map(\.nodeId) {  // reading order, deterministic
            guard let node = nodesById[id] else { continue }
            let directlyPicked = seed.contains(id)
            let insidePickedGroup = seed.contains { pickedId in
                guard pickedId != id, let group = nodesById[pickedId],
                    group.kind == "group"
                else { return false }
                let cx = node.x + node.width / 2
                let cy = node.y + node.height / 2
                return cx > group.x && cx < group.x + group.width
                    && cy > group.y && cy < group.y + group.height
            }
            if (directlyPicked || insidePickedGroup) && !included.contains(id) {
                expanded.append(id)
                included.insert(id)
            }
        }
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
                : "duplicate \(expanded.count) cards"
            let ok = canvasApply(CanvasAction(name: name, ops: ops), to: doc)
            guard ok else { return }
            if single {
                canvasAnnouncer.announce(
                    .confirmation(
                        "Duplicated \"\(doc.outline.first { $0.nodeId == expanded[0] }?.title ?? "card")\" "
                            + CanvasAnnouncer.relativePhrase(placement.relative) + "."))
            } else {
                canvasAnnouncer.announce(
                    .bulk("Duplicated \(expanded.count) cards — one undo restores."))
            }
        } catch {
            canvasAnnouncer.announce(.error("Duplicate failed: \(error.localizedDescription)"))
        }
    }

    // MARK: Convert card → note (U2-2 creation API)

    func canvasPromptConvertToNote() {
        guard let doc = activeCanvasDocument,
            let selected = doc.selection.selected,
            let row = doc.outline.first(where: { $0.nodeId == selected })
        else {
            canvasAnnouncer.announce(.status("Nothing selected."))
            return
        }
        guard row.kind == "text" else {
            canvasAnnouncer.announce(.status("Only text cards convert to notes."))
            return
        }
        // Suggested path: the card's first-line title, slugged lightly.
        let stem = row.title
            .replacingOccurrences(of: "/", with: "-")
            .trimmingCharacters(in: .whitespacesAndNewlines)
        canvasPrompt = .convertToNote(
            nodeId: selected, suggested: (stem.isEmpty ? "Untitled" : stem) + ".md")
    }

    /// Commit: create the note via the U2-2 save path (journaled file
    /// creation), then ONE canvas_apply retargets the card at it.
    /// Canvas undo restores the text card; the note file remains (the
    /// U2 convention — file ops have their own journal).
    func canvasConvertToNote(nodeId: String, path: String) {
        guard let doc = activeCanvasDocument, let session = currentSession,
            let handle = doc.handle,
            let row = doc.outline.first(where: { $0.nodeId == nodeId })
        else { return }
        let cleanPath = path.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !cleanPath.isEmpty, cleanPath.lowercased().hasSuffix(".md") else {
            canvasAnnouncer.announce(.error("The note path must end in .md."))
            return
        }
        // The `files` snapshot is a cheap early bail; it is NOT the
        // collision guard — a file present on disk but absent from the
        // (possibly stale/unscanned) list would slip through. The real
        // guard is the backend's create-if-absent contract below.
        guard !files.contains(where: { $0.path == cleanPath }) else {
            canvasAnnouncer.announce(.error("\(cleanPath) already exists. Pick another name."))
            return
        }
        let text = (try? session.canvasNodeText(handle: handle, nodeId: nodeId)) ?? ""
        do {
            // Data-safety (adversarial review): expectedContentHash ""
            // is the create-IF-ABSENT idiom — the on-disk hash of a
            // missing file is "" (session.rs read_disk_contents_and_hash),
            // so an existing file mismatches and returns WriteConflict
            // rather than being overwritten with the card text. `nil`
            // would save unconditionally and clobber it.
            _ = try session.saveText(
                path: cleanPath, contents: text, expectedContentHash: "")
            // #871 post-merge audit: bypasses publishTreeMutation — barrier the structural undo history so no stale inverse targets this new path.
            clearStructuralUndoStacks()
        } catch let error as VaultError {
            if case .WriteConflict = error {
                canvasAnnouncer.announce(
                    .error("\(cleanPath) already exists on disk. Pick another name."))
            } else {
                canvasAnnouncer.announce(
                    .error("Could not create \(cleanPath): \(error.localizedDescription)"))
            }
            return
        } catch {
            canvasAnnouncer.announce(
                .error("Could not create \(cleanPath): \(error.localizedDescription)"))
            return
        }
        let ok = canvasApply(
            CanvasAction(
                name: "convert \"\(row.title)\" to note",
                ops: [
                    .setNodeContent(
                        id: nodeId, content: .file(file: cleanPath, subpath: nil))
                ]),
            to: doc)
        guard ok else { return }
        canvasAnnouncer.announce(
            .confirmation(
                "Converted to note \(cleanPath). The card now points at it."))
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
            .filter("Filter cleared — \(doc.outline.count) cards."))
    }

    /// Debounced result count (t0 §1.5 — the announcer's filter
    /// category coalesces keystroke bursts).
    func canvasAnnounceFilterCount(doc: CanvasDocument) {
        guard doc.filterActive else { return }
        let count = doc.filteredOutline.count
        canvasAnnouncer.announce(
            .filter("\(count) card\(count == 1 ? "" : "s") match."))
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
                    .error(
                        "Heading \(wanted) was not found in \((path as NSString).lastPathComponent)."
                    ))
            }
        }
    }
}
