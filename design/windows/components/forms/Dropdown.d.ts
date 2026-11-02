import * as React from 'react';
export interface DropdownProps {
  className?: string;
  style?: React.CSSProperties;
  appearance?: "outline" | "transparent" | "fill lighter" | "fill darker";
  size?: "sm" | "md" | "lg" | "small" | "medium" | "large";
  expanded?: boolean;
  /** Placeholder text (Slate addition). */
  placeholder?: string;
}
export declare const Dropdown: React.FC<DropdownProps>;
export default Dropdown;
