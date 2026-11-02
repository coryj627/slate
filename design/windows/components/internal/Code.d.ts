import * as React from 'react';
export interface CodeProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "16" | "20" | "24";
  theme?: "regular" | "filled";
}
export declare const Code: React.FC<CodeProps>;
export default Code;
