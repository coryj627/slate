import * as React from 'react';
export interface TextareaProps {
  className?: string;
  style?: React.CSSProperties;
  style2?: "outline" | "filled darker" | "filled lighter";
  /** Alias of style2 (preferred). */
  appearance?: "outline" | "filled darker" | "filled lighter";
  size?: "sm" | "medium (default)" | "lg" | "small" | "medium" | "large";
  state?: "rest" | "hover" | "pressed" | "focus" | "error" | "disabled" | "read only";
  showResizeHandler?: boolean;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
  /** Placeholder text (Slate addition). */
  placeholder?: string;
}
export declare const Textarea: React.FC<TextareaProps>;
export default Textarea;
