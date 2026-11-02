import * as React from 'react';
export interface Chevron2Props {
  className?: string;
  style?: React.CSSProperties;
  direction?: "down" | "up" | "left" | "right";
  size?: "12" | "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled";
}
export declare const Chevron2: React.FC<Chevron2Props>;
export default Chevron2;
