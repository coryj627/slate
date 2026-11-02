import * as React from 'react';
/**
 * Title bar caption button (minimize, maximize, restore, close) from the Windows UI kit. 46 px wide.
 */
export interface CaptionButtonProps {
  kind?: "minimize" | "maximize" | "restore" | "close";
  /** Forces a visual state; omit for live hover and press. */
  state?: "rest" | "hover" | "pressed";
  /** Window is inactive: the glyph dims. */
  inactive?: boolean;
  /** Accessible name and tooltip. Defaults to Minimize, Maximize, Restore or Close. */
  label?: string;
  onClick?: (e: React.MouseEvent<HTMLButtonElement>) => void;
  /** Title bar height: 48 (tall, Slate) or 32. */
  height?: number;
  buttonRef?: React.Ref<HTMLButtonElement>;
  className?: string;
  style?: React.CSSProperties;
}
export declare function CaptionButton(props: CaptionButtonProps): JSX.Element;
