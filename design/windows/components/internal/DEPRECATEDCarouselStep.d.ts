import * as React from 'react';
export interface DEPRECATEDCarouselStepProps {
  className?: string;
  style?: React.CSSProperties;
  type?: "inactive" | "active";
  state?: "rest" | "hover" | "pressed";
  showFocus?: boolean;
}
export declare const DEPRECATEDCarouselStep: React.FC<DEPRECATEDCarouselStepProps>;
export default DEPRECATEDCarouselStep;
