import * as React from 'react';
export interface InfoButtonProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "sm" | "md" | "lg" | "small" | "medium" | "large";
  state?: "rest" | "hover" | "pressed" | "selected (popover open)" | "focus" | "selected";
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
}
export declare const InfoButton: React.FC<InfoButtonProps>;
export default InfoButton;
