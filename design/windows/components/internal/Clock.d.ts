import * as React from 'react';
export interface ClockProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "12" | "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled" | "light";
}
export declare const Clock: React.FC<ClockProps>;
export default Clock;
