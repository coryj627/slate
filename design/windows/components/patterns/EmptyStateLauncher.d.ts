import * as React from 'react';
export interface EmptyStateLauncherAction {
  label: string;
  /** Chord shown at the end of the row, e.g. "Ctrl+O". */
  shortcut?: string;
  /** Segoe Fluent Icons code point or a React node. Give all actions an icon, or none. */
  icon?: string | React.ReactNode;
  onClick?: () => void;
  disabled?: boolean;
}
/**
 * Empty tab or pane: logo, title, a line of help and the primary actions with their shortcuts.
 * Slate addition (not in the Fluent kit).
 */
export interface EmptyStateLauncherProps {
  /** Default "No note open". Pass null to hide. */
  title?: string | null;
  description?: string;
  actions?: EmptyStateLauncherAction[];
  /** Image URL, e.g. assets/logo/slate-gold.png. */
  logo?: string;
  className?: string;
  style?: React.CSSProperties;
}
export declare function EmptyStateLauncher(props: EmptyStateLauncherProps): JSX.Element;
