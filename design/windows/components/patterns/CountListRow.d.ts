import * as React from 'react';
/**
 * One row of a counted list (tags, properties, backlinks): chevron, icon, label, right-aligned count.
 * Slate addition (not in the Fluent kit), sized like the Fluent TreeItem.
 */
export interface CountListRowProps {
  label: string;
  count?: number | string;
  /** Segoe Fluent Icons code point (e.g. "E8EC" tag) or a React node. */
  icon?: string | React.ReactNode;
  /** Nesting level; each level indents 20 px. */
  depth?: number;
  /** Pass to show the chevron (true = open). Omit for rows that can't expand. */
  expanded?: boolean;
  onToggle?: (expanded: boolean) => void;
  /** In a tree with some expandable rows, set on the others so labels line up. */
  nested?: boolean;
  selected?: boolean;
  onClick?: () => void;
  /** 32 px medium (default) or 24 px small. */
  size?: 'small' | 'medium';
  /** Read after the count by screen readers, e.g. "notes". */
  countLabel?: string;
  /** Default "treeitem"; use "option" in a listbox or "button" in a plain list. */
  role?: string;
  tabIndex?: number;
  disabled?: boolean;
  className?: string;
  style?: React.CSSProperties;
}
export declare function CountListRow(props: CountListRowProps): JSX.Element;
