import * as React from 'react';
export interface HorizontalTabProps {
  className?: string;
  style?: React.CSSProperties;
  iconUnselected?: boolean;
  selected?: boolean;
  style2?: "transparent (default)" | "subtle" | "filled circular" | "subtle circular" | "transparent";
  /** Alias of style2 (preferred). */
  appearance?: "transparent" | "subtle" | "filled circular" | "subtle circular";
  size?: "medium (default)" | "sm" | "medium" | "small";
  state?: "rest" | "hover" | "pressed" | "focus" | "disabled";
  iconSelected?: boolean;
  /** Text content; defaults to "Text". */
  text1?: string;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
}
export declare const HorizontalTab: React.FC<HorizontalTabProps>;
export default HorizontalTab;
