import * as React from 'react';
export interface SwitchProps {
  className?: string;
  style?: React.CSSProperties;
  layout?: "switch" | "switch+label after" | "switch+label before" | "switch+label above";
  checked?: boolean;
  state?: "rest" | "hover" | "pressed" | "focus" | "disabled";
  /** Label text for the layouts with a label (the kit hard-coded "Label"). */
  labelText?: string;
}
export declare const Switch: React.FC<SwitchProps>;
export default Switch;
