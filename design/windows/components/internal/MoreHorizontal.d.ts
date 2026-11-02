import * as React from 'react';
export interface MoreHorizontalProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled";
}
export declare const MoreHorizontal: React.FC<MoreHorizontalProps>;
export default MoreHorizontal;
