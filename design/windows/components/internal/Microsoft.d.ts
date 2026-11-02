import * as React from 'react';
export interface MicrosoftProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "12" | "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "color" | "filled";
  lowDensityScreen?: boolean;
}
export declare const Microsoft: React.FC<MicrosoftProps>;
export default Microsoft;
