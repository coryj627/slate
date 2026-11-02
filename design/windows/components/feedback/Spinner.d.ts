import * as React from 'react';
export interface SpinnerProps {
  className?: string;
  style?: React.CSSProperties;
  layout?: "label after" | "label before" | "label above" | "label below";
  style2?: "primary (default)" | "subtle" | "primary";
  /** Alias of style2 (preferred). */
  appearance?: "primary" | "subtle";
  size?: "extra-tiny" | "tiny" | "extra-small" | "sm" | "md" | "lg" | "extra-large" | "huge" | "extra tiny" | "extra small" | "small" | "medium" | "large" | "extra large";
  /** Text content; defaults to "Saving ...". */
  text1?: string;
}
export declare const Spinner: React.FC<SpinnerProps>;
export default Spinner;
