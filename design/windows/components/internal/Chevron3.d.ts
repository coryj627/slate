import * as React from 'react';
export interface Chevron3Props {
  className?: string;
  style?: React.CSSProperties;
  direction?: "down" | "up" | "left" | "right";
  size?: "12" | "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled";
}
export declare const Chevron3: React.FC<Chevron3Props>;
export default Chevron3;
