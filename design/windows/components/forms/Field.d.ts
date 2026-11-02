import * as React from 'react';
export interface FieldProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "medium (default)" | "sm" | "lg" | "medium" | "small" | "large";
  instance?: React.ReactNode;
  helperText?: boolean;
  statusText?: boolean;
  label?: boolean;
  /** Text content; defaults to "Error text". */
  text1?: string;
  /** Text content; defaults to "Helper text". */
  text2?: string;
  /** Label text (Slate addition). */
  labelText?: string;
}
export declare const Field: React.FC<FieldProps>;
export default Field;
