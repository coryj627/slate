import * as React from 'react';
export interface ArrowSyncProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "12" | "16" | "20" | "24";
  theme?: "regular" | "filled";
}
export declare const ArrowSync: React.FC<ArrowSyncProps>;
export default ArrowSync;
