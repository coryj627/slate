// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

struct BaseQueryBuilderSheet: View {
    @EnvironmentObject private var appState: AppState
    @ObservedObject var model: BaseQueryBuilderModel

    @State private var folders: [String] = []
    @State private var tags: [String] = []
    @State private var notePaths: [String] = []
    @State private var propertyKeys: [String] = []
    @State private var folderQuery = ""
    @State private var tagQuery = ""
    @State private var noteQuery = ""

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            header
            Divider()
            ScrollView {
                VStack(alignment: .leading, spacing: 18) {
                    sourceSection
                    conditionsSection
                }
                .padding(18)
            }
            Divider()
            footer
        }
        .frame(minWidth: 680, idealWidth: 760, minHeight: 560, idealHeight: 620)
        .background(Color(nsColor: .controlBackgroundColor))
        .task {
            async let loadedFolders = appState.loadAllFolders()
            async let loadedTags = appState.basesLoadTags()
            async let loadedNotes = appState.basesLoadNotePaths()
            async let loadedProperties = appState.basesLoadPropertyKeys()
            folders = await loadedFolders
            tags = await loadedTags
            notePaths = await loadedNotes
            propertyKeys = await loadedProperties
        }
        .onExitCommand { appState.basesCloseQueryBuilder() }
    }

    private var header: some View {
        HStack(spacing: 10) {
            SlateSymbol.base.decorative
                .foregroundStyle(Color(nsColor: .secondaryLabelColor))
            Text("Bases Query Builder")
                .font(.headline)
                .accessibilityAddTraits(.isHeader)
            Spacer()
            Button("Cancel") { appState.basesCloseQueryBuilder() }
                .keyboardShortcut(.cancelAction)
        }
        .padding(.horizontal, 18)
        .padding(.vertical, 12)
    }

    private var sourceSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text("Source")
                .font(.subheadline.weight(.semibold))
                .accessibilityAddTraits(.isHeader)
            Picker("Source", selection: sourceKindBinding) {
                ForEach(BaseQuerySourceKind.allCases) { kind in
                    Text(kind.title).tag(kind)
                }
            }
            .pickerStyle(.radioGroup)
            sourceDetail
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Source picker")
        .accessibilityValue(model.source.accessibilityLabel)
    }

    @ViewBuilder
    private var sourceDetail: some View {
        switch sourceKind {
        case .allNotes, .tasks:
            EmptyView()
        case .folder:
            VStack(alignment: .leading, spacing: 8) {
                TextField("Search folders", text: $folderQuery)
                    .textFieldStyle(.roundedBorder)
                    .accessibilityLabel("Search folders")
                selectableList(
                    rows: filteredFolders,
                    selected: folderValue,
                    label: { $0 },
                    action: { model.source = .folder($0) })
            }
        case .tag:
            VStack(alignment: .leading, spacing: 8) {
                TextField("Search tags", text: $tagQuery)
                    .textFieldStyle(.roundedBorder)
                    .accessibilityLabel("Search tags")
                selectableList(
                    rows: filteredTags,
                    selected: tagValue,
                    label: { $0 },
                    action: { model.source = .tag($0) })
            }
        case .recent:
            Stepper(value: recentDaysBinding, in: 1...365) {
                Text("Recently edited: \(recentDays) days")
            }
            .accessibilityLabel("Recently edited days")
            .accessibilityValue("\(recentDays)")
        case .linked:
            VStack(alignment: .leading, spacing: 8) {
                TextField("Search notes", text: $noteQuery)
                    .textFieldStyle(.roundedBorder)
                    .accessibilityLabel("Search notes")
                selectableList(
                    rows: filteredNotes,
                    selected: linkedPath,
                    label: { $0 },
                    action: { model.source = .linked(fromPath: $0) })
            }
        }
    }

    private var conditionsSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Text("Conditions")
                    .font(.subheadline.weight(.semibold))
                    .accessibilityAddTraits(.isHeader)
                Spacer()
                Picker("Combined with", selection: combinatorBinding) {
                    Text("ALL").tag(BaseQueryCombinator.all)
                    Text("ANY").tag(BaseQueryCombinator.any)
                }
                .pickerStyle(.segmented)
                .frame(width: 180)
                Button {
                    appState.basesBuilderAddCondition()
                } label: {
                    SlateSymbol.addProperty.label("Add")
                }
                .help("Add condition")
                Button {
                    appState.basesBuilderAddGroup()
                } label: {
                    SlateSymbol.addProperty.label("Group")
                }
                .help("Add group")
            }
            .accessibilityValue(model.conditionsListAccessibilityValue)

            VStack(spacing: 8) {
                ForEach(Array(model.rows.enumerated()), id: \.offset) { index, row in
                    rowView(row: row, index: index)
                }
            }
            .accessibilityElement(children: .contain)
            .accessibilityLabel("Conditions list")
            .accessibilityValue(model.conditionsListAccessibilityValue)
        }
    }

    @ViewBuilder
    private func rowView(row: BaseQueryBuilderRow, index: Int) -> some View {
        switch row {
        case .condition(let condition):
            conditionRow(condition, index: index)
        case .group(let group):
            groupRow(group, index: index)
        case .advanced(let rawExpression, _):
            advancedRow(rawExpression, index: index)
        }
    }

    private func conditionRow(_ condition: BaseQueryCondition, index: Int) -> some View {
        let rowLabel = BaseQueryBuilderRow.condition(condition).accessibilityLabel(index: index)
        return HStack(spacing: 8) {
            Button {
                model.selectedRowIndex = index
            } label: {
                Text("\(index + 1)")
                    .font(.caption.monospacedDigit())
                    .foregroundStyle(Color(nsColor: .secondaryLabelColor))
                    .frame(width: 24, alignment: .trailing)
            }
            .buttonStyle(.borderless)
            .help("Select condition")
            .accessibilityLabel("Select condition \(index + 1)")
            Picker("Property", selection: conditionPropertyBinding(index: index)) {
                ForEach(propertyChoices, id: \.self) { property in
                    Text(property.accessibilityName).tag(property)
                }
            }
            .frame(minWidth: 150)
            Picker("Operator", selection: conditionOperatorBinding(index: index)) {
                ForEach(operatorChoices, id: \.self) { op in
                    Text(op.accessibilityName).tag(op)
                }
            }
            .frame(minWidth: 150)
            TextField("Value", text: conditionValueBinding(index: index))
                .textFieldStyle(.roundedBorder)
                .frame(minWidth: 150)
            rowButtons(index: index)
        }
        .padding(8)
        .background(rowBackground(index: index))
        .accessibilityElement(children: .contain)
        .accessibilityLabel(rowLabel)
    }

    private func groupRow(_ group: BaseQueryConditionGroup, index: Int) -> some View {
        let rowLabel = BaseQueryBuilderRow.group(group).accessibilityLabel(index: index)
        return VStack(alignment: .leading, spacing: 8) {
            HStack {
                Button {
                    model.selectedRowIndex = index
                } label: {
                    Text(rowLabel)
                        .font(.body.weight(.medium))
                }
                .buttonStyle(.borderless)
                .help("Select group")
                .accessibilityLabel("Select group \(index + 1)")
                Picker("Group combinator", selection: groupCombinatorBinding(index: index)) {
                    Text("ALL").tag(BaseQueryCombinator.all)
                    Text("ANY").tag(BaseQueryCombinator.any)
                    Text("NONE").tag(BaseQueryCombinator.none)
                }
                .pickerStyle(.segmented)
                .frame(width: 210)
                Spacer()
                Button {
                    model.perform(.addConditionToGroup(index: index))
                } label: {
                    SlateSymbol.addProperty.image(label: "Add condition to group")
                }
                .help("Add condition to group")
                rowButtons(index: index)
            }
            ForEach(Array(group.rows.enumerated()), id: \.offset) { childIndex, child in
                groupConditionRow(groupIndex: index, childIndex: childIndex, row: child)
            }
        }
        .padding(8)
        .background(rowBackground(index: index))
        .accessibilityElement(children: .contain)
        .accessibilityLabel(rowLabel)
    }

    @ViewBuilder
    private func groupConditionRow(
        groupIndex: Int,
        childIndex: Int,
        row: BaseQueryBuilderRow
    ) -> some View {
        switch row {
        case .condition(let condition):
            HStack(spacing: 8) {
                Button {
                    model.selectedRowIndex = groupIndex
                } label: {
                    Text("\(groupIndex + 1).\(childIndex + 1)")
                        .font(.caption.monospacedDigit())
                        .foregroundStyle(Color(nsColor: .secondaryLabelColor))
                        .frame(width: 44, alignment: .trailing)
                }
                .buttonStyle(.borderless)
                .help("Select group")
                .accessibilityLabel(
                    "Select group \(groupIndex + 1), condition \(childIndex + 1)")
                Picker(
                    "Property",
                    selection: groupConditionPropertyBinding(
                        groupIndex: groupIndex,
                        childIndex: childIndex)
                ) {
                    ForEach(propertyChoices, id: \.self) { property in
                        Text(property.accessibilityName).tag(property)
                    }
                }
                .frame(minWidth: 150)
                Picker(
                    "Operator",
                    selection: groupConditionOperatorBinding(
                        groupIndex: groupIndex,
                        childIndex: childIndex)
                ) {
                    ForEach(operatorChoices, id: \.self) { op in
                        Text(op.accessibilityName).tag(op)
                    }
                }
                .frame(minWidth: 150)
                TextField(
                    "Value",
                    text: groupConditionValueBinding(groupIndex: groupIndex, childIndex: childIndex)
                )
                .textFieldStyle(.roundedBorder)
                .frame(minWidth: 150)
                Button {
                    model.perform(
                        .removeConditionFromGroup(
                            groupIndex: groupIndex,
                            conditionIndex: childIndex))
                } label: {
                    SlateSymbol.trash.image(label: "Remove condition from group")
                }
                .buttonStyle(.borderless)
                .help("Remove condition from group")
            }
            .padding(.leading, 28)
            .accessibilityElement(children: .contain)
            .accessibilityLabel(
                "Group \(groupIndex + 1) condition \(childIndex + 1): "
                    + condition.accessibilityPhrase)
        case .group, .advanced:
            Text(row.accessibilityLabel(index: childIndex))
                .font(.callout)
                .foregroundStyle(Color(nsColor: .secondaryLabelColor))
                .padding(.leading, 28)
                .accessibilityLabel(row.accessibilityLabel(index: childIndex))
        }
    }

    private func advancedRow(_ rawExpression: String, index: Int) -> some View {
        let rowLabel = BaseQueryBuilderRow.advanced(
            rawExpression: rawExpression,
            filterJSON: nil
        ).accessibilityLabel(index: index)
        return HStack(spacing: 8) {
            Text(rowLabel)
                .foregroundStyle(Color(nsColor: .secondaryLabelColor))
            Spacer()
            rowButtons(index: index)
        }
        .padding(8)
        .background(rowBackground(index: index))
        .contentShape(Rectangle())
        .onTapGesture { model.selectedRowIndex = index }
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Advanced condition: \(rawExpression)")
        .accessibilityAddTraits(.isButton)
        .accessibilityAction(named: Text("Select Row")) {
            model.selectedRowIndex = index
        }
    }

    private func rowButtons(index: Int) -> some View {
        HStack(spacing: 4) {
            Button {
                model.perform(.editCondition(index: index))
            } label: {
                SlateSymbol.rename.image(label: "Edit")
            }
            .help("Edit condition")
            Button {
                model.perform(.removeCondition(index: index))
            } label: {
                SlateSymbol.trash.image(label: "Remove")
            }
            .help("Remove condition")
        }
        .buttonStyle(.borderless)
    }

    private var footer: some View {
        HStack {
            Text(model.conditionsListAccessibilityValue)
                .foregroundStyle(Color(nsColor: .secondaryLabelColor))
            Spacer()
            Button("Done") { appState.basesCloseQueryBuilder() }
                .keyboardShortcut(.defaultAction)
        }
        .padding(.horizontal, 18)
        .padding(.vertical, 12)
    }

    private func selectableList<Row: Hashable>(
        rows: [Row],
        selected: Row?,
        label: @escaping (Row) -> String,
        action: @escaping (Row) -> Void
    ) -> some View {
        let visible = Array(rows.prefix(8))
        return VStack(alignment: .leading, spacing: 4) {
            ForEach(visible, id: \.self) { row in
                Button {
                    action(row)
                } label: {
                    HStack {
                        Text(label(row))
                        Spacer()
                        if selected == row {
                            SlateSymbol.checkmark.decorative
                        }
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
                .buttonStyle(.plain)
                .padding(.vertical, 4)
                .padding(.horizontal, 8)
                .background(
                    selected == row
                        ? Color(nsColor: .selectedContentBackgroundColor).opacity(0.22)
                        : Color.clear
                )
                .accessibilityLabel(label(row))
                .accessibilityIsSelected(selected == row)
            }
        }
    }

    private func rowBackground(index: Int) -> Color {
        model.selectedRowIndex == index
            ? Color(nsColor: .selectedContentBackgroundColor).opacity(0.18)
            : Color(nsColor: .separatorColor).opacity(0.12)
    }

    private var sourceKind: BaseQuerySourceKind {
        sourceKindBinding.wrappedValue
    }

    private var sourceKindBinding: Binding<BaseQuerySourceKind> {
        Binding(
            get: { BaseQuerySourceKind(source: model.source) },
            set: { model.source = $0.defaultSource(current: model.source) })
    }

    private var combinatorBinding: Binding<BaseQueryCombinator> {
        Binding(
            get: { model.combinator },
            set: { model.combinator = $0 })
    }

    private var folderValue: String? {
        if case .folder(let path) = model.source { return path }
        return nil
    }

    private var linkedPath: String? {
        if case .linked(let path) = model.source { return path }
        return nil
    }

    private var tagValue: String? {
        if case .tag(let tag) = model.source { return tag }
        return nil
    }

    private var recentDays: Int {
        if case .recent(let days) = model.source { return days }
        return 7
    }

    private var recentDaysBinding: Binding<Int> {
        Binding(
            get: { recentDays },
            set: { model.source = .recent(days: $0) })
    }

    private var filteredFolders: [String] {
        filter(folders, query: folderQuery)
    }

    private var filteredTags: [String] {
        filter(tags, query: tagQuery)
    }

    private var filteredNotes: [String] {
        filter(notePaths, query: noteQuery)
    }

    private func filter(_ rows: [String], query: String) -> [String] {
        let trimmed = query.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        guard !trimmed.isEmpty else { return rows }
        return rows.filter { $0.lowercased().contains(trimmed) }
    }

    private var propertyChoices: [BaseQueryProperty] {
        var properties: [BaseQueryProperty] = [
            .file(.name), .file(.path), .file(.folder), .file(.mtime),
            .file(.size), .file(.inDegree), .file(.outDegree),
        ]
        let noteKeys = propertyKeys.isEmpty ? ["status", "priority"] : propertyKeys
        properties.append(contentsOf: noteKeys.map(BaseQueryProperty.note))
        if model.source == .tasks {
            properties.append(contentsOf: BaseQueryTaskField.allCases.map(BaseQueryProperty.task))
        }
        return properties
    }

    private var operatorChoices: [BaseQueryOperator] {
        [
            .equals, .notEquals, .contains, .startsWith, .endsWith, .isEmpty,
            .hasTag, .hasLink, .greaterThan, .greaterThanOrEqual, .lessThan,
            .lessThanOrEqual, .matches,
        ]
    }

    private func groupCombinatorBinding(index: Int) -> Binding<BaseQueryCombinator> {
        Binding(
            get: {
                guard model.rows.indices.contains(index),
                    case .group(let group) = model.rows[index]
                else { return .all }
                return group.combinator
            },
            set: { combinator in
                model.perform(.setGroupCombinator(index: index, combinator: combinator))
            })
    }

    private func conditionPropertyBinding(index: Int) -> Binding<BaseQueryProperty> {
        Binding(
            get: {
                guard model.rows.indices.contains(index) else { return .note("status") }
                guard case .condition(let condition) = model.rows[index] else { return .note("status") }
                return condition.property
            },
            set: { property in
                guard model.rows.indices.contains(index) else { return }
                guard case .condition(var condition) = model.rows[index] else { return }
                condition.property = property
                condition.replaceValueEditingText(condition.value.editingText)
                model.rows[index] = .condition(condition)
            })
    }

    private func conditionOperatorBinding(index: Int) -> Binding<BaseQueryOperator> {
        Binding(
            get: {
                guard model.rows.indices.contains(index) else { return .equals }
                guard case .condition(let condition) = model.rows[index] else { return .equals }
                return condition.op
            },
            set: { op in
                guard model.rows.indices.contains(index) else { return }
                guard case .condition(var condition) = model.rows[index] else { return }
                condition.op = op
                model.rows[index] = .condition(condition)
            })
    }

    private func conditionValueBinding(index: Int) -> Binding<String> {
        Binding(
            get: {
                guard model.rows.indices.contains(index) else { return "" }
                guard case .condition(let condition) = model.rows[index] else { return "" }
                return condition.value.editingText
            },
            set: { value in
                guard model.rows.indices.contains(index) else { return }
                guard case .condition(var condition) = model.rows[index] else { return }
                condition.replaceValueEditingText(value)
                model.rows[index] = .condition(condition)
            })
    }

    private func groupConditionPropertyBinding(
        groupIndex: Int,
        childIndex: Int
    ) -> Binding<BaseQueryProperty> {
        Binding(
            get: {
                guard let condition = groupCondition(groupIndex: groupIndex, childIndex: childIndex)
                else { return .note("status") }
                return condition.property
            },
            set: { property in
                updateGroupCondition(groupIndex: groupIndex, childIndex: childIndex) { condition in
                    condition.property = property
                    condition.replaceValueEditingText(condition.value.editingText)
                }
            })
    }

    private func groupConditionOperatorBinding(
        groupIndex: Int,
        childIndex: Int
    ) -> Binding<BaseQueryOperator> {
        Binding(
            get: {
                guard let condition = groupCondition(groupIndex: groupIndex, childIndex: childIndex)
                else { return .equals }
                return condition.op
            },
            set: { op in
                updateGroupCondition(groupIndex: groupIndex, childIndex: childIndex) { condition in
                    condition.op = op
                }
            })
    }

    private func groupConditionValueBinding(groupIndex: Int, childIndex: Int) -> Binding<String> {
        Binding(
            get: {
                guard let condition = groupCondition(groupIndex: groupIndex, childIndex: childIndex)
                else { return "" }
                return condition.value.editingText
            },
            set: { value in
                updateGroupCondition(groupIndex: groupIndex, childIndex: childIndex) { condition in
                    condition.replaceValueEditingText(value)
                }
            })
    }

    private func groupCondition(groupIndex: Int, childIndex: Int) -> BaseQueryCondition? {
        guard model.rows.indices.contains(groupIndex),
            case .group(let group) = model.rows[groupIndex],
            group.rows.indices.contains(childIndex),
            case .condition(let condition) = group.rows[childIndex]
        else { return nil }
        return condition
    }

    private func updateGroupCondition(
        groupIndex: Int,
        childIndex: Int,
        mutate: (inout BaseQueryCondition) -> Void
    ) {
        guard model.rows.indices.contains(groupIndex),
            case .group(var group) = model.rows[groupIndex],
            group.rows.indices.contains(childIndex),
            case .condition(var condition) = group.rows[childIndex]
        else { return }
        mutate(&condition)
        group.rows[childIndex] = .condition(condition)
        model.rows[groupIndex] = .group(group)
    }
}

private enum BaseQuerySourceKind: String, CaseIterable, Identifiable {
    case allNotes
    case folder
    case tag
    case recent
    case linked
    case tasks

    var id: String { rawValue }

    var title: String {
        switch self {
        case .allNotes: return "All notes"
        case .folder: return "Folder"
        case .tag: return "Tag"
        case .recent: return "Recently edited"
        case .linked: return "Linked from note"
        case .tasks: return "Tasks"
        }
    }

    init(source: BaseQuerySource) {
        switch source {
        case .allNotes: self = .allNotes
        case .folder: self = .folder
        case .tag: self = .tag
        case .recent: self = .recent
        case .linked: self = .linked
        case .tasks: self = .tasks
        }
    }

    func defaultSource(current: BaseQuerySource) -> BaseQuerySource {
        switch self {
        case .allNotes:
            return .allNotes
        case .folder:
            if case .folder = current { return current }
            return .folder("")
        case .tag:
            if case .tag = current { return current }
            return .tag("")
        case .recent:
            if case .recent = current { return current }
            return .recent(days: 7)
        case .linked:
            if case .linked = current { return current }
            return .linked(fromPath: "")
        case .tasks:
            return .tasks
        }
    }
}
