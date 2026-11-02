import * as React from 'react';
export interface ErrorCircleProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "12" | "16" | "20" | "24" | "48";
  theme?: "regular" | "filled";
}
export declare const ErrorCircle: React.FC<ErrorCircleProps>;
export default ErrorCircle;
