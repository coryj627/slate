import * as React from 'react';
/** A ⋯ menu entry: `{ label, shortcut?, onSelect?, disabled?, checked? }` or `{ separator: true }`. */
export interface NoteHeaderMenuItem {
  label?: string;
  shortcut?: string;
  onSelect?: () => void;
  disabled?: boolean;
  /** Makes the entry checkable; every row then keeps the check column. */
  checked?: boolean;
  /** Segoe Fluent Icons code point; if any entry has one, all rows keep the icon column. */
  icon?: string;
  separator?: boolean;
}
/**
 * Header bar above a note: back/forward, folder breadcrumb, Source/Reading switch, ⋯ menu.
 * Slate addition (not in the Fluent kit).
 */
export interface NoteHeaderProps {
  /** Folders then the note, e.g. ["learning", "Linear algebra lecture 3"]. */
  path?: string[];
  /** Called with the folder's index when a breadcrumb folder is clicked. */
  onNavigate?: (index: number) => void;
  canGoBack?: boolean;
  canGoForward?: boolean;
  onBack?: () => void;
  onForward?: () => void;
  mode?: 'source' | 'reading';
  onModeChange?: (mode: 'source' | 'reading') => void;
  /** Icon-only view switch (with tooltips). Omit to switch automatically below 520 px. */
  compact?: boolean;
  moreItems?: NoteHeaderMenuItem[];
  moreLabel?: string;
  /** Chords shown in tooltips, e.g. { mode: "Ctrl+Shift+E" }. */
  shortcuts?: { back?: string; forward?: string; mode?: string; more?: string };
  className?: string;
  style?: React.CSSProperties;
}
export declare function NoteHeader(props: NoteHeaderProps): JSX.Element;
