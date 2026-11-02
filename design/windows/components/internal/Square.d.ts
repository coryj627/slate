import * as React from 'react';
export interface SquareProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "12" | "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled";
}
export declare const Square: React.FC<SquareProps>;
export default Square;
