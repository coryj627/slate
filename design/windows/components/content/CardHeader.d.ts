import * as React from 'react';
export interface CardHeaderProps {
  className?: string;
  style?: React.CSSProperties;
  customHeader?: boolean;
  checkbox?: boolean;
  avatar?: boolean;
  headerQuickAction?: boolean;
  subtextString?: string;
  productIcon?: boolean;
  titleString?: string;
  subtext?: boolean;
  pxIcon32?: React.ReactNode;
}
export declare const CardHeader: React.FC<CardHeaderProps>;
export default CardHeader;
