import * as React from 'react';
export interface OverlayDrawerProps {
  className?: string;
  style?: React.CSSProperties;
  footer?: boolean;
  size?: "small (default)" | "md" | "lg" | "small" | "medium" | "large";
  contentSwap?: React.ReactNode;
}
export declare const OverlayDrawer: React.FC<OverlayDrawerProps>;
export default OverlayDrawer;
