import * as React from 'react';
export interface ImageCircle2Props {
  className?: string;
  style?: React.CSSProperties;
  size?: "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled";
}
export declare const ImageCircle2: React.FC<ImageCircle2Props>;
export default ImageCircle2;
