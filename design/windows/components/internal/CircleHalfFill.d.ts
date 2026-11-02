import * as React from 'react';
export interface CircleHalfFillProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "24" | "20" | "12" | "16";
  theme?: "regular" | "filled";
}
export declare const CircleHalfFill: React.FC<CircleHalfFillProps>;
export default CircleHalfFill;
