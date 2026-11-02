import * as React from 'react';
export interface MenuItemProps {
  className?: string;
  style?: React.CSSProperties;
  checkmark?: boolean;
  selected?: boolean;
  endContent?: boolean;
  endContentString?: string;
  chevron?: boolean;
  switch?: boolean;
  focus?: boolean;
  icon?: boolean;
  pxRegular20?: React.ReactNode;
  state?: "rest" | "rest (checked only)" | "hover" | "pressed" | "selected" | "disabled" | "disabled (checked only)";
  pxFilled20?: React.ReactNode;
  secondaryText?: boolean;
  primaryString?: string;
  secondaryString?: string;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon2?: React.ReactNode;
}
export declare const MenuItem: React.FC<MenuItemProps>;
export default MenuItem;
