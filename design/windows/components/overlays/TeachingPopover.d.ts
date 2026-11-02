import * as React from 'react';
export interface TeachingPopoverProps {
  className?: string;
  style?: React.CSSProperties;
  style2?: "default" | "brand";
  /** Alias of style2 (preferred). */
  appearance?: "default" | "brand";
}
export declare const TeachingPopover: React.FC<TeachingPopoverProps>;
export default TeachingPopover;
