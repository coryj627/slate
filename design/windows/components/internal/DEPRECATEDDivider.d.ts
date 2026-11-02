import * as React from 'react';
export interface DEPRECATEDDividerProps {
  className?: string;
  style?: React.CSSProperties;
  layout?: "default" | "with content" | "with icon";
  style2?: "default" | "subtle" | "strong" | "brand";
  /** Alias of style2 (preferred). */
  appearance?: "default" | "subtle" | "strong" | "brand";
  inset?: "block (default)" | "inset" | "block";
  contentAlign?: "center (default)" | "start" | "end" | "center";
  orientation?: "horizontal";
}
export declare const DEPRECATEDDivider: React.FC<DEPRECATEDDividerProps>;
export default DEPRECATEDDivider;
