import * as React from 'react';
export interface InputProps {
  className?: string;
  style?: React.CSSProperties;
  iconBefore16pxRegular?: React.ReactNode;
  style2?: "outline" | "filled darker" | "filled lighter" | "underline";
  /** Alias of style2 (preferred). */
  appearance?: "outline" | "filled darker" | "filled lighter" | "underline";
  size?: "sm" | "medium (default)" | "lg" | "small" | "medium" | "large";
  state?: "rest" | "hover" | "pressed" | "focus" | "error" | "disabled" | "read only";
  iconBefore20pxRegular?: React.ReactNode;
  iconBefore24pxRegular?: React.ReactNode;
  iconAfter1?: boolean;
  iconAfter116pxRegular?: React.ReactNode;
  iconAfter120pxRegular?: React.ReactNode;
  iconAfter124pxRegular?: React.ReactNode;
  iconAfter2?: boolean;
  iconBefore?: boolean;
  iconAfter216pxRegular?: React.ReactNode;
  iconAfter224pxRegular?: React.ReactNode;
  iconAfter220pxRegular?: React.ReactNode;
  /** Placeholder text (Slate addition; the kit hard-codes it). */
  placeholder?: string;
}
export declare const Input: React.FC<InputProps>;
export default Input;
