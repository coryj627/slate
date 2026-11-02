import * as React from 'react';
export interface DrawerFooterProps {
  className?: string;
  style?: React.CSSProperties;
  primaryButton?: boolean;
  moreButton?: boolean;
  divider?: boolean;
  secondaryButton?: boolean;
  /** Button labels (Slate additions). */
  primaryLabel?: string;
  secondaryLabel?: string;
}
export declare const DrawerFooter: React.FC<DrawerFooterProps>;
export default DrawerFooter;
