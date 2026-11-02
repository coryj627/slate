import * as React from 'react';
export interface BinderTriangleProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "16" | "20" | "24" | "32";
  theme?: "regular" | "filled";
}
export declare const BinderTriangle: React.FC<BinderTriangleProps>;
export default BinderTriangle;
