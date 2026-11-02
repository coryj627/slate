import * as React from 'react';
export interface RatingProps {
  className?: string;
  style?: React.CSSProperties;
  ratingQuantity?: string;
  type?: "interactive" | "display";
  size?: "sm" | "md" | "lg" | "xl" | "small" | "medium" | "large" | "extra large";
  compact?: boolean;
  shape?: "stars" | "circles" | "square";
  /** Text content; defaults to "3.5". */
  text1?: string;
  /** Text content; defaults to "·". */
  text2?: string;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon2?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon3?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon4?: React.ReactNode;
}
export declare const Rating: React.FC<RatingProps>;
export default Rating;
