import * as React from 'react';
export interface Checkmark3Props {
  className?: string;
  style?: React.CSSProperties;
  size?: "12" | "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled" | "light";
}
export declare const Checkmark3: React.FC<Checkmark3Props>;
export default Checkmark3;
