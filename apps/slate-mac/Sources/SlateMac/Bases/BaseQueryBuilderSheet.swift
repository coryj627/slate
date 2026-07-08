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
    @State private var formulaName = ""
    @State private var formulaExpression = ""
    @State private var formulaValidation: BaseExpressionValidation?
    @State private var previewSelectedRow: String?
    @State private var previewSelectedCell: AccessibleDataGrid<BaseGridRow>.CellPosition?
    @State private var previewSortState: DataGridSortState?
    @State private var saveAsBasePath = "Queries/New Query.base"
    @State private var savedQueryName = "New query"
    @State private var savedQueryDescription = ""

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            header
            Divider()
            ScrollView {
                VStack(alignment: .leading, spacing: 18) {
                    sourceSection
                    conditionsSection
                    sortGroupSection
                    columnsSection
                    formulasSection
                    previewSection
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
        .onAppear { appState.basesBuilderSchedulePreview(delayNanoseconds: 0) }
        .onChange(of: model.draft) { _, _ in appState.basesBuilderSchedulePreview() }
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

    private var sortGroupSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Text("Sort")
                    .font(.subheadline.weight(.semibold))
                    .accessibilityAddTraits(.isHeader)
                Spacer()
                Button {
                    model.sortKeys.append(BaseQuerySortKey(property: firstPropertyChoice, ascending: true))
                } label: {
                    SlateSymbol.addProperty.label("Add sort")
                }
                .help("Add sort")
            }
            ForEach(Array(model.sortKeys.enumerated()), id: \.offset) { index, _ in
                HStack(spacing: 8) {
                    Picker("Sort property", selection: sortPropertyBinding(index: index)) {
                        ForEach(propertyChoices, id: \.self) { property in
                            Text(property.accessibilityName).tag(property)
                        }
                    }
                    Toggle("Ascending", isOn: sortAscendingBinding(index: index))
                    Button {
                        moveSort(index: index, delta: -1)
                    } label: {
                        SlateSymbol.moveUp.image(label: "Move sort up")
                    }
                    .disabled(index == 0)
                    Button {
                        moveSort(index: index, delta: 1)
                    } label: {
                        SlateSymbol.moveDown.image(label: "Move sort down")
                    }
                    .disabled(index >= model.sortKeys.count - 1)
                    Button {
                        model.sortKeys.remove(at: index)
                    } label: {
                        SlateSymbol.trash.image(label: "Remove sort")
                    }
                }
                .accessibilityElement(children: .contain)
                .accessibilityLabel("Sort \(index + 1)")
            }

            Divider()
            Text("Group")
                .font(.subheadline.weight(.semibold))
                .accessibilityAddTraits(.isHeader)
            HStack(spacing: 8) {
                Picker("Group property", selection: groupPropertyBinding) {
                    ForEach(propertyChoices, id: \.self) { property in
                        Text(property.accessibilityName).tag(property)
                    }
                }
                Toggle("Group ascending", isOn: groupAscendingBinding)
                Button("Clear group") { model.groupBy = nil }
            }
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Sort and group sections")
    }

    private var columnsSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text("Columns")
                .font(.subheadline.weight(.semibold))
                .accessibilityAddTraits(.isHeader)
            Picker("View type", selection: viewTypeBinding) {
                ForEach(BaseQueryViewType.allCases) { viewType in
                    Text(viewType.title).tag(viewType)
                }
            }
            .pickerStyle(.radioGroup)
            ForEach(propertyChoices, id: \.self) { property in
                HStack(spacing: 8) {
                    Toggle("Include column", isOn: columnIncludedBinding(property: property))
                        .accessibilityLabel("Include \(property.accessibilityName)")
                    Text(property.accessibilityName)
                        .frame(minWidth: 96, alignment: .leading)
                    VStack(alignment: .leading, spacing: 3) {
                        Text("Display name")
                            .font(.caption)
                            .foregroundStyle(Color(nsColor: .secondaryLabelColor))
                        TextField(
                            "Display name",
                            text: columnDisplayNameBinding(property: property)
                        )
                        .textFieldStyle(.roundedBorder)
                        .accessibilityLabel("Display name for \(property.accessibilityName)")
                    }
                    .disabled(!isColumnIncluded(property))
                    Button {
                        moveColumn(property: property, delta: -1)
                    } label: {
                        SlateSymbol.moveUp.image(label: "Move column up")
                    }
                    .disabled(!canMoveColumn(property, delta: -1))
                    Button {
                        moveColumn(property: property, delta: 1)
                    } label: {
                        SlateSymbol.moveDown.image(label: "Move column down")
                    }
                    .disabled(!canMoveColumn(property, delta: 1))
                }
            }
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Columns and View type")
    }

    private var formulasSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Text("Formulas")
                    .font(.subheadline.weight(.semibold))
                    .accessibilityAddTraits(.isHeader)
                Spacer()
                if let formulaValidation {
                    let message = formulaValidation.valid
                        ? "Formula valid"
                        : expressionValidationMessage(formulaValidation, fallback: "Formula invalid")
                    if formulaValidation.valid {
                        SlateSymbol.checkmark.label(message)
                    } else {
                        SlateSymbol.warning.label(message)
                            .accessibilityLabel(message)
                    }
                }
            }
            HStack(alignment: .bottom, spacing: 8) {
                VStack(alignment: .leading, spacing: 3) {
                    Text("Formula name")
                        .font(.caption)
                        .foregroundStyle(Color(nsColor: .secondaryLabelColor))
                    TextField("Formula name", text: $formulaName)
                        .textFieldStyle(.roundedBorder)
                        .accessibilityLabel("Formula name")
                }
                VStack(alignment: .leading, spacing: 3) {
                    Text("Expression")
                        .font(.caption)
                        .foregroundStyle(Color(nsColor: .secondaryLabelColor))
                    TextField("Expression", text: $formulaExpression)
                        .textFieldStyle(.roundedBorder)
                        .accessibilityLabel("Formula expression")
                        .onChange(of: formulaExpression) { _, value in
                            validateFormulaExpression(value)
                        }
                }
                Button("Add formula") { addFormula() }
            }
            ForEach(model.formulas) { formula in
                HStack {
                    Text("formula.\(formula.name)")
                    Spacer()
                    Text(formula.expression)
                        .foregroundStyle(Color(nsColor: .secondaryLabelColor))
                    Button {
                        model.removeFormula(named: formula.name)
                    } label: {
                        SlateSymbol.trash.image(label: "Remove formula")
                    }
                    .buttonStyle(.borderless)
                }
                .accessibilityElement(children: .combine)
                .accessibilityLabel("Formula \(formula.name), \(formula.expression)")
            }
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Formulas")
    }

    @ViewBuilder
    private var previewSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("Live preview")
                .font(.subheadline.weight(.semibold))
                .accessibilityAddTraits(.isHeader)
            Text(model.previewState.accessibilityAnnouncement)
                .foregroundStyle(Color(nsColor: .secondaryLabelColor))
                .frame(maxWidth: .infinity, alignment: .leading)
                .accessibilityLabel("Live preview")
                .accessibilityValue(model.previewState.accessibilityAnnouncement)
            previewContent
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Live preview region")
    }

    @ViewBuilder
    private var previewContent: some View {
        switch model.previewState {
        case .ready(let result):
            if result.columns.isEmpty {
                Text("No preview rows.")
                    .foregroundStyle(Color(nsColor: .secondaryLabelColor))
            } else {
                AccessibleDataGrid(
                    columns: previewColumns(from: result),
                    rows: previewRows(from: result),
                    summary: BaseSummaryFormatter.summaryText(result),
                    accessibilityLabel: "Builder preview table",
                    groups: previewGroups(from: result),
                    selection: Binding(
                        get: { previewSelectedRow },
                        set: { previewSelectedRow = $0 }),
                    cellSelection: Binding(
                        get: { previewSelectedCell },
                        set: { previewSelectedCell = $0 }),
                    sortState: Binding(
                        get: { previewSortState },
                        set: { previewSortState = $0 }),
                    cellNavigation: true)
                    .frame(minHeight: 220)
            }
        case .idle, .loading, .failed:
            EmptyView()
        }
    }

    private func previewColumns(
        from result: BasesResultSet
    ) -> [AccessibleDataGrid<BaseGridRow>.Column] {
        result.columns.enumerated().map { columnIndex, column in
            AccessibleDataGrid<BaseGridRow>.Column(
                column.label,
                cell: { row in row.value(at: columnIndex) },
                sort: { lhs, rhs in
                    lhs.value(at: columnIndex).localizedCaseInsensitiveCompare(
                        rhs.value(at: columnIndex)) == .orderedAscending
                },
                accessibilityHint: { _ in "Builder preview table is read-only." })
        }
    }

    private func previewRows(from result: BasesResultSet) -> [BaseGridRow] {
        result.rows.enumerated().map { rowIndex, row in
            BaseGridRow(row: row, ordinal: rowIndex)
        }
    }

    private func previewGroups(
        from result: BasesResultSet
    ) -> [AccessibleDataGrid<BaseGridRow>.Group] {
        result.groups.map {
            .init(
                label: $0.label,
                rowStart: Int($0.rowStart),
                rowCount: Int($0.rowCount),
                summary: BaseSummaryFormatter.summaryText(
                    summaries: $0.summaries, columns: result.columns))
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
        let validation = advancedExpressionValidation(rawExpression: rawExpression)
        return HStack(spacing: 8) {
            Text("Advanced")
                .foregroundStyle(Color(nsColor: .secondaryLabelColor))
            TextField("Raw filter expression", text: advancedExpressionBinding(index: index))
                .textFieldStyle(.roundedBorder)
                .accessibilityLabel(rowLabel)
            if let validation, !validation.valid {
                let message = expressionValidationMessage(
                    validation, fallback: "Expression invalid")
                SlateSymbol.warning.label(message)
                    .foregroundStyle(Color(nsColor: .systemRed))
                    .accessibilityLabel(message)
            }
            Spacer()
            rowButtons(index: index)
        }
        .padding(8)
        .background(rowBackground(index: index))
        .contentShape(Rectangle())
        .onTapGesture { model.selectedRowIndex = index }
        .accessibilityElement(children: .combine)
        .accessibilityLabel(advancedAccessibilityLabel(rawExpression, validation: validation))
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
                model.perform(.editAsExpression(index: index))
            } label: {
                SlateSymbol.showSource.image(label: "Edit as expression")
            }
            .help("Edit as expression")
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
        VStack(alignment: .leading, spacing: 8) {
            HStack(alignment: .bottom) {
                VStack(alignment: .leading, spacing: 3) {
                    Text("Base path")
                        .font(.caption)
                        .foregroundStyle(Color(nsColor: .secondaryLabelColor))
                    TextField("Save as .base", text: $saveAsBasePath)
                        .textFieldStyle(.roundedBorder)
                        .accessibilityLabel("Base file path")
                }
                Button("Save as .base") {
                    appState.basesBuilderSaveAsBase(path: saveAsBasePath)
                }
                VStack(alignment: .leading, spacing: 3) {
                    Text("Saved query name")
                        .font(.caption)
                        .foregroundStyle(Color(nsColor: .secondaryLabelColor))
                    TextField("Saved query name", text: $savedQueryName)
                        .textFieldStyle(.roundedBorder)
                        .accessibilityLabel("Saved query name")
                }
                Button("Save as saved query") {
                    appState.basesBuilderSaveAsSavedQuery(
                        name: savedQueryName,
                        description: savedQueryDescription)
                }
            }
            HStack {
                Text(model.conditionsListAccessibilityValue)
                    .foregroundStyle(Color(nsColor: .secondaryLabelColor))
                Spacer()
                Button("Save to view") { appState.basesBuilderSaveToView() }
                Button("Done") { appState.basesCloseQueryBuilder() }
                    .keyboardShortcut(.defaultAction)
            }
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

    private var firstPropertyChoice: BaseQueryProperty {
        propertyChoices.first ?? .file(.name)
    }

    private var viewTypeBinding: Binding<BaseQueryViewType> {
        Binding(
            get: { model.viewType },
            set: { model.viewType = $0 })
    }

    private func sortPropertyBinding(index: Int) -> Binding<BaseQueryProperty> {
        Binding(
            get: {
                guard model.sortKeys.indices.contains(index) else { return firstPropertyChoice }
                return model.sortKeys[index].property ?? firstPropertyChoice
            },
            set: { property in
                guard model.sortKeys.indices.contains(index) else { return }
                let ascending = model.sortKeys[index].ascending
                model.sortKeys[index] = BaseQuerySortKey(property: property, ascending: ascending)
            })
    }

    private func sortAscendingBinding(index: Int) -> Binding<Bool> {
        Binding(
            get: {
                guard model.sortKeys.indices.contains(index) else { return true }
                return model.sortKeys[index].ascending
            },
            set: { ascending in
                guard model.sortKeys.indices.contains(index) else { return }
                model.sortKeys[index].ascending = ascending
            })
    }

    private var groupPropertyBinding: Binding<BaseQueryProperty> {
        Binding(
            get: { model.groupBy?.property ?? firstPropertyChoice },
            set: { property in
                model.groupBy = BaseQueryGroupBy(
                    property: property,
                    ascending: model.groupBy?.ascending ?? true)
            })
    }

    private var groupAscendingBinding: Binding<Bool> {
        Binding(
            get: { model.groupBy?.ascending ?? true },
            set: { ascending in
                model.groupBy = BaseQueryGroupBy(
                    property: model.groupBy?.property ?? firstPropertyChoice,
                    ascending: ascending)
            })
    }

    private func columnIncludedBinding(property: BaseQueryProperty) -> Binding<Bool> {
        Binding(
            get: { isColumnIncluded(property) },
            set: { included in
                if included {
                    if !isColumnIncluded(property) {
                        model.columns.append(BaseQueryColumn(property: property, displayName: nil))
                    }
                } else {
                    model.columns.removeAll { $0.id == property.sourceExpression }
                }
            })
    }

    private func columnDisplayNameBinding(property: BaseQueryProperty) -> Binding<String> {
        Binding(
            get: {
                model.columns.first { $0.id == property.sourceExpression }?.displayName ?? ""
            },
            set: { value in
                if let index = model.columns.firstIndex(where: { $0.id == property.sourceExpression }) {
                    model.columns[index].displayName = value
                } else if !value.isEmpty {
                    model.columns.append(BaseQueryColumn(property: property, displayName: value))
                }
            })
    }

    private func advancedExpressionBinding(index: Int) -> Binding<String> {
        Binding(
            get: {
                guard model.rows.indices.contains(index),
                    case .advanced(let raw, _) = model.rows[index]
                else { return "" }
                return raw
            },
            set: { value in
                let validation = appState.currentSession?.validateBaseExpression(source: value)
                model.updateAdvancedExpression(index: index, rawExpression: value, validation: validation)
            })
    }

    private func isColumnIncluded(_ property: BaseQueryProperty) -> Bool {
        model.columns.contains { $0.id == property.sourceExpression }
    }

    private func canMoveColumn(_ property: BaseQueryProperty, delta: Int) -> Bool {
        guard let index = model.columns.firstIndex(where: { $0.id == property.sourceExpression })
        else { return false }
        return model.columns.indices.contains(index + delta)
    }

    private func moveColumn(property: BaseQueryProperty, delta: Int) {
        guard let index = model.columns.firstIndex(where: { $0.id == property.sourceExpression })
        else { return }
        let destination = index + delta
        guard model.columns.indices.contains(destination) else { return }
        model.columns.swapAt(index, destination)
    }

    private func moveSort(index: Int, delta: Int) {
        let destination = index + delta
        guard model.sortKeys.indices.contains(index),
            model.sortKeys.indices.contains(destination)
        else { return }
        model.sortKeys.swapAt(index, destination)
    }

    private func addFormula() {
        guard let validation = appState.currentSession?.validateBaseExpression(source: formulaExpression)
        else { return }
        formulaValidation = validation
        guard validation.valid, let exprJSON = validation.exprJson else { return }
        do {
            let formula = try BaseQueryFormula(
                name: formulaName,
                expression: formulaExpression,
                expressionJSON: exprJSON)
            model.formulas.removeAll { $0.name == formula.name }
            model.formulas.append(formula)
            if !model.columns.contains(where: { $0.id == "formula.\(formula.name)" }) {
                model.columns.append(
                    BaseQueryColumn(property: .formula(formula.name), displayName: nil))
            }
            formulaName = ""
            formulaExpression = ""
            formulaValidation = nil
        } catch {
            formulaValidation = BaseExpressionValidation(
                valid: false,
                exprJson: nil,
                message: error.localizedDescription,
                spanStart: 0,
                spanEnd: UInt32(formulaExpression.utf8.count))
        }
    }

    private func validateFormulaExpression(_ value: String) {
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            formulaValidation = nil
            return
        }
        formulaValidation = appState.currentSession?.validateBaseExpression(source: value)
    }

    private func advancedExpressionValidation(
        rawExpression: String
    ) -> BaseExpressionValidation? {
        let trimmed = rawExpression.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return nil }
        return appState.currentSession?.validateBaseExpression(source: rawExpression)
    }

    private func expressionValidationMessage(
        _ validation: BaseExpressionValidation,
        fallback: String
    ) -> String {
        var message = validation.message ?? fallback
        if validation.spanEnd > validation.spanStart {
            message += " at characters \(validation.spanStart)-\(validation.spanEnd)"
        }
        return message
    }

    private func advancedAccessibilityLabel(
        _ rawExpression: String,
        validation: BaseExpressionValidation?
    ) -> String {
        guard let validation, !validation.valid else {
            return "Advanced condition: \(rawExpression)"
        }
        return "Advanced condition: \(rawExpression). "
            + expressionValidationMessage(validation, fallback: "Expression invalid")
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
        properties.append(contentsOf: model.formulas.map { BaseQueryProperty.formula($0.name) })
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
