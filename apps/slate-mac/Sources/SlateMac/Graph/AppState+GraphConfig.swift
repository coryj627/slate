// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Graph-tab config lifecycle (Milestone P, P2-4 #560): load `.slate/
/// graph.json` when the vault activates, apply the persisted filter on
/// each plain Graph-tab open, and persist changes (debounced, off-main,
/// per-vault serialized) as the inspector edits Filters / Groups /
/// Display / Forces.
extension AppState {
    /// The ONE client-side name-filter predicate (case/diacritic-
    /// insensitive label substring) that BOTH the Table and the Diagram
    /// apply — the single source of truth (spec §P2-4 "filter
    /// equivalence"). An empty needle matches everything.
    nonisolated static func graphNameMatches(_ label: String, needle: String) -> Bool {
        let n = needle.trimmingCharacters(in: .whitespaces)
        guard !n.isEmpty else { return true }
        return label.range(of: n, options: [.caseInsensitive, .diacriticInsensitive]) != nil
    }

    /// Load the config OBJECT once per vault (idempotent). The eager load
    /// at vault activation normally beats every caller here, so this is a
    /// safety net for any activation path that skipped it.
    func ensureGraphConfigLoaded() {
        guard let root = currentVaultURL, graphConfigVaultURL != root else { return }
        loadGraphConfig()
    }

    /// Read the vault's `graph.json` into memory: sets `graphConfig`, the
    /// migrated Connections `connectionsDepth`, the writable flag, and
    /// stamps `graphConfigVaultURL`. Does NOT touch the live Table filter —
    /// that's `applyPersistedGraphFilter()`, run on each plain Graph-tab
    /// open so a transient preset never becomes sticky (finding 4).
    ///
    /// A read/parse/version failure keeps DEFAULTS in memory but marks the
    /// config read-only (`graphConfigWritable = false`) so a later save can
    /// never clobber whatever a newer Slate — or a human — put on disk
    /// (finding 2). The file itself is never rewritten on a read.
    func loadGraphConfig() {
        guard let root = currentVaultURL else { return }
        graphConfigVaultURL = root
        do {
            let cfg = try GraphConfigStore(vaultRoot: root).read()
            graphConfig = cfg
            connectionsDepth = Self.clampConnectionsDepth(cfg.connectionsDepth)
            graphConfigWritable = true
        } catch {
            graphConfig = .default
            connectionsDepth = 1
            graphConfigWritable = false
        }
    }

    /// Apply the persisted backend + name filter to the live Table state
    /// (which the Diagram also reads) and CLEAR any transient preset kind
    /// filter. Called on each PLAIN Graph-tab activation — one with no
    /// pending preset — so close→reopen and preset→plain BOTH restore the
    /// saved view rather than leaving a transient preset installed. The
    /// `.ghost` kind filter is a preset-only overlay, never persisted, so a
    /// plain open must drop it too (finding 4).
    func applyPersistedGraphFilter() {
        graphTableFilter = graphConfig.filters.backend
        graphTableTextFilter = graphConfig.filters.nameQuery
        graphTableKindFilter = nil
    }

    /// Snapshot the live filter / depth into `graphConfig` and persist the
    /// whole aggregate, debounced (rapid slider drags coalesce to one
    /// write) and off the main actor via the serializing `GraphConfigWriter`
    /// (so overlapping saves can't lose an update — finding 3).
    ///
    /// Refuses to save unless the current vault's config is actually loaded
    /// (`graphConfigVaultURL == currentVaultURL`) and writable — this is
    /// what stops a stale cross-vault aggregate from being written (finding
    /// 1) and honours the read-only protection (finding 2).
    func scheduleGraphConfigSave() {
        guard let root = currentVaultURL, graphConfigVaultURL == root, graphConfigWritable
        else { return }
        graphConfig.filters = GraphFilterConfig(
            includeAttachments: graphTableFilter.includeAttachments,
            includeGhosts: graphTableFilter.includeGhosts,
            orphansOnly: graphTableFilter.orphansOnly,
            nameQuery: graphTableTextFilter)
        graphConfig.connectionsDepth = Self.clampConnectionsDepth(connectionsDepth)
        let snapshot = graphConfig
        // Coalesce ONLY this vault's still-pending save (per-vault keyed);
        // a pending save for a DIFFERENT vault keeps its own entry and runs
        // to completion, so a fast vault switch loses no edit and no stale
        // same-vault task lingers (finding 3). The write itself is
        // serialized by `GraphConfigWriter`.
        graphConfigSaveTasks[root]?.cancel()
        // Strictly-increasing per-vault generation, NEVER reset — the
        // writer actor's monotonic gate depends on it (an older generation
        // can never clobber a newer one even if the actor reorders the two
        // queued writes; finding 3 round-3).
        let gen = (graphConfigSaveGen[root] ?? 0) + 1
        graphConfigSaveGen[root] = gen
        graphConfigSaveTasks[root] = Task { [weak self] in
            try? await Task.sleep(nanoseconds: 400_000_000)
            guard !Task.isCancelled else { return }
            await GraphConfigWriter.shared.write(vault: root, config: snapshot, generation: gen)
            // Clear only the TASK entry, only if a newer same-vault save
            // hasn't replaced us (a task past its sleep can't be cancelled,
            // so it must not drop its successor's entry). `graphConfigSaveGen`
            // stays monotonic — never removed.
            guard let self, self.graphConfigSaveGen[root] == gen else { return }
            self.graphConfigSaveTasks.removeValue(forKey: root)
        }
    }

    // MARK: Inspector mutators

    /// Retune the forces: mutate the config, re-heat the live layout
    /// (`set_forces`), announce the CHANGED control's value (debounced, so a
    /// slider drag coalesces to its resting value, not one post per tick),
    /// arm the settled-state announcement the renderer fires at
    /// convergence, and persist (finding 8).
    func setGraphForces(_ forces: GraphForcesConfig) {
        let old = graphConfig.forces
        graphConfig.forces = forces
        if let session = graphDiagramModel?.session {
            session.setForces(forces: forces.layoutForces)
            // Arm the settled-state announcement ONLY when a live diagram
            // will actually re-heat + converge. Without a diagram (Table
            // mode) there's no settle, so a stale flag must never survive to
            // announce "settled" on a LATER initial build (finding 8).
            graphForcesSettlePending = true
        }
        if let phrase = Self.forcesChangePhrase(old: old, new: forces) {
            graphAnnouncer.announceForceValue(phrase)
        }
        scheduleGraphConfigSave()
    }

    /// The spoken "control value" for the ONE force that changed (nil if
    /// none did) — e.g. "Repel force 70 percent". Pure + `nonisolated` so
    /// it's unit-testable off the main actor.
    nonisolated static func forcesChangePhrase(
        old: GraphForcesConfig, new: GraphForcesConfig
    ) -> String? {
        func pct(_ v: Double) -> Int { Int((v * 100).rounded()) }
        if new.center != old.center { return "Center force \(pct(new.center)) percent" }
        if new.repel != old.repel { return "Repel force \(pct(new.repel)) percent" }
        if new.link != old.link { return "Link force \(pct(new.link)) percent" }
        if new.linkDistance != old.linkDistance {
            return "Link distance \(pct(new.linkDistance)) percent"
        }
        return nil
    }

    /// Update the display knobs (arrows / text fade / node size / link
    /// thickness). The renderer observes `graphConfig` and re-renders.
    func setGraphDisplay(_ display: GraphDisplay) {
        graphConfig.display = display
        scheduleGraphConfigSave()
    }

    /// Replace the colour-group rules (add / remove / recolour). New
    /// groups take the next ring style in rotation so colour is never the
    /// sole channel.
    func setGraphGroups(_ groups: [GraphGroup]) {
        graphConfig.groups = groups
        scheduleGraphConfigSave()
    }

    /// Append a group for `query`, auto-assigning the next ring style in
    /// rotation (solid → dashed → double → dotted) and the next palette
    /// colour, so successive groups differ on BOTH channels.
    func addGraphGroup(query: String) {
        let idx = graphConfig.groups.count
        let ring = GraphRingStyle.allCases[idx % GraphRingStyle.allCases.count]
        let color = GraphColorToken.allCases[idx % GraphColorToken.allCases.count]
        setGraphGroups(graphConfig.groups + [GraphGroup(query: query, colorToken: color, ringStyle: ring)])
    }

    /// Persist the last-used projection mode (restored on the next open).
    func setGraphMode(_ mode: GraphTabMode) {
        graphConfig.mode = mode
        scheduleGraphConfigSave()
    }
}
