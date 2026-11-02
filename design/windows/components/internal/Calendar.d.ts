import * as React from 'react';
export interface CalendarProps {
  className?: string;
  style?: React.CSSProperties;
  direction?: "ltr" | "rtl";
  size?: "12" | "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled" | "light";
}
export declare const Calendar: React.FC<CalendarProps>;
export default Calendar;
