import * as React from 'react';
export interface ContentHeaderProps {
  className?: string;
  style?: React.CSSProperties;
  layout?: "selection" | "content";
  style2?: "regular" | "semibold";
  /** Alias of style2 (preferred). */
  appearance?: "regular" | "semibold";
  sorting?: boolean;
  focus?: boolean;
  headerContent?: string;
  /** Swappable nested instance; defaults to the design's. */
  icon2?: React.ReactNode;
}
export declare const ContentHeader: React.FC<ContentHeaderProps>;
export default ContentHeader;
