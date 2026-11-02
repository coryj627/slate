import * as React from 'react';
export interface VerticalTabProps {
  className?: string;
  style?: React.CSSProperties;
  iconSelected?: boolean;
  iconUnselected?: boolean;
  selected?: boolean;
  style2?: "transparent (default)" | "subtle" | "filled circular" | "subtle circular" | "transparent";
  /** Alias of style2 (preferred). */
  appearance?: "transparent" | "subtle" | "filled circular" | "subtle circular";
  size?: "medium (default)" | "sm" | "medium" | "small";
  state?: "rest" | "hover" | "pressed" | "focus" | "disabled";
  /** Text content; defaults to "Text". */
  text1?: string;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
}
export declare const VerticalTab: React.FC<VerticalTabProps>;
export default VerticalTab;
