import * as React from 'react';
export interface SplitButtonProps {
  className?: string;
  style?: React.CSSProperties;
  label?: string;
  focus?: boolean;
  style2?: "primary" | "secondary (default)" | "outline" | "subtle" | "transparent" | "secondary";
  /** Alias of style2 (preferred). */
  appearance?: "primary" | "secondary" | "outline" | "subtle" | "transparent";
  state?: "rest" | "hover" | "pressed" | "selected" | "disabled";
  size?: "lg" | "medium (default)" | "sm" | "large" | "medium" | "small";
  layout?: "icon and label (default)" | "icon only" | "icon and label";
  pxFilled20?: React.ReactNode;
  pxRegular24?: React.ReactNode;
  icon?: boolean;
  pxFilled24?: React.ReactNode;
  pxRegular20?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon2?: React.ReactNode;
}
export declare const SplitButton: React.FC<SplitButtonProps>;
export default SplitButton;
