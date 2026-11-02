import * as React from 'react';
export interface SpinButtonProps {
  className?: string;
  style?: React.CSSProperties;
  style2?: "outline (default)" | "filled darker" | "filled lighter" | "underline" | "outline";
  /** Alias of style2 (preferred). */
  appearance?: "outline" | "filled darker" | "filled lighter" | "underline";
  state?: "rest" | "hover" | "pressed" | "selected" | "error" | "disabled" | "read only";
  size?: "medium (default)" | "sm" | "medium" | "small";
  /** Swappable nested instance; defaults to the design's. */
  icon2?: React.ReactNode;
}
export declare const SpinButton: React.FC<SpinButtonProps>;
export default SpinButton;
