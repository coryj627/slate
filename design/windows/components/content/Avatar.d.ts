import * as React from 'react';
export interface AvatarProps {
  className?: string;
  style?: React.CSSProperties;
  presence?: boolean;
  layout?: "image" | "icon" | "initials";
  size?: "16" | "20" | "24" | "28" | "32" | "36" | "40" | "48" | "56" | "64" | "72" | "96" | "120";
  color?: "neutral" | "brand" | "colored";
  activityRing?: boolean;
  text?: string;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon2?: React.ReactNode;
}
export declare const Avatar: React.FC<AvatarProps>;
export default Avatar;
