import * as React from 'react';
export interface LabelProps {
  className?: string;
  style?: React.CSSProperties;
  required?: boolean;
  type?: "regular (default)" | "semibold" | "regular";
  size?: "sm" | "md" | "lg" | "small" | "medium" | "large";
  disabled?: "off" | "on" | boolean;
  text?: string;
  /** Text content; defaults to "*". */
  text1?: string;
}
export declare const Label: React.FC<LabelProps>;
export default Label;
