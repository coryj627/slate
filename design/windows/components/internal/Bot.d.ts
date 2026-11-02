import * as React from 'react';
export interface BotProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled";
}
export declare const Bot: React.FC<BotProps>;
export default Bot;
