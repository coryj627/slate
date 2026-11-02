import * as React from 'react';
export interface WrenchScrewdriverProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "20" | "24" | "32";
  theme?: "regular" | "filled" | "light";
}
export declare const WrenchScrewdriver: React.FC<WrenchScrewdriverProps>;
export default WrenchScrewdriver;
