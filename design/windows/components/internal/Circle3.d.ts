import * as React from 'react';
export interface Circle3Props {
  className?: string;
  style?: React.CSSProperties;
  size?: "12" | "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled";
}
export declare const Circle3: React.FC<Circle3Props>;
export default Circle3;
