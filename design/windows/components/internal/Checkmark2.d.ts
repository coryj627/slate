import * as React from 'react';
export interface Checkmark2Props {
  className?: string;
  style?: React.CSSProperties;
  size?: "12" | "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled" | "light";
}
export declare const Checkmark2: React.FC<Checkmark2Props>;
export default Checkmark2;
