import * as React from 'react';
export interface SelectionSmallProps {
  className?: string;
  style?: React.CSSProperties;
  type?: "checkbox" | "checkmark" | "radio";
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
}
export declare const SelectionSmall: React.FC<SelectionSmallProps>;
export default SelectionSmall;
