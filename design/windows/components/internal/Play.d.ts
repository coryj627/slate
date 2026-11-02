import * as React from 'react';
export interface PlayProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "12" | "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled";
}
export declare const Play: React.FC<PlayProps>;
export default Play;
