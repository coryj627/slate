// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! The main window's title (plan 46 DS-12, DSD9). A screen reader says it on the window
//! and Alt+Tab shows it, so it's core's rather than each host's.

/// What the app calls itself, last in every title.
const APP: &str = "Slate";

/// `Note — Vault — Slate`, `Vault — Slate` or `Slate`, most specific first. A blank part is
/// no part, so a vault that hasn't opened yet doesn't leave a stray dash.
pub fn window_title(note: Option<&str>, vault: Option<&str>) -> String {
    [note, vault]
        .into_iter()
        .flatten()
        .map(str::trim)
        .filter(|part| !part.is_empty())
        .chain(std::iter::once(APP))
        .collect::<Vec<_>>()
        .join(" — ")
}

#[cfg(test)]
mod tests {
    use super::window_title;

    #[test]
    fn a_note_in_a_vault_comes_first() {
        assert_eq!(
            window_title(Some("Why Slate"), Some("demo-vault")),
            "Why Slate — demo-vault — Slate"
        );
    }

    #[test]
    fn a_vault_with_no_note_is_the_vault_and_the_app() {
        assert_eq!(window_title(None, Some("demo-vault")), "demo-vault — Slate");
    }

    #[test]
    fn nothing_open_is_the_app() {
        assert_eq!(window_title(None, None), "Slate");
    }

    #[test]
    fn a_blank_part_is_no_part() {
        assert_eq!(window_title(Some("  "), Some("")), "Slate");
        assert_eq!(
            window_title(Some(""), Some("demo-vault")),
            "demo-vault — Slate"
        );
        assert_eq!(
            window_title(Some(" Why Slate "), Some(" demo-vault ")),
            "Why Slate — demo-vault — Slate"
        );
    }
}
