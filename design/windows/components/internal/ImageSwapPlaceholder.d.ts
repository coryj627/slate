import * as React from 'react';
export interface ImageSwapPlaceholderProps {
  className?: string;
  style?: React.CSSProperties;
  /** Text content; defaults to "40x40". */
  text1?: string;
  /** Text content; defaults to "Use component swap to change". */
  text2?: string;
}
export declare const ImageSwapPlaceholder: React.FC<ImageSwapPlaceholderProps>;
export default ImageSwapPlaceholder;
