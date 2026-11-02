import * as React from 'react';
export interface CopilotProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "color" | "regular" | "filled" | "light";
}
export declare const Copilot: React.FC<CopilotProps>;
export default Copilot;
