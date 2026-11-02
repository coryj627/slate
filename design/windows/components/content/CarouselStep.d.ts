import * as React from 'react';
export interface CarouselStepProps {
  className?: string;
  style?: React.CSSProperties;
  type?: "inactive" | "active";
  state?: "rest" | "hover" | "pressed";
  showFocus?: boolean;
}
export declare const CarouselStep: React.FC<CarouselStepProps>;
export default CarouselStep;
