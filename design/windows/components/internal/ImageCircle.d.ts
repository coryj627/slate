import * as React from 'react';
export interface ImageCircleProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled";
}
export declare const ImageCircle: React.FC<ImageCircleProps>;
export default ImageCircle;
