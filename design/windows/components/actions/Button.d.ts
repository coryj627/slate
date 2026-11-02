import * as React from 'react';
export interface ButtonProps {
  className?: string;
  style?: React.CSSProperties;
  style2?: "primary" | "secondary (default)" | "outline" | "subtle" | "transparent" | "secondary";
  /** Alias of style2 (preferred). */
  appearance?: "primary" | "secondary" | "outline" | "subtle" | "transparent";
  state?: "rest" | "hover" | "pressed" | "selected" | "disabled";
  size?: "lg" | "medium (default)" | "sm" | "large" | "medium" | "small";
  layout?: "icon and label (default)" | "icon only" | "icon and label";
  xOutline24?: React.ReactNode;
  menuButton?: boolean;
  xOutline20?: React.ReactNode;
  xFilled20?: React.ReactNode;
  label?: string;
  focus?: boolean;
  icon?: boolean;
  xFilled24?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
}
export declare const Button: React.FC<ButtonProps>;
export default Button;
