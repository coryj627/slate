// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// What a canvas READ verb needs to run, handed out only by
/// `AppState.canvasReadContext(for:)` so no verb re-derives whether it
/// may proceed. Holding one is the proof that the state mapping said
/// yes.
struct CanvasReadContext {
    let doc: CanvasDocument
    let session: VaultSession
    let handle: UInt64
}

/// The canvas keyboard navigator (Milestone T, #364) — deliberately a
/// **command layer, not a fourth view** (t2 shared-architecture
/// decision): these commands are hosted by every canvas surface and
/// operate on the shared `CanvasSelection`. Plain-arrow bindings apply
/// only while a canvas surface has focus (program rule R2); every
/// movement here is also a `CommandSection.canvas` palette command, so
/// VoiceOver Quick Nav users always have a path.
extension AppState {
    /// The active tab's canvas document, when the active tab is a
    /// ready canvas.
    var activeCanvasDocument: CanvasDocument? {
        guard let tab = workspace.activeTab, case .canvas(let path) = tab.item else {
            return nil
        }
        let doc = canvasDocument(for: path)
        guard case .ready = doc.state else { return nil }
        return doc
    }

    /// The active tab's canvas whatever its load state — DISCOVERY,
    /// separated from admission.
    ///
    /// A read verb needs its document before it may ask the mapping,
    /// because the recorded precedence (m6) puts a verb's own selection
    /// question ahead of the state's: pressing Enter-group with nothing
    /// selected answers "Nothing selected." in the reopening window,
    /// not "reopening". Acquiring the read context first announced
    /// eagerly and inverted that.
    var activeCanvasDocumentAnyState: CanvasDocument? {
        guard let tab = workspace.activeTab, case .canvas(let path) = tab.item else {
            return nil
        }
        return canvasDocument(for: path)
    }

    /// **The one state → response mapping for canvas READ verbs.**
    ///
    /// `nil` means the document can answer a core query. Anything else
    /// is the sentence its state owes the user, and it is TOTAL over
    /// `LoadState` — the `switch` has no `default`, so a new case fails
    /// to compile here rather than falling into somebody's silent arm.
    ///
    /// | State | Answer |
    /// |---|---|
    /// | `.ready`, handle live | `nil` — proceed |
    /// | `.ready`, handle detached | `.reopening` (VA-1) |
    /// | `.loading` | `.loading` (VA-2) |
    /// | `.degraded`, `.failed`, `.retargetFailed` | `.notReadable` |
    ///
    /// A verb may still owe its OWN question first — see
    /// `canvasAnsweredMissingSelection`, which outranks this table on
    /// any state whose retained rows the container actually renders
    /// (`.ready` and `.retargetFailed`). This is what each state owes
    /// once that question is settled.
    ///
    /// This exists because the alternative did not survive contact:
    /// three review rounds in a row found a member missing from a
    /// handwritten list, or a state missing from a handwritten
    /// response set, or two lists disagreeing. Red-team protocol rule 4
    /// says stop patching the sentences and implement the invariant, so
    /// there is now one function to read, one place to change, and a
    /// Rust guard (`slate-uniffi`) that fails when a canvas query is
    /// called from a function that does not route through here and is
    /// not on a named exclusion list.
    ///
    /// Two decisions this table carries. `.reopening` is a NEW sentence
    /// rather than the write refusal, because
    /// `CanvasMutationRefusal.reopening` ends "before making changes",
    /// which is wrong in the ear of a user who pressed a navigation key
    /// and changed nothing. And the detached handle is not reused for
    /// reads: that would downgrade a structural write-safety invariant
    /// — no handle, so `canvas_apply` is unreachable — to a
    /// host-enforced one. Permanently refused (contracts doc, §0b
    /// "Verified during implementation").
    func canvasReadRefusal(for doc: CanvasDocument) -> CanvasStatusNote? {
        switch doc.state {
        case .ready:
            // The one reachable `.ready`-with-no-handle state is
            // `beginBatchRetarget`'s window: the snapshot stays visible
            // while the path-bound handle is detached so nothing can
            // save through the moved-away path.
            return doc.handle == nil || currentSession == nil ? .reopening : nil
        case .loading:
            // A first open, or a prepared replacement installed over an
            // already-open tab. "Reopening" would be false for the
            // first, which is why VA-2 is its own sentence.
            return .loading
        case .degraded, .failed, .retargetFailed:
            // Where-am-I's answer, now everyone's: a canvas that never
            // opened cleanly, was moved to Trash, or failed to reopen
            // cannot answer a structural question, and saying nothing
            // was the t0 §5 gap.
            return .notReadable
        }
    }

    /// What a read verb needs, or `nil` after ANNOUNCING what the
    /// state owes. The announcement happens here so no member decides
    /// which sentence its state deserves.
    func canvasReadContext(for doc: CanvasDocument) -> CanvasReadContext? {
        if let note = canvasReadRefusal(for: doc) {
            canvasAnnouncer.announce(.canvasStatus(note: note))
            return nil
        }
        // Unreachable by the mapping above, which refuses both nils —
        // this is the unwrap, not a second policy.
        guard let session = currentSession, let handle = doc.handle else { return nil }
        return CanvasReadContext(doc: doc, session: session, handle: handle)
    }

    /// The active tab's canvas, through the mapping.
    func canvasReadTarget() -> CanvasReadContext? {
        guard let tab = workspace.activeTab, case .canvas(let path) = tab.item else {
            return nil
        }
        return canvasReadContext(for: canvasDocument(for: path))
    }

    /// The recorded precedence (m6), in one place beside the mapping
    /// that owns the state story: a verb answers its OWN selection
    /// question before the state's — but only on a canvas whose
    /// snapshot the user is actually looking at.
    ///
    /// "Actually looking at" is `LoadState.rendersRetainedSnapshot`,
    /// which is derived from `CanvasContainerView`'s own switch and
    /// pinned against it by a guard. It is NOT `.ready`: writing the
    /// condition out by hand made this gate miss `.retargetFailed`,
    /// whose retained rows the container renders read-only and whose
    /// navigation is palette-reachable, so a no-selection press there
    /// answered with the state instead of the caret (codex 0b round 5 —
    /// the curated-condition class, again).
    ///
    /// Where the predicate is false the snapshot is not on screen at
    /// all, so a selection question has nothing to be about and the
    /// mapping's sentence is the only honest answer.
    ///
    /// Returns true HAVING ANNOUNCED, so the caller returns.
    func canvasAnsweredMissingSelection(_ doc: CanvasDocument) -> Bool {
        guard doc.state.rendersRetainedSnapshot, doc.selection.selected == nil else {
            return false
        }
        canvasAnnounceSelectionUnresolvable()
        return true
    }

    /// The other never-silent arm: the query THREW while the handle was
    /// live. Every structural query in this file refuses an id the model
    /// does not hold with `bad_node`, so a throw here means the
    /// selection no longer names a card this canvas can answer for —
    /// rows outrunning the handle after an external write plus rescan
    /// (contract 0b-6's skew).
    ///
    /// `Nothing selected.` is the accurate existing phrase for that:
    /// nothing RESOLVABLE is selected. It is deliberately not the
    /// verb-specific phrase, because none of those was learned — the
    /// group might have children, the card might have a path, the row
    /// might not be at canvas level. Announcing one of those would be
    /// asserting an answer the query never gave.
    private func canvasAnnounceSelectionUnresolvable() {
        canvasAnnouncer.announce(.canvasStatus(note: .nothingSelected))
    }

    /// Move selection to the next/previous card in reading order.
    func canvasSelectAdjacent(offset: Int) {
        guard let doc = activeCanvasDocument else { return }
        // #373: movement walks the FILTERED set while a filter is on
        // (a view, never a mutation — Esc restores the full canvas).
        let rows = doc.filteredOutline(session: currentSession)
        guard !rows.isEmpty else {
            if doc.filterActive {
                canvasAnnouncer.announce(.canvasStatus(note: .noCardsMatchFilter))
            }
            return
        }
        let order = rows.map(\.nodeId)
        let currentIndex = doc.selection.selected.flatMap { order.firstIndex(of: $0) }
        let target: Int
        if let currentIndex {
            target = max(0, min(order.count - 1, currentIndex + offset))
            if target == currentIndex {
                canvasAnnouncer.announce(
                    .canvasStatus(note: offset > 0 ? .endOfCanvas : .startOfCanvas))
                return
            }
        } else {
            target = offset > 0 ? 0 : order.count - 1
        }
        canvasSelect(nodeId: order[target], in: doc)
    }

    /// Enter the selected group (select its first child), or announce
    /// that the selection isn't a group.
    ///
    /// §W-G row E: the "next outline row one level deeper" walk was a
    /// re-derivation of `GroupTree.children` off the flattened depth
    /// column. It asks core directly now (`canvas_children_of`,
    /// contract 0b-8), whose sibling order `(y, x, document index)` is
    /// the order the outline's depth-first walk emits — so the first
    /// child is the same card it always was.
    ///
    /// The selection is checked BEFORE the handle, so "nothing
    /// selected" answers the same way in every state — the reopening
    /// window must not turn a selection question into a reopening one.
    func canvasEnterGroup() {
        // Discovery, then admission: the selection question is this
        // verb's own and outranks the state's on a canvas the user can
        // see (m6's recorded precedence).
        guard let found = activeCanvasDocumentAnyState else { return }
        if canvasAnsweredMissingSelection(found) { return }
        guard let target = canvasReadContext(for: found) else { return }
        let doc = target.doc
        guard let selected = doc.selection.selected,
            let row = doc.outline.first(where: { $0.nodeId == selected })
        else {
            return canvasAnnounceSelectionUnresolvable()
        }
        guard row.kind == "group" else {
            canvasAnnouncer.announce(.canvasStatus(note: .notAGroup))
            return
        }
        // A THROW is not an empty group — see
        // `canvasAnnounceSelectionUnresolvable`. Only a successful query
        // that came back empty may claim the group is empty.
        guard
            let children = try? target.session.canvasChildrenOf(
                handle: target.handle, groupId: selected)
        else {
            return canvasAnnounceSelectionUnresolvable()
        }
        guard let firstChild = children.first else {
            canvasAnnouncer.announce(.canvasStatus(note: .groupIsEmpty(label: row.title)))
            return
        }
        canvasSelect(nodeId: firstChild, in: doc)
    }

    /// Exit to the containing group (select the group row), or announce
    /// canvas level.
    ///
    /// §W-G row E: the backwards scan for the nearest preceding row at
    /// `depth − 1` is `GroupTree.parent` spelled in outline indices.
    /// `canvas_parent_of` (contract 0b-8) answers it, and `nil` — no
    /// parent — is exactly "at canvas level".
    func canvasExitGroup() {
        guard let found = activeCanvasDocumentAnyState else { return }
        if canvasAnsweredMissingSelection(found) { return }
        guard let target = canvasReadContext(for: found) else { return }
        let doc = target.doc
        guard let selected = doc.selection.selected else {
            return canvasAnnounceSelectionUnresolvable()
        }
        // A THROW is not "at canvas level" — a card the canvas cannot
        // resolve has no level. Only a successful query returning no
        // parent may say that.
        //
        // `do`/`catch`, NOT `try?`: this call returns `String?`, and
        // `try?` on an optional-returning throwing call FLATTENS
        // (SE-0230), so `try?` would collapse "the query threw" and
        // "there is no parent" into one `nil` — erasing the very
        // distinction the two arms below exist to make. The flattening
        // also makes the two-step `guard let` shape fail to compile,
        // which is how it was caught.
        let parent: String?
        do {
            parent = try target.session.canvasParentOf(
                handle: target.handle, nodeId: selected)
        } catch {
            return canvasAnnounceSelectionUnresolvable()
        }
        guard let parent else {
            canvasAnnouncer.announce(.canvasStatus(note: .atCanvasLevel))
            return
        }
        canvasSelect(nodeId: parent, in: doc)
    }

    /// Follow the selected card's Nth connection (1-based) in the given
    /// direction sense: forward = connections leaving or linking this
    /// card; back = connections arriving. Direction respects
    /// `fromEnd`/`toEnd` (t0 §1.2 / #360 model data).
    ///
    /// `No connection…` is a claim about the adjacency list, so it is
    /// spoken only when there IS one. An unanswerable lookup — the
    /// reopening window with a cold cache, or a refused id — takes
    /// VA-1's table instead: the sentence for the state, never a
    /// dead-end phrase nothing returned.
    func canvasFollowConnection(forward: Bool, ordinal: Int = 1) {
        // Split, not combined: the document gate and the selection are
        // different questions with different answers, and folding them
        // into one `guard … else { return }` made a plain
        // nothing-selected press SILENT on an ordinary ready canvas —
        // palette-reachable, and out of step with the three sibling
        // verbs that answer it.
        guard let found = activeCanvasDocumentAnyState else { return }
        if canvasAnsweredMissingSelection(found) { return }
        guard let target = canvasReadContext(for: found) else { return }
        let doc = target.doc
        guard let selected = doc.selection.selected else {
            return canvasAnnounceSelectionUnresolvable()
        }
        // Past the mapping the handle is live, so an unanswerable
        // lookup here is a refused id, not a state.
        guard let neighbors = doc.neighborsIfKnown(of: selected, session: target.session)
        else {
            return canvasAnnounceSelectionUnresolvable()
        }
        let candidates = neighbors.filter { neighbor in
            switch neighbor.direction {
            case .outgoing: return forward
            case .incoming: return !forward
            case .bidirectional, .undirected: return true
            }
        }
        guard candidates.indices.contains(ordinal - 1) else {
            canvasAnnouncer.announce(
                .canvasStatus(
                    note: .noConnection(
                        forward: forward,
                        ordinal: candidates.isEmpty ? nil : UInt32(clamping: ordinal))))
            return
        }
        let neighbor = candidates[ordinal - 1]
        // Narrate the destination's REAL kind (Codoki #613: a group or
        // file target must not be introduced as a text card).
        let otherKind =
            doc.outline.first { $0.nodeId == neighbor.otherNode }?.kind ?? "text"
        canvasAnnouncer.announce(
            .canvasConnectionTraversed(
                direction: neighbor.direction,
                kindLabel: otherKind, title: neighbor.otherTitle,
                label: neighbor.label))
        canvasSelect(nodeId: neighbor.otherNode, in: doc, announce: false)
    }

    /// Trace the outgoing chain from the selected card (cycle-safe),
    /// announcing each hop, ending with the visited count (t3).
    ///
    /// §W-G row E: the greedy first-unseen walk is core's
    /// (`canvas_trace_path`, contract 0b-9) — `Outgoing` and
    /// `Bidirectional` are traversable, `Undirected` is not, neighbours
    /// come in edge document order, and the seen set is keyed by node,
    /// so a cycle or a self-loop ends the walk exactly where mac's loop
    /// ended it. The hops EXCLUDE the start card, so an empty list is
    /// the dead end mac spelled as `visited.count == 1`.
    func canvasTracePath() {
        guard let found = activeCanvasDocumentAnyState else { return }
        if canvasAnsweredMissingSelection(found) { return }
        guard let target = canvasReadContext(for: found) else { return }
        let doc = target.doc
        guard let start = doc.selection.selected else {
            return canvasAnnounceSelectionUnresolvable()
        }
        // A THROW is not a dead end: `No outgoing path from "X".` is
        // spoken only when the walk actually came back with no hops.
        guard let hops = try? target.session.canvasTracePath(
            handle: target.handle, nodeId: start)
        else {
            return canvasAnnounceSelectionUnresolvable()
        }
        let startTitle = doc.outline.first { $0.nodeId == start }?.title
        guard let last = hops.last else {
            canvasAnnouncer.announce(
                .canvasStatus(note: .noOutgoingPath(title: startTitle ?? "")))
            return
        }
        canvasSelect(nodeId: last.nodeId, in: doc, announce: false)
        // The event carries the TITLES only; core speaks their count as
        // the sentence's tail, so the list and the number it claims can
        // never disagree (contracts doc CD-13 — mac spoke
        // `visited.count` while listing `titles`).
        let titles = (startTitle.map { [$0] } ?? []) + hops.map(\.title)
        canvasAnnouncer.announce(.canvasTracePathEnd(titles: titles))
    }

    /// The one selection mutation used by every navigator movement:
    /// updates the shared selection and narrates through the funnel.
    func canvasSelect(nodeId: String, in doc: CanvasDocument, announce: Bool = true) {
        let previous = doc.selection.selected
        doc.selection.selected = nodeId
        guard announce,
            let row = doc.outline.first(where: { $0.nodeId == nodeId })
        else { return }
        let previousPath =
            previous
            .flatMap { prev in doc.outline.first { $0.nodeId == prev } }?.groupPath ?? []
        if row.groupPath != previousPath {
            if let entered = row.groupPath.last, !previousPath.contains(entered) {
                // CD-4: the ENTERED GROUP's own card count, which is
                // exactly `row.totalM` — the arrived-at row's container
                // size, straight from core. mac walked back to the
                // group's own outline row and spoke ITS `totalM`, i.e.
                // how many siblings the GROUP has, a different number.
                // There is no lookup left at all, so Codoki #613's
                // repeated-label miscount cannot recur either.
                canvasAnnouncer.announce(
                    .canvasGroupEntered(label: entered, count: row.totalM))
            } else if let left = previousPath.last, !row.groupPath.contains(left) {
                canvasAnnouncer.announce(.canvasGroupLeft(label: left))
            }
        }
        canvasAnnouncer.announce(
            .canvasMovedTo(
                verbosity: canvasAnnouncer.verbosity,
                kindLabel: row.kind, title: row.title,
                ordinalN: row.ordinalN, totalM: row.totalM,
                container: row.groupPath.last,
                connectionCount: row.connectionCount,
                colorName: row.colorName,
                marked: doc.selection.marked.contains(nodeId)))
    }

    // MARK: Viewport commands (#520)

    private func announceZoom(_ doc: CanvasDocument) {
        canvasAnnouncer.announce(
            .canvasZoom(context: nil, percent: UInt32(clamping: doc.viewport.zoomPercent)))
    }

    func canvasZoomIn() {
        guard let doc = activeCanvasDocument else { return }
        doc.viewport.zoom(by: CanvasViewport.zoomStep)
        announceZoom(doc)
    }

    func canvasZoomOut() {
        guard let doc = activeCanvasDocument else { return }
        doc.viewport.zoom(by: 1 / CanvasViewport.zoomStep)
        announceZoom(doc)
    }

    func canvasActualSize() {
        guard let doc = activeCanvasDocument else { return }
        doc.viewport.setScale(1.0)
        announceZoom(doc)
    }

    func canvasFitCanvas() {
        // §W-G row H: the scene's extent is core's `canvas_bounds`
        // (contract 0b-11) — `SpatialIndex::bounds` verbatim, every
        // node including group frames, exactly what the union loop that
        // stood here covered. `nil` is the empty canvas, which is the
        // `!doc.scene.nodes.isEmpty` guard it replaces.
        guard let target = canvasReadTarget() else { return }
        let doc = target.doc
        // One `?`, not two (SE-0230 flattens): an empty canvas and a
        // thrown query arrive as the same `nil`, and both stay silent —
        // the empty case was silent before PR 0b, and `canvas_bounds`
        // has no `bad_node` path to distinguish (contracts doc, VA-1's
        // recorded exclusions).
        guard let bounds = try? target.session.canvasBounds(handle: target.handle)
        else { return }
        doc.viewport.fit(
            rect: CGRect(
                x: bounds.x, y: bounds.y, width: bounds.width, height: bounds.height))
        canvasAnnouncer.announce(
            .canvasZoom(
                context: .fitCanvas, percent: UInt32(clamping: doc.viewport.zoomPercent)))
    }

    func canvasZoomToSelection() {
        guard let doc = activeCanvasDocument else { return }
        guard let selected = doc.selection.selected,
            let node = doc.scene.nodes.first(where: { $0.nodeId == selected })
        else {
            canvasAnnouncer.announce(.canvasStatus(note: .nothingSelected))
            return
        }
        doc.viewport.fit(
            rect: CGRect(x: node.x, y: node.y, width: node.width, height: node.height),
            padding: 120)
        canvasAnnouncer.announce(
            .canvasZoom(
                context: .zoomedToSelection,
                percent: UInt32(clamping: doc.viewport.zoomPercent)))
    }

    /// Viewport-follows-selection toggle (default ON; the auto-pan
    /// itself stays silent — t0 §1.5 no-doubling).
    func canvasToggleFollowSelection() {
        guard let doc = activeCanvasDocument else { return }
        doc.viewport.followSelection.toggle()
        canvasAnnouncer.announce(
            .canvasFollowSelectionToggled(following: doc.viewport.followSelection))
    }

    /// The per-document mode controller (t0 §2), created on first use.
    /// Focus departure and Esc route through the container.
    func canvasModeController(for doc: CanvasDocument) -> CanvasModeController {
        if let existing = canvasModeControllers[doc.path] {
            return existing
        }
        let controller = CanvasModeController { [weak self] event in
            self?.canvasAnnouncer.announce(event)
        }
        canvasModeControllers[doc.path] = controller
        return controller
    }
}
