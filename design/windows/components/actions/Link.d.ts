import * as React from 'react';
export interface LinkProps {
  className?: string;
  style?: React.CSSProperties;
  style2?: "default" | "subtle" | "overbrand";
  /** Alias of style2 (preferred). */
  appearance?: "default" | "subtle" | "overbrand";
  state?: "rest/visited" | "hover" | "pressed" | "focused" | "disabled";
  showIcon?: boolean;
  inlineStyle?: boolean;
  text?: string;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
}
export declare const Link: React.FC<LinkProps>;
export default Link;
