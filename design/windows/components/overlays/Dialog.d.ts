import * as React from 'react';
export interface DialogProps {
  className?: string;
  style?: React.CSSProperties;
  footer?: boolean;
  headerText?: string;
  secondaryButton?: boolean;
  tertiaryButton?: boolean;
  checkbox?: boolean;
  dismiss?: boolean;
  /** "600px" is the standard dialog, drawn 448 px wide (320 to 520, the WinUI width); "320px" is the compact one. */
  size?: "600px" | "320px";
  layout?: "text" | "placeholder";
  contentSwap?: React.ReactNode;
  body?: boolean;
  bodyText?: string;
  /** Primary button text (the kit hard-coded "Take action"). */
  primaryLabel?: string;
  /** Secondary button text (the kit hard-coded "Different action"). */
  secondaryLabel?: string;
  /** Tertiary button text (the kit hard-coded "Tertiary action"). */
  tertiaryLabel?: string;
}
export declare const Dialog: React.FC<DialogProps>;
export default Dialog;
