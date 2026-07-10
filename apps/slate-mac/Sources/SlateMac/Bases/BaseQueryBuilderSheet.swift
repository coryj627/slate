// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

struct BaseRowReorderCommand {
    enum Direction {
        case up
        case down
    }

    struct Outcome: Equatable {
        let destination: Int?
        let announcement: String
    }

    private static let actionModifierMask: EventModifiers =
        [.shift, .control, .option, .command]

    let direction: Direction

    init?(direction: Direction, modifiers: EventModifiers) {
        // Arrow-key events may carry device flags such as numeric-pad or
        // function. Among action modifiers, accept Option alone. In
        // particular, Control-Option must pass through for VoiceOver Quick Nav.
        guard modifiers.intersection(Self.actionModifierMask) == .option else {
            return nil
        }
        self.direction = direction
    }

    @discardableResult
    static func route(
        isFocused: Bool,
        direction: Direction,
        modifiers: EventModifiers,
        index: Int,
        count: Int,
        label: String,
        move: (Int) -> Void,
        retainFocus: (Int) -> Void,
        announce: (String) -> Void
    ) -> Bool {
        guard isFocused,
            let command = BaseRowReorderCommand(
                direction: direction, modifiers: modifiers)
        else { return false }
        command.perform(
            index: index,
            count: count,
            label: label,
            move: move,
            retainFocus: retainFocus,
            announce: announce)
        return true
    }

    @discardableResult
    func perform(
        index: Int,
        count: Int,
        label: String,
        move: (Int) -> Void,
        retainFocus: (Int) -> Void,
        announce: (String) -> Void
    ) -> Outcome {
        guard index >= 0, index < count else {
            let outcome = Outcome(
                destination: nil,
                announcement: "\(label) cannot be moved.")
            announce(outcome.announcement)
            return outcome
        }
        let destination = index + direction.delta
        guard destination >= 0, destination < count else {
            let outcome = Outcome(
                destination: nil,
                announcement: "\(label) is already \(direction.boundaryName).")
            retainFocus(index)
            announce(outcome.announcement)
            return outcome
        }
        move(destination)
        retainFocus(destination)
        let outcome = Outcome(
            destination: destination,
            announcement:
                "\(label) moved \(direction.announcementName) to position "
                + "\(destination + 1) of \(count).")
        announce(outcome.announcement)
        return outcome
    }
}

private extension BaseRowReorderCommand.Direction {
    var delta: Int {
        switch self {
        case .up: -1
        case .down: 1
        }
    }

    var announcementName: String {
        switch self {
        case .up: "up"
        case .down: "down"
        }
    }

    var boundaryName: String {
        switch self {
        case .up: "first"
        case .down: "last"
        }
    }
}

enum BaseQueryDateCodec {
    static func date(from value: String, timeZone: TimeZone) -> Date? {
        let parts = value.split(separator: "-", omittingEmptySubsequences: false)
        guard parts.count == 3,
            parts[0].count == 4,
            parts[1].count == 2,
            parts[2].count == 2,
            let year = Int(parts[0]),
            let month = Int(parts[1]),
            let day = Int(parts[2])
        else { return nil }
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = timeZone
        guard let result = calendar.date(
            from: DateComponents(
                timeZone: timeZone,
                year: year,
                month: month,
                day: day,
                hour: 12)),
            string(from: result, timeZone: timeZone) == value
        else { return nil }
        return result
    }

    static func string(from date: Date, timeZone: TimeZone) -> String {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = timeZone
        let parts = calendar.dateComponents([.year, .month, .day], from: date)
        return String(
            format: "%04d-%02d-%02d",
            parts.year ?? 0,
            parts.month ?? 0,
            parts.day ?? 0)
    }
}

struct BaseQueryBuilderSheet: View {
    @EnvironmentObject private var appState: AppState
    @Environment(\.timeZone) private var timeZone
    @ObservedObject var model: BaseQueryBuilderModel

    @State private var folders: [String] = []
    @State private var tags: [String] = []
    @State private var notePaths: [String] = []
    @State private var propertySummaries: [PropertyKeySummary] = []
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
    @FocusState private var focusedSortRow: Int?
    @FocusState private var focusedColumnRowID: String?

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
            let summaries = await loadedProperties
            propertySummaries = summaries
            model.applyPropertyChoices(makePropertyChoices(summaries: summaries))
        }
        .onAppear {
            hydrateSavedQueryFields()
            appState.basesBuilderSchedulePreview(delayNanoseconds: 0)
        }
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
                ForEach(sourceKindChoices) { kind in
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
        case .allNotes, .tasks, .unsupported:
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
                .focusable()
                .focused($focusedSortRow, equals: index)
                .onKeyPress(.upArrow, phases: .down) { press in
                    handleSortReorder(
                        index: index, direction: .up, modifiers: press.modifiers)
                }
                .onKeyPress(.downArrow, phases: .down) { press in
                    handleSortReorder(
                        index: index, direction: .down, modifiers: press.modifiers)
                }
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
                ForEach(viewTypeChoices) { viewType in
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
                .accessibilityElement(children: .contain)
                .accessibilityLabel("Column \(property.accessibilityName)")
                .focusable()
                .focused($focusedColumnRowID, equals: property.sourceExpression)
                .onKeyPress(.upArrow, phases: .down) { press in
                    handleColumnReorder(
                        property: property, direction: .up, modifiers: press.modifiers)
                }
                .onKeyPress(.downArrow, phases: .down) { press in
                    handleColumnReorder(
                        property: property, direction: .down, modifiers: press.modifiers)
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
            VStack(alignment: .leading, spacing: 4) {
                Text("Function suggestions")
                    .font(.caption)
                    .foregroundStyle(Color(nsColor: .secondaryLabelColor))
                ScrollView(.horizontal) {
                    HStack(spacing: 6) {
                        ForEach(formulaCompletionNames, id: \.self) { name in
                            Button("\(name)()") {
                                formulaExpression = BaseFormulaCompletion.inserting(
                                    name,
                                    into: formulaExpression)
                            }
                            .buttonStyle(.bordered)
                            .accessibilityLabel("\(name)(), insert function")
                        }
                    }
                }
            }
            .accessibilityElement(children: .contain)
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
                    cellNavigation: true,
                    rowAccessibilityDescription: { $0.row.audioDescription })
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
    private func conditionControls(
        condition: BaseQueryCondition,
        property: Binding<BaseQueryProperty>,
        operator conditionOperator: Binding<BaseQueryOperator>,
        value: Binding<BaseQueryValue>
    ) -> some View {
        let choice = propertyChoice(for: condition.property)
        let inputKind = BaseQueryCondition.inputKind(for: choice.kind, operator: condition.op)
        Picker("Property", selection: property) {
            ForEach(propertyChoices, id: \.self) { item in
                Text(item.accessibilityName).tag(item)
            }
        }
        .frame(minWidth: 150)

        Picker("Operator", selection: conditionOperator) {
            ForEach(BaseQueryOperator.options(for: choice.kind), id: \.self) { item in
                Text(item.accessibilityName).tag(item)
            }
        }
        .frame(minWidth: 150)

        if condition.op == .isEmpty {
            Text("No value")
                .foregroundStyle(Color(nsColor: .secondaryLabelColor))
                .frame(minWidth: 150, alignment: .leading)
                .accessibilityHidden(true)
        } else {
            conditionValueEditor(
                value: value,
                kind: inputKind,
                descriptor: BaseQueryEditorDescriptor.forKind(inputKind))
        }
    }

    @ViewBuilder
    private func conditionValueEditor(
        value: Binding<BaseQueryValue>,
        kind: BaseQueryValueKind,
        descriptor: BaseQueryEditorDescriptor
    ) -> some View {
        switch descriptor {
        case .text:
            TextField("Value", text: valueTextBinding(value, kind: kind))
                .textFieldStyle(.roundedBorder)
                .frame(minWidth: 150)
                .accessibilityLabel("Condition value")
        case .number:
            HStack(spacing: 4) {
                TextField("Number", text: valueTextBinding(value, kind: kind))
                    .textFieldStyle(.roundedBorder)
                    .frame(minWidth: 105)
                    .accessibilityLabel("Condition number")
                Stepper(
                    "Number",
                    value: numberValueBinding(value),
                    in: -1_000_000_000...1_000_000_000,
                    step: 1)
                    .labelsHidden()
                    .accessibilityLabel("Step condition number")
            }
        case .toggle:
            Toggle("Condition value", isOn: booleanValueBinding(value))
                .toggleStyle(.switch)
                .frame(minWidth: 150, alignment: .leading)
        case .tokenList:
            TextField("Comma-separated values", text: tokenValueBinding(value))
                .textFieldStyle(.roundedBorder)
                .frame(minWidth: 150)
                .accessibilityLabel("Condition values, comma separated")
        case .link:
            HStack(spacing: 4) {
                TextField("Note path", text: valueTextBinding(value, kind: kind))
                    .textFieldStyle(.roundedBorder)
                    .frame(minWidth: 105)
                    .accessibilityLabel("Condition note path")
                Menu("Pick…") {
                    ForEach(Array(notePaths.prefix(100)), id: \.self) { path in
                        Button(path) { value.wrappedValue = linkValue(path, kind: kind) }
                    }
                }
                .accessibilityLabel("Pick… condition note path")
            }
        case .dateAndRelative:
            VStack(alignment: .leading, spacing: 4) {
                Picker("Date form", selection: relativeDateBinding(value)) {
                    Text("On date").tag(false)
                    Text("N days ago").tag(true)
                }
                .pickerStyle(.segmented)
                if case .relativeDays = value.wrappedValue {
                    Stepper(value: relativeDaysBinding(value), in: 1...3_650) {
                        Text("\(relativeDaysBinding(value).wrappedValue) days ago")
                    }
                    .accessibilityLabel("Relative date, days ago")
                } else {
                    DatePicker(
                        "Date",
                        selection: absoluteDateBinding(value),
                        displayedComponents: .date)
                        .labelsHidden()
                        .accessibilityLabel("Absolute condition date")
                }
            }
            .frame(minWidth: 180)
        }
    }

    private func valueTextBinding(
        _ value: Binding<BaseQueryValue>,
        kind: BaseQueryValueKind
    ) -> Binding<String> {
        Binding(
            get: { value.wrappedValue.editingText },
            set: { value.wrappedValue = value.wrappedValue.replacingEditingText($0, preferredKind: kind) })
    }

    private func numberValueBinding(_ value: Binding<BaseQueryValue>) -> Binding<Double> {
        Binding(
            get: {
                if case .number(let number) = value.wrappedValue { return number }
                return 0
            },
            set: { value.wrappedValue = .number($0) })
    }

    private func booleanValueBinding(_ value: Binding<BaseQueryValue>) -> Binding<Bool> {
        Binding(
            get: {
                if case .bool(let enabled) = value.wrappedValue { return enabled }
                return false
            },
            set: { value.wrappedValue = .bool($0) })
    }

    private func tokenValueBinding(_ value: Binding<BaseQueryValue>) -> Binding<String> {
        Binding(
            get: { value.wrappedValue.editingText },
            set: { text in
                value.wrappedValue = .tokens(
                    text.split(separator: ",", omittingEmptySubsequences: true)
                        .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
                        .filter { !$0.isEmpty })
            })
    }

    private func linkValue(_ path: String, kind: BaseQueryValueKind) -> BaseQueryValue {
        kind == .file ? .file(path) : .wikilink(path)
    }

    private func relativeDateBinding(_ value: Binding<BaseQueryValue>) -> Binding<Bool> {
        Binding(
            get: {
                if case .relativeDays = value.wrappedValue { return true }
                return false
            },
            set: { relative in
                value.wrappedValue = relative
                    ? .relativeDays(7)
                    : .absoluteDate(BaseQueryDateCodec.string(from: Date(), timeZone: timeZone))
            })
    }

    private func relativeDaysBinding(_ value: Binding<BaseQueryValue>) -> Binding<Int> {
        Binding(
            get: {
                if case .relativeDays(let days) = value.wrappedValue { return days }
                return 7
            },
            set: { value.wrappedValue = .relativeDays(max($0, 1)) })
    }

    private func absoluteDateBinding(_ value: Binding<BaseQueryValue>) -> Binding<Date> {
        Binding(
            get: {
                if case .absoluteDate(let text) = value.wrappedValue,
                    let date = BaseQueryDateCodec.date(from: text, timeZone: timeZone)
                {
                    return date
                }
                return Date()
            },
            set: {
                value.wrappedValue = .absoluteDate(
                    BaseQueryDateCodec.string(from: $0, timeZone: timeZone))
            })
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
            conditionControls(
                condition: condition,
                property: conditionPropertyBinding(index: index),
                operator: conditionOperatorBinding(index: index),
                value: conditionValueBinding(index: index))
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
                conditionControls(
                    condition: condition,
                    property: groupConditionPropertyBinding(
                        groupIndex: groupIndex,
                        childIndex: childIndex),
                    operator: groupConditionOperatorBinding(
                        groupIndex: groupIndex,
                        childIndex: childIndex),
                    value: groupConditionValueBinding(
                        groupIndex: groupIndex,
                        childIndex: childIndex))
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
                    .foregroundStyle(Tokens.ColorRole.destructiveText)
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
                if model.editingSavedQuery != nil {
                    Button("Update saved query") {
                        appState.basesBuilderUpdateSavedQuery()
                    }
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

    private func hydrateSavedQueryFields() {
        guard let editingSavedQuery = model.editingSavedQuery else { return }
        savedQueryName = editingSavedQuery.name
        savedQueryDescription = editingSavedQuery.description ?? ""
        saveAsBasePath = "Queries/\(editingSavedQuery.name).base"
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

    private var viewTypeChoices: [BaseQueryViewType] {
        switch model.viewType {
        case .unsupported:
            return [model.viewType, .table, .list]
        case .table, .list:
            return [.table, .list]
        }
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
                let validation = validateAdvancedExpressionInput(value)
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

    private func handleSortReorder(
        index: Int,
        direction: BaseRowReorderCommand.Direction,
        modifiers: EventModifiers
    ) -> KeyPress.Result {
        guard BaseRowReorderCommand.route(
            isFocused: focusedSortRow == index,
            direction: direction,
            modifiers: modifiers,
            index: index,
            count: model.sortKeys.count,
            label: "Sort \(index + 1)",
            move: { destination in
                moveSort(index: index, delta: destination - index)
            },
            retainFocus: { focusedSortRow = $0 },
            announce: { postAccessibilityAnnouncement($0, priority: .medium) })
        else { return .ignored }
        return .handled
    }

    private func handleColumnReorder(
        property: BaseQueryProperty,
        direction: BaseRowReorderCommand.Direction,
        modifiers: EventModifiers
    ) -> KeyPress.Result {
        guard let index = model.columns.firstIndex(where: {
            $0.id == property.sourceExpression
        })
        else { return .ignored }
        guard BaseRowReorderCommand.route(
            isFocused: focusedColumnRowID == property.sourceExpression,
            direction: direction,
            modifiers: modifiers,
            index: index,
            count: model.columns.count,
            label: "\(property.accessibilityName) column",
            move: { destination in
                moveColumn(property: property, delta: destination - index)
            },
            retainFocus: { _ in focusedColumnRowID = property.sourceExpression },
            announce: { postAccessibilityAnnouncement($0, priority: .medium) })
        else { return .ignored }
        return .handled
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
        validateAdvancedExpressionInput(rawExpression)
    }

    private func validateAdvancedExpressionInput(
        _ rawExpression: String
    ) -> BaseExpressionValidation? {
        let trimmed = rawExpression.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return nil }
        return appState.currentSession?.validateBaseExpression(source: trimmed)
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

    private var sourceKindChoices: [BaseQuerySourceKind] {
        if case .unsupported = model.source {
            return [.unsupported] + BaseQuerySourceKind.supportedCases
        }
        return BaseQuerySourceKind.supportedCases
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

    private var propertyDescriptorChoices: [BaseQueryPropertyChoice] {
        makePropertyChoices(summaries: propertySummaries)
    }

    private var propertyChoices: [BaseQueryProperty] {
        propertyDescriptorChoices.map(\.property)
    }

    private func makePropertyChoices(
        summaries: [PropertyKeySummary]
    ) -> [BaseQueryPropertyChoice] {
        var choices = BaseQueryPropertyChoice.fileChoices
        let noteChoices: [BaseQueryPropertyChoice]
        if summaries.isEmpty {
            noteChoices = [
                BaseQueryPropertyChoice(property: .note("status"), kind: .mixedOrUnknown),
                BaseQueryPropertyChoice(property: .note("priority"), kind: .mixedOrUnknown),
            ]
        } else {
            noteChoices = summaries.map(BaseQueryPropertyChoice.init(summary:))
        }
        choices.append(contentsOf: noteChoices)
        choices.append(
            contentsOf: model.formulas.map {
                BaseQueryPropertyChoice(property: .formula($0.name), kind: .formula)
            })
        if model.source == .tasks {
            choices.append(contentsOf: BaseQueryPropertyChoice.taskChoices)
        }
        return choices
    }

    private func propertyChoice(for property: BaseQueryProperty) -> BaseQueryPropertyChoice {
        propertyDescriptorChoices.first { $0.property == property }
            ?? BaseQueryPropertyChoice(
                property: property,
                kind: property.staticValueKind ?? .mixedOrUnknown)
    }

    private var formulaCompletionNames: [String] {
        let prefix = formulaExpression.split { character in
            !(character.isLetter || character.isNumber || character == "_")
        }.last.map(String.init) ?? ""
        guard !prefix.isEmpty else { return BaseFormulaCompletion.names }
        let matches = BaseFormulaCompletion.names.filter {
            $0.lowercased().hasPrefix(prefix.lowercased())
        }
        return matches.isEmpty ? BaseFormulaCompletion.names : matches
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
                condition.retarget(to: propertyChoice(for: property))
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
                condition.setOperator(op, kind: propertyChoice(for: condition.property).kind)
                model.rows[index] = .condition(condition)
            })
    }

    private func conditionValueBinding(index: Int) -> Binding<BaseQueryValue> {
        Binding(
            get: {
                guard model.rows.indices.contains(index) else { return .text("") }
                guard case .condition(let condition) = model.rows[index] else { return .text("") }
                return condition.value
            },
            set: { value in
                guard model.rows.indices.contains(index) else { return }
                guard case .condition(var condition) = model.rows[index] else { return }
                condition.setValue(value)
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
                    condition.retarget(to: propertyChoice(for: property))
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
                    condition.setOperator(op, kind: propertyChoice(for: condition.property).kind)
                }
            })
    }

    private func groupConditionValueBinding(
        groupIndex: Int,
        childIndex: Int
    ) -> Binding<BaseQueryValue> {
        Binding(
            get: {
                guard let condition = groupCondition(groupIndex: groupIndex, childIndex: childIndex)
                else { return .text("") }
                return condition.value
            },
            set: { value in
                updateGroupCondition(groupIndex: groupIndex, childIndex: childIndex) { condition in
                    condition.setValue(value)
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

private enum BaseQuerySourceKind: String, Identifiable {
    case allNotes
    case folder
    case tag
    case recent
    case linked
    case tasks
    case unsupported

    static let supportedCases: [BaseQuerySourceKind] = [
        .allNotes, .folder, .tag, .recent, .linked, .tasks,
    ]

    var id: String { rawValue }

    var title: String {
        switch self {
        case .allNotes: return "All notes"
        case .folder: return "Folder"
        case .tag: return "Tag"
        case .recent: return "Recently edited"
        case .linked: return "Linked from note"
        case .tasks: return "Tasks"
        case .unsupported: return "Unsupported source (read only)"
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
        case .unsupported: self = .unsupported
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
        case .unsupported:
            return current
        }
    }
}
