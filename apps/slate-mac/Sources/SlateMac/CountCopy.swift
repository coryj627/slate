// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Count-plus-noun copy for announcements, labels, and visible status
/// text. The Swift mirror of core's `count_noun` (sidebar_filter.rs):
/// the singular is taken only at exactly one, so zero stays plural
/// ("0 cards") to match the core summaries.
///
/// Every count string used to reimplement this ternary inline, which is
/// how "1 cards" and "Indexed 1 of 1 files." reached VoiceOver — the
/// misses clustered in the newest surfaces where the idiom was retyped
/// rather than shared. Route new count copy through here.
/// Generic over `BinaryInteger` because counts arrive as `Int` from
/// Swift collections and as `UInt64`/`UInt32` across the FFI boundary;
/// casting at every call site is what invites the next miss.
///
/// **Number formatting is NOT part of the contract.** `counted`
/// interpolates the value plainly — it does not group thousands and
/// does not localize. This is a deliberate divergence from core's
/// `count_noun`, which DOES group ("1,000 tags"), so the two are not
/// byte-identical at ≥ 1000. A caller that needs a grouped or
/// locale-formatted number formats it itself and takes `noun` for the
/// agreement alone (see `SidebarFileRow`'s word count).
enum CountCopy {
    /// `"1 card"`, `"3 cards"`, `"0 cards"`.
    static func counted(
        _ value: some BinaryInteger, _ singular: String, _ plural: String
    ) -> String {
        "\(value) \(noun(value, singular, plural))"
    }

    /// The bare noun for a count — for templates that place the number
    /// elsewhere, e.g. `"\(shown) of \(total) \(CountCopy.noun(total, …))"`.
    static func noun(
        _ value: some BinaryInteger, _ singular: String, _ plural: String
    ) -> String {
        value == 1 ? singular : plural
    }

    /// Present-tense verb agreement for an elided subject, as in
    /// "1 still has it inline" versus "2 still have it inline".
    static func verb(
        _ value: some BinaryInteger, _ singular: String, _ plural: String
    ) -> String {
        value == 1 ? singular : plural
    }
}
