import * as React from 'react';
export interface Calendar3Props {
  className?: string;
  style?: React.CSSProperties;
  direction?: "ltr" | "rtl";
  size?: "12" | "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled" | "light";
}
export declare const Calendar3: React.FC<Calendar3Props>;
export default Calendar3;
