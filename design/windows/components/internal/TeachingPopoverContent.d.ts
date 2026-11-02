import * as React from 'react';
export interface TeachingPopoverContentProps {
  className?: string;
  style?: React.CSSProperties;
  header?: boolean;
  footer?: boolean;
  lastLineString?: string;
  headerTips?: boolean;
  dismiss?: boolean;
  bodyTitle?: boolean;
  titleString?: string;
  bodyString?: string;
  body?: boolean;
  headerContent?: boolean;
  inlineLink?: boolean;
  /** Slate additions: hide the media area, or swap in your own. */
  media?: boolean;
  mediaSwap?: React.ReactNode;
}
export declare const TeachingPopoverContent: React.FC<TeachingPopoverContentProps>;
export default TeachingPopoverContent;
