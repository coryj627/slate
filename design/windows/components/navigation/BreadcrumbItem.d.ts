import * as React from 'react';
export interface BreadcrumbItemProps {
  className?: string;
  /** Item text; defaults to "Text". */
  label?: string;
  style?: React.CSSProperties;
  size?: "lg" | "md" | "sm" | "large" | "medium" | "small";
  type?: "button" | "overflow";
  states?: "rest" | "hover" | "pressed" | "focused" | "disabled" | "current";
  icon?: "on" | "off" | boolean;
  dividerIcon?: React.ReactNode;
  divider?: boolean;
  /** Swappable nested instance; defaults to the design's. */
  icon2?: React.ReactNode;
}
export declare const BreadcrumbItem: React.FC<BreadcrumbItemProps>;
export default BreadcrumbItem;
