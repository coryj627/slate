import * as React from 'react';
export interface CheckmarkProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "12" | "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled" | "light";
}
export declare const Checkmark: React.FC<CheckmarkProps>;
export default Checkmark;
