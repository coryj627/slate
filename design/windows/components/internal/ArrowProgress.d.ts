import * as React from 'react';
export interface ArrowProgressProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "512" | "256" | "384" | "192" | "128" | "64" | "52";
  theme?: "light" | "dark" | "high contrast ligh" | "high contrast dark";
}
export declare const ArrowProgress: React.FC<ArrowProgressProps>;
export default ArrowProgress;
