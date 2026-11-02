import * as React from 'react';
export interface CarouselPlayControlProps {
  className?: string;
  style?: React.CSSProperties;
  state?: "pause" | "play";
}
export declare const CarouselPlayControl: React.FC<CarouselPlayControlProps>;
export default CarouselPlayControl;
