import * as React from 'react';
export interface PauseCircleProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "12" | "16" | "20" | "24" | "32" | "48";
  theme?: "regular" | "filled";
}
export declare const PauseCircle: React.FC<PauseCircleProps>;
export default PauseCircle;
