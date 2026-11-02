import * as React from 'react';
/** A sort entry: `{ value, label }` is one choice (checked when it equals sortValue); `{ label, checked, onSelect }` is a toggle; `{ separator: true }` divides groups. */
export interface PanelToolbarSortOption {
  value?: string;
  label?: string;
  checked?: boolean;
  onSelect?: () => void;
  separator?: boolean;
  disabled?: boolean;
}
/**
 * Tool row at the top of a panel: title + count, then search, sort and collapse/expand all.
 * Slate addition (not in the Fluent kit).
 */
export interface PanelToolbarProps {
  title?: React.ReactNode;
  /** Shown after the title in tertiary text. */
  count?: number | string;
  /** Extra actions (icon buttons), placed before the built-in ones. */
  children?: React.ReactNode;
  /** Show the search button. Default true. */
  search?: boolean;
  query?: string;
  onQueryChange?: (q: string) => void;
  /** Controlled search state; omit to let the row manage it. */
  searchOpen?: boolean;
  onSearchOpenChange?: (open: boolean) => void;
  /** Default "Search <title>". */
  searchPlaceholder?: string;
  sortOptions?: PanelToolbarSortOption[];
  sortValue?: string;
  onSortChange?: (value: string) => void;
  /** Accessible name and tooltip of the sort button. Default "Sort". */
  sortLabel?: string;
  /** Pass to show the collapse/expand all toggle. */
  expanded?: boolean;
  onExpandedChange?: (expanded: boolean) => void;
  className?: string;
  style?: React.CSSProperties;
}
export declare function PanelToolbar(props: PanelToolbarProps): JSX.Element;
