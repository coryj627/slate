// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Graph-tab config lifecycle (Milestone P, P2-4 #560): load `.slate/
/// graph.json` when the tab activates, apply the persisted filter / mode
/// / depth to their existing live state, and persist changes (debounced,
/// off-main) as the inspector edits Filters / Groups / Display / Forces.
extension AppState {
    /// Read the vault's graph config and apply the persisted filter and
    /// Connections depth to their live homes (the diagram reads Groups /
    /// Display / Forces straight off `graphConfig`; the container restores
    /// `graphConfig.mode`). Malformed JSON leaves the defaults + surfaces
    /// nothing destructive — the file is never rewritten on a read.
    /// The ONE client-side name-filter predicate (case/diacritic-
    /// insensitive label substring) that BOTH the Table and the Diagram
    /// apply — the single source of truth (spec §P2-4 "filter
    /// equivalence"). An empty needle matches everything.
    nonisolated static func graphNameMatches(_ label: String, needle: String) -> Bool {
        let n = needle.trimmingCharacters(in: .whitespaces)
        guard !n.isEmpty else { return true }
        return label.range(of: n, options: [.caseInsensitive, .diacriticInsensitive]) != nil
    }

    /// Load the config once per vault (idempotent — a per-activate reload
    /// would clobber a debounced, unsaved edit on a tab-switch).
    func ensureGraphConfigLoaded() {
        guard currentVaultURL != nil, graphConfigVaultURL != currentVaultURL else { return }
        loadGraphConfig()
        graphConfigVaultURL = currentVaultURL
    }

    func loadGraphConfig() {
        guard let root = currentVaultURL else { return }
        let cfg = (try? GraphConfigStore(vaultRoot: root).read()) ?? .default
        graphConfig = cfg
        connectionsDepth = min(3, max(1, cfg.connectionsDepth))
        // Filters are shared with the Table's live state (single source of
        // truth); restore the persisted values — UNLESS a preset is
        // driving the view. `openGraphPreset` sets its own filter and a
        // pending-preset marker just before activation, so applying the
        // persisted filter here would clobber the preset's (P1-3 preset
        // tests). The preset load consumes the marker; a later plain
        // activate then restores the persisted filter normally.
        guard graphTablePendingPreset == nil else { return }
        graphTableFilter = cfg.filters.backend
        graphTableTextFilter = cfg.filters.nameQuery
    }

    /// Snapshot the live filter / depth into `graphConfig` and persist the
    /// whole aggregate, debounced (rapid slider drags coalesce to one
    /// write) and off the main actor. `graph.json` is single-writer, so
    /// no lock.
    func scheduleGraphConfigSave() {
        graphConfig.filters = GraphFilterConfig(
            includeAttachments: graphTableFilter.includeAttachments,
            includeGhosts: graphTableFilter.includeGhosts,
            orphansOnly: graphTableFilter.orphansOnly,
            nameQuery: graphTableTextFilter)
        graphConfig.connectionsDepth = min(3, max(1, connectionsDepth))
        guard let root = currentVaultURL else { return }
        let snapshot = graphConfig
        graphConfigSaveTask?.cancel()
        graphConfigSaveTask = Task { [weak self] in
            try? await Task.sleep(nanoseconds: 400_000_000)
            guard !Task.isCancelled else { return }
            await Task.detached(priority: .utility) {
                try? GraphConfigStore(vaultRoot: root).write(snapshot)
            }.value
            _ = self
        }
    }

    // MARK: Inspector mutators

    /// Retune the forces: mutate the config, re-heat the live layout
    /// (`set_forces`), announce the resulting condition (debounced), and
    /// persist. The renderer observes `graphConfig` and re-settles.
    func setGraphForces(_ forces: GraphForcesConfig) {
        graphConfig.forces = forces
        graphDiagramModel?.session.setForces(forces: forces.layoutForces)
        graphAnnouncer.announce(.status("Forces updated; layout settling."))
        scheduleGraphConfigSave()
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
