import * as React from 'react';
/** Check box rebuilt from the Windows UI kit: 20 px box in a 32 px row. */
export interface CheckboxProps {
  className?: string;
  style?: React.CSSProperties;
  style2?: "standard" | "circular";
  /** Alias of style2 (preferred). */
  appearance?: "standard" | "circular";
  /** Design state shown; omit to follow the live checked value. */
  status?: "unchecked" | "checked" | "indeterminate";
  /** Live value. "mixed" shows the indeterminate dash. */
  checked?: boolean | "mixed";
  onChange?: (checked: boolean) => void;
  /** Forces a visual state (cards and specs). Omit for live hover, press and keyboard focus. */
  state?: "rest" | "hover" | "pressed" | "disabled" | "focus";
  disabled?: boolean;
  /** Show the label (default true). With false, labelText becomes the accessible name. */
  label?: boolean;
  labelText?: string;
  children?: React.ReactNode;
  name?: string;
  id?: string;
  value?: string;
}
export declare const Checkbox: React.FC<CheckboxProps>;
export default Checkbox;
