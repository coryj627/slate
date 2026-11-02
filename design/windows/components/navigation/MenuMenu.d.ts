import * as React from 'react';
/** Menu flyout surface (Windows UI kit): radius 8, 2 px vertical padding, flyout shadow. Children are MenuItems. */
export interface MenuMenuProps {
  className?: string;
  style?: React.CSSProperties;
  /** With customMenu, renders instance as the body. */
  customMenu?: boolean;
  instance?: React.ReactNode;
  /** MenuItems and separators. Without children, a sample menu renders. */
  children?: React.ReactNode;
}
export declare const MenuMenu: React.FC<MenuMenuProps>;
export default MenuMenu;
