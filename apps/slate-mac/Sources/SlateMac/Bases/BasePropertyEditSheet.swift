// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

struct BasePropertyEditRequest: Identifiable, Equatable {
    let id = UUID()
    let row: BasesRow
    let column: BasesColumn
    let currentValue: BasesValue

    var propertyName: String {
        BaseCellEditPolicy.propertyKey(for: column) ?? column.label
    }

    var draftText: String {
        if !currentValue.list.isEmpty {
            return currentValue.list.joined(separator: ", ")
        }
        return currentValue.display
    }
}

struct BasePropertyEditSheet: View {
    let request: BasePropertyEditRequest
    var onSave: (PropertyValue) -> Void
    var onClear: () -> Void

    @Environment(\.dismiss) private var dismiss
    @State private var draft: String
    @State private var validationError: String?

    init(
        request: BasePropertyEditRequest,
        onSave: @escaping (PropertyValue) -> Void,
        onClear: @escaping () -> Void
    ) {
        self.request = request
        self.onSave = onSave
        self.onClear = onClear
        _draft = State(initialValue: request.draftText)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.md) {
            Text("Edit \(request.propertyName)")
                .font(Tokens.Typography.sectionHeader)
            Text(request.row.filePath)
                .font(Tokens.Typography.caption)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
            VStack(alignment: .leading, spacing: Tokens.Spacing.xs) {
                Text("Value")
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                TextField("Value", text: $draft)
                    .textFieldStyle(.roundedBorder)
                    .onSubmit(save)
                    .accessibilityLabel("Property value")
                    .accessibilityHint("Press Return to save, or Escape to cancel.")
            }
            if let validationError {
                Text(validationError)
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.destructiveText)
                    .accessibilityLabel("Validation error: \(validationError)")
            }
            HStack {
                Button("Cancel", role: .cancel) { dismiss() }
                Spacer()
                Button("Clear", role: .destructive) {
                    onClear()
                    dismiss()
                }
                Button("Save") { save() }
                    .keyboardShortcut(.defaultAction)
            }
        }
        .padding(Tokens.Spacing.lg)
        .frame(width: 380)
    }

    private func save() {
        switch BaseCellEditPolicy.propertyValue(from: draft, valueKind: request.column.valueKind) {
        case .success(let value):
            onSave(value)
            dismiss()
        case .failure(let error):
            validationError = error.message
        }
    }
}
