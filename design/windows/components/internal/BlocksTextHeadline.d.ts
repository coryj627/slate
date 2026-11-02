import * as React from 'react';
export interface BlocksTextHeadlineProps {
  className?: string;
  style?: React.CSSProperties;
  preview?: boolean;
  style2?: "xxl headline" | "xl headline" | "l headline" | "m headline";
  /** Alias of style2 (preferred). */
  appearance?: "xxl headline" | "xl headline" | "l headline" | "m headline";
  /** Text content; defaults to "XXL Headline 56". */
  text1?: string;
  /** Text content; defaults to "Preview". */
  text2?: string;
  /** Text content; defaults to "Fluent UI's robust, up-to-date controls are built with the React framework etc. Look through the control list to see the building blocks that are available using Fluent UI.". */
  text3?: string;
}
export declare const BlocksTextHeadline: React.FC<BlocksTextHeadlineProps>;
export default BlocksTextHeadline;
