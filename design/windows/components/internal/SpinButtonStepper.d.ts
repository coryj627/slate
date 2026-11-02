import * as React from 'react';
export interface SpinButtonStepperProps {
  className?: string;
  style?: React.CSSProperties;
  style2?: "default" | "darker";
  /** Alias of style2 (preferred). */
  appearance?: "default" | "darker";
  state?: "rest" | "hover" | "pressed" | "disabled";
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
}
export declare const SpinButtonStepper: React.FC<SpinButtonStepperProps>;
export default SpinButtonStepper;
