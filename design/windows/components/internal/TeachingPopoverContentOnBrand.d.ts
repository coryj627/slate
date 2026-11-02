import * as React from 'react';
export interface TeachingPopoverContentOnBrandProps {
  className?: string;
  style?: React.CSSProperties;
  header?: boolean;
  bodyTitle?: boolean;
  dismiss?: boolean;
  headerTips?: boolean;
  headerContent?: boolean;
  body?: boolean;
  lastLineString?: string;
  titleString?: string;
  footer?: boolean;
  bodyString?: string;
  inlineLink?: boolean;
}
export declare const TeachingPopoverContentOnBrand: React.FC<TeachingPopoverContentOnBrandProps>;
export default TeachingPopoverContentOnBrand;
