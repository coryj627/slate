import * as React from 'react';
export interface SelectionProps {
  className?: string;
  style?: React.CSSProperties;
  type?: "checkbox" | "checkmark" | "radio";
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
}
export declare const Selection: React.FC<SelectionProps>;
export default Selection;
