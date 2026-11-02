import * as React from 'react';
export interface TagPickerTagPickerProps {
  className?: string;
  style?: React.CSSProperties;
  tags?: boolean;
  style2?: "outline" | "transparent" | "filled darker" | "filled lighter";
  /** Alias of style2 (preferred). */
  appearance?: "outline" | "transparent" | "filled darker" | "filled lighter";
  size?: "medium (default)" | "lg" | "extra large" | "medium" | "large";
  state?: "rest" | "hover" | "pressed" | "focused" | "disabled";
  expanded?: boolean;
  secondaryAction?: boolean;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon2?: React.ReactNode;
}
export declare const TagPickerTagPicker: React.FC<TagPickerTagPickerProps>;
export default TagPickerTagPicker;
