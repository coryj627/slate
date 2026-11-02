import * as React from 'react';
export interface VerticalTabListProps {
  className?: string;
  style?: React.CSSProperties;
  layout?: "icon before" | "text only" | "icon only";
  size?: "medium (default)" | "sm" | "medium" | "small";
  circular?: boolean;
}
export declare const VerticalTabList: React.FC<VerticalTabListProps>;
export default VerticalTabList;
