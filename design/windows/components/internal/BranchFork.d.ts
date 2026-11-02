import * as React from 'react';
export interface BranchForkProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "16" | "20" | "24" | "32";
  theme?: "regular" | "filled";
}
export declare const BranchFork: React.FC<BranchForkProps>;
export default BranchFork;
