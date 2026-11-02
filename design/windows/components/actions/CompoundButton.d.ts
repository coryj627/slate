import * as React from 'react';
export interface CompoundButtonProps {
  className?: string;
  style?: React.CSSProperties;
  image?: boolean;
  secondaryText?: boolean;
  primaryString?: string;
  secondaryString?: string;
  style2?: "primary" | "secondary (default)" | "outline" | "subtle" | "transparent" | "secondary";
  /** Alias of style2 (preferred). */
  appearance?: "primary" | "secondary" | "outline" | "subtle" | "transparent";
  state?: "rest" | "hover" | "pressed" | "selected" | "disabled";
  size?: "medium (default)" | "sm" | "lg" | "medium" | "small" | "large";
  focus?: boolean;
  swap40x40Img?: React.ReactNode;
}
export declare const CompoundButton: React.FC<CompoundButtonProps>;
export default CompoundButton;
