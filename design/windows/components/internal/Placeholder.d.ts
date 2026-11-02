import * as React from 'react';
export interface PlaceholderProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "12" | "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled";
}
export declare const Placeholder: React.FC<PlaceholderProps>;
export default Placeholder;
