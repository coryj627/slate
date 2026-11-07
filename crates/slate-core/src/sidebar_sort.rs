// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! The Files sidebar's sort orders as a reader meets them (plan 46 DS-17, DSD12). The
//! order moved from a combo box, which says its value on focus, to a Sort and group
//! menu button, which says only its name; so the button's name carries the value, and
//! a name is core's, with the order labels it's built from.

/// One order the Files sidebar can sort by.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum SidebarSortOrder {
    NameAscending,
    NameDescending,
    ModifiedNewest,
    ModifiedOldest,
    CreatedNewest,
    CreatedOldest,
}

/// Every order, in the order the menu lists them.
pub const SIDEBAR_SORT_ORDERS: [SidebarSortOrder; 6] = [
    SidebarSortOrder::NameAscending,
    SidebarSortOrder::NameDescending,
    SidebarSortOrder::ModifiedNewest,
    SidebarSortOrder::ModifiedOldest,
    SidebarSortOrder::CreatedNewest,
    SidebarSortOrder::CreatedOldest,
];

/// What the button is called, first in every name.
const SORT_AND_GROUP: &str = "Sort and group";

/// An order's label, as its menu row shows and says it: `Name (A to Z)`.
pub fn sidebar_sort_order_label(order: SidebarSortOrder) -> &'static str {
    match order {
        SidebarSortOrder::NameAscending => "Name (A to Z)",
        SidebarSortOrder::NameDescending => "Name (Z to A)",
        SidebarSortOrder::ModifiedNewest => "Modified (newest)",
        SidebarSortOrder::ModifiedOldest => "Modified (oldest)",
        SidebarSortOrder::CreatedNewest => "Created (newest)",
        SidebarSortOrder::CreatedOldest => "Created (oldest)",
    }
}

/// The Sort and group button's name, with the value a combo box used to say on focus:
/// `Sort and group, Name (A to Z)`, and `, grouped by date` when the dates group.
pub fn sidebar_sort_button_name(order: SidebarSortOrder, grouped_by_date: bool) -> String {
    let label = sidebar_sort_order_label(order);
    if grouped_by_date {
        format!("{SORT_AND_GROUP}, {label}, grouped by date")
    } else {
        format!("{SORT_AND_GROUP}, {label}")
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn every_order_has_its_own_label() {
        let labels: Vec<&str> = SIDEBAR_SORT_ORDERS
            .iter()
            .map(|order| sidebar_sort_order_label(*order))
            .collect();
        assert_eq!(
            labels,
            [
                "Name (A to Z)",
                "Name (Z to A)",
                "Modified (newest)",
                "Modified (oldest)",
                "Created (newest)",
                "Created (oldest)",
            ]
        );
    }

    #[test]
    fn the_buttons_name_says_the_order() {
        assert_eq!(
            sidebar_sort_button_name(SidebarSortOrder::NameAscending, false),
            "Sort and group, Name (A to Z)"
        );
        assert_eq!(
            sidebar_sort_button_name(SidebarSortOrder::CreatedOldest, false),
            "Sort and group, Created (oldest)"
        );
    }

    #[test]
    fn grouping_by_date_is_in_the_name() {
        assert_eq!(
            sidebar_sort_button_name(SidebarSortOrder::ModifiedNewest, true),
            "Sort and group, Modified (newest), grouped by date"
        );
    }
}
