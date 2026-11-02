import * as React from 'react';
export interface CardMediaProps {
  className?: string;
  style?: React.CSSProperties;
  checkbox?: boolean;
  aspectRatio?: "16:9" | "4:3" | "flex (default)" | "flex";
  mediaIcon?: boolean;
  pxIcon20?: React.ReactNode;
}
export declare const CardMedia: React.FC<CardMediaProps>;
export default CardMedia;
