import * as React from 'react';
export interface RadioBaseProps {
  className?: string;
  style?: React.CSSProperties;
  subtextString?: string;
  subtext?: boolean;
  layout?: "icon only" | "icon+label after" | "icon+label below";
  state?: "default" | "checked" | "unchecked";
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
}
export declare const RadioBase: React.FC<RadioBaseProps>;
export default RadioBase;
