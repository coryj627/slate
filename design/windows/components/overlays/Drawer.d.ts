import * as React from 'react';
export interface DrawerProps {
  className?: string;
  style?: React.CSSProperties;
  type?: "overlay" | "inline";
  /** Header title and footer button labels (Slate additions). */
  title?: string;
  primaryLabel?: string;
  secondaryLabel?: string;
}
export declare const Drawer: React.FC<DrawerProps>;
export default Drawer;
