import * as React from 'react';
export interface StarProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "12" | "16" | "20" | "24" | "28" | "48";
  theme?: "regular" | "filled";
}
export declare const Star: React.FC<StarProps>;
export default Star;
