import * as React from 'react';
export interface InlineDrawerProps {
  className?: string;
  style?: React.CSSProperties;
  footer?: boolean;
  contentSwap?: React.ReactNode;
  size?: "small (default)" | "md" | "lg" | "small" | "medium" | "large";
  verticalDivider?: "none" | "left" | "right";
}
export declare const InlineDrawer: React.FC<InlineDrawerProps>;
export default InlineDrawer;
