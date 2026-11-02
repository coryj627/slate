import * as React from 'react';
export interface FigmaProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "32";
  theme?: "color" | "regular";
}
export declare const Figma: React.FC<FigmaProps>;
export default Figma;
