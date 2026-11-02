import * as React from 'react';
export interface MenuSectionProps {
  className?: string;
  style?: React.CSSProperties;
  divider?: boolean;
  state?: "rest" | "disabled";
  primaryString?: string;
}
export declare const MenuSection: React.FC<MenuSectionProps>;
export default MenuSection;
