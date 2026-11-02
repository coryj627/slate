import * as React from 'react';
/**
 * One setting: icon, label, description and its control (Switch, dropdown, SpinButton…).
 * Slate addition (not in the Fluent kit). Use inside SettingsLayout.
 */
export interface SettingRowProps {
  label: React.ReactNode;
  description?: React.ReactNode;
  /** Segoe Fluent Icons code point or a React node, 20 px. */
  icon?: string | React.ReactNode;
  /** The control. */
  children?: React.ReactNode;
  /** id of a native control, to make the label a <label for>. */
  htmlFor?: string;
  /** Dims label and description to the disabled text role. Disable the control too. */
  disabled?: boolean;
  /** Puts the control under the text, for wide controls. */
  stacked?: boolean;
  className?: string;
  style?: React.CSSProperties;
}
export declare function SettingRow(props: SettingRowProps): JSX.Element;
