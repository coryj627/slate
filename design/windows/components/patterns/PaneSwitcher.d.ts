import * as React from 'react';
export interface PaneSwitcherItem {
  id: string;
  label: string;
  /** Segoe Fluent Icons code point (e.g. "E71B") or a React node. */
  icon: string | React.ReactNode;
  /** Keyboard chord shown in the tooltip, e.g. "Ctrl+Alt+I". */
  shortcut?: string;
}
/**
 * Icon strip that switches a pane between views, with tooltips, a selected indicator and a "More panels" overflow menu.
 * Slate addition (not in the Fluent kit).
 */
export interface PaneSwitcherProps {
  items: PaneSwitcherItem[];
  /** id of the open view. */
  value?: string;
  onChange?: (id: string) => void;
  /** Slots including "More panels". Default 6. */
  maxVisible?: number;
  orientation?: 'horizontal' | 'vertical';
  /** Accessible name of the tab list. Default "Panels". */
  label?: string;
  moreLabel?: string;
  className?: string;
  style?: React.CSSProperties;
}
export declare function PaneSwitcher(props: PaneSwitcherProps): JSX.Element;
