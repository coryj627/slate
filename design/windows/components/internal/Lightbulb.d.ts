import * as React from 'react';
export interface LightbulbProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled" | "light";
}
export declare const Lightbulb: React.FC<LightbulbProps>;
export default Lightbulb;
