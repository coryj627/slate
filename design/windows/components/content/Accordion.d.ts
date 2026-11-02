import * as React from 'react';
export interface AccordionProps {
  className?: string;
  style?: React.CSSProperties;
  icon?: boolean;
  focused?: boolean;
  iconBefore20pxRegular?: React.ReactNode;
  text?: string;
  contentSwap?: React.ReactNode;
  size?: "md" | "lg" | "sm" | "extra large" | "medium" | "large" | "small";
  state?: "rest" | "disabled";
  expanded?: boolean;
  chevron?: "before" | "after";
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
}
export declare const Accordion: React.FC<AccordionProps>;
export default Accordion;
