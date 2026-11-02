import * as React from 'react';
export interface NavSubItemProps {
  className?: string;
  style?: React.CSSProperties;
  subItemContent?: string;
  focus?: boolean;
  state?: "hover" | "pressed" | "rest";
  selected?: boolean;
  secondaryActions?: boolean;
}
export declare const NavSubItem: React.FC<NavSubItemProps>;
export default NavSubItem;
