// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! The case fold every name match in core shares (#1705, plan 47 CB-32,
//! the owner's CD22).
//!
//! Rust's `str::to_lowercase` applies one context rule, Unicode's
//! Final_Sigma: a capital sigma lowercases to the final form `ς` (U+03C2)
//! at the end of a word and to `σ` (U+03C3) before more letters. So a stem
//! lowered alone and the same stem lowered with its extension fold apart:
//! `[[ΚΟΣΜΟΣ]]` lowers to `κοσμος`, while the file `ΚΟΣΜΟΣ.md` lowers to
//! `κοσμοσ.md` (the `.` is case-ignorable and `m` is cased, so that sigma
//! is not final). Folding `ς` to `σ` after lowercasing makes the fold
//! context-free, so the two meet.
//!
//! Volumes do not all agree on the two sigmas. NTFS keeps `κοσμος.txt` and
//! `κοσμοσ.txt` apart: both can exist in one folder, and neither opens
//! under the other's spelling (measured for CB-32's red team on NTFS with
//! case sensitivity off). APFS, by reading only, folds names with Unicode
//! CaseFolding (`03C2; C; 03C3`) and treats them as one name. The
//! collision gate refuses a name that would alias on a case-insensitive
//! volume, so it refuses such a pair for portability.

/// The lowercase form core compares names, paths and link targets in:
/// full-Unicode `to_lowercase`, then the final sigma folded to sigma.
/// No normalization: [`crate::db::tree_sort_key`] adds NFC where it is
/// stored.
pub(crate) fn fold_case(text: &str) -> String {
    let lower = text.to_lowercase();
    if lower.contains('\u{03C2}') {
        lower.replace('\u{03C2}', "\u{03C3}")
    } else {
        lower
    }
}

/// The per-character half of [`fold_case`], for a matcher that lowercases
/// one character at a time: the final sigma becomes sigma, every other
/// character is unchanged.
pub(crate) fn fold_sigma(c: char) -> char {
    if c == '\u{03C2}' { '\u{03C3}' } else { c }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn every_sigma_spelling_folds_to_one() {
        for spelling in ["ΚΟΣΜΟΣ", "κοσμος", "Κοσμος", "κοσμοσ", "ΚΟΣΜΟΣ.md"]
        {
            let folded = fold_case(spelling);
            assert!(
                folded.starts_with("κοσμοσ"),
                "{spelling:?} folds to {folded:?}"
            );
        }
        assert_eq!(fold_case("ΚΟΣΜΟΣ.md"), fold_case("κοσμος.md"));
    }

    #[test]
    fn a_name_without_a_final_sigma_folds_as_to_lowercase_does() {
        for name in ["Alpha.md", "ÉTUDE", "İstanbul", "日本語.md", "Straße"] {
            assert_eq!(fold_case(name), name.to_lowercase(), "{name:?}");
        }
    }

    #[test]
    fn the_per_character_fold_maps_only_the_final_sigma() {
        assert_eq!(fold_sigma('ς'), 'σ');
        assert_eq!(fold_sigma('σ'), 'σ');
        assert_eq!(fold_sigma('a'), 'a');
    }
}
