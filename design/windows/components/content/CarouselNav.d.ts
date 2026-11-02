import * as React from 'react';
export interface CarouselNavProps {
  className?: string;
  style?: React.CSSProperties;
  mode?: "stepsdefault" | "stepsautoplay" | "imagepreviewautoplay" | "imagepreview default";
}
export declare const CarouselNav: React.FC<CarouselNavProps>;
export default CarouselNav;
