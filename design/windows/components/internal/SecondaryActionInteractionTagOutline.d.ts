import * as React from 'react';
export interface SecondaryActionInteractionTagOutlineProps {
  className?: string;
  style?: React.CSSProperties;
  state?: "rest" | "hover" | "pressed" | "disabled";
  selected?: boolean;
  focus?: boolean;
}
export declare const SecondaryActionInteractionTagOutline: React.FC<SecondaryActionInteractionTagOutlineProps>;
export default SecondaryActionInteractionTagOutline;
