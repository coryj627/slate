import * as React from 'react';
export interface ContentLeftMediumProps {
  className?: string;
  style?: React.CSSProperties;
  leftIconSlot?: boolean;
  text?: string;
  rightIconSlot?: boolean;
  rightIcon1?: boolean;
  rightIcon2?: boolean;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
}
export declare const ContentLeftMedium: React.FC<ContentLeftMediumProps>;
export default ContentLeftMedium;
