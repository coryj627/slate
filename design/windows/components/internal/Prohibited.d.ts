import * as React from 'react';
export interface ProhibitedProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "12" | "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled";
}
export declare const Prohibited: React.FC<ProhibitedProps>;
export default Prohibited;
