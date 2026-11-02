import * as React from 'react';
export interface Circle2Props {
  className?: string;
  style?: React.CSSProperties;
  size?: "12" | "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled";
}
export declare const Circle2: React.FC<Circle2Props>;
export default Circle2;
