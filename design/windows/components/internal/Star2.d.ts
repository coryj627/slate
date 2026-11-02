import * as React from 'react';
export interface Star2Props {
  className?: string;
  style?: React.CSSProperties;
  size?: "12" | "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled";
}
export declare const Star2: React.FC<Star2Props>;
export default Star2;
