import * as React from 'react';
export interface HorizontalTabListProps {
  className?: string;
  style?: React.CSSProperties;
  layout?: "icon before" | "text only" | "icon only";
  size?: "medium (default)" | "sm" | "medium" | "small";
  circular?: boolean;
}
export declare const HorizontalTabList: React.FC<HorizontalTabListProps>;
export default HorizontalTabList;
