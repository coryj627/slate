import * as React from 'react';
export interface FluentProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "20" | "24" | "32" | "48";
  theme?: "regular" | "filled";
}
export declare const Fluent: React.FC<FluentProps>;
export default Fluent;
