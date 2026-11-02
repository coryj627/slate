import * as React from 'react';
export interface SwatchProps {
  className?: string;
  style?: React.CSSProperties;
  state?: "rest" | "hover" | "pressed" | "disabled" | "empty" | "transparent";
  size?: "extrasmall" | "sm" | "medium (default)" | "lg" | "extra small" | "small" | "medium" | "large";
  style2?: "color" | "icon" | "image";
  /** Alias of style2 (preferred). */
  appearance?: "color" | "icon" | "image";
  focus?: boolean;
  selected?: boolean;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
}
export declare const Swatch: React.FC<SwatchProps>;
export default Swatch;
