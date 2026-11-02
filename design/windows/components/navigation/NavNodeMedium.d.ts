import * as React from 'react';
export interface NavNodeMediumProps {
  className?: string;
  style?: React.CSSProperties;
  subItem?: React.ReactNode;
  content?: string;
  group?: boolean;
  selected?: boolean;
  state?: "hover" | "pressed" | "rest";
  open?: boolean;
  secondaryActions?: boolean;
  iconBeforeRegular?: React.ReactNode;
  iconBeforeFilled?: React.ReactNode;
  focus?: boolean;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
}
export declare const NavNodeMedium: React.FC<NavNodeMediumProps>;
export default NavNodeMedium;
