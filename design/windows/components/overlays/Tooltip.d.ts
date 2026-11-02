import * as React from 'react';
export interface TooltipProps {
  className?: string;
  style?: React.CSSProperties;
  style2?: "default" | "brand" | "inverted";
  /** Alias of style2 (preferred). */
  appearance?: "default" | "brand" | "inverted";
  label?: string;
}
export declare const Tooltip: React.FC<TooltipProps>;
export default Tooltip;
