import * as React from 'react';
export interface StaticProgressBarProps {
  className?: string;
  style?: React.CSSProperties;
  style2?: "indeterminate" | "determinate";
  /** Alias of style2 (preferred). */
  appearance?: "indeterminate" | "determinate";
  state?: "default" | "success" | "error" | "warning";
  size?: "medium (default)" | "lg" | "medium" | "large";
}
export declare const StaticProgressBar: React.FC<StaticProgressBarProps>;
export default StaticProgressBar;
