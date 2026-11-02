import * as React from 'react';
export interface RadioGroupProps {
  className?: string;
  style?: React.CSSProperties;
  direction?: "horizontal stacked" | "vertical" | "horizontal";
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon2?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon3?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon4?: React.ReactNode;
}
export declare const RadioGroup: React.FC<RadioGroupProps>;
export default RadioGroup;
