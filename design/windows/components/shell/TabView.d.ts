import * as React from 'react';
export interface TabViewTab { id: string; label: string; /** Segoe Fluent Icons code point, e.g. "E8A5". */ icon?: string; }
/** Tab strip from the Windows UI kit tab view: 32 px tabs, 8 px top radius, close buttons, optional add button. */
export interface TabViewProps {
  tabs?: TabViewTab[];
  /** id of the open tab. */
  value?: string;
  onChange?: (id: string) => void;
  onClose?: (id: string) => void;
  /** Shows the add-tab button. */
  onAdd?: () => void;
  closable?: boolean;
  /** Accessible name of the tab list. */
  label?: string;
  addLabel?: string;
  closeLabel?: string;
  /** Content at the end of the strip (e.g. a mode switch). */
  trailing?: React.ReactNode;
  className?: string;
  style?: React.CSSProperties;
}
export declare function TabView(props: TabViewProps): JSX.Element;
