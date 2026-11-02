import * as React from 'react';
export interface ContentLeftSmallProps {
  className?: string;
  style?: React.CSSProperties;
  rightIcon2?: boolean;
  rightIconSlot?: boolean;
  leftIconSlot?: boolean;
  text?: string;
  rightIcon1?: boolean;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
}
export declare const ContentLeftSmall: React.FC<ContentLeftSmallProps>;
export default ContentLeftSmall;
