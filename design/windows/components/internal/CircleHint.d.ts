import * as React from 'react';
export interface CircleHintProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled";
}
export declare const CircleHint: React.FC<CircleHintProps>;
export default CircleHint;
