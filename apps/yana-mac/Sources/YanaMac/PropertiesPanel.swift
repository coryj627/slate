import Foundation
import SwiftUI

/// Frontmatter properties sidebar section. Read-only display of the
/// currently-selected note's YAML frontmatter as a typed list.
///
/// Sits below `OutlineSidebar`-style sections and above
/// `BacklinksPanel` / `OutgoingLinksPanel` in the sidebar column.
/// Each row's accessibility label includes a type cue ("Property
/// <key>: <value>", "Property <key>, list of N: …", etc.) so
/// VoiceOver users hear what kind of data they're navigating
/// without having to inspect the visual style.
struct PropertiesPanel: View {
    @EnvironmentObject private var appState: AppState
    @State private var isExpanded = true

    var body: some View {
        // Hide entirely when no note is selected OR when the
        // currently-selected note has no frontmatter. EmptyView
        // removes the panel from the AX tree so VoiceOver doesn't
        // enumerate an empty section.
        if appState.selectedFilePath == nil || appState.currentNoteProperties.isEmpty {
            EmptyView()
        } else {
            DisclosureGroup(isExpanded: $isExpanded) {
                content
            } label: {
                header
            }
            .padding(.horizontal, 8)
            .padding(.vertical, 4)
        }
    }

    private var header: some View {
        let count = appState.currentNoteProperties.count
        let suffix = count == 1 ? "item" : "items"
        return Text("Properties, \(count) \(suffix)")
            .font(.headline)
            .accessibilityAddTraits(.isHeader)
    }

    private var content: some View {
        VStack(alignment: .leading, spacing: 4) {
            ForEach(Array(appState.currentNoteProperties.enumerated()), id: \.offset) {
                _,
                property in
                PropertyRow(property: property)
            }
        }
    }
}

/// Single property row. Split into its own view so the panel can
/// keep type-specific formatting localized.
///
/// Wikilink rows render as static text rather than buttons. The
/// previous shape wrapped wikilink rows in a `Button` with hint
/// "Opens the linked note." but the activation path hard-coded
/// `isUnresolved: true`, so every press announced "<target> is
/// unresolved. Cannot open." — the hint was a promise the UI
/// couldn't keep (WCAG 2.5.3 label-in-name concern, #90). Until a
/// real wikilink resolver lands for frontmatter values, we drop
/// the button and let the type-cued accessibility label
/// ("Property X, link to Y") tell the user what the row is
/// without overpromising activation.
private struct PropertyRow: View {
    let property: Property

    var body: some View {
        let display = PropertyValueDisplay.decode(
            kind: property.kind,
            valueJson: property.valueJson
        )
        rowContent(display: display)
            .accessibilityElement(children: .combine)
            .accessibilityLabel(display.accessibilityLabel(for: property.key))
    }

    private func rowContent(display: PropertyValueDisplay) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(property.key)
                .font(.callout.weight(.semibold))
                .foregroundStyle(.primary)
            // No lineLimit: property values are user-authored data
            // (a sentence-long description, a long list, a wikilink
            // path, etc.). At large Dynamic Type sizes the previous
            // `.lineLimit(3)` truncated below the WCAG 1.4.4 threshold
            // for sighted users (`.help()` tooltip helps mouse users
            // but not keyboard-only ones). Let it wrap.
            Text(display.visibleText)
                .font(.callout)
                .foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(.vertical, 2)
        .help(display.tooltip)
    }
}

/// Decoded view-model for one property: the human-readable display
/// text, an accessibility label that includes a type cue, and the
/// optional wikilink target if the property is a wikilink (which
/// drives the row's button activation).
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
        // ISO 8601 covers `YYYY-MM-DDTHH:MM:SS[Z|±HH:MM]`. Try the
        // strictest formatter first; fall back to a relaxed parse
        // for Z-less local times like `2024-01-02T03:04:05` — those
        // are interpreted as `TimeZone.current` per the YAML
        // frontmatter convention (the user typed them; if they
        // wanted UTC, they would have added the `Z`).
        if let date = Self.isoFormatter.date(from: raw) {
            return Self.datetimeFormatter.string(from: date)
        }
        if let date = Self.relaxedDatetimeParser.date(from: raw) {
            return Self.datetimeFormatter.string(from: date)
        }
        return raw
    }

    // Cached formatters: DateFormatter / ISO8601DateFormatter are
    // expensive to construct (CFLocale lookups, ICU init), and the
    // Properties Panel re-decodes on every selection change.
    // Cached as `static let` so all rows in a vault session reuse
    // the same instances.
    //
    // Thread safety: DateFormatter's `date(from:)` /
    // `string(from:)` are documented thread-safe on macOS 10.9+ and
    // iOS 7+; we read-only after construction so this is fine.
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
