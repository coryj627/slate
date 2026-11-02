import * as React from 'react';
export interface ClipboardTaskProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "16" | "20" | "24";
  theme?: "regular" | "filled";
}
export declare const ClipboardTask: React.FC<ClipboardTaskProps>;
export default ClipboardTask;
