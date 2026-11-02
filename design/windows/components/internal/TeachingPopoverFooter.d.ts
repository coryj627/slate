import * as React from 'react';
export interface TeachingPopoverFooterProps {
  className?: string;
  style?: React.CSSProperties;
  carouselSteps?: boolean;
  count?: boolean;
  type?: "multi" | "single";
  countString?: string;
}
export declare const TeachingPopoverFooter: React.FC<TeachingPopoverFooterProps>;
export default TeachingPopoverFooter;
