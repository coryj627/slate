// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Test-only helper that blanks out Swift comments and string
/// literals so structural source-scraping tests don't false-match
/// on a pattern that only appears inside a comment or a string.
///
/// Motivating case (#333): `testSettingsSceneStillExistsInSlateMacApp`
/// greps `SlateMacApp.swift` for `Settings {` to prove the scene
/// declaration is still present. A future
/// `// TODO: bring back the Settings { } scene` comment, or a
/// `let s = "Settings { ... }"` string, would false-pass that
/// check. Running the source through this stripper first removes
/// those occurrences.
///
/// **Approach.** A single character-walk state machine. Stripped
/// regions are replaced with spaces (newlines preserved) so the
/// output stays the same length and line structure as the input —
/// a regex / line-number-reporting test sees identical offsets,
/// just with comment + string *content* blanked.
///
/// **Handled:**
/// - `//` line comments (to end of line)
/// - `/* … */` block comments, including Swift's *nesting* (`/* /* */ */`)
/// - `"…"` string literals, with `\"` / `\\` escape handling
///
/// **Known limitations** (none present in the files this currently
/// scans; documented so a future adopter for a richer file knows
/// the edges):
/// - String *interpolation* `"\(code)"` — the interpolated code is
///   blanked along with the string, not preserved. Fine for
///   structural greps that don't expect their pattern inside an
///   interpolation.
/// - Multiline string literals (`"""…"""`) and raw strings
///   (`#"…"#`) are NOT modelled — the first `"` is treated as an
///   ordinary string delimiter. A file using these would need a
///   richer lexer (or `swift-syntax`, per #333's option 2).
enum SwiftSourceStripping {

    private enum State: Equatable {
        case code
        case lineComment
        case blockComment(depth: Int)
        case string
    }

    /// Returns `source` with comment and string-literal content
    /// replaced by spaces (newlines kept). See the type doc for the
    /// handled cases and limitations.
    static func strippingCommentsAndStrings(_ source: String) -> String {
        let chars = Array(source)
        let n = chars.count
        var result = String()
        result.reserveCapacity(n)

        var state: State = .code
        var i = 0

        /// Append `c` verbatim if it's a newline, else a space —
        /// used inside stripped regions to preserve line structure
        /// while blanking content.
        func appendBlanked(_ c: Character) {
            result.append(c == "\n" ? c : " ")
        }

        while i < n {
            let c = chars[i]
            let next: Character? = (i + 1 < n) ? chars[i + 1] : nil

            switch state {
            case .code:
                if c == "/", next == "/" {
                    state = .lineComment
                    result.append("  ")  // blank the `//`
                    i += 2
                } else if c == "/", next == "*" {
                    state = .blockComment(depth: 1)
                    result.append("  ")  // blank the `/*`
                    i += 2
                } else if c == "\"" {
                    state = .string
                    result.append(" ")  // blank the opening quote
                    i += 1
                } else {
                    result.append(c)
                    i += 1
                }

            case .lineComment:
                if c == "\n" {
                    state = .code
                    result.append(c)  // keep the newline, end the comment
                } else {
                    result.append(" ")
                }
                i += 1

            case .blockComment(let depth):
                if c == "/", next == "*" {
                    state = .blockComment(depth: depth + 1)  // Swift nests
                    result.append("  ")
                    i += 2
                } else if c == "*", next == "/" {
                    let newDepth = depth - 1
                    state = (newDepth == 0) ? .code : .blockComment(depth: newDepth)
                    result.append("  ")
                    i += 2
                } else {
                    appendBlanked(c)
                    i += 1
                }

            case .string:
                if c == "\\" {
                    // Escape sequence: blank this char and the next
                    // so an escaped quote (`\"`) doesn't prematurely
                    // close the string.
                    result.append(" ")
                    if next != nil {
                        result.append(" ")
                        i += 2
                    } else {
                        i += 1
                    }
                } else if c == "\"" {
                    state = .code
                    result.append(" ")  // blank the closing quote
                    i += 1
                } else {
                    appendBlanked(c)
                    i += 1
                }
            }
        }

        return result
    }
}
