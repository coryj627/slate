import * as React from 'react';
export interface ArrowSwapProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "16" | "20" | "24" | "28";
  theme?: "regular" | "filled";
}
export declare const ArrowSwap: React.FC<ArrowSwapProps>;
export default ArrowSwap;
