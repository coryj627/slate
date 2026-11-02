import * as React from 'react';
export interface CaretProps {
  className?: string;
  style?: React.CSSProperties;
  direction?: "down" | "up" | "left" | "right" | "down right";
  size?: "12" | "16" | "20" | "24";
  theme?: "regular" | "filled";
}
export declare const Caret: React.FC<CaretProps>;
export default Caret;
