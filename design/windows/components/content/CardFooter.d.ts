import * as React from 'react';
export interface CardFooterProps {
  className?: string;
  style?: React.CSSProperties;
  quickAction?: boolean;
  customFooter?: boolean;
  primaryAction?: boolean;
  secondaryAction?: boolean;
  /** Footer button labels (Slate additions). */
  primaryLabel?: string;
  secondaryLabel?: string;
}
export declare const CardFooter: React.FC<CardFooterProps>;
export default CardFooter;
