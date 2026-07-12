// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Foundation
import SwiftUI

/// Editable row for one frontmatter property. The editor shape
/// switches on the inferred kind: scalar text / numeric / boolean
/// renders inline; list and tag-list render with add/remove
/// controls; wikilink offers a vault-file picker button alongside
/// the text field.
///
/// Commit happens via an explicit "Save" button (or Enter inside a
/// single-line field). Esc reverts the draft to the last-committed
/// value. The delete button — also keyboard-activated via
/// Cmd+Backspace when the row is focused — opens a confirmation
/// dialog whose default action is Cancel so an accidental Return
/// can't drop a property.
///
/// AppState owns the actual write; this view only mutates local
/// draft state and calls `setProperty` / `deleteProperty`.
struct PropertyEditorRow: View {
    let property: Property
    let path: String
    let vaultRoot: URL?

    @EnvironmentObject private var appState: AppState
    @State private var draft: PropertyEditDraft
    @State private var pendingDelete: Bool = false
    @State private var inputValidationError: String?
    /// One-slot newest-wins re-commit (see commitDraft's coalescing note).
    @State private var pendingRecommitDraft: PropertyEditDraft?

    /// Focus-return target for the delete-confirmation dialog
    /// (WCAG 2.4.3 / 2.1.2). The Cancel and Delete branches both
    /// assign `.row` so VoiceOver returns to this row after the
    /// dialog dismisses, rather than landing on whichever ancestor
    /// SwiftUI happened to fall back to.
    @AccessibilityFocusState private var deleteDialogFocusReturn: DeleteFocusTarget?

    enum DeleteFocusTarget: Hashable {
        case row
    }

    init(property: Property, path: String, vaultRoot: URL?) {
        self.property = property
        self.path = path
        self.vaultRoot = vaultRoot
        _draft = State(initialValue: PropertyEditDraft.from(property: property))
    }

    var body: some View {
        // Layout (issue #228): the label sits on its own row at the
        // top; editor + action buttons share the next row; validation
        // error sits below. The earlier shape pinned `actionButtons`
        // to the outer HStack's first text baseline (the label's),
        // which left the input row looking empty to the right of
        // the field.
        //
        // Alignment is `.top` (audit #237 red-team): on multi-row
        // variants (list / tagList) `.center` made Save / ⊖ sit at
        // the vertical mid-point of a 5-item list, adjacent to
        // item 3, with no visual association to any one row. `.top`
        // anchors them with the first item / single-line input —
        // for scalar variants the visual is identical to `.center`
        // because the editor is one line tall.
        //
        // Spacer between editor and buttons (audit #237 red-team):
        // without it, numeric editors (`.frame(minWidth: 80)` only)
        // leave Save / ⊖ floating in the middle of a wide sidebar.
        // The spacer pushes buttons to the trailing edge so they
        // anchor consistently across all panel widths and variants.
        //
        // Spacing 12 (audit #237 red-team): bumped from 8 to give
        // the field's focus ring + the delete button's button-style
        // ring room to coexist under macOS "Increase contrast"
        // without visually fusing into one fuzzy focus indicator.
        VStack(alignment: .leading, spacing: 4) {
            Text(property.key)
                .font(.callout.weight(.semibold))
                .foregroundStyle(.primary)
                .accessibilityHidden(true)

            HStack(alignment: .top, spacing: 12) {
                editor
                Spacer(minLength: 4)
                actionButtons
            }

            if let err = inputValidationError {
                Text(err)
                    .font(.caption)
                    .foregroundStyle(Tokens.ColorRole.destructiveText)
                    .accessibilityLabel("Validation error: \(err)")
            }
        }
        .padding(.vertical, 2)
        // Audit #237 red-team: pair the row-scoped focus return with
        // `accessibilityElement(children: .contain)` so VoiceOver
        // completes row traversal (editor → Save → delete → error)
        // before walking to the next row. Without `.contain`, a VO
        // user who Tabs forward after a failed commit would skip
        // the validation error and hear the next row's editor.
        .accessibilityElement(children: .contain)
        .accessibilityFocused($deleteDialogFocusReturn, equals: .row)
        .confirmationDialog(
            "Delete property `\(property.key)`?",
            isPresented: $pendingDelete,
            titleVisibility: .visible
        ) {
            // Cancel role first so platform keyboard dismissal
            // (Esc) leaves the property intact.
            Button("Cancel", role: .cancel) {
                deleteDialogFocusReturn = .row
            }
            Button("Delete", role: .destructive) {
                appState.deleteProperty(path: path, key: property.key)
                deleteDialogFocusReturn = .row
            }
        } message: {
            Text("This removes the `\(property.key)` key from the note's frontmatter.")
        }
        // Reset draft when the property updates from disk (e.g.
        // after a successful commit or external reload).
        .onChange(of: property) { _, newValue in
            draft = PropertyEditDraft.from(property: newValue)
            inputValidationError = nil
        }
    }

    @ViewBuilder
    private var editor: some View {
        switch draft {
        case .scalarText(let kind):
            scalarEditor(kind: kind)
        case .integer:
            integerEditor()
        case .float:
            floatEditor()
        case .boolean:
            booleanEditor()
        case .wikilink:
            wikilinkEditor()
        case .list:
            listEditor()
        case .tagList:
            tagListEditor()
        }
    }

    // MARK: Per-variant editors

    /// #857: date/datetime kinds whose STORED value parses cleanly get
    /// the platform `DatePicker` (which also validates for free — no
    /// more shape-valid nonsense like `2026-13-40` committing). The
    /// gate reads the stored property, NOT the mutable draft (Codex
    /// review) — see `storedValueTakesDatePicker`.
    /// Anything that does NOT parse — malformed, non-conforming, or
    /// empty — keeps the raw `TextField` verbatim: a picker over an
    /// unparseable value would have to invent a date, and this row
    /// never destroys data it can't represent.
    @ViewBuilder
    private func scalarEditor(kind: ScalarTextKind) -> some View {
        switch kind.kind {
        case "date" where Self.storedValueTakesDatePicker(storedDraft, kind: "date"):
            datePickerEditor()
        case "datetime"
        where Self.storedValueTakesDatePicker(storedDraft, kind: "datetime"):
            datetimePickerEditor()
        default:
            scalarTextEditor(kind: kind)
        }
    }

    /// The STORED value's draft form — picker-eligibility input only;
    /// the editable state stays in `draft`.
    private var storedDraft: PropertyEditDraft {
        PropertyEditDraft.from(property: property)
    }

    /// Codex review: eligibility gates on the STORED value, never the
    /// in-flight draft — correcting a malformed stored date inside the
    /// raw TextField must not swap the field out from under the edit
    /// before commit (the gate re-evaluates when the property reloads
    /// from disk after a successful commit). Static + pure so the gate
    /// itself is pinned by tests.
    static func storedValueTakesDatePicker(
        _ stored: PropertyEditDraft, kind: String
    ) -> Bool {
        guard case .scalarText(let k) = stored, k.kind == kind else { return false }
        switch kind {
        case "date":
            return PropertyDateEditing.date(fromDateString: k.value) != nil
        case "datetime":
            return PropertyDateEditing.datetime(fromString: k.value) != nil
        default:
            return false
        }
    }

    /// #857: `date` kind — textual field-style picker (the
    /// BaseQueryBuilderSheet DatePicker shape). Commits immediately on
    /// change, like the boolean toggle: a picked date has no
    /// intermediate edit state worth preserving.
    private func datePickerEditor() -> some View {
        DatePicker(
            "",
            selection: Binding(
                get: {
                    if case .scalarText(let k) = draft,
                        let date = PropertyDateEditing.date(fromDateString: k.value)
                    {
                        return date
                    }
                    return Date()
                },
                set: { newDate in
                    draft = .scalarText(
                        ScalarTextKind(
                            kind: "date",
                            value: PropertyDateEditing.dateString(from: newDate)))
                    commitDraft()
                }
            ),
            displayedComponents: [.date]
        )
        .datePickerStyle(.field)
        .labelsHidden()
        .accessibilityLabel(typeCuedLabel)
        // Newest-wins re-commit when the in-flight edit clears (see
        // commitDraft's coalescing note — rapid picker bursts).
        .onChange(of: appState.isEditingProperty) { _, editing in
            guard !editing, let pending = pendingRecommitDraft else { return }
            pendingRecommitDraft = nil
            draft = pending
            commitDraft()
        }
    }

    /// #857: `datetime` kind — same picker plus hour-and-minute. The
    /// serialized FORM is preserved from the stored value (ISO-8601
    /// with timezone stays ISO-8601 UTC; naive local stays naive
    /// local) so a picker edit never silently rewrites the property's
    /// datetime dialect.
    private func datetimePickerEditor() -> some View {
        DatePicker(
            "",
            selection: Binding(
                get: {
                    if case .scalarText(let k) = draft,
                        let parsed = PropertyDateEditing.datetime(fromString: k.value)
                    {
                        return parsed.date
                    }
                    return Date()
                },
                set: { newDate in
                    var form = PropertyDateEditing.DatetimeForm.localNaive
                    if case .scalarText(let k) = draft,
                        let parsed = PropertyDateEditing.datetime(fromString: k.value)
                    {
                        form = parsed.form
                    }
                    draft = .scalarText(
                        ScalarTextKind(
                            kind: "datetime",
                            value: PropertyDateEditing.datetimeString(
                                from: newDate, form: form)))
                    commitDraft()
                }
            ),
            displayedComponents: [.date, .hourAndMinute]
        )
        .datePickerStyle(.field)
        .labelsHidden()
        .accessibilityLabel(typeCuedLabel)
        // Newest-wins re-commit when the in-flight edit clears (see
        // commitDraft's coalescing note — rapid picker bursts).
        .onChange(of: appState.isEditingProperty) { _, editing in
            guard !editing, let pending = pendingRecommitDraft else { return }
            pendingRecommitDraft = nil
            draft = pending
            commitDraft()
        }
    }

    private func scalarTextEditor(kind: ScalarTextKind) -> some View {
        // Bound to a Binding<String> derived from `draft` so the
        // field edits the underlying enum case in place.
        TextField(
            "",
            text: Binding(
                get: {
                    if case .scalarText(let k) = draft, k == kind { return k.value }
                    return ""
                },
                set: { newValue in
                    var copy = kind
                    copy.value = newValue
                    draft = .scalarText(copy)
                }
            )
        )
        .textFieldStyle(.roundedBorder)
        .accessibilityLabel(typeCuedLabel)
        .onSubmit(commitDraft)
    }

    private func integerEditor() -> some View {
        HStack(spacing: 4) {
            TextField(
                "",
                text: Binding(
                    get: {
                        if case .integer(let s) = draft { return s }
                        return ""
                    },
                    set: { draft = .integer($0) }
                )
            )
            .textFieldStyle(.roundedBorder)
            .frame(minWidth: 80)
            .accessibilityLabel(typeCuedLabel)
            .onSubmit(commitDraft)
            Stepper("") {
                bumpInteger(by: 1)
            } onDecrement: {
                bumpInteger(by: -1)
            }
            .labelsHidden()
            .accessibilityLabel("Step \(property.key)")
        }
    }

    private func floatEditor() -> some View {
        HStack(spacing: 4) {
            TextField(
                "",
                text: Binding(
                    get: {
                        if case .float(let s) = draft { return s }
                        return ""
                    },
                    set: { draft = .float($0) }
                )
            )
            .textFieldStyle(.roundedBorder)
            .frame(minWidth: 80)
            .accessibilityLabel(typeCuedLabel)
            .onSubmit(commitDraft)
            Stepper("") {
                bumpFloat(by: 1)
            } onDecrement: {
                bumpFloat(by: -1)
            }
            .labelsHidden()
            .accessibilityLabel("Step \(property.key)")
        }
    }

    private func booleanEditor() -> some View {
        Toggle(
            isOn: Binding(
                get: {
                    if case .boolean(let b) = draft { return b }
                    return false
                },
                set: { newValue in
                    draft = .boolean(newValue)
                    // Commit immediately on toggle change — booleans
                    // have no intermediate edit state worth preserving.
                    commitDraft()
                }
            )
        ) {
            EmptyView()
        }
        .toggleStyle(.switch)
        .labelsHidden()
        .accessibilityLabel(typeCuedLabel)
    }

    private func wikilinkEditor() -> some View {
        HStack(spacing: 4) {
            TextField(
                "",
                text: Binding(
                    get: {
                        if case .wikilink(let s) = draft { return s }
                        return ""
                    },
                    set: { draft = .wikilink($0) }
                )
            )
            .textFieldStyle(.roundedBorder)
            .accessibilityLabel(typeCuedLabel)
            .onSubmit(commitDraft)
            // WCAG 2.5.3: AX label must contain the visible "Pick…"
            // so speech-control activation matches.
            Button("Pick…") {
                pickVaultFile()
            }
            .accessibilityLabel("Pick… vault file for \(property.key)")
        }
    }

    private func listEditor() -> some View {
        VStack(alignment: .leading, spacing: 2) {
            if case .list(var items) = draft {
                ForEach(items.indices, id: \.self) { idx in
                    HStack(spacing: 4) {
                        TextField(
                            "",
                            text: Binding(
                                get: { items[idx] },
                                set: { new in
                                    var copy = items
                                    copy[idx] = new
                                    draft = .list(copy)
                                }
                            )
                        )
                        .textFieldStyle(.roundedBorder)
                        .accessibilityLabel(
                            "Property \(property.key), item \(idx + 1) of \(items.count)"
                        )
                        Button("Remove") {
                            var copy = items
                            copy.remove(at: idx)
                            draft = .list(copy)
                        }
                        .accessibilityLabel("Remove item \(idx + 1) from \(property.key)")
                    }
                }
                Button("Add item") {
                    items.append("")
                    draft = .list(items)
                }
                .accessibilityLabel("Add item to \(property.key)")
            }
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel(typeCuedLabel)
    }

    private func tagListEditor() -> some View {
        VStack(alignment: .leading, spacing: 2) {
            if case .tagList(var tags) = draft {
                ForEach(tags.indices, id: \.self) { idx in
                    HStack(spacing: 4) {
                        TextField(
                            "",
                            text: Binding(
                                get: { tags[idx] },
                                set: { new in
                                    var copy = tags
                                    copy[idx] = new
                                    draft = .tagList(copy)
                                }
                            )
                        )
                        .textFieldStyle(.roundedBorder)
                        .accessibilityLabel(
                            "Property \(property.key), tag \(idx + 1) of \(tags.count)"
                        )
                        Button("Remove") {
                            var copy = tags
                            copy.remove(at: idx)
                            draft = .tagList(copy)
                        }
                        .accessibilityLabel("Remove tag \(idx + 1) from \(property.key)")
                    }
                }
                Button("Add tag") {
                    tags.append("")
                    draft = .tagList(tags)
                }
                .accessibilityLabel("Add tag to \(property.key)")
            }
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel(typeCuedLabel)
    }

    // MARK: Action buttons

    private var actionButtons: some View {
        // Audit #238: Save's window-scoped `.keyboardShortcut(.return)`
        // dropped. Each TextField variant already calls `commitDraft`
        // via `.onSubmit`, so in-field Return still commits. Removing
        // the window-level shortcut means Return inside a different
        // surface (a sheet, another row's editor) can no longer fire
        // *this* row's Save by accident. Tab-to-Save + Space/Return
        // still works via AppKit's default button activation.
        //
        // Audit #239: the delete button used to be icon-only with an
        // accessibility label that didn't appear visibly anywhere —
        // failing WCAG 2.5.3 (Label in Name). Replace with a "Delete"
        // text button so speech control + AT users see the same name
        // sighted users do. Destructive role gives the red treatment
        // the icon was carrying.
        HStack(spacing: 4) {
            Button("Save") { commitDraft() }
                .disabled(!hasUnsavedChanges || appState.isEditingProperty)
                .accessibilityLabel("Save changes to \(property.key)")
                .help("Save changes to \(property.key)")
            Button("Delete", role: .destructive) {
                pendingDelete = true
            }
            .keyboardShortcut(.delete, modifiers: .command)
            .accessibilityLabel("Delete property \(property.key)")
            .help("Delete property \(property.key)")
        }
    }

    // MARK: Commit / revert

    private var hasUnsavedChanges: Bool {
        self.draft != PropertyEditDraft.from(property: property)
    }

    /// Validate the draft, convert to FFI `PropertyValue`, and call
    /// AppState. Validation errors surface inline; nothing flushes
    /// to disk until the user resolves them.
    /// Coalescing (red-team #857): `setProperty` returns nil while an
    /// earlier edit is in flight (`isEditingProperty` guard). A rapid
    /// DatePicker burst used to DROP later picks — the earlier commit's
    /// reload then visibly reverted the control. One-slot newest-wins
    /// re-commit when the in-flight edit completes.
    private func commitDraft() {
        inputValidationError = nil
        let currentDraft: PropertyEditDraft = self.draft
        switch currentDraft.toPropertyValue() {
        case .success(let value):
            if appState.setProperty(path: path, key: property.key, value: value) == nil,
                appState.isEditingProperty {
                pendingRecommitDraft = currentDraft
            }
        case .failure(let error):
            inputValidationError = error.message
        }
    }

    private func bumpInteger(by delta: Int64) {
        guard case .integer(let s) = draft else { return }
        let current = Int64(s) ?? 0
        let next = current.addingReportingOverflow(delta)
        if next.overflow { return }
        draft = .integer(String(next.partialValue))
    }

    private func bumpFloat(by delta: Double) {
        guard case .float(let s) = draft else { return }
        let current = Double(s) ?? 0
        draft = .float(String(current + delta))
    }

    private func pickVaultFile() {
        let panel = NSOpenPanel()
        panel.canChooseFiles = true
        panel.canChooseDirectories = false
        panel.allowsMultipleSelection = false
        panel.allowedContentTypes = [.init(filenameExtension: "md")!]
        panel.directoryURL = vaultRoot
        guard panel.runModal() == .OK, let chosen = panel.url else { return }
        // Compute vault-relative path (drop the .md extension to
        // match Obsidian's wikilink convention).
        if let root = vaultRoot,
            let rel = chosen.path.removingPrefix(root.path + "/")
        {
            let stripped = (rel as NSString).deletingPathExtension
            draft = .wikilink(stripped)
        } else {
            // Fallback: file outside the vault root — store as-is
            // (the user can fix it manually).
            draft = .wikilink(chosen.lastPathComponent)
        }
    }

    // MARK: Accessibility

    private var typeCuedLabel: String {
        let typeWord: String = switch property.kind {
        case "text": "text"
        case "number": "number"
        case "boolean": "boolean"
        case "date": "date"
        case "datetime": "date and time"
        case "wikilink": "link"
        case "list": "list"
        case "tag_list": "tag list"
        default: property.kind
        }
        return "Property \(property.key), \(typeWord), editable"
    }
}

// MARK: - Draft model

/// Lightweight, hand-rolled mirror of `PropertyValue` that uses
/// string-backed editing for numeric kinds (so the user can type a
/// partial value like `"-"` without crashing the parser).
///
/// `scalarText` carries one of text / date / datetime kinds inside
/// `ScalarTextKind` so the row can both remember which kind we're
/// editing AND keep a single editable string.
enum PropertyEditDraft: Equatable {
    case scalarText(ScalarTextKind)
    case integer(String)
    case float(String)
    case boolean(Bool)
    case wikilink(String)
    case list([String])
    case tagList([String])

    static func from(property: Property) -> PropertyEditDraft {
        let display = PropertyValueDisplay.decode(
            kind: property.kind,
            valueJson: property.valueJson
        )
        switch property.kind {
        case "text":
            return .scalarText(ScalarTextKind(kind: "text", value: display.visibleText))
        case "date":
            return .scalarText(ScalarTextKind(kind: "date", value: display.tooltip))
        case "datetime":
            return .scalarText(ScalarTextKind(kind: "datetime", value: display.tooltip))
        case "number":
            // We don't know if the underlying value was integer or
            // float from `kind` alone — peek at the JSON.
            let trimmed = property.valueJson.trimmingCharacters(in: .whitespacesAndNewlines)
            if let _ = Int64(trimmed) {
                return .integer(trimmed)
            }
            return .float(trimmed)
        case "boolean":
            let parsed = (try? JSONSerialization.jsonObject(
                with: Data(property.valueJson.utf8),
                options: [.fragmentsAllowed])) as? Bool ?? false
            return .boolean(parsed)
        case "wikilink":
            return .wikilink(display.wikilinkTarget ?? display.visibleText)
        case "list":
            let items = decodeStringArray(json: property.valueJson)
            return .list(items)
        case "tag_list":
            let tags = decodeStringArray(json: property.valueJson)
            return .tagList(tags)
        default:
            return .scalarText(ScalarTextKind(kind: "text", value: display.visibleText))
        }
    }

    /// Validate + convert to the FFI `PropertyValue` for `setProperty`.
    /// Returns the validation error string on failure (shown inline
    /// in the editor row).
    func toPropertyValue() -> Result<PropertyValue, PropertyEditValidationError> {
        switch self {
        case .scalarText(let k):
            switch k.kind {
            case "text":
                return .success(PropertyValue.text(value: k.value))
            case "date":
                let trimmed = k.value.trimmingCharacters(in: .whitespacesAndNewlines)
                if !looksLikeDate(trimmed) {
                    return .failure(.init(message: "Date must be YYYY-MM-DD."))
                }
                return .success(PropertyValue.date(value: trimmed))
            case "datetime":
                return .success(PropertyValue.datetime(value: k.value))
            default:
                return .success(PropertyValue.text(value: k.value))
            }
        case .integer(let s):
            guard let n = Int64(s.trimmingCharacters(in: .whitespacesAndNewlines)) else {
                return .failure(.init(message: "Must be a whole number."))
            }
            return .success(PropertyValue.integer(value: n))
        case .float(let s):
            guard let n = Double(s.trimmingCharacters(in: .whitespacesAndNewlines)),
                n.isFinite
            else {
                return .failure(.init(message: "Must be a finite decimal number."))
            }
            return .success(PropertyValue.float(value: n))
        case .boolean(let b):
            return .success(PropertyValue.boolean(value: b))
        case .wikilink(let target):
            let trimmed = target.trimmingCharacters(in: .whitespacesAndNewlines)
            if trimmed.isEmpty {
                return .failure(.init(message: "Wikilink target can't be empty."))
            }
            if trimmed.contains("]]") {
                return .failure(.init(message: "Wikilink target can't contain `]]`."))
            }
            return .success(PropertyValue.wikilink(target: trimmed))
        case .list(let items):
            let mapped: [PropertyValue] = items.map { PropertyValue.text(value: $0) }
            return .success(PropertyValue.list(items: mapped))
        case .tagList(let tags):
            // The backend strips a leading `#` on emit (audit
            // #180); we can pass either form without surprise.
            return .success(PropertyValue.tagList(tags: tags))
        }
    }
}

/// Sub-discriminator for `PropertyEditDraft.scalarText` so a single
/// editor flavour can carry text / date / datetime without a
/// separate case per row.
struct ScalarTextKind: Equatable {
    let kind: String  // "text" | "date" | "datetime"
    var value: String
}

/// Wrapper so `Result`'s failure type satisfies the Error protocol
/// while carrying a UI-ready message string.
struct PropertyEditValidationError: Error, Equatable {
    let message: String
}

/// #857: pure parse/serialize for the date & datetime DatePicker
/// editors. Internal (not nested in the view) so the round-trip and
/// malformed-fallback contracts are unit-testable without rendering.
///
/// Accepted forms mirror `PropertyValueDisplay` exactly (the row and
/// the read-only display must agree on what "conforming" means):
///  - date: strict `yyyy-MM-dd`, parsed in `TimeZone.current` (the
///    Codoki PR 83 lesson — UTC parsing drifts the rendered day for
///    users west of UTC). Calendar-validated by `DateFormatter`'s
///    non-lenient parse, so `2026-13-40` — which the shape check
///    `looksLikeDate` accepts — correctly fails here and keeps the
///    raw TextField.
///  - datetime: ISO-8601 with timezone (`2026-07-11T09:30:00Z` /
///    `…+02:00`) or the naive local form (`2026-07-11T09:30:00`).
///    The parsed FORM is reported so serialization preserves the
///    stored dialect.
enum PropertyDateEditing {

    /// Which datetime dialect the stored value used — serialization
    /// preserves it (`iso8601` re-emits with a UTC `Z` suffix; a
    /// non-UTC stored offset normalizes to `Z` only when the user
    /// actually edits the value, never on load).
    enum DatetimeForm: Equatable {
        case iso8601
        case localNaive
    }

    static func date(fromDateString string: String) -> Date? {
        dateParser.date(from: string)
    }

    static func dateString(from date: Date) -> String {
        dateParser.string(from: date)
    }

    static func datetime(fromString string: String) -> (date: Date, form: DatetimeForm)? {
        if let date = isoParser.date(from: string) {
            return (date, .iso8601)
        }
        if let date = localNaiveParser.date(from: string) {
            return (date, .localNaive)
        }
        return nil
    }

    static func datetimeString(from date: Date, form: DatetimeForm) -> String {
        switch form {
        case .iso8601:
            return isoParser.string(from: date)
        case .localNaive:
            return localNaiveParser.string(from: date)
        }
    }

    // Cached formatters (the PropertyValueDisplay pattern — expensive
    // to construct). `en_US_POSIX` pins digit shapes; NOT lenient, so
    // out-of-calendar values fail parse instead of wrapping.
    private static let dateParser: DateFormatter = {
        let f = DateFormatter()
        f.dateFormat = "yyyy-MM-dd"
        f.timeZone = TimeZone.current
        f.locale = Locale(identifier: "en_US_POSIX")
        return f
    }()

    private static let isoParser: ISO8601DateFormatter = {
        let f = ISO8601DateFormatter()
        f.formatOptions = [.withInternetDateTime]
        return f
    }()

    private static let localNaiveParser: DateFormatter = {
        let f = DateFormatter()
        f.dateFormat = "yyyy-MM-dd'T'HH:mm:ss"
        f.timeZone = TimeZone.current
        f.locale = Locale(identifier: "en_US_POSIX")
        return f
    }()
}

private func decodeStringArray(json: String) -> [String] {
    let data = Data(json.utf8)
    guard
        let items = (try? JSONSerialization.jsonObject(with: data, options: []))
            as? [Any]
    else {
        return []
    }
    return items.map { item in
        if let s = item as? String { return s }
        if let n = item as? NSNumber { return n.stringValue }
        if let b = item as? Bool { return b ? "true" : "false" }
        return String(describing: item)
    }
}

private func looksLikeDate(_ s: String) -> Bool {
    // YYYY-MM-DD shape check. We don't validate calendar correctness
    // here — the backend's frontmatter pipeline will accept anything
    // matching this shape and re-emit it verbatim. Calendar checks
    // would be more user-hostile (Feb 30 etc.) without a clear win.
    let parts = s.split(separator: "-")
    guard parts.count == 3 else { return false }
    return parts[0].count == 4 && parts[1].count == 2 && parts[2].count == 2
        && parts.allSatisfy { $0.allSatisfy(\.isNumber) }
}

private extension String {
    /// Trim `prefix` from the start of the string, returning nil if
    /// it isn't present. Used by the wikilink picker to compute a
    /// vault-relative path.
    func removingPrefix(_ prefix: String) -> String? {
        guard hasPrefix(prefix) else { return nil }
        return String(dropFirst(prefix.count))
    }
}
