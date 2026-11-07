// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! The name of the right-pane rail's More panels button (plan 46 DS-16, DSD8). Twelve
//! panels live in its menu, and when one of them is open the button names it, so a
//! reader can tell what's open from the button. NVDA doesn't read a button's item
//! status, so it's in the name, and a name is core's.

/// What the button is called, first in every name.
const MORE_PANELS: &str = "More panels";

/// `More panels`, or `More panels, Tasks open` when the open panel is one of the
/// menu's. A blank title is no title.
pub fn more_panels_name(open: Option<&str>) -> String {
    match open.map(str::trim).filter(|title| !title.is_empty()) {
        Some(title) => format!("{MORE_PANELS}, {title} open"),
        None => MORE_PANELS.to_owned(),
    }
}

#[cfg(test)]
mod tests {
    use super::more_panels_name;

    #[test]
    fn nothing_open_in_the_menu_is_the_buttons_name() {
        assert_eq!(more_panels_name(None), "More panels");
    }

    #[test]
    fn a_panel_open_from_the_menu_is_named() {
        assert_eq!(more_panels_name(Some("Tasks")), "More panels, Tasks open");
        assert_eq!(
            more_panels_name(Some("Tasks Review")),
            "More panels, Tasks Review open"
        );
    }

    #[test]
    fn a_blank_title_is_no_title() {
        assert_eq!(more_panels_name(Some("  ")), "More panels");
        assert_eq!(
            more_panels_name(Some(" History ")),
            "More panels, History open"
        );
    }
}
