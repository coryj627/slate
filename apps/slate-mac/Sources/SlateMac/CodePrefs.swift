// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Per-user preferences for the code pipeline's AT preamble
/// verbosity. Sister type to `MathPrefs`.
///
/// Selected via Settings (#224). Settings panel surface intentionally
/// limited to verbosity for V1; font / theme / per-language colour
/// preferences are V1.x.
struct CodePrefs: Equatable, Codable {
    var verbosity: CodeVerbosity = .preambleOnly
}

/// How much AT context the rendered code block carries. Affects the
/// `accessibilityLabel` that `CodeBlockView` surfaces to VoiceOver.
///
/// - `.preambleOnly` — "Code block, rust, 5 lines." (current default
///   in `CodeBlockView`).
/// - `.preambleFirstLine` — adds the first non-blank line so users
///   get the signature / opening statement before drilling in.
/// - `.preambleAllTokens` — adds the full token stream as part of
///   the label (overkill for casual reading, useful for braille
///   display work).
enum CodeVerbosity: String, Codable, CaseIterable, Equatable {
    case preambleOnly
    case preambleFirstLine
    case preambleAllTokens

    /// Human-readable label for Settings UI.
    var displayName: String {
        switch self {
        case .preambleOnly: return "Preamble only"
        case .preambleFirstLine: return "Preamble + first line"
        case .preambleAllTokens: return "Preamble + all tokens"
        }
    }
}
