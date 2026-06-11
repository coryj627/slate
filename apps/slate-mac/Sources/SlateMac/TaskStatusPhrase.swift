// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

/// #423 (VO test F-G1): spoken status phrase for a task row.
///
/// The Rust parser preserves the raw Tasks-plugin status char; the
/// Swift surfaces previously collapsed everything non-`x` to
/// "Open task", so `[/]` (in progress) and `[-]` (cancelled) both
/// read as open — cancelled-as-open actively misleads. Phrase the
/// two common non-standard chars distinctly; anything else falls
/// back to the completed/open binary so unknown markers stay
/// conservative.
extension TaskItem {
    var statusPhrase: String {
        switch statusChar {
        case "/": return "In-progress task."
        case "-": return "Cancelled task."
        default: return completed ? "Done task." : "Open task."
        }
    }

    /// Leading status word for the row label prefix.
    var statusWord: String {
        switch statusChar {
        case "/": return "In progress"
        case "-": return "Cancelled"
        default: return completed ? "Done" : "Open"
        }
    }
}
