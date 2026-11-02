import * as React from 'react';
export interface AvatarAvatarGroupProps {
  className?: string;
  style?: React.CSSProperties;
  layout?: "spread" | "stack";
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon2?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon3?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon4?: React.ReactNode;
}
export declare const AvatarAvatarGroup: React.FC<AvatarAvatarGroupProps>;
export default AvatarAvatarGroup;
