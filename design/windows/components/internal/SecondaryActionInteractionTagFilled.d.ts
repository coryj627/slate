import * as React from 'react';
export interface SecondaryActionInteractionTagFilledProps {
  className?: string;
  style?: React.CSSProperties;
  state?: "rest" | "hover" | "pressed" | "disabled";
  selected?: boolean;
  focus?: boolean;
}
export declare const SecondaryActionInteractionTagFilled: React.FC<SecondaryActionInteractionTagFilledProps>;
export default SecondaryActionInteractionTagFilled;
