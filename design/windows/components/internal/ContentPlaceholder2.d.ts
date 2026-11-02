import * as React from 'react';
export interface ContentPlaceholder2Props {
  className?: string;
  style?: React.CSSProperties;
  /** Text content; defaults to "SWAP AREA WITH YOUR COMPONENT". */
  text1?: string;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
}
export declare const ContentPlaceholder2: React.FC<ContentPlaceholder2Props>;
export default ContentPlaceholder2;
