import * as React from 'react';
export interface MenuSplitItemProps {
  className?: string;
  style?: React.CSSProperties;
  checkmark?: boolean;
  selected?: boolean;
  sectionHeader?: "false";
  state?: "rest" | "rest (checked only)" | "hover" | "pressed" | "selected" | "disabled" | "disabled (checked only)";
  endContent?: boolean;
  endContentString?: string;
  focus?: boolean;
  icon?: boolean;
  pxRegular20?: React.ReactNode;
  pxFilled20?: React.ReactNode;
  secondaryText?: boolean;
  primaryString?: string;
  secondaryString?: string;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon2?: React.ReactNode;
}
export declare const MenuSplitItem: React.FC<MenuSplitItemProps>;
export default MenuSplitItem;
