import * as React from 'react';
export interface Placeholder5Props {
  className?: string;
  style?: React.CSSProperties;
  slot?: "1" | "2";
  image?: React.ReactNode;
  instance?: React.ReactNode;
  slotBefore?: boolean;
  /** Text content; defaults to "SWAP WITH CONTENT COMPONENT". */
  text1?: string;
}
export declare const Placeholder5: React.FC<Placeholder5Props>;
export default Placeholder5;
