import * as React from 'react';
export interface PopoverProps {
  className?: string;
  style?: React.CSSProperties;
  contentSwap?: React.ReactNode;
  style2?: "default" | "inverted" | "brand";
  /** Alias of style2 (preferred). */
  appearance?: "default" | "inverted" | "brand";
}
export declare const Popover: React.FC<PopoverProps>;
export default Popover;
