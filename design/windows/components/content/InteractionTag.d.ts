import * as React from 'react';
export interface InteractionTagProps {
  className?: string;
  style?: React.CSSProperties;
  style2?: "filled (default)" | "outline" | "brand" | "filled";
  /** Alias of style2 (preferred). */
  appearance?: "filled" | "outline" | "brand";
  size?: "extra small" | "sm" | "medium (default)" | "small" | "medium";
  layout?: "1 line (default)" | "2 line (medium only)" | "1 line" | "2 line";
  state?: "rest" | "hover" | "pressed" | "disabled";
  selected?: boolean;
  dismiss?: boolean;
  avatar?: boolean;
  focus?: boolean;
  icon?: boolean;
  regular12px?: React.ReactNode;
  regular20px?: React.ReactNode;
  regular16px?: React.ReactNode;
  filled20px?: React.ReactNode;
  secondaryString?: string;
  filled16px?: React.ReactNode;
  filled12px?: React.ReactNode;
  primaryString?: string;
  /** Swappable nested instance; defaults to the design's. */
  icon2?: React.ReactNode;
}
export declare const InteractionTag: React.FC<InteractionTagProps>;
export default InteractionTag;
