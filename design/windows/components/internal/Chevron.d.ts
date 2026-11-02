import * as React from 'react';
export interface ChevronProps {
  className?: string;
  style?: React.CSSProperties;
  direction?: "down" | "up" | "left" | "right";
  size?: "12" | "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled";
}
export declare const Chevron: React.FC<ChevronProps>;
export default Chevron;
