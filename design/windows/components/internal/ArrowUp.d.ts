import * as React from 'react';
export interface ArrowUpProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "12" | "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "filled" | "regular";
}
export declare const ArrowUp: React.FC<ArrowUpProps>;
export default ArrowUp;
