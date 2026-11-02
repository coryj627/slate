import * as React from 'react';
export interface ArrowUploadProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "16" | "20" | "24" | "32";
  theme?: "regular" | "filled";
}
export declare const ArrowUpload: React.FC<ArrowUploadProps>;
export default ArrowUpload;
