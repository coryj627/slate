import * as React from 'react';
export interface Placeholder3Props {
  className?: string;
  style?: React.CSSProperties;
  image?: React.ReactNode;
  slot?: "1" | "2";
  instance?: React.ReactNode;
  slotBefore?: boolean;
  /** Text content; defaults to "SWAP WITH CONTENT COMPONENT". */
  text1?: string;
}
export declare const Placeholder3: React.FC<Placeholder3Props>;
export default Placeholder3;
