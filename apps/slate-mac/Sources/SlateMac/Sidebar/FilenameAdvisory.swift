// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// A deterministic, advisory-only description of filename characters that can
/// be rejected by other file systems or make wikilinks less portable. It never
/// participates in validation: the backend remains authoritative at commit.
struct FilenameAdvisory: Equatable {
    let filesystemCharacters: [String]
    let wikilinkCharacters: [String]
    let message: String?
    let accessibilityMessage: String?

    init(_ name: String) {
        filesystemCharacters = [
            name.contains("/") ? "/" : nil,
            name.contains("\\") ? "\\" : nil,
            name.contains(":") ? ":" : nil,
            name.unicodeScalars.contains(where: { $0.value == 0 })
                ? "null character" : nil,
        ].compactMap { $0 }
        wikilinkCharacters = [
            name.contains("[") ? "[" : nil,
            name.contains("]") ? "]" : nil,
            name.contains("#") ? "#" : nil,
            name.contains("^") ? "^" : nil,
            name.contains("|") ? "|" : nil,
        ].compactMap { $0 }

        let filesystemMessage = filesystemCharacters.isEmpty
            ? nil
            : "Some file systems may reject these characters: "
                + filesystemCharacters.joined(separator: ", ") + "."
        let wikilinkMessage = wikilinkCharacters.isEmpty
            ? nil
            : "These characters can make wikilinks less portable: "
                + wikilinkCharacters.joined(separator: ", ") + "."
        let parts = [filesystemMessage, wikilinkMessage].compactMap { $0 }
        message = parts.isEmpty ? nil : parts.joined(separator: " ")

        let spokenFilesystemMessage = filesystemCharacters.isEmpty
            ? nil
            : "Some file systems may reject these characters: "
                + filesystemCharacters.map(Self.spokenName).joined(separator: ", ") + "."
        let spokenWikilinkMessage = wikilinkCharacters.isEmpty
            ? nil
            : "These characters can make wikilinks less portable: "
                + wikilinkCharacters.map(Self.spokenName).joined(separator: ", ") + "."
        let spokenParts = [spokenFilesystemMessage, spokenWikilinkMessage].compactMap { $0 }
        accessibilityMessage = spokenParts.isEmpty
            ? nil
            : spokenParts.joined(separator: " ")
    }

    var isEmpty: Bool { message == nil }

    private static func spokenName(for character: String) -> String {
        switch character {
        case "/": "forward slash"
        case "\\": "backslash"
        case ":": "colon"
        case "[": "opening bracket"
        case "]": "closing bracket"
        case "#": "number sign"
        case "^": "caret"
        case "|": "vertical bar"
        default: character
        }
    }
}

/// Suppresses presentation-time chatter and ordinary keystrokes. A new,
/// non-empty semantic risk set announces once; clearing stays silent while
/// rearming the gate if the user later reintroduces a risk.
struct FilenameAdvisoryAnnouncementGate {
    private var current: FilenameAdvisory

    init(initial: FilenameAdvisory) {
        current = initial
    }

    mutating func announcement(for newValue: FilenameAdvisory) -> String? {
        guard newValue != current else { return nil }
        current = newValue
        return newValue.accessibilityMessage
    }
}

/// Shared visible and VoiceOver representation for inline and sheet naming.
/// The warning symbol and explicit copy make meaning independent of color.
struct FilenameAdvisoryView: View {
    let advisory: FilenameAdvisory
    @State private var announcementGate: FilenameAdvisoryAnnouncementGate

    init(name: String) {
        let advisory = FilenameAdvisory(name)
        self.advisory = advisory
        _announcementGate = State(
            initialValue: FilenameAdvisoryAnnouncementGate(initial: advisory))
    }

    var body: some View {
        Group {
            if let message = advisory.message,
                let accessibilityMessage = advisory.accessibilityMessage
            {
                HStack(alignment: .firstTextBaseline, spacing: Tokens.Spacing.xs) {
                    SlateSymbol.warning.decorative
                        .foregroundStyle(Tokens.ColorRole.warningText)
                    Text(message)
                        .font(Tokens.Typography.caption)
                        .foregroundStyle(Tokens.ColorRole.warningText)
                        .lineLimit(nil)
                        .fixedSize(horizontal: false, vertical: true)
                }
                .accessibilityElement(children: .ignore)
                .accessibilityLabel("Filename warning")
                .accessibilityValue(accessibilityMessage)
                .accessibilityHint(
                    "This advisory does not block submission; the name is still validated when you continue."
                )
            }
        }
        .onChange(of: advisory) { _, newValue in
            if let accessibilityMessage = announcementGate.announcement(for: newValue) {
                // W0.5-3 residue: FilenameAdvisory.accessibilityMessage
                postAccessibilityAnnouncement(
                    .hostComposed(text: accessibilityMessage, priority: .medium))
            }
        }
    }
}
