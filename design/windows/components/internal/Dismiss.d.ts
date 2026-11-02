import * as React from 'react';
export interface DismissProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "12" | "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled" | "light";
}
export declare const Dismiss: React.FC<DismissProps>;
export default Dismiss;
