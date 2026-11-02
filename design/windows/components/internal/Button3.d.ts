import * as React from 'react';
export interface Button3Props {
  className?: string;
  style?: React.CSSProperties;
  icon?: React.ReactNode;
  variant?: "primary" | "destructive" | "inverse" | "success" | "figjam" | "secondary" | "secondary destruct" | "link" | "link danger" | "ghost";
  iconLead?: "false" | "left-aligned" | "center-aligned" | "left aligned" | "center aligned";
  label?: string;
  size?: "default" | "lg" | "wide" | "large";
  disabled?: boolean;
  hotkey?: boolean;
  state?: "default" | "active" | "focused" | "hover";
  /** Text content; defaults to "⏎". */
  text1?: string;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
}
export declare const Button3: React.FC<Button3Props>;
export default Button3;
