import * as React from 'react';
export interface SwatchPickerProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "extrasmall" | "sm" | "medium (default)" | "lg" | "extra small" | "small" | "medium" | "large";
  layout?: "grid" | "row";
  spacing?: "md" | "sm" | "medium" | "small";
}
export declare const SwatchPicker: React.FC<SwatchPickerProps>;
export default SwatchPicker;
