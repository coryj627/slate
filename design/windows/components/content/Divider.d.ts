import * as React from 'react';
export interface DividerProps {
  className?: string;
  style?: React.CSSProperties;
  contentSlot?: boolean;
  layout?: "center (default)" | "start" | "end" | "center";
  vertical?: boolean;
  text?: boolean;
  textString?: string;
  icon?: boolean;
  pxIcon20?: React.ReactNode;
}
export declare const Divider: React.FC<DividerProps>;
export default Divider;
