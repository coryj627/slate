import * as React from 'react';
/** Scroll bar from the Windows UI kit: 2 px thumb at rest, 12 px track with carets on hover. */
export interface ScrollBarProps {
  orientation?: "vertical" | "horizontal";
  /** Length of the bar in px (default 200). */
  length?: number;
  /** Thumb length as a fraction of the track (visible ÷ total content). */
  thumb?: number;
  /** Scroll position, 0–1. Omit to let the bar track its own position. */
  position?: number;
  onChange?: (position: number) => void;
  /** Forces the expanded (hover) look. */
  expanded?: boolean;
  label?: string;
  className?: string;
  style?: React.CSSProperties;
}
export declare function ScrollBar(props: ScrollBarProps): JSX.Element;
