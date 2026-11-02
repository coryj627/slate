import * as React from 'react';
export interface GlassesProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled";
}
export declare const Glasses: React.FC<GlassesProps>;
export default Glasses;
