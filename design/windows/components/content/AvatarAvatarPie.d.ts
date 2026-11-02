import * as React from 'react';
export interface AvatarAvatarPieProps {
  className?: string;
  style?: React.CSSProperties;
  layout?: "2 avatars" | "3 avatars";
  size?: "16" | "20" | "24" | "28" | "32 (default)" | "36" | "40" | "48" | "56" | "64" | "72" | "96" | "120" | "32";
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon2?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon3?: React.ReactNode;
}
export declare const AvatarAvatarPie: React.FC<AvatarAvatarPieProps>;
export default AvatarAvatarPie;
