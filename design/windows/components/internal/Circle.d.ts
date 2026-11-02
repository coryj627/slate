import * as React from 'react';
export interface CircleProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "24" | "20" | "16" | "12" | "32" | "48" | "28";
  theme?: "regular" | "filled";
}
export declare const Circle: React.FC<CircleProps>;
export default Circle;
