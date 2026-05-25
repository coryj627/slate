import Foundation
import SwiftUI

/// Frontmatter properties sidebar section. Editable per-row via
/// `PropertyEditorRow`, with header affordances for adding a new
/// property and renaming a key across the whole vault.
///
/// Sits below `OutlineSidebar`-style sections and above
/// `BacklinksPanel` / `OutgoingLinksPanel` in the sidebar column.
struct PropertiesPanel: View {
    @EnvironmentObject private var appState: AppState
    @State private var isExpanded = true

    var body: some View {
        // Hide entirely when no note is selected. We keep the panel
        // visible (rather than EmptyView) when the note has no
        // properties yet — the Add button needs to be reachable.
        if appState.selectedFilePath == nil {
            EmptyView()
        } else {
            DisclosureGroup(isExpanded: $isExpanded) {
                content
            } label: {
                header
            }
            .padding(.horizontal, 8)
            .padding(.vertical, 4)
            .keyboardShortcut(KeyEquivalent("r"), modifiers: [.command, .shift])
            // Hidden trigger for Cmd+Shift+R when the panel has
            // keyboard focus — opens the bulk-rename sheet. The
            // visible "Bulk rename" button below is the discoverable
            // surface; this shortcut is for power users.
            .background(
                Button("") {
                    appState.isBulkRenameSheetOpen = true
                }
                .keyboardShortcut(KeyEquivalent("r"), modifiers: [.command, .shift])
                .opacity(0)
                .accessibilityHidden(true)
            )
        }
    }

    private var header: some View {
        let count = appState.currentNoteProperties.count
        let suffix = count == 1 ? "item" : "items"
        return HStack {
            Text("Properties, \(count) \(suffix)")
                .font(.headline)
                .accessibilityAddTraits(.isHeader)
            Spacer()
            Button {
                appState.isAddPropertySheetOpen = true
            } label: {
                Image(systemName: "plus.circle")
            }
            .buttonStyle(.borderless)
            .help("Add property")
            .accessibilityLabel("Add property")
            .disabled(appState.loadedFilePath == nil)
            Button {
                appState.isBulkRenameSheetOpen = true
            } label: {
                Image(systemName: "rectangle.2.swap")
            }
            .buttonStyle(.borderless)
            .help("Rename property across the vault")
            .accessibilityLabel("Rename property across the vault")
            .disabled(appState.currentSession == nil)
        }
    }

    private var content: some View {
        VStack(alignment: .leading, spacing: 4) {
            if appState.currentNoteProperties.isEmpty {
                // WCAG 2.5.3: the speech-control label has to contain
                // the visible string verbatim so a user saying the
                // sentence triggers the same element.
                Text("No properties yet. Add one to start.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .padding(.vertical, 2)
                    .accessibilityLabel("No properties yet. Add one to start.")
            } else if let path = appState.loadedFilePath {
                ForEach(Array(appState.currentNoteProperties.enumerated()), id: \.offset) {
                    _,
                    property in
                    PropertyEditorRow(
                        property: property,
                        path: path,
                        vaultRoot: appState.currentVaultURL
                    )
                }
            }
        }
    }
}

/// Decoded view-model for one property: the human-readable display
/// text, an accessibility label that includes a type cue, and the
/// optional wikilink target if the property is a wikilink.
///
/// Used by `PropertyEditorRow` (for the editor's pre-fill draft)
/// and the AX label.
struct PropertyValueDisplay {
    let visibleText: String
    let tooltip: String
    let kind: String
    let listCount: Int?
    let wikilinkTarget: String?

    /// Decode the FFI Property's `(kind, valueJson)` pair into a
    /// view-model. Returns a "raw JSON visible" fallback if the JSON
    /// is malformed so the row stays usable in the UI.
    static func decode(kind: String, valueJson: String) -> PropertyValueDisplay {
        let data = Data(valueJson.utf8)
        let parsed = try? JSONSerialization.jsonObject(
            with: data, options: [.fragmentsAllowed])

        switch kind {
        case "text":
            let s = (parsed as? String) ?? valueJson
            return PropertyValueDisplay(
                visibleText: s, tooltip: s, kind: kind,
                listCount: nil, wikilinkTarget: nil)

        case "number":
            return PropertyValueDisplay(
                visibleText: numberText(from: parsed, fallback: valueJson),
                tooltip: numberText(from: parsed, fallback: valueJson),
                kind: kind, listCount: nil, wikilinkTarget: nil)

        case "boolean":
            let b = parsed as? Bool
            let text = b == true ? "true" : (b == false ? "false" : valueJson)
            return PropertyValueDisplay(
                visibleText: text, tooltip: text, kind: kind,
                listCount: nil, wikilinkTarget: nil)

        case "date":
            let raw = (parsed as? String) ?? valueJson
            let formatted = formatDate(raw)
            return PropertyValueDisplay(
                visibleText: formatted, tooltip: raw, kind: kind,
                listCount: nil, wikilinkTarget: nil)

        case "datetime":
            let raw = (parsed as? String) ?? valueJson
            let formatted = formatDatetime(raw)
            return PropertyValueDisplay(
                visibleText: formatted, tooltip: raw, kind: kind,
                listCount: nil, wikilinkTarget: nil)

        case "wikilink":
            let target = (parsed as? String) ?? valueJson
            return PropertyValueDisplay(
                visibleText: target, tooltip: "[[\(target)]]", kind: kind,
                listCount: nil, wikilinkTarget: target)

        case "list":
            let items = (parsed as? [Any]) ?? []
            let strings = items.map(describeJsonValue)
            let joined = strings.joined(separator: ", ")
            return PropertyValueDisplay(
                visibleText: joined, tooltip: joined, kind: kind,
                listCount: strings.count, wikilinkTarget: nil)

        case "tag_list":
            let items = (parsed as? [String]) ?? []
            let withHash = items.map { "#\($0)" }
            let joined = withHash.joined(separator: ", ")
            return PropertyValueDisplay(
                visibleText: joined, tooltip: joined, kind: kind,
                listCount: items.count, wikilinkTarget: nil)

        default:
            return PropertyValueDisplay(
                visibleText: valueJson, tooltip: valueJson, kind: kind,
                listCount: nil, wikilinkTarget: nil)
        }
    }

    /// Type-cued VoiceOver label per the acceptance criteria.
    func accessibilityLabel(for key: String) -> String {
        switch kind {
        case "list":
            let n = listCount ?? 0
            return "Property \(key), list of \(n): \(visibleText)"
        case "tag_list":
            let n = listCount ?? 0
            return "Property \(key), tag list of \(n): \(visibleText)"
        case "date":
            return "Property \(key), date: \(visibleText)"
        case "datetime":
            return "Property \(key), date and time: \(visibleText)"
        case "wikilink":
            return "Property \(key), link to \(visibleText)"
        case "boolean":
            return "Property \(key), boolean: \(visibleText)"
        case "number":
            return "Property \(key), number: \(visibleText)"
        default:
            return "Property \(key): \(visibleText)"
        }
    }

    private static func describeJsonValue(_ value: Any) -> String {
        if let s = value as? String { return s }
        if let b = value as? Bool { return b ? "true" : "false" }
        if let n = value as? NSNumber {
            // NSNumber may be a Bool internally; the Bool guard above
            // catches that case first.
            return n.stringValue
        }
        return String(describing: value)
    }

    private static func numberText(from parsed: Any?, fallback: String) -> String {
        if let n = parsed as? NSNumber { return n.stringValue }
        return fallback
    }

    /// Format `YYYY-MM-DD` via the current locale's medium date style.
    /// Falls back to the raw string when the date can't be parsed —
    /// keeps the row useful even if the user typed a non-standard
    /// format that yaml-rust2 still recognized as a date pattern.
    ///
    /// Parsed and formatted in `TimeZone.current` (not UTC) so a
    /// date like `2024-01-02` doesn't drift back to Jan 1 when the
    /// user's TZ is west of UTC — Codoki PR 83 callout: previously
    /// the UTC-parsed date would render as a different day for many
    /// users.
    private static func formatDate(_ raw: String) -> String {
        guard let date = Self.dateParser.date(from: raw) else { return raw }
        return Self.dateFormatter.string(from: date)
    }

    private static func formatDatetime(_ raw: String) -> String {
        if let date = Self.isoFormatter.date(from: raw) {
            return Self.datetimeFormatter.string(from: date)
        }
        if let date = Self.relaxedDatetimeParser.date(from: raw) {
            return Self.datetimeFormatter.string(from: date)
        }
        return raw
    }

    // Cached formatters: DateFormatter / ISO8601DateFormatter are
    // expensive to construct (CFLocale lookups, ICU init).
    private static let dateParser: DateFormatter = {
        let f = DateFormatter()
        f.dateFormat = "yyyy-MM-dd"
        f.timeZone = TimeZone.current
        f.locale = Locale(identifier: "en_US_POSIX")
        return f
    }()

    private static let dateFormatter: DateFormatter = {
        let f = DateFormatter()
        f.dateStyle = .medium
        f.timeStyle = .none
        f.locale = Locale.current
        return f
    }()

    private static let isoFormatter: ISO8601DateFormatter = {
        let f = ISO8601DateFormatter()
        f.formatOptions = [.withInternetDateTime]
        return f
    }()

    private static let relaxedDatetimeParser: DateFormatter = {
        let f = DateFormatter()
        f.dateFormat = "yyyy-MM-dd'T'HH:mm:ss"
        f.timeZone = TimeZone.current
        f.locale = Locale(identifier: "en_US_POSIX")
        return f
    }()

    private static let datetimeFormatter: DateFormatter = {
        let f = DateFormatter()
        f.dateStyle = .medium
        f.timeStyle = .short
        f.locale = Locale.current
        return f
    }()
}
