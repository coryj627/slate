import * as React from 'react';
export interface FilterProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "12" | "16" | "20" | "24" | "28" | "32";
  theme?: "regular" | "filled" | "light";
}
export declare const Filter: React.FC<FilterProps>;
export default Filter;
