import * as React from 'react';
export interface ToastProps {
  className?: string;
  style?: React.CSSProperties;
  pxIcon20?: React.ReactNode;
  titleString?: string;
  endContent?: boolean;
  timestampString?: string;
  additionalContent?: boolean;
  quickActions?: boolean;
  additionalAction?: boolean;
  secondaryString?: string;
  tertiaryInformation?: boolean;
  progressString?: string;
  tertiaryString?: string;
  progressBar?: boolean;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon2?: React.ReactNode;
}
export declare const Toast: React.FC<ToastProps>;
export default Toast;
