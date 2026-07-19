// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation
import SwiftUI

/// Sheet for adding a new frontmatter property to the currently-
/// loaded note. The user supplies a key + a type; we construct the
/// type's zero value and call `appState.setProperty(...)`.
///
/// Validation is pre-flight (empty key + key-already-exists). The
/// confirmation step gets a Cancel role so platform keyboard
/// dismissal (Esc) closes without committing.
struct AddPropertySheet: View {
    @EnvironmentObject private var appState: AppState
    @Environment(\.dismiss) private var dismiss

    @State private var key: String = ""
    @State private var selectedKind: AddPropertyKind = .text
    @State private var inlineError: String?
    @State private var isCommitting = false

    @FocusState private var focusedField: Field?

    enum Field: Hashable {
        case key
    }

    private var disabledReason: String? {
        appState.addPropertySheetDisabledReason
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Add property")
                .font(.headline)
                .accessibilityAddTraits(.isHeader)

            VStack(alignment: .leading, spacing: 4) {
                Text("Key")
                    .font(.caption)
                TextField("e.g. author", text: $key)
                    .textFieldStyle(.roundedBorder)
                    .accessibilityLabel("Property key")
                    .focused($focusedField, equals: .key)
                    .onSubmit(commit)
                    .disabled(disabledReason != nil)
                if disabledReason != nil, !key.isEmpty {
                    Text(key)
                        .font(.body.monospaced())
                        .textSelection(.enabled)
                        .accessibilityLabel("Property key draft: \(key)")
                        .accessibilityHint("Selectable copy of the preserved property key draft.")
                }
            }

            VStack(alignment: .leading, spacing: 4) {
                Text("Type")
                    .font(.caption)
                Picker("Property type", selection: $selectedKind) {
                    ForEach(AddPropertyKind.allCases, id: \.self) { kind in
                        Text(kind.displayName).tag(kind)
                    }
                }
                .pickerStyle(.menu)
                .labelsHidden()
                .accessibilityLabel("Property type")
                .disabled(disabledReason != nil)
            }

            if let disabledReason {
                Text(disabledReason)
                    .font(.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .accessibilityLabel(disabledReason)
                    .help(disabledReason)
            }

            if let err = inlineError {
                Text(err)
                    .font(.caption)
                    .foregroundStyle(Tokens.ColorRole.destructiveText)
                    .accessibilityLabel("Validation error: \(err)")
                    .accessibilityAddTraits(.isStaticText)
            }

            HStack {
                if disabledReason == AppState.batchTrashQuarantineReason {
                    Button("Check Again") {
                        _ = appState.retryBatchTrashUnknownReconciliation()
                    }
                    .disabled(appState.isMutatingStructure)
                    .accessibilityHint("Rescan the vault and check whether this note is still present.")
                    .help("Check whether this note is still present")
                }
                Spacer()
                Button("Cancel", role: .cancel) {
                    appState.dismissAddPropertySheet()
                    dismiss()
                }
                .keyboardShortcut(.cancelAction)
                Button("Add") {
                    commit()
                }
                .keyboardShortcut(.defaultAction)
                .disabled(
                    key.trimmingCharacters(in: .whitespaces).isEmpty
                        || appState.isEditingProperty
                        || isCommitting
                        || disabledReason != nil)
                .accessibilityHint(disabledReason ?? "Add this property to the note.")
                .help(disabledReason ?? "Add this property to the note")
            }
        }
        .padding(20)
        .frame(minWidth: 360)
        .onAppear {
            appState.ensureAddPropertySheetOwner()
            focusedField = .key
            postAccessibilityAnnouncement(.addPropertySheetShown)
        }
    }

    private func commit() {
        if let disabledReason {
            inlineError = disabledReason
            // W0.5-3 residue: add-property disabledReason (AppState availability copy)
            postAccessibilityAnnouncement(.hostComposed(text: disabledReason, priority: .high))
            return
        }
        let trimmed = key.trimmingCharacters(in: .whitespaces)
        guard !trimmed.isEmpty else {
            inlineError = "Key can't be empty."
            return
        }
        if trimmed.contains(".") {
            inlineError = "Dotted keys aren't supported yet — use a flat key."
            return
        }
        if appState.currentNoteProperties.contains(where: { $0.key == trimmed }) {
            inlineError = "A property named `\(trimmed)` already exists on this note."
            return
        }
        guard let path = appState.addPropertySheetOwnerPath,
            let owner = appState.addPropertySheetOwner,
            appState.ownsNoteAuthoring(owner),
            BaseExactIdentity.matches(path, appState.loadedFilePath),
            BaseExactIdentity.matches(owner.path, path)
        else {
            inlineError = "No note is loaded."
            return
        }
        inlineError = nil
        guard let task = appState.setProperty(
            path: path,
            key: trimmed,
            value: selectedKind.zeroValue(),
            owner: owner)
        else {
            inlineError =
                appState.addPropertySheetDisabledReason
                ?? "The property could not be added yet. Try again."
            return
        }
        isCommitting = true
        Task { @MainActor in
            await task.value
            isCommitting = false
            guard appState.currentNoteProperties.contains(where: { $0.key == trimmed }) else {
                inlineError =
                    appState.addPropertySheetDisabledReason
                    ?? appState.propertyEditError
                    ?? "The property was not added. Your draft is still here."
                return
            }
            appState.dismissAddPropertySheet()
            dismiss()
        }
    }
}

/// One-line enum of the kinds the Add sheet offers. Mirrors the
/// FFI `PropertyValue` shape but skips the recursive `list` /
/// `tagList` content (the sheet creates empty containers; the user
/// adds items in the editor row after).
enum AddPropertyKind: CaseIterable {
    case text
    case integer
    case float
    case boolean
    case date
    case datetime
    case wikilink
    case list
    case tagList

    var displayName: String {
        switch self {
        case .text: return "Text"
        case .integer: return "Integer"
        case .float: return "Float"
        case .boolean: return "Boolean"
        case .date: return "Date"
        case .datetime: return "Date and time"
        case .wikilink: return "Wikilink"
        case .list: return "List"
        case .tagList: return "Tag list"
        }
    }

    /// Construct the zero value for the type. Date defaults to
    /// today (ISO 8601 yyyy-MM-dd); datetime defaults to now (ISO
    /// 8601 with seconds, in the user's TZ).
    func zeroValue() -> PropertyValue {
        switch self {
        case .text:
            return PropertyValue.text(value: "")
        case .integer:
            return PropertyValue.integer(value: 0)
        case .float:
            return PropertyValue.float(value: 0)
        case .boolean:
            return PropertyValue.boolean(value: false)
        case .date:
            return PropertyValue.date(value: Self.todayString())
        case .datetime:
            return PropertyValue.datetime(value: Self.nowString())
        case .wikilink:
            // Empty wikilink would be rejected by the backend's
            // emit validation (audit #176). Seed with a sentinel
            // so the user has something visible to replace.
            return PropertyValue.wikilink(target: "placeholder")
        case .list:
            return PropertyValue.list(items: [])
        case .tagList:
            return PropertyValue.tagList(tags: [])
        }
    }

    private static func todayString() -> String {
        let f = DateFormatter()
        f.dateFormat = "yyyy-MM-dd"
        f.timeZone = TimeZone.current
        f.locale = Locale(identifier: "en_US_POSIX")
        return f.string(from: Date())
    }

    private static func nowString() -> String {
        let f = DateFormatter()
        f.dateFormat = "yyyy-MM-dd'T'HH:mm:ss"
        f.timeZone = TimeZone.current
        f.locale = Locale(identifier: "en_US_POSIX")
        return f.string(from: Date())
    }
}
