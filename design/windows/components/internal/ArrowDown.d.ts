import * as React from 'react';
export interface ArrowDownProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "12" | "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "filled" | "regular" | "light";
}
export declare const ArrowDown: React.FC<ArrowDownProps>;
export default ArrowDown;
