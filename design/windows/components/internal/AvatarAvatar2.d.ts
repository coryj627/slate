import * as React from 'react';
export interface AvatarAvatar2Props {
  className?: string;
  style?: React.CSSProperties;
  pxRegular16?: React.ReactNode;
  pxRegular24?: React.ReactNode;
  pxRegular20?: React.ReactNode;
  pxRegular28?: React.ReactNode;
  pxRegular12?: React.ReactNode;
  pxRegular32?: React.ReactNode;
  type?: "image" | "icon" | "initials";
  size?: "16" | "20" | "24" | "28" | "32 (default)" | "36" | "40" | "48" | "56" | "64" | "72" | "96" | "120" | "32";
  pxRegular48?: React.ReactNode;
  initialsString1Max?: string;
  initialsString2Max?: string;
  activityRing?: boolean;
  pxRegularBadgeIcon12?: React.ReactNode;
  badgeIcon?: boolean;
  pxRegularBadgeIcon16?: React.ReactNode;
  pxRegularBadgeIcon24?: React.ReactNode;
  presenceBadge?: boolean;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
}
export declare const AvatarAvatar2: React.FC<AvatarAvatar2Props>;
export default AvatarAvatar2;
