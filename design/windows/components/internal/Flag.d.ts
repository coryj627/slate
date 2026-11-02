import * as React from 'react';
export interface FlagProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled";
}
export declare const Flag: React.FC<FlagProps>;
export default Flag;
