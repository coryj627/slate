import * as React from 'react';
export interface SliderProps {
  className?: string;
  style?: React.CSSProperties;
  ticks?: boolean;
  size?: "medium (default)" | "sm" | "medium" | "small";
  state?: "rest" | "hover" | "pressed" | "disabled" | "focus";
}
export declare const Slider: React.FC<SliderProps>;
export default Slider;
