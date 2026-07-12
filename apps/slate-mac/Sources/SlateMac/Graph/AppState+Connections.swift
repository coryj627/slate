// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Connections leaf (Milestone P, P1-1 #554): the active note's local
/// graph neighborhood as structured, keyboard-navigable in/out lists —
/// the local graph, projected accessibly. Backed by `graph_neighborhood`
/// (structure + metrics + the pre-rendered `audioSummary`) plus, at
/// depth 1, `note_load_bundle` for per-row snippets (spec §P1-1).
extension AppState {
    /// The default filter for the leaf: ghosts on (they carry the
    /// "Create note" action) and attachments ON — an `![[pic.png]]`
    /// neighbor is a real local-graph connection and earns the
    /// attachment badge (review round 1 finding 7). Orphans-only is a
    /// global-table preset, never the local view.
    private var connectionsFilter: GraphFilter {
        GraphFilter(includeAttachments: true, includeGhosts: true, orphansOnly: false)
    }

    /// True when the Connections leaf is the active right-pane leaf.
    /// The panel stays mounted (ZStack retention), so loads/announcements
    /// must gate on this or the hidden panel narrates every selection
    /// (review round 1 finding 5).
    var connectionsLeafActiveForView: Bool {
        workspace.activeLeaf == .connections
    }

    /// Clamp any incoming depth into the backend's 1…3 window. Pure —
    /// `nonisolated` so unit tests can call it off the main actor.
    nonisolated static func clampConnectionsDepth(_ depth: Int) -> Int {
        min(3, max(1, depth))
    }

    /// Clear all Connections state — called on vault open/close so a
    /// root or back-stack path from vault A can never load or open
    /// against vault B (review round 1 finding 3).
    func resetConnectionsState() {
        connectionsRootPath = nil
        connectionsBackStack = []
        connectionsNeighborhood = nil
        connectionsBundle = nil
        connectionsLoadedPath = nil
        connectionsError = nil
        connectionsLoading = false
        connectionsDepth = 1
        // Generation is session-local; a stale high-water mark would
        // make the next vault's real mutation read as already-seen
        // (review round 2 finding 5).
        connectionsSeenGraphGeneration = 0
        connectionsLoadSeq += 1  // invalidate any in-flight load
    }

    /// (Re)load the Connections leaf for the effective root note at the
    /// current depth. Compute-then-publish with the O-5 guards so a
    /// stale load for a note the user already left can never publish.
    /// `announce` is false for background refreshes (a generation bump
    /// while the leaf is inactive) so only deliberate views speak.
    func loadConnections(announce: Bool = true) {
        guard let session = currentSession, let path = connectionsEffectivePath else {
            connectionsNeighborhood = nil
            connectionsBundle = nil
            connectionsLoadedPath = nil
            connectionsError = nil
            connectionsLoading = false
            return
        }
        connectionsLoadSeq += 1
        let seq = connectionsLoadSeq
        let depth = UInt32(Self.clampConnectionsDepth(connectionsDepth))
        let filter = connectionsFilter
        let wantBundle = depth == 1
        connectionsLoading = true

        Task { [weak self] in
            let result: Result<(GraphNeighborhood, NoteLoadBundle?), VaultError> =
                await Task.detached(priority: .userInitiated) {
                    do {
                        let hood = try session.graphNeighborhood(
                            path: path, depth: depth, filter: filter)
                        let bundle =
                            wantBundle
                            ? try session.noteLoadBundle(
                                path: path, backlinksPaging: Paging(cursor: nil, limit: 200))
                            : nil
                        return .success((hood, bundle))
                    } catch let error as VaultError {
                        return .failure(error)
                    } catch {
                        return .failure(.Io(message: error.localizedDescription))
                    }
                }.value

            if let gate = self?.connectionsPublishGate { await gate() }

            guard let self else { return }
            guard !Task.isCancelled, self.currentSession === session,
                self.connectionsEffectivePath == path, seq == self.connectionsLoadSeq
            else { return }

            self.connectionsLoading = false
            // Re-check leaf activity at the PUBLISH point, not capture
            // time: the user may have switched leaves during the load,
            // and a hidden panel must stay silent (review round 2
            // finding 2).
            let speak = announce && self.connectionsLeafActiveForView
            switch result {
            case .success(let (hood, bundle)):
                self.connectionsNeighborhood = hood
                self.connectionsBundle = bundle
                self.connectionsLoadedPath = path
                self.connectionsError = nil
                if speak {
                    self.graphAnnouncer.announce(.summary(hood.audioSummary))
                }
            case .failure(let error):
                self.connectionsError = self.humanReadable(error)
                self.connectionsNeighborhood = nil
                self.connectionsBundle = nil
                self.connectionsLoadedPath = path
                if speak {
                    self.graphAnnouncer.announce(
                        .error("Couldn't load connections: \(self.humanReadable(error))"))
                }
            }
        }
    }

    /// Re-query the graph generation after a `VaultEventListener` event
    /// and reload the Connections leaf when the graph actually changed
    /// (P0-3 refresh contract). Cheap: `graph_generation()` is O(1) and
    /// only bumps on a real graph delta. Wired to the event adapter so
    /// edits, ghost creation, renames, and scans keep the leaf current
    /// (review round 1 finding 4).
    func refreshConnectionsIfGraphChanged() {
        guard let session = currentSession else { return }
        Task { [weak self] in
            let generation = await Task.detached(priority: .utility) {
                session.graphGeneration()
            }.value
            guard let self, self.currentSession === session else { return }
            guard generation != self.connectionsSeenGraphGeneration else { return }
            self.connectionsSeenGraphGeneration = generation
            // Refresh whether or not the leaf is visible so it's correct
            // when next shown; only a visible leaf announces.
            self.loadConnections(announce: false)
        }
    }

    /// Change the local-graph depth (clamped 1…3) and reload.
    func setConnectionsDepth(_ depth: Int) {
        let clamped = Self.clampConnectionsDepth(depth)
        guard clamped != connectionsDepth else { return }
        connectionsDepth = clamped
        loadConnections()
    }

    func connectionsDeeper() { setConnectionsDepth(connectionsDepth + 1) }
    func connectionsShallower() { setConnectionsDepth(connectionsDepth - 1) }

    /// Reveal + focus the Connections leaf for the active note.
    func showConnectionsPanel() {
        workspace.activeLeaf = .connections
        loadConnections()
        focusLeafRegionRevealingPane()  // #882: un-hide the pane on reveal
        graphAnnouncer.announce(.status("Connections panel."))
    }

    /// Re-root the leaf on `path` (the row-level / Bases "Show
    /// connections" action). Capture the CURRENT root BEFORE navigating
    /// — `openFile` synchronously moves `selectedFilePath`, so reading
    /// the effective path afterward would push the destination, not the
    /// origin, and lose the first re-root's back step (review round 1
    /// finding 2).
    func reRootConnections(on path: String) {
        // No-op if already rooted here (avoids a self back-step).
        guard connectionsRootPath != path else {
            workspace.activeLeaf = .connections
            focusLeafRegionRevealingPane()  // #882: un-hide the pane on reveal
            return
        }
        // Push BOTH the prior root mode and the note in view, captured
        // BEFORE openFile moves selectedFilePath — so back can restore
        // the exact prior view, including the originally-selected note
        // when the prior mode was follow-selection (review round 3
        // finding 1; round 2 finding 3; round 1 finding 2).
        if let priorEffective = connectionsEffectivePath {
            connectionsBackStack.append((root: connectionsRootPath, effective: priorEffective))
        }
        openFile(path, target: .currentTab)
        connectionsRootPath = path
        workspace.activeLeaf = .connections
        loadConnections()
        focusLeafRegionRevealingPane()  // #882: un-hide the pane on reveal
        // Label only; the authoritative summary follows from the load.
        graphAnnouncer.announce(.reRooted(label: filename(of: path)))
    }

    /// `⌘[`: pop one re-root step, restoring the prior view exactly —
    /// its root mode AND its note (re-selecting the origin note when
    /// returning to follow-mode, so the effective path is the note the
    /// user was actually on, not wherever the selection drifted —
    /// review round 3 finding 1). Returns whether it acted (the key
    /// owner falls through when not re-rooted, round 2 finding 4).
    @discardableResult
    func connectionsBack() -> Bool {
        guard connectionsRootPath != nil, let prior = connectionsBackStack.popLast() else {
            return false
        }
        connectionsRootPath = prior.root
        // Always re-open the note that was in view — restores the
        // selection whether the prior mode was an explicit root or
        // follow-selection.
        openFile(prior.effective, target: .currentTab)
        loadConnections()
        graphAnnouncer.announce(.reRooted(label: filename(of: prior.effective)))
        return true
    }

    /// Create a note materializing a ghost's authored target (spec
    /// §P1-1 item 4). `create_exclusive` is the no-clobber primitive; the
    /// follow-up scan re-resolves the ghost onto the new note, and the
    /// event-driven refresh then updates the neighborhood.
    func createNoteFromGhost(targetRaw: String) {
        guard let session = currentSession else { return }
        let path = Self.ghostNotePath(targetRaw)
        Task { [weak self] in
            let outcome: Result<Void, VaultError> = await Task.detached(
                priority: .userInitiated
            ) {
                do {
                    _ = try session.createExclusive(path: path, content: "")
                    return .success(())
                } catch let e as VaultError { return .failure(e) }
                catch { return .failure(.Io(message: error.localizedDescription)) }
            }.value
            guard let self, self.currentSession === session else { return }
            switch outcome {
            case .success:
                await self.loadFiles()
                self.openFile(path, target: .currentTab)
                self.renamingNode = RenamingNode(path: path, isDirectory: false)
                self.graphAnnouncer.announce(
                    .status("Created note \((path as NSString).lastPathComponent)."))
                // The create bumped the graph generation; refresh the
                // rooted neighborhood so the ghost row updates.
                self.refreshConnectionsIfGraphChanged()
            case .failure(let error):
                self.lastError = self.humanReadable(error)
                self.graphAnnouncer.announce(
                    .error("Couldn't create note: \(self.humanReadable(error))"))
            }
        }
    }

    /// Map an authored ghost target to a vault path: honor an embedded
    /// folder, append `.md` when the author gave no extension.
    nonisolated static func ghostNotePath(_ targetRaw: String) -> String {
        let trimmed = targetRaw.trimmingCharacters(in: .whitespaces)
        let stripped =
            trimmed.hasPrefix("./") ? String(trimmed.dropFirst(2))
            : (trimmed.hasPrefix("/") ? String(trimmed.dropFirst()) : trimmed)
        let last = (stripped as NSString).lastPathComponent
        let hasMarkdownExt = ["md", "markdown", "mdown", "mkd"].contains(
            (last as NSString).pathExtension.lowercased())
        return hasMarkdownExt ? stripped : "\(stripped).md"
    }
}
