import * as React from 'react';
export interface NavNodeSmallProps {
  className?: string;
  style?: React.CSSProperties;
  group?: boolean;
  selected?: boolean;
  state?: "rest" | "hover" | "pressed";
  open?: boolean;
  secondaryActions?: boolean;
  focus?: boolean;
  content?: string;
  iconBeforeRegular?: React.ReactNode;
  iconBeforeFilled?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
}
export declare const NavNodeSmall: React.FC<NavNodeSmallProps>;
export default NavNodeSmall;
