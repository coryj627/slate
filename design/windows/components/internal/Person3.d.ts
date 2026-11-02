import * as React from 'react';
export interface Person3Props {
  className?: string;
  style?: React.CSSProperties;
  size?: "12" | "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled";
}
export declare const Person3: React.FC<Person3Props>;
export default Person3;
