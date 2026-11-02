import * as React from 'react';
export interface CarouselProps {
  className?: string;
  style?: React.CSSProperties;
  layout?: "over content" | "outside content";
  chevronPlacement?: "flexible to edges" | "grouped to steps" | "centered to content";
  pauseButton?: "in nav" | "on content click";
  navType?: "steps" | "image preview";
  title?: boolean;
}
export declare const Carousel: React.FC<CarouselProps>;
export default Carousel;
