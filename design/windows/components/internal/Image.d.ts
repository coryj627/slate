import * as React from 'react';
export interface ImageProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled";
}
export declare const Image: React.FC<ImageProps>;
export default Image;
