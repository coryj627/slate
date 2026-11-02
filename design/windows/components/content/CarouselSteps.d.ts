import * as React from 'react';
export interface CarouselStepsProps {
  className?: string;
  style?: React.CSSProperties;
  steps?: "1 (default)" | "2" | "3" | "4" | "5" | "1";
  showFocus?: boolean;
}
export declare const CarouselSteps: React.FC<CarouselStepsProps>;
export default CarouselSteps;
