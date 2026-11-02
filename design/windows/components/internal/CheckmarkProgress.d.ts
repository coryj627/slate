import * as React from 'react';
export interface CheckmarkProgressProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "512" | "256" | "192" | "384" | "128" | "64" | "52";
  theme?: "light" | "dark";
}
export declare const CheckmarkProgress: React.FC<CheckmarkProgressProps>;
export default CheckmarkProgress;
