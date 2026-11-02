import * as React from 'react';
export interface SubtractCircleProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "28" | "24" | "20" | "32" | "16" | "12" | "48";
  theme?: "regular" | "filled";
}
export declare const SubtractCircle: React.FC<SubtractCircleProps>;
export default SubtractCircle;
