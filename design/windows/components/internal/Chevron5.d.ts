import * as React from 'react';
export interface Chevron5Props {
  className?: string;
  style?: React.CSSProperties;
  expand?: boolean;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
}
export declare const Chevron5: React.FC<Chevron5Props>;
export default Chevron5;
