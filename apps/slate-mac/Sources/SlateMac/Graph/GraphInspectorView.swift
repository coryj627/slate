// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// The graph tab's inspector (Milestone P, P2-4 #560) — a trailing panel,
/// available in both modes, with four sections: **Filters** (shared with
/// the Table's node set), **Groups** (query→colour+ring rules,
/// first-match-wins, colour never the sole channel), **Display** (arrows,
/// text fade, node size, link thickness), and **Forces** (four sliders
/// that live-retune the layout). Every control is a labelled, standard,
/// keyboard-operable control; the panel is one `.contain` AX group.
struct GraphInspectorView: View {
    @EnvironmentObject private var appState: AppState

    var body: some View {
        Form {
            filtersSection
            groupsSection
            displaySection
            forcesSection
        }
        .formStyle(.grouped)
        .frame(minWidth: 260)
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Graph inspector")
    }

    // MARK: Filters (shared node set with the Table)

    private var filtersSection: some View {
        Section("Filters") {
            TextField("Filter by name", text: nameBinding)
                .accessibilityLabel("Filter graph by note name")
            Toggle("Attachments", isOn: backendToggle(\.includeAttachments))
                .accessibilityHint("Include attachment nodes.")
            Toggle("Unresolved", isOn: backendToggle(\.includeGhosts))
                .accessibilityHint("Include unresolved link targets.")
            Toggle("Orphans only", isOn: backendToggle(\.orphansOnly))
                .accessibilityHint("Show only notes with no links in or out.")
        }
    }

    private var nameBinding: Binding<String> {
        Binding(
            get: { appState.graphTableTextFilter },
            set: {
                appState.graphTableTextFilter = $0
                appState.scheduleGraphConfigSave()
            })
    }

    private func backendToggle(_ key: WritableKeyPath<GraphFilter, Bool>) -> Binding<Bool> {
        Binding(
            get: { appState.graphTableFilter[keyPath: key] },
            set: { newValue in
                var f = appState.graphTableFilter
                f[keyPath: key] = newValue
                appState.setGraphTableFilter(f)
                appState.scheduleGraphConfigSave()
            })
    }

    // MARK: Groups (query → colour + ring, first-match-wins)

    private var groupsSection: some View {
        Section("Groups") {
            if appState.graphConfig.groups.isEmpty {
                Text("No groups. Add one to colour matching nodes.")
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .font(.callout)
            }
            ForEach(Array(appState.graphConfig.groups.enumerated()), id: \.offset) { index, group in
                groupRow(index: index, group: group)
            }
            Button {
                appState.addGraphGroup(query: "")
            } label: {
                SlateSymbol.addProperty.label("Add Group")
            }
            .accessibilityHint("Add a colour rule that highlights nodes whose name matches a query.")
        }
    }

    private func groupRow(index: Int, group: GraphGroup) -> some View {
        HStack(spacing: Tokens.Spacing.sm) {
            TextField("Query", text: groupQueryBinding(index))
                .accessibilityLabel("Group \(index + 1) query")
            Picker("Colour", selection: groupColorBinding(index)) {
                ForEach(GraphColorToken.allCases, id: \.self) { Text($0.title).tag($0) }
            }
            .labelsHidden()
            .accessibilityLabel("Group \(index + 1) colour")
            Picker("Ring", selection: groupRingBinding(index)) {
                ForEach(GraphRingStyle.allCases, id: \.self) { Text($0.rawValue.capitalized).tag($0) }
            }
            .labelsHidden()
            .accessibilityLabel("Group \(index + 1) ring style")
            Button {
                var groups = appState.graphConfig.groups
                guard groups.indices.contains(index) else { return }
                groups.remove(at: index)
                appState.setGraphGroups(groups)
            } label: {
                // Decorative glyph; the Button's accessibilityLabel names it.
                // 28×28 pins the HIG macOS default click target (matches the
                // note-properties header glyph buttons).
                SlateSymbol.trash.decorative
                    .frame(minWidth: 28, minHeight: 28)
                    .contentShape(Rectangle())
            }
            .buttonStyle(.borderless)
            .accessibilityLabel("Remove group \(index + 1)")
        }
    }

    private func groupQueryBinding(_ index: Int) -> Binding<String> {
        Binding(
            get: { appState.graphConfig.groups[safe: index]?.query ?? "" },
            set: { newValue in
                var groups = appState.graphConfig.groups
                guard groups.indices.contains(index) else { return }
                groups[index].query = newValue
                appState.setGraphGroups(groups)
            })
    }

    // MARK: Display

    private var displaySection: some View {
        Section("Display") {
            Toggle("Arrows", isOn: displayBinding(\.arrows))
                .accessibilityHint("Draw arrowheads on directed links.")
            labeledSlider(
                "Text fade", value: displayBinding(\.textFadeZoom), range: 0.1...2.0,
                hint: "Zoom level below which node labels hide.")
            labeledSlider(
                "Node size", value: displayBinding(\.nodeSizeMultiplier), range: 0.5...2.0,
                hint: "Multiplier on node circle size.")
            labeledSlider(
                "Link thickness", value: displayBinding(\.linkThickness), range: 0.5...4.0,
                hint: "Edge line width.")
        }
    }

    private func displayBinding<V>(_ key: WritableKeyPath<GraphDisplay, V>) -> Binding<V> {
        Binding(
            get: { appState.graphConfig.display[keyPath: key] },
            set: {
                var d = appState.graphConfig.display
                d[keyPath: key] = $0
                appState.setGraphDisplay(d)
            })
    }

    // MARK: Forces

    private var forcesSection: some View {
        Section("Forces") {
            labeledSlider(
                "Center", value: forceBinding(\.center), range: 0...1,
                hint: "Gravity pulling the graph toward the centre.")
            labeledSlider(
                "Repel", value: forceBinding(\.repel), range: 0...1,
                hint: "How strongly nodes push each other apart.")
            labeledSlider(
                "Link force", value: forceBinding(\.link), range: 0...1,
                hint: "How strongly linked nodes pull together.")
            labeledSlider(
                "Link distance", value: forceBinding(\.linkDistance), range: 0...1,
                hint: "The ideal length of a link.")
        }
    }

    private func forceBinding(_ key: WritableKeyPath<GraphForcesConfig, Double>) -> Binding<Double> {
        Binding(
            get: { appState.graphConfig.forces[keyPath: key] },
            set: {
                var f = appState.graphConfig.forces
                f[keyPath: key] = $0
                appState.setGraphForces(f)
            })
    }

    // MARK: Shared control builders

    private func labeledSlider(
        _ title: String, value: Binding<Double>, range: ClosedRange<Double>, hint: String
    ) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            HStack {
                Text(title)
                Spacer()
                Text(String(format: "%.2f", value.wrappedValue))
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .monospacedDigit()
            }
            Slider(value: value, in: range)
                .accessibilityLabel(title)
                .accessibilityValue(String(format: "%.2f", value.wrappedValue))
                .accessibilityHint(hint)
        }
    }

    private func groupColorBinding(_ index: Int) -> Binding<GraphColorToken> {
        Binding(
            get: { appState.graphConfig.groups[safe: index]?.colorToken ?? .blue },
            set: { newValue in
                var groups = appState.graphConfig.groups
                guard groups.indices.contains(index) else { return }
                groups[index].colorToken = newValue
                appState.setGraphGroups(groups)
            })
    }

    private func groupRingBinding(_ index: Int) -> Binding<GraphRingStyle> {
        Binding(
            get: { appState.graphConfig.groups[safe: index]?.ringStyle ?? .solid },
            set: { newValue in
                var groups = appState.graphConfig.groups
                guard groups.indices.contains(index) else { return }
                groups[index].ringStyle = newValue
                appState.setGraphGroups(groups)
            })
    }
}

extension Array {
    fileprivate subscript(safe index: Int) -> Element? {
        indices.contains(index) ? self[index] : nil
    }
}
