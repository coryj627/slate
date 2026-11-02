import * as React from 'react';
export interface SkeletonProps {
  className?: string;
  style?: React.CSSProperties;
  shape?: "rectangle (default)" | "circle" | "rectangle";
  animated?: boolean;
  frame?: "1" | "2" | "3" | "4" | "5";
}
export declare const Skeleton: React.FC<SkeletonProps>;
export default Skeleton;
