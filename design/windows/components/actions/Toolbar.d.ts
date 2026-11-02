import * as React from 'react';
export interface ToolbarProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "md" | "lg" | "sm" | "medium" | "large" | "small";
  type?: "static" | "contextual (floating)" | "contextual";
  endContent?: boolean;
  endSearch?: boolean;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon2?: React.ReactNode;
}
export declare const Toolbar: React.FC<ToolbarProps>;
export default Toolbar;
