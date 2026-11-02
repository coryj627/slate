import * as React from 'react';
export interface CheckmarkCircleProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "12" | "16" | "20" | "24" | "32" | "48";
  theme?: "regular" | "filled" | "light";
}
export declare const CheckmarkCircle: React.FC<CheckmarkCircleProps>;
export default CheckmarkCircle;
