import * as React from 'react';
export interface Person2Props {
  className?: string;
  style?: React.CSSProperties;
  size?: "12" | "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled" | "light";
}
export declare const Person2: React.FC<Person2Props>;
export default Person2;
