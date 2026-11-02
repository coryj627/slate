import * as React from 'react';
export interface TitleBodyCopyProps {
  className?: string;
  style?: React.CSSProperties;
  showSubheader?: boolean;
  /** Text content; defaults to "Descriptive title". */
  text1?: string;
  /** Text content; defaults to "Description of the content in the carousel.". */
  text2?: string;
}
export declare const TitleBodyCopy: React.FC<TitleBodyCopyProps>;
export default TitleBodyCopy;
