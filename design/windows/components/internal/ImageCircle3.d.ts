import * as React from 'react';
export interface ImageCircle3Props {
  className?: string;
  style?: React.CSSProperties;
  size?: "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled";
}
export declare const ImageCircle3: React.FC<ImageCircle3Props>;
export default ImageCircle3;
