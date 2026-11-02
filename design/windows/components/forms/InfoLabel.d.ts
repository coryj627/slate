import * as React from 'react';
export interface InfoLabelProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "md" | "sm" | "lg" | "medium" | "small" | "large";
  /** Swappable nested instance; defaults to the design's. */
  icon2?: React.ReactNode;
}
export declare const InfoLabel: React.FC<InfoLabelProps>;
export default InfoLabel;
