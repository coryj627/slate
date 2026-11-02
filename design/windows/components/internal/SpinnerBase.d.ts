import * as React from 'react';
export interface SpinnerBaseProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "x-tiny" | "tiny" | "xs" | "sm" | "md" | "lg" | "xl" | "huge" | "tiny 01" | "tiny 02" | "tiny 03" | "x-small 01" | "x-small 02" | "x-small 03" | "small 01" | "small 02" | "small 03" | "medium 01" | "medium 02" | "medium 03" | "large 01" | "large 02" | "large 03" | "x-large 01" | "x-large 02" | "x-large 03" | "huge 01" | "huge 02" | "huge 03" | "x-tiny 01" | "x-tiny 02" | "x-tiny 03" | "x tiny" | "extra small" | "small" | "medium" | "large" | "extra large" | "x small 01" | "x small 02" | "x small 03" | "x large 01" | "x large 02" | "x large 03" | "x tiny 01" | "x tiny 02" | "x tiny 03";
}
export declare const SpinnerBase: React.FC<SpinnerBaseProps>;
export default SpinnerBase;
