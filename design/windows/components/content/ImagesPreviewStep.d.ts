import * as React from 'react';
export interface ImagesPreviewStepProps {
  className?: string;
  style?: React.CSSProperties;
  state?: "selected" | "rest";
}
export declare const ImagesPreviewStep: React.FC<ImagesPreviewStepProps>;
export default ImagesPreviewStep;
