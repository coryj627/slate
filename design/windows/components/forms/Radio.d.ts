import * as React from 'react';
/** Radio button rebuilt from the Windows UI kit: 20 px circle in a 32 px row. Group radios with the same name. */
export interface RadioProps {
  className?: string;
  style?: React.CSSProperties;
  layout?: "icon only" | "icon+label after" | "icon+label below";
  checked?: "on" | "off" | boolean;
  onChange?: (checked: true) => void;
  /** Forces a visual state (cards and specs). Omit for live hover, press and keyboard focus. */
  state?: "rest" | "hover" | "pressed" | "disabled" | "focus";
  disabled?: boolean;
  /** Label text. With layout "icon only" it becomes the accessible name. */
  labelText?: string;
  children?: React.ReactNode;
  name?: string;
  id?: string;
  value?: string;
  /** Replaces the circle (rare). */
  icon1?: React.ReactNode;
}
export declare const Radio: React.FC<RadioProps>;
export default Radio;
