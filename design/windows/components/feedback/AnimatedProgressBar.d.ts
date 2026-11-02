import * as React from 'react';
export interface AnimatedProgressBarProps {
  className?: string;
  style?: React.CSSProperties;
  style2?: "indeterminate" | "determinate";
  /** Alias of style2 (preferred). */
  appearance?: "indeterminate" | "determinate";
  size?: "medium (default)" | "lg" | "medium" | "large";
  state?: "default" | "success" | "error" | "warning";
  status?: "not started" | "in progress" | "complete" | "after" | "before" | "gradient after" | "gradient before" | "99% complete";
}
export declare const AnimatedProgressBar: React.FC<AnimatedProgressBarProps>;
export default AnimatedProgressBar;
