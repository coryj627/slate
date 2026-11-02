import * as React from 'react';
export interface SettingsCategory {
  id: string;
  label: string;
  /** Segoe Fluent Icons code point (e.g. "E70F") or a React node. */
  icon?: string | React.ReactNode;
}
/**
 * Settings window body: category list (vertical tabs, optional search) and the open category's SettingRows.
 * Slate addition (not in the Fluent kit).
 */
export interface SettingsLayoutProps {
  categories: SettingsCategory[];
  /** id of the open category. */
  value?: string;
  onChange?: (id: string) => void;
  /** Page title; defaults to the category label. */
  title?: string;
  description?: string;
  /** Pass onQueryChange to show the search field above the categories. */
  query?: string;
  onQueryChange?: (q: string) => void;
  searchPlaceholder?: string;
  navLabel?: string;
  /** SettingRows and group headings. */
  children?: React.ReactNode;
  className?: string;
  style?: React.CSSProperties;
}
export declare function SettingsLayout(props: SettingsLayoutProps): JSX.Element;
