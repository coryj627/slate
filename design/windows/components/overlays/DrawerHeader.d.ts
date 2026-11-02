import * as React from 'react';
export interface DrawerHeaderProps {
  className?: string;
  style?: React.CSSProperties;
  navigation?: boolean;
  leftActions?: boolean;
  title?: string;
  dismiss?: boolean;
  divider?: boolean;
  rightActions?: boolean;
}
export declare const DrawerHeader: React.FC<DrawerHeaderProps>;
export default DrawerHeader;
