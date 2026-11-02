import * as React from 'react';
export interface BeakerProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "16" | "20" | "24" | "32";
  theme?: "regular" | "filled";
}
export declare const Beaker: React.FC<BeakerProps>;
export default Beaker;
